using System.Collections.Generic;
using UnityEngine;

// Triangulates a flat polygon with holes in it, for the level tools (the ground with its hole openings, later any
// cut-out shape). Ear clipping: each hole is first joined to the outline by a bridge (to the nearest outline vertex it
// can see, after Eberly's method), making one outline that walks round the holes, which is then clipped ear by ear.
//
// Points are 2D (for the ground: x and z). The outline should go anticlockwise and holes clockwise; holes must sit
// inside the outline and not touch each other or it. Output triangles are anticlockwise index triples into Points.
//
// GC: allocates its working lists; it's for edit time and level loading, not per frame.
public static class Level_Triangulator
{
    private const float Epsilon = 1e-7f;

    // Points ends up holding the outline's points then every hole's, in order; triangles index into it
    public static List<int> Triangulate(List<Vector2> outline, List<List<Vector2>> holes, List<Vector2> points)
    {
        points.Clear();
        List<int> polygon = new List<int>(outline.Count);

        for (int i = 0; i < outline.Count; i++)
        {
            polygon.Add(points.Count);
            points.Add(outline[i]);
        }

        // Holes are bridged in from right to left, so each bridge only crosses outline that's already final
        List<int> starts = new List<int>(holes.Count);
        List<int> order = new List<int>(holes.Count);

        for (int h = 0; h < holes.Count; h++)
        {
            starts.Add(points.Count);
            points.AddRange(holes[h]);
            order.Add(h);
        }

        order.Sort((a, b) => MaxX(holes[b]).CompareTo(MaxX(holes[a])));

        for (int o = 0; o < order.Count; o++)
        {
            int h = order[o];

            if (holes[h].Count >= 3)
                Bridge(polygon, points, starts[h], holes[h].Count);
        }

        return EarClip(polygon, points);
    }

    private static float MaxX(List<Vector2> ring)
    {
        float max = float.MinValue;

        for (int i = 0; i < ring.Count; i++)
            max = Mathf.Max(max, ring[i].x);

        return max;
    }

    // Joins a hole into the polygon: from its rightmost point M straight right to the nearest outline edge, then to
    // the outline vertex there it can see, and round the hole and back along the same bridge
    private static void Bridge(List<int> polygon, List<Vector2> points, int start, int count)
    {
        int m = start;

        for (int k = 1; k < count; k++)
        {
            Vector2 p = points[start + k];

            if (p.x > points[m].x || (p.x == points[m].x && p.y < points[m].y))
                m = start + k;
        }

        Vector2 mPoint = points[m];
        int n = polygon.Count;
        float nearest = float.MaxValue;
        int edge = -1;

        // The nearest edge a ray from M going +x crosses
        for (int i = 0; i < n; i++)
        {
            Vector2 a = points[polygon[i]];
            Vector2 b = points[polygon[(i + 1) % n]];

            if (a.y == b.y || Mathf.Min(a.y, b.y) > mPoint.y || Mathf.Max(a.y, b.y) < mPoint.y)
                continue;

            float x = a.x + (mPoint.y - a.y) / (b.y - a.y) * (b.x - a.x);

            if (x >= mPoint.x && x < nearest)
            {
                nearest = x;
                edge = i;
            }
        }

        if (edge < 0)
        {
            Debug.LogWarning("Level_Triangulator: a hole couldn't be joined to the outline (is it outside it?); left out.");
            return;
        }

        // Its end furthest right is the first guess for what M can see
        Vector2 hit = new Vector2(nearest, mPoint.y);
        int aPos = edge;
        int bPos = (edge + 1) % n;
        int pPos = points[polygon[aPos]].x > points[polygon[bPos]].x ? aPos : bPos;
        Vector2 pPoint = points[polygon[pPos]];

        // Unless the outline dents into the triangle M-hit-P: then the dent vertex at the smallest angle from the ray
        float bestCos = -2f;
        float bestDistance = float.MaxValue;
        int found = -1;

        for (int i = 0; i < n; i++)
        {
            if (i == pPos || !IsReflex(polygon, points, i))
                continue;

            Vector2 v = points[polygon[i]];

            if (!InsideOrOn(v, mPoint, hit, pPoint) || v == mPoint)
                continue;

            Vector2 to = v - mPoint;
            float distance = to.magnitude;
            float cos = distance > 0f ? to.x / distance : -1f;

            if (cos > bestCos + Epsilon || (Mathf.Abs(cos - bestCos) <= Epsilon && distance < bestDistance))
            {
                bestCos = cos;
                bestDistance = distance;
                found = i;
            }
        }

        if (found >= 0)
            pPos = found;

        // An earlier bridge may already end at this point, so it's in the polygon more than once. Join at the copy whose
        // corner opens toward M, or the bridges cross.
        pPoint = points[polygon[pPos]];

        for (int i = 0; i < n; i++)
        {
            if (points[polygon[i]] == pPoint && OpensToward(polygon, points, i, mPoint))
            {
                pPos = i;
                break;
            }
        }

        // P, M, round the hole, M, P: the bridge is walked both ways
        List<int> insert = new List<int>(count + 2);

        for (int k = 0; k <= count; k++)
            insert.Add(start + (m - start + k) % count);

        insert.Add(polygon[pPos]);
        polygon.InsertRange(pPos + 1, insert);
    }

    private static List<int> EarClip(List<int> polygon, List<Vector2> points)
    {
        List<int> remaining = new List<int>(polygon);
        List<int> triangles = new List<int>((polygon.Count - 2) * 3);
        int failures = 0;
        int i = 0;

        while (remaining.Count > 3)
        {
            int n = remaining.Count;
            i %= n;

            int prev = remaining[(i - 1 + n) % n];
            int current = remaining[i];
            int next = remaining[(i + 1) % n];

            if (IsEar(remaining, points, prev, current, next))
            {
                triangles.Add(prev);
                triangles.Add(current);
                triangles.Add(next);
                remaining.RemoveAt(i);
                failures = 0;
                continue;
            }

            i++;

            // A full lap with no ear: drop a flat (collinear or repeated) corner and carry on, or give up
            if (++failures > n)
            {
                if (!DropFlat(remaining, points))
                {
                    Debug.LogWarning("Level_Triangulator: couldn't finish (overlapping holes?); some triangles are missing.");
                    break;
                }

                failures = 0;
            }
        }

        if (remaining.Count == 3 && Cross(points[remaining[0]], points[remaining[1]], points[remaining[2]]) > Epsilon)
            triangles.AddRange(remaining);

        return triangles;
    }

    private static bool IsEar(List<int> remaining, List<Vector2> points, int prev, int current, int next)
    {
        Vector2 a = points[prev];
        Vector2 b = points[current];
        Vector2 c = points[next];

        // Has to turn left (anticlockwise outline) and enclose some area
        if (Cross(a, b, c) <= Epsilon)
            return false;

        for (int k = 0; k < remaining.Count; k++)
        {
            int index = remaining[k];

            if (index == prev || index == current || index == next)
                continue;

            Vector2 v = points[index];

            // Bridge ends repeat points; those don't count as inside
            if (v == a || v == b || v == c)
                continue;

            if (StrictlyInside(v, a, b, c))
                return false;
        }

        return true;
    }

    private static bool DropFlat(List<int> remaining, List<Vector2> points)
    {
        int n = remaining.Count;

        for (int i = 0; i < n; i++)
        {
            Vector2 a = points[remaining[(i - 1 + n) % n]];
            Vector2 b = points[remaining[i]];
            Vector2 c = points[remaining[(i + 1) % n]];

            if (Mathf.Abs(Cross(a, b, c)) <= Epsilon)
            {
                remaining.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    // Whether the polygon's inside, at this corner, faces the target: the direction to it falls between the edge out
    // (to the next point) and, turning anticlockwise, the edge back (to the previous one)
    private static bool OpensToward(List<int> polygon, List<Vector2> points, int position, Vector2 target)
    {
        int n = polygon.Count;
        Vector2 corner = points[polygon[position]];
        Vector2 toNext = points[polygon[(position + 1) % n]] - corner;
        Vector2 toPrevious = points[polygon[(position - 1 + n) % n]] - corner;
        Vector2 toTarget = target - corner;

        return TurnAnticlockwise(toNext, toTarget) < TurnAnticlockwise(toNext, toPrevious);
    }

    // Angle from one direction to another going anticlockwise, 0 to 2π
    private static float TurnAnticlockwise(Vector2 from, Vector2 to)
    {
        float angle = Mathf.Atan2(from.x * to.y - from.y * to.x, from.x * to.x + from.y * to.y);
        return angle < 0f ? angle + 2f * Mathf.PI : angle;
    }

    private static bool IsReflex(List<int> polygon, List<Vector2> points, int position)
    {
        int n = polygon.Count;
        return Cross(points[polygon[(position - 1 + n) % n]], points[polygon[position]], points[polygon[(position + 1) % n]]) <= 0f;
    }

    // Twice the signed area of a, b, c: positive when they turn anticlockwise
    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    private static bool StrictlyInside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        return Cross(a, b, p) > Epsilon && Cross(b, c, p) > Epsilon && Cross(c, a, p) > Epsilon;
    }

    // Either winding, edges included
    private static bool InsideOrOn(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(a, b, p);
        float d2 = Cross(b, c, p);
        float d3 = Cross(c, a, p);
        bool negative = d1 < -Epsilon || d2 < -Epsilon || d3 < -Epsilon;
        bool positive = d1 > Epsilon || d2 > Epsilon || d3 > Epsilon;
        return !(negative && positive);
    }
}
