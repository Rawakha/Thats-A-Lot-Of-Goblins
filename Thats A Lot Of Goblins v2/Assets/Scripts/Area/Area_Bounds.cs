using UnityEngine;

// The edges of an area: two rectangles on the ground, centred on this object. Past the yellow Warning line the player is
// told to turn back; at the red Respawn line, Margin further out, Run_Edge takes over and brings the plane back in.
//
// Setup: the Area_Root checklist's Create button makes this. Move and size it with the Scene view handles while it's
// selected, then assign nothing else.
//
// GC: none. No subscriptions.
public class Area_Bounds : MonoBehaviour
{
    [Tooltip("Width (X) and length (Z) of the warning rectangle, metres.")]
    [SerializeField] private Vector2 warningSize = new Vector2(200f, 200f);

    [Tooltip("How far past the warning line the respawn line is, metres, on every side.")]
    [Min(0.1f)] [SerializeField] private float margin = 25f;

    // How far past the warning line a point is, metres: 0 or less is inside
    private float Past(Vector3 point)
    {
        Vector3 centre = transform.position;
        return Mathf.Max(Mathf.Abs(point.x - centre.x) - warningSize.x * 0.5f, Mathf.Abs(point.z - centre.z) - warningSize.y * 0.5f);
    }

    // 0 inside the warning line, up to 1 at the respawn line
    public float WarningAmount(Vector3 point) => Mathf.Clamp01(Past(point) / margin);

    public bool PastRespawn(Vector3 point) => Past(point) >= margin;

    public bool InsideWarning(Vector3 point) => Past(point) < 0f;

    public Vector2 WarningSize => warningSize;

    // Metres from a point to the warning line: negative once past it
    public float DistanceToWarning(Vector3 point) => -Past(point);

    // The flat direction straight out through the side this point is closest to crossing
    public Vector3 Outward(Vector3 point)
    {
        Vector3 centre = transform.position;
        float x = point.x - centre.x;
        float z = point.z - centre.z;

        if (Mathf.Abs(x) - warningSize.x * 0.5f > Mathf.Abs(z) - warningSize.y * 0.5f)
            return x >= 0f ? Vector3.right : Vector3.left;

        return z >= 0f ? Vector3.forward : Vector3.back;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 centre = transform.position;
        Vector3 warning = new Vector3(warningSize.x, 0f, warningSize.y);
        Vector3 respawn = warning + new Vector3(margin * 2f, 0f, margin * 2f);

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(centre, warning);
        Debug_Gizmos.Label(centre + new Vector3(0f, 0f, warningSize.y * 0.5f), "Warning", Gizmos.color);

        Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(centre, respawn);
        Debug_Gizmos.Label(centre + new Vector3(0f, 0f, respawn.z * 0.5f), "Respawn", Gizmos.color);
    }
#endif
}
