> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-03.

# D&D 3.5e Creature Tokens

> Status: no code loads these yet (loader reverted in 281dab7); kept by owner decision for future use (REPO-015). Many filenames do not match their artwork; see Known issues before using any token.

## Overview

This folder holds **278 circular creature token images** extracted from the Monster Manual (Premium Edition) PDF, covering **199 creatures**: 199 primary tokens (`{name}.png`) and 79 alternates (`{name}_N.png`). The folder is about 29 MB. Because it is under `StreamingAssets`, it ships in every build even though nothing reads it.

## Token specifications

- **Size**: 256x256 pixels
- **Format**: PNG with alpha transparency (RGBA)
- **Shape**: circular, with a thin dark border ring
- **Cropping**: upper-center square crop aimed at the face or head
- **Source**: Monster Manual (Premium Edition), pages 8-288

## File naming convention

- Creature names are lowercased, with spaces replaced by underscores.
- Parenthetical notes are removed: `"Babau (demon)"` -> `babau.png`.
- Commas become underscores: `"Ape, dire"` -> `ape_dire.png`.
- Alternative images get a numeric suffix: `achaierai.png`, `achaierai_2.png`, `achaierai_3.png`.

## Manifest file

`creature_manifest.json` has this shape:

```json
{
  "version": "1.0",
  "description": "...",
  "token_size": 256,
  "total_tokens": 278,
  "unique_creatures": 199,
  "creatures": {
    "<Monster Manual name>": {
      "file": "<primary>.png",
      "page": 133,
      "source_size": "WxH",
      "alternatives": ["<name>_2.png"]
    }
  }
}
```

Every file the manifest references exists in this folder. The manifest is the authoritative list of the 199 creature names.

## Code integration status

No code in `Assets/Scripts` reads this folder. A loader (`Assets/Scripts/UI/CreatureTokenLoader.cs`) and an `NPCDefinition.TokenPath` field were added in d17fd90 (2026-05-27) and removed the next day in 281dab7 ("Revert token integration code - keep assets for future manual curation"). The owner decided on 2026-10-03 to keep the assets for future use. The reverted loader can be read with `git show d17fd90:Assets/Scripts/UI/CreatureTokenLoader.cs`; it read files with `File.ReadAllBytes`, which does not work for `StreamingAssets` on Android or WebGL.

In-game tokens currently come from `IconLoader.GetToken` (`Assets/Scripts/UI/Common/IconLoader.cs`), which loads 11 class and monster tokens from `Assets/Resources/Icons/Tokens/`. NPC spawn picks one with `IconLoader.DetermineMonsterType` and otherwise uses `Resources/Sprites/npc_enemy_alive` tinted with `NPCDefinition.SpriteColor` (`Assets/Scripts/_Core/GameManager.NPCSetup.cs`).

## Creature coverage

See `creature_manifest.json` for the full list. Examples that are present:

- **Undead**: Skeleton, Zombie, Mummy, Lich, Vampire, Wraith, Dread wraith, Ghost
- **Humanoids and giants**: Goblin, Hobgoblin, Bugbear, Gnoll, Ogre, Ogre mage, Half-orc, Troll; Stone, Frost, Fire and Cloud giants
- **Dragons**: chromatic and metallic true dragons, Dragon turtle, Pseudodragon
- **Aberrations**: Aboleth, Gauth (beholder), Mind flayer sorcerer
- **Outsiders**: several devils and demons, archons, Astral deva; Air and Earth elementals
- **Beasts and magical beasts**: Ape, dire; Rat, dire; Chimera, Owlbear, Winter wolf
- **Fey**: Dryad, Satyr, Pixie

Not present: Kobold, Orc (only Half-orc), Beholder (only Gauth), Mind Flayer (only Mind flayer sorcerer), Carrion Crawler, Griffon, Manticore, Hill and Storm giants, Ettin, Nymph, Nixie and dire animals other than the ape and rat.

## Known issues

- **Filenames do not reliably match the art.** Some primary tokens show the creature from the preceding Monster Manual entry: `skeleton.png` shows a shocker lizard (manifest pages 225 and 224) and `troll.png` shows a troglodyte (pages 247 and 246). Some tokens are blank crops of page background rather than creature art (`goblin.png` and `aboleth.png`; also reported for `red_dragon.png`). Look at every token before mapping it to a creature.
- Alternates (`{name}_2.png` and up) vary in quality.

## Notes for future integration

- 79 of the 289 unique NPC `Id` strings under `Assets/Scripts/Character/Creatures/` exactly match a token basename; the rest need an explicit mapping.
- Load files with `UnityWebRequest` rather than `File.ReadAllBytes` so the loader also works on Android and WebGL.
- Choose a pixels-per-unit value deliberately: `GameManager.LoadSprite` uses 64 and `IconLoader` uses 100.

## Extraction process

The tokens were extracted with PyMuPDF (fitz) and Pillow. The extraction script is not committed to the repo, so these steps cannot be re-run from it:

1. Parse the PDF table of contents to map pages to creature names.
2. Extract the embedded images from each page (pages 8-288).
3. Skip images smaller than 80 px or 15,000 px² to drop decorations.
4. Crop: upper 65% for tall images, then a center-weighted square crop.
5. Resize to 256x256 with LANCZOS resampling.
6. Apply a circular mask with a thin dark border ring.
7. Save as PNG with alpha transparency.
