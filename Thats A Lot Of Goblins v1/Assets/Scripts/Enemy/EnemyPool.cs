using System.Collections.Generic;
using UnityEngine;

public class EnemyPool : MonoBehaviour
{
    public static EnemyPool Instance;

    [SerializeField] private EnemyData enemyPrefab;
    [SerializeField] private int poolSize = 1000;

    [Header("Stats")]
    [SerializeField, ReadOnly] private int overallPoolSize = 0;
    [SerializeField, ReadOnly] private int activeEnemies = 0;
    [SerializeField, ReadOnly] private int inactiveEnemies = 0;
    [SerializeField, ReadOnly] private int peakActive = 0;

    private Queue<EnemyData> pool;

    public bool Initialize(EnemyManager manager)
    {
        if (!Utilities.CreateInstance<EnemyPool>(ref Instance, this))
        {
            return false;
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
        if (enemyPrefab == null)
            return;

        pool = new Queue<EnemyData>(poolSize);

        for (int i = 0; i < poolSize; i++)
        {
            Return(SpawnNewEnemy());
        }
    }

    public void Return(EnemyData enemy)
    {
        if (enemy == null)
            return;

        if (enemy.body != null)
        {
            enemy.body.linearVelocity = Vector3.zero;
            enemy.body.angularVelocity = Vector3.zero;
        }

        enemy.gameObject.SetActive(false);
        pool.Enqueue(enemy);
    }

    public EnemyData Get(Vector3 position, Quaternion rotation)
    {
        EnemyData enemy = pool.Count > 0 ? pool.Dequeue() : SpawnNewEnemy();

        if (enemy == null || enemy.body == null) 
            return null;

        enemy.facing = rotation;
        enemy.body.interpolation = RigidbodyInterpolation.None;
        enemy.body.position = position;
        enemy.body.rotation = rotation;
        enemy.transform.SetPositionAndRotation(position, rotation);
        enemy.gameObject.SetActive(true);
        enemy.body.interpolation = RigidbodyInterpolation.Interpolate;

        return enemy;
    }

    private EnemyData SpawnNewEnemy()
    {
        overallPoolSize++;

        EnemyData e = Instantiate(enemyPrefab, transform);

        e.OnCreated();

        return e;
    }
}