using System;
using UnityEngine;

public class EnemyData : MonoBehaviour, IFlickable
{
    public Rigidbody body;
    public Collider collider;
    public Transform visual;
    public MeshFilter meshFilter;
    public MeshRenderer renderer;

    [Header("Settings")]
    public float maxFlickDuration = 2f;
    public float flickTorqueMultiplier = 0.25f;

    [Header("Tracking")]
    public EnemyState state = EnemyState.Pooled;
    public bool inMover = false;
    public bool inAnimator = false;
    public bool inFeedback = false;

    [HideInInspector] public int masterIndex = -1;
    [HideInInspector] public int moverIndex = -1;
    [HideInInspector] public int animatorIndex = -1;
    [HideInInspector] public int feedbackIndex = -1;
    [HideInInspector] public int airborneIndex = -1;
    [HideInInspector] public int screamHandle = 0;
    [HideInInspector] public float speed;
    [HideInInspector] public float maxSpeed;
    [HideInInspector] public float bobPhase;
    [HideInInspector] public float bobSpeedMul;
    [HideInInspector] public float leanPitch;
    [HideInInspector] public float leanRoll;
    [HideInInspector] public float speedMultiplier;
    [HideInInspector] public float steerCos = 1f;
    [HideInInspector] public float steerSin = 0f;
    [HideInInspector] public float movementPausedUntil;
    [HideInInspector] public float pendingLaunchSpeed;
    [HideInInspector] public Vector3 moveDelta;

    // Transform Accumulation
    [HideInInspector] public Vector3 visualBaseScale = Vector3.one;
    [HideInInspector] public Vector3 feedbackScale = Vector3.one;
    [HideInInspector] public Vector3 animScale = Vector3.one;
    [HideInInspector] public Vector3 animOffset = Vector3.zero;
    [HideInInspector] public Quaternion animRotation = Quaternion.identity;
    [HideInInspector] public Quaternion facing = Quaternion.identity;

    public void OnSpawn()
    {
        maxSpeed = 1f;
        masterIndex = -1;
        moverIndex = -1;
        animatorIndex = -1;
        feedbackIndex = -1;
        airborneIndex = -1;
        screamHandle = 0;

        inMover = false;
        inAnimator = false;
        inFeedback = false;

        bobPhase = 0f;
        leanPitch = 0f;
        leanRoll = 0f;
        steerCos = 1f;
        steerSin = 0f;
        movementPausedUntil = 0f;
        pendingLaunchSpeed = 0f;
        moveDelta = Vector3.zero;

        feedbackScale = Vector3.one;
        animScale = Vector3.one;
        animOffset = Vector3.zero;
        animRotation = Quaternion.identity;
    }

    public void OnDespawn()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        SetRotationEnabled(false);
    }

    public void OnCreated()
    {
        EnemyManager.Instance.Variation.Apply(this);
    }

    public void PauseMovement(float duration)
    {
        movementPausedUntil = Mathf.Max(movementPausedUntil, Time.time + duration);
    }

    public void SetRotationEnabled(bool enabled)
    {
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

    public void Flick(Vector3 direction, float force)
    {
        EnemyStateMachine.Launch(this, direction, force);
    }

}