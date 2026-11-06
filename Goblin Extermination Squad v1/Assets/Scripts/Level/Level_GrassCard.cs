using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Editable source GameObject for Level_GrassCover. Real flat mesh cards, never boxes. The cover copies the
// authored mesh, material and relative transform into its saved chunks. No runtime ticks or subscriptions.
// Mesh/list allocations happen only when creating or editing the source in the editor.
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Level_GrassCard : MonoBehaviour
{
    public enum Shape { Grass, Flower }
    [SerializeField] private Shape shape;
    public Shape CardShape => shape;
    [SerializeField, Min(0.005f)] private float width = 0.055f;
    [SerializeField, Min(0.01f)] private float height = 0.3f;
    [Tooltip("Width of the flower's square-petalled head.")]
    [SerializeField, Min(0.01f)] private float flowerSize = 0.12f;
    [Header("Optional Grass Shape")]
    [Tooltip("One keeps a rectangle; zero makes a pointed tip.")]
    [SerializeField, Range(0, 1)] private float topWidth = 1f;
    [Tooltip("Sideways lean at the tip, as a fraction of height.")]
    [SerializeField, Range(-0.5f, 0.5f)] private float tipLean = 0f;
    [Tooltip("Slants the top edge, as a fraction of width. Zero keeps a flat top.")]
    [SerializeField, Range(-1, 1)] private float topSlant = 0f;
    public enum FlowerHead { Cross, Square, Diamond }
    [Header("Flower Head")]
    [SerializeField] private FlowerHead flowerHead = FlowerHead.Cross;
    [SerializeField, HideInInspector] private Mesh sourceMesh;
    [SerializeField, HideInInspector] private Vector4 builtDimensions;
    [SerializeField, HideInInspector] private Vector4 builtProfile;

#if UNITY_EDITOR
    // The cover slot defines this source's role. Preserve every authored dimension and transform during repair.
    public void SetShape(Shape kind)
    {
        shape = kind;
        RebuildSource();
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(GetComponent<MeshFilter>());
    }

    private bool queued;
    private void OnValidate() => QueueRefresh();
    private void OnEnable() => QueueRefresh();
    private void QueueRefresh()
    {
        if (queued || EditorApplication.isPlayingOrWillChangePlaymode) return;
        queued = true;
        EditorApplication.delayCall += Refresh;
    }
    private void Refresh()
    {
        queued = false;
        if (this == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (sourceMesh == null || builtDimensions != new Vector4(width, height, flowerSize, (float)shape) ||
            builtProfile != new Vector4(topWidth, tipLean, topSlant, (float)flowerHead)) RebuildSource();
        Level_GrassCover.RequestRebuilds();
    }

    public void Configure(Shape kind, Material material)
    {
        shape = kind;
        width = kind == Shape.Grass ? 0.055f : 0.018f;
        height = kind == Shape.Grass ? 0.3f : 0.44f;
        GetComponent<MeshRenderer>().sharedMaterial = material;
        RebuildSource();
    }

    public void RebuildSource()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        float w = Mathf.Max(0.005f, width), h = Mathf.Max(0.01f, height);
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var pivots = new List<Vector3>();
        var kinds = new List<Vector2>();
        var triangles = new List<int>();
        // Kinds: grass, stem, petal, centre. No texture or alpha-test needed: only the visible rectangles exist.
        void Quad(float x0, float y0, float x1, float y1, float z, float kind)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(x0, y0, z)); vertices.Add(new Vector3(x1, y0, z));
            vertices.Add(new Vector3(x1, y1, z)); vertices.Add(new Vector3(x0, y1, z));
            uv.Add(new Vector2(0, y0 / h)); uv.Add(new Vector2(1, y0 / h));
            uv.Add(new Vector2(1, y1 / h)); uv.Add(new Vector2(0, y1 / h));
            for (int i = 0; i < 4; i++) { pivots.Add(Vector3.zero); kinds.Add(new Vector2(kind, 0)); }
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }
        Quad(-w * 0.5f, 0, w * 0.5f, h, 0, shape == Shape.Grass ? 0 : 1);
        if (shape == Shape.Grass)
        {
            vertices[2] = new Vector3(w * 0.5f * topWidth + tipLean * h, h + topSlant * w * 0.5f, 0);
            vertices[3] = new Vector3(-w * 0.5f * topWidth + tipLean * h, h - topSlant * w * 0.5f, 0);
        }
        if (shape == Shape.Flower)
        {
            float s = Mathf.Max(0.01f, flowerSize), r = s / 6f;
            int firstPetal = vertices.Count;
            if (flowerHead == FlowerHead.Cross)
            {
                // Four rectangular arms and one square centre. No overlapping petal faces.
                Quad(-s * 0.5f, h - r, -r, h + r, 0, 2);
                Quad(r, h - r, s * 0.5f, h + r, 0, 2);
                Quad(-r, h + r, r, h + s * 0.5f, 0, 2);
                Quad(-r, h - s * 0.5f, r, h - r, 0, 2);
                Quad(-r, h - r, r, h + r, 0, 3);
            }
            else
            {
                // A square head split around its centre, keeping every face coplanar and non-overlapping.
                Quad(-s * 0.5f, h - s * 0.5f, -r, h + s * 0.5f, 0, 2);
                Quad(r, h - s * 0.5f, s * 0.5f, h + s * 0.5f, 0, 2);
                Quad(-r, h + r, r, h + s * 0.5f, 0, 2);
                Quad(-r, h - s * 0.5f, r, h - r, 0, 2);
                Quad(-r, h - r, r, h + r, 0, 3);
                if (flowerHead == FlowerHead.Diamond)
                    for (int i = firstPetal; i < vertices.Count; i++)
                    {
                        Vector3 v = vertices[i] - new Vector3(0,h,0);
                        vertices[i] = new Vector3((v.x - v.y) * 0.7071068f, h + (v.x + v.y) * 0.7071068f, v.z);
                    }
            }
        }
        // Replace rather than mutate a potentially shared mesh on a duplicated source GameObject.
        Mesh previous = sourceMesh;
        sourceMesh = new Mesh { name = name + " Source Card" };
        sourceMesh.SetVertices(vertices); sourceMesh.SetUVs(0, uv); sourceMesh.SetUVs(1, pivots);
        sourceMesh.SetUVs(2, kinds); sourceMesh.SetTriangles(triangles, 0);
        sourceMesh.RecalculateNormals(); sourceMesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = sourceMesh;
        builtDimensions = new Vector4(width, height, flowerSize, (float)shape);
        builtProfile = new Vector4(topWidth, tipLean, topSlant, (float)flowerHead);
        Level_GrassCover.ReleaseUnusedMesh(previous);
        GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        EditorUtility.SetDirty(this);
        EditorUtility.SetDirty(GetComponent<MeshFilter>());
    }
#endif
}
