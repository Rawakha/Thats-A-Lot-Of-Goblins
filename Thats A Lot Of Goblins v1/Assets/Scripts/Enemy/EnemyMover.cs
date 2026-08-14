using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class EnemyMover : MonoBehaviour
{
    public static EnemyMover Instance;

    [SerializeField] private FlowField flowField;
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
        if (flowField == null)
            return;

        float dt = Time.fixedDeltaTime;
        float maxDelta = acceleration * dt;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];

            if (e == null || e.body == null) 
                continue;

            Vector3 desired = flowField.Sample(e.body.position) * moveSpeed;
            Vector3 delta = desired - e.body.linearVelocity;
            delta.y = 0f;

            if (delta.sqrMagnitude > maxDelta * maxDelta)
                delta = delta.normalized * maxDelta;

            e.body.AddForce(delta, ForceMode.VelocityChange);
        }
    }

    public void Add(Enemy enemy)
    {
        if (enemy == null)
            return;

        if (enemy.inMover)
            return;

        enemies.Add(enemy);
        enemy.inMover = true;
    }

    public void Remove(Enemy enemy)
    {
        if (enemy == null)
            return;

        if (!enemy.inMover)
            return;

        enemies.Remove(enemy);
        enemy.inMover = false;
    }
}