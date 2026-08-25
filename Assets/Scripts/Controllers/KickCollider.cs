using UnityEngine;

public class KickCollider : MonoBehaviour
{
    public Vector2 forceDirection;
    public PlayerController playerController;

    private void OnTriggerEnter2D(Collider2D other)
    {
        SuperKickState superKick = null;
        bool superActive = playerController != null
            && playerController.TryGetComponent(out superKick)
            && superKick.IsActive;

        MetalChickenState metal = null;
        bool metalActive = playerController != null
            && playerController.TryGetComponent(out metal)
            && metal.IsActive;

        if(other.attachedRigidbody && other.attachedRigidbody.TryGetComponent(out IKickable kickable))
        {
            if (superActive && kickable is PlayerController victim && victim != playerController && victim.isOnGame)
            {
                victim.OnDeath();
            }
            else
            {
                Vector2 impulseDirection = forceDirection;
                impulseDirection.x *= transform.lossyScale.x;

                if (superActive)
                    impulseDirection *= superKick.ImpulseMultiplier;

                if (metalActive)
                    impulseDirection *= metal.KickMultiplier;

                kickable.ReceiveKick(impulseDirection);
            }
        }
        if(other.TryGetComponent(out IDamageable damageable))
        {
            damageable.TakeDamage(superActive ? superKick.BlockDamage : 1, playerController);
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
