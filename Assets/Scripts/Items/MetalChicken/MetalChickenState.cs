using UnityEngine;

/// <summary>
/// Pollo metálico (item catalog #9), Metal Cap style: heavier and lower jump,
/// immune to pushes, stronger kick. The only defensive item in the catalog.
/// Queried by KickCollider for the kick impulse multiplier.
/// </summary>
public sealed class MetalChickenState : PlayerItemState
{
    public float KickMultiplier { get; private set; } = 1f;

    private float _jumpMultiplier    = 1f;
    private float _gravityMultiplier = 1f;

    public void Activate(
        float duration,
        float jumpMultiplier,
        float gravityMultiplier,
        float kickMultiplier,
        Color tint)
    {
        _jumpMultiplier    = jumpMultiplier;
        _gravityMultiplier = gravityMultiplier;
        KickMultiplier     = kickMultiplier;

        BeginState(duration, tint);
    }

    protected override void OnStateStarted()
    {
        Movement.JumpMultiplier    = _jumpMultiplier;
        Movement.GravityMultiplier = _gravityMultiplier;
        Controller.ImpulseImmune   = true;
    }

    protected override void OnStateEnded()
    {
        Movement.JumpMultiplier    = 1f;
        Movement.GravityMultiplier = 1f;
        Controller.ImpulseImmune   = false;
    }
}
