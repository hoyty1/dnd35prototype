# Phase 4 — Extracted Services Reference

> **Project:** D&D 3.5e Prototype (Unity)
> **Extraction period:** Phase 4A–4J
> **Total refactored call sites:** 400+

---

## Overview

Phase 4 extracted nine focused services from the monolithic `GameManager` partial
class.  Each service is a **static utility** (or lightweight `MonoBehaviour` in the
case of `DispelMagicService`) that owns a single responsibility.  All live in the
global namespace — no `using` imports are needed between project files.

| # | Service | Location | Type | Sites |
|---|---------|----------|------|------:|
| 4A | SpellUtilities | `Magic/SpellUtilities.cs` | static | 32 |
| 4B | SpellCastingHelper | `Magic/SpellCastingHelper.cs` | static | 125 |
| 4C | TeamUtility | `Combat/TeamUtility.cs` | static | 88 |
| 4D | DispelMagicService | `Services/DispelMagicService.cs` | MonoBehaviour | 30 |
| 4E | ConcentrationService | `Services/ConcentrationService.cs` | static | 28 |
| 4F–H | CombatLogHelper | `Combat/CombatLogHelper.cs` | static | 51+ |
| 4I | SpellTargetingService | `Services/SpellTargetingService.cs` | static | 40+ |
| 4J | CombatCalculationService | `Combat/CombatCalculationService.cs` | static | 30+ |

---

## 1. SpellUtilities  (`Assets/Scripts/Spell/Casting/SpellUtilities.cs`)

Centralises spell save-DC computation and caster-ability-modifier lookups.

### Key Methods
| Method | Description |
|--------|-------------|
| `GetSpellSaveDC(caster, spell)` | Full DC calc with feats/focus |
| `GetSpellSaveDC(spellLevel, abilityMod)` | Raw `10 + level + mod` |
| `GetCastingAbilityModifier(stats)` | INT for Wizards, WIS for Clerics, CHA for Sorcerers/Bards |
| `IsImmuneToMindAffecting(target)` | Checks undead, vermin, construct, ooze |
| `IsImmuneToSleepEffects(target)` | Elf racial + type immunities |
| `IsLivingCreatureForFear(target)` | Excludes undead/constructs |
| `IsFearSpell(spell)` | Checks Fear descriptor |

---

## 2. SpellCastingHelper  (`Assets/Scripts/Spell/Casting/SpellCastingHelper.cs`)

Provides caster-level calculations, duration formulas, damage-dice scaling,
and the `SpellCastContext` struct for one-stop context building.

### Key Methods
| Method | Description |
|--------|-------------|
| `BuildContext(caster, spell)` | Returns `SpellCastContext` with CL, DC, name, duration |
| `GetEffectiveCasterLevel(caster, spell)` | CL with feats/items/specialisation |
| `GetBaseCasterLevel(stats)` | Raw class-level lookup |
| `CalculateDuration(spell, CL)` | Rounds from `DurationPerLevel × CL` |
| `GetDamageDiceCount(CL, maxDice)` | `min(CL, maxDice)` |
| `RollSpellDamage(CL, max, dieSize, label)` | Rolls Nd(die) with DiceService |
| `IsBlockedBySpellResistance(target, CL)` | Full SR penetration check |
| `PenetratesSpellResistance(caster, target)` | Convenience wrapper |

### SpellCastContext Struct
```csharp
public struct SpellCastContext {
    int CasterLevel, SaveDC;
    string CasterName;
    int DurationRounds => CalculateDuration(...);
    int DamageDice(int maxDice) => GetDamageDiceCount(...);
}
```

---

## 3. TeamUtility  (`Assets/Scripts/Combat/Core/TeamUtility.cs`)

Replaces `GameManager.IsEnemyTeam` / `IsAllyTeam` with pure-static queries.

### Key Methods
| Method | Description |
|--------|-------------|
| `IsEnemy(source, target)` | Different team + both alive |
| `IsAlly(source, target)` | Same team |
| `IsHumanoid(target)` | Type == Humanoid |
| `GetHitDice(target)` | Character level / monster HD |
| `GetAliveTeamMembers(teamId, allChars)` | Filters alive by team |
| `GetClosestEnemy(source, allChars)` | Nearest hostile by distance |

---

## 4. DispelMagicService  (`Assets/Scripts/Services/DispelMagicService.cs`)

Full dispel/counterspell engine — the only non-static service (needs scene
callbacks for UI, character lists, and effect cleanup).

### Key Methods
| Method | Description |
|--------|-------------|
| `Initialize(...)` | Injects `Func<CombatUI>`, character providers, cleanup hooks |
| `PerformDispelCheck(CL, targetCL, isOwn)` | `d20 + CL ≥ 11 + targetCL` |
| `RollDispelCheck(CL)` | Raw `d20 + min(CL, cap)` |
| `GetDispelDC(targetCL)` | `11 + targetCL` |
| `PerformTargetedDispel(caster, target)` | Strips highest-CL buff |
| `PerformAreaDispel(caster, targets)` | AoE: one check per creature |
| `ExpireReadiedCounterspell(char)` | Cleanup on turn end |

---

## 5. ConcentrationService  (`Assets/Scripts/Services/ConcentrationService.cs`)

All concentration-check DC formulas and success-chance math.

### Constants
```
DEFENSIVE_CASTING_DC_BASE  = 15   DAMAGE_DC_BASE = 10
GRAPPLED_DC_BASE           = 20   VIGOROUS_MOTION_DC_BASE = 10
VIOLENT_MOTION_DC_BASE     = 15   ENTANGLED_DC_BASE = 15
```

### Key Methods
| Method | Description |
|--------|-------------|
| `GetDefensiveCastingDC(spellLvl)` | `15 + spellLevel` |
| `GetDamageDC(dmg, spellLvl)` | `10 + damage + spellLevel` |
| `GetGrappledCastingDC(spellLvl)` | `20 + spellLevel` |
| `CalculateSuccessChancePercent(bonus, dc)` | `5 × (21 - (dc - bonus))` clamped |
| `GetConcentrationBonus(caster)` | Skill rank + CON + Combat Casting |
| `IsConcentratingOnSpell(caster, id)` | Checks active concentration effect |

---

## 6. CombatLogHelper  (`Assets/Scripts/Combat/Logging/CombatLogHelper.cs`)

25 named colour constants + semantic formatting helpers.  Returns Unity
rich-text strings — callers still push via `CombatUI?.ShowCombatLog(...)`.

### Colour Constants (selected)
| Constant | Hex | Usage |
|----------|-----|-------|
| `ColorGold` | `#FFD700` | Notable / special |
| `ColorGray` | `#AAAAAA` | Neutral info |
| `ColorBrightRed` | `#FF8888` | Damage |
| `ColorGreen` | `#88FF88` | Success / heals |
| `ColorCyan` | `#88FFEE` | Spell cast / buff |
| `ColorRed` | `#FF6666` | Failures |
| `ColorDarkRed` | `#FF4444` | Critical failures |

### Key Methods
| Method | Returns |
|--------|---------|
| `Color(text, hex)` | `<color=#hex>text</color>` |
| `Damage(emoji, msg)` | Bright-red damage line |
| `Success(emoji, msg)` | Green success line |
| `Failure(emoji, msg)` | Red failure line |
| `Info(emoji, msg)` | Gray informational line |
| `SaveResult(target, success, type, roll, dc)` | Green/red save line |
| `ConditionFaded(emoji, name, effect)` | Gray "X's Y fades." |
| `SpellResisted(target, spell)` | Green resist line |

---

## 7. SpellTargetingService  (`Assets/Scripts/Services/SpellTargetingService.cs`)

Creature-type detection, alignment queries, HD filters, and composite
validators used by 20+ spell implementations.

### Key Methods
| Method | Description |
|--------|-------------|
| `IsValidAliveTarget(target)` | Not null, not dead |
| `PassesBasicTargetingChecks(caster, target, spell)` | Alive + range + LOS |
| `IsHumanoid / IsUndead / IsConstruct / IsAnimal / IsPlant` | Type checks |
| `IsOutsiderOrExtraplanar(target)` | Outsider type or extraplanar flag |
| `IsLivingCreature(target)` | Not undead/construct |
| `GetCreatureType(target)` | String type label |
| `IsWithinHDLimit(target, max)` | HD ≤ cap |
| `IsValidPersonSpellTarget(target)` | Humanoid + ≤ medium |
| `IsValidHumanoidMindAffectingTarget(target)` | Humanoid + not immune |
| `FilterByCreatureType(list, type)` | List filter |
| `IsGoodAligned / IsEvilAligned(target)` | Alignment axis check |

---

## 8. CombatCalculationService  (`Assets/Scripts/Combat/Utilities/CombatCalculationService.cs`)

Pure-math combat formulas — hit determination, AC modifiers, STR scaling,
critical-threat math, concealment, and opposed checks.

### Constants
```
FightingDefensivelyACBonus     =  2
FightingDefensivelyAttackPenalty = -4
PinnedACPenalty                = -4
ConcealmentMissChance          = 20
TotalConcealmentMissChance     = 50
BlindedAttackerMissChance      = 50
```

### Key Methods
| Method | Description |
|--------|-------------|
| `IsHit(nat, total, ac)` | Nat-1 auto-miss, nat-20 auto-hit, else total ≥ AC |
| `SimpleTouchAC(stats)` | `10 + DEX + size` |
| `ProneACModifier(ranged)` | +4 ranged / −4 melee |
| `TwoHandedStrDamage(str)` | `floor(str × 1.5)` |
| `OffHandStrDamage(str)` | `floor(str × 0.5)` |
| `ClampMinimumDamage(raw)` | `max(1, raw)` |
| `IsCriticalThreat(nat, min)` | `nat ≥ min` |
| `DoubledThreatMin(base)` | `21 - 2 × (21 - base)` |
| `CritBonusDice(dice, mult)` | `dice × (mult - 1)` |
| `ConcealmentMiss(roll, pct)` | `roll ≤ pct` |
| `OpposedCheckWins(atk, def)` | `atk ≥ def` |

---

## Testing

Test templates live in `Assets/Scripts/Tests/Services/`.  Run all via:

```csharp
Tests.Services.ServiceTestRunner.RunAll();
```

Each test class follows the project's existing pattern: a static `RunAll()`
method with `Assert(condition, name)` helpers, logging to `Debug.Log`.

---

## Migration Guide

To convert an existing `ShowCombatLog` call:

```csharp
// Before
CombatUI?.ShowCombatLog($"<color=#AAAAAA>🛡 {name}'s Shield fades.</color>");

// After
CombatUI?.ShowCombatLog(CombatLogHelper.ConditionFaded("🛡", name, "Shield"));
```

To use combat math:
```csharp
// Before
if (roll == 20 || (roll != 1 && total >= targetAC)) { /* hit */ }

// After
if (CombatCalculationService.IsHit(roll, total, targetAC)) { /* hit */ }
```
