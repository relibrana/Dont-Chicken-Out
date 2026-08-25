using UnityEngine;

/// <summary>
/// POW capsule reward (item catalog #2). Activates on pickup — there is no
/// use-item input in the current scheme, so timing the POW means timing when
/// you break the capsule.
/// </summary>
public sealed class PowPickup : MonoBehaviour, IInstantItem
{
    [Header("POW")]
    [SerializeField, Range(1, 5), Tooltip("Desde dónde cuenta la cuenta regresiva en pantalla.")]
    private int countFrom = 3;

    [SerializeField, Min(0.5f), Tooltip("Duración del aturdimiento. Ojo: la cámara sigue subiendo (doc).")]
    private float stunSeconds = 2f;

    [SerializeField, Tooltip("Tinte placeholder de los jugadores aturdidos.")]
    private Color stunTint = new Color(0.6f, 0.6f, 0.75f, 1f);

    public void Apply(PlayerController player) => PowSequence.Run(countFrom, stunSeconds, stunTint);
}
