using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Simple billboard grass and flowers on the existing merged shelves. Edit the source card GameObjects, then
// bake their real meshes/materials/transforms into spatial chunks. Unity culls the saved MeshRenderers; wind,
// distance shrinking and billboarding run in the shader. No CPU frame work, runtime subscriptions or frame GC.
// On enable: one property block and renderer arrays. All scatter/mesh working allocations are editor-only.
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Level_GroundSurface))]
public class Level_GrassCover : MonoBehaviour
{
    [Serializable] public class Layer
    {
        public bool enabled = true;
        [Header("Grass Carpet")]
        [Tooltip("Rectangular cards per square metre. Keep them short so goblins remain readable.")]
        [Min(0)] public float grassDensity = 20f;
        [Tooltip("Additional random width and height multipliers, independent of overall Size.")]
        public Vector2 grassWidth = Vector2.one;
        [Tooltip("Independent height range for each blade, multiplied by Overall Size and the source Height.")]
        public Vector2 grassHeight = Vector2.one;
        [Tooltip("Random tip width per blade, relative to the authored source tip. 1 keeps its shape, 0 makes a point. Roots stay unchanged.")]
        public Vector2 tipWidth = new Vector2(0.7f, 0.9f);
        [Tooltip("How much of the shelf has grass. Lower values reveal broad bare patches.")]
        [Range(0, 1)] public float grassCoverage = 1f;
        [Min(0.1f)] public float patchSize = 4f;
        [Tooltip("Softness of the transition from dense patches to bare ground.")]
        [Range(0.01f, 0.5f)] public float patchSoftness = 0.2f;
        [Tooltip("Shared height changes across broad patches, rather than unrelated noisy blades.")]
        [Range(0, 0.8f)] public float patchHeight = 0f;
        [Tooltip("Scatter irregularity. Zero gives an evenly spaced grid, one jitters within each cell.")]
        [Range(0, 1)] public float scatterJitter = 0.7f;
        [Tooltip("Random roll and forward lean in degrees, added to the authored source transform.")]
        [Range(0, 35)] public float roll = 0f;
        [Range(0, 35)] public float lean = 0f;
        [Header("Flowers")]
        [Tooltip("Flowers per square metre before patchiness. Zero disables them.")]
        [Min(0)] public float flowerDensity = 0.18f;
        [Range(0, 1)] public float flowerPatchiness = 0.65f;
        [Min(0.1f)] public float flowerPatchSize = 5.25f;
        [Tooltip("Uniform flower size range, separate from the grass. The source card sets stem/head dimensions.")]
        public Vector2 flowerSize = Vector2.one;
        [Header("Overall Scale")]
        public Vector2 size = new Vector2(0.8f, 1.2f);
    }
    [Header("Editable Source GameObjects")]
    [SerializeField] private Level_GrassCard grassCard;
    [SerializeField] private Level_GrassCard flowerCard;
    [SerializeField, HideInInspector] private GameObject templates;
    [SerializeField, HideInInspector] private GameObject output;
    [Header("Shelf Coverage")]
    [SerializeField] private Layer light = new Layer();
    [SerializeField] private Layer dark = new Layer { grassDensity = 12f, flowerDensity = 0.1f };
    [Tooltip("Keeps card roots inside the shelf edge and away from holes, in ground-local metres.")]
    [SerializeField, Min(0)] private float edgeInset = 0.08f;
    [SerializeField] private int seed = 37;
    [Header("Colours (per ground / area)")]
    [Tooltip("Multiplies the matching shelf colour at the tips. White keeps the same hue.")]
    [SerializeField, ColorUsage(false, true)] private Color tipTint = new Color(1.15f, 1.2f, 1.05f);
    [SerializeField] private Color flowerColour = new Color(1f, 0.6f, 0.77f);
    [SerializeField] private Color flowerCentre = new Color(1f, 0.85f, 0.4f);
    [Tooltip("Optional independent grass colour. Ground Match=1 keeps roots matching the shelf exactly.")]
    [SerializeField] private Color grassColour = new Color(0.42f, 0.34f, 0.62f);
    [SerializeField, Range(0, 1)] private float groundMatch = 1f;
    [SerializeField, Range(0.2f, 4)] private float gradientPower = 1f;
    [Tooltip("Random brightness per card. Keep low for a clean carpet.")]
    [SerializeField, Range(0, 0.4f)] private float colourVariation = 0f;
    [SerializeField, ColorUsage(false, true)] private Color alternateTipTint = new Color(1.25f, 1.1f, 1.3f);
    [SerializeField, Range(0, 1)] private float tipVariation = 0f;
    [SerializeField] private Color alternateFlowerColour = new Color(1f, 0.85f, 0.55f);
    [Tooltip("Fraction of flowers using the alternate colour.")]
    [SerializeField, Range(0, 1)] private float alternateFlowerChance = 0f;
    [Header("Wind and Drawing")]
    [SerializeField, Range(0, 0.3f)] private float wind = 0.045f;
    [SerializeField, Range(0, 4)] private float windSpeed = 1.2f;
    [SerializeField] private Vector2 windDirection = new Vector2(1, 0.3f);
    [SerializeField, Min(0.1f)] private float windWaveSize = 12f;
    [SerializeField, Range(0, 1)] private float flutter = 0f;
    public enum Facing { Upright, Camera, Authored }
    [Tooltip("Upright turns towards camera yaw; Camera also tilts towards the view; Authored keeps the source orientation.")]
    [SerializeField] private Facing grassFacing = Facing.Upright;
    [SerializeField] private Facing flowerFacing = Facing.Camera;
    [SerializeField, Range(0, 1)] private float shadowStrength = 1f;
    [SerializeField, Min(2)] private float chunkSize = 8f;
    [SerializeField, Min(1)] private float drawDistance = 90f;
    [Tooltip("Hard budget shared by both layers, grass and flowers. Exceeding it retains the previous bake.")]
    [SerializeField, Range(100, 200000)] private int maxCards = 80000;
    [SerializeField, HideInInspector] private int cardCount;
    [SerializeField, HideInInspector] private int grassCount, flowerCount;
    [SerializeField, HideInInspector] private int bakedSurfaceVersion = -1;
    public int CardCount => cardCount;
    public int GrassCount => grassCount;
    public int FlowerCount => flowerCount;
    public GameObject Templates => templates;
    public bool HasSourceCards => grassCard != null && flowerCard != null && grassCard != flowerCard;
    private MaterialPropertyBlock properties;

    private void OnEnable()
    {
        if (Application.isPlaying && templates != null) templates.SetActive(false);
        if (output != null) output.SetActive(true);
        ApplyAppearance();
#if UNITY_EDITOR
        RequestRebuilds();
#endif
    }
    private void OnDisable() { if (output != null) output.SetActive(false); }

    public void ApplyAppearance()
    {
        if (properties == null) properties = new MaterialPropertyBlock();
        Level_GroundSurface surface = GetComponent<Level_GroundSurface>();
        void Fill(Level_GrassArea.Kind kind)
        {
            properties.Clear();
            surface.SetColourProperties(properties, kind);
            properties.SetColor("_TipTint", tipTint);
            properties.SetColor("_FlowerColour", flowerColour);
            properties.SetColor("_FlowerCentre", flowerCentre);
            properties.SetFloat("_WindStrength", wind);
            properties.SetFloat("_WindSpeed", windSpeed);
            properties.SetFloat("_DrawDistance", Mathf.Max(1, drawDistance));
            properties.SetColor("_GrassColour", grassColour);
            properties.SetFloat("_GroundMatch", groundMatch);
            properties.SetFloat("_GradientPower", gradientPower);
            properties.SetFloat("_ColourVariation", colourVariation);
            properties.SetColor("_AlternateTipTint", alternateTipTint);
            properties.SetFloat("_TipVariation", tipVariation);
            properties.SetColor("_AlternateFlowerColour", alternateFlowerColour);
            properties.SetFloat("_AlternateFlowerChance", alternateFlowerChance);
            Vector2 direction = windDirection.sqrMagnitude > 0.0001f ? windDirection.normalized : Vector2.right;
            properties.SetVector("_WindDirection", new Vector4(direction.x, direction.y, 0, 0));
            properties.SetFloat("_WindWaveSize", Mathf.Max(0.1f, windWaveSize));
            properties.SetFloat("_Flutter", flutter);
            properties.SetFloat("_GrassFacing", (float)grassFacing);
            properties.SetFloat("_FlowerFacing", (float)flowerFacing);
            properties.SetFloat("_ShadowStrength", shadowStrength);
        }
        Fill(Level_GrassArea.Kind.Light);
        if (grassCard != null) grassCard.GetComponent<MeshRenderer>().SetPropertyBlock(properties);
        if (flowerCard != null) flowerCard.GetComponent<MeshRenderer>().SetPropertyBlock(properties);
        if (output == null) return;
        output.SetActive(isActiveAndEnabled && surface.isActiveAndEnabled && bakedSurfaceVersion == surface.GeometryVersion);
        // Allocates only on enable / editor changes, never every frame.
        foreach (Transform layer in output.transform)
        {
            Fill(layer.name == "Light" ? Level_GrassArea.Kind.Light : Level_GrassArea.Kind.Dark);
            foreach (MeshRenderer renderer in layer.GetComponentsInChildren<MeshRenderer>(true)) renderer.SetPropertyBlock(properties);
        }
    }

#if UNITY_EDITOR
    private static bool queued;
    // Removing just the cover component also removes its derived output. Source GameObjects remain authored.
    // Delay avoids mutating the hierarchy while Unity is undoing or unloading a component.
    private void OnDestroy()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject previous = output;
        EditorApplication.delayCall += () =>
        {
            if (this != null || previous == null || !previous.scene.IsValid() || !previous.scene.isLoaded) return;
            var meshes = new List<Mesh>();
            foreach (MeshFilter filter in previous.GetComponentsInChildren<MeshFilter>(true)) meshes.Add(filter.sharedMesh);
            DestroyImmediate(previous);
            ReleaseUnusedMeshes(meshes);
        };
    }
    [SerializeField, HideInInspector] private string builtKey;
    [NonSerialized] private string failedKey;
    private void OnValidate() => RequestRebuilds();
    public static void RequestRebuilds()
    {
        if (queued || EditorApplication.isPlayingOrWillChangePlaymode) return;
        queued = true;
        EditorApplication.delayCall += Refresh;
    }
    private static void Refresh()
    {
        queued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (GUIUtility.hotControl != 0) { RequestRebuilds(); return; }
        foreach (Level_GrassCover cover in FindObjectsByType<Level_GrassCover>())
        {
            if (!cover.isActiveAndEnabled || EditorUtility.IsPersistent(cover)) continue;
            // A newly added cover, or deliberately deleted source cards, is an authoring setup state.
            // Keep any saved output; explicit setup/bake buttons remain available in the inspector.
            if (!cover.HasSourceCards) { cover.ApplyAppearance(); continue; }
            if (!cover.RepairSourceShapes()) { cover.ApplyAppearance(); continue; }
            string key = cover.ShapeKey();
            if ((cover.output == null || cover.builtKey != key || cover.cardCount != cover.grassCount + cover.flowerCount ||
                cover.bakedSurfaceVersion != cover.GetComponent<Level_GroundSurface>().GeometryVersion) && cover.failedKey != key)
            {
                if (cover.Bake()) cover.builtKey = key;
                else cover.failedKey = key;
            }
            cover.ApplyAppearance();
        }
    }
    private string ShapeKey()
    {
        var key = new System.Text.StringBuilder();
        key.Append(JsonUtility.ToJson(light)).Append(JsonUtility.ToJson(dark));
        key.Append('|').Append(edgeInset).Append('|').Append(seed).Append('|').Append(chunkSize).Append('|').Append(maxCards);
        Level_GroundSurface surface = GetComponent<Level_GroundSurface>();
        key.Append('|').Append(surface.GeometryVersion);
        foreach (Level_GrassCard card in new[] { grassCard, flowerCard })
        {
            if (card == null) continue;
            key.Append((transform.worldToLocalMatrix * card.transform.localToWorldMatrix).ToString("F6"));
            Mesh mesh = card.GetComponent<MeshFilter>().sharedMesh;
            key.Append('|').Append(mesh != null).Append('|').Append(mesh != null && mesh.isReadable);
            if (mesh != null) key.Append('|').Append(mesh.subMeshCount);
            if (mesh != null && mesh.isReadable)
            {
                foreach (Vector3 vertex in mesh.vertices) key.Append(vertex.ToString("F6"));
                foreach (Vector2 uv in mesh.uv) key.Append(uv.ToString("F6"));
                foreach (int index in mesh.triangles) key.Append('|').Append(index);
                var kinds = new List<Vector2>(); mesh.GetUVs(2, kinds);
                foreach (Vector2 value in kinds) key.Append(value.ToString("F6"));
            }
            Material material = card.GetComponent<MeshRenderer>().sharedMaterial;
            key.Append(AssetDatabase.GetAssetPath(material));
            if (material != null) key.Append('|').Append(material.shader != null ? material.shader.name : "");
        }
        return Hash128.Compute(key.ToString()).ToString();
    }

    public void SetUpSources(Material material)
    {
        failedKey = null;
        if (templates == null)
        {
            templates = new GameObject("Editable Grass and Flower Cards");
            Undo.RegisterCreatedObjectUndo(templates, "Create Grass Sources");
            templates.transform.SetParent(transform, false);
        }
        Level_GrassCard Create(string label, Level_GrassCard.Shape shape, float x)
        {
            var source = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(source, "Create Grass Card");
            source.transform.SetParent(templates.transform, false);
            source.transform.localPosition = new Vector3(x, 0, 0);
            var card = Undo.AddComponent<Level_GrassCard>(source);
            card.Configure(shape, material);
            return card;
        }
        if (grassCard == null) grassCard = Create("Grass Card", Level_GrassCard.Shape.Grass, 0);
        if (flowerCard == null) flowerCard = Create("Flower Card", Level_GrassCard.Shape.Flower, 0);
        templates.SetActive(false);
        EditorUtility.SetDirty(this);
        RequestRebuilds();
    }

    public bool TryGetSourceShape(Level_GrassCard card, out Level_GrassCard.Shape shape)
    {
        shape = card == flowerCard ? Level_GrassCard.Shape.Flower : Level_GrassCard.Shape.Grass;
        return card != null && (card == grassCard || card == flowerCard);
    }

    private bool RepairSourceShapes()
    {
        if (grassCard != null && grassCard == flowerCard)
        {
            Debug.LogWarning("Grass Card and Flower Card must be separate source GameObjects.", this);
            return false;
        }
        void Repair(Level_GrassCard card, Level_GrassCard.Shape kind)
        {
            if (card == null || card.CardShape == kind) return;
            Undo.RecordObject(card, "Repair Grass Source Role");
            card.SetShape(kind);
            Debug.Log($"{card.name}: restored {kind} shape for its cover slot; kept authored size and transform.", card);
        }
        Repair(grassCard, Level_GrassCard.Shape.Grass);
        Repair(flowerCard, Level_GrassCard.Shape.Flower);
        return true;
    }

    // Optional starting point for the reference's wider, short, irregular carpet. Keep authored palettes/sources.
    public void ApplyReferenceCarpet()
    {
        foreach (Layer layer in new[] { light, dark })
        {
            layer.grassWidth = new Vector2(1.2f, 2.1f);
            layer.grassHeight = new Vector2(0.7f, 1.35f);
            layer.tipWidth = new Vector2(0.65f, 0.85f);
            layer.grassCoverage = 0.92f; layer.patchSize = 4f; layer.patchSoftness = 0.25f;
            layer.patchHeight = 0.25f; layer.scatterJitter = 1f;
            layer.roll = 12f; layer.lean = 9f;
            layer.flowerSize = new Vector2(1.15f, 1.7f);
            layer.flowerPatchSize = 4f; layer.flowerPatchiness = 0.8f;
        }
        light.grassDensity = 28; dark.grassDensity = 20;
        light.flowerDensity = 0.3f; dark.flowerDensity = 0.15f;
        gradientPower = 1.5f; colourVariation = 0.04f;
        wind = 0.035f; flutter = 0.2f;
        RequestRebuilds();
    }

    private class Source
    {
        public Vector3[] vertices;
        public Vector2[] uv, kinds;
        public int[] triangles;
        public Matrix4x4 matrix;
        public Material material;
        public float reach;
        public float tipCentreX;
    }
    private Source ReadSource(Level_GrassCard card)
    {
        if (card == null) throw new InvalidOperationException("Set up both editable source cards first.");
        Mesh mesh = card.GetComponent<MeshFilter>().sharedMesh;
        Material material = card.GetComponent<MeshRenderer>().sharedMaterial;
        if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0 || mesh.vertexCount > 128 || mesh.subMeshCount != 1 || material == null || material.shader.name != "Goblins/Grass Cards")
            throw new InvalidOperationException("Each source needs a readable single-submesh card and a Goblins/Grass Cards material.");
        var kinds = new List<Vector2>(); mesh.GetUVs(2, kinds);
        var source = new Source { vertices = mesh.vertices, uv = mesh.uv, kinds = kinds.ToArray(), triangles = mesh.triangles,
            matrix = transform.worldToLocalMatrix * card.transform.localToWorldMatrix, material = material };
        if (source.uv.Length != source.vertices.Length || source.kinds.Length != source.vertices.Length)
            throw new InvalidOperationException("Card mesh is missing UVs. Rebuild its source card.");
        // Taper around the authored top centre, preserving deliberate tip lean/slant rather than pulling it
        // back to the object's origin. Generated grass has two top vertices with UV.y = 1.
        float top = float.NegativeInfinity;
        for (int i = 0; i < source.uv.Length; i++) top = Mathf.Max(top, source.uv[i].y);
        int topCount = 0;
        for (int i = 0; i < source.vertices.Length; i++)
            if (source.uv[i].y >= top - 0.0001f) { source.tipCentreX += source.vertices[i].x; topCount++; }
        if (topCount > 0) source.tipCentreX /= topCount;
        // Taper can move a slanted tip horizontally. Bound the source box, including those interior positions.
        Bounds sourceBounds = new Bounds(source.vertices[0], Vector3.zero);
        foreach (Vector3 vertex in source.vertices) sourceBounds.Encapsulate(vertex);
        Vector3 min = sourceBounds.min, max = sourceBounds.max;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 vertex = new Vector3((corner & 1) == 0 ? min.x : max.x,
                (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z);
            source.reach = Mathf.Max(source.reach, source.matrix.MultiplyPoint3x4(vertex).magnitude);
        }
        return source;
    }
    private class Chunk
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>(), kinds = new List<Vector2>();
        public readonly List<Vector3> pivots = new List<Vector3>();
        public readonly List<int> triangles = new List<int>();
        public Material material;
        public Bounds bounds;
    }

    public bool Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !isActiveAndEnabled) return false;
        GameObject next = null;
        var madeMeshes = new List<Mesh>();
        try
        {
            if (!RepairSourceShapes()) return false;
            Source grass = ReadSource(grassCard), flower = ReadSource(flowerCard);
            Level_GroundSurface surface = GetComponent<Level_GroundSurface>();
            next = new GameObject("Baked Grass and Flowers"); next.SetActive(false);
            next.transform.SetParent(transform, false);
            int total = 0, attempts = 0, vertexTotal = 0;
            int nextGrassCount = 0, nextFlowerCount = 0;
            foreach (Level_GrassArea.Kind kind in new[] { Level_GrassArea.Kind.Dark, Level_GrassArea.Kind.Light })
            {
                Layer settings = kind == Level_GrassArea.Kind.Light ? light : dark;
                if (!settings.enabled) continue;
                Level_GrassLayerData data = surface.LayerData(kind);
                if (data == null || data.spans.Length == 0) continue;
                var layer = new GameObject(kind.ToString()); layer.transform.SetParent(next.transform, false);
                var chunks = new Dictionary<Vector3Int, Chunk>();
                for (int type = 0; type < 2; type++)
                {
                    Source source = type == 0 ? grass : flower;
                    float density = type == 0 ? settings.grassDensity : settings.flowerDensity;
                    if (!float.IsFinite(chunkSize) || chunkSize < 2)
                        throw new InvalidOperationException("Use chunk size at least 2 metres.");
                    int cardType = type;
                    Level_GrassScatter.Visit(data, density, seed + type * 7919, ref attempts, (point, x, z) =>
                    {
                        if (!surface.IsExposed(kind, point, edgeInset)) return;
                        float Random(int channel) => Level_GrassScatter.Unit(Level_GrassScatter.Hash(x, z, seed + channel));
                        float patch = Mathf.PerlinNoise(point.x / Mathf.Max(0.1f, settings.patchSize) + seed,
                            point.y / Mathf.Max(0.1f, settings.patchSize));
                        float coverage = settings.grassCoverage >= 1 ? 1 : settings.grassCoverage <= 0 ? 0 :
                            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1 - settings.grassCoverage - settings.patchSoftness,
                                1 - settings.grassCoverage + settings.patchSoftness, patch));
                        if (cardType == 0 && Random(2) >= coverage) return;
                        float flowerPatch = Mathf.PerlinNoise(point.x / Mathf.Max(0.1f, settings.flowerPatchSize) + seed,
                            point.y / Mathf.Max(0.1f, settings.flowerPatchSize));
                        if (cardType == 1 && Random(3) > Mathf.Lerp(1, Mathf.SmoothStep(0, 1, flowerPatch), settings.flowerPatchiness)) return;
                        if (++total > maxCards) throw new InvalidOperationException($"More than {maxCards} cards. Reduce density or increase Max Cards.");
                        if (cardType == 0) nextGrassCount++; else nextFlowerCount++;
                        vertexTotal += source.vertices.Length;
                        if (vertexTotal > 2000000) throw new InvalidOperationException("Cover exceeds two million vertices. Reduce density or source complexity.");
                        float scale = RandomRange(settings.size, Random(4));
                        Vector3 stretch = Vector3.one;
                        Quaternion rotation = Quaternion.identity;
                        float tipWidth = 1f;
                        if (cardType == 0)
                        {
                            stretch.x = RandomRange(settings.grassWidth, Random(5));
                            stretch.y = RandomRange(settings.grassHeight, Random(6)) * (1 + (patch * 2 - 1) * settings.patchHeight);
                            rotation = Quaternion.Euler((Random(7) * 2 - 1) * settings.lean, 0, (Random(8) * 2 - 1) * settings.roll);
                            tipWidth = Mathf.Lerp(Mathf.Clamp01(settings.tipWidth.x), Mathf.Clamp01(settings.tipWidth.y), Random(10));
                        }
                        else scale *= RandomRange(settings.flowerSize, Random(9));
                        Vector3 root = new Vector3(point.x, data.height + 0.002f, point.y);
                        var cell = new Vector3Int(Mathf.FloorToInt(root.x / chunkSize), Mathf.FloorToInt(root.z / chunkSize), cardType);
                        if (!chunks.TryGetValue(cell, out Chunk chunk))
                        {
                            chunk = new Chunk { material = source.material, bounds = new Bounds(root, Vector3.zero) };
                            chunks.Add(cell, chunk);
                        }
                        chunk.bounds.Encapsulate(root);
                        int start = chunk.vertices.Count;
                        for (int v = 0; v < source.vertices.Length; v++)
                        {
                            Vector3 vertex = source.vertices[v];
                            float taper = Mathf.Lerp(1f, tipWidth, Mathf.Clamp01(source.uv[v].y));
                            vertex.x = source.tipCentreX + (vertex.x - source.tipCentreX) * taper;
                            chunk.vertices.Add(root + rotation * Vector3.Scale(source.matrix.MultiplyPoint3x4(vertex), stretch) * scale);
                        }
                        for (int v = 0; v < source.vertices.Length; v++)
                        { chunk.uv.Add(source.uv[v]); chunk.pivots.Add(root); chunk.kinds.Add(source.kinds[v]); }
                        foreach (int index in source.triangles) chunk.triangles.Add(start + index);
                    }, settings.scatterJitter);
                }
                foreach (var pair in chunks)
                {
                    Chunk chunk = pair.Value;
                    var mesh = new Mesh { name = kind + " Grass Cards", indexFormat = chunk.vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                    madeMeshes.Add(mesh);
                    mesh.SetVertices(chunk.vertices); mesh.SetUVs(0, chunk.uv); mesh.SetUVs(1, chunk.pivots);
                    mesh.SetUVs(2, chunk.kinds); mesh.SetTriangles(chunk.triangles, 0);
                    Bounds bounds = chunk.bounds;
                    // Conservative for any camera yaw, authored rotation/offset, size and maximum wind.
                    float grassScale = Mathf.Max(MaxRange(settings.grassWidth), MaxRange(settings.grassHeight) * (1 + settings.patchHeight), 1);
                    float reach = Mathf.Max(grass.reach * grassScale, flower.reach * MaxRange(settings.flowerSize)) * MaxRange(settings.size);
                    bounds.Expand((reach + 0.65f) * 2);
                    mesh.bounds = bounds;
                    var obj = new GameObject(pair.Key.z == 0 ? "Grass Chunk" : "Flower Chunk", typeof(MeshFilter), typeof(MeshRenderer));
                    obj.layer = gameObject.layer; obj.transform.SetParent(layer.transform, false);
                    obj.GetComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial = chunk.material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
            }
            GameObject previous = output;
            MeshFilter[] oldFilters = previous != null ? previous.GetComponentsInChildren<MeshFilter>(true) : Array.Empty<MeshFilter>();
            var oldMeshes = new List<Mesh>();
            foreach (MeshFilter filter in oldFilters) oldMeshes.Add(filter.sharedMesh);
            output = next; cardCount = total;
            grassCount = nextGrassCount; flowerCount = nextFlowerCount;
            bakedSurfaceVersion = surface.GeometryVersion;
            output.SetActive(surface.isActiveAndEnabled);
            if (previous != null) DestroyImmediate(previous);
            ReleaseUnusedMeshes(oldMeshes);
            ApplyAppearance(); builtKey = ShapeKey();
            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            failedKey = null;
            return true;
        }
        catch (Exception error)
        {
            if (next != null && next != output) DestroyImmediate(next);
            if (next != output)
                foreach (Mesh mesh in madeMeshes) if (mesh != null) DestroyImmediate(mesh);
            Debug.LogWarning($"{name}: grass cover kept its previous bake. {error.Message}", this);
            return false;
        }
    }
    private static float RandomRange(Vector2 range, float t) => Mathf.Lerp(Mathf.Max(0.01f, range.x), Mathf.Max(0.01f, range.y), t);
    private static float MaxRange(Vector2 range) => Mathf.Max(0.01f, range.x, range.y);
    public static void ReleaseUnusedMesh(Mesh mesh)
    {
        if (mesh == null || EditorUtility.IsPersistent(mesh)) return;
        foreach (MeshFilter filter in FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            if (filter.sharedMesh == mesh) return;
        foreach (MeshCollider collider in FindObjectsByType<MeshCollider>(FindObjectsInactive.Include))
            if (collider.sharedMesh == mesh) return;
        DestroyImmediate(mesh);
    }

    private static void ReleaseUnusedMeshes(List<Mesh> meshes)
    {
        var unused = new HashSet<Mesh>(meshes);
        foreach (MeshFilter filter in FindObjectsByType<MeshFilter>(FindObjectsInactive.Include)) unused.Remove(filter.sharedMesh);
        foreach (MeshCollider collider in FindObjectsByType<MeshCollider>(FindObjectsInactive.Include)) unused.Remove(collider.sharedMesh);
        foreach (Mesh mesh in unused)
            if (mesh != null && !EditorUtility.IsPersistent(mesh)) DestroyImmediate(mesh);
    }

    [InitializeOnLoadMethod]
    private static void Watch()
    {
        Undo.undoRedoPerformed += RequestRebuilds;
        Undo.postprocessModifications += modifications =>
        {
            foreach (var modification in modifications)
                if (modification.currentValue.target is Transform || modification.currentValue.target is MeshFilter || modification.currentValue.target is MeshRenderer)
                { RequestRebuilds(); break; }
            return modifications;
        };
    }
#endif
}

