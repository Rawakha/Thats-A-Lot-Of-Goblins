using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private bool active = false;

    [Header("Spawn Settings")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnDelay = 0.1f;
    [SerializeField] private int maxSpawnsPerFrame = 100;

    private EnemyPool enemyPool;
    private float spawnTimer;

    private void Start()
    {
        enemyPool = EnemyPool.Instance;
        if (enemyPool == null)
            enabled = false;
    }

    private void Update()
    {
        if (!active)
            return;

        if (spawnPoint == null)
            return;

        spawnTimer += Time.deltaTime;

        int spawns = 0;
        while (spawnTimer >= spawnDelay && spawns < maxSpawnsPerFrame)
        {
            spawnTimer -= spawnDelay;
            SpawnEnemy();
            spawns++;
        }
    }

    private void SpawnEnemy()
    {
        Enemy e = enemyPool.Get(spawnPoint.position, spawnPoint.rotation);

        if (EnemyMover.Instance != null)
        {
            EnemyMover.Instance.Add(e);
        }
    }
}