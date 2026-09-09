using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float normalSpeed = 5f;
    [SerializeField] private float fastSpeed = 10f;
    [SerializeField] private float movementTime = 1f;

    [Header("Zoom")]
    [SerializeField] private Transform zoomTransform;
    [SerializeField] private float zoomAmount = 60f;
    [SerializeField] private float scrollNotch = 120f;
    [SerializeField, Range(0.1f, 1f)] private float zoomFalloff = 0.5f;
    [SerializeField] private float zoomTime = 4f;
    [SerializeField] private float minZoom = -15f;
    [SerializeField] private float maxZoom = 0f;
    [SerializeField] private Vector3 zoomAxis = Vector3.forward;

    [Header("Zoom Rotation")]
    [SerializeField] private float minZoomRotation = 35f;
    [SerializeField] private float maxZoomRotation = 65f;

    [Header("Pan")]
    [SerializeField] private float panDamping = 10f;
    [SerializeField] private bool invertPan = false;

    private Vector3 targetPos;
    private Vector3 baseLocalPos;
    private Vector3 panOrigin;
    private Vector2 panVelocity;

    private float targetZoom;
    private bool isPanning;

    private InputAction moveAction;
    private InputAction sprintAction;


    private void Start()
    {
        moveAction = PlayerInput.Instance.InputActions.Player.Move;
        sprintAction = PlayerInput.Instance.InputActions.Player.Sprint;

        targetPos = transform.position;
        baseLocalPos = zoomTransform.localPosition;

        targetZoom = 0f;
    }

    private void Update()
    {
        HandleMovement();
        HandlePan();
        HandleZoom();
    }

    private void HandleMovement()
    {
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        float sprintInput = sprintAction.ReadValue<float>();
        float speed = (sprintInput > 0.2f) ? fastSpeed : normalSpeed;

        Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        targetPos += moveDirection * speed * Time.deltaTime;

        transform.position = Vector3.Lerp(transform.position, targetPos, movementTime * Time.deltaTime);
    }

    private void HandlePan()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            isPanning = PlayerInput.Instance.TryGetGroundPoint(out panOrigin);
            panVelocity = Vector3.zero;
        }
        else if (Mouse.current.middleButton.wasReleasedThisFrame)
        {
            isPanning = false;
        }

        if (!isPanning)
        {
            Vector3 glide = panVelocity * Time.deltaTime;
            targetPos += glide;
            transform.position += glide;
            panVelocity = Vector3.Lerp(panVelocity, Vector3.zero, panDamping * Time.deltaTime);
            return;
        }

        if (PlayerInput.Instance.TryGetGroundPoint(out Vector3 current))
        {
            Vector3 offset = panOrigin - current;
            offset.y = 0f;

            if (invertPan)
                offset = -offset;

            targetPos += offset;
            transform.position += offset;

            if (Time.deltaTime > 0f)
            {
                panVelocity = offset / Time.deltaTime;
            }
        }
    }

    private void HandleZoom()
    {
        if (zoomTransform == null || Mouse.current == null) 
            return;

        bool hasBefore = PlayerInput.Instance.TryGetGroundPoint(out Vector3 before);
        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            float steps = -scroll / scrollNotch;
            float falloff = Mathf.Lerp(1f, zoomFalloff, Mathf.InverseLerp(minZoom, maxZoom, targetZoom));
            targetZoom = Mathf.Clamp(targetZoom + steps * zoomAmount * falloff, minZoom, maxZoom);
        }

        Vector3 targetLocalPos = baseLocalPos + zoomAxis.normalized * targetZoom;
        zoomTransform.localPosition = Vector3.Lerp(zoomTransform.localPosition, targetLocalPos, zoomTime * Time.deltaTime);

        // Allow Zooming out to not be clamped by Mouse Pos:
        // float currentZoom = Vector3.Dot(zoomTransform.localPosition - baseLocalPos, zoomAxis.normalized);
        // bool zoomingIn = targetZoom < currentZoom;

        if (/*zoomingIn &&*/ hasBefore && PlayerInput.Instance.TryGetGroundPoint(out Vector3 after))
        {
            Vector3 offset = before - after;
            offset.y = 0f;
            targetPos += offset;
            transform.position += offset;
        }

        // Zoom Rotation
        float currentZoomAmount = Mathf.InverseLerp(minZoom, maxZoom, targetZoom);
        float targetAngle = Mathf.Lerp(minZoomRotation, maxZoomRotation, currentZoomAmount);

        Quaternion targetRotation = Quaternion.Euler(targetAngle, 0f, 0f);
        zoomTransform.localRotation = targetRotation;
    }
}