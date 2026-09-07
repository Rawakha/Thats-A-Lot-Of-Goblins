using UnityEngine;

public class ArcherTower : Tower
{
    [Header("Archer")]
    [SerializeField] private Transform archerVisual;
    [SerializeField] private Transform arrowSpawnPoint;
    [SerializeField] private float archerTurnSpeed = 540f;

    [Header("Attack")]
    [SerializeField] private ParabolicProjectile arrowPrefab;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float damage = 50f;
    [SerializeField] private float arrowTravelTime = 0.45f;
    [SerializeField] private float arrowArcHeight = 1.5f;

    private float nextAttackTime;

    private void Update()
    {
        if (CurrentTarget == null || !HasValidTarget()) 
            return;

        RotateArcher(CurrentTarget);

        if (Time.time < nextAttackTime)
            return;

        Shoot(CurrentTarget);
        nextAttackTime = Time.time + attackCooldown;
    }

    private void Shoot(Enemy target)
    {
        if (arrowPrefab == null || arrowSpawnPoint == null || target == null) 
            return;

        ParabolicProjectile projectile = Instantiate(arrowPrefab, arrowSpawnPoint.position, arrowSpawnPoint.rotation);
        projectile.Launch(target, damage, arrowTravelTime, arrowArcHeight, arrowSpawnPoint.position);
    }

    private void RotateArcher(Enemy target)
    {
        if (archerVisual == null || target.body == null) 
            return;

        Vector3 direction = target.body.worldCenterOfMass - archerVisual.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        archerVisual.rotation = Quaternion.RotateTowards(archerVisual.rotation, targetRotation, archerTurnSpeed * Time.deltaTime);
    }
}