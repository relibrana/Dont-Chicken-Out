using UnityEngine;

/// <summary>
/// One-liner access to the camera shake from anywhere (items, explosions,
/// deaths) without every caller having to find the rig or null-check it.
/// The shake itself lives in CinemachineVerticalRig2D; this is only the door.
/// If there is no rig in the scene the calls are silently ignored, so nothing
/// breaks in test scenes.
/// </summary>
public static class ScreenShake
{
    private static CinemachineVerticalRig2D Rig =>
        GameManager.instance != null ? GameManager.instance.cameraRig : null;

    /// <summary>
    /// Short, hard hit. This is the Mario-style punch: strong almost all the
    /// way and then a fast cut, never a long wobble.
    /// </summary>
    public static void Punch(float duration, float amplitude)
    {
        CinemachineVerticalRig2D rig = Rig;
        if (rig == null) return;

        rig.DoPunchShake(duration, amplitude);
    }

    /// <summary>Shake with the rig's own default duration and amplitude.</summary>
    public static void Default() => Rig?.DoShake();
}
