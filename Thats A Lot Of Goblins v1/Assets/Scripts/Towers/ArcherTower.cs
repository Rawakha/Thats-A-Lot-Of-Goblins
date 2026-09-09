using System;
using System.Collections.Generic;
using UnityEngine;

public class ArcherTower : Tower
{
    [Serializable]
    private sealed class ArcherSlot
    {
        public Transform visual;
        public Transform arrowSpawnPoint;

        [NonSerialized] public Enemy target;
        [NonSerialized] public Vector3 restPosition;
        [NonSerialized] public Vector3 cachedDesiredPosition;
        [NonSerialized] public Vector3 moveVelocity;
        [NonSerialized] public float nextMoveTargetRefresh;
        [NonSerialized] public float nextAttackTime;
    }

    [Header("Archers")]
    [SerializeField] private ArcherSlot[] archers;
 
    [Header("Archer")]
    [SerializeField] private float moveDistance = 0.5f;
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float moveSmoothTime = 0.15f;
    [SerializeField] private float moveTargetRefreshInterval = 0.1f;
    [SerializeField] private float minimumDestinationChange = 0.04f;
    [SerializeField] private float archerTurnSpeed = 540f;

    [Header("Attack")]
    [SerializeField] private ParabolicProjectile arrowPrefab;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float damage = 50f;
    [SerializeField] private float arrowTravelTime = 0.45f;
    [SerializeField] private float arrowArcHeight = 1.5f;

    private void Awake()
    {
        for (int i = 0; i < archers.Length; i++)
        {
            ArcherSlot archer = archers[i];

            if (archer.visual == null)
                continue;

            archer.restPosition = archer.visual.localPosition;
            archer.cachedDesiredPosition = archer.restPosition;

            float attackOffset = attackCooldown * i / archers.Length;
            archer.nextAttackTime = Time.time + attackOffset;

            float movementOffset = moveTargetRefreshInterval * i / archers.Length;
            archer.nextMoveTargetRefresh = Time.time + movementOffset;
        }
    }

    private void Update()
    {
        if (currentTarget == null || !HasValidTarget())
        {
            return;
        }

        for (int i = 0; i < archers.Length; i++)
        {
            ArcherSlot archer = archers[i];

            if (!IsTargetValid(archer.target))
            {
                archer.target = null;
                MoveArcher(archer);
                continue;
            }

            MoveArcher(archer);
            RotateArcher(archer);

            if (Time.time < archer.nextAttackTime)
                continue;

            Shoot(archer);
            archer.nextAttackTime = Time.time + attackCooldown;
        }
    }

    private void Shoot(ArcherSlot archer)
    {
        if (arrowPrefab == null || archer.arrowSpawnPoint == null || archer.target == null) 
            return;

        ParabolicProjectile projectile = Instantiate(arrowPrefab, archer.arrowSpawnPoint.position, archer.arrowSpawnPoint.rotation);
        projectile.Launch(archer.target, damage, arrowTravelTime, arrowArcHeight, archer.arrowSpawnPoint.position);
    }

    private void RotateArcher(ArcherSlot archer)
    {
        if (archer.visual == null || archer.target == null || archer.target.body == null) 
            return;

        Vector3 direction = archer.target.body.worldCenterOfMass - archer.visual.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        archer.visual.rotation = Quaternion.RotateTowards(archer.visual.rotation, targetRotation, archerTurnSpeed * Time.deltaTime);
    }

    private void MoveArcher(ArcherSlot archer)
    {
        if (archer.visual == null || archer.visual.parent == null)
            return;

        if (Time.time >= archer.nextMoveTargetRefresh)
        {
            archer.nextMoveTargetRefresh = Time.time + moveTargetRefreshInterval;
            
            Vector3 newDesiredPosition = CalculateDesiredPosition(archer);
            float minimumChangeSqr = minimumDestinationChange * minimumDestinationChange;

            if ((newDesiredPosition - archer.cachedDesiredPosition).sqrMagnitude >= minimumChangeSqr)
                archer.cachedDesiredPosition = newDesiredPosition;
        }

        archer.visual.localPosition = Vector3.SmoothDamp(archer.visual.localPosition, archer.cachedDesiredPosition, ref archer.moveVelocity, moveSmoothTime, moveSpeed, Time.deltaTime);
    }

    private Vector3 CalculateDesiredPosition(ArcherSlot archer)
    {
        Vector3 desiredPosition = archer.restPosition;

        if (archer.target == null || archer.target.body == null)
            return desiredPosition;

        Transform movementSpace = archer.visual.parent;

        Vector3 worldDirection = archer.target.body.worldCenterOfMass - movementSpace.position;
        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude <= 0.0001f)
            return desiredPosition;

        Vector3 localDirection = movementSpace.InverseTransformDirection(worldDirection);
        localDirection.y = 0f;
        localDirection.Normalize();

        return desiredPosition + localDirection * moveDistance;
    }

    public override bool HasValidTarget()
    {
        if (archers == null || archers.Length == 0) 
            return true;

        bool everyArcherHasTarget = true;

        for (int i = 0; i < archers.Length; i++)
        {
            if (IsTargetValid(archers[i].target))
                continue;

            archers[i].target = null;
            everyArcherHasTarget = false;
        }

        return everyArcherHasTarget;
    }

    public override void GetTarget(List<Enemy> results)
    {
        if (results == null || EnemyGrid.Instance == null)
            return;

        int hitCount = EnemyGrid.Instance.QueryRadius(WorldPosition, gainTargetRange, results);

        for (int i = 0; i < archers.Length; i++)
        {
            ArcherSlot archer = archers[i];

            if (IsTargetValid(archer.target))
                continue;

            archer.target = FindClosestUntargetedEnemy(results, hitCount);
        }
    }

    private Enemy FindClosestUntargetedEnemy(List<Enemy> results, int hitCount)
    {
        Enemy closestEnemy = null;
        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Enemy enemy = results[i];

            if (!IsTargetValid(enemy) || IsAlreadyTargeted(enemy))
                continue;

            Vector3 enemyPosition = enemy.body.position;

            float xDistance = enemyPosition.x - WorldPosition.x;
            float zDistance = enemyPosition.z - WorldPosition.z;
            float distanceSqr = xDistance * xDistance + zDistance * zDistance;

            if (distanceSqr >= closestDistanceSqr)
                continue;

            closestDistanceSqr = distanceSqr;
            closestEnemy = enemy;
        }

        return closestEnemy;
    }

    private bool IsAlreadyTargeted(Enemy enemy)
    {
        for (int i = 0; i < archers.Length; i++)
        {
            if (archers[i].target == enemy)
                return true;
        }

        return false;
    }
}