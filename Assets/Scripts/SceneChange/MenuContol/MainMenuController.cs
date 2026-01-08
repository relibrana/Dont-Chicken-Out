using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class MainMenuController : MonoBehaviour
{
    [Header("Minigame")]
    [SerializeField] private MinigameDefinition minigame;

    [Header("Gamepad Navigation")]
    [Tooltip("Boton/Selectable que debe quedar seleccionado al entrar al menu.")]
    [SerializeField] private GameObject firstSelected;

    [Tooltip("Pequeno delay para esperar layout/fade antes de seleccionar.")]
    [Min(0f)]
    [SerializeField] private float selectDelay = 0.05f;

    private Coroutine selectRoutine;

    private void OnEnable()
    {
        StartSelectRoutine();
    }

    private void OnDisable()
    {
        if (selectRoutine != null)
        {
            StopCoroutine(selectRoutine);
            selectRoutine = null;
        }
    }

    public void EnsureSelection()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        if (eventSystem.currentSelectedGameObject != null) return;
        if (firstSelected == null) return;

        eventSystem.SetSelectedGameObject(null);
        eventSystem.SetSelectedGameObject(firstSelected);
    }

    public void PlayButton()
    {
        if (minigame == null) return;

        SceneTransitionService.Instance.LoadSpecificScene("Main_Level");
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

    private void StartSelectRoutine()
    {
        if (selectRoutine != null)
        {
            StopCoroutine(selectRoutine);
        }

        selectRoutine = StartCoroutine(SelectOnEnableRoutine());
    }

    private IEnumerator SelectOnEnableRoutine()
    {
        yield return null;

        if (selectDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(selectDelay);
        }

        EnsureSelection();
        selectRoutine = null;
    }
}
