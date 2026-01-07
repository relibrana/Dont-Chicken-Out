using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private MinigameDefinition minigame;

    public void PlayButton()
    {
        if (minigame == null) return;

        SceneTransitionService.Instance.LoadMinigame(minigame);
    }

    public void ExitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OpenSupportLink()
    {
        Application.OpenURL("https://beacons.ai/raymi.games");
    }
}
