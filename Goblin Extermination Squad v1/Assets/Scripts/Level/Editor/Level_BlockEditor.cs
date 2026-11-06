using UnityEditor;
using UnityEngine;

// Standalone reusable block authoring. Editor-only setup/Undo allocations; no runtime subscriptions.
[CustomEditor(typeof(Level_Block))]
public class Level_BlockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Reusable low-poly stone. Transform controls its placement and dimensions. The shared style controls corner cuts, taper, colour and material. " +
            "Use Transform for placement; style/seed changes regenerate the source mesh and material.",MessageType.Info);
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))DrawDefaultInspector();
    }
    public static Level_BlockStyle DefaultStyle()
    {
        const string stylePath="Assets/Art/Level/Default_BlockStyle.asset",matPath="Assets/Art/Level/Blocks_Mat.mat";
        var style=AssetDatabase.LoadAssetAtPath<Level_BlockStyle>(stylePath);
        if(style!=null)return style;
        Material material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(material==null)
        {
            Shader shader=Shader.Find("Goblins/Level");if(shader==null){Debug.LogWarning("Wait for the level shader to import.");return null;}
            material=new Material(shader){name="Blocks_Mat"};AssetDatabase.CreateAsset(material,matPath);
        }
        style=CreateInstance<Level_BlockStyle>();style.material=material;AssetDatabase.CreateAsset(style,stylePath);return style;
    }
    [MenuItem("GameObject/Level/Common/Low Poly Block",false,46)]
    private static void Create(MenuCommand command)
    {
        var obj=Level_ToolCreation.Create("Low Poly Block",command);
        obj.AddComponent<MeshFilter>();obj.AddComponent<MeshRenderer>();obj.transform.localScale=new Vector3(2,1,1);
        var block=Undo.AddComponent<Level_Block>(obj);block.Configure(DefaultStyle(),1,Level_BlockMesh.Shape.Stone);
    }
}

