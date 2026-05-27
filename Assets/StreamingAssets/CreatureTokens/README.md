# D&D 3.5e Creature Tokens

## Overview
This directory contains **278 circular creature token images** extracted from the Monster Manual (Premium Edition) PDF, covering **199 unique creatures** from A to Z.

## Token Specifications
- **Size**: 256×256 pixels
- **Format**: PNG with alpha transparency
- **Shape**: Circular with subtle dark border ring
- **Cropping**: Smart upper-center crop focusing on face/head area
- **Source**: Monster Manual I (Premium Edition), pages 8–289

## File Naming Convention
- Creature names are sanitized to lowercase with underscores
- Parenthetical notes removed: `"Babau (demon)"` → `babau.png`
- Commas become underscores: `"Bear, dire"` → `bear_dire.png`
- Alternative images suffixed: `goblin.png`, `goblin_2.png`, `goblin_3.png`

## Manifest File
`creature_manifest.json` contains a complete mapping of:
- Creature name → primary token filename
- Source page in the Monster Manual
- Original image dimensions
- Alternative token filenames (for creatures with multiple illustrations)

## Integration with Unity
Use the `CreatureTokenLoader` utility class (`Assets/Scripts/UI/CreatureTokenLoader.cs`):

```csharp
// By creature name
Sprite token = CreatureTokenLoader.GetToken("Goblin");

// By NPCDefinition (checks TokenPath first, then name)
NPCDefinition npc = NPCDatabase.Get("goblin");
Sprite token = CreatureTokenLoader.GetToken(npc);

// Check if token exists
bool hasToken = CreatureTokenLoader.HasToken("Mind Flayer");
```

## NPCDefinition.TokenPath
Each `NPCDefinition` now has an optional `TokenPath` field:
```csharp
new NPCDefinition {
    Id = "goblin",
    Name = "Goblin",
    TokenPath = "goblin.png",  // Optional explicit path
    // ...
};
```

If `TokenPath` is not set, `CreatureTokenLoader` automatically looks up `{sanitized_name}.png`.

## Creature Coverage
Tokens include iconic D&D creatures such as:
- **Undead**: Skeleton, Zombie, Mummy, Lich, Vampire, Wraith, Ghost
- **Humanoids**: Goblin, Orc, Gnoll, Bugbear, Hobgoblin, Kobold, Ogre
- **Dragons**: True Dragons (Chromatic & Metallic), Dragon Turtle, Pseudodragon
- **Aberrations**: Beholder, Mind Flayer, Aboleth, Carrion Crawler
- **Outsiders**: Devils, Demons, Angels, Archons, Elementals
- **Beasts**: Dire Animals, Griffon, Chimera, Manticore, Owlbear
- **Giants**: Hill, Stone, Frost, Fire, Cloud, Storm Giant, Ettin, Troll
- **Fey**: Dryad, Nymph, Satyr, Pixie, Nixie
- And many more...

## Extraction Process
Tokens were extracted using PyMuPDF (fitz) and Pillow:
1. Table of Contents parsed to build page→creature name mapping (413 entries)
2. Embedded images extracted from each PDF page (pages 8–289)
3. Images filtered by minimum size (80px, 15000px² area) to skip decorative elements
4. Smart cropping applied (upper 65% for tall images, center-weighted square crop)
5. Resized to 256×256 with LANCZOS resampling
6. Circular mask applied with subtle dark border ring
7. Saved as PNG with full alpha transparency

Note: Some extracted images may contain stat block backgrounds or page decorations
rather than creature artwork. The primary token (`{name}.png`) is typically the largest
image from that creature's page, while alternatives (`{name}_2.png`) may vary in quality.
