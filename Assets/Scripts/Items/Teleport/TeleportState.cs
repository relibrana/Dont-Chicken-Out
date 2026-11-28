using System;
using UnityEngine;

/// <summary>
/// The teleport as you see it (design, oct 2026; pause added nov 2026): the
/// chicken shrinks to nothing where it stands, stays gone a moment, reappears
/// at the destination and grows back to its size, all inside the teleport
/// sound. Only the drawing scales (VisualRoot); the colliders keep their size
/// so the chicken does not drop while shrinking. While gone it is out of the
/// world (PlayerController.SetInTransit): nothing can touch or kill it.
/// No control while it lasts.
///
/// A PlayerItemState so death and the round reset end it cleanly: the scale
/// comes back, the lock is released, and an unfinished teleport never lands.
/// </summary>
public sealed class TeleportState : PlayerItemState
{
    private float _shrinkSeconds;
    private float _goneSeconds;
    private float _growSeconds;
    private Func<Vector2?> _destination;
    private Action _onArrived;

    private Transform _visual;
    private Vector3   _visualScale;
    private bool      _inTransit;
    private bool      _arrived;

    protected override bool UsesTint => false;

    private float Total => _shrinkSeconds + _goneSeconds + _growSeconds;

    /// <param name="destination">Asked at the moment of arrival, so it follows a moving target. Null = stay.</param>
    public void Activate(float shrinkSeconds, float goneSeconds, float growSeconds, Func<Vector2?> destination, Action onArrived)
    {
        _shrinkSeconds = Mathf.Max(0.01f, shrinkSeconds);
        _goneSeconds   = Mathf.Max(0f, goneSeconds);
        _growSeconds   = Mathf.Max(0.01f, growSeconds);
        _destination   = destination;
        _onArrived     = onArrived;

        BeginState(Total, Color.white);
    }

    protected override void OnStateStarted()
    {
        _visual      = Controller.VisualRoot;
        _visualScale = _visual.localScale;
        _inTransit   = false;
        _arrived     = false;
        Controller.PushMovementLock();
    }

    // Leaving and arriving are gameplay: they happen on the tick.
    protected override void OnTick()
    {
        float elapsed = Total - Remaining;

        if (elapsed >= _shrinkSeconds && !_inTransit && !_arrived)
        {
            _inTransit = true;
            Controller.SetInTransit(true);
        }

        if (elapsed >= _shrinkSeconds + _goneSeconds)
            Arrive();
    }

    // The shrink/grow is drawing: every frame, from the frame-accurate time.
    protected override void OnRender()
    {
        float elapsed = Total - RenderRemaining;
        float growAt  = _shrinkSeconds + _goneSeconds;

        // Still shrinking or gone on screen, or the tick has not moved the body
        // yet: growing back before arriving would show the chicken at the origin.
        if (elapsed < growAt || !_arrived)
        {
            SetVisualScale(1f - EaseIn(Mathf.Clamp01(elapsed / _shrinkSeconds)));
            return;
        }

        SetVisualScale(EaseOut((elapsed - growAt) / _growSeconds));
    }

    protected override void OnStateExpired()
    {
        Arrive();
        EndState();
    }

    protected override void OnStateEnded()
    {
        if (_visual != null) _visual.localScale = _visualScale;
        LeaveTransit();
        Controller.PopMovementLock();
    }

    private void Arrive()
    {
        if (_arrived) return;
        _arrived = true;

        LeaveTransit();

        Vector2? destination = _destination?.Invoke();
        if (destination.HasValue) Controller.TeleportTo(destination.Value);

        _onArrived?.Invoke();
    }

    private void LeaveTransit()
    {
        if (!_inTransit) return;
        _inTransit = false;
        Controller.SetInTransit(false);
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
