using UnityEngine;

// The plane's flight effects, driven by the flight every frame. Purely cosmetic.
//   Speed lines: a particle system of white streaks. Its emission rate scales from nothing at the start speed to its
//     authored rate at top speed, more when flying low or descending.
//   Wing trails: trail renderers on the wing tips. They draw at high speed and in hard turns, the outer wing more
//     than the inner, fading their width and colour with strength and stopping below a minimum.
// Author each effect at full strength (rate, width, colours); this only scales it down.
//
// Speed lines setup: a child of the plane (not the model, so it doesn't bank), Simulation Space World, Start Speed 0,
// Trails module on, Renderer mode None. Streaks are left in the air as the plane flies through them, so they get
// longer the faster it goes.
//
// GC: none per frame. One UpdateManager subscription (OnLateUpdate), added on enable.
[RequireComponent(typeof(Plane_Controller))]
public class Plane_Effects : MonoBehaviour
{
    private struct WingTrail
    {
        public TrailRenderer Trail;
        public float Side;          // -1 left wing, +1 right wing
        public float BaseWidth;
        public Color BaseStart;
        public Color BaseEnd;
        public float Strength;
    }

    [SerializeField] private Plane_EffectsSettings settings;

    [Header("Effects")]
    [Tooltip("White speed streaks. Its authored emission rate is the rate at top speed.")]
    [SerializeField] private ParticleSystem speedLines;

    [Tooltip("Trail renderers on the wing tips. Their authored width and colours are full strength.")]
    [SerializeField] private TrailRenderer[] wingTrails;

    private Plane_Controller plane;
    private WingTrail[] trails;
    private float speedLinesBaseRate;
    private float speedLinesStrength;

    private void Awake()
    {
        plane = GetComponent<Plane_Controller>();

        if (settings == null)
        {
            Debug.LogError($"{name}: Plane_Effects has no Plane_EffectsSettings.", this);
            enabled = false;
            return;
        }

        if (speedLines != null)
            speedLinesBaseRate = speedLines.emission.rateOverTimeMultiplier;

        int count = wingTrails != null ? wingTrails.Length : 0;
        trails = new WingTrail[count];

        for (int i = 0; i < count; i++)
        {
            TrailRenderer trail = wingTrails[i];

            if (trail == null)
                continue;

            float x = transform.InverseTransformPoint(trail.transform.position).x;

            trails[i] = new WingTrail
            {
                Trail = trail,
                Side = Mathf.Abs(x) > 0.01f ? Mathf.Sign(x) : 0f,
                BaseWidth = trail.widthMultiplier,
                BaseStart = trail.startColor,
                BaseEnd = trail.endColor
            };
        }
    }

    private void OnEnable()
    {
        if (UpdateManager.Instance == null)
        {
            Debug.LogError($"{name}: No UpdateManager in the scene, the plane effects won't run.", this);
            return;
        }

        speedLinesStrength = 0f;
        ApplySpeedLines();

        for (int i = 0; i < trails.Length; i++)
        {
            trails[i].Strength = 0f;
            ApplyTrail(ref trails[i]);
        }

        UpdateManager.Instance.OnLateUpdate += LateTick;
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= LateTick;
    }

    private void LateTick()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        Plane_Settings flight = plane.Settings;
        float topSpeed = Mathf.Max(0.01f, flight.straightSpeed + flight.descentSpeedBonus);
        float speed01 = Mathf.Clamp01(plane.Speed / topSpeed);
        float turn = Mathf.Clamp(plane.TurnRate / Mathf.Max(1f, flight.maxTurnRate), -1f, 1f);   // + is turning right
        float low = Mathf.InverseLerp(flight.cruiseHeight, flight.lowFlightHeight, plane.Altitude);

        // Speed lines: fade in above the start speed, boosted low and in a descent
        float lines = Mathf.InverseLerp(settings.speedLinesStart, 1f, speed01)
            * Mathf.Lerp(1f, settings.speedLinesLowBoost, low)
            + plane.Dive * settings.speedLinesDiveBoost * speed01;
        speedLinesStrength = Core_Math.Damp(speedLinesStrength, lines, settings.speedLinesResponse, deltaTime);
        ApplySpeedLines();

        // Wing trails: the stronger of high speed and a hard turn
        float speedPull = Mathf.InverseLerp(settings.trailSpeedStart, 1f, speed01);
        float turnPull = Mathf.InverseLerp(settings.trailTurnStart, 1f, Mathf.Abs(turn));
        float pull = Mathf.Max(speedPull, turnPull);

        for (int i = 0; i < trails.Length; i++)
        {
            ref WingTrail trail = ref trails[i];

            if (trail.Trail == null)
                continue;

            // Turning right, the left wing is on the outside
            float outer = Mathf.Clamp01(-trail.Side * turn);
            float target = Mathf.Clamp01(pull * (1f + outer * settings.trailOuterBoost));

            trail.Strength = Core_Math.Damp(trail.Strength, target, settings.trailResponse, deltaTime);
            ApplyTrail(ref trail);
        }
    }

    private void ApplySpeedLines()
    {
        if (speedLines == null)
            return;

        ParticleSystem.EmissionModule emission = speedLines.emission;
        emission.rateOverTimeMultiplier = speedLinesBaseRate * speedLinesStrength;
    }

    private void ApplyTrail(ref WingTrail trail)
    {
        if (trail.Trail == null)
            return;

        float strength = trail.Strength;

        trail.Trail.emitting = strength > settings.trailMinStrength;
        trail.Trail.widthMultiplier = trail.BaseWidth * strength;
        trail.Trail.startColor = Fade(trail.BaseStart, strength);
        trail.Trail.endColor = Fade(trail.BaseEnd, strength);
    }

    private static Color Fade(Color color, float strength)
    {
        color.a *= strength;
        return color;
    }
}
