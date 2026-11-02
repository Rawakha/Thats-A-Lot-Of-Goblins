using UnityEngine;

// Shared shapes for gizmos (tuning radii, cones and ranges in the Scene view). Flat shapes lie on the XZ plane.
// Call from OnDrawGizmos / OnDrawGizmosSelected; set Gizmos.color first.
public static class Debug_Gizmos
{
    public static void Circle(Vector3 centre, float radius, int segments = 40)
    {
        if (radius <= 0f)
            return;

        Vector3 previous = centre + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 next = centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    // A flat wedge from centre, round a heading (degrees, 0 = +z), halfAngle either side, out to radius
    public static void Wedge(Vector3 centre, float heading, float halfAngle, float radius, int segments = 24)
    {
        if (radius <= 0f)
            return;

        Vector3 left = Direction(heading - halfAngle) * radius;
        Vector3 right = Direction(heading + halfAngle) * radius;
        Gizmos.DrawLine(centre, centre + left);
        Gizmos.DrawLine(centre, centre + right);

        Vector3 previous = centre + left;

        for (int i = 1; i <= segments; i++)
        {
            Vector3 next = centre + Direction(heading - halfAngle + 2f * halfAngle * i / segments) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    // Flat unit direction for a heading in degrees (0 = +z, 90 = +x)
    public static Vector3 Direction(float heading)
    {
        float radians = heading * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
    }
}
