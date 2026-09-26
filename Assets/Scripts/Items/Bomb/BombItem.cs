using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    [SerializeField, Min(0f), Tooltip("Delay para destruir luego de explotar (para que se vea el final de la animación).")]
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
    [SerializeField] private AudioClip explosionSfx;

    private SpriteRenderer[] cachedSpriteRenderers;
    private Coroutine fuseRoutine;
    private float landedGravityScale = 1f;

    // Breathing / fuse visuals.
    private Transform    _breathTarget;
    private Vector3      _breathBaseScale = Vector3.one;
    private float        _breathPhase;
    private LineRenderer _fuseLine;

    private static Material _sharedFuseMaterial;

    private bool hasExploded;

    private readonly HashSet<Rigidbody2D> uniqueBodies = new HashSet<Rigidbody2D>();

    private static readonly int PrepareHash = Animator.StringToHash("Prepare");
    private static readonly int BoomHash = Animator.StringToHash("Boom");

    private void Awake()
    {
        cachedSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);

        // El lanzamiento pisa la gravedad con la del proyectil; se restaura
        // al aterrizar para que la bomba caída pese lo mismo que siempre.
        landedGravityScale = rb2d != null ? rb2d.gravityScale : 1f;

        _breathTarget = breathingVisual != null
            ? breathingVisual
            : (cachedSpriteRenderers.Length > 0 ? cachedSpriteRenderers[0].transform : transform);

        _breathBaseScale = _breathTarget.localScale;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.K))
        {
            StartFuse();
        }
#endif
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
    protected override void OnProjectileHit(Collision2D collision)
    {
        rb2d.gravityScale = landedGravityScale;

        if (!fuseStartsOnThrow)
            TryStartFuse();
    }

    private void TryStartFuse()
    {
        if (fuseRoutine == null && !hasExploded)
            StartFuse();
    }

    private void StartFuse()
    {
        if (fuseRoutine != null)
            StopCoroutine(fuseRoutine);

        Prepare();
        fuseRoutine = StartCoroutine(FuseRoutine());
    }

    private void Prepare()
    {
        if (animator != null)
            animator.SetTrigger(PrepareHash);
        
        AudioManager.Instance.PlaySound("bomb_lighter");

        SetSpriteColor(startColor);
    }

    private IEnumerator FuseRoutine()
    {
        float elapsed = 0f;
        float fuseSafe = Mathf.Max(0.0001f, fuseSeconds);

        _breathPhase = 0f;
        EnsureFuseLine();

        while (elapsed < fuseSafe)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fuseSafe);

            SetSpriteColor(Color.Lerp(startColor, endColor, t));
            TickBreathing(t);
            TickFuse(t);

            yield return null;
        }

        ResetBreathing();
        HideFuse();

        Explosion();

        if (destroyDelay > 0f)
            yield return new WaitForSeconds(destroyDelay);

        Destroy(gameObject);
    }

    private void Explosion()
    {
        if (hasExploded) return;
        hasExploded = true;
        
		rb2d.bodyType = RigidbodyType2D.Kinematic;
        
        ResetBreathing();
        HideFuse();

        AudioManager.Instance.StopSound("bomb_lighter");

        // Paneado por su posición en pantalla: una bomba que estalla a la
        // izquierda suena por el altavoz izquierdo.
        AudioManager.Instance.PlaySoundAt("bomb_explosion", explosionPoint.position);

        ScreenShake.Punch(shakeDuration, shakeAmplitude);

        if (animator != null)
            animator.SetTrigger(BoomHash);

        PlayExplosionFx();

        for (int i = 0; i < colliders.Count; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

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
                player.OnDeath();
            }
            else
            {
                targetRb.AddForce(direction * impulse, ForceMode2D.Impulse);
            }
        }
    }

    private static void DisableBlock(Collider2D col)
    {
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

        if (explosionSfx != null)
            AudioSource.PlayClipAtPoint(explosionSfx, transform.position);
    }

    protected override void OnDisable() {
        base.OnDisable();
        AudioManager.Instance.StopSound("bomb_lighter");

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
