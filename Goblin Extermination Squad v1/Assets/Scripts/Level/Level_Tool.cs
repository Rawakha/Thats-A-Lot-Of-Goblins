using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using System;
using UnityEditor;
#endif

// One generated stone: what a tool's BuildPieces produces. Id must be unique in the tool and stable between rebuilds,
// because hand-made offsets stay attached to it. Position is the stone's bottom centre in the tool's space, size is
// its width, height and depth, and band names a colour in the tool's palette.
public struct Level_ToolPiece
{
    public string id;
    public string label;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 size;
    public string band;
    public Level_BlockMesh.Shape shape;

    public Level_ToolPiece(string id, Vector3 position, Quaternion rotation, Vector3 size, string band = "Main", string label = null,
        Level_BlockMesh.Shape shape = Level_BlockMesh.Shape.Stone)
    {
        this.id = id;
        this.label = label ?? id;
        this.position = position;
        this.rotation = rotation;
        this.size = size;
        this.band = band;
        this.shape = shape;
    }
}

// The base for tools that generate pieces and bake them to one mesh. A new tool supplies its settings (plain serialized
// fields), its pieces (BuildPieces, which should only do maths) and its handles (a Level_ToolEditor). This class does
// the rest: the seed, style and palette, spotting when settings changed, keeping the last good result when a layout
// fails, and syncing real editable stones so hand-moved pieces stay where you put them. See Level_ExamplePiece.
//
// Setup: derive from this, then add a menu item with Level_ToolCreation.CreateTool. Docs/Level_Tools_Plan.md has the steps.
// GC: nothing at runtime, no ticks or subscriptions. Generating allocates working lists and objects in the editor.
public abstract class Level_Tool : Level_BakeGroup
{
    private const int MaxPieces = 4096;

    [Header("Look")]
    [Tooltip("The stones' shape: corner cuts, taper and lean.")]
    [SerializeField] protected Level_BlockStyle style;
    [Tooltip("The material and colour bands.")]
    [SerializeField] protected Level_Palette palette;
    [Tooltip("Change to reshuffle every stone's shape.")]
    [SerializeField] protected int seed = 1;

    [Header("Editing")]
    [Tooltip("Keep position, rotation and scale edits on generated pieces when the layout regenerates.")]
    [SerializeField] protected bool preserveTransformEdits = true;
    [Tooltip("Handles snap to this grid, metres. 0 is off.")]
    [SerializeField, Min(0f)] protected float snap = 0.5f;

    public float Snap => snap;
    public bool HasLook => style != null && palette != null;

    protected abstract void BuildPieces(List<Level_ToolPiece> pieces);

#if UNITY_EDITOR
    // These don't change the layout; style and palette go through their own keys (saved keys can't hold instance IDs)
    private static readonly string[] NotLayout =
    {
        "makeCollider", "saveMeshAsAsset", "sources", "output", "editing", "needsBake", "layoutKey", "bakedPieces",
        "bakedSourceKey", "bakedMeshAsset", "style", "palette", "snap"
    };

    protected override bool CanGenerate => HasLook;

    public void Configure(Level_BlockStyle newStyle, Level_Palette newPalette)
    {
        Undo.RecordObject(this, "Assign Tool Look");
        style = newStyle;
        palette = newPalette;
        RequestRefresh();
    }

    protected override string InputKey()
    {
        string extra = HasLook ? AssetDatabase.GetAssetPath(style) + style.SourceKey() + palette.SourceKey() : null;
        return Level_BuildKey.Stable(this, extra, NotLayout);
    }

    public override bool Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return false;

        if (!HasLook)
        {
            ReportError("Assign a Style and Palette first.");
            return false;
        }

        try
        {
            // Everything is validated before the sources are touched, so a bad layout keeps the last good one
            List<Level_ToolPiece> pieces = new List<Level_ToolPiece>();
            BuildPieces(pieces);

            if (pieces.Count > MaxPieces)
                throw new InvalidOperationException($"{pieces.Count} pieces is over the limit of {MaxPieces}.");

            if (palette.material == null && style.material == null)
                throw new InvalidOperationException("Assign a material on the Palette first.");

            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Generate {name}");
            Undo.RecordObject(this, $"Generate {name}");
            EnsureSourceFolder("Editable Sources");

            Level_SourcePiece.Sync<Level_SourcePiece, Level_ToolPiece>(this, pieces, piece => piece.id, CreatePiece, ConfigurePiece);

            layoutKey = InputKey();
            ReportError(null);
            SetEditing(true);
            MarkDirty();
            Undo.CollapseUndoOperations(undo);
            return true;
        }
        catch (Exception error)
        {
            ReportError(error.Message);
            Debug.LogWarning($"{name}: the layout couldn't generate, so the last bake is kept. {error.Message}", this);
            return false;
        }
    }

    private Level_SourcePiece CreatePiece(Level_ToolPiece piece)
    {
        GameObject stone = new GameObject(piece.label, typeof(MeshFilter), typeof(MeshRenderer)) { layer = gameObject.layer };
        stone.transform.SetParent(sources.transform, false);
        Undo.RegisterCreatedObjectUndo(stone, "Create Tool Piece");
        Undo.AddComponent<Level_Block>(stone);
        return Undo.AddComponent<Level_SourcePiece>(stone);
    }

    private void ConfigurePiece(Level_ToolPiece piece, Level_SourcePiece source)
    {
        Level_Block block = source.GetComponent<Level_Block>();

        if (block == null)
            block = Undo.AddComponent<Level_Block>(source.gameObject);

        Undo.RecordObject(block, "Style Tool Piece");
        source.Place(piece.id, piece.position, piece.rotation, piece.size, preserveTransformEdits);

        // Stable across runs, unlike string.GetHashCode
        int pieceSeed = seed + Animator.StringToHash(piece.id);
        block.Configure(style, pieceSeed, piece.shape, palette.Colour(piece.band), palette);
    }
#endif
}
