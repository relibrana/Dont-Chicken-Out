using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runs the POW countdown (item catalog #2): a shared on-screen 3-2-1, then
/// every grounded player — activator included — gets stunned; airborne players
/// are safe. Builds its own overlay canvas at runtime so no scene wiring is
/// needed (placeholder UI until it moves into UIManager).
/// </summary>
public sealed class PowSequence : MonoBehaviour
{
    private static PowSequence _instance;

    private TextMeshProUGUI _label;
    private bool _running;

    /// <summary>Ignored if a countdown is already running.</summary>
    public static void Run(int countFrom, float stunSeconds, Color stunTint)
    {
        if (_instance == null)
            _instance = Create();

        if (_instance._running) return;

        _instance.StartCoroutine(_instance.Sequence(countFrom, stunSeconds, stunTint));
    }

    private static PowSequence Create()
    {
        var root = new GameObject("PowSequence");
        var sequence = root.AddComponent<PowSequence>();

        var canvasGo = new GameObject("PowCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(root.transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        var textGo = new GameObject("Count", typeof(TextMeshProUGUI));
        textGo.transform.SetParent(canvasGo.transform, false);

        sequence._label = textGo.GetComponent<TextMeshProUGUI>();
        sequence._label.alignment = TextAlignmentOptions.Center;
        sequence._label.fontSize  = 140f;
        sequence._label.fontStyle = FontStyles.Bold;
        sequence._label.color     = new Color(1f, 0.85f, 0.2f, 1f);
        sequence._label.text      = string.Empty;

        RectTransform rt = sequence._label.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.75f);
        rt.sizeDelta = new Vector2(700f, 240f);

        return sequence;
    }

    private IEnumerator Sequence(int countFrom, float stunSeconds, Color stunTint)
    {
        _running = true;

        for (int i = countFrom; i > 0; i--)
        {
            _label.text = i.ToString();

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime;
                _label.transform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(t));
                yield return null;
            }
        }

        _label.text = "POW!";
        _label.transform.localScale = Vector3.one * 1.5f;

        StunGroundedPlayers(stunSeconds, stunTint);

        yield return new WaitForSeconds(0.8f);

        _label.text = string.Empty;
        _running    = false;
    }

    private static void StunGroundedPlayers(float stunSeconds, Color stunTint)
    {
        foreach (PlayerController player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (!player.isOnGame || !player.IsGrounded) continue;

            if (!player.TryGetComponent(out StunState stun))
                stun = player.gameObject.AddComponent<StunState>();

            stun.Activate(stunSeconds, stunTint);
        }
    }
}
