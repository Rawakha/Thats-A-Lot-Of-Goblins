using UnityEngine;

// Where the plane enters an area. It always flies in north (+Z): it starts Fly In Distance south of this point, off the
// bottom of the screen, with the camera already framing this point, and the player gets control back as it passes here.
//
// Setup: the Area_Root checklist's Create button makes this. Move it and set the distance with the Scene view handles.
// If the plane is visible when the fade-in starts, raise Fly In Distance.
//
// GC: none. No subscriptions.
public class Area_Spawn : MonoBehaviour
{
    [Tooltip("How far south of this point the plane starts, metres. Far enough to be off the bottom of the screen.")]
    [Min(0f)] [SerializeField] private float flyInDistance = 45f;

    public Vector3 Point => transform.position;
    public Vector3 Start => transform.position - Vector3.forward * flyInDistance;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
        Gizmos.DrawLine(Start, Point);
        Debug_Gizmos.Circle(Point, 2f);
        Debug_Gizmos.Label(Point, "Control returns", Gizmos.color);
        Debug_Gizmos.Label(Start, "Plane starts", Gizmos.color);
    }
#endif
}
