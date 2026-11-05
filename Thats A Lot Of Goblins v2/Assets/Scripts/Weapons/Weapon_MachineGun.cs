using System;
using UnityEngine;

// Something with a magazine, for the HUD's ammo counter and reload bar
public interface Weapon_IAmmo
{
    int Ammo { get; }
    int MagazineSize { get; }      // 0: bottomless, no reloads
    bool Reloading { get; }
    float ReloadProgress { get; }  // 0 just emptied, 1 full again; 1 when not reloading

    // When Ammo, MagazineSize or Reloading changes (not every frame of a reload)
    event Action AmmoChanged;

    // Starts a reload early (the player pressed Reload). False if there's nothing to do: full, bottomless or already
    // reloading.
    bool TryReload();
}

// Muzzles that fire together as one shot
[Serializable]
public struct Weapon_MuzzleGroup
{
    public Weapon_Muzzle[] muzzles;
}

// A fast-firing gun. Put this on the root of a weapon prefab, with the gun models and their Weapon_Muzzles inside.
// While fire is held it shoots FireRate times a second: each shot fires every muzzle in one group, then the next
// shot uses the next group, looping. Shots follow the shared aim, so it fires level at cruise and strafes the
// ground while descending. Shots that fall between frames are placed where they would have been, so high fire
// rates stay evenly spaced.
//
// Magazine: each shot (not each muzzle) uses one round of Magazine Size. When it runs dry the gun reloads by itself
// over Reload Time, then fires again. Reload (R) starts one early from any count below full; the rounds left are
// kept until it finishes, then the magazine is full. A Magazine Size of 0 never runs out.
//
// Other fast-firing guns can reuse this with different stats, models and muzzles. A gun that behaves differently
// gets its own Equip_Item subclass in the Weapon slot, replacing this one when picked.
//
// GC: none per shot in this script; each shot sound allocates inside AudioManager (a coroutine). No subscription:
// ticked by Equip_Loadout. AmmoChanged is a plain event the HUD listens to.
public class Weapon_MachineGun : Equip_Item, Weapon_IAmmo
{
    // Most shots fired in one frame, so a hitch doesn't dump a burst
    private const int MaxShotsPerFrame = 8;

    [Header("Machine Gun")]
    [Tooltip("The projectile prefab (root has Combat_ProjectileType). Its scale is the shot's size.")]
    [SerializeField] private Combat_ProjectileType projectile;

    [Tooltip("Each shot fires every muzzle in one group, then the next shot uses the next group, looping round. " +
             "Two groups of one (left gun, right gun) alternate sides; more groups make longer patterns.")]
    [SerializeField] private Weapon_MuzzleGroup[] muzzleGroups;

    [Tooltip("How much of the plane's velocity shots keep. 1 keeps the strafe line steady under the plane.")]
    [SerializeField, Range(0f, 1f)] private float inheritVelocity = 1f;

    [Header("Recoil")]
    [Tooltip("Nose-up kick on the plane per shot, degrees/second.")]
    [SerializeField, Min(0f)] private float recoilPitch = 6f;

    [Tooltip("Roll kick per shot, degrees/second. Off-centre groups roll away from their side, so alternating sides rock the plane.")]
    [SerializeField, Min(0f)] private float recoilRoll = 10f;

    [Tooltip("Camera trauma per shot. Sustained fire builds a light chatter.")]
    [SerializeField, Range(0f, 1f)] private float cameraTrauma = 0.04f;

    [Tooltip("Camera push per shot, units/second, back against the firing direction.")]
    [SerializeField, Min(0f)] private float cameraKick = 0.4f;

    [Header("Audio")]
    [Tooltip("Played once per shot (not per muzzle). Give it a few clips and a little pitch range so bursts don't machine-gun the same sample.")]
    [SerializeField] private SoundData shotSound;

    [Tooltip("Played when the trigger is released after firing: the echo / barrel tail that makes a burst feel finished.")]
    [SerializeField] private SoundData releaseSound;

    private Combat_Projectiles projectiles;
    private int projectileType = -1;
    private float cooldown;
    private int nextGroup;
    private bool firing;
    private int ammo;
    private float reloadLeft;

    // Fires after each shot with the group that fired, for whole-gun animation (spinning barrels, heat glow)
    public event Action<int> Shot;

    public event Action AmmoChanged;

    public int Ammo => ammo;
    public int MagazineSize => Mathf.Max(0, Mathf.RoundToInt(Stats[Equip_Stat.MagazineSize]));
    public bool Reloading => reloadLeft > 0f;

    public float ReloadProgress
    {
        get
        {
            float time = Stats[Equip_Stat.ReloadTime];
            return Reloading && time > 0f ? 1f - Mathf.Clamp01(reloadLeft / time) : 1f;
        }
    }

    protected override void Reset()
    {
        slot = Equip_Slot.Weapon;
        trigger = Equip_Trigger.Hold;
        tags = Equip_Tags.Bullet | Equip_Tags.Projectile | Equip_Tags.Air | Equip_Tags.Ground;
        baseStats = new[]
        {
            new Equip_StatValue(Equip_Stat.Damage, 1f),
            new Equip_StatValue(Equip_Stat.FireRate, 12f),
            new Equip_StatValue(Equip_Stat.ProjectileSpeed, 110f),
            new Equip_StatValue(Equip_Stat.Range, 60f),
            new Equip_StatValue(Equip_Stat.Spread, 1.5f),
            new Equip_StatValue(Equip_Stat.Radius, 0.6f),
            new Equip_StatValue(Equip_Stat.MagazineSize, 150f),
            new Equip_StatValue(Equip_Stat.ReloadTime, 2f)
        };
    }

    // An upgrade changed the magazine: a bigger one tops up by the difference, a smaller one cuts the count down
    private int lastMagazineSize;

    public override void OnStatsChanged()
    {
        int size = MagazineSize;

        if (size == lastMagazineSize)
            return;

        if (!Reloading)
            ammo = Mathf.Clamp(ammo + (size - lastMagazineSize), 0, size);

        lastMagazineSize = size;
        AmmoChanged?.Invoke();
    }

    protected override void OnEquip()
    {
        // A full magazine to start (stats are built by now)
        lastMagazineSize = MagazineSize;
        ammo = lastMagazineSize;
        reloadLeft = 0f;
        AmmoChanged?.Invoke();

        projectiles = Combat_Projectiles.Instance;

        if (projectiles == null)
        {
            Debug.LogError($"{DisplayName}: No Combat_Projectiles in the scene, the gun can't fire.", this);
            return;
        }

        if (projectile == null)
        {
            Debug.LogError($"{DisplayName}: No projectile type assigned, the gun can't fire.", this);
            return;
        }

        projectileType = projectiles.RegisterType(projectile);
    }

    public override void Tick(in Equip_Context context)
    {
        cooldown -= context.DeltaTime;

        // Reloading: no shots until the magazine is full again
        if (Reloading)
        {
            reloadLeft -= context.DeltaTime;

            if (reloadLeft <= 0f)
            {
                reloadLeft = 0f;
                ammo = MagazineSize;
                AmmoChanged?.Invoke();
            }
        }

        // Not firing (or reloading): let the gun ready its next shot, but don't bank shots
        if (!context.Active || projectileType < 0 || Reloading)
        {
            if (cooldown < 0f)
                cooldown = 0f;

            if (firing)
            {
                firing = false;
                PlaySound(releaseSound);
            }

            return;
        }

        float fireRate = Stats[Equip_Stat.FireRate];

        if (fireRate <= 0f)
            return;

        float interval = 1f / fireRate;

        for (int shots = 0; cooldown <= 0f && shots < MaxShotsPerFrame && !Reloading; shots++)
        {
            Fire(context.Aim, -cooldown);
            cooldown += interval;
            UseRound();
        }

        if (cooldown < 0f)
            cooldown = 0f;
    }

    // One shot: every muzzle in the current group fires, then the next group is up.
    // lateBy: how long ago this shot should have left, so it's moved on by that much.
    private void Fire(in Weapon_AimData aim, float lateBy)
    {
        int group = -1;
        Weapon_Muzzle[] muzzles = null;

        if (muzzleGroups != null && muzzleGroups.Length > 0)
        {
            group = nextGroup % muzzleGroups.Length;
            muzzles = muzzleGroups[group].muzzles;
            nextGroup = (group + 1) % muzzleGroups.Length;
        }

        float side = 0f;
        bool fired = false;

        if (muzzles != null)
        {
            Transform plane = Loadout.transform;

            for (int i = 0; i < muzzles.Length; i++)
            {
                Weapon_Muzzle muzzle = muzzles[i];

                if (muzzle == null)
                    continue;

                FireFrom(aim, muzzle.Position, lateBy);
                muzzle.Fire();
                side += plane.InverseTransformPoint(muzzle.Position).x;
                fired = true;
            }
        }

        // No muzzles set up: fire from the plane so the gun still works while it's being authored
        if (!fired)
            FireFrom(aim, aim.PlanePosition, lateBy);

        firing = true;
        PlaySound(shotSound);

        Camera_Rig.Shake(cameraTrauma);
        Camera_Rig.Kick(-aim.Direction * cameraKick);

        if (Loadout.Visuals != null)
        {
            float roll = Mathf.Abs(side) > 0.01f ? -Mathf.Sign(side) * UnityEngine.Random.Range(0.5f, 1f) : UnityEngine.Random.Range(-1f, 1f);
            Loadout.Visuals.Kick(Vector3.zero, new Vector3(-recoilPitch, 0f, roll * recoilRoll));
        }

        Shot?.Invoke(group);
    }

    // One round out of the magazine; empty starts the reload (or refills at once with no Reload Time)
    private void UseRound()
    {
        if (MagazineSize <= 0)
            return;

        ammo = Mathf.Max(0, ammo - 1);

        if (ammo == 0)
            StartReload();

        AmmoChanged?.Invoke();
    }

    public bool TryReload()
    {
        if (MagazineSize <= 0 || Reloading || ammo >= MagazineSize)
            return false;

        StartReload();
        AmmoChanged?.Invoke();
        return true;
    }

    // Over Reload Time, or straight away with none
    private void StartReload()
    {
        float time = Stats[Equip_Stat.ReloadTime];

        if (time > 0f)
            reloadLeft = time;
        else
            ammo = MagazineSize;
    }

    private void PlaySound(SoundData data)
    {
        SoundConfig sound = SoundData.Get(data);

        if (sound != null && AudioManager.Instance != null)
            AudioManager.Instance.RequestSound(sound, Loadout.transform.position);
    }

    private void FireFrom(in Weapon_AimData aim, Vector3 origin, float lateBy)
    {
        float spread = Stats[Equip_Stat.Spread];
        Quaternion rotation = Quaternion.Euler(
            aim.Pitch + UnityEngine.Random.Range(-spread, spread),
            aim.AimYaw + UnityEngine.Random.Range(-spread, spread),
            0f);

        float speed = Mathf.Max(Stats[Equip_Stat.ProjectileSpeed], 0.01f);
        Vector3 velocity = rotation * Vector3.forward * speed + aim.PlaneVelocity * inheritVelocity;
        float lifetime = Stats[Equip_Stat.Range] / speed;

        projectiles.Spawn(projectileType, origin + velocity * lateBy, velocity, lifetime - lateBy,
            Stats[Equip_Stat.Damage], Stats[Equip_Stat.Radius], (int)slot);
    }
}
