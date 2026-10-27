using UnityEngine;

/// <summary>
/// Timed "Súper patada" state (item catalog #1). While active, the owner's
/// kicks kill other players, break the sub-block they hit in one kick and push
/// harder. Added to the player at runtime by SuperKickPickup; queried by
/// KickCollider. Blinks during the last second as the reaction window (telegraph lever).
/// </summary>
public sealed class SuperKickState : PlayerItemState
{
    protected override string Label => "Super Kick";

    /// <summary>
    /// One kick breaks the sub-block it hits, whatever its life (design, oct 2026):
    /// only that sub-block, not the whole piece. Not a tuning knob on purpose.
    /// </summary>
    public const int BlockDamage = int.MaxValue;

    public float ImpulseMultiplier { get; private set; } = 1f;

    private const float BlinkWindow = 1f;
    private const float BlinkPeriod = 0.3f;

    /// <summary>Starts (or refreshes) the state. Values come from SuperKickPickup.</summary>
    public void Activate(float duration, float impulseMultiplier, Color tint)
    {
        ImpulseMultiplier = impulseMultiplier;

        BeginState(duration, tint);
    }

    protected override void OnRender()
    {
        float remaining = RenderRemaining;
        if (remaining <= BlinkWindow)
            SetTintVisible(Mathf.Repeat(remaining, BlinkPeriod) > BlinkPeriod * 0.5f);
    }
}
