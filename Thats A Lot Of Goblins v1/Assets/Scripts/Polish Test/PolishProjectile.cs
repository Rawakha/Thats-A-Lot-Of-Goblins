using UnityEngine;

public class PolishProjectile : MonoBehaviour
{
    [SerializeField] private Rigidbody rb;
    [SerializeField] private float damage = 10f;

    private bool isShot = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        Hit(other);
    }

    public void Shoot(Vector3 direction, float force)
    {
        if (isShot)
            return;

        isShot = true;

        if (rb != null)
        {
            rb.rotation = Quaternion.LookRotation(direction);
            rb.AddForce(direction * force, ForceMode.Impulse);
        }
    }

    private void Hit(Collider other)
    {
        if (!isShot)
            return;

        isShot = false;

        if (other.TryGetComponent<PolishEnemy>(out var e))
        {
            Vector3 hitPos = other.ClosestPoint(transform.position);
            e.Damage(damage, hitPos);
        }

        Destroy(gameObject);
    }
}