using UnityEngine;

/// <summary>
/// Teleporte (item catalog #7). Cambios de diseño (sep 2026):
/// · No se activa al romper la cápsula: se lleva en la mano y el jugador lo
///   dispara cuando quiere con el botón de colocar bloque.
/// · El destino NO es un rival al azar: es el jugador vivo que va MÁS ALTO.
/// · Apareces por encima de él y recibes un salto aéreo de un solo uso, para
///   que la reaparición dé opción a reaccionar en vez de ser una caída seca.
/// Si el hueco de arriba está ocupado, sube hasta encontrar sitio libre.
/// </summary>
public sealed class TeleportPickup : HoldableItem
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

    [SerializeField, Min(0), Tooltip("Saltos aéreos de un solo uso que se conceden al teletransportarse.")]
    private int grantedAirJumps = 1;

    [Header("Animación (dentro del sonido, que dura ~1,4 s)")]
    [SerializeField, Min(0.05f), Tooltip("El pollo se encoge hasta desaparecer donde está. Corto: es un tp, no un fundido.")]
    private float shrinkSeconds = 0.15f;

    [SerializeField, Min(0.05f), Tooltip("Reaparece en el destino creciendo hasta su tamaño, con un pequeño rebote.")]
    private float growSeconds = 0.2f;

    /// <summary>Nunca se coloca: se consume al activarlo, desde el suelo o en el aire.</summary>
    public override bool BypassPlacementChecks => true;

    /// <summary>
    /// El botón de colocar bloque activa el ítem en vez de colocarlo.
    /// No se llama a base.PlaceHoldable(): no hay nada que dejar en el mundo.
    /// </summary>
    public override void PlaceHoldable()
    {
        Activate(Owner);
        gameObject.SetActive(false);
    }

    private void Activate(PlayerController user)
    {
        if (user == null) return;
        if (PickHighestTarget(user) == null) return; // nobody to go to: the item is spent, nothing happens

        // The sound covers the whole shrink → reappear (design, oct 2026).
        SimEvents.Raise(SimEvent.PlayerTeleported, user.transform.position, user);

        // Copies: this pickup goes back to the pool right away.
        float height = appearHeight; LayerMask blocked = blockedLayers; Vector2 clearance = clearanceSize;
        float step = nudgeStep; int nudges = maxNudges; int airJumps = grantedAirJumps;

        if (!user.TryGetComponent(out TeleportState state))
            state = user.gameObject.AddComponent<TeleportState>();

        state.Activate(
            shrinkSeconds,
            growSeconds,
            () => FindDestination(user, height, blocked, clearance, step, nudges),
            () => { if (airJumps > 0) user.GrantAirJump(airJumps); });
    }

    /// <summary>
    /// Asked when the chicken reappears, not when the button is pressed, so it
    /// lands over wherever the highest rival is by then. Null = nobody left.
    /// </summary>
    private static Vector2? FindDestination(PlayerController user, float appearHeight, LayerMask blockedLayers,
                                            Vector2 clearanceSize, float nudgeStep, int maxNudges)
    {
        PlayerController target = PickHighestTarget(user);
        if (target == null) return null;

        Vector2 destination = (Vector2)target.transform.position + Vector2.up * appearHeight;

        for (int i = 0; i < maxNudges; i++)
        {
            if (Physics2D.OverlapBox(destination, clearanceSize, 0f, blockedLayers) == null)
                break;

            destination += Vector2.up * nudgeStep;
        }

        return destination;
    }

    /// <summary>
    /// Highest living rival. Ties resolve to whoever is found first. A dead
    /// chicken never qualifies: until oct 2026 the dead were parked in a death
    /// zone with isOnGame still true, and the teleport sent people there.
    /// </summary>
    private static PlayerController PickHighestTarget(PlayerController self)
    {
        PlayerController best = null;
        float bestY = float.NegativeInfinity;

        foreach (PlayerController player in PlayerController.Active)
        {
            if (player == self || !player.IsAlive || !player.isOnGame) continue;

            float y = player.transform.position.y;
            if (y <= bestY) continue;

            bestY = y;
            best  = player;
        }

        return best;
    }
}
