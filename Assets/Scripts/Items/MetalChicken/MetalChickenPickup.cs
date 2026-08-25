using UnityEngine;

/// <summary>
/// Pollo metálico capsule reward (item catalog #9).
/// </summary>
public sealed class MetalChickenPickup : MonoBehaviour, IInstantItem
{
    [Header("Pollo metálico")]
    [SerializeField, Min(1f), Tooltip("Duración del estado. El doc pide vigilarla: la inmunidad anula 2 de los 3 verbos ofensivos.")]
    private float duration = 6f;

    [SerializeField, Range(0.3f, 1f), Tooltip("Multiplicador de la velocidad inicial de salto (más bajo).")]
    private float jumpMultiplier = 0.75f;

    [SerializeField, Min(1f), Tooltip("Multiplicador de gravedad (más pesado, planeo menos efectivo).")]
    private float gravityMultiplier = 1.6f;

    [SerializeField, Min(1f), Tooltip("Multiplicador de fuerza de la patada.")]
    private float kickMultiplier = 1.6f;

    [SerializeField, Tooltip("Tinte placeholder metálico del portador.")]
    private Color metalTint = new Color(0.62f, 0.66f, 0.72f, 1f);

    public void Apply(PlayerController player)
    {
        if (!player.TryGetComponent(out MetalChickenState state))
            state = player.gameObject.AddComponent<MetalChickenState>();

        state.Activate(duration, jumpMultiplier, gravityMultiplier, kickMultiplier, metalTint);
    }
}
