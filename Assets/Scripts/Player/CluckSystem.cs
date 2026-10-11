using UnityEngine;

/// <summary>
/// Manages the cluck input behavior for a single player.
///
/// Melodies are switched off (oct 2026): they played AudioClips from MelodySO
/// through the old AudioSource backend, which is gone — all sound now lives in
/// FMOD and they are not being migrated for now. The press is still tracked
/// (note index per player, reset after resetDelay) so that bringing them back
/// is a single SimEvent + an FMOD event that reads the note index.
/// </summary>
public sealed class CluckSystem : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Reset")]
    [Tooltip("Seconds without a press before the melody resets to the first note.")]
    [SerializeField] private float resetDelay = 0.75f;

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>Which player's melody this is (each player had its own).</summary>
    public int PlayerIndex { get; private set; }

    private int   _currentNoteIndex;
    private float _timeSinceLastPress;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Update()
    {
        _timeSinceLastPress += Time.deltaTime;

        if (_timeSinceLastPress >= resetDelay)
            _currentNoteIndex = 0;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by PlayerController when the cluck button is pressed. Advances
    /// the note index; plays nothing while melodies are off.
    /// </summary>
    public void OnCluckPressed()
    {
        _timeSinceLastPress = 0f;
        _currentNoteIndex++;
    }

    /// <summary>Called by PlayerController after playerIndex is set.</summary>
    public void SetPlayerIndex(int playerIndex) => PlayerIndex = playerIndex;

    /// <summary>Resets melody progress. Call on player death or game reset.</summary>
    public void ResetState()
    {
        _currentNoteIndex   = 0;
        _timeSinceLastPress = 0f;
    }
}
