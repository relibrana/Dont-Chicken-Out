using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
	[SerializeField] private PlatformerValuesSO valuesSO;

	//Components and State Values
	private Rigidbody2D rb;
	private Vector2 currentVelocity;
	private float velocitySmoothing;
	[SerializeField] private KickCollider kickCollider;
	[SerializeField] private Animator animator;
	[SerializeField] private SpriteRenderer sprite;

	//Ground Checkers
	[SerializeField] private float raycastDistance = 0.55f;
	[SerializeField] private float checkSpacing = 0.2f;
	[SerializeField] private float spacingY = 0.2f;
	[SerializeField] private float headCheck = 0.2f;
	[SerializeField] private LayerMask groundLayer;
	private bool isPlayerOnHead;

	//Timers
	private float coyoteTimeTimer;
	private float jumpBufferTimer;

	//Internal Calculations
	private float calculatedGravity;
	private float calculatedInitialJumpVelocity;

	//Input Values
	private InputAction moveInput;
	private float moveDirection;
	private InputAction jumpInput;
	private bool isGrounded;
	private bool isHoldingJump;
	private InputAction placeBlockInput;
	private InputAction kickInput;
	private InputAction cluckInput;
	public int playerIndex;
	public bool isOnGame = false;
	public int roundsWon;

	//InputSystem
	private PlayerInput playerInput;
	private string assignedScheme;

	//Game Manager
	public Action<PlayerController> onDeath;
	public Action<PlayerController> onPlayerReady;
	public Vector2 startPosition;
	//Blocks
	[SerializeField] private Transform blockPosition;
	public BlockScript currentBlockHolding = null;
	[SerializeField] private float blockPlaceCooldown = 1f;
	[SerializeField] private bool canPlaceBlock = false;
	public bool isBlockLogicAvailable = true;
	private bool isBlockOverlapping = false;
	private Material hayMaterial;


	private void Awake()
	{
		playerInput = GetComponent<PlayerInput>();
		InitializeInputActions();

		rb = GetComponent<Rigidbody2D>();

		rb.gravityScale = 0f;
		rb.freezeRotation = true;
		CalculateValues();

		kickCollider.forceDirection = valuesSO.kickForce;
	}

	public void CalculateValues()
	{
		calculatedGravity = 2 * valuesSO.peakHeight / -(valuesSO.timeToPeak * valuesSO.timeToPeak);

		calculatedInitialJumpVelocity = -calculatedGravity * valuesSO.timeToPeak;
	}

	private void InitializeInputActions()
	{
		moveInput = playerInput.actions.FindAction("Move");
		moveInput.Enable();

		jumpInput = playerInput.actions.FindAction("Jump");
		jumpInput.started += OnJumpStarted;
		jumpInput.canceled += OnJumpCanceled;
		jumpInput.Enable();

		placeBlockInput = playerInput.actions.FindAction("PlaceBlock");
		placeBlockInput.started += OnPlaceBlockStarted;

		kickInput = playerInput.actions.FindAction("Kick");
		kickInput.started += OnKickStarted;

		cluckInput = playerInput.actions.FindAction("Cluck");
		cluckInput.started += OnCluckStarted;
	}

	void OnDestroy()
	{
		if (jumpInput != null)
		{
			jumpInput.started -= OnJumpStarted;
			jumpInput.canceled -= OnJumpCanceled;
		}
	}

	private void OnJumpStarted(InputAction.CallbackContext context)
	{
		jumpBufferTimer = valuesSO.jumpBufferTime;
		isHoldingJump = true;
	}

	private void OnJumpCanceled(InputAction.CallbackContext context)
	{
		isHoldingJump = false;
	}

	private void OnPlaceBlockStarted(InputAction.CallbackContext context)
	{
		if (isGrounded && canPlaceBlock && !isBlockOverlapping)
		{
			currentBlockHolding.StopHold();
			currentBlockHolding.AnimateAppear();
			SoundManager.instance.PlaySound("placeBlock");
			currentBlockHolding = null;
			canPlaceBlock = false;
			isBlockLogicAvailable = false;
			DOVirtual.DelayedCall(blockPlaceCooldown, () => isBlockLogicAvailable = true, false);
		}
	}

	private void OnKickStarted(InputAction.CallbackContext context)
	{
		kickCollider.gameObject.SetActive(true);
		DOVirtual.DelayedCall(0.2f, () => kickCollider.gameObject.SetActive(false));
	}

	private void OnCluckStarted(InputAction.CallbackContext context)
	{
		//!CLUCK SOUND
		if (GameManager.instance.gameState == GameState.Menu)
			onPlayerReady?.Invoke(this);
	}

	void Update()
	{
		UpdateHorizontalDirection();

		UpdateJumpValues();

		CheckGround();

		HandleJump();

		HandleBlockLogic();
	}

	private void UpdateHorizontalDirection() => moveDirection = moveInput.ReadValue<float>();

	private void UpdateJumpValues()
	{
		if (jumpBufferTimer > 0)
		{
			jumpBufferTimer -= Time.deltaTime;
		}

		if (isGrounded)
		{
			coyoteTimeTimer = valuesSO.coyoteTime;
		}
		else
		{
			coyoteTimeTimer -= Time.deltaTime;
		}
	}

	private void CheckGround()
	{
		Vector2 pos = transform.position;
		RaycastHit2D hit = Physics2D.Raycast(new Vector2(pos.x - checkSpacing, pos.y - spacingY), Vector2.down, raycastDistance, groundLayer);
		RaycastHit2D hit2 = Physics2D.Raycast(new Vector2(pos.x, pos.y - spacingY - 0.07f), Vector2.down, raycastDistance, groundLayer);
		RaycastHit2D hit3 = Physics2D.Raycast(new Vector2(pos.x + checkSpacing, pos.y - spacingY), Vector2.down, raycastDistance, groundLayer);

		isGrounded = hit.collider || hit2.collider || hit3.collider;

		animator.SetBool("OnGround", isGrounded);

		RaycastHit2D hitH = Physics2D.Raycast(new Vector2(pos.x - checkSpacing, pos.y + headCheck), Vector2.up, raycastDistance, groundLayer);
		RaycastHit2D hitH2 = Physics2D.Raycast(new Vector2(pos.x, pos.y + headCheck + 0.07f), Vector2.up, raycastDistance, groundLayer);
		RaycastHit2D hitH3 = Physics2D.Raycast(new Vector2(pos.x + checkSpacing, pos.y + headCheck), Vector2.up, raycastDistance, groundLayer);

		isPlayerOnHead = ColliderIsPlayer(hitH.collider) || ColliderIsPlayer(hitH2.collider) || ColliderIsPlayer(hitH3.collider);
	}

	private bool ColliderIsPlayer(Collider2D collider)
	{
		if (!collider)
			return false;
		return collider.gameObject.CompareTag("Player");
	}

	void OnDrawGizmos()
	{
		Gizmos.color = Color.yellow;

		float startPosY = transform.position.y - spacingY;
		Gizmos.DrawLine(new Vector2(transform.position.x, startPosY - 0.07f), new Vector2(transform.position.x, startPosY - raycastDistance));
		Gizmos.DrawLine(new Vector2(transform.position.x, startPosY + headCheck + 0.07f),
			new Vector2(transform.position.x, startPosY + headCheck + raycastDistance));

		float gizmos1 = transform.position.x - checkSpacing;
		Gizmos.DrawLine(new Vector2(gizmos1, startPosY), new Vector2(gizmos1, startPosY - raycastDistance));
		Gizmos.DrawLine(new Vector2(gizmos1, startPosY + headCheck), new Vector2(gizmos1, startPosY + headCheck + raycastDistance));

		float gizmos2 = transform.position.x + checkSpacing;
		Gizmos.DrawLine(new Vector2(gizmos2, startPosY), new Vector2(gizmos2, startPosY - raycastDistance));
		Gizmos.DrawLine(new Vector2(gizmos2, startPosY + headCheck), new Vector2(gizmos2, startPosY + headCheck + raycastDistance));
	}

	private void HandleJump()
	{
		bool canJump = jumpBufferTimer > 0f && coyoteTimeTimer > 0f && !isPlayerOnHead;

		if (canJump)
		{
			currentVelocity.y = calculatedInitialJumpVelocity;
			SoundManager.instance.PlaySound("jump");

			isGrounded = false;

			jumpBufferTimer = 0f;
			coyoteTimeTimer = 0f;
		}
	}

	void FixedUpdate()
	{
		HandleHorizontalMovement();
		HandleGravity();

		rb.linearVelocity = currentVelocity;
	}

	private void HandleHorizontalMovement()
	{
		float targetSpeed = moveDirection * valuesSO.maxSpeed;

		if(moveDirection != 0)
			transform.localScale = new Vector3(Mathf.Sign(moveDirection), 1);

		float smoothTime = Mathf.Abs(targetSpeed) > 0.01f ? valuesSO.accelerationTime : valuesSO.decelerationTime;

		currentVelocity.x = Mathf.SmoothDamp(
			currentVelocity.x,
			targetSpeed,
			ref velocitySmoothing,
			smoothTime
		);

		animator.SetFloat("Speed", Mathf.Abs(currentVelocity.x));
	}

	public void AddImpulse(Vector2 impulseDirection)
    {
		currentVelocity += impulseDirection;

		isGrounded = false;
		coyoteTimeTimer = 0f;
    }

	public void SetMaterials(PlayerMaterial mats)
    {
		sprite.material = mats.playerMat;
		hayMaterial = mats.hayMat;
    }
	private void HandleGravity()
	{
		if (isGrounded)
		{
			if (currentVelocity.y <= 0)
				currentVelocity.y = -0.1f;

			return;
		}

		currentVelocity.y += calculatedGravity * Time.deltaTime;

		if (currentVelocity.y < 0)
		{
			float glideMultiplier = isHoldingJump ? valuesSO.glideResistance : 0;

			animator.SetBool("IsGliding", glideMultiplier != 0);

			int fallLimit = isHoldingJump ? -4 : -25;

			float nextVelocityY = currentVelocity.y + calculatedGravity * (valuesSO.fallMultiplier - 1f - glideMultiplier) * Time.deltaTime;

			currentVelocity.y = Mathf.Clamp(nextVelocityY, fallLimit, 50);
		}
		else if (currentVelocity.y > 0 && !isHoldingJump)
		{
			currentVelocity.y += calculatedGravity * (valuesSO.lowJumpMultiplier - 1f) * Time.deltaTime;
		}

		animator.SetFloat("velocityY", currentVelocity.y);
	}

	public void OnDeath()
	{
		onDeath?.Invoke(this);
	}

	public void OnAssignedScheme(string schemeName)
	{
		assignedScheme = schemeName;

		playerInput.SwitchCurrentControlScheme(assignedScheme, playerInput.devices.ToArray());

		playerInput.SwitchCurrentActionMap("Player");
	}

	public void OnPlayerLeft(PlayerInput playerInput)
	{
		if (assignedScheme != "Gamepad")
		{
			GameManager.instance.FreeKeyboardScheme(assignedScheme);
		}
	}

	private void HandleBlockLogic()
	{
		if (!isOnGame || !isBlockLogicAvailable) return;

		if (currentBlockHolding == null)
		{
			int randomIndex = UnityEngine.Random.Range(0, GameManager.instance.blocksPool.pooledObjects.Count);
			currentBlockHolding = GameManager.instance.blocksPool.GetPooledObject(randomIndex, blockPosition.position, 0).GetComponent<BlockScript>();
			currentBlockHolding.SetMaterial(hayMaterial);
			currentBlockHolding.StartHold();
			DOVirtual.DelayedCall(0.3f, () => canPlaceBlock = true, false);
		}
		else
		{
			currentBlockHolding.transform.position = blockPosition.position;
			currentBlockHolding.transform.localScale = new Vector3(transform.lossyScale.x, 1, 1);

			isBlockOverlapping = false;
			foreach (BoxCollider2D col in currentBlockHolding.boxCollider2Ds)
			{
				if (CheckOverlapping(col))
					isBlockOverlapping = true;
			}
			currentBlockHolding.overlapping = isBlockOverlapping;
		}
	}
	
	private bool CheckOverlapping(BoxCollider2D collider)
	{
		Vector2 centerCollider = collider.bounds.center;
		Vector2 colliderSize = new Vector2((collider.size.x - 0.1f) * transform.lossyScale.x, (collider.size.y - 0.1f) * transform.lossyScale.y);

		Collider2D[] overlappedColliders = Physics2D.OverlapBoxAll(centerCollider, colliderSize, 0f);


		foreach (var col in overlappedColliders)
		{
			if (col != collider)
				return true;
		}

		return false;
	}
}
