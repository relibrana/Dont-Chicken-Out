using UnityEngine.InputSystem;

/// <summary>
/// Lightweight static container that carries input session data between scenes.
/// No MonoBehaviour, no DontDestroyOnLoad — just a plain static class.
/// Set by MenuInputRouter / MainMenuController before a scene transition,
/// consumed and cleared by PlayersManager on scene load.
/// </summary>
public static class SessionData
{
    /// <summary>The device that first interacted with the main menu.</summary>
    public static InputDevice PrimaryDevice { get; set; }

    /// <summary>
    /// The control scheme that matches PrimaryDevice.
    /// Matches the scheme names used in PlayersManager: "Gamepad", "Keyboard1", "Keyboard2".
    /// </summary>
    public static string PrimaryScheme { get; set; }

    /// <summary>
    /// True when a player won the full game (reached the required rounds).
    /// Set by GameManager before loading the menu; consumed and cleared by GameManager.Start().
    /// Drives the menu intro music selection: A2 (win fanfare) vs A1 (normal).
    /// </summary>
    public static bool GameWasWon { get; set; }

    /// <summary>True if a primary device was registered this session.</summary>
    public static bool HasPrimaryDevice => PrimaryDevice != null && !string.IsNullOrEmpty(PrimaryScheme);

    /// <summary>Clears session data after it has been consumed.</summary>
    public static void Clear()
    {
        PrimaryDevice = null;
        PrimaryScheme = null;
    }
}