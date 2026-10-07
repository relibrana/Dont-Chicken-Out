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

    [Header("Etiqueta de jugador")]
    [SerializeField, Tooltip("Muestra la etiqueta 'Player N' flotando sobre el pollo.")]
    private bool showNameTag = true;

    [SerializeField, Tooltip("Desplazamiento de la etiqueta respecto al pollo.")]
    private Vector3 nameTagOffset = new Vector3(0f, 1.6f, 0f);

    [SerializeField, Min(0.5f), Tooltip("Tamaño del texto de la etiqueta, en unidades de mundo.")]
    private float nameTagSize = 4f;

    [SerializeField, Tooltip("Recuadro detrás del nombre para que se lea sobre cualquier fondo.")]
    private Color nameTagBackdrop = new Color(0f, 0f, 0f, 0.55f);

    // ── Public state (read by GameManager / UIManager) ────────────────────────

    [NonSerialized] public int     playerIndex;
    [NonSerialized] public int     roundsWon;
    [NonSerialized] public bool    isOnGame;
    [NonSerialized] public Vector2 startPosition;

    public GameStatus GameRank    { get; private set; } = GameStatus.Neutral;

    /// <summary>Sprite for this player's blocks (feathers in their colour). Comes from MaterialsSO.</summary>
    public Sprite     BlockSprite { get; private set; }

    /// <summary>This player's colour for the name tag. Comes from MaterialsSO.</summary>
    public Color LabelColor { get; private set; } = new Color(1f, 1f, 1f, 0f);

    /// <summary>
    /// Main body colour of the chicken in the colour-swap shader. The tag reads
    /// it straight off the material so the label always matches the chicken,
    /// even if Arte retoca los materiales sin avisar.
    /// </summary>
    private static readonly int BodyColorId = Shader.PropertyToID("_ReplacementColor1");

    private PlayerNameTag _nameTag;

    /// <summary>
    /// Colour priority: lo que Arte haya puesto a mano en MaterialsSO, si no el
    /// color del cuerpo en el shader del pollo, y si no la paleta por defecto.
    /// </summary>
    private static Color ResolveLabelColor(PlayerMaterial mats)
    {
        if (mats.labelColor.a > 0f) return mats.labelColor;

        if (mats.playerMat != null && mats.playerMat.HasProperty(BodyColorId))
        {
            Color fromShader = mats.playerMat.GetColor(BodyColorId);
            fromShader.a = 1f;
            return fromShader;
        }

        return new Color(1f, 1f, 1f, 0f); // sin definir: la paleta decide
    }

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
        _inputHandler.OnMovePressed       += HandleMovePress;
        _inputHandler.OnPlaceBlockPressed += HandlePlaceBlock;
        _inputHandler.OnCluckPressed      += HandleCluck;
        _inputHandler.OnPausePressed      += HandlePause;
        _inputHandler.OnBackUIPressed     += HandleBackUI;
    }

    private void UnsubscribeFromInputEvents()
    {
        if (_inputHandler == null) return;

        _inputHandler.OnKickPressed       -= HandleKick;
        _inputHandler.OnMovePressed       -= HandleMovePress;
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
            stuck.OnStrugglePress(fromKick: true, FacingSign);
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

    /// <summary>
    /// Movement presses only matter here while stuck in moco: they feed the
    /// struggle bar (less than a kick) so the player is never left mashing a
    /// single button. Outside that state the axis is read by PlayerMovement.
    /// </summary>
    private void HandleMovePress(float direction)
    {
        if (IsOnPause()) return;

        if (TryGetComponent(out MocoStuckState stuck) && stuck.IsActive)
            stuck.OnStrugglePress(fromKick: false, direction);
    }

    /// <summary>
    /// The chicken's visual rig. Items that need to shake or nudge the chicken
    /// move THIS and not the body: the body is driven by the Rigidbody2D, and
    /// while stuck in moco it is frozen on purpose.
    /// </summary>
    public Transform VisualRoot => animController != null ? animController.transform : transform;

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

        // Aquí y no en Awake: la etiqueta necesita el índice y el color, y los
        // dos los pone GameManager.AddPlayer justo antes de esta llamada.
        CreateNameTag();
    }

    private void CreateNameTag()
    {
        if (!showNameTag || _nameTag != null) return;

        _nameTag = PlayerNameTag.Create(
            this,
            $"Player {playerIndex + 1}",
            PlayerNameTag.ResolveColor(LabelColor, playerIndex),
            nameTagOffset,
            nameTagSize,
            nameTagBackdrop);
    }

    /// <summary>Entry point for the future name-customisation screen.</summary>
    public void SetDisplayName(string displayName) => _nameTag?.SetLabel(displayName);

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

    /// <summary>Sets the player material on all sprite renderers and this player's block sprite.</summary>
    public void SetMaterials(PlayerMaterial mats)
    {
        BlockSprite = mats.blockSprite;
        LabelColor  = ResolveLabelColor(mats);
        if (_blockHandler.CurrentBlock is BlockScript block)
            block.SetBlockSprite(BlockSprite);

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

    /// <summary>
    /// Grants extra mid-air jumps. Used by the teleporte to give the user a
    /// single courtesy jump after reappearing in the air.
    /// </summary>
    public void GrantAirJump(int count = 1) => _movement.AirJumpsRemaining += count;

    /// <summary>
    /// Rumbles this player's gamepad. No-op for keyboard players, so callers
    /// never have to check which scheme somebody is on.
    /// Motors are always stopped on a timer: a rumble left running because the
    /// round ended is the worst bug this feature can have.
    /// </summary>
    public void Rumble(float lowFrequency, float highFrequency, float duration)
    {
        Gamepad pad = FindGamepad();
        if (pad == null) return;

        pad.SetMotorSpeeds(lowFrequency, highFrequency);

        DOVirtual.DelayedCall(duration, () =>
        {
            if (pad.added) pad.SetMotorSpeeds(0f, 0f);
        }, false);
    }

    /// <summary>Stops any rumble on this player's gamepad right now.</summary>
    public void StopRumble() => FindGamepad()?.SetMotorSpeeds(0f, 0f);

    private Gamepad FindGamepad()
    {
        if (playerInput == null) return null;

        foreach (InputDevice device in playerInput.devices)
        {
            if (device is Gamepad gamepad) return gamepad;
        }

        return null;
    }

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