#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility for swapping the active level scenes in Build Settings
/// between the original team-authored versions and the fully-integrated
/// duplicates (input + feedback + camera wired).
///
/// The two scene sets live side by side in the repo:
///   - Original team levels:   Assets/Scenes/Levels/Level_X.unity
///   - Integrated duplicates:  Assets/Scenes/LevelsIntegrated/Level_X.unity
///
/// Same filenames in both folders, different paths. Unity allows this.
/// Build Settings determines which folder's scenes are actually loaded
/// when the main menu calls SceneManager.LoadScene("Level_0") — the
/// menu's code is unaware of which version it's loading.
///
/// Why an Editor menu instead of file renaming or two branches:
///   - File renames can desync .meta files and confuse git.
///   - Maintaining two branches splits the team's work.
///   - Build Settings change is one config file (EditorBuildSettings.asset).
///     A single click swaps cleanly and the change is visible in git diff.
///
/// Usage:
///   Tools → Integration → Activate Integrated Levels (swap to LevelsIntegrated/)
///   Tools → Integration → Activate Original Levels    (swap to Levels/)
///   Tools → Integration → Show Current Build Settings (debug: list active scenes)
///
/// Author: Kane
/// </summary>
public static class LevelIntegrationSwapper
{
    // -- Folder paths. Update these constants if the project's scene layout changes. --
    private const string OriginalLevelsFolder = "Assets/Scenes/Levels";
    private const string IntegratedLevelsFolder = "Assets/Scenes/LevelsIntegrated";

    // -- Menu paths shown in Unity's top menu bar. --
    private const string MenuPathActivateIntegrated = "Tools/Integration/Activate Integrated Levels";
    private const string MenuPathActivateOriginal = "Tools/Integration/Activate Original Levels";
    private const string MenuPathShowCurrent = "Tools/Integration/Show Current Build Settings";

    // ============================================================
    // MENU ITEMS
    // ============================================================

    [MenuItem(MenuPathActivateIntegrated)]
    private static void ActivateIntegratedLevels()
    {
        SwapBuildSettings(fromFolder: OriginalLevelsFolder, toFolder: IntegratedLevelsFolder);
    }

    [MenuItem(MenuPathActivateOriginal)]
    private static void ActivateOriginalLevels()
    {
        SwapBuildSettings(fromFolder: IntegratedLevelsFolder, toFolder: OriginalLevelsFolder);
    }

    [MenuItem(MenuPathShowCurrent)]
    private static void ShowCurrentBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes;
        if (scenes == null || scenes.Length == 0)
        {
            Debug.Log("[LevelIntegrationSwapper] Build Settings is empty.");
            return;
        }

        Debug.Log($"[LevelIntegrationSwapper] Current Build Settings has {scenes.Length} scenes:");
        for (int i = 0; i < scenes.Length; i++)
        {
            var entry = scenes[i];
            string enabled = entry.enabled ? "ENABLED" : "disabled";
            Debug.Log($"  [{i}] {enabled}: {entry.path}");
        }
    }

    // ============================================================
    // SWAP LOGIC
    // ============================================================

    /// <summary>
    /// Replace every Build Settings entry pointing into `fromFolder` with
    /// the corresponding scene file in `toFolder`. The scene FILENAME is
    /// preserved (e.g., Level_0.unity stays Level_0.unity) — only the
    /// folder portion of the path changes.
    ///
    /// Other entries (MainMenu, etc.) that aren't in `fromFolder` are
    /// left untouched.
    /// </summary>
    private static void SwapBuildSettings(string fromFolder, string toFolder)
    {
        var currentScenes = EditorBuildSettings.scenes;
        if (currentScenes == null || currentScenes.Length == 0)
        {
            Debug.LogWarning("[LevelIntegrationSwapper] Build Settings is empty. Nothing to swap.");
            return;
        }

        var newScenes = new List<EditorBuildSettingsScene>(currentScenes.Length);
        int swapped = 0;
        int missing = 0;

        // Normalize folder strings to ensure trailing-slash safety in path checks.
        string fromFolderNormalized = fromFolder.TrimEnd('/') + "/";
        string toFolderNormalized = toFolder.TrimEnd('/') + "/";

        foreach (var entry in currentScenes)
        {
            string path = entry.path;
            if (path.StartsWith(fromFolderNormalized))
            {
                // Compute the candidate path in the new folder.
                string filename = Path.GetFileName(path);
                string candidatePath = toFolderNormalized + filename;

                if (File.Exists(candidatePath))
                {
                    var swappedEntry = new EditorBuildSettingsScene(candidatePath, entry.enabled);
                    newScenes.Add(swappedEntry);
                    swapped++;
                    Debug.Log($"[LevelIntegrationSwapper] Swapped: {path} -> {candidatePath}");
                }
                else
                {
                    // The target file doesn't exist. Keep the original entry
                    // so we don't break the build, but warn loudly.
                    newScenes.Add(entry);
                    missing++;
                    Debug.LogWarning($"[LevelIntegrationSwapper] Target scene missing — kept original: {candidatePath} not found.");
                }
            }
            else
            {
                // Entry isn't in the source folder; leave it alone.
                newScenes.Add(entry);
            }
        }

        EditorBuildSettings.scenes = newScenes.ToArray();

        // Save Project Settings so the change persists across editor restarts.
        AssetDatabase.SaveAssets();

        string summary = $"Swap complete. {swapped} swapped, {missing} missing (kept original).\n" +
                         $"Active folder for swapped scenes: {toFolderNormalized}";

        Debug.Log($"[LevelIntegrationSwapper] {summary}");

        if (missing > 0)
        {
            EditorUtility.DisplayDialog(
                "Integration Swap — Warnings",
                summary + "\n\nCheck the Console for details. Build Settings may be incomplete.",
                "OK"
            );
        }
        else
        {
            EditorUtility.DisplayDialog(
                "Integration Swap Complete",
                summary,
                "OK"
            );
        }
    }
}
#endif