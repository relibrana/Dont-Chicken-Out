using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

/// <summary>
/// Puente entre los ids de sonido que usa el gameplay (`"player_jump"`) y los
/// eventos de FMOD (`event:/SFX/Player/Jump`).
///
/// Por qué un mapa y no un EventReference en cada script: las ~45 llamadas de
/// audio del juego no cambian, y queda **un solo sitio** donde decidir qué
/// suena. Eso es lo que hace barato meter después las guardas de rollback de
/// Fusion 2 y el filtrado de audio por jugador: se tocan aquí, no en 18
/// archivos.
///
/// Un id sin evento asignado NO es un error: el AudioManager cae al backend
/// viejo de AudioSource. Así se puede migrar evento a evento según Audio los
/// vaya entregando, sin que nada se quede mudo por el camino.
/// </summary>
[CreateAssetMenu(fileName = "AudioEventTable", menuName = "Audio/Audio Event Table")]
public sealed class AudioEventTableSO : ScriptableObject
{
    [Serializable]
    public struct Binding
    {
        [Tooltip("El id que usa el código hoy. Tiene que coincidir exacto.")]
        public string id;

        [Tooltip("Evento de FMOD. Vacío = ese id sigue sonando por el sistema viejo.")]
        public EventReference eventRef;

        [Tooltip("Marcar en sonidos que se mantienen y hay que poder parar (la mecha de la bomba, "
                 + "loops de estado). Se guarda la instancia para poder llamar a StopSound.")]
        public bool sustained;

        [Tooltip("Opcional: parámetro del evento al que mandar el pitch cuando se llama a "
                 + "PlaySound(id, pitch). Vacío = se usa el pitch directo de la instancia.")]
        public string pitchParameter;

        [Tooltip("Opcional: parámetro del evento al que mandar el paneo (-1 izquierda, 1 derecha) "
                 + "cuando se llama a PlaySoundAt. Vacío = el evento no se panea.")]
        public string panParameter;
    }

    [Header("SFX")]
    [Tooltip("Mapa id → evento para los efectos.")]
    public List<Binding> sfx = new();

    [Header("Música")]
    [Tooltip("Mapa id → evento para la música. Varios ids pueden apuntar al mismo evento: en FMOD "
             + "el intro y el loop viven dentro de un único evento con transition markers, así que "
             + "BGM_Menu_A1 / A2 / B apuntan todos a event:/BGM/Menu.")]
    public List<Binding> music = new();

    [Header("Mezcla")]
    [Tooltip("VCA que controla el slider de música. Si no existe en el proyecto de FMOD, el volumen "
             + "cae al sistema viejo en vez de fallar.")]
    public string musicVca = "vca:/Music";

    [Tooltip("VCA que controla el slider de efectos.")]
    public string sfxVca = "vca:/SFX";

    [Header("Parámetros globales")]
    [Tooltip("Parámetro global que acompaña el tempo de la música en cada fase de progresión. "
             + "Vacío = se usa el pitch de la instancia (sube también el tono).")]
    public string musicTempoParameter = "";

    [Tooltip("Parámetro global de estado de juego, si Audio lo usa para la música (Menu / Gameplay).")]
    public string gameStateParameter = "GameState";

    private Dictionary<string, Binding> _sfxMap;
    private Dictionary<string, Binding> _musicMap;

    public bool TryGetSfx(string id, out Binding binding)   => TryGet(ref _sfxMap,   sfx,   id, out binding);
    public bool TryGetMusic(string id, out Binding binding) => TryGet(ref _musicMap, music, id, out binding);

    private static bool TryGet(ref Dictionary<string, Binding> cache, List<Binding> source, string id, out Binding binding)
    {
        if (cache == null)
        {
            cache = new Dictionary<string, Binding>();

            foreach (Binding entry in source)
            {
                if (string.IsNullOrEmpty(entry.id)) continue;
                if (entry.eventRef.IsNull) continue;      // sin evento = sigue en el sistema viejo

                cache[entry.id] = entry;
            }
        }

        return cache.TryGetValue(id, out binding);
    }

    /// <summary>El caché se arma una vez; si se edita el asset en Play Mode hay que tirarlo.</summary>
    private void OnValidate()
    {
        _sfxMap   = null;
        _musicMap = null;
    }

    /// <summary>
    /// Ids que todavía no tienen evento de FMOD. Lo usa el AudioManager para
    /// avisar por consola una sola vez, y sirve de checklist para Audio.
    /// </summary>
    public List<string> ListUnmappedIds()
    {
        var pending = new List<string>();

        foreach (Binding entry in sfx)
        {
            if (!string.IsNullOrEmpty(entry.id) && entry.eventRef.IsNull)
                pending.Add(entry.id);
        }

        foreach (Binding entry in music)
        {
            if (!string.IsNullOrEmpty(entry.id) && entry.eventRef.IsNull)
                pending.Add(entry.id);
        }

        return pending;
    }
}
