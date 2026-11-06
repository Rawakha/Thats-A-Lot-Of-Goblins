using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Author light and dark grass outlines like cliffs. Each type merges into one saved flat shelf mesh, above the
// original ground and its own material. Saved horizontal bands provide exact, allocation-free height queries.
// GC: property block and registry on enable; working lists during editor baking. No runtime ticks/subscriptions.
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Level_Ground))]
public class Level_GroundSurface : MonoBehaviour
{
    [Header("Materials")]
    [Tooltip("Original base ground material, for example the grid.")]
    [SerializeField] private Material baseMaterial;
    [SerializeField] private Material grassMaterial;
    [Header("Layers")]
    [SerializeField] private Color lightGrass = new Color(0.392f, 0.514f, 0.294f);
    [SerializeField] private Color darkGrass = new Color(0.251f, 0.345f, 0.216f);
    [Tooltip("Dark shelf height above the base, in local metres.")]
    [SerializeField, Min(0.01f)] private float darkHeight = 0.06f;
    [Tooltip("Light shelf height. Always above the dark shelf.")]
    [SerializeField, Min(0.02f)] private float lightRaise = 0.18f;
    [SerializeField, Range(0.2f, 1f)] private float sideShade = 0.65f;
    [Header("Surface Colour")]
    [SerializeField, Range(0f, 1f)] private float lightingInfluence = 0.6f;
    [Tooltip("World-space size of broad colour patches, shared by layers and future grass blades.")]
    [SerializeField, Min(0.1f)] private float variationScale = 12f;
    [SerializeField, Range(0f, 0.4f)] private float variationStrength = 0.12f;
    [SerializeField, Range(0f, 0.1f)] private float detailStrength = 0.02f;
    [SerializeField] private int seed = 1;
    [SerializeField, HideInInspector] private Level_GrassLayerData darkData;
    [SerializeField, HideInInspector] private Level_GrassLayerData lightData;
    [SerializeField, HideInInspector] private GameObject darkObject;
    [SerializeField, HideInInspector] private GameObject lightObject;
    // Recognise the previous relief so a saved scene can migrate back to the original grid ground.
    [SerializeField, HideInInspector] private Mesh reliefMesh;
    [SerializeField, HideInInspector] private int geometryVersion;
    public int GeometryVersion => geometryVersion;
    public float DarkHeight => darkHeight;
    private Level_GrassWear wear;
    private MaterialPropertyBlock properties;
    private Matrix4x4 toLocal, toWorld;
    private static readonly List<Level_GroundSurface> active = new List<Level_GroundSurface>(8);

    public Level_GrassLayerData LayerData(Level_GrassArea.Kind kind) => kind == Level_GrassArea.Kind.Light ? lightData : darkData;

    // The exposed shelf only, including a conservative four-direction inset from rims and holes.
    public bool IsExposed(Level_GrassArea.Kind kind, Vector2 point, float inset)
    {
        bool At(Vector2 p) => kind == Level_GrassArea.Kind.Light ? lightData != null && lightData.Contains(p) :
            darkData != null && darkData.Contains(p) && (lightData == null || !lightData.Contains(p));
        return At(point) && (inset <= 0 || (At(point + Vector2.right * inset) && At(point - Vector2.right * inset) &&
            At(point + Vector2.up * inset) && At(point - Vector2.up * inset)));
    }

    public void SetColourProperties(MaterialPropertyBlock block, Level_GrassArea.Kind kind)
    {
        block.SetColor("_Color", kind == Level_GrassArea.Kind.Light ? lightGrass : darkGrass);
        block.SetFloat("_LightingInfluence", lightingInfluence);
        block.SetFloat("_VariationScale", variationScale);
        block.SetFloat("_VariationStrength", variationStrength);
        block.SetFloat("_DetailStrength", detailStrength);
        block.SetFloat("_Seed", seed);
        if (wear != null) wear.SetProperties(block);
        else block.SetFloat("_WearEnabled",0);
    }

    private void OnEnable()
    {
        wear = GetComponent<Level_GrassWear>();
        if (!active.Contains(this)) active.Add(this);
        ApplyReliefMesh();
        ApplyAppearance();
#if UNITY_EDITOR
        RequestRebuilds();
#endif
    }

    private void OnDisable()
    {
        active.Remove(this);
        if (darkObject != null) darkObject.SetActive(false);
        if (lightObject != null) lightObject.SetActive(false);
        if (wear != null) wear.ApplyAppearance();
        if (TryGetComponent(out Level_GrassCover cover)) cover.ApplyAppearance();
    }

    // Existing ground rebuilds call this to restore the undeformed base geometry.
    public void ApplyReliefMesh()
    {
        Mesh source = GetComponent<Level_Ground>().SourceMesh;
        if (source == null) return;
        GetComponent<MeshFilter>().sharedMesh = source;
        if (TryGetComponent(out MeshCollider collider) && collider.enabled) collider.sharedMesh = source;
        GetComponent<MeshRenderer>().SetPropertyBlock(null, 0);
    }

    public static float HeightAt(Vector3 world, float fallback, bool applyWear = true)
    {
        for (int i = 0; i < active.Count; i++)
        {
            Level_GroundSurface surface = active[i];
            if (surface == null) continue;
            Vector3 local = surface.toLocal.MultiplyPoint3x4(world);
            Vector2 point = new Vector2(local.x, local.z);
            if (surface.lightData != null && surface.lightData.Contains(point)) local.y = surface.lightData.height;
            else if (surface.darkData != null && surface.darkData.Contains(point)) local.y = surface.darkData.height;
            else continue;
            if (applyWear && surface.wear != null && surface.wear.TryHeight(local,out float worn)) local.y = worn;
            return surface.toWorld.MultiplyPoint3x4(local).y;
        }
        return fallback;
    }

    private void ApplyAppearance()
    {
        wear = GetComponent<Level_GrassWear>();
        toLocal = transform.worldToLocalMatrix;
        toWorld = transform.localToWorldMatrix;
        if (properties == null) properties = new MaterialPropertyBlock();
        ApplyLayer(darkObject, darkData, darkGrass);
        ApplyLayer(lightObject, lightData, lightGrass);
        if (wear != null) wear.ApplyAppearance();
        if (TryGetComponent(out Level_GrassCover cover) && cover.isActiveAndEnabled) cover.ApplyAppearance();
        if (baseMaterial == null) return;
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        Material[] materials = renderer.sharedMaterials;
        if (materials.Length > 0 && materials[0] != baseMaterial)
        {
            materials[0] = baseMaterial;
            renderer.sharedMaterials = materials;
        }
    }
    public void RefreshAppearance() => ApplyAppearance();

    private void ApplyLayer(GameObject layer, Level_GrassLayerData data, Color colour)
    {
        if (layer == null) return;
        layer.SetActive(isActiveAndEnabled && data != null && data.mesh != null && data.mesh.vertexCount > 0);
        if (data == null || data.mesh == null) return;
        MeshFilter filter = layer.GetComponent<MeshFilter>();
        if (filter.sharedMesh != data.mesh) filter.sharedMesh = data.mesh;
        MeshCollider collider = layer.GetComponent<MeshCollider>();
        if (data.mesh.vertexCount > 0 && collider.sharedMesh != data.mesh) collider.sharedMesh = data.mesh;
        MeshRenderer renderer = layer.GetComponent<MeshRenderer>();
        if (grassMaterial != null) renderer.sharedMaterial = grassMaterial;
        properties.Clear();
        properties.SetColor("_Color", colour);
        properties.SetFloat("_LightingInfluence", lightingInfluence);
        properties.SetFloat("_VariationScale", variationScale);
        properties.SetFloat("_VariationStrength", variationStrength);
        properties.SetFloat("_DetailStrength", detailStrength);
        properties.SetFloat("_Seed", seed);
        if (wear != null) wear.SetProperties(properties); else properties.SetFloat("_WearEnabled",0);
        renderer.SetPropertyBlock(properties);
    }

#if UNITY_EDITOR
    private static bool queued;
    [System.NonSerialized] private string builtKey;
    private void OnValidate() => RequestRebuilds();
    public static void RequestRebuilds()
    {
        if (queued || EditorApplication.isPlayingOrWillChangePlaymode) return;
        queued = true;
        EditorApplication.delayCall += RebuildChanged;
    }

    private static void RebuildChanged()
    {
        queued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (GUIUtility.hotControl != 0) { RequestRebuilds(); return; }
        foreach (Level_GroundSurface surface in FindObjectsByType<Level_GroundSurface>())
        {
            if (!surface.isActiveAndEnabled || EditorUtility.IsPersistent(surface)) continue;
            string key = surface.ShapeKey();
            bool missing = surface.darkObject == null || surface.lightObject == null ||
                surface.darkData?.mesh == null || surface.lightData?.mesh == null;
            if (surface.builtKey == null && !missing) surface.builtKey = key;
            if ((missing || surface.builtKey != key) && surface.Bake()) surface.builtKey = key;
            surface.ApplyAppearance();
        }
        Level_GrassCover.RequestRebuilds();
        Level_GrassWear.RequestRebuilds();
    }

    private Level_GrassArea[] Areas() => GetComponentsInChildren<Level_GrassArea>();
    private string ShapeKey()
    {
        var key = new System.Text.StringBuilder();
        key.Append(GetComponent<Level_Ground>().Size.ToString("F6"));
        key.Append('|').Append(darkHeight.ToString("R")).Append('|').Append(lightRaise.ToString("R")).Append('|').Append(sideShade.ToString("R"));
        foreach (Level_GrassArea area in Areas())
        {
            if (!area.isActiveAndEnabled || area.GetComponentInParent<Level_GroundSurface>() != this) continue;
            key.Append(EditorJsonUtility.ToJson(area));
            key.Append((transform.worldToLocalMatrix * area.transform.localToWorldMatrix).ToString("F6"));
        }
        foreach (Vector2[] hole in GetComponent<Level_Ground>().SurfaceOpenings())
            foreach (Vector2 point in hole) key.Append(point.ToString("F6"));
        return Hash128.Compute(key.ToString()).ToString();
    }

    [InspectorButton("Rebuild Grass Layers")]
    private void Rebuild() => Bake();
    public void RebuildRelief() => Bake();
    public bool Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !isActiveAndEnabled) return false;
        if (grassMaterial == null)
        {
            const string path = "Assets/Art/Level/GroundSurface_Mat.mat";
            grassMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (grassMaterial == null)
            {
                Shader shader = Shader.Find("Goblins/Ground Surface");
                if (shader == null) return false;
                grassMaterial = new Material(shader) { name = "GroundSurface_Mat", enableInstancing = true };
                AssetDatabase.CreateAsset(grassMaterial, path);
            }
        }
        darkHeight = Mathf.Max(0.01f, darkHeight);
        lightRaise = Mathf.Max(darkHeight + 0.01f, lightRaise);
        var dark = new List<Vector2[]>();
        var light = new List<Vector2[]>();
        foreach (Level_GrassArea area in Areas())
        {
            if (!area.isActiveAndEnabled || area.GetComponentInParent<Level_GroundSurface>() != this) continue;
            var polygon = new Vector2[area.Nodes.Count];
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector3 node = area.Nodes[i]; node.y = 0f;
                Vector3 point = transform.InverseTransformPoint(area.transform.TransformPoint(node));
                polygon[i] = new Vector2(point.x, point.z);
            }
            if (!Level_SurfaceShape.Valid(polygon))
            {
                Debug.LogWarning($"{area.name}: grass outline has crossing edges or too few distinct corners. Keeping the previous layers.", area);
                return false;
            }
            (area.Layer == Level_GrassArea.Kind.Light ? light : dark).Add(polygon);
        }
        Level_Ground ground = GetComponent<Level_Ground>();
        List<Vector2[]> holes = ground.SurfaceOpenings();
        Level_GrassLayerData nextDark = null, nextLight = null;
        try
        {
            nextDark = Level_GrassLayerMesh.Build(dark, holes, ground.Size, darkHeight, sideShade, light);
            nextLight = Level_GrassLayerMesh.Build(light, holes, ground.Size, lightRaise, sideShade, dark,
                point => nextDark.Contains(point) ? darkHeight : 0f);
        }
        catch (System.Exception error)
        {
            if (nextDark?.mesh != null) DestroyImmediate(nextDark.mesh);
            if (nextLight?.mesh != null) DestroyImmediate(nextLight.mesh);
            Debug.LogError($"{name}: grass layers could not bake. {error.Message}", this);
            return false;
        }
        Mesh oldDark = darkData?.mesh, oldLight = lightData?.mesh;
        darkData = nextDark; lightData = nextLight;
        geometryVersion++;
        RestoreBaseMaterial();
        darkObject = BindLayer(darkObject, "Dark Grass Mesh", darkData);
        lightObject = BindLayer(lightObject, "Light Grass Mesh", lightData);
        ApplyReliefMesh(); ApplyAppearance();
        DestroyUnused(oldDark); DestroyUnused(oldLight);
        if (reliefMesh != null) { DestroyUnused(reliefMesh); reliefMesh = null; }
        Level_GrassWear.RequestRebuilds();
        EditorUtility.SetDirty(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        return true;
    }

    private void RestoreBaseMaterial()
    {
        var data = new SerializedObject(GetComponent<Level_Ground>());
        Material current = data.FindProperty("groundMaterial").objectReferenceValue as Material;
        if (baseMaterial == null)
        {
            // The old surface tool preserved the original grid in the wall slot.
            baseMaterial = current != null && current.shader.name == "Goblins/Ground Surface"
                ? data.FindProperty("wallMaterial").objectReferenceValue as Material
                : current != null ? current : GetComponent<MeshRenderer>().sharedMaterial;
        }
        if (baseMaterial == null) return;
        data.FindProperty("groundMaterial").objectReferenceValue = baseMaterial;
        data.ApplyModifiedProperties();
    }

    private GameObject BindLayer(GameObject layer, string label, Level_GrassLayerData data)
    {
        if (layer == null)
        {
            layer = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            layer.transform.SetParent(transform, false);
        }
        layer.layer = gameObject.layer;
        layer.GetComponent<MeshFilter>().sharedMesh = data.mesh;
        MeshCollider collider = layer.GetComponent<MeshCollider>();
        collider.sharedMesh = null;
        if (data.mesh.vertexCount > 0) collider.sharedMesh = data.mesh;
        PrefabUtility.RecordPrefabInstancePropertyModifications(layer.GetComponent<MeshFilter>());
        PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        return layer;
    }

    private void DestroyUnused(Mesh mesh)
    {
        if (mesh == null || EditorUtility.IsPersistent(mesh)) return;
        foreach (Level_GroundSurface surface in FindObjectsByType<Level_GroundSurface>(FindObjectsInactive.Include))
            if (surface != this && (surface.darkData?.mesh == mesh || surface.lightData?.mesh == mesh || surface.reliefMesh == mesh)) return;
        DestroyImmediate(mesh);
    }

    [InitializeOnLoadMethod]
    private static void Watch()
    {
        Undo.undoRedoPerformed += RequestRebuilds;
        EditorApplication.hierarchyChanged += RequestRebuilds;
        Undo.postprocessModifications += OnModified;
    }
    private static UndoPropertyModification[] OnModified(UndoPropertyModification[] modifications)
    {
        foreach (UndoPropertyModification modification in modifications)
            if (modification.currentValue.target is Transform || modification.currentValue.target is Level_Ground || modification.currentValue.target is Level_Hole)
            { RequestRebuilds(); break; }
        return modifications;
    }
#endif
}
