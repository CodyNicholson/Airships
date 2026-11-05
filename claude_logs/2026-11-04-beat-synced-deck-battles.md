# Beat-Synced Deck Battles

- **Date:** 2026-11-04
- **Participants:** Cody Nicholson, Claude Code (Opus 5.5)
- **Status at end of this entry:** compiled, EditMode tests passing and built into an APK in Unity 6000.6.7f1. Not committed. Not played by hand. No automated Play-mode run of a battle was made.

This entry builds on `2026-11-04-lyra-sable-morrow-brill-redesigns.md`.

## Decisions

Cody asked for:

- Deck battles synced to their music, taking the idea, not any content, from games whose turn-based fights follow the battle track.
- Several new battle songs, exciting and suspenseful, sharing one beat pattern that repeats every three seconds.
- The GUARD, ATTACK, SKILLS and ITEMS buttons removed. A round sword button for attack, a small button per skill with its own icon (Cinder's face for Rook's Cinder's Flame), and a Rum button.
- Three skills per character.
- Three seconds to pick a move once the song begins, the move happening on the beat, and the touch prompts kept for bigger hits and smaller wounds.

| Area | What changed | Why |
| :--- | :--- | :--- |
| Music | `Boarding Cadence` (D minor, orchestral), `Riggings Ablaze` (E minor, big band and chip) and `Tyrant's Tempo` (C minor, bosses). All 160 BPM 4/4, base plus danger layer. Built by `music/build_beat_battle_scores.py` in the music project | Two bars at 160 BPM are exactly 3 s, the "beat sequence". Every sequence opens with crash, bass drum and taiko and closes with a snare and taiko pickup on beat 8. Intros and loops are whole sequences, so the grid survives the loop seam |
| Song choice | `GameMusic.DeckBattleSong`: deck phases scored with Leviathan or Sovereign get `Tyrant's Tempo`, the rest alternate the other two | Every deck battle must play a song on the shared grid. Air fights and story keep their music |
| Beat clock | New `Core/BattleBeat`. `AudioBank.TryGetSongTime` gives the song's position on a smoothed audio clock, less `audioLatencySeconds` (0.05) | Turns follow what the player hears, not frame time |
| Turn flow | A crew turn opens on a sequence's first beat and lasts that sequence (3 s). Picking starts the move on the next beat. Enemies strike on the next bar | Turns land on the music |
| Running out of time | The crew member hesitates ("TOO SLOW!") and loses the turn: a new `BattleActionKind.Pass` that does nothing and earns no momentum. The next fighter, often an enemy, goes on the coming bar | Cody's follow-up: an auto-attack on timeout made waiting free, so the clock had no teeth |
| Count-in | READY?, then 3, 2, 1 on the beats before the first turn, then FIGHT! | The first sequence plays under the boarding entrance, so the first turn opens on the loop's big downbeat |
| Touch prompts | Every pattern rewritten on the eighth-note grid, first target on a whole beat, all within one sequence. Prompts run on the music clock | Taps, swipes and drags land on the music |
| Action pad | Round sword ATTACK button, three round skill buttons with icon, name and MP cost, and a Rum button with its count. A ring drains over the 3 s and a pip goes dark each beat, red for the last two | The requested layout, and the time left shows on screen as well as in the music |
| Targets | Tap an enemy, on deck or in the enemy list. A red reticle marks the target. Heals go to the most hurt ally, and revives to the fallen first. Rum goes to the most hurt ally | No time for target menus in a 3 s window |
| Skills | `CrewDef.skills`: three per crew member, opening by level. Rook: Aimed Shot, Cinder's Flame, Heavy Flak. Four new skills give the crew who had two a third: Brill `belaying_pin`, Hask `quick_draw`, Wren `smelling_salts`, Ivo `static_snare` | Three buttons, one skill each |
| Icons | `Hud_Attack` and 23 `Skill_` glyphs in the Blender project's `fx_icons.py`, on medals coloured by kind: crimson physical, indigo magic, teal healing, ember for Cinder | One picture per skill. Cinder's icon is her iron head, blade ears, ember eyes and brass barrel snout |
| Guard | Gone from the UI. A perfect block now counters on its own, and a crew member's plain attack wins back 10% MP | Guard was the counter's trigger and the only MP refill |
| Port | The chandler no longer sells the Saboteur Charge | The pad has only the Rum button, so a charge can't be used |

Unchanged on purpose:
- **Timing grades and multipliers:** windows and damage changes are as before.
- **Boarding Rush:** its button appears above the pad when momentum is full.
- **Enemy skills and AI:** enemies keep their class skills.

## Follow-up: a louder beat, and the beat on screen

Cody played a battle on the phone: the music wasn't dramatic enough, the beats weren't pronounced, and it wasn't obvious the actions were timed to the music.

| Area | What changed |
| :--- | :--- |
| Pulse | `section()` in the builder now writes the same pulse into every section of every song: a kick on every beat, snare and clap on 2 and 4, an orchestra hit (string and brass stab, crash, bass drum, taiko, timpani) on each sequence's first beat, a cymbal swell over beats 7 and 8 and a snare roll on beat 8. The quiet rim-click drum sections are gone |
| Mix | Drums went from the quietest rhythm voice to the loudest, about -18 dBFS RMS against -22 to -25 for the brass leads. Measured on the base stems, each sequence start lands about 3.5 dB louder than the other beats, and every beat about 1.3 dB over the gaps between beats |
| Danger layer | Plays at 45% through every deck battle and swells to full when the crew is in trouble or a boss rages. Before, it was silent until then |
| On screen | Every fighter bounces on each beat. Eight pips under the turn banner light the beat, the first one gold. Each sequence's first beat flashes the screen's edges gold and jolts the camera |
| Ticks | A new high `Sfx.Tick` sounds on the count-in and on the last three beats of a crew member's window |

## How it was checked

- **Songs:** the score checker passed all three with 0 warnings. Renders peak at -1 dBFS with 0 clipped samples and the loop seam is OK. Loop starts at sample 132300 (3.0 s), and the loops are 18, 22 and 18 sequences. Onset strength at sequence starts averages +3 to +4 dB against about 0 dB on off-beats.
- **Icons:** checked by eye on a contact sheet. `validate_production.py` passes.
- **Tests:** EditMode, 163 of 163 passing, including 11 new ones in `BeatBattleTests`.
- **Build:** the APK built with 0 errors.

## Known issues

- **Not played:** nobody has played a beat battle yet, by hand or by bot. Layout and feel are untested on a phone.
- **Audio latency:** `audioLatencySeconds` is a guess. With Bluetooth headphones the music will lag the rings by around 0.2 s.
- **Saboteur Charges:** missions still hand them out, and saves may hold some, but nothing can use them now.
- **Skill names:** under the small buttons at 19 pt, which may be small on a phone.
