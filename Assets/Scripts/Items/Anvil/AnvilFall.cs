using UnityEngine;

/// <summary>
/// Yunque in flight (item catalog #8). Two phases: the column telegraph
/// (AnvilZone opens over the target X while the anvil waits, hidden, above the
/// screen), then a straight kinematic fall that kills any player it touches
/// (owner included) and destroys every sub-block it passes through, carving a
/// clean vertical channel.
/// Handed out by AnvilPickup from PoolingManager (so the round reset turns it
/// off like any other item); expects a trigger Collider2D.
/// The body is Kinematic and interpolated: the warning and the fall both run
/// on the simulation tick (FixedUpdate, MovePosition) and it is drawn smoothly
/// between ticks. The column only draws the warning; this decides when it falls.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class AnvilFall : MonoBehaviour
{
    private Rigidbody2D _rb;
    private float _fallSpeed;
    private bool  _stopOnKill;
    private bool  _warning;
    private float _warningRemaining;
    private bool  _falling;

    private AnvilZone        _zone;
    private Collider2D[]     _colliders;
    private SpriteRenderer[] _renderers;

    private const float DespawnMargin = 6f;

    private void Awake()
    {
        _rb        = GetComponent<Rigidbody2D>();
        _colliders = GetComponentsInChildren<Collider2D>(includeInactive: true);
        _renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
    }

    /// <summary>Called by AnvilPickup right after taking it from the pool.</summary>
    public void Begin(Vector2 spawnPosition, float fallSpeed, bool stopOnKill, AnvilZone zone, float zoneOpenSeconds, float arrowsFadeSeconds)
    {
        // Body and Transform both: the body is interpolated, and a Transform
        // write alone would be overwritten by the interpolation.
        _rb.position       = spawnPosition;
        transform.position = spawnPosition;

        _fallSpeed  = fallSpeed;
        _stopOnKill = stopOnKill;
        _zone       = zone;
        _falling    = false;

        foreach (Collider2D col in _colliders)
        {
            col.isTrigger = true;
            col.enabled   = false;
        }
        foreach (SpriteRenderer sr in _renderers)
            sr.enabled = false;

        // "Luego de esa animación, cae el yunque" (design, oct 2026): the
        // warning lasts as long as the column's animation, but is counted here.
        if (_zone != null) _zone.Open(zoneOpenSeconds, arrowsFadeSeconds);

        _warning          = true;
        _warningRemaining = zoneOpenSeconds + arrowsFadeSeconds;
    }

    private void StartFalling()
    {
        _warning = false;
        _falling = true;

        foreach (SpriteRenderer sr in _renderers)
            sr.enabled = true;
        foreach (Collider2D col in _colliders)
            col.enabled = true;

        SimEvents.Raise(SimEvent.AnvilFell, transform.position, this);
    }

    private void FixedUpdate()
    {
        if (_warning)
        {
            _warningRemaining -= Time.fixedDeltaTime;
            if (_warningRemaining <= 0f)
                StartFalling();
            return;
        }

        if (!_falling) return;

        Vector2 next = _rb.position + Vector2.down * (_fallSpeed * Time.fixedDeltaTime);
        _rb.MovePosition(next);

        float bottom = GameplayCamera.BottomY(float.NegativeInfinity);

        // Out of sight: the column fades now, the anvil is removed a bit later.
        if (next.y < bottom && _zone != null)
        {
            _zone.Close();
            _zone = null;
        }

        if (next.y < bottom - DespawnMargin)
            gameObject.SetActive(false);
    }

    // Round over before it lands: back to the pool, column included.
    private void OnEnable() => GameManager.RoundEnded += HandleRoundEnded;

    // Leaving the screen, killing with stopOnKill, the round reset or any
    // other teardown: the column goes with the anvil.
    private void OnDisable()
    {
        GameManager.RoundEnded -= HandleRoundEnded;

        _warning = false;
        _falling = false;

        if (_zone != null) _zone.Close();
        _zone = null;
    }

    private void HandleRoundEnded() => gameObject.SetActive(false);

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
            gameObject.SetActive(false);
    }
}
