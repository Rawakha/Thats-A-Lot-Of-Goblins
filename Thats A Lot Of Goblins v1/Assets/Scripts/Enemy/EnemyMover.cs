using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class EnemyMover : MonoBehaviour
{
    public static EnemyMover Instance;

    [SerializeField] private FlowField flowField;
    [SerializeField] private List<Enemy> enemies = new();

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float acceleration = 15f;
    [SerializeField, Range(0.001f, 0.1f)] private float steerEpsilonFraction = 0.02f;

    [Header("Rotation")]
    [SerializeField, Range(0.01f, 0.3f)] private float turnThresholdFraction = 0.1f;
    [SerializeField] private float turnDegreesPerSecond = 180f;

    [Header("Animation Movement")]
    [SerializeField] private float animationStopSpeed = 0.12f;

    private float minTurnSpeedSqr;
    private float epsilonSqr;

    private void Awake()
    {
        Utilities.CreateInstance<EnemyMover>(ref Instance, this);
        RecalculateThresholds();
    }

#if UNITY_EDITOR
    private void OnValidate() => RecalculateThresholds();
#endif

    private void FixedUpdate()
    {
        if (flowField == null)
            return;

        float currentTime = Time.time;
        float dt = Time.fixedDeltaTime;
        float maxDelta = acceleration * dt;
        float turnDegrees = turnDegreesPerSecond * dt;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];

            if (e == null || e.body == null) 
                continue;

            Vector3 currentPosition = e.body.position;
            Vector3 travelled = currentPosition - e.previousMovementPosition;
            travelled.y = 0f;

            e.previousMovementPosition = currentPosition;
            e.animationVelocity = travelled / dt;
            e.animationSpeed = e.animationVelocity.magnitude;

            if (e.animationSpeed < animationStopSpeed)
            {
                e.animationVelocity = Vector3.zero;
                e.animationSpeed = 0f;
            }

            if (currentTime < e.movementPausedUntil)
            {
                e.animationVelocity = Vector3.zero;
                e.animationSpeed = 0f;
                e.moveDelta = Vector3.zero;
                e.turnRate = 0f;
                continue;
            }

            Vector3 velocity = e.body.linearVelocity;
            velocity.y = 0f;

            e.speed = velocity.magnitude;
            e.maxSpeed = Mathf.Max(0.01f, moveSpeed * e.speedMultiplier);

            // Movement
            Vector3 flow = flowField.Sample(e.body.position);
            Vector3 jittered = new Vector3(flow.x * e.steerCos - flow.z * e.steerSin, 0f, flow.x * e.steerSin + flow.z * e.steerCos);
            Vector3 desired = jittered * e.maxSpeed;
            Vector3 delta = desired - velocity;
            delta.y = 0f;

            if (delta.sqrMagnitude > maxDelta * maxDelta)
                delta = delta.normalized * maxDelta;

            if (delta.sqrMagnitude >= epsilonSqr)
            {
                e.moveDelta = delta;
                e.body.AddForce(delta, ForceMode.VelocityChange);
            }
            else
            {
                e.moveDelta = Vector3.zero;
            }

            // Rotation
            if (e.animationSpeed > animationStopSpeed && jittered.sqrMagnitude > 0.001f)
            {
                Quaternion previousFacing = e.facing;
                Quaternion targetFacing = Quaternion.LookRotation(jittered.normalized, Vector3.up);
                e.facing = Quaternion.RotateTowards(e.facing, targetFacing, turnDegrees);

                float signedTurnDelta = Vector3.SignedAngle(previousFacing * Vector3.forward, e.facing * Vector3.forward, Vector3.up);
                e.turnRate = signedTurnDelta / dt;
            }
            else
            {
                e.turnRate = 0f;
            }
        }
    }

    private void RecalculateThresholds()
    {
        float t = moveSpeed * turnThresholdFraction;
        minTurnSpeedSqr = t * t;

        float eps = moveSpeed * steerEpsilonFraction;
        epsilonSqr = eps * eps;
    }

    public void Add(Enemy enemy)
    {
        if (enemy == null || enemy.inMover)
            return;

        enemies.Add(enemy);
        enemy.inMover = true;
        enemy.moverIndex = enemies.Count - 1;
        enemy.previousMovementPosition = enemy.body.position;
        enemy.animationVelocity = Vector3.zero;
        enemy.animationSpeed = 0f;
    }

    public void Remove(Enemy enemy)
    {
        if (enemy == null || !enemy.inMover)
            return;

        int idx = enemy.moverIndex;

        if (idx < 0 || idx >= enemies.Count || enemies[idx] != enemy)
        {
            Debug.LogError($"EnemyMover: staled moverIndex on {enemy.name}", enemy);

            enemies.Remove(enemy);
            enemy.inMover = false;
            enemy.moverIndex = -1;
            return;
        }
 
        int last = enemies.Count - 1;

        if (idx != last)
        {
            enemies[idx] = enemies[last];
            enemies[idx].moverIndex = idx;
        }

        enemies.RemoveAt(last);
        enemy.inMover = false;
        enemy.moverIndex = -1;
    }
}