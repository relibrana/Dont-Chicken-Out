using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Linq;

public enum GameState { Menu, Prepare, Game, Win }

public class GameManager : MonoBehaviour
{
	public static GameManager instance;

	[Header ("References")]
	public CameraController cameraController;

	[Header ("UI")]

	[Header ("Game Variables")]
	[HideInInspector] public float autoMoveCameraCurrentTime;
	public float autoMoveCameraSpeed = 0.2f;
	// bool screenShakeTrigger=false;
	private PlayerController[] inGamePlayers = new PlayerController[4];
	private PlayerController[] playersAlive = new PlayerController[4];

	[SerializeField] private PlayersManager playersManager;
	[SerializeField] private UIManager uiManager;

	[Header("Dont touch")]
	public GameState gameState = GameState.Menu;
	

	public PoolingManager blocksPool;

	public GameObject player1;
	public GameObject player2;
	bool gameStart=false;
	public GameObject p1wins;
	public GameObject p2wins;
	public GameObject lose;

    void Awake()
	{
		if (instance == null)
			instance = this;
		else
			Destroy(gameObject);
	}

	void Update()
	{
		// if (!gameStart)
		// {
		// 	if (player1.activeSelf && player2.activeSelf)
		// 	{
		// 		Debug.Log("Game Start");
		// 		gameStart = true;
		// 		StartGame();
		// 	}
		// }

		if (autoMoveCameraCurrentTime < 0)
			autoMoveCameraCurrentTime = 0;

		// GameState state machine
		switch (gameState)
		{
			case GameState.Menu:
				OnMenuState();
				break;
			case GameState.Prepare:
				OnPrepareState();
				break;
			case GameState.Game:
				OnGameState();
				break;
			case GameState.Win:
				break;
		}
	}

	private void ChangeGameState(GameState newGameState)
	{
		gameState = newGameState;
	}
	private void OnMenuState()
	{
		//RESETEAR TODO PARA PREPARARSE PARA OTRA SESIÓN
		gameState = GameState.Prepare;
    }
	private void OnPrepareState()
    {
        
    }
	private void OnGameState()
	{
		//CameraMovement
		// autoMoveCameraCurrentTime -= Time.deltaTime;

		// if (autoMoveCameraCurrentTime <= 0)
		// {
		// 	cameraController.maxHeightReached += autoMoveCameraSpeed * Time.deltaTime;
		// }

		// Check player coordinates
		// CheckPlayerCoordinates();
	}

	public void AddPlayer(PlayerController player)
	{
		player.onDeath = OnPlayersDeath;
		player.onPlayerReady = PlayerToggleReady;
		AddInGamePlayer(player);

		uiManager.UpdateJoinedPlayers(inGamePlayers);
		CheckIfAllPlayersReady();
	}

	private void AddInGamePlayer(PlayerController player)
    {
		for (int i = 0; i < inGamePlayers.Length; i++)
		{
            if(inGamePlayers[i] == null)
            {
				inGamePlayers[i] = player;
				player.playerIndex = i;
				return;
            }
        }
    }

	private void HandleDisconnection()
    {
        
    }

	private void PlayerToggleReady(PlayerController player)
	{
		bool wasReady = playersAlive.Contains(player);

		uiManager.UpdateReadyPlayer(player.playerIndex, !wasReady);

		playersAlive[player.playerIndex] = !wasReady ? player : null;

		CheckIfAllPlayersReady();
	}
	
	private void CheckIfAllPlayersReady()
	{
		int currPlayersAlive = 0;
		List<PlayerController> playerControllers = new List<PlayerController>();
		for (int i = 0; i < playersAlive.Length; i++)
		{
			if (playersAlive[i] != null)
			{
				currPlayersAlive++;
				playerControllers.Add(playersAlive[i]);
			}
		}

		if (currPlayersAlive != playersManager.currPlayersInGame || currPlayersAlive < 2)
		{
			uiManager.StopGameSequence();
			return;
		}

		uiManager.StartGameSequence(() =>
		{
			ChangeGameState(GameState.Game);
			uiManager.OnGamePlayersUI();
			foreach (PlayerController player in playerControllers)
			{
				player.playerState = PlayerState.InGame;
			}
		});
    }

	private void OnPlayersDeath(PlayerController player)
	{
		//AÑADIR LOGICA DE CUANDO MUERA REVISE QUIENES MURIERON Y QUIENES SIGUEN VIVOS, SI SOLO SIGUE 1 ENTONCES ESE VIVO GANÓ
	}
	
	public void FreeKeyboardScheme(string schemeName) => playersManager.FreeKeyboardScheme(schemeName);

	// void CheckPlayerCoordinates()
	// {
	// 	// foreach (Transform playerTransform in players)
	// 	// {
	// 	// 	Vector3 playerViewportPos = mainCamera.WorldToViewportPoint(playerTransform.position);

	// 	// 	// Check if player is outside the viewport
	// 	// 	if (playerViewportPos.x < 0 || playerViewportPos.x > 1 || playerViewportPos.y < 0 || playerViewportPos.y > 1)
	// 	// 	{
	// 	// 		playerTransform.gameObject.SetActive(false);
	// 	// 	}
	// 	// }
	// 	CheckForDeathPlayers();
	// }



	// void CheckForDeathPlayers()
    // {
	// 	if(!player1.activeSelf && !player2.activeSelf)
	// 	{
	// 		if(screenShakeTrigger==false){
	// 			CameraEffects.instance.DoScreenShake(0.5f,0.2f);
	// 			screenShakeTrigger=true;
	// 		}
	// 		GameOver ();
    //    		lose.SetActive(true);
	// 		return;
	// 	}

	// 	if(!player1.activeSelf)
	// 	{
	// 		if(screenShakeTrigger==false){
	// 			CameraEffects.instance.DoScreenShake(0.5f,0.2f);
	// 			screenShakeTrigger=true;
	// 		}
	// 		GameOver ();
	// 		p2wins.SetActive(true);
	// 	}

	// 	else if (!player2.activeSelf)
	// 	{
	// 		if(screenShakeTrigger==false)
	// 		{
	// 			CameraEffects.instance.DoScreenShake(0.5f,0.2f);
	// 			screenShakeTrigger=true;
	// 		}
	// 		GameOver ();
	// 		p1wins.SetActive(true);
	// 	}
    // }

	// public void StartGame ()
	// {
	// 	Debug.Log ("Call Start Game");
	// 	StartCoroutine (StartGameRoutine());
	// }

	// public void GameOver ()
	// {
	// 	Debug.Log ("Game Over");
	// 	StartCoroutine (GameOverRoutine());
	// }

	// public void RestartScene ()
	// {
	// 	SceneManager.LoadScene (SceneManager.GetActiveScene().buildIndex);
	// }


	// IEnumerator StartGameRoutine ()
	// {
	// 	gameState = GameState.Prepare;
	// 	yield return new WaitForSeconds (4);
	// 	// Start Game
	// 	autoMoveCameraCurrentTime = 0;
	// 	gameState = GameState.Game;
	// }
	// IEnumerator GameOverRoutine ()
	// {
	// 	gameState = GameState.Win;
	// 	yield return new WaitForSeconds (3f);
	// }
}
