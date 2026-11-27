using UnityEngine;

/// <summary>
/// Pollo metálico (item catalog #9), Metal Cap style: the jump stays the same,
/// the fall is heavier and the glide is off, so descents are fast and precise.
/// Immune to pushes, stronger kick. The only defensive item in the catalog.
/// Queried by KickCollider for the kick impulse multiplier.
///
/// Look (nov 2026): the new rig puts on its "Metal" pieces (helmet, armour,
/// wing guards, boots) instead of the grey tint; the old rig keeps the tint.
/// </summary>
public sealed class MetalChickenState : PlayerItemState
{
    protected override string Label => "Metal Body";

    protected override bool UsesTint => Controller == null || Controller.Skin == null;

    public float KickMultiplier { get; private set; } = 1f;

    private float _jumpMultiplier        = 1f;
    private float _gravityMultiplier     = 1f;
    private float _fallGravityMultiplier = 1f;
    private bool  _disableGlide;

    public void Activate(
        float duration,
        float jumpMultiplier,
        float gravityMultiplier,
        float fallGravityMultiplier,
        float kickMultiplier,
        bool disableGlide,
        Color tint)
    {
        _jumpMultiplier        = jumpMultiplier;
        _gravityMultiplier     = gravityMultiplier;
        _fallGravityMultiplier = fallGravityMultiplier;
        _disableGlide          = disableGlide;
        KickMultiplier         = kickMultiplier;

        BeginState(duration, tint);
    }

    protected override void OnStateStarted()
    {
        Movement.JumpMultiplier        = _jumpMultiplier;
        Movement.GravityMultiplier     = _gravityMultiplier;
        Movement.FallGravityMultiplier = _fallGravityMultiplier;
        Movement.GlideEnabled          = !_disableGlide;
        Controller.ImpulseImmune       = true;
        ChickenSkin skin = Controller.Skin;
        if (skin != null) skin.SetMetal(true);
    }

    protected override void OnStateEnded()
    {
        Movement.JumpMultiplier        = 1f;
        Movement.GravityMultiplier     = 1f;
        Movement.FallGravityMultiplier = 1f;
        Movement.GlideEnabled          = true;
        Controller.ImpulseImmune       = false;
        ChickenSkin skin = Controller.Skin;
        if (skin != null) skin.SetMetal(false);
    }
}
