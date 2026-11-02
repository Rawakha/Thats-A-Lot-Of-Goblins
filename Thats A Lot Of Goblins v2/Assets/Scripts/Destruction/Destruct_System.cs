using UnityEngine;

// Runs all destruction from one place.
//   Cracking: pieces whose shards are moving register here and are ticked each frame; they drop out once settled.
//   Debris: every shard knocked off or shattered becomes debris owned by this system. It flies with physics, is
//     frozen once it comes to rest, and after its lifetime shrinks away and is switched off (never destroyed).
//     There's a budget: past Max Debris the oldest piece is switched off at once, so a big collapse can't flood
//     physics. Lower it on mobile.
//
// Put one in the scene (a "Destruction" object). Debris layer: make a "Debris" layer in Tags and Layers, then in the
// Physics collision matrix untick Debris against the plane, and leave it out of projectiles' Hit Layers so falling
// rubble doesn't eat bullets meant for the building behind it.
//
// GC: none per frame. The debris array is allocated once at Max Debris. One UpdateManager subscription (OnUpdate).
[DefaultExecutionOrder(Utilities.ExecutionOrder.DependantSingletons)]
public class Destruct_System : Core_RegistryManager<Destruct_Piece>
{
    private enum State : byte
    {
        Flying,
        Resting,
        Shrinking
    }

    private struct Debris
    {
        public Transform Transform;
        public Rigidbody Body;
        public Vector3 Scale;
        public float SpawnTime;
        public float StateTime;     // when the current state began
        public State State;
    }

    [Header("Debris Budget")]
    [Tooltip("Most debris alive at once. Past this the oldest is removed straight away.")]
    [SerializeField, Min(1)] private int maxDebris = 200;

    [Tooltip("Seconds debris stays before shrinking away.")]
    [SerializeField, Min(0f)] private float debrisLifetime = 8f;

    [Tooltip("Seconds it takes to shrink away.")]
    [SerializeField, Min(0.01f)] private float shrinkDuration = 1f;

    [Tooltip("Once a piece of debris comes to rest it's frozen (kinematic) so it stops costing physics. It still " +
             "blocks other debris.")]
    [SerializeField] private bool freezeWhenResting = true;

    [Header("Layer")]
    [Tooltip("Layer debris is moved to. Empty or missing keeps the shard's own layer.")]
    [SerializeField] private string debrisLayer = "Debris";

    private Debris[] debris;
    private int debrisCount;
    private int layer = -1;

    public static Destruct_System Instance { get; private set; }

    public int DebrisCount => debrisCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"{name}: A second Destruct_System was found, this one is disabled.", this);
            enabled = false;
            return;
        }

        Instance = this;
        debris = new Debris[maxDebris];

        if (!string.IsNullOrEmpty(debrisLayer))
        {
            layer = LayerMask.NameToLayer(debrisLayer);

            if (layer < 0)
                Debug.LogWarning($"{name}: No layer called '{debrisLayer}'. Debris keeps the building's layer, so it " +
                                 "can block bullets and hit the plane.", this);
        }
    }

    private void OnEnable()
    {
        if (UpdateManager.Instance == null)
        {
            Debug.LogError($"{name}: No UpdateManager in the scene, destruction won't animate.", this);
            return;
        }

        UpdateManager.Instance.OnUpdate += Tick;
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnUpdate -= Tick;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (Instance == this)
            Instance = null;
    }

    // Takes over a shard: unparents it, turns on its physics and launches it. Spin is radians/second.
    public void AddDebris(Transform shard, Rigidbody body, Vector3 velocity, Vector3 spin, float depenetrationSpeed)
    {
        if (debrisCount >= debris.Length)
            Retire(Oldest());

        shard.SetParent(null, true);

        if (layer >= 0)
            shard.gameObject.layer = layer;

        if (body != null)
        {
            body.isKinematic = false;
            body.detectCollisions = true;
            body.maxDepenetrationVelocity = depenetrationSpeed;
            body.linearVelocity = velocity;
            body.angularVelocity = spin;
        }

        float time = Time.time;

        debris[debrisCount++] = new Debris
        {
            Transform = shard,
            Body = body,
            Scale = shard.localScale,
            SpawnTime = time,
            StateTime = time,
            State = State.Flying
        };
    }

    private void Tick()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        TickPieces(deltaTime);
        TickDebris();
    }

    private void TickPieces(float deltaTime)
    {
        BeginIteration();

        try
        {
            for (int i = 0; i < Count; i++)
            {
                Destruct_Piece piece = items[i];

                if (!piece.Tick(deltaTime))
                    Unregister(piece);
            }
        }
        finally
        {
            EndIteration();
        }
    }

    // Backwards, so a swap-back removal only moves an already-handled entry into this index
    private void TickDebris()
    {
        float time = Time.time;

        for (int i = debrisCount - 1; i >= 0; i--)
        {
            ref Debris piece = ref debris[i];

            if (piece.Transform == null)
            {
                RemoveAt(i);
                continue;
            }

            switch (piece.State)
            {
                case State.Flying:
                    if (freezeWhenResting && piece.Body != null && piece.Body.IsSleeping())
                    {
                        piece.Body.isKinematic = true;
                        piece.State = State.Resting;
                    }

                    break;
            }

            if (piece.State != State.Shrinking && time - piece.SpawnTime >= debrisLifetime)
            {
                piece.State = State.Shrinking;
                piece.StateTime = time;

                // Physics off so it doesn't jitter as it shrinks into its neighbours
                if (piece.Body != null)
                {
                    piece.Body.isKinematic = true;
                    piece.Body.detectCollisions = false;
                }
            }

            if (piece.State == State.Shrinking)
            {
                float shrink = (time - piece.StateTime) / shrinkDuration;

                if (shrink >= 1f)
                {
                    Retire(i);
                    continue;
                }

                piece.Transform.localScale = piece.Scale * (1f - shrink);
            }
        }
    }

    // Switches it off and forgets it
    private void Retire(int index)
    {
        Transform shard = debris[index].Transform;

        if (shard != null)
            shard.gameObject.SetActive(false);

        RemoveAt(index);
    }

    private void RemoveAt(int index)
    {
        int last = debrisCount - 1;
        debris[index] = debris[last];
        debris[last] = default;
        debrisCount--;
    }

    // Only scanned when the budget is full
    private int Oldest()
    {
        int oldest = 0;

        for (int i = 1; i < debrisCount; i++)
        {
            if (debris[i].SpawnTime < debris[oldest].SpawnTime)
                oldest = i;
        }

        return oldest;
    }
}
