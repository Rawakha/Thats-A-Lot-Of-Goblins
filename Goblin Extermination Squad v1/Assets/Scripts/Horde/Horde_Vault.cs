using System;
using UnityEngine;

// Where goblins get to: any goblin walking into this trigger is taken back to the pool, like v1's Vault. Put it on the
// goal with a trigger collider (a box or sphere with Is Trigger on), usually next to the Flow_Goal they walk to.
// Reached fires for each one, for the run loop to take a life later.
//
// GC: none. No subscriptions: Unity calls OnTriggerEnter during physics.
[RequireComponent(typeof(Collider))]
public class Horde_Vault : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField, ReadOnly] private int reached;

    // A goblin made it, just before it's returned to the pool
    public event Action<Horde_Goblin> Reached;

    public int ReachedCount => reached;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        if (!GetComponent<Collider>().isTrigger)
            Debug.LogWarning($"{name}: Horde_Vault's collider isn't a trigger, so goblins bump into it instead of " +
                             "arriving.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        Horde_Goblin goblin = other.attachedRigidbody != null
            ? other.attachedRigidbody.GetComponent<Horde_Goblin>()
            : other.GetComponentInParent<Horde_Goblin>();

        if (goblin == null || !goblin.IsActive || Horde_System.Instance == null)
            return;

        reached++;
        Reached?.Invoke(goblin);
        Horde_System.Instance.Despawn(goblin);
    }
}
