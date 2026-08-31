using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class EnemyHealthManager : MonoBehaviour
{
    public static EnemyHealthManager Instance;

    [SerializeField] private int maxFeelPerFrame = 32;

    [Header("Knockback")]
    [SerializeField] private float knockbackPerDamage = 0.15f;
    [SerializeField] private float maxKnockback = 8f;
    [SerializeField] private float knockbackUpwardBias = 0.2f;

    private struct PendingDamage
    {
        public Enemy enemy;
        public float total;
        public Vector3 lastHitPos;
    }

    private List<PendingDamage> pending = new(256);

    private void Awake()
    {
        Utilities.CreateInstance(ref Instance, this);
    }

    private void LateUpdate()
    {
        int feelBudget = maxFeelPerFrame;

        for (int i = pending.Count - 1; i >= 0; i--)
        {
            PendingDamage p = pending[i];
            Enemy e = p.enemy;

            if (e == null)
                continue;

            e.currentHealth -= p.total;
            e.damageIndex = -1;

            Vector3 away = e.body.worldCenterOfMass - p.lastHitPos;
            away.y = 0f;
            if (away.sqrMagnitude > 0.001f)
            {
                away.Normalize();
                Vector3 dir = (away + Vector3.up * knockbackUpwardBias).normalized;
                float force = Mathf.Min(p.total * knockbackPerDamage, maxKnockback);
                e.body.AddForce(dir * force, ForceMode.VelocityChange);
            }

            if (e.currentHealth <= 0f)
            {
                EnemyStateMachine.Set(e, EnemyState.Dying);
                continue;
            }

            if (feelBudget > 0)
            {
                EnemyFeelManager.Instance.Add(e, EnemyFeelManager.FeelType.Hit);
                feelBudget--;
            }
        }

        pending.Clear();
    }

    public void Damage(Enemy e, float damage, Vector3 hitPos)
    {
        if (e == null || e.state == EnemyState.Dying || e.state == EnemyState.Pooled || damage <= 0f) 
            return;

        if (e.damageIndex >= 0 && e.damageIndex < pending.Count && pending[e.damageIndex].enemy == e) 
        { 
            PendingDamage p = pending[e.damageIndex];
            p.total += damage;
            p.lastHitPos = hitPos;
            pending[e.damageIndex] = p;
            return;
        }

        e.damageIndex = pending.Count;
        pending.Add(new PendingDamage 
        { 
            enemy = e,
            total = damage, 
            lastHitPos = hitPos
        });
    }
}