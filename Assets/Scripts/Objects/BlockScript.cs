using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockScript : HoldableItem
{
	[SerializeField] protected List<Animator> animators = new();
	protected List<BlockOverlapCheck> _overlapChecks = new();

	[Header("Placement SFX")]
	[SerializeField, Range(0.5f, 2f)] private float basePitch  = 1f;
	[SerializeField, Range(0f,  0.5f)] private float pitchStep = 0.08f;
	[SerializeField, Range(0.5f, 2f)] private float maxPitch   = 1.5f;

	private List<BlockDamageable> _damageables = new();
	private int _aliveChildCount;

	protected virtual void Awake()
	{
		foreach (var animator in animators)
		{
			_overlapChecks.Add(animator.GetComponent<BlockOverlapCheck>());

			var damageable = animator.GetComponent<BlockDamageable>();
			if (damageable != null)
				_damageables.Add(damageable);
		}

		_aliveChildCount = _damageables.Count;
	}

	protected override void OnDisable()
	{
		base.OnDisable();

		// Reset alive counter so the block is fully usable when retrieved from the pool.
		// Each BlockDamageable resets itself via its own OnDisable.
		_aliveChildCount = _damageables.Count;

		foreach (Animator anim in animators)
		{
			anim.enabled = false;
		}
	}

	/// <summary>
	/// Called by a child <see cref="BlockDamageable"/> when it reaches 0 HP.
	/// When all children are dead the block deactivates itself and returns to the pool.
	/// </summary>
	public void OnChildBlockDied()
	{
		_aliveChildCount--;

		if (_aliveChildCount <= 0)
			gameObject.SetActive(false);
	}

	// Placement sound is handled per sub-block inside AnimateAppearRoutine.
	protected override void OnPlaceSfx() { }

    public override void AnimateAppear()
	{
		Color alphaZero =  new Color (1f, 1f, 1f, 0f);
		foreach (SpriteRenderer rend in spriteRenderers)
		{
			rend.color = alphaZero;
		}
		StartCoroutine(AnimateAppearRoutine(0.1f));
	}
	protected IEnumerator AnimateAppearRoutine(float waitTime)
	{
		int noteIndex = 0;
		for (int i = 0; i < animators.Count; i++)
		{
			var overlapCheck = _overlapChecks[i];
			if (overlapCheck.OverlapCheck(overlapCheck.DisableBlock)) 
				continue;
			
			var animator = animators[i];
			animator.enabled = true;
			animator.SetTrigger("appear");

			float pitch = Mathf.Min(basePitch + noteIndex * pitchStep, maxPitch);
			AudioManager.Instance.PlaySound("block_placement", pitch);
			noteIndex++;

			yield return new WaitForSeconds(waitTime);
		}

		foreach (var overlapCheck in _overlapChecks)
			overlapCheck.OverlapCheck(overlapCheck.DisableBlock);
	}
}