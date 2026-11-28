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

    // ── Animator states ───────────────────────────────────────────────────────
    // The new rig (Con_Player, nov 2026) names its states "Anim_X"; the old one
    // used "X". Each state resolves to whichever the controller has.

    private int _stateKick, _statePlace, _stateHitBack, _stateHitFront, _stateIdle, _stateStun;

    // ── Private refs ──────────────────────────────────────────────────────────

    private Animator _animator;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _animator = GetComponent<Animator>();

        _stateKick     = ResolveState("Kick");
        _statePlace    = ResolveState("Place");
        _stateHitBack  = ResolveState("HitBack");
        _stateHitFront = ResolveState("HitFront");
        _stateIdle     = ResolveState("Idle");
        _stateStun     = ResolveState("Stun");
    }

    private int ResolveState(string baseName)
    {
        int newHash = Animator.StringToHash("Anim_" + baseName);
        int oldHash = Animator.StringToHash(baseName);

        if (_animator == null || _animator.runtimeAnimatorController == null) return newHash;
        if (_animator.HasState(0, newHash)) return newHash;
        if (_animator.HasState(0, oldHash)) return oldHash;
        return 0; // the controller has no such state: Play calls are skipped
    }

    private void PlayState(int hash)
    {
        if (hash != 0) _animator.Play(hash);
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
    public void PlayKick() => PlayState(_stateKick);

    /// <summary>Plays the Block placement animation state directly.</summary>
    public void PlayPlace() => PlayState(_statePlace);

    /// <summary>Plays the HitBack animation state (kicked from behind). The clip shows the Hurt face.</summary>
    public void PlayHitBack() => PlayState(_stateHitBack);

    /// <summary>Plays the HitFront animation state (kicked from the front). The clip shows the Hurt face.</summary>
    public void PlayHitFront() => PlayState(_stateHitFront);

    /// <summary>
    /// Keeps the Stun loop on (stars over the head, dizzy face). Stun has no
    /// exit transition, so StunState calls this every frame and ends it with
    /// <see cref="EndStun"/>. A hit while stunned plays out first.
    /// </summary>
    public void KeepStunned()
    {
        if (_stateStun == 0 || _animator.IsInTransition(0)) return;

        int current = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        if (current == _stateStun || current == _stateHitBack || current == _stateHitFront) return;

        PlayState(_stateStun);
    }

    /// <summary>Leaves the Stun loop back to Idle; movement takes it from there.</summary>
    public void EndStun()
    {
        if (_stateStun == 0) return;
        bool inStun = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash == _stateStun
                   || (_animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).shortNameHash == _stateStun);
        if (inStun) PlayState(_stateIdle);
    }

    /// <summary>
    /// Back to Idle from whatever the chicken was doing (stun, die, a kick
    /// mid-air): bones, face and parameters return to their defaults. Called
    /// when the round resets and the chickens are placed again.
    /// </summary>
    public void ResetToIdle()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return;

        // Not Rebind(): it would take the current pose (stun stars included)
        // as the new defaults. Forcing Idle lets write-defaults restore the rest.
        _animator.SetFloat(HashSpeed, 0f);
        _animator.SetFloat(HashVelocityY, 0f);
        _animator.SetBool(HashOnGround, true);
        _animator.SetBool(HashIsGliding, false);
        _animator.SetBool(HashIsTouching, false);
        if (_stateIdle != 0) _animator.Play(_stateIdle, 0, 0f);
        _animator.Update(0f);
    }

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