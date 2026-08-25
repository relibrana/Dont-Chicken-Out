using UnityEngine;

/// <summary>
/// Generic stun: the player keeps physics (falls, decelerates) but all inputs
/// are swallowed for the duration. Used by POW; reusable by any future stunner.
/// </summary>
public sealed class StunState : PlayerItemState
{
    public void Activate(float duration, Color tint) => BeginState(duration, tint);

    protected override void OnStateStarted() => Controller.PushMovementLock();

    protected override void OnStateEnded() => Controller.PopMovementLock();
}
