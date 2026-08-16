using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class EnemyMover : MonoBehaviour
{
    public static EnemyMover Instance;

    [SerializeField] private FlowField flowField;
    [SerializeField] private List<Enemy> enemies;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float acceleration = 15f;

    [Header("Rotation")]
    [SerializeField, Range(0.01f, 0.3f)] private float turnThresholdFraction = 0.1f;
    [SerializeField] private float turnDegreesPerSecond = 180f;

    [Header("Bob")]
    [SerializeField] private float bobHeight = 0.08f;
    [SerializeField] private float bobCyclesPerMeter = 1.2f;
    [SerializeField] private float bobSquash = 0.06f;

    [Header("Lean")]
    [SerializeField] private float leanDegreesPerAccel = 1.5f;
    [SerializeField] private float maxLeanDegrees = 20f;
    [SerializeField] private float leanSmoothing = 8f;

    private float minTurnSpeedSqr;

    private void Awake()
    {
        Utilities.CreateInstance<EnemyMover>(ref Instance, this);

        float t = moveSpeed * turnThresholdFraction;
        minTurnSpeedSqr = t * t;
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

            if (e == null || e.body == null || e.visual == null) 
                continue;

            Vector3 velocity = e.body.linearVelocity;
            velocity.y = 0f;

            // Movement
            Vector3 flow = flowField.Sample(e.body.position);
            Vector3 jittered = new Vector3(flow.x * e.steerCos - flow.z * e.steerSin, 0f, flow.x * e.steerSin + flow.z * e.steerCos);
            Vector3 desired = jittered * (moveSpeed * e.speedMultiplier);
            Vector3 delta = desired - velocity;
            delta.y = 0f;

            if (delta.sqrMagnitude > maxDelta * maxDelta)
                delta = delta.normalized * maxDelta;

            e.body.AddForce(delta, ForceMode.VelocityChange);

            // Rotation
            if (velocity.sqrMagnitude > minTurnSpeedSqr)
            {
                Quaternion target = Quaternion.LookRotation(velocity.normalized, Vector3.up);
                e.facing = Quaternion.RotateTowards(e.facing, target, turnDegreesPerSecond * dt);
            }

            // Lean
            Vector3 accel = delta / dt;
            Vector3 forward = e.facing * Vector3.forward;

            float forwardAccel = Vector3.Dot(accel, forward);
            float sideAccel = Vector3.Dot(accel, Vector3.Cross(Vector3.up, forward));

            float pitch = Mathf.Clamp(-forwardAccel * leanDegreesPerAccel, -maxLeanDegrees, maxLeanDegrees);
            float roll = Mathf.Clamp(-sideAccel * leanDegreesPerAccel, -maxLeanDegrees, maxLeanDegrees);

            e.leanPitch = Mathf.Lerp(e.leanPitch, pitch, leanSmoothing * dt);
            e.leanRoll = Mathf.Lerp(e.leanRoll, roll, leanSmoothing * dt);

            // Compose Rotation + Lean
            e.visual.rotation = e.facing * Quaternion.Euler(e.leanPitch, 0f, e.leanRoll);

            // Bob
            float speed = velocity.magnitude;
            e.bobPhase += speed * bobCyclesPerMeter * e.bobSpeedMul * dt * Mathf.PI * 2f;

            float s = Mathf.Sin(e.bobPhase);
            e.visual.localPosition = new Vector3(0f, Mathf.Abs(s) * bobHeight, 0f);

            // Bob Squash
            float squash = 1f - Mathf.Abs(s) * bobSquash;
            e.visual.localScale = new Vector3(1f + (1f - squash) * 0.5f, squash, 1f + (1f - squash) * 0.5f);
        }
    }

    public void Add(Enemy enemy)
    {
        if (enemy == null || enemy.inMover)
            return;

        enemies.Add(enemy);
        enemy.inMover = true;
        enemy.moverIndex = enemies.Count - 1;
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
        enemies[idx] = enemies[last];
        enemies[idx].moverIndex = idx;
        enemies.RemoveAt(last);

        enemy.inMover = false;
        enemy.moverIndex = -1;
    }
}