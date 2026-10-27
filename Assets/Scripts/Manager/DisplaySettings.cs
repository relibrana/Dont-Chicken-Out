using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Display options (monitor, fullscreen mode, VSync, frame cap): applies them
/// and remembers them between sessions. Applied once at startup; the menu's
/// DisplaySettingsPanel changes them live.
///
/// Why it exists (event, oct 2026): with the laptop mirrored to a TV, Windows
/// paces the game to the primary screen and the TV shows an uneven pick of
/// those frames. Sending the game to the TV alone (Monitor) and letting it own
/// that screen's refresh (exclusive fullscreen) avoids the mirror entirely.
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

    // Windowed size: 16:9, never larger than this nor than 3/4 of the monitor.
    private const int MaxWindowWidth = 1600;

    public static bool VSync         { get; private set; } = true;
    public static int  FrameCapIndex { get; private set; }
    public static int  ModeIndex     { get; private set; }

    private static readonly List<DisplayInfo> _displays = new();

    // ── Startup ───────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void LoadAndApply()
    {
        // Without saved values, keep what the project ships with (Quality
        // level VSync, Player Settings fullscreen mode).
        VSync         = PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        FrameCapIndex = Mathf.Clamp(PlayerPrefs.GetInt(FrameCapKey, 0), 0, FrameCaps.Length - 1);
        ModeIndex     = Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, IndexOfMode(Screen.fullScreenMode)), 0, Modes.Length - 1);

        ApplyFrameTiming();

        if (Application.isEditor) return; // the Game view is not a real window

        string monitor = PlayerPrefs.GetString(MonitorKey, string.Empty);
        int saved = FindDisplay(monitor);
        if (saved >= 0 && saved != CurrentDisplayIndex()) MoveToDisplay(saved, save: false);
        else if (PlayerPrefs.HasKey(ModeKey))             ApplyMode();
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

        // The monitor's own resolution and refresh rate: in exclusive mode the
        // game drives the display, so it must ask for what the screen is set to.
        Screen.SetResolution(display.width, display.height, mode, display.refreshRate);
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
