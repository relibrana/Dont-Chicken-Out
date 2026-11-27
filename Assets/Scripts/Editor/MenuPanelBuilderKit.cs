using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared helpers for the one-click menu panel builders (audio, display).
/// Use Unity's own UI control factories (the same as GameObject > UI) so the
/// hierarchies they create are normal and editable by Arte.
/// </summary>
internal static class MenuPanelBuilderKit
{
    public static TMP_DefaultControls.Resources DefaultResources() => new()
    {
        standard   = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
        background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
        knob       = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
    };

    public static GameObject NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    public static void Stretch(GameObject go)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public static void Place(GameObject go, Vector2 position, Vector2 size)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    /// <summary>Full-screen dimmer (blocks clicks on the menu behind) with a centered window.</summary>
    public static GameObject NewWindowPanel(string panelName, Transform canvas, Vector2 windowSize,
                                            TMP_DefaultControls.Resources r, out GameObject window)
    {
        GameObject panel = NewRect(panelName, canvas);
        Stretch(panel);
        var dimmer = panel.AddComponent<UnityEngine.UI.Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.65f);

        window = NewRect("Window", panel.transform);
        Place(window, Vector2.zero, windowSize);
        var windowImage = window.AddComponent<UnityEngine.UI.Image>();
        PlankImage(windowImage, windowSize);
        windowImage.raycastTarget = false;
        return panel;
    }

    // ── Game skin (nov 2026) ──────────────────────────────────────────────────
    // Wooden planks of the main menu (Sprites/NewArt/UIButton_*: left half plain,
    // right half with feathers = focused) and the game font, instead of Unity's
    // grey default sprites. The planks are 9-sliced (borders set in their .meta),
    // so they stretch to any button or window without bending the ends.

    private const string PlankBigPath     = "Assets/Sprites/NewArt/UIButton_Play.png";
    private const string PlankSmallPath   = "Assets/Sprites/NewArt/UIButton_Exit.png";
    private const string GameFontName     = "DontChickenOutFont SDF";
    private const string GameFontTtfPath  = "Assets/Sprites/NewArt/DontChickenOutFont.ttf";
    private const string FallbackFontName = "CalSans-Regular SDF";

    /// <summary>Text on a plank: warm cream, reads on the brown wood.</summary>
    public static readonly Color PlankText = new(1f, 0.95f, 0.82f, 1f);

    /// <summary>Buttons narrower than this use the small plank (its ends are shorter).</summary>
    private const float SmallPlankWidth = 260f;

    /// <summary>Plain plank and the same plank with feathers (focused), big or small.</summary>
    public static void Plank(bool small, out Sprite normal, out Sprite focused)
    {
        normal = focused = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(small ? PlankSmallPath : PlankBigPath))
        {
            if (asset is not Sprite sprite) continue;
            if (sprite.name.EndsWith("_0")) normal  = sprite;
            if (sprite.name.EndsWith("_1")) focused = sprite;
        }
        if (focused == null) focused = normal;
        if (normal == null) Debug.LogWarning($"[MenuPanelBuilderKit] No plank sprites in {(small ? PlankSmallPath : PlankBigPath)}.");
    }

    /// <summary>Wooden board behind a window, badge or hint, sized <paramref name="size"/>.</summary>
    public static void PlankImage(UnityEngine.UI.Image image, Vector2 size, bool? small = null, bool focusedLook = false)
    {
        Plank(small ?? size.x < SmallPlankWidth, out Sprite normal, out Sprite focused);
        image.sprite = focusedLook ? focused : normal;
        image.type   = UnityEngine.UI.Image.Type.Sliced;
        image.color  = Color.white;
        image.pixelsPerUnitMultiplier = BorderScale(image.sprite, size);
    }

    /// <summary>
    /// A button on a plank: plain when idle, with feathers when focused or
    /// pressed (sprite swap, like the main menu). Colour tints set elsewhere
    /// (OptionGroup's chosen colour, a dimmed locked option) still apply on top.
    /// </summary>
    public static void PlankButton(UnityEngine.UI.Button button, Vector2 size)
    {
        bool small = size.x < SmallPlankWidth;
        var image = button.targetGraphic as UnityEngine.UI.Image;
        if (image == null) return;

        PlankImage(image, size, small);
        Plank(small, out Sprite normal, out Sprite focused);

        button.transition  = UnityEngine.UI.Selectable.Transition.SpriteSwap;
        button.spriteState = new UnityEngine.UI.SpriteState
        {
            highlightedSprite = focused,
            selectedSprite    = focused,
            pressedSprite     = focused,
            disabledSprite    = normal,
        };

        foreach (TextMeshProUGUI label in button.GetComponentsInChildren<TextMeshProUGUI>(true))
            label.color = PlankText;
    }

    /// <summary>
    /// Shrinks the plank's ends and edges (Image pixels-per-unit multiplier)
    /// when the box is too small to fit them at full size.
    /// </summary>
    private static float BorderScale(Sprite sprite, Vector2 size)
    {
        if (sprite == null) return 1f;
        Vector4 b = sprite.border; // x left, y bottom, z right, w top (pixels)
        float k = 1f;
        if (b.x + b.z > 0f && size.x > 0f) k = Mathf.Max(k, (b.x + b.z) / (size.x * 0.6f));
        if (b.y + b.w > 0f && size.y > 0f) k = Mathf.Max(k, (b.y + b.w) / (size.y * 0.8f));
        return k;
    }

    /// <summary>
    /// The game font (Arte, nov 2026). It only has ASCII plus ¡ ¿: no á é í ó ú ñ.
    /// So it is made dynamic (pulls ¡ ¿ from its .ttf) and falls back to CalSans
    /// for the missing letters until Arte adds them. Falls back to CalSans
    /// entirely if the asset is missing.
    /// </summary>
    public static TMP_FontAsset GameFont()
    {
        TMP_FontAsset font     = FindFont(GameFontName);
        TMP_FontAsset fallback = FindFont(FallbackFontName);
        if (font == null) return fallback;

        var so = new SerializedObject(font);
        bool changed = false;

        var ttf = AssetDatabase.LoadAssetAtPath<Font>(GameFontTtfPath);
        SerializedProperty source = so.FindProperty("m_SourceFontFile");
        SerializedProperty mode   = so.FindProperty("m_AtlasPopulationMode");
        if (ttf != null && source != null && source.objectReferenceValue == null)
        {
            source.objectReferenceValue = ttf;
            changed = true;
        }
        if (ttf != null && mode != null && mode.intValue != (int)AtlasPopulationMode.Dynamic)
        {
            mode.intValue = (int)AtlasPopulationMode.Dynamic;
            changed = true;
        }
        if (changed) so.ApplyModifiedPropertiesWithoutUndo();

        if (fallback != null && fallback != font)
        {
            font.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
            if (!font.fallbackFontAssetTable.Contains(fallback))
            {
                font.fallbackFontAssetTable.Add(fallback);
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
        }
        return font;
    }

    public static TextMeshProUGUI Label(GameObject parent, string name, string text, TMP_FontAsset font, float size,
                                        Vector2 position, Vector2 box, TextAlignmentOptions alignment)
    {
        GameObject go = TMP_DefaultControls.CreateText(new TMP_DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        Place(go, position, box);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = size;
        tmp.alignment     = alignment;
        tmp.color         = Color.white;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        return tmp;
    }

    public static UnityEngine.UI.Slider NewSlider(GameObject parent, string name, TMP_DefaultControls.Resources r,
                                                  Vector2 position, float width = 380f)
    {
        var uiResources = new UnityEngine.UI.DefaultControls.Resources
        {
            standard = r.standard, background = r.background, knob = r.knob,
        };
        GameObject go = UnityEngine.UI.DefaultControls.CreateSlider(uiResources);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        Place(go, position, new Vector2(width, 30f));

        var slider = go.GetComponent<UnityEngine.UI.Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value    = 1f;
        return slider;
    }

    public static UnityEngine.UI.Button NewButton(GameObject parent, string name, string text, TMP_FontAsset font,
                                                  TMP_DefaultControls.Resources r, Vector2 position, Vector2 size)
    {
        GameObject go = TMP_DefaultControls.CreateButton(r);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        Place(go, position, size);

        TextMeshProUGUI label = go.GetComponentInChildren<TextMeshProUGUI>();
        label.text     = text;
        label.fontSize = 36f;
        if (font != null) label.font = font;

        go.AddComponent<UIButtonSFX>();
        var button = go.GetComponent<UnityEngine.UI.Button>();
        PlankButton(button, size);
        return button;
    }

    public static void SetNavigation(UnityEngine.UI.Selectable s, UnityEngine.UI.Selectable up, UnityEngine.UI.Selectable down,
                                     UnityEngine.UI.Selectable left = null, UnityEngine.UI.Selectable right = null)
    {
        var nav = new UnityEngine.UI.Navigation
        {
            mode          = UnityEngine.UI.Navigation.Mode.Explicit,
            selectOnUp    = up,
            selectOnDown  = down,
            selectOnLeft  = left,
            selectOnRight = right,
        };
        s.navigation = nav;
    }

    /// <summary>Makes the focused/hovered state of a control obvious (Unity's default is a faint grey).</summary>
    public static void Emphasize(UnityEngine.UI.Selectable s, Color accent)
    {
        UnityEngine.UI.ColorBlock colors = s.colors;
        colors.selectedColor    = accent;
        colors.highlightedColor = Color.Lerp(Color.white, accent, 0.5f);
        s.colors = colors;
    }

    public static TMP_FontAsset FindFont(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"\"{name}\" t:TMP_FontAsset"))
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (font != null) return font;
        }
        return null;
    }
}
