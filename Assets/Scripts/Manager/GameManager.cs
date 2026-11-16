using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using DG.Tweening;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

[Serializable]
public enum GameState { Menu, Prepare, Game, Win }

[DefaultExecutionOrder(-1)]
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("References")]
    public PoolingManager poolManager;
    [SerializeField] private PlayersManager playersManager;

    [Header("UI")]
    [SerializeField] private UIManager uiManager;
    [Tooltip("Between-rounds egg score. Optional: found in the scene if left empty.")]
    [SerializeField] private RoundScoreboard scoreboard;
    public CinemachineVerticalRig2D cameraRig;

    [Header("Camera Auto-Move")]
    [Tooltip("Seconds after the round starts before the camera begins rising.")]
    [SerializeField] private float cameraStartDelay = 3.5f;

    [Tooltip("FALLBACK. Solo se usa en escenas sin ProgressionManager.\n"
             + "Con progresión, la velocidad base de la cámara vive en el asset ProgressionValues, junto "
             + "al resto del tuning — cambiar este campo no hace nada en Main_Level.")]
    [FormerlySerializedAs("cameraInitialSpeed")]
    [FormerlySerializedAs("cameraBaseSpeed")]
    [SerializeField] private float cameraBaseSpeedFallback = 0.65f;

    [Header("Progression")]
    [Tooltip("Optional. Without it the game runs exactly as before, with every progression scalar at 100%.")]
    [SerializeField] private ProgressionManager progression;


    private float _cameraDelayTimer;

    [Header("Game Variables")]
    public Action OnGame;
    public Action OnGameEnd;
    /// <summary>Fired when the game enters Prepare state (countdown started).</summary>
    public Action OnPrepare;

    public List<float> playersPosX;
    private Coroutine checkerCoroutine;
    [SerializeField] private PlayerController[] inGamePlayers = new PlayerController[4];
    [SerializeField] private PlayerController[] playersAlive  = new PlayerController[4];
    [SerializeField] private int playersCheckDelay;

    [Header("End Game")]
    [SerializeField, Min(0f)] private float resultsHoldTime = 1.25f;

    [Tooltip("Seconds on the round winner (camera on them) before the next round or the match result.")]
    [SerializeField, Min(0f)] private float betweenRoundsTime = 2f;

    private PlayerController winner    = null;
    private bool matchWon              = false;
    private bool triggerStartGame      = false;
    private bool _repositionOnReset    = false;
    private const float tieThresHold   = 0.5f;
    private Tween winSequence;
    [SerializeField] private int currPlayersAlive;

    public GameState gameState = GameState.Menu;

    /// <summary>
    /// True only while the round is being played and still undecided. False
    /// from the moment one chicken is left (the tie window before the win
    /// screen included), through the win screen, until the next round starts.
    /// </summary>
    public bool IsRoundLive => gameState == GameState.Game && currPlayersAlive > 1;

    /// <summary>
    /// The pause menu may open during a round, its countdown and the win
    /// screen between rounds (playtest, oct 2026). Not once the match is won:
    /// the results screen goes back to the menu on its own.
    /// </summary>
    public bool CanPause => gameState == GameState.Game
                         || gameState == GameState.Prepare
                         || (gameState == GameState.Win && !matchWon);
    public PlayerController[] InGamePlayers
    {
        get
        {
            var copy = new PlayerController[4];
            for (int i = 0; i < inGamePlayers.Length; i++)
                copy[i] = inGamePlayers[i];
            return copy;
        }
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);

        if (scoreboard == null)
            scoreboard = FindFirstObjectByType<RoundScoreboard>(FindObjectsInactive.Include);
    }

    private void OnDestroy()
    {
        // Leaving from the pause menu on the win screen: the pending "next
        // round" call must not run against this destroyed scene.
        winSequence?.Kill();
    }

    private void Start()
    {
        // BGM is intentionally not started here.
        // MainMenuController starts it on first load (A1 → B).
        // On return from Main_Level the AudioManager (DontDestroyOnLoad) carries
        // whatever was set before the scene transition — no action needed.
    }

    private void Update()
    {
        switch (gameState)
        {
            case GameState.Menu:    OnMenuState();    break;
            case GameState.Prepare: OnPrepareState(); break;
            case GameState.Game:    OnGameState();    break;
            case GameState.Win:     OnWinState();     break;
        }
    }

    // ── State machine ─────────────────────────────────────────────────────────

    private void ChangeGameState(GameState newGameState)
    {
        gameState = newGameState;
        switch (newGameState)
        {
            case GameState.Prepare:
                OnPrepare?.Invoke();
                break;
            case GameState.Game:
                OnGame?.Invoke();
                break;
            case GameState.Menu:
            case GameState.Win:
                OnGameEnd?.Invoke();
                EndRoundEffects();
                break;
        }
    }

    /// <summary>
    /// The round stopped counting (someone won, or back to the menu). Anything
    /// still running on its own timer (POW countdown, falling anvil, lit bomb…)
    /// subscribes here and stops, so nothing goes off over the win screen or
    /// spills into the next round. Static: those effects are spawned at runtime
    /// and should not have to look for the GameManager.
    /// </summary>
    public static event Action RoundEnded;

    private void EndRoundEffects()
    {
        // Power-ups on the chickens (hot potato, teleport mid-way…) end now
        // too: a potato in the winner's hands would otherwise go off on the
        // win screen and kill them.
        foreach (PlayerController player in inGamePlayers)
        {
            if (player != null) PlayerItemState.EndAll(player.gameObject);
        }

        RoundEnded?.Invoke();
    }

    // Enter Play Mode without domain reload keeps statics alive between sessions.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRoundEvents() => RoundEnded = null;

    // Menu = the short wait between the level loading and the first countdown.
    // A won match goes back to the main menu, so the level never comes back here.
    private void OnMenuState() => cameraRig.canMove = false;

    private void OnPrepareState()
    {
        foreach (PlayerController player in inGamePlayers)
        {
            if (player == null) continue;

            if (!player.gameObject.activeSelf)
                player.gameObject.SetActive(true);

            if (triggerStartGame)
                ResetPlayer(player, _repositionOnReset);
        }

        if (!triggerStartGame) return;

        winner = null;
        poolManager.ResetPool();

        cameraRig.canMove = false;
        cameraRig.ResetToGameplay();

        // Reset the speed and delay timer for this new round.
        ResetCameraSpeed();

        // The match is first-to-N: progression must restart at phase 1 every round.
        progression?.ResetForRound();

        uiManager.StartInitialGameSequence(() =>
        {
            ChangeGameState(GameState.Game);
            checkerCoroutine = StartCoroutine(CheckPlayersInGame());
            AudioManager.Instance.PlayMusic("Game");
            AudioManager.Instance.PlaySound("game_start");
        }, prevCallback: null);

        triggerStartGame   = false;
        _repositionOnReset = false;
    }

    private void OnGameState()
    {
        cameraRig.canMove = true;

        for (int i = 0; i < playersAlive.Length; i++)
        {
            if (playersAlive[i] != null)
                playersAlive[i].isOnGame = true;
        }

        // Ticked from here so the progression system inherits the game state and
        // pause handling, and keeps a single deterministic call site per frame.
        progression?.Tick(Time.deltaTime);

        TickCameraAutoMove();
    }

    private void OnWinState() { }

    // ── Camera auto-move ──────────────────────────────────────────────────────

    /// <summary>
    /// Resets the start delay. Call at the start of each round.
    /// </summary>
    private void ResetCameraSpeed()
    {
        _cameraDelayTimer = cameraStartDelay;
    }

    /// <summary>
    /// Called every frame while in GameState.Game.
    /// Waits for the initial delay, then pushes MaxHeightReached upward at the
    /// speed of the current progression phase.
    ///
    /// The camera does not accelerate on its own any more: speed is
    /// base * phase, and a ribbon break momentarily knocks it down to zero
    /// before it climbs back up (the sawtooth in the design doc).
    /// Player-height catch-up is handled by CinemachineVerticalRig2D and is not
    /// affected by the recovery, so a player above the view is never cut off.
    /// </summary>
    private void TickCameraAutoMove()
    {
        if (_cameraDelayTimer > 0f)
        {
            _cameraDelayTimer -= Time.deltaTime;
            return;
        }

        // The base speed lives in ProgressionValues so the whole tuning surface
        // is one asset. The serialized field is only the no-progression fallback.
        ProgressionValuesSO values = progression != null ? progression.Values : null;

        float baseSpeed  = values != null ? values.cameraBaseSpeed : cameraBaseSpeedFallback;
        float phaseScale = ProgressionManager.Get(ProgressionVar.CameraSpeed);
        float recovery   = progression != null ? progression.CameraSpeedFactor : 1f;

        cameraRig.MaxHeightReached += baseSpeed * phaseScale * recovery * Time.deltaTime;
    }

    // ── Player management ─────────────────────────────────────────────────────

    /// <summary>
    /// First-to-N of this match: what the host picked in the lobby. A map
    /// opened directly in the editor uses the default.
    /// </summary>
    public int RoundsToWin => Mathf.Max(1, MatchSetup.RoundsToWin);

    /// <param name="slot">Player index to take (lobby room). -1 = first free.</param>
    public void AddPlayer(PlayerController player, Vector2 startPos, PlayerMaterial playerMat, int slot = -1)
    {
        player.onDeath       = OnPlayersDeath;
        player.startPosition = startPos;
        player.SetMaterials(playerMat);
        AddInGamePlayer(player, slot);

        SimEvents.Raise(SimEvent.PlayerJoined, startPos, player);
    }

    private void AddInGamePlayer(PlayerController player, int slot = -1)
    {
        // The lobby room decides the index, so "Player 3" stays Player 3 even
        // if room 2 was left empty.
        if (slot >= 0 && slot < inGamePlayers.Length && inGamePlayers[slot] == null)
        {
            inGamePlayers[slot] = player;
            player.playerIndex  = slot;
            player.OnPlayerIndexAssigned();
            return;
        }

        for (int i = 0; i < inGamePlayers.Length; i++)
        {
            if (inGamePlayers[i] != null) continue;

            inGamePlayers[i]   = player;
            player.playerIndex = i;
            player.OnPlayerIndexAssigned();
            return;
        }
    }

    private void ResetPlayer(PlayerController player, bool repositionPlayer = false)
    {
        player.DropBlock();
        player.isOnGame = false;

        // Power-ups never carry over to the next round. Ended explicitly here
        // instead of trusting each state's Update to notice isOnGame dropped.
        PlayerItemState.EndAll(player.gameObject);

        // A dead chicken has no valid position (it stays hidden where it died),
        // so it always goes back to its start point, also on the way to the menu.
        bool wasDead = !player.IsAlive;
        player.SetAlive(true);

        if (repositionPlayer || wasDead)
            player.TeleportTo(player.startPosition);
    }

    private void OnPlayersDeath(PlayerController player)
    {
        // The round is already decided: a death on the win screen (the winner
        // falling out of view, anything still in the air) must not count, or it
        // re-runs CheckWinner and plays the win sequence a second time.
        if (gameState == GameState.Win) return;

        // Two killers in the same step (anvil + kill zone) must count one death.
        if (!player.IsAlive) return;

        Vector2 deathPosition = player.transform.position;

        // A dead chicken keeps no power-up (design, oct 2026). The hot potato
        // goes off on the spot, so it must detonate before the body is switched off.
        if (player.TryGetComponent(out HotPotatoState potato))
            potato.DetonateOnCarrierDeath();
        PlayerItemState.EndAll(player.gameObject);

        // Switched off where it died, not moved to a "death zone": a body parked
        // somewhere was still alive for teleports and other target sweeps.
        player.DropBlock();
        player.SetAlive(false);

        playersAlive[player.playerIndex] = null;

        SimEvents.Raise(SimEvent.PlayerDied, deathPosition, player);
        cameraRig.DoDeathShake();

        FeatherVFXController.Instance?.EmitDeath(deathPosition);

        CheckWinner();
    }

    // ── Start sequence ────────────────────────────────────────────────────────

    /// <summary>
    /// First countdown of the match. Called by PlayersManager once the lobby's
    /// players are seated (needs ≥ 2). Later rounds start on their own.
    /// </summary>
    public void TriggerStartSequence()
    {
        if (gameState != GameState.Menu) return;
        BeginStartSequence();
    }

    /// <summary>
    /// Internal start sequence shared by the button flow and the between-rounds flow.
    /// </summary>
    private void BeginStartSequence(bool repositionPlayers = false)
    {
        for (int i = 0; i < inGamePlayers.Length; i++)
            playersAlive[i] = inGamePlayers[i];

        currPlayersAlive = CountJoinedPlayers();

        if (currPlayersAlive < 2) return;

        matchWon           = false;
        triggerStartGame   = true;
        _repositionOnReset = repositionPlayers;
        ChangeGameState(GameState.Prepare);
    }

    private int CountJoinedPlayers()
    {
        int count = 0;
        for (int i = 0; i < inGamePlayers.Length; i++)
        {
            if (inGamePlayers[i] != null) count++;
        }
        return count;
    }

    // ── Winner / rank logic ───────────────────────────────────────────────────

    private IEnumerator CheckPlayersInGame()
    {
        while (gameState == GameState.Game)
        {
            if (currPlayersAlive <= 1) break;

            playersPosX.Clear();

            // playersAlive has holes (a dead chicken, or an empty lobby room
            // between two players): collect the live ones, not the first N slots.
            PlayerController[] remainingPlayers = new PlayerController[currPlayersAlive];
            int found = 0;
            for (int i = 0; i < playersAlive.Length && found < remainingPlayers.Length; i++)
            {
                if (playersAlive[i] == null) continue;
                remainingPlayers[found++] = playersAlive[i];
                playersPosX.Add(playersAlive[i].transform.position.x);
            }
            if (found < remainingPlayers.Length)
                Array.Resize(ref remainingPlayers, found);

            OrderPlayers(remainingPlayers);

            yield return new WaitForSeconds(playersCheckDelay);
        }

        if (checkerCoroutine != null)
            StopCoroutine(checkerCoroutine);
    }

    private void OrderPlayers(PlayerController[] remainingPlayers)
    {
        int total = remainingPlayers.Length;

        Array.Sort(remainingPlayers, (p1, p2) =>
            p2.transform.position.y.CompareTo(p1.transform.position.y));

        for (int i = 0; i < total; i++)
        {
            GameStatus status = GetPlayerStatus(i, total);
            remainingPlayers[i].SetGameRank(status);

#if UNITY_EDITOR
            // Editor only: in a build every Debug.Log captures a stack trace.
            Debug.Log($"Posición: {i + 1} | Jugador: {remainingPlayers[i].name} | Estado: {status}");
#endif
        }
    }

    private GameStatus GetPlayerStatus(int playerRank, int totalPlayers)
    {
        if (playerRank == 0)                return GameStatus.Winning;
        if (playerRank == totalPlayers - 1) return GameStatus.Losing;
        return GameStatus.Neutral;
    }

    private void CheckWinner()
    {
        currPlayersAlive = 0;
        for (int i = 0; i < playersAlive.Length; i++)
        {
            if (playersAlive[i] == null) continue;
            currPlayersAlive++;
            winner = playersAlive[i];
        }

        if (currPlayersAlive == 1)
        {
            StopCheckerCoroutine();
            cameraRig.FocusWinner(winner.transform);

            DOVirtual.DelayedCall(tieThresHold, () =>
            {
                if (gameState == GameState.Win) return;
                DoWin();
            }, false);
        }
        else if (currPlayersAlive == 0)
        {
            StopCheckerCoroutine();
            cameraRig.StopFocusWinner();
            ChangeGameState(GameState.Win);

            // Tie: nobody scores, next round.
            matchWon = false;
            if (scoreboard != null) scoreboard.Show(inGamePlayers, RoundsToWin);
            winSequence?.Kill();
            winSequence = DOVirtual.DelayedCall(betweenRoundsTime, () => CheckGameWon(false), false);
        }
    }

    private void DoWin()
    {
        if (gameState == GameState.Win) return;

        ChangeGameState(GameState.Win);
        winner.roundsWon++;
        cameraRig.FocusWinner(winner.transform);
        winSequence?.Kill();

        // First-to-N, N picked in the lobby. No score UI in the level any more:
        // the match result is decided here, from the chickens' own round count.
        bool wonGame = winner.roundsWon >= RoundsToWin;
        matchWon = wonGame;
        if (scoreboard != null) scoreboard.Show(inGamePlayers, RoundsToWin);

        // Match won: menu music entering at "Victory C" (GameState 1 in
        // event:/BGM/Menu) instead of from the top of the track.
        if (wonGame)
            AudioManager.Instance.PlayMusic("BGM_Menu_B", gameState: 1);
        else
            AudioManager.Instance.PlaySound("win_round");

        winSequence = DOVirtual.DelayedCall(betweenRoundsTime, () => CheckGameWon(wonGame), false);
    }

    private void CheckGameWon(bool wonGame)
    {
        if (wonGame)
        {
            DOVirtual.DelayedCall(resultsHoldTime, () =>
            {
                // The transition service is a DontDestroyOnLoad singleton that only
                // spawns in the MainMenu scene. Playing a level directly in the
                // editor skips it, so fall back to a plain load instead of dying
                // on a null Instance inside this tween callback.
                if (SceneTransitionService.Instance != null)
                    SceneTransitionService.Instance.LoadMenu();
                else
                    SceneManager.LoadScene("MainMenu");
            }, false);

            return;
        }

        // New round — starts on its own.
        if (scoreboard != null) scoreboard.Hide();
        cameraRig.ResetToGameplay();
        BeginStartSequence(repositionPlayers: true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void StopCheckerCoroutine()
    {
        if (checkerCoroutine == null) return;
        StopCoroutine(checkerCoroutine);
        checkerCoroutine = null;
    }
}

/// <summary>
/// Represents the current in-game rank of a player during an active round.
/// Used by GameManager to assign difficulty-scaled blocks via PoolingManager.
/// </summary>
public enum GameStatus { Winning, Neutral, Losing }