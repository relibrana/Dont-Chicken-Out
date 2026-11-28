using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

/// <summary>
/// Look of the chicken rig (Personaje_Chiken.psb, nov 2026). Lives on the rig
/// root, next to the Animator and the SpriteLibrary (Player_Colors).
///
/// - Colour: the art is drawn in four colours as Sprite Library labels
///   (Yellow / Blue / Green / Pink per body part). The player slot picks the
///   label; nothing is tinted by shader any more.
/// - Wear (Headwear, Clothing, Wing/Footwear): hidden unless something asks
///   for it. The Stun clip turns the headwear into the "Stunned" stars by
///   itself; Metal Chicken switches on every "Metal" piece.
/// - Death: the body is switched off on the spot (PlayerController.SetAlive),
///   so the Die clip plays on a detached copy of the rig that fades away.
///
/// Eyes and beak (Normal / Hurt / Stunned / Dead) are not touched here: the
/// clips swap them (HitFront/HitBack = Hurt, Stun = Stunned, Die = Dead).
///
/// Presentation only: nothing here feeds back into the simulation.
/// </summary>
[DefaultExecutionOrder(1000)] // after the SpriteResolvers have applied the animated labels
public sealed class ChickenSkin : MonoBehaviour
{
    // ── Palette ───────────────────────────────────────────────────────────────

    /// <summary>Sprite Library label of each player slot: P1 yellow, P2 blue, P3 green, P4 pink.</summary>
    public static readonly string[] ColorLabels = { "Yellow", "Blue", "Green", "Pink" };

    /// <summary>Name-tag / UI colour matching each label.</summary>
    private static readonly Color[] DisplayColors =
    {
        new Color(1f,    0.82f, 0.25f, 1f), // Yellow
        new Color(0.35f, 0.66f, 1f,    1f), // Blue
        new Color(0.42f, 0.86f, 0.38f, 1f), // Green
        new Color(1f,    0.52f, 0.78f, 1f), // Pink
    };

    public static Color DisplayColor(int slot) =>
        DisplayColors[((slot % DisplayColors.Length) + DisplayColors.Length) % DisplayColors.Length];

    public static string ColorLabel(int slot) =>
        ColorLabels[((slot % ColorLabels.Length) + ColorLabels.Length) % ColorLabels.Length];

    private const string HeadwearCategory = "Headwear";
    private const string StunnedLabel     = "Stunned";
    private const string MetalLabel       = "Metal";

    // Arte renamed them in nov 2026 (Wing_wear → WingWear, Footwear → FootWear):
    // both spellings count, case aside, so an older rig still works.
    private static readonly HashSet<string> WearCategories = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "Headwear", "Clothing", "WingWear_R", "WingWear_L", "FootWear_R", "FootWear_L",
        "Wing_wear_R", "Wing_wear_L",
    };

    private static readonly HashSet<string> ColorLabelSet = new(ColorLabels);

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Muerte")]
    [SerializeField, Tooltip("Estado del Animator con la animación de muerte.")]
    private string dieState = "Anim_Die";

    [SerializeField, Min(0f), Tooltip("Segundos que el cuerpo se queda quieto tras la animación Die antes de desvanecerse.")]
    private float deathLinger = 0.35f;

    [SerializeField, Min(0.01f), Tooltip("Duración del fundido del cuerpo.")]
    private float deathFade = 0.35f;

    // ── State ─────────────────────────────────────────────────────────────────

    private SpriteLibrary    _library;
    private SpriteResolver[] _resolvers;
    private SpriteRenderer   _headwear;          // the animated headwear (Stun stars)
    private SpriteResolver   _headwearResolver;
    private Sprite           _headwearMetal;     // used when there is no standalone metal helmet
    private bool             _hasStandaloneMetalHelmet;

    private readonly List<SpriteRenderer> _metalParts     = new();
    private readonly List<bool>           _metalWasActive = new();
    private readonly List<SpriteRenderer> _plainRenderers = new(); // coloured parts without a resolver
    private readonly Dictionary<Sprite, (string category, string label)> _spriteKeys = new();

    private bool             _built;
    private int              _colorSlot = -1;
    private bool             _metal;
    private bool             _isDeathCopy;
    private PlayerController _owner;

    /// <summary>
    /// Where the feet are drawn. Arte raised the skeleton (Huesos) in the rig so
    /// the root of this object sits at the feet; the import positions of the
    /// sprites do not know that, so never ground the chicken by sprite bounds.
    /// </summary>
    public Vector3 FeetPosition => transform.position;

    public int  ColorSlot => _colorSlot;
    public bool IsMetal   => _metal;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake() => Build();

    private void OnEnable()
    {
        _owner = GetComponentInParent<PlayerController>();
        if (_owner != null && !_isDeathCopy) SimEvents.Raised += OnSimEvent;
    }

    private void OnDisable() => SimEvents.Raised -= OnSimEvent;

    private void LateUpdate() => ApplyHeadwear();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Paints the chicken with the colour of a player slot (0-3).</summary>
    public void SetColor(int slot)
    {
        Build();
        _colorSlot = slot;
        string label = ColorLabel(slot);

        foreach (SpriteResolver resolver in _resolvers)
        {
            string category = resolver.GetCategory();
            if (string.IsNullOrEmpty(category) || WearCategories.Contains(category)) continue;
            if (!ColorLabelSet.Contains(resolver.GetLabel() ?? string.Empty)) continue; // e.g. "Big" thigh
            if (!HasEntry(category, label)) continue;

            if (resolver.TryGetComponent(out SpriteRenderer resolvedRenderer))
                PrepareSwap(resolvedRenderer, _library.GetSprite(category, label));
            resolver.SetCategoryAndLabel(category, label);
            resolver.ResolveSpriteToSpriteRenderer();
        }

        // Parts the rig left without a resolver: swap the sprite by hand.
        foreach (SpriteRenderer sr in _plainRenderers)
        {
            if (sr == null || sr.sprite == null) continue;
            if (!_spriteKeys.TryGetValue(sr.sprite, out var key)) continue;
            if (!ColorLabelSet.Contains(key.label) || !HasEntry(key.category, label)) continue;

            Sprite sprite = _library.GetSprite(key.category, label);
            if (sprite == null) continue;
            PrepareSwap(sr, sprite);
            sr.sprite = sprite;
        }
    }

    /// <summary>Metal Chicken look: helmet, armour, wing guards and boots.</summary>
    public void SetMetal(bool metal)
    {
        Build();
        _metal = metal;

        for (int i = 0; i < _metalParts.Count; i++)
        {
            SpriteRenderer sr = _metalParts[i];
            if (sr == null) continue;
            if (metal) sr.gameObject.SetActive(true);
            else if (!_metalWasActive[i]) sr.gameObject.SetActive(false);
            sr.forceRenderingOff = !metal;
        }

        ApplyHeadwear();
    }

    /// <summary>
    /// Plays the Die clip on a copy of this rig left where the chicken died, then
    /// fades it out. The real body is already hidden by then.
    /// </summary>
    public void SpawnDeathCopy()
    {
        Build();

        GameObject copy = Instantiate(gameObject, transform.position, transform.rotation);
        copy.name = $"{(_owner != null ? _owner.name : name)}_Dead";
        copy.transform.localScale = transform.lossyScale;

        // The clips' events (kick collider, steps) must not reach the real player.
        foreach (PlayerAnimController anim in copy.GetComponentsInChildren<PlayerAnimController>(true))
            DestroyImmediate(anim);
        foreach (Animator animator in copy.GetComponentsInChildren<Animator>(true))
            if (!animator.TryGetComponent(out LobbyAnimEventSink _))
                animator.gameObject.AddComponent<LobbyAnimEventSink>();

        var skin = copy.GetComponent<ChickenSkin>();
        skin._isDeathCopy = true;
        SimEvents.Raised -= skin.OnSimEvent;

        // Copied from a hidden body: switch the drawing back on, then restore the look
        // (forceRenderingOff is not copied by Instantiate).
        foreach (SpriteRenderer sr in copy.GetComponentsInChildren<SpriteRenderer>())
            sr.enabled = true;
        skin.HideAllWear();
        if (_colorSlot >= 0) skin.SetColor(_colorSlot);
        skin.SetMetal(_metal);

        skin.StartCoroutine(skin.DeathRoutine());
    }

    // ── Death ─────────────────────────────────────────────────────────────────

    private void OnSimEvent(SimEventData e)
    {
        if (e.Type != SimEvent.PlayerDied || _owner == null) return;
        if (e.Source != _owner) return;
        SpawnDeathCopy();
    }

    private IEnumerator DeathRoutine()
    {
        float clipLength = 0.6f;
        if (TryGetComponent(out Animator animator) && animator.runtimeAnimatorController != null)
        {
            animator.Play(dieState, 0, 0f);
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == dieState) clipLength = clip.length;
        }

        yield return new WaitForSeconds(clipLength + deathLinger);

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        var startAlpha = new float[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) startAlpha[i] = renderers[i].color.a;

        for (float t = 0f; t < deathFade; t += Time.deltaTime)
        {
            float k = 1f - t / deathFade;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = renderers[i].color;
                c.a = startAlpha[i] * k;
                renderers[i].color = c;
            }
            yield return null;
        }

        Destroy(gameObject);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void Build()
    {
        if (_built) return;
        _built = true;

        _library   = GetComponentInChildren<SpriteLibrary>(true);
        _resolvers = GetComponentsInChildren<SpriteResolver>(true);

        // No global autoRebind: it finds bones by name, and the skeleton has two
        // bones called "Wing_L" (the lower one becomes "Wing_L_1" in the
        // hierarchy), so a rebind loses the lower left wing. See PrepareSwap.

        SpriteLibraryAsset asset = _library != null ? _library.spriteLibraryAsset : null;
        if (asset != null)
        {
            foreach (string category in asset.GetCategoryNames())
            foreach (string label in asset.GetCategoryLabelNames(category))
            {
                Sprite sprite = asset.GetSprite(category, label);
                if (sprite != null) _spriteKeys.TryAdd(sprite, (category, label));
            }
        }
        else
        {
            Debug.LogWarning($"[ChickenSkin] {name}: no SpriteLibrary with an asset — colours and wear won't change.", this);
        }

        var resolved = new HashSet<SpriteRenderer>();
        foreach (SpriteResolver resolver in _resolvers)
        {
            if (!resolver.TryGetComponent(out SpriteRenderer sr)) continue;
            resolved.Add(sr);

            if (resolver.GetCategory() == HeadwearCategory && (_headwear == null || resolver.gameObject.activeSelf))
            {
                _headwear         = sr;
                _headwearResolver = resolver;
            }
        }

        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == _headwear) continue;

            string category = null, label = null;
            if (resolved.Contains(sr))
            {
                var resolver = sr.GetComponent<SpriteResolver>();
                category = resolver.GetCategory();
                label    = resolver.GetLabel();
            }
            else if (sr.sprite != null && _spriteKeys.TryGetValue(sr.sprite, out var key))
            {
                category = key.category;
                label    = key.label;
                _plainRenderers.Add(sr);
            }

            if (category == null || !WearCategories.Contains(category)) continue;

            if (label == MetalLabel)
            {
                _metalParts.Add(sr);
                _metalWasActive.Add(sr.gameObject.activeSelf);
                if (category == HeadwearCategory) _hasStandaloneMetalHelmet = true;
            }
            else
            {
                sr.forceRenderingOff = true; // other wear (Crown, Grandma, BigJump…) has no use yet
            }
        }

        if (_library != null) _headwearMetal = _library.GetSprite(HeadwearCategory, MetalLabel);

        HideStrayVariants(resolved);
        HideAllWear();
    }

    /// <summary>
    /// A part animated through its resolver (Eyes, Beak, Body_Base…) is drawn by
    /// that one object; the other variant layers of the same category must stay
    /// off. The prefab switches them off by override, but those overrides point
    /// at import IDs that change when Arte renames layers: in nov 2026 headwear
    /// "Stunnet" became "Stunned", same name as the dizzy eyes, and the dizzy eyes
    /// came back on under the animated ones (seen with the closed "Hurt" eyes).
    /// So hide any leftover here, whatever the prefab says. Wear is not touched:
    /// its plain pieces are the metal parts, handled by SetMetal.
    /// </summary>
    private void HideStrayVariants(HashSet<SpriteRenderer> resolved)
    {
        var animated = new HashSet<string>();
        foreach (SpriteResolver resolver in _resolvers)
        {
            string category = resolver.GetCategory();
            if (!string.IsNullOrEmpty(category) && resolver.gameObject.activeSelf) animated.Add(category);
        }

        foreach (SpriteRenderer sr in _plainRenderers)
        {
            if (sr == null || resolved.Contains(sr) || !_spriteKeys.TryGetValue(sr.sprite, out var key)) continue;
            if (WearCategories.Contains(key.category) || !animated.Contains(key.category)) continue;
            sr.forceRenderingOff = true;
        }
    }

    private void HideAllWear()
    {
        foreach (SpriteRenderer sr in _metalParts)
            if (sr != null) sr.forceRenderingOff = !_metal;
        ApplyHeadwear();
    }

    /// <summary>
    /// The headwear resolver is animated (Stun clip) and the Animator writes its
    /// default label back every frame, so its visibility is decided every frame.
    /// </summary>
    private void ApplyHeadwear()
    {
        if (_headwear == null) return;

        bool stunned = _headwearResolver != null && _headwearResolver.GetLabel() == StunnedLabel;
        bool visible = stunned;

        if (!stunned && _metal && !_hasStandaloneMetalHelmet && _headwearMetal != null)
        {
            _headwear.sprite = _headwearMetal;
            visible = true;
        }

        _headwear.forceRenderingOff = !visible;
    }

    /// <summary>
    /// Colour variants are not all weighted to the same bones (Body_Base: Yellow
    /// uses Huesos + Torso, Blue/Green/Pink only Huesos). Swapping to a sprite
    /// with another bone list needs a rebind or the skin goes invalid and the
    /// part is drawn out of place. Rebind only those: the rebind matches bones
    /// by name and breaks the parts tied to the duplicated "Wing_L".
    /// </summary>
    private static void PrepareSwap(SpriteRenderer sr, Sprite next)
    {
        if (sr == null || next == null || !sr.TryGetComponent(out SpriteSkin skin)) return;
        skin.autoRebind = !SameBones(sr.sprite, next);
    }

    private static bool SameBones(Sprite a, Sprite b)
    {
        if (a == null || b == null || a == b) return true;

        var bonesA = a.GetBones();
        var bonesB = b.GetBones();
        if (bonesA.Length != bonesB.Length) return false;

        for (int i = 0; i < bonesA.Length; i++)
            if (bonesA[i].name != bonesB[i].name) return false;
        return true;
    }

    private bool HasEntry(string category, string label) =>
        _library != null && _library.GetSprite(category, label) != null;
}
