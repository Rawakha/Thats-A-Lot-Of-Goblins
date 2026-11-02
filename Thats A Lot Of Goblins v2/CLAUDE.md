# That's a Lot of Goblins (V2 - plane version)

Unity 6000.6 (URP, Input System, DOTween), solo dev, ~30 hrs/week. Arcade plane game, angled top-down, PC (Steam).
Release: Steam first, then a premium (paid, no ads or IAP) mobile port. Keep controls device-agnostic and heavy effects behind quality settings so the port stays possible.
Pitch: the goblins have taken the Earth; you're the last pilot, and you kill every last goblin. A "Goblins remaining" counter is always on screen.

**Before doing anything, read `Docs/State.md`** (what's built, what's in progress, what's next) and skim `Docs/Decisions.md` (things already settled; don't undo them without asking).

V1 (`../Thats A Lot Of Goblins v1`) is a read-only reference for old systems. Copy and adapt into V2, never edit v1, never reference it across projects.

## Game direction: Brotato-style roguelite (settled 2026-11-02)
A run is a chain of small areas. It must be super juicy.
- **An area:** a small map with several holes. Waves of goblins pour out of the holes and walk (flow field) to goals; you fly around shooting them.
- **Clearing an area:** once its waves are done, fly to the next edge (a screen-space indicator points the way). The screen fades to black and you're in the next area.
- **Upgrades along the way:**
  - "pick 1 of 3" cards, which give ability charges (consumables, e.g. 2x Ice Wall Bomb) and passives;
  - **Bloons TD-style upgrade paths** per weapon: several paths, and you choose how far down each one to go.
- **Weapons are swapped between** (about 3 at once); abilities are used up and replaced from the cards.
- **The horde is mostly passive:** it just walks. Over time, recognisable special groups appear:
  - **Armed squads** that shoot back at the plane.
  - **Shield tortoises:** goblins round the edge carry overhead shields like a medieval ram's roof, with 20-30 goblins huddled underneath, heading for a goal. The roof blocks fire from above.
- **A wave ends when all its goblins are gone** (killed or into their goal); there's no timer, and escapes cost nothing: kills pay gold, which buys upgrades. **When the plane's health runs out it goes down:** the pilot can eject to save the run, and a failed eject ends it.
- Gold per kill and the multiplier feed (Hit, Multi Kill, Blast, Kill, dark-humour group-kill names) stay.
- Scale target: thousands of goblins now (~1,500 tested), 20k is the long-term stretch (needs the data-only horde, see Decisions).

## Code conventions
- **Naming: `System_ScriptName`** for scripts and files, e.g. `Horde_System`, `Plane_Controller`. The prefix groups by system and makes things searchable.
- **All per-frame logic goes through `UpdateManager`** (`Scripts/General/UpdateManager.cs`): subscribe to `OnUpdate`, `OnFixedUpdate`, `OnLateUpdate`, or the 15/30/60hz events. No `Update()`, `FixedUpdate()` or `LateUpdate()` on gameplay scripts.
- **Performance first.** Burst and the Job System for the horde and other mass systems. The player plane and player weapons stay plain main-thread C# until profiling shows they're a bottleneck. No per-frame allocations, LINQ, `Find*` calls or GC pressure in hot paths. Native containers and structs-of-arrays for horde data.
- **GC budget is tracked.** Flag every new allocation and every new event/`UpdateManager` subscription when writing code, and report it. Horde per-goblin updates are zero-alloc and never subscribe per goblin; one system subscription drives all of them.
- **Rendering is GPU instanced** (`Horde_RenderGroup`, `Graphics.RenderMeshInstanced` in chunks of 1023) with the hand-written URP shader `Goblins/Goblin` (`Art/Characters/Goblins/Shader`). No per-goblin `MeshRenderer`.
- **Registration uses swap-back removal** via `Core_RegistryManager<T>` + `Core_RegistrableBehaviour` (`Scripts/Core/Registry`). Don't hand-roll add/remove lists.
- **Shared maths lives in `Core_Math`** (`Scripts/Core/Math`): damping, springs (`Core_SpringSettings` + spring states), curves, planar helpers. Float/Vector functions are Burst-safe, Quaternion ones are main-thread only. Add new shared maths there.
- **Burst jobs:** write `[Unity.Collections.ReadOnly]` in full; the project has an inspector `ReadOnlyAttribute` in the global namespace that clashes.
- **Sounds are `SoundData` assets** (ScriptableObjects in `Art/SFX`), not inline `SoundConfig`s, so they're easy to find, tune and reuse. Play them through `Utilities.TryPlaySound` or `SoundData.Get(data)`; empty slots stay silent.
- **Gizmos** for anything with a radius, cone or range, via `Debug_Gizmos` (`Scripts/General`), so tuning is visual.
- **Level tools** (`Level_` prefix) stay independent of gameplay systems.
- Profile all the time; thousands of goblins leave no room for slow code.

## Working rules
- **The user commits.** Never commit, push or open PRs, and never offer to. Finish with a summary of what changed and which files, so they can review.
- **One module at a time.** When several features are agreed, build only the current one, then stop and wait until the user says it's polished before starting the next (they tune by hand in the inspector).
- **Spec before code:** agree what a system does first.
- Explain code as it's written; don't add things the dev doesn't understand.
- Feel stays human: plane handling, low flight, camera, hitstop and juice are tuned by hand, so expose tunables (with tooltips) instead of baking in values.
- Match surrounding code style (comment density, naming, idiom). Each script opens with a comment explaining what it does, how to set it up, and its GC/subscription cost.
- Unity scenes and prefabs merge badly: only one agent edits a given scene or prefab at a time. Prefer changing code and ScriptableObjects; tell the user which editor steps they need to do.
- **Keep `Docs/State.md` current.** Update it at the end of every module, and whenever something meaningful changes mid-module, so another agent (Claude or Codex) can pick up cold if work stops suddenly. Add settled design or tech choices to `Docs/Decisions.md`.
