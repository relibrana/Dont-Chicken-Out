using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using DG.Tweening;

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
    [SerializeField] private SpriteRenderer Bg;

    [Header("Game Variables")]
    public float autoMoveCameraSpeed = 0.2f;
    // bool screenShakeTrigger=false;
    [SerializeField] private PlayerController[] inGamePlayers = new PlayerController[4];
    [SerializeField] private PlayerController[] playersAlive = new PlayerController[4];
    [SerializeField] private Transform deathPos;
    private PlayerController winner = null;
    private bool needsAReset = false;
    private bool triggerStartGame = false;


    public GameState gameState = GameState.Menu;

    public PlayerController[] InGamePlayers
    {
        get
        {
            var copy = new PlayerController[4];
            for (int i = 0; i < playersAlive.Length; i++)
                copy[i] = inGamePlayers[i];
            return copy;
        }
    }

    void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }
    void Start()
    {
        Bg.DOFade(0, 1f).SetDelay(3);
        AudioManager.Instance.PlayMusic("Menu");
    }

    void Update()
    {
        if (autoMoveCameraCurrentTime < 0)
            autoMoveCameraCurrentTime = 0;

        switch (gameState)
        {
            case GameState.Menu:
                OnMenuState();
                break;
            case GameState.Prepare:
                OnPrepareState();
                break;
            case GameState.Game:
                OnGameState();
                break;
            case GameState.Win:
                OnWinState();
                break;
        }
    }

    private void ChangeGameState(GameState newGameState)
    {
        gameState = newGameState;
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
            if (player == null)
                continue;
            player.roundsWon = 0;
            ResetPlayer(player);
        }
        for (int i = 0; i < playersAlive.Length; i++)
        {
            playersAlive[i] = null;
        }
        cameraRig.ResetToGameplay();
        needsAReset = false;
    }

    private void OnPrepareState()
    {
        foreach (var player in inGamePlayers)
        {
            if (player == null)
                continue;

            if (!player.gameObject.activeSelf)
                player.gameObject.SetActive(true);

            if (triggerStartGame)
                ResetPlayer(player);
        }

        autoMoveCameraCurrentTime = startUpTime;

        if (triggerStartGame)
        {
            winner = null;
            poolManager.ResetPool();
            for (int i = 0; i < inGamePlayers.Length; i++)
            {
                playersAlive[i] = inGamePlayers[i];
            }
            cameraRig.canMove = false;
            cameraRig.ResetToGameplay();
            uiManager.StartInitialGameSequence(() =>
            {
                ChangeGameState(GameState.Game);
                AudioManager.Instance.PlayMusic("Game");
                uiManager.OnGamePlayersUI();
            });
            triggerStartGame = false;
        }
    }

    private void ResetPlayer(PlayerController player)
    {
        player.transform.position = player.startPosition;
        player.isBlockLogicAvailable = true;
        player.currentBlockHolding = null;
        player.isOnGame = false;
    }

    private void OnGameState()
    {
        cameraRig.canMove = true;
        for (int i = 0; i < playersAlive.Length; i++)
        {
            if (playersAlive[i] != null)
                playersAlive[i].isOnGame = true;
        }

        //CameraMovement
        autoMoveCameraCurrentTime -= Time.deltaTime;

        if (autoMoveCameraCurrentTime <= 0)
        {
            cameraRig.MaxHeightReached += autoMoveCameraSpeed * Time.deltaTime;
        }
    }

    private void OnWinState()
    {
        //uiManager.HidePointsPanel();
    }

    public float CheckPlayerCoordinates()
    {
        float lowestY = float.MaxValue;
        foreach (PlayerController player in inGamePlayers)
        {
            if (player != null && player.transform.position.y < lowestY)
            {
                lowestY = player.transform.position.y;
            }
        }
        return lowestY;
    }

    public void AddPlayer(PlayerController player, Vector2 StartPos, PlayerMaterial playerMat)
    {
        player.onDeath = OnPlayersDeath;
        player.onPlayerReady = PlayerToggleReady;
        player.startPosition = StartPos;
        player.SetMaterials(playerMat);
        AddInGamePlayer(player);

        uiManager.UpdateJoinedPlayers(inGamePlayers);
        CheckIfAllPlayersReady();
    }

    private void AddInGamePlayer(PlayerController player)
    {
        for (int i = 0; i < inGamePlayers.Length; i++)
        {
            if (inGamePlayers[i] == null)
            {
                inGamePlayers[i] = player;
                player.playerIndex = i;
                return;
            }
        }
    }

    private void HandleDisconnection()
    {

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
        int currPlayersAlive = 0;
        for (int i = 0; i < playersAlive.Length; i++)
        {
            if (playersAlive[i] != null)
                currPlayersAlive++;
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

    private void OnPlayersDeath(PlayerController player)
    {
        player.transform.position = player.startPosition;
        player.gameObject.transform.position = deathPos.position;
        if (player.currentBlockHolding)
        {
            player.currentBlockHolding.gameObject.SetActive(false);
            player.currentBlockHolding = null;
        }
        player.isBlockLogicAvailable = false;
        playersAlive[player.playerIndex] = null;

        uiManager.UpdateDeadPlayer(player.playerIndex);
        cameraRig.DoDeathShake();

        CheckWinner();
    }

    private void CheckWinner()
    {
        int currPlayersAlive = 0;
        for (int i = 0; i < playersAlive.Length; i++)
        {
            if (playersAlive[i] != null)
            {
                currPlayersAlive++;
                winner = playersAlive[i];
            }
        }

        if (currPlayersAlive == 1)
        {
            ChangeGameState(GameState.Win);
            winner.roundsWon++;
            cameraRig.FocusWinner(winner.transform);
            uiManager.OnWinRound(inGamePlayers, wonGame => DOVirtual.DelayedCall(2f, () => CheckGameWon(wonGame), false));
        }
    }
    /*private void CheckWinner()
    {
        int currPlayersAlive = 0;
        for (int i = 0; i < playersAlive.Length; i++)
        {
            if (playersAlive[i] != null)
            {
                currPlayersAlive++;
                winner = playersAlive[i];
            }
        }

        if (currPlayersAlive == 1)
        {
            ChangeGameState(GameState.Win);
            winner.roundsWon++;

            // Solo acercar la c�mara al ganador si GAN� LA PARTIDA (por ejemplo, 3 puntos)
            if (winner.roundsWon >= 3)
            {
                cameraRig.FocusWinner(winner.transform);
            }

            uiManager.OnWinRound(inGamePlayers, wonGame =>
                DOVirtual.DelayedCall(2f, () => CheckGameWon(wonGame), false));
        }
    }*/


    /*private void CheckGameWon(bool wonGame)
    {
        if (wonGame)
        {
            uiManager.ResetPlayers(inGamePlayers);
            needsAReset = true;
            ChangeGameState(GameState.Menu);
        }
        else
        {
            uiManager.HidePointsPanel();

            uiManager.UpdateReadyPlayers(inGamePlayers);
            triggerStartGame = true;
            ChangeGameState(GameState.Prepare);
        }
    }*/

    private void CheckGameWon(bool wonGame)
    {
        if (wonGame)
        {
            uiManager.ResetPlayers(inGamePlayers);
            uiManager.HidePointsPanel();
            needsAReset = true;
            ChangeGameState(GameState.Menu);
        }
        else
        {
            cameraRig.ResetToGameplay();

            uiManager.HidePointsPanel();

            uiManager.UpdateReadyPlayers(inGamePlayers);
            triggerStartGame = true;
            ChangeGameState(GameState.Prepare);
        }
    }


    public void FreeKeyboardScheme(string schemeName) => playersManager.FreeKeyboardScheme(schemeName);
}
