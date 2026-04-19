using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to any UI Button to automatically play hover and click sounds.
/// Handles both mouse (IPointerEnterHandler) and gamepad/keyboard navigation (ISelectHandler).
/// Uses a focus flag to prevent double-firing when both input methods are active simultaneously.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIButtonSFX : MonoBehaviour,
    IPointerEnterHandler,
    ISelectHandler,
    IDeselectHandler,
    IPointerClickHandler,
    ISubmitHandler
{
    // Prevents hover sound from firing twice when mouse and gamepad
    // both target the same button at the same time.
    private bool _hasFocus;

    // ── IPointerEnterHandler ──────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_hasFocus) return;

        _hasFocus = true;
        AudioManager.Instance.MakeButtonHoverSound();
    }

    // ── ISelectHandler ────────────────────────────────────────────────────────

    public void OnSelect(BaseEventData eventData)
    {
        if (_hasFocus) return;

        _hasFocus = true;
        AudioManager.Instance.MakeButtonHoverSound();
    }

    // ── IDeselectHandler ─────────────────────────────────────────────────────

    public void OnDeselect(BaseEventData eventData)
    {
        _hasFocus = false;
    }

    // ── IPointerClickHandler ──────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        AudioManager.Instance.MakeButtonSelectedSound();
    }

    // ── ISubmitHandler ────────────────────────────────────────────────────────
    
    /// <summary>
    /// Fired when the player presses Confirm/A on gamepad or Enter on keyboard.
    /// Equivalent to OnPointerClick for non-mouse input.
    /// </summary>
    public void OnSubmit(BaseEventData eventData)
    {
        AudioManager.Instance.MakeButtonSelectedSound();
    }
}