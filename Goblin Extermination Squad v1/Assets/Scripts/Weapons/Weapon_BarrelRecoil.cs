using UnityEngine;

// Kicks a gun part back every time its muzzle fires, then springs it home. Put it on the barrel (or the whole gun)
// inside a weapon prefab and point it at the muzzle that drives it.
//
// GC: none. One Fired subscription on equip, removed on unequip. Ticked by its item.
public class Weapon_BarrelRecoil : MonoBehaviour, Equip_IPart
{
    [SerializeField] private Weapon_Muzzle muzzle;

    [Tooltip("Kick per shot in this part's own axes, units/second. -Z pushes it back down the barrel.")]
    [SerializeField] private Vector3 kick = new Vector3(0f, 0f, -6f);

    [Tooltip("Furthest the part can be pushed from its rest position, so sustained fire doesn't walk it away.")]
    [SerializeField, Min(0f)] private float maxOffset = 0.25f;

    [SerializeField, Min(0f)] private float stiffness = 400f;
    [SerializeField, Range(0f, 2f)] private float dampingRatio = 0.5f;

    private Vector3 restPosition;
    private Core_SpringVector3State spring;
    private bool moving;

    public void OnEquipped(Equip_Item item)
    {
        restPosition = transform.localPosition;
        spring = new Core_SpringVector3State(Vector3.zero);

        if (muzzle != null)
            muzzle.Fired += OnFired;
    }

    public void OnUnequipped()
    {
        if (muzzle != null)
            muzzle.Fired -= OnFired;
    }

    public void Tick(float deltaTime)
    {
        if (!moving)
            return;

        Vector3 offset = Core_Math.SpringRatio(ref spring, Vector3.zero, stiffness, dampingRatio, deltaTime);
        offset = Vector3.ClampMagnitude(offset, maxOffset);
        spring.value = offset;
        transform.localPosition = restPosition + offset;

        // Settled: stop ticking until the next shot
        if (offset.sqrMagnitude < 0.000001f && spring.velocity.sqrMagnitude < 0.0001f)
        {
            spring = new Core_SpringVector3State(Vector3.zero);
            transform.localPosition = restPosition;
            moving = false;
        }
    }

    private void OnFired()
    {
        spring.velocity += transform.localRotation * kick;
        moving = true;
    }
}
