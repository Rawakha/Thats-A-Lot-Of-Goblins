using System;
using UnityEngine;

// Arcade flight: slows into turns and accelerates on straights, steering toward Plane_Input's direction.
// Holding descend (CTRL / left trigger) brings the plane down to the low-flight height and holds it there, trading
// height for speed on the way down. Releasing descend climbs back to cruise height. The plane never lands.
// Moves the transform directly, no physics.
// Visuals, camera and weapons read the public state; nothing else should move this transform.
//
// GC: none per frame. One UpdateManager subscription (OnUpdate), added on enable. Ticked fires every tick after the
// plane has moved (Equip_Loadout aims and fires from it).
[RequireComponent(typeof(Plane_Input))]
public class Plane_Controller : MonoBehaviour
{
    [SerializeField] private Plane_Settings settings;

    private Plane_Input input;
    private Vector3 position;
    private float travelHeading;

    public event Action Ticked;

    public Plane_Settings Settings => settings;

    // Degrees, where the nose points
    public float Heading { get; private set; }

    // Degrees per second, + is turning right
    public float TurnRate { get; private set; }

    // Units per second along the ground
    public float Speed { get; private set; }

    public float Altitude { get; private set; }

    // Units per second, + is climbing
    public float VerticalSpeed { get; private set; }

    public float Dive => input.Dive;
    public Vector3 Velocity { get; private set; }

    private void Awake()
    {
        input = GetComponent<Plane_Input>();

        if (settings == null)
        {
            Debug.LogError($"{name}: Plane_Controller has no Plane_Settings assigned.", this);
            enabled = false;
            return;
        }

        position = transform.position;
        Heading = travelHeading = transform.eulerAngles.y;
        Speed = settings.straightSpeed;
        Altitude = settings.cruiseHeight;
    }

    private void OnEnable()
    {
        if (UpdateManager.Instance == null)
        {
            Debug.LogError($"{name}: No UpdateManager in the scene, the plane won't move.", this);
            return;
        }

        UpdateManager.Instance.OnUpdate += Tick;
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnUpdate -= Tick;
    }

    private void Tick()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        input.Read(position, transform.forward);

        UpdateHeading(deltaTime);
        UpdateAltitude(deltaTime);
        UpdateSpeed(deltaTime);

        // The direction of travel lags the nose, so the plane drifts through turns
        travelHeading = Core_Math.DampAngle(travelHeading, Heading, settings.driftRate, deltaTime);

        Vector3 travel = Quaternion.Euler(0f, travelHeading, 0f) * Vector3.forward * Speed;
        position += travel * deltaTime;
        position.y = Altitude;

        Velocity = new Vector3(travel.x, VerticalSpeed, travel.z);
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Heading, 0f));

        Ticked?.Invoke();
    }

    // Turn toward the wanted direction: the bigger the error the harder the turn, capped, and eased in
    private void UpdateHeading(float deltaTime)
    {
        float maxTurnRate = settings.maxTurnRate;
        float targetHeading = Mathf.Atan2(input.Direction.x, input.Direction.z) * Mathf.Rad2Deg;
        float wantedTurn = Mathf.Clamp(Mathf.DeltaAngle(Heading, targetHeading) * settings.turnGain, -maxTurnRate, maxTurnRate);

        TurnRate = Core_Math.Damp(TurnRate, wantedTurn, settings.turnResponse, deltaTime);
        Heading = Mathf.Repeat(Heading + TurnRate * deltaTime, 360f);
    }

    private void UpdateAltitude(float deltaTime)
    {
        // Released, the plane climbs, easing in near cruise. Held, it sinks, easing in near the low-flight height and
        // holding there. A half-pressed trigger settles in between.
        float climb = Mathf.Min((settings.cruiseHeight - Altitude) * settings.climbGain, settings.climbRate);
        float descend = Mathf.Clamp((settings.lowFlightHeight - Altitude) * settings.climbGain, -settings.descentRate, settings.climbRate);
        float targetVerticalSpeed = Mathf.Lerp(climb, descend, input.Dive);

        VerticalSpeed = Core_Math.Damp(VerticalSpeed, targetVerticalSpeed, settings.verticalResponse, deltaTime);
        Altitude += VerticalSpeed * deltaTime;
    }

    private void UpdateSpeed(float deltaTime)
    {
        // Bleed speed into turns, build it back on straights
        float turnAmount = settings.maxTurnRate > 0f ? Mathf.Clamp01(Mathf.Abs(TurnRate) / settings.maxTurnRate) : 0f;
        float targetSpeed = Mathf.Lerp(settings.straightSpeed, settings.turningSpeed, Mathf.Pow(turnAmount, settings.turnSlowdownCurve));

        // Descending trades height for speed
        float descent = settings.descentRate > 0f ? Mathf.Clamp01(-VerticalSpeed / settings.descentRate) : 0f;
        targetSpeed += descent * settings.descentSpeedBonus;

        // Speed above top speed bleeds off slowly, so a descent's speed carries into what comes next
        float fallRate = Speed > settings.straightSpeed ? settings.overspeedBleed : settings.deceleration;

        Speed = Core_Math.DampAsymmetric(Speed, targetSpeed, settings.acceleration, fallRate, deltaTime);
    }
}
