using UnityEngine;

// Convenience base for MonoBehaviours that register with a Core_RegistryManager.
// Subclasses decide when to Register/Unregister (usually OnEnable/OnDisable).
public abstract class Core_RegistrableBehaviour : MonoBehaviour, Core_IRegistrable
{
    public int RegistryIndex { get; set; } = Core_Registry.Unregistered;
    public bool IsRegistered => RegistryIndex != Core_Registry.Unregistered;
}
