using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CircleTransitionController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform circleRect;

    [Header("Sizes")]
    [Min(0f)]
    [SerializeField] private float minSize = 0f;

    [Min(0f)]
    [SerializeField] private float maxSize = 2500f;

    [Header("Durations")]
    [Min(0f)]
    [SerializeField] private float closeDuration = 0.35f;

    [Min(0f)]
    [SerializeField] private float openDuration = 0.35f;

    public float CloseDuration => closeDuration;
    public float OpenDuration => openDuration;

    private void Awake()
    {
        SetSizeImmediate(minSize);
    }

    public void SetSizeImmediate(float size)
    {
        if (circleRect == null) return;
        circleRect.sizeDelta = new Vector2(size, size);
    }

    public IEnumerator Close()
    {
        yield return Animate(minSize, maxSize, closeDuration);
    }

    public IEnumerator Open()
    {
        yield return Animate(maxSize, minSize, openDuration);
    }

    private IEnumerator Animate(float from, float to, float duration)
    {
        if (circleRect == null) yield break;

        if (duration <= 0f)
        {
            SetSizeImmediate(to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float size = Mathf.Lerp(from, to, k);
            circleRect.sizeDelta = new Vector2(size, size);
            yield return null;
        }

        circleRect.sizeDelta = new Vector2(to, to);
    }
}
