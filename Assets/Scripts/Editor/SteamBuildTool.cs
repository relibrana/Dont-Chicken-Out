using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Tools > Don't Chicken Out > Steam.
///
/// Builds the Windows player ready to upload with SteamPipe (see Docs/steam-upload.md):
///   1. Builds the scenes in Build Settings to Builds/Steam/Windows/DontChickenOut.exe
///      (fixed exe name without spaces or symbols, so the Steamworks launch option never changes).
///   2. Deletes the folders Unity marks "DoNotShip" / "DontShipItWithYourGame".
///   3. If a depot folder inside the SDK's ContentBuilder was chosen, mirrors the build there,
///      so SteamPipeGUI only needs "Upload".
///
/// Also callable from the command line (Unity closed):
///   Unity.exe -batchmode -quit -projectPath . -executeMethod SteamBuildTool.BuildFromCommandLine
/// </summary>
internal static class SteamBuildTool
{
    public const string ExeName = "DontChickenOut.exe";
    private const string DepotPrefKey = "DCO.Steam.DepotFolder";

    private static string BuildFolder =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Steam", "Windows"));

    [MenuItem("Tools/Don't Chicken Out/Steam/Build Windows para Steam")]
    private static void BuildFromMenu()
    {
        if (!Build(out string error))
        {
            EditorUtility.DisplayDialog("Build para Steam", "Falló el build:\n\n" + error, "OK");
            return;
        }

        string depot = EditorPrefs.GetString(DepotPrefKey, "");
        string msg = $"Build listo en:\n{BuildFolder}";
        msg += string.IsNullOrEmpty(depot)
            ? "\n\nNo hay carpeta de depot elegida: copia el contenido a tu carpeta de ContentBuilder a mano " +
              "o usa Tools > Don't Chicken Out > Steam > Elegir carpeta del depot."
            : $"\n\nCopiado también al depot:\n{depot}\n\nSiguiente paso: SteamPipeGUI > Upload.";
        EditorUtility.DisplayDialog("Build para Steam", msg, "OK");
        EditorUtility.RevealInFinder(Path.Combine(BuildFolder, ExeName));
    }

    [MenuItem("Tools/Don't Chicken Out/Steam/Elegir carpeta del depot (ContentBuilder)")]
    private static void ChooseDepotFolder()
    {
        string current = EditorPrefs.GetString(DepotPrefKey, "");
        string picked = EditorUtility.OpenFolderPanel("Carpeta del depot dentro de ContentBuilder", current, "");
        if (string.IsNullOrEmpty(picked)) return;

        if (!IsInsideContentBuilder(picked))
        {
            EditorUtility.DisplayDialog("Carpeta del depot",
                "La carpeta tiene que estar dentro de la carpeta ContentBuilder del Steamworks SDK " +
                "(sdk/tools/ContentBuilder/...). Se vacía en cada build, así que no se acepta otra.", "OK");
            return;
        }

        EditorPrefs.SetString(DepotPrefKey, Path.GetFullPath(picked));
        Debug.Log($"[Steam] Carpeta del depot: {picked}");
    }

    /// <summary>Entry point for -executeMethod. Exits with code 1 on failure.</summary>
    public static void BuildFromCommandLine()
    {
        bool ok = Build(out string error);
        if (!ok) Debug.LogError("[Steam] " + error);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static bool Build(out string error)
    {
        error = null;
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) { error = "No hay escenas activas en Build Settings."; return false; }

        if (Directory.Exists(BuildFolder)) Directory.Delete(BuildFolder, true);
        Directory.CreateDirectory(BuildFolder);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(BuildFolder, ExeName),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            error = $"{report.summary.result} ({report.summary.totalErrors} errores, ver la consola).";
            return false;
        }

        foreach (string dir in Directory.GetDirectories(BuildFolder))
        {
            string name = Path.GetFileName(dir);
            if (name.EndsWith("DoNotShip", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("DontShipItWithYourGame", StringComparison.OrdinalIgnoreCase))
                Directory.Delete(dir, true);
        }

        Debug.Log($"[Steam] Build OK v{PlayerSettings.bundleVersion}: {BuildFolder} " +
                  $"({report.summary.totalSize / (1024f * 1024f):0.0} MB, {report.summary.totalTime:mm\\:ss})");

        string depot = EditorPrefs.GetString(DepotPrefKey, "");
        if (!string.IsNullOrEmpty(depot))
        {
            if (!IsInsideContentBuilder(depot)) { error = "La carpeta del depot guardada no está dentro de ContentBuilder."; return false; }
            MirrorFolder(BuildFolder, depot);
            Debug.Log($"[Steam] Copiado al depot: {depot}");
        }
        return true;
    }

    private static bool IsInsideContentBuilder(string path)
    {
        string full = Path.GetFullPath(path).Replace('\\', '/');
        int i = full.IndexOf("/ContentBuilder/", StringComparison.OrdinalIgnoreCase);
        return i >= 0 && full.Length > i + "/ContentBuilder/".Length; // a subfolder, never ContentBuilder itself
    }

    // Empties target and copies source into it, so files removed from the game don't linger in the depot.
    private static void MirrorFolder(string source, string target)
    {
        if (Directory.Exists(target)) Directory.Delete(target, true);
        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target));
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, target), true);
    }
}
