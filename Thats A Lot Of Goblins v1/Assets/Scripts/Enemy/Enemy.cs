using System;
using UnityEngine;

public class Enemy : MonoBehaviour, IFlickable
{
    public Rigidbody body;
    public Collider collider;
    public Transform visual;
    public MeshFilter meshFilter;
    public MeshRenderer renderer;
    public Material assignedMaterial;

    [Header("Settings")]
    public float maxFlickDuration = 2f;
    public float flickTorqueMultiplier = 0.25f;

    [Header("Health")]
    public float maxHealth = 50f;
    public float currentHealth = 0f;

    [Header("Rendering")]
    public Color renderColor = Color.white;
    [Range(0f, 1f)] public float emissionValue;

    [Header("Tracking")]
    public EnemyState state = EnemyState.Pooled;
    public bool inMover = false;
    public bool inRenderer = false;
    public bool inAnimator = false;
    public bool inFeedback = false;

    [HideInInspector] public int gridCell = -1;
    [HideInInspector] public int masterIndex = -1;
    [HideInInspector] public int moverIndex = -1;
    [HideInInspector] public int rendererIndex = -1;
    [HideInInspector] public int animatorIndex = -1;
    [HideInInspector] public int damageIndex = -1;
    [HideInInspector] public int dyingIndex = -1;
    [HideInInspector] public int feedbackIndex = -1;
    [HideInInspector] public int airborneIndex = -1;
    [HideInInspector] public int screamHandle = 0;
    [HideInInspector] public float speed;
    [HideInInspector] public float maxSpeed;
    [HideInInspector] public float speedMultiplier;
    [HideInInspector] public float steerCos = 1f;
    [HideInInspector] public float steerSin = 0f;
    [HideInInspector] public float movementPausedUntil;
    [HideInInspector] public float movementPhase;
    [HideInInspector] public float movementAnimationWeight;
    [HideInInspector] public float turnRate;
    [HideInInspector] public float currentTurningLean;
    [HideInInspector] public Vector3 previousMovementPosition;
    [HideInInspector] public Vector3 animationVelocity;
    [HideInInspector] public float animationSpeed;
    [HideInInspector] public float pendingLaunchSpeed;
    [HideInInspector] public Vector3 moveDelta;

    // Transform Accumulation
    [HideInInspector] public Vector3 visualStartPos = Vector3.zero;
    [HideInInspector] public Quaternion visualStartRot = Quaternion.identity;
    [HideInInspector] public Vector3 visualBaseScale = Vector3.one;
    [HideInInspector] public Vector3 feedbackScale = Vector3.one;
    [HideInInspector] public Vector3 animScale = Vector3.one;
    [HideInInspector] public Vector3 animOffset = Vector3.zero;
    [HideInInspector] public Quaternion animRot = Quaternion.identity;
    [HideInInspector] public Quaternion facing = Quaternion.identity;

    public void OnSpawn()
    {
        currentHealth = maxHealth;

        maxSpeed = 1f;
        gridCell = -1;
        masterIndex = -1;
        moverIndex = -1;
        rendererIndex = -1;
        animatorIndex = -1;
        damageIndex = -1;
        dyingIndex = -1;
        feedbackIndex = -1;
        airborneIndex = -1;
        screamHandle = 0;

        inMover = false;
        inRenderer = false;
        inAnimator = false;
        inFeedback = false;

        steerCos = 1f;
        steerSin = 0f;
        movementPhase = 0f;
        movementPausedUntil = 0f;
        movementAnimationWeight = 0f;
        turnRate = 0f;
        currentTurningLean = 0f;
        pendingLaunchSpeed = 0f;
        moveDelta = Vector3.zero;

        feedbackScale = Vector3.one;
        animScale = Vector3.one;
        animOffset = Vector3.zero;
        animRot = Quaternion.identity;

        body.isKinematic = false;
        collider.enabled = true;
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

        visualStartPos = visual.localPosition;
        visualStartRot = visual.localRotation;

        if (renderer != null )
            assignedMaterial = renderer.sharedMaterial;
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