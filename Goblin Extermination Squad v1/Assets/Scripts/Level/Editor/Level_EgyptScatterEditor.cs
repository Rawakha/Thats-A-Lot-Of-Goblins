using UnityEditor;
using UnityEngine;

// Handles and menu item for the Egyptian ruin scatter: the shared outline handles (white squares move corners, green dots
// add, Ctrl/Cmd-click deletes). Rebuilds when a drag is released.
// GC: editor only (handles and inspector UI). No subscriptions.
[CustomEditor(typeof(Level_EgyptScatter))]
public class Level_EgyptScatterEditor : Level_ToolEditor
{
    protected override string HelpText => $"{Level_ToolInspector.NodeHelp} A zone can't be dragged into crossing itself. " +
        "Raise Density for more bricks, Patchiness for clumps, change Seed to reshuffle. Move or delete any brick under Editable Sources.";

    protected override void DrawHandles()
    {
        var scatter = (Level_EgyptScatter)target;

        if (Level_OutlineHandles.Draw(scatter, scatter.transform, scatter.Nodes, scatter.Snap))
            Changed();
    }

    // Off the Building layer: scattered bricks are decoration, not walls for the flow field
    [MenuItem("GameObject/Level/Egyptian/Ruin Scatter", false, 50)]
    private static void Create(MenuCommand command) => Level_EgyptLook.Create<Level_EgyptScatter>("Ruin Scatter", command, false);
}
