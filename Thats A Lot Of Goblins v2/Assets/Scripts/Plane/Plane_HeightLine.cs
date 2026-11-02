using UnityEngine;
using UnityEngine.Rendering;

// A faint dotted line from the plane straight down to the ground, with an X lying on the ground where it lands, so
// you can read how high you are. The line starts at the plane plus Offset (in the plane's own space, so it can sit
// under the belly) and drops to whatever is below on Ground Layers (or the flat floor if nothing is hit).
//
// Setup: put this on the plane. Give it a Line Material on a transparent shader that uses vertex colour, such as
// Universal Render Pipeline/Particles/Unlit (Surface Type Transparent); the dots are a small texture made here, so the
// material needs none. Give it the X sprite. The line and marker are made as children at start.
//
// GC: one small texture made at start, none per frame. One UpdateManager subscription (OnLateUpdate), so it
// follows where the plane ended up this frame.
public class Plane_HeightLine : MonoBehaviour
{
    [Header("Start")]
    [Tooltip("Where the line starts, from the plane's position in the plane's own space (e.g. a little under the belly).")]
    [SerializeField] private Vector3 offset = new Vector3(0f, -0.5f, 0f);
    [Tooltip("Draw the start point as a small yellow sphere in the Scene view.")]
    [SerializeField] private bool drawGizmo = true;

    [Header("Ground")]
    [Tooltip("What the line drops onto. Nothing hit within Max Distance uses the flat floor.")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField, Min(1f)] private float maxDistance = 300f;
    [Tooltip("Hide the line and X when the plane is lower than this above the ground, metres.")]
    [SerializeField, Min(0f)] private float hideBelow = 1f;

    [Header("Line")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color lineColour = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField, Min(0.001f)] private float lineWidth = 0.08f;
    [Tooltip("Length of each dot and each gap, metres.")]
    [SerializeField, Min(0.01f)] private float dotLength = 0.4f;
    [SerializeField, Min(0.01f)] private float gapLength = 0.4f;

    [Header("Ground Marker")]
    [SerializeField] private Sprite markerSprite;
    [SerializeField] private Color markerColour = new Color(1f, 1f, 1f, 0.4f);
    [Tooltip("Width of the X on the ground, metres.")]
    [SerializeField, Min(0.01f)] private float markerSize = 1.2f;
    [Tooltip("How far above the ground the X floats, so it doesn't flicker into it.")]
    [SerializeField, Min(0f)] private float markerLift = 0.05f;

    private LineRenderer line;
    private SpriteRenderer marker;
    private Texture2D dots;
    private MaterialPropertyBlock block;
    private bool shown = true;

    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");

    public Vector3 StartPoint => transform.TransformPoint(offset);

    // Ground and cliffs by default, so the line never stops on goblins or the plane itself
    private void Reset()
    {
        int mask = LayerMask.GetMask("Ground", "Building");

        if (mask != 0)
            groundLayers = mask;
    }

    private void Awake()
    {
        // Never on the plane's own colliders
        groundLayers &= ~(1 << gameObject.layer);

        // One dot and one gap across the texture, tiled along the line every Dot + Gap metres
        dots = new Texture2D(16, 1, TextureFormat.RGBA32, false)
        {
            name = "Height Line Dots",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point
        };

        int lit = Mathf.Clamp(Mathf.RoundToInt(16f * dotLength / (dotLength + gapLength)), 1, 15);

        for (int x = 0; x < 16; x++)
            dots.SetPixel(x, 0, x < lit ? Color.white : Color.clear);

        dots.Apply(false, true);

        line = new GameObject("Height Line").AddComponent<LineRenderer>();
        line.transform.SetParent(transform, false);
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.textureMode = LineTextureMode.Tile;
        line.textureScale = new Vector2(1f / (dotLength + gapLength), 1f);
        line.alignment = LineAlignment.View;
        line.widthMultiplier = lineWidth;
        line.startColor = lineColour;
        line.endColor = lineColour;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = lineMaterial;

        block = new MaterialPropertyBlock();
        block.SetTexture(BaseMap, dots);
        block.SetTexture(MainTex, dots);
        line.SetPropertyBlock(block);

        marker = new GameObject("Height Marker").AddComponent<SpriteRenderer>();
        marker.transform.SetParent(transform, false);
        marker.sprite = markerSprite;
        marker.color = markerColour;
        marker.shadowCastingMode = ShadowCastingMode.Off;
        marker.receiveShadows = false;

        if (lineMaterial == null)
            Debug.LogWarning($"{name}: Plane_HeightLine has no Line Material, so the line won't show.", this);
    }

    private void OnEnable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate += Tick;
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= Tick;

        Show(false);
    }

    private void OnDestroy()
    {
        if (dots != null)
            Destroy(dots);
    }

    private void Tick()
    {
        Vector3 start = StartPoint;
        Vector3 ground;

        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, maxDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            ground = hit.point;
        }
        else
        {
            float floor = Combat_Projectiles.Instance != null ? Combat_Projectiles.Instance.FloorHeight : 0f;
            ground = new Vector3(start.x, floor, start.z);
        }

        bool visible = start.y - ground.y > hideBelow;
        Show(visible);

        if (!visible)
            return;

        // Drawn from the ground up, so the dots stay put on the ground as the plane climbs or dives
        line.SetPosition(0, ground);
        line.SetPosition(1, start);

        // The X lies flat on the ground, turned with the plane's heading
        Transform x = marker.transform;
        x.SetPositionAndRotation(ground + Vector3.up * markerLift,
            Quaternion.Euler(90f, transform.eulerAngles.y, 0f));

        float spriteWidth = markerSprite != null ? markerSprite.bounds.size.x : 1f;
        x.localScale = Vector3.one * (markerSize / Mathf.Max(spriteWidth, 0.0001f)) / Mathf.Max(transform.lossyScale.x, 0.0001f);
    }

    private void Show(bool visible)
    {
        if (shown == visible || line == null)
            return;

        shown = visible;
        line.enabled = visible;
        marker.enabled = visible;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmo)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(StartPoint, 0.1f);
    }
}
