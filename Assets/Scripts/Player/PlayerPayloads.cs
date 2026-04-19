/// <summary>
/// Snapshot of raw player input for a single tick.
/// Consumed by PlayerMovement and PlayerBlockHandler.
/// Network-ready: can be serialized and sent over the wire for client-side prediction.
/// </summary>
public struct InputPayload
{
    public float moveDirection;
    public bool jumpPressed;
    public bool jumpHeld;
    public bool placeBlockPressed;
    public bool kickPressed;
    public bool cluckPressed;
}

/// <summary>
/// Snapshot of the player's physical state after a movement tick.
/// Produced by PlayerMovement and consumed by the rest of the system.
/// Network-ready: can be used for server reconciliation.
/// </summary>
public struct StatePayload
{
    public UnityEngine.Vector2 position;
    public UnityEngine.Vector2 velocity;
    public bool isGrounded;
}