using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Seats the players chosen in the Lobby (<see cref="MatchSetup"/>) when a
/// level loads, each in the slot of their lobby room: same colour, same spawn,
/// same "Player N". Then starts the first countdown.
///
/// A level is only playable through the Lobby: opened on its own it sends
/// you there (joining in the arena was the old flow and is gone).
/// </summary>
public class PlayersManager : MonoBehaviour
{
    [SerializeField] private Transform[]  spawnPoints;
    [SerializeField] private MaterialsSO  materialsSO;
    [SerializeField] private GameObject   playerPrefab;

    [Tooltip("Seconds between the scene transition finishing and the 3-2-1, "
             + "so the chickens are seen arriving before the countdown.")]
    [SerializeField, Min(0f)] private float lobbyCountdownDelay = 0.5f;

    [Tooltip("Where to go when the level is opened without players (e.g. Play pressed on the level in the editor).")]
    [SerializeField] private string lobbyScene = "Lobby";

    private const int MinPlayers = 2;

    private int _seated;

    private void Awake()
    {
        // Leftover from the old in-arena join: if a PlayerInputManager is still
        // in the scene it would spawn extra chickens on any button press.
        if (TryGetComponent(out PlayerInputManager legacyJoin))
            legacyJoin.DisableJoining();
    }

    private void Start()
    {
        foreach (LobbySeat seat in MatchSetup.Seats)
        {
            if (seat.Device == null || !seat.Device.added)
            {
                Debug.LogWarning($"[PlayersManager] P{seat.Slot + 1}'s device is gone; seat skipped.");
                continue;
            }
            Seat(seat.Device, seat.Scheme, seat.Slot);
        }
        MatchSetup.ClearSeats();

        if (_seated < MinPlayers)
        {
            // Opened directly, or a pad was unplugged during the transition.
            Debug.LogWarning($"[PlayersManager] {_seated} player(s) seated: a level needs {MinPlayers}. Going to the Lobby.");
            SceneManager.LoadScene(lobbyScene);
            return;
        }

        StartCoroutine(StartMatchWhenVisible());
    }

    private IEnumerator StartMatchWhenVisible()
    {
        yield return null; // every Start() has run

        // Wait for the circle transition to open fully, then give the
        // chickens their moment on screen.
        while (SceneTransitionService.Instance != null && SceneTransitionService.Instance.IsTransitioning)
            yield return null;

        if (lobbyCountdownDelay > 0f)
            yield return new WaitForSeconds(lobbyCountdownDelay);

        GameManager.instance.TriggerStartSequence();
    }

    /// <param name="slot">Lobby room (0..3): fixes spawn, colour and player index.</param>
    private void Seat(InputDevice device, string schemeName, int slot)
    {
        if (slot < 0 || slot >= spawnPoints.Length || slot >= materialsSO.playerMaterials.Count)
        {
            Debug.LogWarning($"[PlayersManager] No spawn point / material for slot {slot}.");
            return;
        }

        PlayerInput newPlayer = PlayerInput.Instantiate(
            prefab:          playerPrefab,
            playerIndex:     -1,
            controlScheme:   schemeName,
            pairWithDevice:  device
        );

        PlayerController playerController = newPlayer.GetComponent<PlayerController>();
        playerController.TeleportTo(spawnPoints[slot].position);
        GameManager.instance.AddPlayer(
            playerController,
            spawnPoints[slot].position,
            materialsSO.playerMaterials[slot],
            slot
        );
        _seated++;

        newPlayer.SendMessage("OnAssignedScheme", schemeName, SendMessageOptions.DontRequireReceiver);
    }
}
