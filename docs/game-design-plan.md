# Ransom Skies: Game Design and Production Plan

- **Date:** 2026-10-31
- **Produced with:** the `design-game` skill, all five phases
- **Companion record:** `claude_logs/2026-10-31-design-audit.md` holds the detailed audit evidence and decision history

## How to read this plan

Three kinds of statement appear below:

- **Measured:** results from running the real rules with scripted bots, 200 to 300 runs per case. Bots aren't people, so trust comparisons more than exact percentages.
- **Observed:** seen in the code, or in an autoplay run in the Editor.
- **Proposed:** starting values and designs that haven't been built or tested. Every proposed number is meant to be tuned through the playtests in Phase 5.

Nobody has played this game on a phone yet, so touch feel and real device performance remain unknown.

## Verdict

The first playable version is a solid technical foundation with an unproven game on top of it. The loop runs from start to finish with no console errors, and the rules, data and art direction are worth keeping. The design problems are fixable, but they're in the core: the dogfight doesn't play as a duel, the Board choice isn't a decision, and difficulty rises until the game becomes unwinnable.

---

# Phase 1: Intake

The script `validate_design_spec.py` reports the settled target design as `VALIDATED` for mobile, with no warnings. It checks only that fields exist and scans for keywords, so this doesn't say the design is good or that the current build matches it.

| Field | Settled answer | Source |
|---|---|---|
| Title and hook | *Ransom Skies*. "Outfly a pirate in a real-time sky duel, then stake your ship to storm their deck." | Cody |
| Platform | Mobile, landscape, two thumbs. Android first on a Pixel 10 Pro XL, iOS later. | Cody, Claude default for landscape |
| Experience goals | Feel: daring captain, ace pilot, clever tactician, attached to a growing crew and ship. Think and do: see the matrix in section 3.3. | Cody |
| Core toy and Three Cs | A broadside duel between heavy ships. | Cody chose the duel; the specification is proposed |
| Loop and economy | Bounty, duel, wager, deck battle, port. | Cody chose the structure; the economy is proposed |
| Theme | Freedom has a price. | Cody |
| Narrative and onboarding | Hybrid structure with named rivals. Wordless training is proposed. | Cody chose the structure |
| Monetization and scope | Freemium, cosmetic only. Airships and upgrades cost in-game currency only. Skins cost in-game currency, and exclusive special skins are also sold for real money. Next build is a one-region vertical slice. | Cody |
| Multiplayer stakes | Ships can't be lost in multiplayer. | Cody |

# Phase 2: Case-Study Risk Audit

| Vulnerability found in this game | Evidence | Failure case | Improvements, with benchmark |
|---|---|---|---|
| Stats scale with no player control | **Measured:** every win adds 18% enemy hull and damage with no cap. 0 of 800 bot campaigns reached 30 wins. | *Anthem* | From *Breath of the Wild*, make rules consistent and visible. 1. Give each region a fixed, labelled threat. 2. Escalate through new enemy behaviors. 3. Keep earlier regions replayable. |
| Upgrades are percentages, and half barely matter | **Measured:** 3 levels of hull, armor or broadside took tier 3 wins from 37% to about 100%. Bow, rudder and engine reached only 40% to 46%. | *Anthem* | 1. Turn upgrades into abilities such as chain shot or grapeshot. 2. Add ship classes as build choices. 3. Show a before-and-after preview when buying. |
| Gold has only one drain, which caps out | **Observed:** the only drain is an upgrade shop that totals 8,065 gold. Nothing else consumes gold. | *Diablo III* | From *Hades*, use lasting, layered sinks. 1. Add repairs, new hulls, recruits and recapture costs. 2. Convert captured parts into abilities. 3. Never sell ship protection or recovery for real money. |
| Classes share two skill templates | **Observed:** every skill is damage times power, or a heal. | *Suicide Squad* | From *Hades* and *BioShock*, express identity through mechanics. 1. Give each class a signature ability. 2. Let the Stormcaller change deck weather. 3. Give the Surgeon a revive. |
| The dogfight has no way to answer an attack | **Observed:** no dodge exists, and volleys have no warning. **Measured:** one steering pattern stalemated 300 of 300 fights. | *Balan Wonderworld* | From *Astro Bot*, make movement great first. 1. Add one evasive move that's never disabled. 2. Telegraph volleys. 3. Run a 60-second toy test before tuning anything else. |
| Falling behind makes things worse | **Measured:** leaving raises difficulty and pays less, so bots that always left stalled sooner (tier 6, against 9). Retreat pays 0 gold while losing pays 15. | *Monopoly*-style runaway | From *Left 4 Dead* and *Mario Kart 8*. 1. Add recovery valleys between peaks. 2. Offer intel and salvage work to struggling players. 3. Raise notoriety only by choice. |
| Weak differentiation and same-shape ships | **Observed at audit:** player and enemy ships were the same mesh with different colors (since fixed by the art kit), and there's no tutorial. The hook exists on paper, but the game doesn't show it. | *Concord* | From *Slay the Spire* and *Apex Legends*. 1. Show enemy intent each turn. 2. Give each ship class its own silhouette. 3. Make the grapple-and-board moment the 5-second clip. |

### Risks in the decisions made during this audit

These decisions are workable, but each carries a risk the plan has to manage.

- **Ship loss by default** could drive players away if a loss feels random. **Measured:** boarding wins for players not using attack skills fell from 96% at tier 3 to 41% at tier 4. Fix that before adding stakes. Ship loss ships last in the roadmap.
- **All four experience goals** could blur the game. Section 3.1 gives each goal one home, and the daring captain wins any conflict. That tiebreak is Claude's suggestion.
- **Medium stakes setting** could be exploited. The setting should change only in port, never during an encounter.

---

# Phase 3: The Four-Book Blueprint

## 3.1 One-Sheet and Tetrad Harmony Map

| One-Sheet | |
|---|---|
| **Title** | *Ransom Skies* (store, trademark and domain checks pending) |
| **Genre** | Real-time ship duels and turn-based party RPG |
| **Platform** | Android first, then iOS. Landscape. |
| **Target audience (proposed, unvalidated)** | Adults and teens who enjoy classic JRPGs and pirate fantasy, and want a real adventure in sessions of a few minutes |
| **Hook** | Outfly a pirate in a real-time sky duel, then stake your ship to storm their deck. |

**Key features**

1. **Two battles, one fight:** how you win in the sky shapes the battle on the deck.
2. **Real stakes:** board an enemy ship and you can lose your own, then hunt its captor down.
3. **Rivals with memory:** the captain who takes your ship becomes a named enemy with a bounty.
4. **A crew of four:** classes with signature abilities, personal stat growth and levels.
5. **Your ship, your style:** ship classes and cannon types earned in play, with cosmetic skins that survive ship loss.

**Where each experience goal lives**

| Goal | Home | Peak moment |
|---|---|---|
| Daring captain | Bounties and the Board or Leave wager | Tapping BOARD with your ship on the line |
| Ace pilot | The duel | Slipping a telegraphed volley, then answering with a full broadside |
| Clever tactician | The deck battle | Reading a captain's intent and countering it |
| Growing crew and ship | The port | Fitting a captured cannon, or a crew member learning a new ability |

**Theme: freedom has a price**

| Element | Today (observed) | Target (proposed) |
|---|---|---|
| **Aesthetics** | Strong. The bright sky, floating islands and faceted style already suit the theme. Audio is placeholder and unheard. | Storm fronts that visibly threaten the open sky. Hull damage visible on your ship. A grapple-and-swing transition. |
| **Mechanics** | Weak. Nothing can be lost, so freedom costs nothing. | Stake what you own to take what you want. Repairs, wagers and recapture. |
| **Story** | Absent, by design for the first version. | Rival captains who take and lose ships. Every feud comes from something the player did. |
| **Technology** | Strong. Rules run separately from Unity and are deterministic. | Battles resume after interruptions, and a server can later settle online results. |

## 3.2 Formal and Dramatic Systems

**Formal elements**

| Element | Specification |
|---|---|
| **Players** | Solo against the system for now. 1v1 online duels come after single-player, per the kickoff decisions. |
| **Objectives** | Outwit in the duel, capture on the deck. Long term: defeat each region's rival. |
| **Procedures** | *Start:* choose a bounty from the sky map. *Progress:* duel. *Special:* wager on boarding, or leave. *Resolve:* deck battle, then rewards and refit in port. |
| **Rules** | Ships fire only inside visible arcs. Hits on bow or stern rake for extra damage. Ship damage carries over until repaired. Losing a boarding battle costs the wager. |
| **Conflict** | *Opponents:* enemy ships and crews. *Obstacles:* cloud banks, storms and the arena edge. *Dilemmas:* board or leave, repair or save, close in for more damage or stay safe. |
| **Boundaries** | A 300 m arena per fight, enforced by wind (existing). Entry ritual: leaving port. Exit ritual: returning to it. |
| **Outcome** | Each encounter has a clear win or loss. Each region ends at its rival's defeat. Bounties continue afterward. |

**Resources (proposed starting values)**

| Resource | Utility | Scarcity | Source | Drain | Converter |
|---|---|---|---|---|---|
| **Gold** | Buys repairs, parts, hulls and recruits | Earned per encounter | Bounties, boarding plunder, salvage jobs | Repairs at 0.15 gold per hull point (about 69 for a full repair of the 460-hull starter), parts, hulls, recapture | Gold buys ship abilities |
| **Hull** | Survival across encounters | 460 on the starter ship, carries over | Repairs at port | Enemy fire | Gold into hull |
| **Captured parts** | Unlock new cannons and fittings | Boarding wins only | Winning a boarding battle | Spent at the shipwright | Parts and gold into abilities |
| **Notoriety** (replaces the hidden win counter) | Unlocks richer bounties and draws rivals | Visible, by region | Winning wagers | Falls after losing a wager | Notoriety into bounty access |
| **Crew XP and levels** | Stats and new skills | Curve exists today | Encounters | None, since it's permanent | XP into abilities |
| **MP** | Skills in deck battles | Resets each battle | Full at battle start; Guard restores some | Skills | MP into damage or healing |
| **Your ship** | The ultimate stake | One active hull | Starter is free, others are bought | Lost on a failed wager | Recaptured by bounty |

**Dramatic elements**

| Element | Specification |
|---|---|
| **Challenge** | Threat labels let players choose their level. A flow target is listed in Phase 5. |
| **Play** | Free flying between objectives gives open play. Wagers and battles give structured play. |
| **Premise** | Pirates in a world where battleships fly. The sky belongs to whoever dares to take it. |
| **Character** | The player is Captain Vale, with Mirren, Tobin and Odessa as crew. Each needs one defining trait and signature ability. |
| **Story** | Light authored arcs per region, plus feuds that emerge from play. |
| **World** | A broken sky of floating islands and storm fronts, with lawless ports as hubs. |
| **Dramatic arc** | Each region: small-time raider, noticed by a rival, loses or nearly loses something, then settles the score. |

## 3.3 Meaningful Play and Cybernetic Loops

| Player action | Discernibility: now (observed) | Discernibility: target (proposed) | Integration: next 10 seconds | Integration: next 10 minutes | Integration: campaign |
|---|---|---|---|---|---|
| **Steer and throttle** | Banking and propeller speed. The benefit of slowing to turn tighter is hidden. | Wake trail, a shown turning arc, and lit firing arcs | Sets who has the angle | Ship class changes how you fly | Pilot skill carries into online play |
| **Fire broadside** | Flash, sound, shake and damage numbers. No arc or range indicator. | Arc lights when it bears, one volley sound, heavy haptic, "RAKED!" on stern hits | 5-second reload leaves a window to punish | What you hit shapes the deck battle | Choice of cannons defines your build |
| **Fire bow** | Works, but is rarely relevant. **Measured:** could bear only 1% to 7% of a fight. | Chain shot visibly slows the target's turning | Sets up the next volley | A different loadout for each enemy type | Part of your ship's identity |
| **Evade** (new) | Doesn't exist | Roll animation, a whoosh, light haptic and a cooldown ring | Dodges a telegraphed volley | Keeping hull lowers repair bills | Mastery lets you take on higher threats |
| **Board or leave** | A prompt that shows only the reward | Names the ship at risk, threat level and reward | Commits you to the deck | Win parts, or lose your hull | Creates rivals and feuds |
| **Choose a deck command** | Numbers, flashes and hit effects. No skill text or enemy intent. | Enemy intent icons, skill text, highlight on the target | Counters the next enemy move | MP spending decides the fight | Crew abilities define party style |
| **Guard** | Text and a small effect. **Measured:** added length but no extra wins. | Restores MP and blunts telegraphed attacks | Trades tempo for safety | Enables a stronger skill turn | Rewards reading enemies |
| **Choose a bounty** | Doesn't exist. The next fight is automatic. | Threat label, reward and a rival's mark on the map | Sets the coming fight | Paces risk and income | Drives notoriety and the region arc |
| **Buy or fit an upgrade** | Gold and level change, with a flash. | Before-and-after preview, and a visible change on the ship | Changes the next fight | Commits scarce gold | Forms a ship build |

**Required loops**

- **Triangular choice (Lens #36):** after a duel, *Leave* banks a small, safe reward. *Board* risks your ship for parts and plunder. Inside the duel, staying at range is safer, while closing in lands more of each volley.
- **Negative feedback stabilizers:**
  1. A free basic ship and risk-free salvage jobs mean a player can never be stranded.
  2. After two lost encounters in a row, the port offers intel on an enemy's habits or a lower-threat bounty.
  3. Notoriety falls after a lost wager, so rivals and threats ease off.
- **Positive feedback:** winning wagers raises notoriety, which brings richer bounties and rival attention. Rewards and danger rise together, and the player chooses when to take them on.

## 3.4 Three Cs and Metrics

### Character

*Updated after the Blender art kit was integrated. See `claude_logs/2026-10-31-art-kit-integration.md`.*

- **Crew (observed):** characters are now rigged kit models with eight animation clips. Hats and props keep them distinct: Vale's coat and cutlass, Mirren's goggles and flintlock, Tobin's wizard hat and staff, Odessa's medic's cap and tonic bottle.
- **Enemies (observed):** the Pirate Captain is now a large Brute with a tricorn and axe, so the captain no longer resembles Vale. Brigands and Bombardiers use the kit's dark Obsidian palette.
- **Crew problem (observed):** the back of the party line still overlaps slightly from the battle camera, though everyone is now visible.
- **Ships (observed):** the kit gives three distinct airship silhouettes. The player flies a Sloop, enemies fly Brigantines, and enemies from tier 6 fly Galleons. Size is cosmetic until ship classes exist.
- **Ship damage:** states should show as smoke, torn sails and a damaged envelope (proposed), extending the smoke that exists today.

### Ship metrics

| Metric | Today (observed) | Proposed start | Why |
|---|---|---|---|
| Top speed, player | 34 m/s | 24 m/s | Fights need time for positioning |
| Top speed, raider | 30 m/s | 21 m/s | Keeps the player's edge |
| Turn rate, player | 70°/s | 40°/s | Heavy-ship feel, and a turning radius near 34 m |
| Turn rate, raider | 60°/s | 34°/s | Prevents endless tight orbits |
| Minimum throttle | 35% | 35% | Keep. Slowing to tighten the turn is a good skill. |
| Hit shape | 7 m circle | Hull-shaped capsule, about 24 m by 8 m | Makes angle matter and allows raking |
| Ship collisions | None | Glancing bounce, small damage to both | Fixes overlapping ships |
| AI standoff | Turns side-on inside 88 m, then collapses to about 30 m or less | Holds 50 to 90 m, breaks off under 40 m | Keeps the fight at gun range |
| Broadside | 5 shots, 24° fan, 5 s reload, 110 m | Unchanged to start | At 70 m about 3 of 5 shots should connect, and at point-blank all 5. That rewards a risky close approach. |
| Bow gun | 10 damage, 0.45 s reload | Chain shot: low damage, slows target's turn 30% for 3 s | Gives it a different job from the broadside |
| Raking bonus | None | 1.5× on bow or stern hits | Rewards crossing behind an enemy |
| Evade | None | 2 s sideways burst, 0.4 s of no hits, 8 s cooldown | The answer to telegraphs |
| Enemy volley warning | None | Gun ports glow for 0.9 s, with sound | Gives enough time for a thumb to react |
| Target fight length | 22 to 77 s in bot fights at tier 0 | 45 to 75 s | Short enough for mobile, long enough for several volleys |

### Camera

**Observed problem:** the chase camera sits at offset (0, 19, −54) with a 52° field of view, looking ahead of the ship. In the autoplay run, an enemy 61 m away was off-screen beside the BOW button, marked only by a diamond at the screen edge. This is the worst possible framing for a broadside duel, because a target in firing position is off to the side.

**Proposed:**

- Pull up and back to about (0, 30, −62), and swing the camera up to 35° toward the enemy so both ships stay in frame.
- Shift the player's ship off-center, opposite the enemy (rule of thirds).
- Keep the off-screen marker clear of the fire buttons, and add a direction arrow around the ship itself.
- Clouds should fade rather than block the view. Today's clouds never cross the play plane, but the new cloud banks will.
- **Battle camera:** spread the party positions so no one is hidden, and center the composition. The lower half of the screen is currently empty deck.

### Controls

| Input | Action | Notes |
|---|---|---|
| Left thumb, floating stick on the left 40% of the screen | Steer and throttle | Exists on the left 48%. Keep the floating behavior, which matches the blueprint. |
| Right thumb, large button | Broadside: hold to ready, and it fires automatically when the arc bears | Today it fires immediately even if out of arc, wasting the 5-second reload |
| Right thumb, medium button | Bow chain shot | Keep to tap or hold |
| Right thumb, third button | Evade | New. Three is the blueprint's limit, and movement verbs are never disabled. |
| Deck battle | Tap commands, then tap the target on deck or in the list | List-only today |

- **Input buffering:** a fire or evade press in the last 120 ms of a cooldown triggers when it's ready.
- **Grace window:** a volley can still fire within 100 ms after the arc leaves the target.
- **Keyboard controls** stay as a development aid only.

## 3.5 Master Beat Chart (one-region vertical slice)

The region and rival names are placeholders. Encounters target 3 minutes or less, per the mobile blueprint.

| Level/Beat | Location & TOD | Story/Emotional Beat | Alley vs. Arena Structure | New Toy/Mechanic (Training Cage) | Enemy Archetypes/Hazards | Economy/Rewards | Interest Curve Intensity (1-10) |
|---|---|---|---|---|---|---|---|
| **1. Cast Off** | Home port waters, morning | Freedom: your first ship, your first sky | Alley: free flight through marker rings | Steering and throttle. No enemy, no failure, no text. | None | None | 3 |
| **2. Target Practice** | Harbor approach, morning | Swagger: firing the guns for the first time | Training cage: a tethered hulk | Broadside arcs. Must land one volley to leave. | Tethered hulk that can't fire | Small gold | 4 |
| **3. First Blood** | Open sky, midday | Thrill: a real pirate shows up | Arena, low threat | Telegraphed volleys and Evade | Scout, fast and fragile | First bounty, first repair bill | 6 |
| **4. Over the Rail** | Scout's deck, midday | Daring: the first boarding, safely | Training cage: deck battle, nothing staked | Skills and enemy intent icons | Brigands | First captured part | 5 |
| **5. Port of Call** | Lawless port, afternoon | Relief, then a rival's name on a wanted poster | Hub, rest valley | Sky map, bounty choice, shipwright, repairs | None | Spend gold, fit chain shot | 2 |
| **6. Into the Murk** | Cloud banks, afternoon | Tension: you can't see them either | Alley into arena | Cloud banks hide ships. Bow chain shot. | Raider, plus a Bombardier on deck | Bounty and parts | 6 |
| **7. Heavy Guns** | Storm's edge, late afternoon | Dread: outgunned, must outmaneuver | Arena | Raking fire on a slow target, and enemy healers on deck | Gunboat with long range and slow turns, enemy Surgeon | Big bounty, new cannon type | 7 |
| **8. The First Wager** | Open sky, dusk | The theme arrives: the prompt names your ship | Arena, then a real wager | Ship stakes unlock. Reward for the first wager is high. | The rival's lieutenant, plus a Bosun who protects allies | Plunder and notoriety, or the recapture feud | 8 |
| **9. Safe Harbor** | Hidden cove, evening | Calm: the crew talks and prepares | Hub, rest valley | Recruit or refit, and a salvage job for anyone short on gold | None | Spend for the finale | 2 |
| **10. The Rival** | Storm front, night | Settling the score, with the whole game tested | Boss arena, three phases | Phase 1: baseline volleys. Phase 2: storm changes the arena. Phase 3: boarding, a final test of deck mastery. | Rival captain, with a unique ship silhouette | Region clear, rival's signature part | 10 |
| **Ongoing** | Sky map, any time | Your own stories: feuds and recaptures | Repeatable arenas | Recapture bounties if your ship was taken | Mix, plus the captor | Repeatable income | 4–8 |

**Complementary enemy roster**

| Role | Sky | Deck |
|---|---|---|
| Swarmer | Scout (new) | Brigand (exists) |
| Shielded brute | Ironclad (later) | Bosun, protects allies (new) |
| Ranged threat | Gunboat (new) | Bombardier (exists) |
| Controller or priority target | Rival's flagship (new) | Enemy Surgeon (new), Pirate Captain (exists, gains intents and phases) |

---

# Phase 4: Mobile Engineering and Production Plan

## Technical decisions

| Area | Decision | Today (observed) | Work needed |
|---|---|---|---|
| **Engine** | Stay on Unity 6 with URP. The blueprint recommends it, and the work is done. | Unity 6000.6.7f1 with URP | None |
| **Orientation** | Landscape, two thumbs | Landscape only | None |
| **Controls** | Floating stick, three radial buttons | Floating stick, two buttons | Add Evade and auto-fire on arc |
| **Safe areas** | Respect `Screen.safeArea` | HUD uses a safe area | Verify on the Pixel |
| **Sessions and interruptions** | Encounters of 3 minutes or less. Never lose progress. | Saves when backgrounded, but boarding battles aren't saved | See the save work below |
| **Thermal and battery** | Hold 60 fps on High. Drop shadows, then render scale, before frame rate. | Manual High or Low, with a memory-based first choice. No game code uses thermal data. | Add automatic quality stepping with Android thermal status, and measure on the device |
| **Haptics** | Android `VibrationEffect`: light for evades and hits taken, heavy for volleys and telegraphs. `UIImpactFeedbackGenerator` on iOS later. | None | Add one haptics layer with a Settings toggle |
| **Audio** | Add an Audio Mixer and replace placeholders | Synthesized clips. Each of the 5 shots in a volley plays its own sound and shake. | Play one volley sound, then add the mixer in the Editor |
| **Offline-first** | The full single-player game works offline, with a local save. Cloud saves are optional later. | Local save only | Add Google Play cloud saves when the slice is proven |
| **Online readiness** | Keep commands-in, events-out rules. The server decides outcomes. | Ready in structure | Don't rely on identical float results across devices. Let the server's result stand. |

## Fixes required before ship stakes

These are the engineering risks that ship loss makes serious.

1. **Save migration.** `SaveStore.Load` discards any save whose version doesn't match, then starts a new game (`SaveStore.cs:30`). That would wipe players' progress the first time the format changes. Add stepwise migrations and a backup before any format change.
2. **Battle resume.** Write the wager to disk the moment the player taps BOARD. Battles are deterministic from a seed, so saving the seed and the list of actions lets the game rebuild a battle exactly. A crash can't cost a ship, and force-quitting can't dodge a loss.
3. **Rules and tests.** Move ship stakes, notoriety and regions into `Assets/Game/Core`. Replace `PartyUsuallyWinsAtMatchingLevel`, which assumes equal levels that real play doesn't produce from tier 3. Add a scripted-campaign balance test.
4. **Purchases.** Keep cosmetics on the account, separate from ship data, so paid items survive ship loss.

## Build and release tasks

- **Store format:** the build produces an APK, but Google Play needs an Android App Bundle.
- **Signing:** set up and securely back up a release signing key. No custom keystore is configured yet.
- **Package name:** `com.codynicholson.airships` can't change after a store release, so confirm it first.
- **Package cleanup:** check whether `com.unity.ai.inference` and `com.unity.ai.assistant` can be left out of player builds. Nearly all build warnings come from `com.unity.ai.inference`. Also check the unused `com.unity.purchasing` until in-app purchases begin.
- **Device coverage:** test Low mode on a budget Android phone, as the kickoff log already planned.

## Roadmap

Each milestone has an exit test, and later milestones shouldn't start until the earlier one passes. Sizes are relative, because there's no measured team speed yet.

| # | Milestone | Scope | Size | Exit test |
|---|---|---|---|---|
| 0 | **Baseline** | Commit the project, install the current build on the Pixel, record frame rate and first impressions | Small | The game runs on the phone, with notes recorded |
| 1 | **Duel toy** | Week 1 prototype below | Medium | Passes the toy test and the duel checks in Phase 5 |
| 2 | **Connected loop** | Damage carryover, repairs, an honest Board prompt, grapple transition, deck effects from the duel | Medium | Testers debate Board or Leave |
| 3 | **Deck rework** | Enemy intents, signature abilities, useful Guard, target highlighting, skill text, revive | Medium | Testers use different approaches, and predict enemy moves |
| 4 | **Vertical slice** | Sky map, region, beats 1 to 10 without stakes, wordless training, rival boss, resource drains, save migration | Large | First-timers finish training without help, and the difficulty curve holds |
| 5 | **Ship stakes** | Wager, ship loss, recapture feud, battle resume, stakes setting, free fallback ship | Medium | Testers who lose a ship want to go back and recapture it |
| 6 | **Polish and release prep** | Audio, haptics, thermal scaling, ship silhouettes, App Bundle, signing | Medium | Stable on the Pixel and a budget phone |

Later, once the slice is proven: more regions, cosmetic store, iOS, then 1v1 online duels.

---

# Phase 5: Prototype and Playtest Plan

## Day 1: Paper prototype

Test the decisions before the code. This needs index cards, two six-sided dice, a pencil and one other person.

**A. The wager and the economy (most important)**

1. Keep a ledger starting with 100 gold, a 460-hull ship and 0 parts.
2. Play 10 encounters. For each, roll 2d6 × 15 hull damage for the duel, and write down the repair bill at 0.15 gold per hull point.
3. Decide whether to board. Show the win chance by threat: low threat wins on a roll of 4+ on 2d6, medium 6+, high 8+.
4. Use these rewards: Leave pays 60 gold, a boarding win adds 90 gold and 1 part, and a loss takes the ship.

Questions to answer:

- Did the choice ever feel hard?
- Does gold run out or pile up by encounter 10?
- Does a player who lost their ship want to continue?

**B. Enemy intent**

1. Make a card for each party member and enemy with HP, one attack and one signature ability.
2. Before each round, reveal each enemy's intent card.
3. Play 3 battles with intents visible and 3 hidden.

Question to answer: do decisions improve and feel more interesting with intent shown?

**C. Duel geometry**

1. Cut out paper ships with marked side arcs, on a table with a ruler.
2. Take turns moving a limited distance and angle, then fire if the enemy is in your arc.

Question to answer: is there a real contest for angle at these turn rates, or does it collapse into circling?

## Week 1: Grey-box prototype

**Build only this:**

- The existing ship models, with the current sky and no new art
- New speed and turn metrics, hull-shaped hit areas and ship collisions
- A standoff AI holding 50 to 90 m
- The new camera with both ships in frame
- Firing arcs drawn on the ship, with auto-fire on arc
- Telegraphed enemy volleys and Evade
- Raking damage, and chain shot on the bow
- One debug panel for live tuning, reading from `game_content.json`

**Leave out:** boarding changes, regions, stakes, story, sound work and new models.

**Test in this order, on the Pixel:**

1. **Toy test, 60 seconds, no enemy:** can someone enjoy just flying and firing at empty sky?
2. **Duel test:** fight the AI 10 times and record results.
3. **Bot regression:** rerun scripted bots to confirm the stalemate is gone and no single approach dominates.

**Pass criteria (proposed):**

- Fights spend most of their time inside 40 to 110 m.
- Both weapons contribute, with neither under about 20% of damage for bots that use both.
- A skilled bot beats a hold-both-triggers bot by a clear margin.
- Fights last 45 to 75 seconds.
- No stalemates occur in 300 bot fights.
- The game holds 60 fps on the Pixel on High.

## Blind playtest protocol

Use 3 to 5 people who haven't seen the game, in sessions of about 20 minutes on the Pixel. Ask them to think aloud. Don't explain or defend anything, and answer questions with "What do you think it does?"

| # | Checkpoint | Watch for | Think-aloud prompt | Schell filter | Pass signal |
|---|---|---|---|---|---|
| 1 | **First 60 seconds of flight** | Do they experiment? Smile? Fight the stick? | "What are you trying to do right now?" | Experience design | They keep playing with the ship without being told a goal |
| 2 | **First broadside** | Do they notice arcs, and line up without help? | "How do you know when to fire?" | Engineering and experience design | They line up a shot by themselves within 2 attempts |
| 3 | **A telegraphed volley** | Do they see the glow and react? Do they call the hit fair? | "Why did you take that hit?" | Playtesting | They can explain why they were hit |
| 4 | **The Board prompt** | Do they hesitate? Do they reason aloud? | "What would make you leave instead?" | Innovation | They weigh it out loud, and some choose differently |
| 5 | **After a loss, and at the end** | Do they want another go? Can they describe the game? Do they mention paying? | "How would you describe this game to a friend?" and "Would you play tomorrow? Why?" | Demographics, business and marketing, artistic impulse, community | They describe the hook unprompted and want to continue |

Record for each session: time to first volley, deaths, Board or Leave choices and reasons, moments of confusion, and exact quotes. Act on patterns seen across players, not one person's preference. If a checkpoint fails, go back to the relevant prototype before adding content.

**Flow check:** repeated losses should lead to clear, usable help such as intel or lower-threat bounties. Flawless wins should lead to higher-threat options.

## Artifact output

This document is the skill's required output: `docs/game-design-plan.md`. `docs/design-spec.json` wasn't requested, so it wasn't created.
