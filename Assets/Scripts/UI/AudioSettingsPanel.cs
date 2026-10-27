using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Audio" tab of the settings panel: Music and SFX sliders (0..1) with their
/// percentage. They only relay the player's choice to AudioManager.ChangeVolume,
/// which scales the FMOD VCAs on top of Audio's mix and remembers it between
/// sessions. Opening, closing and tabs belong to SettingsPanel.
/// </summary>
public sealed class AudioSettingsPanel : SettingsSection
{
    [SerializeField] private Slider musicSlider;
    [SerializeField] private TextMeshProUGUI musicValue;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TextMeshProUGUI sfxValue;

    public override Selectable First => musicSlider;
    public override Selectable Last  => sfxSlider;

    private void Awake()
    {
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(v => { SetVolume(SoundsType.Music, v); RefreshLabels(); });
        if (sfxSlider   != null) sfxSlider.onValueChanged.AddListener(v => { SetVolume(SoundsType.Sfxs, v); RefreshLabels(); });
    }

    public override void OnShown()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio != null)
        {
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(audio.GetMusicVolume());
            if (sfxSlider   != null) sfxSlider.SetValueWithoutNotify(audio.GetSFXsVolume());
        }
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (musicValue != null && musicSlider != null) musicValue.text = Percent(musicSlider.value);
        if (sfxValue   != null && sfxSlider   != null) sfxValue.text   = Percent(sfxSlider.value);
    }

    private static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)}%";

    private static void SetVolume(SoundsType type, float value) =>
        AudioManager.Instance?.ChangeVolume(type, value);
}
