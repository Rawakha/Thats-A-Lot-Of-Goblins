using UnityEngine;

// One goblin's GameObject: just the root, with its Rigidbody and collider. Put it on the goblin prefab to pick Body
// and Head by hand (optional: children called Body and Head are found otherwise); Horde_System adds it if missing.
// When the pool makes a goblin, its mesh GameObjects are removed: the parts are drawn instanced from data by its
// Horde_RenderGroup. Per-frame data (field, speed, facing, part poses) lives in Horde_System's arrays.
//
// GC: none. No subscriptions.
[DisallowMultipleComponent]
public class Horde_Goblin : Core_RegistrableBehaviour, Combat_IDamageable
{
    [Tooltip("The body in the prefab. Empty uses the child called Body.")]
    [SerializeField] private Transform body;

    [Tooltip("The head in the prefab. Empty uses the child called Head.")]
    [SerializeField] private Transform head;

    public Horde_GoblinType Type { get; private set; }
    public Rigidbody Rigidbody { get; private set; }

    // Active means walking in the world, not sitting in the pool
    public bool IsActive => IsRegistered && !InPool;

    // Set the moment it's despawned, even if its registry removal waits for the movement step to finish
    internal bool InPool { get; set; } = true;

    // Its slot in its type's render group while active
    internal int RenderIndex { get; set; } = -1;

    // Thrown (Horde_System.Launch): gravity on, not walking, until it's back down at the ground
    public bool Airborne { get; internal set; }

    // Thrown with real physics: free rotation, on the airborne layer, until it comes to rest and gets up
    public bool Physical { get; internal set; }
    internal float AirTime;
    internal float RestTime;

    // Killed in the air and since come to rest: lying still, waiting to shrink
    internal bool Settled;
    internal int WalkingLayer;
    internal float WalkingDamping;

    // Killed and shrinking away; it goes back to the pool when the shrink ends. Takes no more hits.
    public bool Dead { get; internal set; }

    // Hopping out of a hole (Horde_System.SpawnEmerging): its body waits at the landing spot with no collisions while
    // the animation draws the hop; it walks from EmergeEnd. Takes no hits until then.
    public bool Emerging { get; internal set; }
    internal float EmergeEnd;

    // Dropping into a hole (Horde_System.FallInto): falls freely over the hole's centre and goes back to the pool once
    // it's below the hole's bottom. Takes no more hits.
    public bool FallingIn { get; internal set; }
    internal Vector3 FallCentre;
    internal float FallBottom;

    // The scream while flung (an AudioManager tracked sound), or 0
    internal int ScreamHandle;

    // What it's heading for, set by whoever sent it (a Wave_Hole), so they can tell their goblins apart. Cleared when
    // it goes back to the pool.
    public Component Destination { get; set; }

    // Bullets find this on the goblin's collider
    public void TakeDamage(in Combat_Hit hit)
    {
        if (Horde_System.Instance != null)
            Horde_System.Instance.Damage(this, hit.Damage, hit.Velocity, hit.Point);
    }

    // The authored Body and Head, read from the prefab when its parts are baked
    internal static void FindParts(GameObject prefab, out Transform body, out Transform head)
    {
        Horde_Goblin authored = prefab.GetComponent<Horde_Goblin>();
        body = authored != null && authored.body != null ? authored.body : prefab.transform.Find("Body");
        head = authored != null && authored.head != null ? authored.head : prefab.transform.Find("Head");
    }

    // Once, when the pool makes it: makes sure there's physics, then removes the meshes (they're drawn from data)
    internal void Create(Horde_GoblinType type)
    {
        Type = type;
        WalkingLayer = gameObject.layer;
        Rigidbody = GetComponent<Rigidbody>();

        if (Rigidbody == null)
        {
            Rigidbody = gameObject.AddComponent<Rigidbody>();
            Rigidbody.mass = type.mass;
        }

        WalkingDamping = Rigidbody.linearDamping;

        // Goblins stay upright; Horde_System turns them
        Rigidbody.constraints |= RigidbodyConstraints.FreezeRotation;
        Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        // Pushing apart needs far fewer solver passes than the project default
        Rigidbody.solverIterations = type.solverIterations;
        Rigidbody.solverVelocityIterations = type.solverVelocityIterations;

        if (GetComponentInChildren<Collider>(true) == null)
        {
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = type.radius;
            capsule.height = Mathf.Max(type.height, type.radius * 2f);
            capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
        }

        StripMeshes();
    }

    // Children with no colliders go entirely; ones with colliders lose only their renderers
    private void StripMeshes()
    {
        RemoveRenderers(gameObject);

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;

            if (child.GetComponentInChildren<Collider>(true) == null)
            {
                Destroy(child);
                continue;
            }

            foreach (MeshRenderer meshRenderer in child.GetComponentsInChildren<MeshRenderer>(true))
                RemoveRenderers(meshRenderer.gameObject);
        }

        body = null;
        head = null;
    }

    private static void RemoveRenderers(GameObject target)
    {
        if (target.TryGetComponent(out MeshRenderer meshRenderer))
            Destroy(meshRenderer);

        if (target.TryGetComponent(out MeshFilter meshFilter))
            Destroy(meshFilter);
    }
}
