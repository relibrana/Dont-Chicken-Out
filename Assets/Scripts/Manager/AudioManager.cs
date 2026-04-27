using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum SoundsType
{
    Music,
    Sfxs,
}

[Serializable]
public class Sound
{
    public AudioClip clip;
    public string id;
    [Range(0f, 1f)] public float volume = 0.8f;
    public bool loop;
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Pool Settings (SFX Sources)")]
    [SerializeField] private int initialPoolSize = 10;

    [Header("Mixer Groups")]
    [SerializeField] private AudioMixerGroup sfxMixer;
    [SerializeField] private AudioMixerGroup musicMixer;

    [Header("Audio Settings")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float soundEffectsVolume = 0.8f;

    [Header("SFX Lists")]
    [SerializeField] private List<Sound> bgmSounds = new List<Sound>();
    [SerializeField] private List<Sound> playerSfxs;
    [SerializeField] private List<Sound> playerDeathSfxs;
    [SerializeField] private List<Sound> playerJoinSfxs;
    [SerializeField] private List<Sound> playerStepSfxs;
    [SerializeField] private List<Sound> blocksSfxs;
    [SerializeField] private List<Sound> itemSfxs;
    [SerializeField] private List<Sound> uiSfxs;
    [SerializeField] private List<Sound> miscsSfxs;
    private Dictionary<string, Sound> sfxMap = new();

    [Header("Melodies")]
    [SerializeField] private List<MelodySO> melodies = new();
    private Dictionary<string, MelodySO> melodyMap = new();

    private Stack<AudioSource> freeSources = new();
    private List<AudioSource> allSources = new();
    private AudioSource musicSource;
    private Coroutine introToLoopCoroutine;

    // Sound trackers
    private int currentStep = 0;
    private int currentJoin = 0;
    private int currentDeath = 0;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    protected void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        StopAllSfxs();

        InitializeSoundMap();
        InitializeMelodyMap();

        for (int i = 0; i < initialPoolSize; i++)
            freeSources.Push(CreateSource());

        musicSource = CreateMusicSource();
        musicSource.outputAudioMixerGroup = musicMixer;
        musicSource.volume = musicVolume;
    }

    // ── Initialization ────────────────────────────────────────────────────────

    private void InitializeSoundMap()
    {
        sfxMap = new();
        foreach (var item in playerSfxs)      sfxMap.Add(item.id, item);
        foreach (var item in playerDeathSfxs) sfxMap.Add(item.id, item);
        foreach (var item in playerJoinSfxs)  sfxMap.Add(item.id, item);
        foreach (var item in playerStepSfxs)  sfxMap.Add(item.id, item);
        foreach (var item in blocksSfxs)      sfxMap.Add(item.id, item);
        foreach (var item in itemSfxs)        sfxMap.Add(item.id, item);
        foreach (var item in uiSfxs)          sfxMap.Add(item.id, item);
        foreach (var item in miscsSfxs)       sfxMap.Add(item.id, item);
    }

    private void InitializeMelodyMap()
    {
        melodyMap = new();
        foreach (var melody in melodies)
        {
            if (melody == null) continue;
            if (melodyMap.ContainsKey(melody.MelodyId))
            {
                Debug.LogWarning($"AudioManager => Duplicate melodyId: '{melody.MelodyId}'. Skipping.");
                continue;
            }
            melodyMap.Add(melody.MelodyId, melody);
        }
    }

    // ── Source pool ───────────────────────────────────────────────────────────

    private AudioSource CreateSource()
    {
        var go = new GameObject("PooledAudio");
        DontDestroyOnLoad(go);
        go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.outputAudioMixerGroup = sfxMixer;
        allSources.Add(src);
        return src;
    }

    private AudioSource CreateMusicSource()
    {
        var go = new GameObject("Music Source");
        DontDestroyOnLoad(go);
        go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;
        return src;
    }

    private AudioSource GetSource()
    {
        return freeSources.Count > 0 ? freeSources.Pop() : CreateSource();
    }

    private void ReleaseSource(AudioSource src)
    {
        src.clip = null;
        src.loop = false;
        src.spatialBlend = 0f;
        src.transform.SetParent(transform);
        src.transform.position = transform.position;
        freeSources.Push(src);
    }

    private IEnumerator RecycleWhenDone(AudioSource src)
    {
        yield return new WaitWhile(() => src.isPlaying);
        if (!src.loop)
            ReleaseSource(src);
    }

    // ── Music ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Plays a BGM track. Respects the loop flag set on the Sound asset.
    /// If the requested clip is already playing, it does nothing.
    /// Cancels any pending intro→loop transition before switching.
    /// </summary>
    public AudioSource PlayMusic(string bgmId)
    {
        Sound bgm = FindBgm(bgmId);
        if (bgm == null) return null;

        if (musicSource.isPlaying && musicSource.clip == bgm.clip)
            return musicSource;

        CancelIntroToLoop();

        musicSource.clip = bgm.clip;
        musicSource.volume = musicVolume * bgm.volume;
        musicSource.loop = bgm.loop;
        musicSource.spatialBlend = 0f;
        musicSource.Play();
        return musicSource;
    }

    /// <summary>
    /// Plays an intro clip (non-looping) and automatically transitions to a
    /// looping track when the intro finishes. If called while a previous
    /// intro is still playing, the previous transition is cancelled cleanly.
    /// </summary>
    public void PlayMusicWithIntro(string introId, string loopId)
    {
        Sound intro = FindBgm(introId);
        Sound loop  = FindBgm(loopId);

        if (intro == null || loop == null)
        {
            Debug.LogWarning($"AudioManager => PlayMusicWithIntro: '{introId}' or '{loopId}' not found.");
            return;
        }

        CancelIntroToLoop();

        musicSource.clip = intro.clip;
        musicSource.volume = musicVolume * intro.volume;
        musicSource.loop = false;
        musicSource.spatialBlend = 0f;
        musicSource.Play();

        introToLoopCoroutine = StartCoroutine(TransitionToLoop(loop));
    }

    public void StopMusic()
    {
        CancelIntroToLoop();
        musicSource.Stop();
    }

    private IEnumerator TransitionToLoop(Sound loop)
    {
        yield return new WaitWhile(() => musicSource.isPlaying);

        musicSource.clip = loop.clip;
        musicSource.volume = musicVolume * loop.volume;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.Play();

        introToLoopCoroutine = null;
    }

    private void CancelIntroToLoop()
    {
        if (introToLoopCoroutine == null) return;
        StopCoroutine(introToLoopCoroutine);
        introToLoopCoroutine = null;
    }

    private Sound FindBgm(string bgmId)
    {
        foreach (var sound in bgmSounds)
        {
            if (sound.id == bgmId) return sound;
        }
        Debug.LogWarning($"AudioManager => BGM '{bgmId}' not found.");
        return null;
    }

    // ── SFX ──────────────────────────────────────────────────────────────────

    public AudioSource PlaySound(string id)
    {
        sfxMap.TryGetValue(id, out Sound sfx);
        if (sfx == null)
        {
            Debug.Log($"AudioManager => {id}: IsNull");
            return null;
        }

        var src = GetSource();
        src.clip = sfx.clip;
        src.volume = soundEffectsVolume * sfx.volume;
        src.loop = sfx.loop;
        src.outputAudioMixerGroup = sfxMixer;
        src.spatialBlend = 0;
        src.spread = 180f;
        src.minDistance = 1f;
        src.maxDistance = 10f;
        src.pitch = 1f;
        src.transform.position = transform.position;
        src.transform.SetParent(null);
        src.Play();

        if (!sfx.loop) StartCoroutine(RecycleWhenDone(src));

        return src;
    }

    /// <summary>Plays a sound with a custom pitch. Useful for staggered sub-block placement scales.</summary>
    public AudioSource PlaySound(string id, float pitch)
    {
        var src = PlaySound(id);
        if (src != null) src.pitch = pitch;
        return src;
    }

    public void StopSound(string id)
    {
        sfxMap.TryGetValue(id, out Sound sfx);
        if (sfx == null)
        {
            Debug.Log($"AudioManager => {id}: IsNull");
            return;
        }

        allSources.RemoveAll(src => src == null);

        foreach (var src in allSources)
        {
            if (src.isPlaying && src.clip == sfx.clip && src.outputAudioMixerGroup == sfxMixer)
            {
                src.Stop();
                ReleaseSource(src);
            }
        }
    }

    public void StopAllSfxs()
    {
        allSources.RemoveAll(src => src == null);

        List<AudioSource> sourcesToRelease = new List<AudioSource>();

        foreach (var src in allSources)
        {
            if (src.isPlaying && src.outputAudioMixerGroup == sfxMixer)
            {
                src.Stop();
                sourcesToRelease.Add(src);
            }
        }

        foreach (var src in sourcesToRelease)
            ReleaseSource(src);
    }

    // ── Melodies ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Plays a specific note from a melody by melodyId and note index.
    /// Called by CluckSystem on each cluck press.
    /// </summary>
    public void PlayMelodyNote(string melodyId, int noteIndex)
    {
        if (!melodyMap.TryGetValue(melodyId, out MelodySO melody))
        {
            Debug.LogWarning($"AudioManager => Melody '{melodyId}' not found.");
            return;
        }

        AudioClip clip = melody.GetNote(noteIndex);
        if (clip == null)
        {
            Debug.LogWarning($"AudioManager => Note {noteIndex} not found in melody '{melodyId}'.");
            return;
        }

        PlayClip(clip);
    }

    /// <summary>Returns the note count of a melody by id. Returns 0 if not found.</summary>
    public int GetMelodyNoteCount(string melodyId)
    {
        if (!melodyMap.TryGetValue(melodyId, out MelodySO melody)) return 0;
        return melody.NoteCount;
    }

    // ── Volume ────────────────────────────────────────────────────────────────

    public void ChangeVolume(SoundsType type, float value)
    {
        float clampedValue = Mathf.Clamp01(value);

        switch (type)
        {
            case SoundsType.Music:
                musicVolume = clampedValue;
                if (musicSource != null)
                    musicSource.volume = musicVolume;
                break;

            case SoundsType.Sfxs:
                soundEffectsVolume = clampedValue;
                foreach (var src in allSources)
                {
                    if (src.isPlaying && src.outputAudioMixerGroup == sfxMixer)
                        src.volume = soundEffectsVolume;
                }
                break;
        }
    }

    // ── Simple sound accessors ────────────────────────────────────────────────

    public void MakeStepSound()
    {
        int newStep = GetNonRepeatedRandomNumber(currentStep, playerStepSfxs.Count);
        StopSound($"step{currentStep}");
        PlaySound($"step{newStep}");
        currentStep = newStep;
    }

    public void MakeDeathSound()
    {
        int newDeath = GetNonRepeatedRandomNumber(currentDeath, playerDeathSfxs.Count);
        StopSound($"death{currentDeath}");
        PlaySound($"death{newDeath}");
        currentDeath = newDeath;
    }

    public void MakeJoinSound()
    {
        int newJoin = GetNonRepeatedRandomNumber(currentJoin, playerJoinSfxs.Count);
        StopSound($"join{currentJoin}");
        PlaySound($"join{newJoin}");
        currentJoin = newJoin;
    }

    public void MakeButtonSelectedSound() => PlaySound("button_selected");

    public void MakeButtonHoverSound() => PlaySound("button_hover");

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Plays a raw AudioClip directly through the pool.</summary>
    private void PlayClip(AudioClip clip)
    {
        if (clip == null) return;

        var src = GetSource();
        src.clip = clip;
        src.volume = soundEffectsVolume;
        src.loop = false;
        src.outputAudioMixerGroup = sfxMixer;
        src.spatialBlend = 0f;
        src.transform.SetParent(null);
        src.Play();

        StartCoroutine(RecycleWhenDone(src));
    }

    public int GetNonRepeatedRandomNumber(int nonRepeat, int maxExclusive)
    {
        if (maxExclusive <= 1) return 0;

        int randomNum;
        do
        {
            randomNum = UnityEngine.Random.Range(0, maxExclusive);
        }
        while (randomNum == nonRepeat);

        return randomNum;
    }

    public float GetMusicVolume() => musicVolume;
    public float GetSFXsVolume() => soundEffectsVolume;
}