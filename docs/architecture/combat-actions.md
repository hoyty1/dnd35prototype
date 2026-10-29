> Written 2026-10-29 for batch 13 step 3 (`b13-catalog-skeleton`, uncommitted on d41d23d). Read from code and checked by `ActionCatalogTests` (its Play pass also runs the runner's refusal paths) and a `smoke/*` run with unchanged hashes. No live flow calls the runners or the executors yet, so their live paths are not verified in Play mode; step 6's scenarios first run them.

# Combat actions: the action catalog

Part of [ARCHITECTURE.md](../ARCHITECTURE.md). The plan is `docs/designs/action_catalog_batch13.md` ("Architecture", "End state"). The catalog lists every combat action in scope once, with its PHB Table 8-2 cost and provocation, and gives each one shared executor that the PC panel and the AI both reach. Step 3 built the skeleton over the finished cores as a pure move: no rule changed, and every scenario trace hash stayed the same.

## Where things are

| Piece | File | What it is |
|---|---|---|
| `ActionDefinition` and its enums | `Combat/Actions/ActionDefinition.cs` | One entry: `Id`, `Name`, `Category`, `Cost` (the existing `ActionCostType` from `_Core/Commands/IGameCommand.cs`) with an optional `CostRule` (read through `CostFor(actor, ctx)`), `Provokes` (`ProvokeRule`: No, Yes, Maybe, Varies, ByMovement), `Target` (`ActionTargetKind`), `Roles` (`[Flags] ActionRoles`), `RulesRef`, `Status` (`ImplStatus`), `IssueIds`, `Executor`, `UiAfter` (`ActionUiAfter`) with `UiAfterSeconds`, `Maneuver` with `SubstitutesForMeleeAttack`, `Availability` (read through `IsAvailableTo(actor)`) and the empty `AiModeValuation` slot. |
| `ActionIds` | `Combat/Actions/ActionIds.cs` | One string constant per entry (49). |
| `ActionCatalog` | `Combat/Actions/ActionCatalog.cs` | Lazy `Init`, `Get(id)`, `All`, `ForManeuver(type)`, `AvailableTo(actor)`; the entries are built in `Build`. |
| `ActionContext`, `ActionOutcome` | `Combat/Actions/ActionContext.cs` | The request (actor, `ActorSource Source`, the decider, target, cell, item, inventory index, spell, `Variant`, the wrapped core's `CoreRequest`, the AI's maneuver hook `TryStepManeuver`) and the result (`Performed`, `Refusal`, `Cancelled`, `RefusalLogged`, `CoreOwnsUi`, `Spent`, `Provoked`, `ActorDown`, `CombatEnded`, `CoreOutcome`). `Spent` stays `NotAnAction` when the run was refused before the executor ran, and is `Free` when it ran and spent nothing. |
| `IActionExecutor` | `Combat/Actions/IActionExecutor.cs` | `CanStart(actor)` and `CanExecute(ctx)` (both pure, return `Legality`) and `IEnumerator Execute(ctx, outcome)`. |
| `ActionCosts` | `Combat/Actions/ActionCosts.cs` | `CanAfford`, `TryPay` (over `CommitStandardAction`, `UseMoveAction` or the converted standard, the full-round and swift spends), `From(CastActionCost)`, and `Take`/`SpentSince`/`SpentBetween` (the largest action a run spent). |
| `ActionLegality` | `Combat/Actions/ActionLegality.cs` | Adapters into `Legality`: `FromDisabledReason`, `FromCheck`, `FromCast` (and `CastBar` for the cast core's reason tokens). |
| `ActionDeciders` | `Combat/Actions/ActionDeciders.cs` | `IActionDecider.ConfirmProvocation`, answered with a `ProvocationAnswer` (Proceed, Cancel, Unavailable); the UI decider shows the AoO prompt (`GameManager.TryShowActionAoOConfirmation` over `ShowAoOActionConfirmation`), answers Unavailable when a prompt is already open and for a cast (the cast core has its own prompt, with Cast Defensively); the AI decider always proceeds; `ActionDeciders.For(actor, ActorSource?)` is the catalog's only `IsControllable` read. |
| The executors | `Combat/Actions/Executors/DelegatingExecutors.cs`, `Combat/Actions/Executors/GameManager.ActionCores.cs`, `Combat/Actions/Executors/DefenseExecutors.cs` | The Delegating executors; the partial with the one forwarder to a private core that has no public wrapper (`ActionCore_StandUp`); the first New executor, total defense (`TotalDefenseRules`, `TotalDefenseActionExecutor`; batch 13 step 4, CMB-179). |
| The runners | `_Core/GameManager.ActionRunner.cs` | `RunPlayerAction`, `RunAIAction`, `ResolveActionProvocation`, `ConfirmActionProvocation`. They are `internal`: reachable from Assembly-CSharp (the tests, the harness, the AI), not from a Unity MCP command script, like `RunAttackSequence` and `ExecuteCast`. |

Shared with the live callers so the copies cannot drift: `GameManager.DeclareSmite` (the PC Smite button, the AI and the smite entry), `GameManager.DeclareFlurryOfBlows` (the AI's declaration and the flurry entry), `GameManager.LogStaysProne` (the AI stand-up's refusal line), and `ThreatSystem.GetStandUpAoOProvokers`, which is `GetActionAoOProvokers`.

`Legality` (`Combat/Core/Legality.cs`) gained `LegalityBar Bar` (None, Action, Condition, Compulsion, Target, Reach, Range, Ammunition, Resource, Equipment, Terrain, Ward, Charm, Other) and `Refused(LegalityBar, string)`. The old `Refused(string)` gives `Other`; no reason string and no `ToString` changed. `LegalityRisk` and `ActorSource {Ui, AI}` are unchanged (design choice C7: a forced PC turn passes `AI`).

## The executor rules

- `CanStart` and `CanExecute` roll no dice, log nothing and change nothing.
- `Execute` pays through `ActionCosts.TryPay` or through the core it wraps, and fills the outcome.
- An executor never continues the UI (`ShowActionChoices`, `AfterAttackDelay`, `EndActivePCTurn`, `DelayedEndActivePCTurn`) and never reads `IsControllable`, `ActivePC` or `IsPlayerTurn`. Whose choices they are is the context's `Source`. `ActionCatalogTests` scans every file under `Combat/Actions/Executors` for these names.
- A callback core (the cast core with the UI decider, the maneuver core) is waited on through its flag, and the loop stops when the combat ends. The cast executor reads the cast context's own outcome when a dialog re-entered the cast and replaced its done callback.
- A Delegating executor delegates to a live caller's entry point with that caller's prelude, never to a raw core alone: the attack entries run `NPCPerformAttackForAI` for the AI and `PerformPlayerAttack`'s rules (the direct-attack gate, the ward break) for the player unless the caller passes its own `AttackSequenceRequest`; stand-up applies its callers' gate before `ResolveStandUpFromProne`; overrun computes the path's AoOs (`CheckForAoO`) when the request has none.
- An executor whose core logs its own refusal sets `RefusalLogged` (the cast core, the move core, the 5-foot step, lay on hands, wild shape, bardic music, maintain concentration, the AI attack and stand-up), so the PC runner does not log it twice.

## The runners

- **`RunPlayerAction(ctx)`** sets `Source = Ui`, runs the executor and then applies the entry's `UiAfter`: `ShowChoices` shows the panel; `AttackDelay` runs `AfterAttackDelay` (it ends the turn when nothing is left); `EndTurn` ends it, after `UiAfterSeconds` when given. A refusal logs `"<name> cannot <action>: <reason>."` (one trailing period, nothing for an empty reason, nothing when the core logged it) and shows the panel. A downed actor ends the turn; an ended combat does nothing. An entry with `UiAfter` `None`, or a run whose core continues the UI itself (`CoreOwnsUi`: the UI cast decider's finish continuation, which a held-charge delivery through a UI `SpellCastRequest` also reaches), owns every follow-up, its refusal, cancel and downed actor included: the runner adds nothing, so no line is logged twice and no turn ends twice (CORE-012). A refusal before the executor runs always logs and shows the panel.
- **`RunAIAction(ctx, o)`** sets `Source = AI` and is yielded as a nested enumerator. It pre-checks `CanExecute` only for Extracted and New entries, where a refusal spends nothing; a Delegating entry's core owns its refusals. A pick the actor cannot start (`CanStart` refuses) is logged as `[ActionSet] shadow: ...`, a `Debug.Log` line that no trace hash covers. Step 6 points this hook at the legal set (`ActorActionSet`).
- **`ResolveActionProvocation(actor, AoOProvokingAction, decider, outcome)`** is the one AoO loop for non-movement actions. The threateners come from `ThreatSystem.GetActionAoOProvokers` (the footprint, living foes that can still make an AoO); with none it returns at once. The decider confirms first (`ConfirmActionProvocation`); then each foe takes its AoO until the actor drops, with a `CombatLogHelper.Buff` line for each. It never ends a turn.
- **`ConfirmActionProvocation(actor, action, name, threateners, decider, outcome)`** is the prompt alone, for a core that takes its own AoOs (the stand-up core). A cancel sets `Cancelled` and the refusal; Unavailable refuses without a cancel; a prompt torn down without an answer (`ShowActionChoices`, `CancelMovementSelection` and the encounter reset clear it without calling `OnCancel`) counts as a cancel, so the wait never hangs; a combat end sets `CombatEnded`.
- Both runners read the action economy before and after the run (`ActionCosts.Take`, `SpentSince`) to fill `Spent`.

Total defense (batch 13 step 4) is the first live caller of both runners: the PC's Total Defense button goes through `RunPlayerAction`, and the cornered panicked creature (`FrightenedBehaviorController.ExecuteDecision`, PC or NPC) and the harness's `Step.TotalDefense` go through `RunAIAction`; `TotalDefenseRulesTests` runs that live `RunAIAction` path, and `ActionCatalogTests`' Play pass the runner's refusal paths (a Missing entry, no actor, a stand-up for a creature that is not prone) and `ResolveActionProvocation`'s early exit. Since batch 13 step 5 the AI's out-of-reach alternatives (`AI/AiOutOfReach.cs`: another target, the preferred range or an approach, withdraw, total defense; decision D1 and design choice C1, `docs/systems/AI.md` 5.10) run the attack, full-attack, move, withdraw and total-defense entries through `RunAIAction`. Step 6 routes the rest of the AI through `RunAIAction`; batch 22 makes the panel read the legal set.

## The entries

The status column below is the source for [AI_ACTION_COVERAGE.md](../systems/AI_ACTION_COVERAGE.md) from step 3 on. "Cost" is the Table 8-2 action type; `CostFor` gives the per-actor cost where one is noted. ActionCatalogTests pins every row in its golden manifest.

**Table 8-2 as read (PHB p.141).** The text extract's move-action column is offset by two rows, and its full-round column by one; they are realigned here.
- Provoke: loading a hand or light crossbow, standing up, picking up an item, sheathing a weapon, retrieving a stored item, a coup de grace.
- Do not provoke: drawing a weapon, readying a shield, opening a door, mounting, a full attack, a charge, a withdraw.
- Where the action does not provoke but its movement does (move, charge, withdraw, bull rush, overrun), the entry says `ByMovement`. Trip is `Varies`: the table says No, but PHB p.158 makes the trip an unarmed touch attack, which provokes without Improved Trip (as `ThreatSystem.DoesManeuverProvokeAoO` does).

| Id | Cost | Provokes | Status | Executor or issue |
|---|---|---|---|---|
| attack | Standard | Varies | Delegating | AI: `NPCPerformAttackForAI` capped at one step; player: the direct-attack gate and the ward break, then `RunAttackSequence` (`ForUiAttack`); a caller's own request: `RunAttackSequence` |
| full-attack | FullRound | Varies | Delegating | AI: `NPCPerformAttackForAI` (every step the action economy allows); player: as above with `ForUiFullAttack` |
| flurry-of-blows | FullRound | Varies | Delegating | `DeclareFlurryOfBlows`, then the full attack's path, its request built after the declaration (PHB p.40). The PC button's own declaration line (and its Special Attack substitutions) stays on the button until a PC flow uses this |
| charge | FullRound (Standard when limited to one action) | ByMovement | Delegating | `CanCharge` (its path test is `TryBuildChargePath`, the owner's one charge legality test) and `ExecuteCharge(ChargeRequest, ChargeOutcome)` |
| deliver-held-charge | Standard (none in the casting round) | No | Delegating | the AI's delivery (`TryNPCDeliverHeldChargeForAI`), or a caller's `SpellCastRequest` through the cast core (with the UI decider the cast core continues the UI, `CoreOwnsUi`) |
| attack-wall | Standard | Maybe | PcOnly | AI-054 |
| trip, disarm, grapple, sunder | Standard, one melee attack | Varies, Yes, Yes, Yes | Delegating | `ExecuteManeuver` |
| bull-rush | Standard | ByMovement | Delegating | `ExecuteManeuver` |
| bull-rush-charge | FullRound | ByMovement | Delegating | the charge executor with `BullRush` |
| overrun | Standard (in a move) | ByMovement | Delegating | `RunOverrunMove` (`OverrunActionRequest`), then `EvaluateCombatEnd` |
| feint | Standard (Move with Improved Feint) | No | Delegating | `ExecuteManeuver` |
| coup-de-grace | FullRound | Yes | Delegating | `ExecuteManeuver` |
| aid-another, wake-ally, turn-undead | Standard | Maybe, No, No | PcOnly | AI-054 |
| total-defense | Standard | No | New | `TotalDefenseActionExecutor` (`Combat/Actions/Executors/DefenseExecutors.cs`, batch 13 step 4): `TotalDefenseRules.Check` (a standard action; not grappling, fighting defensively or after a Combat Expertise attack), then `ActionCosts.TryPay` and `CharacterController.BeginTotalDefense` (+4 dodge, +6 with 5 Tumble ranks, in `CharacterStats.DodgeACBonus`; no AoOs in `ThreatSystem.CanMakeAoO`; ended by `StartNewTurn`, `ResetForNewCreature` and the encounter reset). The PC's Total Defense button (`OnTotalDefenseButtonPressed`, its state from `CanStart`) and the cornered panicked creature (`FrightenedBehaviorController.ExecuteDecision`, `RunAIAction`) run it |
| cast-spell | Standard (casting time through `CastActionRules`) | Yes | Delegating | `ExecuteCast` or `RunCastSync` with the request's decider |
| maintain-concentration | Standard | No | Delegating | `TryMaintainConcentrationForAI` (`ExecuteMaintainConcentration`) |
| use-item | Standard | Varies | PcOnly | AI-054 |
| move | Move | ByMovement | Delegating | `MoveCharacterAlongComputedPathForAI`, the move action paid after the walk as today (AI-076) until steps 9 and 10 |
| withdraw | FullRound (Standard when limited to one action) | ByMovement | Delegating | `ExecuteWithdrawMovementForAI` |
| five-foot-step | no action | No | Delegating | `TryTakeFiveFootStepForAI` (`ExecuteFiveFootStep` without the UI return) |
| run | FullRound | Yes | Missing | CMB-202 |
| crawl | Move | Yes | PcOnly | AI-054 |
| stand-up | Move | Yes | Delegating | `ActionCore_StandUp`: the callers' gate (prone, `GetStandUpDisabledReason`; nothing spent on a refusal), the player's StandFromProne prompt, then `ResolveStandUpFromProne` |
| drop-prone | Free | No | PcOnly | AI-054 |
| smite | Free (declared for one melee attack) | No | Delegating | `DeclareSmite` (`SmiteRules.TryDeclare` and its line); the PC button's turned-condition block and withdrawal stay on the button |
| lay-on-hands, wild-shape, bardic-music | Standard (stopping the music: Free) | No, No, Varies | Delegating | `TryLayOnHandsForAI`, `TryWildShapeForAI` or `TryWildShapeRevertForAI` (`Variant` "revert"), the bardic executors (`Variant`: an ability name, "InspireCompetence:<skill>", "concentrate", "suggestion", "stop") |
| rage, domain-power | Free, Standard | No | PcOnly | AI-054 |
| reload | Move (heavy: FullRound; Rapid Reload) | Yes | PcOnly | AI-057 |
| draw-weapon, sheathe-weapon, ready-shield, don-armor | Move, Move, Move, FullRound | No, Yes, No, No | Missing | ITM-069 |
| pick-up-item, drop-item | Move, Free | Yes, No | PcOnly | AI-054 |
| breath-weapon, spittle, engulf, ranged-special | Standard, Free, Standard, Standard | No, Varies, Varies, Varies | AiOnly | CRE-039 (breath also AI-040) |
| gaze | Free | No | OutOfScope | CRE-076 |
| ready, delay | Standard, no action | No | Missing | CMB-029 |

`AvailableTo(actor)` lists the common entries plus the ones the actor's data gives: flurry (a monk), turn undead, smite, lay on hands, wild shape, bardic music, rage, domain powers, casting, and the per-creature breath weapon, spittle (a free-action ranged special), ranged special and engulf. An OutOfScope entry is never available.
