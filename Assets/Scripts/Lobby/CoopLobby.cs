using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

/// <summary>
/// The hen house: one room per player (up to 4). Each player drives their
/// own room with their own controls; the host (Player 1, who came from the
/// menu) starts the match once everyone in is ready.
///
/// Controls, per player (UI map of InputSystem.inputactions, filtered to the
/// player's device and scheme, so two keyboard players never cross):
///   · outside → Select (or Start on a pad) to take the first free room
///   · in      → Select: ready · Back: leave (host: back to the rules screen)
///   · ready   → Back: not ready · host only: Select again to start
///
/// The chickens stand still in their rooms on purpose (design, nov 2026).
/// </summary>
public sealed class CoopLobby : LobbyScreen
{
    [Header("Rooms (slot order: P1, P2, P3, P4)")]
    [SerializeField] private LobbyRoom[] rooms = new LobbyRoom[MatchSetup.MaxSeats];

    [Header("Players")]
    [Tooltip("InputSystem.inputactions — the same asset the chickens use, so remaps carry over.")]
    [SerializeField] private InputActionAsset playerActions;
    [SerializeField] private MaterialsSO      materials;
    [Tooltip("The chicken's visual (the Rig child of PlayerPrefab). Copied into each room without gameplay scripts.")]
    [SerializeField] private GameObject       chickenVisualSource;
    [Tooltip("Height of the chicken in the room, world units (the room is ~4.2 tall). 0 = prefab scale.")]
    [SerializeField, Min(0f)]   private float chickenHeight = 2.3f;
    [SerializeField, Min(1)]    private int   minPlayers   = 2;

    [Header("Global UI")]
    [SerializeField] private TextMeshProUGUI readyCounter;
    [SerializeField] private TextMeshProUGUI startPrompt;

    [Header("Texts ({0} = button name)")]
    [SerializeField] private string joinHint        = "Pulsa  A  ·  E  ·  Shift Der.\npara entrar";
    [SerializeField] private string notReadyHint    = "{0}  ¡Listo!\n{1}  Salir";
    [SerializeField] private string hostNotReadyHint = "{0}  ¡Listo!\n{1}  Volver";
    [SerializeField] private string readyHint       = "{1}  Cancelar";
    [SerializeField] private string readyCounterFmt = "LISTOS  {0}/{1}";
    [SerializeField] private string needPlayersFmt  = "Faltan pollos: mínimo {0}";
    [SerializeField] private string hostStartFmt    = "P{0}:  {1}  ¡A jugar!";
    [SerializeField] private string waitingText     = "Esperando a que todos estén listos…";

    public event Action                  BackRequested;
    public event Action<List<LobbySeat>> StartRequested;

    public override bool UsesMenuCancel => false;

    // ── Seat state ────────────────────────────────────────────────────────────

    private sealed class Seat
    {
        public LobbySeat        Data;
        public InputActionAsset Actions;
        public InputAction      Select;
        public InputAction      Back;
        public bool             Ready;
        public int              JoinedFrame;
        public string           SelectName;
        public string           BackName;
        public Color            Color;
    }

    private readonly Seat[] _seats = new Seat[MatchSetup.MaxSeats];
    private int             _hostSlot = -1;
    private bool            _starting;
    private IDisposable     _anyButton;

    // ── Screen lifecycle ──────────────────────────────────────────────────────

    public override void Enter()
    {
        base.Enter();
        _starting = false;

        // Nothing in this screen is a UI button: keep the EventSystem from
        // "submitting" whatever was focused on the previous screen.
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);

        for (int i = 0; i < rooms.Length; i++)
            rooms[i].ShowEmpty(joinHint);

        // Player 1 is whoever drove the menus: seated straight away, as host.
        if (SessionData.HasPrimaryDevice && SessionData.PrimaryDevice.added)
            TryJoin(SessionData.PrimaryDevice, SessionData.PrimaryScheme);

        _anyButton = InputSystem.onAnyButtonPress.Call(OnAnyButton);
        InputSystem.onDeviceChange += OnDeviceChange;
        RefreshGlobal();
    }

    public override void Exit()
    {
        _anyButton?.Dispose();
        _anyButton = null;
        InputSystem.onDeviceChange -= OnDeviceChange;

        for (int i = 0; i < _seats.Length; i++)
            ReleaseSeat(i);
        _hostSlot = -1;

        base.Exit();
    }

    private void OnDestroy()
    {
        _anyButton?.Dispose();
        InputSystem.onDeviceChange -= OnDeviceChange;
        for (int i = 0; i < _seats.Length; i++)
            ReleaseSeat(i);
    }

    // ── Per-frame input ───────────────────────────────────────────────────────

    private void Update()
    {
        if (_starting) return;

        for (int slot = 0; slot < _seats.Length; slot++)
        {
            Seat seat = _seats[slot];
            if (seat == null || Time.frameCount <= seat.JoinedFrame) continue;

            if (seat.Select.WasPressedThisFrame())
            {
                OnSelect(slot);
            }
            else if (seat.Back.WasPressedThisFrame())
            {
                // Start lives in the Back action of the pad's UI map. In the
                // lobby it reads as "go": mashing Start must not kick you out.
                if (seat.Back.activeControl?.device is Gamepad pad && seat.Back.activeControl == pad.startButton)
                    OnSelect(slot);
                else
                    OnBack(slot);
            }

            if (_starting) return;
        }
    }

    private void OnSelect(int slot)
    {
        Seat seat = _seats[slot];
        if (seat == null) return;

        if (!seat.Ready)
        {
            SetReady(slot, true);
            return;
        }

        if (slot == _hostSlot && CanStart())
            BeginMatch();
    }

    private void OnBack(int slot)
    {
        Seat seat = _seats[slot];
        if (seat == null) return;

        if (seat.Ready)
        {
            SetReady(slot, false);
            return;
        }

        if (slot == _hostSlot)
        {
            PlayUISound("lobby_leave");
            BackRequested?.Invoke();
            return;
        }

        Leave(slot);
    }

    // ── Joining ───────────────────────────────────────────────────────────────

    private void OnAnyButton(InputControl control)
    {
        if (_starting || !isActiveAndEnabled) return;

        if (control.device is Gamepad pad)
        {
            if (control == pad.buttonSouth || control == pad.startButton)
                TryJoin(pad, "Gamepad");
        }
        else if (control.device is Keyboard keyboard && control is KeyControl key)
        {
            // The Select key of each keyboard half also takes a seat.
            if (key.keyCode == Key.E)               TryJoin(keyboard, "Keyboard1");
            else if (key.keyCode == Key.RightShift) TryJoin(keyboard, "Keyboard2");
        }
    }

    private void TryJoin(InputDevice device, string scheme)
    {
        if (device == null || string.IsNullOrEmpty(scheme)) return;
        if (IsSeated(device, scheme)) return;

        int slot = FirstFreeSlot();
        if (slot < 0) return;

        InputActionAsset actions = Instantiate(playerActions);
        actions.devices     = new[] { device };
        actions.bindingMask = InputBinding.MaskByGroup(scheme);
        InputActionMap ui   = actions.FindActionMap("UI", throwIfNotFound: true);
        ui.Enable();

        var seat = new Seat
        {
            Data        = new LobbySeat { Slot = slot, Device = device, Scheme = scheme },
            Actions     = actions,
            Select      = ui.FindAction("Select", throwIfNotFound: true),
            Back        = ui.FindAction("Back",   throwIfNotFound: true),
            JoinedFrame = Time.frameCount,
            Color       = ColorFor(slot),
        };
        seat.SelectName = ButtonName(seat.Select, scheme, null);
        seat.BackName   = ButtonName(seat.Back,   scheme, "<Gamepad>/start");
        _seats[slot] = seat;

        if (_hostSlot < 0) _hostSlot = slot;

        rooms[slot].ShowTaken(slot, seat.Color, slot == _hostSlot, HintFor(slot), SpawnChicken(slot));
        PlayUISound("lobby_join");
        RefreshGlobal();
    }

    private void Leave(int slot)
    {
        if (_seats[slot] == null) return;

        ReleaseSeat(slot);
        rooms[slot].ShowEmpty(joinHint);
        PlayUISound("lobby_leave");

        if (slot == _hostSlot)
        {
            // Only reachable through a disconnect: the next player in line hosts.
            _hostSlot = FirstTakenSlot();
            if (_hostSlot >= 0)
            {
                rooms[_hostSlot].SetHost(true);
                rooms[_hostSlot].SetHint(HintFor(_hostSlot));
            }
        }

        RefreshGlobal();
    }

    private void ReleaseSeat(int slot)
    {
        Seat seat = _seats[slot];
        if (seat == null) return;

        seat.Actions.Disable();
        Destroy(seat.Actions);
        _seats[slot] = null;
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change != InputDeviceChange.Removed && change != InputDeviceChange.Disconnected) return;

        for (int slot = 0; slot < _seats.Length; slot++)
        {
            if (_seats[slot] != null && _seats[slot].Data.Device == device)
                Leave(slot);
        }
    }

    // ── Ready / start ─────────────────────────────────────────────────────────

    private void SetReady(int slot, bool ready)
    {
        Seat seat = _seats[slot];
        seat.Ready = ready;
        rooms[slot].SetReady(ready, seat.Color, HintFor(slot));
        PlayUISound(ready ? "lobby_ready" : "lobby_unready");
        RefreshGlobal();
    }

    private bool CanStart()
    {
        int taken = 0;
        foreach (Seat seat in _seats)
        {
            if (seat == null) continue;
            if (!seat.Ready) return false;
            taken++;
        }
        return taken >= minPlayers;
    }

    private void BeginMatch()
    {
        _starting = true;

        var seats = new List<LobbySeat>();
        foreach (Seat seat in _seats)
            if (seat != null) seats.Add(seat.Data);

        PlayUISound("lobby_start");
        StartRequested?.Invoke(seats);
    }

    // ── View ──────────────────────────────────────────────────────────────────

    private void RefreshGlobal()
    {
        int taken = 0, ready = 0;
        foreach (Seat seat in _seats)
        {
            if (seat == null) continue;
            taken++;
            if (seat.Ready) ready++;
        }

        if (readyCounter != null)
            readyCounter.text = taken < minPlayers
                ? string.Format(needPlayersFmt, minPlayers)
                : string.Format(readyCounterFmt, ready, taken);

        if (startPrompt != null)
        {
            bool canStart = CanStart() && _hostSlot >= 0;
            startPrompt.text = canStart
                ? string.Format(hostStartFmt, _hostSlot + 1, _seats[_hostSlot].SelectName)
                : (taken >= minPlayers ? waitingText : "");
        }

        // The host's hint depends on everyone else (start vs. wait).
        if (_hostSlot >= 0 && _seats[_hostSlot] != null)
            rooms[_hostSlot].SetHint(HintFor(_hostSlot));
    }

    private string HintFor(int slot)
    {
        Seat seat = _seats[slot];
        if (seat == null) return joinHint;

        if (!seat.Ready)
            return string.Format(slot == _hostSlot ? hostNotReadyHint : notReadyHint, seat.SelectName, seat.BackName);

        return string.Format(readyHint, seat.SelectName, seat.BackName);
    }

    private Color ColorFor(int slot)
    {
        PlayerMaterial mats = materials != null && slot < materials.playerMaterials.Count
            ? materials.playerMaterials[slot] : null;
        Color authored = mats != null ? PlayerController.ResolveLabelColor(mats) : new Color(1f, 1f, 1f, 0f);
        return PlayerNameTag.ResolveColor(authored, slot);
    }

    /// <summary>
    /// Copies the chicken's visual into the room: sprites, bones, animator.
    /// Built inactive so none of our gameplay scripts wake up, then stripped
    /// of them before it is switched on.
    /// </summary>
    private GameObject SpawnChicken(int slot)
    {
        if (chickenVisualSource == null) return null;

        Transform anchor = rooms[slot].ChickenAnchor;
        var holder = new GameObject("ChickenHolder");
        holder.SetActive(false);
        holder.transform.SetParent(anchor, false);

        GameObject chicken = Instantiate(chickenVisualSource, holder.transform);
        chicken.name = $"Chicken_P{slot + 1}";

        System.Reflection.Assembly ours = typeof(CoopLobby).Assembly;
        foreach (MonoBehaviour mb in chicken.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null && mb.GetType().Assembly == ours) DestroyImmediate(mb);

        // The clips' animation events need a receiver next to each Animator.
        foreach (Animator animator in chicken.GetComponentsInChildren<Animator>(true))
            animator.gameObject.AddComponent<LobbyAnimEventSink>();

        if (materials != null && slot < materials.playerMaterials.Count)
        {
            Material mat = materials.playerMaterials[slot].playerMat;
            if (mat != null)
                foreach (SpriteRenderer sr in chicken.GetComponentsInChildren<SpriteRenderer>(true))
                    sr.sharedMaterial = mat;
        }

        Transform t = chicken.transform;
        t.SetParent(anchor, false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale    = chickenVisualSource.transform.localScale;
        Destroy(holder);

        // The anchor marks the floor: size the chicken and stand it on it,
        // whatever pivot and scale the rig has in the prefab.
        if (TryGetBounds(chicken, out Bounds b))
        {
            if (chickenHeight > 0f && b.size.y > 0.01f)
            {
                t.localScale *= chickenHeight / b.size.y;
                TryGetBounds(chicken, out b);
            }
            t.position += new Vector3(anchor.position.x - b.center.x, anchor.position.y - b.min.y, 0f);
        }
        return chicken;
    }

    private static bool TryGetBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr.sprite == null) continue;
            // From the sprite and transform, not sr.bounds: skinned sprites
            // only update their bounds after the first render.
            Bounds local = sr.sprite.bounds;
            Vector3 min = sr.transform.TransformPoint(local.min);
            Vector3 max = sr.transform.TransformPoint(local.max);
            var world = new Bounds((min + max) * 0.5f, Vector3.zero);
            world.Encapsulate(min);
            world.Encapsulate(max);
            if (!any) { bounds = world; any = true; }
            else bounds.Encapsulate(world);
        }
        return any;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsSeated(InputDevice device, string scheme)
    {
        foreach (Seat seat in _seats)
        {
            if (seat == null) continue;
            if (device is Keyboard ? seat.Data.Scheme == scheme : seat.Data.Device == device)
                return true;
        }
        return false;
    }

    private int FirstFreeSlot()
    {
        int count = Mathf.Min(rooms.Length, _seats.Length);
        for (int i = 0; i < count; i++)
            if (_seats[i] == null) return i;
        return -1;
    }

    private int FirstTakenSlot()
    {
        for (int i = 0; i < _seats.Length; i++)
            if (_seats[i] != null) return i;
        return -1;
    }

    /// <summary>Name of the button bound to <paramref name="action"/> for this scheme, e.g. "E", "A", "Cross".</summary>
    private static string ButtonName(InputAction action, string scheme, string skipPath)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding b = action.bindings[i];
            if (b.isComposite || b.isPartOfComposite) continue;
            if (string.IsNullOrEmpty(b.groups) || !b.groups.Contains(scheme)) continue;
            if (skipPath != null && b.effectivePath == skipPath) continue;

            string name = action.GetBindingDisplayString(i);
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return "?";
    }
}
