# Agents (Codex and others)

This repo holds two Unity projects:
- **`Thats A Lot Of Goblins v2/`**: the game being built now (the plane version). All new work happens here.
- **`Thats A Lot Of Goblins v1/`**: the old tower-defence version. **Read-only reference**: copy and adapt ideas or code into v2, never edit v1, and never reference it across projects.

This project is mainly built with Claude; you're picking up where it left off. Everything you need is in the v2 folder:

1. **`Thats A Lot Of Goblins v2/CLAUDE.md`**: the project brief, code conventions and working rules. They apply to you exactly as written (the user commits, one module at a time, spec before code, `UpdateManager` instead of `Update()`, GC tracking, `System_ScriptName` naming).
2. **`Thats A Lot Of Goblins v2/Docs/State.md`**: what's built, the current module and how far it's got, the queue, and editor setup still to do. Start from "Current module".
3. **`Thats A Lot Of Goblins v2/Docs/Decisions.md`**: choices already settled. Don't undo them without asking the user.

The root `CLAUDE.md` describes v1 (Goblin TD); ignore it for v2 work.

## GameObject authoring workflow

The user authors visuals with real GameObjects, MeshFilters and MeshRenderers in Unity. Provide inspector controls
that place and configure those objects; later custom rendering/optimisation must carry over their authored meshes,
materials and transforms. Do not replace this workflow with data-only authoring controls.

Chance-based visuals use `Horde_VariationGroup`: direct-child GameObject options, editable Chance (%) controls that redistribute the remainder (stored as compatible weights), Head/Body
anchor, preview buttons and one choice per group per goblin life. Preview visibility must never change spawn
chances. Groups are independent and cannot nest; empty options mean None. Future hats/horns/equipment visuals
use the same workflow. Gameplay behaviour stays separate. Horde_RenderGroup draws only selected options using
reused instance buffers; no per-goblin renderer subscriptions.

Goblin eye layouts use editable quad-card GameObjects authored with `Horde_EyeCards`. The existing Eyes placement
is local Y=1.2 with component Height=0: preserve it. The inspector's Create Two Eyes / Cyclops Variation Group
button wraps it without moving it and creates a separately authored central card. Each layout has its own card
size/position/rotation; preview and weights live on the common group. At bake, actual source meshes/transforms
are combined per layout and carried by the group's animation anchor. Never replace the editable source objects.
`Goblins/Eyes` handles blinking, gaze, hurt squint and black square-ended death X's with length/thickness controls.
Per-goblin size/asymmetry remain on Horde_GoblinType; the central eye ignores pair asymmetry. The implementation
is waiting for Unity setup, visual tuning and runtime verification; read Docs/State.md for details.

When you finish a module, or stop partway, update `Docs/State.md` ("Current module" and a new "Handoff log" entry: what changed, files, editor steps, untested bits, new allocations/subscriptions). Add settled choices to `Docs/Decisions.md`. The next agent depends on it.
