using UnityEngine;

public class Enemy : MonoBehaviour, IFlickable
{
    public Rigidbody body;
    public Collider collider;
    public Transform visual;
    public MeshFilter meshFilter;
    public MeshRenderer renderer;

    [Header("Components")]
    public EnemyFeedback feedback;

    [Header("Settings")]
    public float maxFlickDuration = 2f;
    public float flickTorqueMultiplier = 0.25f;

    [Header("Tracking")]
    public bool isAlive = true;
    public bool inPool = false;
    public bool inMover = false;
    public bool isRotationEnabled = true;
    public bool isProvidingContacts = false;
    public bool isFlicked = false;

    [HideInInspector] public int moverIndex = -1;
    [HideInInspector] public float bobPhase;
    [HideInInspector] public float bobSpeedMul;
    [HideInInspector] public float leanPitch;
    [HideInInspector] public float leanRoll;
    [HideInInspector] public float speedMultiplier;
    [HideInInspector] public float steerCos = 1f;
    [HideInInspector] public float steerSin = 0f;
    [HideInInspector] public Quaternion facing = Quaternion.identity;
    [HideInInspector] public float movementPausedUntil;

    public void Kill()
    {
        if (!isAlive)
            return;

        // EffectDirector.Instance.Play(transform.position, Quaternion.identity);

        if (feedback != null)
        {
            feedback.PlayDeath(Despawn);
        }
        else
        {
            Despawn();
        }
    }

    public void Despawn()
    {
        if (!isAlive)
            return;

        if (feedback != null)
        {
            feedback.StopFeedback();
        }

        isAlive = false;
        EnemyMover.Instance.Remove(this);
        EnemyPool.Instance.Return(this);
    }

    public void ResetEnemy()
    {
        isAlive = true;
        inPool = false;
        inMover = false;
        isFlicked = false;
        moverIndex = -1;
        bobPhase = 0f;
        bobSpeedMul = 1f;
        leanPitch = 0f;
        leanRoll = 0f;
        speedMultiplier = 1f;
        steerCos = 1f;
        steerSin = 0f;
        facing = Quaternion.identity;
        movementPausedUntil = 0f;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        SetRotationEnabled(false);
        SetCollisionCallbacksEnabled(false);

        if (feedback != null)
            feedback.StopFeedback();
    }

    public void PauseMovement(float duration)
    {
        movementPausedUntil = Mathf.Max(movementPausedUntil, Time.time + duration);
    }

    public void SetRotationEnabled(bool enabled)
    {
        isRotationEnabled = enabled;

        if (enabled)
        {
            body.constraints &= ~RigidbodyConstraints.FreezeRotation;
            body.linearDamping = 0f;
        }
        else
        {
            body.angularVelocity = Vector3.zero;
            body.rotation = facing;
            body.constraints |= RigidbodyConstraints.FreezeRotation;
            body.linearDamping = 0.25f;
        }
    }    

    public void SetCollisionCallbacksEnabled(bool enabled)
    {
        if (collider == null || isProvidingContacts == enabled)
            return;

        isProvidingContacts = enabled;
        collider.providesContacts = enabled;

        if (enabled)
        {
            EnemyContactManager.Instance.Add(this);
        }
        else if (EnemyContactManager.Instance != null)
        {
            EnemyContactManager.Instance.Remove(this);
        }
    }

    public void OnFlick(Vector3 direction, float force)
    {
        if (isFlicked || !isAlive)
            return;

        isFlicked = true;
        EnemyMover.Instance.Remove(this);
        SetRotationEnabled(true);
        SetCollisionCallbacksEnabled(true);

        if (feedback != null)
            feedback.PlayFlick();

        Vector3 torqueAxis = Vector3.Cross(Vector3.up, direction).normalized;
        float torque = force * flickTorqueMultiplier;
        body.AddTorque(torqueAxis * torque, ForceMode.Impulse);
        body.AddForce(direction * force, ForceMode.Impulse);
    }
}