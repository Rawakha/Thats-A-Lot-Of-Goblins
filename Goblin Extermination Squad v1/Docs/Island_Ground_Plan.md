> **ABANDONED (2026-11-06).** The user removed the Island tool because it looked wrong, and its code is deleted. Do not restore it. Kept for history only.

# Drawn island ground — implemented first pass

Built 2026-11-05 after the user chose a flat top, a separate lower flat plane for the future water bed,
and kills for goblins falling off. Runtime/editor C# and 37 isolated geometry/regression checks passed.
Unity authoring, visuals, physics, navigation and runtime accounting still need verification.

## Using the tool

Select Ground > **Create / Select Island Outline**, or GameObject > Level > Island (Selected Ground).
White squares move nodes, green dots insert, Ctrl/Cmd deletes and the yellow handle changes cliff depth.
Tune the outline's cliff depth/segments/steps/relief/seed/colour/material and the bed gap/margin/colour/material.
Ground must stay horizontal at world scale 1; outline XZ controls its footprint, Ground Transform Y controls its top.
Drawn grass shelves and cards follow the exact clipped outline. Hole openings must remain wholly inside it.

After shaping, use **Fit Flow Grid to Island Bounds** if needed, then **Bake Navigation for This Area**.
Blocked/disconnected hole goals reject the navigation bake; fix the warnings before playing. Optional
**Fit Area Flight Bounds to Island** keeps the existing rectangular flight system. Save the area.
Existing scenes remain rectangles until explicitly converted. Water rendering and raised playable terraces are later.

The design notes below record the intended direction; the newest State handoff describes the final implementation
and limitations. The cap implementation reuses the exact band-sweep builder rather than changing the triangulator.

## Intended result

Draw a closed island footprint in Scene view using the same handles as cliffs and grass areas. It generates a
flat playable cap with substantial low-poly cliff sides dropping below it. The island has a deliberate silhouette,
not an infinite rectangular plane. Existing rocks, trees, hole openings, shelf outlines and grass remain editable
GameObjects. Choose top and cliff materials/colours independently per island. Keep the grid available as a top material.

The playable cap stays flat in the first version, at the existing ground transform height. This preserves planar
flow movement and the current shallow shelf heights while making the island a real solid shape with a visible drop.
Terrain sculpting, multiple playable elevations, bridges between separate islands, shoreline water and falling-off
gameplay rules are separate decisions.

## Authoring

- Extend `Level_Ground` with an opt-in outline source; existing rectangle grounds and script GUIDs remain valid.
  A proposed child `Level_IslandOutline` owns the closed nodes as a real source GameObject. Output remains real
  MeshFilters, MeshRenderers and saved collision meshes. Never replace or discard editable sources.
- Buttons: Create Island Outline from Current Ground, Rebuild Island, Fit Flow Grid to Island Bounds and
  Fit Area Bounds to Island Bounds. Fitting is explicit; drawing does not silently move existing gameplay limits.
- White planar movement squares, green midpoint insert dots, Ctrl/Cmd remove, existing snapping. Yellow depth
  handle for cliff drop; inspector sliders for global depth, edge segment length, face lean, step count/width,
  silhouette variation, side shade and seed. First geometry stage needs only depth/material plus outline controls.
- Preview the outline during dragging; bake cap, sides and dependents after release. Invalid edits show their
  location and retain the previous valid mesh/collider/scatter. Support Undo, prefab overrides and save/reopen.

## Geometry and appearance

1. Validate a simple closed polygon: finite nodes, >=3 distinct corners, nonzero area, no duplicate/zero-length
   edges, no crossings/touches. Accept either winding and concave shapes. Normalise winding internally.
2. Use the existing exact band-sweep builder for the cap and accepted hole rings, preserving flatness, concave
   coverage and winding. Keep previous geometry on invalid outlines, hole containment failures and budget errors.
3. Generate a continuous solid perimeter skirt dropping below the cap, with a bottom cap if the camera can see
   underneath. Use split vertices for clean flat normals. Keep side seams closed at corners and the top edge.
4. Add the reference's chunky cliff style in a later visual pass: broad vertical facets/slabs, occasional steps
   and restrained offsets, with darker lower faces. Reuse the existing cliff style and materials, but do not place
   an upward `Level_Cliff` Ring on top of the island: its geometry has a different job. A continuous core keeps
   silhouettes watertight beneath optional decorative slabs. Decorative pieces do not add individual colliders.
5. Clamp or reject offsets/steps that cross at narrow concavities. Test acute corners and narrow necks before
   offering larger offsets. Deterministic seed changes detail without moving the authored playable boundary.

Bake opaque meshes in spatial sections when needed for camera culling; choose section size from the actual map
scale and profiling. Use simple saved collision geometry. Subdivide excessively long collision triangles with a
bounded, shared-edge process to avoid the project's previous >500-unit triangle warning. No runtime mesh generation,
per-edge scripts, per-goblin raycasts or per-frame allocations.

## Required integration — part of the first usable island

- **Hole openings:** current `Level_Ground.CollectHoles` checks a rectangle. Replace that check for outline mode
  with whole-opening containment/edge clearance, including concave sections. Reject holes touching/crossing the
  shoreline or each other with actionable gizmos; never silently cap a gameplay hole. Keep accepted rings identical
  for ground and shelf cuts.
- **Grass shelves:** `Level_GrassLayerMesh.Build` currently clips to Size. Include island edges in its sweep
  breakpoints and intersect the union with the island's interior intervals before subtracting holes. Do not merely
  discard outside vertices or test triangle centres. Preserve exact seams/heights, shelf side walls and editable
  patch sources. The existing cover then follows the clipped saved bands and no longer scatters over the void.
- **Navigation:** `Flow_System.Bake` currently uses obstacle CheckBox calls; empty space is open. Bake cells
  outside the island as blocked and use body-radius/edge clearance so cell centres near a drop are also safe.
  Preserve current wall costs and runtime cost overlays. Check valid spawn/goal holes and routes; report unreachable
  destinations rather than accepting an island that traps or routes goblins across the void. Navigation grid may
  remain rectangular; the cost mask represents the island.
- **Ground sampling:** current `Level_GroundSurface.HeightAt` returns a caller's flat fallback outside shelves.
  Island mode needs an explicit ground-presence query, e.g. TryGroundAt(position, out height), distinguishing exposed
  base ground from a hole or outside the island. Audit horde landing/recovery, gibs, blood and projectile virtual floors
  before enabling the footprint: otherwise objects can land on an invisible plane beyond the edge. Reuse cached
  footprint/band data; no per-object Physics raycast. Decide the cleanup/death policy for objects leaving the island
  before implementing any new falling-off behaviour.
- **Plane limits:** existing `Area_Bounds` rectangles remain flight limits. Their optional Fit button uses the
  island's AABB plus authored margin; it does not force plane movement to follow the irregular shoreline.

## Delivery sequence and acceptance

1. Implemented: one island with closed outline, cap/skirt, material/depth controls, a lower bed plane,
   accepted hole rings and the required grass/nav/ground-presence integration. Verify it in a small area before
   converting the current map. Keep all existing scenes in rectangle mode unless the user converts them.
2. Tune the cliff-edge silhouette with broad facets/steps and per-node overrides if the global controls are insufficient.
3. Consider optional decor, underside taper, water/background and multiple islands only after the first result looks right.

Checks: convex/concave/either winding, tight necks, invalid edits retaining old results, holes near bends and edges,
grass crossing the boundary, feet/blood/projectiles/gib landings, routes around bays, no routes through the void,
edge clearance at horde scale, prefab/Undo/save/reopen, collider seams, additive area loading, draw count and bake time.
