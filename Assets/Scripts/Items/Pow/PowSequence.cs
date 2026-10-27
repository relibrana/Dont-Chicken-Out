using System;
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
    // Seconds each number stays on screen, and the "JUMP!" after the blast.
    private const float StepSeconds     = 1f;
    private const float JumpHoldSeconds = 0.8f;

    private static PowSequence _instance;

    private Image _image;

    // Simulation state, advanced by the tick only (same split as PlayerItemState):
    // the blast lands on the same tick on every machine. Update only draws.
    private bool  _running;
    private bool  _blasted;
    private int   _number;
    private float _stepRemaining;

    private Sprite[] _numberSprites;
    private Sprite   _jumpSprite;
    private float    _displayScale;
    private float    _stunSeconds;
    private Color    _stunTint;
    private float    _shakeDuration;
    private float    _shakeAmplitude;
    private Action   _onFinished;

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

        _instance.Begin(
            countFrom, numberSprites, jumpSprite, displayScale,
            stunSeconds, stunTint, shakeDuration, shakeAmplitude, onFinished);
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

        Finish();
        _image.transform.localScale = Vector3.one;

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

    private void Begin(
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
        _numberSprites  = numberSprites;
        _jumpSprite     = jumpSprite;
        _displayScale   = displayScale;
        _stunSeconds    = stunSeconds;
        _stunTint       = stunTint;
        _shakeDuration  = shakeDuration;
        _shakeAmplitude = shakeAmplitude;
        _onFinished     = onFinished;

        _running       = true;
        _blasted       = false;
        _number        = countFrom;
        _stepRemaining = 0f;

        // One FMOD event covers the whole 3-2-1-JUMP! (design, oct 2026). Global
        // effect, so no position to pan from.
        SimEvents.Raise(SimEvent.PowCountdownStarted, Vector3.zero, this);

        if (_number > 0)
            ShowNumber();
        else
            Blast();
    }

    private void FixedUpdate()
    {
        if (!_running) return;

        _stepRemaining -= Time.fixedDeltaTime;
        if (_stepRemaining > 0f) return;

        if (_blasted)
        {
            Finish();
            return;
        }

        _number--;
        if (_number > 0)
            ShowNumber();
        else
            Blast();
    }

    // Each number shrinks from 1.5x to 1x over its step. Drawn from the last
    // tick's value minus the time since that tick; never decides anything.
    private void Update()
    {
        if (!_running || _blasted) return;

        float renderRemaining = Mathf.Max(0f, _stepRemaining - (Time.time - Time.fixedTime));
        float t = 1f - Mathf.Clamp01(renderRemaining / StepSeconds);
        _image.transform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, t);
    }

    private void ShowNumber()
    {
        Sprite number = _numberSprites != null && _number - 1 < _numberSprites.Length ? _numberSprites[_number - 1] : null;
        Show(number, _displayScale);
        _stepRemaining += StepSeconds;
    }

    private void Blast()
    {
        _blasted = true;

        Show(_jumpSprite, _displayScale);
        _image.transform.localScale = Vector3.one * 1.5f;

        // Sacudón corto y fuerte, estilo Mario: el golpe se siente en el
        // instante, no como un temblor largo.
        ScreenShake.Punch(_shakeDuration, _shakeAmplitude);

        StunGroundedPlayers(_stunSeconds, _stunTint);

        Action onFinished = _onFinished;
        _onFinished = null;
        onFinished?.Invoke();

        _stepRemaining += JumpHoldSeconds;
    }

    private void Finish()
    {
        _image.enabled = false;
        _running       = false;
        _onFinished    = null;
        _numberSprites = null;
        _jumpSprite    = null;
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
