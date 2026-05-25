# D&D 3.5e Player's Handbook — Missing Classes Implementation Plan

> **Document Version:** 1.0
> **Date:** 2026-05-25
> **Project:** `/home/ubuntu/dnd35prototype`
> **Scope:** All 11 PHB core classes — audit + implementation plan for missing/incomplete classes

---

## 1. EXECUTIVE SUMMARY

### Class Implementation Status

| # | Class | Status | Completeness | Notes |
|---|-------|--------|-------------|-------|
| 1 | **Barbarian** | 🟡 Partial | ~65% | Class file exists; Rage, Uncanny Dodge, Trap Sense, Fast Movement coded in CharacterStats. Missing: multi-rage scaling, Improved Uncanny Dodge, Damage Reduction, Greater Rage, Indomitable Will, Tireless Rage, Mighty Rage |
| 2 | **Bard** | ❌ Missing | 0% | No class file. 52 spells tagged "Bard" in SpellDatabase. No bardic music, no spontaneous casting infrastructure |
| 3 | **Cleric** | ✅ Complete | ~90% | Full class file, domain system, Turn Undead UI + AI, prepared divine casting, spontaneous Cure/Inflict conversion. Minor gaps: domain granted powers not all implemented |
| 4 | **Druid** | ❌ Missing | ~10% | No class file. SpellcastingComponent has Druid slot code path. 30 spells tagged "Druid". No Wild Shape, no Animal Companion, no nature abilities |
| 5 | **Fighter** | 🟡 Partial | ~55% | Class file exists with starting equipment. Bonus feat system NOT implemented (no feat selection every 2 levels). Weapon Specialization exists as a feat. Missing: systematic bonus feat grants |
| 6 | **Monk** | ✅ Complete | ~85% | Class file, Flurry of Blows, Unarmed Strike scaling, AC Bonus (Wis), Fast Movement, Evasion, Still Mind all in CharacterStats. Has CombatUI button + CombatFlowService integration. Missing: Ki Strike, Abundant Step, Quivering Palm, Perfect Self, Wholeness of Body |
| 7 | **Paladin** | ❌ Missing | ~5% | No class file. `IsPaladin` property exists in CharacterStats. Turn Undead UI checks `IsPaladin && Level >= 4`. No Smite Evil, Lay on Hands, Divine Grace, Special Mount |
| 8 | **Ranger** | ❌ Missing | 0% | No class file. 9 spells tagged "Ranger" in SpellDatabase. No Favored Enemy, Combat Style, Animal Companion, or Ranger spellcasting |
| 9 | **Rogue** | ✅ Complete | ~80% | Class file exists. Sneak Attack fully implemented (CombatUtils + CombatResult + flanking). Evasion works. Trapfinding referenced. Missing: Improved Uncanny Dodge, Improved Evasion, Special Abilities at 10/13/16/19 |
| 10 | **Sorcerer** | ❌ Missing | ~5% | No class file. 131 spells tagged "Sorcerer" in SpellDatabase. `GetPrimaryCastingModifier()` returns CHA for Sorcerer. No spontaneous arcane casting infrastructure |
| 11 | **Wizard** | ✅ Complete | ~90% | Full class file, prepared arcane casting, Scribe Scroll feat, Specialization schools, Familiar system (WizardFamiliar + FamiliarSelectionUI), spellbook. Minor gaps: some bonus feat selection automation |

### Summary Counts
- **✅ Fully Implemented (80%+):** 4 classes (Cleric, Monk, Rogue, Wizard)
- **🟡 Partially Implemented:** 2 classes (Barbarian, Fighter)
- **❌ Not Implemented:** 5 classes (Bard, Druid, Paladin, Ranger, Sorcerer)

### Recommended Implementation Priority
1. **Sorcerer** — Simplest missing class; reuses Wizard spell list; needs only spontaneous casting
2. **Fighter (completion)** — Just needs bonus feat grant system
3. **Barbarian (completion)** — Needs scaling rage + DR
4. **Paladin** — Medium complexity; Turn Undead infrastructure exists
5. **Ranger** — Medium complexity; reuses some Druid infrastructure
6. **Druid** — High complexity; Wild Shape + Animal Companion
7. **Bard** — High complexity; Bardic Music + spontaneous casting

### Overall Timeline Estimate
- **Phase 1 (Simple):** ~2-3 weeks — Sorcerer + Fighter/Barbarian completion
- **Phase 2 (Medium):** ~4-5 weeks — Paladin + Ranger
- **Phase 3 (Complex):** ~6-8 weeks — Druid + Bard

---

## 2. CURRENT STATE ANALYSIS

### 2.1 Architecture Overview

The class system is built on:

1. **`ICharacterClass` interface** — Contract for all classes defining:
   - `ClassName`, `Description`, `HitDie`, `BABAtLevel3`, `SkillPointsPerLevel`
   - Save progressions (`GoodFortitude`, `GoodReflex`, `GoodWill`)
   - `IsSpellcaster`, `ClassSkills` HashSet
   - `SetupStartingEquipment(InventoryComponent inv)`
   - `InitFeats(CharacterStats stats)` for automatic feats
   - `GetQuickStartCharacter()` static factory for character creation

2. **`ClassRegistry`** — Central static registry. Currently registers 6 classes via `Init()`.

3. **`CharacterStats`** — Houses all class feature properties/calculations:
   - `ClassLevels` (List<ClassLevelEntry>) — supports multiclassing
   - `HasClass(string)`, `GetClassLevel(string)` — class queries
   - Class-specific properties: `IsMonk`, `IsRogue`, `IsBarbarian`, `IsWizard`, `IsCleric`, `IsPaladin`
   - Feature implementations: `HasEvasion`, `MonkACBonus`, `MaxRagesPerDay`, `TrapSenseBonus`, etc.

4. **`ProgressionCalculator`** — Shared BAB/save math using `BABProgression` enum (Good/Medium/Poor)

5. **`SpellcastingComponent`** — Manages spell slots, preparation, and casting:
   - Prepared slot system for Wizard/Cleric/Druid
   - No spontaneous casting system (Sorcerer/Bard) yet
   - Spell slot tables hardcoded per class
   - Domain slot support for Cleric
   - Specialist slot support for Wizard

### 2.2 Per-Class Detailed Assessment

#### ✅ Cleric — ~90% Complete
**What's Done:**
- `ClericClass.cs` with full ICharacterClass implementation
- d8 HD, 3/4 BAB, good Fort/Will saves, 2 skill points
- Prepared divine casting with domain spell slots
- Domain system: `DomainData`, `DomainDatabase`, domain spell lists
- Turn Undead: `MaxTurnUndeadAttemptsPerDay` (3 + CHA mod), full UI panel (`TurnUndeadTargetSelectionPanel`), AI integration
- Spontaneous Cure/Inflict conversion: `SpontaneousCastingType` enum exists
- Quick start character with pre-configured spells

**What's Missing:**
- Some domain granted powers not mechanically implemented (just described)
- Spontaneous conversion not wired into SpellcastingComponent (UI exists, backend partially missing)

#### ✅ Wizard — ~90% Complete
**What's Done:**
- `WizardClass.cs` with full ICharacterClass implementation
- d4 HD, 1/2 BAB, good Will save, 2 skill points
- Prepared arcane casting with spellbook system
- Specialization schools with prohibited schools and specialist slots
- Familiar system: `WizardFamiliar` data class, `FamiliarSelectionUI`, bonus table
- Scribe Scroll automatic feat, Ring of Wizardry support
- ~140+ spells available on Wizard list
- Quick start character with pre-configured spellbook

**What's Missing:**
- Bonus feat selection at 5/10/15/20 (metamagic or item creation feats) — partially manual
- Some familiar abilities beyond stat bonuses (Alertness, Share Spells, etc.)

#### ✅ Monk — ~85% Complete
**What's Done:**
- `MonkClass.cs` with full ICharacterClass implementation
- d8 HD, 3/4 BAB, all good saves, 4 skill points
- Flurry of Blows: `GetFlurryOfBlowsBonuses()` in CharacterStats, `PerformFlurryOfBlows()` in CombatFlowService, CombatUI button
- Unarmed Strike: `MonkUnarmedDamageDie`, scaling damage, `GetUnarmedDamage()`, Improved Unarmed Strike automatic feat
- AC Bonus: `MonkACBonus` (Wis mod when unarmored)
- Fast Movement: `MonkFastMovementBonus` (10 ft at level 1+)
- Evasion: `HasEvasion` (level 2+), integrated into spell damage resolution
- Still Mind: `StillMindBonus` (+2 vs enchantments at level 3+)

**What's Missing:**
- Ki Strike (magic/lawful/adamantine at 4/10/16)
- Slow Fall scaling
- Purity of Body (immune to disease at level 5)
- Wholeness of Body (heal self at level 7)
- Improved Evasion (level 9)
- Diamond Body (immune to poison at level 11)
- Abundant Step (dimension door at level 12)
- Diamond Soul (SR at level 13)
- Quivering Palm (level 15)
- Timeless Body (level 17)
- Tongue of the Sun and Moon (level 17)
- Empty Body (etherealness at level 19)
- Perfect Self (DR 10/magic at level 20)

#### ✅ Rogue — ~80% Complete
**What's Done:**
- `RogueClass.cs` with full ICharacterClass implementation
- d6 HD, 3/4 BAB, good Reflex save, 8 skill points
- Sneak Attack: `GetSneakAttackDice()`, `RollSneakAttackDamage()` in CombatUtils, fully integrated into CombatResult with flanking/flat-footed detection
- Evasion: `HasEvasion` (level 2+) in CharacterStats
- Trapfinding: referenced in class description

**What's Missing:**
- Trap Sense bonus (+1 per 3 levels vs traps)
- Uncanny Dodge (level 4: can't be caught flat-footed)
- Improved Uncanny Dodge (level 8: can't be flanked)
- Improved Evasion (level 10 special ability option, if selected)
- Special Abilities at levels 10, 13, 16, 19 (Crippling Strike, Defensive Roll, Feat, Improved Evasion, Opportunist, Skill Mastery, Slippery Mind)

#### 🟡 Barbarian — ~65% Complete
**What's Done:**
- `BarbarianClass.cs` with full ICharacterClass implementation
- d12 HD, full BAB, good Fort save, 4 skill points
- Rage: `IsRaging`, `RagesUsedToday`, `MaxRagesPerDay` (hardcoded to 1), `StartRage()`/`EndRage()` in CharacterStats
- Rage effects: STR/CON +4, Will +2, AC -2, HP gain, fatigue on end
- Fast Movement: `BarbarianFastMovementBonus` (+10 ft in medium or lighter armor)
- Uncanny Dodge: `HasUncannyDodge` (level 2+)
- Trap Sense: `TrapSenseBonus` (scaling +1 per 3 levels from level 3)
- CombatUI: Rage button exists
- Armor of Rage item behavior exists (detects `IsRaging`)

**What's Missing:**
- Rage scaling: should be 1/day at level 1, +1/day at 4/8/12/16/20 (currently hardcoded to 1)
- Greater Rage (level 11): STR/CON +6, Will +3
- Indomitable Will (level 14): +4 vs enchantments while raging
- Tireless Rage (level 17): no fatigue after rage
- Mighty Rage (level 20): STR/CON +8, Will +4
- Improved Uncanny Dodge (level 5): can't be flanked
- Damage Reduction: 1/— at level 7, scaling +1 per 3 levels (2/— at 10, 3/— at 13, 4/— at 16, 5/— at 19)
- Illiteracy class feature (flavor — Barbarians can't read by default)

#### 🟡 Fighter — ~55% Complete
**What's Done:**
- `FighterClass.cs` with full ICharacterClass implementation
- d10 HD, full BAB, good Fort save, 2 skill points
- Starting equipment configured
- Weapon Focus, Weapon Specialization, Greater Weapon Focus feats exist in FeatDefinitions
- Combat feats (Power Attack, Cleave, Great Cleave, etc.) all implemented

**What's Missing:**
- **Bonus Feat System**: Fighter should select a bonus combat feat at level 1 and every even level (2, 4, 6, 8, 10, 12, 14, 16, 18, 20). This is the Fighter's signature mechanic and is NOT automated.
- Bonus feat selection UI integrated with level-up
- Greater Weapon Specialization (level 12+ prerequisite)
- Weapon Mastery (if using optional rules)

#### ❌ Sorcerer — ~5% Implemented
**Existing Infrastructure:**
- 131 spells tagged "Sorcerer" in SpellDatabase
- `GetPrimaryCastingModifier()` returns CHA for "Sorcerer"
- `SpellcastingKind` returns Arcane for "Sorcerer"

**Not Implemented:**
- No `SorcererClass.cs` file
- No spontaneous arcane casting system
- No spells known table
- No class features (familiar, bonus feats at 5/10/15/20)

#### ❌ Paladin — ~5% Implemented
**Existing Infrastructure:**
- `IsPaladin` property in CharacterStats
- Turn Undead UI checks `IsPaladin && Level >= 4`
- 16 spells tagged "Paladin" in SpellDatabase
- `GetPrimaryCastingModifier()` returns WIS for "Paladin"

**Not Implemented:**
- No `PaladinClass.cs` file
- No Smite Evil, Lay on Hands, Divine Grace, Aura of Courage, Remove Disease
- No Paladin spellcasting (prepared divine, starts at level 4)
- No Special Mount
- No Code of Conduct / ex-Paladin mechanics

#### ❌ Ranger — 0% Implemented
**Existing Infrastructure:**
- 9 spells tagged "Ranger" in SpellDatabase
- `GetPrimaryCastingModifier()` returns WIS for "Ranger"

**Not Implemented:**
- No `RangerClass.cs` file
- No Favored Enemy system
- No Combat Style (Archery vs Two-Weapon Fighting)
- No Ranger spellcasting (prepared divine, starts at level 4)
- No Animal Companion (delayed progression)
- No Track, Wild Empathy, Endurance, Woodland Stride, Swift Tracker, Camouflage, Hide in Plain Sight

#### ❌ Druid — ~10% Implemented
**Existing Infrastructure:**
- SpellcastingComponent has Druid slot code path (`InitDruidSpellSlots`)
- 30 spells tagged "Druid" in SpellDatabase
- `GetPrimaryCastingModifier()` returns WIS for "Druid"

**Not Implemented:**
- No `DruidClass.cs` file
- No Wild Shape system
- No Animal Companion system
- No Nature Sense, Wild Empathy, Woodland Stride, Trackless Step, Resist Nature's Lure, Venom Immunity, A Thousand Faces
- No spontaneous Summon Nature's Ally conversion

#### ❌ Bard — 0% Implemented
**Existing Infrastructure:**
- 52 spells tagged "Bard" in SpellDatabase
- `GetPrimaryCastingModifier()` returns CHA for "Bard"

**Not Implemented:**
- No `BardClass.cs` file
- No spontaneous arcane casting
- No Bardic Music system (Inspire Courage, Fascinate, Suggestion, etc.)
- No Bardic Knowledge
- No Countersong
- No class features

---

## 3. MISSING CLASSES — DETAILED PLANS

---

### 3.1 SORCERER

#### Class Overview
- **Role:** Primary arcane striker/controller — spontaneous casting with limited spells known but unlimited flexibility
- **Playstyle:** "Fewer tools, more uses" — knows fewer spells than a Wizard but casts any known spell without preparation
- **Iconic Abilities:** Spontaneous arcane casting, Familiar
- **Complexity Rating:** ⭐⭐ (2/5) — simplest missing class; same spell list as Wizard
- **Key Difference from Wizard:** No spellbook, no specialization, fewer spells known, more spells per day, CHA-based

#### Core Mechanics
| Stat | Value |
|------|-------|
| Hit Die | d4 |
| BAB | Poor (1/2 per level) |
| Good Saves | Will only |
| Skill Points | 2 + INT mod per level |
| Proficiencies | Simple weapons; no armor/shield |
| Casting Stat | Charisma |
| Casting Type | Spontaneous Arcane |

#### Class Skills
Bluff, Concentration, Craft, Knowledge (Arcana), Profession, Spellcraft

#### Level Progression (1–20)

| Level | BAB | Fort | Ref | Will | Special | Spells Known (0/1/2/3/4/5/6/7/8/9) | Spells Per Day (0/1/2/3/4/5/6/7/8/9) |
|-------|-----|------|-----|------|---------|--------------------------------------|----------------------------------------|
| 1 | +0 | +0 | +0 | +2 | Summon Familiar | 4/2/—/—/—/—/—/—/—/— | 5/3/—/—/—/—/—/—/—/— |
| 2 | +1 | +0 | +0 | +3 | — | 5/2/—/—/—/—/—/—/—/— | 6/4/—/—/—/—/—/—/—/— |
| 3 | +1 | +1 | +1 | +3 | — | 5/3/—/—/—/—/—/—/—/— | 6/5/—/—/—/—/—/—/—/— |
| 4 | +2 | +1 | +1 | +4 | — | 6/3/1/—/—/—/—/—/—/— | 6/6/3/—/—/—/—/—/—/— |
| 5 | +2 | +1 | +1 | +4 | — | 6/4/2/—/—/—/—/—/—/— | 6/6/4/—/—/—/—/—/—/— |
| 6 | +3 | +2 | +2 | +5 | — | 7/4/2/1/—/—/—/—/—/— | 6/6/5/3/—/—/—/—/—/— |
| 7 | +3 | +2 | +2 | +5 | — | 7/5/3/2/—/—/—/—/—/— | 6/6/6/4/—/—/—/—/—/— |
| 8 | +4 | +2 | +2 | +6 | — | 8/5/3/2/1/—/—/—/—/— | 6/6/6/5/3/—/—/—/—/— |
| 9 | +4 | +3 | +3 | +6 | — | 8/5/4/3/2/—/—/—/—/— | 6/6/6/6/4/—/—/—/—/— |
| 10 | +5 | +3 | +3 | +7 | — | 9/5/4/3/2/1/—/—/—/— | 6/6/6/6/5/3/—/—/—/— |
| 11 | +5 | +3 | +3 | +7 | — | 9/5/5/4/3/2/—/—/—/— | 6/6/6/6/6/4/—/—/—/— |
| 12 | +6/+1 | +4 | +4 | +8 | — | 9/5/5/4/3/2/1/—/—/— | 6/6/6/6/6/5/3/—/—/— |
| 13 | +6/+1 | +4 | +4 | +8 | — | 9/5/5/4/4/3/2/—/—/— | 6/6/6/6/6/6/4/—/—/— |
| 14 | +7/+2 | +4 | +4 | +9 | — | 9/5/5/4/4/3/2/1/—/— | 6/6/6/6/6/6/5/3/—/— |
| 15 | +7/+2 | +5 | +5 | +9 | — | 9/5/5/4/4/4/3/2/—/— | 6/6/6/6/6/6/6/4/—/— |
| 16 | +8/+3 | +5 | +5 | +10 | — | 9/5/5/4/4/4/3/2/1/— | 6/6/6/6/6/6/6/5/3/— |
| 17 | +8/+3 | +5 | +5 | +10 | — | 9/5/5/4/4/4/3/3/2/— | 6/6/6/6/6/6/6/6/4/— |
| 18 | +9/+4 | +6 | +6 | +11 | — | 9/5/5/4/4/4/3/3/2/1 | 6/6/6/6/6/6/6/6/5/3 |
| 19 | +9/+4 | +6 | +6 | +11 | — | 9/5/5/4/4/4/3/3/3/2 | 6/6/6/6/6/6/6/6/6/4 |
| 20 | +10/+5 | +6 | +6 | +12 | — | 9/5/5/4/4/4/3/3/3/3 | 6/6/6/6/6/6/6/6/6/6 |

#### Class Features

**Summon Familiar (Level 1)**
- Identical to Wizard familiar (reuse existing `WizardFamiliar` + `FamiliarSelectionUI`)
- Uses Sorcerer level instead of Wizard level for familiar abilities

**Spontaneous Arcane Casting**
- Knows a fixed number of spells per level (see Spells Known table)
- Can cast any known spell using an available spell slot of that level
- No spellbook, no preparation — just pick a known spell and spend a slot
- Bonus spells per day from high CHA (same formula as bonus prepared slots)

#### Unique Systems Required

##### SpontaneousCastingSystem
This is the **single most important new system** for Sorcerer (and later Bard). It replaces the prepared slot model.

```csharp
/// <summary>
/// Manages spontaneous casting for Sorcerer and Bard.
/// Instead of preparing specific spells into slots, the caster
/// knows a fixed set of spells and can cast any of them by
/// spending a slot of the appropriate level.
/// </summary>
public class SpontaneousCastingData
{
    /// <summary>Spells known by level. Index = spell level (0-9).</summary>
    public List<string>[] SpellsKnownByLevel = new List<string>[10];

    /// <summary>Max spells known per level (from class table).</summary>
    public int[] MaxSpellsKnownByLevel = new int[10];

    /// <summary>Spell slots remaining per level (0-9).</summary>
    public int[] SlotsRemaining = new int[10];

    /// <summary>Max spell slots per level (base + CHA bonus).</summary>
    public int[] SlotsMax = new int[10];

    /// <summary>Can this caster cast a specific spell right now?</summary>
    public bool CanCast(string spellId, int spellLevel)
    {
        if (spellLevel < 0 || spellLevel > 9) return false;
        if (SlotsRemaining[spellLevel] <= 0) return false;
        if (SpellsKnownByLevel[spellLevel] == null) return false;
        return SpellsKnownByLevel[spellLevel].Contains(spellId);
    }

    /// <summary>Spend a slot to cast a spell.</summary>
    public bool SpendSlot(int spellLevel)
    {
        if (spellLevel == 0) return true; // Cantrips unlimited (3.5e variant)
        if (SlotsRemaining[spellLevel] <= 0) return false;
        SlotsRemaining[spellLevel]--;
        return true;
    }
}
```

##### Sorcerer Spells Per Day Table (static data)
```csharp
// Indexed by [classLevel - 1][spellLevel]
private static readonly int[,] SorcererSlotsPerDay = new int[20, 10]
{
    // Lvl  0  1  2  3  4  5  6  7  8  9
    /*  1*/ {5, 3, 0, 0, 0, 0, 0, 0, 0, 0},
    /*  2*/ {6, 4, 0, 0, 0, 0, 0, 0, 0, 0},
    /*  3*/ {6, 5, 0, 0, 0, 0, 0, 0, 0, 0},
    /*  4*/ {6, 6, 3, 0, 0, 0, 0, 0, 0, 0},
    /*  5*/ {6, 6, 4, 0, 0, 0, 0, 0, 0, 0},
    /*  6*/ {6, 6, 5, 3, 0, 0, 0, 0, 0, 0},
    /*  7*/ {6, 6, 6, 4, 0, 0, 0, 0, 0, 0},
    /*  8*/ {6, 6, 6, 5, 3, 0, 0, 0, 0, 0},
    /*  9*/ {6, 6, 6, 6, 4, 0, 0, 0, 0, 0},
    /* 10*/ {6, 6, 6, 6, 5, 3, 0, 0, 0, 0},
    /* 11*/ {6, 6, 6, 6, 6, 4, 0, 0, 0, 0},
    /* 12*/ {6, 6, 6, 6, 6, 5, 3, 0, 0, 0},
    /* 13*/ {6, 6, 6, 6, 6, 6, 4, 0, 0, 0},
    /* 14*/ {6, 6, 6, 6, 6, 6, 5, 3, 0, 0},
    /* 15*/ {6, 6, 6, 6, 6, 6, 6, 4, 0, 0},
    /* 16*/ {6, 6, 6, 6, 6, 6, 6, 5, 3, 0},
    /* 17*/ {6, 6, 6, 6, 6, 6, 6, 6, 4, 0},
    /* 18*/ {6, 6, 6, 6, 6, 6, 6, 6, 5, 3},
    /* 19*/ {6, 6, 6, 6, 6, 6, 6, 6, 6, 4},
    /* 20*/ {6, 6, 6, 6, 6, 6, 6, 6, 6, 6}
};

// Indexed by [classLevel - 1][spellLevel]
private static readonly int[,] SorcererSpellsKnown = new int[20, 10]
{
    // Lvl  0  1  2  3  4  5  6  7  8  9
    /*  1*/ {4, 2, 0, 0, 0, 0, 0, 0, 0, 0},
    /*  2*/ {5, 2, 0, 0, 0, 0, 0, 0, 0, 0},
    /*  3*/ {5, 3, 0, 0, 0, 0, 0, 0, 0, 0},
    /*  4*/ {6, 3, 1, 0, 0, 0, 0, 0, 0, 0},
    /*  5*/ {6, 4, 2, 0, 0, 0, 0, 0, 0, 0},
    /*  6*/ {7, 4, 2, 1, 0, 0, 0, 0, 0, 0},
    /*  7*/ {7, 5, 3, 2, 0, 0, 0, 0, 0, 0},
    /*  8*/ {8, 5, 3, 2, 1, 0, 0, 0, 0, 0},
    /*  9*/ {8, 5, 4, 3, 2, 0, 0, 0, 0, 0},
    /* 10*/ {9, 5, 4, 3, 2, 1, 0, 0, 0, 0},
    /* 11*/ {9, 5, 5, 4, 3, 2, 0, 0, 0, 0},
    /* 12*/ {9, 5, 5, 4, 3, 2, 1, 0, 0, 0},
    /* 13*/ {9, 5, 5, 4, 4, 3, 2, 0, 0, 0},
    /* 14*/ {9, 5, 5, 4, 4, 3, 2, 1, 0, 0},
    /* 15*/ {9, 5, 5, 4, 4, 4, 3, 2, 0, 0},
    /* 16*/ {9, 5, 5, 4, 4, 4, 3, 2, 1, 0},
    /* 17*/ {9, 5, 5, 4, 4, 4, 3, 3, 2, 0},
    /* 18*/ {9, 5, 5, 4, 4, 4, 3, 3, 2, 1},
    /* 19*/ {9, 5, 5, 4, 4, 4, 3, 3, 3, 2},
    /* 20*/ {9, 5, 5, 4, 4, 4, 3, 3, 3, 3}
};
```

#### Data Structures
```csharp
public class SorcererClass : ICharacterClass
{
    public string ClassName => "Sorcerer";
    public string Description => "Sorcerers cast arcane spells through innate power rather than study. " +
        "They know fewer spells than wizards but can cast any known spell spontaneously.";
    public int HitDie => 4;
    public int BABAtLevel3 => 1; // Poor
    public int SkillPointsPerLevel => 2;
    public bool GoodFortitude => false;
    public bool GoodReflex => false;
    public bool GoodWill => true;
    public bool IsSpellcaster => true;
    // Reuse Wizard spell list (Sor/Wiz list)
    // Familiar reuses WizardFamiliar system
}
```

#### UI Components Needed
- **Spells Known selection UI** — at level-up when new spells are gained, player picks from Sorcerer/Wizard spell list
- **Spontaneous casting panel** — replaces prepared spell slots in Spell Preparation UI; shows known spells grouped by level with remaining slot count
- Familiar selection: reuse existing `FamiliarSelectionUI`

#### Integration Points
- `SpellcastingComponent` — add `IsSpontaneousCaster` flag and `SpontaneousCastingData` field
- `SpellPreparationUI` — branch on `IsSpontaneousCaster` to show "Spells Known" view vs "Spell Slots" view
- `SpellCaster` / `GameManager_Spells` — when casting, spend a slot (any of that level) instead of consuming a specific prepared slot
- `ClassRegistry` — register `SorcererClass`
- `CharacterCreationUI` — add Sorcerer option with spell selection

---

### 3.2 PALADIN

#### Class Overview
- **Role:** Front-line divine warrior — melee combatant with limited healing and anti-evil powers
- **Playstyle:** Defensive tank with smite burst damage and party auras
- **Iconic Abilities:** Smite Evil, Lay on Hands, Divine Grace, Special Mount
- **Complexity Rating:** ⭐⭐⭐ (3/5) — moderate; Turn Undead infrastructure exists

#### Core Mechanics
| Stat | Value |
|------|-------|
| Hit Die | d10 |
| BAB | Good (1 per level) |
| Good Saves | Fortitude, Will (note: Reflex is poor but gets CHA bonus from Divine Grace) |
| Skill Points | 2 + INT mod per level |
| Proficiencies | All simple and martial weapons, all armor, shields |
| Casting Stat | Wisdom (starts at level 4) |
| Casting Type | Prepared Divine (limited list, like Ranger) |

#### Class Skills
Concentration, Craft, Diplomacy, Handle Animal, Heal, Knowledge (Nobility and Royalty), Knowledge (Religion), Profession, Ride, Sense Motive

#### Level Progression (1–20)

| Level | BAB | Fort | Ref | Will | Special | Spells/Day (1/2/3/4) |
|-------|-----|------|-----|------|---------|----------------------|
| 1 | +1 | +2 | +0 | +0 | Aura of Good, Detect Evil, Smite Evil 1/day | —/—/—/— |
| 2 | +2 | +3 | +0 | +0 | Divine Grace, Lay on Hands | —/—/—/— |
| 3 | +3 | +3 | +1 | +1 | Aura of Courage, Divine Health | —/—/—/— |
| 4 | +4 | +4 | +1 | +1 | Turn Undead | 0/—/—/— |
| 5 | +5 | +4 | +1 | +1 | Smite Evil 2/day, Special Mount | 0/—/—/— |
| 6 | +6/+1 | +5 | +2 | +2 | Remove Disease 1/week | 1/—/—/— |
| 7 | +7/+2 | +5 | +2 | +2 | — | 1/—/—/— |
| 8 | +8/+3 | +6 | +2 | +2 | — | 1/0/—/— |
| 9 | +9/+4 | +6 | +3 | +3 | Remove Disease 2/week | 1/0/—/— |
| 10 | +10/+5 | +7 | +3 | +3 | Smite Evil 3/day | 1/1/—/— |
| 11 | +11/+6/+1 | +7 | +3 | +3 | — | 1/1/0/— |
| 12 | +12/+7/+2 | +8 | +4 | +4 | Remove Disease 3/week | 1/1/1/— |
| 13 | +13/+8/+3 | +8 | +4 | +4 | — | 1/1/1/— |
| 14 | +14/+9/+4 | +9 | +4 | +4 | — | 2/1/1/0 |
| 15 | +15/+10/+5 | +9 | +5 | +5 | Remove Disease 4/week, Smite Evil 4/day | 2/1/1/1 |
| 16 | +16/+11/+6/+1 | +10 | +5 | +5 | — | 2/2/1/1 |
| 17 | +17/+12/+7/+2 | +10 | +5 | +5 | — | 2/2/2/1 |
| 18 | +18/+13/+8/+3 | +11 | +6 | +6 | Remove Disease 5/week | 3/2/2/1 |
| 19 | +19/+14/+9/+4 | +11 | +6 | +6 | — | 3/3/3/2 |
| 20 | +20/+15/+10/+5 | +12 | +6 | +6 | Smite Evil 5/day | 3/3/3/3 |

#### Class Features

**Aura of Good (Level 1)**
- Detectable by Detect Good spell. Strength equals Paladin level.
- *Implementation:* Tag on character (`aura:good:strong`). Low priority — mostly flavor.

**Detect Evil (Level 1)**
- At will, as the spell. Standard action.
- *Implementation:* Add as a class ability button. Uses existing spell targeting framework to scan for evil creatures in a cone. Can highlight evil-aligned creatures in the UI.

**Smite Evil (Level 1)**
- Uses per day: 1 at level 1, +1 at 5/10/15/20
- On attack: add CHA bonus to attack roll, add Paladin level to damage
- Only works vs evil creatures; if target isn't evil, smite is wasted
- *Formula:* Attack bonus = CHA modifier; Damage bonus = Paladin level (max 20)

```csharp
public class SmiteEvilData
{
    public int UsesPerDay;    // 1 + (level - 1) / 5
    public int UsesRemaining;
    public int AttackBonus;   // CHA modifier
    public int DamageBonus;   // Paladin level

    public static int GetUsesPerDay(int paladinLevel)
    {
        if (paladinLevel < 1) return 0;
        return 1 + (paladinLevel - 1) / 5; // 1 at 1, 2 at 5, 3 at 10, 4 at 15, 5 at 20
    }
}
```

**Divine Grace (Level 2)**
- Add CHA modifier as bonus to all saving throws
- *Implementation:* In `CharacterStats` save calculation, if `IsPaladin && GetClassLevel("Paladin") >= 2`, add `CharismaModifier` to all saves.
- This is a very powerful feature — CHA 20 gives +5 to all saves.

**Lay on Hands (Level 2)**
- Heal pool: Paladin level × CHA modifier HP per day
- Can heal self or allies (touch range), or deal damage to undead
- *Formula:* `HealingPool = PaladinLevel * CharismaModifier` (minimum 0)

```csharp
public class LayOnHandsData
{
    public int MaxHealingPool;   // PaladinLevel * CHA mod
    public int HealingRemaining;

    public void Reset(int paladinLevel, int chaModifier)
    {
        MaxHealingPool = Mathf.Max(0, paladinLevel * chaModifier);
        HealingRemaining = MaxHealingPool;
    }
}
```

**Aura of Courage (Level 3)**
- Paladin is immune to fear
- All allies within 10 feet gain +4 morale bonus on saving throws against fear effects
- *Implementation:* `ImmuneToFear` flag on Paladin; aura effect on nearby allies during fear saves.

**Divine Health (Level 3)**
- Immune to all diseases (including magical like Mummy Rot)
- *Implementation:* `ImmuneToDiseases` flag in CharacterStats

**Turn Undead (Level 4)**
- As a Cleric of (Paladin level - 3)
- *Implementation:* Already exists — Turn Undead system is fully built with UI and AI. Just set effective turning level = `PaladinLevel - 3`.

**Spellcasting (Level 4)**
- Prepared divine casting, spell levels 1-4 only
- Caster level = Paladin level - 3 (minimum 1)
- Uses WIS for bonus spells and save DCs
- Spell list is small (~16 spells already tagged in SpellDatabase)

```csharp
// Paladin Spells Per Day [classLevel - 1][spellLevel (1-4)]
// Levels 1-3 have no spellcasting
private static readonly int[,] PaladinSlotsPerDay = new int[20, 4]
{
    // Lvl  1  2  3  4
    /*  1*/ {0, 0, 0, 0},
    /*  2*/ {0, 0, 0, 0},
    /*  3*/ {0, 0, 0, 0},
    /*  4*/ {0, 0, 0, 0},
    /*  5*/ {0, 0, 0, 0},
    /*  6*/ {1, 0, 0, 0},
    /*  7*/ {1, 0, 0, 0},
    /*  8*/ {1, 0, 0, 0},
    /*  9*/ {1, 0, 0, 0},
    /* 10*/ {1, 1, 0, 0},
    /* 11*/ {1, 1, 0, 0},
    /* 12*/ {1, 1, 1, 0},
    /* 13*/ {1, 1, 1, 0},
    /* 14*/ {2, 1, 1, 0},
    /* 15*/ {2, 1, 1, 1},
    /* 16*/ {2, 2, 1, 1},
    /* 17*/ {2, 2, 2, 1},
    /* 18*/ {3, 2, 2, 1},
    /* 19*/ {3, 3, 3, 2},
    /* 20*/ {3, 3, 3, 3}
};
```

**Special Mount (Level 5)**
- Summoned as a full-round action; lasts indefinitely
- Heavy warhorse with enhanced stats based on Paladin level
- *Implementation:* Create as a summoned creature using existing summoning framework. Stats scale with Paladin level.

**Remove Disease (Level 6)**
- Sp ability, uses per week: 1 at 6, +1 per 3 levels (2 at 9, 3 at 12, etc.)
- As the spell *remove disease*
- *Formula:* `UsesPerWeek = (PaladinLevel - 3) / 3` (minimum 1 at level 6)

**Code of Conduct**
- Must be Lawful Good; loses all class features if alignment changes
- *Implementation:* Alignment flag check. If violated, set `ExPaladin = true` and disable all Paladin abilities.
- Can be restored via *atonement* spell.

#### UI Components Needed
- Smite Evil button in combat action bar (like Rage button for Barbarian)
- Lay on Hands usage panel (target selection, amount to heal)
- Detect Evil toggle/button
- Turn Undead: reuse existing `TurnUndeadTargetSelectionPanel` with effective level = Paladin level - 3
- Special Mount summon button (pre-combat or in-combat)
- Spell preparation UI: reuse Cleric-style slot system with Paladin spell list

#### Integration Points
- `CharacterStats` — add `SmiteEvilUsesRemaining`, `LayOnHandsRemaining`, `DivineGraceSaveBonus`, `AuraOfCourageActive`
- Combat system — Smite Evil modifies attack/damage on evil targets
- Save calculation — Divine Grace adds CHA to all saves
- Turn Undead — use effective level `PaladinLevel - 3`
- Spell system — small prepared divine caster starting at level 4

---

### 3.3 RANGER

#### Class Overview
- **Role:** Versatile scout/striker — excels against specific enemies with dual-wield or archery combat style
- **Playstyle:** Adaptable combatant with tracking, wilderness survival, and limited divine casting
- **Iconic Abilities:** Favored Enemy, Combat Style, Animal Companion
- **Complexity Rating:** ⭐⭐⭐ (3/5) — Favored Enemy and Combat Style systems are moderate

#### Core Mechanics
| Stat | Value |
|------|-------|
| Hit Die | d8 |
| BAB | Good (1 per level) |
| Good Saves | Fortitude, Reflex |
| Skill Points | 6 + INT mod per level |
| Proficiencies | All simple and martial weapons, light armor, shields (except tower shield) |
| Casting Stat | Wisdom (starts at level 4) |
| Casting Type | Prepared Divine (limited list) |

#### Class Skills
Climb, Concentration, Craft, Handle Animal, Heal, Hide, Jump, Knowledge (Dungeoneering), Knowledge (Geography), Knowledge (Nature), Listen, Move Silently, Profession, Ride, Search, Spot, Survival, Swim, Use Rope

#### Level Progression (1–20)

| Level | BAB | Fort | Ref | Will | Special | Spells/Day (1/2/3/4) |
|-------|-----|------|-----|------|---------|----------------------|
| 1 | +1 | +2 | +2 | +0 | 1st Favored Enemy, Track, Wild Empathy | —/—/—/— |
| 2 | +2 | +3 | +3 | +0 | Combat Style | —/—/—/— |
| 3 | +3 | +3 | +3 | +1 | Endurance | —/—/—/— |
| 4 | +4 | +4 | +4 | +1 | Animal Companion | 0/—/—/— |
| 5 | +5 | +4 | +4 | +1 | 2nd Favored Enemy | 0/—/—/— |
| 6 | +6/+1 | +5 | +5 | +2 | Improved Combat Style | 1/—/—/— |
| 7 | +7/+2 | +5 | +5 | +2 | Woodland Stride | 1/—/—/— |
| 8 | +8/+3 | +6 | +6 | +2 | Swift Tracker | 1/0/—/— |
| 9 | +9/+4 | +6 | +6 | +3 | Evasion | 1/0/—/— |
| 10 | +10/+5 | +7 | +7 | +3 | 3rd Favored Enemy | 1/1/—/— |
| 11 | +11/+6/+1 | +7 | +7 | +3 | Combat Style Mastery | 1/1/0/— |
| 12 | +12/+7/+2 | +8 | +8 | +4 | — | 1/1/1/— |
| 13 | +13/+8/+3 | +8 | +8 | +4 | Camouflage | 1/1/1/— |
| 14 | +14/+9/+4 | +9 | +9 | +4 | — | 2/1/1/0 |
| 15 | +15/+10/+5 | +9 | +9 | +5 | 4th Favored Enemy | 2/1/1/1 |
| 16 | +16/+11/+6/+1 | +10 | +10 | +5 | — | 2/2/1/1 |
| 17 | +17/+12/+7/+2 | +10 | +10 | +5 | Hide in Plain Sight | 2/2/2/1 |
| 18 | +18/+13/+8/+3 | +11 | +11 | +6 | — | 3/2/2/1 |
| 19 | +19/+14/+9/+4 | +11 | +11 | +6 | — | 3/3/3/2 |
| 20 | +20/+15/+10/+5 | +12 | +12 | +6 | 5th Favored Enemy | 3/3/3/3 |

#### Class Features

**Favored Enemy (Level 1, 5, 10, 15, 20)**
- Choose a creature type from: Aberration, Animal, Construct, Dragon, Elemental, Fey, Giant, Humanoid (subtype), Magical Beast, Monstrous Humanoid, Ooze, Outsider (subtype), Plant, Undead, Vermin
- +2 bonus to Bluff, Listen, Sense Motive, Spot, Survival checks AND weapon damage rolls against that type
- At each new favored enemy selection, increase one existing favored enemy bonus by +2
- *Implementation:* Track list of `(CreatureType, bonus)` pairs. Check target's type during attack and skill rolls.

```csharp
public class FavoredEnemyData
{
    public List<FavoredEnemyEntry> Entries = new List<FavoredEnemyEntry>();

    public int GetBonusAgainst(CreatureTypeId type)
    {
        foreach (var entry in Entries)
            if (entry.CreatureType == type) return entry.Bonus;
        return 0;
    }
}

public class FavoredEnemyEntry
{
    public CreatureTypeId CreatureType;
    public string SubType;     // For Humanoid/Outsider subtypes
    public int Bonus;          // Starts at +2, increases by +2
}
```

**Track (Level 1)**
- Bonus feat: Track. Allows Survival checks to follow tracks.
- *Implementation:* Automatically grant Track feat via `InitFeats()`. Already exists in FeatDefinitions.

**Wild Empathy (Level 1)**
- Diplomacy check to influence animal attitude. Roll = d20 + Ranger level + CHA mod.
- *Implementation:* Flavor/utility — low priority for combat prototype.

**Combat Style (Level 2, 6, 11)**
- Choose Archery OR Two-Weapon Fighting at level 2.
- **Archery Path:**
  - Level 2: Rapid Shot (bonus feat, even without prerequisites)
  - Level 6: Manyshot
  - Level 11: Improved Precise Shot
- **Two-Weapon Fighting Path:**
  - Level 2: Two-Weapon Fighting (bonus feat, even without prerequisites)
  - Level 6: Improved Two-Weapon Fighting
  - Level 11: Greater Two-Weapon Fighting
- Only usable in light or no armor.

```csharp
public enum RangerCombatStyle
{
    Archery,
    TwoWeaponFighting
}

public class RangerCombatStyleData
{
    public RangerCombatStyle Style;
    public int EffectiveLevel;  // 2, 6, or 11+

    public List<string> GetGrantedFeats()
    {
        var feats = new List<string>();
        if (Style == RangerCombatStyle.Archery)
        {
            if (EffectiveLevel >= 2) feats.Add("Rapid Shot");
            if (EffectiveLevel >= 6) feats.Add("Manyshot");
            if (EffectiveLevel >= 11) feats.Add("Improved Precise Shot");
        }
        else
        {
            if (EffectiveLevel >= 2) feats.Add("Two-Weapon Fighting");
            if (EffectiveLevel >= 6) feats.Add("Improved Two-Weapon Fighting");
            if (EffectiveLevel >= 11) feats.Add("Greater Two-Weapon Fighting");
        }
        return feats;
    }
}
```

**Endurance (Level 3)** — Bonus feat. Grants +4 to various endurance checks. Already in FeatDefinitions.

**Animal Companion (Level 4)**
- As Druid, but effective Druid level = Ranger level - 3
- Uses shared Animal Companion system (see §4.1)

**Woodland Stride (Level 7)** — Move through undergrowth without penalty. Mostly flavor for grid combat.

**Swift Tracker (Level 8)** — Move at normal speed while tracking. Utility/exploration feature.

**Evasion (Level 9)** — Same as Monk/Rogue. Already implemented in `CharacterStats.HasEvasion` — just add Ranger check.

**Camouflage (Level 13)** — Use Hide skill in any natural terrain without cover.

**Hide in Plain Sight (Level 17)** — Can use Hide even while being observed, in any natural terrain.

**Ranger Spellcasting (Level 4)**
- Prepared divine casting, spell levels 1-4 only
- Same slot table as Paladin
- Caster level = Ranger level / 2 (minimum 1)
- Uses WIS for bonus spells

#### UI Components Needed
- Favored Enemy selection UI at character creation and level-up
- Combat Style selection at level 2
- Favored Enemy bonus display in combat tooltips
- Animal Companion management (shared with Druid — see §4.1)

#### Integration Points
- `CharacterStats` — `FavoredEnemies` list, `RangerCombatStyle`, `HasEvasion` (add Ranger level 9+ check)
- Combat system — Apply favored enemy bonuses to attack/damage when target creature type matches
- `CreatureTypeId` enum already exists with all D&D creature types
- Spell system — Paladin-style prepared casting with Ranger spell list

---

### 3.4 BARBARIAN (Completion)

#### What's Already Done
- Class file, starting equipment, quickstart character
- Rage (start/end, STR/CON +4, Will +2, AC -2, HP, fatigue)
- Fast Movement (+10 ft), Uncanny Dodge (level 2+), Trap Sense (scaling)
- CombatUI Rage button, Armor of Rage item interaction

#### What Needs to Be Added

**Rage Scaling (currently hardcoded to 1/day)**
```csharp
// CURRENT (CharacterStats.cs line ~661):
public int MaxRagesPerDay => IsBarbarian ? 1 : 0;

// CORRECT:
public int MaxRagesPerDay
{
    get
    {
        if (!IsBarbarian) return 0;
        int lvl = GetClassLevel("Barbarian");
        if (lvl < 1) return 0;
        return 1 + (lvl - 1) / 4; // 1 at 1, 2 at 4, 3 at 8, 4 at 12, 5 at 16, 6 at 20
    }
}
```

**Improved Uncanny Dodge (Level 5)**
- Can't be flanked unless flanker's rogue level exceeds Barbarian level by 4+
- *Implementation:* In flanking detection code, check `target.Stats.HasImprovedUncannyDodge` and attacker's rogue level.

```csharp
public bool HasImprovedUncannyDodge =>
    (IsBarbarian && GetClassLevel("Barbarian") >= 5) ||
    (IsRogue && GetClassLevel("Rogue") >= 8);

public int ImprovedUncannyDodgeEffectiveLevel
{
    get
    {
        int best = 0;
        if (IsBarbarian) best = Mathf.Max(best, GetClassLevel("Barbarian"));
        if (IsRogue) best = Mathf.Max(best, GetClassLevel("Rogue"));
        return best;
    }
}
```

**Damage Reduction (Level 7)**
- DR 1/— at level 7, 2/— at 10, 3/— at 13, 4/— at 16, 5/— at 19
- *Formula:* `DR = (BarbarianLevel - 4) / 3` (minimum 0, starts at 7)
- *Implementation:* Add to `DamageReductions` list in CharacterStats during level-up or recalculation.

```csharp
public int BarbarianDamageReduction
{
    get
    {
        int lvl = GetClassLevel("Barbarian");
        if (lvl < 7) return 0;
        return 1 + (lvl - 7) / 3; // 1 at 7, 2 at 10, 3 at 13, 4 at 16, 5 at 19
    }
}
```

**Greater Rage (Level 11)**
- STR/CON bonus increases to +6, Will bonus to +3
- *Implementation:* Modify `StartRage()` to check level and apply appropriate bonuses.

**Indomitable Will (Level 14)**
- While raging: +4 bonus on Will saves against enchantments
- *Implementation:* In Will save calculation, add bonus when `IsRaging && GetClassLevel("Barbarian") >= 14`.

**Tireless Rage (Level 17)**
- No longer fatigued after rage ends
- *Implementation:* In `EndRage()`, skip fatigue application if level 17+.

**Mighty Rage (Level 20)**
- STR/CON bonus increases to +8, Will bonus to +4
- *Implementation:* Further scaling in `StartRage()`.

```csharp
public void StartRage()
{
    int lvl = GetClassLevel("Barbarian");
    int strConBonus = lvl >= 20 ? 8 : (lvl >= 11 ? 6 : 4);
    int willBonus = lvl >= 20 ? 4 : (lvl >= 11 ? 3 : 2);
    // Apply bonuses...
}
```

---

### 3.5 FIGHTER (Completion)

#### What's Already Done
- Class file, starting equipment, quickstart character
- d10 HD, full BAB, good Fort save
- All combat feats exist in FeatDefinitions

#### What Needs to Be Added

**Bonus Feat System**
- Fighter gains a bonus combat feat at level 1 and every even level (2, 4, 6, 8, 10, 12, 14, 16, 18, 20)
- Total: 11 bonus feats by level 20 (plus normal feats at 1, 3, 6, 9, 12, 15, 18)
- Must be from the Fighter Bonus Feat list (combat feats)

```csharp
public static int GetBonusFeatCount(int fighterLevel)
{
    if (fighterLevel < 1) return 0;
    return 1 + fighterLevel / 2; // 1 at 1, 2 at 2, 3 at 4, ... 11 at 20
}
```

**Fighter Bonus Feat List** (PHB p.38):
Combat Expertise, Improved Disarm, Improved Feint, Improved Trip, Whirlwind Attack,
Dodge, Mobility, Spring Attack, Combat Reflexes, Improved Critical, Improved Initiative,
Improved Shield Bash, Power Attack, Cleave, Great Cleave, Improved Bull Rush, Improved Overrun, Improved Sunder,
Point Blank Shot, Far Shot, Precise Shot, Rapid Shot, Manyshot, Shot on the Run, Improved Precise Shot,
Quick Draw, Rapid Reload, Two-Weapon Fighting, Two-Weapon Defense, Improved Two-Weapon Fighting, Greater Two-Weapon Fighting,
Weapon Finesse, Weapon Focus, Weapon Specialization, Greater Weapon Focus, Greater Weapon Specialization,
Blind-Fight, Mounted Combat, Mounted Archery, Ride-By Attack, Spirited Charge, Trample

**Greater Weapon Specialization (Level 12+)**
- Prerequisite: Fighter level 12, Weapon Specialization, Greater Weapon Focus with that weapon
- Additional +2 damage (total +4 with Weapon Specialization)

#### UI Components Needed
- Bonus feat selection UI during level-up (show only eligible Fighter bonus feats)
- Feat prerequisite validation

---

### 3.6 DRUID

#### Class Overview
- **Role:** Versatile divine caster — shapeshifter, summoner, controller with animal companion
- **Playstyle:** Nature magic + melee via Wild Shape; excellent battlefield control
- **Iconic Abilities:** Wild Shape, Animal Companion, 9th-level divine casting
- **Complexity Rating:** ⭐⭐⭐⭐⭐ (5/5) — highest complexity; Wild Shape alone is a major system

#### Core Mechanics
| Stat | Value |
|------|-------|
| Hit Die | d8 |
| BAB | Medium (3/4 per level) |
| Good Saves | Fortitude, Will |
| Skill Points | 4 + INT mod per level |
| Proficiencies | Club, dagger, dart, quarterstaff, scimitar, sickle, shortspear, sling, spear; light/medium armor (non-metal), shields (non-metal) |
| Casting Stat | Wisdom |
| Casting Type | Prepared Divine (full 9-level progression) |

#### Class Skills
Concentration, Craft, Diplomacy, Handle Animal, Heal, Knowledge (Nature), Listen, Profession, Ride, Spellcraft, Spot, Survival, Swim

#### Level Progression (1–20)

| Level | BAB | Fort | Ref | Will | Special |
|-------|-----|------|-----|------|---------|
| 1 | +0 | +2 | +0 | +2 | Animal Companion, Nature Sense, Wild Empathy |
| 2 | +1 | +3 | +0 | +3 | Woodland Stride |
| 3 | +2 | +3 | +1 | +3 | Trackless Step |
| 4 | +3 | +4 | +1 | +4 | Resist Nature's Lure |
| 5 | +3 | +4 | +1 | +4 | Wild Shape 1/day (Small/Medium animal) |
| 6 | +4 | +5 | +2 | +5 | Wild Shape 2/day |
| 7 | +5 | +5 | +2 | +5 | Wild Shape 3/day (Large animal) |
| 8 | +6/+1 | +6 | +2 | +6 | Wild Shape (Large) |
| 9 | +6/+1 | +6 | +3 | +6 | Venom Immunity |
| 10 | +7/+2 | +7 | +3 | +7 | Wild Shape 4/day |
| 11 | +8/+3 | +7 | +3 | +7 | Wild Shape (Tiny) |
| 12 | +9/+4 | +8 | +4 | +8 | Wild Shape (Plant — Large) |
| 13 | +9/+4 | +8 | +4 | +8 | A Thousand Faces |
| 14 | +10/+5 | +9 | +4 | +9 | Wild Shape 5/day |
| 15 | +11/+6/+1 | +9 | +5 | +9 | Timeless Body, Wild Shape (Huge) |
| 16 | +12/+7/+2 | +10 | +5 | +10 | Wild Shape (Huge elemental) |
| 17 | +12/+7/+2 | +10 | +5 | +10 | — |
| 18 | +13/+8/+3 | +11 | +6 | +11 | Wild Shape 6/day (Huge elemental) |
| 19 | +14/+9/+4 | +11 | +6 | +11 | — |
| 20 | +15/+10/+5 | +12 | +6 | +12 | Wild Shape (Huge elemental, at will) |

#### Druid Spells Per Day

| Level | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|-------|---|---|---|---|---|---|---|---|---|---|
| 1 | 3 | 1 | — | — | — | — | — | — | — | — |
| 2 | 4 | 2 | — | — | — | — | — | — | — | — |
| 3 | 4 | 2 | 1 | — | — | — | — | — | — | — |
| 4 | 5 | 3 | 2 | — | — | — | — | — | — | — |
| 5 | 5 | 3 | 2 | 1 | — | — | — | — | — | — |
| 6 | 5 | 3 | 3 | 2 | — | — | — | — | — | — |
| 7 | 6 | 4 | 3 | 2 | 1 | — | — | — | — | — |
| 8 | 6 | 4 | 3 | 3 | 2 | — | — | — | — | — |
| 9 | 6 | 4 | 4 | 3 | 2 | 1 | — | — | — | — |
| 10 | 6 | 4 | 4 | 3 | 3 | 2 | — | — | — | — |
| 11 | 6 | 5 | 4 | 4 | 3 | 2 | 1 | — | — | — |
| 12 | 6 | 5 | 4 | 4 | 3 | 3 | 2 | — | — | — |
| 13 | 6 | 5 | 5 | 4 | 4 | 3 | 2 | 1 | — | — |
| 14 | 6 | 5 | 5 | 4 | 4 | 3 | 3 | 2 | — | — |
| 15 | 6 | 5 | 5 | 5 | 4 | 4 | 3 | 2 | 1 | — |
| 16 | 6 | 5 | 5 | 5 | 4 | 4 | 3 | 3 | 2 | — |
| 17 | 6 | 5 | 5 | 5 | 5 | 4 | 4 | 3 | 2 | 1 |
| 18 | 6 | 5 | 5 | 5 | 5 | 4 | 4 | 3 | 3 | 2 |
| 19 | 6 | 5 | 5 | 5 | 5 | 5 | 4 | 4 | 3 | 3 |
| 20 | 6 | 5 | 5 | 5 | 5 | 5 | 4 | 4 | 4 | 4 |

#### Unique Systems Required

**Wild Shape System** (MAJOR — estimated 2-3 weeks alone)

Wild Shape is effectively polymorph into animal forms with specific rules:
- Duration: 1 hour per Druid level
- Uses per day: 1 at 5, +1 per 2 levels thereafter
- Size range: Small/Medium at 5, Large at 8, Tiny at 11, Huge at 15
- Plant forms at 12+, Elemental forms at 16+

```csharp
public class WildShapeSystem
{
    public int UsesPerDay;
    public int UsesRemaining;
    public bool IsShifted;
    public WildShapeForm CurrentForm;
    public int DurationRounds;        // 10 * DruidLevel rounds (1 hour/level)
    public int RoundsRemaining;

    public List<WildShapeForm> AvailableForms;

    public void Shift(WildShapeForm form, CharacterStats stats)
    {
        // Store original stats
        // Apply form's physical ability scores (STR, DEX, CON)
        // Apply natural armor, natural attacks, movement modes
        // Keep mental scores (INT, WIS, CHA), class features, HP, saves
        // Gain form's extraordinary abilities (Scent, Low-Light Vision, etc.)
        // Lose ability to cast spells (unless Natural Spell feat)
    }

    public void Revert(CharacterStats stats)
    {
        // Restore original physical stats
        // Remove natural attacks/armor
    }
}

public class WildShapeForm
{
    public string FormName;           // "Wolf", "Bear", "Eagle"
    public CreatureSizeCategory Size;
    public int StrengthScore;
    public int DexterityScore;
    public int ConstitutionScore;
    public int NaturalArmor;
    public int BaseSpeed;             // In feet
    public List<NaturalAttack> Attacks;
    public List<string> SpecialAbilities; // Scent, Trip, etc.
    public WildShapeFormType FormType;    // Animal, Plant, Elemental
}
```

**Animal Companion System** (see §4.1 — shared with Ranger)

**Spontaneous Summon Nature's Ally**
- Druid can "lose" a prepared spell to spontaneously cast *summon nature's ally* of the same level
- Similar to Cleric's spontaneous Cure/Inflict conversion
- *Implementation:* In `SpontaneousCastingType`, add Druid option that maps each spell level to the corresponding *summon nature's ally* spell ID.

#### UI Components Needed
- Wild Shape selection panel (list available forms, show stats preview)
- Wild Shape status indicator (current form, duration remaining)
- Animal Companion management panel (see §4.1)
- Natural attack display (when in wild shape form)
- Spell preparation UI: reuse Cleric-style with Druid spell list + spontaneous SNA conversion

---

### 3.7 BARD

#### Class Overview
- **Role:** Jack-of-all-trades — spontaneous arcane caster, party buffer, skill monkey
- **Playstyle:** Support and utility; Bardic Music enhances the whole party
- **Iconic Abilities:** Bardic Music, spontaneous casting (6-level max), Bardic Knowledge
- **Complexity Rating:** ⭐⭐⭐⭐ (4/5) — Bardic Music is a complex system with many abilities

#### Core Mechanics
| Stat | Value |
|------|-------|
| Hit Die | d6 |
| BAB | Medium (3/4 per level) |
| Good Saves | Reflex, Will |
| Skill Points | 6 + INT mod per level |
| Proficiencies | Simple weapons, longsword, rapier, sap, short sword, shortbow, whip; light armor, shields (except tower shield) |
| Casting Stat | Charisma |
| Casting Type | Spontaneous Arcane (max 6th-level spells) |

#### Class Skills
Appraise, Balance, Bluff, Climb, Concentration, Craft, Decipher Script, Diplomacy, Disguise, Escape Artist, Gather Information, Hide, Jump, Knowledge (all), Listen, Move Silently, Perform, Profession, Sense Motive, Sleight of Hand, Speak Language, Spellcraft, Swim, Tumble, Use Magic Device

#### Level Progression (1–20)

| Level | BAB | Fort | Ref | Will | Special | Spells Known (0/1/2/3/4/5/6) | Spells/Day (0/1/2/3/4/5/6) |
|-------|-----|------|-----|------|---------|-------------------------------|------------------------------|
| 1 | +0 | +0 | +2 | +2 | Bardic Music, Bardic Knowledge, Countersong, Fascinate, Inspire Courage +1 | 4/—/—/—/—/—/— | 2/—/—/—/—/—/— |
| 2 | +1 | +0 | +3 | +3 | — | 5/2¹/—/—/—/—/— | 3/0/—/—/—/—/— |
| 3 | +2 | +1 | +3 | +3 | Inspire Competence | 6/3/—/—/—/—/— | 3/1/—/—/—/—/— |
| 4 | +3 | +1 | +4 | +4 | — | 6/3/2¹/—/—/—/— | 3/2/0/—/—/—/— |
| 5 | +3 | +1 | +4 | +4 | — | 6/4/3/—/—/—/— | 3/3/1/—/—/—/— |
| 6 | +4 | +2 | +5 | +5 | Suggestion | 6/4/3/—/—/—/— | 3/3/2/—/—/—/— |
| 7 | +5 | +2 | +5 | +5 | — | 6/4/4/2¹/—/—/— | 3/3/2/0/—/—/— |
| 8 | +6/+1 | +2 | +6 | +6 | Inspire Courage +2 | 6/4/4/3/—/—/— | 3/3/3/1/—/—/— |
| 9 | +6/+1 | +3 | +6 | +6 | Inspire Greatness | 6/4/4/3/—/—/— | 3/3/3/2/—/—/— |
| 10 | +7/+2 | +3 | +7 | +7 | — | 6/4/4/4/2¹/—/— | 3/3/3/2/0/—/— |
| 11 | +8/+3 | +3 | +7 | +7 | — | 6/4/4/4/3/—/— | 3/3/3/3/1/—/— |
| 12 | +9/+4 | +4 | +8 | +8 | Song of Freedom | 6/4/4/4/3/—/— | 3/3/3/3/2/—/— |
| 13 | +9/+4 | +4 | +8 | +8 | — | 6/4/4/4/4/2¹/— | 3/3/3/3/2/0/— |
| 14 | +10/+5 | +4 | +9 | +9 | Inspire Courage +3 | 6/4/4/4/4/3/— | 4/3/3/3/3/1/— |
| 15 | +11/+6/+1 | +5 | +9 | +9 | Inspire Heroics | 6/4/4/4/4/3/— | 4/4/3/3/3/2/— |
| 16 | +12/+7/+2 | +5 | +10 | +10 | — | 6/5/4/4/4/4/2¹ | 4/4/4/3/3/2/0 |
| 17 | +12/+7/+2 | +5 | +10 | +10 | — | 6/5/5/4/4/4/3 | 4/4/4/4/3/3/1 |
| 18 | +13/+8/+3 | +6 | +11 | +11 | Mass Suggestion | 6/5/5/5/4/4/3 | 4/4/4/4/4/3/2 |
| 19 | +14/+9/+4 | +6 | +11 | +11 | — | 6/5/5/5/5/4/4 | 4/4/4/4/4/4/3 |
| 20 | +15/+10/+5 | +6 | +12 | +12 | Inspire Courage +4 | 6/5/5/5/5/5/4 | 4/4/4/4/4/4/4 |

¹ = Gained at this level

#### Unique Systems Required

**Bardic Music System** (MAJOR)

Bardic Music is activated as a standard action and maintained as a free action each round. Uses per day = Bard level.

```csharp
public class BardicMusicSystem
{
    public int UsesPerDay;        // = Bard level
    public int UsesRemaining;
    public bool IsPerforming;
    public BardicMusicType ActivePerformance;
    public int InspireCourageBonus; // +1 at 1, +2 at 8, +3 at 14, +4 at 20

    public enum BardicMusicType
    {
        None,
        Countersong,       // Level 1: Allies use Bard's Perform check vs sonic/language saves
        Fascinate,         // Level 1: Creatures within 90ft are fascinated (Will save)
        InspireCourage,    // Level 1: Allies get morale bonus to attack/damage and saves vs fear/charm
        InspireCompetence, // Level 3: One ally gets +2 competence bonus on skill checks
        Suggestion,        // Level 6: As suggestion spell on fascinated creature
        InspireGreatness,  // Level 9: 1+ allies get bonus HD, attack, Fort save
        SongOfFreedom,     // Level 12: break enchantment effect
        InspireHeroics,    // Level 15: +4 dodge AC and +4 morale on saves to one ally
        MassSuggestion     // Level 18: suggestion on all fascinated creatures
    }
}
```

**Key Bardic Music Abilities:**

*Inspire Courage (Level 1):*
- All allies within 30 feet gain:
  - Morale bonus on saves vs charm and fear: +1 (levels 1-7), +2 (8-13), +3 (14-19), +4 (20)
  - Competence bonus on attack and weapon damage: same progression
- Maintained as free action; requires Bard to continue performing

*Inspire Greatness (Level 9):*
- Target: 1 ally at level 9, +1 ally per 3 levels thereafter
- Grants: +2 bonus HD (d10), +2 competence bonus on attack, +1 competence bonus on Fort saves
- Extra HP from bonus HD are temporary HP

*Countersong (Level 1):*
- Within 30 feet, allies can use Bard's Perform check in place of saving throws against sonic or language-dependent magical attacks for 10 rounds

**Bardic Knowledge**
- Check: d20 + Bard level + INT modifier
- Like a lore check — can identify magic items, recall historical facts, etc.
- *Implementation:* Passive ability; could be used in item identification or lore system.

#### Bard Spells Per Day Table
```csharp
private static readonly int[,] BardSlotsPerDay = new int[20, 7]
{
    //       0  1  2  3  4  5  6
    /*  1*/ {2, 0, 0, 0, 0, 0, 0},
    /*  2*/ {3, 0, 0, 0, 0, 0, 0},
    /*  3*/ {3, 1, 0, 0, 0, 0, 0},
    /*  4*/ {3, 2, 0, 0, 0, 0, 0},
    /*  5*/ {3, 3, 1, 0, 0, 0, 0},
    /*  6*/ {3, 3, 2, 0, 0, 0, 0},
    /*  7*/ {3, 3, 2, 0, 0, 0, 0},
    /*  8*/ {3, 3, 3, 1, 0, 0, 0},
    /*  9*/ {3, 3, 3, 2, 0, 0, 0},
    /* 10*/ {3, 3, 3, 2, 0, 0, 0},
    /* 11*/ {3, 3, 3, 3, 1, 0, 0},
    /* 12*/ {3, 3, 3, 3, 2, 0, 0},
    /* 13*/ {3, 3, 3, 3, 2, 0, 0},
    /* 14*/ {4, 3, 3, 3, 3, 1, 0},
    /* 15*/ {4, 4, 3, 3, 3, 2, 0},
    /* 16*/ {4, 4, 4, 3, 3, 2, 0},
    /* 17*/ {4, 4, 4, 4, 3, 3, 1},
    /* 18*/ {4, 4, 4, 4, 4, 3, 2},
    /* 19*/ {4, 4, 4, 4, 4, 4, 3},
    /* 20*/ {4, 4, 4, 4, 4, 4, 4}
};

private static readonly int[,] BardSpellsKnown = new int[20, 7]
{
    //       0  1  2  3  4  5  6
    /*  1*/ {4, 0, 0, 0, 0, 0, 0},
    /*  2*/ {5, 2, 0, 0, 0, 0, 0},
    /*  3*/ {6, 3, 0, 0, 0, 0, 0},
    /*  4*/ {6, 3, 2, 0, 0, 0, 0},
    /*  5*/ {6, 4, 3, 0, 0, 0, 0},
    /*  6*/ {6, 4, 3, 0, 0, 0, 0},
    /*  7*/ {6, 4, 4, 2, 0, 0, 0},
    /*  8*/ {6, 4, 4, 3, 0, 0, 0},
    /*  9*/ {6, 4, 4, 3, 0, 0, 0},
    /* 10*/ {6, 4, 4, 4, 2, 0, 0},
    /* 11*/ {6, 4, 4, 4, 3, 0, 0},
    /* 12*/ {6, 4, 4, 4, 3, 0, 0},
    /* 13*/ {6, 4, 4, 4, 4, 2, 0},
    /* 14*/ {6, 4, 4, 4, 4, 3, 0},
    /* 15*/ {6, 4, 4, 4, 4, 3, 0},
    /* 16*/ {6, 5, 4, 4, 4, 4, 2},
    /* 17*/ {6, 5, 5, 4, 4, 4, 3},
    /* 18*/ {6, 5, 5, 5, 4, 4, 3},
    /* 19*/ {6, 5, 5, 5, 5, 4, 4},
    /* 20*/ {6, 5, 5, 5, 5, 5, 4}
};
```

#### UI Components Needed
- Bardic Music activation panel (pick which song to perform)
- Bardic Music status indicator (active performance, rounds remaining, uses left)
- Inspire Courage party-wide buff indicator
- Spontaneous casting panel (shared with Sorcerer)
- Spells Known selection at level-up (shared with Sorcerer)

---

### 3.8 ROGUE (Completion)

#### What Needs to Be Added

**Trap Sense (Level 3)**
- +1 bonus to Reflex saves and AC against traps, per 3 Rogue levels
- *Formula:* `TrapSenseBonus = RogueLevel / 3` (max +6 at level 18)

**Uncanny Dodge (Level 4)**
- Can't be caught flat-footed; retains DEX bonus to AC even when attacked by invisible creature
- *Implementation:* In flat-footed checks, if `HasUncannyDodge`, character always keeps DEX to AC.

**Improved Uncanny Dodge (Level 8)**
- Can't be flanked (same as Barbarian implementation above)
- *Implementation:* Shared code with Barbarian version.

**Special Abilities (Levels 10, 13, 16, 19)**
- Player selects one from list at each of these levels:
  - **Crippling Strike:** Sneak attack deals 2 STR damage in addition to normal damage
  - **Defensive Roll:** Once per day, make Reflex save (DC = damage) to take half damage from an attack that would otherwise reduce HP to 0 or below
  - **Improved Evasion:** On failed Reflex save, take half damage (on success, no damage)
  - **Opportunist:** Once per round, AoO against foe that was just struck by another character
  - **Skill Mastery:** Choose several skills; always take 10 on those skills
  - **Slippery Mind:** If failed Will save against enchantment, get another save 1 round later
  - **Feat:** Take any feat the Rogue qualifies for

```csharp
public enum RogueSpecialAbility
{
    CripplingStrike,
    DefensiveRoll,
    ImprovedEvasion,
    Opportunist,
    SkillMastery,
    SlipperyMind,
    BonusFeat
}
```

---

### 3.9 MONK (Completion)

#### What Needs to Be Added (High-Level Features)

**Ki Strike (Levels 4, 10, 16)**
- Level 4: Unarmed attacks count as magic for bypassing DR
- Level 10: Also count as lawful
- Level 16: Also count as adamantine
- *Implementation:* Tag unarmed attacks with `DamageBypassTag.Magic` at 4, `Lawful` at 10, `Adamantine` at 16.

**Slow Fall (Level 4+)**
- Reduce falling damage: 20 ft at 4, 30 at 6, 40 at 8, 50 at 10, 60 at 12, 70 at 14, 80 at 16, 90 at 18, any at 20
- *Implementation:* Low priority for combat prototype.

**Purity of Body (Level 5)** — Immune to all diseases. Set `ImmuneToDiseases` flag.

**Wholeness of Body (Level 7)** — Heal self for Monk level × 2 HP per day. Standard action.

**Improved Evasion (Level 9)** — On failed save, take half damage. On success, no damage.

**Diamond Body (Level 11)** — Immune to all poisons. Set `ImmuneToPoison` flag.

**Abundant Step (Level 12)** — 1/day, as *dimension door* with CL = half Monk level.

**Diamond Soul (Level 13)** — SR = Monk level + 10.

**Quivering Palm (Level 15)** — 1/week. If unarmed strike hits, target must make Fort save (DC 10 + half Monk level + WIS mod) or die. Usable only on living creatures with discernible anatomy.

**Empty Body (Level 19)** — Become ethereal for 1 round per Monk level per day.

**Perfect Self (Level 20)** — DR 10/magic, treated as outsider.

---

## 4. SHARED SYSTEMS

### 4.1 Animal Companion System (Druid + Ranger)

Both Druid and Ranger gain animal companions, but at different effective levels:
- **Druid:** Effective level = Druid level
- **Ranger:** Effective level = Ranger level - 3

```csharp
public class AnimalCompanionSystem
{
    public string CompanionType;         // "Wolf", "Bear", "Eagle", etc.
    public int EffectiveDruidLevel;
    public AnimalCompanionStats Stats;
    public bool IsActive;

    // Companion improves with master's level:
    // Bonus HD, Natural Armor, STR/DEX, Bonus Tricks, Special Abilities
}

public class AnimalCompanionStats
{
    public int BonusHD;        // +0 at 1-2, +2 at 3-5, +4 at 6-8, etc.
    public int NaturalArmorAdj; // +0, +2, +4, +6, etc.
    public int StrDexAdj;       // +0, +1, +2, +3, etc.
    public int BonusTricks;     // +1, +2, +3, etc.
    public List<string> SpecialAbilities; // Link, Share Spells, Evasion, Devotion, Multiattack, Improved Evasion
}
```

**Companion Progression Table:**

| Effective Level | Bonus HD | Natural Armor | STR/DEX | Bonus Tricks | Special |
|----------------|----------|---------------|---------|-------------|---------|
| 1-2 | +0 | +0 | +0 | +1 | Link, Share Spells |
| 3-5 | +2 | +2 | +1 | +2 | Evasion |
| 6-8 | +4 | +4 | +2 | +3 | Devotion |
| 9-11 | +6 | +6 | +3 | +4 | Multiattack |
| 12-14 | +8 | +8 | +4 | +5 | — |
| 15-17 | +10 | +10 | +5 | +6 | Improved Evasion |
| 18-20 | +12 | +12 | +6 | +7 | — |

**Available Companions (1st level):** Badger, Camel, Dire Rat, Dog, Eagle, Hawk, Horse (light/heavy), Owl, Pony, Snake (Small/Medium viper), Wolf

**Higher-Level Companions (available at higher effective levels):** Bear, Crocodile, Dire Badger, Dire Bat, Dire Weasel, Leopard, Lizard (Monitor), Shark, Snake (Large viper/constrictor), Wolverine

**UI:** Companion management panel showing stats, tricks, and commands.

### 4.2 Spontaneous Casting System (Sorcerer + Bard)

Shared system for both spontaneous casters. Key difference from prepared casting:
- No spell preparation step
- Fixed spells known (cannot change daily)
- Any known spell can be cast using a slot of that level
- Bonus slots from ability score (CHA)

See §3.1 for full `SpontaneousCastingData` class.

**Integration with existing `SpellcastingComponent`:**
```csharp
// Add to SpellcastingComponent:
public bool IsSpontaneousCaster => className == "Sorcerer" || className == "Bard";
public SpontaneousCastingData SpontaneousData;  // Only used if IsSpontaneousCaster

// In casting resolution:
if (IsSpontaneousCaster)
{
    // Check: is spell in SpellsKnownByLevel[spellLevel]?
    // Spend: SlotsRemaining[spellLevel]--
}
else
{
    // Existing prepared slot system
}
```

### 4.3 Turn Undead (Cleric + Paladin)

Already fully implemented for Cleric. For Paladin:
- Same mechanics, but effective turning level = Paladin level - 3
- Uses per day: 3 + CHA modifier (same formula as Cleric)

**Integration:**
```csharp
// In CharacterStats.MaxTurnUndeadAttemptsPerDay:
public int MaxTurnUndeadAttemptsPerDay
{
    get
    {
        if (IsCleric) return 3 + Mathf.Max(0, CharismaModifier);
        if (IsPaladin && GetClassLevel("Paladin") >= 4) return 3 + Mathf.Max(0, CharismaModifier);
        return 0;
    }
}

// Effective turning level:
public int EffectiveTurningLevel
{
    get
    {
        if (IsCleric) return GetClassLevel("Cleric");
        if (IsPaladin) return Mathf.Max(1, GetClassLevel("Paladin") - 3);
        return 0;
    }
}
```

### 4.4 Bonus Feat Selection UI

Multiple classes need bonus feat selection at level-up:
- **Fighter:** Every even level (combat feats only)
- **Wizard:** Every 5 levels (metamagic or item creation feats only)
- **Ranger:** At levels 2/6/11 (specific feats based on combat style)

**Shared Component:**
```csharp
public class BonusFeatSelectionUI
{
    /// <summary>Show feat selection filtered by allowed feats for the class.</summary>
    public void Show(CharacterStats stats, List<string> allowedFeats, Action<string> onSelected)
    {
        // Display eligible feats (from allowed list, meeting prerequisites)
        // Player selects one
        // Grant feat via onSelected callback
    }
}
```

### 4.5 Half-Caster Spell Slot System (Paladin + Ranger)

Both Paladin and Ranger:
- Start casting at level 4
- Only spell levels 1-4
- Prepared divine casting
- Small spell lists (~16-20 spells each)

**Shared slot table structure** (same table shape, different values):
```csharp
// Add to SpellcastingComponent:
private void InitHalfCasterSpellSlots(string className, int classLevel)
{
    // Use Paladin or Ranger table based on className
    // Same logic as InitClericSpellSlots but:
    //   - Only 4 spell levels (1-4)
    //   - No domain slots
    //   - WIS bonus slots
    //   - Start at class level 4
}
```

---

## 5. IMPLEMENTATION ROADMAP

### Phase 1 — Simple Classes & Completions (Weeks 1-3)

| Week | Task | Effort | Dependencies |
|------|------|--------|-------------|
| 1 | **Sorcerer class file** + spontaneous casting data model | 3 days | None |
| 1 | **SpontaneousCastingData** in SpellcastingComponent | 2 days | Sorcerer class |
| 2 | **Sorcerer spell selection UI** at level-up + creation | 3 days | SpontaneousCastingData |
| 2 | **Sorcerer integration:** casting resolution, spell prep UI branch | 2 days | All above |
| 2 | **Fighter bonus feat** system + UI | 2 days | None |
| 3 | **Barbarian completion:** rage scaling, Greater/Mighty Rage, DR, Improved Uncanny Dodge | 3 days | None |
| 3 | **Rogue completion:** Uncanny Dodge, Improved Uncanny Dodge, Special Abilities | 2 days | None |

**Deliverables:** 
- Sorcerer fully playable (spontaneous casting, familiar, spells known selection)
- Fighter bonus feat system working
- Barbarian rage properly scales to level 20
- Rogue has all core defensive features

### Phase 2 — Medium Complexity (Weeks 4-8)

| Week | Task | Effort | Dependencies |
|------|------|--------|-------------|
| 4 | **Paladin class file** + base stats | 1 day | None |
| 4 | **Smite Evil** system + combat integration | 2 days | Paladin class |
| 4 | **Lay on Hands** + Divine Grace + Aura of Courage | 2 days | Paladin class |
| 5 | **Paladin Turn Undead** integration (use existing system) | 1 day | Paladin class |
| 5 | **Half-caster spell system** (Paladin/Ranger shared) | 3 days | SpellcastingComponent |
| 5 | **Paladin spellcasting** integration | 1 day | Half-caster system |
| 6 | **Ranger class file** + base stats | 1 day | None |
| 6 | **Favored Enemy** system + combat integration | 3 days | Ranger class |
| 6 | **Combat Style** system (Archery/TWF feat grants) | 2 days | Ranger class |
| 7 | **Ranger spellcasting** (reuse half-caster system) | 1 day | Half-caster system |
| 7 | **Animal Companion** system (shared Druid/Ranger) | 4 days | None |
| 8 | **Monk completion:** Ki Strike, Diamond Body/Soul, Quivering Palm | 3 days | None |
| 8 | **Paladin Special Mount** (use summoning framework) | 2 days | Paladin class |

**Deliverables:**
- Paladin fully playable (Smite, LoH, Grace, spells, Turn Undead)
- Ranger fully playable (Favored Enemy, Combat Style, companion, spells)
- Animal Companion system shared between Druid and Ranger
- Monk has all features through level 20

### Phase 3 — Complex Classes (Weeks 9-16)

| Week | Task | Effort | Dependencies |
|------|------|--------|-------------|
| 9-10 | **Wild Shape system** (forms database, stat swapping, natural attacks) | 8 days | None |
| 10 | **Wild Shape UI** (form selection, status display) | 2 days | Wild Shape system |
| 11 | **Druid class file** + nature abilities | 2 days | None |
| 11 | **Druid spellcasting** (reuse Cleric slot system, different spell list) | 2 days | SpellcastingComponent |
| 11 | **Druid spontaneous SNA conversion** | 1 day | Druid spellcasting |
| 12 | **Druid integration** (Wild Shape + companion + casting) | 3 days | All above |
| 13 | **Bard class file** + base stats | 1 day | SpontaneousCastingData (from Phase 1) |
| 13-14 | **Bardic Music system** (all 9 performance types) | 8 days | Bard class |
| 14 | **Bard spellcasting** (reuse Sorcerer spontaneous system, Bard spell list) | 2 days | SpontaneousCastingData |
| 15 | **Bardic Music UI** (activation panel, buff indicators) | 3 days | Bardic Music system |
| 15-16 | **Bard integration + polish** | 4 days | All above |

**Deliverables:**
- Druid fully playable (Wild Shape, companion, full casting)
- Bard fully playable (Bardic Music, spontaneous casting)

---

## 6. TESTING STRATEGY

### Per-Class Test Framework

For each class, create a test file at `Assets/Scripts/Tests/Classes/{ClassName}Tests.cs`:

#### Level 1 Tests
- Verify HD, BAB, saves at level 1
- Verify starting proficiencies
- Verify class skill list
- Verify starting class features are active
- Verify starting equipment

#### Level 10 Tests (Mid-Game)
- Verify BAB/save progression is correct
- Verify all class features up to level 10 are present and functional
- Verify spellcasting (if applicable) has correct slots
- Combat scenario: verify class features affect damage/defense

#### Level 20 Tests (Capstone)
- Verify all class features through level 20
- Verify capstone abilities work
- Verify no regression in lower-level features

#### Multiclass Tests
- Fighter 5/Wizard 5: verify BAB stacking, separate spell progression
- Rogue 3/Fighter 3: verify sneak attack + bonus feats both work
- Paladin 4/Sorcerer 1: verify Turn Undead, separate casting progressions

#### Specific Feature Tests

**Sorcerer:**
- `TestSorcererKnowsCorrectNumberOfSpells()` — verify spells known table
- `TestSorcererSlotsPerDay()` — verify slot table
- `TestSpontaneousCasting()` — can cast any known spell, slot consumed
- `TestCannotCastUnknownSpell()` — verify known spell requirement
- `TestBonusSlotsFromCHA()` — high CHA grants extra slots

**Paladin:**
- `TestSmiteEvilDamage()` — verify CHA to attack, level to damage
- `TestSmiteEvilUsesPerDay()` — verify scaling 1/5/10/15/20
- `TestSmiteEvilVsNonEvil()` — wasted smite does no bonus damage
- `TestDivineGraceSaveBonus()` — CHA mod added to all saves
- `TestLayOnHandsHealing()` — PaladinLevel × CHA HP per day
- `TestPaladinTurnUndeadLevel()` — effective level = Paladin level - 3

**Ranger:**
- `TestFavoredEnemyBonus()` — +2 attack/damage vs chosen type
- `TestFavoredEnemyStacking()` — at level 5, one existing bonus increases by +2
- `TestCombatStyleArchery()` — Rapid Shot granted without prereqs
- `TestCombatStyleTWF()` — Two-Weapon Fighting granted without prereqs
- `TestRangerEvasion()` — Evasion at level 9

**Barbarian (Completion):**
- `TestRageScaling()` — 1/day at 1, 2/day at 4, etc.
- `TestGreaterRage()` — +6 STR/CON at level 11
- `TestBarbarianDR()` — 1/— at 7, 2/— at 10, etc.
- `TestTirelessRage()` — no fatigue at level 17

**Druid:**
- `TestWildShapeTransform()` — stat replacement, attack changes
- `TestWildShapeRevert()` — stats restored correctly
- `TestWildShapeUsesPerDay()` — scaling from level 5
- `TestAnimalCompanionProgression()` — bonus HD/armor/etc.
- `TestSpontaneousSNA()` — convert prepared spell to summon

**Bard:**
- `TestInspireCourageBonus()` — verify +1/+2/+3/+4 progression
- `TestBardicMusicUsesPerDay()` — uses = Bard level
- `TestCountersong()` — allies use Perform check vs sonic saves
- `TestBardSpontaneousCasting()` — same as Sorcerer but Bard spell list

---

## 7. PRIORITY MATRIX

| Class | Popularity | Complexity | Dependencies | Gameplay Variety | **Priority Score** |
|-------|-----------|------------|-------------|-----------------|-------------------|
| Sorcerer | High | Low | None (some Wizard infra) | Medium (another arcane caster) | **1 (Highest)** |
| Fighter (fix) | Very High | Very Low | None | Low (already partially works) | **2** |
| Barbarian (fix) | High | Low | None | Low (already works at low level) | **3** |
| Rogue (fix) | High | Low | None | Low (core features work) | **4** |
| Paladin | High | Medium | Turn Undead (exists) | High (divine warrior archetype) | **5** |
| Ranger | Medium | Medium | Animal Companion shared system | High (exploration/combat hybrid) | **6** |
| Monk (fix) | Medium | Low | None | Low (core features work) | **7** |
| Druid | Medium | Very High | Wild Shape, Animal Companion, Casting | Very High (unique playstyle) | **8** |
| Bard | Medium | High | Spontaneous Casting (from Sorcerer), Bardic Music | Very High (unique support role) | **9** |

### Rationale

1. **Sorcerer first** because it forces creation of the spontaneous casting system that Bard also needs, while being the simplest class to implement (reuses Wizard spell list, no complex features).
2. **Fighter/Barbarian/Rogue/Monk completions** are small, isolated changes that round out existing classes.
3. **Paladin** adds a new archetype (divine warrior) with mostly straightforward mechanics and existing Turn Undead support.
4. **Ranger** introduces Favored Enemy and Combat Style which are unique mechanics, plus shares Animal Companion with Druid.
5. **Druid and Bard** are saved for last because they require the most complex new systems (Wild Shape, Bardic Music).

---

## 8. INTEGRATION CHECKLIST

### Files That Need Changes For Each New Class

| File | Sorcerer | Paladin | Ranger | Druid | Bard |
|------|----------|---------|--------|-------|------|
| `Classes/{Name}Class.cs` | NEW | NEW | NEW | NEW | NEW |
| `Classes/ClassRegistry.cs` | ✏️ | ✏️ | ✏️ | ✏️ | ✏️ |
| `Character/CharacterStats.cs` | ✏️ | ✏️ | ✏️ | ✏️ | ✏️ |
| `Magic/SpellcastingComponent.cs` | ✏️ (spontaneous) | ✏️ (half-caster) | ✏️ (half-caster) | ✏️ (slot tables) | ✏️ (spontaneous) |
| `UI/CharacterCreationUI.cs` | ✏️ | ✏️ | ✏️ | ✏️ | ✏️ |
| `UI/SpellPreparationUI.cs` | ✏️ (spontaneous view) | ✏️ | ✏️ | ✏️ | ✏️ |
| `UI/Panels/ActionButtonPanel.cs` | — | ✏️ (Smite) | — | ✏️ (Wild Shape) | ✏️ (Bardic Music) |
| `Services/CombatFlowService.cs` | — | ✏️ (Smite) | ✏️ (Favored Enemy) | ✏️ (Wild Shape attacks) | ✏️ (Inspire Courage) |
| `Services/AIService.cs` | ✏️ | ✏️ | ✏️ | ✏️ | ✏️ |
| `Core/SceneBootstrap.cs` | — | — | — | — | — |

### New Files Per Phase

**Phase 1:**
- `Classes/SorcererClass.cs`
- `Magic/SpontaneousCastingData.cs`
- `UI/SpellsKnownSelectionUI.cs`
- `Tests/Classes/SorcererTests.cs`

**Phase 2:**
- `Classes/PaladinClass.cs`
- `Classes/RangerClass.cs`
- `Character/SmiteEvilData.cs`
- `Character/LayOnHandsData.cs`
- `Character/FavoredEnemyData.cs`
- `Character/RangerCombatStyleData.cs`
- `Character/AnimalCompanionSystem.cs`
- `Character/AnimalCompanionStats.cs`
- `UI/AnimalCompanionUI.cs`
- `Tests/Classes/PaladinTests.cs`
- `Tests/Classes/RangerTests.cs`

**Phase 3:**
- `Classes/DruidClass.cs`
- `Classes/BardClass.cs`
- `Character/WildShapeSystem.cs`
- `Character/WildShapeForm.cs`
- `Character/BardicMusicSystem.cs`
- `UI/WildShapeSelectionUI.cs`
- `UI/BardicMusicPanel.cs`
- `Tests/Classes/DruidTests.cs`
- `Tests/Classes/BardTests.cs`

---

## 9. RESOURCE REQUIREMENTS

### Art/Visual Assets
| Class | Visual Needs |
|-------|-------------|
| Sorcerer | Class icon, arcane casting VFX (can share Wizard effects) |
| Paladin | Class icon, Smite Evil VFX (divine strike glow), Lay on Hands VFX (healing hands), Special Mount model |
| Ranger | Class icon, Favored Enemy indicator icon, Animal Companion models (wolf, bear, hawk, etc.) |
| Druid | Class icon, Wild Shape transformation VFX, animal form sprites/models for each shiftable form, companion models (shared with Ranger) |
| Bard | Class icon, Bardic Music VFX (musical notes/aura), Inspire Courage party-wide buff indicator, Performance animation |

### Audio
| Class | Audio Needs |
|-------|-------------|
| Paladin | Smite Evil sound effect, Lay on Hands healing sound |
| Druid | Wild Shape transformation sound, nature spell ambient sounds |
| Bard | Bardic Music performance tracks (lute/flute/singing loops), Countersong sound |

---

## 10. APPENDIX — D&D 3.5e REFERENCE FORMULAS

### BAB Progression
- **Good (Fighter, Barbarian, Ranger, Paladin):** Level 1 = +1
- **Medium (Cleric, Druid, Monk, Rogue, Bard):** Level 1 = +0, Level 4 = +3
- **Poor (Wizard, Sorcerer):** Level 1 = +0, Level 4 = +2

### Save Progression
- **Good:** +2 at level 1, increases by +1 every 2 levels → `2 + Level/2`
- **Poor:** +0 at level 1, increases by +1 every 3 levels → `Level/3`

### Bonus Spells from Ability Score
A caster with ability modifier M gets:
- +1 bonus spell at level L if `M >= L` (for spell levels 1-9)
- Never grants access to a new spell level

### Multiclass Spellcasting
- Each casting class tracks spell slots independently
- Caster level = class level in that casting class (not character level)
- Bonus spells based on the relevant ability score for each class

### XP Table (for multiclass penalty reference)
| Level | XP Required | XP for Next |
|-------|-------------|-------------|
| 1 | 0 | 1,000 |
| 2 | 1,000 | 3,000 |
| 3 | 3,000 | 6,000 |
| 4 | 6,000 | 10,000 |
| 5 | 10,000 | 15,000 |
| ... | ... | ... |
| 20 | 190,000 | — |

---

*End of Implementation Plan*
