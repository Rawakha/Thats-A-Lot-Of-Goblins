using UnityEngine;

public class Enemy : MonoBehaviour
{
    public Rigidbody body;

    [Header("Tracking")]
    public bool inPool = false;
    public bool inMover = false;
}