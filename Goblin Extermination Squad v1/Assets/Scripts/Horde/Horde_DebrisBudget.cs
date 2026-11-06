using UnityEngine;

// How much carnage the battlefield keeps, in one asset. Corpses and gibs stay until their limit, then the oldest leave
// first (they're the massacre, so crowded spots are kept); splats give way from the most crowded spots (Effect_BloodSplats).
// Packed spots are also thinned: past a limit per square of ground, that square's oldest pieces leave (never fresh ones),
// so one huge blast doesn't use up the budget and push out older fights elsewhere. A Lifetime of 0 means no timer. Quality scales every limit, for lower-end machines and the mobile port.
//
// Setup: Create > Goblins/Debris Budget, saved as Resources/Horde/Debris Budget. Without one, the defaults below are used.
// It's read when play starts, so change it between runs. Horde_System, Horde_Gibs and Effect_BloodSplats all use it.
//
// GC: the default is loaded once, the first time it's needed.
[CreateAssetMenu(menuName = "Goblins/Debris Budget", fileName = "Debris Budget")]
public class Horde_DebrisBudget : ScriptableObject
{
    private const string ResourcePath = "Horde/Debris Budget";

    // While on, corpses and gibs aren't cleaned up or aged, so the wreckage stays as it is (the end of an area)
    public static bool CleanupPaused;

    [Tooltip("Scales every limit below: 0.5 keeps half as much, for lower-end machines.")]
    [Range(0.1f, 2f)] public float quality = 1f;

    [Header("Corpses")]
    [Tooltip("Most dead goblins lying around. Each is still a full pooled goblin, so this is the expensive one; the pool grows by this much at start.")]
    [Min(0)] public int maxCorpses = 300;

    [Tooltip("Seconds a corpse lies there before it shrinks away. 0: until Max Corpses needs the room.")]
    [Min(0f)] public float corpseLifetime;

    [Header("Gibs")]
    [Tooltip("Most flying and settled pieces.")]
    [Min(16)] public int maxGibs = 3000;

    [Tooltip("Seconds a piece stays before it springs away. 0: until Max Gibs needs the room.")]
    [Min(0f)] public float gibLifetime;

    [Header("Splats")]
    [Tooltip("Most blood splats on the ground.")]
    [Min(16)] public int maxSplats = 2048;

    [Tooltip("Seconds a splat stays before it springs away. 0: until Max Splats needs the room.")]
    [Min(0f)] public float splatLifetime;

    [Header("Packed Spots")]
    [Tooltip("Size of the squares crowding is counted in, metres.")]
    [Min(0.25f)] public float crowdCell = 2f;

    [Tooltip("Most pieces on the ground in one square before its oldest leave. 0 turns this off.")]
    [Min(0)] public int maxGibsPerCell = 12;

    [Tooltip("Most corpses in one square before its oldest shrink away. 0 turns this off.")]
    [Min(0)] public int maxCorpsesPerCell = 5;

    [Tooltip("Seconds on the ground before anything can be thinned, so a kill and the fresh pile are always seen.")]
    [Min(0f)] public float thinAfter = 4f;

    [Tooltip("Most removed per second from packed spots, for each of gibs and corpses, so a pile thins out gradually.")]
    [Min(1f)] public float thinPerSecond = 40f;

    public int MaxCorpses => Mathf.RoundToInt(maxCorpses * quality);
    public int MaxGibs => Mathf.Max(16, Mathf.RoundToInt(maxGibs * quality));
    public int MaxSplats => Mathf.Max(16, Mathf.RoundToInt(maxSplats * quality));

    private static Horde_DebrisBudget loaded;

    // The project's budget (Resources/Horde/Debris Budget), or the defaults
    public static Horde_DebrisBudget Current
    {
        get
        {
            if (loaded != null)
                return loaded;

            loaded = Resources.Load<Horde_DebrisBudget>(ResourcePath);

            if (loaded == null)
            {
                loaded = CreateInstance<Horde_DebrisBudget>();
                loaded.hideFlags = HideFlags.HideAndDontSave;
            }

            return loaded;
        }
    }
}
