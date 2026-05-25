# D&D 3.5e Wondrous Items — Organized by Equipment Slot

> **Source:** Dungeon Master's Guide 3.5e, Pages 246–265  
> **Companion to:** `wondrous_items_implementation_plan.md`  
> **Date:** May 2026

---

## SLOT SUMMARY

| Slot | Base Items | Variants | Total | Price Range |
|------|-----------|----------|-------|-------------|
| Feet (Boots) | 8 | 0 | 8 | 2,500–49,000 gp |
| Shoulders (Cloaks) | 8 | 7 | 15 | 1,000–55,000 gp |
| Hands (Gloves) | 5 | 2 | 7 | 4,000–36,000 gp |
| Head | 8 | 4 | 12 | 1,800–73,500 gp |
| Face | 5 | 0 | 5 | 1,250–56,000 gp |
| Throat (Neck) | 14 | 16 | 30 | 1,000–120,000 gp |
| Waist (Belt) | 2 | 1 | 3 | 13,000–36,000 gp |
| Arms (Bracers) | 3 | 8 | 11 | 1,000–64,000 gp |
| Torso (Body) | 8 | 0 | 8 | 2,400–120,000 gp |
| Slotless | 55+ | 20+ | 75+ | 50–200,000 gp |
| **Grand Total** | **~116** | **~58** | **~174+** | |

---

## FEET (BOOTS & FOOTWEAR)

**Slot Rule:** One pair of footwear at a time.

### Minor Items (≤ 15,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Boots of Elvenkind | 2,500 gp | +5 competence Move Silently | Continuous |
| Boots of the Winterlands | 2,500 gp | Endure cold, +10 speed in snow, no tracks on snow, walk on ice | Continuous |
| Slippers of Spider Climbing | 4,800 gp | Spider climb at will (half speed) | Continuous |
| Boots of Striding and Springing | 5,500 gp | +10 ft enhancement to land speed, +5 competence Jump | Continuous |
| Boots of Levitation | 7,500 gp | Levitate at will (self only) | Command word |
| Boots of Speed | 12,000 gp | Haste, 10 rounds/day (not consecutive) | Free action |

### Medium Items (15,001–60,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Winged Boots | 16,000 gp | Fly 3/day, 5 min each (60 ft, good) | Command word |
| Boots of Teleportation | 49,000 gp | Teleport 3/day (self + 50 lbs) | Command word |

### Implementation Notes
- All boots are straightforward: continuous bonuses or daily-use spell effects.
- Boots of Speed requires round-by-round tracking (10 rounds total, free action toggle).
- Boots of the Winterlands has conditional speed bonus (snow terrain only).
- Slippers of Spider Climbing grants special movement mode at half speed.

---

## SHOULDERS (CLOAKS & CAPES)

**Slot Rule:** One shoulder item at a time.

### Minor Items (≤ 15,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Cloak of Resistance +1 | 1,000 gp | +1 resistance to all saves | Continuous |
| Cloak of Elvenkind | 2,500 gp | +5 competence Hide | Continuous |
| Cloak of Resistance +2 | 4,000 gp | +2 resistance to all saves | Continuous |
| Cloak of Charisma +2 | 4,000 gp | +2 enhancement to Charisma | Continuous |
| Cloak of Resistance +3 | 9,000 gp | +3 resistance to all saves | Continuous |
| Cloak of Arachnida | 14,000 gp | Spider climb, web immunity, web 1/day (DC 14) | Mixed |

### Medium Items (15,001–60,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Cloak of Charisma +4 | 16,000 gp | +4 enhancement to Charisma | Continuous |
| Cloak of Resistance +4 | 16,000 gp | +4 resistance to all saves | Continuous |
| Cloak of Displacement, Minor | 24,000 gp | 20% miss chance (blur) | Continuous |
| Cloak of Resistance +5 | 25,000 gp | +5 resistance to all saves | Continuous |
| Cloak of the Bat | 26,000 gp | +5 Hide, fly in dim light, polymorph bat at night | Mixed |
| Cloak of Charisma +6 | 36,000 gp | +6 enhancement to Charisma | Continuous |
| Cloak of Displacement, Major | 50,000 gp | 50% miss chance (displacement) | Continuous |
| Cloak of Etherealness | 55,000 gp | Ethereal jaunt 10 min/day total | Free action |
| Wings of Flying | 54,000 gp | Fly at will (60 ft, good) | Command word |

### Implementation Notes
- Cloak of Resistance and Cloak of Charisma are simple passive bonuses (Tier 1).
- Cloak of Arachnida combines three effects — continuous spider climb, passive web immunity, and 1/day web spell.
- Cloak of the Bat has conditional abilities — fly only in dim light, polymorph only outdoors at night.
- Displacement cloaks require miss chance resolution system (new mechanic).
- Cloak of Etherealness tracks total minutes used per day.

---

## HANDS (GLOVES & GAUNTLETS)

**Slot Rule:** One pair of gloves/gauntlets at a time.

### Minor Items (≤ 15,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Gauntlets of Ogre Power | 4,000 gp | +2 enhancement to Strength | Continuous |
| Gloves of Arrow Snaring | 4,000 gp | Snatch arrows 1/round (as Snatch Arrows feat) | Immediate |
| Gloves of Dexterity +2 | 4,000 gp | +2 enhancement to Dexterity | Continuous |
| Gloves of Swimming and Climbing | 6,250 gp | +5 competence Swim and Climb | Continuous |

### Medium Items (15,001–60,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Gauntlet of Rust | 11,500 gp | Rusting grasp (destroy metal on touch) | Standard action |
| Gloves of Dexterity +4 | 16,000 gp | +4 enhancement to Dexterity | Continuous |
| Gloves of Dexterity +6 | 36,000 gp | +6 enhancement to Dexterity | Continuous |

### Implementation Notes
- Most are simple passive bonuses (Tier 1).
- Gloves of Arrow Snaring requires reaction/immediate action system.
- Gauntlet of Rust is single gauntlet (left hand only), requires metal detection for targeting.

---

## HEAD (Headbands, Helms, Hats, Circlets)

**Slot Rule:** One headgear item at a time. Separate from Face slot.

### Minor Items (≤ 15,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Hat of Disguise | 1,800 gp | Disguise self at will | Standard action |
| Headband of Intellect +2 | 4,000 gp | +2 enhancement to Intelligence | Continuous |
| Circlet of Persuasion | 4,500 gp | +3 competence Charisma-based checks | Continuous |
| Helm of Comprehend Languages and Read Magic | 5,200 gp | Comprehend languages + read magic continuously | Continuous |

### Medium Items (15,001–60,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Headband of Intellect +4 | 16,000 gp | +4 enhancement to Intelligence | Continuous |
| Helm of Underwater Action | 24,000 gp | Breathe underwater, clear vision, swim 30 ft | Continuous |
| Helm of Telepathy | 27,000 gp | Detect thoughts at will, suggest 1/day, telepathy 60 ft | Mixed |
| Headband of Intellect +6 | 36,000 gp | +6 enhancement to Intelligence | Continuous |

### Major Items (> 60,000 gp — deferred)

| Item | Price | Effect |
|------|-------|--------|
| Helm of Teleportation | 73,500 gp | Teleport 3/day |

### Implementation Notes
- Headband of Intellect is simple passive (Tier 1).
- Helm of Telepathy combines three effects (Tier 5) — at-will detect thoughts, 1/day suggest, passive telepathy.
- Circlet of Persuasion applies to ALL Charisma-based checks (Bluff, Diplomacy, Disguise, Gather Information, Handle Animal, Intimidate, Perform, Use Magic Device).

---

## FACE (Eyes, Goggles, Lenses)

**Slot Rule:** One face item at a time. Separate from Head slot.

### Minor Items (≤ 15,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Goggles of Minute Seeing | 1,250 gp | +5 competence Search | Continuous |
| Eyes of the Eagle | 2,500 gp | +5 competence Spot | Continuous |
| Goggles of Night | 2,500 gp | Darkvision 60 ft (extends by 60 ft if already has) | Continuous |
| Lens of Detection | 3,500 gp | +5 competence Search, +5 Survival (tracking) | Continuous |

### Medium Items (15,001–60,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Eyes of Charming | 56,000 gp | Charm person 3/day (DC 16), eye contact required | Standard action |

### Implementation Notes
- Most face items are simple skill bonuses (Tier 1).
- Goggles of Night grants darkvision mode — system needs vision mode tracking.
- Eyes of Charming requires line of sight / eye contact mechanic.

---

## THROAT (Amulets, Necklaces, Periapts, Phylacteries)

**Slot Rule:** One throat item at a time. This is the most populated worn slot.

### Minor Items (≤ 15,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Phylactery of Faithfulness | 1,000 gp | Warns cleric of alignment-violating actions | Continuous |
| Brooch of Shielding | 1,500 gp | Absorbs magic missiles (101 HP total) | Continuous |
| Necklace of Fireballs Type I | 1,650 gp | 1 bead (5d6) | Standard (throw) |
| Amulet of Natural Armor +1 | 2,000 gp | +1 enhancement to natural armor AC | Continuous |
| Necklace of Fireballs Type II | 2,700 gp | 2 beads (5d6, 3d6) | Standard (throw) |
| Amulet of Health +2 | 4,000 gp | +2 enhancement to Constitution | Continuous |
| Periapt of Wisdom +2 | 4,000 gp | +2 enhancement to Wisdom | Continuous |
| Necklace of Fireballs Type III | 4,350 gp | 3 beads (5d6, 3d6, 3d6) | Standard (throw) |
| Necklace of Fireballs Type IV | 5,400 gp | 3 beads (7d6, 5d6, 3d6) | Standard (throw) |
| Necklace of Fireballs Type V | 5,850 gp | 4 beads (7d6, 5d6, 3d6, 3d6) | Standard (throw) |
| Amulet of Mighty Fists +1 | 6,000 gp | +1 enhancement to unarmed/natural attacks | Continuous |
| Periapt of Health | 7,500 gp | Immunity to all disease | Continuous |
| Amulet of Natural Armor +2 | 8,000 gp | +2 enhancement to natural armor AC | Continuous |
| Necklace of Fireballs Type VI | 8,100 gp | 4 beads (9d6, 5d6, 5d6, 3d6) | Standard (throw) |
| Necklace of Fireballs Type VII | 8,700 gp | 5 beads (9d6, 7d6, 5d6, 3d6, 3d6) | Standard (throw) |
| Necklace of Adaptation | 9,000 gp | Breathe in any environment | Continuous |

### Medium Items (15,001–60,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Phylactery of Undead Turning | 11,000 gp | +4 turning check and turning damage | Continuous |
| Amulet of Health +4 | 16,000 gp | +4 enhancement to Constitution | Continuous |
| Periapt of Wisdom +4 | 16,000 gp | +4 enhancement to Wisdom | Continuous |
| Amulet of Natural Armor +3 | 18,000 gp | +3 enhancement to natural armor AC | Continuous |
| Amulet of Mighty Fists +2 | 24,000 gp | +2 enhancement to unarmed/natural attacks | Continuous |
| Periapt of Proof Against Poison | 27,000 gp | Immunity to all poison | Continuous |
| Amulet of Natural Armor +4 | 32,000 gp | +4 enhancement to natural armor AC | Continuous |
| Amulet of Proof Against Detection | 35,000 gp | Immune to divination spells targeting wearer | Continuous |
| Amulet of Health +6 | 36,000 gp | +6 enhancement to Constitution | Continuous |
| Periapt of Wisdom +6 | 36,000 gp | +6 enhancement to Wisdom | Continuous |
| Scarab of Protection | 38,000 gp | +3 resistance saves, absorb 12 death/drain effects | Continuous |
| Amulet of Natural Armor +5 | 50,000 gp | +5 enhancement to natural armor AC | Continuous |
| Amulet of Mighty Fists +3 | 54,000 gp | +3 enhancement to unarmed/natural attacks | Continuous |

### Implementation Notes
- Throat slot has the most items — 30+ variants.
- Necklace of Fireballs is a consumable: individual beads are used up. Fire damage to wearing character causes all remaining beads to detonate simultaneously.
- Ability score items (Amulet of Health, Periapt of Wisdom) are simple passive (Tier 1).
- Scarab of Protection has dual function: passive save bonus + charge-based death ward.

---

## WAIST (BELTS)

**Slot Rule:** One belt/waist item at a time.

### Medium Items (15,001–60,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Monk's Belt | 13,000 gp | AC bonus (Wis + level/5), improved unarmed damage. +5 monk levels if monk. | Continuous |
| Belt of Giant Strength +4 | 16,000 gp | +4 enhancement to Strength | Continuous |
| Belt of Giant Strength +6 | 36,000 gp | +6 enhancement to Strength | Continuous |

### Implementation Notes
- No minor waist items in DMG.
- Belt of Giant Strength is simple passive (Tier 1).
- Monk's Belt is complex (Tier 5) — grants monk-like abilities to non-monks, enhances monks by +5 effective levels. Requires monk class feature awareness.

---

## ARMS (BRACERS & ARMBANDS)

**Slot Rule:** One pair of bracers at a time.

### Minor Items (≤ 15,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Bracers of Armor +1 | 1,000 gp | +1 armor bonus to AC | Continuous |
| Bracers of Armor +2 | 4,000 gp | +2 armor bonus to AC | Continuous |
| Bracers of Archery, Lesser | 5,000 gp | +1 competence attack with bows, bow proficiency | Continuous |
| Bracers of Armor +3 | 9,000 gp | +3 armor bonus to AC | Continuous |

### Medium Items (15,001–60,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Bracers of Armor +4 | 16,000 gp | +4 armor bonus to AC | Continuous |
| Bracers of Armor +5 | 25,000 gp | +5 armor bonus to AC | Continuous |
| Bracers of Archery, Greater | 25,000 gp | +2 competence attack, +1 damage with bows, bow proficiency | Continuous |
| Bracers of Armor +6 | 36,000 gp | +6 armor bonus to AC | Continuous |
| Bracers of Armor +7 | 49,000 gp | +7 armor bonus to AC | Continuous |
| Bracers of Armor +8 | 64,000 gp | +8 armor bonus to AC | Continuous |

### Implementation Notes
- Bracers of Armor provide armor bonus — does NOT stack with worn armor (use higher).
- Bracers of Armor +3 or higher can accept armor special abilities (additional cost).
- Bracers of Archery grant proficiency with all bows — needs feat/proficiency system hook.

---

## TORSO (BODY — Robes & Vestments)

**Slot Rule:** One body item at a time. Worn over or instead of armor.

### Minor Items (≤ 15,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Robe of Bones | 2,400 gp | 12 undead patches, each becomes undead when detached | Standard (detach) |
| Druid's Vestment | 3,750 gp | +1 extra wild shape/day | Continuous |
| Robe of Useful Items | 7,000 gp | Patches become real items when detached | Standard (detach) |

### Medium Items (15,001–60,000 gp)

| Item | Price | Effect | Activation |
|------|-------|--------|-----------|
| Robe of Blending | 8,400 gp | Disguise self at will | Standard action |
| Robe of Scintillating Colors | 27,000 gp | Hypnotic pattern 30-ft radius, 10 rounds/day | Standard action |
| Robe of Stars | 58,000 gp | +1 luck saves, 6 star missiles (5d4 each), astral projection 1/day | Mixed |

### Major Items (> 60,000 gp — deferred)

| Item | Price | Effect |
|------|-------|--------|
| Robe of the Archmagi | 75,000 gp | +5 AC, SR 18, +4 saves, 0% arcane failure |
| Robe of Eyes | 120,000 gp | 360° vision, see invisible, darkvision 120 ft |

### Implementation Notes
- Robe of Bones and Robe of Useful Items use a "patch" system — limited-use components that are individually tracked and consumed.
- Robe of Stars has three separate abilities: passive save bonus, consumable star missiles, and 1/day astral projection.
- Druid's Vestment requires class feature interaction (wild shape).
- Body slot is separate from armor slot in standard interpretation.

---

## SLOTLESS (NO EQUIPMENT SLOT)

**Slot Rule:** No slot required. Multiple slotless items can be active simultaneously.

### Sub-categories:

#### Containers (7 items)
| Item | Price | Category |
|------|-------|----------|
| Efficient Quiver | 1,800 gp | Minor |
| Handy Haversack | 2,000 gp | Minor |
| Bag of Holding Type I | 2,500 gp | Minor |
| Bag of Holding Type II | 5,000 gp | Minor |
| Bag of Holding Type III | 7,400 gp | Minor |
| Bag of Holding Type IV | 10,000 gp | Minor |
| Portable Hole | 20,000 gp | Medium |

#### Consumables — Dusts (5 items)
| Item | Price | Category |
|------|-------|----------|
| Dust of Tracelessness | 250 gp | Minor |
| Dust of Dryness | 850 gp | Minor |
| Dust of Illusion | 1,200 gp | Minor |
| Dust of Appearance | 1,800 gp | Minor |
| Dust of Disappearance | 3,500 gp | Minor |

#### Consumables — Elixirs (7 items)
| Item | Price | Category |
|------|-------|----------|
| Elixir of Love | 150 gp | Minor |
| Elixir of Hiding | 250 gp | Minor |
| Elixir of Swimming | 250 gp | Minor |
| Elixir of Tumbling | 250 gp | Minor |
| Elixir of Vision | 250 gp | Minor |
| Elixir of Truth | 500 gp | Minor |
| Elixir of Fire Breath | 1,100 gp | Minor |

#### Consumables — Feather Tokens (6 items)
| Item | Price | Category |
|------|-------|----------|
| Anchor | 50 gp | Minor |
| Fan | 200 gp | Minor |
| Bird | 300 gp | Minor |
| Tree | 400 gp | Minor |
| Swan Boat | 450 gp | Minor |
| Whip | 500 gp | Minor |

#### Consumables — Other (8 items)
| Item | Price | Category |
|------|-------|----------|
| Universal Solvent | 50 gp | Minor |
| Unguent of Timelessness | 150 gp | Minor |
| Silversheen | 250 gp | Minor |
| Salve of Slipperiness | 1,000 gp | Minor |
| Sovereign Glue | 2,400 gp | Minor |
| Restorative Ointment | 4,000 gp | Minor |
| Stone Salve | 4,000 gp | Minor |
| Incense of Meditation | 4,900 gp | Minor |

#### Ioun Stones (16 items)
| Item | Price | Category |
|------|-------|----------|
| Clear Spindle | 4,000 gp | Minor |
| Dusty Rose Prism | 5,000 gp | Medium |
| Deep Red Sphere | 8,000 gp | Medium |
| Incandescent Blue Sphere | 8,000 gp | Medium |
| Pale Blue Rhomboid | 8,000 gp | Medium |
| Pink Rhomboid | 8,000 gp | Medium |
| Pink and Green Sphere | 8,000 gp | Medium |
| Scarlet and Blue Sphere | 8,000 gp | Medium |
| Dark Blue Rhomboid | 10,000 gp | Medium |
| Iridescent Spindle | 18,000 gp | Medium |
| Pale Lavender Ellipsoid | 20,000 gp | Medium |
| Pearly White Spindle | 20,000 gp | Medium |
| Orange Prism | 30,000 gp | Medium |
| Pale Green Prism | 30,000 gp | Medium |
| Vibrant Purple Prism | 36,000 gp | Medium |
| Lavender and Green Ellipsoid | 40,000 gp | Medium |

#### Pearls of Power (9 items)
| Item | Price | Category |
|------|-------|----------|
| Pearl of Power (1st) | 1,000 gp | Minor |
| Pearl of Power (2nd) | 4,000 gp | Minor |
| Pearl of Power (3rd) | 9,000 gp | Minor |
| Pearl of Power (4th) | 16,000 gp | Medium |
| Pearl of Power (5th) | 25,000 gp | Medium |
| Pearl of Power (6th) | 36,000 gp | Medium |
| Pearl of Power (7th) | 49,000 gp | Medium |
| Pearl of Power (8th) | 64,000 gp | Medium |
| Pearl of Power (9th) | 81,000 gp | Major |

#### Summoning Items (16+ items)
| Item | Price | Category |
|------|-------|----------|
| Bag of Tricks (Gray) | 900 gp | Minor |
| Elemental Gem (×4) | 2,250 gp each | Minor |
| Bag of Tricks (Rust) | 3,000 gp | Minor |
| Figurine — Silver Raven | 3,800 gp | Minor |
| Bag of Tricks (Tan) | 6,900 gp | Minor |
| Figurine — Serpentine Owl | 9,100 gp | Minor |
| Figurine — Bronze Griffon | 10,000 gp | Medium |
| Figurine — Ebony Fly | 10,000 gp | Medium |
| Figurine — Onyx Dog | 15,500 gp | Medium |
| Figurine — Golden Lions | 16,500 gp | Medium |
| Figurine — Marble Elephant | 17,000 gp | Medium |
| Figurine — Ivory Goats | 21,000 gp | Medium |
| Figurine — Obsidian Steed | 28,500 gp | Medium |
| Horn of Valhalla (Brass) | 34,000 gp | Medium |
| Horn of Valhalla (Bronze) | 40,000 gp | Medium |
| Horn of Valhalla (Silver) | 50,000 gp | Medium |

#### Utility & Miscellaneous (20+ items)
| Item | Price | Category |
|------|-------|----------|
| Hand of the Mage | 900 gp | Minor |
| Pipes of the Sewers | 1,150 gp | Minor |
| Pipes of Sounding | 1,800 gp | Minor |
| Horn of Fog | 2,000 gp | Minor |
| Candle of Truth | 2,500 gp | Minor |
| Golembane Scarab | 2,500 gp | Minor |
| Stone of Alarm | 2,700 gp | Minor |
| Bead of Force | 3,000 gp | Minor |
| Chime of Opening | 3,000 gp | Minor |
| Horseshoes of Speed | 3,000 gp | Minor |
| Rope of Climbing | 3,000 gp | Minor |
| Eversmoking Bottle | 5,400 gp | Minor |
| Sustaining Spoon | 5,400 gp | Minor |
| Wind Fan | 5,500 gp | Minor |
| Horseshoes of a Zephyr | 6,000 gp | Minor |
| Horn of Goodness/Evil | 6,500 gp | Minor |
| Pipes of Haunting | 6,500 gp | Minor |
| Bottle of Air | 7,250 gp | Minor |
| Folding Boat | 7,200 gp | Minor |
| Decanter of Endless Water | 9,000 gp | Minor |
| Pipes of Pain | 12,000 gp | Medium |
| Gem of Brightness | 13,000 gp | Medium |
| Lyre of Building | 13,000 gp | Medium |
| Carpet of Flying (5×5) | 20,000 gp | Medium |
| Horn of Blasting | 20,000 gp | Medium |
| Stone of Good Luck | 20,000 gp | Medium |
| Portable Hole | 20,000 gp | Medium |
| Rope of Entanglement | 21,000 gp | Medium |
| Mattock of the Titans | 23,348 gp | Medium |
| Iron Bands of Binding | 26,000 gp | Medium |
| Cube of Frost Resistance | 27,000 gp | Medium |
| Lantern of Revealing | 30,000 gp | Medium |
| Carpet of Flying (5×10) | 35,000 gp | Medium |
| Carpet of Flying (10×10) | 60,000 gp | Medium |
| Cube of Force | 62,000 gp | Medium |

---

## SLOT CONFLICT MATRIX

Items sharing the same slot cannot be worn simultaneously. This table shows the most common conflicts players will encounter:

| Slot | Common Conflict |
|------|----------------|
| Shoulders | Cloak of Resistance vs Cloak of Charisma vs Cloak of Displacement vs Wings of Flying |
| Throat | Amulet of Natural Armor vs Amulet of Health vs Periapt of Wisdom vs Necklace of Fireballs |
| Hands | Gauntlets of Ogre Power vs Gloves of Dexterity vs Gloves of Arrow Snaring |
| Head | Headband of Intellect vs Hat of Disguise vs Helm of Telepathy vs Circlet of Persuasion |
| Feet | Boots of Speed vs Boots of Elvenkind vs Winged Boots vs Slippers of Spider Climbing |
| Arms | Bracers of Armor vs Bracers of Archery |
| Torso | Robe of Stars vs Robe of Useful Items vs Druid's Vestment |
| Waist | Belt of Giant Strength vs Monk's Belt |
| Face | Eyes of the Eagle vs Goggles of Night vs Eyes of Charming |

**Resolution:** Player must choose one item per slot. Upgrading to a combined item (custom creation rules, DMG p. 288) costs 1.5× the cheaper item's price added to the more expensive item.

---

*Document created: May 2026*
