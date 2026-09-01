using System;
using System.Collections.Generic;
using UnityEngine;

public enum EffectType
{
    Hit,
    Death
}

public struct ActiveEffect
{
    public ParticleSystem effect;
    public float startTime;
}

[System.Serializable]
public class EffectPool
{
    public EffectType type;
    public int poolSize = 30;
    public ParticleSystem effect;
    public float effectDuration = 3f;

    private Queue<ParticleSystem> inactiveEffects = new();
    private List<ActiveEffect> activeEffects = new();

    public void ReturnFinished(float time)
    {
        if (activeEffects == null || activeEffects.Count == 0)
            return;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveEffect activeEffect = activeEffects[i];

            if (time - activeEffect.startTime >= effectDuration)
            {
                Return(activeEffect.effect);
                activeEffects.RemoveAt(i);
            }
        }
    }

    public void Play(Vector3 position, Quaternion rotation, Color color = default)
    {
        if (!TryGetEffect(out ParticleSystem effect))
            return;

        if (color != default)
        {
            var main = effect.main;
            main.startColor = color;
        }

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

    private bool TryGetEffect(out ParticleSystem effect)
    {
        if (inactiveEffects.Count > 0)
        {
            effect = inactiveEffects.Dequeue();
        }
        else if (activeEffects.Count > 0)
        {
            effect = activeEffects[0].effect;
            activeEffects.RemoveAt(0);
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        else
        {
            effect = null;
            return false;
        }

        return true;
    }

    public void Return(ParticleSystem effect)
    {
        if (effect == null)
            return;

        effect.Stop();
        effect.gameObject.SetActive(false);
        inactiveEffects.Enqueue(effect);
    }
}