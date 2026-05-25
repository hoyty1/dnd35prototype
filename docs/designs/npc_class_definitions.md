# NPC Class Definitions — Complete Specifications

**Document Version:** 1.0  
**Date:** 2026-05-25  
**Companion:** `npc_classes_implementation_plan.md`

---

## Table of Contents

- [1. Overview & Shared Rules](#1-overview--shared-rules)
- [2. Adept](#2-adept)
- [3. Aristocrat](#3-aristocrat)
- [4. Commoner](#4-commoner)
- [5. Expert](#5-expert)
- [6. Warrior](#6-warrior)
- [7. C# Implementation Architecture](#7-c-implementation-architecture)

---

## 1. Overview & Shared Rules

### 1.1 NPC Class Common Properties

All NPC classes share these characteristics:
- **Feat Progression:** 1 feat at 1st level, +1 every 3 levels (3rd, 6th, 9th, …) — same as PCs
- **Ability Score Increases:** +1 to one ability score every 4 character levels (4th, 8th, 12th, …)
- **Default Stat Array:** Nonelite (13, 12, 11, 10, 9, 8)
- **CR Calculation:** Character Level − 1 for pure NPC class characters
- **CR when added to monsters:** Always nonassociated (+½ CR per level, up to creature's RHD)
- **Multiclassing:** NPC classes can multiclass freely (except Aristocrat restriction)
- **Max Skill Ranks:** Character Level + 3 (cross-class: half)
- **1st Level HP:** Maximum hit die value + Con modifier
- **Subsequent Levels:** Average roll (round down) + Con modifier

### 1.2 BAB & Save Progression Formulas

| Progression | Formula | Level 1 | Level 5 | Level 10 | Level 15 | Level 20 |
|:-----------|:--------|:--------|:--------|:---------|:---------|:---------|
| **Good BAB** | +level | +1 | +5 | +10 | +15 | +20 |
| **Medium BAB** | +¾ level | +0 | +3 | +7 | +11 | +15 |
| **Poor BAB** | +½ level | +0 | +2 | +5 | +7 | +10 |
| **Good Save** | 2 + level/2 | +2 | +4 | +7 | +9 | +12 |
| **Poor Save** | level/3 | +0 | +1 | +3 | +5 | +6 |

---

## 2. Adept

### 2.1 Class Overview

The Adept is a rudimentary divine spellcaster — a shaman, hedge wizard, or village healer found in isolated communities or among monstrous humanoids. They prepare and cast divine spells from a unique spell list.

### 2.2 Core Statistics

| Property | Value |
|:---------|:------|
| **Hit Die** | d6 |
| **BAB Progression** | Poor (+½ level) |
| **Good Saves** | Fortitude, Will |
| **Skill Points** | 2 + Int modifier per level |
| **Weapon Proficiency** | All simple weapons |
| **Armor Proficiency** | None |
| **Spellcasting** | Divine, prepared (Wisdom-based) |

### 2.3 Class Features

| Level | Feature |
|:------|:--------|
| 1 | Divine spellcasting (0th and 1st level spells) |
| 2 | Summon Familiar (as Sorcerer/Wizard) |

### 2.4 Level Progression Table

| Level | BAB | Fort | Ref | Will | Spells/Day 0th | 1st | 2nd | 3rd | 4th | 5th |
|:------|:----|:-----|:----|:-----|:---------------|:----|:----|:----|:----|:----|
| 1 | +0 | +2 | +0 | +2 | 3 | 1 | — | — | — | — |
| 2 | +1 | +3 | +0 | +3 | 3 | 1 | — | — | — | — |
| 3 | +1 | +3 | +1 | +3 | 3 | 2 | — | — | — | — |
| 4 | +2 | +4 | +1 | +4 | 3 | 2 | 0 | — | — | — |
| 5 | +2 | +4 | +1 | +4 | 3 | 2 | 1 | — | — | — |
| 6 | +3 | +5 | +2 | +5 | 3 | 2 | 1 | — | — | — |
| 7 | +3 | +5 | +2 | +5 | 3 | 3 | 2 | — | — | — |
| 8 | +4 | +6 | +2 | +6 | 3 | 3 | 2 | 0 | — | — |
| 9 | +4 | +6 | +3 | +6 | 3 | 3 | 2 | 1 | — | — |
| 10 | +5 | +7 | +3 | +7 | 3 | 3 | 2 | 1 | — | — |
| 11 | +5 | +7 | +3 | +7 | 3 | 3 | 3 | 2 | — | — |
| 12 | +6/+1 | +8 | +4 | +8 | 3 | 3 | 3 | 2 | 0 | — |
| 13 | +6/+1 | +8 | +4 | +8 | 3 | 3 | 3 | 2 | 1 | — |
| 14 | +7/+2 | +9 | +4 | +9 | 3 | 3 | 3 | 2 | 1 | — |
| 15 | +7/+2 | +9 | +5 | +9 | 3 | 3 | 3 | 3 | 2 | — |
| 16 | +8/+3 | +10 | +5 | +10 | 3 | 3 | 3 | 3 | 2 | 0 |
| 17 | +8/+3 | +10 | +5 | +10 | 3 | 3 | 3 | 3 | 2 | 1 |
| 18 | +9/+4 | +11 | +6 | +11 | 3 | 3 | 3 | 3 | 2 | 1 |
| 19 | +9/+4 | +11 | +6 | +11 | 3 | 3 | 3 | 3 | 3 | 2 |
| 20 | +10/+5 | +12 | +6 | +12 | 3 | 3 | 3 | 3 | 3 | 2 |

*Bonus spells from high Wisdom apply as normal.*

### 2.5 Adept Spell List

**0th Level (Orisons):** *create water, cure minor wounds, detect magic, ghost sound, guidance, light, mending, purify food and drink, read magic, touch of fatigue*

**1st Level:** *bless, burning hands, cause fear, command, comprehend languages, cure light wounds, detect chaos/evil/good/law, endure elements, obscuring mist, protection from chaos/evil/good/law, sleep*

**2nd Level:** *aid, animal trance, bear's endurance, bull's strength, cat's grace, cure moderate wounds, darkness, delay poison, invisibility, mirror image, resist energy, scorching ray, see invisibility, web*

**3rd Level:** *animate dead, bestow curse, contagion, continual flame, cure serious wounds, daylight, deeper darkness, lightning bolt, neutralize poison, remove curse, remove disease, tongues*

**4th Level:** *cure critical wounds, minor creation, polymorph, restoration, stoneskin, wall of fire*

**5th Level:** *baleful polymorph, break enchantment, commune, heal, major creation, raise dead, true seeing, wall of stone*

### 2.6 Class Skills

Concentration, Craft (any), Handle Animal, Heal, Knowledge (all taken individually), Profession (any), Spellcraft, Survival

### 2.7 C# Implementation Notes

```csharp
public class AdeptClass : ICharacterClass
{
    public string ClassName => "Adept";
    public string Description => "A rudimentary divine spellcaster — shaman or hedge wizard.";
    public int HitDie => 6;
    public int BABAtLevel3 => 1;           // Poor: floor(3/2) = 1
    public int SkillPointsPerLevel => 2;
    public bool GoodFortitude => true;
    public bool GoodReflex => false;
    public bool GoodWill => true;
    public bool IsSpellcaster => true;
    // ...
}
```

---

## 3. Aristocrat

### 3.1 Class Overview

The Aristocrat models individuals of high social standing — nobles, courtiers, wealthy merchants, and political leaders. They have broad education and combat training.

### 3.2 Core Statistics

| Property | Value |
|:---------|:------|
| **Hit Die** | d8 |
| **BAB Progression** | Medium (+¾ level) |
| **Good Saves** | Will |
| **Skill Points** | 4 + Int modifier per level |
| **Weapon Proficiency** | All simple and martial weapons |
| **Armor Proficiency** | All armor, all shields (including tower) |
| **Spellcasting** | None |

### 3.3 Class Features

None. The Aristocrat's strength lies in broad proficiencies and a large skill list.

**Special Rule:** A character cannot multiclass *into* Aristocrat unless it was their 1st-level class. Characters can multiclass *out of* Aristocrat freely.

### 3.4 Level Progression Table

| Level | BAB | Fort | Ref | Will |
|:------|:----|:-----|:----|:-----|
| 1 | +0 | +0 | +0 | +2 |
| 2 | +1 | +0 | +0 | +3 |
| 3 | +2 | +1 | +1 | +3 |
| 4 | +3 | +1 | +1 | +4 |
| 5 | +3 | +1 | +1 | +4 |
| 6 | +4 | +2 | +2 | +5 |
| 7 | +5 | +2 | +2 | +5 |
| 8 | +6/+1 | +2 | +2 | +6 |
| 9 | +6/+1 | +3 | +3 | +6 |
| 10 | +7/+2 | +3 | +3 | +7 |
| 11 | +8/+3 | +3 | +3 | +7 |
| 12 | +9/+4 | +4 | +4 | +8 |
| 13 | +9/+4 | +4 | +4 | +8 |
| 14 | +10/+5 | +4 | +4 | +9 |
| 15 | +11/+6/+1 | +5 | +5 | +9 |
| 16 | +12/+7/+2 | +5 | +5 | +10 |
| 17 | +12/+7/+2 | +5 | +5 | +10 |
| 18 | +13/+8/+3 | +6 | +6 | +11 |
| 19 | +14/+9/+4 | +6 | +6 | +11 |
| 20 | +15/+10/+5 | +6 | +6 | +12 |

### 3.5 Class Skills

Appraise, Bluff, Diplomacy, Disguise, Forgery, Gather Information, Handle Animal, Intimidate, Knowledge (all taken individually), Listen, Perform, Ride, Sense Motive, Speak Language, Spot, Swim, Survival

### 3.6 C# Implementation Notes

```csharp
public class AristocratClass : ICharacterClass
{
    public string ClassName => "Aristocrat";
    public string Description => "A noble with broad education and martial training.";
    public int HitDie => 8;
    public int BABAtLevel3 => 2;           // Medium: floor(3*3/4) = 2
    public int SkillPointsPerLevel => 4;
    public bool GoodFortitude => false;
    public bool GoodReflex => false;
    public bool GoodWill => true;
    public bool IsSpellcaster => false;
    // DefaultArmorBonus => 5 (chainmail)
    // DefaultShieldBonus => 2 (heavy shield)
    // DefaultDamageDice => 8 (longsword)
}
```

---

## 4. Commoner

### 4.1 Class Overview

The weakest class in the game. Represents farmers, laborers, servants, and unskilled workers — the vast majority of the population.

### 4.2 Core Statistics

| Property | Value |
|:---------|:------|
| **Hit Die** | d4 |
| **BAB Progression** | Poor (+½ level) |
| **Good Saves** | None |
| **Skill Points** | 2 + Int modifier per level |
| **Weapon Proficiency** | One simple weapon (DM's choice) |
| **Armor Proficiency** | None |
| **Spellcasting** | None |

### 4.3 Class Features

None. The Commoner has no class features whatsoever.

### 4.4 Level Progression Table

| Level | BAB | Fort | Ref | Will |
|:------|:----|:-----|:----|:-----|
| 1 | +0 | +0 | +0 | +0 |
| 2 | +1 | +0 | +0 | +0 |
| 3 | +1 | +1 | +1 | +1 |
| 4 | +2 | +1 | +1 | +1 |
| 5 | +2 | +1 | +1 | +1 |
| 6 | +3 | +2 | +2 | +2 |
| 7 | +3 | +2 | +2 | +2 |
| 8 | +4 | +2 | +2 | +2 |
| 9 | +4 | +3 | +3 | +3 |
| 10 | +5 | +3 | +3 | +3 |
| 11 | +5 | +3 | +3 | +3 |
| 12 | +6/+1 | +4 | +4 | +4 |
| 13 | +6/+1 | +4 | +4 | +4 |
| 14 | +7/+2 | +4 | +4 | +4 |
| 15 | +7/+2 | +5 | +5 | +5 |
| 16 | +8/+3 | +5 | +5 | +5 |
| 17 | +8/+3 | +5 | +5 | +5 |
| 18 | +9/+4 | +6 | +6 | +6 |
| 19 | +9/+4 | +6 | +6 | +6 |
| 20 | +10/+5 | +6 | +6 | +6 |

### 4.5 Class Skills

Climb, Craft (any), Handle Animal, Jump, Listen, Profession (any), Ride, Spot, Swim, Use Rope

### 4.6 C# Implementation Notes

```csharp
public class CommonerClass : ICharacterClass
{
    public string ClassName => "Commoner";
    public string Description => "An untrained laborer — the weakest class in the game.";
    public int HitDie => 4;
    public int BABAtLevel3 => 1;           // Poor: floor(3/2) = 1
    public int SkillPointsPerLevel => 2;
    public bool GoodFortitude => false;
    public bool GoodReflex => false;
    public bool GoodWill => false;
    public bool IsSpellcaster => false;
    // DefaultArmorBonus => 0 (no armor)
    // DefaultShieldBonus => 0
    // DefaultDamageDice => 4 (club or dagger)
}
```

---

## 5. Expert

### 5.1 Class Overview

The skilled professional — blacksmiths, scribes, merchants, navigators, and artisans. Their defining feature is choosing any 10 skills as class skills.

### 5.2 Core Statistics

| Property | Value |
|:---------|:------|
| **Hit Die** | d6 |
| **BAB Progression** | Medium (+¾ level) |
| **Good Saves** | Reflex |
| **Skill Points** | 6 + Int modifier per level |
| **Weapon Proficiency** | All simple weapons |
| **Armor Proficiency** | Light armor only |
| **Spellcasting** | None |

### 5.3 Class Features

| Level | Feature |
|:------|:--------|
| 1 | Choose 10 class skills from any skill list |

### 5.4 Level Progression Table

| Level | BAB | Fort | Ref | Will |
|:------|:----|:-----|:----|:-----|
| 1 | +0 | +0 | +2 | +0 |
| 2 | +1 | +0 | +3 | +0 |
| 3 | +2 | +1 | +3 | +1 |
| 4 | +3 | +1 | +4 | +1 |
| 5 | +3 | +1 | +4 | +1 |
| 6 | +4 | +2 | +5 | +2 |
| 7 | +5 | +2 | +5 | +2 |
| 8 | +6/+1 | +2 | +6 | +2 |
| 9 | +6/+1 | +3 | +6 | +3 |
| 10 | +7/+2 | +3 | +7 | +3 |
| 11 | +8/+3 | +3 | +7 | +3 |
| 12 | +9/+4 | +4 | +8 | +4 |
| 13 | +9/+4 | +4 | +8 | +4 |
| 14 | +10/+5 | +4 | +9 | +4 |
| 15 | +11/+6/+1 | +5 | +9 | +5 |
| 16 | +12/+7/+2 | +5 | +10 | +5 |
| 17 | +12/+7/+2 | +5 | +10 | +5 |
| 18 | +13/+8/+3 | +6 | +11 | +6 |
| 19 | +14/+9/+4 | +6 | +11 | +6 |
| 20 | +15/+10/+5 | +6 | +12 | +6 |

### 5.5 Default Class Skills (DM-Configurable)

The Expert's 10 class skills are chosen at creation. Common presets:

**Merchant:** Appraise, Bluff, Diplomacy, Forgery, Gather Information, Knowledge (local), Listen, Profession (merchant), Sense Motive, Spot

**Blacksmith:** Appraise, Craft (armorsmithing), Craft (weaponsmithing), Craft (blacksmithing), Knowledge (architecture), Listen, Profession (blacksmith), Search, Spot, Use Rope

**Sage:** Decipher Script, Gather Information, Knowledge (arcana), Knowledge (history), Knowledge (nature), Knowledge (religion), Knowledge (the planes), Listen, Profession (scribe), Speak Language

### 5.6 C# Implementation Notes

```csharp
public class ExpertClass : ICharacterClass
{
    public string ClassName => "Expert";
    public string Description => "A skilled professional with 10 chosen class skills.";
    public int HitDie => 6;
    public int BABAtLevel3 => 2;           // Medium
    public int SkillPointsPerLevel => 6;
    public bool GoodFortitude => false;
    public bool GoodReflex => true;
    public bool GoodWill => false;
    public bool IsSpellcaster => false;
    
    // The Expert's ClassSkills are dynamic — set at creation time
    private HashSet<string> _chosenSkills = new HashSet<string>();
    
    public HashSet<string> ClassSkills => _chosenSkills;
    
    /// <summary>
    /// Set the Expert's 10 chosen class skills. Must be called during creation.
    /// </summary>
    public void SetChosenSkills(IEnumerable<string> skills)
    {
        _chosenSkills = new HashSet<string>(skills);
    }
}
```

---

## 6. Warrior

### 6.1 Class Overview

A simplified martial class representing trained but unexceptional combatants — city guards, soldiers, caravan guards, and bandits. Full BAB like a Fighter but no bonus feats or class features.

### 6.2 Core Statistics

| Property | Value |
|:---------|:------|
| **Hit Die** | d8 |
| **BAB Progression** | Good (+level) |
| **Good Saves** | Fortitude |
| **Skill Points** | 2 + Int modifier per level |
| **Weapon Proficiency** | All simple and martial weapons |
| **Armor Proficiency** | All armor, all shields (including tower) |
| **Spellcasting** | None |

### 6.3 Class Features

None. The Warrior has no class features.

### 6.4 Level Progression Table

| Level | BAB | Fort | Ref | Will |
|:------|:----|:-----|:----|:-----|
| 1 | +1 | +2 | +0 | +0 |
| 2 | +2 | +3 | +0 | +0 |
| 3 | +3 | +3 | +1 | +1 |
| 4 | +4 | +4 | +1 | +1 |
| 5 | +5 | +4 | +1 | +1 |
| 6 | +6/+1 | +5 | +2 | +2 |
| 7 | +7/+2 | +5 | +2 | +2 |
| 8 | +8/+3 | +6 | +2 | +2 |
| 9 | +9/+4 | +6 | +3 | +3 |
| 10 | +10/+5 | +7 | +3 | +3 |
| 11 | +11/+6/+1 | +7 | +3 | +3 |
| 12 | +12/+7/+2 | +8 | +4 | +4 |
| 13 | +13/+8/+3 | +8 | +4 | +4 |
| 14 | +14/+9/+4 | +9 | +4 | +4 |
| 15 | +15/+10/+5 | +9 | +5 | +5 |
| 16 | +16/+11/+6/+1 | +10 | +5 | +5 |
| 17 | +17/+12/+7/+2 | +10 | +5 | +5 |
| 18 | +18/+13/+8/+3 | +11 | +6 | +6 |
| 19 | +19/+14/+9/+4 | +11 | +6 | +6 |
| 20 | +20/+15/+10/+5 | +12 | +6 | +6 |

### 6.5 Class Skills

Climb, Handle Animal, Intimidate, Jump, Ride, Swim

### 6.6 C# Implementation Notes

```csharp
public class WarriorClass : ICharacterClass
{
    public string ClassName => "Warrior";
    public string Description => "A trained but unexceptional combatant — guard or soldier.";
    public int HitDie => 8;
    public int BABAtLevel3 => 3;           // Good: full BAB
    public int SkillPointsPerLevel => 2;
    public bool GoodFortitude => true;
    public bool GoodReflex => false;
    public bool GoodWill => false;
    public bool IsSpellcaster => false;
    // DefaultArmorBonus => 5 (chainmail)
    // DefaultShieldBonus => 2 (heavy shield)
    // DefaultDamageDice => 8 (longsword)
}
```

---

## 7. C# Implementation Architecture

### 7.1 Shared NPC Class Base (Optional Helper)

While all classes implement `ICharacterClass` directly, a shared helper reduces boilerplate:

```csharp
/// <summary>
/// Optional base for NPC classes that provides common defaults.
/// Not required — each class can implement ICharacterClass directly.
/// </summary>
public abstract class NPCClassBase : ICharacterClass
{
    public abstract string ClassName { get; }
    public abstract string Description { get; }
    public abstract int HitDie { get; }
    public abstract int BABAtLevel3 { get; }
    public abstract int SkillPointsPerLevel { get; }
    public abstract bool GoodFortitude { get; }
    public abstract bool GoodReflex { get; }
    public abstract bool GoodWill { get; }
    public abstract HashSet<string> ClassSkills { get; }
    
    // NPC classes share these defaults
    public virtual int DefaultArmorBonus => 0;
    public virtual int DefaultShieldBonus => 0;
    public virtual int DefaultDamageDice => 4;
    public virtual bool IsSpellcaster => false;
    
    public virtual Color TitleColor => new Color(0.6f, 0.6f, 0.6f);
    public virtual Color ButtonColor => new Color(0.4f, 0.4f, 0.4f);
    public virtual string InfoText => $"Hit Die: d{HitDie} | NPC Class";
    
    public virtual void SetupStartingEquipment(InventoryComponent inv) { }
    public virtual void InitFeats(CharacterStats stats) { }
    
    /// <summary>
    /// Returns true if this is an NPC class (always true for subclasses).
    /// Used by CR calculator to determine nonassociated status.
    /// </summary>
    public bool IsNPCClass => true;
}
```

### 7.2 ClassRegistry Update

```csharp
public static void Init()
{
    if (_initialized) return;
    _initialized = true;

    // Existing PHB classes
    Register(new FighterClass());
    Register(new RogueClass());
    Register(new MonkClass());
    Register(new BarbarianClass());
    Register(new WizardClass());
    Register(new ClericClass());
    Register(new SorcererClass());
    Register(new RangerClass());
    Register(new PaladinClass());
    Register(new BardClass());
    Register(new DruidClass());
    
    // NEW: NPC classes
    Register(new AdeptClass());
    Register(new AristocratClass());
    Register(new CommonerClass());
    Register(new ExpertClass());
    Register(new WarriorClass());

    // ... rest unchanged
}
```

### 7.3 IsNPCClass Detection

Add a helper method to `ClassRegistry` or a utility class:

```csharp
/// <summary>
/// NPC class names for CR calculation (always nonassociated).
/// </summary>
public static readonly HashSet<string> NPCClassNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "Adept", "Aristocrat", "Commoner", "Expert", "Warrior"
};

public static bool IsNPCClass(string className)
{
    return NPCClassNames.Contains(className);
}
```

### 7.4 Adept Spellcasting Integration

The Adept uses the same divine preparation model as the Cleric. Key differences:

```csharp
public class AdeptSpellList
{
    // Spells per day by level (index = spell level 0-5, value = array by class level)
    public static readonly int[,] SpellsPerDay = new int[6, 21]
    {
        // Level:     0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 16 17 18 19 20
        /* 0th */ { 0, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 },
        /* 1st */ { 0, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 },
        /* 2nd */ { 0, 0, 0, 0, 0, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 },
        /* 3rd */ { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3 },
        /* 4th */ { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2, 2, 2, 2, 3, 3 },
        /* 5th */ { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2, 2 },
    };
    
    public static int GetSpellsPerDay(int classLevel, int spellLevel)
    {
        if (classLevel < 1 || classLevel > 20 || spellLevel < 0 || spellLevel > 5)
            return 0;
        return SpellsPerDay[spellLevel, classLevel];
    }
    
    // Spell IDs indexed by spell level
    public static readonly Dictionary<int, List<string>> SpellsByLevel = new Dictionary<int, List<string>>
    {
        [0] = new List<string> { "create_water", "cure_minor_wounds", "detect_magic",
            "ghost_sound", "guidance", "light", "mending", "purify_food_and_drink",
            "read_magic", "touch_of_fatigue" },
        [1] = new List<string> { "bless", "burning_hands", "cause_fear", "command",
            "comprehend_languages", "cure_light_wounds", "detect_chaos", "detect_evil",
            "detect_good", "detect_law", "endure_elements", "obscuring_mist",
            "protection_from_chaos", "protection_from_evil", "protection_from_good",
            "protection_from_law", "sleep" },
        [2] = new List<string> { "aid", "animal_trance", "bears_endurance",
            "bulls_strength", "cats_grace", "cure_moderate_wounds", "darkness",
            "delay_poison", "invisibility", "mirror_image", "resist_energy",
            "scorching_ray", "see_invisibility", "web" },
        [3] = new List<string> { "animate_dead", "bestow_curse", "contagion",
            "continual_flame", "cure_serious_wounds", "daylight", "deeper_darkness",
            "lightning_bolt", "neutralize_poison", "remove_curse", "remove_disease",
            "tongues" },
        [4] = new List<string> { "cure_critical_wounds", "minor_creation", "polymorph",
            "restoration", "stoneskin", "wall_of_fire" },
        [5] = new List<string> { "baleful_polymorph", "break_enchantment", "commune",
            "heal", "major_creation", "raise_dead", "true_seeing", "wall_of_stone" }
    };
}
```

### 7.5 NPC Class Comparison Summary

| Property | Adept | Aristocrat | Commoner | Expert | Warrior |
|:---------|:------|:-----------|:---------|:-------|:--------|
| `HitDie` | 6 | 8 | 4 | 6 | 8 |
| `BABAtLevel3` | 1 (Poor) | 2 (Medium) | 1 (Poor) | 2 (Medium) | 3 (Good) |
| `SkillPointsPerLevel` | 2 | 4 | 2 | 6 | 2 |
| `GoodFortitude` | ✓ | ✗ | ✗ | ✗ | ✓ |
| `GoodReflex` | ✗ | ✗ | ✗ | ✓ | ✗ |
| `GoodWill` | ✓ | ✓ | ✗ | ✗ | ✗ |
| `IsSpellcaster` | ✓ | ✗ | ✗ | ✗ | ✗ |
| `DefaultArmorBonus` | 0 | 5 | 0 | 2 | 5 |
| `DefaultDamageDice` | 6 | 8 | 4 | 6 | 8 |

---

*End of NPC Class Definitions*
