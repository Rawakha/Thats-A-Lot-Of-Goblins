using System;
using UnityEngine;

// The one thing you set up in an area scene. It sits on the Wave_Director's object and points at the area's
// Area_Settings; the Wave_Director and Run_State read everything from here, so nothing else needs assigning. Its
// inspector shows a checklist of what the area still needs, the settings to tune, and a Play button.
//
// Areas are separate scenes loaded on top of the Run scene (plane, camera, HUD, horde), so anything in the Run scene
// that needs the area listens to Changed (called with the area as it loads, and with null as it unloads) instead of
// holding a reference.
//
// Setup: Goblins > Area > New Area does it all. By hand: add this to the Wave_Director's object and assign or create
// the Area Settings.
//
// GC: none. No subscriptions. Runs early so Current exists when other scripts wake.
[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Wave_Director))]
public class Area_Root : MonoBehaviour
{
    [Tooltip("This area's name, waves and health rules.")]
    [SerializeField] private Area_Settings settings;

    [Tooltip("The area's edges. The checklist can make this.")]
    [SerializeField] private Area_Bounds bounds;

    [Tooltip("Where the plane enters. The checklist can make this.")]
    [SerializeField] private Area_Spawn spawn;

    public static Area_Root Current { get; private set; }

    public Area_Bounds Bounds => bounds;
    public Area_Spawn Spawn => spawn;

    // The area that just loaded, or null when it unloaded
    public static event Action<Area_Root> Changed;

    public Area_Settings Settings => settings;
    public Wave_Director Director { get; private set; }

    private void Awake()
    {
        Current = this;
        Director = GetComponent<Wave_Director>();
        Changed?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (Current != this)
            return;

        Current = null;
        Changed?.Invoke(null);
    }
}
