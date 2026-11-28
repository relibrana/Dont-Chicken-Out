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
    [Tooltip("Velocidad fija de bajada al planear (GDD: una velocidad, no un multiplicador). Es lo que equilibra el planeo: "
             + "si es muy baja, el vacío deja de amenazar.")]
    public float glideFallSpeed = 4f;
    [Tooltip("Segundos de caída antes de que arranque el planeo. Absorbe el planeo accidental de quien suelta el salto un poco tarde.")]
    public float glideEntryDelay = 0.08f;
    [Tooltip("Corrección de esquinas: si al subir la cabeza roza la esquina de un bloque, se desplaza hasta esto de lado para pasar. 0 = off.")]
    public float cornerCorrection = 0.15f;


    [Header("Additional Values")]
    [Tooltip("Time the player can jump after leaving the ground")]
    public float coyoteTime = 0.15f;
    [Tooltip("Time the Jump Input waits before reaching the ground")]
    public float jumpBufferTime = 0.15f;
    [Tooltip("Amount of Force the kick will make")]
    public Vector2 kickForce;
    [Tooltip("Tiempo mínimo entre dos patadas del mismo pollito (GDD: recuperación).")]
    public float kickCooldown = 0.3f;
    [Tooltip("Cooldown time between each block Placement. Scaled by the progression system (Placement Cadence).\n"
             + "This plus Block Generation Delay is the total time between two placements, which is what "
             + "drives blocks/second in the progression safety check.")]
    public float blockPlacementCD = 0.15f;
    [Tooltip("Delay between receiving a block and being able to place it. Scaled by the progression system (Block Generation)")]
    public float blockGenerationDelay = 0.15f;
}
