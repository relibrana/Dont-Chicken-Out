using TMPro;
using UnityEngine;

/// <summary>
/// Marks one option row of a menu panel (label + control + value). The
/// SelectionHighlight frames the whole row, not just the slider, and paints
/// its label in the accent colour while the row's control has the focus.
/// </summary>
public sealed class SettingsRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;

    private Color _idleColor;
    private bool  _hasIdleColor;

    public void SetFocused(bool focused, Color accent)
    {
        if (label == null) return;

        if (!_hasIdleColor)
        {
            _idleColor    = label.color;
            _hasIdleColor = true;
        }

        label.color = focused ? accent : _idleColor;
    }
}
