using System;
using UnityEngine;

/// <summary>
/// Drives the progression system (doc "Sistema de progresión").
///
/// Loop:
///   1. A phase timer runs in the background during GameState.Game.
///   2. On zero the ribbon spawns at a fixed offset above the view, anchored
///      to the world, and the timer freezes.
///   3. The first player to touch the ribbon breaks it.
///   4. The break applies the next phase: all seven scalars are recomputed,
///      the camera pauses briefly and the timer restarts with the new interval.
///
/// Consumers never read this class directly for values — they call the static
/// <see cref="Get"/>, which returns 1.0 when no progression manager exists.
/// That keeps menus, tests and scenes without the system working unchanged.
///
/// Netcode (Fusion 2, host mode): all mutation funnels through
/// <see cref="ApplyPhase"/> and <see cref="Tick"/>, and <see cref="isAuthority"/>
/// gates who may spawn and break the ribbon. A networked wrapper only needs to
/// replicate CurrentPhase + the timer and call ApplyPhase on clients.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProgressionManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static ProgressionManager Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("All tuning lives here. Without it the system stays inert and every scalar is 1.0.")]
    [SerializeField] private ProgressionValuesSO values;

    [Tooltip("Ribbon prefab. Leave empty to use a runtime placeholder while art is pending.")]
    [SerializeField] private ProgressionRibbon ribbonPrefab;

    [Tooltip("Parent for the spawned ribbon. MUST NOT be the camera or anything that moves — "
             + "the ribbon is anchored to the world on purpose.")]
    [SerializeField] private Transform ribbonParent;

    [Tooltip("Camera used to decide where the ribbon spawns. Leave empty to resolve it automatically: "
             + "Camera.main first, then the only Camera in the scene.")]
    [SerializeField] private Camera viewCamera;

    [Header("Placeholder")]
    [SerializeField] private Color placeholderColor = new Color(1f, 0.85f, 0.2f, 0.9f);

    [Header("Netcode")]
    [Tooltip("Host authority. When false this instance only mirrors phases pushed by the host.")]
    public bool isAuthority = true;

    // ── Public state ──────────────────────────────────────────────────────────

    /// <summary>Current phase, 1-based. Phase 1 is the untouched round start.</summary>
    public int CurrentPhase { get; private set; } = 1;

    /// <summary>Seconds left before the next ribbon spawns. Frozen while a ribbon is up.</summary>
    public float TimeToNextRibbon => _timer;

    /// <summary>True while the timer is frozen waiting for the ribbon to be broken.</summary>
    public bool RibbonIsUp => _ribbon != null && _ribbon.IsLive;

    /// <summary>True while the camera is climbing back to full speed after a break.</summary>
    public bool IsCameraRecovering => _recoveryTimer > 0f;

    /// <summary>
    /// Multiplier applied to the camera speed, 0 to 1.
    /// A ribbon break knocks the camera down to 0 and it climbs back to the new
    /// phase speed over the recovery window — the descending edge of the
    /// sawtooth. Returns 1 whenever no recovery is running.
    /// </summary>
    public float CameraSpeedFactor
    {
        get
        {
            if (_recoveryTimer <= 0f || _recoveryDuration <= 0f) return 1f;

            float t = 1f - Mathf.Clamp01(_recoveryTimer / _recoveryDuration);
            return Mathf.SmoothStep(0f, 1f, t);
        }
    }

    /// <summary>Fires with the new phase right after the scalars are applied.</summary>
    public event Action<int> OnPhaseChanged;

    /// <summary>Fires when a ribbon appears.</summary>
    public event Action OnRibbonSpawned;

    /// <summary>Fires when a ribbon is broken. Argument is the breaker, null if broken by the safety net.</summary>
    public event Action<PlayerController> OnRibbonBroken;

    // ── Private state ─────────────────────────────────────────────────────────

    private static readonly int VarCount = Enum.GetValues(typeof(ProgressionVar)).Length;

    private readonly float[] _scalars = new float[VarCount];

    private ProgressionRibbon _ribbon;
    private Camera            _cam;
    private bool              _cameraWarningLogged;

    private float _timer;
    private bool  _timerRunning;

    // Camera recovery after a break: counts down from _recoveryDuration to 0.
    private float _recoveryTimer;
    private float _recoveryDuration;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        // Scalars must be valid before any consumer's first Update.
        ResetForRound();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    private void Update()
    {
        // Editor-only shortcuts for balancing, mirroring CinemachineVerticalRig2D's Z/X keys.
        if (Input.GetKeyDown(KeyCode.P)) ForceSpawnRibbon();
        if (Input.GetKeyDown(KeyCode.O)) ForceBreakRibbon();
    }
#endif

    // ── Driven by GameManager ─────────────────────────────────────────────────

    /// <summary>
    /// Advances the system. Called once per frame by GameManager while in
    /// GameState.Game, so it inherits pause handling and round state for free.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (values == null) return;

        if (_recoveryTimer > 0f)
            _recoveryTimer -= deltaTime;

        if (RibbonIsUp)
        {
            CheckRibbonSafetyNet();
            return; // Timer stays frozen until the ribbon is broken (doc §2.3).
        }

        if (!_timerRunning) return;

        _timer -= deltaTime;
        if (_timer <= 0f)
        {
            _timer = 0f;
            SpawnRibbon();
        }
    }

    /// <summary>
    /// Returns the system to phase 1. Called at the start of every round —
    /// the match is first-to-N, so progression must not leak between rounds.
    /// </summary>
    public void ResetForRound()
    {
        CurrentPhase      = 1;
        _recoveryTimer    = 0f;
        _recoveryDuration = 0f;

        RecalculateScalars();

        _timer        = values != null ? values.IntervalForPhase(1) : 0f;
        _timerRunning = values != null;

        _ribbon?.Despawn();

        AudioManager.Instance?.SetMusicPitch(1f);
    }

    // ── Phase logic ───────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a phase: recomputes every scalar, starts the camera pause and
    /// restarts the timer with the new interval.
    /// Single entry point for all phase mutation — the netcode wrapper calls
    /// this on clients with the phase replicated by the host.
    /// </summary>
    public void ApplyPhase(int phase)
    {
        if (values == null) return;

        CurrentPhase = Mathf.Max(1, phase);

        RecalculateScalars();

        // Knock the camera down to zero and let it climb back to the new speed.
        _recoveryDuration = values.cameraPauseDuration * Scalar(ProgressionVar.CameraPause);
        _recoveryTimer    = _recoveryDuration;

        _timer        = values.IntervalForPhase(CurrentPhase);
        _timerRunning = true;

        ApplyMusicTempo();
        PlaySfx(values.phaseChangeSfxId);

        OnPhaseChanged?.Invoke(CurrentPhase);
    }

    private void RecalculateScalars()
    {
        for (int i = 0; i < VarCount; i++)
        {
            _scalars[i] = values != null
                ? values.ScalarForPhase((ProgressionVar)i, CurrentPhase)
                : 1f;
        }
    }

    // ── Ribbon ────────────────────────────────────────────────────────────────

    private void SpawnRibbon()
    {
        if (!isAuthority || values == null) return;

        // Without a camera the spawn height would be meaningless and the ribbon
        // would land at the world origin, below everyone, frozen forever.
        if (!HasCamera) return;

        EnsureRibbonExists();
        if (_ribbon == null) return;

        float spawnY = ViewTopY + values.spawnOffsetAboveView;

        // Width 0 means "cover the whole visible area", which is what the design
        // doc asks for and survives any aspect ratio.
        float width = values.ribbonWidth > 0f
            ? values.ribbonWidth
            : ViewHalfWidth * 2f + values.spawnOffsetAboveView;

        _ribbon.Spawn(
            new Vector2(values.ribbonCenterX, spawnY),
            width,
            values.ribbonHeight,
            HandleRibbonBroken);

        _timerRunning = false;

        OnRibbonSpawned?.Invoke();
    }

    private void HandleRibbonBroken(PlayerController breaker)
    {
        if (values != null)
            PlaySfx(values.ribbonBreakSfxId);

        OnRibbonBroken?.Invoke(breaker);

        ApplyPhase(CurrentPhase + 1);
    }

    /// <summary>
    /// Safety net. If the ribbon is left behind by the camera without anyone
    /// touching it, break it anyway — otherwise the timer stays frozen and the
    /// round never progresses again.
    /// </summary>
    private void CheckRibbonSafetyNet()
    {
        if (_ribbon == null || !_ribbon.IsLive) return;

        float deadLine = ViewBottomY - values.autoBreakMarginBelowView;
        if (_ribbon.transform.position.y > deadLine) return;

        Debug.LogWarning("[Progression] El listón quedó por debajo de la cámara sin romperse — "
                         + "se rompe automáticamente para no congelar el cronómetro.");
        _ribbon.ForceBreak();
    }

    private void EnsureRibbonExists()
    {
        if (_ribbon != null) return;

        _ribbon = ribbonPrefab != null
            ? Instantiate(ribbonPrefab, ribbonParent)
            : ProgressionRibbon.CreatePlaceholder(ribbonParent, placeholderColor);
    }

    // ── Value access ──────────────────────────────────────────────────────────

    /// <summary>Multiplier for a variable at the current phase.</summary>
    public float Scalar(ProgressionVar variable) => _scalars[(int)variable];

    /// <summary>
    /// Multiplier for a variable, safe to call from anywhere.
    /// Returns 1.0 when no progression manager is present in the scene.
    /// </summary>
    public static float Get(ProgressionVar variable) =>
        Instance != null ? Instance._scalars[(int)variable] : 1f;

    // ── Feedback ──────────────────────────────────────────────────────────────

    private void PlaySfx(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        AudioManager.Instance?.PlaySound(id);
    }

    private void ApplyMusicTempo()
    {
        if (values == null || AudioManager.Instance == null) return;

        float pitch = 1f + values.musicPitchPerPhase * (CurrentPhase - 1);
        AudioManager.Instance.SetMusicPitch(Mathf.Min(pitch, values.musicPitchMax));
    }

    // ── View helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the camera that defines the visible area.
    /// Camera.main is NOT reliable here: no camera in this project carries the
    /// MainCamera tag, so it returns null and every ribbon would spawn at the
    /// world origin. The scene lookup is the fallback that actually works.
    /// </summary>
    private Camera Cam
    {
        get
        {
            if (_cam != null) return _cam;

            _cam = viewCamera != null ? viewCamera : Camera.main;

            if (_cam == null)
                _cam = FindAnyObjectByType<Camera>();

            if (_cam == null && !_cameraWarningLogged)
            {
                _cameraWarningLogged = true;
                Debug.LogError("[Progression] No hay ninguna cámara en la escena: el listón no puede "
                               + "calcular dónde aparecer. Asigna View Camera en el ProgressionManager.");
            }

            return _cam;
        }
    }

    /// <summary>Half height of the visible area in world units.</summary>
    private float ViewHalfHeight => Cam != null
        ? (Cam.orthographic ? Cam.orthographicSize : Mathf.Abs(Cam.transform.position.z) * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad))
        : 0f;

    /// <summary>Half width of the visible area in world units.</summary>
    private float ViewHalfWidth => ViewHalfHeight * (Cam != null ? Cam.aspect : 1f);

    private bool HasCamera => Cam != null;

    private float ViewCenterY => Cam != null ? Cam.transform.position.y : 0f;
    private float ViewTopY    => ViewCenterY + ViewHalfHeight;
    private float ViewBottomY => ViewCenterY - ViewHalfHeight;

    // ── Debug ─────────────────────────────────────────────────────────────────

    [ContextMenu("Force ribbon now")]
    public void ForceSpawnRibbon()
    {
        // Guarded so the context menu cannot litter the scene outside play mode.
        if (!Application.isPlaying || RibbonIsUp) return;
        SpawnRibbon();
    }

    [ContextMenu("Force break ribbon")]
    public void ForceBreakRibbon()
    {
        if (!RibbonIsUp) return;
        _ribbon.ForceBreak();
    }

    [ContextMenu("Log current scalars")]
    public void LogCurrentScalars()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Progression] Fase {CurrentPhase} — siguiente listón en {_timer:0.0}s");

        for (int i = 0; i < VarCount; i++)
            sb.AppendLine($"  {(ProgressionVar)i,-17}: {_scalars[i] * 100f:0.#}%");

        Debug.Log(sb.ToString());
    }
}
