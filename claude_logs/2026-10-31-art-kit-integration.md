# Art Kit Integration

- **Date:** 2026-10-31
- **Participants:** Cody Nicholson, Claude Code (Opus 5.5)
- **Status at end of this entry:** kit integrated and running in the Editor, with an Android build made. Not yet played on a phone, and not committed.

## Decisions

Cody decided:

- The project uses the assets in `Airship_Blender_Assets_Project` together with the existing assets.
- Claude chooses which is better wherever only one can be used.

Claude's choices, all open to change:

| Asset | Used | Why |
|---|---|---|
| Ships and propellers | Kit | Balloon envelopes read as airships, and there are three distinct silhouettes |
| Character bodies and animation | Kit | Rigged with eight shared clips, where the old models were moved by code |
| Character accessories | Existing, restyled | The kit has no Stormcaller or Surgeon, so older hats, staff and bomb fill the gap |
| Battle deck | Kit | Includes party, enemy and camera sockets, and shows the ship's edges against the sky |
| Sky islands | Kit | More surface detail, and a low-detail version for Low mode |
| Clouds | Existing | One mesh holds 24,480 triangles at High. 74 kit clouds would be about 77,000 triangles and 74 draw calls |
| Sky, shot visuals and effects | Existing | Shots glow and stay readable at range, where a dark kit cannonball wouldn't |
| Palettes | Kit | One shared atlas set, with faction colors. Player crew and ship use Crimson, enemies use Obsidian, Tobin uses Azure, Odessa uses Emerald |

Character mapping:

| Game class | Kit body | Additions |
|---|---|---|
| Buccaneer (Captain Vale) | `SK_PirateCaptain` | Cutlass |
| Gunner (Mirren) | `SK_PirateGunner` | Flintlock |
| Stormcaller (Tobin) | `SK_PirateDeckhand` | Older wizard hat, new staff |
| Surgeon (Odessa) | `SK_PirateDeckhand` | Older medic's cap, rum bottle as a tonic |
| Brigand | `SK_PirateDeckhand` | Cutlass |
| Bombardier | `SK_PirateGunner` | New bomb |
| Pirate Captain | `SK_PirateBrute` | Boarding axe, older tricorn |

Ship mapping: the player flies a Sloop. Enemies fly a Brigantine, and a Galleon from tier 6. Hull choice is cosmetic for now and doesn't change stats.

## What changed

- **Package:** added glTFast 6.20.0, needed to import `.glb` files.
- **Kit files:** copied to `Assets/Game/Art/Kit`, with atlases in `Assets/Game/Resources/Art/Textures`. The kit's own project was not changed.
- **Import tool:** `Airships/Import Art Kit` (`ArtKitImport.cs`) bakes prefabs into `Assets/Game/Resources/Art`, with sockets as child objects. Re-run it after copying in updated kit files.
- **Shader:** `StylizedLit` now also samples a palette texture.
- **Animation:** `KitCharacter.cs` plays clips and drives their time itself, because the importer flags every clip as looping.
- **Battle:** attacks, hits, guarding, death and victory use the clips, with damage landing on the clip's strike frame. Fighters stand in a diagonal line so nobody is hidden. A chest and gold appear on victory.
- **Removed:** the old code-built ships, bodies, islands and deck.

## Verified

- The kit's own validator passes.
- Autoplay completed full encounters in High and Low modes with no console errors or warnings.
- 21 of 21 EditMode tests pass.
- The Android APK builds with 0 errors. Its 975 warnings were not reviewed one by one, and those seen came from unrelated packages, mostly shaders in `com.unity.ai.inference`.
- The APK is 51 MB, up from 44 MB. The build report shows no textures or materials shipping from the `.glb` files.

| Screen | High triangles | High draws | Low triangles | Low draws |
|---|---|---|---|---|
| Dogfight | about 38,600 | 43 | about 15,800 | 19 |
| Boarding | about 51,300 | 78 | about 18,800 | 40 |

These are Editor statistics, not device measurements.

## Not verified

- Nothing has run on a phone, so frame rate and how the new art reads on a small screen are unknown.
- The player battle menus were not clicked in this pass, because autoplay skips them.
- Galleons have not appeared in actual play, because autoplay didn't reach tier 6. They were only seen in a comparison render.
- A lost boarding battle wasn't seen with the new animation.
- Changing graphics mode updates islands immediately, but other models only on the next screen.

## Known issues and leftovers

- **Imported but unused for now:** kit clouds, cannonball, deck cannon, barrel, crate, grappling hook and closed chest. They still ship in the build, at small cost. The grappling hook suits the planned boarding transition.
- **Crew overlap:** the back of the party line still overlaps a little from the battle camera.
- **Results screen:** the "LEVEL UP!" banner crosses the summary panel. This predates the kit.
- **Build size:** `com.unity.ai.inference` adds about 29 MB of assets to the build, and the game doesn't use it. Removing it is the largest available size saving, but check first that the Editor's AI tooling doesn't need it.
- **Git:** game code isn't committed, so the removed model code has no git history.
