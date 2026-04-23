# D&D 3.5e AI Framework

## Overview
This folder contains a data-driven AI profile system for NPC behavior.

## Core Types
- `AIProfile` (`ScriptableObject`): base profile class.
- `AIBehaviorData`: shared enums/data (`CombatStyle`, `GrappleBehavior`, movement + maneuver preferences, tag priorities).

## Default Profiles
- **Berserk**: melee aggression, willing to eat AoOs, prioritizes wounded targets.
- **Ranged**: keep distance, avoid AoOs, prioritize unarmored/light targets.
- **Grappler**: initiate/maintain grapples, control-focused maneuvering.
- **Animal**: pack-hunter melee profile (nearest-target pressure, AoO-aware pathing, flanking opportunity bias).
- **Spellcaster (base)**: school-priority spell scoring + ally-aware AoE safety evaluation.
- **Evoker**: aggressive AoE damage (Evocation-first).
- **Abjurer**: defensive, single-target-leaning caster with strict friendly-fire limits.
- **Necromancer**: debuff/attrition profile prioritizing Necromancy.

## Custom AI
Create a new class inheriting `AIProfile` and override:
- `ScoreTarget(target, self)`
- `ShouldEscapeGrapple(self)`
- `ShouldInitiateGrapple(self, target)`
- `GetPreferredManeuver(self, target)`

See `Custom/CustomAIExample.cs` for a template.

## Assigning Profiles
Assign `CharacterController.aiProfile` on any NPC. During NPC turns, `AIService` reads this profile and adjusts:
- target scoring
- movement scoring preferences (preferred range, AoO-avoidance weight, flanking bias)
- maneuver selection hints (trip/disarm/grapple)

## Dual-Layer Runtime AI (Important)
This project currently uses **both** systems together at runtime:

1. **`EnemyAIProfileArchetype` / `AIProfile`** (decision brain)
   - Built in `GameManager.BuildRuntimeAIProfile(def)` and assigned to `npc.aiProfile`.
   - Drives target scoring, maneuver preference, grapple intent, and spell scoring.

2. **`EnemyAIBehavior`** (tactical shell)
   - Stored per NPC in `GameManager._npcAIBehaviors`.
   - Passed into `AIService.ExecuteNPCTurn(npc, behavior)`.
   - Selects which top-level tactical coroutine runs (`AggressiveMelee`, `DefensiveMelee`, `RangedKiter`).

In `AIService`, the intent is explicit: profile drives detailed decisions, while `EnemyAIBehavior` still selects the tactical shell.

### Practical interpretation
- **AIProfileArchetype** answers: *"Who should I target, which maneuver/spell should I prefer, and why?"*
- **AIBehavior** answers: *"Should this turn execute aggressive melee, defensive melee, or ranged-kiting movement/attack flow?"*

### Is `AIBehavior` redundant?
Not at runtime today.
- If a profile exists, behavior is still used to choose the shell (except some profile-driven ranged cases).
- If a profile is missing, behavior is the direct fallback switch.

### What if you remove `AIBehavior = ...` from one enemy entry?
For entries like wolf that omit the assignment, `EnemyDefinition.AIBehavior` defaults to `AggressiveMelee`, so behavior is effectively unchanged **today**.

However, removing the field assignment broadly would:
- collapse many enemies to default aggressive behavior,
- lose per-enemy tactical shell selection (defensive/ranged),
- reduce combat variety and could regress intended encounter behavior.

## Tag Priority Examples
- `Race: Elf`
- `Armor: Light Armor`
- `Armor: Unarmored`
- `Wielding: Unarmed`
- `HP State: Staggered`


## Maneuver Target Validation
`AIProfile` now validates maneuver targets before choosing special attacks.

### Built-in checks
- **Trip**: skipped if target is already `Prone`.
- **Disarm**: skipped if target has no disarmable held weapon.
- **Sunder**: skipped if target has no sunderable equipped item.

### Extensible hooks
Override these in custom profiles for extra rules:
- `IsValidTripTarget(target)`
- `IsValidDisarmTarget(target)`
- `IsValidSunderTarget(target)`
- `IsValidBullRushTarget(target, self)`
- `IsValidOverrunTarget(target, self)`

Example usage is included in `Custom/CustomAIExample.cs`.


## Healer Profile
- **Specialty**: Support spellcasting and party sustain.
- **Priority order**:
  1. `CriticalHealing` (ally below critical threshold)
  2. `Healing` (ally below healing threshold)
  3. `Buffing` (all allies healthy)
  4. `OffensiveSpell` (damage/debuff support)
  5. `PhysicalAttack` (last resort)

### Adaptive melee vs ranged (physical fallback)
Healer physical mode is chosen dynamically from stats:
- compares **Armor Class** against `MinimumMeleeArmorClass`
- compares **melee attack bonus** vs **ranged attack bonus**
- can force ranged when AC is low (`ForceRangedIfLowAC`)

### Default thresholds
- `HealingThreshold`: **70% HP**
- `CriticalHealingThreshold`: **25% HP**
- `HealthyThreshold`: **75% HP**
- `MinimumMeleeArmorClass`: **16**
- `AttackBonusPreference`: **+2**

### Tuning examples
- **Dedicated healer**: raise `HealingThreshold` and `CriticalHealingThreshold`, keep `ForceRangedIfLowAC = true`
- **War cleric**: lower `HealingThreshold`, lower `MinimumMeleeArmorClass`, smaller `AttackBonusPreference`
