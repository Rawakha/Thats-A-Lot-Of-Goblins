using System;
using UnityEngine;

// A stick of bombs. Put this on the root of an ability prefab (Ability slot, right mouse), with the bomb bay's
// Weapon_Muzzle inside. One press drops Projectile Count bombs (3), Drop Interval apart, so they land in a line along
// the flight path: a carpet. Each bomb is a projectile with gravity that explodes where it lands (its Combat_
// ProjectileType has Explodes on): the middle of each blast (the Radius stat) blows goblins apart, the ring out to
// Radius x the bomb's Fling Radius Scale hurts and flings them. Then the bay waits Cooldown seconds.
//
// The bomb prefab is like a bullet's: a Combat_ProjectileType on the root, visuals as children pointing +Z, no
// Rigidbody or colliders. Give it Gravity (~25), Explodes, and a big Surface Impact (the explosion effect).
//
// On the ability belt: each stick costs a charge. Swapping away mid-stick doesn't stop it; the rest of the stick
// still drops (Busy until it has).
//
// GC: none per drop; each bomb is pooled by Combat_Projectiles. No subscription: ticked by Equip_Loadout.
public class Weapon_CarpetBomb : Equip_Ability
{
    [Header("Carpet Bomb")]
    [SerializeField] private Combat_ProjectileType bomb;

    [Tooltip("Where bombs leave from. Empty drops them from just under the plane.")]
    [SerializeField] private Weapon_Muzzle bombBay;

    [Tooltip("Seconds between bombs in one stick. Longer spreads them further apart along the ground.")]
    [SerializeField, Min(0.01f)] private float dropInterval = 0.15f;

    [Tooltip("How much of the plane's velocity bombs keep, so they carry on forward as they fall.")]
    [SerializeField, Range(0f, 1f)] private float inheritVelocity = 0.9f;

    [Tooltip("Push straight down as each bomb leaves, m/s.")]
    [SerializeField, Min(0f)] private float dropSpeed = 4f;

    [Tooltip("Random sideways scatter per bomb, m/s, so a stick isn't a perfect line.")]
    [SerializeField, Min(0f)] private float scatter = 1f;

    [Header("Feel")]
    [Tooltip("Camera trauma per bomb dropped (the blasts shake on their own).")]
    [SerializeField, Range(0f, 1f)] private float dropTrauma = 0.05f;

    [Tooltip("Nose-up kick on the plane per bomb, degrees/second, as it gets lighter.")]
    [SerializeField, Min(0f)] private float dropKick = 8f;

    [SerializeField] private SoundData dropSound;

    [Header("Gizmos")]
    [Tooltip("Draw where the stick would land right now: each bomb's kill radius (red) and fling radius (orange). " +
             "Shows while this or the plane is selected; Always shows it all the time.")]
    [SerializeField] private bool drawLanding = true;
    [SerializeField] private bool drawAlways;

    [Tooltip("Speed the preview pretends the plane flies at outside play mode, m/s, along this object's forward.")]
    [SerializeField, Min(0f)] private float previewSpeed = 32f;

    private Combat_Projectiles projectiles;
    private int bombType = -1;
    private float cooldown;
    private int toDrop;
    private float nextDrop;

    // 0 ready, up to 1 just used: for the UI
    public float CooldownFraction => Stats[Equip_Stat.Cooldown] > 0f ? Mathf.Clamp01(cooldown / Stats[Equip_Stat.Cooldown]) : 0f;
    public bool Ready => cooldown <= 0f && toDrop == 0;

    public override float CooldownProgress => toDrop > 0 ? 0f : 1f - CooldownFraction;
    public override bool Busy => toDrop > 0;

    // Each bomb as it leaves
    public event Action Dropped;

    protected override void Reset()
    {
        base.Reset();
        tags = Equip_Tags.Explosive | Equip_Tags.Projectile | Equip_Tags.Ground;
        baseStats = new[]
        {
            new Equip_StatValue(Equip_Stat.Damage, 2f),
            new Equip_StatValue(Equip_Stat.ProjectileCount, 3f),
            new Equip_StatValue(Equip_Stat.Radius, 3f),
            new Equip_StatValue(Equip_Stat.Cooldown, 4f),
            new Equip_StatValue(Equip_Stat.Range, 200f)
        };
    }

    protected override void OnEquip()
    {
        projectiles = Combat_Projectiles.Instance;

        if (projectiles == null)
        {
            Debug.LogError($"{DisplayName}: No Combat_Projectiles in the scene, the bombs can't drop.", this);
            return;
        }

        if (bomb == null)
        {
            Debug.LogError($"{DisplayName}: No bomb projectile assigned.", this);
            return;
        }

        if (!bomb.explodes)
            Debug.LogWarning($"{DisplayName}: the bomb projectile doesn't have Explodes on, so it'll just hit like a bullet.", bomb);

        bombType = projectiles.RegisterType(bomb);
    }

    public override void Tick(in Equip_Context context)
    {
        if (bombType < 0)
            return;

        float deltaTime = context.DeltaTime;

        // A stick in progress: the next bomb when its moment comes (more than one if a frame ran long)
        if (toDrop > 0)
        {
            nextDrop -= deltaTime;

            while (toDrop > 0 && nextDrop <= 0f)
            {
                Drop(context.Aim);
                toDrop--;
                nextDrop += dropInterval;
            }

            if (toDrop == 0)
                cooldown = Stats[Equip_Stat.Cooldown];

            return;
        }

        cooldown = Mathf.Max(0f, cooldown - deltaTime);

        if (context.Active && cooldown <= 0f)
        {
            toDrop = Mathf.Max(1, Mathf.RoundToInt(Stats[Equip_Stat.ProjectileCount]));
            nextDrop = 0f;
            NotifyUsed();
            Tick(new Equip_Context(context.Aim, 0f, context.Held, false, false));     // the first one goes now
        }
    }

    private void Drop(in Weapon_AimData aim)
    {
        Vector3 from = bombBay != null ? bombBay.Position : aim.PlanePosition + Vector3.down;
        Vector2 jitter = UnityEngine.Random.insideUnitCircle * scatter;
        Vector3 velocity = aim.PlaneVelocity * inheritVelocity + Vector3.down * dropSpeed + new Vector3(jitter.x, 0f, jitter.y);

        // Lives until it lands: the floor always catches it
        float lifetime = Stats[Equip_Stat.Range] / Mathf.Max(velocity.magnitude, 1f) + 10f;

        projectiles.Spawn(bombType, from, velocity, lifetime, Stats[Equip_Stat.Damage], Stats[Equip_Stat.Radius], (int)slot);

        if (bombBay != null)
            bombBay.Fire();

        Utilities.TryPlaySound(dropSound, from);
        Camera_Rig.Shake(dropTrauma);

        if (Loadout.Visuals != null)
            Loadout.Visuals.Kick(Vector3.zero, new Vector3(-dropKick, 0f, 0f));

        Dropped?.Invoke();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (drawAlways)
            DrawLanding();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawAlways)
            DrawLanding();
    }

    // Each bomb of a stick dropped now: where it would land with this flight (or the preview's), and its blast
    private void DrawLanding()
    {
        if (!drawLanding || bomb == null)
            return;

        bool live = Application.isPlaying && Loadout != null && Loadout.Plane != null;
        Vector3 from = bombBay != null ? bombBay.Position : transform.position + Vector3.down;
        Vector3 planeVelocity = live ? Loadout.Plane.Velocity : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * previewSpeed;
        float floor = Combat_Projectiles.Instance != null ? Combat_Projectiles.Instance.FloorHeight : 0f;

        float radius = Stat(Equip_Stat.Radius, live);
        int count = Mathf.Max(1, Mathf.RoundToInt(Stat(Equip_Stat.ProjectileCount, live)));
        Vector3 velocity = planeVelocity * inheritVelocity + Vector3.down * dropSpeed;
        float gravity = Mathf.Max(bomb.gravity, 0.01f);

        for (int k = 0; k < count; k++)
        {
            // Dropped k intervals later, from wherever the plane has got to by then
            Vector3 start = from + planeVelocity * (dropInterval * k);
            float drop = start.y - floor;

            if (drop <= 0f)
                continue;

            // Falling from start.y with velocity.y under gravity: when it reaches the floor
            float time = (velocity.y + Mathf.Sqrt(velocity.y * velocity.y + 2f * gravity * drop)) / gravity;
            Vector3 landing = start + new Vector3(velocity.x, 0f, velocity.z) * time;
            landing.y = floor;

            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawLine(start, landing);
            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
            Debug_Gizmos.Circle(landing, radius);
            Gizmos.color = new Color(1f, 0.65f, 0.15f, 0.7f);
            Debug_Gizmos.Circle(landing, radius * bomb.flingRadiusScale);
        }
    }

    // The stat as the game has it in play, or as authored on this prefab outside play
    private float Stat(Equip_Stat stat, bool live)
    {
        if (live)
            return Stats[stat];

        if (baseStats != null)
        {
            for (int i = 0; i < baseStats.Length; i++)
            {
                if (baseStats[i].stat == stat)
                    return baseStats[i].value;
            }
        }

        return 0f;
    }
#endif
}
