using UnityEngine;

// A flow field leading to this object. On enable it takes a field from Flow_System with this position as its goal,
// and if Follow is on it moves the goal when the object moves into another cell (re-solving at most every Min
// Interval). Use it for fixed targets (a nest, a base) and as a test goal: select it to see its field.
// In edit mode, Flow_System's Preview Fields solves every Flow_Goal in the scene for the gizmos.
//
// GC: none per frame. One UpdateManager subscription (OnUpdate15hz) while Follow is on, to notice moves.
public class Flow_Goal : MonoBehaviour
{
    [Tooltip("Shown in Flow_System's stats.")]
    [SerializeField] private string fieldName = "Goal";

    [Tooltip("Move the goal with this object. Off for fixed targets.")]
    [SerializeField] private bool follow = true;

    [Tooltip("Least time between re-solves while it moves, seconds.")]
    [SerializeField, Min(0f)] private float minInterval = 0.25f;

    private Flow_FieldHandle field;
    private int lastCell = -1;
    private bool subscribed;
    private bool started;

    public Flow_FieldHandle Field => field;

    // Flow_System sets up in Awake, so the first field is taken in Start; later re-enables take it straight away
    private void Start()
    {
        started = true;
        Begin();
    }

    private void OnEnable()
    {
        if (started)
            Begin();
    }

    private void OnDisable()
    {
        if (subscribed && UpdateManager.Instance != null)
            UpdateManager.Instance.OnUpdate15hz -= Check;

        subscribed = false;

        if (Flow_System.Instance != null)
            Flow_System.Instance.Release(field);

        field = Flow_FieldHandle.Invalid;
    }

    private void Begin()
    {
        Flow_System flow = Flow_System.Instance;

        if (flow == null || !flow.IsReady)
        {
            Debug.LogWarning($"{name}: no ready Flow_System, so no field.", this);
            return;
        }

        field = flow.RequestField(string.IsNullOrEmpty(fieldName) ? name : fieldName, minInterval);
        lastCell = flow.Grid.CellIndex(transform.position);
        flow.SetGoal(field, transform.position);

        if (follow && UpdateManager.Instance != null && !subscribed)
        {
            UpdateManager.Instance.OnUpdate15hz += Check;
            subscribed = true;
        }
    }

    private void Check()
    {
        Flow_System flow = Flow_System.Instance;

        if (flow == null)
            return;

        int cell = flow.Grid.CellIndex(transform.position);

        if (cell == lastCell)
            return;

        lastCell = cell;
        flow.SetGoal(field, transform.position);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }

    private void OnDrawGizmosSelected()
    {
        Flow_System flow = Flow_System.Instance != null ? Flow_System.Instance : FindAnyObjectByType<Flow_System>();

        if (flow != null)
            flow.DrawGizmos(this);
    }
#endif
}
