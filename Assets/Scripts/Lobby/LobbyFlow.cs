using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem.UI;

/// <summary>
/// The pre-match flow of the Lobby scene (Docs/lobby.md):
///   Mode (Local / Online locked) → Map → Rules → Hen house → map scene.
/// Back walks the same path in reverse; Back on the first screen returns to
/// the main menu. What was chosen is written to <see cref="MatchSetup"/>.
/// </summary>
public sealed class LobbyFlow : MonoBehaviour
{
    [SerializeField] private ModeSelectScreen  modeScreen;
    [SerializeField] private MapSelectScreen   mapScreen;
    [SerializeField] private MatchConfigScreen configScreen;
    [SerializeField] private CoopLobby         coopScreen;

    [Tooltip("UI module of the EventSystem; its Cancel action drives \"back\" on the menu screens.")]
    [SerializeField] private InputSystemUIInputModule uiModule;

    [SerializeField] private string mainMenuScene = "MainMenu";

    private LobbyScreen _current;
    private int         _changedFrame = -1;
    private bool        _leaving;

    private void Awake()
    {
        modeScreen.ModeChosen     += mode => { MatchSetup.Mode = mode; Show(mapScreen); };
        modeScreen.BackRequested  += () => Load(mainMenuScene);

        mapScreen.MapChosen       += map => { MatchSetup.Map = map; Show(configScreen); };
        mapScreen.BackRequested   += () => Show(modeScreen);

        configScreen.Confirmed     += () => Show(coopScreen);
        configScreen.BackRequested += () => Show(mapScreen);

        coopScreen.BackRequested  += () => Show(configScreen);
        coopScreen.StartRequested += StartMatch;

        foreach (LobbyScreen s in new LobbyScreen[] { modeScreen, mapScreen, configScreen, coopScreen })
            s.gameObject.SetActive(false);
    }

    private void Start()
    {
        // A new lobby always starts from an empty roster; the rules of the
        // last match (map, rounds) are kept as the default.
        MatchSetup.ClearSeats();
        Show(modeScreen);
    }

    private void Update()
    {
        if (_current == null || _leaving) return;

        // Same-frame guard: the press that changed the screen must not also
        // act on the new one (Back from the hen house would skip two screens).
        if (Time.frameCount == _changedFrame) return;

        if (_current.UsesMenuCancel)
        {
            if (uiModule != null && uiModule.cancel != null && uiModule.cancel.action != null
                && uiModule.cancel.action.WasPressedThisFrame())
            {
                _current.OnCancel();
                return;
            }
            _current.EnsureSelection();
        }
    }

    private void Show(LobbyScreen next)
    {
        if (_leaving || next == _current) return;

        // Show() can run from inside the old screen's Update (hen house Back),
        // so the swap happens here and the guard covers the rest of the frame.
        _current?.Exit();
        _current      = next;
        _changedFrame = Time.frameCount;
        _current.Enter();
    }

    private void StartMatch(List<LobbySeat> seats)
    {
        if (MatchSetup.Map == null)
        {
            Debug.LogError("[LobbyFlow] No map picked: cannot start.");
            return;
        }

        MatchSetup.SetSeats(seats);
        Load(MatchSetup.Map.SceneName);
    }

    private void Load(string sceneName)
    {
        if (_leaving) return;
        _leaving = true;

        // The transition service lives in MainMenu (DontDestroyOnLoad). Opening
        // the Lobby scene directly in the editor skips it: plain load instead.
        if (SceneTransitionService.Instance != null)
            SceneTransitionService.Instance.LoadSpecificScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }
}
