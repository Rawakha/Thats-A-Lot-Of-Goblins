using UnityEditor;
using UnityEngine;

// Handles, menu item and wall placement for the Egyptian pillar. White square moves it, yellow cone sets height, orange
// cubes set width, and two blue rings set Turn (flat ring at the base) and Roll (upright ring at mid-height).
// GC: editor only (handles and inspector UI). One delayed call per placed pillar. No subscriptions.
[CustomEditor(typeof(Level_EgyptPillar))]
public class Level_EgyptPillarEditor : Level_ToolEditor
{
    protected override string HelpText => "White square moves it, yellow cone sets height, orange cubes set width. The flat blue ring turns it, " +
        "the upright blue ring rolls (tips) it over from its base.";

    protected override void DrawHandles()
    {
        var pillar = (Level_EgyptPillar)target;
        Transform space = pillar.transform;

        // Moving doesn't change the pillar's own settings, so the wall it stands in has to be told
        if (Level_BoxHandles.MoveObject(space, pillar.Snap))
            pillar.NotifyWall();

        Quaternion rotation = pillar.Rotation;
        Matrix4x4 matrix = space.localToWorldMatrix * Matrix4x4.Rotate(rotation);
        serializedObject.Update();
        bool changed = false;

        if (Level_BoxHandles.Reach(matrix, Vector3.zero, Vector3.up, pillar.Height, Level_Handles.Height, true, out float height))
        {
            serializedObject.FindProperty("height").floatValue = Mathf.Max(1f, Level_Handles.Snap(height, pillar.Snap));
            changed = true;
        }

        for (int sign = -1; sign <= 1; sign += 2)
        {
            if (Level_BoxHandles.Reach(matrix, Vector3.up * pillar.Height * 0.5f, Vector3.right * sign, pillar.Width * 0.5f,
                Level_Handles.Width, false, out float half))
            {
                serializedObject.FindProperty("width").floatValue = Mathf.Max(0.4f, Level_Handles.Snap(half * 2f, pillar.Snap));
                changed = true;
            }
        }

        // Turn: a flat ring round the base. Roll: an upright ring through the pillar's middle, round its forward axis.
        Vector3 origin = space.position;
        float ring = Mathf.Max(pillar.Width * 1.1f, 1.2f);
        Vector3 yawAxis = space.up;
        float newYaw = Level_Handles.TurnRing(origin, yawAxis, pillar.Yaw, ring);

        if (!Mathf.Approximately(newYaw, pillar.Yaw))
        {
            serializedObject.FindProperty("yaw").floatValue = Mathf.Clamp(newYaw, -180f, 180f);
            changed = true;
        }

        Vector3 middle = origin + space.TransformDirection(rotation * Vector3.up) * pillar.Height * 0.5f;
        Vector3 rollAxis = space.TransformDirection(Quaternion.Euler(0f, pillar.Yaw, 0f) * Vector3.forward);
        float newRoll = Level_Handles.TurnRing(middle, rollAxis, pillar.Roll, ring);

        if (!Mathf.Approximately(newRoll, pillar.Roll))
        {
            serializedObject.FindProperty("roll").floatValue = Mathf.Clamp(newRoll, -90f, 90f);
            changed = true;
        }

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            Changed();
        }
    }

    [MenuItem("GameObject/Level/Egyptian/Pillar", false, 48)]
    private static void Create(MenuCommand command) => Level_EgyptLook.Create<Level_EgyptPillar>("Egyptian Pillar", command);

    // Stands a pillar on a wall spot, facing along the wall and a little taller than it. It goes in the wall's Editable
    // Sources so it bakes with the wall; the wall stays selected so the next spot can be clicked straight away.
    public static void PlaceOnWall(Level_EgyptWall wall, Level_EgyptSpots.Spot spot)
    {
        GameObject obj = Level_ToolCreation.Create("Egyptian Pillar", new MenuCommand(wall.gameObject));
        Level_EgyptPillar pillar = Undo.AddComponent<Level_EgyptPillar>(obj);
        obj.transform.position = wall.transform.TransformPoint(spot.position);
        pillar.Configure(Level_EgyptLook.Style(), Level_EgyptLook.Palette());
        pillar.Setup(spot.height + 1.4f, Mathf.Max(1.6f, spot.thickness * 1.25f),
            Mathf.Atan2(spot.direction.x, spot.direction.z) * Mathf.Rad2Deg);
        pillar.Generate(); // also tells the wall, which cuts itself round the new pillar
        Selection.activeGameObject = wall.gameObject;
    }
}
