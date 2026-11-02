using UnityEngine;

// Pops a part in from nothing with a springy overshoot when its equipment is picked up, so new guns and racks
// appear on the plane with some life. Put it on any visual part in an equipment prefab; stagger Delay across parts
// to have them pop in one after another.
//
// GC: none. Ticked by its item; stops working once it has settled.
public class Equip_PopIn : MonoBehaviour, Equip_IPart
{
    [Tooltip("Seconds after equipping before this part pops in.")]
    [SerializeField, Min(0f)] private float delay;

    [SerializeField, Min(0f)] private float stiffness = 250f;

    [Tooltip("Below 1 overshoots its size and wobbles back. Lower is bouncier.")]
    [SerializeField, Range(0f, 2f)] private float dampingRatio = 0.4f;

    private Vector3 baseScale;
    private Core_SpringFloatState spring;
    private float wait;
    private bool popping;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    public void OnEquipped(Equip_Item item)
    {
        spring = new Core_SpringFloatState(0f);
        wait = delay;
        popping = true;
        transform.localScale = Vector3.zero;
    }

    public void OnUnequipped() { }

    public void Tick(float deltaTime)
    {
        if (!popping)
            return;

        if (wait > 0f)
        {
            wait -= deltaTime;
            return;
        }

        float size = Core_Math.SpringRatio(ref spring, 1f, stiffness, dampingRatio, deltaTime);
        transform.localScale = baseScale * Mathf.Max(size, 0f);

        if (Mathf.Abs(size - 1f) < 0.001f && Mathf.Abs(spring.velocity) < 0.01f)
        {
            transform.localScale = baseScale;
            popping = false;
        }
    }
}
