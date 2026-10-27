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
        windowImage.sprite = r.standard;
        windowImage.type   = UnityEngine.UI.Image.Type.Sliced;
        windowImage.color  = new Color(0.12f, 0.10f, 0.18f, 0.95f);
        windowImage.raycastTarget = false;
        return panel;
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
        return go.GetComponent<UnityEngine.UI.Button>();
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
