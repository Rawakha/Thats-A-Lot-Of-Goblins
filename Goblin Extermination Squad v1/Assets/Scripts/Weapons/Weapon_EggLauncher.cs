using UnityEngine;

// Main-slot egg launcher, step 1: hold Fire to lob eggs; impacts use the projectile's crack effect.
// Uses the machine gun's magazine, R/automatic reload, muzzle groups, sounds and recoil. Author a weapon prefab
// with editable models/muzzles and assign an egg Combat_ProjectileType (gravity > 0, Explodes off, Linger 0).
// Launch Angle is up from horizontal; shared aim supplies yaw only. Ground/air toggle doesn't flatten the lob.
// Chickens and hatch chance come in step 2. The crosshair predicts terrain, not intervening scenery.
//
// GC: none per frame/shot here. Inherited equipment stats/part arrays allocate on equip; Effect_Pool allocates on
// first use/growth and optional AudioManager sounds allocate coroutines. No new event or frame subscriptions.
public class Weapon_EggLauncher : Weapon_MachineGun, Weapon_IAimOverride
{
    // Slow lobbed eggs leave now rather than skipping their collision path to catch up after a hitch.
    protected override int MaxShotsPerFrame => 1;
    protected override bool BackdateShots => false;

    [Header("Egg Flight")]
    [Tooltip("Degrees above horizontal. Positive lobs upward; negative fires toward the ground. Shared aim controls yaw only.")]
    [SerializeField, Range(-60f, 80f)] private float launchAngle = 20f;

    [Tooltip("Muzzle used for the crosshair and arc preview. Assign the firing muzzle; with multiple muzzles this previews this one only.")]
    [SerializeField] private Weapon_Muzzle previewMuzzle;

    [Tooltip("Show the egg's centre trajectory and impact radius while selected. In edit mode it previews from this object's height, without plane velocity.")]
    [SerializeField] private bool drawArc = true;

    protected override void Reset()
    {
        base.Reset();
        displayName = "Egg Launcher";
        description = "Lobs eggs that crack on impact.";
        tags = Equip_Tags.Projectile | Equip_Tags.Ground;
        inheritVelocity = 0.25f;
        baseStats = new[]
        {
            new Equip_StatValue(Equip_Stat.Damage, 1f),
            new Equip_StatValue(Equip_Stat.FireRate, 2f),
            new Equip_StatValue(Equip_Stat.ProjectileSpeed, 22f),
            new Equip_StatValue(Equip_Stat.Range, 160f),
            new Equip_StatValue(Equip_Stat.Spread, 0f),
            new Equip_StatValue(Equip_Stat.Radius, 1.2f),
            new Equip_StatValue(Equip_Stat.MagazineSize, 6f),
            new Equip_StatValue(Equip_Stat.ReloadTime, 2f)
        };
    }

    protected override Vector3 ShotVelocity(in Weapon_AimData aim, float speed, float spread)
    {
        Quaternion rotation = Quaternion.Euler(-launchAngle + Random.Range(-spread, spread),
            aim.AimYaw + Random.Range(-spread, spread), 0f);
        return rotation * Vector3.forward * speed + aim.PlaneVelocity * inheritVelocity;
    }

    public void RefineAim(ref Weapon_AimData aim)
    {
        if (projectile == null)
            return;

        Vector3 origin = previewMuzzle != null ? previewMuzzle.Position : aim.PlanePosition;
        float speed = Mathf.Max(Stats[Equip_Stat.ProjectileSpeed], 0.01f);
        float lifetime = Mathf.Max(Stats[Equip_Stat.Range] / speed, 0f);
        Vector3 velocity = PreviewVelocity(aim.AimYaw, aim.PlaneVelocity, speed);
        aim.ShotPoint = TraceArc(origin, velocity, lifetime, false, out bool landed);
        aim.GroundPoint = aim.ShotPoint;
        aim.HasGroundPoint = landed;
        aim.AimPoint = TraceArc(origin, PreviewVelocity(aim.Heading, aim.PlaneVelocity, speed), lifetime, false, out _);
    }

    private Vector3 PreviewVelocity(float yaw, Vector3 planeVelocity, float speed)
    {
        return Quaternion.Euler(-launchAngle, yaw, 0f) * Vector3.forward * speed + planeVelocity * inheritVelocity;
    }

    // Bounded terrain-only preview. The dt term matches Combat_Projectiles' semi-implicit gravity on flat ground.
    // Sampling catches shelves and ledges; actual sphere sweeps can hit scenery earlier than this centre path.
    private Vector3 TraceArc(Vector3 origin, Vector3 velocity, float lifetime, bool draw, out bool landed)
    {
        const int Segments = 64;
        float gravity = projectile != null ? projectile.gravity : 0f;
        float step = Application.isPlaying ? Time.deltaTime : 1f / 60f;
        float floor = Combat_Projectiles.Instance != null ? Combat_Projectiles.Instance.FloorHeight : 0f;
        Vector3 previous = origin;
        landed = false;

        for (int i = 1; i <= Segments; i++)
        {
            float time = lifetime * i / Segments;
            Vector3 point = origin + velocity * time - Vector3.up * (0.5f * gravity * time * (time + step));
            float height = Level_GroundSurface.HeightAt(point, floor);

            if (point.y <= height)
            {
                float fraction = previous.y > height ? Mathf.Clamp01((previous.y - height) / (previous.y - point.y)) : 0f;
                point = Vector3.Lerp(previous, point, fraction);
                if (draw) Gizmos.DrawLine(previous, point);
                landed = true;
                return point;
            }

            if (draw) Gizmos.DrawLine(previous, point);
            previous = point;
        }

        return previous;
    }

    private float PreviewStat(Equip_Stat stat)
    {
        if (Application.isPlaying && Loadout != null)
            return Stats[stat];
        if (baseStats != null)
            for (int i = 0; i < baseStats.Length; i++)
                if (baseStats[i].stat == stat) return baseStats[i].value;
        return 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawArc || projectile == null)
            return;
        Vector3 origin = previewMuzzle != null ? previewMuzzle.Position : transform.position;
        float yaw = transform.eulerAngles.y;
        Vector3 planeVelocity = Vector3.zero;
        if (Application.isPlaying && Loadout != null)
        {
            Weapon_AimData aim = Loadout.GetComponent<Weapon_Aim>().Data;
            yaw = aim.AimYaw;
            planeVelocity = aim.PlaneVelocity;
        }
        float speed = Mathf.Max(PreviewStat(Equip_Stat.ProjectileSpeed), 0.01f);
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        Vector3 end = TraceArc(origin, PreviewVelocity(yaw, planeVelocity, speed),
            Mathf.Max(PreviewStat(Equip_Stat.Range) / speed, 0f), true, out bool landed);
        if (landed) Debug_Gizmos.Circle(end, PreviewStat(Equip_Stat.Radius));
    }
}
