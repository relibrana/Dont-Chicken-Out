using UnityEngine;

/// <summary>
/// POW (item catalog #2). Cambio de diseño (sep 2026): ya no se activa al
/// romper la cápsula. Se lleva en la mano como el moco o la llanta, se lanza
/// con el botón de colocar y **la cuenta atrás arranca en el impacto** — así el
/// jugador elige el momento y el sitio en vez de dispararse solo al recoger.
///
/// Al impactar se clava donde cae y la cuenta 3-2-1-JUMP! sale encima de él,
/// para que todos vean de dónde va a venir el golpe. El aturdimiento en sí lo
/// resuelve PowSequence.
/// </summary>
public sealed class PowPickup : ThrowableItem
{
    [Header("POW")]
    [SerializeField, Range(1, 5), Tooltip("Desde dónde cuenta la cuenta regresiva.")]
    private int countFrom = 3;

    [SerializeField, Min(0.5f), Tooltip("Duración del aturdimiento. Ojo: la cámara sigue subiendo (doc).")]
    private float stunSeconds = 2.5f;

    [SerializeField, Tooltip("Tinte placeholder de los jugadores aturdidos.")]
    private Color stunTint = new Color(0.6f, 0.6f, 0.75f, 1f);

    [Header("Cuenta atrás")]
    [SerializeField, Min(10f), Tooltip("Tamaño del texto de la cuenta. El cartel va centrado en pantalla "
             + "porque el POW es un efecto global, así que esto son píxeles de UI, no unidades de mundo.")]
    private float countdownSize = 140f;

    [Header("Detonación")]
    [SerializeField, Min(0f), Tooltip("Duración del sacudón. Corto: es un golpe, no un terremoto.")]
    private float shakeDuration = 0.22f;

    [SerializeField, Min(0f), Tooltip("Amplitud del sacudón. Fuerte.")]
    private float shakeAmplitude = 0.9f;

    private bool _armed;

    /// <summary>
    /// Primer impacto tras el lanzamiento: se clava donde cae y arranca la
    /// cuenta. Se queda en el mundo hasta que estalla para que se vea dónde
    /// aterrizó, aunque el cartel de la cuenta va centrado en pantalla.
    /// </summary>
    protected override void OnProjectileHit(Collision2D collision)
    {
        if (_armed) return;
        _armed = true;

        StickInPlace();

        PowSequence.Run(
            countFrom,
            stunSeconds,
            stunTint,
            countdownSize,
            shakeDuration,
            shakeAmplitude,
            Despawn);
    }

    private void StickInPlace()
    {
        rb2d.linearVelocity  = Vector2.zero;
        rb2d.angularVelocity = 0f;
        rb2d.bodyType        = RigidbodyType2D.Static;

        // Sin collider sólido mientras cuenta: no queremos que se convierta en
        // una plataforma improvisada ni que estorbe al construir.
        foreach (Collider2D col in colliders)
            col.isTrigger = true;
    }

    private void Despawn() => gameObject.SetActive(false);

    protected override void OnDisable()
    {
        base.OnDisable();
        _armed = false;
    }
}
