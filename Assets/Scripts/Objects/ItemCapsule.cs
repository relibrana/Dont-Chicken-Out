using System.Collections.Generic;
using UnityEngine;

public class ItemCapsule : IDamageable
{
    [SerializeField] private List<Color> colors = new();
    [SerializeField] private SpriteRenderer sr;
    private const int baseLife = 3;
    private int life;

    void Awake()
    {
        life = baseLife;
        sr.color = Color.green;
    }

    public override void TakeDamage(int amount, PlayerController player)
    {
        life -= amount;
        sr.color = colors[life];
        if(life <= 0)
        {
            Break(player);
        }
    }

    private void Break(PlayerController player)
    {
        PoolingManager poolManager = GameManager.instance.poolManager;
		int randomIndex = Random.Range(0, poolManager.pooledItems.Count);
		GameObject randomItem = poolManager.GetPooledItem(randomIndex, player.GetBlockPosition());
        if(player.currentBlockHolding != null)
        {
            player.currentBlockHolding.gameObject.SetActive(false);
        }
        player.currentBlockHolding = randomItem.GetComponent<HoldableItem>();
        player.currentBlockHolding.StartHold();
        gameObject.SetActive(false);
        life = baseLife;
        sr.color = Color.green;
    }
}
