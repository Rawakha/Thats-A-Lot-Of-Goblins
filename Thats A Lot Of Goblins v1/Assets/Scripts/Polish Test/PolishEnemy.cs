using DG.Tweening;
using MoreMountains.Feedbacks;
using UnityEngine;

public class PolishEnemy : MonoBehaviour
{
    public enum SquashAxis
    {
        XtoYZ,      // X gets curve, Y and Z get inverse
        YtoXZ,      // Y gets curve, X and Z get inverse
        ZtoXY,      // Z gets curve, X and Y get inverse
        Uniform     // all three get curve
    }

    [System.Serializable]
    public struct SquashSettings
    {
        public bool use;
        public SquashAxis axis;
        [Min(0.01f)] public float duration;
        public float remapZero;
        public float remapOne;
        public AnimationCurve curve;
    }


    [System.Serializable]
    public struct FlashSettings
    {
        public bool use;
        [Min(0.01f)] public float duration;
        public float remapZero;
        public float remapOne;
        public AnimationCurve curve;
    }

    [SerializeField] private Rigidbody rb;

    [Header("Movement")]
    [SerializeField] private bool followFlowField = true;
    [SerializeField] private FlowField flowField;
    [SerializeField] private Transform targetPosition;
    [SerializeField] private float reachedDistance = 0.1f;
    [SerializeField] private float moveSpeed = 2f;

    [Header("Flow Field Movement")]
    [SerializeField] private float flowAcceleration = 15f;
    [SerializeField, Range(0.001f, 0.1f)] private float flowSteerEpsilonFraction = 0.02f;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 0f;

    [Header("Rotation")]
    [SerializeField] private float turnDegreesPerSecond = 540f;
    [SerializeField] private float minTurnSpeed = 0.3f;

    [Header("Knockback")]
    [SerializeField] private float knockbackStunDuration = 0.2f;
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float knockbackUpwardBias = 0.2f;

    [Header("Feedbacks")]
    [SerializeField] private Transform visual;
    [SerializeField] private MeshRenderer renderer;

    [Header("MMF Feedback")]
    [SerializeField] private MMF_Player feedback;
    [SerializeField] private bool playFeedback = true;

    [Header("Hit Feedback")]
    [SerializeField] private SquashSettings squash;
    [SerializeField] private FlashSettings flash;
    [SerializeField] private SoundData hitSoundData;
    [SerializeField] private ParticleSystem hitParticlePrefab;

    [Header("Squash Spring")]
    [SerializeField] private bool useSpring = false;
    [SerializeField] private float stiffness = 200f;
    [SerializeField] private float damping = 15f;
    [SerializeField] private float restValue = 1f;
    [SerializeField] private float impactStrength = 10f;

    [Header("Death Feedback")]
    [SerializeField] private float deathDuration = 0.35f;
    [SerializeField] private Ease deathEase = Ease.InBack;
    [SerializeField] private ParticleSystem deathParticlePrefab;

    [Header("Respawn")]
    [SerializeField] private bool autoRespawn = true;
    [SerializeField] private float respawnDelay = 1.5f;
    [SerializeField] private float dropHeight = 8f;

    [Header("Movement Animation")]
    [SerializeField] private float fullAnimationSpeed = 2f;
    [SerializeField] private float animationBlendSpeed = 8f;
    [SerializeField] private float strideCyclesPerMetre = 1.2f;
    [SerializeField] private float waddleAngle = 8f;
    [SerializeField] private float bounceHeight = 0.04f;
    [SerializeField] private float yawSwingAngle = 3f;

    [Header("Lean")]
    [SerializeField] private float forwardLeanAngle = 7f;
    [SerializeField] private float maxTurningLeanAngle = 10f;
    [SerializeField] private float turnRateForFullLean = 240f;
    [SerializeField] private float turningLeanSpeed = 60f;

    [Header("Movement Squash")]
    [SerializeField, Range(0f, 0.15f)] private float landingSquashAmount = 0.10f;
    [SerializeField, Range(0f, 0.08f)] private float risingStretchAmount = 0.025f;
    [SerializeField, Range(1f, 16f)] private float landingSquashSharpness = 3f;
    [SerializeField, Range(1f, 8f)] private float risingStretchSharpness = 2f;

    private Quaternion facing = Quaternion.identity;

    private bool isKnockedback;
    private float knockbackElapsed;

    private bool isSquashing;
    private float squashElapsed;
    private Vector3 squashLocalDir = Vector3.forward;

    private float springValue = 1f;
    private float springVelocity;

    private bool isFlashing;
    private float flashElapsed;

    private bool isDying;
    private float deathElapsed;
    private Vector3 deathStartScale;

    private Vector3 startPos;
    private Vector3 spawnPoint => new Vector3(startPos.x, dropHeight, startPos.z);
    private float respawnAt;
    private bool isWaitingToRespawn;

    private float movementPhase;
    private float movementAnimationWeight;
    private Vector3 visualStartPosition;
    private Quaternion visualStartRotation;

    private float currentTurningLean;

    private Vector3 feedbackScale = Vector3.one;
    private Vector3 movementScale = Vector3.one;

    private static MaterialPropertyBlock block;
    private static readonly int EmissionValueId = Shader.PropertyToID("_EmissionValue");

    private void Awake()
    {
        startPos = rb.position;
        currentHealth = maxHealth;

        visualStartPosition = visual.localPosition;
        visualStartRotation = visual.localRotation;

        movementPhase = Random.value * Mathf.PI * 2f;
        facing = transform.rotation;
    }

    private void Update()
    {
        if (isWaitingToRespawn)
        {
            if (Time.time >= respawnAt && autoRespawn)
                Respawn();

            return;
        }

        if (isDying)
        {
            deathElapsed += Time.deltaTime;

            float t = Mathf.Clamp01(deathElapsed / deathDuration);
            float eased = DOVirtual.EasedValue(1f, 0f, t, deathEase);

            visual.localScale = deathStartScale * eased;

            if (t >= 1f)
            {
                isDying = false;
                isWaitingToRespawn = true;
                respawnAt = Time.time + respawnDelay;
                visual.localScale = Vector3.zero;

                rb.position = spawnPoint;
                transform.position = spawnPoint;
            }
        }
        else if (useSpring)
        {
            float dt = Time.deltaTime;

            float force = (restValue - springValue) * stiffness - springVelocity * damping;
            springVelocity += force * dt;
            springValue += springVelocity * dt;

            float primary = springValue;
            float secondary = primary > 0.0001f ? 1f / Mathf.Sqrt(primary) : 1f;
            feedbackScale = new Vector3(secondary, primary, secondary);
        }
        else if (isSquashing)
        {
            squashElapsed += Time.deltaTime;

            if (squashElapsed >= squash.duration)
            {
                isSquashing = false;
                feedbackScale = Vector3.one;
            }
            else
            {
                Vector3 multiplier = EvaluateDirectionalSquash(squash, squashElapsed, squashLocalDir);
                feedbackScale = Vector3.Scale(Vector3.one, multiplier);
            }
        }

        if (isFlashing)
        {
            flashElapsed += Time.deltaTime;

            if (flashElapsed >= flash.duration)
            {
                isFlashing = false;
                SetMaterialEmmision(renderer, 0f);
            }
            else
            {
                float value = EvaluateFlash(flash, flashElapsed);
                SetMaterialEmmision(renderer, value);
            }
        }

        if (isKnockedback)
        {
            knockbackElapsed += Time.deltaTime;

            if (knockbackElapsed >= knockbackStunDuration)
            {
                isKnockedback = false;
            }
        }

        UpdateMovementAnimation(Time.deltaTime);

        // Apply Combined Scale
        visual.localScale = Vector3.Scale(feedbackScale, movementScale);
    }

    private void FixedUpdate()
    {
        if (isDying || isWaitingToRespawn)
            return;

        if (followFlowField)
            MoveWithFlowField();
        else
            MoveToPosition();
    }

    public void Damage(float damage, Vector3 hitPos)
    {
        if (isDying)
            return;

        Vector3 local = visual.InverseTransformDirection(GetHitDirection(hitPos));
        local.y = 0f;
        squashLocalDir = local.sqrMagnitude > 0.001f ? local.normalized : Vector3.zero;

        currentHealth = Mathf.Max(currentHealth - damage, 0f);

        if (currentHealth <= 0f)
        {
            Die(hitPos);
        }

        ApplyKnockback(hitPos);
        PlayDamageFeedbacks(hitPos);
    }

    public void Die(Vector3 hitPos)
    {
        if (isDying)
            return;

        isDying = true;
        deathElapsed = 0f;
        deathStartScale = visual.localScale;

        isSquashing = false;

        if (deathParticlePrefab != null)
        {
            ParticleSystem p = Instantiate(deathParticlePrefab, rb.worldCenterOfMass, Quaternion.LookRotation(-GetHitDirection(hitPos)));
            p.gameObject.SetActive(true);
            p.Play();
        }
    }

    private void Respawn()
    {
        isWaitingToRespawn = false;

        rb.isKinematic = false;
        rb.position = spawnPoint;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = spawnPoint;

        currentHealth = maxHealth;
        springValue = 1f;
        springVelocity = 0f;
        isSquashing = false;
        isFlashing = false;
        isKnockedback = false;

        movementPhase = Random.value * Mathf.PI * 2f;
        movementAnimationWeight = 0f;

        visual.localPosition = visualStartPosition;
        visual.localRotation = visualStartRotation;

        feedbackScale = Vector3.one;
        movementScale = Vector3.one;

        facing = transform.rotation;

        visual.localScale = Vector3.one;
        SetMaterialEmmision(renderer, 0f);
    }

    private void MoveToPosition()
    {
        if (targetPosition == null || isKnockedback)
            return;

        // Move
        float distance = Vector3.Distance(targetPosition.position, rb.position);
        if (distance > reachedDistance)
        {
            Vector3 direction = (targetPosition.position - rb.position).normalized;
            rb.AddForce(direction * moveSpeed, ForceMode.Force);
        }
    }

    private void MoveWithFlowField()
    {
        if (flowField == null || isKnockedback)
            return;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;

        Vector3 flow = flowField.Sample(rb.position);
        flow.y = 0f;

        Vector3 desiredVelocity = flow * moveSpeed;
        Vector3 velocityDelta = desiredVelocity - velocity;
        velocityDelta.y = 0f;

        float maxDelta = flowAcceleration * Time.fixedDeltaTime;
        if (velocityDelta.sqrMagnitude > maxDelta * maxDelta)
            velocityDelta = velocityDelta.normalized * maxDelta;

        float epsilon = moveSpeed * flowSteerEpsilonFraction;
        if (velocityDelta.sqrMagnitude >= epsilon * epsilon)
            rb.AddForce(velocityDelta, ForceMode.VelocityChange);
    }

    private void UpdateMovementAnimation(float deltaTime)
    {
        Vector3 planarVelocity = rb.linearVelocity;
        planarVelocity.y = 0f;

        float speed = planarVelocity.magnitude;
        bool isMoving = !isDying && !isWaitingToRespawn && !isKnockedback && speed > 0.05f;

        float targetWeight = isMoving ? Mathf.InverseLerp(0.05f, fullAnimationSpeed * 0.4f, speed) : 0f;

        movementAnimationWeight = Mathf.MoveTowards(movementAnimationWeight, targetWeight, animationBlendSpeed * deltaTime);

        float targetTurningLean = 0f;

        if (isMoving && speed > minTurnSpeed)
        {
            Vector3 previousForward = facing * Vector3.forward;
            Quaternion targetFacing = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);

            facing = Quaternion.RotateTowards(facing, targetFacing, turnDegreesPerSecond * deltaTime);

            Vector3 newForward = facing * Vector3.forward;
            float signedTurnDelta = Vector3.SignedAngle(previousForward, newForward, Vector3.up);
            float turnRate = signedTurnDelta / Mathf.Max(deltaTime, 0.0001f);
            float normalizedTurnRate = Mathf.Clamp(turnRate / turnRateForFullLean, -1f, 1f);

            targetTurningLean = -normalizedTurnRate * maxTurningLeanAngle * movementAnimationWeight;
        }

        currentTurningLean = Mathf.MoveTowards(currentTurningLean, targetTurningLean, turningLeanSpeed * deltaTime);

        // Advance based on distance travelled
        if (isMoving)
        {
            movementPhase += speed * strideCyclesPerMetre * Mathf.PI * 2f * deltaTime;
            movementPhase = Mathf.Repeat(movementPhase, Mathf.PI * 2f);
        }

        float stepWave = Mathf.Sin(movementPhase);
        float bounceWave = Mathf.Abs(stepWave);

        UpdateVerticalBounce(bounceWave);
        UpdateMovementSquash(bounceWave);
        UpdateVisualRotation(stepWave);
    }

    private void UpdateVerticalBounce(float bounceWave)
    {
        float verticalOffset = bounceWave * bounceHeight * movementAnimationWeight;
        visual.localPosition = visualStartPosition + Vector3.up * verticalOffset;
    }

    private void UpdateVisualRotation(float stepWave)
    {
        float yawSwing = stepWave * yawSwingAngle * movementAnimationWeight;
        float sideWaddle = -stepWave * waddleAngle * movementAnimationWeight;
        float forwardLean = forwardLeanAngle * movementAnimationWeight;
        float totalSideLean = sideWaddle + currentTurningLean;

        Quaternion movementRotation = Quaternion.Euler(forwardLean, yawSwing, totalSideLean);
        Quaternion parentWorldRotation = visual.parent != null ? visual.parent.rotation : Quaternion.identity;
        Quaternion localFacing = Quaternion.Inverse(parentWorldRotation) * facing;

        visual.localRotation = localFacing * visualStartRotation * movementRotation;
    }

    private void UpdateMovementSquash(float bounceWave)
    {
        float landingPulse = Mathf.Pow(1f - bounceWave, landingSquashSharpness);
        float risingStretch = Mathf.Pow(bounceWave, risingStretchSharpness);

        float verticalScale = 1f - landingPulse * landingSquashAmount + risingStretch * risingStretchAmount;
        verticalScale = Mathf.Lerp(1f, verticalScale, movementAnimationWeight);

        float horizontalScale = 1f / Mathf.Sqrt(verticalScale);

        movementScale = new Vector3(horizontalScale, verticalScale, horizontalScale);
    }

    private void ApplyKnockback(Vector3 hitPos)
    {
        isKnockedback = true;
        knockbackElapsed = 0f;

        Vector3 dir = (GetHitDirection(hitPos) + Vector3.up * knockbackUpwardBias).normalized;
        rb.AddForce(dir * knockbackForce, ForceMode.VelocityChange);
    }

    private void PlayDamageFeedbacks(Vector3 hitPos)
    {
        if (useSpring)
        {
            springVelocity -= impactStrength;
        }
        else if (squash.use)
        {
            squashElapsed = 0f;
            isSquashing = true;
        }

        if (flash.use)
        {
            flashElapsed = 0f;
            isFlashing = true;
        }

        if (feedback != null && playFeedback)
        {
            feedback.PlayFeedbacks();
        }

        if (hitSoundData != null)
        {
            Utilities.TryPlaySound(hitSoundData, transform.position, additive: true);
        }

        if (hitParticlePrefab != null && !isDying)
        {
            ParticleSystem s = Instantiate(hitParticlePrefab, hitPos, Quaternion.LookRotation(-GetHitDirection(hitPos)));
            s.gameObject.SetActive(true);
            s.Play();
        }
    }

    private Vector3 EvaluateSquash(SquashSettings s, float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / s.duration);
        float curveValue = s.curve.Evaluate(t);

        float primary = Mathf.LerpUnclamped(s.remapZero, s.remapOne, curveValue);
        float secondary = primary > 0.0001f ? 1f / Mathf.Sqrt(primary) : 1f;

        switch (s.axis)
        {
            case SquashAxis.XtoYZ: return new Vector3(primary, secondary, secondary);
            case SquashAxis.YtoXZ: return new Vector3(secondary, primary, secondary);
            case SquashAxis.ZtoXY: return new Vector3(secondary, secondary, primary);
            default: return new Vector3(primary, primary, primary);
        }
    }

    private Vector3 EvaluateDirectionalSquash(SquashSettings s, float elapsed, Vector3 localDir)
    {
        float t = Mathf.Clamp01(elapsed / s.duration);
        float curveValue = s.curve.Evaluate(t);

        float primary = Mathf.LerpUnclamped(s.remapZero, s.remapOne, curveValue);
        float secondary = primary > 0.0001f ? 1f / Mathf.Sqrt(primary) : 1f;

        float xWeight = Mathf.Abs(localDir.x);
        float zWeight = Mathf.Abs(localDir.z);

        float scaleX = Mathf.Lerp(secondary, primary, xWeight);
        float scaleZ = Mathf.Lerp(secondary, primary, zWeight);
        float scaleY = secondary;

        return new Vector3(scaleX, scaleY, scaleZ);
    }

    private Vector3 GetHitDirection(Vector3 hitPos)
    {
        Vector3 away = hitPos - rb.worldCenterOfMass;
        away.y = 0f;

        if (away.sqrMagnitude < 0.001f)
        {
            away = -transform.forward;
        }

        return away.normalized;
    }

    private float EvaluateFlash(FlashSettings f, float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / f.duration);
        float curveValue = f.curve.Evaluate(t);
        return Mathf.LerpUnclamped(f.remapZero, f.remapOne, curveValue);
    }

    private void SetMaterialEmmision(MeshRenderer renderer, float v)
    {
        if (renderer == null) return;

        block ??= new MaterialPropertyBlock();

        renderer.GetPropertyBlock(block);
        block.SetFloat(EmissionValueId, v);
        renderer.SetPropertyBlock(block);
    }
}