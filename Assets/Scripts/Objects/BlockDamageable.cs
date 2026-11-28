using UnityEngine;

/// <summary>
/// Attached to each individual sub-block child of a BlockScript. Since nov
/// 2026 the life is the whole piece's (GDD): hits are forwarded to the parent
/// BlockScript, which breaks every block at once at zero. A block with no
/// parent piece keeps its own HP, as before.
/// Notifies the parent BlockScript when it leaves so the parent can check
/// whether all children are gone and return itself to the pool.
/// Reset on Awake and OnDisable ensures pool reuse is clean.
/// </summary>
public sealed class BlockDamageable : MonoBehaviour, IDamageable
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField, Min(1), Tooltip("HP of this individual sub-block.")]
    private int maxLife = 3;

    [SerializeField, Tooltip("Tint applied when the block is at 0 HP (death colour).")]
    private Color damagedColor = new Color(1f, 0.25f, 0.25f, 1f);

    // ── Private state ─────────────────────────────────────────────────────────

    private int            _currentLife;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    private BlockScript    _parentBlock;

    // Cached to avoid recomputing every damage call.
    private static readonly Color HealthyColor = Color.white;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _parentBlock    = GetComponentInParent<BlockScript>();

        // Not only on OnDisable: a block activated without ever having been
        // disabled would start at 0 HP and ignore every hit.
        ResetState();
    }

    private void OnDisable()
    {
        ResetState();
    }

    // ── IDamageable ───────────────────────────────────────────────────────────

    /// <summary>The piece this block belongs to (null for a loose block).</summary>
    public BlockScript Piece => _parentBlock;

    public void TakeDamage(int amount, PlayerController player)
    {
        if (amount <= 0 || _currentLife <= 0) return;

        if (_parentBlock != null)
        {
            _parentBlock.TakeHit(amount, player);
            return;
        }

        _currentLife -= amount;

        if (_currentLife <= 0)
        {
            Die();
            return;
        }

        RefreshColor();
    }

    /// <summary>
    /// Takes this sub-block out of play without it having been damaged: the
    /// sub-blocks BlockOverlapCheck hides because they spawned inside another
    /// block. It has to count as a death for the parent — otherwise the piece
    /// never reaches zero living children and never returns to the pool.
    /// </summary>
    public void RemoveFromPlay()
    {
        if (_currentLife <= 0) return;

        Die();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void Die()
    {
        _currentLife = 0;

        // Disable visuals and collision immediately.
        _spriteRenderer.enabled         = false;
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled    = false;

        // Notify the parent so it can check if all sub-blocks are dead.
        _parentBlock?.OnChildBlockDied();
    }

    private void ResetState()
    {
        _currentLife            = maxLife;
        _spriteRenderer.color   = HealthyColor;

        // Re-enable visuals and collision in case this sub-block was dead.
        _spriteRenderer.enabled = true;
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;
    }

    /// <summary>Damage tint set by the piece (its life, not this block's).</summary>
    public void SetTint(Color tint)
    {
        if (_currentLife > 0) _spriteRenderer.color = tint;
    }

    private void RefreshColor()
    {
        // t = 0 → healthy (white), t = 1 → fully damaged (damagedColor).
        float t = 1f - (float)_currentLife / maxLife;
        _spriteRenderer.color = Color.Lerp(HealthyColor, damagedColor, t);
    }
}