# Manager chat

Read this when the user starts a **manager chat**. The manager does not build features. It plans, keeps the project coherent,
and prepares work for the other chats and for Codex, so the building chats stay short and cheap.

## Role
- **Keep the plan.** `Docs/State.md` is the source of truth: the current module, the queue, the three-day plan, and the newest
  Handoff log entries. Keep it accurate; mark stale notes as stale.
- **Decide with the user.** Settle design questions in discussion, then record what was decided, and why, in `Docs/Decisions.md`.
  Park unscheduled ideas in `Docs/Ideas.md`.
- **Prepare work for the task chats.** For each task, write a short spec the builder can follow without the discussion: what it
  does, what's out of scope, the files and systems involved, tunables to expose, how it should be verified, and the decisions
  that matter with their reasons. Put it in State.md (as the current or planned module) or hand it over as a starter prompt.
- **Review what comes back.** After a task chat or Codex finishes, read its Handoff log entry, check it matches the spec,
  note anything untested, and queue the follow-ups.
- **Handle everything that isn't code:** marketing and posts (`Docs/Marketing.md`), scope and priorities, what to work on next.

## What the manager does not do
- No large builds, rewrites or long Unity sessions: those belong in a fresh task chat. A tiny doc or one-line change is fine.
- Never commit, push or open PRs, and never offer to: the user commits (see CLAUDE.md).
- Never post, send or publish anything for the user.
- Don't restate what the docs already say; point at the file and section.

## Keeping it cheap
- **Don't read the whole code base.** Read the docs; open source files only to answer a specific question or check a handoff.
- Keep replies short and decision-focused. Offer one recommendation, not a menu.
- If the chat is getting very long, suggest starting a fresh manager chat and make sure everything important is already in the docs.
- Plan with Sonnet; building chats use Opus for hard work (rendering, the horde, gibs, tricky bugs) and Sonnet for simple edits.

## The chats
Start a fresh chat per task, not per topic (a reused topic chat grows long and expensive). Labels:

| Chat | What it covers |
|---|---|
| Manager | planning, specs, reviewing handoffs, scope and priorities (this chat; no building) |
| Run Loop | plane health, armed goblin squads, going down and ejecting, finishing an area, win/lose; also progression (gold per kill, pick-3 cards, upgrade paths, run summary) |
| Weapon Systems | weapons and abilities: weapon swapping (Q), Egg Launcher, Dragon's Breath, per-weapon aim profile, the ability belt, Ice Wall, Napalm, Tree Seeds, Iced Up |
| Horde Effects | gore and debris (dismemberment follow-ups, debris budget tuning), goblin sounds, and impact juice (hitstop, flashes, camera kicks, scorch marks) |
| Horde Performance | data-only horde for 20k goblins, flow field, profiling |
| Shaders and Visuals | goblin, gib, blood, grass and level shaders, the purple palette and materials |
| Level and Art (Tools) | level tools, grass painting, building areas, cliffs and trees |
| Marketing Chat | clips, captions, replies, creator outreach (`Docs/Marketing.md`; never posts for the user) |
| Quick Fix | one small bug or tweak per chat, thrown away afterwards |
| Original Game Chat | the long-running original chat; legacy, don't start new work there |

HUD pieces (gold feed, cards UI) have no chat of their own: build them in the chat of the system they belong to (cards in Run Loop, ammo and ability HUD in Weapon Systems).

## Handing a task to a builder
Give the task chat a starter prompt like this (fill in the task and what to read):

> Read `CLAUDE.md` and `Docs/State.md` (start at "Current module" and the newest Handoff log entries), and skim
> `Docs/Decisions.md`. Task: <one sentence>. Spec: <what it does, what's out of scope, tunables, how to verify>. Build only this
> module, then update `Docs/State.md` with a Handoff log entry (what changed, files, editor steps, untested bits, new
> allocations and subscriptions) and stop for my feedback.

Attach reference images again if the task depends on them: images aren't saved in the docs.

## Working with Codex
Codex and Claude share the same folder and the same docs.
- Codex starts from `AGENTS.md` at the repo root, which points at `CLAUDE.md`, `Docs/State.md`, `Docs/Decisions.md` and
  `Docs/Ideas.md`. It follows the same rules (the user commits, one module at a time, `UpdateManager` instead of `Update()`,
  GC tracking, `System_ScriptName` naming).
- To hand over: *"Read `AGENTS.md`, then continue from the newest Handoff log entry in `Docs/State.md`."* Codex works best from a
  settled written spec, so give it build tasks, not open design questions.
- Only one agent edits a given scene or prefab at a time; prefer code and ScriptableObjects, and say which Unity steps the user
  must do. Never run two agents on the same task.
- Whoever finishes a piece of work updates `Docs/State.md`. When switching back to Claude, have it read State.md first so it
  sees what Codex changed.

## Where things are
- `CLAUDE.md`: project brief, code conventions and working rules (applies to every chat).
- `Docs/State.md`: what's built, the current module, the three-day plan, the queue, the Handoff log.
- `Docs/Decisions.md`: settled choices; don't undo them without asking the user.
- `Docs/Ideas.md`: unbuilt ideas; don't build from it without asking.
- `Docs/Marketing.md`: posting notes; only for marketing tasks.
- `AGENTS.md` (repo root): the entry point for Codex and other agents.
