using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BombItem : HoldableItem
{
    [Header("Explosion")]
    [SerializeField, Min(0f), Tooltip("El tiempo (en segundos) que tarda la bomba en explotar después de activar el fusible.")]
    private float fuseSeconds = 1.25f;
    [SerializeField, Min(0f), Tooltip("El radio de la explosión que afecta a otros objetos.")]
    private float explosionRadius = 3f;
    [SerializeField, Min(0f), Tooltip("La intensidad del impulso aplicado a los objetos afectados por la explosión.")] 
    private float explosionImpulse = 12f;

    [Header("Detection"), Tooltip("Las capas que la explosión puede afectar.")]
    [SerializeField] private LayerMask detectLayer;

    [Header("Visual Feedback (SpriteRenderer)")]
    [SerializeField] private Color startColor = Color.white;
    [SerializeField] private Color endColor = Color.red;

    [Header("Optional FX")]
    [SerializeField] private ParticleSystem[] explosionParticles;
    [SerializeField] private AudioClip explosionSfx;

    private SpriteRenderer[] _spriteRenderers;
    private Coroutine _fuseRoutine;

    private readonly HashSet<Rigidbody2D> _uniqueBodies = new();

    private void Awake()
    {
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
    }

//     private void Update()
//     {

// #if UNITY_EDITOR
//         if (Input.GetKeyDown(KeyCode.K))
//         {
//             StartFuse();
//         }
// #endif
//     }

    public override void PlaceHoldable()
    {
        base.PlaceHoldable();
        StartFuse();
    }

    private void StartFuse()
    {
        if (_fuseRoutine != null)
            StopCoroutine(_fuseRoutine);

        _fuseRoutine = StartCoroutine(FuseRoutine());
    }

    private IEnumerator FuseRoutine()
    {
        SetSpriteColor(startColor);

        float elapsed = 0f;
        float fuseSafe = Mathf.Max(0.0001f, fuseSeconds);

        while (elapsed < fuseSafe)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fuseSafe);

            SetSpriteColor(Color.Lerp(startColor, endColor, t));
            yield return null;
        }

        Explode();
    }

    private void SetSpriteColor(Color color)
    {
        if (_spriteRenderers == null) return;

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (sr != null)
                sr.color = color;
        }
    }

    private void Explode()
    {
        PlayExplosionFx();

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            (Vector2)transform.position,
            explosionRadius,
            detectLayer
        );

        _uniqueBodies.Clear();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null) continue;

            if (col.CompareTag("Block"))
            {
                Debug.Log("BombItem.Explode: Disabling block " + col.name);
                DisableBlock(col);
                continue;
            }

            Rigidbody2D targetRb = col.attachedRigidbody;
            if (targetRb == null) continue;
            if (!_uniqueBodies.Add(targetRb)) continue;

            Vector2 toTarget = (Vector2)(targetRb.transform.position - transform.position);
            float distance = toTarget.magnitude;

            Vector2 direction = distance > 0.0001f ? toTarget.normalized : Vector2.up;

            float attenuation = Mathf.Clamp01(1f - (distance / explosionRadius));
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

        Destroy(gameObject);
    }

    private static void DisableBlock(Collider2D col)
    {
        col.gameObject.SetActive(false);
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
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
#endif
}
