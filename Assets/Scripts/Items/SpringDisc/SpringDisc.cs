using DG.Tweening;
using UnityEngine;

/// <summary>
/// Llanta (item catalog #5). Replaces the old placeable spring disc (design
/// decision, ago 2026): carried like any holdable but thrown on the place
/// input. It sticks where it first lands — pushing the player it hits, if
/// any — and stays there for the whole round as a trampoline that works for
/// everyone. Keeps the original disc's bounce feel and squash/recoil
/// animations. Once stuck it is immovable: kicks no longer shove it around.
/// </summary>
public class SpringDisc : ThrowableItem
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

    [Header("Render recoil")]
    [SerializeField] private float recoilDistance;
    [SerializeField] private float recoilAnimationDuration;
    [SerializeField] private Ease recoilAnimationEasing;
    [SerializeField] private Ease recoilAnimationReturnEasing;
    private Sequence recoilAnimation;
    private Vector3 spriteInitialPos;
    private int objectDirection = 1;

    private bool _stuck;

    void Awake()
    {
        spriteInitialPos = spriteRenderer.transform.localPosition;
    }

    public override void PlaceHoldable()
    {
        base.PlaceHoldable();
        objectDirection = (int)Mathf.Sign(transform.localScale.x);
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

        AudioManager.Instance.PlaySound("tire_bounce");

        TriggerAnimation();
        RecoilAnimation(-direction);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _stuck = false;
    }

    private void TriggerAnimation()
    {
        Sequence animationSequence = DOTween.Sequence();
        animationSequence.Append(transform.DOScaleY(animationScaleAmount, animationDuration / 2).From(1f).SetEase(animationEasing));
        animationSequence.Join(transform.DOScaleX(animationScaleAmount * objectDirection, animationDuration / 2).From(objectDirection).SetEase(animationEasing));
        animationSequence.Append(transform.DOScaleY(1f, animationDuration / 2).From(animationScaleAmount).SetEase(animationEasing));
        animationSequence.Join(transform.DOScaleX(objectDirection, animationDuration / 2).From(animationScaleAmount * objectDirection).SetEase(animationEasing));
        animationSequence.Play();
    }

    private void RecoilAnimation(Vector2 direction)
    {
        if (recoilAnimation != null && recoilAnimation.IsActive())
        {
            recoilAnimation.Kill();
            spriteRenderer.transform.localPosition = spriteInitialPos;
        }

        spriteInitialPos = spriteRenderer.transform.localPosition;
        Vector3 targetPosition = spriteInitialPos + (Vector3)(direction.normalized * recoilDistance);
        recoilAnimation = DOTween.Sequence();
        recoilAnimation.Append(spriteRenderer.transform.DOLocalMove(targetPosition, recoilAnimationDuration / 2).SetEase(recoilAnimationEasing));
        recoilAnimation.Append(spriteRenderer.transform.DOLocalMove(spriteInitialPos, recoilAnimationDuration / 2).SetEase(recoilAnimationReturnEasing));
        recoilAnimation.Play();
    }
}
