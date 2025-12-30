using UnityEngine;

public class SpringDisc : MonoBehaviour
{
    [Header("Bounce Forces")]
    [SerializeField] private float bounceForce;

    [Header("Collission Settings")]
    [SerializeField] private LayerMask detectLayer;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & detectLayer) == 0) return;

        Rigidbody2D rb = collision.gameObject.GetComponent<Rigidbody2D>();
        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (rb == null || player == null) return;

        Vector2 direction = ((Vector2)(collision.gameObject.transform.position - transform.position)).normalized;
        player.AddImpulse(direction * bounceForce);
    }
}