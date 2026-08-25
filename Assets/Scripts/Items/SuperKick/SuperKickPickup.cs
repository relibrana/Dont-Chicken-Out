using UnityEngine;

/// <summary>
/// Capsule reward for the "Súper patada" item (catalog #1). Applied instantly on
/// pickup — the player keeps their held block. Every serialized value is a
/// tuning knob from the doc's "por validar en test" list.
/// </summary>
public sealed class SuperKickPickup : MonoBehaviour, IInstantItem
{
    [Header("Súper patada")]
    [SerializeField, Min(0.5f), Tooltip("Duración del estado. Rango del doc: 3 a 5 segundos.")]
    private float duration = 4f;

    [SerializeField, Min(1), Tooltip("Daño por patada a cada sub-bloque. La vida base es 3, así que 3 = romper de una patada.")]
    private int blockDamage = 3;

    [SerializeField, Min(1f), Tooltip("Multiplicador de fuerza de la patada contra objetos pateables (discos, bombas...).")]
    private float impulseMultiplier = 1.5f;

    [SerializeField, Tooltip("Tinte placeholder del portador mientras dura el estado (visual final por definir).")]
    private Color carrierTint = new Color(1f, 0.55f, 0.1f, 1f);

    public void Apply(PlayerController player)
    {
        if (!player.TryGetComponent(out SuperKickState state))
            state = player.gameObject.AddComponent<SuperKickState>();

        state.Activate(duration, blockDamage, impulseMultiplier, carrierTint);
    }
}
