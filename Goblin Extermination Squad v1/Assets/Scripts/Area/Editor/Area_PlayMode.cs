using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Makes Play work from any area scene. The Run scene (first in Build Settings) holds the plane, camera, HUD and horde,
// so pressing Play with an area open starts in the Run scene and has Run_Boot load that area on top. With the Run scene
// open (or anything else), Play is normal. Nothing to set up; it only changes the editor's Play start scene.
[InitializeOnLoad]
public static class Area_PlayMode
{
    private static bool warned;

    static Area_PlayMode()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.activeSceneChangedInEditMode += (_, _) => Refresh();
        EditorSceneManager.sceneOpened += (_, _) => Refresh();
        EditorApplication.delayCall += Refresh;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredEditMode)
            Refresh();
    }

    // Points Play at the Run scene when the active scene is an area, and clears it otherwise
    private static void Refresh()
    {
        if (EditorApplication.isPlaying)
            return;

        SessionState.EraseString(Run_Boot.EditorStartKey);
        EditorSceneManager.playModeStartScene = null;

        Scene active = EditorSceneManager.GetActiveScene();

        if (!active.IsValid() || string.IsNullOrEmpty(active.path) || !HasArea(active))
            return;

        string runPath = Area_BuildSettings.RunScenePath();
        SceneAsset run = runPath != null ? AssetDatabase.LoadAssetAtPath<SceneAsset>(runPath) : null;

        if (run == null)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("Can't Play from an area: there's no Run scene. Name it 'Run', or put it first in Build " +
                                 "Settings (Goblins > Area > Fix Build Settings).");
            }

            return;
        }

        warned = false;

        if (runPath == active.path)
            return;

        EditorSceneManager.playModeStartScene = run;
        SessionState.SetString(Run_Boot.EditorStartKey, active.path);
    }

    private static bool HasArea(Scene scene)
    {
        if (!scene.isLoaded)
            return false;

        foreach (UnityEngine.GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<Area_Root>(true) != null)
                return true;
        }

        return false;
    }
}
