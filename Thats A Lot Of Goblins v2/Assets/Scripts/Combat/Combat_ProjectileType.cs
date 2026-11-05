using UnityEngine;

// A projectile prefab. Put this on the root, with the visuals (mesh, trail, particles) as children pointing along +Z.
// Weapons reference the prefab; Combat_Projectiles pools copies of it and flies them, pointed along their velocity.
// The root's scale is the shot's size in the world, so scale the prefab, not the code.
// It must not move or destroy itself: no Rigidbody, no colliders, no projectile or self-destroy scripts from effect
// packs (Effect_Pool warns on first use if it finds any).
// Speed, damage and range come from the firing weapon's stats. Bullets have no gravity; give it gravity and the
// same system drops bombs.
[DisallowMultipleComponent]
public class Combat_ProjectileType : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Seconds the shot stays after an impact so its trail and particles can fade. 0 removes it at once.")]
    [Min(0f)] public float linger = 0.3f;

    [Header("Impact")]
    [Tooltip("Hit effect prefab spawned on the ground and scenery.")]
    public Effect_Hit surfaceImpact;

    [Tooltip("Hit effect prefab spawned on things that take damage. Empty uses the surface impact.")]
    public Effect_Hit damageableImpact;

    [Tooltip("Size of the impact effects, as a multiple of the prefab's own size. 1 is as made.")]
    [Min(0.01f)] public float impactScale = 1f;

    [Tooltip("Played on impact. Limited per frame by Combat_Projectiles, so a stream of bullets doesn't stack up.")]
    public SoundData impactSound;

    [Header("Flight")]
    public LayerMask hitLayers = ~0;

    [Tooltip("How fat the shot is for hitting things, metres: it hits anything it passes within this distance of. " +
             "Forgiving aim without looking like it. 0 is a thin ray.")]
    [Min(0f)] public float hitRadius = 0.35f;

    [Tooltip("Pull downward, units/second². 0 for bullets.")]
    [Min(0f)] public float gravity;

    [Header("Explosion (bombs)")]
    [Tooltip("Blows up on impact: everything within the shot's Radius (the weapon's stat) is blown apart, and out to " +
             "Radius x Fling Radius Scale is damaged and flung. Off: an ordinary hit.")]
    public bool explodes;

    [Tooltip("How far past the blast radius things are flung, as a multiple of it.")]
    [Min(1f)] public float flingRadiusScale = 2.5f;

    [Tooltip("Sideways throw at the blast's edge of the kill radius, m/s. Weaker further out.")]
    [Min(0f)] public float flingOut = 7f;

    [Tooltip("Upward throw at the edge of the kill radius, m/s. Weaker further out.")]
    [Min(0f)] public float flingUp = 11f;

    [Tooltip("Chance (0-1) a goblin in the fling ring dies on the spot, right at the kill radius. The rest fly " +
             "screaming, eyes open; any the blast's damage would kill die when they hit the ground.")]
    [Range(0f, 1f)] public float instantDeathInner = 0.8f;

    [Tooltip("The same chance at the ring's outer edge. It fades from Inner to this across the ring.")]
    [Range(0f, 1f)] public float instantDeathOuter = 0.1f;

    [Tooltip("Camera trauma for the blast, 0-1.")]
    [Range(0f, 1f)] public float explosionTrauma = 0.45f;

    [Header("Gizmos")]
    [Tooltip("Exploding shots: draw the blast round this object while it's selected, the kill radius (red, goblins " +
             "blown apart) and the fling radius (orange, goblins thrown). The kill radius really comes from the firing " +
             "weapon's Radius stat, so set this to match it (the Carpet Bomb's is 3).")]
    [SerializeField] private bool drawBlast = true;
    [SerializeField, Min(0f)] private float previewRadius = 3f;

    private void OnDrawGizmosSelected()
    {
        if (!drawBlast || !explodes)
            return;

        Vector3 centre = transform.position;
        Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
        Debug_Gizmos.Circle(centre, previewRadius);
        Gizmos.color = new Color(1f, 0.65f, 0.15f, 0.8f);
        Debug_Gizmos.Circle(centre, previewRadius * flingRadiusScale);
    }
}
