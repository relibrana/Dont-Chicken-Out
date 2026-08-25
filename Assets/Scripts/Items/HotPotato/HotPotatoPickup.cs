using UnityEngine;

/// <summary>
/// Papa caliente capsule reward (item catalog #6). All fuse/explosion values
/// are tuning knobs from the doc's "por validar en test" list.
/// </summary>
public sealed class HotPotatoPickup : MonoBehaviour, IInstantItem
{
    [Header("Papa caliente")]
    [SerializeField, Min(2f), Tooltip("Duración del temporizador. Se conserva al transferirse.")]
    private float fuseSeconds = 8f;

    [SerializeField, Min(1f), Tooltip("Multiplicador de velocidad lateral del portador.")]
    private float speedMultiplier = 1.3f;

    [SerializeField, Min(0f), Tooltip("Radio de la explosión final.")]
    private float explosionRadius = 2.5f;

    [SerializeField, Min(0f), Tooltip("Empuje a los jugadores cercanos al explotar (no letal en v1).")]
    private float explosionImpulse = 12f;

    [SerializeField, Min(0), Tooltip("Daño a los sub-bloques dentro del radio.")]
    private int explosionBlockDamage = 3;

    [SerializeField, Tooltip("Capas afectadas por la explosión (jugadores y bloques).")]
    private LayerMask explosionLayers = ~0;

    [SerializeField, Tooltip("Tinte placeholder del portador; pulsa más rápido al agotarse.")]
    private Color carrierTint = new Color(1f, 0.35f, 0.15f, 1f);

    public void Apply(PlayerController player)
    {
        if (!player.TryGetComponent(out HotPotatoState state))
            state = player.gameObject.AddComponent<HotPotatoState>();

        state.Activate(
            fuseSeconds,
            speedMultiplier,
            explosionRadius,
            explosionImpulse,
            explosionBlockDamage,
            explosionLayers,
            carrierTint);
    }
}
