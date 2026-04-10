using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    public static PauseManager instance;
    [SerializeField] private GameObject pauseMenuUI;
    public bool isPaused = false;
    private PlayerController currentPlayerController = null;

    [SerializeField] private GameObject selectedFirstButton;
    [SerializeField] TextMeshProUGUI playerName;
    [SerializeField] Image playerUI;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public void TogglePauseMenu()
    {
        if(isPaused) Resume();
        else Pause();
    }

    public void Resume()
    {
        if(!pauseMenuUI.activeSelf) return;
        
        AudioManager.Instance.MakeButtonSelectedSound();
        isPaused = false;
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;

        if(currentPlayerController != null)
        {
		    currentPlayerController.playerInput.SwitchCurrentActionMap("Player");
            currentPlayerController = null;
        }
        
        EventSystem.current.SetSelectedGameObject(null);
    }

    public void Pause(PlayerController currentPC = null)
    {
        if(pauseMenuUI.activeSelf) return;

        AudioManager.Instance.MakeButtonSelectedSound();
        isPaused = true;
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;

        if(currentPC != null) 
        {
            currentPlayerController = currentPC;
		    currentPlayerController.playerInput.SwitchCurrentActionMap("UI");
            playerName.text = $"Player {currentPlayerController.playerIndex + 1}";
            playerUI.material = currentPlayerController.playerMat;

            var uiModule = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            if(uiModule != null)
            {
                uiModule.actionsAsset = currentPC.playerInput.actions;
            }
        }

        EventSystem.current.SetSelectedGameObject(selectedFirstButton);
    }

    public void GoToMenu()
    {
        AudioManager.Instance.MakeButtonSelectedSound();
        Time.timeScale = 1f;
        SceneTransitionService.Instance.LoadMenu();
    }

    public void RestartGame()
    {
        AudioManager.Instance.MakeButtonSelectedSound();
        Time.timeScale = 1f;
        SceneTransitionService.Instance.LoadSpecificScene("Main_Level");
    }
}