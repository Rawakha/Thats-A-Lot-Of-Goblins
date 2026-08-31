using UnityEngine;

public class Vault : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out Enemy e))
            return;

        if (e.state != EnemyState.Walking && e.state != EnemyState.Airborne)
            return;

        RunManager.Instance.LoseLife();
        EnemyStateMachine.Set(e, EnemyState.Pooled);
    }
}