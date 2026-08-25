using System;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Central coordinator for the player. Owns player state and public API.
/// Delegates input reading to PlayerInputHandler, physics to PlayerMovement,
/// block lifecycle to PlayerBlockHandler, and animation to PlayerAnimController.
/// GameManager and other external systems interact exclusively through this class.
/// </summary>
public sealed class PlayerController : MonoBehaviour, IKickable
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private PlatformerValuesSO valuesSO;
    [SerializeField] private KickCollider kickCollider;
    [SerializeField] private PlayerAnimController animController;
    [SerializeField] private Transform glideFlapOrigin;

    // ── Public state (read by GameManager / UIManager) ────────────────────────

    [NonSerialized] public int     playerIndex;
    [NonSerialized] public int     roundsWon;
    [NonSerialized] public bool    isOnGame;
    [NonSerialized] public Vector2 startPosition;

    public GameStatus GameRank    { get; private set; } = GameStatus.Neutral;
    public Material   HayMaterial { get; private set; }

    // ── Item state hooks ──────────────────────────────────────────────────────

    /// <summary>
    /// True while any item state (stun, moco) is locking the player's actions.
    /// While locked, the movement tick receives a neutral InputPayload and
    /// kick/place inputs are swallowed. Counter-based so overlapping states
    /// compose without stepping on each other.
    /// </summary>
    public bool IsMovementLocked => _movementLocks > 0;

    /// <summary>While true, incoming kicks and impulses are ignored (pollo metálico).</summary>
    public bool ImpulseImmune { get; set; }

    /// <summary>Facing sign of the sprite: 1 = right, -1 = left. Used by throwable items.</summary>
    public float FacingSign => Mathf.Sign(transform.localScale.x);

    private int _movementLocks;

    public void PushMovementLock() => _movementLocks++;
    public void PopMovementLock()  => _movementLocks = Mathf.Max(0, _movementLocks - 1);

    // ── External callbacks (set by GameManager) ───────────────────────────────

    public Action<PlayerController> onDeath;

    // ── Private component refs ────────────────────────────────────────────────

    private PlayerInputHandler _inputHandler;
    private PlayerMovement     _movement;
    private PlayerBlockHandler _blockHandler;
    private CluckSystem        _cluckSystem;

    private bool  _isGliding;
    private float _flapTimer;

    // ── Input / scheme ────────────────────────────────────────────────────────

    public PlayerInput playerInput { get; private set; }
    private string _assignedScheme;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        playerInput   = GetComponent<PlayerInput>();
        _inputHandler = GetComponent<PlayerInputHandler>();
        _movement     = GetComponent<PlayerMovement>();
        _blockHandler = GetComponent<PlayerBlockHandler>();
        _cluckSystem  = GetComponent<CluckSystem>();

        _movement.SetAnimController(animController);
        _blockHandler.Initialize(valuesSO, animController);

        kickCollider.forceDirection   = valuesSO.kickForce;
        kickCollider.playerController = this;

        SubscribeToInputEvents();
        SubscribeToMovementEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromInputEvents();
        UnsubscribeFromMovementEvents();
    }

    private void Update()
    {
        _movement.ProcessInput(IsMovementLocked ? default : _inputHandler.CurrentInput);

        if (_isGliding)
            TickFlapVFX();

        // Only the "is this player playing" flag is driven from here.
        // The placement cooldown lives inside PlayerBlockHandler.IsAvailable and
        // must not be touched every frame, or the cooldown never applies.
        _blockHandler.IsInGame = isOnGame;
    }

    private void TickFlapVFX()
    {
        if (FeatherVFXController.Instance == null) return;

        _flapTimer -= Time.deltaTime;
        if (_flapTimer > 0f) return;

        _flapTimer = FeatherVFXController.Instance.FlapInterval;

        Vector2 origin = glideFlapOrigin != null
            ? (Vector2)glideFlapOrigin.position
            : (Vector2)transform.position;

        FeatherVFXController.Instance.EmitFlap(origin);
    }

    // ── Input event routing ───────────────────────────────────────────────────

    private void SubscribeToInputEvents()
    {
        _inputHandler.OnKickPressed       += HandleKick;
        _inputHandler.OnPlaceBlockPressed += HandlePlaceBlock;
        _inputHandler.OnCluckPressed      += HandleCluck;
        _inputHandler.OnPausePressed      += HandlePause;
        _inputHandler.OnBackUIPressed     += HandleBackUI;
    }

    private void UnsubscribeFromInputEvents()
    {
        if (_inputHandler == null) return;

        _inputHandler.OnKickPressed       -= HandleKick;
        _inputHandler.OnPlaceBlockPressed -= HandlePlaceBlock;
        _inputHandler.OnCluckPressed      -= HandleCluck;
        _inputHandler.OnPausePressed      -= HandlePause;
        _inputHandler.OnBackUIPressed     -= HandleBackUI;
    }

    private void SubscribeToMovementEvents()
    {
        _movement.OnJumped            += HandleJumpVFX;
        _movement.OnGlideStateChanged += HandleGlideStateChanged;
    }

    private void UnsubscribeFromMovementEvents()
    {
        if (_movement == null) return;

        _movement.OnJumped            -= HandleJumpVFX;
        _movement.OnGlideStateChanged -= HandleGlideStateChanged;
    }

    private void HandleKick()
    {
        if (IsOnPause()) return;

        // Stuck in moco: the kick press becomes the struggle input.
        if (TryGetComponent(out MocoStuckState stuck) && stuck.IsActive)
        {
            stuck.OnStrugglePress();
            return;
        }

        if (IsMovementLocked) return;

        animController.PlayKick();
        AudioManager.Instance.PlaySound("player_kick");

        FeatherVFXController.Instance?.EmitKickDealt(
            transform.position,
            Vector2.right * transform.localScale.x
        );
    }

    private void HandlePlaceBlock()
    {
        if (IsOnPause()) return;
        if (!isOnGame) return;
        if (IsMovementLocked) return;

        _blockHandler.TryPlaceBlock();
    }

    private void HandleCluck()
    {
        if (IsOnPause()) return;

        _cluckSystem?.OnCluckPressed();
    }

    private void HandlePause()
    {
        if (GameManager.instance.gameState != GameState.Game) return;

        PauseManager.instance.Pause(this);
    }

    private void HandleBackUI()
    {
        PauseManager.instance.Resume();
    }

    private bool IsOnPause() => PauseManager.instance.isPaused;

    // ── IKickable ─────────────────────────────────────────────────────────────

    public void ReceiveKick(Vector2 kickImpulse)
    {
        if (ImpulseImmune) return;

        _movement.AddImpulse(kickImpulse);

        if (Mathf.Sign(kickImpulse.x) == Mathf.Sign(transform.localScale.x))
            animController.PlayHitBack();
        else
            animController.PlayHitFront();

        FeatherVFXController.Instance?.EmitKickReceived(transform.position, kickImpulse);
    }

    // ── VFX handlers (movement events) ───────────────────────────────────────

    private void HandleJumpVFX()
    {
        FeatherVFXController.Instance?.EmitJump(transform.position);
    }

    private void HandleGlideStateChanged(bool isGliding)
    {
        _isGliding = isGliding;

        if (isGliding)
            _flapTimer = 0f; // Emit on the very first frame of glide.
    }

    // ── Public API (called by GameManager) ────────────────────────────────────

    /// <summary>Called by GameManager after playerIndex is assigned.</summary>
    public void OnPlayerIndexAssigned()
    {
        _cluckSystem?.SetPlayerIndex(playerIndex);
    }

    /// <summary>Triggers the player death flow.</summary>
    public void OnDeath() => onDeath?.Invoke(this);

    /// <summary>
    /// Applies an external impulse to the player (explosion, spring, etc.).
    /// Optionally plays hit animations if treated as a kick.
    /// </summary>
    public void AddImpulse(Vector2 impulse, bool isKick = false, bool resetSpeed = false)
    {
        if (ImpulseImmune) return;

        _movement.AddImpulse(impulse, resetSpeed);

        if (!isKick) return;

        if (Mathf.Sign(impulse.x) == Mathf.Sign(transform.localScale.x))
            animController.PlayHitBack();
        else
            animController.PlayHitFront();
    }

    /// <summary>Sets player and hay materials on all sprite renderers.</summary>
    public void SetMaterials(PlayerMaterial mats)
    {
        HayMaterial = mats.hayMat;
        _blockHandler.CurrentBlock?.SetMaterial(HayMaterial);

        var renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in renderers)
            sr.material = mats.playerMat;

        playerMat = mats.playerMat;
    }

    /// <summary>Updates the player's rank in the current game session.</summary>
    public void SetGameRank(GameStatus rank) => GameRank = rank;

    /// <summary>Returns the world position used to spawn/hold blocks.</summary>
    public Vector2 GetBlockPosition() => _blockHandler.GetBlockSpawnPosition();

    /// <summary>Instantly moves the player (teleport item).</summary>
    public void TeleportTo(Vector2 position) => _movement.Teleport(position);

    /// <summary>Drops the currently held block. Called on death and reset.</summary>
    public void DropBlock() => _blockHandler.DropBlock();

    /// <summary>
    /// Discards the current block and assigns a new one.
    /// Called by ItemCapsule when the capsule breaks.
    /// </summary>
    public void SwapBlock(HoldableItem newBlock) => _blockHandler.SwapBlock(newBlock);

    // ── Input scheme (called by PlayersManager) ───────────────────────────────

    public void OnAssignedScheme(string schemeName)
    {
        _assignedScheme = schemeName;
        playerInput.SwitchCurrentControlScheme(_assignedScheme, playerInput.devices.ToArray());
        playerInput.SwitchCurrentActionMap("Player");
    }

    public void OnPlayerLeft(PlayerInput input)
    {
        if (_assignedScheme != "Gamepad")
            GameManager.instance.FreeKeyboardScheme(_assignedScheme);
    }

    // ── Step sound (called by animation event) ────────────────────────────────

    public void StepSound() => AudioManager.Instance.MakeStepSound();

    /// <summary>Delegated from PlayerMovement. Used by CinemachineVerticalRig2D.</summary>
    public bool IsGrounded => _movement.IsGrounded;

    // ── Editor ────────────────────────────────────────────────────────────────

    [NonSerialized] public Material playerMat;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = GameRank == GameStatus.Winning ? Color.red :
                       GameRank == GameStatus.Losing  ? Color.green : Color.yellow;

        Vector3 labelPos = transform.position + Vector3.up * 1f;
        Gizmos.DrawWireSphere(labelPos, 0.5f);
        UnityEditor.Handles.Label(labelPos + Vector3.up, $"Status: {GameRank}");
    }
#endif
}