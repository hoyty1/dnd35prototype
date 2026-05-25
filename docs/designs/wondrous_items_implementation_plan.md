# D&D 3.5e Wondrous Items — Comprehensive Implementation Plan

> **Source:** Dungeon Master's Guide 3.5e, Pages 246–265  
> **Status:** Pre-Implementation Planning  
> **Previous Sprint:** Ring Sprint 3 (91% complete), Rod Implementation Plan  
> **Date:** May 2026

---

## WONDROUS ITEMS OVERVIEW

### What Are Wondrous Items?
Wondrous items are the largest and most diverse category of magic items in D&D 3.5e. They encompass everything that doesn't fall neatly into weapons, armor, shields, rings, rods, staves, scrolls, wands, or potions. They range from simple stat-boosting worn items to complex multi-ability artifacts with unique activation mechanics.

### Total Item Count

| Category | Price Range | Item Count (incl. variants) |
|----------|------------|-----------------------------|
| **Minor** | ≤ 15,000 gp | ~80 items |
| **Medium** | 15,001–60,000 gp | ~50 items |
| **Combined Minor+Medium** | ≤ 60,000 gp | **~130 items** |
| Major (out of scope) | > 60,000 gp | ~40 items |

> **Note:** Many items have multiple variants (e.g., Cloak of Resistance +1 through +5, Bag of Holding Types I–IV, Ioun Stones with 12+ subtypes). Each variant counts separately in the totals above.

### Equipment Slots Used

| Slot | # of Base Items | Examples |
|------|----------------|----------|
| **Feet (Boots)** | 8 | Boots of Speed, Slippers of Spider Climbing |
| **Shoulders (Cloaks)** | 10+ variants | Cloak of Resistance, Cloak of Displacement |
| **Hands (Gloves/Gauntlets)** | 5 base (8 w/ variants) | Gauntlets of Ogre Power, Gloves of Dexterity |
| **Head (Headgear)** | 12+ base | Hat of Disguise, Helm of Telepathy |
| **Face (Eyes/Goggles)** | 5 | Eyes of the Eagle, Goggles of Night |
| **Throat (Neck)** | 12+ base (20+ variants) | Amulet of Natural Armor, Necklace of Fireballs |
| **Waist (Belts)** | 2 base (4 variants) | Belt of Giant Strength, Monk's Belt |
| **Arms/Wrists (Bracers)** | 3 base (10+ variants) | Bracers of Armor, Bracers of Archery |
| **Torso/Body (Robes)** | 8 | Robe of the Archmagi, Robe of Eyes |
| **Slotless** | 60+ | Bag of Holding, Ioun Stones, Figurines |

### Item Categories by Function

| Category | Count | Description |
|----------|-------|-------------|
| Ability Score Enhancement | 19 | +2/+4/+6 to ability scores |
| Armor Class Enhancement | 13 | Natural armor, deflection, miss chance |
| Saving Throw Enhancement | 6 | Resistance bonuses, immunities |
| Movement Enhancement | 10 | Speed, flight, teleportation, special movement |
| Stealth & Detection | 12 | Skill bonuses, vision modes, concealment |
| Skill Enhancement | 15 | Competence bonuses, special abilities |
| Storage & Utility | 15 | Extradimensional containers, tools |
| Combat Items | 12 | Offensive abilities, projectiles |
| Summoning & Transformation | 10 | Creature summoning, shapechanging |
| Spell Recall | 9 | Pearl of Power (1st–9th level) |
| Ioun Stones | 16 | Slotless orbiting stones with varied effects |
| Protection Items | 8 | Damage/spell resistance, immunities |
| Divination & Knowledge | 8 | True seeing, telepathy, scrying |
| Special Robes & Vestments | 7 | Multi-ability worn items |

### Wondrous Item Physical Properties (General)
- **Weight:** Varies widely (negligible for dust/gems, 1–5 lbs for worn items, up to 100 lbs for carpets/boats)
- **Material:** Varies (cloth, leather, metal, crystal, etc.)
- **Activation:** Most require standard action; some are continuous/passive
- **Usability:** Most usable by any class unless specified (some robes require specific alignment/class)
- **Charges:** Most are permanent (exceptions: Necklace of Fireballs, Robe of Bones, Robe of Useful Items — single-use components)
- **Caster Level:** Varies per item; determines DC for dispelling
- **Aura:** Varies per item; determines school of magic

### Random Generation Tables (DMG Table 7–18)

**Minor Wondrous Items (d% roll):**

| d% | Item | Price |
|----|------|-------|
| 01 | Feather token (anchor) | 50 gp |
| 02 | Universal solvent | 50 gp |
| 03 | Elixir of love | 150 gp |
| 04 | Unguent of timelessness | 150 gp |
| 05 | Feather token (fan) | 200 gp |
| 06 | Dust of tracelessness | 250 gp |
| 07 | Elixir of hiding | 250 gp |
| 08 | Elixir of tumbling | 250 gp |
| 09 | Elixir of swimming | 250 gp |
| 10 | Elixir of vision | 250 gp |
| 11 | Silversheen | 250 gp |
| 12 | Feather token (bird) | 300 gp |
| 13 | Feather token (tree) | 400 gp |
| 14 | Feather token (swan boat) | 450 gp |
| 15 | Elixir of truth | 500 gp |
| 16 | Feather token (whip) | 500 gp |
| 17 | Dust of dryness | 850 gp |
| 18 | Bag of tricks (gray) | 900 gp |
| 19 | Hand of the mage | 900 gp |
| 20 | Bracers of armor +1 | 1,000 gp |
| 21 | Cloak of resistance +1 | 1,000 gp |
| 22 | Pearl of power (1st) | 1,000 gp |
| 23 | Phylactery of faithfulness | 1,000 gp |
| 24 | Salve of slipperiness | 1,000 gp |
| 25 | Elixir of fire breath | 1,100 gp |
| 26 | Pipes of the sewers | 1,150 gp |
| 27 | Dust of illusion | 1,200 gp |
| 28 | Goggles of minute seeing | 1,250 gp |
| 29 | Brooch of shielding | 1,500 gp |
| 30 | Necklace of fireballs (type I) | 1,650 gp |
| 31 | Dust of appearance | 1,800 gp |
| 32 | Hat of disguise | 1,800 gp |
| 33 | Pipes of sounding | 1,800 gp |
| 34 | Quiver of Ehlonna | 1,800 gp |
| 35 | Amulet of natural armor +1 | 2,000 gp |
| 36 | Handy haversack | 2,000 gp |
| 37 | Horn of fog | 2,000 gp |
| 38 | Elemental gem | 2,250 gp |
| 39 | Robe of bones | 2,400 gp |
| 40 | Sovereign glue | 2,400 gp |
| 41 | Bag of holding (type I) | 2,500 gp |
| 42 | Boots of elvenkind | 2,500 gp |
| 43 | Boots of the winterlands | 2,500 gp |
| 44 | Candle of truth | 2,500 gp |
| 45 | Cloak of elvenkind | 2,500 gp |
| 46 | Eyes of the eagle | 2,500 gp |
| 47 | Goggles of night | 2,500 gp |
| 48 | Scarab, golembane | 2,500 gp |
| 49 | Necklace of fireballs (type II) | 2,700 gp |
| 50 | Stone of alarm | 2,700 gp |
| 51 | Bag of tricks (rust) | 3,000 gp |
| 52 | Bead of force | 3,000 gp |
| 53 | Chime of opening | 3,000 gp |
| 54 | Horseshoes of speed | 3,000 gp |
| 55 | Rope of climbing | 3,000 gp |
| 56 | Bag of holding (type II) | 5,000 gp |
| 57 | Dust of disappearance | 3,500 gp |
| 58 | Lens of detection | 3,500 gp |
| 59 | Figurine of wondrous power (silver raven) | 3,800 gp |
| 60 | Amulet of health +2 | 4,000 gp |
| 61 | Bracers of armor +2 | 4,000 gp |
| 62 | Cloak of charisma +2 | 4,000 gp |
| 63 | Cloak of resistance +2 | 4,000 gp |
| 64 | Gauntlets of ogre power | 4,000 gp |
| 65 | Gloves of arrow snaring | 4,000 gp |
| 66 | Gloves of dexterity +2 | 4,000 gp |
| 67 | Headband of intellect +2 | 4,000 gp |
| 68 | Ioun stone (clear spindle) | 4,000 gp |
| 69 | Restorative ointment | 4,000 gp |
| 70 | Periapt of wisdom +2 | 4,000 gp |
| 71 | Necklace of fireballs (type III) | 4,350 gp |
| 72 | Circlet of persuasion | 4,500 gp |
| 73 | Slippers of spider climbing | 4,800 gp |
| 74 | Incense of meditation | 4,900 gp |
| 75 | Bag of holding (type III) | 7,400 gp |
| 76 | Amulet of natural armor +2 | 8,000 gp |
| 77 | Bracers of armor +3 | 9,000 gp |
| 78 | Cloak of resistance +3 | 9,000 gp |
| 79 | Decanter of endless water | 9,000 gp |
| 80 | Necklace of fireballs (type IV) | 5,400 gp |
| 81 | Pearl of power (2nd) | 4,000 gp |
| 82 | Stone salve | 4,000 gp |
| 83 | Necklace of fireballs (type V) | 5,850 gp |
| 84 | Boots of striding and springing | 5,500 gp |
| 85 | Wind fan | 5,500 gp |
| 86 | Amulet of mighty fists +1 | 6,000 gp |
| 87 | Horseshoes of a zephyr | 6,000 gp |
| 88 | Pipes of haunting | 6,500 gp |
| 89 | Gloves of swimming and climbing | 6,250 gp |
| 90 | Bag of holding (type IV) | 10,000 gp |
| 91 | Bag of tricks (tan) | 6,900 gp |
| 92 | Necklace of fireballs (type VI) | 8,100 gp |
| 93 | Boots of levitation | 7,500 gp |
| 94 | Helm of comprehend languages and read magic | 5,200 gp |
| 95 | Vest of escape | 5,200 gp |
| 96 | Eversmoking bottle | 5,400 gp |
| 97 | Sustaining spoon | 5,400 gp |
| 98 | Necklace of fireballs (type VII) | 8,700 gp |
| 99 | Bracers of archery (lesser) | 5,000 gp |
| 100 | Periapt of health | 7,500 gp |

**Medium Wondrous Items (d% roll) — selected highlights:**

| d% Range | Item | Price |
|-----------|------|-------|
| 01 | Boots of speed | 12,000 gp |
| 02 | Cloak of displacement (minor) | 24,000 gp |
| 03–04 | Amulet of natural armor +3 | 18,000 gp |
| 05–06 | Belt of giant strength +4 | 16,000 gp |
| 07–08 | Bracers of armor +4 | 16,000 gp |
| 09–10 | Cloak of charisma +4 | 16,000 gp |
| 11–12 | Cloak of resistance +4 | 16,000 gp |
| 13–14 | Gloves of dexterity +4 | 16,000 gp |
| 15–16 | Headband of intellect +4 | 16,000 gp |
| 17–18 | Pearl of power (4th) | 16,000 gp |
| 19–20 | Periapt of wisdom +4 | 16,000 gp |
| 21–22 | Amulet of health +4 | 16,000 gp |
| 23 | Winged boots | 16,000 gp |
| 24 | Bracers of armor +5 | 25,000 gp |
| 25 | Cloak of resistance +5 | 25,000 gp |
| 26 | Eyes of charming | 56,000 gp |
| 27 | Figurine of wondrous power (bronze griffon) | 10,000 gp |
| 28 | Figurine of wondrous power (ebony fly) | 10,000 gp |
| 29 | Figurine of wondrous power (golden lions) | 16,500 gp |
| 30 | Gem of brightness | 13,000 gp |
| 31 | Helm of telepathy | 27,000 gp |
| 32 | Horn of blasting | 20,000 gp |
| 33 | Horn of valhalla (silver) | 50,000 gp |
| 34 | Ioun stone (dusty rose prism) | 5,000 gp |
| 35–36 | Amulet of natural armor +4 | 32,000 gp |
| 37 | Bracers of armor +6 | 36,000 gp |
| 38 | Belt of giant strength +6 | 36,000 gp |
| 39 | Cloak of charisma +6 | 36,000 gp |
| 40 | Gloves of dexterity +6 | 36,000 gp |
| 41 | Headband of intellect +6 | 36,000 gp |
| 42 | Periapt of wisdom +6 | 36,000 gp |
| 43 | Amulet of health +6 | 36,000 gp |
| 44 | Amulet of natural armor +5 | 50,000 gp |
| 45 | Bracers of armor +7 | 49,000 gp |
| 46 | Cloak of arachnida | 14,000 gp |
| 47 | Robe of blending | 8,400 gp |
| 48 | Robe of eyes | 120,000 gp |
| 49 | Rope of entanglement | 21,000 gp |
| 50 | Cube of frost resistance | 27,000 gp |
| 51 | Helm of underwater action | 24,000 gp |
| 52 | Pearl of power (3rd) | 9,000 gp |
| 53 | Figurine of wondrous power (marble elephant) | 17,000 gp |
| 54 | Figurine of wondrous power (ivory goats) | 21,000 gp |
| 55 | Phylactery of undead turning | 11,000 gp |
| 56 | Gauntlet of rust | 11,500 gp |
| 57 | Bottle of air | 7,250 gp |
| 58 | Boots of teleportation | 49,000 gp |
| 59 | Bracers of armor +8 | 64,000 gp |
| 60 | Carpet of flying (5×5) | 20,000 gp |
| 61 | Carpet of flying (5×10) | 35,000 gp |
| 62 | Carpet of flying (10×10) | 60,000 gp |
| 63 | Cloak of the bat | 26,000 gp |
| 64 | Cube of force | 62,000 gp |
| 65 | Folding boat | 7,200 gp |
| 66 | Gem of seeing | 75,000 gp |
| 67 | Iron bands of binding | 26,000 gp |
| 68 | Lantern of revealing | 30,000 gp |
| 69 | Monk's belt | 13,000 gp |
| 70 | Pearl of power (5th–9th) | 25,000–81,000 gp |
| 71 | Portable hole | 20,000 gp |
| 72 | Robe of scintillating colors | 27,000 gp |
| 73 | Robe of stars | 58,000 gp |
| 74 | Robe of useful items | 7,000 gp |
| 75 | Wings of flying | 54,000 gp |
| 76–100 | Roll on Major table | — |

---

## COMPLETE CATALOG BY SLOT

### **BOOTS & FOOTWEAR** (Slot: Feet)

| # | Item | Price | Category | CL | Aura | Weight |
|---|------|-------|----------|----|----- |--------|
| 1 | Boots of Elvenkind | 2,500 gp | Minor | 5th | Faint transmutation | 1 lb |
| 2 | Boots of Levitation | 7,500 gp | Minor | 3rd | Faint transmutation | 1 lb |
| 3 | Boots of Speed | 12,000 gp | Medium | 10th | Moderate transmutation | 1 lb |
| 4 | Boots of Striding and Springing | 5,500 gp | Minor | 3rd | Faint transmutation | 1 lb |
| 5 | Boots of Teleportation | 49,000 gp | Medium | 9th | Moderate conjuration | 3 lbs |
| 6 | Boots of the Winterlands | 2,500 gp | Minor | 5th | Faint abjuration/transmutation | 1 lb |
| 7 | Slippers of Spider Climbing | 4,800 gp | Minor | 4th | Faint transmutation | 0.5 lb |
| 8 | Winged Boots | 16,000 gp | Medium | 8th | Moderate transmutation | 1 lb |

**Detailed Abilities:**

- **Boots of Elvenkind:** +5 competence bonus on Move Silently checks. Continuous.
- **Boots of Levitation:** *Levitate* at will on the wearer (self only). Command word activation.
- **Boots of Speed:** *Haste* effect, 10 rounds/day (not necessarily consecutive). Free action to activate.
- **Boots of Striding and Springing:** +10 ft enhancement bonus to base land speed, +5 competence bonus on Jump checks. Continuous.
- **Boots of Teleportation:** *Teleport* 3/day (self + 50 lbs of objects). Command word.
- **Boots of the Winterlands:** Endure elements (cold) continuously, +10 ft enhancement bonus in snow, walk on snow without leaving tracks, walk on ice without penalty.
- **Slippers of Spider Climbing:** *Spider climb* at will. Continuous (move at half speed on vertical/inverted surfaces, 15 ft).
- **Winged Boots:** *Fly* 3/day for up to 5 minutes each. Speed 60 ft (good maneuverability). Command word.

---

### **CLOAKS & CAPES** (Slot: Shoulders)

| # | Item | Price | Category | CL | Aura |
|---|------|-------|----------|----|----- |
| 1 | Cloak of Arachnida | 14,000 gp | Medium | 6th | Faint conjuration/illusion |
| 2 | Cloak of the Bat | 26,000 gp | Medium | 7th | Moderate transmutation |
| 3 | Cloak of Charisma +2 | 4,000 gp | Minor | 8th | Moderate transmutation |
| 4 | Cloak of Charisma +4 | 16,000 gp | Medium | 8th | Moderate transmutation |
| 5 | Cloak of Charisma +6 | 36,000 gp | Medium | 8th | Moderate transmutation |
| 6 | Cloak of Displacement, Minor | 24,000 gp | Medium | 3rd | Faint illusion |
| 7 | Cloak of Displacement, Major | 50,000 gp | Medium | 7th | Moderate illusion |
| 8 | Cloak of Elvenkind | 2,500 gp | Minor | 3rd | Faint illusion |
| 9 | Cloak of Etherealness | 55,000 gp | Medium | 15th | Strong transmutation |
| 10 | Cloak of Resistance +1 | 1,000 gp | Minor | 5th | Faint abjuration |
| 11 | Cloak of Resistance +2 | 4,000 gp | Minor | 6th | Moderate abjuration |
| 12 | Cloak of Resistance +3 | 9,000 gp | Minor | 9th | Moderate abjuration |
| 13 | Cloak of Resistance +4 | 16,000 gp | Medium | 12th | Strong abjuration |
| 14 | Cloak of Resistance +5 | 25,000 gp | Medium | 15th | Strong abjuration |
| 15 | Wings of Flying | 54,000 gp | Medium | 10th | Moderate transmutation |

**Detailed Abilities:**

- **Cloak of Arachnida:** *Spider climb* at will, immunity to entrapment by web spells or effects, *web* 1/day (DC 14).
- **Cloak of the Bat:** +5 competence bonus on Hide checks. In dim light, *fly* at will (average maneuverability, 40 ft). Can hang from ceiling like a bat. If outdoors at night, can polymorph into a bat (as *polymorph*, bat form only).
- **Cloak of Charisma (+2/+4/+6):** Enhancement bonus to Charisma score. Continuous.
- **Cloak of Displacement, Minor:** Continuous 20% miss chance (as *blur*). Concealment, not total concealment.
- **Cloak of Displacement, Major:** Continuous 50% miss chance (as *displacement*). Total concealment.
- **Cloak of Elvenkind:** +5 competence bonus on Hide checks. Continuous.
- **Cloak of Etherealness:** *Ethereal jaunt* for up to 10 minutes total per day, activated/deactivated as a free action.
- **Cloak of Resistance (+1 to +5):** Resistance bonus on all saving throws. Continuous.
- **Wings of Flying:** *Fly* at will. Speed 60 ft (good maneuverability). Command word to transform cloak into wings and back.

---

### **GLOVES & GAUNTLETS** (Slot: Hands)

| # | Item | Price | Category | CL | Aura |
|---|------|-------|----------|----|----- |
| 1 | Gauntlet of Rust | 11,500 gp | Medium | 7th | Moderate transmutation |
| 2 | Gauntlets of Ogre Power | 4,000 gp | Minor | 6th | Moderate transmutation |
| 3 | Gloves of Arrow Snaring | 4,000 gp | Minor | 3rd | Faint abjuration |
| 4 | Gloves of Dexterity +2 | 4,000 gp | Minor | 8th | Moderate transmutation |
| 5 | Gloves of Dexterity +4 | 16,000 gp | Medium | 8th | Moderate transmutation |
| 6 | Gloves of Dexterity +6 | 36,000 gp | Medium | 8th | Moderate transmutation |
| 7 | Gloves of Swimming and Climbing | 6,250 gp | Minor | 5th | Faint transmutation |

**Detailed Abilities:**

- **Gauntlet of Rust:** Destroys up to 1 inch of metal thickness (iron/iron alloy) per touch, as *rusting grasp*. Single gauntlet, standard action to activate. Works on ferrous metals only.
- **Gauntlets of Ogre Power:** +2 enhancement bonus to Strength. Continuous.
- **Gloves of Arrow Snaring:** Wearer can *snatch arrows* — once per round when an arrow/bolt that would strike the wearer can be snatched out of the air (as *Snatch Arrows* feat), even without free hand. Must be aware of the attack.
- **Gloves of Dexterity (+2/+4/+6):** Enhancement bonus to Dexterity score. Continuous.
- **Gloves of Swimming and Climbing:** +5 competence bonus on Swim and Climb checks. Continuous.

---

### **HEADGEAR** (Slot: Head / Face)

| # | Item | Price | Category | CL | Aura | Slot |
|---|------|-------|----------|----|----- |------|
| 1 | Circlet of Persuasion | 4,500 gp | Minor | 5th | Faint transmutation | Head |
| 2 | Eyes of Charming | 56,000 gp | Medium | 7th | Moderate enchantment | Face |
| 3 | Eyes of the Eagle | 2,500 gp | Minor | 3rd | Faint divination | Face |
| 4 | Goggles of Minute Seeing | 1,250 gp | Minor | 3rd | Faint divination | Face |
| 5 | Goggles of Night | 2,500 gp | Minor | 3rd | Faint transmutation | Face |
| 6 | Hat of Disguise | 1,800 gp | Minor | 1st | Faint illusion | Head |
| 7 | Headband of Intellect +2 | 4,000 gp | Minor | 8th | Moderate transmutation | Head |
| 8 | Headband of Intellect +4 | 16,000 gp | Medium | 8th | Moderate transmutation | Head |
| 9 | Headband of Intellect +6 | 36,000 gp | Medium | 8th | Moderate transmutation | Head |
| 10 | Helm of Comprehend Languages and Read Magic | 5,200 gp | Minor | 1st | Faint divination | Head |
| 11 | Helm of Telepathy | 27,000 gp | Medium | 5th | Faint divination/enchantment | Head |
| 12 | Helm of Teleportation | 73,500 gp | Major | 9th | Strong conjuration | Head |
| 13 | Helm of Underwater Action | 24,000 gp | Medium | 5th | Faint transmutation | Head |
| 14 | Lens of Detection | 3,500 gp | Minor | 9th | Moderate divination | Face |

**Detailed Abilities:**

- **Circlet of Persuasion:** +3 competence bonus on Charisma-based checks. Continuous.
- **Eyes of Charming:** *Charm person* 3/day (DC 16) on any creature the wearer makes eye contact with. Will save negates.
- **Eyes of the Eagle:** +5 competence bonus on Spot checks. Continuous.
- **Goggles of Minute Seeing:** +5 competence bonus on Search checks for finding small details (traps, secret doors, etc.). Continuous.
- **Goggles of Night:** Darkvision 60 ft. Continuous. If already has darkvision, extends range by 60 ft.
- **Hat of Disguise:** *Disguise self* at will. Standard action to activate.
- **Headband of Intellect (+2/+4/+6):** Enhancement bonus to Intelligence score. Continuous. Does not grant additional skill ranks retroactively.
- **Helm of Comprehend Languages and Read Magic:** *Comprehend languages* and *read magic* both continuously active.
- **Helm of Telepathy:** *Detect thoughts* at will (DC 13). 1/day *suggest* (DC 14) on a creature whose surface thoughts are being read. Telepathic communication with any creature within 60 ft whose language the wearer speaks.
- **Helm of Teleportation:** *Teleport* 3/day. Command word. (Major item, outside scope for implementation.)
- **Helm of Underwater Action:** Wearer can breathe underwater, see clearly underwater (as *water breathing* + *freedom of movement* for swimming only). Continuous. Grants swim speed 30 ft.
- **Lens of Detection:** +5 competence bonus on Search checks, +5 on Survival checks for tracking.

---

### **NECK ITEMS (Amulets, Periapts, Necklaces)** (Slot: Throat)

| # | Item | Price | Category | CL | Aura |
|---|------|-------|----------|----|----- |
| 1 | Amulet of Health +2 | 4,000 gp | Minor | 8th | Moderate transmutation |
| 2 | Amulet of Health +4 | 16,000 gp | Medium | 8th | Moderate transmutation |
| 3 | Amulet of Health +6 | 36,000 gp | Medium | 8th | Moderate transmutation |
| 4 | Amulet of Mighty Fists +1 | 6,000 gp | Minor | 5th | Faint evocation |
| 5 | Amulet of Mighty Fists +2 | 24,000 gp | Medium | 8th | Moderate evocation |
| 6 | Amulet of Mighty Fists +3 | 54,000 gp | Medium | 12th | Strong evocation |
| 7 | Amulet of Natural Armor +1 | 2,000 gp | Minor | 5th | Faint transmutation |
| 8 | Amulet of Natural Armor +2 | 8,000 gp | Minor | 6th | Moderate transmutation |
| 9 | Amulet of Natural Armor +3 | 18,000 gp | Medium | 9th | Moderate transmutation |
| 10 | Amulet of Natural Armor +4 | 32,000 gp | Medium | 12th | Strong transmutation |
| 11 | Amulet of Natural Armor +5 | 50,000 gp | Medium | 15th | Strong transmutation |
| 12 | Amulet of Proof Against Detection and Location | 35,000 gp | Medium | 8th | Moderate abjuration |
| 13 | Amulet of the Planes | 120,000 gp | Major | 15th | Strong conjuration |
| 14 | Brooch of Shielding | 1,500 gp | Minor | 1st | Faint abjuration |
| 15 | Necklace of Adaptation | 9,000 gp | Minor | 7th | Moderate transmutation |
| 16 | Necklace of Fireballs Type I | 1,650 gp | Minor | 10th | Moderate evocation |
| 17 | Necklace of Fireballs Type II | 2,700 gp | Minor | 10th | Moderate evocation |
| 18 | Necklace of Fireballs Type III | 4,350 gp | Minor | 10th | Moderate evocation |
| 19 | Necklace of Fireballs Type IV | 5,400 gp | Minor | 10th | Moderate evocation |
| 20 | Necklace of Fireballs Type V | 5,850 gp | Minor | 10th | Moderate evocation |
| 21 | Necklace of Fireballs Type VI | 8,100 gp | Minor | 10th | Moderate evocation |
| 22 | Necklace of Fireballs Type VII | 8,700 gp | Minor | 10th | Moderate evocation |
| 23 | Periapt of Health | 7,500 gp | Minor | 5th | Faint conjuration |
| 24 | Periapt of Proof Against Poison | 27,000 gp | Medium | 7th | Moderate conjuration |
| 25 | Periapt of Wisdom +2 | 4,000 gp | Minor | 8th | Moderate transmutation |
| 26 | Periapt of Wisdom +4 | 16,000 gp | Medium | 8th | Moderate transmutation |
| 27 | Periapt of Wisdom +6 | 36,000 gp | Medium | 8th | Moderate transmutation |
| 28 | Phylactery of Faithfulness | 1,000 gp | Minor | 1st | Faint divination |
| 29 | Phylactery of Undead Turning | 11,000 gp | Medium | 10th | Moderate necromancy |
| 30 | Scarab of Protection | 38,000 gp | Medium | 18th | Strong abjuration/necromancy |

**Detailed Abilities (selected):**

- **Amulet of Health (+2/+4/+6):** Enhancement bonus to Constitution. Continuous.
- **Amulet of Mighty Fists (+1/+2/+3):** Enhancement bonus to attack and damage with unarmed/natural weapons. Continuous.
- **Amulet of Natural Armor (+1 to +5):** Enhancement bonus to natural armor AC. Stacks with existing natural armor. Continuous.
- **Amulet of Proof Against Detection and Location:** Immune to *detect thoughts*, *discern lies*, *locate creature/object*, and any divination that targets the wearer. Continuous.
- **Brooch of Shielding:** Absorbs *magic missile* damage (total of 101 points, then destroyed). Continuous.
- **Necklace of Adaptation:** *Life bubble* effect — breathe normally in any environment (underwater, vacuum, stinking cloud, etc.). Continuous.
- **Necklace of Fireballs (Types I–VII):** Strand of beads, each bead = *fireball* at various damage levels. Standard action to throw (30 ft range increment). Reflex DC 14 for half. Single-use per bead.

| Type | Beads | Damage Dice |
|------|-------|-------------|
| I | 1 | 5d6 |
| II | 1+1 | 5d6, 3d6 |
| III | 1+1+1 | 5d6, 3d6, 3d6 |
| IV | 1+1+1 | 7d6, 5d6, 3d6 |
| V | 1+1+1+1 | 7d6, 5d6, 3d6, 3d6 |
| VI | 1+1+1+1 | 9d6, 5d6, 5d6, 3d6 |
| VII | 1+1+1+1+1 | 9d6, 7d6, 5d6, 3d6, 3d6 |

- **Periapt of Health:** Immunity to all disease (including supernatural and magical disease). Continuous.
- **Periapt of Proof Against Poison:** Immunity to all poison. Continuous.
- **Periapt of Wisdom (+2/+4/+6):** Enhancement bonus to Wisdom. Continuous.
- **Phylactery of Faithfulness:** Warning to cleric if contemplated action would negatively affect alignment/standing with deity. Continuous awareness.
- **Phylactery of Undead Turning:** +4 bonus on turning checks and turning damage dice. Continuous.
- **Scarab of Protection:** +3 resistance bonus on saving throws, absorbs 12 energy drain/death effects (then crumbles). Continuous.

---

### **BELTS & WAIST ITEMS** (Slot: Waist)

| # | Item | Price | Category | CL | Aura |
|---|------|-------|----------|----|----- |
| 1 | Belt of Giant Strength +4 | 16,000 gp | Medium | 8th | Moderate transmutation |
| 2 | Belt of Giant Strength +6 | 36,000 gp | Medium | 8th | Moderate transmutation |
| 3 | Monk's Belt | 13,000 gp | Medium | 10th | Moderate transmutation |

**Detailed Abilities:**

- **Belt of Giant Strength (+4/+6):** Enhancement bonus to Strength score. Continuous.
  - Note: +2 Strength is covered by Gauntlets of Ogre Power (Hands slot).
- **Monk's Belt:** Wearer treated as a monk for purposes of AC bonus (Wis bonus to AC, +1 per 5 levels) and unarmed damage. If wearer IS a monk, functions as if 5 levels higher for AC and unarmed damage. Continuous.

---

### **ARMS/WRISTS (Bracers, Armbands)** (Slot: Arms)

| # | Item | Price | Category | CL | Aura |
|---|------|-------|----------|----|----- |
| 1 | Bracers of Archery, Lesser | 5,000 gp | Minor | 4th | Faint transmutation |
| 2 | Bracers of Archery, Greater | 25,000 gp | Medium | 8th | Moderate transmutation |
| 3 | Bracers of Armor +1 | 1,000 gp | Minor | 7th | Moderate conjuration |
| 4 | Bracers of Armor +2 | 4,000 gp | Minor | 7th | Moderate conjuration |
| 5 | Bracers of Armor +3 | 9,000 gp | Minor | 7th | Moderate conjuration |
| 6 | Bracers of Armor +4 | 16,000 gp | Medium | 7th | Moderate conjuration |
| 7 | Bracers of Armor +5 | 25,000 gp | Medium | 7th | Moderate conjuration |
| 8 | Bracers of Armor +6 | 36,000 gp | Medium | 7th | Moderate conjuration |
| 9 | Bracers of Armor +7 | 49,000 gp | Medium | 7th | Moderate conjuration |
| 10 | Bracers of Armor +8 | 64,000 gp | Medium | 7th | Moderate conjuration |

**Detailed Abilities:**

- **Bracers of Archery, Lesser:** +1 competence bonus on attack rolls with bows. Continuous. Proficiency with all bows granted.
- **Bracers of Archery, Greater:** +2 competence bonus on attack rolls with bows, +1 competence bonus on damage. Continuous. Proficiency with all bows granted.
- **Bracers of Armor (+1 to +8):** Armor bonus to AC (as if wearing armor of that enhancement). Does not stack with actual worn armor. Continuous. Can be enchanted with armor special abilities at +3 or higher.

---

### **BODY ITEMS (Vestments, Robes)** (Slot: Torso)

| # | Item | Price | Category | CL | Aura |
|---|------|-------|----------|----|----- |
| 1 | Robe of the Archmagi | 75,000 gp | Major | 14th | Strong varied |
| 2 | Robe of Blending | 8,400 gp | Medium | 10th | Moderate transmutation |
| 3 | Robe of Bones | 2,400 gp | Minor | 6th | Moderate necromancy |
| 4 | Robe of Eyes | 120,000 gp | Major | 11th | Moderate divination |
| 5 | Robe of Scintillating Colors | 27,000 gp | Medium | 11th | Moderate illusion |
| 6 | Robe of Stars | 58,000 gp | Medium | 15th | Strong varied |
| 7 | Robe of Useful Items | 7,000 gp | Medium | 9th | Moderate transmutation |
| 8 | Vestment, Druid's | 3,750 gp | Minor | 1st | Faint transmutation |

**Detailed Abilities:**

- **Robe of the Archmagi:** +5 armor bonus to AC, SR 18, +4 resistance bonus to saves, 50% arcane spell failure reduced to 0%. Alignment-specific (white=good, gray=neutral, black=evil). Major item — outside primary scope.
- **Robe of Blending:** *Disguise self* at will. If wearer is detected (true seeing, etc.), the robe changes form to match a mundane garment.
- **Robe of Bones:** 12 embroidered undead figures that can be detached and thrown to become actual undead. Each figure animates into a specific undead creature. Single-use patches.
  - Default patches: Small skeleton (×2), Medium skeleton (×2), Small zombie (×2), Medium zombie (×2), wolf skeleton, heavy horse skeleton, troll skeleton, ogre zombie.
- **Robe of Eyes:** All-around vision (360°), +10 competence bonus on Search/Spot, can't be flanked, darkvision 120 ft, *see invisibility* continuously. Vulnerable to *light* and *daylight* spells (blindness).
- **Robe of Scintillating Colors:** 30 ft radius pattern effect (hypnotic pattern, DC 16 Will). Uses 1 round of duration per activation, 10 rounds/day. Creatures within radius must save or be dazed.
- **Robe of Stars:** +1 luck bonus to saves. Six star patches — each can be thrown as a *magic missile* dealing 5d4 force damage. 1/day can step into the Astral Plane (as *astral projection*). Patches do not regenerate.
- **Robe of Useful Items:** Covered in cloth patches that can be detached and become real items. Default patches + additional random patches.
  - Default: dagger, bullseye lantern, mirror, pole, hempen rope, sack.
  - Random (4d4 additional): bag of 100 gp, coffer (silver, 500 gp), door (iron, up to 10×10 ft), gems (10×100 gp), ladder (24 ft), mule, pit (10×10×10 ft), potion of cure serious wounds, rowboat, scroll of spell (1st–3rd), war dogs (pair), window.
- **Druid's Vestment:** Wearer gains one extra use of wild shape per day. Continuous.

---

### **SLOTLESS ITEMS (Miscellaneous)**

This is the largest category. Slotless items do not occupy any equipment slot and can be used simultaneously with any other items.

#### **Storage & Containers**

| # | Item | Price | Category | CL | Capacity/Effect |
|---|------|-------|----------|----|----- |
| 1 | Bag of Holding Type I | 2,500 gp | Minor | 9th | 250 lbs / 30 cu ft, weighs 15 lbs |
| 2 | Bag of Holding Type II | 5,000 gp | Minor | 9th | 500 lbs / 70 cu ft, weighs 25 lbs |
| 3 | Bag of Holding Type III | 7,400 gp | Minor | 9th | 1,000 lbs / 150 cu ft, weighs 35 lbs |
| 4 | Bag of Holding Type IV | 10,000 gp | Minor | 9th | 1,500 lbs / 250 cu ft, weighs 60 lbs |
| 5 | Efficient Quiver (Quiver of Ehlonna) | 1,800 gp | Minor | 9th | 60 arrows, 18 javelins, 6 bows/staves |
| 6 | Handy Haversack | 2,000 gp | Minor | 9th | 120 lbs total, always retrieves desired item |
| 7 | Portable Hole | 20,000 gp | Medium | 12th | 10×10×10 ft extradimensional space |

**Implementation Notes:**
- Bag of Holding placed inside Bag of Holding/Portable Hole → rift to Astral Plane (both destroyed, nearby creatures pulled in).
- Portable Hole placed inside Bag of Holding → gate to Astral Plane.
- Bag of Holding placed inside Portable Hole → rift to Astral Plane.
- Handy Haversack always produces the desired item on top (standard action to retrieve becomes move action).

#### **Summoning Items**

| # | Item | Price | Category | CL | Effect |
|---|------|-------|----------|----|----- |
| 1 | Bag of Tricks (Gray) | 900 gp | Minor | 3rd | Pull fuzzy ball → random animal |
| 2 | Bag of Tricks (Rust) | 3,000 gp | Minor | 5th | Pull fuzzy ball → random animal |
| 3 | Bag of Tricks (Tan) | 6,900 gp | Minor | 9th | Pull fuzzy ball → random animal |
| 4 | Elemental Gem (Air) | 2,250 gp | Minor | 11th | Summon Large Air Elemental |
| 5 | Elemental Gem (Earth) | 2,250 gp | Minor | 11th | Summon Large Earth Elemental |
| 6 | Elemental Gem (Fire) | 2,250 gp | Minor | 11th | Summon Large Fire Elemental |
| 7 | Elemental Gem (Water) | 2,250 gp | Minor | 11th | Summon Large Water Elemental |
| 8 | Efreeti Bottle | 145,000 gp | Major | 19th | Release efreeti (3 wishes or service) |
| 9 | Iron Flask | 170,000 gp | Major | 20th | Trap/release outsiders |

**Bag of Tricks Animals:**

| d% | Gray | Rust | Tan |
|----|------|------|-----|
| 01–30 | Bat | Wolverine | Brown bear |
| 31–60 | Rat | Wolf | Lion |
| 61–75 | Cat | Boar | Heavy horse |
| 76–90 | Weasel | Panther | Tiger |
| 91–100 | Riding dog | Giant wasp | Rhinoceros |

**Figurines of Wondrous Power:**

| # | Figurine | Price | Category | CL | Creature/Ability |
|---|----------|-------|----------|----|----- |
| 1 | Bronze Griffon | 10,000 gp | Medium | 11th | Griffon, 6 hrs/week |
| 2 | Ebony Fly | 10,000 gp | Medium | 11th | Giant fly (flying mount), 12 hrs/week |
| 3 | Golden Lions (pair) | 16,500 gp | Medium | 11th | 2 lions, 1 hr/day |
| 4 | Ivory Goats | 21,000 gp | Medium | 11th | 3 goats — travel, travail, terror |
| 5 | Marble Elephant | 17,000 gp | Medium | 11th | Elephant, 24 hrs/month |
| 6 | Obsidian Steed | 28,500 gp | Medium | 15th | Heavy warhorse/nightmare |
| 7 | Onyx Dog | 15,500 gp | Medium | 11th | Riding dog + scent +8 |
| 8 | Serpentine Owl | 9,100 gp | Minor | 11th | Giant owl/owl |
| 9 | Silver Raven | 3,800 gp | Minor | 6th | Raven, *animal messenger* |

#### **Combat Items**

| # | Item | Price | Category | CL | Effect |
|---|------|-------|----------|----|----- |
| 1 | Bead of Force | 3,000 gp | Minor | 10th | 5d6 force damage + *resilient sphere* trap |
| 2 | Horn of Blasting | 20,000 gp | Medium | 7th | 5d6 sonic cone, DC 16 Fort or deafened |
| 3 | Horn of Blasting, Greater | 70,000 gp | Major | 16th | 10d6 sonic cone + stunned 1 round |
| 4 | Iron Bands of Binding | 26,000 gp | Medium | 13th | Ranged touch, binds Large or smaller creature |
| 5 | Cube of Force | 62,000 gp | Medium | 10th | 10-ft cube force wall with 6 effect modes |

**Cube of Force Effects:**

| Face | Effect | Charges/Min |
|------|--------|-------------|
| 1 | Keep out gases, wind, etc. | 1 |
| 2 | Keep out nonliving matter | 2 |
| 3 | Keep out living matter | 3 |
| 4 | Keep out magic | 4 |
| 5 | Keep out all things | 6 |
| 6 | Deactivate | 0 |

#### **Dusts & Consumables**

| # | Item | Price | Category | CL | Effect |
|---|------|-------|----------|----|----- |
| 1 | Dust of Appearance | 1,800 gp | Minor | 5th | Reveals invisible creatures/objects in 10-ft radius |
| 2 | Dust of Disappearance | 3,500 gp | Minor | 7th | *Greater invisibility* for 2d6 rounds |
| 3 | Dust of Dryness | 850 gp | Minor | 11th | Absorbs water (100 cu ft) or deals 5d6 to water elementals |
| 4 | Dust of Illusion | 1,200 gp | Minor | 6th | *Disguise self* + clothing for 2 hours |
| 5 | Dust of Tracelessness | 250 gp | Minor | 3rd | No tracks for 250 ft, +5 DC to track |
| 6 | Incense of Meditation | 4,900 gp | Minor | 7th | Burns 8 hours, maximizes divine spells for 24 hours |
| 7 | Restorative Ointment | 4,000 gp | Minor | 5th | 5 applications — cure 1d8+5 or *neutralize poison* or *remove disease* |
| 8 | Salve of Slipperiness | 1,000 gp | Minor | 6th | *Freedom of movement* for 8 hours |
| 9 | Sovereign Glue | 2,400 gp | Minor | 20th | Permanently bonds any two surfaces |
| 10 | Universal Solvent | 50 gp | Minor | 20th | Dissolves sovereign glue, tanglefoot bags, etc. |
| 11 | Stone Salve | 4,000 gp | Minor | 13th | Returns petrified creature to flesh (2 applications) |
| 12 | Silversheen | 250 gp | Minor | 5th | Weapon counts as silver for 1 hour |
| 13 | Unguent of Timelessness | 150 gp | Minor | 3rd | Preserves item indefinitely |

#### **Feather Tokens**

| # | Token | Price | Category | CL | Effect |
|---|-------|-------|----------|----|----- |
| 1 | Anchor | 50 gp | Minor | 12th | Moors vessel in place |
| 2 | Bird | 300 gp | Minor | 12th | Carries written message 500 miles |
| 3 | Fan | 200 gp | Minor | 12th | Creates gentle breeze (ship speed +2) |
| 4 | Swan Boat | 450 gp | Minor | 12th | Creates swan-shaped boat (24 passengers) |
| 5 | Tree | 400 gp | Minor | 12th | Creates 60-ft oak tree |
| 6 | Whip | 500 gp | Minor | 12th | Creates +1 dancing whip |

#### **Ropes, Horns, Pipes**

| # | Item | Price | Category | CL | Effect |
|---|------|-------|----------|----|----- |
| 1 | Rope of Climbing | 3,000 gp | Minor | 3rd | 60-ft rope that climbs/knots on command |
| 2 | Rope of Entanglement | 21,000 gp | Medium | 12th | Animates to grapple (+15 check), entangles foes |
| 3 | Horn of Fog | 2,000 gp | Minor | 3rd | Creates *obscuring mist* (10-ft radius) |
| 4 | Horn of Goodness/Evil | 6,500 gp | Minor | 6th | *Magic circle against evil/good* for 1 hour, 1/day |
| 5 | Horn of Valhalla (Silver) | 50,000 gp | Medium | 13th | Summons 2d4+2 barbarians (2nd level) for 1 hr |
| 6 | Horn of Valhalla (Brass) | 34,000 gp | Medium | 13th | Summons 2d4+2 barbarians (3rd level), requires proficiency with all martial weapons |
| 7 | Horn of Valhalla (Bronze) | 40,000 gp | Medium | 13th | Summons 2d4+2 barbarians (4th level), requires proficiency with medium armor |
| 8 | Horn of Valhalla (Iron) | 75,000 gp | Major | 13th | Summons 2d4+2 barbarians (5th level), requires Martial Weapon Proficiency |
| 9 | Pipes of Haunting | 6,500 gp | Minor | 4th | Fear effect in 30-ft radius, DC 13 Will |
| 10 | Pipes of Pain | 12,000 gp | Medium | — | 2d4 damage + *cause fear* (DC 14) in 30-ft area |
| 11 | Pipes of Sounding | 1,800 gp | Minor | 2nd | *Ghost sound* + *sound burst* (1d8, DC 13) |
| 12 | Pipes of the Sewers | 1,150 gp | Minor | 2nd | Summons/controls rats (as *summon swarm*) |

#### **Ioun Stones (Slotless, Orbiting)**

All Ioun Stones: Small crystalline stones that orbit the owner's head at a distance of 1d3 feet. They have AC 24, 10 HP, hardness 5.

| # | Stone Type | Price | Category | Effect |
|---|-----------|-------|----------|--------|
| 1 | Clear Spindle | 4,000 gp | Minor | Sustains creature without food or water |
| 2 | Dusty Rose Prism | 5,000 gp | Medium | +1 insight bonus to AC |
| 3 | Deep Red Sphere | 8,000 gp | Medium | +2 enhancement bonus to Dexterity |
| 4 | Incandescent Blue Sphere | 8,000 gp | Medium | +2 enhancement bonus to Wisdom |
| 5 | Pale Blue Rhomboid | 8,000 gp | Medium | +2 enhancement bonus to Strength |
| 6 | Pink Rhomboid | 8,000 gp | Medium | +2 enhancement bonus to Constitution |
| 7 | Pink and Green Sphere | 8,000 gp | Medium | +2 enhancement bonus to Charisma |
| 8 | Scarlet and Blue Sphere | 8,000 gp | Medium | +2 enhancement bonus to Intelligence |
| 9 | Dark Blue Rhomboid | 10,000 gp | Medium | Alertness feat (when not already possessing it) |
| 10 | Vibrant Purple Prism | 36,000 gp | Medium | Stores three levels of spells (as *ring of spell storing*) |
| 11 | Iridescent Spindle | 18,000 gp | Medium | Sustains creature without air |
| 12 | Pale Lavender Ellipsoid | 20,000 gp | Medium | Absorbs spells of 4th level or lower (absorbs 20 levels, then burns out) |
| 13 | Pearly White Spindle | 20,000 gp | Medium | Regenerate 1 HP per 10 minutes (not true regeneration) |
| 14 | Orange Prism | 30,000 gp | Medium | +1 caster level |
| 15 | Pale Green Prism | 30,000 gp | Medium | +1 competence bonus on attack rolls, saves, skill checks, ability checks |
| 16 | Lavender and Green Ellipsoid | 40,000 gp | Medium | Absorbs spells of 8th level or lower (absorbs 50 levels, then burns out) |

**Implementation Notes:**
- Ioun Stones do NOT occupy an equipment slot but only one stone of each type can be active at a time.
- Ability score enhancement Ioun Stones DO stack with other enhancement bonuses from different sources, but NOT with same-typed bonuses from other items (e.g., Pale Blue Rhomboid Str +2 does not stack with Gauntlets of Ogre Power Str +2 — both are enhancement bonuses).
- Ioun Stones can be grabbed by a target in combat (ranged touch attack vs AC 24) or struck (AC 24, 10 HP, hardness 5).
- A "burned out" or dull gray Ioun Stone still orbits but confers no benefit.

#### **Pearls of Power (Slotless)**

| # | Pearl Level | Price | Category |
|---|------------|-------|----------|
| 1 | Pearl of Power (1st) | 1,000 gp | Minor |
| 2 | Pearl of Power (2nd) | 4,000 gp | Minor |
| 3 | Pearl of Power (3rd) | 9,000 gp | Minor |
| 4 | Pearl of Power (4th) | 16,000 gp | Medium |
| 5 | Pearl of Power (5th) | 25,000 gp | Medium |
| 6 | Pearl of Power (6th) | 36,000 gp | Medium |
| 7 | Pearl of Power (7th) | 49,000 gp | Medium |
| 8 | Pearl of Power (8th) | 64,000 gp | Medium |
| 9 | Pearl of Power (9th) | 81,000 gp | Major |
| 10 | Pearl of Power (Two Spells) | 70,000 gp | Major |

**Effect:** Once per day, recall any one spell of the designated level that was already cast. Standard action, must have originally prepared/known the spell.

#### **Miscellaneous Slotless**

| # | Item | Price | Category | CL | Effect |
|---|------|-------|----------|----|----- |
| 1 | Bottle of Air | 7,250 gp | Minor | 7th | Unlimited air supply when uncorked underwater |
| 2 | Candle of Truth | 2,500 gp | Minor | 3rd | *Zone of truth* (DC 13) in 5-ft radius while burning (1 hr) |
| 3 | Carpet of Flying (5×5 ft) | 20,000 gp | Medium | 5th | 40 ft/round, 200 lb capacity |
| 4 | Carpet of Flying (5×10 ft) | 35,000 gp | Medium | 10th | 40 ft/round, 400 lb capacity |
| 5 | Carpet of Flying (10×10 ft) | 60,000 gp | Medium | 10th | 40 ft/round, 800 lb capacity |
| 6 | Chime of Opening | 3,000 gp | Minor | 11th | Opens locks/lids, 10 charges, *knock* effect |
| 7 | Cube of Frost Resistance | 27,000 gp | Medium | 5th | Absorbs cold damage (continuously), 10-ft radius |
| 8 | Decanter of Endless Water | 9,000 gp | Minor | 9th | Produces water on command (stream/fountain/geyser) |
| 9 | Eversmoking Bottle | 5,400 gp | Minor | 3rd | *Obscuring mist* in 50-ft radius when uncorked |
| 10 | Folding Boat | 7,200 gp | Minor | 6th | 12-ft boat/24-ft ship on command |
| 11 | Gem of Brightness | 13,000 gp | Medium | 6th | 50 charges: light (1), bright light 30-ft (1), *daylight* (1), blind (5 charges) |
| 12 | Gem of Seeing | 75,000 gp | Major | 10th | *True seeing* for 30 min/day |
| 13 | Hand of the Mage | 900 gp | Minor | 2nd | *Mage hand* at will |
| 14 | Helm of Brilliance | 125,000 gp | Major | 13th | Multiple gems with spell effects |
| 15 | Horseshoes of Speed | 3,000 gp | Minor | 3rd | +30 ft to horse's base speed |
| 16 | Horseshoes of a Zephyr | 6,000 gp | Minor | 3rd | Horse doesn't touch ground (no tracks, walk on water) |
| 17 | Lantern of Revealing | 30,000 gp | Medium | 5th | *Invisibility purge* in 25-ft radius while lit |
| 18 | Lyre of Building | 13,000 gp | Medium | 6th | Perform check → *fabricate* effect (construction) |
| 19 | Mantle of Faith | 76,000 gp | Major | 20th | DR 5/evil |
| 20 | Mantle of Spell Resistance | 90,000 gp | Major | 9th | SR 21 |
| 21 | Mattock of the Titans | 23,348 gp | Medium | — | +3 adamantine warhammer for Large creatures |
| 22 | Mirror of Life Trapping | 200,000 gp | Major | 17th | Trap creatures inside (up to 15) |
| 23 | Mirror of Mental Prowess | 175,000 gp | Major | 17th | *Detect thoughts*, *clairvoyance*, *gate* |
| 24 | Mirror of Opposition | 92,000 gp | Major | 15th | Creates hostile duplicate |
| 25 | Scarab, Golembane | 2,500 gp | Minor | 8th | Detects golems, attacks as +1 to hit/bypass DR |
| 26 | Stone of Alarm | 2,700 gp | Minor | 3rd | Mental alarm in 20-ft radius |
| 27 | Stone of Controlling Earth Elementals | 100,000 gp | Major | — | Control earth elementals |
| 28 | Stone of Good Luck (Luckstone) | 20,000 gp | Medium | 5th | +1 luck bonus on saves, ability checks, skill checks |
| 29 | Sustaining Spoon | 5,400 gp | Minor | 5th | Provides sustenance for 1 creature/day |
| 30 | Vest of Escape | 5,200 gp | Minor | 4th | +6 competence on Escape Artist, +4 on Open Lock |
| 31 | Well of Many Worlds | 82,000 gp | Major | 12th | Opens random planar portal |
| 32 | Wind Fan | 5,500 gp | Minor | 5th | *Gust of wind* 1/day |

#### **Elixirs (Slotless, Consumable)**

| # | Item | Price | Category | CL | Effect |
|---|------|-------|----------|----|----- |
| 1 | Elixir of Fire Breath | 1,100 gp | Minor | 11th | 3 uses of fire breath (4d6, 25-ft cone) |
| 2 | Elixir of Hiding | 250 gp | Minor | 5th | +10 competence on Hide for 1 hour |
| 3 | Elixir of Love | 150 gp | Minor | 4th | *Charm person* (DC 14) on next creature seen |
| 4 | Elixir of Swimming | 250 gp | Minor | 2nd | +10 competence on Swim for 1 hour |
| 5 | Elixir of Truth | 500 gp | Minor | 5th | *Zone of truth* on drinker for 10 minutes |
| 6 | Elixir of Tumbling | 250 gp | Minor | 5th | +10 competence on Tumble for 1 hour |
| 7 | Elixir of Vision | 250 gp | Minor | 2nd | +10 competence on Spot for 1 hour |

---

## CATEGORIZATION BY FUNCTION

### **1. ABILITY SCORE ENHANCEMENT (19 items)**

**+2 Variants (Minor — 4,000 gp each):**

| Item | Slot | Ability | Price |
|------|------|---------|-------|
| Gauntlets of Ogre Power | Hands | Str +2 | 4,000 gp |
| Gloves of Dexterity +2 | Hands | Dex +2 | 4,000 gp |
| Amulet of Health +2 | Throat | Con +2 | 4,000 gp |
| Headband of Intellect +2 | Head | Int +2 | 4,000 gp |
| Periapt of Wisdom +2 | Throat | Wis +2 | 4,000 gp |
| Cloak of Charisma +2 | Shoulders | Cha +2 | 4,000 gp |

**+4 Variants (Medium — 16,000 gp each):**

| Item | Slot | Ability | Price |
|------|------|---------|-------|
| Belt of Giant Strength +4 | Waist | Str +4 | 16,000 gp |
| Gloves of Dexterity +4 | Hands | Dex +4 | 16,000 gp |
| Amulet of Health +4 | Throat | Con +4 | 16,000 gp |
| Headband of Intellect +4 | Head | Int +4 | 16,000 gp |
| Periapt of Wisdom +4 | Throat | Wis +4 | 16,000 gp |
| Cloak of Charisma +4 | Shoulders | Cha +4 | 16,000 gp |

**+6 Variants (Medium — 36,000 gp each):**

| Item | Slot | Ability | Price |
|------|------|---------|-------|
| Belt of Giant Strength +6 | Waist | Str +6 | 36,000 gp |
| Gloves of Dexterity +6 | Hands | Dex +6 | 36,000 gp |
| Amulet of Health +6 | Throat | Con +6 | 36,000 gp |
| Headband of Intellect +6 | Head | Int +6 | 36,000 gp |
| Periapt of Wisdom +6 | Throat | Wis +6 | 36,000 gp |
| Cloak of Charisma +6 | Shoulders | Cha +6 | 36,000 gp |

**Ioun Stone Ability Variants (Medium — 8,000 gp each):**

| Stone | Ability | Price |
|-------|---------|-------|
| Pale Blue Rhomboid | Str +2 | 8,000 gp |
| Deep Red Sphere | Dex +2 | 8,000 gp |
| Pink Rhomboid | Con +2 | 8,000 gp |
| Scarlet and Blue Sphere | Int +2 | 8,000 gp |
| Incandescent Blue Sphere | Wis +2 | 8,000 gp |
| Pink and Green Sphere | Cha +2 | 8,000 gp |

**Implementation:** Simple passive enhancement bonus to designated ability score. Enhancement bonuses from different items targeting the same ability score do NOT stack (highest applies). These are the simplest items to implement.

---

### **2. ARMOR CLASS ENHANCEMENT (13 items)**

| Item | AC Type | Bonus | Price |
|------|---------|-------|-------|
| Amulet of Natural Armor +1 | Natural armor (enhancement) | +1 | 2,000 gp |
| Amulet of Natural Armor +2 | Natural armor (enhancement) | +2 | 8,000 gp |
| Amulet of Natural Armor +3 | Natural armor (enhancement) | +3 | 18,000 gp |
| Amulet of Natural Armor +4 | Natural armor (enhancement) | +4 | 32,000 gp |
| Amulet of Natural Armor +5 | Natural armor (enhancement) | +5 | 50,000 gp |
| Bracers of Armor +1 to +8 | Armor bonus | +1 to +8 | 1,000–64,000 gp |
| Cloak of Displacement, Minor | Miss chance | 20% | 24,000 gp |
| Cloak of Displacement, Major | Miss chance | 50% | 50,000 gp |
| Dusty Rose Prism (Ioun Stone) | Insight to AC | +1 | 5,000 gp |

**Implementation:**
- Natural armor enhancement bonus → stacks with actual natural armor but not with other enhancement bonuses to natural armor.
- Bracers of Armor → armor bonus, does not stack with worn armor.
- Displacement → miss chance mechanic (separate from AC).
- Insight bonus → stacks with everything (Dusty Rose Prism).

---

### **3. SAVING THROW ENHANCEMENT (6+ items)**

| Item | Save Affected | Bonus | Price |
|------|--------------|-------|-------|
| Cloak of Resistance +1 | All saves | +1 resistance | 1,000 gp |
| Cloak of Resistance +2 | All saves | +2 resistance | 4,000 gp |
| Cloak of Resistance +3 | All saves | +3 resistance | 9,000 gp |
| Cloak of Resistance +4 | All saves | +4 resistance | 16,000 gp |
| Cloak of Resistance +5 | All saves | +5 resistance | 25,000 gp |
| Scarab of Protection | All saves | +3 resistance + death ward (12 charges) | 38,000 gp |
| Pale Green Prism (Ioun Stone) | All saves + attacks + skills | +1 competence | 30,000 gp |
| Robe of Stars | All saves | +1 luck | 58,000 gp |

**Implementation:** Resistance bonus tracking on all saves. Multiple resistance bonuses do NOT stack (highest applies). Competence and luck bonuses stack with resistance bonuses.

---

### **4. MOVEMENT ENHANCEMENT (10 items)**

| Item | Movement Type | Effect | Price |
|------|-------------|--------|-------|
| Boots of Elvenkind | Stealth movement | +5 Move Silently | 2,500 gp |
| Boots of Levitation | Vertical | Levitate at will | 7,500 gp |
| Boots of Speed | Speed | Haste 10 rds/day | 12,000 gp |
| Boots of Striding and Springing | Land + jump | +10 ft speed, +5 Jump | 5,500 gp |
| Boots of Teleportation | Instant | Teleport 3/day | 49,000 gp |
| Boots of the Winterlands | Snow/ice | +10 ft in snow, endure cold | 2,500 gp |
| Slippers of Spider Climbing | Climb | Spider climb at will | 4,800 gp |
| Winged Boots | Flight | Fly 3/day (60 ft, good) | 16,000 gp |
| Wings of Flying | Flight | Fly at will (60 ft, good) | 54,000 gp |
| Carpet of Flying | Flight | Fly 40 ft, capacity varies | 20,000–60,000 gp |
| Horseshoes of Speed | Mount speed | +30 ft to horse speed | 3,000 gp |
| Horseshoes of a Zephyr | Mount movement | Walk on air/water, no tracks | 6,000 gp |

**Implementation:** Speed modifiers, flight mechanics, teleportation, special movement modes. Requires movement system integration.

---

### **5. STEALTH & DETECTION (12 items)**

| Item | Type | Effect | Price |
|------|------|--------|-------|
| Boots of Elvenkind | Skill | +5 Move Silently | 2,500 gp |
| Cloak of Elvenkind | Skill | +5 Hide | 2,500 gp |
| Dust of Appearance | Reveal | Reveals invisible (10-ft radius) | 1,800 gp |
| Dust of Disappearance | Conceal | Greater invisibility 2d6 rounds | 3,500 gp |
| Dust of Tracelessness | Conceal | No tracks, +5 DC to track | 250 gp |
| Eyes of the Eagle | Skill | +5 Spot | 2,500 gp |
| Goggles of Night | Vision | Darkvision 60 ft | 2,500 gp |
| Hat of Disguise | Illusion | Disguise self at will | 1,800 gp |
| Amulet of Proof Against Detection | Ward | Immune to divination targeting wearer | 35,000 gp |
| Lantern of Revealing | Reveal | Invisibility purge 25-ft radius | 30,000 gp |
| Robe of Blending | Illusion | Disguise self at will | 8,400 gp |
| Robe of Eyes | Vision | 360° vision, see invisible, darkvision 120 | 120,000 gp |

**Implementation:** Skill bonuses, vision mode toggles, invisibility/revelation mechanics.

---

### **6. SKILL ENHANCEMENT (15 items)**

| Item | Skill(s) | Bonus | Price |
|------|---------|-------|-------|
| Circlet of Persuasion | Charisma-based checks | +3 competence | 4,500 gp |
| Eyes of the Eagle | Spot | +5 competence | 2,500 gp |
| Goggles of Minute Seeing | Search | +5 competence | 1,250 gp |
| Gloves of Swimming and Climbing | Swim, Climb | +5 competence | 6,250 gp |
| Lens of Detection | Search, Survival (tracking) | +5 competence | 3,500 gp |
| Vest of Escape | Escape Artist, Open Lock | +6/+4 competence | 5,200 gp |
| Cloak of the Bat | Hide | +5 competence | 26,000 gp |
| Boots of Elvenkind | Move Silently | +5 competence | 2,500 gp |
| Cloak of Elvenkind | Hide | +5 competence | 2,500 gp |
| Boots of Striding and Springing | Jump | +5 competence | 5,500 gp |

**Implementation:** Competence bonus modifiers to specific skills. All competence bonuses from different sources to the same skill do NOT stack (highest applies).

---

### **7. STORAGE & UTILITY (15 items)**

See **Slotless Items — Storage & Containers** section above for full details.

**Key Implementation Challenges:**
- Weight reduction calculations (bag contents weigh less than capacity)
- Extradimensional space interactions (bag in bag → Astral rift)
- Retrieval mechanics (Haversack always finds right item)
- Volume vs weight tracking

---

### **8. COMBAT ITEMS (12 items)**

See **Slotless Items — Combat Items** section above for full details.

**Key Implementation Challenges:**
- Fireball bead targeting and AoE damage
- Force effects (Bead of Force, Cube of Force)
- Binding/grapple mechanics (Iron Bands)
- Charge tracking (Cube of Force)

---

### **9. SUMMONING & TRANSFORMATION (10 items)**

See **Slotless Items — Summoning Items** section above for full details.

**Key Implementation Challenges:**
- Random animal tables (Bag of Tricks)
- Duration tracking per figurine
- Creature stat blocks for summoned entities
- Weekly/monthly reset tracking

---

### **10. SPELL RECALL (9 items)**

Pearl of Power — see Pearls of Power section above.

**Implementation:** Interface with spellcasting system. Recall a spent spell slot of the designated level. Standard action, 1/day per pearl.

---

### **11. PROTECTION ITEMS (8 items)**

| Item | Protection Type | Effect | Price |
|------|---------------|--------|-------|
| Brooch of Shielding | Spell | Absorbs magic missiles (101 points) | 1,500 gp |
| Cube of Frost Resistance | Cold | Absorbs cold damage, 10-ft radius | 27,000 gp |
| Periapt of Health | Disease | Immunity to all disease | 7,500 gp |
| Periapt of Proof Against Poison | Poison | Immunity to all poison | 27,000 gp |
| Scarab of Protection | Death/drain | Absorbs 12 negative levels/death effects | 38,000 gp |
| Necklace of Adaptation | Environment | Breathe in any environment | 9,000 gp |
| Mantle of Spell Resistance | Spells | SR 21 | 90,000 gp |
| Mantle of Faith | Physical | DR 5/evil | 76,000 gp |

---

### **12. DIVINATION & KNOWLEDGE (8 items)**

| Item | Effect | Price |
|------|--------|-------|
| Gem of Seeing | True seeing 30 min/day | 75,000 gp |
| Helm of Comprehend Languages | Comprehend languages + read magic continuously | 5,200 gp |
| Helm of Telepathy | Detect thoughts at will, suggest 1/day | 27,000 gp |
| Lens of Detection | +5 Search and Survival (tracking) | 3,500 gp |
| Robe of Eyes | 360° vision, see invisible, darkvision 120 ft | 120,000 gp |
| Stone of Alarm | Mental alarm in 20-ft radius | 2,700 gp |

---

### **13. SPECIAL ROBES & VESTMENTS (7 items)**

See **Body Items** section above for full details.

---

## COMPLEXITY TIERS

### **TIER 1: Simple Passive (⭐) — ~45 items**
Items that apply a static bonus with no activation or special mechanics.

| Category | Items | Count |
|----------|-------|-------|
| Ability Score +2 | Gauntlets of Ogre Power, Gloves of Dex +2, Amulet of Health +2, Headband of Int +2, Periapt of Wis +2, Cloak of Cha +2 | 6 |
| Ability Score +4 | Belt of Giant Str +4, Gloves of Dex +4, Amulet of Health +4, Headband of Int +4, Periapt of Wis +4, Cloak of Cha +4 | 6 |
| Ability Score +6 | Belt of Giant Str +6, Gloves of Dex +6, Amulet of Health +6, Headband of Int +6, Periapt of Wis +6, Cloak of Cha +6 | 6 |
| Resistance | Cloak of Resistance +1 to +5 | 5 |
| Natural Armor | Amulet of Natural Armor +1 to +5 | 5 |
| Bracers of Armor | Bracers of Armor +1 to +8 | 8 |
| Skill Bonuses | Boots of Elvenkind, Cloak of Elvenkind, Eyes of the Eagle, Goggles of Minute Seeing, Goggles of Night, Circlet of Persuasion, Gloves of Swimming/Climbing, Lens of Detection, Vest of Escape | 9 |
| Misc Passive | Periapt of Health, Phylactery of Faithfulness, Horseshoes of Speed, Boots of Striding/Springing, Monk's Belt | 5 |

**Implementation Pattern:**
```
class SimplePassiveItem:
    slot: EquipmentSlot
    bonus_type: BonusType  # enhancement, competence, resistance, etc.
    bonus_target: str       # ability score, save, skill, AC
    bonus_value: int
    # On equip: add bonus
    # On unequip: remove bonus
```

**Estimated Time:** 1–2 weeks

---

### **TIER 2: Active Single Ability (⭐⭐) — ~30 items**
Items with a single activated ability, uses per day, or toggle on/off.

| Category | Items | Count |
|----------|-------|-------|
| At-Will Abilities | Hat of Disguise, Boots of Levitation, Slippers of Spider Climbing, Hand of the Mage, Robe of Blending | 5 |
| X/Day Abilities | Boots of Speed (10 rds), Winged Boots (3/day fly), Eyes of Charming (3/day charm), Horn of Goodness/Evil (1/day) | 4 |
| Movement Modes | Wings of Flying, Cloak of Etherealness, Boots of Teleportation | 3 |
| Continuous Special | Cloak of Displacement Minor/Major, Helm of Comprehend Languages, Helm of Underwater Action, Necklace of Adaptation, Scarab of Protection, Brooch of Shielding | 6 |
| Consumable Single-Use | All Dusts, Elixirs, Feather Tokens, Candle of Truth, Incense of Meditation | 12+ |

**Implementation Pattern:**
```
class ActiveItem:
    activation_type: str   # command_word, standard_action, free_action
    uses_per_day: int      # -1 = at will
    duration: Duration     # rounds, minutes, hours, continuous
    spell_effect: str      # underlying spell replicated
    # Activation tracking
    # Duration management
    # Reset on rest/day
```

**Estimated Time:** 2–3 weeks

---

### **TIER 3: Storage & Containers (⭐⭐⭐) — ~15 items**
Items requiring inventory management sub-systems.

| Item | Key Mechanic |
|------|-------------|
| Bag of Holding I–IV | Weight reduction, volume limit, extradimensional |
| Handy Haversack | Auto-retrieve, weight reduction |
| Efficient Quiver | Compartmented storage |
| Portable Hole | Large extradimensional space |
| Folding Boat | Transforms between objects |
| Bottle of Air | Unlimited resource generation |
| Decanter of Endless Water | Variable-rate resource generation |
| Eversmoking Bottle | Area effect toggle |

**Implementation Pattern:**
```
class MagicContainer:
    weight_limit: float
    volume_limit: float    # cubic feet
    actual_weight: float   # weight when carried (reduced)
    contents: List[Item]
    is_extradimensional: bool
    # Extradimensional interaction rules
    # Weight/volume tracking
    # Retrieval mechanics
```

**Estimated Time:** 2 weeks

---

### **TIER 4: Combat & Summoning (⭐⭐⭐⭐) — ~25 items**
Items requiring combat system integration, creature summoning, and duration tracking.

| Category | Items | Count |
|----------|-------|-------|
| Necklace of Fireballs | Types I–VII (fireball beads) | 7 |
| Bag of Tricks | Gray, Rust, Tan (random animal summoning) | 3 |
| Elemental Gems | Air, Earth, Fire, Water (Large elemental summon) | 4 |
| Figurines of Wondrous Power | 9 figurine types with usage tracking | 9 |
| Combat Misc | Bead of Force, Horn of Blasting, Iron Bands, Bracers of Archery, Gauntlet of Rust | 5 |
| Horn of Valhalla | Silver, Brass, Bronze, Iron (summon barbarians) | 4 |

**Implementation Requirements:**
- Creature stat blocks for all summonable creatures
- Duration tracking (hours/day, hours/week, hours/month per figurine)
- Random table rolls (Bag of Tricks)
- AoE damage mechanics (Necklace of Fireballs)
- Grapple mechanics (Iron Bands of Binding)

**Estimated Time:** 3–4 weeks

---

### **TIER 5: Complex Multi-Ability (⭐⭐⭐⭐⭐) — ~15 items**
Items with multiple interacting abilities, complex state, or requiring new subsystems.

| Item | Complexity Reason |
|------|------------------|
| Robe of the Archmagi | AC + SR + saves + arcane failure reduction + alignment |
| Robe of Stars | Saves + force missiles + astral projection + limited patches |
| Robe of Useful Items | Random patch generation + multiple item creation |
| Robe of Bones | 12 undead patches with individual creature stats |
| Robe of Eyes | Multiple vision modes + vulnerability |
| Cube of Force | 6 modes + charge tracking + wall mechanics |
| Ioun Stones (16 types) | Slotless system + varied effects + physical targeting |
| Cloak of Arachnida | Spider climb + web immunity + web 1/day |
| Cloak of the Bat | Hide + fly + polymorph (conditional) |
| Helm of Telepathy | Detect thoughts + suggest + telepathic communication |
| Robe of Scintillating Colors | AoE save-or-daze + daily usage tracking |
| Carpet of Flying | Movement platform + weight capacity |
| Rope of Entanglement | Animated grapple mechanics |

**Estimated Time:** 3–4 weeks

---

## IMPLEMENTATION TIMELINE

### **PHASE 1: Foundation (2 weeks)**
**Goal:** Build the wondrous item infrastructure.

**Tasks:**
1. **Equipment Slot System Expansion**
   - Define all wondrous item slots: Feet, Shoulders, Hands, Head, Face, Throat, Waist, Arms, Torso
   - Implement slot validation (only one item per slot)
   - Handle slotless item management (no slot restriction)

2. **Wondrous Item Base Class**
   ```
   WondrousItem extends MagicItem:
       equipment_slot: EquipmentSlot | None
       activation_type: ActivationType  # continuous, command_word, use_activated, etc.
       uses_per_day: int
       charges: int | None
       caster_level: int
       aura_strength: AuraStrength
       aura_school: MagicSchool
       weight: float
       body_text: str  # description
   ```

3. **Wondrous Item Factory**
   - Registration system for all wondrous item types
   - Construction from database/configuration
   - Variant handling (e.g., Cloak of Resistance +1 through +5 from single template)

4. **Activation Framework**
   - Command word activation
   - Use-activated (standard/move/free action)
   - Continuous (always on when worn)
   - Consumable (single-use then destroyed)
   - Charges (track, decrement, destroy when empty)

5. **Database Schema**
   ```sql
   CREATE TABLE wondrous_items (
       id TEXT PRIMARY KEY,
       name TEXT NOT NULL,
       base_type TEXT NOT NULL,      -- e.g., 'cloak_of_resistance'
       variant TEXT,                  -- e.g., '+3'
       equipment_slot TEXT,           -- nullable for slotless
       price_gp INTEGER NOT NULL,
       caster_level INTEGER,
       aura_school TEXT,
       aura_strength TEXT,
       weight REAL,
       activation_type TEXT,
       uses_per_day INTEGER,
       charges INTEGER,
       description TEXT
   );
   ```

**Deliverables:** Equipment slot system, WondrousItem base class, factory, database, activation framework.

---

### **PHASE 2: Ability Score Items (2 weeks)**
**Goal:** Implement all 18 ability score enhancement items + 6 Ioun Stone ability variants.

**Items (24 total):**
- Gauntlets of Ogre Power, Belt of Giant Strength +4/+6
- Gloves of Dexterity +2/+4/+6
- Amulet of Health +2/+4/+6
- Headband of Intellect +2/+4/+6
- Periapt of Wisdom +2/+4/+6
- Cloak of Charisma +2/+4/+6
- Ioun Stones: Pale Blue Rhomboid, Deep Red Sphere, Pink Rhomboid, Scarlet and Blue Sphere, Incandescent Blue Sphere, Pink and Green Sphere

**Implementation:**
- `AbilityScoreEnhancementItem` template class
- Apply enhancement bonus on equip, remove on unequip
- Verify non-stacking of same-type bonuses
- Intelligence bonus: clarify no retroactive skill points

**Tests:**
- Equip/unequip changes ability score correctly
- Multiple enhancement bonuses to same ability → only highest applies
- Ability modifier recalculation on equip/unequip

---

### **PHASE 3: AC & Save Items (2 weeks)**
**Goal:** Implement AC bonuses, save bonuses, and displacement mechanics.

**Items (~20 variants):**
- Amulet of Natural Armor +1 to +5
- Bracers of Armor +1 to +8
- Cloak of Resistance +1 to +5
- Cloak of Displacement Minor/Major
- Dusty Rose Prism Ioun Stone
- Scarab of Protection

**Implementation:**
- `NaturalArmorEnhancementItem` — adds enhancement bonus to natural armor
- `ArmorBonusItem` — adds armor bonus (doesn't stack with worn armor)
- `ResistanceBonusItem` — adds resistance bonus to all saves
- `DisplacementItem` — miss chance mechanic (20% or 50%)
- Bonus stacking rules enforcement

**New Mechanics:**
- Miss chance calculation (d% roll, 01–20 for minor, 01–50 for major)
- Displacement does NOT stack with other miss chance effects (best applies)

**Tests:**
- AC calculation with multiple bonus sources
- Resistance bonus stacking (only highest)
- Displacement miss chance resolution

---

### **PHASE 4: Movement Items (2 weeks)**
**Goal:** Implement all movement-enhancing wondrous items.

**Items (12):**
- Boots of Elvenkind, Levitation, Speed, Striding/Springing, Teleportation, Winterlands
- Slippers of Spider Climbing
- Winged Boots
- Wings of Flying
- Carpet of Flying (3 sizes)
- Horseshoes of Speed/Zephyr

**Implementation:**
- Speed modification system (enhancement bonus to base speed)
- Flight mode: speed, maneuverability rating (perfect/good/average/poor/clumsy)
- Levitation: vertical movement only
- Spider climb: wall/ceiling movement at half speed
- Teleportation: spell-like effect with daily limit
- Haste: 10 rounds/day tracking (Boots of Speed)
- Mount-specific items (Horseshoes)

**New Systems:**
- Flight maneuverability system
- Teleportation mechanics (range, accuracy)
- Duration-per-day tracking (Boots of Speed: 10 rounds)
- Conditional movement bonuses (Boots of Winterlands: snow only)

**Tests:**
- Speed calculations with multiple bonuses
- Flight duration tracking
- Teleportation destination validation
- Haste round tracking and reset

---

### **PHASE 5: Stealth & Detection Items (2 weeks)**
**Goal:** Implement stealth, detection, and vision items.

**Items (~15):**
- Boots of Elvenkind, Cloak of Elvenkind
- Dust of Appearance, Disappearance, Tracelessness
- Eyes of the Eagle
- Goggles of Night
- Hat of Disguise
- Robe of Blending
- Amulet of Proof Against Detection
- Lantern of Revealing

**Implementation:**
- Skill bonus items (competence bonuses to Hide, Move Silently, Spot, Search)
- Vision modes: Darkvision (distance), See Invisible, True Seeing
- Disguise mechanics (Disguise Self spell effect)
- Anti-divination ward
- Invisibility (greater invisibility from Dust of Disappearance)
- Reveal mechanics (Dust of Appearance, Lantern of Revealing)

**Tests:**
- Skill check modifications
- Darkvision range calculations
- Disguise/reveal interaction

---

### **PHASE 6: Storage Items (2 weeks)**
**Goal:** Implement extradimensional storage and container system.

**Items (~10):**
- Bag of Holding Types I–IV
- Handy Haversack
- Efficient Quiver
- Portable Hole
- Folding Boat
- Decanter of Endless Water
- Eversmoking Bottle
- Bottle of Air

**New Systems Required:**

1. **Container System**
   ```
   MagicContainer:
       weight_limit: float (lbs)
       volume_limit: float (cu ft)
       carried_weight: float  # actual weight of container when carried
       is_extradimensional: bool
       contents: List[Item]
       
       add_item(item) → bool  # false if over limit
       remove_item(item) → Item
       get_total_weight() → float
       get_total_volume() → float
   ```

2. **Extradimensional Interaction Rules**
   - Bag of Holding + Bag of Holding → Astral rift (both destroyed)
   - Bag of Holding + Portable Hole → gate to Astral Plane
   - Must check on every insert operation

3. **Retrieval Mechanics**
   - Handy Haversack: move action to retrieve (not standard action)
   - Regular Bag of Holding: standard action to retrieve
   - Efficient Quiver: move action for appropriate compartment

**Tests:**
- Weight/volume limit enforcement
- Actual carried weight calculation
- Extradimensional nesting detection and consequence
- Retrieval action economy

---

### **PHASE 7: Combat Items (3 weeks)**
**Goal:** Implement offensive wondrous items.

**Items (~15):**
- Necklace of Fireballs Types I–VII
- Bead of Force
- Horn of Blasting
- Iron Bands of Binding
- Bracers of Archery (Lesser/Greater)
- Gauntlet of Rust
- Cube of Force

**Implementation:**

1. **Necklace of Fireballs:**
   - Each type has a specific set of beads with damage dice
   - Thrown as ranged touch attack (30 ft range increment)
   - Reflex DC 14 for half damage
   - If necklace is struck by fire damage while wearing → all remaining beads detonate on wearer
   - Single-use beads (consumed on use)

2. **Bead of Force:**
   - Thrown as ranged attack (range 60 ft)
   - 5d6 force damage to target
   - Creates *resilient sphere* around target (Reflex DC 16 negates)
   - Single-use

3. **Horn of Blasting:**
   - Cone attack (100-ft cone)
   - 5d6 sonic damage
   - Fort DC 16 or deafened 2d6 rounds
   - Crystalline objects take 7d6
   - 1/day, 20% chance of explosion if overused

4. **Iron Bands of Binding:**
   - Ranged touch attack (60 ft)
   - Target bound (AC 20, 20 HP, Break DC 30)
   - 1/day

5. **Cube of Force:**
   - 36 charges, regains 1d6/day
   - 6 face modes with different costs per minute
   - Wall of force mechanics around user
   - Deactivation conditions per face

**Tests:**
- Fireball bead damage calculation and AoE
- Chain detonation on fire damage
- Cone attack targeting
- Binding grapple mechanics
- Cube charge tracking

---

### **PHASE 8: Summoning Items (3 weeks)**
**Goal:** Implement creature-summoning items.

**Items (~20 variants):**
- Bag of Tricks (Gray, Rust, Tan)
- Elemental Gems (4 types)
- Figurines of Wondrous Power (9 types)
- Horn of Valhalla (Silver, Brass, Bronze, Iron)

**New Systems Required:**

1. **Summoning System**
   ```
   SummonedCreature:
       creature_type: CreatureType
       stat_block: CreatureStats
       duration: Duration
       summoner: Character
       commands: List[Command]  # what summoner can order
       
       act(round) → Action
       dismiss() → void
       check_duration() → bool
   ```

2. **Figurine Duration Tracking**
   - Each figurine has a maximum usage time per period
   - Bronze Griffon: 6 hours/week
   - Golden Lions: 1 hour/day
   - Marble Elephant: 24 hours/month
   - Must track across rests/sessions

3. **Random Summoning Tables**
   - Bag of Tricks: d% roll → creature type
   - Multiple creatures per bag type

4. **Creature Stat Blocks Required:**
   - Gray Bag: Bat, Rat, Cat, Weasel, Riding Dog
   - Rust Bag: Wolverine, Wolf, Boar, Panther, Giant Wasp
   - Tan Bag: Brown Bear, Lion, Heavy Horse, Tiger, Rhinoceros
   - Elementals: Large Air/Earth/Fire/Water Elemental
   - Figurines: Griffon, Giant Fly, Lion, Goat (3 types), Elephant, Nightmare/Warhorse, Riding Dog, Giant Owl, Raven
   - Valhalla: 2nd–5th level human barbarians

**Tests:**
- Random summoning table distribution
- Duration tracking across time periods
- Summoned creature actions and commands
- Figurine recharge mechanics

---

### **PHASE 9: Ioun Stones (2 weeks)**
**Goal:** Implement the complete Ioun Stone subsystem.

**Items (16 types):**
- See Ioun Stones table above

**New Systems Required:**

1. **Slotless Orbiting System**
   - Stones orbit 1d3 feet from owner's head
   - No equipment slot consumed
   - Multiple stones can be active simultaneously
   - Each type limited to one active stone
   - Stones can be targeted (AC 24, 10 HP, hardness 5)
   - Burned-out stones still orbit but have no effect

2. **Spell Absorption Mechanics**
   - Pale Lavender Ellipsoid: absorbs spells of 4th level or lower, 20 total spell levels
   - Lavender and Green Ellipsoid: absorbs spells of 8th level or lower, 50 total spell levels
   - Once absorption capacity exhausted → becomes dull gray (burned out)

3. **Spell Storage**
   - Vibrant Purple Prism: stores up to 3 spell levels (as ring of spell storing)
   - Requires spellcaster to cast spell into stone
   - Anyone can activate stored spells

**Tests:**
- Multiple Ioun Stone management
- Bonus stacking with worn items
- Spell absorption tracking
- Targeting stones in combat (AC 24)
- Burn-out tracking

---

### **PHASE 10: Special Robes & Complex Items (3 weeks)**
**Goal:** Implement remaining complex multi-ability items.

**Items (~15):**
- Robe of the Archmagi
- Robe of Stars
- Robe of Useful Items
- Robe of Bones
- Robe of Scintillating Colors
- Cloak of Arachnida
- Cloak of the Bat
- Helm of Telepathy
- Rope of Entanglement
- Cube of Frost Resistance
- Mirror items (if in scope)

**Implementation Highlights:**

1. **Robe of Useful Items:**
   - Random patch generation at creation
   - Each patch detached → becomes real item
   - One-time use per patch
   - Need item creation system for detached patches

2. **Robe of Bones:**
   - 12 undead figure patches
   - Each becomes specific undead when detached
   - Need undead stat blocks

3. **Cloak of Arachnida:**
   - Continuous spider climb
   - Immunity to web entanglement
   - Web spell 1/day (DC 14)
   - Three separate effects on one item

4. **Helm of Telepathy:**
   - Detect thoughts at will (DC 13)
   - Suggest 1/day on detected creature (DC 14)
   - Telepathic communication (60 ft, shared language)

**Tests:**
- Multi-ability item activation
- Patch/component tracking (robes)
- Complex conditional effects (Cloak of the Bat)

---

**Total Estimated Time: 23–25 weeks (5–6 months)**

---

## ITEM COUNT SUMMARY

### By Price Category

| Category | Price Range | Count |
|----------|------------|-------|
| Minor | ≤ 15,000 gp | ~80 items |
| Medium | 15,001–60,000 gp | ~50 items |
| **Total Minor+Medium** | **≤ 60,000 gp** | **~130 items** |

### By Equipment Slot

| Slot | Base Items | Including Variants |
|------|-----------|-------------------|
| Feet (Boots) | 8 | 8 |
| Shoulders (Cloaks) | 8 | 15+ |
| Hands (Gloves) | 5 | 7 |
| Head (Headgear) | 8 | 12+ |
| Face (Eyes/Goggles) | 5 | 5 |
| Throat (Neck) | 12 | 30+ |
| Waist (Belt) | 2 | 3 |
| Arms (Bracers) | 3 | 11+ |
| Torso (Body/Robes) | 8 | 8 |
| Slotless | 50+ | 60+ |
| **Total** | **~109** | **~160+** |

### By Complexity Tier

| Tier | Difficulty | Item Count | Est. Time |
|------|-----------|-----------|-----------|
| 1 — Simple Passive | ⭐ | ~45 | 1–2 weeks |
| 2 — Active Single | ⭐⭐ | ~30 | 2–3 weeks |
| 3 — Storage | ⭐⭐⭐ | ~15 | 2 weeks |
| 4 — Combat/Summon | ⭐⭐⭐⭐ | ~25 | 3–4 weeks |
| 5 — Complex Multi | ⭐⭐⭐⭐⭐ | ~15 | 3–4 weeks |
| **Total** | | **~130** | **23–25 weeks** |

---

## NEW SYSTEMS REQUIRED

### SYSTEM 1: Equipment Slot Expansion

**Current slots (from Ring implementation):**
- Ring (Left/Right)
- Held (MainHand/OffHand)

**New slots needed:**
| Slot | D&D 3.5e Name | Items Using Slot |
|------|--------------|-----------------|
| Feet | Boots/Footwear | 8 items |
| Shoulders | Cloak/Cape | 15+ items |
| Hands | Gloves/Gauntlets | 7 items |
| Head | Headband/Helm/Hat | 12+ items |
| Face | Eyes/Goggles/Lenses | 5 items |
| Throat | Amulet/Necklace/Periapt | 30+ items |
| Waist | Belt | 3 items |
| Arms | Bracers/Armbands | 11+ items |
| Torso | Robe/Vestment | 8 items |

**Rules:**
- Only ONE item per slot (except Ring which allows two)
- Slotless items bypass slot restrictions entirely
- Body slot (Torso) conflicts with worn armor in some interpretations — use DMG standard (separate slot)

**Implementation:**
```python
class EquipmentSlot(Enum):
    HEAD = "head"
    FACE = "face"
    THROAT = "throat"
    SHOULDERS = "shoulders"
    TORSO = "torso"
    ARMS = "arms"
    HANDS = "hands"
    WAIST = "waist"
    FEET = "feet"
    RING_LEFT = "ring_left"
    RING_RIGHT = "ring_right"
    HELD_MAIN = "held_main"
    HELD_OFF = "held_off"
    SLOTLESS = None  # No slot restriction
```

---

### SYSTEM 2: Slotless Item Management

**Challenge:** Slotless items (60+ items) have no equipment slot restriction. A character can have multiple slotless items active simultaneously.

**Items affected:**
- Ioun Stones (16 types, multiple can orbit simultaneously)
- Pearls of Power (multiple pearls of different levels allowed)
- Bags of Holding, Haversack, Portable Hole
- All consumables (dusts, elixirs, feather tokens)
- Figurines of Wondrous Power
- Miscellaneous (candles, horns, pipes, ropes, etc.)

**Implementation:**
```python
class SlotlessItemManager:
    active_items: List[WondrousItem]
    ioun_stones: List[IounStone]  # Special subset
    
    add_item(item) → bool
    remove_item(item) → bool
    get_active_effects() → List[Effect]
    
    # Ioun Stones have special rules:
    # - Only one of each type
    # - Can be targeted individually
    # - Orbit visually
```

---

### SYSTEM 3: Container System

**Purpose:** Manage magical containers with weight reduction, volume limits, and extradimensional spaces.

**Core Features:**
- Weight capacity and volume capacity tracking
- Carried weight reduction (bag weighs less than contents)
- Retrieval mechanics (action economy)
- Extradimensional space interaction rules
- Nested container detection and consequences

**Data Model:**
```python
class MagicContainer:
    weight_limit: float    # max weight of contents (lbs)
    volume_limit: float    # max volume of contents (cu ft)
    carried_weight: float  # weight of container when carried
    is_extradimensional: bool
    contents: List[Item]
    
    def add_item(self, item: Item) -> bool:
        if self._would_exceed_limits(item):
            return False
        if self.is_extradimensional and item.is_extradimensional:
            self._trigger_astral_rift()
            return False
        self.contents.append(item)
        return True
    
    def retrieve_item(self, item: Item) -> Tuple[Item, ActionCost]:
        # Haversack: move action
        # Bag of Holding: standard action
        pass
```

---

### SYSTEM 4: Figurine Summoning System

**Purpose:** Handle transformation of figurines into creatures with duration tracking across time periods.

**Core Features:**
- Transform figurine → creature (standard action, command word)
- Duration tracking per usage period (hours/day, hours/week, hours/month)
- Creature stat blocks for all figurine forms
- Command/control mechanics
- Reversion to figurine form
- Cooldown between uses

**Data Model:**
```python
class FigurineOfWondrousPower:
    figurine_type: FigurineType
    creature_form: CreatureStatBlock
    max_duration: Duration         # e.g., 6 hours
    usage_period: TimePeriod       # e.g., per week
    remaining_duration: Duration   # time left in current period
    is_active: bool
    cooldown: Duration             # time before can be used again after reverting
    
    def activate(self) -> Creature:
        if self.remaining_duration <= 0:
            return None
        self.is_active = True
        return self._create_creature()
    
    def deactivate(self):
        self.is_active = False
        # Start cooldown
    
    def tick(self, elapsed: Duration):
        if self.is_active:
            self.remaining_duration -= elapsed
```

---

### SYSTEM 5: Displacement Mechanics

**Purpose:** Handle miss chance effects (blur, displacement, concealment).

**Core Features:**
- Percentile miss chance roll before attack roll
- Minor displacement: 20% miss chance
- Major displacement: 50% miss chance
- Does not stack with other miss chance effects (use highest)
- Negated by *true seeing*, *see invisibility* (for invisibility-based concealment)
- Displacement is NOT negated by see invisibility but IS negated by true seeing

**Implementation:**
```python
class MissChanceEffect:
    chance: int           # 20 or 50
    source: str           # "displacement_minor", "displacement_major", "blur"
    negated_by: List[str] # ["true_seeing"]
    
    def resolve(self, attacker: Creature) -> bool:
        """Returns True if attack misses due to miss chance."""
        if any(attacker.has_effect(neg) for neg in self.negated_by):
            return False
        roll = random.randint(1, 100)
        return roll <= self.chance
```

---

### SYSTEM 6: Charge Tracking System

**Purpose:** Track charges for items that have limited uses before depletion.

**Items Using Charges:**
- Cube of Force (36 charges, regain 1d6/day)
- Gem of Brightness (50 charges)
- Chime of Opening (10 charges)
- Brooch of Shielding (101 HP of absorption)
- Necklace of Fireballs (varies by type, single-use beads)

**Implementation:**
```python
class ChargeTracker:
    max_charges: int
    current_charges: int
    recharge_rate: Optional[DiceRoll]  # e.g., 1d6/day
    recharge_period: Optional[TimePeriod]
    destroy_on_empty: bool  # True for most consumables
    
    def use_charges(self, amount: int) -> bool:
        if self.current_charges < amount:
            return False
        self.current_charges -= amount
        if self.current_charges <= 0 and self.destroy_on_empty:
            self._destroy_item()
        return True
    
    def recharge(self):
        if self.recharge_rate:
            gained = self.recharge_rate.roll()
            self.current_charges = min(self.max_charges, self.current_charges + gained)
```

---

### SYSTEM 7: Daily Use Tracking

**Purpose:** Track uses-per-day for activated items.

**Items Using Daily Limits:**
- Boots of Speed (10 rounds/day)
- Winged Boots (3 uses of 5 min/day)
- Eyes of Charming (3/day)
- Boots of Teleportation (3/day)
- Cloak of Etherealness (10 min total/day)
- Helm of Telepathy: Suggest (1/day)
- Horn of Goodness/Evil (1/day)

**Implementation:**
```python
class DailyUseTracker:
    max_uses: int          # -1 for at-will
    uses_remaining: int
    duration_per_use: Optional[Duration]  # e.g., 5 minutes for Winged Boots
    total_duration_remaining: Optional[Duration]  # for items tracked by total time
    
    def can_use(self) -> bool:
        if self.max_uses == -1:
            return True
        return self.uses_remaining > 0
    
    def use(self) -> bool:
        if not self.can_use():
            return False
        self.uses_remaining -= 1
        return True
    
    def reset_daily(self):
        self.uses_remaining = self.max_uses
        if self.total_duration_remaining is not None:
            self.total_duration_remaining = self.max_total_duration
```

---

## CROSS-REFERENCES WITH EXISTING SYSTEMS

### Ring System Integration
- **Equipment slots:** Wondrous items use different slots than rings (no conflict)
- **Bonus stacking:** Same stacking rules apply — enhancement bonuses don't stack, different types do
- **Activation framework:** Rings already have command word / continuous / use-activated — reuse for wondrous items
- **Database:** Extend existing magic item tables

### Rod System Integration
- **Held items:** Rods use Held slots, wondrous items use worn slots (no conflict)
- **Charge system:** Reuse Rod charge tracking for Cube of Force, Gem of Brightness, etc.
- **Daily use tracking:** Reuse any daily-use system from rods

### Armor/Weapon System Integration
- **Bracers of Armor:** Armor bonus → same type as worn armor, doesn't stack
- **Amulet of Natural Armor:** Enhancement to natural armor → interacts with creature natural armor
- **Amulet of Mighty Fists:** Enhancement to unarmed/natural attacks → needs weapon system hooks

---

## TESTING STRATEGY

### Unit Tests (per item)
- Equip/unequip correctly applies/removes bonuses
- Bonus stacking rules enforced
- Activation cost (action economy) correct
- Duration tracking accurate
- Charge/use tracking accurate
- Slot conflict detection works

### Integration Tests
- Multiple wondrous items worn simultaneously
- Wondrous items + rings + armor + weapons
- Extradimensional container interactions
- Summoned creature management
- Combat with displacement mechanics

### Edge Cases
- Wearing two items that enhance same ability score
- Placing Bag of Holding inside Portable Hole
- Using Pearl of Power without having cast a spell
- Ioun Stone targeted while multiple orbit
- Figurine duration exhausted mid-combat
- Cube of Force deactivation conditions
- Necklace of Fireballs fire damage while wearing

---

## RISK ASSESSMENT

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Scope creep (130+ items) | High | Strict tier-based implementation, defer Major items |
| Summoning complexity | Medium | Simplified creature AI, predetermined actions |
| Container system edge cases | Medium | Extensive testing, clear rules documentation |
| Bonus stacking bugs | High | Centralized bonus management system, comprehensive tests |
| Equipment slot conflicts | Low | Clear slot validation, one-item-per-slot enforcement |
| Duration tracking across sessions | Medium | Persistent state, clear reset rules |
| Performance with many active effects | Medium | Efficient effect aggregation, lazy recalculation |

---

## APPENDIX: ITEMS DEFERRED TO MAJOR CATEGORY

The following items exceed 60,000 gp and are classified as Major items, deferred to a future implementation phase:

| Item | Price | Reason for Deferral |
|------|-------|-------------------|
| Robe of the Archmagi | 75,000 gp | Complex multi-ability, alignment-restricted |
| Robe of Eyes | 120,000 gp | Multiple vision modes + vulnerability |
| Gem of Seeing | 75,000 gp | True seeing mechanics |
| Helm of Teleportation | 73,500 gp | Teleportation system |
| Helm of Brilliance | 125,000 gp | Multiple gem-based spell effects |
| Mantle of Faith | 76,000 gp | DR mechanics |
| Mantle of Spell Resistance | 90,000 gp | SR mechanics |
| Pearl of Power (9th) | 81,000 gp | High-level spell recall |
| Mirror of Life Trapping | 200,000 gp | Complex trapping mechanics |
| Mirror of Mental Prowess | 175,000 gp | Multiple divination abilities |
| Mirror of Opposition | 92,000 gp | Creature duplication |
| Efreeti Bottle | 145,000 gp | Wish mechanics |
| Iron Flask | 170,000 gp | Outsider imprisonment |
| Well of Many Worlds | 82,000 gp | Random planar travel |
| Stone of Controlling Earth Elementals | 100,000 gp | Elemental control |
| Horn of Valhalla (Iron) | 75,000 gp | High-level summoning |
| Bracers of Armor +8 | 64,000 gp | Edge case pricing |

> **Note:** Some items listed as Medium in the random generation tables may have variants that cross into Major pricing. Implementation should handle the Minor/Medium variants and note the Major variants for future work.

---

*Document created: May 2026*  
*Next steps: Begin Phase 1 (Foundation) after Rod implementation sprint.*
