using System.Collections;
using UnityEngine;

public class CloudPool : MonoBehaviour
{
    public int cloudsPerBatch = 2;
    public float spawnRate = 5.0f;
    private float elapsedTime = 0.0f;

	public PoolingManager cloudsPool;
	Camera mainCamera;

    void Start()
    {
		mainCamera = CameraEffects.instance.mainCamera.GetComponent<Camera>();
        // Iniciar la primera tanda de nubes
        StartCoroutine(SpawnCloudsBatch());
    }

    void Update()
    {
        elapsedTime += Time.deltaTime;

		// Verificar si ha pasado el tiempo de espera para spawnear una nueva nube
		if (elapsedTime >= spawnRate)
		{
			// Ahora la generación de nubes se realiza en la corrutina, no es necesario spawnear aquí
			elapsedTime = 0.0f; // Reiniciar el temporizador
		}
    }

    IEnumerator SpawnCloudsBatch()
	{
		while (true)
		{
			yield return new WaitForSeconds(spawnRate);

			// Spawnear una tanda de nubes
			for (int i = 0; i < cloudsPerBatch; i++)
			{
				SpawnCloud(i); // Pasa el índice i para ajustar la posición de spawn
			}
		}
	}


    void SpawnCloud(int index)
	{
		// Buscar una nube inactiva en el pool
		float spawnX = Screen.width + 3f;
		float spawnY = Random.Range(GameManager.instance.cameraRig.MaxHeightReached, Screen.height + GameManager.instance.cameraRig.MaxHeightReached + Random.Range (-5f,5f));

		Vector3 worldPos = mainCamera.ScreenToWorldPoint (new Vector3(spawnX, spawnY, 0));
		worldPos.z = 1;

		GameObject cloud = cloudsPool.GetPooledObject (0, worldPos, Vector3.zero, 20) ;
	}
}
