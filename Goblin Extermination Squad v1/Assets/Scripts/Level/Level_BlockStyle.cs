using UnityEngine;

// Shared stone appearance for level architecture. Create via Assets > Create > Goblins > Level > Block Style.
// Geometry/colour are baked into real source meshes in edit mode. No runtime work, allocations or subscriptions.
[CreateAssetMenu(menuName = "Goblins/Level/Block Style", fileName = "Block Style")]
public class Level_BlockStyle : ScriptableObject
{
    public Material material;
    public Color colour = new Color(0.62f,0.57f,0.51f);
    [Tooltip("Fraction of the unit stone cut from each corner. Zero is a plain block.")]
    [Range(0,0.35f)] public float cornerCut = 0.16f;
    [Tooltip("Uneven corner cuts give broad, irregular facets while keeping the mesh closed.")]
    [Range(0,1)] public float cornerVariation = 0.45f;
    [Tooltip("How much narrower the stone is at the top.")]
    [Range(0,0.2f)] public float topTaper = 0.025f;
    [Tooltip("Maximum sideways shear at the top, as a share of the unit block width.")]
    [Range(0,0.12f)] public float topLean = 0.025f;
    [Range(0,0.3f)] public float shadeVariation = 0.07f;
    [Range(0,0.4f)] public float sideDarkening = 0.1f;
#if UNITY_EDITOR
    private void OnValidate() => Level_Block.RequestRefresh();
    public string SourceKey() => Hash128.Compute($"{colour:F6}|{cornerCut:R}|{cornerVariation:R}|{topTaper:R}|{topLean:R}|{shadeVariation:R}|{sideDarkening:R}|"+
        UnityEditor.AssetDatabase.GetAssetPath(material)).ToString();
#endif
}
