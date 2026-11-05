using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Goblins > Area > New Area: type a name, pick the scene to copy, press Create. It makes Assets/Areas/<name>/ holding
// the new scene and its own copies of the Area Settings and Wave Settings, linked up, so tuning the new area never
// touches the old one. It also turns Debug Waves off, clears the flow map so the new area bakes its own, adds the scene
// to Build Settings, opens it and selects the Area_Root, whose checklist says what's left.
public class Area_NewWindow : EditorWindow
{
    private const string Root = "Assets/Areas";

    private string areaName = "";
    private SceneAsset copyFrom;

    [MenuItem("Goblins/Area/New Area")]
    public static void Open()
    {
        Area_NewWindow window = GetWindow<Area_NewWindow>(true, "New Area");
        window.minSize = window.maxSize = new Vector2(380f, 130f);
        window.copyFrom = AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorSceneManager.GetActiveScene().path);
    }

    private void OnGUI()
    {
        GUI.SetNextControlName("Name");
        areaName = EditorGUILayout.TextField(new GUIContent("Name", "e.g. Forest 01. Names the scene, its assets and the banner."), areaName);
        copyFrom = (SceneAsset)EditorGUILayout.ObjectField(new GUIContent("Copy From", "The area scene to start from."), copyFrom, typeof(SceneAsset), false);

        string problem = Problem();

        if (problem != null)
            EditorGUILayout.HelpBox(problem, MessageType.Info);

        using (new EditorGUI.DisabledScope(problem != null))
        {
            if (GUILayout.Button("Create", GUILayout.Height(28f)))
                Create();
        }
    }

    private string Problem()
    {
        string trimmed = areaName.Trim();

        if (trimmed.Length == 0)
            return "Give the area a name.";

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "The name has a character files can't use.";

        if (AssetDatabase.IsValidFolder($"{Root}/{trimmed}"))
            return "There's already an area with that name.";

        return copyFrom == null ? "Pick a scene to copy." : null;
    }

    private void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string name = areaName.Trim();
        string folder = EnsureFolder(name);
        string scenePath = $"{folder}/{name}.unity";

        if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(copyFrom), scenePath))
        {
            EditorUtility.DisplayDialog("New Area", "Couldn't copy the scene.", "OK");
            return;
        }

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Area_Root root = FindAnyObjectByType<Area_Root>();

        if (root == null)
        {
            EditorUtility.DisplayDialog("New Area", "The scene you copied has no Area_Root. Add one to its Wave_Director's " +
                                                    "object, then use the Create button there.", "OK");
            return;
        }

        SerializedObject rootObject = new SerializedObject(root);
        SerializedProperty settings = rootObject.FindProperty("settings");
        settings.objectReferenceValue = CreateSettings(folder, name, settings.objectReferenceValue as Area_Settings);
        rootObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject director = new SerializedObject(root.GetComponent<Wave_Director>());
        director.FindProperty("debugWaves").boolValue = false;
        director.ApplyModifiedPropertiesWithoutUndo();

        Flow_System flow = FindAnyObjectByType<Flow_System>();

        if (flow != null)
        {
            SerializedObject flowObject = new SerializedObject(flow);
            flowObject.FindProperty("map").objectReferenceValue = null;
            flowObject.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.SaveScene(scene);
        AddToBuild(scenePath);

        Selection.activeGameObject = root.gameObject;
        Close();
    }

    public static string EnsureFolder(string name)
    {
        if (!AssetDatabase.IsValidFolder(Root))
            AssetDatabase.CreateFolder("Assets", "Areas");

        string folder = $"{Root}/{name}";

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(Root, name);

        return folder;
    }

    // New Area Settings and Wave Settings in the folder, linked. Copies of the source's if there is one, so tuning carries over.
    public static Area_Settings CreateSettings(string folder, string name, Area_Settings source)
    {
        Wave_Settings waves = source != null && source.waves != null
            ? Instantiate(source.waves)
            : CreateInstance<Wave_Settings>();

        AssetDatabase.CreateAsset(waves, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name} Waves.asset"));

        Area_Settings area = source != null ? Instantiate(source) : CreateInstance<Area_Settings>();
        area.areaName = name;
        area.waves = waves;
        AssetDatabase.CreateAsset(area, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name} Area.asset"));

        AssetDatabase.SaveAssets();
        return area;
    }

    private static void AddToBuild(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
