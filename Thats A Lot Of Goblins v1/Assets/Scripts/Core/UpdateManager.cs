using System;
using UnityEngine;

public class UpdateManager : MonoBehaviour
{
    public static UpdateManager Instance { get; private set; }

    // Events
    public event Action OnUpdate;
    public event Action OnFixedUpdate;
    public event Action OnLateUpdate;

    // Custom update intervals
    public event Action OnUpdate15hz;
    public event Action OnUpdate30hz;
    public event Action OnUpdate60hz;

    private const float update15HzInterval = 1f / 15f; // 15 times per second
    private const float update30HzInterval = 1f / 30f; // 30 times per second
    private const float update60HzInterval = 1f / 60f; // 60 times per second

    private float next15hz;
    private float next30hz;
    private float next60hz;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null); // Ensure the UpdateManager is not a child of any other object
            DontDestroyOnLoad(gameObject);

            next15hz = Time.time + update15HzInterval;
            next30hz = Time.time + update30HzInterval;
            next60hz = Time.time + update60HzInterval;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        OnUpdate?.Invoke();

        Tick(ref next15hz, update15HzInterval, OnUpdate15hz, 5);
        Tick(ref next30hz, update30HzInterval, OnUpdate30hz, 3);
        Tick(ref next60hz, update60HzInterval, OnUpdate60hz, 1);
    }

    private void FixedUpdate()
    {
        OnFixedUpdate?.Invoke();
    }

    private void LateUpdate()
    {
        OnLateUpdate?.Invoke();
    }

    private static void Tick(ref float nextTime, float interval, Action action, int maxCatchUp)
    {
        if (action == null) return;

        int ticks = 0;
        while (Time.time >= nextTime)
        {
            action.Invoke();
            nextTime += interval;
            
            if (ticks++ >= maxCatchUp)
            {
                Debug.LogWarning($"UpdateManager: Caught up to {ticks} ticks, stopping further updates to prevent performance issues.");
                nextTime = Time.time + interval; // Reset next time to prevent further updates
                break;
            }
        }
    }
}