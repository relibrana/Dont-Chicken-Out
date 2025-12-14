using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Materials")]
public class MaterialsSO : ScriptableObject
{
    public List<PlayerMaterial> playerMaterials;
}

[Serializable]
public class PlayerMaterial
{
    public Material playerMat;
    public Material hayMat;
}