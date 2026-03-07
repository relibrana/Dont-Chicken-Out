using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BlockPool")]
public class BlocksPoolSO : ScriptableObject
{
    public List<BlockData> blocks;
}

[System.Serializable]
public class BlockData
{
    public GameObject prefab;
    [Range(0, 100)] public float chance;
}
