using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Teleporte capsule reward (item catalog #7). Teleports the user above a
/// random other alive player — no position swap. If the space above the target
/// is blocked, nudges upward until a clear spot is found (doc's open edge case,
/// resolved as "closest free spot above").
/// </summary>
public sealed class TeleportPickup : MonoBehaviour, IInstantItem
{
    [Header("Teleporte")]
    [SerializeField, Min(0.5f), Tooltip("Altura sobre el jugador objetivo a la que apareces.")]
    private float appearHeight = 2.5f;

    [SerializeField, Tooltip("Capas que cuentan como espacio ocupado (bloques/suelo).")]
    private LayerMask blockedLayers;

    [SerializeField, Tooltip("Tamaño del chequeo de espacio libre (aprox. el collider del pollo).")]
    private Vector2 clearanceSize = new Vector2(0.9f, 1.1f);

    [SerializeField, Min(0.1f), Tooltip("Paso hacia arriba al buscar hueco libre.")]
    private float nudgeStep = 0.6f;

    [SerializeField, Min(1), Tooltip("Máximo de intentos de hueco antes de teletransportar igual.")]
    private int maxNudges = 8;

    public void Apply(PlayerController player)
    {
        PlayerController target = PickRandomTarget(player);
        if (target == null) return;

        Vector2 destination = (Vector2)target.transform.position + Vector2.up * appearHeight;

        for (int i = 0; i < maxNudges; i++)
        {
            if (Physics2D.OverlapBox(destination, clearanceSize, 0f, blockedLayers) == null)
                break;

            destination += Vector2.up * nudgeStep;
        }

        player.TeleportTo(destination);
    }

    private static PlayerController PickRandomTarget(PlayerController self)
    {
        List<PlayerController> candidates = new();

        foreach (PlayerController player in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (player != self && player.isOnGame)
                candidates.Add(player);
        }

        if (candidates.Count == 0) return null;

        return candidates[Random.Range(0, candidates.Count)];
    }
}
