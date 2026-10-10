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

/// <summary>
/// Puerta única de audio del juego. Desde oct 2026 el backend es **FMOD**: cada
/// id se resuelve contra <see cref="AudioEventTableSO"/> y se dispara el evento
/// correspondiente.
///
/// Los ids que todavía no tienen evento siguen sonando por el backend viejo de
/// AudioSource, que se conserva a propósito: así la migración va evento a
/// evento sin que nada se quede mudo, y el día que Audio entregue el que falta
/// basta con asignarlo en la tabla — cero cambios de código.
///
/// Netcode (Fusion 2): esta clase es la frontera. Las guardas de rollback y el
/// filtrado de "esto solo lo oye quien lo ve" van aquí dentro, no repartidas
/// por los 18 scripts que piden sonido.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Pool Settings (SFX Sources)")]
    [SerializeField] private int initialPoolSize = 10;

    [Header("Mixer Groups")]
    [SerializeField] private AudioMixerGroup sfxMixer;
    [SerializeField] private AudioMixerGroup musicMixer;

    [Header("FMOD")]
    [SerializeField, Tooltip("Mapa id -> evento de FMOD. Sin asignar, todo suena por el sistema viejo.")]
    private AudioEventTableSO eventTable;

    [SerializeField, Tooltip("Avisa por consola de los ids que todavía no tienen evento de FMOD.")]
    private bool logUnmappedIdsOnStart = true;

    // Elección del jugador en ajustes (0..1), no una decisión de mezcla: la
    // mezcla la hace Audio en FMOD y esto sólo la escala a través de los VCAs.
    // A 1 se oye exactamente lo que Audio mezcló. A propósito no se expone en
    // el inspector: el código no decide cómo suena nada.
    private float musicVolume        = 1f;
    private float soundEffectsVolume = 1f;

    private Camera _pannningCamera;

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
    [Range(0f, 1f)] public float melodiesVolume = 0.3f;
    private Dictionary<string, MelodySO> melodyMap = new();

    private Stack<AudioSource> freeSources = new();
    private List<AudioSource> allSources = new();
    private AudioSource musicSource;
    private Coroutine introToLoopCoroutine;

    /// <summary>True while any BGM clip is actively playing on the music source.</summary>
    public bool IsMusicPlaying
    {
        get
        {
            if (_musicInstanceValid)
            {
                _musicInstance.getPlaybackState(out FMOD.Studio.PLAYBACK_STATE state);
                return state != FMOD.Studio.PLAYBACK_STATE.STOPPED;
            }

            return musicSource != null && musicSource.isPlaying;
        }
    }

    // Sound trackers
    private int currentStep = 0;
    private int currentJoin = 0;
    private int currentDeath = 0;

    // FMOD state. Sólo se guardan instancias de los sonidos que hay que poder
    // parar; el resto son one-shots y FMOD los libera solo.
    // Clave = (id, dueño): cada bomba controla su propia mecha. Dueño 0 = sin
    // dueño, un único loop global por id.
    private readonly Dictionary<(string id, int owner), FMOD.Studio.EventInstance> _sustained = new();

    // Lo mismo para los loops que aún caen al backend viejo de AudioSource.
    private readonly Dictionary<(string id, int owner), AudioSource> _legacyLoops = new();
    private FMOD.Studio.EventInstance _musicInstance;
    private bool _musicInstanceValid;

    // Identidad del evento que suena ahora. Se guarda el GUID y no su ToString():
    // FMOD.GUID no sobreescribe ToString(), así que comparar por string daría
    // "igual" para TODOS los eventos y la música no cambiaría nunca.
    private FMOD.GUID _musicEventGuid;

    private readonly MixerHandle _musicMixerHandle = new();
    private readonly MixerHandle _sfxMixerHandle   = new();

    private bool UsingFmod => eventTable != null;

    // ââ Unity lifecycle âââââââââââââââââââââââââââââââââââââââââââââââââââââââ

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

        InitializeFmod();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        ReleaseAllSustained();
        ReleaseMusicInstance();
    }

    // ââ Initialization ââââââââââââââââââââââââââââââââââââââââââââââââââââââââ

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

    private void InitializeFmod()
    {
        if (!UsingFmod)
        {
            Debug.LogWarning("AudioManager => sin AudioEventTable asignada: todo el audio sigue por AudioSource.");
            return;
        }

        ApplyVcaVolumes();

        if (!logUnmappedIdsOnStart) return;

        List<string> pending = eventTable.ListUnmappedIds();
        if (pending.Count > 0)
        {
            Debug.Log($"AudioManager => {pending.Count} ids todavía sin evento de FMOD "
                      + $"(suenan por AudioSource): {string.Join(", ", pending)}");
        }
    }

    /// <summary>
    /// Mando de volumen de FMOD para un slider de ajustes. Acepta **VCA o bus**:
    /// el proyecto de Studio puede agrupar de cualquiera de las dos formas y el
    /// código no debería obligar a una. Se decide por el prefijo de la ruta
    /// ("vca:/..." o "bus:/...").
    ///
    /// Resuelve en perezoso a propósito: en Awake los bancos pueden no estar
    /// cargados todavía, y resolver ahí dejaría el slider muerto para siempre.
    /// Si la ruta no existe se avisa UNA vez y el volumen sigue aplicándose al
    /// sistema viejo, en vez de petar.
    /// </summary>
    private sealed class MixerHandle
    {
        private FMOD.Studio.VCA _vca;
        private FMOD.Studio.Bus _bus;

        private bool _isBus;
        private bool _resolved;
        private bool _warned;

        public void SetVolume(string path, float volume)
        {
            if (!TryResolve(path)) return;

            if (_isBus) _bus.setVolume(volume);
            else        _vca.setVolume(volume);
        }

        private bool TryResolve(string path)
        {
            if (_resolved) return true;
            if (string.IsNullOrEmpty(path)) return false;

            _isBus = path.StartsWith("bus:", StringComparison.OrdinalIgnoreCase);

            try
            {
                if (_isBus)
                {
                    _bus      = FMODUnity.RuntimeManager.GetBus(path);
                    _resolved = _bus.isValid();
                }
                else
                {
                    _vca      = FMODUnity.RuntimeManager.GetVCA(path);
                    _resolved = _vca.isValid();
                }
            }
            catch (Exception)
            {
                // VCANotFoundException / BusNotFoundException: todavía no existe
                // en el proyecto de FMOD, o los bancos no han cargado aún.
                Warn(path);
                _resolved = false;
            }

            return _resolved;
        }

        private void Warn(string path)
        {
            if (_warned) return;

            _warned = true;
            Debug.LogWarning($"AudioManager => '{path}' no existe en el proyecto de FMOD. "
                             + "Ese slider de volumen seguirá moviendo solo el audio viejo.");
        }
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

    // ââ Source pool âââââââââââââââââââââââââââââââââââââââââââââââââââââââââââ

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

    // ââ Music âââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââ

    /// <summary>
    /// Plays a BGM track directly. Respects the loop flag set on the Sound asset.
    /// If the requested clip is already playing, does nothing.
    /// Cancels any pending introâloop transition before switching.
    /// </summary>
    public AudioSource PlayMusic(string bgmId)
    {
        if (TryPlayFmodMusic(bgmId)) return null;

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
        // En FMOD el intro y el loop viven dentro del MISMO evento, con
        // transition markers. Así que aquí sólo hace falta pedir el evento: el
        // id del loop y el del intro apuntan al mismo sitio en la tabla.
        if (TryPlayFmodMusic(loopId) || TryPlayFmodMusic(introId)) return;

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
        ReleaseMusicInstance();

        CancelIntroToLoop();
        musicSource.Stop();
    }

    /// <summary>
    /// Arranca la música por FMOD. Si el evento pedido ya está sonando no se
    /// reinicia: es lo que permite que BGM_Menu_A1 / A2 / B apunten todos a
    /// event:/BGM/Menu sin cortarse unos a otros al cambiar de pantalla.
    /// </summary>
    private bool TryPlayFmodMusic(string bgmId)
    {
        if (!UsingFmod) return false;
        if (!eventTable.TryGetMusic(bgmId, out AudioEventTableSO.Binding binding)) return false;

        FMOD.GUID eventGuid = binding.eventRef.Guid;

        if (_musicInstanceValid && _musicEventGuid == eventGuid)
            return true;

        ReleaseMusicInstance();

        // Si quedaba música del sistema viejo sonando, se corta: sólo puede
        // haber una fuente de BGM a la vez.
        CancelIntroToLoop();
        if (musicSource != null) musicSource.Stop();

        _musicInstance      = FMODUnity.RuntimeManager.CreateInstance(binding.eventRef);
        _musicInstanceValid = true;
        _musicEventGuid     = eventGuid;

        _musicInstance.start();
        return true;
    }

    /// <summary>
    /// Le dice a FMOD en qué fase de progresión está la partida (1, 2, 3…) por
    /// el parámetro global <c>progressionPhaseParameter</c>. Qué cambia con la
    /// fase (tempo, capas, intensidad) lo decide Audio; el código no toca el
    /// pitch de la música. Al ser global, sobrevive a los cambios de pista y lo
    /// puede leer cualquier evento.
    /// </summary>
    public void SetProgressionPhase(int phase)
    {
        if (!UsingFmod) return;
        if (string.IsNullOrEmpty(eventTable.progressionPhaseParameter)) return;

        FMODUnity.RuntimeManager.StudioSystem.setParameterByName(eventTable.progressionPhaseParameter, phase);
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

    // ââ SFX ââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââ

    /// <summary>
    /// Dispara un efecto. Intenta FMOD primero y cae al AudioSource viejo si ese
    /// id todavía no tiene evento. Nadie usa el AudioSource que devuelve — se
    /// mantiene el tipo para no tocar las llamadas existentes.
    /// </summary>
    public AudioSource PlaySound(string id)
    {
        if (TryPlayFmodSfx(id, null, null)) return null;

        return PlayLegacySound(id);
    }

    /// <summary>Camino viejo: pool de AudioSource. Se usa como red de seguridad.</summary>
    private AudioSource PlayLegacySound(string id)
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

    /// <summary>
    /// Dispara un efecto acompañado de un dato del juego (p. ej. qué celda de
    /// la pieza se coloca: 0, 1, 2…). El dato va al parámetro <c>valueParameter</c>
    /// del evento y Audio decide en FMOD qué hace con él. El código nunca lo
    /// convierte en pitch ni volumen.
    /// </summary>
    public AudioSource PlaySound(string id, float value)
    {
        if (TryPlayFmodSfx(id, value, null)) return null;

        return PlayLegacySound(id);
    }

    /// <summary>
    /// Dispara un efecto diciéndole a FMOD en qué lado de la pantalla pasó
    /// (parámetro <c>panParameter</c>, −1 … 1). Cuánto panea lo decide Audio.
    /// Los SFX son 2D a propósito: el audio 3D también atenuaría por distancia
    /// y se perderían los de los bordes de una pantalla de 4 jugadores.
    /// </summary>
    public AudioSource PlaySoundAt(string id, Vector3 worldPosition)
    {
        if (TryPlayFmodSfx(id, null, worldPosition)) return null;

        return PlayLegacySound(id);
    }

    /// <summary>
    /// Loop con dueño: varios pueden sonar a la vez (una mecha por bomba) y cada
    /// dueño para sólo el suyo con <see cref="StopSoundFor"/>. Para un one-shot
    /// el dueño no cambia nada.
    /// </summary>
    public void PlaySoundFor(string id, UnityEngine.Object owner)
    {
        var key = (id, OwnerKey(owner));

        if (TryPlayFmodSfx(id, null, null, key.Item2)) return;

        StopLegacyLoop(key); // nunca dos copias del mismo loop para el mismo dueño

        AudioSource src = PlayLegacySound(id);
        if (src != null && src.loop)
            _legacyLoops[key] = src;
    }

    /// <summary>Para el loop de ese dueño, sin tocar los de los demás.</summary>
    public void StopSoundFor(string id, UnityEngine.Object owner)
    {
        var key = (id, OwnerKey(owner));

        StopSustained(key);
        StopLegacyLoop(key);
    }

    private static int OwnerKey(UnityEngine.Object owner) => owner != null ? owner.GetInstanceID() : 0;

    private void StopLegacyLoop((string id, int owner) key)
    {
        if (!_legacyLoops.TryGetValue(key, out AudioSource src)) return;
        _legacyLoops.Remove(key);
        if (src == null) return;

        src.Stop();
        ReleaseSource(src);
    }

    // ---- Backend de FMOD ----------------------------------------------------

    /// <summary>
    /// Intenta sonar por FMOD. Devuelve false si no hay tabla o si ese id
    /// todavía no tiene evento, que es la señal para caer al backend viejo.
    /// </summary>
    private bool TryPlayFmodSfx(string id, float? value, Vector3? worldPosition, int owner = 0)
    {
        if (!UsingFmod) return false;
        if (!eventTable.TryGetSfx(id, out AudioEventTableSO.Binding binding)) return false;

        // Sostenidos (la mecha de la bomba, loops de estado): se guarda la
        // instancia porque hay que poder pararlos por id y dueño.
        if (binding.sustained)
        {
            var key = (id, owner);
            StopSustained(key); // nunca dos copias del mismo loop para el mismo dueño

            FMOD.Studio.EventInstance loop = FMODUnity.RuntimeManager.CreateInstance(binding.eventRef);
            ApplyValue(loop, binding, value);
            ApplyPan(loop, binding, worldPosition);
            loop.start();

            _sustained[key] = loop;
            return true;
        }

        // One-shot sin datos: el camino barato, FMOD lo gestiona entero.
        if (!value.HasValue && !worldPosition.HasValue)
        {
            FMODUnity.RuntimeManager.PlayOneShot(binding.eventRef);
            return true;
        }

        FMOD.Studio.EventInstance instance = FMODUnity.RuntimeManager.CreateInstance(binding.eventRef);
        ApplyValue(instance, binding, value);
        ApplyPan(instance, binding, worldPosition);
        instance.start();
        instance.release(); // se libera sola al acabar
        return true;
    }

    // El código sólo entrega datos del juego a parámetros que Audio expone.
    // Nunca toca pitch, volumen ni paneo de la instancia: sin parámetro, el
    // dato se ignora y el evento suena tal cual se autoró.
    private static void ApplyValue(FMOD.Studio.EventInstance instance, AudioEventTableSO.Binding binding, float? value)
    {
        if (!value.HasValue) return;
        if (string.IsNullOrEmpty(binding.valueParameter)) return;

        instance.setParameterByName(binding.valueParameter, value.Value);
    }

    private void ApplyPan(FMOD.Studio.EventInstance instance, AudioEventTableSO.Binding binding, Vector3? worldPosition)
    {
        if (!worldPosition.HasValue) return;
        if (string.IsNullOrEmpty(binding.panParameter)) return;

        instance.setParameterByName(binding.panParameter, ScreenSideOf(worldPosition.Value));
    }

    private void StopSustained((string id, int owner) key)
    {
        if (!_sustained.TryGetValue(key, out FMOD.Studio.EventInstance instance)) return;
        _sustained.Remove(key);

        instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        instance.release();
    }

    private void ReleaseAllSustained()
    {
        foreach (FMOD.Studio.EventInstance instance in _sustained.Values)
        {
            instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            instance.release();
        }

        _sustained.Clear();
    }

    private void ReleaseMusicInstance()
    {
        if (!_musicInstanceValid) return;

        _musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        _musicInstance.release();

        _musicInstanceValid = false;
        _musicEventGuid     = default;
    }

    private void ApplyVcaVolumes()
    {
        if (!UsingFmod) return;

        _musicMixerHandle.SetVolume(eventTable.musicVca, musicVolume);
        _sfxMixerHandle.SetVolume(eventTable.sfxVca,     soundEffectsVolume);
    }

    /// <summary>
    /// En qué lado de la pantalla está una posición: −1 borde izquierdo, 0
    /// centro, 1 borde derecho. Es un dato del juego, no un paneo: la curva de
    /// paneo (cuánto, con qué forma) la pone Audio en el parámetro de FMOD.
    /// </summary>
    public float ScreenSideOf(Vector3 worldPosition)
    {
        Camera cam = ResolveCamera();
        if (cam == null) return 0f;

        // Camera.main is null in this project (the play camera is inside
        // CameraRig.prefab and is Untagged), hence ResolveCamera.
        float viewportX = cam.WorldToViewportPoint(worldPosition).x;

        return Mathf.Clamp((viewportX - 0.5f) * 2f, -1f, 1f);
    }

    private Camera ResolveCamera()
    {
        if (_pannningCamera != null) return _pannningCamera;

        _pannningCamera = Camera.main != null
            ? Camera.main
            : FindFirstObjectByType<Camera>();

        return _pannningCamera;
    }

    /// <summary>Para todas las copias de ese sonido, tengan dueño o no.</summary>
    public void StopSound(string id)
    {
        bool hadSustained = false;
        foreach (var key in new List<(string id, int owner)>(_sustained.Keys))
        {
            if (key.id != id) continue;
            StopSustained(key);
            hadSustained = true;
        }
        if (hadSustained) return;

        foreach (var key in new List<(string id, int owner)>(_legacyLoops.Keys))
            if (key.id == id) _legacyLoops.Remove(key); // el barrido de abajo las para

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
        ReleaseAllSustained();
        _legacyLoops.Clear(); // el barrido de abajo las para

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

    // ââ Melodies ââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââ

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

        PlayClip(clip, melodiesVolume);
    }

    /// <summary>Returns the note count of a melody by id. Returns 0 if not found.</summary>
    public int GetMelodyNoteCount(string melodyId)
    {
        if (!melodyMap.TryGetValue(melodyId, out MelodySO melody)) return 0;
        return melody.NoteCount;
    }

    // ââ Volume ââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââ

    public void ChangeVolume(SoundsType type, float value)
    {
        float clampedValue = Mathf.Clamp01(value);

        switch (type)
        {
            // Se aplica a los dos backends a la vez: el VCA manda sobre lo que
            // ya suena por FMOD, y el volumen viejo sobre lo que todavía no ha
            // migrado. Mientras la migración esté a medias hacen falta ambos.
            case SoundsType.Music:
                musicVolume = clampedValue;
                if (UsingFmod) _musicMixerHandle.SetVolume(eventTable.musicVca, musicVolume);
                if (musicSource != null)
                    musicSource.volume = musicVolume;
                break;

            case SoundsType.Sfxs:
                soundEffectsVolume = clampedValue;
                if (UsingFmod) _sfxMixerHandle.SetVolume(eventTable.sfxVca, soundEffectsVolume);
                foreach (var src in allSources)
                {
                    if (src.isPlaying && src.outputAudioMixerGroup == sfxMixer)
                        src.volume = soundEffectsVolume;
                }
                break;
        }
    }

    // ââ Simple sound accessors ââââââââââââââââââââââââââââââââââââââââââââââââ

    // Estos tres elegían a mano un clip al azar sin repetir el anterior. En FMOD
    // eso lo hace el propio evento (multi-instrument), así que basta con
    // dispararlo: el código deja de decidir CÓMO suena y Audio puede iterarlo
    // sin pedir builds. Si el evento todavía no existe, se cae a la rotación
    // vieja y no se nota nada.

    public void MakeStepSound()
    {
        if (TryPlayFmodSfx("player_steps", null, null)) return;

        int newStep = GetNonRepeatedRandomNumber(currentStep, playerStepSfxs.Count);
        StopSound($"step{currentStep}");
        PlaySound($"step{newStep}");
        currentStep = newStep;
    }

    public void MakeDeathSound()
    {
        if (TryPlayFmodSfx("player_death", null, null)) return;

        int newDeath = GetNonRepeatedRandomNumber(currentDeath, playerDeathSfxs.Count);
        StopSound($"death{currentDeath}");
        PlaySound($"death{newDeath}");
        currentDeath = newDeath;
    }

    public void MakeJoinSound()
    {
        if (TryPlayFmodSfx("player_spawn", null, null)) return;

        int newJoin = GetNonRepeatedRandomNumber(currentJoin, playerJoinSfxs.Count);
        StopSound($"join{currentJoin}");
        PlaySound($"join{newJoin}");
        currentJoin = newJoin;
    }

    public void MakeButtonSelectedSound() => PlaySound("button_selected");

    public void MakeButtonHoverSound() => PlaySound("button_hover");

    // ââ Helpers âââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââ

    /// <summary>Plays a raw AudioClip directly through the pool.</summary>
    private void PlayClip(AudioClip clip, float? volume = null)
    {
        if (clip == null) return;

        var src = GetSource();
        src.clip = clip;
        src.volume = volume ?? soundEffectsVolume;
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
