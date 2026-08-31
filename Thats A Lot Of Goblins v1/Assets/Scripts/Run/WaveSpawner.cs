using System;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [SerializeField] private bool active = false;

    [Header("Spawn Settings")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private int maxSpawnsPerFrame = 100;
    [SerializeField] private float maxBacklogSeconds = 1f;

    [Header("Wave Spawner Settings")]
    [SerializeField, ReadOnly] private int spawnCount = 0;
    [SerializeField, ReadOnly] private float spawnDelay = 0f;

    private float spawnTimer;

    private Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;

    public event Action<WaveSpawner> OnSpawningComplete;

    private void Update()
    {
        if (spawnPoint == null)
            return;

        if (!active || spawnCount <= 0)
            return;

        EnemyManager enemyManager = EnemyManager.Instance;
        spawnTimer = Mathf.Min(spawnTimer + Time.deltaTime, maxBacklogSeconds);
        int spawnsThisFrame = 0;

        while (spawnTimer >= spawnDelay && spawnsThisFrame < maxSpawnsPerFrame)
        {
            if (!enemyManager.CanSpawn)
                break;

            spawnTimer -= spawnDelay;
            enemyManager.Spawn(SpawnPoint.position, spawnPoint.rotation);
            spawnCount--;
            spawnsThisFrame++;

            if (spawnCount <= 0)
            {
                active = false;
                spawnCount = 0;
                OnSpawningComplete?.Invoke(this);
                break;
            }
        }
    }

    public void BeginSpawning(int count,  float spawnDelay)
    {
        this.spawnCount = count;
        this.spawnDelay = spawnDelay;

        active = true;
    }

    public void ForceStopSpawning()
    {
        active = false;
        spawnCount = 0;
    }
}