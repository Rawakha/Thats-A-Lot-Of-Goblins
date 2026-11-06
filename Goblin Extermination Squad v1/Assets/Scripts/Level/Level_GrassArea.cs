using System.Collections.Generic;
using UnityEngine;

// A grass outline under a Level_GroundSurface. Shape it with the same node handles as cliffs.
// The surface merges outlines of the same type into a flat shelf mesh. Keep these GameObjects as editable sources.
// GC: editor baking only. No runtime ticks, allocations or subscriptions.
[ExecuteAlways, DisallowMultipleComponent]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "Level_DarkGrassArea")]
public class Level_GrassArea : MonoBehaviour
{
    public enum Kind { Dark, Light }
    [SerializeField] private Kind layer = Kind.Dark;
    public Kind Layer => layer;

    [Tooltip("Outline in this object's XZ plane. Concave outlines are supported. Edges must not cross.")]
    [SerializeField] private List<Vector3> nodes = new List<Vector3>
    {
        new Vector3(-6f, 0f, -6f), new Vector3(6f, 0f, -6f),
        new Vector3(6f, 0f, 6f), new Vector3(-6f, 0f, 6f)
    };
    [SerializeField, Min(0f)] private float snap = 0.5f;

    public List<Vector3> Nodes => nodes;
    public float Snap => snap;

#if UNITY_EDITOR
    private void OnEnable() => Level_GroundSurface.RequestRebuilds();
    private void OnDisable() => Level_GroundSurface.RequestRebuilds();
    private void OnValidate() => Level_GroundSurface.RequestRebuilds();
#endif
}

// Polygon maths shared by the mask baker and its validation. All coordinates are ground-local metres.
public static class Level_SurfaceShape
{
    public static float SignedDistance(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        float nearest = float.PositiveInfinity;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[j];
            Vector2 b = polygon[i];
            Vector2 edge = b - a;
            float t = edge.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude) : 0f;
            nearest = Mathf.Min(nearest, (point - a - edge * t).sqrMagnitude);
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < a.x + (point.y - a.y) * (b.x - a.x) / (b.y - a.y))
                inside = !inside;
        }
        return Mathf.Sqrt(nearest) * (inside ? 1f : -1f);
    }

    public static bool Valid(Vector2[] polygon)
    {
        if (polygon.Length < 3)
            return false;
        double area = 0;
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Length];
            if (!float.IsFinite(a.x) || !float.IsFinite(a.y) || (b - a).sqrMagnitude < 0.000001f)
                return false;
            area += (double)a.x * b.y - (double)a.y * b.x;
            for (int j = i + 1; j < polygon.Length; j++)
            {
                if (j == i + 1 || (i == 0 && j == polygon.Length - 1))
                    continue;
                if (Crosses(a, b, polygon[j], polygon[(j + 1) % polygon.Length]))
                    return false;
            }
        }
        return System.Math.Abs(area) > 0.0001;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

    private static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        if (Mathf.Max(a.x, b.x) < Mathf.Min(c.x, d.x) || Mathf.Max(c.x, d.x) < Mathf.Min(a.x, b.x) ||
            Mathf.Max(a.y, b.y) < Mathf.Min(c.y, d.y) || Mathf.Max(c.y, d.y) < Mathf.Min(a.y, b.y))
            return false;
        return Cross(a, b, c) * Cross(a, b, d) <= 0f && Cross(c, d, a) * Cross(c, d, b) <= 0f;
    }
}
