# Whale Targeting, Ship Speed and Harder Timing Patterns

- **Date:** 2026-10-31
- **Participants:** Cody Nicholson, Claude Code (Opus 5.5)
- **Status at end of this entry:** implemented and running in the Editor. Not yet played by a person or on a phone, and not committed.

This entry changes parts of `2026-10-31-dogfight-altitude-hazards.md` and `2026-10-31-boarding-timing-combat.md`.

## Decisions

Cody decided:

- The sky whale can't eat an AI enemy or end the game by doing so. It avoids enemies and targets only the player.
- Ships fly a little faster by default, and get faster with upgrades.
- Timed prompts in boarding battles must be harder to succeed at.
- Prompts use multiple speeds and tap patterns to earn the damage bonuses and reductions.

Claude's choices, all open to change:

| Topic | Choice | Why |
|---|---|---|
| Whale avoidance | It steers around enemy ships within 75 m, even mid-lunge, and passes through harmlessly if it can't turn in time | Enemies are never eaten either way |
| Default speed | Player 34 to 40 m/s, Raider 30 to 35 m/s | About 17% faster, and the player keeps an edge |
| Engine upgrade | +8% to +12% per level, so five levels add 60% | Made the upgrade clearly noticeable |
| Whale lunge speed | 80 to 92 m/s | Keeps it dangerous against faster ships |
| Grading | Every target must be hit in order and on the spot. One miss fails the pattern, and the weakest hit sets the grade | A Perfect now means a clean run |
| Timing windows | Perfect 0.08 s to 0.04 s, Good 0.18 s to 0.10 s | Harder even on a single target |
| Patterns | 11 patterns of 1 to 5 targets, with rings closing over 0.2 to 1.1 seconds, on one or both sides of the screen | Mixes speeds, rhythms and thumb jumps |
| Difficulty by tier | 4 patterns at tier 0, rising to all 11 by tier 5. Repeats back to back are avoided | Gives new players time to learn |
| Attack and defense | Some patterns are only for attacks or only for blocks | Each side gets its own feel |
| Multipliers | Unchanged: 1.2× and 1.5× on attack, 0.65× and 0.3× on defense | Harder prompts make these rewards rarer already |
| Missing | **Replaced by Cody's later decision below:** a miss now costs damage | n/a |
| Stray taps | Taps over 0.4 s before the first target are ignored. Any other badly timed or wrong-spot tap fails the pattern | Prevents menu double-taps from failing it, while still punishing mashing |

Patterns and windows are in `game_content.json`, so they can be tuned without code changes.

## Results

Each table comes from scripted simulations of the real rules. The thumbs are a rough model of timing error (35 ms, 60 ms and 90 ms standard deviation, plus extra for jumping between spots), not measured players.

| Simulated player | Tier 0: Perfect / Good / Miss | Tier 3: Perfect / Good / Miss |
|---|---|---|
| Sharp | 60% / 37% / 3% | 39% / 47% / 15% |
| Typical | 35% / 46% / 19% | 19% / 42% / 40% |
| Casual | 22% / 37% / 41% | 11% / 27% / 62% |

| Tier 10 enemies with a level 6 party, attacks and heals | Win rate |
|---|---|
| Sharp | 98% |
| Typical | 76% |
| Casual | 47% |

| Dogfight bot, tier 3 | Before this change | Now |
|---|---|---|
| AI pilot, no upgrades | 57% wins, 43 crash damage | 48% wins, 57 crash damage |
| AI pilot, 3 engine levels | Not measured | 61% wins, 93 crash damage |
| AI pilot, 5 engine levels | Not measured | 54% wins, 94 crash damage |
| Careless bot, no upgrades | 74% wins | 71% wins |

In 1,800 simulated fights with a whale, it lunged at the enemy 0 times and ate it 0 times. Its closest approach to the enemy in a fight averaged 64–115 m, depending on the case.

## Verified

- 50 of 50 EditMode tests pass, 9 of them new.
- Testing caught a real bug: on an exact head-on course, the whale's avoidance did nothing. It now veers to one side.
- Real interface buttons were driven by script at tier 5, with taps sent to actual screen positions. Nine different patterns appeared, and perfect, good, wrong-spot and no-tap runs all graded correctly.

## Not verified

- **Nobody has played this.** The hit rates above come from a thumb model that doesn't include reading the pattern, finger reach or fatigue. Real difficulty may be noticeably higher.
- **Phone touch delay** could make the tighter windows too hard. It can't be judged in the Editor.
- Nothing has run on a phone.

## Later change: missing now has a cost

Cody decided that missing a prompt should mean dealing less damage and taking more, by a noticeable percentage.

Claude's choices, open to change:

| Topic | Choice | Why |
|---|---|---|
| Attack miss | 0.75×, so 25% less damage | Easy to notice |
| Defense miss | 1.25×, so 25% more damage | Matches the attack penalty |
| Heals | A missed heal restores the normal amount | The request covered damage, and a failed heal on top of a hit could snowball quickly |
| Feedback | Missed attacks show small, muted numbers. Hits taken after a missed block show large red ones | The cost should be visible at a glance |

Simulated win rates with the penalty, attacks and heals only:

| Fight | Sharp | Typical | Casual | Never tapping |
|---|---|---|---|---|
| Tier 3, level 4 party (fair) | 100% | 100% | 99.8% | 92.5% |
| Tier 6, level 4 party (under-leveled) | 100% | 84.5% | 40% | 0% |
| Tier 10, level 6 party (under-leveled) | 93.8% | 41.5% | 2.8% | Not measured |

Before the penalty, the tier 6 row was 100%, 97.5%, 91% and 47%. Under-leveled fights are what normal progression produces, because enemy levels outpace the party's, so the difficulty wall arrives sooner for players who miss often.

## Known issues

- **Longer actions:** patterns last up to 1.6 seconds. The tier 5 test battle took 59 seconds for 28 actions, about 2.1 seconds each. That works against the earlier goal of shorter battles.
- **Faster ships crash more:** tier 3 bot wins fell, and top engine upgrades can reduce the AI pilot's wins because it crashes more. A person may handle speed differently.
- **Hard patterns:** `flurry` and `gauntlet` are very difficult in the model, with Typical players missing about 87% and 73%. They may need easing after real play.
- **Hidden targets:** targets can sit over characters, which could hide part of the action.
