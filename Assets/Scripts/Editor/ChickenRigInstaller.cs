using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D.Animation;

/// <summary>
/// Tools > Don't Chicken Out > Arte nuevo > Montar pollo nuevo en PlayerPrefab.
///
/// The new chicken (Personaje_Chiken.psb, nov 2026) was assembled by Arte in
/// Scenes/ChickenRig.unity: Sprite Resolvers per part, SpriteLibrary
/// (Player_Colors) and Animator (Con_Player). This tool:
///  1. Saves that scene object as Prefabs/Chicken/Chicken.prefab (a variant of
///     the PSB, so re-imports of the art still flow through) plus ChickenSkin.
///     Only if it does not exist yet: delete it to rebuild from the scene.
///  2. In PlayerPrefab, swaps the old rig under "Rig" for that prefab, moves
///     PlayerAnimController (with its animation events) onto it, wires
///     PlayerController.animController/visualRoot, and scales/places "Rig" so
///     the new chicken is as tall as the old one and stands on the same feet.
///
/// "Rig" itself is kept (same object): the lobby copies it as the chicken it
/// shows in each room. Safe to run twice: an already mounted rig is skipped.
/// </summary>
internal static class ChickenRigInstaller
{
    private const string RigScenePath    = "Assets/Scenes/ChickenRig.unity";
    private const string ControllerPath  = "Assets/Sprites/NewArt/Con_Player.controller";
    private const string ChickenPrefab   = "Assets/Prefabs/Chicken/Chicken.prefab";
    private const string PlayerPrefab    = "Assets/Prefabs/PlayerPrefab.prefab";
    private const string RigName         = "Rig";

    private static readonly HashSet<string> WearCategories = new()
    {
        "Headwear", "Clothing", "Wing_wear_R", "Wing_wear_L", "Footwear_R", "Footwear_L",
    };

    [MenuItem("Tools/Don't Chicken Out/Arte nuevo/Montar pollo nuevo en PlayerPrefab")]
    private static void Run()
    {
        GameObject chickenAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ChickenPrefab);
        if (chickenAsset == null)
        {
            chickenAsset = CreateChickenPrefab();
            if (chickenAsset == null) return;
        }

        string report = MountInPlayerPrefab(chickenAsset);
        if (report == null) return;

        AssetDatabase.SaveAssets();
        Debug.Log("[ChickenRigInstaller] " + report);
        EditorUtility.DisplayDialog("Pollo nuevo", report, "OK");
    }

    // ── 1. Chicken prefab from the art scene ──────────────────────────────────

    private static GameObject CreateChickenPrefab()
    {
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller == null) return Fail($"No encuentro el controlador {ControllerPath}.");

        Scene scene   = SceneManager.GetSceneByPath(RigScenePath);
        bool  opened  = false;
        if (!scene.isLoaded)
        {
            scene  = EditorSceneManager.OpenScene(RigScenePath, OpenSceneMode.Additive);
            opened = true;
        }

        try
        {
            GameObject source = FindRig(scene, controller);
            if (source == null)
                return Fail($"En {RigScenePath} no hay ningún objeto con un Animator que use Con_Player.");

            Directory.CreateDirectory(Path.GetDirectoryName(ChickenPrefab));
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(source, ChickenPrefab, out bool ok);
            if (!ok || saved == null) return Fail("No se pudo guardar " + ChickenPrefab);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }

        // Clean root + ChickenSkin, edited on the asset so the scene stays untouched.
        GameObject contents = PrefabUtility.LoadPrefabContents(ChickenPrefab);
        try
        {
            contents.name = "Chicken";
            contents.transform.localPosition = Vector3.zero;
            contents.transform.localRotation = Quaternion.identity;
            contents.transform.localScale    = Vector3.one;
            if (!contents.TryGetComponent(out ChickenSkin _)) contents.AddComponent<ChickenSkin>();
            PrefabUtility.SaveAsPrefabAsset(contents, ChickenPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.ImportAsset(ChickenPrefab);
        return AssetDatabase.LoadAssetAtPath<GameObject>(ChickenPrefab);
    }

    private static GameObject FindRig(Scene scene, RuntimeAnimatorController controller)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            if (animator.runtimeAnimatorController != controller) continue;
            if (animator.GetComponentInParent<PlayerController>(true) != null) continue; // the PlayerPrefab copy in that scene
            return animator.gameObject;
        }
        return null;
    }

    // ── 2. Swap the rig inside PlayerPrefab ───────────────────────────────────

    private static string MountInPlayerPrefab(GameObject chickenAsset)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            var player = contents.GetComponent<PlayerController>();
            Transform rig = contents.transform.Find(RigName);
            if (player == null || rig == null)
                return FailText($"{PlayerPrefab} no tiene PlayerController o hijo '{RigName}'.");

            foreach (Transform child in rig)
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == chickenAsset)
                {
                    Wire(player, rig, child.GetComponent<PlayerAnimController>());
                    PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefab);
                    return "El pollo nuevo ya estaba montado: solo se revisaron las referencias.";
                }
            }

            // Old rig: bounds at rest, animation events, Animator.
            bool hadOld = TryGetBounds(rig, null, out Bounds oldBounds);
            PlayerAnimController oldAnim = rig.GetComponent<PlayerAnimController>();
            Animator oldAnimator         = rig.GetComponent<Animator>();

            var oldChildren = new List<GameObject>();
            foreach (Transform child in rig) oldChildren.Add(child.gameObject);

            // New chicken, nested prefab instance.
            var chicken = (GameObject)PrefabUtility.InstantiatePrefab(chickenAsset, rig);
            chicken.name = "Chicken";
            chicken.transform.localPosition = Vector3.zero;
            chicken.transform.localRotation = Quaternion.identity;
            chicken.transform.localScale    = Vector3.one;

            var newAnim = chicken.AddComponent<PlayerAnimController>();
            if (oldAnim != null) EditorUtility.CopySerialized(oldAnim, newAnim);

            Wire(player, rig, newAnim);

            if (oldAnim != null)     Object.DestroyImmediate(oldAnim, true);
            if (oldAnimator != null) Object.DestroyImmediate(oldAnimator, true);
            foreach (GameObject old in oldChildren) Object.DestroyImmediate(old, true);

            // Same height and feet as the old chicken.
            string sizing = "sin tamaño de referencia (Rig sin cambios)";
            SpriteLibrary libraryComponent = chicken.GetComponentInChildren<SpriteLibrary>(true);
            SpriteLibraryAsset library = libraryComponent != null ? libraryComponent.spriteLibraryAsset : null;
            if (hadOld && TryGetBounds(rig, WearSprites(library), out Bounds newBounds) && newBounds.size.y > 0.01f)
            {
                float k = oldBounds.size.y / newBounds.size.y;
                rig.localScale = new Vector3(rig.localScale.x * k, rig.localScale.y * k, 1f);

                // Feet = the chicken root: Arte raised the skeleton so the drawn feet
                // sit there. The sprite bounds are the import pose (≈1.3 u lower).
                TryGetBounds(rig, WearSprites(library), out newBounds);
                rig.position += new Vector3(oldBounds.center.x - newBounds.center.x,
                                            oldBounds.min.y - chicken.transform.position.y, 0f);
                sizing = $"Rig escalado x{k:0.###} (alto {oldBounds.size.y:0.##} u) y apoyado en los mismos pies";
            }

            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefab);

            return "Pollo nuevo montado en PlayerPrefab.\n\n"
                 + $"- {ChickenPrefab} bajo '{RigName}', con PlayerAnimController (eventos copiados) y ChickenSkin.\n"
                 + $"- {sizing}.\n"
                 + "- Quitados el rig viejo, su Animator y su PlayerAnimController.\n\n"
                 + "Revisa en Play: tamaño, pies sobre el suelo, patada y colores P1-P4.";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void Wire(PlayerController player, Transform rig, PlayerAnimController anim)
    {
        var so = new SerializedObject(player);
        so.FindProperty("animController").objectReferenceValue = anim;
        so.FindProperty("visualRoot").objectReferenceValue     = rig;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HashSet<Sprite> WearSprites(SpriteLibraryAsset library)
    {
        var set = new HashSet<Sprite>();
        if (library == null) return set;

        foreach (string category in library.GetCategoryNames())
        {
            if (!WearCategories.Contains(category)) continue;
            foreach (string label in library.GetCategoryLabelNames(category))
            {
                Sprite sprite = library.GetSprite(category, label);
                if (sprite != null) set.Add(sprite);
            }
        }
        return set;
    }

    /// <summary>World bounds of the active sprites, from sprite rects (skinned bounds are stale in edit mode).</summary>
    private static bool TryGetBounds(Transform root, HashSet<Sprite> skip, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr.sprite == null || !sr.enabled) continue;
            if (skip != null && skip.Contains(sr.sprite)) continue;

            Bounds local = sr.sprite.bounds;
            Vector3 min = sr.transform.TransformPoint(local.min);
            Vector3 max = sr.transform.TransformPoint(local.max);
            var world = new Bounds((min + max) * 0.5f, Vector3.zero);
            world.Encapsulate(min);
            world.Encapsulate(max);
            if (!any) { bounds = world; any = true; }
            else bounds.Encapsulate(world);
        }
        return any;
    }

    private static GameObject Fail(string message)
    {
        FailText(message);
        return null;
    }

    private static string FailText(string message)
    {
        Debug.LogError("[ChickenRigInstaller] " + message);
        EditorUtility.DisplayDialog("Pollo nuevo", message, "OK");
        return null;
    }
}
