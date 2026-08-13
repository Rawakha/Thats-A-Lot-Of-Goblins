using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private bool active = false;

    [Header("Enemies")]
    [ReadOnly] public int activeEnemies = 0;
    [SerializeField] private Enemy enemyPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnDelay = 0.1f;

    private float nextSpawnTime;

    private void Update()
    {
        if (!active)
            return;

        if (spawnPoint == null || enemyPrefab == null)
            return;

        if (Time.time >= nextSpawnTime)
        {
            SpawnEnemy();
            nextSpawnTime = Time.time + spawnDelay;
        }
    }

    private void SpawnEnemy()
    {
        Enemy e = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);

        if (EnemyMover.Instance != null)
        {
            EnemyMover.Instance.Add(e);
        }

        activeEnemies++;
    }
}