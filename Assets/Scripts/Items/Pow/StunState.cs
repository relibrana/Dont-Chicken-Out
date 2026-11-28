using UnityEngine;

/// <summary>
/// Generic stun: all inputs are swallowed for the duration and the player is
/// left standing still — the lateral momentum they had is cut on the spot
/// (diseño, sep 2026: "los pollitos aturdidos se quedan quietos"), but gravity
/// still applies, so a stunned player on a ledge falls off normally.
/// Used by POW; reusable by any future stunner.
///
/// Look (nov 2026): with the new rig the Stun clip shows it (stars over the
/// head, dizzy eyes and beak), so there is no tint. The old rig keeps the tint.
/// </summary>
public sealed class StunState : PlayerItemState
{
    protected override bool UsesTint => Controller == null || Controller.Skin == null;

    public void Activate(float duration, Color tint) => BeginState(duration, tint);

    protected override void OnStateStarted()
    {
        Controller.PushMovementLock();

        // Sin esto el pollo sigue deslizándose por la deceleración normal y no
        // se lee como aturdido, se lee como que patina.
        Movement.StopHorizontal();
        Movement.GlideLocks++; // GDD: kicked while stunned flies off without gliding

        KeepStunned();
    }

    protected override void OnRender() => KeepStunned();

    private void KeepStunned()
    {
        PlayerAnimController anim = Controller.AnimController;
        if (anim != null) anim.KeepStunned();
    }

    protected override void OnStateEnded()
    {
        Controller.PopMovementLock();
        Movement.GlideLocks = Mathf.Max(0, Movement.GlideLocks - 1);
        PlayerAnimController anim = Controller.AnimController;
        if (anim != null) anim.EndStun();
    }
}
