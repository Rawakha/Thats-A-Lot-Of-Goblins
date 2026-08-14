using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class EnemyPool : MonoBehaviour
{
    public static EnemyPool Instance;

    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private int poolSize = 1000;

    [Header("Stats")]
    [SerializeField, ReadOnly] private int overallPoolSize = 0;
    [SerializeField, ReadOnly] private int activeEnemies = 0;
    [SerializeField, ReadOnly] private int inactiveEnemies = 0;
    [SerializeField, ReadOnly] private int peakActive = 0;

    private Queue<Enemy> pool;

    private void Awake()
    {
        Utilities.CreateInstance<EnemyPool>(ref Instance, this);

        CreatePool();
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
        if (enemy == null)
            return;

        if (enemy.inPool)
            return;

        enemy.body.linearVelocity = Vector3.zero;
        enemy.body.angularVelocity = Vector3.zero;
        enemy.gameObject.SetActive(false);
        enemy.inPool = true;

        pool.Enqueue(enemy);
    }

    public Enemy Get(Vector3 position, Quaternion rotation)
    {
        Enemy enemy = pool.Count > 0 ? pool.Dequeue() : SpawnNewEnemy();
        
        enemy.inPool = false;
        enemy.transform.SetPositionAndRotation(position, rotation);
        enemy.gameObject.SetActive(true);

        return enemy;
    }

    private Enemy SpawnNewEnemy()
    {
        overallPoolSize++;
        return Instantiate(enemyPrefab, transform);
    }
}