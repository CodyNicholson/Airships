# Symphonic Dogfight Music and Missing Sound Effects

- **Date:** 2026-11-01
- **Participants:** Cody Nicholson, Claude Code (Opus 5.5)
- **Status at end of this entry:** compiled and run by automated players in Unity 6000.6.7f1. Nobody has listened to it, played it by hand or run it on a phone, and it isn't committed.

This entry builds on `2026-11-01-music-prologue-dodge.md`.

## Decisions

Cody asked for more exciting, dramatic symphonic battle music for the dogfights, and sound effects for actions that have none.

Claude's choices, all open to change:

| Topic | Choice | Why |
|---|---|---|
| Songs | Two new dogfight songs, taking turns per fight, plus a defeat jingle | One theme wears out over a campaign, and a failed mission used to end in silence |
| Boss and finale fights | Keep Leviathan and The Sovereign Descends | Those songs were written for those story moments |
| Broadsides at Dawn | No longer played, and its WAVs are still in `Resources/Music` | Deleting wasn't asked for. Removing them would save build size |
| Sound source | Rendered to WAV by the music project, in `Resources/Sfx` | They can be measured and auditioned outside Unity. The first 12 sounds are still synthesized at startup |
| Stand-in sounds | Replaced where the sound was clearly wrong for the action | A UI "denied" buzz warned of enemy volleys, and pistols fired with a cannon sound |
| Bad-news banners | Now play a falling brass sting | "DEFEATED" used to play the same bright major arpeggio as a win |

## Songs

| Song | Key and tempo | Loop | Sections |
|---|---|---|---|
| Wings of the Tempest | C minor, 168 BPM, 4/4 | 68.6 s | alarm (intro), scramble, theme, dive, heart, rise, theme up a tone, turn |
| Gauntlet of Thunderheads | E minor, 186 BPM, 6/8 | 58.1 s | gather (intro), hunt, chase, eye, ambush, chase, last |
| Defeat Lament | C minor, 72 BPM | one shot, 12.4 s | fall. It fades out as soon as a retry starts another song |

Both battle songs have a danger layer that fades in as the old one did. The prologue's first fight now uses Wings of the Tempest.

## Where the new sounds play

| Moment | Sound |
|---|---|
| Flight | ShipRoll, BoostIgnite, EngineLoop (follows the throttle), ShotWhiz, NearMiss, ShipFlyby, GliderLaunch |
| Hits and hazards | FlakBurst, MortarBurst, TimberBreak, JellyPop, Raked, HullCreak, WhaleGulp |
| Hooks | HookThrow, HookLand, RopeSnap, ChainTaut |
| Ship abilities | MissileLaunch, Overdrive, SignatureFire, AmmoLoad, Pickup |
| Warnings | VolleyWarning, HullAlarm, ThunderRumble, LightningCrack, ThunderDistant |
| Banners | WarHorn (fight start, reinforcements, boarding rush), DangerSting, LevelUp |
| Deck fights | Pistol, MagicCast, GuardUp, BladeClash, BodyFall, Burn, StatusHit, Stunned, BossRoar, Revive, GrappleSwing, Plunder |
| Shop | CoinPurchase |

Other changes in `AudioBank`: 16 voices instead of 10, and a clip can't restart within 40 ms of itself, so a broadside doesn't stack into one loud phasey hit.

## Verified

- All 40 tests in the music project pass, including new ones that render every effect. An existing song still renders sample-identical after the synth changes (checked on Victory Fanfare).
- Renderer: 0 clipped samples, peak -1 dBFS and a clean loop seam on both battle songs, with 0 checker warnings on all three scores.
- All 44 effects pass the checks for clipping, silence, end clicks and loop seam.
- A script confirmed every `Sfx` name used in code exists, every clip has a file, and every new sound is played somewhere.

Unity checks, all run on a separate full copy of the project with LFS files pulled, so the open editor wasn't disturbed:

- All 124 EditMode tests pass, including `EverySoundEffectClipExists`.
- A 13-minute autoplay run in Play mode, at 3x game speed, reached 24 finished missions and 28 dogfights with 0 game errors. The only logged exception was in Unity's own QuickSearch indexer at startup. An earlier run that stopped partway reached the same screens. Ordinary dogfights alternated between the two new songs, and boss fights kept Leviathan.
- A directed Play mode test passed all checks: all 55 sounds play, and both new songs move from intro to loop with their layers 0 samples apart. Compression kept the loop lengths exact, the danger layers fade in and out, the engine follows the throttle and fades out, and the defeat jingle gives way to a retry without using up the victory fanfare's cooldown.

## Not verified

- **Some call sites never ran in a real fight:** the autoplayer never triggered AmmoLoad, BoostIgnite, Burn, CoinPurchase, FlakBurst, GuardUp, HullCreak, MortarBurst, Revive, ShipFlyby, StatusHit, Stunned, ThunderDistant or WhaleGulp. Each clip plays when called directly, but the code that calls it hasn't run. The defeat jingle was also only tested directly, and the new songs' danger layers never came up in autoplay.
- **Nobody has heard any of it.** Volumes in code are estimates, and the mix of effects against music needs a listen. Check WarHorn over each song's intro first, since both start together.
- **Realism:** the `orch_` voices are synthesized, so they aim for a section sound and won't pass for a recorded orchestra.
- **Phones:** decompressing the effects at startup adds load time and memory that hasn't been measured.

## Known issues

- **Git LFS:** the project was first opened with LFS pointer files in place of its art and older songs. `git lfs pull` has since restored them.
- **Layer drift on Leviathan:** autoplay logged one reading of 941 samples (21 ms) between its two layers late in the run, and nothing higher. That song wasn't changed in this work. A real offset would blur its danger layer, so it's worth a listen.
- **Repetition:** in autoplay, VolleyWarning fired about 150 times and Raked about 84 times over two runs. They may need thinning if they get tiresome.
- **Settings file:** opening the project in Unity emptied `m_RuntimeSettings` in `UniversalRenderPipelineGlobalSettings.asset`. This work didn't change that, and it was left uncommitted and untouched for Cody to decide on.
- **Meta files:** Unity has generated `.meta` files for the new WAVs. Commit them along with the WAVs.
- **Repository size:** the new songs add 50 MB of WAV and the effects 8 MB, all through LFS.
