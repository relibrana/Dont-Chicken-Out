using UnityEngine;

/// <summary>
/// Moco (item catalog #3). Thrown projectile with two outcomes:
/// hit a player → they get stuck in place and must mash to escape;
/// hit anything else → becomes a sticky trap that stays for the round and
/// applies the same effect to whoever touches it.
/// </summary>
public sealed class MocoProjectile : ThrowableItem
{
    [Header("Moco")]
    [SerializeField, Min(1), Tooltip("Puntos de forcejeo para liberarse (1 punto = 1 pulsación de movimiento).")]
    private int strugglePresses = 8;

    [SerializeField, Min(0f), Tooltip("Puntos que aporta cada patada.")]
    private float kickStrugglePoints = 2f;

    [SerializeField, Min(0f), Tooltip("Puntos que aporta cada pulsación de movimiento (izq/der).")]
    private float moveStrugglePoints = 1f;

    [SerializeField, Min(0f), Tooltip("Puntos que se descargan por segundo: hay que machacar más rápido que esto. 0 = barra sin descarga.")]
    private float struggleDrainPerSecond = 3f;

    [SerializeField, Min(0f), Tooltip("Cuánto se desplaza el pollo al forcejear con movimiento, en unidades.")]
    private float moveYank = 0.08f;

    [SerializeField, Min(0f), Tooltip("Lo mismo al forcejear con patada: más amplio y marcado.")]
    private float kickYank = 0.18f;

    [SerializeField, Min(0.1f), Tooltip("Velocidad a la que el moco devuelve al pollo al centro (unidades/s).")]
    private float yankDecay = 1.2f;

    [SerializeField, Min(0.5f), Tooltip("Tope de tiempo pegado (accesibilidad): se libera solo al cumplirse.")]
    private float maxStuckSeconds = 3f;

    [SerializeField, Tooltip("Tinte placeholder de la víctima mientras está pegada.")]
    private Color stuckTint = new Color(0.55f, 0.9f, 0.3f, 1f);

    [SerializeField, Tooltip("Color de la mancha cuando queda como trampa en pared/bloque.")]
    private Color trapColor = new Color(0.55f, 0.9f, 0.3f, 0.85f);

    [SerializeField, Tooltip("La trampa se consume con la primera víctima. Si no, dura toda la ronda (doc) — ojo con el stunlock al lado de la mancha.")]
    private bool trapSingleUse = true;

    private bool _isTrap;

    protected override void OnProjectileHit(Collision2D collision)
    {
        Rigidbody2D hitBody = collision.collider.attachedRigidbody;

        if (hitBody != null && hitBody.TryGetComponent(out PlayerController victim))
        {
            StickPlayer(victim);
            gameObject.SetActive(false);
            return;
        }

        BecomeTrap();
    }

    private void BecomeTrap()
    {
        _isTrap = true;

        rb2d.linearVelocity = Vector2.zero;
        rb2d.bodyType       = RigidbodyType2D.Static;

        foreach (Collider2D col in colliders)
            col.isTrigger = true;

        SetColor(trapColor);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isTrap) return;

        Rigidbody2D body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent(out PlayerController victim)) return;
        if (!victim.isOnGame) return;

        if (victim.TryGetComponent(out MocoStuckState existing)
            && (existing.IsActive || existing.HasReleaseImmunity))
            return;

        StickPlayer(victim);

        if (trapSingleUse)
            gameObject.SetActive(false);
    }

    private void StickPlayer(PlayerController victim)
    {
        if (!victim.TryGetComponent(out MocoStuckState state))
            state = victim.gameObject.AddComponent<MocoStuckState>();

        state.Activate(
            strugglePresses,
            kickStrugglePoints,
            moveStrugglePoints,
            struggleDrainPerSecond,
            maxStuckSeconds,
            moveYank,
            kickYank,
            yankDecay,
            stuckTint);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _isTrap = false;
    }
}
