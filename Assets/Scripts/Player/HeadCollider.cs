using UnityEngine;

/// <summary>
/// Trigger placed at the top of the player's collider.
/// Deals damage to any IDamageable hit while the player is rising (VelocityY > 0).
/// Only one hit is allowed per jump: the ability is consumed on first contact
/// and restored automatically when the player lands (OnGroundedChanged event).
/// </summary>
public sealed class HeadCollider : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField, Tooltip("Reference to the player's root PlayerController.")]
    private PlayerController playerController;

    [SerializeField, Tooltip("Minimum upward velocity required to trigger a head-break.")]
    private float minUpwardVelocity = 0.5f;

    // ── Private state ─────────────────────────────────────────────────────────

    private PlayerMovement _movement;
    private bool           _canHit = true;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _movement = playerController.GetComponent<PlayerMovement>();
    }

    private void OnEnable()
    {
        _movement.OnGroundedChanged += HandleGroundedChanged;
    }

    private void OnDisable()
    {
        _movement.OnGroundedChanged -= HandleGroundedChanged;
        _canHit = true;
    }

    // ── Trigger ───────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_canHit) return;
        if (_movement.VelocityY < minUpwardVelocity) return;
        if (!other.TryGetComponent(out IDamageable damageable)) return;

        damageable.TakeDamage(1, playerController);

        // Consume the hit for this jump.
        _canHit = false;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void HandleGroundedChanged(bool isGrounded)
    {
        // Restore the ability to hit as soon as the player touches the ground.
        if (isGrounded)
            _canHit = true;
    }
}