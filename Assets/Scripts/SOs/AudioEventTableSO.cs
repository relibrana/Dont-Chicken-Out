using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;

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
/// Un id sin evento asignado no suena (el AudioManager lo avisa por consola).
/// No hay backend de respaldo: todo lo que tiene que ver con cómo suena algo lo
/// autora Audio en FMOD, nada se decide en código.
/// </summary>
[CreateAssetMenu(fileName = "AudioEventTable", menuName = "Audio/Audio Event Table")]
public sealed class AudioEventTableSO : ScriptableObject
{
    [Serializable]
    public struct Binding
    {
        [Tooltip("El id que usa el código hoy. Tiene que coincidir exacto.")]
        public string id;

        [Tooltip("Evento de FMOD. Vacío = ese id no suena hasta que Audio lo cree.")]
        public EventReference eventRef;

        [Tooltip("Marcar en sonidos que se mantienen y hay que poder parar (la mecha de la bomba, "
                 + "loops de estado). Se guarda la instancia para poder llamar a StopSound.")]
        public bool sustained;

        [Tooltip("Opcional: parámetro del evento que recibe el dato del juego que acompaña al sonido "
                 + "(p. ej. block_placement manda 0, 1, 2… por cada celda de la pieza). Qué hace con él "
                 + "lo decide Audio en FMOD. Vacío = el dato se ignora.")]
        [FormerlySerializedAs("pitchParameter")]
        public string valueParameter;

        [Tooltip("Opcional: parámetro del evento que recibe en qué lado de la pantalla pasó "
                 + "(-1 borde izquierdo … 1 borde derecho). Cuánto panea lo decide Audio. Vacío = no se manda.")]
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

    [Header("Ajustes del jugador")]
    [Tooltip("VCA o bus del slider de música de los ajustes. Acepta vca:/... o bus:/... indistintamente. "
             + "Sólo transmite la elección del jugador; la mezcla la hace Audio en FMOD.")]
    public string musicVca = "vca:/MUSIC";

    [Tooltip("VCA o bus del slider de efectos de los ajustes.")]
    public string sfxVca = "vca:/SFX";

    [Header("Parámetros globales")]
    [Tooltip("Parámetro global que recibe la fase de progresión actual (1, 2, 3…). Qué hace con ella "
             + "(tempo, capas, intensidad) lo decide Audio. Vacío = no se manda.")]
    [FormerlySerializedAs("musicTempoParameter")]
    public string progressionPhaseParameter = "";

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
