using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bomba (GDD, nov 2026). Thrown in the common arc; it bounces off chickens
/// and tires and gets stuck (fixed in the air, solid, a platform) on the
/// first piece, capsule or wall it hits. There it charges and then blows up:
/// every piece it touches breaks whole, every chicken inside dies (except the
/// metal one), and capsules, tires and other charging bombs break.
/// A kick slides it (it bounces off walls and pieces and keeps charging) and
/// it sticks again where it stops. Thrown off-camera without hitting
/// anything, it goes off on its own after a few seconds.
/// </summary>
[DisallowMultipleComponent]
public sealed class BombItem : ThrowableItem
{
    [Header("Explosion")]
    [SerializeField, Tooltip("Transform del punto de explosión")]
    private Transform explosionPoint;

    [SerializeField, Min(0f), Tooltip("Tiempo (segundos) desde que se activa el fusible hasta que explota.")]
    private float fuseSeconds = 1.25f;

    [SerializeField, Min(0f), Tooltip("Radio de la explosión.")]
    private float explosionRadius = 3f;

    [SerializeField, Min(0f), Tooltip("Impulso máximo aplicado a los cuerpos afectados.")]
    private float explosionImpulse = 12f;

    [SerializeField, Min(0f), Tooltip("Delay para devolverla al pool luego de explotar (para que se vea el final de la animación).")]
    private float destroyDelay = 0.5f;

    [Header("Detection")]
    [SerializeField, Tooltip("Capas afectadas por la explosión.")]
    private LayerMask detectLayer;

    [Header("Visual Feedback")]
    [SerializeField] private Color startColor = Color.white;
    [SerializeField] private Color endColor = Color.red;

    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Throw")]
    [SerializeField, Tooltip("La mecha arranca al lanzarla. Si se desmarca, arranca al primer impacto.")]
    private bool fuseStartsOnThrow = true;

    [Header("Fijarse, patada y fuera de cámara (GDD)")]
    [SerializeField, Min(0.5f), Tooltip("Si sale por debajo de la cámara sin haber chocado, explota sola a los X segundos.")]
    private float offCameraSeconds = 3.5f;

    [SerializeField, Min(0f), Tooltip("Fracción de la patada que la desliza. Corta: patearla es una apuesta, no una salida.")]
    private float kickPushScale = 0.45f;

    [SerializeField, Min(0f), Tooltip("Rozamiento mientras se desliza tras una patada.")]
    private float slideDamping = 3f;

    [SerializeField, Min(0.01f), Tooltip("Por debajo de esta velocidad deja de deslizarse y vuelve a quedarse fija.")]
    private float refixSpeed = 0.35f;

    [SerializeField, Range(0f, 1f), Tooltip("Cuánto rebota en paredes y piezas mientras se desliza.")]
    private float slideBounciness = 0.6f;

    [Header("Feedback: respiración")]
    [SerializeField, Tooltip("Transform que se infla y desinfla. Vacío = el sprite de la bomba.")]
    private Transform breathingVisual;

    [SerializeField, Min(0f), Tooltip("Respiraciones por segundo al encender la mecha.")]
    private float breathStartHz = 1.5f;

    [SerializeField, Min(0f), Tooltip("Respiraciones por segundo justo antes de estallar.")]
    private float breathEndHz = 9f;

    [SerializeField, Range(0f, 0.5f), Tooltip("Cuánto se deforma al principio (0.04 = 4%).")]
    private float breathStartAmplitude = 0.04f;

    [SerializeField, Range(0f, 0.5f), Tooltip("Cuánto se deforma al final.")]
    private float breathEndAmplitude = 0.16f;

    [Header("Feedback: mecha")]
    [SerializeField, Tooltip("Mecha procedural (placeholder). Desmarcar cuando Arte entregue la suya.")]
    private bool useProceduralFuse = true;

    [SerializeField, Tooltip("Origen de la mecha en local, relativo a la bomba.")]
    private Vector2 fuseAnchor = new Vector2(0f, 0.35f);

    [SerializeField, Min(0.05f), Tooltip("Largo de la mecha entera, en unidades.")]
    private float fuseLength = 0.5f;

    [SerializeField, Min(0.01f)] private float fuseWidth = 0.06f;
    [SerializeField] private Color fuseColor  = new Color(0.32f, 0.25f, 0.18f, 1f);
    [SerializeField] private Color sparkColor = new Color(1f, 0.88f, 0.35f, 1f);

    [Header("Feedback: explosión")]
    [SerializeField, Min(0f), Tooltip("Duración del sacudón de cámara. Corto y fuerte.")]
    private float shakeDuration = 0.25f;

    [SerializeField, Min(0f), Tooltip("Amplitud del sacudón, en unidades de mundo.")]
    private float shakeAmplitude = 0.55f;

    [Header("Optional FX")]
    [SerializeField] private ParticleSystem[] explosionParticles;

    private SpriteRenderer[] cachedSpriteRenderers;
    private Color[] originalSpriteColors;
    private float landedGravityScale = 1f;
    private float landedLinearDamping;

    // Breathing / fuse visuals.
    private Transform    _breathTarget;
    private Vector3      _breathBaseScale = Vector3.one;
    private float        _breathPhase;
    private LineRenderer _fuseLine;

    private static Material _sharedFuseMaterial;

    // Simulation state, advanced by the tick only (same split as PlayerItemState):
    // the fuse and the despawn after the blast count in FixedUpdate so the bomb
    // goes off on the same tick on every machine. Update only draws.
    private bool  fuseLit;
    private float fuseRemaining;
    private bool  hasExploded;
    private float despawnRemaining;

    private readonly HashSet<Rigidbody2D> uniqueBodies = new HashSet<Rigidbody2D>();

    // Fixed in place / sliding after a kick / seconds off-camera untouched.
    private bool    _fixed;
    private bool    _sliding;
    private float   _offCameraTime;
    private Vector2 _lastVelocity;
    private PhysicsMaterial2D   _slideMaterial;
    private PhysicsMaterial2D[] _restMaterials;

    private static readonly int PrepareHash = Animator.StringToHash("Prepare");
    private static readonly int BoomHash = Animator.StringToHash("Boom");

    private void Awake()
    {
        cachedSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);

        // The fuse tints every sprite; a pooled bomb has to come back with its own colours.
        originalSpriteColors = new Color[cachedSpriteRenderers.Length];
        for (int i = 0; i < cachedSpriteRenderers.Length; i++)
            originalSpriteColors[i] = cachedSpriteRenderers[i].color;

        // El lanzamiento pisa la gravedad con la del proyectil; se restaura
        // al aterrizar para que la bomba caída pese lo mismo que siempre.
        landedGravityScale = rb2d != null ? rb2d.gravityScale : 1f;

        // Lo mismo con el rozamiento: el de la bomba en el suelo es muy alto
        // (para que una patada no la mande rodando lejos) y en el aire frenaba
        // el lanzamiento en seco. Vuela sin él y lo recupera al tocar algo.
        landedLinearDamping = rb2d != null ? rb2d.linearDamping : 0f;

        _breathTarget = breathingVisual != null
            ? breathingVisual
            : (cachedSpriteRenderers.Length > 0 ? cachedSpriteRenderers[0].transform : transform);

        _breathBaseScale = _breathTarget.localScale;

        if (TryGetComponent(out KickResponse kick))
            kick.KickOverride = OnKicked;

        _slideMaterial = new PhysicsMaterial2D("BombSlide") { bounciness = slideBounciness, friction = 0f };
        _restMaterials = new PhysicsMaterial2D[colliders.Count];
        for (int i = 0; i < colliders.Count; i++)
            _restMaterials[i] = colliders[i] != null ? colliders[i].sharedMaterial : null;
    }

    private void FixedUpdate()
    {
        if (rb2d != null) _lastVelocity = rb2d.linearVelocity;

        if (_sliding && !hasExploded && rb2d.linearVelocity.magnitude < refixSpeed)
            Fix();

        // Thrown below the view without touching anything: it does not charge,
        // it just goes off after a while wherever it is (nobody may see it).
        if (Launched && !_fixed && !_sliding && !fuseLit && !hasExploded
            && transform.position.y < GameplayCamera.BottomY(float.NegativeInfinity))
        {
            _offCameraTime += Time.fixedDeltaTime;
            if (_offCameraTime >= offCameraSeconds)
            {
                Explosion();
                despawnRemaining = destroyDelay;
                return;
            }
        }

        if (hasExploded)
        {
            despawnRemaining -= Time.fixedDeltaTime;
            if (despawnRemaining <= 0f)
                gameObject.SetActive(false); // back to the pool; OnDisable resets it
            return;
        }

        if (!fuseLit) return;

        fuseRemaining -= Time.fixedDeltaTime;
        if (fuseRemaining <= 0f)
        {
            Explosion();
            despawnRemaining = destroyDelay;
        }
    }

    protected override void Update()
    {
        base.Update();

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.K))
        {
            StartFuse();
        }
#endif

        if (fuseLit && !hasExploded)
            RenderFuse();
    }

    /// <summary>
    /// La bomba se lanza en vez de colocarse (diseño, sep 2026: es un ítem
    /// lanzable como el moco). base.PlaceHoldable() de ThrowableItem ya hace
    /// el disparo; aquí sólo se decide cuándo empieza a correr la mecha.
    /// </summary>
    public override void PlaceHoldable()
    {
        base.PlaceHoldable();

        if (fuseStartsOnThrow)
            TryStartFuse();
    }

    /// <summary>
    /// Primer impacto tras el lanzamiento. La bomba NO se queda clavada como
    /// el moco: sigue siendo un cuerpo físico normal para que los rivales
    /// puedan patearla lejos, que es su counterplay de siempre.
    /// </summary>
    protected override void Launch()
    {
        base.Launch();
        rb2d.linearDamping = 0f;
    }

    protected override void OnProjectileHit(Collision2D collision)
    {
        if (_fixed || _sliding || hasExploded) return;

        // In flight it goes through nothing: chickens and tires send it
        // bouncing on (it keeps flying); anything else fixes it.
        Rigidbody2D body = collision.rigidbody;
        if (body != null && (body.TryGetComponent(out PlayerController _) || body.TryGetComponent(out Tire _)))
        {
            Vector2 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector2.up;
            rb2d.linearVelocity = Vector2.Reflect(_lastVelocity, normal);
            return;
        }

        Fix();
        if (!fuseStartsOnThrow)
            TryStartFuse();
    }

    /// <summary>Suspended and fixed where it is, not hanging on what it hit: solid, a platform.</summary>
    private void Fix()
    {
        _fixed   = true;
        _sliding = false;

        rb2d.linearVelocity  = Vector2.zero;
        rb2d.angularVelocity = 0f;
        rb2d.bodyType        = RigidbodyType2D.Kinematic;
        rb2d.gravityScale    = 0f;
        rb2d.linearDamping   = landedLinearDamping;

        for (int i = 0; i < colliders.Count; i++)
            if (colliders[i] != null) colliders[i].sharedMaterial = _restMaterials[i];
    }

    /// <summary>
    /// A kick slides the fixed bomb along the kick (bouncing off walls and
    /// pieces, pushing chickens) and the charge goes on. What sat on top does
    /// not ride along: it loses its support. In flight a kick acts as usual.
    /// </summary>
    private bool OnKicked(Vector2 impulse)
    {
        if (hasExploded) return true;
        if (!_fixed && !_sliding) return false;

        _fixed   = false;
        _sliding = true;

        rb2d.bodyType      = RigidbodyType2D.Dynamic;
        rb2d.gravityScale  = 0f;
        rb2d.linearDamping = slideDamping;
        foreach (Collider2D col in colliders)
            if (col != null) col.sharedMaterial = _slideMaterial;

        rb2d.linearVelocity = new Vector2(impulse.x * kickPushScale, 0f);
        return true;
    }

    /// <summary>Broken by another blast or by the anvil while charging: no explosion (GDD).</summary>
    public void Defuse()
    {
        if (hasExploded || !gameObject.activeInHierarchy) return;
        if (!Launched) return; // still waiting in a slot: not in the world
        gameObject.SetActive(false);
    }

    private void TryStartFuse()
    {
        if (!fuseLit && !hasExploded)
            StartFuse();
    }

    private void StartFuse()
    {
        if (hasExploded) return;

        Prepare();

        fuseLit       = true;
        fuseRemaining = Mathf.Max(0.0001f, fuseSeconds);
        _breathPhase  = 0f;
        EnsureFuseLine();
    }

    private void Prepare()
    {
        if (animator != null)
            animator.SetTrigger(PrepareHash);
        
        SimEvents.Raise(SimEvent.BombFuseLit, transform.position, this);

        SetSpriteColor(startColor);
    }

    /// <summary>
    /// Draws the fuse from the last tick's value minus the time since that
    /// tick (same as PlayerItemState.RenderRemaining). Never decides anything.
    /// </summary>
    private void RenderFuse()
    {
        float fuseSafe        = Mathf.Max(0.0001f, fuseSeconds);
        float renderRemaining = Mathf.Max(0f, fuseRemaining - (Time.time - Time.fixedTime));
        float t               = 1f - Mathf.Clamp01(renderRemaining / fuseSafe);

        SetSpriteColor(Color.Lerp(startColor, endColor, t));
        TickBreathing(t);
        TickFuse(t);
    }

    private void Explosion()
    {
        if (hasExploded) return;
        hasExploded = true;
        fuseLit     = false;
        
		rb2d.bodyType = RigidbodyType2D.Kinematic;
        
        ResetBreathing();
        HideFuse();

        SimEvents.Raise(SimEvent.BombFuseOut, transform.position, this);
        SimEvents.Raise(SimEvent.BombExploded, explosionPoint.position, this);

        ScreenShake.Punch(shakeDuration, shakeAmplitude);

        if (animator != null)
            animator.SetTrigger(BoomHash);

        PlayExplosionFx();

        for (int i = 0; i < colliders.Count; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        BreakItemsInBlast();

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            (Vector2)explosionPoint.position,
            explosionRadius,
            detectLayer
        );

        uniqueBodies.Clear();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null) continue;

            if (col.CompareTag("Block"))
            {
                DisableBlock(col);
                continue;
            }

            Rigidbody2D targetRb = col.attachedRigidbody;
            if (targetRb == null) continue;
            if (!uniqueBodies.Add(targetRb)) continue;

            Vector2 toTarget = (Vector2)(targetRb.transform.position - transform.position);
            float distance = toTarget.magnitude;

            Vector2 direction = distance > 0.0001f ? (toTarget / distance) : Vector2.up;

            float radiusSafe = Mathf.Max(0.0001f, explosionRadius);
            float attenuation = Mathf.Clamp01(1f - (distance / radiusSafe));
            float impulse = explosionImpulse * attenuation;

            if (col.TryGetComponent<PlayerController>(out var player))
            {
                // The metal chicken survives the blast (GDD).
                if (!ExplosionBlast.IsMetal(player))
                    player.OnDeath();
            }
            else
            {
                targetRb.AddForce(direction * impulse, ForceMode2D.Impulse);
            }
        }
    }

    /// <summary>Capsules (item lost), tires and charging bombs in the blast break too (GDD).</summary>
    private void BreakItemsInBlast() =>
        ExplosionBlast.BreakItems(explosionPoint.position, explosionRadius, this);

    private static void DisableBlock(Collider2D col)
    {
        // GDD: the blast breaks every piece it touches whole, whatever its
        // life. Through the piece, so it also goes back to the pool.
        if (col.TryGetComponent(out BlockDamageable block))
        {
            block.TakeDamage(int.MaxValue, null);
            return;
        }

        Collider2D[] colliders2D = col.GetComponentsInChildren<Collider2D>(includeInactive: true);
        for (int i = 0; i < colliders2D.Length; i++)
            colliders2D[i].enabled = false;

        SpriteRenderer[] srs = col.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        for (int i = 0; i < srs.Length; i++)
            srs[i].enabled = false;
    }

    private void SetSpriteColor(Color color)
    {
        if (cachedSpriteRenderers == null) return;

        for (int i = 0; i < cachedSpriteRenderers.Length; i++)
        {
            SpriteRenderer sr = cachedSpriteRenderers[i];
            if (sr != null)
                sr.color = color;
        }
    }

    private void PlayExplosionFx()
    {
        if (explosionParticles != null)
        {
            for (int i = 0; i < explosionParticles.Length; i++)
            {
                ParticleSystem ps = explosionParticles[i];
                if (ps != null) ps.Play();
            }
        }

        // The explosion sound is the FMOD event raised in Explode (SimEvent.BombExploded).
    }

    private void OnEnable() => GameManager.RoundEnded += HandleRoundEnded;

    // A bomb already thrown must not go off over the win screen (it would kill
    // the winner) nor carry into the next round: it is defused on the spot.
    // One still in hand is left alone; the round reset drops it.
    private void HandleRoundEnded()
    {
        if (Launched && !hasExploded)
            gameObject.SetActive(false);
    }

    protected override void OnDisable() {
        base.OnDisable();
        GameManager.RoundEnded -= HandleRoundEnded;
        SimEvents.Raise(SimEvent.BombFuseOut, transform.position, this);

        // Back to the pool: a pooled bomb must come out unlit and whole.
        fuseLit        = false;
        hasExploded    = false;
        _fixed         = false;
        _sliding       = false;
        _offCameraTime = 0f;
        if (_restMaterials != null)
            for (int i = 0; i < colliders.Count && i < _restMaterials.Length; i++)
                if (colliders[i] != null) colliders[i].sharedMaterial = _restMaterials[i];

        for (int i = 0; i < colliders.Count; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = true;
        }

        for (int i = 0; i < cachedSpriteRenderers.Length; i++)
        {
            if (cachedSpriteRenderers[i] != null)
                cachedSpriteRenderers[i].color = originalSpriteColors[i];
        }

        // Dropped mid-flight (round reset): come back with its ground values.
        if (rb2d != null)
        {
            rb2d.gravityScale  = landedGravityScale;
            rb2d.linearDamping = landedLinearDamping;
        }

        ResetBreathing();
        HideFuse();
    }

    // ── Respiración ───────────────────────────────────────────────────────────

    /// <summary>
    /// La bomba se infla y se desinfla, y el ritmo se acelera a medida que se
    /// acaba la mecha: es el reloj visual del item. La fase se acumula en vez
    /// de calcularse como Time.time * frecuencia, porque al subir la
    /// frecuencia sobre un reloj absoluto el seno pega saltos.
    /// </summary>
    private void TickBreathing(float t)
    {
        if (_breathTarget == null) return;

        float hz        = Mathf.Lerp(breathStartHz, breathEndHz, t);
        float amplitude = Mathf.Lerp(breathStartAmplitude, breathEndAmplitude, t);

        _breathPhase += hz * Time.deltaTime;

        float pulse = Mathf.Sin(_breathPhase * 2f * Mathf.PI);

        // Con algo de squash: al hincharse se ensancha más de lo que crece a lo
        // alto, que es lo que hace que se lea como aire y no como un zoom.
        _breathTarget.localScale = new Vector3(
            _breathBaseScale.x * (1f + pulse * amplitude),
            _breathBaseScale.y * (1f + pulse * amplitude * 0.6f),
            _breathBaseScale.z);
    }

    private void ResetBreathing()
    {
        if (_breathTarget != null)
            _breathTarget.localScale = _breathBaseScale;
    }

    // ── Mecha y chispa ────────────────────────────────────────────────────────

    /// <summary>
    /// Mecha placeholder por código: una línea que se acorta desde la punta
    /// mientras la chispa la consume. Se sustituye por el asset de Arte
    /// apagando Use Procedural Fuse.
    /// </summary>
    private void EnsureFuseLine()
    {
        if (!useProceduralFuse || _fuseLine != null) return;

        var go = new GameObject("FuseProcedural");
        go.transform.SetParent(transform, false);

        _fuseLine = go.AddComponent<LineRenderer>();
        _fuseLine.useWorldSpace  = false;
        _fuseLine.positionCount  = 2;
        _fuseLine.numCapVertices = 2;
        _fuseLine.textureMode    = LineTextureMode.Stretch;
        _fuseLine.startWidth     = fuseWidth;
        _fuseLine.endWidth       = fuseWidth;
        _fuseLine.sortingOrder   = 10;

        if (_sharedFuseMaterial == null)
            _sharedFuseMaterial = new Material(Shader.Find("Sprites/Default"));

        _fuseLine.sharedMaterial = _sharedFuseMaterial;
    }

    private void TickFuse(float t)
    {
        if (_fuseLine == null) return;

        _fuseLine.enabled = true;

        // La mecha se consume desde la punta hacia la bomba.
        float remaining = fuseLength * (1f - t);
        Vector3 start   = fuseAnchor;
        Vector3 end     = start + Vector3.up * remaining;

        _fuseLine.SetPosition(0, start);
        _fuseLine.SetPosition(1, end);

        // La chispa parpadea al mismo ritmo que la respiración, para que los
        // dos feedbacks cuenten exactamente el mismo tiempo.
        float flicker = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(_breathPhase * 2f * Mathf.PI));

        _fuseLine.startColor = fuseColor;
        _fuseLine.endColor   = Color.Lerp(fuseColor, sparkColor, flicker);
    }

    private void HideFuse()
    {
        if (_fuseLine != null)
            _fuseLine.enabled = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(explosionPoint.position, explosionRadius);
    }
#endif
}
