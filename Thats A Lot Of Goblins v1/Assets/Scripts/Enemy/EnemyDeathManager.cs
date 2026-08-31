using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class EnemyDeathManager : MonoBehaviour
{
    public static EnemyDeathManager Instance;

    [SerializeField] private float toppleDuration = 0.5f;
    [SerializeField] private float lingerDuration = 3f;
    [SerializeField] private float sinkDuration = 1f;
    [SerializeField] private float sinkDepth = 5f;

    private struct Dying
    {
        public Enemy enemy;
        public float startTime;
        public Quaternion startRotation;
        public Quaternion targetRotation;
        public Vector3 startPosition;
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

            float elapsed = now - d.startTime;
            if (elapsed < toppleDuration)
            {
                float t = elapsed / toppleDuration;
                float eased = DOVirtual.EasedValue(0f, 1f, t, Ease.InQuad);
                d.enemy.visual.rotation = Quaternion.Slerp(d.startRotation, d.targetRotation, eased);
            }
            else if (elapsed > toppleDuration + lingerDuration)
            {
                float t = (elapsed - toppleDuration - lingerDuration) / sinkDuration;
                d.enemy.transform.position = d.startPosition + Vector3.down * (t * sinkDepth);

                if (t >= 1f)
                {
                    EnemyStateMachine.Set(d.enemy, EnemyState.Pooled);
                }
            }
        }
    }

    public void Add(Enemy e)
    {
        if (e == null || e.dyingIndex >= 0)
            return;

        e.body.isKinematic = true;
        e.collider.enabled = false;
        e.dyingIndex = dying.Count;

        // Later Change to Damage Direction + Variance
        float yaw = Random.Range(0f, 360f);
        Quaternion target = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);

        dying.Add(new Dying
        {
            enemy = e,
            startTime = Time.time,
            startRotation = e.visual.rotation,
            targetRotation = target,
            startPosition = e.visual.position
        });
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
        
    public void RemoveAtSwap(int index)
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