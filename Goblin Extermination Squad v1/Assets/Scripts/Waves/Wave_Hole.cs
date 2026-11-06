using System;
using UnityEngine;

// A goblin hole. Put it at the centre of the hole's mouth, at ground level, with the hole's model (a pit, lower than
// the ground) as a child or alongside. It's both:
//   a source: Wave_Director launches goblins up out of its bottom (Depth below the mouth), tumbling, and
//   a target: goblins sent to it walk its flow field (the Flow_Goal on it) and, once inside Mouth Radius, drop in and
//     are disabled at the bottom.
// Only goblins sent to this hole fall in; others walk past it (or out of it, when it's their source).
//
// Setup: the trigger collider is made for you (a sphere of Mouth Radius); its layer must touch the goblins' layer in
// the Physics matrix. Untick Follow on the Flow_Goal (holes don't move). Keep the hole's walls off the flow field's
// Obstacle Layers, or goblins can't reach the mouth.
//
// GC: none. No subscriptions: Unity calls OnTriggerStay during physics, only while something is in the mouth.
[RequireComponent(typeof(Flow_Goal), typeof(SphereCollider))]
public class Wave_Hole : MonoBehaviour
{
    [Tooltip("Goblins sent here drop in once they're this close to the centre, metres.")]
    [SerializeField, Min(0.1f)] private float mouthRadius = 1.2f;

    [Tooltip("How far below the mouth the bottom is, metres: where goblins are launched from and disappear at.")]
    [SerializeField, Min(0.1f)] private float depth = 2.5f;

    [Tooltip("Goblins that reach the mouth jump in to a random spot within this distance of the centre, metres, so " +
             "they don't all land on the middle. 0 is the centre every time.")]
    [SerializeField, Min(0f)] private float dropSpread = 0.6f;

    [Tooltip("Goblins come out anywhere in this circle at the bottom, metres.")]
    [SerializeField, Min(0f)] private float spawnSpread = 0.4f;

    [Header("Stats")]
    [SerializeField, ReadOnly] private int sent;
    [SerializeField, ReadOnly] private int reached;

    private Flow_Goal goal;
    private SphereCollider mouth;

    // A goblin sent here got here, just before it drops in
    public event Action<Wave_Hole, Horde_Goblin> Reached;

    public Flow_FieldHandle Field => goal != null ? goal.Field : Flow_FieldHandle.Invalid;
    public Vector3 Centre => transform.position;
    public Vector3 Bottom => transform.position + Vector3.down * depth;
    public float MouthRadius => mouthRadius;

    // Somewhere in the mouth for a goblin to jump in to
    public Vector3 DropPoint()
    {
        Vector2 offset = UnityEngine.Random.insideUnitCircle * dropSpread;
        return Centre + new Vector3(offset.x, 0f, offset.y);
    }

    // Somewhere on the bottom to launch a goblin from
    public Vector3 SpawnPoint()
    {
        Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnSpread;
        return Bottom + new Vector3(offset.x, 0f, offset.y);
    }

    internal void CountSent() => sent++;

    private void Reset() => SetUpMouth();

    private void OnValidate() => SetUpMouth();

    private void Awake()
    {
        goal = GetComponent<Flow_Goal>();
        SetUpMouth();
    }

    private void SetUpMouth()
    {
        if (mouth == null)
            mouth = GetComponent<SphereCollider>();

        if (mouth == null)
            return;

        mouth.isTrigger = true;
        mouth.center = Vector3.zero;
        mouth.radius = mouthRadius;
    }

    // Stay rather than Enter: a goblin that came into the mouth still in the air drops in once it has landed
    private void OnTriggerStay(Collider other)
    {
        Horde_Goblin goblin = other.attachedRigidbody != null
            ? other.attachedRigidbody.GetComponent<Horde_Goblin>()
            : other.GetComponentInParent<Horde_Goblin>();

        if (goblin == null || goblin.Destination != this || !goblin.IsActive || goblin.Dead || goblin.FallingIn
            || goblin.Airborne || Horde_System.Instance == null)
            return;

        reached++;
        Reached?.Invoke(this, goblin);

        // Down to below the bottom before it's put away, so it's out of sight
        Horde_System.Instance.FallInto(goblin, DropPoint(), Bottom.y - 0.5f);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 centre = transform.position;
        Vector3 bottom = Bottom;

        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
        DrawCircle(centre, mouthRadius);

        // Where goblins jump in to
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.9f);
        DrawCircle(centre, dropSpread);

        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.4f);
        DrawCircle(bottom, Mathf.Max(spawnSpread, 0.1f));
        Gizmos.DrawLine(centre + Vector3.right * mouthRadius, bottom + Vector3.right * spawnSpread);
        Gizmos.DrawLine(centre - Vector3.right * mouthRadius, bottom - Vector3.right * spawnSpread);
        Gizmos.DrawLine(centre + Vector3.forward * mouthRadius, bottom + Vector3.forward * spawnSpread);
        Gizmos.DrawLine(centre - Vector3.forward * mouthRadius, bottom - Vector3.forward * spawnSpread);
    }

    private static void DrawCircle(Vector3 centre, float radius)
    {
        const int Segments = 32;
        Vector3 previous = centre + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            Vector3 next = centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
#endif
}
