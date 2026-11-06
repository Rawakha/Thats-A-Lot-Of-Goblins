# Agents (Codex and others)

This repo holds two Unity projects:
- **`Goblin Extermination Squad/`**: the game being built now (the plane version). All new work happens here.

This project is mainly built with Claude; you're picking up where it left off. Everything you need is in the v2 folder:

1. **`Goblin Extermination Squad/CLAUDE.md`**: the project brief, code conventions and working rules. They apply to you exactly as written (the user commits, one module at a time, spec before code, `UpdateManager` instead of `Update()`, GC tracking, `System_ScriptName` naming).
2. **`Goblin Extermination Squad/Docs/State.md`**: what's built, the current module and how far it's got, the queue, and editor setup still to do. Start from "Current module".
3. **`Goblin Extermination Squad/Docs/Decisions.md`**: choices already settled. Don't undo them without asking the user.
4. **`Goblin Extermination Squad/Docs/Ideas.md`**: unbuilt ideas (don't build from it without asking). `Docs/Marketing.md` is posting notes, only for marketing tasks.

The root `CLAUDE.md` describes v1 (Goblin TD); ignore it for v2 work.

## GameObject authoring workflow

Ground uses its original mesh and grid material. `Level_GroundSurface` owns two separate flat shelf meshes,
with dark below light. Author both types explicitly as child `Level_GrassArea` GameObjects using cliff-style
outline nodes. Same-type patches merge, hole openings remain open, and source GameObjects stay editable.
`Level_GrassArea` replaces `Level_DarkGrassArea` with preserved script GUID and migration metadata.
`Level_GroundSurface.HeightAt` uses saved band data for exact shelf heights without raycasts.
`GrassSurfaceColour.hlsl` provides shared world-space colour variation for surfaces and matching grass cards.
`Level_GrassCover` bakes simple rectangular billboards and sparse square-petalled flowers onto exposed shelves.
Keep its editable `Level_GrassCard` GameObjects (real meshes/materials/transforms); output is saved spatial mesh
chunks with normal Unity renderer culling and shader wind/billboarding. Per-ground controls set density, size,
tip tint and flower colours; shelf colours remain on `Level_GroundSurface`. Expanded cover controls include independent
width/height ranges, broad coverage/height patches, jitter, roll/lean, separate flower sizes/patch scale, colour gradients,
alternate tips/flowers, facing modes and wind direction/flutter. Source cards support flat/tapered/slanted tips and
cross/square/diamond flower heads. Apply Reference Carpet Settings is optional and preserves palette/source transforms.
Per-layer Tip Width varies taper independently for each blade (0.7–0.9 by default, 1–1 preserves source tips),
around the authored tip centre with fixed roots. Size/tilt/taper are baked; grass stays one quad per blade.
Cover slots enforce Grass/Flower source roles without resetting dimensions or transforms. Assigned source inspectors
show the role read-only; a saved Flower Card accidentally set to Grass repairs itself on editor refresh/bake.
`Level_IslandOutline` is now the opt-in island tool: flat cap at Ground height, editable closed outline,
downward stepped cliff sides and a separate flat future water-bed plane. Ground retains its top/grid material.
Saved footprint/cap bands drive clipping and height queries; the bed is the lower floor outside the island and
through open holes. `Level_IslandRegistry` uses the shared swap-back manager, with no frame subscription.
Walking navigation masks the shoreline and rejects disconnected/blocked hole goals on bake. Flung goblins
outside the island die through the normal Kill path, then return to the pool near the bed. Source nodes/meshes
stay authorable GameObjects; no scene converts automatically. Bake Navigation after reshaping and save the area.
`Docs/Island_Ground_Plan.md` records implemented scope, setup and remaining Unity checks. Setup disables the old `Level_Grass`
in that area only after a nonempty successful bake, preserving its painted data. Visual tuning/Unity verification
is still pending. The earlier implicit-light mask and deformed relief are superseded.
Optional `Level_GrassWear` adds a real lower near-black shelf, clipped by a runtime shared R8 wear mask.
Horde walking drives capped, throttled capsule stamps through `Level_GrassWearSystem` (shared registry, no new
frame subscriptions). Upper shelf/card shaders clip worn pixels in all passes. CPU bilinear sampling matches
the mask for walking/resting support and blood. Paths merge and persist for the area life; reset on restart or Clear.
Keep static shelf colliders: airborne collision remains the baked shape; this is visual wear with cached height
support, not dynamic collider excavation. Setup is Ground Surface > Set Up Goblin Worn Paths; bake/save before play.

The user authors visuals with real GameObjects, MeshFilters and MeshRenderers in Unity. Provide inspector controls
that place and configure those objects; later custom rendering/optimisation must carry over their authored meshes,
materials and transforms. Do not replace this workflow with data-only authoring controls.

The user removed pyramids (2026-11-05). Do not restore Level_Pyramid or its layout/editor menu from older notes.
Reusable `Level_BlockStyle`, `Level_BlockMesh`, standalone `Level_Block` and the generic editor-only
`Level_BlockBaker` remain for future architecture. GameObject > Level > Low Poly Block is provided by the
independent `Level_BlockEditor`; no references to a pyramid component remain. See `Docs/Block_Tool.md`.
`Level_Ruins` is the new Egyptian ruins tool (GameObject > Level > Egyptian > Ruins). Cliff-style node handles
draw open walls or closed enclosures with per-section Wall/Broken Wall/Gateway/Gap, pillars, rubble and palette
bands. Keep real editable source Level_Block GameObjects under its EditorOnly group. Bake Ruins to One Mesh
uses Level_BlockBaker's singleMesh mode: ONE MeshFilter/Renderer and optional MeshCollider, with material
overrides preserved as submeshes. Default sandstone/terracotta/teal colours share one material via per-block
vertex colour overrides. Matching source transforms survive regeneration; manual extras remain, deleted
procedural slots return. Bake/save then rebake navigation; lintels must clear the flow bake's obstacle height.
Do not restore pyramids or start other proposed architecture tools. See `Docs/Ruins_Tool.md`; native Unity
visual/Undo/prefab/collider/navigation checks are pending. Block SourceChanged is a shared editor-only event,
not a dependency on a specific layout tool.
New static tools use Level_BakeGroup, Level_SourcePiece.Sync, Level_ToolCreation and optional Level_PathHandles. Level > Common > Bake Group combines nested tool sources once. See Docs/Level_Tools_Plan.md.
Grass cover with missing/duplicate source cards is an incomplete setup: show inspector guidance and skip
automatic bakes, retaining any saved output. Set Up Editable Cards explicitly creates missing sources.
Failed automatic bakes are retried only after source/settings changes or an explicit rebuild.

Chance-based visuals use `Horde_VariationGroup`: direct-child GameObject options, editable Chance (%) controls that redistribute the remainder (stored as compatible weights), Head/Body
anchor, preview buttons and one choice per group per goblin life. Preview visibility must never change spawn
chances. Groups are independent and cannot nest; empty options mean None. Future hats/horns/equipment visuals
use the same workflow. Gameplay behaviour stays separate. Horde_RenderGroup draws only selected options using
reused instance buffers; no per-goblin renderer subscriptions.

Goblin eye layouts use editable quad-card GameObjects authored with `Horde_EyeCards`. The existing Eyes placement
is local Y=1.2 with component Height=0: preserve it. The inspector's Create Two Eyes / Cyclops Variation Group
button wraps it without moving it and creates a separately authored central card. Each layout has its own card
size/position/rotation; preview and weights live on the common group. At bake, actual source meshes/transforms
are combined per layout and carried by the group's animation anchor. Never replace the editable source objects.
`Goblins/Eyes` handles blinking, gaze, hurt squint and black square-ended death X's with length/thickness controls.
Per-goblin size/asymmetry remain on Horde_GoblinType; the central eye ignores pair asymmetry. The implementation
is waiting for Unity setup, visual tuning and runtime verification; read Docs/State.md for details.

When you finish a module, or stop partway, update `Docs/State.md` ("Current module" and a new "Handoff log" entry: what changed, files, editor steps, untested bits, new allocations/subscriptions). Add settled choices to `Docs/Decisions.md`. The next agent depends on it.
