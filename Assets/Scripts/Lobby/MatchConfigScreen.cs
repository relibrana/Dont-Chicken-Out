using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Match rules chosen by the host before the hen house. Today: rounds to
/// win (first-to-N, options 1 · 3 · 5). The map picked on the previous
/// screen is shown as a reminder.
/// </summary>
public sealed class MatchConfigScreen : LobbyScreen
{
    [SerializeField] private OptionGroup     roundsToWin;
    [SerializeField] private Button          continueButton;
    [SerializeField] private TextMeshProUGUI mapLabel;

    public event Action Confirmed;
    public event Action BackRequested;

    private void Awake()
    {
        if (roundsToWin == null || continueButton == null)
        {
            Debug.LogError("[MatchConfigScreen] Faltan referencias (escena Lobby de una versión anterior). "
                         + "Regenérala con Tools > Don't Chicken Out > Crear escena Lobby.", this);
            enabled = false;
            return;
        }

        continueButton.onClick.AddListener(() =>
        {
            MatchSetup.RoundsToWin = roundsToWin.Value;
            Confirmed?.Invoke(); // press sound: the button's UIButtonSFX
        });
        roundsToWin.ValueChanged += v => MatchSetup.RoundsToWin = v;
    }

    public override void Enter()
    {
        if (roundsToWin != null)
        {
            roundsToWin.SetValue(MatchSetup.RoundsToWin);
            MatchSetup.RoundsToWin = roundsToWin.Value;
        }
        if (mapLabel != null)
            mapLabel.text = MatchSetup.Map != null ? MatchSetup.Map.DisplayName : "";
        base.Enter();
    }

    public override void OnCancel() => BackRequested?.Invoke();
}
