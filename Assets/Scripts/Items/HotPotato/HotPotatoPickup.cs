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
    private float explosionRadius = 2f;

    [SerializeField, Min(0f), Tooltip("Empuje a los jugadores cercanos al explotar, atenuado con la distancia como la bomba. No letal.")]
    private float explosionImpulse = 60f;

    [SerializeField, Min(0), Tooltip("Daño a los sub-bloques dentro del radio.")]
    private int explosionBlockDamage = 3;

    [SerializeField, Tooltip("Capas afectadas por la explosión (jugadores y bloques).")]
    private LayerMask explosionLayers = ~0;

    [Header("Visual")]
    [SerializeField, Tooltip("La papa que se ve encima del portador y explota como la bomba.")]
    private HotPotatoVisual visualPrefab;

    [SerializeField, Tooltip("Dónde va la papa respecto al portador. Por encima de la etiqueta 'Player N' (+1,6).")]
    private Vector3 visualOffset = new Vector3(0f, 2.25f, 0f);

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
            visualPrefab,
            visualOffset);
    }
}
