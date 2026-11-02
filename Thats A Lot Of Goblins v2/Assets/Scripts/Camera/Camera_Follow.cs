using UnityEngine;

// Simple fixed-angle follow camera for the plane. The view never rotates: pitch and yaw are set here, and the camera
// only slides to keep the plane framed, leading it in the direction of travel.
//
// GC: none per frame. One UpdateManager subscription (OnLateUpdate), added on enable.
public class Camera_Follow : MonoBehaviour
{
    [SerializeField] private Plane_Controller target;

    [Header("Angle")]
    [SerializeField, Range(10f, 90f)] private float pitch = 60f;
    [SerializeField] private float yaw = 0f;
    [SerializeField, Min(1f)] private float distance = 40f;

    [Header("Follow")]
    [Tooltip("Seconds of travel to look ahead of the plane.")]
    [SerializeField, Min(0f)] private float lookAhead = 0.6f;

    [Tooltip("How quickly the camera catches up, per second. Lower is lazier.")]
    [SerializeField, Min(0f)] private float followRate = 6f;

    [Tooltip("0 frames the ground under the plane, 1 frames the plane itself and follows its dives.")]
    [SerializeField, Range(0f, 1f)] private float heightFollow = 0.3f;

    private Vector3 focus;

    private void OnEnable()
    {
        if (target == null)
        {
            Debug.LogError($"{name}: Camera_Follow has no target.", this);
            return;
        }

        if (UpdateManager.Instance == null)
        {
            Debug.LogError($"{name}: No UpdateManager in the scene, the camera won't follow.", this);
            return;
        }

        focus = WantedFocus();
        Apply();
        UpdateManager.Instance.OnLateUpdate += LateTick;
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= LateTick;
    }

    private void LateTick()
    {
        focus = Core_Math.Damp(focus, WantedFocus(), followRate, Time.deltaTime);
        Apply();
    }

    private Vector3 WantedFocus()
    {
        Vector3 planePosition = target.transform.position;
        Vector3 ground = new Vector3(planePosition.x, planePosition.y * heightFollow, planePosition.z);

        return ground + Core_Math.Flatten(target.Velocity) * lookAhead;
    }

    private void Apply()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
    }
}
