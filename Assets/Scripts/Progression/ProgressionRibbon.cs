using System;
using UnityEngine;

/// <summary>
/// The ribbon ("listón"): a horizontal finish-line tape that spawns a fixed
/// distance above the view and stays anchored to the world, so the rising
/// camera always ends up reaching it.
///
/// It is broken by the FIRST player that touches it. Breaking is what applies
/// the phase change — spawning only decides *when it becomes available*.
///
/// Expected hierarchy (both for the art prefab and the runtime placeholder):
///   root      — BoxCollider2D (trigger) + this component. Scale stays at 1.
///     └ visual — the sprite / animated art. Scaled to the requested size.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProgressionRibbon : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Child transform holding the art. Scaled to the width/height requested by ProgressionManager.")]
    [SerializeField] private Transform visualRoot;

    [Tooltip("Trigger collider that detects the first player. Sized by ProgressionManager.")]
    [SerializeField] private BoxCollider2D triggerCollider;

    [Tooltip("Optional. Receives the break trigger. Leave empty until art delivers the animation.")]
    [SerializeField] private Animator animator;

    [Header("Break")]
    [SerializeField] private string breakTriggerName = "Break";

    [Tooltip("Seconds the broken ribbon stays visible so the break animation can play.")]
    [SerializeField] private float despawnDelay = 0.6f;

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>True while the ribbon is up and can still be broken.</summary>
    public bool IsLive { get; private set; }

    private Action<PlayerController> _onBroken;
    private float _despawnTimer;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Update()
    {
        if (IsLive || _despawnTimer <= 0f) return;

        _despawnTimer -= Time.deltaTime;
        if (_despawnTimer <= 0f) Despawn();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsLive) return;

        // Colliders live on child objects (head, feet, kick box), so walk up.
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.isOnGame) return;

        Break(player);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Places the ribbon at a world position and arms it.
    /// The transform is never re-parented to the camera — the anchor to the
    /// world is what guarantees the camera eventually reaches it.
    /// </summary>
    public void Spawn(Vector2 worldPosition, float width, float height, Action<PlayerController> onBroken)
    {
        _onBroken     = onBroken;
        _despawnTimer = 0f;

        transform.position   = worldPosition;
        transform.localScale = Vector3.one;

        if (visualRoot != null)
            visualRoot.localScale = new Vector3(width, height, 1f);

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            triggerCollider.size      = new Vector2(width, height);
            triggerCollider.offset    = Vector2.zero;
            triggerCollider.enabled   = true;
        }

        gameObject.SetActive(true);
        IsLive = true;
    }

    /// <summary>
    /// Breaks the ribbon without a player, used by the safety net in
    /// ProgressionManager when the ribbon fell below the view untouched.
    /// </summary>
    public void ForceBreak() => Break(null);

    /// <summary>Hides the ribbon immediately (round reset).</summary>
    public void Despawn()
    {
        IsLive        = false;
        _despawnTimer = 0f;
        _onBroken     = null;
        gameObject.SetActive(false);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Single-shot. Several players can touch the ribbon on the same frame, so
    /// IsLive is cleared before the callback runs.
    /// </summary>
    private void Break(PlayerController breaker)
    {
        if (!IsLive) return;

        IsLive = false;

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        if (animator != null && !string.IsNullOrEmpty(breakTriggerName))
            animator.SetTrigger(breakTriggerName);

        _despawnTimer = Mathf.Max(0.01f, despawnDelay);

        Action<PlayerController> callback = _onBroken;
        _onBroken = null;
        callback?.Invoke(breaker);
    }

    // ── Placeholder factory ───────────────────────────────────────────────────

    /// <summary>
    /// Builds a runtime placeholder ribbon so the system is fully playable
    /// before art delivers the asset. Replace by assigning a prefab on
    /// ProgressionManager — no code change needed.
    /// </summary>
    public static ProgressionRibbon CreatePlaceholder(Transform parent, Color color)
    {
        var root = new GameObject("ProgressionRibbon (placeholder)");
        root.transform.SetParent(parent, false);

        var collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;

        var visual = new GameObject("visual");
        visual.transform.SetParent(root.transform, false);

        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite       = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        renderer.color        = color;
        renderer.sortingOrder = 500;

        var ribbon = root.AddComponent<ProgressionRibbon>();
        ribbon.visualRoot      = visual.transform;
        ribbon.triggerCollider = collider;

        root.SetActive(false);
        return ribbon;
    }
}
