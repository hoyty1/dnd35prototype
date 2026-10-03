> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-02.

# Combat & grid

`Assets/Scripts/Combat/` mostly holds data types, static helpers and `GameManager` partial classes. The rules math is elsewhere: to-hit, crit and damage are in `CharacterController`, and mitigation and HP are in `CharacterStats`. Player and NPC turns are driven by `GameManager` and the services on its GameObject.

| Concern | Where to look |
|---|---|
| Grid, cells, pathfinding | `Grid/SquareGrid.cs`, `Grid/SquareGridUtils.cs`, `Services/MovementService.cs` |
| Initiative, rounds, turns | `Services/TurnService.cs`; `_Core/GameManager.cs` (`OnTurnStarted`, `OnNewRound`, `StartPCTurn`, `NextInitiativeTurn`) |
| Player input to combat | `_Core/GameManager.CombatActions.cs` (`OnCellClicked` and the `Handle*Click` methods) |
| Player attack orchestration | `Services/CombatFlowService.cs`, which reaches GameManager state through `_Core/GameManager.CombatFlowAccessors.cs` (`Combat_*`) |
| NPC attack orchestration | `_Core/GameManager.NPCTurns.cs` (`NPCPerformAttack`), driven by `AIService` |
| Attack and maneuver math | `Character/Controller/CharacterController.cs` (`Attack`, `FullAttack`, `PerformSingleAttackWithCrit`, `ExecuteSpecialAttack`, `Resolve*`) |
| Mitigation, HP, condition sums | `Character/Stats/CharacterStats.cs` (`ApplyIncomingDamage`, `TakeDamage`, `Condition*` properties) |
| AoOs | `Combat/Core/ThreatSystem.cs` (static) |
| Conditions | `Combat/StatusEffects/StatusEffect.cs` (data), `Combat/Conditions/ConditionManager.cs`, `Services/ConditionService.cs` |
| Maneuver flow and UI | GameManager partials in `Combat/Maneuvers/*.cs` and `Combat/Special/*.cs` |

## Grid

The map is a square grid. The hex grid was removed in f5e29ed (2026-04-09), so any remaining "hex" wording in comments or `RangeCalculator` aliases is stale. `SceneBootstrap.CreateSquareGrid` adds a `SquareGrid` component at runtime, and `GameManager.Start` calls `Grid.GenerateGrid()`. The default size is 20x20 (`SquareGrid.Width/Height`). Prefer `GameManager.Grid` over `SquareGrid.Instance`, because every `SquareGrid.Awake` overwrites `Instance` and tests create extra grids.

**Coordinates.** Positions are `Vector2Int`. (0,0) is the bottom-left cell, +x is East and +y is North (`SquareGridUtils.Directions`). Cell (x,y) is at world (x, y, 0) because `SquareGridUtils.CellSize = 1`. One square is 5 ft (`RangeCalculator.FeetPerSquare`). Clicks reach cells through each cell's `BoxCollider2D`.

**Footprints.** A creature's `GridPosition` is the bottom-left square of its footprint. The footprint extends +x/+y by `SizeCategory.GetSpaceWidthSquares`: Fine through Medium 1, Large 2, Huge 3, Gargantuan 4, Colossal 6. Tiny and smaller creatures never share squares. Use `CharacterController.GetOccupiedSquares()` / `SquareGrid.GetOccupiedSquares(base, size)` and `SquareGrid.GetCenteredWorldPosition` rather than assuming one square. A cell can hold several occupants (`SquareCell.Occupants`) for grapples and swarms. Dead occupants do not block `CanPlaceCreature`.

**Distance.** Choose the function by what the distance means:

| Function | Rule | Used for |
|---|---|---|
| `SquareGridUtils.GetDistance` | 3.5e 1-2-1 diagonals (odd diagonals cost 1, even cost 2) | movement range, spell/AoE range, ranged weapon range (`CharacterController.IsTargetInWeaponRange` with `chebyshev:false`) |
| `SquareGridUtils.GetChebyshevDistance` (alias `ChebyshevDistance`) | diagonal = 1 | melee reach and threat (`ThreatSystem.GetThreatenedSquares`, `CharacterController.CalculateDistance`) |
| `SquareGridUtils.CalculatePathCost` | 1-2-1 with diagonal parity carried across a whole path | pricing an actual path |

Distances between creatures use the closest pair of footprint squares (`CharacterController.GetMinimumDistanceToTargetSquares`). Chebyshev reach matches the 10-ft reach diagonal exception but overstates 15-ft+ reach at the corners.

**Pathfinding.** `SquareGrid.FindPathAoOAware` is a weighted A* that returns an `AoOPathResult`, whose `Path` excludes the start square; an empty path means no move. It passes through allies and, when the caller allows it, through enemies (overrun, swarms). It is blocked by Wall of Ice and Wall of Force cells, diagonal wall corners and Resilient Sphere boundaries. A path that costs too much is trimmed from the end. If A* finds nothing, it falls back to `ThreatSystem.GenerateSimplePath`, cut at the first invalid step and trimmed to `maxRange` by straight-line distance, so the mover stops short instead of getting a "no path" error. `SquareGrid.FindSafePath` unions the threat squares of every living creature with `Team != mover.Team`, runs the A* with `mover.Stats.MoveRange` (no range override, no pass-through flags) and then `ThreatSystem.AnalyzePathForAoOs`. `MovementService` wraps all of this.

There are three movement-cost models, and they disagree:

| Model | Where | Diagonal cost |
|---|---|---|
| A* selection weights | `SquareGrid.FindPathAoOAware` (`ORTH_COST=2`, `DIAG_COST=3`, `THREAT_COST=200` for leaving a threatened square) | weighted only to choose a path |
| D&D path cost | `SquareGridUtils.CalculatePathCost`, used to trim the A* result | 1, 2, 1, 2... |
| Execution budget | `GameManager.ExecuteMovement` (in `_Core/GameManager.CombatActions.cs`; `stepCost = 1 + GetGreaseAreaExtraMovementCost`) | 1 |

Difficult terrain doubles cost only in `MovementService.GetMovementCost`, which nothing live calls (its `GameManager.GetMovementCost` wrapper has no callers either). Six area effects write difficult terrain through `GameManager.SetAreaDifficultTerrain` (Grease, Entangle, Web, Plant Growth, Soften Earth, Spike Stones), and only two places read it: 5-ft step validation (`MovementService.IsValidAdjacentStepDestination`) and charge path validation (`SupportActions.cs`, `ContainsChargeBlockingTerrain`). The set is a plain `HashSet`, so when one overlapping effect ends it clears cells another effect still covers. Movement highlighting (`MovementService.CalculateMovementRange`) uses straight-line range, not reachability. A* pruning uses straight-line distance from the start, and the A* threat penalty checks only the mover's base square. For Withdraw, the click path loses the Withdraw range and first-step suppression because `MovementService.FindPath(avoidThreats:true)` calls `SquareGrid.FindSafePath`, which ignores both. The hover preview does apply them, so the preview and the actual move can differ. These are tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md).

**Line of sight and line of effect.** There is no geometric line of sight. `CharacterController.CanSee(target)` is true unless the target is dead or has 50%+ miss chance against the observer (`HasTotalConcealment`). The wall-specific line-of-sight helpers (`WallOfIceAreaEffect.DoesCellBlockLineOfSight` and similar) have no callers. Line of effect (`LineOfEffectService`) is applied only to area spells (see [line of effect](spells.md#persistent-areas-walls-emanations-holy-areas-line-of-effect)), so walls do not block melee, ranged or single-target spells. Only Resilient Sphere blocks attacks, through `ResilientSphereAreaEffect.DoesSphereBlockInteraction` checks in `CombatFlowService.PerformPlayerAttack` and `ThreatSystem`. In addition, `AIService.ExecuteNPCTurn` ends the turn of an NPC inside a sphere, and `GameManager.SpellCasting.cs` blocks non-self spells from a caster inside one. `GameManager.NPCPerformAttack` has no sphere check, so an NPC outside a sphere attacking a creature inside it is not blocked (inferred from code, not verified in Play mode). The comment in `CharacterStats.ApplyIncomingDamage` about a sphere "safety net" has no code behind it.

## Turn structure

**Initiative.** `TurnService` owns the order. `TurnService.StartCombat` creates one `InitiativeEntry` (d20 + `Stats.InitiativeModifier`) per combatant that is not dead, sorts the entries (`SortInitiativeOrder`: total, then modifier, then a coin flip inside the comparator), applies forced-first test ordering, sets round 1, fires `OnNewRound(1)`, then starts the first turn. Summons join mid-combat via `AddToInitiative`. There is no surprise round, and `FlatFooted` is never applied (it is only checked). `InitiativeSystem` and `CombatStateMachine` are dormant (see [Dormant and unused infrastructure](../ARCHITECTURE.md#dormant-and-unused-infrastructure)); the live phase state is `GameManager.CurrentPhase` (`TurnPhase`) and `CurrentSubPhase` (`PlayerSubPhase`).

**Turn advancement is synchronous and re-entrant.** No coroutine sits between turns:

```
EndCurrentTurn -> EndActivePCTurn (PC) | NextInitiativeTurn (NPC)
NextInitiativeTurn: True Strike expiry, pinned duration, ConditionService.OnTurnEnd,
                    ProcessEndOfTurnHPState, TurnService.EndTurn
  TurnService.AdvanceToNextTurn -> StartTurnAtCurrentIndex
    index wraps -> round++ -> OnNewRound -> GameManager.OnNewRound (round-start ticks)
    OnTurnStarted(next):
      1. GameManager.OnTurnStarted -> TickDomainPowerDurations, then
           StartPCTurn(next)                              (IsPC = IsControllable)
           or StartCoroutine(SingleNPCTurnFromInitiative) (runs synchronously to its first yield)
      2. ConditionService.OnTurnStartedFromTurnService(next)
  A creature that cannot act (ShouldSkipTurnDueToHPState) calls NextInitiativeTurn again.
```

A chain of helpless combatants recurses on the call stack, and code in these callbacks runs inside the previous turn's call. Because GameManager subscribes first (`GameManager.Awake`, `_turnService.OnTurnStarted += OnTurnStarted`), its handler runs before ConditionService's. `ConditionService.OnTurnStart` runs twice per turn: once from `StartPCTurn` or `BeginNPCTurnForAI`, and once from its own subscription. This is harmless today: turn-boundary expiry and the negative-level refresh are idempotent, and the turn-start escape save (`ConditionEscapeCheckData`, which would roll twice) is never instantiated outside tests.

**Where things happen:**

| Moment | Code | What runs |
|---|---|---|
| Round start (global, round 1 included) | `GameManager.OnNewRound` | `TickAllSpellDurations` (spell effects), `_conditionService.OnRoundEnd()` (condition durations, despite the name), summons, emanations, grease, turn-undead trackers, quickened-spell and damage-mode resets, daily effects every 14,400 rounds (spell-side order in [Durations and ticking](spells.md#durations-and-ticking)) |
| PC turn start | `GameManager.StartPCTurn` | `ConditionService.OnTurnStart`, Flaming Sphere, Acid Arrow, regeneration, Aid Another expiry, HP-state skip check, `CharacterController.StartNewTurn` |
| NPC turn start | `SingleNPCTurnFromInitiative`, then `AIService.ExecuteNPCTurn`, which calls back `GameManager.BeginNPCTurnForAI` | Aid Another expiry, Flaming Sphere and the HP-state skip check first; then, in `BeginNPCTurnForAI`, `ConditionService.OnTurnStart`, Acid Arrow, regeneration, `npc.StartNewTurn` and perception. The order differs from the PC path (`OnTurnStart` runs after the skip check) |
| Turn end | `GameManager.NextInitiativeTurn` | `ConditionService.OnTurnEnd`, dying/death processing |

Condition durations tick at the global round boundary, not per creature. `StatusEffect.Tick` returns expired when `RemainingRounds` reaches 0, so a 1-round condition applied to a creature that has already acted expires before that creature loses a turn. Only conditions applied with `expiresAtStartOfTurn`/`expiresAtEndOfTurn` metadata (for example `ChargePenalty` in `SupportActions.cs`, `ApplyChargePenaltyUntilStartOfNextTurn`) are turn-relative. See [KNOWN_ISSUES.md](../KNOWN_ISSUES.md).

**Action economy.** `Combat/Core/ActionEconomy.cs` is a flag bag owned by `CharacterController.Actions`. `CharacterController.StartNewTurn` resets it, along with AoO counters (`ThreatSystem.ResetAoOForTurn`) and per-turn maneuver pools. `HasFullRoundAction` requires that no other action was spent. `ConvertStandardToMove` gives a second move. `SingleActionOnly` is set for Disabled/Staggered HP states or `Stats.IsSingleActionsOnly`. The 5-ft step is `CharacterController.FiveFootStep` / `MovementService.IsValidAdjacentStepDestination`. Withdraw spends the full-round action in `ExecuteMovement`. Swift actions are effectively unmodelled: `SwiftActionUsed` is set only by Dimension Door, and quickened spells use a separate per-round flag (`SpellcastingComponent.HasCastQuickenedSpellThisRound`). There are no readied actions (except readied counterspell) and no delay. `ActionEconomy.HasMoved5Ft` is read only by the dead `_Core/Commands` classes (see [Dormant and unused infrastructure](../ARCHITECTURE.md#dormant-and-unused-infrastructure)).

## Attack resolution

**Where the math lives.** Every weapon attack ends in `CharacterController.PerformSingleAttackWithCrit` (about 1,000 lines). It applies the target-side adjustments (helpless +4, invisibility, feint window, concealment, Mirror Image, Deflect Arrows), rolls with `CharacterStats.RollToHitWithMod` (natural 20 hits, natural 1 misses), confirms crits, rolls damage, adds sneak attack and enchantment dice, and calls `ApplyIncomingDamage`. It is reached from three main builders that each assemble their own modifier sum (the grapple attack resolvers `ResolveUseOpponentWeapon` and `ResolveAttackWhileGrappling` also call it):

| Builder | Used by | Notes |
|---|---|---|
| `CharacterController.Attack` | single attacks, iterative sequence steps, AoOs, off-hand attacks, charges | Uses `AttackCalculator.CalculateAllFeatModifiers`; includes morale; DEX for ranged |
| `CharacterController.FullAttack` | Full Attack button (one call per attack, `maxAttacks:1`), single natural-attack steps in `CombatFlowService` (`maxAttacks:1`), NPC full attacks, pounce | Bonuses come from `CharacterStats.GetIterativeAttackBonuses` (BAB-5k + STR + size), plus Rapid Shot and Haste extras and the natural-attack sequence |
| `CharacterController.DualWieldAttack` | `CombatFlowService.PerformDualWieldAttack` only | Correct per-weapon crit and half-STR off-hand, but unreachable in play: `GameManager.OnDualWieldButtonPressed` has no caller, and `CombatUI.DualWieldButton` is always hidden |

`FlurryOfBlows` is a fourth entry point, called from `CombatFlowService.PerformFlurryOfBlows`.

**Orchestration.** Player attacks and NPC attacks use different pipelines, so a feature added to one is often missing from the other (Cleave, ammunition checks and thrown-weapon drop exist only on the player side; NPCs still get natural attacks, because `FullAttack` and `Attack` handle them inside `CharacterController`).

```
Player: button -> PendingAttackMode -> OnCellClicked -> HandleAttackTargetClick
        -> CombatFlowService.PerformPlayerAttack (Resilient Sphere block, Sanctuary break,
           flanking +2, RangeInfo)
           Single in sequence -> PerformIterativeSequenceAttack -> CharacterController.Attack
           Single             -> PerformSingleAttack (Cleave, free trip) -> Attack
           FullAttack         -> GameManager.PerformFullAttackWithRetargetingAndFiveFootStep -> FullAttack
           FlurryOfBlows      -> PerformFlurryOfBlows -> FlurryOfBlows
        -> log, MeleeReactionService.TriggerReactions, victory check, AfterAttackDelay
NPC:    AIService -> GameManager.NPCPerformAttack -> npc.FullAttack | npc.Attack
        | PerformNPCFullAttackWithAdaptiveRetargeting (no Cleave)
```

**Shared iterative sequence and off-hand lane.** GameManager keeps one per-turn sequence that is shared by weapon attacks (melee and thrown) and main-hand maneuvers. The state is in `_Core/GameManager.cs`: `_isInAttackSequence`, `_attackingCharacter`, `_equippedWeapon`, `_totalAttackBudget`, `_totalAttacksUsed` (the position in the sequence; there is no separate index), `_currentAttackBAB` and `_attackSequenceConsumesFullRound`. The BAB ladder is `CharacterCombatStats.GetAttackBonuses` (BAB, -5 at 6+, -10 at 11+, -15 at 16+). Haste appends one extra attack at the highest BAB. For innate natural attacks, the budget is `Stats.GetTotalNaturalAttackCount()`.

- Weapon attacks: `GameManager.StartAttackSequence` commits the Standard action (or the Move action, marking the sequence full-round, when an off-hand attack already used the Standard action) and sets the budget. `CombatFlowService.PerformIterativeSequenceAttack` increments `_totalAttacksUsed` through `Combat_SetTotalAttacksUsed`, then sets the next BAB through `GetSequenceAttackBaseBonus`, plus the main-hand dual-wield penalty for melee and thrown. Grepping GameManager for the increment misses it.
- Progressive full attack (house rule, labelled "Progressive house-rule attack tracking" at `_Core/GameManager.cs:384`): the first weapon attack costs only the Standard action. Before the second, `GameManager.TryEnterProgressiveFullAttackStage` spends the Move action and marks the sequence as full-round. A separate Full Attack button spends the full round up front and allows retargeting and a 5-ft step between attacks.
- Maneuvers: Disarm, Sunder, Trip, Bull Rush and Grapple each use one iterative slot through `TryStartMainHandSpecialManeuverSequence` / `AdvanceMainHandSequenceAfterSpecialManeuverUse` (`Combat/Maneuvers/StandardManeuvers.cs`). A maneuver that opens a sequence with a full round available spends the full round immediately and gets the whole budget; with only a Standard action, the budget is 1. A maneuver used after a weapon attack never spends the Move action (likely bug, inferred from code; see [KNOWN_ISSUES.md](../KNOWN_ISSUES.md)).
- Off-hand lane: `_offHandAttackAvailableThisTurn`, `_offHandAttackUsedThisTurn`, `_currentOffHandBAB` (= `BaseAttackBonus + _offHandPenalty`) and `_currentOffHandWeapon`. It is independent of the main-hand budget and adds at most one attack. Dual-wield state (`_dualWieldingChoiceMade`, `_isDualWielding`, `_mainHandPenalty`, `_offHandPenalty`) comes from the dual-wield prompt (`ShowDualWieldingPrompt`, `CalculateDualWieldingPenalties`), and `StartPCTurn` resets it. Off-hand Disarm and Sunder need a "Yes" to that prompt. The player picks the hand from the menu, and nothing falls back automatically. `CanUseOffHandAttackOption` only allows `ActivePC`, so NPCs never use this lane.
- Remaining attempts: inside an owned sequence, budget minus used. Outside one, the iterative count if a full round is available, 1 if only a Standard action is, otherwise 0.
- `CharacterController` also has legacy per-maneuver counters (`TryConsumeIterativeDisarmAttackAction` and others). Only tests use them; the runtime flow uses the GameManager pools.

**Crits.** The threat range and multiplier come from `Stats.CritThreatMin`/`CritMultiplier`, which `Inventory.ApplyWeaponStats` syncs from the main weapon only. Confirmation uses the same attack modifier. A melee attack against an adjacent paralyzed target auto-crits. Only the weapon dice are multiplied; STR, enhancement and feat bonuses are added once (`PerformSingleAttackWithCrit`, around `CharacterController.cs:6933`). This is a rules deviation, not a house rule (RAW multiplies the static bonuses too), tracked as [CMB-004](../issues/CMB.md); the code comments that present it as the 3.5 rule are CMB-057. Coup de grace instead multiplies base damage and sneak attack by the crit multiplier, which is also part of CMB-004. Improved Critical is not weapon-specific and stacks with Keen.

**Damage pipeline.** A weapon hit builds a `DamagePacket` (`Combat/Core/DamageModel.cs`) with the weapon's physical types and bypass tags, plus Ranged, enchantment alignment and Magic Stone tags. It then calls `target.Stats.ApplyIncomingDamage(amount, packet)`:

```
swarm weapon immunity -> typed immunity -> Protection from Energy pool
-> max(typed resistance, Resist Energy) -> Fire Shield 50% (0 with save-for-half)
-> best non-bypassed DR (weapon/natural only; Protection from Arrows and Stoneskin pools)
-> petrified hardness -> ApplyNonlethalDamage | TakeDamage
TakeDamage: Shield Other split -> temp HP -> HP floor -10 -> regeneration death prevention
            (RegenerationEffect.CheckDeathPrevention)
```

DR bypass is OR-only (`DamageReductionEntry.BypassAnyTag`), so "DR 10/silver and magic" cannot be expressed. Enchantment energy damage (Flaming, Frost, Holy...) and sneak attack go into the same packet, typed only with the weapon's physical types. Fire resistance therefore never reduces Flaming damage, while DR reduces the whole total. Many sources call `Stats.TakeDamage` directly and skip all mitigation: Fire Shield retribution, turn-undead destroy, Spirited Charge extra damage (unreachable, see [Mounted combat](#mounted-combat)), the Snatch Arrows counter-attack, Vicious backlash to the wielder, Vorpal and a number of spell resolvers. About 40 non-test `.TakeDamage(` call sites exist, against about 11 for `ApplyIncomingDamage`, so grep before assuming mitigation applies.

**Other rules deviations, in one line each** (full list in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md)): `FullAttack` uses STR for ranged attacks and drops `MoraleAttackBonus` (Bless, charge and pounce +2). Off-hand attacks through `Attack(isOffHandAttack:true)` get full STR and main-hand crit stats. The `Flanked` condition (-2 AC against everyone, refreshed by `ConditionService.RefreshFlankedConditions`) stacks with the flanker's +2 attack, an effective +4. Cleave exists only in `CombatFlowService.PerformSingleAttack`, and its follow-up target must be adjacent to the attacker's base square (`FindAdjacentEnemy`), not within reach. Player charges (`ExecuteCharge`) pass `isFlanking:false`; NPC charges compute flanking.

## Attacks of opportunity

`Combat/Core/ThreatSystem.cs` is static:

- `GetThreatenedSquares`: Chebyshev rings from every footprint square between `GetMeleeMinAttackDistance` and `GetMeleeMaxAttackDistance`, so reach weapons have a dead zone. Creatures with no melee weapon and creatures with a `PreventsThreatening` condition threaten nothing. Natural and unarmed attacks threaten.
- `AnalyzePathForAoOs`: leaving any square of the previous footprint that an enemy threatens provokes, up to that enemy's remaining AoOs. Swarm movers never provoke. `suppressFirstSquareAoO` implements Withdraw. Each `AoOThreatInfo` carries a `PathIndex`.
- `ExecuteAoO`: Resilient Sphere block, total concealment, `UseAoO`, then the Spring Attack suppression check (which runs after the AoO is consumed), Mobility as -4 to the attacker, best melee weapon, `threatener.Attack(...)`, a free trip follow-up for `HasTripAttack` creatures, and `MeleeReactionService.TriggerReactions` on a hit.
- The cap comes from `FeatManager.GetMaxAoOPerRound` (Combat Reflexes: 1 + DEX). It resets in each creature's own `StartNewTurn`.

Provoking actions: movement (confirmation prompt with `AoOProvokingAction.Movement`, then `ExecuteMovement` resolves the AoOs for each step after the mover has entered the square), casting (prompt with cast defensively), drinking a potion, retrieving an item, standing from prone, crawling, a ranged or thrown attack while threatened (automatic, no prompt: `CombatFlowService.ResolveRangedAttackAoOIfProvoked`, duplicated for NPCs in `NPCTurns`), charge paths, and these maneuvers: Grapple and Sunder (target only, unless Improved Grapple/Sunder) and Coup de Grace (all threateners), in `GameManager.ExecuteSpecialAttack` (in `_Core/GameManager.CombatActions.cs`). Trip, Disarm and Bull Rush never provoke. Overrun provokes from the target unless the attacker has Improved Overrun. There is no Tumble.

Known issues: `ThreatSystem` (and `SquareGrid.FindSafePath`) skip allies with `character.Team == mover.Team` instead of `TeamUtility.IsEnemy`, so `CharacterTeam.Neutral` creatures would threaten both sides. This is latent: no production code assigns `CharacterTeam.Neutral`. AoO counters are reset only in each creature's own `StartNewTurn`, so an AoO spent late in one combat still counts until that creature's first turn in the next. Because a movement AoO resolves after the step, `Attack()` re-checks reach from the new square and may record an unrolled miss (not verified in Play mode).

## Conditions and status effects

Two separate systems share similar names:

| System | Instance type | Storage | Ticked by |
|---|---|---|---|
| Conditions (Prone, Stunned, Grappled, EnergyDrained...) | `StatusEffect` (`Combat/StatusEffects/StatusEffect.cs`). Despite the name, it is a condition instance: `Type`, `SourceName`, `RemainingRounds` (-1 = indefinite) | `CharacterStats.ActiveConditions` (`List<StatusEffect>`) | `ConditionService` at round start |
| Spell buffs and debuffs (Bless, Haste, Stoneskin...) | `ActiveSpellEffect` (`Spell/StatusEffects/ActiveSpellEffect.cs`) | per-character `StatusEffectManager` (`Spell/Components/StatusEffectManager.cs`) with bonus-type stacking | `GameManager.TickAllSpellDurations` (`GameManager.SpellCasting.cs`) |

A spell that imposes a condition has two durations that can drift apart.

**Apply chain (normal path):**

```
CharacterController.ApplyCondition(type, rounds, sourceName)    GameManager.ApplyCondition(target, ..., data,
  -> CharacterConditions.ApplyCondition                            expiresAtStart/EndOfTurn, sourceCategory)
  -> ConditionService.ApplyCondition  <--------------------------- (no-op if _conditionService is null)
       -> CharacterController.ApplyConditionDirect
            -> ConditionManager.ApplyCondition -> CharacterStats.ActiveConditions
            -> StatusTagManager.UpdateStatusEffectTags, SpellcastingComponent.ApplyNegativeLevelSlotLoss
       -> SyncCharacter + metadata (Source, SourceCategory, SourceId, Data payload, ExpiresAt*)
```

`ConditionService` is not the store of record. It mirrors `ActiveConditions` with metadata and typed payloads (`Combat/Conditions/*ConditionData.cs`, for example `CharmedConditionData`). Use `GameManager.ApplyCondition` when you need a payload or turn-boundary expiry. Recipe: "Add a condition" in [DEVELOPMENT_RECIPES.md](../DEVELOPMENT_RECIPES.md). Expiry fires `ConditionService.OnConditionExpired`, which calls `GameManager.HandleConditionExpired`; that method special-cases Turned, Asleep, Color Spray, Animate Rope, Web and Frightened.

**Two ApplyCondition implementations.** `ConditionManager.ApplyCondition` is what runs in game, because `CharacterController` always adds a `ConditionManager`. It links Helpless for both Paralyzed and Unconscious, stacks `EnergyDrained`, and runs the `OnConditionApplied` hooks: Stunned drops held items, Petrified gets a grey tint, and the others only log. `CharacterStats.ApplyCondition` (`CharacterStats.cs:1407`) is the legacy fallback. It has the Fatigued-to-Exhausted escalation that `ConditionManager` lacks, and it links Helpless only for Paralyzed. Direct `target.Stats.ApplyCondition` callers (`GameManager_Spells_Phase1.cs:1151`, `GameManager_Spells_Phase2.cs:170/233/546`, `WishExecutor.cs:583`) bypass the whole stack, and tests also call it directly. When changing condition semantics, change both implementations.

**Rules data.** `ConditionRules` (in `Combat/StatusEffects/StatusEffect.cs`) holds 49 `ConditionDefinition`s, including a `None` placeholder, plus the aliases KnockedDown->Prone and Grappling->Grappled (`ConditionRules.Normalize`). A definition carries attack/AC/save/skill/initiative modifiers, a movement multiplier, `Prevents*` flags, `DeniesDexToAc` and `CoupDeGraceVulnerable`. Effects are computed on read: `CharacterStats.ConditionAttackPenalty`, `ConditionACPenalty` and the save, initiative and movement sums add up every active definition through `SumConditionValue` (`ConditionAttackPenalty` and `ConditionReflexModifier` also fold in Haste and Slow). `ThreatSystem` and `CharacterConditions.CanAttack/CanMove` read the flags. Some numbers are not in the table: the +4 melee bonus against helpless targets is applied in `PerformSingleAttackWithCrit`. The hard-coded modifiers in `CharacterConditions.GetCondition*Modifier` are dead. Any HP-state change removes Unconscious, Disabled, Staggered, Dying and Stable whatever their source (`CharacterController.UpdateConditionsForHPState`).

**Poison and disease to condition.** `PoisonDatabase` / `DiseaseDatabase` entries can carry `PoisonSpecialEffect`s. `CharacterController.ApplyPoison` (immunity check, then a Fort save via `DiceService.D20`) leads to `ApplySpecialEffectList` / `ApplySpecialEffect`. That calls `PoisonSpecialEffect.RollDurationInRounds()` (hour = 600 rounds, minute = 10, round = 1, "permanent" = -1) and `ToConditionType()` (Paralysis->Paralyzed, Unconsciousness->Unconscious, Nausea->Nauseated, Confusion->Confused, Blindness->Blinded, Deafness->Deafened, Exhaustion->Exhausted, Petrification->Petrified), then `GameManager.ApplyCondition(..., sourceCategory: "Poison")`. Death sets HP to -10 without a condition. Only two poisons carry special effects, no disease populates them, and nothing references those two poisons. Poison secondary saves run on real time (`GameManager.UpdatePoisonTimers`, `Time.deltaTime`), not rounds. Creature on-hit paralysis is a separate path (`NaturalAttackDefinition.ParalysisOnHitDC`, around `CharacterController.cs:4643`).

## Maneuvers

The `GrappleSystem`, `OverrunSystem`, `StandardManeuvers`, `SupportActions` and `TurnUndeadSystem` components are empty shells (see [Dormant and unused infrastructure](../ARCHITECTURE.md#dormant-and-unused-infrastructure)); they have been empty since commit 365b5d2 created them. All the logic is in the `public partial class GameManager` block in the same file; add new maneuver code there (recipe: "Add a combat maneuver" in [DEVELOPMENT_RECIPES.md](../DEVELOPMENT_RECIPES.md)). The player opens the Special Attack menu (`CombatUI`), which calls `OnSpecialAttackSelected`, then `ShowSpecialAttackTargets`, `HandleSpecialAttackTargetClick` and `GameManager.ExecuteSpecialAttack` (in `_Core/GameManager.CombatActions.cs`; action cost and AoOs). That calls `CharacterController.ExecuteSpecialAttack`, which dispatches to `Resolve*`.

| Maneuver | Flow / UI (GameManager partial) | Resolution (`CharacterController`) | Action cost |
|---|---|---|---|
| Trip | `StandardManeuvers.cs` pools | `ResolveTrip` (Prone, indefinite) | one iterative slot, else Standard |
| Disarm / Sunder | `StandardManeuvers.cs` (main/off-hand prompts, `TryConsumeDisarm/SunderAttackAction`) | `ResolveDisarm`, `ResolveSunder` | one iterative slot, else Standard |
| Bull Rush | `StandardManeuvers.cs` (`ResolveBullRushPushAndFollow`); charge variant in `SupportActions.cs` | `ResolveBullRush` | one iterative slot, else Standard |
| Grapple | `GrappleSystem.cs` | `ResolveGrapple` (touch attack, then opposed checks; `GrappleLink` in a static registry); `ResolveImprovedGrabFreeAttempt` | one iterative slot, else Standard |
| Overrun | `OverrunSystem.cs`: destination-based movement through enemies (`ResolveOverrunOpposedCheck`, its own formula and size table) and target-based (`ResolveOverrunSpecialAttack`) | `ResolveOverrunAttempt` / `ResolveOverrun` | Standard |
| Feint | `GameManager.TryConsumeFeintAction` (in `Combat/Maneuvers/StandardManeuvers.cs`) | `ResolveFeint` (registers a feint window that denies Dex) | Standard, or Move with Improved Feint |
| Coup de grace | eligibility in `StandardManeuvers.cs` | `ResolveCoupDeGrace` (Fort DC 10 + damage) | Full round |
| Charge | `SupportActions.cs` (`OnChargeButtonPressed`, `ExecuteCharge`, NPC charge) | `Attack` or `FullAttack` (pounce, rake, improved grab) with +2 injected as `MoraleAttackBonus` in try/finally | Full round; `ChargePenalty` until the creature's next turn |
| Aid Another | `SupportActions.cs` (`ExecuteAidAnother`, wake sleeping ally) | bonuses consumed by `Attack`/`FullAttack` (`ConsumeAidAnother*`) | Standard |

**Grapple actions.** Once a PC is grappling, `ActionButtonPanel.UpdateActionButtons` hides the normal action buttons (`HideNonGrappleActionButtons`) and shows direct "Grapple: ..." buttons (damage, attack with a light weapon, attack unarmed, pin, break pin, Escape Artist, escape check, move, use opponent's weapon, disarm small object, release pin), wired in `SceneBootstrap.WireButtons` to `GameManager.OnGrapple*ButtonPressed`. Those handlers call `GameManager.TryHandleDirectGrappleAction`, which validates the grapple state, pin restrictions and the shared grapple attack pool, opens a small `ShowSpecialStyleSelectionMenu` sub-prompt for damage mode (`GrappleDamageModeMenu`) or opponent-weapon hand (`GrappleUseWeaponMenu`), and then calls `GameManager.ExecuteGrappleAction` and `CharacterController.ResolveGrappleAction`. All of these are GameManager partial methods in `Combat/Maneuvers/GrappleSystem.cs`. `GameManager.RedirectPinnedCharacterToGrappleMenu` (same file), called by movement, withdraw, 5-ft step, special attack, charge, Aid Another, overrun, smite, item use and reload handlers, does not open a menu despite its name: if the PC is pinned it logs a warning and calls `ShowActionChoices`, which leaves only the escape buttons. The full grapple action menu, `GameManager.ShowGrappleActionMenu` (public overload at `GrappleSystem.cs:64`, private builder at `:1011`), renders through `CombatUI.ShowSpecialStyleSelectionMenu(menuName: "GrappleActionMenu", ...)` and is the only place that offers "Draw a Light Weapon" and "Retrieve a Spell Component" (both labelled not implemented); its only caller is `CombatUI.OnGrappleActionsClicked`, which nothing calls, because the legacy "Grapple Actions" button is stripped of listeners and hidden ([UI-028, UI-039](../issues/UI.md)). "Disarm Small Object" is a stub ([CMB-033](../issues/CMB.md)). Casting while grappled is checked in the cast path (`GameManager.ResolveGrappledOrPinnedCastingConcentration`, called from `Spell/Resolution/GameManager.SpellCasting.cs`). NPCs use `GameManager.AI_GrappleRestrictedTurn`.

Formula deviations (Trip uses the attack size modifier and gives the defender +4 for Improved Trip, ties go different ways, bull rush adds BAB, two overrun formulas) are tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md).

## Reactions, turn undead, negative levels, template smite

- **Melee reactions.** `Combat/Reactions/MeleeReactionService.cs` is a static registry of `IMeleeReactionEffect`. The attack pipelines call `TriggerReactions(attacker, defender, result)` after a melee hit: `CombatFlowService`, `NPCTurns`, `ThreatSystem.ExecuteAoO`, charge and full attack. Its only implementation is `FireShieldReactionEffect`: 1d6 + CL (max 15) through raw `Stats.TakeDamage`, so a fire-immune attacker still burns. A new instance is registered on every Fire Shield cast (`GameManager_Spells_F.cs`). `Unregister` has no callers, and the registry is cleared only by `ClearAll` in `GameManager.OnCombatEnded` and `RestorePartyAfterCombat`. Because `FireShieldReactionEffect.IsActiveOn` only checks `Stats.FireShieldActive`, recasting Fire Shield on the same creature in one combat makes every registered instance fire. Template Smite and the turn-undead destroy never trigger reactions.
- **Turn Undead.** `Combat/Special/TurnUndeadSystem.cs` (GameManager partial): `CanUseTurnUndead` (Cleric, or Paladin level 4+) and `ExecuteTurnUndead` (turning check d20 + CHA sets the maximum HD affected via `ComputeTurnUndeadMaxHitDice`, HD pool 2d6 + effective level + CHA, enemy undead within 12 squares). There is a selection panel when the pool is short. Undead are destroyed (`TakeDamage`) if level >= 2x HD; otherwise they are Turned for 10 rounds, with fear-break tracking. The effective level is total character level (Paladin: level - 3), plus 1 for Improved Turning, and undead HD is read from `Stats.Level`. Turn resistance and rebuke are not implemented.
- **Negative levels.** Stacks of the `EnergyDrained` condition. `NegativeLevelSystem` is a static facade over `CharacterController.ApplyNegativeLevels/RemoveNegativeLevels` and `CharacterStats.EnforceNegativeLevelDeathThreshold` (death when negative levels >= HD). `ConditionService.OnTurnStart` refreshes the state and `SpellcastingComponent.ApplyNegativeLevelSlotLoss`.
- **Template Smite.** `Combat/Special/TemplateSmiteSystem.cs` (GameManager partial, no shell): template smite (`HasTemplateSmiteEvil`/`HasTemplateSmiteGood`) through `PendingAttackMode.TemplateSmite`, limited by `Stats.TemplateSmiteUsed`, which `RestorePartyAfterCombat` clears. It adds +CHA (minimum 0) attack and +HD damage by temporarily mutating `MoraleAttackBonus`/`MoraleDamageBonus` around `Attack()`.

## Mounted combat

`Combat/Mounts/` (`MountSystem`, `MountedCombatSystem`, `MountDatabase`, `MountData`) is a complete rules library with a test suite (`Tests/Mounts/MountSystemTests.cs`). It is unreachable in play: `MountSystem.TryMount`/`CreateMount` have no non-test callers, so `MountSystem.IsMounted` is always false. The production references (`GetMountedRangedPenalty` and `GetMountedACBonus` in `CharacterController`, `ProcessMountedChargeDamage` in `SupportActions`) are therefore no-ops. Do not document mounted combat as a player feature.

## Combat log

Player-visible messages go through `CombatUI.ShowCombatLog(string)`, which calls `CombatLogPanel.AddMessage`. Build the rich text with `Combat/Logging/CombatLogHelper.cs` (`Damage`, `Warning`, `ConditionApplied`, `Buff` and others), or with `CombatResult.GetDetailedSummary()` / `FullAttackResult.GetFullSummary()` for attack breakdowns. `Combat/Logging/CombatLogger.cs` is the older formatter and mostly dead (only `FormatSavingThrow`, the blink spell-failure formatters and `Show` are used). `GameManager.OnNewRound` adds round separators through `CombatUI.AddTurnSeparator`. Combat debug tags, in addition to the general ones under [Cross-cutting conventions](../ARCHITECTURE.md#cross-cutting-conventions): `[Attack]`, `[AttackFlow]`, `[ThreatSystem]`, `[Grapple]`, `[DeathFlow]`. Logging on hot paths (threat queries, A*, hover preview) is very heavy.
