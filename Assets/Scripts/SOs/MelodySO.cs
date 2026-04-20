using UnityEngine;

/// <summary>
/// Defines a recognizable melody sequence used by the cluck system.
/// Each note is played in order when the player is in Melody state.
/// Add new melodies by creating new instances of this ScriptableObject.
/// </summary>
[CreateAssetMenu(fileName = "MelodySO")]
public sealed class MelodySO : ScriptableObject
{
    [SerializeField] private string melodyId;
    [SerializeField] private AudioClip[] notes;

    public string MelodyId => melodyId;
    public int NoteCount   => notes?.Length ?? 0;

    /// <summary>
    /// Returns the note at the given index, or null if out of range.
    /// </summary>
    public AudioClip GetNote(int index)
    {
        if (notes == null || index < 0 || index >= notes.Length)
            return null;

        return notes[index];
    }
}