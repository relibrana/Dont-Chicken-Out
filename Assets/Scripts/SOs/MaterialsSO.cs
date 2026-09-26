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

    [Tooltip("Color de la etiqueta 'Player N' de este jugador. Alfa 0 = sin definir: "
             + "se usa la paleta por defecto (rojo / azul / amarillo / verde).")]
    public Color labelColor = new Color(1f, 1f, 1f, 0f);
}