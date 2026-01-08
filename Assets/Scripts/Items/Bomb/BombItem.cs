using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BombItem : HoldableItem
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

    [Header("Optional FX")]
    [SerializeField] private ParticleSystem[] explosionParticles;
    [SerializeField] private AudioClip explosionSfx;

    private SpriteRenderer[] cachedSpriteRenderers;
    private Coroutine fuseRoutine;

    private bool hasExploded;

    private readonly HashSet<Rigidbody2D> uniqueBodies = new HashSet<Rigidbody2D>();

    private static readonly int PrepareHash = Animator.StringToHash("Prepare");
    private static readonly int BoomHash = Animator.StringToHash("Boom");

    private void Awake()
    {
        cachedSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
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

    public override void PlaceHoldable()
    {
        base.PlaceHoldable();

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

        SetSpriteColor(startColor);
    }

    private IEnumerator FuseRoutine()
    {
        float elapsed = 0f;
        float fuseSafe = Mathf.Max(0.0001f, fuseSeconds);

        while (elapsed < fuseSafe)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fuseSafe);
            SetSpriteColor(Color.Lerp(startColor, endColor, t));
            yield return null;
        }

        Explosion();

        if (destroyDelay > 0f)
            yield return new WaitForSeconds(destroyDelay);

        Destroy(gameObject);
    }

    private void Explosion()
    {
        if (hasExploded) return;
        hasExploded = true;

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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(explosionPoint.position, explosionRadius);
    }
#endif
}
