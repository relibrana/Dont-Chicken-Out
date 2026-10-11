using UnityEngine;

/// <summary>
/// Yunque capsule reward (item catalog #8). Activates on pickup: opens the
/// warning column (AnvilZone) over the user's current X and drops the anvil
/// from above the top of the screen once the column is up, so the aiming skill
/// is standing in the right place when the capsule breaks.
/// </summary>
public sealed class AnvilPickup : MonoBehaviour, IInstantItem
{
    [Header("Yunque")]
    [SerializeField, Tooltip("Prefab del yunque: sprite + Collider2D en modo trigger + AnvilFall.")]
    private GameObject anvilPrefab;

    [SerializeField, Min(1f), Tooltip("Velocidad de caída.")]
    private float fallSpeed = 14f;

    [SerializeField, Tooltip("Si se detiene al matar al primer jugador o continúa hasta abajo (por validar en doc).")]
    private bool stopOnKill = false;

    [SerializeField, Min(0f), Tooltip("Margen sobre el borde superior de la cámara donde aparece.")]
    private float spawnMargin = 1.5f;

    [Header("Aviso (columna)")]
    [SerializeField, Tooltip("La columna que marca dónde cae (Zona-Yunque + Zona-YunqueFlecha).")]
    private AnvilZone zonePrefab;

    [SerializeField, Min(0f), Tooltip("La columna se abre de ancho 0 a 1. Junto con el fundido es el aviso total (antes 1 s).")]
    private float zoneOpenSeconds = 0.3f;

    [SerializeField, Min(0f), Tooltip("Fundido de entrada de las flechas. Al terminar, cae el yunque.")]
    private float arrowsFadeSeconds = 0.7f;

    public void Apply(PlayerController player)
    {
        if (anvilPrefab == null)
        {
            Debug.LogWarning("AnvilPickup: falta asignar anvilPrefab.");
            return;
        }

        float x   = player.transform.position.x;
        float top = GameplayCamera.TopY(player.transform.position.y + 12f);

        AnvilZone  zone  = AnvilZone.Spawn(zonePrefab, x);
        GameObject anvil = Instantiate(anvilPrefab, new Vector3(x, top + spawnMargin, 0f), Quaternion.identity);

        if (anvil.TryGetComponent(out AnvilFall fall))
            fall.Begin(fallSpeed, stopOnKill, zone, zoneOpenSeconds, arrowsFadeSeconds);
        else
            Debug.LogWarning("AnvilPickup: el prefab no tiene AnvilFall.");
    }
}
