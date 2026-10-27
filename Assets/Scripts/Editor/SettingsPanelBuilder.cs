using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static MenuPanelBuilderKit;

/// <summary>
/// One-click builder for the main menu's settings window: tab bar (Audio,
/// Pantalla), one section per tab, shared Back button, focus highlight and a
/// controls hint. Run it with MainMenu.unity open: it replaces the old
/// stand-alone audio/display panels if present (asks first), wires
/// SettingsButton to open the new one and leaves it selected so Arte can
/// restyle it. Undoable with Ctrl+Z.
/// </summary>
public static class SettingsPanelBuilder
{
    private const string PanelName = "SettingsPanel";
    private const string Title     = "Panel de ajustes";

    // Stand-alone panels made by the previous builders (oct 2026).
    private static readonly string[] OldPanels = { "AudioSettingsPanel", "DisplaySettingsPanel" };

    private static readonly Color Accent  = new(1f, 0.78f, 0.2f, 1f);
    private static readonly Color TabIdle = new(0.30f, 0.26f, 0.40f, 1f);

    [MenuItem("Tools/Don't Chicken Out/Crear panel de ajustes en el menú")]
    private static void Build()
    {
        GameObject settingsButton = GameObject.Find("SettingsButton");
        if (settingsButton == null || !settingsButton.TryGetComponent(out Button openButton))
        {
            EditorUtility.DisplayDialog(Title, "Abre MainMenu.unity: no encuentro el SettingsButton.", "Vale");
            return;
        }

        Canvas canvas = settingsButton.GetComponentInParent<Canvas>().rootCanvas;
        if (canvas.transform.Find(PanelName) != null)
        {
            EditorUtility.DisplayDialog(Title, "El panel de ajustes ya existe en esta escena.", "Vale");
            return;
        }

        if (!RemoveOldPanels(canvas.transform)) return;

        TMP_DefaultControls.Resources resources = DefaultResources();
        TMP_FontAsset font = FindFont("CalSans-Regular SDF");

        // ── Window ────────────────────────────────────────────────────────────

        GameObject panel = NewWindowPanel(PanelName, canvas.transform, new Vector2(900f, 680f), resources, out GameObject window);

        // Focus bar first, so it draws behind everything in the window.
        GameObject barGo = NewRect("SelectionBar", window.transform);
        Place(barGo, Vector2.zero, new Vector2(100f, 60f));
        var barImage = barGo.AddComponent<Image>();
        barImage.sprite = resources.standard;
        barImage.type   = Image.Type.Sliced;
        barImage.color  = new Color(Accent.r, Accent.g, Accent.b, 0.3f);
        barImage.raycastTarget = false;

        Label(window, "Title", "Ajustes", font, 56f, new Vector2(0f, 270f), new Vector2(840f, 80f), TextAlignmentOptions.Center);

        Button audioTab   = Tab(window, "AudioTab",   "Audio",    new Vector2(-125f, 190f), font, resources);
        Button displayTab = Tab(window, "DisplayTab", "Pantalla", new Vector2( 125f, 190f), font, resources);

        // ── Audio section ─────────────────────────────────────────────────────

        GameObject audioSection = Section(window, "AudioSection");
        Slider music = Row(audioSection, "Music", "Música",   80f, 300f, font, resources, out TextMeshProUGUI musicValue);
        Slider sfx   = Row(audioSection, "Sfx",   "Efectos",   0f, 300f, font, resources, out TextMeshProUGUI sfxValue);
        SetNavigation(music, up: null,  down: sfx);
        SetNavigation(sfx,   up: music, down: null);

        var audio = audioSection.AddComponent<AudioSettingsPanel>();
        Assign(audio, ("musicSlider", music), ("musicValue", musicValue), ("sfxSlider", sfx), ("sfxValue", sfxValue));

        // ── Display section ───────────────────────────────────────────────────

        GameObject displaySection = Section(window, "DisplaySection");
        Slider monitor  = Row(displaySection, "Monitor",  "Monitor",     80f, 240f, font, resources, out TextMeshProUGUI monitorValue);
        Slider mode     = Row(displaySection, "Mode",     "Modo",         0f, 240f, font, resources, out TextMeshProUGUI modeValue);
        Slider vsync    = Row(displaySection, "VSync",    "VSync",      -80f, 240f, font, resources, out TextMeshProUGUI vsyncValue);
        Slider frameCap = Row(displaySection, "FrameCap", "Límite FPS", -160f, 240f, font, resources, out TextMeshProUGUI frameCapValue);
        foreach (Slider s in new[] { monitor, mode, vsync, frameCap }) s.wholeNumbers = true;
        SetNavigation(monitor,  up: null,    down: mode);
        SetNavigation(mode,     up: monitor, down: vsync);
        SetNavigation(vsync,    up: mode,    down: frameCap);
        SetNavigation(frameCap, up: vsync,   down: null);

        var display = displaySection.AddComponent<DisplaySettingsPanel>();
        Assign(display, ("monitorSlider", monitor), ("monitorValue", monitorValue), ("modeSlider", mode), ("modeValue", modeValue),
                        ("vsyncSlider", vsync), ("vsyncValue", vsyncValue), ("frameCapSlider", frameCap), ("frameCapValue", frameCapValue));
        displaySection.SetActive(false);

        // ── Back + hint ───────────────────────────────────────────────────────

        Button back = NewButton(window, "BackButton", "Volver", font, resources, new Vector2(0f, -245f), new Vector2(260f, 70f));
        Emphasize(back, Accent);

        TextMeshProUGUI hint = Label(window, "Hint", "LB / RB: cambiar pestaña      B / Esc: volver", font, 24f,
                                     new Vector2(0f, -305f), new Vector2(840f, 40f), TextAlignmentOptions.Center);
        hint.color = new Color(1f, 1f, 1f, 0.6f);

        // ── Components ────────────────────────────────────────────────────────

        var highlight = window.AddComponent<SelectionHighlight>();
        Assign(highlight, ("bar", barGo.transform), ("barImage", barImage));

        var component = panel.AddComponent<SettingsPanel>();
        var so = new SerializedObject(component);
        SerializedProperty tabs = so.FindProperty("tabs");
        tabs.arraySize = 2;
        SetTab(tabs.GetArrayElementAtIndex(0), audioTab,   audio);
        SetTab(tabs.GetArrayElementAtIndex(1), displayTab, display);
        so.FindProperty("backButton").objectReferenceValue      = back;
        so.FindProperty("reselectOnClose").objectReferenceValue = settingsButton;
        so.FindProperty("activeTabColor").colorValue            = Accent;
        so.FindProperty("idleTabColor").colorValue              = TabIdle;
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.transform.SetAsLastSibling();
        panel.SetActive(false);
        Undo.RegisterCreatedObjectUndo(panel, "Crear panel de ajustes");

        // SettingsButton: drop links to the old panels (or to deleted objects), open the new one.
        Undo.RecordObject(openButton, "Conectar SettingsButton");
        for (int i = openButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            Object target = openButton.onClick.GetPersistentTarget(i);
            if (target == null || target is SettingsSection || target is SettingsPanel)
                UnityEventTools.RemovePersistentListener(openButton.onClick, i);
        }
        UnityEventTools.AddPersistentListener(openButton.onClick, component.Open);

        EditorSceneManager.MarkSceneDirty(settingsButton.scene);
        Selection.activeGameObject = panel;
        Debug.Log("Panel de ajustes creado (Audio + Pantalla) y conectado al SettingsButton. Guarda la escena (Ctrl+S).");
    }

    private static bool RemoveOldPanels(Transform canvas)
    {
        var found = new List<GameObject>();
        foreach (string name in OldPanels)
        {
            Transform old = canvas.Find(name);
            if (old != null) found.Add(old.gameObject);
        }
        if (found.Count == 0) return true;

        bool ok = EditorUtility.DisplayDialog(Title,
            "Se van a borrar los paneles sueltos (" + string.Join(", ", found.ConvertAll(g => g.name)) + ") " +
            "y sustituir por el panel con pestañas. Si Arte los había retocado, ese estilo se pierde " +
            "(se puede deshacer con Ctrl+Z).",
            "Sustituir", "Cancelar");
        if (!ok) return false;

        foreach (GameObject go in found) Undo.DestroyObjectImmediate(go);
        return true;
    }

    private static GameObject Section(GameObject window, string name)
    {
        GameObject section = NewRect(name, window.transform);
        Stretch(section);
        return section;
    }

    private static Button Tab(GameObject window, string name, string text, Vector2 position,
                              TMP_FontAsset font, TMP_DefaultControls.Resources resources)
    {
        Button tab = NewButton(window, name, text, font, resources, position, new Vector2(220f, 60f));
        // No Emphasize here: the tint multiplies the tab colour (SettingsPanel
        // paints the active one) and would muddy it. The focus bar marks tabs.
        tab.image.color = TabIdle;
        tab.GetComponentInChildren<TextMeshProUGUI>().color = Color.white;
        return tab;
    }

    /// <summary>One option row: label, stepping slider, current value; the row is what gets highlighted.</summary>
    private static Slider Row(GameObject section, string id, string text, float y, float sliderWidth,
                              TMP_FontAsset font, TMP_DefaultControls.Resources resources, out TextMeshProUGUI value)
    {
        GameObject row = NewRect(id + "Row", section.transform);
        Place(row, new Vector2(0f, y), new Vector2(800f, 70f));

        TextMeshProUGUI label = Label(row, "Label", text, font, 38f, new Vector2(-270f, 0f), new Vector2(240f, 60f), TextAlignmentOptions.Left);
        Slider slider = NewSlider(row, "Slider", resources, Vector2.zero, sliderWidth);
        Emphasize(slider, Accent);
        value = Label(row, "Value", "", font, 30f, new Vector2(270f, 0f), new Vector2(240f, 60f), TextAlignmentOptions.Left);

        var marker = row.AddComponent<SettingsRow>();
        Assign(marker, ("label", label));
        return slider;
    }

    private static void SetTab(SerializedProperty element, Button button, SettingsSection section)
    {
        element.FindPropertyRelative("button").objectReferenceValue  = button;
        element.FindPropertyRelative("section").objectReferenceValue = section;
    }

    private static void Assign(Object component, params (string field, Object value)[] refs)
    {
        var so = new SerializedObject(component);
        foreach ((string field, Object value) in refs)
            so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
