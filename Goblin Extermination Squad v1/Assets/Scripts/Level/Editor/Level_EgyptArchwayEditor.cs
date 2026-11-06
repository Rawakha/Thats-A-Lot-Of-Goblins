using UnityEditor;
using UnityEngine;

// Handles and menu item for the Egyptian archway: white square moves it, yellow cone sets the clear height, orange cubes
// set the opening's width, pink cubes set its depth, and the blue ring turns it.
// GC: editor only (handles and inspector UI). No subscriptions.
[CustomEditor(typeof(Level_EgyptArchway))]
public class Level_EgyptArchwayEditor : Level_ToolEditor
{
    protected override string HelpText => "White square moves it, yellow cone sets the clear height under the lintel, orange cubes set the " +
        "opening's width, pink cubes its depth, the blue ring turns it.";

    protected override void DrawHandles()
    {
        var arch = (Level_EgyptArchway)target;
        Transform space = arch.transform;
        Level_BoxHandles.MoveObject(space, arch.Snap);

        Matrix4x4 matrix = space.localToWorldMatrix * Matrix4x4.Rotate(arch.Rotation);
        serializedObject.Update();
        bool changed = false;

        if (Level_BoxHandles.Reach(matrix, Vector3.zero, Vector3.up, arch.ClearHeight, Level_Handles.Height, true, out float height))
        {
            serializedObject.FindProperty("clearHeight").floatValue = Mathf.Max(0.5f, Level_Handles.Snap(height, arch.Snap));
            changed = true;
        }

        for (int sign = -1; sign <= 1; sign += 2)
        {
            if (Level_BoxHandles.Reach(matrix, Vector3.up * arch.ClearHeight * 0.5f, Vector3.right * sign, arch.ClearWidth * 0.5f,
                Level_Handles.Width, false, out float half))
            {
                serializedObject.FindProperty("clearWidth").floatValue = Mathf.Max(1f, Level_Handles.Snap(half * 2f, arch.Snap));
                changed = true;
            }

            if (Level_BoxHandles.Reach(matrix, Vector3.zero, Vector3.forward * sign, arch.Depth * 0.5f, Level_Handles.Length, false,
                out float reach))
            {
                serializedObject.FindProperty("depth").floatValue = Mathf.Max(0.3f, Level_Handles.Snap(reach * 2f, arch.Snap));
                changed = true;
            }
        }

        float ring = Mathf.Max(arch.ClearWidth * 0.6f + 1f, 2f);
        float newYaw = Level_Handles.TurnRing(space.position, space.up, arch.Yaw, ring);

        if (!Mathf.Approximately(newYaw, arch.Yaw))
        {
            serializedObject.FindProperty("yaw").floatValue = Mathf.Clamp(newYaw, -180f, 180f);
            changed = true;
        }

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            Changed();
        }
    }

    [MenuItem("GameObject/Level/Egyptian/Archway", false, 49)]
    private static void Create(MenuCommand command) => Level_EgyptLook.Create<Level_EgyptArchway>("Egyptian Archway", command);
}
