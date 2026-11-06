using System;
using System.Collections.Generic;
using UnityEngine;

// One ability on the belt as authored: which ability, and how many charges (or infinite)
[Serializable]
public struct Equip_AbilityStack
{
    public Equip_Ability ability;

    [Tooltip("Never runs out: only its cooldown limits it. The HUD shows no count.")]
    public bool infinite;

    [Tooltip("Uses before it's gone. Ignored when Infinite.")]
    [Min(0)] public int charges;
}

// The plane's abilities: a short list of consumables, each with charges, one of them selected. Abilities are used
// up and replaced (the pick 1 of 3 cards add them), so the player keeps cycling through different ones.
//   - Every ability on the belt is spawned and ticked all the time, so cooldowns recover and things it already let
//     go of (falling bombs) carry on while it isn't selected. Only the selected one gets the ability button.
//   - Cycle Ability (E) selects the next one, wrapping round. Swapping is allowed any time: each ability decides in
//     OnDeselected what stops when it's swapped away from.
//   - Using one costs a charge (infinite ones never run out). At 0 it leaves the belt once it's finished what it
//     started (Busy), and the next ability is selected.
//   - Add stacks charges onto an ability already on the belt, or adds it at the end.
//
// Setup: put this on the plane next to Equip_Loadout and list the Starting Abilities (e.g. Carpet Bomb, Infinite).
// Abilities no longer go in the loadout's Starting Equipment; one left there is put on the belt as infinite.
//
// GC: none per frame. Add instantiates the ability's prefab; Remove destroys it. No UpdateManager subscription:
// Equip_Loadout ticks the belt. Each ability's Used event is subscribed while it's on the belt.
[RequireComponent(typeof(Equip_Loadout))]
public class Equip_AbilityBelt : MonoBehaviour
{
    private class Entry
    {
        public Equip_Ability Prefab;
        public Equip_Ability Item;
        public int Charges;
        public bool Infinite;

        public bool Empty => !Infinite && Charges <= 0;
    }

    [SerializeField] private Equip_AbilityStack[] startingAbilities;

    [Header("Debug")]
    [Tooltip("Added by the Add Debug Ability button, with its charges.")]
    [SerializeField] private Equip_AbilityStack debugAbility;

    private readonly List<Entry> entries = new List<Entry>(8);
    private Equip_Loadout loadout;
    private int selected = -1;
    private bool ticking;

    // Abilities were added or removed, or charges changed
    public event Action Changed;

    // A different ability was selected (or none)
    public event Action SelectionChanged;

    // The ability at this index just went off (infinite ones too), for the HUD's use punch
    public event Action<int> Used;

    public int Count => entries.Count;
    public int SelectedIndex => selected;
    public Equip_Ability SelectedAbility => selected >= 0 ? entries[selected].Item : null;

    public Equip_Ability AbilityAt(int index) => entries[index].Item;
    public int ChargesAt(int index) => entries[index].Charges;
    public bool IsInfinite(int index) => entries[index].Infinite;

    // Used up, waiting to finish what it started before it leaves the belt
    public bool IsEmpty(int index) => entries[index].Empty;

    private void Awake()
    {
        loadout = GetComponent<Equip_Loadout>();
    }

    private void Start()
    {
        if (startingAbilities == null)
            return;

        for (int i = 0; i < startingAbilities.Length; i++)
            Add(startingAbilities[i]);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].Item.Used -= OnUsed;

            if (entries[i].Item != null)
                loadout.Despawn(entries[i].Item);
        }

        entries.Clear();
    }

    public void Add(in Equip_AbilityStack stack) => Add(stack.ability, stack.charges, stack.infinite);

    // Adds charges of an ability: onto it if it's already on the belt, otherwise as a new one at the end. The first
    // ability on an empty belt is selected.
    public void Add(Equip_Ability prefab, int charges, bool infinite)
    {
        if (prefab == null || (!infinite && charges <= 0))
            return;

        int index = IndexOf(prefab);

        if (index >= 0)
        {
            Entry existing = entries[index];
            existing.Infinite |= infinite;

            if (!existing.Infinite)
                existing.Charges += charges;

            Changed?.Invoke();
            return;
        }

        Equip_Ability item = (Equip_Ability)loadout.Spawn(prefab);
        item.Used += OnUsed;
        entries.Add(new Entry { Prefab = prefab, Item = item, Charges = charges, Infinite = infinite });
        item.NotifyEquipped();

        Changed?.Invoke();

        if (selected < 0)
            Select(entries.Count - 1);
    }

    // Takes an ability off the belt (once it's finished what it started)
    public void Remove(Equip_Ability prefab)
    {
        int index = IndexOf(prefab);

        if (index < 0)
            return;

        entries[index].Charges = 0;
        entries[index].Infinite = false;

        if (!ticking)
            RemoveFinished();
    }

    public void Select(int index)
    {
        if (index < 0 || index >= entries.Count || index == selected || entries[index].Empty)
            return;

        if (selected >= 0)
            entries[selected].Item.SetSelected(false);

        selected = index;
        entries[selected].Item.SetSelected(true);

        SelectionChanged?.Invoke();
        loadout.NotifySlotChanged(Equip_Slot.Ability);
    }

    // The next ability that still has charges, wrapping round
    public void Cycle()
    {
        int count = entries.Count;

        for (int step = 1; step <= count; step++)
        {
            int index = ((selected < 0 ? -1 : selected) + step + count) % count;

            if (index != selected && !entries[index].Empty)
            {
                Select(index);
                return;
            }
        }
    }

    // Called by Equip_Loadout every tick, with the ability button's state
    internal void Tick(in Weapon_AimData aim, float deltaTime, bool held, bool pressed, bool cyclePressed)
    {
        if (cyclePressed)
            Cycle();

        ticking = true;

        try
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];

                // Only the selected one hears the button, and not once it's used up
                bool live = i == selected && !entry.Empty;
                bool entryHeld = live && held;
                bool entryPressed = live && pressed;
                bool active = Equip_Loadout.IsActive(entry.Item.trigger, entryHeld, entryPressed);

                entry.Item.Tick(new Equip_Context(aim, deltaTime, entryHeld, entryPressed, active));
                entry.Item.TickParts(deltaTime);
            }
        }
        finally
        {
            ticking = false;
        }

        RemoveFinished();
    }

    internal void RebuildStats(List<Equip_ModifierEntry> modifiers)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            Equip_Ability item = entries[i].Item;
            item.Stats.Rebuild(item, modifiers);
            item.OnStatsChanged();
        }
    }

    private void OnUsed(Equip_Ability ability)
    {
        int index = IndexOfItem(ability);

        if (index < 0)
            return;

        // The entry stays at this index until RemoveFinished, after this tick, so the HUD can still punch it
        if (!entries[index].Infinite)
        {
            entries[index].Charges = Mathf.Max(0, entries[index].Charges - 1);
            Changed?.Invoke();
        }

        Used?.Invoke(index);
    }

    // Used-up abilities leave once they've finished; if the selected one left, the next one is selected
    private void RemoveFinished()
    {
        bool removed = false;
        int removedSelected = -1;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry entry = entries[i];

            if (!entry.Empty || entry.Item.Busy)
                continue;

            if (i == selected)
            {
                entry.Item.SetSelected(false);
                removedSelected = i;
                selected = -1;
            }
            else if (i < selected)
            {
                selected--;
            }

            if (removedSelected > i)
                removedSelected--;

            entry.Item.Used -= OnUsed;
            entries.RemoveAt(i);
            loadout.Despawn(entry.Item);
            removed = true;
        }

        if (!removed)
            return;

        Changed?.Invoke();

        if (removedSelected < 0)
            return;

        // The one that took the removed one's place, or the next after it that still has charges
        int count = entries.Count;

        for (int step = 0; step < count; step++)
        {
            int index = (removedSelected + step) % count;

            if (!entries[index].Empty)
            {
                Select(index);
                return;
            }
        }

        // Nothing left to select
        SelectionChanged?.Invoke();
        loadout.NotifySlotChanged(Equip_Slot.Ability);
    }

    private int IndexOf(Equip_Ability prefab)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Prefab == prefab)
                return i;
        }

        return -1;
    }

    private int IndexOfItem(Equip_Ability item)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Item == item)
                return i;
        }

        return -1;
    }

    [InspectorButton("Cycle", true)]
    private void DebugCycle() => Cycle();

    [InspectorButton("Add Debug Ability", true)]
    private void DebugAdd() => Add(debugAbility);

    [InspectorButton("Remove Selected", true)]
    private void DebugRemoveSelected()
    {
        if (SelectedAbility != null)
            Remove(entries[selected].Prefab);
    }
}
