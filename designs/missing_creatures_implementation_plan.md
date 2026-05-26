# Missing Creatures Implementation Plan
## D&D 3.5e NPCDatabase — Adding 74 Missing Creatures

**Project**: dnd35prototype  
**Date**: May 26, 2026  
**Current Coverage**: 124/198 creatures (62.6%)  
**Target Coverage**: 198/198 creatures (100%)  
**Missing Creatures**: 74

---

## Contents
- [Overview](#overview)
- [Implementation Phases](#implementation-phases)
  - [Phase 1: Quick Wins](#phase-1-quick-wins-name-mappings--simple-stat-blocks)
  - [Phase 2: Core Missing Creatures](#phase-2-core-missing-creatures)
  - [Phase 3: Template Systems](#phase-3-template-systems)
  - [Phase 4: Advanced Features](#phase-4-advanced-features-size-scaling--class-leveled-npcs)
- [Technical Architecture](#technical-architecture)
- [Code Structure Examples](#code-structure-examples)
- [Testing Strategy](#testing-strategy)
- [Success Metrics](#success-metrics)
- [Appendix: Complexity Breakdown](#appendix-complexity-breakdown)

---

## Overview

### Current State
The NPCDatabase has strong coverage of:
- **Dragons**: 60 variants (all age categories × 10 types)
- **Mephits**: All 10 types
- **Vermin**: Size-scaled centipedes (7), scorpions (3), spiders (3)
- **Templates**: 9 creature templates (Skeleton, Zombie, Lycanthrope variants, Celestial/Fiendish)
- **Classes**: 16 character classes via CreatureClassEngine
- **Spawner**: DungeonEncounterSpawner with dynamic class levels and template application

### Major Gaps
- **Aberrations**: 0 of 10 (Choker, Grick, Otyugh, Gauth, Mind Flayer, etc.)
- **Lycanthropes**: Template exists but missing 5 core variants (Wererat, Wereboar, etc.)
- **Critical Low-CR Creatures**: Darkmantle, Ghoul, Kobold, standard humanoid warriors
- **Hydras**: Need multi-head system (4 variants)
- **Special Abilities**: Gaze attacks, petrification, energy drain, disease variations
- **Size-Scaled Vermin**: 15 missing (Giant Constrictor Snake, Dire Boar, etc.)

### Implementation Philosophy
1. **No modifications to base creatures** — always clone before applying changes
2. **Reuse existing systems** — CreatureTemplateRegistry, CreatureClassEngine, DungeonEncounterSpawner
3. **Coding style**: No namespaces, 4-space indent, XML doc comments
4. **Alphabetical organization** — Creatures in NPCDatabase_[Letter].cs files
5. **Debug logging** — Use `Debug.Log` for registration tracking

---

## Implementation Phases

## Phase 1: Quick Wins (Name Mappings & Simple Stat Blocks)
**Goal**: Add 30 creatures in 2-3 days  
**Focus**: Simple stat blocks, name mappings, and creatures that require minimal special mechanics  
**Expected Coverage Improvement**: 62.6% → 77.8% (+15.2%)

### 1.1 Simple Name Mappings (20 creatures, ~4 hours)
These creatures exist but need ID mappings for encounter table compatibility.

| Creature | Mapping | File | Effort |
|----------|---------|------|--------|
| Goblin Warrior | `goblin` → `goblin_warrior` | NPCDatabase_G.cs | Alias |
| Orc Warrior | `orc_berserker` → `orc_warrior` | NPCDatabase_O.cs | Alias |
| Hobgoblin Warrior | `hobgoblin_sergeant` → `hobgoblin_warrior` | NPCDatabase_H.cs | Alias |
| Dire Wolf | Existing → `dire_wolf_mm` | NPCDatabaseCustom.cs | Alias |
| Brown Bear | Existing → `brown_bear_mm` | NPCDatabaseCustom.cs | Alias |
| Human Warrior 1-3 | Create variants | NPCDatabase_H.cs | 3 entries |

**Implementation**:
```csharp
// In RegisterCreatures_G() — NPCDatabase_G.cs
RegisterSummonAlias("goblin_warrior", "goblin");

// In RegisterCreatures_O() — NPCDatabase_O.cs
RegisterSummonAlias("orc_warrior", "orc_berserker");

// In RegisterCreatures_H() — NPCDatabase_H.cs
RegisterHumanWarrior1();
RegisterHumanWarrior2();
RegisterHumanWarrior3();
RegisterSummonAlias("hobgoblin_warrior", "hobgoblin_sergeant");
```

**Dependencies**: None  
**Testing**: Verify encounter table lookup, ensure no ID conflicts  
**Estimated Time**: 4 hours

---

### 1.2 Simple Animals (5 creatures, ~2 hours)
Basic animal stat blocks with minimal abilities.

| Creature | CR | Special Abilities | File |
|----------|-----|-------------------|------|
| Hyena | 1 | Trip on bite | NPCDatabase_H.cs |
| Monitor Lizard | 2 | Grab | NPCDatabase_M.cs |
| Giant Constrictor Snake | 5 | Constrict, improved grab | NPCDatabase_G.cs |
| Dire Boar | 4 | Ferocity | NPCDatabase_D.cs |
| Krenshar | 1 | Scare (skull illusion) | NPCDatabase_K.cs |

**Implementation Example** (Hyena):
```csharp
/// <summary>
/// Hyena (CR 1) — Medium animal.
/// MM 3.5e p.274. Pack hunter with trip attack.
/// </summary>
private static void RegisterHyena()
{
    Register(new NPCDefinition
    {
        Id = "hyena",
        Name = "Hyena",
        ChallengeRating = "1",
        Level = 2,
        CharacterClass = "Warrior",
        CreatureType = "Animal",
        HitDice = 2,
        SizeCategory = SizeCategory.Medium,
        IsTallCreature = false,
        STR = 14, DEX = 15, CON = 15, WIS = 13, INT = 2, CHA = 6,
        BAB = 1,
        NaturalArmorBonus = 2,
        NaturalAttacks = new List<NaturalAttackDefinition>
        {
            new NaturalAttackDefinition
            {
                Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1,
                BonusDamageSource = DamageBonusSource.StrengthOneAndHalf,
                Range = 1, IsPrimary = true,
                TripOnHit = true
            }
        },
        BaseSpeed = 10,
        BaseHitDieHP = 13,
        CreatureTags = new List<string> { "Animal", "MM35" },
        Feats = new List<string> { "Alertness" },
        HasScent = true,
        SpecialAbilities = new List<string>
        {
            "Low-light vision",
            "Scent",
            "Trip: On successful bite, can attempt free trip (no AoO)",
            "Skills: Hide +3, Listen +6, Spot +4"
        },
        EquipmentIds = new List<EquipmentSlotPair>(),
        BackpackItemIds = new List<string>(),
        AIBehavior = NPCAIBehavior.AggressiveMelee,
        AIProfileArchetype = NPCAIProfileArchetype.Animal,
        SpriteColor = new Color(0.75f, 0.65f, 0.45f, 1f),
        PanelColor = new Color(0.25f, 0.2f, 0.1f, 0.85f),
        NameColor = new Color(0.95f, 0.88f, 0.7f),
        Description = "Monster Manual hyena. Medium pack hunter with trip attack and scent tracking."
    });
}
```

**Dependencies**: Existing NaturalAttackDefinition, trip mechanics  
**Testing**: Verify trip on bite works in combat  
**Estimated Time**: 2 hours

---

### 1.3 Basic Humanoids (5 creatures, ~3 hours)
Low-CR humanoid warriors for common encounters.

| Creature | CR | Equipment | File |
|----------|-----|-----------|------|
| Kobold Warrior | 1/4 | Spear, sling | NPCDatabase_K.cs |
| Grimlock | 1 | Battleaxe, blindsight | NPCDatabase_G.cs |
| Skum | 2 | Trident | NPCDatabase_S.cs |
| Derro | 3 | Aklys, poison, spell-like | NPCDatabase_D.cs |
| Human Commoner 1 | 1/2 | Club, basic gear | NPCDatabase_H.cs |

**Implementation Example** (Kobold):
```csharp
/// <summary>
/// Kobold Warrior (CR 1/4) — Small humanoid (reptilian).
/// MM 3.5e p.161. Warrior 1. Light sensitivity, trap-making.
/// </summary>
private static void RegisterKoboldWarrior()
{
    Register(new NPCDefinition
    {
        Id = "kobold_warrior",
        Name = "Kobold Warrior",
        ChallengeRating = "1/4",
        Level = 1,
        CharacterClass = "Warrior",
        CreatureType = "Humanoid",
        HitDice = 1,
        SizeCategory = SizeCategory.Small,
        IsTallCreature = false,
        STR = 9, DEX = 13, CON = 10, WIS = 10, INT = 10, CHA = 8,
        BAB = 0,
        NaturalArmorBonus = 1,
        NaturalAttacks = new List<NaturalAttackDefinition>(),
        BaseSpeed = 6,
        BaseHitDieHP = 4,
        CreatureTags = new List<string> { "Humanoid", "Reptilian", "MM35" },
        Feats = new List<string> { "Alertness" },
        SpecialAbilities = new List<string>
        {
            "Darkvision 60 ft.",
            "Light Sensitivity: -1 penalty to attacks in bright sunlight",
            "Skills: Craft (trapmaking) +2, Hide +6, Listen +2, Move Silently +2, Profession (miner) +2, Search +2, Spot +2"
        },
        EquipmentIds = new List<EquipmentSlotPair>
        {
            new EquipmentSlotPair("main_hand", "spear"),
            new EquipmentSlotPair("off_hand", "light_wooden_shield"),
            new EquipmentSlotPair("ranged", "sling")
        },
        BackpackItemIds = new List<string>(),
        AIBehavior = NPCAIBehavior.RangedThenMelee,
        AIProfileArchetype = NPCAIProfileArchetype.SkirmisherRanged,
        SpriteColor = new Color(0.6f, 0.45f, 0.3f, 1f),
        PanelColor = new Color(0.2f, 0.15f, 0.1f, 0.85f),
        NameColor = new Color(0.9f, 0.75f, 0.55f),
        Description = "Monster Manual kobold warrior. Small reptilian humanoid with light sensitivity and trap expertise."
    });
}
```

**Dependencies**: Equipment system, light sensitivity mechanics (if implemented)  
**Testing**: Verify equipment loads, AI behavior  
**Estimated Time**: 3 hours

---

### Phase 1 Success Criteria
- [ ] 30 creatures added to NPCDatabase
- [ ] All creatures compile without errors
- [ ] Coverage improves from 62.6% to 77.8%
- [ ] Encounter tables can resolve all Phase 1 creature IDs
- [ ] Manual spawn test of 5 random Phase 1 creatures succeeds
- [ ] Debug.Log shows registration count increase

**Phase 1 Effort Summary**:  
- **Total creatures**: 30  
- **Estimated time**: 2-3 days (16-24 hours)  
- **Files modified**: NPCDatabase_D/G/H/K/M/O/S.cs (7 files)  
- **New systems required**: None  
- **Dependencies**: Existing equipment, AI systems

---

## Phase 2: Core Missing Creatures
**Goal**: Add 25 high-priority creatures in 4-5 days  
**Focus**: Critical dungeon encounter creatures with moderate complexity  
**Expected Coverage Improvement**: 77.8% → 90.4% (+12.6%)

### 2.1 Aberrations (10 creatures, ~8 hours)
Key aberrations with unique abilities.

| Creature | CR | Special Abilities | Complexity | File |
|----------|-----|-------------------|------------|------|
| Darkmantle | 1 | Darkness aura, grab | Medium | NPCDatabase_D.cs |
| Choker | 2 | Reach tentacles, improved grab | Medium | NPCDatabase_C.cs |
| Grick | 3 | Damage reduction 10/magic | Easy | NPCDatabase_G.cs |
| Otyugh | 4 | Disease, improved grab, tentacles | Medium | NPCDatabase_O.cs |
| Carrion Crawler | 4 | Paralysis tentacles (×8) | Medium | NPCDatabase_C.cs |
| Gibbering Mouther | 5 | Gibbering (confusion), ground manipulation | Hard | NPCDatabase_G.cs |
| Gauth | 6 | Eye rays (6 types, 1/round) | Hard | NPCDatabase_G.cs |
| Chuul | 7 | Paralytic tentacles, amphibious | Medium | NPCDatabase_C.cs |
| Destrachan | 8 | Sonic attacks (3 types) | Medium | NPCDatabase_D.cs |
| Mind Flayer | 8 | Mind blast, extract brain | Hard | NPCDatabase_M.cs |

**Implementation Example** (Darkmantle):
```csharp
/// <summary>
/// Darkmantle (CR 1) — Small magical beast.
/// MM 3.5e p.52. Cave-dwelling ambush predator that creates darkness.
/// </summary>
private static void RegisterDarkmantle()
{
    Register(new NPCDefinition
    {
        Id = "darkmantle",
        Name = "Darkmantle",
        ChallengeRating = "1",
        Level = 1,
        CharacterClass = "Warrior",
        CreatureType = "Magical Beast",
        HitDice = 1,
        SizeCategory = SizeCategory.Small,
        IsTallCreature = false,
        STR = 16, DEX = 15, CON = 13, WIS = 12, INT = 2, CHA = 10,
        BAB = 1,
        NaturalArmorBonus = 5,
        NaturalAttacks = new List<NaturalAttackDefinition>
        {
            new NaturalAttackDefinition
            {
                Name = "Slam", DamageDice = 4, DamageCount = 1, Count = 1,
                BonusDamageSource = DamageBonusSource.StrengthOneAndHalf,
                Range = 1, IsPrimary = true
            }
        },
        BaseSpeed = 4,
        BaseHitDieHP = 9,
        CreatureTags = new List<string> { "Magical Beast", "MM35" },
        Feats = new List<string> { "Improved Initiative" },
        HasBlindSense = true,
        BlindSenseRange = 90,
        SpecialAbilities = new List<string>
        {
            "Blindsight 90 ft.",
            "Darkness (Su): Once per day, 20 ft radius for 1 minute (CL 5)",
            "Improved Grab: On slam hit, can start grapple as free action",
            "Skills: Hide +10, Listen +4"
        },
        SpellLikeAbilities = new List<SpellLikeAbilityDefinition>
        {
            new SpellLikeAbilityDefinition
            {
                SpellId = "darkness",
                UsesPerDay = 1,
                CasterLevel = 5
            }
        },
        EquipmentIds = new List<EquipmentSlotPair>(),
        BackpackItemIds = new List<string>(),
        AIBehavior = NPCAIBehavior.AggressiveMelee,
        AIProfileArchetype = NPCAIProfileArchetype.Ambusher,
        SpriteColor = new Color(0.2f, 0.2f, 0.25f, 1f),
        PanelColor = new Color(0.1f, 0.1f, 0.15f, 0.9f),
        NameColor = new Color(0.6f, 0.6f, 0.7f),
        Description = "Monster Manual darkmantle. Small cave-dweller that cloaks itself in magical darkness before dropping on prey."
    });
}
```

**Implementation Example** (Gauth — Eye Rays):
```csharp
/// <summary>
/// Gauth (CR 6) — Medium aberration (beholder-kin).
/// MM 3.5e p.27. Lesser beholder with 6 eye rays and central eye stunning beam.
/// </summary>
private static void RegisterGauth()
{
    Register(new NPCDefinition
    {
        Id = "gauth",
        Name = "Gauth",
        ChallengeRating = "6",
        Level = 6,
        CharacterClass = "Warrior",
        CreatureType = "Aberration",
        HitDice = 6,
        SizeCategory = SizeCategory.Medium,
        IsTallCreature = false,
        STR = 10, DEX = 14, CON = 14, WIS = 14, INT = 13, CHA = 13,
        BAB = 4,
        NaturalArmorBonus = 9,
        NaturalAttacks = new List<NaturalAttackDefinition>
        {
            new NaturalAttackDefinition
            {
                Name = "Bite", DamageDice = 4, DamageCount = 2, Count = 1,
                BonusDamageSource = DamageBonusSource.Strength,
                Range = 1, IsPrimary = true
            }
        },
        BaseSpeed = 5,
        BaseHitDieHP = 45,
        CreatureTags = new List<string> { "Aberration", "MM35" },
        Feats = new List<string> { "Alertness", "Improved Initiative", "Iron Will" },
        SpecialAbilities = new List<string>
        {
            "All-Around Vision: Cannot be flanked, +4 Spot/Search",
            "Eye Rays (Su): 6 small eyes, each can fire once per round, 100 ft range, +8 ranged touch",
            "  1. Inflict Moderate Wounds (2d8+6 damage, Will DC 16 half)",
            "  2. Dispel Magic (CL 13, +13 dispel check)",
            "  3. Exhaustion Ray (Fort DC 16 or become exhausted)",
            "  4. Fear Ray (Will DC 16 or flee 1 minute)",
            "  5. Paralyzing Ray (Fort DC 16 or hold person 1d4+1 rounds)",
            "  6. Scorching Ray (4d6 fire damage, ranged touch)",
            "Central Eye (Su): Stunning beam 60 ft cone, 1/round, Fort DC 16 or stunned 1d4 rounds",
            "Flight 20 ft (good)",
            "Darkvision 60 ft.",
            "Skills: Hide +11, Listen +13, Search +14, Spot +13"
        },
        EquipmentIds = new List<EquipmentSlotPair>(),
        BackpackItemIds = new List<string>(),
        AIBehavior = NPCAIBehavior.RangedController,
        AIProfileArchetype = NPCAIProfileArchetype.TacticalCaster,
        SpriteColor = new Color(0.4f, 0.35f, 0.5f, 1f),
        PanelColor = new Color(0.15f, 0.1f, 0.2f, 0.9f),
        NameColor = new Color(0.8f, 0.7f, 0.9f),
        Description = "Monster Manual gauth. Lesser beholder with 6 magical eye rays and stunning central eye beam."
    });
}
```

**Dependencies**: 
- SpellLikeAbilityDefinition for Darkmantle darkness
- Eye ray attack system for Gauth (may need new AttackType enum)
- Improved grab mechanics

**Testing**: 
- Darkmantle spawns with darkness ability
- Gauth eye rays fire correctly (1/round rotation)
- Mind Flayer mind blast affects multiple targets

**Estimated Time**: 8 hours

---

### 2.2 Undead (5 creatures, ~4 hours)
Critical undead with paralysis, stench, and energy drain.

| Creature | CR | Special Abilities | File |
|----------|-----|-------------------|------|
| Ghoul | 1 | Paralysis touch, undead traits | NPCDatabase_G.cs |
| Ghast | 3 | Stench, paralysis, undead | NPCDatabase_G.cs |
| Wight | 3 | Energy drain (1 level), create spawn | NPCDatabase_W.cs |
| Vampire Spawn | 4 | Energy drain (2 levels), dominate | NPCDatabase_V.cs |
| Mohrg | 8 | Paralysis tongue, create zombies | NPCDatabase_M.cs |

**Implementation Example** (Ghoul):
```csharp
/// <summary>
/// Ghoul (CR 1) — Medium undead.
/// MM 3.5e p.118. Flesh-eating undead with paralysis touch.
/// </summary>
private static void RegisterGhoul()
{
    Register(new NPCDefinition
    {
        Id = "ghoul",
        Name = "Ghoul",
        ChallengeRating = "1",
        Level = 2,
        CharacterClass = "Warrior",
        CreatureType = "Undead",
        HitDice = 2,
        SizeCategory = SizeCategory.Medium,
        IsTallCreature = false,
        STR = 13, DEX = 15, CON = 0, WIS = 10, INT = 13, CHA = 12,
        BAB = 1,
        NaturalArmorBonus = 2,
        NaturalAttacks = new List<NaturalAttackDefinition>
        {
            new NaturalAttackDefinition
            {
                Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1,
                BonusDamageSource = DamageBonusSource.Strength,
                Range = 1, IsPrimary = true,
                HasParalysisOnHit = true, ParalysisDurationRounds = 4
            },
            new NaturalAttackDefinition
            {
                Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 2,
                BonusDamageSource = DamageBonusSource.StrengthHalf,
                Range = 1, IsPrimary = false,
                HasParalysisOnHit = true, ParalysisDurationRounds = 4
            }
        },
        BaseSpeed = 6,
        BaseHitDieHP = 13,
        CreatureTags = new List<string> { "Undead", "MM35" },
        Feats = new List<string> { "Multiattack" },
        HasDarkvision = true,
        SpecialAbilities = new List<string>
        {
            "Undead Traits: No Constitution, immunity to mind effects/poison/sleep/paralysis/stunning/disease/death effects",
            "Paralysis (Ex): Bite/claw hit, Fort DC 12 or paralyzed 1d4+1 rounds (elves immune)",
            "+2 turn resistance",
            "Darkvision 60 ft.",
            "Skills: Balance +6, Climb +5, Hide +6, Jump +5, Move Silently +6, Spot +5"
        },
        EquipmentIds = new List<EquipmentSlotPair>(),
        BackpackItemIds = new List<string>(),
        AIBehavior = NPCAIBehavior.AggressiveMelee,
        AIProfileArchetype = NPCAIProfileArchetype.Undead,
        SpriteColor = new Color(0.45f, 0.5f, 0.4f, 1f),
        PanelColor = new Color(0.15f, 0.2f, 0.15f, 0.9f),
        NameColor = new Color(0.75f, 0.85f, 0.7f),
        Description = "Monster Manual ghoul. Flesh-eating undead with paralytic bite and claws."
    });
}
```

**Implementation Example** (Ghast — with Stench):
```csharp
/// <summary>
/// Ghast (CR 3) — Medium undead.
/// MM 3.5e p.119. More powerful ghoul with stench ability and immunity to Turn Undead.
/// </summary>
private static void RegisterGhast()
{
    Register(new NPCDefinition
    {
        Id = "ghast",
        Name = "Ghast",
        ChallengeRating = "3",
        Level = 4,
        CharacterClass = "Warrior",
        CreatureType = "Undead",
        HitDice = 4,
        SizeCategory = SizeCategory.Medium,
        IsTallCreature = false,
        STR = 17, DEX = 17, CON = 0, WIS = 10, INT = 13, CHA = 16,
        BAB = 2,
        NaturalArmorBonus = 4,
        NaturalAttacks = new List<NaturalAttackDefinition>
        {
            new NaturalAttackDefinition
            {
                Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1,
                BonusDamageSource = DamageBonusSource.Strength,
                Range = 1, IsPrimary = true,
                HasParalysisOnHit = true, ParalysisDurationRounds = 6
            },
            new NaturalAttackDefinition
            {
                Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2,
                BonusDamageSource = DamageBonusSource.StrengthHalf,
                Range = 1, IsPrimary = false,
                HasParalysisOnHit = true, ParalysisDurationRounds = 6
            }
        },
        BaseSpeed = 6,
        BaseHitDieHP = 26,
        CreatureTags = new List<string> { "Undead", "MM35" },
        Feats = new List<string> { "Multiattack", "Toughness" },
        HasDarkvision = true,
        SpecialAbilities = new List<string>
        {
            "Undead Traits",
            "Stench (Ex): 10 ft aura. Fort DC 15 or sickened for 1d6+4 minutes",
            "Paralysis (Ex): Bite/claw hit, Fort DC 15 or paralyzed 1d4+4 rounds (no elf immunity)",
            "+4 turn resistance",
            "Darkvision 60 ft.",
            "Skills: Balance +7, Climb +7, Hide +7, Jump +7, Move Silently +7, Spot +7"
        },
        EquipmentIds = new List<EquipmentSlotPair>(),
        BackpackItemIds = new List<string>(),
        AIBehavior = NPCAIBehavior.AggressiveMelee,
        AIProfileArchetype = NPCAIProfileArchetype.Undead,
        SpriteColor = new Color(0.35f, 0.4f, 0.35f, 1f),
        PanelColor = new Color(0.12f, 0.15f, 0.12f, 0.92f),
        NameColor = new Color(0.65f, 0.75f, 0.65f),
        Description = "Monster Manual ghast. Powerful ghoul with nauseating stench and paralytic attacks."
    });
}
```

**Dependencies**: 
- Paralysis system (HasParalysisOnHit flag)
- Energy drain mechanics (for Wight/Vampire Spawn)
- Stench aura system (new)

**Testing**: Ghoul paralysis works on hit, Ghast stench affects nearby creatures  
**Estimated Time**: 4 hours

---

### 2.3 Magical Beasts (6 creatures, ~5 hours)
Iconic dungeon creatures.

| Creature | CR | Special Abilities | File |
|----------|-----|-------------------|------|
| Owlbear | 4 | Improved grab | NPCDatabase_O.cs |
| Ankheg | 3 | Spit acid, improved grab | NPCDatabase_A.cs |
| Basilisk | 5 | Petrifying gaze | NPCDatabase_B.cs |
| Displacer Beast | 4 | Displacement (50% miss) | NPCDatabase_D.cs |
| Manticore | 5 | Tail spike volley | NPCDatabase_M.cs |
| Phase Spider | 5 | Ethereal jaunt | NPCDatabase_P.cs |

**Implementation Example** (Owlbear):
```csharp
/// <summary>
/// Owlbear (CR 4) — Large magical beast.
/// MM 3.5e p.206. Bear-owl hybrid, aggressive and territorial.
/// </summary>
private static void RegisterOwlbear()
{
    Register(new NPCDefinition
    {
        Id = "owlbear",
        Name = "Owlbear",
        ChallengeRating = "4",
        Level = 5,
        CharacterClass = "Warrior",
        CreatureType = "Magical Beast",
        HitDice = 5,
        SizeCategory = SizeCategory.Large,
        IsTallCreature = true,
        STR = 21, DEX = 12, CON = 17, WIS = 12, INT = 2, CHA = 10,
        BAB = 5,
        NaturalArmorBonus = 5,
        NaturalAttacks = new List<NaturalAttackDefinition>
        {
            new NaturalAttackDefinition
            {
                Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2,
                BonusDamageSource = DamageBonusSource.Strength,
                Range = 2, IsPrimary = true
            },
            new NaturalAttackDefinition
            {
                Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1,
                BonusDamageSource = DamageBonusSource.StrengthHalf,
                Range = 2, IsPrimary = false
            }
        },
        BaseSpeed = 6,
        BaseHitDieHP = 42,
        CreatureTags = new List<string> { "Magical Beast", "MM35" },
        Feats = new List<string> { "Alertness", "Track" },
        HasScent = true,
        SpecialAbilities = new List<string>
        {
            "Low-light vision",
            "Scent",
            "Improved Grab (Ex): On claw hit, can start grapple as free action. If both claws hit, gets +4 to grapple and can rake on next round.",
            "Skills: Listen +8, Spot +8"
        },
        EquipmentIds = new List<EquipmentSlotPair>(),
        BackpackItemIds = new List<string>(),
        AIBehavior = NPCAIBehavior.AggressiveMelee,
        AIProfileArchetype = NPCAIProfileArchetype.Brute,
        SpriteColor = new Color(0.55f, 0.45f, 0.35f, 1f),
        PanelColor = new Color(0.2f, 0.15f, 0.1f, 0.88f),
        NameColor = new Color(0.85f, 0.75f, 0.65f),
        Description = "Monster Manual owlbear. Large territorial predator with ferocious grappling attacks."
    });
}
```

**Implementation Example** (Basilisk — Gaze Attack):
```csharp
/// <summary>
/// Basilisk (CR 5) — Medium magical beast.
/// MM 3.5e p.23. Reptilian creature with petrifying gaze.
/// </summary>
private static void RegisterBasilisk()
{
    Register(new NPCDefinition
    {
        Id = "basilisk",
        Name = "Basilisk",
        ChallengeRating = "5",
        Level = 6,
        CharacterClass = "Warrior",
        CreatureType = "Magical Beast",
        HitDice = 6,
        SizeCategory = SizeCategory.Medium,
        IsTallCreature = false,
        STR = 15, DEX = 8, CON = 15, WIS = 12, INT = 2, CHA = 11,
        BAB = 6,
        NaturalArmorBonus = 7,
        NaturalAttacks = new List<NaturalAttackDefinition>
        {
            new NaturalAttackDefinition
            {
                Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1,
                BonusDamageSource = DamageBonusSource.StrengthOneAndHalf,
                Range = 1, IsPrimary = true
            }
        },
        BaseSpeed = 4,
        BaseHitDieHP = 45,
        CreatureTags = new List<string> { "Magical Beast", "MM35" },
        Feats = new List<string> { "Alertness", "Blind-Fight", "Great Fortitude" },
        SpecialAbilities = new List<string>
        {
            "Petrifying Gaze (Su): 30 ft range, Fort DC 13 or turn to stone permanently",
            "Gaze can be avoided by averting eyes (-4 to attack basilisk) or closing eyes (50% miss chance, lose Dex bonus to AC)",
            "Basilisk's gaze also affects creatures with darkvision in darkness",
            "Darkvision 60 ft.",
            "Low-light vision",
            "Skills: Listen +7, Spot +7"
        },
        EquipmentIds = new List<EquipmentSlotPair>(),
        BackpackItemIds = new List<string>(),
        AIBehavior = NPCAIBehavior.AggressiveMelee,
        AIProfileArchetype = NPCAIProfileArchetype.Bruiser,
        SpriteColor = new Color(0.5f, 0.55f, 0.3f, 1f),
        PanelColor = new Color(0.18f, 0.2f, 0.1f, 0.9f),
        NameColor = new Color(0.8f, 0.85f, 0.6f),
        Description = "Monster Manual basilisk. Eight-legged reptile whose gaze turns victims to stone."
    });
}
```

**Dependencies**: 
- Improved grab (existing)
- Gaze attack system (new — needs GazeAttackDefinition)
- Displacement visual effect (for Displacer Beast)

**Testing**: Owlbear grapple works, Basilisk gaze triggers save  
**Estimated Time**: 5 hours

---

### 2.4 Oozes & Plants (4 creatures, ~3 hours)
Dungeon hazards with unique damage types.

| Creature | CR | Special Abilities | File |
|----------|-----|-------------------|------|
| Grey Ooze | 4 | Acid, corrosion | NPCDatabase_G.cs |
| Black Pudding | 7 | Acid, split, corrosion | NPCDatabase_B.cs |
| Violet Fungus | 3 | Rotting touch tentacles | NPCDatabase_V.cs |
| Phantom Fungus | 3 | Invisibility | NPCDatabase_P.cs |

**Implementation Example** (Grey Ooze):
```csharp
/// <summary>
/// Grey Ooze (CR 4) — Medium ooze.
/// MM 3.5e p.202. Acidic ooze that corrodes metal.
/// </summary>
private static void RegisterGreyOoze()
{
    Register(new NPCDefinition
    {
        Id = "grey_ooze",
        Name = "Grey Ooze",
        ChallengeRating = "4",
        Level = 3,
        CharacterClass = "Warrior",
        CreatureType = "Ooze",
        HitDice = 3,
        SizeCategory = SizeCategory.Medium,
        IsTallCreature = false,
        STR = 12, DEX = 1, CON = 21, WIS = 1, INT = 0, CHA = 1,
        BAB = 2,
        NaturalArmorBonus = 0,
        NaturalAttacks = new List<NaturalAttackDefinition>
        {
            new NaturalAttackDefinition
            {
                Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1,
                BonusDamageSource = DamageBonusSource.StrengthOneAndHalf,
                DamageType = DamageType.Acid,
                Range = 1, IsPrimary = true,
                AdditionalEffect = "1d6 acid damage"
            }
        },
        BaseSpeed = 2,
        BaseHitDieHP = 34,
        CreatureTags = new List<string> { "Ooze", "MM35" },
        Feats = new List<string>(),
        HasBlindSight = true,
        BlindSightRange = 60,
        SpecialAbilities = new List<string>
        {
            "Ooze Traits: Immune to mind effects, poison, sleep, paralysis, polymorph, stunning, critical hits, flanking",
            "Acid (Ex): 1d6 acid damage on slam hit plus 1d6 per round to armor/clothing",
            "Constrict (Ex): Automatic slam damage on successful grapple",
            "Improved Grab (Ex): On slam hit, can start grapple",
            "Transparent (Ex): DC 15 Spot check to notice when motionless",
            "Blindsight 60 ft.",
            "Immune to cold and fire"
        },
        EquipmentIds = new List<EquipmentSlotPair>(),
        BackpackItemIds = new List<string>(),
        AIBehavior = NPCAIBehavior.AggressiveMelee,
        AIProfileArchetype = NPCAIProfileArchetype.Mindless,
        SpriteColor = new Color(0.5f, 0.5f, 0.52f, 1f),
        PanelColor = new Color(0.25f, 0.25f, 0.27f, 0.85f),
        NameColor = new Color(0.7f, 0.7f, 0.75f),
        Description = "Monster Manual grey ooze. Acidic ooze that corrodes metal and is nearly transparent."
    });
}
```

**Dependencies**: Acid damage type, transparent mechanics  
**Testing**: Acid damage applies to equipment  
**Estimated Time**: 3 hours

---

### Phase 2 Success Criteria
- [ ] 25 creatures added (total: 55 of 74)
- [ ] Coverage improves to 90.4%
- [ ] All aberrations compile and spawn correctly
- [ ] Ghoul paralysis and Ghast stench work in combat
- [ ] Basilisk gaze triggers petrification save
- [ ] Eye rays (Gauth) fire 1/round
- [ ] Ooze acid damage applies correctly
- [ ] Encounter tables resolve all Phase 2 creature IDs

**Phase 2 Effort Summary**:  
- **Total creatures**: 25  
- **Estimated time**: 4-5 days (32-40 hours)  
- **Files modified**: NPCDatabase_A/B/C/D/G/K/M/O/P/V/W.cs (11 files)  
- **New systems required**: 
  - Gaze attack system (GazeAttackDefinition)
  - Stench aura system
  - Eye ray rotation system
  - Energy drain mechanics (if not existing)
- **Dependencies**: Existing paralysis, improved grab, blindsight

---

## Phase 3: Template Systems
**Goal**: Implement 3 template systems and 13 variants in 5-6 days  
**Focus**: Lycanthrope, Ghost, and multi-head systems  
**Expected Coverage Improvement**: 90.4% → 96.9% (+6.5%)

### 3.1 Lycanthrope Template Completion (5 creatures, ~12 hours)
The lycanthrope template framework exists. Need to add missing variants.

**Existing**: LycanthropeTemplate.cs, LycanthropeFactory with Werewolf/Werewolf Lord/Wererat/Wereboar/Weretiger/Werebear (already done per context)

**Missing Variants**:
| Creature | CR | Base Form | Animal Form | Status |
|----------|-----|-----------|-------------|--------|
| Wererat | 2 | Human Rogue 1 | Dire Rat | ✅ Exists |
| Werewolf | 3 | Human Warrior 1 | Wolf | ✅ Exists |
| Wereboar | 4 | Human Barbarian 1 | Boar | ✅ Exists |
| Werebear | 5 | Human Commoner 1 | Brown Bear | ✅ Exists |
| Weretiger | 5 | Human Noble 4 | Tiger | ✅ Exists |

**NOTE**: Based on NPCDatabase_Lycanthropes.cs review, all 5 core lycanthropes are already implemented via LycanthropeFactory! This phase focuses on:
1. Verifying template consistency
2. Adding lycanthrope encounter presets (already exist)
3. Creating "animal form" and "hybrid form" variants if needed

**Revised Phase 3.1**: Add Dire Wereboar and Afflicted variants (already done), focus on expanding template to other base creatures.

**New Goal**: Add 3 custom lycanthrope variants using existing template:
- Werewolf (Elf Ranger 3 base)
- Wererat (Halfling Rogue 2 base)
- Werebear (Dwarf Fighter 4 base)

**Implementation Example** (Custom Werewolf):
```csharp
// In LycanthropeFactory.cs, add new method:

/// <summary>
/// Elf Ranger Werewolf (CR 5) — Natural lycanthrope.
/// Elf ranger 3 + wolf hybrid form.
/// </summary>
public static NPCDefinition ElfRangerWerewolf()
{
    // Clone base elf ranger 3 (would need to create this first)
    NPCDefinition baseElf = NPCDatabase.Get("elf_ranger_3").Clone();
    NPCDefinition wolfBase = NPCDatabase.Get("wolf").Clone();
    
    return LycanthropeTemplate.Apply(
        baseHumanoid: baseElf,
        animalBase: wolfBase,
        lycanthropeId: "elf_ranger_werewolf",
        lycanthropeName: "Elf Ranger Werewolf",
        isAfflicted: false,
        challengeRating: "5"
    );
}

// Then register in NPCDatabase_Lycanthropes.cs:
Register(LycanthropeFactory.ElfRangerWerewolf());
```

**Dependencies**: 
- LycanthropeTemplate.Apply() (exists)
- Base creatures (elf_ranger_3, halfling_rogue_2, dwarf_fighter_4 — need to create)

**Testing**: 
- Custom lycanthrope spawns with correct stats
- DR 10/silver applies
- Hybrid form stats merge correctly

**Estimated Time**: 12 hours (includes creating 3 base NPC classes)

---

### 3.2 Hydra Multi-Head System (4 creatures, ~16 hours)
Hydras need a parametric system for variable head count affecting attacks and HP.

| Creature | CR | Heads | Breath | File |
|----------|-----|-------|--------|------|
| 5-headed Hydra | 4 | 5 | No | NPCDatabase_H.cs |
| 6-headed Hydra | 5 | 6 | No | NPCDatabase_H.cs |
| 7-headed Pyrohydra | 8 | 7 | Fire | NPCDatabase_H.cs |
| 8-headed Hydra | 7 | 8 | No | NPCDatabase_H.cs |

**Design**: Create HydraFactory with parametric head generation.

**New File**: `Assets/Scripts/Character/HydraFactory.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Factory for generating D&D 3.5e hydras with variable head counts.
/// MM 3.5e p.155-157.
/// 
/// Design:
///   - Base stats scale with head count
///   - Each head = 1 bite attack
///   - HD = 5 + head count
///   - CR scales: 5-head (CR 4), 6-head (CR 5), 7-head (CR 6), 8-head (CR 7)
///   - Pyrohydra variant has fire immunity and breath weapon
/// 
/// Fast Healing scales with heads:
///   - Standard: 10 + (5 × heads)
///   - Pyrohydra: 15 + (5 × heads)
/// </summary>
public static class HydraFactory
{
    /// <summary>
    /// Generate a standard hydra with specified head count.
    /// </summary>
    public static NPCDefinition CreateHydra(int headCount)
    {
        if (headCount < 5 || headCount > 12)
        {
            Debug.LogWarning($"[HydraFactory] Invalid head count {headCount}. Clamping to 5-12.");
            headCount = Mathf.Clamp(headCount, 5, 12);
        }

        int hd = 5 + headCount;
        int cr = headCount - 1; // 5-head = CR 4, 6-head = CR 5, etc.
        
        // STR scales slightly with size
        int str = 17 + (headCount - 5) / 2;
        
        // Natural attacks: 1 bite per head
        List<NaturalAttackDefinition> bites = new List<NaturalAttackDefinition>();
        for (int i = 0; i < headCount; i++)
        {
            bites.Add(new NaturalAttackDefinition
            {
                Name = "Bite",
                DamageDice = 10,
                DamageCount = 1,
                Count = 1,
                BonusDamageSource = DamageBonusSource.Strength,
                Range = 2,
                IsPrimary = true
            });
        }

        return new NPCDefinition
        {
            Id = $"hydra_{headCount}head",
            Name = $"{headCount}-Headed Hydra",
            ChallengeRating = cr.ToString(),
            Level = hd,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            HitDice = hd,
            SizeCategory = SizeCategory.Huge,
            IsTallCreature = true,
            STR = str, DEX = 12, CON = 20, WIS = 10, INT = 2, CHA = 9,
            BAB = hd,
            NaturalArmorBonus = 5,
            NaturalAttacks = bites,
            BaseSpeed = 4,
            BaseHitDieHP = (hd * 7) + (hd * 5), // d10 average = 7, +5 Con bonus
            CreatureTags = new List<string> { "Magical Beast", "MM35", "Hydra" },
            Feats = GenerateHydraFeats(headCount),
            SpecialAbilities = new List<string>
            {
                $"Combat Reflexes: Can make {headCount} AoOs per round",
                $"Fast Healing {10 + (headCount * 5)}: Regrows heads unless cauterized with fire/acid",
                "Low-light vision",
                "Scent",
                $"Skills: Listen +{hd + 2}, Spot +{hd + 2}, Swim +{hd + 8}"
            },
            HasScent = true,
            HasFastHealing = true,
            FastHealingRate = 10 + (headCount * 5),
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.4f, 0.6f, 0.3f, 1f),
            PanelColor = new Color(0.15f, 0.25f, 0.1f, 0.88f),
            NameColor = new Color(0.7f, 0.9f, 0.6f),
            Description = $"Monster Manual {headCount}-headed hydra. Huge multi-headed serpent with regenerating heads and relentless attacks."
        };
    }

    /// <summary>
    /// Generate a pyrohydra (fire-breathing variant).
    /// </summary>
    public static NPCDefinition CreatePyrohydra(int headCount)
    {
        NPCDefinition hydra = CreateHydra(headCount);
        
        hydra.Id = $"pyrohydra_{headCount}head";
        hydra.Name = $"{headCount}-Headed Pyrohydra";
        hydra.ChallengeRating = (headCount + 1).ToString(); // +1 CR for fire breath
        
        // Pyrohydra has better fast healing
        hydra.FastHealingRate = 15 + (headCount * 5);
        
        // Add fire immunity and breath weapon
        hydra.SpecialAbilities.Add("Fire Immunity");
        hydra.SpecialAbilities.Add($"Breath Weapon (Su): Each head can breathe 10 ft cone of fire (3d6 damage, Reflex DC {12 + headCount} half) 1/day");
        hydra.SpecialAbilities[0] = $"Combat Reflexes: Can make {headCount} AoOs per round";
        hydra.SpecialAbilities[1] = $"Fast Healing {15 + (headCount * 5)}: Regrows heads (not cauterized by fire)";
        
        hydra.CreatureTags.Add("Fire");
        hydra.SpriteColor = new Color(0.8f, 0.4f, 0.2f, 1f);
        hydra.PanelColor = new Color(0.3f, 0.15f, 0.1f, 0.88f);
        hydra.NameColor = new Color(1f, 0.7f, 0.4f);
        hydra.Description = $"Monster Manual {headCount}-headed pyrohydra. Fire-breathing hydra immune to flames.";
        
        return hydra;
    }

    private static List<string> GenerateHydraFeats(int headCount)
    {
        List<string> feats = new List<string>
        {
            "Combat Reflexes",
            "Iron Will",
            "Toughness"
        };
        
        // Additional feats based on HD
        int hd = 5 + headCount;
        if (hd >= 9) feats.Add("Weapon Focus (bite)");
        if (hd >= 12) feats.Add("Improved Initiative");
        
        return feats;
    }

    // ── Standard MM hydra presets ──

    public static NPCDefinition Hydra5Head() => CreateHydra(5);
    public static NPCDefinition Hydra6Head() => CreateHydra(6);
    public static NPCDefinition Hydra7Head() => CreateHydra(7);
    public static NPCDefinition Hydra8Head() => CreateHydra(8);
    
    public static NPCDefinition Pyrohydra7Head() => CreatePyrohydra(7);
}
```

**Register in NPCDatabase_H.cs**:
```csharp
private static void RegisterCreatures_H()
{
    // ... existing registrations ...
    
    // Hydras
    Register(HydraFactory.Hydra5Head());
    Register(HydraFactory.Hydra6Head());
    Register(HydraFactory.Hydra7Head());
    Register(HydraFactory.Hydra8Head());
    Register(HydraFactory.Pyrohydra7Head());
    
    Debug.Log("[NPCDatabase] Registered 5 hydra variants.");
}
```

**Dependencies**: 
- Fast healing system (HasFastHealing, FastHealingRate properties)
- Multiple identical attacks (NaturalAttackDefinition list)
- Combat Reflexes feat (for AoO limit)

**Testing**: 
- 5-head hydra has 5 bite attacks
- Fast healing applies each round
- Pyrohydra immune to fire
- CR scales correctly

**Estimated Time**: 16 hours (includes HydraFactory creation, testing, integration)

---

### 3.3 Ghost Template (4 creatures, ~12 hours)
Ghost template applies incorporeal undead properties to any creature.

**Design**: Create GhostTemplate that takes base creature and converts to incorporeal undead.

**Target Variants**:
- Ghost Human Fighter 3 (CR 5)
- Ghost Elf Wizard 5 (CR 7)
- Ghost Dwarf Cleric 7 (CR 9)
- Ghost Ogre (CR 6)

**New File**: `Assets/Scripts/Character/Templates/GhostTemplate.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ghost creature template for D&D 3.5e (MM p.117).
/// Converts any creature to an incorporeal undead with horrific appearance,
/// manifestation, and corrupting touch.
/// 
/// Template Changes:
///   - Type → Undead (retains all subtypes)
///   - Incorporeal: 50% miss chance, ignores AC/natural armor, flies
///   - +4 Dex, +3 Cha
///   - Turn resistance +4
///   - Horrific Appearance (Su): Fort save or age 1d4×10 years + frightened
///   - Corrupting Touch (Su): Touch attack ignores armor, Fortitude save or 1d4 Cha damage + 1d6 Con damage
///   - Manifestation: Can become visible/invisible at will
///   - Rejuvenation: Reforms 2d4 days after destruction unless laid to rest
///   - CR: Base +2
/// </summary>
public class GhostTemplate : ICreatureTemplate
{
    public string TemplateId => "ghost";
    public int ApplicationOrder => 10; // Applied early (undead conversion)

    public void ApplyToDefinition(NPCDefinition definition)
    {
        if (definition == null)
            return;

        AddTemplateId(definition, "ghost");

        // ── Type change ──
        definition.CreatureType = "Undead";
        if (!definition.CreatureTags.Contains("Incorporeal"))
            definition.CreatureTags.Add("Incorporeal");

        // ── Ability score adjustments ──
        definition.DEX += 4;
        definition.CHA += 3;
        definition.CON = 0; // Undead have no Constitution

        // ── Incorporeal traits ──
        definition.IsIncorporeal = true;
        definition.NaturalArmorBonus = 0; // Incorporeal ignores natural armor

        // ── Turn resistance ──
        definition.TurnResistance = (definition.TurnResistance ?? 0) + 4;

        // ── Special abilities ──
        int cr = CalculateCR(definition);
        int saveDC = 10 + (cr / 2) + GetChaModifier(definition.CHA);

        if (definition.SpecialAbilities == null)
            definition.SpecialAbilities = new List<string>();

        definition.SpecialAbilities.Insert(0, "Undead Traits: Immune to mind effects, poison, sleep, paralysis, stunning, disease, death effects, critical hits, nonlethal damage, ability drain, energy drain, fatigue, exhaustion, any effect requiring Fortitude save (unless affects objects)");
        definition.SpecialAbilities.Insert(1, "Incorporeal: 50% miss chance from non-magical attacks. Can pass through solid objects. Immune to non-magical weapons.");
        definition.SpecialAbilities.Insert(2, $"Horrific Appearance (Su): Living creatures within 60 ft that see ghost must make Fort DC {saveDC} or age 1d4×10 years and become frightened for 1d4+1 rounds");
        definition.SpecialAbilities.Insert(3, $"Corrupting Touch (Su): Melee touch attack, Fort DC {saveDC} or 1d4 Cha damage + 1d6 temporary Con damage (undead creatures take 2d4 damage instead, no save)");
        definition.SpecialAbilities.Insert(4, "Manifestation (Su): Can become ethereal or manifest as visible incorporeal form");
        definition.SpecialAbilities.Insert(5, "Rejuvenation (Su): Destroyed ghost returns 2d4 days later unless remains properly laid to rest");
        definition.SpecialAbilities.Insert(6, "+4 turn resistance");

        // ── Natural attacks → touch attacks ──
        if (definition.NaturalAttacks != null)
        {
            foreach (var attack in definition.NaturalAttacks)
            {
                attack.DamageType = DamageType.None; // Touch ignores physical damage
                attack.AdditionalEffect = $"Corrupting touch (Fort DC {saveDC})";
            }
        }

        // ── Feats: Add Alertness if not present ──
        if (definition.Feats == null)
            definition.Feats = new List<string>();
        if (!definition.Feats.Contains("Alertness"))
            definition.Feats.Add("Alertness");

        // ── CR adjustment ──
        int baseCR = ParseCR(definition.ChallengeRating);
        definition.ChallengeRating = (baseCR + 2).ToString();

        // ── Visual ──
        definition.SpriteColor = new Color(0.6f, 0.7f, 0.8f, 0.5f); // Translucent blue-white
        definition.PanelColor = new Color(0.15f, 0.2f, 0.3f, 0.75f);
        definition.NameColor = new Color(0.8f, 0.9f, 1f);

        // ── AI ──
        definition.AIBehavior = NPCAIBehavior.TacticalMelee;
        definition.AIProfileArchetype = NPCAIProfileArchetype.Undead;

        definition.Description = $"{definition.Name} (Ghost). {definition.Description}";

        Debug.Log($"[GhostTemplate] Applied to {definition.Name}. CR: {definition.ChallengeRating}");
    }

    private static void AddTemplateId(NPCDefinition definition, string templateId)
    {
        if (definition.AppliedTemplateIds == null)
            definition.AppliedTemplateIds = new List<string>();
        if (!definition.AppliedTemplateIds.Contains(templateId))
            definition.AppliedTemplateIds.Add(templateId);
    }

    private static int CalculateCR(NPCDefinition definition)
    {
        return ParseCR(definition.ChallengeRating);
    }

    private static int ParseCR(string cr)
    {
        if (string.IsNullOrEmpty(cr)) return 1;
        if (cr.Contains("/")) return 0; // Fractional CR
        if (int.TryParse(cr, out int result)) return result;
        return 1;
    }

    private static int GetChaModifier(int cha)
    {
        return (cha - 10) / 2;
    }
}
```

**Register in CreatureTemplateRegistry.cs**:
```csharp
private static readonly Dictionary<string, ICreatureTemplate> _templates = new Dictionary<string, ICreatureTemplate>(StringComparer.OrdinalIgnoreCase)
{
    // ... existing templates ...
    { "ghost", new GhostTemplate() },
};
```

**Create Ghost Variants in NPCDatabase_G.cs**:
```csharp
/// <summary>
/// Ghost template creature registrations.
/// </summary>
private static void RegisterGhostCreatures()
{
    // Ghost Human Fighter 3
    NPCDefinition ghostFighter = NPCDatabase.Get("human_fighter_3").Clone();
    ghostFighter.AppliedTemplateIds = new List<string> { "ghost" };
    ghostFighter.Id = "ghost_fighter_3";
    ghostFighter.Name = "Ghost Fighter";
    Register(CreatureTemplateRegistry.ApplyTemplatesClone(ghostFighter));

    // Ghost Elf Wizard 5
    NPCDefinition ghostWizard = NPCDatabase.Get("elf_wizard_5").Clone();
    ghostWizard.AppliedTemplateIds = new List<string> { "ghost" };
    ghostWizard.Id = "ghost_wizard_5";
    ghostWizard.Name = "Ghost Wizard";
    Register(CreatureTemplateRegistry.ApplyTemplatesClone(ghostWizard));

    // ... etc.
    
    Debug.Log("[NPCDatabase] Registered 4 ghost template variants.");
}
```

**Dependencies**: 
- IsIncorporeal property
- Incorporeal combat mechanics
- Touch attack system
- Ability score damage (Cha, Con)
- Rejuvenation mechanics

**Testing**: 
- Ghost spawns as incorporeal
- 50% miss chance applies
- Corrupting touch triggers save
- Horrific appearance affects nearby creatures

**Estimated Time**: 12 hours

---

### Phase 3 Success Criteria
- [ ] HydraFactory creates 4 hydra variants with correct head count
- [ ] Lycanthrope template supports 3 new custom variants
- [ ] GhostTemplate applies incorporeal undead traits correctly
- [ ] Coverage improves to 96.9% (total: 68 of 74)
- [ ] Hydra multi-bite attacks display correctly
- [ ] Ghost horrific appearance triggers within 60 ft
- [ ] All templates compile and integrate with DungeonEncounterSpawner

**Phase 3 Effort Summary**:  
- **Total creatures**: 13 (3 lycanthropes + 5 hydras + 4 ghosts)  
- **Estimated time**: 5-6 days (40-48 hours)  
- **Files created**: HydraFactory.cs, GhostTemplate.cs  
- **Files modified**: NPCDatabase_H.cs, NPCDatabase_G.cs, NPCDatabase_Lycanthropes.cs, CreatureTemplateRegistry.cs  
- **New systems required**: 
  - Multi-head attack generation
  - Ghost incorporeal mechanics
  - Ability score damage tracking
- **Dependencies**: Existing template framework, fast healing system

---

## Phase 4: Advanced Features (Size Scaling & Class-Leveled NPCs)
**Goal**: Add final 6 creatures with advanced mechanics in 2-3 days  
**Focus**: Size-scaled vermin, class-leveled humanoid NPCs  
**Expected Coverage Improvement**: 96.9% → 100% (+3.1%)

### 4.1 Size-Scaled Vermin (3 creatures, ~4 hours)
Using existing vermin scaling pattern from centipedes/scorpions/spiders.

| Creature | Base CR | Sizes Needed | File |
|----------|---------|--------------|------|
| Locust Swarm | 3 | Swarm (10 ft square) | NPCDatabase_L.cs |
| Centipede Swarm | 4 | Swarm (10 ft square) | NPCDatabase_C.cs |
| Hellwasp Swarm | 12 | Swarm (10 ft square) | NPCDatabase_H.cs |

**Implementation Example** (Locust Swarm):
```csharp
/// <summary>
/// Locust Swarm (CR 3) — Diminutive vermin swarm.
/// MM 3.5e p.237. Dense cloud of biting insects.
/// </summary>
private static void RegisterLocustSwarm()
{
    Register(new NPCDefinition
    {
        Id = "locust_swarm",
        Name = "Locust Swarm",
        ChallengeRating = "3",
        Level = 6,
        CharacterClass = "Warrior",
        CreatureType = "Vermin",
        HitDice = 6,
        SizeCategory = SizeCategory.Diminutive, // Swarm
        IsTallCreature = false,
        STR = 1, DEX = 17, CON = 10, WIS = 10, INT = 0, CHA = 2,
        BAB = 4,
        NaturalArmorBonus = 0,
        NaturalAttacks = new List<NaturalAttackDefinition>
        {
            new NaturalAttackDefinition
            {
                Name = "Swarm", DamageDice = 6, DamageCount = 2, Count = 1,
                BonusDamageSource = DamageBonusSource.None,
                Range = 0, IsPrimary = true,
                IsSwarmAttack = true
            }
        },
        BaseSpeed = 4,
        BaseHitDieHP = 27,
        CreatureTags = new List<string> { "Vermin", "Swarm", "MM35" },
        Feats = new List<string>(),
        HasDarkvision = true,
        SpecialAbilities = new List<string>
        {
            "Swarm Traits: Half damage from slashing/piercing, immune to weapon damage, grappling, tripping. Takes 1.5× damage from area effects.",
            "Distraction (Ex): Living creature in swarm must make Fort DC 12 or nauseated for 1 round",
            "Swarm Attack: 2d6 damage to all creatures in swarm's space",
            "Immune to mind effects (mindless)",
            "Darkvision 60 ft."
        },
        EquipmentIds = new List<EquipmentSlotPair>(),
        BackpackItemIds = new List<string>(),
        AIBehavior = NPCAIBehavior.AggressiveMelee,
        AIProfileArchetype = NPCAIProfileArchetype.Mindless,
        SpriteColor = new Color(0.65f, 0.55f, 0.3f, 0.8f),
        PanelColor = new Color(0.25f, 0.2f, 0.1f, 0.75f),
        NameColor = new Color(0.9f, 0.8f, 0.5f),
        Description = "Monster Manual locust swarm. Cloud of biting insects that engulfs and nauseates victims."
    });
}
```

**Dependencies**: 
- Swarm mechanics (IsSwarmAttack, swarm traits)
- Distraction effect (nausea condition)

**Testing**: Swarm damages all creatures in space, distraction triggers save  
**Estimated Time**: 4 hours

---

### 4.2 Class-Leveled Humanoid NPCs (8 creatures, ~8 hours)
Standard racial warriors for encounter variety.

| Creature | CR | Class | Equipment | File |
|----------|-----|-------|-----------|------|
| Elf Warrior 1 | 1/2 | Warrior | Longsword, longbow | NPCDatabase_E.cs |
| Dwarf Warrior 1 | 1/2 | Warrior | Waraxe, chainmail | NPCDatabase_D.cs |
| Halfling Warrior 1 | 1/2 | Warrior | Short sword, sling | NPCDatabase_H.cs |
| Drow Elf Warrior | 1 | Warrior | Rapier, hand crossbow, SR | NPCDatabase_D.cs |
| Duergar Warrior | 1 | Warrior | Warhammer, enlarge | NPCDatabase_D.cs |
| Svirfneblin Warrior | 1 | Warrior | Pick, spell-like, SR | NPCDatabase_S.cs |
| Hill Giant | 7 | Warrior | Rock throwing, greatclub | NPCDatabase_H.cs |
| Stone Giant | 8 | Warrior | Rock throwing, greatclub | NPCDatabase_S.cs |

**Implementation Example** (Drow Elf Warrior):
```csharp
/// <summary>
/// Drow Elf Warrior (CR 1) — Medium humanoid (elf).
/// MM 3.5e p.102. Warrior 1. Spell resistance, spell-like abilities, light blindness.
/// </summary>
private static void RegisterDrowWarrior()
{
    Register(new NPCDefinition
    {
        Id = "drow_warrior",
        Name = "Drow Warrior",
        ChallengeRating = "1",
        Level = 1,
        CharacterClass = "Warrior",
        CreatureType = "Humanoid",
        HitDice = 1,
        SizeCategory = SizeCategory.Medium,
        IsTallCreature = false,
        STR = 11, DEX = 15, CON = 10, WIS = 11, INT = 13, CHA = 10,
        BAB = 1,
        NaturalArmorBonus = 0,
        NaturalAttacks = new List<NaturalAttackDefinition>(),
        BaseSpeed = 6,
        BaseHitDieHP = 8,
        CreatureTags = new List<string> { "Humanoid", "Elf", "MM35" },
        Feats = new List<string> { "Weapon Finesse" },
        SpellResistance = 11 + 1, // SR 11 + class level
        HasDarkvision = true,
        SpecialAbilities = new List<string>
        {
            "Darkvision 120 ft.",
            "Light Blindness: Abrupt exposure to bright light blinds drow for 1 round; -1 circumstance penalty in bright light",
            "Spell-Like Abilities (Sp): 1/day—dancing lights, darkness, faerie fire (CL 1)",
            "Spell Resistance 12",
            "Immune to magic sleep",
            "+2 racial bonus on saves vs enchantment",
            "+2 racial bonus on Listen, Search, Spot checks",
            "Skills: Listen +2, Search +3, Spot +2"
        },
        SpellLikeAbilities = new List<SpellLikeAbilityDefinition>
        {
            new SpellLikeAbilityDefinition { SpellId = "dancing_lights", UsesPerDay = 1, CasterLevel = 1 },
            new SpellLikeAbilityDefinition { SpellId = "darkness", UsesPerDay = 1, CasterLevel = 1 },
            new SpellLikeAbilityDefinition { SpellId = "faerie_fire", UsesPerDay = 1, CasterLevel = 1 }
        },
        EquipmentIds = new List<EquipmentSlotPair>
        {
            new EquipmentSlotPair("main_hand", "rapier"),
            new EquipmentSlotPair("off_hand", "buckler"),
            new EquipmentSlotPair("ranged", "hand_crossbow"),
            new EquipmentSlotPair("armor", "chainmail")
        },
        BackpackItemIds = new List<string> { "drow_poison" },
        AIBehavior = NPCAIBehavior.RangedThenMelee,
        AIProfileArchetype = NPCAIProfileArchetype.TacticalWarrior,
        SpriteColor = new Color(0.3f, 0.3f, 0.35f, 1f),
        PanelColor = new Color(0.1f, 0.1f, 0.15f, 0.9f),
        NameColor = new Color(0.85f, 0.85f, 0.95f),
        Description = "Monster Manual drow warrior. Dark elf with spell resistance and light blindness."
    });
}
```

**Implementation Example** (Hill Giant):
```csharp
/// <summary>
/// Hill Giant (CR 7) — Large giant.
/// MM 3.5e p.123. Huge humanoid with rock throwing and powerful club attacks.
/// </summary>
private static void RegisterHillGiant()
{
    Register(new NPCDefinition
    {
        Id = "hill_giant",
        Name = "Hill Giant",
        ChallengeRating = "7",
        Level = 12,
        CharacterClass = "Warrior",
        CreatureType = "Giant",
        HitDice = 12,
        SizeCategory = SizeCategory.Large,
        IsTallCreature = true,
        STR = 25, DEX = 8, CON = 19, WIS = 10, INT = 6, CHA = 7,
        BAB = 9,
        NaturalArmorBonus = 6,
        NaturalAttacks = new List<NaturalAttackDefinition>
        {
            new NaturalAttackDefinition
            {
                Name = "Slam", DamageDice = 4, DamageCount = 1, Count = 2,
                BonusDamageSource = DamageBonusSource.Strength,
                Range = 2, IsPrimary = true
            }
        },
        BaseSpeed = 8,
        BaseHitDieHP = 102,
        CreatureTags = new List<string> { "Giant", "MM35" },
        Feats = new List<string> { "Cleave", "Improved Bull Rush", "Power Attack", "Weapon Focus (greatclub)" },
        HasLowLightVision = true,
        SpecialAbilities = new List<string>
        {
            "Rock Throwing (Ex): 100 ft range, 2d6+10 damage",
            "Rock Catching (Ex): Can catch Small, Medium, or Large rocks (or similar projectiles)",
            "Low-light vision",
            "Skills: Climb +10, Jump +5, Listen +2, Spot +6"
        },
        RangedAttacks = new List<RangedAttackDefinition>
        {
            new RangedAttackDefinition
            {
                Name = "Rock", Range = 100, DamageDice = 6, DamageCount = 2,
                BonusDamage = 10, AmmoCount = 999 // Unlimited rocks
            }
        },
        EquipmentIds = new List<EquipmentSlotPair>
        {
            new EquipmentSlotPair("main_hand", "greatclub_large"),
            new EquipmentSlotPair("armor", "hide_armor")
        },
        BackpackItemIds = new List<string> { "sack", "rocks" },
        AIBehavior = NPCAIBehavior.RangedThenMelee,
        AIProfileArchetype = NPCAIProfileArchetype.Brute,
        SpriteColor = new Color(0.6f, 0.55f, 0.45f, 1f),
        PanelColor = new Color(0.25f, 0.2f, 0.15f, 0.88f),
        NameColor = new Color(0.9f, 0.85f, 0.75f),
        Description = "Monster Manual hill giant. Large brutish giant with rock throwing and devastating club attacks."
    });
}
```

**Dependencies**: 
- Spell resistance system
- Spell-like abilities (darkness, faerie fire)
- Rock throwing (RangedAttackDefinition)
- Light blindness mechanics

**Testing**: 
- Drow SR applies vs spells
- Hill Giant rock throwing works at range
- Equipment loads correctly

**Estimated Time**: 8 hours

---

### Phase 4 Success Criteria
- [ ] 3 swarm variants compile and spawn
- [ ] 8 class-leveled humanoids compile and spawn
- [ ] Coverage reaches 100% (74 of 74 creatures)
- [ ] Swarm distraction effect triggers
- [ ] Drow spell resistance blocks appropriate spells
- [ ] Hill Giant rock throwing hits at 100 ft range
- [ ] All encounter tables resolve to valid creature IDs

**Phase 4 Effort Summary**:  
- **Total creatures**: 6 (3 swarms + 3 humanoids, counts vary based on interpretation)  
- **Estimated time**: 2-3 days (16-24 hours)  
- **Files modified**: NPCDatabase_C/D/E/H/L/S.cs (6 files)  
- **New systems required**: 
  - Rock throwing ranged attack
  - Light blindness condition
- **Dependencies**: Swarm mechanics, spell resistance system, equipment

---

## Technical Architecture

### Files Modified Summary

| File | Creatures Added | Phase |
|------|----------------|-------|
| NPCDatabase_A.cs | Ankheg | 2 |
| NPCDatabase_B.cs | Basilisk, Black Pudding | 2 |
| NPCDatabase_C.cs | Choker, Carrion Crawler, Chuul, Centipede Swarm | 2 |
| NPCDatabase_D.cs | Darkmantle, Destrachan, Displacer Beast, Dire Boar, Derro, Dwarf Warrior, Drow Warrior, Duergar Warrior | 1, 2, 4 |
| NPCDatabase_E.cs | Elf Warrior | 4 |
| NPCDatabase_G.cs | Ghoul, Ghast, Grick, Grey Ooze, Gauth, Gibbering Mouther, Grimlock, Giant Constrictor, Ghost variants | 1, 2, 3 |
| NPCDatabase_H.cs | Hyena, Hydra variants (5), Hill Giant, Halfling Warrior, Hellwasp Swarm | 1, 3, 4 |
| NPCDatabase_K.cs | Kobold Warrior, Krenshar | 1 |
| NPCDatabase_L.cs | Locust Swarm | 4 |
| NPCDatabase_M.cs | Monitor Lizard, Mind Flayer, Manticore, Mohrg | 1, 2 |
| NPCDatabase_O.cs | Otyugh, Owlbear | 2 |
| NPCDatabase_P.cs | Phase Spider, Phantom Fungus | 2 |
| NPCDatabase_S.cs | Skum, Svirfneblin Warrior, Stone Giant | 1, 4 |
| NPCDatabase_V.cs | Violet Fungus, Vampire Spawn | 2 |
| NPCDatabase_W.cs | Wight | 2 |

**Total Files Modified**: 16 NPCDatabase_*.cs files

### New Files Created

| File | Purpose | Phase |
|------|---------|-------|
| HydraFactory.cs | Parametric hydra generation with variable heads | 3 |
| GhostTemplate.cs | Ghost incorporeal undead template | 3 |

**Total New Files**: 2

### Helper Systems Needed

| System | Purpose | Implementation Location | Phase |
|--------|---------|------------------------|-------|
| Gaze Attack System | Basilisk, Bodak gaze attacks | NaturalAttackDefinition or new GazeAttackDefinition | 2 |
| Stench Aura | Ghast, Troglodyte | SpecialAbilities or new AuraEffect | 2 |
| Eye Ray Rotation | Gauth multi-ray attacks | SpecialAbilities or new EyeRaySystem | 2 |
| Energy Drain | Wight, Vampire Spawn level drain | NaturalAttackDefinition.HasEnergyDrain | 2 |
| Multi-Head System | Hydra head count → attacks/HP | HydraFactory parametric generation | 3 |
| Incorporeal Mechanics | Ghost 50% miss chance | IsIncorporeal flag + combat system | 3 |
| Ability Score Damage | Ghost corrupting touch | NaturalAttackDefinition.AbilityDamage | 3 |
| Rock Throwing | Giant ranged attacks | RangedAttackDefinition | 4 |
| Light Blindness | Drow, Kobold bright light penalty | SpecialAbilities or condition system | 4 |

**Total New Systems**: 9 (some may already exist)

### Integration Points with DungeonEncounterSpawner

All creatures integrate seamlessly with the existing spawner:

```csharp
// Example: Spawn a custom Drow encounter
var encounter = new EncounterDefinition("Drow Patrol")
    .AddCreatureWithClass("drow_warrior", "Ranger", 3, count: 1) // Leader
    .AddCreature("drow_warrior", count: 3); // Warriors

var result = DungeonEncounterSpawner.PrepareEncounter(encounter);
// result.EnemyIds → ready for SetupEnemyEncounter
```

**Template Application**:
```csharp
// Ghost template applied at spawn time
var encounter = new EncounterDefinition("Haunted Crypt")
    .AddCreatureWithTemplate("human_fighter_3", "ghost", count: 1)
    .AddCreature("skeleton_warrior", count: 4);

var result = DungeonEncounterSpawner.PrepareEncounter(encounter);
```

**Hydra Variants**:
```csharp
// Hydra with dynamic head count
var encounter = new EncounterDefinition("Hydra Lair")
    .AddCreature("hydra_7head", count: 1);
```

---

## Code Structure Examples

### Example 1: Simple Creature Addition (Hyena)

**File**: `NPCDatabase_H.cs`

```csharp
/// <summary>
/// Hyena (CR 1) — Medium animal.
/// MM 3.5e p.274. Pack hunter with trip attack.
/// </summary>
private static void RegisterHyena()
{
    Register(new NPCDefinition
    {
        Id = "hyena",
        Name = "Hyena",
        ChallengeRating = "1",
        Level = 2,
        CharacterClass = "Warrior",
        CreatureType = "Animal",
        HitDice = 2,
        SizeCategory = SizeCategory.Medium,
        IsTallCreature = false,
        STR = 14, DEX = 15, CON = 15, WIS = 13, INT = 2, CHA = 6,
        BAB = 1,
        NaturalArmorBonus = 2,
        NaturalAttacks = new List<NaturalAttackDefinition>
        {
            new NaturalAttackDefinition
            {
                Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1,
                BonusDamageSource = DamageBonusSource.StrengthOneAndHalf,
                Range = 1, IsPrimary = true,
                TripOnHit = true
            }
        },
        BaseSpeed = 10,
        BaseHitDieHP = 13,
        CreatureTags = new List<string> { "Animal", "MM35" },
        Feats = new List<string> { "Alertness" },
        HasScent = true,
        SpecialAbilities = new List<string>
        {
            "Low-light vision",
            "Scent",
            "Trip: On successful bite, can attempt free trip (no AoO)",
            "Skills: Hide +3, Listen +6, Spot +4"
        },
        EquipmentIds = new List<EquipmentSlotPair>(),
        BackpackItemIds = new List<string>(),
        AIBehavior = NPCAIBehavior.AggressiveMelee,
        AIProfileArchetype = NPCAIProfileArchetype.Animal,
        SpriteColor = new Color(0.75f, 0.65f, 0.45f, 1f),
        PanelColor = new Color(0.25f, 0.2f, 0.1f, 0.85f),
        NameColor = new Color(0.95f, 0.88f, 0.7f),
        Description = "Monster Manual hyena. Medium pack hunter with trip attack and scent tracking."
    });
}
```

**Then add to RegisterCreatures_H()**:
```csharp
private static void RegisterCreatures_H()
{
    // ... existing registrations ...
    RegisterHyena();
}
```

---

### Example 2: Lycanthrope Template Application (Custom Werewolf)

**File**: `LycanthropeFactory.cs`

```csharp
/// <summary>
/// Elf Ranger Werewolf (CR 5) — Natural lycanthrope.
/// Custom variant: Elf ranger 3 + wolf hybrid form.
/// </summary>
public static NPCDefinition ElfRangerWerewolf()
{
    // Step 1: Get base creatures
    NPCDefinition baseElf = NPCDatabase.Get("elf_ranger_3");
    if (baseElf == null)
    {
        Debug.LogError("[LycanthropeFactory] Cannot find elf_ranger_3 base creature.");
        return null;
    }

    NPCDefinition wolfBase = NPCDatabase.Get("wolf");
    if (wolfBase == null)
    {
        Debug.LogError("[LycanthropeFactory] Cannot find wolf base creature.");
        return null;
    }

    // Step 2: Clone to avoid modifying originals
    NPCDefinition clonedElf = baseElf.Clone();
    NPCDefinition clonedWolf = wolfBase.Clone();

    // Step 3: Apply lycanthrope template
    NPCDefinition lycanthrope = LycanthropeTemplate.Apply(
        baseHumanoid: clonedElf,
        animalBase: clonedWolf,
        lycanthropeId: "elf_ranger_werewolf",
        lycanthropeName: "Elf Ranger Werewolf",
        isAfflicted: false,
        challengeRating: "5"
    );

    // Step 4: Custom adjustments
    lycanthrope.Description = "Natural lycanthrope. Elf ranger merged with wolf, retaining archery skills and wilderness cunning.";
    lycanthrope.SpecialAbilities.Add("Favored Enemy (humanoid): +2 bonus vs one humanoid type");
    lycanthrope.SpecialAbilities.Add("Track: +2 bonus on Survival checks to track");

    Debug.Log("[LycanthropeFactory] Created Elf Ranger Werewolf (CR 5)");

    return lycanthrope;
}
```

**Register in NPCDatabase_Lycanthropes.cs**:
```csharp
private static void RegisterCreatures_Lycanthropes()
{
    // ... existing lycanthropes ...
    Register(LycanthropeFactory.ElfRangerWerewolf());
    
    Debug.Log("[NPCDatabase] Registered 9 lycanthrope template variants.");
}
```

---

### Example 3: Hydra Multi-Head Variant

**File**: `HydraFactory.cs` (see Phase 3.2 for full implementation)

**Usage in NPCDatabase_H.cs**:
```csharp
private static void RegisterCreatures_H()
{
    // ... existing registrations ...
    
    // Hydras (parametric multi-head generation)
    Register(HydraFactory.Hydra5Head());
    Register(HydraFactory.Hydra6Head());
    Register(HydraFactory.Hydra7Head());
    Register(HydraFactory.Hydra8Head());
    Register(HydraFactory.Pyrohydra7Head());
    
    Debug.Log("[NPCDatabase] Registered 5 hydra variants.");
}
```

**Dynamic Hydra Creation** (if needed for custom encounters):
```csharp
// In encounter setup code:
NPCDefinition customHydra = HydraFactory.CreateHydra(headCount: 10);
NPCDatabase.Register(customHydra); // Temporary registration
```

---

### Example 4: Size Scaling for Vermin (Swarm)

**File**: `NPCDatabase_L.cs`

```csharp
/// <summary>
/// Locust Swarm (CR 3) — Diminutive vermin swarm.
/// MM 3.5e p.237. Dense cloud of biting insects.
/// </summary>
private static void RegisterLocustSwarm()
{
    Register(new NPCDefinition
    {
        Id = "locust_swarm",
        Name = "Locust Swarm",
        ChallengeRating = "3",
        Level = 6,
        CharacterClass = "Warrior",
        CreatureType = "Vermin",
        HitDice = 6,
        SizeCategory = SizeCategory.Diminutive, // Swarm occupies 10 ft square
        IsTallCreature = false,
        STR = 1, DEX = 17, CON = 10, WIS = 10, INT = 0, CHA = 2,
        BAB = 4,
        NaturalArmorBonus = 0,
        NaturalAttacks = new List<NaturalAttackDefinition>
        {
            new NaturalAttackDefinition
            {
                Name = "Swarm", DamageDice = 6, DamageCount = 2, Count = 1,
                BonusDamageSource = DamageBonusSource.None,
                Range = 0, IsPrimary = true,
                IsSwarmAttack = true // Flag for swarm damage to all in space
            }
        },
        BaseSpeed = 4,
        BaseHitDieHP = 27,
        CreatureTags = new List<string> { "Vermin", "Swarm", "MM35" },
        Feats = new List<string>(),
        HasDarkvision = true,
        SpecialAbilities = new List<string>
        {
            "Swarm Traits: Half damage from slashing/piercing, immune to weapon damage, grappling, tripping. Takes 1.5× damage from area effects.",
            "Distraction (Ex): Living creature in swarm must make Fort DC 12 or nauseated for 1 round",
            "Swarm Attack: 2d6 damage to all creatures in swarm's 10 ft space",
            "Immune to mind effects (mindless)",
            "Darkvision 60 ft."
        },
        EquipmentIds = new List<EquipmentSlotPair>(),
        BackpackItemIds = new List<string>(),
        AIBehavior = NPCAIBehavior.AggressiveMelee,
        AIProfileArchetype = NPCAIProfileArchetype.Mindless,
        SpriteColor = new Color(0.65f, 0.55f, 0.3f, 0.8f),
        PanelColor = new Color(0.25f, 0.2f, 0.1f, 0.75f),
        NameColor = new Color(0.9f, 0.8f, 0.5f),
        Description = "Monster Manual locust swarm. Cloud of biting insects that engulfs and nauseates victims."
    });
}
```

**Swarm Comparison** (existing Bat Swarm):
```csharp
// Reference existing swarm for consistency
private static void RegisterBatSwarm()
{
    Register(new NPCDefinition
    {
        Id = "bat_swarm",
        Name = "Bat Swarm",
        ChallengeRating = "2",
        // ... similar swarm structure ...
        NaturalAttacks = new List<NaturalAttackDefinition>
        {
            new NaturalAttackDefinition
            {
                Name = "Swarm", DamageDice = 6, DamageCount = 1, Count = 1,
                BonusDamageSource = DamageBonusSource.None,
                Range = 0, IsPrimary = true,
                IsSwarmAttack = true
            }
        },
        // ... rest of definition ...
    });
}
```

---

## Testing Strategy

### Phase 1 Testing (Quick Wins)
**Manual Spawn Tests**:
1. Spawn 5 random Phase 1 creatures via debug console
2. Verify stats match MM 3.5e values
3. Check equipment loads correctly
4. Confirm AI behavior (melee vs ranged)

**Automated Checks**:
```csharp
[Test]
public void Phase1_AllCreaturesRegistered()
{
    NPCDatabase.Init();
    
    string[] phase1Ids = {
        "hyena", "kobold_warrior", "grimlock", "skum", "derro",
        "human_warrior_1", "human_warrior_2", "human_warrior_3",
        // ... all Phase 1 creature IDs
    };
    
    foreach (string id in phase1Ids)
    {
        Assert.IsNotNull(NPCDatabase.Get(id), $"Creature {id} not registered");
    }
}
```

### Phase 2 Testing (Core Creatures)
**Combat Ability Tests**:
1. **Ghoul Paralysis**: Spawn ghoul, attack player, verify paralysis triggers
2. **Ghast Stench**: Verify 10 ft aura causes sickness
3. **Basilisk Gaze**: Check 30 ft gaze triggers Fort save
4. **Gauth Eye Rays**: Verify 1 ray fires per round, 6 different effects
5. **Darkmantle Darkness**: Confirm darkness spell-like ability works

**Spawn Integration Tests**:
```csharp
[Test]
public void Phase2_EncounterSpawnerIntegration()
{
    var encounter = new EncounterDefinition("Aberration Test")
        .AddCreature("darkmantle", count: 2)
        .AddCreature("gauth", count: 1);
    
    var result = DungeonEncounterSpawner.PrepareEncounter(encounter);
    
    Assert.IsTrue(result.IsValid, "Encounter preparation failed");
    Assert.AreEqual(3, result.Count, "Wrong creature count");
}
```

### Phase 3 Testing (Templates)
**Template Application Tests**:
1. **Hydra Multi-Head**: Verify 5-head hydra has 5 bite attacks
2. **Lycanthrope DR**: Check DR 10/silver applies to custom lycanthropes
3. **Ghost Incorporeal**: Verify 50% miss chance, touch attacks work
4. **Fast Healing**: Confirm hydra regains HP each round

**Template Composition Test**:
```csharp
[Test]
public void Phase3_GhostTemplateApplication()
{
    NPCDefinition baseFighter = NPCDatabase.Get("human_fighter_3").Clone();
    baseFighter.AppliedTemplateIds = new List<string> { "ghost" };
    
    NPCDefinition ghost = CreatureTemplateRegistry.ApplyTemplatesClone(baseFighter);
    
    Assert.AreEqual("Undead", ghost.CreatureType);
    Assert.IsTrue(ghost.IsIncorporeal);
    Assert.AreEqual(0, ghost.NaturalArmorBonus);
    Assert.IsTrue(ghost.SpecialAbilities.Any(s => s.Contains("Horrific Appearance")));
}
```

### Phase 4 Testing (Advanced Features)
**Size Scaling Tests**:
1. Spawn swarms, verify damage applies to all creatures in space
2. Check swarm traits (half damage from slashing/piercing)

**Class-Leveled NPC Tests**:
1. **Drow SR**: Cast spell at drow, verify SR check
2. **Hill Giant Rock Throw**: Verify 100 ft ranged attack
3. **Spell-Like Abilities**: Drow casts darkness

**Coverage Test**:
```csharp
[Test]
public void Phase4_FullCoverage()
{
    NPCDatabase.Init();
    
    // All 74 missing creatures from audit
    string[] allMissingIds = {
        // Phase 1 (30)
        "hyena", "kobold_warrior", /* ... */,
        // Phase 2 (25)
        "darkmantle", "gauth", /* ... */,
        // Phase 3 (13)
        "hydra_5head", "ghost_fighter_3", /* ... */,
        // Phase 4 (6)
        "locust_swarm", "hill_giant", /* ... */
    };
    
    int foundCount = 0;
    foreach (string id in allMissingIds)
    {
        if (NPCDatabase.Get(id) != null)
            foundCount++;
    }
    
    Assert.AreEqual(74, foundCount, "Not all missing creatures implemented");
}
```

---

## Success Metrics

### Phase-by-Phase Coverage Goals

| Phase | Creatures Added | Cumulative Total | Coverage % | Milestone |
|-------|----------------|------------------|------------|-----------|
| **Start** | 0 | 124 | 62.6% | Baseline |
| **Phase 1** | 30 | 154 | 77.8% | Quick wins complete |
| **Phase 2** | 25 | 179 | 90.4% | Core creatures complete |
| **Phase 3** | 13 | 192 | 96.9% | Templates complete |
| **Phase 4** | 6 | 198 | **100%** | **Full coverage achieved** |

### Quality Metrics
- [ ] All 74 creatures compile without errors
- [ ] 100% of encounter table IDs resolve to valid NPCDefinitions
- [ ] Zero regression in existing 124 creatures
- [ ] DungeonEncounterSpawner integrates all new creatures
- [ ] All special abilities documented in SpecialAbilities list
- [ ] AI behavior appropriate for creature type

### Performance Metrics
- [ ] NPCDatabase.Init() time < 500ms (with all 198 creatures)
- [ ] Creature lookup (NPCDatabase.Get) < 1ms
- [ ] Template application < 50ms per creature
- [ ] Encounter preparation (20 creatures) < 200ms

### Documentation Metrics
- [ ] Each creature has XML doc comment with MM page reference
- [ ] All special abilities listed in SpecialAbilities
- [ ] CreatureTags include "MM35" for all Monster Manual creatures
- [ ] Description field summarizes creature's tactical role

---

## Appendix: Complexity Breakdown

### By Implementation Difficulty

| Complexity | Count | Examples | Avg Time per Creature |
|-----------|-------|----------|----------------------|
| **Easy** | 25 | Hyena, Kobold, Grimlock, Owlbear, basic animals | 15-30 min |
| **Medium** | 35 | Darkmantle, Ghoul, Otyugh, Basilisk, Hill Giant | 30-60 min |
| **Hard** | 14 | Gauth, Mind Flayer, Hydras, Ghost, Lycanthropes | 1-3 hours |
| **TOTAL** | **74** | — | **~14-20 days** |

### By Creature Type

| Type | Count | Examples |
|------|-------|----------|
| Aberrations | 10 | Darkmantle, Choker, Grick, Gauth, Mind Flayer |
| Animals | 5 | Hyena, Monitor Lizard, Dire Boar, Giant Constrictor |
| Giants | 3 | Hill Giant, Stone Giant, Ettin |
| Humanoids | 14 | Kobold, Grimlock, Drow, Duergar, racial warriors |
| Magical Beasts | 11 | Owlbear, Ankheg, Basilisk, Hydras (5) |
| Oozes | 2 | Grey Ooze, Black Pudding |
| Outsiders | 0 | (Future phases: Devils, Demons, Celestials) |
| Plants | 2 | Violet Fungus, Phantom Fungus |
| Undead | 9 | Ghoul, Ghast, Wight, Vampire Spawn, Ghost (4) |
| Vermin (Swarms) | 3 | Locust Swarm, Centipede Swarm, Hellwasp Swarm |
| Templates | 15 | Lycanthropes (3 custom), Hydras (5), Ghosts (4) |

### By Special Ability Type

| Ability | Count | Implementation Notes |
|---------|-------|---------------------|
| Paralysis | 5 | HasParalysisOnHit flag |
| Poison | 3 | HasPoisonOnHit, PoisonType |
| Disease | 2 | HasDiseaseOnHit, DiseaseType |
| Energy Drain | 3 | HasEnergyDrain, DrainLevels |
| Gaze Attacks | 2 | New GazeAttackDefinition |
| Spell-Like Abilities | 6 | SpellLikeAbilityDefinition |
| Spell Resistance | 3 | SpellResistance property |
| Incorporeal | 4 | IsIncorporeal flag |
| Fast Healing | 5 | HasFastHealing, FastHealingRate |
| Swarm | 3 | IsSwarmAttack flag |
| Rock Throwing | 2 | RangedAttackDefinition |
| Improved Grab | 8 | Existing NaturalAttackDefinition |
| Trip | 2 | TripOnHit flag |

---

## Implementation Roadmap Timeline

### Week 1: Phases 1-2 (Foundation)
- **Days 1-2**: Phase 1 (30 quick wins) — 77.8% coverage
- **Days 3-6**: Phase 2 (25 core creatures) — 90.4% coverage
- **Day 7**: Testing, bug fixes, integration validation

### Week 2: Phases 3-4 (Advanced)
- **Days 1-4**: Phase 3 (template systems, 13 variants) — 96.9% coverage
- **Days 5-6**: Phase 4 (final 6 creatures) — 100% coverage
- **Day 7**: Final testing, documentation, polish

### Total Project Duration: **14-16 days** (120-140 hours)

---

## Conclusion

This implementation plan provides a clear, phased approach to adding 74 missing creatures to the D&D 3.5e NPCDatabase. By organizing work into 4 progressive phases — Quick Wins, Core Creatures, Templates, and Advanced Features — the project maintains momentum while building increasingly complex systems.

**Key Success Factors**:
1. Reuse existing frameworks (templates, spawner, class engine)
2. Progressive complexity (easy → hard)
3. Clear testing criteria at each phase
4. Consistent coding style and documentation
5. Minimal new systems (9 total, many optional)

**Final Coverage**: 198/198 creatures (100%)  
**Expected Effort**: 14-20 days  
**Risk Level**: Low (incremental, testable, reversible)

The phased approach allows for early validation and course correction, ensuring high-quality implementations that integrate seamlessly with the existing combat and encounter systems.

---

**Document Version**: 1.0  
**Last Updated**: May 26, 2026  
**Author**: Abacus AI Agent  
**Status**: Ready for Implementation
