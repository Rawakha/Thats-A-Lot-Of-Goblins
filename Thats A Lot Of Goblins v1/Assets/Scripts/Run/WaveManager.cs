using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [Header("Spawners")]
    [SerializeField] private WaveSpawner[] spawners;

    public void StartWaves()
    {
        SetSpawners(true);
    }

    public void StopWaves()
    {
        SetSpawners(false);
    }

    private void SetSpawners(bool t)
    {
        if (spawners == null || spawners.Length == 0)
            return;

        foreach (var s in spawners)
        {
            s.SetActive(t);
        }
    }

    [InspectorButton]
    public void GetSpawners()
    {
        spawners = FindObjectsByType<WaveSpawner>();
    }
}