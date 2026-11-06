using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

// Editable stone source GameObject. Use Transform for size/position/rotation; assign a shared Block Style.
// Standalone blocks render normally; future layout tools can bake the actual mesh/material/transform.
// Runtime: saved mesh only, no ticks or subscriptions. Editor rebuilds allocate mesh working data.
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
public class Level_Block : MonoBehaviour, Level_IBakeSource
{
    [SerializeField] private Level_BlockStyle style;
    [SerializeField] private Level_BlockMesh.Shape shape;
    [SerializeField] private int seed = 1;
    [Tooltip("Override the shared style colour for this stone; used for architectural trim without extra materials.")]
    [SerializeField] private bool overrideColour;
    [SerializeField] private Color colour = Color.white;
    [Tooltip("Set by tools: this theme's material and shade variation replace the style's. Empty uses the style's.")]
    [SerializeField, HideInInspector] private Level_Palette palette;
    [SerializeField, HideInInspector] private Mesh generatedMesh;
    [SerializeField, HideInInspector] private string meshKey;
    public Level_BlockStyle Style => style;

#if UNITY_EDITOR
    // Layout tools can observe source changes without introducing a dependency on any particular tool.
    public static event System.Action<Level_Block> SourceChanged;
    private static bool queued;
    private void OnEnable() { if (!Application.isPlaying) RequestRefresh(); }
    private void OnValidate() => RequestRefresh();
    private void OnDestroy()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        SourceChanged?.Invoke(this);
        Level_MeshBaker.ReleaseUnusedMesh(generatedMesh);
    }
    public static void RequestRefresh()
    {
        if (queued || EditorApplication.isPlayingOrWillChangePlaymode) return;
        queued = true; EditorApplication.delayCall += Refresh;
    }
    private static void Refresh()
    {
        queued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (GUIUtility.hotControl != 0) { RequestRefresh(); return; }
        var invalidStyles=new HashSet<Level_BlockStyle>();
        foreach (var block in FindObjectsByType<Level_Block>(FindObjectsInactive.Include))
        {
            if(block==null||!block.enabled||EditorUtility.IsPersistent(block)||invalidStyles.Contains(block.style))continue;
            try
            {
                block.EnsureMesh();
            }
            catch(System.Exception error)
            {
                invalidStyles.Add(block.style);
                Debug.LogWarning($"{block.name}: retained the previous stone mesh. {error.Message}",block);
            }
        }
    }
    public void Configure(Level_BlockStyle profile, int value, Level_BlockMesh.Shape kind, Color? tint = null, Level_Palette theme = null)
    { style=profile;seed=value;shape=kind;overrideColour=tint.HasValue;colour=tint ?? Color.white;palette=theme;EnsureMesh(); }
    public void PrepareForBake() => EnsureMesh();
    public string BakeKey => meshKey;
    public bool EnsureMesh()
    {
        if (style == null) return false;
        // Keep the original key for existing untinted blocks so this extension preserves custom source meshes.
        string key=style.SourceKey()+"|"+seed+"|"+shape;
        if(overrideColour)key+="|colour:"+colour.ToString("F6");
        if(palette!=null)key+="|palette:"+palette.SourceKey();
        if (meshKey==key && GetComponent<MeshFilter>().sharedMesh!=null) return false;
        Mesh next = Level_BlockMesh.Build(style.cornerCut,style.cornerVariation,style.topTaper,style.topLean,overrideColour ? colour : style.colour,
            palette!=null ? palette.shadeVariation : style.shadeVariation,style.sideDarkening,seed,shape).ToMesh("Low Poly Stone");
        Mesh old=generatedMesh;generatedMesh=next;meshKey=key;
        GetComponent<MeshFilter>().sharedMesh=next;
        GetComponent<MeshRenderer>().sharedMaterial=palette!=null&&palette.material!=null ? palette.material : style.material;
        Level_MeshBaker.ReleaseUnusedMesh(old);
        EditorUtility.SetDirty(this); PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(GetComponent<MeshFilter>());
        PrefabUtility.RecordPrefabInstancePropertyModifications(GetComponent<MeshRenderer>());
        SourceChanged?.Invoke(this);
        return true;
    }
#endif
}
