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
    [SerializeField, Min(1), Tooltip("Pulsaciones del botón de patada para liberarse.")]
    private int strugglePresses = 8;

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

        state.Activate(strugglePresses, maxStuckSeconds, stuckTint);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _isTrap = false;
    }
}
