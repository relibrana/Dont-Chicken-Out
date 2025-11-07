using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public class CinemachineVerticalRig2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform baseFollowTarget;
    [SerializeField] private CinemachineCamera cineCam;

    [Header("Follow")]
    [SerializeField] private float smoothTimeY = 0.5f;
    [SerializeField] private float minFollowHeight = 1f;
    [SerializeField] private float yOffset = 0f;

    [Header("Zoom")]
    [SerializeField] private float normalOrthoSize = 8f;
    [SerializeField] private float winnerOrthoSize = 4.5f;
    [SerializeField] private float zoomSmooth = 0.4f;

    [Header("Shake")]
    [SerializeField] private float defaultShakeDuration = 0.35f;
    [SerializeField] private float defaultShakeAmplitude = 0.5f;
    [SerializeField] private float shakeFrequency = 25f;
    [SerializeField] private bool shakeAffectsX = true;
    [SerializeField] private bool shakeAffectsY = true;
    public bool canMove = false;

    // Estado interno
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
        if(Input.GetKeyDown(KeyCode.Z))
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

        float highestY = float.NegativeInfinity;
        foreach (var p in players)
        {
            if (p == null || !p.isActiveAndEnabled) continue;
            highestY = Mathf.Max(highestY, p.transform.position.y);
        }
        if (highestY == float.NegativeInfinity) return;

        highestY = Mathf.Max(highestY, minFollowHeight);
        FollowUpOnly(highestY);
        ZoomTo(normalOrthoSize);
    }

    void FollowUpOnly(float targetY)
    {
        MaxHeightReached = Mathf.Max(MaxHeightReached, targetY);
        float desiredY = MaxHeightReached + yOffset;

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
    /// Temblor visual
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
    /// Reinicia estado de c�mara para una nueva ronda.
    /// </summary>
    public void ResetToGameplay()
    {
        cineCam.Follow = baseFollowTarget;
        _focusWinner = false;
        _winner = null;
        _velY = 0f;
        _velZoom = 0f;
        LensOrthoSize = normalOrthoSize;
        MaxHeightReached = 0f;
        

        if (baseFollowTarget != null) baseFollowTarget.position = Vector3.zero;

        if (baseFollowTarget != null)
        {
            baseFollowTarget.localPosition = Vector3.zero;
        }
        _shakeTimeLeft = 0f;
        _lastAppliedShake = Vector3.zero;
    }
}
