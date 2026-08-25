using UnityEngine;

/// <summary>
/// Doble salto capsule reward (item catalog #10).
/// </summary>
public sealed class DoubleJumpPickup : MonoBehaviour, IInstantItem
{
    [Header("Doble salto")]
    [SerializeField, Min(1f), Tooltip("Duración del estado (el doc pide duración corta).")]
    private float duration = 6f;

    [SerializeField, Range(0.3f, 1.5f), Tooltip("Altura del segundo salto respecto al primero (por validar en doc).")]
    private float airJumpMultiplier = 0.85f;

    [SerializeField, Tooltip("Si el salto aéreo se recarga al aterrizar (repetible) o es de un solo uso (por validar en doc).")]
    private bool repeatable = true;

    [SerializeField, Tooltip("Tinte placeholder del portador.")]
    private Color carrierTint = new Color(0.35f, 0.8f, 1f, 1f);

    public void Apply(PlayerController player)
    {
        if (!player.TryGetComponent(out DoubleJumpState state))
            state = player.gameObject.AddComponent<DoubleJumpState>();

        state.Activate(duration, airJumpMultiplier, repeatable, carrierTint);
    }
}
