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
    [SerializeField] private float xOffset;
    [SerializeField] private float yOffset;

    private Sequence spawnSequence;

    private void Start()
    {
        GameManager.instance.OnGame    += StartSpawn;
        GameManager.instance.OnGameEnd += StopSpawn;
    }

    private void StartSpawn()
    {
        spawnSequence?.Kill();
        ScheduleNextSpawn();
    }

    /// <summary>
    /// Builds a one-shot Sequence with a fresh random interval each cycle.
    /// When it fires it spawns and immediately schedules the next one,
    /// avoiding the SetLoops ambiguity that caused the double-spawn.
    /// </summary>
    private void ScheduleNextSpawn()
    {
        float interval = spawnTime + GetRandomModifier();

        spawnSequence = DOTween.Sequence();
        spawnSequence.AppendInterval(interval);
        spawnSequence.AppendCallback(() =>
        {
            Spawn();
            ScheduleNextSpawn();
        });
    }

    private void StopSpawn()
    {
        spawnSequence?.Kill();
        spawnSequence = null;
    }

    private float GetRandomModifier() => Random.Range(0, spawnTimeModifier);

    private void Spawn()
    {
        if (GameManager.instance.playersPosX.Count == 0) return;

        int   randomPlayerIndex = Random.Range(0, GameManager.instance.playersPosX.Count);
        float playerXPos        = GameManager.instance.playersPosX[randomPlayerIndex];

        float xPos = playerXPos + Random.Range(-xOffset, xOffset);

        Vector3 localPos = new Vector3(Mathf.Clamp(xPos, xMin, xMax), yOffset, 0);
        Vector3 worldPos = transform.TransformPoint(localPos);

        GameObject capsuleObj = GameManager.instance.poolManager.GetCapsule();
        capsuleObj.transform.position = worldPos;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color  = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;

        Vector3 start = new Vector3(xMin, yOffset, 0);
        Vector3 end   = new Vector3(xMax, yOffset, 0);

        Gizmos.DrawLine(start, end);
        Gizmos.DrawLine(start + Vector3.up * 0.2f, start + Vector3.down * 0.2f);
        Gizmos.DrawLine(end   + Vector3.up * 0.2f, end   + Vector3.down * 0.2f);
    }
}