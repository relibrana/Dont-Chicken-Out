using UnityEngine;

/// <summary>
/// The egg capsule. Breaks on the first hit (design, oct 2026: no damage
/// states) and hands an item from the weighted pool to whoever broke it.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ItemCapsule : MonoBehaviour, IDamageable
{
    private Rigidbody2D _rb;
    private bool        _armInterpolation;

    private void Awake() => _rb = GetComponent<Rigidbody2D>();

    // Pooled: HorizontalSpawner moves the capsule right after activating it.
    // Interpolation (smooth drawing between physics ticks) is armed one tick
    // later, or it would draw the egg sliding from where it was last disabled.
    private void OnEnable()
    {
        _rb.interpolation = RigidbodyInterpolation2D.None;
        _armInterpolation = true;
    }

    private void FixedUpdate()
    {
        if (!_armInterpolation) return;

        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _armInterpolation = false;
    }

    public void TakeDamage(int amount, PlayerController player)
    {
        if (amount <= 0) return;

        // Two kicks landing in the same physics step both reach here; only the
        // first one may break the egg, or it would hand out two items.
        if (!gameObject.activeInHierarchy) return;

        SimEvents.Raise(SimEvent.CapsuleHit, transform.position, this);
        Break(player);
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
}
