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
///
/// Warning icon (Aviso-YunqueFall, nov 2026): at the top edge of the screen
/// over the column, fades in with the arrows and pulses while it is up. Drawn
/// on the Items layer so blocks at the top of the view never hide it.
/// </summary>
public sealed class AnvilZone : MonoBehaviour
{
    [SerializeField] private SpriteRenderer column;
    [SerializeField] private SpriteRenderer arrows;

    [SerializeField, Min(0f), Tooltip("Fundido de salida cuando el yunque ya pasó.")]
    private float closeSeconds = 0.2f;

    [Header("Icono de aviso")]
    [SerializeField, Tooltip("Icono arriba de la columna. Vacío = sin icono.")]
    private Sprite warningIcon;

    [SerializeField] private string iconSortingLayer = "Items";
    [SerializeField] private int    iconSortingOrder = 100;

    [SerializeField, Min(0f), Tooltip("Distancia del centro del icono al borde superior de la pantalla.")]
    private float iconMarginTop = 0.9f;

    [SerializeField, Min(0.01f)] private float iconScale = 1f;

    [SerializeField, Min(0f), Tooltip("Velocidad del pulso (rad/s).")]
    private float iconPulseSpeed = 9f;

    [SerializeField, Range(0f, 0.5f), Tooltip("Cuánto crece en cada pulso.")]
    private float iconPulseAmount = 0.12f;

    private bool _closing;
    private SpriteRenderer _icon;

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

    private void LateUpdate()
    {
        FollowCamera();

        if (_icon != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * iconPulseSpeed) * iconPulseAmount;
            _icon.transform.localScale = Vector3.one * (iconScale * pulse);
        }
    }

    private void FollowCamera()
    {
        Camera cam = GameplayCamera.Get();
        if (cam == null) return;

        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, cam.transform.position.y, p.z);

        if (_icon != null && cam.orthographic)
            _icon.transform.localPosition = new Vector3(0f, cam.orthographicSize - iconMarginTop, 0f);
    }

    private void CreateIcon()
    {
        if (warningIcon == null || _icon != null) return;

        var go = new GameObject("WarningIcon");
        go.transform.SetParent(transform, false);

        _icon = go.AddComponent<SpriteRenderer>();
        _icon.sprite         = warningIcon;
        _icon.sortingLayerID = SortingLayer.NameToID(iconSortingLayer);
        _icon.sortingOrder   = iconSortingOrder;
        go.transform.localScale = Vector3.one * iconScale;
        FollowCamera();
    }

    private IEnumerator OpenRoutine(float openSeconds, float arrowsFadeSeconds)
    {
        CreateIcon();
        SetAlpha(arrows, 0f);
        SetAlpha(_icon, 0f);

        for (float t = 0f; t < openSeconds; t += Time.deltaTime)
        {
            SetColumnWidth(t / openSeconds);
            yield return null;
        }
        SetColumnWidth(1f);

        for (float t = 0f; t < arrowsFadeSeconds; t += Time.deltaTime)
        {
            SetAlpha(arrows, t / arrowsFadeSeconds);
            SetAlpha(_icon, t / arrowsFadeSeconds);
            yield return null;
        }
        SetAlpha(arrows, 1f);
        SetAlpha(_icon, 1f);
    }

    private IEnumerator CloseRoutine()
    {
        float columnAlpha = column != null ? column.color.a : 1f;
        float arrowsAlpha = arrows != null ? arrows.color.a : 1f;
        float iconAlpha   = _icon  != null ? _icon.color.a  : 1f;

        for (float t = 0f; t < closeSeconds; t += Time.deltaTime)
        {
            float k = 1f - t / closeSeconds;
            SetAlpha(column, columnAlpha * k);
            SetAlpha(arrows, arrowsAlpha * k);
            SetAlpha(_icon, iconAlpha * k);
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
