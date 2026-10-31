# Project Kickoff: Flying Pirate RPG

- **Date:** 2026-10-31
- **Participants:** Cody Nicholson, Claude Code (Opus 5.5)
- **Status at end of this entry:** design direction recorded, no game code or scenes created

## Summary

Cody wants to build a mobile RPG in Unity, using the repository named "Airships". Claude confirmed that
the Unity plugin can reach the open Editor, then asked about concept, combat, visual style, platform and
monetization. Cody's answers are recorded below.

## Decisions

Decisions below were made by Cody.

### Concept

- The player is a pirate in a world where battleships can fly.
- The game's defining feature is multiple combat systems within one RPG.

### Party combat

- Standard turn-based RPG combat, in the style of Final Fantasy or Dragon Quest.
- Up to four humans in the party at one time, including the main character.
- Each human has a class, base stat totals and a level.
- Each stat has its own growth rate, influenced by both class and the character's individual base stat values.

### Airship layer and combat

- The player can buy, upgrade and personalize an airship.
- Airships fight other airships in a separate "dogfight" combat system.
- Each airship has different types of cannons, plus its own speed, health and armor values.
- Dogfights are real-time.

### Linking the two combat systems

- After defeating another pirate in airship combat, the player can choose to board the defeated ship.
- Boarding leads to a fight against that pirate using the party-of-four turn-based system.

### Visual style and performance

- The target look and style is comparable to *Skies of Arcadia Legends* (GameCube).
- The game will have a low definition mode and a high definition mode, to run well on as many devices as possible.
- This replaces Claude's earlier recommendation of 2D pixel art.

### Platforms

- The long-term targets are Android and iOS.
- Development starts on Android only, optimized for Cody's test device, a Google Pixel 10 Pro XL.

### Monetization

- The game is freemium.
- Players can buy styles and skins for characters and battleships.
- Free players must get the same experience of the core mechanics as paying players.

### Multiplayer

- The game should allow online multiplayer with both friends and random players.
- An online "Ranked" mode may be added in the future. This is a possible expansion, not a commitment.

Claude recommended the following and Cody accepted:

- Multiplayer starts with dogfights, and boarding battles are added afterward. Shared world exploration is left out.
- The first multiplayer format is 1v1 versus. Co-op or team battles can come later.
- Multiplayer ships after the single-player game, but is planned for from the start.
- Dogfight rules are written separately from visuals and input, so they can later run on a server that decides outcomes.
- Open matchmaking pairs players by ship power. Stricter rules wait until Ranked is designed.

## First playable version

Claude proposed this scope and Cody approved it. It proves that one full pirate encounter works and is fun on the Pixel 10 Pro XL.

**The loop:** real-time dogfight against one AI pirate, choose whether to board, party battle against their crew, earn experience and currency, buy a ship upgrade, then play again with progress saved.

**Included:**

- One open-sky arena, with touch steering, two cannon types and one AI enemy ship.
- One party of four, with four classes, levels, individual stat growth and basic skills.
- One upgrade shop screen and a local save.
- Low and high definition modes.
- An Android build that runs on the Pixel.

**Left out for now:** world exploration, story, online multiplayer, in-app purchases, skins and iOS.

### Defaults Claude set for this version

Cody was told about the first three and didn't object. The rest are Claude's choices, and all are open to change.

- Declining to board ends the encounter, and the player keeps only the dogfight reward.
- The player switches graphics modes manually in Settings. The first launch picks high definition on devices with at least 4 GB of memory.
- The four classes and all balance numbers live in `Assets/Game/Resources/game_content.json`, for easy editing.
- The dogfight is played on a flat plane with steering and throttle, so height isn't a control. This suits thumbs, and it's simpler for a server to run later.
- The game is landscape only.
- The party recovers fully between battles.
- Losing a dogfight costs nothing and pays a small salvage reward. Losing a boarding battle keeps the dogfight reward.
- The visual style uses faceted, two-tone shaded models built in code, without outlines. Placeholder music and sound are generated in code.
- "Airships" is the working title, and `com.codynicholson.airships` is the working Android package name. The package name can't change after a store release.

### Character classes (draft for Cody's review)

| Class | Role | Starting HP / MP | Attack / Defense / Spirit / Speed | Skills (unlock level) |
|---|---|---|---|---|
| Buccaneer | Front-line fighter | 120 / 20 | 18 / 14 / 6 / 11 | Cleave (1), Crushing Blow (3) |
| Gunner | Fast ranged damage | 90 / 24 | 20 / 9 / 8 / 15 | Aimed Shot (1), Scattershot (3) |
| Stormcaller | Sky magic | 75 / 50 | 7 / 8 / 20 / 12 | Lightning Bolt (1), Squall (3) |
| Surgeon | Healer | 85 / 45 | 8 / 10 / 16 / 10 | Patch Up (1), Rally (3) |

The starting crew has individual base stats that differ from these class values: Captain Vale (Buccaneer), Mirren (Gunner), Tobin (Stormcaller) and Odessa (Surgeon).

**Stat growth:** `stat at level = individual base + growth rate x (level - 1)`, rounded down. `growth rate = class growth x (1 + (individual base / class base - 1) x 0.5)`. A character whose base stat beats their class's value therefore grows faster in that stat, which follows Cody's requirement that growth depends on both class and individual base stats.

### Camera and graphics modes

| Setting | Low definition | High definition |
|---|---|---|
| Frame rate target | 30 | 60 |
| Render scale | 60% | 85% |
| Anti-aliasing | Off | 2x MSAA |
| Shadows | Off | On |
| Post-processing and HDR | Off | On |
| Cloud clusters | 38 | 74 |
| Particle density | 45% | 100% |
| Shot trails | Off | On |

## Open questions

1. Are airships and upgrades bought only with in-game currency? This is implied by the monetization
   decision, but hasn't been stated.
2. Are skins sold directly or through a premium currency?
3. Do the draft classes, skills and stat growth above suit the game Cody wants?
4. What is the game's final title?

## Project state

- **Editor:** Unity 6000.6.7f1 with URP. The active build target is Android, and the game scene is `Assets/Game/Scenes/Main.unity`.
- **Packages added:** Cinemachine 6.6.0 and Test Framework 1.8.0. TextMesh Pro's essential resources were imported.
- **Code layout:** `Assets/Game/Core` holds the game rules with no Unity dependency, so a server can reuse them. `Assets/Game/Scripts` holds visuals, input and screens. `Assets/Game/Editor` holds project setup and the build command, and `Assets/Game/Tests` holds the rules tests.
- **Asset generation:** Unity's AI asset generation reports `NoSubscription`, so all art and audio are procedural for now.
- **Audio:** volumes run through a code-based gain layer. Unity has no scripting API for creating an Audio Mixer, so adding one is a later step in the Editor.
- **Verified in the Editor:** 21 tests pass, and the automated player completed full loops with no console errors. Title, shop, broadside, pause, settings and battle menus were tested with simulated clicks. The thumb stick was tested with events sent directly to it, because the click simulator can't hold a press.
- **Not yet tested:** the Retreat button, the Leave Them button by click, losing a boarding battle in the running game, and how the audio actually sounds. Music was confirmed playing, but nobody has listened to it.
- **Known gaps:** ships pass through each other because the dogfight has no ship collisions yet. Classes, balance and art are first drafts.
- **Android build:** `Builds/Android/Airships.apk` is about 42 MB and builds with no errors. It hasn't run on a phone yet, so frame rate and touch feel are unmeasured. Nearly all build warnings come from shaders in Unity's `com.unity.ai.inference` package.
- **Git:** the first commit holds the template project and this log. Cody approved only that commit, so the game code is uncommitted.

## Next steps

1. Cody connects the Pixel 10 Pro XL with USB debugging on, then installs and plays the build.
2. Tune feel and performance from real play on the phone.
3. Test low definition mode on a budget Android phone before release.
