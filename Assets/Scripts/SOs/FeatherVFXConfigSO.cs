using UnityEngine;

/// <summary>
/// Defines the emission parameters for each feather VFX event.
/// Assign one config per event type in the Inspector.
/// All runtime reads are zero-allocation (structs + value types).
/// </summary>
[CreateAssetMenu(fileName = "FeatherVFXConfig", menuName = "VFX/Feather VFX Config")]
public sealed class FeatherVFXConfigSO : ScriptableObject
{
    // ── Quantity presets ──────────────────────────────────────────────────────

    [System.Serializable]
    public struct QuantityPreset
    {
        [Tooltip("Minimum number of feathers to emit.")]
        public int min;
        [Tooltip("Maximum number of feathers to emit.")]
        public int max;
    }

    [Header("Quantity Presets")]
    [Tooltip("Very few feathers (1–3). Used for Jump.")]
    public QuantityPreset veryFew  = new QuantityPreset { min = 1, max = 3  };
    [Tooltip("Few feathers (3–5). Used for Kick / Flap.")]
    public QuantityPreset few      = new QuantityPreset { min = 3, max = 5  };
    [Tooltip("Moderate feathers (5–8). Used for Kick hit on player.")]
    public QuantityPreset moderate = new QuantityPreset { min = 5, max = 8  };
    [Tooltip("Many feathers (9–15). Used for Death.")]
    public QuantityPreset many     = new QuantityPreset { min = 9, max = 15 };

    // ── Per-event configs ─────────────────────────────────────────────────────

    [System.Serializable]
    public struct EventConfig
    {
        [Tooltip("Base direction of the emission (will be normalized at runtime).")]
        public Vector2 baseDirection;
        [Tooltip("Half-angle in degrees for the random spread cone around baseDirection.")]
        [Range(0f, 180f)]
        public float spreadAngle;
        [Tooltip("Base speed of the emitted feathers.")]
        public float speed;
    }

    [Header("Event Configs")]
    public EventConfig jump;
    public EventConfig flap;
    public EventConfig kickDealt;
    public EventConfig kickReceived;
    public EventConfig death;

    // ── Flap timing ───────────────────────────────────────────────────────────

    [Header("Flap Timing")]
    [Tooltip("Seconds between each feather burst while gliding.")]
    [Min(0.05f)]
    public float flapInterval = 0.3f;
}