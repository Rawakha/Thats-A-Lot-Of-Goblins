using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class EffectDirector : MonoBehaviour
{
    public static EffectDirector Instance;

    [SerializeField] private EffectPool pool;
    [SerializeField] private int maxPlaysPerFrame = 12;
    [SerializeField] private int maxQueueSize = 60;

    private struct Request
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    private Queue<Request> pending = new();

    private void Awake()
    {
        Utilities.CreateInstance(ref Instance, this);
    }

    private void Update()
    {
        if (pool == null)
        {
            Debug.LogError("BloodDirector: No Pool Assigned");
            return;
        }

        int played = 0;

        while (pending.Count > 0 && played < maxPlaysPerFrame)
        {
            Request r = pending.Dequeue();
            pool.Play(r.position, r.rotation);
            played++;
        }
    }

    public void Play(Vector3 position, Quaternion rotation)
    {
        if (pending.Count >= maxQueueSize)
            return;

        pending.Enqueue(new Request
        {
            position = position,
            rotation = rotation
        });
    }
}