using UnityEngine;

// Springy juice for any HUD element: kicks to its scale, rotation and position that overshoot and wobble back, and
// a target scale it springs to instead of snapping (a selected row growing to 1.05). The one place UI springs live:
// panels call Punch() or SetTargetScale() and never move transforms themselves.
//   - Punch(): the authored kick (scale up, rotate alternating left/right, push along Kick Direction). Kicks stack,
//     so a fast gun keeps the element buzzing; the Max values cap it. Punch(0.5f) is half a kick; Punch(1f, 0.05f)
//     lands 0.05 s later, so elements punched together can land one after another instead of as one block.
//   - KickScale / KickRotation / KickPosition: one kind of kick, for anything else.
//   - SetTargetScale(1.05f): springs to that size (times the element's own scale) and stays there.
//   - SetIdle(degrees, speed, breathe, breatheSpeed): a rock and a scale pulse that fade in and carry on under the kicks
//     until SetIdle(0, ...) fades them out (a hovered button).
// It moves the element's own RectTransform from its rest pose (scale and rotation taken on first use, position when
// a push starts, so elements in a layout group can still use it). On a UI_Shadowed main the shadow follows by itself.
//
// Setup: add to the element (the ammo text, an ability row) and tune. Low damper = more wobble.
//
// GC: none. One UpdateManager subscription (OnLateUpdate), only while it's moving or idling: it unsubscribes once settled.
[DisallowMultipleComponent]
public class UI_Punch : MonoBehaviour
{
    [Header("Punch()")]
    [Tooltip("Scale speed added per Punch: how hard it enlarges the element. 0 turns it off.")]
    [Min(0f)] [SerializeField] private float scaleKick = 4f;

    [Tooltip("Rotation speed (degrees per second) added per Punch, alternating direction each time. 0 turns it off.")]
    [Min(0f)] [SerializeField] private float rotateKick = 300f;

    [Tooltip("Movement speed (canvas units per second) added per Punch, along Kick Direction. 0 turns it off.")]
    [Min(0f)] [SerializeField] private float positionKick = 0f;

    [Tooltip("Which way Punch pushes, as a share of Position Kick on each axis: (0, 1) is a full kick up, (0.5, 0) half a kick right. Negative goes left or down.")]
    [SerializeField] private Vector2 kickDirection = new Vector2(0f, 1f);

    [Header("Limits")]
    [Tooltip("The most the element grows or shrinks around its target scale from kicks (0.3 is 30%).")]
    [Min(0f)] [SerializeField] private float maxScale = 0.3f;

    [Tooltip("The furthest it rotates either way, in degrees.")]
    [Min(0f)] [SerializeField] private float maxAngle = 12f;

    [Tooltip("The furthest it moves from its rest position, in canvas units.")]
    [Min(0f)] [SerializeField] private float maxOffset = 10f;

    [Header("Springs")]
    [Tooltip("How the scale springs back (and to a new target scale). Strength pulls, damper stops; a low damper wobbles.")]
    [SerializeField] private Core_SpringSettings scaleSpring = new Core_SpringSettings(400f, 18f);

    [Tooltip("How the rotation springs back.")]
    [SerializeField] private Core_SpringSettings rotateSpring = new Core_SpringSettings(300f, 14f);

    [Tooltip("How the position springs back.")]
    [SerializeField] private Core_SpringSettings positionSpring = new Core_SpringSettings(400f, 18f);

    [Header("Time")]
    [Tooltip("Keep moving when the game is paused or in hitstop. On for menus and buttons.")]
    [SerializeField] private bool unscaledTime;

    [Tooltip("Seconds SetIdle's rock and pulse take to fade in or out.")]
    [Min(0.01f)] [SerializeField] private float idleFade = 0.2f;

    private const float RestEpsilon = 0.0005f;

    private RectTransform rect;
    private Vector3 restScale = Vector3.one;
    private Vector2 restPosition;
    private Quaternion restRotation = Quaternion.identity;
    private bool ready;
    private bool ticking;
    private bool displaced;

    private float targetScale = 1f;
    private Core_SpringFloatState scale = new Core_SpringFloatState(1f);
    private Core_SpringFloatState angle;
    private Core_SpringVector2State offset;
    private float kickSign = 1f;

    // A punch waiting for its delay
    private float pendingStrength;
    private float pendingTime;

    // The idle rock and pulse
    private float idleDegrees;
    private float idleSpeed;
    private float idleBreathe;
    private float idleBreatheSpeed;
    private float idleBlend;
    private float idleBlendTarget;
    private float idleTime;

    public float TargetScale => targetScale;

    private void Awake()
    {
        Init();
    }

    private void OnDisable()
    {
        StopTicking();
        pendingStrength = 0f;

        // Back to rest at the target size, so a hidden element isn't left mid-wobble
        if (!ready)
            return;

        scale = new Core_SpringFloatState(targetScale);
        angle = default;
        offset = default;
        idleBlend = 0f;
        idleBlendTarget = 0f;
        Apply();
    }

    // Callers can use this before its Awake, so the rest pose is taken on first use
    private void Init()
    {
        if (ready)
            return;

        ready = true;
        rect = transform as RectTransform;
        restScale = transform.localScale;
        restRotation = transform.localRotation;
    }

    // The authored kick, times strength, after delay seconds (0 is now). Delayed punches made before the first lands
    // add to it.
    public void Punch(float strength, float delay)
    {
        if (delay <= 0f)
        {
            Punch(strength);
            return;
        }

        Init();

        if (pendingStrength <= 0f)
            pendingTime = delay;

        pendingStrength += strength;
        StartTicking();
    }

    // The authored kick, times strength
    public void Punch(float strength = 1f)
    {
        KickScale(scaleKick * strength);
        KickRotation(rotateKick * strength * kickSign);
        KickPosition(kickDirection * (positionKick * strength));
        kickSign = -kickSign;
    }

    public void KickScale(float speed)
    {
        Init();
        scale.velocity += speed;
        StartTicking();
    }

    // Degrees per second; + is anticlockwise
    public void KickRotation(float speed)
    {
        Init();
        angle.velocity += speed;
        StartTicking();
    }

    public void KickPosition(Vector2 velocity)
    {
        Init();

        if (velocity == Vector2.zero)
            return;

        // The rest position is taken when a push starts, so a layout group can still place this between pushes
        if (!displaced && rect != null)
        {
            restPosition = rect.anchoredPosition;
            displaced = true;
        }

        offset.velocity += velocity;
        StartTicking();
    }

    // Springs to this size (a multiple of the element's own scale) and stays; snap jumps there at once
    public void SetTargetScale(float value, bool snap = false)
    {
        Init();
        targetScale = value;

        if (snap)
        {
            scale = new Core_SpringFloatState(value);
            Apply();
            return;
        }

        StartTicking();
    }

    // A rock of this many degrees either way (speed in radians per second) and a scale pulse (0.03 is 3%). Fades in, and
    // carries on until called with 0 degrees and 0 breathe, which fades it out.
    public void SetIdle(float degrees, float speed, float breathe, float breatheSpeed)
    {
        Init();

        if (degrees > 0f || breathe > 0f)
        {
            idleDegrees = degrees;
            idleSpeed = speed;
            idleBreathe = breathe;
            idleBreatheSpeed = breatheSpeed;
            idleBlendTarget = 1f;
        }
        else
        {
            idleBlendTarget = 0f;
        }

        StartTicking();
    }

    private void StartTicking()
    {
        if (ticking || !isActiveAndEnabled || UpdateManager.Instance == null)
            return;

        ticking = true;
        UpdateManager.Instance.OnLateUpdate += Tick;
    }

    private void StopTicking()
    {
        if (!ticking)
            return;

        ticking = false;

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= Tick;
    }

    private void Tick()
    {
        float deltaTime = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        idleBlend = Mathf.MoveTowards(idleBlend, idleBlendTarget, deltaTime / idleFade);
        idleTime += deltaTime;

        if (pendingStrength > 0f)
        {
            pendingTime -= deltaTime;

            if (pendingTime <= 0f)
            {
                float strength = pendingStrength;
                pendingStrength = 0f;
                Punch(strength);
            }
        }

        Core_Math.Spring(ref scale, targetScale, scaleSpring, deltaTime);
        Core_Math.Spring(ref angle, 0f, rotateSpring, deltaTime);
        Core_Math.Spring(ref offset, Vector2.zero, positionSpring, deltaTime);

        // Capped, and the speed is dropped at the cap so a long burst doesn't pile up past it
        float stretch = scale.value - targetScale;

        if (stretch > maxScale) { scale.value = targetScale + maxScale; scale.velocity = Mathf.Min(scale.velocity, 0f); }
        if (stretch < -maxScale) { scale.value = targetScale - maxScale; scale.velocity = Mathf.Max(scale.velocity, 0f); }
        if (angle.value > maxAngle) { angle.value = maxAngle; angle.velocity = Mathf.Min(angle.velocity, 0f); }
        if (angle.value < -maxAngle) { angle.value = -maxAngle; angle.velocity = Mathf.Max(angle.velocity, 0f); }

        float distance = offset.value.magnitude;

        if (distance > maxOffset && distance > 0f)
        {
            Vector2 outward = offset.value / distance;
            offset.value = outward * maxOffset;
            offset.velocity -= outward * Mathf.Max(0f, Vector2.Dot(offset.velocity, outward));
        }

        // Settled: land exactly on rest and stop ticking until the next kick
        if (pendingStrength <= 0f && idleBlend <= 0f && idleBlendTarget <= 0f && Mathf.Abs(scale.value - targetScale) < RestEpsilon && Mathf.Abs(scale.velocity) < RestEpsilon * 10f
            && Mathf.Abs(angle.value) < RestEpsilon && Mathf.Abs(angle.velocity) < RestEpsilon * 10f
            && offset.value.sqrMagnitude < RestEpsilon && offset.velocity.sqrMagnitude < RestEpsilon)
        {
            scale = new Core_SpringFloatState(targetScale);
            angle = default;
            offset = default;
            StopTicking();
        }

        Apply();
    }

    private void Apply()
    {
        float rock = idleBlend * idleDegrees * Mathf.Sin(idleTime * idleSpeed);
        float breathe = 1f + idleBlend * idleBreathe * Mathf.Sin(idleTime * idleBreatheSpeed);

        transform.localScale = restScale * (scale.value * breathe);
        transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, angle.value + rock);

        // Position is only touched while pushed, then put back once, so it never fights a layout group
        if (rect != null && displaced)
        {
            rect.anchoredPosition = restPosition + offset.value;

            if (offset.value == Vector2.zero && offset.velocity == Vector2.zero)
                displaced = false;
        }
    }
}
