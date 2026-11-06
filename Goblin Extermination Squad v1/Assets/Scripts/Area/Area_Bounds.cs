using UnityEngine;

// The edges of an area: two rectangles on the ground, centred on this object. Past the red dotted Warning line the player is
// told to turn back; at the red Respawn line, Margin further out, Run_Edge takes over and brings the plane back in.
//
// Setup: the Area_Root checklist's Create button makes this. Move and size it with the Scene view handles while it's
// selected, then assign nothing else.
//
// Warning dots are one real child MeshRenderer, baked in edit mode and refreshed once at runtime startup to
// sample the loaded floor. No frame work/subscriptions; mesh/raycast arrays allocate only during rebuild.
[ExecuteAlways]
public class Area_Bounds : MonoBehaviour
{
    [Tooltip("Width (X) and length (Z) of the warning rectangle, metres.")]
    [SerializeField] private Vector2 warningSize = new Vector2(200f, 200f);

    [Tooltip("How far past the warning line the respawn line is, metres, on every side.")]
    [Min(0.1f)] [SerializeField] private float margin = 25f;

    [Header("Ground Warning Line")]
    [SerializeField] private bool showWarningLine = true;
    [SerializeField] private Color warningColour = new Color(1f,0.08f,0.04f,1f);
    [Tooltip("Overall line opacity. Multiplies the Warning Colour's alpha.")]
    [SerializeField, Range(0f,1f)] private float lineAlpha = 1f;
    [SerializeField, Min(0.02f)] private float lineWidth = 0.45f;
    [SerializeField, Min(0.05f)] private float dotLength = 0.9f;
    [SerializeField, Min(0.05f)] private float gapLength = 0.8f;
    [Tooltip("Small offset above the sampled floor to avoid flickering.")]
    [SerializeField, Min(0.001f)] private float lineHeight = 0.035f;
    [SerializeField] private LayerMask floorLayers = ~0;
    [SerializeField] private Material warningMaterial;
    [SerializeField, HideInInspector] private GameObject warningObject;
    [SerializeField, HideInInspector] private Mesh warningMesh;
    [SerializeField, HideInInspector] private string bakedLineKey;
    private MaterialPropertyBlock warningProperties;
    private Vector3 builtPosition;
    private Matrix4x4 builtTransform;
    private bool queued;

    private void OnEnable()
    {
        if (warningObject != null) warningObject.SetActive(showWarningLine);
        ApplyWarningColour();
#if UNITY_EDITOR
        if (!Application.isPlaying) RequestWarningLine();
#endif
    }
    private void Start() { if (Application.isPlaying) RebuildWarningLine(); }
    private void OnDisable() { if (warningObject != null) warningObject.SetActive(false); }
    private void ApplyWarningColour()
    {
        if (warningObject == null) return;
        if (warningProperties == null) warningProperties = new MaterialPropertyBlock();
        Color colour = warningColour; colour.a *= lineAlpha;
        warningProperties.SetColor("_Color",colour);
        warningProperties.SetColor("_BaseColor",colour);
        warningObject.GetComponent<MeshRenderer>().SetPropertyBlock(warningProperties);
    }
    public void RefreshWarningAppearance() => ApplyWarningColour();

    public void RebuildWarningLine()
    {
#if UNITY_EDITOR
        if (warningMaterial == null) warningMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Level/MapWarning_Mat.mat");
#endif
        if (!showWarningLine) { if (warningObject != null) warningObject.SetActive(false); return; }
        if (warningMaterial == null) { Debug.LogWarning("Assign MapWarning_Mat to Area Bounds' Warning Material.",this); return; }
        float width = Mathf.Max(0.02f,lineWidth), dot = Mathf.Max(0.05f,dotLength), gap = Mathf.Max(0.05f,gapLength);
        float x = Mathf.Max(1,warningSize.x)*0.5f, z = Mathf.Max(1,warningSize.y)*0.5f;
        if (4*(x+z)/(dot+gap) > 8192) { Debug.LogWarning("Warning line exceeds 8192 dots. Increase Dot/Gap Length.",this); return; }
        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        var colours = new System.Collections.Generic.List<Color>();
        var hits = new RaycastHit[16];
        Physics.SyncTransforms();
        var corners = new[] {new Vector3(-x,0,-z),new Vector3(-x,0,z),new Vector3(x,0,z),new Vector3(x,0,-z)};
        for (int edge = 0; edge < 4; edge++)
        {
            Vector3 a = transform.position+corners[edge], b = transform.position+corners[(edge+1)%4];
            Vector3 direction = (b-a).normalized, side = Vector3.Cross(direction,Vector3.up)*width*0.5f;
            float length = Vector3.Distance(a,b);
            // Centre a whole number of dashes on each edge, leaving a gap at the corners.
            int count = Mathf.Max(1,Mathf.FloorToInt((length+gap)/(dot+gap)));
            float used = count*dot+(count-1)*gap, inset = Mathf.Max(0,(length-used)*0.5f);
            for (int i = 0; i < count; i++)
            {
                float from = inset+i*(dot+gap), to = Mathf.Min(length,from+dot);
                Vector3 p = a+direction*from, q = a+direction*to;
                Vector3 middle = (p+q)*0.5f;
                float floor = transform.position.y;
                int found = Physics.RaycastNonAlloc(new Vector3(middle.x,floor+1000,middle.z),Vector3.down,hits,4000,floorLayers,QueryTriggerInteraction.Ignore);
                float best = float.NegativeInfinity;
                for (int hit = 0; hit < found; hit++)
                    if (hits[hit].collider.attachedRigidbody == null && hits[hit].normal.y > 0.5f) best = Mathf.Max(best,hits[hit].point.y);
                if (!float.IsNegativeInfinity(best)) floor = best;
                p.y = q.y = floor+Mathf.Max(0.001f,lineHeight);
                int start = vertices.Count;
                vertices.Add(transform.InverseTransformPoint(p-side)); vertices.Add(transform.InverseTransformPoint(p+side));
                vertices.Add(transform.InverseTransformPoint(q+side)); vertices.Add(transform.InverseTransformPoint(q-side));
                for (int v = 0; v < 4; v++) colours.Add(Color.white);
                triangles.Add(start); triangles.Add(start+1); triangles.Add(start+2);
                triangles.Add(start); triangles.Add(start+2); triangles.Add(start+3);
            }
        }
        Mesh next = new Mesh {name = "Red Dotted Warning Boundary",indexFormat = vertices.Count>65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16};
        next.SetVertices(vertices); next.SetColors(colours); next.SetTriangles(triangles,0); next.RecalculateNormals(); next.RecalculateBounds();
        if (warningObject == null)
        { warningObject = new GameObject("Red Dotted Warning Line",typeof(MeshFilter),typeof(MeshRenderer)); warningObject.transform.SetParent(transform,false); }
        warningObject.layer = gameObject.layer;
        warningObject.transform.localPosition = Vector3.zero;
        warningObject.transform.localRotation = Quaternion.identity;
        warningObject.transform.localScale = Vector3.one;
        Mesh previous = warningMesh; warningMesh = next;
        warningObject.GetComponent<MeshFilter>().sharedMesh = next;
        MeshRenderer renderer = warningObject.GetComponent<MeshRenderer>(); renderer.sharedMaterial = warningMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        warningObject.SetActive(isActiveAndEnabled); ApplyWarningColour(); builtPosition = transform.position; builtTransform = transform.localToWorldMatrix;
        if (previous != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) Level_GrassCover.ReleaseUnusedMesh(previous);
            else
#endif
                Destroy(previous);
        }
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            bakedLineKey = WarningLineKey();
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(warningObject.GetComponent<MeshFilter>());
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    // How far past the warning line a point is, metres: 0 or less is inside
    private float Past(Vector3 point)
    {
        Vector3 centre = transform.position;
        return Mathf.Max(Mathf.Abs(point.x - centre.x) - warningSize.x * 0.5f, Mathf.Abs(point.z - centre.z) - warningSize.y * 0.5f);
    }

    // 0 inside the warning line, up to 1 at the respawn line
    public float WarningAmount(Vector3 point) => Mathf.Clamp01(Past(point) / margin);

    public bool PastRespawn(Vector3 point) => Past(point) >= margin;

    public bool InsideWarning(Vector3 point) => Past(point) < 0f;

    public Vector2 WarningSize => warningSize;

    // Metres from a point to the warning line: negative once past it
    public float DistanceToWarning(Vector3 point) => -Past(point);

    // The flat direction straight out through the side this point is closest to crossing
    public Vector3 Outward(Vector3 point)
    {
        Vector3 centre = transform.position;
        float x = point.x - centre.x;
        float z = point.z - centre.z;

        if (Mathf.Abs(x) - warningSize.x * 0.5f > Mathf.Abs(z) - warningSize.y * 0.5f)
            return x >= 0f ? Vector3.right : Vector3.left;

        return z >= 0f ? Vector3.forward : Vector3.back;
    }

#if UNITY_EDITOR
    private string WarningLineKey() => Hash128.Compute(transform.localToWorldMatrix.ToString("F6")+warningSize.ToString("F6")+
        $"|{showWarningLine}|{lineWidth:R}|{dotLength:R}|{gapLength:R}|{lineHeight:R}|{floorLayers.value}|"+
        UnityEditor.AssetDatabase.GetAssetPath(warningMaterial)).ToString();
    private void OnDestroy()
    {
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject previous = warningObject;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null || previous == null || !previous.scene.IsValid() || !previous.scene.isLoaded) return;
            Mesh mesh = previous.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(previous); Level_GrassCover.ReleaseUnusedMesh(mesh);
        };
    }
    private void OnValidate() => RequestWarningLine();
    public void RequestWarningLine()
    {
        if (queued || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
        queued = true; UnityEditor.EditorApplication.delayCall += RebuildDeferred;
    }
    private void RebuildDeferred()
    {
        queued = false;
        if (this == null || !isActiveAndEnabled || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.EditorUtility.IsPersistent(this)) return;
        if (GUIUtility.hotControl != 0) { RequestWarningLine(); return; }
        if (warningObject != null && warningMesh != null && bakedLineKey == WarningLineKey())
        { builtPosition = transform.position; builtTransform = transform.localToWorldMatrix; warningObject.SetActive(showWarningLine); ApplyWarningColour(); return; }
        RebuildWarningLine();
    }
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying && (builtPosition != transform.position || builtTransform != transform.localToWorldMatrix)) RequestWarningLine();
        Vector3 centre = transform.position;
        Vector3 warning = new Vector3(warningSize.x, 0f, warningSize.y);
        Vector3 respawn = warning + new Vector3(margin * 2f, 0f, margin * 2f);

        Gizmos.color = new Color(1f, 0.08f, 0.04f, 0.7f);
        if (!showWarningLine || warningObject == null || GUIUtility.hotControl != 0) Gizmos.DrawWireCube(centre, warning);
        Debug_Gizmos.Label(centre + new Vector3(0f, 0f, warningSize.y * 0.5f), "Warning", Gizmos.color);

        Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(centre, respawn);
        Debug_Gizmos.Label(centre + new Vector3(0f, 0f, respawn.z * 0.5f), "Respawn", Gizmos.color);
    }
#endif
}
