# D&D 3.5e Wondrous Items — Priority Matrix

> **Source:** Dungeon Master's Guide 3.5e, Pages 246–265  
> **Companion to:** `wondrous_items_implementation_plan.md`  
> **Date:** May 2026

---

## PRIORITY SCORING METHODOLOGY

Each item is scored on four dimensions (1–5 scale each):

| Dimension | Weight | Description |
|-----------|--------|-------------|
| **Player Demand** | 30% | How frequently players use this item |
| **System Reusability** | 25% | How much shared infrastructure the item contributes |
| **Implementation Ease** | 25% | How straightforward to implement (5 = easy) |
| **Game Impact** | 20% | How significantly the item affects gameplay |

**Priority Score = (Demand × 0.30) + (Reusability × 0.25) + (Ease × 0.25) + (Impact × 0.20)**

**Priority Bands:**
- 🔴 **P1 Critical** (4.0–5.0): Must-have, implement first
- 🟡 **P2 Important** (3.0–3.9): High value, implement in early phases
- 🟢 **P3 Standard** (2.0–2.9): Implement when capacity allows
- ⚪ **P4 Low** (1.0–1.9): Implement last or defer

---

## PRIORITY MATRIX BY PHASE

### 🔴 PHASE 1: FOUNDATION (Weeks 1–2) — Priority: CRITICAL

**No individual items — infrastructure only.**

| System Component | Reusability | Items Enabled | Priority |
|-----------------|-------------|---------------|----------|
| Equipment Slot System | 5 | All worn items (~70) | 🔴 P1 |
| WondrousItem Base Class | 5 | All wondrous items (~130) | 🔴 P1 |
| Bonus Management System | 5 | All passive items (~45) | 🔴 P1 |
| Activation Framework | 5 | All active items (~50) | 🔴 P1 |
| Daily Use Tracker | 4 | ~20 items | 🔴 P1 |
| Database Schema | 5 | All items | 🔴 P1 |

---

### 🔴 PHASE 2: ABILITY SCORE ITEMS (Weeks 3–4) — Priority: CRITICAL

| Item | Demand | Reuse | Ease | Impact | Score | Priority |
|------|--------|-------|------|--------|-------|----------|
| Headband of Intellect +2 | 5 | 5 | 5 | 4 | 4.80 | 🔴 P1 |
| Cloak of Charisma +2 | 5 | 5 | 5 | 4 | 4.80 | 🔴 P1 |
| Gauntlets of Ogre Power | 5 | 5 | 5 | 4 | 4.80 | 🔴 P1 |
| Gloves of Dexterity +2 | 5 | 5 | 5 | 4 | 4.80 | 🔴 P1 |
| Amulet of Health +2 | 5 | 5 | 5 | 4 | 4.80 | 🔴 P1 |
| Periapt of Wisdom +2 | 5 | 5 | 5 | 4 | 4.80 | 🔴 P1 |
| Belt of Giant Strength +4 | 5 | 4 | 5 | 4 | 4.55 | 🔴 P1 |
| Gloves of Dexterity +4 | 5 | 4 | 5 | 4 | 4.55 | 🔴 P1 |
| Amulet of Health +4 | 5 | 4 | 5 | 4 | 4.55 | 🔴 P1 |
| Headband of Intellect +4 | 5 | 4 | 5 | 4 | 4.55 | 🔴 P1 |
| Periapt of Wisdom +4 | 5 | 4 | 5 | 4 | 4.55 | 🔴 P1 |
| Cloak of Charisma +4 | 5 | 4 | 5 | 4 | 4.55 | 🔴 P1 |
| +6 variants (×6) | 4 | 3 | 5 | 4 | 3.95 | 🟡 P2 |

**Rationale:** Every character in D&D 3.5e uses ability score items. The "Big Six" (+2 enhancement items at 4,000 gp each) are the most commonly purchased magic items in the game. The template pattern established here serves all 18 variants.

---

### 🔴 PHASE 3: AC & SAVE ITEMS (Weeks 5–6) — Priority: CRITICAL

| Item | Demand | Reuse | Ease | Impact | Score | Priority |
|------|--------|-------|------|--------|-------|----------|
| Cloak of Resistance +1 | 5 | 5 | 5 | 5 | 5.00 | 🔴 P1 |
| Cloak of Resistance +2 | 5 | 4 | 5 | 5 | 4.75 | 🔴 P1 |
| Cloak of Resistance +3 | 5 | 4 | 5 | 5 | 4.75 | 🔴 P1 |
| Amulet of Natural Armor +1 | 5 | 4 | 5 | 5 | 4.75 | 🔴 P1 |
| Amulet of Natural Armor +2 | 5 | 4 | 5 | 5 | 4.75 | 🔴 P1 |
| Bracers of Armor +1 | 5 | 4 | 5 | 5 | 4.75 | 🔴 P1 |
| Bracers of Armor +2 | 5 | 4 | 5 | 5 | 4.75 | 🔴 P1 |
| Bracers of Armor +3–+5 | 4 | 3 | 5 | 4 | 3.95 | 🟡 P2 |
| Bracers of Armor +6–+8 | 3 | 3 | 5 | 4 | 3.65 | 🟡 P2 |
| Cloak of Resistance +4–+5 | 4 | 3 | 5 | 5 | 4.20 | 🔴 P1 |
| Amulet of Natural Armor +3–+5 | 4 | 3 | 5 | 5 | 4.20 | 🔴 P1 |
| Cloak of Displacement, Minor | 4 | 4 | 3 | 5 | 3.95 | 🟡 P2 |
| Cloak of Displacement, Major | 3 | 3 | 3 | 5 | 3.40 | 🟡 P2 |

**Rationale:** Cloak of Resistance is THE most universal magic item in 3.5e — literally every optimized character owns one. Natural armor amulets and bracers of armor are the next most common defensive items.

---

### 🟡 PHASE 4: MOVEMENT ITEMS (Weeks 7–8) — Priority: IMPORTANT

| Item | Demand | Reuse | Ease | Impact | Score | Priority |
|------|--------|-------|------|--------|-------|----------|
| Boots of Speed | 5 | 3 | 3 | 5 | 4.00 | 🔴 P1 |
| Boots of Elvenkind | 4 | 3 | 5 | 3 | 3.75 | 🟡 P2 |
| Boots of Striding and Springing | 4 | 3 | 5 | 3 | 3.75 | 🟡 P2 |
| Winged Boots | 4 | 4 | 3 | 4 | 3.75 | 🟡 P2 |
| Wings of Flying | 3 | 3 | 3 | 4 | 3.20 | 🟡 P2 |
| Boots of Levitation | 3 | 3 | 4 | 3 | 3.25 | 🟡 P2 |
| Slippers of Spider Climbing | 3 | 3 | 4 | 3 | 3.25 | 🟡 P2 |
| Boots of the Winterlands | 2 | 2 | 4 | 2 | 2.50 | 🟢 P3 |
| Boots of Teleportation | 3 | 3 | 2 | 5 | 3.20 | 🟡 P2 |
| Carpet of Flying (×3) | 2 | 3 | 3 | 3 | 2.75 | 🟢 P3 |
| Horseshoes of Speed | 2 | 2 | 5 | 2 | 2.65 | 🟢 P3 |
| Horseshoes of a Zephyr | 1 | 2 | 4 | 2 | 2.15 | 🟢 P3 |

**Rationale:** Boots of Speed (haste 10 rds/day) is one of the most powerful and popular wondrous items. Flight items are critical for mid-to-high level play. Movement mechanics enable many gameplay scenarios.

---

### 🟡 PHASE 5: STEALTH & DETECTION (Weeks 9–10) — Priority: IMPORTANT

| Item | Demand | Reuse | Ease | Impact | Score | Priority |
|------|--------|-------|------|--------|-------|----------|
| Cloak of Elvenkind | 4 | 3 | 5 | 3 | 3.75 | 🟡 P2 |
| Goggles of Night | 4 | 3 | 4 | 3 | 3.55 | 🟡 P2 |
| Hat of Disguise | 4 | 3 | 4 | 3 | 3.55 | 🟡 P2 |
| Eyes of the Eagle | 3 | 3 | 5 | 2 | 3.25 | 🟡 P2 |
| Goggles of Minute Seeing | 3 | 3 | 5 | 2 | 3.25 | 🟡 P2 |
| Circlet of Persuasion | 4 | 2 | 5 | 3 | 3.55 | 🟡 P2 |
| Lens of Detection | 3 | 2 | 5 | 2 | 2.95 | 🟢 P3 |
| Dust of Appearance | 2 | 2 | 3 | 3 | 2.45 | 🟢 P3 |
| Dust of Disappearance | 3 | 2 | 3 | 3 | 2.75 | 🟢 P3 |
| Dust of Tracelessness | 1 | 2 | 4 | 1 | 1.90 | ⚪ P4 |
| Amulet of Proof Against Detection | 2 | 2 | 3 | 3 | 2.45 | 🟢 P3 |
| Lantern of Revealing | 2 | 2 | 3 | 3 | 2.45 | 🟢 P3 |

---

### 🟡 PHASE 6: STORAGE ITEMS (Weeks 11–12) — Priority: IMPORTANT

| Item | Demand | Reuse | Ease | Impact | Score | Priority |
|------|--------|-------|------|--------|-------|----------|
| Handy Haversack | 5 | 4 | 3 | 4 | 4.05 | 🔴 P1 |
| Bag of Holding Type I | 5 | 5 | 3 | 4 | 4.30 | 🔴 P1 |
| Bag of Holding Type II | 4 | 4 | 3 | 3 | 3.55 | 🟡 P2 |
| Bag of Holding Type III | 3 | 3 | 3 | 3 | 3.00 | 🟡 P2 |
| Bag of Holding Type IV | 3 | 3 | 3 | 3 | 3.00 | 🟡 P2 |
| Efficient Quiver | 4 | 3 | 3 | 3 | 3.30 | 🟡 P2 |
| Portable Hole | 3 | 3 | 3 | 4 | 3.20 | 🟡 P2 |
| Folding Boat | 2 | 2 | 3 | 2 | 2.25 | 🟢 P3 |
| Decanter of Endless Water | 3 | 2 | 3 | 2 | 2.55 | 🟢 P3 |
| Eversmoking Bottle | 2 | 2 | 3 | 2 | 2.25 | 🟢 P3 |
| Bottle of Air | 2 | 2 | 4 | 2 | 2.50 | 🟢 P3 |
| Sustaining Spoon | 1 | 1 | 5 | 1 | 1.80 | ⚪ P4 |

**Rationale:** Handy Haversack and Bag of Holding are nearly universal equipment. The container system built here enables all future storage items.

---

### 🟢 PHASE 7: COMBAT ITEMS (Weeks 13–15) — Priority: STANDARD

| Item | Demand | Reuse | Ease | Impact | Score | Priority |
|------|--------|-------|------|--------|-------|----------|
| Bracers of Archery, Lesser | 4 | 2 | 4 | 3 | 3.30 | 🟡 P2 |
| Bracers of Archery, Greater | 3 | 2 | 4 | 3 | 3.00 | 🟡 P2 |
| Necklace of Fireballs III | 3 | 4 | 3 | 4 | 3.45 | 🟡 P2 |
| Necklace of Fireballs (other types) | 2 | 3 | 3 | 3 | 2.70 | 🟢 P3 |
| Bead of Force | 2 | 2 | 3 | 3 | 2.45 | 🟢 P3 |
| Horn of Blasting | 2 | 2 | 3 | 3 | 2.45 | 🟢 P3 |
| Iron Bands of Binding | 2 | 2 | 2 | 3 | 2.20 | 🟢 P3 |
| Gauntlet of Rust | 2 | 1 | 3 | 2 | 2.00 | 🟢 P3 |
| Gloves of Arrow Snaring | 3 | 2 | 3 | 3 | 2.75 | 🟢 P3 |

---

### 🟢 PHASE 8: SUMMONING ITEMS (Weeks 16–18) — Priority: STANDARD

| Item | Demand | Reuse | Ease | Impact | Score | Priority |
|------|--------|-------|------|--------|-------|----------|
| Bag of Tricks (Gray) | 3 | 4 | 3 | 3 | 3.25 | 🟡 P2 |
| Bag of Tricks (Rust) | 3 | 3 | 3 | 3 | 3.00 | 🟡 P2 |
| Bag of Tricks (Tan) | 3 | 3 | 3 | 3 | 3.00 | 🟡 P2 |
| Figurine — Silver Raven | 2 | 3 | 3 | 2 | 2.50 | 🟢 P3 |
| Figurine — Bronze Griffon | 2 | 3 | 3 | 3 | 2.70 | 🟢 P3 |
| Figurine — Ebony Fly | 3 | 3 | 3 | 3 | 3.00 | 🟡 P2 |
| Figurine — Golden Lions | 2 | 2 | 2 | 3 | 2.20 | 🟢 P3 |
| Figurine — Marble Elephant | 2 | 2 | 2 | 3 | 2.20 | 🟢 P3 |
| Figurine — Ivory Goats | 2 | 2 | 2 | 3 | 2.20 | 🟢 P3 |
| Figurine — Obsidian Steed | 2 | 2 | 2 | 3 | 2.20 | 🟢 P3 |
| Elemental Gem (×4) | 3 | 3 | 3 | 3 | 3.00 | 🟡 P2 |
| Horn of Valhalla (Silver) | 1 | 2 | 2 | 3 | 1.90 | ⚪ P4 |
| Horn of Valhalla (Brass) | 1 | 2 | 2 | 3 | 1.90 | ⚪ P4 |
| Horn of Valhalla (Bronze) | 1 | 2 | 2 | 3 | 1.90 | ⚪ P4 |

---

### 🟢 PHASE 9: IOUN STONES (Weeks 19–20) — Priority: STANDARD

| Item | Demand | Reuse | Ease | Impact | Score | Priority |
|------|--------|-------|------|--------|-------|----------|
| Dusty Rose Prism (+1 AC) | 4 | 3 | 3 | 3 | 3.30 | 🟡 P2 |
| Pale Green Prism (+1 all) | 4 | 3 | 3 | 4 | 3.50 | 🟡 P2 |
| Orange Prism (+1 CL) | 3 | 2 | 3 | 4 | 2.95 | 🟢 P3 |
| Ability score stones (×6) | 3 | 4 | 3 | 3 | 3.25 | 🟡 P2 |
| Clear Spindle (sustenance) | 2 | 2 | 4 | 1 | 2.20 | 🟢 P3 |
| Iridescent Spindle (no air) | 2 | 2 | 4 | 1 | 2.20 | 🟢 P3 |
| Pearly White Spindle (regen) | 3 | 2 | 3 | 3 | 2.75 | 🟢 P3 |
| Dark Blue Rhomboid (Alertness) | 2 | 2 | 3 | 2 | 2.25 | 🟢 P3 |
| Pale Lavender Ellipsoid (absorb) | 3 | 3 | 2 | 4 | 2.95 | 🟢 P3 |
| Vibrant Purple Prism (store) | 2 | 3 | 2 | 4 | 2.65 | 🟢 P3 |
| Lavender and Green Ellipsoid | 2 | 2 | 2 | 4 | 2.40 | 🟢 P3 |

---

### 🟢 PHASE 10: SPECIAL ROBES & COMPLEX (Weeks 21–23) — Priority: STANDARD

| Item | Demand | Reuse | Ease | Impact | Score | Priority |
|------|--------|-------|------|--------|-------|----------|
| Monk's Belt | 4 | 2 | 2 | 4 | 3.00 | 🟡 P2 |
| Robe of Useful Items | 2 | 3 | 2 | 2 | 2.25 | 🟢 P3 |
| Robe of Bones | 2 | 2 | 2 | 2 | 2.00 | 🟢 P3 |
| Robe of Stars | 2 | 2 | 1 | 4 | 2.15 | 🟢 P3 |
| Robe of Scintillating Colors | 2 | 2 | 2 | 3 | 2.20 | 🟢 P3 |
| Robe of Blending | 2 | 2 | 4 | 2 | 2.50 | 🟢 P3 |
| Cloak of Arachnida | 3 | 2 | 2 | 3 | 2.50 | 🟢 P3 |
| Cloak of the Bat | 2 | 2 | 2 | 3 | 2.20 | 🟢 P3 |
| Helm of Telepathy | 2 | 2 | 2 | 3 | 2.20 | 🟢 P3 |
| Cube of Force | 2 | 2 | 1 | 4 | 2.15 | 🟢 P3 |
| Rope of Entanglement | 2 | 2 | 2 | 3 | 2.20 | 🟢 P3 |
| Cube of Frost Resistance | 1 | 2 | 2 | 2 | 1.70 | ⚪ P4 |
| Druid's Vestment | 2 | 1 | 3 | 2 | 2.00 | 🟢 P3 |

---

## CONSUMABLE & MISC ITEM PRIORITIES

### Elixirs (7 items)

| Item | Price | Priority | Rationale |
|------|-------|----------|-----------|
| Elixir of Fire Breath | 1,100 gp | 🟢 P3 | Useful combat consumable |
| Elixir of Truth | 500 gp | 🟢 P3 | Social encounter utility |
| Elixir of Hiding | 250 gp | ⚪ P4 | Superseded by magic items |
| Elixir of Tumbling | 250 gp | ⚪ P4 | Niche use |
| Elixir of Swimming | 250 gp | ⚪ P4 | Niche use |
| Elixir of Vision | 250 gp | ⚪ P4 | Niche use |
| Elixir of Love | 150 gp | ⚪ P4 | RP item |

### Feather Tokens (6 items)

| Item | Price | Priority | Rationale |
|------|-------|----------|-----------|
| Swan Boat | 450 gp | 🟢 P3 | Utility |
| Tree | 400 gp | ⚪ P4 | Niche |
| Bird | 300 gp | 🟢 P3 | Message delivery |
| Whip | 500 gp | ⚪ P4 | Niche combat |
| Fan | 200 gp | ⚪ P4 | Niche |
| Anchor | 50 gp | ⚪ P4 | Very niche |

### Pearls of Power (8 items in scope)

| Item | Price | Priority | Rationale |
|------|-------|----------|-----------|
| Pearl of Power (1st) | 1,000 gp | 🔴 P1 | Extremely popular for spellcasters |
| Pearl of Power (2nd) | 4,000 gp | 🟡 P2 | Very popular |
| Pearl of Power (3rd) | 9,000 gp | 🟡 P2 | Popular |
| Pearl of Power (4th) | 16,000 gp | 🟡 P2 | Common at mid levels |
| Pearl of Power (5th) | 25,000 gp | 🟢 P3 | Used at higher levels |
| Pearl of Power (6th) | 36,000 gp | 🟢 P3 | Used at higher levels |
| Pearl of Power (7th) | 49,000 gp | 🟢 P3 | Used at high levels |
| Pearl of Power (8th) | 64,000 gp | ⚪ P4 | Rare, very high level |

### Miscellaneous Consumables

| Item | Price | Priority | Rationale |
|------|-------|----------|-----------|
| Restorative Ointment | 4,000 gp | 🟡 P2 | Healing utility |
| Incense of Meditation | 4,900 gp | 🟡 P2 | Popular with divine casters |
| Salve of Slipperiness | 1,000 gp | 🟢 P3 | Freedom of movement |
| Sovereign Glue | 2,400 gp | ⚪ P4 | Situational |
| Universal Solvent | 50 gp | ⚪ P4 | Counter to Sovereign Glue |
| Stone Salve | 4,000 gp | 🟢 P3 | Counter to petrification |
| Silversheen | 250 gp | 🟡 P2 | DR bypass, common need |
| Unguent of Timelessness | 150 gp | ⚪ P4 | Niche flavor item |

### Miscellaneous Permanent

| Item | Price | Priority | Rationale |
|------|-------|----------|-----------|
| Scarab of Protection | 38,000 gp | 🟡 P2 | Death/drain protection |
| Periapt of Proof Against Poison | 27,000 gp | 🟡 P2 | Poison immunity |
| Phylactery of Undead Turning | 11,000 gp | 🟢 P3 | Cleric-specific |
| Brooch of Shielding | 1,500 gp | 🟡 P2 | Common anti-magic-missile |
| Necklace of Adaptation | 9,000 gp | 🟡 P2 | Environmental protection |
| Stone of Good Luck | 20,000 gp | 🟡 P2 | Universal +1 luck bonus |
| Stone of Alarm | 2,700 gp | 🟢 P3 | Camp security |
| Golembane Scarab | 2,500 gp | ⚪ P4 | Very situational |
| Chime of Opening | 3,000 gp | 🟢 P3 | Lock bypass |
| Rope of Climbing | 3,000 gp | 🟢 P3 | Dungeon utility |
| Horn of Fog | 2,000 gp | ⚪ P4 | Limited use |
| Pipes of Sounding | 1,800 gp | ⚪ P4 | Limited use |
| Pipes of the Sewers | 1,150 gp | ⚪ P4 | Very niche |
| Mattock of the Titans | 23,348 gp | ⚪ P4 | Large creature only |
| Gem of Brightness | 13,000 gp | 🟢 P3 | Versatile light/blind item |
| Lyre of Building | 13,000 gp | ⚪ P4 | Construction niche |
| Hand of the Mage | 900 gp | 🟢 P3 | Cheap cantrip item |
| Vest of Escape | 5,200 gp | 🟢 P3 | Escape utility |

---

## MASTER PRIORITY LIST — TOP 30 ITEMS

Sorted by priority score (highest first):

| Rank | Item | Score | Phase | Tier |
|------|------|-------|-------|------|
| 1 | Cloak of Resistance +1 | 5.00 | 3 | 1 |
| 2 | Headband of Intellect +2 | 4.80 | 2 | 1 |
| 3 | Cloak of Charisma +2 | 4.80 | 2 | 1 |
| 4 | Gauntlets of Ogre Power | 4.80 | 2 | 1 |
| 5 | Gloves of Dexterity +2 | 4.80 | 2 | 1 |
| 6 | Amulet of Health +2 | 4.80 | 2 | 1 |
| 7 | Periapt of Wisdom +2 | 4.80 | 2 | 1 |
| 8 | Amulet of Natural Armor +1 | 4.75 | 3 | 1 |
| 9 | Bracers of Armor +1 | 4.75 | 3 | 1 |
| 10 | Cloak of Resistance +2 | 4.75 | 3 | 1 |
| 11 | Belt of Giant Strength +4 | 4.55 | 2 | 1 |
| 12 | Gloves of Dexterity +4 | 4.55 | 2 | 1 |
| 13 | Amulet of Health +4 | 4.55 | 2 | 1 |
| 14 | Headband of Intellect +4 | 4.55 | 2 | 1 |
| 15 | Periapt of Wisdom +4 | 4.55 | 2 | 1 |
| 16 | Cloak of Charisma +4 | 4.55 | 2 | 1 |
| 17 | Bag of Holding Type I | 4.30 | 6 | 3 |
| 18 | Cloak of Resistance +4 | 4.20 | 3 | 1 |
| 19 | Amulet of Natural Armor +3 | 4.20 | 3 | 1 |
| 20 | Handy Haversack | 4.05 | 6 | 3 |
| 21 | Boots of Speed | 4.00 | 4 | 2 |
| 22 | Pearl of Power (1st) | 3.95 | 2 | 2 |
| 23 | Cloak of Displacement, Minor | 3.95 | 3 | 2 |
| 24 | Boots of Elvenkind | 3.75 | 4 | 1 |
| 25 | Cloak of Elvenkind | 3.75 | 5 | 1 |
| 26 | Winged Boots | 3.75 | 4 | 2 |
| 27 | Circlet of Persuasion | 3.55 | 5 | 1 |
| 28 | Goggles of Night | 3.55 | 5 | 2 |
| 29 | Hat of Disguise | 3.55 | 5 | 2 |
| 30 | Necklace of Fireballs III | 3.45 | 7 | 4 |

---

## DEPENDENCY GRAPH

```
Phase 1: Foundation
├── Equipment Slot System ─────────────┬── All worn items (Phases 2-5, 9-10)
├── Bonus Management System ───────────┤
├── Activation Framework ──────────────┤
├── Daily Use Tracker ─────────────────┤
└── Database Schema ───────────────────┘
        │
        ├── Phase 2: Ability Scores (no further dependencies)
        ├── Phase 3: AC & Saves (needs displacement → miss chance system)
        ├── Phase 4: Movement (needs flight maneuverability system)
        ├── Phase 5: Stealth & Detection (needs vision mode system)
        │
        ├── Phase 6: Storage (needs Container System — new)
        │       └── Extradimensional interaction rules
        │
        ├── Phase 7: Combat (needs AoE targeting, charge tracking)
        │       └── Fireball chain detonation mechanic
        │
        ├── Phase 8: Summoning (needs Creature Summoning System — new)
        │       ├── Random animal tables
        │       ├── Figurine duration tracking
        │       └── Creature stat blocks (20+ creatures)
        │
        ├── Phase 9: Ioun Stones (needs Slotless Orbiting System — new)
        │       ├── Spell absorption tracking
        │       └── Spell storage system
        │
        └── Phase 10: Complex Items (needs all above systems)
                ├── Patch/component tracking
                ├── Conditional ability activation
                └── Multi-ability state management
```

---

## SPRINT PLANNING TEMPLATE

### Sprint W1 (Week 1): Foundation Part 1
- [ ] Define EquipmentSlot enum with all 10+ slots
- [ ] Implement WondrousItem base class
- [ ] Create BonusManager for stacking rules
- [ ] Define BonusType enum (enhancement, competence, resistance, luck, insight, etc.)
- [ ] Create database schema for wondrous items

### Sprint W2 (Week 2): Foundation Part 2
- [ ] Implement activation framework (continuous, command word, use-activated)
- [ ] Implement DailyUseTracker
- [ ] Implement ChargeTracker
- [ ] Create WondrousItemFactory
- [ ] Write foundation unit tests

### Sprint W3 (Week 3): Ability Scores — +2 Items
- [ ] Implement all 6 ability score +2 items
- [ ] Verify bonus application and removal
- [ ] Verify non-stacking with same type
- [ ] Verify ability modifier recalculation

### Sprint W4 (Week 4): Ability Scores — +4/+6 Items
- [ ] Implement all 12 ability score +4/+6 items
- [ ] Full regression testing on ability score system

### Sprint W5 (Week 5): AC Items
- [ ] Implement Bracers of Armor +1 to +8
- [ ] Implement Amulet of Natural Armor +1 to +5
- [ ] Verify armor bonus vs natural armor bonus stacking rules

### Sprint W6 (Week 6): Save Items + Displacement
- [ ] Implement Cloak of Resistance +1 to +5
- [ ] Implement miss chance system
- [ ] Implement Cloak of Displacement Minor/Major
- [ ] Implement Scarab of Protection

### Sprint W7–W8 (Weeks 7–8): Movement Items
- [ ] Implement speed modifiers (Boots of Striding)
- [ ] Implement flight system (Winged Boots, Wings of Flying)
- [ ] Implement haste tracking (Boots of Speed)
- [ ] Implement special movement (Spider Climbing, Levitation)
- [ ] Implement teleportation (Boots of Teleportation)

### Sprint W9–W10 (Weeks 9–10): Stealth, Detection, Skills
- [ ] Implement all skill bonus items
- [ ] Implement darkvision mode (Goggles of Night)
- [ ] Implement disguise mechanics (Hat of Disguise)
- [ ] Implement consumable dust items

### Sprint W11–W12 (Weeks 11–12): Storage
- [ ] Implement Container base class
- [ ] Implement Bag of Holding Types I–IV
- [ ] Implement Handy Haversack
- [ ] Implement extradimensional nesting rules
- [ ] Implement Portable Hole

### Sprint W13–W15 (Weeks 13–15): Combat + Summoning
- [ ] Implement Necklace of Fireballs (all types)
- [ ] Implement AoE damage mechanics
- [ ] Implement Bag of Tricks (all types)
- [ ] Implement Figurines of Wondrous Power (9 types)
- [ ] Implement Elemental Gems (4 types)

### Sprint W16–W18 (Weeks 16–18): Ioun Stones
- [ ] Implement slotless orbiting system
- [ ] Implement all 16 Ioun Stone types
- [ ] Implement spell absorption mechanics
- [ ] Implement burn-out tracking

### Sprint W19–W23 (Weeks 19–23): Complex Items + Polish
- [ ] Implement multi-ability robes
- [ ] Implement Helm of Telepathy
- [ ] Implement Cube of Force
- [ ] Implement remaining items
- [ ] Integration testing
- [ ] Edge case testing

---

## RISK-ADJUSTED TIMELINE

| Phase | Best Case | Expected | Worst Case | Risk Level |
|-------|-----------|----------|------------|------------|
| Foundation | 1.5 weeks | 2 weeks | 3 weeks | Low |
| Ability Scores | 1 week | 2 weeks | 2 weeks | Low |
| AC & Saves | 1.5 weeks | 2 weeks | 3 weeks | Medium (displacement) |
| Movement | 1.5 weeks | 2 weeks | 3 weeks | Medium (flight) |
| Stealth & Detection | 1.5 weeks | 2 weeks | 2.5 weeks | Low |
| Storage | 1.5 weeks | 2 weeks | 3 weeks | Medium (nesting rules) |
| Combat | 2 weeks | 3 weeks | 4 weeks | High (AoE + chain detonation) |
| Summoning | 2 weeks | 3 weeks | 4 weeks | High (creature stats) |
| Ioun Stones | 1.5 weeks | 2 weeks | 3 weeks | Medium (new subsystem) |
| Complex Items | 2 weeks | 3 weeks | 4 weeks | High (integration) |
| **Total** | **16 weeks** | **23 weeks** | **31.5 weeks** | |

---

## MILESTONE CHECKPOINTS

| Milestone | Week | Items Complete | Cumulative % |
|-----------|------|---------------|-------------|
| M1: Foundation complete | 2 | 0 (infrastructure) | 0% |
| M2: "Big Six" ability items | 4 | 18 | 14% |
| M3: AC + Save items | 6 | 38 | 29% |
| M4: Movement items | 8 | 50 | 38% |
| M5: Stealth + Skills | 10 | 62 | 48% |
| M6: Storage system | 12 | 77 | 59% |
| M7: Combat items | 15 | 92 | 71% |
| M8: Summoning items | 18 | 112 | 86% |
| M9: Ioun Stones | 20 | 128 | 98% |
| M10: Complex + Polish | 23 | 130 | 100% |

---

*Document created: May 2026*
