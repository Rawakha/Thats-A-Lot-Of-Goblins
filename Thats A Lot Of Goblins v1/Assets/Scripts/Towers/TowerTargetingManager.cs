using System.Collections.Generic;
using UnityEngine;

public class TowerTargetingManager : MonoBehaviour
{
    public static TowerTargetingManager Instance;

    [SerializeField, Min(1)] private int queriesPerFrame = 10;
    [SerializeField, Min(1)] private int initialResultsCapacity = 128;

    private int nextTowerIndex;
    private List<Enemy> results;

    private readonly List<Tower> towers = new List<Tower>();

    private void Awake()
    {
        Utilities.CreateInstance(ref Instance, this);

        results = new List<Enemy>(initialResultsCapacity);
    }

    private void Update()
    {
        if (towers.Count == 0)
            return;

        int queriesPerformed = 0;
        int towersChecked = 0;
        int towerCount = towers.Count;

        while (queriesPerformed < queriesPerFrame && towersChecked < towerCount)
        {
            if (nextTowerIndex >= towers.Count)
                nextTowerIndex = 0;

            Tower tower = towers[nextTowerIndex];
            nextTowerIndex++;
            towersChecked++;

            if (tower == null || tower.HasValidTarget())
                continue;

            results.Clear();
            tower.GetTarget(results);
            queriesPerformed++;
        }
    }

    public void Register(Tower tower)
    {
        if (tower == null || towers.Contains(tower))
            return;

        towers.Add(tower);
    }

    public void Unregister(Tower tower)
    {
        int index = towers.IndexOf(tower);

        if (index < 0)
            return;

        int lastIndex = towers.Count - 1;
        towers[index] = towers[lastIndex];
        towers.RemoveAt(lastIndex);

        if (nextTowerIndex > towers.Count)
            nextTowerIndex = 0;
    }
}