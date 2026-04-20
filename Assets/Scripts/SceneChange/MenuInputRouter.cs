using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Listens for the first input on any device in the main menu scene and
/// records it in SessionData so PlayersManager can auto-join that device
/// as Player 1 when the game scene loads.
///
/// Also assigns the MenuInputActions asset to the InputSystemUIInputModule
/// so the EventSystem responds to all devices from frame 1, before any
/// PlayerInput exists.
///
/// Place this on the same GameObject as the EventSystem.
/// </summary>
[DisallowMultipleComponent]
public sealed class MenuInputRouter : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Tooltip("The InputSystemUIInputModule on the EventSystem.")]
    [SerializeField] private InputSystemUIInputModule uiInputModule;

    [Tooltip("InputActionAsset used exclusively for menu navigation. " +
             "Must contain a 'UI' action map with Navigate, Submit and Cancel.")]
    [SerializeField] private InputActionAsset menuActions;

    // ── Keyboard scheme key sets ──────────────────────────────────────────────

    // WASD-side → Keyboard1
    private static readonly Key[] Keyboard1Keys =
    {
        Key.W, Key.A, Key.S, Key.D, Key.F, Key.Space,
    };

    // Arrow-side → Keyboard2
    private static readonly Key[] Keyboard2Keys =
    {
        Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.RightShift,
    };

    // ── Private state ─────────────────────────────────────────────────────────

    private IDisposable _subscription;
    private bool        _routed;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Assign the menu-specific action asset to the UI module immediately
        // so any device can navigate the UI from the very first frame.
        if (uiInputModule != null && menuActions != null)
        {
            menuActions.Enable();
            uiInputModule.actionsAsset = menuActions;
        }
    }

    private void OnEnable()
    {
        _routed = false;
        _subscription = InputSystem.onAnyButtonPress.Subscribe(new InputObserver(this));
    }

    private void OnDisable()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    // ── Routing logic ─────────────────────────────────────────────────────────

    private void HandlePress(InputControl control)
    {
        if (_routed) return;

        string scheme = ResolveScheme(control);
        if (scheme == null) return;

        _routed = true;

        SessionData.PrimaryDevice = control.device;
        SessionData.PrimaryScheme = scheme;

        // No longer need to listen.
        _subscription?.Dispose();
        _subscription = null;

        Debug.Log($"[MenuInputRouter] Primary device set → '{control.device.name}' | scheme: '{scheme}'");
    }

    private static string ResolveScheme(InputControl control)
    {
        if (control.device is Gamepad)
            return "Gamepad";

        if (control.device is Keyboard && control is KeyControl keyControl)
        {
            Key key = keyControl.keyCode;

            foreach (Key k in Keyboard1Keys)
                if (k == key) return "Keyboard1";

            foreach (Key k in Keyboard2Keys)
                if (k == key) return "Keyboard2";
        }

        return null;
    }

    // ── Inner observer ────────────────────────────────────────────────────────

    private sealed class InputObserver : IObserver<InputControl>
    {
        private readonly MenuInputRouter _router;
        public InputObserver(MenuInputRouter router) => _router = router;
        public void OnNext(InputControl value) => _router.HandlePress(value);
        public void OnError(Exception error)   => Debug.LogError($"[MenuInputRouter] {error.Message}");
        public void OnCompleted()              { }
    }
}