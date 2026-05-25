# NPC Templates by Level — Quick Generation Reference

**Document Version:** 1.0  
**Date:** 2026-05-25  
**Companion:** `npc_classes_implementation_plan.md`, `npc_class_definitions.md`, `creature_class_application_system.md`

---

## Table of Contents

- [1. Stat Arrays](#1-stat-arrays)
- [2. Wealth by Level Tables](#2-wealth-by-level-tables)
- [3. PHB Class Templates](#3-phb-class-templates)
- [4. NPC Class Templates](#4-npc-class-templates)
- [5. Typical Feat Selections](#5-typical-feat-selections)
- [6. Standard Equipment by Class & Level](#6-standard-equipment-by-class--level)
- [7. Example Classed Creatures](#7-example-classed-creatures)
- [8. C# Template Database Architecture](#8-c-template-database-architecture)

---

## 1. Stat Arrays

### 1.1 Standard Arrays

| Array | Values | Use Case |
|:------|:-------|:---------|
| **Elite** | 15, 14, 13, 12, 10, 8 | PCs, important NPCs with PC classes, monsters with PC class levels |
| **Nonelite** | 13, 12, 11, 10, 9, 8 | Standard NPCs with NPC classes, monsters with NPC class levels |
| **Basic** | 11, 11, 11, 10, 10, 10 | Average commoners, background creatures, baseline monsters |

### 1.2 Ability Score Arrangement by Class

| Class | 1st | 2nd | 3rd | 4th | 5th | 6th |
|:------|:----|:----|:----|:----|:----|:----|
| Fighter | STR | CON | DEX | WIS | CHA | INT |
| Barbarian | STR | CON | DEX | WIS | CHA | INT |
| Ranger | DEX | STR | CON | WIS | CHA | INT |
| Paladin | STR | CHA | CON | WIS | DEX | INT |
| Rogue | DEX | INT | CHA | CON | WIS | STR |
| Bard | CHA | DEX | CON | INT | WIS | STR |
| Monk | WIS | STR | DEX | CON | CHA | INT |
| Cleric | WIS | CON | STR | CHA | DEX | INT |
| Druid | WIS | CON | DEX | CHA | STR | INT |
| Wizard | INT | DEX | CON | WIS | CHA | STR |
| Sorcerer | CHA | DEX | CON | WIS | INT | STR |
| Adept | WIS | CON | STR | CHA | DEX | INT |
| Aristocrat | CHA | WIS | INT | DEX | CON | STR |
| Commoner | STR | CON | WIS | DEX | CHA | INT |
| Expert | INT | DEX | WIS | CON | CHA | STR |
| Warrior | STR | CON | DEX | WIS | CHA | INT |

### 1.3 Arranged Elite Array Examples

| Class | STR | DEX | CON | INT | WIS | CHA |
|:------|:----|:----|:----|:----|:----|:----|
| Fighter | 15 | 13 | 14 | 8 | 12 | 10 |
| Rogue | 8 | 15 | 12 | 14 | 10 | 13 |
| Wizard | 8 | 14 | 13 | 15 | 12 | 10 |
| Cleric | 13 | 10 | 14 | 8 | 15 | 12 |
| Sorcerer | 8 | 14 | 13 | 10 | 12 | 15 |

### 1.4 Arranged Nonelite Array Examples

| Class | STR | DEX | CON | INT | WIS | CHA |
|:------|:----|:----|:----|:----|:----|:----|
| Warrior | 13 | 11 | 12 | 8 | 10 | 9 |
| Adept | 11 | 9 | 12 | 8 | 13 | 10 |
| Expert | 8 | 12 | 10 | 13 | 11 | 9 |
| Aristocrat | 8 | 10 | 9 | 11 | 12 | 13 |
| Commoner | 13 | 10 | 12 | 8 | 11 | 9 |

---

## 2. Wealth by Level Tables

### 2.1 Character Wealth by Level (PC/Elite NPC)

| Level | Wealth (gp) | Level | Wealth (gp) |
|:------|:-----------|:------|:-----------|
| 1 | 900 | 11 | 66,000 |
| 2 | 2,700 | 12 | 88,000 |
| 3 | 5,400 | 13 | 110,000 |
| 4 | 9,000 | 14 | 150,000 |
| 5 | 13,000 | 15 | 200,000 |
| 6 | 19,000 | 16 | 260,000 |
| 7 | 27,000 | 17 | 340,000 |
| 8 | 36,000 | 18 | 440,000 |
| 9 | 49,000 | 19 | 580,000 |
| 10 | 66,000 | 20 | 760,000 |

### 2.2 NPC Gear Value (Nonelite NPCs)

NPC gear value is typically **half** the PC wealth at equivalent level:

| Level | NPC Gear (gp) | Level | NPC Gear (gp) |
|:------|:-------------|:------|:-------------|
| 1 | 450 | 6 | 9,500 |
| 2 | 1,350 | 7 | 13,500 |
| 3 | 2,700 | 8 | 18,000 |
| 4 | 4,500 | 9 | 24,500 |
| 5 | 6,500 | 10 | 33,000 |

---

## 3. PHB Class Templates

### 3.1 Fighter Templates

#### Fighter Level 1 (CR 1, Elite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 15, DEX 13, CON 14, INT 8, WIS 12, CHA 10 |
| **HP** | 12 (1d10+2) |
| **AC** | 17 (Scale Mail +4, Heavy Shield +2, DEX +1) |
| **BAB/Grapple** | +1/+3 |
| **Melee** | Longsword +3 (1d8+2/19-20) |
| **Fort/Ref/Will** | +4/+1/+1 |
| **Feats** | Power Attack, Cleave |
| **Equipment** | Scale Mail, Heavy Steel Shield, Longsword, Shortbow, 20 arrows |
| **Wealth** | 900 gp total |

#### Fighter Level 5 (CR 5, Elite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 16 (+1 at 4th), DEX 13, CON 14, INT 8, WIS 12, CHA 10 |
| **HP** | 42 (5d10+10) |
| **AC** | 21 (+1 Full Plate +9, +1 Heavy Shield +3) |
| **BAB/Grapple** | +5/+8 |
| **Melee** | +1 Greatsword +10 (2d6+5/19-20) |
| **Fort/Ref/Will** | +6/+2/+2 |
| **Feats** | Power Attack, Cleave, Weapon Focus (Greatsword), Weapon Specialization (Greatsword), Great Cleave |
| **Equipment** | +1 Full Plate, +1 Heavy Shield, +1 Greatsword, Gauntlets of Ogre Power +2 |
| **Wealth** | ~13,000 gp total |

#### Fighter Level 10 (CR 10, Elite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 18 (+1 at 4th, +4 belt), DEX 13, CON 14, INT 8, WIS 12, CHA 10 |
| **HP** | 84 (10d10+20) |
| **AC** | 24 (+3 Full Plate +11, Ring of Protection +2, DEX +1) |
| **BAB/Grapple** | +10/+14 |
| **Melee** | +2 Keen Greatsword +18/+13 (2d6+11/17-20) |
| **Fort/Ref/Will** | +9/+4/+4 |
| **Feats** | Power Attack, Cleave, Great Cleave, Weapon Focus, Weapon Specialization, Improved Critical, Greater Weapon Focus, Improved Initiative, Toughness |
| **Equipment** | +2 Keen Greatsword, +3 Full Plate, Belt of Giant Strength +4, Ring of Protection +2, Amulet of Natural Armor +2, Cloak of Resistance +1 |
| **Wealth** | ~66,000 gp total |

#### Fighter Level 15 (CR 15, Elite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 21 (+2 levels, +6 belt), DEX 13, CON 14, INT 8, WIS 12, CHA 10 |
| **HP** | 126 (15d10+30) |
| **AC** | 30 (+4 Full Plate +12, Ring of Protection +4, Nat Armor +4, DEX +1, Deflection +1) |
| **BAB/Grapple** | +15/+20 |
| **Melee** | +4 Keen Greatsword +25/+20/+15 (2d6+15/17-20) |
| **Fort/Ref/Will** | +11/+6/+6 |
| **Equipment** | +4 Keen Greatsword, +4 Full Plate (Light Fortification), Belt of Giant Strength +6, Ring of Protection +4, Amulet of Natural Armor +4, Cloak of Resistance +3, Boots of Speed |
| **Wealth** | ~200,000 gp total |

#### Fighter Level 20 (CR 20, Elite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 24 (+3 levels, +6 belt), DEX 14 (+1 level), CON 14, INT 8, WIS 12, CHA 10 |
| **HP** | 172 (20d10+40) |
| **AC** | 35 (+5 Full Plate +13, Ring of Protection +5, Nat Armor +5, DEX +2) |
| **BAB/Grapple** | +20/+27 |
| **Melee** | +5 Keen Vorpal Greatsword +33/+28/+23/+18 (2d6+19/17-20) |
| **Fort/Ref/Will** | +14/+8/+8 |
| **Equipment** | +5 Keen Vorpal Greatsword, +5 Full Plate (Moderate Fort), Belt of Giant Strength +6, Ring of Protection +5, Amulet of Natural Armor +5, Cloak of Resistance +5, Boots of Speed, Ioun Stones |
| **Wealth** | ~760,000 gp total |

### 3.2 Rogue Templates

#### Rogue Level 1 (CR 1, Elite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 8, DEX 15, CON 12, INT 14, WIS 10, CHA 13 |
| **HP** | 8 (1d6+2) |
| **AC** | 14 (Leather +2, DEX +2) |
| **BAB/Grapple** | +0/−1 |
| **Melee** | Rapier +2 (1d6−1/18-20) |
| **Sneak Attack** | +1d6 |
| **Fort/Ref/Will** | +0/+4/+0 |
| **Feats** | Weapon Finesse |
| **Equipment** | Leather Armor, Rapier, Shortbow, Thieves' Tools (MW) |

#### Rogue Level 5 (CR 5, Elite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 8, DEX 16 (+1 at 4th), CON 12, INT 14, WIS 10, CHA 13 |
| **HP** | 28 (5d6+5) |
| **AC** | 17 (+1 Leather +3, DEX +3, Ring +1) |
| **Sneak Attack** | +3d6 |
| **Feats** | Weapon Finesse, Dodge, Two-Weapon Fighting |
| **Special** | Evasion, Uncanny Dodge, Trap Sense +1 |
| **Equipment** | +1 Leather, +1 Rapier, Gloves of Dexterity +2, Ring of Protection +1, Cloak of Elvenkind |
| **Wealth** | ~13,000 gp |

#### Rogue Level 10 (CR 10, Elite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 8, DEX 18 (+2 levels, +4 gloves), CON 12, INT 14, WIS 10, CHA 13 |
| **HP** | 53 (10d6+10) |
| **Sneak Attack** | +5d6 |
| **Special** | Improved Evasion, Improved Uncanny Dodge |
| **Equipment** | +2 Shadow Studded Leather, +2 Rapier, Gloves of Dexterity +4, Ring of Protection +2, Amulet of Natural Armor +2, Cloak of Elvenkind, Boots of Elvenkind |
| **Wealth** | ~66,000 gp |

### 3.3 Wizard Templates

#### Wizard Level 1 (CR 1, Elite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 8, DEX 14, CON 13, INT 15, WIS 12, CHA 10 |
| **HP** | 5 (1d4+1) |
| **AC** | 12 (DEX +2) |
| **BAB/Grapple** | +0/−1 |
| **Spells** | 3 0th, 1 1st (+ bonus) |
| **Feats** | Scribe Scroll, Spell Focus (Evocation) |
| **Equipment** | Quarterstaff, Spellbook, Spell Component Pouch |

#### Wizard Level 5 (CR 5, Elite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 8, DEX 14, CON 13, INT 16 (+1 at 4th), WIS 12, CHA 10 |
| **HP** | 19 (5d4+5) |
| **Spells** | 4/4/2/1 + bonus |
| **Feats** | Scribe Scroll, Spell Focus (Evocation), Greater Spell Focus (Evocation), Combat Casting |
| **Equipment** | Headband of Intellect +2, Cloak of Resistance +1, Ring of Protection +1 |
| **Wealth** | ~13,000 gp |

#### Wizard Level 10 (CR 10, Elite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 8, DEX 14, CON 13, INT 20 (+2 levels, +4 headband), WIS 12, CHA 10 |
| **HP** | 35 (10d4+10) |
| **Spells** | 4/5/4/3/3/2 + bonus |
| **Feats** | Scribe Scroll, Spell Focus (Evo), Greater Spell Focus (Evo), Combat Casting, Spell Penetration, Improved Counterspell |
| **Equipment** | Headband of Intellect +4, Cloak of Resistance +3, Ring of Protection +2, Amulet of Natural Armor +2, Lesser Metamagic Rod of Quicken |
| **Wealth** | ~66,000 gp |

### 3.4 Cleric Templates

#### Cleric Level 1 (CR 1, Elite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 13, DEX 10, CON 14, INT 8, WIS 15, CHA 12 |
| **HP** | 10 (1d8+2) |
| **AC** | 18 (Scale Mail +4, Heavy Shield +2, DEX +0, Nat +0, Ring +0, Deflect +2 [Shield of Faith]) |
| **Feats** | Heavy Armor Proficiency |
| **Spells** | 3/2+1 (domain) |
| **Equipment** | Scale Mail, Heavy Steel Shield, Morningstar, Wooden Holy Symbol |

#### Cleric Level 5 (CR 5, Elite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 13, DEX 10, CON 14, INT 8, WIS 16 (+1 at 4th), CHA 12 |
| **HP** | 38 (5d8+10) |
| **Feats** | Heavy Armor Proficiency, Combat Casting, Extra Turning |
| **Spells** | 5/4+1/3+1/2+1 |
| **Equipment** | +1 Full Plate, +1 Heavy Shield, +1 Morningstar, Periapt of Wisdom +2 |
| **Wealth** | ~13,000 gp |

### 3.5 Barbarian Templates (Summary)

| Level | HP | BAB | Primary Attack | AC | Key Feats |
|:------|:---|:----|:--------------|:---|:----------|
| 1 | 14 (1d12+2) | +1 | Greataxe +4 (1d12+4) | 15 | Power Attack |
| 5 | 52 (5d12+10) | +5 | +1 Greataxe +10 (1d12+8) | 17 | Power Attack, Cleave, Extra Rage |
| 10 | 97 (10d12+20) | +10 | +2 Greataxe +17/+12 (1d12+10) | 20 | Power Attack, Cleave, Great Cleave, Improved Critical |
| 15 | 146 (15d12+30) | +15 | +4 Greataxe +25/+20/+15 | 23 | + Greater Rage, Tireless Rage |
| 20 | 195 (20d12+40) | +20 | +5 Greataxe +32/+27/+22/+17 | 27 | + Mighty Rage |

### 3.6 Remaining PHB Classes (Summary)

| Class | Lvl 1 HP | Lvl 5 HP | Lvl 10 HP | Lvl 15 HP | Lvl 20 HP | Key Stat |
|:------|:---------|:---------|:----------|:----------|:----------|:---------|
| Paladin | 12 | 42 | 84 | 126 | 172 | STR/CHA |
| Ranger | 10 | 38 | 72 | 108 | 146 | DEX/WIS |
| Bard | 8 | 28 | 53 | 80 | 108 | CHA |
| Druid | 10 | 38 | 72 | 108 | 146 | WIS |
| Monk | 10 | 38 | 72 | 108 | 146 | WIS/STR |
| Sorcerer | 6 | 19 | 35 | 53 | 72 | CHA |

---

## 4. NPC Class Templates

### 4.1 Warrior Templates

#### Warrior Level 1 (CR ½, Nonelite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 13, DEX 11, CON 12, INT 8, WIS 10, CHA 9 |
| **HP** | 9 (1d8+1) |
| **AC** | 16 (Scale Mail +4, Heavy Shield +2) |
| **BAB/Grapple** | +1/+2 |
| **Melee** | Longsword +3 (1d8+1/19-20) |
| **Fort/Ref/Will** | +3/+0/+0 |
| **Feats** | Weapon Focus (Longsword) |
| **Equipment** | Scale Mail, Heavy Steel Shield, Longsword, Light Crossbow, 10 bolts |
| **Gear Value** | ~200 gp |

#### Warrior Level 5 (CR 4, Nonelite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 14 (+1 at 4th), DEX 11, CON 12, INT 8, WIS 10, CHA 9 |
| **HP** | 30 (5d8+5) |
| **AC** | 18 (MW Full Plate +8, MW Shield +2, −2 for two-handing) or 20 with shield |
| **BAB/Grapple** | +5/+7 |
| **Melee** | MW Longsword +8 (1d8+2/19-20) |
| **Fort/Ref/Will** | +5/+1/+1 |
| **Feats** | Weapon Focus (Longsword), Toughness, Alertness |
| **Equipment** | MW Full Plate, MW Heavy Shield, MW Longsword, Heavy Crossbow, 20 bolts |
| **Gear Value** | ~4,500 gp |

#### Warrior Level 10 (CR 9, Nonelite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 15 (+1 at 4th, +1 at 8th), DEX 11, CON 12, INT 8, WIS 10, CHA 9 |
| **HP** | 58 (10d8+10) |
| **AC** | 22 (+1 Full Plate +9, +1 Heavy Shield +3) |
| **BAB/Grapple** | +10/+12 |
| **Melee** | +1 Longsword +14/+9 (1d8+3/19-20) |
| **Fort/Ref/Will** | +8/+3/+3 |
| **Feats** | Weapon Focus (Longsword), Toughness, Alertness, Improved Initiative |
| **Equipment** | +1 Full Plate, +1 Heavy Shield, +1 Longsword, Cloak of Resistance +1, Gauntlets of Ogre Power +2 |
| **Gear Value** | ~33,000 gp |

### 4.2 Adept Templates

#### Adept Level 1 (CR ½, Nonelite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 11, DEX 9, CON 12, INT 8, WIS 13, CHA 10 |
| **HP** | 7 (1d6+1) |
| **AC** | 9 (no armor, DEX −1) |
| **BAB/Grapple** | +0/+0 |
| **Melee** | Quarterstaff +0 (1d6) |
| **Spells** | 3/1 |
| **Fort/Ref/Will** | +2/+0/+4 |
| **Feats** | Toughness |
| **Key Spells** | *cure minor wounds, detect magic, light; cure light wounds* |
| **Equipment** | Quarterstaff, Healer's Kit, Holy Symbol |

#### Adept Level 5 (CR 4, Nonelite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 11, DEX 9, CON 12, INT 8, WIS 14 (+1 at 4th), CHA 10 |
| **HP** | 22 (5d6+5) |
| **Spells** | 3/2/1 |
| **Fort/Ref/Will** | +4/+1/+6 |
| **Feats** | Toughness, Combat Casting |
| **Key Spells** | *cure light wounds, burning hands, sleep; cure moderate wounds, web* |
| **Special** | Summon Familiar |
| **Equipment** | Quarterstaff, Periapt of Wisdom +2, Scroll of cure moderate wounds ×2 |

#### Adept Level 10 (CR 9, Nonelite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 11, DEX 9, CON 12, INT 8, WIS 15 (+1 at 4th, +1 at 8th), CHA 10 |
| **HP** | 40 (10d6+10) |
| **Spells** | 3/3/2/1 |
| **Fort/Ref/Will** | +7/+3/+9 |
| **Feats** | Toughness, Combat Casting, Spell Focus (Evocation), Improved Initiative |
| **Equipment** | +1 Quarterstaff, Periapt of Wisdom +4, Cloak of Resistance +2, Ring of Protection +1, various scrolls |

### 4.3 Aristocrat Templates

#### Aristocrat Level 1 (CR ½, Nonelite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 8, DEX 10, CON 9, INT 11, WIS 12, CHA 13 |
| **HP** | 7 (1d8−1) |
| **AC** | 13 (Studded Leather +3) |
| **Melee** | Rapier +0 (1d6−1/18-20) |
| **Fort/Ref/Will** | +0/+0/+4 |
| **Skills** | Diplomacy +5, Sense Motive +5, Knowledge (nobility) +4, Bluff +5 |
| **Feats** | Negotiator |
| **Equipment** | Studded Leather, Rapier, Fine Clothing, Signet Ring |

#### Aristocrat Level 5 (CR 4, Nonelite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 8, DEX 10, CON 9, INT 11, WIS 12, CHA 14 (+1 at 4th) |
| **HP** | 22 (5d8−5) |
| **AC** | 15 (MW Chain Shirt +4, Ring +1) |
| **Melee** | MW Rapier +4 (1d6−1/18-20) |
| **Fort/Ref/Will** | +1/+1/+6 |
| **Skills** | Diplomacy +10, Sense Motive +9, Bluff +10, Intimidate +10 |
| **Feats** | Negotiator, Skill Focus (Diplomacy) |
| **Equipment** | MW Chain Shirt, MW Rapier, Ring of Protection +1, Cloak of Charisma +2, Fine Clothing |

### 4.4 Commoner Templates

#### Commoner Level 1 (CR ½, Basic Array)

| Stat | Value |
|:-----|:------|
| **Array** | STR 11, DEX 10, CON 11, INT 10, WIS 10, CHA 10 |
| **HP** | 4 (1d4) |
| **AC** | 10 (no armor) |
| **Melee** | Club +0 (1d6) or Pitchfork +0 (1d6) |
| **Fort/Ref/Will** | +0/+0/+0 |
| **Skills** | Profession +4, Craft +4 |
| **Feats** | Endurance |
| **Equipment** | Peasant clothing, Club or farming implement, 1d6 sp |

### 4.5 Expert Templates

#### Expert Level 1 (CR ½, Nonelite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 8, DEX 12, CON 10, INT 13, WIS 11, CHA 9 |
| **HP** | 6 (1d6) |
| **AC** | 13 (Leather +2, DEX +1) |
| **Melee** | Dagger +0 (1d4−1/19-20) |
| **Fort/Ref/Will** | +0/+2/+0 |
| **Skills** | Craft (chosen) +5, Appraise +5, Knowledge (local) +5, Profession +4, Diplomacy +3 |
| **Feats** | Skill Focus (primary Craft) |
| **Equipment** | Leather Armor, Dagger, Artisan's Tools, 2d6 gp |

#### Expert Level 5 (CR 4, Nonelite)

| Stat | Value |
|:-----|:------|
| **Array** | STR 8, DEX 12, CON 10, INT 14 (+1 at 4th), WIS 11, CHA 9 |
| **HP** | 20 (5d6) |
| **Skills** | Primary Craft +12, Appraise +10, Knowledge +10 |
| **Feats** | Skill Focus (Craft), Diligent |
| **Equipment** | MW Artisan's Tools, +1 Dagger, Leather Armor, various trade goods |

---

## 5. Typical Feat Selections

### 5.1 Martial Feats by Level

| Level | Fighter/Warrior | Barbarian | Ranger | Paladin |
|:------|:---------------|:----------|:-------|:--------|
| 1 | Power Attack, Cleave | Power Attack | Track, Rapid Shot | Power Attack |
| 3 | Great Cleave | Cleave | Endurance | Cleave |
| 6 | Weapon Focus | Great Cleave | Manyshot | Improved Critical |
| 9 | Weapon Specialization | Improved Critical | Improved Initiative | Great Cleave |
| 12 | Improved Critical | Power Critical | Improved Precise Shot | Mounted Combat |
| 15 | Greater Weapon Focus | Blind-Fight | — | Ride-By Attack |

### 5.2 Caster Feats by Level

| Level | Wizard | Cleric | Sorcerer | Druid | Adept |
|:------|:-------|:-------|:---------|:------|:------|
| 1 | Spell Focus | Heavy Armor Prof | Spell Focus | Natural Spell (later) | Toughness |
| 3 | Greater Spell Focus | Combat Casting | Greater Spell Focus | Augment Summoning | Combat Casting |
| 6 | Combat Casting | Extra Turning | Combat Casting | Natural Spell | Spell Focus |
| 9 | Spell Penetration | Spell Focus | Spell Penetration | Spell Focus | — |
| 12 | Quicken Spell | Greater Spell Focus | Quicken Spell | — | — |

### 5.3 NPC Class Feat Recommendations

| Class | Recommended Feats |
|:------|:-----------------|
| **Warrior** | Weapon Focus, Toughness, Alertness, Improved Initiative, Power Attack, Cleave |
| **Adept** | Toughness, Combat Casting, Spell Focus (Evocation), Improved Initiative |
| **Aristocrat** | Negotiator, Skill Focus (Diplomacy), Alertness, Iron Will, Persuasive |
| **Commoner** | Endurance, Toughness, Skill Focus (Profession), Run |
| **Expert** | Skill Focus (primary Craft), Diligent, Self-Sufficient, Alertness |

---

## 6. Standard Equipment by Class & Level

### 6.1 Martial Classes (Fighter, Barbarian, Warrior, Paladin, Ranger)

| Level | Weapon | Armor | Key Magic Items |
|:------|:-------|:------|:---------------|
| 1 | MW or standard | Scale Mail / Chain Shirt | — |
| 3 | MW weapon | MW armor | Potion of healing ×2 |
| 5 | +1 weapon | +1 armor | Gauntlets of Ogre Power +2 or Gloves of Dex +2 |
| 7 | +1 weapon | +2 armor | +2 ability item, Ring of Protection +1 |
| 10 | +2 weapon | +3 armor | +4 ability item, Ring +2, Amulet +2 |
| 13 | +3 weapon | +4 armor | +4 ability item, Ring +3, Cloak +3 |
| 15 | +4 weapon | +4 w/ special | +6 ability item, Ring +4, Amulet +4 |
| 20 | +5 weapon w/ special | +5 w/ special | +6 ability, Ring +5, Amulet +5, Boots of Speed |

### 6.2 Arcane Casters (Wizard, Sorcerer)

| Level | Weapon | Armor | Key Magic Items |
|:------|:-------|:------|:---------------|
| 1 | Quarterstaff | None | Spell component pouch |
| 5 | MW Quarterstaff | None | Headband of Intellect/Charisma +2, Cloak +1 |
| 10 | +1 Quarterstaff | None | Headband +4, Cloak +3, Ring +2, Metamagic Rod |
| 15 | +2 Quarterstaff | Bracers of Armor +4 | Headband +6, Cloak +5, Ring +4, Staff of Power |
| 20 | Staff of Power | Bracers of Armor +8 | Headband +6, Cloak +5, Ring +5, Robe of Archmagi |

### 6.3 Divine Casters (Cleric, Druid, Adept)

| Level | Weapon | Armor | Key Magic Items |
|:------|:-------|:------|:---------------|
| 1 | Morningstar/Quarterstaff | Scale Mail | Holy/Druidic Focus |
| 5 | +1 weapon | +1 armor | Periapt of Wisdom +2, Cloak +1 |
| 10 | +2 weapon | +2 armor | Periapt +4, Cloak +2, Ring +2 |
| 15 | +3 weapon | +3 armor | Periapt +6, Cloak +4, Ring +3 |
| 20 | +5 weapon | +5 armor | Periapt +6, Cloak +5, Ring +5 |

---

## 7. Example Classed Creatures

### 7.1 Lizardfolk Druid 5 (CR 6)

```
Lizardfolk Druid 5, CR 6
Medium Humanoid (Reptilian)
HP: 52 (2d8+4 [racial] + 5d8+10 [druid])
Init: +0
AC: 20 (+5 natural, +3 hide armor, +2 shield), touch 10, flat-footed 20
BAB/Grapple: +4/+6

Racial HD: 2 (Humanoid, d8)
Class: Druid 5
Total HD: 7
ECL: 7 + 1 (LA) = 8

Ability Scores (Elite + Racial [+2 STR, +2 CON, -2 INT]):
  STR 15 (13+2), DEX 10, CON 16 (14+2), INT 6 (8-2), WIS 15, CHA 12

BAB Calculation:
  Racial: 2 HD Humanoid @ Medium = floor(2*3/4) = 1
  Druid 5 @ Medium = floor(5*3/4) = 3
  Total BAB: 4

Saves:
  Fort: Racial 0 (Humanoid Poor 2HD) + Druid Good 5 (4) = 4, + CON 3 = 7
  Ref:  Racial 0 + Druid Poor 5 (1) = 1, + DEX 0 = 1
  Will: Racial 0 + Druid Good 5 (4) = 4, + WIS 2 = 6

CR Calculation:
  Druid is associated with Lizardfolk → +1 per level
  Base CR 1 + 5 = CR 6

Feats (7 total HD = 3 feats):
  Natural Spell, Augment Summoning, Spell Focus (Conjuration)

Spells: As 5th-level Druid (5/4/3/2 + bonus)

Equipment (Wealth ~19,000 gp):
  +1 Hide Armor, +1 Heavy Wooden Shield, +1 Club,
  Periapt of Wisdom +2, Cloak of Resistance +1
```

### 7.2 Ogre Barbarian 3 (CR 6)

```
Ogre Barbarian 3, CR 6
Large Giant
HP: 66 (4d8+16 [ogre] + 3d12+12 [barbarian])
Init: -1
AC: 17 (-1 size, -1 Dex, +5 natural, +4 hide armor), touch 8, flat-footed 17
Space/Reach: 10 ft./10 ft.
BAB/Grapple: +6/+17

Racial HD: 4 (Giant, d8)
Class: Barbarian 3
Total HD: 7
LA: +2, ECL: 9

Ability Scores (Elite + Racial [+10 STR, -2 DEX, +4 CON, -4 INT, -4 CHA]):
  STR 25 (15+10), DEX 11 (13-2), CON 18 (14+4), INT 4 (8-4), WIS 12, CHA 4 (8-4)

BAB Calculation:
  Racial: 4 HD Giant @ Medium = floor(4*3/4) = 3
  Barbarian 3 @ Good = 3
  Total BAB: 6

Saves:
  Fort: Giant 4 Good (4) + Barb 3 Good (3) = 7, + CON 4 = 11
  Ref:  Giant 4 Poor (1) + Barb 3 Poor (1) = 2, + DEX 0 = 2
  Will: Giant 4 Poor (1) + Barb 3 Poor (1) = 2, + WIS 1 = 3

CR Calculation:
  Barbarian is associated → +1 per level
  Base CR 3 + 3 = CR 6

Feats (7 total HD = 3 feats):
  Toughness (racial), Power Attack, Cleave

Special: Rage 1/day, Uncanny Dodge, Trap Sense +1, Fast Movement (+10 ft)

Melee: Huge Greatclub +13 (2d8+10) or Huge Greatclub +11 (2d8+16, Power Attack -2)
Ranged: Huge Javelin +5 (1d8+7)

Equipment:
  Large Hide Armor, Huge Greatclub, 4 Huge Javelins
```

### 7.3 Kobold Sorcerer 4 (CR 4)

```
Kobold Sorcerer 4, CR 4
Small Humanoid (Reptilian)
HP: 18 (4d4+4)
Init: +1
AC: 16 (+1 size, +1 Dex, +1 natural, +2 leather, +1 deflection), touch 13, flat-footed 15
BAB/Grapple: +2/-3

Racial HD: 0 (1st HD is class level, as with standard humanoids)
Class: Sorcerer 4
Total HD: 4
LA: +0, ECL: 4

Ability Scores (Elite + Racial [-4 STR, +2 DEX, -2 CON]):
  STR 4 (8-4), DEX 16 (14+2), CON 11 (13-2), INT 10, WIS 12, CHA 15

BAB: Sorcerer 4 @ Poor = floor(4/2) = 2

Saves:
  Fort: Sorc 4 Poor (1) + CON 0 = 1
  Ref:  Sorc 4 Poor (1) + DEX 3 = 4
  Will: Sorc 4 Good (4) + WIS 1 = 5

CR Calculation:
  Sorcerer is associated with Kobold → +1 per level
  Base CR 1/4 + 4 ≈ CR 4

Feats (4 total HD → 1 + (4-1)/3 = 2 feats):
  Spell Focus (Evocation), Point Blank Shot

Spells Known: 6/4 0th/1st, 6/7 spells/day
Known: 0th — acid splash, detect magic, ghost sound, mage hand, ray of frost, read magic
       1st — burning hands, magic missile, shield, sleep

Equipment (~9,000 gp):
  MW Leather Armor (Small), Ring of Protection +1,
  Cloak of Charisma +2, 2 scrolls of magic missile (CL 3),
  Wand of burning hands (CL 1, 20 charges)
```

### 7.4 Bugbear Rogue 3 (CR 6)

```
Bugbear Rogue 3, CR 6
Medium Humanoid (Goblinoid)
HP: 44 (3d8+6 [bugbear] + 3d6+6 [rogue])
Init: +2
AC: 18 (+2 Dex, +3 natural, +3 studded leather), touch 12, flat-footed 16
BAB/Grapple: +4/+7

Racial HD: 3 (Humanoid, d8)
Class: Rogue 3
Total HD: 6
LA: +1, ECL: 7

Ability Scores (Elite + Racial [+4 STR, +2 DEX, +2 CON, -2 CHA]):
  STR 17 (13+4), DEX 16 (14+2), CON 16 (14+2), INT 12, WIS 10, CHA 6 (8-2)

BAB:
  Racial: 3 HD Humanoid @ Medium = floor(3*3/4) = 2
  Rogue 3 @ Medium = floor(3*3/4) = 2
  Total BAB: 4

Saves:
  Fort: Humanoid 3 Poor (1) + Rogue 3 Poor (1) = 2, + CON 3 = 5
  Ref:  Humanoid 3 Poor (1) + Rogue 3 Good (3) = 4, + DEX 3 = 7
  Will: Humanoid 3 Poor (1) + Rogue 3 Poor (1) = 2, + WIS 0 = 2

CR: Rogue is associated (stealthy brute) → Base CR 2 + 3 = CR 5
    (Some DMs rule CR 6 due to Bugbear natural abilities; adjust per table)

Sneak Attack: +2d6

Feats (6 HD → 3 feats):
  Weapon Finesse, Dodge, Alertness

Special: Darkvision 60 ft., Scent, Evasion, Trap Sense +1

Melee: MW Rapier +7 (1d6+3/18-20)
Ranged: MW Shortbow +7 (1d6/×3)

Equipment (~13,000 gp):
  +1 Studded Leather, MW Rapier, MW Shortbow,
  Gloves of Dexterity +2, Cloak of Elvenkind, Potion of Invisibility ×2
```

### 7.5 Minotaur Fighter 2 (CR 6)

```
Minotaur Fighter 2, CR 6
Large Monstrous Humanoid
HP: 74 (6d8+24 [minotaur] + 2d10+8 [fighter])
Init: +0
AC: 16 (-1 size, +5 natural, +2 armor), touch 9, flat-footed 16
Space/Reach: 10 ft./10 ft.
BAB/Grapple: +8/+18

Racial HD: 6 (Monstrous Humanoid, d8)
Class: Fighter 2
Total HD: 8
LA: +2, ECL: 10

Ability Scores (Elite + Racial [+8 STR, -2 INT, -2 CHA]):
  STR 23 (15+8), DEX 10, CON 18, INT 6 (8-2), WIS 12, CHA 6 (8-2)

BAB:
  Racial: 6 HD Monstrous Humanoid @ Good = 6
  Fighter 2 @ Good = 2
  Total BAB: 8

Saves:
  Fort: MonHum 6 Poor (2) + Fighter 2 Good (3) = 5, + CON 4 = 9
  Ref:  MonHum 6 Good (5) + Fighter 2 Poor (0) = 5, + DEX 0 = 5
  Will: MonHum 6 Good (5) + Fighter 2 Poor (0) = 5, + WIS 1 = 6

CR: Fighter is associated → Base CR 4 + 2 = CR 6

Feats (8 HD → 3 feats + 1 Fighter bonus):
  Power Attack, Great Cleave, Improved Bull Rush, Weapon Focus (Greataxe) [fighter bonus]

Special: Natural Cunning, Scent, Charge (gore 1d8+6)

Melee: Large Greataxe +14/+9 (3d6+9/×3)
       Gore +9 (1d8+3)
```

---

## 8. C# Template Database Architecture

### 8.1 NPCTemplate Data Structure

```csharp
/// <summary>
/// Pre-configured NPC template for quick generation.
/// </summary>
[System.Serializable]
public class NPCTemplate
{
    public string Id;                  // e.g., "fighter_5", "ogre_barbarian_3"
    public string DisplayName;         // e.g., "Fighter 5", "Ogre Barbarian 3"
    
    // Base creature (null for humanoid NPCs)
    public string BaseCreatureId;      // NPCDatabase ID, e.g., "ogre"
    
    // Class configuration
    public string ClassName;           // e.g., "Fighter"
    public int ClassLevel;             // e.g., 5
    
    // Stat configuration
    public StatArrayType StatArray;    // Elite, Nonelite, or Basic
    
    // Pre-selected feats
    public string[] Feats;             // e.g., {"Power Attack", "Cleave", "Great Cleave"}
    
    // Ability score increase assignments (index = which increase, value = ability index)
    public int[] AbilityIncreaseTargets; // e.g., {0} means 1st increase goes to STR
    
    // Equipment template
    public NPCEquipmentTemplate Equipment;
    
    // Computed stats (cached)
    public float ExpectedCR;
    public int ExpectedHP;
}

/// <summary>
/// Equipment template for auto-assignment.
/// </summary>
[System.Serializable]
public class NPCEquipmentTemplate
{
    public string WeaponId;            // ItemDatabase ID
    public int WeaponEnhancement;      // +1, +2, etc.
    public string[] WeaponSpecials;    // "Keen", "Flaming", etc.
    
    public string ArmorId;
    public int ArmorEnhancement;
    public string[] ArmorSpecials;
    
    public string ShieldId;
    public int ShieldEnhancement;
    
    // Wondrous items by slot
    public string HeadItemId;
    public string NeckItemId;
    public string CloakItemId;
    public string RingItemId;
    public string BeltItemId;
    public string BootsItemId;
    public string HandsItemId;
    
    // Consumables
    public string[] PotionIds;
    public string[] ScrollIds;
}
```

### 8.2 NPCTemplateDatabase

```csharp
/// <summary>
/// Central lookup for pre-built NPC templates.
/// Provides quick generation for common NPC archetypes.
/// </summary>
public static class NPCTemplateDatabase
{
    private static Dictionary<string, NPCTemplate> _templates;
    private static bool _initialized;
    
    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _templates = new Dictionary<string, NPCTemplate>(StringComparer.OrdinalIgnoreCase);
        
        // PHB classes at key levels
        RegisterPHBTemplates();
        
        // NPC classes at key levels
        RegisterNPCClassTemplates();
        
        // Classed creatures
        RegisterClassedCreatureTemplates();
    }
    
    /// <summary>
    /// Generate a complete CharacterStats from a template ID.
    /// </summary>
    public static CharacterStats Generate(string templateId)
    {
        Init();
        if (!_templates.TryGetValue(templateId, out NPCTemplate template))
        {
            Debug.LogWarning($"[NPCTemplateDB] Template not found: {templateId}");
            return null;
        }
        
        return GenerateFromTemplate(template);
    }
    
    /// <summary>
    /// Generate a classed creature on-the-fly (not from a pre-built template).
    /// </summary>
    public static CharacterStats Generate(string creatureId, string className, int classLevel)
    {
        NPCDefinition baseDef = NPCDatabase.Get(creatureId);
        if (baseDef == null)
        {
            Debug.LogWarning($"[NPCTemplateDB] Creature not found: {creatureId}");
            return null;
        }
        
        // Determine stat array based on class type
        StatArrayType array = ClassRegistryUtils.IsNPCClass(className) 
            ? StatArrayType.Nonelite 
            : StatArrayType.Elite;
        
        return ClassLevelApplier.Apply(baseDef, className, classLevel, array);
    }
    
    /// <summary>
    /// Get all template IDs matching a filter.
    /// </summary>
    public static List<string> GetTemplateIds(string classFilter = null, int? levelFilter = null)
    {
        Init();
        var results = new List<string>();
        foreach (var kvp in _templates)
        {
            if (classFilter != null && !string.Equals(kvp.Value.ClassName, classFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (levelFilter.HasValue && kvp.Value.ClassLevel != levelFilter.Value)
                continue;
            results.Add(kvp.Key);
        }
        return results;
    }
    
    // === REGISTRATION METHODS ===
    
    private static void RegisterPHBTemplates()
    {
        // Fighter at 1, 5, 10, 15, 20
        Register(new NPCTemplate {
            Id = "fighter_1", DisplayName = "Fighter 1",
            ClassName = "Fighter", ClassLevel = 1,
            StatArray = StatArrayType.Elite,
            Feats = new[] { "Power Attack", "Cleave" },
            ExpectedCR = 1, ExpectedHP = 12
        });
        Register(new NPCTemplate {
            Id = "fighter_5", DisplayName = "Fighter 5",
            ClassName = "Fighter", ClassLevel = 5,
            StatArray = StatArrayType.Elite,
            Feats = new[] { "Power Attack", "Cleave", "Weapon Focus (Greatsword)", 
                           "Weapon Specialization (Greatsword)", "Great Cleave" },
            AbilityIncreaseTargets = new[] { 0 }, // STR at 4th
            ExpectedCR = 5, ExpectedHP = 42
        });
        Register(new NPCTemplate {
            Id = "fighter_10", DisplayName = "Fighter 10",
            ClassName = "Fighter", ClassLevel = 10,
            StatArray = StatArrayType.Elite,
            Feats = new[] { "Power Attack", "Cleave", "Great Cleave", 
                           "Weapon Focus (Greatsword)", "Weapon Specialization (Greatsword)",
                           "Improved Critical (Greatsword)", "Greater Weapon Focus (Greatsword)",
                           "Improved Initiative", "Toughness" },
            AbilityIncreaseTargets = new[] { 0, 0 }, // STR at 4th, 8th
            ExpectedCR = 10, ExpectedHP = 84
        });
        Register(new NPCTemplate {
            Id = "fighter_15", DisplayName = "Fighter 15",
            ClassName = "Fighter", ClassLevel = 15,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 15, ExpectedHP = 126
        });
        Register(new NPCTemplate {
            Id = "fighter_20", DisplayName = "Fighter 20",
            ClassName = "Fighter", ClassLevel = 20,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 20, ExpectedHP = 172
        });
        
        // Rogue at 1, 5, 10, 15, 20
        Register(new NPCTemplate {
            Id = "rogue_1", DisplayName = "Rogue 1",
            ClassName = "Rogue", ClassLevel = 1,
            StatArray = StatArrayType.Elite,
            Feats = new[] { "Weapon Finesse" },
            ExpectedCR = 1, ExpectedHP = 8
        });
        Register(new NPCTemplate {
            Id = "rogue_5", DisplayName = "Rogue 5",
            ClassName = "Rogue", ClassLevel = 5,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 5, ExpectedHP = 28
        });
        Register(new NPCTemplate {
            Id = "rogue_10", DisplayName = "Rogue 10",
            ClassName = "Rogue", ClassLevel = 10,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 10, ExpectedHP = 53
        });
        Register(new NPCTemplate {
            Id = "rogue_15", DisplayName = "Rogue 15",
            ClassName = "Rogue", ClassLevel = 15,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 15, ExpectedHP = 80
        });
        Register(new NPCTemplate {
            Id = "rogue_20", DisplayName = "Rogue 20",
            ClassName = "Rogue", ClassLevel = 20,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 20, ExpectedHP = 108
        });
        
        // Wizard at 1, 5, 10, 15, 20
        Register(new NPCTemplate {
            Id = "wizard_1", DisplayName = "Wizard 1",
            ClassName = "Wizard", ClassLevel = 1,
            StatArray = StatArrayType.Elite,
            Feats = new[] { "Scribe Scroll", "Spell Focus (Evocation)" },
            ExpectedCR = 1, ExpectedHP = 5
        });
        Register(new NPCTemplate {
            Id = "wizard_5", DisplayName = "Wizard 5",
            ClassName = "Wizard", ClassLevel = 5,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 5, ExpectedHP = 19
        });
        Register(new NPCTemplate {
            Id = "wizard_10", DisplayName = "Wizard 10",
            ClassName = "Wizard", ClassLevel = 10,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 10, ExpectedHP = 35
        });
        Register(new NPCTemplate {
            Id = "wizard_15", DisplayName = "Wizard 15",
            ClassName = "Wizard", ClassLevel = 15,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 15, ExpectedHP = 53
        });
        Register(new NPCTemplate {
            Id = "wizard_20", DisplayName = "Wizard 20",
            ClassName = "Wizard", ClassLevel = 20,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 20, ExpectedHP = 72
        });
        
        // Cleric at 1, 5, 10, 15, 20
        Register(new NPCTemplate {
            Id = "cleric_1", DisplayName = "Cleric 1",
            ClassName = "Cleric", ClassLevel = 1,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 1, ExpectedHP = 10
        });
        Register(new NPCTemplate {
            Id = "cleric_5", DisplayName = "Cleric 5",
            ClassName = "Cleric", ClassLevel = 5,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 5, ExpectedHP = 38
        });
        Register(new NPCTemplate {
            Id = "cleric_10", DisplayName = "Cleric 10",
            ClassName = "Cleric", ClassLevel = 10,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 10, ExpectedHP = 72
        });
        Register(new NPCTemplate {
            Id = "cleric_15", DisplayName = "Cleric 15",
            ClassName = "Cleric", ClassLevel = 15,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 15, ExpectedHP = 108
        });
        Register(new NPCTemplate {
            Id = "cleric_20", DisplayName = "Cleric 20",
            ClassName = "Cleric", ClassLevel = 20,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 20, ExpectedHP = 146
        });
        
        // Barbarian, Ranger, Paladin, Bard, Monk, Druid, Sorcerer at 1, 5, 10, 15, 20
        string[] remainingClasses = { "Barbarian", "Ranger", "Paladin", "Bard", "Monk", "Druid", "Sorcerer" };
        foreach (string cls in remainingClasses)
        {
            foreach (int lvl in new[] { 1, 5, 10, 15, 20 })
            {
                Register(new NPCTemplate {
                    Id = $"{cls.ToLower()}_{lvl}",
                    DisplayName = $"{cls} {lvl}",
                    ClassName = cls,
                    ClassLevel = lvl,
                    StatArray = StatArrayType.Elite,
                    ExpectedCR = lvl
                });
            }
        }
    }
    
    private static void RegisterNPCClassTemplates()
    {
        // NPC classes at 1, 5, 10
        string[] npcClasses = { "Warrior", "Adept", "Aristocrat", "Commoner", "Expert" };
        foreach (string cls in npcClasses)
        {
            foreach (int lvl in new[] { 1, 5, 10 })
            {
                StatArrayType array = cls == "Commoner" && lvl == 1 
                    ? StatArrayType.Basic 
                    : StatArrayType.Nonelite;
                    
                Register(new NPCTemplate {
                    Id = $"{cls.ToLower()}_{lvl}",
                    DisplayName = $"{cls} {lvl}",
                    ClassName = cls,
                    ClassLevel = lvl,
                    StatArray = array,
                    ExpectedCR = Mathf.Max(0.5f, lvl - 1)
                });
            }
        }
    }
    
    private static void RegisterClassedCreatureTemplates()
    {
        // Common classed creature combos
        Register(new NPCTemplate {
            Id = "ogre_barbarian_3",
            DisplayName = "Ogre Barbarian 3",
            BaseCreatureId = "ogre",
            ClassName = "Barbarian", ClassLevel = 3,
            StatArray = StatArrayType.Elite,
            Feats = new[] { "Power Attack", "Cleave", "Toughness" },
            ExpectedCR = 6
        });
        
        Register(new NPCTemplate {
            Id = "lizardfolk_druid_5",
            DisplayName = "Lizardfolk Druid 5",
            BaseCreatureId = "lizardfolk",
            ClassName = "Druid", ClassLevel = 5,
            StatArray = StatArrayType.Elite,
            Feats = new[] { "Natural Spell", "Augment Summoning", "Spell Focus (Conjuration)" },
            ExpectedCR = 6
        });
        
        Register(new NPCTemplate {
            Id = "kobold_sorcerer_4",
            DisplayName = "Kobold Sorcerer 4",
            BaseCreatureId = "kobold",
            ClassName = "Sorcerer", ClassLevel = 4,
            StatArray = StatArrayType.Elite,
            Feats = new[] { "Spell Focus (Evocation)", "Point Blank Shot" },
            ExpectedCR = 4
        });
        
        Register(new NPCTemplate {
            Id = "bugbear_rogue_3",
            DisplayName = "Bugbear Rogue 3",
            BaseCreatureId = "bugbear",
            ClassName = "Rogue", ClassLevel = 3,
            StatArray = StatArrayType.Elite,
            Feats = new[] { "Weapon Finesse", "Dodge", "Alertness" },
            ExpectedCR = 5
        });
        
        Register(new NPCTemplate {
            Id = "minotaur_fighter_2",
            DisplayName = "Minotaur Fighter 2",
            BaseCreatureId = "minotaur",
            ClassName = "Fighter", ClassLevel = 2,
            StatArray = StatArrayType.Elite,
            Feats = new[] { "Power Attack", "Great Cleave", "Improved Bull Rush", "Weapon Focus (Greataxe)" },
            ExpectedCR = 6
        });
        
        Register(new NPCTemplate {
            Id = "gnoll_ranger_3",
            DisplayName = "Gnoll Ranger 3",
            BaseCreatureId = "gnoll",
            ClassName = "Ranger", ClassLevel = 3,
            StatArray = StatArrayType.Elite,
            Feats = new[] { "Track", "Rapid Shot", "Point Blank Shot" },
            ExpectedCR = 4
        });
        
        Register(new NPCTemplate {
            Id = "troll_fighter_4",
            DisplayName = "Troll Fighter 4",
            BaseCreatureId = "troll",
            ClassName = "Fighter", ClassLevel = 4,
            StatArray = StatArrayType.Elite,
            ExpectedCR = 9
        });
        
        Register(new NPCTemplate {
            Id = "ogre_warrior_3",
            DisplayName = "Ogre Warrior 3",
            BaseCreatureId = "ogre",
            ClassName = "Warrior", ClassLevel = 3,
            StatArray = StatArrayType.Nonelite,
            ExpectedCR = 4 // Nonassociated: 3 + 3/2 = 4.5 → 4
        });
    }
    
    private static void Register(NPCTemplate template)
    {
        _templates[template.Id] = template;
    }
}
```

### 8.3 Usage Examples

```csharp
// Generate from pre-built template
CharacterStats ogreBarbarian = NPCTemplateDatabase.Generate("ogre_barbarian_3");

// Generate on-the-fly
CharacterStats trollWizard = NPCTemplateDatabase.Generate("troll", "Wizard", 6);

// Generate a standard NPC
CharacterStats guardCaptain = NPCTemplateDatabase.Generate("warrior_5");

// List all Fighter templates
List<string> fighterTemplates = NPCTemplateDatabase.GetTemplateIds(classFilter: "Fighter");
// → ["fighter_1", "fighter_5", "fighter_10", "fighter_15", "fighter_20", "minotaur_fighter_2", "troll_fighter_4"]
```

---

*End of NPC Templates by Level*
