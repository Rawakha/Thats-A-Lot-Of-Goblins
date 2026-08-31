using UnityEngine;

[CreateAssetMenu(menuName = "TD/Wave Definition")]
public class WaveDefinition : ScriptableObject
{
    public int totalEnemies = 100;
    public float spawnDelay = 0.1f;
    public int spawnPointCount = 1;
    public int goldReward = 100;
}