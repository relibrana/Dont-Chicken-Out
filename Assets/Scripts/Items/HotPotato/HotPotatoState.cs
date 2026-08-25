using UnityEngine;

/// <summary>
/// Papa caliente (item catalog #6). The carrier gets a speed buff and a fuse;
/// touching another player transfers potato and remaining fuse to them. If it
/// expires on you, you die and the blast pushes nearby players and damages
/// blocks. The tint pulses faster as the fuse runs out (visible-timer
/// placeholder until real UI/VFX).
/// </summary>
public sealed class HotPotatoState : PlayerItemState
{
    private float _fuseTotal;
    private float _speedMultiplier = 1f;
    private float _explosionRadius;
    private float _explosionImpulse;
    private int   _explosionBlockDamage;
    private LayerMask _explosionLayers;
    private Color _baseTint;

    private float _noTransferUntil;

    private const float TransferImmunitySeconds = 0.5f;

    public void Activate(
        float fuseSeconds,
        float speedMultiplier,
        float explosionRadius,
        float explosionImpulse,
        int explosionBlockDamage,
        LayerMask explosionLayers,
        Color tint)
    {
        _fuseTotal            = fuseSeconds;
        _speedMultiplier      = speedMultiplier;
        _explosionRadius      = explosionRadius;
        _explosionImpulse     = explosionImpulse;
        _explosionBlockDamage = explosionBlockDamage;
        _explosionLayers      = explosionLayers;
        _baseTint             = tint;
        _noTransferUntil      = Time.time + TransferImmunitySeconds;

        BeginState(fuseSeconds, tint);
    }

    protected override void OnStateStarted() => Movement.SpeedMultiplier = _speedMultiplier;

    protected override void OnStateEnded() => Movement.SpeedMultiplier = 1f;

    protected override void OnTick()
    {
        // Pulse speeds up as the fuse shortens: ~2 Hz early, ~8 Hz at the end.
        float urgency   = 1f - Mathf.Clamp01(Remaining / Mathf.Max(0.01f, _fuseTotal));
        float frequency = Mathf.Lerp(2f, 8f, urgency);
        float wave      = 0.5f + 0.5f * Mathf.Sin(Time.time * frequency * 2f * Mathf.PI);

        SetTintColor(Color.Lerp(Color.white, _baseTint, 0.35f + 0.65f * wave));
    }

    protected override void OnStateExpired()
    {
        Explode();
        EndState();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsActive) return;
        if (Time.time < _noTransferUntil) return;

        Rigidbody2D body = collision.collider.attachedRigidbody;
        if (body == null || !body.TryGetComponent(out PlayerController other)) return;
        if (other == Controller || !other.isOnGame) return;

        TransferTo(other);
    }

    private void TransferTo(PlayerController receiver)
    {
        if (!receiver.TryGetComponent(out HotPotatoState theirs))
            theirs = receiver.gameObject.AddComponent<HotPotatoState>();

        theirs.Activate(
            Remaining,
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

        Controller.OnDeath();
    }
}
