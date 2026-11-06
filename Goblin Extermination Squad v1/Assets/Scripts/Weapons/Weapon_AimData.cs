using UnityEngine;

// Optional weapon-specific crosshair prediction, called after shared aim is built. No subscription.
public interface Weapon_IAimOverride
{
    void RefineAim(ref Weapon_AimData aim);
}

// Where the plane's weapons point this frame, built once by Weapon_Aim and shared by every weapon.
// Air weapons fire along Direction (Pitch and AimYaw, assist included). Ground-only weapons (bombs) ignore the pitch
// and use BelowPoint / PlaneVelocity.
public struct Weapon_AimData
{
    public Vector3 PlanePosition;
    public Vector3 PlaneVelocity;

    // Yaw only, for turning muzzle offsets into world space: the plane's own heading
    public Quaternion YawRotation;
    public float Heading;

    // Where the guns point: degrees, + is nose down (0 level, up to the strafe angle with the guns down), and the yaw
    // (the heading, bent toward the assist's target)
    public float Pitch;
    public float AimYaw;
    public Vector3 Direction;

    // What the aim assist is pulling toward, if anything
    public bool HasTarget;
    public Vector3 TargetPoint;

    // Where Direction meets the ground. False when firing level.
    public bool HasGroundPoint;
    public Vector3 GroundPoint;

    // Straight below the plane, on the ground
    public Vector3 BelowPoint;

    // For the crosshair. AimPoint: where you're aiming, the nose and gun angle without the assist. ShotPoint: where
    // the guns actually point (Direction, assist included) meets the ground, or Aim Distance ahead when firing level.
    public Vector3 AimPoint;
    public Vector3 ShotPoint;
}
