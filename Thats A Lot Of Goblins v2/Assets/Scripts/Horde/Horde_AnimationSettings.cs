using UnityEngine;

// How a goblin type moves its body and head, in one asset so several types can share it and it tunes live in play
// mode. Ported from Coil's Hop and Lean modules (the Imps project), plus a head spring:
//   Hop: each hop has a short spell on the ground (crouch and squash, tip forward to launch) and an arc through the
//     air (stretch, rise, tip back to land), with a waddle from side to side. Hops come quicker and higher with
//     speed, land with a squash that wobbles on a spring, and finish when the goblin stops.
//   Variety: every goblin has its own rate, height, waddle, tip, crouch and squash; every hop differs from the last;
//     set-offs wait a random moment and the pace drifts, so a crowd never bounces in step.
//   Lean: the body leans into its movement, back when braking, and banks into turns, on a spring.
//   Head: a looser spring that lags the body's bounce and tilt, so it bobbles after every hop and stays more upright.
// Read by Horde_AnimateJob through Params, so nothing here is used per goblin on the main thread.
[CreateAssetMenu(menuName = "Goblins/Horde/Animation Settings", fileName = "Horde_AnimationSettings")]
public class Horde_AnimationSettings : ScriptableObject
{
    [Header("Cadence")]
    [Tooltip("Speed (m/s) at which hops are at their quickest and highest.")]
    [Min(0.01f)] public float referenceSpeed = 4f;
    [Tooltip("Below this speed (m/s) a goblin stops hopping.")]
    [Min(0f)] public float minSpeed = 0.3f;
    [Tooltip("Hops per second when moving slowly.")]
    [Min(0.1f)] public float slowHopRate = 2.2f;
    [Tooltip("Hops per second at the reference speed.")]
    [Min(0.1f)] public float fastHopRate = 3.2f;
    [Tooltip("How quickly hopping builds up when a goblin sets off, and fades when it stops.")]
    [Min(0.01f)] public float startSpeed = 8f;

    [Header("Shape")]
    [Tooltip("Metres the body rises at the top of a slow hop.")]
    [Min(0f)] public float slowHopHeight = 0.1f;
    [Tooltip("Metres the body rises at the top of a hop at the reference speed.")]
    [Min(0f)] public float fastHopHeight = 0.18f;
    [Tooltip("Share of each hop spent on the ground, crouching before the next launch.")]
    [Range(0.05f, 0.6f)] public float groundTime = 0.3f;
    [Tooltip("Metres the body dips while crouched on the ground.")]
    [Min(0f)] public float crouchDepth = 0.04f;

    [Header("Tip and Waddle")]
    [Tooltip("Degrees the body tips forward as it launches, and back as it lands.")]
    public float pitch = 10f;
    [Tooltip("Degrees the body rolls through each hop, alternating sides.")]
    public float roll = 5f;

    [Header("Squash")]
    [Tooltip("Squash while crouched between hops. The body keeps its volume: flatter is wider.")]
    public float crouchSquash = 0.12f;
    [Tooltip("Stretch through the launch and the drop. Round again at the top of the hop.")]
    public float airStretch = 0.1f;
    [Tooltip("Squash kick on each landing, bigger at speed. It wobbles on the squash spring.")]
    public float landSquash = 0.6f;
    [Tooltip("Squash spring: how quickly the wobble pulls back.")]
    [Min(1f)] public float squashStiffness = 400f;
    [Tooltip("Squash spring bounce: lower wobbles more.")]
    [Range(0.05f, 1.5f)] public float squashDamping = 0.45f;

    [Header("Variety")]
    [Tooltip("How much each goblin's hop rate and height differ, seeded per goblin.")]
    [Range(0f, 0.5f)] public float characterVariety = 0.15f;
    [Tooltip("How much each hop's height differs from the last.")]
    [Range(0f, 0.5f)] public float hopVariety = 0.15f;
    [Tooltip("How much each hop's timing differs from the last.")]
    [Range(0f, 0.5f)] public float hopRateVariety = 0.15f;
    [Tooltip("Seconds, at most, a goblin waits before its first hop when it sets off.")]
    [Min(0f)] public float startDelay = 0.2f;
    [Tooltip("How much body size slows hops: big goblins hop slower. 0 = size makes no difference.")]
    [Range(0f, 1f)] public float sizeInfluence = 1f;
    [Tooltip("How far a goblin's pace drifts over time, either way.")]
    [Range(0f, 0.2f)] public float driftAmount = 0.05f;
    [Tooltip("How quickly the pace drifts.")]
    [Min(0f)] public float driftSpeed = 0.15f;

    [Header("Personality")]
    [Range(0f, 0.5f)] public float waddleVariety = 0.25f;
    [Range(0f, 0.5f)] public float tipVariety = 0.2f;
    [Tooltip("How much each goblin's crouch differs: depth, squash and time on the ground (long reads stompy, short springy).")]
    [Range(0f, 0.5f)] public float crouchVariety = 0.2f;
    [Range(0f, 0.5f)] public float squashVariety = 0.2f;

    [Header("Lean")]
    [Tooltip("Degrees the body leans at full lean.")]
    public float maxLean = 15f;
    [Tooltip("Speed (m/s) that counts as full lean.")]
    [Min(0.01f)] public float leanReferenceSpeed = 6f;
    [Tooltip("How much speeding up and slowing down adds to the lean.")]
    public float accelerationWeight = 0.6f;
    [Tooltip("Acceleration (m/s²) that counts as full lean.")]
    [Min(0.01f)] public float accelerationReference = 30f;
    [Tooltip("How much turning banks the body into the turn.")]
    public float bankWeight = 0.5f;
    [Tooltip("The most the bank can add to the sideways lean.")]
    [Min(0f)] public float maxBank = 0.6f;
    [Tooltip("Turn rate (degrees/second) that counts as a full bank.")]
    [Min(0.01f)] public float bankTurnRate = 180f;
    [Min(1f)] public float leanStiffness = 90f;
    [Range(0.05f, 1.5f)] public float leanDamping = 0.47f;

    [Header("Head")]
    [Tooltip("How much the head lags the body's bounce. 0 = it's carried rigidly.")]
    [Range(0f, 2f)] public float headBounce = 1f;
    [Tooltip("How much of the body's tilt the head ends up following. Lower keeps the head more upright.")]
    [Range(0f, 1f)] public float headFollow = 0.5f;
    [Tooltip("Head spring: lower is floppier.")]
    [Min(1f)] public float headStiffness = 140f;
    [Tooltip("Head spring bounce: lower bobbles more.")]
    [Range(0.05f, 1.5f)] public float headDamping = 0.3f;
    [Tooltip("Furthest the head tilts against the body, degrees.")]
    [Min(0f)] public float headMaxTilt = 25f;

    [Header("Hit: Push")]
    [Tooltip("How far a hit shoves the body, per unit of hit force (metres per second of spring kick).")]
    public float hitPush = 1.5f;
    [Min(1f)] public float hitPushStiffness = 200f;
    [Range(0.05f, 1.5f)] public float hitPushDamping = 0.4f;

    [Header("Hit: Lean and Twist")]
    [Tooltip("Height of the body's centre above its feet, metres. Hits above it tip the body back, below it forward.")]
    public float hitCentreHeight = 0.5f;
    [Tooltip("How hard a hit off-centre spins the body, degrees/second per unit of turning force.")]
    public float hitTwist = 300f;
    [Tooltip("Extra turn toward the way a sideways hit pushes, degrees/second per unit of force.")]
    public float hitSideTwist = 120f;
    [Min(1f)] public float hitLeanStiffness = 150f;
    [Range(0.05f, 1.5f)] public float hitLeanDamping = 0.35f;
    [Tooltip("Furthest a hit tips the body, degrees.")]
    public float hitMaxLean = 25f;

    [Header("Hit: Head Whip")]
    [Tooltip("Degrees/second per unit of force: front hits snap the chin up.")]
    public float hitHeadPitch = 300f;
    [Tooltip("Side hits turn the head away.")]
    public float hitHeadYaw = 250f;
    [Tooltip("Side hits tip the head away.")]
    public float hitHeadRoll = 150f;
    [Tooltip("Hits from above nod the head down; from below snap it back.")]
    public float hitHeadNod = 250f;
    [Tooltip("Seconds after the body before the head reacts, so the body takes the hit first.")]
    [Min(0f)] public float hitHeadDelay = 0.04f;

    [Header("Hit: Squash and Flash")]
    [Tooltip("Squash kicked into the squash spring by a hit. Downward hits squash fully.")]
    public float hitSquash = 1f;
    [Tooltip("How much of the squash a flat (sideways or frontal) hit gives, against a downward one.")]
    [Range(0f, 1f)] public float hitFlatSquash = 0.4f;
    [Tooltip("Seconds the white flash takes to fade after a hit. Needs the Goblins/Goblin shader.")]
    [Min(0.01f)] public float hitFlashTime = 0.12f;

    [Header("Landing")]
    [Tooltip("Squash kicked into the squash spring by a landing (after being thrown or launched out of a hole), at " +
             "Landing Speed Reference or faster. Slower landings squish less.")]
    public float landingSquash = 1.5f;
    [Tooltip("Downward speed (m/s) that counts as a full-strength landing.")]
    [Min(0.01f)] public float landingSpeedReference = 10f;
    [Tooltip("How quickly a tumbling goblin pops upright when it lands.")]
    [Min(1f)] public float uprightStiffness = 300f;
    [Tooltip("Upright spring bounce: lower wobbles more as it pops up.")]
    [Range(0.05f, 1.5f)] public float uprightDamping = 0.4f;

    [Header("Airborne")]
    [Tooltip("Vertical speed (m/s) above which a goblin counts as in the air (thrown, falling): the hop fades out.")]
    [Min(0.1f)] public float airborneSpeed = 2f;

    // A blittable copy for the job, made each frame so changes in play mode show at once
    public Horde_AnimationParams Params => new Horde_AnimationParams
    {
        ReferenceSpeed = referenceSpeed,
        MinSpeed = minSpeed,
        SlowHopRate = slowHopRate,
        FastHopRate = fastHopRate,
        StartSpeed = startSpeed,
        SlowHopHeight = slowHopHeight,
        FastHopHeight = fastHopHeight,
        GroundTime = groundTime,
        CrouchDepth = crouchDepth,
        Pitch = pitch,
        Roll = roll,
        CrouchSquash = crouchSquash,
        AirStretch = airStretch,
        LandSquash = landSquash,
        SquashStiffness = squashStiffness,
        SquashDamping = squashDamping,
        HopVariety = hopVariety,
        HopRateVariety = hopRateVariety,
        StartDelay = startDelay,
        SizeInfluence = sizeInfluence,
        DriftAmount = driftAmount,
        DriftSpeed = driftSpeed,
        MaxLean = maxLean,
        LeanReferenceSpeed = leanReferenceSpeed,
        AccelerationWeight = accelerationWeight,
        AccelerationReference = accelerationReference,
        BankWeight = bankWeight,
        MaxBank = maxBank,
        BankTurnRate = bankTurnRate,
        LeanStiffness = leanStiffness,
        LeanDamping = leanDamping,
        HeadBounce = headBounce,
        HeadFollow = headFollow,
        HeadStiffness = headStiffness,
        HeadDamping = headDamping,
        HeadMaxTilt = headMaxTilt,
        AirborneSpeed = airborneSpeed,
        HitPush = hitPush,
        HitPushStiffness = hitPushStiffness,
        HitPushDamping = hitPushDamping,
        HitCentreHeight = hitCentreHeight,
        HitTwist = hitTwist,
        HitSideTwist = hitSideTwist,
        HitLeanStiffness = hitLeanStiffness,
        HitLeanDamping = hitLeanDamping,
        HitMaxLean = hitMaxLean,
        HitHeadPitch = hitHeadPitch,
        HitHeadYaw = hitHeadYaw,
        HitHeadRoll = hitHeadRoll,
        HitHeadNod = hitHeadNod,
        HitHeadDelay = hitHeadDelay,
        HitSquash = hitSquash,
        HitFlatSquash = hitFlatSquash,
        HitFlashTime = hitFlashTime,
        LandingSquash = landingSquash,
        LandingSpeedReference = landingSpeedReference,
        UprightStiffness = uprightStiffness,
        UprightDamping = uprightDamping
    };
}

public struct Horde_AnimationParams
{
    public float ReferenceSpeed, MinSpeed, SlowHopRate, FastHopRate, StartSpeed;
    public float SlowHopHeight, FastHopHeight, GroundTime, CrouchDepth;
    public float Pitch, Roll;
    public float CrouchSquash, AirStretch, LandSquash, SquashStiffness, SquashDamping;
    public float HopVariety, HopRateVariety, StartDelay, SizeInfluence, DriftAmount, DriftSpeed;
    public float MaxLean, LeanReferenceSpeed, AccelerationWeight, AccelerationReference, BankWeight, MaxBank, BankTurnRate;
    public float LeanStiffness, LeanDamping;
    public float HeadBounce, HeadFollow, HeadStiffness, HeadDamping, HeadMaxTilt;
    public float AirborneSpeed;
    public float HitPush, HitPushStiffness, HitPushDamping;
    public float HitCentreHeight, HitTwist, HitSideTwist, HitLeanStiffness, HitLeanDamping, HitMaxLean;
    public float HitHeadPitch, HitHeadYaw, HitHeadRoll, HitHeadNod, HitHeadDelay;
    public float HitSquash, HitFlatSquash, HitFlashTime;
    public float LandingSquash, LandingSpeedReference, UprightStiffness, UprightDamping;
}
