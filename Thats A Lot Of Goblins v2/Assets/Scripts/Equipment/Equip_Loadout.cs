using System;
using System.Collections.Generic;
using UnityEngine;

// Everything the plane has equipped, one piece per slot, plus every stat modifier from upgrades.
// Equip spawns an equipment prefab under the mount (the plane's model by default, so its parts bank and bob with
// the plane) and replaces whatever was in that slot, destroying it. Stats are rebuilt from the full modifier list
// on every change, so upgrades can be added and removed in any order.
// Scale: the mount is usually scaled (it's inside the plane's model). By default the prefab is spawned at its own
// world size, so author equipment prefabs at real size with the root at the mount's pivot, and the mount's scale
// never shrinks or stretches them.
// Ticks after Plane_Controller (on its Ticked event), so the aim is built from this frame's movement.
// Abilities: with an Equip_AbilityBelt on the same object, abilities live on the belt instead of the Ability slot.
// The loadout ticks the belt and rebuilds its stats; Get(Ability) is the belt's selected ability, and Equip with an
// ability puts it on the belt with infinite charges (cards should call the belt's Add with charges instead).
//
// GC: none per frame. Equip instantiates the prefab (and finds its parts); modifiers allocate only if the list
// grows past its capacity. One subscription (Plane_Controller.Ticked), added on enable.
[RequireComponent(typeof(Plane_Controller), typeof(Plane_Input), typeof(Weapon_Aim))]
public class Equip_Loadout : MonoBehaviour
{
    private static readonly int SlotCount = Enum.GetValues(typeof(Equip_Slot)).Length;

    [Tooltip("Where equipment prefabs are spawned. Empty uses Plane_Visuals' model root, or this object.")]
    [SerializeField] private Transform mount;

    [Tooltip("On: prefabs spawn at their own world size, whatever the mount's scale, and their position offset is in " +
             "world units. Off: they inherit the mount's scale, so author them as if built inside it.")]
    [SerializeField] private bool keepPrefabScale = true;

    [SerializeField] private Equip_Item[] startingEquipment;

    [Header("Debug")]
    [SerializeField] private Equip_Item debugEquipment;
    [SerializeField] private Equip_Modifier debugModifier;

    private readonly List<Equip_ModifierEntry> modifiers = new List<Equip_ModifierEntry>(32);
    private Equip_Item[] slots;
    private Equip_AbilityBelt belt;

    private Plane_Controller plane;
    private Plane_Input input;
    private Weapon_Aim aim;

    // Fires after a slot's contents change (equipped, replaced or emptied), for UI
    public event Action<Equip_Slot> SlotChanged;

    public Plane_Controller Plane => plane;
    public Plane_Visuals Visuals { get; private set; }
    public Transform Mount => mount;

    private void Awake()
    {
        slots = new Equip_Item[SlotCount];
        plane = GetComponent<Plane_Controller>();
        input = GetComponent<Plane_Input>();
        aim = GetComponent<Weapon_Aim>();
        Visuals = GetComponent<Plane_Visuals>();
        belt = GetComponent<Equip_AbilityBelt>();

        if (mount == null)
            mount = Visuals != null && Visuals.ModelRoot != null ? Visuals.ModelRoot : transform;
    }

    private void Start()
    {
        if (startingEquipment == null)
            return;

        for (int i = 0; i < startingEquipment.Length; i++)
            Equip(startingEquipment[i]);
    }

    private void OnEnable()
    {
        plane.Ticked += Tick;
    }

    private void OnDisable()
    {
        plane.Ticked -= Tick;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < slots.Length; i++)
            RemoveFromSlot(i);
    }

    // The spawned item in a slot, or null (also null before Awake, e.g. when the HUD wakes first). With a belt, the
    // Ability slot is the belt's selected ability.
    public Equip_Item Get(Equip_Slot slot)
    {
        if (slot == Equip_Slot.Ability && belt != null)
            return belt.SelectedAbility;

        // Bounds-checked: a serialized slot value from an older enum layout would otherwise throw
        int index = (int)slot;
        return slots != null && index >= 0 && index < slots.Length ? slots[index] : null;
    }

    public bool IsEquipped(Equip_Item prefab)
    {
        return prefab != null && slots[(int)prefab.slot] != null && slots[(int)prefab.slot].Is(prefab);
    }

    // Spawns the prefab into its slot, replacing what was there. Returns the replaced prefab, or null.
    public Equip_Item Equip(Equip_Item prefab)
    {
        if (prefab == null)
            return null;

        if (belt != null && prefab is Equip_Ability ability)
        {
            belt.Add(ability, 0, true);
            return null;
        }

        int index = (int)prefab.slot;
        Equip_Item current = slots[index];

        if (current != null && current.Is(prefab))
            return null;

        Equip_Item replaced = current != null ? current.Source : null;
        RemoveFromSlot(index);

        Equip_Item item = Spawn(prefab);
        slots[index] = item;
        item.NotifyEquipped();

        SlotChanged?.Invoke(prefab.slot);
        return replaced;
    }

    public void Unequip(Equip_Slot slot)
    {
        if (RemoveFromSlot((int)slot))
            SlotChanged?.Invoke(slot);
    }

    public void AddModifier(object source, in Equip_Modifier modifier)
    {
        modifiers.Add(new Equip_ModifierEntry { Source = source, Modifier = modifier });
        RebuildStats();
    }

    public void AddModifiers(object source, IReadOnlyList<Equip_Modifier> sourceModifiers)
    {
        for (int i = 0; i < sourceModifiers.Count; i++)
            modifiers.Add(new Equip_ModifierEntry { Source = source, Modifier = sourceModifiers[i] });

        RebuildStats();
    }

    // Removes every modifier added by this source. Order doesn't matter to the maths, so this swaps back.
    public void RemoveModifiers(object source)
    {
        bool removed = false;

        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            if (modifiers[i].Source != source)
                continue;

            int last = modifiers.Count - 1;
            modifiers[i] = modifiers[last];
            modifiers.RemoveAt(last);
            removed = true;
        }

        if (removed)
            RebuildStats();
    }

    // Spawns a prefab under the mount with its stats built, not yet in a slot and not yet told it's equipped. Used by
    // Equip and by the ability belt.
    internal Equip_Item Spawn(Equip_Item prefab)
    {
        Equip_Item item = Instantiate(prefab, mount, false);

        if (keepPrefabScale)
            CancelMountScale(item.transform, prefab.transform);

        item.name = prefab.name;
        item.Bind(prefab, this);
        item.Stats.Rebuild(item, modifiers);
        return item;
    }

    internal void Despawn(Equip_Item item)
    {
        item.NotifyUnequipped();
        Destroy(item.gameObject);
    }

    internal void NotifySlotChanged(Equip_Slot slot)
    {
        SlotChanged?.Invoke(slot);
    }

    // Whether a trigger says act now, from its button state
    internal static bool IsActive(Equip_Trigger trigger, bool held, bool pressed)
    {
        switch (trigger)
        {
            case Equip_Trigger.Hold: return held;
            case Equip_Trigger.Press: return pressed;
            case Equip_Trigger.Auto: return true;
            default: return false;
        }
    }

    // Divides the mount's world scale out of the spawned root, so it ends up the prefab's own size in the world.
    // Assumes the mount is scaled evenly or only along its own axes, as models are.
    private void CancelMountScale(Transform spawned, Transform prefab)
    {
        Vector3 mountScale = mount.lossyScale;

        spawned.localPosition = Divide(prefab.localPosition, mountScale);
        spawned.localScale = Divide(prefab.localScale, mountScale);
    }

    private static Vector3 Divide(Vector3 value, Vector3 by)
    {
        return new Vector3(
            Mathf.Abs(by.x) > 0.0001f ? value.x / by.x : value.x,
            Mathf.Abs(by.y) > 0.0001f ? value.y / by.y : value.y,
            Mathf.Abs(by.z) > 0.0001f ? value.z / by.z : value.z);
    }

    private bool RemoveFromSlot(int index)
    {
        Equip_Item item = slots[index];

        if (item == null)
            return false;

        slots[index] = null;
        Despawn(item);
        return true;
    }

    private void RebuildStats()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            Equip_Item item = slots[i];

            if (item == null)
                continue;

            item.Stats.Rebuild(item, modifiers);
            item.OnStatsChanged();
        }

        if (belt != null)
            belt.RebuildStats(modifiers);
    }

    private void Tick()
    {
        float deltaTime = Time.deltaTime;
        aim.Compute(deltaTime);
        Weapon_AimData aimData = aim.Data;

        for (int i = 0; i < slots.Length; i++)
        {
            Equip_Item item = slots[i];

            if (item == null)
                continue;

            ReadSlotInput((Equip_Slot)i, out bool held, out bool pressed);
            bool active = IsActive(item.trigger, held, pressed);

            item.Tick(new Equip_Context(aimData, deltaTime, held, pressed, active));
            item.TickParts(deltaTime);
        }

        if (belt != null)
            belt.Tick(aimData, deltaTime, input.AbilityHeld, input.AbilityPressed, input.CycleAbilityPressed);

        // Reload (R): the weapon reloads early, if it has a magazine that isn't full
        if (input.ReloadPressed && slots[(int)Equip_Slot.Weapon] is Weapon_IAmmo magazine)
            magazine.TryReload();
    }

    private void ReadSlotInput(Equip_Slot slot, out bool held, out bool pressed)
    {
        switch (slot)
        {
            case Equip_Slot.Weapon:
                held = input.FireHeld;
                pressed = input.FirePressed;
                break;

            case Equip_Slot.Ability:
                held = input.AbilityHeld;
                pressed = input.AbilityPressed;
                break;

            default:
                held = pressed = false;
                break;
        }
    }

    [InspectorButton("Equip Debug Equipment", true)]
    private void DebugEquip() => Equip(debugEquipment);

    [InspectorButton("Empty Weapon Slot", true)]
    private void DebugUnequipWeapon() => Unequip(Equip_Slot.Weapon);

    [InspectorButton("Add Debug Modifier", true)]
    private void DebugAddModifier() => AddModifier(this, debugModifier);

    [InspectorButton("Remove Debug Modifiers", true)]
    private void DebugRemoveModifiers() => RemoveModifiers(this);
}
