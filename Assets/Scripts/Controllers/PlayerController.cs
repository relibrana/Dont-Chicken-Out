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

    // ── Public state (read by GameManager / UIManager) ────────────────────────

    [NonSerialized] public int     playerIndex;
    [NonSerialized] public int     roundsWon;
    [NonSerialized] public bool    isOnGame;
    [NonSerialized] public Vector2 startPosition;

    public GameStatus GameRank    { get; private set; } = GameStatus.Neutral;
    public Material   HayMaterial { get; private set; }

    // ── External callbacks (set by GameManager) ───────────────────────────────

    public Action<PlayerController> onDeath;

    // ── Private component refs ────────────────────────────────────────────────

    private PlayerInputHandler _inputHandler;
    private PlayerMovement     _movement;
    private PlayerBlockHandler _blockHandler;

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

        _movement.SetAnimController(animController);
        _blockHandler.Initialize(valuesSO.blockPlacementCD, animController);

        kickCollider.forceDirection   = valuesSO.kickForce;
        kickCollider.playerController = this;

        SubscribeToInputEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromInputEvents();
    }

    private void Update()
    {
        _movement.ProcessInput(_inputHandler.CurrentInput);

        if (!isOnGame) return;

        _blockHandler.IsAvailable = true;
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

    private void HandleKick()
    {
        if (IsOnPause()) return;

        animController.PlayKick();
        AudioManager.Instance.PlaySound("player_kick");
    }

    private void HandlePlaceBlock()
    {
        if (IsOnPause()) return;
        if (!isOnGame) return;

        _blockHandler.TryPlaceBlock();
    }

    private void HandleCluck()
    {
        if (IsOnPause()) return;

        AudioManager.Instance.MakeCuackSound();
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
        _movement.AddImpulse(kickImpulse);

        if (Mathf.Sign(kickImpulse.x) == Mathf.Sign(transform.localScale.x))
            animController.PlayHitBack();
        else
            animController.PlayHitFront();
    }

    // ── Public API (called by GameManager) ────────────────────────────────────

    /// <summary>Triggers the player death flow.</summary>
    public void OnDeath() => onDeath?.Invoke(this);

    /// <summary>
    /// Applies an external impulse to the player (explosion, spring, etc.).
    /// Optionally plays hit animations if treated as a kick.
    /// </summary>
    public void AddImpulse(Vector2 impulse, bool isKick = false, bool resetSpeed = false)
    {
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