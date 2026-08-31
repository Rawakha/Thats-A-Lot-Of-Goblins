using UnityEngine;
using static EnemyFeelManager;

public static class EnemyStateMachine
{
    public static void Set(Enemy e, EnemyState next)
    {
        if (e == null || e.state == next)
            return;

        EnemyState previous = e.state;
        Exit(e, previous);
        e.state = next;
        Enter(e, next, previous);
    }

    private static void Enter(Enemy e, EnemyState s, EnemyState previous)
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
                if (previous == EnemyState.Airborne) 
                {
                    Debug.Log("Airborne Death");

                    EnemyFeelManager.Instance.Add(e, FeelType.Death, () => Set(e, EnemyState.Pooled));
                }
                else
                {
                    EnemyFeelManager.Instance.Add(e, FeelType.Hit);
                    EnemyDeathManager.Instance.Add(e);
                }
                break;

            case EnemyState.Pooled:
                EnemyManager.Instance.Despawn(e);
                break;
        }
    }

    private static void Exit(Enemy e, EnemyState s)
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
                EnemyDeathManager.Instance.Remove(e);
                break;
        }
    }

    public static void Launch(Enemy e, Vector3 direction, float force)
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