using UnityEngine;

/// <summary>
/// Plays a list of sprites on a SpriteRenderer at a fixed frame rate, for art
/// delivered as frame strips (moco splat, teleport poof) without building an
/// Animator for each. Optional loop over the tail of the strip (impact frames
/// once, then the idle frames forever) and optional self-destroy at the end.
/// Presentation only.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SpriteFlipbook : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private Sprite[]       _frames;
    private float          _fps;
    private int            _loopFrom;
    private bool           _destroyOnEnd;
    private float          _time;
    private int            _shown = -1;

    /// <summary>
    /// Starts (or restarts) a strip on <paramref name="renderer"/>.
    /// <paramref name="loopFrom"/> = first frame of the loop, -1 = play once and stop on the last frame.
    /// </summary>
    public static SpriteFlipbook Play(SpriteRenderer renderer, Sprite[] frames, float fps, int loopFrom = -1, bool destroyOnEnd = false)
    {
        if (renderer == null || frames == null || frames.Length == 0) return null;

        if (!renderer.TryGetComponent(out SpriteFlipbook book))
            book = renderer.gameObject.AddComponent<SpriteFlipbook>();

        book._renderer     = renderer;
        book._frames       = frames;
        book._fps          = Mathf.Max(1f, fps);
        book._loopFrom     = loopFrom < frames.Length ? loopFrom : -1;
        book._destroyOnEnd = destroyOnEnd;
        book._time         = 0f;
        book._shown        = -1;
        book.enabled       = true;
        book.Show(0);
        return book;
    }

    /// <summary>One-shot effect in the world: a new object that plays the strip once and removes itself.</summary>
    public static SpriteRenderer SpawnOnce(string name, Sprite[] frames, float fps, Vector3 position,
                                           float scale, int sortingLayerId, int sortingOrder)
    {
        if (frames == null || frames.Length == 0) return null;

        SpriteRenderer sr = SpawnSprite(name, frames[0], position, scale, sortingLayerId, sortingOrder);
        Play(sr, frames, fps, -1, destroyOnEnd: true);
        return sr;
    }

    /// <summary>Plain world sprite (no animation), for effects tweened by their caller.</summary>
    public static SpriteRenderer SpawnSprite(string name, Sprite sprite, Vector3 position,
                                             float scale, int sortingLayerId, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.position   = position;
        go.transform.localScale = Vector3.one * scale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite         = sprite;
        sr.sortingLayerID = sortingLayerId;
        sr.sortingOrder   = sortingOrder;
        return sr;
    }

    /// <summary>Sorting layer and order range of everything drawn under <paramref name="root"/> (e.g. a chicken).</summary>
    public static (int layerId, int minOrder, int maxOrder) SortingOf(Transform root)
    {
        int layer = 0, min = int.MaxValue, max = int.MinValue;
        bool any = false;
        foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>())
        {
            // Only what is actually drawn: hidden parts (forceRenderingOff on the
            // new rig, switched-off renderers) may sit on another layer.
            if (!sr.enabled || sr.forceRenderingOff) continue;
            if (!any) { layer = sr.sortingLayerID; any = true; }
            if (sr.sortingLayerID != layer) continue;
            min = Mathf.Min(min, sr.sortingOrder);
            max = Mathf.Max(max, sr.sortingOrder);
        }
        return any ? (layer, min, max) : (0, 0, 0);
    }

    /// <summary>Stops on the current frame.</summary>
    public void Stop() => enabled = false;

    private void Update()
    {
        if (_frames == null) return;

        _time += Time.deltaTime;
        int frame = Mathf.FloorToInt(_time * _fps);

        if (frame >= _frames.Length)
        {
            if (_loopFrom >= 0)
            {
                int loopLength = _frames.Length - _loopFrom;
                frame = _loopFrom + (frame - _frames.Length) % loopLength;
            }
            else
            {
                if (_destroyOnEnd) { Destroy(gameObject); return; }
                Show(_frames.Length - 1);
                enabled = false;
                return;
            }
        }

        Show(frame);
    }

    private void Show(int frame)
    {
        if (frame == _shown || _renderer == null) return;
        _shown = frame;
        _renderer.sprite = _frames[frame];
    }
}
