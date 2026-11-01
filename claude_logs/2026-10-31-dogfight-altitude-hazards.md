# Dogfight Rework: Altitude and Sky Hazards

- **Date:** 2026-10-31
- **Participants:** Cody Nicholson, Claude Code (Opus 5.5)
- **Status at end of this entry:** implemented and running in the Editor. Not yet played by a person or on a phone, and not committed.

## Decisions

Cody decided:

- Dogfights should stop being ships circling and waiting to fire broadsides.
- Players can climb and dive.
- The sky has hazards to avoid: rocks, sky debris and flying sea life.
- Crashing into debris does damage proportional to the debris's size.
- A sky whale can eat the player's ship, ending things immediately.

Claude's choices, all open to change:

| Topic | Choice | Why |
|---|---|---|
| What "ends the game" means | The encounter ends at once as a loss, with no gold. The save, ship and upgrades are kept | Ship loss is reserved for boarding, per the earlier decision. Say if the whale should cost more |
| Whale fairness | About 3 seconds of warning (sound, banner, marker), then a straight lunge it can't steer | An instant kill has to feel avoidable |
| First encounter | No whale until tier 1 | A new player shouldn't be eaten in their first fight |
| Whale and enemies | **Replaced by Cody's later decision:** the whale hunts only the player and avoids enemies. See `2026-10-31-whale-speed-timing-patterns.md`. Originally it hunted the nearest ship and could eat the enemy | n/a |
| Stick | Up and down now climbs and dives, with a dead zone | Altitude needs a thumb, and it's more useful than throttle |
| Throttle | Replaced by a held BOOST button | Keeps three action buttons, the mobile limit in the design plan. The slow-speed tight turn is gone |
| Climb and dive | Climbing costs up to 25% speed, and diving gains it | Gives altitude a trade-off |
| Gun elevation | Broadside 10°, bow 40° | Getting above or below an enemy escapes its broadside, and gives the bow a job |
| Bow gun | Damage 10 to 8, reload 0.45 s to 0.6 s | Once it could reach targets, tailing with it dominated |
| Cover | Rocks block shots. Debris breaks after enough hits. Sea life lets shots pass | Terrain becomes something to fight around |
| Sea life | Jellyfish and fish schools, as well as the whale | Damage also scales with their size |
| Ship collisions | Ships now push apart and take 12 damage | Fixes the known overlap gap |
| Enemy AI | Attack runs: approach firing the bow, deliver a broadside, break away to a new altitude. It weaves when tailed, avoids hazards and flees the whale | This is what actually stops the circling |

## Damage

Damage is the hazard's radius times its damage per meter, ignoring armor.

| Hazard | Radius | Per meter | Damage | On impact |
|---|---|---|---|---|
| Rock | 8–22 m | 8 | 64–176 | Stays, and stops the ship |
| Debris | 2–6 m | 12 | 24–72 | Breaks |
| Jellyfish | 3–6 m | 8 | 24–48 | Bursts |
| Fish school | 6 m | 6 | 36 | Stays |
| Sky whale | n/a | n/a | Instant loss | Mouth open only |

The starter ship has 460 hull. All values are in `game_content.json`. A 0.8 second grace period stops one rock from hitting every tick.

## Results

Measured with scripted pilots over 300 fights per case, using the real rules. Bots aren't people, so these show patterns, not how it will feel.

| Measure | Before | After |
|---|---|---|
| Typical fighting distance | About 5–30 m after the first pass, in traced fights | About 80% of fight time at 40–200 m |
| Time closer than 20 m | Not measured | 2–3% |
| Stalemate fights | 300 of 300 with one steering pattern | 0 in the cases run |
| Bow share of damage | About 5–20%, in the runs that used both guns | About 18% for the AI pilot |
| Altitude difference between ships | None possible | About 12 m on average |

| Pilot, tier 3 with no upgrades | Win rate | Crashes per fight | Eaten |
|---|---|---|---|
| AI pilot, avoids everything | 57% | 0.5 | 7.7% of fights |
| Careless, holds both triggers, ignores hazards | 74% | 0.5 | 15.3% of fights |
| Beginner, dodges only the whale | 69% | 0.7 | 3.0% of fights |

Whale catch rate per lunge was about 3–4% for the Beginner pilot, against about 16–18% for the Careless pilot, so dodging clearly works.

## Verified

- 33 of 33 EditMode tests pass, 12 of them new.
- Autoplay ran the real game flow with no console errors or warnings.
- Both whale endings were forced and seen working:
  - Player eaten: shows SWALLOWED, gives no gold, and doesn't raise the win count.
  - Enemy eaten: shows WHALE'S MEAL, pays the dogfight reward, and offers no boarding.
- With hazards on High, a dogfight drew up to about 43,600 triangles and 76 draw calls in the Editor.

## Not verified

- **Nobody has flown this by hand.** Autoplay uses AI commands, so the stick, climb and dive labels, BOOST button and keyboard keys haven't had real input. Fun can't be measured by bots.
- Nothing has run on a phone, so frame rate with the extra objects is unknown.
- The whale call was synthesized and hasn't been heard.

## Known issues

- **Careless still beats careful:** at tier 3 the careless bot wins more often than the avoiding AI pilot, because tailing with the bow is still strong. Worth watching in playtests.
- **Difficulty wall:** enemy scaling is unchanged, so it still exists. Wins at tier 6 were 15% with no upgrades, against 87% with three levels each of hull, armor and broadside.
- **Close passes:** ships can still briefly get close, as at 12 m in one screenshot, though they no longer overlap.
- **Hit size:** the hit area is still a circle plus a height limit, not hull-shaped, and raking fire isn't built.
- **Art:** rocks, jellyfish, fish and the whale are code-built placeholders, not from the Blender kit. They'd go through that project's pipeline if replaced.
