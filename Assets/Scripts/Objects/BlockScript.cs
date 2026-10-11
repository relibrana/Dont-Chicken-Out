using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockScript : HoldableItem
{
	[SerializeField] protected List<Animator> animators = new();
	protected List<BlockOverlapCheck> _overlapChecks = new();


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

	/// <summary>
	/// Swaps every sub-block to the owner's block sprite (paja con plumas de su color).
	/// Null keeps the prefab's sprite. Pooled blocks are shared between players,
	/// so this runs every time a player takes one from the pool.
	/// </summary>
	public void SetBlockSprite(Sprite sprite)
	{
		if (sprite == null) return;

		foreach (SpriteRenderer rend in spriteRenderers)
		{
			rend.sprite = sprite;
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

			// Sólo dice qué celda de la pieza es (0, 1, 2…); la escala que suena
			// la decide Audio en FMOD con ese dato.
			SimEvents.Raise(SimEvent.BlockPlaced, animator.transform.position, this, noteIndex);
			noteIndex++;

			yield return new WaitForSeconds(waitTime);
		}

		foreach (var overlapCheck in _overlapChecks)
			overlapCheck.OverlapCheck(overlapCheck.DisableBlock);

		// Performance (oct 2026): after "appear" the controller sits forever in
		// an empty idle loop, and an enabled Animator is evaluated every frame
		// anyway — 4 per piece, so a tall tower spent a big slice of the frame
		// animating nothing. Switch them off once the last one has settled; a
		// disabled Animator leaves the sprites exactly as they were.
		yield return new WaitForSeconds(AppearSettleSeconds);

		foreach (Animator animator in animators)
			animator.enabled = false;
	}

	// block_orange_appear lasts 0.42 s, plus room for the transition into idle.
	private const float AppearSettleSeconds = 0.6f;
}