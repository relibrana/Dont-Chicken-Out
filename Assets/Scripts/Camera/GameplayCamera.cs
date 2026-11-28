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

    /// <summary>
    /// Which horizontal third of the view a world height is in: 0 bottom,
    /// 1 middle, 2 top (GDD, asignación de pieza e ítem). The thirds are of what
    /// the camera shows, so they rise with it. Above or below the view clamps
    /// to the top or bottom third; no camera = middle.
    /// </summary>
    public static int ViewThird(float worldY)
    {
        Camera cam = Get();
        if (cam == null) return 1;

        float bottom = cam.ViewportToWorldPoint(Vector3.zero).y;
        float top    = cam.ViewportToWorldPoint(Vector3.one).y;
        if (top <= bottom) return 1;

        float t = (worldY - bottom) / (top - bottom);
        return t < 1f / 3f ? 0 : t < 2f / 3f ? 1 : 2;
    }
}
