using System;
using UnityEngine;

// A theme for level tools: one material, a few named colour bands, and how much each piece's shade varies. Tools pick
// bands by name, and the colour goes into the piece's vertex colours, so a whole theme shares one material rather than
// one material per tint. Level_BlockStyle still sets the stones' shape. Duplicate this asset to make a new theme.
//
// Setup: Assets > Create > Goblins > Level > Palette, or let a tool's Assign Defaults button make the default one.
// GC: none at runtime; generated meshes are made in the editor. No ticks or subscriptions.
[CreateAssetMenu(menuName = "Goblins/Level/Palette", fileName = "Palette")]
public class Level_Palette : ScriptableObject
{
    [Serializable]
    public struct Band
    {
        [Tooltip("The name tools ask for, like Main or Trim.")]
        public string name;
        public Color colour;
    }

    [Tooltip("Shared by every piece. Use a material on Goblins/Level, which shows vertex colours.")]
    public Material material;

    [Tooltip("The colours tools pick from. A name that isn't here gets the first band.")]
    public Band[] bands =
    {
        new Band { name = "Main", colour = new Color(0.62f, 0.57f, 0.51f) },
        new Band { name = "Trim", colour = new Color(0.78f, 0.72f, 0.62f) },
        new Band { name = "Accent", colour = new Color(0.45f, 0.62f, 0.62f) }
    };

    [Tooltip("How much each piece's shade varies, lighter or darker.")]
    [Range(0f, 0.3f)] public float shadeVariation = 0.07f;

    public Color Colour(string band)
    {
        if (bands == null || bands.Length == 0)
            return Color.white;

        for (int i = 0; i < bands.Length; i++)
        {
            if (bands[i].name == band)
                return bands[i].colour;
        }

        return bands[0].colour;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Level_Block.RequestRefresh();
        Level_BakeGroup.RequestRefreshAll();
    }

    public string SourceKey()
    {
        System.Text.StringBuilder key = new System.Text.StringBuilder(UnityEditor.AssetDatabase.GetAssetPath(material));
        key.Append('|').Append(shadeVariation.ToString("R"));

        if (bands != null)
        {
            foreach (Band band in bands)
                key.Append('|').Append(band.name).Append(band.colour.ToString("F6"));
        }

        return Hash128.Compute(key.ToString()).ToString();
    }
#endif
}
