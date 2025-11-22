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

    [Header("Audio Settings (Volúmenes Base)")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float soundEffectsVolume = 0.8f;

    [SerializeField] private List<Sound> bgmSounds = new List<Sound>();
    [SerializeField] private List<Sound> playerSfxs;
    [SerializeField] private List<Sound> playerCockSfxs;
    [SerializeField] private List<Sound> playerStepSfxs;
    [SerializeField] private List<Sound> blocksSfxs;
    private Dictionary<string, Sound> sfxMap = new();

    private Stack<AudioSource> freeSources = new();
    private List<AudioSource> allSources = new();
    private AudioSource musicSource;

	//Sounds
	private int currentStep;
	private int currentCuack;


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

        InitializeSoundMap();

        for (int i = 0; i < initialPoolSize; i++)
        {
            freeSources.Push(CreateSource());
        }

        musicSource = CreateMusicSource();
        musicSource.outputAudioMixerGroup = musicMixer;
        musicSource.volume = musicVolume;
    }

    private void InitializeSoundMap()
    {
        sfxMap = new();
        foreach (var item in playerSfxs)
        {
            sfxMap.Add(item.id, item);
        }
        foreach (var item in playerCockSfxs)
        {
            item.volume = 0.2f;
            sfxMap.Add(item.id, item);
        }
        foreach (var item in playerStepSfxs)
        {
            sfxMap.Add(item.id, item);
        }
        foreach (var item in blocksSfxs)
        {
            sfxMap.Add(item.id, item);
        }
    }

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
        {
            ReleaseSource(src);
        }
    }
    
    public AudioSource PlayMusic(string bgmId)
    {
        Sound bgm = null;

        foreach (var sound in bgmSounds)
        {
            if (sound.id == bgmId)
                bgm = sound;
        }

        if (bgm == null)
            return null;

        if (musicSource.isPlaying && musicSource.clip == bgm.clip)
        {
            return musicSource;
        }

        musicSource.clip = bgm.clip;
        musicSource.volume = musicVolume * bgm.volume;
        
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.Play();
        return musicSource;
    }

    public void StopMusic()
    {
        musicSource.Stop();
    }

    public AudioSource PlaySound(string id)
    {
        sfxMap.TryGetValue(id, out Sound sfx);
        if (sfx == null) 
            return null;

        var src = GetSource();
        src.clip = sfx.clip;
        src.volume = soundEffectsVolume * sfx.volume;
        src.loop = sfx.loop;
        src.outputAudioMixerGroup = sfxMixer;

        src.spatialBlend = 0;
        src.spread = 180f;
        src.minDistance = 1f;
        src.maxDistance = 10f;
        src.transform.position = transform.position;

        src.transform.SetParent(null);

        src.Play();

        if (!sfx.loop) StartCoroutine(RecycleWhenDone(src));

        return src;
    }

    public void Stop(string id)
    {
        sfxMap.TryGetValue(id, out Sound sfx);
        if (sfx == null) 
            return;
        
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
        {
            ReleaseSource(src);
        }
    }
    
    public void ChangeVolume(SoundsType type, float value)
    {
        float clampedValue = Mathf.Clamp01(value);

        switch (type)
        {
            case SoundsType.Music:
                musicVolume = clampedValue;
                if (musicSource != null)
                {
                    musicSource.volume = musicVolume; 
                }
                break;
            case SoundsType.Sfxs:
                soundEffectsVolume = clampedValue;
                
                foreach (var src in allSources)
                {
                    if (src.isPlaying && src.outputAudioMixerGroup == sfxMixer)
                    {
                        src.volume = soundEffectsVolume; 
                    }
                }
                break;
            default:
                break;
        }
    }
    
    // Métodos de acceso simples (propio del script)
	public void MakeStepSound()
    {
        GetNonRepeatedRandomNumber(currentStep, playerStepSfxs.Count, out int newStep);

        Stop($"step{currentStep}");

        PlaySound($"steps{newStep}");
        currentStep = newStep;
    }

	public void MakeCuackSound()
    {
        GetNonRepeatedRandomNumber(currentCuack, playerCockSfxs.Count, out int newCuack);

        Stop($"cuack{currentCuack}");

        PlaySound($"cuack{newCuack}");
        currentCuack = newCuack;
    }

    public int GetNonRepeatedRandomNumber(int nonRepeat, int maxExclusive, out int randomNum)
    {
        do
        {
            randomNum = UnityEngine.Random.Range(0, maxExclusive);
        }
        while(randomNum == nonRepeat);

        return randomNum;
    }
    public float GetMusicVolume() { return musicVolume; }
    public float GetSFXsVolume() { return soundEffectsVolume; }

    // Se elimina IsSameMusicPlaying<TEnum> y RegisterClip ya que usaban la lógica de librerías.
}