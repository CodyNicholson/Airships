# Torpedo, Main Enemies, Tap-to-Read Dialogue, Whales and New Portraits

- **Date:** 2026-11-02
- **Participants:** Cody Nicholson, Claude Code (Opus 5.5)
- **Status at end of this entry:** compiled and run by automated players in Unity 6000.6.7f1 on a separate copy of the project. Not committed. Not run on a phone by Claude.

This entry builds on `2026-11-01-dogfight-music-and-sfx.md`.

## Decisions

Cody asked for:

- The new character portraits in the game, with the music and sound effects from the music project.
- The whole crew visible on the quarterdeck on every standard phone screen, including Sable on a Pixel 10 Pro XL.
- "Bow" renamed Torpedo. It should be big, slow enough to watch, faster than the fastest ship, with an explosion and a rattle on hit.
- One main enemy per chapter that can be beaten and boarded, with everything else shoot-only. The main enemy can't drop below 1 health, and boarding opens under 25% hull.
- Working enemy health bars, one per enemy.
- Story text that only moves on when the player taps.
- Dodge rolls that go up or down when the player is climbing or diving.
- Jellyfish that move more, sky whales after the first level, and at least three in Lyra's six-target level.
- A whale mouth that opens as it closes in, and a better swallow animation.
- Much weaker torpedo homing, so hitting still takes aim.
- Round wind tunnels in place of the flat wind currents, propelling ships flying through them.
- Ships that start farther apart in every level.
- Enemy torpedoes with the player's homing, slow early and fast late, that a roll can dodge.
- Tornadoes that carry a ship all the way up, and new upside-down ones that carry it all the way down.
- Every "bow" name in the code changed to torpedo.
- Wind tunnels and tornadoes kept farther apart and spread evenly, because crowded skies were confusing.
- Dr. Thorne redesigned as a dark, quiet sorcerer who heals and revives, with high health and defense and low attack.
- Rook redesigned as a huge, muscular gunner, with a robot dog that breathes fire from a cannon mouth.
- A closer quarterdeck group, because the crew stood too far apart.
- Three characters at most in party combat, with everyone else defending the ship.
- AIM removed from dogfights: every shot hits the hull, and crippling comes from the ammo.
- Overdrive removed from dogfights, since ships already have a speed attachment.
- Battle tap controls only on the right of the screen, by the enemies.
- Tapping the middle of the control pad to stop and start the engines, with looking around while stopped.
- A ship below 40% hull that drifts left or right at random moments.
- A blue dot on the radar for each sky whale. The radar itself still arrives with Morrow, as Cody decided.

Claude's choices, all open to change:

| Topic | Choice | Why |
|---|---|---|
| Chapters with no main enemy | a1_mines, s_hoard, s_currents, a2_lyra, a2_haven, a4_haven, a3_gliders, and the last phase of a4_hop | Their goal isn't defeating a ship, so nothing there can be boarded. This is narrower than "always" |
| Sink-all chapters | a1_leak, a4_coalition and s_lyra became main-enemy fights with a new deck battle | So they follow the new rule |
| Prologue's first fight | Stays sink-all | The prologue's main enemy is the scout in its second fight, which starts at 20% hull so the scripted catch fits the rule |
| Dogfight comms | Still time out | Fights are real time and thumbs are busy. Comms in story, choice, assign and deck screens wait for a tap |
| Enemy guns | Keep their old chase guns, renamed "Chase Guns" | Enemy torpedoes would about double the damage they deal |
| Internal names | `FireBow`, `Loadout.Bow`, the `bow` control and upgrade ids stay | Save compatibility |
| Torpedo homing | 0.1 turn rate and a 10 degree lock, down from 1.2 and 22 | Cody found 1.2 far too easy. In the test case it turns under 20 degrees and misses a target that moves 70 m off its line |
| Roll direction | Whichever way the stick is pushed furthest. A tie goes up or down | Matches "if ascending" without needing an exact vertical stick |
| Roll distance | Same speed, time, immunity and cooldown in all directions | "Just like" left and right |
| Roll spin | Up and down pitch the ship over, nose first | The spin then shows the direction, as it does sideways |
| Roll with no climb | Altitude-locked ships and balloonless ships trying to rise roll sideways | The button should never do nothing |
| Whale count | One in every sky phase after the prologue, three in Lyra's Currents | "Lyra's Bounty" doesn't exist. Lyra's Currents is Lyra's six-target level |
| Whales taking turns | Only one hunts at a time | Keeps a warning readable, and it's still a hunt about every 10 s |
| Pack of whales | First hunts at 8, 16 and 24 s, starting inside the arena | The test pilot had finished the level before the third whale hunted |
| Whale warnings | New lines from Vane in a1_leak and Lyra in Lyra's Currents | A whale eats the ship outright, so the player needs telling first |
| Rolling away from a whale | Roll immunity now also beats the mouth | Being immune "except to this" would feel like a bug |
| Damage drift | Player's ship only. Pulls of 0.8 to 1.8 s, 1.5 to 4.5 s apart, using 55% of its turn plus a sideways slip, with a hull creak and small shake. Clears above 40% hull | Over half the stick so it's felt, under all of it so a player can always fight it |
| Spawn distance | Ships start about 470 m apart, up from about 220 m, and a lone enemy starts off to one side. Krag keeps the old 220 m, as Cody decided | Beyond the torpedo's 360 m range and outside its 10 degree lock, so a first shot has to be earned. Reinforcement waves still arrive at their old distance |
| The Bastion's hull | Scale cut from 0.75 to 0.55 | With the weaker torpedo the bot needed 5.4 minutes for this fight, and at most 3.5 for any other |
| Wind tunnels | Built in code: 240 m long tubes, 26 m in radius, of hoops and spiral strands that flow the way the wind blows. Doubled speed plus an instant 26 m/s push, all ships, always visible | The old `SM_Env_WindCurrent` model is flat, and was hidden until a mechanic unlocked, while its speed-up still worked |
| Enemy torpedoes | Speeds of 50, 62, 76 and 92 m/s for acts 1 to 4, each with a 5 s flight, 7.5 to 9 s reloads and the player's 0.1 homing and 10 degree lock. Enemies also keep their guns | Cody asked for slow early and fast late. Their guns are most of their damage, so removing them would have made fights easier |
| No enemy torpedoes | The first level, gliders and allies | The first level is for learning, gliders are too small, and allied ones would only add clutter |
| Forward guns | Named Chase Guns: `chase_guns`, `chaseCannon`, `FireChaseGuns` | They aren't torpedoes, so renaming them "torpedo" would have been wrong. "bowsprit" in one line of dialogue is a real word, and stays |
| Old saves | Save version 3 converts the `bow` upgrade and mechanic ids to `torpedo` | So nobody loses their upgrades. These are the only remaining uses of the word in game code |
| Tornadoes | Caught on contact at any height, carried at 45 m/s to the top or bottom, with the stick locked out until then. All ships. No effect in fights where ships can't change height | "All the way" |
| Thorne's name and story | Keeps "Dr. Thorne", and is still recruited at the Citadel, now a prisoner bound to its locks rather than its Chief Engineer. Lines cut to a few words each | Cody asked for a dark character who says little. Renaming wasn't asked for |
| Thorne's kit | New `sorcerer` class: most health and defense, least attack of the crew. Dark Mending, Veil of Life, Raise the Fallen. A new Sorcerer palette and black staff | The shared atlas is full, so a new faction palette carries purple and green. Wren keeps the old `surgeon` class |
| Rook's dog | Cinder is now `SK_Cinder`, with its own 21-bone four-legged skeleton and Idle, Walk and Fire clips, replacing the static prop. Sharp steel jaws with fangs close around a brass cannon barrel, and a `muzzle` bone follows its tip. It walks whenever it's moving, walks out and fires for Cinder's Flame: 9 MP, all enemies, 75% chance to burn for 2 turns | Cody asked for a skeleton and a sharp metal jaw |
| Battle formation | Fighters stand in one spaced row on the furthest slot, up to 2.1 m apart, narrower when the screen or arena requires it. It applies to enemies too. Cinder stands directly in front of Rook | The old diagonal hid the third character |
| Final deck fight | Four enemies down to three | The bot couldn't win it with three characters |
| Quarterdeck group | Sable, Lyra, Morrow, Rook and Thorne stand on set spots below the helm, away from their station props. The camera moves in by itself | Cody found the view too empty. Their props stay where they were, as dressing |
| Three in combat | The final battle was the only fight that took the whole crew, so it now takes the chosen three. Its opening line changed to match. The crew screen says "defending the ship" instead of "in reserve". The captain still always fights | Cody decided. Defending was already the default for anyone not in the party |
| AIM removed | Chain damages engines, Mortar damages guns and engines, and Flak now damages balloons (a share equal to 60% of the shot), since nothing else would. The enemy part bars show once you have more than one ammo type | Cody decided. Balloon damage would otherwise have left the game |
| Overdrive removed | Button, rules, the `overdrive` mechanic and its teach line are gone, and Thorne's two lines mentioning it were reworded. `Overdrive.wav` is still in `Resources/Sfx`, now unused | Cody decided. Deleting the sound wasn't asked for |
| Upside-down tornado color | Dark grey ribbons and rings, as Cody decided after seeing red | White for up and dark grey for down tells them apart |
| Sky layout | Tunnels and tornadoes share evenly spaced slots around one ring, taking turns, with each tunnel running along the ring. At least 40 m of open sky between any two | Random placement could stack them |
| How many | Tunnels, up tornadoes and down tornadoes: cyclone 2, 1, 1. Storm 2, 1, 1. Maelstrom 2, 1, 1. Jetstream 3, 0, 0. Citadel 2, 0, 0. Down from as many as 7 in one sky (cyclone's 5 tunnels and 2 tornadoes) | Cody found some levels overwhelming, and four is the most a ring holds with room to spare |
| Battle tap controls | Every left-hand beat, at 14% across the screen, moved to 66%. The `left_right` pattern is now `side_step`. A test holds all beats and drags to the right 45% of the screen | Cody found watching both sides at once too hard |
| Stopping the engines | Tap under 0.25 s, moving under 30 units, within 75 units of the pad's rest center. The knob reads STOP or START. X does the same on a keyboard | A longer press or a drag still steers |
| While stopped | Left and right turn the ship, up and down tilt the view up to about 34 degrees, without changing height. Shots with no lock go where it's looking. Wind and tornadoes still move it. Rolls still work | "Look" has to do something a player can use. A stopped ship is an easy target |
| Jellyfish | Swim 8 to 15 m/s in wide curves, pulsing, and bob twice as far | Before, they only bobbed up and down in place |

## What changed

- **Portraits:** all 56 images imported, for 28 characters. 13 characters are new to the game. `SpeakerDef.portrait` lets a speaker point at a portrait directly.
- **Quarterdeck:** `HubFraming.Fit` finds the closest camera that fits everyone in the area beside the menus, using the real safe area. Morrow, Thorne and Rook were moved apart, because they were standing in front of each other.
- **Boarding:** `BoardingRule` on each ship, a hull floor of 1 for the main enemy, and a main enemy that flees but can't leave.
- **Torpedo:** a new `torpedo_tube` cannon, with a large model, a thick trail, an explosion, a shockwave and a rattle on hit.
- **Health bars:** `Bar.Set` restarted its tween every frame, which pinned the bar. Now fixed. Added a row for each escort, a mini bar under each ship marker, a 25% line and a "BOARD!" prompt.
- **Whale jaw:** the hinge sat in the middle of the jaw, so the tip barely moved. It's now at the back, the jaw opens 55 degrees, the head tips back, and a dark throat shows when it opens.
- **Swallow:** the whale keeps coming, the ship shrinks and spins into the mouth, the jaws snap shut, a gulp pulses through the body, and it spits splinters and swims off.

## Verified

- All 152 EditMode tests pass, including the bot campaign playthroughs.
- Quarterdeck captured at 16:9, 18:9, 19.5:9, 20:9 and 21:9 before the crew spacing change, and at 20:9 (Pixel 10 Pro XL shape) after it. The unit test covers the same camera on phone shapes with cutouts, but cutouts weren't rendered.
- A full campaign ran in a Mac player from a fresh save, at 2x speed, on the final code. All 26 missions finished in about 29 minutes with 0 errors or exceptions, including the new deck fights in a1_leak, a4_coalition and s_lyra. The bot was swallowed once.
- Thorne, his staff, Rook and Cinder passed the Blender project's budget gate and `validate_production.py`. Thorne's and Rook's redesigns were checked on all 14 animation clips (Rook's after the last chest fix on arms only). The quarterdeck and Cinder's Flame were checked in screenshots at Pixel 10 Pro XL shape.
- In a Mac player at Pixel 10 Pro XL shape:
  - Story text stayed on the same shot for 30 s without a tap.
  - Boss hull and its bar matched at every reading.
  - A real roll through the input code climbed 54 m and dived 54 m, and a left roll stayed level.
  - A jellyfish moved 40 m in 5 s.
  - Two different whales hunted in Lyra's Currents, one at a time, before the test pilot was swallowed at 16 s. A third whale's turn was covered only by a unit test.
  - The staged swallow ran with 0 errors, and the screenshots show the open mouth, the ship inside and the closed jaws.
  - Lyra's Currents had 4 wind tunnels, drawn as round tubes. Flying through one took the ship from about 53 m/s to 91 m/s, then it slowed down again after leaving. The radar showed three blue whale dots with Morrow on the crew.
  - A run with no radar mechanic was not re-captured after Cody's decision to keep the radar with Morrow. That gate is the original code.

## Not verified

- **Feel:** torpedo homing, whale pressure and the swallow's timing were checked from numbers and still frames, not in hand. Audio has still not been heard by Claude.
- **Real phone:** nothing ran on a phone by Claude. The Android build wasn't rebuilt, and three whales plus up to five tunnels haven't been measured on one.
- **Damage drift:** covered by a unit test only. It wasn't watched running.
- **Spawn distance:** covered by a unit test and bot campaigns. It wasn't reviewed in screenshots at the start of a fight with enemies.
- **Touch input:** rolls were driven by a simulated keyboard and the BOOST tap branch, not by a real thumb on the stick. The engine tap was triggered at its flag, so the pad's tap detection itself has not been run.
- **Difficulty:** bots only. A whale ends the attempt outright, so a whale in every chapter may be much harder for people. The Krag tether is riskier too, since nothing lifts the hull above 25% there.

## Known issues

- **Blender gate method:** the `mobile-asset-optimizer` agent isn't registered in Claude Code here and the Blender MCP tools aren't available, so a general-purpose agent followed that agent's written steps using the same enforcer script in a headless Blender.
- **Cinder at the screen edge:** in interiors, its feet can touch the Momentum panel when Rook is in the middle of the row. Its walk is only a trot, with no run.
- **Thorne's lower robe:** the leg panels are rigid, so a raised knee in Surrender, Grapple or Work swings a panel out like a stiff wing.
- **`short_jacket` and `long_coat`:** their front opening sits half a segment off centre on other characters. Thorne and Rook no longer use them, and the others weren't changed.
- **Old props:** `SM_Prop_BigWrench` is now unused.

- Afterburner can briefly outrun a torpedo. The 72 m/s speed beats every ship's full throttle of up to 48, but not a 96 m/s burst.
- A roll up at the ceiling or down at the floor gets cut short, but still gives immunity.
- Pink quads on the Undertow disc show in the Maelstrom sky when looking down. They vanished when that model was hidden, so they come from the disc's art and not from this work.
- `SM_Env_WindCurrent` is no longer used by the game.
- Wind tunnels are opaque hoops and strands with no see-through wall, to avoid transparency cost on phones. They can overlap each other, and rocks can sit inside them.
- Some code is now mostly unused: `TargetLost`, `boardOnly`, `TargetEscaped` in `FinishSky`, and the "Scrap only" results.
- Comms in deck fights can sit over the top of the screen until tapped.
- New files need the Editor to take focus to import them: portrait metas and `HubFraming.cs`.
- `UniversalRenderPipelineGlobalSettings.asset` is still modified from before, and was left alone.
