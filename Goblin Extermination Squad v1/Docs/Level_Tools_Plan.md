# How to add a level tool

Written 2026-11-06. This replaces the earlier planning notes. Scripts are in `Assets/Scripts/Level/`.

## Pick a kind

Copy the example that matches, and its editor in `Editor/Level_ExampleEditors.cs`. Try them under
**GameObject > Level > Common > Examples**.

| Kind | Example | Handles | For |
|---|---|---|---|
| Piece | `Level_ExamplePiece` | `Level_BoxHandles` | One object with a size: pillar, doorway |
| Path | `Level_ExamplePath` | `Level_PathHandles` | A line of nodes: wall, fence |
| Outline | `Level_ExampleOutline` | `Level_OutlineHandles` | A closed shape: rock formation, compound |

## Steps

1. Derive from `Level_Tool`. Add your settings as serialized fields with tooltips.
2. Fill in `BuildPieces`: add a `Level_ToolPiece` for each stone. Only do maths here, and use `seed` for anything random.
   Give each piece an id that stays the same between rebuilds, because hand-moved pieces keep their offsets through it.
3. Derive an editor from `Level_ToolEditor`. Override `HelpText` and `DrawHandles`, and call `Changed()` when a handle edits something.
4. Add a menu item: `GameObject/Level/<Category>/<Tool>` calling `Level_ToolCreation.CreateTool<T>`. Only add a category once it has a real tool.
5. Open the file with the conventions from `CLAUDE.md`: what it does, setup, GC and subscriptions.

If a tool needs special-case code in the foundation to work, fix the foundation rather than working around it.

## Handle language

White squares move, green dots add, Ctrl/Cmd-click red dots delete, yellow cones set height, orange cubes set width,
pink cubes set length, blue rings turn, purple cubes place another tool's object (pillar, archway) on a wall. Colours and sizes live in `Level_Handles`. Don't pick your own.

## What the foundation does for you

- `Level_Tool`: seed, style, palette, spotting setting changes, keeping the last good result when a layout fails, syncing real stones.
- `Level_BakeGroup`: Editable Sources, one-mesh output, the stale check, Undo, prefab-safe mesh assets.
- `Level_ToolInspector` and `Level_ToolFeedback`: status, buttons, errors in the inspector, the Scene view label and footprint.
- `Level_Palette`: a theme's material, named colour bands and shade variation. `Level_BlockStyle` sets the stones' shape.

## Using a bake group

Selecting a group and creating something from the Level menu puts it in the group's sources. Two different tools in one
group bake to one mesh, with a submesh per material. Keep gameplay objects outside the sources. Separate groups let
distant assemblies cull independently, so don't merge a whole map. Save the scene after baking, and rebake navigation if
the collider changed.

The bake rejects transparent materials, property blocks, blend shapes, nonstandard UV0 and the animated grass shaders.

**Stale bakes:** a fingerprint of the sources is saved at bake time and compared whenever something changes. Entering
Play warns with the names of stale groups. **Tools > Goblins > Bake Stale Level Groups On Save** is off by default.

**Undo:** a bake is one Undo step. The exception is a mesh saved as an asset: rebaking overwrites its contents, which
Undo can't bring back. Meshes replaced by a bake stay in memory for Undo until the scene next loads, so Unity may log
that it's cleaning up leaked meshes on save. That's harmless.

**Prefabs:** a mesh under a prefab always saves as an asset in a `Baked Meshes` folder next to the prefab. **Save Mesh As
Asset** does the same for anything else, next to the scene. Asset meshes are not deleted when a group is removed.

## Tools that build their own output

`Level_Cliff`, `Level_TreeArea` and `Level_Ground` derive from `Level_BuiltTool`, which owns their rebuild rules: changes in
one tick make one rebuild, only if the inputs changed, and saved output is trusted after a load. They use the shared
handles, inspector help, status label and creation, but keep their own geometry. A cliff can also go inside a bake group,
through `Level_IBakeSource`.

Grass (`Level_GroundSurface`, `Level_GrassCover`, `Level_GrassWear`, `Level_Grass`) keeps its own bake, because wind and custom
vertex data can't go through the generic one. Its outlines use the shared handles and its menus follow the convention.
