using UnityEngine;

// A hit effect prefab: sparks, dust, a decal. Put this on the root, with the particles as children. Projectiles (and
// later bombs and crashes) reference the prefab; Effect_Pool plays pooled copies and returns them after Lifetime.
// The root's scale is the effect's size in the world, so scale the prefab, not the code.
// It must not destroy itself: remove self-destroy scripts from effect packs (Effect_Pool warns on first use).
//
// GC: none. No subscription; it's only data.
[DisallowMultipleComponent]
public class Effect_Hit : MonoBehaviour
{
    public enum Facing
    {
        ZAlongNormal,   // +Z points out of the surface
        YAlongNormal,   // +Y points out of the surface
        World           // as authored, ignoring the surface
    }

    [Tooltip("Seconds before it's returned to the pool. Cover the longest particle, or it gets cut off.")]
    [Min(0.05f)] public float lifetime = 1f;

    [Tooltip("Which axis of the prefab points out of the surface it hit. If the effect sprays into the ground or " +
             "sideways, try the other one.")]
    public Facing facing = Facing.ZAlongNormal;

    public void Play(Vector3 point, Vector3 normal)
    {
        if (Effect_Pool.Instance != null)
            Effect_Pool.Instance.Play(gameObject, point, Rotation(normal), lifetime);
    }

    public Quaternion Rotation(Vector3 normal)
    {
        switch (facing)
        {
            case Facing.ZAlongNormal: return Core_Math.LookRotation(normal, Vector3.forward);
            case Facing.YAlongNormal: return Quaternion.FromToRotation(Vector3.up, normal);
            default: return transform.rotation;
        }
    }
}
