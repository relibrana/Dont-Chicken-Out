using UnityEngine;

public class KickCollider : MonoBehaviour
{
    public Vector2 forceDirection;
    public PlayerController playerController;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.attachedRigidbody && other.attachedRigidbody.TryGetComponent(out IKickable kickable))
        {
            Vector2 impulseDirection = forceDirection;
            impulseDirection.x *= transform.lossyScale.x;
            kickable.ReceiveKick(impulseDirection);
        }
        if(other.TryGetComponent(out IDamageable damageable))
        {
            damageable.TakeDamage(1, playerController);
        }
        // if (!other.isTrigger && 
        //     (other.gameObject.CompareTag("Capsule") 
        //     || other.gameObject.CompareTag("Block") 
        //     || other.gameObject.CompareTag("Item"))) //This needs to be improved
        // {
        //     Vector2 impulseDirection = forceDirection;
        //     impulseDirection.x *= transform.lossyScale.x;

        //     other.GetComponent<IKickable>()?.OnKick();

        //     other.attachedRigidbody.linearVelocity = impulseDirection;
        // }
    }
}
