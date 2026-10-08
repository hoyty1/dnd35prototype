> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-03.

# Enemy AI: where it stands today

This is the reference for planning and building deeper enemy AI. It describes how a non-player combatant takes its turn today, which parts of the 3.5e combat option space it can use, where the code deviates from the PHB, DMG and MM, and how to extend it. How the AI fits into the rest of the code is summarised in [architecture/ai-encounters-ui.md](../architecture/ai-encounters-ui.md); bugs live in [issues/AI.md](../issues/AI.md) and the other `issues/` files and are cited here by ID. Creature content (which monsters have which abilities) is in [CREATURES.md](CREATURES.md); spell content is in [SPELLS_AND_METAMAGIC.md](SPELLS_AND_METAMAGIC.md). The tactics inventory in [tactics/README.md](tactics/README.md) lists every action, spell and creature ability in the level 1-8 scope with its AI status and a tactical model, and proposes the action-catalog schema and backlog. For a concrete request ("make goblins flank", "make enemies drink potions"), start at [section 11.8](#118-request-playbooks), which maps likely requests to entry points, existing code, blockers and the issues to fix first.

Conventions:

- Paths are relative to `Assets/Scripts/` unless they start with `docs/`. Line numbers are from commit 0dd8e76 and drift; search for the method name.
- Everything here was established by reading code. **Nothing was verified in Play mode** unless a line says so. "Inferred" marks a behaviour worked out by tracing several methods.
- Defects are cited by their issue ID (`AI-nnn`, `CMB-nnn` and so on; see `docs/KNOWN_ISSUES.md`). The ones found while writing this doc are summarised in [section 10.2](#102-issues-found-while-writing-this-doc).
- Rules citations give book and chapter. Check the book before filing a rules deviation.

Contents:

1. [Summary](#1-summary)
2. [Architecture](#2-architecture)
3. [The NPC turn pipeline](#3-the-npc-turn-pipeline)
4. [Target selection and movement scoring](#4-target-selection-and-movement-scoring)
5. [Routines](#5-routines)
6. [Profiles and archetypes](#6-profiles-and-archetypes)
7. [Spellcasting AI](#7-spellcasting-ai)
8. [Special creatures and condition-driven behaviour](#8-special-creatures-and-condition-driven-behaviour)
9. [Action coverage matrix](#9-action-coverage-matrix)
10. [Known issues](#10-known-issues)
11. [Extension points](#11-extension-points), including the [request playbooks](#118-request-playbooks):
    - [11.8.1 Flanking and Aid Another](AI_PLAYBOOKS.md#1181-make-goblins-flank-and-use-aid-another)
    - [11.8.2 Fireball and Haste](AI_PLAYBOOKS.md#1182-make-enemy-wizards-cast-fireball-and-haste-sensibly)
    - [11.8.3 Potions](AI_PLAYBOOKS.md#1183-make-enemies-drink-potions-when-low)
    - [11.8.4 Trip and disarm](AI_PLAYBOOKS.md#1184-make-monsters-trip-and-disarm)
    - [11.8.5 Morale and fleeing](AI_PLAYBOOKS.md#1185-add-morale-and-fleeing)
    - [11.8.6 Cleric healing and channelling](AI_PLAYBOOKS.md#1186-make-npc-clerics-heal-and-channel)
    - [11.8.7 Class abilities](AI_PLAYBOOKS.md#1187-make-enemies-use-class-abilities-smite-evil-rage-turnrebuke-undead-bardic-music-flurry)
12. [Testing and debugging](#12-testing-and-debugging)
13. [Toward deeper AI](#13-toward-deeper-ai-analysis-for-the-owner-to-decide)

## 1. Summary

**What the AI does today.**

- Every non-controllable combatant (enemy NPCs, enemy-side summons, AI-controlled swarms) runs `AIService.ExecuteNPCTurn`, a coroutine that applies a fixed chain of gates and then hands the turn to one of six hand-written routines: AggressiveMelee, RangedKiter, DefensiveMelee, Dragon, Swarm, Healer, plus a separate summon routine in `GameManager`.
- It picks a target by scoring every visible enemy (profile score plus a large visibility term), moves with a single move action to the best-scoring reachable cell, then charges, maneuvers, or makes a single or full attack.
- It can charge (with pounce), trip, disarm, grapple (including a weighted-random in-grapple routine), coup de grace, take a 5-foot step in two narrow cases, withdraw when badly hurt (DefensiveMelee only), fire breath weapons with real cone/line positioning (Dragon profile only), emit auras, cast single-target spells (Healer, Dragon and RangedKiter routines only), track invisible enemies by last-known position and Listen checks, and follow forced behaviour for Confused, Charmed and Frightened.

**Biggest limitations.**

1. **Narrow action set.** No double move, run, total defense, ready, delay, Power Attack, Combat Expertise, two-weapon fighting, Aid Another, dropping prone or crawling, weapon switching, item use, turning, class abilities such as Rage, Flurry of Blows or smite (11.8.7), spontaneous cure/inflict conversion, area spells or metamagic (section 9). Several PC executors for these are bound to the PC turn flow and cannot be called by the AI as they are (AI-054).
2. **Asymmetries that favour NPCs.** Breath weapons and specials cost no action (AI-040, AI-053), monster ranged specials skip mitigation (AI-006) and silenced NPCs can cast (SPL-092). Casting (7.6), movement and maneuver attacks of opportunity (4.5) use the same helpers for PCs and NPCs.
3. **Asymmetries that cripple NPCs.** A prone confused NPC never stands up and the AI never crawls (CMB-074; other prone NPCs, charmed ones included, stand at turn start). Panicked, Turned, Pinned and Nauseated NPCs lose their whole turn before the AI runs (CMB-075). Breath weapons fire once per spawn (AI-032) and only for the Dragon profile (AI-033). Almost no monster can actually cast: the lich has no prepared spells (SPL-015), the vampire is not a spellcaster (CRE-030), class-levelled DMG spawns have no spell lists (ENC-021) and creature spell-like abilities do not exist (CRE-015, CRE-017).
4. **No decision layer.** Each routine is a fixed script. There is no comparison of the expected value of a spell, a breath, a full attack or a maneuver; `SelectBestAction` computes six results but its callers act only on `Charge` (AI-027). Profiles that set Trip trip every standing target in reach with their first attack, with no odds check (AI-035); a per-turn stopgap (AI-060) only stops them chaining maneuvers or retrying a failed one.
5. **No group play.** Every NPC scores alone. There is no focus fire, flank pairing, protection of casters, morale or retreat policy (AI-010).

**Mental model.** Think of an NPC's AI as three stacked layers. The **turn pipeline** (`ExecuteNPCTurn`) owns the order of operations and the condition takeovers. The **tactical routine** (chosen by `NPCAIBehavior`, the profile class, or the summon/swarm status) owns the shape of the turn: approach-then-hit, cast-then-kite, breathe-then-retreat. The **profile** (`AIProfile` subclass chosen by `NPCAIProfileArchetype`) only answers questions the routine asks: how good is this target, should I charge, which maneuver, which spell. Spell choice is a fourth, mostly independent scorer (`AISpellcastingStrategist`). Most resolution (attacks, breath, casting) is done by `GameManager` partials that `AIService` reaches through about 40 `*ForAI` wrappers. To change *what* an NPC can do you usually edit a routine and a `GameManager` executor; to change *how it chooses* you edit a profile or the strategist.

## 2. Architecture

### 2.1 Layers

| Layer | Selected by | Code | Decides |
|---|---|---|---|
| Turn pipeline | always | `AIService.ExecuteNPCTurn` (Services/AIService.cs:48) | gate order, condition takeovers, routing |
| Condition controllers | active condition | Combat/Behaviors/{Confused,Charmed,Fascinated,Frightened}BehaviorController.cs | the whole turn while the condition lasts |
| Tactical routine | `NPCAIBehavior`, profile class, `CombatStyle`, summon/swarm status | `AIService.Execute*Turn`; `GameManager.AI_SummonedCreature`; `GameManager.AI_GrappleRestrictedTurn` | turn shape: move, charge, cast, attack, retreat |
| Profile ("brain") | `NPCAIProfileArchetype` via `GameManager.BuildRuntimeAIProfile` | AI/AIProfile.cs, AI/Profiles/*.cs (19 concrete classes) | target score, charge, maneuver, coup de grace, AoO tolerance, preferred range, spell score |
| Spell strategist | always, when casting | AI/AISpellcastingStrategist.cs (static) | spell score terms, spell target, defensive-cast decision |
| Perception | always | `CharacterController.CanSee`/`GetMissChance`; AI/LastKnownPositionTracker.cs | what counts as visible; last-known squares; Listen pinpointing |
| Executors | called by routines | `GameManager` partials (`NPCPerformAttack`, `TryNPCPerformSpellCast`, `NPCExecuteCharge`, `NPCExecuteBreathWeaponForAI`, `TryNPCSpecialAttackIfBeneficial`, movement); action cost of attacks and attack-substitute maneuvers through the per-creature attack sequence (`CharacterController.TryCommitAttack`, `ManeuverActionCost`); the NPC melee attack steps through it (`PerformNPCMeleeAttackSequence`, per-step maneuver from `AIService.PerformMeleeAttackActionWithManeuvers`, CMB-102) | rules resolution |

### 2.2 Diagram

```
 TurnService.OnTurnStarted
          |
 GameManager.OnTurnStarted --(IsControllable)--> StartPCTurn (player)
          | not controllable
 GameManager.SingleNPCTurnFromInitiative
          |-- ShouldSkipTurnDueToHPState? --> log, NextInitiativeTurn   (CMB-075)
          v
 AIService.ExecuteNPCTurn
   BeginNPCTurnForAI (ticks, regen, perception)
   HP<=0? -> end
   Confused / Charmed / Fascinated ----> BehaviorController (whole turn; charmed stands up first)
   Prone (not grappling) -> TryStandUpFromProneForAI (move action, AoOs)
   Frightened ----> BehaviorController (whole turn)
   Animate Rope escape
   SelectBestTarget ----------------------------+--> profile.ScoreTarget / GetTargetPriority
   (none -> search move)                        +--> perception terms, LastKnownPositionTracker
   Turned undead -> flee | Grappling -> AI_GrappleRestrictedTurn
   Aura (free) ; free-action ranged special
   Resilient Sphere -> end
   route:  SwarmAI -> ExecuteSwarmTurn
           summon  -> GameManager.AI_SummonedCreature
           Healer  -> heal/buff via strategist, else Kiter/Melee
           Dragon  -> ExecuteDragonTurn (cast > breath > melee)
           profile -> DefensiveMelee | RangedKiter (or CombatStyle.Ranged) | AggressiveMelee
           none    -> switch(AIBehavior), default AggressiveMelee
          |
   routines call: EvaluateMovementOptions -> MoveCharacterAlongComputedPathForAI
                  SelectBestAction (charge only) -> NPCExecuteChargeForAI
                  PerformMeleeAttackActionWithManeuvers -> NPCPerformAttackForAI(tryStepManeuver:
                    ShouldUseManeuver -> TryNPCSpecialAttackByTypeForAI, before each melee step)
                  TryExecuteSpellcastAction -> SelectSpell -> TryNPCPerformSpellCastForAI
                  NPCPerformAttackForAI (melee step by step; ranged full or single attack)
          v
 SingleNPCTurnFromInitiative: AreAllPCsDead? -> DEFEAT ; else NextInitiativeTurn
```

### 2.3 File map

| File | Lines | Contents |
|---|---|---|
| Services/AIService.cs | 3,504 | Pipeline, all routines except summon and grapple, targeting, movement scoring, maneuver gate, spell selection glue, auras, specials, swarm damage (AI-020) |
| _Core/GameManager.NPCTurns.cs | 1,863 | `SingleNPCTurnFromInitiative` 35, `AI_SummonedCreature` 79, `TryNPCSpecialAttackIfBeneficial` 306, melee attack sequence `PerformNPCMeleeAttackSequence` 593, `TryNPCPerformSpellCast` 951, bombardier spray 1188, `NPCPerformAttack` 1285, breath 1651, frightful presence 1791 |
| _Core/GameManager.cs | 11,516 | `OnTurnStarted` 3764, `ShouldSkipTurnDueToHPState` 3909, `NextInitiativeTurn` 3948, the `*ForAI` block 10879-11036 |
| _Core/GameManager.NPCSetup.cs | 818 | `SetupEnemyEncounter` (adds `_npcAIBehaviors` 179), `InitializeNPCFromDefinition`, `BuildRuntimeAIProfile` 758 |
| _Core/GameManager.CombatActions.cs | | `MoveCharacterAlongComputedPath` 1148, withdraw 1102, shared AoO helpers `ResolveMovementAoOsBeforeStep`/`ExecutePathWithMovementAoOs`/`ResolveManeuverInitiationAoOs` 720-935, Wall of Ice AI helpers 2636-2670 |
| Combat/Maneuvers/SupportActions.cs | | `CanChargeTarget` 1061, `NPCExecuteCharge` 1801 |
| Combat/Maneuvers/GrappleSystem.cs | | `AI_GrappleRestrictedTurn` 1633, `ChooseNPCGrappleAction` 1747 |
| Combat/Behaviors/*.cs | 1,047 | The four condition controllers |
| AI/AIProfile.cs, AI/AIBehaviorData.cs, AI/SpellcasterAIBehaviorData.cs | | Profile base class and its data types |
| AI/Profiles/*.cs | | 19 concrete profiles; AI/Custom/CustomAIExample.cs is an unwired sample (AI-029) |
| AI/AISpellcastingStrategist.cs | 1,546 | Spell scoring, spell targeting, defensive-cast estimate |
| AI/SpellCategoryClassifier.cs | | Rewrites `SpellData.EffectType` at SpellDatabase init |
| AI/LastKnownPositionTracker.cs | 307 | Last-known squares, Listen and See Invisibility pinpointing |
| AI/AIManeuverTurnMemory.cs | | The per-creature, per-turn maneuver memory of the AI-060 stopgap (`CharacterController.AIManeuverMemory`) |
| AI/NPCTemplateAIConfigurator.cs, AI/AIConsumableManager.cs | | Reachable only from tests (AI-009, CRE-020) |
| Character/Creatures/NPCDatabase.cs | | `NPCDefinition.AIBehavior` 626, `AIProfileArchetype` 627, `AITargetPriority` 629, `UseCoupDeGrace` 631; enums `NPCAIProfileArchetype` 777, `NPCAIBehavior` 808 |

`AI/README.md` is stale; do not trust it.

### 2.4 Where configuration comes from

Per-NPC AI state lives in three places:

- `NPCDefinition` fields (above). `InitializeNPCFromDefinition` copies them: `npc.aiProfile = BuildRuntimeAIProfile(def)` (NPCSetup.cs:748), `EnemyUseCoupDeGraceOverride` (749), `PriorityTargetName` (750).
- `GameManager._npcAIBehaviors`, a list index-parallel to `NPCs` and read by `GetNPCBehaviorForAI` (AI-015, CRE-006).
- One `AIProfile` instance per NPC from `ScriptableObject.CreateInstance`, with all tuning hard-set in `OnEnable`. There are no profile `.asset` files (AI-017).

Writers of the AI fields besides the database files:

| Writer | Sets |
|---|---|
| `NPCDatabase_Dragons.BuildDragonDefinition` | archetype Dragon for 60 dragons |
| `NPCDatabase_M.MephitBase` | archetype Animal for 10 mephits |
| Skeleton, Zombie templates (`Apply`) | AggressiveMelee + UndeadMindless; `SkeletonFactory.HumanArcherSkeleton` sets behaviour `Ranged` |
| Lycanthrope template | AggressiveMelee + Berserk; wererat Humanoid |
| `DungeonEncounterSpawner.UpdateAIForClass` (Encounters/DungeonEncounterSpawner.cs:224) | class map for DMG spawns with class levels (table in 6.5) |
| Summon Swarm / Summon Monster swarm options (Spell/Resolution/GameManager.SpellCasting.cs:641, 778) | `IndiscriminateSwarmAI` / `SwarmAI` |
| Test overrides (NPCSetup.cs:184-197) | armor-targeting test: `RangedAIProfile` + tag; shield-bash test: `UndeadMindlessAIProfile` |
| `NPCTemplateAIConfigurator`, `QuickSpawnSystem.GetDefaultAI` | test-only paths with a different class map |

The `SkeletonCreatureTemplate` and `ZombieCreatureTemplate` registry adapters do not copy AI fields, so a creature skeletonised through `CreatureTemplateRegistry.ApplyTemplatesClone` keeps its original archetype (a skeleton ogre stays Brute). Related: CRE-023.

## 3. The NPC turn pipeline

### 3.1 From initiative to the AI

1. `TurnService.StartCombat` rolls initiative (d20 + modifier; ties by modifier, then random) and fires `OnTurnStarted` for each actor. `AISpellcastingStrategist.ResetCombatState` runs right after (GameManager.cs:3732).
2. `GameManager.OnTurnStarted` (3764) sends controllable characters to `StartPCTurn` and everything else to `SingleNPCTurnFromInitiative` (3800). `IsPC` means controllable (CORE-014).
3. `SingleNPCTurnFromInitiative` (NPCTurns.cs:35) sets the phase and UI, expires Aid Another bonuses, handles Flaming Sphere, then calls **`ShouldSkipTurnDueToHPState`**. If that is true it logs "X is <reason> and cannot act" and calls `NextInitiativeTurn` (CORE-012: synchronous).
4. Otherwise it reads the behaviour (`GetNPCBehaviorForAI`, default AggressiveMelee), runs `AIService.ExecuteNPCTurn`, and then checks only `AreAllPCsDead` (defeat). It does **not** check victory; see CORE-011.

### 3.2 The turn-skip gate

`ShouldSkipTurnDueToHPState` (GameManager.cs:3909) skips the turn when `CanTakeTurnActions()` is false (HP state not Healthy, Disabled or Staggered) or when `CharacterConditions.CanTakeActions()` is false. The latter is false for any active condition whose `ConditionDefinition` sets both `PreventsStandardActions` and `PreventsFullRoundActions` (Combat/StatusEffects/StatusEffect.cs). Those conditions are: BlownAway, Cowering, Dazed, HideousLaughter, Dead, Dying, Fascinated, Asleep, Helpless, Nauseated, Panicked, Paralyzed, Petrified, Pinned, Stable, Stunned, Turned, Unconscious.

Consequences (CMB-075; the PC path uses the same gate, GameManager.cs:3989):

- **Panicked** creatures never flee (PHB ch.8 / DMG ch.8 Condition Summary: a panicked creature drops what it holds and flees). The Panicked branch of `FrightenedBehaviorController` is unreachable, so frightful presence that panics a creature just freezes it.
- **Turned** undead never flee; `AIService.ExecuteTurnedUndeadTurn` (443) is unreachable. `TurnUndeadSystem` logs "continues fleeing" without moving anything.
- **Fascinated**: the `ExecuteNPCTurn` Fascinated branch (99-114), including its "source dead, remove the condition" cleanup, is unreachable.
- **Pinned** creatures lose their turn, so the pinned branch of `ChooseNPCGrappleAction` (break pin, escape) never runs for a pinned NPC. PHB ch.8 (Grapple) lets a pinned creature try to escape.
- **Nauseated** creatures (swarm distraction) lose the whole turn; DMG ch.8 allows a single move action.
- A skipped NPC also skips `BeginNPCTurnForAI`: no regeneration, no Melf's Acid Arrow tick, no `StartNewTurn`, no perception. `StartPCTurn` applies regeneration and acid arrow *before* its skip check. A troll at negative HP therefore never regenerates, while `AreAllNPCsDead` refuses to count a regenerating NPC at HP ≤ 0 as defeated (CORE-034).

### 3.3 `ExecuteNPCTurn` gate order

| # | Line | Gate | Effect |
|---|---|---|---|
| 0 | 50 | GameManager, npc or Stats null | end |
| 1 | 53 | `BeginNPCTurnForAI` (GameManager.cs:10930) | `ConditionService.OnTurnStart`, acid arrow damage, bombardier cooldown tick, `ApplyRegenerationAtTurnStart`, `StartNewTurn`, round-start perception (`ProcessRoundStartPerception` 2934: tracker update plus a Listen check), turn-undead tracker pruning. No breath, ranged-special or terrain cooldown is ticked (AI-032) |
| 2 | 55-61 | Turn banner | `CombatUI.` without null check (AI-018), wait 0.6 s |
| 3 | 66 | `CurrentHP <= 0` | end. A Disabled (0 HP) NPC never takes its single action (AI-053) |
| 4 | 73 | Confused | d% roll; any result except ActNormally runs the controller and ends the turn |
| 5 | 84 | Charmed | stands up first when prone, not grappling and not fascinated (same call as 6a; ends the turn if an AoO drops it), then controller, end |
| 6 | 99 | Fascinated | no action, end (unreachable, 3.2) |
| 6a | 119 | Prone and not `IsGrappling` | `GameManager.TryStandUpFromProneForAI` stands it up through `ResolveStandUpFromProne`, shared with the PC Stand Up button: spends the move action (or the standard converted to a move), AoOs from `ThreatSystem.GetStandUpAoOProvokers`, then removes Prone. Ends the turn if an AoO drops it. Stays prone, and cannot move, when `GetStandUpDisabledReason` refuses (CMB-074) |
| 7 | 129 | Frightened (or Panicked, unreachable) | controller, end |
| 8 | 135 | `TryExecuteAnimateRopeEscapeForNpc` | an NPC entangled by Animate Rope spends its standard action on STR/Escape Artist; turn continues |
| 9 | 140-153 | `SelectBestTarget` | none: `ExecuteSearchTurnWhenNoTargets`, reselect; still none: end |
| 10 | 155 | Turned and undead | `ExecuteTurnedUndeadTurn` (unreachable) |
| 11 | 161 | `IsGrappling` | `RunGrappleTurnForAI` -> `AI_GrappleRestrictedTurn`; end, unless the grapple ended with the standard action unspent (`CanContinueTurnAfterGrappleEnded`, after a free pin release), then continue to gate 12 |
| 12 | 170 | `HasAuraAbility` | `ProcessAuraAbility` (free) |
| 13 | 179-192 | Free-action ranged special ready | fire at the closest enemy in range (gibbering mouther Spittle) |
| 14 | 196 | In a Resilient Sphere | end (no movement inside the sphere either) |
| 15 | 204 | Profile is `SwarmAI` | `ExecuteSwarmTurn`, end |
| 16 | 211 | Summoned creature | `AI_SummonedCreature`, end |
| 17 | 219 | `HealerAIProfile` | Healer branch |
| 18 | 282 | `DragonAIProfile` | `ExecuteDragonTurn` |
| 19 | 289-300 | Other profile | DefensiveMelee; else RangedKiter if behaviour RangedKiter or `CombatStyle == Ranged`; else AggressiveMelee. `CombatStyle.Mixed` counts as melee |
| 20 | 305-319 | No profile | switch on behaviour; `Ranged` and default go to AggressiveMelee (AI-003) |

Side effects of the order: auras and free-action Spittle are skipped on any turn ended by gates 4-11 (AI-053); a grapple turn that continues after a pin release does reach them. Gates 4-7 never consult profile or behaviour. Target selection (gate 9) runs before the grapple, swarm and summon checks, so it rolls Sanctuary saves for creatures that then ignore the result (AI-005).

### 3.4 Coroutines, pacing and action economy

- `AIService` is a component on the GameManager object. Every nested coroutine is started with `_gameManager.StartCoroutine(...)` and awaited with `yield return` (49 sites). `ResetCombatStateForNextEncounter` calls `StopAllCoroutines`, which kills AI coroutines mid-turn (CORE-013).
- `CharmedBehaviorController` now awaits its moves too (it used to start them and act mid-walk).
- **Re-check HP after every movement yield.** Routines test `CurrentHP <= 0` after each move; `MoveAlongPath` also stops per step at HP ≤ 0. Attacks of opportunity, Wall of Fire, Wall of Ice breaches and similar can kill a mover.
- Pacing: 41 `WaitForSeconds` literals in AIService (0.2-0.8 s), 17 in NPCTurns.cs (0.35-1.0 s), more in controllers and GrappleSystem. Movement is 0.08 s per square (`PlayerMoveSecondsPerStep`, CombatActions.cs:619), charge 0.06. No speed setting (AI-023).
- **Action economy is the caller's job.** Movement helpers never spend actions; each AI routine tests `npc.Actions.HasMoveAction` before moving and then calls `npc.Actions.UseMoveAction()`, which returns false and changes nothing when no move action is left (Combat/Core/ActionEconomy.cs). After a search move or a breath-positioning move, the melee approach is skipped; no routine double-moves (a second move would be an explicit `ConvertStandardToMove`). Specials that spend no action remain (AI-053). Use `CharacterController.CommitStandardAction()` for standard actions; it handles the Disabled single-action rule.

### 3.5 How a turn ends

The routine returns; unused actions are discarded (no Delay, Ready, Total Defense). `SingleNPCTurnFromInitiative` checks `AreAllPCsDead`, then `NextInitiativeTurn` (GameManager.cs:3948) runs True Strike expiry, pinned-duration bookkeeping, `ConditionService.OnTurnEnd`, `ProcessEndOfTurnHPState` (dying and stabilisation), threat-preview invalidation, and advances `TurnService`. On wrap, `OnNewRound` ticks spell durations and `ConditionService.OnRoundEnd` (which runs at round start, CMB-006).

## 4. Target selection and movement scoring

### 4.1 `SelectBestTarget` (AIService.cs:1499)

1. **Mirror Image override.** `GameManager.GetMirrorImagePriorityTargetForAI` returns the nearest enemy Mirror Image caster or clone anywhere on the map, before every exclusion and ignoring the candidate list passed in (AI-036).
2. Ensures a `LastKnownPositionTracker` on the NPC.
3. For each living candidate that `TeamUtility.IsEnemy` accepts, applies exclusions in order: charm source (`ShouldExcludeTargetBecauseOfCharm`), fear source, Sanctuary (a fresh Will save each call, AI-005), Hide from Undead (undead attackers only; mindless excluded automatically; a successful save ends the spell, AI-005).
4. Partitions into **visible** (`npc.CanSee(target, ranged)`, which only means "miss chance below 50%"; no line of sight, GRID-009) and **concealed but tracked** (has a last-known square).
5. Returns the best visible target. If none, runs `AttemptListenChecks` on the concealed set (re-rolled per call, AI-007) and returns the best pinpointed, else the best tracked. Unseen enemies with no last-known square do not exist for the AI.

`SelectBestTargetFromCandidates` (1586) tries, in order: `PriorityTargetName` exact name match (from `AITargetPriority`; 4 definitions, `grease_test_grappler1`-`4`, all naming "Slippery Sam"), then the profile scorer, then armor-priority targeting (unreachable in practice: its only writer also assigns a profile, AI-053), then the default heuristic. With a profile, the profile scorer always returns a target.

A typical melee turn calls `SelectBestTarget` twice, a kiter three times; each call re-rolls ward saves and may roll Listen.

### 4.2 Score terms

**No profile: `GetTargetPriority` (2038)** + perception.

| Term | Formula |
|---|---|
| Proximity | `max(0, 12 - dist) * 1.8` |
| Threat | `CalculateThreat * 2`: +3 Cleric or Wizard, +2 Rogue, `clamp(Level*0.35, 0, 4)`, `clamp(STRMod+DEXMod, -2, 6)*0.35`, -0.75 if prone |
| Wounded | `(1 - hp%) * 8` |
| Armor | `clamp(26 - AC, -6, 8) * 0.7` |
| "Flanking" | +2.5 if the NPC can already threaten the target from its square (misnamed; not a flank test) |
| Creature type | +1.5 if undead or outsider |

**Base `AIProfile.ScoreTarget` (AI/AIProfile.cs:64):** 10; each `TagPriorities` match ±priority (case-insensitive bidirectional substring match on StatusTagManager tags such as "Armor: Heavy", "HP State: Disabled"); `PrioritizeWounded` +(1-hp%)×8; `PrioritizeIsolated` +4 alone or -1.25 per adjacent ally; Ranged style `-|dist - PreferredRange| * 0.9` and -5 adjacent; Melee style `max(0, 6 - dist) * 0.7`; Mixed no distance term. "HP State: Staggered" tags rarely fire because `HPState.Staggered` means nonlethal damage equals current HP.

**Overrides:**

| Profile | Score |
|---|---|
| Animal | base + `max(0, 8-dist)*1.8`; +2.5 if an ally is adjacent to the target; Grappler +2 if not already grappling it; Tripper +1.5 if standing |
| Dragon | base + `max(0, 10-dist)*1.2`; +5 vs Wizard/Cleric/Sorcerer/Druid (fades beyond 6 squares); +2 if target HP < 50% |
| Spellcaster family | base + 4 vs Wizard or Cleric |
| Lich | Spellcaster + Wizard 8 / Cleric 6, +2.5 per enemy within 4 squares of the target, +3 at 3-10 squares, -4 adjacent |
| Vampire | Spellcaster + Will ≤2 +8, ≤5 +4, ≥10 -3; HP < 30% +6; charmed/fascinated -5; Level ≥5 +2 |
| UndeadMindless | `100000 - 1000*dist` (nearest always wins, even over visibility) |
| UndeadTactical | own: 10 + `max(0, 8-d)*1.2`; helpless (or paralysed) adjacent +25, elsewhere +8, else +6; low Fort +5/+2; low AC +3; isolation; tags |
| UndeadBrute | own: 10 + `max(0, 10-d)*1.5`; helpless (or paralysed) adjacent +20, elsewhere +8; frightened/panicked/shaken +4; `(1 - hp%) * 10`; +2 per enemy within 6 squares of the target; tags |
| UndeadIncorporeal | own: 10 + `max(0, 8-d)`; touch AC ≤11 +8, ≤13 +4, ≤15 +1, else -2; existing drain up to +8; Wizard or Cleric +3; isolation; tags |
| Swarm | `100 - dist` (the swarm routine uses `ResolveTarget` instead) |

Profile-path extras: unconscious enemies are dropped first if `ShouldIgnoreUnconsciousTargets` (only Animal) and a conscious one exists. Everyone else keeps hitting dying PCs, and the maneuver chooser trips, disarms and grapples helpless targets (AI-058).

**Perception adjustment (`GetPerceptionTargetingAdjustment`, 1925),** added in all scorers. Side effect: writes the second last-known store (AI-021). Invisible: -18, or with Scent +4 within 6 squares / -6 beyond. Unseen: -12, +4 back if tracked. Then `GetConcealmentTargetingAdjustment` (2001): miss chance 0% +50, below 50% -30, else -80 (-40 more if unseen, +20 if tracked), times `ConcealmentPenaltyMultiplier` (always 1). These ±50-120 terms dwarf every profile term (about 10-60), so the concealment tier decides the target almost always; within a tier, distance and threat decide.

### 4.3 Other target selectors

| Selector | Used by | Filters |
|---|---|---|
| `SelectLowestHPEnemy` (2657) | DefensiveMelee's first target and charge | lowest absolute HP; no ward, charm, visibility or unconscious filter (AI-049) |
| `SelectAdaptiveFullAttackTarget` (3063) | mid-full-attack retarget | living, in reach if required, then `SelectBestTarget` (so Mirror Image can override reach, AI-036) |
| `TryTakeFiveFootStepForAdaptiveFullAttack` (NPCTurns.cs:863) | full attack with no target in reach | `ScoreTarget - dist*0.25` over legal 5-ft steps |
| `SelectSummonTarget` (2911) | `AI_SummonedCreature` | nearest enemy to summon or caster; no filters (AI-050) |
| `BuildSwarmTargetCandidates` + `SwarmAI.ResolveTarget` | swarms | sticky nearest; no filters (AI-050) |
| Controllers' nearest-creature helpers | Charmed, Confused | no filters (AI-050) |

### 4.4 Movement scoring

**`EvaluateMovementOptions(mover, targetPos, retreat, targetCharacter, profile)` (2087)**, 13 call sites.

- Candidates: every cell in one move range (`Grid.GetCellsInRange`, 3.5 1-2-1 diagonals, **current cell excluded**, so "stay" is never an option) that passes `CanPlaceCreature` and has a path from `FindPath(avoidThreats:false)`. One A* per cell (AI-019). A failed A* returns a non-null partial fallback path that the scorer accepts (AI-052).
- Parameters: `preferredRange = profile.Movement.PreferredRangeSquares` (1 without a profile; melee profiles 0; Dragon and Vampire 2; Ranged 6; casters 6; Lich 8). `avoidAoOs = Movement.AvoidAoOs && !ShouldIgnoreAoO` (false without a profile). `seekFlanking = Movement.SeekFlanking` (true without a profile).

| Mode | Score per cell |
|---|---|
| Retreat | `dist * 2` |
| Approach | `-|dist - preferredRange| * 2`; +2 if it can threaten the target from the cell; +3 if it would flank (`IsAttackerFlankingFromPosition`; only when a target character is passed) |
| Path provokes | -1000 if avoiding AoOs, else -2 per provoked AoO |

Ties keep the first cell in dictionary order. There is no cover, terrain, hazard, reach-weapon, ally-spacing or area-spread logic, and the horizon is one move action.

Other movement scorers: `EvaluateWithdrawRetreatDestination` (2143; 2× range, `dist*3 - provokes*4 - overshoot*2`); `TryTakeTacticalFiveFootStep` (1074; minimise expected AoO damage, +6 if no threats remain); dragon breath positioning (8.2); `ConfusedBehaviorController.FindBestMovementCell` (no path check); `FrightenedBehaviorController.FindBestFleeCell` (must increase distance).

### 4.5 Movement execution and attacks of opportunity

`MoveCharacterAlongComputedPathForAI` → `MoveCharacterAlongComputedPath` (`GameManager.CombatActions.cs`) recomputes the path with `MovementService.FindPath`, then `ExecutePathWithMovementAoOs` walks it. AoO-free stretches move in one `MoveAlongPath` call; before each step that leaves a threatened square, the shared `ResolveMovementAoOsBeforeStep` resolves that step's `ProvokedAoOs` while the mover is still in the square (PHB p.137). The same helper serves the PC move loop (`ExecuteMovement`), AI withdraw (`MoveCharacterAlongComputedPathWithdraw`, first square exempt), crawl and both charge paths. Movement stops if an AoO drops the mover (0 HP or less, dead, unconscious), trips it (newly Prone, e.g. a wolf's free trip) or gives it a condition that prevents movement (`ThreatSystem.ShouldStopMovementAfterAoO`); a tripped mover still takes the other AoOs provoked at that step. `ThreatSystem.AnalyzePathForAoOs` gives each enemy at most one AoO per path, whatever its Combat Reflexes count (per path, not per round: CMB-084). 5-foot steps never reach this code, and there is no Tumble. So enemies, enemy summons and charmed, confused and frightened creatures now provoke like PCs, and the -1000/-2 path scoring and `ShouldIgnoreAoO` matter. Every caller re-checks the mover's HP after the move coroutine before acting again; charmed movement now waits for the move instead of starting it and carrying on. Not verified in Play mode. Melee routines still path toward an unseen target's true square (AI-051).

## 5. Routines

### 5.1 `ExecuteAggressiveMeleeTurn` (503)

Used by 293 of 389 effective NPC definitions.

1. Standard-action ranged special in range → fire, end.
2. `SelectBestAction(preferAggression:true)`; only `Charge` is used (AI-027) → `NPCExecuteChargeForAI`, end.
3. Out of weapon reach and `HasMoveAction` → `EvaluateMovementOptions`, move, `UseMoveAction`, `ActivateTerrainManipulation` (log only, CRE-013).
4. Re-target with `SelectBestTarget` (may differ from the target moved toward).
5. In reach: Engulf if adjacent and able; else `PerformMeleeAttackActionWithManeuvers` (`NPCPerformAttackForAI` with `ShouldUseManeuver && TryExecutePreferredManeuver` tried before each melee step, CMB-102).
6. Out of reach: post-move ranged special, else `TryAIWallInteraction` (Wall of Ice), else the standard action is wasted (no double move).

Never casts (AI-002), never runs or double-moves, never uses Total Defense, Power Attack or Combat Expertise. Standard-action specials, Engulf and terrain manipulation exist only in this routine.

### 5.2 `ExecuteDragonTurn` (655)

1. Resets per-turn breath state on the profile instance (AI-017).
2. If at most 2 enemies are adjacent (`IsTooCloseForCasting`) and `IsSpellcaster`: `TryExecuteSpellcastAction`; success ends the turn. No score floor, so cantrips can pre-empt breath every turn (AI-037).
3. Breath ready: `DragonAIProfile.FindBestBreathPosition` tries the current square plus every reachable placeable cell (A* per cell), and every enemy as the aim point; picks most enemy hits, then fewest ally hits (0 allowed, `MaxAcceptableAllyHits`), then not moving; needs at least 1 enemy (`MinEnemiesForBreath`). Moves if needed, re-evaluates from the actual square, breathes, then retreats one move if an enemy is adjacent (block duplicated, AI-020). Positioning ignores AoOs and multi-square footprints.
4. If breath is not viable, falls through to melee with the move possibly already spent. Then charge (needs a full-round action), approach only if `HasMoveAction`, re-target, maneuver or attack.

Secondary breath is never evaluated (AI-008); `PreferBreathThreshold` is unread (AI-026). Breath recharge never ticks (AI-032).

### 5.3 `ExecuteRangedKiterTurn` (861)

1. Target `SelectBestTarget` (variable named `closestPC`). Update tracker if visible.
2. Not visible: Listen (AI-007); if pinpointed or tracked, attack the last-known square (`NPCPerformAttackForAI`); else advance and end.
3. `TryExecuteSpellcastAction`; success ends the turn.
4. Only with a ranged weapon equipped: `AssessRangedAoORisk` (threatening enemies that can make AoOs, expected damage) against `CalculateRangedRiskTolerance` (fraction of max HP: 0.25 above 75% HP, 0.10 above 50%, else 0.05; × profile multiplier clamped 0.25-2; +0.05 vs a target at ≤25% HP or a Wizard or Cleric; final 0.02-0.35). Too risky and threatened → `TryTakeTacticalFiveFootStep`.
5. Retreat a move if the target is within 2 squares or risk is too high (not gated on a ranged weapon, so melee-armed kiters back away from melee, AI-039).
6. Re-target, second `TryExecuteSpellcastAction`.
7. Range = max(weapon maximum range, best spell range). In range → maneuver or attack. Out of range → approach and **end with the standard action unused** (AI-045).

### 5.4 `ExecuteDefensiveMeleeTurn` (1170)

1. Target `SelectLowestHPEnemy` (unfiltered, AI-049); fall back to the passed target.
2. Charge if chosen; the `Retreat` result of `SelectBestAction` is ignored.
3. HP < 30% (hard-coded, AI-010) and a full-round action → withdraw (`EvaluateWithdrawRetreatDestination` + withdraw move with first-square AoO suppression), end.
4. Advance if `HasMoveAction`, re-target with `SelectBestTarget`, maneuver or attack, else wall interaction.

The enum docs promise Combat Expertise and holding position; neither exists (AI-031).

### 5.5 `ExecuteSwarmTurn` (1261)

Sticky target via `SwarmAI.ResolveTarget`. Moves directly onto the target's square (no movement scoring; only swarms share squares, GRID-005). `ApplySwarmDamageToOccupants` (1338) hits everything on the swarm's square: flat `SwarmDamage` (CRE-007) through `Stats.TakeDamage` (AI-006), then Fort vs `DistractionDC` or Nauseated 1 round; poison is log-only (CRE-014). No attack roll and no action spent. MM (Swarm subtype) checks distraction when a creature begins its turn in the swarm.

### 5.6 Healer branch (197-257)

`HealerAIProfile.DetermineActionPriority`: any ally (including itself, `Team ==`, AI-016) below 70% HP → Healing (≤25% → CriticalHealing); else castable spells and every ally ≥75% → Buffing; else castable → OffensiveSpell; else PhysicalAttack (`DetermineCombatMode`: kiter if AC < 16 or the ranged bonus is at least 2 higher than melee; melee if the melee bonus is at least 2 higher; otherwise melee only at AC ≥ 18. Melee then means DefensiveMelee or AggressiveMelee by behaviour). Heal and buff attempts call `TryExecuteSpellcastAction`; failure or OffensiveSpell → `ExecuteRangedKiterTurn`. The healer never moves to deliver a touch heal and can heal itself instead (AI-047). No database NPC uses this profile; only DMG Cleric spawns do, and those have no spells (ENC-021).

### 5.7 `GameManager.AI_SummonedCreature` (NPCTurns.cs:79)

Reached only by non-controllable summons (PC-cast summons are controllable and get PC turns; swarm summons go to the swarm routine first). Target by `SummonCommand` (AttackNearest / ProtectCaster; ProtectCaster is effectively unreachable, CRE-032). Retreat at ≤30% HP (not a withdraw) if a move action is left. Advance if a move action is left, re-target. A once-only template smite (Morale bonus fields, CMB-003), else `NPCPerformAttack` with a trip-only per-step evaluation for `HasTripAttack` summons: the trip replaces one step of the shared melee sequence at that step's BAB and the remaining steps attack (CMB-102; contradicts the Animal profile's "free trip on hit only"). Never casts or breathes; ignores behaviour and profile routing.

### 5.8 Search, Turned, grapple-restricted

- `ExecuteSearchTurnWhenNoTargets` (322): needs a move action; moves to the cell nearest a tracked last-known square, else toward the map centre with 0-4 random noise. The turn continues if a target appears; the next routine cannot move again (move action spent) but can still attack in reach.
- `ExecuteTurnedUndeadTurn` (443): flee one move from the turner; unreachable (3.2).
- `AI_GrappleRestrictedTurn` (GrappleSystem.cs:1636): see 8.8.

### 5.9 How attacks resolve: `GameManager.NPCPerformAttack` (NPCTurns.cs)

- Setup: break the attacker's own Sanctuary/Hide from Undead, fire pending frightful presence, `TryEnsureWeaponFallback` (UndeadMindless re-equips a backpack weapon for free), reload if needed (ends the turn), flank bonus, giant bombardier beetle acid spray (hard-coded by ID).
- **Melee** (main weapon not `WeaponCategory.Ranged`; the split reads the NPC's own weapon, not the PC thrown-mode global, CMB-096): `NPCMeleeAttackSequence` -> `PerformNPCMeleeAttackSequence` runs the NPC's own attack sequence one step at a time (CMB-102). The first step spends the standard action and the second turns the turn into a full attack (move action), so an NPC that moved, is slowed or can take one action makes one attack, and Haste adds one step: an iterative step to weapon and unarmed sequences, or one extra natural attack (at that attack's normal bonus) after the natural attacks of a natural sequence (PHB p.239, owner decision 2026-10-07). The AI picks that natural attack with `AIProfile.ChooseHasteNaturalAttackIndex` (default `ScoreHasteNaturalAttack` = `AI/NaturalAttackChoice.cs`: a rider that still matters against the target first (Improved Grab trigger, trip, poison, disease, paralysis, petrification, energy or ability drain), then the highest attack bonus, then the higher average damage); the NPC pounce asks the same. Before each step, weapon or natural, the caller's maneuver evaluation may replace the attack (a natural attack at its own BAB: primary full, secondary -5, -2 with Multiattack; MM p.312), and the remaining steps (including the other natural attacks) continue; a grapple started that way hands the remaining steps to `AI_GrappleRestrictedTurn`. In a natural sequence the NPC makes at most one maneuver substitute (an AI limit until AI-035; PCs may replace every natural attack), and sunder is never offered to a creature without a manufactured weapon (`CharacterController.CanSunderWithMainWeapon`). Each attack is `CharacterController.ResolveAttackSequenceStep`, the PC iterative resolver. Profiles with `ShouldSwitchTargetsMidFullAttack` re-target between steps (optionally with a 5-ft step); other NPCs stop when their target falls or leaves reach (logged as the reason). Per hit: concentration checks, melee reactions, free trip, Improved Grab (the NPC keeps attacking after a grab). Per kill: summon cleanup and the `AreAllPCsDead` check.
- **Ranged and thrown** (unchanged, CMB-091): the maneuver evaluation once, then a full attack when the full-round action is unspent, the target is in range and the NPC is not slowed, else a single attack (`CommitStandardAction`). Ranged attacks while threatened provoke first (`ResolveRangedAttackAoOForNPCAttackIfProvoked`).
- **Last-known misses:** after 3 consecutive auto-misses on a remembered square the target is forgotten; otherwise the NPC may spend its remaining move searching toward it.
- **Natural attacks** are used only when no manufactured weapon is equipped (`ShouldUseInnateNaturalAttackProfile`, CharacterController.cs:995). A creature with a weapon never adds secondary natural attacks (MM Introduction; CMB-077). Secondary natural attacks take -5, or -2 with Multiattack (read by the feat name, MM p.304; `CharacterStats.GetNaturalAttackSequencePenalty`).
- **Charge** (`NPCExecuteCharge`, SupportActions.cs:1801): decision by `ShouldNPCCharge` (full-round action, melee weapon, not in reach, `CanChargeTarget` straight-line path) and `profile.ShouldPreferCharge`. Path AoOs are resolved. +2 is applied as `MoraleAttackBonus` (RAW untyped). Pounce gives a full attack plus rake. Calls the victory check.
- **Maneuvers:** `ShouldUseManeuver` (2241) → coup de grace first (profile or `UseCoupDeGrace` override, full-round, helpless adjacent target); else `profile.GetPreferredManeuver` re-validated (Trip needs a standing target and any melee or natural weapon; Disarm, Grapple, Sunder their checks); without a profile the legacy chooser `TryNPCSpecialAttackIfBeneficial` (AI-025). Trip, disarm, sunder and grapple cost one step of the NPC's attack sequence at that step's bonus (`ManeuverActionCost.ReplacesMeleeAttack`, `CharacterController.TryCommitManeuverSubstituteStep`; the first step spends the standard action), bull rush and overrun a standard action, coup de grace the full round; the evaluation runs before every step of the NPC melee attack, weapon or natural. On its own it would chain maneuvers (Humanoid: trip, disarm an armed target, grapple; Berserk: trip, grapple; Grappler: trip, disarm, grapple; null profile: trip, disarm at STR mod 3+, grapple at STR mod 4+) and retry failed ones. A stopgap in `AIService.TryExecutePreferredManeuver` (owner decision 2026-10-07, AI-060, to be replaced by weighted personality scoring) keeps a per-creature, per-turn memory (`CharacterController.AIManeuverMemory`, `AI/AIManeuverTurnMemory.cs`, cleared in `StartNewTurn`): once a maneuver succeeds the AI makes no further maneuver that turn and attacks with the remaining steps, so a trip that lands is followed by attacks on the prone target; after a maneuver fails (a lost check or an attempt foiled by an AoO) it does not retry that type against that target that turn, and the step attacks unless the evaluation picks a different maneuver (a profile picks one maneuver per step, so in practice it attacks; the legacy chooser's pick is read through `GameManager.PeekNPCFallbackManeuverForAI`). A grapple that takes hold still hands the remaining steps to `AI_GrappleRestrictedTurn`, and coup de grace, the profile order and the legacy chooser are unchanged. The executor reports what it attempted and whether it succeeded (`TryNPCSpecialAttackByTypeForAI(..., out succeeded)`, `TryNPCSpecialAttackIfBeneficialForAI(..., out attempted, out succeeded)`). The ranged kiter's pre-attack maneuver goes through the same check; the summon trip in `AI_SummonedCreature` does not (it is trip-only and stops once the target is prone). Inside a natural-attack sequence any step can be a maneuver, rolled at the BAB of the natural attack it replaces, and the other natural attacks follow (AI-035, CMB-102; owner decision 2026-10-07). Grapple, sunder, trip and disarm provoke from the target (not with the matching Improved feat), bull rush from every threatening enemy (Improved Bull Rush spares only the defender; 25% of the others' AoOs strike the defender) and coup de grace from every threatening enemy, all through `ResolveManeuverInitiationAoOs`, shared with the PC wrapper; after a successful bull rush the NPC's stay-or-follow choice comes from `AIService.ChooseBullRushPush` -> `AIProfile.ChooseBullRushPush` (default: follow for the maximum; CMB-098); a foiled attempt still spends the action (disruption rule: CMB-083; a disarm fails only on AoO damage). The opposed-check math follows PHB p.154-158 in the shared `CharacterController.Resolve*` methods (CMB-014 fixed; not verified in Play mode).

## 6. Profiles and archetypes

### 6.1 Behaviour to routine

| `NPCAIBehavior` | No profile | With profile | Effective users |
|---|---|---|---|
| AggressiveMelee (field default) | AggressiveMelee | AggressiveMelee unless `CombatStyle == Ranged` | 357 |
| RangedKiter | RangedKiter | RangedKiter | 22 (archers, test casters, bralani, elf_warrior, erinyes, halfling_warrior, lantern_archon, manticore, medusa, drider) |
| DefensiveMelee | DefensiveMelee | DefensiveMelee (checked first) | 6: hobgoblin_sergeant, human_cleric, test_fighter_gust, svirfneblin, skeleton_warrior, shrieker |
| Ranged | AggressiveMelee (AI-003) | AggressiveMelee unless `CombatStyle == Ranged` | 4: gauth, beholder, rakshasa, template skeleton_archer |

Effective routine reached (389 registered IDs; behaviour and archetype counts confirmed at runtime in the editor via `NPCDatabase.AllNPCs` on 2026-10-03): AggressiveMelee 293, Dragon 61 (60 dragons + hell_hound), RangedKiter 23 (includes the lich via `CombatStyle.Ranged`), Swarm 6, DefensiveMelee 6, Healer 0.

The skeleton archer (behaviour `Ranged`, UndeadMindless) runs AggressiveMelee, but because `IsTargetInCurrentWeaponRange` uses its bow's maximum range it usually stands and shoots without AoO assessment, and charges when a legal charge exists.

### 6.2 Archetype to profile (`GameManager.BuildRuntimeAIProfile`, NPCSetup.cs:758)

| Archetype | Class | CombatStyle | Effective | Representatives | Key behaviour |
|---|---|---|---|---|---|
| None | null (Animal if `CreatureType == "Animal"`) | — | 4 | gelatinous_cube, ochre_jelly, thoqqua, gauth | legacy maneuver chooser |
| Animal | AnimalAIProfile | Melee | 126 | tiger, dire_wolf, lion, vermin variants, 10 mephits, basilisk, gibbering_mouther, digester | specialty (Grappler/Tripper/PackHunter), charge rules, ignores unconscious, mid-attack retarget |
| Humanoid | HumanoidAIProfile | Melee | 66 | goblin, hobgoblin, orc_warrior, gnoll, bugbear, troll, drow, ghost, spectre, wererat | Trip > Disarm > grapple when STR ≥ target |
| Berserk | BerserkAIProfile | Melee | 19 | orc_berserker, ogre_brute, slaads, 7 lycanthropes | Trip > aggressive grapple; no AoO avoidance or flanking; coup de grace |
| Grappler | GrapplerAIProfile | Melee | 16 | owlbear, chuul, choker, mimic, behir, constrictor snake | Trip > Disarm > aggressive grapple (Improved Grab creatures never choose a manual grapple; the rules allow one) |
| Ranged | RangedAIProfile | Ranged | 8 | bralani, elf_warrior, erinyes, halfling_warrior, lantern_archon, manticore, medusa | kiter, preferred range 6 |
| Spellcaster | SpellcasterAIProfile | Melee (never set) | 15 | aboleth, mind_flayer, ogre_mage, succubus, beholder, rakshasa, drider, 3 test casters | spell scoring; preferred range 6 |
| Evoker | EvokerAIProfile | Ranged | 3 | arcane_missile_adept, gust_druid, mist_wizard | school priorities Evocation 10, Transmutation 5, Abjuration 3 (others 1), each ×10 in `ScoreSpell` |
| Healer | HealerAIProfile | Ranged | 0 | DMG Cleric spawns only | Healer branch |
| Abjurer / Necromancer | | Mixed / Ranged | 0 | none | Abjurer would route as melee and never cast |
| UndeadMindless | UndeadMindlessAIProfile | Melee | 22 | zombies, skeletons, black_pudding, gray_ooze, violet_fungus, shrieker | nearest target; ignores AoOs; weapon re-equip |
| UndeadTactical (and Undead) | UndeadTacticalAIProfile | Melee | 5 | ghoul, ghast, wight, wight_dreadwalker, vampire_spawn | helpless/low-Fort targeting; coup de grace |
| UndeadBrute | UndeadBruteAIProfile | Melee | 2 | mummy, bodak | always charge; ignores AoOs |
| UndeadIncorporeal | UndeadIncorporealAIProfile | Melee | 3 | allip, shadow, wraith | low touch AC targeting |
| Vampire | VampireAIProfile | Melee | 1 | vampire | grapple when STR +2 or target helpless/paralysed; never casts (AI-002, CRE-030) |
| Lich | LichAIProfile | Ranged | 1 | lich | kiter; no prepared spells (SPL-015) |
| Dragon | DragonAIProfile | Mixed | 61 | 60 dragons, hell_hound | Dragon routine |
| Swarm / IndiscriminateSwarm | SwarmAI / IndiscriminateSwarmAI | Melee | 3 / 3 | bat, rat, spider / centipede, hellwasp, locust swarms | swarm routine |
| Brute | null (AI-004) | — | 29 | ogre, giants, hydras, minotaur, purple_worm, umber_hulk, ankheg, chimera, gorgon, treant, wyvern | legacy chooser |
| Caster | null (AI-004) | — | 2 | couatl, spirit_naga | legacy chooser |

35 effective definitions have a null profile. Every profile except the swarms logs as `[AI][Profile:Default AI]`, because subclasses set `ProfileName` only when empty and the base initialiser already set it (AI-053).

### 6.3 Profile members and hooks

| Member | Read by | Status |
|---|---|---|
| `CombatStyle` | routing (AIService.cs:293), base `ScoreTarget` | Mixed is never special-cased |
| `TagPriorities`, `PrioritizeWounded`, `PrioritizeIsolated` | base `ScoreTarget` (and copies in the undead profiles) | live |
| `Movement.PreferredRangeSquares`, `AvoidAoOs`, `SeekFlanking` | `EvaluateMovementOptions`, `TryTakeTacticalFiveFootStep` | live |
| `GrappleBehavior` | `ShouldInitiateGrapple`, `GetPreferredManeuver` | `Maintain` unreachable (no maneuvers while grappling) |
| `Maneuvers.AttemptTrip/Disarm/Sunder/BullRush/Overrun` | base `GetPreferredManeuver`, fixed order | Sunder, BullRush, Overrun never set (AI-014) |
| `Aggression`, `SwitchTargetsOften`, `Movement.MaintainDistance`, `Movement.UseCover`, `Maneuvers.UseAidAnother/UseCombatExpertise/UsePowerAttack`, `FleeHealthThreshold`, `StayNearWoundedAllies`, `PreferBreathThreshold`, `SpellSchoolPriority.CombatOnly` | nothing | dead (AI-026, AI-010) |

| Hook | Runtime callers |
|---|---|
| `ScoreTarget` | AIService.cs:1818, 3119; NPCTurns.cs:914 |
| `ShouldPreferCharge` | `SelectBestAction` (2634) |
| `ShouldIgnoreUnconsciousTargets` | AIService.cs:1810, 3089; NPCTurns.cs:622, 777, 890 |
| `ShouldSwitchTargetsMidFullAttack`, `ShouldTakeFiveFootStepToContinueFullAttack` | NPCTurns.cs:593, 646 (melee attack sequence only) |
| `ShouldUseCoupDeGrace` | AIService.cs:2360; NPCTurns.cs:293 (data override wins) |
| `ShouldIgnoreAoO` | AIService.cs:2109 |
| `GetRangedAoORiskToleranceMultiplier` | AIService.cs:1088 (kiter with ranged weapon only) |
| `TryEnsureWeaponFallback` | NPCTurns.cs:1298 |
| `ShouldInitiateGrapple`, `GetPreferredManeuver` | AIService.cs:2279, 2263, 2333 |
| `ScoreSpell` (Spellcaster family) | `SelectSpell` |
| `ShouldEscapeGrapple` | none (AI-029) |

### 6.4 Hook matrix (overrides only)

| Profile | CdG | IgnoreAoO | Switch mid-FA | 5-ft in FA | Ignore uncon. | Charge | AoO risk × | Maneuver |
|---|---|---|---|---|---|---|---|---|
| base | false | false | false | false | false | true | 1.0 | Trip > Disarm > Sunder > BullRush > Overrun > Grapple by flags |
| Animal | true | | true | true | true | by specialty (≥2 sq; Grappler needs Pounce; Tripper if standing; PackHunter ≥4) | | none (free trip / Improved Grab) |
| Humanoid, Grappler | false | | | | | | | base |
| Berserk | true | | | | | | | base |
| Spellcaster family | false | | | | | | 0.75 | base (none set) |
| Lich | true | | | | | | 0.5 | none |
| Vampire | true | | false | true (unreachable) | | | | own grapple test (ignores Improved Grab) |
| Dragon | false | false | true | true | | false while breath ready; else 3-8 sq | 0.85 | none |
| UndeadMindless | false | true | | | | | | none; weapon fallback |
| UndeadTactical | true | false | true | true | | | | none |
| UndeadBrute | true | true | | | | always | | none |
| UndeadIncorporeal | false | true | true | true | false | | | none |

### 6.5 Mapping gaps and surprising combinations (inferred)

- **Spellcaster-profile monsters hover.** `SpellcasterAIProfile` leaves `CombatStyle` Melee, so aboleth, mind_flayer, ogre_mage and the rest run AggressiveMelee with a preferred range of 6: the approach scoring parks them about 6 squares away with nothing in reach. They reach melee only by charging. None has spells anyway (AI-038).
- **Range-2 tie.** Vampire and Dragon profiles prefer range 2. For a reach-1 creature an adjacent cell scores -2+2 and a 2-square cell 0, so grid order decides whether it closes (AI-038). The vampire (`SeekFlanking` true) does close when an adjacent cell would flank (+3); the dragon never gets that term.
- **Weaponless ranged monsters.** Bows in `EquipSlot.Ranged` are never equipped (ITM-004), so bralani, elf_warrior, erinyes, halfling_warrior, medusa and drider spawn unarmed on the kiter routine and oscillate (AI-039). The lantern archon's ray and manticore spikes are not modelled as ranged attacks.
- **Trip loop.** Humanoid, Berserk, Grappler and all null-profile NPCs trip every standing target in reach with their first attack step (136 effective definitions, AI-035, CMB-102). Since the stopgap of 2026-10-07 (AI-060) a trip that lands is followed by attacks on the prone target, and a failed trip is not retried against that target that turn; before it, the later steps went to the next maneuver (disarm, then grapple) or retried the trip. No odds or size check (PHB ch.8 limits trips by size).
- **Breath only through the Dragon profile.** ankheg, behir, chimera, digester and gorgon never breathe (AI-033).
- **Mephits** use Animal (PackHunter) tactics; their breath and SLAs are text (CRE-017).
- **Class maps disagree.** `DungeonEncounterSpawner.UpdateAIForClass` vs the test-only `NPCTemplateAIConfigurator`: Sorcerer Evoker vs Spellcaster, Adept Spellcaster vs Healer, Paladin Humanoid vs Ranged, Barbarian Berserk vs Humanoid. The spawner only overrides the archetype for Fighter/Paladin/Monk/Warrior/Rogue if it was None or Animal.

## 7. Spellcasting AI

### 7.1 Who can cast

Casting is attempted only by the Healer branch (heal/buff), `ExecuteDragonTurn` (before breath), `ExecuteRangedKiterTurn` (twice per turn) and a charmed NPC healing its charmer (direct call, no strategist). AggressiveMelee, DefensiveMelee, Swarm and summons never cast (AI-002).

An NPC gets a `SpellcastingComponent` only if `stats.IsSpellcaster` (a caster class) **and** its `KnownSpellIds` or `PreparedSpellSlotIds` is non-empty (NPCSetup.cs:716-718). Prepared IDs are assigned **by slot index** (`SpellcastingComponent.ApplyPreparedSpellSlotIds`), slots are ordered from cantrips up, mismatched levels are rejected, and wizard NPCs only get spell levels 0-2: 4/3/2 base slots from class level 4 up, plus one bonus 1st-level slot at Int modifier +1 and one bonus 2nd-level slot at +2 (`GetWizardSlotsForLevel`), so 4/4/3 for the lich and the Int 16+ test wizards. Result by static reading:

| NPC | Expected castable |
|---|---|
| arcane_missile_adept (Wizard 5, `AI:MagicMissileOnly`) | 3× Magic Missile |
| evil_enchanter_test | 2× Charm Person (and Charm Person is capped at HD 4 by targeting, SPL-093) |
| neutral_mage_test | Daze, unlimited |
| evil_acolyte_test, gust_druid, mist_wizard | nothing |
| lich (Wizard 11) | nothing: its list starts with 1st-level spells, every index lands in a slot of another level (SPL-015) |
| vampire | nothing: `CharacterClass = "Fighter"` (CRE-030) |
| 60 dragons | spontaneous Sorcerer via an injected class level; cantrips plus some 1st-4th level (CRE-016) |
| Spellcaster/Caster-archetype monsters, DMG class-levelled casters, human_cleric, paladins | nothing (no lists; SLAs text only; ENC-021, CHR-024) |

So the casting AI is exercised today only by three test NPCs and the dragons.

### 7.2 `TryExecuteSpellcastAction` (AIService.cs:2385)

1. Needs a standard action and `IsSpellcaster`. `SelectSpell` returns the single best spell (null: return false).
2. `AISpellcastingStrategist.EvaluateDefensiveCasting`: 2 = no adjacent non-ally; 1 = adjacent but estimated Concentration (DC 15 + level) ≥ 50%, or HP ≤ 25% with a Healing/Escape spell; 0 otherwise. 0 → try `SelectLowerLevelAlternative`, else abort. 1 → log, then cast. The roll happens at cast time (7.6): `ResolveNPCSpellcastProvocation` uses the real threat list (reach included, so a caster threatened only by a reach weapon also gets the choice) and `AISpellcastingStrategist.ShouldCastDefensively`, the same ≥ 50% or desperate-heal test.
3. `SelectBestSpellTarget` → `TryNPCPerformSpellCastForAI`. False → the routine falls back to non-spell actions.

There is no spell-versus-weapon comparison and no score floor: any castable spell with a valid target is cast. If the best spell is refused at cast time (Area), the caster does not try the second best.

### 7.3 `SelectSpell` and scoring

Candidates: `GetCastablePreparedSpells` (distinct prepared unused IDs; known spells with a slot for spontaneous casters; cantrips unlimited). Silence is not checked here or in `CanCast` (SPL-092). Buff/Illusion spells already active on the caster are skipped. Score = `SpellcasterAIProfile.ScoreSpell` for that family, else `AISpellcastingStrategist.ScoreSpellComprehensive`. Highest wins, even if negative.

`SpellcasterAIProfile.ScoreSpell` (AI/Profiles/SpellcasterAIProfile.cs:49): school priority ×10 (unlisted school = 10); AoE terms (+3 per enemy hit, -10 per ally casualty, -6 below minimum, -1000 unsafe; a null primary target counts as unsafe); single-target preference ±4-5; buff already active -1000; buff-first +4 above 65% HP; conserve 3rd+ level -1/-4; counter +2; utility -5; plus the strategist. Healer, Lich and Vampire add their own terms (Healer ±10-40 by action priority; Lich Necromancy/Control/save-or-die/dispel bonuses; Vampire Control/charm/hold bonuses).

`ScoreSpellComprehensive` (AI/AISpellcastingStrategist.cs:1115) sums 19 terms:

| Term | Summary |
|---|---|
| Effect base | Damage 8 (+15 if it kills), Healing 6 (+20/+10 when hurt), Buff 5, Debuff 7, Control 9 (+5 outnumbered), Summon 6, Wall 7, Illusion 6, Dispel 4, Escape 2, Utility/Divination 1; + 1.5×level |
| Pre-buff | rounds ≤2, no adjacent enemy: +20 (+10 if an enemy is within 6), duration and protection bonuses |
| Threatened penalty | adjacent enemy: -2 to -40 by Concentration odds |
| Save exploit | +15 weakest save, -10 strongest, +10 no save |
| Resistance | immune -50, resistance -5 to -30, force +5 |
| Resources | penalties for the last spell at a level and low remaining counts (counts distinct IDs, not slots) |
| Dispel, Summon, Area denial, Counterspell, Escape | situational bonuses |
| Multi-round plan | +12 for any Buff in rounds ≤2; the plan store itself is never filled (AI-011) |
| Combo | Haste per frontliner, Web/Grease, invisibility when adjacent |
| Metamagic | rewards swift (none exist) and non-somatic spells; no metamagic is ever applied (AI-001) |
| Spell Focus, domain, bard, druid | small class terms; the domain term rewards any domain-list spell (AI-048) |
| Enemy type | -50 mind-affecting vs undead/construct/ooze (mindless counts as undead) |

Scale problems (school ×10 dominating, overlapping pre-buff bonuses): AI-022. Terms that use a target use the routine's fallback target, not the one later chosen.

### 7.4 Spell targeting (`AISpellcastingStrategist.SelectBestSpellTarget`, 226)

- Self spells: the caster.
- Ally spells (Healing, non-Area Buff, SingleAlly): `SelectBestAllyTarget` scores allies in range (healing by missing HP, frontliner bonus; buff skipped if already active); returns the **caster** if no ally is in range. Touch range means adjacent only, so a healer often heals itself (AI-047).
- Enemy spells: Mirror Image target in range wins; else `ScoreEnemyTarget` (wounded, casters, save exploit, resistance, kill bonus, SR). The SR chance is computed as `(CL + 1 - SR)/20` instead of `(21 + CL - SR)/20` (PHB ch.10, Spell Resistance), so moderately resistant targets look unbeatable (AI-046). A `CanSee` check (miss chance below 50%, not line of sight, GRID-009) applies only to SingleEnemy spells. No Sanctuary (AI-005), creature-type, HD or immunity filtering. Falls back to the passed target, which in the Healer branch is a wounded ally (AI-047).
- Team checks use raw `Team` comparisons (AI-016).

### 7.5 `SpellCategoryClassifier` (AI/SpellCategoryClassifier.cs)

`ReclassifyAll`, called from `SpellDatabase.Init`, **permanently overwrites `SpellData.EffectType`** for the whole game using ID lists (Control, Summon, Dispel, Wall, Illusion, Escape, Divination, Utility) and a heuristic (mind-affecting Will-save Debuff → Control). The runtime log `[SpellCategoryClassifier] Reclassified N spells` gives the count. Side effects outside the AI: save negation has to treat Debuff and Control alike, so both single-target cast paths call `SpellUtilities.IsEffectNegatedBySave`; spells moved to Summon, Dispel, Escape, Divination or Utility no longer reach `ApplySpellBuff`, which by static reading makes targeted Dispel Magic and Break Enchantment unreachable on both cast paths (SPL-091); scroll/wand gates and UI colours read the new type; Spell/Database depends on an AI class (SPL-068).

### 7.6 The NPC cast path (`GameManager.TryNPCPerformSpellCast`, NPCTurns.cs:939)

Order: action and `CanCast` checks; `IsValidTargetForSpell`; range; **reject `TargetType == Area` (808, AI-001)**; `CommitStandardAction` (always standard, even for full-round spells such as Summon Monster); entangled (DC 15 + level) and grappled (DC 20 + level) casting Concentration through the PC helpers, which spend the slot on a failure; `CastSpellFromSlot`; arcane spell failure and Blink; `ResolveNPCSpellcastProvocation` (PHB p.140: if threatened, cast defensively at Concentration DC 15 + level or cast normally and take AoOs, each hit forcing DC 10 + damage + level; the choice is `AISpellcastingStrategist.ShouldCastDefensively`, the rolls are the PC prompt's `ResolveThreatenedSpellcast`; a failure loses the spell, SPL-006); break invisibility; counterspell; Ring of Counterspells on the target; Mirror Image redirect; `SpellCaster.Cast` with null metamagic; save negation shared with the PC path (`SpellUtilities.IsEffectNegatedBySave`: Debuff and Control negate, Cause Fear and Scare are partial); 17 of 51 effect handlers (SPL-054); `ApplySpellBuff` for tracked effect types; concentration on damage; death handling. Missing against the PC path: components, metamagic, energy-type choice, the check for casting while maintaining a concentration spell (`HandleConcentrationOnCasting`), concentration-duration tracking, the victory check, Silence.

Outcome by category (static reading): Area spells refused; single-target damage works (no CL scaling, SPL-005); Healing works on adjacent targets only; Buffs use the generic path (SPL-037); Debuffs negated by saves; Control applied despite saves; Summon, Dispel, Escape, Divination and Utility casts spend the slot and do nothing (SPL-091).

### 7.7 Spell-like abilities, counterspells, consumables

- **Spell-like abilities:** none. No `NPCDefinition` field, no cast path; dragon SLA lists are unread (CRE-015); creature SLAs are text (CRE-017). MM Introduction treats SLAs as spells without components or slots, usable at will or per day.
- **Counterspells:** `AIService.TryAIReadyCounterspell` (2705) has no callers (SPL-047). PHB ch.10 counterspelling requires a readied action (PHB ch.8), which does not exist (CMB-029).
- **Consumables:** `AIConsumableManager` is test-only and only returns a potion name (AI-009). The only live NPC consumable use is a charmed NPC healing its charmer. Seven NPC definitions carry potions they never drink (11.8.3).

## 8. Special creatures and condition-driven behaviour

### 8.1 Condition controllers (Combat/Behaviors)

| Controller | Reached? | Behaviour | Deviation |
|---|---|---|---|
| Confused | yes (also forces PC turns, `TryBeginConfusedPCTurn`) | d%: 01-10 attack the source if in reach else **hit itself**; 11-20 act normally; 21-50 babble; 51-70 flee one move; 71-100 attack the nearest creature of any team | PHB ch.11 (Confusion): 01-10 attacks the caster or closes with it; no self-damage; no "attacked creature retaliates" rule; flee should be at top speed (AI-043) |
| Charmed | yes | caster dead → remove, lose turn; caster injured → move adjacent (awaited) and heal it by spell or potion; else attack the nearest enemy of the caster | PHB ch.11 (Charm Person): friendly, not controlled; attacking its own allies is Dominate behaviour (AI-042). Charm breaks only when the charmer attacks the subject |
| Fascinated | no (turn skipped, 3.2) | stares | |
| Frightened | Frightened yes; Panicked no | source dead → clear; withdraw (2× move) with a full-round action, else "run" at 1× move (it provokes like any move); cornered Frightened fights defensively and **never attacks**; cornered Panicked logs "cowers" with no condition | PHB ch.8 run is ×4; DMG ch.8 a cornered frightened creature may fight (AI-044) |

The source of each condition comes from `ActiveCondition.Source`, then a typed payload, then a display-name match (CORE-017). Frightened PCs get no forced behaviour.

### 8.2 Dragons

- 60 generated dragons (10 types × Wyrmling-Adult) plus hell_hound use `DragonAIProfile`. Turn order: cast (≤2 adjacent enemies), breath with positioning, melee (5.2).
- **Breath execution** (`NPCExecuteBreathWeaponForAI`, NPCTurns.cs:1641): one damage roll (`UnityEngine.Random`) for all targets; hits everything in the cone or line including allies (RAW); immunity list, then `D20 + save` with no natural 1/20 and no Evasion, half on success, first matching resistance subtracted by hand, then `Stats.TakeDamage`, bypassing `ApplyIncomingDamage` (AI-040, same class as AI-006). It spends no action. The cooldown is set but `TickBreathWeaponCooldown` has no callers, so each creature breathes at most once per spawn (AI-032).
- **Frightful presence** (`ResolveFrightfulPresence`, 1570; Young Adult and Adult dragons, and the lich's "fear aura"): fires once per combat on the first attack or breath, not on charge; Manhattan-distance range; no "fewer HD than the dragon" filter; HD ≤4 panicked else shaken for 4d6 rounds; raw d20 save (AI-041). Panicked targets then lose their turns (CMB-075). MM (Dragons, Frightful Presence) triggers it on attacks and charges, and a creature that saves is immune for 24 hours.
- Secondary (metallic) breath: AI-008. Dragon SLAs: CRE-015. Dragon cantrip loop: AI-037.

### 8.3 Undead, animals, swarms

- **Lich:** kiter routing, but no prepared spells (SPL-015); paralyzing touch only; area spells would be refused anyway (AI-001); no fear aura (AI-010).
- **Vampire:** AggressiveMelee; never casts (CRE-030, AI-002); grapple when STR mod ≥ target +2 or target helpless or paralysed, refused at execution because it has Improved Grab; `BloodDrainDefinition` unused (CRE-031); no gaseous-form escape (AI-010).
- **Undead Tactical / Brute / Incorporeal / Mindless:** targeting heuristics only (6.2). Incorporeality is a flat 50% miss chance (CMB-025), which also makes an incorporeal creature "unseen" to every AI observer (CMB-078); `IsIncorporeal` has no other reader. The bodak's death gaze and the ghast's stench do nothing (CRE-031, CREATURES.md).
- **Animals:** `DetermineSpecialty` picks Grappler (pounce + Improved Grab + rake), Tripper (`HasTripAttack`) or PackHunter. "Pack" means only +2.5 target score when an ally is adjacent to the target; there is no shared target, surround or retreat.
- **Swarms:** see 5.5. Swarm and summon targeting ignores wards and visibility (AI-050).

### 8.4 Monster specials and auras

| Ability | AI use | Notes |
|---|---|---|
| Engulf (cloaker, gelatinous_cube, gibbering_mouther, ochre_jelly) | AggressiveMelee, adjacent target, replaces the attack | Reflex save then permanent Helpless (CMB-024); no action spent |
| Ranged special (gibbering mouther Spittle) | free-action version at turn start; standard-action versions in AggressiveMelee | bypasses the attack/damage pipeline (AI-006); spends no action |
| Terrain manipulation | after moving in AggressiveMelee | log only (CRE-013) |
| Bombardier acid spray | inside `NPCPerformAttack` by creature ID | typed damage via `ApplyIncomingDamage`; dice labelled "Fireball"; cooldown does tick |
| Auras and gazes (14 definitions) | `ProcessAuraAbility` (3312) each non-skipped turn | enemies in range, mind-affecting immunity, save via `SpellSaveResolver.RollSave` (no Luck reroll, CORE-030), condition for 1dN or fixed rounds; 24-hour immunity in a static set never cleared after ordinary victories (CORE-002); unmapped effects become Confused (AI-013); placeholders such as the beholder antimagic cone (CRE-012). Gazes are modelled as auras: no averting eyes |
| Stench (ghast, nightmare, troglodyte), grapple blood drain | none | configured, never read (CRE-031) |
| Constrict, swallow whole, trample, rock throwing | none | text only (CRE-018, CREATURES.md) |

### 8.5 Regeneration

`ApplyRegenerationAtTurnStart` heals a fixed amount unless suppressed by the right damage tag or HP ≤ -10. It runs only at PC turn start and in `BeginNPCTurnForAI`, so an NPC at negative HP never regenerates (CORE-034). MM regeneration (damage becomes nonlethal) is not modelled; troll and others behave as fast healing (CRE-017). No profile reasons about regeneration (own or enemy).

### 8.6 Grapple AI

- **Starting a grapple:** via `GetPreferredManeuver`/`ShouldInitiateGrapple` (Aggressive always; InitiateWhenSafe if STR mod ≥ target's; never with Improved Grab), or the legacy chooser (STR mod ≥4). Improved Grab is a free attempt on a qualifying hit, automatic for NPCs.
- **In a grapple** (`AI_GrappleRestrictedTurn`, up to 8 iterations, 20% random early stop): predatory animals use their full natural routine plus rake, escape below 25% HP and deal grapple damage while pinning; an AI pinner whose pin is due (its next turn after pinning; PHB p.156, a pin lasts 1 round, CMB-120) pins again first only when that pays (`ShouldNPCRenewDuePin`: it keeps a grapple attack for damage this turn, or another able ally is in the fight and the target is a spellcaster or that ally is adjacent to it); otherwise it takes the pinning choices below, which let the pin lapse, so a lone one-attack pinner keeps dealing damage instead of renewing every round while the pinned target loses its turns (CMB-075); a predator below 25% HP that pins releases the pin (free, ends the grapple) before any escape check; others, when pinning, Damage > Use Opponent's Weapon > Disarm 50% > Move 20% > Release 5%; otherwise Pin 35% (STR mod ≥3), light weapon 45%, opponent's weapon 30%, unarmed 20%, then Damage. Releasing a pin is a free action that ends the grapple for both creatures and moves neither (PHB p.157, CMB-089 fixed; `CharacterController.IsFreeGrappleAction`). The AI rolls the release only before it has spent an attack this turn, so no iterative attacks are lost, and `AIService.RunGrappleTurnForAI` / `CanContinueTurnAfterGrappleEnded` then let `ExecuteNPCTurn` carry on with a normal turn (standing up first if prone) while the standard action is unspent (not verified in Play mode). Several chosen sub-actions are stubs (CMB-033). Non-animal NPCs escape only when pinned, which never happens because pinned turns are skipped (CMB-075). `GrappleBehavior.EscapeOnly`/`Avoid` are ignored once grappled (AI-029).

### 8.7 Summons and allied NPCs

| Creature | Team / control | Turn path |
|---|---|---|
| PC-cast Summon Monster / Nature's Ally | Player, controllable | PC turn |
| PC-cast swarm options, Summon Swarm | Player, not controllable | swarm routine (Summon Swarm is indiscriminate) |
| Enemy-side summon (test scenarios only) | Enemy | `AI_SummonedCreature` |
| Lion's Shield lion | wielder's team | not a recognised summon; no `_npcAIBehaviors` entry (AI-015) |
| Test allies (dire_tiger, template tests) | Player, controllable | PC turn |

No NPC can summon (SPL-091): summon spells spend the slot and spawn nothing. No definition sets `IsAlly`, nothing uses `CharacterTeam.Neutral` (AI-016), and defeat/victory checks would not count an allied AI NPC.

### 8.8 Perception and wards

- `CanSee` = miss chance below 50% (`GetMissChance`: invisibility unless See Invisibility or Glitterdust, Darkness, Wall of Fire, Entropic Shield, incorporeal, Displacement). No line of sight (GRID-009); no blindsight, tremorsense or blindsense.
- Two last-known stores: `LastKnownPositionTracker` and `CharacterController._lastKnownTargetPositions`; never cleared (AI-021). Going invisible writes both for all enemies.
- `AttemptListenChecks`: See Invisibility users roll opposed Spot vs Hide; everyone else rolls one Listen vs a flat DC 20 that pinpoints all concealed targets (re-rolled per call, AI-007). PHB ch.4 (Listen) and DMG (Invisibility) use opposed Move Silently with distance penalties and a higher DC to pinpoint.
- Sanctuary and Hide from Undead: see 4.1 and AI-005. `BreakProtectiveWardsOnAttack` runs on the PC attack path and `NPCPerformAttack` only, not on charges, maneuvers, grapples or spells (SPL-094, partly unverified).

### 8.9 Morale and fleeing

There is no morale system, surrender or rout. The only retreats are: DefensiveMelee withdraw below 30% HP, summon retreat at 30%, the animal grapple escape below 25%, the dragon's post-breath step, the kiter's distance keeping, and condition-forced fleeing (Confused 51-70 and Frightened work; Panicked and Turned do not, CMB-075). `FleeHealthThreshold`, `GaseousFormFleeThreshold` and `Aggression` are unread (AI-010, AI-026). The 3.5 core books leave monster morale to the DM; the DMG offers guidance rather than a mechanic. What blocks a morale system (no map exit, victory and XP only at HP ≤ 0) is in 11.8.5.

## 9. Action coverage matrix

The full matrix (every 3.5e combat option: AI status, where, PC availability, issue IDs) and the "PC can, NPC cannot" list are in [AI_ACTION_COVERAGE.md](AI_ACTION_COVERAGE.md).

## 10. Known issues

### 10.1 Issues this doc depends on

From [issues/AI.md](../issues/AI.md), AI-001 to AI-031 (AI-032 to AI-054 were filed from this doc and are listed in 10.2):

- **Casting:** AI-001 (no area spells or metamagic, High), AI-002 (melee routines never cast), AI-011 (planning stores never filled), AI-022 (score scales), AI-028 (unused strategist statics).
- **Routing and data:** AI-003 (`Ranged` behaviour unhandled), AI-004 (Brute and Caster have no profile), AI-015 (index-parallel `_npcAIBehaviors`), AI-016 (raw team checks, Neutral), AI-017 (profile leak, `OnEnable` overwrites, dragon per-turn state), AI-031 (enum docs lie).
- **Targeting and perception:** AI-005 (ward saves re-rolled), AI-007 (Listen re-rolled), AI-021 (two last-known stores).
- **Monster abilities:** AI-006 (specials bypass the pipeline), AI-008 (secondary breath), AI-013 (status and aura mappings), AI-014 (overrun target placeholder; bull rush and overrun never chosen).
- **Missing behaviour:** AI-009 (consumables), AI-010 (flee thresholds, lich aura), AI-012 (difficulty unused).
- **Structure and performance:** AI-018 (`CombatUI` null derefs), AI-019 (A* per cell), AI-020 (3,504-line god class), AI-023 (hard-coded delays), AI-024 (two-way coupling via `*ForAI`), AI-025 (duplicate maneuver choosers), AI-026 (unread settings), AI-027 (`SelectBestAction` results other than `Charge` are ignored; if morale is built, wire in its `Retreat` result instead of reducing it to a charge test, 13.3), AI-029 (uncalled members), AI-030 (dead `ShouldNPCUseCharge`).

From other files: CMB-003, CMB-004, CMB-006, CMB-008, CMB-015 to CMB-019, CMB-021 to CMB-029, CMB-031 to CMB-033, CMB-037, CMB-039, CMB-044, CMB-055 ([issues/CMB.md](../issues/CMB.md)); SPL-005, SPL-024, SPL-025, SPL-027, SPL-037, SPL-038, SPL-041, SPL-047, SPL-054, SPL-068 ([issues/SPL.md](../issues/SPL.md)); CRE-002, CRE-004, CRE-006, CRE-007, CRE-012 to CRE-018, CRE-020, CRE-022 to CRE-024 ([issues/CRE.md](../issues/CRE.md)); CORE-002, CORE-004, CORE-011 to CORE-015, CORE-017, CORE-022, CORE-030 ([issues/CORE.md](../issues/CORE.md)); GRID-005, GRID-007, GRID-009 to GRID-011 ([issues/GRID.md](../issues/GRID.md)); ITM-004, ITM-005, ITM-018, ITM-019, ITM-024 ([issues/ITM.md](../issues/ITM.md)); CHR-003 to CHR-005, CHR-008, CHR-020, CHR-021, CHR-023, CHR-024, CHR-026, CHR-053, CHR-062 ([issues/CHR.md](../issues/CHR.md)); ENC-001, ENC-005, ENC-012 ([issues/ENC.md](../issues/ENC.md)); TST-001 to TST-003, TST-008 ([issues/TST.md](../issues/TST.md)).

### 10.2 Issues found while writing this doc

Filed in `issues/` while this doc was written; all are static readings, so confirm them in Play mode before fixing. Severity and evidence are in the issue entries.

| ID | Issue |
|---|---|
| CMB-074 | Confused turns never stand up from prone; the AI never crawls (narrowed 2026-10-07: other NPCs, charmed ones included, now stand up, and nobody moves normally while prone) |
| CMB-075 | Conditions that prevent standard and full-round actions skip the turn before the AI: Panicked and Turned never flee, Pinned never escapes, Nauseated gets no move action, and skipped turns miss regeneration and acid-arrow ticks |
| AI-032 | Breath weapons, ranged specials and terrain manipulation never recharge (their tick methods have no callers) |
| AI-033 | Only the Dragon profile breathes; ankheg, behir, chimera, digester and gorgon never do |
| AI-035 | Trip-flagged profiles and null-profile NPCs trip every standing target in reach (one attack step), with no odds check |
| AI-036 | The Mirror Image priority target overrides all targeting map-wide, before exclusions and reach filters |
| SPL-015 | The lich, evil_acolyte_test, gust_druid and mist_wizard prepare nothing (positional slot assignment plus the wizard 2nd-level cap) |
| CRE-030 | The vampire is a Fighter, so it never gets a `SpellcastingComponent` |
| ENC-021 | DMG class-levelled casters get caster AI but no spell lists |
| AI-037 | Dragons cast before breathing with no score floor, so cantrips can pre-empt breath every turn |
| AI-038 | Spellcaster-profile monsters on AggressiveMelee hold at about 6 squares; Vampire and Dragon range-2 tie |
| AI-039 | Kiter retreat is not gated on a ranged weapon; weaponless ranged monsters oscillate |
| AI-040 | Breath saves have no natural 1/20 or Evasion, damage bypasses `ApplyIncomingDamage`, and breath spends no action |
| AI-041 | Frightful presence fires once per combat, not on a charge, with Manhattan range, no HD filter and a raw save |
| AI-042 | Charmed NPCs attack their own allies |
| AI-043 | Confusion 01-10 makes the creature hit itself; no retaliation rule; one-move flee |
| AI-044 | Frightened "run" is 1× speed; cornered Frightened never attacks; cornered Panicked applies no Cowering |
| AI-045 | The kiter's approach branch ends the turn with the standard action unused |
| CMB-077 | Natural attacks are used only when no weapon is equipped (no weapon plus secondary naturals) |
| AI-046 | The strategist's SR chance `(CL+1-SR)/20` is 20 points too low |
| AI-047 | The healer heals itself when no ally is adjacent, never moves to touch, and can aim an offensive spell at its wounded ally |
| AI-048 | The domain spell term rewards any spell on any domain list |
| SPL-091 | NPC Summon, Dispel, Escape, Divination and Utility casts spend the slot with no effect; targeted Dispel Magic and Break Enchantment are unreachable after reclassification |
| SPL-092 | NPC casts ignore Silence |
| SPL-093 | Charm Person targeting is capped at HD 4 |
| CRE-031 | Stench auras and the grapple `BloodDrainDefinition` are configured but never read |
| CORE-034 | Regenerating NPCs at negative HP never regenerate yet are not counted as defeated, which can stall combat |
| CRE-032 | The summon command menu has no effect; ProtectCaster is unreachable |
| CMB-078 | Incorporeal creatures count as unseen to every AI observer |
| AI-049 | DefensiveMelee moves toward the lowest-HP enemy without ward, charm or visibility filters |
| AI-050 | Summon, swarm, charmed and confused targeting ignore wards and visibility |
| AI-051 | Melee routines path to the true square of unseen targets |
| AI-052 | `EvaluateMovementOptions` accepts A*'s partial fallback path, so unreachable cells can win |
| AI-053 | Assorted small defects and uncalled members (auras skipped on controlled turns, Disabled NPCs never act, specials spend no action, `ProfileName` always "Default AI", and others) |
| TST-025 | Stale `AIProfileFrameworkTests` expectations; the AoE safety test can pass vacuously |
| CORE-011 | The NPC attack path has no victory check except on a charge |
| SPL-094 | Sanctuary and Hide from Undead are broken only by `NPCPerformAttack` and the PC attack path, not by charges, maneuvers or spells |
| AI-054 | PC action executors (Aid Another, item use, area casting, Turn Undead) are bound to the PC turn flow, so the AI cannot reuse them |
| AI-058 | `ShouldUseManeuver` never checks whether the target is helpless, so the AI trips, disarms and grapples unconscious enemies (seen in Play mode by the scenario harness) |
| CMB-121 | A creature cannot join a grapple in progress; the refused attempt still uses the attack, and the chooser offers it again every round (103 refused attempts in the 50-fight soak) |
| AI-059 | The AI tries targeted spells against swarms, which are immune (trips are refused by `CanTrip` since CMB-079); with no area spells (AI-001) an AI-run party cannot hurt a swarm (seen in the soak) |
| AI-060 | The per-turn maneuver stopgap (no maneuver after one succeeds, no retry of a failed type against the same target; owner decision 2026-10-07) is to be replaced by weighted personality scoring ([design](../designs/enemy_ai_knowledge_and_personalities.md) section 7, Increment 1) |

## 11. Extension points

### 11.1 Add a tactical routine

1. Add the value to `NPCAIBehavior` (NPCDatabase.cs:808) and fix its XML doc (AI-031).
2. Route it in `ExecuteNPCTurn`: the profile branch (267-278), the no-profile switch (283-297), and the Healer PhysicalAttack sub-branch if relevant. Dragon, swarm and summon routing bypass behaviour.
3. Write `private IEnumerator ExecuteXTurn(CharacterController npc, CharacterController target)` following AggressiveMelee: HP check on entry; charge via `SelectBestAction`; `EvaluateMovementOptions` → `MoveCharacterAlongComputedPathForAI(npc, cell, GetPlayerMoveSecondsPerStepForAI())`; **HP check after every movement yield**; **test `HasMoveAction` before moving** and call `UseMoveAction` (it returns false when no move is left); re-target; maneuver or `NPCPerformAttackForAI` (it chooses full vs single attack); fallback `TryAIWallInteraction`. Call `TryExecuteSpellcastAction` if it should cast. Start nested coroutines with `_gameManager.StartCoroutine` and `yield return` them.
4. Set `AIBehavior` in data and in the other writers (2.4). The value reaches the AI through `_npcAIBehaviors`; keep it aligned (AI-015).

### 11.2 Add a profile

1. `AI/Profiles/<Name>AIProfile.cs`, `namespace DND35.AI.Profiles`, `: AIProfile` or `: SpellcasterAIProfile`.
2. Set tuning in `OnEnable` (`private` in `AIProfile` subclasses; `protected virtual` in `SpellcasterAIProfile` and `SwarmAI`, so call `base.OnEnable()`). Set `CombatStyle` explicitly; `SpellcasterAIProfile` leaves it Melee, which means no casting (AI-002). Set `ProfileName` unconditionally if you want readable logs.
3. Override hooks from 6.3. Keep score magnitudes in mind: visibility terms are ±50-120.
4. Add an `NPCAIProfileArchetype` value and a `case` in `BuildRuntimeAIProfile`; set the archetype on definitions.
5. Needs its own routine (like Healer or Dragon)? Add an `is` branch in `ExecuteNPCTurn` before line 266.
6. Keep per-turn state off the instance or accept one instance per NPC (AI-017).
7. Unit-test it in `AIProfileFrameworkTests` (`ScriptableObject.CreateInstance<T>()`, then `TestHelpers.Cleanup`).

### 11.3 Add a `*ForAI` wrapper

AIService may use only public GameManager members (docs/ARCHITECTURE.md conventions; AI-024). Add an expression-bodied delegate next to the block at GameManager.cs:10879-11036, e.g. `public bool TryNPCPerformSpellCastForAI(...) => TryNPCPerformSpellCast(...);`. Coroutine executors return the `IEnumerator`; AIService runs them with `_gameManager.StartCoroutine`. Existing wrappers: 40 (34 in GameManager.cs); 4 have no callers (AI-053).

### 11.4 Add an NPC action (attack form, monster ability, special action)

1. **Executor:** a GameManager partial next to `NPCPerformAttack`, `NPCExecuteBreathWeaponForAI`, `NPCExecuteCharge`, or in AIService if it needs no GameManager privates (`TryExecuteEngulf`, `ProcessAuraAbility`). Expose GameManager executors with a wrapper.
2. **Action economy:** spend the action yourself (`CommitStandardAction`, `UseFullRoundAction`, `UseMoveAction`); existing specials forget to (AI-053).
3. **Rules plumbing:** resolve AoOs where PHB ch.8 says the action provokes (movement: `ResolveMovementAoOsBeforeStep`; maneuvers: `ResolveManeuverInitiationAoOs`; casting: `ResolveNPCSpellcastProvocation`); route damage through `Stats.ApplyIncomingDamage` with a typed `DamagePacket` (do not copy AI-006 or the breath code); saves through `SavingThrowResolver`; check `AreAllPCsDead` and victory (CORE-011).
4. **Call site:** free actions at the top of `ExecuteNPCTurn` (after the condition gates); standard actions before or after movement in the routines that should use it. Prefer a capability check usable by every routine over adding it to AggressiveMelee only.
5. **Data:** a new ability kind needs an `NPCDefinition` field, a deep copy in `NPCDefinition.Clone`, a `CharacterController.ConfigureX`, a call in `InitializeNPCFromDefinition`, and the template copy lists (DEVELOPMENT_RECIPES "Add a monster or NPC"). Tick its cooldown in `BeginNPCTurnForAI` (AI-032).
6. **Feedback:** `CombatUI?.ShowCombatLog(CombatLogHelper...)`, a `[AI][Tag]` `Debug.Log`, and a `WaitForSeconds`.

Maneuvers go through `ManeuverPreferences` → `GetPreferredManeuver` → `ShouldUseManeuver` → `TryNPCSpecialAttackByTypeForAI`; see DEVELOPMENT_RECIPES "Add a combat maneuver".

### 11.5 Add a condition-forced turn

Write `Combat/Behaviors/<Name>BehaviorController.cs` with `TryBuildDecision` and `ExecuteDecision`; add a lazy field and `TryGet...TurnDecisionForAI` / `Execute...TurnDecisionForAI` wrappers in GameManager; call them in `ExecuteNPCTurn` before targeting (73-111). **Check the condition's `PreventsStandardActions`/`PreventsFullRoundActions` flags first**: if both are set the turn is skipped before your controller runs (CMB-075).

### 11.6 Add a spell-scoring term

Add a static `Get<X>Score` to `AISpellcastingStrategist` and sum it in `ScoreSpellComprehensive` (1115); constants at 31-87. Profile-specific terms go in a `ScoreSpell` override. Remember `EffectType` is the reclassified value (7.5) and that the scale must compete with school ×10 (AI-022). See DEVELOPMENT_RECIPES "Make NPCs cast a spell, and test a spell".

### 11.7 Pitfalls

- `_npcAIBehaviors` is index-parallel to `NPCs`; summons append and remove (AI-015, CRE-006).
- Every movement yield can kill the mover; re-check HP.
- `StopAllCoroutines` on encounter reset kills AI mid-turn (CORE-013); turn advancement is re-entrant (CORE-012).
- `CombatUI.` without `?.` throws in headless tests (AI-018).
- `SelectBestTarget` has side effects (ward saves, Listen, tracker writes); do not call it speculatively in a loop without accepting them.
- Spawn-time `aiProfile` assignments are overwritten by `BuildRuntimeAIProfile`; change the archetype instead.
- Duplicate creature IDs: the later registration wins (CRE-022).
- PC action executors (Aid Another, item use, area casting, turning) end or redraw the PC turn; wrap their rules core instead of calling them from the AI (AI-054).

### 11.8 Request playbooks

Worked playbooks for likely requests (flanking and Aid Another, area spells and Haste, potions, trips and disarms, morale and fleeing, cleric healing and channelling, class abilities such as Smite, Rage and Turn Undead) are in [AI_PLAYBOOKS.md](AI_PLAYBOOKS.md).

## 12. Testing and debugging

### 12.1 Suites

| Suite | Covers |
|---|---|
| `Tests.AI.AIProfileFrameworkTests.RunAll` (38 test methods, no callers) | profile scoring (Berserk, Ranged, Animal), maneuver preferences, coup de grace defaults, NPC data archetypes, Evoker/Abjurer/Spellcaster AoE settings, Healer priorities, concealment tiers, ThreatSystem estimates, swarm targeting, `ActionEconomy.UseMoveAction` failure and the one-move gate. 2-3 tests are stale (TST-025) |
| `Tests.Classes.NPCTemplateSystemTests` | configurator class maps and `AIConsumableManager` (test-only paths; TST-003, TST-008) |
| `Tests.Combat.MirrorImageRulesTests`, `CauseFearRulesTests`, `MediumConditionRulesTests` | Mirror Image priority target, Frightened decision, confusion d% distribution |

Untested: `ExecuteNPCTurn` and every routine, `SelectBestTarget`, `EvaluateMovementOptions`, `SelectSpell`, the strategist, the classifier, `LastKnownPositionTracker`, `BuildRuntimeAIProfile`, `TryNPCPerformSpellCast`, summon and grapple AI, Dragon/Lich/Vampire/Undead/Humanoid/Necromancer profiles.

Run them with the committed runner in [TESTING.md section 3](../TESTING.md#3-running-suites), for example `Tests.Runner.StaticSuiteRunner.RunFromCommand("filter=AIProfileFrameworkTests,MirrorImageRulesTests")` in Play mode (CauseFear runs in the edit pass). Pitfalls: some tests `AddComponent<GameManager>()`, which `Awake` destroys in MainScene; one adds a `SquareGrid` that replaces `SquareGrid.Instance` (GRID-010), so restart Play mode afterwards. Pure functions you can test without a GameManager: `GetConcealmentTargetingAdjustment`, `FindClosestAliveEnemy`, `SelectSummonTarget`, `FindNearestEnemy`, and all profile hooks. In Play mode the live service is `GameManager.Instance.GetComponent<AIService>()`. Call `AISpellcastingStrategist.ResetCombatState()` between strategist tests.

### 12.2 Encounter presets for AI work

| Preset | Exercises |
|---|---|
| `goblin_raiders` | Humanoid, DefensiveMelee and the `Ranged` skeleton archer in one fight |
| `grapple_test`, `tiger_hunt_test` | Grappler choices; pounce, Improved Grab, rake, emergency escape, scent vs an invisible PC |
| `grease_test` | `AITargetPriority` |
| `armor_targeting_test` | `RangedAIProfile` tag targeting and the kiter (not the armor-priority scorer, AI-053) |
| `obscuring_mist_ranged_only`, `wind_dispersion_test` | concealment tiers, last-known attacks, Listen, ranged AoO risk |
| `disrupt_undead_test`, `charm_person_test` | DefensiveMelee; charmed NPC healing its charmer |
| `mirror_image_test` | Mirror Image override |
| `npc_magic_missile_test`, `protection_from_evil_test` | NPC cast path end to end |
| `summon_monster_test` | summons |
| `tier2_ooze_dungeon` | null-profile NPCs, engulf |
| `tier3_cloaker_ambush`, `tier3_mummy_tomb` | auras |
| `tier3_hell_hound_pack`, dragon presets (`dragon_red_young_solo`, `dragon_red_vs_silver`, ...) | Dragon routine, breath positioning, frightful presence, dragon casting |
| `undead_showcase`, `tier5_lich_sanctum`, `tier4_vampire_hunt` | undead profiles, lich, vampire |
| `wolf_pack`, `beast_arena` | animal specialties |

No preset contains a Brute or Caster NPC, a swarm, the gibbering mouther, a Spellcaster-archetype monster, a non-dragon breather, or a Healer, Abjurer or Necromancer profile. Use the Custom Encounter Builder (UI/Encounter/CustomEncounterBuilderUI.cs; at most 8 creatures; hides `_test`/`_drill` IDs) or DMG spawns (Cleric levels give the Healer profile). Preset caveats: TESTING.md 4.2, ENC-001, ENC-012, CORE-004.

### 12.3 Observing decisions

All AI logs are unconditional `Debug.Log`; there is no verbose flag. Filter the Console on `[AI` to catch every tag: `[AI]`, `[AI][Profile:...]`, `[AI][Default]`, `[AI][PriorityTarget]`, `[AI][MirrorImage]`, `[AI][Sanctuary]`, `[AI][HideFromUndead]`, `[AI][Search]`, `[AI][Healer]`, `[AI][Spell]`, `[AI][Dragon]`, `[AI][DragonBreath]`, `[AI][RangedAoO]`, `[AI][Maneuver]`, `[AI][Aura]`, `[AI][Attack]`, `[AI][Concealment]`, `[AI][SpecialAttack]`, `[AI][Grapple]`, `[AI][Animal]`, `[AI][SpecialAbilities]`, `[AI][UndeadMindless]`, `[AI][BreathWeapon]`, `[AI][ArmorPriority]`, `[AI SelectBestAction]`, `[AI Charge]`, `[AI Validation]`. Also `[SpellCategoryClassifier]` at startup.

Not logged anywhere: which routine ran, the behaviour value, the profile class assigned at spawn, the chosen movement cell and its score, and per-spell scores. The strategist, the tracker and the condition controllers have no `Debug.Log`. Adding a single decision-trace line per turn (routine, target and score, cell, action) is the cheapest debugging investment. Noise that buries AI lines: per-call A* logs (GRID-011), threat rebuild logs (CMB-039), `[AOO-DEBUG]`, `AreAllNPCsDead` lines.

### 12.4 Cost and pacing

- **CPU (estimated, not profiled):** each `EvaluateMovementOptions` call runs one A* (with allocations and a log line) plus a path-AoO analysis per candidate cell; candidates grow from 60 cells at move 4 to 432 at move 12 (capped by the 20×20 grid). Withdraw searches double range; dragon breath positioning adds a breath-area computation per cell per enemy. `GetAllCharactersForAI` allocates a new list on every call (21 call sites in AIService, 38 project-wide). All of it runs synchronously between yields, so it shows as a frame hitch (AI-019, CMB-039).
- **Wall clock (summed from literals):** turn start 0.6 s; stationary full attack about 1.6 s; move plus single attack about 2.1 s plus 0.08 s per square; kiter shot about 2.4 s. A round with eight goblins is roughly 15-20 s (AI-023).

## 13. Toward deeper AI (analysis for the owner to decide)

This section is analysis, not a plan. It describes structural constraints and options; the owner decides direction.

### 13.1 Structural constraints

- **Decisions are encoded as control flow.** The routine order (cast, then breath, then move, then attack) *is* the decision. Adding an option means adding a branch to each routine that should consider it, which is why engulf, specials and breath each live in one routine only. `SelectBestAction`/`EvaluateAttackOptions` and `EvaluateBestManeuver` are vestiges of a scoring layer that was never finished (AI-025, AI-027); `SelectBestAction`'s `Retreat` result is worth keeping for morale (13.3).
- **Scores are not comparable.** Target scores, movement scores, spell scores and maneuver preferences use unrelated scales (and visibility swamps targeting). There is no common unit such as expected damage, expected HP removed, or expected conditions imposed, so "spell versus full attack versus breath" cannot be compared.
- **The rules engine is asymmetric** (full audit and unification plan: [PC_NPC_PARITY.md](PC_NPC_PARITY.md)). NPC paths skip parts of the save and damage pipeline and let some specials spend no action (AI-053); AoOs on movement, maneuvers and casting, and casting Concentration, now follow the PC rules. Any smarter AI built on top would optimise against rules the player does not share. These asymmetries are also the main source of "the AI feels wrong" reports that are not AI bugs.
- **Capabilities are data the AI cannot see.** Spell-like abilities, stench, constrict, secondary breath and many MM specials exist only as text or unread fields; most monsters that should cast cannot. A deeper chooser has little to choose from until the capability data is executable.
- **One-move horizon, no memory, no team.** Movement looks one move ahead, profiles are stateless except for dragons and swarms (each creature's only other memory is the per-turn maneuver stopgap, AI-060), and NPCs share nothing.
- **Performance budget.** Per-cell A* already causes hitches; any search over action sequences needs a shared reachability flood and a cached threat map per turn first (AI-019).
- **Testability and the AI-depth metric.** The Play-mode scenario harness ([TESTING.md](../TESTING.md) 3.4-3.5) runs `ExecuteNPCTurn` in the real scene: whole fights with seeded and forced dice, a typed trace of every turn, attack, AoO, maneuver, NPC cast, move and condition, and rules invariants; actors can be AI-run, scripted (typed steps through the same `*ForAI` executors, after the compulsion gates), idle or PC-driven. Its 24 `rules/*` scenarios pin shared mechanics. The AI-vs-AI soak `soak/dmg-random-encounters` (the AI-run Quick Start party against DMG dungeon random encounters, seeds 1-50, run with `ScenarioHarness.QueueFresh("soak/*", "1-50", "perSession=10")`) is the AI-depth baseline: its `soak` statistics give per-team actions per turn (baseline 2026-10-07: party 0.17 attacks, 0.15 maneuvers and 0.28 casts per turn; enemies 0.50 attacks, 0.21 maneuvers and no casts; 13 of 50 fights reached the 30-round cap). Compare a change to the AI against that table and read the traces of the seeds that moved. The soak surfaced AI-047 (the Healer cleric cures itself at full HP on 468 of 624 turns), AI-059, CMB-121, CMB-123, SPL-121 and CRE-044; earlier smoke runs surfaced AI-058 and confirmed CORE-011. AI-018 (the unguarded `CombatUI` uses in `ExecuteNPCTurn`) still blocks a headless run outside the scene.

### 13.2 Prerequisites (fix before tuning behaviour)

Ordered by how much they distort what the AI experiences:

1. Rules parity on NPC paths: the turn-skip gate versus controllers (CMB-075), stand up in confused turns and crawl (CMB-074), breath and special resolution through the standard pipeline with recharge (AI-032, AI-040, AI-006).
2. Capability data that works: spells for the lich, vampire, DMG class levels and Spellcaster monsters (SPL-015, CRE-030, ENC-021), or an SLA system (CRE-015, CRE-017); ranged weapons actually equipped (ITM-004); null profiles mapped (AI-004); `Ranged` behaviour handled (AI-003).
3. Plumbing: behaviour stored on the NPC instead of the parallel list (AI-015); one target filter `IsTargetableBy(attacker, target, action)` with cached ward saves used by every selector (AI-005, AI-049, AI-050); a decision trace log.

### 13.3 Options for a deeper decision layer

- **Unified candidate-action evaluation.** Generate legal options each turn (move, double move, 5-ft step, full attack, single attack, charge, each maneuver, each castable spell or SLA with its best target or area placement, breath, item use, withdraw, total defense, delay/ready once they exist) and score each in one unit, for example expected HP removed from enemies plus a value for conditions imposed, minus expected damage taken (including AoOs provoked) and resources spent. Profiles would become weight vectors and veto rules over this scorer instead of routines. The existing routines could remain as fallbacks while options migrate.
- **Area spells and positioning.** An NPC area-cast path reusing the PC AoE resolution (AI-001), with placement search shared with breath positioning and the existing `EvaluateAOECast` ally-safety model.
- **Two-step planning.** Score "move now, full attack next round" and "5-ft step plus full attack", and reach-weapon spacing; needs the reachability cache.
- **Group tactics.** A per-side blackboard: claimed targets for focus fire, flank pairing (complementary squares), Aid Another and waking sleeping allies (11.8.1), protecting casters and healers, spreading against known area casters.
- **Morale.** A data-driven flee/surrender policy per profile (read `FleeHealthThreshold`, `Aggression`), leader-loss checks, intelligent undead never fleeing. It would consume the `Retreat` result that `SelectBestAction` already produces, with the HP test replaced by the policy and called from every routine, not only DefensiveMelee. That is also the intended fix for AI-027: act on the `Retreat` result instead of reducing `SelectBestAction` to a charge test as the issue's suggested fix proposes. It first needs a way off the map and a defeated state that victory and XP honour (11.8.5).
- **Perception per RAW.** Opposed Listen/Move Silently, Spot/Hide, senses (scent, blindsense, tremorsense) and line of sight (GRID-009), so AI information matches what the creature could know (fixes AI-051).
- **Intelligence tiers.** MM Intelligence scores could select how much of the option space a creature considers (mindless: nearest target, no maneuvers; animal: instinctive charges and grabs; intelligent: full evaluation and team play), which also gives the unused difficulty setting (AI-012) a natural meaning.
- **Encounter-level behaviour.** DMG ch.3 random encounters bring surprise and encounter distance; the AI currently has no notion of surprise, ambush or retreat off the map (CMB-028), which matters for the encounter-driven game loop.

Each option depends on the prerequisites above; the cheapest high-value sequence is probably rules parity first, then the decision trace and target filter, then the unified evaluator for melee options, then spells and areas, then team play.
