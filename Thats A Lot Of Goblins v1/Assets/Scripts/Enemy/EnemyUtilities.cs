using System.Collections.Generic;
using UnityEngine;

public static class EnemyUtilities
{
    public static Enemy GetClosestEnemy(Vector3 origin, float range, List<Enemy> results)
    {
        if (EnemyGrid.Instance == null)
            return null;

        int hitCount = EnemyGrid.Instance.QueryRadius(origin, range, results);

        if (hitCount <= 0) 
            return null;

        Enemy closestEnemy = null;
        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Enemy enemy = results[i];
            if (enemy == null) continue;

            Vector3 enemyPos = enemy.body.position;
            float xDistance = origin.x - enemyPos.x;
            float zDistance = origin.z - enemyPos.z;
            float distanceSqr = xDistance * xDistance + zDistance * zDistance;

            if (distanceSqr > closestSqrDistance)
                continue;

            closestEnemy = enemy;
            closestSqrDistance = distanceSqr;
        }

        return closestEnemy;
    }
}