using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tools > Don't Chicken Out > Limpiar nivel (quitar lobby antiguo).
///
/// Strips a level scene down to a playable stage: everything of the old
/// in-arena lobby goes (hay start button, per-player UI and scores, dim
/// layer, controls panel, "x / 4" counter, PlayerInputManager join). Works on
/// the open scene if it is a level (has a GameManager), else opens Main_Level.
/// Safe to run twice: it only removes what it finds.
/// </summary>
internal static class LevelCleanupTool
{
    private const string DefaultLevel = "Assets/Scenes/Main_Level.unity";

    // Children of the UIManager object that belonged to the old lobby.
    // (PlayersPoints stays: it is the between-rounds score, driven by RoundScoreboard.)
    private static readonly string[] OldUiChildren =
    {
        "DimLayer", "PlayersUI",
        "Controls", "Controls1", "Controls2",
        "PlayersCount", "Players1", "Players2",
    };

    private const string ScorePanel     = "PlayersPoints";
    private const string FilledEggGuid  = "d9bdadbfc3a40ac4cb41d85ff2895657"; // the egg sprite the old UIManager used

    // Root objects of the old lobby.
    private static readonly string[] OldRoots = { "GameStartButton" };

    [MenuItem("Tools/Don't Chicken Out/Limpiar nivel (quitar lobby antiguo)")]
    private static void Run()
    {
        if (Object.FindFirstObjectByType<GameManager>() == null)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(DefaultLevel, OpenSceneMode.Single);
        }

        Scene scene = SceneManager.GetActiveScene();
        var removed = new List<string>();

        UIManager ui = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (ui != null)
        {
            foreach (string name in OldUiChildren)
            {
                Transform child = ui.transform.Find(name);
                if (child == null) continue;
                removed.Add($"{ui.name}/{name}");
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (System.Array.IndexOf(OldRoots, root.name) < 0) continue;
            removed.Add(root.name);
            Undo.DestroyObjectImmediate(root);
        }

        foreach (PlayerInputManager pim in Object.FindObjectsByType<PlayerInputManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            removed.Add($"{pim.name} (PlayerInputManager)");
            Undo.DestroyObjectImmediate(pim);
        }

        // Scripts deleted with the old lobby (StartGameButton, PlayerUI) leave
        // "missing script" components behind on anything that survived.
        int missing = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                missing += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        if (missing > 0) removed.Add($"{missing} componente(s) con script faltante");

        string score = WireScoreboard(ui);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[LevelCleanupTool] {scene.name} guardada. {score}\n"
                + (removed.Count == 0 ? "Nada que quitar." : "Quitado:\n- " + string.Join("\n- ", removed)));
    }

    /// <summary>
    /// PlayersPoints / PlayerNContent / PlayerN / PlayerNPoints / EggKImage:
    /// one row per player slot, eggs in order. Adds and fills RoundScoreboard.
    /// </summary>
    private static string WireScoreboard(UIManager ui)
    {
        Transform panel = ui != null ? FindDeep(ui.transform, ScorePanel) : null;
        if (panel == null) return "Marcador: no se encontró PlayersPoints (nada que conectar).";

        if (!panel.TryGetComponent(out CanvasGroup _)) Undo.AddComponent<CanvasGroup>(panel.gameObject);
        if (!panel.TryGetComponent(out RoundScoreboard board)) board = Undo.AddComponent<RoundScoreboard>(panel.gameObject);

        var so = new SerializedObject(board);
        SerializedProperty rows = so.FindProperty("rows");
        rows.arraySize = 4;
        int wired = 0;
        for (int slot = 0; slot < 4; slot++)
        {
            SerializedProperty row = rows.GetArrayElementAtIndex(slot);
            Transform content = FindDeep(panel, $"Player{slot + 1}Content");
            Transform points  = FindDeep(panel, $"Player{slot + 1}Points");

            row.FindPropertyRelative("root").objectReferenceValue = content != null ? content.gameObject : null;

            var eggs = new List<Image>();
            if (points != null)
                foreach (Transform child in points)
                    if (child.name.StartsWith("Egg") && child.TryGetComponent(out Image img)) eggs.Add(img);

            SerializedProperty eggsProp = row.FindPropertyRelative("eggs");
            eggsProp.arraySize = eggs.Count;
            for (int i = 0; i < eggs.Count; i++)
                eggsProp.GetArrayElementAtIndex(i).objectReferenceValue = eggs[i];
            if (eggs.Count > 0) wired++;
        }

        string eggPath = AssetDatabase.GUIDToAssetPath(FilledEggGuid);
        so.FindProperty("filledEgg").objectReferenceValue =
            string.IsNullOrEmpty(eggPath) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(eggPath);
        so.ApplyModifiedProperties();

        GameManager gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm != null)
        {
            var gmSo = new SerializedObject(gm);
            gmSo.FindProperty("scoreboard").objectReferenceValue = board;
            gmSo.ApplyModifiedProperties();
        }

        return $"Marcador entre rondas conectado ({wired}/4 filas con huevos).";
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
