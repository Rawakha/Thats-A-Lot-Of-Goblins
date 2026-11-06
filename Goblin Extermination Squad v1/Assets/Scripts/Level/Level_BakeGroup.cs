using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public enum Level_BakeStatus { Editing, Baked, NeedsBake }

// A group of static pieces that bakes into one mesh. Put pieces (or other tools) in its Editable Sources folder, press
// Bake, save the scene. Sources are EditorOnly, so builds only contain the saved output. Tools that generate their
// pieces derive from Level_Tool; this class owns the sources, the output and whether the bake is stale.
//
// A bake is one Undo step and keeps the last good output if anything fails, showing the error in the inspector.
// Stale bakes are found by comparing a fingerprint of the sources with the one saved at bake time: it's checked once
// per editor tick after something changes, and every group is checked before entering Play.
//
// Meshes normally save inside the scene. Under a prefab they can't, so they go to an asset in a Baked Meshes folder
// next to the scene or prefab (Save Mesh As Asset forces this anywhere).
//
// Setup: GameObject > Level > Common > Bake Group. GC: nothing at runtime, no ticks and no runtime subscriptions.
// Editor: one static set of groups, five shared editor callbacks (Undo x2, hierarchy, play mode, scene saving), and a
// fingerprint string per group per check.
[ExecuteAlways, DisallowMultipleComponent]
public class Level_BakeGroup : MonoBehaviour
{
    [Tooltip("Give the baked mesh a matching MeshCollider. Rebake navigation afterwards if it blocks goblins.")]
    [SerializeField] private bool makeCollider = true;
    [Tooltip("Save the baked mesh as an asset instead of inside the scene. Always on under a prefab. " +
             "Rebaking overwrites the asset, so Undo can't bring back its old contents.")]
    [SerializeField] private bool saveMeshAsAsset;

    [SerializeField, HideInInspector] protected GameObject sources, output;
    [SerializeField, HideInInspector] private bool editing, needsBake;
    [SerializeField, HideInInspector] protected string layoutKey;
    [SerializeField, HideInInspector] private int bakedPieces;
    [SerializeField, HideInInspector] private string bakedSourceKey;
    [SerializeField, HideInInspector] private Mesh bakedMeshAsset;

    public bool Editing => editing;
    public bool NeedsBake => needsBake;
    public bool HasBake => output != null;
    public int BakedPieces => bakedPieces;
    public GameObject Sources => sources;
    public GameObject Output => output;

    public Level_BakeStatus Status => needsBake || output == null ? Level_BakeStatus.NeedsBake
        : editing ? Level_BakeStatus.Editing : Level_BakeStatus.Baked;

    protected void OnEnable()
    {
        if (Application.isPlaying)
        {
            if (output != null) output.SetActive(true);
            if (sources != null) sources.SetActive(false);
            if (output == null) Debug.LogWarning("Bake this level group and save the scene before playing.", this);
        }
#if UNITY_EDITOR
        else
        {
            all.Add(this);
            RequestRefresh();
            QueueStaleCheck();
        }
#endif
    }

#if UNITY_EDITOR
    private static readonly HashSet<Level_BakeGroup> all = new HashSet<Level_BakeGroup>();
    private static readonly HashSet<Level_BakeGroup> pending = new HashSet<Level_BakeGroup>();
    private static bool queued, staleCheckQueued;
    private const string BakeOnSaveKey = "Goblins.Level.BakeOnSave";

    [NonSerialized] private string failedLayout;

    public string LastError { get; private set; }
    public string SourceProblem { get; private set; }
    public bool NavigationReminder { get; private set; }

    // Off by default: baking while Unity saves can't be tried from outside the editor, so it stays opt-in until it has been.
    public static bool BakeOnSave
    {
        get => EditorPrefs.GetBool(BakeOnSaveKey, false);
        set => EditorPrefs.SetBool(BakeOnSaveKey, value);
    }

    protected void OnDisable() => all.Remove(this);
    protected void OnValidate() => RequestRefresh();

    public void MarkDirty()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var parent = transform.parent != null ? transform.parent.GetComponentInParent<Level_BakeGroup>(true) : null;
        if (parent != null && parent.sources != null && transform.IsChildOf(parent.sources.transform)) parent.MarkDirty();
        SetNeedsBake(true);
        QueueStaleCheck();
    }

    private void SetNeedsBake(bool value)
    {
        if (needsBake == value) return;
        needsBake = value;
        EditorUtility.SetDirty(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }

    public void RequestRefresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        pending.Add(this);
        if (queued) return;
        queued = true;
        EditorApplication.delayCall += Refresh;
    }

    public static void RequestRefreshAll()
    {
        foreach (Level_BakeGroup group in all)
            group.RequestRefresh();
    }

    private static void Refresh()
    {
        queued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode) { pending.Clear(); return; }
        if (GUIUtility.hotControl != 0) { queued = true; EditorApplication.delayCall += Refresh; return; }

        var batch = new Level_BakeGroup[pending.Count];
        pending.CopyTo(batch);
        pending.Clear();

        foreach (Level_BakeGroup group in batch)
        {
            if (group == null || !group.isActiveAndEnabled || EditorUtility.IsPersistent(group) || !group.CanGenerate) continue;
            string key = group.InputKey();
            if ((group.sources != null && group.layoutKey == key) || group.failedLayout == key) continue;

            if (group.Generate())
            {
                group.LastError = null;
            }
            else
            {
                group.failedLayout = key;
                group.ReportError("The layout couldn't generate; the last bake is kept. See the Console.");
                group.MarkDirty();
            }
        }

        QueueStaleCheck();
    }

    protected virtual bool CanGenerate => true;
    protected virtual string InputKey() => "static";

    public virtual bool Generate()
    {
        EnsureSourceFolder("Editable Sources");
        layoutKey = InputKey();
        SetEditing(true);
        MarkDirty();
        return true;
    }

    // Keeps the first error of a failure: Level_Tool's specific message rather than Refresh's general one
    protected void ReportError(string message)
    {
        if (string.IsNullOrEmpty(LastError) || message == null)
            LastError = message;
    }

    protected void EnsureSourceFolder(string label)
    {
        if (sources != null) return;
        Undo.RecordObject(this, "Create Editable Sources");
        sources = new GameObject(label) { layer = gameObject.layer, tag = "EditorOnly" };
        sources.transform.SetParent(transform, false);
        Undo.RegisterCreatedObjectUndo(sources, "Create Editable Sources");
    }

    private static void CheckOwnedComponents(Transform item, Level_BakeGroup nested)
    {
        foreach (var component in item.GetComponents<Component>())
        {
            if (component == nested && nested != null) continue;
            if (component is Transform || component is Level_IBakeSource || component is Level_SourcePiece) continue;
            if (nested == null && (component is MeshRenderer || component is MeshFilter)) continue;
            if (component is Collider collider && !collider.isTrigger) continue;
            throw new InvalidOperationException(item.name + ": move gameplay components outside Editable Sources.");
        }
    }

    // generate: regenerate out-of-date layouts first (baking). Off just reads what's there (the stale check).
    internal void CollectSources(List<MeshFilter> filters, bool generate = true)
    {
        if (generate && (sources == null || layoutKey != InputKey()) && (!CanGenerate || !Generate()))
            throw new InvalidOperationException(name + ": fix the source layout before baking.");
        if (sources == null) return;
        if (sources.transform.parent != transform || sources == output || !sources.CompareTag("EditorOnly"))
            throw new InvalidOperationException(name + ": invalid editable source folder.");
        if (sources.GetComponents<Component>().Length != 1)
            throw new InvalidOperationException("Editable Sources is a container. Put mesh components on its children.");
        foreach (Transform child in sources.transform) Collect(child, filters, true, generate);
    }

    private static void Collect(Transform item, List<MeshFilter> filters, bool visible, bool generate)
    {
        visible &= item.gameObject.activeSelf;
        var group = item.GetComponent<Level_BakeGroup>();
        CheckOwnedComponents(item, group);

        if (group != null)
        {
            foreach (Transform child in item)
            {
                if (child.gameObject != group.sources && child.gameObject != group.output)
                    throw new InvalidOperationException(item.name + ": move extra children into its Editable Sources or outside this group.");
            }

            if (visible && group.enabled) group.CollectSources(filters, generate);
            return;
        }

        if (visible && item.TryGetComponent(out MeshFilter filter) &&
            item.TryGetComponent(out MeshRenderer meshRenderer) && meshRenderer.enabled)
        {
            if (meshRenderer.HasPropertyBlock())
                throw new InvalidOperationException(item.name + ": material property overrides require a tool-specific bake.");

            foreach (var material in meshRenderer.sharedMaterials)
            {
                if (material == null) continue;
                if (material.renderQueue >= 3000) throw new InvalidOperationException(item.name + ": transparent meshes need independent sorting.");
                if (material.shader.name == "Goblins/Grass Cards" || material.shader.name == "Goblins/Grass")
                    throw new InvalidOperationException(item.name + ": grass deformation needs the grass tool's bake.");
            }

            var mesh = filter.sharedMesh;

            if (mesh != null)
            {
                if (mesh.blendShapeCount != 0)
                    throw new InvalidOperationException(item.name + ": only static geometry is supported.");
                if (mesh.HasVertexAttribute(VertexAttribute.TexCoord0) && mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord0) > 2)
                    throw new InvalidOperationException(item.name + ": UV0 must be a standard texture coordinate.");
            }

            filters.Add(filter);
        }

        foreach (Transform child in item) Collect(child, filters, visible, generate);
    }

    // keepEditing: leave the sources showing (the bake on save, so saving doesn't hide what you're working on)
    public bool Bake(bool keepEditing = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;

        LastError = null;

        if ((sources == null || layoutKey != InputKey()) && !Generate())
        {
            ReportError("The layout couldn't generate; the last bake is kept. See the Console.");
            return false;
        }

        Undo.FlushUndoRecordObjects();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Bake Level Group");
        GameObject next = null;

        try
        {
            if (output != null && PrefabUtility.IsPartOfPrefabInstance(output) && !PrefabUtility.IsAddedGameObjectOverride(output))
                throw new InvalidOperationException("The last bake belongs to the prefab. Open the prefab to rebake it.");

            var filters = new List<MeshFilter>();
            CollectSources(filters);
            next = Level_MeshBaker.Bake(transform, filters, makeCollider, out int count);
            Mesh asset = StoreMesh(next);
            Undo.RegisterCreatedObjectUndo(next, "Bake Level Group");

            Undo.RecordObject(this, "Bake Level Group");
            GameObject previous = output;
            output = next;
            bakedPieces = count;
            needsBake = false;
            bakedMeshAsset = asset;
            bakedSourceKey = SourceKey();
            SetEditing(keepEditing);

            // Destroyed through Undo with its mesh left alone, so undoing the bake brings back the old one
            if (previous != null) Undo.DestroyObjectImmediate(previous);

            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            if (!EditorSceneManager.IsPreviewScene(gameObject.scene)) EditorSceneManager.MarkSceneDirty(gameObject.scene);

            NavigationReminder = makeCollider;
            if (makeCollider) Debug.Log($"{name}: baked. Its collider changed, so bake the area's navigation again if goblins walk here.", this);
            return true;
        }
        catch (Exception error)
        {
            if (next != null && next != output) DestroyImmediate(next);
            ReportError(error.Message);
            Debug.LogWarning($"{name}: the bake failed and the last bake is kept. {error.Message}", this);
            return false;
        }
    }

    // In-memory meshes can't be saved in a prefab, so anything prefab-related gets an asset
    private Mesh StoreMesh(GameObject baked)
    {
        PrefabStage stage = PrefabStageUtility.GetPrefabStage(gameObject);

        if (!saveMeshAsAsset && stage == null && !PrefabUtility.IsPartOfAnyPrefab(gameObject))
            return null;

        string owner = stage != null ? stage.assetPath
            : PrefabUtility.IsPartOfAnyPrefab(gameObject) ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject)
            : gameObject.scene.path;
        string folder = string.IsNullOrEmpty(owner) ? "Assets/Art/Level" : Path.GetDirectoryName(owner).Replace('\\', '/');

        // A duplicated group still points at the original's asset: it needs its own
        Mesh reuse = bakedMeshAsset;

        foreach (Level_BakeGroup other in all)
        {
            if (other != this && other.bakedMeshAsset == reuse)
                reuse = null;
        }

        return Level_MeshBaker.SaveAsAsset(baked, reuse, folder + "/Baked Meshes", name);
    }

    public void SetEditing(bool value)
    {
        Undo.RecordObject(this, "Preview Editable Sources");
        editing = value;

        if (sources != null)
        {
            Undo.RecordObject(sources, "Preview Editable Sources");
            sources.SetActive(value);
        }

        if (output != null)
        {
            Undo.RecordObject(output, "Preview Editable Sources");
            output.SetActive(!value);
        }

        EditorUtility.SetDirty(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }

    #region Stale detection
    // What the bake would be made from. Meshes are compared through their tool's key or their asset path and size,
    // never instance IDs, because the key is saved and has to survive a restart.
    private string SourceKey()
    {
        var filters = new List<MeshFilter>();
        CollectSources(filters, false);
        var key = new StringBuilder(makeCollider ? "collider" : "none");
        Matrix4x4 toOwner = transform.worldToLocalMatrix;

        foreach (MeshFilter filter in filters)
        {
            filter.TryGetComponent(out Level_IBakeSource source);

            if (source is Behaviour behaviour && !behaviour.enabled)
                continue;

            key.Append('|').Append((toOwner * filter.transform.localToWorldMatrix).ToString("F4"));

            foreach (Material material in filter.GetComponent<MeshRenderer>().sharedMaterials)
                key.Append(',').Append(material != null ? AssetDatabase.GetAssetPath(material) + material.name : "none");

            if (source != null)
            {
                key.Append(',').Append(source.BakeKey);
            }
            else if (filter.sharedMesh != null)
            {
                Mesh mesh = filter.sharedMesh;
                key.Append(',').Append(AssetDatabase.GetAssetPath(mesh)).Append(mesh.name).Append(mesh.vertexCount)
                    .Append(mesh.bounds.ToString("F4"));
            }
        }

        return Hash128.Compute(key.ToString()).ToString();
    }

    private void CheckStale()
    {
        if (this == null || !isActiveAndEnabled || EditorUtility.IsPersistent(this))
            return;

        if (sources == null)
        {
            SetNeedsBake(output == null);
            return;
        }

        string key;

        try
        {
            key = SourceKey();
            SourceProblem = null;
        }
        catch (Exception error)
        {
            SourceProblem = error.Message;
            SetNeedsBake(true);
            return;
        }

        // Saved before fingerprints existed: trust the saved state, without dirtying the scene just for looking
        if (string.IsNullOrEmpty(bakedSourceKey))
        {
            if (output != null && !needsBake) bakedSourceKey = key;
            return;
        }

        SetNeedsBake(output == null || key != bakedSourceKey);

        if (needsBake)
            NavigationReminder = false;
    }

    private static void QueueStaleCheck()
    {
        if (staleCheckQueued || all.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode) return;
        staleCheckQueued = true;
        EditorApplication.delayCall += RunStaleCheck;
    }

    private static void RunStaleCheck()
    {
        staleCheckQueued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (GUIUtility.hotControl != 0) { QueueStaleCheck(); return; }
        CheckAll();
    }

    private static void CheckAll()
    {
        var batch = new Level_BakeGroup[all.Count];
        all.CopyTo(batch);

        foreach (Level_BakeGroup group in batch)
        {
            if (group != null) group.CheckStale();
        }
    }

    // True for groups inside another group's sources: the outer bake includes them, so only outer ones count
    private bool IsNested()
    {
        var parent = transform.parent != null ? transform.parent.GetComponentInParent<Level_BakeGroup>(true) : null;
        return parent != null && parent.sources != null && transform.IsChildOf(parent.sources.transform);
    }

    private static void WarnBeforePlay()
    {
        CheckAll();
        var stale = new StringBuilder();
        Level_BakeGroup first = null;

        foreach (Level_BakeGroup group in all)
        {
            if (group == null || !group.needsBake || group.IsNested()) continue;
            if (first == null) first = group;
            stale.Append(stale.Length > 0 ? ", " : "").Append(group.name);
        }

        if (first != null)
            Debug.LogWarning($"These level groups changed since their last bake, so Play shows the old bake: {stale}.", first);
    }

    private static void BakeBeforeSave(UnityEngine.SceneManagement.Scene scene)
    {
        if (!BakeOnSave) return;
        CheckAll();
        var batch = new Level_BakeGroup[all.Count];
        all.CopyTo(batch);

        foreach (Level_BakeGroup group in batch)
        {
            if (group != null && group.needsBake && group.gameObject.scene == scene && !group.IsNested())
                group.Bake(group.editing);
        }
    }

    // Bakes every group in the open scenes that isn't baked yet: stale, never baked, or showing its sources. Groups inside
    // another group's sources are left to the outer one. A failure doesn't stop the rest; the summary says how many.
    public static void BakeAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        CheckAll();
        var batch = new List<Level_BakeGroup>();

        foreach (Level_BakeGroup group in all)
        {
            if (group != null && group.isActiveAndEnabled && !group.IsNested() && (group.needsBake || group.output == null || group.editing))
                batch.Add(group);
        }

        int baked = 0, failed = 0;

        try
        {
            for (int i = 0; i < batch.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Baking level groups", batch[i].name, (float)i / batch.Count);

                if (batch[i].Bake()) baked++;
                else failed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string message = batch.Count == 0
            ? "Every level group in the open scenes is already baked."
            : $"Baked {baked} level group(s){(failed > 0 ? $", {failed} failed (see the Console and each group's inspector)" : "")}. Save the scene, and rebake navigation if goblins walk here.";

        if (failed > 0) Debug.LogWarning(message);
        else Debug.Log(message);
    }

    [InitializeOnLoadMethod]
    private static void Watch()
    {
        Level_Block.SourceChanged += block =>
        {
            if (block == null) return;
            var group = block.GetComponentInParent<Level_BakeGroup>(true);
            if (group != null) { group.MarkDirty(); group.RequestRefresh(); }
        };
        Undo.undoRedoPerformed += () =>
        {
            RequestRefreshAll();
            QueueStaleCheck();
            Level_Block.RequestRefresh();
        };
        Undo.postprocessModifications += changes =>
        {
            foreach (var change in changes)
            {
                UnityEngine.Object target = change.currentValue.target;
                GameObject obj = target as GameObject;
                if (obj == null && target is Component component) obj = component.gameObject;
                if (obj == null) continue;
                var group = obj.GetComponentInParent<Level_BakeGroup>(true);
                if (group == null) continue;
                if (target == group) group.RequestRefresh();
                QueueStaleCheck();
            }
            return changes;
        };
        EditorApplication.hierarchyChanged += QueueStaleCheck;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) WarnBeforePlay();
        };
        EditorSceneManager.sceneSaving += (scene, path) => BakeBeforeSave(scene);
    }
    #endregion

    protected void OnDestroy()
    {
        all.Remove(this);
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject previous = output;
        GameObject retained = sources;
        EditorApplication.delayCall += () =>
        {
            if (this != null) return;
            if (previous != null && previous.scene.IsValid() && previous.scene.isLoaded) Level_MeshBaker.RemoveOutput(previous);
            if (retained != null && retained.scene.IsValid() && retained.scene.isLoaded)
            {
                retained.tag = "Untagged";
                retained.SetActive(true);
            }
        };
    }
#endif
}
