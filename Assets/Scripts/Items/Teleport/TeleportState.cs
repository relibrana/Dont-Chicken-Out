using System;
using UnityEngine;

/// <summary>
/// The teleport as you see it (design, oct 2026): the chicken shrinks to
/// nothing where it stands, reappears at the destination and grows back to its
/// size, all inside the teleport sound. Only the drawing scales (VisualRoot);
/// the colliders keep their size so the chicken does not drop while shrinking.
/// No control while it lasts.
///
/// A PlayerItemState so death and the round reset end it cleanly: the scale
/// comes back, the lock is released, and an unfinished teleport never lands.
/// </summary>
public sealed class TeleportState : PlayerItemState
{
    private float _shrinkSeconds;
    private float _growSeconds;
    private Func<Vector2?> _destination;
    private Action _onArrived;

    private Transform _visual;
    private Vector3   _visualScale;
    private bool      _arrived;

    protected override bool UsesTint => false;

    /// <param name="destination">Asked at the moment of arrival, so it follows a moving target. Null = stay.</param>
    public void Activate(float shrinkSeconds, float growSeconds, Func<Vector2?> destination, Action onArrived)
    {
        _shrinkSeconds = Mathf.Max(0.01f, shrinkSeconds);
        _growSeconds   = Mathf.Max(0.01f, growSeconds);
        _destination   = destination;
        _onArrived     = onArrived;

        BeginState(_shrinkSeconds + _growSeconds, Color.white);
    }

    protected override void OnStateStarted()
    {
        _visual      = Controller.VisualRoot;
        _visualScale = _visual.localScale;
        _arrived     = false;
        Controller.PushMovementLock();
    }

    // The jump itself is gameplay: it happens on the tick.
    protected override void OnTick()
    {
        if (_shrinkSeconds + _growSeconds - Remaining >= _shrinkSeconds)
            Arrive();
    }

    // The shrink/grow is drawing: every frame, from the frame-accurate time.
    protected override void OnRender()
    {
        float elapsed = _shrinkSeconds + _growSeconds - RenderRemaining;

        // Still shrinking on screen, or the tick has not moved the body yet:
        // growing back before arriving would show the chicken at the origin.
        if (elapsed < _shrinkSeconds || !_arrived)
        {
            SetVisualScale(1f - EaseIn(Mathf.Clamp01(elapsed / _shrinkSeconds)));
            return;
        }

        SetVisualScale(EaseOut((elapsed - _shrinkSeconds) / _growSeconds));
    }

    protected override void OnStateExpired()
    {
        Arrive();
        EndState();
    }

    protected override void OnStateEnded()
    {
        if (_visual != null) _visual.localScale = _visualScale;
        Controller.PopMovementLock();
    }

    private void Arrive()
    {
        if (_arrived) return;
        _arrived = true;

        Vector2? destination = _destination?.Invoke();
        if (destination.HasValue) Controller.TeleportTo(destination.Value);

        _onArrived?.Invoke();
    }

    private void SetVisualScale(float k)
    {
        // No upper clamp: the arrival overshoots on purpose.
        if (_visual != null) _visual.localScale = _visualScale * Mathf.Max(0f, k);
    }

    // Snappy on purpose (design, oct 2026: "muy suave para ser un tp").
    // Shrink: slow start, then gone in a snap (cubic).
    private static float EaseIn(float t) => t * t * t;

    // Grow: overshoots ~10% above its size and settles back (back-out), so the
    // arrival reads as a pop rather than a fade-in.
    private static float EaseOut(float t)
    {
        const float s = 1.70158f;
        t -= 1f;
        return t * t * ((s + 1f) * t + s) + 1f;
    }
}
