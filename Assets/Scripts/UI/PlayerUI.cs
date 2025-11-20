using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System;

[Serializable]
public enum PlayerUIState { WaitJoin, Joined, Ready, InGame, Dead, Round, Results, NotPlayer }

public class PlayerUI : MonoBehaviour
{
    public int playerIndex;
    public int roundsWon;
    public string place;
    [SerializeField] private PlayerUIState uiState;
    [SerializeField] private TextMeshProUGUI InitialText;
    [SerializeField] private GameObject InGameBox;
    [SerializeField] private GameObject DeadBox;

    private Coroutine waitJoinBlinkCoroutine;

    void Awake()
    {
        waitJoinBlinkCoroutine = StartCoroutine(WaitJoinBlink());
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
            case PlayerUIState.Joined:
                StopCoroutine(waitJoinBlinkCoroutine);
                InitialText.color= Color.white;
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
            case PlayerUIState.Round:
                InitialText.gameObject.SetActive(true);
                InitialText.text = $"{roundsWon} Points";
                break;
            case PlayerUIState.Results:
                InitialText.gameObject.SetActive(true);
                InitialText.text = $"{place}";
                break;
            default:
                break;
        }
    }

    private IEnumerator WaitJoinBlink()
    {
        float duration = 0.5f;
        while (true)
        {
            // Fade in
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float alpha = Mathf.Lerp(0, 1, t / duration);
                InitialText.color = new Color(InitialText.color.r, InitialText.color.g, InitialText.color.b, alpha);
                yield return null;
            }
            InitialText.color = new Color(InitialText.color.r, InitialText.color.g, InitialText.color.b, 1);
            // Fade out
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float alpha = Mathf.Lerp(1, 0, t / duration);
                InitialText.color = new Color(InitialText.color.r, InitialText.color.g, InitialText.color.b, alpha);
                yield return null;
            }
            InitialText.color = new Color(InitialText.color.r, InitialText.color.g, InitialText.color.b, 0);
        }
    }

}
