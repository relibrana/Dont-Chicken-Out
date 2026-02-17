using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Blocks")]
public class BlocksSO : ScriptableObject
{
    public float mass;
    public float linearDamping;
    public float gravityScale;
}