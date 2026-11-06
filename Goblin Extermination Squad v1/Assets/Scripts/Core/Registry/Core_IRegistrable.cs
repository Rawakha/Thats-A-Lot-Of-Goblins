// Implemented by anything stored in a Core_RegistryManager.
// The registry owns RegistryIndex; never set it yourself. One index means one registry per object.
public interface Core_IRegistrable
{
    int RegistryIndex { get; set; }
}

public static class Core_Registry
{
    public const int Unregistered = -1;
}
