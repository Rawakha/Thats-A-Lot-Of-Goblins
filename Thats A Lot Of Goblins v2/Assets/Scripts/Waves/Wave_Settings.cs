using System.Text;
using UnityEngine;

// Everything about the waves, in one asset. Each wave:
//   Goblins: First Wave Goblins, plus Extra Per Wave for each wave after, grown by Growth Per Wave (compounding), up
//     to Max Per Wave.
//   Holes: some of the scene's holes spawn, each sending its share of the wave to a randomly picked other hole. How
//     many is picked at random between Min Holes and the wave's max, which starts at Min Holes and goes up by one
//     every Waves Per Extra Hole waves (1 to 5: one hole, 6 to 10: one or two, ...), never more than Max Holes.
//   Hop Out: goblins hop out of the hole's bottom along a scripted arc to a spot round the rim (some flipping), and
//     land with a squish. Scripted, not physical: they can't get stuck in the pit.
// The Preview below shows what the first waves come out as; it updates as you edit.
[CreateAssetMenu(menuName = "Goblins/Waves/Wave Settings", fileName = "Wave_Settings")]
public class Wave_Settings : ScriptableObject
{
    private const int PreviewWaves = 20;

    [Header("Goblins")]
    [Tooltip("The goblin every wave spawns.")]
    public Horde_GoblinType goblinType;

    [Tooltip("Goblins in wave 1.")]
    [Min(1)] public int firstWaveGoblins = 10;

    [Tooltip("Goblins added for each wave after the first.")]
    [Min(0f)] public float extraGoblinsPerWave = 5f;

    [Tooltip("On top of that, each wave is this much bigger than the last (0.05 = 5%, compounding). 0 keeps growth steady.")]
    [Range(0f, 0.5f)] public float growthPerWave = 0.05f;

    [Tooltip("Most goblins in one wave. 0 for no limit.")]
    [Min(0)] public int maxGoblinsPerWave = 0;

    [Header("Holes")]
    [Tooltip("The fewest holes a wave spawns from.")]
    [Min(1)] public int minHoles = 1;

    [Tooltip("Every this many waves, one more hole may be used (a max, not a must: each wave picks between Min Holes " +
             "and the max at random).")]
    [Min(1)] public int wavesPerExtraHole = 5;

    [Tooltip("Most holes a wave can spawn from. 0 for no limit beyond the holes in the scene.")]
    [Min(0)] public int maxHoles = 0;

    [Tooltip("A hole's goblins go to a hole at least this far away, metres, picked at random among those. If none is " +
             "that far, they go to the furthest one.")]
    [Min(0f)] public float minTargetDistance = 30f;

    [Header("Waves")]
    [Tooltip("The last wave: clearing it clears the area. 0 keeps going for ever, so the area never clears.")]
    [Min(0)] public int lastWave = 0;

    [Header("Timing")]
    [Tooltip("Each wave (wave 1 included) waits for the player to press Next Wave (Space). Off: they follow on by themselves.")]
    public bool waitForInput = true;

    [Tooltip("Seconds before wave 1 (or before Next Wave works for it).")]
    [Min(0f)] public float firstWaveDelay = 1f;

    [Tooltip("Seconds after a wave is cleared before the next starts, or before Next Wave works for it.")]
    [Min(0f)] public float timeBetweenWaves = 1f;

    [Tooltip("Seconds between goblins coming out of one hole.")]
    [Min(0.01f)] public float spawnInterval = 0.12f;

    [Header("Hop Out")]
    [Tooltip("How far from the hole's edge goblins land, metres, picked between these.")]
    public Vector2 hopDistance = new Vector2(1f, 4f);

    [Tooltip("Height of the hop above the higher of its two ends, metres, picked between these. It starts down in the " +
             "pit, so give it enough to clear the rim.")]
    public Vector2 hopHeight = new Vector2(2.5f, 3.5f);

    [Tooltip("Seconds the hop takes, picked between these.")]
    public Vector2 hopDuration = new Vector2(0.45f, 0.65f);

    [Tooltip("How it moves along the hop (DOTween ease of its progress). Linear is a natural throw; OutQuad pops out " +
             "fast and floats down.")]
    public DG.Tweening.Ease hopEase = DG.Tweening.Ease.Linear;

    [Tooltip("How much the landing spots lean toward the target hole rather than all round. 0 all round, 1 straight at it.")]
    [Range(0f, 1f)] public float hopTowardTarget = 0.3f;

    [Tooltip("Chance a goblin does a full flip on the way out.")]
    [Range(0f, 1f)] public float flipChance = 0.25f;

    [Tooltip("Degrees of a flip: 360 a forward flip, -360 a backflip, 720 a double.")]
    public float flipDegrees = 360f;

    [Header("Preview")]
    [Tooltip("What the first waves come out as (holes are capped by how many are in the scene at play).")]
    [ReadOnly, TextArea(6, 22)] public string preview;

    // Goblins in a wave (1 is the first)
    public int GoblinsForWave(int wave)
    {
        int steps = Mathf.Max(0, wave - 1);
        float count = (firstWaveGoblins + extraGoblinsPerWave * steps) * Mathf.Pow(1f + growthPerWave, steps);
        int goblins = Mathf.Max(1, Mathf.RoundToInt(count));
        return maxGoblinsPerWave > 0 ? Mathf.Min(goblins, maxGoblinsPerWave) : goblins;
    }

    // Most holes a wave may spawn from (before capping to the holes in the scene)
    public int MaxHolesForWave(int wave)
    {
        int holes = minHoles + Mathf.Max(0, wave - 1) / wavesPerExtraHole;
        return maxHoles > 0 ? Mathf.Clamp(holes, minHoles, Mathf.Max(minHoles, maxHoles)) : holes;
    }

    public bool IsLastWave(int wave) => lastWave > 0 && wave >= lastWave;

    private void OnValidate()
    {
        StringBuilder text = new StringBuilder();
        int waves = lastWave > 0 ? Mathf.Min(lastWave, PreviewWaves) : PreviewWaves;

        for (int wave = 1; wave <= waves; wave++)
        {
            int max = MaxHolesForWave(wave);
            string holes = max == minHoles ? $"{minHoles} hole{(minHoles == 1 ? "" : "s")}" : $"{minHoles}-{max} holes";
            text.Append($"Wave {wave,2}:  {GoblinsForWave(wave),4} goblins,  {holes}\n");
        }

        if (lastWave == 0)
            text.Append("... and on for ever");

        preview = text.ToString();
    }
}
