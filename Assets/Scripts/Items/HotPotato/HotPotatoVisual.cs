using System.Collections;
using UnityEngine;

/// <summary>
/// The hot potato you can see (design, oct 2026: chicken states become visible
/// items). Floats over the carrier's head and "breathes" like the bomb, faster
/// as the shared fuse runs out. On detonation it leaves the carrier, plays the
/// bomb's explosion frames where the carrier stands and destroys itself.
///
/// Presentation only: HotPotatoState owns the fuse, the transfer and the blast.
/// It follows the carrier instead of being parented to it so the chicken's
/// facing flip (negative scale) never mirrors or rescales the potato.
/// </summary>
public sealed class HotPotatoVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Bombeo (mismos valores que la bomba)")]
    [SerializeField] private float breathStartHz        = 1.5f;
    [SerializeField] private float breathEndHz          = 9f;
    [SerializeField] private float breathStartAmplitude = 0.04f;
    [SerializeField] private float breathEndAmplitude   = 0.16f;

    [Header("Explosión (los cuadros de la bomba)")]
    [SerializeField, Tooltip("Anim_Bomba_5 … Anim_Bomba_11.")]
    private Sprite[] explosionFrames;
    [SerializeField] private float explosionFps   = 12f;
    [SerializeField] private float shakeDuration  = 0.25f;
    [SerializeField] private float shakeAmplitude = 0.55f;

    private Transform _carrier;
    private Vector3   _offset;
    private Vector3   _baseScale;
    private float     _breathPhase;
    private bool      _detonated;

    public static HotPotatoVisual Spawn(HotPotatoVisual prefab, Transform carrier, Vector3 offset)
    {
        if (prefab == null || carrier == null) return null;

        HotPotatoVisual visual = Instantiate(prefab);
        visual._carrier = carrier;
        visual._offset  = offset;
        visual.FollowCarrier();
        return visual;
    }

    private void Awake() => _baseScale = transform.localScale;

    private void LateUpdate()
    {
        if (!_detonated) FollowCarrier();
    }

    private void FollowCarrier()
    {
        if (_carrier != null)
            transform.position = _carrier.position + _offset;
    }

    /// <summary>
    /// Bomb-style pulse. <paramref name="urgency"/> goes 0 → 1 over the whole
    /// fuse window, so a hand-off keeps the same rhythm.
    /// </summary>
    public void Pulse(float urgency)
    {
        if (_detonated) return;

        float hz        = Mathf.Lerp(breathStartHz, breathEndHz, urgency);
        float amplitude = Mathf.Lerp(breathStartAmplitude, breathEndAmplitude, urgency);

        _breathPhase += hz * Time.deltaTime;
        float pulse = Mathf.Sin(_breathPhase * 2f * Mathf.PI);

        // Wider than taller when it swells, like the bomb: reads as air, not zoom.
        transform.localScale = new Vector3(
            _baseScale.x * (1f + pulse * amplitude),
            _baseScale.y * (1f + pulse * amplitude * 0.6f),
            _baseScale.z);
    }

    /// <summary>The potato vanishes and the bomb's explosion plays at <paramref name="position"/>.</summary>
    public void Detonate(Vector3 position)
    {
        if (_detonated) return;
        _detonated = true;

        _carrier             = null;
        transform.position   = position;
        transform.localScale = _baseScale;

        ScreenShake.Punch(shakeDuration, shakeAmplitude);
        StartCoroutine(PlayExplosion());
    }

    /// <summary>The potato left this carrier without exploding (hand-off, round reset).</summary>
    public void Discard()
    {
        if (_detonated) return;
        Destroy(gameObject);
    }

    private IEnumerator PlayExplosion()
    {
        if (spriteRenderer != null && explosionFrames != null && explosionFrames.Length > 0)
        {
            var frame = new WaitForSeconds(1f / Mathf.Max(1f, explosionFps));
            foreach (Sprite sprite in explosionFrames)
            {
                spriteRenderer.sprite = sprite;
                yield return frame;
            }
        }

        Destroy(gameObject);
    }
}
