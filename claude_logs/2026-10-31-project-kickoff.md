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

## Proposed first playable version

Claude's proposal, awaiting Cody's approval. It proves that one full pirate encounter works and is fun on the Pixel 10 Pro XL.

**The loop:** real-time dogfight against one AI pirate, choose whether to board, party battle against their crew, earn experience and currency, buy a ship upgrade, then play again with progress saved.

**Included:**

- One open-sky arena, with touch steering, two cannon types and one AI enemy ship.
- One party of four, with four classes, levels, individual stat growth and basic skills.
- One upgrade shop screen and a local save.
- Low and high definition modes.
- An Android build that runs on the Pixel.

**Left out for now:** world exploration, story, online multiplayer, in-app purchases, skins and iOS.

## Open questions

1. What happens if the player declines to board a defeated ship? Claude's suggestion for the first playable version is a smaller reward.
2. How does the player switch between low and high definition: automatically, manually, or both?
3. Are airships and upgrades bought only with in-game currency? This is implied by the monetization
   decision, but hasn't been stated.
4. Are skins sold directly or through a premium currency?
5. Which character classes, stats and growth formulas will the game use?
6. What is the game's title? "Airships" is currently only the repository name.

## Project state

Checked at the start of this conversation:

- **Editor:** Unity 6000.6.7f1, with URP and separate mobile and PC render settings. The project is still the template, with only `SampleScene`.
- **Unity plugin:** installed and connected. A read-only script ran successfully in the Editor.
- **Console:** no errors. Two warnings say Unity couldn't read the Windows signatures for `relay_win.exe` and `claude.exe`; neither blocked the connection.
- **Build modules:** the Android and iOS modules were missing at first, but a later check found both installed. The active build target is still Windows.
- **Git:** no commits yet. The template files are staged on `main`.

## Next steps

1. Cody approves or changes the proposed first playable version, which replaces the earlier suggestion of one explorable area, one battle and a save system.
2. Claude makes the first git commit, if Cody approves, and switches the active build target to Android.
3. Claude builds the first playable version.
