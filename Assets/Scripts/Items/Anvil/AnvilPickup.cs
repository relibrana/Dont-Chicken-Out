using UnityEngine;

/// <summary>
/// Yunque capsule reward (item catalog #8). Activates on pickup: spawns the
/// anvil above the top of the screen in the user's current column, so the
/// aiming skill is standing in the right place when the capsule breaks.
/// </summary>
public sealed class AnvilPickup : MonoBehaviour, IInstantItem
{
    [Header("Yunque")]
    [SerializeField, Tooltip("Prefab del yunque: sprite + Collider2D en modo trigger + AnvilFall.")]
    private GameObject anvilPrefab;

    [SerializeField, Min(1f), Tooltip("Velocidad de caída.")]
    private float fallSpeed = 14f;

    [SerializeField, Min(0f), Tooltip("Duración del telegrafiado (parpadeo arriba) antes de caer.")]
    private float telegraphSeconds = 1f;

    [SerializeField, Tooltip("Si se detiene al matar al primer jugador o continúa hasta abajo (por validar en doc).")]
    private bool stopOnKill = false;

    [SerializeField, Min(0f), Tooltip("Margen sobre el borde superior de la cámara donde aparece.")]
    private float spawnMargin = 1.5f;

    public void Apply(PlayerController player)
    {
        if (anvilPrefab == null)
        {
            Debug.LogWarning("AnvilPickup: falta asignar anvilPrefab.");
            return;
        }

        float top = Camera.main != null
            ? Camera.main.ViewportToWorldPoint(Vector3.one).y
            : player.transform.position.y + 12f;

        Vector3 spawnPosition = new Vector3(
            player.transform.position.x,
            top + spawnMargin,
            0f);

        GameObject anvil = Instantiate(anvilPrefab, spawnPosition, Quaternion.identity);

        if (anvil.TryGetComponent(out AnvilFall fall))
            fall.Begin(fallSpeed, telegraphSeconds, stopOnKill);
        else
            Debug.LogWarning("AnvilPickup: el prefab no tiene AnvilFall.");
    }
}
