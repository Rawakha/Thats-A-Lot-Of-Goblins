using System.Collections.Generic;
using UnityEngine;

public class EnemyAnimator : MonoBehaviour
{
    [SerializeField] private List<Enemy> enemies = new();

    [Header("Distance Culling")]
    [SerializeField] private Transform camGroundPos;
    [SerializeField] private float animateDistance = 25f;

    [Header("Movement Animation")]
    [SerializeField] private float fullAnimationSpeed = 2f;
    [SerializeField] private float animationBlendSpeed = 60f;
    [SerializeField] private float strideCyclesPerMetre = 1.2f;
    [SerializeField] private float waddleAngle = 10f;
    [SerializeField] private float bounceHeight = 0.1f;
    [SerializeField] private float yawSwingAngle = 15f;

    [Header("Lean")]
    [SerializeField] private float forwardLeanAngle = 7f;
    [SerializeField] private float maxTurningLeanAngle = 13f;
    [SerializeField] private float turnRateForFullLean = 180f;
    [SerializeField] private float turningLeanSpeed = 60f;

    [Header("Movement Squash")]
    [SerializeField, Range(0f, 0.15f)] private float landingSquashAmount = 0.1f;
    [SerializeField, Range(0f, 0.08f)] private float risingStretchAmount = 0.04f;
    [SerializeField, Range(1f, 16f)] private float landingSquashSharpness = 3f;
    [SerializeField, Range(1f, 8f)] private float risingStretchSharpness = 3f;

    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = false;

    private float animateDistanceSqr;

    private void Awake()
    {
        animateDistanceSqr = animateDistance * animateDistance;
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
                e.movementPhase += e.animationSpeed * strideCyclesPerMetre * Mathf.PI * 2f * dt;
                e.movementPhase = Mathf.Repeat(e.movementPhase, Mathf.PI * 2f);
                continue;   // keep phase advancing, skip everything else
            }

            UpdateMovementAnimation(dt, e);

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
                    e.visual.SetLocalPositionAndRotation(e.animOffset, e.animRot);
                }

                e.visual.localScale = Vector3.Scale(Vector3.Scale(e.visualBaseScale, e.animScale), e.feedbackScale);
            }
        }
    }

    private void UpdateMovementAnimation(float deltaTime, Enemy e)
    {
        if (e == null)
            return;

        float speed = e.animationSpeed;

        // Animation weight blending
        bool isMoving = e.state == EnemyState.Walking && speed > 0f;
        float targetWeight = isMoving ? Mathf.InverseLerp(0.05f, fullAnimationSpeed, speed) : 0f;
        e.movementAnimationWeight = Mathf.MoveTowards(e.movementAnimationWeight, targetWeight, animationBlendSpeed * deltaTime);

        // Movement lean
        float normalizedTurnRate = Mathf.Clamp(e.turnRate / turnRateForFullLean, -1f, 1f);
        float targetTurningLean = -normalizedTurnRate * maxTurningLeanAngle * e.movementAnimationWeight;
        e.currentTurningLean = Mathf.MoveTowards(e.currentTurningLean, targetTurningLean, turningLeanSpeed * deltaTime);

        // Movement Phase
        if (isMoving)
        {
            e.movementPhase += speed * strideCyclesPerMetre * Mathf.PI * 2f * deltaTime;
            e.movementPhase = Mathf.Repeat(e.movementPhase, Mathf.PI * 2f);
        }

        float stepWave = Mathf.Sin(e.movementPhase);
        float bounceWave = Mathf.Abs(stepWave);

        UpdateVerticalBounce(bounceWave, e);
        UpdateMovementSquash(bounceWave, e);
        UpdateVisualRotation(stepWave, e);
    }

    private void UpdateVerticalBounce(float bounceWave, Enemy e)
    {
        float verticalOffset = bounceWave * bounceHeight * e.movementAnimationWeight;
        e.animOffset = e.visualStartPos + Vector3.up * verticalOffset;
    }

    private void UpdateMovementSquash(float bounceWave, Enemy e)
    {
        float landingPulse = Mathf.Pow(1f - bounceWave, landingSquashSharpness);
        float risingStretch = Mathf.Pow(bounceWave, risingStretchSharpness);

        float verticalScale = 1f - landingPulse * landingSquashAmount + risingStretch * risingStretchAmount;
        verticalScale = Mathf.Lerp(1f, verticalScale, e.movementAnimationWeight);

        float horizontalScale = 1f / Mathf.Sqrt(verticalScale);

        e.animScale = new Vector3(horizontalScale, verticalScale, horizontalScale);
    }

    private void UpdateVisualRotation(float stepWave, Enemy e)
    {
        float yawSwing = stepWave * yawSwingAngle * e.movementAnimationWeight;
        float sideWaddle = -stepWave * waddleAngle * e.movementAnimationWeight;
        float forwardLean = forwardLeanAngle * e.movementAnimationWeight;
        float totalSideLean = sideWaddle + e.currentTurningLean;

        Quaternion movementRotation = Quaternion.Euler(forwardLean, yawSwing, totalSideLean);
        Quaternion parentWorldRotation = e.visual.parent != null ? e.visual.parent.rotation : Quaternion.identity;
        Quaternion localFacing = Quaternion.Inverse(parentWorldRotation) * e.facing;

        e.animRot = localFacing * e.visualStartRot * movementRotation;
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