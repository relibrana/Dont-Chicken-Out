using System;
using UnityEngine;

/// <summary>
/// Handles all physics-based movement for the player.
/// Consumes an InputPayload each tick and produces a StatePayload.
/// Has no knowledge of game state, blocks, UI, or networking.
///
/// Simulation vs presentation: everything that decides gameplay runs in the
/// fixed tick (FixedUpdate) and reads the Rigidbody2D, never the Transform.
/// The Transform is the drawn position: the body interpolates it between the
/// last two ticks, so motion is smooth at any display refresh rate (60 Hz TVs,
/// 144 Hz laptops) and the jump is identical on all of them. With Fusion 2 the
/// tick becomes FixedUpdateNetwork() and its Render() takes over the drawing.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMovement : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private PlatformerValuesSO valuesSO;

    [Header("Ground Detection")]
    [SerializeField] private float raycastDistance = 0.55f;
    [SerializeField] private float checkSpacing    = 0.2f;
    [SerializeField] private float spacingY        = 0.2f;
    [SerializeField] private float headCheckOffset = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>State snapshot after the last ProcessInput call.</summary>
    public StatePayload CurrentState { get; private set; }

    public bool  IsGrounded => _isGrounded;
    public bool  IsFalling  => _currentVelocity.y < 0f && !_isGrounded;
    public float VelocityY  => _currentVelocity.y;
    public float SpeedX     => Mathf.Abs(_currentVelocity.x);

    /// <summary>Fired when grounded state changes. bool = isGrounded.</summary>
    public event Action<bool> OnGroundedChanged;

    /// <summary>Fired once when a jump is successfully executed.</summary>
    public event Action OnJumped;

    /// <summary>Fired when glide state changes. bool = isGliding.</summary>
    public event Action<bool> OnGlideStateChanged;

    // ── Item state hooks ──────────────────────────────────────────────────────
    // Written only by PlayerItemState subclasses; every state must restore its
    // hook to the default on end so effects never leak across rounds.

    /// <summary>Lateral speed multiplier (papa caliente). 1 = normal.</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>Initial jump velocity multiplier (pollo metálico). 1 = normal.</summary>
    public float JumpMultiplier { get; set; } = 1f;

    /// <summary>Gravity multiplier (pollo metálico). 1 = normal.</summary>
    public float GravityMultiplier { get; set; } = 1f;

    /// <summary>Extra gravity applied only while falling (pollo metálico: mismo
    /// salto, caída más pesada). 1 = normal.</summary>
    public float FallGravityMultiplier { get; set; } = 1f;

    /// <summary>While false, holding jump no longer glides (pollo metálico).</summary>
    public bool GlideEnabled { get; set; } = true;

    /// <summary>
    /// States that forbid gliding for a while (stun: "no puede planear mientras
    /// siga aturdido"). A counter so two states never re-enable each other.
    /// </summary>
    public int GlideLocks { get; set; }

    /// <summary>Glide speed multiplier (pollo metálico: the metal weighs, the wings brake less).</summary>
    public float GlideSpeedMultiplier { get; set; } = 1f;

    /// <summary>
    /// Grounded only on other chickens (no block or floor under the feet).
    /// POW does not count a rival's head as ground (GDD).
    /// </summary>
    public bool IsOnPlayer { get; private set; }

    /// <summary>Air jumps left (doble salto). Consumed by HandleJump when airborne past coyote.</summary>
    public int AirJumpsRemaining { get; set; }

    /// <summary>Air jump height relative to the first jump (doble salto). 1 = same.</summary>
    public float AirJumpMultiplier { get; set; } = 1f;

    /// <summary>While true the body is frozen in place: no input velocity, no gravity (moco).</summary>
    public bool HoldPosition { get; set; }

    // ── Private state ─────────────────────────────────────────────────────────

    // Blocks a second ground jump right after takeoff, while the ground rays
    // still touch the floor.
    private const float JumpLockSeconds = 0.5f;

    private Rigidbody2D          _rb;
    private CapsuleCollider2D    _capsule;
    private int                  _solidMask;     // groundLayer without chickens (corner correction)
    private float                _descentTime;   // seconds falling this time (glide entry delay)
    private Rigidbody2D          _groundBody;    // what the feet stand on (a pushed piece carries the chicken)
    private readonly RaycastHit2D[] _castHits    = new RaycastHit2D[4];
    private readonly Collider2D[]   _overlapHits = new Collider2D[4];
    private PlayerAnimController _animController;

    private Vector2 _currentVelocity;
    private float   _velocitySmoothing;

    // Input arrives every frame; the tick reads the latest one. A press is
    // latched until a tick consumes it, so it is never lost on high refresh
    // rates (frames with no tick) nor applied twice on low ones (several ticks
    // in one frame).
    private InputPayload _input;
    private bool         _jumpPressedLatched;

    private bool _isGrounded;
    private bool _wasGrounded;
    private bool _isPlayerOnHead;
    private bool _isHoldingJump;
    private bool _isGliding;

    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private float _jumpLockTimer;

    private float _calculatedGravity;
    private float _calculatedInitialJumpVelocity;

    // Set by Teleport: interpolation is off until the next tick so the body
    // jumps instead of sliding from where it was.
    private bool _restoreInterpolation;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _capsule   = GetComponent<CapsuleCollider2D>();
        _solidMask = groundLayer.value & ~(1 << LayerMask.NameToLayer("Player"));

        _rb.gravityScale   = 0f;
        _rb.freezeRotation = true;
        _rb.interpolation  = RigidbodyInterpolation2D.Interpolate;

        CalculateJumpValues();
    }

    private void OnDisable()
    {
        _input              = default;
        _jumpPressedLatched = false;
    }

    /// <summary>
    /// Injects the PlayerAnimController reference from PlayerController.
    /// Called before any Update runs.
    /// </summary>
    public void SetAnimController(PlayerAnimController animController)
    {
        _animController = animController;
    }

    /// <summary>The simulation tick. All movement decisions happen here.</summary>
    private void FixedUpdate()
    {
        if (valuesSO == null) return;

        if (_restoreInterpolation)
        {
            _rb.interpolation     = RigidbodyInterpolation2D.Interpolate;
            _restoreInterpolation = false;
        }

        float dt = Time.fixedDeltaTime;

        UpdateTimers(dt);
        CheckGround();

        _isHoldingJump = _input.jumpHeld;

        if (HoldPosition)
        {
            _currentVelocity   = Vector2.zero;
            _velocitySmoothing = 0f;
        }
        else
        {
            HandleHorizontalMovement(_input.moveDirection, dt);
            HandleJump(_jumpPressedLatched);
        }
        _jumpPressedLatched = false;

        // Gravity always applies, regardless of game state.
        HandleGravity(dt);
        NotifyGroundedChange();
        _animController?.SetVelocityY(_currentVelocity.y);

        _currentVelocity.y = Mathf.Min(_currentVelocity.y, valuesSO.maxJumpSpeed);
        ApplyCornerCorrection(dt);
        ApplyVelocityToRigidbody();

        CurrentState = new StatePayload
        {
            position   = _rb.position,
            velocity   = _currentVelocity,
            isGrounded = _isGrounded,
        };
    }

    // ── Public methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Main entry point. Called by PlayerController every Update with the
    /// current frame's InputPayload. Only stores it: the next tick applies it.
    /// </summary>
    public void ProcessInput(InputPayload input)
    {
        _input = input;
        if (input.jumpPressed) _jumpPressedLatched = true;
    }

    /// <summary>
    /// Applies an external velocity impulse (kick, explosion, spring, etc.).
    /// </summary>
    public void AddImpulse(Vector2 impulse, bool resetSpeed = false)
    {
        if (resetSpeed) _currentVelocity = Vector2.zero;

        _currentVelocity += impulse;
        _isGrounded       = false;
        _coyoteTimer      = 0f;
    }

    /// <summary>
    /// Kills lateral momentum without touching the fall. Used by the stun: a
    /// player who only has input locked keeps sliding by deceleration, which
    /// reads as skidding rather than as being dazed.
    /// </summary>
    public void StopHorizontal()
    {
        _currentVelocity.x = 0f;
        _velocitySmoothing = 0f;
    }

    /// <summary>
    /// Instantly moves the body (teleport item, spawn, death zone). Clears
    /// velocity and airborne timers. Any instant reposition of the chicken must
    /// go through here: writing transform.position directly is overwritten by
    /// interpolation, or slides the chicken across the screen.
    /// </summary>
    public void Teleport(Vector2 position)
    {
        _rb.interpolation     = RigidbodyInterpolation2D.None;
        _rb.position          = position;
        transform.position    = position;
        _restoreInterpolation = true;

        _currentVelocity   = Vector2.zero;
        _velocitySmoothing = 0f;
        _isGrounded        = false;
        _coyoteTimer       = 0f;
    }

    /// <summary>Recalculates gravity and jump velocity from the ScriptableObject values.</summary>
    public void CalculateJumpValues()
    {
        if (valuesSO == null) return;

        _calculatedGravity             = 2f * valuesSO.peakHeight / -(valuesSO.timeToPeak * valuesSO.timeToPeak);
        _calculatedInitialJumpVelocity = -_calculatedGravity * valuesSO.timeToPeak;
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    private void HandleHorizontalMovement(float moveDirection, float dt)
    {
        // Lateral speed is one of the seven progression variables. The jump is
        // deliberately left untouched (doc §4.3), so only the arc width changes.
        float targetSpeed = moveDirection * valuesSO.maxSpeed * ProgressionManager.Get(ProgressionVar.LateralSpeed) * SpeedMultiplier;

        if (moveDirection != 0f)
            transform.localScale = new Vector3(Mathf.Sign(moveDirection), 1f, 1f);

        float smoothTime = Mathf.Abs(targetSpeed) > 0.01f
            ? valuesSO.accelerationTime
            : valuesSO.decelerationTime;

        _currentVelocity.x = Mathf.SmoothDamp(
            _currentVelocity.x,
            targetSpeed,
            ref _velocitySmoothing,
            smoothTime,
            Mathf.Infinity,
            dt
        );

        _animController?.SetSpeed(Mathf.Abs(_currentVelocity.x));
    }

    private void HandleJump(bool jumpPressed)
    {
        if (jumpPressed)
            _jumpBufferTimer = valuesSO.jumpBufferTime;

        bool canJump = _jumpBufferTimer > 0f
                    && _coyoteTimer     > 0f
                    && !_isPlayerOnHead
                    && _jumpLockTimer  <= 0f;

        // Air jump (doble salto item): a fresh press while airborne, past coyote.
        // Ignores the jump lock on purpose — it only guards the ground jump, and
        // would otherwise block a double jump within 0.5 s of takeoff.
        bool canAirJump = !canJump
                       && _jumpBufferTimer   > 0f
                       && !_isGrounded
                       && _coyoteTimer      <= 0f
                       && AirJumpsRemaining  > 0
                       && !_isPlayerOnHead;

        if (!canJump && !canAirJump) return;

        float multiplier = JumpMultiplier * (canAirJump ? AirJumpMultiplier : 1f);

        _currentVelocity.y = _calculatedInitialJumpVelocity * multiplier;
        _isGrounded        = false;
        _jumpBufferTimer   = 0f;
        _coyoteTimer       = 0f;

        if (canAirJump)
        {
            AirJumpsRemaining--;
        }
        else
        {
            _jumpLockTimer = JumpLockSeconds;
        }

        SimEvents.Raise(SimEvent.PlayerJumped, _rb.position, this);
        _animController?.SetGrounded(false);
        OnJumped?.Invoke();
    }

    private void HandleGravity(float dt)
    {
        if (HoldPosition)
        {
            _currentVelocity.y = 0f;
            return;
        }

        if (_isGrounded)
        {
            if (_currentVelocity.y <= 0f)
                _currentVelocity.y = -0.1f;
            return;
        }

        float gravity = _calculatedGravity * GravityMultiplier;

        // Falling can be heavier than rising (pollo metálico: no recorta el salto,
        // solo hace la caída más rápida). Se decide antes de integrar para que
        // todo el tick de descenso use la misma gravedad.
        if (_currentVelocity.y < 0f)
            gravity *= FallGravityMultiplier;

        _currentVelocity.y += gravity * dt;

        _descentTime = _currentVelocity.y < 0f ? _descentTime + dt : 0f;

        if (_currentVelocity.y < 0f)
        {
            bool  isGliding       = _isHoldingJump && GlideEnabled && GlideLocks <= 0
                                 && _descentTime >= valuesSO.glideEntryDelay;
            float glideMultiplier = isGliding ? valuesSO.glideResistance : 0f;
            // El tope de caída también escala con el peso: si no, un pollo pesado
            // solo llegaría antes al mismo límite en vez de caer más rápido.
            float fallLimit       = isGliding ? -4f : -25f * FallGravityMultiplier;

            if (isGliding != _isGliding)
            {
                _isGliding = isGliding;
                OnGlideStateChanged?.Invoke(_isGliding);
                SimEvents.Raise(_isGliding ? SimEvent.PlayerGlideStarted : SimEvent.PlayerGlideEnded, _rb.position, this);
            }

            _animController?.SetGliding(isGliding);

            if (isGliding)
            {
                // GDD: a fixed glide speed. The fall brakes at once (no slow
                // ease into it) and then holds; heavier states glide faster.
                float target = -valuesSO.glideFallSpeed * GlideSpeedMultiplier;
                _currentVelocity.y = Mathf.Max(_currentVelocity.y, target);
                _currentVelocity.y = Mathf.MoveTowards(_currentVelocity.y, target, Mathf.Abs(gravity) * dt);
            }
            else
            {
                float nextY = _currentVelocity.y
                            + gravity * (valuesSO.fallMultiplier - 1f - glideMultiplier) * dt;

                _currentVelocity.y = Mathf.Clamp(nextY, fallLimit, 50f);
            }
        }
        else if (_currentVelocity.y > 0f && !_isHoldingJump)
        {
            _currentVelocity.y += gravity * (valuesSO.lowJumpMultiplier - 1f) * dt;
        }
    }

    private void ApplyVelocityToRigidbody()
    {
        // GDD (empujar): a chicken standing on a piece that is being pushed
        // travels with it. Not on another chicken (that one never drags you).
        Vector2 velocity = _currentVelocity;
        if (_isGrounded && _groundBody != null)
            velocity.x += _groundBody.linearVelocity.x;

        _rb.linearVelocity = velocity;
    }

    private static Rigidbody2D PickGroundBody(Collider2D col)
    {
        if (col == null || IsPlayerCollider(col)) return null;
        Rigidbody2D body = col.attachedRigidbody;
        return body != null && body.bodyType == RigidbodyType2D.Dynamic ? body : null;
    }

    // ── Corner correction ─────────────────────────────────────────────────────

    /// <summary>
    /// GDD (colisionar): rising into the corner of a block, the chicken is
    /// slid sideways just enough to pass instead of bonking. Only when the way
    /// straight up is blocked and a small side step clears it; the smaller
    /// step wins. Chickens are not corners (they are excluded from the mask).
    /// </summary>
    private void ApplyCornerCorrection(float dt)
    {
        if (_capsule == null || _currentVelocity.y <= 0f || valuesSO.cornerCorrection <= 0f) return;

        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(_solidMask);

        Vector2 centre   = _rb.position + _capsule.offset;
        Vector2 size     = _capsule.size;
        float   distance = _currentVelocity.y * dt + 0.02f;

        if (!CapsuleBlocked(centre, size, filter, distance)) return;

        const float step = 0.03f;
        for (float shift = step; shift <= valuesSO.cornerCorrection + 0.0001f; shift += step)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 moved = centre + new Vector2(side * shift, 0f);
                if (Physics2D.OverlapCapsule(moved, size, _capsule.direction, 0f, filter, _overlapHits) > 0) continue;
                if (CapsuleBlocked(moved, size, filter, distance)) continue;

                _rb.position += new Vector2(side * shift, 0f);
                return;
            }
        }
    }

    private bool CapsuleBlocked(Vector2 centre, Vector2 size, ContactFilter2D filter, float distance) =>
        Physics2D.CapsuleCast(centre, size, _capsule.direction, 0f, Vector2.up, filter, _castHits, distance) > 0;

    // ── Ground detection ──────────────────────────────────────────────────────

    private void CheckGround()
    {
        // The body, not the Transform: the Transform is the interpolated
        // (drawn) position and lags up to one tick behind.
        Vector2 pos = _rb.position;

        Collider2D footL = RaycastNonTrigger(new Vector2(pos.x - checkSpacing, pos.y - spacingY), Vector2.down);
        Collider2D footC = RaycastNonTrigger(new Vector2(pos.x, pos.y - spacingY - 0.07f), Vector2.down);
        Collider2D footR = RaycastNonTrigger(new Vector2(pos.x + checkSpacing, pos.y - spacingY), Vector2.down);

        _isGrounded = footL != null || footC != null || footR != null;
        _groundBody = PickGroundBody(footC) ?? PickGroundBody(footL) ?? PickGroundBody(footR);
        IsOnPlayer  = _isGrounded
                   && (footL == null || IsPlayerCollider(footL))
                   && (footC == null || IsPlayerCollider(footC))
                   && (footR == null || IsPlayerCollider(footR));

        RaycastHit2D headL = Physics2D.Raycast(new Vector2(pos.x - checkSpacing, pos.y + headCheckOffset),         Vector2.up, raycastDistance, groundLayer);
        RaycastHit2D headC = Physics2D.Raycast(new Vector2(pos.x,                pos.y + headCheckOffset + 0.07f), Vector2.up, raycastDistance, groundLayer);
        RaycastHit2D headR = Physics2D.Raycast(new Vector2(pos.x + checkSpacing, pos.y + headCheckOffset),         Vector2.up, raycastDistance, groundLayer);

        _isPlayerOnHead = IsPlayerCollider(headL.collider)
                       || IsPlayerCollider(headC.collider)
                       || IsPlayerCollider(headR.collider);
    }

    private Collider2D RaycastNonTrigger(Vector2 origin, Vector2 direction)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, raycastDistance, groundLayer);
        foreach (var hit in hits)
        {
            if (!hit.collider.isTrigger)
                return hit.collider;
        }
        return null;
    }

    private static bool IsPlayerCollider(Collider2D col) =>
        col != null && col.gameObject.CompareTag("Player");

    // ── Timers ────────────────────────────────────────────────────────────────

    private void UpdateTimers(float dt)
    {
        if (_jumpBufferTimer > 0f) _jumpBufferTimer -= dt;
        if (_jumpLockTimer   > 0f) _jumpLockTimer   -= dt;

        if (_isGrounded)
            _coyoteTimer = valuesSO.coyoteTime;
        else if (_coyoteTimer > 0f)
            _coyoteTimer -= dt;
    }

    // ── Grounded change notification ──────────────────────────────────────────

    private void NotifyGroundedChange()
    {
        if (_isGrounded == _wasGrounded) return;

        if (_isGrounded)
        {
            SimEvents.Raise(SimEvent.PlayerLanded, _rb.position, this);

            if (_isGliding)
            {
                _isGliding = false;
                OnGlideStateChanged?.Invoke(false);
                SimEvents.Raise(SimEvent.PlayerGlideEnded, _rb.position, this);
            }
        }

        _wasGrounded = _isGrounded;
        _animController?.SetGrounded(_isGrounded);
        OnGroundedChanged?.Invoke(_isGrounded);
    }

    // ── Editor helpers ────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        float startY = transform.position.y - spacingY;
        float x      = transform.position.x;

        Gizmos.color = Color.yellow;

        Gizmos.DrawLine(new Vector2(x,                startY - 0.07f),                   new Vector2(x,                startY - raycastDistance));
        Gizmos.DrawLine(new Vector2(x - checkSpacing, startY),                            new Vector2(x - checkSpacing, startY - raycastDistance));
        Gizmos.DrawLine(new Vector2(x + checkSpacing, startY),                            new Vector2(x + checkSpacing, startY - raycastDistance));

        Gizmos.DrawLine(new Vector2(x,                startY + headCheckOffset + 0.07f),  new Vector2(x,                startY + headCheckOffset + raycastDistance));
        Gizmos.DrawLine(new Vector2(x - checkSpacing, startY + headCheckOffset),          new Vector2(x - checkSpacing, startY + headCheckOffset + raycastDistance));
        Gizmos.DrawLine(new Vector2(x + checkSpacing, startY + headCheckOffset),          new Vector2(x + checkSpacing, startY + headCheckOffset + raycastDistance));
    }
#endif
}