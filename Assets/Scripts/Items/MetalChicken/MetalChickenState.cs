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
    private float _glideSpeedMultiplier = 2f;
    private float _massMultiplier       = 3f;
    private float _baseMass;
    private bool  _blinkOff;

    // Last second: the metal pieces blink so everyone sees it is ending (GDD).
    private const float BlinkWindow = 1f;
    private const float BlinkPeriod = 0.15f;

    public void Activate(
        float duration,
        float jumpMultiplier,
        float gravityMultiplier,
        float fallGravityMultiplier,
        float kickMultiplier,
        bool disableGlide,
        Color tint,
        float glideSpeedMultiplier = 2f,
        float massMultiplier = 3f)
    {
        _glideSpeedMultiplier = Mathf.Max(1f, glideSpeedMultiplier);
        _massMultiplier       = Mathf.Max(1f, massMultiplier);
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
        Controller.ImpulseImmune       = true; // kicks do nothing (GDD); walking into it still pushes it
        Movement.GlideSpeedMultiplier  = _glideSpeedMultiplier; // the metal weighs: glides, but falls faster

        // Heavier to push: a walking push moves it slower than a normal chicken.
        if (Controller.TryGetComponent(out Rigidbody2D rb))
        {
            _baseMass = rb.mass;
            rb.mass   = _baseMass * _massMultiplier;
        }

        _blinkOff = false;
        ChickenSkin skin = Controller.Skin;
        if (skin != null) skin.SetMetal(true);
    }

    protected override void OnRender()
    {
        ChickenSkin skin = Controller.Skin;
        if (skin == null) return;

        bool off = RenderRemaining < BlinkWindow && Mathf.FloorToInt(RenderRemaining / BlinkPeriod) % 2 == 1;
        if (off == _blinkOff) return;
        _blinkOff = off;
        skin.SetMetal(!off);
    }

    protected override void OnStateEnded()
    {
        Movement.JumpMultiplier        = 1f;
        Movement.GravityMultiplier     = 1f;
        Movement.FallGravityMultiplier = 1f;
        Movement.GlideEnabled          = true;
        Controller.ImpulseImmune       = false;
        Movement.GlideSpeedMultiplier  = 1f;
        if (Controller.TryGetComponent(out Rigidbody2D rb) && _baseMass > 0f)
            rb.mass = _baseMass;
        ChickenSkin skin = Controller.Skin;
        if (skin != null) skin.SetMetal(false);
    }
}
