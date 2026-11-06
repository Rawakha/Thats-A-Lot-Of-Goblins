# Decisions

Things already settled, with the reason. Don't undo one without asking the user. Add new entries at the top of their section.

## Game design
- **Overview and run structure (2026-11-06):** see `Docs/Overview.md`. Upgrades belong to the plane, not the map; a map lasts
  about 4-5 minutes with an upgrade screen after each. Missions are authored maps of three types: goblins attacking or entering
  something, goblins raiding something (in and out with loot), protecting a road for cars. Pick Three (roguelike, random map
  order) is built and tested first; if it isn't fun, try the permanent upgrade tree (incremental) and let play-test feedback
  choose. Armed goblins (RPGs) and vehicles are planned as separate systems.
- **Island terrain (user, 2026-11-05):** a flat playable island top, cliff edges dropping to a separate flat plane that will later be the water bed. Goblins falling off die and count as kills, not escapes. Walking goblins avoid the shoreline; blast throws remain physical. No raised playable terraces or water rendering in this module.
- **Results reuse the crosshair as the cursor (user correction, 2026-11-05).** Keep the same crosshair visual visible on pass/fail, following the actual mouse position directly regardless of gameplay aim mode or smoothing. The system cursor stays hidden.
- **Results wait for every goblin (user, 2026-11-05).** Reaching zero leaks left never stops spawning or ends play. Finish all configured waves and wait until no living wave goblins remain (killed or entering their goal holes), then show the pass/fail result. Leaks continue counting beyond the limit for accurate results; the existing limit still determines pass/fail at completion. Debug waves still cannot end an area. This supersedes the immediate zero-health failure rule below.
- **The tower-defence version is dead (pivot to the plane game).** Towers, troops and the farm loop are not planned; the tower
  code and commits are legacy. Don't build on them without asking.
- **Art direction (2026-11-04):** purple world (dusky lavender ground and leaves, chunky orange or purple grass), green goblins, a
  warm cream-and-orange plane (the lightest, most contrasting thing on screen), orange explosions, saturated purple blood. The
  plane must beat both the world and the goblins in a greyscale check. Not yet applied everywhere; change materials, not code.
- **Gore is purple, cartoony and ad-safe (2026-11-04):** heads pop on 50% of every kill (X eyes), scraps tear off otherwise, blasts
  burst goblins into soft rounded chunks (skin outside, purple cut faces) that thud, squish and splat; blood must be visible
  from the plane (saturated droplets, splats a little brighter than the ground but not louder than the goblins).
- **Carnage stays (2026-11-04):** corpses and gibs leave oldest-first and only at their limit (no timer), because crowded
  spots are the massacre; only blood splats thin from the most crowded spots (flat and redundant). Packed squares do thin their
  oldest pieces gradually so one blast can't use the whole budget; fresh pieces are protected for a few seconds.
- **Tree Seeds is limited to one tree wall on the map at a time (2026-11-04):** planting a new one removes the old.
- **Weapon vs low flight (2026-11-04):** the machine gun must not benefit much from flying low; specific weapons (flamethrower,
  scatter gun, paint roller) should. Diving only shortens the strafe distance, so a goblin right by a hole needs a dive.
- **End of an area (2026-11-05), pass and fail alike:** a beat, slow motion on a final kill, then the game flies the plane south off screen with the camera stopped. The HUD slides off and a centred panel comes in over the wreckage; corpse and gib clean-up is paused throughout.
- **Going incremental (user, 2026-11-04), because it has a clear audience to target.** A skill tree replaces the pick-3; its visuals are
  to be explained by the user. Entries below describing the pick-3 loop are superseded where they conflict; finer details (per-map
  leaks vs the health pool, stars, gold, level select) aren't settled yet. See the checklist at the top of `State.md`.
- **The earlier run design is `Docs/Run_Loop_Brief.md` (manager, 2026-11-04): a run-based pick-3 loop, to test whether it's fun.**
  It replaces the Brotato-style roguelite entry and the stars / incremental briefing that came before it (never built beyond
  removals, since restored). If it isn't fun after three areas in a row, the pivot is the incremental version: gold buys
  permanent upgrades, stars (20 / 10 / 0 leaks), a results screen and a level select. Keep areas, spawn events, the Run scene
  and `Run_Carry` reusable for it.
- **The loop (2026-11-04):** the player starts each area with a fresh **flare** ability and nothing spawns until it's fired (no
  Ready phase, no Space / Next Wave). Each area's `Wave_Director` holds **spawn events**: spawn hole, target hole, count, rate,
  and a trigger (flare fired, after N seconds, after N goblins killed, when N or fewer remain, after another event finishes);
  the first is normally the flare. The area is clear when every event has finished and no goblins are left. Debug Waves stay for
  clips. (Replaces "a wave ends when all its goblins are gone", "skipping waves keeps survivors in the clear count" and the
  Space-starts-the-next-wave entry. Stage 2.)
- **Leaks are a run-wide health pool; some comes back after each clear (2026-11-04, kept).** Each leak spends one and zero fails
  the run (Game Over with Restart). After a clear half of what was lost (`Area_Settings.healthBack`, rounded up) comes back as
  the next area fades in, as a pop on the counter. The pool is `Area_Settings.startingHealth` of the first area and carries
  through `Run_Carry`. Debug waves count as leaks unless `Run_State.countDebugWaves` is off.
- **3D UI wherever we can (2026-11-04).** After a clear there are no buttons or bars: choices are made by flying.
- **Results screen (2026-11-04), replacing every fly-through exit idea (kept simple to test the loop; 3D UI can return as polish).**
  One screen for pass and fail (`UI_GameOver`): a random title from a list and a message with {0} for goblins that got away. A pass
  has Next Area and Restart; a fail has only Restart. Fail messages can be per area (`Area_Settings.failMessages`). When the screen
  appears the plane stops where it is and its audio fades out. Next Area: fade, `Run_Edge.UpgradeHook`, next area. Map limits stay on, and a red dotted line
  marks the warning line (`Run_Gates`). Planned: the HUD slides out for the panel, with stars and gold.
- **Pick-3 shop (2026-11-04, stages 3-4):** at the fade, one pick from three offers drawn from one pool of weapons and abilities;
  offers exclude what was taken last time; picks are kept for the run in `Run_Carry`; weapons swap with Q (needs a second weapon).
- **Out of scope for this version:** gold upgrades and the upgrades screen, the loadout screen, stars and the results screen,
  the level select, bosses, plane health and eject, armed squads. Gold shows "earned this round", not a score, when it arrives.
- **The horde is mostly passive.** Special groups appear over time: **armed squads** that shoot the plane, and **shield tortoises** (shield bearers round the edge with an overhead roof, 20-30 goblins huddled under it) that block fire from above.
- **The multiplier feed** uses dark-humour group-kill names ("Family of four x2.5").
- **Areas are separate scenes in a persistent shell (2026-11-04).** A Run scene (plane, camera, HUD, `Horde_System`,
  `UpdateManager`, audio, run state) stays loaded; one Area scene (ground, holes, cliffs, flow map, `Wave_Director`,
  `Area_Settings`) is loaded on top of it. Each area scene is playable alone (Play from any open area starts in the Run scene).
  Restart reloads the Run scene. Built so semi-procedural areas can assemble the same pieces later (needs a runtime flow-map
  bake, not built yet).
- **Area authoring (2026-11-04):** an area is a scene plus an `Area_Settings` asset that points at its `Wave_Settings`. `Area_Root`
  on the Wave_Director's object is the one thing assigned; its inspector holds a checklist, Play This Area and the settings.
  Goblins > Area > New Area copies a scene and makes its own linked assets in `Assets/Areas/<name>/`. A debug wave can't
  clear an area.
- **Scene split (2026-11-04):** the Run scene (first in Build Settings: plane, camera, HUD, horde, `UpdateManager`, `Run_State`,
  `Run_Boot`, light and volume) always loads first; `Run_Boot` loads the area scene on top and makes it the active scene.
  In the editor, pressing Play with an area open starts in the Run scene and loads that area (`Area_PlayMode`). Run-scene
  scripts never hold area references; they listen to `Area_Root.Changed`. Restart reloads the Run scene. North is world +Z.
- **Loading the next area reloads the Run scene (2026-11-04).** The debris, effect and pool systems have no clear/wipe, so
  leaving an area reloads the Run scene (like Restart); `Run_Carry` (static) holds the health pool, what's still owed and the area
  to load, and the picks. Swapping the area scene in place can return once those systems can be wiped.
- **Map limits (2026-11-04, kept):** two rectangles (`Area_Bounds`): crossing the warning line shows the warning; crossing the
  respawn line takes control, stops the camera, fades out and flies the plane back in from the bottom at the area's spawn point
  (`Run_Edge`). It's later replaced by the plane being shot down.
- **Prompts are one system (2026-11-04):** every on-screen prompt is a `UI_Prompt` on a panel (priority; slides via `UI_SlidePanel`)
  that calls `SetWanted`; the static `UI_Prompts` shows only the highest priority wanted one and brings the others back when it
  goes (`Suppress` hides all during fades). Priorities: edge warning 100, wave prompt 10 (removed with Next Wave in stage 2).
  New prompts use it too; don't slide panels by hand.
- **Run stages (2026-11-04), one at a time:** 1 clean-up and the ground indicator (done); 2 spawn events and the flare (flare and
  time triggers first, then kill-count, remaining and after-event; remove the Ready phase and Space start); 3 pick-3 shop with a
  few placeholder items, the no-repeat rule and the screen; 4 weapons in the pool (equip and swap with Q; needs the Egg
  Launcher or another second weapon). Then the fun test: three areas in a row.
- **Ejecting (2026-11-02, later; belongs with plane health and armed squads, and is out of scope for the current run loop):** when the plane goes down, the pilot can eject to save the run. A failed eject plus the plane destroyed ends it. **An eject succeeds with a timed button press:** a short window as the plane goes down. Miss it and you go down with the plane. **Ejects are limited per run:** you start with 1, and extra ejects come from pick-3 cards. Once they're used up, a crash ends the run. **After a good eject** the pilot parachutes down, and a few seconds later a replacement plane flies in and you carry on. Armed squads and anything else that hurts the plane take health. This replaces "hits cost fuel". Whether fuel stays at all is still open (State.md).
- **Waves:** goblins hop out of a hole and walk to a target hole at least `minTargetDistance` away (falling back if no hole is far enough). Space / gamepad South starts the next wave for now; spawn events replace that (see above).
- **Ammo:** magazine plus auto-reload when empty; R also reloads early (added 2026-11-03).

## Goblin physics and feel
- **Juice bar: Vlambeer (2026-11-04).** Feel and juice work aims at Nuclear Throne / Luftrausers or beyond: bold, layered feedback
  (shake, hitstop, kick, flashes, permanence, sound punch), every amount still a tunable with a tooltip. UI springs go through
  `UI_Punch`; text and images with shadows use `UI_Shadowed`.
- **Flung goblins in the fling ring (2026-11-03):** each rolls to die on the spot (80% at the kill radius fading to 10% at the edge);
  the rest fly screaming with eyes open, and any the blast would have killed die when they land. Landing hurts (fall damage), so
  tougher goblins can survive. Landing kills most of the spin so they slam and skid instead of spinning on the floor.
- **One-shot machine gun for now (2026-11-04):** the Default Goblin has 3 health and the gun does 3 damage; kills feel better at
  horde scale. Tougher goblin types get more health instead of lowering gun damage.
- **Flung goblins use real Rigidbody physics** with one v1-style torque impulse around the axis square to the throw (`launchTorque`), and no linear damping in the air. A scripted or drawn tumble looked bad.
- **Flinging out of holes is a scripted hop-out arc**, not physics; the physics launch got goblins stuck.
- **Ground deaths:** collider and physics off (kinematic), tip over directionally without sinking into the ground, wobble stops, then shrink away.
- **Air deaths:** stay physical until at rest, then shrink, with no second hit flash.
- **Launched goblins stop the walking animation** (motion fades out) and don't disappear until they land.

## Weapons and input
- **Egg Launcher is an active main weapon (user, 2026-11-05).** Fires eggs with a hatch chance; chickens choose the nearest living goblin and attack. Build in separately tuned steps: egg flight/impact/ammo first, hatching/lifetime/cap second, chase/peck third. Do not build later steps or weapon cycling in step 1. Keep chicken visuals authored as editable real GameObjects. The earlier 50% hatch chance is a starting suggestion for tuning.
- **Ground / air targeting is a toggle** (middle mouse, gamepad North), not Ctrl or a hold.
- **Ability (carpet bomb) is right mouse / RB / gamepad South.** Space is only Next Wave.
- **The carpet bomb is self-contained:** no extra managers. Bombs are `Combat_Projectiles` with `explodes`; the inner radius bursts goblins and the outer ring flings them screaming.
- **Player plane and weapons stay main-thread C#**; Burst/Jobs only for the horde and mass systems unless profiling says otherwise.

## Tech
- **Level tools share one foundation; two tool bases (user, 2026-11-06).** Generating tools derive from `Level_Tool` (stones baked to one mesh). Tools that build their own output derive from `Level_BuiltTool` (Cliff, Tree Area, Ground), keeping their geometry but sharing rebuild rules, handles, feedback, help and creation. Three handle kinds: Piece, Path, Outline, all on one set of node gestures and one handle look. Grass keeps its own bake because of wind and custom vertex data. Egyptian Ruins is not converted, as the user has a new Egyptian spec. See `Level_Tools_Plan.md`.
- **Closed outlines refuse crossing edits (2026-11-06).** A drag or delete that would make a valid outline cross itself is ignored, keeping the last valid shape; an already tangled outline can still be edited so it can be fixed.
- **Bake meshes save in the scene, or as assets under prefabs (2026-11-06).** Prefabs can't hold scene meshes, so anything prefab-related saves to a Baked Meshes folder, overwritten on rebake. Bake On Save stays off until it's been tried in Unity.
- **Island, Cliff Ring and Cliff Chunks removed (user, 2026-11-06), because they looked wrong.** The Island ground tool (`Level_IslandOutline`, `Level_IslandMesh`, `Level_IslandRegistry` and their references in ground, grass, flow and horde code) is deleted, as are the Cliff Ring and Cliff Chunks shapes. Cliff Line and Cliff Block remain unchanged (`Level_CliffShape` keeps Line = 0, Block = 1). This supersedes the Island terrain decision (Game design) and the island outline decisions (Tech). Don't restore them.
- **Static architecture tools share bake groups and authoring helpers (2026-11-06).** Keep editable geometry under an explicit owned folder. Parent bakes consume nested source visuals once, preserve materials as submeshes, and reject unsupported inputs before replacing output. Paths and source-piece tracking are optional helpers. Specialized grass/ground baking stays independent.

- **Level tools favour separate manually composed architecture (user, 2026-11-06).** Creation menus should be categorised by theme (Egyptian/Modern/Medieval etc). Egyptian walls should default to open paths, with independent pillar and doorway GameObjects and optional pillars along walls. Common baking/authoring infrastructure must be planned before implementation; the current enclosure-first tool is not the desired default workflow. See the proposed foundation in `Level_Tools_Plan.md`; implementation/details await discussion.
- **Egyptian ruins bake to one combined mesh (user, 2026-11-06).** Draw editable wall/enclosure nodes with Wall/Broken Wall/Gateway/Gap sections, pillars and seeded rubble. Retain real source GameObjects, then combine actual authored geometry/materials/transforms into one MeshFilter/Renderer with optional exact static MeshCollider. Default sandstone/terracotta/teal bands use vertex colours and one material; custom materials remain as submeshes in that same mesh. EditorOnly sources are stripped from builds. Preserve matching transform offsets; layout/style regeneration restores deleted procedural slots and removes obsolete slots while retaining manual extras. Use real open gateway collision and sufficient lintel height for the flow bake. Reuse low-poly stone helpers; do not restore the removed pyramid system.
- **Pyramids removed by the user (2026-11-05; supersedes the earlier pyramid decision below).** Do not restore the pyramid component, layout or menu. Retain standalone low-poly block/style/mesh/baker utilities, independent of any layout component. Incomplete grass-card setup is handled in the inspector instead of repeated automatic-bake warnings; unchanged invalid bake inputs do not retry on unrelated editor refreshes.
- **Custom environment lighting receives URP light cookies (2026-11-05).** Compile `_LIGHT_COOKIES` forward variants for level masonry/cliffs, grass shelves and grass cards. Custom light loops must use the world-position-aware cookie lookup (or explicitly sample it for legacy unshadowed grass). Preserve the existing Lighting Influence blend: cookies affect lit colour, while unlit/emission remains unchanged. Do not multiply the whole final colour by the cookie, which would also darken ambient and self-lit colour.
- **Pyramids share reusable low-poly masonry (user, 2026-11-05).** Support both stepped flat-top tiers and narrowing pointed courses with an apex capstone. `Level_BlockStyle` owns clipped corners, taper/lean, colour/shading and material; bottom-centred `Level_Block` GameObjects retain editable source geometry. Layout and source baking are independent for future ruins. Bake actual source meshes/materials/transforms into spatial render/collision chunks; retain sources in an EditorOnly group, stripped from builds. Matching generated slots preserve transform offsets; layout regeneration restores deleted slots, while style/seed edits regenerate source geometry/material. No runtime generation/ticks or per-stone rendering in built pyramids. A dedicated ruins layout tool is not included.
- **Warning line opacity uses transparent rendering (user, 2026-11-05).** Line Alpha (0–1) multiplies Warning Colour alpha on Area_Bounds. A dedicated unlit transparent shader replaces the opaque level material, with no depth writes or shadow/depth passes; appearance edits update the existing property block without geometry rebuilding.
- **Warning boundary is a red dotted ground mesh (user, 2026-11-05).** `Area_Bounds` owns an always-visible authorable marker at WarningSize, with configurable dash dimensions/colour/floor offset and a saved source mesh. Bake floor sampling and one startup refresh avoid per-frame work. The marker does not move the warning/respawn limits. This replaces the unwired Run_Gates helper for the current area setup.
- **Goblin-worn paths reveal a lower shelf (user, 2026-11-05).** Walking traffic removes grass and reveals a near-black mesh below both grass shelves, merging into persistent paths like the earlier reference. Optional per-ground `Level_GrassWear` uses capped CPU capsule rasterisation plus a shared R8 texture, batched through the existing horde fixed loop. No per-goblin effect objects, runtime mesh rebuilding or new frame subscriptions. Cached CPU sampling keeps walking/settled support and blood aligned; upper shaders clip consistently in colour/depth/normals/shadow passes. Airborne physics retains baked shelf colliders; exact dynamic collider excavation is outside this visual module. Wear resets with the area or Clear, with width/strength/height/colour/performance controls.
- **Island outlines extend the existing ground (2026-11-05).** Opt-in child `Level_IslandOutline` sources preserve rectangle-mode scenes and top materials. Use the existing exact band-sweep mesh builder for concave island caps/holes and shelf intersection, retain continuous cliff collision separate from decorative stepped slabs, and refine collision triangles to <=64 m edges. Cache footprint/cap bands for runtime height and shore queries via `Level_IslandRegistry` on `Core_RegistryManager`; no per-goblin raycasts. Existing effect virtual floors use the lower bed outside the island. Navigation bakes reject blocked/disconnected hole goals and retain the previous map; geometry changes require an explicit navigation rebake.
- **Cover slots define source roles (bug fix, 2026-11-05).** An assigned Flower Card must generate flower geometry and petal metadata; an assigned Grass Card must generate grass. Repair role mismatches in the editor without resetting authored dimensions, mesh material or transform, and show the assigned role read-only to prevent accidental invisible flowers. Distinct source GameObjects are required for the two slots.
- **Grass tips vary per blade (user, 2026-11-05).** Keep single-quad grass cards, with independently random width, height, roll/lean and tip-width ranges. Narrow tips modestly around the authored tip centre while preserving roots and source placement. Bake this variation into the existing chunks; no per-frame CPU deformation or additional geometry.
- **Reference-style grass stays authored and tunable (user, 2026-11-05).** Expand the rectangular-card workflow with independent width/height, broad coverage/height patches, tilt, colour/facing/wind and flower-head controls. Offer an optional carpet preset without changing the user's palette or source placement. Island ground is the next requested planning topic, not an implemented feature; see `Island_Ground_Plan.md` for the proposed scope and required navigation/shelf/floor integration.
- **Simple billboard grass and flowers (user, 2026-11-05).** Replace the detailed slab/box grass look with short rectangular camera-facing cards and sparse square-petalled flowers, following the supplied third reference. Editable `Level_GrassCard` source GameObjects supply actual meshes, materials and transforms to `Level_GrassCover`'s saved spatial chunks. Scatter on exposed light/dark shelves only, excluding openings and covered dark areas. Grass roots use the shelf's exact shared colour function/settings; tip tint and flower colours are per ground/area. No per-blade objects or CPU frame work. Keep the old painter/data available but disable its component in the area after a successful nonempty setup. Broader area-wide lighting/atmosphere presets are still a proposal, not implemented by this vegetation step.

- **Grid base plus two authored grass shelves (user correction, 2026-11-05).** Keep the original ground/grid material at the bottom. Explicitly draw both light and dark grass as closed GameObject outlines. Same-type overlaps merge into one flat mesh per type, dark above the grid and light above dark, with visible side faces. Use a shared world-space colour function for shelf shading and future matching grass blades. This replaces the implicit light ground, colour mask and deformed heightfield decisions below.

- **Grass surface relief is baked geometry (user, 2026-11-05).** Raise light grass and recess dark patches with shallow bevels. Preserve the original Level_Ground mesh and hole openings, and use a separate derived mesh for rendering/collision. A cached height lookup keeps walking goblins and ground effects aligned without per-goblin raycasts. Refinement concentrates at patch transitions and has a vertex cap.
- **Grass surface authoring uses cliff-style outlines (user, 2026-11-05).** Light grass covers Level_Ground by default. Dark patches are retained child GameObjects with editable closed nodes, white movement squares, green add dots and Ctrl/Cmd deletion. Bake their union into a single saved ground mask. The top surface shader owns the light/dark look. Keep this independent of the future runtime horde wear mask and grass cards.
- **Variation authoring uses exact percentages** (2026-11-02): changing one option redistributes the remainder proportionally across the others, or evenly if they were all zero. Percentages total 100; a sole option is 100. Existing relative weights remain the compatible storage/runtime representation. New options start at 0% except the first; removing an option normalises the survivors.
- **Chance-based visual options use reusable variation groups** (2026-11-02): direct-child authored GameObjects with relative weights, exactly one choice per group per goblin life, independent groups, and explicit Head/Body anchor. Empty option means None; preview selection never controls runtime chances. Spawn selections are data used by instanced draw filtering; source authoring stays GameObjects. Cyclops and two-eye layouts are the first application, with separate central-card transforms and existing continuous size/asymmetry variation retained. Groups do not nest; future gameplay behaviours remain separate.
- **Eye asymmetry and death X shape** (2026-11-02): death X uses black, square-ended strokes with separate length and thickness controls. Per-type chance/size-range settings can make one randomly selected eye larger per goblin; both still share blink, gaze and expressions.
- **Eye optimisation preserves the two editable source cards** (2026-11-02): combine their actual meshes/transforms once per horde render group into one owned mesh, render it as a head-animated part, and dispose it with the group. Eyes may be beside Head (current prefab: Eyes Y=1.2, component Height=0); anchor conversion preserves that placement without reparenting. Both eyes share one per-goblin blink/gaze/hurt/death state. Black death X's start at lethal damage even airborne. Per-goblin eye size is shader-driven around each authored card centre, with the same roll for both eyes.
- **Author visuals with real GameObjects first** (2026-11-02): inspector tools position/configure MeshFilter/MeshRenderer objects in prefabs; later instancing/optimisation must preserve those authored meshes, materials and transforms. Goblin eyes start as two separate quad cards under Head, with slider placement (`Horde_EyeCards`). A combined mesh and shared per-goblin eye state are intended for a later rendering step. Eye shader/animation are explicitly outside step 1.
- **Level tools share the tree/cliff Scene-view workflow** (2026-11-02): white planar movement squares, green add dots, Ctrl/Cmd-click red delete dots where points apply; yellow height/depth cones, orange width/radius cubes, pink length cubes and blue turn rings where relevant. Configurable snapping, Undo, visible outlines and inspector guidance. Ground/hole meshes and colliders rebuild on release, with outline previews while dragging. The town blockout was only a test and is excluded from this work.
- **Cliffs are one generated mesh each** (`Level_Cliff`), never a cube per block. The look matches the user's Cube & Seek reference exactly: chunky square block columns, stepped and jittered, soft pastel shade variation. Goblins can't walk on top (for now); cliffs are flow-field obstacles via the Building layer.
- **Goblins are prefabs and GameObjects with a Rigidbody root** for now, rendered by instancing (the meshes are stripped from the GameObjects). Head and body are sibling parts with separate scales.
- **Path to 20k goblins:** move to a data-only horde (NativeArrays, Burst movement, spatial-hash separation, custom ballistic flight, optionally a small pool of Rigidbody "stunt doubles"), then spatial-hash combat queries, culling plus `RenderMeshIndirect`, and LOD simulation. **Deferred (2026-11-04):** the three-day plan builds small armed squads on the current horde first; do the rewrite before shield tortoises or large squads.
- **Physics solver iterations are 2** by default (set per goblin type).
- **Sounds are `SoundData` assets**, not inline configs.
- **The flow field is rebuilt from scratch** (not ported from v1): baked map, frame-sliced solves, local repair, multiple fields from a pool.
