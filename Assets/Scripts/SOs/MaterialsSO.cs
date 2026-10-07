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

    [Tooltip("Sprite de los bloques de este jugador (paja con plumas de su color). "
             + "Reemplaza al antiguo material de tinte: los bloques ya no se pintan.")]
    public Sprite blockSprite;

    [Tooltip("Color de la etiqueta 'Player N' de este jugador. Alfa 0 = sin definir: "
             + "se usa la paleta por defecto (rojo / azul / amarillo / verde).")]
    public Color labelColor = new Color(1f, 1f, 1f, 0f);
}