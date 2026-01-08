using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockScript : HoldableItem
{
	[SerializeField] protected List<Animator> animators = new();

	protected override void OnDisable()
	{
		base.OnDisable();
		
		foreach (Animator anim in animators)
		{
			anim.enabled = false;
		}
	}

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
		foreach (Animator anim in animators)
		{
			anim.enabled = true;
			anim.SetTrigger ("appear");
			yield return new WaitForSeconds (waitTime);
		}
	}
}
