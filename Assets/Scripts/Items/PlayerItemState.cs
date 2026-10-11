using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base for timed item states living on the player (súper patada, stun, papa
/// caliente...). Owns the countdown, the placeholder tint, and cleanup: a state
/// never survives the owner's death nor the round reset (GameManager calls
/// EndAll in both), the owner leaving the game (isOnGame drops) nor the
/// component being disabled. On death the hot potato detonates first.
/// Several states may tint at once; the colour always returns to the real
/// original once the last one ends. Subclasses hook OnStateStarted / OnTick /
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
        Controller.ShowStateLabel(Label);
        OnStateStarted();
    }

    public void EndState()
    {
        if (!IsActive) return;

        IsActive = false;
        SetTintVisible(false);
        if (Controller != null) Controller.HideStateLabel(Label);
        OnStateEnded();
    }

    /// <summary>
    /// Name shown over the chicken's head while the state lasts (placeholder
    /// until it gets its own art). Null = no label, e.g. states that already
    /// show themselves (hot potato) or that are not a power-up (stun, moco).
    /// </summary>
    protected virtual string Label => null;

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

    /// <summary>
    /// Ends every item state on that player right now. Called by the round
    /// reset so cleanup does not depend on Update noticing isOnGame dropped.
    /// </summary>
    public static void EndAll(GameObject owner)
    {
        foreach (PlayerItemState state in owner.GetComponents<PlayerItemState>())
            state.EndState();
    }

    /// <summary>Timer reached zero. Default just ends the state; override to add an on-expiry effect.</summary>
    protected virtual void OnStateExpired() => EndState();

    protected virtual void OnStateStarted() { }
    protected virtual void OnTick() { }
    protected virtual void OnStateEnded() { }

    // ── Placeholder tint ──────────────────────────────────────────────────────

    /// <summary>
    /// False for states that already show themselves with their own art (the
    /// hot potato over the head). Item states are moving from tints to visible
    /// items/abilities (design, oct 2026); this lets each one switch over.
    /// </summary>
    protected virtual bool UsesTint => true;

    protected void SetTintVisible(bool visible)
    {
        if (!UsesTint)
        {
            _tintVisible = false;
            return;
        }

        _tintVisible = visible;

        for (int i = 0; i < _renderers.Count; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].color = visible ? _tint : _originalColors[i];
        }

        // Several states can tint the same chicken. When this one lets go, the
        // colour goes back to whichever other state is still showing its tint.
        if (!visible)
        {
            PlayerItemState stillTinting = FindOtherTintingState();
            if (stillTinting != null) stillTinting.SetTintVisible(true);
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
        // If another state is already tinting the chicken, the renderers are
        // showing ITS tint: the real originals are the ones it cached. Copying
        // sr.color here would store a tint as "original" and leave the chicken
        // coloured forever once both states end.
        PlayerItemState other = FindOtherTintingState();

        _renderers.Clear();
        _originalColors.Clear();

        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
        {
            _renderers.Add(sr);
            _originalColors.Add(other != null && other.TryGetOriginalColor(sr, out Color original)
                ? original
                : sr.color);
        }
    }

    private PlayerItemState FindOtherTintingState()
    {
        foreach (PlayerItemState state in GetComponents<PlayerItemState>())
        {
            if (state != this && state.IsActive && state._tintVisible)
                return state;
        }
        return null;
    }

    private bool TryGetOriginalColor(SpriteRenderer sr, out Color original)
    {
        int index = _renderers.IndexOf(sr);
        original = index >= 0 ? _originalColors[index] : default;
        return index >= 0;
    }
}
