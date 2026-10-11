using UnityEngine;

/// <summary>
/// Yunque in flight (item catalog #8). Two phases: the column telegraph
/// (AnvilZone opens over the target X while the anvil waits, hidden, above the
/// screen), then a straight kinematic fall that kills any player it touches
/// (owner included) and destroys every sub-block it passes through, carving a
/// clean vertical channel.
/// Spawned and configured by AnvilPickup; expects a trigger Collider2D.
/// </summary>
public sealed class AnvilFall : MonoBehaviour
{
    private float _fallSpeed;
    private bool  _stopOnKill;
    private bool  _falling;

    private AnvilZone        _zone;
    private Collider2D[]     _colliders;
    private SpriteRenderer[] _renderers;

    private const float DespawnMargin = 6f;

    /// <summary>Called by AnvilPickup right after instantiation.</summary>
    public void Begin(float fallSpeed, bool stopOnKill, AnvilZone zone, float zoneOpenSeconds, float arrowsFadeSeconds)
    {
        _fallSpeed  = fallSpeed;
        _stopOnKill = stopOnKill;
        _zone       = zone;

        _colliders = GetComponentsInChildren<Collider2D>(includeInactive: true);
        _renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);

        foreach (Collider2D col in _colliders)
        {
            col.isTrigger = true;
            col.enabled   = false;
        }
        foreach (SpriteRenderer sr in _renderers)
            sr.enabled = false;

        // "Luego de esa animación, cae el yunque" (design, oct 2026).
        if (_zone != null) _zone.Open(zoneOpenSeconds, arrowsFadeSeconds, StartFalling);
        else               StartFalling();
    }

    private void StartFalling()
    {
        if (this == null) return;

        _falling = true;

        foreach (SpriteRenderer sr in _renderers)
            sr.enabled = true;
        foreach (Collider2D col in _colliders)
            col.enabled = true;

        SimEvents.Raise(SimEvent.AnvilFell, transform.position, this);
    }

    private void Update()
    {
        if (!_falling) return;

        transform.Translate(Vector3.down * (_fallSpeed * Time.deltaTime), Space.World);

        float bottom = GameplayCamera.BottomY(float.NegativeInfinity);

        // Out of sight: the column fades now, the anvil is removed a bit later.
        if (transform.position.y < bottom && _zone != null)
        {
            _zone.Close();
            _zone = null;
        }

        if (transform.position.y < bottom - DespawnMargin)
            Destroy(gameObject);
    }

    // Leaving the screen, killing with stopOnKill, or any other teardown:
    // the column goes with the anvil.
    private void OnDestroy()
    {
        if (_zone != null) _zone.Close();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_falling) return;

        // AnvilHit sounds both when it smashes a block and when it crushes a
        // chicken (design, oct 2026).
        if (other.TryGetComponent(out BlockDamageable block))
        {
            block.TakeDamage(999, null);
            SimEvents.Raise(SimEvent.AnvilHit, other.transform.position, this);
            return;
        }

        Rigidbody2D body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent(out PlayerController player)) return;
        if (!player.isOnGame) return;

        player.OnDeath();
        SimEvents.Raise(SimEvent.AnvilHit, player.transform.position, this);

        if (_stopOnKill)
            Destroy(gameObject);
    }
}
