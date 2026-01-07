using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(menuName = "Game/Minigame Definition", fileName = "MinigameDefinition")]
public sealed class MinigameDefinition : ScriptableObject
{
    [Header("Scene")]
    [SerializeField] private string sceneName;

    [Header("Loading UI")]
    [SerializeField] private string displayName;
    [TextArea(2, 4)]
    [SerializeField] private string description;

    [Header("Preview (optional)")]
    [SerializeField] private Sprite previewImage;
    [SerializeField] private VideoClip previewVideo;

    public string SceneName => sceneName;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite PreviewImage => previewImage;
    public VideoClip PreviewVideo => previewVideo;
}
