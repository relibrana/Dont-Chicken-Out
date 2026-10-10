using System;
using UnityEngine;

/// <summary>Qué acaba de pasar en la simulación. La presentación decide cómo se ve y cómo suena.</summary>
public enum SimEvent
{
    PlayerJumped,
    PlayerLanded,
    PlayerKicked,
    PlayerJoined,
    PlayerDied,
    BlockPlaced,
    BlockPlaceRejected,
    HoldablePlaced,
    CapsuleHit,
    TireBounced,
    BombFuseLit,
    BombFuseOut,
    BombExploded,
}

public readonly struct SimEventData
{
    public readonly SimEvent Type;
    public readonly Vector3 Position;
    /// <summary>Quién lo originó (pollo, bloque, ítem). Sirve para filtrar por cámara o por dueño.</summary>
    public readonly UnityEngine.Object Source;
    /// <summary>Dato extra según el evento (p. ej. pitch de la nota de colocación). 1 si no aplica.</summary>
    public readonly float Value;

    public SimEventData(SimEvent type, Vector3 position, UnityEngine.Object source, float value)
    {
        Type     = type;
        Position = position;
        Source   = source;
        Value    = value;
    }
}

/// <summary>
/// Frontera entre simulación y presentación. El código que se va a networkear (jugador, bloques,
/// ítems, reglas) solo llama a <see cref="Raise"/>; nunca a audio, VFX ni cámara directamente.
///
/// Es el único sitio donde vive la guarda de resimulación: con Fusion 2, el driver del tick pone
/// <see cref="Suppressed"/> = <c>Runner.IsResimulation</c> antes de simular, y los eventos de un tick
/// re-simulado se descartan porque ya sonaron en el tick original.
/// </summary>
public static class SimEvents
{
    public static event Action<SimEventData> Raised;

    /// <summary>True mientras se re-simula un tick ya presentado. Nadie lo activa todavía (offline).</summary>
    public static bool Suppressed { get; set; }

    public static void Raise(SimEvent type, Vector3 position, UnityEngine.Object source = null, float value = 1f)
    {
        if (Suppressed && !EndsSomething(type)) return;
        Raised?.Invoke(new SimEventData(type, position, source, value));
    }

    // Los eventos que cierran algo sostenido (un loop) pasan siempre: si una resimulación se los
    // comiera, el loop quedaría sonando para siempre. Repetir un "stop" es inofensivo.
    private static bool EndsSomething(SimEvent type) => type == SimEvent.BombFuseOut;

    // Con "Enter Play Mode" sin domain reload los estáticos sobreviven entre sesiones de play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Raised     = null;
        Suppressed = false;
    }
}
