using UnityEngine;

public class KickCollider : MonoBehaviour
{
    public Vector2 forceDirection;

    private void OnTriggerEnter2D(Collider2D other)
    {
        //!LOGIC FOR BLOCKS
        PlayerController playerController = other.gameObject.GetComponent<PlayerController>();

        if (playerController != null)
        {
            Vector2 impulseDirection = forceDirection;
            impulseDirection.x *= transform.lossyScale.x;
            
            playerController.AddImpulse(impulseDirection);
        }
    }
}
