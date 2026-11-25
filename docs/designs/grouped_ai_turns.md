> Status (2026-11-24): step 1 built (the pacing service, PURE: every trace hash and frame count of its test budget equal to batch 14's closing regression; docs/TESTING.md 3.2); step 2 built (poison secondaries in rounds, narrow behaviour change, CMB-007; 0 hashes moved in its budget; docs/TESTING.md 3.2); step 3 built (tokens render through `TokenView`, NEUTRAL: 174 pre-existing hashes and frame counts of its budget equal; docs/TESTING.md 3.2; the owner's visual check not done); step 4 built (a summon leaves play at once and the Lesser Globe centre reads the grid, narrow behaviour change, CRE-312; its budget run 2026-11-24: static edit and Play passes OK, the 5 new scenarios Pass, every pre-existing rules and smoke hash equal, one soak hash moved (seed 3) and explained by the earlier summon expiry line; docs/TESTING.md 3.2); step 5 built (the escape square of a forced grappling PC, narrow behaviour change, CMB-308 filed and fixed; its budget run 2026-11-24: the new scenario Passes, every pre-existing grapple, flee and panic hash equal, the grapple static suite OK; docs/TESTING.md 3.2); step 6 built (the AI speed factor, NEUTRAL, AI-023 fixed; its budget run 2026-11-24: every job hash-equal at 1x and 4x, in fast mode `rules/*` seeds 1-2 and soak seeds 1-10, in fine mode the smoke, summon, poison, grapple, globe and area subsets; static edit and Play passes OK; the HUD button not checked by hand; docs/TESTING.md 3.2); step 7 run (the midpoint full regression, 2026-11-24 on 063ce4e, on a restarted editor: static edit and Play passes OK; `rules/*` and `smoke/*` seeds 1-5 and the soak seeds 1-50 OK with 0 Fail, every rules and smoke hash equal to batch 14's closing regression, soak seeds 3 and 48 moved and explained by step 4; `aiSpeed=4` and fine mode hash-equal to 1x, soak seed 48 included in a separate run; seed 48's hash rested on a frame boundary (CRE-311, fixed 2026-11-25 before step 9; seed 48's hash moved once there, run 20261125-085010, the same outcome; docs/TESTING.md 3.2); the soak's decision time within batch 14's step 1 baseline; nothing new filed; docs/TESTING.md 3.2): stage 1 (steps 1-6) is closed; step 8 built 2026-11-25 (the presentation facade in Passthrough only, PURE: `rules/*` seed 1 and `smoke/*` seeds 1-3 hash-equal to step 7's run plus the recorded moves, their `presentation.jsonl` kept as the Passthrough baseline; UI-058 filed; docs/TESTING.md 3.2); step 9 built 2026-11-25 (run-ahead with sequential playback, NEUTRAL with grouping off by default; UI-058 fixed; its budget run 2026-11-25 after the review fixes: static edit and Play passes OK, the eleven `rules/group-*` pairs Pass with every group expectation, `smoke/*` and `rules/*` seeds 1-3 as `group=pair` 1857 of 1857 pairs `RepeatCheck` and `PresentationCheck` same, the fine-mode subset as pairs same, the seed 48 canary 651e227 in both reps; four harness faults the pairs found fixed (ground items and party gold leaking between jobs, an in-flight Ui step emitted at a frame-dependent place, frame numbers in one expectation, the setup in the presentation trace), which moved 24 rep 1 hashes with no rules outcome; docs/TESTING.md 3.2); steps 10-12 not built. Q6 answered by the owner 2026-11-24 (follow RAW) and built 2026-11-25 (`gt-ingested-poison-raw`, CMB-307: an ingested poison's secondary also at 10 rounds; no existing hash moved, docs/TESTING.md 3.2). Plan written 2026-11-16. Asked for by the owner on 2026-10-30. It runs after batch 14 (weighted personalities) and before batch 15 (NPC spellcasting), without renumbering the later batches. Queue `_work/queue_grouped_workflow.js` generated, not launched; the owner launches it. Read-only design pass on master (code unchanged since 5176586): five code mappers, two competing designs, a judge that checked the key claims in the code, and two adversarial checks. Nothing here was compiled, run or verified in Play mode. Anchor on member names: batch 14 moves lines. Paths are under `Assets/Scripts/` unless they start with `docs/`.

# Grouped AI turns

## Summary

**The rule.** Same code, same order, same dice. Grouping may change only three things:
- when the screen shows what has already happened;
- how long the pauses last;
- when the player's input is accepted.

No new `IsControllable` read is added. The one stop that asks "is this creature player-run?" asks the existing decider seam (`!AttackDeciders.For(c).IsSynchronous`). Step 10 records it in `docs/systems/PC_NPC_PARITY.md` as a presentation-only consumer of that seam.

Every step is one of three classes:
- **PURE:** identical hashes and identical frames.
- **NEUTRAL:** identical hashes; only presentation or frame counts change.
- **A named behaviour change,** narrow, with each moved hash explained.

**Six decisions.**
1. **No group runner.** The turn chain (`NextInitiativeTurn` -> `TurnService.EndTurn` -> `GameManager.OnTurnStarted` -> `StartPCTurn` or `SingleNPCTurnFromInitiative`) stays exactly as it is, CORE-012 re-entrancy included.
   - A group is a presentation state.
   - It opens when an AI-decided turn starts. It closes when control returns to the player, at a defender prompt, at combat end, or at a reset.
2. **Recording keeps the frame structure.** Inside a group, each pacing wait still yields one frame, and each `MoveAlongPath` step still yields one frame. This keeps unchanged:
   - the area-effect `Update()` trackers;
   - the Stalemate halt before the first decision of a round;
   - `SkipChainRunaway`;
   - `InlineAction`'s leaf-yield contract.
3. **Rules that read wall time or token lifetime are moved off it first,** before any speed factor or recording:
   - poison secondaries (CMB-007);
   - summon removal, which waits on its despawn tween (new CRE issue);
   - the Lesser Globe centre, which is read from the token transform.

   A finer-grained harness time mode (`mode=fine`) then proves that pacing feeds no rule.
4. **Tokens render through a mirror view (`TokenView`).** The creature's own GameObject stays rules state:
   - area tracking finds creatures with `FindObjectsOfType<CharacterController>`;
   - `IsActiveCombatant` needs `activeInHierarchy`.
5. **Everything else is captured at the `CombatUI` facade** (after `ScenarioHooks.CombatLog`), at `SquareCell.SetHighlight`, and at area-visual creation.
   - Tokens are keyframed at every rules boundary (each pacing wait and each step).
   - Live play only renders the state at a yield, so the playback shows exactly the frames that live play would have shown.
6. **Stops are presentation.**
   - An automatic reaction by a player-run creature (an AoO, or an acid spray answered by the AI fallback) ends the overlap window, and that turn plays sequentially.
   - A defender prompt opens after the playback reaches it, because the rules already wait on its answer.
   - Control return and combat end drain the playback.
   - The rules never pause for playback.

There are 12 steps: 10 build steps and two full regressions.
- **Stage 1 (steps 1-6):** the pacing service, the rules moved off wall time, the mirror view and the AI speed factor. It is useful alone and closes at the midpoint regression (step 7, Q3).
- **Stage 2 (steps 8-11):** the presentation facade, run-ahead with sequential playback, the stopping points, then the overlap and the options. Step 12 is the final regression.

**Why this shape.** Two design passes were merged by a judge.
- **Kept from both:** the lazy marks instead of a group runner; recording that keeps one frame per wait and per step; stops detected at seams, never predicted.
- **Corrected in both** (checked in code):
  - The grapple blink waits must not go through pacing. `RunGrappleAlternatingDisplay` is an animation that polls grapple state: in Holding it would toggle every frame, and with the speed factor it would blink faster. Both passes routed all 97 calls.
  - Summon removal is a rules timing dependency, not a reveal. `DespawnSummonWithEffect` removes the summon from `NPCs`, initiative and the grid only after a 0.45 s `Time.deltaTime` tween, so with the speed factor or recording the removal lands at a different point in the turn sequence, and fast mode cannot see that. A lingering token is also still found by `FindObjectsOfType<CharacterController>`, so "destroy after the visual" is wrong too: the model is destroyed at once and the view plays the effect.
  - `CombatPacing.Wait` must always return a `WaitForSeconds`. `RunAIActionNow` pushes any `IEnumerator` it receives, a `WaitUntil` included, drains it in place and stops after 10,000 steps, so a pacing wait that returned `WaitUntil` would break synchronous AI actions. There is no Draining mode.
  - The AoO mark goes after the Spring Attack suppression in `ThreatSystem.ExecuteAoO`. That suppression returns after `UseAoO`, so the point after it is where the AoO is really made.
  - The group closes at the action-button show (the control beat), and facade calls keep queueing while the queue is not empty. An earlier "no beats after the closing beat" contradicts the rest of `ShowActionChoices` and `NextInitiativeTurn`'s trailing `UpdateInitiativeUI`.
  - Token keyframes are taken at every rules boundary, not only at `UpdateAllStats4PC` or `UpdateInitiativeDisplay` calls. A teleport, a tint or a death followed by a wait without a stats refresh would otherwise be missed.
  - `FlamingSphereEntity` (moved by `TryControlFlamingSphereForAI`) is a token-like object that neither pass covered.
  - Poison secondaries anchor on the exposure's turn (the `TurnDurations` rule, CMB-006), not on the victim's count.
  - The `OnCombatEnded` cleanup gap is CORE-002. A new CORE issue would duplicate it.

## 1. Request

The owner, 2026-10-30: "design AI turns to take into account how many AI driven actors are acting before player driven actors so that they can act 'simultaneously' as long as they are not prompting any action from the player driven actors such as attacks of opportunities or readied actions".

The goal is pacing. A run of enemy turns should play out together, instead of one after another with pauses, so the player waits less between their own turns.

## 2. The rules stay sequential

Combatants act in initiative order, from the highest result to the lowest (PHB p.135-136), and each turn starts from what the earlier turns left. Truly simultaneous resolution would change outcomes:
- Two orcs would both attack a goblin that the first one already dropped.
- Two creatures would move into the same square.
- One creature's move would not see the square an ally just vacated.

So **every turn is still resolved in order, exactly as today. Only what the player sees overlaps.** The rules, the dice order and the results do not change. Grouping never decides which rules run: PCs and NPCs keep one rules path (CLAUDE.md), and the four new marks sit on paths both already share.

## What exists today (checked in code; corrections to the proposal)

- **Decisions are cheap.** In the batch 13 final soak (runs `20261030-062257` to `-062539`, fast mode), a whole turn took a median of 45 ms and a mean of 55 ms of real time: 61.6 s over 1,121 turns. That figure includes movement, attacks and logging. Resolving five enemy turns back to back takes a fraction of a second.
- **The waiting is pacing, but not all of it.**
  - Game code (outside `Tests/`) has **97** `new WaitForSeconds` calls, not 98 (the 98th is the `InlineAction` doc comment): 48 in `Services/AIService.cs`, 12 in `_Core/GameManager.NPCTurns.cs`, 7 in `_Core/GameManager.CombatActions.cs`, 7 in `Combat/Maneuvers/GrappleSystem.cs`, 4 in each of the four `*BehaviorController`s (Charmed, Confused, Frightened, Suggested), 2 in `AI/AiOutOfReach.cs`, 2 in `CharacterController.cs`, and 1 each in `_Core/GameManager.cs` (`ResolveStandUpFromProne`), `GameManager.ActionRunner.cs` (`ResolveActionProvocation`) and `Combat/Maneuvers/SupportActions.cs`.
  - **92 are pacing.** The two in `CharacterController.RunGrappleAlternatingDisplay` are an animation, and the three in `GrappleSystem.CheckMenuAfterDelay` are debug.
  - Pacing also lives in 1 `WaitUntil` (`AI_GrappleRestrictedTurn`), 1 `WaitWhile` (`RunAttackSequence`), 23 `yield return null` (13 are polling loops that wait on a player answer or on a cast, maneuver or charge to settle; the other 10 are the `MoveAlongPath` and `SmoothScaleCoroutine` tweens, `SummonedCreatureVisual`'s two effect tweens, and UI, bootstrap and dormant `CommandProcessor` frames) and the `Time.deltaTime` tweens. Movement animates square by square in `CharacterController.MoveAlongPath`, whose own comment says the per-step frame exists for the area trackers.
- **The turn loop.** `GameManager.SingleNPCTurnFromInitiative` (`_Core/GameManager.NPCTurns.cs`) runs `AIService.ExecuteNPCTurn`. Turns advance re-entrantly through coroutines (CORE-012).
- **No AoO can prompt anyone.**
  - `AttackSequenceRequest.ForAoO` fixes `AttackDeciders.For(threatener, ActorSource.AI)` and `CallerCanWait = false`.
  - `RunPendingAttackedReactions(canWait:false)` swaps a prompting decider for the AI's.
  - So the owner's AoO stop is a policy stop. It needs its own seam (`ThreatSystem.ExecuteAoO`), because no decider is called.
- **Attacker-side prompts never fire in a group.** Cleave, Improved Grab, Haste, the full-attack step, touch delivery, the cast pickers and the action AoO confirmation all go to the AI decider when the actor is AI-run, by construction. The proposal's test "a group stops at a Cleave prompt" cannot be built. The prompts a group can reach are on the defender or reactor side:
  - counter-trip (`ChooseCounterTrip`);
  - overrun response (`ChooseOverrunResponse`);
  - grapple escape opposition (`ChooseOpposeEscape`);
  - a player-run bombardier beetle's acid spray (`ChooseAttackedReaction`, only where `CallerCanWait`: `NPCAttackSequence` and the charge).
- **There are four decider families,** not three: attack (`IAttackDecider`), maneuver (`IManeuverDecider`, which the proposal missed), cast and action. The decider seam does not see AoOs or the combat-end UI.
- **A charmed PC is not a forced turn** (`TryRunCompulsionTurn`: "Charmed is no turn compulsion"). The forced PC turns are confusion that forces the turn, suggestion, and panicked, cornered or grappling flight (fear or turned), all through `GameManager.BeginForcedPCTurn` (`_Core/GameManager.TurnStart.cs`). A forced turn passes `ActorSource.AI` (batch 13 C7).
- **No `WaitingFor*` flag blocks input during AI turns.** The block is structural: `IsPlayerTurn` (phase `PCTurn` with `ActivePC`) plus the `Animating` sub-phase. A group needs its own input gate.
- **The fast-mode "proof" is weaker than the proposal said.** `ScenarioFastMode` keeps every wait and frame and only scales time (1 game second per frame). Every wait of 1 s or less is already one frame, so a fast-mode equality cannot see sub-second wall-time dependence.
- **Known wall-time and lifetime readers in rules code:**
  - poison: `GameManager.UpdatePoisonTimers` runs from `Update` and counts `ActivePoison.TimeUntilSecondary` down by `Time.deltaTime` (60 s, or 600 s ingested), menus open or not (CMB-007; fixed in step 2, 2026-11-24: `PoisonTiming` counts rounds at initiative counts);
  - summon removal after the 0.45 s despawn tween (`DespawnSummonWithEffect`);
  - `LesserGlobeOfInvulnerabilityAreaEffect.Update` and `OnRoundStart` read the caster token's transform, which lags the grid square during a step tween;
  - `AreaAimSearch`'s `Time.frameCount` cache (its key changes with the board, so it is safe while the frame partition holds);
  - `PlanarTravelSystem`'s `Time.time` gate (a 9th-level spell, out of scope), `TurnUndeadSystem.AppliedTime` (written, never read), the dormant `CreatureTrapSystem`, and `MoveAlongPath`'s own `startPos` read.
- **A decider leak (G4).** `GrappleSystem.OfferFreeAdjacentMovement` calls `ManeuverDeciders.For(actor)` with no source override, and `CanSelectSquareNow` passes on a forced PC turn, so a square prompt can open in an AI-decided turn, against batch 13 C7. Reproduced in step 5 (2026-11-24, CMB-308) and fixed there (verified by its scenario run the same day).
- **There are no pop-ups, falls, sound or camera moves in the code.** A "fall" today is the `DeadSprite` swap in `OnDeath`. The camera never moves on its own. Pop-ups would be new presentation (Q5).
- **Combat end does not run through `OnCombatEnded`.** A won or lost fight ends in `HandleCombatVictoryDetected` or `HandleCombatDefeatDetected` (`_Core/GameManager.CombatEnd.cs`). `OnCombatEnded` is a second route into `BeginPostCombatLootCollection` (`_Core/GameManager.LootCollection.cs`). Its cleanup gap is CORE-002, already filed and out of scope.
- **Readied actions and readied counterspells do not exist yet** (CMB-029, SPL-047): `ActionIds.Ready` is `ImplStatus.Missing`, and `TryAIReadyCounterspell` has no caller.
- **Ingested poison delay.** The code used 600 s for ingested secondaries and its comment claimed the DMG. DMG p.296-297 gives secondary damage at 1 minute after exposure for every type. Kept as a possibly intentional deviation and asked (Q6); the owner answered 2026-11-24 (follow RAW), and every type now waits 10 rounds (CMB-307 fixed).

## Design choices (game direction the owner may overrule)

Recorded here, not as rules readings.

- **C1. A player-run creature's automatic reaction ends the overlap window.** This covers an AoO and an acid spray the AI fallback takes for a player-run beetle. Its turn plays sequentially, and the rules do not pause. This is how the owner's settled AoO ruling is built.
- **C2. The speed factor applies to every AI-decided turn,** forced PC turns included, whether or not grouping is on. Player turns stay at 1x.
- **C3. At a defender prompt, the playback plays out up to the prompt, then the prompt opens.**
- **C4. No new pop-ups, falls or sound in this work** (Q5).
- **C5. A creature's second turn ends the overlap window.**
- **C6. Open character, skills and inventory windows close when a group opens.**

## Architecture

### The group

A **group** is a maximal stretch of turns, in initiative order, during which no player-run creature is asked anything. It opens at the start of an AI-decided turn and closes at the first of these:
- (a) control returns to the player: the action buttons are shown;
- (b) a defender or reactor prompt to a player-run creature opens;
- (c) combat ends;
- (d) a reset or harness halt.

A group of one is legal; it looks like today's turn at the AI speed. Inside a group, playback is divided into **overlap windows** (Playback). A window ends at a player-run creature's automatic reaction (S2) or before a creature's second turn (S5); the group itself stays open.

**Who is AI-decided.** There is one source of truth: the paths the code already forks to.
- **AI-decided turns** (they open or continue a group):
  - **The NPC path,** `OnTurnStarted`'s `else` branch: NPCs and enemy summons; summons of an AI-run caster (`SpawnSummonedCreature`: `controllable: alliedToPlayer && caster.IsControllable`); swarms (`SetControllable(false)`), even allied ones; non-controllable party allies; harness Ai, Scripted and Idle party members.
  - **A forced PC turn** (`BeginForcedPCTurn`): confusion that forces the turn, suggestion, and panicked, cornered or grappling flight. Its deciders are the AI's (batch 13 C7).
  - **A skipped turn** (dying or helpless, `ShouldSkipTurnDueToHPState`) keeps an open group open, because nobody decides anything.
- **Player-run turns** (control returns, and the group closes before the menu shows): every turn that reaches `ShowActionChoices` and shows the action buttons. That covers a normal PC turn, the player's summons, a charmed PC, a confused PC that acts normally, a frightened or turned PC that chooses its flight from the restricted menu (`ChoosesFlight`), and a cornered frightened PC whose forced flight hands back the menu (`ExecuteFleeingForcedPCTurn` -> `ShowActionChoices`).

**How it is collected: lazily, by marks.** The group is never collected ahead, because initiative changes mid-group: `TurnService.AddToInitiative` inserts a summon after its caster, `RemoveFromInitiative` removes a creature, and a forced PC's membership is known only after its confusion d% roll in `EvaluateTurnCompulsion`. Four presentation-only calls in a static `TurnControl` carry the whole logic:
1. **`TurnControl.AiTurnStarting(actor)`.** Called in `OnTurnStarted`'s NPC branch immediately before `StartCoroutine(SingleNPCTurnFromInitiative(character))`, and in `BeginForcedPCTurn` immediately before `StartCoroutine(forcedTurn)`.
   - It sets the pacing kind to Ai.
   - If `GameSettings.groupAiTurns` is on (read once, when a group opens), no group is open and the phase is not CombatOver, it opens a group (Holding).
   - If a group is open and `actor` already has a lane in the current window, it applies the window cap (S5).
2. **`TurnControl.PlayerControlReturned(pc)`.** Called in `ShowActionChoices` immediately before `CombatUI.ShowActionButtonsForCharacter(pc)`, its only caller. That is after the `ActivePC`, `EvaluateCombatEnd` and submenu-abort returns, and after the method's own highlight and hide work. It sets the kind to Player. If a group is open, it closes it, and the button show becomes the group's control beat.
3. **`TurnControl.CombatEnded()`.** Called right after `CurrentPhase = CombatOver` in `HandleCombatVictoryDetected` and `HandleCombatDefeatDetected`, and in `OnCombatEnded`. It closes the group.
4. **`TurnControl.PromptOpened()`.** Called by the facade when an interactive prompt is recorded, and by `PromptAttackedReaction` before its pre-show wait (S3). It closes the group.

**The first AI turn of a group.** Its turn-start work (`ConditionService.OnTurnStart`, `BeginCombatantTurn`, and `OnNewRound` on a wrap) runs before the mark, live, as today. For a forced PC turn the mark sits deeper: `BeginForcedPCTurn` is reached from `StartPCTurn` -> `BeginActingTurn` -> `TryRunCompulsionTurn`, so `StartPCTurn`'s "turn begins" log line, its `SetActivePC`/`SetActiveNPC` highlights and `UpdateInitiativeUI`, the perception check and the confusion d% also run before the mark. Everything after the mark is recorded until the group closes, including later round wraps, a skipped creature's "cannot act" line, and a player-run creature's turn-start work up to its control beat.

**The window cap (S5).** When a creature would start its second turn inside the current window, the window ends (a barrier), and a new window opens with that turn. This keeps overlap from spanning two turns of the same creature and gives an all-AI fight (the soak, or a party that is all dominated or dying) regular window ends. The rules are never paused.

### Stopping points

Each stop is detected when its call arrives at a seam. There is no prediction.

| # | Stop | Seam (checked member) | Are the rules waiting? | Reaction |
|---|---|---|---|---|
| S1 | Control returns to a player-run creature | `ShowActionChoices` -> `TurnControl.PlayerControlReturned` -> `CombatUI.ShowActionButtonsForCharacter` | No, idle: the menu is the next input | **Animated drain.** The button show is the group's last beat. Beats from the rest of the call chain (`NextInitiativeTurn`'s `UpdateInitiativeUI` after `EndTurn` returns) join that beat. Input stays blocked until the queue is empty (Input). |
| S2 | A player-run creature takes an automatic reaction during the group: an AoO, or an acid spray answered by the AI fallback | `ThreatSystem.ExecuteAoO`, after the Spring Attack suppression and before the attack sequence runs; `GameManager.RunAttackedReaction`, just before `ResolveAcidSpray`. Both test `CombatPresentation.Holding && !AttackDeciders.For(creature).IsSynchronous` | No: the reaction is automatic and has no input | **Window stop (C1).** The lane holding the mark starts only after every earlier beat has played, plays at its recorded pacing divided by the AI speed, and the next lane opens a new window. The rules do not pause. |
| S3 | A defender or reactor prompt to a player-run creature: counter-trip, overrun response, escape opposition (all `ShowConfirmationDialog`), and the acid-spray menu (`PromptAttackedReaction`) | The `CombatUI.ShowConfirmationDialog` facade (Interactive class) and a pre-show wait in `PromptAttackedReaction` | Yes. The flag is set before the show (`_counterTripPromptOpen`, `_escapeOppositionPromptAnswer`, `_overrunResponsePromptAnswer`), and the rules poll it (`WaitWhile(IsAwaitingCounterTripChoice)`, `WaitUntil(result != null)`, the overrun `answered` loop) | **Play out, then prompt (C3).** The dialog is queued as an interactive barrier and opens after every earlier beat has played; the group closes. The acid-spray menu coroutine first calls `TurnControl.PromptOpened()`, because the menu is not a facade call and nothing else would close the group. It then waits (`while (CombatPresentation.BlocksInput) yield return null;`, which now waits only for the queue to drain), then shows the menu as today, so its "menu closed means decline" loop is untouched. After the answer, the rest of that turn runs live at the AI speed. |
| S4 | Combat ends | `HandleCombatVictoryDetected`, `HandleCombatDefeatDetected` -> `ShowDefeatScreen`, and both callers of `BeginPostCombatLootCollection` | Idle: waiting on the loot window or the defeat choice | **Animated drain.** The rules work stays immediate (Combat end and reset). The banner and button hide are queued, `ShowDefeatPanel` is an interactive barrier, and `LootCollectionUI.Open` runs through `WhenDrained`. |
| S5 | A creature's second turn in the window | `TurnControl.AiTurnStarting` | No | A window barrier; the group stays open. |
| S6 | Reset or halt | `ResetCombatStateForNextEncounter`, `Harness_HaltCombat`, `Harness_ResetWorld` | Not applicable | `CombatPresentation.Clear()`. |
| S7 | Future: readied actions (CMB-029) and readied counterspells (SPL-047) | Not built: `ActionIds.Ready` is `ImplStatus.Missing`, and `TryAIReadyCounterspell` has no caller | Not applicable | Reserved API: `MarkStop(StopKind.PlayerReaction, actor)` for an automatic trigger, and the Interactive facade for a prompt. A counterspell triggers inside `RunCastSync` (`CastStageCounterspell`), which cannot wait, so its prompt needs its own design when it is built. |

**Why S2 does not suspend the rules.**
- The AoO resolves synchronously inside the mover's coroutine, and several provoking paths cannot yield at all: the ranged shot inside `RunAttackSequence`, `ResolveManeuverInitiationAoOs`, and the cast provocation inside `RunCastSync`.
- The player gives no input to an AoO. So "everything before, then this turn sequentially" looks the same whether the rest of the turn resolved before or after the AoO was shown.
- If the game ever lets a player decline an AoO (RAW allows it; today it is automatic for everyone), that becomes a prompt under S3. It would then need a pre-flight stop at the coroutine boundaries that already build provoker lists: `ResolveMovementAoOsBeforeStep` (`AoOPathResult.ProvokedAoOs`), `ResolveActionProvocation` (`GetActionAoOProvokers`) and `ResolveStandUpFromProne` (`GetStandUpAoOProvokers`).

**Not stops,** because each is answered by the AI or is automatic and not an action of the player-run creature: Cleave, Improved Grab, Haste, the full-attack step, cast pickers and touch delivery; `ForgoSave` (both deciders return `SpellSaveRules.IsHarmlessOrBeneficialTo`); Deflect Arrows, Uncanny Dodge, spell resistance, concentration; dying and stabilization rolls; being attacked.

**Gaps the code maps found, and how each is handled:**
- **G1.** The AoO has no decider and no suspension point. Handled by S2, a mark only.
- **G2.** The combat-end UI has no decider and two loot routes, and the treasure dice sit inside the UI function. Handled by S4, which defers only `LootCollectionUI.Open` and the defeat panel.
- **G3.** A forced PC's membership is known only after its d% roll, and a forced flight can become a menu. Handled by the lazy marks; the conversion reaches S1.
- **G4.** The escape-square leak (What exists today). Fixed in step 5.
- **G5.** There is no central "a prompt is open" predicate. The facade classes every public `CombatUI` method (`CombatUI` method classes), and a reflection test fails on any unclassed one. `CombatPresentation.HasPendingInteractive` joins `Harness_PendingPrompt`.
- **G6.** The Ui maneuver decider answers as the AI when its prompt cannot open (`PromptCounterTrip` returns false while one is open). The flag is still set at rules time, so this is unchanged; a test pins that `PromptCounterTrip` returns true while a group is open.
- **G7.** The escape and overrun waits are inline. The flags are set before the show, as today, so they hold.
- **G8.** `SpellCastRequest.Decider` defaults to Ui. That is not reachable from an AI request (`RunCastSync`'s violation report stays the guard), so it is out of scope.
- **Fallback.** Any other Interactive-class call made while the queue is not empty runs `CatchUpNow()` (apply every queued beat at once), then shows as today, with a `Debug.LogWarning("[Presentation] ...")`. A new prompt can never open before the state it refers to is on screen.

### Recording (resolve now, show later)

While a group is open, `CombatPresentation.Holding` is true and the rules code runs unchanged.

- **Pacing.** `CombatPacing.Wait(seconds)` first calls `CombatPresentation.Boundary()`, then records `Pace(seconds, kind)` and returns a cached `WaitForSeconds(0f)`, which is one frame. The playback divides by the speed when it plays, so a speed change applies to beats already queued.
- **Movement.** `CharacterController.MoveAlongPath` takes `CombatPacing.StepSeconds(secondsPerStep)`, which is 0.01 while holding, so each step is still one frame. Right after `GridPosition = nextCell.Coords` it calls `CombatPresentation.RecordStep(this, fromCell, nextCell.Coords, startPos, endPos, secondsPerStep)`, which also counts as a boundary. Its rules half is untouched: the occupancy clear, `IsStepRefused`, `RunAfterStepHooks`, the incapacitation checks and `NotifyCharacterMovement`.
- **Boundaries.** `Boundary()` records a `Keyframe` for every `TokenView` whose model differs from its last keyframe. A keyframe holds the sprite, colour, `enabled`, `flipX`, lossy scale, world position and `activeInHierarchy`, plus the `StatusEffectIndicator` icon data (built by its existing builder), the blur-active flag, and the grapple-blink partner instead of the blink's momentary `enabled`. Boundaries fall at every pacing wait, at every step, before every control or interactive beat, and at group close. Live play only renders the state at a yield, and these are the yields of a group (the player-answer loops are S3), so the keyframe sequence equals what live play would have shown.
- **The `CombatUI` facade.** Every Queued-class method records a beat with values snapshotted at call time and returns:
  - `ShowCombatLog` records text; `ScenarioHooks.CombatLog` stays its first statement;
  - `AddTurnSeparator`; `SetTurnIndicator` records text;
  - `UpdateInitiativeDisplay` records the string built at call time, never rebuilt;
  - `SetActivePC` / `SetActiveNPC` record the index plus the creature, through new creature overloads used by `SingleNPCTurnFromInitiative` and `StartPCTurn`. At playback the creature is resolved against the last applied panel snapshot, so an `NPCs.RemoveAt` cannot shift the highlight;
  - `SetActionButtonsVisible`;
  - `UpdateAllStats4PC` (and the legacy `UpdateAllStats*` / `UpdateStats`) records a panel snapshot per row (creature, name, HP text, bar fill and colour, AC, dead mark), through a split of `CharacterInfoPanel.UpdateCharacterStats` into `Capture` and `Apply`;
  - (no summon panel exists: `CombatUI` has no summon panel add or remove method; summons appear in the NPC rows that `UpdateAllStats4PC` fills).
- **Cells.** `SquareCell.SetHighlight(HighlightType)` and `SetHighlight(Color)` and `SquareCell.ClearHighlight()` (it disables the custom overlay; `PersistentAreaEffect`, `WallOfFireAreaEffect` and `GameManager.SpellCasting` call it) store the value at once and record a `CellHighlight` beat while queueing; the renderer is written at the beat. Nothing reads a cell's highlight back (no getter exists), and `PersistentAreaEffect.IsGridHighlightApplied` is the area's own flag.
- **Area and one-shot visuals.** `PersistentAreaEffect.CreateVisualIndicator` creates its indicator with renderers disabled while queueing and records `AreaShown`; its destruction records `AreaHidden` and destroys the detached indicator at the beat. `GustOfWindEffect.SpawnWindLineVisual` and similar one-shot visuals go through `CombatPresentation.Defer(closure over values)`. Step 8 scans for every renderer created in AI-reachable spell and area code and files any left as a UI reveal (next free UI ID).
- **Views.** A model created while queueing gets its view hidden until a `Spawned` beat. The model itself is created at rules time, because `CharacterController.Awake` draws `_blurVisualTimeOffset` from the global dice stream. A model destroyed while queueing records `Removed`; its view keeps showing its last keyframe until then, plays the summon despawn effect if it has one, and destroys itself.
- **Queue while behind.** While the queue is not empty, every facade, cell and area call is queued, even after the group has closed. Passthrough resumes only when the queue is empty, so order is preserved across a close.
- **Tags.** Every beat carries `seq` (rules order) and the (actor, round) that `TurnService` reports at record time; `_currentCharacter` is set before `OnTurnStarted`. Round-wrap beats (`OnNewRound`, `ReachInitiativeCount` ticks) carry the new round and the previous actor, and they always serialize.
- **No live references.** Nothing recorded is a reference into a live rules list or a live-state reader. A creature that is gone at playback is skipped, and a highlight for it is cleared.
- **No rules effects.** `CombatPresentation` never draws `Random`, never writes rules state, and never calls `SetActive` or `Destroy` on a model. A source scan pins this.

### Run-ahead state kept hidden

| Live surface | Treatment |
|---|---|
| Token sprite, colour, scale, position, `enabled`, active state | The model's `SpriteRenderer.forceRenderingOff = true`. The view copies the model every `LateUpdate` while live, and shows keyframes while held. |
| Blur pulse (`CharacterController.LateUpdate`), grapple blink (`RunGrappleAlternatingDisplay`) | Live: copied. Held: the view reproduces the pulse or alternation from the keyframe flags. Both are cosmetic. |
| Status icons and their hover tooltip | The `StatusEffectIndicator` container is parented to the view. Its `LateUpdate` rebuild is skipped while its view is held, and it applies each keyframe's icon data, so the tooltip shows the shown state. |
| Summon aura, duration bar, `[S]` badge, summon and despawn flashes | `SummonedCreatureVisual` lives on the view object; callers reach it through the controller's view. |
| `FlamingSphereEntity` (moved by `TryControlFlamingSphereForAI`) | It gets a `TokenView` too. |
| Stat panels, initiative bar, banner, highlights, action panel | Value beats. |
| Cell highlights and persistent area visuals | The cell gate and the area beats. |
| Character sheet, skills and inventory windows | Closed when a group opens (`CloseInventoryIfOpen` plus the sheet and skills close, C6). Their hotkeys (I, K, C in `InputService.HandleKeyboardInput`, before the gate) are refused while `BlocksInput`. `CharacterSheetUI.RefreshIfOpen` then has nothing to refresh. |
| Invisibility alpha (`RefreshInvisibilityVisualForObserver`, observer `ActivePC ?? CurrentCharacter`) | Captured by the keyframes as the value it had at rules time, which is exactly what live play shows. |
| Hover tooltip, path, AoE and charge previews | Already gated by `CanProcessWorldInput`. |

### Playback

`CombatPresentation` is a MonoBehaviour on its own GameObject, created lazily (CORE-009). It is not on GameManager, so CORE-013's `StopAllCoroutines` never kills it silently; `Clear()` is explicit.

It consumes the queue as a stream: it never shows a beat past the recording head, it waits when it catches up, and it runs on scaled `Time.deltaTime`. The mode `playback=instant` (the harness default in fast mode) applies every available beat in the same frame's `LateUpdate`; `timed` is the default in play and in watch mode.

**Sequential playback (step 9).** Beats play in `seq` order. A `Pace(s)` beat lasts `s / speed`, and a step lasts `secondsPerStep / speed`. The picture is stage 1's; only the run-ahead and the input hold differ.

**Overlapped playback (step 11).** Only token steps play in parallel lanes, one lane per turn. Every other beat (the result track) serializes. The scheduler (`PlaybackScheduler`) is a pure class with no Unity calls.
- **R1. Rules order is `seq`.** A lane is admitted only once its turn is fully recorded, that is once a beat with another (actor, round), a stop or a close has arrived. So the scheduler knows whether the lane holds an S2 mark before it starts it.
- **R2. The result track plays strictly in `seq` order.** It holds log, separator, banner, initiative, highlights, panels, keyframes, cells, area beats, Spawned and Removed beats, the control beat and interactive beats. Beats are spaced by `Pace / speed` while only one lane is admitted in the window, and by `min(Pace / speed, Stagger)` while two or more are. Stagger is 0.15 s divided by the speed.
- **R3. Per token, beats play in `seq` order.** A step starts only after the same token's previous beat has finished.
- **R4. A step into cell c waits for earlier beats on c.** It starts only after every earlier-`seq` beat that vacates c or changes the token there has played. So a creature walking into a square freed by a fallen ally starts after that fall's keyframe.
- **R5. Segments wait for results.** Within a lane, a run of consecutive steps is a segment. Segment k+1 waits for the result-track beats recorded between segments k and k+1, so an AoO hit mid-move shows before the mover continues. A lane's result beats wait for that lane's earlier segments, so an attack shows after the walk arrives.
- **R6. Lane starts are staggered.** A lane starts no earlier than Stagger after the previous lane started.
- **R7. A window ends at an S2 mark, at S5, and at a group close.** The lane holding the mark starts after every earlier beat has played and plays with `Pace / speed` (no Stagger); the next lane opens a new window. Interactive and control beats are barriers: nothing after them starts before they finish.
- **R8. At most 3 lanes play at once.**

Guarantees: no beat shows before a beat it depends on in rules order; the log shows the same lines in the same order as live play; per token, the keyframe sequence equals live play's.

### Drains and clears

- **`WhenDrained(Action)`** runs the action at once if the queue is empty, otherwise when playback empties it.
- **An animated drain (S1, S4)** plays the rest normally. `Holding` ends at the close, and `BlocksInput` ends when the queue is empty.
- **`CatchUpNow()`** applies every remaining beat instantly, in order: views snap to their latest keyframes, the log lines are appended, the panels set. It is used for the S3 fallback, before harness reads (Input) and in `Clear()`.
- **`Clear()`** drops pending `WhenDrained` actions, applies the latest cell and area state, snaps every view to its model and destroys views whose models are gone, sets the kind to Player, and empties the queue.

### The frame cost of run-ahead

Recording costs about one frame per wait and one per square: roughly 20-40 frames for an orc's turn, about 0.5 s at 60 fps. That is far below the playback length, so streaming hides it.

The floor is deliberate. It keeps the frame partition that the 14 `Update()` area trackers and `AreaAimSearch`'s per-`Time.frameCount` cache rely on. Removing it needs an explicit tracker flush and is out of scope.

### Input

`CombatPresentation.BlocksInput` is true while holding or while the queue is not empty. It is checked:
- as the first test of `GameManager.CanProcessWorldInput`, which is `InputService`'s `canProcessInput` delegate and is also used by `GameManager.Update`;
- in `InputService.HandleKeyboardInput`, for the I, K and C toggles;
- in `GameManager.OnEndTurnButtonPressed`. `IsPlayerTurn` is already true during an S1 drain; the action panel is hidden until the control beat anyway.

`IsPlayerTurn` is not changed, because rules branch on it (`EndCurrentTurn`). During an S1 drain the phase is already `PCTurn` with sub-phase `ChoosingAction` at rules time; only the gate holds input back.

**Harness (test code):**
- `UiTurnDriver`'s ready test adds `!gm.PresentationBlocksInput`, an internal accessor in the editor-only harness partial;
- the three prompt answerers (`HandleCounterTripPrompt`, `HandleEscapeOppositionPrompt`, `HandleOverrunResponsePrompt`) answer only when `!PresentationBlocksInput`, so a queued dialog is never answered before it shows;
- the runner calls `CatchUpNow()` before `SweepLogLeak` and `CleanCheck`;
- the harness sets `SuppressPlayerInput`, so the hold itself is tested by a Play-mode static suite (`PresentationHoldTests`).

### The pacing service and the speed factor (stage 1)

**`CombatPacing`** (static, `Combat/Pacing/CombatPacing.cs`):
- `WaitForSeconds Wait(float seconds)`. It always returns a `WaitForSeconds`, never an `IEnumerator`, `CustomYieldInstruction` or null. `InlineAction` passes it as a leaf, and `RunAIActionNow` skips it with its existing warning.
- `float StepSeconds(float secondsPerStep)`.
- `PaceKind Kind {Player, Ai}`, set only by the `TurnControl` marks.
- `float Speed`, which reads `GameSettings.aiTurnSpeed`.

| Mode | `Wait(s)` | `StepSeconds(x)` |
|---|---|---|
| Live, Player kind | `new WaitForSeconds(s)`, as today | `x` |
| Live, Ai kind | `new WaitForSeconds(s / Speed)` | `x / Speed` (still clamped to 0.01 by `MoveAlongPath`) |
| Holding | `Boundary()`, a `Pace(s)` beat, the cached `WaitForSeconds(0f)` | 0.01, plus `RecordStep` |

**Which waits it covers.**
- It routes the 92 pacing waits, re-counted on batch 14's final code.
- **Exempt** (a source-scan allowlist): the two `RunGrappleAlternatingDisplay` waits (animation) and the three debug `CheckMenuAfterDelay` waits.
- **Not pacing, so unchanged:** the 13 `yield return null` polling loops (player answers and sub-action settles), the `SmoothScaleCoroutine` and `SummonedCreatureVisual` tweens, the `WaitUntil` in `AI_GrappleRestrictedTurn`, the `WaitWhile` in `RunAttackSequence`, `CombatLogPanel.ScrollToBottomNextFrame`, `PreCombatInventoryUI` and `DarknessAreaEffect.ReapplyDarknessHighlightsNextFrame`.
- **Durations stay at the call sites.** Many are variables: `secondsAfterEachAoO` is 1.0 for the PC walk and 0.5 for AI movers, `ChargeCore`'s `AoODelay` is 0.8 or 0.5, the crawl's `aooWait` is 0.8 Ui and 0.5 AI, and `AfterAttackDelay` takes 0.5-2.0 s. The service multiplies; it never chooses.

**Scope of the factor (C2).** The factor applies while the kind is Ai: from an AI-decided turn's mark until the next control return. That covers the waits inside shared rules coroutines (the grapple waits, the forced-behaviour controllers, an AoO dwell during an AI mover) without changing their yield structure. PC-turn pacing is untouched: `AfterAttackDelay`, `DelayedEndActivePCTurn`, `ReturnToActionChoicesAfterDelay`, `PromptFullAttackStep`, and an NPC's AoO dwell during the PC's walk.

**Neutrality.**
- **Speed 1, outside a group:** every call returns today's object, so step 1 is PURE.
- **The speed factor:** in fast mode a speed change barely changes frame counts, so a fast-mode equality alone proves little. The new harness `mode=fine` (timeScale 3, captureFramerate 30, so 0.1 s per frame; wall cap x5) makes a 0.6 s wait 6 frames at 1x, 2 frames at 4x and 1 frame in Holding. Fine-mode equality is the proof that frame counts feed no rule.
- **Before that proof,** steps 2 and 4 remove the known time readers, and step 4 pins the rest in a source-scan allowlist (it must also list the cosmetic and diagnostic readers already in game code at 5176586: `MoveAlongPath`'s and `SmoothScaleCoroutine`'s `Time.deltaTime` tweens, the blur pulse's `Time.time` in `CharacterController.LateUpdate`, `CameraController`, the dormant `CommandProcessor`'s `Timestamp`, and the `frame={Time.frameCount}` stamps in `Debug.Log` lines of `GameManager`, `GameManager.CombatEnd`, `GameManager.LootCollection` and `SupportActions`): `AreaAimSearch`'s `Time.frameCount` cache, `PlanarTravelSystem`'s gate `Time.time` (out of scope), `TurnUndeadSystem.AppliedTime`, the dormant `CreatureTrapSystem`, and `MoveAlongPath`'s own `startPos` read.

### Combat end and reset

**Victory.** `HandleCombatVictoryDetected` keeps at rules time:
- `CurrentPhase = CombatOver`, so the `phase.by` frame names in the trace stay identical;
- the anchor clear and the XP snapshot;
- `BeginPostCombatLootCollection`'s treasure and appraisal dice, `AddGold`, its log lines, and `WaitingForLootCollection = true`, which blocks `Update` input;
- the pending poison secondaries (step 2).

What moves: `TurnControl.CombatEnded()` closes the group; the banner and button hide are Queued beats; only `LootCollectionUI.Open(...)` moves into `CombatPresentation.WhenDrained(...)`, inside `BeginPostCombatLootCollection`, which covers both callers (`HandleCombatVictoryDetected` and `OnCombatEnded`).

**Defeat.** `HandleCombatDefeatDetected` and `ShowDefeatScreen` set `WaitingForDefeatChoice` at rules time. Its log line and banner are queued, and `CombatUI.ShowDefeatPanel` is an interactive barrier. The player sees the killing blow, then the screen.

**Reset and halt.** `ResetCombatStateForNextEncounter` calls `CombatPresentation.Clear()` next to its other clears. `Harness_HaltCombat` (after its `StopAllCoroutines`) and `Harness_ResetWorld` call it too. `ScenarioRunner.CleanCheck` asserts an empty queue, no pending `WhenDrained` action, every view synced to its model, and no orphan view.

**Reused slots (CRE-046).** A view follows its model's active state and sprite every frame while live, so `ResetForNewCreature` and `ResetCharacterSlotForSpawn` need no change; `TokenViewTests` checks a reused pool slot.

### Options

| Option | Field (`GameSettings`, session-only; the game has no saves) | Default in play | Harness |
|---|---|---|---|
| Group AI turns | `groupAiTurns` | on (Q1, from step 11) | `group=` off, on or pair; default off |
| AI turn speed | `aiTurnSpeed` (1, 1.5, 2, 3, 4) | 2x (Q4, from step 6) | `aiSpeed=`, default 1 |
| Playback | none | timed | `playback=` instant or timed; instant in fast mode |
| Time resolution | none | not applicable | `mode=` fast, watch or fine |
| Camera | none | stays where the player left it (Q2) | not applicable |

- **Constants in `PlaybackScheduler`:** Stagger 0.15 s, 3 lanes.
- **Controls:** a code-built "AI speed" button (step 6) and a "Group AI turns" toggle (step 11) on the combat HUD, wired in `SceneBootstrap.WireButtons`, plus `DebugCommands` entries.
- **Harness options:** `RunOptions.Parse` and `ToString` gain the cases; `ScenarioRunner.Setup` and `Cleanup` save and restore both fields beside `hpCalculationMode`. `group=pair` runs `Repeat=2`, with rep 1 off and rep 2 on, compared through the existing `RepeatCheck` and `FirstDiffSeq`.

### Composition with batch 14 (built first)

- **Step 1 (`AiDecisionTrace`, `AiDecisionTimer.BeginTurn`/`EndTurn` in `AIService.ExecuteNPCTurn`).** Its scopes close before any wait and never wrap an `Execute*` call or playback (batch 14's own rule). Recording only shrinks waits, so decision time is unchanged. Keying by actor already covers CORE-012 chains, and turns stay strictly sequential, so `BeginTurn`/`EndTurn` never nest differently. Group wall time is not decision time and is never judged by the 50 ms budget. `ScenarioTrace.HarnessVersion` stays as batch 14 leaves it; nothing is added to `meta` or to any hashed event.
- **Steps 5-6 and 9-10** (the blend sites, `AiManeuverChooser`, `AiLegal`, `ActionContext.Probe`, `AiOutOfReach.Run`) are synchronous decision code. Any wait they add is picked up when our step 1 re-greps.
- **Step 11 (`ChooseAfterStandardAction` at the wait-and-`yield break` sites, moving through `RunAIAction(Move)`).** It changes the very lines that hold AIService waits, so our step 1 runs on batch 14's final code and trusts no line numbers or counts. Its moves go through `MoveAlongPath`, so they are recorded with no extra work, and their AoOs from player-run threateners hit S2. `MovementLegalityTests.TestMoveSiteClassification` is re-read, never hard-coded; we add no move site.
- **Step 13 (`ChooseProneStart`, the crawl through `ActionIds.Crawl`).** The stand-up's AoOs (`ResolveStandUpFromProne`, with its own dwell) are S2. The crawl's `aooWait` goes through `CombatPacing`.
- **Step 14 (`TryNPCDomainPowerForAI` through `RunAIAction`):** any wait it adds is caught by the re-grep.
- **Sidecars.** `groups.jsonl` and `presentation.jsonl` go beside `caststages.jsonl` (`ScenarioRunner.WriteCastStages`), written after `ai-timing.tsv`. `soak-rows.tsv` is untouched, because `SoakStats.MergeRuns` reads it by column index. `JobResult.Grouping`, `JobResult.GroupCheck` and `JobResult.PresentationCheck` are new `summary.json` fields, never trace events.
- **The equality gate doubles as a batch 14 check.** The on/off and fine-mode equalities run on batch 14's final behaviour, so they also prove its neutral parts stay neutral under the new pacing.
- **Baseline.** Batch 14's closing regression is the baseline: the follow-up regression of 2026-11-23 on b739140 (`docs/TESTING.md` 3.2, batch 20261123-232643), which supersedes the final regression's hashes for soak seeds 38 and 48 (AI-091). If the editor does not reproduce those hashes, step 1 first records a fresh baseline at its parent commit (batch 14's own rule).

### Files (new unless marked; Unity generates the metas)

Unity must import new C# files to generate their metas. Commit each generated `.meta` with its file; never hand-write one.

| File | Content |
|---|---|
| `Combat/Pacing/CombatPacing.cs` | `Wait`, `StepSeconds`, `Kind`, `Speed`, the exemption list |
| `Combat/Pacing/TurnControl.cs` | `AiTurnStarting`, `PlayerControlReturned`, `CombatEnded`, `PromptOpened`; forwards to pacing and presentation |
| `UI/Presentation/TokenView.cs` | `[DefaultExecutionOrder(10000)]` mirror on its own root GameObject (never a `CharacterController`): Live copy, Held mode, `Apply(keyframe)`, step animation, the Spawned and Removed beats, the summon visual host |
| `UI/Presentation/CombatPresentation.cs` | MonoBehaviour singleton on its own GameObject: `Holding`, `BlocksInput`, `HasPendingInteractive`, `Boundary`, `Record*`, `MarkStop`, `Defer`, `WhenDrained`, `CatchUpNow`, `Clear`, the playback loop, the `CombatUI` class table |
| `UI/Presentation/PresentationBeat.cs` | Beat kinds, value snapshots, `Keyframe` |
| `UI/Presentation/PlaybackScheduler.cs` | The pure R1-R8 scheduler |
| `UI/Presentation/PresentationTrace.cs` | Editor and development sink of applied events per channel; writes the unhashed `presentation.jsonl` (the `ActionSetShadow` pattern) |
| `Tests/Combat/CombatPacingTests.cs` | Mode math, kinds, leaf type, the `new WaitForSeconds` source scan, the time-reader allowlist scan |
| `Tests/Combat/PoisonTimingTests.cs` | Step 2 |
| `Tests/Magic/SummonDespawnTimingTests.cs` | Step 4 |
| `Tests/UI/TokenViewTests.cs` | Play pass |
| `Tests/UI/CombatPresentationTests.cs` | Queue order, queue-while-behind, drains, `Clear`, the `CombatUI` classification reflection test, the no-`Random` and no-rules-write scan of `UI/Presentation/` |
| `Tests/UI/PresentationHoldTests.cs` | Play pass: input, hotkeys and End Turn refused while `BlocksInput` |
| `Tests/UI/PlaybackSchedulerTests.cs` | The pure scheduler |
| `Tests/Scenarios/Catalog/GroupedTurnScenarios.cs` | The scenarios named in the steps |

Add each new static suite to `tools/tests/static-suites.json` with its pass.

### Changed members (checked names)

- **Turn flow.** `GameManager.OnTurnStarted` (NPC branch, `_Core/GameManager.cs`) and `GameManager.BeginForcedPCTurn` (`_Core/GameManager.TurnStart.cs`): `TurnControl.AiTurnStarting`. `GameManager.ShowActionChoices`: `TurnControl.PlayerControlReturned` before `CombatUI.ShowActionButtonsForCharacter`. `HandleCombatVictoryDetected`, `HandleCombatDefeatDetected`, `OnCombatEnded`: `TurnControl.CombatEnded`. `SingleNPCTurnFromInitiative`, `StartPCTurn`: the creature overloads of the highlights.
- **Waits.** Every pacing `new WaitForSeconds` becomes `CombatPacing.Wait`, in `AIService`, `GameManager.NPCTurns`, `GameManager.CombatActions`, `GrappleSystem` (4), the four `*BehaviorController`s, `AiOutOfReach`, `GameManager.ResolveActionProvocation`, `GameManager.ResolveStandUpFromProne` and `SupportActions`, plus whatever batch 14 adds.
- **Movement.** `CharacterController.MoveAlongPath` uses `StepSeconds` and calls `RecordStep`.
- **Stops.** `ThreatSystem.ExecuteAoO` and `GameManager.RunAttackedReaction`: `MarkStop`. `GameManager.PromptAttackedReaction`: `TurnControl.PromptOpened`, then the pre-show wait.
- **Poison (step 2).** `ActivePoison`, `PoisonData.GetSecondaryDamageDelaySeconds` (becomes rounds), `GameManager.UpdatePoisonTimers` (removed from `Update`), `OnInitiativeCountReached`, `OnInitiativeCountRemoved` (the heir), and a combat-end helper called from both end handlers and `OnCombatEnded`.
- **Rules off frames (step 4).** `GameManager.DespawnSummonWithEffect` keeps its name (TST-006); its rules removal and `Destroy` happen before any yield, and the effect is on the view. `LesserGlobeOfInvulnerabilityAreaEffect.Update` and `OnRoundStart`, and the globe creation in `GameManager_Spells_L` and `GameManager_Spells_Phase1`: the centre comes from `Grid.GetCenteredWorldPosition(Caster.GridPosition, Caster.GetVisualSquaresOccupied())`.
- **Escape square (step 5).** `GrappleSystem.OfferFreeAdjacentMovement` and `TryOfferFreeAdjacentMovementAfterGrappleEnds` gain an `ActorSource?` source; `AI_GrappleRestrictedTurn` passes AI.
- **Tokens (step 3).** `CharacterController.Awake` creates the view after its existing draw (no new `Random`) and sets `forceRenderingOff`. `StatusEffectIndicator`: the container parent becomes the view; held mode. `SummonedCreatureVisual` is hosted on the view, with its callers updated. `FlamingSphereEntity` gets a view.
- **UI (steps 8-11).** `CombatUI`: the Queued, Interactive and Control classes, the creature highlight overloads, the HUD controls. `CharacterInfoPanel.UpdateCharacterStats`: split into `Capture` and `Apply`. `SquareCell.SetHighlight` (both overloads) and `SquareCell.ClearHighlight`. `PersistentAreaEffect.CreateVisualIndicator` and its destruction. `GustOfWindEffect.SpawnWindLineVisual`.
- **Combat end, reset and input.** `GameManager.BeginPostCombatLootCollection`: `Open` through `WhenDrained`. `ResetCombatStateForNextEncounter`: `Clear`. `CanProcessWorldInput`, `InputService.HandleKeyboardInput`, `OnEndTurnButtonPressed`: `BlocksInput`.
- **Harness (test code).** `ScenarioRunner.RunOptions.Parse` / `ToString`; `Setup` / `Cleanup` (save and restore the two settings); `Finalize` (`pair` via `Repeat=2`; `JobResult.Grouping`, `GroupCheck`, `PresentationCheck`); `CleanCheck`; `WriteCastStages` (the two sidecars); `ScenarioFastMode.Apply("fine")`; `UiTurnDriver` ready and the three answerers; `Harness_PendingPrompt` (`HasPendingInteractive`); `Harness_HaltCombat` / `Harness_ResetWorld` (`Clear`). No new `ScenarioHooks`.

### `CombatUI` method classes

Every public `CombatUI` method is classed in a table in `CombatPresentation`.
- **Queued (value beats):** `ShowCombatLog` (after the hook), `AddTurnSeparator`, `SetTurnIndicator`, `UpdateInitiativeDisplay`; `SetActivePC` / `SetActiveNPC` and their creature overloads; `SetActionButtonsVisible`; `UpdateAllStats4PC` and the legacy stats variants (`UpdateAllStats`, `UpdateAllStatsMultiNPC`, `UpdateStats`).
- **Control:** `ShowActionButtonsForCharacter`.
- **Interactive barrier:** `ShowConfirmationDialog`, `ShowDefeatPanel`. `LootCollectionUI.Open` goes through `WhenDrained`.
- **CatchUp fallback (unexpected while behind):** every other prompt, menu or picker show: `ShowSpecialStyleSelectionMenu`, `ShowAoOConfirmationPrompt`, `ShowTouchSpellPrompt`, `ShowBullRushExtraPushChoice`, and the item, character and spell pickers.
- **Live:** queries (`IsSpecialStyleSelectionMenuOpen`, `TurnIndicatorText` reads, `IsSpontaneousCast` and the related fields), every `Hide*`, every `Ensure*`, and the out-of-combat panels.

`CombatPresentationTests.TestCombatUiMethodsClassified` reflects over `CombatUI` and fails on any unclassed public method (72 public methods at HEAD by grep). This is how a future prompt is covered without new code.

### Tests that must still pass unchanged

- `CastCoreStageOrderTests.TestCoreSourceTouchesNoUi`: the cast core is not touched.
- `ActionCatalogTests.TestExecutorSourceScan`: no executor is touched.
- `ActionCatalogTests.TestInlineAction`: the same yield object passes through.
- `MovementLegalityTests.TestMoveSiteClassification`.
- `rules/combat-log-pool`: pinned to `group=off`, because it reads the panel's line objects in the same frame.

## Owner questions (the plan applies the default unless the owner answers before the named step)

- **Q1. Default for grouping (step 11).**
  - **Default:** on in play, with a HUD toggle. The harness stays off unless the option is given.
  - **Alternative:** off by default, opt-in.
- **Q2. Camera during playback (step 11).**
  - **Default:** it stays where the player left it, as today, and shows the whole group's moves; no code moves the camera.
  - **Alternative:** follow the leading lane's actor (new camera code).
- **Q3. Stage 1 only, or the full overlap?**
  - **Default:** both. Stage 1 (steps 1-6) first, closing at the midpoint (step 7), so the speed gain lands early even if the overlap needs more tuning.
  - **Alternative:** stage 1 only. Steps 8-12 can be dropped without undoing stage 1; the batch then closes at step 7 with docs.
- **Q4. AI speed default and choices (step 6).**
  - **Default:** 2x, chosen from 1, 1.5, 2, 3 and 4x, session-only, on the combat HUD.
  - **Alternatives:** 1x (opt-in only); `PlayerPrefs`; a settings panel.
  - **Owner answer (2026-11-24):** the 2x default is fine; the player must be able to set normal speed. The step 6 button already cycles through 1x, which reads "AI Normal" since then (`GameSettings.AiTurnSpeedLabel`).
- **Q5. Floating combat text** (damage, healing, misses, condition names over tokens).
  - **Default:** not in this work; filed as a follow-up UI request. Overlapped playback reads from the log, the panels and the tokens.
  - **Alternative:** build it in step 11.
- **Q6. Ingested poison secondary delay (step 2 files it).**
  - DMG p.296-297 defines secondary damage at 1 minute after exposure for every type; the code used 10 minutes for ingested poisons (before the CMB-307 fix).
  - **Default (superseded by the owner answer below):** unchanged at 100 rounds until the owner rules. Ingested poisons are hard to use in combat (DMG), so the effect is small.
  - **Alternative:** 10 rounds, like every other type.
  - **Owner answer (2026-11-24):** follow RAW: the secondary comes 1 minute (10 rounds) after exposure for every type, ingested ones included (DMG p.296-297, PHB p.138). Built in the step `gt-ingested-poison-raw` (CMB-307 fixed, entry deleted): `PoisonData.GetSecondaryDamageDelayRounds` returns `PoisonTiming.SecondaryDelayRounds` for every type and `PoisonTiming.IngestedSecondaryDelayRounds` is gone; scenario `rules/poison-ingested-secondary-after-ten-rounds`.

**Settled by the owner:** a player-run creature's AoO stops a group (the request names attacks of opportunity). This plan builds it as C1, a window stop: the AoO turn plays sequentially, the rules do not pause, and the player gives no input to an AoO. It extends the same stop to the acid spray that the AI fallback takes for a player-run beetle.

## Steps

Every C# step runs `bash tools/compile_check.sh` and expects exit=0, errors=0 and the warning baseline then in force (batch 14's final count); a jump is a regression.

Each step keeps the docs true in the same commit:
- **Issues.** Delete fixed entries in `docs/issues/<PREFIX>.md`; update the counts and Top issues in `docs/KNOWN_ISSUES.md`. File new issues at the next free ID at the time: another workflow may have taken IDs since this plan (the IDs below are the ones free at HEAD).
- **Rules.** Record each delegated RAW reading in `docs/systems/RULES_COVERAGE.md` 4.1 with its date, and report it.
- **System docs.** `docs/architecture/combat-and-grid.md` (the presentation layer and pacing), `docs/TESTING.md` (options, sidecars, `mode=fine`), `docs/DEVELOPMENT_RECIPES.md` ("Add a combat wait" and "Add a `CombatUI` method"), and `docs/systems/PC_NPC_PARITY.md` (step 10).

House rules for every step:
- New `.cs` files get their Unity-generated `.meta`; never hand-write one.
- Grep `Assets/Scripts/Tests` before renaming or deleting a member (TST-006).
- Write "not verified in Play mode" in commit bodies for anything only read.
- New scenarios go in `Tests/Scenarios/Catalog/GroupedTurnScenarios.cs`.

**TEST BUDGET rule for every step** (batch 13's rule):
- Run the new and touched scenarios and the touched subsystem's, in a fresh Play session (`QueueFresh`), plus the static suites covering the touched code, edit and Play passes.
- A *broad* step also runs `smoke/*` seeds 1-3.
- A PURE or NEUTRAL step compares every pre-existing hash with the baseline or the previous step's run. A behaviour step explains each moved hash with `ScenarioTraceDiff` (`docs/TESTING.md` 3.4).
- The full sweep and the soak run only in the two regression steps, except where a step's budget names a soak subset.
- Never run scenarios in a Play session that ran static suites.

### 1. The pacing service (`gt-pacing-service`)

IDs: owner request 2026-10-30; CORE-012 (noted, not changed). PURE, *broad*.

1. Add `CombatPacing` (Live branch only, `Speed` fixed at 1) and `TurnControl`, whose marks only set `Kind`.
2. Re-grep on batch 14's final code: 92 pacing waits at 5176586, plus any batch 14 adds (step 11's after-cast moves, step 14's domain powers; step 13's AI crawl goes through `ActionIds.Crawl`, whose `aooWait` already exists at 5176586 in the crawl core of `GameManager.ActionCores.cs` and feeds the counted `ResolveMovementAoOsBeforeStep` wait). Trust no line numbers or counts from this plan.
3. Route every pacing `new WaitForSeconds(x)` through `CombatPacing.Wait(x)`. Keep each site's seconds and variables, and each wait inside its coroutine exactly where it is. List the exemptions (the two grapple-blink waits, the three `CheckMenuAfterDelay` waits) in the source scan.
4. `MoveAlongPath` uses `StepSeconds`.

Tests:
- new `CombatPacingTests`: Live returns a `WaitForSeconds` with the caller's seconds; the return is never an `IEnumerator`; a source scan finds no `new WaitForSeconds` in game code outside `CombatPacing.cs` and the allowlist;
- `ActionCatalogTests` (`TestInlineAction`, `TestExecutorSourceScan`) and `MovementLegalityTests` unchanged.

TEST BUDGET: `smoke/*` and `rules/*` seeds 1-3, plus `soak/dmg-random-encounters` seeds 1-10. Every job hash must equal the baseline (broad, because every AI wait is touched).

Built 2026-11-24. The re-grep on batch 14's final code (b6a380a) found 105 game-code `new WaitForSeconds`, not 97: `AIService` has 56 (batch 14 added 8), the other files as listed above. 100 are pacing and now go through `CombatPacing.Wait`; the 5 exempt ones are in `CombatPacing.ExemptWaitSites`. No new `WaitForSeconds` came with the crawl (its `aooWait` is a variable fed to the counted `ResolveMovementAoOsBeforeStep` wait). The marks are placed at their named sites and only set `CombatPacing.Kind`; `CombatEnded` is also marked in `ResetCombatStateForNextEncounter` (the harness reset goes through it), and `PromptOpened` leaves the kind and has no caller until stage 2, since the factor runs until control returns (C2).

### 2. Poison secondaries in rounds (`gt-poison-rounds`)

IDs: CMB-007 (fixed, entry deleted); a new CMB issue (the next free CMB ID when the step runs), "Ingested poison secondary at 10 minutes; DMG p.296-297 defines secondary damage at 1 minute after exposure", left open for Q6 (filed as CMB-307; fixed after the owner's answer of 2026-11-24). Behaviour change, narrow.

1. `ActivePoison` counts rounds instead of seconds: 10 for a 60 s delay, and 100 for the ingested 600 s delay, unchanged until Q6 (answered 2026-11-24: 10 for every type, CMB-307). `PoisonData.GetSecondaryDamageDelaySeconds` becomes a rounds getter (grep `Tests/` first, TST-006).
2. Anchor the count like durations (`TurnDurations`, CMB-006): at the count of the creature whose turn it was at exposure, moving to the heir when that creature leaves initiative (`OnInitiativeCountReached`, `OnInitiativeCountRemoved`). The secondary rolls when its count is reached 10 rounds later.
3. `UpdatePoisonTimers` and its `Time.deltaTime` countdown leave `GameManager.Update`.
4. **Delegated RAW reading** (record in `docs/systems/RULES_COVERAGE.md` 4.1, dated, and report it): a secondary still pending when combat ends resolves at combat end, in initiative order, after `CurrentPhase = CombatOver` and before the loot window, because the minute passes before the next encounter. A combat-end helper is called from `HandleCombatVictoryDetected`, `HandleCombatDefeatDetected` and `OnCombatEnded`.
5. `GetStatusSummary` shows rounds.
6. Check the `PlanarTravelSystem` `Time.time` gates (a 9th-level spell) and note them out of scope.

Why it is here: with real-time poison, any speed factor or recording would change when secondaries fire.

Tests:
- new `PoisonTimingTests`: 10 rounds; the anchor and the heir; extra frames and open menus have no effect; combat-end resolution;
- new scenarios `rules/poison-secondary-after-ten-rounds` and `rules/poison-secondary-pending-at-combat-end`.

TEST BUDGET: the new scenarios; every catalog scenario whose creatures or items carry poison (grep the catalog for `PoisonOnHitId` creature ids and `Poison`), seeds 1-3; `smoke/*` seeds 1-3. Explain each moved hash with `ScenarioTraceDiff`; the first divergence must be a secondary save.

Built 2026-11-24. `ActivePoison.RoundsUntilSecondary` and `DurationAnchor` (`TurnDurations.CurrentAnchor` at exposure) replace `TimeUntilSecondary`; `PoisonData.GetSecondaryDamageDelayRounds` (10; ingested 100 until the owner's Q6 answer of 2026-11-24 made it 10 too, CMB-307) replaces the seconds getter (no test used it); the new `Effects/PoisonTiming.cs` holds the rule: `TickAtCount` from the end of `GameManager.OnInitiativeCountReached`, `Reanchor` from `OnInitiativeCountRemoved` (the durations' heir), `TickAtRoundBoundary` from `OnNewRound` for unanchored poisons, and `ResolvePendingAtCombatEnd` through `GameManager.ResolvePendingPoisonSecondariesAtCombatEnd` (`GameManager.CombatEnd.cs`), called after `CurrentPhase = CombatOver` in `HandleCombatVictoryDetected` (before `BeginPostCombatLootCollection`), `HandleCombatDefeatDetected` (before `ShowDefeatScreen`) and `OnCombatEnded` (before its cleanup and loot); the victims come in the live initiative order, or `TurnService.OrderAtLastCombatEnd` (new: `EndCombat` clears the order before raising `OnCombatEnded`), then any other active creature; it returns how many saves it rolled, and both victory routes then re-check the party side (`TurnVictoryIntoDefeatIfPartyOut`: a secondary that drops the last PC opens the defeat screen instead of the loot window, delegated RAW reading 2026-11-24); `TurnService` clears the saved order in `StartCombat` and `ForceResetWithoutCallbacks`, so it only holds the fight that just ended. A dead victim makes no save (delegated RAW reading 2026-11-24). `ActivePoison.SaveDC` keeps the DC figured at exposure, so the secondary uses a wild-shaped druid's own DC as the initial save does (DMG p.296, CHR-020). A secondary that resolves mid-fight refreshes the stat panels (`UpdateAllStatsUI`). `UpdatePoisonTimers` is gone from `GameManager.Update` (also CORE-021, its per-frame list). The ingested delay is CMB-307 (Q6). `PlanarTravelSystem`'s `Time.time` gate (`ExpiryTime`, Gate, a 9th-level spell) was checked and stays out of scope. Scenario catalog grep: the poison-carrying jobs are `rules/save-natural-one`, `-twenty` and `rules/save-bonuses` (`ApplyPoison` in a step), `rules/monster-alignment-data` (medium spider), `rules/summon-creature-entries` (giant bee) and `rules/caster-archetype-without-spells-fights-ai` (couatl); all their hashes and `smoke/*`'s stayed equal to step 1's, so no hash moved and no `ScenarioTraceDiff` was needed (the soak, which may move, runs at the midpoint regression).

### 3. Tokens render through a mirror view (`gt-token-view`)

IDs: none (it prepares steps 4 and 8); CRE-046 checked. NEUTRAL (presentation only).

1. Add `TokenView`, Live mode only, on its own root GameObject.
2. `CharacterController.Awake` creates it after the existing `Random` draw (no new draw) and sets the model's `SpriteRenderer.forceRenderingOff`.
3. The `StatusEffectIndicator` container parents to the view.
4. `SummonedCreatureVisual` is hosted on the view; its callers (`DespawnSummonWithEffect`, the spawn site, the `SetDuration` callers) reach it through the controller.
5. `FlamingSphereEntity` gets a view.
6. The view follows its model's active state and destruction.
7. Scan for any other object parented to a token transform and re-parent it or list it. Confirm that nothing reads the model renderer's `isVisible` or `bounds` (only the status icons' bounds, now on the view).

Tests:
- new `TokenViewTests` (Play pass): the mirror copies sprite, colour, `enabled`, `flipX`, scale and position after `LateUpdate`; destruction and inactive state follow; a reused pool slot shows the new sprite (CRE-046); a summon's aura and duration bar sit on the view;
- `CleanCheck` gets its views-synced and no-orphan checks.

TEST BUDGET: `smoke/*` seeds 1-3, plus `rules/summon-*`, `rules/*grapple*`, `rules/*enlarge*`, `rules/*reduce*`, `rules/*invisib*`, `rules/*flaming-sphere*` and `rules/*lesser-globe*` seeds 1-3. All hash-identical. The owner's visual check in Play is listed as not verified. Rollback: stop creating views and setting `forceRenderingOff`.

Built 2026-11-24. `UI/Presentation/TokenView.cs`: a root GameObject `<model> (view)` with an always-active root (so its LateUpdate keeps running) and a child `Body` that mirrors the model's active state, world position, rotation and lossy scale and carries the drawn SpriteRenderer (sprite, colour, enabled, flips, sorting layer and order, material, draw mode and size); `Release` (from the model's `OnDestroy`, and from `LateUpdate` when the model is gone) destroys it at the end of the frame; `TokenView.Live` and `IsSyncedWithModel` serve the clean check and the tests. `CharacterController.Awake` creates it after the `_blurVisualTimeOffset` draw and sets `forceRenderingOff`; `CharacterController.View`, `SummonVisual` and `EnsureSummonVisual` are new. `StatusEffectIndicator.Start` parents its container to the view's Body (and its `OnDestroy` removes it). `SummonedCreatureVisual` is added to the Body by `EnsureSummonVisual` at the summon spawn (`SpawnSummonedCreature`), and `DespawnSummonWithEffect`, `TickSummonDurations`, `RegisterActiveSummon` and the dormant `SummoningService` reach it through `SummonVisual`; its `OnDestroy` removes the objects it built. `FlamingSphereEntity` creates a sprite-less view and builds its quads on it; its `OnDestroy` releases the view. The scan found nothing else parented to a token transform (the area visuals, hover marker, gust streaks and ground-item tokens have their own roots or the cells) and no reader of a token renderer's `isVisible` or `bounds` (only the status icons' own bounds). One deviation from "no change" in the slot reset: `ResetForNewCreature` destroyed every non-kept component, the summon visual included, which now lives on the view, so it calls `TokenView.ClearSummonVisual` (CRE-046 rule for per-creature state; summons are never pooled today, so it is a guard). Test side: `ScenarioRunner.CleanCheck` checks that every controller has its own view, every live view is synced and none outlives its model; `StaticSuiteRunner` does not count a view as a leak (its model is counted) and destroys a view whose model is gone; new scenario `rules/token-view-mirrors-tokens`. Review fixes (2026-11-24): the clean check and the scenario run the view's own copy before they compare (a coroutine resumes before `LateUpdate`, so a model changed earlier in the frame, a size animation or the grapple blink, would otherwise mark the session dirty); `ClearSummonVisual` destroys at once and `EnsureSummonVisual` trusts only its field and copies the model first; `CharacterController.OnDestroy` releases the view first; `TokenView.Create` returns null outside Play mode; `StatusEffectIndicator` rebuilds a lost container; diagnostics name the model and creature (`TokenView.Describe`). The plan's `rules/*enlarge*`, `*reduce*`, `*flaming-sphere*` and `*lesser-globe*` filters match no scenario id; the budget ran `rules/*summon*`, `*grapple*`, `*size*`, `movement-aoo-withdraw-large*`, `scroll-casting-time-*`, `*invisib*`, `*globe*` and `*sphere*` instead, which hold the jobs that use those effects.

### 4. Rules off wall time and token lifetime (`gt-rules-off-frames`)

IDs: a new CRE issue (the next free CRE ID when the step runs), "Summon removal waits on the 0.45 s despawn tween", filed and fixed in this step (the ID is reserved by bumping Next ID, cited in the commit, and no entry is kept); CORE-013 narrowed (a despawn no longer has rules work that a reset can lose). Behaviour change, narrow.

1. `DespawnSummonWithEffect` does all its rules work before any yield, that is synchronously at its `StartCoroutine` call: occupancy; `NPCs` and `_npcAIBehaviors` together (AI-015); the summon lists; `RemoveFromInitiative`; the log line; `UpdateInitiativeUI` and `UpdateAllStatsUI`; `Destroy(model)`. A summon can be removed during its own turn: slain by an AoO on its move, killed by a reaction to its attack, or dying in `ProcessEndOfTurnHPState` inside `NextInitiativeTurn`. In that case `TurnService.RemoveFromInitiative` would leave the index on the next creature and the turn's `AdvanceToNextTurn` would skip it. So the removal of the current turn's entry (`index == _currentInitiativeIndex && _currentCharacter == combatant`) marks the turn vacated and the advance does not step past the creature that now fills the slot, so that the next creature still acts (the plan first said to step the index back one; the step 4 review replaced that, since a second removal in the same turn then skipped a creature). A `SummonDespawnTimingTests` case and `rules/summon-slain-on-own-turn-next-still-acts` pin it. The view plays `PlayDespawnEffect` and destroys itself, so a lingering token is never found by `FindObjectsOfType<CharacterController>` in area tracking and never counted by `IsActiveCombatant`.
2. The Lesser Globe centre comes from the caster's grid square (`Grid.GetCenteredWorldPosition(Caster.GridPosition, Caster.GetVisualSquaresOccupied())`), at creation, in `Update` and in `OnRoundStart`. The PHB centres the globe on the caster but calls it immobile (PHB p.236): the following was kept pending owner question SPL-335 (found in the step 4 review; the plan first called it RAW), and the owner's ruling of 2026-11-24 (globe immobile, as RAW) removed the re-centring in `Update` and `OnRoundStart` after step 7, so the centre is now read once, at creation. Expected hash-identical in fast mode, where the token is already at its square when `Update` reads it.
3. Add the time-reader scan to `CombatPacingTests`: `Time.deltaTime`, `Time.time`, `Time.frameCount` and creature `transform.position` reads outside `UI/` and `UI/Presentation/` must be on an allowlist (the pacing service section).

Tests:
- new `SummonDespawnTimingTests`: removal in the same frame; the despawn line before the next creature's turn-start lines; the view lingers and plays;
- new scenarios `rules/summon-slain-leaves-initiative-at-once`, `rules/summon-expires-leaves-at-once`, `rules/lesser-globe-follows-caster-grid` (since the SPL-335 fix `rules/lesser-globe-stays-put`).

TEST BUDGET: `rules/*summon*`, `rules/*globe*` and the new scenarios, seeds 1-3; `smoke/*` seeds 1-3; soak seeds 1-10. Each moved hash is explained by the earlier summon removal.

Written 2026-11-24, CRE-312 reserved (Next ID CRE-313, no entry). `GameManager.DespawnSummonWithEffect` yields nothing: it detaches the view first (`TokenView.PlayDespawnAndRelease`, which leaves `TokenView.Live`, drops the model and plays `PlayDespawnEffect` on the view's own coroutine before destroying itself; `Release` is a no-op on a despawning view), then clears the occupancy and sets `CharacterController.IsRemovedFromPlay` (reset by `ResetForNewCreature`; `SquareCell.AddOccupant` refuses such a creature, so a movement unwinding in the same frame cannot put it back), removes `NPCs` and `_npcAIBehaviors` together, the summon lists and the initiative entry, logs the unchanged despawn line, updates the UI and calls `Destroy`. `TurnService.RemoveFromInitiative` stepped the index back one when the removed entry is the current turn's creature (replaced by the vacated turn in the review fixes below); `StartTurnAtCurrentIndex` returns when a round-boundary removal empties the order; the round-boundary removal of `TickSummonDurations` keeps the old index rules. `TickAllSpellDurations` walks a copy of `NPCs`, since an expiry that kills a summon now removes it at once. The Lesser Globe centre is `LesserGlobeOfInvulnerabilityAreaEffect.CasterCenter` (the caster's grid square through `GetCenteredWorldPosition`), at creation (`GameManager_Spells_L`, `GameManager_Spells_Phase1`), in `Update` and in `OnRoundStart`. The time-reader scan (`CombatPacingTests.TestTimeReadersAllowlisted`) allowlists 22 readers outside `UI/` and `Tests/` (each with its file, method or whole file, kind, count and reason); its one offender, `HealerAIProfile`'s frame-keyed cure cache (batch 14 step 12), is filed as AI-095 and kept as a known failure, not allowlisted. The harness gained adopted actors (`ScenarioBuilder.Adopted`, `ScenarioContext.Adopt`; `GameManager.Harness_SummonIntoFight`, `Harness_IsActiveSummon`) for the summons the scenarios bring in, and a fourth scenario, `rules/summon-slain-on-own-turn-next-still-acts` (a player-run and an NPC-path summon, each slain by an AoO on its own move; the next creature still acts). Review fixes (2026-11-24): the index step-back became a vacated turn (`TurnService.VacatedTurnOwner`: the index stays on the slot the next creature fills, `Count` at the last slot so the round wraps, and `AdvanceToNextTurn` skips its increment once; later removals in the same turn keep the old rules; `GetNextCharacter` and the initiative bar's marker follow it; the -1 index is gone); `StartTurnAtCurrentIndex` re-checks its entry after `ReachInitiativeCount`, so a tick at a count that removes that count's creature lets the next one act (the round wraps at the last slot); `TurnService.IsCurrentTurnOf` and `GameManager.IsCurrentTurnOf` keep the turn's owner after it left the order, and the fleeing, confused and suggested forced-PC-turn tails end the turn on it instead of Unity's null-or-destroyed equality on `CurrentCharacter`; a turn whose creature left play before its end-of-turn processing (an AoO on its own move, a reaction to its own attack) publishes no `TurnEndedEvent` (no trace `turn_end`) and runs no end-of-turn work for it, while one that dies in the end-of-turn HP check (`ProcessEndOfTurnHPState` inside `NextInitiativeTurn`) still gets both; `AIService.ExecuteNPCTurn` closes the decision timer and trace records and the held-charge plan by reference; `IsActiveCombatant` (both copies) and the scene-wide area scans skip `IsRemovedFromPlay`; `RequestDismissSummon` removes the record first and ends the turn of a summon dismissed during its own turn (who may dismiss, and at what cost, filed as SPL-336; since the SPL-336 fix of 2026-11-24 only the caster dismisses, on its own turn, so that branch is gone); the Lesser Globe's following is filed as owner question SPL-335 instead of being called RAW; the time-reader scan also counts `Stopwatch`, `DateTime.Now`/`UtcNow`, `Environment.TickCount` and `localPosition` (one more entry: `AiDecisionTimer`'s clock, a diagnostic). The implementation and the review could not run in Unity (the Unity MCP was attached to another project); the budget ran on 2026-11-24 once it was attached here (docs/TESTING.md 3.2): Unity generated the new script's `.meta`; static edit pass OK; Play pass OK after four test and baseline fixes (the CombatPacingTests known-failure match made a real prefix of its label; SummonDespawnTimingTests reads the HashSet summon lists as `IEnumerable`, pads `_npcAIBehaviors` to `NPCs` as an encounter would, and captures the despawn line through `Debug.unityLogger.logHandler`), `SummonDespawnTimingTests` 45/45, `CombatPacingTests` 82/83 (AI-095 known); scenarios after two scenario fixes (`rules/summon-expires-leaves-at-once` tests the destroyed dog by reference; `rules/summon-slain-on-own-turn-next-still-acts` walks the summons out in round 2, when the orc and the guard are no longer flat-footed, PHB p.137): `rules/*summon*` 36 Pass, `rules/*globe*` 27, `smoke/*` 9, soak 1-10 10 Pass; every pre-existing rules and smoke hash equal to step 3's, soak seed 3 moved only by a summoned scorpion's expiry line, now before the next `turn_start` at the round boundary (`ScenarioTraceDiff`, index 272). Second review fixes (2026-11-24): a player-run summon that left play during its own turn off the catalog move (a grapple-escape move's or a provoking cast's AoO, a reaction to its own attack) left the turn open, since `ActivePC` is null on a vacated turn; `GameManager.IsVacatedPlayerTurn` now ends it by reference in `ShowActionChoices`, `EndCurrentTurn` (through `EndActivePCTurn`, so the dismiss confirm and the forced-turn tails run the PC cleanup), `AfterAttackDelay` and `DelayedEndActivePCTurn`, and the grapple move tails ask `IsCurrentTurnOf`; `TurnService.AddToInitiative` keeps the index for an insertion at the next creature's index during a vacated turn; `Harness_DespawnSummon` marks the summon out of play and releases its view; the scenario checker's turn-structure invariant reads `IsRemovedFromPlay`; new scenario `rules/summon-slain-off-its-move-next-still-acts`. Run (docs/TESTING.md 3.2): static Play pass OK (`SummonDespawnTimingTests` 54/54), `rules/*summon*` 39 Pass, `rules/*grappl*` 63, `smoke/*` 9, every pre-existing hash equal.

### 5. The escape square of a forced grappling PC (`gt-escape-square-decider`)

IDs: a new CMB issue (the next free CMB ID when the step runs), filed and fixed: `OfferFreeAdjacentMovement` asks `ManeuverDeciders.For(actor)` with no source (G4, against batch 13 C7). Behaviour change, narrow (a parity bug).

1. First reproduce the leak with the scenario below. If it does not reproduce, close the step with docs only.
2. `OfferFreeAdjacentMovement` and `TryOfferFreeAdjacentMovementAfterGrappleEnds` take the caller's `ActorSource?`. `AI_GrappleRestrictedTurn` passes AI, which covers the NPC grapple turn and `RunGrappleEscapeForFlight`. The PC button path passes none, like `StartGrappleMoveChoice(uiFlow)`.

Tests: new scenario `rules/panicked-grappled-pc-escape-square-ai`: a Ui PC, panicked while grappling, escapes; no square prompt opens, and the AI profile's square is used.

TEST BUDGET: the new scenario; `rules/*grapple*`, `rules/*flee*`, `rules/*panic*` seeds 1-3.

Built 2026-11-24 (CMB-308, filed and fixed). The scenario `rules/panicked-grappled-pc-escape-square-ai` (`GroupedTurnScenarios.cs`; the Scripted grapple-drill orc grapples the Ui hero at (10,10) and panics it; the orc in the shared square threatens every escape square and an Idle guard at (8,10) adds a second threat on the west ones; the ally at (13,10) is nearest to the east squares, so the AI decider's square is (11,9) by the y tie-break; new probe `GameManager.Harness_IsEscapeSquareSelectionOpen`) reproduced the leak before the fix (run 20261124-140211, 3 Fail): the forced turn opened the "choose one" selection, made no escape move, ran from the shared square and left the selection open into the next turn. CMB-308 was filed. Fix: `OfferFreeAdjacentMovement(actor, formerOpponent, source, out autoMove)` and `TryOfferFreeAdjacentMovementAfterGrappleEnds(..., source, out autoMove)` pass the source to `ManeuverDeciders.For(actor, source)`; `AI_GrappleRestrictedTurn` passes `ActorSource.AI` (the NPC grapple turn, `RunGrappleEscapeForFlight`, `RunGrappleActionForAI`, and the grappling gate before an attack in `GameManager.NPCTurns.cs`, so a confused or charmed forced PC turn whose attack becomes a grapple action also takes the AI square), `FinalizeGrappleActionResolution` (the PC grapple menu) passes null. Mechanics unchanged, only the decider. No test reflected on either member. Verified 2026-11-24 (`QueueFresh` batch 20261124-141514): the new scenario Passes on seeds 1-3 with the escape move to (11,9), and all 93 pre-existing jobs of `rules/*grappl*`, `rules/*flee*` and `rules/*panic*` keep their hashes; the grapple static suite's Play pass is OK (docs/TESTING.md 3.2). CMB-308 was deleted as fixed.

### 6. The AI speed factor (`gt-ai-speed`)

IDs: owner request 2026-10-30; Q4. NEUTRAL (stage 1 ships here).

1. `GameSettings.aiTurnSpeed` (default 2x), read by `CombatPacing.Speed` for kind Ai, waits and steps.
2. The code-built "AI speed" HUD button, wired in `SceneBootstrap.WireButtons`, and a `DebugCommands` entry.
3. Harness: `aiSpeed=` (default 1, saved and restored in `Setup` / `Cleanup`) and `ScenarioFastMode.Apply("fine")` (`mode=fine`: timeScale 3, captureFramerate 30, wall cap x5).

Tests: `CombatPacingTests` gains the factor and kind cases; a forced PC turn counts as Ai; `ShowActionChoices` resets the kind to Player; the PC hand-off waits stay at 1x; the step floor.

TEST BUDGET:
- (a) fast mode: `rules/*` seeds 1-2 and soak seeds 1-10 at `aiSpeed=1` and `aiSpeed=4`, job-by-job equal;
- (b) `mode=fine`: `smoke/*`, `rules/*summon*`, `rules/*poison*`, `rules/*grapple*`, `rules/*globe*`, and the area filters (`rules/*cloud*`, `*fog*`, `*mist*`, `*tentacles*`, `*darkness*`, `*daylight*`, `*sleet*`, `*sphere*`, `*wall-of-*`, `*wind-wall*`), seeds 1-2 at `aiSpeed=1` and `aiSpeed=4`, equal.

The listed `rules/*cloud*`, `*mist*`, `*tentacles*`, `*darkness*`, `*daylight*` and `*wind-wall*` match no scenario id (2026-11-24). The fine-mode subset that ran, and that step 7.4 and step 9 reuse, is this exact `QueueFresh` filter, seeds `1-2`, once with options `mode=fine` and once with `mode=fine;aiSpeed=4`:

`smoke/*,rules/*summon*,rules/*poison*,rules/*grapple*,rules/*globe*,rules/*fog*,rules/*sleet*,rules/*sphere*,rules/*wall-of-*,rules/*area*,rules/*grease*,rules/*entangle*,rules/*web*,rules/*silence*,rules/*zone*,rules/*consecrate*,rules/ai-speed-*`

Any difference is a frame or time rule: file it and fix it before step 8.

Built 2026-11-24. `GameSettings.aiTurnSpeed` (default `DefaultAiTurnSpeed` 2, choices `AiTurnSpeedChoices` 1, 1.5, 2, 3, 4; `NextAiTurnSpeed`, `CycleAiTurnSpeed`, `AiTurnSpeedLabel`) is read through `GameSettings.CurrentAiTurnSpeed`, which never creates the settings object (edit mode and an unset session read the default) and reads a value off the list as the default. `CombatPacing.Speed` reads it; the pure rule `CombatPacing.Scaled(seconds, kind, speed)` divides only under the Ai kind (a speed of 1 or less never scales), and `Wait` and `StepSeconds` apply it (`MoveAlongPath` still clamps to 0.01 s). The marks were already at their sites (step 1), so the PC hand-off waits stay at 1x with no change of their own. The "AI speed" button sits on the combat log's title bar, which stays shown during AI turns (`CombatUI.AiSpeedButton`, `AiSpeedLabel`, `RefreshAiSpeedButton`; built in `SceneBootstrap`'s combat log section, wired in `WireButtons` to `DebugCommands.CycleAiTurnSpeed`, which Left Ctrl+J also calls). Harness: `RunOptions.AiSpeed` (`aiSpeed=`, default 1, in `ToString` only when not 1), set and restored in `ScenarioRunner.Setup` and `Cleanup` beside `hpCalculationMode`; `mode=fine` (`ScenarioFastMode.Apply("fine")`, timeScale 3, every job's default wall cap times `RunOptions.FineWallCapFactor` 5, an explicit `wallCap=` as given). New scenario `rules/ai-speed-pacing-kind-follows-turns` and the `CombatPacingTests` cases. AI-023 (no AI speed setting) is fixed and deleted. Verified the same day (docs/TESTING.md 3.2): (a) fast mode `rules/*` seeds 1-2 and soak seeds 1-10 at 1x and 4x, every job hash-equal; (b) fine mode at 1x and 4x, every job hash-equal, for the named filters that match an id (`smoke/*`, `*summon*`, `*poison*`, `*grapple*`, `*globe*`, `*fog*`, `*sleet*`, `*sphere*`, `*wall-of-*`) and, since `*cloud*`, `*mist*`, `*tentacles*`, `*darkness*`, `*daylight*` and `*wind-wall*` match no scenario id, `*area*`, `*grease*`, `*entangle*`, `*web*`, `*silence*`, `*zone*` and `*consecrate*` in their place. No frame or time rule was found.

### 7. Midpoint full regression (`regression-midpoint`)

Kind regression. No game code. Run, through the Unity MCP:
1. the static runner in edit mode, then in Play mode, against `tools/tests/static-suites.json`: verdict OK;
2. `ScenarioHarness.QueueFresh("rules/*,smoke/*", "1-5", "")` and the soak `QueueFresh("soak/*", "1-50", "perSession=10")` at the defaults: every hash equals the baseline, except the explained moves of steps 2, 4 and 5;
3. `rules/*` seeds 1-2 and soak seeds 1-10 at `aiSpeed=4`, equal to `aiSpeed=1`;
4. the fine-mode subset of step 6 (its exact filter string, under its TEST BUDGET) at `aiSpeed=1` versus 4;
5. batch 14's `AiTiming` p95 and max show no jump against its step 1 baseline (wall-clock values; batch 14's budget rule files an issue on a jump).

Record in `docs/TESTING.md` 3.2. If the owner takes stage 1 only (Q3), the work closes here with docs, including CLAUDE.md "Current state".

Run 2026-11-24 on 063ce4e (docs/TESTING.md 3.2, 3.4 and 3.5), on an editor restarted with `tools/restart_unity.ps1`. (1) Static edit pass 20261124T200024Z-edit OK (12 suites, 322 passed, 3 known failures); Play pass 20261124T200033Z-play OK (163 suites, 12770 passed, 79 known failures, 0 regressions, 0 stale). (2) `QueueFresh` batch 20261124-150055: `rules/*` seeds 1-5 (run 20261124-150100: 3020 jobs, 3009 Pass, 5 Known SPL-155, 6 Inconclusive at their round caps as at batch 14's closing regression, 0 Fail), `smoke/*` 15 Pass, soak seeds 1-50 (`perSession=10`, runs -150853 to -151002) 50 Pass. Against batch 14's closing regression (runs 20261123-232648, -233402, -233412 to -233517): 3033 hashes equal, 50 new (the ten scenarios of steps 2-6), 2 moved: soak seed 3 (step 4's explained move, the same hash as step 4's run) and soak seed 48 (`ScenarioTraceDiff` index 388: the summoned scorpion's expiry line now before the werewolf's round-12 `turn_start`, so the werewolf attacks the rogue instead of the vanished scorpion; the fight then ends in a Stalemate at the cap instead of a Victory in 19 rounds). Steps 2 and 5 moved no hash of the full set. (3) Batch 20261124-151142: `rules/*` seeds 1-2 and soak seeds 1-10 at `aiSpeed=4` hash-equal to the 1x jobs (1208 of 1208, 10 of 10). (4) The fine-mode subset at 1x and 4x: 170 of 170 jobs hash-equal, and equal to fast mode. These two checks found no frame or time rule in the jobs they cover, but they leave out soak seed 48, whose new hash depends on a frame boundary: the scorpion vanishes and the werewolf's `turn_start` snapshot still lists both as Grappled in one frame (155), the end-of-frame `OnDestroy` releases the grapple, and the werewolf decides in the next frame (CRE-311). Seed 48 alone at 1x, `aiSpeed=4`, `mode=fine` and `mode=fine;aiSpeed=4` (batches 20261124-152845 and -152933) keeps run 20261124-151002's hash, since each run still decides at least one frame after the despawn. Step 9's run-ahead may remove that frame, so CRE-311 is to be fixed before step 9 and seed 48 under `group=pair` is in step 9's TEST BUDGET (Risks). (5) Soak `aiTiming` p50 1.01 ms, p95 12.34 ms, max 42.1 ms over 801 turns (batch 14 step 1 baseline 0.96, 12.13, 53.1): no jump; 3 warm-up turns over 50 ms (seed 21, a warning). Nothing new filed (CRE-311 updated); no baseline entry, waiver or scenario expectation changed. Q3 stays at its default (both stages): stage 2 starts at step 8. Not verified by hand in Play mode: the AI speed button, a real fight at 2x and the despawn fade.

### 8. The presentation facade, Passthrough only (`gt-presentation-gates`)

IDs: none; new UI issues (from the next free UI ID) only for reveals the renderer scan leaves. PURE (Passthrough only).

1. `CombatPresentation` and `PresentationBeat`, Passthrough only: every call applies at once, in the same order and the same frame.
2. The Queued, Control and Interactive facade in `CombatUI`, with the creature highlight overloads.
3. The `CharacterInfoPanel` `Capture` / `Apply` split.
4. The `SquareCell.SetHighlight` gate (both overloads) and `SquareCell.ClearHighlight` (the overlay clear the area effects call).
5. The `PersistentAreaEffect` indicator hooks, and `Defer` for one-shot visuals (`GustOfWindEffect.SpawnWindLineVisual` and the rest the scan finds).
6. The `StatusEffectIndicator` held-mode API, unused.
7. `Boundary()` keyframe capture, used only by `PresentationTrace` in editor and development builds, and the `presentation.jsonl` sidecar. In Passthrough it is called at the same points as in Holding (`CombatPacing.Wait`, each `MoveAlongPath` step, before the control beat and every Interactive-class call, and in `TurnControl.CombatEnded`), as trace only that never changes the returned yield, so step 9's `PresentationCheck` compares rep 1 (off) and rep 2 (on) at the same boundaries.
8. The renderer scan of AI-reachable spell and area code.

Tests: `CombatPresentationTests`: Passthrough is immediate; the classification reflection test; the no-`Random` and no-rules-write scan.

TEST BUDGET: `smoke/*` seeds 1-3 and `rules/*` seed 1, hash-equal to step 7's run. Keep their `presentation.jsonl` as the Passthrough baseline.

Built 2026-11-25 (not committed in the step run). New files under `UI/Presentation/`: `CombatPresentation.cs` (a MonoBehaviour singleton on its own lazily created root `CombatPresentation`, never on GameManager, Play mode only; `Mode` is always `Passthrough`, `Holding` false, and `Queueing` is true only while beats wait, so in Passthrough every facade applies at once; `Enqueue` is internal, for step 9 and the queue test; `Boundary`, `RecordStep`, `Defer`, `AreaShown`, `AreaHidden`, `WhenDrained`, `CatchUpNow`, `Clear`, `ApplyBeat`; `CombatUiClasses` and `ClassOf` class every public `CombatUI` method as Queued, Control, Interactive, CatchUp or Live), `PresentationBeat.cs` (`BeatKind`, `PresentationBeat`, the panel snapshot rows, `TokenKeyframe`) and `PresentationTrace.cs` (editor and development builds only, records only while the harness records). `CombatUI`: the Queued methods (`ShowCombatLog` with `ScenarioHooks.CombatLog` still its first statement, `AddTurnSeparator`, `SetTurnIndicator`, `UpdateInitiativeDisplay`, `SetActivePC`, `SetActiveNPC`, `SetActionButtonsVisible`, the three `UpdateAllStats*`, `UpdateStats`, `AddCombatMessage`, `LogAttack`, `LogDamage`) test `CombatPresentation.Queueing` and otherwise call their `Apply*` body; `ShowActionButtonsForCharacter` (Control) and `ShowConfirmationDialog` and `ShowDefeatPanel` (Interactive) call `Boundary` first. The new overloads `SetActivePC(int, CharacterController)` and `SetActiveNPC(int, CharacterController)` carry the creature; `StartPCTurn` and `SingleNPCTurnFromInitiative` call them. `CharacterInfoPanel`: `CaptureAllStats`, `CaptureAllStats4PC` and `CaptureAllStatsMultiNPC` read in the old order into a `PanelSnapshot`, `Apply(PanelSnapshot)` writes; the old `UpdateAllStats*` are `Apply(Capture...())`. `SquareCell`: both `SetHighlight` overloads and `ClearHighlight` store the requested state (`RequestedHighlight`, `RequestedOverlayOn`, `RequestedOverlayColor`, for step 9's clear) and go through the gate to `ApplyHighlight`, `ApplyOverlay` and `ApplyOverlayClear`. `PersistentAreaEffect.CreateVisualIndicator` reports `AreaShown`; `ReleaseIndicator` (a re-creation, and `ExpireEffect` before its deferred `Destroy(gameObject)`, so the hide is in rules order) asks `AreaHidden` (false in Passthrough, so the indicator is destroyed as before; true would detach it for the presentation), and `OnDestroy` asks as the fallback of the direct destroy sites. `GustOfWindEffect.SpawnWindLineVisual` builds its streaks through `CombatPresentation.Defer` (`BuildWindLineVisual`). `StatusEffectIndicator` gained the held-mode API (`Hold`, `ApplyHeld`, `ReleaseHold`, `CaptureIcons`, `CaptureIconSignature`; unused). `Boundary` is called in `CombatPacing.Wait`, after each `MoveAlongPath` step (`RecordStep`), before the control beat and every Interactive-class call, and in `TurnControl.CombatEnded`; it returns at once unless the trace records, and then records the boundary and each live `TokenView`'s keyframe (position, active, sprite, colour without the pulse tint, the blur flag and alpha, the grapple partner through the new pure read `CharacterController.PeekGrappleOpponent`, since `TryGetGrappleState` ends stale links, and the status icons), kept only when it changed. `ScenarioRunner` starts the trace with the job and writes `<run>/<job>/presentation.jsonl` after `caststages.jsonl` (`WritePresentation`, also on the error path); the file is not hashed and `soak-rows.tsv` is unchanged. Renderer scan of AI-reachable spell and area code: the tokens, summon visuals, the Flaming Sphere view, the condition tint and the invisibility alpha are drawn by the views or keyframed; the Darkness and Wall of Fire highlights go through the cell gate; the persistent area indicators through the area hooks; Gust of Wind through `Defer`; the path preview, hover marker and Imbue panel are not reachable from an AI turn. One reveal is left: the ground-item token (`SquareCell.AddGroundItem` / `RemoveGroundItem`, reached by an AI disarm or drop), filed as UI-058 for step 9. Tests: `Tests.UI.CombatPresentationTests` (`play`, 70/70; the classification test re-counts 75 public `CombatUI` methods under 72 names; the no-`Random` and no-rules-write scan of `UI/Presentation/`); `CastCoreStageOrderTests.TestCoreSourceTouchesNoUi` unchanged; `StaticSuiteRunner` keeps the `CombatPresentation` root. Verified 2026-11-25 (docs/TESTING.md 3.2): compile 0 errors, 505 warnings; static edit pass OK (322 passed, 3 known), Play pass 20261125T142715Z-play OK (164 suites, 12862 passed, 79 known failures, 0 regressions); `rules/*` seed 1 (run 20261125-092807: 606 Pass, 1 Known) has 606 of 607 hashes equal to step 7's run and the later fix runs, the one move (`rules/spell-buff-dispatch` seed 1) SPL-335's recorded globe change (the creation line and the dropped per-round area line, nothing else); `smoke/*` seeds 1-3 (run 20261125-092947) 9 of 9 equal. Their `presentation.jsonl` files are the Passthrough baseline. Not verified by hand in Play mode; the Holding path is exercised only by the queue-order test.

After review (2026-11-25): the trace is fault-isolated (`Boundary`, `RecordStep` and every `PresentationTrace` entry point catch, write one `error` line, warn and stop recording; the fault is a job note), every job end stops it (`ScenarioRunner.Cleanup`, `ErrorResult` without a trace, `ScenarioSessionGuard`), the sidecars' write failures reach `summary.json` (they go on the result's notes), trace strings are built only while recording, the rarely used Queued closures got their own `BeatKind.QueuedClosure` (no extra `defer` line, so both paths trace alike), and the area indicator's hide moved into `ReleaseIndicator` as above. Limit for step 9: the `OnDestroy` fallback cannot hand the indicator over (Unity is already destroying it as the effect's child), so step 9 makes the direct destroy sites (`AreaEffectManager.ClearAllEffects`, the spell handlers that destroy an area object) release the indicator first, or call `ExpireEffect`. Also for step 9: a keyframe taken during an Enlarge or Reduce Person scale tween (wall-time) can differ between two runs of the same job (3 of 607 rules sidecars after review), so `PresentationCheck` compares scales at rest or with a tolerance. Re-verified after review (docs/TESTING.md 3.2): `CombatPresentationTests` 77/77, Play pass OK, `rules/*` seed 1 and `smoke/*` seeds 1-3 all hash-equal to the first step 8 runs.

### 9. Run-ahead with sequential playback (`gt-run-ahead`)

IDs: owner request 2026-10-30; CORE-013 (playback lives off GameManager; noted); CORE-002 (noted, not fixed). NEUTRAL (grouping off by default).

1. Holding pacing: `WaitForSeconds(0f)`, `Pace` beats, `StepSeconds` 0.01, `RecordStep`.
2. The group opens and closes from the `TurnControl` marks, with queue-while-behind. Until step 10, a `ShowConfirmationDialog` call (the counter-trip, overrun-response and escape-opposition prompts) made while holding or behind calls `TurnControl.PromptOpened()` and `CatchUpNow()` before it shows. Otherwise a defender prompt would wait on a held group that the gated harness answerers never answer.
3. View Held mode, the Spawned and Removed beats, and the `StatusEffectIndicator` hold. Revisit step 3's view checks here: `ScenarioRunner.CleanCheck` and `rules/token-view-mirrors-tokens` run the view's Live copy (`SyncFromModel`) before they compare, which a held view must not do.
4. Sequential playback, a single lane.
5. S1: the control beat; `BlocksInput` in `CanProcessWorldInput`, the I, K and C hotkeys and `OnEndTurnButtonPressed`; the windows closed at group open (C6).
6. S4: the `WhenDrained` loot open, the queued banner, the defeat-panel barrier.
7. `Clear` in `ResetCombatStateForNextEncounter`, `Harness_HaltCombat` and `Harness_ResetWorld`.
8. Harness: `group=off|on|pair` and `playback=`; the `UiTurnDriver` gate and the answerer gates; `CatchUpNow` before the log-leak sweep and `CleanCheck`; `CleanCheck` queue-empty; the `groups.jsonl` sidecar (group id, lanes as round and actor, stop kind and seq, beats, playback seconds); `JobResult.Grouping`; a runner-side `ExpectGroups` that writes `JobResult.GroupCheck`, never a trace event; `JobResult.PresentationCheck`, comparing the `presentation.jsonl` of rep 1 and rep 2 (the log channel and each token's keyframe sequence must be equal).

Tests:
- `CombatPresentationTests`: queue order, queue-while-behind, drains, `WhenDrained` at once on an empty queue, `Clear`;
- new `PresentationHoldTests` (Play pass);
- new `pair` scenarios: `rules/group-three-orcs-before-ui-fighter` (the Ui driver sees an ordinary turn start after the drain), `rules/group-confused-pc-inside`, `rules/group-dying-pc-skipped-inside`, `rules/group-cornered-frightened-pc-gets-menu`, `rules/group-charmed-pc-ends-group`, `rules/group-summon-slain-inside`, `rules/group-spans-round-wrap`, `rules/group-victory-mid-group` (the CombatOver phase, the `by` names, no `combat-end-detected` violation), `rules/group-defeat-mid-group`, `rules/group-stalemate-halt`, `rules/group-lesser-globe-caster-moves`.

TEST BUDGET: the new scenarios; `smoke/*` and `rules/*` seeds 1-3 as `group=pair`, with `RepeatCheck` "same" and `PresentationCheck` "same"; the fine-mode subset of step 6 as `group=pair`, seeds 1-2; and the CRE-311 canary, `soak/dmg-random-encounters` seed 48, as `group=pair`, hash-equal with grouping on and off and to the CRE-311 fix's run of it, 20261125-085010 (hash 651e227; docs/TESTING.md 3.2). CRE-311 is fixed (2026-11-25: `DespawnSummonWithEffect` ends the summon's grapple and feint links before `Destroy`, so no decision in the despawn's frame sees them): Holding pacing resolves turns with far fewer frames between them, and before the fix that seed's werewolf could again have decided while the vanished scorpion's grapple still stood. The fix moved that seed's hash once, against step 7's run 20261124-151002, through the round-12 Grappled removals, the werewolf's `turn_start` snapshot and its `turn_end` diff only (`ScenarioTraceDiff` first divergence at seq 391; the same Stalemate, attack and dice).

Built 2026-11-25 (not committed in the step run). Holding: `CombatPresentation` (`UI/Presentation/CombatPresentation.cs`) gains `Mode` Holding, `Holding`, `Playback` (Timed or Instant), `Queueing` (holding or behind, never while a beat applies), `BlocksInput`, `HasPendingInteractive`, `PendingBeats`, `PendingDrainActions`, `CurrentGroup` and `GroupLog` (`GroupRecord`: id, lanes `round:actor`, stop `GroupStop` Control, Prompt, CombatEnd or Reset, seq span, beats, Timed playback seconds; `ResetGroupLog`); `OpenGroup` (holds every live view with a keyframe), `NoteLane`, `CloseGroup` (a last keyframe; drains at once on an empty queue), `HoldingWait` (a Pace beat), `RecordFinalKeyframe`, `RecordSpawned`, `RecordRemoved`, `RecordStep` (a Step beat while queueing), `CatchUpNow`, `Clear` (closes as Reset, drops the queue and the drain actions unshown, gives cells, ground items and area indicators their latest state, destroys a removed view and releases every hold) and `Pump`, the single-lane playback in its `LateUpdate` (a Pace beat holds the head for its seconds over the AI speed and a Step beat tweens the held view, Timed; everything in the frame, Instant). New beats (`PresentationBeat.cs`): Pace, Step, Keyframe, Spawned, Removed, GroundItem, with `TokenPicture` (what a held view draws, captured at a boundary). `CombatPacing.Wait` records a Pace beat while holding and yields `CombatPacing.HoldingFrame` (one cached `WaitForSeconds(0f)`, one frame); `StepSeconds` is `HoldingStepSeconds` (0.01, one frame). `TurnControl.AiTurnStarting` opens a group when `GameSettings.groupAiTurns` (new, default off, `CurrentGroupAiTurns`) is on and the combat is not over, closes the sheet, skills and inventory windows (`GameManager.ClosePlayerWindowsForGroup`, C6) and notes the lane; `PlayerControlReturned` closes it (Control), `CombatEnded` (Boundary then CombatEnd) and the new `PromptOpened` (Prompt). `TokenView` Held mode (`Hold`, `ReleaseHold`, `HoldAll`, `ReleaseAllHolds`, `AnyHeld`, `ApplyPicture`, `ShowStep`, `ApplySpawned`, `ApplyRemoved`, `SetHidden`, `IsHeld`, `IsHiddenUntilSpawn`, `LastRecorded`): a view created while queueing is hidden until its Spawned beat, a model released or despawned while queueing leaves its view drawn until its Removed beat; `StatusEffectIndicator`'s hold is used (`SignatureOf` added). `SquareCell`: `RefreshGroundItemTokenVisual` records `RequestedGroundItemVisible` and queues a GroundItem beat (`ApplyGroundItemVisible`, `ApplyRequestedState`; UI-058 fixed). `PersistentAreaEffect.ReleaseIndicatorBeforeDestroy`, called by `AreaEffectManager.ClearAllEffects` before its `Destroy`. `CombatUI.ShowConfirmationDialog`: Boundary, then while holding or behind `TurnControl.PromptOpened()` and `CatchUpNow()`, then the dialog. `GameManager`: `BlocksInput` first in `CanProcessWorldInput` and `OnEndTurnButtonPressed`; `InputService.HandleKeyboardInput` returns while blocking (the I, K and C toggles); `IsPlayerTurn` unchanged; the loot window opens through `WhenDrained` (`GameManager.LootCollection.cs`); `Clear` in `ResetCombatStateForNextEncounter`, `Harness_HaltCombat` and `Harness_ResetWorld`; `PresentationBlocksInput` for the harness. Deviation: `Clear` does not write `CombatPacing.Kind` (only `TurnControl` writes it, `CombatPacingTests`); the reset's kind comes from `TurnControl.CombatEnded` in `ResetCombatStateForNextEncounter`. Cosmetic limits while held: the blur pulse does not play and a grappling token is drawn without its blink. CORE-013 noted (the playback lives on its own root, off GameManager); CORE-002 noted, not fixed. Harness (`ScenarioRunner`, `ScenarioModel`, `ScenarioSteps`): `group=off|on|pair`, `playback=instant|timed`, `.Grouping(GroupMode)`, `.ExpectGroups`, `JobResult.Grouping`, `Groups`, `GroupCheck`, `PresentationCheck` (`PresentationCompare`), the `groups.jsonl` sidecar, `HaltCaughtUp`, the `UiTurnDriver` ready gate and the three answerer gates on `PresentationBlocksInput`, `CatchUpNow` before the log-leak sweep and both `CleanCheck` calls, `CleanCheck`'s presentation checks; `rules/combat-log-pool` and `rules/token-view-mirrors-tokens` pinned to `GroupMode.Off`. Tests: `CombatPresentationTests` (grouping off opens no group, the holding queue order, queue while behind, the drains, a close on an empty queue, Timed and Instant playback, `Clear`), new `PresentationHoldTests`, and the eleven `rules/group-*` pair scenarios in `GroupedTurnScenarios` (the two copies, `group-summon-slain-inside` and `group-lesser-globe-caster-moves`, reuse the step 4 scenarios' rules checks). Review fixes (2026-11-25): `CombatPacingTests` expects the two raw waits of `CombatPacing.cs` and checks the Holding branch; `TokenView.Release` records the last picture before it marks the view released; `MoveAlongPath` reads the step time per step; `TokenView.MaybeAnyHeld`; `BlocksInput` keeps a playback root alive; `StatusEffectIndicator` binds its creature at capture time; the harness's `CleanCheck` reads the presentation raw after a reset and finds an orphaned view, a pair's hash difference is GROUPING-DIFFERS, an inconclusive presentation check an ERROR, summary.json lists the grouping problems, the rep 1 copies are dropped once compared, the presentation state is in the diag. The pairs found four harness faults, fixed in the harness: `Harness_ResetWorld` clears ground items and the party gold; an in-flight Ui step is emitted at the turn end (`ScenarioJob.OnTurnEnded`); no frame numbers in `rules/summon-expiry-ends-grapple-at-once`'s detail; the presentation trace restarts after the setup. `rules/group-charmed-pc-ends-group` gained an uncharmed Ui ally (the charm alone ended the fight as a defeat). Limit: `PresentationCheck`'s keyframes come from the model at rules time in both reps, so they cannot catch a held view that drew the wrong picture; `PresentationHoldTests` covers Held mode, a drawn-picture channel is left for later. Verified 2026-11-25 (docs/TESTING.md 3.2): compile 0 errors, 505 warnings; static edit pass 20261125T161644Z-edit OK; Play pass 20261125T161702Z-play OK (`CombatPacingTests` 181/182, AI-095 known; `CombatPresentationTests` 103/103; `PresentationHoldTests` 33/33); `rules/group-*` seeds 1-3 (run 20261125-105116) 66 Pass, every `groupCheck` pass; `smoke/*` and `rules/*` seeds 1-3 as `group=pair` (runs 20261125-110726 and 20261125-110737) OK, 1857 of 1857 pairs same in hash and presentation; the fine-mode subset as `mode=fine;group=pair` seeds 1-2 same (runs 20261125-105957 to 20261125-110249, `rules/*summon*` and `rules/*grapple*` re-run after the fixes, 20261125-111530 and 20261125-111545); the seed 48 canary (run 20261125-105136) 651e227 in both reps. Rep 1 against the midpoint plus the recorded moves: 1797 equal, 24 moved by the harness fixes and SPL-335 only, no rules outcome moved. Not verified by hand in Play mode.

### 10. The stopping points (`gt-stop-points`)

IDs: CMB-029 and SPL-047 (the reserved S7 API, noted). NEUTRAL.

1. S2: `MarkStop` in `ThreatSystem.ExecuteAoO` (after the Spring Attack suppression) and in `GameManager.RunAttackedReaction` (before `ResolveAcidSpray`), through `!AttackDeciders.For(c).IsSynchronous`.
2. S3: `ShowConfirmationDialog` as an interactive barrier; `TurnControl.PromptOpened`; in `PromptAttackedReaction`, `PromptOpened` and then the pre-show wait; the CatchUp fallback with its warning; `HasPendingInteractive` in `Harness_PendingPrompt`.
3. The S7 API, with no callers.
4. `docs/systems/PC_NPC_PARITY.md`: the presentation-only consumer of the decider seam.

Tests:
- `CombatPresentationTests`: S3 barrier order; `PromptCounterTrip` returns true while holding (G6); the fallback runs `CatchUpNow`;
- new `pair` scenarios: `rules/group-stops-at-fighter-aoo`, `rules/group-counter-trip-prompt-plays-out-first`, `rules/group-overrun-response-prompt`, `rules/group-escape-opposition-prompt`, `rules/group-beetle-acid-spray-prompt` and `rules/group-beetle-acid-spray-auto` (a Ui-controlled giant bombardier beetle ally; if one cannot be set up, file a TST gap). The prompt scenario needs a new runner answerer, `HandleAttackedReactionPrompt`, beside the three that exist, with a `ScenarioDef` answer map, gated on `!PresentationBlocksInput`, `rules/group-web-cast-inside`;
- `GroupCheck` expectations read the stop kinds from the in-memory group record.

TEST BUDGET: the new scenarios; `rules/*aoo*`, `rules/*trip*`, `rules/*overrun*`, `rules/*grapple*`, `rules/*beetle*` seeds 1-3 as `group=pair`.

### 11. Overlapped playback and the options (`gt-overlap-and-options`)

IDs: owner request 2026-10-30; Q1, Q2. NEUTRAL in hashes; a presentation behaviour change.

1. `PlaybackScheduler` (R1-R8, the window cap S5).
2. `TokenView` step animation in parallel lanes.
3. The banner shows the actor of the current result-track beat.
4. The "Group AI turns" HUD toggle and a `DebugCommands` entry.
5. `groupAiTurns` defaults to on in play (Q1); the harness stays off unless the option is given.
6. Docs: `docs/TESTING.md` 3.3-3.5 (the options, sidecars, `CleanCheck`, the Ui and answerer gates, `mode=fine`, Limits); `docs/architecture/combat-and-grid.md` (the presentation layer and pacing); `docs/DEVELOPMENT_RECIPES.md` ("Add a combat wait: use `CombatPacing.Wait`" and "Add a `CombatUI` method: class it").

Tests:
- new `PlaybackSchedulerTests` on fake beats: moves overlap; result order holds; R4 (walk into a fallen ally's square); R5 (an AoO mid-move); R7 (a window at an S2 mark, and a barrier at an interactive beat); S5 (a second turn); at most 3 lanes; a single lane plays at `Pace / speed`; streaming never passes the head;
- `CombatPresentationTests`: the setting is read once per group.

TEST BUDGET: `smoke/*` and the `rules/group-*` scenarios seeds 1-3 as `group=pair` (hashes and `PresentationCheck`); watch-mode `playback=timed` spot runs of `rules/group-three-orcs-before-ui-fighter` and `rules/group-stops-at-fighter-aoo` for the owner to view (not verified by the agent).

### 12. Final full regression (`regression-final`)

Kind regression. Run the same set as step 7, then:
- **Grouping.** `rules/*` and `smoke/*` seeds 1-5, plus soak seeds 1-50, as `group=pair` at `aiSpeed=1`: `RepeatCheck` "same" and `PresentationCheck` "same" everywhere, and hashes equal to step 7's run. A `group=on`, `aiSpeed=4` soak 1-50, hash-equal to the off run. The fine-mode `pair` subset.
- **Timing.** `AiTiming` p95 and max show no jump against batch 14's baseline (wall-clock values; its budget rule files an issue on a jump). Wall time and frames per job, on and off (informational, unhashed).
- **Records.** Record the run in `docs/TESTING.md` 3.2 and 3.5.
- **Issues.** Delete or update the touched issues (CMB-007, the new CRE and CMB issues, CORE-013 narrowed).
- **Docs.** Update CLAUDE.md "Current state" (grouped AI turns done; next batch 15); set this file's status to built. The owner's Play-mode check of the playback is listed as not verified.

## Behaviour changes

| Step | Who changes | What |
|---|---|---|
| 1, 3, 8 | nobody | machinery only; hashes identical |
| 2 | creatures hit by poison | the secondary fires 10 rounds after exposure, at the exposure turn's count, whatever the real time; a pending secondary resolves at combat end (CMB-007); ingested poison too since the owner's Q6 answer (2026-11-24, CMB-307; it was 100 rounds before) |
| 4 | slain, expired or dismissed summons; Lesser Globe casters | the summon leaves initiative, the grid, `NPCs` and area tracking at once, and its line comes before the next creature's turn-start lines; the globe follows its caster's square (real-time mid-tween enter and leave lines go away; since the SPL-335 fix of 2026-11-24 it stays where it was cast); outcomes should not change, but those jobs' hashes move once |
| 5 | a forced grappling PC | its escape square is chosen by the AI profile, never by a prompt |
| 6 | AI-decided turns | they pace at 2x (Q4); player turns do not change |
| 9-11 | presentation, with grouping on (Q1) | AI runs play back overlapped, with moves together and results in rules order; the log has the same lines in the same order; a player-run creature's AoO or automatic spray turn plays sequentially; a defender prompt opens after the playback reaches it; the loot window and defeat panel open after the playback; world input, the I, K and C toggles and End Turn wait for the playback; character, skills and inventory windows close when a group starts |
| 3 | every token | it renders through its mirror view; no visible change is intended |

No rule, die or result changes apart from steps 2, 4 and 5. The PURE and NEUTRAL steps prove it by hash equality (fast and fine modes) and by per-channel presentation equality.

## Risks

- **A frame or wall-time rule still hidden.** Known ones: poison (step 2), summon removal and the globe (step 4), CRE-311 (fixed 2026-11-25: a vanished summon's grapple was released only by the end-of-frame `CharacterController.OnDestroy`, so an AI decision taken in the frame of the despawn still saw the grapple; `GameManager.DespawnSummonWithEffect` now ends the summon's grapple and feint links itself, `CharacterController.ReleaseLinksOnLeavingPlay`, and `rules/summon-expiry-ends-grapple-at-once` pins it; soak seed 48's hash moved once, through its round-12 Grappled removals, `turn_start` snapshot and `turn_end` diff, with the same outcome, run 20261125-085010, docs/TESTING.md 3.2; step 9's TEST BUDGET still pins that seed under `group=pair`, against that run's hash), `Update()` area tracking (the frame structure is kept), `AreaAimSearch`'s frame cache (the frame partition is kept), `PlanarTravelSystem` (out of scope). Gates: the time-reader allowlist scan, fine-mode `aiSpeed` and `group=pair` equality, and `FirstDiffSeq` with `ScenarioTraceDiff`.
- **Yield shape.** `CombatPacing.Wait` returns only `WaitForSeconds`, and `TestInlineAction` pins pass-through. No `WaitUntil` comes from pacing, so `RunAIActionNow` is safe.
- **Hash leaks from the recorder.** The log hook stays first. `UpdateAllStatsUI`'s `RefreshFlankedConditions` and the invisibility refresh still run at rules time; only the panel write and the token picture are deferred. Group data lives only in sidecars and `JobResult`. The token GameObject's lifetime and active state are never touched by presentation.
- **CORE-012.** No new yield, loop or frame is added between turns. Holding keeps one frame per wait and step, so the nesting, `SkipChainRunaway` and the Stalemate halt see the same structure.
- **Deaths, despawns and slot reuse (CRE-046).** Death is a keyframe (`DeadSprite`) at its beat. A despawn's rules removal is immediate, while its view lingers to the Removed beat. `Clear` runs at the three reset sites, and `CleanCheck` checks views and the queue. `TokenViewTests` covers a reused slot.
- **The mirror changes every token's rendering, even with grouping off.** It runs last (`DefaultExecutionOrder`) and copies every field. It has its own step, with Play-pass tests and an owner visual check. Rollback is one line.
- **Unclassed or new `CombatUI` methods.** The reflection test fails on them, and the CatchUp fallback means a prompt can never open ahead of its state.
- **Prompts that show UI outside `CombatUI`.** The grapple square selection through `CanSelectSquareNow` is reachable only on the creature's own PC turn once G4 is fixed. Step 8's scan lists the others.
- **Reset mid-playback (CORE-013).** Playback runs on its own GameObject, `Clear` is explicit, and `CleanCheck` asserts the result.
- **All-AI fights.** Run-ahead is unbounded in rounds but bounded per frame, and the window cap keeps overlap readable. Nothing waits on the player, the defeat panel opens after the playback, and the harness halts and clears. If memory or playback length proves a problem, a pause at the top of `SingleNPCTurnFromInitiative` would add a yield and need its own neutrality proof; it is not planned.
- **Harness timing.** Instant playback keeps the Ui and answerer waits within a frame in fast mode, far below the 600-frame Ui settle and the 300-frame SoftLock. Fine-mode jobs get a x5 wall cap.
- **Readability of overlapped moves.** Results serialize, the camera stays still, windows end at stops and second turns, at most 3 lanes play, and the speed and grouping controls let the player back off.
- **Batch 14 drift.** Step 1 re-counts and re-greps the waits and re-reads `TestMoveSiteClassification`, and the source scan pins the rule from then on.
- **Concurrent issue edits.** Another workflow edits `docs/issues/*`. Every step files issues at the next free ID at the time.

## Not in scope

- **CORE-002:** a won or lost fight skips `OnCombatEnded`'s cleanup; nothing here relies on it.
- **Readied actions and readied counterspells** (CMB-029, SPL-047): only the reserved S7 API. A readied counterspell's prompt inside `RunCastSync` needs its own design.
- **A player declining an AoO:** RAW allows it, but today every AoO is automatic for everyone. It would become an S3 prompt with a pre-flight stop.
- **Floating combat text, sound and new death animations** (Q5, C4).
- **Camera follow** (Q2's alternative).
- ~~**Changing the ingested poison delay** (Q6).~~ Done after the owner's answer of 2026-11-24 (CMB-307).
- **Removing the one-frame floor** of recording (needs an explicit tracker flush).
- **`PlanarTravelSystem`'s `Time.time` gates** (a 9th-level spell).
- **A pause at the top of `SingleNPCTurnFromInitiative`** to bound run-ahead.
- **Saved settings:** the options are session-only; the game has no saves.
- **Batch 15 (NPC spellcasting)** follows this work.

## Size

12 steps: 10 build steps and two full regressions. Steps 8-11 are the largest (the facade, run-ahead and the scheduler). The fine-mode and `group=pair` runs double some budgets, so it is larger than the proposal's estimate: roughly 12 to 16% of a week's usage by batch 13's rate, an estimate. Stage 1 alone (steps 1-7) is roughly half of that.
