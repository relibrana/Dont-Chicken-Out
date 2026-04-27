using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Controls vertical (and winner-follow) camera movement for a 2D platformer.
///
/// Architecture:
///   baseFollowTarget  — driven by SmoothDamp; GameManager reads/writes MaxHeightReached.
///       └── shakeTarget (created in Awake) — receives only shake offsets;
///                                            CinemachineCamera follows this child.
///
/// Three states:
///   1. Reset      — baseFollowTarget at origin, canMove = false.
///   2. Gameplay   — SmoothDamp toward MaxHeightReached.
///                   MaxHeightReached is raised by GameManager (auto-move) or by this
///                   rig when a live grounded player is above it (at playerFollowSpeed).
///   3. WinnerFocus — SmoothDamp toward winner's XY position.
/// </summary>
[DisallowMultipleComponent]
public class CinemachineVerticalRig2D : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Transform that the camera follows. This script moves it vertically during gameplay.")]
    [SerializeField] private Transform baseFollowTarget;

    [Tooltip("CinemachineCamera to control.")]
    [SerializeField] private CinemachineCamera cineCam;

    [Header("Follow")]
    [Tooltip("SmoothDamp time on the Y axis. Smaller = snappier, larger = smoother.")]
    [SerializeField] private float smoothTimeY = 0.5f;

    [Tooltip("Minimum player height before the camera starts tracking upward.")]
    [SerializeField] private float minFollowHeight = 1f;

    [Tooltip("Additional vertical offset above MaxHeightReached.")]
    [SerializeField] private float yOffset = 0f;

    [Tooltip("Speed (units/s) at which MaxHeightReached catches up to the highest live grounded player. "
             + "Uses MoveTowards so it never overshoots.")]
    [SerializeField] private float playerFollowSpeed = 0.8f;

    [Header("Zoom")]
    [Tooltip("Orthographic size during normal gameplay.")]
    [SerializeField] private float normalOrthoSize = 8f;

    [Tooltip("Orthographic size when focusing the winner.")]
    [SerializeField] private float winnerOrthoSize = 4.5f;

    [Tooltip("SmoothDamp time for zoom transitions.")]
    [SerializeField] private float zoomSmoothTime = 0.4f;

    [Header("Shake")]
    [Tooltip("Default shake duration in seconds.")]
    [SerializeField] private float defaultShakeDuration = 0.35f;

    [Tooltip("Default shake amplitude in units.")]
    [SerializeField] private float defaultShakeAmplitude = 0.5f;

    [Tooltip("Perlin noise sampling frequency (higher = faster oscillation).")]
    [SerializeField] private float shakeFrequency = 25f;

    [Tooltip("Whether the shake affects the X axis.")]
    [SerializeField] private bool shakeAffectsX = true;

    [Tooltip("Whether the shake affects the Y axis.")]
    [SerializeField] private bool shakeAffectsY = true;

    // ── Public state ──────────────────────────────────────────────────────────

    /// <summary>
    /// Enables or disables all camera movement (set by GameManager).
    /// </summary>
    public bool canMove = false;

    /// <summary>
    /// Highest Y position the camera should track to during gameplay.
    /// Written by GameManager (auto-move) and by this rig when a live
    /// grounded player exceeds the current value. Never decreases.
    /// </summary>
    public float MaxHeightReached { get; set; } = 0f;

    // ── Private state ─────────────────────────────────────────────────────────

    // SmoothDamp velocities — must be reset together with position.
    private float _velY;
    private float _velX;
    private float _velZoom;

    // Winner focus state.
    private bool      _focusWinner;
    private bool      _focusWinnerPending; // waiting for shake to finish before activating follow
    private Transform _winner;

    // Shake state — lives on shakeTarget, completely isolated from baseFollowTarget.
    private Transform _shakeTarget;   // child of baseFollowTarget, created in Awake
    private float     _shakeTimeLeft;
    private float     _shakeAmplitude;
    private Vector2   _shakeSeed;

    // ── Lens helper ───────────────────────────────────────────────────────────

    private float LensOrthoSize
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

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (baseFollowTarget == null)
            Debug.LogError("[CinemachineVerticalRig2D] baseFollowTarget not assigned — camera will not work.");

        if (cineCam == null)
            Debug.LogError("[CinemachineVerticalRig2D] cineCam not assigned — camera will not work.");

        // Create the shake target as a child of baseFollowTarget.
        // The CinemachineCamera follows this child so shake offsets never
        // contaminate the SmoothDamp velocity on baseFollowTarget.
        _shakeTarget = new GameObject("ShakeTarget").transform;

        if (baseFollowTarget != null)
        {
            _shakeTarget.SetParent(baseFollowTarget, worldPositionStays: false);
            _shakeTarget.localPosition = Vector3.zero;
        }

        // Redirect CinemachineCamera to follow the shake target.
        if (cineCam != null)
            cineCam.Follow = _shakeTarget;

        _shakeSeed    = new Vector2(Random.Range(0f, 1000f), Random.Range(0f, 1000f));
        LensOrthoSize = normalOrthoSize;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Z)) DoDeathShake();
        if (Input.GetKeyDown(KeyCode.X)) FocusWinner(FindAnyObjectByType<PlayerController>()?.transform);
#endif

        // Shake runs independently of canMove so the visual feedback always plays.
        UpdateShake();

        if (!canMove) return;

        // Once the shake expires, promote a pending winner focus to active.
        if (_focusWinnerPending && _shakeTimeLeft <= 0f)
        {
            _focusWinner        = true;
            _focusWinnerPending = false;
        }

        if (_focusWinner && _winner != null)
        {
            FollowWinner(_winner.position.x, _winner.position.y);
            ZoomTo(winnerOrthoSize);
            return;
        }

        UpdateMaxHeightFromPlayers();
        FollowUpOnly(MaxHeightReached);
        ZoomTo(_focusWinnerPending ? winnerOrthoSize : normalOrthoSize);
    }

    // ── Movement helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Raises MaxHeightReached toward the highest live grounded player at a
    /// controlled speed. Only grounded players count — jumping or airborne
    /// positions are intentionally ignored so a big jump never spikes the camera.
    /// Never decreases MaxHeightReached.
    /// </summary>
    private void UpdateMaxHeightFromPlayers()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        if (players == null || players.Length == 0) return;

        float highestGroundedY = float.NegativeInfinity;

        foreach (PlayerController p in players)
        {
            if (p == null || !p.isActiveAndEnabled || !p.isOnGame) continue;
            if (!p.IsGrounded) continue;

            highestGroundedY = Mathf.Max(highestGroundedY, p.transform.position.y);
        }

        if (highestGroundedY == float.NegativeInfinity || highestGroundedY <= minFollowHeight) return;

        // MoveTowards advances at a constant rate and never overshoots the target.
        // This means a player death (or shake) cannot cause an instant spike —
        // the camera glides toward the highest survivor at playerFollowSpeed u/s.
        if (highestGroundedY > MaxHeightReached)
            MaxHeightReached = Mathf.MoveTowards(MaxHeightReached, highestGroundedY, playerFollowSpeed * Time.deltaTime);
    }

    /// <summary>Moves baseFollowTarget upward only toward targetY.</summary>
    private void FollowUpOnly(float targetY)
    {
        float desiredY  = targetY + yOffset;
        Vector3 pos     = baseFollowTarget.position;
        float newY      = Mathf.SmoothDamp(pos.y, desiredY, ref _velY, smoothTimeY);

        // Never let the camera go below its current position during gameplay.
        baseFollowTarget.position = new Vector3(pos.x, newY, pos.z);
    }

    /// <summary>Moves baseFollowTarget toward the winner on both axes.</summary>
    private void FollowWinner(float targetX, float targetY)
    {
        float desiredY  = targetY + yOffset;
        Vector3 pos     = baseFollowTarget.position;
        float newY      = Mathf.SmoothDamp(pos.y, desiredY, ref _velY, smoothTimeY);
        float newX      = Mathf.SmoothDamp(pos.x, targetX,  ref _velX, smoothTimeY);

        baseFollowTarget.position = new Vector3(newX, newY, pos.z);
    }

    private void ZoomTo(float targetSize)
    {
        float newSize  = Mathf.SmoothDamp(LensOrthoSize, targetSize, ref _velZoom, zoomSmoothTime);
        LensOrthoSize  = newSize;
    }

    // ── Shake ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies Perlin-noise shake exclusively to _shakeTarget.localPosition.
    /// baseFollowTarget is never touched, so SmoothDamp velocities stay clean.
    /// </summary>
    private void UpdateShake()
    {
        if (_shakeTarget == null) return;

        if (_shakeTimeLeft <= 0f)
        {
            _shakeTarget.localPosition = Vector3.zero;
            return;
        }

        _shakeTimeLeft -= Time.deltaTime;

        // Fade amplitude to zero as the shake expires (smooth tail-off).
        float normalizedTime = Mathf.Clamp01(_shakeTimeLeft / defaultShakeDuration);
        float amp            = _shakeAmplitude * normalizedTime;

        float t  = Time.time * shakeFrequency;
        float nx = Mathf.PerlinNoise(_shakeSeed.x, t) * 2f - 1f;
        float ny = Mathf.PerlinNoise(_shakeSeed.y, t) * 2f - 1f;

        float ox = shakeAffectsX ? nx * amp : 0f;
        float oy = shakeAffectsY ? ny * amp : 0f;

        _shakeTarget.localPosition = new Vector3(ox, oy, 0f);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Triggers a camera shake. Safe to call at any time and from any state.
    /// </summary>
    public void DoDeathShake(float duration = -1f, float amplitude = -1f)
    {
        _shakeTimeLeft  = duration  > 0f ? duration  : defaultShakeDuration;
        _shakeAmplitude = amplitude > 0f ? amplitude : defaultShakeAmplitude;
    }

    /// <summary>
    /// Registers the winner. If a shake is active the follow waits for it to finish.
    /// The zoom transition starts immediately regardless.
    /// </summary>
    public void FocusWinner(Transform winner)
    {
        if (winner == null) return;

        _winner = winner;

        if (_shakeTimeLeft > 0f)
        {
            _focusWinnerPending = true;
            _focusWinner        = false;
        }
        else
        {
            _focusWinner        = true;
            _focusWinnerPending = false;
        }
    }

    /// <summary>
    /// Fully resets the rig for a new round. Call before BeginStartSequence.
    /// </summary>
    public void ResetToGameplay()
    {
        _focusWinner        = false;
        _focusWinnerPending = false;
        _winner             = null;

        _velY    = 0f;
        _velX    = 0f;
        _velZoom = 0f;

        MaxHeightReached = 0f;

        _shakeTimeLeft = 0f;

        if (baseFollowTarget != null)
            baseFollowTarget.position = Vector3.zero;

        if (_shakeTarget != null)
            _shakeTarget.localPosition = Vector3.zero;

        LensOrthoSize = normalOrthoSize;

        if (cineCam != null)
            cineCam.Follow = _shakeTarget;
    }

    /// <summary>
    /// Clears winner focus without doing a full reset (used on tie).
    /// </summary>
    public void StopFocusWinner()
    {
        _focusWinner        = false;
        _focusWinnerPending = false;
        _winner             = null;
        _velY               = 0f;
        _velX               = 0f;
        _velZoom            = 0f;
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (cineCam == null) return;
        Vector3 camPos = cineCam.transform.position;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(
            new Vector3(camPos.x - 10f, minFollowHeight, camPos.z),
            new Vector3(camPos.x + 10f, minFollowHeight, camPos.z));

        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            new Vector3(camPos.x - 10f, MaxHeightReached, camPos.z),
            new Vector3(camPos.x + 10f, MaxHeightReached, camPos.z));
    }
}