using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class EnemyDeathManager : MonoBehaviour
{
    public static EnemyDeathManager Instance;

    [SerializeField] private float deathDuration = 0.1f;
    [SerializeField] private Ease deathEase = Ease.InBack;

    private struct Dying
    {
        public Enemy enemy;
        public float startTime;
    }

    private List<Dying> dying = new List<Dying>(128);

    private void Awake()
    {
        Utilities.CreateInstance(ref Instance, this);
    }

    private void Update()
    {
        if (dying == null || dying.Count == 0)
            return;

        float now = Time.time;

        for (int i = dying.Count - 1; i >= 0; i--)
        {
            Dying d = dying[i];

            if (d.enemy == null)
            {
                RemoveAtSwap(i);
                continue;
            }

            float t = Mathf.Clamp01((now - d.startTime) / deathDuration);
            float eased = DOVirtual.EasedValue(1f, 0f, t, deathEase);
            d.enemy.deathScale = Vector3.one * eased;

            if (t >= 1f)
            {
                EnemyStateMachine.Set(d.enemy, EnemyState.Pooled);
            }
        }
    }

    private void PlayDeathParticles(Enemy e)
    {
        if (e == null)
            return;

        // Play death particles
        Vector3 position = e.body.worldCenterOfMass;
        Vector3 awayDirecton = e.lastHitPos - position; awayDirecton.y = 0f;
        Quaternion rotation = awayDirecton.sqrMagnitude > 0.001f ? Quaternion.LookRotation(awayDirecton.normalized, Vector3.up) : Quaternion.LookRotation(-e.transform.forward, Vector3.up);
        EffectDirector.Instance.Request(EffectType.Death, position, rotation, e.renderColor);
    }

    public void Add(Enemy e)
    {
        if (e == null || e.dyingIndex >= 0)
            return;

        e.body.isKinematic = true;
        e.collider.enabled = false;
        e.dyingIndex = dying.Count;

        dying.Add(new Dying
        {
            enemy = e,
            startTime = Time.time
        });

        PlayDeathParticles(e);
    }

    public void Remove(Enemy e)
    {
        if (e == null || e.dyingIndex < 0)
            return;

        if (e.dyingIndex >= dying.Count || dying[e.dyingIndex].enemy != e)
        {
            Debug.LogError($"EnemyDeathManager: staled dyingIndex on {e.name}", e);

            for (int i = 0; i < dying.Count; ++i)
            {
                if (dying[i].enemy == e)
                {
                    dying.RemoveAt(i);
                    e.body.isKinematic = false;
                    e.collider.enabled = true;
                    e.dyingIndex = -1;
                    return;
                }
            }

            return;
        }

        RemoveAtSwap(e.dyingIndex);
        e.body.isKinematic = false;
        e.collider.enabled = true;
        e.dyingIndex = -1;
    }
        
    private void RemoveAtSwap(int index)
    {
        int lastIndex = dying.Count - 1;

        if (index != lastIndex)
        {
            Dying moved = dying[lastIndex];
            dying[index] = moved;
            moved.enemy.dyingIndex = index;
        }

        dying.RemoveAt(lastIndex);
    }
}