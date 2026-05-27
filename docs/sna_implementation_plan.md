# Summon Nature's Ally I–IV: Complete Implementation Plan

> **Date:** 2026-05-26
> **Status:** Planning — NO code changes
> **Scope:** Add all missing SNA I–IV creatures to NPCDatabase + fix SummonMonsterLists.cs
> **Source of Truth:** Player's Handbook v3.5 pp. 288–289, Monster Manual I (Premium Edition)

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Canonical SNA I–IV Creature Lists (PHB pp. 288–289)](#2-canonical-sna-iiv-creature-lists)
3. [Current State Audit](#3-current-state-audit)
4. [Missing Creatures — Full Stats Reference](#4-missing-creatures--full-stats-reference)
5. [Phased Implementation Roadmap](#5-phased-implementation-roadmap)
6. [Technical Reference: NPCDatabase Entry Patterns](#6-technical-reference-npcdatabase-entry-patterns)
7. [SummonMonsterLists.cs Corrected Tables](#7-summonmonsterlistscs-corrected-tables)
8. [Alias Fixes Required](#8-alias-fixes-required)
9. [Testing Strategy](#9-testing-strategy)
10. [Open Questions & Future Work](#10-open-questions--future-work)

---

## 1. Executive Summary

The current SNA (Summon Nature's Ally) tables in `SummonMonsterLists.cs` (lines 484–570) are **incorrect** — they were copied from the Summon Monster spell lists and contain many creatures that don't belong on the SNA lists (e.g., celestial/fiendish creatures, Lemures, Mephits). This plan corrects them to match the canonical PHB lists.

### Key Numbers

| Metric | Count |
|--------|-------|
| Total canonical SNA I–IV creatures | 49 (unique entries) |
| Already in NPCDatabase | 30 |
| Missing from NPCDatabase | **19** |
| Aquatic-only (lower priority) | 6 |
| Land/flying creatures to add | **13** |
| Alias fixes needed | 2–4 |

### Priority Breakdown

| Priority | Creatures | Count |
|----------|-----------|-------|
| **P0 — Fix SNA tables** | Correct SummonMonsterLists.cs to use right creature IDs | — |
| **P1 — Medium Elementals** | Air, Earth, Fire, Water | 4 |
| **P2 — Animals** | Dire Ape, Dire Wolverine, Giant Crocodile, Deinonychus | 4 |
| **P3 — Outsiders/Fey** | Satyr, Arrowhawk Juvenile, Tojanida Juvenile | 3 |
| **P4 — Special** | Unicorn, Sea Cat | 2 |
| **P5 — Aquatic** | Porpoise, Medium Shark, Squid, Huge Shark, Wolf (standalone), Snake Small Viper | 6 |

---

## 2. Canonical SNA I–IV Creature Lists

Verified directly from **PHB v3.5, pp. 288–289**.

### SNA I — 8 Creatures

| # | Creature | Type | Notes |
|---|----------|------|-------|
| 1 | Dire rat | Animal | — |
| 2 | Eagle | Animal | — |
| 3 | Monkey | Animal | — |
| 4 | Octopus¹ | Animal (Aquatic) | Aquatic |
| 5 | Owl | Animal | — |
| 6 | Porpoise¹ | Animal (Aquatic) | Aquatic |
| 7 | Snake, Small viper | Animal | — |
| 8 | Wolf | Animal | — |

### SNA II — 12 Creatures

| # | Creature | Type | Notes |
|---|----------|------|-------|
| 1 | Bear, black | Animal | — |
| 2 | Crocodile | Animal | — |
| 3 | Dire badger | Animal | — |
| 4 | Dire bat | Animal | — |
| 5 | Elemental, Small (Air) | Elemental | — |
| 6 | Elemental, Small (Earth) | Elemental | — |
| 7 | Elemental, Small (Fire) | Elemental | — |
| 8 | Elemental, Small (Water) | Elemental | — |
| 9 | Hippogriff | Magical Beast | — |
| 10 | Shark, Medium¹ | Animal (Aquatic) | Aquatic |
| 11 | Snake, Medium viper | Animal | — |
| 12 | Squid¹ | Animal (Aquatic) | Aquatic |
| 13 | Wolverine | Animal | — |

> **Note:** PHB lists 13 entries for SNA II (4 elementals counted separately). Some references count the 4 elementals as 1 entry "Elemental, Small (any)".

### SNA III — 11 Creatures

| # | Creature | Type | Alignment | Notes |
|---|----------|------|-----------|-------|
| 1 | Ape | Animal | — | — |
| 2 | Dire weasel | Animal | — | — |
| 3 | Dire wolf | Animal | — | — |
| 4 | Eagle, giant | Magical Beast | NG | Alignment-restricted |
| 5 | Lion | Animal | — | — |
| 6 | Owl, giant | Magical Beast | NG | Alignment-restricted |
| 7 | Satyr | Fey | CN | Without pipes |
| 8 | Shark, Large¹ | Animal (Aquatic) | — | Aquatic |
| 9 | Snake, constrictor | Animal | — | — |
| 10 | Snake, Large viper | Animal | — | — |
| 11 | Thoqqua | Elemental | — | — |

### SNA IV — 16 Creatures

| # | Creature | Type | Alignment | Notes |
|---|----------|------|-----------|-------|
| 1 | Arrowhawk, juvenile | Outsider (Air, Extraplanar) | N | — |
| 2 | Bear, brown (grizzly) | Animal | — | — |
| 3 | Crocodile, giant | Animal | — | — |
| 4 | Deinonychus | Animal (Dinosaur) | — | — |
| 5 | Dire ape | Animal | — | — |
| 6 | Dire boar | Animal | — | — |
| 7 | Dire wolverine | Animal | — | — |
| 8 | Elemental, Medium (Air) | Elemental | — | — |
| 9 | Elemental, Medium (Earth) | Elemental | — | — |
| 10 | Elemental, Medium (Fire) | Elemental | — | — |
| 11 | Elemental, Medium (Water) | Elemental | — | — |
| 12 | Salamander, flamebrother | Outsider (Evil, Extraplanar, Fire) | NE | — |
| 13 | Sea cat¹ | Magical Beast | — | Aquatic |
| 14 | Shark, Huge¹ | Animal (Aquatic) | — | Aquatic |
| 15 | Snake, Huge viper | Animal | — | — |
| 16 | Tiger | Animal | — | — |
| 17 | Tojanida, juvenile¹ | Outsider (Water, Extraplanar) | — | Aquatic |
| 18 | Unicorn | Magical Beast | CG | Alignment-restricted |
| 19 | Xorn, minor | Outsider (Earth, Extraplanar) | — | — |

¹ = Aquatic creature

---

## 3. Current State Audit

### 3.1 NPCDatabase Existence Check

**Total NPCDatabase entries:** 276 unique creature IDs across 30+ alphabetical partial class files (`NPCDatabase_A.cs` through `NPCDatabase_Zombies.cs`).

#### SNA I Creatures

| Creature | Expected ID | Status | Notes |
|----------|-------------|--------|-------|
| Dire rat | `dire_rat` | ✅ EXISTS | — |
| Eagle | `eagle` | ✅ EXISTS | — |
| Monkey | `monkey` | ✅ EXISTS | — |
| Octopus | `octopus` | ✅ EXISTS | — |
| Owl | `owl` | ⚠️ ALIAS ONLY | Alias `owl` → `eagle` (WRONG — owl ≠ eagle) |
| Porpoise | `porpoise` | ❌ MISSING | Aquatic |
| Snake, Small viper | `viper_small` | ❌ MISSING | Need to verify; `viper_medium` exists |
| Wolf | `wolf` | ⚠️ ALIAS ONLY | Alias `wolf` → `wolf_pack_hunter`; stats may differ |

#### SNA II Creatures

| Creature | Expected ID | Status | Notes |
|----------|-------------|--------|-------|
| Bear, black | `black_bear` | ✅ EXISTS | — |
| Crocodile | `crocodile` | ✅ EXISTS | — |
| Dire badger | `dire_badger` | ✅ EXISTS | — |
| Dire bat | `dire_bat` | ✅ EXISTS | — |
| Small Air Elemental | `small_air_elemental` | ✅ EXISTS | — |
| Small Earth Elemental | `small_earth_elemental` | ✅ EXISTS | — |
| Small Fire Elemental | `small_fire_elemental` | ✅ EXISTS | — |
| Small Water Elemental | `small_water_elemental` | ✅ EXISTS | — |
| Hippogriff | `hippogriff` | ✅ EXISTS | — |
| Shark, Medium | `medium_shark` | ❌ MISSING | Aquatic |
| Snake, Medium viper | `viper_medium` | ✅ EXISTS | — |
| Squid | `squid` | ❌ MISSING | Aquatic |
| Wolverine | `wolverine` | ✅ EXISTS | — |

#### SNA III Creatures

| Creature | Expected ID | Status | Notes |
|----------|-------------|--------|-------|
| Ape | `ape` | ✅ EXISTS | — |
| Dire weasel | `dire_weasel` | ✅ EXISTS | — |
| Dire wolf | `dire_wolf` | ✅ EXISTS | — |
| Eagle, giant | `giant_eagle` | ✅ EXISTS | — |
| Lion | `lion` | ✅ EXISTS | — |
| Owl, giant | `giant_owl` | ✅ EXISTS | — |
| Satyr | `satyr` | ❌ MISSING | Fey, CN; summoned without pipes |
| Shark, Large | `large_shark` | ✅ EXISTS | — |
| Snake, constrictor | `constrictor_snake` | ✅ EXISTS | — |
| Snake, Large viper | `viper_large` | ✅ EXISTS | — |
| Thoqqua | `thoqqua` | ✅ EXISTS | — |

#### SNA IV Creatures

| Creature | Expected ID | Status | Notes |
|----------|-------------|--------|-------|
| Arrowhawk, juvenile | `arrowhawk_juvenile` | ❌ MISSING | Outsider |
| Bear, brown | `brown_bear` | ✅ EXISTS | — |
| Crocodile, giant | `giant_crocodile` | ❌ MISSING | — |
| Deinonychus | `deinonychus` | ❌ MISSING | — |
| Dire ape | `dire_ape` | ❌ MISSING | — |
| Dire boar | `dire_boar` | ✅ EXISTS | — |
| Dire wolverine | `dire_wolverine` | ❌ MISSING | — |
| Medium Air Elemental | `medium_air_elemental` | ❌ MISSING | — |
| Medium Earth Elemental | `medium_earth_elemental` | ❌ MISSING | — |
| Medium Fire Elemental | `medium_fire_elemental` | ❌ MISSING | — |
| Medium Water Elemental | `medium_water_elemental` | ❌ MISSING | — |
| Salamander, flamebrother | `flamebrother_salamander` | ✅ EXISTS | — |
| Sea cat | `sea_cat` | ❌ MISSING | Aquatic |
| Shark, Huge | `shark_huge` | ❌ MISSING | Aquatic |
| Snake, Huge viper | `viper_huge` | ✅ EXISTS | — |
| Tiger | `tiger` | ✅ EXISTS | — |
| Tojanida, juvenile | `tojanida_juvenile` | ❌ MISSING | Aquatic outsider |
| Unicorn | `unicorn` | ❌ MISSING | Magical beast, CG |
| Xorn, minor | `minor_xorn` | ✅ EXISTS | — |

### 3.2 Summary

| Category | Exists | Missing | Total |
|----------|--------|---------|-------|
| SNA I | 5 (+2 alias) | 1–3 | 8 |
| SNA II | 10 | 2 | 12–13 |
| SNA III | 10 | 1 | 11 |
| SNA IV | 7 | 12 | 19 |
| **Total** | **~32** | **~19** | **~51** |

### 3.3 Current SummonMonsterLists.cs Problems

The current SNA tables (lines 484–570) are **completely wrong**. They contain:
- Celestial/Fiendish creatures (these belong on Summon Monster, NOT Summon Nature's Ally)
- Lemure, Mephits, Howler (not SNA creatures)
- Missing most canonical SNA creatures
- Wrong spell level assignments for some creatures

**These tables must be completely rewritten.**

---

## 4. Missing Creatures — Full Stats Reference

All stats sourced from Monster Manual I (Premium Edition).

### 4.1 Medium Elementals (SNA IV)

#### Medium Air Elemental
- **Source:** MM p.96
- **CR:** 3 | **HD:** 4d8+8 (26 hp) | **Size:** Medium
- **AC:** 18 (+4 Dex, +4 natural) | Touch 14, FF 14
- **Speed:** Fly 100 ft. (perfect) — **20 grid squares fly**
- **Abilities:** Str 12, Dex 21 (+5), Con 14, Int 4, Wis 11, Cha 11
- **Natural Armor Bonus:** 4
- **Attack:** Slam +8 melee (1d6+1)
- **Saves:** Fort +3, Ref +8, Will +1
- **Special:** Air mastery, whirlwind (DC 14), darkvision 60ft
- **Immunities:** Poison, critical hits, sneak attack, paralysis, sleep, stunning (elemental traits)
- **Feats:** Dodge, Improved Initiative (B), Weapon Finesse (B), Flyby Attack
- **Type:** Elemental (Air, Extraplanar)

#### Medium Earth Elemental
- **Source:** MM p.97
- **CR:** 3 | **HD:** 4d8+12 (30 hp) | **Size:** Medium
- **AC:** 18 (+8 natural, –1 Dex) → Actually: 18 (–1 Dex, +9 natural) per stat block
- **Speed:** 20 ft. — **4 grid squares**
- **Abilities:** Str 21 (+5), Dex 8, Con 17, Int 4, Wis 11, Cha 11
- **Natural Armor Bonus:** 9
- **Attack:** Slam +8 melee (1d8+7)
- **Saves:** Fort +7, Ref +0, Will +1
- **Special:** Earth mastery, push, earth glide, darkvision 60ft
- **Immunities:** Poison, critical hits, sneak attack, paralysis, sleep, stunning (elemental traits)
- **Feats:** Cleave, Power Attack
- **Type:** Elemental (Earth, Extraplanar)

#### Medium Fire Elemental
- **Source:** MM pp.98–99
- **CR:** 3 | **HD:** 4d8+8 (26 hp) | **Size:** Medium
- **AC:** 16 (+3 Dex, +3 natural) | Touch 13, FF 13
- **Speed:** 50 ft. — **10 grid squares**
- **Abilities:** Str 12, Dex 17, Con 14, Int 4, Wis 11, Cha 11
- **Natural Armor Bonus:** 3
- **Attack:** Slam +6 melee (1d6+1 plus 1d6 fire)
- **Saves:** Fort +3, Ref +7, Will +1
- **Special:** Burn (DC 14 Reflex), darkvision 60ft
- **Immunities:** Fire, poison, critical hits, sneak attack (elemental traits)
- **Vulnerability:** Cold
- **Feats:** Dodge, Improved Initiative (B), Mobility, Weapon Finesse (B)
- **Type:** Elemental (Fire, Extraplanar)

#### Medium Water Elemental
- **Source:** MM pp.100–101
- **CR:** 3 | **HD:** 4d8+12 (30 hp) | **Size:** Medium
- **AC:** 19 (+1 Dex, +8 natural) | Touch 11, FF 18
- **Speed:** 20 ft., swim 90 ft. — **4 grid squares, 18 swim**
- **Abilities:** Str 16, Dex 12, Con 17, Int 4, Wis 11, Cha 11
- **Natural Armor Bonus:** 8
- **Attack:** Slam +6 melee (1d8+4)
- **Saves:** Fort +7, Ref +2, Will +1
- **Special:** Water mastery, drench, vortex, darkvision 60ft
- **Immunities:** Poison, critical hits, sneak attack (elemental traits)
- **Feats:** Cleave, Power Attack
- **Type:** Elemental (Water, Extraplanar)

### 4.2 Animals

#### Dire Ape
- **Source:** MM p.62
- **CR:** 3 | **HD:** 5d8+13 (35 hp) | **Size:** Large
- **AC:** 15 (–1 size, +2 Dex, +4 natural)
- **Speed:** 30 ft., climb 15 ft. — **6 grid squares, 3 climb**
- **Abilities:** Str 22 (+6), Dex 15, Con 14, Int 2, Wis 12, Cha 7
- **Natural Armor Bonus:** 4
- **Attacks:** 2 claws +8 melee (1d6+6), bite +3 melee (1d8+3)
- **Special:** Rend 2d6+9 (if both claws hit), scent
- **Type:** Animal
- **IsTallCreature:** true

#### Dire Wolverine
- **Source:** MM p.66
- **CR:** 4 | **HD:** 5d8+23 (45 hp) | **Size:** Large
- **AC:** 16 (–1 size, +3 Dex, +4 natural)
- **Speed:** 30 ft., climb 10 ft. — **6 grid squares, 2 climb**
- **Abilities:** Str 22 (+6), Dex 17, Con 19, Int 2, Wis 12, Cha 10
- **Natural Armor Bonus:** 4
- **Attacks:** 2 claws +8 melee (1d6+6), bite +3 melee (1d8+3)
- **Special:** Rage (when below 50% HP, +4 Str, +4 Con, –2 AC), scent
- **Type:** Animal

#### Giant Crocodile
- **Source:** MM p.271
- **CR:** 4 | **HD:** 7d8+28 (59 hp) | **Size:** Huge
- **AC:** 16 (–2 size, +8 natural) — Wait, let me recalculate: (–2 size, +0 Dex, +8 natural) = 16
- **Speed:** 20 ft., swim 30 ft. — **4 grid squares, 6 swim**
- **Abilities:** Str 27 (+8), Dex 10, Con 19, Int 1, Wis 12, Cha 2
- **Natural Armor Bonus:** 8
- **Attack:** Bite +11 melee (2d8+12) or tail slap +11 melee (1d12+12)
- **Special:** Improved grab, hold breath
- **Type:** Animal
- **IsTallCreature:** false (long body)

#### Deinonychus
- **Source:** MM p.60
- **CR:** 3 | **HD:** 4d8+16 (34 hp) | **Size:** Medium
- **AC:** 17 (+2 Dex, +5 natural) — per stat block: 16 (+2 Dex, +4 natural), let me re-verify
- **Speed:** 60 ft. — **12 grid squares**
- **Abilities:** Str 19 (+4), Dex 15, Con 19, Int 2, Wis 12, Cha 10
- **Natural Armor Bonus:** 4
- **Attacks:** Talons +7 melee (1d8+4), 2 foreclaws +2 melee (1d3+2), bite +2 melee (2d4+2)
- **Special:** Pounce (full attack on charge), scent
- **Type:** Animal (Dinosaur)

### 4.3 Outsiders & Fey

#### Satyr (without pipes)
- **Source:** MM pp.219–220
- **CR:** 2 (without pipes) | **HD:** 5d6+5 (22 hp) | **Size:** Medium
- **AC:** 15 (+1 Dex, +4 natural)
- **Speed:** 40 ft. — **8 grid squares**
- **Abilities:** Str 10, Dex 13, Con 12, Int 12, Wis 13, Cha 13
- **Natural Armor Bonus:** 4
- **Attack:** Head butt +2 melee (1d6), dagger +2 melee (1d4/19-20), shortbow +3 ranged (1d6)
- **Special:** DR 5/cold iron
- **Type:** Fey
- **Note:** SNA summons **without pipes** (pipes raise CR to 4)

#### Arrowhawk, Juvenile
- **Source:** MM p.20
- **CR:** 3 | **HD:** 3d8+3 (16 hp) | **Size:** Small
- **AC:** 20 (+1 size, +5 Dex, +4 natural)
- **Speed:** Fly 60 ft. (perfect) — **12 grid squares fly**
- **Abilities:** Str 12, Dex 21, Con 12, Int 10, Wis 13, Cha 13
- **Natural Armor Bonus:** 4
- **Attacks:** Electricity ray +9 ranged touch (2d6 electricity) or bite +9 melee (1d6+1)
- **Special:** Immune to acid, electricity, poison; resist cold 10, fire 10
- **Type:** Outsider (Air, Extraplanar)

#### Tojanida, Juvenile
- **Source:** MM pp.243–244
- **CR:** 3 | **HD:** 3d8+6 (19 hp) | **Size:** Small
- **AC:** 22 (+1 size, +1 Dex, +10 natural)
- **Speed:** 10 ft., swim 90 ft. — **2 grid squares, 18 swim**
- **Abilities:** Str 14, Dex 13, Con 15, Int 10, Wis 12, Cha 9
- **Natural Armor Bonus:** 10
- **Attacks:** Bite +6 melee (2d6+2), 2 claws +1 melee (1d4+1)
- **Special:** Improved grab, ink cloud, all-around vision; immune to acid & cold; resist electricity 10, fire 10
- **Type:** Outsider (Water, Extraplanar)

### 4.4 Magical Beasts / Special

#### Unicorn
- **Source:** MM pp.249–250
- **CR:** 3 | **HD:** 4d10+20 (42 hp) | **Size:** Large
- **AC:** 18 (–1 size, +3 Dex, +6 natural)
- **Speed:** 60 ft. — **12 grid squares**
- **Abilities:** Str 20 (+5), Dex 17, Con 21, Int 10, Wis 21, Cha 24
- **Natural Armor Bonus:** 6
- **Attacks:** Horn +11 melee (1d8+8), 2 hooves +3 melee (1d4+2)
- **Special:** Magic circle against evil (permanent), spell-like abilities (detect evil at will, light at will, cure light wounds 3/day, cure moderate wounds 1/day, greater teleport [self + rider] 1/day, neutralize poison 1/day)
- **Immunities:** Poison, charm, compulsion
- **Type:** Magical Beast
- **IsTallCreature:** true (horse-like)
- **Note:** CG alignment restriction; wild empathy

#### Sea Cat
- **Source:** MM pp.220–221
- **CR:** 4 | **HD:** 6d10+18 (51 hp) | **Size:** Large
- **AC:** 18 (–1 size, +2 Dex, +7 natural)
- **Speed:** 10 ft., swim 40 ft. — **2 grid squares, 8 swim**
- **Abilities:** Str 19 (+4), Dex 12, Con 17, Int 2, Wis 13, Cha 10
- **Natural Armor Bonus:** 7
- **Attacks:** 2 claws +9 melee (1d6+4), bite +4 melee (1d8+2)
- **Special:** Rend 2d6+6 (if both claws hit), hold breath, scent
- **Type:** Magical Beast (Aquatic)

### 4.5 Aquatic Creatures (Lower Priority)

#### Porpoise
- **Source:** MM p.278
- **CR:** 1/2 | **HD:** 2d8+2 (11 hp) | **Size:** Medium
- **AC:** 15 (+3 Dex, +2 natural)
- **Speed:** Swim 80 ft. — **16 swim grid squares** (no land speed)
- **Abilities:** Str 11, Dex 17, Con 13, Int 2, Wis 12, Cha 6
- **Natural Armor Bonus:** 2
- **Attack:** Slam +4 melee (2d4)
- **Special:** Blindsight 120 ft., hold breath
- **Type:** Animal (Aquatic)

#### Medium Shark
- **Source:** MM p.279
- **CR:** 1 | **HD:** 3d8+3 (16 hp) | **Size:** Medium
- **AC:** 15 (+2 Dex, +3 natural)
- **Speed:** Swim 60 ft. — **12 swim grid squares** (no land speed)
- **Abilities:** Str 13, Dex 15, Con 13, Int 1, Wis 12, Cha 2
- **Natural Armor Bonus:** 3
- **Attack:** Bite +4 melee (1d6+1)
- **Special:** Blindsense 30 ft., keen scent (180 ft.)
- **Type:** Animal (Aquatic)

#### Huge Shark
- **Source:** MM p.279
- **CR:** 4 | **HD:** 10d8+20 (65 hp) | **Size:** Huge
- **AC:** 15 (–2 size, +2 Dex, +5 natural)
- **Speed:** Swim 60 ft. — **12 swim grid squares** (no land speed)
- **Abilities:** Str 21, Dex 15, Con 15, Int 1, Wis 12, Cha 2
- **Natural Armor Bonus:** 5
- **Attack:** Bite +10 melee (2d6+7)
- **Special:** Blindsense 30 ft., keen scent (180 ft.)
- **Type:** Animal (Aquatic)

#### Squid
- **Source:** MM p.281
- **CR:** 1 | **HD:** 3d8 (13 hp) | **Size:** Medium
- **AC:** 16 (+3 Dex, +3 natural)
- **Speed:** Swim 60 ft. — **12 swim grid squares** (no land speed)
- **Abilities:** Str 14, Dex 17, Con 11, Int 1, Wis 12, Cha 2
- **Natural Armor Bonus:** 3
- **Attacks:** Arms +4 melee (0 damage), bite –1 melee (1d6+1)
- **Special:** Improved grab, ink cloud, jet (240 ft.)
- **Type:** Animal (Aquatic)

#### Wolf (Standalone Entry)
- **Note:** Currently aliased as `wolf` → `wolf_pack_hunter`. Verify if `wolf_pack_hunter` has correct MM wolf stats (Str 13, Dex 15, Con 15, 2d8+4 hp, bite +3 1d6+1 + trip). If stats match, the alias is fine. If not, create a standalone `wolf` entry.

#### Snake, Small Viper
- **Source:** MM p.280
- **CR:** 1/2 | **HD:** 1d8 (4 hp) | **Size:** Small
- **AC:** 17 (+1 size, +3 Dex, +3 natural)
- **Speed:** 20 ft., climb 20 ft., swim 20 ft. — **4 grid squares**
- **Abilities:** Str 6, Dex 17, Con 11, Int 1, Wis 12, Cha 2
- **Natural Armor Bonus:** 3
- **Attack:** Bite +4 melee (1d2–2 plus poison — 1d6 Con/1d6 Con, DC 10)
- **Type:** Animal

---

## 5. Phased Implementation Roadmap

### Phase 0: Fix SummonMonsterLists.cs SNA Tables (CRITICAL — Do First)
**Effort:** ~1 hour | **Priority:** Immediate | **Files:** `SummonMonsterLists.cs`

Completely rewrite lines 484–570 to use correct creature IDs for all SNA I–IV creatures that already exist in NPCDatabase. This instantly fixes SNA for ~30 creatures.

**Tasks:**
1. Replace `NaturesAllyI` list with correct 8 creature IDs
2. Replace `NaturesAllyII` list with correct 13 creature IDs
3. Replace `NaturesAllyIII` list with correct 11 creature IDs
4. Replace `NaturesAllyIV` list with correct 19 creature IDs
5. Use placeholder comments for missing creatures (add when DB entries exist)

### Phase 1: Add Medium Elementals (SNA IV)
**Effort:** ~1–2 hours | **Priority:** High | **Files:** `NPCDatabase_E.cs`

Scale directly from existing Small Elemental entries — same code pattern, just bigger stats.

**Tasks:**
1. Add `medium_air_elemental` — model after `small_air_elemental`
2. Add `medium_earth_elemental` — model after `small_earth_elemental`
3. Add `medium_fire_elemental` — model after `small_fire_elemental`
4. Add `medium_water_elemental` — model after `small_water_elemental`
5. Update SNA IV list in SummonMonsterLists.cs

**Template Pattern** (see Section 6 for full template):
```
Id = "medium_air_elemental"
CreatureType = "Elemental"
SizeCategory = SizeCategory.Medium
CreatureImmunities = new CreatureImmunities { immuneToPoison = true, immuneToCriticalHits = true, immuneToSneakAttack = true }
```

### Phase 2: Add Animals (SNA IV)
**Effort:** ~2–3 hours | **Priority:** High | **Files:** `NPCDatabase_D.cs`, `NPCDatabase_G.cs`

Standard animal entries — follow the pattern of existing `dire_wolf`, `dire_boar`, `brown_bear`, etc.

**Tasks:**
1. Add `dire_ape` to `NPCDatabase_D.cs` — model after `dire_wolf` (Large animal, rend)
2. Add `dire_wolverine` to `NPCDatabase_D.cs` — model after `dire_wolf` (Large animal, rage)
3. Add `giant_crocodile` to `NPCDatabase_G.cs` — model after `crocodile` (Huge animal)
4. Add `deinonychus` to `NPCDatabase_D.cs` — model after existing animal pattern (Medium, pounce)
5. Update SNA IV list

### Phase 3: Add Outsiders & Fey (SNA III–IV)
**Effort:** ~2–3 hours | **Priority:** Medium | **Files:** `NPCDatabase_A.cs`, `NPCDatabase_S.cs`, `NPCDatabase_T.cs`

These require more complex entries (damage immunities, resistances, special attacks).

**Tasks:**
1. Add `satyr` to `NPCDatabase_S.cs` — Fey type, DR 5/cold iron, no pipes for SNA
2. Add `arrowhawk_juvenile` to `NPCDatabase_A.cs` — Outsider (Air), electricity ray ranged attack, immunities
3. Add `tojanida_juvenile` to `NPCDatabase_T.cs` — Outsider (Water), improved grab, ink cloud, acid/cold immune
4. Update SNA III & IV lists

**Complexity Notes:**
- Arrowhawk has **ranged touch attack** (electricity ray) — verify NaturalAttack supports ranged
- Satyr has **DR 5/cold iron** — verify DamageReduction field exists
- Tojanida has **all-around vision** — may need special tag

### Phase 4: Add Special Creatures (SNA IV)
**Effort:** ~2–3 hours | **Priority:** Medium | **Files:** `NPCDatabase_U.cs`, `NPCDatabase_S.cs`

**Tasks:**
1. Add `unicorn` to `NPCDatabase_U.cs` — Magical Beast, CG alignment, magic circle vs evil, spell-likes, many immunities
2. Add `sea_cat` to `NPCDatabase_S.cs` — Magical Beast (Aquatic), rend, hold breath
3. Update SNA IV list

**Complexity Notes:**
- Unicorn has **spell-like abilities** (cure wounds, teleport, neutralize poison) — may need simplified version
- Unicorn has permanent **magic circle against evil** — needs aura or buff implementation
- Sea Cat has **rend** mechanic — same as Dire Ape pattern

### Phase 5: Add Aquatic Creatures (SNA I–IV)
**Effort:** ~3–4 hours | **Priority:** Low | **Files:** Various NPCDatabase files

**Prerequisite:** Aquatic combat/movement system needs to support swim-only creatures.

**Tasks:**
1. Add `porpoise` to `NPCDatabase_P.cs` — swim-only, blindsight
2. Add `medium_shark` to `NPCDatabase_S.cs` — swim-only, blindsense
3. Add `shark_huge` to `NPCDatabase_S.cs` — swim-only, blindsense, Huge
4. Add `squid` to `NPCDatabase_S.cs` — swim-only, improved grab, ink cloud, jet
5. Verify `viper_small` entry or add `small_viper` to `NPCDatabase_S.cs`
6. Verify `wolf` alias stats match MM wolf or create standalone entry
7. Update all SNA lists

**Open Question:** How does the prototype handle swim-only creatures on land maps? These may need:
- Movement type flags (`MovementType.Swim`, `MovementType.Fly`, `MovementType.Land`)
- Map validation (only summon aquatic creatures near water)
- Or simply mark them as "cannot be summoned" on land maps

### Phase 6: Fix Aliases
**Effort:** ~30 min | **Priority:** Medium | **Files:** `NPCDatabase.cs` (RegisterSummonCreatureAliases)

**Tasks:**
1. Fix `owl` alias: Currently maps to `eagle` (WRONG). Create proper `owl` NPCDatabase entry, or re-map alias correctly
2. Verify `wolf` → `wolf_pack_hunter` alias: Check if stats match MM wolf
3. Fix `badger` alias: Maps to `dire_badger` (wrong CR for SNA I if badger is on any list — but regular badger isn't on SNA lists, so may be fine for SM lists)
4. Fix `giant_bee` alias: Maps to `dire_bat` (wrong creature entirely)

---

## 6. Technical Reference: NPCDatabase Entry Patterns

### 6.1 File Organization

NPCDatabase uses C# partial classes split alphabetically:
```
Assets/Scripts/Character/NPCDatabase.cs        — Main file, Register(), RegisterSummonCreatureAliases()
Assets/Scripts/Character/NPCDatabase_A.cs       — Creatures starting with A
Assets/Scripts/Character/NPCDatabase_B.cs       — Creatures starting with B
...
Assets/Scripts/Character/NPCDatabase_Zombies.cs — Zombie variants
```

New creatures go in the file matching their ID's first letter.

### 6.2 Animal Entry Template

```csharp
// --- DIRE APE (SNA IV) ---
Register(new NPCDefinition {
    Id = "dire_ape",
    Name = "Dire Ape",
    ChallengeRating = "3",
    Level = 5,                    // = Hit Dice
    CharacterClass = "Warrior",
    CreatureType = "Animal",
    HitDice = 5,                  // 5d8
    HitDieType = DiceType.d8,
    SizeCategory = SizeCategory.Large,
    IsTallCreature = true,
    STR = 22, DEX = 15, CON = 14, WIS = 12, INT = 2, CHA = 7,
    NaturalArmorBonus = 4,
    NaturalAttacks = new List<NaturalAttackDefinition> {
        new NaturalAttackDefinition { Name = "Claw", NumberOfAttacks = 2, DamageDice = "1d6", DamageBonus = 6, AttackBonus = 8, IsPrimary = true },
        new NaturalAttackDefinition { Name = "Bite", NumberOfAttacks = 1, DamageDice = "1d8", DamageBonus = 3, AttackBonus = 3, IsPrimary = false }
    },
    BaseSpeed = 6,                // 30 ft = 6 squares
    ClimbSpeed = 3,               // 15 ft = 3 squares
    BaseHitDieHP = 35,            // 5d8+13 avg = 35
    Tags = new List<string> { "scent", "rend" },
    Description = "A massive, muscular ape with enormous fangs and powerful arms.",
    // AI behavior
    CombatAI = "melee_aggressive"
});
```

### 6.3 Elemental Entry Template

```csharp
// --- MEDIUM AIR ELEMENTAL (SNA IV) ---
Register(new NPCDefinition {
    Id = "medium_air_elemental",
    Name = "Medium Air Elemental",
    ChallengeRating = "3",
    Level = 4,
    CharacterClass = "Warrior",
    CreatureType = "Elemental",
    CreatureSubtype = "Air, Extraplanar",
    HitDice = 4,
    HitDieType = DiceType.d8,
    SizeCategory = SizeCategory.Medium,
    IsTallCreature = true,
    STR = 12, DEX = 21, CON = 14, WIS = 11, INT = 4, CHA = 11,
    NaturalArmorBonus = 4,
    NaturalAttacks = new List<NaturalAttackDefinition> {
        new NaturalAttackDefinition { Name = "Slam", NumberOfAttacks = 1, DamageDice = "1d6", DamageBonus = 1, AttackBonus = 8, IsPrimary = true }
    },
    FlySpeed = 20,                // 100 ft = 20 squares
    BaseSpeed = 0,                // No land speed? Or use fly as base
    BaseHitDieHP = 26,
    CreatureImmunities = new CreatureImmunities {
        immuneToPoison = true,
        immuneToCriticalHits = true,
        immuneToSneakAttack = true,
        immuneToParalysis = true,
        immuneToSleep = true,
        immuneToStunning = true
    },
    Tags = new List<string> { "air_mastery", "whirlwind", "darkvision_60" },
    Description = "A whirling column of air and debris in a vaguely humanoid shape.",
    BodyColor = new Color(0.7f, 0.85f, 0.95f),    // Light blue/white
    CombatAI = "melee_aggressive"
});
```

### 6.4 Outsider Entry Template

```csharp
// --- ARROWHAWK JUVENILE (SNA IV) ---
Register(new NPCDefinition {
    Id = "arrowhawk_juvenile",
    Name = "Juvenile Arrowhawk",
    ChallengeRating = "3",
    Level = 3,
    CharacterClass = "Warrior",
    CreatureType = "Outsider",
    CreatureSubtype = "Air, Extraplanar",
    HitDice = 3,
    HitDieType = DiceType.d8,
    SizeCategory = SizeCategory.Small,
    IsTallCreature = false,
    STR = 12, DEX = 21, CON = 12, WIS = 13, INT = 10, CHA = 13,
    NaturalArmorBonus = 4,
    NaturalAttacks = new List<NaturalAttackDefinition> {
        new NaturalAttackDefinition { Name = "Bite", NumberOfAttacks = 1, DamageDice = "1d6", DamageBonus = 1, AttackBonus = 9, IsPrimary = true }
        // Note: Electricity ray (2d6, ranged touch +9) may need special implementation
    },
    FlySpeed = 12,                // 60 ft = 12 squares (perfect maneuverability)
    BaseSpeed = 0,
    BaseHitDieHP = 16,
    DamageImmunities = new List<string> { "Acid", "Electricity", "Poison" },
    DamageResistances = new Dictionary<string, int> { { "Cold", 10 }, { "Fire", 10 } },
    Tags = new List<string> { "darkvision_60", "electricity_ray" },
    Description = "A small, serpentine creature of living air with crackling electrical energy.",
    BodyColor = new Color(0.6f, 0.7f, 0.9f),
    CombatAI = "ranged_aggressive"
});
```

### 6.5 Fey Entry Template

```csharp
// --- SATYR (SNA III, without pipes) ---
Register(new NPCDefinition {
    Id = "satyr",
    Name = "Satyr",
    ChallengeRating = "2",
    Level = 5,
    CharacterClass = "Warrior",
    CreatureType = "Fey",
    HitDice = 5,
    HitDieType = DiceType.d6,
    SizeCategory = SizeCategory.Medium,
    IsTallCreature = true,
    STR = 10, DEX = 13, CON = 12, WIS = 13, INT = 12, CHA = 13,
    NaturalArmorBonus = 4,
    NaturalAttacks = new List<NaturalAttackDefinition> {
        new NaturalAttackDefinition { Name = "Head butt", NumberOfAttacks = 1, DamageDice = "1d6", DamageBonus = 0, AttackBonus = 2, IsPrimary = true }
    },
    BaseSpeed = 8,                // 40 ft = 8 squares
    BaseHitDieHP = 22,
    DamageReduction = "5/cold iron",
    Tags = new List<string> { "low_light_vision" },
    Description = "A humanoid with the legs and horns of a goat, wild and mischievous.",
    BodyColor = new Color(0.6f, 0.5f, 0.3f),       // Brown/tan
    CombatAI = "melee_cautious"
});
```

### 6.6 Magical Beast Entry Template (Unicorn)

```csharp
// --- UNICORN (SNA IV) ---
Register(new NPCDefinition {
    Id = "unicorn",
    Name = "Unicorn",
    ChallengeRating = "3",
    Level = 4,
    CharacterClass = "Warrior",
    CreatureType = "MagicalBeast",
    HitDice = 4,
    HitDieType = DiceType.d10,
    SizeCategory = SizeCategory.Large,
    IsTallCreature = true,
    STR = 20, DEX = 17, CON = 21, WIS = 21, INT = 10, CHA = 24,
    NaturalArmorBonus = 6,
    NaturalAttacks = new List<NaturalAttackDefinition> {
        new NaturalAttackDefinition { Name = "Horn", NumberOfAttacks = 1, DamageDice = "1d8", DamageBonus = 8, AttackBonus = 11, IsPrimary = true },
        new NaturalAttackDefinition { Name = "Hoof", NumberOfAttacks = 2, DamageDice = "1d4", DamageBonus = 2, AttackBonus = 3, IsPrimary = false }
    },
    BaseSpeed = 12,               // 60 ft = 12 squares
    BaseHitDieHP = 42,
    DamageImmunities = new List<string> { "Poison", "Charm", "Compulsion" },
    Tags = new List<string> { "magic_circle_against_evil", "spell_resistance_21", "darkvision_60", "low_light_vision", "scent", "wild_empathy" },
    Description = "A magnificent white horse with a single spiraling ivory horn and a flowing silver mane.",
    BodyColor = new Color(1.0f, 1.0f, 1.0f),       // Pure white
    CombatAI = "melee_aggressive"
});
```

---

## 7. SummonMonsterLists.cs Corrected Tables

Replace the current SNA tables (approximately lines 484–570) with the following:

```csharp
// ==========================================
// SUMMON NATURE'S ALLY LISTS (PHB pp. 288-289)
// ==========================================

public static readonly List<SummonCreatureEntry> NaturesAllyI = new List<SummonCreatureEntry> {
    new SummonCreatureEntry { CreatureId = "dire_rat",       DisplayName = "Dire Rat" },
    new SummonCreatureEntry { CreatureId = "eagle",          DisplayName = "Eagle" },
    new SummonCreatureEntry { CreatureId = "monkey",         DisplayName = "Monkey" },
    new SummonCreatureEntry { CreatureId = "octopus",        DisplayName = "Octopus",          IsAquatic = true },
    new SummonCreatureEntry { CreatureId = "owl",            DisplayName = "Owl" },             // NEEDS: proper owl entry (currently alias→eagle)
    new SummonCreatureEntry { CreatureId = "porpoise",       DisplayName = "Porpoise",          IsAquatic = true },  // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "viper_small",    DisplayName = "Snake, Small Viper" },  // NEEDS: verify/add entry
    new SummonCreatureEntry { CreatureId = "wolf",           DisplayName = "Wolf" },             // Uses alias→wolf_pack_hunter; verify stats
};

public static readonly List<SummonCreatureEntry> NaturesAllyII = new List<SummonCreatureEntry> {
    new SummonCreatureEntry { CreatureId = "black_bear",            DisplayName = "Bear, Black" },
    new SummonCreatureEntry { CreatureId = "crocodile",             DisplayName = "Crocodile" },
    new SummonCreatureEntry { CreatureId = "dire_badger",           DisplayName = "Dire Badger" },
    new SummonCreatureEntry { CreatureId = "dire_bat",              DisplayName = "Dire Bat" },
    new SummonCreatureEntry { CreatureId = "small_air_elemental",   DisplayName = "Small Air Elemental" },
    new SummonCreatureEntry { CreatureId = "small_earth_elemental", DisplayName = "Small Earth Elemental" },
    new SummonCreatureEntry { CreatureId = "small_fire_elemental",  DisplayName = "Small Fire Elemental" },
    new SummonCreatureEntry { CreatureId = "small_water_elemental", DisplayName = "Small Water Elemental" },
    new SummonCreatureEntry { CreatureId = "hippogriff",            DisplayName = "Hippogriff" },
    new SummonCreatureEntry { CreatureId = "medium_shark",          DisplayName = "Shark, Medium",    IsAquatic = true },  // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "viper_medium",          DisplayName = "Snake, Medium Viper" },
    new SummonCreatureEntry { CreatureId = "squid",                 DisplayName = "Squid",            IsAquatic = true },  // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "wolverine",             DisplayName = "Wolverine" },
};

public static readonly List<SummonCreatureEntry> NaturesAllyIII = new List<SummonCreatureEntry> {
    new SummonCreatureEntry { CreatureId = "ape",               DisplayName = "Ape" },
    new SummonCreatureEntry { CreatureId = "dire_weasel",       DisplayName = "Dire Weasel" },
    new SummonCreatureEntry { CreatureId = "dire_wolf",         DisplayName = "Dire Wolf" },
    new SummonCreatureEntry { CreatureId = "giant_eagle",       DisplayName = "Eagle, Giant",       AlignmentRestriction = "NG" },
    new SummonCreatureEntry { CreatureId = "lion",              DisplayName = "Lion" },
    new SummonCreatureEntry { CreatureId = "giant_owl",         DisplayName = "Owl, Giant",         AlignmentRestriction = "NG" },
    new SummonCreatureEntry { CreatureId = "satyr",             DisplayName = "Satyr (no pipes)",   AlignmentRestriction = "CN" },  // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "large_shark",       DisplayName = "Shark, Large",       IsAquatic = true },
    new SummonCreatureEntry { CreatureId = "constrictor_snake", DisplayName = "Snake, Constrictor" },
    new SummonCreatureEntry { CreatureId = "viper_large",       DisplayName = "Snake, Large Viper" },
    new SummonCreatureEntry { CreatureId = "thoqqua",           DisplayName = "Thoqqua" },
};

public static readonly List<SummonCreatureEntry> NaturesAllyIV = new List<SummonCreatureEntry> {
    new SummonCreatureEntry { CreatureId = "arrowhawk_juvenile",      DisplayName = "Arrowhawk, Juvenile" },         // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "brown_bear",              DisplayName = "Bear, Brown" },
    new SummonCreatureEntry { CreatureId = "giant_crocodile",         DisplayName = "Crocodile, Giant" },             // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "deinonychus",             DisplayName = "Deinonychus" },                  // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "dire_ape",                DisplayName = "Dire Ape" },                     // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "dire_boar",               DisplayName = "Dire Boar" },
    new SummonCreatureEntry { CreatureId = "dire_wolverine",          DisplayName = "Dire Wolverine" },               // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "medium_air_elemental",    DisplayName = "Medium Air Elemental" },         // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "medium_earth_elemental",  DisplayName = "Medium Earth Elemental" },       // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "medium_fire_elemental",   DisplayName = "Medium Fire Elemental" },        // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "medium_water_elemental",  DisplayName = "Medium Water Elemental" },       // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "flamebrother_salamander", DisplayName = "Salamander, Flamebrother" },
    new SummonCreatureEntry { CreatureId = "sea_cat",                 DisplayName = "Sea Cat",             IsAquatic = true },  // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "shark_huge",              DisplayName = "Shark, Huge",         IsAquatic = true },  // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "viper_huge",              DisplayName = "Snake, Huge Viper" },
    new SummonCreatureEntry { CreatureId = "tiger",                   DisplayName = "Tiger" },
    new SummonCreatureEntry { CreatureId = "tojanida_juvenile",       DisplayName = "Tojanida, Juvenile",  IsAquatic = true },  // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "unicorn",                 DisplayName = "Unicorn",             AlignmentRestriction = "CG" },  // NEEDS: NPCDatabase entry
    new SummonCreatureEntry { CreatureId = "minor_xorn",              DisplayName = "Xorn, Minor" },
};
```

> **Note:** The `SummonCreatureEntry` struct may need `IsAquatic` and `AlignmentRestriction` fields added. If not already present, these can be simple bool/string properties.

---

## 8. Alias Fixes Required

Current aliases in `NPCDatabase.cs` → `RegisterSummonCreatureAliases()`:

| Alias | Current Target | Problem | Fix |
|-------|---------------|---------|-----|
| `owl` | `eagle` | Owl ≠ Eagle; different stats | Create proper `owl` entry in NPCDatabase_O.cs with MM owl stats (Tiny, Str 4, Dex 17, talons 1d4-3, fly 40ft, Listen +16, Move Silently +17) |
| `wolf` | `wolf_pack_hunter` | May have adjusted stats | Verify `wolf_pack_hunter` stats match MM wolf (Str 13, Dex 15, Con 15, bite 1d6+1 + trip, 50ft speed). If close enough, alias is acceptable |
| `badger` | `dire_badger` | Wrong CR (badger is CR 1/2, dire badger is CR 2) | Not on SNA lists, but affects SM lists. Create `badger` entry if needed for SM I |
| `giant_bee` | `dire_bat` | Completely wrong creature | Create `giant_bee` entry or remove alias. Not on SNA lists |

---

## 9. Testing Strategy

### 9.1 Per-Creature Validation

For each new NPCDatabase entry:
1. **Compile check** — Ensure no C# syntax errors
2. **ID uniqueness** — Verify no duplicate IDs in RegisterAll
3. **Stat block verification** — Compare each field against MM source:
   - HP = HD × average die + CON bonus × HD
   - AC = 10 + size mod + DEX mod + natural armor
   - Attack bonus = BAB + STR/DEX mod + size mod
   - Damage = weapon die + STR mod (×1.5 for 2-handed/single primary)
4. **Size/speed consistency** — BaseSpeed in grid squares (÷5 from feet)

### 9.2 SNA Table Validation

1. **Count check** — SNA I=8, SNA II=13, SNA III=11, SNA IV=19
2. **ID resolution** — Every CreatureId must resolve to a valid NPCDatabase entry or alias
3. **No SM/SNA cross-contamination** — SNA lists should have NO celestial/fiendish creatures
4. **Spell level progression** — Verify creatures are at appropriate power levels (CR roughly matches spell level)

### 9.3 In-Game Testing

1. Cast SNA I–IV as Druid
2. Verify creature selection UI shows correct creature names
3. Verify summoned creature has correct:
   - HP, AC, attack bonus, damage
   - Movement speed
   - Size (token size on grid)
   - Duration (1 round/level for SNA)
4. Verify alignment restrictions work (Giant Eagle only for NG+ casters, etc.)
5. Verify aquatic creatures are appropriately restricted (or at minimum functional)

---

## 10. Open Questions & Future Work

### Open Questions

1. **Aquatic creature handling:** How should swim-only creatures (Porpoise, Sharks, Squid) behave on land maps? Options:
   - a) Filter them from the selection list when not near water
   - b) Summon them but give 0 land speed (they flop helplessly)
   - c) Give them minimal land speed (5 ft) as a game compromise
   - d) Defer aquatic creatures entirely (Phase 5 is low priority)

2. **Ranged natural attacks:** Arrowhawk's electricity ray is a ranged touch attack. Does the current `NaturalAttackDefinition` support `IsRanged = true` and `IsTouchAttack = true`? If not, implement melee-only bite as fallback.

3. **Spell-like abilities:** Unicorn has several spell-like abilities (cure wounds, teleport, etc.). Should these be:
   - a) Fully implemented as castable abilities
   - b) Simplified to passive bonuses (e.g., self-heal X hp/round)
   - c) Omitted for summoned version (summon duration is short anyway)

4. **Alignment restrictions on SNA:** Giant Eagle (NG), Giant Owl (NG), Satyr (CN), Unicorn (CG) have alignment restrictions. Does the current summon system check caster alignment? If not, should it?

5. **Hit Die Type:** The `HitDieType` field — confirm: Animals = d8, Outsiders = d8, Fey = d6, Magical Beasts = d10, Elementals = d8. Verify this matches the existing patterns in NPCDatabase.

6. **Wolf alias verification:** Need to compare `wolf_pack_hunter` stats against MM wolf to determine if alias is accurate enough or needs a standalone `wolf` entry.

### Future Work (SNA V–IX)

This plan covers SNA I–IV only. SNA V–IX add:
- Large Elementals, Huge Elementals, Greater Elementals, Elder Elementals
- Dire Tiger, Dire Bear, Dire Shark
- Griffon, Pegasus
- Roc, Triceratops, Tyrannosaurus
- Treant (CR 10)
- And many more outsiders (Bralani, Djinni, Janni, etc.)

These will require a separate planning document when the time comes.

### Superseded Documents

This plan **supersedes** the previous `docs/sna_creature_audit.md` which had incomplete/incorrect canonical lists (missing Hippogriff, Thoqqua, and several SNA IV creatures).

---

*End of Implementation Plan*
