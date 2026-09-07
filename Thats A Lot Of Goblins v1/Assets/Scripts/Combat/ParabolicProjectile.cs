using UnityEngine;

public class ParabolicProjectile : MonoBehaviour
{
    private Enemy target;

    private Vector3 startPosition;
    private Vector3 lastTargetPosition;
    private Vector3 damageOrigin;

    private float damage;
    private float travelDuration;
    private float arcHeight;
    private float elapsed;

    private bool launched;
    private bool targetLost;

    public void Launch(Enemy target, float damage, float travelDuration, float arcHeight, Vector3 damageOrigin)
    {
        this.target = target;
        this.damage = damage;
        this.travelDuration = Mathf.Max(0.01f, travelDuration);
        this.arcHeight = arcHeight;
        this.damageOrigin = damageOrigin;

        startPosition = transform.position;
        lastTargetPosition = GetTargetPosition(target);
        targetLost = target == null || !target.IsAlive || target.body == null;
        launched = true;
        elapsed = 0f;
    }

    private void Update()
    {
        if (!launched)
            return;

        if (!targetLost)
        {
            if (target != null && target.IsAlive && target.body != null)
                lastTargetPosition = target.body.worldCenterOfMass;
            else
                targetLost = true;
        }

        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / travelDuration);

        Vector3 nextPosition = Vector3.Lerp(startPosition, lastTargetPosition, progress);
        nextPosition.y += 4f * arcHeight * progress * (1f - progress);

        Vector3 direction = nextPosition - transform.position;

        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        transform.position = nextPosition;

        if (progress >= 1f)
            Impact();
    }

    private void Impact()
    {
        launched = false;

        if (!targetLost && target != null && target.IsAlive && EnemyHealthManager.Instance != null)
            EnemyHealthManager.Instance.Damage(target, damage, damageOrigin);

        Destroy(gameObject);
    }

    private static Vector3 GetTargetPosition(Enemy target)
    {
        if (target != null && target.body != null)
            return target.body.worldCenterOfMass;

        return Vector3.zero;
    }
}
