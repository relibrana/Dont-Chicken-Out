using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public class CinemachineVerticalRig2D : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform 'ancla' que la cámara sigue. Normalmente es un objeto vacío que esta cámara moverá verticalmente.")]
    [SerializeField] private Transform baseFollowTarget;

    [Tooltip("Referencia a la CinemachineCamera que se va a controlar.")]
    [SerializeField] private CinemachineCamera cineCam;

    [Header("Follow")]
    [Tooltip("Tiempo de amortiguación vertical (en segundos). Valores más pequeños = respuesta más rápida (la cámara sube más rápido). Valores grandes = movimiento más suave pero más lento.")]
    [SerializeField] private float smoothTimeY = 0.5f;

    [Tooltip("Altura mínima que los jugadores deben superar para que la cámara comience a elevarse. Si nadie supera este valor, la cámara no sube.")]
    [SerializeField] private float minFollowHeight = 1f;

    [Tooltip("Desfase vertical adicional aplicado por encima de la altura máxima alcanzada. Útil para dejar más 'aire' encima del personaje.")]
    [SerializeField] private float yOffset = 0f;

    [Header("Zoom")]
    [Tooltip("Tamaño ortográfico base durante el gameplay.")]
    [SerializeField] private float normalOrthoSize = 8f;

    [Tooltip("Tamaño ortográfico cuando se enfoca al ganador (zoom-in).")]
    [SerializeField] private float winnerOrthoSize = 4.5f;

    [Tooltip("Amortiguación del cambio de zoom. Menor = zoom más rápido, Mayor = más suave pero más lento.")]
    [SerializeField] private float zoomSmooth = 0.4f;

    [Header("Shake")]
    [Tooltip("Duración por defecto del temblor de cámara.")]
    [SerializeField] private float defaultShakeDuration = 0.35f;

    [Tooltip("Amplitud por defecto del temblor de cámara.")]
    [SerializeField] private float defaultShakeAmplitude = 0.5f;

    [Tooltip("Frecuencia de muestreo del 'ruido' del shake.")]
    [SerializeField] private float shakeFrequency = 25f;

    [Tooltip("Si está activo, el shake también afecta el eje X.")]
    [SerializeField] private bool shakeAffectsX = true;

    [Tooltip("Si está activo, el shake también afecta el eje Y.")]
    [SerializeField] private bool shakeAffectsY = true;

    [Tooltip("Habilita/deshabilita el movimiento de cámara por código (útil para pausar o durante cinemáticas).")]
    public bool canMove = false;

    // Estado interno
    [Tooltip("Mayor altura REAL alcanzada por los jugadores en esta sesión. Solo sube cuando alguien sobrepasa minFollowHeight.")]
    public float MaxHeightReached { get; set; } = 0f;

    float _velY, _velZoom;
    bool _focusWinner;
    Transform _winner;

    float _shakeTimeLeft = 0f;
    float _shakeAmplitude = 0f;
    Vector2 _shakeSeed;
    Vector3 _lastAppliedShake = Vector3.zero;

    float LensOrthoSize
    {
        get => cineCam != null ? cineCam.Lens.OrthographicSize : Camera.main.orthographicSize;
        set
        {
            if (cineCam != null)
            {
                var lens = cineCam.Lens;
                lens.OrthographicSize = value;
                cineCam.Lens = lens;
            }
            else
            {
                Camera.main.orthographicSize = value;
            }
        }
    }

    void Awake()
    {
        if (baseFollowTarget == null) Debug.LogWarning("[CinemachineVerticalRig2D] baseFollowTarget no asignado.");
        if (cineCam == null) Debug.LogWarning("[CinemachineVerticalRig2D] cineCam no asignado.");

        LensOrthoSize = normalOrthoSize;
        _shakeSeed = new Vector2(Random.Range(0f, 1000f), Random.Range(0f, 1000f));
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Z))
        {
            DoDeathShake();
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            FocusWinner(FindAnyObjectByType<PlayerController>()?.transform);
        }
#endif

        if (baseFollowTarget == null || !canMove) return;

        UpdateShakeOffset();

        if (_focusWinner && _winner != null)
        {
            FollowUpOnly(_winner.position.y);
            ZoomTo(winnerOrthoSize);
            return;
        }

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        if (players == null || players.Length == 0) return;

        float highestGroundedY = float.NegativeInfinity;

        foreach (var p in players)
        {
            if (p == null || !p.isActiveAndEnabled) continue;
            if (!p.IsGrounded) continue;

            highestGroundedY = Mathf.Max(highestGroundedY, p.transform.position.y);
        }

        if (highestGroundedY != float.NegativeInfinity && highestGroundedY > minFollowHeight)
        {
            MaxHeightReached = Mathf.Max(MaxHeightReached, highestGroundedY);
        }

        FollowUpOnly(MaxHeightReached);
        ZoomTo(normalOrthoSize);

    }

    void FollowUpOnly(float targetY)
    {
        float desiredY = targetY + yOffset;

        Vector3 pos = baseFollowTarget.position;
        float newY = Mathf.SmoothDamp(pos.y, desiredY, ref _velY, smoothTimeY);
        baseFollowTarget.position = new Vector3(pos.x, newY, pos.z);
    }

    void ZoomTo(float targetSize)
    {
        float newSize = Mathf.SmoothDamp(LensOrthoSize, targetSize, ref _velZoom, zoomSmooth);
        LensOrthoSize = newSize;
    }

    void UpdateShakeOffset()
    {
        if (_lastAppliedShake != Vector3.zero)
        {
            baseFollowTarget.localPosition -= _lastAppliedShake;
            _lastAppliedShake = Vector3.zero;
        }

        if (_shakeTimeLeft <= 0f) return;

        _shakeTimeLeft -= Time.deltaTime;

        float t = Mathf.Clamp01(_shakeTimeLeft / defaultShakeDuration);
        float amp = _shakeAmplitude * t;

        float time = Time.time * shakeFrequency;
        float nx = Mathf.PerlinNoise(_shakeSeed.x, time) * 2f - 1f;
        float ny = Mathf.PerlinNoise(_shakeSeed.y, time) * 2f - 1f;

        float ox = shakeAffectsX ? nx * amp : 0f;
        float oy = shakeAffectsY ? ny * amp : 0f;

        _lastAppliedShake = new Vector3(ox, oy, 0f);
        baseFollowTarget.localPosition += _lastAppliedShake;
    }

    /// <summary>
    /// Temblor visual.
    /// </summary>
    public void DoDeathShake(float duration = -1f, float amplitude = -1f)
    {
        _shakeTimeLeft = (duration > 0f) ? duration : defaultShakeDuration;
        _shakeAmplitude = (amplitude > 0f) ? amplitude : defaultShakeAmplitude;
    }

    /// <summary>
    /// Enfoca ganador y hace zoom.
    /// </summary>
    public void FocusWinner(Transform winner)
    {
        if (winner == null) return;

        cineCam.Follow = winner;
        _winner = winner;
        _focusWinner = true;

        if (_lastAppliedShake != Vector3.zero)
        {
            baseFollowTarget.localPosition -= _lastAppliedShake;
            _lastAppliedShake = Vector3.zero;
        }
        _shakeTimeLeft = 0f;
    }

    /// <summary>
    /// Reinicia estado de cámara para una nueva ronda.
    /// </summary>
    public void ResetToGameplay()
    {
        if (cineCam != null) cineCam.Follow = baseFollowTarget;
        _focusWinner = false;
        _winner = null;
        _velY = 0f;
        _velZoom = 0f;
        LensOrthoSize = normalOrthoSize;
        MaxHeightReached = 0f;

        if (baseFollowTarget != null) baseFollowTarget.position = Vector3.zero;
        if (baseFollowTarget != null) baseFollowTarget.localPosition = Vector3.zero;

        _shakeTimeLeft = 0f;
        _lastAppliedShake = Vector3.zero;
    }

    /// <summary>
    /// Quita el enfoque al jugador ganador, se utiliza para el empate.
    /// </summary>
    public void StopFocusWinner()
    {
        if (cineCam != null) cineCam.Follow = baseFollowTarget;
        _focusWinner = false;
        _winner = null;
        _velY = 0f;
        _velZoom = 0f;
    }

    void OnDrawGizmos()
    {
        if (cineCam == null) return;
        Vector3 camPos = cineCam.transform.position;

        Gizmos.color = Color.green;   // Umbral para empezar a seguir
        Gizmos.DrawLine(new Vector3(camPos.x - 10f, minFollowHeight, camPos.z), new Vector3(camPos.x + 10f, minFollowHeight, camPos.z));

        Gizmos.color = Color.red;     // Altura máxima real alcanzada
        Gizmos.DrawLine(new Vector3(camPos.x - 10f, MaxHeightReached, camPos.z), new Vector3(camPos.x + 10f, MaxHeightReached, camPos.z));
    }
}
