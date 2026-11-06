using UnityEditor;
using UnityEngine;

// GameObject source setup and baking. Editor-only Undo/asset allocations, no runtime subscriptions.
[CustomEditor(typeof(Level_GrassCover))]
public class Level_GrassCoverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var cover = (Level_GrassCover)target;
        EditorGUILayout.HelpBox("Flat rectangular grass and sparse square-petalled flowers on your drawn shelves. " +
            "Ground colours come from Level Ground Surface. Edit the source GameObjects' size, mesh, material and transform; " +
            "the bake carries them into culled chunks. Save the scene after baking.", MessageType.Info);
        EditorGUILayout.HelpBox("Expand Light / Dark: Grass Width, Grass Height, Tip Width, Roll and Lean vary independently " +
            "for every blade. Tip Width 0.7–0.9 gently narrows the tips; 1–1 keeps the authored source shape. " +
            "The reference preset applies more visible size and tilt variation.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button(new GUIContent("Apply Reference Carpet Settings", "Wider short cards, broad patches, irregular spacing and larger flowers. Keeps palette and source transforms.")))
            {
                Undo.RecordObject(cover, "Reference Grass Carpet");
                cover.ApplyReferenceCarpet();
                EditorUtility.SetDirty(cover);
            }
            DrawDefaultInspector();
            if (!cover.HasSourceCards)
                EditorGUILayout.HelpBox("Assign two separate Grass Card / Flower Card sources, or use Set Up Editable Cards to create missing ones. " +
                    "Automatic baking waits until both are assigned; any existing bake is retained.", MessageType.Info);
            if (GUILayout.Button("Set Up Editable Cards")) SetUp(cover);
            if (cover.Templates != null)
            {
                if (GUILayout.Button(cover.Templates.activeSelf ? "Hide Source Cards" : "Show and Select Source Cards"))
                {
                    Undo.RecordObject(cover.Templates, "Preview Grass Sources");
                    cover.Templates.SetActive(!cover.Templates.activeSelf);
                    if (cover.Templates.activeSelf) Selection.activeGameObject = cover.Templates;
                }
            }
            using (new EditorGUI.DisabledScope(!cover.HasSourceCards))
            {
                if (GUILayout.Button("Rebuild Grass and Flowers"))
                    if (cover.GetComponent<Level_GroundSurface>().Bake()) cover.Bake();
            }
            EditorGUILayout.LabelField("Baked Cards", cover.CardCount.ToString("N0"));
            EditorGUILayout.LabelField("Grass Blades", cover.GrassCount.ToString("N0"));
            EditorGUILayout.LabelField("Flowers", cover.FlowerCount.ToString("N0"));
        }
    }

    public static void SetUp(Level_GrassCover cover)
    {
        const string path = "Assets/Art/Level/GrassCards_Mat.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Goblins/Grass Cards");
            if (shader == null) { Debug.LogWarning("Wait for Grass Cards shader import, then try again."); return; }
            material = new Material(shader) { name = "GrassCards_Mat", enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
        }
        Undo.RecordObject(cover, "Set Up Grass Cards");
        cover.SetUpSources(material);
        if (!cover.GetComponent<Level_GroundSurface>().Bake() || !cover.Bake()) return;
        if (cover.CardCount == 0)
        {
            Debug.Log("No grass cards yet. Draw light/dark shelf outlines and enable their coverage on Level_GrassCover.", cover);
            return;
        }
        // Replacement is reversible: retain the old painted data, disable its slab renderer in this area only.
        foreach (Level_Grass legacy in Object.FindObjectsByType<Level_Grass>(FindObjectsInactive.Include))
        {
            if (!legacy.enabled || legacy.gameObject.scene != cover.gameObject.scene) continue;
            Undo.RecordObject(legacy, "Disable Old Grass");
            legacy.enabled = false;
            EditorUtility.SetDirty(legacy);
        }
    }

    [MenuItem("GameObject/Level/Terrain/Grass Cards and Flowers (Selected Ground)", false, 43)]
    private static void Create(MenuCommand command)
    {
        GameObject selected = command.context as GameObject;
        if (selected == null) selected = Selection.activeGameObject;
        Level_GroundSurface surface = selected != null ? selected.GetComponentInParent<Level_GroundSurface>() : null;
        if (surface == null) { Debug.LogWarning("Select your ground with Level_GroundSurface first."); return; }
        Level_GrassCover cover = surface.GetComponent<Level_GrassCover>();
        if (cover == null) cover = Undo.AddComponent<Level_GrassCover>(surface.gameObject);
        SetUp(cover);
        Selection.activeGameObject = cover.gameObject;
    }
}

[CustomEditor(typeof(Level_GrassCard))]
public class Level_GrassCardEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("A real source mesh. Width/Height change its shape. Use Transform for tilt, roll, scale " +
            "and offset. Changes rebake the cover after you release the control. Hide source previews before judging coverage.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            var card = (Level_GrassCard)target;
            Level_GrassCover cover = card.GetComponentInParent<Level_GrassCover>(true);
            if (cover != null && cover.TryGetSourceShape(card, out Level_GrassCard.Shape role))
            {
                EditorGUILayout.LabelField("Source Role", role.ToString());
                serializedObject.Update();
                DrawPropertiesExcluding(serializedObject, "m_Script", "shape");
                serializedObject.ApplyModifiedProperties();
            }
            else DrawDefaultInspector();
        }
    }
}
