using UnityEngine;

/// <summary>
/// Doble salto (item catalog #10). While active the player can jump once in
/// mid-air. In repeatable mode the air jump recharges on every landing; in
/// single-use mode the state grants exactly one air jump (doc leaves this open,
/// so both are supported via the pickup toggle).
/// </summary>
public sealed class DoubleJumpState : PlayerItemState
{
    private float _airJumpMultiplier = 1f;
    private bool  _repeatable;
    private bool  _subscribed;

    public void Activate(float duration, float airJumpMultiplier, bool repeatable, Color tint)
    {
        _airJumpMultiplier = airJumpMultiplier;
        _repeatable        = repeatable;

        BeginState(duration, tint);
    }

    protected override void OnStateStarted()
    {
        Movement.AirJumpsRemaining = 1;
        Movement.AirJumpMultiplier = _airJumpMultiplier;

        if (_repeatable && !_subscribed)
        {
            Movement.OnGroundedChanged += HandleGroundedChanged;
            _subscribed = true;
        }
    }

    protected override void OnStateEnded()
    {
        Movement.AirJumpsRemaining = 0;
        Movement.AirJumpMultiplier = 1f;

        if (_subscribed)
        {
            Movement.OnGroundedChanged -= HandleGroundedChanged;
            _subscribed = false;
        }
    }

    private void HandleGroundedChanged(bool grounded)
    {
        if (grounded && IsActive)
            Movement.AirJumpsRemaining = 1;
    }
}
