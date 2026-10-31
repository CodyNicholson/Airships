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

## Quality settings

| | High | Low |
| :--- | :--- | :--- |
| Ship / character budget | 4,500 tris | 1,500 tris |
| Environment budget | 1,500 tris | 600 tris |
| Prop budget | 800 tris | 250 tris |
| Palette atlas | 512 px, surface detail | 128 px, flat colour |
| Dogfight: 6 Galleons with 4 propellers each | about 27k tris | about 7k tris |
| Combat: 8 Captains with weapons, plus deck | about 16k tris | about 6k tris |

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

**Propellers.** The model's pivot is at its bottom, so spin it under a parent placed at the `hub`
socket, around that parent's Z axis. The blades' spin axis runs front to back.

**Held weapons.** Parent a weapon's root to a bone, with no rotation offset and a local position
equal to the negative of its `grip` socket. Use `weapon.R` for the cutlass and axe, `gun.R` for
the flintlock. Parent under the bone's node directly; if your engine retargets or re-orients bones
on import, keep the original axes for these two.

**Animation.** All characters share the clips below at 24 fps. Clip data is rotation on every bone
plus hips translation, so it plays in place and can be shared between all four characters.
`loop` values and lengths are in the manifest.

`Idle`, `Walk`, `AttackMelee`, `AttackRanged`, `Hit`, `Block`, `Death`, `Victory`

**Factions.** `textures/production/T_Palette_<Crimson|Azure|Emerald|Obsidian>_<HQ|LQ>.png` differ only
in their accent colours. Swap the material's base colour texture to recolour any model, for example
to tell player crews apart. The same swap can share a single texture across all models: import the
atlas once, point every material at it, and discard the copy embedded in each `.glb`. Compress it as
ASTC, falling back to ETC2 on the oldest devices.

## Regenerating

The source of truth is `tools/blender/`, not the exported files. Change a generator there, then send
the affected assets through the `mobile-asset-optimizer` subagent. Never export straight to production.
Budgets live in `BUDGET_TIERS` in the gate script.

    python3 tools/blender/palette.py          # rebuild palette atlases
    python3 tools/validate_production.py      # check every .glb against budgets and the manifest

Inside Blender, `catalog.write_manifest()` rebuilds `asset_manifest.json`.
