using UnityEngine;

// Spent shell casings flicked out of a gun every shot: a particle system that's told to emit on each of its muzzle's
// shots, so the casings stream out at exactly the fire rate. The particle system does the rest (the flick, the spin,
// falling, bouncing off the ground), all set up in its own modules.
//
// Setup: in the weapon prefab, add an empty at the gun's ejection port (beside the barrel, not at its tip), pointing
// the way the casings should fly (its blue Z arrow out to the side and a bit up). Put a Particle System and this on it,
// and drag the barrel's Weapon_Muzzle into Muzzle. One per barrel; set the particle system up as described in the
// handoff notes (Emission rate 0, World space, Collision on, no Play On Awake).
//
// GC: none per shot (ParticleSystem.Emit with a count). One subscription to the muzzle's Fired event while enabled.
[RequireComponent(typeof(ParticleSystem))]
public class Weapon_ShellEjector : MonoBehaviour
{
    [Tooltip("The barrel whose shots throw out casings.")]
    [SerializeField] private Weapon_Muzzle muzzle;

    [Tooltip("Casings per shot.")]
    [SerializeField, Min(1)] private int shellsPerShot = 1;

    [Tooltip("Chance (0-1) a shot throws a casing at all, to thin them out at very high fire rates.")]
    [SerializeField, Range(0f, 1f)] private float chance = 1f;

    private ParticleSystem shells;

    private void Awake()
    {
        shells = GetComponent<ParticleSystem>();
    }

    private void OnEnable()
    {
        if (muzzle != null)
            muzzle.Fired += Eject;
        else
            Debug.LogWarning($"{name}: Weapon_ShellEjector has no Muzzle, so no casings.", this);
    }

    private void OnDisable()
    {
        if (muzzle != null)
            muzzle.Fired -= Eject;
    }

    private void Eject()
    {
        if (chance < 1f && Random.value > chance)
            return;

        shells.Emit(shellsPerShot);
    }
}
