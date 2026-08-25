using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Weighted item pool for capsule rewards — the "Distribución" balance lever
/// from the item catalog. Same weighting scheme as BlocksPoolSO.
/// </summary>
[CreateAssetMenu(fileName = "ItemsPool")]
public class ItemsPoolSO : ScriptableObject
{
    public List<ItemData> items;

    /// <summary>Rolls a prefab using the relative weights. Null if the pool is empty.</summary>
    public GameObject GetWeightedPrefab()
    {
        if (items == null || items.Count == 0) return null;

        float totalWeight = 0f;
        foreach (ItemData item in items) totalWeight += item.chance;

        if (totalWeight <= 0f) return items[0].prefab;

        float randomValue = Random.Range(0f, totalWeight);
        float processedWeight = 0f;

        foreach (ItemData item in items)
        {
            processedWeight += item.chance;
            if (randomValue <= processedWeight)
                return item.prefab;
        }

        return items[items.Count - 1].prefab;
    }
}

[System.Serializable]
public class ItemData
{
    public GameObject prefab;
    [Range(0, 100)] public float chance;
}
