using System.Collections.Generic;
using Destruct_FractureCore;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Pre-fractures a mesh in Edit mode, like Blender's Cell Fracture: Voronoi cells cut from the mesh, with capped
// interiors, optional recursion, colliders and rigidbodies. Add it to any object with a MeshFilter, set it up,
// press Fracture. Nothing is cut at runtime: the shards are baked into a hidden child and Destruct_Piece swaps them
// in when the object is hit (crack, break off, shatter).
//
// Ported from the Imps project's MeshFracture. The cutting maths and editor tools are unchanged; the runtime moved to
// Destruct_Piece and Destruct_System so it follows UpdateManager and the debris budget.
//
// The mesh should be closed (watertight) for clean interior faces. Open meshes still cut, but some caps will be missing.
//
// GC: none at runtime. Awake only resets the shards to the intact pose. No subscription.
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Destruct_Fracture : MonoBehaviour
{
    public enum PointSource
    {
        Uniform,        // seeds scattered evenly through the volume
        Impact,         // seeds clustered around the impact point: small shards there, big chunks further away
        MeshVertices    // seeds on the mesh's own vertices (Blender's "Own Verts")
    }

    public enum ColliderMode
    {
        None,
        ConvexMesh,
        Box
    }

    [Header("Shards")]
    [Min(2)] public int shardCount = 24;
    public PointSource pointSource = PointSource.Uniform;
    public int seed = 1;
    [Tooltip("Random jitter on the seed points. Higher = more irregular shard sizes.")]
    [Range(0f, 1f)] public float noise = 0f;
    [Tooltip("Squashes the seed points along each axis. Lower on an axis = shards stretched along it (e.g. Y = 0.3 for tall splinters).")]
    public Vector3 pointScale = Vector3.one;

    [Header("Impact (Point Source = Impact)")]
    [Tooltip("Local space. Drag the handle in the Scene view.")]
    public Vector3 impactPoint;
    [Min(0.001f)] public float impactRadius = 0.5f;
    [Tooltip("Higher = seeds packed tighter around the impact point.")]
    [Range(1f, 4f)] public float impactConcentration = 2f;
    [Tooltip("Share of seeds spread over the whole mesh instead of around the impact.")]
    [Range(0f, 1f)] public float uniformFill = 0.25f;

    [Header("Recursion")]
    [Tooltip("Fracture some shards again, Blender style.")]
    [Range(0, 3)] public int recursionLevels = 0;
    [Min(2)] public int recursionShards = 4;
    [Tooltip("Share of shards fractured again at each level.")]
    [Range(0f, 1f)] public float recursionChance = 0.25f;
    [Tooltip("Pick the largest shards to fracture again instead of random ones.")]
    public bool recursionPreferLarge = true;

    [Header("Cut")]
    [Tooltip("Gap between neighbouring shards, in local units.")]
    [Min(0f)] public float margin = 0f;
    [Tooltip("Separate pieces of a shard that aren't connected (happens on concave meshes).")]
    public bool splitIslands = true;
    [Tooltip("Shards smaller than this share of the total volume are removed.")]
    [Range(0f, 0.05f)] public float minShardVolume = 0f;

    [Header("Interior")]
    [Tooltip("Material for the cut faces. Empty = the mesh's first material.")]
    public Material interiorMaterial;
    public float interiorUVScale = 1f;

    [Header("Physics")]
    public ColliderMode colliders = ColliderMode.ConvexMesh;
    public bool addRigidbodies = true;
    [Tooltip("Mass per cubic metre. Shard mass = density × volume.")]
    [Min(0.001f)] public float density = 500f;

    [Header("Output")]
    [Tooltip("Store the shard meshes in an asset, so prefabs keep them.")]
    public bool saveMeshesAsAsset = true;
    public string assetFolder = "Assets/Art/Fractured Meshes";
    [Tooltip("Merge every face, cut faces included, into one submesh with the mesh's first material. Needed to use the shards in a Particle System. Re-fracture after changing.")]
    public bool singleSubmesh = false;

    // Written by the editor tools; a player build only reads some of them
#pragma warning disable 0169, 0414, 0649
    [SerializeField, HideInInspector] private GameObject fracturedRoot;
    [SerializeField, HideInInspector] private List<Transform> shards = new List<Transform>();
    [SerializeField, HideInInspector] private List<Vector3> shardCentres = new List<Vector3>();
    [SerializeField, HideInInspector] private Vector3 meshCentre;
    [SerializeField, HideInInspector] private string savedAssetPath;
    [SerializeField, HideInInspector] private bool previewing;
    [SerializeField, HideInInspector] private float explode;
    [SerializeField, HideInInspector] private float cullAmount;
    [SerializeField, HideInInspector] private int cullSeed = 1;
    [SerializeField, HideInInspector] private float cullInner = 0.5f;
    [SerializeField, HideInInspector] private Mesh crackMesh;

    // Support data, baked with the shards: each shard's bounds in this object's space, and which shards share a face
    // (neighbours of shard i are neighbours[neighbourStarts[i] .. neighbourStarts[i + 1]])
    [SerializeField, HideInInspector] private List<Vector3> shardMins = new List<Vector3>();
    [SerializeField, HideInInspector] private List<Vector3> shardMaxs = new List<Vector3>();
    [SerializeField, HideInInspector] private List<int> neighbourStarts = new List<int>();
    [SerializeField, HideInInspector] private List<int> neighbours = new List<int>();
#pragma warning restore 0169, 0414, 0649

    public bool IsFractured => fracturedRoot != null;
    public int ShardCount => shards.Count;
    public GameObject FracturedRoot => fracturedRoot;

    // The baked shards and where each sits in this object's space when intact. Entries can be null if deleted.
    public List<Transform> Shards => shards;
    public List<Vector3> ShardCentres => shardCentres;

    // Centre of the intact mesh, in this object's space: shards crack outward from it
    public Vector3 MeshCentre => meshCentre;

    // Every shard merged into one mesh for the CrackShatter shader (Bake Crack Mesh). Null until baked.
    public Mesh CrackMesh => crackMesh;

    public bool HasSupports => neighbourStarts.Count == shards.Count + 1 && shardMins.Count == shards.Count;
    public List<Vector3> ShardMins => shardMins;
    public List<Vector3> ShardMaxs => shardMaxs;
    public List<int> NeighbourStarts => neighbourStarts;
    public List<int> Neighbours => neighbours;
    public int LinkCount => neighbours.Count / 2;

    #region Runtime
    private void Awake()
    {
        if (!Application.isPlaying || fracturedRoot == null)
            return;

        // Start intact, whatever the editor preview was showing
        for (int i = 0; i < shards.Count && i < shardCentres.Count; i++)
        {
            if (shards[i] != null)
                shards[i].SetLocalPositionAndRotation(shardCentres[i], Quaternion.identity);
        }

        fracturedRoot.SetActive(false);

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = true;
    }
    #endregion

    #region Supports
    // Works out which shards touch, for Destruct_Piece's support check. Voronoi neighbours share a cut face, so two
    // shards are linked when at least three distinct vertex positions of one sit on the other (an edge or a corner
    // alone gives two or one, so shards meeting only diagonally aren't linked). Done when fracturing; at runtime only
    // as a fallback for objects fractured before supports existed.
    // GC: allocates working sets; editor or load time only.
    public void BuildSupports()
    {
        int count = shards.Count;
        shardMins.Clear();
        shardMaxs.Clear();
        neighbourStarts.Clear();
        neighbours.Clear();

        MeshFilter source = GetComponent<MeshFilter>();
        float size = source != null && source.sharedMesh != null ? source.sharedMesh.bounds.size.magnitude : 1f;

        // Cut faces are computed separately for each cell, so matching vertices only nearly match. The margin is the
        // gap left between shards.
        float cell = Mathf.Max(margin * 1.5f + size * 0.002f, 1e-5f);

        var keys = new HashSet<Vector3Int>[count];
        var grid = new Dictionary<Vector3Int, List<int>>();

        for (int i = 0; i < count; i++)
        {
            keys[i] = new HashSet<Vector3Int>();
            Vector3 centre = i < shardCentres.Count ? shardCentres[i] : Vector3.zero;
            Mesh mesh = shards[i] != null && shards[i].TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;

            if (mesh == null)
            {
                shardMins.Add(centre);
                shardMaxs.Add(centre);
                continue;
            }

            Bounds bounds = mesh.bounds;
            shardMins.Add(bounds.min + centre);
            shardMaxs.Add(bounds.max + centre);

            // Shard meshes are built around their own centre, and the shard sits at that centre
            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector3 p = (vertex + centre) / cell;
                Vector3Int key = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));

                if (!keys[i].Add(key))
                    continue;

                if (!grid.TryGetValue(key, out List<int> owners))
                {
                    owners = new List<int>(2);
                    grid.Add(key, owners);
                }

                owners.Add(i);
            }
        }

        // shared[i * count + j]: how many of i's distinct points sit on j
        int[] shared = new int[count * count];
        int[] seen = new int[count];
        int stamp = 0;

        for (int i = 0; i < count; i++)
        {
            foreach (Vector3Int key in keys[i])
            {
                stamp++;

                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    if (!grid.TryGetValue(new Vector3Int(key.x + x, key.y + y, key.z + z), out List<int> owners))
                        continue;

                    foreach (int j in owners)
                    {
                        if (j == i || seen[j] == stamp)
                            continue;

                        seen[j] = stamp;
                        shared[i * count + j]++;
                    }
                }
            }
        }

        for (int i = 0; i < count; i++)
        {
            neighbourStarts.Add(neighbours.Count);

            for (int j = 0; j < count; j++)
            {
                if (j != i && (shared[i * count + j] >= 3 || shared[j * count + i] >= 3))
                    neighbours.Add(j);
            }
        }

        neighbourStarts.Add(neighbours.Count);
    }
    #endregion

#if UNITY_EDITOR
    #region Editor
    public bool EditorPreviewing => previewing;
    public float EditorExplode => explode;

    public void EditorFracture()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;

        if (mesh == null)
        {
            Debug.LogWarning($"{name}: no mesh to fracture.", this);
            return;
        }

        EditorClear();

        Bounds bounds = mesh.bounds;
        float size = Mathf.Max(bounds.size.magnitude, 1e-4f);
        Tolerances tolerances = new Tolerances(size);

        int nextPid = 0;
        Piece source = PieceFromMesh(mesh, tolerances, ref nextPid);

        if (source == null || source.IsEmpty)
        {
            Debug.LogWarning($"{name}: the mesh has no triangles to fracture.", this);
            return;
        }

        FractureParams parameters = new FractureParams
        {
            count = shardCount,
            mode = (PointMode)(int)pointSource,
            seed = seed,
            noise = noise,
            pointScale = pointScale,
            impactPoint = impactPoint,
            impactRadius = impactRadius,
            impactConcentration = impactConcentration,
            uniformFill = uniformFill,
            recursionLevels = recursionLevels,
            recursionShards = recursionShards,
            recursionChance = recursionChance,
            recursionPreferLarge = recursionPreferLarge,
            margin = margin,
            splitIslands = splitIslands,
            minVolumeFraction = minShardVolume,
            uvScale = interiorUVScale,
            tolerances = tolerances
        };

        List<Piece> pieces;
        FractureStats stats;

        try
        {
            pieces = FractureGeometry.Fracture(source, parameters, out stats,
                progress => EditorUtility.DisplayCancelableProgressBar("Destruct Fracture", $"Fracturing {name}...", progress));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (pieces == null)
        {
            Debug.Log($"{name}: fracture cancelled.", this);
            return;
        }

        if (pieces.Count == 0)
        {
            Debug.LogWarning($"{name}: the fracture produced no shards. Is the mesh closed?", this);
            return;
        }

        BuildShards(pieces, mesh);

        if (stats.openCuts > 0)
            Debug.LogWarning($"{name}: {stats.openCuts} cuts couldn't be capped, so some shards have holes. The mesh is probably not closed (watertight).", this);
    }

    public void EditorClear()
    {
        if (fracturedRoot != null)
            DestroyImmediate(fracturedRoot);

        if (!string.IsNullOrEmpty(savedAssetPath) && AssetDatabase.LoadMainAssetAtPath(savedAssetPath) != null)
            AssetDatabase.DeleteAsset(savedAssetPath);

        fracturedRoot = null;
        shards.Clear();
        shardCentres.Clear();
        shardMins.Clear();
        shardMaxs.Clear();
        neighbourStarts.Clear();
        neighbours.Clear();
        savedAssetPath = null;
        previewing = false;
        explode = 0f;
        cullAmount = 0f;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = true;

        EditorUtility.SetDirty(this);
    }

    public void EditorSetPreview(bool show)
    {
        if (fracturedRoot == null)
            return;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        Undo.RecordObjects(new Object[] { this, meshRenderer, fracturedRoot }, "Toggle Shard Preview");

        previewing = show;
        fracturedRoot.SetActive(show);

        if (meshRenderer != null)
            meshRenderer.enabled = !show;
    }

    public void EditorSetExplode(float amount)
    {
        explode = amount;

        for (int i = 0; i < shards.Count && i < shardCentres.Count; i++)
        {
            if (shards[i] != null)
                shards[i].localPosition = shardCentres[i] + (shardCentres[i] - meshCentre) * amount;
        }

        EditorUtility.SetDirty(this);
    }

    // The horde version of Crack/Shatter: merges the shards back into one mesh so the CrackShatter shader can crack
    // and explode it with no GameObject per shard. Per vertex, UV2.xyz = the shard's centre and UV3.xyz = shard centre
    // minus mesh centre (the Explode View direction), UV3.w = a random per shard. Hidden shards are left out.
    // Re-baking overwrites the same asset, so anything using it keeps the reference.
    public void EditorBakeCrackMesh()
    {
        if (fracturedRoot == null)
        {
            Debug.LogWarning($"{name}: fracture first, then bake.", this);
            return;
        }

        int subMeshCount = 0;
        foreach (Transform shard in shards)
        {
            if (shard != null && shard.gameObject.activeSelf && shard.TryGetComponent(out MeshFilter f) && f.sharedMesh != null)
                subMeshCount = Mathf.Max(subMeshCount, f.sharedMesh.subMeshCount);
        }

        if (subMeshCount == 0)
        {
            Debug.LogWarning($"{name}: no shard meshes to bake.", this);
            return;
        }

        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var tangents = new List<Vector4>();
        var uvs = new List<Vector2>();
        var centres = new List<Vector4>();
        var infos = new List<Vector4>();
        var triangles = new List<int>[subMeshCount];
        for (int s = 0; s < subMeshCount; s++)
            triangles[s] = new List<int>();

        var random = new System.Random(seed);
        bool allTangents = true;
        int baked = 0;

        for (int i = 0; i < shards.Count && i < shardCentres.Count; i++)
        {
            Transform shard = shards[i];
            if (shard == null || !shard.gameObject.activeSelf || !shard.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                continue;

            Mesh mesh = filter.sharedMesh;
            Vector3 centre = shardCentres[i];
            Vector3 outward = centre - meshCentre;
            var centreUV = new Vector4(centre.x, centre.y, centre.z, 0f);
            var infoUV = new Vector4(outward.x, outward.y, outward.z, (float)random.NextDouble());

            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            Vector4[] t = mesh.tangents;
            Vector2[] uv = mesh.uv;
            allTangents &= t.Length == v.Length;
            int first = positions.Count;

            // Shard meshes sit around their own centre, and the shard sits at that centre in this object's space
            for (int k = 0; k < v.Length; k++)
            {
                positions.Add(v[k] + centre);
                normals.Add(n.Length == v.Length ? n[k] : Vector3.up);
                tangents.Add(t.Length == v.Length ? t[k] : new Vector4(1f, 0f, 0f, 1f));
                uvs.Add(uv.Length == v.Length ? uv[k] : Vector2.zero);
                centres.Add(centreUV);
                infos.Add(infoUV);
            }

            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                // The last submesh is always the interior, even if this shard has fewer submeshes
                int target = s == mesh.subMeshCount - 1 ? subMeshCount - 1 : s;
                foreach (int index in mesh.GetTriangles(s))
                    triangles[target].Add(first + index);
            }

            baked++;
        }

        bool newAsset = crackMesh == null || !AssetDatabase.Contains(crackMesh);
        Mesh result = newAsset ? new Mesh() : crackMesh;
        result.Clear();
        result.name = $"{name}_Crack";
        result.indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        result.SetVertices(positions);
        result.SetNormals(normals);
        if (allTangents)
            result.SetTangents(tangents);
        result.SetUVs(0, uvs);
        result.SetUVs(2, centres);
        result.SetUVs(3, infos);
        result.subMeshCount = subMeshCount;
        for (int s = 0; s < subMeshCount; s++)
            result.SetTriangles(triangles[s], s, false);
        result.RecalculateBounds();

        // Room for shards flying out on death, so a plain MeshRenderer doesn't cull them mid-air
        Bounds bounds = result.bounds;
        bounds.Expand(4f);
        result.bounds = bounds;

        if (newAsset)
        {
            string folder = string.IsNullOrWhiteSpace(assetFolder) ? "Assets/FracturedMeshes" : assetFolder.Trim().TrimEnd('/');
            if (!folder.StartsWith("Assets"))
                folder = "Assets/FracturedMeshes";

            EnsureFolder(folder);
            string fileName = string.Join("_", name.Split(System.IO.Path.GetInvalidFileNameChars()));
            AssetDatabase.CreateAsset(result, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}_Crack.asset"));
        }
        else
        {
            EditorUtility.SetDirty(result);
        }

        AssetDatabase.SaveAssets();
        crackMesh = result;
        EditorUtility.SetDirty(this);

        Debug.Log($"{name}: baked {baked} shards into {AssetDatabase.GetAssetPath(result)} ({positions.Count} vertices).", this);
    }

    // Removing shards: hides a share of them so you can see what's left, then deletes the hidden ones for good.
    // Which shards go is fixed by the seed, and the middle ones go first (Prefer Middle), so the silhouette holds.
    public float EditorCullAmount => cullAmount;
    public int EditorCullSeed => cullSeed;
    public float EditorCullInner => cullInner;
    public int EditorCullCount => Mathf.Min(Mathf.RoundToInt(shards.Count * cullAmount), shards.Count);

    public void EditorSetCull(float amount, int seed, float inner)
    {
        if (fracturedRoot == null)
            return;

        if (!previewing)
            EditorSetPreview(true);

        List<Object> recorded = new List<Object> { this };
        foreach (Transform shard in shards)
        {
            if (shard != null)
                recorded.Add(shard.gameObject);
        }

        Undo.RecordObjects(recorded.ToArray(), "Hide Shards");

        cullAmount = Mathf.Clamp01(amount);
        cullSeed = seed;
        cullInner = Mathf.Clamp01(inner);

        HashSet<int> culled = CulledIndices();

        for (int i = 0; i < shards.Count; i++)
        {
            if (shards[i] != null)
                shards[i].gameObject.SetActive(!culled.Contains(i));
        }

        EditorUtility.SetDirty(this);
    }

    // Deletes the hidden shards and their meshes. Not undoable: Re-fracture brings them back.
    public bool EditorDeleteCulled()
    {
        int target = EditorCullCount;
        if (target == 0)
            return false;

        // Shards belong to the prefab, so a scene instance can't lose them. Inside Prefab Mode it's the asset itself.
        bool inPrefabMode = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null;

        if (!inPrefabMode && PrefabUtility.IsPartOfNonAssetPrefabInstance(this))
        {
            EditorUtility.DisplayDialog("Can't delete shards here", $"{name} is an instance of a prefab, and its shards live in the prefab. Open the prefab (double-click it, or Open in the Inspector) and delete them there.", "OK");
            return false;
        }

        try
        {
            DeleteCulled(target);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e, this);
            EditorUtility.DisplayDialog("Delete failed", $"{e.Message}\n\nSee the Console for details.", "OK");
            return false;
        }

        return true;
    }

    private void DeleteCulled(int target)
    {
        HashSet<int> culled = CulledIndices();
        List<Transform> kept = new List<Transform>(shards.Count - target);
        List<Vector3> keptCentres = new List<Vector3>(shards.Count - target);

        for (int i = 0; i < shards.Count; i++)
        {
            Transform shard = shards[i];

            if (!culled.Contains(i))
            {
                kept.Add(shard);
                keptCentres.Add(i < shardCentres.Count ? shardCentres[i] : Vector3.zero);
                continue;
            }

            if (shard == null)
                continue;

            // Only sub-assets can be removed from the saved mesh asset; the main one stays
            if (shard.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
            {
                Mesh mesh = meshFilter.sharedMesh;

                if (AssetDatabase.Contains(mesh) && !AssetDatabase.IsMainAsset(mesh))
                {
                    AssetDatabase.RemoveObjectFromAsset(mesh);
                    DestroyImmediate(mesh, true);
                }
            }

            DestroyImmediate(shard.gameObject);
        }

        shards = kept;
        shardCentres = keptCentres;
        cullAmount = 0f;
        BuildSupports();     // indices changed

        // What's left is the shape: show all of it again
        foreach (Transform shard in shards)
        {
            if (shard != null)
                shard.gameObject.SetActive(true);
        }

        if (!string.IsNullOrEmpty(savedAssetPath))
            AssetDatabase.SaveAssets();

        EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private HashSet<int> CulledIndices()
    {
        int count = Mathf.Min(shards.Count, shardCentres.Count);
        int target = Mathf.Min(Mathf.RoundToInt(shards.Count * cullAmount), count);

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        Vector3 extents = meshFilter != null && meshFilter.sharedMesh != null ? meshFilter.sharedMesh.bounds.extents : Vector3.one;

        // How deep inside the mesh each shard sits: 0 at the centre, 1 for the outermost
        float[] depth = new float[count];
        float deepest = 1e-5f;

        for (int i = 0; i < count; i++)
        {
            Vector3 d = shardCentres[i] - meshCentre;
            d.x /= Mathf.Max(extents.x, 1e-5f);
            d.y /= Mathf.Max(extents.y, 1e-5f);
            d.z /= Mathf.Max(extents.z, 1e-5f);
            depth[i] = d.magnitude;
            deepest = Mathf.Max(deepest, depth[i]);
        }

        System.Random rng = new System.Random(cullSeed);
        float[] score = new float[count];
        List<int> order = new List<int>(count);

        for (int i = 0; i < count; i++)
        {
            score[i] = Mathf.Lerp((float)rng.NextDouble(), depth[i] / deepest, cullInner);
            order.Add(i);
        }

        order.Sort((a, b) => score[a].CompareTo(score[b]));
        return new HashSet<int>(order.GetRange(0, target));
    }

    private static Piece PieceFromMesh(Mesh mesh, Tolerances tolerances, ref int nextPid)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uvs = mesh.uv;

        int[][] subMeshTriangles = new int[mesh.subMeshCount][];

        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            subMeshTriangles[s] = mesh.GetTopology(s) == MeshTopology.Triangles
                ? mesh.GetTriangles(s)
                : new int[0];
        }

        return Piece.FromArrays(vertices, normals, uvs, subMeshTriangles, tolerances.weld, ref nextPid);
    }

    private void BuildShards(List<Piece> pieces, Mesh sourceMesh)
    {
        GameObject root = new GameObject($"{name} (Fractured)");
        root.layer = gameObject.layer;
        root.transform.SetParent(transform, false);

        MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();
        Material[] sourceMaterials = sourceRenderer.sharedMaterials;
        int sourceSubMeshes = sourceMesh.subMeshCount;

        // One material per source submesh, then the interior
        Material[] materials = new Material[sourceSubMeshes + 1];

        for (int i = 0; i < sourceSubMeshes; i++)
        {
            materials[i] = sourceMaterials.Length == 0 ? null : sourceMaterials[Mathf.Min(i, sourceMaterials.Length - 1)];
        }

        materials[sourceSubMeshes] = interiorMaterial != null
            ? interiorMaterial
            : (sourceMaterials.Length > 0 ? sourceMaterials[0] : null);

        // One submesh means one material: the first
        if (singleSubmesh)
            materials = new[] { materials[0] };

        bool hasNormals = sourceMesh.HasVertexAttribute(VertexAttribute.Normal);
        bool hasTangents = sourceMesh.HasVertexAttribute(VertexAttribute.Tangent);
        Vector3 lossy = transform.lossyScale;
        float volumeScale = Mathf.Abs(lossy.x * lossy.y * lossy.z);

        List<Mesh> meshes = new List<Mesh>(pieces.Count);
        shards.Clear();
        shardCentres.Clear();

        float minHullThickness = sourceMesh.bounds.size.magnitude * 2e-3f;
        int slivers = 0;

        for (int i = 0; i < pieces.Count; i++)
        {
            Piece piece = pieces[i];
            FractureGeometry.GetBounds(piece, out Vector3 min, out Vector3 max);
            Vector3 centre = (min + max) * 0.5f;

            Mesh mesh = ToMesh(piece, centre, $"{name}_Shard_{i:000}", !hasNormals, hasTangents, singleSubmesh);
            meshes.Add(mesh);

            GameObject shard = new GameObject($"Shard {i:000}");
            shard.layer = gameObject.layer;
            shard.transform.SetParent(root.transform, false);
            shard.transform.localPosition = centre;

            shard.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer shardRenderer = shard.AddComponent<MeshRenderer>();
            shardRenderer.sharedMaterials = materials;
            shardRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
            shardRenderer.receiveShadows = sourceRenderer.receiveShadows;

            switch (colliders)
            {
                case ColliderMode.ConvexMesh:
                    // PhysX can't build a hull from a sliver (flat or needle-thin), so those get a box instead
                    if (HasThickness(mesh, minHullThickness))
                    {
                        MeshCollider meshCollider = shard.AddComponent<MeshCollider>();
                        meshCollider.sharedMesh = mesh;
                        meshCollider.convex = true;
                    }
                    else
                    {
                        AddBoxCollider(shard, mesh);
                        slivers++;
                    }
                    break;

                case ColliderMode.Box:
                    AddBoxCollider(shard, mesh);
                    break;
            }

            if (addRigidbodies)
            {
                Rigidbody body = shard.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.mass = Mathf.Max(0.01f, density * FractureGeometry.Volume(piece) * volumeScale);
            }

            shards.Add(shard.transform);
            shardCentres.Add(centre);
        }

        meshCentre = sourceMesh.bounds.center;
        fracturedRoot = root;
        BuildSupports();

        if (slivers > 0)
            Debug.LogWarning($"{name}: {slivers} shards are too thin for a convex collider and got a box collider instead. Raise Min Shard Volume to remove slivers like these.", this);

        if (saveMeshesAsAsset)
            SaveMeshes(meshes);

        explode = 0f;
        previewing = true;
        sourceRenderer.enabled = false;
        EditorUtility.SetDirty(this);
    }

    private static void AddBoxCollider(GameObject shard, Mesh mesh)
    {
        BoxCollider box = shard.AddComponent<BoxCollider>();
        box.center = mesh.bounds.center;
        box.size = mesh.bounds.size;
    }

    // True when the mesh has four points spread out enough to make a solid: a long edge, a triangle off that edge's
    // line, and a point off that triangle's plane, each at least minThickness away. Flat or needle-thin meshes fail.
    private static bool HasThickness(Mesh mesh, float minThickness)
    {
        Vector3[] v = mesh.vertices;
        if (v.Length < 4)
            return false;

        Vector3 a = v[0];
        Vector3 b = a;
        float best = 0f;

        foreach (Vector3 p in v)
        {
            float d = (p - a).sqrMagnitude;
            if (d > best) { best = d; b = p; }
        }

        // A second pass from the far end finds a longer edge than the first guess
        a = b;
        best = 0f;

        foreach (Vector3 p in v)
        {
            float d = (p - a).sqrMagnitude;
            if (d > best) { best = d; b = p; }
        }

        Vector3 edge = b - a;
        if (edge.magnitude < minThickness)
            return false;

        Vector3 c = a;
        best = 0f;

        foreach (Vector3 p in v)
        {
            float d = Vector3.Cross(p - a, edge.normalized).magnitude;
            if (d > best) { best = d; c = p; }
        }

        if (best < minThickness)
            return false;

        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
        best = 0f;

        foreach (Vector3 p in v)
            best = Mathf.Max(best, Mathf.Abs(Vector3.Dot(p - a, normal)));

        return best >= minThickness;
    }

    private static Mesh ToMesh(Piece piece, Vector3 offset, string meshName, bool recalculateNormals, bool recalculateTangents, bool singleSubmesh)
    {
        Mesh mesh = new Mesh { name = meshName };

        if (piece.pos.Count > 65000)
            mesh.indexFormat = IndexFormat.UInt32;

        List<Vector3> vertices = new List<Vector3>(piece.pos.Count);
        foreach (Vector3 p in piece.pos)
            vertices.Add(p - offset);

        mesh.SetVertices(vertices);
        mesh.SetNormals(piece.nrm);
        mesh.SetUVs(0, piece.uv);

        if (singleSubmesh)
        {
            List<int> all = new List<int>();
            foreach (List<int> triangles in piece.tris)
                all.AddRange(triangles);

            mesh.subMeshCount = 1;
            mesh.SetTriangles(all, 0, false);
        }
        else
        {
            mesh.subMeshCount = piece.SubMeshCount;

            for (int s = 0; s < piece.SubMeshCount; s++)
                mesh.SetTriangles(piece.tris[s], s, false);
        }

        mesh.RecalculateBounds();

        if (recalculateNormals)
            mesh.RecalculateNormals();

        if (recalculateTangents)
            mesh.RecalculateTangents();

        return mesh;
    }

    private void SaveMeshes(List<Mesh> meshes)
    {
        if (meshes.Count == 0)
            return;

        string folder = string.IsNullOrWhiteSpace(assetFolder) ? "Assets/FracturedMeshes" : assetFolder.Trim().TrimEnd('/');

        if (!folder.StartsWith("Assets"))
        {
            Debug.LogWarning($"{name}: the asset folder must be inside Assets. Meshes are stored in the scene instead.", this);
            return;
        }

        EnsureFolder(folder);

        string fileName = string.Join("_", name.Split(System.IO.Path.GetInvalidFileNameChars()));
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}_Fracture.asset");

        AssetDatabase.CreateAsset(meshes[0], path);

        for (int i = 1; i < meshes.Count; i++)
            AssetDatabase.AddObjectToAsset(meshes[i], path);

        AssetDatabase.SaveAssets();
        savedAssetPath = path;
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
    #endregion
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(Destruct_Fracture)), CanEditMultipleObjects]
public class Destruct_FractureEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Destruct_Fracture fracture = (Destruct_Fracture)target;
        EditorGUILayout.Space(10);

        // Breaking is Destruct_Piece's job at runtime
        if (Application.isPlaying)
            return;

        if (GUILayout.Button(fracture.IsFractured ? "Re-fracture" : "Fracture", GUILayout.Height(32)))
        {
            foreach (Object t in targets)
                ((Destruct_Fracture)t).EditorFracture();

            GUIUtility.ExitGUI();
        }

        using (new EditorGUI.DisabledScope(!fracture.IsFractured))
        {
            EditorGUILayout.BeginHorizontal();

            bool preview = GUILayout.Toggle(fracture.EditorPreviewing, "Show Shards", "Button");
            if (preview != fracture.EditorPreviewing)
            {
                foreach (Object t in targets)
                    ((Destruct_Fracture)t).EditorSetPreview(preview);
            }

            if (GUILayout.Button("Clear"))
            {
                foreach (Object t in targets)
                    ((Destruct_Fracture)t).EditorClear();

                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            float explode = EditorGUILayout.Slider("Explode View", fracture.EditorExplode, 0f, 1.5f);

            if (EditorGUI.EndChangeCheck())
            {
                foreach (Object t in targets)
                    ((Destruct_Fracture)t).EditorSetExplode(explode);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Crack Shader (Horde)", EditorStyles.boldLabel);

            if (GUILayout.Button(new GUIContent(fracture.CrackMesh != null ? "Re-bake Crack Mesh" : "Bake Crack Mesh",
                    "Merge the shards into one mesh for the CrackShatter shader. Re-bake after changing or deleting shards.")))
            {
                foreach (Object t in targets)
                    ((Destruct_Fracture)t).EditorBakeCrackMesh();

                GUIUtility.ExitGUI();
            }

            if (fracture.CrackMesh != null)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("Crack Mesh", fracture.CrackMesh, typeof(Mesh), false);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Remove Shards", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            float cullAmount = EditorGUILayout.Slider(new GUIContent("Hide", "Share of shards to remove. Hidden live in the Scene view; nothing is deleted until you press Delete."), fracture.EditorCullAmount, 0f, 0.75f);
            float cullInner = EditorGUILayout.Slider(new GUIContent("Prefer Middle", "0 = random shards. 1 = the shards deepest inside go first, so the outside keeps its shape."), fracture.EditorCullInner, 0f, 1f);

            if (EditorGUI.EndChangeCheck())
            {
                foreach (Object t in targets)
                    ((Destruct_Fracture)t).EditorSetCull(cullAmount, ((Destruct_Fracture)t).EditorCullSeed, cullInner);
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Reshuffle"))
            {
                foreach (Object t in targets)
                {
                    Destruct_Fracture m = (Destruct_Fracture)t;
                    m.EditorSetCull(m.EditorCullAmount, m.EditorCullSeed + 1, m.EditorCullInner);
                }
            }

            using (new EditorGUI.DisabledScope(fracture.EditorCullCount == 0))
            {
                if (GUILayout.Button($"Delete {fracture.EditorCullCount} Hidden"))
                {
                    if (EditorUtility.DisplayDialog("Delete hidden shards", "Delete the hidden shards and their meshes? This can't be undone. Re-fracture to get them back.", "Delete", "Cancel"))
                    {
                        foreach (Object t in targets)
                            ((Destruct_Fracture)t).EditorDeleteCulled();
                    }

                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            if (GUILayout.Button(new GUIContent("Rebuild Supports", "Work out again which shards touch. Fracturing does " +
                    "this already; use it on objects fractured before supports existed.")))
            {
                foreach (Object t in targets)
                {
                    Destruct_Fracture f = (Destruct_Fracture)t;
                    Undo.RecordObject(f, "Rebuild Supports");
                    f.BuildSupports();
                    EditorUtility.SetDirty(f);
                }
            }

            if (fracture.IsFractured)
            {
                string supports = fracture.HasSupports ? $"{fracture.LinkCount} links between them" : "no supports yet";
                EditorGUILayout.HelpBox($"{fracture.ShardCount} shards, {supports}. Add Destruct_Piece to break it at runtime.", MessageType.None);
            }
        }
    }

    private void OnSceneGUI()
    {
        Destruct_Fracture fracture = (Destruct_Fracture)target;

        if (fracture.pointSource != Destruct_Fracture.PointSource.Impact)
            return;

        Transform t = fracture.transform;
        Vector3 world = t.TransformPoint(fracture.impactPoint);

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.PositionHandle(world, t.rotation);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(fracture, "Move Impact Point");
            fracture.impactPoint = t.InverseTransformPoint(moved);
        }

        Vector3 scale = t.lossyScale;
        float radius = fracture.impactRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

        Handles.color = new Color(1f, 0.45f, 0.1f, 0.9f);
        Handles.DrawWireDisc(world, t.up, radius);
        Handles.DrawWireDisc(world, t.right, radius);
        Handles.DrawWireDisc(world, t.forward, radius);
    }
}
#endif

// FRACTURE-CORE-BEGIN
// The fracture maths. Plain C# on UnityEngine vectors, no Unity objects, so it can be tested outside the editor.
namespace Destruct_FractureCore
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public enum PointMode
    {
        Uniform,
        Impact,
        MeshVertices
    }

    public struct Tolerances
    {
        public float plane;     // distances to a cutting plane smaller than this count as on it
        public float weld;      // points closer than this are the same point

        public Tolerances(float meshSize)
        {
            plane = meshSize * 1e-6f;
            weld = meshSize * 1e-5f;
        }
    }

    public struct FractureStats
    {
        public int openCuts;    // cut outlines that didn't close, so they couldn't be capped
    }

    public sealed class FractureParams
    {
        public int count = 24;
        public PointMode mode = PointMode.Uniform;
        public int seed = 1;
        public float noise;
        public Vector3 pointScale = Vector3.one;
        public Vector3 impactPoint;
        public float impactRadius = 0.5f;
        public float impactConcentration = 2f;
        public float uniformFill = 0.25f;
        public int recursionLevels;
        public int recursionShards = 4;
        public float recursionChance = 0.25f;
        public bool recursionPreferLarge = true;
        public float margin;
        public bool splitIslands = true;
        public float minVolumeFraction;
        public float uvScale = 1f;
        public Tolerances tolerances;
    }

    // A triangle mesh being cut. pid is a position id: vertices at the same point share it, even when their normals
    // or UVs differ, so cuts and islands can tell which corners touch.
    public sealed class Piece
    {
        public readonly List<Vector3> pos;
        public readonly List<Vector3> nrm;
        public readonly List<Vector2> uv;
        public readonly List<int> pid;
        public readonly List<int>[] tris;   // one list per submesh, the last is the interior (cut faces)

        public Piece(int subMeshCount, int vertexCapacity = 64)
        {
            pos = new List<Vector3>(vertexCapacity);
            nrm = new List<Vector3>(vertexCapacity);
            uv = new List<Vector2>(vertexCapacity);
            pid = new List<int>(vertexCapacity);
            tris = new List<int>[subMeshCount];

            for (int i = 0; i < subMeshCount; i++)
                tris[i] = new List<int>();
        }

        public int SubMeshCount => tris.Length;
        public int InteriorSubMesh => tris.Length - 1;

        public bool IsEmpty
        {
            get
            {
                foreach (List<int> list in tris)
                {
                    if (list.Count > 0)
                        return false;
                }

                return true;
            }
        }

        public int AddVertex(Vector3 p, Vector3 n, Vector2 t, int id)
        {
            pos.Add(p);
            nrm.Add(n);
            uv.Add(t);
            pid.Add(id);
            return pos.Count - 1;
        }

        // Builds a piece from mesh data, with one extra (empty) submesh for the interior.
        public static Piece FromArrays(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int[][] subMeshTriangles, float weldTolerance, ref int nextPid)
        {
            int subMeshCount = subMeshTriangles.Length + 1;
            Piece piece = new Piece(subMeshCount, vertices.Length);
            PointWelder welder = new PointWelder(weldTolerance);

            bool hasNormals = normals != null && normals.Length == vertices.Length;
            bool hasUvs = uvs != null && uvs.Length == vertices.Length;

            for (int i = 0; i < vertices.Length; i++)
            {
                int index = welder.FindOrAdd(vertices[i], nextPid, out bool added);
                if (added)
                    nextPid++;

                piece.AddVertex(vertices[i], hasNormals ? normals[i] : Vector3.zero, hasUvs ? uvs[i] : Vector2.zero, welder.ids[index]);
            }

            for (int s = 0; s < subMeshTriangles.Length; s++)
            {
                int[] triangles = subMeshTriangles[s];

                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];

                    // Skip collapsed triangles: they have no area and only confuse the cuts
                    if (piece.pid[a] == piece.pid[b] || piece.pid[b] == piece.pid[c] || piece.pid[a] == piece.pid[c])
                        continue;

                    piece.tris[s].Add(a);
                    piece.tris[s].Add(b);
                    piece.tris[s].Add(c);
                }
            }

            return piece;
        }
    }

    // Merges points closer than a tolerance, using a hash grid.
    public sealed class PointWelder
    {
        private readonly float cell;
        private readonly float toleranceSqr;
        private readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();

        public readonly List<Vector3> points = new List<Vector3>();
        public readonly List<int> ids = new List<int>();

        public float Tolerance => cell;

        public PointWelder(float tolerance)
        {
            cell = Mathf.Max(tolerance, 1e-9f);
            toleranceSqr = cell * cell;
        }

        public int Find(Vector3 p)
        {
            int cx = Mathf.FloorToInt(p.x / cell);
            int cy = Mathf.FloorToInt(p.y / cell);
            int cz = Mathf.FloorToInt(p.z / cell);

            for (int x = cx - 1; x <= cx + 1; x++)
            {
                for (int y = cy - 1; y <= cy + 1; y++)
                {
                    for (int z = cz - 1; z <= cz + 1; z++)
                    {
                        if (!grid.TryGetValue(Key(x, y, z), out List<int> list))
                            continue;

                        foreach (int index in list)
                        {
                            if ((points[index] - p).sqrMagnitude <= toleranceSqr)
                                return index;
                        }
                    }
                }
            }

            return -1;
        }

        public int FindOrAdd(Vector3 p, int id, out bool added)
        {
            int found = Find(p);

            if (found >= 0)
            {
                added = false;
                return found;
            }

            added = true;
            int index = points.Count;
            points.Add(p);
            ids.Add(id);

            long key = Key(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell));

            if (!grid.TryGetValue(key, out List<int> bucket))
            {
                bucket = new List<int>(2);
                grid[key] = bucket;
            }

            bucket.Add(index);
            return index;
        }

        private static long Key(int x, int y, int z)
        {
            return ((long)x * 73856093L) ^ ((long)y * 19349663L) ^ ((long)z * 83492791L);
        }
    }

    public static class FractureGeometry
    {
        #region Fracture
        // The whole pipeline: seeds, Voronoi cells, recursion, islands and the size filter.
        // Returns null when cancelled (progress returns true).
        public static List<Piece> Fracture(Piece source, FractureParams p, out FractureStats stats, Func<float, bool> progress = null)
        {
            stats = default;
            System.Random rng = new System.Random(p.seed);

            List<Vector3> seeds = GenerateSeeds(source, p.count, p.mode, p, rng);
            int totalSteps = Mathf.Max(1, seeds.Count) * (1 + p.recursionLevels);
            int step = 0;

            Func<bool> cancel = () => progress != null && progress(Mathf.Min(step++, totalSteps - 1) / (float)totalSteps);

            List<Piece> pieces = Voronoi(source, seeds, p, ref stats, cancel);
            if (pieces == null)
                return null;

            for (int level = 0; level < p.recursionLevels; level++)
            {
                List<int> chosen = ChooseForRecursion(pieces, p, rng);
                if (chosen.Count == 0)
                    break;

                HashSet<int> chosenSet = new HashSet<int>(chosen);
                List<Piece> next = new List<Piece>(pieces.Count + chosen.Count * p.recursionShards);

                for (int i = 0; i < pieces.Count; i++)
                {
                    if (!chosenSet.Contains(i))
                    {
                        next.Add(pieces[i]);
                        continue;
                    }

                    List<Vector3> childSeeds = GenerateSeeds(pieces[i], p.recursionShards, PointMode.Uniform, p, rng);

                    if (childSeeds.Count < 2)
                    {
                        next.Add(pieces[i]);
                        continue;
                    }

                    List<Piece> children = Voronoi(pieces[i], childSeeds, p, ref stats, cancel);
                    if (children == null)
                        return null;

                    if (children.Count > 0)
                        next.AddRange(children);
                    else
                        next.Add(pieces[i]);
                }

                pieces = next;
            }

            if (p.splitIslands)
            {
                List<Piece> split = new List<Piece>(pieces.Count);
                foreach (Piece piece in pieces)
                    split.AddRange(SplitIslands(piece));
                pieces = split;
            }

            if (p.minVolumeFraction > 0f)
            {
                float limit = Mathf.Abs(Volume(source)) * p.minVolumeFraction;
                pieces.RemoveAll(piece => Mathf.Abs(Volume(piece)) < limit);
            }

            pieces.RemoveAll(piece => piece.IsEmpty);
            return pieces;
        }

        private static List<int> ChooseForRecursion(List<Piece> pieces, FractureParams p, System.Random rng)
        {
            List<int> chosen = new List<int>();

            if (p.recursionPreferLarge)
            {
                int take = Mathf.Clamp(Mathf.RoundToInt(pieces.Count * p.recursionChance), p.recursionChance > 0f ? 1 : 0, pieces.Count);
                List<int> order = new List<int>(pieces.Count);
                float[] volumes = new float[pieces.Count];

                for (int i = 0; i < pieces.Count; i++)
                {
                    order.Add(i);
                    volumes[i] = Mathf.Abs(Volume(pieces[i]));
                }

                order.Sort((a, b) => volumes[b].CompareTo(volumes[a]));

                for (int i = 0; i < take; i++)
                    chosen.Add(order[i]);
            }
            else
            {
                for (int i = 0; i < pieces.Count; i++)
                {
                    if (rng.NextDouble() < p.recursionChance)
                        chosen.Add(i);
                }
            }

            return chosen;
        }
        #endregion

        #region Voronoi
        // A cutting plane of a cell. Inside is dot(normal, p) <= d.
        private struct CellPlane
        {
            public Vector3 normal;
            public float d;
        }

        // A face of a convex cell. Its points run counter-clockwise seen from outside the cell.
        private sealed class CellFace
        {
            public int plane;           // index into the cell's planes, -1 for a face of the starting box
            public List<Vector3> points;
        }

        // A surface vertex being clipped
        private struct SurfaceVertex
        {
            public Vector3 p;
            public Vector3 n;
            public Vector2 t;
        }

        // The source's triangles with their bounds, worked out once per fracture.
        private sealed class TriangleSet
        {
            public readonly int count;
            public readonly int[] sub, a, b, c;
            public readonly Vector3[] min, max;

            public TriangleSet(Piece piece)
            {
                int total = 0;
                foreach (List<int> list in piece.tris)
                    total += list.Count / 3;

                count = total;
                sub = new int[total];
                a = new int[total];
                b = new int[total];
                c = new int[total];
                min = new Vector3[total];
                max = new Vector3[total];

                int k = 0;

                for (int s = 0; s < piece.SubMeshCount; s++)
                {
                    List<int> list = piece.tris[s];

                    for (int t = 0; t + 2 < list.Count; t += 3, k++)
                    {
                        sub[k] = s;
                        a[k] = list[t];
                        b[k] = list[t + 1];
                        c[k] = list[t + 2];
                        Vector3 pa = piece.pos[a[k]], pb = piece.pos[b[k]], pc = piece.pos[c[k]];
                        min[k] = Vector3.Min(pa, Vector3.Min(pb, pc));
                        max[k] = Vector3.Max(pa, Vector3.Max(pb, pc));
                    }
                }
            }
        }

        // Cuts the Voronoi cell of every seed out of the source. Each cell is built as an exact convex polyhedron
        // first; then the surface is clipped to it and every cut face is filled once from the source's cross-section,
        // so no cut face is ever cut again. Returns null when cancelled.
        public static List<Piece> Voronoi(Piece source, List<Vector3> seeds, FractureParams p, ref FractureStats stats, Func<bool> cancel = null)
        {
            List<Piece> result = new List<Piece>(seeds.Count);

            GetBounds(source, out Vector3 min, out Vector3 max);
            Vector3 pad = (max - min) * 0.1f + Vector3.one * (p.tolerances.weld * 100f + p.margin);
            Vector3 boxMin = min - pad;
            Vector3 boxMax = max + pad;

            TriangleSet triangles = new TriangleSet(source);
            List<CellPlane> planes = new List<CellPlane>();

            for (int i = 0; i < seeds.Count; i++)
            {
                if (cancel != null && cancel())
                    return null;

                planes.Clear();
                List<CellFace> faces = BuildCell(i, seeds, p.margin, boxMin, boxMax, planes, p.tolerances);

                if (faces == null)
                    continue;

                Piece cell = CutCell(source, triangles, faces, planes, p, ref stats);

                if (cell != null && !cell.IsEmpty)
                    result.Add(cell);
            }

            return result;
        }

        // The convex cell of one seed: a box cut by the bisector planes to its neighbours, nearest first.
        private static List<CellFace> BuildCell(int self, List<Vector3> seeds, float margin, Vector3 boxMin, Vector3 boxMax, List<CellPlane> planes, Tolerances tolerances)
        {
            Vector3 seed = seeds[self];
            List<CellFace> faces = Box(boxMin, boxMax);

            int count = seeds.Count;
            float[] distanceSqr = new float[count];
            List<int> others = new List<int>(count);

            for (int j = 0; j < count; j++)
            {
                distanceSqr[j] = (seeds[j] - seed).sqrMagnitude;
                if (j != self)
                    others.Add(j);
            }

            others.Sort((x, y) => distanceSqr[x].CompareTo(distanceSqr[y]));
            float reach = MaxDistance(faces, seed);

            foreach (int j in others)
            {
                float distance = Mathf.Sqrt(distanceSqr[j]);
                if (distance < 1e-9f)
                    continue;

                float offset = distance * 0.5f - margin * 0.5f;

                // This bisector, and every later one, is beyond the cell
                if (offset > reach)
                    break;

                Vector3 normal = (seeds[j] - seed) / distance;
                planes.Add(new CellPlane { normal = normal, d = Vector3.Dot(normal, seed) + offset });

                faces = ClipCell(faces, normal, planes[planes.Count - 1].d, planes.Count - 1, tolerances);

                if (faces.Count < 4)
                    return null;

                reach = MaxDistance(faces, seed);
            }

            return faces;
        }

        private static List<CellFace> Box(Vector3 min, Vector3 max)
        {
            Vector3 Corner(int x, int y, int z) => new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);

            List<CellFace> faces = new List<CellFace>(6);

            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                List<Vector3> points = new List<Vector3> { a, b, c, d };
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
                    points.Reverse();
                faces.Add(new CellFace { plane = -1, points = points });
            }

            Face(Corner(0, 0, 0), Corner(0, 1, 0), Corner(0, 1, 1), Corner(0, 0, 1), new Vector3(-1, 0, 0));
            Face(Corner(1, 0, 0), Corner(1, 1, 0), Corner(1, 1, 1), Corner(1, 0, 1), new Vector3(1, 0, 0));
            Face(Corner(0, 0, 0), Corner(1, 0, 0), Corner(1, 0, 1), Corner(0, 0, 1), new Vector3(0, -1, 0));
            Face(Corner(0, 1, 0), Corner(1, 1, 0), Corner(1, 1, 1), Corner(0, 1, 1), new Vector3(0, 1, 0));
            Face(Corner(0, 0, 0), Corner(1, 0, 0), Corner(1, 1, 0), Corner(0, 1, 0), new Vector3(0, 0, -1));
            Face(Corner(0, 0, 1), Corner(1, 0, 1), Corner(1, 1, 1), Corner(0, 1, 1), new Vector3(0, 0, 1));
            return faces;
        }

        // Cuts a convex cell with a plane and closes it with a new face.
        private static List<CellFace> ClipCell(List<CellFace> faces, Vector3 normal, float d, int planeIndex, Tolerances tolerances)
        {
            float eps = tolerances.plane;
            List<CellFace> result = new List<CellFace>(faces.Count + 1);
            PointWelder capPoints = new PointWelder(tolerances.weld);

            foreach (CellFace face in faces)
            {
                List<Vector3> points = face.points;
                List<Vector3> output = new List<Vector3>(points.Count + 2);
                int count = points.Count;

                for (int i = 0; i < count; i++)
                {
                    Vector3 a = points[i];
                    Vector3 b = points[(i + 1) % count];
                    float da = Vector3.Dot(normal, a) - d;
                    float db = Vector3.Dot(normal, b) - d;

                    if (da <= eps)
                    {
                        output.Add(a);
                        if (da >= -eps)
                            capPoints.FindOrAdd(a, 0, out _);
                    }

                    if ((da < -eps && db > eps) || (da > eps && db < -eps))
                    {
                        Vector3 x = IntersectOrdered(a, b, da, db);
                        output.Add(x);
                        capPoints.FindOrAdd(x, 0, out _);
                    }
                }

                if (output.Count >= 3)
                    result.Add(new CellFace { plane = face.plane, points = output });
            }

            if (capPoints.points.Count >= 3)
            {
                // The new face is convex: order its points by angle, counter-clockwise seen from outside
                Basis(normal, out Vector3 u, out Vector3 v);
                Vector3 centre = Vector3.zero;
                foreach (Vector3 point in capPoints.points)
                    centre += point;
                centre /= capPoints.points.Count;

                List<Vector3> ordered = new List<Vector3>(capPoints.points);
                ordered.Sort((x, y) =>
                {
                    Vector3 dx = x - centre, dy = y - centre;
                    float ax = Mathf.Atan2(Vector3.Dot(dx, v), Vector3.Dot(dx, u));
                    float ay = Mathf.Atan2(Vector3.Dot(dy, v), Vector3.Dot(dy, u));
                    return ax.CompareTo(ay);
                });

                result.Add(new CellFace { plane = planeIndex, points = ordered });
            }

            return result;
        }

        // The source inside one cell: its clipped surface plus a filled face on every cutting plane.
        private static Piece CutCell(Piece src, TriangleSet triangles, List<CellFace> faces, List<CellPlane> planes, FractureParams p, ref FractureStats stats)
        {
            float eps = p.tolerances.plane;

            Vector3 cellMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 cellMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            List<int> cuts = new List<int>();

            foreach (CellFace face in faces)
            {
                foreach (Vector3 point in face.points)
                {
                    cellMin = Vector3.Min(cellMin, point);
                    cellMax = Vector3.Max(cellMax, point);
                }

                if (face.plane >= 0 && !cuts.Contains(face.plane))
                    cuts.Add(face.plane);
            }

            Vector3 slack = Vector3.one * p.tolerances.weld;
            cellMin -= slack;
            cellMax += slack;

            Piece dst = new Piece(src.SubMeshCount, 256);
            PointWelder welder = new PointWelder(p.tolerances.weld);

            // Vertices at the same point share one position exactly, so shards have no hairline cracks
            int AddVertex(Vector3 position, Vector3 n, Vector2 t)
            {
                int index = welder.FindOrAdd(position, welder.points.Count, out _);
                return dst.AddVertex(welder.points[index], n, t, welder.ids[index]);
            }

            // Surface
            List<SurfaceVertex> polygon = new List<SurfaceVertex>(8);
            List<SurfaceVertex> scratch = new List<SurfaceVertex>(8);

            for (int k = 0; k < triangles.count; k++)
            {
                if (!Overlaps(triangles.min[k], triangles.max[k], cellMin, cellMax))
                    continue;

                int ia = triangles.a[k], ib = triangles.b[k], ic = triangles.c[k];
                Vector3 pa = src.pos[ia], pb = src.pos[ib], pc = src.pos[ic];
                bool outside = false, crosses = false;

                foreach (int q in cuts)
                {
                    CellPlane plane = planes[q];
                    float d0 = Vector3.Dot(plane.normal, pa) - plane.d;
                    float d1 = Vector3.Dot(plane.normal, pb) - plane.d;
                    float d2 = Vector3.Dot(plane.normal, pc) - plane.d;

                    if (d0 > eps && d1 > eps && d2 > eps)
                    {
                        outside = true;
                        break;
                    }

                    if (d0 > eps || d1 > eps || d2 > eps)
                        crosses = true;
                }

                if (outside)
                    continue;

                polygon.Clear();
                polygon.Add(new SurfaceVertex { p = pa, n = src.nrm[ia], t = src.uv[ia] });
                polygon.Add(new SurfaceVertex { p = pb, n = src.nrm[ib], t = src.uv[ib] });
                polygon.Add(new SurfaceVertex { p = pc, n = src.nrm[ic], t = src.uv[ic] });

                if (crosses)
                {
                    foreach (int q in cuts)
                    {
                        ClipPolygon(polygon, scratch, planes[q], eps);
                        List<SurfaceVertex> swap = polygon;
                        polygon = scratch;
                        scratch = swap;

                        if (polygon.Count < 3)
                            break;
                    }
                }

                if (polygon.Count < 3)
                    continue;

                List<int> output = dst.tris[triangles.sub[k]];
                int first = AddVertex(polygon[0].p, polygon[0].n, polygon[0].t);
                int previous = AddVertex(polygon[1].p, polygon[1].n, polygon[1].t);

                for (int m = 2; m < polygon.Count; m++)
                {
                    int current = AddVertex(polygon[m].p, polygon[m].n, polygon[m].t);

                    if (dst.pid[first] != dst.pid[previous] && dst.pid[previous] != dst.pid[current] && dst.pid[first] != dst.pid[current])
                    {
                        output.Add(first);
                        output.Add(previous);
                        output.Add(current);
                    }

                    previous = current;
                }
            }

            // Cut faces
            foreach (CellFace face in faces)
            {
                if (face.plane >= 0)
                    FillCutFace(src, triangles, face, planes[face.plane], cellMin, cellMax, p, dst, AddVertex, ref stats);
            }

            CancelMirroredPairs(dst);

            if (dst.IsEmpty)
                return null;

            Piece shared = ShareVertices(dst);
            CloseSmallHoles(shared, p.tolerances.weld * 200f);
            return shared;
        }

        // Where a cell corner sits almost exactly on the surface, the three faces meeting there are computed
        // separately and can miss each other by a hair, leaving a pinhole. Find outlines no triangle closes and fill
        // the small ones, so the shard is watertight (and can be fractured again).
        private static void CloseSmallHoles(Piece piece, float maxSize)
        {
            // Pinholes and hairline slits have almost no area; a real opening (an open source mesh) has plenty
            float maxArea = maxSize * maxSize;

            Dictionary<long, int> directed = new Dictionary<long, int>();
            Dictionary<int, int> vertexOfPid = new Dictionary<int, int>();

            long Key(int a, int b) => ((long)a << 32) | (uint)b;

            foreach (List<int> triangles in piece.tris)
            {
                for (int t = 0; t + 2 < triangles.Count; t += 3)
                {
                    for (int e = 0; e < 3; e++)
                    {
                        int va = triangles[t + e];
                        int vb = triangles[t + (e + 1) % 3];
                        int a = piece.pid[va], b = piece.pid[vb];

                        if (!vertexOfPid.ContainsKey(a))
                            vertexOfPid[a] = va;

                        if (a == b)
                            continue;

                        long key = Key(a, b);
                        directed[key] = directed.TryGetValue(key, out int n) ? n + 1 : 1;
                    }
                }
            }

            Dictionary<int, List<int>> outgoing = new Dictionary<int, List<int>>();

            foreach (KeyValuePair<long, int> edge in directed)
            {
                int a = (int)(edge.Key >> 32);
                int b = (int)(edge.Key & 0xffffffffL);

                if (directed.ContainsKey(Key(b, a)))
                    continue;

                if (!outgoing.TryGetValue(a, out List<int> list))
                {
                    list = new List<int>(1);
                    outgoing[a] = list;
                }

                list.Add(b);
            }

            if (outgoing.Count == 0)
                return;

            HashSet<long> used = new HashSet<long>();
            List<int> interior = piece.tris[piece.InteriorSubMesh];

            foreach (KeyValuePair<int, List<int>> start in outgoing)
            {
                foreach (int firstTarget in start.Value)
                {
                    if (used.Contains(Key(start.Key, firstTarget)))
                        continue;

                    List<int> loop = new List<int>();
                    Vector3 area = Vector3.zero;
                    int from = start.Key, to = firstTarget;
                    bool closed = false;

                    for (int guard = 0; guard < 64; guard++)
                    {
                        used.Add(Key(from, to));
                        loop.Add(from);
                        area += Vector3.Cross(piece.pos[vertexOfPid[from]], piece.pos[vertexOfPid[to]]);

                        if (to == start.Key)
                        {
                            closed = true;
                            break;
                        }

                        int next = -1;
                        if (outgoing.TryGetValue(to, out List<int> candidates))
                        {
                            foreach (int candidate in candidates)
                            {
                                if (!used.Contains(Key(to, candidate)))
                                {
                                    next = candidate;
                                    break;
                                }
                            }
                        }

                        if (next < 0)
                            break;

                        from = to;
                        to = next;
                    }

                    if (!closed || loop.Count < 3 || area.magnitude * 0.5f > maxArea)
                        continue;

                    // Fill with a fan, wound against the outline so each new edge pairs with the existing one
                    int v0 = vertexOfPid[loop[0]];

                    for (int k = 1; k + 1 < loop.Count; k++)
                    {
                        interior.Add(v0);
                        interior.Add(vertexOfPid[loop[k + 1]]);
                        interior.Add(vertexOfPid[loop[k]]);
                    }
                }
            }
        }

        private static void ClipPolygon(List<SurfaceVertex> input, List<SurfaceVertex> output, CellPlane plane, float eps)
        {
            output.Clear();
            int count = input.Count;

            for (int i = 0; i < count; i++)
            {
                SurfaceVertex a = input[i];
                SurfaceVertex b = input[(i + 1) % count];
                float da = Vector3.Dot(plane.normal, a.p) - plane.d;
                float db = Vector3.Dot(plane.normal, b.p) - plane.d;

                if (da <= eps)
                    output.Add(a);

                if ((da < -eps && db > eps) || (da > eps && db < -eps))
                {
                    // Interpolate in a fixed direction, so neighbouring triangles get exactly the same point
                    if (Less(b.p, a.p))
                    {
                        SurfaceVertex swapVertex = a;
                        a = b;
                        b = swapVertex;
                        float swapDistance = da;
                        da = db;
                        db = swapDistance;
                    }

                    float t = da / (da - db);
                    Vector3 n = a.n + (b.n - a.n) * t;
                    if (n.sqrMagnitude > 1e-12f)
                        n.Normalize();

                    output.Add(new SurfaceVertex { p = a.p + (b.p - a.p) * t, n = n, t = a.t + (b.t - a.t) * t });
                }
            }
        }

        // The filled face on one cutting plane: the source's cross-section on that plane, trimmed to the cell's face.
        private static void FillCutFace(Piece src, TriangleSet triangles, CellFace face, CellPlane plane, Vector3 cellMin, Vector3 cellMax, FractureParams p,
            Piece dst, Func<Vector3, Vector3, Vector2, int> addVertex, ref FractureStats stats)
        {
            float eps = p.tolerances.plane;
            float weld = p.tolerances.weld;
            Vector3 normal = plane.normal;
            Basis(normal, out Vector3 u, out Vector3 v);

            Vector2 Flat(Vector3 point) => new Vector2(Vector3.Dot(point, u), Vector3.Dot(point, v));

            // The cell's face, counter-clockwise
            List<Vector2> cellFace = new List<Vector2>(face.points.Count);
            foreach (Vector3 point in face.points)
                cellFace.Add(Flat(point));

            // The source's cross-section, each segment with the inside of the source on its left
            List<Vector2> sectionFrom = new List<Vector2>();
            List<Vector2> sectionTo = new List<Vector2>();
            Vector3[] onPlane = new Vector3[3];

            // The whole cross-section, not just the part near the cell: the inside test below casts rays across it
            for (int k = 0; k < triangles.count; k++)
            {
                int ia = triangles.a[k], ib = triangles.b[k], ic = triangles.c[k];
                Vector3 pa = src.pos[ia], pb = src.pos[ib], pc = src.pos[ic];
                float da = Snap(Vector3.Dot(normal, pa) - plane.d, eps);
                float db = Snap(Vector3.Dot(normal, pb) - plane.d, eps);
                float dc = Snap(Vector3.Dot(normal, pc) - plane.d, eps);

                if ((da > 0f && db > 0f && dc > 0f) || (da < 0f && db < 0f && dc < 0f))
                    continue;

                int zeros = (da == 0f ? 1 : 0) + (db == 0f ? 1 : 0) + (dc == 0f ? 1 : 0);
                if (zeros == 3)
                    continue;

                // An edge lying on the plane belongs to the triangle on the inside, so it's counted once
                if (zeros == 2 && da + db + dc > 0f)
                    continue;

                int found = 0;
                CollectSectionPoint(pa, pb, da, db, onPlane, ref found);
                CollectSectionPoint(pb, pc, db, dc, onPlane, ref found);
                CollectSectionPoint(pc, pa, dc, da, onPlane, ref found);

                if (found != 2)
                    continue;

                Vector3 from = onPlane[0], to = onPlane[1];
                Vector3 outward = Vector3.Cross(pb - pa, pc - pa);

                if (Vector3.Dot(Vector3.Cross(normal, to - from), outward) > 0f)
                {
                    Vector3 swap = from;
                    from = to;
                    to = swap;
                }

                // No length filter here: tiny segments are merged by the welder below. Dropping them separately
                // breaks outlines where a vertex sits just off the plane and leaves a cluster of near-equal points.
                sectionFrom.Add(Flat(from));
                sectionTo.Add(Flat(to));
            }

            if (sectionFrom.Count == 0)
                return;

            List<Vector2> keptFrom = new List<Vector2>();
            List<Vector2> keptTo = new List<Vector2>();

            // 1. Cross-section segments inside the cell's face
            for (int i = 0; i < sectionFrom.Count; i++)
            {
                if (ClipSegmentToConvex(sectionFrom[i], sectionTo[i], cellFace, eps, out Vector2 a, out Vector2 b))
                {
                    keptFrom.Add(a);
                    keptTo.Add(b);
                }
            }

            // 2. Edges of the cell's face that run through the inside of the source
            List<float> splits = new List<float>();

            for (int e = 0; e < cellFace.Count; e++)
            {
                Vector2 e0 = cellFace[e];
                Vector2 e1 = cellFace[(e + 1) % cellFace.Count];
                float length = (e1 - e0).magnitude;

                if (length <= weld)
                    continue;

                splits.Clear();
                splits.Add(0f);
                splits.Add(1f);

                for (int i = 0; i < sectionFrom.Count; i++)
                {
                    if (SegmentCrossing(e0, e1, sectionFrom[i], sectionTo[i], out float t))
                        splits.Add(t);
                }

                splits.Sort();

                for (int i = 0; i + 1 < splits.Count; i++)
                {
                    float t0 = splits[i], t1 = splits[i + 1];

                    if (t1 <= t0)
                        continue;

                    Vector2 middle = e0 + (e1 - e0) * ((t0 + t1) * 0.5f);

                    if (InsideSection(middle, sectionFrom, sectionTo))
                    {
                        keptFrom.Add(e0 + (e1 - e0) * t0);
                        keptTo.Add(e0 + (e1 - e0) * t1);
                    }
                }
            }

            if (keptFrom.Count < 3)
                return;

            // Chain the kept segments into closed outlines
            PointWelder points = new PointWelder(weld);
            List<int> segmentFrom = new List<int>(keptFrom.Count);
            List<int> segmentTo = new List<int>(keptFrom.Count);

            for (int i = 0; i < keptFrom.Count; i++)
            {
                int a = points.FindOrAdd(new Vector3(keptFrom[i].x, keptFrom[i].y, 0f), 0, out _);
                int b = points.FindOrAdd(new Vector3(keptTo[i].x, keptTo[i].y, 0f), 0, out _);

                if (a != b)
                {
                    segmentFrom.Add(a);
                    segmentTo.Add(b);
                }
            }

            List<List<int>> loops = ChainLoops(segmentFrom, segmentTo, points.points, weld * 50f, ref stats);

            if (loops.Count == 0)
                return;

            List<Vector2> flat = new List<Vector2>(points.points.Count);
            foreach (Vector3 point in points.points)
                flat.Add(new Vector2(point.x, point.y));

            List<int> result = new List<int>();
            Triangulator.Triangulate(flat, loops, result);

            Dictionary<int, int> vertexOf = new Dictionary<int, int>();
            List<int> faceTriangles = new List<int>(result.Count);

            int CutVertex(int index)
            {
                if (!vertexOf.TryGetValue(index, out int vertex))
                {
                    Vector2 q = flat[index];
                    Vector3 position = u * q.x + v * q.y + normal * plane.d;
                    vertex = addVertex(position, normal, q * p.uvScale);
                    vertexOf[index] = vertex;
                }

                return vertex;
            }

            for (int t = 0; t + 2 < result.Count; t += 3)
            {
                int va = CutVertex(result[t]);
                int vb = CutVertex(result[t + 1]);
                int vc = CutVertex(result[t + 2]);

                if (dst.pid[va] == dst.pid[vb] || dst.pid[vb] == dst.pid[vc] || dst.pid[va] == dst.pid[vc])
                    continue;

                // The triangulator winds counter-clockwise seen from outside the cell (u x v = normal), which is
                // Unity's front face pointing out of the shard. Trust it rather than a cross product: a zero-area
                // triangle along a straight run has no reliable cross product.
                faceTriangles.Add(va);
                faceTriangles.Add(vb);
                faceTriangles.Add(vc);
            }

            dst.tris[dst.InteriorSubMesh].AddRange(faceTriangles);
        }

        // Neighbouring faces (two cut faces, or a cut face and the surface) can each put a zero-area sliver on their
        // shared edge, one each way round. Together they're a fin with no volume that leaves four triangles on one
        // edge: remove such mirrored pairs.
        private static void CancelMirroredPairs(Piece piece)
        {
            Dictionary<(int, int, int), (int sub, int t)> seen = new Dictionary<(int, int, int), (int, int)>();
            List<bool[]> cancelled = new List<bool[]>(piece.SubMeshCount);
            bool any = false;

            for (int s = 0; s < piece.SubMeshCount; s++)
            {
                List<int> triangles = piece.tris[s];
                bool[] flags = new bool[triangles.Count / 3];
                cancelled.Add(flags);

                for (int t = 0; t < flags.Length; t++)
                {
                    int a = piece.pid[triangles[t * 3]], b = piece.pid[triangles[t * 3 + 1]], c = piece.pid[triangles[t * 3 + 2]];
                    (int, int, int) reversed = RotateSmallestFirst(a, c, b);

                    if (seen.TryGetValue(reversed, out (int sub, int t) other))
                    {
                        flags[t] = true;
                        cancelled[other.sub][other.t] = true;
                        seen.Remove(reversed);
                        any = true;
                        continue;
                    }

                    seen[RotateSmallestFirst(a, b, c)] = (s, t);
                }
            }

            if (!any)
                return;

            for (int s = 0; s < piece.SubMeshCount; s++)
            {
                List<int> triangles = piece.tris[s];
                bool[] flags = cancelled[s];
                List<int> kept = new List<int>(triangles.Count);

                for (int t = 0; t < flags.Length; t++)
                {
                    if (flags[t])
                        continue;

                    kept.Add(triangles[t * 3]);
                    kept.Add(triangles[t * 3 + 1]);
                    kept.Add(triangles[t * 3 + 2]);
                }

                triangles.Clear();
                triangles.AddRange(kept);
            }
        }

        private static (int, int, int) RotateSmallestFirst(int a, int b, int c)
        {
            if (a <= b && a <= c)
                return (a, b, c);

            return b <= c ? (b, c, a) : (c, a, b);
        }

        private static void CollectSectionPoint(Vector3 a, Vector3 b, float da, float db, Vector3[] points, ref int found)
        {
            if (da == 0f)
            {
                if (found < 3)
                    points[found++] = a;
            }
            else if ((da < 0f && db > 0f) || (da > 0f && db < 0f))
            {
                if (found < 3)
                    points[found++] = IntersectOrdered(a, b, da, db);
            }
        }

        // Chains directed segments into closed outlines. A break smaller than bridge (a pinhole in the source) is
        // stepped over to the nearest loose end.
        private static List<List<int>> ChainLoops(List<int> segmentFrom, List<int> segmentTo, List<Vector3> points, float bridge, ref FractureStats stats)
        {
            int segmentCount = segmentFrom.Count;
            Dictionary<int, List<int>> outgoing = new Dictionary<int, List<int>>();

            for (int i = 0; i < segmentCount; i++)
            {
                if (!outgoing.TryGetValue(segmentFrom[i], out List<int> list))
                {
                    list = new List<int>(1);
                    outgoing[segmentFrom[i]] = list;
                }

                list.Add(i);
            }

            bool[] used = new bool[segmentCount];
            List<List<int>> loops = new List<List<int>>();

            for (int i = 0; i < segmentCount; i++)
            {
                if (used[i])
                    continue;

                List<int> loop = new List<int>();
                int start = segmentFrom[i];
                int current = i;
                bool closed = false;

                for (int guard = 0; guard <= segmentCount; guard++)
                {
                    used[current] = true;
                    loop.Add(segmentFrom[current]);
                    int end = segmentTo[current];

                    if (end == start)
                    {
                        closed = true;
                        break;
                    }

                    int next = -1;

                    if (outgoing.TryGetValue(end, out List<int> candidates))
                    {
                        foreach (int candidate in candidates)
                        {
                            if (!used[candidate])
                            {
                                next = candidate;
                                break;
                            }
                        }
                    }

                    if (next < 0)
                    {
                        Vector3 endPoint = points[end];

                        if ((points[start] - endPoint).sqrMagnitude <= bridge * bridge)
                        {
                            closed = true;
                            break;
                        }

                        float best = bridge * bridge;

                        for (int k = 0; k < segmentCount; k++)
                        {
                            if (used[k])
                                continue;

                            float distance = (points[segmentFrom[k]] - endPoint).sqrMagnitude;

                            if (distance <= best)
                            {
                                best = distance;
                                next = k;
                            }
                        }

                        if (next < 0)
                            break;
                    }

                    current = next;
                }

                if (closed && loop.Count >= 3)
                    loops.Add(loop);
                else if (!closed)
                    stats.openCuts++;
            }

            return loops;
        }

        // Clips a segment to a convex counter-clockwise polygon (Cyrus-Beck).
        private static bool ClipSegmentToConvex(Vector2 a, Vector2 b, List<Vector2> polygon, float tolerance, out Vector2 clippedA, out Vector2 clippedB)
        {
            float t0 = 0f, t1 = 1f;

            for (int e = 0; e < polygon.Count; e++)
            {
                Vector2 e0 = polygon[e];
                Vector2 edge = polygon[(e + 1) % polygon.Count] - e0;
                float length = edge.magnitude;

                if (length <= 1e-12f)
                    continue;

                // Signed distance to the edge, positive inside
                float fa = Cross(edge, a - e0) / length;
                float fb = Cross(edge, b - e0) / length;

                if (fa < -tolerance && fb < -tolerance)
                {
                    clippedA = clippedB = default;
                    return false;
                }

                // Clip exactly (no tolerance), so the cut point matches where the face's edge is split below
                if (fa < 0f && fb > fa)
                    t0 = Mathf.Max(t0, fa / (fa - fb));
                else if (fb < 0f && fa > fb)
                    t1 = Mathf.Min(t1, fa / (fa - fb));
            }

            if (t1 <= t0)
            {
                clippedA = clippedB = default;
                return false;
            }

            clippedA = a + (b - a) * t0;
            clippedB = a + (b - a) * t1;
            return true;
        }

        // Where segment (a, b) crosses edge (e0, e1), as a fraction along the edge.
        private static bool SegmentCrossing(Vector2 e0, Vector2 e1, Vector2 a, Vector2 b, out float t)
        {
            Vector2 r = e1 - e0;
            Vector2 s = b - a;
            float denominator = Cross(r, s);
            t = 0f;

            if (Mathf.Abs(denominator) <= 1e-12f)
                return false;

            Vector2 offset = a - e0;
            t = Cross(offset, s) / denominator;
            float along = Cross(offset, r) / denominator;

            return t > 0f && t < 1f && along >= 0f && along <= 1f;
        }

        // Inside the source's cross-section or not: ray parity in three skewed directions, majority wins. One ray
        // can run exactly through a vertex (symmetric meshes make that common) and count it twice.
        private static readonly Vector2[] RayDirections =
        {
            new Vector2(0.9135455f, 0.4067366f),
            new Vector2(-0.5877853f, 0.8090170f),
            new Vector2(-0.3420201f, -0.9396926f)
        };

        private static bool InsideSection(Vector2 point, List<Vector2> from, List<Vector2> to)
        {
            int votes = 0;

            foreach (Vector2 direction in RayDirections)
            {
                if (RayParity(point, direction, from, to))
                    votes++;
            }

            return votes >= 2;
        }

        private static bool RayParity(Vector2 point, Vector2 direction, List<Vector2> from, List<Vector2> to)
        {
            bool inside = false;

            for (int i = 0; i < from.Count; i++)
            {
                // In a frame where the ray runs along +x
                Vector2 a = from[i] - point;
                Vector2 b = to[i] - point;
                float ay = Cross(direction, a);
                float by = Cross(direction, b);

                if ((ay > 0f) == (by > 0f))
                    continue;

                float ax = Vector2.Dot(a, direction);
                float bx = Vector2.Dot(b, direction);

                if (ax + (bx - ax) * (-ay / (by - ay)) > 0f)
                    inside = !inside;
            }

            return inside;
        }

        // Makes vertices with the same position, normal and UV one vertex.
        private static Piece ShareVertices(Piece piece)
        {
            Piece result = new Piece(piece.SubMeshCount, piece.pos.Count / 2);
            Dictionary<(int, float, float, float, float, float), int> shared = new Dictionary<(int, float, float, float, float, float), int>();

            for (int s = 0; s < piece.SubMeshCount; s++)
            {
                foreach (int vertex in piece.tris[s])
                {
                    Vector3 n = piece.nrm[vertex];
                    Vector2 t = piece.uv[vertex];
                    var key = (piece.pid[vertex], n.x, n.y, n.z, t.x, t.y);

                    if (!shared.TryGetValue(key, out int index))
                    {
                        index = result.AddVertex(piece.pos[vertex], n, t, piece.pid[vertex]);
                        shared[key] = index;
                    }

                    result.tris[s].Add(index);
                }
            }

            return result;
        }

        private static float Snap(float distance, float eps)
        {
            return distance > eps || distance < -eps ? distance : 0f;
        }

        private static Vector3 IntersectOrdered(Vector3 a, Vector3 b, float da, float db)
        {
            if (Less(b, a))
            {
                Vector3 swap = a;
                a = b;
                b = swap;
                float swapDistance = da;
                da = db;
                db = swapDistance;
            }

            return a + (b - a) * (da / (da - db));
        }

        private static bool Less(Vector3 a, Vector3 b)
        {
            if (a.x != b.x) return a.x < b.x;
            if (a.y != b.y) return a.y < b.y;
            return a.z < b.z;
        }

        private static void Basis(Vector3 normal, out Vector3 u, out Vector3 v)
        {
            u = Mathf.Abs(normal.x) < 0.9f ? Vector3.Cross(normal, Vector3.right) : Vector3.Cross(normal, Vector3.up);
            u.Normalize();
            v = Vector3.Cross(normal, u);
        }

        private static bool Overlaps(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax)
        {
            return aMin.x <= bMax.x && aMax.x >= bMin.x && aMin.y <= bMax.y && aMax.y >= bMin.y && aMin.z <= bMax.z && aMax.z >= bMin.z;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static float MaxDistance(List<CellFace> faces, Vector3 from)
        {
            float best = 0f;

            foreach (CellFace face in faces)
            {
                foreach (Vector3 point in face.points)
                    best = Mathf.Max(best, (point - from).sqrMagnitude);
            }

            return Mathf.Sqrt(best);
        }
        #endregion

        #region Seeds
        public static List<Vector3> GenerateSeeds(Piece target, int count, PointMode mode, FractureParams p, System.Random rng)
        {
            GetBounds(target, out Vector3 min, out Vector3 max);
            Vector3 size = max - min;
            Vector3 centre = (min + max) * 0.5f;
            float diagonal = size.magnitude;
            List<Vector3> points = new List<Vector3>(count);

            switch (mode)
            {
                case PointMode.MeshVertices:
                {
                    Dictionary<int, Vector3> unique = new Dictionary<int, Vector3>();
                    for (int i = 0; i < target.pos.Count; i++)
                        unique[target.pid[i]] = target.pos[i];

                    List<Vector3> candidates = new List<Vector3>(unique.Values);

                    for (int i = candidates.Count - 1; i > 0; i--)
                    {
                        int j = rng.Next(i + 1);
                        Vector3 swap = candidates[i];
                        candidates[i] = candidates[j];
                        candidates[j] = swap;
                    }

                    for (int i = 0; i < candidates.Count && points.Count < count; i++)
                        points.Add(candidates[i]);
                    break;
                }

                case PointMode.Impact:
                {
                    int clustered = Mathf.RoundToInt(count * (1f - p.uniformFill));

                    for (int attempt = 0; attempt < clustered * 60 && points.Count < clustered; attempt++)
                    {
                        float r = p.impactRadius * Mathf.Pow((float)rng.NextDouble(), p.impactConcentration);
                        Vector3 candidate = p.impactPoint + RandomUnit(rng) * r;

                        if (InBounds(candidate, min, max) && IsInside(target, candidate))
                            points.Add(candidate);
                    }

                    break;
                }
            }

            // Fill the rest evenly through the volume
            int remaining = count - points.Count;

            for (int attempt = 0; attempt < remaining * 60 && points.Count < count; attempt++)
            {
                Vector3 candidate = RandomInBox(rng, min, max);
                if (IsInside(target, candidate))
                    points.Add(candidate);
            }

            // An open mesh can fail the inside test everywhere: fall back to the bounds
            while (points.Count < count)
                points.Add(RandomInBox(rng, min, max));

            if (p.noise > 0f)
            {
                float jitter = p.noise * diagonal / Mathf.Pow(Mathf.Max(count, 1), 1f / 3f) * 0.5f;

                for (int i = 0; i < points.Count; i++)
                    points[i] += RandomUnit(rng) * (jitter * (float)rng.NextDouble());
            }

            if (p.pointScale != Vector3.one)
            {
                for (int i = 0; i < points.Count; i++)
                    points[i] = centre + Vector3.Scale(points[i] - centre, p.pointScale);
            }

            // Two seeds at the same spot would make a zero-thickness cell
            float minimumSqr = (diagonal * 1e-4f) * (diagonal * 1e-4f);
            List<Vector3> distinct = new List<Vector3>(points.Count);

            foreach (Vector3 point in points)
            {
                bool duplicate = false;

                foreach (Vector3 kept in distinct)
                {
                    if ((kept - point).sqrMagnitude < minimumSqr)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                    distinct.Add(point);
            }

            return distinct;
        }

        private static Vector3 RandomInBox(System.Random rng, Vector3 min, Vector3 max)
        {
            return new Vector3(
                min.x + (max.x - min.x) * (float)rng.NextDouble(),
                min.y + (max.y - min.y) * (float)rng.NextDouble(),
                min.z + (max.z - min.z) * (float)rng.NextDouble());
        }

        private static Vector3 RandomUnit(System.Random rng)
        {
            for (int i = 0; i < 32; i++)
            {
                Vector3 v = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f);
                float lengthSqr = v.sqrMagnitude;

                if (lengthSqr > 1e-6f && lengthSqr <= 1f)
                    return v / Mathf.Sqrt(lengthSqr);
            }

            return Vector3.up;
        }

        private static bool InBounds(Vector3 p, Vector3 min, Vector3 max)
        {
            return p.x >= min.x && p.y >= min.y && p.z >= min.z && p.x <= max.x && p.y <= max.y && p.z <= max.z;
        }
        #endregion

        #region Queries
        // Ray parity test: an odd number of crossings means inside. Exact for closed meshes.
        public static bool IsInside(Piece piece, Vector3 point)
        {
            Vector3 direction = new Vector3(0.5377f, 0.7311f, 0.4192f).normalized;
            int hits = 0;

            foreach (List<int> triangles in piece.tris)
            {
                for (int t = 0; t + 2 < triangles.Count; t += 3)
                {
                    if (RayHitsTriangle(point, direction, piece.pos[triangles[t]], piece.pos[triangles[t + 1]], piece.pos[triangles[t + 2]]))
                        hits++;
                }
            }

            return (hits & 1) == 1;
        }

        private static bool RayHitsTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 edge1 = b - a;
            Vector3 edge2 = c - a;
            Vector3 h = Vector3.Cross(direction, edge2);
            float det = Vector3.Dot(edge1, h);

            if (Mathf.Abs(det) < 1e-12f)
                return false;

            float inverse = 1f / det;
            Vector3 s = origin - a;
            float u = inverse * Vector3.Dot(s, h);
            if (u < 0f || u > 1f)
                return false;

            Vector3 q = Vector3.Cross(s, edge1);
            float v = inverse * Vector3.Dot(direction, q);
            if (v < 0f || u + v > 1f)
                return false;

            return inverse * Vector3.Dot(edge2, q) > 1e-7f;
        }

        public static float Volume(Piece piece)
        {
            double volume = 0.0;

            foreach (List<int> triangles in piece.tris)
            {
                for (int t = 0; t + 2 < triangles.Count; t += 3)
                {
                    Vector3 a = piece.pos[triangles[t]];
                    Vector3 b = piece.pos[triangles[t + 1]];
                    Vector3 c = piece.pos[triangles[t + 2]];
                    volume += Vector3.Dot(a, Vector3.Cross(b, c));
                }
            }

            return (float)(volume / 6.0);
        }

        public static void GetBounds(Piece piece, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (Vector3 p in piece.pos)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            if (piece.pos.Count == 0)
            {
                min = Vector3.zero;
                max = Vector3.zero;
            }
        }

        // Splits a piece into its connected parts.
        public static List<Piece> SplitIslands(Piece piece)
        {
            Dictionary<int, int> local = new Dictionary<int, int>();
            List<int> parent = new List<int>();

            int Local(int id)
            {
                if (!local.TryGetValue(id, out int index))
                {
                    index = parent.Count;
                    local[id] = index;
                    parent.Add(index);
                }

                return index;
            }

            int Root(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }

                return x;
            }

            void Union(int a, int b)
            {
                int ra = Root(a), rb = Root(b);
                if (ra != rb)
                    parent[ra] = rb;
            }

            foreach (List<int> triangles in piece.tris)
            {
                for (int t = 0; t + 2 < triangles.Count; t += 3)
                {
                    int a = Local(piece.pid[triangles[t]]);
                    int b = Local(piece.pid[triangles[t + 1]]);
                    int c = Local(piece.pid[triangles[t + 2]]);
                    Union(a, b);
                    Union(a, c);
                }
            }

            Dictionary<int, int> groupOfRoot = new Dictionary<int, int>();
            for (int i = 0; i < parent.Count; i++)
            {
                int root = Root(i);
                if (!groupOfRoot.ContainsKey(root))
                    groupOfRoot[root] = groupOfRoot.Count;
            }

            if (groupOfRoot.Count <= 1)
                return new List<Piece> { piece };

            Piece[] parts = new Piece[groupOfRoot.Count];
            for (int g = 0; g < parts.Length; g++)
                parts[g] = new Piece(piece.SubMeshCount);

            int[] remap = new int[piece.pos.Count];
            for (int i = 0; i < remap.Length; i++)
                remap[i] = -1;

            for (int s = 0; s < piece.SubMeshCount; s++)
            {
                List<int> triangles = piece.tris[s];

                for (int t = 0; t + 2 < triangles.Count; t += 3)
                {
                    int group = groupOfRoot[Root(local[piece.pid[triangles[t]]])];
                    Piece part = parts[group];

                    for (int k = 0; k < 3; k++)
                    {
                        int vertex = triangles[t + k];

                        if (remap[vertex] < 0)
                            remap[vertex] = part.AddVertex(piece.pos[vertex], piece.nrm[vertex], piece.uv[vertex], piece.pid[vertex]);

                        part.tris[s].Add(remap[vertex]);
                    }
                }
            }

            return new List<Piece>(parts);
        }
        #endregion
    }

    // Fills flat outlines (with holes) with triangles: holes are bridged into their outer outline, then ear clipped.
    public static class Triangulator
    {
        public static void Triangulate(List<Vector2> points, List<List<int>> loops, List<int> result)
        {
            int loopCount = loops.Count;
            float[] area = new float[loopCount];
            int biggest = -1;
            float biggestArea = 0f;

            for (int i = 0; i < loopCount; i++)
            {
                area[i] = SignedArea(points, loops[i]);

                if (Mathf.Abs(area[i]) > biggestArea)
                {
                    biggestArea = Mathf.Abs(area[i]);
                    biggest = i;
                }
            }

            if (biggest < 0)
                return;

            // The largest outline is always an outer one; outlines wound the same way are outers, the rest are holes
            float outerSign = Mathf.Sign(area[biggest]);
            float tiny = biggestArea * 1e-9f;
            List<int> outers = new List<int>();
            List<int> holes = new List<int>();

            for (int i = 0; i < loopCount; i++)
            {
                if (Mathf.Abs(area[i]) <= tiny)
                    continue;

                if (Mathf.Sign(area[i]) == outerSign)
                {
                    if (area[i] < 0f)
                        loops[i].Reverse();
                    outers.Add(i);
                }
                else
                {
                    if (area[i] > 0f)
                        loops[i].Reverse();
                    holes.Add(i);
                }

                area[i] = Mathf.Abs(area[i]);
            }

            Dictionary<int, List<int>> holesOf = new Dictionary<int, List<int>>();

            foreach (int hole in holes)
            {
                Vector2 probe = points[loops[hole][0]];
                int owner = -1;
                float ownerArea = float.MaxValue;

                foreach (int outer in outers)
                {
                    if (area[outer] < ownerArea && PointInPolygon(probe, points, loops[outer]))
                    {
                        owner = outer;
                        ownerArea = area[outer];
                    }
                }

                if (owner < 0)
                    continue;

                if (!holesOf.TryGetValue(owner, out List<int> list))
                {
                    list = new List<int>();
                    holesOf[owner] = list;
                }

                list.Add(hole);
            }

            foreach (int outer in outers)
            {
                List<int> polygon = new List<int>(loops[outer]);

                if (holesOf.TryGetValue(outer, out List<int> ownHoles))
                {
                    // Rightmost holes first, so each bridge sees the outline it needs
                    ownHoles.Sort((a, b) => MaxX(points, loops[b]).CompareTo(MaxX(points, loops[a])));

                    foreach (int hole in ownHoles)
                        Bridge(points, polygon, loops[hole]);
                }

                EarClip(points, polygon, result);
            }
        }

        private static float SignedArea(List<Vector2> points, List<int> loop)
        {
            float sum = 0f;

            for (int i = 0; i < loop.Count; i++)
            {
                Vector2 a = points[loop[i]];
                Vector2 b = points[loop[(i + 1) % loop.Count]];
                sum += a.x * b.y - b.x * a.y;
            }

            return sum * 0.5f;
        }

        private static float MaxX(List<Vector2> points, List<int> loop)
        {
            float best = float.MinValue;
            foreach (int i in loop)
                best = Mathf.Max(best, points[i].x);
            return best;
        }

        private static bool PointInPolygon(Vector2 p, List<Vector2> points, List<int> loop)
        {
            bool inside = false;

            for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
            {
                Vector2 a = points[loop[i]];
                Vector2 b = points[loop[j]];

                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }

            return inside;
        }

        // Joins a hole (clockwise) into the outer outline (counter-clockwise) with a two-way cut, so one outline remains.
        private static void Bridge(List<Vector2> points, List<int> polygon, List<int> hole)
        {
            int holeStart = 0;
            for (int k = 1; k < hole.Count; k++)
            {
                if (points[hole[k]].x > points[hole[holeStart]].x)
                    holeStart = k;
            }

            Vector2 m = points[hole[holeStart]];
            int target = -1;
            float nearestX = float.MaxValue;
            Vector2 hit = m;

            // Cast a ray to the right and find the first outline edge it crosses
            for (int k = 0; k < polygon.Count; k++)
            {
                Vector2 a = points[polygon[k]];
                Vector2 b = points[polygon[(k + 1) % polygon.Count]];

                if ((a.y > m.y) == (b.y > m.y))
                    continue;

                float x = a.x + (m.y - a.y) * (b.x - a.x) / (b.y - a.y);

                if (x < m.x || x >= nearestX)
                    continue;

                nearestX = x;
                hit = new Vector2(x, m.y);
                target = a.x > b.x ? k : (k + 1) % polygon.Count;
            }

            if (target < 0)
            {
                // Numerical trouble: fall back to the nearest outline vertex
                float best = float.MaxValue;

                for (int k = 0; k < polygon.Count; k++)
                {
                    float d = (points[polygon[k]] - m).sqrMagnitude;

                    if (d < best)
                    {
                        best = d;
                        target = k;
                    }
                }
            }
            else
            {
                // A vertex inside the triangle (m, hit, target) would block the bridge: use the one closest in angle
                Vector2 p = points[polygon[target]];
                float bestAngle = float.MaxValue;
                float bestDistance = float.MaxValue;
                int blocker = -1;

                for (int k = 0; k < polygon.Count; k++)
                {
                    if (k == target)
                        continue;

                    Vector2 q = points[polygon[k]];

                    if (q.x < m.x || q == p || !InTriangleInclusive(q, m, hit, p))
                        continue;

                    float angle = Mathf.Abs(Mathf.Atan2(q.y - m.y, q.x - m.x));
                    float distance = (q - m).sqrMagnitude;

                    if (angle < bestAngle - 1e-6f || (Mathf.Abs(angle - bestAngle) <= 1e-6f && distance < bestDistance))
                    {
                        bestAngle = angle;
                        bestDistance = distance;
                        blocker = k;
                    }
                }

                if (blocker >= 0)
                    target = blocker;
            }

            List<int> merged = new List<int>(polygon.Count + hole.Count + 2);

            for (int k = 0; k <= target; k++)
                merged.Add(polygon[k]);

            for (int k = 0; k < hole.Count; k++)
                merged.Add(hole[(holeStart + k) % hole.Count]);

            merged.Add(hole[holeStart]);
            merged.Add(polygon[target]);

            for (int k = target + 1; k < polygon.Count; k++)
                merged.Add(polygon[k]);

            polygon.Clear();
            polygon.AddRange(merged);
        }

        private static void EarClip(List<Vector2> points, List<int> polygon, List<int> result)
        {
            List<int> v = new List<int>(polygon);

            // Area tolerance from the outline's size
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            foreach (int i in v)
            {
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }

            // A point closer than this to the line through its neighbours counts as on it. Cut points carry float
            // noise of about 1e-7 of the mesh size, so an area test alone mistakes straight runs for corners.
            float extent = (max - min).magnitude;
            float lineTolerance = extent * 2e-6f;

            int index = 0;
            int sinceLastEar = 0;

            while (v.Count > 3)
            {
                int n = v.Count;
                int previous = (index + n - 1) % n;
                int next = (index + 1) % n;

                Vector2 a = points[v[previous]];
                Vector2 b = points[v[index]];
                Vector2 c = points[v[next]];
                float cross = Cross(b - a, c - b);

                bool stuck = sinceLastEar > n;

                if (IsFlat(a, b, c, lineTolerance))
                {
                    // No area. A spike (the outline doubles back) is closed off at once; a vertex on a straight run
                    // waits for a real ear to swallow it, and is closed off only when nothing else is left. Either way
                    // it gets a zero-area triangle rather than vanishing, because the side faces share its edges.
                    if (stuck || Vector2.Dot(b - a, c - b) < 0f)
                    {
                        result.Add(v[previous]);
                        result.Add(v[index]);
                        result.Add(v[next]);
                        v.RemoveAt(index);

                        if (index >= v.Count)
                            index = 0;

                        sinceLastEar = 0;
                        continue;
                    }

                    index = (index + 1) % n;
                    sinceLastEar++;
                    continue;
                }

                if (cross > 0f && (stuck || !AnyPointInside(points, v, previous, index, next, a, b, c)))
                {
                    result.Add(v[previous]);
                    result.Add(v[index]);
                    result.Add(v[next]);
                    v.RemoveAt(index);

                    if (index >= v.Count)
                        index = 0;

                    sinceLastEar = 0;
                    continue;
                }

                if (sinceLastEar > 2 * n)
                    break;

                index = (index + 1) % n;
                sinceLastEar++;
            }

            if (v.Count == 3)
            {
                // Kept even with no area (a straight run): it joins edges the side faces share, so the shard stays
                // closed. Triangles whose corners coincide are dropped by the caller.
                result.Add(v[0]);
                result.Add(v[1]);
                result.Add(v[2]);
            }
            else if (v.Count > 3)
            {
                // Couldn't find ears (a badly self-intersecting outline): fan what's left rather than leave a hole
                for (int k = 1; k + 1 < v.Count; k++)
                {
                    result.Add(v[0]);
                    result.Add(v[k]);
                    result.Add(v[k + 1]);
                }
            }
        }

        private static bool AnyPointInside(List<Vector2> points, List<int> v, int previous, int index, int next, Vector2 a, Vector2 b, Vector2 c)
        {
            for (int k = 0; k < v.Count; k++)
            {
                if (k == previous || k == index || k == next)
                    continue;

                Vector2 q = points[v[k]];

                if (q == a || q == b || q == c)
                    continue;

                if (Cross(b - a, q - a) > 0f && Cross(c - b, q - b) > 0f && Cross(a - c, q - c) > 0f)
                    return true;
            }

            return false;
        }

        // True when b is within tolerance of the line from a to c, or the three points nearly coincide.
        private static bool IsFlat(Vector2 a, Vector2 b, Vector2 c, float tolerance)
        {
            float span = Mathf.Max((c - a).magnitude, Mathf.Max((b - a).magnitude, (c - b).magnitude));

            if (span <= tolerance)
                return true;

            return Mathf.Abs(Cross(b - a, c - a)) <= tolerance * span;
        }

        private static bool InTriangleInclusive(Vector2 q, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, q - a);
            float d2 = Cross(c - b, q - b);
            float d3 = Cross(a - c, q - c);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
    }
}
// FRACTURE-CORE-END
