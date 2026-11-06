using UnityEditor;
using UnityEngine;

// Surface setup on a selected Level_Ground, and creation of editable light/dark grass area GameObjects.
// Editor only. Standard Undo/asset creation allocations, no runtime subscriptions.
[CustomEditor(typeof(Level_GroundSurface))]
public class Level_GroundSurfaceEditor : InspectorButtonEditor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("The grid ground is the base. Draw both light and dark grass areas like cliffs. " +
            "Each type merges into one flat mesh. Light sits above dark. Colours share world-space variation, " +
            "shared with matching grass cards and flowers.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Set Up Grass Layers"))
                AssignMaterial((Level_GroundSurface)target);
            if (GUILayout.Button("Set Up Goblin Worn Paths")) Level_GrassWearEditor.SetUp((Level_GroundSurface)target);
            if (GUILayout.Button("Add Light Grass Area"))
                AddArea((Level_GroundSurface)target, Level_GrassArea.Kind.Light);
            if (GUILayout.Button("Add Dark Grass Area"))
                AddArea((Level_GroundSurface)target, Level_GrassArea.Kind.Dark);
            if (GUILayout.Button("Set Up Grass Cards and Flowers"))
            {
                var surface = (Level_GroundSurface)target;
                Level_GrassCover cover = surface.GetComponent<Level_GrassCover>();
                if (cover == null) cover = Undo.AddComponent<Level_GrassCover>(surface.gameObject);
                Level_GrassCoverEditor.SetUp(cover);
            }
            base.OnInspectorGUI();
        }
    }

    [MenuItem("GameObject/Level/Terrain/Grass Surface (Selected Ground)", false, 42)]
    private static void CreateSurface(MenuCommand command)
    {
        GameObject selected = command.context as GameObject;
        if (selected == null) selected = Selection.activeGameObject;
        Level_Ground ground = selected != null ? selected.GetComponentInParent<Level_Ground>() : null;
        if (ground == null)
        {
            Debug.LogWarning("Select a Level_Ground, then add a Grass Surface.");
            return;
        }
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set Up Grass Surface");
        Level_GroundSurface surface = ground.GetComponent<Level_GroundSurface>();
        if (surface == null) surface = Undo.AddComponent<Level_GroundSurface>(ground.gameObject);
        AssignMaterial(surface);
        surface.Bake();
        Undo.CollapseUndoOperations(undo);
        Selection.activeGameObject = surface.gameObject;
    }

    private static void AssignMaterial(Level_GroundSurface surface)
    {
        const string path = "Assets/Art/Level/GroundSurface_Mat.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Goblins/Ground Surface");
            if (shader == null) { Debug.LogError("Ground Surface shader has not imported yet."); return; }
            material = new Material(shader) { name = "GroundSurface_Mat", enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
        }
        var data = new SerializedObject(surface);
        data.FindProperty("grassMaterial").objectReferenceValue = material;
        data.ApplyModifiedProperties();
        Level_GroundSurface.RequestRebuilds();
    }

    public static void AddArea(Level_GroundSurface surface, Level_GrassArea.Kind kind)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var area = new GameObject(kind == Level_GrassArea.Kind.Light ? "Light Grass Area" : "Dark Grass Area");
        Undo.RegisterCreatedObjectUndo(area, "Create Grass Area");
        Undo.SetTransformParent(area.transform, surface.transform, "Parent Grass Area");
        Vector3 position = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : surface.transform.position;
        position = surface.transform.InverseTransformPoint(position);
        Vector2 half = surface.GetComponent<Level_Ground>().Size * 0.5f;
        area.transform.localPosition = new Vector3(Mathf.Clamp(position.x, -half.x, half.x), 0f, Mathf.Clamp(position.z, -half.y, half.y));
        area.transform.localRotation = Quaternion.identity;
        area.transform.localScale = Vector3.one;
        area.layer = surface.gameObject.layer;
        Level_GrassArea component = Undo.AddComponent<Level_GrassArea>(area);
        var data = new SerializedObject(component);
        data.FindProperty("layer").enumValueIndex = (int)kind;
        data.ApplyModifiedProperties();
        Selection.activeGameObject = area;
        Level_GroundSurface.RequestRebuilds();
    }
}
