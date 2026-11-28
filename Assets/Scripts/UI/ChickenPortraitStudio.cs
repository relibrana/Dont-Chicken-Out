using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Live portraits of the chickens for UI (between-rounds scoreboard, nov 2026).
/// Each player gets a "booth" far away from the level: a copy of their rig
/// (same art, same colour, idling) and a small camera that draws it into a
/// RenderTexture the UI shows with a RawImage. A copy and not a sprite, so the
/// portrait always matches the chicken in play, whatever Arte changes.
///
/// The copy keeps ChickenSkin (it only draws) and loses every other script of
/// ours: PlayerAnimController's events would toggle the real player's kick
/// collider. Cameras only run while a portrait is on screen (SetLive).
/// </summary>
public sealed class ChickenPortraitStudio : MonoBehaviour
{
    private static readonly Vector3 Origin = new(-10000f, -10000f, 0f);
    private const float BoothSpacing = 50f;

    // Height of the view around the chicken, in world units.
    private const float ViewHeight = 2.7f;

    // Where the view is centred above the feet.
    private const float ViewCentreAboveFeet = 1.15f;

    private sealed class Booth
    {
        public PlayerController Player;
        public GameObject       Chicken;
        public Camera           Camera;
        public RenderTexture    Texture;
        public int              ColorSlot;
    }

    private static ChickenPortraitStudio _instance;
    private readonly Dictionary<int, Booth> _booths = new();

    /// <summary>Portrait texture of <paramref name="player"/> (slot = colour and booth). Null if no player.</summary>
    public static RenderTexture PortraitOf(PlayerController player, int slot, int size = 256)
    {
        if (player == null) return null;
        if (_instance == null)
        {
            var go = new GameObject("ChickenPortraitStudio");
            _instance = go.AddComponent<ChickenPortraitStudio>();
        }
        return _instance.Get(player, slot, size);
    }

    /// <summary>Turns the booth cameras and chickens on (portraits visible) or off.</summary>
    public static void SetLive(bool live)
    {
        if (_instance == null) return;
        foreach (Booth booth in _instance._booths.Values)
        {
            if (booth.Camera  != null) booth.Camera.enabled = live;
            if (booth.Chicken != null) booth.Chicken.SetActive(live);
        }
    }

    private void OnDestroy()
    {
        foreach (Booth booth in _booths.Values)
            if (booth.Texture != null) booth.Texture.Release();
        if (_instance == this) _instance = null;
    }

    private RenderTexture Get(PlayerController player, int slot, int size)
    {
        if (_booths.TryGetValue(slot, out Booth booth) && booth.Player == player && booth.Chicken != null)
        {
            ChickenSkin skin = booth.Chicken.GetComponentInChildren<ChickenSkin>(true);
            if (skin != null && booth.ColorSlot != slot) { skin.SetColor(slot); booth.ColorSlot = slot; }
            return booth.Texture;
        }

        if (booth != null) Clear(booth);
        booth = Build(player, slot, size);
        _booths[slot] = booth;
        return booth.Texture;
    }

    private Booth Build(PlayerController player, int slot, int size)
    {
        var booth = new Booth { Player = player, ColorSlot = slot };
        Vector3 anchor = Origin + Vector3.right * (BoothSpacing * slot);

        // Built under an inactive holder so none of the copied scripts wake up
        // before they are stripped (same trick as the lobby rooms).
        var holder = new GameObject("PortraitHolder");
        holder.SetActive(false);
        holder.transform.SetParent(transform, false);

        Transform source = player.VisualRoot;
        GameObject chicken = Instantiate(source.gameObject, holder.transform);
        chicken.name = $"Portrait_P{slot + 1}";

        System.Reflection.Assembly ours = typeof(ChickenPortraitStudio).Assembly;
        foreach (MonoBehaviour mb in chicken.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null && mb.GetType().Assembly == ours && mb is not ChickenSkin) DestroyImmediate(mb);
        foreach (Animator animator in chicken.GetComponentsInChildren<Animator>(true))
        {
            animator.gameObject.AddComponent<LobbyAnimEventSink>();
            animator.updateMode = AnimatorUpdateMode.UnscaledTime; // the scoreboard fades in unscaled time too
        }
        // A dead player's renderers are switched off: the copy is not dead.
        foreach (SpriteRenderer sr in chicken.GetComponentsInChildren<SpriteRenderer>(true))
            sr.enabled = true;

        Vector3 scale = source.lossyScale;
        chicken.transform.SetParent(transform, false);
        chicken.transform.position   = anchor;
        chicken.transform.rotation   = Quaternion.identity;
        chicken.transform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 1f); // always facing right
        Destroy(holder);

        ChickenSkin skin = chicken.GetComponentInChildren<ChickenSkin>(true);
        if (skin != null)
        {
            skin.SetColor(slot);
            skin.SetMetal(false);
        }
        Vector3 feet = skin != null ? skin.FeetPosition : anchor;

        booth.Texture = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32) { name = $"Portrait_P{slot + 1}" };
        booth.Texture.Create();

        var camGo = new GameObject($"PortraitCamera_P{slot + 1}");
        camGo.transform.SetParent(transform, false);
        camGo.transform.position = new Vector3(feet.x, feet.y + ViewCentreAboveFeet, anchor.z - 10f);

        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic     = true;
        cam.orthographicSize = ViewHeight * 0.5f;
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = new Color(0f, 0f, 0f, 0f);
        cam.nearClipPlane    = 0.1f;
        cam.farClipPlane     = 50f;
        cam.depth            = -100f;
        cam.targetTexture    = booth.Texture;

        booth.Chicken = chicken;
        booth.Camera  = cam;
        return booth;
    }

    private static void Clear(Booth booth)
    {
        if (booth.Chicken != null) Destroy(booth.Chicken);
        if (booth.Camera  != null) Destroy(booth.Camera.gameObject);
        if (booth.Texture != null) booth.Texture.Release();
    }
}
