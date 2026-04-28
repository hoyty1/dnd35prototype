# Status Effects Implementation Workflow (Current Code)

Project: `/home/ubuntu/dnd35prototype`

This document reflects the **actual current implementation** in code (not the older tag-boolean model).

## 1) System architecture (actual)

### Core layers

1. **Poison/Disease data definitions**
   - `Assets/Scripts/Effects/PoisonDatabase.cs`
   - `Assets/Scripts/Effects/DiseaseDatabase.cs`
   - Definitions include ability damage plus `PoisonSpecialEffect` payloads.

2. **Special effect mapping + duration parsing**
   - `Assets/Scripts/Effects/PoisonSpecialEffect.cs`
   - `PoisonEffectType` → `CombatConditionType` via `ToConditionType()`.
   - Durations like `"2d6 minutes"` are converted to rounds by `RollDurationInRounds()`.

3. **Condition model and rules metadata**
   - `Assets/Scripts/Combat/StatusEffect.cs`
   - `CombatConditionType` enum (includes `Paralyzed`, `Nauseated`, `Sickened`, `Helpless`, etc.)
   - `ConditionRules` provides display names, stacking policy, numeric modifiers, and behavior flags.

4. **Character condition application/ticking**
   - `Assets/Scripts/Character/CharacterController.cs`
   - `ApplyPoison()`, `ProcessPoisonSecondaryDamage()`, `ProcessDiseaseEffectsDaily()`
   - `ApplySpecialEffectList()` / `ApplySpecialEffect()` convert poison effects into combat conditions.

5. **Condition storage + lifecycle**
   - Local character condition manager: `Assets/Scripts/Combat/ConditionManager.cs`
   - Runtime orchestration service: `Assets/Scripts/Services/ConditionService.cs`
   - `GameManager` wires service turn/round events and logs expiration.

6. **Status tag presentation layer**
   - `Assets/Scripts/Character/StatusTagManager.cs`
   - This is now mainly a **tag synchronization/UI layer** (`Status: Paralyzed`, etc.), not old-style boolean condition state.

---

## 2) Paralysis workflow (complete, real path)

## Example source: Carrion Crawler Brain Juice

From `PoisonDatabase.cs`:

```csharp
_poisons["carrion_crawler_brain_juice"] = new PoisonData
{
    FortitudeDC = 13,
    InitialSpecialEffects = new List<PoisonSpecialEffect>
    {
        new PoisonSpecialEffect
        {
            EffectType = PoisonEffectType.Paralysis,
            DurationFormula = "2d6 minutes",
            AppliesToInitial = true,
            AppliesToSecondary = false
        }
    }
};
```

### Step A — Poison is applied

`CharacterController.ApplyPoison(string poisonId, int dcModifier = 0)`:

```csharp
PoisonData poison = PoisonDatabase.GetPoison(poisonId);
int dc = Mathf.Max(0, poison.FortitudeDC + dcModifier);
int roll = Random.Range(1, 21);
int total = roll + Stats.FortitudeSave;
```

If initial save fails:

```csharp
ApplyAbilityEffectList(poison.InitialDamage, $"{poison.Name} (initial)");
ApplySpecialEffectList(poison.InitialSpecialEffects, poison.Name, applyForInitial: true, applyForSecondary: false);
```

### Step B — Special effect list filtering

`ApplySpecialEffectList(...)` checks each effect’s `AppliesToInitial`/`AppliesToSecondary` flags and calls:

```csharp
ApplySpecialEffect(effect, source);
```

### Step C — Special effect becomes combat condition

`ApplySpecialEffect(PoisonSpecialEffect effect, string source)`:

```csharp
int rounds = effect.RollDurationInRounds();
CombatConditionType conditionType = effect.ToConditionType();
ApplyCondition(conditionType, rounds, sourceName);

if (conditionType == CombatConditionType.Paralyzed || conditionType == CombatConditionType.Unconscious)
    ApplyCondition(CombatConditionType.Helpless, rounds, sourceName);
```

Important points:
- Paralysis duration is rolled from dice formula and converted to rounds.
- Paralysis auto-applies **Helpless** for the same duration.

### Step D — Condition storage and normalization

`ApplyCondition()` routes through `CharacterConditions` to `ConditionService` (if available), else direct manager.

Ultimately condition instances are stored as `StatusEffect` entries with:
- `Type` (normalized `CombatConditionType`)
- `SourceName`
- `RemainingRounds`

### Step E — Condition effects in engine

Condition definitions in `StatusEffect.cs`:

```csharp
Type = CombatConditionType.Paralyzed,
PreventsMovement = true,
PreventsStandardActions = true,
PreventsFullRoundActions = true,
PreventsSpellcasting = true,
PreventsAoO = true,
PreventsThreatening = true,
MovementMultiplier = 0f
```

And `Helpless` has additional AC penalty metadata (`ArmorClassModifier = -4`) and action/movement denial flags.

### Step F — Round/turn expiration

- `GameManager.OnNewRound()` calls `_conditionService?.OnRoundEnd()`.
- `ConditionService.OnRoundEnd()` calls `UpdateConditionTimers(character)` for each living tracked character.
- `TickConditionsDirect()` decrements duration and removes expired entries.
- On expiration, `ConditionService.OnConditionExpired` is raised.
- `GameManager.HandleConditionExpired(...)` logs “is no longer <condition>”.

---

## 3) Nausea workflow (complete real path + current data status)

## Current repo status

- Mapping exists and is functional:

```csharp
case PoisonEffectType.Nausea:
    return CombatConditionType.Nauseated;
```

- `CombatConditionType.Nauseated` is fully defined in `ConditionRules`.
- **No built-in poison entry currently uses `PoisonEffectType.Nausea` in `PoisonDatabase.cs`.**

So the runtime path is implemented end-to-end, but current default poison catalog does not ship a nausea poison entry yet.

## Runtime path when nausea effect is present in a poison/disease

1. Poison/disease save fails (`ApplyPoison`, `ProcessPoisonSecondaryDamage`, or `ProcessDiseaseEffectsDaily`).
2. `ApplySpecialEffectList(...)` selects the effect by timing flags.
3. `ApplySpecialEffect(...)`:
   - rolls rounds via `RollDurationInRounds()`
   - maps `PoisonEffectType.Nausea -> CombatConditionType.Nauseated`
   - calls `ApplyCondition(CombatConditionType.Nauseated, rounds, sourceName)`
4. Condition lifecycle/expiration handled by same `ConditionService` + `ConditionManager` path as paralysis.

## Nauseated rules metadata currently configured

From `ConditionRules`:

```csharp
Type = CombatConditionType.Nauseated,
AttackModifier = -10,
PreventsStandardActions = true,
PreventsFullRoundActions = true,
PreventsSpellcasting = true,
MovementMultiplier = 1f
```

Note: `Sickened` is also defined separately (`-2` attack/saves), but poison `Nausea` currently maps to **Nauseated**, not Sickened.

---

## 4) How the status system works now

## A) Condition truth source

The canonical condition state is character `ActiveConditions` (`StatusEffect` entries), managed by `ConditionManager`/`ConditionService`.

## B) Tag layer

`StatusTagManager` mirrors active conditions into UI-friendly tags:

```csharp
AddTrackedTag(tags, _appliedStatusEffectTags, $"Status: {name}");
```

So tags are synchronized output, not primary mechanical state.

## C) Mechanical integration

Condition modifiers are aggregated from `ConditionRules` into stats:

- `ConditionAttackPenalty`
- `ConditionACPenalty`
- `ConditionFortitudeModifier`
- `ConditionReflexModifier`
- `ConditionWillModifier`
- `MovementBlockedByCondition`
- `ConditionMovementMultiplier`

Example formulas:

```csharp
public int AttackBonus => BaseAttackBonus + STRMod + SizeModifier + MoraleAttackBonus + ConditionAttackPenalty;
public int FortitudeSave => CONMod + ClassFortSave + FeatFortitudeBonus + MoraleSaveBonus + ConditionFortitudeModifier;
```

## D) Turn/round orchestration

- `GameManager` initializes and binds `ConditionService` to `TurnService`.
- Round-based condition ticking happens via `_conditionService.OnRoundEnd()`.
- Optional start/end-of-turn expiration is supported via condition metadata flags tracked in `ConditionService.ActiveCondition`.

---

## 5) Key implementation differences vs older model

1. There is **no `SetParalyzed` / `SetSickened` / `IsParalyzed` boolean API** in current `StatusTagManager`.
2. `ActiveSpecialEffect` tracking is no longer the main mechanism; special effects are converted into standard `StatusEffect` combat conditions.
3. Condition duration and expiration are centralized through `ConditionService` + condition ticking, with expiration events surfaced to `GameManager` logs.

---

## 6) Quick reference: main files involved

- `Assets/Scripts/Effects/PoisonDatabase.cs`
- `Assets/Scripts/Effects/PoisonSpecialEffect.cs`
- `Assets/Scripts/Character/CharacterController.cs`
- `Assets/Scripts/Combat/StatusEffect.cs`
- `Assets/Scripts/Combat/ConditionManager.cs`
- `Assets/Scripts/Services/ConditionService.cs`
- `Assets/Scripts/Character/StatusTagManager.cs`
- `Assets/Scripts/Core/GameManager.cs`
