using UnityEngine;
using UnityEngine.Rendering;

// The mist round the edge of an area, so a small map doesn't need modelling past its limits: clear over the play space,
// billowing thicker past the area's warning line until nothing shows through. It follows each area's Area_Bounds by itself.
// A few flat layers at different heights give it depth as the camera moves. Each is a ring with a hole over the play
// space, so the mist costs nothing where it's clear.
//
// Setup (Run scene): make a material on Goblins/Edge Mist (colours, billow size and drift are on it), put this on an object and
// assign the material. Raise Lowest Height until the mist covers your cliffs and trees at the edge.
//
// GC: none per frame; one mesh per area and a property block per layer. One UpdateManager subscription (OnLateUpdate), plus
// Area_Root.Changed.
public class Run_EdgeMist : MonoBehaviour
{
    [Tooltip("A material on the Goblins/Edge Mist shader.")]
    [SerializeField] private Material material;

    [Header("Shape")]
    [Tooltip("Metres inside the warning line that the mist starts.")]
    [SerializeField] private float startInside = 10f;

    [Tooltip("Metres the mist takes to go from nothing to solid.")]
    [Min(0.1f)] [SerializeField] private float thickenOver = 35f;

    [Tooltip("How far out the mist reaches past the warning line, metres. Enough to fill the view from the map limits.")]
    [Min(10f)] [SerializeField] private float reach = 400f;

    [Header("Layers")]
    [Tooltip("How many layers. More looks deeper and costs more where the mist is on screen.")]
    [Range(1, 4)] [SerializeField] private int layers = 3;

    [Tooltip("Height of the lowest layer above the floor, metres.")]
    [Min(0f)] [SerializeField] private float lowestHeight = 4f;

    [Tooltip("Metres between layers.")]
    [Min(0f)] [SerializeField] private float layerGap = 2.5f;

    [Tooltip("Each layer's strength. The top ones want to be fainter, so the ones below show through.")]
    [Range(0f, 1f)] [SerializeField] private float layerStrength = 0.75f;

    private static readonly int AreaId = Shader.PropertyToID("_Area");
    private static readonly int EdgeId = Shader.PropertyToID("_Edge");

    private readonly MaterialPropertyBlock[] blocks = new MaterialPropertyBlock[4];
    private Mesh mesh;
    private Area_Bounds bounds;

    private void Awake()
    {
        for (int i = 0; i < blocks.Length; i++)
            blocks[i] = new MaterialPropertyBlock();

        mesh = new Mesh { name = "Edge Mist" };
    }

    private void OnEnable()
    {
        if (material == null)
        {
            Debug.LogWarning($"{name}: Run_EdgeMist needs a material on Goblins/Edge Mist.", this);
            return;
        }

        Area_Root.Changed += Bind;
        Bind(Area_Root.Current);

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate += Tick;
    }

    private void OnDisable()
    {
        Area_Root.Changed -= Bind;

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= Tick;
    }

    private void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);
    }

    // A new area loaded (or the old one went, with null)
    private void Bind(Area_Root root)
    {
        bounds = root != null ? root.Bounds : null;

        if (bounds != null)
            Build();
    }

    // A ring in world XZ: its hole stops where the mist starts (with room for the wobble), its outside at Reach
    private void Build()
    {
        Vector3 centre = bounds.transform.position;
        Vector2 half = bounds.WarningSize * 0.5f;
        Vector2 inner = Vector2.Max(half - Vector2.one * (startInside + 15f), Vector2.zero);
        Vector2 outer = half + Vector2.one * reach;

        mesh.Clear();
        mesh.vertices = new[]
        {
            new Vector3(centre.x - inner.x, 0f, centre.z - inner.y),
            new Vector3(centre.x - inner.x, 0f, centre.z + inner.y),
            new Vector3(centre.x + inner.x, 0f, centre.z + inner.y),
            new Vector3(centre.x + inner.x, 0f, centre.z - inner.y),
            new Vector3(centre.x - outer.x, 0f, centre.z - outer.y),
            new Vector3(centre.x - outer.x, 0f, centre.z + outer.y),
            new Vector3(centre.x + outer.x, 0f, centre.z + outer.y),
            new Vector3(centre.x + outer.x, 0f, centre.z - outer.y)
        };
        mesh.triangles = new[] { 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0 };
        mesh.RecalculateBounds();

        Vector4 area = new Vector4(centre.x, centre.z, half.x, half.y);

        for (int i = 0; i < blocks.Length; i++)
        {
            blocks[i].SetVector(AreaId, area);
            blocks[i].SetVector(EdgeId, new Vector4(startInside, thickenOver, i * 17.3f, layerStrength));
        }
    }

    private void Tick()
    {
        if (bounds == null)
            return;

        float floor = Combat_Projectiles.Instance != null ? Combat_Projectiles.Instance.FloorHeight : 0f;

        for (int i = 0; i < layers; i++)
        {
            Matrix4x4 place = Matrix4x4.Translate(new Vector3(0f, floor + lowestHeight + i * layerGap, 0f));
            Graphics.DrawMesh(mesh, place, material, gameObject.layer, null, 0, blocks[i], ShadowCastingMode.Off, false);
        }
    }
}
