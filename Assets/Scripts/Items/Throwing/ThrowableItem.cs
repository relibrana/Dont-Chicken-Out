using UnityEngine;

/// <summary>
/// Base for aimed items (moco, llanta): carried like any holdable but launched
/// as a projectile on the place input instead of being placed. v1 aiming is the
/// carrier's facing plus a fixed launch angle — the current control scheme has
/// no vertical aim input (Move is a 1D axis).
/// </summary>
public abstract class ThrowableItem : HoldableItem
{
    [Header("Throw")]
    [SerializeField, Min(0f), Tooltip("Velocidad inicial del proyectil.")]
    protected float throwSpeed = 12f;

    [SerializeField, Range(-80f, 80f), Tooltip("Ángulo de salida sobre la horizontal, en grados.")]
    protected float launchAngle = 25f;

    [SerializeField, Min(0f), Tooltip("Escala de gravedad del proyectil en vuelo (0 = trayectoria recta).")]
    protected float projectileGravity = 2f;

    [SerializeField, Min(0f), Tooltip("Segundos ignorando al lanzador tras el disparo, para no chocar con él al salir.")]
    protected float ownerGraceSeconds = 0.25f;

    protected bool Launched { get; private set; }

    private float _launchTime;

    public override bool BypassPlacementChecks => true;

    public override void PlaceHoldable()
    {
        base.PlaceHoldable();
        Launch();
    }

    protected virtual void Launch()
    {
        Launched    = true;
        _launchTime = Time.time;

        float sign = Owner != null ? Owner.FacingSign : Mathf.Sign(transform.localScale.x);
        float rad  = launchAngle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(rad) * sign, Mathf.Sin(rad));

        rb2d.gravityScale   = projectileGravity;
        rb2d.linearVelocity = direction * throwSpeed;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Launched          = false;
        rb2d.gravityScale = 0f;
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (!Launched) return;

        if (IsOwnerCollider(collision.collider) && Time.time - _launchTime < ownerGraceSeconds)
            return;

        OnProjectileHit(collision);
    }

    protected bool IsOwnerCollider(Collider2D col) =>
        Owner != null
        && col.attachedRigidbody != null
        && col.attachedRigidbody.gameObject == Owner.gameObject;

    /// <summary>First real impact after launch. The projectile decides its fate here.</summary>
    protected abstract void OnProjectileHit(Collision2D collision);

    /// <summary>Throwables make no placement sound; impact SFX is per-item.</summary>
    protected override void OnPlaceSfx() { }
}
