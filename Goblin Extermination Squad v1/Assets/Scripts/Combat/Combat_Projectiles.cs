using System.Collections.Generic;
using UnityEngine;

public struct Combat_Projectile
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Life;
    public float Damage;
    public float Radius;
    public int Type;
    public int Source;
}

// Every projectile in flight, for every weapon. Weapons only call Spawn; this moves them, finds hits and deals
// damage. Each frame every projectile steps forward and raycasts (sphere-casts, for fat shots) the stretch it just
// covered. Projectiles that reach FloorHeight hit the ground even where there's no collider. Each projectile is a
// pooled copy of its Combat_ProjectileType prefab, moved along with it; impacts play the type's Effect_Hit prefabs.
//
// Hits: a Combat_IDamageable (on the collider, its Rigidbody or a parent) takes the damage directly. Anything else
// (ground, scenery) passes the impact to Combat_Damage as area damage, which is how the horde will be hit. Exploding
// types (bombs) instead set off one blast, Combat_Damage.ApplyExplosion, whatever they land on.
//
// Plain main-thread code: a few hundred bullets is cheap. Move it to jobs only if the profiler says so.
//
// GC: none per frame. Arrays are allocated once at capacity; shot visuals and impacts come from Effect_Pool.
// One UpdateManager subscription (OnLateUpdate).
[DefaultExecutionOrder(Utilities.ExecutionOrder.DependantSingletons)]
[RequireComponent(typeof(Effect_Pool))]
public class Combat_Projectiles : MonoBehaviour
{
    [Tooltip("Most projectiles in flight at once. Spawns past this are dropped.")]
    [SerializeField, Min(1)] private int capacity = 1024;

    [Tooltip("Height of the flat ground. Projectiles that reach it hit, with or without a collider.")]
    [SerializeField] private float floorHeight;

    [Tooltip("Most impact sounds started per frame, across all projectiles.")]
    [SerializeField, Min(0)] private int maxImpactSoundsPerFrame = 2;

    [Header("Gizmos")]
    [Tooltip("Draw each blast (kill radius red, fling radius orange) where it went off, for a moment, in the Scene view.")]
    [SerializeField] private bool drawBlasts = true;
    [SerializeField, Min(0.1f)] private float blastGizmoTime = 2f;

    // Projectiles and their visuals, in matching order. Removal swaps the last one into the gap.
    private Combat_Projectile[] projectiles;
    private Effect_Instance[] visuals;
    private int count;

    private readonly List<Combat_ProjectileType> types = new List<Combat_ProjectileType>(16);
    private readonly List<Combat_Hit> frameHits = new List<Combat_Hit>(256);

    private Effect_Pool effects;
    private bool warnedFull;

    // The last few blasts, for the gizmos
    private struct Blast
    {
        public Vector3 Point;
        public float Kill;
        public float Fling;
        public float Time;
    }

    private readonly Blast[] blasts = new Blast[16];
    private int nextBlast;
    private int impactSoundsThisFrame;

    public static Combat_Projectiles Instance { get; private set; }

    public float FloorHeight => floorHeight;
    public int Count => count;

    // This frame's impacts, for upgrade hooks (ricochet, fire on hit). Valid until the next LateUpdate.
    public IReadOnlyList<Combat_Hit> FrameHits => frameHits;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"{name}: A second Combat_Projectiles was found, this one is disabled.", this);
            enabled = false;
            return;
        }

        Instance = this;
        effects = GetComponent<Effect_Pool>();
        projectiles = new Combat_Projectile[capacity];
        visuals = new Effect_Instance[capacity];
    }

    private void OnEnable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate += Tick;
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= Tick;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Weapons register their projectile prefab once (on equip) and spawn with the index
    public int RegisterType(Combat_ProjectileType type)
    {
        int index = types.IndexOf(type);

        if (index >= 0)
            return index;

        types.Add(type);
        return types.Count - 1;
    }

    public bool Spawn(int type, Vector3 position, Vector3 velocity, float lifetime, float damage, float radius, int source)
    {
        if (count >= capacity)
        {
            if (!warnedFull)
            {
                Debug.LogWarning($"{name}: Projectile capacity ({capacity}) reached, extra shots are dropped.", this);
                warnedFull = true;
            }

            return false;
        }

        projectiles[count] = new Combat_Projectile
        {
            Position = position,
            Velocity = velocity,
            Life = lifetime,
            Damage = damage,
            Radius = radius,
            Type = type,
            Source = source
        };

        visuals[count] = effects.Spawn(types[type].gameObject, position, Core_Math.LookRotation(velocity, Vector3.up));
        count++;
        return true;
    }

    // Backwards, so a swap-back removal only ever moves an already-handled projectile into this index
    private void Tick()
    {
        frameHits.Clear();
        impactSoundsThisFrame = 0;

        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        for (int i = count - 1; i >= 0; i--)
        {
            ref Combat_Projectile projectile = ref projectiles[i];
            Combat_ProjectileType type = types[projectile.Type];

            projectile.Velocity.y -= type.gravity * deltaTime;

            Vector3 from = projectile.Position;
            Vector3 delta = projectile.Velocity * deltaTime;
            float distance = delta.magnitude;

            projectile.Position = from + delta;
            projectile.Life -= deltaTime;

            // How far along this step the floor is, if it's reached
            float floorDistance = -1f;

            float surfaceHeight = Level_GroundSurface.HeightAt(projectile.Position, floorHeight);
            if (projectile.Position.y <= surfaceHeight)
                floorDistance = delta.y < 0f ? distance * Mathf.Clamp01((from.y - surfaceHeight) / -delta.y) : 0f;

            // A fat shot sweeps a sphere, so near misses still connect
            RaycastHit hit = default;
            bool struck = distance > 0f && (type.hitRadius > 0f
                ? Physics.SphereCast(from, type.hitRadius, delta / distance, out hit, distance, type.hitLayers, QueryTriggerInteraction.Ignore)
                : Physics.Raycast(from, delta / distance, out hit, distance, type.hitLayers, QueryTriggerInteraction.Ignore));

            if (struck && (floorDistance < 0f || hit.distance <= floorDistance))
            {
                Impact(i, hit.point, hit.normal, hit.collider);
            }
            else if (floorDistance >= 0f)
            {
                Vector3 floorPoint = distance > 0f ? from + delta * (floorDistance / distance) : from;
                Impact(i, floorPoint, Vector3.up, null);
            }
            else if (projectile.Life <= 0f)
            {
                Remove(i, 0f);
            }
            else
            {
                visuals[i].Transform.SetPositionAndRotation(projectile.Position, Core_Math.LookRotation(projectile.Velocity, Vector3.up));
            }
        }
    }

    private void Impact(int index, Vector3 point, Vector3 normal, Collider collider)
    {
        Combat_Projectile projectile = projectiles[index];
        Combat_ProjectileType type = types[projectile.Type];

        Combat_Hit hit = new Combat_Hit
        {
            Point = point,
            Normal = normal,
            Velocity = projectile.Velocity,
            Damage = projectile.Damage,
            Radius = projectile.Radius,
            Source = projectile.Source,
            ProjectileType = projectile.Type
        };

        Combat_IDamageable target = null;

        if (collider != null && !collider.TryGetComponent(out target) && collider.attachedRigidbody != null)
            collider.attachedRigidbody.TryGetComponent(out target);

        // Parts of something bigger (a building's shards) answer through their owner
        if (target == null && collider != null)
            target = collider.GetComponentInParent<Combat_IDamageable>();

        if (type.explodes)
        {
            // A bomb: one blast, whatever it landed on
            Combat_Damage.ApplyExplosion(new Combat_Explosion
            {
                Point = point,
                Velocity = projectile.Velocity,
                KillRadius = projectile.Radius,
                FlingRadius = projectile.Radius * type.flingRadiusScale,
                Damage = projectile.Damage,
                FlingOut = type.flingOut,
                FlingUp = type.flingUp,
                InstantDeathInner = type.instantDeathInner,
                InstantDeathOuter = type.instantDeathOuter,
                Source = projectile.Source
            });

            Camera_Rig.Shake(type.explosionTrauma);

            blasts[nextBlast] = new Blast
            {
                Point = point,
                Kill = projectile.Radius,
                Fling = projectile.Radius * type.flingRadiusScale,
                Time = Time.time
            };
            nextBlast = (nextBlast + 1) % blasts.Length;
        }
        else if (target != null)
        {
            hit.HitDamageable = true;
            target.TakeDamage(hit);
        }
        else
        {
            Combat_Damage.ApplyArea(hit);
        }

        frameHits.Add(hit);

        Effect_Hit effect = target != null && type.damageableImpact != null ? type.damageableImpact : type.surfaceImpact;

        if (effect != null)
            effects.Play(effect.gameObject, point, effect.Rotation(normal), effect.lifetime, type.impactScale);

        SoundConfig impactSound = SoundData.Get(type.impactSound);

        if (impactSound != null && impactSoundsThisFrame < maxImpactSoundsPerFrame && AudioManager.Instance != null)
        {
            AudioManager.Instance.RequestSound(impactSound, point);
            impactSoundsThisFrame++;
        }

        // Leave the shot at the impact so its trail ends there, then let it fade
        visuals[index].Transform.position = point;
        Remove(index, type.linger);
    }

    private void Remove(int index, float linger)
    {
        Effect_Instance visual = visuals[index];
        int last = count - 1;

        projectiles[index] = projectiles[last];
        visuals[index] = visuals[last];
        visuals[last] = null;
        count--;

        effects.ReleaseAfter(visual, linger, true);
    }

    private void OnDrawGizmos()
    {
        if (!drawBlasts || !Application.isPlaying)
            return;

        float now = Time.time;

        for (int i = 0; i < blasts.Length; i++)
        {
            Blast blast = blasts[i];
            float age = now - blast.Time;

            if (blast.Fling <= 0f || age > blastGizmoTime)
                continue;

            float fade = 1f - age / blastGizmoTime;
            Gizmos.color = new Color(1f, 0.25f, 0.2f, fade);
            Debug_Gizmos.Circle(blast.Point, blast.Kill);
            Gizmos.color = new Color(1f, 0.65f, 0.15f, fade);
            Debug_Gizmos.Circle(blast.Point, blast.Fling);
        }
    }
}
