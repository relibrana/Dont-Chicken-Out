using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Performance overlay for diagnosing on site (events, TVs, new hardware).
/// F3 toggles it, in builds too. Shows the real frame rate and the worst
/// frame of the last half second, what the monitor runs at, the window mode,
/// VSync / frame cap and the physics tick.
///
/// How to read it: FPS steady at the monitor's Hz but motion still stutters →
/// cadence problem (mirrored screens, TV processing), not performance. FPS
/// under the Hz, or a "worst frame" far above the average → the machine can't
/// keep up. Born from the event of oct 2026 (see Docs/deuda-tecnica.md).
/// Created on its own at startup; needs no scene object.
/// </summary>
public sealed class DebugOverlay : MonoBehaviour
{
    private const float SampleWindow = 0.5f;

    private bool _visible;

    private int   _frames;
    private float _elapsed;
    private float _worstFrame;

    private float _fps;
    private float _avgMs;
    private float _worstMs;

    private GUIStyle _style;
    private readonly StringBuilder _text = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject(nameof(DebugOverlay));
        go.AddComponent<DebugOverlay>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        if (TogglePressed())
            _visible = !_visible;

        // Unscaled: pausing (timeScale 0) must not freeze or distort the reading.
        float dt = Time.unscaledDeltaTime;
        _frames++;
        _elapsed    += dt;
        _worstFrame  = Mathf.Max(_worstFrame, dt);

        if (_elapsed < SampleWindow) return;

        _fps     = _frames / _elapsed;
        _avgMs   = _elapsed / _frames * 1000f;
        _worstMs = _worstFrame * 1000f;

        _frames     = 0;
        _elapsed    = 0f;
        _worstFrame = 0f;
    }

    /// <summary>
    /// F3, or both sticks clicked together (L3 + R3) on any gamepad: those two
    /// are unbound in the game and almost impossible to press by accident.
    /// </summary>
    private static bool TogglePressed()
    {
        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            return true;

        foreach (Gamepad pad in Gamepad.all)
        {
            var l3 = pad.leftStickButton;
            var r3 = pad.rightStickButton;
            if (l3.isPressed && r3.isPressed && (l3.wasPressedThisFrame || r3.wasPressedThisFrame))
                return true;
        }
        return false;
    }

    private void OnGUI()
    {
        if (!_visible) return;

        _style ??= new GUIStyle(GUI.skin.label) { richText = true };
        _style.fontSize = Mathf.Max(14, Screen.height / 45);

        DisplayInfo display = Screen.mainWindowDisplayInfo;
        Resolution  screen  = Screen.currentResolution;
        int cap = Application.targetFrameRate;

        _text.Clear();
        _text.Append($"<b>{_fps:0} FPS</b>   {_avgMs:0.0} ms   (peor: {_worstMs:0.0} ms)\n");
        _text.Append($"Pantalla: {screen.width}x{screen.height} @ {screen.refreshRateRatio.value:0.##} Hz   {display.name}\n");
        _text.Append($"Ventana: {Screen.width}x{Screen.height}   {Screen.fullScreenMode}\n");
        _text.Append($"VSync: {(QualitySettings.vSyncCount > 0 ? "sí" : "no")}   ");
        _text.Append($"Límite: {(cap > 0 ? cap + " fps" : "ninguno")}   ");
        _text.Append($"Física: {1f / Time.fixedDeltaTime:0} Hz");

        GUIContent content = new(_text.ToString());
        Vector2 size = _style.CalcSize(content);
        var box = new Rect(8f, 8f, size.x + 16f, size.y + 12f);

        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(box.x + 8f, box.y + 6f, size.x, size.y), content, _style);
    }
}
