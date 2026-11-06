using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The base for tools that build their own output directly (Level_Cliff's mesh, Level_TreeArea's prefabs, Level_Ground's
// mesh) rather than generating stones to bake. It owns the shared rebuild rules: changes in one editor tick make one
// rebuild, a rebuild only happens if the inputs changed (see Level_BuildKey), straight after a load the saved output is
// trusted instead of rebuilt, and Undo checks every tool. It has no fields, so saved objects are unaffected.
//
// Setup: derive from this and supply InputKey, HasSavedOutput and RebuildNow; call RememberBuild at the end of a build.
// GC: nothing at runtime, no ticks. Editor: one shared Undo callback; each rebuild request queues one delayCall.
public abstract class Level_BuiltTool : MonoBehaviour
{
#if UNITY_EDITOR
    [NonSerialized] private bool rebuildQueued;

    // What the output was last built from. Not saved, so an undo or reload never makes it lie.
    [NonSerialized] private string builtKey;

    protected abstract string InputKey();
    protected abstract bool HasSavedOutput { get; }
    protected abstract void RebuildNow();

    protected void RememberBuild() => builtKey = InputKey();

    protected void BuildIfChanged()
    {
        string key = InputKey();

        if (builtKey == null && HasSavedOutput)
        {
            builtKey = key;
            return;
        }

        if (key != builtKey)
            RebuildNow();
    }

    // Rebuilds on the next editor tick if anything changed. Never in play. Editors call this after a drag or click.
    public void QueueRebuild()
    {
        if (rebuildQueued || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        rebuildQueued = true;
        EditorApplication.delayCall += () =>
        {
            rebuildQueued = false;

            if (this != null && !EditorApplication.isPlayingOrWillChangePlaymode && !PrefabUtility.IsPartOfPrefabAsset(this))
                BuildIfChanged();
        };
    }

    protected virtual void OnValidate() => QueueRebuild();

    // Undo puts values back without calling OnValidate, so every tool checks itself; only changed ones rebuild
    [InitializeOnLoadMethod]
    private static void WatchUndo()
    {
        Undo.undoRedoPerformed += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            foreach (Level_BuiltTool tool in FindObjectsByType<Level_BuiltTool>(FindObjectsInactive.Exclude))
                tool.QueueRebuild();
        };
    }
#endif
}
