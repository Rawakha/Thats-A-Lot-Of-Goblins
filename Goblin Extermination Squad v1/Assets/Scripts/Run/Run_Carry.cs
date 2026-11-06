using UnityEngine;

// What a run keeps when the Run scene reloads for the next area. Leaving an area reloads the Run scene (so the horde,
// debris, effects and pools all start clean, the same as Restart) and this carries across: the health pool, the health
// still to come back, and which area to load. The picks from the shop go here too (stage 3).
//
// Nothing to set up. Restart clears it; Run_State saves and reads it, Run_Boot reads the area.
//
// GC: none.
public static class Run_Carry
{
    public static bool Has;
    public static int Leaks;
    public static int Limit;
    public static int PendingBack;
    public static string AreaPath;

    public static void Clear()
    {
        Has = false;
        Leaks = 0;
        Limit = 0;
        PendingBack = 0;
        AreaPath = null;
    }

    // Domain-reload-free play: start with nothing carried
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Clear();
}
