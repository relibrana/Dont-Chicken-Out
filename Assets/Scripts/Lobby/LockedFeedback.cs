using DG.Tweening;
using UnityEngine;

/// <summary>"This is locked" nudge: a short horizontal shake and the invalid sound.</summary>
public static class LockedFeedback
{
    public static void Play(Transform target)
    {
        if (target == null) return;

        target.DOKill(complete: true);
        if (target is RectTransform rect)
            rect.DOShakeAnchorPos(0.3f, new Vector2(14f, 0f), vibrato: 20, randomness: 0f).SetUpdate(true);
        else
            target.DOShakePosition(0.3f, new Vector3(0.12f, 0f, 0f), vibrato: 20, randomness: 0f).SetUpdate(true);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("block_invalid");
    }
}
