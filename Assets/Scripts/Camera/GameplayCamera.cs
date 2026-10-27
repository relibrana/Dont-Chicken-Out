using UnityEngine;

/// <summary>
/// The camera that shows the round: the one inside CameraRig.prefab, tagged
/// MainCamera, so this is Camera.main. Kept as the one place that knows it,
/// plus the view-edge helpers.
/// </summary>
public static class GameplayCamera
{
    public static Camera Get() => Camera.main;

    /// <summary>World Y of the top / bottom edge of the view, or the fallback if there is no camera.</summary>
    public static float TopY(float fallback)    => Get() != null ? Get().ViewportToWorldPoint(Vector3.one).y  : fallback;
    public static float BottomY(float fallback) => Get() != null ? Get().ViewportToWorldPoint(Vector3.zero).y : fallback;
}
