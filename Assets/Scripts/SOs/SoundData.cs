using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "NewSoundData", menuName = "SoundData")]
public class SoundData : ScriptableObject
{
    public string soundName;
    public AudioClip audioClip;
    public float volume = 1f;
}
