using System.Collections.Generic;
using UnityEngine;

public class HoldableItem : MonoBehaviour
{
    protected bool holding;
	[SerializeField] Color holdColor = new Color (1f, 1f, 1f, 0.25f);
	[SerializeField] Color overlappingColor = new Color (1f, 0, 0, 0.25f);
	[SerializeField] protected List<Collider2D> colliders = new();
    [SerializeField] protected Rigidbody2D rb2d;
	[SerializeField] protected LayerMask startLayer;
	[SerializeField] protected LayerMask placedLayer;
	[HideInInspector] public bool overlapping = false;
    public List<SpriteRenderer> spriteRenderers = new();

	/// <summary>Player currently holding (or who last held) this item. Set by PlayerBlockHandler.</summary>
	public PlayerController Owner { get; private set; }

	/// <summary>
	/// True for items that launch on the place input instead of being placed
	/// (throwables). Skips the grounded/overlap placement checks.
	/// </summary>
	public virtual bool BypassPlacementChecks => false;

	public void SetOwner(PlayerController owner) => Owner = owner;

	protected virtual void OnDisable()
	{
		foreach (Collider2D col in colliders)
		{
			col.isTrigger = true;
			col.gameObject.layer = (int)Mathf.Log(startLayer.value, 2);;
		}
	}
	
	public void StartHold ()
	{
		holding = true;
		rb2d.bodyType = RigidbodyType2D.Kinematic;

		SetColor(holdColor);

		foreach (Collider2D col in colliders)
		{
			col.isTrigger = true;
			col.gameObject.layer = (int)Mathf.Log(startLayer.value, 2);
		}
	}
	public virtual void PlaceHoldable ()
	{
		foreach (Collider2D col in colliders)
		{
			col.isTrigger = false;
			col.gameObject.layer = (int)Mathf.Log(placedLayer.value, 2);
		}
		gameObject.layer = (int)Mathf.Log(placedLayer.value, 2);
		holding = false;
		rb2d.bodyType = RigidbodyType2D.Dynamic;
		AnimateAppear();
		OnPlaceSfx();
	}

	public List<Collider2D> GetColliders() => colliders;

	/// <summary>
	/// Called at the end of PlaceHoldable to play the placement sound.
	/// Override in subclasses to customise or suppress the default behaviour.
	/// </summary>
	protected virtual void OnPlaceSfx()
	{
		AudioManager.Instance.PlaySound("block_placement");
	}


	void Update ()
	{
		// foreach (GameObject obj in blocks)
		// {
		// 	float xScale = transform.localScale.x * -1f;
		// 	obj.transform.localScale = new Vector3 (xScale, 1, 1);
		// }

		if (overlapping)
		{
			SetColor(overlappingColor);
		}
		else if (holding)
		{
			SetColor(holdColor);
		}
	}


    public void SetMaterial(Material material)
    {
        foreach (SpriteRenderer sr in spriteRenderers)
        {
            sr.material = material;
        }
    }
    public void SetColor(Color color)
    {
        foreach (SpriteRenderer sr in spriteRenderers)
        {
            sr.color = color;
        }
    }


	public virtual void AnimateAppear()
	{
		Color newColor = holdColor;
		newColor.a = 1;
		foreach (SpriteRenderer rend in spriteRenderers)
		{
			rend.color = newColor;
		}
	}
}