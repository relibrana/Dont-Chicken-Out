using UnityEngine;

/// <summary>
/// Attached to each individual sub-block child of a BlockScript.
/// Handles its own HP, color feedback (white → soft red), and death.
/// Notifies the parent BlockScript when it dies so the parent can
/// check whether all children are gone and return itself to the pool.
/// Reset on OnDisable ensures pool reuse is clean.
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
    }

    private void OnDisable()
    {
        ResetState();
    }

    // ── IDamageable ───────────────────────────────────────────────────────────

    public void TakeDamage(int amount, PlayerController player)
    {
        if (amount <= 0 || _currentLife <= 0) return;

        _currentLife -= amount;

        if (_currentLife <= 0)
        {
            Die();
            return;
        }

        RefreshColor();
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

    private void RefreshColor()
    {
        // t = 0 → healthy (white), t = 1 → fully damaged (damagedColor).
        float t = 1f - (float)_currentLife / maxLife;
        _spriteRenderer.color = Color.Lerp(HealthyColor, damagedColor, t);
    }
}