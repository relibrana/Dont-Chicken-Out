using UnityEngine;

/// <summary>
/// Manages the cluck input behavior for a single player.
/// Each press advances the player's assigned melody by one note.
/// If the player stops pressing for longer than resetDelay, the melody resets to the start.
/// Loops back to the first note when the melody ends.
/// </summary>
public sealed class CluckSystem : MonoBehaviour
{
    // ── Melody ID map (by playerIndex) ────────────────────────────────────────

    private static readonly string[] MelodyByIndex = { "mario", "tequila", "sonic", "megalovania" };

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Reset")]
    [Tooltip("Seconds without a press before the melody resets to the first note.")]
    [SerializeField] private float resetDelay = 0.75f;

    // ── State ─────────────────────────────────────────────────────────────────

    private int    _currentNoteIndex;
    private float  _timeSinceLastPress;
    private string _assignedMelodyId;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Update()
    {
        _timeSinceLastPress += Time.deltaTime;

        if (_timeSinceLastPress >= resetDelay)
            _currentNoteIndex = 0;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by PlayerController when the cluck button is pressed.
    /// Plays the current note and advances to the next one.
    /// </summary>
    public void OnCluckPressed()
    {
        _timeSinceLastPress = 0f;

        AudioManager.Instance.PlayMelodyNote(_assignedMelodyId, _currentNoteIndex);

        int noteCount = AudioManager.Instance.GetMelodyNoteCount(_assignedMelodyId);
        _currentNoteIndex = (_currentNoteIndex + 1) % noteCount;
    }

    /// <summary>
    /// Assigns the melody for this player based on their index.
    /// Called by PlayerController after playerIndex is set.
    /// </summary>
    public void SetPlayerIndex(int playerIndex)
    {
        int clampedIndex = Mathf.Clamp(playerIndex, 0, MelodyByIndex.Length - 1);
        _assignedMelodyId = MelodyByIndex[clampedIndex];
    }

    /// <summary>Resets melody progress. Call on player death or game reset.</summary>
    public void ResetState()
    {
        _currentNoteIndex   = 0;
        _timeSinceLastPress = 0f;
    }
}