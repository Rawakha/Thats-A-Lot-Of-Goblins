using UnityEngine;

// One shared swap-back registry. Horde_System's existing fixed loop drives batched stamps and one texture
// upload per dirty ground. No UpdateManager subscriptions, per-goblin objects, or steady-state managed allocation.
public class Level_GrassWearSystem : Core_RegistryManager<Level_GrassWear>
{
    public static Level_GrassWearSystem Current { get; private set; }
    public static int Revision { get; private set; }
    private static int serial, stride = 1;
    public static Level_GrassWearSystem GetOrCreate()
    {
        if (Current == null) Current = new GameObject("Grass Wear Registry").AddComponent<Level_GrassWearSystem>();
        return Current;
    }
    protected override void OnDestroy() { if (Current == this) Current = null; base.OnDestroy(); }
    public static bool BeginStep(int goblins, float now)
    {
        if (Current == null || Current.Count == 0) return false;
        bool any = false;
        stride = Mathf.Max(1,(goblins+2047)/2048);
        for (int i = 0; i < Current.Count; i++) any |= Current[i].BeginTick(now);
        if (any) serial++;
        return any;
    }
    public static void Stamp(int index, Vector3 from, Vector3 to)
    {
        if ((index+serial)%stride != 0 || Current == null) return;
        for (int i = 0; i < Current.Count; i++) Current[i].Stamp(from,to);
    }
    public static bool ShouldSample(int index) => (index+serial)%stride == 0;
    public static void EndStep()
    {
        if (Current == null) return;
        bool changed = false;
        for (int i = 0; i < Current.Count; i++) changed |= Current[i].Upload();
        if (changed) Revision++;
    }
    public static void MarkChanged() => Revision++;
}
