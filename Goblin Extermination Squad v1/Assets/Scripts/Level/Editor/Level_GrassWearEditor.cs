using UnityEditor;
using UnityEngine;

// Optional wear setup and runtime diagnostics. Editor-only UI/Undo allocations, no subscriptions.
[CustomEditor(typeof(Level_GrassWear))]
public class Level_GrassWearEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var wear = (Level_GrassWear)target;
        EditorGUILayout.HelpBox("Walking goblins remove grass and reveal a near-black layer below dark grass. " +
            "Paths merge and last until the area restarts. Radius controls path width; Strength controls how quickly it wears. " +
            "Meshes are baked; runtime work is capped mask painting.",MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)) DrawDefaultInspector();
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Mask",$"{wear.TextureWidth} × {wear.TextureHeight}");
            EditorGUILayout.LabelField("Last Pixel Checks",wear.PixelChecks.ToString("N0"));
            EditorGUILayout.LabelField("Last Budget Skips",wear.Skipped.ToString("N0"));
            if (GUILayout.Button("Clear Worn Paths")) wear.ClearPaths();
        }
        else if (GUILayout.Button("Rebuild Worn Ground Layer")) wear.Bake();
    }
    public static void SetUp(Level_GroundSurface surface)
    {
        var wear = surface.GetComponent<Level_GrassWear>();
        if (wear == null) wear = Undo.AddComponent<Level_GrassWear>(surface.gameObject);
        if (surface.Bake()) wear.Bake();
        Selection.activeGameObject = surface.gameObject;
    }
}
