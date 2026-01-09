using DG.Tweening;
using UnityEngine;

public class SpringDisc : HoldableItem
{
    private Rigidbody2D _rb;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Bounce Forces")]
    [SerializeField] private float bounceForce;

    [Header("On Kick Settings")]
    [SerializeField] private float attenuationOnMovement;

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
    private int objectDirection;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        spriteInitialPos = spriteRenderer.transform.localPosition;
    }

    void FixedUpdate()
    {
        AttenuationOfMovement();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & detectLayer) == 0) return;

        Rigidbody2D rb = collision.gameObject.GetComponent<Rigidbody2D>();
        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (rb == null || player == null) return;

        Vector2 direction = ((Vector2)(collision.gameObject.transform.position - spriteRenderer.transform.position)).normalized;
        player.AddImpulse(direction * bounceForce);

        TriggerAnimation();
        RecoilAnimation(-direction);
    }
    public override void PlaceHoldable()
    {
        base.PlaceHoldable();
        objectDirection = (int)Mathf.Sign(transform.localScale.x);
    }

    private void AttenuationOfMovement()
    {
        Vector2 velocity = _rb.linearVelocity;
        if (velocity.sqrMagnitude > 0)
        {
            float x = Mathf.Lerp(velocity.x, 0, attenuationOnMovement * Time.fixedDeltaTime);
            float y = Mathf.Lerp(velocity.y, 0, attenuationOnMovement * 2f * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector2(x, y);

            if(_rb.linearVelocity.sqrMagnitude < 0.01f)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.bodyType = RigidbodyType2D.Kinematic;
            }
        }
    }

    public void OnKick()
    {
        _rb.bodyType = RigidbodyType2D.Dynamic;
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