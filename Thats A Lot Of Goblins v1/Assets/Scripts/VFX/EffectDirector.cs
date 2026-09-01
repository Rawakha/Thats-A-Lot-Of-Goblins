using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class EffectDirector : MonoBehaviour
{
    public static EffectDirector Instance;

    [SerializeField] private EffectPool[] effectPools;
    [SerializeField] private int maxRequestProcessesPerFrame = 12;
    [SerializeField] private int maxQueueSize = 60;

    private struct EffectRequest
    {
        public EffectType type;
        public Vector3 position;
        public Quaternion rotation;
        public Color color;
    }

    private Queue<EffectRequest> pending = new();

    private void Awake()
    {
        Utilities.CreateInstance(ref Instance, this);

        if (effectPools == null || effectPools.Length == 0)
            return;

        for (int i = 0; i < effectPools.Length; i++)
        {
            CreatePool(effectPools[i], transform);
        }
    }

    private void Update()
    {
        if (effectPools == null || effectPools.Length == 0)
        {
            Debug.LogError("EffectDirector: No Pools Assigned");
            return;
        }

        for (int i = 0; i < effectPools.Length; i++)
        {
            effectPools[i].ReturnFinished(Time.time);
        }

        int processed = 0;

        while (pending.Count > 0 && processed < maxRequestProcessesPerFrame)
        {
            EffectRequest r = pending.Dequeue();
            ProcessRequest(r);
            processed++;
        }
    }

    private void ProcessRequest(EffectRequest request)
    {
        EffectPool pool = GetPool(request.type);

        if (pool == null)
            return;

        pool.Play(request.position, request.rotation, request.color);
    }

    private EffectPool GetPool(EffectType type)
    {
        for (int i = 0; i < effectPools.Length; i++)
        {
            if (effectPools[i].type == type)
                return effectPools[i];
        }

        return null;
    }

    public void Request(EffectType type, Vector3 position, Quaternion rotation, Color color = default)
    {
        if (pending.Count >= maxQueueSize)
            return;

        pending.Enqueue(new EffectRequest
        {
            type = type,
            position = position,
            rotation = rotation,
            color = color
        });
    }

    private static void CreatePool(EffectPool pool, Transform transform)
    {
        if (pool == null || pool.effect == null || pool.poolSize <= 0)
            return;

        for (int i = 0; i < pool.poolSize; i++)
        {
            ParticleSystem ps = Instantiate(pool.effect, transform);
            pool.Return(ps);
        }
    }
}