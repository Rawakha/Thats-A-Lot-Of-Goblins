using UnityEditor;
using UnityEngine;

// The editors and menu items for the three example tools, one of each kind. Copy the one that matches your tool.
// GC: editor only (handles and inspector UI). No subscriptions.
[CustomEditor(typeof(Level_ExamplePiece))]
public class Level_ExamplePieceEditor : Level_ToolEditor
{
    protected override string HelpText => "White square moves it; orange, pink and yellow handles set width, depth and height.";

    protected override void DrawHandles()
    {
        Level_BoxHandles.MoveObject(Tool.transform, Tool.Snap);
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty("size");
        Vector3 size = property.vector3Value;

        if (Level_BoxHandles.DrawSize(Tool.transform, ref size, Tool.Snap))
        {
            property.vector3Value = size;
            serializedObject.ApplyModifiedProperties();
            Changed();
        }
    }

    [MenuItem("GameObject/Level/Common/Examples/Piece", false, 60)]
    private static void Create(MenuCommand command) => Level_ToolCreation.CreateTool<Level_ExamplePiece>("Example Piece", command);
}

[CustomEditor(typeof(Level_ExamplePath))]
public class Level_ExamplePathEditor : Level_ToolEditor
{
    protected override string HelpText => $"{Level_ToolInspector.NodeHelp} Yellow cones set height, orange cubes set thickness.";

    protected override void DrawHandles()
    {
        var path = (Level_ExamplePath)target;
        Level_PathHandles.Draw(path, path.Nodes, false, path.Height, path.Thickness, path.Snap,
            (a, b, position) => new Level_PathNode
            {
                position = position,
                height = (a.height + b.height) * 0.5f,
                thickness = (a.thickness + b.thickness) * 0.5f
            });
    }

    [MenuItem("GameObject/Level/Common/Examples/Path", false, 61)]
    private static void Create(MenuCommand command) => Level_ToolCreation.CreateTool<Level_ExamplePath>("Example Path", command);
}

[CustomEditor(typeof(Level_ExampleOutline))]
public class Level_ExampleOutlineEditor : Level_ToolEditor
{
    protected override string HelpText => $"{Level_ToolInspector.NodeHelp} An outline can't be dragged into crossing itself.";

    protected override void DrawHandles()
    {
        var outline = (Level_ExampleOutline)target;

        if (Level_OutlineHandles.Draw(outline, outline.transform, outline.Nodes, outline.Snap))
            Changed();
    }

    [MenuItem("GameObject/Level/Common/Examples/Outline", false, 62)]
    private static void Create(MenuCommand command) => Level_ToolCreation.CreateTool<Level_ExampleOutline>("Example Outline", command);
}
