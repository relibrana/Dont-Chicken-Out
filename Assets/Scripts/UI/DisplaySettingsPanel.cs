using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// "Pantalla" tab of the settings panel: Monitor, Mode, Resolution, Frequency,
/// VSync and frame cap. Each option is a whole-number Slider (left/right on the
/// gamepad steps through the choices) with a text showing the current one.
/// Changes apply at once through DisplaySettings, which also remembers them.
/// Monitor and Mode only take effect in a build (the editor's Game view is not
/// a real window).
///
/// Resolution and Frequency are different: stepping through them only picks a
/// mode (Frequency lists the rates the picked size offers); A / Enter (or a
/// short pause) tries it, and a "keep it?" prompt reverts on its own after a
/// few seconds, like Windows does, because a TV that cannot show the mode goes
/// black. They only work in fullscreen (exclusive).
/// A warning takes the hint line when the screen runs below ~50 Hz.
/// </summary>
public sealed class DisplaySettingsPanel : SettingsSection
{
    [SerializeField] private Slider monitorSlider;
    [SerializeField] private TextMeshProUGUI monitorValue;
    [SerializeField] private Slider modeSlider;
    [SerializeField] private TextMeshProUGUI modeValue;
    [SerializeField] private Slider resolutionSlider;
    [SerializeField] private TextMeshProUGUI resolutionValue;
    [SerializeField] private Slider refreshSlider;
    [SerializeField] private TextMeshProUGUI refreshValue;
    [SerializeField] private Slider vsyncSlider;
    [SerializeField] private TextMeshProUGUI vsyncValue;
    [SerializeField] private Slider frameCapSlider;
    [SerializeField] private TextMeshProUGUI frameCapValue;

    [Header("Aviso y confirmación")]
    [SerializeField] private TextMeshProUGUI refreshWarning;
    [SerializeField, Tooltip("La línea de controles de la ventana: el aviso ocupa su sitio mientras se ve.")]
    private GameObject hint;
    [SerializeField] private GameObject confirmOverlay;
    [SerializeField] private TextMeshProUGUI confirmText;
    [SerializeField] private Button keepButton;
    [SerializeField] private Button revertButton;

    [SerializeField, Min(3f), Tooltip("Segundos antes de volver a la resolución anterior si nadie confirma.")]
    private float confirmSeconds = 10f;

    [SerializeField, Min(0.2f), Tooltip("Pausa en Resolución / Frecuencia tras la que se prueba el modo elegido.")]
    private float applyDelay = 1.5f;

    // Player-facing names: "Pantalla completa" is the exclusive mode, the only
    // one that can change the refresh rate.
    private static readonly string[] ModeNames = { "Ventana sin bordes", "Pantalla completa", "Ventana" };

    private Resolution? _pick;     // picked on the sliders, not tried yet
    private float _applyAt;
    private float _countdown;
    private int   _shownSeconds;

    // The A press that tried the mode must not also hit the prompt's button.
    private int _confirmOpenedFrame = -1;

    // With a single monitor its row cannot change: the tab starts on Mode.
    public override Selectable First => monitorSlider != null && monitorSlider.interactable ? monitorSlider : modeSlider;
    public override Selectable Last  => frameCapSlider;

    public override bool IsModal => confirmOverlay != null && confirmOverlay.activeSelf;

    private void Awake()
    {
        if (monitorSlider    != null) monitorSlider.onValueChanged.AddListener(v => { DisplaySettings.MoveToDisplay((int)v); Refresh(); });
        if (modeSlider       != null) modeSlider.onValueChanged.AddListener(v => { DisplaySettings.SetMode((int)v); Refresh(); });
        if (resolutionSlider != null) resolutionSlider.onValueChanged.AddListener(v => PickSize((int)v));
        if (refreshSlider    != null) refreshSlider.onValueChanged.AddListener(v => PickRate((int)v));
        if (vsyncSlider      != null) vsyncSlider.onValueChanged.AddListener(v => { DisplaySettings.SetVSync(v > 0.5f); Refresh(); });
        if (frameCapSlider   != null) frameCapSlider.onValueChanged.AddListener(v => { DisplaySettings.SetFrameCap((int)v); Refresh(); });

        if (keepButton   != null) keepButton.onClick.AddListener(()   => { if (Time.frameCount != _confirmOpenedFrame) Keep(); });
        if (revertButton != null) revertButton.onClick.AddListener(() => { if (Time.frameCount != _confirmOpenedFrame) Revert(); });
        if (confirmOverlay != null) confirmOverlay.SetActive(false);
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

        _pick = null;
        Refresh();
    }

    public override void OnCancel() => Revert();

    private void OnDisable()
    {
        // Never leave a mode on trial behind a closed tab.
        _pick = null;
        if (IsModal) Revert();
        if (hint != null) hint.SetActive(true); // the other tabs need it
    }

    private void Update()
    {
        if (IsModal)
        {
            _countdown -= Time.unscaledDeltaTime;
            if (_countdown <= 0f) Revert();
            else ShowCountdown();
            return;
        }

        if (_pick is not Resolution pick) return;

        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool onRows = (resolutionSlider != null && selected == resolutionSlider.gameObject)
                   || (refreshSlider    != null && selected == refreshSlider.gameObject);
        if ((SubmitPressed() && onRows) || Time.unscaledTime >= _applyAt) Try(pick);
    }

    // ── Resolution + frequency ────────────────────────────────────────────────

    /// <summary>The mode the two rows show: the one being picked, else the one in use.</summary>
    private Resolution? Shown => _pick ?? DisplaySettings.ActiveResolution;

    private void PickSize(int index)
    {
        IReadOnlyList<Vector2Int> sizes = DisplaySettings.GetSizes();
        if (index < 0 || index >= sizes.Count) return;

        // Keep the same frequency if the new size has it, else take its highest.
        double hz = Shown is Resolution shown ? shown.refreshRateRatio.value : 0d;
        IReadOnlyList<Resolution> rates = DisplaySettings.GetRefreshRates(sizes[index]);
        Resolution pick = rates[rates.Count - 1];
        foreach (Resolution r in rates)
        {
            if (System.Math.Abs(r.refreshRateRatio.value - hz) < 0.01) pick = r;
        }
        Pick(pick);
    }

    private void PickRate(int index)
    {
        if (Shown is not Resolution shown) return;
        IReadOnlyList<Resolution> rates = DisplaySettings.GetRefreshRates(new Vector2Int(shown.width, shown.height));
        if (index >= 0 && index < rates.Count) Pick(rates[index]);
    }

    private void Pick(Resolution mode)
    {
        _pick    = mode;
        _applyAt = Time.unscaledTime + applyDelay;
        Refresh();
    }

    private void Try(Resolution mode)
    {
        _pick = null;
        if (DisplaySettings.SameMode(mode, DisplaySettings.ActiveResolution)) { Refresh(); return; }

        DisplaySettings.PreviewResolution(mode);
        if (confirmOverlay == null) { Keep(); return; }

        _countdown    = confirmSeconds;
        _shownSeconds = -1;
        ShowCountdown();
        _confirmOpenedFrame = Time.frameCount;
        confirmOverlay.SetActive(true);
        // Revert has the focus: someone mashing A on a black screen goes back.
        Select(revertButton != null ? revertButton.gameObject : null);
    }

    private void Keep()
    {
        DisplaySettings.KeepResolution();
        CloseConfirm();
    }

    private void Revert()
    {
        DisplaySettings.RevertResolution();
        CloseConfirm();
    }

    private void CloseConfirm()
    {
        if (confirmOverlay != null) confirmOverlay.SetActive(false);
        Refresh();
        if (isActiveAndEnabled) Select(resolutionSlider != null ? resolutionSlider.gameObject : null);
    }

    private void ShowCountdown()
    {
        int seconds = Mathf.CeilToInt(_countdown);
        if (seconds == _shownSeconds) return;
        _shownSeconds = seconds;
        SetText(confirmText, $"¿Mantener esta resolución?\nSe revierte en {seconds} s");
    }

    // ── View ──────────────────────────────────────────────────────────────────

    private void Refresh()
    {
        IReadOnlyList<DisplayInfo> displays = DisplaySettings.GetDisplays();
        int monitor = monitorSlider != null ? (int)monitorSlider.value : 0;
        SetText(monitorValue, monitor < displays.Count ? $"{monitor + 1}: {displays[monitor].name}" : "1");

        SetText(modeValue, ModeNames[DisplaySettings.ModeIndex]);
        RefreshResolution();
        SetText(vsyncValue, DisplaySettings.VSync ? "Sí" : "No");

        // Unity ignores the frame cap while VSync is on: say so instead of
        // showing a number that does nothing.
        int cap = DisplaySettings.FrameCaps[DisplaySettings.FrameCapIndex];
        SetText(frameCapValue, DisplaySettings.VSync ? "Lo fija el VSync"
                              : cap > 0 ? $"{cap} fps" : "Sin límite");
        if (frameCapSlider != null) frameCapSlider.interactable = !DisplaySettings.VSync;

        RefreshWarning();
    }

    private void RefreshResolution()
    {
        bool exclusive = DisplaySettings.IsExclusive;
        Resolution? shown = Shown;

        // Both lists follow the monitor, so they are rebuilt every time.
        IReadOnlyList<Vector2Int> sizes = DisplaySettings.GetSizes();
        int sizeIndex = shown is Resolution s ? IndexOf(sizes, new Vector2Int(s.width, s.height)) : -1;
        Setup(resolutionSlider, sizes.Count, sizeIndex);

        int rateCount = 0, rateIndex = -1;
        if (shown is Resolution m)
        {
            IReadOnlyList<Resolution> rates = DisplaySettings.GetRefreshRates(new Vector2Int(m.width, m.height));
            rateCount = rates.Count;
            for (int i = 0; i < rates.Count; i++)
            {
                if (DisplaySettings.SameMode(rates[i], m)) rateIndex = i;
            }
        }
        Setup(refreshSlider, rateCount, rateIndex);

        if (resolutionSlider != null) resolutionSlider.interactable = exclusive && sizes.Count > 0;
        if (refreshSlider    != null) refreshSlider.interactable    = exclusive && rateCount > 0;

        if (!exclusive || shown is not Resolution mode)
        {
            // Outside fullscreen Windows owns the screen: show what it runs at.
            SetText(resolutionValue, "Solo en pantalla completa");
            SetText(refreshValue,    $"{DisplaySettings.CurrentRefreshRate:0.##} Hz (Windows)");
            return;
        }
        SetText(resolutionValue, $"{mode.width}x{mode.height}");
        SetText(refreshValue,    $"{mode.refreshRateRatio.value:0.##} Hz");
    }

    private void RefreshWarning()
    {
        if (refreshWarning == null) return;

        string text = string.Empty;
        float hz = DisplaySettings.CurrentRefreshRate;

        if (_pick != null)
            text = "A / Enter: probar este modo";
        else if (hz > 0f && hz < DisplaySettings.LowRefreshRate)
        {
            // Point at the fix that can work: the best mode here, or Windows / the
            // TV when this screen (or its cable) offers nothing smoother.
            Resolution? best = DisplaySettings.BestResolution();
            if (best is not Resolution b || b.refreshRateRatio.value < DisplaySettings.LowRefreshRate)
                text = $"Tu pantalla va a {hz:0} Hz: súbela a 60 Hz en Windows o en la TV.";
            else if (DisplaySettings.IsExclusive)
                text = $"Tu pantalla va a {hz:0} Hz: prueba {b.width}x{b.height} a {b.refreshRateRatio.value:0} Hz.";
            else
                text = $"Tu pantalla va a {hz:0} Hz: pon Modo en Pantalla completa para jugar a {b.refreshRateRatio.value:0} Hz.";
        }

        refreshWarning.text = text;
        refreshWarning.gameObject.SetActive(text.Length > 0);
        if (hint != null) hint.SetActive(text.Length == 0);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int IndexOf(IReadOnlyList<Vector2Int> sizes, Vector2Int size)
    {
        for (int i = 0; i < sizes.Count; i++)
        {
            if (sizes[i] == size) return i;
        }
        return -1;
    }

    private static void Setup(Slider slider, int options, int value)
    {
        if (slider == null) return;
        slider.wholeNumbers = true;
        slider.minValue     = 0;
        slider.maxValue     = Mathf.Max(0, options - 1);
        slider.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, options - 1)));
    }

    private static void SetText(TextMeshProUGUI label, string text)
    {
        if (label != null) label.text = text;
    }

    private static void Select(GameObject target)
    {
        if (EventSystem.current == null || target == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
    }

    private static bool SubmitPressed() =>
        EventSystem.current != null
        && EventSystem.current.currentInputModule is InputSystemUIInputModule module
        && module.submit != null && module.submit.action != null
        && module.submit.action.WasPressedThisFrame();
}
