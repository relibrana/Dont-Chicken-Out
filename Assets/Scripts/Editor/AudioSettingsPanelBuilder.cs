using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click builder for the main menu's audio settings panel (Music + SFX
/// sliders). Run it with MainMenu.unity open: it creates the panel inactive on
/// top of the menu canvas, wires SettingsButton to open it and leaves it
/// selected so Arte can restyle it. Uses Unity's own UI control factories (the
/// same as GameObject > UI) so the hierarchy is a normal, editable one.
/// </summary>
public static class AudioSettingsPanelBuilder
{
    private const string PanelName = "AudioSettingsPanel";

    [MenuItem("Tools/Don't Chicken Out/Crear panel de audio en el menú")]
    private static void Build()
    {
        GameObject settingsButton = GameObject.Find("SettingsButton");
        if (settingsButton == null || !settingsButton.TryGetComponent(out UnityEngine.UI.Button button))
        {
            EditorUtility.DisplayDialog("Panel de audio", "Abre MainMenu.unity: no encuentro el SettingsButton.", "Vale");
            return;
        }

        Canvas canvas = settingsButton.GetComponentInParent<Canvas>().rootCanvas;
        if (canvas.transform.Find(PanelName) != null)
        {
            EditorUtility.DisplayDialog("Panel de audio", "El panel ya existe en esta escena.", "Vale");
            return;
        }

        var resources = new TMP_DefaultControls.Resources
        {
            standard   = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob       = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        };
        TMP_FontAsset font = FindFont("CalSans-Regular SDF");

        // Full-screen dimmer: blocks clicks on the menu behind while it is open.
        GameObject panel = NewRect(PanelName, canvas.transform);
        Stretch(panel);
        var dimmer = panel.AddComponent<UnityEngine.UI.Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.65f);

        GameObject window = NewRect("Window", panel.transform);
        Place(window, Vector2.zero, new Vector2(760f, 420f));
        var windowImage = window.AddComponent<UnityEngine.UI.Image>();
        windowImage.sprite = resources.standard;
        windowImage.type   = UnityEngine.UI.Image.Type.Sliced;
        windowImage.color  = new Color(0.12f, 0.10f, 0.18f, 0.95f);
        windowImage.raycastTarget = false;

        Label(window, "Title", "Audio", font, 56f, new Vector2(0f, 150f), new Vector2(700f, 80f), TextAlignmentOptions.Center);
        Label(window, "MusicLabel", "Música", font, 40f, new Vector2(-200f, 50f), new Vector2(260f, 60f), TextAlignmentOptions.Left);
        Label(window, "SfxLabel", "Efectos", font, 40f, new Vector2(-200f, -40f), new Vector2(260f, 60f), TextAlignmentOptions.Left);

        UnityEngine.UI.Slider music = NewSlider(window, "MusicSlider", resources, new Vector2(140f, 50f));
        UnityEngine.UI.Slider sfx   = NewSlider(window, "SfxSlider",   resources, new Vector2(140f, -40f));

        GameObject backGo = TMP_DefaultControls.CreateButton(resources);
        backGo.name = "BackButton";
        backGo.transform.SetParent(window.transform, false);
        Place(backGo, new Vector2(0f, -150f), new Vector2(260f, 70f));
        TextMeshProUGUI backText = backGo.GetComponentInChildren<TextMeshProUGUI>();
        backText.text     = "Volver";
        backText.fontSize = 36f;
        if (font != null) backText.font = font;
        backGo.AddComponent<UIButtonSFX>();
        var back = backGo.GetComponent<UnityEngine.UI.Button>();

        // Gamepad: music ↕ sfx ↕ back, nothing leaks to the menu behind.
        SetNavigation(music, up: back, down: sfx);
        SetNavigation(sfx, up: music, down: back);
        SetNavigation(back, up: sfx, down: music);

        var component = panel.AddComponent<AudioSettingsPanel>();
        var so = new SerializedObject(component);
        so.FindProperty("musicSlider").objectReferenceValue     = music;
        so.FindProperty("sfxSlider").objectReferenceValue       = sfx;
        so.FindProperty("backButton").objectReferenceValue      = back;
        so.FindProperty("reselectOnClose").objectReferenceValue = settingsButton;
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.transform.SetAsLastSibling();
        panel.SetActive(false);
        Undo.RegisterCreatedObjectUndo(panel, "Crear panel de audio");

        Undo.RecordObject(button, "Conectar SettingsButton");
        UnityEventTools.AddPersistentListener(button.onClick, component.Open);

        EditorSceneManager.MarkSceneDirty(settingsButton.scene);
        Selection.activeGameObject = panel;
        Debug.Log("Panel de audio creado y conectado al SettingsButton. Guarda la escena (Ctrl+S).");
    }

    private static GameObject NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(GameObject go)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Place(GameObject go, Vector2 position, Vector2 size)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void Label(GameObject parent, string name, string text, TMP_FontAsset font, float size,
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
    }

    private static UnityEngine.UI.Slider NewSlider(GameObject parent, string name, TMP_DefaultControls.Resources r, Vector2 position)
    {
        var uiResources = new UnityEngine.UI.DefaultControls.Resources
        {
            standard = r.standard, background = r.background, knob = r.knob,
        };
        GameObject go = UnityEngine.UI.DefaultControls.CreateSlider(uiResources);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        Place(go, position, new Vector2(380f, 30f));

        var slider = go.GetComponent<UnityEngine.UI.Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value    = 1f;
        return slider;
    }

    private static void SetNavigation(UnityEngine.UI.Selectable s, UnityEngine.UI.Selectable up, UnityEngine.UI.Selectable down)
    {
        var nav = new UnityEngine.UI.Navigation
        {
            mode          = UnityEngine.UI.Navigation.Mode.Explicit,
            selectOnUp    = up,
            selectOnDown  = down,
        };
        s.navigation = nav;
    }

    private static TMP_FontAsset FindFont(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"\"{name}\" t:TMP_FontAsset"))
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (font != null) return font;
        }
        return null;
    }
}
