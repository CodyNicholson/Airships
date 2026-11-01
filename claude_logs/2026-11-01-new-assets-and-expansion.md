# New Art Kit, and a Bigger Campaign, Dogfights and Deck Combat

- **Date:** 2026-11-01
- **Participants:** Cody Nicholson, Claude Code (Opus 5.5)
- **Status at end of this entry:** implemented and run by automated players. Not yet played by a person or on a phone, and not committed.

This entry builds on `2026-11-01-campaign-mode.md`.

## Decisions

Cody decided:

- Use all the new assets from `Airship_Blender_Assets_Project`.
- Build out the campaign, dogfights and turn-based combat further. The scope of "more" was left open.

Claude's choices, all open to change:

| Topic | Choice | Why |
|---|---|---|
| Assets | All 117 models, 16 flipbooks, 21 effect presets, 7 skies, the icons and the portraits are imported. See "Not used yet" for what isn't on screen | The request was all of them, and the list below is honest about the gaps |
| Roll | A quick tap of BOOST rolls, and holding it boosts, rather than adding a button | The late-game screen is already crowded |
| Enemy volleys | Announced 0.75 s ahead with glowing guns and an on-screen warning | The audit found the duel had no way to answer an attack |
| Story enemy damage | Multiplied by 1.3 in the campaign only | Warnings made fights safer, and Free Skies stays as it was |
| Deck actions | Enemies now show what they'll do next | The audit asked for intents, and it makes Guard a real decision |
| Boarding Rush | Timed prompts fill Momentum. When full, the whole crew attacks every enemy on one prompt | A reason to chase Perfect timing beyond one hit's damage |
| Sprite scale and light | The kit's sun values are halved and fog distance is 1.5x, with a floor under ambient light | The kit's values assume a different renderer, and dark hulls vanished against dark skies |
| New named characters | Captain Ria and a Haven Runner, with lines | The new escort missions needed someone to protect |

## What was added

**Art in the game:**

- Every ship, crew member, rival and enemy type uses its own kit model and palette, with its named weapons.
- Seven panoramic skies with their own sun, ambient light and fog, using a new sky shader.
- Flipbook effects on the kit's sockets, following its glob rule. Explosions ignore depth so hulls can't hide them, and caps come from the kit's HQ and LQ values.
- Seven arenas in place of one deck, and the Quarterdeck hub with crew at their stations playing Work and Talk.
- Glow from the emissive atlas, painted portraits, and icons across the HUD and menus.
- All six new clips: Run, Grapple, Talk, Work, Surrender and Taunt.
- Set pieces: the Gale becoming its wreck at the mutiny, wreckage on ship deaths, the storm wall, hazard models, ammo and missile models, the lifeboat after a scuttle, and rain.

**Dogfights:**

- Roll with brief immunity, telegraphed volleys, stern raking, and aiming at the balloon, which sinks a ship.
- Homing missiles, plus enemy flying styles: hit-and-run clippers using afterburners, long-range frigates, and the Bastion's ram.
- Allied ships, reinforcement waves, Lift-Stone mines that hurt everyone, and toxic clouds.
- New objectives: escort and collect.

**Deck battles:**

- Enemy intents, burn, stun, exposed and haste, and signature skills for each main crew member.
- Thorne can revive. Guard restores MP, and a perfect block while guarding counters.
- Momentum and Boarding Rush, usable items, boss skills, and bosses that rage when hurt.

**Campaign:** 5 new missions, for 26 in all:

- The Lift-Stone Run (Act I), Haven Under Fire (Act II) and Defend the Free Haven (Act IV).
- Side jobs: Krag's Hoard and Lyra's Currents.
- The War Room is now a staged 3D scene followed by a fight.
- A Voyage Log with half-pay replays, a cipher tracker, a chandler selling items, and loot on missions.

## Results

Measured with a headless bot harness over 20 full campaigns at each of three skill levels:

| Bot skill | Campaigns finished | Fight time, excluding story | Final duel wins per attempt | Lowest average hull left in a sky fight |
|---|---|---|---|---|
| Casual | 20 of 20 | about 24 min | 34% | 66% |
| Typical | 20 of 20 | about 23 min | 91% | 68% |
| Sharp | 20 of 20 | about 21 min | 100% | 67% |

These numbers describe bots, so they're a check that nothing is impossible, not a measure of how hard the game feels. Escorts looked too easy before the 1.3x story damage, and they weren't measured separately afterwards.

## Verified

- All 120 EditMode tests pass, 20 of them new for these rules.
- A full 26-mission campaign ran in Play mode at 2x speed in high definition with 0 errors and 0 retries, across 142 screens. It took about 18 minutes. After it, only the hub and choice layouts changed, and those were rechecked with a run through the first five missions, also with 0 errors.
- Low definition ran through the first six missions with 0 errors, using LQ models and the opaque fallback effects. At most 13 fallback effects were alive at once.
- Screenshots of every deck fight, most sky fights, the hub, choices and story scenes were reviewed. They led to fixes for overlapping banners, oversized lightning, dark interiors, framing in the chamber, and hub tags over menus.
- The Android APK builds with 0 errors, at 63 MB, up from 51 MB. It hasn't been installed on a device.
- The Gale wreck with fire, smoke and an explosion was captured in the Maelstrom sky, forced by the test driver, because the prologue itself plays that moment in a dawn sky.

## Not verified

- **Nobody has played this, and it hasn't run on a phone.** The art kit's author listed LQ smoke overdraw on a weak phone as the top performance risk, and that's still untested.
- **Low definition past Act I:** only the first six missions ran in it, and frame rate means nothing on this PC.
- **Effect tuning:** sizes and rates were judged from still screenshots, not in motion.
- **Real difficulty:** bots react instantly, roll rarely, and don't use items, Rush timing or status skills the way a person would.

## Known issues

- **Screen crowding:** the late-game flight HUD has up to 10 controls. The roll saved one, but it's still far past the audit's limit of three.
- **Comms over labels:** comms can briefly cover the turn's action label, and two side-by-side fighters can overlap at the moment of a lunge.
- **Arena fit:** some camera angles on the interiors are compromises, and the final chamber is the least tested.
- **Choice screen:** the three cards cover the two characters, who are only clear after a choice is made.
- **Dark ships:** the Mirage is a black ship and stays a silhouette in night story scenes, partly behind the comic panel.
- **Broadside smoke:** one puff per muzzle at the same moment can read as a neat white column when a ship is end-on.
- **Fallback effects:** each one creates and destroys a small object, which may cost more on a weak phone than a pooled version would.

## Not used yet

Everything from the kit is imported, and nearly all of it is on screen. These are the exceptions:

- `SM_Ship_Galleon` has no ship using it.
- The `RainSheet` preset isn't played as a particle effect. Rain is a screen overlay instead.
- Icons with no screen yet: `Ability_ThorneOverdrive`, `Ability_ThornePatch`, `Hud_Back`, `Hud_ThrottleDown`, `Module_DeckMortar`, `Target_Engine` and `Target_LiftCore`.
- `SM_VFX_*` meshes appear only in low definition.

## Open question for Cody

- The ship-loss wager from `2026-11-01-campaign-mode.md` is still left out.
