using UnityEditor;
using UnityEngine;

// The shared look of every Egyptian tool (wall now; pillar and archway later): the sandstone stone shape and a palette
// with the band names they ask for (Main, Trim, Band, Accent). Both assets are made the first time and reused, so all
// Egyptian pieces share one material and bake together. Edit the palette asset to retheme every Egyptian tool at once.
// Setup: call Create from a tool's menu item. GC: editor only, menu clicks allocate. No subscriptions.
public static class Level_EgyptLook
{
    private const string StylePath = "Assets/Art/Level/Ruins_BlockStyle.asset";
    private const string PalettePath = "Assets/Art/Level/Egyptian_Palette.asset";

    public static Level_BlockStyle Style()
    {
        Level_BlockStyle style = AssetDatabase.LoadAssetAtPath<Level_BlockStyle>(StylePath);

        if (style != null)
            return style;

        style = ScriptableObject.CreateInstance<Level_BlockStyle>();
        style.material = Level_BlockEditor.DefaultStyle()?.material;
        style.cornerCut = 0.075f;
        style.cornerVariation = 0.3f;
        style.topTaper = 0.025f;
        style.topLean = 0f;
        style.shadeVariation = 0.045f;
        style.sideDarkening = 0.08f;
        AssetDatabase.CreateAsset(style, StylePath);
        return style;
    }

    public static Level_Palette Palette()
    {
        Level_Palette palette = AssetDatabase.LoadAssetAtPath<Level_Palette>(PalettePath);

        if (palette != null)
        {
            // Palettes made before the houses lack their dark frame and wood colours
            AddMissingBand(palette, "Frame", new Color(0.32f, 0.17f, 0.14f));
            AddMissingBand(palette, "Wood", new Color(0.5f, 0.3f, 0.16f));
            return palette;
        }

        palette = ScriptableObject.CreateInstance<Level_Palette>();
        palette.material = Style() != null ? Style().material : null;
        palette.shadeVariation = 0.05f;
        palette.bands = new[]
        {
            new Level_Palette.Band { name = "Main", colour = new Color(0.82f, 0.65f, 0.4f) },
            new Level_Palette.Band { name = "Trim", colour = new Color(0.92f, 0.76f, 0.49f) },
            new Level_Palette.Band { name = "Band", colour = new Color(0.58f, 0.27f, 0.16f) },
            new Level_Palette.Band { name = "Accent", colour = new Color(0.23f, 0.37f, 0.33f) },
            new Level_Palette.Band { name = "Frame", colour = new Color(0.32f, 0.17f, 0.14f) },
            new Level_Palette.Band { name = "Wood", colour = new Color(0.5f, 0.3f, 0.16f) }
        };
        AssetDatabase.CreateAsset(palette, PalettePath);
        return palette;
    }

    private static void AddMissingBand(Level_Palette palette, string name, Color colour)
    {
        foreach (Level_Palette.Band band in palette.bands)
        {
            if (band.name == name)
                return;
        }

        Undo.RecordObject(palette, "Add Palette Band");
        System.Array.Resize(ref palette.bands, palette.bands.Length + 1);
        palette.bands[palette.bands.Length - 1] = new Level_Palette.Band { name = name, colour = colour };
        EditorUtility.SetDirty(palette);
    }

    // A tool of this type with the Egyptian look, its stones made and shown ready to edit
    public static T Create<T>(string label, MenuCommand command, bool onBuildingLayer = true) where T : Level_Tool
    {
        T tool = Undo.AddComponent<T>(Level_ToolCreation.Create(label, command, onBuildingLayer));
        tool.Configure(Style(), Palette());
        tool.Generate();
        return tool;
    }
}
