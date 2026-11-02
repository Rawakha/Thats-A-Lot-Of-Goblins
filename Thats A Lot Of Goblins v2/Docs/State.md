# Project State

Last updated: 2026-11-02 (Codex)

Read this first. It says what exists, what's being worked on, and what's next. Whoever finishes a piece of work updates it.

## Current module
**Project review fixes: implemented; Unity Play Mode verification pending.** Horde position caches now initialise on spawn, survive capacity growth and follow swap-back removal. HUD sway tolerates an empty registry. Wave advances include surviving goblins and verify the living count before clearing. Wave setup removes missing/disabled/duplicate holes before subscribing, requires two distinct usable holes, and guards holes becoming unavailable during spawning. See the newest handoff entry for validation and editor checks.

**Reusable visual variations + cyclops authoring: implemented, ready for the user's Unity setup/test.** On the existing Eyes component, press **Create Two Eyes / Cyclops Variation Group**. This wraps the existing Eyes object in an identity group, preserves its Y=1.2/card transforms, and creates a separate Cyclops option with independent central-card width/height, XYZ position and pitch/yaw/roll. Defaults are 85:15 weights. The generic Horde_VariationGroup inspector supports any direct-child visual options, Head/Body anchors, editable Chance (%) sliders that redistribute the remainder, option previews, Randomise Preview, Add Option and empty/None options. Preview visibility is independent of spawn choices. No prefab/scene was edited automatically this step. Runtime/editor C# compiled and weighted selection tests passed; editor interaction, prefab Undo, runtime batching/appearance and profiling still need Unity checks. Stop for feedback before moving on.

**Goblin eyes: shader and combined horde rendering implemented; waiting for Unity verification/tuning.** User placed Eyes beside Head at local Y = 1.2, component Height = 0; all authored transforms are preserved. New `Goblins/Eyes` shader/material draws white-rimmed dark-pupil eyes, shared asynchronous-per-goblin blinking/glances, controllable gaze, adjustable hurt squint, square-ended black death X's (including airborne deaths). The render bake combines the two source cards into one head-animated part per pair; the prefab keeps both editable GameObjects. Material assigned in `Prefabs/Goblin.prefab`. Tune `Goblin_Eyes.mat` (independent X stroke length/width) and `Horde_GoblinType` eye size/asymmetry controls; no reparenting needed. C# compilation passed; Unity batch validation was refused because this project is already open, so shader compilation, appearance, combining at runtime and profiling are still unverified. Stop here for the user's visual feedback.

**Level-design tool consistency: built, waiting for the user to try in Unity.** Ground and holes now follow the tree/cliff Scene-view workflow, with white movement squares, orange width/radius cubes, pink length cubes and yellow depth cones. Trees and cliffs share the same movement squares and palette. Ground/hole outlines preview drags; mesh/collider rebuilds wait for release. Town blockout is excluded at the user's request (it was only a test). No further queued gameplay module has been started.

**Carpet bomb / ability weapon: polish and tuning by the user.** Built and working; the user is tuning feel (bomb spacing, blast radii, fling, goblin spin). Don't start the next queued module until the user says this one is polished.

**Also just built, waiting for the user to try: the cliff tool (`Level_Cliff`).** See the handoff log. Expect feedback on the look (it must match the user's Cube & Seek reference: chunky stacked square blocks, soft pastel shading) and on the handles.

Open question: the user has talked about "the missile weapon". The carpet bomb is the ability built so far; homing missiles (lock onto the densest clump) are **not started**. Ask before building them.

**Current (2026-11-02): ability belt + ability HUD, step 1 built, waiting for the user's Unity test.** Settled input: Q cycles weapons (not built; weapon switching waits until the general UI layout is done), E / gamepad left shoulder cycles abilities. Next steps for the ability HUD, not started: cooldown fill on every icon (vertical Image fill, from `Equip_Ability.CooldownProgress`), and the use kick (scale/position/rotation, like the ammo counter) on icon and text; Claude proposed moving the kick into a shared `UI_Punch`. Second weapon to test swapping: Claude suggested a Flak Cannon (not agreed yet).

## Queue (in order, one at a time, wait for "polished" between each)
1. **Machine gun ammo**: magazine + auto-reload, and an ammo counter that squishes and rotates per shot.
2. **Gold + multiplier feed**: gold per kill, shown as gold earned this round (not a score). A feed of multipliers (Hit, Multi Kill, Blast, Kill) with dark-humour group-kill names, e.g. "Family of four x2.5".
3. **Fuel**: fuel plus a fuel bar with a liquid-physics shader. Fuel becomes the round timer.
4. Alongside these: a **UI juice core** (`UI_Punch` springs, built with the first UI module) and weapon index dots.

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
- Data-only horde rewrite: do it before armed squads and shield tortoises, so they're built on the new layout.

Planned weapon (user, 2026-11-02): **Egg Launcher** (main weapon): lobs eggs in an arc that splat for small area damage, ~50% hatch a chicken that charges the nearest goblin and pecks (capped live count, lifetime; flung by bombs like goblins). Upgrade path ideas: Hatchery (hatch chance, twins, rooster taunt), Explosive eggs, Fowl army (chicks on death). Own message context ("FOWL PLAY"). Open: do goblins kill chickens later, hatch chance tuning.

Planned (user, 2026-11-02), not started:
- **Dragon's Breath** (weapon): flamethrower; short cone, sets goblins and ground on fire (burning spreads, panic-run). Fire message context.
- **Iced Up** (ability or weapon, TBD): freezes goblins into heavy ice blocks (physics bodies that can be flung/shattered; block lanes). Freeze context ("BRRRRR").
- **Tree Seeds** (ability): drops seeds that grow a permanent wall of trees, changing the map (flow-field obstacles). Code must enforce "only one" (exact meaning to confirm with the user).

Later, not scheduled: **goblin families and events** (user, 2026-11-02): goblins spawn and walk in family groups so dark lines can be literal ("WIPED OUT THE DURSLEYS" only when a whole family dies); special processions like a wedding (bride) or a funeral ("DEATH AT A FUNERAL"), each with its own message context. More contexts: Freeze ("BRRRRR"), fire, into a hole.

Later, not scheduled: homing missiles, roguelike upgrade picks between rounds, escaped goblins feeding "Goblins remaining", data-only horde for 20k goblins.

## What's built
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
