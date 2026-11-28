using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads raw input from PlayerInput and exposes it as an InputPayload each frame.
/// Has no knowledge of physics, game state, or UI.
/// Dispatches higher-level events (pause, cluck) for PlayerController to route.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public sealed class PlayerInputHandler : MonoBehaviour
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Current frame's input snapshot. Consume this in Update/FixedUpdate.</summary>
    public InputPayload CurrentInput { get; private set; }

    /// <summary>Fired when the player presses Pause during gameplay.</summary>
    public event Action OnPausePressed;

    /// <summary>Fired when the player presses Back from the UI action map.</summary>
    public event Action OnBackUIPressed;

    /// <summary>Fired when the player presses Cluck (ready-up / quack).</summary>
    public event Action OnCluckPressed;

    /// <summary>Fired when the player presses Kick.</summary>
    public event Action OnKickPressed;

    /// <summary>Fired when the player presses PlaceBlock.</summary>
    public event Action OnPlaceBlockPressed;

    /// <summary>Fired on a fresh Jump press (the moco struggle counts jumps).</summary>
    public event Action OnJumpPressed;

    /// <summary>Fired when the player presses UseItem (own button, GDD).</summary>
    public event Action OnUseItemPressed;

    /// <summary>
    /// Fired on a *fresh* directional press: the axis leaving neutral or
    /// flipping side. Holding a key does not re-fire — the struggle mechanics
    /// count presses, not held input. The float is the direction pressed,
    /// so a struggle can shake the chicken towards it.
    /// </summary>
    public event Action<float> OnMovePressed;

    // ── Stick (mando) ─────────────────────────────────────────────────────────
    // Nov 2026: a stick let go fast springs past the centre for a frame or two;
    // with the default deadzone (0.125) that was enough to turn the chicken
    // round. Keyboards never go through this.

    [Header("Stick del mando")]
    [SerializeField, Range(0f, 0.9f), Tooltip("Por debajo de esto el stick cuenta como suelto. El resto se reescala a 0-1.")]
    private float stickDeadzone = 0.3f;

    [SerializeField, Range(0f, 1f), Tooltip("Justo después de soltar, ir al lado contrario solo cuenta si se empuja al menos esto (filtra el rebote del stick).")]
    private float stickReverseThreshold = 0.6f;

    [SerializeField, Min(0f), Tooltip("Cuánto dura esa protección contra el rebote tras soltar.")]
    private float stickReverseWindow = 0.12f;

    private float _stickHeldSign;
    private float _stickHeldTime = float.NegativeInfinity;

    // ── Private state ─────────────────────────────────────────────────────────

    private PlayerInput _playerInput;

    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _placeBlockAction;
    private InputAction _kickAction;
    private InputAction _useItemAction;
    private InputAction _cluckAction;
    private InputAction _pauseAction;
    private InputAction _backUIAction;

    // Transient per-frame flags, consumed into CurrentInput each Update.
    private float _moveDirection;
    private bool _jumpPressed;
    private bool _jumpHeld;
    private bool _placeBlockPressed;
    private bool _kickPressed;
    private bool _cluckPressed;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        BindActions();
    }

    private void OnEnable()
    {
        EnableActions();
    }

    private void OnDisable()
    {
        DisableActions();
    }

    private void OnDestroy()
    {
        UnbindActions();
    }

    private void Update()
    {
        // Snapshot current frame input into the payload.
        CurrentInput = new InputPayload
        {
            moveDirection    = _moveDirection,
            jumpPressed      = _jumpPressed,
            jumpHeld         = _jumpHeld,
            placeBlockPressed = _placeBlockPressed,
            kickPressed      = _kickPressed,
            cluckPressed     = _cluckPressed,
        };

        // Consume single-frame flags so they don't bleed into the next tick.
        _jumpPressed      = false;
        _placeBlockPressed = false;
        _kickPressed      = false;
        _cluckPressed     = false;
    }

    // ── Input binding ─────────────────────────────────────────────────────────

    private void BindActions()
    {
        var actions = _playerInput.actions;

        _moveAction       = actions.FindAction("Move");
        _jumpAction       = actions.FindAction("Jump");
        _placeBlockAction = actions.FindAction("PlaceBlock");
        _kickAction       = actions.FindAction("Kick");
        _useItemAction    = actions.FindAction("UseItem");
        _cluckAction      = actions.FindAction("Cluck");
        _pauseAction      = actions.FindAction("Pause");
        _backUIAction     = actions.FindActionMap("UI")?.FindAction("Back");

        _moveAction.performed       += OnMovePerformed;
        _moveAction.canceled        += OnMoveCanceled;
        _jumpAction.started         += OnJumpStarted;
        _jumpAction.performed       += OnJumpPerformed;
        _jumpAction.canceled        += OnJumpCanceled;
        _placeBlockAction.started   += OnPlaceBlockStarted;
        _kickAction.started         += OnKickStarted;
        if (_useItemAction != null)
            _useItemAction.started  += OnUseItemStarted;
        _cluckAction.started        += OnCluckStarted;
        _pauseAction.started        += OnPauseStarted;

        if (_backUIAction != null)
            _backUIAction.started   += OnBackUIStarted;
    }

    private void UnbindActions()
    {
        if (_moveAction != null)
        {
            _moveAction.performed -= OnMovePerformed;
            _moveAction.canceled  -= OnMoveCanceled;
        }
        if (_jumpAction != null)
        {
            _jumpAction.started   -= OnJumpStarted;
            _jumpAction.performed -= OnJumpPerformed;
            _jumpAction.canceled  -= OnJumpCanceled;
        }
        if (_placeBlockAction != null) _placeBlockAction.started -= OnPlaceBlockStarted;
        if (_kickAction != null)       _kickAction.started       -= OnKickStarted;
        if (_useItemAction != null)    _useItemAction.started    -= OnUseItemStarted;
        if (_cluckAction != null)      _cluckAction.started      -= OnCluckStarted;
        if (_pauseAction != null)      _pauseAction.started      -= OnPauseStarted;
        if (_backUIAction != null)     _backUIAction.started     -= OnBackUIStarted;
    }

    private void EnableActions()
    {
        _moveAction?.Enable();
        _jumpAction?.Enable();
        _placeBlockAction?.Enable();
        _kickAction?.Enable();
        _useItemAction?.Enable();
        _cluckAction?.Enable();
        _pauseAction?.Enable();
        _backUIAction?.Enable();
    }

    private void DisableActions()
    {
        _moveAction?.Disable();
        _jumpAction?.Disable();
        _placeBlockAction?.Disable();
        _kickAction?.Disable();
        _useItemAction?.Disable();
        _cluckAction?.Disable();
        _pauseAction?.Disable();
        _backUIAction?.Disable();
    }

    // ── Input callbacks ───────────────────────────────────────────────────────

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        float previous = _moveDirection;
        float raw      = ctx.ReadValue<float>();
        _moveDirection = ctx.control?.device is Gamepad ? FilterStick(raw, previous) : raw;

        if (_moveDirection != 0f && (previous == 0f || Mathf.Sign(previous) != Mathf.Sign(_moveDirection)))
            OnMovePressed?.Invoke(_moveDirection);
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx) =>
        _moveDirection = 0f;

    /// <summary>
    /// Bigger deadzone, rescaled so a gentle tilt still walks slowly, and the
    /// release snapback ignored: right after holding one side, the other side
    /// only counts when pushed for real (a genuine quick turn goes far past it).
    /// </summary>
    private float FilterStick(float raw, float previous)
    {
        float abs = Mathf.Abs(raw);
        if (abs < stickDeadzone) return 0f;

        float sign = Mathf.Sign(raw);
        float now  = Time.unscaledTime;

        // Still holding the old side at the previous reading: "just released" is now.
        bool  heldUntilNow = previous != 0f && Mathf.Sign(previous) == _stickHeldSign;
        float sinceHeld    = heldUntilNow ? 0f : now - _stickHeldTime;

        if (_stickHeldSign != 0f && sign != _stickHeldSign && sinceHeld < stickReverseWindow && abs < stickReverseThreshold)
            return 0f;

        _stickHeldSign = sign;
        _stickHeldTime = now;
        return sign * Mathf.InverseLerp(stickDeadzone, 1f, abs);
    }

    private void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        _jumpPressed = true;
        OnJumpPressed?.Invoke();
    }

    private void OnJumpPerformed(InputAction.CallbackContext ctx) =>
        _jumpHeld = true;

    private void OnJumpCanceled(InputAction.CallbackContext ctx) =>
        _jumpHeld = false;

    private void OnPlaceBlockStarted(InputAction.CallbackContext ctx)
    {
        _placeBlockPressed = true;
        OnPlaceBlockPressed?.Invoke();
    }

    private void OnKickStarted(InputAction.CallbackContext ctx)
    {
        _kickPressed = true;
        OnKickPressed?.Invoke();
    }

    private void OnUseItemStarted(InputAction.CallbackContext ctx) =>
        OnUseItemPressed?.Invoke();

    private void OnCluckStarted(InputAction.CallbackContext ctx)
    {
        _cluckPressed = true;
        OnCluckPressed?.Invoke();
    }

    private void OnPauseStarted(InputAction.CallbackContext ctx) =>
        OnPausePressed?.Invoke();

    private void OnBackUIStarted(InputAction.CallbackContext ctx) =>
        OnBackUIPressed?.Invoke();
}