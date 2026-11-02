using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Inspector and Scene view editing for Level_Cliff.
//
// Inspector: under each section a small "Set Default" / "Reset" pair. Set Default saves that section as the default
// for new cliffs; Reset puts it back to the built-in defaults. The pair at the bottom does every section at once.
// (Saved defaults live in ProjectSettings/Level_CliffDefaults.json; see Level_CliffDefaults.)
//
// Scene view, with the cliff selected:
//   White squares: drag to move a node along the ground (snaps to the cliff's Snap grid).
//   Yellow cones: drag up or down for the cliff's height at that node.
//   Orange cubes (Line and Ring): drag sideways for the wall's width at that node.
//   Chunks: orange cubes set width, pink cubes set length, the blue ring turns it, the red ring leans it.
//   Green dots: click to add a node there (between two nodes, past the end of a Line, or beside the last chunk).
//   Hold Ctrl (Cmd on Mac): nodes turn into red dots; click one to delete it.
// The cliff rebuilds live while you drag, without its collider; the collider is rebuilt when you let go.
// GC: editor-only handle/Undo work; defaults allocate temporary objects on button clicks. No new subscriptions.
[CustomEditor(typeof(Level_Cliff))]
[CanEditMultipleObjects]
public class Level_CliffEditor : InspectorButtonEditor
{
    private static readonly Color OutlineColour = Level_Handles.Outline;
    private static readonly Color HeightColour = Level_Handles.Height;
    private static readonly Color WidthColour = Level_Handles.Width;
    private static readonly Color LengthColour = Level_Handles.Length;
    private static readonly Color TurnColour = new Color(0.35f, 0.6f, 1f);
    private static readonly Color LeanColour = new Color(1f, 0.3f, 0.3f);
    private static readonly Color AddColour = Level_Handles.Add;
    private static readonly Color RaiseColour = new Color(0.55f, 0.85f, 1f);
    private static readonly Color DeleteColour = Level_Handles.Delete;

    #region Inspector
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Scene view: drag white squares to move nodes, yellow cones for height, orange cubes for width " +
            "(Chunks: pink cubes for length, blue ring to turn, red ring to lean). Click green dots to add nodes. " +
            "Hold Ctrl and click a node to delete it.", MessageType.Info);

        serializedObject.Update();
        SerializedProperty property = serializedObject.GetIterator();
        string section = null;
        bool first = true;

        while (property.NextVisible(first))
        {
            first = false;

            if (property.name == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(property);

                continue;
            }

            string header = Level_CliffDefaults.SectionOf(property.name);

            if (header != null && header != section)
            {
                DrawSectionButtons(section);
                section = header;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        DrawSectionButtons(section);
        serializedObject.ApplyModifiedProperties();

        // Every section at once
        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Set All as Default", EditorStyles.miniButtonLeft, GUILayout.Width(120f)))
            SaveDefaults(null);

        if (GUILayout.Button("Reset All", EditorStyles.miniButtonRight, GUILayout.Width(80f)))
            ResetDefaults(null);

        EditorGUILayout.EndHorizontal();

        DrawInspectorButtons();
    }

    // The small Set Default / Reset pair under a section (none for Shape and Stats, which aren't settings)
    private void DrawSectionButtons(string section)
    {
        if (section == null || !Level_CliffDefaults.HasDefaults(section))
            return;

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Set Default", EditorStyles.miniButtonLeft, GUILayout.Width(80f)))
            SaveDefaults(section);

        if (GUILayout.Button("Reset", EditorStyles.miniButtonRight, GUILayout.Width(60f)))
            ResetDefaults(section);

        EditorGUILayout.EndHorizontal();
    }

    private void SaveDefaults(string section)
    {
        Level_CliffDefaults.Save((Level_Cliff)target, section);
    }

    private void ResetDefaults(string section)
    {
        foreach (Object each in targets)
        {
            Level_Cliff cliff = (Level_Cliff)each;
            Level_CliffDefaults.Reset(cliff, section);
            cliff.Build();
        }

        // The serialized object still holds the old values; reload it so this frame doesn't write them back
        serializedObject.Update();
        GUIUtility.ExitGUI();
    }
    #endregion

    #region Scene view
    private void OnSceneGUI()
    {
        if (Application.isPlaying)
            return;

        Level_Cliff cliff = (Level_Cliff)target;
        List<Level_CliffNode> nodes = cliff.Nodes;
        Transform transform = cliff.transform;
        Event current = Event.current;
        bool deleting = EditorGUI.actionKey;
        bool changed = false;
        int count = nodes.Count;

        // Ctrl doesn't repaint on its own, so the delete dots would only show once the mouse moved over a handle
        if (current.type == EventType.MouseMove || current.type == EventType.KeyDown || current.type == EventType.KeyUp)
            SceneView.RepaintAll();

        if (!cliff.IsChunks)
            DrawOutline(cliff, transform);

        if (!deleting && count > 0)
            changed = RaiseHandle(cliff, transform);

        for (int i = 0; i < count && !changed; i++)
        {
            Level_CliffNode node = nodes[i];
            Vector3 local = Flat(node.position);
            Vector3 world = transform.TransformPoint(local);
            float size = HandleUtility.GetHandleSize(world);

            if (deleting)
            {
                Handles.color = count > cliff.MinNodes ? DeleteColour : Color.grey;

                if (Handles.Button(world, Quaternion.identity, size * 0.08f, size * 0.12f, Handles.DotHandleCap)
                    && count > cliff.MinNodes)
                {
                    Undo.RecordObject(cliff, "Delete Cliff Node");
                    nodes.RemoveAt(i);
                    changed = true;
                }

                continue;
            }

            // Move along the ground
            Handles.color = Color.white;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Level_Handles.Move(world, transform);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(cliff, "Move Cliff Node");
                node.position = Snapped(Flat(transform.InverseTransformPoint(moved)), cliff.Snap);
                nodes[i] = node;
                changed = true;
                continue;
            }

            // Height: a cone on top of the node
            Vector3 top = transform.TransformPoint(local + Vector3.up * node.height);
            Handles.color = HeightColour;
            Handles.DrawDottedLine(world, top, 3f);
            EditorGUI.BeginChangeCheck();
            Vector3 raised = Handles.Slider(top, transform.up, size * 0.15f, Handles.ConeHandleCap, 0f);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(cliff, "Change Cliff Height");
                node.height = Mathf.Max(0.5f, Round(transform.InverseTransformPoint(raised).y, 0.25f));
                nodes[i] = node;
                changed = true;
                continue;
            }

            if (cliff.IsChunks)
            {
                changed = ChunkHandles(cliff, transform, i, size);
                continue;
            }

            // Width: a cube out to one side of the wall
            if (!cliff.UsesWidth)
                continue;

            Vector3 side = Side(nodes, i, cliff.IsClosed);
            float width = node.width;

            if (SizeHandle(cliff, transform, local, side, ref width, WidthColour, size, "Change Cliff Width"))
            {
                node.width = width;
                nodes[i] = node;
                changed = true;
            }
        }

        if (!deleting && !changed)
            changed = cliff.IsChunks ? DrawAddChunkButton(cliff, transform) : DrawAddButtons(cliff, transform);

        if (changed)
        {
            // Cooking the collider every drag frame is slow; leave it for the release
            cliff.Build(GUIUtility.hotControl == 0);
            EditorUtility.SetDirty(cliff);
            PrefabUtility.RecordPrefabInstancePropertyModifications(cliff);
        }

        if (cliff.ColliderStale && (current.rawType == EventType.MouseUp || GUIUtility.hotControl == 0))
            cliff.Build();
    }

    private void OnDisable()
    {
        if (target != null && !Application.isPlaying && ((Level_Cliff)target).ColliderStale)
            ((Level_Cliff)target).Build();
    }

    // A light blue arrow over the middle of the cliff: drag it to raise or lower every node by the same amount, so
    // their differences stay. It starts above the tallest node and above Unity's own move handle, so the two can't
    // be grabbed by mistake.
    private static bool RaiseHandle(Level_Cliff cliff, Transform transform)
    {
        List<Level_CliffNode> nodes = cliff.Nodes;
        Vector3 middle = Vector3.zero;
        float tallest = 0f;

        for (int i = 0; i < nodes.Count; i++)
        {
            middle += Flat(nodes[i].position);
            tallest = Mathf.Max(tallest, nodes[i].height);
        }

        middle /= nodes.Count;

        // Clear of the transform handle (about one handle size tall) and of the nodes' height cones
        float handleSize = HandleUtility.GetHandleSize(transform.position);
        Vector3 pivot = transform.InverseTransformPoint(transform.position);
        float above = Mathf.Max(tallest + handleSize * 0.5f, pivot.y + handleSize * 1.4f);
        Vector3 start = transform.TransformPoint(middle + Vector3.up * above);
        float size = HandleUtility.GetHandleSize(start);

        Handles.color = RaiseColour;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider(start, transform.up, size * 0.9f, Handles.ArrowHandleCap, 0f);

        if (!EditorGUI.EndChangeCheck())
            return false;

        float change = transform.InverseTransformPoint(moved).y - above;

        if (Mathf.Abs(change) < 1e-4f)
            return false;

        Undo.RecordObject(cliff, "Raise Cliff");

        for (int i = 0; i < nodes.Count; i++)
        {
            Level_CliffNode node = nodes[i];
            node.height = Mathf.Max(0.5f, node.height + change);
            nodes[i] = node;
        }

        return true;
    }

    // A chunk's width (orange) and length (pink) cubes, turn ring (blue) and lean ring (red)
    private static bool ChunkHandles(Level_Cliff cliff, Transform transform, int i, float size)
    {
        List<Level_CliffNode> nodes = cliff.Nodes;
        Level_CliffNode node = nodes[i];
        Vector3 local = Flat(node.position);
        Quaternion facing = Quaternion.Euler(0f, node.turn, 0f);
        Vector3 right = facing * Vector3.right;
        Vector3 forward = facing * Vector3.forward;
        float width = node.width;
        float length = Mathf.Max(node.length, 0.5f);

        if (SizeHandle(cliff, transform, local, right, ref width, WidthColour, size, "Change Chunk Width"))
        {
            node.width = width;
            nodes[i] = node;
            return true;
        }

        if (SizeHandle(cliff, transform, local, forward, ref length, LengthColour, size, "Change Chunk Length"))
        {
            node.length = length;
            nodes[i] = node;
            return true;
        }

        Vector3 middle = transform.TransformPoint(local + Vector3.up * (node.height * 0.5f));
        float radius = Mathf.Max(width, length) * 0.6f;

        // Turn: a flat ring round the chunk
        Handles.color = TurnColour;
        EditorGUI.BeginChangeCheck();
        Quaternion turned = Handles.Disc(transform.rotation * facing, transform.TransformPoint(local), transform.up,
            radius * transform.lossyScale.x, false, 5f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(cliff, "Turn Chunk");
            node.turn = Mathf.Round((Quaternion.Inverse(transform.rotation) * turned).eulerAngles.y);
            nodes[i] = node;
            return true;
        }

        // Lean: a ring round the chunk's left-right axis, through its middle
        Handles.color = LeanColour;
        Quaternion leaning = transform.rotation * facing * Quaternion.Euler(node.lean, 0f, 0f);
        EditorGUI.BeginChangeCheck();
        Quaternion leaned = Handles.Disc(leaning, middle, transform.TransformDirection(right),
            radius * 0.7f * transform.lossyScale.x, false, 1f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(cliff, "Lean Chunk");
            Quaternion relative = Quaternion.Inverse(transform.rotation * facing) * leaned;
            float angle = relative.eulerAngles.x;
            node.lean = Mathf.Clamp(Mathf.Round(angle > 180f ? angle - 360f : angle), -60f, 60f);
            nodes[i] = node;
            return true;
        }

        return false;
    }

    // A cube out along a direction at half the size; dragging it sets the size (snapped to quarter metres)
    private static bool SizeHandle(Level_Cliff cliff, Transform transform, Vector3 local, Vector3 direction,
        ref float value, Color colour, float size, string undo)
    {
        Vector3 edge = transform.TransformPoint(local + direction * (value * 0.5f));
        Handles.color = colour;
        Handles.DrawLine(transform.TransformPoint(local - direction * (value * 0.5f)), edge);
        EditorGUI.BeginChangeCheck();
        Vector3 pulled = Handles.Slider(edge, transform.TransformDirection(direction), size * 0.08f, Handles.CubeHandleCap, 0f);

        if (!EditorGUI.EndChangeCheck())
            return false;

        Undo.RecordObject(cliff, undo);
        float half = Vector3.Dot(transform.InverseTransformPoint(pulled) - local, direction);
        value = Mathf.Max(0.5f, Round(half * 2f, 0.25f));
        return true;
    }

    private static void DrawOutline(Level_Cliff cliff, Transform transform)
    {
        List<Level_CliffNode> nodes = cliff.Nodes;
        int segments = cliff.IsClosed ? nodes.Count : nodes.Count - 1;
        Handles.color = OutlineColour;

        for (int i = 0; i < segments; i++)
        {
            Vector3 a = transform.TransformPoint(Flat(nodes[i].position));
            Vector3 b = transform.TransformPoint(Flat(nodes[(i + 1) % nodes.Count].position));
            Handles.DrawAAPolyLine(3f, a, b);
        }
    }

    // Green dots halfway along each segment, and past each end of a Line; clicking one adds a node there
    private static bool DrawAddButtons(Level_Cliff cliff, Transform transform)
    {
        List<Level_CliffNode> nodes = cliff.Nodes;
        int count = nodes.Count;

        if (count == 0)
            return false;

        Handles.color = AddColour;
        int segments = cliff.IsClosed ? count : count - 1;

        for (int i = 0; i < segments; i++)
        {
            Level_CliffNode a = nodes[i];
            Level_CliffNode b = nodes[(i + 1) % count];
            Vector3 middle = (Flat(a.position) + Flat(b.position)) * 0.5f;

            if (AddButton(transform.TransformPoint(middle)))
            {
                Undo.RecordObject(cliff, "Add Cliff Node");
                nodes.Insert(i + 1, new Level_CliffNode(middle, (a.height + b.height) * 0.5f, (a.width + b.width) * 0.5f));
                return true;
            }
        }

        if (cliff.IsClosed || count < 2)
            return false;

        // Past either end, carrying on the way the line goes
        for (int end = 0; end < 2; end++)
        {
            Level_CliffNode last = end == 0 ? nodes[0] : nodes[count - 1];
            Level_CliffNode before = end == 0 ? nodes[1] : nodes[count - 2];
            Vector3 direction = Flat(last.position) - Flat(before.position);
            Vector3 beyond = Flat(last.position) + (direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.right) * 6f;

            if (AddButton(transform.TransformPoint(beyond)))
            {
                Undo.RecordObject(cliff, "Add Cliff Node");
                nodes.Insert(end == 0 ? 0 : count, new Level_CliffNode(Snapped(beyond, cliff.Snap), last.height, last.width));
                return true;
            }
        }

        return false;
    }

    // Chunks: one green dot beside the last chunk; clicking it adds a new chunk there, a bit different in size
    private static bool DrawAddChunkButton(Level_Cliff cliff, Transform transform)
    {
        List<Level_CliffNode> nodes = cliff.Nodes;
        Level_CliffNode last = nodes.Count > 0 ? nodes[nodes.Count - 1] : new Level_CliffNode(Vector3.zero, 4f, 3f);
        Vector3 right = Quaternion.Euler(0f, last.turn, 0f) * Vector3.right;
        Vector3 beside = Flat(last.position) + right * (last.width * 0.5f + 2f);
        Handles.color = AddColour;

        if (nodes.Count > 0 && !AddButton(transform.TransformPoint(beside)))
            return false;

        Undo.RecordObject(cliff, "Add Chunk");
        float height = Mathf.Max(1f, last.height * Random.Range(0.6f, 1.2f));
        float width = Mathf.Max(1f, last.width * Random.Range(0.7f, 1.2f));
        float length = Mathf.Max(1f, Mathf.Max(last.length, 0.5f) * Random.Range(0.7f, 1.2f));
        nodes.Add(new Level_CliffNode(Snapped(beside + right * (width * 0.5f - 2f), cliff.Snap), height, width, length,
            last.turn + Random.Range(-10f, 10f), 0f));
        return true;
    }

    private static bool AddButton(Vector3 world)
    {
        float size = HandleUtility.GetHandleSize(world) * 0.06f;
        return Handles.Button(world, Quaternion.identity, size, size * 1.5f, Handles.DotHandleCap);
    }

    // Square to the wall at a node, in the cliff's space: the way its width spreads
    private static Vector3 Side(List<Level_CliffNode> nodes, int i, bool closed)
    {
        int count = nodes.Count;
        int previous = closed ? (i - 1 + count) % count : Mathf.Max(i - 1, 0);
        int next = closed ? (i + 1) % count : Mathf.Min(i + 1, count - 1);
        Vector3 along = Flat(nodes[next].position) - Flat(nodes[previous].position);

        if (along.sqrMagnitude < 1e-6f)
            return Vector3.right;

        along.Normalize();
        return new Vector3(along.z, 0f, -along.x);
    }

    private static Vector3 Flat(Vector3 position) => new Vector3(position.x, 0f, position.z);

    private static Vector3 Snapped(Vector3 position, float grid)
    {
        return Level_Handles.SnapPlanar(position, grid);
    }

    private static float Round(float value, float step) => Mathf.Round(value / step) * step;
    #endregion
}

// Saved defaults for Level_Cliff, a section (inspector header) at a time. Built-in defaults are the field values in
// Level_Cliff's code; saved ones are kept in ProjectSettings/Level_CliffDefaults.json (shared with the project) and
// given to every new cliff the GameObject > Level menu makes. Shape, nodes, the seed and the stats are never
// defaults: they belong to one cliff.
public static class Level_CliffDefaults
{
    private const string SavePath = "ProjectSettings/Level_CliffDefaults.json";
    private static readonly HashSet<string> Never = new HashSet<string> { "seed" };
    private static readonly HashSet<string> NoDefaults = new HashSet<string> { "Shape", "Stats" };

    private static Dictionary<string, string> sections;

    // The header a field sits under (null for hidden fields)
    public static string SectionOf(string field)
    {
        Build();
        return sections.TryGetValue(field, out string section) ? section : null;
    }

    public static bool HasDefaults(string section) => !NoDefaults.Contains(section);

    // Saves the cliff's settings for one section (or all, for null) as the defaults, keeping the other sections' saved ones
    public static void Save(Level_Cliff cliff, string section)
    {
        WithTemporary(true, defaults =>
        {
            Copy(cliff, defaults, section, false);
            System.IO.File.WriteAllText(SavePath, EditorJsonUtility.ToJson(defaults, true));
        });

        Debug.Log($"Level_Cliff: saved {(section ?? "all settings")} as the default for new cliffs.");
    }

    // Puts one section (or all, for null) back to the built-in defaults
    public static void Reset(Level_Cliff cliff, string section)
    {
        WithTemporary(false, defaults => Copy(defaults, cliff, section, true));
    }

    // Gives a new cliff the saved defaults, if any were saved
    public static void ApplySaved(Level_Cliff cliff)
    {
        if (System.IO.File.Exists(SavePath))
            WithTemporary(true, defaults => Copy(defaults, cliff, null, false));
    }

    private static void Copy(Level_Cliff from, Level_Cliff to, string section, bool undoable)
    {
        Build();
        SerializedObject source = new SerializedObject(from);
        SerializedObject destination = new SerializedObject(to);

        foreach (KeyValuePair<string, string> field in sections)
        {
            if (Never.Contains(field.Key) || !HasDefaults(field.Value) || (section != null && field.Value != section))
                continue;

            SerializedProperty property = source.FindProperty(field.Key);

            if (property != null)
                destination.CopyFromSerializedProperty(property);
        }

        if (undoable)
            destination.ApplyModifiedProperties();
        else
            destination.ApplyModifiedPropertiesWithoutUndo();
    }

    // A throwaway cliff holding the built-in defaults (or the saved ones, if asked and there are any)
    private static void WithTemporary(bool saved, System.Action<Level_Cliff> use)
    {
        GameObject holder = new GameObject("Cliff Defaults") { hideFlags = HideFlags.HideAndDontSave };

        try
        {
            Level_Cliff defaults = holder.AddComponent<Level_Cliff>();

            if (saved && System.IO.File.Exists(SavePath))
                EditorJsonUtility.FromJsonOverwrite(System.IO.File.ReadAllText(SavePath), defaults);

            use(defaults);
        }
        finally
        {
            Object.DestroyImmediate(holder);
        }
    }

    // Every serialized field in declaration order, under the last header above it
    private static void Build()
    {
        if (sections != null)
            return;

        sections = new Dictionary<string, string>();
        string current = null;

        foreach (FieldInfo field in typeof(Level_Cliff).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (field.GetCustomAttribute<HideInInspector>() != null)
                continue;

            if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)
                continue;

            HeaderAttribute header = field.GetCustomAttribute<HeaderAttribute>();

            if (header != null)
                current = header.header;

            if (current != null)
                sections[field.Name] = current;
        }
    }
}
