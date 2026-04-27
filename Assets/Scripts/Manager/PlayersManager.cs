using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class PlayersManager : MonoBehaviour
{
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private MaterialsSO materialsSO;
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

        InputControlObserver observer = new InputControlObserver(this);
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
        GameManager.instance.AddPlayer(playerController, spawnPoints[currPlayersInGame].position, materialsSO.playerMaterials[currPlayersInGame]);
        currPlayersInGame++;

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

#if UNITY_EDITOR
    // --- Propiedades de solo lectura para el editor ---

    public int SpawnPointCount => spawnPoints != null ? spawnPoints.Length : 0;

    /// <summary>
    /// Instancia un jugador sin device real para prop�sitos de debug en el editor.
    /// Utiliza el mismo prefab y datos de spawn que el flujo normal.
    /// </summary>
    public void Debug_AddPlayer()
    {
        // Validar que haya slots disponibles
        if (playerPrefab == null)
        {
            Debug.LogWarning("[Debug] playerPrefab no asignado en PlayersManager.");
            return;
        }

        if (spawnPoints == null || currPlayersInGame >= spawnPoints.Length)
        {
            Debug.LogWarning("[Debug] No hay spawn points disponibles para m�s jugadores.");
            return;
        }

        if (materialsSO == null || currPlayersInGame >= materialsSO.playerMaterials.Count)
        {
            Debug.LogWarning("[Debug] No hay materiales disponibles para m�s jugadores.");
            return;
        }

        // Instanciamos sin PlayerInput real � el jugador existir� en escena pero sin input
        GameObject debugPlayer = UnityEngine.Object.Instantiate(playerPrefab, spawnPoints[currPlayersInGame].position, Quaternion.identity);
        PlayerController playerController = debugPlayer.GetComponent<PlayerController>();

        if (playerController == null)
        {
            Debug.LogWarning("[Debug] El prefab no tiene un PlayerController.");
            UnityEngine.Object.Destroy(debugPlayer);
            return;
        }

        GameManager.instance.AddPlayer(
            playerController,
            spawnPoints[currPlayersInGame].position,
            materialsSO.playerMaterials[currPlayersInGame]
        );

        currPlayersInGame++;
        Debug.Log($"[Debug] Jugador {currPlayersInGame} agregado sin input device.");
    }
#endif

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