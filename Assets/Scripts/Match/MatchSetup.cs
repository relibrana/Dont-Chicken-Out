using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum MatchMode { Local, Online }

/// <summary>
/// Cosmetic choice of one chicken, as fixed-width ids (never assets), so the
/// same struct can later be written to a [Networked] property as-is
/// (Docs/fusion2-integracion.md §4.1). Personalization is locked for now:
/// every seat carries <see cref="Default"/> and the colour comes from the room.
/// </summary>
[System.Serializable]
public struct PlayerLoadout
{
    public byte   chickenType;
    public byte   colorVariant;
    public ushort hat;
    public ushort outfit;
    public ushort accessory;

    public static PlayerLoadout Default => default;
}

/// <summary>
/// One local player sitting in a lobby room. <see cref="Slot"/> is the room
/// (0..3) and also the player's index in the match: it fixes spawn point,
/// colour and the "Player N" label, so what you saw in the lobby is what you
/// get in the arena.
/// </summary>
public sealed class LobbySeat
{
    public int           Slot;
    public InputDevice   Device;
    /// <summary>"Gamepad", "Keyboard1" or "Keyboard2" — the schemes in InputSystem.inputactions.</summary>
    public string        Scheme;
    public PlayerLoadout Loadout = PlayerLoadout.Default;
}

/// <summary>
/// What the lobby decided, carried into the map scene. Plain static, like
/// <see cref="SessionData"/>: written by the lobby screens, read by
/// PlayersManager / GameManager when the map loads.
///
/// Playing a map scene directly in the editor leaves this empty, and the map
/// falls back to the old in-arena join flow (hay button included).
/// </summary>
public static class MatchSetup
{
    public const int MaxSeats          = 4;
    public const int DefaultRoundsToWin = 3;

    public static MatchMode        Mode        { get; set; } = MatchMode.Local;
    public static MapDefinitionSO  Map         { get; set; }
    public static int              RoundsToWin { get; set; } = DefaultRoundsToWin;

    private static readonly List<LobbySeat> _seats = new();
    public static IReadOnlyList<LobbySeat> Seats => _seats;

    /// <summary>True when the players were picked in the lobby (the map must not open its own join).</summary>
    public static bool HasSeats => _seats.Count > 0;

    public static void SetSeats(IEnumerable<LobbySeat> seats)
    {
        _seats.Clear();
        _seats.AddRange(seats);
        _seats.Sort((a, b) => a.Slot.CompareTo(b.Slot));
    }

    /// <summary>Forget the roster, keep the rules (so "play again" keeps the host's choices).</summary>
    public static void ClearSeats() => _seats.Clear();

    public static void Clear()
    {
        _seats.Clear();
        Mode        = MatchMode.Local;
        Map         = null;
        RoundsToWin = DefaultRoundsToWin;
    }

    // Enter Play Mode without domain reload keeps statics alive between sessions.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Clear();
}
