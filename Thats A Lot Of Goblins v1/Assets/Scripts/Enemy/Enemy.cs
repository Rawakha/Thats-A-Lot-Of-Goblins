using UnityEngine;

public class Enemy : MonoBehaviour
{
    public Rigidbody body;
    public Transform visual;
    public MeshFilter meshFilter;
    public MeshRenderer[] renderers;

    [Header("Tracking")]
    public bool isAlive = true;
    public bool inPool = false;
    public bool inMover = false;

    [HideInInspector] public int moverIndex = -1;
    [HideInInspector] public float bobPhase;
    [HideInInspector] public float bobSpeedMul;
    [HideInInspector] public float leanPitch;
    [HideInInspector] public float leanRoll;
    [HideInInspector] public float speedMultiplier;
    [HideInInspector] public float steerCos = 1f;
    [HideInInspector] public float steerSin = 0f;
    [HideInInspector] public Quaternion facing = Quaternion.identity;

    public void Kill()
    {
        if (!isAlive)
            return;

        isAlive = false;
        EnemyMover.Instance.Remove(this);
        EnemyPool.Instance.Return(this);
    }
}