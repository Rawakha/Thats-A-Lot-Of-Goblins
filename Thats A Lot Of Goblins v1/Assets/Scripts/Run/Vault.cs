using UnityEngine;

public class Vault : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out Enemy e))
            return;

        RunManager.Instance.LoseLife();
        e.Despawn();
    }
}