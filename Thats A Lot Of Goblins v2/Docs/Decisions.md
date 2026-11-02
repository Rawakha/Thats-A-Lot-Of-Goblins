# Decisions

Things already settled, with the reason. Don't undo one without asking the user. Add new entries at the top of their section.

## Game design
- **Skipping waves keeps surviving goblins in the clear count (2026-11-02).** Starting the next wave counts all living goblins still heading to holes, including survivors from earlier waves. A wave cannot clear until those survivors and its new spawns are killed or reach their holes.
- **Brotato-style roguelite (2026-11-02), replacing the plain incremental plan.** A run is a chain of small areas: waves from several holes, then you fly to the next edge (screen-space indicator, fade to black, next area).
  - Pick-3 cards give consumable ability charges and passives.
  - Weapons have Bloons TD-style upgrade paths.
  - About 3 weapons are swapped between.
- **The horde is mostly passive.** Special groups appear over time: **armed squads** that shoot the plane, and **shield tortoises** (shield bearers round the edge with an overhead roof, 20-30 goblins huddled under it) that block fire from above.
- **Superseded:** "incremental first" (2026-11). It was a power fantasy where you're rewarded for kills and not punished for goblins that reach the far hole; check with the user whether leaks still don't matter.
- **Gold shows "earned this round"**, not a score. The multiplier feed uses dark-humour group-kill names ("Family of four x2.5").
- **A wave ends when all its goblins are gone (2026-11-02)**, killed or into their goal hole. There's no timer.
- **Goblins reaching their goal cost nothing (2026-11-02).** The reason to kill is gold (upgrades) and the multiplier feed. Escapes aren't punished.
- **A run ends when the plane's health runs out (2026-11-02).**
- **Ejecting (2026-11-02):** when the plane goes down, the pilot can eject to save the run. A failed eject plus the plane destroyed ends it. **An eject succeeds with a timed button press:** a short window as the plane goes down. Miss it and you go down with the plane. **Ejects are limited per run:** you start with 1, and extra ejects come from pick-3 cards. Once they're used up, a crash ends the run. **After a good eject** the pilot parachutes down, and a few seconds later a replacement plane flies in and you carry on. Armed squads and anything else that hurts the plane take health. This replaces "hits cost fuel". Whether fuel stays at all is still open (State.md).
- **Waves:** goblins hop out of a hole and walk to a target hole at least `minTargetDistance` away (falling back if no hole is far enough). Space / gamepad South starts the next wave.
- **Ammo:** magazine plus auto-reload (no manual reload).

## Goblin physics and feel
- **Flung goblins use real Rigidbody physics** with one v1-style torque impulse around the axis square to the throw (`launchTorque`), and no linear damping in the air. A scripted or drawn tumble looked bad.
- **Flinging out of holes is a scripted hop-out arc**, not physics; the physics launch got goblins stuck.
- **Ground deaths:** collider and physics off (kinematic), tip over directionally without sinking into the ground, wobble stops, then shrink away.
- **Air deaths:** stay physical until at rest, then shrink, with no second hit flash.
- **Launched goblins stop the walking animation** (motion fades out) and don't disappear until they land.

## Weapons and input
- **Ground / air targeting is a toggle** (middle mouse, gamepad North), not Ctrl or a hold.
- **Ability (carpet bomb) is right mouse / RB / gamepad South.** Space is only Next Wave.
- **The carpet bomb is self-contained:** no extra managers. Bombs are `Combat_Projectiles` with `explodes`; the inner radius bursts goblins and the outer ring flings them screaming.
- **Player plane and weapons stay main-thread C#**; Burst/Jobs only for the horde and mass systems unless profiling says otherwise.

## Tech
- **Variation authoring uses exact percentages** (2026-11-02): changing one option redistributes the remainder proportionally across the others, or evenly if they were all zero. Percentages total 100; a sole option is 100. Existing relative weights remain the compatible storage/runtime representation. New options start at 0% except the first; removing an option normalises the survivors.
- **Chance-based visual options use reusable variation groups** (2026-11-02): direct-child authored GameObjects with relative weights, exactly one choice per group per goblin life, independent groups, and explicit Head/Body anchor. Empty option means None; preview selection never controls runtime chances. Spawn selections are data used by instanced draw filtering; source authoring stays GameObjects. Cyclops and two-eye layouts are the first application, with separate central-card transforms and existing continuous size/asymmetry variation retained. Groups do not nest; future gameplay behaviours remain separate.
- **Eye asymmetry and death X shape** (2026-11-02): death X uses black, square-ended strokes with separate length and thickness controls. Per-type chance/size-range settings can make one randomly selected eye larger per goblin; both still share blink, gaze and expressions.
- **Eye optimisation preserves the two editable source cards** (2026-11-02): combine their actual meshes/transforms once per horde render group into one owned mesh, render it as a head-animated part, and dispose it with the group. Eyes may be beside Head (current prefab: Eyes Y=1.2, component Height=0); anchor conversion preserves that placement without reparenting. Both eyes share one per-goblin blink/gaze/hurt/death state. Black death X's start at lethal damage even airborne. Per-goblin eye size is shader-driven around each authored card centre, with the same roll for both eyes.
- **Author visuals with real GameObjects first** (2026-11-02): inspector tools position/configure MeshFilter/MeshRenderer objects in prefabs; later instancing/optimisation must preserve those authored meshes, materials and transforms. Goblin eyes start as two separate quad cards under Head, with slider placement (`Horde_EyeCards`). A combined mesh and shared per-goblin eye state are intended for a later rendering step. Eye shader/animation are explicitly outside step 1.
- **Level tools share the tree/cliff Scene-view workflow** (2026-11-02): white planar movement squares, green add dots, Ctrl/Cmd-click red delete dots where points apply; yellow height/depth cones, orange width/radius cubes, pink length cubes and blue turn rings where relevant. Configurable snapping, Undo, visible outlines and inspector guidance. Ground/hole meshes and colliders rebuild on release, with outline previews while dragging. The town blockout was only a test and is excluded from this work.
- **Cliffs are one generated mesh each** (`Level_Cliff`), never a cube per block. The look matches the user's Cube & Seek reference exactly: chunky square block columns, stepped and jittered, soft pastel shade variation. Goblins can't walk on top (for now); cliffs are flow-field obstacles via the Building layer.
- **Goblins are prefabs and GameObjects with a Rigidbody root** for now, rendered by instancing (the meshes are stripped from the GameObjects). Head and body are sibling parts with separate scales.
- **Path to 20k goblins:** move to a data-only horde (NativeArrays, Burst movement, spatial-hash separation, custom ballistic flight, optionally a small pool of Rigidbody "stunt doubles"), then spatial-hash combat queries, culling plus `RenderMeshIndirect`, and LOD simulation. Do this before goblin types and armed goblins. **Deferred** until the incremental modules are in.
- **Physics solver iterations are 2** by default (set per goblin type).
- **Sounds are `SoundData` assets**, not inline configs.
- **The flow field is rebuilt from scratch** (not ported from v1): baked map, frame-sliced solves, local repair, multiple fields from a pool.
