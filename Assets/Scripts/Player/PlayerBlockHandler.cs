using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the full lifecycle of the block the player is currently holding.
/// Depends on PlayerMovement for grounded state and position.
/// Depends on PlayerController for game rank and materials.
/// Has no knowledge of input, physics, or UI.
/// </summary>
public sealed class PlayerBlockHandler : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private Transform blockPosition;
    [SerializeField] private Transform blockCheckPoint;
    [SerializeField] private LayerMask blockLayer;
    [SerializeField] private LayerMask layersToDetect;
    [SerializeField] private float blockDetectDistance = 0.1f;

    [Header("Empujar (GDD)")]
    [SerializeField, Min(0f), Tooltip("Cuánto hay que insistir contra una pieza antes de que empiece el empuje. Muy corto: lo justo para no empujar sin querer.")]
    private float pushThreshold = 0.12f;

    [SerializeField, Min(0f), Tooltip("Velocidad a la que se mueve lo empujado (escala con la variable Empuje de la progresión).")]
    private float pushSpeed = 3f;

    [SerializeField, Min(1f), Tooltip("El pollo metálico empuja piezas más rápido (GDD).")]
    private float metalPushMultiplier = 1.5f;

    private float _pushInsist;
    private PlayerInputHandler _inputHandler;

    // ── Public API ────────────────────────────────────────────────────────────

    public HoldableItem CurrentBlock { get; private set; }
    public bool IsBlockUnavailable   { get; private set; }
    public bool CanPlaceBlock        { get; private set; }

    /// <summary>
    /// False while the placement cooldown is running. Owned by this class only —
    /// nothing outside may write it, or the cooldown is lost.
    /// </summary>
    public bool IsAvailable { get; private set; } = true;

    /// <summary>
    /// True while the player is taking part in a round. Set by PlayerController.
    /// Kept separate from IsAvailable so the per-frame "player is playing" flag
    /// cannot overwrite the placement cooldown.
    /// </summary>
    public bool IsInGame { get; set; }

    // ── Private refs ──────────────────────────────────────────────────────────

    private PlayerMovement      _movement;
    private PlayerAnimController _animController;
    private PlayerController    _controller;

    private PlatformerValuesSO _values;

    // Both delays count down on the simulation tick (FixedUpdate), not with
    // DOVirtual: a pending tween could not be cancelled, so a block swapped in
    // mid-delay inherited the old countdown and became placeable early.
    private bool  _generationPending;
    private float _generationTimer;
    private float _placementTimer;

    // ── Progression-scaled values ─────────────────────────────────────────────

    /// <summary>Cooldown between placements, scaled by the current phase.</summary>
    private float PlacementCooldown => _values != null
        ? _values.blockPlacementCD * ProgressionManager.Get(ProgressionVar.PlacementCadence)
        : 0f;

    /// <summary>Delay before a freshly received block can be placed, scaled by the current phase.</summary>
    private float GenerationDelay => _values != null
        ? _values.blockGenerationDelay * ProgressionManager.Get(ProgressionVar.BlockGeneration)
        : 0.3f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _movement     = GetComponent<PlayerMovement>();
        _controller   = GetComponent<PlayerController>();
        _inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void Update()
    {
        if (!IsInGame) return;

        // The push animation keeps updating during the placement cooldown —
        // the player is still leaning on the block either way.
        CheckPushing();

        if (!IsAvailable) return;

        HandleBlockLogic();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        TickPush(dt);

        if (_generationPending)
        {
            _generationTimer -= dt;
            if (_generationTimer <= 0f)
            {
                _generationPending = false;
                CanPlaceBlock      = true;
            }
        }

        if (!IsAvailable)
        {
            _placementTimer -= dt;
            if (_placementTimer <= 0f)
                IsAvailable = true;
        }
    }

    // ── Public methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Discards the current block and assigns a new one already initialized.
    /// Called by ItemCapsule when the capsule breaks and gives the player a new item.
    /// </summary>
    public void SwapBlock(HoldableItem newBlock)
    {
        DropBlock();
        CurrentBlock = newBlock;
        CurrentBlock.SetOwner(_controller);
        CurrentBlock.StartHold();
        StartGenerationDelay();
    }

    /// <summary>
    /// Attempts to place the current block. Called by PlayerController when
    /// PlaceBlock input is received.
    /// </summary>
    public void TryPlaceBlock()
    {
        if (CurrentBlock == null) return;

        // Throwables launch from anywhere: only the generation delay applies.
        if (CurrentBlock.BypassPlacementChecks)
        {
            if (!CanPlaceBlock) return;
        }
        else if (!_movement.IsGrounded || !CanPlaceBlock || IsBlockUnavailable)
        {
            if (!_movement.IsGrounded || !CanPlaceBlock)
                return;

            SimEvents.Raise(SimEvent.BlockPlaceRejected, transform.position, this);
            return;
        }

        _animController.PlayPlace();
        CurrentBlock.PlaceHoldable();
        CurrentBlock = null;
        CanPlaceBlock      = false;
        _generationPending = false;
        IsAvailable        = false;
        _placementTimer    = PlacementCooldown;
    }

    /// <summary>
    /// Drops and disables the current block without placing it.
    /// Called by PlayerController on death or game reset.
    /// </summary>
    public void DropBlock()
    {
        if (CurrentBlock == null) return;

        CurrentBlock.gameObject.SetActive(false);
        CurrentBlock       = null;
        CanPlaceBlock      = false;
        _generationPending = false;
    }

    /// <summary>
    /// Called by PlayerController on Awake to inject dependencies and settings.
    /// The ScriptableObject is kept as a live reference (not copied) so the
    /// progression system can scale its values every frame.
    /// </summary>
    public void Initialize(PlatformerValuesSO values, PlayerAnimController animController)
    {
        _values         = values;
        _animController = animController;
    }

    // ── Private logic ─────────────────────────────────────────────────────────

    private void HandleBlockLogic()
    {
        if (CurrentBlock == null)
        {
            FetchNewBlock();
            return;
        }

        UpdateBlockTransform();
        UpdateBlockAvailability();
    }

    private void FetchNewBlock()
    {
        PoolingManager poolManager = GameManager.instance.poolManager;
        int third                  = GameplayCamera.ViewThird(transform.position.y);
        GameObject randomBlock     = poolManager.GetPooledBlock(blockPosition.position, third);

        CurrentBlock = randomBlock.GetComponent<HoldableItem>();
        CurrentBlock.SetOwner(_controller);
        if (CurrentBlock is BlockScript block)
            block.SetBlockSprite(_controller.BlockSprite);
        CurrentBlock.StartHold();

        StartGenerationDelay();
    }

    /// <summary>A freshly received block becomes placeable after GenerationDelay.</summary>
    private void StartGenerationDelay()
    {
        CanPlaceBlock      = false;
        _generationPending = true;
        _generationTimer   = GenerationDelay;
    }

    private void UpdateBlockTransform()
    {
        CurrentBlock.transform.position   = blockPosition.position;
        CurrentBlock.transform.localScale  = new Vector3(transform.lossyScale.x, 1f, 1f);
    }

    private void UpdateBlockAvailability()
    {
        if (CurrentBlock.BypassPlacementChecks)
        {
            IsBlockUnavailable      = false;
            CurrentBlock.overlapping = false;
            return;
        }

        // GDD (colocar pieza): only still and supported. Walking or sliding to a
        // stop shows the preview red and placing does nothing.
        IsBlockUnavailable = !_movement.IsGrounded || _movement.SpeedX > StillSpeed;

        if (!IsBlockUnavailable)
        {
            foreach (Collider2D col in CurrentBlock.GetColliders())
            {
                if (CheckOverlapping(col))
                {
                    IsBlockUnavailable = true;
                    break;
                }
            }
        }

        CurrentBlock.overlapping = IsBlockUnavailable;
    }

    // Runs every frame for every sub-block of the held piece: one reused buffer
    // instead of a new List per call (that was ~16 lists a frame with 4 players).
    private readonly List<Collider2D> _overlapResults = new();

    /// <summary>Horizontal speed under which the chicken counts as still for placing.</summary>
    private const float StillSpeed = 0.25f;

    private bool CheckOverlapping(Collider2D collider)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(layersToDetect);
        filter.useTriggers = false;

        int count = collider.Overlap(filter, _overlapResults);

        for (int i = 0; i < count; i++)
        {
            if (_overlapResults[i] != collider)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Drives the push animation bool. There is no push force by design:
    /// pushing is the player walking into a block, so it already scales with
    /// the lateral speed progression variable.
    /// </summary>
    private void CheckPushing()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            blockCheckPoint.position,
            transform.right * transform.localScale.x,
            blockDetectDistance,
            blockLayer
        );

        _animController.SetTouchingBlock(hit.collider != null);
    }

    /// <summary>
    /// GDD (empujar): walking into a placed piece and holding the direction a
    /// moment turns into a push. Renewed every tick while the player insists,
    /// so it stops as soon as they let go or turn round.
    /// </summary>
    private void TickPush(float dt)
    {
        if (_controller == null || _inputHandler == null || !_controller.isOnGame || _controller.IsMovementLocked)
        {
            _pushInsist = 0f;
            return;
        }

        float move   = _inputHandler.CurrentInput.moveDirection;
        float facing = Mathf.Sign(transform.localScale.x);
        if (Mathf.Abs(move) < 0.5f || Mathf.Sign(move) != facing)
        {
            _pushInsist = 0f;
            return;
        }

        RaycastHit2D hit = Physics2D.Raycast(blockCheckPoint.position, Vector2.right * facing, blockDetectDistance, blockLayer);
        BlockScript piece = hit.collider != null ? hit.collider.GetComponentInParent<BlockScript>() : null;
        if (piece == null || !piece.IsPlacedPiece)
        {
            _pushInsist = 0f;
            return;
        }

        _pushInsist += dt;
        if (_pushInsist < pushThreshold) return;

        float speed = pushSpeed * ProgressionManager.Get(ProgressionVar.Push);
        if (_controller.TryGetComponent(out MetalChickenState metal) && metal.IsActive) speed *= metalPushMultiplier;

        BlockScript.PushGroup(piece, facing, speed, dt * 2.5f);
    }

    /// <summary>Returns the world position where a new block should be spawned.</summary>
    public Vector2 GetBlockSpawnPosition() => blockPosition.position;
}