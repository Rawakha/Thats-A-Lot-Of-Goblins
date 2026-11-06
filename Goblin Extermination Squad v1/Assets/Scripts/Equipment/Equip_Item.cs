using UnityEngine;

// What a piece of equipment gets each tick
public readonly struct Equip_Context
{
    public readonly Weapon_AimData Aim;
    public readonly float DeltaTime;

    // The slot's button state, for equipment that wants more than Active (e.g. charge on hold, fire on release)
    public readonly bool Held;
    public readonly bool Pressed;

    // Whether the trigger says act now: held for Hold, pressed this frame for Press, always for Auto
    public readonly bool Active;

    public Equip_Context(in Weapon_AimData aim, float deltaTime, bool held, bool pressed, bool active)
    {
        Aim = aim;
        DeltaTime = deltaTime;
        Held = held;
        Pressed = pressed;
        Active = active;
    }
}

// Animated or reactive pieces inside an equipment prefab: barrel recoil, pop-in, spinning barrels, missile racks.
// Found once on equip and ticked by their item, so parts never subscribe to UpdateManager themselves.
public interface Equip_IPart
{
    void OnEquipped(Equip_Item item);
    void OnUnequipped();
    void Tick(float deltaTime);
}

// The root of an equipment prefab. Everything a weapon or ability is lives in its prefab: this script, its models
// (the guns under the wings, the missile rack), its muzzles and its animated parts. Equip_Loadout spawns the prefab
// under the plane's model, so author the prefab with its root at the plane's origin and place parts where they sit
// on the plane. The prefab asset itself is the identity: the upgrade pool offers prefabs, and modifiers aimed at
// one piece of equipment reference its prefab.
//
// Each kind of equipment subclasses this. No Update: the loadout ticks it. Subclasses set their default slot,
// trigger, tags and stats in Reset.
public abstract class Equip_Item : MonoBehaviour
{
    [Header("Info")]
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Slot")]
    [Tooltip("Equipping this into a slot that's already full replaces what's there.")]
    public Equip_Slot slot;
    public Equip_Trigger trigger;
    public Equip_Tags tags;

    [Header("Stats")]
    [Tooltip("Stats not listed here start at 0.")]
    public Equip_StatValue[] baseStats;

    private Equip_IPart[] parts;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    // The prefab this was spawned from. On the prefab asset itself, null.
    public Equip_Item Source { get; private set; }
    public Equip_Loadout Loadout { get; private set; }

    // Rebuilt by the loadout whenever equipment or modifiers change; read it, never cache values from it
    public Equip_Stats Stats { get; } = new Equip_Stats();

    // Whether this is the given prefab, or was spawned from it
    public bool Is(Equip_Item prefab) => prefab != null && (this == prefab || Source == prefab);

    internal void Bind(Equip_Item source, Equip_Loadout loadout)
    {
        Source = source;
        Loadout = loadout;
        parts = GetComponentsInChildren<Equip_IPart>(true);
    }

    internal void NotifyEquipped()
    {
        OnEquip();

        for (int i = 0; i < parts.Length; i++)
            parts[i].OnEquipped(this);
    }

    internal void NotifyUnequipped()
    {
        for (int i = 0; i < parts.Length; i++)
            parts[i].OnUnequipped();

        OnUnequip();
    }

    internal void TickParts(float deltaTime)
    {
        for (int i = 0; i < parts.Length; i++)
            parts[i].Tick(deltaTime);
    }

    // Stats are already built when this is called
    protected virtual void OnEquip() { }

    // Clean up anything this spawned or registered outside its own prefab
    protected virtual void OnUnequip() { }

    public virtual void OnStatsChanged() { }

    public abstract void Tick(in Equip_Context context);

    protected virtual void Reset() { }
}
