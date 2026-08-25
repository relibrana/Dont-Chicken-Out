using System.Collections.Generic;
using UnityEngine;

public class ItemCapsule : MonoBehaviour, IDamageable
{
    [Header("Damage Visuals")]
    [SerializeField, Tooltip("Sprites por daño: [0]=intacto, [1]=dañado1, [2]=dañado2")]
    private List<Sprite> damageSprites = new();

    [SerializeField] private SpriteRenderer sr;

    private const int baseLife = 1;
    private int life;

    private void Awake()
    {
        ResetState();
    }

    private void OnDisable()
    {
        ResetState();
    }

    public void TakeDamage(int amount, PlayerController player)
    {
        if (amount <= 0) return;

        life -= amount;

        AudioManager.Instance.PlaySound("egg_break");

        if (life <= 0)
        {
            Break(player);
            return;
        }

        UpdateSprite();
    }

    private void Break(PlayerController player)
    {
        PoolingManager poolManager = GameManager.instance.poolManager;

        GameObject rolledPrefab = poolManager.itemsPool != null
            ? poolManager.itemsPool.GetWeightedPrefab()
            : null;

        GameObject randomItem = rolledPrefab != null
            ? poolManager.GetPooledItem(rolledPrefab, player.GetBlockPosition())
            : poolManager.GetPooledItem(Random.Range(0, poolManager.pooledItems.Count), player.GetBlockPosition());

        if (randomItem.TryGetComponent(out IInstantItem instantItem))
        {
            instantItem.Apply(player);
            randomItem.SetActive(false);
        }
        else
        {
            HoldableItem newBlock = randomItem.GetComponent<HoldableItem>();
            player.SwapBlock(newBlock);
        }

        gameObject.SetActive(false);
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

        int index = Mathf.Clamp(baseLife - life, 0, damageSprites.Count - 1);

        Sprite sprite = damageSprites[index];
        if (sprite != null)
            sr.sprite = sprite;
    }
}