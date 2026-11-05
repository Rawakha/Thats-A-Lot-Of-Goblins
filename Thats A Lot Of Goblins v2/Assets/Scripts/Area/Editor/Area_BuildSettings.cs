using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Keeps Build Settings the way areas need it: the Run scene first (Play from any area, and Restart, depend on that), then
// the areas, with no scenes that have gone missing. Goblins > Area > Fix Build Settings does it, and so does the Fix button on
// an Area_Root's checklist.
public static class Area_BuildSettings
{
    private const string RunSceneName = "Run";

    // The scene Play and Restart start from: Build Settings' first scene, if it exists
    public static bool RunSceneFirst(string notThisScene)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        return scenes.Length > 0 && scenes[0].path != notThisScene
            && AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[0].path) != null;
    }

    [MenuItem("Goblins/Area/Fix Build Settings")]
    public static void Fix()
    {
        string run = FindRunScene();

        if (run == null)
        {
            EditorUtility.DisplayDialog("Fix Build Settings", $"Couldn't find a scene called \"{RunSceneName}\" in Assets. " +
                                        "Name the Run scene that, or drag it to the top of File > Build Profiles by hand.", "OK");
            return;
        }

        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(run, true) };
        int dropped = 0;

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == run)
                continue;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) == null)
                dropped++;
            else
                list.Add(scene);
        }

        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"Build Settings: '{run}' is first now" + (dropped > 0 ? $", and {dropped} missing scene(s) were removed." : "."));
    }

    // The Run scene to start Play from: Build Settings' first scene if it exists, otherwise the scene called Run
    public static string RunScenePath()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        if (scenes.Length > 0 && AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[0].path) != null)
            return scenes[0].path;

        return FindRunScene();
    }

    // The scene asset named Run, in Assets but not in a plugin
    private static string FindRunScene()
    {
        foreach (string guid in AssetDatabase.FindAssets($"{RunSceneName} t:SceneAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (path.StartsWith("Assets/Plugins") || Path.GetFileNameWithoutExtension(path) != RunSceneName)
                continue;

            return path;
        }

        return null;
    }
}
