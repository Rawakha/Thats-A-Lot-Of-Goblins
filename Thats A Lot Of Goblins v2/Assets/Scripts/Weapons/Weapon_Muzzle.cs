using System;
using UnityEngine;

// A point a weapon fires from. Put it on an empty at the barrel tip inside the weapon prefab. Shots leave from here
// but follow the shared aim, so a banking wing never throws them sideways.
// The flash is a particle system authored in the prefab (usually a child of this), restarted every shot.
// Fired is the hook for anything that reacts per shot: barrel recoil, shell casings, a light.
//
// GC: none per shot. Fired subscriptions come from the prefab's own parts, added on equip.
public class Weapon_Muzzle : MonoBehaviour
{
    [Tooltip("Played every shot. Leave its Play On Awake and Looping off.")]
    [SerializeField] private ParticleSystem flash;

    public event Action Fired;

    public Vector3 Position => transform.position;

    public void Fire()
    {
        if (flash != null)
        {
            flash.Clear(true);
            flash.Play(true);
        }

        Fired?.Invoke();
    }
}
