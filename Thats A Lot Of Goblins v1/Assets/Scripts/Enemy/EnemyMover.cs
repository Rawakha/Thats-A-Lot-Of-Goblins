using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class EnemyMover : MonoBehaviour
{
    public static EnemyMover Instance;

    [SerializeField] private Transform target;
    [SerializeField] private List<Enemy> enemies;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float acceleration = 15f;

    private void Awake()
    {
        Utilities.CreateInstance<EnemyMover>(ref Instance, this);
    }

    private void FixedUpdate()
    {
        if (target == null)
            return;

        float dt = Time.fixedDeltaTime;
        float maxDelta = acceleration * dt;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];

            if (e == null || e.rigidbody == null) 
                continue;

            Vector3 desired = (target.position - e.rigidbody.position).normalized * moveSpeed;
            Vector3 delta = desired - e.rigidbody.linearVelocity;
            delta.y = 0f;

            if (delta.sqrMagnitude > maxDelta * maxDelta)
                delta = delta.normalized * maxDelta;

            e.rigidbody.AddForce(delta, ForceMode.VelocityChange);
        }
    }

    public void Add(Enemy enemy)
    {
        if (enemy == null)
            return;

        if (enemies.Contains(enemy))
            return;

        enemies.Add(enemy);
    }

    public void Remove(Enemy enemy)
    {
        if (enemy == null)
            return;

        enemies.Remove(enemy);
    }
}