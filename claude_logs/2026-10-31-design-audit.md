# Design Audit: First Playable Version

- **Date:** 2026-10-31
- **Participants:** Cody Nicholson, Claude Code (Opus 5.5)
- **Status at end of this entry:** audit complete, no game code or balance data changed

## Summary

Claude audited the first playable version against the `design-game` skill's four-book framework, design-smell table and mobile blueprint. The loop works from start to finish and the engineering is sound, but three design problems outweigh the rest:

1. The dogfight doesn't play the way its numbers intend.
2. The Board or Leave choice isn't a real decision, and the two combat systems don't affect each other.
3. Every playthrough ends at a difficulty wall, with no ending.

## Decisions

Decisions below were made by Cody.

- **Experience goals:** a player should feel all four of these: daring pirate captain, skilled ace pilot, clever tactician, and attachment to a growing crew and ship.
- **Dogfight identity:** a broadside duel. Heavy ships jockey for firing angles at range, with big telegraphed volleys.
- **Boarding stakes:** by default, a player can lose their ship by losing a boarding battle. A setting lets players switch to a medium wager, where they forfeit plunder instead.
- **Long-term structure:** a hybrid. A hub and sky map of regions, with bounties to choose from, named rival captains as bosses, and light story.
- **Skill reference:** `design-game` uses the "Four-Books Game Design Reference Handbook" version of `four-books-framework.md`, not the "Reference Matrices" version.
- **Hook:** "Outfly a pirate in a real-time sky duel, then stake your ship to storm their deck."
- **Theme:** freedom has a price.
- **What ship loss takes:** the captor takes the hull and its fitted upgrades until the player recaptures it by hunting them down. Crew, levels, gold and cosmetics stay, and a free basic ship keeps the player going.
- **Next build:** a one-region vertical slice, with about 5 to 8 encounters, wordless training, one named rival boss and ship loss.
- **Airships and upgrades:** bought only with in-game currency.
- **Multiplayer stakes:** ships can't be lost in multiplayer.
- **Skins:** sold for in-game currency. Exclusive special skins are also sold for real money.
- **Title:** *Ransom Skies*, chosen from Claude's recommendation. This replaces the working title "Airships". Store, trademark and domain checks haven't been done.

## Full skill run

After the skill was repaired, Claude ran all five phases. The result is `docs/game-design-plan.md`, which holds the One-Sheet, Master Beat Chart, mobile production plan and playtest plan. Its metrics and economy numbers are Claude's untested proposals.

That run also read the remaining rendering, camera and audio code and watched an autoplay run in the Editor, which logged no errors or warnings. New findings:

- The chase camera leaves an enemy in firing position off-screen or beside the fire buttons.
- Player and enemy ships share one silhouette.
- `SaveStore.Load` discards saves from other versions, which would wipe progress when the format changes.
- The build isn't set up for Google Play: it makes an APK and has no release signing key.

## How this was checked

- **Read:** the design log, all rules code, the balance data, and the dogfight, boarding, shell, save and input code.
- **Measured:** the real `Assets/Game/Core` rules and `game_content.json` ran outside Unity with scripted pilots, at 200 to 300 runs per case. The test harness is not in the project.
- **Not assessed:** nobody has played this on a phone, and Claude did not run the game in the Editor for this first pass. The later full skill run below covers the Editor view and silhouettes. Touch feel and how the audio sounds remain unjudged, and the Unity test suite was not rerun.
- **How to read the numbers:** they come from bots, not people. Trust the comparisons and patterns more than the exact percentages. A person could do better or worse, especially with engine and rudder upgrades, which a person may use better than these bots.
- **Skill problem:** when this audit ran, the installed `SKILL.md` stopped at line 41, partway through Phase 1, so the instructions for Phases 2 to 5 were missing. Claude followed the phase references inside the reference files instead. Cody later supplied the missing text and the skill was repaired. The Phase 3 to 5 outputs are in `docs/game-design-plan.md`, described under "Full skill run" below.

## Findings

### 1. The dogfight doesn't match its numbers (Three Cs, Toy First, Discernibility)

Guns reach 110 to 130 m, but after the first pass the traced fights mostly stayed within about 30 m, often at 1 to 7 m. Ship radius is 7 m and ships have no collisions, so they often overlap. The enemy AI turns side-on inside 88 m and ends up circling close (`DogfightSim.cs:415`).

| Pilot, tier 0, unupgraded ship | Player win rate | Bow damage per second | Broadside damage per second |
|---|---|---|---|
| Side-on AI pilot, both guns | 100% | 0.5 | 10.6 |
| Same pilot, bow only | 0% | 0.2 | none |
| Nose on target, full throttle, bow only | 0% | 1.5 | none |
| Nose on target, both triggers held | 100% | 2.5 | 10.1 |
| Side-on orbit, broadside only | 100% | none | 15.7 |

- **The bow barely matters.** Even a bot steering straight at the target had the bow able to bear only 1% to 7% of the fight, because the target's angle changes too quickly at this range. The enemy's bow is just as minor, at 16 of 461 damage dealt at tier 3.
- **Firing takes little judgment.** Nothing limits holding both triggers, the game picks the broadside's side, and holding both beat careful firing. At tier 3 the hold-both bot won 45%, against 29% for the side-on AI pilot.
- **There's a stalemate.** Steering at the enemy at half throttle settles into a repeating 6-second loop at 17 m where neither ship can fire. All 300 of 300 such fights were unresolved after 5 minutes, using the bow alone.
- **The player can't read the fight.** The HUD doesn't show broadside arcs or effective range, enemy volleys have no warning, and there's no dodge. Throttle has a hidden effect: turn rate ignores speed, so slowing down tightens the turn.
- **Height isn't used.** The arena is a flat, empty plane, and clouds appear to be scenery only. That removes much of the fantasy of flying and gives positioning little to work with.

**Recommendations**

- Rebuild for the broadside duel chosen above, and prove it in a 60-second toy test on the Pixel before tuning anything else.
- Have the AI hold a 50 to 90 m standoff, add ship collisions, and slow ships down enough for positioning to matter.
- Draw arcs on the ship that light up when a volley will land.
- Telegraph enemy volleys with glowing gun ports and add one evasive move on a cooldown, for a read-and-respond exchange.
- Give the bow a different job from the broadside. For example, chain shot could slow the target's turning, so it sets up the next volley.
- Make clouds or storms affect play, such as hiding ships or changing how they handle.

### 2. Board or Leave isn't a real decision, and the combat systems don't connect (Triangularity, Integration)

This is the game's defining moment, and it currently has one right answer.

- The dogfight reward is banked before the prompt (`GameDirector.cs:108`) and the party heals fully, so boarding has no risk and pays more.
- Leaving is worse than it looks, because the encounter count rises either way. Bots that always left gave up at a median tier of 6, against 9 for bots that always boarded.
- Retreating pays nothing, while losing pays 15 gold (`GameDirector.cs:131`), so giving up is worse than dying.
- How the dogfight went has no effect on the boarding battle, and the boarding result has no effect on later dogfights.
- The change between modes is a fade to a new screen.

**Recommendations**

- Make damage type shape the deck. Grapeshot could mean fewer enemy crew, and rigging hits could slow the enemy's turns.
- Let the player's remaining hull grant a ship's guns support action during boarding.
- Have boarding wins pay out something dogfights need, such as salvaged cannon types.
- Replace the fade with a grapple-and-swing moment. Together with the stakes, that sequence is the game's hook and the 5-second clip that sells it.
- Let damage carry over to the next fight and cost gold to repair, which also adds a gold drain.

### 3. Difficulty rises until the game stops (Flow Channel, Interest Curve, Anthem smell)

Enemy hull and damage both grow 18% per win with no cap, and enemy level is wins + 1 (`Progression.cs:144`, `PartyBattle.cs:65`). Nothing limits the rise, and players can't choose an easier fight.

| Bot campaign, 200 runs | Stopped at tier (median) | Reached 30 wins | Bought the whole shop |
|---|---|---|---|
| Cheapest upgrades first, uses skills | 9 | 0 of 200 | Never |
| Hull, armor and broadside first, uses skills | 13 | 0 of 200 | Never |
| Cheapest upgrades first, no attack skills | 8 | 0 of 200 | Never |
| Cheapest upgrades first, always leaves | 6 | 0 of 200 | Never |

A bot "stopped" after 12 straight dogfight losses at a tier. Fights take about 30 to 60 seconds, so this is very little play time. Once stuck, a player earns only 15 gold per loss and can't replay easier fights.

The party also falls behind by design. If every encounter is boarded and won, the party is level 3 against enemy level 5 at tier 4, and level 6 against 11 at tier 10. `PartyUsuallyWinsAtMatchingLevel` sets both to the same level (`CoreRulesTests.cs:186`), a situation real play stops producing from tier 3, so the test can't catch this.

**Recommendations**

- Replace the global win counter with the regions already chosen. Each region has a fixed, visible threat range, the player chooses a bounty, and earlier regions stay replayable.
- Create difficulty through new enemy types and ship classes instead of multiplying stats.
- Let regions end in a named rival, using a three-phase boss fight: baseline, escalation with a changed arena, then a final test of mastery.
- Give each resource a documented source and drain. Candidates are repairs, new ships, crew recruitment and ransoms.

### 4. Half the upgrades are close to worthless (Discernibility, Integration, Anthem smell)

Three levels of one upgrade, tier 3, side-on AI pilot, 37.0% baseline win rate:

| Upgrade | Cost | Win rate |
|---|---|---|
| Iron Plating (armor) | 516 | 100.0% |
| Reinforced Hull | 413 | 99.7% |
| Heavy Battery (broadside) | 568 | 99.3% |
| Bored Chasers (bow) | 464 | 46.0% |
| Sky Rudder | 310 | 44.3% |
| Tuned Engines | 361 | 40.3% |

A new player spends gold without being able to tell these apart. With 100 starting gold, Heavy Battery at 110 is also out of reach.

**Recommendations**

- Change most upgrades from percentages into new abilities: chain shot, grapeshot, a faster reload on one side.
- Add the ship classes and cannon types from the kickoff decisions, so ship choice becomes a build decision. Every ship currently uses the same two cannons.

### 5. Boarding is mostly solved and its classes feel alike (Tetrad, Suicide Squad smell)

| Approach at matching level | Win rate, tier 5 | Win rate, tier 10 | Party actions, tier 10 |
|---|---|---|---|
| Skills and heals | 100% | 100% | 10 |
| Guard every other round, plus skills | 100% | 100% | 22 |
| Basic attacks and heals | 99.7% | 89.3% | 34 |
| Basic attacks only | 60% | 25% | 24 |

- **Skills are an easy answer.** With skills, fights end in about 3 rounds, so MP never becomes a real limit.
- **Guard adds nothing.** It doubled the length of these fights without improving the win rate, which was already 100%.
- **The four classes share two templates.** Every skill is damage times power, or a heal. There's no revive, status effect or item.
- **Difficulty is about to matter more.** In real campaigns without attack skills, boarding win rate fell from 96% at tier 3 to 41% at tier 4 and 4% at tier 6. That swing would feel unfair with a ship at stake.
- **Enemies have little identity.** Crew make-up follows one fixed pattern, captains are unnamed, and nothing shows what enemies plan to do.
- **Some information is missing.**
  - Skill descriptions exist in the data, but the menu doesn't show them (`BoardingScreen.cs:476`).
  - At tier 0, the target list shows two identical "Brigand" entries, and they aren't highlighted on the deck (`BoardingScreen.cs:501`).
  - Level-up lines don't mention new skills or stats.

**Recommendations**

- Show enemy intentions each turn, as *Slay the Spire* does. This gives players something to plan around and makes losses feel fair.
- Give each class one signature ability that matches its identity. Examples: the Buccaneer protects allies and grapples, the Gunner marks targets, the Stormcaller changes the weather on deck for both sides, and the Surgeon revives.
- Make Guard useful, for example by restoring MP or blunting a telegraphed attack.
- Number or letter duplicate enemies, highlight the current target on deck, and show what each skill does.

### 6. There's no onboarding or story yet (Training Cage, Tetrad)

The first dogfight opens with a banner and no guidance, and the game has no tutorial or hints. Tier 0 is gentle enough to work as an informal practice fight, but nothing teaches arcs, throttle or skills. The first playable version deliberately left out story.

**Recommendations**

- Make the opening encounter a wordless training fight against a tethered or drifting ship, advancing only when the player lands a broadside.
- Introduce boarding and skills the same way, with no ship at risk.
- Use a single theme to hold art, rules and story together. A suggested one: *freedom of the open sky, and the risk of taking what you want.* Weather and wind fit it in both combat modes.

### 7. Mobile gaps (Platform blueprint)

- The game has no haptics. Volley hits and hull damage are obvious candidates.
- Graphics modes are chosen once, then changed manually (`GameDirector.cs:65`). The blueprint asks for automatic thermal scaling that drops shadows before frame rate.
- An interrupted boarding battle isn't saved. This matters much more now that ships are at stake.

## What already works

Keep these:

- Rules are separate from visuals and input, deterministic, and ready for a server.
- Balance lives in editable data.
- The fixed-step dogfight has interpolated rendering, hit-stop and floating numbers.
- Safe areas are respected, and the game saves when backgrounded.
- An encounter is short enough for phone sessions.
- The monetization plan is cosmetic only, which protects the value of in-game rewards (Lens #2).

## Required design fields

| Field | Status | Note |
|---|---|---|
| Title and hook | Partial | Working title only. Suggested hook: *Outfly a pirate in a real-time sky duel, then stake your ship to storm their deck.* |
| Target platform | Set | Mobile, landscape, two thumbs, Android first |
| Experience goals | Set today | See the suggested mapping below |
| Core toy and Three Cs | Unproven | Needs the dogfight rebuild and a phone test |
| Loop and economy | Partial | The loop works, but gold has one drain and nothing stabilizes it |
| Tetrad and theme | Missing | Theme suggested in finding 6 |
| Narrative and onboarding | Partial | Structure set today, onboarding missing |
| Monetization and scope | Set | Freemium, cosmetic only |

## Claude's suggestions on Cody's decisions

These are suggestions, not decisions.

### Making all four experience goals work

Trying to maximize all four everywhere would blur the game, so give each one a home:

| Goal | Where it lives |
|---|---|
| Daring captain | Choosing bounties and the Board or Leave wager |
| Ace pilot | The dogfight |
| Tactician | The boarding battle |
| Growing crew and ship | The port, between encounters |

When goals conflict, let the daring captain win, because that goal is what connects the other three.

### Making ship loss work as the default

This raises the stakes considerably, and it's what makes the Board choice real. To keep it from driving players away:

- **Never strand a player.** Always provide a free basic ship, and a way to earn gold with it.
- **Turn loss into a story.** The captain who took your ship becomes a named rival with a bounty, and you can win the ship back. This suits the hybrid structure and could become the game's most memorable feature.
- **Show the stakes first.** The prompt should name the ship at risk, the enemy's threat level and the reward.
- **Protect against interruptions.** Save the battle when the player taps Board, and resume it when they reopen the game. This stops force-quitting from dodging a loss, and stops a crash or phone call from costing a ship.
- **Offer a costly way out.** Add a mid-battle escape that forfeits the plunder, so a player can back out of a fight going badly.
- **Protect new players.** Don't put ships at risk until the training fights are done.
- **Limit the setting.** Allow changing between high and medium stakes only in port, so nobody can switch after seeing a fight go badly.
- **Protect purchases.** Paid skins belong to the account and must survive ship loss. Never sell insurance, ship recovery or protection, because that would break the rule that free players get the same core experience.

Ship loss should wait until findings 3 and 5 are addressed. Today's difficulty swings would make it feel random.

## Recommended order

1. Install and play the current build on the Pixel. It's cheap, and touch feel is still unknown.
2. Run the broadside duel toy test: standoff AI, collisions, arcs, telegraphs and an evasive move.
3. Have 3 to 5 people play it while thinking aloud, with no explanation, and adjust.
4. Connect the combat systems: damage carryover, a grapple transition and an honest Board prompt.
5. Rework boarding: enemy intentions, signature abilities and a useful Guard.
6. Replace the win counter with the first region and first named rival, and add the wordless training fight.
7. Add ship classes, cannon types and resource drains.
8. Add ship loss, the rival recovery bounty and the stakes setting.

## Open questions

1. Does losing a dogfight ever risk the ship? Cody's decision covers boarding, so dogfight losses stay as they are for now.
