using UnityEngine;

public enum GrabState
{ 
    Free,
    Held
}

[RequireComponent(typeof(Rigidbody))]
public class Grabbable : MonoBehaviour
{
    [SerializeField] protected Rigidbody body;

    [Header("Grab Settings")]
    [SerializeField] private float positionSpring = 200f;
    [SerializeField] private float positionDamper = 25f;
    [SerializeField] private float maxThrowSpeed = 20f;

    [Header("Tracking")]
    [SerializeField, ReadOnly] private GrabState state = GrabState.Free;

    private Vector3 targetPos;

    public GrabState State => state;
    public Vector3 Position => body != null ? body.position : transform.position;

    private void FixedUpdate()
    {
        if (body == null)
            return;

        switch (state)
        {
            case GrabState.Held:
                Utilities.ApplyForcePD(body, targetPos, positionSpring, positionDamper);
                break;
        }
    }

    public void OnGrabbed()
    {
        if (state == GrabState.Held)
            return;

        state = GrabState.Held;
    }

    public void OnDragged(Vector3 worldPos)
    {
        targetPos = worldPos;
    }

    public void OnReleased(Vector3 velocity)
    {
        if (state == GrabState.Free)
            return;

        if (body != null)
        {
            if (velocity.sqrMagnitude > maxThrowSpeed * maxThrowSpeed)
                velocity = velocity.normalized * maxThrowSpeed;

            body.linearVelocity = velocity;
        }

        state = GrabState.Free;

        OnReleasedInternal(velocity);
    }

    protected virtual void OnReleasedInternal(Vector3 velocity)
    {

    }
}