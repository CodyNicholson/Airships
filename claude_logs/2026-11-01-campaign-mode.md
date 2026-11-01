# Single-Player Campaign: Shattered Skies

- **Date:** 2026-11-01
- **Participants:** Cody Nicholson, Claude Code (Opus 5.5)
- **Status at end of this entry:** implemented and run by automated players. Not yet played by a person or on a phone, and not committed.

This entry supersedes the region structure and named crew in `docs/game-design-plan.md`.

## Decisions

Cody decided:

- Add a single-player campaign that follows his design plan: an in-media-res invisible tutorial, Mega Man-style ship capture, a prologue and four acts, Davy Jones' Choice, live comms barks, a Quarterdeck hub and a Nemesis Bounty Board.

Claude's choices where the plan conflicted with earlier decisions or left a gap, all open to change:

| Topic | Choice | Why |
|---|---|---|
| Game title | The game stays *Ransom Skies*, and "Shattered Skies" is the campaign's name | Cody chose the title earlier, and the plan didn't say to rename it |
| Old crew | Captain Vale is replaced by Vane. Mirren, Odessa and Tobin become the veteran crew lost in the prologue. Rook, Lyra, Sable, Dr. Thorne and Morrow are the new roster | The plan says the veteran crew is lost, so this keeps the old names meaningful |
| Theme | "Freedom has a price" stays, and is Vane's last line | Cody's earlier decision fits the betrayal story |
| The old loop | Kept as a separate mode, **Free Skies**, which unlocks after the prologue. It keeps its rising tiers, whale and the old Board or Leave prompt | The request was to add a mode |
| Ship loss | **Not added.** The medium "ship loss on boarding" wager from the audit isn't in this plan, so losing a fight means retrying from a checkpoint | Cody's plan describes nemeses, not ship loss. This is an open question for Cody |
| Boss rewards | Whatever you pick at Davy Jones' Choice, a boss's signature weapon is always yours. Only Seize gives you the ship | The plan promises each boss's weapon, and also offers Scuttle and Press-Gang. Act II also needs the harpoon |
| Signature weapons | Equippable on any ship, in Fleet | Follows from the rule above, and adds a build choice |
| Crew limit | Three board as before. The final deck brawl allows up to six | The plan says the full crew fights there |
| Sable's sabotage | Sable fights in the party while a five-round timer runs, rather than leaving the line | Keeps battles simple, and she stays useful |
| Below-deck levels | Sequential deck battles with wounds carried over, 25% healed, under a dark ceiling on the same deck model | No interior art exists yet |
| Ship look | Five named ships use the kit's three silhouettes with different palettes | No new ship art was in scope |
| Press-Gang officers | Bosun Brill, Hask, Sawbones Wren and Ivo are new optional crew | The plan asks for officers but doesn't name them |
| Portraits | Rendered at runtime from each character's 3D model | The plan asks for comic portraits, and none exist as art |
| Voice | Text barks only | No voice recordings exist |

## What was built

**Story:** 21 missions and 79 phases, in `Resources/campaign_content.json`. That's 17 story missions across the prologue and four acts, plus 4 loyalty side missions.

**Mechanics, in the order the story teaches them:**

| Act | Taught | How it works |
|---|---|---|
| Prologue | Steering, throttle, broadside, emergency grapple, deck combat | Fly the Sovereign Gale, then the betrayal burns it while a scout closes in. Controls appear one at a time and pulse until used |
| I | Subsystem aim, multi-hook boarding, flak, claim or strip | Engines and guns have their own health. A brig takes three hooks, and drifting snaps them. Sinking pays 20% scrap |
| I boss | Krag's tether | His harpoon drags both ships down to a crushing floor. Climb the line, beat his bruisers, then duel him alone |
| II | Altitude, wind currents, cloud stealth, chain and fire shot, defend or board | Altitude is locked until Lyra joins. Winds double airspeed. Clouds hide you, and your first volley from one hits 1.5x. Fire shot wounds the crew you'll meet on deck. Defenders reduce hull lost while boarding |
| II | Sable's recruitment and sabotage | Beat her bodyguards without felling her. Her charges then force a surrender after five rounds |
| II boss | Stealing a ship mid-fight | Harpoon Vex, board, then fly Vex's clipper to hunt Vesper, whose damage carries over |
| III | Counter-boarding, below-deck levels, mortars, Overdrive, hull patch | Gliders that reach you become a deck fight on your own ship, against as many boarders as landed |
| III boss | Fighting on two fronts | Defenders hold your deck while the party takes the Bastion |
| IV | Lightning, updrafts, ship-hopping, radar, weak points | Lightning warns, then strikes. Morrow, if spared, brings radar and stern weak points |
| Final | Three-stage fight | Sky duel, full-crew brawl, then a solo Lift-Core duel with Cassian |

**Systems:**

- **Davy Jones' Choice:** Seize the Helm, Press-Gang, or Scuttle & Plunder, as the plan describes.
- **Aether-Comms:** barks with portraits, triggered by events in flight and on deck.
- **Quarterdeck hub:** the main menu is your ship's deck. Tap crew for short conversations that build loyalty, unlock perks and open side missions, including Rook's incendiary grapeshot.
- **Nemesis Bounty Board:** a named captain who flees or beats you gets tougher, returns in that sector with a taunt, and can be hunted for a bounty.
- **Fleet:** owned ships, repairs at 0.15 gold per hull point, equipped signature weapon and stripped modules. Damage carries between missions.
- **Save format:** version 2. Older saves are backed up and migrated, keeping the captain's level, gold and upgrades. A mission resumes from its last phase.

## Results

Simulated players running the real rules through the 17 story missions, 20 campaigns each. They differ in deck timing and flight aim, but all fly with a bot's reactions and perfect knowledge. Win rates are wins per attempt.

| Simulated player | Finished | Air fight length, by phase average | Lowest average hull left in a fight | Phases under 100% wins |
|---|---|---|---|---|
| Sharp | 20 of 20 | 16 to 119 s | 48% | None |
| Typical | 20 of 20 | 14 to 103 s | 53% | Cassian's duel 91%, glider boarders 88% |
| Casual | 20 of 20 | 17 to 110 s | 45% | Cassian's duel 40%, Morrow's deck 80%, Krag's duel 87%, five others 91 to 95% |

**Reading this:** air fights rarely threatened the bots, which may mean they're too easy, or may only reflect bot reactions. Fights that are mostly under a minute are near the plan's session target, but the shortest are probably too short. Only human play can settle this.

Bugs the balance runs found and fixed before any screen existed: aiming plus chain-shot destroyed engines in one volley, the harpoon boarded healthy ships at once, the final duel was close to unwinnable for typical players, and mid-game deck battles were too easy.

## Verified

- 100 of 100 EditMode tests pass, 40 of them new, on the final code. One of them plays the whole story with the automated captain.
- **A full autoplay run in the real game, on the final code:** all 21 missions from a new game through the ending, in 17 minutes at double speed, with 0 errors and 0 retries. The screenshots from it and the earlier runs were reviewed.
- **Scripted presses on the real UI:** crew talk, loyalty perk, Fleet and repair, Bounty Board, crew assignment, a nemesis bounty, the Port and Free Skies, with 0 errors.
- `Builds/Android/Airships.apk` built with 0 errors.

Bugs found by playing and looking, all fixed:

- The below-deck ceiling was attached to nothing, so it stayed in the world forever and swallowed the hub and Port. Its front wall also hid the fight from the camera.
- Autoplay always used the first skill, so it lost the solo duels. This was a bot bug, not a balance one.
- A data generator bug wrote the sky rules twice, so Unity read stale values. A test caught it.
- Repairs could take gold below zero by rounding.
- Reloading a save undid the crew assignment.
- Overlapping PATCH and OVERDRIVE buttons, comms covering the enemy panel, a clipped boss name and tangled crew tags.
- Updating the rules before the tests hung the Editor on an old test's unguarded loop. Those loops now fail instead of hanging. The Editor had to be force-closed, and Unity kept the scene backup as `Assets/_Recovery/0 (1).unity`. It can be deleted after checking that nothing was lost.

## Not verified

- **Nobody has played this.** The bots fly with perfect information and instant reactions. Sky difficulty for a person may be much harder than the numbers show, and teaching is the biggest unknown.
- **Touch input on a phone.** Six or more buttons on screen at once hasn't been tried with real thumbs. The plan's three-control limit is exceeded by the late game.
- **Pacing and writing.** Whether barks can be read during a fight is unknown.
- **Real-time fight lengths.** Numbers come from bot play.

## Known issues

- **Choices not driven in the UI:** Press-Gang and Scuttle are covered by rule tests, while the UI runs always chose Seize. Retry appeared in the earlier run, where autoplay kept losing to Cassian. Swapping in the Port wasn't re-checked with the new crew.
- **Below deck is a dressed set:** the deck's sail and sky edges still show, because there's no interior art.
- **Full crew crowding:** with six on deck, the party panel covers the front fighters' legs and the line overlaps.
- **Art reuse:** Vex's clipper and the Mirage are visually the same sloop, and the Gale is a recolored Galleon. Characters reuse kit models with different palettes and hats.
- **Press-Gang is the only way to get Morrow.** Choosing otherwise leaves radar and weak points locked, which is the plan's "optional" intent, but no mission offers a second chance.
- **Audio:** comms and new events reuse placeholder sounds.
- **Design-audit risks still open:** enemy intent icons and telegraphed volleys from the audit aren't part of this work.
