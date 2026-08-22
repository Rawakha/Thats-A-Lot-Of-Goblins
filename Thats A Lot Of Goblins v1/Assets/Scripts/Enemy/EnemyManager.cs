using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : GameManagerBase
{
    public static EnemyManager Instance;

    [SerializeField] private HordeAudioManager audioManager;
    [SerializeField] private EnemyVariation variation;
    [SerializeField] private EnemyPool pool;
    [SerializeField] private EnemyMover mover;
    [SerializeField] private EnemyAnimator animator;
    [SerializeField] private EnemyAirborneManager airborneManager;
    [SerializeField] private EnemyFeelManager feelManager;

    [Header("Enemy Tracking")]
    [SerializeField] private List<EnemyData> activeEnemies = new List<EnemyData>();
    [SerializeField] private int maxActiveEnemies = 1000;
    [SerializeField] private int resumeThreshold = 900;

    public EnemyVariation Variation => variation;
    public int ActiveCount => activeEnemies.Count;
    public bool CanSpawn => activeEnemies.Count < maxActiveEnemies;

    #region Initialization
    protected override bool OnInitialize(GameLevelBootstrap levelBootstrap)
    {
        if (!audioManager || !variation || !pool || !animator || !airborneManager || !feelManager)
        {
            Debug.LogError("EnemyManager: Missing components", this); 
            return false;
        }

        Utilities.CreateInstance(ref Instance, this);

        audioManager.Initialize(this);
        variation.Initialize();
        airborneManager.Initialize(this);
        pool.Initialize(this);

        return true;
    }

    [InspectorButton]
    public void GetComponents()
    {
        audioManager = GetComponentInChildren<HordeAudioManager>();
        pool = GetComponentInChildren<EnemyPool>();
        mover = GetComponentInChildren<EnemyMover>();
        animator = GetComponentInChildren<EnemyAnimator>();
        airborneManager = GetComponentInChildren<EnemyAirborneManager>();
        feelManager = GetComponentInChildren<EnemyFeelManager>();
    }
    #endregion

    public EnemyData Spawn(Vector3 position, Quaternion rotation)
    {
        EnemyData e = pool.Get(position, rotation);
        if (e == null)
            return null;

        e.state = EnemyState.Pooled;
        e.OnSpawn();
        animator.Add(e);
        activeEnemies.Add(e);
        e.masterIndex = activeEnemies.Count - 1;

        EnemyStateMachine.Set(e, EnemyState.Walking);
        return e;
    }

    public void Despawn(EnemyData e)
    {
        if (e == null) 
            return;

        e.OnDespawn();

        animator.Remove(e);
        feelManager.Remove(e);
        Remove(e);                  // Remove from Manager's list
        pool.Return(e);
    }

    private void Remove(EnemyData e)
    {
        if (e == null)
            return;

        int idx = e.masterIndex;

        if (idx < 0 || idx > activeEnemies.Count || activeEnemies[idx] != e)
        {
            Debug.LogError($"EnemyMover: staled moverIndex on {e.name}", e);

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