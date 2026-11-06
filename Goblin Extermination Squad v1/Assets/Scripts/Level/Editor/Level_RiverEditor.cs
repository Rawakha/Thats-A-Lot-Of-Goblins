using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Inspector and Scene view editing for Level_River, plus its menu item. Scene view: the shared node handles (white squares
// move, green dots add or extend, Ctrl/Cmd-click deletes) with an orange cube on every node that sets the river's width
// there, and a live cyan outline of the real rim, so you see the ragged edge as you drag. The banks and water rebuild while
// you drag; the colliders and the ground's cut-out wait for the release.
// GC: editor only (handles, Undo, a rim list per repaint). No subscriptions.
[CustomEditor(typeof(Level_River))]
public class Level_RiverEditor : InspectorButtonEditor
{
    private bool dragging;

    private sealed class RiverNodes : Level_NodeList
    {
        private readonly Level_River river;
        private List<Level_RiverNode> Nodes => river.Nodes;

        public RiverNodes(Level_River river)
        {
            this.river = river;
            Owner = river;
            Space = river.transform;
            Minimum = 2;
            Maximum = 64;
            Snap = river.Snap;
            ExtendDistance = 8f;
            ExtendStart = true;
        }

        public override int Count => Nodes.Count;
        public override Vector3 Get(int index) => Level_Handles.Flat(Nodes[index].position);
        public override void Set(int index, Vector3 position) => Nodes[index].position = position;
        public override void RemoveAt(int index) => Nodes.RemoveAt(index);

        public override void Insert(int index, Vector3 position, int from, int to)
        {
            Nodes.Insert(index, new Level_RiverNode { position = position, width = (Nodes[from].width + Nodes[to].width) * 0.5f });
        }

        // An orange cube each side of the node, so the river's width there is the gap between them
        public override bool DrawNodeHandles(int index, Vector3 local)
        {
            Vector3 direction = index + 1 < Nodes.Count ? Get(index + 1) - local : index > 0 ? local - Get(index - 1) : Vector3.right;
            Vector3 side = new Vector3(-direction.z, 0f, direction.x).normalized;

            if (side.sqrMagnitude < 0.01f)
                side = Vector3.forward;

            Level_RiverNode node = Nodes[index];
            Matrix4x4 matrix = Space.localToWorldMatrix;
            float half = river.Width * node.width * 0.5f;

            for (int sign = -1; sign <= 1; sign += 2)
            {
                if (Level_BoxHandles.Reach(matrix, local, side * sign, half, Level_Handles.Width, false, out float reached))
                {
                    Undo.RecordObject(river, "Set River Width");
                    node.width = Mathf.Clamp(Level_Handles.Snap(reached * 2f, Snap) / Mathf.Max(1f, river.Width), 0.3f, 3f);
                    return true;
                }
            }

            return false;
        }
    }

    public override void OnInspectorGUI()
    {
        var river = (Level_River)target;
        Level_ToolInspector.Help($"{Level_ToolInspector.NodeHelp} Orange cubes set the river's width at a node. The ground is cut " +
            "out round it, so keep it well inside the ground and clear of holes. Rebake the flow obstacles after changing a river. " +
            "Replace the Water material with your shader.");

        string problem = river.Problem();

        if (problem != null)
            EditorGUILayout.HelpBox(problem, MessageType.Error);

        base.OnInspectorGUI();
    }

    private void OnSceneGUI()
    {
        if (Application.isPlaying)
            return;

        var river = (Level_River)target;

        // The real outline, live
        List<Vector2> rim = new List<Vector2>();

        if (river.TryGetRim(river.transform, rim, out _, false) && Event.current.type == EventType.Repaint)
        {
            Vector3[] line = new Vector3[rim.Count + 1];

            for (int i = 0; i < rim.Count; i++)
                line[i] = river.transform.TransformPoint(new Vector3(rim[i].x, 0f, rim[i].y));

            line[rim.Count] = line[0];
            Handles.color = Level_Handles.Outline;
            Handles.DrawAAPolyLine(Level_Handles.LineWidth, line);
        }

        bool changed = Level_NodeHandles.Draw(new RiverNodes(river));
        Level_ToolFeedback.Built(river.transform);

        if (changed)
        {
            dragging = true;

            // Colliders cook slowly: leave them for the release
            river.Build(GUIUtility.hotControl == 0);
            EditorUtility.SetDirty(river);
        }

        if (dragging && (Event.current.rawType == EventType.MouseUp || GUIUtility.hotControl == 0))
        {
            dragging = false;

            if (river.ColliderStale)
                river.Build();

            Level_Ground.RequestRebuild();
        }
    }

    private void OnDisable()
    {
        if (target != null && !Application.isPlaying && ((Level_River)target).ColliderStale)
        {
            ((Level_River)target).Build();
            Level_Ground.RequestRebuild();
        }
    }

    private const string WaterMaterialPath = "Assets/Art/Level/River_Water_Mat.mat";

    // A plain material on the pipeline's default shader, for you to swap for your water shader
    private static Material WaterMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);

        if (material != null)
            return material;

        Material standard = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null
            ? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.defaultMaterial
            : null;

        if (standard == null)
            return null;

        material = new Material(standard) { name = "River_Water_Mat" };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", new Color(0.25f, 0.55f, 0.75f));

        AssetDatabase.CreateAsset(material, WaterMaterialPath);
        return material;
    }

    // On the ground's height where the Scene view is looking. Not on the Building layer: its banks and blocker set their own.
    [MenuItem("GameObject/Level/Terrain/River", false, 34)]
    private static void Create(MenuCommand command)
    {
        GameObject obj = Level_ToolCreation.Create("River", command, false);
        obj.transform.position = Level_ToolCreation.ScenePivot();
        var river = Undo.AddComponent<Level_River>(obj);

        SerializedObject serialized = new SerializedObject(river);
        serialized.FindProperty("bankMaterial").objectReferenceValue = Level_River.LoadBankMaterial();
        serialized.FindProperty("waterMaterial").objectReferenceValue = WaterMaterial();
        serialized.FindProperty("seed").intValue = Random.Range(1, 100000);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        river.Build();
        Level_Ground.RequestRebuild();
    }
}
