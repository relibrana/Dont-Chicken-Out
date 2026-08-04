using System;
using UnityEngine;

/// <summary>
/// Scaling rate for a single progression variable, expressed in percentage
/// points over the base value, exactly like the design doc tables.
///
/// Phase 1 is always 100%. Each later phase adds <see cref="ratePerPhase"/>
/// (halved from <see cref="ProgressionValuesSO.halfRateFromPhase"/> onward).
/// </summary>
[Serializable]
public struct ScalingRate
{
    [Tooltip("Percentage points added per phase. Positive = faster. Negative = shorter wait (also faster).")]
    public float ratePerPhase;

    [Tooltip("Lower clamp, in % of the base value. 0 = no floor. Wait times need one so they never reach zero.")]
    public float floorPercent;

    [Tooltip("Upper clamp, in % of the base value. 0 = no ceiling (the doc leaves speeds uncapped).")]
    public float ceilingPercent;

    public ScalingRate(float ratePerPhase, float floorPercent = 0f, float ceilingPercent = 0f)
    {
        this.ratePerPhase   = ratePerPhase;
        this.floorPercent   = floorPercent;
        this.ceilingPercent = ceilingPercent;
    }
}

/// <summary>
/// Full tuning surface for the progression system ("sistema del listón").
/// Every number in the design doc lives here — no magic values in code.
///
/// Defaults reproduce the doc's first iteration exactly:
///   cadence 100 / 85 / 70 / 55 / 40 (floor 40)
///   up:   camera +6, lateral +5, block fall +9, push +7
///   down: generation -6, cadence -6, camera pause -8 (floors 50 / 50 / 40)
///   half rates from phase 7 onward
/// </summary>
[CreateAssetMenu(fileName = "Progression Values")]
public class ProgressionValuesSO : ScriptableObject
{
    // ── Ribbon cadence (doc §3) ───────────────────────────────────────────────

    [Header("Ribbon cadence")]
    [Tooltip("Seconds from round start until the first ribbon (L1) appears.")]
    public float firstInterval = 100f;

    [Tooltip("Seconds removed from the interval on every phase. Negative shortens it.")]
    public float intervalStepPerPhase = -15f;

    [Tooltip("Shortest allowed interval between ribbons. Stops the system from tripping over itself.")]
    public float minInterval = 40f;

    // ── Rate curve shape (doc §4.1 / §4.2) ────────────────────────────────────

    [Header("Rate curve")]
    [Tooltip("From this phase onward every increment and reduction is multiplied by Half Rate Multiplier.")]
    public int halfRateFromPhase = 7;

    [Tooltip("Multiplier applied to all rates from Half Rate From Phase onward.")]
    public float halfRateMultiplier = 0.5f;

    // ── Per-variable rates ────────────────────────────────────────────────────

    [Header("Variables that speed up (§4.1)")]
    [Tooltip("Camera auto-rise speed. The doc's main pressure lever: raise to +7 if testing shows too little pressure.")]
    public ScalingRate cameraSpeed = new ScalingRate(6f);

    [Tooltip("Player horizontal max speed. NOTE: the jump stays fixed, so this also widens the jump arc.")]
    public ScalingRate lateralSpeed = new ScalingRate(5f);

    [Tooltip("Gravity scale of blocks. Affects how fast a placed block falls AND how heavy the tower feels.")]
    public ScalingRate blockFall = new ScalingRate(9f);

    [Tooltip("NO SE CONSUME. El push es empujar caminando contra el bloque, así que ya escala a través "
             + "de Lateral Speed (+5). Se mantiene el campo para no perder el valor del documento de diseño.")]
    public ScalingRate push = new ScalingRate(7f);

    [Header("Waits that shrink (§4.2)")]
    [Tooltip("Delay between receiving a block and being able to place it. Floor 50%.")]
    public ScalingRate blockGeneration = new ScalingRate(-6f, 50f);

    [Tooltip("Cooldown between two placements. Floor 50%. Together with generation this drives blocks/second.")]
    public ScalingRate placementCadence = new ScalingRate(-6f, 50f);

    [Tooltip("Time the camera takes to climb back to full speed after a ribbon breaks (the breather in §5). "
             + "Shorter every phase, so late phases recover almost instantly. Floor 40%.")]
    public ScalingRate cameraPause = new ScalingRate(-8f, 40f);

    // ── Ribbon presentation ───────────────────────────────────────────────────

    [Header("Ribbon placement")]
    [Tooltip("World units above the TOP of the current view where the ribbon spawns and anchors.")]
    public float spawnOffsetAboveView = 3f;

    [Tooltip("Total width of the ribbon. It must cover the whole play area so it cannot be dodged sideways.\n"
             + "0 = automatic: the full visible width plus a margin, which survives any aspect ratio.")]
    public float ribbonWidth = 0f;

    [Tooltip("World X where the ribbon is centred.")]
    public float ribbonCenterX = 0f;

    [Tooltip("Height of the ribbon trigger box.")]
    public float ribbonHeight = 0.5f;

    [Tooltip("Safety net: if the ribbon ends up this far BELOW the view without being touched, it breaks itself. "
             + "Without this the phase timer would stay frozen for the rest of the round.")]
    public float autoBreakMarginBelowView = 3f;

    // ── Camera pause ──────────────────────────────────────────────────────────

    [Header("Camera pause")]
    [Tooltip("When a ribbon breaks the camera drops to 0 and climbs back to the new phase speed over these "
             + "seconds (smoothstep). Scaled by the Camera Pause rate, so the recovery gets shorter every phase.\n"
             + "This is the descending edge of the sawtooth: the camera never actually freezes, it restarts.\n"
             + "Player follow is unaffected — a player above the view is never left behind.")]
    public float cameraPauseDuration = 1.2f;

    // ── Feedback ──────────────────────────────────────────────────────────────

    [Header("Audio (ids from AudioManager)")]
    [Tooltip("SFX played when a player breaks the ribbon. Leave empty to play nothing.")]
    public string ribbonBreakSfxId = "ribbon_break";

    [Tooltip("SFX played when the new phase is applied. Leave empty to play nothing.")]
    public string phaseChangeSfxId = "phase_change";

    [Header("Music tempo")]
    [Tooltip("Interim tempo scaling: raises the music AudioSource pitch per phase. "
             + "Pitch shifts the key as well — replace with an FMOD tempo parameter when audio provides one.")]
    public float musicPitchPerPhase = 0.03f;

    [Tooltip("Hard cap for the music pitch so high phases do not turn the track into chipmunks.")]
    public float musicPitchMax = 1.25f;

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>Seconds the given phase lasts before its ribbon appears (doc §3 table).</summary>
    public float IntervalForPhase(int phase)
    {
        float interval = firstInterval + intervalStepPerPhase * (phase - 1);
        return Mathf.Max(minInterval, interval);
    }

    /// <summary>Percentage of the base value for a variable at a given phase. Phase 1 is always 100.</summary>
    public float PercentForPhase(ProgressionVar variable, int phase)
    {
        ScalingRate rate = GetRate(variable);
        float percent    = 100f;

        for (int p = 2; p <= phase; p++)
            percent += p < halfRateFromPhase ? rate.ratePerPhase : rate.ratePerPhase * halfRateMultiplier;

        if (rate.floorPercent   > 0f) percent = Mathf.Max(percent, rate.floorPercent);
        if (rate.ceilingPercent > 0f) percent = Mathf.Min(percent, rate.ceilingPercent);

        return percent;
    }

    /// <summary>Multiplier applied to the base value at a given phase (1.0 at phase 1).</summary>
    public float ScalarForPhase(ProgressionVar variable, int phase) => PercentForPhase(variable, phase) * 0.01f;

    public ScalingRate GetRate(ProgressionVar variable) => variable switch
    {
        ProgressionVar.CameraSpeed      => cameraSpeed,
        ProgressionVar.LateralSpeed     => lateralSpeed,
        ProgressionVar.BlockFall        => blockFall,
        ProgressionVar.Push             => push,
        ProgressionVar.BlockGeneration  => blockGeneration,
        ProgressionVar.PlacementCadence => placementCadence,
        ProgressionVar.CameraPause      => cameraPause,
        _                               => new ScalingRate(0f),
    };

    // ── Safety check (doc §4.3) ───────────────────────────────────────────────

    /// <summary>
    /// Reproduces the doc's §4.3 verification: the ability to build must always
    /// outrun the camera. Blocks per second is driven by the two wait times, so
    /// its ratio is 1 / (time scalar).
    /// Logs a warning on any phase where the camera catches up.
    /// </summary>
    [ContextMenu("Log safety check (§4.3)")]
    public void LogSafetyCheck()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Fase | Cámara | Bloques/seg | OK");

        bool anyViolation = false;

        for (int phase = 1; phase <= 12; phase++)
        {
            float camera     = ScalarForPhase(ProgressionVar.CameraSpeed, phase);
            float generation = ScalarForPhase(ProgressionVar.BlockGeneration,  phase);
            float cadence    = ScalarForPhase(ProgressionVar.PlacementCadence, phase);

            // Blocks/second is the inverse of the total wait per block.
            float waitScalar = (generation + cadence) * 0.5f;
            float blocksRate = waitScalar > 0f ? 1f / waitScalar : float.PositiveInfinity;

            bool ok = blocksRate >= camera;
            if (!ok) anyViolation = true;

            sb.AppendLine($"{phase,4} | {camera,6:0.00} | {blocksRate,11:0.00} | {(ok ? "ok" : "CÁMARA ADELANTA")}");
        }

        if (anyViolation)
            Debug.LogWarning($"[Progression] La cámara supera la capacidad de construir en alguna fase:\n{sb}");
        else
            Debug.Log($"[Progression] Verificación de seguridad §4.3 correcta:\n{sb}");
    }

    private void OnValidate()
    {
        firstInterval      = Mathf.Max(1f, firstInterval);
        minInterval        = Mathf.Max(1f, minInterval);
        halfRateFromPhase  = Mathf.Max(2,  halfRateFromPhase);
        ribbonWidth        = Mathf.Max(0f, ribbonWidth);
        ribbonHeight       = Mathf.Max(0.05f, ribbonHeight);
        cameraPauseDuration = Mathf.Max(0f, cameraPauseDuration);
    }
}
