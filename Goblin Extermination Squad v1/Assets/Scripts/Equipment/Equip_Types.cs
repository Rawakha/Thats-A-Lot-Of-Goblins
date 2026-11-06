using System;
using UnityEngine;

// Which slot a piece of equipment lives in. Each slot holds one piece: equipping into a full slot replaces what's
// there, which is how a new fast-firing gun swaps out the machine gun. More slots (a second ability) are new values.
public enum Equip_Slot
{
    Weapon,     // fired with the fire button
    Ability     // activated with the ability button, e.g. carpet bombs
}

// When a piece of equipment acts
public enum Equip_Trigger
{
    Hold,       // while its slot's button is held, e.g. guns
    Press,      // once per press, e.g. carpet bombs, boost
    Auto,       // all the time, e.g. a tail gunner
    Passive     // never ticks an action, only changes things, e.g. armour
}

// What a piece of equipment is, so upgrades can target groups ("+20% radius to Explosive")
[Flags]
public enum Equip_Tags
{
    None = 0,
    Bullet = 1 << 0,
    Explosive = 1 << 1,
    Fire = 1 << 2,
    Projectile = 1 << 3,
    Air = 1 << 4,       // can hit things in the air
    Ground = 1 << 5     // can hit things on the ground
}

// Every number upgrades can change. Add new stats before Length.
public enum Equip_Stat
{
    Damage,
    FireRate,           // shots per second
    ProjectileSpeed,
    Range,
    Spread,             // degrees either side
    ProjectileCount,
    Radius,             // area damage radius on impact
    Cooldown,
    Duration,
    MagazineSize,       // shots before a reload; 0 is a bottomless magazine
    ReloadTime,         // seconds to refill an empty magazine
    Length
}

public enum Equip_ModifierOp
{
    Add,                // added to the base
    Percent,            // value in percent (20 = +20%); all are summed, then applied once: +20% and +30% is +50%
    Multiply            // applied last, each one stacks
}

public enum Equip_ModifierScope
{
    All,                // every piece of equipment
    Tags,               // equipment with any of the given tags
    Equipment           // one specific piece of equipment (its prefab)
}

[Serializable]
public struct Equip_StatValue
{
    public Equip_Stat stat;
    public float value;

    public Equip_StatValue(Equip_Stat stat, float value)
    {
        this.stat = stat;
        this.value = value;
    }
}

[Serializable]
public struct Equip_Modifier
{
    public Equip_ModifierScope scope;
    public Equip_Tags tags;
    [Tooltip("For the Equipment scope: the equipment prefab this applies to.")]
    public Equip_Item equipment;
    public Equip_Stat stat;
    public Equip_ModifierOp op;
    public float value;

    public bool AppliesTo(Equip_Item item)
    {
        switch (scope)
        {
            case Equip_ModifierScope.All: return true;
            case Equip_ModifierScope.Tags: return (item.tags & tags) != 0;
            default: return item.Is(equipment);
        }
    }
}
