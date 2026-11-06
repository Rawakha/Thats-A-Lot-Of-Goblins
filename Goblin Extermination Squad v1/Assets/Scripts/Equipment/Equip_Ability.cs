using System;
using UnityEngine;

// The base of every ability (carpet bomb, ice wall, napalm...): what they all share on top of Equip_Item, so the
// belt and the HUD can treat any ability the same way. Abilities live on Equip_AbilityBelt, not in a loadout slot:
// every ability on the belt is spawned and ticked all the time (so cooldowns recover and dropped bombs keep falling
// while it isn't selected), but only the selected one gets the ability button.
//
// A subclass:
//   - calls NotifyUsed() when it actually goes off (not on a press during its cooldown): that costs a belt charge;
//   - reports CooldownProgress (0 just used, 1 ready) for the HUD's icon fill;
//   - reports Busy while something it started must finish before it can be removed (a stick of bombs still
//     dropping), so using the last charge doesn't cut it off;
//   - overrides OnDeselected to stop anything that should end when the player swaps away (a beam fired from the
//     plane). Things already let go of, like falling bombs, just carry on.
//
// GC: none. Used is a plain event the belt listens to.
public abstract class Equip_Ability : Equip_Item
{
    [Header("Ability")]
    [Tooltip("The ability's colour on the HUD: its icon when selected.")]
    public Color color = Color.white;

    public event Action<Equip_Ability> Used;

    public bool Selected { get; private set; }

    // 0 just used, 1 ready: for the HUD's cooldown fill
    public virtual float CooldownProgress => 1f;

    // Still finishing something it started; the belt won't remove it until this is false
    public virtual bool Busy => false;

    protected void NotifyUsed()
    {
        Used?.Invoke(this);
    }

    internal void SetSelected(bool selected)
    {
        if (Selected == selected)
            return;

        Selected = selected;

        if (selected)
            OnSelected();
        else
            OnDeselected();
    }

    protected virtual void OnSelected() { }

    // Stop anything that ends when the player swaps away
    protected virtual void OnDeselected() { }

    protected override void Reset()
    {
        slot = Equip_Slot.Ability;
        trigger = Equip_Trigger.Press;
    }
}
