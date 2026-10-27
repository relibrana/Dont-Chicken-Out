using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One tab of the settings panel (Audio, Pantalla...). Holds only its own
/// options: SettingsPanel owns opening, closing, tabs, Back and Cancel, and
/// links this section's first and last option to the tab bar and Back button
/// for gamepad navigation. To add a tab: a new subclass + a Tab entry in the
/// panel (see SettingsPanelBuilder).
/// </summary>
public abstract class SettingsSection : MonoBehaviour
{
    /// <summary>Gets the focus when the tab opens; ↑ from it goes to the tab bar.</summary>
    public abstract Selectable First { get; }

    /// <summary>↓ from it goes to Back.</summary>
    public abstract Selectable Last { get; }

    /// <summary>Called every time the tab is shown: reload the current values.</summary>
    public virtual void OnShown() { }
}
