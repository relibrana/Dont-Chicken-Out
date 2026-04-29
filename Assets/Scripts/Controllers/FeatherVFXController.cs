using UnityEngine;

/// <summary>
/// Scene singleton responsible for emitting all feather particle effects.
/// Uses a single ParticleSystem with ParticleSystem.EmitParams for zero-allocation
/// per-event overrides (position, velocity, count).
/// 
/// Setup: Place this component in the scene alongside a configured ParticleSystem
/// (Texture Sheet Animation with your 4 feather sprites, Noise, Rotation over Lifetime,
/// low Gravity Modifier, etc.). All visual tuning lives in the PS asset + FeatherVFXConfigSO.
/// </summary>
public sealed class FeatherVFXController : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static FeatherVFXController Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private ParticleSystem featherPS;
    [SerializeField] private FeatherVFXConfigSO config;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ── Public properties ─────────────────────────────────────────────────────

    /// <summary>Interval in seconds between flap bursts. Read by PlayerController.</summary>
    public float FlapInterval => config != null ? config.flapInterval : 0.3f;

    // ── Public emit methods ───────────────────────────────────────────────────

    /// <summary>Emits a small burst of feathers when the player jumps.</summary>
    public void EmitJump(Vector2 position)
    {
        Emit(position, config.jump, config.veryFew);
    }

    /// <summary>Emits a small burst of feathers during glide/flap (called on interval).</summary>
    public void EmitFlap(Vector2 position)
    {
        Emit(position, config.flap, config.few);
    }

    /// <summary>Emits feathers from the player who dealt the kick.</summary>
    /// <param name="facingDirection">Normalized horizontal facing direction of the kicker.</param>
    public void EmitKickDealt(Vector2 position, Vector2 facingDirection)
    {
        var eventConfig = config.kickDealt;
        eventConfig.baseDirection = facingDirection;
        Emit(position, eventConfig, config.few);
    }

    /// <summary>Emits feathers from the player who received the kick.</summary>
    /// <param name="kickImpulse">The raw kick impulse applied to the receiver.</param>
    public void EmitKickReceived(Vector2 position, Vector2 kickImpulse)
    {
        var eventConfig = config.kickReceived;
        eventConfig.baseDirection = kickImpulse.normalized;
        Emit(position, eventConfig, config.few);
    }

    /// <summary>Emits a large burst of feathers at the death position.</summary>
    public void EmitDeath(Vector2 position)
    {
        Emit(position, config.death, config.many);
    }

    // ── Core emission ─────────────────────────────────────────────────────────

    /// <summary>
    /// Emits [min, max] particles using EmitParams overrides.
    /// Zero heap allocations: EmitParams is a struct, Random calls are value ops.
    /// </summary>
    private void Emit(
        Vector2 position,
        FeatherVFXConfigSO.EventConfig eventConfig,
        FeatherVFXConfigSO.QuantityPreset preset)
    {
        if (featherPS == null || config == null) return;

        int count = Random.Range(preset.min, preset.max + 1);

        Vector2 baseDir = eventConfig.baseDirection.sqrMagnitude > 0f
            ? eventConfig.baseDirection.normalized
            : Vector2.up;

        var emitParams = new ParticleSystem.EmitParams();
        emitParams.position = position;

        for (int i = 0; i < count; i++)
        {
            Vector2 dir      = RandomDirectionInCone(baseDir, eventConfig.spreadAngle);
            emitParams.velocity = dir * eventConfig.speed;

            featherPS.Emit(emitParams, 1);
        }
    }

    /// <summary>
    /// Returns a random normalized direction within a cone of half-angle degrees
    /// around baseDirection, operating entirely in 2D.
    /// </summary>
    private static Vector2 RandomDirectionInCone(Vector2 baseDir, float halfAngleDegrees)
    {
        float baseAngle   = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
        float randomAngle = baseAngle + Random.Range(-halfAngleDegrees, halfAngleDegrees);
        float rad         = randomAngle * Mathf.Deg2Rad;

        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }
}