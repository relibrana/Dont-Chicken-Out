using TMPro;
using UnityEngine;

/// <summary>
/// World-space "Player 1..4" tag that floats over a chicken, in that player's
/// colour (diseño, sep 2026 — referencia: las etiquetas de Smash). El color
/// sale del shader del propio pollo, así que etiqueta y pollo no se pueden
/// desincronizar.
///
/// Deliberately NOT a child of the player: the chicken flips by negating its
/// localScale.x when it turns around, and a child label would read mirrored.
/// It follows in LateUpdate instead, after movement has run for the frame.
///
/// The text is a plain string so name customisation can drop straight in
/// later — SetLabel is the only thing a future "player names" screen needs.
/// </summary>
public sealed class PlayerNameTag : MonoBehaviour
{
    /// <summary>
    /// Fallback colours by player index, used only if neither MaterialsSO nor
    /// the chicken's material say anything. Red / blue / yellow / green.
    /// </summary>
    private static readonly Color[] DefaultPalette =
    {
        new Color(1f,    0.35f, 0.35f, 1f),
        new Color(0.35f, 0.66f, 1f,    1f),
        new Color(1f,    0.83f, 0.30f, 1f),
        new Color(0.42f, 0.88f, 0.42f, 1f),
    };

    private const float BackdropPaddingX = 0.22f;
    private const float BackdropPaddingY = 0.10f;

    private static Sprite _backdropSprite;

    private PlayerController _owner;
    private TextMeshPro      _label;
    private SpriteRenderer   _backdrop;
    private Vector3          _offset;

    /// <summary>Resolves the tag colour for a player: the authored one, or the palette.</summary>
    public static Color ResolveColor(Color authored, int playerIndex)
    {
        if (authored.a > 0f) return authored;

        return DefaultPalette[Mathf.Clamp(playerIndex, 0, DefaultPalette.Length - 1)];
    }

    public static PlayerNameTag Create(
        PlayerController owner,
        string text,
        Color color,
        Vector3 offset,
        float size,
        Color backdropColor)
    {
        var go  = new GameObject($"NameTag_{owner.name}", typeof(TextMeshPro));
        var tag = go.AddComponent<PlayerNameTag>();

        tag._owner  = owner;
        tag._offset = offset;
        tag._label  = go.GetComponent<TextMeshPro>();

        tag._label.alignment = TextAlignmentOptions.Center;
        tag._label.fontSize  = size;
        tag._label.fontStyle = FontStyles.Bold;
        tag._label.color     = color;
        tag._label.text      = text;

        // Over the blocks, under the POW countdown.
        tag._label.GetComponent<MeshRenderer>().sortingOrder = 400;

        tag._label.rectTransform.sizeDelta = new Vector2(4f, 1f);

        tag.CreateBackdrop(backdropColor);
        tag.FitBackdrop();

        return tag;
    }

    /// <summary>
    /// Recuadro oscuro detrás del texto para que se lea sobre cualquier fondo.
    /// El sprite es un píxel blanco generado en runtime: no hace falta arte, y
    /// cuando llegue el cartelito de verdad esto se cambia por su sprite.
    /// </summary>
    private void CreateBackdrop(Color backdropColor)
    {
        if (_backdropSprite == null)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            _backdropSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        var go = new GameObject("Backdrop");
        go.transform.SetParent(transform, false);

        _backdrop = go.AddComponent<SpriteRenderer>();
        _backdrop.sprite       = _backdropSprite;
        _backdrop.color        = backdropColor;
        _backdrop.sortingOrder = 399; // justo por detrás del texto
    }

    private void FitBackdrop()
    {
        if (_backdrop == null || _label == null) return;

        // El tamaño real del texto sólo se conoce después de generar la malla.
        _label.ForceMeshUpdate();

        Bounds bounds = _label.textBounds;

        _backdrop.transform.localPosition = bounds.center;
        _backdrop.transform.localScale    = new Vector3(
            bounds.size.x + BackdropPaddingX,
            bounds.size.y + BackdropPaddingY,
            1f);
    }

    /// <summary>Entry point for the future name-customisation screen.</summary>
    public void SetLabel(string text)
    {
        if (_label == null) return;

        _label.text = text;
        FitBackdrop();
    }

    public void SetColor(Color color)
    {
        if (_label != null) _label.color = color;
    }

    private void LateUpdate()
    {
        if (_owner == null)
        {
            Destroy(gameObject);
            return;
        }

        // Hidden while the player is out of the round: a tag floating over a
        // corpse is noise, and the chicken is not on screen anyway.
        bool visible = _owner.isOnGame && _owner.gameObject.activeInHierarchy;

        if (_label.enabled != visible)
        {
            _label.enabled = visible;
            if (_backdrop != null) _backdrop.enabled = visible;
        }

        if (!visible) return;

        transform.position = _owner.transform.position + _offset;
    }
}
