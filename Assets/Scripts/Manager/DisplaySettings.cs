using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Display options (monitor, fullscreen mode, resolution + refresh rate, VSync,
/// frame cap): applies them and remembers them between sessions. Applied once
/// at startup; the menu's DisplaySettingsPanel changes them live.
///
/// Why it exists (event, oct 2026): with the laptop mirrored to a TV, Windows
/// paces the game to the primary screen and the TV shows an uneven pick of
/// those frames. Sending the game to the TV alone (Monitor) and letting it own
/// that screen's refresh (exclusive fullscreen) avoids the mirror entirely.
/// Second event (oct 2026): a TV left at 30 Hz by Windows (4K over a cable that
/// cannot do 4K at 60) looked laggy. The game now starts in exclusive
/// fullscreen at the best mode the monitor offers (BestResolution: 60 Hz
/// before size), and Resolution + Frequency let the player pick another.
/// Only exclusive mode can change the refresh rate: borderless and windowed
/// always show at the desktop's.
/// </summary>
public static class DisplaySettings
{
    /// <summary>Frame cap options; 0 = no cap. Unity ignores the cap while VSync is on.</summary>
    public static readonly int[] FrameCaps = { 0, 30, 60, 120, 144, 240 };

    public static readonly FullScreenMode[] Modes =
    {
        FullScreenMode.FullScreenWindow,     // borderless: the default, alt-tab friendly
        FullScreenMode.ExclusiveFullScreen,  // the game owns the monitor and its refresh rate (Windows)
        FullScreenMode.Windowed,
    };

    private const string VSyncKey    = "settings.display.vsync";
    private const string FrameCapKey = "settings.display.frameCap";
    private const string ModeKey     = "settings.display.mode";
    private const string MonitorKey  = "settings.display.monitor";
    private const string ResWidthKey  = "settings.display.resolution.width";
    private const string ResHeightKey = "settings.display.resolution.height";
    private const string ResHzNumKey  = "settings.display.resolution.hzNum";
    private const string ResHzDenKey  = "settings.display.resolution.hzDen";

    // Windowed size: 16:9, never larger than this nor than 3/4 of the monitor.
    private const int MaxWindowWidth = 1600;

    // Smaller modes are not worth a slot in the list (the game is 16:9 HD art).
    private const int MinListedHeight = 720;

    /// <summary>
    /// Under this the screen shows fewer frames than the 50 Hz simulation makes:
    /// the 24/25/30 Hz modes TVs fall back to. The panel warns about it.
    /// </summary>
    public const float LowRefreshRate = 48f;

    public static bool VSync         { get; private set; } = true;
    public static int  FrameCapIndex { get; private set; }
    public static int  ModeIndex     { get; private set; }

    public static bool IsExclusive => Modes[ModeIndex] == FullScreenMode.ExclusiveFullScreen;

    /// <summary>Refresh rate the screen is running at now (in exclusive mode, the one the game asked for).</summary>
    public static float CurrentRefreshRate => (float)Screen.currentResolution.refreshRateRatio.value;

    /// <summary>True while a resolution is being tried and not yet kept (KeepResolution / RevertResolution).</summary>
    public static bool ResolutionPending => !SameMode(_resolution, _keptResolution);

    /// <summary>
    /// The mode exclusive fullscreen uses: the player's choice if this monitor
    /// offers it, else the best one (BestResolution). null only if the monitor
    /// lists no modes at all.
    /// </summary>
    public static Resolution? ActiveResolution => FindListed(_resolution) ?? BestResolution();

    // null = never chosen: use the best mode of whatever monitor the game is on.
    private static Resolution? _resolution;
    private static Resolution? _keptResolution;

    private static readonly List<DisplayInfo> _displays    = new();
    private static readonly List<Resolution>  _resolutions = new();
    private static readonly List<Vector2Int>  _sizes       = new();
    private static readonly List<Resolution>  _rates       = new();

    // ── Startup ───────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void LoadAndApply()
    {
        // Without saved values: Quality level VSync, and fullscreen (exclusive)
        // at the best mode, so a TV that Windows left at 30 Hz plays at 60 out
        // of the box when it can.
        VSync         = PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        FrameCapIndex = Mathf.Clamp(PlayerPrefs.GetInt(FrameCapKey, 0), 0, FrameCaps.Length - 1);
        ModeIndex     = Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, IndexOfMode(FullScreenMode.ExclusiveFullScreen)), 0, Modes.Length - 1);
        _resolution   = _keptResolution = LoadResolution();

        ApplyFrameTiming();

        if (Application.isEditor) return; // the Game view is not a real window

        string monitor = PlayerPrefs.GetString(MonitorKey, string.Empty);
        int saved = FindDisplay(monitor);
        if (saved >= 0 && saved != CurrentDisplayIndex()) MoveToDisplay(saved, save: false);
        else                                              ApplyMode();
    }

    // ── Setters (used by the panel) ───────────────────────────────────────────

    public static void SetVSync(bool on)
    {
        VSync = on;
        PlayerPrefs.SetInt(VSyncKey, on ? 1 : 0);
        PlayerPrefs.Save();
        ApplyFrameTiming();
    }

    public static void SetFrameCap(int index)
    {
        FrameCapIndex = Mathf.Clamp(index, 0, FrameCaps.Length - 1);
        PlayerPrefs.SetInt(FrameCapKey, FrameCapIndex);
        PlayerPrefs.Save();
        ApplyFrameTiming();
    }

    public static void SetMode(int index)
    {
        ModeIndex = Mathf.Clamp(index, 0, Modes.Length - 1);
        PlayerPrefs.SetInt(ModeKey, ModeIndex);
        PlayerPrefs.Save();
        if (!Application.isEditor) ApplyMode();
    }

    /// <summary>
    /// Fullscreen modes (size + refresh rate) of the game's monitor, for
    /// exclusive mode. Refreshed on every call: the list changes with the monitor.
    /// </summary>
    public static IReadOnlyList<Resolution> GetResolutions()
    {
        _resolutions.Clear();
        foreach (Resolution r in Screen.resolutions)
        {
            if (r.height >= MinListedHeight) _resolutions.Add(r);
        }
        // A screen that only offers small modes still needs a list.
        if (_resolutions.Count == 0) _resolutions.AddRange(Screen.resolutions);
        return _resolutions;
    }

    /// <summary>The sizes in GetResolutions(), each once, smallest first (the Resolution row).</summary>
    public static IReadOnlyList<Vector2Int> GetSizes()
    {
        _sizes.Clear();
        foreach (Resolution r in GetResolutions())
        {
            var size = new Vector2Int(r.width, r.height);
            if (!_sizes.Contains(size)) _sizes.Add(size);
        }
        _sizes.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y));
        return _sizes;
    }

    /// <summary>The modes of one size, lowest refresh rate first (the Frequency row).</summary>
    public static IReadOnlyList<Resolution> GetRefreshRates(Vector2Int size)
    {
        _rates.Clear();
        foreach (Resolution r in GetResolutions())
        {
            if (r.width == size.x && r.height == size.y) _rates.Add(r);
        }
        _rates.Sort((a, b) => a.refreshRateRatio.value.CompareTo(b.refreshRateRatio.value));
        return _rates;
    }

    /// <summary>
    /// The default mode: smooth first (a 1080p mode at 60 Hz beats 4K at
    /// 30 Hz: the 30 Hz TV case), then the monitor's own shape (TVs list
    /// 4096x2160 next to 3840x2160), then the most pixels, then the most Hz.
    /// </summary>
    public static Resolution? BestResolution()
    {
        DisplayInfo display = Screen.mainWindowDisplayInfo;
        float aspect = display.height > 0 ? (float)display.width / display.height : 16f / 9f;

        Resolution? best = null;
        foreach (Resolution r in GetResolutions())
        {
            if (best == null || Better(r, best.Value, aspect)) best = r;
        }
        return best;
    }

    /// <summary>
    /// Tries a mode. Not saved until KeepResolution: a TV that cannot show it
    /// goes black, and the panel reverts on a timer.
    /// </summary>
    public static void PreviewResolution(Resolution mode)
    {
        _resolution = mode;
        if (!Application.isEditor && IsExclusive) ApplyMode();
    }

    public static void KeepResolution()
    {
        _keptResolution = _resolution;
        if (_resolution is Resolution r)
        {
            PlayerPrefs.SetInt(ResWidthKey,  r.width);
            PlayerPrefs.SetInt(ResHeightKey, r.height);
            PlayerPrefs.SetInt(ResHzNumKey,  (int)r.refreshRateRatio.numerator);
            PlayerPrefs.SetInt(ResHzDenKey,  (int)r.refreshRateRatio.denominator);
        }
        else
        {
            PlayerPrefs.DeleteKey(ResWidthKey);
            PlayerPrefs.DeleteKey(ResHeightKey);
            PlayerPrefs.DeleteKey(ResHzNumKey);
            PlayerPrefs.DeleteKey(ResHzDenKey);
        }
        PlayerPrefs.Save();
    }

    public static void RevertResolution()
    {
        if (!ResolutionPending) return;
        _resolution = _keptResolution;
        if (!Application.isEditor && IsExclusive) ApplyMode();
    }

    /// <summary>Monitors as Windows lays them out. Refreshed on every call (TVs get plugged in).</summary>
    public static IReadOnlyList<DisplayInfo> GetDisplays()
    {
        _displays.Clear();
        Screen.GetDisplayLayout(_displays);
        return _displays;
    }

    public static int CurrentDisplayIndex()
    {
        DisplayInfo current = Screen.mainWindowDisplayInfo;
        IReadOnlyList<DisplayInfo> displays = GetDisplays();
        for (int i = 0; i < displays.Count; i++)
        {
            if (displays[i].Equals(current)) return i;
        }
        return 0;
    }

    /// <summary>Sends the game window to another monitor, then re-applies the mode there.</summary>
    public static void MoveToDisplay(int index, bool save = true)
    {
        IReadOnlyList<DisplayInfo> displays = GetDisplays();
        if (index < 0 || index >= displays.Count) return;

        DisplayInfo target = displays[index];
        if (save)
        {
            PlayerPrefs.SetString(MonitorKey, target.name);
            PlayerPrefs.Save();
        }

        if (Application.isEditor) return;

        Vector2Int size = WindowedSize(target);
        Vector2Int position = Modes[ModeIndex] == FullScreenMode.Windowed
            ? new Vector2Int((target.width - size.x) / 2, (target.height - size.y) / 2)
            : Vector2Int.zero; // ignored for fullscreen windows

        AsyncOperation move = Screen.MoveMainWindowTo(target, position);
        move.completed += _ => ApplyMode();
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    private static void ApplyFrameTiming()
    {
        QualitySettings.vSyncCount  = VSync ? 1 : 0;
        int cap = FrameCaps[FrameCapIndex];
        Application.targetFrameRate = cap > 0 ? cap : -1;
    }

    private static void ApplyMode()
    {
        FullScreenMode mode    = Modes[ModeIndex];
        DisplayInfo    display = Screen.mainWindowDisplayInfo;

        if (mode == FullScreenMode.Windowed)
        {
            Vector2Int size = WindowedSize(display);
            Screen.SetResolution(size.x, size.y, mode);
            return;
        }

        // Exclusive mode drives the display, so it asks for a full mode (size +
        // refresh rate). Borderless ignores the refresh rate: the desktop's is used.
        if (mode == FullScreenMode.ExclusiveFullScreen && ActiveResolution is Resolution chosen)
        {
            Screen.SetResolution(chosen.width, chosen.height, mode, chosen.refreshRateRatio);
            return;
        }
        Screen.SetResolution(display.width, display.height, mode, display.refreshRate);
    }

    private static bool Better(Resolution a, Resolution b, float aspect)
    {
        bool aSmooth = a.refreshRateRatio.value >= LowRefreshRate;
        bool bSmooth = b.refreshRateRatio.value >= LowRefreshRate;
        if (aSmooth != bSmooth) return aSmooth;

        bool aShape = Mathf.Abs((float)a.width / a.height - aspect) < 0.02f;
        bool bShape = Mathf.Abs((float)b.width / b.height - aspect) < 0.02f;
        if (aShape != bShape) return aShape;

        long aPixels = (long)a.width * a.height;
        long bPixels = (long)b.width * b.height;
        if (aPixels != bPixels) return aPixels > bPixels;

        return a.refreshRateRatio.value > b.refreshRateRatio.value;
    }

    private static Resolution? FindListed(Resolution? wanted)
    {
        if (wanted == null) return null;
        foreach (Resolution r in GetResolutions())
        {
            if (SameMode(r, wanted)) return r;
        }
        return null; // saved for another monitor
    }

    public static bool SameMode(Resolution? a, Resolution? b)
    {
        if (a == null || b == null) return a == null && b == null;
        Resolution x = a.Value, y = b.Value;
        return x.width == y.width && x.height == y.height
            && System.Math.Abs(x.refreshRateRatio.value - y.refreshRateRatio.value) < 0.01;
    }

    private static Resolution? LoadResolution()
    {
        int width = PlayerPrefs.GetInt(ResWidthKey, 0);
        if (width <= 0) return null;

        return new Resolution
        {
            width  = width,
            height = PlayerPrefs.GetInt(ResHeightKey, 0),
            refreshRateRatio = new RefreshRate
            {
                numerator   = (uint)PlayerPrefs.GetInt(ResHzNumKey, 60),
                denominator = (uint)Mathf.Max(1, PlayerPrefs.GetInt(ResHzDenKey, 1)),
            },
        };
    }

    private static Vector2Int WindowedSize(DisplayInfo display)
    {
        int width = Mathf.Min(MaxWindowWidth, display.width * 3 / 4);
        return new Vector2Int(width, width * 9 / 16);
    }

    private static int FindDisplay(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;

        IReadOnlyList<DisplayInfo> displays = GetDisplays();
        for (int i = 0; i < displays.Count; i++)
        {
            if (displays[i].name == name) return i;
        }
        return -1;
    }

    private static int IndexOfMode(FullScreenMode mode)
    {
        int index = System.Array.IndexOf(Modes, mode);
        return index >= 0 ? index : 0;
    }
}
