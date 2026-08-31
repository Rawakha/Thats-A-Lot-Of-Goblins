using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class EnemyHealthTester : MonoBehaviour
{
    [SerializeField] private LayerMask enemyLayerMask;
    [SerializeField] private float damageAmount = 50f;
    [SerializeField] private float damageRadius = 4f;
    [SerializeField] private bool drawGizmos = true;

    private List<Enemy> queryResults = new List<Enemy>();

    private void Update()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (!PlayerInput.Instance.TryGetGroundPoint(out Vector3 point))
                return;

            Collider[] hits = Physics.OverlapSphere(point, damageRadius, enemyLayerMask);

            int hitCount = EnemyGrid.Instance.QueryRadius(point, damageRadius, queryResults);
            // Gizmos
            EnemyGrid.Instance.DebugDrawQuery(point, damageRadius);

            if (hitCount > 0) 
            {
                for (int i = 0; i < hitCount; i++)
                {
                    Enemy e = queryResults[i];
                    if (e == null) continue;

                    Vector3 hitPoint = e.collider.ClosestPoint(point);
                    EnemyHealthManager.Instance.Damage(e, damageAmount, hitPoint);
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !drawGizmos)
            return;

        Color c = Color.red;
        c.a = 0.2f;
        Gizmos.color = c;

        Gizmos.DrawWireSphere(PlayerInput.Instance.MouseWorldPos, damageRadius);
        Gizmos.DrawSphere(PlayerInput.Instance.MouseWorldPos, damageRadius);
    }
}
