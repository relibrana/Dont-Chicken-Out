using UnityEngine;

/// <summary>
/// The camera that shows the round. Camera.main is NOT reliable in this
/// project: the play camera lives inside CameraRig.prefab and nothing carries
/// the MainCamera tag, so it returns null. Falls back to the scene's camera.
/// (AudioManager and ProgressionManager still carry their own copy of this.)
/// </summary>
public static class GameplayCamera
{
    private static Camera _cached;

    public static Camera Get()
    {
        if (_cached != null) return _cached;

        _cached = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        return _cached;
    }

    /// <summary>World Y of the top / bottom edge of the view, or the fallback if there is no camera.</summary>
    public static float TopY(float fallback)    => Get() != null ? Get().ViewportToWorldPoint(Vector3.one).y  : fallback;
    public static float BottomY(float fallback) => Get() != null ? Get().ViewportToWorldPoint(Vector3.zero).y : fallback;

    // Enter Play Mode without domain reload keeps statics alive between sessions.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _cached = null;
}
