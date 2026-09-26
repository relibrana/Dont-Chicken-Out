using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runs the POW countdown (item catalog #2): a shared 3-2-1 and a final
/// "JUMP!" at the moment of the blast. The cartel va **centrado en pantalla**
/// (diseño, sep 2026): el POW es un efecto global, así que su aviso no cuelga
/// del objeto — todo el mundo lo ve igual, caiga donde caiga el proyectil.
///
/// Every grounded player — the thrower included — gets stunned; whoever is in
/// the air is safe.
///
/// Builds its own overlay canvas at runtime so no scene wiring is needed
/// (placeholder until Arte entregue los sprites de la cuenta y esto se mueva
/// a UIManager).
/// </summary>
public sealed class PowSequence : MonoBehaviour
{
    private static PowSequence _instance;

    private TextMeshProUGUI _label;
    private bool _running;

    /// <summary>Starts the countdown. Ignored if one is already running.</summary>
    /// <param name="onFinished">Called after the blast, so the POW can despawn.</param>
    public static void Run(
        int countFrom,
        float stunSeconds,
        Color stunTint,
        float labelSize,
        float shakeDuration,
        float shakeAmplitude,
        Action onFinished)
    {
        if (_instance == null)
            _instance = Create();

        if (_instance._running)
        {
            onFinished?.Invoke();
            return;
        }

        _instance._label.fontSize = labelSize;

        _instance.StartCoroutine(_instance.Sequence(
            countFrom, stunSeconds, stunTint, shakeDuration, shakeAmplitude, onFinished));
    }

    private static PowSequence Create()
    {
        var root = new GameObject("PowSequence");
        var sequence = root.AddComponent<PowSequence>();

        var canvasGo = new GameObject("PowCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(root.transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
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
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(900f, 300f);

        return sequence;
    }

    private IEnumerator Sequence(
        int countFrom,
        float stunSeconds,
        Color stunTint,
        float shakeDuration,
        float shakeAmplitude,
        Action onFinished)
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

        _label.text = "JUMP!";
        _label.transform.localScale = Vector3.one * 1.5f;

        // Sacudón corto y fuerte, estilo Mario: el golpe se siente en el
        // instante, no como un temblor largo.
        ScreenShake.Punch(shakeDuration, shakeAmplitude);

        StunGroundedPlayers(stunSeconds, stunTint);

        onFinished?.Invoke();

        yield return new WaitForSeconds(0.8f);

        _label.text = string.Empty;
        _running    = false;
    }

    private static void StunGroundedPlayers(float stunSeconds, Color stunTint)
    {
        foreach (PlayerController player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (!player.isOnGame) continue;

            if (!player.IsGrounded)
            {
                // Esquivó saltando: sólo un toque de vibración para que note
                // que la onda pasó por debajo.
                player.Rumble(0.15f, 0.15f, 0.12f);
                continue;
            }

            if (!player.TryGetComponent(out StunState stun))
                stun = player.gameObject.AddComponent<StunState>();

            stun.Activate(stunSeconds, stunTint);

            // Le pegó de lleno: plumas como cuando lo patean, y vibración
            // fuerte durante el aturdimiento.
            FeatherVFXController.Instance?.EmitKickReceived(player.transform.position, Vector2.up);
            player.Rumble(0.85f, 0.7f, Mathf.Min(stunSeconds, 0.45f));
        }
    }
}
