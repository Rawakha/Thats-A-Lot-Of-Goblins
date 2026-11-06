using UnityEditor;
using UnityEngine;

// GameObject > Level > Terrain: the ground and what sits on it. Each is undoable and lands where the Scene view is
// looking, through Level_ToolCreation. Other categories keep their menu items beside their tools' editors.
// GC: only explicit menu clicks allocate. No subscriptions.
public static class Level_Menu
{
    [MenuItem("GameObject/Level/Terrain/Ground", false, 30)]
    private static void CreateGround(MenuCommand command)
    {
        GameObject ground = Level_ToolCreation.Create("Ground", command, false);
        ground.AddComponent<MeshFilter>();
        MeshRenderer groundRenderer = ground.AddComponent<MeshRenderer>();
        ground.transform.position = Level_ToolCreation.ScenePivot();

        // Start with the pipeline's default material so it isn't pink
        Material material = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null
            ? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.defaultMaterial
            : null;

        if (material != null)
            groundRenderer.sharedMaterial = material;

        Undo.AddComponent<Level_Ground>(ground).Build();
    }

    // A hole where the Scene view is looking, at the selected ground's height (or the world's 0), under it if one is
    // selected
    [MenuItem("GameObject/Level/Terrain/Hole", false, 31)]
    private static void CreateHole(MenuCommand command)
    {
        GameObject parent = command.context as GameObject;
        Level_Ground ground = parent != null ? parent.GetComponentInParent<Level_Ground>() : null;

        GameObject hole = new GameObject("Hole");
        Undo.RegisterCreatedObjectUndo(hole, "Create Level Hole");

        if (ground != null)
            hole.transform.SetParent(ground.transform, false);

        hole.transform.position = Level_ToolCreation.ScenePivot(ground != null ? ground.transform.position.y : 0f);

        Undo.AddComponent<Level_Hole>(hole);
        Selection.activeGameObject = hole;
    }

    // A tree area where the Scene view is looking, on the world's floor. Add tree prefabs to it, then shape it.
    [MenuItem("GameObject/Level/Terrain/Tree Area", false, 40)]
    private static void CreateTreeArea(MenuCommand command)
    {
        GameObject area = Level_ToolCreation.Create("Tree Area", command, false);
        area.transform.position = Level_ToolCreation.ScenePivot();
        Undo.AddComponent<Level_TreeArea>(area);
    }

    // A grass painter where the Scene view is looking. Assign a material on Goblins/Grass, then press Paint Grass.
    [MenuItem("GameObject/Level/Terrain/Grass", false, 41)]
    private static void CreateGrass(MenuCommand command)
    {
        GameObject grass = Level_ToolCreation.Create("Grass", command, false);
        Level_Grass component = Undo.AddComponent<Level_Grass>(grass);

        // Its material: the shared purple one, made the first time
        Material material = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
        Shader shader = Shader.Find("Goblins/Grass");

        if (material == null && shader != null)
        {
            material = new Material(shader) { name = "Grass_Mat", enableInstancing = true };
            AssetDatabase.CreateAsset(material, GrassMaterialPath);
        }

        SerializedObject serialized = new SerializedObject(component);
        serialized.FindProperty("material").objectReferenceValue = material;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        component.Reload();
    }

    private const string GrassMaterialPath = "Assets/Art/Level/Grass/Grass_Mat.mat";

    private const string CliffMaterialPath = "Assets/Art/Level/Cliff_Mat.mat";

    [MenuItem("GameObject/Level/Terrain/Cliff Line", false, 32)]
    private static void CreateCliffLine(MenuCommand command) => CreateCliff(command, Level_CliffShape.Line, "Cliff Line");

    [MenuItem("GameObject/Level/Terrain/Cliff Block", false, 33)]
    private static void CreateCliffBlock(MenuCommand command) => CreateCliff(command, Level_CliffShape.Block, "Cliff Block");

    // On the Building layer (which the flow field bakes as an obstacle), static so it batches, with the shared cliff
    // material (made the first time). Selecting a bake group first puts the cliff in its sources.
    private static void CreateCliff(MenuCommand command, Level_CliffShape shape, string label)
    {
        GameObject cliff = Level_ToolCreation.Create(label, command);
        cliff.AddComponent<MeshFilter>();
        MeshRenderer cliffRenderer = cliff.AddComponent<MeshRenderer>();
        GameObjectUtility.SetStaticEditorFlags(cliff, StaticEditorFlags.BatchingStatic);
        cliffRenderer.sharedMaterial = CliffMaterial();

        Level_Cliff component = Undo.AddComponent<Level_Cliff>(cliff);
        component.Setup(shape);
        Level_CliffDefaults.ApplySaved(component);
        component.Build();
    }

    private static Material CliffMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(CliffMaterialPath);

        if (material != null)
            return material;

        Shader shader = Shader.Find("Goblins/Level");

        if (shader == null)
        {
            Debug.LogWarning("Level_Menu: the Goblins/Level shader is missing, so the cliff has no material.");
            return null;
        }

        material = new Material(shader) { name = "Cliff_Mat" };
        AssetDatabase.CreateAsset(material, CliffMaterialPath);
        return material;
    }
}
