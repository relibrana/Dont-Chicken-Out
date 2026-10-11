using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Small audio settings panel of the main menu: Music and SFX sliders (0..1).
/// They only relay the player's choice to AudioManager.ChangeVolume, which
/// scales the FMOD VCAs on top of Audio's mix and remembers it between
/// sessions. Opened by the menu's SettingsButton; closed with its Back button
/// or the UI Cancel action (B / Esc), handing the selection back so gamepad
/// navigation carries on where it was.
/// </summary>
public sealed class AudioSettingsPanel : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Slider musicSlider;
    [SerializeField] private UnityEngine.UI.Slider sfxSlider;
    [SerializeField] private UnityEngine.UI.Button backButton;

    [SerializeField, Tooltip("Vuelve a quedar seleccionado al cerrar (el botón que abrió el panel).")]
    private GameObject reselectOnClose;

    private void Awake()
    {
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(v => SetVolume(SoundsType.Music, v));
        if (sfxSlider   != null) sfxSlider.onValueChanged.AddListener(v => SetVolume(SoundsType.Sfxs, v));
        if (backButton  != null) backButton.onClick.AddListener(Close);
    }

    public void Open()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio != null)
        {
            audio.MakeButtonSelectedSound();
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(audio.GetMusicVolume());
            if (sfxSlider   != null) sfxSlider.SetValueWithoutNotify(audio.GetSFXsVolume());
        }

        gameObject.SetActive(true);
        Select(musicSlider != null ? musicSlider.gameObject : null);
    }

    public void Close()
    {
        AudioManager.Instance?.MakeButtonSelectedSound();
        gameObject.SetActive(false);
        Select(reselectOnClose);
    }

    private void Update()
    {
        if (EventSystem.current != null
            && EventSystem.current.currentInputModule is InputSystemUIInputModule module
            && module.cancel != null && module.cancel.action != null
            && module.cancel.action.WasPressedThisFrame())
        {
            Close();
        }
    }

    private static void SetVolume(SoundsType type, float value) =>
        AudioManager.Instance?.ChangeVolume(type, value);

    private static void Select(GameObject target)
    {
        if (EventSystem.current == null || target == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
    }
}
