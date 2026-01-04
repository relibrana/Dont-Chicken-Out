using UnityEngine;

public class KickCollider : MonoBehaviour
{
    public Vector2 forceDirection;
    public PlayerController playerController;

    private void OnTriggerEnter2D(Collider2D other)
    {
        //!LOGIC FOR BLOCKS

        if (other.TryGetComponent(out PlayerController playerCtrl))
        {
            Vector2 impulseDirection = forceDirection;
            impulseDirection.x *= transform.lossyScale.x;
            
            playerCtrl.AddImpulse(impulseDirection);
        }
        else if (!other.isTrigger && 
            (other.gameObject.CompareTag("Capsule") 
            || other.gameObject.CompareTag("Block") 
            || other.gameObject.CompareTag("Item")))
        {
            Vector2 impulseDirection = forceDirection;
            impulseDirection.x *= transform.lossyScale.x;

            other.attachedRigidbody.linearVelocity = impulseDirection;

            if(other.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(1, playerController);
            }
        }
    }
}
