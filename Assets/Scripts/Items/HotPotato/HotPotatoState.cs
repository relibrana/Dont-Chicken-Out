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
/// and the original window: the pulse accelerates against the whole window, not
/// against each carrier's slice of it.
///
/// Seen as the potato over the carrier's head (HotPotatoVisual), pulsing like
/// the bomb; it goes off with the bomb's explosion where the carrier stands
/// (design, oct 2026). No tint: the potato itself is the tell.
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

    private HotPotatoVisual _visualPrefab;
    private Vector3         _visualOffset;
    private HotPotatoVisual _visual;

    private float _noTransferUntil;

    private const float TransferImmunitySeconds = 0.5f;

    protected override bool UsesTint => false;

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
        HotPotatoVisual visualPrefab,
        Vector3 visualOffset)
    {
        Begin(
            fuseSeconds,
            fuseSeconds,
            speedMultiplier,
            explosionRadius,
            explosionImpulse,
            explosionBlockDamage,
            explosionLayers,
            visualPrefab,
            visualOffset);
    }

    private void Begin(
        float remaining,
        float window,
        float speedMultiplier,
        float explosionRadius,
        float explosionImpulse,
        int explosionBlockDamage,
        LayerMask explosionLayers,
        HotPotatoVisual visualPrefab,
        Vector3 visualOffset)
    {
        _fuseWindow           = Mathf.Max(remaining, window);
        _speedMultiplier      = speedMultiplier;
        _explosionRadius      = explosionRadius;
        _explosionImpulse     = explosionImpulse;
        _explosionBlockDamage = explosionBlockDamage;
        _explosionLayers      = explosionLayers;
        _visualPrefab         = visualPrefab;
        _visualOffset         = visualOffset;
        _noTransferUntil      = Time.time + TransferImmunitySeconds;

        BeginState(remaining, Color.white);
    }

    protected override void OnStateStarted()
    {
        Movement.SpeedMultiplier = _speedMultiplier;
        _visual = HotPotatoVisual.Spawn(_visualPrefab, transform, _visualOffset);
    }

    protected override void OnStateEnded()
    {
        Movement.SpeedMultiplier = 1f;

        // Hand-off or round reset: the potato just leaves this chicken. When it
        // explodes, TakeVisual already moved it out of here before EndState.
        if (_visual != null) _visual.Discard();
        _visual = null;
    }

    protected override void OnRender()
    {
        // Measured against _fuseWindow, so a hand-off does not reset the
        // warning — the new carrier picks it up already pulsing fast.
        float urgency = 1f - Mathf.Clamp01(RenderRemaining / Mathf.Max(0.01f, _fuseWindow));
        if (_visual != null) _visual.Pulse(urgency);
    }

    // EndState goes first so that, when Controller.OnDeath reaches
    // GameManager.OnPlayersDeath, this potato is no longer active and does not
    // get detonated a second time.
    protected override void OnStateExpired()
    {
        HotPotatoVisual visual = TakeVisual();
        EndState();
        Explode(visual);
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

        HotPotatoVisual visual = TakeVisual();
        EndState();
        Explode(visual);
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
            _visualPrefab,
            _visualOffset);

        EndState();
    }

    /// <summary>Hands the potato over to the explosion so EndState does not discard it.</summary>
    private HotPotatoVisual TakeVisual()
    {
        HotPotatoVisual visual = _visual;
        _visual = null;
        return visual;
    }

    private void Explode(HotPotatoVisual visual)
    {
        Vector2 origin = transform.position;

        if (visual != null) visual.Detonate(origin);
        SimEvents.Raise(SimEvent.HotPotatoExploded, origin, this);

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

            // Same falloff as the bomb (design, oct 2026): full push at the
            // centre, none at the edge of the radius. Unlike the bomb it does
            // not kill the neighbours, it only throws them.
            Vector2 toOther   = (Vector2)other.transform.position - origin;
            float   distance  = toOther.magnitude;
            Vector2 direction = distance > 0.0001f ? toOther / distance : Vector2.up;
            float   falloff   = Mathf.Clamp01(1f - distance / Mathf.Max(0.0001f, _explosionRadius));
            other.AddImpulse(direction * (_explosionImpulse * falloff), isKick: true, resetSpeed: true);
        }
    }
}
