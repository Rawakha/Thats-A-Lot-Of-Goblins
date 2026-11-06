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
// Scene view: the shared node handles (Level_NodeHandles), with a yellow height cone on every node, an orange width
// cube on every node of a Line, and a light blue arrow that raises the whole cliff. A Block won't let you drag its
// outline into crossing itself. The cliff rebuilds live while you drag, without its collider; the collider is rebuilt
// when you let go.
// GC: editor-only handle/Undo work; defaults allocate temporary objects on button clicks. No new subscriptions.
[CustomEditor(typeof(Level_Cliff))]
[CanEditMultipleObjects]
public class Level_CliffEditor : InspectorButtonEditor
{
    private static readonly Color RaiseColour = new Color(0.55f, 0.85f, 1f);

    private sealed class CliffNodes : Level_NodeList
    {
        private readonly Level_Cliff cliff;
        private List<Level_CliffNode> Nodes => cliff.Nodes;

        public CliffNodes(Level_Cliff cliff)
        {
            this.cliff = cliff;
            Owner = cliff;
            Space = cliff.transform;
            Closed = cliff.IsClosed;
            Minimum = cliff.MinNodes;
            Snap = cliff.Snap;
            ExtendDistance = 6f;
            ExtendStart = true;
            RejectCrossing = cliff.IsClosed;
        }

        public override int Count => Nodes.Count;
        public override Vector3 Get(int index) => Level_Handles.Flat(Nodes[index].position);
        public override void RemoveAt(int index) => Nodes.RemoveAt(index);

        public override void Set(int index, Vector3 position)
        {
            Level_CliffNode node = Nodes[index];
            node.position = position;
            Nodes[index] = node;
        }

        public override void Insert(int index, Vector3 position, int from, int to)
        {
            Level_CliffNode a = Nodes[from];
            Level_CliffNode b = Nodes[to];
            Nodes.Insert(index, new Level_CliffNode(position, (a.height + b.height) * 0.5f, (a.width + b.width) * 0.5f));
        }

        public override bool DrawNodeHandles(int index, Vector3 local)
        {
            Level_CliffNode node = Nodes[index];
            Matrix4x4 matrix = Space.localToWorldMatrix;

            if (Level_BoxHandles.Reach(matrix, local, Vector3.up, node.height, Level_Handles.Height, true, out float height))
            {
                Undo.RecordObject(cliff, "Change Cliff Height");
                node.height = Mathf.Max(0.5f, Round(height, 0.25f));
                Nodes[index] = node;
                return true;
            }

            if (!cliff.UsesWidth)
                return false;

            Vector3 side = Side(Nodes, index, Closed);
            Handles.color = Level_Handles.Width;
            Handles.DrawLine(Space.TransformPoint(local - side * (node.width * 0.5f)), Space.TransformPoint(local + side * (node.width * 0.5f)));

            if (Level_BoxHandles.Reach(matrix, local, side, node.width * 0.5f, Level_Handles.Width, false, out float half))
            {
                Undo.RecordObject(cliff, "Change Cliff Width");
                node.width = Mathf.Max(0.5f, Round(half * 2f, 0.25f));
                Nodes[index] = node;
                return true;
            }

            return false;
        }
    }

    #region Inspector
    public override void OnInspectorGUI()
    {
        Level_ToolInspector.Help($"Scene view: {Level_ToolInspector.NodeHelp} Yellow cones set height, orange cubes set width (Line), " +
            "the blue arrow raises the whole cliff.");

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
        Transform transform = cliff.transform;
        Event current = Event.current;
        bool changed = false;

        if (!EditorGUI.actionKey && cliff.Nodes.Count > 0)
            changed = RaiseHandle(cliff, transform);

        if (!changed)
            changed = Level_NodeHandles.Draw(new CliffNodes(cliff));

        Level_ToolFeedback.Built(transform);

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

    private static Vector3 Flat(Vector3 position) => Level_Handles.Flat(position);

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
