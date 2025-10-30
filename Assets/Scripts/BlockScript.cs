using System.Collections;
using System.Collections.Generic;
using Microsoft.Unity.VisualStudio.Editor;
using UnityEngine;

public class BlockScript : MonoBehaviour
{
	[SerializeField] private List<GameObject> blocks = new ();
	[SerializeField] private List<SpriteRenderer> spriteRenderers = new ();
	public List<BoxCollider2D> boxCollider2Ds = new ();
	[SerializeField] private List<Animator> animators = new ();
	// public Sprite orangeSprite;
	// public Sprite blueSprite;

	[SerializeField] private LayerMask startLayer;
	[SerializeField] private LayerMask placedLayer;

	public Rigidbody2D rb2D;

	Color holdColor = new Color (1f, 1f, 1f, 0.25f);
	Color overlappingColor = new Color (1f, 0, 0, 0.25f);

	bool holding = false;
	[HideInInspector] public bool overlapping = false;


    void Start()
    {
    }

	void Update ()
	{
		foreach (GameObject obj in blocks)
		{
			float xScale = transform.localScale.x * -1f;
			obj.transform.localScale = new Vector3 (xScale, 1, 1);
		}

		if (overlapping)
		{
			foreach (SpriteRenderer rend in spriteRenderers)
			{
				rend.color = overlappingColor;
			}
		}
		else if (holding)
		{
			foreach (SpriteRenderer rend in spriteRenderers)
			{
				rend.color = holdColor;
			}
		}
	}

	public void StartHold ()
	{
		holding = true;
		rb2D.bodyType = RigidbodyType2D.Kinematic;

		foreach (SpriteRenderer rend in spriteRenderers)
		{
			// rend.sprite = playerIndex == 1 ? blueSprite : orangeSprite;
			rend.color = holdColor;
		}
		foreach (BoxCollider2D col in boxCollider2Ds)
		{
			col.isTrigger = true;
			col.gameObject.layer = (int)Mathf.Log(startLayer.value, 2);;
		}
	}
	public void StopHold ()
	{
		foreach (BoxCollider2D col in boxCollider2Ds)
		{
			col.isTrigger = false;
			col.gameObject.layer = (int)Mathf.Log(placedLayer.value, 2);;
		}
		holding = false;
		rb2D.bodyType = RigidbodyType2D.Dynamic;
	}

	public void AnimateAppear ()
	{
		Color alphaZero =  new Color (1f, 1f, 1f, 0f);
		foreach (SpriteRenderer rend in spriteRenderers)
		{
			rend.color = alphaZero;
		}
		StartCoroutine (AnimateAppearRoutine(0.1f));
	}

	private void OnDisable()
	{
		foreach (Animator anim in animators)
		{
			anim.enabled = false;
		}
		foreach (BoxCollider2D col in boxCollider2Ds)
		{
			col.isTrigger = true;
			col.gameObject.layer = (int)Mathf.Log(startLayer.value, 2);;
		}
	}

	IEnumerator AnimateAppearRoutine(float waitTime)
	{
		foreach (Animator anim in animators)
		{
			anim.enabled = true;
			anim.SetTrigger ("appear");
			yield return new WaitForSeconds (waitTime);
		}
	}
}
