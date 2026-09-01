using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerHand : MonoBehaviour
{
    [SerializeField] private LayerMask grabbableLayerMask;
    [SerializeField] private Camera cam;

    [Header("Grab Settings")]
    [SerializeField] private float orbitRadius = 1.5f;
    [SerializeField] private float orbitSpinSpeed = 90f;
    [SerializeField] private float holdHeight = 6f;
    [SerializeField] private float dragVelocitySmoothing = 1f;
    [SerializeField] private float throwMultiplier = 1.5f;

    [Header("Flick Settings")]
    [SerializeField] private FlowField flowField;
    [SerializeField] private float flickForce = 50f;
    [SerializeField] private float flickUpwardBias = 0.4f;
    [SerializeField] private float flickRadius = 5f;
    [SerializeField] private ParticleSystem flickParticle;

    private bool isPressing;
    private Vector3 pressGroundPos;
    private Vector3 releasePos;

    private Vector3 lastDragPos;
    private Vector3 dragVelocity;
    private float orbitAngle;
    private readonly List<Grabbable> heldObjects = new(8);
    private List<Enemy> flickQueryResults = new List<Enemy>();

    private void Update()
    {
        if (Mouse.current == null || Keyboard.current == null)
            return;

        bool pressed = Mouse.current.leftButton.wasPressedThisFrame;
        bool released = Mouse.current.leftButton.wasReleasedThisFrame;
        bool down = Mouse.current.leftButton.isPressed;
        bool modifier = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.leftCtrlKey.isPressed;

        if (!PlayerInput.Instance.TryGetGroundPoint(out Vector3 groundPos))
        {
            if (isPressing)
                EndPress();

            return;
        }

        Vector3 holdPos = groundPos + Vector3.up * holdHeight;

        if (pressed)
            BeginPress(groundPos);

        if (down & isPressing)
        {
            UpdateDragVelocity(groundPos);

            if (modifier && heldObjects.Count > 0)
                TryGrab();

            UpdateGrab(holdPos);
        }

        if (released && isPressing)
            EndPress();
    }

    private void BeginPress(Vector3 groundPos)
    {
        isPressing = true;
        pressGroundPos = groundPos;
        lastDragPos = groundPos;
        dragVelocity = Vector3.zero;
        orbitAngle = 0f;

        if (TryGrab())
            return;

        TryFlick(groundPos);
    }

    private void EndPress()
    {
        isPressing = false;
        ReleaseAll();
    }

    private bool TryGrab()
    {
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, grabbableLayerMask))
            return false;

        Rigidbody rb = hit.collider.attachedRigidbody;
        if (rb == null || !rb.TryGetComponent(out Grabbable g))
            return false;

        return TryCapture(g);
    }

    private bool TryCapture(Grabbable g)
    {
        if (g.State != GrabState.Free) return false;

        g.OnGrabbed();
        heldObjects.Add(g);
        return true;
    }

    private void UpdateDragVelocity(Vector3 groundPos)
    {
        Vector3 raw = (groundPos - lastDragPos) / Mathf.Max(Time.deltaTime, 0.0001f);
        dragVelocity = Vector3.Lerp(dragVelocity, raw, dragVelocitySmoothing);
        lastDragPos = groundPos;
    }

    private void UpdateGrab(Vector3 holdPos)
    {
        orbitAngle += orbitSpinSpeed * Time.deltaTime;

        int count = heldObjects.Count;

        // Assign single hold point
        if (count == 1)
        {
            heldObjects[0].OnDragged(holdPos);
            return;
        }

        // Assign the orbit hold points
        float step = 360f / count;
        for (int i = 0; i < count; i++)
        {
            float angle = (orbitAngle + step * i) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * orbitRadius;
            heldObjects[i].OnDragged(holdPos + offset);
        }
    }

    private void ReleaseAll()
    {
        float step = heldObjects.Count > 1 ? 360f / heldObjects.Count : 0f;
        float spinRadians = orbitSpinSpeed * Mathf.Deg2Rad;

        for (int i = 0; i < heldObjects.Count; i++)
        {
            Grabbable g = heldObjects[i];
            if (g == null) continue;

            Vector3 vel = dragVelocity * throwMultiplier;

            if (heldObjects.Count > 1)
            {
                float angle = (orbitAngle + step * i) * Mathf.Deg2Rad;
                Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                vel += tangent * orbitRadius * spinRadians;
            }

            g.OnReleased(vel);
        }

        heldObjects.Clear();
        dragVelocity = Vector3.zero;
    }

    private bool TryFlick(Vector3 flickPos)
    {
        if (EnemyGrid.Instance == null)
        {
            Debug.LogWarning("EnemyGrid instance is null. Cannot perform flick.");
            return false;
        }

        // Find Goblins
        int hits = EnemyGrid.Instance.QueryRadius(flickPos, flickRadius, flickQueryResults);
        // Gizmos
        EnemyGrid.Instance.DebugDrawQuery(flickPos, flickRadius);

        if (hits == 0) 
            return false;

        Vector3 flowfieldDirection = -flowField.Sample(flickPos);
        flowfieldDirection.y = 0f;

        bool hasFlicked = false;

        for (int i = 0; i < hits; i++)
        {
            Enemy e = flickQueryResults[i];
            if (e == null) 
                continue;

            Rigidbody rb = e.body;
            if (rb == null)
                continue;

            Vector3 awayDirection = rb.worldCenterOfMass - flickPos;
            awayDirection.y = 0f;

            Vector3 flickDirection = flowfieldDirection.sqrMagnitude > 0.001f ? flowfieldDirection : awayDirection;
            flickDirection.Normalize();

            Vector3 launchDirection = (flickDirection + Vector3.up * flickUpwardBias).normalized;

            e.Flick(launchDirection, flickForce);
            hasFlicked = true;
        }

        if (!hasFlicked)
            return false;

        if (flickParticle != null)
        {
            flickParticle.transform.position = flickPos + (Vector3.up * 0.15f);
            flickParticle.Play();
        }

        return true;
    }

    private void OnDrawGizmos()
    {
        Color c = Color.cyan;
        c.a = 0.1f;

        Gizmos.color = c;
        Gizmos.DrawSphere(pressGroundPos, flickRadius);
        Gizmos.DrawWireSphere(pressGroundPos, flickRadius);

        if (!Application.isPlaying || !isPressing) return;

        Vector3 holdPos = lastDragPos + Vector3.up * holdHeight;
    }
}