using UnityEngine;

/// <summary>
/// The chicken's item slot (GDD, usar ítem, nov 2026): one manual item at a
/// time, kept apart from the piece in hand and used with its own button
/// (UseItem). Everyone sees which one it is: its icon floats over the head
/// until it is spent.
///
/// Manual items (bomb, tire, moco, POW, teleport) wait here; automatic ones
/// still apply on the spot when the capsule breaks and never touch the slot.
/// Breaking a capsule with an item already in the slot keeps the old one and
/// loses the new one. The item is lost on death and at the round reset.
///
/// The item GameObject stays pooled and switched off while it waits; using it
/// puts it in front of the chicken and runs its normal PlaceHoldable (throw,
/// teleport, POW countdown), the same path the place button used before.
/// </summary>
public sealed class PlayerItemSlot : MonoBehaviour
{
    [Tooltip("Dónde va el icono respecto al pollo: sobre la cabeza, por encima de la etiqueta 'Player N'.")]
    [SerializeField] private Vector3 iconOffset = new(0f, 2.3f, 0f);

    [Tooltip("Alto del icono en unidades de mundo.")]
    [SerializeField, Min(0.1f)] private float iconHeight = 0.6f;

    private PlayerController _player;
    private SpriteRenderer   _icon;

    /// <summary>The item waiting to be used, null if the slot is empty.</summary>
    public HoldableItem Held { get; private set; }

    private void Awake() => _player = GetComponent<PlayerController>();

    private void OnDestroy()
    {
        if (_icon != null) Destroy(_icon.gameObject);
    }

    private void LateUpdate()
    {
        if (_icon == null) return;

        bool show = Held != null && _player != null && _player.IsAlive && !_player.InTransit;
        _icon.enabled = show;
        if (show) _icon.transform.position = transform.position + iconOffset;
    }

    /// <summary>
    /// Puts a freshly won item in the slot. False (and the item is dropped) if
    /// the slot was already taken: the chicken keeps the item it had.
    /// </summary>
    public bool TryStore(HoldableItem item)
    {
        if (item == null) return false;

        if (Held != null)
        {
            item.gameObject.SetActive(false);
            return false;
        }

        Held = item;
        item.SetOwner(_player);
        item.gameObject.SetActive(false);
        ShowIcon(item.Icon);
        return true;
    }

    /// <summary>Uses the item in the slot from the chicken's hand. False if the slot is empty.</summary>
    public bool TryUse(Vector3 handPosition, float facingSign)
    {
        if (Held == null) return false;

        HoldableItem item = Held;
        Held = null;

        item.gameObject.SetActive(true);
        item.transform.position   = handPosition;
        item.transform.localScale = new Vector3(Mathf.Sign(facingSign == 0f ? 1f : facingSign), 1f, 1f);
        item.SetOwner(_player);
        item.StartHold();
        item.PlaceHoldable();
        return true;
    }

    /// <summary>Loses the item (death, round reset).</summary>
    public void Clear()
    {
        if (Held != null) Held.gameObject.SetActive(false);
        Held = null;
    }

    private void ShowIcon(Sprite sprite)
    {
        if (_icon == null)
        {
            var go = new GameObject($"ItemIcon_{name}");
            _icon = go.AddComponent<SpriteRenderer>();
            _icon.sortingLayerName = "Characters";
            _icon.sortingOrder     = 450; // over the chickens, under nothing that matters
        }

        _icon.sprite = sprite;
        float size = VisibleSize(sprite);
        _icon.transform.localScale = Vector3.one * (size > 0.0001f ? iconHeight / size : 1f);
        _icon.transform.position   = transform.position + iconOffset;
    }

    /// <summary>
    /// The larger side of what is actually drawn (the tight mesh), not the
    /// frame: the bomb's frames are 512 px for the explosion but the bomb
    /// itself is ~93 px, so sizing by the frame left its icon tiny.
    /// </summary>
    private static float VisibleSize(Sprite sprite)
    {
        if (sprite == null) return 0f;

        Vector2[] vertices = sprite.vertices;
        if (vertices == null || vertices.Length == 0)
            return Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);

        Vector2 min = vertices[0], max = vertices[0];
        foreach (Vector2 v in vertices)
        {
            min = Vector2.Min(min, v);
            max = Vector2.Max(max, v);
        }
        return Mathf.Max(max.x - min.x, max.y - min.y);
    }
}
