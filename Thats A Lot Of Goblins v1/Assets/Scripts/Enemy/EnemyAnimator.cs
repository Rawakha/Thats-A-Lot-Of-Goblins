using System.Collections.Generic;
using UnityEngine;

public class EnemyAnimator : MonoBehaviour
{
    [SerializeField] private List<Enemy> enemies = new();

    [Header("Distance Culling")]
    [SerializeField] private Transform camGroundPos;
    [SerializeField] private float animateDistance = 25f;

    [Header("Bob")]
    [SerializeField] private float bobHeight = 0.08f;
    [SerializeField] private float bobSharpness = 0.5f; // >1 = snappier peaks, <1 = floatier
    [SerializeField] private float bobCyclesPerMeter = 1.2f;
    [SerializeField] private float bobSquash = 0.06f;

    [Header("Lean")]
    [SerializeField] private float leanDegreesPerAccel = 1.5f;
    [SerializeField] private float maxLeanDegrees = 20f;
    [SerializeField] private float leanSmoothing = 8f;

    [Header("Waddle")]
    [SerializeField] private float waddleDegrees = 10f;

    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = false;

    private float animateDistanceSqr;
    private float appliedPi;

    private void Awake()
    {
        animateDistanceSqr = animateDistance * animateDistance;
        appliedPi = Mathf.PI * 2f;
    }

#if UNITY_EDITOR
    private void OnValidate() => animateDistanceSqr = animateDistance * animateDistance;
#endif

    private void Update()
    {
        float dt = Time.deltaTime;
        Vector3 camPos = camGroundPos.position;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];

            if (e == null)
                continue;

            Vector3 toCam = e.body.position - camPos;
            if (toCam.sqrMagnitude > animateDistanceSqr)
            {
                e.bobPhase += e.speed * bobCyclesPerMeter * e.bobSpeedMul * dt * appliedPi;
                continue;   // keep phase advancing, skip everything else
            }

            float speedNormalized = e.maxSpeed > 0.01f ? Mathf.Clamp01(e.speed / e.maxSpeed) : 0f;

            // Lean
            Vector3 accel = e.moveDelta / dt;
            Vector3 forward = e.facing * Vector3.forward;

            float forwardAccel = Vector3.Dot(accel, forward);
            float sideAccel = Vector3.Dot(accel, Vector3.Cross(Vector3.up, forward));

            float pitch = Mathf.Clamp(-forwardAccel * leanDegreesPerAccel, -maxLeanDegrees, maxLeanDegrees);
            float roll = Mathf.Clamp(-sideAccel * leanDegreesPerAccel, -maxLeanDegrees, maxLeanDegrees);

            e.leanPitch = Mathf.Lerp(e.leanPitch, pitch, leanSmoothing * dt);
            e.leanRoll = Mathf.Lerp(e.leanRoll, roll, leanSmoothing * dt);

            // Bob + Wobble
            e.bobPhase += e.speed * bobCyclesPerMeter * e.bobSpeedMul * dt * appliedPi;

            float rawSin = Mathf.Sin(e.bobPhase);
            float bob = Mathf.Pow(Mathf.Abs(rawSin), bobSharpness);
            float waddle = rawSin * waddleDegrees * speedNormalized;

            // Bob Squash
            float squash = 1f - bob * bobSquash;

            // Compose the changes
            Vector3 bobPosition = new Vector3(0f, bob * bobHeight, 0f);

            // Write to accumulators
            e.animScale = new Vector3(1f + (1f - squash) * 0.5f, squash, 1f + (1f - squash) * 0.5f);
            e.animOffset = bobPosition;

            // Apply accumulators
            if (e.state == EnemyState.Dying)
            {
                e.visual.localScale = Vector3.Scale(e.visualBaseScale, e.feedbackScale);
            }
            else
            {
                if (e.state == EnemyState.Airborne)
                {
                    e.visual.localRotation = Quaternion.identity;
                    e.visual.localPosition = Vector3.zero;
                }
                else
                {
                    Quaternion animLocal = Quaternion.Euler(e.leanPitch, 0f, e.leanRoll + waddle);
                    e.visual.SetLocalPositionAndRotation(e.animOffset, animLocal);
                }

                e.visual.localScale = Vector3.Scale(Vector3.Scale(e.visualBaseScale, e.animScale), e.feedbackScale);
            }
        }
    }

    public void Add(Enemy e)
    {
        if (e == null || e.inAnimator)
            return;

        enemies.Add(e);
        e.inAnimator = true;
        e.animatorIndex = enemies.Count - 1;
    }

    public void Remove(Enemy e)
    {
        if (e == null || !e.inAnimator)
            return;

        int idx = e.animatorIndex;

        if (idx < 0 || idx >= enemies.Count || enemies[idx] != e)
        {
            Debug.LogError($"EnemyAnimator: staled animatorIndex on {e.name}", e);

            enemies.Remove(e);
            e.inAnimator = false;
            e.animatorIndex = -1;
            return;
        }

        int last = enemies.Count - 1;

        if (idx != last)
        {
            enemies[idx] = enemies[last];
            enemies[idx].animatorIndex = idx;
        }

        enemies.RemoveAt(last);
        e.inAnimator = false;
        e.animatorIndex = -1;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        if (camGroundPos != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(camGroundPos.transform.position, animateDistance);
        }
    }
}