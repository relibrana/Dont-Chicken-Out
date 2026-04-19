using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// World-space button that lives in the game scene.
/// Becomes visible when ≥2 players have joined.
/// Pressing it (via kick) triggers or cancels the start countdown.
/// Hides permanently once the game begins; reappears on the next full game reset.
/// </summary>
[DisallowMultipleComponent]
public sealed class StartGameButton : MonoBehaviour, IKickable
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Positions")]
    [Tooltip("Transform marking the hidden position (e.g. below the floor).")]
    [SerializeField] private Transform hiddenPoint;

    [Tooltip("Transform marking the active/visible position (above the floor).")]
    [SerializeField] private Transform activePoint;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveInDuration  = 0.5f;
    [SerializeField, Min(0f)] private float moveOutDuration = 0.35f;
    [SerializeField]          private Ease  moveInEase      = Ease.OutBack;
    [SerializeField]          private Ease  moveOutEase     = Ease.InBack;

    [Header("Sprites")]
    [Tooltip("Sprite shown when the button is available to be pressed.")]
    [SerializeField] private Sprite availableSprite;

    [Tooltip("Sprite shown while the countdown is running.")]
    [SerializeField] private Sprite pressedSprite;

    [SerializeField] private SpriteRenderer buttonRenderer;
    [SerializeField] private Collider2D     buttonCollider;

    // ── State ─────────────────────────────────────────────────────────────────

    private enum ButtonState { Hidden, Available, Counting }

    private ButtonState _state = ButtonState.Hidden;
    private Tween       _moveTween;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        transform.position     = hiddenPoint.position;
        buttonCollider.enabled = false;
    }

    private void OnEnable()
    {
        GameManager.instance.OnPlayersCountChanged += HandlePlayersCountChanged;
        GameManager.instance.OnGame                += HandleGameStarted;
    }

    private void OnDisable()
    {
        // Guard: GameManager may already be destroyed (scene unload).
        if (GameManager.instance == null) return;

        GameManager.instance.OnPlayersCountChanged -= HandlePlayersCountChanged;
        GameManager.instance.OnGame                -= HandleGameStarted;
    }

    // ── IKickable ─────────────────────────────────────────────────────────────

    public void ReceiveKick(Vector2 kickImpulse)
    {
        switch (_state)
        {
            case ButtonState.Available:
                StartCountdown();
                break;

            case ButtonState.Counting:
                CancelCountdown();
                break;
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void HandlePlayersCountChanged(int playerCount)
    {
        switch (_state)
        {
            case ButtonState.Hidden when playerCount >= 2:
                ShowButton();
                break;

            // A new player joined while counting → cancel and stay visible.
            case ButtonState.Counting:
                CancelCountdown();
                break;
        }
    }

    private void HandleGameStarted()
    {
        HideButtonPermanently();
    }

    // ── Button flow ───────────────────────────────────────────────────────────

    private void ShowButton()
    {
        _state = ButtonState.Available;
        SetSprite(availableSprite);
        buttonCollider.enabled = false;
        AnimateTo(activePoint.position, moveInDuration, moveInEase,
            onComplete: () => buttonCollider.enabled = true);
    }

    private void StartCountdown()
    {
        _state = ButtonState.Counting;
        SetSprite(pressedSprite);
        GameManager.instance.TriggerStartSequence();
    }

    private void CancelCountdown()
    {
        _state = ButtonState.Available;
        SetSprite(availableSprite);
        GameManager.instance.CancelStartSequence();
    }

    /// <summary>
    /// Moves the button back to the hidden position and prevents it from
    /// reappearing until the next full game (OnPlayersCountChanged via ResetValues).
    /// </summary>
    private void HideButtonPermanently()
    {
        _state                 = ButtonState.Hidden;
        buttonCollider.enabled = false;
        AnimateTo(hiddenPoint.position, moveOutDuration, moveOutEase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AnimateTo(Vector3 target, float duration, Ease ease, Action onComplete = null)
    {
        _moveTween?.Kill();
        _moveTween = transform.DOMove(target, duration)
            .SetEase(ease)
            .OnComplete(() => onComplete?.Invoke());
    }

    private void SetSprite(Sprite sprite)
    {
        if (buttonRenderer != null && sprite != null)
            buttonRenderer.sprite = sprite;
    }
}