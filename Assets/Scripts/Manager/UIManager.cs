using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Arena UI of a level: only the round countdown (3 · 2 · 1 · GO!).
///
/// Everything about joining, readiness, scores and results lives in the
/// Lobby now (Docs/lobby.md); a level is just a playable stage. Who is who
/// is shown by each chicken's own PlayerNameTag.
/// </summary>
public class UIManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI startGameTimerText;

    [Tooltip("Seconds each step of the countdown stays on screen.")]
    [SerializeField, Min(0.1f)] private float countdownStep = 1f;

    private Sequence startGameSequence;

    private void Awake()
    {
        if (startGameTimerText != null)
            startGameTimerText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        // Leaving from the pause menu mid-countdown: the sequence would keep
        // ticking into the next scene and start a round on destroyed objects.
        startGameSequence?.Kill();
    }

    /// <summary>
    /// 3 · 2 · 1 · GO! <paramref name="prevCallback"/> fires on "1",
    /// <paramref name="callback"/> on "GO!" (the round starts).
    /// </summary>
    public void StartInitialGameSequence(Action callback, Action prevCallback)
    {
        if (startGameSequence != null)
            return;

        AudioManager.Instance.PlaySound("game_countdown");

        startGameSequence = DOTween.Sequence();
        startGameSequence.AppendCallback(() =>
        {
            startGameTimerText.gameObject.SetActive(true);
            startGameTimerText.text = "3";
        });
        startGameSequence.AppendInterval(countdownStep);
        startGameSequence.AppendCallback(() => startGameTimerText.text = "2");
        startGameSequence.AppendInterval(countdownStep);
        startGameSequence.AppendCallback(() =>
        {
            prevCallback?.Invoke();
            startGameTimerText.text = "1";
        });
        startGameSequence.AppendInterval(countdownStep);
        startGameSequence.AppendCallback(() =>
        {
            startGameTimerText.text = "GO!";
            callback.Invoke();
        });
        startGameSequence.AppendInterval(countdownStep);
        startGameSequence.AppendCallback(() =>
        {
            startGameTimerText.gameObject.SetActive(false);
            startGameSequence = null;
        });
    }

    public void StopInitialGameSequence()
    {
        if (startGameSequence == null) return;

        startGameSequence.Kill();
        startGameSequence = null;
        startGameTimerText.gameObject.SetActive(false);
    }
}
