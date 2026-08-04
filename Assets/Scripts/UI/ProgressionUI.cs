using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// On-screen feedback for the progression system: the "Faster!" notice that
/// communicates the speed increase (doc §6.2) plus an optional phase/timer
/// readout for balancing sessions.
///
/// Purely reactive — it only listens to ProgressionManager events and never
/// drives gameplay, so it can be dropped in or removed without side effects.
/// </summary>
public sealed class ProgressionUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Faster! notice")]
    [Tooltip("Label shown on every phase change. Its GameObject is toggled by this script.")]
    [SerializeField] private TextMeshProUGUI fasterLabel;

    [Tooltip("Text shown on the notice. {0} is replaced by the new phase number.")]
    [SerializeField] private string fasterFormat = "FASTER!";

    [SerializeField] private float noticeHoldTime  = 0.9f;
    [SerializeField] private float noticePunchTime = 0.35f;
    [SerializeField] private float noticePunchScale = 0.4f;

    [Header("Ribbon incoming")]
    [Tooltip("Optional label shown while a ribbon is up and waiting to be broken.")]
    [SerializeField] private GameObject ribbonIndicator;

    [Header("Balancing readout")]
    [Tooltip("Optional. Shows current phase and seconds to the next ribbon. Disable for builds.")]
    [SerializeField] private TextMeshProUGUI debugReadout;

    // ── Private state ─────────────────────────────────────────────────────────

    private Sequence _noticeSequence;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        // Subscribed in Start so ProgressionManager.Awake has already run.
        if (ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnPhaseChanged  += HandlePhaseChanged;
            ProgressionManager.Instance.OnRibbonSpawned += HandleRibbonSpawned;
            ProgressionManager.Instance.OnRibbonBroken  += HandleRibbonBroken;
        }

        if (fasterLabel    != null) fasterLabel.gameObject.SetActive(false);
        if (ribbonIndicator != null) ribbonIndicator.SetActive(false);
    }

    private void OnDestroy()
    {
        _noticeSequence?.Kill();

        if (ProgressionManager.Instance == null) return;

        ProgressionManager.Instance.OnPhaseChanged  -= HandlePhaseChanged;
        ProgressionManager.Instance.OnRibbonSpawned -= HandleRibbonSpawned;
        ProgressionManager.Instance.OnRibbonBroken  -= HandleRibbonBroken;
    }

    private void Update()
    {
        if (debugReadout == null) return;

        ProgressionManager progression = ProgressionManager.Instance;
        if (progression == null)
        {
            debugReadout.text = string.Empty;
            return;
        }

        debugReadout.text = progression.RibbonIsUp
            ? $"Fase {progression.CurrentPhase} — listón activo"
            : $"Fase {progression.CurrentPhase} — {progression.TimeToNextRibbon:0.0}s";
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void HandlePhaseChanged(int newPhase)
    {
        if (fasterLabel == null) return;

        fasterLabel.text = string.Format(fasterFormat, newPhase);
        fasterLabel.gameObject.SetActive(true);
        fasterLabel.transform.localScale = Vector3.one;

        _noticeSequence?.Kill();
        _noticeSequence = DOTween.Sequence();
        _noticeSequence.Append(fasterLabel.transform.DOPunchScale(
            Vector3.one * noticePunchScale, noticePunchTime, 6, 0.6f));
        _noticeSequence.AppendInterval(noticeHoldTime);
        _noticeSequence.AppendCallback(() =>
        {
            fasterLabel.gameObject.SetActive(false);
            _noticeSequence = null;
        });
    }

    private void HandleRibbonSpawned()
    {
        if (ribbonIndicator != null) ribbonIndicator.SetActive(true);
    }

    private void HandleRibbonBroken(PlayerController breaker)
    {
        if (ribbonIndicator != null) ribbonIndicator.SetActive(false);
    }
}
