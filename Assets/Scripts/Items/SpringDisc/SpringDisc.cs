using DG.Tweening;
using UnityEngine;

public class SpringDisc : HoldableItem
{
    private Rigidbody2D _rb;

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

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
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

        Vector2 direction = ((Vector2)(collision.gameObject.transform.position - transform.position)).normalized;
        player.AddImpulse(direction * bounceForce);

        TriggerAnimation();
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
        animationSequence.Append(transform.DOScale(animationScaleAmount, animationDuration / 2).From(1f).SetEase(animationEasing));
        animationSequence.Append(transform.DOScale(1f, animationDuration / 2).From(animationScaleAmount).SetEase(animationEasing));
        animationSequence.Play();
    }
}