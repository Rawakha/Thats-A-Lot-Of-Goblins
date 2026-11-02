using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// Turns raw input into what the plane wants: a flat world direction to fly toward, how hard to dive (0-1), the
// fire / ability / cycle ability buttons for Equip_Loadout, and the target mode toggle (middle click, gamepad Y) for Weapon_Aim.
// Mouse steers toward the cursor; stick / WASD give a direction relative to the fixed camera. Whichever was used
// last wins. Plane_Controller calls Read each tick, so this has no update subscription of its own.
public class Plane_Input : MonoBehaviour
{
    private enum Device
    {
        Pointer,
        Stick
    }

    [SerializeField] private InputActionReference steerAction;
    [SerializeField] private InputActionReference aimAction;
    [SerializeField] private InputActionReference diveAction;
    [SerializeField] private InputActionReference fireAction;
    [SerializeField] private InputActionReference abilityAction;
    [Tooltip("Plane / Target Mode: switches the guns between ground and air targets (middle click, gamepad Y).")]
    [SerializeField, FormerlySerializedAs("gunAngleAction")] private InputActionReference targetModeAction;
    [Tooltip("Plane / Cycle Ability: selects the next ability on the belt (E, gamepad left shoulder).")]
    [SerializeField] private InputActionReference cycleAbilityAction;
    [SerializeField] private Camera viewCamera;

    [Header("Tuning")]
    [SerializeField, Range(0f, 1f)] private float stickDeadzone = 0.2f;

    [Tooltip("Pixels the cursor has to move in one frame to take steering back from the stick.")]
    [SerializeField, Min(0f)] private float pointerMoveThreshold = 2f;

    [Tooltip("World units around the plane where the cursor is ignored, so it doesn't spin when the cursor is on it.")]
    [SerializeField, Min(0f)] private float pointerDeadzone = 1.5f;

    private Device activeDevice = Device.Pointer;
    private Vector2 lastPointer;

    public Vector3 Direction { get; private set; }

    // The cursor in screen pixels, and whether the mouse (not the stick) is steering right now
    public Vector2 PointerPosition { get; private set; }
    public bool UsingPointer => activeDevice == Device.Pointer;
    public float Dive { get; private set; }
    public bool FireHeld { get; private set; }
    public bool FirePressed { get; private set; }
    public bool AbilityHeld { get; private set; }
    public bool AbilityPressed { get; private set; }

    // Target Mode was pressed this tick
    public bool TargetModePressed { get; private set; }

    // Cycle Ability was pressed this tick
    public bool CycleAbilityPressed { get; private set; }

    private void Awake()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;
    }

    private void OnEnable()
    {
        SetEnabled(steerAction, true);
        SetEnabled(aimAction, true);
        SetEnabled(diveAction, true);
        SetEnabled(fireAction, true);
        SetEnabled(abilityAction, true);
        SetEnabled(targetModeAction, true);
        SetEnabled(cycleAbilityAction, true);
    }

    private void OnDisable()
    {
        SetEnabled(steerAction, false);
        SetEnabled(aimAction, false);
        SetEnabled(diveAction, false);
        SetEnabled(fireAction, false);
        SetEnabled(abilityAction, false);
        SetEnabled(targetModeAction, false);
        SetEnabled(cycleAbilityAction, false);
    }

    // Call once per tick before reading Direction / Dive / buttons. With no steering input the plane keeps currentForward.
    public void Read(Vector3 planePosition, Vector3 currentForward)
    {
        Vector2 stick = ReadValue<Vector2>(steerAction);
        Vector2 pointer = ReadValue<Vector2>(aimAction);
        bool stickActive = stick.sqrMagnitude > stickDeadzone * stickDeadzone;

        if (stickActive)
            activeDevice = Device.Stick;
        else if ((pointer - lastPointer).sqrMagnitude > pointerMoveThreshold * pointerMoveThreshold)
            activeDevice = Device.Pointer;

        lastPointer = pointer;
        PointerPosition = pointer;

        Vector3 direction = currentForward;

        if (activeDevice == Device.Stick)
        {
            if (stickActive)
                direction = StickToWorld(stick);
        }
        else if (TryPointerToWorld(pointer, planePosition.y, out Vector3 point))
        {
            Vector3 toPoint = Core_Math.Flatten(point - planePosition);

            if (toPoint.sqrMagnitude > pointerDeadzone * pointerDeadzone)
                direction = toPoint;
        }

        Direction = Core_Math.FlatDirection(direction, currentForward);
        Dive = Mathf.Clamp01(ReadValue<float>(diveAction));

        FireHeld = IsHeld(fireAction);
        FirePressed = WasPressed(fireAction);
        AbilityHeld = IsHeld(abilityAction);
        AbilityPressed = WasPressed(abilityAction);
        TargetModePressed = WasPressed(targetModeAction);
        CycleAbilityPressed = WasPressed(cycleAbilityAction);
    }

    // The camera never rotates, so stick up is always up the screen
    private Vector3 StickToWorld(Vector2 stick)
    {
        if (viewCamera == null)
            return new Vector3(stick.x, 0f, stick.y);

        Transform view = viewCamera.transform;
        Vector3 up = Core_Math.FlatDirection(view.forward, Core_Math.FlatDirection(view.up, Vector3.forward));
        Vector3 right = Core_Math.FlatDirection(view.right, Vector3.right);

        return right * stick.x + up * stick.y;
    }

    // Where the cursor points on a flat plane at the given height
    private bool TryPointerToWorld(Vector2 pointer, float height, out Vector3 point)
    {
        point = default;

        if (viewCamera == null)
            return false;

        Ray ray = viewCamera.ScreenPointToRay(pointer);
        Plane ground = new Plane(Vector3.up, new Vector3(0f, height, 0f));

        if (!ground.Raycast(ray, out float distance))
            return false;

        point = ray.GetPoint(distance);
        return true;
    }

    private static T ReadValue<T>(InputActionReference reference) where T : struct
    {
        return reference != null && reference.action != null ? reference.action.ReadValue<T>() : default;
    }

    private static bool IsHeld(InputActionReference reference)
    {
        return reference != null && reference.action != null && reference.action.IsPressed();
    }

    private static bool WasPressed(InputActionReference reference)
    {
        return reference != null && reference.action != null && reference.action.WasPressedThisFrame();
    }

    private static void SetEnabled(InputActionReference reference, bool enabled)
    {
        if (reference == null || reference.action == null)
            return;

        if (enabled)
            reference.action.Enable();
        else
            reference.action.Disable();
    }
}
