> Status (2026-10-30): proposal, nothing built. Asked for by the owner on 2026-10-30. It runs after batch 14 (weighted personalities) and before batch 15 (NPC spellcasting), without renumbering the later batches. Its design pass and queue follow when batch 14 closes. The code facts below were read at f8eaac5 and are not verified in Play mode.

# Grouped AI turns

## 1. Request

The owner, 2026-10-30: "design AI turns to take into account how many AI driven actors are acting before player driven actors so that they can act 'simultaneously' as long as they are not prompting any action from the player driven actors such as attacks of opportunities or readied actions".

The goal is pacing. A run of enemy turns should play out together, instead of one after another with pauses, so the player waits less between their own turns.

## 2. The rules stay sequential

Combatants act in initiative order, from the highest result to the lowest (PHB p.135-136), and each turn starts from what the earlier turns left. Truly simultaneous resolution would change outcomes:
- Two orcs would both attack a goblin that the first one already dropped.
- Two creatures would move into the same square.
- One creature's move would not see the square an ally just vacated.

So **every turn is still resolved in order, exactly as today. Only what the player sees overlaps.** The rules, the dice order and the results do not change.

## 3. What exists today

- **Decisions are cheap.** In the batch 13 final soak (runs `20261030-062257` to `-062539`, fast mode), a whole turn took a median of 45 ms and a mean of 55 ms of real time: 61.6 s over 1,121 turns. That figure includes movement, attacks and logging, and fast mode shortens the pauses (`ScenarioFastMode`: timeScale 30). Resolving five enemy turns back to back takes a fraction of a second.
- **The waiting is pacing.** Game code (outside `Assets/Scripts/Tests`) has 98 `WaitForSeconds` calls:
  - 48 in `Services/AIService.cs` and 12 in `_Core/GameManager.NPCTurns.cs` (0.35 to 1.0 s each);
  - 7 in `GameManager.CombatActions.cs`, where `AfterAttackDelay` waits 0.8 to 1.2 s after an attack;
  - 7 in `GrappleSystem.cs` and 4 in each of the forced-behaviour controllers (Charmed, Confused, Frightened, Suggested).

  Movement animates square by square in `CharacterController.MoveAlongPath`.
- **The turn loop.** `GameManager.SingleNPCTurnFromInitiative` (`_Core/GameManager.NPCTurns.cs`) runs `AIService.ExecuteNPCTurn`. Turns advance re-entrantly through coroutines (CORE-012).
- **Attacks of opportunity are automatic.** `ResolveMovementAoOsBeforeStep` (`GameManager.CombatActions.cs`) calls `ThreatSystem.ExecuteAoO` for each threatener, with no prompt, then waits after each AoO. An AoO can still lead to a prompt for the player: a Cleave target, an Improved Grab choice, or an attacked reaction (`PromptAttackedReaction`).
- **Player prompts go through deciders.** These are `IAttackDecider` (`Combat/Core/IAttackDecider.cs`), the cast deciders of the cast core, and the action catalog's AoO confirmation (`TryShowActionAoOConfirmation`). Only the deciders read `IsControllable`.
- **Readied actions and readied counterspells do not exist yet** (CMB-029, SPL-047).

## 4. Design

### 4.1 The group

When the next creature in initiative is AI-run, the turn runner collects it and every AI-run creature after it, up to the next player-run one. That is the **group**.
- **AI-run:** NPCs, plus party members on a forced turn (charmed, confused or panicked; the attack decider's AI source) and AI-run party members.
- **Player-run:** any creature whose turn the player controls, including the player's summons.

A group of one plays as today.

### 4.2 Resolve now, show later

Each turn in the group resolves in order, at full speed. While a group runs:
- the pacing service (4.4) records instead of waiting;
- moves, attack results, damage numbers, condition changes and combat-log lines go into a **playback queue** instead of to the screen.

The rules state runs ahead of what is shown. World input is blocked for the whole group, like any other `WaitingFor*` state, so the player cannot act on the hidden state.

### 4.3 Playback

The queue plays as one overlapped sequence:
- All the group's moves animate at the same time, each token along its own recorded path.
- Attack, damage and condition pop-ups follow in short staggered beats (about 0.15 s apart), in initiative order.
- The combat log shows every line in initiative order, the same text as today.
- A creature that drops plays its fall when its beat comes.

The order inside the playback never contradicts the rules order. For example, a creature that moves into a square freed by a fallen ally starts its move after that fall.

### 4.4 The pacing service

A single `CombatPacing` service takes over every pacing wait in game code, starting with the 98 `WaitForSeconds` calls:
- **Live** (outside a group): it waits as today.
- **Recording** (inside a group): it returns at once and records a beat.
- **Speed:** a factor for AI-only stretches. Stage 1 can ship with the speed factor alone, before any overlap.

The scenario harness already proves that the rules do not depend on these waits: fast mode gives identical results 30x faster.

### 4.5 Stopping points

A group stops before anything that involves a player-run creature. The queue plays out up to that point, and the turn that needs the player then plays live, at normal speed, as today. Stopping points:

1. **A player-run creature takes an attack of opportunity,** even though it takes it automatically today. It can lead to a prompt (a Cleave target, an Improved Grab choice, an attacked reaction), and the owner wants AoOs to stop the group.
2. **Any call into a player decider** during a group: the UI side of `IAttackDecider`, the cast decider, the action AoO confirmation, `PromptAttackedReaction`. The decider seam is the single place to detect a prompt, so prompts added later are covered without new code.
3. **Readied actions and readied counterspells** (CMB-029, SPL-047): they stop the group through the same hook once they are built.
4. **The end of combat.** `EvaluateCombatEnd` plays out the queue before the victory or defeat screen.

Two ways to detect a stopping point:
- **At the decider, when the call arrives** (recommended). The turn's resolution suspends at the prompt's coroutine, the queue plays out, and then the prompt shows. This needs no prediction.
- **By predicting before each turn** whether its plan passes a player-run threatener. This is fragile because plans change mid-turn, so it is only an optimisation to end a group early.

### 4.6 Options

- **Group AI turns:** on or off. Default on (Q1).
- **AI turn speed:** a factor used inside groups and, if the owner wants, on every AI turn.

## 5. Proposed steps

These are for the design pass to refine; it is a medium batch.

1. **The pacing service.** A pure move: every game-code `WaitForSeconds` goes through `CombatPacing`. Trace hashes must stay identical.
2. **The speed factor** for AI-only stretches, with the option. This is stage 1, useful on its own.
3. **The playback queue:** record moves, pop-ups, condition changes and log lines; play them back overlapped.
4. **The group runner:** collect the group, run its turns in recording mode, and stop at the stopping points through the decider seam and the AoO hook.
5. **The combat-end flush,** so `EvaluateCombatEnd` waits for the queue.
6. **Options UI.**
7. **Tests:**
   - a scenario with three orcs acting before a PC;
   - a group that stops at a fighter's AoO;
   - a group that stops at a Cleave prompt;
   - a charmed PC inside a group;
   - combat ending mid-group;
   - `rules/*` run with grouping on and off, which must give identical trace hashes, because grouping changes only what is shown.
8. **Final regression.**

## 6. Risks

- **Pacing is mixed into rules code.** Some waits sit inside rules coroutines (grapple, the forced-behaviour controllers). Step 1 must keep the yield structure, or turn order can change through CORE-012's re-entrant advance.
- **Code that reads token positions instead of grid positions** would see stale positions while the state runs ahead. Grep for `transform.position` reads in rules code before step 3.
- **Deaths and removal.** Defeated creatures must not vanish before their fall plays.
- **Reused slots** (CRE-046): the queue must be empty before `ResetCombatStateForNextEncounter`.
- **Readability.** Overlapped moves are harder to follow, so the log and the pop-up beats keep the order visible. Q2 covers the camera.

## 7. Owner questions, with defaults

- **Q1. Default for grouping:** on (default), with the option to turn it off.
- **Q2. Camera during playback:** stay where it is and show the whole group's moves (default), or follow each actor in turn.
- **Q3. Stage 1 only (faster sequential AI turns) or the full overlap?** Default: build both, stage 1 first, so the speed gain lands early even if the overlap needs more tuning.

The owner already settled that a player-run creature's AoO stops a group (the request names attacks of opportunity).

## 8. Size

About 6 to 10 steps plus a regression, roughly 8 to 10% of a week's usage, by batch 13's rate. It is independent of the personality and spellcasting batches.
