using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using DG.Tweening;
using System.Collections;

[Serializable]
public enum GameState { Menu, Prepare, Game, Win }

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("References")]
    public PoolingManager poolManager;
    [SerializeField] private PlayersManager playersManager;

    [Header("UI")]
    [SerializeField] private UIManager uiManager;
    public CinemachineVerticalRig2D cameraRig;
    private float autoMoveCameraCurrentTime;
    private const float startUpTime = 3.5f;

    [Header("Game Variables")]
    public Action OnGame;
    public Action OnGameEnd;
    public float autoMoveCameraSpeed = 0.2f;
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
        AudioManager.Instance.PlayMusic("Menu");
    }

    private void Update()
    {
        if (autoMoveCameraCurrentTime < 0)
            autoMoveCameraCurrentTime = 0;

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
        AudioManager.Instance.PlayMusic("Menu");
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
        needsAReset = false;
    }

    private void OnPrepareState()
    {
        foreach (var player in inGamePlayers)
        {
            if (player == null) continue;

            if (!player.gameObject.activeSelf)
                player.gameObject.SetActive(true);

            if (triggerStartGame)
                ResetPlayer(player);
        }

        autoMoveCameraCurrentTime = startUpTime;

        if (!triggerStartGame) return;

        winner = null;
        poolManager.ResetPool();
        currPlayersAlive = 0;

        for (int i = 0; i < inGamePlayers.Length; i++)
        {
            playersAlive[i] = inGamePlayers[i];
            if (playersAlive[i] != null) currPlayersAlive++;
        }

        cameraRig.canMove = false;
        cameraRig.ResetToGameplay();

        uiManager.StartInitialGameSequence(() =>
        {
            ChangeGameState(GameState.Game);
            checkerCoroutine = StartCoroutine(CheckPlayersInGame());
            AudioManager.Instance.PlayMusic("Game");
            uiManager.OnGamePlayersUI();
        });

        triggerStartGame = false;
    }

    private void OnGameState()
    {
        cameraRig.canMove = true;

        for (int i = 0; i < playersAlive.Length; i++)
        {
            if (playersAlive[i] != null)
                playersAlive[i].isOnGame = true;
        }

        autoMoveCameraCurrentTime -= Time.deltaTime;

        if (autoMoveCameraCurrentTime <= 0)
            cameraRig.MaxHeightReached += autoMoveCameraSpeed * Time.deltaTime;
    }

    private void OnWinState() { }

    // ── Player management ─────────────────────────────────────────────────────

    public void AddPlayer(PlayerController player, Vector2 startPos, PlayerMaterial playerMat)
    {
        player.onDeath       = OnPlayersDeath;
        player.onPlayerReady = PlayerToggleReady;
        player.startPosition = startPos;
        player.SetMaterials(playerMat);
        AddInGamePlayer(player);

        AudioManager.Instance.PlaySound("player_join");

        uiManager.UpdateJoinedPlayers(inGamePlayers);
        CheckIfAllPlayersReady();
    }

    private void AddInGamePlayer(PlayerController player)
    {
        for (int i = 0; i < inGamePlayers.Length; i++)
        {
            if (inGamePlayers[i] != null) continue;

            inGamePlayers[i]  = player;
            player.playerIndex = i;
            return;
        }
    }

    private void ResetPlayer(PlayerController player)
    {
        player.transform.position = player.startPosition;
        player.DropBlock();
        player.isOnGame = false;
    }

    private void OnPlayersDeath(PlayerController player)
    {
        player.gameObject.transform.position = deathPos.position;
        player.DropBlock();

        playersAlive[player.playerIndex] = null;

        AudioManager.Instance.PlaySound("player_death");
        uiManager.UpdateDeadPlayer(player.playerIndex);
        cameraRig.DoDeathShake();

        CheckWinner();
    }

    private void PlayerToggleReady(PlayerController player)
    {
        bool wasReady = playersAlive.Contains(player);

        uiManager.UpdateReadyPlayer(player.playerIndex, !wasReady);
        playersAlive[player.playerIndex] = !wasReady ? player : null;

        CheckIfAllPlayersReady();
    }

    private void CheckIfAllPlayersReady()
    {
        currPlayersAlive = 0;
        for (int i = 0; i < playersAlive.Length; i++)
        {
            if (playersAlive[i] != null) currPlayersAlive++;
        }

        if (currPlayersAlive != playersManager.currPlayersInGame || currPlayersAlive < 2)
        {
            uiManager.StopInitialGameSequence();
            ChangeGameState(GameState.Menu);
            return;
        }

        triggerStartGame = true;
        ChangeGameState(GameState.Prepare);
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
        if (playerRank == 0)                  return GameStatus.Winning;
        if (playerRank == totalPlayers - 1)   return GameStatus.Losing;
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
            AudioManager.Instance.PlaySound(wonGame ? "win_game" : "win_round");
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

        cameraRig.ResetToGameplay();
        uiManager.HidePointsPanel();
        uiManager.UpdateReadyPlayers(inGamePlayers);
        triggerStartGame = true;
        ChangeGameState(GameState.Prepare);
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