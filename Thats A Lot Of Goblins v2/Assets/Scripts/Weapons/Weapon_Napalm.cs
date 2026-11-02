using UnityEngine;

// Napalm (not built yet): will set the ground below the plane on fire. For now it does nothing but use a charge on
// press (and log it), so the ability belt, swapping and the HUD can be tested.
//
// Setup: an empty GameObject with this on it, saved as a prefab; set its Display Name and Color.
//
// GC: none. No subscription: ticked by Equip_AbilityBelt.
public class Weapon_Napalm : Equip_Ability
{
    public override void Tick(in Equip_Context context)
    {
        if (!context.Active)
            return;

        Debug.Log($"{DisplayName} used (not built yet).", this);
        NotifyUsed();
    }
}
