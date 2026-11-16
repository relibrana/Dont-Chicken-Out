using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The maps shown in the selector, in order, followed by locked "?" cards for
/// maps that are not in the game yet.
/// </summary>
[CreateAssetMenu(fileName = "MapCatalog", menuName = "Don't Chicken Out/Map Catalog")]
public sealed class MapCatalogSO : ScriptableObject
{
    [SerializeField] private List<MapDefinitionSO> maps = new();

    [Tooltip("Locked \"?\" cards shown after the real maps.")]
    [SerializeField, Min(0)] private int comingSoonSlots = 5;

    public IReadOnlyList<MapDefinitionSO> Maps => maps;
    public int ComingSoonSlots => comingSoonSlots;
}
