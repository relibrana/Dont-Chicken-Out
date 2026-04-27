using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public PlayerUI[] playersUI = new PlayerUI[4];
    [SerializeField] private GameObject dimLayerBG;
    [SerializeField] private TextMeshProUGUI startGameTimerText;

    [Header("Kick Button Prompt")]
    [Tooltip("UI block shown when the start button is active and available to kick.")]
    [SerializeField] private GameObject kickButtonPrompt;

    [Header("Points Manage")]
    [SerializeField] private Sprite eggPoint;
    [SerializeField] private GameObject pointsPanelHUD;
    private CanvasGroup pointsCanvasGroup;
    private Tween pointsFadeTween;

    private Sequence startGameSequence;
    private static readonly string[] PUESTOS = { "FIRST", "SECOND", "THIRD", "FOURTH" };

    [SerializeField] GameObject controls;
    [SerializeField] Transform controlsStart;
    [SerializeField] Transform controlsEnd;
    [SerializeField] GameObject playersCount;
    [SerializeField] Transform playersCountStart;
    [SerializeField] Transform playersCountEnd;
    [SerializeField] TextMeshProUGUI playersCountText;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        pointsCanvasGroup = pointsPanelHUD.GetComponent<CanvasGroup>();

        if (pointsPanelHUD != null)
            pointsPanelHUD.SetActive(false);
        if (pointsCanvasGroup != null)
            pointsCanvasGroup.alpha = 0f;

        if (kickButtonPrompt != null)
            kickButtonPrompt.SetActive(false);

        for (int i = 0; i < playersUI.Length; i++)
        {
            playersUI[i].playerIndex = i + 1;
            playersUI[i].ChangeUIState(PlayerUIState.WaitJoin);
        }
    }

    private void OnEnable()
    {
        GameManager.instance.OnPlayersCountChanged += HandlePlayersCountChanged;
        GameManager.instance.OnPrepare             += HandlePrepare;
        GameManager.instance.OnGame                += HandleGame;
        GameManager.instance.OnGameEnd             += HandleGameEnd;
    }

    private void OnDisable()
    {
        if (GameManager.instance == null) return;

        GameManager.instance.OnPlayersCountChanged -= HandlePlayersCountChanged;
        GameManager.instance.OnPrepare             -= HandlePrepare;
        GameManager.instance.OnGame                -= HandleGame;
        GameManager.instance.OnGameEnd             -= HandleGameEnd;
    }

    // ── Kick button prompt ────────────────────────────────────────────────────

    private void HandlePlayersCountChanged(int playerCount)
    {
        if (kickButtonPrompt == null) return;

        // Only show the prompt in Menu state and when enough players have joined.
        bool shouldShow = playerCount >= 2
                       && GameManager.instance.gameState == GameState.Menu;

        kickButtonPrompt.SetActive(shouldShow);
    }

    private void HandlePrepare()
    {
        // Countdown started — hide the prompt.
        if (kickButtonPrompt != null)
            kickButtonPrompt.SetActive(false);
    }

    private void HandleGame()
    {
        // Game started — prompt is already hidden, nothing extra needed.
        if (kickButtonPrompt != null)
            kickButtonPrompt.SetActive(false);
    }

    private void HandleGameEnd()
    {
        // Win or back to Menu — re-evaluate based on current player count.
        if (kickButtonPrompt == null) return;

        int count = 0;
        foreach (var p in GameManager.instance.InGamePlayers)
        {
            if (p != null) count++;
        }

        bool shouldShow = count >= 2
                       && GameManager.instance.gameState == GameState.Menu;

        kickButtonPrompt.SetActive(shouldShow);
    }

    // ── Players UI ────────────────────────────────────────────────────────────

    public void ResetPlayers(PlayerController[] inGamePlayers)
    {
        for (int i = 0; i < playersUI.Length; i++)
        {
            if (inGamePlayers[i] != null)
                playersUI[i].ChangeUIState(PlayerUIState.Joined);
            else
                playersUI[i].ChangeUIState(PlayerUIState.WaitJoin);
        }
        controls.SetActive(true);
        playersCount.SetActive(true);
        DOVirtual.DelayedCall(1f, () =>
        {
            controls.transform.DOMove(controlsStart.transform.position, 0.7f).SetEase(Ease.OutBack);
            playersCount.transform.DOMove(playersCountStart.transform.position, 0.6f).SetEase(Ease.OutBack).SetDelay(0.15f);
        });

        UpdatePointsHUDActiveStates(inGamePlayers);
    }

    public void UpdateJoinedPlayers(PlayerController[] inGamePlayers)
    {
        int playersAmount = 0;
        for (int i = 0; i < playersUI.Length; i++)
        {
            if (inGamePlayers[i] == null)
                playersUI[i].ChangeUIState(PlayerUIState.WaitJoin);
            else
            {
                playersAmount++;
                if (playersUI[i].GetPlayerUIState() == PlayerUIState.WaitJoin)
                    playersUI[i].ChangeUIState(PlayerUIState.Joined);
            }
        }

        playersCountText.text = $"{playersAmount} / 4";
        UpdatePointsHUDActiveStates(inGamePlayers);
    }

    private void UpdatePointsHUDActiveStates(PlayerController[] inGamePlayers)
    {
        for (int i = 0; i < playersUI.Length; i++)
        {
            bool isActive = inGamePlayers != null && i < inGamePlayers.Length && inGamePlayers[i] != null;
            playersUI[i].SetPointsHUDActive(isActive);
        }
    }

    public void UpdateReadyPlayers(PlayerController[] inGamePlayers)
    {
        for (int i = 0; i < playersUI.Length; i++)
        {
            if (inGamePlayers[i] != null)
                playersUI[i].ChangeUIState(PlayerUIState.Ready);
        }
    }

    public void UpdateReadyPlayer(int playerIndex, bool isReady)
    {
        PlayerUIState newState = isReady ? PlayerUIState.Ready : PlayerUIState.Joined;
        playersUI[playerIndex].ChangeUIState(newState);
    }

    public void UpdateDeadPlayer(int playerIndex)
    {
        playersUI[playerIndex].ChangeUIState(PlayerUIState.Dead);
    }

    public void OnWinRound(PlayerController[] inGamePlayers, Action<bool> callback)
    {
        ShowPointsPanel();

        bool didPlayerWonGame = false;
        for (int i = 0; i < playersUI.Length; i++)
        {
            if (inGamePlayers[i] != null)
            {
                playersUI[i].roundsWon = inGamePlayers[i].roundsWon;
                playersUI[i].ChangeUIState(PlayerUIState.Round);

                playersUI[i].UpdatePointsHUD(eggPoint);

                if (playersUI[i].roundsWon == 3)
                    didPlayerWonGame = true;
            }
        }

        if (didPlayerWonGame)
        {
            int[] roundsWon = new int[4];
            for (int i = 0; i < roundsWon.Length; i++)
            {
                if (inGamePlayers[i] != null)
                    roundsWon[i] = inGamePlayers[i].roundsWon;
            }

            List<string> positions = GetAssignedRanks(roundsWon);

            for (int i = 0; i < positions.Count; i++)
            {
                if (inGamePlayers[i] != null)
                {
                    playersUI[i].place = positions[i];
                    playersUI[i].ChangeUIState(PlayerUIState.Results);
                }
            }

            callback.Invoke(true);
        }
        else
        {
            callback.Invoke(false);
        }
    }

    private void ShowPointsPanel()
    {
        if (pointsPanelHUD == null || pointsCanvasGroup == null)
            return;

        if (pointsFadeTween != null && pointsFadeTween.IsActive())
            pointsFadeTween.Kill();

        pointsPanelHUD.SetActive(true);
        pointsCanvasGroup.alpha = 0f;
        pointsFadeTween = pointsCanvasGroup.DOFade(1f, 0.5f);
    }

    public void HidePointsPanel()
    {
        if (pointsPanelHUD == null || pointsCanvasGroup == null)
            return;

        if (pointsFadeTween != null && pointsFadeTween.IsActive())
            pointsFadeTween.Kill();

        pointsFadeTween = pointsCanvasGroup
            .DOFade(0f, 0.5f)
            .OnComplete(() =>
            {
                pointsPanelHUD.SetActive(false);
            });
    }

    private List<string> GetAssignedRanks(int[] scores)
    {
        List<string> ranks = new List<string>(4);

        for (int i = 0; i < scores.Length; i++)
        {
            int currentScore = scores[i];
            int rank = 1;
            for (int j = 0; j < scores.Length; j++)
            {
                if (scores[j] > currentScore)
                    rank++;
            }

            int rankIndex = rank - 1;

            if (rankIndex >= 0 && rankIndex < PUESTOS.Length)
                ranks.Add(PUESTOS[rankIndex]);
        }

        return ranks;
    }

    public void OnGamePlayersUI()
    {
        for (int i = 0; i < playersUI.Length; i++)
        {
            if (playersUI[i].GetPlayerUIState() == PlayerUIState.Ready)
                playersUI[i].ChangeUIState(PlayerUIState.InGame);
            else
                playersUI[i].ChangeUIState(PlayerUIState.NotPlayer);
        }
    }

    public void StartInitialGameSequence(Action callback, Action prevCallback)
    {
        if (startGameSequence != null)
            return;
        
        AudioManager.Instance.PlaySound("game_countdown");

        StartGameText(callback, prevCallback);
    }

    public void StopInitialGameSequence()
    {
        if (startGameSequence == null) return;

        startGameSequence.Kill();
        startGameSequence = null;
        startGameTimerText.gameObject.SetActive(false);
        dimLayerBG.SetActive(true);
        controls.SetActive(true);
        playersCount.SetActive(true);
        controls.transform.DOMove(controlsStart.transform.position, 0.7f).SetEase(Ease.OutBack);
        playersCount.transform.DOMove(playersCountStart.transform.position, 0.6f).SetEase(Ease.OutBack).SetDelay(0.15f);
    }

    private void StartGameText(Action callback, Action prevCallback)
    {
        startGameSequence = DOTween.Sequence();
        startGameSequence.AppendCallback(() =>
        {
            startGameTimerText.gameObject.SetActive(true);
            dimLayerBG.SetActive(false);
            startGameTimerText.text = "3";
        });

        startGameSequence.AppendInterval(1.25f);

        startGameSequence.AppendCallback(() =>
        {
            startGameTimerText.text = "2";
        });

        startGameSequence.AppendInterval(1.25f);

        startGameSequence.AppendCallback(() =>
        {
            prevCallback?.Invoke();
            startGameTimerText.text = "1";
            DOVirtual.DelayedCall(1.15f, () =>
            {
                controls.SetActive(false);
                playersCount.SetActive(false);
            });
            controls.transform.DOMove(controlsEnd.transform.position, 0.7f).SetEase(Ease.InBack);
            playersCount.transform.DOMove(playersCountEnd.transform.position, 0.6f).SetEase(Ease.InBack).SetDelay(0.15f);
        });

        startGameSequence.AppendInterval(1.25f);

        startGameSequence.AppendCallback(() =>
        {
            startGameTimerText.text = "GO!";
            callback.Invoke();
        });

        startGameSequence.AppendInterval(1f);

        startGameSequence.AppendCallback(() =>
        {
            startGameTimerText.gameObject.SetActive(false);
            startGameSequence = null;
        });
    }
}