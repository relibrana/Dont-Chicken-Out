using System.Collections;
using UnityEngine;

/// <summary>
/// Telegraph of the anvil's column (design, oct 2026). Opens a full-height
/// column over the target X (Zona-Yunque, scale X 0 → 1) and fades the arrows
/// in on top (Zona-YunqueFlecha). Presentation only: AnvilFall counts the
/// warning on the tick and decides when it falls. Drawn on the Bg layer above
/// every background sprite and behind the floor.
///
/// Spans the whole view (the art is 1080 px = the camera's 10.8 units), so it
/// follows the camera vertically while it is up. Closes when the anvil leaves.
/// </summary>
public sealed class AnvilZone : MonoBehaviour
{
    [SerializeField] private SpriteRenderer column;
    [SerializeField] private SpriteRenderer arrows;

    [SerializeField, Min(0f), Tooltip("Fundido de salida cuando el yunque ya pasó.")]
    private float closeSeconds = 0.2f;

    private bool _closing;

    public static AnvilZone Spawn(AnvilZone prefab, float x)
    {
        if (prefab == null) return null;

        AnvilZone zone = Instantiate(prefab, new Vector3(x, 0f, 0f), Quaternion.identity);
        zone.FollowCamera();
        return zone;
    }

    /// <summary>Column opens, then arrows fade in. Only draws: the anvil falls on its own timer.</summary>
    public void Open(float openSeconds, float arrowsFadeSeconds)
        => StartCoroutine(OpenRoutine(openSeconds, arrowsFadeSeconds));

    public void Close()
    {
        if (_closing || this == null) return;
        _closing = true;
        StopAllCoroutines();
        StartCoroutine(CloseRoutine());
    }

    private void LateUpdate() => FollowCamera();

    private void FollowCamera()
    {
        Camera cam = GameplayCamera.Get();
        if (cam == null) return;

        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, cam.transform.position.y, p.z);
    }

    private IEnumerator OpenRoutine(float openSeconds, float arrowsFadeSeconds)
    {
        SetAlpha(arrows, 0f);

        for (float t = 0f; t < openSeconds; t += Time.deltaTime)
        {
            SetColumnWidth(t / openSeconds);
            yield return null;
        }
        SetColumnWidth(1f);

        for (float t = 0f; t < arrowsFadeSeconds; t += Time.deltaTime)
        {
            SetAlpha(arrows, t / arrowsFadeSeconds);
            yield return null;
        }
        SetAlpha(arrows, 1f);
    }

    private IEnumerator CloseRoutine()
    {
        float columnAlpha = column != null ? column.color.a : 1f;
        float arrowsAlpha = arrows != null ? arrows.color.a : 1f;

        for (float t = 0f; t < closeSeconds; t += Time.deltaTime)
        {
            float k = 1f - t / closeSeconds;
            SetAlpha(column, columnAlpha * k);
            SetAlpha(arrows, arrowsAlpha * k);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void SetColumnWidth(float k)
    {
        if (column == null) return;
        column.transform.localScale = new Vector3(Mathf.Clamp01(k), 1f, 1f);
    }

    private static void SetAlpha(SpriteRenderer sr, float alpha)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = Mathf.Clamp01(alpha);
        sr.color = c;
    }
}
