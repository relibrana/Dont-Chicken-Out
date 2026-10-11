using DG.Tweening;
using UnityEngine;

/// <summary>
/// Llanta (item catalog #5). Replaces the old placeable spring disc (design
/// decision, ago 2026): carried like any holdable but thrown on the place
/// input. It sticks where it first lands — pushing the player it hits, if
/// any — and stays there for the whole round as a trampoline that works for
/// everyone. Keeps the original disc's bounce feel and squash animation.
/// Once stuck it is immovable: kicks no longer shove it around.
/// Named SpringDisc until oct 2026.
/// </summary>
public class Tire : ThrowableItem
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Bounce Forces")]
    [SerializeField] private Vector2 bounceForce;

    [SerializeField, Tooltip("Empuje al jugador si el proyectil lo golpea en vuelo (doc: 'lo empuja y queda suspendida').")]
    private Vector2 hitPushForce = new Vector2(8f, 6f);

    [Header("Collission Settings")]
    [SerializeField] private LayerMask detectLayer;

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration;
    [SerializeField] private float animationScaleAmount;
    [SerializeField] private Ease animationEasing;

    private Sequence squashAnimation;
    private Vector3 spriteBaseScale;

    private bool _stuck;

    void Awake()
    {
        spriteBaseScale = spriteRenderer.transform.localScale;
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_stuck)
        {
            base.OnCollisionEnter2D(collision);
            return;
        }

        Bounce(collision);
    }

    protected override void OnProjectileHit(Collision2D collision)
    {
        Rigidbody2D hitBody = collision.collider.attachedRigidbody;

        if (hitBody != null && hitBody.TryGetComponent(out PlayerController victim))
        {
            Vector2 direction = ((Vector2)(victim.transform.position - transform.position)).normalized;
            victim.AddImpulse(Vector2.Scale(direction, hitPushForce), isKick: true, resetSpeed: true);
        }

        Stick();
    }

    private void Stick()
    {
        _stuck = true;

        rb2d.linearVelocity = Vector2.zero;
        rb2d.bodyType       = RigidbodyType2D.Static;
    }

    private void Bounce(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & detectLayer) == 0) return;

        Rigidbody2D rb = collision.gameObject.GetComponent<Rigidbody2D>();
        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (rb == null || player == null) return;

        Vector2 direction = ((Vector2)(collision.gameObject.transform.position - spriteRenderer.transform.position)).normalized;
        player.AddImpulse(direction * bounceForce, resetSpeed: true);

        SimEvents.Raise(SimEvent.TireBounced, transform.position, this);

        TriggerAnimation();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _stuck = false;

        // Pooled: never come back squashed.
        squashAnimation?.Kill();
        spriteRenderer.transform.localScale = spriteBaseScale;
    }

    // Only the rubber (spriteRenderer, Item-LlantaBack) squashes, in place: it
    // no longer recoils sideways, which pulled it off the rigid hub on top
    // (Item-LlantaFront) and showed the hole inside (design, oct 2026). The hub
    // is a sibling and stays still, so the root is never scaled here — which
    // also keeps its facing flip intact.
    private void TriggerAnimation()
    {
        Transform rubber = spriteRenderer.transform;
        Vector3 squashed = spriteBaseScale * animationScaleAmount;

        if (squashAnimation != null && squashAnimation.IsActive())
            squashAnimation.Kill();
        rubber.localScale = spriteBaseScale;

        squashAnimation = DOTween.Sequence();
        squashAnimation.Append(rubber.DOScale(squashed, animationDuration / 2).SetEase(animationEasing));
        squashAnimation.Append(rubber.DOScale(spriteBaseScale, animationDuration / 2).SetEase(animationEasing));
        squashAnimation.Play();
    }
}
