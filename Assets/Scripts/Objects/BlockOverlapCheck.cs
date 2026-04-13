using System;
using UnityEngine;

public class BlockOverlapCheck : MonoBehaviour
{
    private const float BLOCK_INNER_AREA_RATIO = .95f;

    [SerializeField] private LayerMask layersToDetect;
    private BoxCollider2D _boxCollider2D;
    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        _boxCollider2D = GetComponent<BoxCollider2D>();
        _spriteRenderer = gameObject.GetComponentInChildren<SpriteRenderer>();
    }

    public bool OverlapCheck(Action onOverlap = null)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(layersToDetect);
        filter.useTriggers = false;

        Collider2D[] overlapColliders = Physics2D.OverlapBoxAll(_boxCollider2D.bounds.center, _boxCollider2D.bounds.size * BLOCK_INNER_AREA_RATIO, 0f, layersToDetect);
        foreach(Collider2D collider in overlapColliders)
        {
            if(collider != _boxCollider2D && !collider.isTrigger)
            {
                onOverlap?.Invoke();
                return true;
            }
        }
        return false;
    }
    
    public void DisableBlock()
    {
        _boxCollider2D.enabled = false;
        _spriteRenderer.enabled = false;
    }
}
