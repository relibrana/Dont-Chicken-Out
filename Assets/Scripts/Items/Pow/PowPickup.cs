using UnityEngine;

/// <summary>
/// POW (item catalog #2). Se lleva en la mano (sep 2026: ya no se activa al
/// romper la cápsula) y **no se lanza** (oct 2026): al pulsar colocar, el POW
/// desaparece y arranca en ese momento la cuenta 3-2-1-JUMP!, centrada en
/// pantalla porque es un efecto global. Así el jugador elige el momento. El
/// aturdimiento en sí lo resuelve PowSequence.
/// </summary>
public sealed class PowPickup : HoldableItem
{
    [Header("POW")]
    [SerializeField, Range(1, 3), Tooltip("Desde dónde cuenta la cuenta regresiva. Hasta 3: es lo que dibujó Arte.")]
    private int countFrom = 3;

    [SerializeField, Min(0.5f), Tooltip("Duración del aturdimiento. Ojo: la cámara sigue subiendo (doc).")]
    private float stunSeconds = 2.5f;

    [SerializeField, Tooltip("Tinte placeholder de los jugadores aturdidos.")]
    private Color stunTint = new Color(0.6f, 0.6f, 0.75f, 1f);

    [Header("Cuenta atrás")]
    [SerializeField, Tooltip("Sprites de los números: [0] = \"1\", [1] = \"2\", [2] = \"3\" (Item-PowNum).")]
    private Sprite[] countdownSprites;

    [SerializeField, Tooltip("El \"JUMP!\" del golpe (Item-PowAviso).")]
    private Sprite jumpSprite;

    [SerializeField, Min(0.1f), Tooltip("Escala sobre el tamaño nativo de los sprites. El cartel va centrado en "
             + "pantalla porque el POW es un efecto global: el tamaño nativo son píxeles de UI.")]
    private float countdownScale = 1f;

    [Header("Detonación")]
    [SerializeField, Min(0f), Tooltip("Duración del sacudón. Corto: es un golpe, no un terremoto.")]
    private float shakeDuration = 0.22f;

    [SerializeField, Min(0f), Tooltip("Amplitud del sacudón. Fuerte.")]
    private float shakeAmplitude = 0.9f;

    /// <summary>Se usa desde el suelo o en el aire: no hay nada que colocar.</summary>
    public override bool BypassPlacementChecks => true;

    /// <summary>
    /// El botón de colocar activa el POW en vez de colocarlo. No se llama a
    /// base.PlaceHoldable(): no deja nada en el mundo.
    /// </summary>
    public override void PlaceHoldable()
    {
        PowSequence.Run(
            countFrom,
            countdownSprites,
            jumpSprite,
            countdownScale,
            stunSeconds,
            stunTint,
            shakeDuration,
            shakeAmplitude,
            onFinished: null);

        gameObject.SetActive(false);
    }
}
