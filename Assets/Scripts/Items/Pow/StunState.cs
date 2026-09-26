using UnityEngine;

/// <summary>
/// Generic stun: all inputs are swallowed for the duration and the player is
/// left standing still — the lateral momentum they had is cut on the spot
/// (diseño, sep 2026: "los pollitos aturdidos se quedan quietos"), but gravity
/// still applies, so a stunned player on a ledge falls off normally.
/// Used by POW; reusable by any future stunner.
/// </summary>
public sealed class StunState : PlayerItemState
{
    public void Activate(float duration, Color tint) => BeginState(duration, tint);

    protected override void OnStateStarted()
    {
        Controller.PushMovementLock();

        // Sin esto el pollo sigue deslizándose por la deceleración normal y no
        // se lee como aturdido, se lee como que patina.
        Movement.StopHorizontal();
    }

    protected override void OnStateEnded() => Controller.PopMovementLock();
}
