using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

public class PlayersManager : MonoBehaviour
{
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private GameObject playerPrefab;
    [NonSerialized] public int currPlayersInGame;
    private PlayerInputManager playerInputManager;

    private List<string> usedKeyboardSchemes = new List<string>();

    private readonly Dictionary<Key, string> keyboardJoinKeys = new Dictionary<Key, string>()
    {
        { Key.E, "Keyboard1" },
        { Key.RightShift, "Keyboard2" }
    };

    private IDisposable anyButtonPressSubscription;

    private void Awake()
    {
        playerInputManager = GetComponent<PlayerInputManager>();
    }

    private void OnEnable()
    {
        playerInputManager.DisableJoining();

        var observer = new InputControlObserver(this);
        anyButtonPressSubscription = InputSystem.onAnyButtonPress.Subscribe(observer);
    }

    private void OnDisable()
    {

        anyButtonPressSubscription?.Dispose();
    }

    private bool IsPossibleToPair(InputControl control)
    {
        if (GameManager.instance.gameState != GameState.Menu)
            return false;

        if (playerInputManager.maxPlayerCount > 0 && currPlayersInGame >= playerInputManager.maxPlayerCount)
        {
            //!LOGIC TO SHOW MAX PLAYERS REACHED
            return false;
        }

        if (control.device is Keyboard)
        {
            return CheckKeyboardJoin(control);
        }
        else if (control.device is Gamepad gamepad)
        {
            return CheckGamepadJoin(control, gamepad);
        }

        return false;

        bool CheckKeyboardJoin(InputControl inputControl)
        {
            if (!(inputControl is KeyControl keyControl))
                return false;
            
            if (!keyboardJoinKeys.ContainsKey(keyControl.keyCode))
                return false;

            string scheme = keyboardJoinKeys[keyControl.keyCode];

            if (usedKeyboardSchemes.Contains(scheme))
                return false;

            return true;
        }

        bool CheckGamepadJoin(InputControl inputControl, Gamepad gamepad)
        {
            if (inputControl != gamepad.startButton)
                return false;

            if (PlayerInput.all.Any(player => player.devices.Contains(inputControl.device)))
                return false;
            
            return true;
        }
    }

    private void HandleButtonPress(InputControl control)
    {
        if (!IsPossibleToPair(control))
            return;
        

        InputDevice device = control.device;
        string schemeName;

        if (device is Gamepad)
        {
            schemeName = "Gamepad";
            AttemptToJoin(device, schemeName);
        }

        else if (device is Keyboard)
        {
            string keyScheme = keyboardJoinKeys[((KeyControl)control).keyCode];
            schemeName = keyScheme;
            AttemptToJoin(device, schemeName);
        }
    }

    private void AttemptToJoin(InputDevice device, string schemeName)
    {
        if (device is Keyboard)
            usedKeyboardSchemes.Add(schemeName);

        PlayerInput newPlayer = PlayerInput.Instantiate(
            prefab: playerPrefab,
            playerIndex: -1,
            controlScheme: schemeName,
            pairWithDevice: device
        );

        newPlayer.transform.position = spawnPoints[currPlayersInGame].position;
        PlayerController playerController = newPlayer.gameObject.GetComponent<PlayerController>();
        currPlayersInGame++;
        GameManager.instance.AddPlayer(playerController, spawnPoints[currPlayersInGame].position);

        newPlayer.SendMessage("OnAssignedScheme", schemeName, SendMessageOptions.DontRequireReceiver);
        Debug.Log($"New player with device: {device.name} and scheme: {schemeName}");
    }

    public void FreeKeyboardScheme(string schemeName)
    {
        if (usedKeyboardSchemes.Contains(schemeName))
        {
            usedKeyboardSchemes.Remove(schemeName);
            Debug.Log($"Esquema {schemeName} liberado.");
        }
    }

    private class InputControlObserver : IObserver<InputControl>
    {
        private PlayersManager manager;

        public InputControlObserver(PlayersManager manager)
        {
            this.manager = manager;
        }

        public void OnNext(InputControl value)
        {
            manager.HandleButtonPress(value);
        }

        public void OnError(Exception error)
        {
            Debug.LogError("InputSystem Observable Error: " + error.Message);
        }

        public void OnCompleted() { }
    }
}

