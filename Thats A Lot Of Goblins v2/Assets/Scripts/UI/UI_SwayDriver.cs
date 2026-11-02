using UnityEngine;

// Sways the HUD with the plane's flight: reads the plane once a frame and moves every UI_Sway element under this
// canvas. Three channels, tuned in UI_SwaySettings:
//   Turn: elements slide against the turn and tilt into it, as if they have weight.
//   Speed change: speeding up presses them down, slowing down lets them float up; a steady speed doesn't move them.
//   Dive: while descending they spread outward from the screen centre, then spring back in when the plane levels.
// Each element springs toward its own target, so they overshoot and settle back instead of snapping.
//
// Setup: put this on the HUD canvas (the root RectTransform: the dive spreads from its centre), assign the plane and a
// UI_SwaySettings asset, then add UI_Sway to each panel that should move.
//
// GC: none per frame. One UpdateManager subscription (OnLateUpdate, after the plane has moved). Elements register
// themselves on enable (swap-back registry, pre-sized).
public class UI_SwayDriver : Core_RegistryManager<UI_Sway>
{
    [SerializeField] private Plane_Controller plane;
    [SerializeField] private UI_SwaySettings settings;

    private RectTransform canvasRect;
    private float lastSpeed;
    private float acceleration;

    public UI_SwaySettings Settings => settings;
    public RectTransform CanvasRect => canvasRect;

    // This frame's flight, each roughly -1 to 1: turn (+ right), acceleration (+ speeding up), dive (0-1, descending)
    public float Turn { get; private set; }
    public float Acceleration { get; private set; }
    public float Dive { get; private set; }

    private void Awake()
    {
        canvasRect = transform as RectTransform;

        if (plane == null || settings == null)
            Debug.LogWarning($"{name}: UI_SwayDriver needs a Plane_Controller and a UI_SwaySettings; the HUD won't sway.", this);
    }

    private void OnEnable()
    {
        if (plane != null)
            lastSpeed = plane.Speed;

        acceleration = 0f;

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate += Tick;
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= Tick;
    }

    private void Tick()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f || plane == null || settings == null)
            return;

        // Turn: signed, 1 at the max turn rate
        float maxTurn = plane.Settings != null ? plane.Settings.maxTurnRate : 0f;
        Turn = maxTurn > 0f ? Mathf.Clamp(plane.TurnRate / maxTurn, -1f, 1f) : 0f;

        // Speed change: smoothed so small wobbles in speed don't jitter the HUD
        float rawAcceleration = (plane.Speed - lastSpeed) / deltaTime;
        lastSpeed = plane.Speed;
        acceleration = Core_Math.Damp(acceleration, rawAcceleration, settings.accelerationSmoothing, deltaTime);
        Acceleration = Mathf.Clamp(acceleration / settings.fullAcceleration, -1f, 1f);

        // Dive: how fast the plane is coming down
        Dive = Mathf.Clamp01(-plane.VerticalSpeed / settings.fullDiveSpeed);

        BeginIteration();

        try
        {
            for (int i = 0; i < Count; i++)
                items[i].Sway(this, deltaTime);
        }
        finally
        {
            EndIteration();
        }
    }
}
