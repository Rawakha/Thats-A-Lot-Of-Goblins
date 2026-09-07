using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class PlayerInput : MonoBehaviour
{
    public static PlayerInput Instance;

    [Header("Mouse World Position")]
    [SerializeField] private Camera cam;
    [SerializeField] private float groundHeight = 0f;

    [Header("Gizmos")]
    [SerializeField] private bool drawMousePos = true;

    private InputActions inputActions;

    private Vector2 mouseScreenPos;
    private Vector3 mouseWorldPos;
    private bool hasMouseWorldPos;

    public InputActions InputActions => inputActions;
    public bool HadMouseWorldPos => hasMouseWorldPos;
    public Vector3 MouseWorldPos => mouseWorldPos;
    public Vector2 MouseScreenPos => mouseScreenPos;

    private void OnEnable()
    {
        Utilities.CreateInstance<PlayerInput>(ref Instance, this);
        SetInputActions(true);
    }

    private void OnDisable()
    {
        SetInputActions(false);

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Mouse.current != null)
        {
            mouseScreenPos = Mouse.current.position.ReadValue();
            hasMouseWorldPos = TryGetGroundPoint(mouseScreenPos, out mouseWorldPos);
        }
    }

    public bool TryGetGroundPoint(Vector3 screenPos, out Vector3 worldPoint)
    {
        worldPoint = default;
        if (cam == null || Mouse.current == null)
            return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        Plane ground = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));

        if (ground.Raycast(ray, out float enter))
        {
            worldPoint = ray.GetPoint(enter);
            return true;
        }

        return false;
    }

    public bool TryGetGroundPoint(out Vector3 worldPoint)
    {
        return TryGetGroundPoint(mouseScreenPos, out worldPoint);
    }

    private void SetInputActions(bool enable)
    {
        if (enable)
        {
            if (inputActions == null)
                inputActions = new InputActions();

            inputActions.Enable();
        }
        else
        {
            if (inputActions != null)
                inputActions.Disable();
        }
    }

    private void OnDrawGizmos()
    {
        if (drawMousePos)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(mouseWorldPos, 0.2f);
            Gizmos.DrawWireSphere(mouseWorldPos, 0.21f);
        }
    }
}