# Egg Launcher

User-approved scope (2026-11-05): a main weapon that shoots eggs; a chance to hatch chickens, which seek the nearest living goblin and attack. Build and tune one step at a time.

1. **Egg firing (implemented, Unity verification pending):** hold the normal fire button to lob eggs. Crack on ground/scenery impact. Tunable flight, firing rate, magazine/reload, impact radius/damage, recoil, sounds and editable visual prefabs.
2. **Hatching (not built):** chance-based chicken spawning on impact (earlier starting suggestion: 50%), authored chicken prefab, lifetime and live-count limit. Settle exact spawn/cleanup rules before coding.
3. **Chicken combat (not built):** seek the nearest living goblin, approach and peck, retarget after its death. Settle navigation/obstacles, no-target behaviour and attack rules before coding.

Stop after each step for the user's Unity tuning. Weapon cycling, upgrades, bomb-flung chickens and other earlier ideas are outside step 1.

## Unity setup for step 1

1. Let scripts import, then choose **Goblins > Weapons > Create Egg Launcher Starter Prefabs**. It creates assets under `Assets/Prefabs/Weapons/Egg Launcher`: weapon, egg projectile, crack effect, egg mesh and materials. Repeating selects the existing weapon; a partial pre-existing folder is preserved by creating a unique folder instead.
2. Open **Egg Launcher.prefab**. The body, barrel and muzzle are editable GameObjects with real meshes/materials/transforms. Position these to suit the plane; keep the root at the equipment mount's origin. The Muzzle group and Preview Muzzle already point to its Muzzle.
3. On the plane's **Equip_Loadout**, replace the machine gun entry in **Starting Equipment** with Egg Launcher. Keep the carpet bomb/ability entry. Alternatively, assign Egg Launcher to **Debug Equipment**, enter Play Mode and press **Equip Debug Equipment**.
4. Hold Fire, empty the magazine, release through the reload and fire again. Press R with a partly empty magazine. Check weapon name/ammo, reload bar and crosshair reload feedback. No new HUD wiring is needed.
5. Test at cruise and low height, turning and descending, on grid ground/grass shelves, against cliffs, and beyond the island/over holes. Eggs use existing projectile collision and cached surface heights; they crack on the lower bed outside the island. Lifetime expiry removes an egg without an impact. No chicken spawns yet.
6. For the landing marker, use **UI_Crosshair > Placement > ShotPoint**; Cursor mode continues following the mouse. Selected launcher gizmos draw its arc and impact radius.
7. Save the tuned prefabs and Run scene.

## Starting tunables

| Setting | Default | Where |
| --- | --- | --- |
| Fire Rate | 2 eggs/sec | Launcher base stats |
| Magazine Size / Reload Time | 6 / 2 sec | Launcher base stats |
| Projectile Speed | 22 m/sec | Launcher base stats |
| Range | 160 (lifetime = Range / Projectile Speed) | Launcher base stats |
| Damage / Radius | 1 / 1.2 m, small ordinary splat | Launcher base stats |
| Spread | 0 degrees | Launcher base stats |
| Launch Angle | 20 degrees above horizontal | Launcher Egg Flight |
| Inherit Velocity | 0.25 | Launcher Projectile and Muzzles |
| Gravity / Hit Radius / Linger | 25 / 0.2 m / 0 sec | Egg Projectile |

The launcher uses shared assisted yaw and its own fixed elevation, so it still lobs in air mode. Flight tunables are starter values for hand tuning. Aim prediction uses the centre path against cached terrain only: scenery and the egg's collision radius can produce earlier impacts. Spread is omitted from prediction. A multi-muzzle setup previews only the assigned Preview Muzzle. After a hitch, at most one egg fires from the current muzzle, avoiding skipped flight/collision paths.

The starter crack is a brief pooled shell-and-yolk mesh effect. Replace or edit it freely; use SoundData assets for firing/impact sounds. No sounds are assigned automatically. Keep projectile/effect prefabs free of colliders, Rigidbodies and self-moving/self-destroy scripts, as required by Effect_Pool.

## Validation and performance

Runtime and editor C# compiled against installed Unity references. Thirteen isolated checks of production weapon code passed with mocked Unity/services: main slot/default magazine, upward flight/inherited speed, lob in air mode, ammo per egg, no backdated eggs, manual/automatic reload, hitch cap, terrain prediction, prediction agreement within 10 cm of the projectile integrator on flat ground, and machine-gun pitch behaviour.

Unity import, menu asset creation/repeat action, actual collision/visuals/HUD, scene persistence and profiling remain unverified. Terrain preview samples up to 64 points per arc (two arcs/tick), with no physics queries; changing frame time and terrain edges can affect accuracy.

No new frame/event subscriptions or steady-state managed allocations. Existing equip stat/part arrays allocate on equip; pools allocate on first use/growth; optional audio uses existing coroutine allocations. The menu allocates editor-only meshes/materials/prefabs. No scene or existing prefab was edited automatically.
