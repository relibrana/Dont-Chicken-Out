using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Pantalla" tab of the settings panel: Monitor, Mode, VSync and frame cap.
/// Each option is a whole-number Slider (left/right on the gamepad steps
/// through the choices) with a text showing the current one. Changes apply at
/// once through DisplaySettings, which also remembers them. Monitor and Mode
/// only take effect in a build (the editor's Game view is not a real window).
/// </summary>
public sealed class DisplaySettingsPanel : SettingsSection
{
    [SerializeField] private Slider monitorSlider;
    [SerializeField] private TextMeshProUGUI monitorValue;
    [SerializeField] private Slider modeSlider;
    [SerializeField] private TextMeshProUGUI modeValue;
    [SerializeField] private Slider vsyncSlider;
    [SerializeField] private TextMeshProUGUI vsyncValue;
    [SerializeField] private Slider frameCapSlider;
    [SerializeField] private TextMeshProUGUI frameCapValue;

    private static readonly string[] ModeNames = { "Sin bordes", "Exclusiva", "Ventana" };

    // With a single monitor its row cannot change: the tab starts on Mode.
    public override Selectable First => monitorSlider != null && monitorSlider.interactable ? monitorSlider : modeSlider;
    public override Selectable Last  => frameCapSlider;

    private void Awake()
    {
        if (monitorSlider  != null) monitorSlider.onValueChanged.AddListener(v => { DisplaySettings.MoveToDisplay((int)v); Refresh(); });
        if (modeSlider     != null) modeSlider.onValueChanged.AddListener(v => { DisplaySettings.SetMode((int)v); Refresh(); });
        if (vsyncSlider    != null) vsyncSlider.onValueChanged.AddListener(v => { DisplaySettings.SetVSync(v > 0.5f); Refresh(); });
        if (frameCapSlider != null) frameCapSlider.onValueChanged.AddListener(v => { DisplaySettings.SetFrameCap((int)v); Refresh(); });
    }

    public override void OnShown()
    {
        IReadOnlyList<DisplayInfo> displays = DisplaySettings.GetDisplays();
        Setup(monitorSlider,  Mathf.Max(1, displays.Count),     DisplaySettings.CurrentDisplayIndex());
        Setup(modeSlider,     DisplaySettings.Modes.Length,     DisplaySettings.ModeIndex);
        Setup(vsyncSlider,    2,                                DisplaySettings.VSync ? 1 : 0);
        Setup(frameCapSlider, DisplaySettings.FrameCaps.Length, DisplaySettings.FrameCapIndex);

        if (monitorSlider != null)
        {
            monitorSlider.interactable = displays.Count > 1;
            // Skip the dead row when walking up from Mode.
            if (modeSlider != null)
            {
                Navigation nav = modeSlider.navigation;
                nav.selectOnUp = monitorSlider.interactable ? monitorSlider : null;
                modeSlider.navigation = nav;
            }
        }

        Refresh();
    }

    private void Refresh()
    {
        IReadOnlyList<DisplayInfo> displays = DisplaySettings.GetDisplays();
        int monitor = monitorSlider != null ? (int)monitorSlider.value : 0;
        SetText(monitorValue, monitor < displays.Count ? $"{monitor + 1}: {displays[monitor].name}" : "1");

        SetText(modeValue, ModeNames[DisplaySettings.ModeIndex]);
        SetText(vsyncValue, DisplaySettings.VSync ? "Sí" : "No");

        // Unity ignores the frame cap while VSync is on: say so instead of
        // showing a number that does nothing.
        int cap = DisplaySettings.FrameCaps[DisplaySettings.FrameCapIndex];
        SetText(frameCapValue, DisplaySettings.VSync ? "Lo fija el VSync"
                              : cap > 0 ? $"{cap} fps" : "Sin límite");
        if (frameCapSlider != null) frameCapSlider.interactable = !DisplaySettings.VSync;
    }

    private static void Setup(Slider slider, int options, int value)
    {
        if (slider == null) return;
        slider.wholeNumbers = true;
        slider.minValue     = 0;
        slider.maxValue     = Mathf.Max(0, options - 1);
        slider.SetValueWithoutNotify(Mathf.Clamp(value, 0, options - 1));
    }

    private static void SetText(TextMeshProUGUI label, string text)
    {
        if (label != null) label.text = text;
    }
}
