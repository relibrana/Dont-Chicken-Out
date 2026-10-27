using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// The main menu's settings window: a tab bar (Audio, Pantalla...) over one
/// SettingsSection at a time, plus a shared Back button. Opened by the menu's
/// SettingsButton.
///
/// Always clear where you are: the active tab is painted in the accent colour,
/// moving the focus onto a tab shows its section right away, and
/// SelectionHighlight marks the focused row.
/// Gamepad: LB / RB (Q / E on keyboard) switch tabs from anywhere; ↑ from the
/// first option reaches the tab bar; B / Esc (UI Cancel) closes and hands the
/// selection back to SettingsButton.
/// Built by Tools > Don't Chicken Out > Crear panel de ajustes en el menú.
/// </summary>
public sealed class SettingsPanel : MonoBehaviour
{
    [Serializable]
    public struct Tab
    {
        public Button          button;
        public SettingsSection section;
    }

    [SerializeField] private Tab[]  tabs;
    [SerializeField] private Button backButton;

    [SerializeField, Tooltip("Vuelve a quedar seleccionado al cerrar (el botón que abrió el panel).")]
    private GameObject reselectOnClose;

    [SerializeField] private Color activeTabColor = new(1f, 0.78f, 0.2f, 1f);
    [SerializeField] private Color idleTabColor   = new(0.30f, 0.26f, 0.40f, 1f);

    private int _active;

    // The press that opened the panel must not also act on it.
    private int _openedFrame = -1;

    private void Awake()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            if (tabs[i].button != null) tabs[i].button.onClick.AddListener(() => Show(index, selectFirst: true));
        }
        if (backButton != null) backButton.onClick.AddListener(Close);
    }

    public void Open()
    {
        AudioManager.Instance?.MakeButtonSelectedSound();
        _openedFrame = Time.frameCount;
        gameObject.SetActive(true);
        Show(_active, selectFirst: true);
    }

    public void Close()
    {
        AudioManager.Instance?.MakeButtonSelectedSound();
        gameObject.SetActive(false);
        Select(reselectOnClose);
    }

    private void Update()
    {
        if (Time.frameCount == _openedFrame || tabs.Length == 0) return;

        if (CancelPressed())
        {
            Close();
            return;
        }

        int step = TabStep();
        if (step != 0 && tabs.Length > 1)
        {
            AudioManager.Instance?.MakeButtonSelectedSound();
            Show((_active + step + tabs.Length) % tabs.Length, selectFirst: true);
            return;
        }

        // Walking the tab bar previews each section, so the content under the
        // focused tab is always the one you would enter.
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        for (int i = 0; i < tabs.Length; i++)
        {
            if (i != _active && tabs[i].button != null && selected == tabs[i].button.gameObject)
            {
                Show(i, selectFirst: false);
                break;
            }
        }
    }

    private void Show(int index, bool selectFirst)
    {
        _active = Mathf.Clamp(index, 0, tabs.Length - 1);

        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = i == _active;
            if (tabs[i].section != null) tabs[i].section.gameObject.SetActive(active);
            if (tabs[i].button  != null && tabs[i].button.image != null)
                tabs[i].button.image.color = active ? activeTabColor : idleTabColor;
        }

        SettingsSection section = tabs[_active].section;
        if (section != null) section.OnShown();

        LinkNavigation(section);

        if (selectFirst && section != null && section.First != null)
            Select(section.First.gameObject);
    }

    /// <summary>
    /// Tab bar ↔ section ↔ Back. Done at runtime because "the section under
    /// the tabs" changes with the active tab.
    /// </summary>
    private void LinkNavigation(SettingsSection section)
    {
        Selectable first   = section != null ? section.First : null;
        Selectable last    = section != null ? section.Last  : null;
        Selectable tab     = tabs[_active].button;
        int        n       = tabs.Length;

        for (int i = 0; i < n; i++)
        {
            if (tabs[i].button == null) continue;
            SetNav(tabs[i].button,
                   up:    backButton,
                   down:  first != null ? first : backButton,
                   left:  n > 1 ? tabs[(i - 1 + n) % n].button : null,
                   right: n > 1 ? tabs[(i + 1) % n].button : null);
        }

        if (first != null)
        {
            Navigation nav = first.navigation;
            nav.mode       = Navigation.Mode.Explicit;
            nav.selectOnUp = tab;
            first.navigation = nav;
        }
        if (last != null)
        {
            Navigation nav   = last.navigation;
            nav.mode         = Navigation.Mode.Explicit;
            nav.selectOnDown = backButton;
            last.navigation  = nav;
        }

        if (backButton != null)
            SetNav(backButton, up: last != null ? last : tab, down: tab);
    }

    private static void SetNav(Selectable s, Selectable up, Selectable down, Selectable left = null, Selectable right = null)
    {
        s.navigation = new Navigation
        {
            mode          = Navigation.Mode.Explicit,
            selectOnUp    = up,
            selectOnDown  = down,
            selectOnLeft  = left,
            selectOnRight = right,
        };
    }

    private static int TabStep()
    {
        foreach (Gamepad pad in Gamepad.all)
        {
            if (pad.leftShoulder.wasPressedThisFrame)  return -1;
            if (pad.rightShoulder.wasPressedThisFrame) return  1;
        }

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.qKey.wasPressedThisFrame) return -1;
            if (kb.eKey.wasPressedThisFrame) return  1;
        }
        return 0;
    }

    private static bool CancelPressed() =>
        EventSystem.current != null
        && EventSystem.current.currentInputModule is InputSystemUIInputModule module
        && module.cancel != null && module.cancel.action != null
        && module.cancel.action.WasPressedThisFrame();

    private static void Select(GameObject target)
    {
        if (EventSystem.current == null || target == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
    }
}
