using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shows which option has the focus, so a gamepad player always knows where
/// they are: a softly pulsing bar slides behind the focused row (the whole
/// SettingsRow, or the control itself for tabs and buttons) and that row's
/// label takes the accent colour. Follows the EventSystem's selection, so it
/// works the same with gamepad, keyboard and mouse.
/// Lives on the panel window; the bar must be the window's first child so it
/// draws behind everything else.
/// </summary>
public sealed class SelectionHighlight : MonoBehaviour
{
    [SerializeField] private RectTransform bar;
    [SerializeField] private Image barImage;

    [SerializeField] private Color accent   = new(1f, 0.78f, 0.2f, 1f);
    [SerializeField] private Vector2 padding = new(24f, 10f);

    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.18f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.36f;
    [SerializeField, Min(0f)] private float pulseSpeed = 3f;
    [SerializeField, Min(1f)] private float followSpeed = 20f;

    private GameObject  _current;
    private SettingsRow _row;

    private readonly Vector3[] _corners = new Vector3[4];

    private void OnEnable()
    {
        _current = null;
        _row     = null;
        if (bar != null) bar.gameObject.SetActive(false);
    }

    private void OnDisable() => SetRow(null);

    private void LateUpdate()
    {
        if (bar == null) return;

        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null || !selected.activeInHierarchy || !selected.transform.IsChildOf(transform))
        {
            SetRow(null);
            _current = null;
            bar.gameObject.SetActive(false);
            return;
        }

        if (selected != _current)
        {
            _current = selected;
            SetRow(selected.GetComponentInParent<SettingsRow>());
        }

        var target = (RectTransform)(_row != null ? _row.transform : selected.transform);
        var parent = (RectTransform)bar.parent;

        target.GetWorldCorners(_corners);
        Vector2 min = parent.InverseTransformPoint(_corners[0]);
        Vector2 max = parent.InverseTransformPoint(_corners[2]);
        Vector2 center = (min + max) * 0.5f;
        Vector2 size   = max - min + padding;

        // Unscaled: the menu may be open while the game is paused.
        bool snap = !bar.gameObject.activeSelf;
        float k = snap ? 1f : 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
        bar.anchoredPosition = Vector2.Lerp(bar.anchoredPosition, center, k);
        bar.sizeDelta        = Vector2.Lerp(bar.sizeDelta, size, k);
        bar.gameObject.SetActive(true);

        if (barImage != null)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            Color c = accent;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, pulse);
            barImage.color = c;
        }
    }

    private void SetRow(SettingsRow row)
    {
        if (_row == row) return;
        if (_row != null) _row.SetFocused(false, accent);
        _row = row;
        if (_row != null) _row.SetFocused(true, accent);
    }
}
