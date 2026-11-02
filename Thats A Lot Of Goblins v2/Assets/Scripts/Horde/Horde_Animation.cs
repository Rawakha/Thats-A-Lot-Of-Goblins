using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

// One goblin's animation state, kept by its render group in the same order as the goblins. Personality is rolled
// once on spawn; the rest is the hop's clock, the springs and what was measured last frame.
public struct Horde_AnimState
{
    public Unity.Mathematics.Random Random;

    // Personality, rolled on spawn
    public float RateScale;
    public float HeightScale;
    public float RollScale;
    public float PitchScale;
    public float CrouchScale;
    public float SquashScale;
    public float DriftSeed;

    // The hop
    public float Phase;             // 0-1 through the current hop: 0 = landing, then ground, then air
    public float Active;            // 0-1, how much it's hopping
    public float Ground;            // 0-1, on the ground, eased
    public float HopSize;           // this hop's height against the settings
    public float HopRate;           // this hop's timing against the settings
    public float StartWait;         // seconds left before the first hop of a set-off
    public bool Started;
    public bool RollLeft;
    public Core_SpringFloatState Squash;

    // Which way it faces, degrees around y. Turned toward where it's actually going, every frame.
    public float Yaw;

    // Measured from how the root moved
    public bool Measured;
    public float3 LastPosition;
    public float3 Velocity;
    public float3 Acceleration;
    public float YawRate;

    // Lean (x sideways, y forward) and the head's lag behind the body
    public Core_SpringVector3State Lean;
    public Core_SpringFloatState HeadLift;
    public Core_SpringVector3State HeadTilt;    // x pitch, y roll, z yaw, degrees

    // Hit reaction (Coil's HitReaction). Hits are added to the pending values on the main thread (Horde_RenderGroup.Hit)
    // and taken by the job on its next run.
    public float3 PendingHit;                   // world impulse
    public float3 PendingMoment;                // world turning force, about the feet
    public Core_SpringVector3State HitPush;     // offset, in the goblin's own space
    public Core_SpringVector3State HitLean;     // degrees: x pitch, y yaw, z roll
    public float3 HeadKick;                     // head whip waiting for the delay: pitch, roll, yaw degrees/second
    public float HeadTimer;
    public float Flash;                         // 0-1, fading

    // Shared by both eyes. Stored with the existing swap-back state; only Hit/SetEyeGaze change it after spawn.
    public float EyeSeed;
    public float EyeHitTime;
    public float EyeSize;
    public float EyeAsymmetry;                  // signed extra scale: negative = bigger left, positive = bigger right
    public float2 EyeGaze;                      // -1..1, on top of the material's gaze/glances

    // Death: the time it was killed, or -1 alive. The job tips it over and shrinks it along the type's death ease.
    public float DeathTime;
    public float DeathDelay;                    // seconds lying there before the shrink
    public float DeathLength;                   // seconds of shrinking (short for a goblin blown apart)
    public float3 TipAxis;                      // world axis it falls around: away from the killing hit
    public float3 TipDirection;                 // flat world direction it falls in

    // Flying (thrown, launched out of a hole): it tumbles round TumbleAxis; on landing it squishes and springs upright
    public bool Airborne;
    public float3 TumbleAxis;
    public float TumbleSpeed;                   // degrees/second while airborne
    public Core_SpringFloatState Tumble;        // degrees tumbled
    public float PendingLanding;                // downward speed of a landing the job hasn't played yet

    // Thrown with real physics: drawn exactly as its Rigidbody lies (PhysicsRotations) until it gets up, then eased from
    // RecoverFrom to upright over RecoverDuration (RecoverStart -1 when not getting up)
    public bool Physical;

    // How much of its movement animation (hop, squash, lean, hit, head bobble) shows: 1 walking, easing to 0 while
    // it's flying or dead, back to 1 once it's up
    public float Motion;
    public float RecoverStart;
    public float RecoverDuration;
    public quaternion RecoverFrom;

    // Emerging (hopping out of a hole): drawn along an arc from EmergeFrom to EmergeTo (where its body already waits),
    // optionally flipping, then it lands with a squish. EmergeStart is the time it began, or -1 when not emerging.
    public float EmergeStart;
    public float EmergeDuration;
    public float3 EmergeFrom;
    public float3 EmergeTo;
    public float EmergeHeight;
    public float EmergeFlip;                    // degrees turned over the hop (360 = a full flip, landing upright)
    public float3 EmergeAxis;                   // world axis it flips round: square to its travel
    public Core_SpringFloatState Tip;           // degrees tipped

    // A fresh goblin: its personality rolled from the settings, standing still, facing yaw degrees
    public static Horde_AnimState Create(Horde_AnimationSettings settings, uint seed, float yaw)
    {
        Horde_AnimState state = default;
        state.Random = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
        state.Yaw = yaw;

        if (settings != null)
        {
            state.RateScale = 1f + state.Spread(settings.characterVariety);
            state.HeightScale = 1f + state.Spread(settings.characterVariety);
            state.RollScale = 1f + state.Spread(settings.waddleVariety);
            state.PitchScale = 1f + state.Spread(settings.tipVariety);
            state.CrouchScale = 1f + state.Spread(settings.crouchVariety);
            state.SquashScale = 1f + state.Spread(settings.squashVariety);
            state.HopSize = 1f + state.Spread(settings.hopVariety);
            state.HopRate = 1f + state.Spread(settings.hopRateVariety);
        }

        state.DriftSeed = state.Random.NextFloat(0f, 100f);
        state.RollLeft = state.Random.NextBool();
        state.Ground = 1f;
        state.DeathTime = -1f;
        state.EmergeStart = -1f;
        state.RecoverStart = -1f;
        state.Motion = 1f;
        return state;
    }

    // A random amount between -range and range
    public float Spread(float range) => (Random.NextFloat() * 2f - 1f) * range;
}

// The goblins' procedural animation, ported from Coil's Hop and Lean modules into one Burst job. For each goblin it
// measures how its root moved (velocity, acceleration), turns it toward where it's going (facing is only ever data:
// the Rigidbody never rotates), runs the hop clock, and writes the facing and the body and head poses the render
// group draws with:
//   Body: lift and crouch, tip forward to launch and back to land, waddle, lean into movement and turns, and squash
//     (crouch, air stretch, landing wobble on a spring), keeping its volume.
//   Head: a spring that lags the body's bounce and tilt, so it bobbles and stays more upright.
//   Hits (ported from Coil's HitReaction): the body is shoved and tipped about its centre by the hit, a sideways hit
//     twists it, the head whips a moment later, the squash spring is kicked, and a white flash fades (written into
//     the instance colour's alpha for the Goblins/Goblin shader).
//   Emerging: a goblin hopping out of a hole is drawn along a scripted arc from the hole to where its body waits on
//     the ground (eased by EmergeCurve, a baked DOTween ease), optionally flipping, and lands with a squish. Nothing
//     physical goes near the hole, so it can't get stuck.
//   Death: the root tips over away from the killing hit on a spring (a fall with a bounce), then after the delay its
//     scale follows the type's death ease (a DOTween ease baked into DeathCurve), down to nothing.
// Body squash also lowers and raises the head on its own, since the head rides on the body's stretched offset.
//
// Without Animation Settings (Animate off) it only turns, flashes and shrinks the goblins; poses stay as authored.
//
// GC: none. Runs after the roots are read and before the matrices are built, in the render group's chain.
[BurstCompile]
public struct Horde_AnimateJob : IJobParallelFor
{
    // Springs and measurements are stepped with at most this much time, so a hitch can't kick them
    private const float MaxDeltaTime = 0.05f;
    private const float VelocitySmoothing = 20f;
    private const float AccelerationSmoothing = 10f;
    private const float TurnSmoothing = 25f;
    private const float GroundBlendSpeed = 10f;

    // How quickly the movement animation fades out when it's thrown or killed (and back in once it's up), per second
    private const float MotionFade = 10f;

    // Read for movement, then moved by a dead goblin's tip (it pivots on its body's edge, not its feet)
    public NativeArray<float3> RootPositions;
    [Unity.Collections.ReadOnly] public NativeArray<float3> BodyScales;
    [Unity.Collections.ReadOnly] public NativeArray<quaternion> PhysicsRotations;    // each root's real rotation

    [Unity.Collections.ReadOnly] public NativeArray<float> DeathCurve;     // root scale through the shrink, evenly sampled
    [Unity.Collections.ReadOnly] public NativeArray<float> EmergeCurve;    // progress along the hop out of a hole, evenly sampled

    public NativeArray<Horde_AnimState> States;
    public NativeArray<float4> Colours;                                     // .w is written: the hit flash
    [WriteOnly] public NativeArray<quaternion> RootRotations;
    [WriteOnly] public NativeArray<float> RootScales;
    [WriteOnly] public NativeArray<Horde_Pose> BodyPoses;
    [WriteOnly] public NativeArray<Horde_Pose> HeadPoses;

    public Horde_AnimationParams P;
    public bool Animate;
    public float TurnSpeed;         // degrees/second
    public float TurnMinSpeed;      // m/s
    public float TipAngle;          // degrees a killed goblin falls over
    public float TipStiffness;
    public float TipDamping;
    public float TipEdge;           // metres from the root to the body's bottom edge, at body scale 1
    public float TipBounce;         // 0-1, bounce back off the ground at 90°
    public float DeltaTime;
    public float Time;

    public void Execute(int index)
    {
        Horde_AnimState s = States[index];
        float dt = math.min(DeltaTime, MaxDeltaTime);
        RootScales[index] = DeathScale(s);

        if (dt <= 0f)
        {
            RootRotations[index] = s.Physical
                ? PhysicsRotations[index]
                : GetUp(s, Tip(index, s, Tumbled(s, quaternion.RotateY(math.radians(s.Yaw)))));
            Emerge(index, ref s);
            return;
        }

        Measure(ref s, RootPositions[index], dt);

        float3 flat = new float3(s.Velocity.x, 0f, s.Velocity.z);
        float speed = math.length(flat);
        quaternion rotation = Turn(ref s, flat, speed, dt);

        // Killed: falls over away from the hit, rolling over the body's bottom edge so it lands on its side on the
        // ground, and bouncing back off the ground if it falls flat. The rest of the job keeps using the upright
        // facing for its own space.
        if (s.DeathTime >= 0f)
        {
            Core_Math.SpringRatioLight(ref s.Tip, TipAngle, TipStiffness, TipDamping, dt);

            if (s.Tip.value > 90f)
            {
                s.Tip.value = 90f;

                if (s.Tip.velocity > 0f)
                    s.Tip.velocity = -s.Tip.velocity * TipBounce;
            }
        }

        if (s.Physical)
        {
            // Thrown: real physics tumbles it, so draw it exactly as the Rigidbody lies
            RootRotations[index] = PhysicsRotations[index];
        }
        else
        {
            // Hopping out of a hole: drawn along its arc (the body waits at the landing spot); lands with a squish
            if (Emerge(index, ref s))
                s.PendingLanding = math.max(s.PendingLanding, math.max(P.LandingSpeedReference, 1f));

            // Flying without physics: tumbling. Landed: springs upright, and squishes into the squash spring.
            UpdateTumble(ref s, dt);

            // Getting up after a throw: eased from how it came to rest to upright
            if (s.EmergeStart < 0f)
                RootRotations[index] = GetUp(s, Tip(index, s, Tumbled(s, rotation)));

            if (s.RecoverStart >= 0f && Time - s.RecoverStart >= s.RecoverDuration)
                s.RecoverStart = -1f;
        }

        if (!Animate)
        {
            // No reaction to play: just the flash, fading at a fixed rate
            s.PendingHit = float3.zero;
            s.PendingMoment = float3.zero;
            s.Flash = math.max(0f, s.Flash - dt / 0.12f);
            WriteFlash(index, s.Flash);
            States[index] = s;
            return;
        }

        if (math.lengthsq(s.PendingHit) > 0f || math.lengthsq(s.PendingMoment) > 0f)
            TakeHit(ref s, rotation);

        s.Flash = math.max(0f, s.Flash - dt / math.max(P.HitFlashTime, 0.01f));
        WriteFlash(index, s.Flash);

        float speed01 = math.saturate(speed / P.ReferenceSpeed);
        bool grounded = !s.Physical && math.abs(s.Velocity.y) < P.AirborneSpeed;
        bool moving = grounded && speed > P.MinSpeed;

        // Hop clock: a hop in the air finishes and lands even if it stopped; a set-off waits a random moment first
        bool hopping = moving || s.Phase > 0f;

        if (!hopping)
        {
            s.Started = false;
            s.StartWait = 0f;
        }
        else
        {
            if (!s.Started)
            {
                s.Started = true;
                s.StartWait = s.Random.NextFloat() * P.StartDelay;
            }

            if (s.StartWait > 0f)
            {
                s.StartWait -= dt;
                hopping = false;
            }
        }

        s.Active = Core_Math.Damp(s.Active, hopping ? 1f : 0f, P.StartSpeed, dt);
        s.Ground = Core_Math.Damp(s.Ground, grounded ? 1f : 0f, GroundBlendSpeed, dt);

        float groundTime = math.clamp(P.GroundTime * s.CrouchScale, 0.05f, 0.6f);

        if (grounded && hopping)
        {
            // Big goblins hop slower: a hop's time grows with the square root of its height
            float sizeRate = math.pow(math.max(BodyScales[index].y, 0.1f), -0.5f * P.SizeInfluence);
            float rate = math.lerp(P.SlowHopRate, P.FastHopRate, speed01) * s.RateScale * sizeRate * s.HopRate
                         * (1f + Drift(ref s, 0f));
            float next = s.Phase + rate * dt;

            if (next >= 1f)
            {
                // Landing: a squash kick, the waddle swaps sides, and the next hop is a little different
                s.Squash.velocity -= P.LandSquash * s.SquashScale * (0.3f + speed01) * s.Active;
                s.RollLeft = !s.RollLeft;
                s.HopSize = 1f + s.Spread(P.HopVariety);
                s.HopRate = 1f + s.Spread(P.HopRateVariety);
                next = moving ? math.frac(next) : 0f;
            }

            s.Phase = next;
        }

        float amount = s.Active * s.Ground;

        // The hop's shape: crouch on the ground rocking from the landing into the launch, then the arc
        float height = math.lerp(P.SlowHopHeight, P.FastHopHeight, speed01) * s.HeightScale * s.HopSize
                       * (1f + Drift(ref s, 1f));
        float lift;
        float tip;          // 1 tipped forward (launching), -1 tipped back (landing)
        float shape;        // squash: negative flattens, positive stretches

        if (s.Phase < groundTime)
        {
            float t = s.Phase / groundTime;
            float dip = math.sin(t * math.PI);
            lift = -dip * P.CrouchDepth * s.CrouchScale;
            tip = -math.cos(t * math.PI);
            shape = -dip * P.CrouchSquash * s.CrouchScale;
        }
        else
        {
            float t = math.saturate((s.Phase - groundTime) / (1f - groundTime));
            lift = 4f * t * (1f - t) * height;
            tip = math.cos(t * math.PI);
            shape = math.abs(math.sin(t * math.PI * 2f)) * P.AirStretch * s.SquashScale;
        }

        float wobble = Core_Math.SpringRatioLight(ref s.Squash, 0f, P.SquashStiffness, P.SquashDamping, dt);
        float squash = math.clamp(shape * amount + wobble, -0.6f, 0.6f);
        float waddle = (s.RollLeft ? 1f : -1f) * math.sin(s.Phase * math.PI) * P.Roll * s.RollScale;

        // Lean into the movement, back when braking, banked into turns
        float2 lean = Lean(ref s, rotation, speed, dt);

        float bodyPitch = tip * P.Pitch * s.PitchScale * amount + lean.y * P.MaxLean;
        float bodyRoll = waddle * amount - lean.x * P.MaxLean;
        float bodyLift = lift * amount;
        float width = math.rsqrt(math.max(1f + squash, 0.1f));      // flatter is wider: volume kept

        // Hit reaction: the shove and the tip, each springing back
        Vector3 push = Core_Math.SpringRatioLight(ref s.HitPush, Vector3.zero, P.HitPushStiffness, P.HitPushDamping, dt);
        Vector3 hitLean = Vector3.ClampMagnitude(
            Core_Math.SpringRatioLight(ref s.HitLean, Vector3.zero, P.HitLeanStiffness, P.HitLeanDamping, dt), P.HitMaxLean);
        quaternion hitTurn = quaternion.Euler(math.radians(hitLean.x), math.radians(hitLean.y), math.radians(hitLean.z));

        // Flying or dead: every wobble (hop, squash, lean, hit, head bobble) fades out quickly, so it tumbles and falls
        // over stiff; once it's up, it fades back in
        s.Motion = Core_Math.Damp(s.Motion, s.Physical || s.DeathTime >= 0f ? 0f : 1f, MotionFade, dt);
        float alive = s.Motion;

        BodyPoses[index] = Settle(new Horde_Pose
        {
            Position = new float3(push.x, bodyLift + push.y, push.z),
            Rotation = math.mul(hitTurn, quaternion.Euler(math.radians(bodyPitch), 0f, math.radians(bodyRoll))),
            Scale = new float3(width, 1f + squash, width)
        }, alive);

        // The head whips a moment after the body takes the hit
        if (s.HeadTimer > 0f)
        {
            s.HeadTimer -= dt;

            if (s.HeadTimer <= 0f)
            {
                s.HeadTilt.velocity += (Vector3)s.HeadKick;
                s.HeadKick = float3.zero;
            }
        }

        // Head: springs chase the body's lift and (part of) its tilt; the difference is the lag the head shows
        float headLift = Core_Math.SpringRatioLight(ref s.HeadLift, bodyLift, P.HeadStiffness, P.HeadDamping, dt);
        Vector3 tilt = Core_Math.SpringRatioLight(ref s.HeadTilt,
            new Vector3(bodyPitch * P.HeadFollow, bodyRoll * P.HeadFollow, 0f), P.HeadStiffness, P.HeadDamping, dt);
        float headPitch = math.clamp(tilt.x - bodyPitch, -P.HeadMaxTilt, P.HeadMaxTilt);
        float headRoll = math.clamp(tilt.y - bodyRoll, -P.HeadMaxTilt, P.HeadMaxTilt);
        float headYaw = math.clamp(tilt.z, -P.HeadMaxTilt, P.HeadMaxTilt);

        HeadPoses[index] = Settle(new Horde_Pose
        {
            Position = new float3(0f, (headLift - bodyLift) * P.HeadBounce, 0f),
            Rotation = quaternion.Euler(math.radians(headPitch), math.radians(headYaw), math.radians(headRoll)),
            Scale = new float3(1f)
        }, alive);

        States[index] = s;
    }

    // Coil's hit reaction: the hit (in the goblin's own space) shoves the body, tips it about its centre, twists it
    // if it came from the side, queues the head whip and kicks the squash spring
    private void TakeHit(ref Horde_AnimState s, quaternion rotation)
    {
        quaternion toLocal = math.inverse(rotation);
        float3 force = math.mul(toLocal, s.PendingHit);

        s.HitPush.velocity += (Vector3)(force * P.HitPush);

        // Turning about the body's centre instead of the feet: moment - cross(centre, F)
        float3 centre = new float3(0f, P.HitCentreHeight, 0f);
        float3 moment = math.mul(toLocal, s.PendingMoment - math.cross(centre, s.PendingHit));
        s.HitLean.velocity += (Vector3)(moment * P.HitTwist + new float3(0f, force.x * P.HitSideTwist, 0f));

        s.HeadKick += new float3(
            force.z * P.HitHeadPitch - force.y * P.HitHeadNod,     // pushed back: chin up; hit from above: chin down
            -force.x * P.HitHeadRoll,                               // pushed right: head tips right
            force.x * P.HitHeadYaw);                                // pushed right: face turns right
        s.HeadTimer = P.HitHeadDelay;

        // A downward hit squashes fully, a flat one partly
        float horizontal = math.length(force.xz);
        float downward = math.max(0f, -force.y);
        s.Squash.velocity -= P.HitSquash * (horizontal * P.HitFlatSquash + downward);

        s.PendingHit = float3.zero;
        s.PendingMoment = float3.zero;
    }

    private void WriteFlash(int index, float flash)
    {
        float4 colour = Colours[index];
        colour.w = flash;
        Colours[index] = colour;
    }

    // Places a goblin that's hopping out of a hole: along the arc from the hole to its landing spot by the eased
    // progress, the sideways part lagging (squared) so it rises out of the pit before it moves over the rim, flipping
    // if it was given a flip. Returns true on the frame it lands (and stops emerging).
    private bool Emerge(int index, ref Horde_AnimState s)
    {
        if (s.EmergeStart < 0f)
            return false;

        float t = math.saturate((Time - s.EmergeStart) / math.max(s.EmergeDuration, 0.0001f));
        float e = Sample(EmergeCurve, t);

        float3 position = s.EmergeFrom + (s.EmergeTo - s.EmergeFrom) * new float3(e * e, e, e * e);
        position.y += 4f * s.EmergeHeight * e * (1f - e);

        quaternion facing = quaternion.RotateY(math.radians(s.Yaw));
        quaternion flip = quaternion.AxisAngle(s.EmergeAxis, math.radians(s.EmergeFlip * e));

        RootPositions[index] = position;
        RootRotations[index] = math.mul(flip, facing);

        if (t < 1f)
            return false;

        s.EmergeStart = -1f;
        return true;
    }

    // An evenly sampled 0-1 curve at t (0-1); straight through without one
    private static float Sample(NativeArray<float> curve, float t)
    {
        if (curve.Length < 2)
            return t;

        float x = math.saturate(t) * (curve.Length - 1);
        int i = math.min((int)x, curve.Length - 2);
        return math.lerp(curve[i], curve[i + 1], x - i);
    }

    // In the air it spins at its tumble speed. On landing the spin is cut and it springs back to the nearest upright,
    // overshooting a little (a pop), while the landing kicks the squash spring by how hard it came down.
    private void UpdateTumble(ref Horde_AnimState s, float dt)
    {
        if (s.Airborne)
        {
            s.Tumble.value += s.TumbleSpeed * dt;
            s.Tumble.velocity = s.TumbleSpeed;
            return;
        }

        if (s.PendingLanding > 0f)
        {
            if (Animate)
                s.Squash.velocity -= P.LandingSquash * math.saturate(s.PendingLanding / math.max(P.LandingSpeedReference, 0.01f));

            s.Tumble.velocity *= 0.15f;
            s.PendingLanding = 0f;
        }

        if (s.Tumble.value == 0f && s.Tumble.velocity == 0f)
            return;

        // Upright is the nearest whole turn
        s.Tumble.value -= 360f * math.round(s.Tumble.value / 360f);

        if (Animate && P.UprightStiffness > 0f)
            Core_Math.SpringRatioLight(ref s.Tumble, 0f, P.UprightStiffness, P.UprightDamping, dt);
        else
            s.Tumble = default;

        if (math.abs(s.Tumble.value) < 0.05f && math.abs(s.Tumble.velocity) < 0.5f)
            s.Tumble = default;
    }

    // The facing, turned by however far it has tumbled
    private static quaternion Tumbled(in Horde_AnimState s, quaternion facing)
    {
        if (math.abs(s.Tumble.value) < 0.01f)
            return facing;

        return math.mul(quaternion.AxisAngle(s.TumbleAxis, math.radians(s.Tumble.value)), facing);
    }

    // Blends a pose back toward the authored one: 1 keeps it, 0 is still
    private static Horde_Pose Settle(Horde_Pose pose, float amount)
    {
        if (amount >= 1f)
            return pose;

        pose.Position *= amount;
        pose.Rotation = math.slerp(quaternion.identity, pose.Rotation, amount);
        pose.Scale = math.lerp(new float3(1f), pose.Scale, amount);
        return pose;
    }

    // While getting up after a throw: from how it lay to the given upright rotation, easing out (quick, then settling)
    private quaternion GetUp(in Horde_AnimState s, quaternion upright)
    {
        if (s.RecoverStart < 0f)
            return upright;

        float t = math.saturate((Time - s.RecoverStart) / math.max(s.RecoverDuration, 0.0001f));
        float eased = 1f - (1f - t) * (1f - t) * (1f - t);
        return math.slerp(s.RecoverFrom, upright, eased);
    }

    // The facing tipped over by however far it has fallen (a world axis, so the fall ignores its facing). It turns
    // about the body's bottom edge on the side it falls toward, not its feet, so the root is moved to match: a body
    // turned about its middle would sink half its width into the ground.
    private quaternion Tip(int index, in Horde_AnimState s, quaternion facing)
    {
        // A thrown goblin falls over with real physics instead
        if (s.DeathTime < 0f || s.Physical || math.abs(s.Tip.value) < 0.01f)
            return facing;

        quaternion tip = quaternion.AxisAngle(s.TipAxis, math.radians(s.Tip.value));

        // The edge, out from the root along the fall, as far as this goblin's (varied) body is wide
        float3 bodyScale = BodyScales[index];
        float3 edge = s.TipDirection * (TipEdge * (bodyScale.x + bodyScale.z) * 0.5f);

        RootPositions[index] = RootPositions[index] + edge - math.mul(tip, edge);
        return math.mul(tip, facing);
    }

    // Root scale: 1 alive, then down the baked death ease once killed (after the delay)
    private float DeathScale(in Horde_AnimState s)
    {
        if (s.DeathTime < 0f || DeathCurve.Length < 2)
            return 1f;

        float t = math.saturate((Time - s.DeathTime - s.DeathDelay) / math.max(s.DeathLength, 0.0001f)) * (DeathCurve.Length - 1);
        int i = math.min((int)t, DeathCurve.Length - 2);
        return math.max(0f, math.lerp(DeathCurve[i], DeathCurve[i + 1], t - i));
    }

    // Velocity and acceleration, from how the root moved since last frame
    private static void Measure(ref Horde_AnimState s, float3 position, float dt)
    {
        if (!s.Measured)
        {
            s.Measured = true;
            s.LastPosition = position;
            return;
        }

        float3 velocity = (position - s.LastPosition) / dt;
        float3 previous = s.Velocity;
        s.Velocity = math.lerp(s.Velocity, velocity, Core_Math.DampFactor(VelocitySmoothing, dt));
        s.Acceleration = math.lerp(s.Acceleration, (s.Velocity - previous) / dt, Core_Math.DampFactor(AccelerationSmoothing, dt));
        s.LastPosition = position;
    }

    // Turns toward where it's actually moving, at Turn Speed, once it's going fast enough; standing goblins keep
    // their facing. Also measures the turn rate, for the bank.
    private quaternion Turn(ref Horde_AnimState s, float3 flatVelocity, float speed, float dt)
    {
        float before = s.Yaw;

        if (speed > TurnMinSpeed)
        {
            float target = math.degrees(math.atan2(flatVelocity.x, flatVelocity.z));
            float delta = target - s.Yaw;
            delta -= 360f * math.round(delta / 360f);
            float step = TurnSpeed * dt;
            s.Yaw += math.clamp(delta, -step, step);
            s.Yaw -= 360f * math.round(s.Yaw / 360f);
        }

        float turned = s.Yaw - before;
        turned -= 360f * math.round(turned / 360f);
        s.YawRate = Core_Math.Damp(s.YawRate, turned / dt, TurnSmoothing, dt);

        return quaternion.RotateY(math.radians(s.Yaw));
    }

    // The lean, as (sideways, forward) in the goblin's own frame: 1 is full lean
    private float2 Lean(ref Horde_AnimState s, quaternion rotation, float speed, float dt)
    {
        quaternion toLocal = math.inverse(rotation);
        float3 localVelocity = math.mul(toLocal, new float3(s.Velocity.x, 0f, s.Velocity.z));
        float3 localAcceleration = math.mul(toLocal, new float3(s.Acceleration.x, 0f, s.Acceleration.z));

        float2 target = localVelocity.xz / P.LeanReferenceSpeed
                        + localAcceleration.xz / P.AccelerationReference * P.AccelerationWeight;

        float speed01 = math.saturate(speed / P.LeanReferenceSpeed);
        target.x += math.clamp(s.YawRate / P.BankTurnRate * P.BankWeight * speed01, -P.MaxBank, P.MaxBank);

        Vector3 current = Core_Math.SpringRatioLight(ref s.Lean, new Vector3(target.x, target.y, 0f),
            P.LeanStiffness, P.LeanDamping, dt);

        float2 lean = new float2(current.x, current.y);
        float length = math.length(lean);
        return length > 1f ? lean / length : lean;
    }

    // A slow wander, -DriftAmount to DriftAmount, on its own strand of noise for each channel
    private float Drift(ref Horde_AnimState s, float channel)
    {
        if (P.DriftAmount <= 0f)
            return 0f;

        return noise.snoise(new float2(Time * P.DriftSpeed, s.DriftSeed + channel * 17.31f)) * P.DriftAmount;
    }
}
