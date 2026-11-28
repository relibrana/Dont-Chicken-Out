using System;
using UnityEngine;

public class KickResponse : MonoBehaviour, IKickable
{
    private Rigidbody2D rb;
    public Action OnBeforeKick;

    /// <summary>
    /// Lets the object take the kick its own way (the bomb and the tire slide
    /// and bounce, the moco is deflected). Return true when it was handled;
    /// false falls back to the default (velocity = kick).
    /// </summary>
    public Func<Vector2, bool> KickOverride;

    void Awake() => rb = GetComponent<Rigidbody2D>();

    public void ReceiveKick(Vector2 impulseDirection)
    {
        OnBeforeKick?.Invoke();
        if (KickOverride != null && KickOverride(impulseDirection)) return;
        rb.linearVelocity = impulseDirection;
    }
}
