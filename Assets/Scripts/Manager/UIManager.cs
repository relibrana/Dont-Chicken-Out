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
    private Sequence startGameSequence;
    private static readonly string[] PUESTOS = { "FIRST", "SECOND", "THIRD", "FOURTH" };

    void Awake()
    {
        for (int i = 0; i < playersUI.Length; i++)
        {
            playersUI[i].playerIndex = i + 1;
            playersUI[i].ChangeUIState(PlayerUIState.WaitJoin);
        }
    }

    public void ResetJoinedPlayers(PlayerController[] inGamePlayers)
    {
        for (int i = 0; i < playersUI.Length; i++)
        {
            if (inGamePlayers[i] != null)
                playersUI[i].ChangeUIState(PlayerUIState.Joined);
        }
    }

    public void UpdateJoinedPlayers(PlayerController[] inGamePlayers)
    {
        for (int i = 0; i < playersUI.Length; i++)
        {
            if (inGamePlayers[i] == null)
                playersUI[i].ChangeUIState(PlayerUIState.WaitJoin);
            else if (playersUI[i].GetPlayerUIState() == PlayerUIState.WaitJoin)
                playersUI[i].ChangeUIState(PlayerUIState.Joined);
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
        bool didPlayerWonGame = false;
        for (int i = 0; i < playersUI.Length; i++)
        {
            if (inGamePlayers[i] != null)
            {
                playersUI[i].roundsWon = inGamePlayers[i].roundsWon;
                playersUI[i].ChangeUIState(PlayerUIState.Round);
                if (playersUI[i].roundsWon == 3)
                    didPlayerWonGame = true;
            }
        }
        if (didPlayerWonGame)
        {
            int[] roundsWon = new int[4];
            for (int i = 0; i < roundsWon.Length; i++)
            {
                if(inGamePlayers[i] != null)
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

    private List<string> GetAssignedRanks(int[] scores)
    {
        List<string> ranks = new List<string>(4);

        for (int i = 0; i < scores.Length; i++)
        {
            int currentScore = scores[i];
            int rank = 1;
            for (int j = 0; j < scores.Length; j++)
            {
                int otherScore = scores[j];

                if (otherScore > currentScore)
                {
                    rank++;
                }
            }
            
            int rankIndex = rank - 1;

            if (rankIndex >= 0 && rankIndex < PUESTOS.Length)
            {
                ranks.Add(PUESTOS[rankIndex]);
            }
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

    public void StartInitialGameSequence(Action callback)
    {
        if (startGameSequence != null)
            return;
        
        StartGameText(callback);
    }

    public void StopInitialGameSequence()
    {
        if (startGameSequence != null)
        {
            startGameSequence.Kill();
            startGameSequence = null;
            startGameTimerText.gameObject.SetActive(false);
            dimLayerBG.SetActive(true);
        }
    }

    private void StartGameText(Action callback)
    {
        startGameSequence = DOTween.Sequence();
        startGameSequence.AppendCallback(() =>
        {
            startGameTimerText.gameObject.SetActive(true);
            dimLayerBG.SetActive(false);
            startGameTimerText.text = "Ready...";
        });

        startGameSequence.AppendInterval(2f);

        startGameSequence.AppendCallback(() =>
        {
            startGameTimerText.text = "Steady...";
        });

        startGameSequence.AppendInterval(2f);

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
    
    // public void PlayAgainButton ()
    // {
    //     SoundManager.instance.PlaySound("start");
    //     GameManager.instance.RestartScene ();
    // }

}
