using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Weighted item pool for capsule rewards — the "Distribución" balance lever
/// from the item catalog. Weights per third of the view (GDD, asignación de
/// ítem, nov 2026): the item is decided when the capsule breaks, by the third
/// the breaker is in. Items with no per-third weights use the flat chance.
/// </summary>
[CreateAssetMenu(fileName = "ItemsPool")]
public class ItemsPoolSO : ScriptableObject
{
    public List<ItemData> items;

    /// <summary>Rolls a prefab using the flat weights. Null if the pool is empty.</summary>
    public GameObject GetWeightedPrefab() => GetWeightedPrefab(-1);

    /// <summary>
    /// Rolls a prefab for a third of the view (0 bottom, 1 middle, 2 top;
    /// GameplayCamera.ViewThird). -1 = flat weights. Null if the pool is empty.
    /// </summary>
    public GameObject GetWeightedPrefab(int viewThird)
    {
        if (items == null || items.Count == 0) return null;

        if (ItemTestFilter.Active)
        {
            GameObject filtered = GetFiltered(viewThird);
            if (filtered != null) return filtered;
        }

        float totalWeight = 0f;
        foreach (ItemData item in items) totalWeight += item.Weight(viewThird);

        if (totalWeight <= 0f) return items[0].prefab;

        float randomValue = Random.Range(0f, totalWeight);
        float processedWeight = 0f;

        foreach (ItemData item in items)
        {
            processedWeight += item.Weight(viewThird);
            if (randomValue <= processedWeight)
                return item.prefab;
        }

        return items[items.Count - 1].prefab;
    }

    /// <summary>
    /// Editor testing (ItemTestFilter): only the allowed items, by their weight
    /// in this third, or evenly if none of them has weight here. Null = no
    /// allowed item at all (the normal roll goes on).
    /// </summary>
    private GameObject GetFiltered(int viewThird)
    {
        int allowed = 0;
        float total = 0f;
        foreach (ItemData item in items)
        {
            if (item?.prefab == null || !ItemTestFilter.Allows(item.prefab)) continue;
            allowed++;
            total += item.Weight(viewThird);
        }
        if (allowed == 0) return null;

        bool even  = total <= 0f;
        float roll = Random.Range(0f, even ? allowed : total);
        float sum  = 0f;
        GameObject last = null;
        foreach (ItemData item in items)
        {
            if (item?.prefab == null || !ItemTestFilter.Allows(item.prefab)) continue;
            last = item.prefab;
            sum += even ? 1f : item.Weight(viewThird);
            if (roll <= sum) return item.prefab;
        }
        return last;
    }
}

[System.Serializable]
public class ItemData
{
    public GameObject prefab;
    [Range(0, 100), Tooltip("Peso plano (sin tercios). Solo se usa si los tres pesos por tercio están a 0.")]
    public float chance;

    [Header("Peso por tercio de la cámara")]
    [Range(0, 100)] public float bottomChance;
    [Range(0, 100)] public float middleChance;
    [Range(0, 100)] public float topChance;

    private bool HasThirds => bottomChance > 0f || middleChance > 0f || topChance > 0f;

    public float Weight(int viewThird)
    {
        if (viewThird < 0 || !HasThirds) return chance;
        return viewThird == 0 ? bottomChance : viewThird == 1 ? middleChance : topChance;
    }
}
