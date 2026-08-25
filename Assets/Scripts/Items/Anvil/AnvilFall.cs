using UnityEngine;

/// <summary>
/// Yunque in flight (item catalog #8). Two phases: a blinking telegraph hovering
/// at the top of the screen over the target column, then a straight kinematic
/// fall that kills any player it touches (owner included) and destroys every
/// sub-block it passes through, carving a clean vertical channel.
/// Spawned and configured by AnvilPickup; expects a trigger Collider2D.
/// </summary>
public sealed class AnvilFall : MonoBehaviour
{
    private float _fallSpeed;
    private float _telegraphSeconds;
    private bool  _stopOnKill;

    private bool  _falling;
    private float _telegraphElapsed;

    private Collider2D[]     _colliders;
    private SpriteRenderer[] _renderers;

    private const float BlinkFrequency = 8f;
    private const float DespawnMargin  = 6f;

    /// <summary>Called by AnvilPickup right after instantiation.</summary>
    public void Begin(float fallSpeed, float telegraphSeconds, bool stopOnKill)
    {
        _fallSpeed        = fallSpeed;
        _telegraphSeconds = telegraphSeconds;
        _stopOnKill       = stopOnKill;

        _colliders = GetComponentsInChildren<Collider2D>(includeInactive: true);
        _renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);

        foreach (Collider2D col in _colliders)
        {
            col.isTrigger = true;
            col.enabled   = false;
        }
    }

    private void Update()
    {
        if (!_falling)
        {
            TickTelegraph();
            return;
        }

        transform.Translate(Vector3.down * (_fallSpeed * Time.deltaTime), Space.World);

        if (Camera.main != null)
        {
            float bottom = Camera.main.ViewportToWorldPoint(Vector3.zero).y;
            if (transform.position.y < bottom - DespawnMargin)
                Destroy(gameObject);
        }
    }

    private void TickTelegraph()
    {
        _telegraphElapsed += Time.deltaTime;

        bool visible = Mathf.Repeat(_telegraphElapsed * BlinkFrequency, 1f) > 0.4f;
        foreach (SpriteRenderer sr in _renderers)
            sr.enabled = visible;

        if (_telegraphElapsed < _telegraphSeconds) return;

        _falling = true;

        foreach (SpriteRenderer sr in _renderers)
            sr.enabled = true;

        foreach (Collider2D col in _colliders)
            col.enabled = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_falling) return;

        if (other.TryGetComponent(out BlockDamageable block))
        {
            block.TakeDamage(999, null);
            return;
        }

        Rigidbody2D body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent(out PlayerController player)) return;
        if (!player.isOnGame) return;

        player.OnDeath();

        if (_stopOnKill)
            Destroy(gameObject);
    }
}
