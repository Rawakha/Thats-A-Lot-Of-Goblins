#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Combines source MeshFilters into ONE mesh and renderer, with a submesh per material. Copies the actual meshes,
// materials and transforms; the sources stay editable. It knows nothing about stones or walls: a source that needs
// preparing implements Level_IBakeSource. Used by Level_BakeGroup, so tools don't call it themselves.
// Also owns safe mesh cleanup and saving a baked mesh as an asset.
//
// Setup: none. GC: editor only (working lists on each bake); no ticks. One delayCall per batch of mesh releases.
public static class Level_MeshBaker
{
    private const int MaxSources = 4096;
    private const int MaxSourceVertices = 60000;
    private const int MaxTotalVertices = 1000000;

    // Returns the inactive output object, or throws and leaves nothing behind
    public static GameObject Bake(Transform owner, IEnumerable<MeshFilter> sources, bool collider, out int pieces)
    {
        GameObject output = new GameObject("Baked Level Mesh");
        output.SetActive(false);
        output.transform.SetParent(owner, false);
        Mesh combined = null;
        pieces = 0;

        try
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector4> tangents = new List<Vector4>();
            List<Color> colours = new List<Color>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector4>[] extraUVs = new List<Vector4>[8];
            List<Material> materials = new List<Material>();
            List<List<int>> submeshes = new List<List<int>>();

            foreach (MeshFilter filter in sources)
            {
                filter.TryGetComponent(out Level_IBakeSource source);

                if (source is Behaviour behaviour && !behaviour.enabled)
                    continue;

                if (!filter.TryGetComponent(out MeshRenderer meshRenderer) || !meshRenderer.enabled)
                    continue;

                source?.PrepareForBake();

                if (++pieces > MaxSources)
                    throw new InvalidOperationException($"The bake exceeds {MaxSources} source objects.");

                AddSource(owner, filter, meshRenderer, vertices, normals, tangents, colours, uvs, extraUVs, materials, submeshes);

                if (vertices.Count > MaxTotalVertices)
                    throw new InvalidOperationException("The combined geometry exceeds one million vertices.");
            }

            combined = new Mesh
            {
                name = "Combined Level Mesh",
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            combined.SetVertices(vertices);
            combined.SetNormals(normals);
            combined.SetColors(colours);
            combined.SetUVs(0, uvs);
            combined.SetTangents(tangents);

            for (int channel = 1; channel < 8; channel++)
            {
                if (extraUVs[channel] == null)
                    continue;

                while (extraUVs[channel].Count < vertices.Count)
                    extraUVs[channel].Add(Vector4.zero);

                combined.SetUVs(channel, extraUVs[channel]);
            }

            combined.subMeshCount = submeshes.Count;

            for (int sub = 0; sub < submeshes.Count; sub++)
                combined.SetTriangles(submeshes[sub], sub);

            combined.RecalculateBounds();

            GameObject geometry = new GameObject("Combined Geometry", typeof(MeshFilter), typeof(MeshRenderer));
            geometry.transform.SetParent(output.transform, false);
            geometry.layer = owner.gameObject.layer;
            geometry.GetComponent<MeshFilter>().sharedMesh = combined;
            geometry.GetComponent<MeshRenderer>().sharedMaterials = materials.ToArray();

            if (collider && combined.vertexCount > 0)
                geometry.AddComponent<MeshCollider>().sharedMesh = combined;

            return output;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(output);

            if (combined != null)
                UnityEngine.Object.DestroyImmediate(combined);

            throw;
        }
    }

    private static void AddSource(Transform owner, MeshFilter filter, MeshRenderer meshRenderer, List<Vector3> vertices,
        List<Vector3> normals, List<Vector4> tangents, List<Color> colours, List<Vector2> uvs, List<Vector4>[] extraUVs,
        List<Material> materials, List<List<int>> submeshes)
    {
        Mesh mesh = filter.sharedMesh;

        if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0)
            throw new InvalidOperationException(filter.name + ": the source needs a readable mesh.");

        if (mesh.vertexCount > MaxSourceVertices)
            throw new InvalidOperationException($"{filter.name}: the source exceeds {MaxSourceVertices} vertices.");

        Matrix4x4 matrix = owner.worldToLocalMatrix * filter.transform.localToWorldMatrix;

        for (int element = 0; element < 16; element++)
        {
            if (!float.IsFinite(matrix[element]))
                throw new InvalidOperationException(filter.name + ": the source transform must be finite.");
        }

        if (Mathf.Abs(matrix.determinant) < 0.00000001f)
            throw new InvalidOperationException(filter.name + ": the source scale is zero or too small to bake.");

        Matrix4x4 normalMatrix = matrix.inverse.transpose;
        bool mirrored = matrix.determinant < 0f;
        Material[] sourceMaterials = meshRenderer.sharedMaterials;
        Vector3[] sourceVertices = mesh.vertices;
        Vector3[] sourceNormals = mesh.normals;
        Vector4[] sourceTangents = mesh.tangents;
        Vector2[] sourceUVs = mesh.uv;
        Color[] sourceColours = mesh.colors;
        int first = vertices.Count;

        // Every part is checked before anything is added, so a failed source can't leave half a mesh
        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            if (sourceMaterials.Length == 0 || sourceMaterials[Mathf.Min(sub, sourceMaterials.Length - 1)] == null)
                throw new InvalidOperationException(filter.name + ": the source material is missing.");

            if (mesh.GetTopology(sub) != MeshTopology.Triangles)
                throw new InvalidOperationException(filter.name + ": only triangle meshes can be baked.");
        }

        for (int v = 0; v < sourceVertices.Length; v++)
        {
            Vector3 normal = sourceNormals.Length == sourceVertices.Length ? normalMatrix.MultiplyVector(sourceNormals[v]).normalized : Vector3.up;
            vertices.Add(matrix.MultiplyPoint3x4(sourceVertices[v]));
            normals.Add(normal);
            uvs.Add(sourceUVs.Length == sourceVertices.Length ? sourceUVs[v] : Vector2.zero);
            colours.Add(sourceColours.Length == sourceVertices.Length ? sourceColours[v] : Color.white);

            if (sourceTangents.Length == sourceVertices.Length)
            {
                Vector4 tangent = sourceTangents[v];
                Vector3 axis = matrix.MultiplyVector(new Vector3(tangent.x, tangent.y, tangent.z));
                axis = (axis - normal * Vector3.Dot(normal, axis)).normalized;
                tangents.Add(new Vector4(axis.x, axis.y, axis.z, mirrored ? -tangent.w : tangent.w));
            }
            else
            {
                tangents.Add(Vector4.zero);
            }
        }

        for (int channel = 1; channel < 8; channel++)
        {
            if (!mesh.HasVertexAttribute((VertexAttribute)((int)VertexAttribute.TexCoord0 + channel)))
                continue;

            List<Vector4> incoming = new List<Vector4>(sourceVertices.Length);
            mesh.GetUVs(channel, incoming);
            List<Vector4> values = extraUVs[channel] ??= new List<Vector4>();

            while (values.Count < first)
                values.Add(Vector4.zero);

            values.AddRange(incoming);
        }

        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            Material material = sourceMaterials[Mathf.Min(sub, sourceMaterials.Length - 1)];
            int slot = materials.IndexOf(material);

            if (slot < 0)
            {
                slot = materials.Count;
                materials.Add(material);
                submeshes.Add(new List<int>());
            }

            List<int> output = submeshes[slot];
            int[] indices = mesh.GetTriangles(sub);

            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                output.Add(first + indices[i]);
                output.Add(first + indices[i + (mirrored ? 2 : 1)]);
                output.Add(first + indices[i + (mirrored ? 1 : 2)]);
            }
        }
    }

    // Moves the baked mesh into an asset, so it survives prefabs. Rebaking overwrites the same asset (existing), so
    // the mesh's contents can't be undone. Returns the asset.
    public static Mesh SaveAsAsset(GameObject output, Mesh existing, string folder, string label)
    {
        MeshFilter filter = output.GetComponentInChildren<MeshFilter>(true);
        Mesh baked = filter.sharedMesh;
        Mesh asset = existing;

        if (asset != null)
        {
            EditorUtility.CopySerialized(baked, asset);
            UnityEngine.Object.DestroyImmediate(baked);
        }
        else
        {
            MakeFolder(folder);

            foreach (char bad in Path.GetInvalidFileNameChars())
                label = label.Replace(bad, '_');

            AssetDatabase.CreateAsset(baked, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{label} Baked.asset"));
            asset = baked;
        }

        filter.sharedMesh = asset;

        if (filter.TryGetComponent(out MeshCollider meshCollider))
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = asset;
        }

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }

    private static void MakeFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        MakeFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    // Removes an output object now and its in-memory meshes once nothing uses them. Not undoable: bakes use
    // Undo.DestroyObjectImmediate on their replaced output instead, keeping its mesh for the undo.
    public static void RemoveOutput(GameObject output)
    {
        if (output == null)
            return;

        HashSet<Mesh> meshes = new HashSet<Mesh>();

        foreach (MeshFilter filter in output.GetComponentsInChildren<MeshFilter>(true))
            meshes.Add(filter.sharedMesh);

        UnityEngine.Object.DestroyImmediate(output);

        foreach (Mesh mesh in meshes)
            ReleaseUnusedMesh(mesh);
    }

    private static readonly HashSet<Mesh> unusedMeshes = new HashSet<Mesh>();
    private static bool cleanupQueued;

    // Batches native mesh cleanup once per editor rebuild, rather than scanning the scene for every piece.
    // Duplicated objects may still share a mesh; filters and colliders both keep it alive.
    public static void ReleaseUnusedMesh(Mesh mesh)
    {
        if (mesh == null || EditorUtility.IsPersistent(mesh))
            return;

        unusedMeshes.Add(mesh);

        if (cleanupQueued)
            return;

        cleanupQueued = true;
        EditorApplication.delayCall += ReleaseUnusedMeshes;
    }

    private static void ReleaseUnusedMeshes()
    {
        cleanupQueued = false;

        foreach (MeshFilter filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            unusedMeshes.Remove(filter.sharedMesh);

        foreach (MeshCollider meshCollider in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include))
            unusedMeshes.Remove(meshCollider.sharedMesh);

        foreach (Mesh mesh in unusedMeshes)
        {
            if (mesh != null && !EditorUtility.IsPersistent(mesh))
                UnityEngine.Object.DestroyImmediate(mesh);
        }

        unusedMeshes.Clear();
    }
}
#endif
