using UnityEngine;

/// <summary>
/// Stuck-in-moco state. Freezes the body in place (no gravity, no input) until
/// the victim fills the struggle bar, or the accessibility time cap releases
/// them. Kicking and moving both feed the bar (a kick is worth more than a
/// step) and the bar drains on its own, so mashing has to outpace the drain
/// instead of just repeating one button at any speed.
/// PlayerController routes kick and move presses here while active.
///
/// Every press yanks the chicken towards the side it pushed (diseño, sep 2026:
/// "forcejeo por movimiento y por patada resueltos por código") — small for a
/// movement press, wider for a kick. The yank moves the visual rig, never the
/// body: the body is frozen on purpose and driven by the Rigidbody2D.
///
/// A short immunity after release keeps a lingering trap from re-catching the
/// victim on the same frame they escape.
/// </summary>
public sealed class MocoStuckState : PlayerItemState
{
    private const float ReleaseImmunitySeconds = 1f;

    private float _struggleNeeded;
    private float _struggleFilled;
    private float _drainPerSecond;
    private float _kickPoints;
    private float _movePoints;
    private Color _stuckTint;

    private float _releasedAt = float.NegativeInfinity;

    // Struggle yank.
    private float _moveYank;
    private float _kickYank;
    private float _yankDecay;
    private float _yankOffset;

    private Transform _visual;
    private Vector3   _visualBase;

    /// <summary>0–1 fill of the struggle bar. For the HUD once real UI exists.</summary>
    public float StruggleNormalized =>
        _struggleNeeded <= 0f ? 1f : Mathf.Clamp01(_struggleFilled / _struggleNeeded);

    public bool HasReleaseImmunity =>
        !IsActive && Time.time - _releasedAt < ReleaseImmunitySeconds;

    public void Activate(
        int strugglePresses,
        float kickPoints,
        float movePoints,
        float drainPerSecond,
        float maxStuckSeconds,
        float moveYank,
        float kickYank,
        float yankDecay,
        Color tint)
    {
        _struggleNeeded = Mathf.Max(1, strugglePresses);
        _struggleFilled = 0f;
        _kickPoints     = kickPoints;
        _movePoints     = movePoints;
        _drainPerSecond = drainPerSecond;
        _moveYank       = moveYank;
        _kickYank       = kickYank;
        _yankDecay      = Mathf.Max(0.01f, yankDecay);
        _stuckTint      = tint;

        BeginState(maxStuckSeconds, tint);
    }

    /// <summary>
    /// Called by PlayerController on each struggle input while stuck.
    /// A kick is worth more than a movement press: the kick is the intended
    /// escape, movement is the fallback so the state never feels unresponsive.
    /// </summary>
    public void OnStrugglePress(bool fromKick, float direction)
    {
        if (!IsActive) return;

        _struggleFilled += fromKick ? _kickPoints : _movePoints;

        float yank = fromKick ? _kickYank : _moveYank;
        float sign = direction != 0f ? Mathf.Sign(direction) : 1f;

        // Se acumula en vez de sobrescribirse: machacar rápido se siente como
        // un pollo tironeando, no como una sola sacudida que se reinicia.
        _yankOffset = Mathf.Clamp(_yankOffset + yank * sign, -yank * 2f, yank * 2f);

        if (_struggleFilled >= _struggleNeeded)
            EndState();
    }

    protected override void OnStateStarted()
    {
        Movement.HoldPosition = true;
        Controller.PushMovementLock();

        _visual     = Controller.VisualRoot;
        _visualBase = _visual != null ? _visual.localPosition : Vector3.zero;
        _yankOffset = 0f;
    }

    protected override void OnTick()
    {
        // The bar drains: stop mashing and you lose ground.
        if (_drainPerSecond > 0f && _struggleFilled > 0f)
            _struggleFilled = Mathf.Max(0f, _struggleFilled - _drainPerSecond * Time.deltaTime);

        TickYank();

        // Placeholder feedback until the bar has real UI: the tint washes out
        // as the victim gets closer to breaking free.
        SetTintColor(Color.Lerp(_stuckTint, Color.white, StruggleNormalized * 0.7f));
    }

    private void TickYank()
    {
        if (_visual == null) return;

        // Vuelve al centro sola: cada pulsación la aleja, el moco la devuelve.
        _yankOffset = Mathf.MoveTowards(_yankOffset, 0f, _yankDecay * Time.deltaTime);

        _visual.localPosition = _visualBase + new Vector3(_yankOffset, 0f, 0f);
    }

    protected override void OnStateEnded()
    {
        Movement.HoldPosition = false;
        Controller.PopMovementLock();

        if (_visual != null)
            _visual.localPosition = _visualBase;

        _yankOffset = 0f;
        _releasedAt = Time.time;
    }
}
