# Swipe and Drag Triggers

- **Date:** 2026-10-31
- **Participants:** Cody Nicholson, Claude Code (Opus 5.5)
- **Status at end of this entry:** implemented and running in the Editor. Not yet played by a person or on a phone, and not committed.

This entry changes the patterns in `2026-10-31-whale-speed-timing-patterns.md`.

## Decisions

Cody decided:

- Boarding battle prompts need more variety than tapping at the right time.
- Swipe and drag triggers are mixed in with tap triggers, to test reflexes more than repeated tapping does.

Claude's choices, all open to change:

| Topic | Choice | Why |
|---|---|---|
| Tap | Press on the target as its ring closes | Unchanged |
| Swipe | Press on the target, then flick the way the arrow points, finishing as the ring closes. Graded when the flick finishes | Tests direction as well as timing |
| Swipe rules | Must travel 90 units, within 40° of the arrow. The wrong way, or lifting without flicking, fails | Forgiving enough for thumbs, but a tap can't pass as a swipe |
| Drag | Press as the ring closes, then keep your finger on a marker travelling along a path. The press sets the grade | Tests tracking, which is a different skill from timing |
| Drag rules | Straying over 170 units or lifting before 90% of the path fails | The final stretch is forgiven, so finishing a moment early isn't punished |
| Mix | 15 patterns. Every tier includes all three gestures, and most multi-step patterns mix them | Starts variety from the first encounter |
| Labels | Each target shows its number and TAP, SWIPE or HOLD, with its own color | Gestures must be readable at a glance |
| Grading | Same as before: one failed step fails the pattern, and the weakest step sets the grade. Miss, Good and Perfect multipliers are unchanged | n/a |

Patterns and gesture limits are in `game_content.json`.

## Results

Simulated thumbs running the real gesture rules. These are rough models of timing, aim and tracking error, and they don't include reading the gesture, finger reach or fatigue.

| Simulated player | Tier 0: Perfect / Good / Miss | Tier 3: Perfect / Good / Miss |
|---|---|---|
| Sharp | 71% / 29% / 0% | 40% / 46% / 14% |
| Typical | 44% / 43% / 13% | 19% / 40% / 41% |
| Casual | 29% / 37% / 34% | 11% / 25% / 65% |

| Simulated win rate, attacks and heals | Sharp | Typical | Casual |
|---|---|---|---|
| Tier 3, level 4 party (fair) | 100% | 100% | 100% |
| Tier 6, level 4 party | 99.5% | 83.5% | 38% |
| Tier 10, level 6 party | 93.8% | 40% | 1.5% |

## Verified

- 60 of 60 EditMode tests pass, 8 of them new.
- Scripted presses, flicks and marker-following went through the real pointer handlers at tier 5. 13 patterns appeared. Clean runs graded Perfect, while wrong-way flicks, early lifts and no input graded Miss. Good grades were covered by the tests rather than this run.
- That battle took 58.6 seconds for 25 actions, including 0.25-second scripted menu waits.

## Not verified

- **Nobody has played this.** Real thumbs may find these gestures harder or easier than the model does.
- **Unity's real touch pipeline is untested.** Scripted events called the handlers directly, so an actual finger's drag threshold and phone touch delay haven't been exercised.
- The final arrow size change was checked by compiling, not with a new screenshot.

## Known issues

- **Tier 0 got slightly easier in the model:** a typical player's misses went from 19% to 13%, because three of the four starting patterns have one step. Tier 3 is about the same. New gestures are probably harder in practice than the model shows, so judge this in play before changing it.
- **Longer patterns:** `gauntlet` now lasts 2.2 seconds, and most battles are not getting shorter.
- **Hardest patterns:** `flurry` and `gauntlet` fail about 88% and 86% of the time for a typical player in the model.
- **Thumb position:** a drag's end point can be beside the next target, so back-to-back steps may suit two thumbs better than one.
- **No teaching:** nothing explains swipes and drags except the labels. This belongs with the planned wordless training fights.
