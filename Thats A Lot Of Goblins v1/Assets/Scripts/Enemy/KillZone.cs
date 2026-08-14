using UnityEngine;

public class KillZone : MonoBehaviour
{
    private EnemyPool pool;
    private EnemyMover mover;

    private void Awake()
    {
        pool = EnemyPool.Instance;
        mover = EnemyMover.Instance;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out Enemy e))
            return;

        mover.Remove(e);
        pool.Return(e);
    }
}