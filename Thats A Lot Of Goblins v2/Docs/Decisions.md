# Decisions

Things already settled, with the reason. Don't undo one without asking the user. Add new entries at the top of their section.

## Game design
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
- **Path to 20k goblins:** move to a data-only horde (NativeArrays, Burst movement, spatial-hash separation, custom ballistic flight, optionally a small pool of Rigidbody "stunt doubles"), then spatial-hash combat queries, culling plus `RenderMeshIndirect`, and LOD simulation. **Deferred (2026-11-04):** the three-day plan builds small armed squads on the current horde first; do the rewrite before shield tortoises or large squads.
- **Physics solver iterations are 2** by default (set per goblin type).
- **Sounds are `SoundData` assets**, not inline configs.
- **The flow field is rebuilt from scratch** (not ported from v1): baked map, frame-sliced solves, local repair, multiple fields from a pool.
