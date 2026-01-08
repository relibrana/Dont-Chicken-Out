using System.Collections.Generic;
using UnityEngine;

public class ItemCapsule : IDamageable
{
    [Header("Damage Visuals")]
    [SerializeField, Tooltip("Sprites por daño: [0]=intacto, [1]=dañado1, [2]=dañado2")]
    private List<Sprite> damageSprites = new();

    [SerializeField] private SpriteRenderer sr;

    private const int baseLife = 3;
    private int life;

    private void Awake()
    {
        ResetState();
    }

    public override void TakeDamage(int amount, PlayerController player)
    {
        if (amount <= 0) return;

        life -= amount;

        if (life <= 0)
        {
            Break(player);
            return;
        }

        UpdateSprite();
    }

    private void OnDisable()
    {
        ResetState();
    }

    private void ResetState()
    {
        life = baseLife;
        UpdateSprite();
    }

    private void UpdateSprite()
    {
        if (sr == null) return;
        if (damageSprites == null || damageSprites.Count == 0) return;

        int index = baseLife - life;

        index = Mathf.Clamp(index, 0, damageSprites.Count - 1);

        Sprite sprite = damageSprites[index];
        if (sprite != null)
            sr.sprite = sprite;
    }

    private void Break(PlayerController player)
    {
        PoolingManager poolManager = GameManager.instance.poolManager;
        int randomIndex = Random.Range(0, poolManager.pooledItems.Count);

        GameObject randomItem = poolManager.GetPooledItem(
            randomIndex,
            player.GetBlockPosition()
        );

        if (player.currentBlockHolding != null)
            player.currentBlockHolding.gameObject.SetActive(false);

        player.currentBlockHolding = randomItem.GetComponent<HoldableItem>();
        player.currentBlockHolding.StartHold();

        gameObject.SetActive(false);
    }
}
