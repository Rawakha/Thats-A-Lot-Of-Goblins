using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The Area_Root inspector, the one place to author an area: the settings field (or a button to make them), a checklist
// of what the area still needs with a fix button where one can be offered, Play This Area, New Area From This One, and
// the Area Settings and Wave Settings to tune right here.
[CustomEditor(typeof(Area_Root))]
public class Area_RootEditor : Editor
{
    private static bool showArea = true;
    private static bool showWaves = true;

    private Editor areaEditor;
    private Editor wavesEditor;

    private void OnDisable()
    {
        if (areaEditor != null)
            DestroyImmediate(areaEditor);

        if (wavesEditor != null)
            DestroyImmediate(wavesEditor);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("settings"));
        serializedObject.ApplyModifiedProperties();

        Area_Root root = (Area_Root)target;
        Area_Settings area = root.Settings;

        if (area == null)
        {
            EditorGUILayout.HelpBox("This area has no settings. Drag in an Area Settings asset, or make new ones.", MessageType.Warning);

            if (GUILayout.Button("Create Area Settings", GUILayout.Height(28f)))
                CreateSettings();

            return;
        }

        EditorGUILayout.Space();
        DrawChecklist(root, area);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Play This Area", GUILayout.Height(32f)))
                EditorApplication.isPlaying = true;

            if (GUILayout.Button("New Area From This One"))
                Area_NewWindow.Open();
        }

        EditorGUILayout.Space();
        showArea = EditorGUILayout.Foldout(showArea, $"Area Settings ({area.name})", true, EditorStyles.foldoutHeader);

        if (showArea)
            DrawNested(area, ref areaEditor);

        if (area.waves != null)
        {
            showWaves = EditorGUILayout.Foldout(showWaves, $"Wave Settings ({area.waves.name})", true, EditorStyles.foldoutHeader);

            if (showWaves)
                DrawNested(area.waves, ref wavesEditor);
        }
    }

    private static void DrawNested(UnityEngine.Object asset, ref Editor editor)
    {
        CreateCachedEditor(asset, null, ref editor);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            editor.OnInspectorGUI();
    }

    private static void DrawChecklist(Area_Root root, Area_Settings area)
    {
        EditorGUILayout.LabelField("Checklist", EditorStyles.boldLabel);
        Wave_Settings waves = area.waves;
        Wave_Director director = root.GetComponent<Wave_Director>();

        Check(waves != null, "Wave Settings assigned");

        if (waves != null)
        {
            Check(waves.goblinType != null, "Goblin Type set on the Wave Settings");
            Check(waves.lastWave > 0, "Last Wave set, so the area can be cleared", "Set to 5", () =>
            {
                Undo.RecordObject(waves, "Set Last Wave");
                waves.lastWave = 5;
                EditorUtility.SetDirty(waves);
            });
        }

        int holes = 0;
        Wave_Hole[] found = FindObjectsByType<Wave_Hole>(FindObjectsInactive.Exclude);

        for (int i = 0; i < found.Length; i++)
        {
            if (found[i].TryGetComponent(out Flow_Goal goal) && goal.enabled)
                holes++;
        }

        Check(holes >= 2, $"At least two holes with Flow Goals ({holes} found)");

        Flow_System flow = FindAnyObjectByType<Flow_System>();
        Check(flow != null, "Flow_System in the scene");

        if (flow != null)
        {
            Flow_Map map = new SerializedObject(flow).FindProperty("map").objectReferenceValue as Flow_Map;
            Check(map != null && map.IsBaked, "Flow map baked", "Select Flow_System", () => Selection.activeObject = flow);
        }

        Check(root.Bounds != null, "Map edges (Area_Bounds)", "Create", () => CreateChild<Area_Bounds>(root, "Map Edges", "bounds"));
        Check(root.Spawn != null, "Plane spawn (Area_Spawn)", "Create", () => CreateChild<Area_Spawn>(root, "Plane Spawn", "spawn"));

        if (root.Bounds != null && root.Spawn != null)
            Check(root.Bounds.InsideWarning(root.Spawn.Point), "Plane spawn inside the warning line");

        // Areas load on top of the Run scene, which has to be first in Build Settings
        string scenePath = root.gameObject.scene.path;
        EditorBuildSettingsScene[] built = EditorBuildSettings.scenes;
        bool runFirst = Area_BuildSettings.RunSceneFirst(scenePath);
        bool inBuild = false;

        for (int i = 0; i < built.Length; i++)
        {
            if (built[i].path == scenePath)
                inBuild = true;
        }

        Check(runFirst, "Run scene is first in Build Settings (needed to Play from here)", "Fix", Area_BuildSettings.Fix);
        Check(inBuild, "This area is in Build Settings", "Add", () =>
        {
            EditorBuildSettingsScene[] list = new EditorBuildSettingsScene[built.Length + 1];
            built.CopyTo(list, 0);
            list[built.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = list;
        });

        Check(!director.DebugWaves, "Debug Waves off (a debug wave can't clear the area)", "Turn Off", () =>
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("debugWaves").boolValue = false;
            so.ApplyModifiedProperties();
        });
    }

    // A new child object with this component, assigned to the Area_Root's field and selected for its handles
    private static void CreateChild<T>(Area_Root root, string childName, string field) where T : Component
    {
        GameObject child = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
        child.transform.SetParent(root.transform, false);
        T component = child.AddComponent<T>();

        SerializedObject so = new SerializedObject(root);
        so.FindProperty(field).objectReferenceValue = component;
        so.ApplyModifiedProperties();
        Selection.activeGameObject = child;
    }

    private static void Check(bool ok, string label, string fixLabel = null, Action fix = null)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUIContent icon = EditorGUIUtility.IconContent(ok ? "TestPassed" : "TestFailed");
            GUILayout.Label(icon, GUILayout.Width(20f), GUILayout.Height(18f));
            EditorGUILayout.LabelField(label);

            if (!ok && fix != null && GUILayout.Button(fixLabel, GUILayout.Width(130f)))
                fix();
        }
    }

    // New linked settings beside the scene's other area assets
    private void CreateSettings()
    {
        string sceneName = ((Area_Root)target).gameObject.scene.name;

        if (string.IsNullOrEmpty(sceneName))
        {
            EditorUtility.DisplayDialog("Save the scene first", "The settings are named after the scene, so save it first.", "OK");
            return;
        }

        string folder = Area_NewWindow.EnsureFolder(sceneName);
        Area_Settings area = Area_NewWindow.CreateSettings(folder, sceneName, null);

        serializedObject.FindProperty("settings").objectReferenceValue = area;
        serializedObject.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(((Area_Root)target).gameObject.scene);
    }
}
