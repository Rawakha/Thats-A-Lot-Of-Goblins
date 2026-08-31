using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : GameManagerBase
{
    public static EnemyManager Instance;

    [SerializeField] private HordeAudioManager audioManager;
    [SerializeField] private EnemyRenderer renderer;
    [SerializeField] private EnemyVariation variation;
    [SerializeField] private EnemyPool pool;
    [SerializeField] private EnemyMover mover;
    [SerializeField] private EnemyAnimator animator;
    [SerializeField] private EnemyAirborneManager airborneManager;
    [SerializeField] private EnemyFeelManager feelManager;
    [SerializeField] private EnemyGrid grid;
    [SerializeField, ReadOnly] private int frameCounter;

    [Header("Enemy Tracking")]
    [SerializeField] private List<Enemy> activeEnemies = new List<Enemy>();
    [SerializeField] private int maxActiveEnemies = 1000;
    [SerializeField] private int resumeThreshold = 900;

    public EnemyVariation Variation => variation;
    public int ActiveCount => activeEnemies.Count;
    public bool CanSpawn => activeEnemies.Count < maxActiveEnemies;

    #region Initialization
    protected override bool OnInitialize(GameLevelBootstrap levelBootstrap)
    {
        if (!audioManager || !renderer || !variation || !pool || !animator || !airborneManager || !feelManager || !grid)
        {
            Debug.LogError("EnemyManager: Missing components", this); 
            return false;
        }

        Utilities.CreateInstance(ref Instance, this);

        audioManager.Initialize(this);
        renderer.Initialize(this);
        // variation.Initialize();
        airborneManager.Initialize(this);
        pool.Initialize(this);
        grid.Initialize(this);

        frameCounter = 0;

        return true;
    }

    [InspectorButton]
    public void GetComponents()
    {
        audioManager = GetComponentInChildren<HordeAudioManager>();
        renderer = GetComponentInChildren<EnemyRenderer>();
        pool = GetComponentInChildren<EnemyPool>();
        mover = GetComponentInChildren<EnemyMover>();
        animator = GetComponentInChildren<EnemyAnimator>();
        airborneManager = GetComponentInChildren<EnemyAirborneManager>();
        feelManager = GetComponentInChildren<EnemyFeelManager>();
        grid = GetComponentInChildren<EnemyGrid>();
    }
    #endregion

    public void LateUpdate()
    {
        frameCounter++;

        if (frameCounter >= grid.RebuildInterval)
        {
            frameCounter = 0;
            grid.Rebuild(activeEnemies);
        }
    }

    public Enemy Spawn(Vector3 position, Quaternion rotation)
    {
        Enemy e = pool.Get(position, rotation);
        if (e == null)
            return null;

        e.state = EnemyState.Pooled;
        e.OnSpawn();
        animator.Add(e);
        renderer.Add(e);
        activeEnemies.Add(e);
        e.masterIndex = activeEnemies.Count - 1;

        EnemyStateMachine.Set(e, EnemyState.Walking);
        return e;
    }

    public void Despawn(Enemy e)
    {
        if (e == null) 
            return;

        e.OnDespawn();

        animator.Remove(e);
        renderer.Remove(e);
        feelManager.Remove(e);
        Remove(e);                  // Remove from Manager's list
        pool.Return(e);

        WaveManager.Instance?.NotifyEnemyRemoved();
    }

    public void DespawnAll()
    {
        if (activeEnemies == null || activeEnemies.Count == 0)
            return;

        for (int i = activeEnemies.Count - 1; i >= 0; i--)
            EnemyStateMachine.Set(activeEnemies[i], EnemyState.Pooled);
    }

    private void Remove(Enemy e)
    {
        if (e == null)
            return;

        int idx = e.masterIndex;

        if (idx < 0 || idx >= activeEnemies.Count || activeEnemies[idx] != e)
        {
            Debug.LogError($"EnemyManager: staled masterIndex on {e.name}", e);

            activeEnemies.Remove(e);
            e.masterIndex = -1;
            return;
        }

        int last = activeEnemies.Count - 1;

        if (idx != last)
        {
            activeEnemies[idx] = activeEnemies[last];
            activeEnemies[idx].masterIndex = idx;
        }

        activeEnemies.RemoveAt(last);
        e.masterIndex = -1;
    }
}