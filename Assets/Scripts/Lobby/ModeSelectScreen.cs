using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local or Online. Online is shown but locked until the netcode lands
/// (the demo is local + Steam Remote Play Together).
/// </summary>
public sealed class ModeSelectScreen : LobbyScreen
{
    [SerializeField] private Button localButton;
    [SerializeField] private Button onlineButton;

    [Tooltip("While true the Online button is visible but does nothing.")]
    [SerializeField] private bool onlineLocked = true;

    [Tooltip("Lock badge over the Online button.")]
    [SerializeField] private GameObject onlineLockedBadge;

    public event Action<MatchMode> ModeChosen;
    public event Action            BackRequested;

    private void Awake()
    {
        localButton.onClick.AddListener(() => Choose(MatchMode.Local));
        onlineButton.onClick.AddListener(() =>
        {
            if (onlineLocked) { LockedFeedback.Play(onlineButton.transform); return; }
            Choose(MatchMode.Online);
        });
    }

    public override void Enter()
    {
        if (onlineLockedBadge != null) onlineLockedBadge.SetActive(onlineLocked);
        base.Enter();
    }

    // Press sound comes from the button's UIButtonSFX.
    private void Choose(MatchMode mode) => ModeChosen?.Invoke(mode);

    public override void OnCancel() => BackRequested?.Invoke();
}
