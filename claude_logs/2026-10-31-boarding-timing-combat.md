# Boarding Rework: Three-Member Party and Timed Prompts

- **Date:** 2026-10-31
- **Participants:** Cody Nicholson, Claude Code (Opus 5.5)
- **Status at end of this entry:** implemented and running in the Editor. Not yet played by a person or on a phone, and not committed.

The single ring, grade windows and hit rates below were replaced by harder patterns. See `2026-10-31-whale-speed-timing-patterns.md`.

## Decisions

Cody decided:

- A boarding party has three members instead of four, so combat is shorter. This replaces the kickoff decision of up to four.
- Turn-based combat gains real-time elements, similar to *Clair Obscur: Expedition 33*.
- On-screen touch triggers appear when attacking and when being attacked. Hitting them raises damage dealt or lowers damage taken.

Claude's choices, all open to change:

| Topic | Choice | Why |
|---|---|---|
| The fourth crew member | All four stay in the crew, and the player chooses who boards at the Port with a SWAP button. Captain Vale always boards | Keeps the existing characters, and the kickoff says the party includes the main character |
| Default party | Vale, Mirren, Odessa | The sturdiest of the three combinations in testing, with a healer |
| Reserve crew | Still earn XP | Avoids punishing a party choice |
| Enemy crews | One fewer deckhand: 2 enemies, or 3 from tier 3 | Removing a party member alone made battles longer, because fewer attackers means more rounds |
| The prompt | A ring closes on a target circle in right-thumb reach, landing on the attack's strike frame. Tapping anywhere on screen counts | Lets eyes stay on the action, and reading the swing helps the timing |
| Grades | Perfect within 0.08 s, Good within 0.18 s, otherwise Miss | Starting values, to be tuned on a real phone |
| Attacking | Good 1.2× damage, Perfect 1.5× | Heals and magic use the same multipliers |
| Defending | Good takes 0.65× damage, Perfect takes 0.3× | An area attack uses one prompt for everyone hit |
| Missing | No penalty | The request was for bonuses, and it keeps the game winnable without quick reflexes |
| Mashing | One tap per prompt, so tapping too early counts as a miss. Taps over 0.45 s early are ignored, so a double-tap on a menu button doesn't waste the prompt | Stops spamming without punishing menu taps |
| Guard | Unchanged, with no prompt, and stacks with a block | Not part of this request |
| Pacing | Small waits between turns were shortened | Part of the goal of shorter combat |

Unlike *Expedition 33*, there are no counterattacks, dodges or multi-hit sequences. Those could be later additions.

## Results

Measured with scripted bots over 400 battles per case, at matching levels. "Average" is assumed to be 20% Perfect, 40% Good and 40% Miss, and "Expert" 70%, 25% and 5%. Real players' hit rates are unknown.

| Tier 3 battle, attacks and heals | Total actions | Win rate |
|---|---|---|
| Before reducing enemy crews, average timing | 34.6 | 100% |
| After, never tapping | 31.9 | 100% |
| After, average timing | 25.5 | 100% |
| After, expert timing | 20.0 | 100% |

With attack skills used well, the same tier takes about 15 total actions at average timing, and 13.5 at expert.

Timing matters most when the party is under-leveled, as it is in real progression:

| Tier 6 enemies with a level 4 party | Win rate |
|---|---|
| Never tapping | 47% |
| Average timing | 97% |
| Expert timing | 100% |

| Tier 10 enemies with a level 6 party | Win rate |
|---|---|
| Never tapping | 2% |
| Average timing | 84% |
| Expert timing | 100% |

## Verified

- All EditMode tests pass, including 8 new ones covering party rules, older saves without a party, and each grade's effect on damage and healing.
- Real UI buttons and pointer taps were driven by script, not autoplay: SWAP, Attack, Skills, Guard, target menus, and all three grades on attack and defense. That run covered a party with Tobin in it.
- That battle finished in victory with no console errors: 21 actions in 39 seconds. The script waited 0.3 seconds between menu presses, and a person may be slower.

## Not verified

- **Nobody has played this.** The script tapped at exact moments, so whether the windows feel fair is unknown. Phone touch delay could make Perfect much harder than it looks here.
- Nothing has run on a phone.
- A lost boarding battle wasn't seen with the prompts.

## Known issues

- **Prompts are mandatory attention:** every action shows a prompt, including every enemy turn. This might feel tiring across many encounters.
- **Skills remain strongest:** using attack skills is still far quicker than basic attacks.
- **Comparison limit:** I can't give an exact before-and-after for three against four members, because the four-member battles were never measured with the same bots.
- **Visuals:** the "Attack" label can overlap the turn arrow, and "LEVEL UP!" still crosses the results panel.
- **Online play:** the timing grade is an input from the player's device, so a future server would need to guard against cheating. Only matters if boarding comes to multiplayer.
