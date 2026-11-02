using System.Collections.Generic;
using UnityEngine;

// One pooled copy of an effect prefab, with its particles and trails cached so restarting it never searches
public sealed class Effect_Instance
{
    public readonly GameObject GameObject;
    public readonly Transform Transform;
    public readonly GameObject Prefab;

    private readonly ParticleSystem[] particles;
    private readonly TrailRenderer[] trails;

    internal float ReleaseTime;
    internal int TimedIndex = -1;

    internal Effect_Instance(GameObject prefab, GameObject instance)
    {
        Prefab = prefab;
        GameObject = instance;
        Transform = instance.transform;
        particles = instance.GetComponentsInChildren<ParticleSystem>(true);
        trails = instance.GetComponentsInChildren<TrailRenderer>(true);

        // Particle systems ignore their parents' scale by default (Local); Hierarchy lets Play's scale reach every
        // system in the effect. Identical at scale 1.
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem.MainModule main = particles[i].main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    internal void Restart()
    {
        for (int i = 0; i < trails.Length; i++)
        {
            trails[i].Clear();
            trails[i].emitting = true;
        }

        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Clear(false);
            particles[i].Play(false);
        }
    }

    // Stops emitting but lets what's already out fade, e.g. a tracer trail after the bullet hits
    internal void StopEmitting()
    {
        for (int i = 0; i < trails.Length; i++)
            trails[i].emitting = false;

        for (int i = 0; i < particles.Length; i++)
            particles[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }
}

// Pools effect prefabs (impacts, tracers, muzzle flashes) so nothing is instantiated or destroyed mid-run.
// Play: fire and forget, returned after a lifetime. Spawn / Release: the caller owns it until it's released,
// e.g. a tracer that follows a projectile.
//
// Pooled prefabs must not move or destroy themselves. The first time a prefab is used it's checked for Rigidbodies,
// colliders and scripts (effect packs often ship projectile and self-destroy scripts) and a warning lists them.
//
// GC: allocates only when a pool is empty (first use of a prefab, or more live at once than ever before).
// One UpdateManager subscription (OnUpdate) for timed returns.
[DefaultExecutionOrder(Utilities.ExecutionOrder.DependantSingletons)]
public class Effect_Pool : MonoBehaviour
{
    [SerializeField, Min(0)] private int timedCapacity = 256;

    private readonly Dictionary<GameObject, Stack<Effect_Instance>> pools = new Dictionary<GameObject, Stack<Effect_Instance>>();
    private List<Effect_Instance> timed;

    public static Effect_Pool Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"{name}: A second Effect_Pool was found, this one is disabled.", this);
            enabled = false;
            return;
        }

        Instance = this;
        timed = new List<Effect_Instance>(timedCapacity);
    }

    private void OnEnable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnUpdate += Tick;
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnUpdate -= Tick;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Scale: a multiple of the prefab's own size (1 is as made)
    public void Play(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime, float scale = 1f)
    {
        if (prefab == null)
            return;

        ReleaseAfter(Spawn(prefab, position, rotation, scale), lifetime, false);
    }

    public Effect_Instance Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1f)
    {
        if (!pools.TryGetValue(prefab, out Stack<Effect_Instance> pool))
        {
            pool = new Stack<Effect_Instance>(16);
            pools.Add(prefab, pool);
            WarnIfUnpoolable(prefab);
        }

        Effect_Instance instance = pool.Count > 0
            ? pool.Pop()
            : new Effect_Instance(prefab, Instantiate(prefab, transform));

        instance.Transform.SetPositionAndRotation(position, rotation);
        instance.Transform.localScale = prefab.transform.localScale * scale;     // always set: pooled copies get reused
        instance.GameObject.SetActive(true);
        instance.Restart();
        return instance;
    }

    // Returns it now
    public void Release(Effect_Instance instance)
    {
        if (instance.TimedIndex >= 0)
            RemoveTimed(instance.TimedIndex);

        instance.GameObject.SetActive(false);
        pools[instance.Prefab].Push(instance);
    }

    // Returns it after a delay. With stopEmitting, its particles and trails stop now and fade out over the delay.
    public void ReleaseAfter(Effect_Instance instance, float delay, bool stopEmitting)
    {
        if (delay <= 0f)
        {
            Release(instance);
            return;
        }

        if (stopEmitting)
            instance.StopEmitting();

        instance.ReleaseTime = Time.time + delay;

        if (instance.TimedIndex < 0)
        {
            instance.TimedIndex = timed.Count;
            timed.Add(instance);
        }
    }

    private void Tick()
    {
        float time = Time.time;

        for (int i = timed.Count - 1; i >= 0; i--)
        {
            if (time >= timed[i].ReleaseTime)
                Release(timed[i]);
        }
    }

    // Once per prefab. A self-destroying effect breaks the pool, and a Rigidbody or collider fights the projectile system.
    private void WarnIfUnpoolable(GameObject prefab)
    {
        string problems = null;

        if (prefab.GetComponentInChildren<Rigidbody>(true) != null)
            problems += " a Rigidbody,";

        if (prefab.GetComponentInChildren<Collider>(true) != null)
            problems += " a collider,";

        MonoBehaviour[] scripts = prefab.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < scripts.Length; i++)
        {
            if (scripts[i] != null && !(scripts[i] is Effect_Hit) && !(scripts[i] is Combat_ProjectileType))
                problems += $" {scripts[i].GetType().Name} (on {scripts[i].name}),";
        }

        if (problems != null)
            Debug.LogWarning($"{name}: Effect prefab '{prefab.name}' has{problems.TrimEnd(',')}. Pooled effects must not " +
                             "move or destroy themselves; remove anything that does.", prefab);
    }

    // Swap-back: the last timed entry takes this one's place
    private void RemoveTimed(int index)
    {
        Effect_Instance removed = timed[index];
        int last = timed.Count - 1;

        if (index != last)
        {
            timed[index] = timed[last];
            timed[index].TimedIndex = index;
        }

        timed.RemoveAt(last);
        removed.TimedIndex = -1;
    }
}
