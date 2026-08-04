using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using DG.Tweening;
using System.Collections;
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
    public CinemachineVerticalRig2D cameraRig;

    [Header("Camera Auto-Move")]
    [Tooltip("Seconds after the round starts before the camera begins rising.")]
    [SerializeField] private float cameraStartDelay = 3.5f;

    [Tooltip("Upward speed of the camera at phase 1, in units per second.\n"
             + "The camera no longer accelerates on its own: all acceleration comes from the progression "
             + "phases (+6% each), so this value sets the pace of the whole round and has to be re-tuned.")]
    [FormerlySerializedAs("cameraInitialSpeed")]
    [SerializeField] private float cameraBaseSpeed = 0.15f;

    [Header("Progression")]
    [Tooltip("Optional. Without it the game runs exactly as before, with every progression scalar at 100%.")]
    [SerializeField] private ProgressionManager progression;


    private float _cameraDelayTimer;

    [Header("Game Variables")]
    public Action OnGame;
    public Action OnGameEnd;
    /// <summary>Fired when the game enters Prepare state (countdown started).</summary>
    public Action OnPrepare;
    public Action OnGameStarting;
    /// <summary>Fired whenever the number of joined players changes.</summary>
    public Action<int> OnPlayersCountChanged;

    public List<float> playersPosX;
    private Coroutine checkerCoroutine;
    [SerializeField] private PlayerController[] inGamePlayers = new PlayerController[4];
    [SerializeField] private PlayerController[] playersAlive  = new PlayerController[4];
    [SerializeField] private Transform deathPos;
    [SerializeField] private int playersCheckDelay;

    [Header("End Game")]
    [SerializeField, Min(0f)] private float resultsHoldTime = 1.25f;

    private PlayerController winner    = null;
    private bool needsAReset           = false;
    private bool triggerStartGame      = false;
    private bool _repositionOnReset    = false;
    private const float tieThresHold   = 0.5f;
    private Tween winSequence;
    [SerializeField] private int currPlayersAlive;

    public GameState gameState = GameState.Menu;

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
                break;
        }
    }

    private void OnMenuState()
    {
        if (needsAReset) ResetValues();
        cameraRig.canMove = false;
    }

    private void ResetValues()
    {
        poolManager.ResetPool();

        foreach (var player in inGamePlayers)
        {
            if (player == null) continue;
            player.roundsWon = 0;
            ResetPlayer(player);
        }

        for (int i = 0; i < playersAlive.Length; i++)
            playersAlive[i] = null;

        cameraRig.ResetToGameplay();
        progression?.ResetForRound();
        needsAReset = false;

        OnPlayersCountChanged?.Invoke(CountJoinedPlayers());
    }

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
            uiManager.OnGamePlayersUI();
            AudioManager.Instance.PlaySound("game_start");
        }, () => OnGameStarting.Invoke());

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

        float phaseScale = ProgressionManager.Get(ProgressionVar.CameraSpeed);
        float recovery   = progression != null ? progression.CameraSpeedFactor : 1f;

        cameraRig.MaxHeightReached += cameraBaseSpeed * phaseScale * recovery * Time.deltaTime;
    }

    // ── Player management ─────────────────────────────────────────────────────

    public void AddPlayer(PlayerController player, Vector2 startPos, PlayerMaterial playerMat)
    {
        player.onDeath       = OnPlayersDeath;
        player.startPosition = startPos;
        player.SetMaterials(playerMat);
        AddInGamePlayer(player);

        AudioManager.Instance.MakeJoinSound();

        uiManager.UpdateJoinedPlayers(inGamePlayers);

        int count = CountJoinedPlayers();
        OnPlayersCountChanged?.Invoke(count);
    }

    private void AddInGamePlayer(PlayerController player)
    {
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

        if (repositionPlayer)
            player.transform.position = player.startPosition;
    }

    private void OnPlayersDeath(PlayerController player)
    {
        Vector2 deathPosition = player.transform.position;

        player.gameObject.transform.position = deathPos.position;
        player.DropBlock();

        playersAlive[player.playerIndex] = null;

        AudioManager.Instance.MakeDeathSound();
        uiManager.UpdateDeadPlayer(player.playerIndex);
        cameraRig.DoDeathShake();

        FeatherVFXController.Instance?.EmitDeath(deathPosition);

        CheckWinner();
    }

    // ── Start sequence ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by StartGameButton when pressed with ≥2 players in Menu state.
    /// </summary>
    public void TriggerStartSequence()
    {
        if (gameState != GameState.Menu) return;
        BeginStartSequence();
    }

    /// <summary>
    /// Called by StartGameButton to cancel the countdown.
    /// </summary>
    public void CancelStartSequence()
    {
        if (gameState != GameState.Prepare) return;

        triggerStartGame = false;
        uiManager.StopInitialGameSequence();
        ChangeGameState(GameState.Menu);
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

            PlayerController[] remainingPlayers = new PlayerController[currPlayersAlive];
            for (int i = 0; i < remainingPlayers.Length; i++)
            {
                remainingPlayers[i] = playersAlive[i];
                playersPosX.Add(remainingPlayers[i].transform.position.x);
            }

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

            Debug.Log($"Posición: {i + 1} | Jugador: {remainingPlayers[i].name} | Estado: {status}");
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

            uiManager.OnWinRound(inGamePlayers, wonGame =>
            {
                winSequence?.Kill();
                winSequence = DOVirtual.DelayedCall(2f, () => CheckGameWon(wonGame), false);
            });
        }
    }

    private void DoWin()
    {
        if (gameState == GameState.Win) return;

        ChangeGameState(GameState.Win);
        winner.roundsWon++;
        cameraRig.FocusWinner(winner.transform);
        winSequence?.Kill();

        uiManager.OnWinRound(inGamePlayers, wonGame =>
        {
            if (wonGame)
                AudioManager.Instance.PlayMusicWithIntro("BGM_Menu_A2", "BGM_Menu_B");
            else
                AudioManager.Instance.PlaySound("win_round");

            winSequence = DOVirtual.DelayedCall(2f, () => CheckGameWon(wonGame), false);
        });
    }

    private void CheckGameWon(bool wonGame)
    {
        if (wonGame)
        {
            uiManager.ResetPlayers(inGamePlayers);

            DOVirtual.DelayedCall(resultsHoldTime, () =>
            {
                uiManager.HidePointsPanel();
                needsAReset = true;
                SceneTransitionService.Instance.LoadMenu();
            }, false);

            return;
        }

        // New round — start automatically without going through the button again.
        cameraRig.ResetToGameplay();
        uiManager.HidePointsPanel();
        BeginStartSequence(repositionPlayers: true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void StopCheckerCoroutine()
    {
        if (checkerCoroutine == null) return;
        StopCoroutine(checkerCoroutine);
        checkerCoroutine = null;
    }

    public float CheckPlayerCoordinates()
    {
        float lowestY = float.MaxValue;
        foreach (var player in inGamePlayers)
        {
            if (player != null && player.transform.position.y < lowestY)
                lowestY = player.transform.position.y;
        }
        return lowestY;
    }

    public void FreeKeyboardScheme(string schemeName) =>
        playersManager.FreeKeyboardScheme(schemeName);
}

/// <summary>
/// Represents the current in-game rank of a player during an active round.
/// Used by GameManager to assign difficulty-scaled blocks via PoolingManager.
/// </summary>
public enum GameStatus { Winning, Neutral, Losing }