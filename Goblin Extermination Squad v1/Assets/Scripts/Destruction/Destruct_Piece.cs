#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering;
#endif
using UnityEngine;

// One breakable piece: a pre-fractured mesh (Destruct_Fracture) with health.
//   Hit: the shards around the hit crack loose and jump, and the nearest few are knocked off as debris.
//   Support: shards rest on the ground or on the shards they share a face with. After chunks come off, anything that
//     no longer has a path down to the ground shudders briefly and drops, staggered so it crumbles rather than
//     falling as one block. If most of the base is shot away, the whole thing gives way.
//   Health gone: every remaining shard flies off along the killing shot, fastest near it, the far side mostly
//     collapsing under gravity, with an optional dust effect, sound and camera shake.
// Before the first hit, bullets hit the piece's own collider. From then on its shards are shown and their colliders
// take the hits, so holes are real and falling debris lands on what's left.
// Knocked-off, fallen and shattered shards are handed to Destruct_System, which owns debris from then on (budget,
// settling, despawn). While shards are moving or waiting to fall, the piece is registered with Destruct_System to be
// ticked; it unregisters once settled, so idle and intact pieces cost nothing per frame.
//
// Setup: GameObject > Goblins > Make Destructible on any object with a MeshFilter (a cube scaled to a house works).
// Later, Destruct_Building groups pieces with a support graph between them; this is the single-piece part of that.
//
// GC: none per hit. Per-shard arrays are built once in Awake. Registers with Destruct_System only while moving.
[DisallowMultipleComponent]
[RequireComponent(typeof(Destruct_Fracture))]
public class Destruct_Piece : Core_RegistrableBehaviour, Combat_IDamageable
{
    // Below this, a crack value counts as settled
    private const float Settled = 0.001f;

    [SerializeField] private Destruct_PieceSettings settings;
    [SerializeField, Min(0.01f)] private float maxHealth = 60f;

    [Header("Debug")]
    [Tooltip("When selected, draw which shards hold which up: green stands on the ground, white rests on others, " +
             "red is about to fall. Links behind the surface are faded.")]
    [SerializeField] private bool drawSupports = true;

    private Destruct_Fracture fracture;
    private Transform root;
    private MeshRenderer intactRenderer;
    private Collider[] intactColliders;

    // Per shard. Null entries are shards that were deleted in the editor or have left as debris.
    private Transform[] shards;
    private Rigidbody[] bodies;
    private Collider[] colliders;
    private Vector3[] centres;
    private Vector3[] tiltAxes;
    private float[] crackTarget;
    private float[] crackCurrent;
    private float[] crackJump;
    private int remaining;

    // Support: who touches whom, who stands on the ground, and who is waiting to fall (time it drops, 0 for none)
    private int[] neighbourStarts;
    private int[] neighbours;
    private bool[] grounded;
    private float[] height01;
    private float[] fallTime;
    private int[] visited;
    private int[] queue;
    private int visitStamp;
    private int groundedCount;

    private float health;
    private bool cracked;

    public float Health => health;
    public float MaxHealth => maxHealth;
    public bool IsShattered { get; private set; }

    private void Awake()
    {
        fracture = GetComponent<Destruct_Fracture>();
        intactRenderer = GetComponent<MeshRenderer>();
        intactColliders = GetComponents<Collider>();
        health = maxHealth;

        if (settings == null || !fracture.IsFractured)
        {
            Debug.LogError($"{name}: Destruct_Piece needs Destruct_PieceSettings and a fractured mesh (press Fracture).", this);
            enabled = false;
            return;
        }

        if (intactColliders.Length == 0)
            Debug.LogWarning($"{name}: Destruct_Piece has no collider of its own, so bullets can't hit it.", this);

        root = fracture.FracturedRoot.transform;

        int count = Mathf.Min(fracture.Shards.Count, fracture.ShardCentres.Count);
        shards = new Transform[count];
        bodies = new Rigidbody[count];
        colliders = new Collider[count];
        centres = new Vector3[count];
        tiltAxes = new Vector3[count];
        crackTarget = new float[count];
        crackCurrent = new float[count];
        crackJump = new float[count];

        for (int i = 0; i < count; i++)
        {
            Transform shard = fracture.Shards[i];

            // Shards hidden with the editor's Remove Shards tools are gone for good
            if (shard == null || !shard.gameObject.activeSelf)
                continue;

            shards[i] = shard;
            bodies[i] = shard.GetComponent<Rigidbody>();
            colliders[i] = shard.GetComponent<Collider>();
            centres[i] = fracture.ShardCentres[i];
            tiltAxes[i] = Random.onUnitSphere;
            remaining++;
        }

        if (remaining > 0 && bodies[FirstShard()] == null)
            Debug.LogWarning($"{name}: the shards have no Rigidbodies; turn on Add Rigidbodies and re-fracture.", this);

        BuildSupport(count);
    }

    private void OnDisable()
    {
        if (IsRegistered && Destruct_System.Instance != null)
            Destruct_System.Instance.Unregister(this);
    }

    public void TakeDamage(in Combat_Hit hit)
    {
        if (!enabled || IsShattered)
            return;

        float damage = Mathf.Max(0f, hit.Damage);

        if (damage <= 0f)
            return;

        health -= damage;

        Vector3 direction = hit.Velocity.sqrMagnitude > 0.0001f ? hit.Velocity.normalized : -hit.Normal;

        if (health <= 0f)
        {
            Shatter(hit.Point, direction);
            return;
        }

        Crack(hit.Point, damage / maxHealth * settings.crackPerDamage);
        BreakOff(hit.Point, direction, settings.chunksPerHit);
    }

    // Works the shards loose around a world point. Amount 1 fully cracks the shards at the point.
    public void Crack(Vector3 point, float amount)
    {
        if (IsShattered || amount <= 0f)
            return;

        BeginCrack();

        for (int i = 0; i < shards.Length; i++)
        {
            if (shards[i] == null)
                continue;

            float distance = Vector3.Distance(root.TransformPoint(centres[i]), point);
            float nearHit = 1f - Mathf.SmoothStep(0f, 1f, distance / settings.crackRadius);
            float added = amount * Mathf.Lerp(settings.crackFill, 1f, nearHit);

            crackTarget[i] = Mathf.Min(1f, crackTarget[i] + added);
            crackJump[i] += added * settings.crackPop;
        }

        Animate();
    }

    // Knocks up to count shards off near a world point, nearest first, then drops whatever that left unsupported.
    // Returns how many came off.
    public int BreakOff(Vector3 point, Vector3 direction, int count)
    {
        if (IsShattered || count <= 0 || Destruct_System.Instance == null)
            return 0;

        BeginCrack();

        int broken = 0;

        while (broken < count)
        {
            int index = NearestShard(point, settings.breakOffRadius);

            if (index < 0)
                break;

            Vector3 velocity = direction * settings.breakOffSpeed + Outward(index) * settings.breakOffPush;
            Detach(index, velocity, Random.insideUnitSphere * settings.shatterSpin * 0.5f);
            broken++;
        }

        if (broken == 0)
            return 0;

        if (settings.breakOffEffect != null)
            settings.breakOffEffect.Play(point, -direction);

        PlaySound(settings.breakOffSound, point);

        if (remaining == 0)
            Finish();
        else
            CheckSupport();

        return broken;
    }

    // Every remaining shard flies off along the shot: fastest near the hit, the far side mostly falling
    public void Shatter(Vector3 point, Vector3 direction)
    {
        if (IsShattered)
            return;

        float furthest = 0.0001f;

        for (int i = 0; i < shards.Length; i++)
        {
            if (shards[i] != null)
                furthest = Mathf.Max(furthest, Vector3.Distance(shards[i].position, point));
        }

        for (int i = 0; i < shards.Length; i++)
        {
            Transform shard = shards[i];

            if (shard == null)
                continue;

            Vector3 away = shard.position - point;
            float distance = away.magnitude;
            away = distance > 0.0001f ? away / distance : direction;

            float near = 1f - Mathf.Clamp01(distance / furthest);
            float falloff = 1f - settings.shatterFalloff * (1f - near);

            Vector3 velocity = direction * (settings.shatterSpeed * falloff)
                + away * (settings.shatterBurst * near * Random.Range(0.5f, 1f))
                + Outward(i) * (settings.shatterOutward * Random.Range(0.5f, 1f))
                + Vector3.up * (settings.shatterUp * near)
                + Random.insideUnitSphere * (settings.shatterSpeed * settings.shatterScatter);

            Detach(i, velocity, Random.insideUnitSphere * settings.shatterSpin);
        }

        if (settings.shatterEffect != null)
            settings.shatterEffect.Play(transform.position, Vector3.up);

        PlaySound(settings.shatterSound, transform.position);
        Camera_Rig.Shake(settings.shatterTrauma);
        Finish();
    }

    // Called by Destruct_System each frame while registered. Returns false once nothing is moving or waiting to fall.
    public bool Tick(float deltaTime)
    {
        if (IsShattered)
            return false;

        float time = Time.time;
        float creep = 1f - Mathf.Exp(-settings.crackSpeed * deltaTime);
        float settle = Mathf.Exp(-settings.popSettle * deltaTime);
        Vector3 meshCentre = fracture.MeshCentre;
        bool moving = false;

        for (int i = 0; i < shards.Length; i++)
        {
            Transform shard = shards[i];

            if (shard == null)
                continue;

            if (fallTime[i] > 0f)
            {
                if (time >= fallTime[i])
                {
                    Drop(i);
                    continue;
                }

                // Still waiting: keep it shuddering
                crackJump[i] = Mathf.Max(crackJump[i], settings.collapseShudder * Random.Range(0.3f, 1f));
                moving = true;
            }

            crackCurrent[i] += (crackTarget[i] - crackCurrent[i]) * creep;
            crackJump[i] *= settle;

            moving |= crackTarget[i] - crackCurrent[i] > Settled || crackJump[i] > Settled;

            float crack = crackCurrent[i] + crackJump[i];
            Vector3 centre = centres[i];

            shard.SetLocalPositionAndRotation(
                centre + (centre - meshCentre) * (crack * settings.crackSpread),
                Quaternion.AngleAxis(crack * settings.crackTilt, tiltAxes[i]));
        }

        if (remaining == 0)
        {
            Finish();
            return false;
        }

        return moving;
    }

    // Walks from the grounded shards through touching shards. Anything not reached has nothing holding it up and is
    // queued to fall; if too little of the base is left, everything is.
    private void CheckSupport()
    {
        if (neighbourStarts == null)
            return;

        visitStamp++;
        int head = 0;
        int tail = 0;
        int groundedLeft = 0;

        for (int i = 0; i < shards.Length; i++)
        {
            if (shards[i] == null || fallTime[i] > 0f || !grounded[i])
                continue;

            visited[i] = visitStamp;
            queue[tail++] = i;
            groundedLeft++;
        }

        while (head < tail)
        {
            int i = queue[head++];

            for (int k = neighbourStarts[i]; k < neighbourStarts[i + 1]; k++)
            {
                int j = neighbours[k];

                if (shards[j] == null || fallTime[j] > 0f || visited[j] == visitStamp)
                    continue;

                visited[j] = visitStamp;
                queue[tail++] = j;
            }
        }

        bool givesWay = groundedLeft < groundedCount * settings.minBaseLeft;
        int queued = 0;

        for (int i = 0; i < shards.Length; i++)
        {
            if (shards[i] == null || fallTime[i] > 0f || (!givesWay && visited[i] == visitStamp))
                continue;

            fallTime[i] = Time.time + Mathf.Max(0.01f, settings.collapseDelay
                + Random.value * settings.collapseStagger
                + height01[i] * settings.collapseRipple);
            queued++;
        }

        if (queued == 0)
            return;

        Animate();

        if (givesWay)
        {
            if (settings.collapseEffect != null)
                settings.collapseEffect.Play(transform.position, Vector3.up);

            PlaySound(settings.collapseSound, transform.position);
            Camera_Rig.Shake(settings.collapseTrauma);
        }
    }

    // An unsupported shard lets go: mostly gravity, a little outward drift and tumble
    private void Drop(int index)
    {
        Vector3 velocity = Random.insideUnitSphere * settings.collapseScatter + Outward(index) * settings.collapseOutward;
        Detach(index, velocity, Random.insideUnitSphere * settings.collapseSpin);
    }

    // Swaps the intact mesh for the shards, riding along with the piece. From here the shards' own colliders take the
    // hits, so holes are real and falling debris lands on what's left.
    private void BeginCrack()
    {
        if (cracked)
            return;

        cracked = true;

        for (int i = 0; i < intactColliders.Length; i++)
            intactColliders[i].enabled = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = true;
        }

        if (intactRenderer != null)
            intactRenderer.enabled = false;

        root.gameObject.SetActive(true);
    }

    private void Animate()
    {
        if (!IsRegistered && Destruct_System.Instance != null)
            Destruct_System.Instance.Register(this);
    }

    private void Detach(int index, Vector3 velocity, Vector3 spinDegrees)
    {
        Transform shard = shards[index];
        shards[index] = null;
        remaining--;

        fallTime[index] = 0f;

        if (!cracked)
            BeginCrack();

        if (colliders[index] != null)
            colliders[index].enabled = true;

        Destruct_System.Instance.AddDebris(shard, bodies[index], velocity, spinDegrees * Mathf.Deg2Rad,
            settings.depenetrationSpeed);
    }

    // Nothing left standing: the piece stops taking hits
    private void Finish()
    {
        if (IsShattered)
            return;

        IsShattered = true;
        health = 0f;

        for (int i = 0; i < intactColliders.Length; i++)
            intactColliders[i].enabled = false;

        if (intactRenderer != null)
            intactRenderer.enabled = false;

        if (IsRegistered && Destruct_System.Instance != null)
            Destruct_System.Instance.Unregister(this);
    }

    // Copies the baked links and finds the grounded shards: those whose lowest point, in the world, is within
    // Ground Tolerance of the lowest point of the whole piece.
    private void BuildSupport(int count)
    {
        fallTime = new float[count];

        if (!fracture.HasSupports)
        {
            Debug.LogWarning($"{name}: no support data, worked out at load instead. Press Rebuild Supports on " +
                             "Destruct_Fracture to bake it.", this);
            fracture.BuildSupports();

            if (!fracture.HasSupports)
                return;
        }

        neighbourStarts = fracture.NeighbourStarts.ToArray();
        neighbours = fracture.Neighbours.ToArray();
        grounded = new bool[count];
        height01 = new float[count];
        visited = new int[count];
        queue = new int[count];

        groundedCount = FindGround(fracture, root, shards, settings.groundTolerance, grounded, height01);
    }

    // Grounded shards are those whose lowest point, in the world, is within tolerance of the lowest point of the
    // whole piece. Also fills each shard's height through the piece, 0 bottom to 1 top. Null shards are skipped.
    // Returns how many are grounded. GC: two working arrays; load time and editor gizmos only.
    private static int FindGround(Destruct_Fracture fracture, Transform root, Transform[] alive, float tolerance,
        bool[] grounded, float[] height01)
    {
        int count = alive.Length;
        float[] lowest = new float[count];
        float[] highest = new float[count];
        float bottom = float.MaxValue;
        float top = float.MinValue;

        for (int i = 0; i < count; i++)
        {
            if (alive[i] == null)
                continue;

            Vector3 min = fracture.ShardMins[i];
            Vector3 max = fracture.ShardMaxs[i];
            lowest[i] = float.MaxValue;
            highest[i] = float.MinValue;

            // The world height of each bounds corner, so a rotated or tilted piece still finds its real base
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = new Vector3((c & 1) == 0 ? min.x : max.x, (c & 2) == 0 ? min.y : max.y, (c & 4) == 0 ? min.z : max.z);
                float y = root.TransformPoint(corner).y;
                lowest[i] = Mathf.Min(lowest[i], y);
                highest[i] = Mathf.Max(highest[i], y);
            }

            bottom = Mathf.Min(bottom, lowest[i]);
            top = Mathf.Max(top, highest[i]);
        }

        float span = Mathf.Max(top - bottom, 0.0001f);
        int groundedCount = 0;

        for (int i = 0; i < count; i++)
        {
            if (alive[i] == null)
                continue;

            grounded[i] = lowest[i] - bottom <= tolerance;
            height01[i] = ((lowest[i] + highest[i]) * 0.5f - bottom) / span;

            if (grounded[i])
                groundedCount++;
        }

        return groundedCount;
    }

    private int NearestShard(Vector3 point, float radius)
    {
        int nearest = -1;
        float nearestDistance = radius * radius;

        for (int i = 0; i < shards.Length; i++)
        {
            if (shards[i] == null)
                continue;

            float distance = (shards[i].position - point).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = i;
            }
        }

        return nearest;
    }

    // World direction from the piece's centre out through a shard
    private Vector3 Outward(int index)
    {
        Vector3 outward = root.TransformVector(centres[index] - fracture.MeshCentre);
        return outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.up;
    }

    private int FirstShard()
    {
        for (int i = 0; i < shards.Length; i++)
        {
            if (shards[i] != null)
                return i;
        }

        return 0;
    }

    private static void PlaySound(SoundConfig sound, Vector3 position)
    {
        if (sound != null && sound.HasClips && AudioManager.Instance != null)
            AudioManager.Instance.RequestSound(sound, position);
    }

#if UNITY_EDITOR
    private static readonly Color LinkColour = new Color(0.55f, 0.8f, 1f, 0.9f);
    private static readonly Color HiddenLinkColour = new Color(0.55f, 0.8f, 1f, 0.12f);
    private static readonly Color GroundedColour = new Color(0.35f, 1f, 0.45f, 1f);
    private static readonly Color RestingColour = new Color(1f, 1f, 1f, 0.9f);
    private static readonly Color FallingColour = new Color(1f, 0.3f, 0.25f, 1f);

    // Each link once, trimmed short of both shards so the dots read clearly. Links in front of the surface are solid,
    // links behind it faint. In play mode it shows the live state: shards that have gone are dropped, red ones are
    // waiting to fall.
    private void OnDrawGizmosSelected()
    {
        if (!drawSupports)
            return;

        Destruct_Fracture source = fracture != null ? fracture : GetComponent<Destruct_Fracture>();

        if (source == null || !source.IsFractured || !source.HasSupports)
            return;

        Transform shardRoot = source.FracturedRoot.transform;
        int count = Mathf.Min(source.Shards.Count, source.ShardCentres.Count);
        bool live = Application.isPlaying && shards != null && grounded != null;

        Transform[] alive = live ? shards : new Transform[count];
        bool[] standing = live ? grounded : new bool[count];

        if (!live)
        {
            for (int i = 0; i < count; i++)
                alive[i] = source.Shards[i] != null && source.Shards[i].gameObject.activeSelf ? source.Shards[i] : null;

            float tolerance = settings != null ? settings.groundTolerance : 0.5f;
            FindGround(source, shardRoot, alive, tolerance, standing, new float[count]);
        }

        Vector3[] points = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            if (alive[i] != null)
                points[i] = live ? alive[i].position : shardRoot.TransformPoint(source.ShardCentres[i]);
        }

        // Each link once (i < j), shortened at both ends
        var segments = new System.Collections.Generic.List<Vector3>(source.Neighbours.Count);
        var starts = source.NeighbourStarts;

        for (int i = 0; i < count; i++)
        {
            if (alive[i] == null)
                continue;

            for (int k = starts[i]; k < starts[i + 1]; k++)
            {
                int j = source.Neighbours[k];

                if (j <= i || j >= count || alive[j] == null)
                    continue;

                Vector3 step = (points[j] - points[i]) * 0.18f;
                segments.Add(points[i] + step);
                segments.Add(points[j] - step);
            }
        }

        Vector3[] lines = segments.ToArray();
        CompareFunction previous = Handles.zTest;

        Handles.zTest = CompareFunction.Greater;
        Handles.color = HiddenLinkColour;
        Handles.DrawLines(lines);

        Handles.zTest = CompareFunction.LessEqual;
        Handles.color = LinkColour;
        Handles.DrawLines(lines);

        // Dots: always on top, so the structure reads even through the mesh
        Handles.zTest = CompareFunction.Always;

        for (int i = 0; i < count; i++)
        {
            if (alive[i] == null)
                continue;

            bool aboutToFall = live && fallTime != null && fallTime[i] > 0f;
            Handles.color = aboutToFall ? FallingColour : standing[i] ? GroundedColour : RestingColour;
            Handles.DotHandleCap(0, points[i], Quaternion.identity, HandleUtility.GetHandleSize(points[i]) * 0.035f, EventType.Repaint);
        }

        Handles.zTest = previous;
    }
#endif
}

#if UNITY_EDITOR
public static class Destruct_PieceMenu
{
    // Adds a collider if there's none, fractures the mesh and leaves it showing intact. Assign Piece Settings after.
    [MenuItem("GameObject/Goblins/Make Destructible", false, 20)]
    private static void Make()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            if (!go.TryGetComponent(out MeshFilter meshFilter) || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"{go.name}: Make Destructible needs a MeshFilter with a mesh.", go);
                continue;
            }

            if (!go.TryGetComponent(out Collider _))
            {
                MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(go);
                meshCollider.convex = true;
            }

            Destruct_Fracture fracture = go.GetComponent<Destruct_Fracture>();

            if (fracture == null)
                fracture = Undo.AddComponent<Destruct_Fracture>(go);

            if (!go.TryGetComponent(out Destruct_Piece _))
                Undo.AddComponent<Destruct_Piece>(go);

            fracture.EditorFracture();
            fracture.EditorSetPreview(false);   // fracturing leaves the shards showing; go back to the intact mesh
        }
    }

    [MenuItem("GameObject/Goblins/Make Destructible", true)]
    private static bool Validate() => Selection.gameObjects.Length > 0;
}
#endif
