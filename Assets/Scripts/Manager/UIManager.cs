using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public PlayerUI[] playersUI = new PlayerUI[4];
    [SerializeField] private GameObject dimLayerBG;
    [SerializeField] private TextMeshProUGUI startGameTimerText;
    private Coroutine startGameCoroutine;

    void Awake()
    {
        for (int i = 0; i < playersUI.Length; i++)
        {
            playersUI[i].playerIndex = i + 1;
            playersUI[i].ChangeUIState(PlayerUIState.WaitJoin);
        }
    }

    public void UpdateJoinedPlayers(PlayerController[] inGamePlayers)
    {
        for (int i = 0; i < playersUI.Length; i++)
        {
            if (inGamePlayers[i] == null)
                playersUI[i].ChangeUIState(PlayerUIState.WaitJoin);
            else if (playersUI[i].GetPlayerUIState() == PlayerUIState.WaitJoin)
                playersUI[i].ChangeUIState(PlayerUIState.WaitReady);
        }
    }

    public void UpdateReadyPlayer(int playerIndex, bool isReady)
    {
        PlayerUIState newState = isReady ? PlayerUIState.Ready : PlayerUIState.WaitReady;
        playersUI[playerIndex].ChangeUIState(newState);
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

    public void StartGameSequence(Action callback)
    {
        if (startGameCoroutine != null)
            return;
        
        startGameCoroutine = StartCoroutine(StartGameText(callback));
    }

    public void StopGameSequence()
    {
        if (startGameCoroutine != null)
        {
            StopCoroutine(startGameCoroutine);
            startGameTimerText.gameObject.SetActive(false);
            dimLayerBG.SetActive(true);
            startGameCoroutine = null;
        }
    }
    
    private IEnumerator StartGameText(Action callback)
    {
        startGameTimerText.gameObject.SetActive(true);
        dimLayerBG.SetActive(false);
        startGameTimerText.text = "Ready...";
        yield return new WaitForSeconds(2f);
        startGameTimerText.text = "Steady...";
        yield return new WaitForSeconds(2f);
        startGameTimerText.text = "GO!";
        callback.Invoke();
        yield return new WaitForSeconds(1f);
        startGameTimerText.gameObject.SetActive(false);
    }
    
    // public void PlayAgainButton ()
    // {
    //     SoundManager.instance.PlaySound("start");
    //     GameManager.instance.RestartScene ();
    // }

}
