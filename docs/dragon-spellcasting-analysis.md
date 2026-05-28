# Dragon Spellcasting System — Current Implementation Analysis

**Date:** May 28, 2026  
**Scope:** D&D 3.5e Unity Prototype (`/home/ubuntu/dnd35prototype`, master branch)

---

## 1. Executive Summary

Dragon spellcasting is **partially implemented but entirely non-functional**. The data layer (DragonData.cs, NPCDatabase_Dragons.cs) has the scaffolding for sorcerer spell assignment, but two critical blockers prevent any dragon from ever casting a spell:

1. **`IsSpellcaster` always returns `false`** — Dragons use `CharacterClass = "Warrior"`, and `WarriorClass.IsSpellcaster` is `false`. The `SorcererCasterLevel` from DragonAgeStats is never propagated to the character's class system.

2. **`SpellcastingComponent` is never initialized** — The NPC setup code (GameManager.NPCSetup.cs:679) gates spellcasting initialization on `stats.IsSpellcaster`, which is always false for dragons.

Even if these blockers were fixed, the spell lists are **severely limited** — only 4 of 10 dragon types have any spells defined at all, and those that do only know `mage_armor` and `shield` (two 1st-level defensive buffs) regardless of age or caster level.

---

## 2. Architecture — How Dragon Spells Are Currently Determined

### 2.1 Data Layer: DragonData.cs

Each `DragonTypeTemplate` has a single **flat spell list** shared across all age categories:

```csharp
// DragonTypeTemplate (line 139)
public List<string> SorcererSpellIds;  // Same for ALL ages of this dragon type
```

Each `DragonAgeStats` entry has a **per-age caster level**:

```csharp
// DragonAgeStats (line 101)
public int SorcererCasterLevel;  // 0 = no casting at this age
```

### 2.2 Assignment Layer: NPCDatabase_Dragons.cs

When a dragon is spawned (lines 98–108):

```csharp
List<string> knownSpells = new List<string>();
List<string> preparedSlots = new List<string>();
if (stats.SorcererCasterLevel > 0 && template.SorcererSpellIds != null && template.SorcererSpellIds.Count > 0)
{
    knownSpells.AddRange(template.SorcererSpellIds);
    // "Simplified" — gives slots equal to min(casterLevel, spellCount)
    for (int s = 0; s < Mathf.Min(stats.SorcererCasterLevel, template.SorcererSpellIds.Count); s++)
        preparedSlots.Add(template.SorcererSpellIds[s]);
}
```

These are stored in the NPCDefinition:
```csharp
KnownSpellIds = knownSpells,        // line 169
PreparedSpellSlotIds = preparedSlots, // line 170
```

### 2.3 Initialization Blocker: GameManager.NPCSetup.cs

```csharp
// Line 679 — THIS IS THE BLOCKER
bool shouldInitSpellcasting = stats.IsSpellcaster  // ← Always FALSE for dragons!
    && ((def.KnownSpellIds != null && def.KnownSpellIds.Count > 0)
        || (def.PreparedSpellSlotIds != null && def.PreparedSpellSlotIds.Count > 0));

if (shouldInitSpellcasting)
{
    SpellcastingComponent spellComp = npc.Spellcasting
        ?? npc.gameObject.AddComponent<SpellcastingComponent>();
    // ... init spells ...
}
```

Because `CharacterClass = "Warrior"` and `WarriorClass.IsSpellcaster => false`, the `SpellcastingComponent` is **never created**.

### 2.4 AI Layer: AIService.ExecuteDragonTurn

```csharp
// Line 648–656 — Would try casting, but IsSpellcaster is always false
if (!dragonProfile.IsTooCloseForCasting(npc, allCombatants, _gameManager))
{
    if (npc.Stats.IsSpellcaster && TryExecuteSpellcastAction(npc, target))  // ← never true
    {
        Debug.Log($"[AI][Dragon] {npc.Stats.CharacterName} cast a spell (priority 1/2).");
        yield return new WaitForSeconds(0.8f);
        yield break;
    }
}
```

And in `TryExecuteSpellcastAction` (line 2348):
```csharp
if (!caster.Stats.IsSpellcaster)
    return false;  // ← always exits here for dragons
```

---

## 3. Current Data: Spells and Caster Levels by Dragon Type

### 3.1 Dragon Type Spell Definitions

| Dragon  | SorcererSpellIds              | Has Spells? |
|---------|-------------------------------|-------------|
| Red     | `mage_armor`, `shield`        | ✅ (2)       |
| Blue    | `mage_armor`, `shield`        | ✅ (2)       |
| Green   | *(empty list)*                | ❌           |
| Black   | *(empty list)*                | ❌           |
| White   | *(empty list)*                | ❌           |
| Gold    | `mage_armor`, `shield`        | ✅ (2)       |
| Silver  | `mage_armor`, `shield`        | ✅ (2)       |
| Bronze  | *(empty list)*                | ❌           |
| Copper  | *(empty list)*                | ❌           |
| Brass   | *(empty list)*                | ❌           |

### 3.2 Sorcerer Caster Levels by Age Category

| Dragon  | Wyrmling | Very Young | Young | Juvenile | Young Adult | Adult |
|---------|----------|------------|-------|----------|-------------|-------|
| Red     | 0        | 0          | 1     | 3        | 5           | 7     |
| Blue    | 0        | 0          | 1     | 3        | 5           | 7     |
| Green   | —        | —          | —     | —        | —           | —     |
| Black   | —        | —          | —     | —        | —           | —     |
| White   | —        | —          | —     | —        | —           | —     |
| Gold    | 0        | 1          | 3     | 5        | 7           | 9     |
| Silver  | 0        | 0          | 1     | 3        | 5           | 7     |
| Bronze  | 0        | 0          | 0     | 1        | 3           | 5     |
| Copper  | —        | —          | —     | —        | —           | —     |
| Brass   | 0        | 0          | —     | —        | —           | —     |

*(— = SorcererCasterLevel field not defined in the age stats, defaults to 0)*

### 3.3 Spell-Like Abilities

All 10 dragon types have `SpellLikeAbilityIds = new List<string>()` — **completely empty**.

---

## 4. D&D 3.5e Monster Manual Compliance

### 4.1 What the Rules Say (MM p.67-77)

Per D&D 3.5e, **all true dragons cast spells as sorcerers** starting from a specific age:

- **Chromatic dragons** gain sorcerer casting at Young (age 3) or later
- **Metallic dragons** generally gain it earlier (Very Young or Young)
- The caster level equals the dragon's listed caster level for that age
- Spells known/per day follow the Sorcerer table (PHB p.54) for that caster level
- **Spell selection is DM's choice**, but the MM gives suggested spell lists

### 4.2 Caster Level Discrepancies (per MM)

| Dragon  | MM Start Age | Impl Start Age | MM Adult CL | Impl Adult CL | Notes |
|---------|-------------|----------------|-------------|---------------|-------|
| Red     | Young (3)   | Young (3) ✅    | 7th         | 7 ✅           | OK    |
| Blue    | Young (3)   | Young (3) ✅    | 7th         | 7 ✅           | OK    |
| Green   | Young (3)   | Never ❌        | 5th         | 0 ❌           | **Missing entirely** |
| Black   | Young (3)   | Never ❌        | 3rd         | 0 ❌           | **Missing entirely** |
| White   | Juvenile (4)| Never ❌        | 1st         | 0 ❌           | **Missing entirely** |
| Gold    | Very Young (2)| Very Young (2) ✅| 11th       | 9 ⚠️          | CL too low for adult |
| Silver  | Young (3)   | Young (3) ✅    | 7th         | 7 ✅           | OK    |
| Bronze  | Juvenile (4)| Juvenile (4) ✅ | 5th         | 5 ✅           | OK    |
| Copper  | Young (3)   | Never ❌        | 5th         | 0 ❌           | **Missing entirely** |
| Brass   | Juvenile (4)| Never ❌        | 3rd         | 0 ❌           | **Missing entirely** |

**Summary:** 5 of 10 dragon types are completely missing sorcerer casting. 1 has a slightly incorrect adult CL.

### 4.3 Missing Age Categories

The implementation only covers **6 of 12** D&D 3.5e age categories:
- ✅ Implemented: Wyrmling, Very Young, Young, Juvenile, Young Adult, Adult
- ❌ Missing: Mature Adult, Old, Very Old, Ancient, Wyrm, Great Wyrm

Higher ages have higher caster levels and access to more powerful spells.

### 4.4 Spell Selection Problems

Even for dragons that have `SorcererSpellIds` defined, the selection is woefully inadequate:

| Issue | Description |
|-------|-------------|
| **Only 2 spells** | All casting dragons know only `mage_armor` and `shield` |
| **No offensive spells** | No damage/control spells (fireball, lightning bolt, etc.) |
| **Same spells regardless of age** | A CL1 Young dragon and a CL7 Adult know identical spells |
| **No scaling by caster level** | CL7 sorcerer should know ~14 spells (0th-3rd level) |
| **Flat list, not per-age** | `SorcererSpellIds` is on the template, not per-age |
| **No spell-like abilities** | Many dragons gain SLAs at specific ages (e.g., Red: locate object at juvenile) |

### 4.5 What a CL7 Sorcerer Should Know (Adult Red/Blue/Silver Dragon)

Per the PHB Sorcerer table (p.54), a 7th-level sorcerer knows:
- **Level 0:** 7 cantrips known, 6/day
- **Level 1:** 4 spells known, 6/day
- **Level 2:** 2 spells known, 5/day
- **Level 3:** 1 spell known, 3/day

Current implementation: 2 spells total (both level 1). That's **~14 spells short**.

---

## 5. Spell Database Availability

The SpellDatabase has **128 spells accessible to Sorcerers**, sufficient for proper dragon spell selection:

| Spell Level | Count | Example Spells Available |
|-------------|-------|------------------------|
| 0 (cantrip) | 3     | Dancing Lights, Daze, Touch of Fatigue |
| 1           | 18    | Mage Armor, Shield, Magic Missile, Grease, Sleep, Cause Fear, etc. |
| 2           | 34    | Mirror Image, Invisibility, Web, Resist Energy, Blur, etc. |
| 3           | 31    | Fireball, Lightning Bolt, Haste, Dispel Magic, Displacement, etc. |
| 4           | 22    | Wall of Fire, Greater Invisibility, Stoneskin, Dimension Door, etc. |
| 5           | 9     | Cone of Cold, Wall of Force, Hold Monster, Telekinesis, etc. |
| 6           | 6     | Chain Lightning, Disintegrate, Globe of Invulnerability, etc. |
| 7           | 2     | Spell Turning, Plane Shift |
| 8           | 2     | Protection from Spells, Sunburst |
| 9           | 1     | Wish |

---

## 6. What Needs to Be Fixed (Priority Order)

### 6.1 Critical — Make Dragon Spellcasting Actually Work

**Fix 1: `IsSpellcaster` bypass for dragons**
- Either add "Sorcerer" to the dragon's class levels when `SorcererCasterLevel > 0`
- Or modify `GameManager.NPCSetup.cs` line 679 to also check if the NPCDefinition has a non-zero `SorcererCasterLevel` or non-empty `KnownSpellIds`
- The cleanest approach: Add a `SorcererLevel` field to `NPCDefinition`, and in NPCSetup, add a Sorcerer class level if it's > 0

**Fix 2: Propagate caster level to CharacterStats**
- Currently `SorcererCasterLevel` is never stored on the instantiated character
- The `SpellcastingComponent.Init()` needs a caster level to determine slots/day

### 6.2 High — Add Missing Dragon Caster Levels

Add `SorcererCasterLevel` to `DragonAgeStats` for the 5 missing dragon types:
- **Green:** Young=1, Juvenile=3, YoungAdult=5, Adult=5 (per MM p.75)
- **Black:** Young=1, Juvenile=1, YoungAdult=3, Adult=3 (per MM p.70)  
- **White:** Juvenile=1, YoungAdult=1, Adult=1 (per MM p.78)
- **Copper:** Young=1, Juvenile=3, YoungAdult=5, Adult=5 (per MM p.83)
- **Brass:** Juvenile=1, YoungAdult=3, Adult=3 (per MM p.80)

### 6.3 High — Expand Spell Lists Per Age

Replace the flat `SorcererSpellIds` with per-age spell selection. Design options:

**Option A: Per-age spell lists in DragonAgeStats**
```csharp
// Add to DragonAgeStats
public List<string> SorcererSpellsKnown; // Spells known at this age
```

**Option B: Algorithmic selection based on CL**
```csharp
// Auto-generate spell lists from the sorcerer spells/known table
// using curated pools per dragon type (themed to their element)
```

**Recommended thematic spell selections per dragon type:**

| Dragon | Theme | Suggested Spells |
|--------|-------|-----------------|
| Red    | Fire, destruction | `burning_hands`, `fireball`, `wall_of_fire`, `fire_shield`, `haste` |
| Blue   | Lightning, illusion | `shocking_grasp`, `lightning_bolt`, `mirror_image`, `blur`, `invisibility` |
| Green  | Poison, enchantment | `charm_person`, `sleep`, `suggestion`, `stinking_cloud`, `dominate_person` |
| Black  | Acid, darkness | `darkness`, `cause_fear`, `ray_of_enfeeblement`, `web`, `fear` |
| White  | Cold (minimal casting) | `grease`, `shield` (White dragons are the weakest casters) |
| Gold   | Fire, protection | `fireball`, `protection_from_energy`, `stoneskin`, `haste`, `dispel_magic` |
| Silver | Cold, healing | `shield`, `mirror_image`, `protection_from_energy`, `slow`, `haste` |
| Bronze | Lightning, water | `lightning_bolt`, `fog_cloud`, `resist_energy`, `displacement` |
| Copper | Acid, trickery | `grease`, `web`, `slow`, `confusion`, `stoneskin` |
| Brass  | Fire, talk | `sleep`, `charm_person`, `hold_person`, `suggestion` |

### 6.4 Medium — Add Spell-Like Abilities

Many dragons gain specific spell-like abilities at certain ages. These are separate from their sorcerer spellcasting:

| Dragon | SLA | Age Gained |
|--------|-----|-----------|
| Red    | Locate Object | Juvenile |
| Blue   | Create/Destroy Water (at will) | Wyrmling |
| Green  | Suggestion | Juvenile |
| Black  | Darkness (at will, 3×/day) | Juvenile |
| White  | Fog Cloud | Young |
| Gold   | Bless | Very Young |
| Silver | Fog Cloud | Very Young |
| Bronze | Speak with Animals (at will) | Wyrmling |
| Copper | Spider Climb (at will) | Wyrmling |
| Brass  | Speak with Animals (at will) | Wyrmling |

### 6.5 Low — Sorcerer Slot System

The current "simplified" slot assignment (`min(casterLevel, spellCount)`) doesn't match the sorcerer spells-per-day table. A proper implementation would need:
- Spells known by level (from PHB sorcerer table)
- Spells per day by level (from PHB sorcerer table)
- Spontaneous casting (can cast any known spell using available slots)

---

## 7. Quick Fix vs. Full Fix

### Quick Fix (minimal changes to make existing spells work)
1. In `GameManager.NPCSetup.cs` line 679, change condition to:
   ```csharp
   bool shouldInitSpellcasting = (stats.IsSpellcaster || (def.KnownSpellIds != null && def.KnownSpellIds.Count > 0))
       && ((def.KnownSpellIds != null && def.KnownSpellIds.Count > 0)
           || (def.PreparedSpellSlotIds != null && def.PreparedSpellSlotIds.Count > 0));
   ```
   This lets creatures with explicit spell lists (like dragons) bypass the class check.

2. Add `SorcererCasterLevel` to `NPCDefinition` and pass it through to CharacterStats so the spellcasting system knows the caster level.

### Full Fix (D&D 3.5e compliant)
1. All items from Quick Fix
2. Add caster levels for all 10 dragon types (section 6.2)
3. Create proper per-age spell lists (section 6.3)
4. Add spell-like abilities (section 6.4)
5. Implement sorcerer slot system (section 6.5)
6. Consider adding the missing 6 age categories (Mature Adult through Great Wyrm)

---

## 8. Files Involved

| File | Role | Status |
|------|------|--------|
| `Assets/Scripts/Character/Creatures/DragonData.cs` | Dragon type templates & age stats | Has CL data for 5/10 dragons, flat spell lists |
| `Assets/Scripts/Character/Creatures/NPCDatabase_Dragons.cs` | Builds NPCDefinition from template | Has spell assignment code, but never executes effectively |
| `Assets/Scripts/_Core/GameManager.NPCSetup.cs` | Initializes SpellcastingComponent | **BLOCKER** — gates on `IsSpellcaster` which is false for dragons |
| `Assets/Scripts/Character/Stats/CharacterStats.cs` | `IsSpellcaster` property | Checks class system, not NPC-defined caster levels |
| `Assets/Scripts/Character/Classes/NPC/WarriorClass.cs` | Warrior class definition | `IsSpellcaster => false` |
| `Assets/Scripts/Services/AIService.cs` | Dragon AI turn execution | Has spellcasting priority, but blocked by `IsSpellcaster` |
| `Assets/Scripts/AI/Profiles/DragonAIProfile.cs` | Dragon AI profile | Properly prioritizes spells before breath weapon |
| `Assets/Scripts/Spell/Database/SpellDatabase_*.cs` | Spell definitions | 128 Sorcerer-accessible spells available |
| `Assets/Scripts/Spell/Components/SpellcastingComponent.cs` | Spell management | Would work if initialized |
