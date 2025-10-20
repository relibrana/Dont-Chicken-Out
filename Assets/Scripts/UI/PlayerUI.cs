using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using TMPro;

public enum PlayerUIState { WaitJoin, WaitReady, Ready, InGame, Dead, NotPlayer }

public class PlayerUI : MonoBehaviour
{
    public int playerIndex;
    private PlayerUIState uiState;
    [SerializeField] private TextMeshProUGUI InitialText;
    [SerializeField] private GameObject InGameBox;
    [SerializeField] private GameObject DeadBox;

    void Awake()
    {
        ChangeUIState(PlayerUIState.WaitJoin);
    }

    public PlayerUIState GetPlayerUIState() => uiState;

    public void ChangeUIState(PlayerUIState newState)
    {
        InitialText.gameObject.SetActive(false);
        InGameBox.SetActive(false);
        DeadBox.SetActive(false);

        uiState = newState;

        switch(uiState)
        {
            case PlayerUIState.WaitJoin:
                InitialText.gameObject.SetActive(true);
                InitialText.text = $"PLAYER {playerIndex} PRESS START";
                break;
            case PlayerUIState.WaitReady:
                InitialText.gameObject.SetActive(true);
                InitialText.text = "CLUCK CLUCK\nTO BE\nREADY STEADY";
                break;
            case PlayerUIState.Ready:
                InitialText.gameObject.SetActive(true);
                InitialText.text = $"PLAYER {playerIndex} READY";
                break;
            case PlayerUIState.InGame:
                InGameBox.SetActive(true);
                break;
            case PlayerUIState.Dead:
                DeadBox.SetActive(true);
                break;
            default:
                break;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
