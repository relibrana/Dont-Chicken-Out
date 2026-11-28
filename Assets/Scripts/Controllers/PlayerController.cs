using System;
using System.Collections.Generic;
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

    [SerializeField, Tooltip("Contenedor del dibujo del pollo ('Rig'). Lo que escalan/sacuden los ítems; vacío = el objeto del Animator (rig viejo).")]
    private Transform visualRoot;

    [Header("Etiqueta de jugador")]
    [SerializeField, Tooltip("Muestra la etiqueta 'Player N' flotando sobre el pollo.")]
    private bool showNameTag = true;

    [SerializeField, Tooltip("Desplazamiento de la etiqueta respecto al pollo.")]
    private Vector3 nameTagOffset = new Vector3(0f, 1.6f, 0f);

    [SerializeField, Min(0.5f), Tooltip("Tamaño del texto de la etiqueta, en unidades de mundo.")]
    private float nameTagSize = 4f;

    [SerializeField, Tooltip("Recuadro detrás del nombre para que se lea sobre cualquier fondo.")]
    private Color nameTagBackdrop = new Color(0f, 0f, 0f, 0.55f);

    [SerializeField, Tooltip("Dónde va el texto del power-up activo (Metal Body, Double Jump…): a la altura de la papa caliente.")]
    private Vector3 stateTagOffset = new Vector3(0f, 2.25f, 0f);

    // ── Public state (read by GameManager / UIManager) ────────────────────────

    [NonSerialized] public int     playerIndex;
    [NonSerialized] public int     roundsWon;
    [NonSerialized] public bool    isOnGame;
    [NonSerialized] public Vector2 startPosition;

    /// <summary>
    /// False from death until the next round reset (see SetAlive). A dead
    /// chicken is never a valid target: it also has isOnGame = false.
    /// </summary>
    public bool IsAlive { get; private set; } = true;

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
    /// Colour priority: lo que Arte haya puesto a mano en MaterialsSO; si no, con
    /// el pollo nuevo (nov 2026), el color de su etiqueta de la Sprite Library
    /// (P1 amarillo, P2 azul, P3 verde, P4 rosa); si no, el color del cuerpo en
    /// el shader del pollo viejo, y si no la paleta por defecto.
    /// </summary>
    public static Color ResolveLabelColor(PlayerMaterial mats, int slot, bool usesSkin)
    {
        if (mats != null && mats.labelColor.a > 0f) return mats.labelColor;

        if (usesSkin) return ChickenSkin.DisplayColor(slot);

        if (mats != null && mats.playerMat != null && mats.playerMat.HasProperty(BodyColorId))
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
    private Rigidbody2D        _rb;

    // Renderers hidden on death and their state before it (see SetAlive).
    private Renderer[] _renderers;
    private bool[]     _rendererWasEnabled;

    private bool  _isGliding;
    private float _nextKickTime;

    /// <summary>
    /// When the last kick was pressed. The hitbox opens later, on an animation
    /// event (Anim_Kick, 0.083 s), so a kick can be "in the air" for a moment:
    /// the hot potato waits for it before going off.
    /// </summary>
    public float LastKickTime { get; private set; } = float.NegativeInfinity;
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
        _rb           = GetComponent<Rigidbody2D>();

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

    // ── Registry ──────────────────────────────────────────────────────────────

    private static readonly List<PlayerController> _active = new();

    /// <summary>
    /// Every active chicken, kept by OnEnable/OnDisable. Use this instead of
    /// FindObjectsByType: the camera rig asks every frame, and a scene scan
    /// there walks the whole scene and allocates an array each time.
    /// Same set FindObjectsByType returned (active and enabled only).
    /// </summary>
    public static IReadOnlyList<PlayerController> Active => _active;

    private void OnEnable()  => _active.Add(this);
    private void OnDisable() => _active.Remove(this);

    // Enter Play Mode without domain reload keeps statics alive between sessions.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => _active.Clear();

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
        _inputHandler.OnUseItemPressed    += HandleUseItem;
        _inputHandler.OnJumpPressed       += HandleJumpPress;
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
        _inputHandler.OnUseItemPressed    -= HandleUseItem;
        _inputHandler.OnJumpPressed       -= HandleJumpPress;
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
        if (!IsAlive) return;

        // Stuck in moco: the kick press becomes the struggle input.
        if (TryGetComponent(out MocoStuckState stuck) && stuck.IsActive)
        {
            stuck.OnStrugglePress(fromKick: true, FacingSign);
            return;
        }

        if (IsMovementLocked) return;

        // GDD (patear): a minimum time between two kicks of the same chicken.
        if (Time.time < _nextKickTime) return;
        _nextKickTime = Time.time + valuesSO.kickCooldown;
        LastKickTime  = Time.time;

        animController.PlayKick();

        bool superKick = TryGetComponent(out SuperKickState superKickState) && superKickState.IsActive;
        SimEvents.Raise(superKick ? SimEvent.PlayerSuperKicked : SimEvent.PlayerKicked, transform.position, this);

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
    /// The chicken's visual rig. Items that need to shake, nudge or scale the
    /// chicken (moco, teleport) touch THIS and not the body: the body is driven
    /// by the Rigidbody2D (frozen on purpose while stuck in moco), its X scale is
    /// the facing sign and it carries the colliders.
    /// The new rig animates its own root (squash, hop), so the visual root is
    /// the "Rig" holder above it, set in the prefab; the old rig falls back to
    /// the Animator's object as before.
    /// </summary>
    public Transform VisualRoot =>
        visualRoot != null ? visualRoot
        : animController != null ? animController.transform
        : transform;

    private void HandlePlaceBlock()
    {
        if (IsOnPause()) return;
        if (!isOnGame) return;
        if (IsMovementLocked) return;

        // Once the round is decided nobody places anything until the next one
        // starts (design, oct 2026). Covers items used with this button too
        // (POW, teleport, throws).
        if (GameManager.instance != null && !GameManager.instance.IsRoundLive) return;

        _blockHandler.TryPlaceBlock();
    }

    /// <summary>Stuck in moco, a jump press is a struggle press like a kick (GDD: salto o patada).</summary>
    private void HandleJumpPress()
    {
        if (IsOnPause() || !IsAlive) return;
        if (TryGetComponent(out MocoStuckState stuck) && stuck.IsActive)
            stuck.OnStrugglePress(fromKick: true, FacingSign);
    }

    /// <summary>
    /// Own item button (GDD): uses the item in the slot, whenever the player
    /// wants, unless something stops them acting (stunned, stuck in moco).
    /// </summary>
    private void HandleUseItem()
    {
        if (IsOnPause()) return;
        if (!isOnGame || !IsAlive) return;
        if (IsMovementLocked) return;
        if (GameManager.instance != null && !GameManager.instance.IsRoundLive) return;

        ItemSlot.TryUse(GetBlockPosition(), FacingSign);
    }

    /// <summary>
    /// Round reset: the drawing goes back to a clean idle chicken (no stun
    /// stars, no metal, normal size), whatever state the last round left.
    /// </summary>
    public void ResetVisuals()
    {
        if (animController != null) animController.ResetToIdle();

        ChickenSkin skin = Skin;
        if (skin != null) skin.SetMetal(false);
    }

    /// <summary>The manual item the chicken carries (one at a time).</summary>
    public PlayerItemSlot ItemSlot
    {
        get
        {
            if (_itemSlot == null && !TryGetComponent(out _itemSlot))
                _itemSlot = gameObject.AddComponent<PlayerItemSlot>();
            return _itemSlot;
        }
    }
    private PlayerItemSlot _itemSlot;

    /// <summary>Called by ItemCapsule: a manual item goes to the slot. False if the slot was taken (item lost).</summary>
    public bool StoreItem(HoldableItem item) => ItemSlot.TryStore(item);

    private void HandleCluck()
    {
        if (IsOnPause()) return;
        // Dead chickens do not cluck (design, oct 2026).
        if (!IsAlive) return;

        _cluckSystem?.OnCluckPressed();
    }

    private void HandlePause()
    {
        if (!GameManager.instance.CanPause) return;

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

    // ── Active power-up label ─────────────────────────────────────────────────
    // Placeholder until item states get their own art (design, oct 2026): the
    // name of every active power-up over the chicken's head, one per line, in
    // a single tag so two of them never overlap. Driven by PlayerItemState.

    private PlayerNameTag _stateTag;
    private readonly List<string> _stateLabels = new();

    public void ShowStateLabel(string label)
    {
        if (string.IsNullOrEmpty(label) || _stateLabels.Contains(label)) return;
        _stateLabels.Add(label);
        RefreshStateTag();
    }

    public void HideStateLabel(string label)
    {
        if (!_stateLabels.Remove(label)) return;
        RefreshStateTag();
    }

    private void RefreshStateTag()
    {
        if (_stateLabels.Count == 0)
        {
            if (_stateTag != null) Destroy(_stateTag.gameObject);
            _stateTag = null;
            return;
        }

        string text = string.Join("\n", _stateLabels);

        if (_stateTag == null)
            _stateTag = PlayerNameTag.Create(this, text, Color.white, stateTagOffset, nameTagSize, nameTagBackdrop);
        else
            _stateTag.SetLabel(text);
    }

    /// <summary>Triggers the player death flow.</summary>
    public void OnDeath() => onDeath?.Invoke(this);

    /// <summary>
    /// Takes the chicken out of play (death) or brings it back (round reset).
    /// The GameObject stays active on purpose: deactivating it unpairs the
    /// PlayerInput from its gamepad. Dead = no physics (colliders out of every
    /// query), no movement tick, nothing drawn, isOnGame false. With Fusion 2
    /// the player object also survives death, so this becomes a replicated flag.
    /// After reviving, place the chicken with TeleportTo: it stays where it died.
    /// </summary>
    public void SetAlive(bool alive)
    {
        if (IsAlive == alive) return;
        IsAlive = alive;

        if (!alive) isOnGame = false;

        _rb.simulated     = alive;
        _movement.enabled = alive;
        SetRenderersVisible(alive);
    }

    /// <summary>
    /// Between leaving and arriving in a teleport (nov 2026): still alive, but
    /// out of the world — no physics, so nothing can kick, hit or kill it, and
    /// its tag and item icon hide. The rig is hidden by TeleportState itself.
    /// </summary>
    public bool InTransit { get; private set; }

    public void SetInTransit(bool inTransit)
    {
        InTransit = inTransit;
        if (IsAlive) _rb.simulated = !inTransit;
    }

    private void SetRenderersVisible(bool visible)
    {
        if (!visible)
        {
            // Remember what was on, so reviving does not switch on renderers
            // that were off by design.
            _renderers          = GetComponentsInChildren<Renderer>(includeInactive: true);
            _rendererWasEnabled = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _rendererWasEnabled[i] = _renderers[i].enabled;
                _renderers[i].enabled  = false;
            }
            return;
        }

        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].enabled = _rendererWasEnabled[i];
        }
        _renderers = null;
    }

    /// <summary>
    /// Applies an external impulse to the player (explosion, spring, etc.).
    /// Optionally plays hit animations if treated as a kick.
    /// </summary>
    public void AddImpulse(Vector2 impulse, bool isKick = false, bool resetSpeed = false, bool ignoreImmunity = false)
    {
        if (ImpulseImmune && !ignoreImmunity) return;

        _movement.AddImpulse(impulse, resetSpeed);

        if (!isKick) return;

        if (Mathf.Sign(impulse.x) == Mathf.Sign(transform.localScale.x))
            animController.PlayHitBack();
        else
            animController.PlayHitFront();
    }

    /// <summary>
    /// Applies this player's look: block sprite, chicken colour and name-tag colour.
    /// With the new rig (ChickenSkin) the chicken is coloured by Sprite Library
    /// label and keeps the default sprite material; the old colour-swap material
    /// only goes on renderers outside the rig (and on the old rig if still mounted).
    /// </summary>
    public void SetMaterials(PlayerMaterial mats, int slot)
    {
        ChickenSkin skin = Skin;

        BlockSprite = mats.blockSprite;
        LabelColor  = ResolveLabelColor(mats, slot, skin != null);
        if (_blockHandler.CurrentBlock is BlockScript block)
            block.SetBlockSprite(BlockSprite);

        if (skin != null) skin.SetColor(slot);

        var renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in renderers)
        {
            if (skin != null && sr.transform.IsChildOf(skin.transform)) continue;
            sr.material = mats.playerMat;
        }

        playerMat = mats.playerMat;
    }

    /// <summary>Look of the new chicken rig (colour, metal, death copy). Null on the old rig.</summary>
    public ChickenSkin Skin
    {
        get
        {
            if (_skin == null && animController != null)
            {
                _skin = animController.GetComponentInParent<ChickenSkin>();
                if (_skin == null) _skin = animController.GetComponentInChildren<ChickenSkin>(true);
            }
            return _skin != null ? _skin : null;
        }
    }
    private ChickenSkin _skin;

    /// <summary>Animation driver, for item states that play their own clips (Stun).</summary>
    public PlayerAnimController AnimController => animController;

    /// <summary>Updates the player's rank in the current game session.</summary>
    public void SetGameRank(GameStatus rank) => GameRank = rank;

    /// <summary>Returns the world position used to spawn/hold blocks.</summary>
    public Vector2 GetBlockPosition() => _blockHandler.GetBlockSpawnPosition();

    /// <summary>
    /// Instantly moves the player (teleport item, spawn, death zone). The only
    /// safe way to reposition a chicken: see PlayerMovement.Teleport.
    /// </summary>
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
    public void DropBlock()
    {
        _blockHandler.DropBlock();
        // The item is lost with the chicken (GDD) and never crosses rounds.
        if (_itemSlot != null) _itemSlot.Clear();
    }

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

    // ── Step sound (called by animation event) ────────────────────────────────

    public void StepSound() => AudioManager.Instance.PlaySoundAt("player_steps", transform.position);

    /// <summary>Delegated from PlayerMovement. Used by CinemachineVerticalRig2D.</summary>
    public bool IsGrounded => _movement.IsGrounded;

    /// <summary>Velocity of the last tick (before any contact this tick). Used by the tire to send you back.</summary>
    public Vector2 Velocity => _movement.CurrentState.velocity;

    /// <summary>Standing only on other chickens. Delegated from PlayerMovement.</summary>
    public bool IsOnPlayer => _movement.IsOnPlayer;

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