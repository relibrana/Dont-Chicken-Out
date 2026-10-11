using System;
using System.Collections.Generic;
using UnityEngine;

public enum SoundsType
{
    Music,
    Sfxs,
}

/// <summary>
/// Puerta única de audio del juego. Sólo habla con **FMOD**: cada id se
/// resuelve contra <see cref="AudioEventTableSO"/> y se dispara su evento.
///
/// Regla (oct 2026): el código no decide cómo suena nada. Volumen, pitch,
/// paneo, variación, fades y tempo los autora Audio en FMOD Studio. Aquí sólo
/// se (1) dispara el evento cuando algo pasa y (2) se pasan datos del juego en
/// crudo a parámetros que Audio expone (celda colocada, lado de pantalla, fase
/// de progresión). Un id sin evento en la tabla no suena: se avisa por consola.
///
/// La única excepción es el volumen que elige el jugador en ajustes, que entra
/// por los VCAs como multiplicador encima de la mezcla de Audio.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("FMOD")]
    [SerializeField, Tooltip("Mapa id -> evento de FMOD. Sin asignar, no suena nada.")]
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

    // Sólo se guardan instancias de los sonidos que hay que poder parar; el
    // resto son one-shots y FMOD los libera solo.
    // Clave = (id, dueño): cada bomba controla su propia mecha. Dueño 0 = sin
    // dueño, un único loop global por id.
    private readonly Dictionary<(string id, int owner), FMOD.Studio.EventInstance> _sustained = new();

    private FMOD.Studio.EventInstance _musicInstance;
    private bool _musicInstanceValid;

    // Identidad del evento que suena ahora. Se guarda el GUID y no su ToString():
    // FMOD.GUID no sobreescribe ToString(), así que comparar por string daría
    // "igual" para TODOS los eventos y la música no cambiaría nunca.
    private FMOD.GUID _musicEventGuid;

    private readonly MixerHandle _musicMixerHandle = new();
    private readonly MixerHandle _sfxMixerHandle   = new();

    // Ids sin evento ya avisados, para no llenar la consola en cada disparo.
    private readonly HashSet<string> _warnedIds = new();

    /// <summary>True while the current BGM event is playing.</summary>
    public bool IsMusicPlaying
    {
        get
        {
            if (!_musicInstanceValid) return false;

            _musicInstance.getPlaybackState(out FMOD.Studio.PLAYBACK_STATE state);
            return state != FMOD.Studio.PLAYBACK_STATE.STOPPED;
        }
    }

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

        InitializeFmod();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        ReleaseAllSustained();
        ReleaseMusicInstance();
    }

    // ── Initialization ────────────────────────────────────────────────────────

    private void InitializeFmod()
    {
        if (eventTable == null)
        {
            Debug.LogError("AudioManager => sin AudioEventTable asignada: no va a sonar nada.");
            return;
        }

        ApplyVcaVolumes();

        if (!logUnmappedIdsOnStart) return;

        List<string> pending = eventTable.ListUnmappedIds();
        if (pending.Count > 0)
        {
            Debug.Log($"AudioManager => {pending.Count} ids todavía sin evento de FMOD "
                      + $"(no suenan hasta que Audio los cree): {string.Join(", ", pending)}");
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
    /// Si la ruta no existe se avisa UNA vez en vez de petar.
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
                             + "Ese slider de volumen no va a hacer nada.");
        }
    }

    // ── Music ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Arranca la música. Si el evento pedido ya está sonando no se reinicia: es
    /// lo que permite que BGM_Menu_A1 / A2 / B apunten todos a event:/BGM/Menu
    /// sin cortarse unos a otros al cambiar de pantalla.
    /// </summary>
    public void PlayMusic(string bgmId)
    {
        if (eventTable == null) return;
        if (!eventTable.TryGetMusic(bgmId, out AudioEventTableSO.Binding binding))
        {
            WarnUnmapped(bgmId);
            return;
        }

        FMOD.GUID eventGuid = binding.eventRef.Guid;

        if (_musicInstanceValid && _musicEventGuid == eventGuid)
            return;

        ReleaseMusicInstance();

        _musicInstance      = FMODUnity.RuntimeManager.CreateInstance(binding.eventRef);
        _musicInstanceValid = true;
        _musicEventGuid     = eventGuid;

        _musicInstance.start();
    }

    /// <summary>
    /// En FMOD el intro y el loop viven dentro del MISMO evento, con transition
    /// markers: el id del intro y el del loop apuntan al mismo sitio en la tabla
    /// y basta con pedir el evento.
    /// </summary>
    public void PlayMusicWithIntro(string introId, string loopId)
    {
        if (eventTable != null && eventTable.TryGetMusic(loopId, out _))
            PlayMusic(loopId);
        else
            PlayMusic(introId);
    }

    public void StopMusic() => ReleaseMusicInstance();

    /// <summary>
    /// Le dice a FMOD en qué fase de progresión está la partida (1, 2, 3…) por
    /// el parámetro global <c>progressionPhaseParameter</c>. Qué cambia con la
    /// fase (tempo, capas, intensidad) lo decide Audio; el código no toca el
    /// pitch de la música. Al ser global, sobrevive a los cambios de pista y lo
    /// puede leer cualquier evento.
    /// </summary>
    public void SetProgressionPhase(int phase)
    {
        if (eventTable == null) return;
        if (string.IsNullOrEmpty(eventTable.progressionPhaseParameter)) return;

        FMODUnity.RuntimeManager.StudioSystem.setParameterByName(eventTable.progressionPhaseParameter, phase);
    }

    private void ReleaseMusicInstance()
    {
        if (!_musicInstanceValid) return;

        _musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        _musicInstance.release();

        _musicInstanceValid = false;
        _musicEventGuid     = default;
    }

    // ── SFX ───────────────────────────────────────────────────────────────────

    /// <summary>Dispara un efecto tal cual lo autoró Audio.</summary>
    public void PlaySound(string id) => PlaySfx(id, null, null);

    /// <summary>
    /// Dispara un efecto acompañado de un dato del juego (p. ej. qué celda de
    /// la pieza se coloca: 0, 1, 2…). El dato va al parámetro <c>valueParameter</c>
    /// del evento y Audio decide en FMOD qué hace con él. El código nunca lo
    /// convierte en pitch ni volumen.
    /// </summary>
    public void PlaySound(string id, float value) => PlaySfx(id, value, null);

    /// <summary>
    /// Dispara un efecto diciéndole a FMOD en qué lado de la pantalla pasó
    /// (parámetro <c>panParameter</c>, −1 … 1). Cuánto panea lo decide Audio.
    /// Los SFX son 2D a propósito: el audio 3D también atenuaría por distancia
    /// y se perderían los de los bordes de una pantalla de 4 jugadores.
    /// </summary>
    public void PlaySoundAt(string id, Vector3 worldPosition) => PlaySfx(id, null, worldPosition);

    /// <summary>
    /// Loop con dueño: varios pueden sonar a la vez (una mecha por bomba) y cada
    /// dueño para sólo el suyo con <see cref="StopSoundFor"/>. Para un one-shot
    /// el dueño no cambia nada.
    /// </summary>
    public void PlaySoundFor(string id, UnityEngine.Object owner) => PlaySfx(id, null, null, OwnerKey(owner));

    /// <summary>Para el loop de ese dueño, sin tocar los de los demás.</summary>
    public void StopSoundFor(string id, UnityEngine.Object owner) => StopSustained((id, OwnerKey(owner)));

    /// <summary>Para todas las copias de ese sonido, tengan dueño o no.</summary>
    public void StopSound(string id)
    {
        foreach (var key in new List<(string id, int owner)>(_sustained.Keys))
        {
            if (key.id == id) StopSustained(key);
        }
    }

    public void StopAllSfxs() => ReleaseAllSustained();

    private static int OwnerKey(UnityEngine.Object owner) => owner != null ? owner.GetInstanceID() : 0;

    private void PlaySfx(string id, float? value, Vector3? worldPosition, int owner = 0)
    {
        if (eventTable == null) return;
        if (!eventTable.TryGetSfx(id, out AudioEventTableSO.Binding binding))
        {
            WarnUnmapped(id);
            return;
        }

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
            return;
        }

        // One-shot sin datos: el camino barato, FMOD lo gestiona entero.
        if (!value.HasValue && !worldPosition.HasValue)
        {
            FMODUnity.RuntimeManager.PlayOneShot(binding.eventRef);
            return;
        }

        FMOD.Studio.EventInstance instance = FMODUnity.RuntimeManager.CreateInstance(binding.eventRef);
        ApplyValue(instance, binding, value);
        ApplyPan(instance, binding, worldPosition);
        instance.start();
        instance.release(); // se libera sola al acabar
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

    private void WarnUnmapped(string id)
    {
        if (!_warnedIds.Add(id)) return;
        Debug.LogWarning($"AudioManager => '{id}' no tiene evento de FMOD en la tabla: no suena.");
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

    // ── Player volume (settings) ──────────────────────────────────────────────

    /// <summary>
    /// Volumen que elige el jugador en ajustes. Escala el VCA correspondiente
    /// encima de la mezcla de Audio; a 1 se oye la mezcla tal cual.
    /// </summary>
    public void ChangeVolume(SoundsType type, float value)
    {
        float clampedValue = Mathf.Clamp01(value);

        switch (type)
        {
            case SoundsType.Music:
                musicVolume = clampedValue;
                break;

            case SoundsType.Sfxs:
                soundEffectsVolume = clampedValue;
                break;
        }

        ApplyVcaVolumes();
    }

    private void ApplyVcaVolumes()
    {
        if (eventTable == null) return;

        _musicMixerHandle.SetVolume(eventTable.musicVca, musicVolume);
        _sfxMixerHandle.SetVolume(eventTable.sfxVca,     soundEffectsVolume);
    }

    public float GetMusicVolume() => musicVolume;
    public float GetSFXsVolume() => soundEffectsVolume;

    // ── Simple sound accessors ────────────────────────────────────────────────

    // Antes estos tres elegían en C# un clip al azar sin repetir el anterior.
    // Esa variación la hace ahora el propio evento (multi-instrument de FMOD).

    public void MakeStepSound()  => PlaySound("player_steps");
    public void MakeDeathSound() => PlaySound("player_death");
    public void MakeJoinSound()  => PlaySound("player_spawn");

    public void MakeButtonSelectedSound() => PlaySound("button_selected");
    public void MakeButtonHoverSound()    => PlaySound("button_hover");
}
