using UnityEngine;

[CreateAssetMenu(fileName = "Platformer Values")]
public class PlatformerValuesSO : ScriptableObject
{
    [Header("Horizontal Movement")]
    [Tooltip("Player's Maximum Horizontal Velocity")]
    public float maxSpeed = 10f;
    [Tooltip("Time it takes to accelerate from 0 to Max Speed. For a snappy feeling try low values")]
    public float accelerationTime = 0.1f;
    [Tooltip("Time it takes to brake from Max Speed to 0")]
    public float decelerationTime = 0.2f;


    [Header("Jump Values")]
    [Tooltip("Player's Maximum Vertical Velocity")]
    public float maxJumpSpeed = 16f;
    [Tooltip("Maximum Height the player can reach in a full jump")]
    public float peakHeight = 4f;
    [Tooltip("Time it takes to reach the Peak Height from the ground")]
    public float timeToPeak = 0.4f;
    [Tooltip("Gravity Multiplier for a quicker fall")]
    public float fallMultiplier = 2.5f;
    [Tooltip("Gravity Multiplier when the Jump Button is released")]
    public float lowJumpMultiplier = 2f;
    [Tooltip("Resistance force when the Jump Button is on hold while falling")]
    public float glideResistance = 1f;


    [Header("Additional Values")]
    [Tooltip("Time the player can jump after leaving the ground")]
    public float coyoteTime = 0.15f;
    [Tooltip("Time the Jump Input waits before reaching the ground")]
    public float jumpBufferTime = 0.15f;
    [Tooltip("Amount of Force the kick will make")]
    public Vector2 kickForce;
}
