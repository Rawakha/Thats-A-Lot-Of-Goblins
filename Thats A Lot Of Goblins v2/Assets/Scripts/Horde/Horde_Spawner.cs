using UnityEngine;

// Spawns goblins of one type in a circle and sends them along a Flow_Goal's field. Place as many as you like; each
// can head for a different goal. On Start it fills the type's pool; then it spawns Burst On Start goblins once the
// goal's field exists, and keeps spawning at Rate while Spawning is on. The buttons spawn more by hand in play mode.
//
// GC: none per spawn (goblins come from the pool). One UpdateManager subscription (OnUpdate).
public class Horde_Spawner : MonoBehaviour
{
    [SerializeField] private Horde_GoblinType goblinType;

    [Tooltip("Where they walk to: the field of this Flow_Goal.")]
    [SerializeField] private Flow_Goal target;

    [Tooltip("Goblins appear anywhere in this circle, metres.")]
    [SerializeField, Min(0f)] private float radius = 4f;

    [Header("Spawning")]
    [SerializeField] private bool spawning = true;

    [Tooltip("Goblins per second while Spawning is on.")]
    [SerializeField, Min(0f)] private float rate = 2f;

    [Tooltip("Spawned at once when the goal's field is ready.")]
    [SerializeField, Min(0)] private int burstOnStart = 0;

    [Tooltip("Stops spawning while this many of its goblins are walking. 0 for no limit.")]
    [SerializeField, Min(0)] private int maxAlive = 0;

    [Header("Stats")]
    [SerializeField, ReadOnly] private int spawned;

    private float owed;
    private bool burstDone;
    private bool subscribed;

    private void Start()
    {
        if (goblinType == null || target == null)
        {
            Debug.LogError($"{name}: Horde_Spawner needs a Goblin Type and a Target.", this);
            enabled = false;
            return;
        }

        if (Horde_System.Instance == null)
        {
            Debug.LogError($"{name}: No Horde_System in the scene.", this);
            enabled = false;
            return;
        }

        Horde_System.Instance.Prewarm(goblinType);
        Subscribe();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        if (subscribed && UpdateManager.Instance != null)
            UpdateManager.Instance.OnUpdate -= Tick;

        subscribed = false;
    }

    private void Subscribe()
    {
        // Start does the first subscribe, once the checks have passed
        if (subscribed || goblinType == null || Horde_System.Instance == null || UpdateManager.Instance == null)
            return;

        UpdateManager.Instance.OnUpdate += Tick;
        subscribed = true;
    }

    private void Tick()
    {
        // The goal takes its field in its own Start, which may come after this one's
        if (!FieldReady())
            return;

        if (!burstDone)
        {
            burstDone = true;
            Spawn(burstOnStart);
        }

        if (!spawning || rate <= 0f)
        {
            owed = 0f;
            return;
        }

        owed += rate * Time.deltaTime;
        int count = (int)owed;

        if (count > 0)
        {
            owed -= count;
            Spawn(count);
        }
    }

    [InspectorButton("Spawn 1", true)]
    private void SpawnOne() => Spawn(1);

    [InspectorButton("Spawn 10", true)]
    private void SpawnTen() => Spawn(10);

    [InspectorButton("Spawn 100", true)]
    private void SpawnHundred() => Spawn(100);

    public void Spawn(int count)
    {
        if (!FieldReady())
            return;

        Horde_System horde = Horde_System.Instance;
        // Height comes from Horde_System's Ground Height
        Vector3 centre = transform.position;

        for (int i = 0; i < count; i++)
        {
            if (maxAlive > 0 && AliveFromHere() >= maxAlive)
                break;

            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 position = centre + new Vector3(offset.x, 0f, offset.y);
            Quaternion facing = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            if (horde.Spawn(goblinType, position, facing, target.Field) != null)
                spawned++;
        }
    }

    private bool FieldReady()
    {
        return target != null && Horde_System.Instance != null && Flow_System.Instance != null
            && Flow_System.Instance.IsValid(target.Field);
    }

    // Counted only when Max Alive is set; fine for a handful of spawners
    private int AliveFromHere()
    {
        Horde_System horde = Horde_System.Instance;
        int alive = 0;

        for (int i = 0; i < horde.Count; i++)
        {
            if (horde[i].IsActive && horde[i].Type == goblinType)
                alive++;
        }

        return alive;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.8f);
        DrawCircle(transform.position, radius);

        if (target != null)
        {
            Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.35f);
            Gizmos.DrawLine(transform.position, target.transform.position);
        }
    }

    private static void DrawCircle(Vector3 centre, float circleRadius)
    {
        const int Segments = 32;
        Vector3 previous = centre + new Vector3(circleRadius, 0f, 0f);

        for (int i = 1; i <= Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            Vector3 next = centre + new Vector3(Mathf.Cos(angle) * circleRadius, 0f, Mathf.Sin(angle) * circleRadius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
#endif
}
