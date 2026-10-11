using System;
using System.Collections;
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
/// The numbers and the "JUMP!" are Arte's sprites (Item-PowNum, Item-PowAviso,
/// oct 2026), shown at their native size times <c>displayScale</c>. Builds its
/// own overlay canvas at runtime so no scene wiring is needed.
/// </summary>
public sealed class PowSequence : MonoBehaviour
{
    private static PowSequence _instance;

    private Image _image;
    private bool  _running;

    /// <summary>Starts the countdown. Ignored if one is already running.</summary>
    /// <param name="numberSprites">[0] = "1", [1] = "2", [2] = "3"…</param>
    /// <param name="onFinished">Called after the blast, so the POW can despawn.</param>
    public static void Run(
        int countFrom,
        Sprite[] numberSprites,
        Sprite jumpSprite,
        float displayScale,
        float stunSeconds,
        Color stunTint,
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

        _instance.StartCoroutine(_instance.Sequence(
            countFrom, numberSprites, jumpSprite, displayScale,
            stunSeconds, stunTint, shakeDuration, shakeAmplitude, onFinished));
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

        var imageGo = new GameObject("Count", typeof(Image));
        imageGo.transform.SetParent(canvasGo.transform, false);

        sequence._image = imageGo.GetComponent<Image>();
        sequence._image.raycastTarget  = false;
        sequence._image.preserveAspect = true;
        sequence._image.enabled        = false;

        RectTransform rt = sequence._image.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        GameManager.RoundEnded += sequence.Cancel;
        return sequence;
    }

    private void OnDestroy() => GameManager.RoundEnded -= Cancel;

    /// <summary>
    /// The round ended mid-countdown: no blast, no stun, nothing carried into
    /// the next round (bug, oct 2026: the countdown showed up again at its start).
    /// </summary>
    private void Cancel()
    {
        if (!_running) return;

        StopAllCoroutines();
        _image.enabled = false;
        _image.transform.localScale = Vector3.one;
        _running = false;

        // Ending event: never dropped by a resimulation, so the sound always stops.
        SimEvents.Raise(SimEvent.PowCancelled, Vector3.zero, this);
    }

    private void Show(Sprite sprite, float displayScale)
    {
        _image.enabled = sprite != null;
        if (sprite == null) return;

        _image.sprite = sprite;
        _image.SetNativeSize();
        _image.rectTransform.sizeDelta *= displayScale;
    }

    private IEnumerator Sequence(
        int countFrom,
        Sprite[] numberSprites,
        Sprite jumpSprite,
        float displayScale,
        float stunSeconds,
        Color stunTint,
        float shakeDuration,
        float shakeAmplitude,
        Action onFinished)
    {
        _running = true;

        // One FMOD event covers the whole 3-2-1-JUMP! (design, oct 2026). Global
        // effect, so no position to pan from.
        SimEvents.Raise(SimEvent.PowCountdownStarted, Vector3.zero, this);

        for (int i = countFrom; i > 0; i--)
        {
            Sprite number = numberSprites != null && i - 1 < numberSprites.Length ? numberSprites[i - 1] : null;
            Show(number, displayScale);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime;
                _image.transform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(t));
                yield return null;
            }
        }

        Show(jumpSprite, displayScale);
        _image.transform.localScale = Vector3.one * 1.5f;

        // Sacudón corto y fuerte, estilo Mario: el golpe se siente en el
        // instante, no como un temblor largo.
        ScreenShake.Punch(shakeDuration, shakeAmplitude);

        StunGroundedPlayers(stunSeconds, stunTint);

        onFinished?.Invoke();

        yield return new WaitForSeconds(0.8f);

        _image.enabled = false;
        _running       = false;
    }

    private static void StunGroundedPlayers(float stunSeconds, Color stunTint)
    {
        foreach (PlayerController player in PlayerController.Active)
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
