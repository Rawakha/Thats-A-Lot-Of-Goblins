using UnityEngine;

// One HUD element that sways with the plane (see UI_SwayDriver). It moves and tilts a child, Content, away from its
// rest position and springs it back, so the panel's own layout and anchors are never touched; later juice (squish,
// pops) can go on Content's children and stack on top.
//
// Setup: put this on a panel under a canvas with a UI_SwayDriver. Make a child RectTransform holding the panel's
// visuals (stretched to fill the panel) and drag it into Content. The weights scale the driver's settings for this
// element: 0 turns a channel off, above 1 sways harder.
//
// Content's rest position is taken on enable, so move it in edit mode, not while playing.
//
// GC: none. No UpdateManager subscription of its own: the driver moves every element in its one LateUpdate.
// Registers with the driver on enable.
public class UI_Sway : Core_RegistrableBehaviour
{
    [Tooltip("The child that moves: hold the panel's visuals here. Empty moves this transform itself, which fights layout groups and anchors.")]
    [SerializeField] private RectTransform content;

    [Header("Weights")]
    [Tooltip("Slide and tilt with turns.")]
    [Min(0f)] [SerializeField] private float turnWeight = 1f;

    [Tooltip("Push down and float up with speed changes.")]
    [Min(0f)] [SerializeField] private float speedWeight = 1f;

    [Tooltip("Spread outward while diving.")]
    [Min(0f)] [SerializeField] private float diveWeight = 1f;

    [Tooltip("How quickly this element follows, as a multiple of the settings' springs. Below 1 lags behind the others, above 1 is snappier. The wobble stays the same shape.")]
    [Range(0.25f, 4f)] [SerializeField] private float response = 1f;

    private UI_SwayDriver driver;
    private Vector2 restPosition;
    private float restTilt;
    private Core_SpringVector2State offset;
    private Core_SpringFloatState tilt;

    private void OnEnable()
    {
        if (content == null)
            content = transform as RectTransform;

        restPosition = content.anchoredPosition;
        restTilt = content.localEulerAngles.z;
        offset = new Core_SpringVector2State(Vector2.zero);
        tilt = new Core_SpringFloatState(0f);

        driver = GetComponentInParent<UI_SwayDriver>();

        if (driver == null)
        {
            Debug.LogWarning($"{name}: UI_Sway has no UI_SwayDriver above it; it won't sway.", this);
            return;
        }

        driver.Register(this);
    }

    private void OnDisable()
    {
        if (driver != null)
            driver.Unregister(this);

        // Back to rest, so a disabled panel isn't left mid-sway
        if (content != null)
        {
            content.anchoredPosition = restPosition;
            content.localRotation = Quaternion.Euler(0f, 0f, restTilt);
        }
    }

    // Called by the driver once a frame
    public void Sway(UI_SwayDriver from, float deltaTime)
    {
        UI_SwaySettings settings = from.Settings;

        // Turn: slide against it, lean into it (turning right is clockwise, negative Z)
        Vector2 target = new Vector2(-from.Turn * settings.turnOffset * turnWeight, 0f);
        float tiltTarget = -from.Turn * settings.turnTilt * turnWeight;

        // Speed change: speeding up presses down
        target.y -= from.Acceleration * settings.speedOffset * speedWeight;

        // Dive: outward from the canvas centre, along the line from the centre to this panel
        if (from.Dive > 0f && from.CanvasRect != null)
        {
            Vector2 fromCentre = from.CanvasRect.InverseTransformPoint(transform.position);
            float distance = fromCentre.magnitude;

            if (distance > 0.001f)
                target += fromCentre / distance * (from.Dive * settings.diveSpread * diveWeight);
        }

        if (settings.maxOffset > 0f)
            target = Vector2.ClampMagnitude(target, settings.maxOffset);

        // Response scales how fast the spring moves but not how much it overshoots
        Vector2 position = Core_Math.Spring(ref offset, target, Scaled(settings.offsetSpring), deltaTime);
        float angle = Core_Math.Spring(ref tilt, tiltTarget, Scaled(settings.tiltSpring), deltaTime);

        content.anchoredPosition = restPosition + position;
        content.localRotation = Quaternion.Euler(0f, 0f, restTilt + angle);
    }

    private Core_SpringSettings Scaled(Core_SpringSettings spring)
    {
        return new Core_SpringSettings(spring.strength * response * response, spring.damper * response);
    }
}
