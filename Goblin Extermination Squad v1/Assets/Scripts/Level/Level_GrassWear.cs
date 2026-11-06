using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Optional worn paths on an authored grass ground. Bake one real lower shelf mesh in the editor. At runtime,
// shared horde sampling paints a bounded CPU mask and uploads one R8 texture, revealing the lower mesh while
// clipping upper shelves/cards in every render pass. Paths last for the area life; disabling/clearing resets them.
// GC: arrays/texture/property block on enable and editor bake lists. No frame subscriptions or per-stamp allocations.
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Level_GroundSurface))]
public class Level_GrassWear : Core_RegistrableBehaviour
{
    [Header("Path Appearance")]
    [SerializeField] private Color colour = new Color(0.055f,0.04f,0.065f);
    [Tooltip("Height above the base ground; automatically kept below the dark grass shelf.")]
    [SerializeField, Min(0.001f)] private float height = 0.015f;
    [SerializeField, Range(0.1f,0.9f)] private float threshold = 0.5f;
    [Header("Walking Wear")]
    [SerializeField, Range(0.1f,2)] private float radius = 0.65f;
    [SerializeField, Range(0.05f,1)] private float strength = 0.8f;
    [Header("Performance")]
    [SerializeField, Range(1,20)] private int tickRate = 10;
    [SerializeField, Range(0.1f,1)] private float metresPerTexel = 0.3f;
    [SerializeField, Range(64,1024)] private int maxResolution = 1024;
    [SerializeField, Range(1024,262144)] private int maxPixelChecks = 65536;
    [SerializeField, HideInInspector] private Level_GrassLayerData data;
    [SerializeField, HideInInspector] private GameObject lowerObject;
    [SerializeField, HideInInspector] private int bakedVersion = -1;
    private Level_GroundSurface surface;
    private Texture2D texture;
    private Level_GrassWearMap mask;
    private MaterialPropertyBlock properties;
    private Matrix4x4 toLocal;
    private Vector2 size, min;
    private float nextTick;
    private bool tick, hasWear;
    public int TextureWidth => mask != null ? mask.Width : 0;
    public int TextureHeight => mask != null ? mask.Height : 0;
    public int PixelChecks => mask != null ? mask.Work : 0;
    public int Skipped => mask != null ? mask.Skipped : 0;
    public bool Ready => Application.isPlaying && isActiveAndEnabled && mask != null && texture != null && surface != null && surface.isActiveAndEnabled;

    private void OnEnable()
    {
        surface = GetComponent<Level_GroundSurface>();
        if (Application.isPlaying && (data?.mesh == null || bakedVersion != surface.GeometryVersion))
            Debug.LogWarning("Worn paths need a current bake. Exit Play Mode, Rebuild Worn Ground Layer and save the area.",this);
        if (Application.isPlaying && data?.mesh != null && bakedVersion == surface.GeometryVersion)
        {
            size = GetComponent<Level_Ground>().Size; min = -size*0.5f;
            toLocal = transform.worldToLocalMatrix;
            int width = Mathf.Clamp(Mathf.CeilToInt(size.x/metresPerTexel),64,maxResolution);
            int pixelsHigh = Mathf.Clamp(Mathf.CeilToInt(size.y/metresPerTexel),64,maxResolution);
            mask = new Level_GrassWearMap(width,pixelsHigh,size);
            nextTick = 0; tick = false; hasWear = false;
            texture = new Texture2D(width,pixelsHigh,TextureFormat.R8,false,true) { name = "Runtime Grass Wear", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.LoadRawTextureData(mask.Pixels); texture.Apply(false,false);
            Level_GrassWearSystem.GetOrCreate().Register(this);
        }
        if (lowerObject != null) lowerObject.GetComponent<MeshRenderer>().enabled = false;
        surface.RefreshAppearance();
#if UNITY_EDITOR
        if (!Application.isPlaying) RequestRebuilds();
#endif
    }
    private void OnDisable()
    {
        if (IsRegistered && Level_GrassWearSystem.Current != null) Level_GrassWearSystem.Current.Unregister(this);
        if (lowerObject != null) lowerObject.GetComponent<MeshRenderer>().enabled = false;
        if (texture != null) { if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture); }
        texture = null; mask = null;
        hasWear = false;
        if (surface != null) surface.RefreshAppearance();
        Level_GrassWearSystem.MarkChanged();
    }
    public void SetProperties(MaterialPropertyBlock block, bool lower = false)
    {
        block.SetFloat("_WearEnabled",Ready ? 1 : 0);
        block.SetFloat("_WearLayer",lower ? 1 : 0);
        block.SetFloat("_WearThreshold",threshold);
        if (!Ready) return;
        block.SetTexture("_WearMask",texture);
        block.SetMatrix("_WearWorldToLocal",toLocal);
        block.SetVector("_WearBounds",new Vector4(min.x,min.y,1/size.x,1/size.y));
    }
    public bool TryHeight(Vector3 local, out float wornHeight)
    {
        wornHeight = data != null ? data.height : 0;
        return Ready && mask.Sample(new Vector2(local.x-min.x,local.z-min.y)) >= threshold;
    }
    internal bool BeginTick(float now)
    {
        tick = Ready && now >= nextTick;
        if (tick) { nextTick = now+1f/Mathf.Max(1,tickRate); mask.Begin(maxPixelChecks); }
        return tick;
    }
    internal void Stamp(Vector3 from, Vector3 to)
    {
        if (!tick || !Ready) return;
        Vector3 end = toLocal.MultiplyPoint3x4(to);
        if (data == null || !data.Contains(new Vector2(end.x,end.z))) return;
        Vector3 start = toLocal.MultiplyPoint3x4(from);
        mask.Stamp(new Vector2(start.x-min.x,start.z-min.y),new Vector2(end.x-min.x,end.z-min.y),radius,strength);
    }
    internal bool Upload()
    {
        if (!Ready || !mask.Dirty) return false;
        texture.LoadRawTextureData(mask.Pixels); texture.Apply(false,false); mask.Uploaded();
        if (lowerObject != null) lowerObject.GetComponent<MeshRenderer>().enabled = true;
        hasWear = true;
        return true;
    }
    public void ClearPaths()
    {
        if (mask == null) return;
        mask.Clear(); Upload();
        if (lowerObject != null) lowerObject.GetComponent<MeshRenderer>().enabled = false;
        hasWear = false;
        Level_GrassWearSystem.MarkChanged();
    }
    public void ApplyAppearance()
    {
        if (surface == null) surface = GetComponent<Level_GroundSurface>();
        if (lowerObject == null || data?.mesh == null) return;
        if (properties == null) properties = new MaterialPropertyBlock();
        properties.Clear(); surface.SetColourProperties(properties,Level_GrassArea.Kind.Dark);
        properties.SetColor("_Color",colour); SetProperties(properties,true);
        lowerObject.GetComponent<MeshRenderer>().SetPropertyBlock(properties);
        lowerObject.GetComponent<MeshRenderer>().enabled = Ready && hasWear;
    }

#if UNITY_EDITOR
    private void OnDestroy()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject previous = lowerObject;
        EditorApplication.delayCall += () =>
        {
            if (this != null || previous == null || !previous.scene.IsValid() || !previous.scene.isLoaded) return;
            Mesh mesh = previous.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(previous); Level_GrassCover.ReleaseUnusedMesh(mesh);
        };
    }
    private static bool queued;
    private void OnValidate() { if (!EditorApplication.isPlayingOrWillChangePlaymode) RequestRebuilds(); }
    public static void RequestRebuilds()
    {
        if (queued || EditorApplication.isPlayingOrWillChangePlaymode) return;
        queued = true; EditorApplication.delayCall += Refresh;
    }
    private static void Refresh()
    {
        queued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (GUIUtility.hotControl != 0) { RequestRebuilds(); return; }
        foreach (var wear in FindObjectsByType<Level_GrassWear>())
        {
            if (!wear.isActiveAndEnabled || EditorUtility.IsPersistent(wear)) continue;
            wear.surface = wear.GetComponent<Level_GroundSurface>();
            float target = Mathf.Clamp(wear.height,0.001f,Mathf.Max(0.001f,wear.surface.DarkHeight-0.005f));
            if (wear.data?.mesh == null || wear.lowerObject == null || wear.bakedVersion != wear.surface.GeometryVersion || Mathf.Abs(wear.data.height-target)>0.00001f)
                wear.Bake();
            wear.surface.RefreshAppearance();
        }
    }
    public void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        surface = GetComponent<Level_GroundSurface>(); var ground = GetComponent<Level_Ground>();
        Mesh nextMesh = null;
        try
        {
            var polygons = new List<Vector2[]>();
            foreach (var area in GetComponentsInChildren<Level_GrassArea>())
            {
                if (!area.isActiveAndEnabled || area.GetComponentInParent<Level_GroundSurface>() != surface) continue;
                var polygon = new Vector2[area.Nodes.Count];
                for (int i=0;i<polygon.Length;i++) { Vector3 p=area.Nodes[i];p.y=0;p=transform.InverseTransformPoint(area.transform.TransformPoint(p));polygon[i]=new Vector2(p.x,p.z); }
                if (!Level_SurfaceShape.Valid(polygon)) throw new System.InvalidOperationException("Fix invalid grass outlines before baking wear.");
                polygons.Add(polygon);
            }
            float target = Mathf.Clamp(height,0.001f,Mathf.Max(0.001f,surface.DarkHeight-0.005f));
            var next = Level_GrassLayerMesh.Build(polygons,ground.SurfaceOpenings(),ground.Size,target,0.7f);
            nextMesh = next.mesh;
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Level/GroundSurface_Mat.mat");
            if (material == null) throw new System.InvalidOperationException("Set Up Grass Layers first.");
            if (lowerObject == null)
            {
                lowerObject = new GameObject("Worn Ground Layer",typeof(MeshFilter),typeof(MeshRenderer));
                lowerObject.transform.SetParent(transform,false); lowerObject.layer = gameObject.layer;
            }
            Mesh old = data?.mesh; data = next; bakedVersion = surface.GeometryVersion;
            lowerObject.GetComponent<MeshFilter>().sharedMesh = data.mesh;
            lowerObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            lowerObject.GetComponent<MeshRenderer>().enabled = false;
            ApplyAppearance(); Level_GrassCover.ReleaseUnusedMesh(old);
            EditorUtility.SetDirty(this); PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(lowerObject.GetComponent<MeshFilter>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(lowerObject.GetComponent<MeshRenderer>());
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
        catch (System.Exception error)
        { if (nextMesh != null && nextMesh != data?.mesh) DestroyImmediate(nextMesh); Debug.LogWarning($"Grass wear bake: {error.Message}",this); }
    }
#endif
}
