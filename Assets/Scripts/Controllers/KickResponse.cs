using System;
using UnityEngine;

public class KickResponse : MonoBehaviour, IKickable
{
    private Rigidbody2D rb;
    public Action OnBeforeKick;

    void Awake() => rb = GetComponent<Rigidbody2D>();

    public void ReceiveKick(Vector2 impulseDirection)
    {
        OnBeforeKick?.Invoke();
        rb.linearVelocity = impulseDirection;
    }
}