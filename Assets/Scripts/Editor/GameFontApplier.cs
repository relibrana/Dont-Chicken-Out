using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Don't Chicken Out > Arte nuevo > Aplicar fuente del juego.
///
/// Puts the game font (DontChickenOutFont SDF, Arte nov 2026) on every
/// TextMeshPro text of the scenes in Build Settings and of the prefabs under
/// Assets/Prefabs, and makes it TMP's default font so texts created at
/// runtime (the "Player N" tags) use it too.
///
/// Texts with a custom material preset (outline, shadow…) keep the look: the
/// preset is copied onto the new font's atlas (saved next to the font).
/// The font lacks á é í ó ú ñ: GameFont() adds CalSans as fallback, so those
/// letters show in CalSans until Arte adds them to the font.
/// Safe to run twice: texts already on the game font are skipped.
/// </summary>
internal static class GameFontApplier
{
    private const string PrefabsFolder = "Assets/Prefabs";

    [MenuItem("Tools/Don't Chicken Out/Arte nuevo/Aplicar fuente del juego")]
    private static void Run()
    {
        TMP_FontAsset font = MenuPanelBuilderKit.GameFont();
        if (font == null || font.name != "DontChickenOutFont SDF")
        {
            EditorUtility.DisplayDialog("Fuente del juego", "No encuentro 'DontChickenOutFont SDF'.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var presets = new Dictionary<Material, Material>();
        int texts = 0;
        var touched = new List<string>();

        // Prefabs first: scene instances then inherit the change.
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int n = ApplyTo(root, font, presets, skipPrefabInstances: false);
                if (n == 0) continue;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                texts += n;
                touched.Add($"{Path.GetFileName(path)} ({n})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        string startScene = SceneManager.GetActiveScene().path;
        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || string.IsNullOrEmpty(entry.path)) continue;
            Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);

            int n = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                n += ApplyTo(root, font, presets, skipPrefabInstances: true);
            if (n == 0) continue;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            texts += n;
            touched.Add($"{Path.GetFileName(entry.path)} ({n})");
        }

        SetTmpDefaultFont(font);
        AssetDatabase.SaveAssets();

        if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);

        string report = $"Fuente del juego aplicada a {texts} textos.\n\n"
                      + (touched.Count > 0 ? string.Join("\n", touched) : "Ya estaba todo con la fuente del juego.")
                      + "\n\nTambién es la fuente por defecto de TextMeshPro (textos creados en juego)."
                      + "\nRevisa que ningún texto se salga de su caja: la fuente nueva mide distinto.";
        Debug.Log("[GameFontApplier] " + report);
        EditorUtility.DisplayDialog("Fuente del juego", report, "OK");
    }

    private static int ApplyTo(GameObject root, TMP_FontAsset font, Dictionary<Material, Material> presets,
                               bool skipPrefabInstances)
    {
        int count = 0;
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font == font) continue;
            // In scenes, prefab instances get the font from their prefab (done above).
            if (skipPrefabInstances && PrefabUtility.IsPartOfPrefabInstance(text)) continue;

            Material oldMaterial = text.fontSharedMaterial;
            bool customPreset = text.font != null && oldMaterial != null && oldMaterial != text.font.material;

            Undo.RecordObject(text, "Aplicar fuente del juego");
            text.font = font;
            if (customPreset) text.fontSharedMaterial = PresetFor(oldMaterial, font, presets);

            EditorUtility.SetDirty(text);
            count++;
        }
        return count;
    }

    /// <summary>Copy of a material preset (outline, glow…) that samples the game font's atlas.</summary>
    private static Material PresetFor(Material old, TMP_FontAsset font, Dictionary<Material, Material> presets)
    {
        if (presets.TryGetValue(old, out Material cached)) return cached;

        string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(font))?.Replace('\\', '/') ?? "Assets";
        string path   = $"{folder}/{font.name} - {old.name}.mat";

        var preset = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (preset == null)
        {
            preset = new Material(old) { name = $"{font.name} - {old.name}" };
            preset.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            preset.SetFloat(ShaderUtilities.ID_TextureWidth,  font.atlasWidth);
            preset.SetFloat(ShaderUtilities.ID_TextureHeight, font.atlasHeight);
            preset.SetFloat(ShaderUtilities.ID_GradientScale, font.atlasPadding + 1);
            AssetDatabase.CreateAsset(preset, path);
        }

        presets[old] = preset;
        return preset;
    }

    private static void SetTmpDefaultFont(TMP_FontAsset font)
    {
        TMP_Settings settings = TMP_Settings.instance;
        if (settings == null) return;

        var so = new SerializedObject(settings);
        SerializedProperty prop = so.FindProperty("m_defaultFontAsset");
        if (prop == null || prop.objectReferenceValue == font) return;

        prop.objectReferenceValue = font;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }
}
