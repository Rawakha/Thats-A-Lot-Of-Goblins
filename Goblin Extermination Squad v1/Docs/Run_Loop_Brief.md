# Run Loop brief (manager, 2026-11-04)

This replaces the run design in `Decisions.md` where they differ. The Run Loop chat updates the conflicting Decisions entries
as part of its first stage. Build one stage at a time and stop after each for feedback.

## The idea
Build the first playable loop and test whether it is fun. It is a run-based pick-3 loop, which is cheaper than an upgrade tree.
If it isn't fun after three areas in a row, pivot to the incremental version (gold buys permanent upgrades, stars, level select).
Keep everything reusable for that pivot: areas, spawn events, the Run scene and `Run_Carry`.

## The loop
1. **Start of an area:** the player has a **flare** ability (a fresh one each area). Nothing spawns until it is fired, so the
   player starts when ready. No Ready phase and no Space / Next Wave press.
2. **Spawn events:** every area's `Wave_Director` holds spawn events. Each has a spawn hole, a target hole, a count, a rate and a
   trigger: flare fired, after N seconds, after N goblins killed, when N or fewer remain, or after another event finishes.
   The first event is normally triggered by the flare. The area is cleared when every event has finished and no goblins are left.
   Keep Debug Waves for recording clips.
3. **Leaks:** keep the run-wide leak pool already built (each leak spends one, zero fails the run, some comes back after a clear).
4. **Clear, then fly north:** no "Area Cleared" prompt or fly-north prompt or vignette. Instead a **mesh indicator on the ground
   in front of the plane** points the way to the exit. At the north edge the existing fade, fly-on and next-area load stay.
   The map-limit warning and respawn behaviour stays.
5. **Pick-3 shop at the fade:** one pick from three offers, drawn from one pool of weapons and abilities. Offers exclude what
   the player took last time, so they don't repeat the same picks. Picks are kept for the run (`Run_Carry`). Weapons swap with Q.
6. **Fail:** the leak pool hitting zero shows the Game Over screen with Restart.

## Out of scope for this version
Gold upgrades and the upgrades screen, the loadout screen, stars and the results screen, the level select, bosses, plane health
and eject, armed squads. Keep the leak HUD.

## Stages (propose changes, then wait for my go-ahead)
1. **Clean-up and indicator:** first check what has already been removed. Remove what the loop no longer needs (Area Cleared prompt,
   fly-north prompt and vignette, anything only they used), add the ground mesh indicator, and fix the conflicting Decisions
   entries. List removals and dependencies before deleting.
2. **Spawn events and the flare:** restructure `Wave_Director` and `Wave_Settings` into trigger-driven events. Flare and time
   triggers first, then kill-count, remaining and after-event triggers. Remove the Ready phase and Space start.
3. **Pick-3 shop:** the offer pool with a few placeholder items, the no-repeat rule and the screen, with picks kept across areas.
4. **Weapons in the pool:** make picked weapons equip and swap with Q (needs the Egg Launcher or another second weapon).

## Verify (each stage)
Compiles; play the area start to clear; restart leaves nothing behind (goblins, UI, subscriptions); check the next area loads and
the carried picks survive the Run scene reload.

## Tunables and rules
Expose tunables with tooltips and gizmos for the holes. Use `UpdateManager`, `UI_Punch`, `UI_Shadowed` and `System_ScriptName`.
Flag every new allocation and subscription. After each stage, add a Handoff log entry to `Docs/State.md` (what changed, files,
editor steps, untested bits, allocations and subscriptions). The user commits.

## Fun test (after stage 4)
Play three areas in a row. If a fourth feels worth playing, the loop works; if picks feel same-y or dull, add gold upgrades.
