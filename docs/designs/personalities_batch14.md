# Batch 14: weighted personalities

> Status (2026-11-23): step 1 built (the decision trace and timer, the harness timing and the soak baseline in `docs/TESTING.md` 3.5; AI-083 and AI-084 filed); step 2 built (the 16 facts, `AttackValue`, `AiDamageMemory` with a third writer, `SpellCaster.Cast`, the per-target spell damage step that skips `DealDamage`, and a turn-start roll-over instead of a clear; AI-085 filed for the damage paths none of the three writers sees; `rules/ai-damage-memory`); step 3 built (`CognitionTier`, `AI/Personality/`: the seven traits, the 16 considerations, the validator, the factory, the profile and `AiBlend` with `ScoreTarget`, `Judge`, `AoOCost`, `AoOPenalty`, `RiskToleranceFactor` and `OrderOutOfReach`, no caller yet; `SelfFacts` and the other `AiBlend` members land with their sites in steps 5 and 6); step 4 built (`NPCDefinition.AIPersonality` copied by `Clone` and the three templates, `CharacterController.Personality` built at spawn and cleared in `ResetForNewCreature`, `AIService.Difficulty` reading `GameSettings.aiDifficulty` live (a harness override wins), Normal, no menu; the harness `difficulty=` option, `ScenarioBuilder.AiDifficulty` and `.Personality`; goblin_adept, orc_sadist, hobgoblin_captain and the presets ai_personality_test, ai_sadist_fallen and ai_captain_focus; `rules/ai-personality-spawn` and its `-easy` twin; nothing reads a personality yet, so every older hash is unchanged); step 5 built (`AiBlend.ScoreTarget`, `IgnoresUnconscious`, `Adaptive`, `UsesCoupDeGrace`, `TracesTargets` and `TraceTarget` at the five score sites, the six ignore-downed consumers and the adaptive retarget; `AIService.UsesCoupDeGrace` merges `ShouldNPCUseCoupDeGrace` and the test in `HasNPCCoupDeGraceOption`; `SelfFacts`; `AiBlendPassThroughTests`; `rules/ai-captain-focuses-softest`, `rules/ai-sadist-coup-de-grace` (+ `-easy`) and `rules/ai-sadist-keeps-downed-target`, each with a `-plain` twin; every older hash in the test budget unchanged; the captain's Humanoid Role opens with a trip, so its scenario checks the first attack or maneuver, and the dretch in the keeps-downed scenario is given the Animal Role, since its spawned Role is Humanoid, CRE-022); step 6 built (`AiBlend.PrefersCharge`, the creature `AoOCost`, `RiskToleranceFactor` and `OrderOutOfReach`, `PreferredRange`, `KeepAwayDistance` and `IsRangedOrCastingRole`, `PersonalityProfile.RangeAdjust`, at `SelectBestAction`, `EvaluateMovementOptions`, `CalculateRangedRiskTolerance`, the kiter's keep-away test, `AiOutOfReach.Run` and `ApproachRangeSquares`; the kiter's tactical 5-foot step and `AIProfile.ScoreTarget`'s ranged distance term are not routed (AI-086); the kiter's keep-away reads the creature's Role, as the other range sites do; the six scenarios, each with a `-plain` twin; no shipped Role's out-of-reach list holds both an approach and total defense, so the two order scenarios use a test Role; the AoO scenarios fatigue the orc so it cannot charge and let the fighters act first, since a flat-footed guard makes no AoO; every older hash in the test budget unchanged); step 7 run (the midpoint full regression of 2026-11-23 on 661b0b8, `docs/TESTING.md` 3.2: static edit and Play passes, `rules/*`, `smoke/*` and the soak all OK, 0 Fail; every pre-existing hash equal to its baseline; the legal-set counts equal to the batch 13 final's (AI-080) and the soak decision time within the step 1 baseline; nothing filed); step 8 built (a pure refactor: `CharacterController.GetDisarmCheckModifiers` and `GetSunderCheckModifiers`, the terms `RollDisarmCheck` and `ResolveSunder` add, and the public getters the trip and grapple rolls now use: `GetManeuverTouchAttackModifierAgainst`, `GetManeuverTouchArmorClassAgainst`, `GetGrappleCheckModifierToStart` and `GetGrappleCheckModifierToResistStart`, the last two built on the grapple check's own terms because `GetGrappleModifier` lacks greased armor and the Improved Grab hold terms; `AI/Valuation/ManeuverOdds.cs` and `TargetNumbers.cs`, the first `[KnowledgeLegacy]` member (`AI/Knowledge/KnowledgeLegacyAttribute.cs`); `ThreatSystem.EstimateExpectedAoODamage` internal with `EstimateAoOHitChance` and `CanManeuverAoOHitDisruptAttempt` split out; `GameManager.ManeuverStepIntentForAI` and `GetManeuverStepBab`, which `ManeuverOdds.StepBab` reads so the odds keep the step's two-weapon penalty, where this plan's scorer section names `GetManeuverSubstituteBAB` alone; the odds treat an AoO hit as dealing damage, so DR that absorbs it is not modelled; `ManeuverOddsTests`, `rules/maneuver-odds-disarm` and `rules/maneuver-odds-sunder`; every older hash in the test budget unchanged, run 20261123-163900; review rework: the odds price the natural attack the step would give up, not the last substitute recorded (`GameManager.GetManeuverNaturalAttackIndexForAI`, the grapple check's secondary penalty and the sunder's natural attack, CMB-102), take the hand a disarm or sunder pays (`attackerWeaponOverride`), and read the provokers' AoO estimates through `TargetNumbers`; run 20261123-165652 hash-equal); step 9 built (`DND35.AI.AiManeuverChooser`, `AI/AiManeuverChooser.cs`: `Decide` before each melee step through `CreateMeleeStepManeuverEvaluator` and for the kiter's one-shot through `AIService.TryExecuteChosenManeuver`, with NPT 1182 reached through the evaluator it is handed; `Propose`, the Role's one proposal or `ChooseNPCFallbackManeuver` without a profile; `Gather` and `Score` as the scorer section says, sunder and bull rush keeping the attack; a helpless or downed target gets the attack, AI-058; `TryExecutePreferredManeuver`, `IsManeuverForbiddenByTurnStopgap`, `AIManeuverTurnMemory`, `CharacterController.AIManeuverMemory`, `PeekNPCFallbackManeuver` and the coup-taking executor overloads deleted, AI-060 and AI-025; `SelectBestAction` no longer asks about a maneuver; one trace row per decided step, site "Maneuver"; `ManeuverChooserTests`, the rewritten `GrappleDamageRulesTests` scorer tests, `rules/ai-maneuver-trip-when-odds-good`, `-trip-fail-redecided`, `-attacks-when-odds-poor` and `-skips-helpless`; 93 jobs of 31 scenarios moved, each where a trip became a swing, run 20261123-172241, `docs/TESTING.md` 3.4); step 10 built (`ActorActionSet.AllowsNow` and `CheckNow`, `AI/AiLegal.cs`, the gates of the AI-080 decision, `ActionContext.Probe`, `BeginProbe` and the shadow's AI-run and probe counts; AI-run picks 0 in its run, the soak left for step 15; see step 10's "As built"); step 11 built (`AIProfile.ChooseAfterStandardAction` and its default, `AiBlend.AfterStandardAction`, `AIService.UseMoveAfterStandardAction` at the fourteen sites; AI-077 narrowed to the breath sites, AI-088 filed and fixed in the review; 21 jobs of 7 scenarios moved, each where the creature now moves after its ability, run 20261123-185227, and the review rerun 20261123-191352 387 Pass; see step 11's "As built"); step 12 built (`HealerAIProfile.HasCastableCure` gates the Healing turn; `AIProfile.ChooseHealerApproach` and its default, `AiBlend.HealerApproach`, `AIService.RunHealerApproach` at the failed cure; `FindTouchApproachSquareForAI` gains `maxProvokedAoOs` and a price per provoker, defaults unchanged; the Q8 default applied; AI-047 narrowed to the touch buff and lowered to Low, AI-063 narrowed to step 13's part; six scenarios; every older hash in the test budget unchanged, run 20261123-193218; see step 12's "As built"); step 13 built (`AIProfile.ChooseMeleeTurnOption` and `ChooseProneStart` and their defaults, `AiBlend.MeleeTurn`, `AI/Valuation/TurnOptionValue.cs`, the melee option at the aggressive and defensive routines, the prone start in place of the unconditional stand, total defense out of reach only when a foe can reach; Q9 filed and its default applied: total defense's value at the creature's exchange rate; AI-063 narrowed; ten scenarios; 315 jobs Pass, 6 moved and explained, run batch 20261123-203212; the review rework (charge reach, the turn's plan for posture, the aid-another "engaging" reading) 324 Pass, 3 moved, batch 20261123-205958; see step 13's "As built"); steps 14-15 not built. Plan of 2026-11-15: Queue `_work/queue_b14_workflow.js` generated, not launched; the owner launches it. Read-only design pass: three code mappers on master f8eaac5 (the code is unchanged at 1fa6706), two competing designs, and a judge pass that checked the key claims in the code and in the books. Nothing here was compiled, run or verified in Play mode. Line numbers are anchors from f8eaac5: they drift, so anchor on the member names. Paths are under `Assets/Scripts/` unless they start with `docs/`. AIS = `Services/AIService.cs`, NPT = `_Core/GameManager.NPCTurns.cs`, PROF = `AI/AIProfile.cs`, OOR = `AI/AiOutOfReach.cs`, ATK = `_Core/GameManager.AttackCore.cs`.

## Summary

Batch 14 is Increment 1 of `docs/designs/enemy_ai_knowledge_and_personalities.md` (section 7) plus the AI choices batch 13 handed on ("Not in scope: Batch 14" of `docs/designs/action_catalog_batch13.md`).

1. **Personalities.** A creature keeps its Role: today's profile and routine. Weighted traits (`Cowardly:0.6,Healer:0.4`) sit in `CharacterController.Personality`. A personality is a scorer, never a routine: it adds a bounded term to the Role's target score, votes on yes/no decisions, prices attacks of opportunity (AoOs), and tilts the Role's choices for the actions it has left.
   - It is built from `NPCDefinition.AIPersonality` by `PersonalityFactory`, checked by `PersonalityValidator`, and reads the seven starter rows of `TraitDatabase`.
   - It reads targets only through the knowledge facade (`ITargetView`). The facade gains the facts the considerations need.
2. **The blend at every decision site:** target choice, the ignore-downed tests, coup de grace, charge, provoking an AoO, range and risk, the out-of-reach list, and maneuver versus attack. With no personality, every site returns today's answer.
3. **Role-level fixes for everyone:**
   - maneuvers scored by odds and value (AI-035, AI-060, AI-058);
   - no AI pick outside the legal set (AI-080);
   - the move left after a cast (AI-077);
   - the healer reaches its patient (AI-047, AI-063);
   - aid another, total defense and staying prone as real choices;
   - the domain powers.
4. **Measurement.** `AiDecisionTrace` v1 and a decision-time budget (owner request 2026-10-30), both outside the hashed trace.

There are 15 steps: 13 build steps and two full regressions.
- **Steps 1-6 add machinery only.** A creature with no traits plays exactly as before, so every pre-existing trace hash stays identical, and the midpoint regression (step 7) proves it.
- **Steps 8-14 change behaviour.** Step 8 is a pure refactor; steps 9-14 change Role-level behaviour, and each explains its moved hashes.

**Why this shape.** The base is design B ("options scored"), with design A's ("facade at the sites") leftover shape merged in.
- **What B brings:**
  - the personality as a scorer;
  - expected-hit-point valuation helpers (`AI/Valuation/`) that batch 22's evaluator reuses;
  - the single-entry legality check for AI-080;
  - the out-of-reach re-weighting kept neutral;
  - the hobgoblin captain on the aggressive routine (the defensive routine swaps its target, AI-049).
- **What A brings:**
  - leftover choices as virtual `AIProfile` hooks with static `Default*` twins (batch 13's C6 pattern) instead of a new slot framework;
  - the adaptive retarget for traits that ignore downed foes;
  - the disarm failure when the defender's AoO deals damage;
  - timing in a sidecar file.
- **Cut:**
  - B's `AiTurnChooser` slot framework and its "ordinal" regime (batch 22 would delete it);
  - A's implied-role resolution;
  - A's normalisation of a partial weight to 1;
  - A's facade over `SelectLowestHPEnemy` (AI-049 stays filed);
  - A's separate docs-only step (step 1 files the findings).

**Launch prerequisites.**
1. A clean master at or after 1fa6706, with Unity 6000.4.0f1 open on this project, `Library/Bee` present, and the Unity MCP attached here.
2. **Hash baseline.** The launch commit's code equals 5176586, the fixture follow-up. Its baselines are:
   - `rules/*`: run 20261030-072041, seeds 1-5;
   - `smoke/*` and the soak: the batch 13 final regression, batch 20261030-061414 (the follow-up changed only two rules scenarios).

   Step 1 first reruns `smoke/*` seeds 1-3 at the launch commit, to confirm the editor reproduces those hashes; if it does not, it records a fresh baseline run before any edit.
3. **Usage.** Weekly usage below 62% at the start of each step (the queue's gate).

## Rules decisions (explicit RAW; checked in the PHB and DMG on 2026-11-15)

A personality only chooses among actions the cores allow, so no new delegated reading is needed. Each reading is recorded in RULES_COVERAGE 4.1 by the step that applies it.

- **D1. Order of actions.**
  - A turn is a standard action and a move action, in either order, or one full-round action (PHB p.135, 138).
  - A quickened spell is a free action, one per round, and leaves both actions (p.98).
  - Not moving is legal.
  - Whether a move is left is `ActionEconomy.HasMoveAction` plus `MovementService.CanStartMovement` (a 5-foot step, prone, grappled). A 1-round or spontaneous-metamagic cast is a full-round action and leaves no move.
- **D2. Coup de grace** (p.153). A full-round action with a melee weapon against a helpless opponent; it provokes. A dying or unconscious creature is helpless. Legality stays `GameManager.CanUseCoupDeGraceAttackOption`.
- **D3. Aid another** (p.154, batch 13 D6). A standard action: an attack roll against AC 10, then +2 on the friend's next attack against that opponent, or +2 AC against its next attack, until the aider's next turn. The aider must be able to make a melee attack on an opponent that is engaging the friend.
- **D4. Total defense** (p.142, batch 13 D2). A standard action: +4 dodge AC until the next turn, and no AoOs.
- **D5. Posture.**
  - Standing up is a move action that provokes (Table 8-2, p.141). Crawling 5 feet is a move action that provokes from every attacker threatening any point of it (p.142).
  - Prone (p.311): -4 on melee attacks, no ranged weapon except a crossbow, +4 AC against ranged attacks and -4 against melee.
  - Nothing makes a prone creature stand: the AI's unconditional stand (AIS:233) is an AI choice, and becomes a scored one.
- **D6. What the maneuver odds mirror.** They copy the cores; they add no rule.
  - **Trip** (p.158): a melee touch attack, which provokes without Improved Trip; then an opposed Strength check against the defender's Strength or Dexterity, with size and stability terms. A failed trip lets the defender try to trip back. Improved Trip gives an immediate melee attack after a success (p.96, built with CMB-079).
  - **Disarm** (p.155): it provokes without Improved Disarm, and the attempt fails if that AoO deals any damage. Then opposed attack rolls; losing lets the defender try to disarm back.
  - **Sunder** (p.158): it provokes without Improved Sunder; then opposed attack rolls.
  - **Grapple** (p.155-156): a melee touch attack, then opposed grapple checks.
- **D7. Domain powers.** Each is once a day.
  - **Strength** (p.188): a free action; an enhancement bonus to Strength equal to the cleric level, for 1 round.
  - **Destruction smite** (p.186): declared before one melee attack; +4 on the attack and the cleric level added to the damage.
  - **Death touch** (p.186): a melee touch attack on a living creature. Roll 1d6 per cleric level; if the total is at least the creature's current hit points, it dies.

  The code comment in `DomainPowerActionExecutor` cites p.187 for the touch; step 14 corrects it to p.186.

Also checked: an Adept is proficient with simple weapons only, no armor or shield (DMG p.107). Most goblin spellcasters are adepts (MM p.133-134).

## Design choices (game direction the owner may overrule)

- **C1. The Role picks the routine; traits never do.**
  - The archetype stays explicit data. No implied roles are resolved in batch 14.
  - The validator warns when a Healer or Caster trait sits on a creature with no castable spell. Such a trait still scores targets, but needs ENC-021 (batch 15) to act.
  - The 13 concrete-type checks (12 lines) stay untouched.
- **C2. Weights.**
  - A sum above 1 is scaled to 1. A shortfall belongs to the Role: `Smart:0.5` is Smart at half strength, so adding Disciplined later (batch 18) does not silently change Smart's share.
  - The quorum uses the given weights.
- **C3. Trait rows hold only what batch 14 reads (AI-026):**
  - target weights and opinions (design 4.4);
  - the minimum tier, RiskAversion (RA) and `PreferredRangeAdjust`;
  - a new `RoleDesire` column per `ActionRoles` value (Attack, Approach, Retreat, Defend, Support, Heal), which tilts the leftover hooks.

  Cruelty, MoraleBreak, Discipline, Persistence and the Withdraw opinions wait for batch 18.
- **C4. Target term.**
  - `score = legacy + K * sum_j e_j * cons_j(view)`, with `K = 20` and `e_j = sum_i w_i * traitWeight_ij` folded once at spawn. The term is bounded to ±20, which can reorder targets inside the Role's usual 10-40 band but stays below the perception adjustments (AIS:2432), so "visible beats concealed" holds.
  - It is added on the profile path and on the no-profile path.
- **C5. Yes/no decisions.** In order:
  1. The rules gates.
  2. The data override (`EnemyUseCoupDeGraceOverride`) wins.
  3. If traits with a given weight of at least 0.5 say Forbid, the answer is no; Insist at that weight makes it yes.
  4. Otherwise `v = sum w * opinion`, where a Forbid or Insist below the quorum counts -2 or +2: `v > 0.15` is yes, `v < -0.15` is no.
  5. Otherwise the Role's legacy answer. Legacy hooks are never turned into votes.

  Rows may carry a condition (`SelfWoundAtLeast(band)`, `NoEnemyThreatensSelf`, `StandingThreatInReach`) and a `MinDifficulty`.
- **C6. AoO price.** `AoOCost` replaces the boolean (`avoidAoOs ? -1000 : -2` per provoker, AIS:2653). Forbid gives 1000; Against `max(base, 6)`; For `min(base, 2)`; Insist 0; Neutral keeps the base.
- **C7. Risk.** `rho = lerp(0.4, 1.6, RA)`, which is 1.0 at RA 0.5. The ranged-AoO tolerance multiplies the Role's own multiplier by `2 - rho`, keeping the caller's clamp (AIS:1555). The expected-damage costs in the valuations multiply by `rho`.
- **C8. Leftovers are Role hooks.** Each leftover choice is a virtual `AIProfile` hook with a static `Default*` twin (the C6 pattern of batch 13). The hook receives a context struct of pure reads, and the traits' `RoleDesire` tilts it through `AiBlend`.
  - The hooks: `ChooseAfterStandardAction`, `ChooseHealerApproach`, `ChooseMeleeTurnOption`, `ChooseProneStart`, `ChooseDomainPower`. The re-weighted `GetOutOfReachAlternatives` joins them.
  - The `Default*` twins are today's behaviour, except where today is a filed fault (AI-077, AI-063, AI-047, AI-080, AI-035).
- **C9. The Role proposes, the scorer disposes.** `GetPreferredManeuver` (or the no-profile peek) proposes at most one maneuver per step, and the scorer decides between it and the attack.
  - Grapple stays behind `ShouldInitiateGrapple`.
  - The natural-sequence limit of one substitute stays (`naturalSubstituteUsed`, ATK:190).
- **C10. No per-turn decision cache.** Its reasons, the Sanctuary and Listen rolls, were fixed (AI-005, AI-007). `SelectBestTarget` has side effects (AIS:2105, 2462), so no step adds a call to it. The timing budget measures the cost instead.
- **C11. Difficulty.** The owner's ruling (2026-10-30) is that a sadistic coup de grace is allowed at Normal and above and blocked on Easy.
  - Nothing reads `AIService._difficulty` today (AI-012). The setting becomes `AIService.Difficulty`, seeded from a new `GameSettings.aiDifficulty` (Easy, Normal or Hard; default Normal), plus a harness option `difficulty=`.
  - Easy skips the opinion rows marked `MinDifficulty = Normal`; only Sadistic's coup de grace Insist carries the mark. Hard equals Normal in batch 14. (Q6.)
- **C12. Parity.** AI-run party members carry no traits unless a harness spec gives them some. They use every Role hook, as NPCs do. No personality code reads `IsPC`, `IsControllable` or `ActivePC`.
- **C13. Templates.** The zombie, skeleton and lycanthrope templates copy `AIPersonality` (CRE-023) but attach no trait. The undead Roles already play mindless, and a Mindless lock on them would move hashes. On a mindless creature, every trait above its tier is dropped.
- **C14. Domain scope.** Only Strength, Destruction and Death get an AI choice. Sun's greater turning is already in the turn undead choice (`TurnUndeadSystem.cs:634`). Luck is a reroll after a roll, not a turn choice. The plant and elemental powers are turning variants with no live creature. No shipped NPC has an activated domain power, so the tests use AI-run clerics given domains by `Tweak`.

## Architecture

### Files (new unless marked; Unity generates the metas)

- `AI/Knowledge/CognitionTier.cs`: Mindless (no Int score), Animal (Int 1-2), Low (3-7), Average (8-12), Keen (13-16), Brilliant (17+), from `Stats.IsMindless` and the base Intelligence (`EffectiveINTScore`, CharacterStats.cs:3631). This is design 3.3; batch 17 makes the tier filter facts.
- `AI/Valuation/` (batch 22 reuses all of it):
  - `AttackValue`: hit chance and expected damage, extracted from the private `AIProfile.HitChance` and `AverageWeaponDamage` (PROF:884, 892) and `ThreatSystem.EstimateAverageMeleeDamage`. The old members delegate to it with the same formula.
  - `ManeuverOdds`.
  - `TargetNumbers`: the one `[KnowledgeLegacy]` choke point for a target's check modifiers until batch 17.
- `AI/AiDamageMemory.cs`: who hurt this creature since its last turn.
- `AI/Personality/`: `TraitDefinition`, `TraitDatabase`, `Considerations`, `PersonalityProfile`, `PersonalityFactory`, `PersonalityValidator`, `AiBlend`, `SelfFacts`.
- `AI/AiLegal.cs`: `CanAttackNow`, `CanMoveNow` and `CanUse`, the AI's reads of the legal set.
- `AI/AiManeuverChooser.cs`.
- `AI/Debug/AiDecisionTrace.cs`, `AiDecisionTimer.cs`.
- `CharacterController.Personality` (beside `aiProfile`, CharacterController.cs:325) and `CharacterController.DamageMemory`, both cleared in `ResetForNewCreature` (:5094, CRE-046). `NPCDefinition.AIPersonality` (NPCDatabase.cs:896).

### New facts (added to `OmniscientKnowledgeProvider.TruthView`, at `Confirmed`)

`FactKey` gains 16 keys:

| Key | Source or meaning |
|---|---|
| `Down` | `CharacterController.IsUnconscious` (:2308): dying, stable or knocked out; not `WoundBand.Down`, which is `currentHp <= 0` |
| `Helpless` | `IsHelplessForCoupDeGrace` (:7748) |
| `Fleeing` | `IsWithdrawing` (:423), Frightened or Panicked |
| `Healer` | a cleric, Lay on Hands, or a prepared Healing-effect spell |
| `Distance` | |
| `ThreatensObserver` | `ThreatSystem.GetThreateningEnemies` |
| `ThreatensAllyInNeed` | it threatens an ally of the observer below 70% HP |
| `EngagedByAlly` | |
| `FoesNear` | other foes within 2 squares |
| `FlankingNow` | |
| `DamagedObserver` | from `AiDamageMemory` |
| `AcEstimate`, `TouchAcEstimate` | |
| `HpCurrent`, `HpMax` | |
| `ThreatPerRound` | 0 when the subject cannot act |

Batch 17 degrades them in its own provider; the considerations never notice. `OmniscienceLintTests` fails when `AI/Personality/**` or `AI/Valuation/**` dereferences a target or `Subject` into `.Stats`, `.GridPosition`, `.HasCondition` or `.Is*`. It skips `SelfFacts` (the creature's own state) and `TargetNumbers`.

### Considerations (each returns a value in [0, 1] from an `ITargetView`)

| Consideration | Value |
|---|---|
| WoundSeverity | standing targets only: Unhurt 0, Hurt .33, BadlyHurt .66 |
| IsDown | |
| Helpless | |
| Softness | Unarmored 1, Light .67, Medium .33, Heavy 0 |
| KillChance | `clamp01(my expected damage / HpCurrent) * threat`: threat 1 for a known caster or healer, .6 otherwise, 0 when Down |
| KnownCaster, KnownHealer | |
| Proximity | `(12 - d) / 11` |
| ThreatensMe | |
| EngagedByAlly | |
| FlankOpportunity | `FlankingNow` |
| Clustered | `FoesNear` |
| LooksDangerous | |
| Fleeing | |
| ThreatToWard | `ThreatensAllyInNeed` |
| HurtMe | `DamagedObserver` |

### The seven starter traits

The target weights and opinions are design 4.4's rows, verbatim. Two columns are new: the tier and RA come from the design, and `RoleDesire` is new in batch 14.

| Trait | Min tier | RA | Notes | RoleDesire (starter) |
|---|---|---|---|---|
| Sadistic | Low | .3 | Evil hint, warns only; CoupDeGrace Insist with `MinDifficulty` Normal; IgnoreUnconscious Forbid | Attack +.4, Retreat -.4 |
| Cowardly | Animal | 1.0 | Charge Forbid; ProvokeAoO Forbid if SelfWound >= Hurt, else Against; range +2 for Ranged, Mixed or casting Roles | Retreat +.8, Defend +.5, Approach -.5, Attack -.2 |
| Smart | Average | .6 | IgnoreUnconscious For if a standing threat is in reach; CoupDeGrace For if no enemy threatens it, else Against; ProvokeAoO Against | none |
| Caster | Low | .7 | Charge Forbid; ProvokeAoO Against | Retreat +.4, Approach -.2, Support +.2 |
| Healer | Low | .3 | CoupDeGrace Against; Charge Against | Heal +.8, Support +.6, Defend +.1 |
| Berserker | Animal | -.3 | Charge Insist; ProvokeAoO For | Attack +.6, Approach +.6, Retreat -.8, Defend -.6 |
| Mindless | Mindless | 0 | Proximity 1.0; `ExclusiveLock` removes every other trait | none |

Design 4.4's `IgnoreAoO` and `ProvokeAoO` opinions are one decision (AIS:2601-2604).

`PersonalityValidator`:
- drops an unknown id, or a trait below its tier, with one warning per definition;
- applies the Mindless lock;
- warns on an alignment mismatch, and nothing more (owner, 2026-10-30);
- scales a sum above 1.

A creature with no surviving trait gets `Personality == null`.

### The blend (`AiBlend`, static; each member returns its `legacy` argument when `Personality == null`)

| Decision | Sites | Member |
|---|---|---|
| Target score | `SelectBestTargetFromProfile` (AIS:2281), `SelectBestTargetDefault` (:2330), `SelectAdaptiveFullAttackTarget` (:4065), `ChooseCleaveTargetForAI` (`GameManager.AttackResolved.cs:486`), NPT:851 | `ScoreTarget` |
| Ignore downed | AIS:2302, 4033; NPT:635, 663, 826; `AttackResolved.cs:477` | `IgnoresUnconscious` |
| Mid-sequence retarget | NPT:616 | `Adaptive`: on when the Role says so, or when the traits' IgnoreUnconscious verdict is yes, so a Smart Humanoid's full attack does not stop on a downed target (NPT:671-674) |
| Coup de grace | AIS:2910 and NPT:292, merged into one | `UsesCoupDeGrace` |
| Charge | `SelectBestAction` (AIS:3568) | `PrefersCharge` |
| Provoke an AoO | `EvaluateMovementOptions` (AIS:2601, 2653) | `AoOCost` |
| Range and risk | AIS:2597, `AiOutOfReach.ApproachRangeSquares`, `CalculateRangedRiskTolerance` (AIS:1538), the kiter's keep-away distance (AIS:1390) | `PreferredRange`, `RiskToleranceFactor`, `KeepAwayDistance` |
| Out-of-reach order | OOR:70-72 | `OrderOutOfReach`: the Role's list, re-weighted by RoleDesire |
| Maneuver or attack | `CreateMeleeStepManeuverEvaluator` (AIS:2832), NPT:1182, the kiter's AIS:1457 | `AiManeuverChooser.Decide` (step 9) |

Each member writes one trace row when its creature has a personality. A per-candidate site (the five target-score sites) writes one summary row per choice instead of one per candidate: `Personality.Target`, `AdaptiveTarget`, `FiveFootStep` or `Cleave`.

### Maneuver scorer (expected hit points per attack step)

`Decide(npc, target)` runs once per step; today `GetPreferredManeuver` runs twice per step. Order:
1. A coup de grace when `CanUseCoupDeGraceAttackOption` and `UsesCoupDeGrace` say yes.
2. Otherwise the Role's proposal, through today's eligibility gates (a ranged-only loadout, `CanTrip`, a disarmable weapon, `ShouldInitiateGrapple`).
3. Then the comparison below.

A helpless target gets no maneuver (AI-058).

- **Odds**, from `ManeuverOdds.Estimate(npc, target, type, stepBab)`, where `stepBab = GetManeuverSubstituteBAB(MainHandStepsUsed)` (CharacterController.cs:1654).
  - **Trip:** `P_touch` times `EstimateOpposedCheckWinChance(GetTripAttackerCheckModifier, target.GetTripOrOverrunDefenderCheckModifier)` (:13489, 13499, 13775).
  - **Grapple:** the same touch, then `GetGrappleModifier` (:15518) on both sides.
  - **Disarm:** `(1 - P(the AoO deals damage))` times the opposed-attack odds.
  - **Sunder:** the opposed-attack odds.
  - The disarm and sunder modifiers move out of `RollDisarmCheck` (:15606) and `ResolveSunder` (:14716) into shared methods, which the roll and the estimate both call.
  - The provokers come from `ThreatSystem.GetManeuverAoOProvokers` (:970).
- **Value** (starter constants, pinned by `ManeuverChooserTests`). The terms:
  - `S`: the replaced attack's `AttackValue`;
  - `Th`: the target's `ThreatPerRound`;
  - `r`: my steps left;
  - `a`: allies already threatening the target;
  - `D`: average hit damage;
  - `dH`: the gain in hit chance from +4.

  The value of each maneuver:
  - **Trip:** `dH * D * (r + a) + 0.4 * Th`, plus `S + dH * D` with Improved Trip (built in step 9: the follow-up attack is made against the prone defender, PHB p.96, p.311; the plan first had `S`).
  - **Disarm:** `0.75 * Th * (the weapon's share of it)`.
  - **Grapple:** `0.5 * Th + D`.
  - **Sunder and bull rush:** dormant (no shipped profile enables them).
  - A target that cannot act has `Th = 0`.
- **Costs:** `rho * sum EstimateExpectedAoODamage(provokers)` (made internal, ThreatSystem.cs:268). For a trip, add `(1 - P_win) * P_counter * 0.4 * Th`; for a disarm, the counter-disarm odds times my weapon's share.
- **Choice:** the maneuver wins iff `P_success * V - costs >= S * (1 + 0.5 * desire(Attack)) + max(0.5, 0.1 * S)`.
  - An even match attacks. Improved Trip, a large Strength gap, or allies in reach make a trip win.
  - For example, a goblin (Str 11, Small) tripping a Str 15 fighter wins about 0.16 of the time, so it swings.

### Leftover hooks (C8; defaults apply to every creature)

- **`ChooseAfterStandardAction`** returns Stay, Approach, KeepDistance or StandUp.
  - A melee or Mixed Role approaches a focus out of reach, as wild shape already does (AIS:747).
  - A Ranged or casting Role keeps its distance when a foe is within the keep-away distance or threatens it.
  - Otherwise it stays.
  - A free-action or quickened cast keeps the routine going (D1).
- **`ChooseHealerApproach`** applies when the cure found no target and a castable Healing-effect spell exists.
  - If an ally is within one move plus one square, the healer moves to `FindTouchApproachSquareForAI` (`GameManager.CastDeciders.cs:262`, which gains a `maxProvokedAoOs` taken from `AoOCost`) and casts in the same turn (D1).
  - Otherwise it makes a partial approach with `EvaluateMovementOptions(..., preferredRangeOverride: 1)`.
  - Otherwise it falls back to today's kiter. (Q8.)
- **`ChooseMeleeTurnOption`** returns Attack, AidOffense, AidDefense or TotalDefense.
  - It keeps the attack unless an alternative beats it by `max(0.5, 0.15 * S)`.
  - Aid offense: `HitChance(myBab, 10) * (HitChance(allyBab + 2) - HitChance(allyBab)) * D_ally`.
  - Aid defense: the same aid roll times the foe's lost hits on the ally at +2 AC.
  - Total defense: the reduction of the foes' next-round damage at +4 AC, and 0 when no foe can reach it.
- **`ChooseProneStart`** returns StandUp, StayProne or Crawl. It hooks before AIS:233; the grappling exception stays.
  - The creature stays prone when no foe threatens it, its planned action works prone (a cast, or a crossbow shot), and a foe in view attacks at range.
  - It crawls only to a square that no foe can threaten next turn.
  - Otherwise it stands, as today.
- **`ChooseDomainPower`** (C14).
  - Strength: when the creature will make melee attacks this turn.
  - Destruction: on the first melee swing, when `HitChance(bab + 4) * (D + level) - HitChance(bab) * D` beats 0.
  - Death touch: replaces an attack step when `P(touch) * P(sum of level d6 >= HpCurrent) * HpCurrent` beats `S`.

### Trace and decision timing (step 1; side sinks, never in `ScenarioTrace.HashLines`)

- **`AiDecisionTrace`** follows the `ActionSetShadow` pattern (`ActorActionSet.cs:256`).
  - It is enabled in the editor and development builds only.
  - It keeps one record per AI turn: round, actor, Role, traits, and its decisions (site, chosen, legacy, bonus, reason).
  - It keeps a ring of the last 50 turns, plus counts per site and verdict.
  - With `AIService.TraceEnabled` on, it also logs a collapsed `[AI-TRACE]` Debug.Log line.
- **`AiDecisionTimer`** times only the decision code.
  - `BeginTurn` and `EndTurn` are keyed by actor, so re-entrant turns (CORE-012) do not mix.
  - `Scope` is a struct that counts exclusive time per bucket: Target, Action, Maneuver, Spell, Movement, LegalSet and Personality.
  - Scopes open and close in synchronous decision code only. They never wrap an `Execute*` call, a coroutine wait or movement playback.
- **Harness.**
  - After the `ActionSetShadow` copy (`ScenarioRunner.cs:1435`), the runner fills `JobResult.AiTiming` {turns, p50, p95, max, over50, warmup} in `summary.json`.
  - It writes a per-run sidecar, `ai-timing.tsv`. `soak-rows.tsv` is not changed, because `SoakStats.MergeRuns` reads it by column index.
  - `SoakStats` adds a whole-run summary over the turns that are not warm-up. The first job of each fresh Play session counts as warm-up.
  - A turn whose decisions take more than 50 ms adds a Notes warning, never a Fail.
- **Budget rule** (`docs/TESTING.md` 3.5). Step 1 records the baseline from one soak 1-50. Every later regression compares p95 and max with it and files an issue on a jump.
  - **Plan line for later batches:** batch 18's stance path checks and batch 22's evaluator over the whole action set stay one step deep, inside this budget, with no look-ahead search.

## AI-063, AI-047, AI-080

- **AI-063: in scope (steps 12, 13).** Two causes:
  - `HealerAIProfile.DetermineActionPriority` returns Healing whenever an ally is hurt, castable cure or not (checked: its `hasCastableSpells` argument gates only the buff and offensive branches). The branch then falls to the kiter, and, with nothing in reach, to total defense where it stands.
  - The cure never reaches an ally out of touch range (AI-047).

  The fixes: Healing needs a castable cure; `ChooseHealerApproach` moves the healer; total defense is chosen only when a foe can reach the creature (`ChooseMeleeTurnOption`). The out-of-reach list keeps its entry otherwise (C8; the C1 lists stay neutral).
- **AI-047: in scope, narrowly (step 12).** The fix is move-then-touch-cast in one turn, or a partial approach, for a Healer Role with a castable Healing-effect spell.
  - The Support, Skirmish and Withdraw trade-off for any NPC with a heal or buff stays in batch 18.
  - The issue's other clauses are already fixed: SPL-005, and the offensive fallback onto an ally (AI-016).
  - `goblin_adept` has no spells before batch 15, so it never approaches a patient.
- **AI-080: in scope for the AI-run kinds (step 10).**
  - Kind 1: `SelectGuardPassingTarget` returns null when `AiLegal.CanAttackNow` is false. A nauseated creature then reaches its out-of-reach alternatives and uses its move.
  - Kinds 11 and 12: the free spittle gate (AIS:344) and `TryExecuteEngulf` (AIS:4240) call `ActorActionSet.AllowsNow`.
  - The `HasMoveAction`-only tests become `AiLegal.CanMoveNow`: AIS:749, 851, 1089, 1240, 1721, 1805, 3129, 3215.
  - Kinds 3, 4, 5, 6 and 7 are scripted probe steps that try a refused action on purpose. A new `ActionContext.Probe`, set by the harness steps, separates them in the shadow count.
  - The issue is deleted when the final regression's AI-run count is 0.

## Owner questions (the plan applies the default unless the owner answers before the named step)

Numbered after batch 13's Q4 and Q5.

- **Q6. Difficulty (step 4).**
  - **Default:** `GameSettings.aiDifficulty` (Easy, Normal, Hard; default Normal), read through `AIService.Difficulty`, with a harness option and no menu in batch 14. Easy blocks only the trait-driven coup de grace. Roles that take a coup de grace by default (Berserk, Animal, some undead) and the data override are unchanged.
  - **Alternatives:** Easy blocks every AI coup de grace on a PC; or a difficulty menu now.
  - **Open (step 5 review, 2026-11-23):** as built, Easy skips only Sadistic's Insist; Smart's coup de grace For (no `MinDifficulty`) still drives a trait-based coup de grace on Easy, for example the `hobgoblin_captain` when no enemy threatens it. The owner may say whether Smart's For should also be gated on Easy.
- **Q7. Traits for existing creatures (step 4).**
  - **Default:** in batch 14, only the three test creatures carry traits. A later batch adds an opt-in table of MM-flavoured mixes (for example goblins somewhat Cowardly), chosen with the owner, after the final regression shows trait parity.
  - The three new definitions join the Random Encounter Generator's pool (`RandomEncounterSystem.BuildCandidates` enumerates `NPCDatabase.AllNPCs`). The soak is unaffected, because it draws from the DMG dungeon tables (`DungeonEncounterTableManager`).
  - **Alternative:** attach defaults now.
- **Q9. What is a hit point kept off worth against a hit point dealt (step 13, filed 2026-11-23 by the step)?**
  - **Default (applied in step 13):** total defense's damage kept off counts at the creature's exchange rate, its own attack value per hit point it takes next round, at most 1 (`TurnOptionValue.TotalDefense`). The formula in "Leftover hooks" counts it one for one; built that way, a goblin facing any stronger fighter took total defense every turn (`rules/ai-maneuver-attacks-when-odds-poor` and `rules/ai-captain-focuses-softest-plain` failed), which delays its loss and helps no ally. Aid defense keeps the plan's one-for-one value with the drop weight. What the default does in play: the value is at most the damage kept off times S over the incoming damage, so without a drop weight it is at most S and never beats the attack by the margin; the melee turn's total defense is then chosen only when one average hit of a foe would drop the creature (drop weight 2) and +4 AC removes at least about 57.5% of the incoming damage (foes that hit it only on high rolls), with S large enough to pay the 0.5 floor (`AiLeftoverHookTests.TestMeleeTurnOptionFormulas` pins this; no scenario reaches that branch yet). The out-of-reach total defense is not valued and still runs whenever a foe can reach the creature.
  - **Alternatives:** one for one as the plan reads; or the rate for aid defense too (the ally's attack value per hit point it takes), which would make aid defense of a weak ally almost never win.
- **Q8. May a healer take a provoking path to touch a patient (step 12)?**
  - **Default:** the Role's AoO rule decides. The Healer avoids provoking, except for an ally at or below the critical threshold (25%), where a provoking path costs 6 per provoker instead of 1000; a healer whose traits say Forbid never provokes.
  - **Alternatives:** never; always.

## Steps

Every C# step runs `bash tools/compile_check.sh` and expects exit=0, errors=0, warnings=506; a jump is a regression.

Each step keeps the docs true in the same commit:
- **Issues.** Delete fixed entries in `docs/issues/<PREFIX>.md`; update the counts and Top issues in `docs/KNOWN_ISSUES.md`. File new issues at the next free ID: another workflow may have taken IDs since this plan.
- **System docs.** Update `docs/systems/AI.md` (1, 3.3-3.5, 5, 6, 9-13) and `docs/systems/AI_ACTION_COVERAGE.md` where an entry lands.
- **Rules.** Record each D-reading and design choice in RULES_COVERAGE 4.1, dated 2026-11-15.
- **Recipes.** An "Add a trait" recipe in `docs/DEVELOPMENT_RECIPES.md` (step 3).

House rules for every step:
- New `.cs` files get their Unity-generated `.meta`; never hand-write one.
- Grep `Assets/Scripts/Tests` before renaming or deleting a member (TST-006).
- Write "not verified in Play mode" in commit bodies for anything only read.
- New scenarios go in `Tests/Scenarios/Catalog/ActionCatalogScenarios.cs`, or a new `PersonalityScenarios.cs`. A `-plain` twin drops the personality.

**TEST BUDGET rule for every step** (batch 13's rule):
- Run the new and touched scenarios and the touched subsystem's, seeds 1-3, in a fresh Play session (`QueueFresh`), plus the static suites covering the touched code.
- A *broad* step also runs `smoke/*` seeds 1-3.
- A neutral step compares every pre-existing hash with the baseline or the previous step's run. A behaviour step explains each moved hash with `ScenarioTraceDiff` (`docs/TESTING.md` 3.4).
- A step that changes a Role's choices adds every scenario whose AI actor uses that Role: grep the catalog for `goblin`, `orc_warrior`, `orc_berserker`, `orc_grapple_drill`, `hobgoblin_sergeant` and `zombie_shambler`.
- The full sweep and the soak run only in the two regression steps (and step 1's timing baseline).
- Never run scenarios in a Play session that ran static suites.

### 1. Decision trace and timing (`b14-trace-and-timing`)

IDs: owner request 2026-10-30; AI-012 (noted). Neutral, *broad*.

1. Confirm the hash baseline (launch prerequisite 2) with `smoke/*` seeds 1-3 before any edit.
2. Add `AiDecisionTrace`, `AiDecisionTimer` and `AIService.TraceEnabled` (architecture).
3. **Scopes.** Wrap the decision calls, not their executions:
   - `SelectBestTarget` (AIS:2064), `SelectAdaptiveFullAttackTarget` (:4002), `SelectLowestHPEnemy`, `SelectBestAction` (:3558);
   - the decision parts of `ShouldUseManeuver` (:2731) and `TryExecutePreferredManeuver` (:2845);
   - `ScoreCastableSpells` (:3377), `RankedSpellCandidates` (:3064), `EvaluateMovementOptions` (:2580), and `ActionSetShadowCheck` (`ActionRunner.cs:321`).

   `ExecuteNPCTurn` (AIS:129) calls `BeginTurn` and `EndTurn`.
4. **Harness plumbing:** `JobResult.AiTiming`, the `ai-timing.tsv` sidecar, the `SoakStats` whole-run summary, warm-up jobs, and Notes warnings over 50 ms. `ScenarioTrace.HarnessVersion` stays "6", and nothing new enters the hash.
5. **Baseline.** Run one soak 1-50 and record the timing baseline and the budget rule in `docs/TESTING.md` 3.5, with the plan line for batches 18 and 22.
6. **File the map findings** as new issues at the next free AI ID, unless an entry already covers them:
   - the adaptive 5-foot step skips the ward, sphere and charm filters (NPT:800-874);
   - the scorer does not price Improved Grab or other attack riders (C9).

Tests:
- new `AiDecisionTimerTests`: exclusive nesting, a disabled no-op, percentiles;
- new `AiDecisionTraceTests`: the ring, the drain, and the hash untouched.

TEST BUDGET: those two suites and `AIProfileFrameworkTests`; `rules/ai-*` and `smoke/*` seeds 1-3, hash-equal; one soak 1-50 for the timing baseline, hash-equal to the batch 13 final.

### 2. Facts for the considerations (`b14-knowledge-facts`)

IDs: none. Neutral (writers with no consumer yet).

1. The 16 `FactKey`s and their `TruthView.Get` cases (`AI/Knowledge/KnowledgeService.cs`), with helpers in `KnowledgeFacts`.
2. Extract `AttackValue`; the private statics delegate to it with the same formula.
3. Add `AiDamageMemory`. Its writers are `GameManager.OnAttackResolved` and `DealDamage`, with no log and no dice. It is cleared in the `ResetForNewCreature` AI block and at the creature's turn start.
   As built (2026-11-23): the turn start rolls the memory over instead of clearing it (`AiDamageMemory.RollOver` in `CharacterController.StartNewTurn`): it keeps two windows, since the previous turn start and since the latest, and reads both. A clear there would leave the creature's own decision with only the damage of its current turn, so HurtMe would always be 0; the roll-over also keeps the ongoing damage dealt at the turn start (Melf's Acid Arrow) before `StartNewTurn`. `SpellCaster.Cast` is a third writer, and the encounter reset also clears it.

Tests:
- extend `KnowledgeFactsTests`: each key; dying is Down, prone is not;
- new `AttackValueTests`: equal to the old formulas;
- a `NpcSlotReuseTests` case.

TEST BUDGET: those and `AIProfileFrameworkTests`; `rules/ai-*`, `rules/cleave-*` and `smoke/*`, hash-equal.

### 3. Traits, considerations, validator and blend core (`b14-traits-and-blend`)

IDs: none. Pure: nothing calls it yet.

1. Add `CognitionTier`; `TraitDatabase` with the seven rows (C3); and `Considerations`, the 16 in the architecture section.
2. Add `PersonalityProfile`, which parses the spec, folds the weights, and provides `TargetTerm`, `Judge` and `RoleShift`.
3. Add `PersonalityValidator` and `PersonalityFactory`.
4. Add `AiBlend` with `Judge`, `AoOCost`, `RiskToleranceFactor` and `OrderOutOfReach` (C5-C8).
5. Add `OmniscienceLintTests`, and the "Add a trait" recipe.

Tests (all new):
- `TraitDatabaseTests`: pins every table number; target weights sum to 1 in absolute value.
- `PersonalityValidatorTests`:
  - Smart on a mindless creature is dropped, leaving a null personality;
  - `Mindless, Smart:0.5` keeps only Mindless;
  - Sadistic on a lawful good creature only warns;
  - Smart on an Int 7 creature is dropped.
- `PersonalityBlendTests` (design section 8):
  - one trait at 1.0 equals that trait;
  - the folded weights equal the per-trait sum;
  - the quorum boundaries at .5 ± ε;
  - the legacy fallback inside ±.15;
  - the data override wins;
  - Easy skips the sadistic row;
  - partial weights (C2);
  - the adept's RA .72 and the orc's folded weights (design 4.5).
- `ConsiderationTests`: the `StubView` pattern of `KnowledgeFactsTests`.

TEST BUDGET: those suites and `KnowledgeFactsTests`; no scenario.

### 4. Personality data, test creatures and difficulty (`b14-personality-data`)

IDs: AI-012 (narrowed); CRE-023 pattern; Q6, Q7 defaults. Neutral for every existing creature.

1. Add `NPCDefinition.AIPersonality`. `Clone` (:910) copies the string by `MemberwiseClone`; add a line to each of the three `CopyDefinitionFields` (`ZombieCreatureTemplate.cs:37`, `SkeletonCreatureTemplate.cs:41`, `LycanthropeTemplateBase.cs:52`).
2. Add `CharacterController.Personality`, nulled in `ResetForNewCreature`. `InitializeNPCFromDefinition` builds it after `AIControl` (NPCSetup.cs:1151); its warnings go to `Debug.LogWarning`.
3. Add `AIService.Difficulty` and `GameSettings.aiDifficulty` (C11), and point `AiBlend.SettingsDifficulty` (step 3: the source of a JudgeContext that leaves Difficulty null) at it; harness `RunOptions` `difficulty=` and `Scenario.AiDifficulty(level)`; `ActorSpec.Personality(string)` through `Tweak`.
4. **Data.**
   - `goblin_adept` (`NPCDatabase_G.cs`): the MM goblin with Adept 1, with no spells until batch 15 (CHR-024, ENC-021). Adept proficiency (DMG p.107) means no armor or shield: a morningstar and a sling. Archetype Healer, AIBehavior RangedKiter, personality `Cowardly:0.6,Healer:0.4`. If the Adept class cannot spawn before CHR-024, use Warrior 1 and file the gap.
   - `orc_sadist` (`NPCDatabase_O.cs`): from `orc_warrior`, Humanoid, `Sadistic:0.7,Berserker:0.3`.
   - `hobgoblin_captain` (`NPCDatabase_H.cs`): from `hobgoblin_sergeant`, with Fighter 3, Int 13 (Keen), LE and Humanoid. Its AIBehavior is AggressiveMelee, because the defensive routine swaps its target for the lowest-HP foe (AIS:1639, AI-049). Personality `Smart:0.5`.
   - Presets `ai_personality_test`, `ai_sadist_fallen` and `ai_captain_focus` (`ListEncounterPresets`). No DMG table maps to the new ids.

Tests:
- new `PersonalityFactoryTests`; `PersonalityTemplateCopyTests` (the string survives the zombie, skeleton and lycanthrope templates);
- a `NpcSlotReuseTests` case; `NPCTemplateSystemTests`;
- `AIProfileFrameworkTests` (`TestNPCDefinitionsAssignProfileArchetypes`);
- scenario `rules/ai-personality-spawn`: the three spawn with their personalities, and `zombie_shambler` with `Smart:0.5` spawns with none and logs the warning.

TEST BUDGET: those; `rules/ai-*`, `rules/slot-reuse-*`, `rules/large-encounter-*` and `smoke/*`, hash-equal.

### 5. The blend at the target sites (`b14-blend-target-sites`)

IDs: prepares AI-058. Neutral, *broad*.

1. Route the five score sites, the six ignore-downed consumers, the adaptive retarget, and the two coup de grace copies (merged into one `UsesCoupDeGrace`) through `AiBlend`.
2. Scenarios:
   - `rules/ai-captain-focuses-softest` and its `-plain` twin. The layout is fixed in `PersonalityBlendTests` first, then tuned until the pair differs: the plain captain takes the near, healthy, armored foe; the Smart one takes the soft, wounded one.
   - `rules/ai-sadist-coup-de-grace`, with `-easy` and `-plain` twins: a PC at -3 hit points beside the orc. A coup de grace at Normal; none on Easy; none from the plain orc.
   - `rules/ai-sadist-keeps-downed-target`.

Tests: new `AiBlendPassThroughTests` (every member returns its legacy argument with a null personality); `PersonalityBlendTests`.

TEST BUDGET: the new scenarios; `rules/ai-*`, `rules/dragon-ai-*`, `rules/cleave-*`, `rules/charmed-*`, `rules/combat-end-*`, `rules/shared-square-coup-de-grace`, `rules/invisible-pinpoint-*`, `rules/sanctuary-*`; `smoke/*`. Hash-equal outside the new ids.

### 6. The blend at the movement sites and the out-of-reach order (`b14-blend-movement-sites`)

IDs: batch 13's C1 re-weighting. Neutral, *broad*.

1. `PrefersCharge` at AIS:3568; `AoOCost` at :2601 and :2653; `PreferredRange` at :2597 and `ApproachRangeSquares`; `RiskToleranceFactor` at :1555; `KeepAwayDistance` at :1390; `OrderOutOfReach` at OOR:70-72.
2. Scenarios:
   - `rules/ai-adept-never-charges`, with a `-plain` twin (a plain `goblin_warrior` that charges); the adept itself, because the kiter has no charge site, so the scenario also gives a melee-routed goblin the Cowardly spec;
   - `rules/ai-adept-keeps-distance`: it stays at least 3 squares from every PC for 4 rounds, while a plain goblin closes;
   - `rules/ai-cowardly-avoids-aoo`, `rules/ai-berserker-takes-aoo-path`;
   - `rules/ai-cowardly-caster-defends-before-approach`, `rules/ai-berserker-approaches-first`.

Tests: `AiBlendPassThroughTests`; `AIProfileFrameworkTests` (`TestRangedRiskMultiplierDefaults`, `TestOutOfReachAlternativeDefaults`).

TEST BUDGET: the new scenarios; `rules/charge-*`, `rules/movement-aoo-*`, `rules/ai-*`, `rules/dragon-ai-*`, `rules/total-defense-*`; `smoke/*`. Hash-equal.

### 7. Midpoint full regression (`regression-midpoint`)

Kind regression. Run, through the Unity MCP:
1. the static runner in edit mode, then in Play mode, against `tools/tests/static-suites.json`;
2. `ScenarioHarness.QueueFresh("rules/*,smoke/*", "1-5", "")`;
3. the soak `QueueFresh("soak/*", "1-50", "perSession=10")`.

Neutral claim: every pre-existing job hash equals the baseline. Record the run, the AI-run `actionSet` count, and the AI timing against step 1's baseline in `docs/TESTING.md` 3.2. Change no game code; file any regression as a new issue.

### 8. Maneuver odds (`b14-maneuver-odds`)

IDs: prepares AI-035. Pure refactor.

1. Move the disarm modifiers out of `RollDisarmCheck` and the sunder modifiers out of `ResolveSunder` into shared methods that the roll and the estimate both call. Trip and grapple use their public getters.
2. Add `ManeuverOdds` and `TargetNumbers`; make `ThreatSystem.EstimateExpectedAoODamage` internal; add the step-intent getter in `ManeuverCore.cs`.

Tests: new `ManeuverOddsTests`:
- the trip estimate against 3,000 seeded rolls of the real attack, within 0.03;
- disarm, sunder and grapple by shared-modifier identity;
- an AoO that deals damage cancels a disarm.

Also `GrappleDamageRulesTests`, `BullRushRulesTests` and `AttackValueTests`.

TEST BUDGET: `rules/maneuver-*`, `rules/grapple-*`, `rules/npc-disarm-best-item`, `rules/ai-maneuver-stopgap-*`; hash-equal (the dice order is unchanged).

### 9. Maneuver versus attack by odds and value (`b14-maneuver-scorer`)

IDs: AI-035, AI-060, AI-058, AI-025; CMB-102 item 7. Behaviour, *broad*.

1. `AiManeuverChooser.Decide` replaces `ShouldUseManeuver` plus `TryExecutePreferredManeuver` inside `CreateMeleeStepManeuverEvaluator` (AIS:2832), at NPT:1182 and at the kiter's AIS:1457. `ShouldUseManeuver` stays as a thin public wrapper for the tests that call it.
2. **Delete the stopgap:** `AI/AIManeuverTurnMemory.cs`, `CharacterController.AIManeuverMemory` (:440, and its Clears at :5058 and :13239) and `IsManeuverForbiddenByTurnStopgap` (AIS:2898).
   - The three copies of the no-profile chooser collapse into one proposer, `ChooseNPCFallbackManeuver` (NPT:309), used by `Decide` (AI-025).
   - The summon's trip lambda (NPT:182) is unchanged.
3. **Rewrite the stopgap tests to the scorer:** `TestAiManeuverStopgapTripSucceedsThenAttacks` and `...FailedTripNotRetried` (`GrappleDamageRulesTests.cs:3348, 3426`), and `rules/ai-maneuver-stopgap-trip(-fail)` (`RulesScenarios.cs:243-244`). Update CMB-102 item 7.

Tests:
- new `ManeuverChooserTests`, a golden table: a Strength gap trips; an even match attacks; Improved Trip; many allies; a helpless target gets an attack; Cowardly weighs the AoO more;
- scenarios `rules/ai-maneuver-trip-when-odds-good`, `rules/ai-maneuver-attacks-when-odds-poor`, `rules/ai-maneuver-skips-helpless`.

TEST BUDGET: the new scenarios; every scenario whose AI actor is a Humanoid, Berserk, Grappler, Animal or no-profile NPC (grep the catalog for their ids); `rules/maneuver-*`, `rules/natural-*`, `rules/improved-grab-*`, `rules/npc-*`; `GrappleDamageRulesTests`; `smoke/*`.

### 10. AI-run picks inside the legal set (`b14-ai-legal-gate`)

IDs: AI-080. Behaviour, *broad*.

1. Add `ActorActionSet.AllowsNow(gm, actor, id)`: the executor's `CanStart` plus `CompulsionFilter`, without a full `Build`. Add `AiLegal`.
2. Apply the gates of the AI-080 decision above. `ActionContext.Probe` is set by the harness steps; `ActionSetShadow.Record` counts `aiRun` and `probe` apart, and `summary.json` reports both. `AiOutOfReach.Run` drops an alternative whose entry fails `AllowsNow`.

Tests:
- new scenarios `rules/ai-nauseated-moves-instead-of-attacking`, `rules/ai-spittle-gated-when-move-only`;
- updated expectations, where the "makes no attack" line is gone: `rules/nauseated-move-only-ai`, `rules/slowed-no-full-attack-ai`, `rules/npc-feint-standard`, `rules/overrun-blocked-loses`, `rules/spittle-*`, `rules/breath-*`;
- `ActionCatalogTests`, `ActionPanelParityTests`.

TEST BUDGET: those; `rules/ai-*`, `rules/dragon-ai-*`, `rules/prone-*`; `smoke/*`.

As built (2026-11-23):
- `AiLegal.CanMoveNow` is the move entry (`MovementService.CanStartMovement(Move)`, which already includes `ActionCosts.CanAfford(Move)`) **and** `ActionEconomy.HasMoveAction`: the AI's move request (`MoveRequest.ForAI`) never converts the standard action, so with the plan's bare `CanAfford(Move)` a turn whose move action is spent would enter a move branch that the spend then refuses. Converting the standard action to a second move stays an AI choice for later (step 11, batch 18). The eight sites are, at 0d5874c, `MoveTowardTargetAfterStandardAction`, the aggressive, dragon (and its breath positioning), defensive and swarm approaches, `SelectHoldChargeTarget` and the held charge's approach in `TryDeliverHeldChargeTurn`; the post-breath repositions, the search move and `SelectBestAction` keep `HasMoveAction`.
- The probe is two things: `ActionContext.Probe` (a fixture sets it: the two rage scenarios) and `ActionContext.BeginProbe(actor)` / `ProbeActor`, which `ScriptedSteps.RunAi` sets around each typed step, because the *ForAI wrappers build their own contexts. The runner clears it at each job's start and end.
- `rules/ai-spittle-gated-when-move-only` is in `MonsterAbilityScenarios.cs` (its mouther fixtures live there); `rules/ai-nauseated-moves-instead-of-attacking` in `ActionCatalogScenarios.cs`. Both read the shadow's AI-run count through a Scripted watcher's note.
- Review follow-up (2026-11-23): the AI attack declarations outside `SelectGuardPassingTarget` read `AiLegal.CanAttackNow` too (the summoned creature's routine, whose two move tests became `CanMoveNow`; the ranged kiter's blind attack; the confused creature's attack on the caster, its self-attack and its attack on the nearest creature, the close-with-the-caster move staying AI-043's; the charmed creature's attack); `TryExecuteRangedSpecialAttack` itself asks `AiLegal.CanUse`, covering every ranged special caller; the harness's probe scope is restored in a `finally`; `ActionSetShadow.AiRunLines` keeps AI-run lines apart from probe lines; new scenario `rules/ai-summon-nauseated-no-attack` and an `ActionCatalogTests` check of the engulf and ranged special gates (the scenario's engulf log check cannot reach that gate). Left for later and recorded in AI-080: the attack entry's `CanStart` reads the action economy only, not the core's pinned and condition bars (`CanAttackWithWeapon`). Nauseated is cited at PHB p.310 (printed page) in the lines this step adds.
- Only `nauseated-move-only-ai`, `breath-costs-standard-ai` and `spittle-nauseated-ai` lost a line ("cannot attack: Current condition prevents attacking"; not "makes no attack"), so only they got a new expectation; `slowed-no-full-attack-ai` kept its hash (its refused pick logged nothing hashed), and the "makes no attack" lines of `npc-feint-standard` and `overrun-blocked-loses` are Scripted probes, kept.

### 11. The move left after a standard-action ability (`b14-after-cast-move`)

IDs: AI-077; D1; batch 13's C4. Behaviour, *broad*.

1. Add `ChooseAfterStandardAction` and its default.
2. It replaces the wait-and-`yield break` after a cast, a song, a maintained spell, Lay on Hands, Turn Undead, and the healer's cure and buff, at AIS:320, 328, 381, 390, 417, 427, 484, 498, 821, 1060, 1352, 1444, 1669 and 1713 (the wild shape site, :437, already moves).
3. If `Actions.HasStandardAction` is still true (a quickened or free cast), the routine continues; otherwise the hook runs.
4. The destination comes from `EvaluateMovementOptions` (AoO priced by `AoOCost`). The move runs through `RunAIAction(Move)`, after `AiLegal.CanMoveNow`.

Tests:
- `AiLeftoverHookTests`: the default table; Cowardly and Berserker reorder it;
- scenarios `rules/ai-kiter-casts-then-retreats`, `rules/ai-melee-caster-casts-then-closes`, `rules/ai-quickened-cast-continues-turn`, `rules/ai-fullround-cast-no-move`, `rules/ai-lay-on-hands-then-moves`, `rules/ai-cowardly-casts-then-retreats`.

TEST BUDGET: the new scenarios; `rules/npc-cast-*`, `rules/melee-caster-*`, `rules/ai-spell-retry*`, `rules/bard-inspire-courage-ai`, `rules/paladin-lay-*`, `rules/npc-cleric-*`, `rules/dragon-ai-*`, `rules/turn-undead-*`; `smoke/*`.

As built (2026-11-23):
- `AfterStandardActionChoice` and `AfterStandardActionContext` sit in `AI/AIBehaviorData.cs`; the default reads the context only (`AIProfile.DefaultChooseAfterStandardAction`, `KeepsDistanceRole`: a Ranged style or a casting profile, `StandFirstIfProne`). The tilt is `AiBlend.AfterStandardAction`: the Role's pick scores 1, each other open choice 2 times the traits' RoleDesire for its role (Approach; Retreat for KeepDistance), and only a strictly higher score wins, so with no traits every site keeps the Role's answer.
- `AIService.KeepsRoutineAfterAbility` is the step's item 3 (the standard action still left: a quickened cast); `UseMoveAfterStandardAction` builds the context (`BuildAfterStandardActionContext`), runs the hook and the tilt, traces site "AfterStandard", and moves through `RunAIAction(Move)` to the cell `EvaluateMovementOptions` picks (toward the focus at `AiOutOfReach.ApproachRangeSquares`, or in retreat mode from the nearest foe); StandUp runs the stand-up entry.
- Besides the listed sites, the grappled spellcast and the grappling maintain use the hook too; the wild shape site keeps `MoveTowardTargetAfterStandardAction`, and the breath sites are unchanged (AI-077 now covers only them).
- Two exceptions the plan did not name: a creature that holds a touch charge it plans to deliver (`_heldChargePlans`) skips the hook, so `TryDeliverHeldChargeTurn` keeps its move (SPL-115); and the focus is not "out of reach" when it is an undead the creature turned (`GameManager.IsTurnedByForAI`), because approaching within 10 feet ends the turning (PHB p.159). The first run broke `rules/npc-cleric-turns-undead-ai` that way (run 20261123-184956); the rerun 20261123-185227 keeps its hash.
- A threatened Ranged or casting Role that backs away walked out of the threat and provoked rather than taking a 5-foot step (`rules/npc-cast-threatened`; AI-088 filed, then fixed in the step's review: KeepDistance first tries a 5-foot step out of every threat, `AIService.TryFiveFootStepOutOfThreat`).
- Review fixes: no destination of the move ends within 10 feet of any undead the creature turned (`GameManager.WouldBreakTurningAtForAI`, not only the focus); a quickened touch spell held for delivery ends the routine (`KeepsRoutineAfterAbility`); a threatened prone creature that wants distance, or whose traits Forbid provoking, stays down; the traits' KeepDistance for a threatened creature needs a way to fight from range (AI-039). New scenarios `rules/ai-cleric-move-keeps-turning` and `rules/ai-threatened-caster-steps-away`.
- Moved hashes, each at the turn end after the ability where the creature now moves (`ScenarioTraceDiff.FirstDivergence`): `rules/bard-fascinate-ai`, `rules/bard-inspire-courage-ai`, `rules/dragon-ai-breath-juvenile`, `rules/melee-caster-casts-first-ai`, `rules/melee-caster-casts-first-party-ai`, `rules/npc-cast-threatened` and `rules/paladin-lay-on-hands-ai`, seeds 1-3; the test budget's other 339 jobs are unchanged against run 20261123-183853 (`docs/TESTING.md` 3.4 and the run log).

### 12. The healer reaches its patient (`b14-healer-touch-approach`)

IDs: AI-047, AI-063 (the priority and the approach); Q8 default. Behaviour, narrow (Healer-Role creatures).

1. `HealerAIProfile.DetermineActionPriority` returns Healing only with a castable cure.
2. `ChooseHealerApproach` runs at the failed cure (AIS:491); `FindTouchApproachSquareForAI` gains `maxProvokedAoOs`, with the default unchanged for its other callers.

Tests:
- scenarios `rules/ai-healer-moves-to-touch-and-cures`, `rules/ai-healer-approaches-far-ally`, `rules/ai-healer-critical-provokes`, `rules/ai-healer-no-spell-no-approach` (`goblin_adept`), `rules/ai-healer-cowardly-skips-aoo-approach`;
- `AIProfileFrameworkTests` (`GetPriorityHealTarget`).

TEST BUDGET: the new scenarios; `rules/npc-cleric-*`, `rules/ai-spell-retry*`, `rules/npc-cast-*`, `rules/npc-hold-charge-approach`, `rules/cast-held-*`; `smoke/*`.

As built (2026-11-23):
- `HealerApproachChoice` (Kiter, MoveAndCast, PartialApproach) and `HealerApproachContext` sit in `AI/AIBehaviorData.cs`; the default reads the context only (`AIProfile.DefaultChooseHealerApproach`: a touch square, MoveAndCast; else a partial square, PartialApproach; else Kiter). The tilt is `AiBlend.HealerApproach`: the Role's pick scores 1, each open choice 2 times the traits' RoleDesire for its roles (Heal for MoveAndCast, Heal or Approach for PartialApproach), and only a strictly higher score wins.
- `HealerAIProfile.DetermineActionPriority` takes a required `hasCastableCure`; `HealerAIProfile.HasCastableCure` (over `CastableCures`) counts a castable spell that cures hit points (`SpellDiceRules.CuresHitPoints`: the cure ... wounds spells and Heal, PHB p.32, p.239; not Remove Paralysis, the restorations, Make Whole and the other Healing-effect spells, review of 2026-11-23) and a cleric's spontaneous cure (`AiConversionPlanner.Candidates`), checked by `CheckCasterLegalityForAI`. In Play mode the answer is kept for the frame while the castable spells, slots left and spontaneous casting stay the same, so the scoring passes do not repeat the legality checks. `ScoreSpell` passes it too, so the spell scores match the turn's priority, and gives the Healing bonus only to a hit-point cure. `IsCriticallyWounded` is public.
- `AIService.BuildHealerApproachContext` makes the price, the touch square and the partial square (docs/systems/AI.md 6.13). "Within one move plus one square" is read as: a 5-foot step reaches a square beside the patient (an unthreatened one first), else `FindTouchApproachSquareForAI` finds one within one move. `FindTouchApproachSquareForAI` gains `maxProvokedAoOs`, `aooPricePerProvoker` (each provoker priced as that many squares, ties to fewer provokers), `rejectSquare` and `preferSquare`; with the defaults its other callers behave as before. Q8 as built (delegated reading, 2026-11-23, recorded in RULES_COVERAGE): the cure now is worth 6, so the touch path's provokers times the price may not exceed 6 (`AIService.MaxTouchPathProvokers`: one at the critical price 6, none at 1000, three at 2). A touch square a foe threatens is refused when no castable cure would be cast defensively or quickened there (`CanCastCureWhileThreatened`, `ShouldCastDefensively`; the cast would provoke, PHB p.141, or the AI would decline it), else ranked after the unthreatened ones; the 5-foot step follows the same rule. The partial approach is `EvaluateMovementOptions(..., preferredRangeOverride: 1)` with the new optional `rejectProvokingPaths`, tested on the path the search already computes; it keeps the Healer's own price even for a critical patient, since it touches no one this turn.
- The Healing turn first walks the cures only (`TryExecuteSpellcastAction` with a Healing-effect filter). Without it, an orison with a legal target (Light, AI-069) took the standard action before the approach ran. When the approach chooses Kiter, today's full walk and kiter run.
- MoveAndCast ends the turn after the cure unless it was quickened (`KeepsRoutineAfterAbility`); a cure refused after the move, and a partial approach, leave the standard action to `ExecuteRangedKiterTurn`.
- A hurt living healer always cures itself first, since its own square is in reach. So the never-provoke path of a Cowardly healer that is hurt (its traits Forbid provoking) is reached only by a healer that is not a cure target; `rules/ai-healer-cowardly-skips-aoo-approach` makes the cleric undead for that reason.
- Scenarios: `rules/ai-healer-moves-to-touch-and-cures`, `rules/ai-healer-approaches-far-ally`, `rules/ai-healer-critical-provokes`, `rules/ai-healer-hurt-ally-no-aoo` (one more than the plan named: the Q8 default for a patient above 25%), `rules/ai-healer-cowardly-skips-aoo-approach` and `rules/ai-healer-no-spell-no-approach` (`goblin_adept`); `AIProfileFrameworkTests` (`TestHealerNeedsCastableCureToHeal`, `TestHealerCastableCureDetection` on a real cleric's slots, `TestHealerApproachDefaults` with `MaxTouchPathProvokers`; `GetPriorityHealTarget` stays covered by `TestHealerPrioritizesMostWoundedAlly`) and `AiLeftoverHookTests` (`TestHealerApproachTilt`). The Healer branch writes a "HealerPriority" trace row (the priority against the one before AI-063), which `rules/ai-healer-no-spell-no-approach` asserts (PhysicalAttack, legacy Healing).
- Hashes: the test budget plus every scenario with an AI-run Healer-profile actor (`rules/ai-adept-*`, `rules/ai-area-aim`, `rules/cast-ai-spiritual-weapon`, `rules/druid-wild-shape-ai`, `rules/npc-magic-weapon`, `rules/ai-personality-spawn*`, `rules/ai-cowardly-*`, `rules/ai-kiter-*`, `rules/ai-melee-caster-*`, `rules/ai-threatened-caster-*`), seeds 1-3: 126 pre-existing jobs, none moved against the baseline run 20261123-192519 (run 20261123-193218); the new scenarios pass in run 20261123-193603 (18 Pass).

### 13. Aid another, total defense and posture (`b14-turn-options`)

IDs: AI-063 (the rest); batch 13's C5; D3-D5. Behaviour, *broad*: the hook runs on every melee turn, so hashes move only where an alternative wins.

1. `ChooseMeleeTurnOption` before `PerformMeleeAttackActionWithManeuvers` at AIS:913 and 1765, through `RunAIAction(AidAnother | TotalDefense)`. Total defense in the out-of-reach list is taken only when a foe can reach the creature.
2. `ChooseProneStart` replaces the unconditional stand at AIS:233; a crawl goes through `ActionIds.Crawl`.
3. Update AI_ACTION_COVERAGE (aid another, crawl, drop prone, total defense).

Tests: scenarios `rules/ai-weak-attacker-aids-offense`, `rules/ai-aids-defense-badly-hurt-ally`, `rules/ai-strong-attacker-attacks`, `rules/ai-total-defense-only-when-reachable`, `rules/ai-prone-caster-stays-prone`, `rules/ai-prone-melee-stands`; `AiLeftoverHookTests` (the formulas).

TEST BUDGET: the new scenarios; `rules/aid-another-*`, `rules/crawl-*`, `rules/prone-*`, `rules/total-defense-*`, `rules/confused-prone-*`, `rules/panicked-*`, `rules/ai-*`; `smoke/*`.

As built (2026-11-23):
- `MeleeTurnOption` (Attack, AidOffense, AidDefense, TotalDefense), `MeleeTurnOptionContext`, `ProneStartChoice` (StandUp, StayProne, Crawl) and `ProneStartContext` sit in `AI/AIBehaviorData.cs`; `AIProfile.ChooseMeleeTurnOption` and `ChooseProneStart` are virtual with static `DefaultChooseMeleeTurnOption` and `DefaultChooseProneStart`; no shipped profile overrides them (`AiLeftoverHookTests` checks the four shipped Roles against the defaults).
- The values (`AI/Valuation/TurnOptionValue.cs`, starter constants pinned by `AiLeftoverHookTests`): the plan's aid offense; aid defense times the opponent's share on the ally and a drop weight of 2 when one average hit drops the ally (an addition: without it aiding a badly hurt ally's defense never wins); total defense as the plan's sum with the same share and drop weight, times the creature's exchange rate (Q9 default, filed by this step). S is the full attack's value when the full-round action is free, else one attack. A foe's share is 1 over the creatures of the side it threatens now, else over those it can reach. The aid modifiers are the roll's terms (`BuildAttackBonus` plus the enhancement and masterwork bonus; `SupportActions.GetAidAnotherAttackModifier`, now internal); the ACs the facade's `AcEstimate`.
- The tilt (`AiBlend.MeleeTurn`, gain 0.5): each value scaled by 1 + 0.5 times the RoleDesire of its roles (Attack; Support; Support and Defend; Defend); another option must beat the Role's pick by the margin. `ChooseProneStart` has no tilt: the traits name no posture desire.
- The melee option is synchronous and starts no coroutine when the attack wins, so turns that attack keep their hashes; after a performed alternative the routine runs `UseMoveAfterStandardAction` (step 11) and ends. The dragon routine is not routed. The out-of-reach total defense reads `AIService.AnyFoeCanReach`.
- The prone start is skipped under a fear or turning compulsion (the flight code stands as before), and a charmed creature's turn keeps its unconditional stand (its charm controller's plan is not one `PlannedActionWorksProne` can read). `PlannedActionWorksProne` reads the turn's plan (review rework): a Ranged Role with a usable crossbow; or a casting Role that is not a Melee one, whose best spell (`SelectSpell`) reaches the nearest foe from the ground, holding no ranged weapon but a crossbow and no throwable melee weapon the kiter would throw (CMB-196: PHB p.311 is not enforced on the attack), and, for a Healer, only on an OffensiveSpell turn. The threat test covers every square of the creature's space. A crawl goes through `ActionIds.Crawl` (and the incapacitation check after its AoOs); the crawl square is the valid crawl destination whose whole shifted space no foe could threaten next turn, farthest from the nearest foe; a refused crawl stands up. A creature that stayed prone or crawled is not stood up by the move after its standard action (`AfterStandardActionContext.StayingProne`). No scenario reaches the crawl branch; `TestProneStartDefault` covers its default.
- Reach (review rework): `TurnOptionValue.FoeReach` counts a charge (`ReachKind.Charge`: the foe could charge next round, beyond one move, at least 10 feet and within twice its speed, one attack at +2, PHB p.154) for `AnyFoeCanReach`, the foes' shares and `CouldThreatenNextTurn`; a foe's spells are not counted.
- Aid another's target rule (review rework, a delegated RAW reading of 2026-11-23, RULES_COVERAGE 4.1): the friend and the opponent are engaged in melee when either threatens the other (`GameManager.IsEngagedInMelee`), for the aid-another entry and the PC aid menu alike; before, the friend had to threaten the opponent. The AI's aid-defense candidates follow it; aid offense still needs the ally to threaten the opponent.
- Scenarios as the plan names them, plus `rules/ai-total-defense-only-when-reachable-reachable` (the twin where the foe can reach) and, from the review, `-charge` (a foe that can only charge it), `rules/ai-prone-caster-stays-prone-archer-close` and `rules/ai-prone-healer-stands-to-cure`; the aid offense layout gives the fighter +6 natural armor (the goblin hits only on a 20, the ogre's +2 is not clamped).
- Hashes: first batch 20261123-202343 failed (a fighter off the grid; three older or new scenarios took one-for-one total defense, which led to Q9); rerun batch 20261123-203212 of the test budget, seeds 1-3: 315 jobs, 315 Pass, `aiRun` 0; 288 equal, 21 new, 6 moved: `rules/ai-healer-approaches-far-ally` (the cleric's out-of-reach total defense against a foe that cannot reach it is gone, AI-063) and `rules/ai-healer-no-spell-no-approach` (only the expectation added by step 12's review differs), docs/TESTING.md 3.4. Static Play pass 20261124T013626Z-play OK (`AiLeftoverHookTests` 88/88; `AIProfileFrameworkTests` 172/175 after a fixture fix to the step 12 review's cure test). The soak is left to step 15. After the review rework: batch 20261123-205958 of the same test budget, 324 jobs, 324 Pass; against the first rerun 312 equal, 9 new, 3 moved (`rules/ai-prone-caster-stays-prone`, an expectation detail only: the idle fighter can now charge the adept's square); `rules/ai-healer-approaches-far-ally` kept its hashes (the fighter is beyond its charge). Static Play pass 20261124T020245Z-play OK (`AiLeftoverHookTests` 92/92), docs/TESTING.md 3.2.

### 14. Domain powers (`b14-domain-powers`)

IDs: batch 13's C5 leftover; D7; C14. Behaviour, narrow (clerics with these domains).

1. `GameManager.TryNPCDomainPowerForAI` runs after `TryNPCRageForAI` (AIS:264) for Strength. Death touch replaces an attack step when its value wins. Destruction goes at the smite declaration, beside `ShouldDeclareSmite` (NPT:1171). All of them run through `RunAIAction(DomainPower)` with the domain as the Variant.
2. Correct the p.187 cite to p.186 (D7).

Tests: scenarios `rules/ai-cleric-strength-before-full-attack`, `rules/ai-cleric-death-touch-low-hp-foe`, `rules/ai-cleric-destruction-smite-first-swing` (domains set by `Tweak`); `AiLeftoverHookTests`.

TEST BUDGET: the new scenarios; `rules/greater-turning-sun-ai`, `rules/paladin-smite-*`, `rules/npc-cleric-*`; `smoke/*`.

As built (2026-11-23):
- `AIProfile.ChooseDomainPower(self, in DomainPowerContext)` with its twin `DefaultChooseDomainPower`; the context (`AIBehaviorData.cs`: `DomainPowerSite`, `DomainPowerChoice`, `DomainPowerContext`) carries pure numbers. The default: Strength when it is open and the creature will make melee attacks; else Death when the touch's value beats S; else Destruction when the smite's gain is positive; else None. No trait tilts it (both sides of each choice are Attack-role actions), so the shipped profiles equal the default.
- Values (`AI/Valuation/DomainPowerValue.cs`): `KillChance` is the exact chance that the cleric level's d6 reach the current hit points (a table, at most 40 dice); `DeathTouch` = P(touch) x that chance x current HP; `SmiteGain` = P(hit at +4) x (D + level) - P(hit) x D on the AC the creature knows. S at the melee step is the full attack's expected damage when the full-round action is free, else one attack's.
- `GameManager.TryNPCDomainPowerForAI(npc, site, target)` returns at once unless the creature has a Strength, Destruction or Death use left (`HasAiDomainPowerLeft`), so creatures without them play as before; it asks `AIService.ChooseDomainPowerForAI` (a "DomainPower" trace row, legacy None) and runs `RunAIActionNow(DomainPower)` with the domain as the Variant (a Death touch with its Target), asking once more after Strength. Four sites: the turn start after `TryNPCRageForAI` (Strength, only for a grappling cleric since the step's review: elsewhere the turn's later choice, aid, total defense, a cure or a retreat, would waste the use), the top of each melee step (`PerformMeleeAttackActionWithManeuvers`: Strength, and Death, which then ends the step and uses the move left), the smite declaration in `NPCPerformAttack` beside `TryNPCDeclareSmite` (Strength if not used yet, and Destruction), and, added after the first run, a charge's attack in `ChargeCore` (Strength and Destruction: the `-far` twin's cleric charged and attacked with no feat).
- Death touch targets: the foes the entry's own `CanExecute` accepts, the PC button's rule: a living creature (`DeathTouchTargetValid`: not undead or a construct) past every direct-attack gate (`AttackLegality.Check` with all checks). Review rework: `ActivateDeathTouch` runs the attack stages (it ends the cleric's Sanctuary, Hide from Undead and invisibility, rolls the target's ward save, a failure losing the touch and the standard action but not the use, D6's reading), its touch is the shared melee touch formula (`GetManeuverTouchAttackModifierAgainst`, shaken's -2 included), and death ward (PHB p.217; CRE-296 narrowed) no longer refuses the touch: the use is spent and a hit has no effect. The AI's P(touch) reads `TargetNumbers` and the current HP `TargetNumbers.CurrentHitPoints` (true until batch 17).
- The p.187 cite in `DomainPowerActionExecutor` is p.186 (D7).
- C14 does not hold for Sun: the turn undead choice (`TryNPCTurnUndeadTurnForAI`) runs the plain turning, and the greater turning is reached only from the PC menus; filed as AI-090.
- Scenarios as the plan names them, plus `rules/ai-cleric-strength-before-full-attack-charge` (the feat at a charge's attack), `-far` (cleric speed 1: no attack, no feat) and `rules/ai-cleric-death-touch-healthy-foe-attacks` (the touch's twin: it attacks). Final run 20261123-212816 (the test budget plus `rules/charge-*`, seeds 1-3): 105 jobs, 105 Pass; against each job's latest earlier run 87 equal, 18 new, 0 moved. Static Play pass 20261124T023009Z-play: `AiLeftoverHookTests` 113/113; `OmniscienceLintTests` 13/14 by step 13's `TurnOptionValue.cs` (AI-089). The review's rework added `rules/ai-cleric-death-touch-ends-invisibility` and `-sanctuary-save` and `TestDeathTouchFormulaShaken`, and does not baseline AI-089 (a batch regression), so the runner reports `OmniscienceLintTests` as REGRESSION until it is fixed. Review run: batch 20261123-215235 (the test budget plus `rules/sanctuary-*`, seeds 1-3), 120 jobs, 120 Pass; 108 equal, 6 new, 6 moved by expectation text only; Play pass 20261124T025438Z-play, `AiLeftoverHookTests` 117/117. docs/TESTING.md 3.4.

### 15. Final full regression (`regression-final`)

Kind regression. Run the same set as step 7. Then:
- **Hashes.** Every moved hash of steps 9-14 needs an explained first divergence, and the neutral scenarios must still match.
- **Legal set.** `actionSet.aiRun` is 0.
- **Timing.** Compare p95 and max with the baseline; list turns over 50 ms as warnings.
- **Records.** Record the run in `docs/TESTING.md` 3.2 and 3.5.
- **Issues.** Close or narrow AI-077, AI-047, AI-063, AI-080, AI-035, AI-060, AI-058, AI-025 and AI-012. AI-089 (step 13's `TurnOptionValue.cs` past the knowledge facade) must be fixed before this run: it is not baselined, so `OmniscienceLintTests` reports REGRESSION until then.
- **CLAUDE.md "Current state":** batch 14 done; next is the grouped AI turns (`docs/designs/grouped_ai_turns.md`), then batch 15.

## Behaviour changes

| Step | Who changes | What |
|---|---|---|
| 1-8 | nobody | machinery only; new scenarios only add hashes |
| 9 | Humanoid, Berserk, Grappler, Animal and no-profile NPCs, and AI-run party members | trips, disarms and grapples only when their value beats the attack; no maneuver on a helpless target; no per-turn memory |
| 10 | AI-run creatures that are nauseated, slowed or out of actions | no refused attack; the move is used instead |
| 11 | every AI caster, singer, Lay on Hands user and turner | its move action is used after the standard action |
| 12 | Healer-Role creatures with cures | they reach a patient within one move; Healing only with a castable cure |
| 13 | AI melee creatures | aid another or total defense where it wins; prone casters and crossbow users may stay prone |
| 14 | AI-run clerics with Strength, Destruction or Death | they use those powers |

## Risks

- **The soak shifts from step 9 on.** Maneuver-heavy creatures (goblins, orcs) now attack more often. Compare the win rate and the mean rounds with the batch 13 soak (48 wins, 2 defeats, mean 4.42 rounds) and explain the change.
- **Starter constants.** The maneuver and leftover values are starter numbers; `ManeuverChooserTests` and `AiLeftoverHookTests` pin them, so a retune is a visible diff.
- **Captain layout.** The Smart captain at half weight (C2) may not differ from the plain captain in a casual layout; step 5 tunes the layout first in a unit test.
- **Decision cost.** The blend adds work per candidate (`AiAttackGuard.Passes` already runs `AttackIntent.ForAI` per candidate). Step 1's timing shows whether the 50 ms budget holds.
- **Concurrent issue edits.** Another workflow edits `docs/issues/*`. Every step files issues at the next free ID.

## Not in scope

- **Batch 15 (NPC spellcasting):** ENC-021, SPL-041, Adept casting (CHR-024), consumable choice (AI-009). With it, `goblin_adept` and other DMG casters get spells.
- **Grouped AI turns** (`docs/designs/grouped_ai_turns.md`): after batch 14, before batch 15.
- **Batch 17 (fair sight):** knowledge levels and sources, Rules mode, `EncounterEntityId`, AI-051, the tier filter on facts.
- **Batch 18 (stances):** stances and Withdraw, AI-010's flee thresholds, Disciplined, Leader and Protector, the generalised Support stance, `StayNearWoundedAllies`, and the Cruelty and MoraleBreak dials.
- **Batch 22:** the one evaluator over `ActorActionSet`, replacing the Role hooks and the C2 attack-mode rule.
- **Also out:**
  - default trait mixes for existing creatures (Q7);
  - implied roles (C1);
  - the remaining domain powers (C14);
  - AI-049 (`SelectLowestHPEnemy` filters);
  - the summon routine's choices.
