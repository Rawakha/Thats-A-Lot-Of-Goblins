using UnityEngine;

// A round hole in the ground. Put it anywhere over a Level_Ground: the ground cuts an opening of this Radius at its
// position and builds the hole's inside wall down to Depth, and a bottom, as part of its own mesh (and collider), so
// things really can fall in. Move it, resize it or switch it off in the editor and the ground rebuilds at once.
// The mouth is always at the ground's height; only this object's x and z count.
//
// A level tool: it knows nothing about goblins. Gameplay that uses holes (Wave_Hole) can sit on the same object.
//
// GC: none at runtime. No Update and no subscriptions: edit-time changes are picked up by Level_Ground's editor hook.
[DisallowMultipleComponent]
public class Level_Hole : MonoBehaviour
{
    [Tooltip("Scene handle grid in metres for movement, radius and depth. Zero allows free dragging.")]
    [SerializeField, Min(0f)] private float snap = 0.25f;

    [Tooltip("Radius of the opening, metres.")]
    [SerializeField, Min(0.1f)] private float radius = 1.5f;

    [Tooltip("How far down the hole goes from the ground, metres.")]
    [SerializeField, Min(0.05f)] private float depth = 3f;

    [Tooltip("Sides around the circle. More is rounder.")]
    [SerializeField, Range(6, 96)] private int segments = 32;

    [Tooltip("Close the hole with a floor at the bottom. Off leaves it open (bottomless, for a deep drop).")]
    [SerializeField] private bool bottom = true;

    public float Radius => radius;
    public float Snap => snap;
    public float Depth => depth;
    public int Segments => segments;
    public bool HasBottom => bottom;

#if UNITY_EDITOR
    private void OnValidate() => Level_Ground.RequestRebuild();
    private void OnEnable() => Level_Ground.RequestRebuild();
    private void OnDisable() => Level_Ground.RequestRebuild();

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        Vector3 centre = transform.position;
        DrawCircle(centre, radius);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
        DrawCircle(centre + Vector3.down * depth, radius);
        Gizmos.DrawLine(centre, centre + Vector3.down * depth);
    }

    private static void DrawCircle(Vector3 centre, float circleRadius)
    {
        const int Steps = 32;
        Vector3 previous = centre + new Vector3(circleRadius, 0f, 0f);

        for (int i = 1; i <= Steps; i++)
        {
            float angle = i * Mathf.PI * 2f / Steps;
            Vector3 next = centre + new Vector3(Mathf.Cos(angle) * circleRadius, 0f, Mathf.Sin(angle) * circleRadius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
#endif
}
