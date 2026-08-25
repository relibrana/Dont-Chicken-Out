using UnityEngine;

/// <summary>
/// Timed "Súper patada" state (item catalog #1). While active, the owner's
/// kicks kill other players, deal extra damage to blocks and push harder.
/// Added to the player at runtime by SuperKickPickup; queried by KickCollider.
/// Blinks during the last second as the reaction window (telegraph lever).
/// </summary>
public sealed class SuperKickState : PlayerItemState
{
    public int   BlockDamage       { get; private set; } = 1;
    public float ImpulseMultiplier { get; private set; } = 1f;

    private const float BlinkWindow = 1f;
    private const float BlinkPeriod = 0.3f;

    /// <summary>Starts (or refreshes) the state. Values come from SuperKickPickup.</summary>
    public void Activate(float duration, int blockDamage, float impulseMultiplier, Color tint)
    {
        BlockDamage       = blockDamage;
        ImpulseMultiplier = impulseMultiplier;

        BeginState(duration, tint);
    }

    protected override void OnTick()
    {
        if (Remaining <= BlinkWindow)
            SetTintVisible(Mathf.Repeat(Remaining, BlinkPeriod) > BlinkPeriod * 0.5f);
    }
}
