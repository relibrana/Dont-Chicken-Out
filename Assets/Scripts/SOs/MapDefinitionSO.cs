using UnityEngine;

/// <summary>
/// One playable map: everything the map selector and the loader need to know
/// about it. Each new map is one of these assets plus its scene in Build
/// Settings — no code changes.
/// </summary>
[CreateAssetMenu(fileName = "Map_", menuName = "Don't Chicken Out/Map Definition")]
public sealed class MapDefinitionSO : ScriptableObject
{
    [Tooltip("Stable id (save data, netcode). Do not rename once shipped.")]
    [SerializeField] private string id = "new_map";

    [SerializeField] private string displayName = "New Map";

    [Tooltip("Scene loaded when the match starts. Must be in Build Settings.")]
    [SerializeField] private string sceneName = "Main_Level";

    [Tooltip("Shown on the selector card.")]
    [SerializeField] private Sprite preview;

    public string Id          => id;
    public string DisplayName => displayName;
    public string SceneName   => sceneName;
    public Sprite Preview     => preview;
}
