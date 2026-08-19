using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class EffectPool : MonoBehaviour
{
    [SerializeField] private VisualEffect bloodEffect;
    [SerializeField] private int poolSize = 30;
    [SerializeField] private float bloodDuration = 3f;

    [Header("Stats")]
    [SerializeField, ReadOnly] private int currentPoolSize = 0;
    [SerializeField, ReadOnly] private int active = 0;
    [SerializeField, ReadOnly] private int inactive = 0;
    [SerializeField, ReadOnly] private int peakActive = 0;

    private struct ActiveEffect
    {
        public VisualEffect effect;
        public float startTime;
    }

    private Queue<VisualEffect> effectQueue;
    private List<ActiveEffect> activeEffects;

    private void Awake()
    {
        CreatePool();
    }

    private void Update()
    {
        if (activeEffects == null || activeEffects.Count == 0)
            return;

        float time = Time.time;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveEffect activeEffect = activeEffects[i];

            if (time - activeEffect.startTime >= bloodDuration)
            {
                Return(activeEffect.effect);
                activeEffects.RemoveAt(i);
            }
        }
    }

    private void CreatePool()
    {
        if (bloodEffect == null)
            return;

        effectQueue = new Queue<VisualEffect>(poolSize);
        activeEffects = new List<ActiveEffect>();

        for (int i = 0; i < poolSize; i++)
        {
            Return(Instantiate(bloodEffect, transform));
        }
    }

    private void Return(VisualEffect effect)
    {
        if (effect == null)
            return;

        effect.Stop();
        effect.gameObject.SetActive(false);
        effectQueue.Enqueue(effect);
    }

    public void Play(Vector3 position, Quaternion rotation)
    {
        if (effectQueue.Count == 0)
        {
            // Debug.Log("BloodPool: No blood left in pool");
            return;
        }

        VisualEffect effect = effectQueue.Dequeue();
        effect.transform.position = position;
        effect.transform.rotation = rotation;
        effect.gameObject.SetActive(true);
        effect.Play();

        activeEffects.Add(new ActiveEffect
        {
            effect = effect,
            startTime = Time.time
        });
    }
}