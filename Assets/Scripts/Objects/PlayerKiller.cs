using UnityEngine;

public class PlayerKiller : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (GameManager.instance.gameState != GameState.Game)
            return;
        PlayerController playerController = collision.gameObject.GetComponent<PlayerController>();
        if(playerController != null)
        {
            playerController.OnDeath();
        }
    }
}
