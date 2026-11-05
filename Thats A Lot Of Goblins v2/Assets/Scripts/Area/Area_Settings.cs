using UnityEngine;

// The rules for one area, in one asset: its name, which waves it runs, and its health rules. Made for you by
// Goblins > Area > New Area, or by the Create button on an Area_Root. Assign it on the scene's Area_Root.
//
// GC: none. Read once when the scene starts.
[CreateAssetMenu(menuName = "Goblins/Area/Area Settings", fileName = "Area Settings")]
public class Area_Settings : ScriptableObject
{
    [Tooltip("Shown on the Area Cleared banner. Empty uses the asset's name.")]
    public string areaName;

    [Tooltip("The waves this area runs. The area is cleared when Last Wave (on this asset) is cleared.")]
    public Wave_Settings waves;

    [Header("Health")]
    [Tooltip("Goblins that can reach their goal hole before the run fails. 0 for no limit. Only the first area's counts " +
             "once there are several: health carries over between areas.")]
    [Min(0)] public int startingHealth = 20;

    [Tooltip("The share of lost health that comes back when this area is cleared, rounded up. 0.5 is half.")]
    [Range(0f, 1f)] public float healthBack = 0.5f;

    [Header("Results")]
    [Tooltip("Shown when the run fails here, one picked at random. {0} is the goblins that got away, e.g. \"{0} goblins got " +
             "into the bunker and ate the general.\" Empty uses the results screen's own.")]
    [TextArea] public string[] failMessages;

    public string DisplayName => string.IsNullOrEmpty(areaName) ? name : areaName;
}
