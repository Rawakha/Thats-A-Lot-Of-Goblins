> **Done (2026-11-06).** Step 1 is built, with one change from the user: the current non-Egyptian tools were converted too. See `Level_Tools_Plan.md`.

# Level Tools: handoff brief

Written 2026-11-06 by Claude for ChatGPT/Codex, which is taking over this work. All paths are relative to
`Thats A Lot Of Goblins v2/`. Scripts are in `Assets/Scripts/Level/`.

Read first: `CLAUDE.md`, the top of `Docs/State.md`, and the Tech section of `Docs/Decisions.md`. Line numbers
below are from today and may drift, so search by name.

## Goal

The user wants level-design tools that make low-poly areas very quickly. The style is chunky flat-shaded blocks
with soft pastel colour, like Totally Accurate Battlegrounds and stacked-slab floating islands. Ease of use comes
first. Every tool must share the same foundation and the same gizmos, so learning one teaches the rest. Tools
must be robust, and pieces such as the bricks of a wall must bake into one mesh.

## Scope of this job

**Step 1 only: remove the unwanted tools, then build the foundation.** Do not build Egyptian Wall, Pillar,
Doorway, Rock Formation or any other real tool. The user will come back and direct those afterwards. Stop when
step 1 is finished and report back.

Rules that apply here:
- The user commits. Never commit, push or open PRs, and never offer to. Finish with a summary of changed files.
- Prefer code over scene or prefab edits. Don't edit `.unity` files; list any editor steps for the user.
- Match the conventions in `CLAUDE.md`: `Level_` prefix, an opening comment on each script (what it does, setup,
  GC and subscription cost), tooltips on tunables, and no `Update()`. Level tools stay independent of gameplay.
- Report every new allocation and subscription. Say plainly what you could not verify inside Unity.

## Part A: removals

Checked today: `Assets/Scenes/Sample Area.unity` is the only scene or prefab using `Level_Cliff`, and its saved
`shape:` values found were only 0 (Line) and 1 (Block). No scene or prefab references `Level_IslandOutline` or
`Level_IslandRegistry`. Re-run that check before deleting, because the user has uncommitted scene changes.

### A1. Remove Cliff Ring and Cliff Chunks (keep Cliff Line and Cliff Block working exactly as they are)

- `Level_Cliff.cs`: remove `Ring` and `Chunks` from `Level_CliffShape`. Keep `Line` = 0 and `Block` = 1 in that
  order, because saved scenes store the integer. Remove `BuildChunks`, the `IsChunks` branches, Ring cases,
  Ring-only and Chunks-only fields (node length, turn and lean, and the outer-face option), and related tooltips.
- `Editor/Level_CliffEditor.cs`: remove Chunks handles and buttons, including `DrawAddChunkButton`, plus the
  header comment and help text that mention Ring or Chunks.
- `Editor/Level_Menu.cs`: remove the Cliff Ring and Cliff Chunks menu items.
- Check `Level_CliffDefaults` for saved defaults that refer to removed fields.
- Do not change Line or Block geometry. Their output for a given seed must stay the same, including the random
  sequence from `EdgeRandom`.

### A2. Remove the Island tool completely

Delete, with their `.meta` files: `Level_IslandOutline.cs`, `Level_IslandMesh.cs`, `Level_IslandRegistry.cs`,
`Editor/Level_IslandOutlineEditor.cs`.

Remove the references, keeping non-island behaviour identical:
- `Level_Ground.cs`: the `Island` property, the island branches in `SurfaceSize`, openings, build and keys,
  `BuildIsland`, the watched-island loop, and island-only hole validation.
- `Editor/Level_GroundEditor.cs`: the island help box, the Create / Select button, and the early return.
- `Level_GroundSurface.cs`: the registry fallback in the height lookup (return `fallback`), island key text,
  `CanBakeIsland`, and island footprint use.
- `Level_GrassWear.cs`: the island footprint argument (pass null, or remove it if nothing else uses it).
- `Flow/Flow_System.cs`: the `areaIslands` collection, the `shoreSafe` check, `ValidateIslandRoutes`, and the
  `NavigationBaked` call. Flow baking for normal grounds must be unchanged.
- `Horde/Horde_System.cs`: three places, the `Outside` fall-kill block, the `islandTop` surface height, and the
  `CanWalk` shoreline check. This is a hot path, so remove cleanly and add nothing.
- `Weapons/Weapon_EggLauncher.cs`: update the comment mentioning island edges.

Keep `Level_GroundShapeEditor` (in `Editor/Level_Handles.cs`), because Ground and Hole use it. After removal,
search for dead helpers that only the island used, such as footprint parameters in the grass mesh code, and
remove them only if truly unused. Finish with a case-insensitive search for `island` across `Assets/Scripts`.
`splitIslands` in Destruction and `Horde_GibSet` is unrelated, so leave it.

Docs: mark `Docs/Island_Ground_Plan.md` as abandoned at its top. Add a `Decisions.md` entry saying Island, Cliff
Ring and Cliff Chunks were removed by the user because they looked wrong, superseding the Island terrain and
island outline decisions. Don't restore them.

## Part B: the foundation

Already there, so extend it rather than replace it:

| Script | Job |
|---|---|
| `Level_BakeGroup` | Owns `Editable Sources` and the baked output, edit/bake state, deferred refresh, nesting |
| `Level_BlockBaker` | Combines source meshes into one mesh with a submesh per material |
| `Level_SourcePiece` | Stable IDs that keep manual offsets across regeneration |
| `Level_PathNode`, `Editor/Level_PathHandles` | Open or closed node paths and their gestures |
| `Editor/Level_ToolCreation` | Menu placement, Building layer, parenting, Undo |
| `Editor/Level_Handles` | Handle colours, snapping, the move square |
| `Level_BlockMesh`, `Level_BlockStyle`, `Level_Block` | Chunky low-poly stones and their style |
| `Level_Ruins`, `Level_RuinsLayout` | The only tool currently on this foundation. Must keep working |

Handle language, which every tool must follow: white squares move, green dots add, Ctrl/Cmd-click red dots
delete, yellow cones set height, orange cubes set width, pink cubes set length, blue rings turn.

### B1. Make the baker generic
Rename `Level_BlockBaker` to `Level_MeshBaker`, keeping the file's `.meta` GUID. Replace "stone" wording in names
and errors. `RemoveOutput` calls `Level_Block.ReleaseUnusedMesh`; route that so the baker doesn't depend on blocks
except through a clean hook.

### B2. One generator base
Add a small base class, proposed `Level_Tool : Level_BakeGroup`, so a new tool only supplies its settings, its
pieces and its handles. It should provide: seed, style reference, the `InputKey` fingerprint helper, failed-layout
handling, and piece emission through `Level_SourcePiece.Sync`. Move what is generic out of `Level_Ruins`, and keep
its script GUID and serialized field names so saved objects survive. Keep inheritance shallow and put geometry in
pure layout classes, as `Level_RuinsLayout` does.

### B3. Three shared handle sets, one for each tool kind
- **Piece** (new, proposed `Level_BoxHandles`): a single object with width, depth and height, for pillars or doors.
- **Path** (`Level_PathHandles`): exists. Move its hard-coded limits and the 5 m extend distance into parameters.
- **Outline** (new, proposed `Level_OutlineHandles`): a closed polygon with move, insert and delete. Reuse the
  gestures from `Level_PathHandles` rather than copying them, and reject self-crossing shapes while keeping the
  previous valid one. `Level_SurfaceShape.Valid` already checks this.

All must snap, support Undo, record prefab changes, preview during a drag, and rebuild on release.

### B4. Shared Scene-view feedback
Every tool draws the same footprint outline and a status label: **Editing**, **Baked** or **Needs Bake**, with
piece count. Use `Debug_Gizmos` where it fits. Add a shared inspector block (proposed `Level_ToolInspector`) holding
Edit Sources, Bake, Regenerate, Make Collider and any error text, and have `Level_BakeGroupEditor` use it. The
current failure mode is a console warning that is easy to miss, so also show the last bake error in the inspector.

### B5. Style asset
Add a theme asset, proposed `Level_Palette`, holding the material, named colour bands and shade variation. Tools
pick from it, and the default uses vertex colours on one material (`Goblins/Level`). Reuse `Level_BlockStyle` for
the shape, and don't duplicate materials per tint.

### B6. Robustness fixes in `Level_BakeGroup`
Check each one in the code, then fix it:
1. **Mesh persistence.** Baked meshes are created in memory and saved inside the scene. Under a prefab they can
   be lost. Decide on an approach, such as optional mesh assets under the area's folder, and test it.
2. **Undo.** `Bake` replaces output with `DestroyImmediate`, so it isn't undoable. Make that safe, or state it
   clearly in the UI.
3. **Stale detection.** The `hierarchyChanged` watcher only compares source filter counts, and it runs
   `FindObjectsByType` on every hierarchy change. Check for missed changes and the cost in a big scene.
4. **Forgotten bakes.** Add an optional bake-on-save and an entering-Play warning that names the stale groups.
   Default it to on if that proves safe.
5. **Navigation.** After a successful bake with a collider, show a reminder that Flow needs rebaking. Level tools
   must not reference `Flow_System` directly; the reminder is text only.

### B7. Menus
Move creation into one convention: `GameObject/Level/<Category>/<Tool>`. Ground, Hole, Tree Area, Grass and Cliff
become `Level/Terrain/...`; keep `Level/Common/Bake Group` and `Level/Egyptian/Ruins`. Only list categories with
real tools. `Level_TownBlockout` was only a test, so leave it alone.

### B8. Prove it, and leave a template
Add a tiny example of each kind, built only on the foundation, under `Level/Common/Examples`: a Piece, a Path and
an Outline that emit plain `Level_Block`s. Keep each to about 60 lines of tool code. These are the copy-paste
starting point for future tools, so if one needs special-case code, fix the foundation instead. Then rewrite
`Docs/Level_Tools_Plan.md` as a short "how to add a tool" guide. That file is not valid UTF-8, so save it as UTF-8.

## Do not change
- Specialised grass and ground baking (`Level_GrassCover`, `Level_GroundSurface`, `Level_GrassWear`), beyond the
  island removal. Grass has wind and custom vertex data and keeps its own pipeline.
- Cliff Line and Block geometry. They don't migrate to the foundation in this job.
- Existing Ruins objects, script GUIDs and serialized field names.

## Definition of done
- Runtime, editor and player builds compile with no references to removed code.
- No runtime tick or per-piece subscription added by level tools.
- Ask the user to check in Unity, since this can't be confirmed from outside the editor:
  1. Open `Sample Area`: cliffs look unchanged, with no missing-script warnings.
  2. Bake Obstacles, then play a wave: goblins walk, die and fall normally.
  3. Create each example, use every handle, Undo, bake, save, reopen, enter Play.
  4. Put two different tools in one Bake Group and bake them to one mesh.
  5. Existing Ruins still regenerates and bakes.
- Update `Docs/State.md` (top entry plus handoff log) and `Docs/Decisions.md`.
- Stop and report. Wait for the user before starting any real tool.

## Afterwards (do not start)
Egyptian Wall, Pillar and Doorway as described in `Docs/Decisions.md`; a stacked-slab Rock Formation on the Outline
kind to replace what was removed; theme categories such as Medieval and Modern.
