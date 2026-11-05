using UnityEngine;

// The red dotted line just above the floor that marks the map's warning line, fading in as the plane gets near it.
//
// Setup (Run scene): put this on an object and assign the Plane and an Unlit, transparent material (URP / Unlit). Tune the
// look here; each area sets its limits.
//
// GC: none per frame; the texture and one line object are made once. One UpdateManager subscription (OnLateUpdate).
public class Run_Gates : MonoBehaviour
{
    [SerializeField] private Transform plane;

    [Tooltip("An Unlit, transparent material for the lines.")]
    [SerializeField] private Material lineMaterial;

    [Header("Lines")]
    [Tooltip("Metres above the floor.")]
    [Min(0f)] [SerializeField] private float height = 0.2f;

    [Tooltip("How wide the lines are, metres.")]
    [Min(0.02f)] [SerializeField] private float width = 0.5f;

    [Tooltip("Length of one dot, metres.")]
    [Min(0.05f)] [SerializeField] private float dotLength = 2f;

    [Tooltip("Gap between dots, metres.")]
    [Min(0.05f)] [SerializeField] private float gapLength = 1.5f;

    [Header("Map Limit")]
    [SerializeField] private Color limitColour = new Color(1f, 0.2f, 0.15f, 1f);

    [Tooltip("The red line starts to show when the plane is this close to it, metres.")]
    [Min(0.1f)] [SerializeField] private float limitShowDistance = 30f;

    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly Vector3[] corners = new Vector3[4];
    private Texture2D dots;
    private MaterialPropertyBlock block;
    private LineRenderer limit;
    private Area_Bounds bounds;

    private void Awake()
    {
        dots = new Texture2D(16, 1, TextureFormat.RGBA32, false)
        {
            name = "Gate Line Dots",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point
        };

        int lit = Mathf.Clamp(Mathf.RoundToInt(16f * dotLength / (dotLength + gapLength)), 1, 15);

        for (int x = 0; x < 16; x++)
            dots.SetPixel(x, 0, x < lit ? Color.white : Color.clear);

        dots.Apply(false, true);
        block = new MaterialPropertyBlock();
        block.SetTexture(BaseMap, dots);
        block.SetTexture(MainTex, dots);

        limit = BuildLine("Map Limit");
    }

    private LineRenderer BuildLine(string lineName)
    {
        GameObject holder = new GameObject(lineName);
        holder.transform.SetParent(transform, false);
        holder.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        LineRenderer line = holder.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 4;
        line.alignment = LineAlignment.TransformZ;
        line.textureMode = LineTextureMode.Tile;
        line.textureScale = new Vector2(1f / (dotLength + gapLength), 1f);
        line.widthMultiplier = width;
        line.sharedMaterial = lineMaterial;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    private void OnEnable()
    {
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
        if (dots != null)
            Destroy(dots);
    }

    // A new area loaded (or the old one went, with null)
    private void Bind(Area_Root root)
    {
        bounds = root != null ? root.Bounds : null;
        limit.enabled = false;

        if (bounds != null)
            Place(limit, bounds.transform.position, bounds.WarningSize);
    }

    private void Tick()
    {
        if (bounds == null || plane == null)
            return;

        Show(limit, limitColour, 1f - Mathf.Clamp01(bounds.DistanceToWarning(plane.position) / limitShowDistance));
    }

    private void Show(LineRenderer line, Color colour, float amount)
    {
        line.enabled = amount > 0f;

        if (amount <= 0f)
            return;

        colour.a *= amount;
        block.SetColor(BaseColor, colour);
        block.SetColor(ColorId, colour);
        line.SetPropertyBlock(block);
    }

    private void Place(LineRenderer line, Vector3 centre, Vector2 size)
    {
        float floor = Combat_Projectiles.Instance != null ? Combat_Projectiles.Instance.FloorHeight : 0f;
        Vector3 middle = new Vector3(centre.x, floor + height, centre.z);
        float x = size.x * 0.5f;
        float z = size.y * 0.5f;

        corners[0] = middle + new Vector3(-x, 0f, z);
        corners[1] = middle + new Vector3(x, 0f, z);
        corners[2] = middle + new Vector3(x, 0f, -z);
        corners[3] = middle + new Vector3(-x, 0f, -z);
        line.SetPositions(corners);
    }
}
