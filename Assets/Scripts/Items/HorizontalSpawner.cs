using System.Collections;
using DG.Tweening;
using UnityEngine;

public class HorizontalSpawner : MonoBehaviour
{
    [Header("Configuración de Spawn")]
    [SerializeField] private float spawnTime = 2f;
    [SerializeField] private float spawnTimeModifier = 0.5f;
    
    [Header("Rango Horizontal")]
    [SerializeField] private float xMin;
    [SerializeField] private float xMax;
    [SerializeField] private float yOffset;

    private Sequence spawnSequence;

    private void Start()
    {
        GameManager.instance.OnGame += StartSpawn;
        GameManager.instance.OnGameEnd += StopSpawn;
    }

    private void StartSpawn()
    {
        Debug.LogWarning("A");
        spawnSequence?.Kill();
        float randomModifier = 0;
        spawnSequence = DOTween.Sequence();
        spawnSequence.AppendCallback(() => 
        {
            randomModifier = GetRandomModifier();
            Debug.LogWarning("Repeat");
        });
        spawnSequence.AppendInterval(spawnTime + randomModifier);
        spawnSequence.AppendCallback(() => 
        {
            Spawn();
            Debug.LogWarning("Spawn");
        }).SetLoops(-1);
    }

    private float GetRandomModifier() => Random.Range(-spawnTimeModifier, spawnTimeModifier);

    private void StopSpawn()
    {
        spawnSequence.Kill();
    }

    private void Spawn()
    {
        Vector3 newPos = new Vector3(Random.Range(xMin, xMax), yOffset, 0);

        Vector3 worldPos = transform.TransformPoint(newPos);

        GameObject capsuleObj = GameManager.instance.poolManager.GetCapsule();
        capsuleObj.transform.position = worldPos;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        
        Gizmos.matrix = transform.localToWorldMatrix;
        
        Vector3 start = new Vector3(xMin, yOffset, 0);
        Vector3 end = new Vector3(xMax, yOffset, 0);
        
        Gizmos.DrawLine(start, end);
        
        Gizmos.DrawLine(start + Vector3.up * 0.2f, start + Vector3.down * 0.2f);
        Gizmos.DrawLine(end + Vector3.up * 0.2f, end + Vector3.down * 0.2f);
    }
}
