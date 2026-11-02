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

- All 143 EditMode tests pass, including the bot campaign playthroughs.
- Quarterdeck captured at 16:9, 18:9, 19.5:9, 20:9 and 21:9 before the crew spacing change, and at 20:9 (Pixel 10 Pro XL shape) after it. The unit test covers the same camera on phone shapes with cutouts, but cutouts weren't rendered.
- A full campaign ran in a Mac player from a fresh save, at 2x speed, on the final code. All 26 missions finished in about 27.5 minutes with 0 errors or exceptions, including the new deck fights in a1_leak, a4_coalition and s_lyra. The bot was swallowed once.
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
- **Touch input:** rolls were driven by a simulated keyboard and the BOOST tap branch, not by a real thumb on the stick.
- **Difficulty:** bots only. A whale ends the attempt outright, so a whale in every chapter may be much harder for people. The Krag tether is riskier too, since nothing lifts the hull above 25% there.

## Known issues

- Afterburner can briefly outrun a torpedo. The 72 m/s speed beats every ship's full throttle of up to 48, but not a 96 m/s burst.
- A roll up at the ceiling or down at the floor gets cut short, but still gives immunity.
- `SM_Env_WindCurrent` is no longer used by the game.
- Wind tunnels are opaque hoops and strands with no see-through wall, to avoid transparency cost on phones. They can overlap each other, and rocks can sit inside them.
- Some code is now mostly unused: `TargetLost`, `boardOnly`, `TargetEscaped` in `FinishSky`, and the "Scrap only" results.
- Comms in deck fights can sit over the top of the screen until tapped.
- New files need the Editor to take focus to import them: portrait metas and `HubFraming.cs`.
- `UniversalRenderPipelineGlobalSettings.asset` is still modified from before, and was left alone.
