using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CameraController : MonoBehaviour
{
	// public List<Transform> players => GameManager.instance.players;
	// Height
	[HideInInspector] public float maxHeightReached = 0;
	float minimumHeightForCameraFollow = 1f;

	// Follow
	public float smoothness = 0.5f; // Ajusta la suavidad del seguimiento
	private GameManager gameManager => GameManager.instance;



    void Start()
    {

    }

    void Update()
    {
		// if (players != null && players.Count > 0 && GameManager.instance.gameState != GameManager.GameState.Over)
		// {
		// 	// Camera reach new height
		// 	foreach (Transform p in players)
		// 	{
		// 		try
		// 		{
		// 			if (p.transform.position.y > maxHeightReached)
		// 			{
		// 				maxHeightReached = p.transform.position.y;
		// 				gameManager.autoMoveCameraCurrentTime = gameManager.autoMoveCameraTime;
		// 			}
		// 		}
		// 		catch{}
		// 	}

		// 	// Camera Follow Vertical
		// 	if (maxHeightReached > minimumHeightForCameraFollow)
		// 	{
		// 		Vector3 desiredPosition = new Vector3(0, maxHeightReached, transform.position.z);

		// 		transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothness * Time.deltaTime);
		// 	}
		// }
    }

	private void OnDrawGizmos()
	{
		Gizmos.color =  Color.yellow;
		Gizmos.DrawLine (new Vector3 (-10, maxHeightReached, 0), new Vector3 (10, maxHeightReached, 0));
	}


	public void Restart ()
	{
		transform.position = Vector3.zero;
		maxHeightReached = 0;
	}
}
