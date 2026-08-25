using UnityEngine;

/// <summary>
/// Stuck-in-moco state. Freezes the body in place (no gravity, no input) until
/// the victim mashes the kick button enough times, or the accessibility time
/// cap releases them. PlayerController routes kick presses here while active.
/// A short immunity after release keeps a lingering trap from re-catching the
/// victim on the same frame they escape.
/// </summary>
public sealed class MocoStuckState : PlayerItemState
{
    private const float ReleaseImmunitySeconds = 1f;

    private int   _pressesLeft;
    private float _releasedAt = float.NegativeInfinity;

    public bool HasReleaseImmunity =>
        !IsActive && Time.time - _releasedAt < ReleaseImmunitySeconds;

    public void Activate(int strugglePresses, float maxStuckSeconds, Color tint)
    {
        _pressesLeft = strugglePresses;
        BeginState(maxStuckSeconds, tint);
    }

    /// <summary>Called by PlayerController on each kick press while stuck.</summary>
    public void OnStrugglePress()
    {
        if (!IsActive) return;

        _pressesLeft--;

        if (_pressesLeft <= 0)
            EndState();
    }

    protected override void OnStateStarted()
    {
        Movement.HoldPosition = true;
        Controller.PushMovementLock();
    }

    protected override void OnStateEnded()
    {
        Movement.HoldPosition = false;
        Controller.PopMovementLock();
        _releasedAt = Time.time;
    }
}
