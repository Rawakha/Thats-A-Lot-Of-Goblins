using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Baked flat grass shelves. Horizontal bands split at polygon vertices and edge crossings so overlaps merge
// exactly, holes stay open, and tops remain flat. Band data also supplies allocation-free gameplay height queries.
// Mesh generation allocates only during authoring. No runtime callbacks or subscriptions.
[Serializable]
public class Level_GrassLayerData
{
    [Serializable] public struct Band { public float z0, z1; public int first, count; }
    [Serializable] public struct Span { public float left0, right0, left1, right1; }
    public Mesh mesh;
    public float height;
    public Band[] bands = Array.Empty<Band>();
    public Span[] spans = Array.Empty<Span>();

    public bool Contains(Vector2 point)
    {
        int low = 0, high = bands.Length - 1;
        while (low <= high)
        {
            int middle = (low + high) / 2;
            Band band = bands[middle];
            if (point.y < band.z0) { high = middle - 1; continue; }
            if (point.y > band.z1) { low = middle + 1; continue; }
            float t = Mathf.Clamp01((point.y - band.z0) / (band.z1 - band.z0));
            if (InBand(point.x, band, t)) return true;
            // A shared band boundary can belong to the adjacent band instead.
            if (Mathf.Abs(point.y - band.z0) < 0.0001f && middle > 0)
                return InBand(point.x, bands[middle - 1], 1f);
            if (Mathf.Abs(point.y - band.z1) < 0.0001f && middle + 1 < bands.Length)
                return InBand(point.x, bands[middle + 1], 0f);
            return false;
        }
        return false;
    }

    private bool InBand(float x, Band band, float t)
    {
        int low = band.first, high = band.first + band.count - 1;
        while (low <= high)
        {
            int middle = (low + high) / 2;
            Span span = spans[middle];
            if (x < Mathf.Lerp(span.left0, span.left1, t) - 0.0001f) high = middle - 1;
            else if (x > Mathf.Lerp(span.right0, span.right1, t) + 0.0001f) low = middle + 1;
            else return true;
        }
        return false;
    }
}

public static class Level_GrassLayerMesh
{
    private const float Epsilon = 0.0001f;
    private struct Edge
    {
        public Vector2 a, b;
        public float X(float z) => a.x + (z - a.y) * (b.x - a.x) / (b.y - a.y);
    }
    private struct Interval { public Edge left, right; public float x0, x1; }

    public static Level_GrassLayerData Build(List<Vector2[]> polygons, List<Vector2[]> holes, Vector2 size,
        float height, float sideShade, List<Vector2[]> otherLayer = null, Func<Vector2, float> sideFloor = null)
    {
        var edges = new List<Edge>();
        var levels = new List<float> { -size.y * 0.5f, size.y * 0.5f };
        AddEdges(polygons, edges, levels);
        AddEdges(holes, edges, levels);
        if (otherLayer != null) AddEdges(otherLayer, edges, levels);
        Edge left = new Edge { a = new Vector2(-size.x * 0.5f, -size.y * 0.5f), b = new Vector2(-size.x * 0.5f, size.y * 0.5f) };
        Edge right = new Edge { a = new Vector2(size.x * 0.5f, -size.y * 0.5f), b = new Vector2(size.x * 0.5f, size.y * 0.5f) };
        edges.Add(left); edges.Add(right);
        if (edges.Count > 2048) throw new InvalidOperationException("Grass outlines exceed 2048 edges. Split the ground into smaller pieces.");
        for (int i = 0; i < edges.Count; i++)
        for (int j = i + 1; j < edges.Count; j++)
        {
            Vector2 a = edges[i].b - edges[i].a, b = edges[j].b - edges[j].a;
            float cross = a.x * b.y - a.y * b.x;
            if (Mathf.Abs(cross) < 0.000001f) continue;
            Vector2 offset = edges[j].a - edges[i].a;
            float t = (offset.x * b.y - offset.y * b.x) / cross;
            float u = (offset.x * a.y - offset.y * a.x) / cross;
            if (t > 0f && t < 1f && u > 0f && u < 1f) levels.Add(edges[i].a.y + a.y * t);
        }
        levels.Sort();
        var z = new List<float>();
        foreach (float value in levels)
            if (value >= -size.y * 0.5f && value <= size.y * 0.5f && (z.Count == 0 || value - z[z.Count - 1] > Epsilon)) z.Add(value);
        if (z.Count > 20000) throw new InvalidOperationException("Grass outlines have too many intersections. Simplify the outlines.");
        var bands = new List<Level_GrassLayerData.Band>();
        var spans = new List<Level_GrassLayerData.Span>();
        var knots = new List<float>[z.Count];
        for (int i = 0; i < knots.Length; i++) knots[i] = new List<float>();
        for (int i = 0; i < z.Count - 1; i++)
        {
            float middle = (z[i] + z[i + 1]) * 0.5f;
            List<Interval> fill = Union(polygons, middle);
            List<Interval> cuts = Union(holes, middle);
            var band = new Level_GrassLayerData.Band { z0 = z[i], z1 = z[i + 1], first = spans.Count };
            foreach (Interval region in fill)
            {
                var pieces = new List<Interval>();
                if (region.x1 <= left.a.x || region.x0 >= right.a.x) continue;
                Interval clipped = region;
                if (clipped.x0 < left.a.x) { clipped.left = left; clipped.x0 = left.a.x; }
                if (clipped.x1 > right.a.x) { clipped.right = right; clipped.x1 = right.a.x; }
                pieces.Add(clipped);
                foreach (Interval cut in cuts)
                {
                    for (int p = pieces.Count - 1; p >= 0; p--)
                    {
                        Interval piece = pieces[p];
                        if (cut.x1 <= piece.x0 || cut.x0 >= piece.x1) continue;
                        pieces.RemoveAt(p);
                        if (cut.x0 > piece.x0) pieces.Add(new Interval { left = piece.left, right = cut.left, x0 = piece.x0, x1 = cut.x0 });
                        if (cut.x1 < piece.x1) pieces.Add(new Interval { left = cut.right, right = piece.right, x0 = cut.x1, x1 = piece.x1 });
                    }
                }
                pieces.Sort((a, b) => a.x0.CompareTo(b.x0));
                foreach (Interval piece in pieces)
                {
                    var span = new Level_GrassLayerData.Span { left0 = piece.left.X(z[i]), right0 = piece.right.X(z[i]), left1 = piece.left.X(z[i + 1]), right1 = piece.right.X(z[i + 1]) };
                    spans.Add(span);
                    knots[i].Add(span.left0); knots[i].Add(span.right0);
                    knots[i + 1].Add(span.left1); knots[i + 1].Add(span.right1);
                }
            }
            band.count = spans.Count - band.first;
            bands.Add(band);
        }
        foreach (List<float> row in knots) row.Sort();
        var vertices = new List<Vector3>();
        var colours = new List<Color>();
        var triangles = new List<int>();
        var vertexIds = new Dictionary<Vector2Int, int>();
        var boundary = new Dictionary<ulong, Vector2Int>();
        for (int i = 0; i < bands.Count; i++)
        {
            Level_GrassLayerData.Band band = bands[i];
            for (int s = band.first; s < band.first + band.count; s++)
            {
                Level_GrassLayerData.Span span = spans[s];
                var polygon = new List<Vector2>();
                Row(polygon, knots[i], span.left0, span.right0, band.z0, false);
                Row(polygon, knots[i + 1], span.left1, span.right1, band.z1, true);
                var ids = new List<int>();
                foreach (Vector2 point in polygon)
                {
                    Vector2Int key = new Vector2Int(Mathf.RoundToInt(point.x * 10000f), Mathf.RoundToInt(point.y * 10000f));
                    if (!vertexIds.TryGetValue(key, out int id))
                    { id = vertices.Count; vertexIds.Add(key, id); vertices.Add(new Vector3(point.x, height, point.y)); colours.Add(Color.white); }
                    if (ids.Count == 0 || ids[ids.Count - 1] != id) ids.Add(id);
                }
                if (ids.Count > 1 && ids[0] == ids[ids.Count - 1]) ids.RemoveAt(ids.Count - 1);
                if (ids.Count < 3) continue;
                Vector3 centre = Vector3.zero;
                foreach (int id in ids) centre += vertices[id];
                centre /= ids.Count;
                int centreId = vertices.Count; vertices.Add(centre); colours.Add(Color.white);
                for (int p = 0; p < ids.Count; p++)
                {
                    int a = ids[p], b = ids[(p + 1) % ids.Count];
                    triangles.Add(centreId); triangles.Add(b); triangles.Add(a);
                    ulong key = ((ulong)(uint)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
                    if (!boundary.Remove(key)) boundary.Add(key, new Vector2Int(a, b));
                }
            }
        }
        Color sideColour = new Color(sideShade, sideShade, sideShade, 1f);
        foreach (Vector2Int edge in boundary.Values)
        {
            Vector3 a = vertices[edge.x], b = vertices[edge.y];
            float bottom = sideFloor != null ? sideFloor(new Vector2((a.x + b.x) * 0.5f, (a.z + b.z) * 0.5f)) : 0f;
            bottom = Mathf.Min(bottom, height);
            if (height - bottom <= 0.000001f) continue;
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(new Vector3(b.x, bottom, b.z)); vertices.Add(new Vector3(a.x, bottom, a.z));
            for (int i = 0; i < 4; i++) colours.Add(sideColour);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
        if (vertices.Count > 250000) throw new InvalidOperationException("Grass mesh exceeds 250,000 vertices. Simplify the outlines.");
        var mesh = new Mesh { name = "Merged Grass Layer", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.SetVertices(vertices); mesh.SetColors(colours); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return new Level_GrassLayerData { mesh = mesh, height = height, bands = bands.ToArray(), spans = spans.ToArray() };
    }

    private static void AddEdges(List<Vector2[]> polygons, List<Edge> edges, List<float> levels)
    {
        foreach (Vector2[] polygon in polygons)
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
            levels.Add(a.y);
            if (Mathf.Abs(a.y - b.y) > 0.000001f) edges.Add(new Edge { a = a, b = b });
        }
    }

    private static List<Interval> Union(List<Vector2[]> polygons, float z)
    {
        var all = new List<Interval>();
        foreach (Vector2[] polygon in polygons)
        {
            var hits = new List<Edge>();
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                if ((a.y > z) != (b.y > z)) hits.Add(new Edge { a = a, b = b });
            }
            hits.Sort((a, b) => a.X(z).CompareTo(b.X(z)));
            for (int i = 0; i + 1 < hits.Count; i += 2)
                all.Add(new Interval { left = hits[i], right = hits[i + 1], x0 = hits[i].X(z), x1 = hits[i + 1].X(z) });
        }
        all.Sort((a, b) => a.x0.CompareTo(b.x0));
        var merged = new List<Interval>();
        foreach (Interval piece in all)
        {
            if (merged.Count == 0 || piece.x0 > merged[merged.Count - 1].x1 + Epsilon) { merged.Add(piece); continue; }
            Interval last = merged[merged.Count - 1];
            if (piece.x1 > last.x1) { last.right = piece.right; last.x1 = piece.x1; merged[merged.Count - 1] = last; }
        }
        return merged;
    }

    private static void Row(List<Vector2> polygon, List<float> knots, float left, float right, float z, bool reverse)
    {
        var row = new List<float> { left };
        foreach (float x in knots)
            if (x > left + Epsilon && x < right - Epsilon && x - row[row.Count - 1] > Epsilon) row.Add(x);
        if (right - row[row.Count - 1] > Epsilon) row.Add(right);
        if (reverse) row.Reverse();
        foreach (float x in row) polygon.Add(new Vector2(x, z));
    }
}
