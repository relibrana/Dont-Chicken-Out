using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class PlayersManager : MonoBehaviour
{
    [SerializeField] private Transform[]  spawnPoints;
    [SerializeField] private MaterialsSO  materialsSO;
    [SerializeField] private GameObject   playerPrefab;

    [NonSerialized] public int currPlayersInGame;

    private PlayerInputManager _playerInputManager;
    private List<string> _usedKeyboardSchemes = new List<string>();

    private readonly Dictionary<Key, string> keyboardJoinKeys = new Dictionary<Key, string>
    {
        { Key.E,          "Keyboard1" },
        { Key.RightShift, "Keyboard2" },
    };

    private IDisposable _anyButtonSubscription;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _playerInputManager = GetComponent<PlayerInputManager>();
    }

    private void Start()
    {
        // If a primary device was registered in the menu, auto-join it as Player 1
        // before opening up the normal join flow.
        if (SessionData.HasPrimaryDevice)
        {
            AttemptToJoin(SessionData.PrimaryDevice, SessionData.PrimaryScheme);
            SessionData.Clear();
        }
    }

    private void OnEnable()
    {
        _playerInputManager.DisableJoining();

        _anyButtonSubscription = InputSystem.onAnyButtonPress
            .Subscribe(new InputControlObserver(this));
    }

    private void OnDisable()
    {
        _anyButtonSubscription?.Dispose();
        _anyButtonSubscription = null;
    }

    // ── Join validation ───────────────────────────────────────────────────────

    private bool IsPossibleToPair(InputControl control)
    {
        if (GameManager.instance.gameState != GameState.Menu)
            return false;

        if (_playerInputManager.maxPlayerCount > 0
            && currPlayersInGame >= _playerInputManager.maxPlayerCount)
            return false;

        if (control.device is Keyboard)
            return CheckKeyboardJoin(control);

        if (control.device is Gamepad gamepad)
            return CheckGamepadJoin(control, gamepad);

        return false;
    }

    private bool CheckKeyboardJoin(InputControl inputControl)
    {
        if (!(inputControl is KeyControl keyControl))
            return false;

        if (!keyboardJoinKeys.ContainsKey(keyControl.keyCode))
            return false;

        string scheme = keyboardJoinKeys[keyControl.keyCode];
        return !_usedKeyboardSchemes.Contains(scheme);
    }

    private bool CheckGamepadJoin(InputControl inputControl, Gamepad gamepad)
    {
        if (inputControl != gamepad.startButton)
            return false;

        return !PlayerInput.all.Any(p => p.devices.Contains(inputControl.device));
    }

    // ── Button press handling ─────────────────────────────────────────────────

    private void HandleButtonPress(InputControl control)
    {
        if (!IsPossibleToPair(control)) return;

        InputDevice device = control.device;

        if (device is Gamepad)
        {
            AttemptToJoin(device, "Gamepad");
        }
        else if (device is Keyboard)
        {
            string scheme = keyboardJoinKeys[((KeyControl)control).keyCode];
            AttemptToJoin(device, scheme);
        }
    }

    // ── Player instantiation ──────────────────────────────────────────────────

    private void AttemptToJoin(InputDevice device, string schemeName)
    {
        // Guard: avoid joining with an already-used keyboard scheme.
        if (device is Keyboard && _usedKeyboardSchemes.Contains(schemeName))
        {
            Debug.Log($"[PlayersManager] Scheme '{schemeName}' is already in use.");
            return;
        }

        // Guard: avoid joining with a device already paired to another player.
        if (device is Gamepad
            && PlayerInput.all.Any(p => p.devices.Contains(device)))
        {
            Debug.Log($"[PlayersManager] Device '{device.name}' is already paired.");
            return;
        }

        if (device is Keyboard)
            _usedKeyboardSchemes.Add(schemeName);

        PlayerInput newPlayer = PlayerInput.Instantiate(
            prefab:          playerPrefab,
            playerIndex:     -1,
            controlScheme:   schemeName,
            pairWithDevice:  device
        );

        newPlayer.transform.position = spawnPoints[currPlayersInGame].position;

        PlayerController playerController = newPlayer.GetComponent<PlayerController>();
        GameManager.instance.AddPlayer(
            playerController,
            spawnPoints[currPlayersInGame].position,
            materialsSO.playerMaterials[currPlayersInGame]
        );

        currPlayersInGame++;

        newPlayer.SendMessage("OnAssignedScheme", schemeName, SendMessageOptions.DontRequireReceiver);
        Debug.Log($"[PlayersManager] Joined '{device.name}' with scheme '{schemeName}'.");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void FreeKeyboardScheme(string schemeName)
    {
        if (!_usedKeyboardSchemes.Contains(schemeName)) return;

        _usedKeyboardSchemes.Remove(schemeName);
        Debug.Log($"[PlayersManager] Scheme '{schemeName}' freed.");
    }

    // ── Inner observer ────────────────────────────────────────────────────────

    private sealed class InputControlObserver : IObserver<InputControl>
    {
        private readonly PlayersManager _manager;
        public InputControlObserver(PlayersManager manager) => _manager = manager;
        public void OnNext(InputControl value) => _manager.HandleButtonPress(value);
        public void OnError(Exception error)   => Debug.LogError($"[PlayersManager] {error.Message}");
        public void OnCompleted()              { }
    }
}