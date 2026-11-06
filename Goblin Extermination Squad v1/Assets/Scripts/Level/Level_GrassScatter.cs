using System;
using UnityEngine;

// Deterministic jittered scatter over the shelf union's saved bands. Half-open ownership avoids duplicate
// cards at band/span seams. Authoring only: callbacks and mesh working data may allocate; no runtime ticks.
public static class Level_GrassScatter
{
    public static void Visit(Level_GrassLayerData data, float density, int seed, ref int attempts, Action<Vector2, int, int> add, float jitter = 0.7f)
    {
        if (density == 0) return;
        if (!float.IsFinite(density) || density < 0 || density > 200)
            throw new InvalidOperationException("Use density between zero and 200 cards per square metre.");
        float step = 1f / Mathf.Sqrt(density);
        jitter = Mathf.Clamp01(jitter);
        // Scan only span bounds rather than every point in the entire ground rectangle.
        foreach (Level_GrassLayerData.Band band in data.bands)
        for (int s = band.first; s < band.first + band.count; s++)
        {
            Level_GrassLayerData.Span span = data.spans[s];
            int z0 = Mathf.FloorToInt(band.z0 / step), z1 = Mathf.CeilToInt(band.z1 / step);
            int x0 = Mathf.FloorToInt(Mathf.Min(span.left0, span.left1) / step), x1 = Mathf.CeilToInt(Mathf.Max(span.right0, span.right1) / step);
            if (((long)z1 - z0) * ((long)x1 - x0) + attempts > 4000000)
                throw new InvalidOperationException("Scatter exceeds four million candidate checks. Reduce density or simplify outlines.");
            for (int z = z0; z < z1; z++)
            for (int x = x0; x < x1; x++)
            {
                attempts++;
                Vector2 point = new Vector2((x + 0.5f + (Unit(Hash(x, z, seed)) - 0.5f) * jitter) * step,
                    (z + 0.5f + (Unit(Hash(x, z, seed + 1)) - 0.5f) * jitter) * step);
                if (point.y < band.z0 || point.y >= band.z1) continue;
                float t = (point.y - band.z0) / (band.z1 - band.z0);
                if (point.x < Mathf.Lerp(span.left0, span.left1, t) || point.x >= Mathf.Lerp(span.right0, span.right1, t)) continue;
                add(point, x, z);
            }
        }
    }

    public static uint Hash(int x, int z, int seed)
    {
        unchecked { uint h = (uint)x * 374761393u + (uint)z * 668265263u + (uint)seed * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u; return h ^ (h >> 16); }
    }
    public static float Unit(uint hash) => (hash & 0xffffff) / 16777216f;
}
