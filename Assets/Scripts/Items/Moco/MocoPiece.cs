using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A piece covered in moco (GDD, nov 2026). The moco that hits a piece
/// covers it whole: every block gets the goo (Item_MocoSplash: two "filling"
/// frames once, then the bubbling loop) and the piece becomes a trap. The
/// first chicken that touches it, kicking it included, gets stuck, and the
/// piece goes back to normal: each moco catches once. The moco also absorbs
/// that first kick (no damage, no push; KickCollider asks here first).
/// Apart from that the piece behaves as any other: it falls and is pushed.
///
/// Added at runtime to the BlockScript the moco hit; cleared when the piece
/// leaves play (break, pool, round reset).
/// </summary>
public sealed class MocoPiece : MonoBehaviour
{
    private readonly List<SpriteRenderer> _overlays = new();
    private Action<PlayerController> _stick;

    /// <summary>True while the piece is a trap waiting for its first chicken.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Covers every live block of the piece. <paramref name="frames"/> is the whole
    /// strip; from <paramref name="fillFrom"/> it plays once up to
    /// <paramref name="loopFrom"/>, then loops to the end.
    /// </summary>
    public void Apply(Sprite[] frames, float fps, int fillFrom, int loopFrom,
                      Vector2 overlayOffset, float overlayScale, Action<PlayerController> stick)
    {
        Clear();
        _stick   = stick;
        IsActive = true;

        Sprite[] strip = Slice(frames, fillFrom);
        int loop = Mathf.Clamp(loopFrom - fillFrom, 0, Mathf.Max(0, strip.Length - 1));

        // Fit by what is drawn, not by the frame: the goo is a block-wide slab
        // at the bottom of a 128 px frame, so centring the frame left it off its
        // block (nov 2026). The loop frame decides; the fill frames share it.
        bool fit = TryVisibleBox(strip.Length > 0 ? strip[loop] : null, out Vector2 visCenter, out float visWidth, out float visTop);

        foreach (BlockDamageable block in GetComponentsInChildren<BlockDamageable>())
        {
            SpriteRenderer blockRenderer = block.GetComponentInChildren<SpriteRenderer>();
            if (blockRenderer == null || !blockRenderer.enabled) continue; // a block already gone

            var go = new GameObject("Moco");
            go.transform.SetParent(block.transform, false);
            go.transform.localRotation = Quaternion.identity;

            // World size and place: the piece may be flipped (scale x -1) and the
            // block's sprite sits wherever its child puts it.
            Bounds blockBounds = blockRenderer.bounds;
            float  scale       = (fit ? blockBounds.size.x / visWidth : 1f) * overlayScale;
            Vector3 parentScale = block.transform.lossyScale;
            go.transform.localScale = new Vector3(scale / Mathf.Abs(parentScale.x), scale / Mathf.Abs(parentScale.y), 1f);
            // On the top edge of each block (design, nov 2026): the slab lies on
            // the block and its drips hang down its face. Centred sideways.
            Vector2 anchor = new Vector2(blockBounds.center.x, blockBounds.max.y);
            Vector2 drawn  = fit ? new Vector2(visCenter.x, visTop) * scale : Vector2.zero;
            go.transform.position   = anchor + overlayOffset - drawn;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = blockRenderer.sortingLayerID;
            sr.sortingOrder   = blockRenderer.sortingOrder + 5;

            if (strip.Length > 0) SpriteFlipbook.Play(sr, strip, fps, loop);
            _overlays.Add(sr);
        }
    }

    /// <summary>
    /// Sticks <paramref name="chicken"/> and turns the piece back to normal. False
    /// (nothing happens) if it is not a trap any more or the chicken cannot be caught.
    /// </summary>
    public bool Catch(PlayerController chicken)
    {
        if (!IsActive || chicken == null || !chicken.isOnGame || !chicken.IsAlive) return false;
        if (chicken.TryGetComponent(out MocoStuckState stuck) && (stuck.IsActive || stuck.HasReleaseImmunity))
            return false;

        Action<PlayerController> stick = _stick;
        Clear();
        stick?.Invoke(chicken);
        return true;
    }

    // Collisions of any block reach the piece root (it carries the Rigidbody2D).
    private void OnCollisionEnter2D(Collision2D collision) => TryCatch(collision);
    private void OnCollisionStay2D(Collision2D collision)  => TryCatch(collision);

    private void TryCatch(Collision2D collision)
    {
        if (!IsActive || collision.rigidbody == null) return;
        if (collision.rigidbody.TryGetComponent(out PlayerController chicken))
            Catch(chicken);
    }

    public void Clear()
    {
        IsActive = false;
        _stick   = null;
        foreach (SpriteRenderer sr in _overlays)
            if (sr != null) Destroy(sr.gameObject);
        _overlays.Clear();
    }

    private void OnDisable() => Clear();

    /// <summary>Centre, width and top of the drawn part (tight mesh), in sprite units from the pivot.</summary>
    private static bool TryVisibleBox(Sprite sprite, out Vector2 center, out float width, out float top)
    {
        center = Vector2.zero;
        width  = 0f;
        top    = 0f;
        if (sprite == null) return false;

        Vector2[] vertices = sprite.vertices;
        if (vertices == null || vertices.Length == 0) return false;

        Vector2 min = vertices[0], max = vertices[0];
        foreach (Vector2 v in vertices) { min = Vector2.Min(min, v); max = Vector2.Max(max, v); }

        center = (min + max) * 0.5f;
        width  = max.x - min.x;
        top    = max.y;
        return width > 0.0001f;
    }

    private static Sprite[] Slice(Sprite[] frames, int from)
    {
        if (frames == null || from >= frames.Length) return Array.Empty<Sprite>();
        from = Mathf.Max(0, from);
        var strip = new Sprite[frames.Length - from];
        Array.Copy(frames, from, strip, 0, strip.Length);
        return strip;
    }
}
