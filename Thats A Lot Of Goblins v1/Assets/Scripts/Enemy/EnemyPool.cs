using System.Collections.Generic;
using UnityEngine;

public class EnemyPool : GameManagerBase
{
    public static EnemyPool Instance;

    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private int poolSize = 1000;

    [Header("Stats")]
    [SerializeField, ReadOnly] private int overallPoolSize = 0;
    [SerializeField, ReadOnly] private int activeEnemies = 0;
    [SerializeField, ReadOnly] private int inactiveEnemies = 0;
    [SerializeField, ReadOnly] private int peakActive = 0;

    private EnemyVariation enemyVariation;
    private Queue<Enemy> pool;

    protected override bool OnInitialize(GameLevelBootstrap levelBootstrap)
    {
        if (!Utilities.CreateInstance<EnemyPool>(ref Instance, this))
        {
            return false;
        }

        if (TryGetComponent<EnemyVariation>(out EnemyVariation variation))
        {
            enemyVariation = variation;
            enemyVariation.Initialize();
        }

        CreatePool();

        return true;
    }

#if UNITY_EDITOR
    private void OnDisable()
    {
        Debug.Log($"Peak Active Enemies in Run: {peakActive}");
    }

    private void Update()
    {
        inactiveEnemies = pool.Count;
        activeEnemies = overallPoolSize - inactiveEnemies;

        if (activeEnemies > peakActive) peakActive = activeEnemies; 
    }
#endif

    private void CreatePool()
    {
        pool = new Queue<Enemy>(poolSize);

        for (int i = 0; i < poolSize; i++)
        {
            Return(SpawnNewEnemy());
        }
    }

    public void Return(Enemy enemy)
    {
        if (enemy == null || enemy.inPool)
            return;

        if (enemy.body != null)
        {
            enemy.body.linearVelocity = Vector3.zero;
            enemy.body.angularVelocity = Vector3.zero;
        }

        enemy.SetCollisionCallbacksEnabled(false);
        enemy.gameObject.SetActive(false);
        enemy.inPool = true;

        pool.Enqueue(enemy);
    }

    public Enemy Get(Vector3 position, Quaternion rotation)
    {
        Enemy enemy = pool.Count > 0 ? pool.Dequeue() : SpawnNewEnemy();

        enemy.ResetEnemy();

        enemy.facing = rotation;
        enemy.transform.SetPositionAndRotation(position, rotation);
        enemy.gameObject.SetActive(true);

        return enemy;
    }

    private Enemy SpawnNewEnemy()
    {
        overallPoolSize++;

        Enemy e = Instantiate(enemyPrefab, transform);

        if (enemyVariation != null)
        {
            enemyVariation.Apply(e);
        }

        return e;
    }
}