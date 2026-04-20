using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Owns all Animator interaction for the player.
/// Receives state updates from PlayerMovement and action calls from PlayerController.
/// Animation events (e.g. KickCollider on/off) are dispatched via animationEvents list,
/// keeping hitbox sync tied to specific animation frames.
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class PlayerAnimController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Tooltip("Indexed animation events fired from Animation clips via playAnimEvent(int id).")]
    [SerializeField] private List<UnityEvent> animationEvents = new();

    // ── Animator parameter hashes ─────────────────────────────────────────────

    private static readonly int HashOnGround    = Animator.StringToHash("OnGround");
    private static readonly int HashSpeed       = Animator.StringToHash("Speed");
    private static readonly int HashVelocityY   = Animator.StringToHash("velocityY");
    private static readonly int HashIsGliding   = Animator.StringToHash("IsGliding");
    private static readonly int HashIsTouching  = Animator.StringToHash("IsTouchingBlock");

    // ── Private refs ──────────────────────────────────────────────────────────

    private Animator _animator;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    // ── Movement state setters (called by PlayerMovement) ─────────────────────

    /// <summary>Updates the OnGround animator bool.</summary>
    public void SetGrounded(bool isGrounded) =>
        _animator.SetBool(HashOnGround, isGrounded);

    /// <summary>Updates the horizontal Speed animator float.</summary>
    public void SetSpeed(float speed) =>
        _animator.SetFloat(HashSpeed, speed);

    /// <summary>Updates the vertical velocityY animator float.</summary>
    public void SetVelocityY(float velocityY) =>
        _animator.SetFloat(HashVelocityY, velocityY);

    /// <summary>Updates the IsGliding animator bool.</summary>
    public void SetGliding(bool isGliding) =>
        _animator.SetBool(HashIsGliding, isGliding);

    /// <summary>Updates the IsTouchingBlock animator bool (used for push animation).</summary>
    public void SetTouchingBlock(bool isTouching) =>
        _animator.SetBool(HashIsTouching, isTouching);

    // ── Action triggers (called by PlayerController) ──────────────────────────

    /// <summary>Plays the Kick animation state directly.</summary>
    public void PlayKick() =>
        _animator.Play("Kick");

    /// <summary>Plays the Block placement animation state directly.</summary>
    public void PlayPlace() =>
        _animator.Play("Place");

    /// <summary>Plays the HitBack animation state (kicked from behind).</summary>
    public void PlayHitBack() =>
        _animator.Play("HitBack");

    /// <summary>Plays the HitFront animation state (kicked from the front).</summary>
    public void PlayHitFront() =>
        _animator.Play("HitFront");

    // ── Animation event receivers (called by Animation clips) ─────────────────

    /// <summary>
    /// Called by Animation clips via an Animation Event.
    /// Routes to the corresponding UnityEvent in the animationEvents list.
    /// Used to toggle KickCollider on/off in sync with specific frames.
    /// </summary>
    public void PlayAnimEvent(int id)
    {
        if (id < 0 || id >= animationEvents.Count)
        {
            Debug.LogWarning($"[PlayerAnimController] Animation event id {id} is out of range.");
            return;
        }

        animationEvents[id].Invoke();
    }

    /// <summary>
    /// Called by Animation clips via an Animation Event to play a sound by id.
    /// </summary>
    public void PlaySFX(string sfxName) =>
        AudioManager.Instance.PlaySound(sfxName);
    
    public void PlayBGM(string bgmName) =>
        AudioManager.Instance.PlayMusic(bgmName);
}