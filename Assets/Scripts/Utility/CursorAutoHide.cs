using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hides the mouse cursor in fullscreen (playtest, oct 2026). It comes back
/// while the mouse moves, so the menus can still be used with it, and hides
/// again after a short idle. Windowed mode keeps the normal cursor.
///
/// Installs itself once per session: no scene setup needed.
/// </summary>
public sealed class CursorAutoHide : MonoBehaviour
{
    private const float IdleSeconds = 2f;

    private float _lastMoveTime = float.NegativeInfinity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Application.isEditor) return; // the Game view is not a real window

        var host = new GameObject(nameof(CursorAutoHide));
        DontDestroyOnLoad(host);
        host.AddComponent<CursorAutoHide>();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 0f)
            _lastMoveTime = Time.unscaledTime;

        bool fullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
        bool visible    = !fullscreen || Time.unscaledTime - _lastMoveTime < IdleSeconds;

        if (Cursor.visible != visible)
            Cursor.visible = visible;
    }
}
