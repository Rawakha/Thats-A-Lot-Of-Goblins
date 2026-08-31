using UnityEngine;

public class PolishShooter : MonoBehaviour
{
    [SerializeField] private Transform shootPoint;
    [SerializeField] private PolishProjectile projectilePrefab;
    [SerializeField] private float shootForce = 20f;
    [SerializeField] private float spreadDegrees = 3f;

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private float targetHeight = 0.5f;

    [InspectorButton]
    public void ShootProjectile()
    {
        if (shootPoint == null || projectilePrefab == null)
            return;

        Vector3 startPos = shootPoint.position;
        Vector3 endPos = new Vector3(target.position.x, targetHeight, target.position.z);
        Vector3 direction = (endPos - startPos).normalized;

        if (spreadDegrees > 0f)
        {
            Quaternion offset = Quaternion.Euler(
                Random.Range(-spreadDegrees, spreadDegrees),
                Random.Range(-spreadDegrees, spreadDegrees),
                0f);

            direction = offset * direction;
        }

        PolishProjectile p = Instantiate(projectilePrefab, shootPoint.position, shootPoint.rotation);
        p.gameObject.SetActive(true);
        p.Shoot(direction, shootForce);
    }

    private void OnDrawGizmos()
    {
        if (target == null || shootPoint == null) 
            return;

        Vector3 startPos = shootPoint.position;
        Vector3 endPos = new Vector3(target.position.x, targetHeight, target.position.z);
        Vector3 direction = (endPos - startPos).normalized;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(startPos, endPos);
        Gizmos.DrawWireSphere(endPos, 0.1f);
    }
}