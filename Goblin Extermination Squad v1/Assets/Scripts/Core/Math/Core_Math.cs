using UnityEngine;

// Runtime state for the springs in Core_Math. Keep one per value you spring (in a component, or in a struct array for jobs).
public struct Core_SpringFloatState
{
    public float value;
    public float velocity;

    public Core_SpringFloatState(float value)
    {
        this.value = value;
        velocity = 0f;
    }
}

public struct Core_SpringVector2State
{
    public Vector2 value;
    public Vector2 velocity;

    public Core_SpringVector2State(Vector2 value)
    {
        this.value = value;
        velocity = Vector2.zero;
    }
}

public struct Core_SpringVector3State
{
    public Vector3 value;
    public Vector3 velocity;

    public Core_SpringVector3State(Vector3 value)
    {
        this.value = value;
        velocity = Vector3.zero;
    }
}

public struct Core_SpringRotationState
{
    public Quaternion value;
    public Vector3 angularVelocity;     // radians per second, as axis * speed

    public Core_SpringRotationState(Quaternion value)
    {
        this.value = Core_Math.SafeRotation(value);
        angularVelocity = Vector3.zero;
    }
}

// Shared game maths: smoothing, curves, planar helpers, rotations, springs, two-bone limbs, squash.
// Pure functions only: values in, values out, no Transforms, no state of their own (springs take their state by ref).
// No allocations. The float / Vector2 / Vector3 functions (Damp, Decay, curves, Spring, SpringRatio) are plain
// managed maths and Burst-safe. Anything taking a Quaternion calls engine functions (Slerp, AngleAxis, LookRotation),
// so keep those on the main thread; jobs use Unity.Mathematics quaternions instead. Use these rather than hand-rolling
// a lerp or spring in a system; if two systems need the same calculation, it belongs here.
public static class Core_Math
{
    private const float Epsilon = 0.000001f;

    // Stiff springs are stepped in pieces no longer than this, so they stay stable at low frame rates.
    private const float SpringSubstep = 0.005f;

    // The longest single step the strength / damper springs take. Frames up to 1/30 s are one step;
    // a longer frame is split, because a one-step spring blows up past about 80 ms.
    private const float SpringMaxStep = 1f / 30f;

    #region Smoothing
    // How far to move toward a target this frame for a frame-rate independent ease. Rate is "per second".
    public static float DampFactor(float rate, float deltaTime)
    {
        return 1f - Mathf.Exp(-rate * deltaTime);
    }

    public static float Damp(float current, float target, float rate, float deltaTime)
    {
        return Mathf.Lerp(current, target, DampFactor(rate, deltaTime));
    }

    public static Vector2 Damp(Vector2 current, Vector2 target, float rate, float deltaTime)
    {
        return Vector2.Lerp(current, target, DampFactor(rate, deltaTime));
    }

    public static Vector3 Damp(Vector3 current, Vector3 target, float rate, float deltaTime)
    {
        return Vector3.Lerp(current, target, DampFactor(rate, deltaTime));
    }

    public static Quaternion Damp(Quaternion current, Quaternion target, float rate, float deltaTime)
    {
        return Quaternion.Slerp(current, target, DampFactor(rate, deltaTime));
    }

    // Eases an angle in degrees the short way round.
    public static float DampAngle(float current, float target, float rate, float deltaTime)
    {
        return current + Mathf.DeltaAngle(current, target) * DampFactor(rate, deltaTime);
    }

    // Different speeds going up and coming down, e.g. react quickly, let go slowly.
    public static float DampAsymmetric(float current, float target, float riseRate, float fallRate, float deltaTime)
    {
        return Damp(current, target, target > current ? riseRate : fallRate, deltaTime);
    }

    // Shrinks a value toward zero, e.g. a lunge velocity bleeding off.
    public static float Decay(float value, float rate, float deltaTime)
    {
        return value * Mathf.Exp(-rate * deltaTime);
    }

    public static Vector3 Decay(Vector3 value, float rate, float deltaTime)
    {
        return value * Mathf.Exp(-rate * deltaTime);
    }
    #endregion

    #region Curves
    // Smoothstep: eases in and out, clamped to 0-1.
    public static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // Slow start, fast finish. Higher power = more extreme.
    public static float EaseIn(float t, float power)
    {
        return Mathf.Pow(Mathf.Clamp01(t), power);
    }

    // Fast start, slow finish.
    public static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    // 0 at 'from', 1 at 'to', eased in between. Works either way round (from can be bigger than to).
    public static float SmoothRange(float value, float from, float to)
    {
        return Smooth01(Mathf.InverseLerp(from, to, value));
    }

    // 0-1 progress through a timed phase. Never divides by zero.
    public static float Progress(float time, float duration)
    {
        return Mathf.Clamp01(time / Mathf.Max(0.01f, duration));
    }

    // True if a looping 0-1 phase passed 'mark' while moving from 'previous' to 'current' (wrap included).
    // A mark of 0 means "the phase just wrapped".
    public static bool CrossedPhase(float previous, float current, float mark)
    {
        if (current >= previous)
            return previous < mark && mark <= current;

        return mark > previous || mark <= current;
    }
    #endregion

    #region Planar
    // The vector with its vertical part removed.
    public static Vector3 Flatten(Vector3 vector)
    {
        return new Vector3(vector.x, 0f, vector.z);
    }

    // A flat, normalised direction, or the fallback if the vector points straight up or down.
    public static Vector3 FlatDirection(Vector3 vector, Vector3 fallback)
    {
        Vector3 flat = Flatten(vector);
        return flat.sqrMagnitude > Epsilon ? flat.normalized : fallback;
    }

    // An upright rotation facing the given direction: the frame the head and body levels are measured in.
    public static Quaternion LevelRotation(Vector3 forward)
    {
        Vector3 flat = Flatten(forward);
        return flat.sqrMagnitude > Epsilon ? Quaternion.LookRotation(flat, Vector3.up) : Quaternion.identity;
    }

    // A world vector expressed in a facing's frame, as (sideways, forward). Used for velocity and acceleration.
    public static Vector2 ToLocalPlanar(Vector3 world, Quaternion facing)
    {
        Vector3 local = Quaternion.Inverse(facing) * world;
        return new Vector2(local.x, local.z);
    }

    // Turn speed in degrees per second between two yaw readings.
    public static float YawRate(float previousYaw, float yaw, float deltaTime)
    {
        return deltaTime > 0f ? Mathf.DeltaAngle(previousYaw, yaw) / deltaTime : 0f;
    }
    #endregion

    #region Vectors And Rotations
    public static Vector3 SafeNormalize(Vector3 vector, Vector3 fallback)
    {
        return vector.sqrMagnitude > Epsilon ? vector.normalized : fallback;
    }

    // Normalised, with an empty or broken quaternion (e.g. a struct that was never set) turned into identity.
    public static Quaternion SafeRotation(Quaternion rotation)
    {
        float lengthSqr = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;

        if (lengthSqr < 0.0001f || float.IsNaN(lengthSqr) || float.IsInfinity(lengthSqr))
            return Quaternion.identity;

        return Quaternion.Normalize(rotation);
    }

    // Quaternion.LookRotation in plain maths, so it is Burst-safe (the engine version is main-thread only).
    // Falls back to looking along 'forward' with world forward as up when forward is parallel to up.
    public static Quaternion LookRotation(Vector3 forward, Vector3 up)
    {
        Vector3 f = SafeNormalize(forward, Vector3.forward);
        Vector3 r = Vector3.Cross(up, f);

        if (r.sqrMagnitude < Epsilon)
            r = Vector3.Cross(Vector3.forward, f);

        r = SafeNormalize(r, Vector3.right);
        Vector3 u = Vector3.Cross(f, r);

        // Columns are right, up, forward
        float trace = r.x + u.y + f.z;

        if (trace > 0f)
        {
            float s = Mathf.Sqrt(trace + 1f) * 2f;
            return new Quaternion((u.z - f.y) / s, (f.x - r.z) / s, (r.y - u.x) / s, 0.25f * s);
        }

        if (r.x > u.y && r.x > f.z)
        {
            float s = Mathf.Sqrt(1f + r.x - u.y - f.z) * 2f;
            return new Quaternion(0.25f * s, (u.x + r.y) / s, (f.x + r.z) / s, (u.z - f.y) / s);
        }

        if (u.y > f.z)
        {
            float s = Mathf.Sqrt(1f + u.y - r.x - f.z) * 2f;
            return new Quaternion((u.x + r.y) / s, 0.25f * s, (f.y + u.z) / s, (f.x - r.z) / s);
        }

        float t = Mathf.Sqrt(1f + f.z - r.x - u.y) * 2f;
        return new Quaternion((f.x + r.z) / t, (f.y + u.z) / t, 0.25f * t, (r.y - u.x) / t);
    }

    // A rotation scaled by a 0-1 weight: 0 = no rotation, 1 = the full rotation.
    public static Quaternion Scale(Quaternion rotation, float weight)
    {
        return Quaternion.Slerp(Quaternion.identity, rotation, weight);
    }

    // The rotation that takes 'from' to 'to', the short way round.
    public static Quaternion ShortestDelta(Quaternion from, Quaternion to)
    {
        Quaternion delta = to * Quaternion.Inverse(from);

        if (delta.w < 0f)
            delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);

        return delta;
    }

    // A rotation as one vector: axis * angle in radians, the short way round.
    public static Vector3 ToRotationVector(Quaternion rotation)
    {
        if (rotation.w < 0f)
            rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);

        rotation.ToAngleAxis(out float degrees, out Vector3 axis);

        if (degrees < 0.0001f || float.IsNaN(axis.x))
            return Vector3.zero;

        return axis.normalized * (degrees * Mathf.Deg2Rad);
    }

    // The reverse of ToRotationVector.
    public static Quaternion FromRotationVector(Vector3 radians)
    {
        float angle = radians.magnitude;
        return angle > Epsilon ? Quaternion.AngleAxis(angle * Mathf.Rad2Deg, radians / angle) : Quaternion.identity;
    }

    // A rotation stored as axis * degrees (handy for springing a twist), back to a quaternion.
    public static Quaternion FromAxisDegrees(Vector3 axisTimesDegrees)
    {
        float angle = axisTimesDegrees.magnitude;
        return angle > 0.001f ? Quaternion.AngleAxis(angle, axisTimesDegrees / angle) : Quaternion.identity;
    }

    // Tips a part over toward a direction given as (sideways, 0, forward) in degrees, + = right and forward. The
    // vector's length is the angle. Used for leans that are sprung as one vector.
    public static Quaternion Tip(Vector3 angles)
    {
        float angle = angles.magnitude;

        if (angle < 0.001f)
            return Quaternion.identity;

        return Quaternion.AngleAxis(angle, Vector3.Cross(Vector3.up, angles / angle));
    }

    // Splits a rotation into the part that turns around 'axis' (twist) and everything else (swing),
    // so that rotation = swing * twist.
    public static void SplitTwist(Quaternion rotation, Vector3 axis, out Quaternion swing, out Quaternion twist)
    {
        axis.Normalize();

        Vector3 along = Vector3.Dot(new Vector3(rotation.x, rotation.y, rotation.z), axis) * axis;
        twist = new Quaternion(along.x, along.y, along.z, rotation.w);

        float length = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
        twist = length > 0.0001f
            ? new Quaternion(twist.x / length, twist.y / length, twist.z / length, twist.w / length)
            : Quaternion.identity;

        swing = rotation * Quaternion.Inverse(twist);
    }

    // Splits a rotation into a turn around vertical (yaw) and the lean / roll that is left (tilt).
    public static void SplitYaw(Quaternion rotation, out Quaternion yaw, out Quaternion tilt)
    {
        SplitTwist(rotation, Vector3.up, out tilt, out yaw);
    }

    // Signed angle in degrees from one direction to another, measured around an axis (both are flattened onto the
    // plane of the axis first). 0 if either lies along the axis.
    public static float SignedAngleAround(Vector3 from, Vector3 to, Vector3 axis)
    {
        from = Vector3.ProjectOnPlane(from, axis);
        to = Vector3.ProjectOnPlane(to, axis);

        if (from.sqrMagnitude < Epsilon || to.sqrMagnitude < Epsilon)
            return 0f;

        return Vector3.SignedAngle(from, to, axis);
    }

    // 'reference' made perpendicular to 'axis' and normalised. If it lies along the axis, a sensible stand-in is used
    // (down, or right if the axis itself is near vertical). The zero reference for elbow swivels.
    public static Vector3 PlaneReference(Vector3 reference, Vector3 axis)
    {
        Vector3 flat = Vector3.ProjectOnPlane(reference, axis);

        if (flat.sqrMagnitude < Epsilon)
            flat = Vector3.ProjectOnPlane(Mathf.Abs(axis.normalized.y) < 0.9f ? Vector3.down : Vector3.right, axis);

        return flat.normalized;
    }
    #endregion

    #region Springs
    // Strength / damper spring (strength pulls toward the target, damper resists velocity).
    // A frame longer than 1/30 s is split into equal steps so the spring stays stable through a hitch.
    public static float Spring(ref Core_SpringFloatState state, float target, Core_SpringSettings settings, float deltaTime)
    {
        int steps = SpringSteps(deltaTime);
        float h = deltaTime / steps;

        for (int i = 0; i < steps; i++)
        {
            float force = (target - state.value) * settings.strength - state.velocity * settings.damper;

            state.velocity += force * h;
            state.value += state.velocity * h;
        }

        return state.value;
    }

    public static Vector2 Spring(ref Core_SpringVector2State state, Vector2 target, Core_SpringSettings settings, float deltaTime)
    {
        int steps = SpringSteps(deltaTime);
        float h = deltaTime / steps;

        for (int i = 0; i < steps; i++)
        {
            Vector2 force = (target - state.value) * settings.strength - state.velocity * settings.damper;

            state.velocity += force * h;
            state.value += state.velocity * h;
        }

        return state.value;
    }

    public static Vector3 Spring(ref Core_SpringVector3State state, Vector3 target, Core_SpringSettings settings, float deltaTime)
    {
        int steps = SpringSteps(deltaTime);
        float h = deltaTime / steps;

        for (int i = 0; i < steps; i++)
        {
            Vector3 force = (target - state.value) * settings.strength - state.velocity * settings.damper;

            state.velocity += force * h;
            state.value += state.velocity * h;
        }

        return state.value;
    }

    private static int SpringSteps(float deltaTime)
    {
        return Mathf.Max(1, Mathf.CeilToInt(deltaTime / SpringMaxStep));
    }

    // Stiffness / damping-ratio spring, substepped so stiff values stay stable. Damping ratio 1 = no overshoot,
    // lower = wobbles before settling. Easier to tune than strength / damper: stiffness is speed, ratio is bounce.
    public static float SpringRatio(ref Core_SpringFloatState state, float target, float stiffness, float dampingRatio, float deltaTime)
    {
        if (deltaTime <= 0f)
            return state.value;

        float damping = Damping(stiffness, dampingRatio);
        int steps = Substeps(deltaTime);
        float h = deltaTime / steps;

        for (int i = 0; i < steps; i++)
        {
            float force = (target - state.value) * stiffness - state.velocity * damping;
            state.velocity += force * h;
            state.value += state.velocity * h;
        }

        return state.value;
    }

    public static Vector3 SpringRatio(ref Core_SpringVector3State state, Vector3 target, float stiffness, float dampingRatio, float deltaTime)
    {
        if (deltaTime <= 0f)
            return state.value;

        float damping = Damping(stiffness, dampingRatio);
        int steps = Substeps(deltaTime);
        float h = deltaTime / steps;

        for (int i = 0; i < steps; i++)
        {
            Vector3 force = (target - state.value) * stiffness - state.velocity * damping;
            state.velocity += force * h;
            state.value += state.velocity * h;
        }

        return state.value;
    }

    // Springs a rotation toward a target, the short way round.
    public static Quaternion SpringRatio(ref Core_SpringRotationState state, Quaternion target, float stiffness, float dampingRatio, float deltaTime)
    {
        state.value = SafeRotation(state.value);

        if (deltaTime <= 0f)
            return state.value;

        float damping = Damping(stiffness, dampingRatio);
        int steps = Substeps(deltaTime);
        float h = deltaTime / steps;

        for (int i = 0; i < steps; i++)
        {
            Vector3 error = ToRotationVector(ShortestDelta(state.value, target));

            state.angularVelocity += (error * stiffness - state.angularVelocity * damping) * h;

            float speed = state.angularVelocity.magnitude;

            if (speed > 0.00001f)
            {
                Quaternion step = Quaternion.AngleAxis(speed * h * Mathf.Rad2Deg, state.angularVelocity / speed);
                state.value = Quaternion.Normalize(step * state.value);
            }
        }

        return state.value;
    }

    // The same stiffness / damping-ratio spring for soft secondary motion (crowds, wobbles), stepped at most every
    // 1/30 s instead of every 5 ms: about 4x cheaper at 60 fps. Stable while stiffness * step² stays under ~4, so up
    // to stiffness ~1000 at 30 fps; use SpringRatio for anything stiffer. Burst-safe.
    public static float SpringRatioLight(ref Core_SpringFloatState state, float target, float stiffness, float dampingRatio, float deltaTime)
    {
        if (deltaTime <= 0f)
            return state.value;

        float damping = Damping(stiffness, dampingRatio);
        int steps = SpringSteps(deltaTime);
        float h = deltaTime / steps;

        for (int i = 0; i < steps; i++)
        {
            float force = (target - state.value) * stiffness - state.velocity * damping;
            state.velocity += force * h;
            state.value += state.velocity * h;
        }

        return state.value;
    }

    public static Vector3 SpringRatioLight(ref Core_SpringVector3State state, Vector3 target, float stiffness, float dampingRatio, float deltaTime)
    {
        if (deltaTime <= 0f)
            return state.value;

        float damping = Damping(stiffness, dampingRatio);
        int steps = SpringSteps(deltaTime);
        float h = deltaTime / steps;

        for (int i = 0; i < steps; i++)
        {
            Vector3 force = (target - state.value) * stiffness - state.velocity * damping;
            state.velocity += force * h;
            state.value += state.velocity * h;
        }

        return state.value;
    }

    private static float Damping(float stiffness, float dampingRatio)
    {
        return 2f * Mathf.Sqrt(Mathf.Max(0f, stiffness)) * dampingRatio;
    }

    private static int Substeps(float deltaTime)
    {
        return Mathf.Max(1, Mathf.CeilToInt(deltaTime / SpringSubstep));
    }
    #endregion

    #region Limbs
    // The direction a two-bone limb's middle joint should bend toward: the pole made perpendicular to the
    // root-to-target line. Falls back to the current bend, then to any perpendicular, if the pole is in line.
    public static Vector3 BendDirection(Vector3 targetDirection, Vector3 toPole, Vector3 toCurrentMiddle)
    {
        Vector3 bend = Vector3.ProjectOnPlane(toPole, targetDirection);

        if (bend.sqrMagnitude < Epsilon)
            bend = Vector3.ProjectOnPlane(toCurrentMiddle, targetDirection);

        if (bend.sqrMagnitude < Epsilon)
            bend = Vector3.ProjectOnPlane(Mathf.Abs(targetDirection.y) < 0.9f ? Vector3.up : Vector3.right, targetDirection);

        return bend.normalized;
    }

    // Solves a two-bone limb (shoulder > elbow > hand) in positions. The reach is clamped so the limb never fully
    // straightens or folds flat. Returns where the middle and end joints go; turning that into bone rotations is
    // the solver's job.
    public static void SolveTwoBone(Vector3 root, Vector3 target, float upperLength, float lowerLength,
        Vector3 bendDirection, out Vector3 middle, out Vector3 end)
    {
        Vector3 toTarget = target - root;
        float distance = toTarget.magnitude;
        Vector3 direction = distance > Epsilon ? toTarget / distance : SafeNormalize(-bendDirection, Vector3.down);

        float margin = Mathf.Min(upperLength, lowerLength) * 0.001f;
        float minReach = Mathf.Abs(upperLength - lowerLength) + margin;
        float maxReach = upperLength + lowerLength - margin;
        float reach = Mathf.Clamp(distance, minReach, maxReach);

        // Law of cosines: how far along the line the elbow sits, and how far out to the side
        float along = (upperLength * upperLength - lowerLength * lowerLength + reach * reach) / (2f * reach);
        float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));

        Vector3 bend = PlaneReference(bendDirection, direction);

        middle = root + direction * along + bend * height;
        end = root + direction * reach;
    }

    // The direction the elbow points for a given swivel: the reference made perpendicular to the limb line, then
    // turned around the line by the swivel angle in degrees.
    public static Vector3 SwivelDirection(Vector3 limbLine, Vector3 reference, float swivelDegrees)
    {
        return Quaternion.AngleAxis(swivelDegrees, limbLine) * PlaneReference(reference, limbLine);
    }

    // The reverse of SwivelDirection: how many degrees the elbow is swivelled away from the reference.
    public static float MeasureSwivel(Vector3 root, Vector3 middle, Vector3 end, Vector3 reference)
    {
        Vector3 line = SafeNormalize(end - root, Vector3.down);
        Vector3 bend = Vector3.ProjectOnPlane(middle - root, line);

        if (bend.sqrMagnitude < Epsilon)
            return 0f;

        return Vector3.SignedAngle(PlaneReference(reference, line), bend, line);
    }
    #endregion

    #region Squash
    // A base scale squashed and stretched: positive squash = taller and thinner. Width is how much thinner per unit.
    public static Vector3 SquashScale(Vector3 baseScale, float squash, float width)
    {
        return new Vector3(
            baseScale.x * (1f - squash * width),
            baseScale.y * (1f + squash),
            baseScale.z * (1f - squash * width));
    }
    #endregion
}
