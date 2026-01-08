using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SceneTransitionService : MonoBehaviour
{
    private static SceneTransitionService instance;

    public static SceneTransitionService Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = FindFirstObjectByType<SceneTransitionService>();
            return instance;
        }
    }

    [Header("References")]
    [SerializeField] private LoadingScreenController loadingScreen;
    [SerializeField] private CircleTransitionController circleTransition;

    [Header("Scenes")]
    [SerializeField] private string menuSceneName = "MainMenu";

    private bool isTransitioning;
    private Coroutine transitionRoutine;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Minijuego -> Menú
    public void LoadMenu()
    {
        if (isTransitioning) return;
        if (circleTransition == null) return;

        isTransitioning = true;
        StartTransition(MinigameToMenuRoutine(menuSceneName));
    }

    public void LoadSpecificScene(string sceneName)
    {
        if (isTransitioning) return;
        if (circleTransition == null) return;

        isTransitioning = true;
        StartTransition(MinigameToMenuRoutine(sceneName));
    }

    // Menú -> Minijuego
    public void LoadMinigame(MinigameDefinition definition)
    {
        if (definition == null) return;
        if (isTransitioning) return;
        if (circleTransition == null) return;
        if (loadingScreen == null) return;

        var payload = new LoadingScreenController.Payload(
            definition.DisplayName,
            definition.Description,
            definition.PreviewImage,
            definition.PreviewVideo
        );

        isTransitioning = true;
        StartTransition(MenuToMinigameRoutine(definition.SceneName, payload));
    }

    // =========================
    // FLUJO INTERNO
    // =========================

    private void StartTransition(IEnumerator routine)
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        transitionRoutine = StartCoroutine(routine);
    }

    // Menú -> Minijuego
    private IEnumerator MenuToMinigameRoutine(string sceneName, LoadingScreenController.Payload payload)
    {
        // 1) Circle TOP
        yield return circleTransition.Close();

        // 2) alpha -> 1
        loadingScreen.Show(payload);

        float showWait = loadingScreen.FadeDuration;
        if (showWait > 0f) yield return new WaitForSecondsRealtime(showWait);
        else yield return null;

        // 3) Carga escena
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (!op.isDone)
        {
            yield return null;
        }

        // 4) alpha -> 0
        loadingScreen.Hide();
        float hideWait = loadingScreen.FadeDuration;
        if (hideWait > 0f) yield return new WaitForSecondsRealtime(hideWait);
        else yield return null;

        // 5) Circle MIN
        yield return circleTransition.Open();

        isTransitioning = false;
        transitionRoutine = null;
    }

    // Minijuego -> Menú
    private IEnumerator MinigameToMenuRoutine(string sceneName)
    {
        // 1) Circle TOP
        yield return circleTransition.Close();

        // 2) Cambio de escena
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (!op.isDone)
        {
            yield return null;
        }

        // 3) Circle MIN
        yield return circleTransition.Open();

        isTransitioning = false;
        transitionRoutine = null;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
