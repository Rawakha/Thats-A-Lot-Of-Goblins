using UnityEngine;

// A small on-screen readout of carnage against its budget (corpses, gibs, splats), for tuning Horde_DebrisBudget while
// profiling. Editor and development builds only.
//
// Setup: put this anywhere in the scene. Untick the component to hide it.
//
// GC: builds its text every OnGUI (debug only, compiled out of release builds). No subscriptions.
public class Horde_DebrisReadout : MonoBehaviour
{
    [SerializeField] private Vector2 position = new Vector2(10f, 10f);

#if UNITY_EDITOR || DEBUG
    private void OnGUI()
    {
        Horde_DebrisBudget budget = Horde_DebrisBudget.Current;
        int corpses = Horde_System.Instance != null ? Horde_System.Instance.CorpseCount : 0;
        int gibs = Horde_Gibs.Instance != null ? Horde_Gibs.Instance.Count : 0;
        int splats = Effect_BloodSplats.Instance != null ? Effect_BloodSplats.Instance.Count : 0;

        GUI.Label(new Rect(position.x, position.y, 300f, 70f),
            $"Corpses {corpses} / {budget.MaxCorpses}\nGibs {gibs} / {budget.MaxGibs}\nSplats {splats} / {budget.MaxSplats}");
    }
#endif
}
