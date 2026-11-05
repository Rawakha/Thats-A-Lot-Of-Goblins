# Ideas

A parking lot for ideas that are NOT built and NOT scheduled, so nothing discussed gets lost. Things that are planned
with a decision are in State.md (Queue / planned lists) and Decisions.md. Don't build from this file without asking the
user; pick one, agree a spec, then move it to State.md.

## Tone rule for jokes and gore
Never cruel to real things. The joke is that goblins are absurdly ordinary: families, jobs, lunch breaks, paperwork. Deaths
are cartoonish (X eyes, poofs, hats flying off, purple gore), never grim. Keep it ad-safe and platform-safe.

## Main weapons
- **Leaf Blower:** doesn't kill, pushes the crowd (into holes, off cliffs, into piles to bomb). Uses the fling physics.
- **Bowling Ball Bay:** one huge ball rolls down a lane flinging goblins like pins; ten in one roll pops "STRIKE!".
- **Garden Gnome Mortar:** lobs gnomes that stand up and explode a second later (goblins stop to stare at them first).
- **Tennis Ball Machine:** rapid weak balls that ricochet between goblins and off cliffs.
- **Vacuum Cannon:** sucks goblins up from below and fires them back as ammo; the ammo counter is a goblin count.
- **Paint Roller Plane:** a huge roller flattens everything on a very low pass (only works within a metre or two of the ground).
- **Piano Dropper:** slow, cartoon whistle on the way down, one huge CLONK and a chord.
- **Scatter Gun, Rail Lance (charge, piercing beam), Tesla Coil (auto-zaps, chains), Rocket Pods (homing salvos), Goblin Grinder:**
  earlier shortlist; see State.md for the ones already planned (Egg Launcher, Dragon's Breath, Iced Up).
- **Low flight benefits belong to specific weapons** (flamethrower, scatter gun, paint roller, a "+% damage under 4 m" stat),
  not the machine gun.

## Abilities (consumables from the pick-3 cards)
- **Free Lunch Sign:** a sign drops, nearby goblins swarm it, then bomb the crowd (a lure point for the flow field).
- **Ice Cream Van:** drives across the map playing a jingle; goblins chase it in a line (perfect strafing run).
- **Inflatable Decoy Plane:** armed squads shoot it instead of you; pops with a sad squeak.
- **Mother-in-Law:** drops one enormous goblin granny; nearby goblins flee her in terror (crowd control by fear).
- **Bubble Wrap Field:** goblins stop to pop bubbles (a slow field).
- **Group Photo:** a flash freezes goblins in a radius posing; the next hit on them pays bonus gold ("SAY CHEESE").
- **Tax Audit:** goblins in a zone stop to fill out forms (a long stun, longer for bigger crowds).
- **Rubber Duck Flood:** a wave of water and rubber ducks washes a lane clean.
- **Fake Hole:** a painted hole goblins walk into (onto spikes).

## Plane passives / items (cards)
Hood Ornament Chicken (lays an egg bomb now and then), Loudspeaker (elevator music slows goblins), Bumper Stickers (each
sticker gives a stat and the plane visibly fills with them), Lucky Horseshoe (extra eject; on a good eject it flies off and
bonks a goblin).

## Cards (pick 1 of 3): flavour lines and cursed cards
Each card gets a joke line under its stats: "Bigger Bullets: they're more polite this way.", "+20% Fire Rate: HR has been
notified.", "Extra Eject: the parachute is mostly holes.", "Blast Radius +1: the neighbours complained." Cursed cards with a
joke downside: **Goblin Union** (+50% gold, but goblins sometimes strike and stop walking, which makes them easy targets),
**Honest Accounting** (shows how many goblin families you've wiped out).

## Humour hooks not built
- **Group-kill name table** for the gold/multiplier feed: 2 "Couple's retreat", 4 "Family of four", 8 "Book club",
  12 "Jury duty", 20 "Wedding party", 50 "Town meeting", 100+ "Census correction". Context ones: "Carpool" (killed in the
  air together), "Shift change" (walking into a hole), "Kids eat free" (small goblins).
- **Goblins in the crowd:** signs ("Will work for food", "Honk if you're a goblin", "Is this the line?"), a few pointing and
  laughing when another dies, one at a hole waving the others in like a traffic marshal, one taking a selfie as the bomb
  drops, one with an umbrella that blocks bullets, one dragging a suitcase, one on a tiny bike. Goblin families, a wedding
  and a funeral procession ("DEATH AT A FUNERAL") are in State.md.
- **Run end screen as an obituary / police report:** "Cause of death: Machine Gun (8,412), Confetti (214), Embarrassment (1)".
  Loading-screen goblin facts ("Goblins are 80% scream by volume"). Failed eject: "Pilot has been rescheduled." The parachute is
  a bedsheet. Gold per kill as "Goblin Insurance Payouts". "Goblins remaining" with fine print.
- **More context-message sets:** Freeze "BRRRRR", fire "CRISPY / WELL DONE", into a hole "SHIFT CHANGE", last goblin of a wave,
  low pass "CLOSE SHAVE". Explosion lines and dark lines already exist (UI_MessageSet).
- **Dark line placeholder names** use "Dursleys" (a Harry Potter reference): swap for a made-up family name before release.

## Dismemberment: next steps (first pass is built: heads and scraps, blown-apart chunks, squish, splats)
1. **Stretchy strands:** as two pieces separate, purple strands stretch between them and snap after ~0.2 s (instanced stretched
   blobs, only drawn while a piece leaves).
2. **Stumps that stay:** a headless body stays up a beat, pulses a purple fountain from the neck a few times, staggers, then
   falls; a cut-in-half goblin's lower half takes a step or two.
3. **Bite holes in living goblins:** each hit cuts a sphere out of the goblin in the goblin shader (up to 3 per goblin), faces
   behind drawn in gore purple, so damage builds before death.
4. **Meat:** pieces drip droplets in flight and leave a smear where they skid; a wet thud sound per landing; a resting wobble.
5. Smallest pieces first within the same age when the debris budget is tight; off-screen pieces first (optional).

## Feel / juice not built (Vlambeer bar: bold, stacked, as juicy or juicier)
- **Hitstop** 40-80 ms on big blasts (scaled by kills, capped); the HUD runs on unscaled time so it keeps animating.
- **Explosion flash** (1-2 frame white/orange overlay), directional camera kick and zoom punch on blasts.
- **Scorch marks** under blasts; corpses and casings that stay; **slow motion** (0.3 s at 30%) when a dark line pops.
- **Bullet impact puffs** where shots hit the ground or goblins (misses are invisible now).
- **Head fountain** pulses from the neck; bigger or tougher goblins spray more; a small spurt on non-fatal hits.
- **Plane nose tilts down in ground mode** and the gun barrels angle with it, so the arcade aim reads as a strafing run
  (visual only; answers "guns point down" comments).
- **Hit markers:** a hit-confirm punch on the crosshair when a bullet lands; rising pitch on consecutive hit sounds; scale marker
  with damage; jitter position per spawn. (Merging and springs are built.)
- **Plane wash** pushing grass flat when flying low; explosions flattening or scorching grass; goblins parting it.

## Aim and weapons architecture (do with the second weapon)
Move weapon-level aim settings into a per-weapon `Weapon_AimProfile` (strafe distances, max strafe pitch, assist strength,
cone, range, yaw/pitch limits, stickiness) that Weapon_Aim reads from the equipped weapon and blends between on a swap. Shot
speed and inherit velocity should come from the weapon's live stats (today Weapon_Aim has copies that must be matched by
hand). Ballistic weapons (eggs, mortars) need a lob solver flag instead of the straight-shot maths.

## Run structure ideas
- Meta progression: a permanent goblin-themed upgrade tree between runs ("The Goblin Bureau of Extermination") bought with a
  second currency; its nodes mostly unlock weapons, abilities and cards, plus small stat bumps. Runs stay fun without it.
- Fuel: Claude suggested dropping it (no job left with plane health as the fail state and no wave timer); still undecided.
- Armed squads and shield tortoises should be built after the data-only horde rewrite.
