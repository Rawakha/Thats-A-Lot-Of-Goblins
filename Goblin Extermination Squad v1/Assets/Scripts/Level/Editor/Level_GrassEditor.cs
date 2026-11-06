using UnityEditor;
using UnityEngine;

// Scene view painting for Level_Grass. Press Paint Grass in the inspector (or P with the grass selected), then:
//   Left drag: paint grass where the brush touches the ground or a cliff top (it fills up to the Density).
//   Shift + left drag: erase.
//   [ and ]: smaller / bigger brush.
// The brush circle follows the surface under the mouse: green paints, red erases. Steep surfaces (cliff sides) are
// skipped by the Flatness setting. Every stroke is one Undo step.
// GC: editor-only (raycasts, chunk rebuilds after each stroke). No runtime cost.
[CustomEditor(typeof(Level_Grass))]
public class Level_GrassEditor : InspectorButtonEditor
{
    private static readonly Color PaintColour = new Color(0.35f, 1f, 0.4f, 0.9f);
    private static readonly Color EraseColour = new Color(1f, 0.3f, 0.25f, 0.9f);

    private static bool painting;
    private readonly System.Random random = new System.Random();
    private Vector3 lastStroke;
    private bool stroking;

    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndo;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndo;

        // Deselected while painting: give the move tools back
        if (painting)
        {
            painting = false;
            Tools.hidden = false;
        }
    }

    // Undo restores the stored clumps; reload them into what's drawn
    private void OnUndo()
    {
        if (target is Level_Grass grass)
            grass.Reload();
    }

    public override void OnInspectorGUI()
    {
        Level_Grass grass = (Level_Grass)target;

        EditorGUILayout.HelpBox(
            "Paint Grass, then in the Scene view: left drag paints, Shift + drag erases, [ and ] resize the brush. " +
            "Colours and wind are on the material.", MessageType.Info);

        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = painting ? PaintColour : previous;

        if (GUILayout.Button(painting ? "Painting (click to stop)" : "Paint Grass (P)", GUILayout.Height(28f)))
            SetPainting(!painting);

        GUI.backgroundColor = previous;
        EditorGUILayout.LabelField($"Clumps: {grass.ClumpCount} / {grass.MaxClumps}");

        base.OnInspectorGUI();
    }

    private void SetPainting(bool on)
    {
        painting = on;
        Tools.hidden = on;  // hide the move gizmo while painting, so clicks go to the brush
        SceneView.RepaintAll();
    }

    private void OnSceneGUI()
    {
        if (Application.isPlaying)
            return;

        Level_Grass grass = (Level_Grass)target;
        Event current = Event.current;

        if (current.type == EventType.KeyDown && current.keyCode == KeyCode.P && GUIUtility.hotControl == 0)
        {
            SetPainting(!painting);
            current.Use();
        }

        if (!painting)
        {
            Tools.hidden = false;
            return;
        }

        // Take the mouse so clicks paint instead of selecting things
        int control = GUIUtility.GetControlID(FocusType.Passive);

        if (current.type == EventType.Layout)
            HandleUtility.AddDefaultControl(control);

        if (current.type == EventType.KeyDown && (current.keyCode == KeyCode.LeftBracket || current.keyCode == KeyCode.RightBracket))
        {
            Undo.RecordObject(grass, "Grass Brush Size");
            grass.BrushRadius *= current.keyCode == KeyCode.LeftBracket ? 0.85f : 1.15f;
            current.Use();
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, 5000f, grass.PaintLayers, QueryTriggerInteraction.Ignore))
            return;

        bool erasing = current.shift;
        float radius = grass.BrushRadius;

        Handles.color = erasing ? EraseColour : PaintColour;
        Handles.DrawWireDisc(hit.point, hit.normal, radius, 2f);
        Handles.DrawWireDisc(hit.point, hit.normal, radius * 0.05f, 2f);

        if (current.type == EventType.MouseMove)
            SceneView.RepaintAll();

        if (current.alt || current.button != 0)
            return;

        switch (current.type)
        {
            case EventType.MouseDown:
                Undo.RecordObject(grass, erasing ? "Erase Grass" : "Paint Grass");
                stroking = true;
                Stroke(grass, hit.point, erasing);
                lastStroke = hit.point;
                current.Use();
                break;

            case EventType.MouseDrag when stroking:
                // A dab each third of a brush moved, so slow drags don't pile it on
                if ((hit.point - lastStroke).sqrMagnitude >= radius * radius * 0.1f)
                {
                    Stroke(grass, hit.point, erasing);
                    lastStroke = hit.point;
                }

                current.Use();
                break;

            case EventType.MouseUp when stroking:
                stroking = false;
                current.Use();
                break;
        }
    }

    // One dab: erase inside the brush, or fill it up to the density, dropping each clump onto the surface below
    private void Stroke(Level_Grass grass, Vector3 centre, bool erasing)
    {
        float radius = grass.BrushRadius;

        if (erasing)
        {
            grass.RemoveWithin(centre, radius, 0.6f, random);
            grass.Commit();
            EditorUtility.SetDirty(grass);
            return;
        }

        int wanted = Mathf.CeilToInt(grass.Density * Mathf.PI * radius * radius);
        int need = Mathf.Min(wanted - grass.CountWithin(centre, radius), 400);
        int added = 0;
        float drop = radius + 3f;

        for (int tries = 0; tries < need * 3 && added < need; tries++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
            Vector3 above = centre + new Vector3(Mathf.Cos(angle) * distance, drop, Mathf.Sin(angle) * distance);

            if (!Physics.Raycast(above, Vector3.down, out RaycastHit hit, drop * 2f, grass.PaintLayers, QueryTriggerInteraction.Ignore))
                continue;

            // Too steep (a cliff side): no grass
            if (hit.normal.y < grass.Flatness)
                continue;

            if (!grass.AddClump(hit.point, random))
                break;

            added++;
        }

        if (added > 0)
        {
            grass.Commit();
            EditorUtility.SetDirty(grass);
        }
    }
}
