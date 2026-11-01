# Soundtrack, a Filmed Prologue and a Longer Dodge

- **Date:** 2026-11-01
- **Participants:** Cody Nicholson, Claude Code (Opus 5.5)
- **Status at end of this entry:** implemented and run by automated players. Nobody has listened to it, played it by hand or run it on a phone, and it isn't committed.

This entry builds on `2026-11-01-new-assets-and-expansion.md`.

## Decisions

Cody decided:

- Use the music from `Video_Game_Music_Generator_Project`, wherever each song makes the most sense.
- Give the prologue more animation and storytelling, with more exposition and character introductions.
- Make the dodge roll move the ship farther sideways, so dodging actually works.

Claude's choices, all open to change:

| Topic | Choice | Why |
|---|---|---|
| Which songs | The three in `music/` plus the four examples inside the composer skill | "The music contained in" the project covers both, and the three alone had no hub, deck-battle or victory music |
| Roll distance | About 48 m, up from about 21 m: 0.55 s at 88 m/s, immune for the whole roll, 6 s cooldown unchanged | The largest ship's hit radius is 15 m, so even the Gale ends up well clear of where shots were aimed |
| Prologue shape | Two new scenes before the first fight, the mutiny as its own scene between two fights, and a longer ending | The mutiny used to happen in comms lines during a fight, where it was easy to miss |
| New story details | Vane was pulled off an Armada prison barge at twelve, Morrow is the navigator and Vane's oldest friend, Odessa keeps the Lift-Core, and Mirren is shot on deck | The story needed them, and they fit later lines such as "You were the best thing I ever stole" |
| Danger layers | The battle songs' second layer fades in below 40% hull or crew health, or when a boss rages. The adventure song's fades in during storm scenes | The songs were composed with these layers |
| Skippable | Tapping moves to the next shot, and SKIP ends the scene | Replays and second playthroughs shouldn't have to sit through it |
| Loops | Each song is split into intro and loop clips, scheduled on the audio clock | Unity ignores the loop points stored in a WAV |

## Where each song plays

| Song | Where |
|---|---|
| Skyward Regatta | Title, story scenes, Davy Jones' Choice, crew assignment, and the prologue's opening |
| Harbor of Lanterns | Quarterdeck, port, and after a mission |
| Broadsides at Dawn | Sky fights |
| Cannons Over Cumulus | Deck battles |
| Leviathan of the Upper Winds | Boss fights in the sky and on deck, and when the whale appears in Free Skies |
| The Sovereign Descends | The mutiny and escape, the fall of the Gale, the War Room, and the whole final mission |
| Victory Fanfare | Winning a mission, a boss's Davy Jones' Choice, and winning in Free Skies, at most once in 45 s |

The game's old synthesized shanty is gone. Sound effects are still synthesized.

## The prologue, in order

1. **The Shattered Sky (scene):** the broken world, the Undertow, Lift-Stone, the Armada, everyone else, and the Sovereign Gale.
2. **Aboard the Gale (scene):** name cards for Vane, Cassian, Mirren, Odessa, Tobin and Morrow, then Cassian's plan for the vault.
3. **Sky fight:** two Armada patrols.
4. **The Mutiny (scene):** the celebration, Morrow turning on the crew, the Lift-Core blast, and the Armada closing in.
5. **Sky fight:** the burning Gale chases the scout.
6. **Deck fight:** two guards.
7. **The Fall of the Gale (scene):** the wreck falling, Cassian in irons, Vane escaping, and the title card.

Scenes come from `shots` data in the campaign content, played by `Scripts/Cutscene.cs`, so later missions can use them too. The scenes add up to about 2 minutes if every line is read. Cassian's later twist isn't revealed.

## Verified

- All 123 EditMode tests pass, including new ones for roll distance, the prologue's data and every song existing.
- A full 26-mission campaign ran in Play mode at 2x speed with 0 errors and 0 retries. Music was playing at all 83 screen checks. After that run, only the Undertow's colour settings and one subtitle's colour changed, and all tests passed again. The subtitle was checked in a prologue-only run. The Undertow's return to its original settings wasn't captured again.
- All six looping songs were measured 13 s after starting. Each had moved from intro to loop, and the two layers of the four layered songs were at the same sample. Compression kept the loop lengths exact.
- The renderer reported 0 clipped samples for all seven songs, and a clean loop seam for all six looping songs.
- Every prologue shot was reviewed in screenshots after staging.
- The bot harness finished all 60 campaigns at 3 skill levels.
- The Android APK builds with 0 errors, at 74 MB, up from 63 MB. It hasn't been installed on a device.

## Not verified

- **Nobody has heard any of this.** The songs and the loop seams after compression were checked by measurement only, so the mix against sound effects and whether each song suits its moment need a listen.
- **The danger layers** were only measured at silence and in sync. Fading in during real play wasn't listened to.
- **Whether the longer roll feels right.** A test measures the distance, but only a person can say if it's too far, too fast or disorienting.
- **Phones:** not run on a device. Memory and decoding load from music are untested there.
- **Prologue pacing** and animation were judged from still shots, not in motion.

## Known issues

- **Mines and the longer roll:** bots now succeed on The Lift-Stone Run's collect fight about 74% to 80% per attempt, down from 91% to 100%. They probably roll into mines more often, since a roll now goes farther. Retries are free, and a person can choose which way to roll, so this was left alone.
- **Undertow colours:** the bright magenta patches are the art kit's deliberate "plasma" panels, set in `story_env.py`. They can look like a rendering fault, and the earlier log entry wrongly listed them as fixed.
- **Repository size:** the songs add 92 MB of WAV files to `Assets/Game/Resources/Music`, which may suit Git LFS. The renderer treats WAVs as build output, and `tools/import_music.py` recreates these from renders.
- **The roll's camera:** the chase camera wasn't changed, so a 48 m slide may look abrupt.
