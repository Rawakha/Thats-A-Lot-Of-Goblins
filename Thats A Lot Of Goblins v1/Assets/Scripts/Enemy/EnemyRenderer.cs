using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(1000)]
public class EnemyRenderer : MonoBehaviour
{
    private const int MaxInstancesPerBatch = 1023;

    [Header("Rendering")]
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;
    [SerializeField] private int subMeshIndex;
    [SerializeField] private ShadowCastingMode shadowCasting = ShadowCastingMode.On;
    [SerializeField] private bool receivedShadows = true;

    [Header("Tracking")]
    [SerializeField] private List<Enemy> enemies = new List<Enemy>(1000);

    private readonly Matrix4x4[] matrices = new Matrix4x4[MaxInstancesPerBatch];
    private readonly Vector4[] colorFlashValues = new Vector4[MaxInstancesPerBatch];

    private MaterialPropertyBlock propertyBlock;

    private static readonly int GoblinColorFlashId = Shader.PropertyToID("_GoblinColorFlash");

    public bool Initialize(EnemyManager manager)
    {
        if (mesh == null || material == null)
        {
            Debug.LogError("EnemyRenderer: Missing mesh or material.", this);
            return false;
        }

        material.enableInstancing = true;
        propertyBlock = new MaterialPropertyBlock();
        return true;
    }

    private void LateUpdate()
    {
        DrawEnemies();
    }

    private void DrawEnemies()
    {
        int totalCount = enemies.Count;
        for (int batchStart = 0; batchStart < totalCount; batchStart += MaxInstancesPerBatch)
        {
            int listCount = Mathf.Min(MaxInstancesPerBatch, totalCount - batchStart);
            int validCount = 0;

            for (int i = 0; i < listCount; i++)
            {
                Enemy e = enemies[batchStart + i];

                if (e == null || e.visual == null || !e.gameObject.activeInHierarchy)
                    continue;

                Color color = e.renderColor;
                colorFlashValues[validCount] = new Vector4(color.r, color.g, color.b, Mathf.Clamp01(e.emissionValue));
                matrices[validCount] = e.visual.localToWorldMatrix;

                validCount++;
            }

            if (validCount == 0)
                continue;

            propertyBlock.SetVectorArray(GoblinColorFlashId, colorFlashValues);
            Graphics.DrawMeshInstanced(mesh, subMeshIndex, material, matrices, listCount, propertyBlock, shadowCasting, receivedShadows, gameObject.layer, null, LightProbeUsage.Off);
        }
    }

    public void Add(Enemy e)
    {
        if (e == null || e.inRenderer)
            return;

        enemies.Add(e);
        e.inRenderer = true;
        e.rendererIndex = enemies.Count - 1;

        if (e.renderer != null)
            e.renderer.enabled = false;
    }

    public void Remove(Enemy e)
    {
        if (e == null || !e.inRenderer)
            return;

        int index = e.rendererIndex;
        if (index < 0 || index >= enemies.Count || enemies[index] != e)
        {
            Debug.LogError($"EnemyRenderer: stale rendererIndez on {e.name}", e);
            enemies.Remove(e);
            ResetTracking(e);
            return;
        }

        int lastIndex = enemies.Count - 1;
        if (index != lastIndex)
        {
            Enemy movedEnemy = enemies[lastIndex];
            enemies[index] = movedEnemy;
            movedEnemy.rendererIndex = index;
        }

        enemies.RemoveAt(lastIndex);
        ResetTracking(e);
    }

    private static void ResetTracking(Enemy e)
    {
        if (e == null)
            return;

        e.inRenderer = false;
        e.rendererIndex = -1;
        e.emissionValue = 0f;
    }
}
