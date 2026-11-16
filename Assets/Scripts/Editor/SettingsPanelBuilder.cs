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

    // "Pantalla" rows, top to bottom: Monitor, Mode, Resolution, Frequency,
    // VSync, FrameCap. Tighter than Audio's so six rows fit above Back.
    private static readonly float[] DisplayRowY = { 125f, 65f, 5f, -55f, -115f, -175f };
    private const float DisplayRowHeight = 60f;

    private static readonly string[] DisplayRows = { "MonitorRow", "ModeRow", "ResolutionRow", "RefreshRow", "VSyncRow", "FrameCapRow" };

    // The controls hint line under Back; the low-Hz warning takes its place.
    private static readonly Vector2 HintPosition = new(0f, -305f);
    private static readonly Vector2 HintSize     = new(840f, 40f);

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
        Slider monitor  = DisplayRow(displaySection, "Monitor",  "Monitor",    0, font, resources, out TextMeshProUGUI monitorValue);
        Slider mode     = DisplayRow(displaySection, "Mode",     "Modo",       1, font, resources, out TextMeshProUGUI modeValue);
        Slider vsync    = DisplayRow(displaySection, "VSync",    "VSync",      4, font, resources, out TextMeshProUGUI vsyncValue);
        Slider frameCap = DisplayRow(displaySection, "FrameCap", "Límite FPS", 5, font, resources, out TextMeshProUGUI frameCapValue);
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
                                     HintPosition, HintSize, TextAlignmentOptions.Center);
        hint.color = new Color(1f, 1f, 1f, 0.6f);

        // After Back and the hint: the keep/revert prompt must cover them.
        AddResolutionControls(display, font, resources);

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

    /// <summary>
    /// Brings the "Pantalla" tab of an existing scene up to date (oct 2026):
    /// adds whatever is missing of Resolution, Frequency, the low-Hz warning
    /// and the keep/revert prompt, keeping whatever Arte restyled. Works on
    /// panels built before any of it and on ones that already have Resolution.
    /// Only moves that tab's rows (vertical position and height) to make room.
    /// </summary>
    [MenuItem("Tools/Don't Chicken Out/Actualizar pestaña Pantalla (resolución y Hz)")]
    private static void UpdateDisplayTab()
    {
        const string title = "Pestaña Pantalla";

        var display = Object.FindFirstObjectByType<DisplaySettingsPanel>(FindObjectsInactive.Include);
        if (display == null)
        {
            EditorUtility.DisplayDialog(title, "Abre MainMenu.unity: no encuentro la pestaña Pantalla del panel de ajustes.", "Vale");
            return;
        }

        var so = new SerializedObject(display);
        Object Field(string name) => so.FindProperty(name).objectReferenceValue;

        if (Field("refreshSlider") != null && Field("hint") != null)
        {
            EditorUtility.DisplayDialog(title, "La pestaña Pantalla ya tiene Resolución y Frecuencia.", "Vale");
            return;
        }
        if (!(Field("modeSlider") is Slider mode) || !(Field("vsyncSlider") is Slider vsync))
        {
            EditorUtility.DisplayDialog(title, "A la pestaña Pantalla le faltan las filas Modo o VSync: no sé dónde meter Resolución.", "Vale");
            return;
        }

        bool ok = EditorUtility.DisplayDialog(title,
            "Se añaden las filas Resolución y Frecuencia (entre Modo y VSync), el aviso de Hz bajos (en la línea de controles) " +
            "y la ventana de \"¿Mantener?\". Las filas de Pantalla se recolocan en vertical para hacer hueco. " +
            "Se puede deshacer con Ctrl+Z.",
            "Actualizar", "Cancelar");
        if (!ok) return;

        Undo.SetCurrentGroupName("Actualizar pestaña Pantalla");
        int group = Undo.GetCurrentGroup();

        for (int i = 0; i < DisplayRows.Length; i++)
        {
            if (!(display.transform.Find(DisplayRows[i]) is RectTransform row)) continue;
            Undo.RecordObject(row, "Recolocar fila");
            row.anchoredPosition = new Vector2(row.anchoredPosition.x, DisplayRowY[i]);
            row.sizeDelta        = new Vector2(row.sizeDelta.x, DisplayRowHeight);
        }

        // Everything AddResolutionControls may change on objects that already exist.
        Undo.RecordObject(display, "Conectar resolución");
        Undo.RecordObject(mode, "Navegación");
        Undo.RecordObject(vsync, "Navegación");
        foreach (string name in new[] { "resolutionSlider", "modeValue" })
        {
            if (Field(name) != null) Undo.RecordObject(Field(name), "Pestaña Pantalla");
        }
        if (Field("refreshWarning") is TextMeshProUGUI oldWarning) Undo.RecordObject(oldWarning.transform, "Recolocar aviso");

        // "Ventana sin bordes" is longer than the old "Sin bordes".
        if (Field("modeValue") is TextMeshProUGUI modeValue) AutoSize(modeValue);

        foreach (GameObject created in AddResolutionControls(display, FindFont("CalSans-Regular SDF"), DefaultResources()))
            Undo.RegisterCreatedObjectUndo(created, "Actualizar pestaña Pantalla");
        Undo.CollapseUndoOperations(group);

        EditorSceneManager.MarkSceneDirty(display.gameObject.scene);
        Selection.activeGameObject = display.gameObject;
        Debug.Log("Pestaña Pantalla actualizada (Resolución + Frecuencia). Guarda la escena (Ctrl+S).");
    }

    /// <summary>
    /// Resolution and Frequency rows (between Mode and VSync), the low-Hz
    /// warning on the hint line and the keep/revert prompt; creates the ones
    /// the panel lacks and wires all of them. The prompt is the window's last
    /// child so it covers everything, Back included.
    /// Returns the new root objects (for Undo).
    /// </summary>
    private static List<GameObject> AddResolutionControls(DisplaySettingsPanel display, TMP_FontAsset font,
                                                          TMP_DefaultControls.Resources resources)
    {
        var created = new List<GameObject>();
        GameObject section = display.gameObject;
        Transform  window  = section.transform.parent;

        var so = new SerializedObject(display);
        T Field<T>(string name) where T : Object => so.FindProperty(name).objectReferenceValue as T;

        Slider mode  = Field<Slider>("modeSlider");
        Slider vsync = Field<Slider>("vsyncSlider");

        Slider resolution = Field<Slider>("resolutionSlider");
        TextMeshProUGUI resolutionValue = Field<TextMeshProUGUI>("resolutionValue");
        if (resolution == null)
        {
            resolution = DisplayRow(section, "Resolution", "Resolución", 2, font, resources, out resolutionValue);
            created.Add(resolution.transform.parent.gameObject);
        }

        Slider refresh = Field<Slider>("refreshSlider");
        TextMeshProUGUI refreshValue = Field<TextMeshProUGUI>("refreshValue");
        if (refresh == null)
        {
            refresh = DisplayRow(section, "Refresh", "Frecuencia", 3, font, resources, out refreshValue);
            created.Add(refresh.transform.parent.gameObject);
        }

        // Hierarchy in reading order: Mode, Resolution, Frequency, VSync.
        resolution.transform.parent.SetSiblingIndex(mode.transform.parent.GetSiblingIndex() + 1);
        refresh.transform.parent.SetSiblingIndex(resolution.transform.parent.GetSiblingIndex() + 1);

        SetNavigation(resolution, up: mode,       down: refresh);
        SetNavigation(refresh,    up: resolution, down: vsync);
        Navigation nav = mode.navigation;
        nav.selectOnDown = resolution;
        mode.navigation = nav;
        nav = vsync.navigation;
        nav.selectOnUp = refresh;
        vsync.navigation = nav;

        // ── Low-Hz warning: on the hint line, which it hides while shown ──────

        GameObject hint = window.Find("Hint") is Transform h ? h.gameObject : null;
        TextMeshProUGUI warning = Field<TextMeshProUGUI>("refreshWarning");
        if (warning == null)
        {
            warning = Label(section, "RefreshWarning", "", font, 24f, HintPosition, HintSize, TextAlignmentOptions.Center);
            warning.color = Accent;
            AutoSize(warning, 14f, 24f);
            warning.gameObject.SetActive(false);
            created.Add(warning.gameObject);
        }
        else Place(warning.gameObject, HintPosition, HintSize);

        // ── Keep / revert prompt ──────────────────────────────────────────────

        GameObject overlay = Field<GameObject>("confirmOverlay");
        TextMeshProUGUI message = Field<TextMeshProUGUI>("confirmText");
        Button keep   = Field<Button>("keepButton");
        Button revert = Field<Button>("revertButton");
        if (overlay == null)
        {
            overlay = NewRect("ResolutionConfirm", window);
            Stretch(overlay);
            var dimmer = overlay.AddComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.75f); // also blocks clicks on the rows behind

            GameObject box = NewRect("Box", overlay.transform);
            Place(box, Vector2.zero, new Vector2(700f, 300f));
            var boxImage = box.AddComponent<Image>();
            boxImage.sprite = resources.standard;
            boxImage.type   = Image.Type.Sliced;
            boxImage.color  = new Color(0.12f, 0.10f, 0.18f, 1f);

            message = Label(box, "Message", "¿Mantener esta resolución?", font, 38f,
                            new Vector2(0f, 55f), new Vector2(660f, 120f), TextAlignmentOptions.Center);
            keep   = NewButton(box, "KeepButton",   "Mantener", font, resources, new Vector2(-150f, -80f), new Vector2(260f, 70f));
            revert = NewButton(box, "RevertButton", "Revertir", font, resources, new Vector2( 150f, -80f), new Vector2(260f, 70f));
            Emphasize(keep, Accent);
            Emphasize(revert, Accent);
            SetNavigation(keep,   up: null, down: null, left: revert, right: revert);
            SetNavigation(revert, up: null, down: null, left: keep,   right: keep);
            overlay.SetActive(false);
            created.Add(overlay);
        }

        Assign(display, ("resolutionSlider", resolution), ("resolutionValue", resolutionValue),
                        ("refreshSlider", refresh), ("refreshValue", refreshValue),
                        ("refreshWarning", warning), ("hint", hint),
                        ("confirmOverlay", overlay), ("confirmText", message), ("keepButton", keep), ("revertButton", revert));
        return created;
    }

    /// <summary>A "Pantalla" row in its slot (DisplayRowY); its value text shrinks to fit long values.</summary>
    private static Slider DisplayRow(GameObject section, string id, string text, int slot,
                                     TMP_FontAsset font, TMP_DefaultControls.Resources resources, out TextMeshProUGUI value)
    {
        Slider slider = Row(section, id, text, DisplayRowY[slot], 240f, font, resources, out value, DisplayRowHeight);
        slider.wholeNumbers = true;
        AutoSize(value); // "Solo en pantalla completa", "Ventana sin bordes"
        return slider;
    }

    private static void AutoSize(TextMeshProUGUI label, float min = 18f, float max = 30f)
    {
        label.enableAutoSizing = true;
        label.fontSizeMin = min;
        label.fontSizeMax = max;
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
                              TMP_FontAsset font, TMP_DefaultControls.Resources resources, out TextMeshProUGUI value,
                              float height = 70f)
    {
        GameObject row = NewRect(id + "Row", section.transform);
        Place(row, new Vector2(0f, y), new Vector2(800f, height));

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
