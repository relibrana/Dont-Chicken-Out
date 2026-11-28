/// <summary>
/// The seven gameplay variables scaled by the progression system.
/// Naming and order follow the design doc: §4.1 (values that go up) first,
/// §4.2 (wait times that go down) last.
///
/// Every variable is consumed the same way: base value * scalar.
/// For the §4.2 wait times a smaller scalar means a shorter wait, i.e. faster.
/// </summary>
public enum ProgressionVar
{
    /// <summary>Camera auto-rise speed. Consumed by GameManager.</summary>
    CameraSpeed = 0,

    /// <summary>Player horizontal max speed. Consumed by PlayerMovement.</summary>
    LateralSpeed = 1,

    /// <summary>Gravity scale applied to blocks when they leave the pool. Consumed by PoolingManager.</summary>
    BlockFall = 2,

    /// <summary>
    /// Push speed (nov 2026, GDD empujar): how fast a pushed piece moves, and
    /// how far a kick slides it. Consumed by PlayerBlockHandler and BlockScript.
    /// </summary>
    Push = 3,

    /// <summary>Delay between fetching a block and being able to place it. Consumed by PlayerBlockHandler.</summary>
    BlockGeneration = 4,

    /// <summary>Cooldown between two block placements. Consumed by PlayerBlockHandler.</summary>
    PlacementCadence = 5,

    /// <summary>Duration of the camera pause played right after a ribbon breaks. Consumed by GameManager.</summary>
    CameraPause = 6,
}
