# Marketing and community

Notes from planning chats. Not instructions for coding agents: read it only for marketing tasks, and never post anything for
the user (they post themselves).

## Where things stand (2026-11-04)
- The user's other game, **Implications**, did very well early with an account under 100 followers: one X post at 17k views
  (previous best 98), others at 5k and 1k, and a top-1% r/Unity3D post with 100k+ views and close to 2,000 upvotes across 3
  posts, all with grey-box visuals. So the hook beat the polish. Implications is the proven one: keep it fed, and link it
  from every goblin post.
- The goblin game ("That's a Lot of Goblins", plane vs a huge horde) is a small prototype made as an internal burnout-break game
  jam. Frame it that way: no promises, "thinking about continuing it".

## The clip (10-15 s, 60 fps, sound on)
- Frame 1 is already mid-action: the horde snaking between cliffs, goblin counter visible. No intro or title card.
- The beat: a low pass, a carpet bomb down the line, screaming goblins in the air, a context message (a dark line is the
  punchline), the counter slamming down. Cut while it's still exciting; end on carnage left on the ground.
- Record a build or full-screen Game view with gizmos and stats off. Set the explosion Message Set's Dark Chance to 1 for the take
  and back to 0.15 after. Debug waves (Wave_Director, Debug Waves, Space to start/stop) make takes easy.
- Make the plane the lightest, most contrasting thing on screen (purple world, green goblins, warm cream-and-orange plane).

## Posts
- **Reddit (post two first, then others over a day or two, never all at once):** r/IndieDev (best fit), r/IndieGaming,
  r/Unity3D (add the tech angle in the first comment: ~1,500 physics goblins, instanced rendering), then r/gamedev (strict about
  self-promo; use feedback threads), r/indiegames, r/incremental_games (check rules; Feedback Friday is safest; frame it
  around upgrades and scaling). Wait on r/roguelites until runs, cards and upgrades exist. Read each sidebar first.
- Titles: "Took a break from Implications to fight burnout with a mini game jam. Now I can't stop carpet bombing goblins" (where
  Implications is known); "My burnout cure: 1 plane, 1,500 goblins, zero restraint" (cold audiences); "Bombing goblins from a
  plane... small prototype I've been working on. Is there a game here?" Post a first comment straight away that asks a
  specific question (roguelite or incremental? which would you rather play?).
- **X caption:** "Feeling burnt out... so I did a little game jam. Turns out bombing goblins is suuuuper fun" plus the egg launcher
  seed ("Next weapon is an egg launcher that sometimes hatches chickens that fight the goblins. What other absurd weapons should
  I add?"), #gamedev #indiedev, and a pinned reply linking Implications.
- Timing: Reddit is mostly US audience (post around 8-10am US Eastern); reuse the slot that worked for Implications.
- Reply to comments quickly in the first hour or two. Note which moments people mention (dark lines, screams, physics): that is
  the real hook for the next clip.
- Replying to "guns point down, use a helicopter?": "It's a deliberate arcade cheat so you can strafe the horde without
  dive-bombing every few seconds; a helicopter could be a fun unlock. What would you want it to do differently?"

## Channels beyond Reddit and X
- Cross-post the same clips as vertical shorts to TikTok and YouTube Shorts (horde and physics games do well there).
- A Steam page for Implications as soon as possible: wishlists are what the algorithm reads at launch.

## Streamers and YouTubers
- They sell horde roguelites (Vampire Survivors, Brotato), but need a playable demo, not clips. Do outreach once there is a
  fun 15-20 minute loop and a Steam demo, ideally timed for Steam Next Fest. Target mid-size creators (roughly 10k-200k subs)
  who play roguelites and chaotic indies; short personal emails with a key and a 30-second clip.

## Copying worries
Encryption won't stop someone describing the game from a clip. IL2CPP plus an obfuscator (and Managed Stripping High, debug
off) stops casual code ripping. The real protection is feel and juice, the horde tech, the humour and an audience that knows
it's the user's game.
