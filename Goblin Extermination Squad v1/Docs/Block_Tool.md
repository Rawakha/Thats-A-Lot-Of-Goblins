# Low-poly blocks

The user removed the pyramid tool on 2026-11-05. Its component, layout and menu must not be restored from older handoffs.
The reusable masonry primitives remain for future architecture; no dedicated ruins layout tool is built.

## Authoring

- **GameObject > Level > Low Poly Block** creates a real MeshFilter/MeshRenderer GameObject.
- Use Transform for position, rotation and size. The mesh pivot is bottom-centre.
- Assign a **Block Style** and choose a Seed for clipped-corner geometry and subtle shade variation.
- Create styles via **Assets > Create > Goblins > Level > Block Style**; duplicate the default asset for independent looks.
- Style controls colour/material, corner cuts/variation, top taper/lean, shade variation and side darkening.
- The default material uses Goblins/Level and multiplies generated vertex colours by its material Color.
- Style/seed changes regenerate the source mesh and material assignment; apply manual mesh/material overrides afterward.
- Standalone blocks render normally and have no default collider. Add a collider normally if needed; rebake navigation after obstacle changes.

## Reuse and cost

`Level_BlockStyle`, `Level_BlockMesh`, `Level_Block` and the editor-only `Level_BlockBaker` are independent of layout tools.
A future wall/ruin tool can consume actual source meshes/materials/transforms without another stone generator.
The generic baker groups by spatial cell/material, with optional static MeshColliders; it currently has no standalone inspector bake command.

Clipped stones use 72 vertices / 44 triangles; zero corner cut uses 24 / 12. A cap primitive remains available (16 / 6).
Baking limits are 4,096 active sources, 60,000 vertices per source/chunk and one million emitted vertices.
Mesh property blocks and renderer flags are not copied by the generic baker.

No runtime ticking, subscriptions, generation or steady-state managed allocations are added by these primitives.
Standalone blocks remain individual renderers until a future layout tool explicitly bakes them. Source meshes are saved
with the scene/prefab; editor generation/cleanup allocates working lists and meshes and retains shared filter/collider meshes.
Native mesh persistence, visual tuning, Undo and prefab behaviour still need checking in Unity.
