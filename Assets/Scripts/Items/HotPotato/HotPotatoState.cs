using UnityEngine;

/// <summary>
/// Papa caliente (item catalog #6). The carrier gets a speed buff and a fuse;
/// KICKING another player transfers potato and remaining fuse to them — contact
/// alone does nothing (diseño, sep 2026: pasarla tiene que ser deliberado).
///
/// The fuse is an ABSOLUTE window (diseño, sep 2026): it starts when the potato
/// appears and keeps running across every hand-off — a transfer never resets it
/// nor restarts the warning. Whoever holds it when it hits zero dies, no matter
/// how recently they got it. That is why the transfer carries BOTH the time left
/// and the original window: the blink accelerates against the whole window, not
/// against each carrier's slice of it.
/// </summary>
public sealed class HotPotatoState : PlayerItemState
{
    /// <summary>Full window the potato was created with. Survives every transfer.</summary>
    private float _fuseWindow;

    private float _speedMultiplier = 1f;
    private float _explosionRadius;
    private float _explosionImpulse;
    private int   _explosionBlockDamage;
    private LayerMask _explosionLayers;
    private Color _baseTint;

    private float _noTransferUntil;

    private const float TransferImmunitySeconds = 0.5f;

    /// <summary>Seconds left on the shared timer. For the HUD once real UI exists.</summary>
    public float FuseRemaining => IsActive ? Remaining : 0f;

    /// <summary>Fresh potato out of a capsule: the window starts now.</summary>
    public void Activate(
        float fuseSeconds,
        float speedMultiplier,
        float explosionRadius,
        float explosionImpulse,
        int explosionBlockDamage,
        LayerMask explosionLayers,
        Color tint)
    {
        Begin(
            fuseSeconds,
            fuseSeconds,
            speedMultiplier,
            explosionRadius,
            explosionImpulse,
            explosionBlockDamage,
            explosionLayers,
            tint);
    }

    private void Begin(
        float remaining,
        float window,
        float speedMultiplier,
        float explosionRadius,
        float explosionImpulse,
        int explosionBlockDamage,
        LayerMask explosionLayers,
        Color tint)
    {
        _fuseWindow           = Mathf.Max(remaining, window);
        _speedMultiplier      = speedMultiplier;
        _explosionRadius      = explosionRadius;
        _explosionImpulse     = explosionImpulse;
        _explosionBlockDamage = explosionBlockDamage;
        _explosionLayers      = explosionLayers;
        _baseTint             = tint;
        _noTransferUntil      = Time.time + TransferImmunitySeconds;

        BeginState(remaining, tint);
    }

    protected override void OnStateStarted() => Movement.SpeedMultiplier = _speedMultiplier;

    protected override void OnStateEnded() => Movement.SpeedMultiplier = 1f;

    protected override void OnTick()
    {
        // Pulse speeds up as the shared window runs out: ~2 Hz early, ~8 Hz at
        // the end. Measured against _fuseWindow, so a hand-off does not reset
        // the warning — the new carrier picks it up already blinking fast.
        float urgency   = 1f - Mathf.Clamp01(Remaining / Mathf.Max(0.01f, _fuseWindow));
        float frequency = Mathf.Lerp(2f, 8f, urgency);
        float wave      = 0.5f + 0.5f * Mathf.Sin(Time.time * frequency * 2f * Mathf.PI);

        SetTintColor(Color.Lerp(Color.white, _baseTint, 0.35f + 0.65f * wave));
    }

    // EndState goes first so that, when Controller.OnDeath reaches
    // GameManager.OnPlayersDeath, this potato is no longer active and does not
    // get detonated a second time.
    protected override void OnStateExpired()
    {
        EndState();
        Explode();
        Controller.OnDeath();
    }

    /// <summary>
    /// The carrier died with the potato in hand (design, oct 2026): it goes off
    /// right there. Called by GameManager before the dead chicken is moved away,
    /// so the blast happens where they died. The carrier is already dying, so
    /// it does not kill them again.
    /// </summary>
    public void DetonateOnCarrierDeath()
    {
        if (!IsActive) return;

        EndState();
        Explode();
    }

    /// <summary>
    /// Called by KickCollider when the carrier lands a kick on another player.
    /// The short grace after receiving it stops the potato from ping-ponging
    /// between two players trading kicks in the same instant.
    /// </summary>
    public bool TryTransferByKick(PlayerController victim)
    {
        if (!IsActive) return false;
        if (Time.time < _noTransferUntil) return false;
        if (victim == null || victim == Controller || !victim.isOnGame) return false;

        TransferTo(victim);
        return true;
    }

    private void TransferTo(PlayerController receiver)
    {
        if (!receiver.TryGetComponent(out HotPotatoState theirs))
            theirs = receiver.gameObject.AddComponent<HotPotatoState>();

        // Time left AND the original window travel together: the countdown is
        // one continuous clock owned by the potato, not by whoever holds it.
        theirs.Begin(
            Remaining,
            _fuseWindow,
            _speedMultiplier,
            _explosionRadius,
            _explosionImpulse,
            _explosionBlockDamage,
            _explosionLayers,
            _baseTint);

        EndState();
    }

    private void Explode()
    {
        Vector2 origin = transform.position;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, _explosionRadius, _explosionLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit.TryGetComponent(out BlockDamageable block))
            {
                block.TakeDamage(_explosionBlockDamage, Controller);
                continue;
            }

            Rigidbody2D body = hit.attachedRigidbody;
            if (body == null || !body.TryGetComponent(out PlayerController other)) continue;
            if (other == Controller) continue;

            Vector2 toOther  = (Vector2)other.transform.position - origin;
            Vector2 direction = toOther.sqrMagnitude > 0.0001f ? toOther.normalized : Vector2.up;
            other.AddImpulse(direction * _explosionImpulse, isKick: true, resetSpeed: true);
        }
    }
}
