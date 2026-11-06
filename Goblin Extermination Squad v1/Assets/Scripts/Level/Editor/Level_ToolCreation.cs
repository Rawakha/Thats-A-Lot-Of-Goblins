using UnityEditor;
using UnityEngine;

// The one way level tools are created from GameObject > Level > <Category> > <Tool>: where it lands, its layer, its
// parent, Undo and selection. Selecting a bake group first puts the new object in its Editable Sources.
// Setup: call from a [MenuItem]. GC: only explicit menu clicks allocate. No subscriptions.
public static class Level_ToolCreation
{
    private const string PalettePath = "Assets/Art/Level/Default_Palette.asset";

    // Where the Scene view is looking, on the floor at this height
    public static Vector3 ScenePivot(float height = 0f)
    {
        Vector3 position = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        position.y = height;
        return position;
    }

    // onBuildingLayer: the layer the flow field bakes as an obstacle. Off for things goblins walk on or don't notice.
    public static GameObject Create(string label, MenuCommand command, bool onBuildingLayer = true)
    {
        var parent = command.context as GameObject;

        if (parent != null && parent.TryGetComponent(out Level_BakeGroup group))
        {
            if (group.Sources == null && !group.Generate()) throw new System.InvalidOperationException("Prepare the parent sources first.");
            group.SetEditing(true);
            parent = group.Sources;
        }

        var obj = new GameObject(label);
        GameObjectUtility.SetParentAndAlign(obj, parent);

        if (parent == null)
            obj.transform.position = ScenePivot();

        int layer = onBuildingLayer ? LayerMask.NameToLayer("Building") : -1;

        if (layer >= 0)
            obj.layer = layer;

        Undo.RegisterCreatedObjectUndo(obj, "Create " + label);
        Selection.activeGameObject = obj;
        return obj;
    }

    // A generating tool with the default look, its pieces made and shown ready to edit
    public static T CreateTool<T>(string label, MenuCommand command) where T : Level_Tool
    {
        T tool = Undo.AddComponent<T>(Create(label, command));
        tool.Configure(DefaultStyle(), DefaultPalette());
        tool.Generate();
        return tool;
    }

    public static Level_BlockStyle DefaultStyle() => Level_BlockEditor.DefaultStyle();

    public static Level_Palette DefaultPalette()
    {
        Level_Palette palette = AssetDatabase.LoadAssetAtPath<Level_Palette>(PalettePath);

        if (palette != null)
            return palette;

        Level_BlockStyle style = DefaultStyle();
        palette = ScriptableObject.CreateInstance<Level_Palette>();
        palette.material = style != null ? style.material : null;
        AssetDatabase.CreateAsset(palette, PalettePath);
        return palette;
    }
}
