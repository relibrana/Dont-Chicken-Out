using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One tile of the map selector. Either a real map (preview + name) or a
/// locked "?" tile for a map that is not in the game yet. Locked tiles still
/// take focus, so the grid feels complete and the player can browse it.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class MapCard : MonoBehaviour, ISelectHandler
{
    [SerializeField] private Image           preview;
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private GameObject      lockedOverlay;

    public MapDefinitionSO Map    { get; private set; }
    public bool            Locked => Map == null;
    public Button          Button { get; private set; }

    public event Action<MapCard> Focused;
    public event Action<MapCard> Clicked;

    private void Awake()
    {
        Button = GetComponent<Button>();
        Button.onClick.AddListener(() => Clicked?.Invoke(this));
    }

    public void Bind(MapDefinitionSO map)
    {
        Map = map;

        if (preview != null)
        {
            preview.sprite  = map != null ? map.Preview : null;
            preview.enabled = map != null && map.Preview != null;
        }
        if (nameLabel != null)     nameLabel.text = map != null ? map.DisplayName : "?";
        if (lockedOverlay != null) lockedOverlay.SetActive(map == null);
    }

    public void OnSelect(BaseEventData eventData) => Focused?.Invoke(this);
}
