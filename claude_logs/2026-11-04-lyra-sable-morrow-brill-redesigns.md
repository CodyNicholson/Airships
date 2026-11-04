# Lyra, Sable, Morrow and Bosun Brill Redesigns in the Game

- **Date:** 2026-11-04
- **Participants:** Cody Nicholson, Claude Code (Opus 5.5)
- **Status at end of this entry:** compiled and run by automated players in Unity 6000.6.7f1 on a separate copy of the project. Not committed. Not run on a phone by Claude.

This entry builds on `2026-11-02-torpedo-main-enemy-and-dialogue.md`.

## Decisions

Cody asked for:

- Lyra, Sable, Morrow and Bosun Brill redesigned in the Blender project from written descriptions:
  - Lyra as a nomad wind mage and musician.
  - Sable as a female dark pirate assassin.
  - Morrow as a stylish navigator in emerald velvet with caged dark spectacles.
  - Brill as a barrel-shaped boatswain.
- Those redesigns moved into the game in place of the old ones, with every reference to each character updated to match.

| Area | What changed | Why |
| :--- | :--- | :--- |
| Models | New `SK_Lyra`, `SK_Sable` and `SK_Morrow`, and a new `SK_Brill`, all HQ and LQ. Before this, Brill used the generic `SK_PirateDeckhand` | The redesigns |
| Props | `SM_Prop_DriftwoodLyre` is new to the game. Its feathers and wind chimes and the blood-tipped `SM_Prop_SableDagger` replace the old files | Lyra plays a lyre, and Sable's stiletto is blood-tipped |
| Palettes | New `Nomad` (Lyra), `Shadow` (Sable and her dagger) and `Bosun` (Brill) atlases. Morrow uses the existing `Emerald` | The shared atlas is full, so new colours come in faction palettes |
| Looks | Lyra changes to `Nomad` and holds the lyre in place of the spyglass. Sable changes to `Shadow` and Morrow to `Emerald`. Brill changes to `SK_Brill` with `Bosun` | Each model needs its own palette to show its colours |
| Captain Ria | New `ria` look. It keeps exactly what she wore before: `SK_PirateDeckhand`, Crimson, a cutlass | She borrowed Brill's look, so she would otherwise have become his double |
| Hub talks | Lyra's three talks now say "tuning her lyre" instead of "studying a sky-chart" | She's a musician now. Her lines about her mother's chart still fit |
| Quarterdeck | Brill gets his own deck spot. Lyra and Morrow moved apart | With all six crew on deck, Brill stood in a spare slot on top of Sable, and Morrow's hat covered Lyra |
| Portraits | All four replaced: Lyra on a windswept cliff, Sable in moonlit rigging, Morrow at a chart, Brill on deck with a rigging block | The redesigns |

Unchanged on purpose:
- **Generic saboteurs:** the class fallback still uses `SK_Sable` with the `Obsidian` palette, so it stays a darker variant and not a copy of Sable.
- **Mechanics:** classes, skills and stats are untouched.
- **Quarterdeck chart table:** it stays at Lyra's corner as ship dressing.

## How it was checked

- **Budget gate:** all four characters and both props passed the Blender project's gate and `validate_production.py`.
- **Prefabs:** baked with `Airships/Import Art Kit` on a copy. Only the changed and new prefabs and metas were copied back, because the other prefabs differed only in float rounding.
- **Tests:** EditMode, 152 of 152 passing.
- **Capture player:** showed the quarterdeck with all six crew, plus two battles, Lyra, Sable and Morrow, then Brill, Lyra and Sable, with 0 errors.
- **Full bot campaign:** 26 of 26 missions and 0 errors, in about 27 minutes.

## Known issues

- **Brill's cutlass:** on HQ it sits about 5 cm inside his right forearm in every frame, because his forearms are thick and the cutlass grip is the shared one. LQ is clean.
- **Details too small for the models:** Lyra's wind markings, Sable's poison rag and glowing toxin, Brill's freckles and his rigging block are in the portraits only.
- **HQ-only details:** Sable's kohl, scar and venom drop, and Brill's tattoos, squint and cap pins.
- **Brill on the quarterdeck:** Cinder stands just in front of his legs.

## Testing switch: every chapter unlocked

Cody asked for the game to start with the full Voyage Log unlocked, for testing, so any chapter can be played at any time.

- **The switch:** `Campaign.AllChaptersUnlocked` in `Core/Campaign.cs`, set to `true`. Set it to `false` before release.
- **What it does:**
  - The VOYAGE LOG button works from the first launch, before the prologue.
  - Every story mission shows its title and a PLAY button. Missions already played keep REPLAY.
- **PLAY on an unplayed chapter:** it first calls `Campaign.FastForwardTo`, which completes every unplayed story mission before it. That gives their gold, XP, items, recruits, ships, mechanics and signature weapons, plus both the officer and the ship from each Davy Jones choice. Then it starts the chapter, so it opens with the crew and ships it expects.
- **Jumping backwards:** jumping again, or to an earlier chapter, never pays out twice.
- **Checked by:**
  - a new EditMode test, `JumpingToAChapterGivesWhatTheStoryWouldHaveByThen` (153 of 153 passing)
  - a capture player that opened the log on a fresh save and jumped to Tartarus Aloft. It started Act III with 12 missions done, 8 crew, 5 ships and 0 errors.
- **What a jump commits:** it writes those rewards into the save, so going back afterwards plays as a replay. Delete the save for a fresh run.
