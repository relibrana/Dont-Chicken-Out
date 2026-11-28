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

    [SerializeField, Min(0f), Tooltip("Cuánto se mete la máscara del preview dentro de la tabla oscura, para que no pise su borde claro.")]
    private float previewMaskInset = 6f;

    public MapDefinitionSO Map    { get; private set; }
    public bool            Locked => Map == null;
    public Button          Button { get; private set; }

    public event Action<MapCard> Focused;
    public event Action<MapCard> Clicked;

    private void Awake()
    {
        Button = GetComponent<Button>();
        Button.onClick.AddListener(() => Clicked?.Invoke(this));
        MaskPreview();
    }

    /// <summary>
    /// The map picture is a rectangle; the "?" tiles are a board with a shaped
    /// outline. Clip the picture to that same board (nov 2026), so a real map
    /// sits in the frame like the locked ones. Done here, at runtime, so the
    /// Lobby scene does not need regenerating.
    /// </summary>
    private void MaskPreview()
    {
        if (preview == null || lockedOverlay == null) return;
        if (preview.transform.parent != null && preview.transform.parent.name == "PreviewMask") return;
        if (!lockedOverlay.TryGetComponent(out Image board) || board.sprite == null) return;

        var go = new GameObject("PreviewMask", typeof(RectTransform), typeof(Image), typeof(Mask));
        var rt = (RectTransform)go.transform;
        var boardRt = (RectTransform)lockedOverlay.transform;
        rt.SetParent(preview.transform.parent, false);
        rt.SetSiblingIndex(preview.transform.GetSiblingIndex());
        rt.anchorMin = boardRt.anchorMin;
        rt.anchorMax = boardRt.anchorMax;
        rt.pivot     = boardRt.pivot;
        rt.offsetMin = boardRt.offsetMin + Vector2.one * previewMaskInset;
        rt.offsetMax = boardRt.offsetMax - Vector2.one * previewMaskInset;

        var shape = go.GetComponent<Image>();
        shape.sprite                  = board.sprite;
        shape.type                    = board.type;
        shape.pixelsPerUnitMultiplier = board.pixelsPerUnitMultiplier;
        shape.raycastTarget           = false;
        go.GetComponent<Mask>().showMaskGraphic = false;

        preview.transform.SetParent(rt, true);
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
