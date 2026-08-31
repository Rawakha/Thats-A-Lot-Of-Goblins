using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance;

    [Header("Waves")]
    [SerializeField] private WaveSpawner[] spawners;
    [SerializeField] private WaveDefinition[] waveDefinitions;

    public WaveDefinition CurrentWaveDefinition { get; private set; }
    public int SpawnersRemaining { get; private set; }
    public int EnemiesRemaining { get; private set; }

    private void Awake()
    {
        Utilities.CreateInstance(ref Instance, this);
    }

    public void BeginWave(int index)
    {
        WaveDefinition w = waveDefinitions[index];
        WaveSpawner[] spawners = GetSpawners(w.spawnPointCount);
        int enemiesPerSpawner = w.totalEnemies / w.spawnPointCount;

        foreach (var s in spawners)
        {
            s.BeginSpawning(enemiesPerSpawner, w.spawnDelay);
            s.OnSpawningComplete += OnSpawnerComplete;
        }

        CurrentWaveDefinition = w;
        SpawnersRemaining = w.spawnPointCount;
        EnemiesRemaining = w.totalEnemies;

        RunManagerUI.Instance.SetEnemies(w.totalEnemies);
    }

    public void StopSpawning()
    {
        foreach (WaveSpawner s in spawners)
        {
            s.ForceStopSpawning();
        }
    }

    private void OnSpawnerComplete(WaveSpawner spawner)
    {
        if (spawner == null)
            return;

        SpawnersRemaining--;

        // Spawning is done we can now transition to clearing
        if (SpawnersRemaining <= 0)
        {
            RunManager.Instance.SetPhase(RunManager.RunPhase.Clearing);
        }

        // Unsubscribe from event
        spawner.OnSpawningComplete -= OnSpawnerComplete;
    }

    private WaveSpawner[] GetSpawners(int count)
    {
        List<WaveSpawner> availableSpawners = spawners.ToList();
        WaveSpawner[] chosenSpawners = new WaveSpawner[count];
        
        for (int i = 0; i < count; i++)
        {
            WaveSpawner spawner = Utilities.Random(availableSpawners);
            chosenSpawners[i] = spawner;

            availableSpawners.Remove(spawner);
        }

        return chosenSpawners;
    }

    public void NotifyEnemyRemoved()
    {
        EnemiesRemaining--;
        RunManagerUI.Instance.SetEnemies(EnemiesRemaining);
    }
}