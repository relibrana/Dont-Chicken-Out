using UnityEngine;

/// <summary>
/// Moco (GDD, nov 2026). Thrown in the common arc, it does not stop until it
/// hits a piece or a chicken: walls send it bouncing on, and below the view it
/// is lost. A kick deflects it in flight (a moco can be sent back).
///  - Hits a chicken: stuck on the spot, even in the air (MocoStuckState).
///  - Hits a piece: the whole piece turns mucous (MocoPiece) and catches the
///    first chicken that touches or kicks it, then goes back to normal.
/// Stuck chickens mash jump or kick to break free (about five presses); a kick
/// from someone else frees them at once.
///
/// Art (Item_MocoSplash): frame 0 is the item (slot icon), frame 1 the moco
/// flying; from frame 2 the goo on each block of a mucous piece (2-3 filling,
/// then 4-9 looping).
/// </summary>
public sealed class MocoProjectile : ThrowableItem
{
    [Header("Moco")]
    [SerializeField, Min(1), Tooltip("Vida del moco: pulsaciones de salto o patada para liberarse (GDD: unas cinco).")]
    private int strugglePresses = 5;

    [SerializeField, Min(0f), Tooltip("Puntos que quita cada patada o salto.")]
    private float kickStrugglePoints = 1f;

    [SerializeField, Min(0f), Tooltip("Puntos que quita cada pulsación de movimiento (GDD: 0, solo salto o patada).")]
    private float moveStrugglePoints = 0f;

    [SerializeField, Min(0f), Tooltip("Puntos que se recuperan por segundo. 0 = sin descarga (GDD).")]
    private float struggleDrainPerSecond = 0f;

    [SerializeField, Min(0f), Tooltip("Cuánto se desplaza el pollo al forcejear con movimiento, en unidades.")]
    private float moveYank = 0.08f;

    [SerializeField, Min(0f), Tooltip("Lo mismo al forcejear con salto o patada: más amplio y marcado.")]
    private float kickYank = 0.18f;

    [SerializeField, Min(0.1f), Tooltip("Velocidad a la que el moco devuelve al pollo al centro (unidades/s).")]
    private float yankDecay = 1.2f;

    [SerializeField, Min(0.5f), Tooltip("Tope de tiempo pegado (accesibilidad): se libera solo al cumplirse.")]
    private float maxStuckSeconds = 3f;

    [SerializeField, Tooltip("Tinte de la víctima mientras está pegada (hasta tener animación de pollo atrapado).")]
    private Color stuckTint = new Color(0.55f, 0.9f, 0.3f, 1f);

    [Header("Vuelo")]
    [SerializeField, Range(0f, 1f), Tooltip("Energía que conserva al rebotar en una pared o en el suelo.")]
    private float bounceKeep = 0.75f;

    [SerializeField, Min(0f), Tooltip("Por debajo de esta velocidad tras rebotar en el suelo, se pierde.")]
    private float loseBelowSpeed = 2f;

    [SerializeField, Min(0f), Tooltip("Tras desviarlo de una patada, segundos en los que no pega a nadie (para no pegar al que patea).")]
    private float deflectGraceSeconds = 0.15f;

    [Header("Arte")]
    [SerializeField, Tooltip("Frames de Item_MocoSplash: 0 ítem, 1 en vuelo, 2-3 se llena, 4-9 bucle sobre cada bloque.")]
    private Sprite[] splatFrames;

    [SerializeField, Min(0)] private int flyingFrame = 1;
    [SerializeField, Min(0)] private int pieceFillFrom = 2;
    [SerializeField, Min(0)] private int pieceLoopFrom = 4;
    [SerializeField, Min(1f)] private float splatFps = 12f;

    [SerializeField, Tooltip("Ajuste fino (unidades de mundo). Con 0 la parte de arriba del moco toca el borde de arriba de cada bloque; Y positivo lo sube por encima del borde.")]
    private Vector2 pieceOverlayOffset = new Vector2(0f, 0.15f);

    [SerializeField, Min(0.1f), Tooltip("Multiplicador sobre el ajuste automático: 1 = el moco visible mide lo mismo que el bloque de ancho.")]
    private float pieceOverlayScale = 1f;

    private bool    _done;
    private float   _deflectedAt = float.NegativeInfinity;
    private Vector2 _lastVelocity;
    private Sprite  _itemSprite;

    private bool HasArt => splatFrames != null && splatFrames.Length > 0;

    private void Awake()
    {
        if (spriteRenderers.Count > 0 && spriteRenderers[0] != null)
            _itemSprite = spriteRenderers[0].sprite;

        if (TryGetComponent(out KickResponse kick))
            kick.KickOverride = OnKicked;
    }

    private void FixedUpdate()
    {
        if (rb2d != null) _lastVelocity = rb2d.linearVelocity;

        // Fell below the view without hitting anything: lost.
        if (Launched && !_done && transform.position.y < GameplayCamera.BottomY(float.NegativeInfinity) - 2f)
            Finish();
    }

    protected override void Launch()
    {
        base.Launch();
        if (HasArt && flyingFrame < splatFrames.Length && spriteRenderers.Count > 0)
            spriteRenderers[0].sprite = splatFrames[flyingFrame];
    }

    protected override void OnProjectileHit(Collision2D collision)
    {
        if (_done) return;

        Rigidbody2D hitBody = collision.collider.attachedRigidbody;

        if (hitBody != null && hitBody.TryGetComponent(out PlayerController victim))
        {
            if (Time.time - _deflectedAt < deflectGraceSeconds) return;
            if (!victim.isOnGame) return;

            StickPlayer(victim);
            Finish();
            return;
        }

        BlockScript piece = collision.collider.GetComponentInParent<BlockScript>();
        if (piece != null && piece.gameObject.activeInHierarchy)
        {
            CoverPiece(piece);
            Finish();
            return;
        }

        // A wall or the floor: it does not stick, it bounces on.
        Vector2 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector2.up;
        rb2d.linearVelocity = Vector2.Reflect(_lastVelocity, normal) * bounceKeep;

        if (Mathf.Abs(normal.y) > 0.5f && rb2d.linearVelocity.magnitude < loseBelowSpeed)
            Finish();
    }

    /// <summary>A kick in flight sends it the other way, at full throw speed.</summary>
    private bool OnKicked(Vector2 impulse)
    {
        if (!Launched || _done) return true;

        float sign = impulse.x != 0f ? Mathf.Sign(impulse.x) : 1f;
        float rad  = launchAngle * Mathf.Deg2Rad;
        rb2d.linearVelocity = new Vector2(Mathf.Cos(rad) * sign, Mathf.Sin(rad)) * throwSpeed;
        _deflectedAt = Time.time;
        return true;
    }

    private void CoverPiece(BlockScript piece)
    {
        if (!piece.TryGetComponent(out MocoPiece moco))
            moco = piece.gameObject.AddComponent<MocoPiece>();

        moco.Apply(splatFrames, splatFps, pieceFillFrom, pieceLoopFrom,
                   pieceOverlayOffset, pieceOverlayScale, StickPlayer);
    }

    /// <summary>Glues a chicken in place with this moco's values. Also called later by a mucous piece.</summary>
    private void StickPlayer(PlayerController victim)
    {
        if (victim == null) return;
        if (!victim.TryGetComponent(out MocoStuckState state))
            state = victim.gameObject.AddComponent<MocoStuckState>();
        if (state.IsActive || state.HasReleaseImmunity) return;

        state.Activate(
            strugglePresses,
            kickStrugglePoints,
            moveStrugglePoints,
            struggleDrainPerSecond,
            maxStuckSeconds,
            moveYank,
            kickYank,
            yankDecay,
            stuckTint);
    }

    private void Finish()
    {
        _done = true;
        gameObject.SetActive(false);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _done        = false;
        _deflectedAt = float.NegativeInfinity;
        if (spriteRenderers.Count > 0 && spriteRenderers[0] != null && _itemSprite != null)
            spriteRenderers[0].sprite = _itemSprite;
    }
}
