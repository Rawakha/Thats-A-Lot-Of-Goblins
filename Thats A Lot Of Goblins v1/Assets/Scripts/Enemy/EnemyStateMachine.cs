using UnityEngine;

public static class EnemyStateMachine
{
    public static void Set(EnemyData e, EnemyState next)
    {
        if (e == null || e.state == next)
            return;

        Exit(e, e.state);
        e.state = next;
        Enter(e, next);
    }

    private static void Enter(EnemyData e, EnemyState s)
    {
        if (e == null)
            return;

        switch (s)
        {
            case EnemyState.Walking:
                e.SetRotationEnabled(false);
                EnemyMover.Instance.Add(e);
                break;

            case EnemyState.Airborne:
                EnemyMover.Instance.Remove(e);
                e.SetRotationEnabled(true);
                EnemyAirborneManager.Instance.Add(e, e.pendingLaunchSpeed);
                EnemyFeelManager.Instance.Add(e, EnemyFeelManager.FeelType.Launch);
                break;

            case EnemyState.Dying:
                EnemyMover.Instance.Remove(e);
                EnemyFeelManager.Instance.Add(e, EnemyFeelManager.FeelType.Death, () =>
                {
                    Set(e, EnemyState.Pooled);
                });
                break;

            case EnemyState.Pooled:
                EnemyManager.Instance.Despawn(e);
                break;
        }
    }

    private static void Exit(EnemyData e, EnemyState s)
    {
        if (e == null)
            return;

        switch (s)
        {
            case EnemyState.Walking:
                EnemyMover.Instance.Remove(e);
                break;

            case EnemyState.Airborne:
                EnemyAirborneManager.Instance.Remove(e);
                break;

            case EnemyState.Dying:
                EnemyFeelManager.Instance.Remove(e);
                break;
        }
    }

    public static void Launch(EnemyData e, Vector3 direction, float force)
    {
        if (e == null || e.state != EnemyState.Walking)
            return;

        e.pendingLaunchSpeed = force;
        Set(e, EnemyState.Airborne);

        // Apply the force
        if (e.body != null)
        {
            Vector3 torqueAxis = Vector3.Cross(Vector3.up, direction).normalized;
            float torque = force * e.flickTorqueMultiplier;
            e.body.AddTorque(torqueAxis * torque, ForceMode.Impulse);
            e.body.AddForce(direction * force, ForceMode.Impulse);
        }
    }
}