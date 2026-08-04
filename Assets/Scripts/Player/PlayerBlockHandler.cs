using System.Collections.Generic;
using DG.Tweening;
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
        _movement   = GetComponent<PlayerMovement>();
        _controller = GetComponent<PlayerController>();
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

    // ── Public methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Discards the current block and assigns a new one already initialized.
    /// Called by ItemCapsule when the capsule breaks and gives the player a new item.
    /// </summary>
    public void SwapBlock(HoldableItem newBlock)
    {
        DropBlock();
        CurrentBlock = newBlock;
        CurrentBlock.StartHold();
        CanPlaceBlock = false;
        DOVirtual.DelayedCall(GenerationDelay, () => CanPlaceBlock = true, false);
    }

    /// <summary>
    /// Attempts to place the current block. Called by PlayerController when
    /// PlaceBlock input is received.
    /// </summary>
    public void TryPlaceBlock()
    {
        if (!_movement.IsGrounded || !CanPlaceBlock || IsBlockUnavailable)
        {
            if (!_movement.IsGrounded || !CanPlaceBlock)
                return;

            AudioManager.Instance.PlaySound("block_invalid");
            return;
        }

        _animController.PlayPlace();
        CurrentBlock.PlaceHoldable();
        CurrentBlock = null;
        CanPlaceBlock = false;
        IsAvailable   = false;

        DOVirtual.DelayedCall(PlacementCooldown, () => IsAvailable = true, false);
    }

    /// <summary>
    /// Drops and disables the current block without placing it.
    /// Called by PlayerController on death or game reset.
    /// </summary>
    public void DropBlock()
    {
        if (CurrentBlock == null) return;

        CurrentBlock.gameObject.SetActive(false);
        CurrentBlock  = null;
        CanPlaceBlock = false;
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
        GameObject randomBlock     = poolManager.GetPooledBlock(blockPosition.position, _controller.GameRank);

        CurrentBlock = randomBlock.GetComponent<HoldableItem>();
        CurrentBlock.SetMaterial(_controller.HayMaterial);
        CurrentBlock.StartHold();

        CanPlaceBlock = false;
        DOVirtual.DelayedCall(GenerationDelay, () => CanPlaceBlock = true, false);
    }

    private void UpdateBlockTransform()
    {
        CurrentBlock.transform.position   = blockPosition.position;
        CurrentBlock.transform.localScale  = new Vector3(transform.lossyScale.x, 1f, 1f);
    }

    private void UpdateBlockAvailability()
    {
        IsBlockUnavailable = !_movement.IsGrounded;

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

    private bool CheckOverlapping(Collider2D collider)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(layersToDetect);
        filter.useTriggers = false;

        List<Collider2D> results = new List<Collider2D>();
        int count = collider.Overlap(filter, results);

        for (int i = 0; i < count; i++)
        {
            if (results[i] != collider)
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

    /// <summary>Returns the world position where a new block should be spawned.</summary>
    public Vector2 GetBlockSpawnPosition() => blockPosition.position;
}