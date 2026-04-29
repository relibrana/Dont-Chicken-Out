using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Handles all physics-based movement for the player.
/// Consumes an InputPayload each tick and produces a StatePayload.
/// Has no knowledge of game state, blocks, UI, or networking.
/// Network-ready: ProcessInput() can be driven by local input or by a
/// reconciled payload from the server without changing any logic here.
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

    // ── Private state ─────────────────────────────────────────────────────────

    private Rigidbody2D          _rb;
    private PlayerAnimController _animController;

    private Vector2 _currentVelocity;
    private float   _velocitySmoothing;

    private bool _isGrounded;
    private bool _wasGrounded;
    private bool _isPlayerOnHead;
    private bool _isHoldingJump;
    private bool _jumpLocked;
    private bool _isGliding;

    private float _coyoteTimer;
    private float _jumpBufferTimer;

    private float _calculatedGravity;
    private float _calculatedInitialJumpVelocity;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        _rb.gravityScale   = 0f;
        _rb.freezeRotation = true;

        CalculateJumpValues();
    }

    /// <summary>
    /// Injects the PlayerAnimController reference from PlayerController.
    /// Called before any Update runs.
    /// </summary>
    public void SetAnimController(PlayerAnimController animController)
    {
        _animController = animController;
    }

    private void Update()
    {
        if (valuesSO == null) return;

        UpdateTimers();
        CheckGround();
        HandleGravity();
        NotifyGroundedChange();
        _animController?.SetVelocityY(_currentVelocity.y);
    }

    private void FixedUpdate()
    {
        if (valuesSO == null) return;

        _currentVelocity.y = Mathf.Min(_currentVelocity.y, valuesSO.maxJumpSpeed);
        ApplyVelocityToRigidbody();
    }

    // ── Public methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Main entry point. Called by PlayerController every Update with the
    /// current frame's InputPayload. Drives horizontal movement and jump.
    /// Gravity runs independently in Update so it always applies regardless
    /// of game state.
    /// </summary>
    public void ProcessInput(InputPayload input)
    {
        _isHoldingJump = input.jumpHeld;

        HandleHorizontalMovement(input.moveDirection);
        HandleJump(input.jumpPressed);

        CurrentState = new StatePayload
        {
            position   = _rb.position,
            velocity   = _currentVelocity,
            isGrounded = _isGrounded,
        };
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

    /// <summary>Recalculates gravity and jump velocity from the ScriptableObject values.</summary>
    public void CalculateJumpValues()
    {
        if (valuesSO == null) return;

        _calculatedGravity             = 2f * valuesSO.peakHeight / -(valuesSO.timeToPeak * valuesSO.timeToPeak);
        _calculatedInitialJumpVelocity = -_calculatedGravity * valuesSO.timeToPeak;
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    private void HandleHorizontalMovement(float moveDirection)
    {
        float targetSpeed = moveDirection * valuesSO.maxSpeed;

        if (moveDirection != 0f)
            transform.localScale = new Vector3(Mathf.Sign(moveDirection), 1f, 1f);

        float smoothTime = Mathf.Abs(targetSpeed) > 0.01f
            ? valuesSO.accelerationTime
            : valuesSO.decelerationTime;

        _currentVelocity.x = Mathf.SmoothDamp(
            _currentVelocity.x,
            targetSpeed,
            ref _velocitySmoothing,
            smoothTime
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
                    && !_jumpLocked;

        if (!canJump) return;

        _currentVelocity.y = _calculatedInitialJumpVelocity;
        _isGrounded        = false;
        _jumpLocked        = true;
        _jumpBufferTimer   = 0f;
        _coyoteTimer       = 0f;

        DOVirtual.DelayedCall(0.5f, () => _jumpLocked = false, false);

        AudioManager.Instance.PlaySound("player_jump");
        _animController?.SetGrounded(false);
        OnJumped?.Invoke();
    }

    private void HandleGravity()
    {
        if (_isGrounded)
        {
            if (_currentVelocity.y <= 0f)
                _currentVelocity.y = -0.1f;
            return;
        }

        _currentVelocity.y += _calculatedGravity * Time.deltaTime;

        if (_currentVelocity.y < 0f)
        {
            float glideMultiplier = _isHoldingJump ? valuesSO.glideResistance : 0f;
            int   fallLimit       = _isHoldingJump ? -4 : -25;

            bool nowGliding = _isHoldingJump;
            if (nowGliding != _isGliding)
            {
                _isGliding = nowGliding;
                OnGlideStateChanged?.Invoke(_isGliding);
            }

            _animController?.SetGliding(_isHoldingJump);

            float nextY = _currentVelocity.y
                        + _calculatedGravity * (valuesSO.fallMultiplier - 1f - glideMultiplier) * Time.deltaTime;

            _currentVelocity.y = Mathf.Clamp(nextY, fallLimit, 50f);
        }
        else if (_currentVelocity.y > 0f && !_isHoldingJump)
        {
            _currentVelocity.y += _calculatedGravity * (valuesSO.lowJumpMultiplier - 1f) * Time.deltaTime;
        }
    }

    private void ApplyVelocityToRigidbody()
    {
        _rb.linearVelocity = _currentVelocity;
    }

    // ── Ground detection ──────────────────────────────────────────────────────

    private void CheckGround()
    {
        Vector2 pos = transform.position;

        _isGrounded =
            RaycastNonTrigger(new Vector2(pos.x - checkSpacing, pos.y - spacingY), Vector2.down) != null
         || RaycastNonTrigger(new Vector2(pos.x, pos.y - spacingY - 0.07f), Vector2.down) != null
         || RaycastNonTrigger(new Vector2(pos.x + checkSpacing, pos.y - spacingY), Vector2.down) != null;

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

    private void UpdateTimers()
    {
        if (_jumpBufferTimer > 0f) _jumpBufferTimer -= Time.deltaTime;

        if (_isGrounded)
            _coyoteTimer = valuesSO.coyoteTime;
        else if (_coyoteTimer > 0f)
            _coyoteTimer -= Time.deltaTime;
    }

    // ── Grounded change notification ──────────────────────────────────────────

    private void NotifyGroundedChange()
    {
        if (_isGrounded == _wasGrounded) return;

        if (_isGrounded)
        {
            AudioManager.Instance.PlaySound("player_land");

            if (_isGliding)
            {
                _isGliding = false;
                OnGlideStateChanged?.Invoke(false);
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