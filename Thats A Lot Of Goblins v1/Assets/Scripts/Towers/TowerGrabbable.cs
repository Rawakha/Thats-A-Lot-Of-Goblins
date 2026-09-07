using DG.Tweening;
using System;
using System.Collections;
using UnityEngine;

public class TowerGrabbable : Grabbable
{
    [Header("Tower Settings")]
    [SerializeField] private TowerDefinition towerDefinition;

    [Header("Release PD")]
    [SerializeField] private bool useReleasePD = true;
    [SerializeField] private float strength = 5f;
    [SerializeField] private float damping = 5f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayer;

    private bool canPlace = false;
    private bool released = false;
    private bool spawning = false;
    private Vector2Int releasedCell;

    private void Update()
    {
        if (State == GrabState.Held)
        {
            // Check Placement
            Vector2Int centreCell = GetFootprintOrigin(TowerManager.Instance.Grid);
            canPlace = TowerManager.Instance.IsFootprintFree(centreCell, towerDefinition.size);
        }
        else if (useReleasePD && released && canPlace)
        {
            // Apply PD force to snap to grid
            Vector3 centre = TowerManager.Instance.GetFootprintCenter(releasedCell, towerDefinition.size);
            Utilities.ApplyForcePD(body, centre, strength, damping);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!released || !canPlace || spawning || collision == null)
            return;

        if (!Utilities.IsInLayerMask(collision.gameObject, groundLayer))
            return;

        Tower tower = TowerManager.Instance.Place(towerDefinition, releasedCell);
        Destroy(gameObject);
    }

    protected override void OnReleasedInternal(Vector3 velocity)
    {
        released = true;

        Vector2Int centreCell = GetFootprintOrigin(TowerManager.Instance.Grid);
        canPlace = TowerManager.Instance.IsFootprintFree(centreCell, towerDefinition.size);

        if (!canPlace)
            return;

        releasedCell = centreCell;
        body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
    }

    private Vector2Int GetFootprintOrigin(Grid grid)
    {
        Vector2Int cell = grid.WorldToCell(transform.position);
        return new Vector2Int(cell.x - (towerDefinition.size.x - 1) / 2, cell.y - (towerDefinition.size.y - 1) / 2);
    }

    private void OnDrawGizmos()
    {
        if (State == GrabState.Held)
        {
            Gizmos.color = canPlace ? Color.green : Color.red;

            Grid grid = TowerManager.Instance.Grid;
            Vector2Int origin = GetFootprintOrigin(grid);

            Vector3 centre = TowerManager.Instance.GetFootprintCenter(origin, towerDefinition.size);
            Vector3 size = new Vector3(towerDefinition.size.x * grid.cellSize, 0.1f, towerDefinition.size.y * grid.cellSize);
            Gizmos.DrawWireCube(centre, size);
        }
    }
}