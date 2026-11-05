using UnityEditor;
using UnityEngine;

// GameObject > Level: quick ways to drop level pieces in. Each is undoable and lands where the Scene view is
// looking.
public static class Level_Menu
{
    [MenuItem("GameObject/Level/Ground", false, 30)]
    private static void CreateGround(MenuCommand command)
    {
        GameObject ground = new GameObject("Ground", typeof(MeshFilter), typeof(MeshRenderer));
        GameObjectUtility.SetParentAndAlign(ground, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(ground, "Create Level Ground");

        SceneView view = SceneView.lastActiveSceneView;
        Vector3 position = view != null ? view.pivot : Vector3.zero;
        position.y = 0f;
        ground.transform.position = position;

        // Start with the pipeline's default material so it isn't pink
        Material material = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null
            ? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.defaultMaterial
            : null;

        if (material != null)
            ground.GetComponent<MeshRenderer>().sharedMaterial = material;

        Undo.AddComponent<Level_Ground>(ground).Build();
        Selection.activeGameObject = ground;
    }

    // A hole where the Scene view is looking, at the selected ground's height (or the world's 0), under it if one is
    // selected
    [MenuItem("GameObject/Level/Hole", false, 31)]
    private static void CreateHole(MenuCommand command)
    {
        GameObject parent = command.context as GameObject;
        Level_Ground ground = parent != null ? parent.GetComponentInParent<Level_Ground>() : null;

        GameObject hole = new GameObject("Hole");
        Undo.RegisterCreatedObjectUndo(hole, "Create Level Hole");

        if (ground != null)
            hole.transform.SetParent(ground.transform, false);

        SceneView view = SceneView.lastActiveSceneView;
        Vector3 position = view != null ? view.pivot : Vector3.zero;
        position.y = ground != null ? ground.transform.position.y : 0f;
        hole.transform.position = position;

        Undo.AddComponent<Level_Hole>(hole);
        Selection.activeGameObject = hole;
    }

    // A tree area where the Scene view is looking, on the world's floor. Add tree prefabs to it, then shape it.
    [MenuItem("GameObject/Level/Tree Area", false, 40)]
    private static void CreateTreeArea(MenuCommand command)
    {
        GameObject area = new GameObject("Tree Area");
        GameObjectUtility.SetParentAndAlign(area, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(area, "Create Tree Area");

        SceneView view = SceneView.lastActiveSceneView;
        Vector3 position = view != null ? view.pivot : Vector3.zero;
        position.y = 0f;
        area.transform.position = position;

        Undo.AddComponent<Level_TreeArea>(area);
        Selection.activeGameObject = area;
    }

    // A grass painter where the Scene view is looking. Assign a material on Goblins/Grass, then press Paint Grass.
    [MenuItem("GameObject/Level/Grass", false, 41)]
    private static void CreateGrass(MenuCommand command)
    {
        GameObject grass = new GameObject("Grass");
        GameObjectUtility.SetParentAndAlign(grass, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(grass, "Create Grass");
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

        Selection.activeGameObject = grass;
    }

    private const string GrassMaterialPath = "Assets/Art/Level/Grass/Grass_Mat.mat";

    private const string CliffMaterialPath = "Assets/Art/Level/Cliff_Mat.mat";

    [MenuItem("GameObject/Level/Cliff Line", false, 32)]
    private static void CreateCliffLine(MenuCommand command) => CreateCliff(command, Level_CliffShape.Line, "Cliff Line");

    [MenuItem("GameObject/Level/Cliff Block", false, 33)]
    private static void CreateCliffBlock(MenuCommand command) => CreateCliff(command, Level_CliffShape.Block, "Cliff Block");

    [MenuItem("GameObject/Level/Cliff Ring (Map Edge)", false, 34)]
    private static void CreateCliffRing(MenuCommand command) => CreateCliff(command, Level_CliffShape.Ring, "Cliff Ring");

    [MenuItem("GameObject/Level/Cliff Chunks", false, 35)]
    private static void CreateCliffChunks(MenuCommand command) => CreateCliff(command, Level_CliffShape.Chunks, "Cliff Chunks");

    // A cliff where the Scene view is looking, on the world's floor, on the Building layer (which the flow field
    // bakes as an obstacle), static so it batches, with the shared cliff material (made the first time)
    private static void CreateCliff(MenuCommand command, Level_CliffShape shape, string label)
    {
        GameObject cliff = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        GameObjectUtility.SetParentAndAlign(cliff, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(cliff, $"Create {label}");

        SceneView view = SceneView.lastActiveSceneView;
        Vector3 position = view != null ? view.pivot : Vector3.zero;
        position.y = 0f;
        cliff.transform.position = position;

        int layer = LayerMask.NameToLayer("Building");

        if (layer >= 0)
            cliff.layer = layer;

        GameObjectUtility.SetStaticEditorFlags(cliff, StaticEditorFlags.BatchingStatic);
        cliff.GetComponent<MeshRenderer>().sharedMaterial = CliffMaterial();

        Level_Cliff component = Undo.AddComponent<Level_Cliff>(cliff);
        component.Setup(shape);
        Level_CliffDefaults.ApplySaved(component);
        component.Build();
        Selection.activeGameObject = cliff;
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
