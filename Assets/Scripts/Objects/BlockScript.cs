using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockScript : HoldableItem
{
	[SerializeField] protected List<Animator> animators = new();
	protected List<BlockOverlapCheck> _overlapChecks = new();

	// ── Piece life (GDD, vida de la pieza, nov 2026) ─────────────────────────
	// The life belongs to the whole piece, not to each block: every kick takes
	// one (once per kick, however many of its blocks it touches) and at zero
	// the piece breaks whole and disappears. Bomb, anvil and super kick deal
	// more than any life, so they break it whole in one go.

	[SerializeField, Min(1), Tooltip("Golpes que aguanta la pieza entera. Igual para todos los tipos (GDD: 3).")]
	private int pieceLife = 3;

	[SerializeField, Tooltip("Tinte con la pieza a punto de romperse (un estado por golpe hasta tener sprites de grieta).")]
	private Color damagedColor = new Color(1f, 0.45f, 0.4f, 1f);

	private List<BlockDamageable> _damageables = new();
	private int _aliveChildCount;
	private int _life;

	/// <summary>Hits left before the piece breaks whole.</summary>
	public int Life => _life;

	// ── Push (GDD, empujar, nov 2026) ─────────────────────────────────────────
	// A placed piece is held still sideways (X frozen) until someone pushes it:
	// leaning on it a moment, or kicking it. The push goes from piece to piece
	// by contact, everything resting on top rides along (and stays behind if
	// it hits something), and two pushes in opposite directions cancel out.

	[Header("Empuje y patada")]
	[SerializeField, Min(0f), Tooltip("Velocidad a la que la patada desliza la pieza (escala con la variable Empuje de la progresión).")]
	private float kickSlideSpeed = 5f;

	[SerializeField, Min(0f), Tooltip("Cuánto dura ese deslizamiento. Velocidad x duración = distancia de la patada.")]
	private float kickSlideSeconds = 0.2f;

	private float _rightUntil, _leftUntil;   // fixed time until which a push in each direction holds
	private float _rightSpeed, _leftSpeed;
	private bool  _xFree;

	private static readonly RaycastHit2D[]     CastHits = new RaycastHit2D[16];
	private static readonly HashSet<BlockScript> Visited  = new();
	private static readonly Queue<(BlockScript piece, bool chain)> Frontier = new();
	private const int MaxPushGroup = 32;

	/// <summary>Placed in the world (not in someone’s hand), so it can be pushed.</summary>
	public bool IsPlacedPiece => !holding && rb2d != null && rb2d.bodyType == RigidbodyType2D.Dynamic && gameObject.activeInHierarchy;

	/// <summary>
	/// Pushes <paramref name="first"/> sideways for <paramref name="seconds"/> and with it
	/// every piece it touches in that direction (the chain) and everything
	/// resting on any of them (the riders, which do not push what is in front
	/// of them: if they hit something they stay behind).
	/// </summary>
	public static void PushGroup(BlockScript first, float direction, float speed, float seconds)
	{
		if (first == null || !first.IsPlacedPiece || direction == 0f) return;

		Visited.Clear();
		Frontier.Clear();
		Frontier.Enqueue((first, true));
		Visited.Add(first);

		Vector2 forward = new Vector2(Mathf.Sign(direction), 0f);
		while (Frontier.Count > 0 && Visited.Count <= MaxPushGroup)
		{
			(BlockScript piece, bool chain) = Frontier.Dequeue();
			piece.RequestPush(direction, speed, seconds);

			if (chain) piece.EnqueueTouching(forward, true);
			piece.EnqueueTouching(Vector2.up, false);
		}
	}

	private void EnqueueTouching(Vector2 direction, bool chain)
	{
		var filter = new ContactFilter2D { useTriggers = false };
		filter.NoFilter();
		filter.useTriggers = false;

		int count = rb2d.Cast(direction, filter, CastHits, 0.06f);
		for (int i = 0; i < count; i++)
		{
			BlockScript other = CastHits[i].collider != null ? CastHits[i].collider.GetComponentInParent<BlockScript>() : null;
			if (other == null || other == this || !other.IsPlacedPiece) continue;
			if (Visited.Add(other)) Frontier.Enqueue((other, chain));
		}
	}

	private void RequestPush(float direction, float speed, float seconds)
	{
		float until = Time.fixedTime + Mathf.Max(seconds, Time.fixedDeltaTime);
		if (direction > 0f) { _rightUntil = Mathf.Max(_rightUntil, until); _rightSpeed = speed; }
		else                { _leftUntil  = Mathf.Max(_leftUntil, until);  _leftSpeed  = speed; }
	}

	private void FixedUpdate()
	{
		if (!IsPlacedPiece) return;

		float now   = Time.fixedTime;
		bool  right = now <= _rightUntil;
		bool  left  = now <= _leftUntil;

		// Opposite pushes on the same piece cancel (the "pulseada").
		float velocityX = right && !left ? _rightSpeed : left && !right ? -_leftSpeed : 0f;

		if (velocityX != 0f)
		{
			if (!_xFree) { rb2d.constraints = RigidbodyConstraints2D.FreezeRotation; _xFree = true; }
			// The piece’s high linear damping (what keeps its fall steady) would eat
			// part of this every step: compensate so it moves at the set speed.
			float damping = 1f + Time.fixedDeltaTime * rb2d.linearDamping;
			rb2d.linearVelocity = new Vector2(velocityX * damping, rb2d.linearVelocity.y);
		}
		else if (_xFree || (rb2d.constraints & RigidbodyConstraints2D.FreezePositionX) == 0)
		{
			rb2d.linearVelocity = new Vector2(0f, rb2d.linearVelocity.y);
			rb2d.constraints    = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
			_xFree = false;
		}
	}

	/// <summary>A kick slides the piece (and its chain) a short way: the same rules as pushing (GDD).</summary>
	private bool OnKicked(Vector2 impulse)
	{
		if (!IsPlacedPiece) return false;
		if (impulse.x == 0f) return true;

		PushGroup(this, Mathf.Sign(impulse.x), kickSlideSpeed * ProgressionManager.Get(ProgressionVar.Push), kickSlideSeconds);
		return true;
	}

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
		_life            = pieceLife;

		if (TryGetComponent(out KickResponse kick))
			kick.KickOverride = OnKicked;
	}

	/// <summary>
	/// A hit on the piece, from any of its blocks. Kick = 1; bomb, anvil and
	/// super kick pass a huge amount and break it whole.
	/// </summary>
	public void TakeHit(int amount, PlayerController attacker)
	{
		if (amount <= 0 || _life <= 0 || !gameObject.activeInHierarchy) return;

		// The preview in someone's hand is not in play yet: kicks, headbutts,
		// blasts or the anvil going through it must not wear it down (bug, nov
		// 2026: a piece broken in hand was then placed while inactive).
		if (holding) return;

		_life = amount >= _life ? 0 : _life - amount;

		if (_life <= 0)
		{
			Break();
			return;
		}

		// One visual state per hit taken (tint until Arte delivers cracks).
		float t = 1f - (float)_life / pieceLife;
		Color tint = Color.Lerp(Color.white, damagedColor, t);
		foreach (BlockDamageable block in _damageables)
			if (block != null) block.SetTint(tint);
	}

	/// <summary>Breaks the whole piece at once: every block leaves, then the piece returns to the pool.</summary>
	public void Break()
	{
		_life = 0;
		foreach (BlockDamageable block in _damageables)
			if (block != null) block.RemoveFromPlay();
	}

	protected override void OnDisable()
	{
		base.OnDisable();

		// Reset alive counter so the block is fully usable when retrieved from the pool.
		// Each BlockDamageable resets itself via its own OnDisable.
		_aliveChildCount = _damageables.Count;
		_life            = pieceLife;

		// Back to the pool: free to fall and be held again, no push pending.
		_rightUntil = _leftUntil = 0f;
		_xFree = false;
		if (rb2d != null) rb2d.constraints = RigidbodyConstraints2D.FreezeRotation;

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
		if (!gameObject.activeInHierarchy) return; // never place a piece that is no longer in play

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