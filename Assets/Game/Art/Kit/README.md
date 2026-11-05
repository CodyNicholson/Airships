# Airship Pirates: Asset Kit

Every asset exists as `<Name>_HQ.glb` and `<Name>_LQ.glb` in `models/production/`. The game's
quality setting chooses the suffix. Both variants share a pivot, facing and UVs, and characters
share one skeleton and clip set, so gameplay code doesn't change between settings.

## What's here

| Game moment | Assets |
| :--- | :--- |
| Dogfight | `SM_Ship_Sloop`, `SM_Ship_Brigantine`, `SM_Ship_Galleon`, `SM_Ship_Propeller`, `SM_Prop_Cannonball`, `SM_Env_SkyIsland`, `SM_Env_Cloud` |
| Boarding | `SM_Prop_GrapplingHook` |
| Turn-based combat | `SM_Env_BoardingDeck`, `SK_PirateCaptain` (player), `SK_PirateDeckhand` (melee), `SK_PirateGunner` (ranged), `SK_PirateBrute` (tank), `SM_Prop_Cutlass`, `SM_Prop_Flintlock`, `SM_Prop_BoardingAxe`, `SM_Prop_RumBottle` (item) |
| Rewards | `SM_Prop_TreasureChest`, `SM_Prop_TreasureChestOpen`, `SM_Prop_GoldPile` |
| Deck dressing | `SM_Prop_Cannon`, `SM_Prop_Barrel`, `SM_Prop_Crate` |

A captured ship joins the fleet using the same ship model, so fleet screens need no extra art.

## Shattered Skies campaign

Everything the story mode needs, by where it first appears. Ships, characters and environments
embed the atlas that suits them (`default_faction` in the manifest), so they look right on import.

| Act | Ships | Characters | Environments and arenas | Props and items |
| :--- | :--- | :--- | :--- | :--- |
| **Prologue:** The Mutiny at Cloud-Break | `SM_Ship_SovereignGale`, `SM_Ship_SovereignGaleWrecked`, `SM_Ship_RustWing` | `SK_Vane`, `SK_CassianPirate`, `SK_Morrow`, `SK_ArmadaSoldier`, `SK_ArmadaOfficer` | `SM_Env_GaleQuarterdeck` (the Gale's deck, for the opening cutscenes), `SM_Env_SkyVault`, `SM_Env_UndertowDisc` | `SM_Prop_LiftCore`, `SM_Prop_HookSabre`, `SM_Prop_WreckChunk`, `SM_Prop_ArmadaMusket` |
| **Act I:** The Scrap-Winds | `SM_Ship_IronMaw`, `SM_Ship_ScrapRaider` | `SK_Rook`, `SK_Krag`, `SK_Brill` | `SM_Env_ScrapIsle`, `SM_Env_LiftMine`, `SM_Env_ToxicCloud`, `SM_Env_DebrisField`, `SM_Env_ScrapDeck` | `SM_Prop_HarpoonWinch`, `SM_Prop_Harpoon`, `SM_Prop_KragAnchor`, `SM_Prop_WinchControls`, `SM_Prop_AnchorHammer`, `SM_Prop_HandMortar`, `SM_Prop_FlakGun`, `SM_Prop_ScrapPile`, `SM_Prop_LiftStone`, `SM_Prop_WeaponModule` |
| **Act II:** The Smuggler's Jetstream | `SM_Ship_Mirage`, `SM_Ship_SmugglerRunner` | `SK_Lyra`, `SK_Sable`, `SK_Vex`, `SK_Vesper` | `SM_Env_SmugglersHaven`, `SM_Env_WindCurrent`, `SM_Env_UpdraftColumn`, `SM_Env_StormCloud` | `SM_Prop_CipherKey`, `SM_Prop_ChainShot`, `SM_Prop_FireShot`, `SM_Prop_Grapeshot`, `SM_Prop_SaboteurCharge`, `SM_Prop_AfterburnerTurbine`, `SM_Prop_TwinScimitar`, `SM_Prop_SableDagger`, `SM_Prop_DriftwoodLyre`, `SM_Prop_Spyglass` |
| **Act III:** The Cirrus Citadel | `SM_Ship_TartarusAloft`, `SM_Ship_Bastion`, `SM_Ship_AssaultGlider` | `SK_Thorne`, `SK_Malakor`, `SK_CassianAdmiral` | `SM_Env_GunDeck`, `SM_Env_CellBlock`, `SM_Env_WarRoom` | `SM_Prop_DeckMortar`, `SM_Prop_MortarShell`, `SM_Prop_CellDoor`, `SM_Prop_RepairKit`, `SM_Prop_BigWrench`, `SM_Prop_WardenFlail` |
| **Act IV:** Eye of the Maelstrom | `SM_Ship_ArmadaCorvette`, `SM_Ship_ArmadaFrigate`, `SM_Ship_SovereignGale` (Gilded atlas) | `SK_RoyalGuard` | `SM_Env_FreeHaven`, `SM_Env_StormWall`, `SM_Env_LightningBolt`, `SM_Env_DreadnoughtDeck`, `SM_Env_LiftCoreChamber` | `SM_Prop_SiegeCannon`, `SM_Prop_Missile`, `SM_Prop_ArmadaBanner`, `SM_Prop_ArmadaHalberd`, `SM_Prop_RoyalSabre`, `SM_Prop_DuelPistol` |
| **Between missions** | `SM_Ship_Lifeboat` (Scuttle & Plunder) | | `SM_Env_HangarHub` | `SM_Prop_AetherComm`, `SM_Prop_BountyBoard`, `SM_Prop_SkyChartTable`, `SM_Prop_HangingLantern`, `SM_Prop_RopeCoil` |

**One model, several story roles.** Swapping the atlas carries the plot, so these share a mesh:

| Model | Atlas | Plays as |
| :--- | :--- | :--- |
| `SM_Ship_RustWing` | `Gilded` | The Armada Scout you steal in the prologue, and the prologue's patrol ships |
| `SM_Ship_RustWing` | `Scrap` | *The Rust-Wing* after the theft |
| `SM_Ship_SovereignGale` | `Crimson`, then `Gilded`, then `Crimson` | Your flagship, Cassian's Sky-Killer, and the ship you reclaim |
| `SM_Ship_Mirage` | `Syndicate` and `Obsidian` | Vex's and Vesper's clippers |
| `SK_Thorne`, `SM_Prop_ShadowStaff` | `Sorcerer` | Dr. Thorne: purple clothes and green eyes need this atlas |
| `SK_Lyra`, `SM_Prop_DriftwoodLyre` | `Nomad` | Lyra: olive trousers and cloak patches, an emerald waistcoat and scarf, turquoise beads |
| `SK_Sable`, `SM_Prop_SableDagger` | `Shadow` | Sable: a dark grey coat and cowl, pewter buttons, a crimson scar and a blood-tipped blade |
| `SK_Morrow` | `Emerald` | Morrow: the emerald velvet coat |
| `SK_Brill` | `Bosun` | Bosun Brill: an indigo-and-cream striped shirt, indigo tattoos and a charcoal watch cap |
| `SM_Prop_DriftwoodLyre` | Any | Lyra's lyre, for `weapon.L` or `weapon.R`, held by its `grip` |
| `SK_Cinder` | Any | Rook's robot hound, on its own four-legged skeleton with `Idle`, `Walk` and `Fire` clips. A `muzzle` bone follows the barrel's tip |
| `SK_ArmadaSoldier` | `Gilded` or `Warden` | Armada crews, or Tartarus prison guards |
| `SK_PirateBrute`, `SK_PirateDeckhand`, `SK_PirateGunner` | `Scrap` or `Syndicate` | Krag's bruisers, Syndicate crews and bodyguards |

**Portraits.** `textures/production/T_Portrait_<Name>_<HQ|LQ>.png` are the dialogue and Aether-Comms
portraits, 512 px and 256 px. They are illustrated busts, not renders of the game models: each character has
their own sculpted face, painted features, pose and background motif, drawn by `tools/blender/portraits.py`
from the designs in `portrait_cast.py`. The busts are never exported, so they have no triangle budget. Crew
face right and rivals face left, so a comms exchange plays as a stand-off. Cassian faces left as a pirate
too, because he looks towards the crew he commands from the other side of the frame.

| Group | Portraits |
| :--- | :--- |
| Crew | `Vane`, `Rook`, `Lyra`, `Sable`, `Thorne`, `Morrow`, `Brill`, `Hask`, `Wren`, `Ivo` |
| Old crew and allies | `CassianPirate`, `Tobin`, `Mirren`, `Odessa`, `CaptainRia`, `Runner` |
| Rivals | `Krag`, `Gorse`, `Scrapper`, `Ashgrave`, `Thrace`, `Vex`, `Vesper`, `Malakor`, `CassianAdmiral` |
| Armada | `ArmadaOfficer`, `ArmadaSoldier`, `RoyalGuard` |

**Hub stations.** `SM_Env_HangarHub` has a `station_<name>` socket for each tappable crew member, and a
`ship_berth` socket where the ship you're flying is moored, scaled to fill 13 m.
Stand them there on the `Work` clip, with `SM_Prop_SkyChartTable` at `station_lyra_table`. Recolour the
hub with the current ship's atlas.

## Effects, skies and icons

Everything below is listed in `asset_manifest.json` under `effects`, `effect_presets`, `skies` and
`icons`, so the game can load it as data.

**Effects.** `textures/production/T_VFX_<Name>_<HQ|LQ>.png` are flipbook sheets, read left to right and
top to bottom, painted in the comic style of the portraits. Each entry gives the `columns`, `rows`,
`fps`, `loop`, `blend` and `anchor` (where the emitter sits in the frame, v up). LQ one-shots have fewer
frames than HQ, so take the frame count from the file's own entry.

| Flipbooks | Use |
| :--- | :--- |
| `Fire`, `Ember` | Burning decks, drifting sparks off them |
| `SmokeDark`, `SmokeLight`, `GasLeak`, `ToxicSmog` | Damage smoke, cannon smoke, leaking Lift-Gas, Scrap-Winds smog |
| `Explosion`, `FlakBurst`, `Shockwave` | Ship deaths and mortars, Rook's flak, the ring after a blast |
| `MuzzleFlash`, `Sparks`, `SlashArc` | Cannon shots, hits and blade clashes, melee swings |
| `AfterburnerPlume`, `LiftBurst`, `LightningFlash`, `Rain` | The Mirage's boost, Lift-Core sabotage and Overdrive, storm strikes, Act IV rain |

Import `premultiplied` sheets with premultiplied alpha, and draw `additive` ones additively. Colour runs
under every transparent pixel, so premultiplying can't leave dark fringes. Smoke is lit from the top left,
so keep its rotation small. `Rain` wraps: put it on a full-screen quad and scroll it down.

**Presets.** `effect_presets` are ready-made emitters tied to the models' sockets:

| Preset | Where | Moment |
| :--- | :--- | :--- |
| `HullFire`, `EmberDrift` | `fire_*`, falling back to `deck_centre` | Burning decks |
| `DamageSmoke` | `smoke_vent` | Ship below half hull |
| `CannonFire`, then `CannonSmoke` | `muzzle*`, `siege_muzzle`, `harpoon_muzzle` | Every shot |
| `EnvelopeHit` | `envelope_centre`, offset to the hit | Leaking Lift-Gas |
| `ShipExplosion`, then `ShipShockwave` | `lift_core`, falling back to `deck_centre` | Ship destroyed, with `SM_Prop_WreckChunk` debris |
| `LiftCoreSabotage` | `lift_core`, `charge_slot`, `glow` | Morrow's sabotage, Sable's charges, Overdrive |
| `AfterburnerBoost` | `afterburner_*`, `exhaust` | The Mirage's boost, kept attached |
| `MeleeSlash`, `BladeClash` | `trail_tip`, `centre_stage` | Melee |
| `LightningStrike` | `lightning_?`, `lightning_strike_*`, `strike` | Act IV hazards, with `SM_Env_LightningBolt` |

`sockets` are globs tried in order: the first that matches anything on an asset wins, and every match
gets an emitter. `hit_point` and `camera` are positions your code supplies. `follow_up` names the preset
to fire next. `max_particles` caps each emitter per quality, and an LQ value of 0 means skip it.
`depth_test: false` means draw it without depth testing: `lift_core` is inside the hull, which would
otherwise hide the blast. Every value is a starting point to tune by eye in the engine.

**Mesh effects.** `SM_VFX_Flame`, `SM_VFX_Fireball`, `SM_VFX_SmokePuff`, `SM_VFX_ShockRing` and
`SM_VFX_Plume` are opaque glowing models with no transparency, named by a preset's `fallback_mesh`. Use
them where a device can't afford overdraw: fake the animation by scaling and rotating about their
`origin` socket.

**Skies.** `T_Sky_<Name>_<HQ|LQ>.png` are equirectangular panoramas, 2048x1024 and 1024x512, for a
skybox or a sky-dome shader. They wrap horizontally, so set wrap to repeat on U and clamp on V, and use
bilinear filtering. The image's centre looks along +Z.

| Sky | Moment |
| :--- | :--- |
| `CloudBreak` | Prologue: the sunrise heist |
| `ScrapWinds` | Act I: hunted in the toxic junk-clouds |
| `Jetstream` | Act II: fast and bright, with streaks that all run one way |
| `CirrusCitadel` | Act III: cold moonlight around Tartarus Aloft |
| `Maelstrom` | Act IV: black storm towers and a lit eye overhead |
| `HangarDusk` | The hub, at sunset |
| `FreeDawn` | The ending, after the storm breaks |

Each sky's manifest entry also has `sun_dir`, `sun_color`, `sun_intensity`, `ambient_color`, `fog_color`
and fog distances. Use them with the sky so ships look like they belong in it.

**Icons.** `T_Icon_<Group>_<Name>_<HQ|LQ>.png` are transparent, 256 px and 128 px.

| Group | Icons |
| :--- | :--- |
| `Hud_` | Fire, grapple, board, defend, repair, altitude, throttle, hull, crew, comms, timer, map, pause, settings, close, back, locked |
| `Target_` | Sub-system targeting: engine, guns, sails, envelope, Lift-Core, plus a reticle |
| `Marker_` | Weak point, objective, waypoint, enemy fleet |
| `Hazard_` | Lightning, updraft, toxic cloud, wind current |
| `Story_` | Surrender, bounty, nemesis, loyalty |
| `Choice_` | The Davy Jones' Choice: `SeizeHelm`, `PressGang`, `ScuttlePlunder` |
| `Ability_` | `RookPrecision`, `LyraWinds`, `LyraCloudStealth`, `SableSabotage`, `ThorneOverdrive`, `ThornePatch`, `MorrowRadar` |
| `Ship_`, `Item_`, `Ammo_`, `Module_`, `Weapon_` | Rendered from the real models. `source_model` in the manifest names each one |

`Hud_` and `Target_` glyphs are near-white, so you can tint them for disabled or selected states.

## Quality settings

| | High | Low |
| :--- | :--- | :--- |
| Ship / character budget | 4,500 tris | 1,500 tris |
| Environment budget | 1,500 tris | 600 tris |
| Prop budget | 800 tris | 250 tris |
| Palette atlas | 512 px, surface detail | 128 px, flat colour |
| Dogfight: 6 Galleons with 4 propellers each | about 27k tris | about 7k tris |
| Combat: 8 Captains with weapons, plus deck | about 16k tris | about 6k tris |
| Finale: Sovereign Gale, 2 frigates, 2 corvettes, with propellers | about 14k tris | about 4k tris |
| Upper-deck brawl: 8 Royal Guards with halberds, plus deck | about 16k tris | about 5k tris |
| Hub: full crew, helm, chart table and bounty board, plus deck | about 11k tris | about 3.5k tris |

These are worst cases, and they cover the listed models only. On low quality, also cap on-screen ships and clouds,
use a simple unlit or vertex-lit shader, and turn off real-time shadows. Those are engine settings
and aren't part of these files.

## Integration

**Coordinates.** Metres, +Y up, model front is +Z (glTF convention). Every pivot is the bottom-centre
of the model's bounding box.

**Sockets.** `asset_manifest.json` lists attachment points per asset and quality, relative to the
pivot. Load them from there, because HQ and LQ positions can differ slightly.

| Socket | Use |
| :--- | :--- |
| `propeller_<L/R><n>` | Attach `SM_Ship_Propeller`. Place its `hub` socket here |
| `muzzle_<L/R><n>` | Cannon fire VFX and projectile spawn on a ship |
| `boarding_<L/R>`, `deck_centre` | Grappling line ends, boarding camera target |
| `smoke_vent`, `envelope_centre` | Damage smoke and balloon-hit VFX |
| `party_slot_<1-4>`, `enemy_slot_<1-4>` | Where combatants stand on `SM_Env_BoardingDeck` |
| `camera_look_at`, `centre_stage` | Combat camera target, melee meeting point |
| `grip`, `trail_tip`, `muzzle`, `rope` | Weapon and hook attachment, slash trail, gun flash, rope end |
| `lift_core`, `helm`, `weakpoint_<L/R>` | Sub-system targeting, the Seize the Helm camera, Act IV vulnerability markers |
| `fire_<n>`, `afterburner_<L/R>`, `exhaust`, `glow` | Deck-fire, engine-plume and glow VFX |
| `harpoon_muzzle`, `winch`, `ram_tip`, `siege_muzzle`, `missile_<...>` | Signature weapons on the boss ships |
| `glider_launch_<L/R>`, `seat_<n>`, `dock`, `brig`, `war_room` | Assault-glider launch and crew seats, mission set-pieces on Tartarus Aloft |
| `station_<name>`, `poster_<n>` | Hub crew positions, and pin spots on `SM_Prop_BountyBoard` |
| `lightning_<n>`, `lightning_strike_<n>`, `gravity_flip_<n>`, `bay_edge` | Hazards in storm clouds and arenas |

**Propellers.** The model's pivot is at its bottom, so spin it under a parent placed at the `hub`
socket, around that parent's Z axis. The blades' spin axis runs front to back.

**Held weapons.** Parent a weapon's root to a bone, with no rotation offset and a local position
equal to the negative of its `grip` socket. `held_item_bones` in the manifest lists which items go
on which bones: blades use `weapon.R` and `weapon.L`, guns use `gun.R` and `gun.L`, so the twin blades
and Cassian's sword-and-pistol work without extra rigging. Parent under the bone's node directly; if
your engine retargets or re-orients bones on import, keep the original axes for these four.

**Animation.** All characters share the clips below at 24 fps. Clip data is rotation on every bone
plus hips translation, so it plays in place and can be shared between every character.
`loop` values and lengths are in the manifest.

`Idle`, `Walk`, `AttackMelee`, `AttackRanged`, `Hit`, `Block`, `Death`, `Victory`, `Run`, `Grapple`
(the swing between ships), `Talk`, `Work` (hub tasks), `Surrender` (the Davy Jones' Choice), `Taunt`

**Factions.** `textures/production/T_Palette_<Crimson|Azure|Emerald|Obsidian|Gilded|Scrap|Syndicate|Warden|Sorcerer|Nomad|Shadow|Bosun>_<HQ|LQ>.png` differ only
in their accent colours. Swap the material's base colour texture to recolour any model, for example
to tell player crews apart. The same swap can share a single texture across all models: import the
atlas once, point every material at it, and discard the copy embedded in each `.glb`. Compress it as
ASTC, falling back to ETC2 on the oldest devices.

**Glow.** `T_Emissive_<HQ|LQ>.png` shares the palette's grid and is black except on lit swatches
(lanterns, lift-stone, embers, lightning, plasma, wind). It's the material's emissive texture, so these
surfaces read as light sources with no real-time lights. It never changes with faction. Keep it when
sharing textures across models, and add bloom on HQ if the budget allows.

**Hazard meshes.** `SM_Env_LightningBolt`, `SM_Env_WindCurrent` and `SM_Env_UpdraftColumn` are fully
emissive. Flash the bolt for a few frames only, and spin the updraft around its Z axis. Twelve copies of
`SM_Env_StormWall`, rotated 30 degrees apart about its `ring_centre` socket, form the Maelstrom's eye.

## Regenerating

The source of truth is `tools/blender/`, not the exported files. Change a generator there, then send
the affected assets through the `mobile-asset-optimizer` subagent. Never export straight to production.
Budgets live in `BUDGET_TIERS` in the gate script.

    python3 tools/blender/palette.py          # rebuild palette and emissive atlases
    python3 tools/validate_production.py      # check every .glb and image against budgets and the manifest

Inside Blender, `catalog.write_manifest()` rebuilds `asset_manifest.json`, `portraits.render_all()` re-renders the
portraits in a temporary scene (pass a list of names for just those), and `fx_flipbooks.generate_all()`, `fx_skies.generate_all()` and
`fx_icons.generate_all(scratch_dir)` rebuild the effects, skies and icons. Their contents are listed in
`fx_catalog.py`. Art direction for each campaign asset is in
the docstrings of `story_ships.py`, `story_characters.py`, `story_props.py` and `story_env.py`.
