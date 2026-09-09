using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class Tower : MonoBehaviour
{
    [SerializeField] private TowerDefinition definition;
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private int managerIndex;

    [Header("Spawn Animation")]
    [SerializeField] private Transform visual;
    [SerializeField] private float spawnStartScale = 0.15f;
    [SerializeField] private float spawnDuration = 0.25f;
    [SerializeField] private Ease spawnEase = Ease.OutBack;

    [Header("Targeting")]
    [SerializeField] protected float gainTargetRange = 5f;
    [SerializeField] protected float loseTargetRange = 6f;
    [SerializeField] protected Enemy currentTarget;
    [SerializeField] private bool drawTargetingGizmos = false;

    private Tween spawnTween;

    public TowerDefinition Definition => definition;
    public Vector3 WorldPosition => transform.position;
    public Vector2Int GridPosition => gridPosition;
    public int ManagerIndex => managerIndex;

    public void Initialize(TowerDefinition definition, Vector2Int gridPos, int index)
    {
        this.definition = definition;
        gridPosition = gridPos;
        managerIndex = index;

        TowerTargetingManager.Instance.Register(this);

        PlaySpawnAnimation();
    }

    public virtual void OnDestroy()
    {
        if (TowerTargetingManager.Instance != null)
            TowerTargetingManager.Instance.Unregister(this);
    }

    private void PlaySpawnAnimation()
    {
        spawnTween?.Kill();

        visual.localScale = Vector3.one * spawnStartScale;

        spawnTween = visual.DOScale(Vector3.one, spawnDuration).SetEase(spawnEase).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    public void SetManagerIndex(int managerIndex)
    {
        this.managerIndex = managerIndex;
    }

    private void OnDisable()
    {
        spawnTween?.Kill();
        spawnTween = null;
    }

    public virtual void GetTarget(List<Enemy> results)
    {
        if (results == null)
            return;

        // Default Get Closest
        currentTarget = EnemyUtilities.GetClosestEnemy(WorldPosition, gainTargetRange, results);
    }

    public virtual bool HasValidTarget()
    {
        return IsTargetValid(currentTarget);
    }

    protected virtual bool IsTargetValid(Enemy target)
    {
        if (target == null || !target.IsAlive) 
            return false;

        return Utilities.IsWithinHorizontalRange(target.body.position, WorldPosition, loseTargetRange);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (drawTargetingGizmos)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(WorldPosition, gainTargetRange);

            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(WorldPosition, loseTargetRange);

            if (currentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(WorldPosition, currentTarget.body.position);
            }
        }

        if (definition ==  null || TowerManager.Instance == null)
            return;
        
        Grid g = TowerManager.Instance.Grid;
        Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.35f);

        for (int y = 0; y < definition.size.y; y++)
        {
            for (int x = 0; x < definition.size.x; x++)
            {
                Vector2Int cell = new Vector2Int(gridPosition.x + x, gridPosition.y + y);
                if (g.InBounds(cell))
                {
                    Vector3 centre = g.CellCentre(cell);
                    Gizmos.DrawCube(centre + Vector3.up * 0.05f, new Vector3(g.cellSize, 0.05f, g.cellSize) * 0.9f);
                }
            }
        }
    }
#endif
}