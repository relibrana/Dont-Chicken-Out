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

	protected virtual void Awake()
	{
		foreach (var animator in animators)
			_overlapChecks.Add(animator.GetComponent<BlockOverlapCheck>());
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		
		foreach (Animator anim in animators)
		{
			anim.enabled = false;
		}
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