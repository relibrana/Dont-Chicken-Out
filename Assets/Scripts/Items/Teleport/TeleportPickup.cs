using DG.Tweening;
using UnityEngine;

/// <summary>
/// Teleporte (item catalog #7). Cambios de diseño (sep 2026):
/// · No se activa al romper la cápsula: se lleva en la mano y el jugador lo
///   dispara cuando quiere con el botón de colocar bloque.
/// · El destino NO es un rival al azar: es el jugador vivo que va MÁS ALTO.
/// · Apareces por encima de él y recibes un salto aéreo de un solo uso, para
///   que la reaparición dé opción a reaccionar en vez de ser una caída seca.
/// Si el hueco de arriba está ocupado, sube hasta encontrar sitio libre.
/// Arte (nov 2026): se abre un hueco (Item_TeleportHueco) a los pies del pollo
/// mientras se encoge, y en el destino sale el poof (Item_TeleportPoof).
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

    [SerializeField, Min(0f), Tooltip("Tiempo desaparecido entre irse y reaparecer. Mientras tanto nada lo puede tocar ni matar.")]
    private float goneSeconds = 0.6f;

    [SerializeField, Min(0.05f), Tooltip("Reaparece en el destino creciendo hasta su tamaño, con un pequeño rebote.")]
    private float growSeconds = 0.2f;

    [Header("Arte")]
    [SerializeField, Tooltip("Hueco que se abre a los pies del pollo al irse.")]
    private Sprite holeSprite;

    [SerializeField, Min(0.1f), Tooltip("Tamaño del hueco.")]
    private float holeScale = 1.5f;

    [SerializeField, Tooltip("Capa de las piezas: el hueco se dibuja por encima del suelo aunque el pollo vaya en una capa inferior.")]
    private string groundSortingLayer = "Items";

    [SerializeField, Min(0.01f), Tooltip("Lo que tarda el hueco en abrirse y en cerrarse.")]
    private float holeOpenSeconds = 0.08f;

    [SerializeField, Min(0.01f)]
    private float holeCloseSeconds = 0.15f;

    [SerializeField, Tooltip("Frames del poof con el que aparece en el destino.")]
    private Sprite[] poofFrames;

    [SerializeField, Min(1f)]
    private float poofFps = 16f;

    [SerializeField, Tooltip("Desplazamiento del poof respecto al punto de llegada.")]
    private Vector2 poofOffset = new Vector2(0f, 0.3f);

    [SerializeField, Min(0.1f), Tooltip("Tamaño del poof de llegada.")]
    private float poofScale = 1.6f;

    [Header("Temblor al llegar")]
    [SerializeField, Min(0f)] private float arrivalShakeDuration = 0.15f;
    [SerializeField, Min(0f), Tooltip("Pequeño: la bomba usa 0,55 y el POW 0,9.")]
    private float arrivalShakeAmplitude = 0.2f;

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

        var sorting = SpriteFlipbook.SortingOf(user.VisualRoot);
        OpenHole(user, sorting.layerId, sorting.minOrder);

        Sprite[] poof = poofFrames; float fps = poofFps; Vector2 offset = poofOffset; float poofSize = poofScale;
        float shakeTime = arrivalShakeDuration, shakeAmp = arrivalShakeAmplitude;

        state.Activate(
            shrinkSeconds,
            goneSeconds,
            growSeconds,
            () => FindDestination(user, height, blocked, clearance, step, nudges),
            () =>
            {
                if (airJumps > 0) user.GrantAirJump(airJumps);
                SpriteFlipbook.SpawnOnce("TeleportPoof", poof, fps,
                    (Vector2)user.transform.position + offset, poofSize, sorting.layerId, sorting.maxOrder + 1);
                if (shakeAmp > 0f) ScreenShake.Punch(shakeTime, shakeAmp);
            });
    }

    /// <summary>
    /// The hole the chicken sinks into: opens at its feet and stays there while
    /// it shrinks and is gone, closing as it reappears. Presentation only.
    /// Behind the chicken when the chicken is drawn over the pieces; otherwise
    /// (until nov 2026 the ground hid it) over the pieces, in front of all.
    /// </summary>
    private void OpenHole(PlayerController user, int chickenLayerId, int chickenMinOrder)
    {
        int groundLayerId = SortingLayer.NameToID(groundSortingLayer);
        bool chickenOverGround = SortingLayer.GetLayerValueFromID(chickenLayerId) > SortingLayer.GetLayerValueFromID(groundLayerId);
        int sortingLayerId = chickenOverGround ? chickenLayerId : groundLayerId;
        int sortingOrder   = chickenOverGround ? chickenMinOrder - 1 : 1000;

        if (holeSprite == null) return;

        // Right at the feet. The new rig's root sits at the feet (ChickenSkin);
        // sprite bounds of a skinned rig are not reliable for this, so they are
        // only the fallback for a chicken without the new rig.
        Vector3 feet = user.transform.position;
        if (user.Skin != null)
        {
            feet = user.Skin.FeetPosition;
        }
        else
        {
            bool any = false;
            Bounds bounds = default;
            foreach (SpriteRenderer sr in user.VisualRoot.GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr.forceRenderingOff || !sr.enabled) continue;
                if (!any) { bounds = sr.bounds; any = true; }
                else bounds.Encapsulate(sr.bounds);
            }
            if (any) feet = new Vector3(bounds.center.x, bounds.min.y, 0f);
        }
        feet.z = 0f;

        SpriteRenderer hole = SpriteFlipbook.SpawnSprite("TeleportHole", holeSprite, feet, holeScale, sortingLayerId, sortingOrder);
        Transform t = hole.transform;
        t.localScale = new Vector3(0f, holeScale, 1f);

        GameObject go = hole.gameObject;
        DOTween.Sequence()
            .Append(t.DOScaleX(holeScale, holeOpenSeconds).SetEase(Ease.OutBack))
            .AppendInterval(Mathf.Max(0f, shrinkSeconds + goneSeconds - holeOpenSeconds))
            .Append(t.DOScaleX(0f, holeCloseSeconds).SetEase(Ease.InBack))
            .OnComplete(() => { if (go != null) Destroy(go); })
            .SetLink(go);
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
