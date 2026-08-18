using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHand : MonoBehaviour
{
    [SerializeField] private LayerMask interactionlayerMask;

    [Header("Flick Settings")]
    [SerializeField] private FlowField flowField;
    [SerializeField] private float flickForce = 50f;
    [SerializeField] private float flickUpwardBias = 0.4f;
    [SerializeField] private float flickRadius = 5f;
    [SerializeField] private ParticleSystem flickParticle;

    private Vector3 pressGroundPos;
    private Vector3 releasePos;
    private float pressTime;
    private bool isPressing;

    private Collider[] flickHits = new Collider[32];

    private void Update()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)                   // Initial press, find ground position
        {
            if (!PlayerInput.Instance.TryGetGroundPoint(out pressGroundPos)) 
                return;

            pressTime = Time.unscaledTime;
            isPressing = true;

        }
        else if (Mouse.current.leftButton.wasReleasedThisFrame)             // Release, find ground position and calculate flick
        {
            if (!PlayerInput.Instance.TryGetGroundPoint(out releasePos))
                return;

            DoFlick(releasePos);

            isPressing = false;
        }
    }

    private void DoFlick(Vector3 flickOrigin)
    {
        // Find Goblins
        int hits = Physics.OverlapSphereNonAlloc(flickOrigin, flickRadius, flickHits, interactionlayerMask, QueryTriggerInteraction.Ignore);
        if (hits == 0) 
            return;

        Vector3 fallbackDirection = -flowField.Sample(flickOrigin);
        fallbackDirection.y = 0f;

        bool hasFlicked = false;

        for (int i = 0; i < hits; i++)
        {
            Rigidbody rb = flickHits[i].attachedRigidbody;
            if (rb == null)
                continue;

            if (rb.TryGetComponent<IFlickable>(out IFlickable flickable))
            {
                Vector3 awayDirection = rb.worldCenterOfMass - flickOrigin;
                awayDirection.y = 0f;
                awayDirection = awayDirection.sqrMagnitude > 0.001f ? awayDirection : fallbackDirection;
                awayDirection.Normalize();

                Vector3 launchDirection = (awayDirection + Vector3.up * flickUpwardBias).normalized;

                flickable.OnFlick(launchDirection, flickForce);
                hasFlicked = true;
            }
        }

        if (!hasFlicked)
            return;

        if (flickParticle != null)
        {
            flickParticle.transform.position = flickOrigin + (Vector3.up * 0.15f);
            flickParticle.Play();
        }
    }

    private void OnDrawGizmos()
    {
        Color c = Color.cyan;
        c.a = 0.1f;

        Gizmos.color = c;
        Gizmos.DrawSphere(releasePos, flickRadius);
        Gizmos.DrawWireSphere(releasePos, flickRadius);
    }
}