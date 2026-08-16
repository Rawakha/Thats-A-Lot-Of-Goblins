using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [SerializeField] private bool active = false;

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnDelay = 0.1f;
    [SerializeField] private int maxSpawnsPerFrame = 100;

    private EnemyPool enemyPool;
    private float spawnTimer;
    private int spawnPointIndex;

    private void Start()
    {
        enemyPool = EnemyPool.Instance;
        if (enemyPool == null)
            enabled = false;

        spawnPointIndex = 0;
    }

    private void Update()
    {
        if (!active)
            return;

        if (spawnPoints == null || spawnPoints.Length == 0)
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
        Transform spawnPoint = spawnPoints[spawnPointIndex];
        Enemy e = enemyPool.Get(spawnPoint.position, spawnPoint.rotation);

        if (EnemyMover.Instance != null)
        {
            EnemyMover.Instance.Add(e);
        }

        spawnPointIndex = (spawnPointIndex + 1) % spawnPoints.Length;
    }

    public void SetActive(bool t)
    {
        active = t;
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return;

        foreach (var s in spawnPoints)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(s.position, 0.5f);
        }
    }
}