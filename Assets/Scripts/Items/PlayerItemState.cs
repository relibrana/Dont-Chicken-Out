using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base for timed item states living on the player (súper patada, stun, papa
/// caliente...). Owns the countdown, the placeholder tint, and cleanup: a state
/// never survives the owner's death or the end of a round (isOnGame drops) nor
/// the component being disabled. Subclasses hook OnStateStarted / OnTick /
/// OnStateExpired / OnStateEnded and must undo in OnStateEnded everything they
/// set in OnStateStarted.
/// NOTE: no DisallowMultipleComponent here — Unity applies it to subtypes, and
/// a player must be able to hold several different states at once.
/// </summary>
public abstract class PlayerItemState : MonoBehaviour
{
    public bool IsActive { get; private set; }

    protected PlayerController Controller { get; private set; }
    protected PlayerMovement   Movement   { get; private set; }

    /// <summary>Seconds left before the state expires.</summary>
    protected float Remaining;

    private Color _tint = Color.white;
    private bool  _tintVisible;

    private readonly List<SpriteRenderer> _renderers      = new();
    private readonly List<Color>          _originalColors = new();

    /// <summary>Starts (or refreshes) the state and applies the placeholder tint.</summary>
    protected void BeginState(float duration, Color tint)
    {
        if (Controller == null)
        {
            Controller = GetComponent<PlayerController>();
            Movement   = GetComponent<PlayerMovement>();
        }

        bool wasActive = IsActive;
        if (wasActive)
        {
            SetTintVisible(false);
            OnStateEnded();
        }

        IsActive  = true;
        Remaining = duration;
        _tint     = tint;

        CacheRenderers();
        SetTintVisible(true);
        OnStateStarted();
    }

    public void EndState()
    {
        if (!IsActive) return;

        IsActive = false;
        SetTintVisible(false);
        OnStateEnded();
    }

    private void Update()
    {
        if (!IsActive) return;

        if (Controller != null && !Controller.isOnGame)
        {
            EndState();
            return;
        }

        Remaining -= Time.deltaTime;

        if (Remaining <= 0f)
        {
            OnStateExpired();
            return;
        }

        OnTick();
    }

    private void OnDisable() => EndState();

    /// <summary>Timer reached zero. Default just ends the state; override to add an on-expiry effect.</summary>
    protected virtual void OnStateExpired() => EndState();

    protected virtual void OnStateStarted() { }
    protected virtual void OnTick() { }
    protected virtual void OnStateEnded() { }

    // ── Placeholder tint ──────────────────────────────────────────────────────

    protected void SetTintVisible(bool visible)
    {
        _tintVisible = visible;

        for (int i = 0; i < _renderers.Count; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].color = visible ? _tint : _originalColors[i];
        }
    }

    /// <summary>Changes the tint color while active (pulses). Reapplies if visible.</summary>
    protected void SetTintColor(Color tint)
    {
        _tint = tint;
        if (_tintVisible) SetTintVisible(true);
    }

    private void CacheRenderers()
    {
        _renderers.Clear();
        _originalColors.Clear();

        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
        {
            _renderers.Add(sr);
            _originalColors.Add(sr.color);
        }
    }
}
