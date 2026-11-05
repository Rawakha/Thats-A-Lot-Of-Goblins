# Project State

Last updated: 2026-11-04 (Claude)

Read this first. It says what exists, what's being worked on, and what's next. Whoever finishes a piece of work updates it.

## Current module
**Direction change (2026-11-04): incremental; a skill tree replaces the pick-3 (the user explains its visuals next session). Next-session checklist, in order, one at a time:**
1. Play-test what's built (nothing since the results screen has been checked in Unity): start from Run and from an area, clear, fail, Restart, Next Area, map-limit respawn, loading cover, crosshair. Fix what's found.
2. Decide the rules: leaks per map or the run-wide pool (and health back), the fail line, whether stars (20 / 10 / 0) are in.
3. Flare starts each wave instead of Space (planned, not built; removes `UI_WavePrompt`'s Space text and the Ready press).
4. Make a second area with Goblins > Area > New Area (the tool is untested), with its own settings, and an ordered area list so Next Area goes to it (today it replays the same scene; the last area needs an end state).
5. Results screen: HUD slides out, stats panel slides in (pass or fail, leaks, kills, stars, gold).
6. Gold per kill, kept between maps and saved to disk (`Run_Carry` is memory only).
7. Skill tree screen on `Run_Edge.UpgradeHook`, as a shell first, once the user has explained the visuals. Add an Upgrades button on the results screen, including on a fail.
8. Update `CLAUDE.md` Game direction to match. Parked: spawn events, level select, weapon swapping.

**Current run design: `Docs/Run_Loop_Brief.md` (manager, 2026-11-04), a run-based pick-3 loop to test whether it's fun (flare starts
each area, spawn events, run-wide leak pool with half back, fly north with a ground indicator, a pick-3 shop at the fade, picks kept
in `Run_Carry`). It replaces the Day 2/3 plan and the Brotato-style notes below, and an earlier stars / incremental briefing that is
now only the parked pivot. Stage 1 (clean-up and the ground indicator) is built and waiting for the user's scene setup and
go-ahead. Stage 2 is next: spawn events and the flare in `Wave_Director` / `Wave_Settings`, removing the Ready phase and Space
start. Then the pick-3 shop (stage 3) and weapons in the pool (stage 4).** See the newest Handoff log entry.

**Where we are (2026-11-04):** the horde, weapons, HUD, aim, blood and dismemberment are built and in the user's hands; many older
notes below say "waiting for Unity verification"; treat those as unverified rather than blocking, and the newest Handoff log
entries as authoritative. The tower commits are from the old TD version (see Queue). The
user has posted early clips and wants to turn the prototype into a game. Read `Docs/Ideas.md` (unbuilt ideas) and
`Docs/Marketing.md` (posting notes, not for coding) only when relevant.

**Three-day plan (agreed 2026-11-04), in this order, one module at a time:**
- Day 1: tune the new gibs, blood and debris budget in play and profile at 1,500+ goblins; cheap clip impact (hitstop, explosion
  flash and camera kick, scorch marks); then plane health.
- Day 2, the run loop: armed goblin squads that shoot the plane, plane health and hit feedback, going down and the timed eject,
  finishing an area (fade to the next), win/lose. The goblins are passive today, so nothing can end a run.
- Day 3, progression: gold per kill, pick-3 cards (6-8 to start), weapon swapping (Q) with the Egg Launcher as the second
  weapon, a rough run summary. Leave out: fuel, shield tortoises, families, the meta tree, strands and bite holes.
- Decision gate before Day 2: check how the posted clips did; if people want more weapons and chaos, shift Day 3 toward weapons.

Scream text is available on the existing context message HUD. It follows the goblin's authored Mouth Position and pulses during tracked scream audio. Default cap is three. Unity visual and audio tuning are pending.

Flow grid authoring has Scene view movement and resize handles plus **Center Grid on Transform**. Run **Bake Obstacles** to apply edited bounds. Editor compilation passed. Scene view interaction still needs a Unity check.

**Project review fixes: implemented; Unity Play Mode verification pending.** Horde position caches now initialise on spawn, survive capacity growth and follow swap-back removal. HUD sway tolerates an empty registry. Wave advances include surviving goblins and verify the living count before clearing. Wave setup removes missing/disabled/duplicate holes before subscribing, requires two distinct usable holes, and guards holes becoming unavailable during spawning. See the newest handoff entry for validation and editor checks.

**Reusable visual variations + cyclops authoring: implemented, ready for the user's Unity setup/test.** On the existing Eyes component, press **Create Two Eyes / Cyclops Variation Group**. This wraps the existing Eyes object in an identity group, preserves its Y=1.2/card transforms, and creates a separate Cyclops option with independent central-card width/height, XYZ position and pitch/yaw/roll. Defaults are 85:15 weights. The generic Horde_VariationGroup inspector supports any direct-child visual options, Head/Body anchors, editable Chance (%) sliders that redistribute the remainder, option previews, Randomise Preview, Add Option and empty/None options. Preview visibility is independent of spawn choices. No prefab/scene was edited automatically this step. Runtime/editor C# compiled and weighted selection tests passed; editor interaction, prefab Undo, runtime batching/appearance and profiling still need Unity checks. Stop for feedback before moving on.

**Goblin eyes: shader and combined horde rendering implemented; waiting for Unity verification/tuning.** User placed Eyes beside Head at local Y = 1.2, component Height = 0; all authored transforms are preserved. New `Goblins/Eyes` shader/material draws white-rimmed dark-pupil eyes, shared asynchronous-per-goblin blinking/glances, controllable gaze, adjustable hurt squint, square-ended black death X's (including airborne deaths). The render bake combines the two source cards into one head-animated part per pair; the prefab keeps both editable GameObjects. Material assigned in `Prefabs/Goblin.prefab`. Tune `Goblin_Eyes.mat` (independent X stroke length/width) and `Horde_GoblinType` eye size/asymmetry controls; no reparenting needed. C# compilation passed; Unity batch validation was refused because this project is already open, so shader compilation, appearance, combining at runtime and profiling are still unverified. Stop here for the user's visual feedback.

**Level-design tool consistency: built, waiting for the user to try in Unity.** Ground and holes now follow the tree/cliff Scene-view workflow, with white movement squares, orange width/radius cubes, pink length cubes and yellow depth cones. Trees and cliffs share the same movement squares and palette. Ground/hole outlines preview drags; mesh/collider rebuilds wait for release. Town blockout is excluded at the user's request (it was only a test). No further queued gameplay module has been started.

**Carpet bomb / ability weapon:** built and working; the user tunes feel by hand. It no longer blocks the plan: start the three-day plan's Day 1 when the user says go.

**Also just built, waiting for the user to try: the cliff tool (`Level_Cliff`).** See the handoff log. Expect feedback on the look (it must match the user's Cube & Seek reference: chunky stacked square blocks, soft pastel shading) and on the handles.

Open question: the user has talked about "the missile weapon". The carpet bomb is the ability built so far; homing missiles (lock onto the densest clump) are **not started**. Ask before building them.

**Current (2026-11-02): ability belt + ability HUD, step 1 built, waiting for the user's Unity test.** Settled input: Q cycles weapons (not built; weapon switching waits until the general UI layout is done), E / gamepad left shoulder cycles abilities. Next steps for the ability HUD, not started: cooldown fill on every icon (vertical Image fill, from `Equip_Ability.CooldownProgress`), and the use kick (scale/position/rotation, like the ammo counter) on icon and text; Claude proposed moving the kick into a shared `UI_Punch`. Second weapon to test swapping: Claude suggested a Flak Cannon (not agreed yet).

## Queue (superseded by the three-day plan above; one module at a time, wait for "polished" between each)
Done since this queue was written: machine gun ammo and counter, `UI_Punch` springs, weapon HUD. Gold and the multiplier feed
are Day 3 (gold per kill) and later (the feed). **Fuel is dropped from the plan** (no job left with health as the fail state
and no wave timer); revisit only if the user wants it back. Weapon index dots are unscheduled.

**Towers are legacy:** the Tower Placement and Archer Tower commits are from the original tower-defence version of the game,
before the pivot to the plane. They are not part of the plane game or its plan; don't build on them.

**Open design question (user wants to come back to it, don't build yet): loadout and upgrades.**
- Leaning toward 3 swappable main weapons, each with a small upgrade tree (stat nodes, one branch choice, a capstone).
- **Abilities are consumables, not equipped slots (user's direction, 2026-11-02).** The "pick 1 of 3" cards offer ability charges (e.g. "2x Ice Wall Bomb": drop it and a wall of ice forms below the plane, blocking goblins). They're used up, so the player keeps cycling through different abilities instead of locking in one or two.
  - Still to settle: belt size, how to cycle and use them, whether charges carry between rounds, and anti-hoarding.
  - Ice wall needs dynamic flow-field obstacles (the cost overlay and local repair already exist).
- Suggested split: gold buys tree nodes between rounds; after-round "pick 1 of 3" cards give new items and passives.
- Undecided: do trees reset each run (roguelite) or stay permanent (incremental)? Claude suggested a hybrid: reset per run, with permanent unlocks of new branches.

**Open questions from the Brotato-style direction (2026-11-02).** Settled: a run ends when plane health runs out; a wave ends when all its goblins are gone (no timer); goblins reaching their goal cost nothing.
- Does fuel stay, and if so what for? (Claude suggested dropping it: no job left with health as the fail state and no wave timer.)
- Ejecting (settled: a timed button press as the plane goes down; a limited number of ejects per run). Start with 1, more from pick-3 cards. After a good eject: parachute down, then a replacement plane. Fine details (can goblins shoot the pilot, does the new plane keep upgrades and health) to settle when it's built.
- Are areas handmade, procedural, or handmade pieces shuffled?
- Data-only horde rewrite: **deferred** (2026-11-04 plan). Day 2 builds armed squads on the current Rigidbody horde with small squads only; the rewrite comes before shield tortoises or any 20k scale work.

Planned weapon (user, 2026-11-02): **Egg Launcher** (main weapon): lobs eggs in an arc that splat for small area damage, ~50% hatch a chicken that charges the nearest goblin and pecks (capped live count, lifetime; flung by bombs like goblins). Upgrade path ideas: Hatchery (hatch chance, twins, rooster taunt), Explosive eggs, Fowl army (chicks on death). Own message context ("FOWL PLAY"). Open: do goblins kill chickens later, hatch chance tuning.

Planned (user, 2026-11-02), not started:
- **Dragon's Breath** (weapon): flamethrower; short cone, sets goblins and ground on fire (burning spreads, panic-run). Fire message context.
- **Iced Up** (ability or weapon, TBD): freezes goblins into heavy ice blocks (physics bodies that can be flung/shattered; block lanes). Freeze context ("BRRRRR").
- **Tree Seeds** (ability): drops seeds that grow a permanent wall of trees, changing the map (flow-field obstacles). "Only one" (settled): only one tree wall on the map at a time; planting a new one removes the old (trees shrink away, flow field updates). Possibly also: the card is not offered while the player holds Tree Seeds.

Later, not scheduled: **goblin families and events** (user, 2026-11-02): goblins spawn and walk in family groups so dark lines can be literal ("WIPED OUT THE DURSLEYS" only when a whole family dies); special processions like a wedding (bride) or a funeral ("DEATH AT A FUNERAL"), each with its own message context. More contexts: Freeze ("BRRRRR"), fire, into a hole.

Later, not scheduled: homing missiles, roguelike upgrade picks between rounds, escaped goblins feeding "Goblins remaining", data-only horde for 20k goblins.

## What's built
**Added since the list below (2026-11-02 to 11-04; each has a Handoff log entry with details):**
- **Equipment:** `Equip_Ability` base and `Equip_AbilityBelt` (charges or infinite, E cycles, abilities keep ticking when not
  selected); Ice Wall and Napalm are empty placeholders; R reloads the machine gun early; the machine gun has a magazine.
- **HUD (`UI/`):** weapon panel (name, ammo, reload bar, shot and reload punches), ability panel (rows, selected/dimmed states,
  cooldown fill with a smooth drain, switch and use punches), circle crosshair on the cursor (reload fill, ability ring), enemy
  counter, context messages (`UI_ContextMessages` + `UI_MessageSet`: "BOOOOOM" and rare dark lines), merging spring hit markers,
  HUD sway (`UI_Sway*`). Shared UI tech: `UI_Shadowed` (text/image plus its shadow as one; Alt+Shift+S makes a shadow),
  `UI_Style` (dimmed look), `UI_Punch` (reusable springs), `UI_ScaleBaker`.
- **Aim:** `Weapon_Aim` compensates plane speed so shots land Strafe Distance ahead at any height, with a shorter Low Strafe
  Distance when diving; labelled gizmos per setting.
- **Effects:** `Effect_BloodSplats` (instanced, spring in/out, crowded-first removal, fade option), `Effect_BloodSpray` (custom
  droplet system, directional, with puffs), scream particles from v1, `Weapon_ShellEjector`.
- **Dismemberment and debris:** `Horde_GibSet` (baked body/head chunks with purple insides and X-eye heads), `Horde_Gibs`
  (heads pop on 50% of kills, scraps, blown-apart bursts; thud, squish, splat, age/sink), `Horde_DebrisBudget` (limits for
  corpses, gibs, splats; oldest-first at the cap; packed squares thinned), corpses dim, `Horde_GoblinAudio` (all goblin sounds in
  one asset, separate corpse / gib / blown-apart death sounds).
- **Level:** `Level_Grass` (painted chunky grass, wind), plus cliffs, tree areas and ground/hole tools.
- **Waves:** `Wave_Director` Debug Waves (hole-to-hole lists, Space starts/stops) for recording clips.
- **Attributes:** `[InspectorButton]` (now also on ScriptableObjects, with `ShowIf`), `ShowIf`, `ReadOnly`.

- **Plane** (`Plane/`): controller, input, visuals, effects, audio (engine/wind loops, turn whoosh), pilot. Tunables in `Resources/Controller/*.asset`.
- **Camera** (`Camera/`): rig with shake/kick, follow, settings asset.
- **Equipment** (`Equipment/`): `Equip_Loadout` ticks equipped items by slot (Primary = hold, Ability = press), with stats and tags.
- **Weapons** (`Weapons/`):
  - `Weapon_MachineGun`: fires along the aim yaw.
  - `Weapon_CarpetBomb`: Ability slot; drops a stick of 3 bombs; gizmos show predicted landings.
  - `Weapon_Aim`: aim assist and strafe; middle mouse / gamepad North toggles ground vs air targets; gizmos for the cone, range and limits.
- **Combat** (`Combat/`):
  - `Combat_Projectiles`: pooled projectiles with sphere-cast hits; explosions go through `Combat_Damage.ApplyExplosion`; blast gizmos.
  - Damage, explosion and target interfaces.
- **Horde** (`Horde/`):
  - Goblins are Rigidbody GameObjects, pooled, registered in `Horde_System`.
  - Walking is steered by the flow field.
  - Rendering is instanced through `Horde_RenderGroup`, animated in Burst (`Horde_Animation`: hop, lean, head spring, hit flash).
  - Variation (size, shape, colour) is set per `Horde_GoblinType` (`Resources/Horde/Default Goblin.asset`).
  - Deaths: on the ground they go kinematic, tip over, then shrink. Killed in the air, they wait until at rest.
  - Explosions: the inner ring bursts goblins; the outer ring flings them with real Rigidbody physics plus a v1-style launch torque and a scream. They get up when they land.
- **Flow field** (`Flow/`): baked `Flow_Map`, multiple goals, Dijkstra with a binary heap, solves frame-sliced, Burst jobs, timing stats.
- **Waves** (`Waves/`):
  - `Wave_Director` handles the Ready phase and Next Wave (Space / gamepad South).
  - `Wave_Hole`: goblins hop out, walk to a target hole at least the minimum distance away, and fall in.
  - `Wave_Settings` is the tunable SO.
- **Level tools** (`Level/`): `Level_Ground` mesh with hole openings (live editor rebuild); `Level_Hole`; triangulator; `GameObject/Level` menu; town blockout tool.
  - `Level_Cliff` (+ `Editor/Level_CliffEditor`): cliffs from nodes, shapes Line / Block / Ring (map edge). Each is one mesh of chunky block columns (seeded per edge), optional tiers, vertex-colour shading, sides + tops submeshes (tops get grass later), MeshCollider. Scene handles move nodes and set height and width; green dots add nodes, Ctrl-click deletes. Shader `Goblins/Level` (`Art/Level/Level.shader`); the menu makes `Art/Level/Cliff_Mat.mat` and puts cliffs on the Building layer, which `Flow_System` bakes as obstacles.
- **Destruction** (`Destruction/`): fracture, pieces with support collapse, system.
- **UI** (`UI/`): pooled hit markers with hit/kill sprites and sounds.
- **Audio**: all plane/weapon/goblin sound slots are `SoundData` assets (`Art/SFX`, `Art/SFX/Plane`). The destruction sounds are still inline `SoundConfig`s.

Performance at ~1,500 goblins: movement ~1.5 ms, rendering ~0.3 ms, Physics.Simulate ~1.9 ms (2 solver iterations).

## Editor setup the user still needs to do
- `GoblinAirborne` physics layer + collision matrix (if not done yet).
- Assign Sound Data to the empty slots as wanted: Turn Whoosh, Machine Gun Release, Bomb Drop, Bullet Impact, Goblin Burst, Goblin Scream.
- Pool sizes per goblin type and projectile capacity to suit wave sizes.
- Input: gamepad South is bound to both Ability and Next Wave (same clash Space had). The user hasn't decided whether to remove it from Ability.

## Known issues / notes
- Unity prefabs open in prefab mode can overwrite file changes made outside the editor; close them before agents edit prefab YAML.
- Goblins flung on top of a cliff will get up there and be stuck (the flow field marks it blocked). Not handled yet; the user said goblins can't walk on top for now.
- Physics.Simulate occasionally spikes to ~7 ms with lots of flung goblins.

## Handoff log
Newest first. One short entry per module or session: what changed, files, editor steps, untested bits, new allocations/subscriptions.

- **2026-11-04 (Claude)**: Fade In Time and Fade Out Time moved from `Run_Edge` to `UI_Fade` (FadeIn/FadeOut). `UI_Crosshair` Show Ability Ring, off by default: the ring image is a whole circle in the No Ability colour. Not checked in Unity.

- **2026-11-04 (Claude)**: `Run_State` reloads the Run scene with `LoadSceneAsync` instead of `LoadScene`, so the loading dots keep moving while it loads. Scene activation (Awakes, pool prewarm) still blocks briefly. Not checked in Unity.

- **2026-11-04 (Claude)**: `UI_Fade` now fades a Canvas Group (added if missing) instead of the Image's alpha, and animates "Loading." to "Loading..." in Loading Content (Loading Line, Dot Interval); the text fades with the cover. OnLateUpdate only while fading or loading. Strings built once. Not checked in Unity.

- **2026-11-04 (Claude)**: Loading cover. `UI_Fade` starts covered on Awake and has `Cover()` and optional Loading Content; `Run_Boot.Loading` (static) holds the fade-in in `Run_Edge` until the area has loaded. Restart and Next Area hide the panel and cover at once before the reload. Editor steps: match the Fade colour to the Game Over background, add a LOADING child, assign it. Not checked in Unity. "Ready" means the scene load finished plus Black Time; flow-field solves aren't waited on. No new subscriptions.

- **2026-11-04 (Claude)**: When the results screen appears, `UI_GameOver` calls new `Plane_Controller.Stop` (plane freezes in the air, weapons stop ticking) and `Plane_Audio.FadeOut` (Fade Out Time; loops returned at zero). Both are found from the Plane Input slot, so it must be assigned. Not checked in Unity. No new subscriptions or per-frame allocations.

- **2026-11-04 (Claude)**: Results screen replaces the square and gates. Deleted `Area_Gates`, `UI_GateBars` and the square in `Run_Gates` (now just the red limit line). `UI_GameOver` handles pass and fail: title lists, message lists with {0}, `Area_Settings.failMessages`, Next Area (pass only) and Restart. `Run_Edge.GoToNextArea` does the fade, hook and reload. `Plane_Input.SetWeaponsLocked` while the screen is up. Editor steps: delete Missing components and the old bars; on UI_GameOver assign Edge, Plane Input, Title, Message, Next Button. Not checked in Unity. Subscriptions: UI_GameOver RunFailed, AreaCleared. Flare not built.

- **2026-11-04 (Claude)**: Transition square replaces world gates. New `Area/Area_Gates.cs` (centre, size, handles, checklist Create), `UI/UI_GateBars.cs` (top and bottom bars, direct UI_SlidePanel because they show together). `Run_Gates` rewritten: dotted LineRenderers just above the floor, the square after a clear and a red warning line that fades in by distance. `Run_Edge`: square replaces map limits after a clear; top = fade, hook, next area; bottom = fade, `Run_State.Restart` (clears the carry, so a whole new run); left, right, or a clear outside the square = steer back to the middle until Pull Back Inset inside. `GateTaken` event added; preview buttons gone (gizmo and handles instead). Editor steps: checklist Create; Run_Gates Plane and Line Material; delete the old world label; add UI_GateBars and two panels. Not checked in Unity. Subscriptions: Run_Gates OnLateUpdate and Area_Root.Changed; UI_GateBars AreaCleared and GateTaken. Allocations: one texture and two line objects at startup. The stats panel and flare are not built.

- **2026-11-04 (Claude)**: `Run_Gates` authoring preview: buttons Preview / Apply Changes and Hide Preview draw unsaved copies of the gates around Preview At (or the object), in or out of play. A preview mesh and copies are made per press; nothing at runtime unless pressed. Not checked in Unity.

- **2026-11-04 (Claude)**: Gates reworked into fly-through lines; the stopped camera is gone. `Run_Gates` rewritten: a square (Size, Height) around the clear point, kept inside the warning line (`Area_Bounds.KeepInside`), each listed gate a world-aligned line with its label north of it. Removed: chevron, `Camera_Rig` framing freeze, `Plane_HeightLine.GroundPoint`, offscreen and fly-back-from-bottom code in `Run_Edge`. Crossing a line runs `BeginExit` (hold, fly on, fade, hook, reload). Editor steps: check Line Material and the label on Run_Gates; tune Height above obstacles but below cruise; overlay TMP material optional. Compiled; not checked in Unity. Allocations and subscriptions unchanged from the last gates entry. Flare checkpoint still not built.

- **2026-11-04 (Claude)**: World gates replace the exit bar. Deleted `UI/UI_ExitBar.cs`, `Area_Bounds.PastNorth`, `Run_Edge.ExitStarted/ExitOpen`. New `Run/Run_Gates.cs` (gate list: side, world TMP label, text, offset; signs flat at `Plane_HeightLine.GroundPoint`, spring pop then still, one static chevron beyond the text). `Run_Edge`: on clear (deferred until Playing if mid-respawn) `Camera_Rig.Hold(coast, freezeFraming)` and opens gates, map limits off; off screen through a gate = fade, hook, reload; other sides = fade and fly in from below the stopped view, control back at Return Control At. Editor steps: delete the exit bar and Area Clear Screen objects; add Run_Gates (Height Line, chevron material, gate label) and assign it on Run_Edge, which logs an error and disables itself without it. Compile result in the session; not checked in Unity. Allocations: one mesh and a renderer object per gate at startup. Subscriptions: Run_Gates OnLateUpdate only while popping. Flare checkpoint still not built.

- **2026-11-04 (Claude)**: The user didn't like the chevron look, so `Run/Run_ExitIndicator.cs` is deleted (with `Area_Bounds.NorthEdge`; `Plane_HeightLine.GroundPoint` stays, unused). New `UI/UI_ExitBar.cs`: a bar that slides in from the top a moment after the clear ("CONTINUE TO NEXT AREA", priority 50 prompt, pop and pulse on landing), leaves for good when `Run_Edge.ExitStarted` fires. Editor steps: delete any Run_ExitIndicator object, its material and 3D text label; the old "Area Clear Screen" panel can be reused for the bar (remove the Missing component, set the Panel's UI_SlidePanel Side to Top, add UI_ExitBar to the HUD, assign Run State, Run Edge, Panel, Text, Punch). An in-world marker is discussed, not built. Compiled; not checked in Unity. Subscriptions: UI_ExitBar (Run_State.AreaCleared, Run_Edge.ExitStarted, prompt Landed). Flare checkpoint 2 still not built.

- **2026-11-04 (Claude)**: Exit indicator reworked after the user's feedback (too big, and it swung round the plane's heading). `Run_ExitIndicator` now grows from the ground spot the plane's height line lands on (new `Plane_HeightLine.GroundPoint`, assign the plane's height line on it), smaller (3 chevrons, thin arms, 55% alpha), marches outward along a direction that turns smoothly round that spot (Aim: North, or Exit Point = the middle of the north edge, via new `Area_Bounds.NorthEdge`; Turn Speed). The label stays flat and upright at the far end. Tuning fields were renamed (chevrons, startOffset, gap, chevronWidth, chevronLength, armThickness, marchSpeed, tint), so their new defaults apply; assignments are kept. Compiled; not checked in Unity. The flare / wave-start checkpoint 2 is planned (waves stay, a flare starts each one) but NOT built; wait for the user's go.

- **2026-11-04 (Claude)**: Run loop brief, stage 1 (clean-up and indicator). The manager's `Run_Loop_Brief.md` replaced the stars briefing, so the pieces removed under that one were rebuilt: `Run_State` (health pool, health back deferred to arrival, `RestoreHealth`, `SaveCarry`, `LoadNextArea`, carry read on enable), `Run_Carry` (Has, Leaks, Limit, PendingBack, AreaPath), `Area_Settings.healthBack`, `Area_Bounds.PastNorth`, `UI_LeakCounter` health-back pop, and `Run_Edge`'s north exit (exit opens on clear, no warning at the north edge, control taken, camera held, fly on, fade, `UpgradeHook` on black, reload with the carry, health restored as the next area fades in; `ExitStarted` and `ExitOpen` exposed). Still removed (the brief agrees): `UI_AreaCleared`, `UI_DirectionVignette`. New `Run/Run_ExitIndicator.cs`: chevrons (one generated mesh, `Graphics.DrawMesh`, property-block tints) on the ground ahead of the plane's nose, pointing north and marching, fading in a moment after the clear and out as the plane leaves, with a "[Next Area]" world-space TextMeshPro label (position, scale pulse and alpha set by the script); needs an Unlit transparent material and the label assigned, gizmo shows the placement. `CLAUDE.md` and `Decisions.md` rewritten to the brief. Editor steps: assign Run State on Run Edge, make the material and the 3D TMP text, add Run_ExitIndicator and assign Run State, Run Edge, Plane, Material, Label; delete the "Area Clear Screen" object if still there. Compiled runtime and editor separately; not checked in Unity. Subscriptions: indicator (Run_State.AreaCleared, Run_Edge.ExitStarted, OnLateUpdate while visible), Run_Edge (Run_State.AreaCleared). Allocations: one mesh and eight property blocks at startup. Next: stage 2 (flare and spawn events).

- **2026-11-04 (Claude)**: Run design change, stage 1: removed the old run loop. Deleted `UI/UI_AreaCleared.cs` and `UI/UI_DirectionVignette.cs` (with their .meta). `Run_Edge`: all of the north-edge exit (exit open, BeginExit, Hook/Loading steps, `UpgradeHook`, `ExitStarted`, the `Run State` field); the map-limit warning, respawn, fly-in and arrival from black stay. `Area_Bounds.PastNorth` removed. `Run_State`: no health pool, health back or carry (`RestoreHealth`, `SaveCarry`, `LoadNextArea`, `HealthRestored`, `LastHealthBack` gone); leaks, the limit and fail reset on each map bind; Restart stays. `Run_Carry` now only holds the map path (`Run_Boot` still reads it). `Area_Settings.healthBack` removed; `startingHealth` is the interim leak limit. `UI_LeakCounter`: health-back pop removed. `CLAUDE.md` and `Decisions.md` rewritten for the new design. Compiled runtime and editor separately; not run in Unity. Scene cleanup is the user's (see the stage 1 reply). Now, clearing a map does nothing visible until stage 3 (the results screen). Subscriptions removed: Run_Edge's AreaCleared, the vignette's three. No new allocations or subscriptions.

- **2026-11-04 (Claude)**: Stage 4, checkpoint 3: clear, fly north, next area (built, untested). The area-cleared banner is now one bottom prompt "GOBLINS CLEARED - FLY NORTH" (`UI_AreaCleared` rewritten; health text fields gone). New `UI/UI_DirectionVignette.cs` (top glow, generated gradient). `Run_Edge`: needs a `Run State` assigned; once cleared the north edge (`Area_Bounds.PastNorth`) starts the exit with no warning (lock, camera hold, fly on, fade), then on black `UpgradeHook(Continue)` (the pick-3 hook; null passes through), then `Run_State.SaveCarry` + `LoadNextArea` reload the Run scene; the next area enters from black and `RestoreHealth` fires as it fades in (counter pop in `UI_LeakCounter`, now on `HealthRestored`). New `Run/Run_Carry.cs` (static: leaks, limit, pending health, next area path; Restart clears; `Run_Boot` loads its path). `Run_State`: health back is deferred, `LastHealthBack` set on restore. Decision recorded: areas reload the Run scene (no wipe systems exist). Not done: shop hook, real next-area choice (same area for now), debris wipe in place. Subscriptions added: UI_DirectionVignette (AreaCleared, ExitStarted, OnLateUpdate while visible), Run_Edge (Run_State.AreaCleared). One-off allocations: vignette texture and sprite. Compiled separately; not checked in Unity.

- **2026-11-04 (Claude)**: Prompts hide during the fade. `UI_Prompts.Suppress(bool)` (counted): while on, every prompt slides away and keeps what it wants; they return when it's released. `Run_Edge` suppresses from the respawn line (before the warning leaves, so the wave prompt can't swap in) or arrival until control returns, and releases on disable. Compiled; not checked in Unity.

- **2026-11-04 (Claude)**: Prompt system. New `UI/UI_Prompt.cs` (on a panel: priority, SetWanted, Landed event, Swap Wait) and `UI/UI_Prompts.cs` (static: shows the highest priority wanted prompt, newest among equals; displaced ones slide out and return). `UI_WavePrompt` (10), `UI_AreaCleared` (50), `UI_EdgeWarning` (100) now use it (they add UI_Prompt to their Panel if missing; pop/pulse happen on Landed, so they replay when a prompt comes back). `UI_SlidePanel` exposes InTime/OutTime. Game Over untouched. Compiled separately; not checked in Unity. Static state resets at SubsystemRegistration.

- **2026-11-04 (Claude)**: Fix: Play from an area scene ran the area alone (errors: no UpdateManager, no Horde_System). Cause: Build Settings index 0 was a scene that no longer exists ("Sample Scene", the renamed original), so `Area_PlayMode` found no Run scene. New `Area/Editor/Area_BuildSettings.cs` (Goblins > Area > Fix Build Settings, and a Fix button on the Area_Root checklist: Run first, missing scenes dropped); `Area_PlayMode` now falls back to the scene named "Run". UI_Shadowed.SetVisible added earlier for the health text. Compiled; not checked in Unity. The user's Build Settings still need the fix (Run is not in them).

- **2026-11-04 (Claude)**: Prompts slide. New `UI/UI_SlidePanel.cs` (top or bottom, Show/Hide/HideNow, DOTween unscaled; the standard for all UI prompts). `UI_AreaCleared` slides in, punches on landing, holds (Hold Time 2.5 s), slides out (Canvas Group fade removed). `UI_WavePrompt` slides, and pops as it leaves on Space. `UI_EdgeWarning` now takes a Panel and slides (the amount-based alpha and Text field are gone). Game Over not changed. Compiled separately; not checked in Unity. One small DOTween allocation per slide.

- **2026-11-04 (Claude)**: Stage 4, checkpoint 2: map edges and fly-in. Scene split confirmed working by the user. New `Run/Run_Edge.cs` (warning amount, then lock, camera hold, fly off, fade, teleport, fly in, camera catch, unlock; every area also enters this way from black; `EdgeCrossed` is the hook for shoot-downs), `Area/Area_Bounds.cs`, `Area/Area_Spawn.cs`, `Area/Editor/Area_BoundsEditor.cs` (handles for both; checklist Create buttons on Area_Root), `UI/UI_Fade.cs`, `UI/UI_EdgeWarning.cs`. `Plane_Input.Lock/Unlock` blanks all flight and weapon input; `Plane_Controller.Teleport` + Teleported (Plane_Effects clears trails); `Camera_Rig.Hold/HoldAt/CatchUp`; `Wave_Director.InputLocked` blocks Next Wave. Goblins keep going during the sequence. Editor steps: Create on the checklist, size edges, Run_Edge + fade + warning in the Run scene. Compiled separately; not checked in Unity. Subscriptions: Run_Edge OnUpdate + Area_Root.Changed; UI_Fade OnLateUpdate while fading; UI_EdgeWarning WarningChanged; Plane_Effects Teleported. No per-frame allocations. Next: checkpoint 3 (clear, fly north, next area).

- **2026-11-04 (Claude)**: Stage 4, checkpoint 1: scene split (code done, the user does the scene work). New `Run/Run_Boot.cs` (Run scene: loads the first area additively and sets it active; skips if an area is already open), `Area/Editor/Area_PlayMode.cs` (Play from any open area starts in the Run scene = Build Settings index 0, via `playModeStartScene` + SessionState key). `Area_Root`: static `Changed(root or null)`. `Run_State` rewritten to bind to each area (no Awake; health carries over, the first area sets it; `LimitChanged` event; `Restart` reloads the Run scene by build index). `UI_EnemyCounter` (director field removed) and `UI_WavePrompt` bind via `Area_Root.Changed`; `UI_LeakCounter` listens to LimitChanged. Area_RootEditor checklist: Run scene first and area in Build Settings. Compiles; not checked in Unity. Next: checkpoint 2 (map edges, Area_Spawn, plane autopilot override, camera clamp, UI_Fade, respawn), then checkpoint 3 (clear, fly north, next area). Subscriptions: each of Run_State, UI_EnemyCounter, UI_WavePrompt adds one to Area_Root.Changed plus the current director's events.

- **2026-11-04 (Claude)**: Removed the banner's Again button and Hide On Show from `UI_AreaCleared` (after a clear nothing happens until stage 4's exit). New `UI/UI_WavePrompt.cs`: "PRESS SPACE FOR WAVE 2" on Wave_Director.WaitingForInput, hides on WaveStarted, pop and pulse. Shows the action's first binding only (no gamepad swap yet). Editor steps: delete the banner's button, make the prompt text. Compiled separately; not checked in Unity. Upgrade timing (pick-3 vs per-wave) under discussion, not settled.

- **2026-11-04 (Claude)**: Run loop stage 3: areas. New `Area/Area_Settings.cs` (name, waves, starting health, health back), `Area/Area_Root.cs` (on the Wave_Director's object; `Current` static), `Area/Editor/Area_RootEditor.cs` (checklist with fixes, Play This Area, nested settings), `Area/Editor/Area_NewWindow.cs` (Goblins > Area > New Area copies a scene; no separate template until the scene split), `UI/UI_AreaCleared.cs` (banner, Play Again button). `Wave_Director`: settings now come from Area_Root (serialized field removed), new AreaCleared event. `Wave_Settings.leakLimit` removed. `Run_State`: no director field, finds Area_Root.Current; health back on clear; `Restart()` moved here. `UI_LeakCounter`: delayed green refill flash and punch. Hand-written `Assets/Areas/SampleScene/SampleScene Area.asset` links the existing Wave Settings. Editor steps: add Area_Root to the Wave_Director object, assign that asset, set Last Wave (checklist button), Debug Waves off to test, build the banner panel. Compiled separately from Unity; not checked in Unity, and the New Area window is untested. Subscriptions: Run_State director AreaCleared; UI_AreaCleared Run_State.AreaCleared; UI_LeakCounter Run_State.AreaCleared. No per-frame allocations.

- **2026-11-04 (Claude)**: New `UI/UI_Button.cs` on a Unity Button (its colour tint is switched off): hover springs up, punches, starts an idle rock and pulse; press squashes; click punches and plays a sound; hover/pad select fades the TMP text's own colour (through UI_Shadowed if present) and back. Hover and click SoundData slots. `UI_Punch` gained `SetIdle` and Unscaled Time. The moving part must be a child, not the hit area. Not checked in Unity. Subscriptions: one onClick; OnLateUpdate only while the colour fades or the punch moves/idles. Possible issue: a Restart click sound may be cut off by the scene reload.

- **2026-11-04 (Claude)**: Restart + leak fix (run loop stage 2, first pass). `Run_State` Count Debug Waves (default on): the user's scene has Debug Waves on, which stage 1 had excluded, so no leaks counted; turn it off to record clips (Decisions' "debug waves never count" now means this toggle). `Wave_Director.StartDebugWave` ignores calls once Stopped (Space could restart spawning after a fail). `UI_GameOver`: Restart Button + optional Restart Action (polled on OnUpdate only while the panel is up), Restart Delay 0.5 s, Hide On Game Over objects (put the crosshair there so the system cursor returns), Restart = DOTween.KillAll + reload the active scene (SampleScene is in Build Settings). `UpdateManager` is DontDestroyOnLoad and persists across the reload; everything else is scene-owned. Not checked in Unity: restart 5x and watch the debris readout, pools, leak count, no duplicate-instance errors.

- **2026-11-04 (Claude)**: Counter icons. `UI_EnemyCounter` and `UI_LeakCounter` take an optional Icon (UI_Shadowed) that flashes in step with the number (the leak icon also slides to Danger Colour) and an optional Icon Punch (UI_Punch) kicked with the number. No new subscriptions or allocations. Compiles; not checked in Unity.

- **2026-11-04 (Claude)**: Run loop stage 1. New `Run/Run_State.cs` (leak pool from new `Wave_Settings.leakLimit`, default 20, 0 = none; Debug Waves never count; Add Leak button; at the limit calls `Wave_Director.StopWaves` and fires RunFailed). `Wave_Director`: new Stopped phase and `StopWaves` (goblins already out keep walking but can't leak). New `UI/UI_LeakCounter.cs` (one number: health left, counting down to 0; punch per frame of leaks, flash, resting colour slides to danger) and `UI/UI_GameOver.cs` (panel shell, delayed unscaled fade, title punch, no restart yet; the plane still flies behind it). Editor steps: Run_State object, counter text + UI_Shadowed + UI_Punch, Game Over panel + Canvas Group, assignments. Limit moves to `Area_Settings` in stage 3. Not checked in Unity. Subscriptions: Run_State one director event; UI_LeakCounter OnLateUpdate + LeaksChanged; UI_GameOver RunFailed. Allocations: one DOTween on showing Game Over.

- **2026-11-03 (Codex)**: Added mouth-following scream labels to `UI_ContextMessages` using the existing pooled message prefab and LateUpdate. `Horde_System` starts labels for tracked screams and releases them on landing, death or return to the pool. `AudioManager.IsTrackedPlaying` also lets labels fade when a clip ends naturally. Defaults are AAAAAA!, three labels, and 0.25 s between punches with staggered starts. Labels need an assigned goblin scream sound and the existing context message HUD. Tune under Screams in its inspector. Runtime and editor compilation passed. Check mouth alignment, pulse strength and release in Unity. No new runtime allocations or subscriptions.

- **2026-11-03 (Codex)**: Added `Flow/Editor/Flow_SystemEditor.cs`. The white square moves the world XZ grid. Orange and pink cubes resize around its center in whole cells. The inspector button aligns the grid center with the system's world position. Edits support Undo through serialized properties. Existing bake and preview buttons remain available. Editor compilation passed. Test dragging, Undo and rebaking in Unity. No runtime allocations or subscriptions.

- **2026-11-04 (Claude)**: Packed spots thin out. `Horde_DebrisBudget`: Crowd Cell, Max Gibs Per Cell 12, Max Corpses Per Cell 5, Thin After 4 s, Thin Per Second 40. New `Horde/Horde_CrowdGrid.cs`. Over a square's limit, its oldest leave (oldest first across the map, never younger than Thin After). `Horde_Gibs` moved from a ring to a free list plus an age-ordered linked list; red gizmos on over-limit pieces when selected. Compiles; not checked in Unity.

- **2026-11-04 (Claude)**: Goblin sounds moved to `Horde/Horde_GoblinAudio.cs` (SO, assigned on Horde_GoblinType.audio; `Resources/Horde/Default Goblin Audio.asset` made with the existing sounds, hand-written YAML). New Corpse / Gib / Blown Apart death sounds, empty until assigned; capped by Horde_System Max Death Sounds Per Frame. Scrap kills count as corpse deaths; head pops as gib deaths (set by Horde_Gibs via `DiedInPieces`). Compiles; not checked in Unity.

- **2026-11-04 (Claude)**: Gibs age on the ground (`Horde_Gibs` Ageing): over Age Time they dim/grey, sink and flatten, to cut clutter; they only become cached Settled pieces after that. Compiles; not checked in Unity.

- **2026-11-04 (Claude)**: Corpses dim. `Horde_AnimateJob` lerps a dead goblin's tint toward Corpse Brightness / Corpse Grey over Corpse Dim Time (`Horde_GoblinType`), from `LiveColour` saved in Kill. Air kills dim once they've settled. Eyes unaffected. Compiles; not checked in Unity.

- **2026-11-04 (Claude)**: Debris budget. New `Horde/Horde_DebrisBudget.cs` (SO, Resources/Horde/Debris Budget, code defaults without it): quality scale, Max Corpses 300, Max Gibs 3000, Max Splats 2048, lifetimes (0 = no timer). Corpses (`Horde_System`) lie until the cap, oldest shrink first (`Horde_RenderGroup.StartShrink`); Prewarm adds Max Corpses to the pool; `Horde_GoblinType.deathShrinkDelay` removed. `Horde_Gibs`: no timer by default, oldest first at the cap; settled pieces move to per-mesh StaticChunks with cached draw data. Splats' max/lifetime moved to the budget (crowded-first unchanged: gibs and corpses deliberately aren't, they're the massacre). New `Horde/Horde_DebrisReadout.cs` (OnGUI, dev only). Compiles; NOT profiled yet: check 1,500+ goblins with full budgets. Corpses are still full goblins (movement/animation jobs, Rigidbody); if costly, convert settled corpses to lightweight instanced props. Open: wipe on area change (no areas yet), dimming corpses.

- **2026-11-04 (Claude)**: Dismemberment, first pass (flying pieces). New `Horde/Horde_Gibs.cs` (set + Goblins/Gib material): on Horde_System.Killed, Head Pop Chance (50%, every kill) hides the head (`Horde_RenderGroup.HideHead`, new GetAnchors/ColourAt/HideAll) and throws the whole head; else 1-2 scraps from the body while it topples; DeathBurst goblins hide entirely and throw all chunks up and away (biggest only on busy frames). Pieces: fixed pool, flat-floor gravity (pass through cliffs), low bounce, world-space squish spring on impact, skid, splat on first impact, spring out after Lifetime or near Max Gibs; instanced per mesh with per-instance _SkinColor/_Seed/_WobbleAmount (wobble in flight only).
  - `Horde_GibSet`: Unevenness (Impact-mode cuts: big piece + scraps), chunks in the anchor's space and sorted biggest first, baked Head Whole with X eyes at Eye_Left/Eye_Right (vertex green); cyclops goblins also get two X's. **Must re-bake.**
  - Still planned: stretchy strands, stumps staying up with a neck fountain, bite holes in living goblins; later pass: wet thud sound, resting wobble.
  - Compiles; not checked in Unity.

- **2026-11-03 (Claude)**: Dismemberment, step 1 (bake + blood); gibs not yet flying.
  - Agreed spec: purple gore (not red); 50% chance on every kill the head pops off whole (X eyes), else 1-2 body chunks tear out; bomb kill-ring goblins burst into all their body + head chunks; chunks are fractured Body/Head with purple insides, rounded so they read soft, wobbling and stretched along their flight; blood visible from the plane.
  - New `Horde/Horde_GibSet.cs` (SO, Goblins/Gib Set): Bake cuts the goblin prefab's Body and Head meshes with Destruct_FractureCore (Body Pieces 4, Head Pieces 3), rounds each chunk (Roundness), smooth normals, cut faces red in vertex colours; meshes saved inside the asset with each chunk's centre; keeps the whole head mesh.
  - New shader `Art/Characters/Goblins/Gibs/Gib.shader` (Goblins/Gib): per-instance _SkinColor/_Seed, cut faces in Gore / Gore Deep colour, jelly wobble.
  - New `Effects/Effect_BloodSpray.cs` + shader `Art/Shaders/Blood/Blood Droplet.shader`: custom code particles (no ParticleSystem), fixed pool, gravity/drag, instanced chunky droplets stretched along velocity, each goblin death sprays (Per Death), landings make small splats via `Effect_BloodSplats.AddSplat` (new static Instance). `TrySpray(point, dir, count, strength)` for anything else. Per-frame emit and splat caps.
  - Blood Splat shader defaults now purple (existing materials keep their colour).
  - Next: the runtime gib system (pooled instanced chunks with simple physics, per-goblin skin colour), hiding a goblin's head per instance in Horde_RenderGroup for head pops, and hooking machine-gun and bomb deaths.
  - Compiles; shaders and Bake not checked in Unity.

- **2026-11-03 (Claude)**: Painted grass (Cube & Seek style, squarish blades), wind only.
  - New `Level/Level_Grass.cs` ([ExecuteAlways]): clumps of rectangular, tapered, leaning blades with slanted tops (generated mesh, flat-shaded, Clump Shape settings). Clumps stored on the component (local positions, yaw, size, height) packed into one byte[]; drawn GPU instanced in spatial chunks (Chunk Size, split at 1023) with per-camera frustum culling via RenderPipelineManager.beginCameraRendering (edit mode too). Clear All Grass button.
  - New `Level/Editor/Level_GrassEditor.cs`: Paint Grass toggle (or P), left drag paints (fills to Density), Shift erases, [ ] resize; rays drop each clump onto Paint Layers, skipping surfaces steeper than Flatness (cliff sides). One undo per stroke (Reload on undo).
  - New shader `Art/Level/Grass/Grass.shader` (Goblins/Grass): base-to-tip gradient (purple defaults), per-clump shade variation, soft lighting, wind waves + flutter on the upper blade, DepthOnly pass. Colours/wind on the material.
  - `GameObject > Level > Grass` creates it and makes/assigns `Art/Level/Grass/Grass_Mat.mat`.
  - Visual only (goblins aren't affected); paint low Height where goblins walk. Compiles; shader and painting not checked in Unity.

- **2026-11-03 (Claude)**: Blood splats. New `Effects/Effect_BloodSplats.cs` + shader `Art/Shaders/Blood/Blood Splat.shader` (Goblins/Blood Splat): on Horde_System.Killed, a big blob + 2-5 droplets on the ground, popping in with an overshoot; air kills splat when they come down within Landed Height (dropped after Max Wait or if despawned/down a hole). Ring buffer of Max Splats (2048) flat quads drawn with RenderMeshInstanced (1023 per call), no GameObjects; the shader draws the blob (wobbly edge, darker rim, per-splat shade from a position hash), unlit, depth offset, no shadows. Setup: material on the shader, assign it, add the component. Compiles; shader not compiled in Unity yet.

- **2026-11-03 (Claude)**: Aim/assist gizmos rebuilt for authoring. `Weapon_Aim` draws each setting where it acts, each its own colour and labelled with name and value (new `Debug_Gizmos.Label`, editor only): assist range arc + on-ground reach, angle cone, max yaw, max pitch and the blind spot under the plane, strength/response; strafe landing point now (real shot path in play), cruise/low strafe marks, where Max Strafe Pitch limits it; in play the real shot point, the assist bend and the target with its sticky radius. Toggles: Draw Assist / Strafe / Live / Labels. Assist fields hide when Assist is off (ShowIf). Future plan: weapon-level aim settings move into a per-weapon `Weapon_AimProfile` (strafe, assist), with shot speed/inherit velocity read from the weapon's stats, when the second weapon arrives. Compiles; not checked in Unity.

- **2026-11-03 (Claude)**: `Weapon_Aim` Low Strafe Distance (10 m): the ground-mode strafe distance fades from Strafe Distance at cruise height to this at the low-flight height (Plane_Settings heights), so diving shrinks the unhittable gap in front of the plane. Gizmo uses the current one in play. Compiles; not checked in Unity.

- **2026-11-03 (Claude)**: Strafe aim compensates plane speed. `Weapon_Aim` Compensate Speed (on): the ground-mode gun pitch is solved from the shot's real velocity (gun + inherited forward and vertical plane speed) so shots land Strafe Distance ahead at any height and while diving (before: barrels pointed there, so forward speed carried shots ~25 m at both heights and diving pulled them short). At cruise (15 m, 32 m/s, 18 m strafe) it needs ~50 degrees, above the scene's Max Strafe Pitch 45 (clamps to ~21 m): raise it to ~55. Low-flight weapon benefits are meant for specific weapons, not the machine gun. Compiles; not checked in Unity.

- **2026-11-03 (Claude)**: Debug Waves toggle. `Wave_Director` Debug Waves bool; the debug settings are grouped in `Wave_DebugWave` (spawns, interval, on play) shown only when it's on (ShowIf on the group, since ShowIf on an array only hides its elements). With it on, normal waves don't start on play and Next Wave (Space) toggles Start/Stop Debug Wave. `InspectorButtonAttribute` gained `ShowIf = nameof(bool)` (InspectorButtonEditor hides the button while false); the debug buttons use it. The earlier debug fields were regrouped, so any values set in them need re-entering. Compiles; not checked in Unity.

- **2026-11-03 (Claude)**: Debug waves for recording clips. `Wave_Director` Debug Wave header: Debug Spawns (`Wave_DebugSpawn`: spawn hole, target hole, count, 0 = endless), Debug Interval (0 = settings), Debug On Play; buttons Start / Stop Debug Wave. Replaces the normal wave sequence; endless lines keep spawning until Stop; a finite debug wave goes Idle when cleared. Sources now carry their own Interval and an Endless flag; endless goblins aren't counted in ToSpawn. Compiles; not checked in Unity.

- **2026-11-03 (Claude)**: Manual reload. New Plane action Reload (R, gamepad West; backup of the actions file in the scratchpad), `Plane_Input.ReloadPressed` (assign Reload Action). `Weapon_IAmmo.TryReload()`; the machine gun starts its normal reload from any count below full (rounds kept until it finishes; ignored when full, bottomless or already reloading). `Equip_Loadout` calls it on R. The HUD reload bar, crosshair fill and reload-complete punches work unchanged. Compiles; not checked in Unity.

- **2026-11-03 (Claude)**: Shell ejection. New `Weapons/Weapon_ShellEjector.cs`: on a Particle System at a barrel's ejection port, Emit(Shells Per Shot) on its Weapon_Muzzle's Fired (Chance to thin). Particle setup (given to the user): Emission rate 0, no Play On Awake/Looping, World space, Inherit Velocity ~0.8, Shape cone at the port, gravity ~2-3, random 3D rotation + angular velocity, Collision World on the ground layer with bounce/dampen/lifetime loss, Max Particles ~200. User makes the ejector objects in the Machine Gun prefab. Compiles; not checked in Unity. One Fired subscription per ejector.

- **2026-11-03 (Claude)**: Scream particles back from v1.
  - Copied from v1 (GUIDs kept): `Prefabs/VFX/Scream Particles.prefab`, and `Art/Characters/Goblins/Scream/` (ScreamParticles_Mat, WindTrail.mat, exclamation 1.png). v1's prefab pointed its main particle material at a material missing even in v1; it now uses ScreamParticles_Mat. That material's second texture was also missing in v1.
  - `Horde_System` Scream Particles (prefab + Max Scream Particles 12): pool filled in Awake; StartScream/StopScream (now instance methods) also start/stop particles parented to the goblin at `Horde_GoblinType` Mouth Position / Mouth Rotation (v1 defaults). Full pool: the longest screamer gives its particles up. Particles stop with the scream (landing, death, get up, despawn).
  - Setup: assign the prefab on Horde_System; tune the mouth on the goblin type. Compiles; not checked in Unity.

- **2026-11-03 (Claude)**: Blast fling ring rework + fall damage + no spinning on the ground.
  - `Combat_ProjectileType` Instant Death Inner (0.8) / Outer (0.1), passed in `Combat_Explosion`: in the fling ring each goblin rolls to die on the spot (X eyes in the air), fading from the kill radius to the edge. The rest fly screaming with eyes open; blast damage is applied without killing (`HurtInAir`), and if it would have killed they're Doomed (counted in the blast's kill message now).
  - Fall damage: `Horde_System` spots the ground hit (vertical speed jumps up by Impact Speed Jump, 2 m/s) and calls `Impact`: damage = (speed - Safe Fall Speed) x Fall Damage Per Speed (type, 8 m/s / 0.3); Doomed or out of health dies there (Killed fires then, so the enemy counter drops as they land). Tougher goblins can survive and get up.
  - Spin: on impact the spin is cut to Impact Spin Keep (0.15) and angular damping goes to Ground Angular Damping (8); Launch sets Air Angular Damping (0.05). New `Horde_GoblinType` Landing header.
  - Compiles; not checked in Unity. No new allocations or subscriptions.

- **2026-11-02 (Claude)**: Enemy counter. New `UI/UI_EnemyCounter.cs` (on the counter text, top right): shows Wave_Director.Alive + ToSpawn, updated in LateUpdate so the number jumps by a frame's kills; one punch per frame scaled by kills (Kill Punch + Punch Per Kill, capped), red flash fading back (strength by kills), optional Tick Sound; escapes punch lightly, a new wave refills with Refill Punch. Assign director, UI_Shadowed text, UI_Punch. Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: Hit markers: merging + springs. `UI_HitMarkers` rewritten (same TryPlay API, references and sound fields kept; the DOTween ease fields are gone). A hit within Merge Radius (60 px) of a marker still in its Hold re-punches it (Repunch Kick, random rotation kick, a kill upgrades it to a kill marker) instead of spawning. New markers pop from 0 with Pop Kick through a scale spring, random rotation kick, capped by Max Over Scale / Max Angle; shrink away after Hold (0.15 s) without hits over Exit Duration. Sounds still play per hit. Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: Kill messages became context messages (user: per-kill numbers were unreadable at horde scale).
  - Renamed (files + .meta, GUIDs kept): `UI_KillMessage` -> `UI_ContextMessage`, `UI_KillMessages` -> `UI_ContextMessages`, `UI_KillStreakTable` -> `UI_MessageSet`. The streak (x1, x2...) is gone entirely.
  - `UI_MessageSet` (SO, Goblins/Message Set): a context (enum `UI_MessageContext`, only Explosion so far), Min Kills (3), Cooldown (0.6 s), lines, colour and size-by-kills, plus Dark Lines (fixed Dark Chance 15%, Dark Min Kills 4, own colour, bigger, Dark Hold +1 s). Default explosion and dark lines are written in.
  - `UI_ContextMessages.TryPlay(context, worldPoint, kills)`: random line without repeats, same pooled pop/wobble/rise/fade animation; optional Dark Sound. Assign the prefab and the sets (old table field is gone).
  - `Horde_System.TakeExplosion` counts its kills and calls it. A carpet bomb's three blasts are separate; the cooldown usually lets only the first speak.
  - Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: Kill messages (replace damage numbers), step 1.
  - New `UI/UI_KillStreakTable.cs` (SO, Goblins/Kill Streak Table): colour bands by streak count and milestone names (defaults: x4 CHOIR GONE QUIET, x8 BOOK CLUB, x12 JURY DUTY, x20 WEDDING PARTY, x50 TOWN MEETING, x100 CENSUS CORRECTION).
  - New `UI/UI_KillMessage.cs`: prefab root (RectTransform, children text + shadow; UI_Shadowed on the text assigned as Text). Animation moves the root.
  - New `UI/UI_KillMessages.cs` (on the HUD): pooled messages; streak (Streak Window 1.5 s) from Horde_System.Killed; same-frame kills batched into one message (a bomb) at their average spot with the highest milestone passed. Each message: pops from 0 past its size (scale spring), alternating wobble, rises/drifts (ease-out), holds (longer for bigger streaks/milestones), shrinks and fades. Size, hold and colour grow with the streak; milestones bigger + optional sound. All tunable.
  - Setup: build the prefab, add UI_KillMessages, assign canvas, prefab, table.
  - Not built yet: top-right feed, gold/multiplier. Compiles; not checked in Unity. One OnLateUpdate + Horde Killed subscription; milestone messages build a string.

- **2026-11-02 (Claude)**: Ability panel icons drain like the crosshair ring: `UI_AbilitySlot.PlayUse()` (use punch + DOTween drain, Drain Duration / Drain Ease under Cooldown); ShowCooldown shows max(drain, cooldown). Panel calls PlayUse on belt.Used. Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: Crosshair ability ring: on the selected ability's use it punches (Ability Ring Punch, a UI_Punch on the ring; Ability Use Punch strength) and drains from full over Drain Duration (DOTween, Drain Ease); shows max(drain, cooldown) so the drain meets the refill smoothly. Subscribes belt.Used; one small DOTween allocation per use. Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: `UI_Shadowed` Shadow Scale: the shadow's size as a multiple of the main's (applied on top of following the main's scale).

- **2026-11-02 (Claude)**: Crosshair ability ring (test). `UI_Crosshair` Ability Ring (Image, Filled Radial 360) + Belt: tinted the selected ability's colour, filled with its CooldownProgress. User's crosshair: Crosshair > Icon Shadow, Icon > Outline. Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: `UI_ScaleBaker` Toggle Preview: scales the transform by Scale temporarily (follows Scale edits), restores it when toggled off; baking ends the preview first. Preview state is serialized. Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: `UI/UI_ScaleBaker.cs` (editor tool): Bake Scale / Bake Transform Scale resize a panel and all children by writing into rect sizes/positions, TMP font sizes/margins, sliced image PPU multiplier, layout padding/spacing/cells, layout element sizes, Outline/Shadow distances and UI_Shadowed offsets, so transforms stay 1,1,1. Uniform only; one undo. Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: Crosshair follows the cursor. `Plane_Input` exposes `PointerPosition` and `UsingPointer`. `UI_Crosshair` Follow = Cursor (default): the mouse position, falling back to the shot point while a gamepad steers; Hide Cursor (on) hides the system cursor while enabled (restored on disable, re-hidden on focus), Confine Cursor (off). Assign Input. Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: Circle crosshair. `UI/UI_Crosshair.cs` rewritten (old square/plus fields gone): follows the shot point (or aim point), inner Fill (UI_Shadowed, Filled Radial 360) fills over the main weapon's reload (Idle Fill otherwise), UI_Punch on a child punches per round (Shot Punch) and on reload complete (Reload Punch). Needs the user's crosshair objects + assignments. Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: Reload bar. `UI_WeaponPanel` fills the Reload Bar slider (0-1) from `ReloadProgress` while reloading (OnLateUpdate only while reloading), hides it otherwise (Hide Bar When Loaded), and on reload complete punches the ammo counter (Reload Punch, 3x) and plays Reload Sound (optional). Assign Reload Bar. Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: `UI_AbilitySlot.Use Punch`: the use punch's strength as a share of the switch punch (`PunchUse()`).

- **2026-11-02 (Claude)**: Ability cooldown fill. `UI_AbilityPanel` feeds every row's `CooldownProgress` to `UI_AbilitySlot.ShowCooldown` each LateUpdate (new subscription; the icon only redraws when the value changes). The icon Image must be set to Filled / Radial 360 in the inspector. `UI_Shadowed` has Copy Fill (on by default): off keeps the shadow whole while the main fills. Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: Ability use punch. `Equip_AbilityBelt.Used(int index)` fires when an ability goes off (infinite too). `UI_AbilitySlot.Punch()` is the shared selection punch (row pop, icon, delayed name), played on switch and on use via `UI_AbilityPanel`. Fixed: the slot's plain scale fallback had ended up applying even with a UI_Punch, overriding the spring. Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: Ability select: icon and name punch separately. `UI_Punch.Punch(strength, delay)` lands a punch later. `UI_AbilitySlot` now has Icon Punch / Label Punch (UI_Punch on the icon image and name text; assign) with their own strengths and Label Delay (0.06 s); the row only springs its size plus Row Pop (replaces Select Punch). Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: Reusable UI springs + ability switch juice.
  - New `UI/UI_Punch.cs`: the one place UI springs live. Punch(strength) (scale, alternating rotation, push along Kick Direction), KickScale/KickRotation/KickPosition, SetTargetScale(size, snap). Caps, three springs. Position is only touched while pushed (safe in layout groups). Subscribes OnLateUpdate only while moving.
  - `UI_WeaponPanel`: its own punch code moved to UI_Punch (on Ammo Count, assign Ammo Punch; set its Position Kick to 200 to match the old default, which is 0 on UI_Punch).
  - `UI_AbilitySlot`: scales are now floats on a UI_Punch (assign Punch on the row). A real switch springs: the new row pops (Select Punch) to 1.05, the old squashes (Deselect Squash) to 1. Charge refreshes don't disturb a playing spring.
  - `UI_AbilityPanel`: Switch Sound (SoundData, make it 2D) on a real switch, not on the first ability.
  - Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: Warning/error fixes.
  - `FindObjectsByType` calls no longer pass the obsolete `FindObjectsSortMode` (Flow_System, Level_Cliff, Level_Ground, Level_TreeArea, Wave_Director). The InstanceID sorts are gone, so hole/goal lists come back in Unity's own order.
  - `Flow_System`: the private `Start(int, int, float)` is now `StartSolve` (Unity treated it as the Start message).
  - `Wave_Director.phase` (inspector-only) has its CS0414 warning silenced.
  - `UI_Shadowed` edit mode: only writes and marks dirty when a value really differs (it was dirtying the scene every frame), and copies the auto-size range instead of the computed size.

- **2026-11-02 (Claude)**: Drop shadow shortcut.
  - New `UI/Editor/UI_ShadowMenu.cs`: Alt+Shift+S (GameObject > UI > Add Drop Shadow) copies the selected text/image as a bare "<name> Shadow" sibling behind it (style shadow colour, no raycast) and adds UI_Shadowed. One undo step.
  - `UI_Shadowed` is now [ExecuteAlways] with an Offset field (default +5, -5): the shadow copies the main's anchors, pivot, size, rotation and scale and sits at the main's position + Offset (the offset doesn't rotate with the main). In edit mode it also copies the text (font, size, alignment, style, spacing) or sprite, marking the shadow dirty so the scene saves. The old "keep the authored offset" placement is gone: existing shadows snap to Offset.
  - `UI_Style`: Shadow Colour and Shadow Offset defaults for new shadows.
  - Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: Shared UI tech: shadows + dimming.
  - New `UI/UI_Shadowed.cs`: on a main text or image, pointing at its sibling shadow (auto-found as "<name> Shadow"). SetText / SetSprite / SetFill change both; SetColor / SetDimmed change the main only (shadow alpha follows the main's). The shadow follows the main's position/rotation/scale with its authored offset (Canvas.willRenderCanvases, only when the main moved). Disabling it hides the shadow.
  - New `UI/UI_Style.cs` (SO, Resources/UI/UI_Style; code defaults without it): dimmed brightness/saturation for text and images.
  - `UI_WeaponPanel`: Name Text / Ammo Text are now single UI_Shadowed refs (re-assign); the punch moves the main ammo text only.
  - `UI_AbilitySlot`: Icon / Label are UI_Shadowed (re-assign); unselected dims both, plus alpha/scale/empty sprite as before.
  - Not changed: UI_HitMarker's own image/shadow.
  - Compiles; not checked in Unity. One willRenderCanvases subscription per enabled UI_Shadowed; no allocations.

- **2026-11-02 (Claude)**: Ability belt + ability HUD, step 1 (create rows, selected/unselected states).
  - New `Equipment/Equip_Ability.cs`: base for abilities (colour, `NotifyUsed`, `CooldownProgress`, `Busy`, `OnSelected/OnDeselected`). `Weapon_CarpetBomb` now derives from it (a stick costs a charge; Busy while dropping).
  - New `Equipment/Equip_AbilityBelt.cs` (on the plane): abilities with charges or Infinite, one selected. All belt abilities tick (cooldowns recover, bombs keep falling); only the selected gets the button. Swapping is allowed any time; each ability stops what it should in OnDeselected. At 0 charges it leaves once not Busy and the next is selected. Add stacks onto an existing entry. Events Changed / SelectionChanged. Inspector buttons: Cycle, Add Debug Ability, Remove Selected.
  - `Equip_Loadout`: Spawn/Despawn helpers shared with the belt; ticks the belt and rebuilds its stats; Get(Ability) is the belt's selection; Equip with an ability puts it on the belt as infinite (so the old Starting Equipment carpet bomb still works).
  - Input: new Plane action Cycle Ability (E, gamepad left shoulder; right shoulder is Ability). `Plane_Input.cycleAbilityAction` needs assigning. Backup of the actions file in the scratchpad.
  - New placeholder abilities `Weapon_IceWall`, `Weapon_Napalm`: a press logs and uses a charge.
  - New `UI/UI_AbilityPanel.cs` (rows from the template, reused, belt order) and `UI/UI_AbilitySlot.cs` (icon tinted the ability colour; selected = alpha/scale 1.05/full icon, else alpha/scale 1/empty icon; "NAME [n]", no count when infinite).
  - Compiles; not checked in Unity. No per-frame allocations; row text builds a string on change; first-time row Instantiate.

- **2026-11-02 (Codex)**: Fixed the four findings from the project review, at the user's request.
  - `Horde/Horde_RenderGroup.cs`: initialise `rootPositions` from the spawn transform immediately, copy the moved goblin's cached position on swap-back removal, and retain/copy existing positions when capacity doubles. Other render outputs still rebuild each frame. Aim assist/area damage/explosions can safely query the cache before the next render.
  - `UI/UI_SwayDriver.cs`: iterate using the registry's null-safe `Count`, so a driver with no registered panels does not throw.
  - `Waves/Wave_Director.cs`: `BeginWave` counts existing living hole-bound goblins, including earlier wave survivors; clearing verifies a zero counter against the horde as well as retaining the periodic recount. Settled survivor behaviour recorded in Decisions. Sanitise missing, disabled and duplicate hole entries before event subscriptions (including enabled Flow_Goal requirement); gate subscriptions until Start validates setup. Each wave chooses from currently usable holes; fewer than two stops wave starts with an actionable error. If a source/target disappears or is disabled during pending spawning, cancel that source's remaining spawns once with a warning, updating `toSpawn` instead of dereferencing a missing target. This does not reroute already-spawned goblins whose destination is removed.
  - Validation: runtime and editor assemblies compile with Unity 6000.6 Roslyn/project references; existing warnings only. An ignored standalone regression harness in `Temp/ReviewFixChecks` exercises production wave/UI/registry code with engine stand-ins; cache lifecycle methods are extracted from production for container checks. This is not Unity integration verification. No scene or prefab edits. In Unity, test early Next Wave with survivors, a normal full clear, pool growth/reuse and blast targeting, HUD with zero/one panels, duplicate/missing holes and disabling a hole during spawning.
  - GC/subscriptions: no new event subscriptions or per-frame managed allocations. Existing native capacity growth briefly retains the previous position array until the copy finishes (old-capacity × 12 bytes extra peak native memory), then disposes it. Hole filtering/shuffling reuses existing lists; growth past their existing capacities can allocate as before. Error/warning strings allocate only on invalid setup/unavailable-hole transitions.

- **2026-11-02 (Claude)**: Ammo counter shot punch. `UI_WeaponPanel` now kicks the ammo texts' scale, rotation (alternating sides) and position (Kick Direction × Position Kick, capped by Max Offset) on each round fired, springing back; kicks stack up to Max Scale / Max Angle. Tunables under Shot Punch. Adds one OnLateUpdate subscription; no allocations. Compiles; not checked in Unity.

- **2026-11-02 (Claude)**: HUD sway (part of the ammo/UI module).
  - New `UI/UI_SwaySettings.cs` (SO, Goblins/UI Sway Settings), `UI/UI_SwayDriver.cs` (on the HUD canvas, a `Core_RegistryManager<UI_Sway>`), `UI/UI_Sway.cs` (per panel).
  - Channels: turn (slide against, tilt into), speed change (speeding up presses down; smoothed acceleration), dive (spread outward from the canvas centre, from descending vertical speed). Buffet was offered and declined.
  - Each element springs a child `Content` RectTransform, so layout and anchors are untouched; weights per channel and a Response multiplier.
  - Setup: driver on the canvas with the plane and a settings asset; on each panel, a Content child holding its visuals, then UI_Sway.
  - Also fixed: `Equip_Loadout.Get` returns null before Awake (the HUD woke first and threw).
  - Compiles; not checked in Unity. One OnLateUpdate subscription in the driver; no allocations per frame.

- **2026-11-02 (Claude)**: Ammo module, step 1 (function only, no feel).
  - New `Equip_Stat.MagazineSize` and `ReloadTime`, added before Length, so existing stat indices are unchanged.
  - `Weapon_IAmmo` interface, implemented by `Weapon_MachineGun`:
    - one round per shot;
    - when empty it auto-reloads over Reload Time and can't fire meanwhile;
    - a Magazine Size of 0 is bottomless;
    - an `AmmoChanged` event.
  - Machine Gun prefab: magazine 150, reload 2 s.
  - New `UI/UI_WeaponPanel.cs`: shows the Weapon slot's name and ammo. Name Texts and Ammo Texts are arrays, so each shadow copy matches its text. It follows `SlotChanged`.
  - The user's panel is in the scene (not the prefab) with Weapon Name and Shadow, Ammo Count and Shadow, a Reload Bar (Slider) and Weapon Dots (Vertical Layout). The user dropped the max-ammo text and the UI shader.
  - Setup: add UI_WeaponPanel to Weapon Panel, assign the Equip_Loadout and the four texts.
  - Not yet: reload bar, dots, juice. Compiles; not checked in Unity. Events only, no UpdateManager subscription; SetText with a number doesn't allocate.

- **2026-11-02 (Claude)**: Review fixes.
  - New `Level/Level_BuildKey.cs`: a hash of a level tool's inputs.
  - `Level_Cliff`, `Level_TreeArea` and `Level_Ground` (with its holes) now rebuild from OnValidate, undo and load hooks only when that hash changed.
    - After a load they adopt the saved result instead of rebuilding. Before, every script reload, scene open and play exit rebuilt them, dirtied the scene, and re-made every tree with new file IDs.
    - Explicit Rebuild buttons and handle edits still always build.
  - `Horde_RenderGroup`: with variation groups, non-varied parts use the direct chunked draw again; only option parts compact (`DrawDirect` / `DrawSelected`). Eye state is gathered only when an eye part draws there.
  - `UI_Crosshair.cs` is unused and can be deleted.
  - Compiles; not checked in Unity. No new runtime allocations or subscriptions; edit-time JSON strings only.

- **2026-11-02 (Codex)**: Exact variation percentage controls. `Horde_VariationGroupEditor.cs` replaces raw weights with per-option Chance (%) sliders; editing one preserves its exact chance and redistributes the rest proportionally, or evenly if all others were zero. Existing relative-weight data displays correctly without migration. A sole option stays at 100%; legacy all-zero data can be enabled explicitly. Add Option starts at 0% (first option 100%); removal normalises remaining options and deactivates the removed source root without deleting it. Name/root fields, preview, Undo and prefab overrides remain available. `Horde_VariationGroup.SetPercentage` supplies the allocation-free maths; only the editor calls it with a temporary array. No runtime draw/selection changes, new per-frame allocations or subscriptions. Runtime/editor C# compiled; production maths tests cover proportional redistribution, zero cases, one option, and 0/100% endpoints. Inspector interaction/Undo still needs Unity verification. Adjust chances in the existing group inspector, save prefab and restart Play Mode.


- **2026-11-02 (Codex)**: Reusable weighted visual groups and cyclops eye authoring.
  - New `Scripts/Horde/Horde_VariationGroup.cs` (+ meta): serializable options (label, direct-child root, relative weight), Head/Body anchor, validation (no nested groups or duplicate/non-child roots), and `Horde_Variations` bake/selection storage. Options may be empty for None; zero weights are excluded, all zero means no option. Bake reads inactive preview alternatives but respects deliberately inactive descendants and disabled MeshRenderers. Weights are snapshotted at bake; choice rolls once per spawn, uses independent seeded RNG, swaps back on removal, resets on reuse and is disposed with the render group.
  - New `Scripts/Horde/Editor/Horde_VariationGroupEditor.cs` (+ meta): percentages, Preview buttons, Randomise Preview and Add Option. Preview toggles real option GameObjects with Undo/prefab overrides. Editing preview does not change weights. Generic groups can later author hats, horns, equipment visuals etc. Gameplay behaviour remains separate.
  - `Horde_EyeCards.cs` now supports a single central card with independent width/height, XYZ offsets and pitch/yaw/roll sliders. Shared bake handles 1 or 2 cards; central UV side=0 prevents unequal-pair scaling. `Horde_EyeCardsEditor.cs` adds Create Two Eyes / Cyclops Variation Group: an identity wrapper preserves existing placement, existing Eyes becomes Two Eyes (85), a separate Cyclops component/card starts near the same face placement (15). One Undo group covers setup; source prefab is not edited until the user invokes it in Unity. Repeated setup inside a group is rejected.
  - `Horde_RenderGroup.cs`: resolves each part to its option and anchor, bakes all eligible alternatives including preview-hidden roots. Draw compacts selected instance matrices and matching colour/eye data into fixed buffers per batch. No drawing of unselected options, no combinatorial batches for combinations of independent groups; common meshes still draw all goblins. The no-variation draw path is retained. CPU matrix jobs currently calculate all baked parts before draw filtering; profile large numbers of options before expanding heavily.
  - Setup: open Goblin prefab, select Eyes, click setup button. Select Eye Variations to preview Two Eyes or Cyclops and edit weights. Select Cyclops and edit its Central Eye sliders; keep shared material. Add new visual groups via Add Component > Goblins > Horde > Variation Group; Add Option creates roots to put meshes under. Options must be direct children; groups must be independent siblings, not nested. Save prefab, restart Play Mode to rebake weights/layouts, test a wave, damage/death and pooled reuse.
  - Validation: runtime + editor script sets compiled with Unity Roslyn/project references. Executed production weighted selector outside Unity: empty/all-zero, zero-weight exclusions, edge rolls and 10,000 stratified rolls for 85/15 (exactly 1,500 cyclops) passed. Harness/output in ignored `Temp/VariationChecks`. Unity batch remains unavailable while project is open; visual/Undo/prefab/save-load and runtime buffer alignment/swap-back need editor verification. No scene/prefab/v1 changes this step.
  - GC/subscriptions: none per frame, no new subscriptions. Bake allocates group/weight arrays and option-root dictionary; one NativeList<int> per group (4 bytes per goblin per group before capacity overhead), grows only on spawn. One Matrix4x4[1023] (~64 KiB) draw buffer per type with variations; existing colour/eye buffers reused. Eye bake allocates card/combine/centre arrays/lists once per option; owned meshes disposed as before. Editor GUI, creation, preview and Undo allocate during authoring only.


- **2026-11-02 (Codex)**: Boxier death X and occasional unequal eyes. `Goblin_Eyes.shader` now uses two square-ended rectangle strokes with independent Death X Stroke Length and Stroke Half Width, retaining black. `Horde_GoblinType` adds Eye Asymmetry Chance (default 25%) and Range (default 15-35% extra size); one randomly selected eye grows on spawn. `Horde_EyeCards` bakes left/right identity in UV1.w; `Horde_Animation` stores signed asymmetry, and `Horde_RenderGroup` rolls it once on spawn and sends it in the existing gaze buffer. Shared blink/gaze remains; final card scale capped at 2x so existing expanded bounds still cover it. Restart Play Mode to rebake mesh metadata and respawn variation. Runtime assembly compiled successfully; shader/visual verification still pending in Unity. Adds one float per existing goblin anim state; no new buffers, per-frame allocations or subscriptions.

- **2026-11-02 (Codex)**: Death X colour corrected to black at the user's request in `Art/Characters/Goblins/Shader/Goblin_Eyes.shader`. Normal eye colours unchanged. No setup, allocations or subscriptions added. Source checked; visual verification remains in Unity (use Preview Death X on the material).

- **2026-11-02 (Codex)**: Eye shader + combined instanced rendering, preserving the user's placed cards.
  - New `Art/Characters/Goblins/Shader/Goblin_Eyes.shader`, `.mat` and metas. Procedural white eye/dark pupil, per-goblin blink interval/phase and smooth automatic glances, pupil direction/travel, temporary hit squint, white death X's. Unlit with fog; clipped silhouette in forward, depth and depth-normal passes; no shadow casting. Material exposes shape, colour, pupil, blink, glance, hurt and X controls plus preview blink/hurt/death. Instancing enabled on the material.
  - `Horde_EyeCards.cs`: TryBake reads actual child meshes/materials/transforms and combines the pair once per render group, keeping 0-1 UVs per eye; UV1 stores per-vertex card centres for size variation without changing spacing. Expanded bounds cover supported scale up to 2. Requires enabled cards, readable meshes and one shared Goblins/Eyes material; otherwise warns and leaves the ordinary renderer path. Rebuild now falls back to the project's eye material. Authoring GameObjects remain separate.
  - `Horde_RenderGroup.cs`: skips the two source renderers only after successful baking; one combined eye part/batch per pair, attached to Head regardless of source hierarchy. Uses headFrame inverse times actual root-relative Eyes transform, so the Eyes parent at Y=1.2 and card Height=0 retain their placement, including the head's authored rotation/scale. Owns/disposes combined meshes. Eye draw properties are separate from body colour properties. Existing draw chunks and matrix Burst jobs are reused.
  - `Horde_Animation.cs`: shared eye seed, last-hit time, size and gaze live in the existing swap-back anim state. RenderGroup.Hit timestamps the reaction; Draw reads Goblin.Dead immediately (does not wait for grounded shrink). Spawn rolls fresh state and size, so pool reuse/swap-back use existing lifecycle. `SetEyeGaze(index, Vector2)` changes both pupils, added to material gaze/glances (disable automatic glances on material for fully directed gaze). `Horde_GoblinType.cs` adds eyeSizeRange (default 0.9-1.1; supported 0.1-2); both eyes share one roll. Damage squint is the adjustable default; set Hurt Squint Strength to 0 to disable.
  - Updated `Horde_EyeCardsEditor.cs`, root `AGENTS.md` and `Docs/Decisions.md`. `Prefabs/Goblin.prefab` changed only its three eye-material references (component and both renderers); verified inverse replacement exactly matched original file text. Original snapshot is in ignored `Temp/EyesValidation/Goblin.before.prefab`. No scene or v1 changes.
  - Editor steps: let scripts/assets import, reopen the Goblin prefab if it was already open so its new material references load, then test a wave. Keep Eyes at Y=1.2 and component Height=0. Check head bob/size variation, pupils/blinks, nonlethal-hit squint, ground/air death X's and pool reuse. Tune Goblin_Eyes.mat (Preview fields should be 0 during play). If a previously open prefab overwrites the material references, assign Goblin_Eyes on the Eyes component and press Create / Update Eye Cards before saving.
  - Validation: full runtime/editor script sets compiled with Unity 6000.6 Roslyn and project references; only existing warnings. Attempted Unity batch validation of shader, 8 vertices/12 indices, prefab-space preservation and batch count, but Unity refused because this project is already open. Temporary validator was moved out of Assets into ignored Temp/EyesValidation; it has not run and no rendered preview is claimed. Shader variants, runtime visual behaviour, head carry, draw-call count, GC and performance need Unity checks.
  - GC/subscriptions: no per-frame managed allocations or new subscriptions in the added code. Bake allocates a combined mesh, combine array, centre list and renderer set; meshes disposed with the group. Eye groups allocate two fixed Vector4[1023] buffers (~32 KiB total) and one MaterialPropertyBlock once; shared eye fields add 20 bytes before struct padding to existing per-goblin state/capacity. Existing capacity growth remains the only data-array growth; GPU time drives procedural blinks/glances. One eye geometry submission per pair per draw chunk (URP may run extra depth passes).

- **2026-11-02 (Codex)**: Goblin eyes step 1, authoring only, per the user's explicit scope.
  - New `Scripts/Horde/Horde_EyeCards.cs`, `Scripts/Horde/Editor/Horde_EyeCardsEditor.cs`, script/folder metas. Create / Update Eye Cards makes/reuses `Eye_Left` and `Eye_Right` as actual child GameObjects. Each has a shared Unity Quad mesh (full 0-1 UVs), a MeshRenderer, no generated collider; both use the selected shared preview material (empty uses the pipeline default). Inspector sliders update real transforms with Undo and prefab override recording; no OnValidate scheduling or runtime rebuilding.
  - Editor setup: open the goblin prefab in Prefab Mode, add an empty Eyes child under its authored Head, reset its local transform and add Horde_EyeCards. Point the Eyes parent's +Z out of the face. Press Create / Update Eye Cards; tune Spacing, Height, Forward, Width, Eye Height, Left/Right Size, Pitch, Splay and Roll. Move/rotate Eyes to place the whole pair. Keep the child MeshRenderers enabled. Save the prefab. Sliders drive child transforms, so place individual cards using the component rather than editing them directly. Quads are plain rectangles until the shader step.
  - No prefab/scene or Horde_RenderGroup changes. Existing renderer already discovers enabled MeshRenderers under Head; no dedicated eye rendering/animation integration has been added. Combining the pair into one mesh and sharing one per-goblin state are future work, not implemented in this step. Intended later features: blinks, controllable pupils, per-goblin size variation, death X's. Damage expression is still unsettled (squint versus temporary X's).
  - Updated root `AGENTS.md` and `Docs/Decisions.md` with the GameObject-authoring requirement and staged eye scope so Claude can continue. Full runtime/editor script sets compiled successfully outside Unity using Unity 6000.6's Roslyn compiler and existing project references, including both new files; only pre-existing warnings. Outputs in ignored `Temp/LevelToolsCheck`. Placement, appearance, slider interaction, Undo/Redo and prefab saving still need checking in Unity.
  - GC/subscriptions: editor creation allocates two GameObjects and their standard components (temporary primitive colliders are removed); Undo/inspector operations allocate only on authoring edits. Built-in mesh and material assets are shared, no material clones or custom mesh allocations. No runtime rebuilds, per-frame work, event or UpdateManager subscriptions.

- **2026-11-02 (Codex)**: Consistent level-tool handles, following the spec agreed with the user; town blockout excluded.
  - New `Scripts/Level/Editor/Level_Handles.cs`, `Level_GroundEditor.cs`, `Level_HoleEditor.cs` and their metas. Shared colours, planar movement squares and snapping; ground width/length cubes; hole radius cubes and depth cone. Ground/hole inspectors explain the controls. Each has a configurable Snap (0 = free).
  - Updated `Level_Ground.cs` (Size accessor, Snap, positive size validation, edit-drag rebuild deferral and Undo rebuild hook), `Level_Hole.cs` (Snap), `Editor/Level_Menu.cs` (new ground appears at Scene-view pivot, on world floor). Hole handles follow the containing ground's transformed space and ignore the hole object's height, matching ground generation.
  - Updated `Level_TreeAreaEditor.cs` and `Level_CliffEditor.cs` to use shared white movement squares and colours, record prefab-instance handle changes, and finish pending tree/collider rebuilds on release/lost control/deselection. Existing add/delete and cliff-specific handles remain. Custom handles are edit-mode only.
  - Editor steps: no setup or scene/prefab migration. Select ground, holes, trees or cliffs with Scene-view Gizmos enabled; drag their handles, check Undo/Redo and adjust Snap as wanted. Ground/hole geometry updates on release, with wire outlines while dragging. Bake flow obstacles again after changing level geometry as usual.
  - Validation: compiled full runtime/editor script sets outside Unity using Unity 6000.6's Roslyn compiler and project references, including all new editor files. Passed; only pre-existing obsolete object-search and unused-field warnings. Outputs in ignored `Temp/LevelToolsCheck`; no generated csproj or Unity assemblies changed. Scene interaction, Undo/Redo, prefab overrides, transformed grounds and obstacle baking still need testing in Unity.
  - GC/subscriptions: no new runtime allocations or UpdateManager subscriptions. Editor-only SerializedObject/Undo/handle allocations; hole editor allocates a ground-search result on Layout when idle. One new editor subscription: `Undo.undoRedoPerformed += Level_Ground.RequestRebuild`, registered by the existing domain-load hook. Deferred rebuilds reuse the existing delayCall scheduling. No scenes, prefabs or v1 files edited.

- **2026-11-01 (Claude)**: Height line. The user dropped the crosshair (more fun without; the assist alone feels skilful). `UI_Crosshair` is unused and can be deleted, but `AimPoint`/`ShotPoint` stay in the aim data.
  - New `Plane/Plane_HeightLine.cs`: a faint dotted LineRenderer from the plane + offset (plane space) down to the ground, with an X sprite lying flat at the ground point.
  - The dot texture is made at start. There's a yellow 0.1 gizmo sphere at the start point.
  - Setup: add to the plane; give it a transparent vertex-colour material (URP Particles/Unlit) and the X sprite.
  - Compiles; not checked in Unity.
- **2026-11-01 (Claude)**: Crosshair.
  - New `UI/UI_Crosshair.cs`: a square for the plain aim point (nose and gun angle, never the assist target, no lock-on). A plus shows where the guns actually point.
  - `Weapon_AimData` gains `AimPoint` and `ShotPoint`; `Weapon_Aim` gains `aimDistance` (60, used when the guns are level).
  - Setup: add the component under the HUD canvas, assign the plane's Weapon_Aim and the two images.
  - Projectile and impact effects also have `impactScale` now. Compiles; not checked in Unity.
- **2026-11-01 (Claude)**: Tree placer.
  - New `Scripts/Level/Level_TreeArea.cs` and `Editor/Level_TreeAreaEditor.cs`; menu: GameObject > Level > Tree Area.
  - You draw an outline with nodes (same handles as cliffs) and it fills with weighted tree prefabs. Placement follows spacing, Perlin patches and edge margin, with random size, turn and lean.
  - Trees are dropped onto Ground layers, skipped on Avoid layers (Building, so not on cliffs) and inside holes.
  - Trees are real prefab instances under a generated "Trees" child (marked Batching Static). They're rebuilt on release, not every drag frame.
  - Trees only block goblins if the tree prefab has a collider on an obstacle layer (then Bake Obstacles).
  - Compiles; not checked in Unity.
- **2026-11-01 (Claude)**: Cliff look refined toward the new references (big tall slabs, layered, slightly turned, cool lavender, deep crevices).
  - Wall columns are now turned and leaning boxes, with new Blocks settings: Pillar, Front, Ledge, Turn Jitter and Lean Jitter.
  - Fake AO comes from vertex colours, with new Colour settings: Recess, Side and Contact Darkening.
  - Bigger defaults and cooler colours. Existing cliffs keep their old values until Reset.
  - Compiles; not checked in Unity.
- **2026-11-01 (Claude)**: Cliff Chunks and defaults.
  - New shape **Chunks** (menu: Cliff Chunks): one plain box per node, with per-node width, length, turn and lean (new `Level_CliffNode` fields). Scene handles: orange width, pink length, blue turn ring, red lean ring; a green dot adds a chunk.
  - Inspector: small **Set Default / Reset** under every section, and **Set All as Default / Reset All** at the bottom.
    - Saved defaults go to `ProjectSettings/Level_CliffDefaults.json` and apply to new cliffs from the menu.
    - Reset goes back to the built-in code defaults.
    - Shape, nodes and the seed are never defaults.
  - Compiles; not checked in Unity.
- **2026-11-01 (Claude)**: Cliff follow-ups. Columns sticking up above the top now build their back face above the top (no see-through from on top). Optional **Top Rocks** (off by default): seeded clusters of stacked blocks scattered over the top tier, kept in from the edges, sitting on the top surface. Compiles; not checked in Unity.
- **2026-11-01 (Claude)**: Cliff tool.
  - New files: `Scripts/Level/Level_Cliff.cs`, `Scripts/Level/Editor/Level_CliffEditor.cs`, `Art/Level/Level.shader`.
  - Menu items added to `Level_Menu`: Cliff Line, Cliff Block, Cliff Ring (Map Edge).
  - Compiles in an outside-Unity check, but **not yet tried in Unity**: look, handles, triangulation of odd shapes and the obstacle bake are all untested.
  - Editor steps: create cliffs from GameObject > Level, then press Bake Obstacles on the Flow_System.
  - No runtime cost beyond a static mesh and collider; no subscriptions. Build reuses static lists.

- **2026-11-01 (Claude)**: Added handoff docs (`AGENTS.md` at the repo root, so Codex sees v1 too; `Docs/State.md`, `Docs/Decisions.md`) and updated `CLAUDE.md`. Converted plane/weapon/projectile/goblin sound fields from inline `SoundConfig` to `SoundData`, with `SoundData.Get()` as a null-safe helper. Clips that existed were moved to new assets (Plane Engine Low/High, Plane Wind, Machine Gun Shot, Bomb Impact). Not yet checked in Unity. No new allocations or subscriptions.
- **2026-11-01 (Claude)**: Flung goblins use v1-style torque (`Horde_GoblinType.launchTorque`). Added gizmos to the carpet bomb, projectiles and aim.
