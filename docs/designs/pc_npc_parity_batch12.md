# Batch 12: the rest of the PC/NPC parity plan

> Status (2026-10-28): in progress; steps 1-10 landed (e67c2e3 to 37f6ae9) and the step 11 midpoint regression ran on 37f6ae9 (docs/TESTING.md 3.2; filed TST-043, TST-044 and CHR-087; CMB-176 seen in Play mode); step 12 built (CMB-093, CMB-104 and AI-005 closed, CMB-092 narrowed to the enclosed NPC's lost turn and areas; a minimal `AttackIntent` added for step 13 to extend; docs/TESTING.md 3.4); steps 13-25 not started. Until a test-only fix of TST-043 and TST-044 (both scenario-only: force the dummies' `[save]` d20 with `.Force(20, 2, ...)`; count only the Ui wizard's refusal lines), recommended before step 25, every full rules run ends with verdict FAIL from exactly 7 expected Fail jobs (`bard-fascinate-ai` and `bard-fascinate-scripted` on seed 5, `quicken-once-per-round` on seeds 1-5); the step 25 final regression should treat those 7, and only those, as known. Earlier status (2026-10-23): unbuilt; design for building, not started. Produced by a read-only design workflow on master 35480e04 (three code mappers, two independent designs, one judge who checked the key claims against the code and the books). PC_NPC_PARITY section 5, steps 1-8, 11 and 12, plus the cast-core quirk bits batch 11 left (CORE-039, SPL-161, SPL-160, SPL-156). Step 9 (the cast core) was batch 11; step 10 (the action catalog) is batch 13. Nothing here was compiled, run or verified in Play mode. Line numbers are anchors from 2026-10-23: they drift, so anchor on the member names.

## Summary

Today many rules run differently depending on whether a creature is controlled by the player or by the AI. A paralyzed troll on the enemy side never regenerates, but a PC troll would. Only PCs' rage runs out. Only AI-run creatures notice invisible foes by sound, and only they roll against Sanctuary (and they reroll it every time they look). A player's full attack never grabs with Improved Grab, only the player's single attack can Cleave, and enemy archers never run out of arrows. Charges, maneuvers and movement each have a PC copy and an NPC copy that disagree (flanking on a charge, Grease, the 5-foot step, which maneuvers replace an attack). Saving throws are rolled in three places, and none of them applies "a natural 1 always fails, a natural 20 always succeeds".

This batch builds one shared routine for each of these: the start of a turn, "may this creature attack that one", "what happens after an attack lands", the full attack, the charge, the maneuver, the movement step and the saving throw. Each routine asks a "decider" at the points where the rules leave a choice (whom to Cleave, whether to grab, which item to disarm). The player answers through the menus and the AI answers through its profile; that is the only remaining difference between the two sides. The work goes in the batch 11 order: refactors that must not change any fight come first and prove it by keeping every scenario trace identical, then one rules fix per commit. Expect the enemy to get tougher and the soak numbers to move: dragons breathe again every few rounds, disabled enemies keep fighting, archers spend arrows, fighters Cleave, and natural 1s and 20s on saves count. Each such change is RAW, and the midpoint and final regressions explain the shifts.

## Rules decisions (2026-10-23, delegated; the owner may overrule)

All of these were settled by the judge under the owner's delegation (2026-10-11, 2026-10-14): where the books are silent or ambiguous, the reading with the most textual support is applied, recorded in `docs/systems/RULES_COVERAGE.md` 4.1 when its step lands (labelled "delegated RAW reading" in code comments) and reported. Pages are printed pages (pdf page = printed page + 1). "Explicit RAW" needs no 4.1 row.

| ID | Question | Kind | Applied in step |
|---|---|---|---|
| D1 | Bombardier beetle acid spray: action, trigger, frequency | gap-best-reading | `b12-acid-spray-numbers-charge-flanking`, `b12-cleave-and-attacked-triggers` |
| D2 | Partial charge, and charging while slowed | explicit-RAW + sub-reading | `b12-charge-core` |
| D3 | Turn-start effects and resets on a turn the creature cannot use | gap-best-reading | `b12-turn-start-raw` |
| D4 | Which conditions skip the whole turn | gap-best-reading | `b12-skip-gate-compulsions` |
| D5 | Disabled creatures act and move | explicit-RAW | `b12-action-limits` |
| D6 | Sanctuary and Hide from Undead: when the save is rolled and what a failure costs | gap-best-reading | `b12-attack-legality` |
| D7 | Cleave and Great Cleave: what "drops" means, reach, AoOs | gap-best-reading | `b12-cleave-and-attacked-triggers` |
| D8 | Improved Grab per hit, inside a pounce and after an AoO | gap-best-reading | `b12-post-attack-raw` |
| D9 | When Rapid Shot is declared | gap-best-reading | `b12-attack-sequence-executor` |
| D10 | Grapple escape: the square, and whether a grappler must oppose | explicit-RAW + sub-reading | `b12-maneuver-core` |
| D11 | Overrun: when, the defender's AoO, Improved Overrun | explicit-RAW | `b12-overrun-move-through` |
| D12 | Grease saves and the Balance check | gap-best-reading | `b12-movement-step-hook` |
| D13 | Natural 1 and 20, Luck and countersong on every save | explicit-RAW | `b12-one-save-roll` |
| D14 | Breath weapon and ranged-special recharge per creature | explicit-RAW | `b12-turn-start-raw` |
| D15 | Charm broken by the charmer's allies; disarm and sunder item chosen by the attacker | explicit-RAW | `b12-post-attack-raw`, `b12-maneuver-core` |

### D1. Bombardier beetle acid spray

**Decision.** The spray is a reaction that costs no action. It may be used out of the beetle's turn, at most once per round (between the start of one of its turns and the start of the next). It is triggered when the beetle is attacked or disturbed:
- *attacked*: a creature makes an attack roll against it (an AoO included), attempts a maneuver against it, or targets it with a hostile spell;
- *disturbed*: a creature grapples it, or a foe's effect damages it.

The spray comes right after the triggering attack or effect has resolved. It is a 10-ft cone that must include the triggering creature if any legal aim does. Each creature in the cone makes a DC 13 Fortitude save (10 + half of 2 HD + Con +2) or takes 1d4+2 acid damage. The damage is rolled once for the spray (decision D6 of batch 11). The cone is an area effect, so a target's Sanctuary does not stop it (PHB p.274). It does count as the beetle attacking, so it ends the beetle's own Sanctuary or invisibility. Whether to spray is a decider choice: the AI sprays when the cone would catch at least one foe, and a controlled beetle gets a prompt.

**Basis.** MM p.285 (the ability: triggered "when attacked or disturbed", once per round, the cone, the save and the damage). MM p.315 (an extraordinary ability is a free action unless noted). PHB p.142 (most extraordinary abilities are not actions and happen reactively). The current use, the spray as part of the beetle's own attack, has no textual support. "Once per round" only makes sense if the spray can come outside the beetle's own turn.

### D2. Partial charge and Slow

**Decision.** A creature limited to one standard or move action on its turn may charge as a standard action, moving up to its speed rather than double. This covers slowed, disabled and staggered creatures and the surprise round. A slowed creature's speed is already halved. A nauseated creature cannot charge, because it cannot attack.

**Basis.** PHB p.154 (charge, the single-action option); PHB p.280 (Slow allows one move or standard action and no full-round actions; a partial charge is a standard action); DMG p.301 (nauseated, from the index: the local text stops at pdf p.308 and lacks p.301).

### D3. Turn-start effects on a turn the creature cannot use

**Decision.** At the start of every creature's turn, whether or not it can act, these all happen:
- ongoing damage (Melf's Acid Arrow, Flaming Sphere);
- regeneration;
- cooldown ticks (breath, ranged special, terrain manipulation);
- the once-per-round flags;
- Aid Another expiry;
- the per-turn resets of `StartNewTurn` (actions, AoO count, fighting defensively, readied counterspell, movement flags).

Concentration maintenance keeps its existing rule (`MaintainConcentrationOnSkippedTurn`). Rage passes at the barbarian's initiative count, the way bardic music does.

**Basis.** MM p.314 (regeneration works every round); PHB p.138 (durations run on the creature's own initiative count); PHB p.142 (fighting defensively lasts until your next turn); PHB p.25 (rage duration in rounds).

### D4. Which conditions skip the whole turn

**Decision.** The turn is skipped only when the creature can take no action at all:
- dead, dying, stable, unconscious or asleep;
- helpless, paralyzed or petrified;
- stunned, dazed or cowering;
- fascinated, under Hideous Laughter, or Blown Away.

The other conditions do not skip the turn:
- *Nauseated:* the creature gets the turn with a single move action.
- *Pinned:* the creature acts through grapple options only (escape and the other grapple actions the rules allow a pinned creature).
- *Panicked:* the creature flees, and may use special abilities to escape.
- *Turned:* the creature flees. When it cannot flee it cowers, and then it skips.

**Basis.** DMG p.301 conditions (index only, see D2); PHB p.156 (pinned); PHB p.159 (turned undead flee or cower); the existing pin reading (RULES_COVERAGE 4.1, owner decision 2026-10-07).

### D5. Disabled creatures act and move

**Decision (explicit RAW).** A disabled creature (0 hp, or negative hp with Diehard) takes one move or one standard action each turn. A standard action, or any other strenuous action, deals it 1 damage afterwards. A move action does not. A disabled creature is not incapacitated, so an AoO that leaves a mover at exactly 0 hp does not end its move; one that drops it below 0 does. This matches the existing combat-end predicate (`CombatEndRules.IsOutOfFight`, owner definition 2026-10-07: disabled is in the fight).

**Basis.** PHB p.145.

### D6. Sanctuary and Hide from Undead

**Decision.**
- **Sanctuary.**
  - **When it is rolled.** Each opponent makes one Will save per casting, when it commits a direct attack on the warded creature: an attack roll (an AoO included), a maneuver, a touch, or a spell that targets it.
  - **On a success** the opponent is unaffected by that casting for the rest of its duration.
  - **On a failure** the attempted attack is lost and the opponent cannot directly attack that creature for the duration:
    - a weapon attack loses its attack step, or the standard action if it was a single attack;
    - a spell loses its casting action, but the slot is kept because the spell was never cast.
  - **Predictions never roll.** Choosing or highlighting a target, and AI scoring, are not attempts.
  - **Area spells and effects** ignore the ward.
- **Hide from Undead.**
  - An intelligent undead makes one Will save per casting, and a success lets it perceive the warded creature. It does not end the spell.
  - Mindless undead get no save.
  - The spell ends for every recipient when a warded creature attacks, turns or commands undead, or touches an undead.
  - As built in step 12 (delegated RAW reading 2026-10-28, RULES_COVERAGE 4.1): the save is rolled at the undead's turn-start perception step (or its first attack), and until it passes the warded creature is unseen by it (total concealment); a failure loses no attack.

**Basis.** PHB p.274 (Sanctuary: one save per opponent per casting, the failed save's cost, area spells unaffected); PHB p.241 (Hide from Undead). The slot-kept sub-reading follows the "cannot follow through" wording.

### D7. Cleave and Great Cleave

**Decision.**
- **When it triggers.** A foe "drops" when the attack's damage takes it below 0 hp, kills it, or knocks it unconscious (nonlethal damage included). A target left disabled at exactly 0 hp has not dropped.
- **The extra attack.**
  - It is made at once against another creature within the attacker's reach (reach weapons and large creatures included), with the same weapon at the same bonus as the attack that dropped the foe.
  - No 5-foot step comes first.
  - It may follow any melee attack: single, full attack, charge or AoO.
- **How often.** Cleave once per round. Great Cleave has no limit.
- **The target** is the attacker's choice (decider).

**Basis.** PHB p.92 (Cleave; Great Cleave). The "typically" examples in the text (below 0 hp or killed) set the meaning of "drop", and an unconscious creature falls just as one below 0 hp does.

### D8. Improved Grab

**Decision.** Improved Grab triggers on each hit with the named attack, whatever the action: a single attack, each step of a full attack, a charge, and an AoO. Inside a pounce the grab attempt follows the hit at once. The later steps of that pounce then follow the per-step mid-sequence behaviour that the NPC melee path already has (scenario `rules/improved-grab-mid-sequence`). The choice to grapple, hold with -20 or skip is the grabber's, through the decider.

**Basis.** MM p.310 (Improved Grab: "if it hits", a free action; the -20 hold option).

### D9. Rapid Shot

**Decision.** Rapid Shot is declared before the first attack of the turn, and declaring it commits the creature to the full-attack action. It gives the extra ranged attack at the highest bonus and -2 on every attack that round. This follows the flurry precedent (`CharacterController` around :819-823).

**Basis.** PHB p.99 (Rapid Shot: -2 on every attack in that round, needs the full attack action); PHB p.143 (choosing between attack and full attack after the first attack). Rapid Shot's penalty applies to the first attack, so the choice cannot wait until after it.

### D10. Grapple escape

**Decision.**
- **The square.** The escaping creature chooses the free square adjacent to its opponents (decider). There is no random pick.
- **Opposing an escape.** Each grappler may choose not to oppose the escape. Not opposing counts as losing against that grappler.
  - This corrects both designs and the CMB-098 entry, which said RAW has no such option.
  - The existing PC prompt is RAW. The defect is only that the AI side has no choice: it becomes a decider choice for both sides, and the AI default is always to oppose.

**Basis.** PHB p.156 (escape: move into any space adjacent to the opponents; the parenthetical that opponents need not try to hold you).

### D11. Overrun

**Decision (explicit RAW).**
- **When.** An overrun is a standard action taken during a move or as part of a charge, never a standing action.
- **The AoO.** Moving into the defender's space provokes an AoO from the defender, Improved Overrun or not (CMB-086). This corrects the foundation design's claim that the feat suppresses it.
- **Avoid or block.** The defender chooses (decider). Improved Overrun removes the avoid option and adds +4.
- **The result.** The winner knocks the defender prone. A loser moves 5 ft back.
- **Not in scope.** The defender's knock-prone counter after a failed overrun (CMB-117) stays open.

**Basis.** PHB p.157-158 (overrun steps 1-4); PHB p.96 (Improved Overrun).

### D12. Grease

**Decision.**
- **The Reflex save.** A creature in the area when the spell is cast makes a Reflex save or falls. It repeats the save on each of the caster's later turns while it stays in the area. The spell's "your turn" is read as the caster's, because spell text addresses the caster.
- **Moving.** A creature moving within or into the area makes a DC 10 Balance check, once per round on its first square of greased movement. Success lets it move there at half speed for that round. Failure means it cannot move that round and must make the Reflex save. Failure by 5 or more means it falls.
- **No entry save.** There is no Reflex save merely for entering the area.

**Basis.** PHB p.237 (Grease); PHB p.67 (Balance: one check covers a round of movement at half speed). The save's turn is a sub-reading.

### D13. Saving throws (explicit RAW)

**Decision.** Every save roll, on every path, applies:
- a natural 1 always fails and a natural 20 always succeeds;
- the Luck domain reroll when it is armed;
- countersong against sonic or language-dependent effects;
- Evasion and Improved Evasion on Reflex-half damage (the Javelin of Lightning and breath weapons included).

**Basis.** PHB p.177 (automatic failures and successes); PHB p.187 (Luck domain); PHB p.29 (countersong); PHB p.50 (evasion).

### D14. Breath weapon recharge (explicit RAW)

**Decision.** Each creature recharges as its MM entry says. Examples to verify while auditing:
- dragons: 1d4 rounds (MM p.69);
- hell hound: 2d4 rounds (MM p.151);
- chimera: 1d4 rounds;
- behir: once every 10 rounds;
- gorgon: 5 times per day;
- ankheg: once every 6 hours;
- digester (acid spray): 1d4 rounds (MM p.59).

Today every entry uses a fixed `RechargeRounds`, and nothing ticks it (AI-032).

### D15. Charm and item choice (explicit RAW)

**Decision.** Any threatening act by the charmer or the charmer's apparent allies breaks Charm Person (PHB p.209). The attacker chooses which held item a disarm or sunder targets (PHB p.155, p.158).

## Architecture

### End state

The batch builds five cores on the batch 11 cast-core pattern. Each core has:
1. a request built by a source-specific builder;
2. a pure legality function that rolls no dice and returns `Legality`;
3. an executor that commits costs only through `ActionEconomy`, asks a decider at fixed points, and returns an outcome.

No executor does UI continuation (`ShowActionChoices`, `EndActivePCTurn`, `AfterAttackDelay`); the thin PC and AI wrappers do. The only reads of `IsControllable` are in the `*Deciders.For(actor)` factories and in the PC UI resets of `StartPCTurn`. Each factory takes an optional override, so batch 13 can pass `ActorSource.Forced`.

The cores are:
- **Turn start.** Partial `_Core/GameManager.TurnStart.cs` holds two methods.
  - `void BeginCombatantTurn(CharacterController actor)` runs synchronously before the skip gate for every actor. It runs the D3 effects in this order:
    1. Flaming Sphere tick.
    2. Melf's Acid Arrow tick through `DealDamage`.
    3. Regeneration.
    4. `TickTurnCooldowns` (bombardier, breath, ranged special, terrain).
    5. The once-per-round flags.
    6. Aid Another expiry.
    7. `actor.StartNewTurn()` (rules resets only).
    8. `EvaluateCombatEnd("TurnStart")`.
  - `void BeginActingTurn(CharacterController actor)` runs after the gate. It runs the Turn Undead prune and proximity check (one routine, `CheckTurnUndeadProximityBreakingForCleric`), the perception pass, and the compulsion pass, which sets `actor.CurrentTurnStart` (a small `TurnStartState { TurnCompulsion Compulsion; }`, reset in `ResetForNewCreature`; batch 13 reads it).
  - The skip gate becomes `ConditionRules.DeniesAllActions(actor)` (D4).
  - Rage ticks in `OnInitiativeCountReached`.
- **Action limits.** `ActionEconomy` keeps its settable `SingleActionOnly` field (tests set it directly: AIProfileFrameworkTests:1446, GrappleDamageRulesTests:1637) and gains `MoveActionOnly`, reset in `Reset()`. While it is set, `HasStandardAction` and `HasFullRoundAction` are false.
  - One formula, `CharacterController.RefreshActionLimits()`, writes both fields:
    - single action when disabled, staggered, `Stats.IsSingleActionsOnly` or slowed;
    - move only when nauseated.
  - It is called from `StartNewTurn`, `OnHPStateChanged` and the Slow, Haste and Nauseated apply and remove sites.
  - The refresh never grants back an action already spent.
- **Attack core** (`Combat/Core/`):
  - **`AttackIntent`.**
    - Fields: `AttackMode Mode` (Melee, Ranged, Thrown); `AttackStepKind Hand`; `ItemData Weapon`; `bool RapidShot, Flurry, TwoWeapon, FightDefensively, HeldChargeTouch, IsAoO`; charge info.
    - Builders: `ForUi(gm)` reads `_currentAttackType`/`_pendingAttackMode` once; `ForAI(actor, target, choice)`.
  - **`AttackLegality.CanDirectlyAttack(attacker, target, DirectAttackKind kind, AttackIntent intent)`**, with kinds Weapon, Natural, Touch, Maneuver, AoO, Charge and Spell. It returns `Legality` after checking:
    - the Resilient Sphere;
    - the one-body-part hold bar (`IsBarredFromOrdinaryAttackOn`/`IsBarredFromSpecialAttackOn`);
    - reach and range for the kind;
    - the action limit;
    - the ward ledger, read only.
  - **`WardSaveLedger`.** Keyed by (attacker, ward effect instance). `Predict` never rolls; `Commit` rolls once and logs once (D6). It is cleared on slot reuse.
  - **`IAttackDecider`** (`UiAttackDecider`, `AiAttackDecider`, `AttackDeciders.For`), with `ChooseNextStep`, `ChooseImprovedGrab`, `ChooseCleaveTarget`, `ChooseHasteNaturalAttack` and `ChooseAttackedReaction` (D1). Each answers through a callback, like `ICastDecider`.
  - **`OnAttackResolved`** (partial `_Core/GameManager.AttackResolved.cs`, `IEnumerator OnAttackResolved(AttackResolution res, PostAttackOutcome outcome)`) runs once per resolved attack step on every path, in this order:
    1. charm, fascination and command breaks;
    2. thrown drop and ammunition spend;
    3. Concentration per hit;
    4. melee reactions;
    5. target-side triggers (D1);
    6. free trip;
    7. Improved Grab;
    8. Cleave;
    9. summon cleanup;
    10. combat end for target and attacker;
    11. publishing `AttackResolvedEvent` (tactics README 1.4 rule 6).
  - **`RunAttackSequence`** (partial `_Core/GameManager.AttackCore.cs`, `IEnumerator RunAttackSequence(AttackSequenceRequest req, AttackSequenceOutcome o)`) is one per-step loop for every full attack, with builders `ForUiAttack`, `ForUiFullAttack`, `ForAI`, `ForAoO` and `ForCharge`. Its step counts come from `CharacterCombatStats.GetAttackBonuses`, plus Rapid Shot and Haste.
- **Maneuver core** (`Combat/Maneuvers/ManeuverCore.cs`):
  - `ManeuverRequest`;
  - `ManeuverActionCost.For(type, actor)` for every `SpecialAttackType`;
  - `CanStartManeuver`;
  - `IEnumerator ExecuteManeuver(req, ManeuverOutcome)`, which pays, then makes the initiation AoOs, then resolves, then calls `OnAttackResolved` for the maneuvers that are attacks;
  - `IManeuverDecider`, with `ChooseItem`, `ChooseBullRushPush`, `ChooseCounterTrip`, `ChooseOverrunResponse`, `ChooseGrappleMoveDestination`, `ChooseEscapeSquare` and `ChooseOpposeEscape`.
- **Charge core** (`Combat/Maneuvers/ChargeCore.cs`): `ChargeRequest`, `CanCharge` (which admits the partial charge, D2), and `ExecuteCharge`. The executor runs the step loop, then `ResolveChargeEnd`: flanking, smite and Spirited Charge, then `RunAttackSequence(ForCharge)` (pounce and rake are steps of it), or the bull rush, or the overrun.
- **Movement.**
  - `MovementService.CanStartMovement(actor, MoveKind kind, int squares)`, with kinds Move, DoubleMove, Run, Withdraw, RestrictedWithdraw, FiveFootStep and Charge, is called by every PC button and every AI mover.
  - `IMovementStepHook` instances are registered in `MovementService` and run on each step inside `CharacterController.MoveAlongPath` (Grease, zone entry, the Wall of Ice breach, step cost).
- **Saves.** `Character/Stats/SaveRollRules.cs` is the one d20 roll: natural 1/20, Luck, `SaveRules.Modifier`, countersong. It returns roll, modifier, total and success. `SavingThrowResolver.ResolveSave` and both `SpellSaveResolver.RollSave` overloads build their own result types from it (the two result types and the two `SaveType` enums stay; SaveRules already converts the enums). Evasion is `SpellSaveResolver.ApplyEvasion` for every Reflex-half caller. This is PARITY's "SaveService"; no MonoBehaviour service is added.

**Deleted by the end:**
- `LegacyCastQuirk.cs` and its plumbing;
- `BeginNPCTurnForAI`;
- `CFS.PerformFullAttack`, `ExecuteIterativeAttacks`/`ExecuteDualWieldAttacks` and `GMCA.PerformFullAttack`;
- `CharacterStats.GetIterativeAttackBonuses`, `GetIterativeBaseAttackBonuses` and `IterativeAttackCount`;
- the overrun dead code (`ResolveOverrunSpecialAttack`, `ShowOverrunResponsePrompt`);
- the `PostAttackQuirk` bits;
- the Lion's Shield's `SummoningService` registration.

### Migration rule

This is the batch 11 strangler pattern:
- **Pure moves.** A pure-move step keeps every pre-existing scenario trace hash. Any behaviour difference between paths is kept on purpose as a named quirk bit set by that path's builder (`PostAttackQuirk`, the batch 11 `LegacyCastQuirk` pattern). If a pure move changes a hash, stop and find the cause.
- **Behaviour changes.** A behaviour-change step removes bits or changes a rule for both sides at once and explains its trace differences in the commit body (`ScenarioTraceDiff.FirstDivergence`, docs/TESTING.md 3.5).
- **Pooled slots.** New per-creature state is cleared in `ResetForNewCreature` or `GameManager.ResetCharacterSlotForSpawn`. This covers `CurrentTurnStart`, the ward ledger entries, the Cleave and spray flags, the perception tracker, the Rapid Shot declaration and the attack intent. CRE-046 has no entry (closed); the rule lives in CLAUDE.md. `NpcSlotReuseTests` covers it.
- **Coroutines.** Executors may yield, but `BeginCombatantTurn` may not (CORE-012: the skip gate calls `NextInitiativeTurn` synchronously).

### Corrections to the input designs and maps (checked in code or books 2026-10-23)

- **CRE-046 and CMB-122 have no entries, so both are closed.** The rest of the 51 in-scope IDs, plus AI-032 and CORE-003, are open.
- **The Turn Undead proximity check is already one routine.** `CheckTurnUndeadProximityBreakingForMover` only calls `...ForCleric` (TurnUndeadSystem.cs:235-242).
- **Breath weapons and ranged specials never recharge.** The only decrements are inside the three uncalled ticks (CharacterController.cs:4676, :4703, :4688). Recharge is a fixed `RechargeRounds` per entry (NPCDatabase.cs:299; for example the behir entry has 3, though MM gives every 10 rounds).
- **Javelins and darts never drop.** They are `WeaponCategory.Ranged` with `IsThrown` (ItemDatabase.cs:325-332). `ResolveThrownWeaponAfterAttack` (GameManager.cs:4968) requires `IsThrowableMeleeWeapon` (:5233, Melee category) and the PC global `_currentAttackType == Thrown`, and it writes the PC field `_equippedWeapon`.
- **`SingleActionOnly` must stay a settable field.** Tests assign it.
- **`SavingThrowResolver.SaveResult` and `SpellSaveResolver.SaveResult` are different types.** One delegating to the other needs the shared core above.
- **Improved Overrun keeps the AoO (D11).** Not opposing a grapple escape is RAW (D10). A target left disabled is not "dropped" for Cleave (D7). The acid spray is a reaction, not an own-turn action (D1).
- **The "SaveService"** in PARITY section 5 step 12 is a static rules class here, not a service.

## Steps

Every C# step runs `bash tools/compile_check.sh` (exit=0, errors=0, warnings=507; a jump is a regression). Each step keeps the docs true in the same commit:
- delete fixed entries in `docs/issues/<PREFIX>.md` and update counts and Top issues in `docs/KNOWN_ISSUES.md`;
- update `docs/systems/PC_NPC_PARITY.md` section 5;
- record each applied D-reading in `docs/systems/RULES_COVERAGE.md` 4.1, dated 2026-10-23;
- update `docs/ARCHITECTURE.md` or its part file and `docs/DEVELOPMENT_RECIPES.md` where structure or a recipe changes;
- remove only the waivers, `ExpectKnown` pins and `tools/tests/static-suites.json` entries that the step fixes.

New `.cs` files need their Unity-generated `.meta` (refresh in edit mode; never hand-write). Grep `Assets/Scripts/Tests` before renaming or deleting any member, because tests reflect on private names (TST-006). "Not verified in Play mode" goes in commit bodies for anything only read.

**TEST BUDGET rule for every step.** Run the new or changed scenarios, the existing scenarios of the touched subsystem (by name filter, seeds 1-3) in a fresh Play session, and the static suites that cover the touched code. A step marked *broad* also runs `smoke/*` seeds 1-3. The full `rules/*` + `smoke/*` sweep and the soak run only in the two regression steps.

### 1. File the batch 12 findings and refresh stale entries (`b12-file-findings`)

Docs only; no C# change. Before filing each item, re-verify it at HEAD (grep the members) and check the rule page. Use the next IDs from the KNOWN_ISSUES table (expected: CMB-173, CMB-174, CMB-175, SPL-200, CRE-073, CHR-086), and bump Next ID and the counts. Follow each prefix file's entry format, `- **ID** · **Sev** · Player-visible — **Title.** text *Evidence:* ... *Fix:* ...`.

New entries:
- **CMB-173 (Med).**
  - The PC Full Attack button path (`GameManager.CombatActions.cs` `PerformFullAttackWithRetargetingAndFiveFootStep`, around :2598-2812) never provokes for ranged attacks and never checks or spends ammunition.
  - It never drops thrown weapons, and it has no Cleave and no Improved Grab.
  - Its count (`GetPlannedFullAttackCount`, CharacterController.cs:6129) omits the weapon Haste attack.
  - Fix: batch 12 steps `b12-post-attack-raw` and `b12-attack-sequence-executor`.
- **CMB-174 (Med).**
  - Javelins and darts (Ranged category, `IsThrown`) are never dropped on either side and spend no ammunition, so they can be thrown without limit (evidence above; `RequiresAmmoType` None in CombatFlowService around :1162).
  - RAW: a thrown weapon leaves the hand (PHB p.113, p.158 thrown weapons).
- **CMB-175 (Med; filed as CMB-108, CMB-175 retired, see "Filed" below).** No partial charge. Every charge gate requires `HasFullRoundAction` (SupportActions.cs :984, :1086, :1518, :1929), so slowed, disabled, staggered and surprise-round creatures cannot charge on either side. RAW: PHB p.154 (decision D2).
- **SPL-200 (Low).** `ApplyMelfsAcidArrowTurnStartDamage` (GameManager.SpellCasting.cs around :4271-4318) calls `ApplyIncomingDamage`, `CheckConcentrationOnDamage` and the death and summon cleanup by hand instead of `GameManager.DealDamage`. This is a leftover of SPL-004.
- **CRE-073 (Med).** Breath weapon and ranged-special recharge is a fixed `RechargeRounds` per entry (NPCDatabase.cs:299, the entries in NPCDatabase_A/B/C/D/G/H/Dragons). The MM gives per-creature forms (D14). Verify each against its MM page.
- **CHR-086 (Low).** `CharacterStats.RollSkillCheck` (around :7145) omits `GetFamiliarSkillModifier`, which `GetSkillBonus` adds (:7087 vs :7177-7187). The Skills dictionary lookup is case-sensitive.

Refresh these entries:
- **CHR-069:** the PC rage tick sits after the PC skip gate (GameManager.cs :3893 vs :3938), so a paralyzed PC's rage never ticks either.
- **CORE-003:** the NPC turn-start combat-end check (NPCTurns.cs :54) runs before Melf's and regeneration (now in `BeginNPCTurnForAI`, GameManager.cs :11045-11047).
- **CMB-090:** the turned-undead fear break now runs on the full attack, NPC, dual-wield, off-hand, maneuver and charge paths. It is missing from AoOs and NPC maneuvers. Maneuvers send only the fascination break (CharacterController.cs around :12728). The charm break matches only the charmer (GameManager.cs :3087).
- **CMB-091:** the flurry concentration bullet is stale (per click now). Rapid Shot is inert only on the Attack-button path (path B). The GM :8555 evidence is stale.
- **GRID-019:** the grappling half was fixed by CMB-122 (`GetCurrentMoveRangeSquares` is 0 while grappling; `rules/grappler-feared-stays`).
- **CMB-098:** the AI already chooses grapple or hold, but never skip. Not opposing an escape is RAW (PHB p.156, D10); the defect is that the AI side has no such choice. The escape square is random (GrappleSystem.cs :500).
- **CHR-070:** the conversion is read from `CastSource` (GameManager.cs :9748-9757, CastCore.cs :836). Only the UI builder sets it; `AiCastDecider`/`SpellCastRequest.ForAI` never do.
- **AI-005:** add that a failed AI save costs the AI nothing, because it only filters targets (PHB p.274 says the attempted attack is lost), and that `AISpellcastingStrategist.SelectBestEnemyTarget` (:408) has no ward check.
- **AI-032:** evidence anchors (CharacterController.cs :4674, :4688, :4701).
- **CRE-040:** evidence anchors (NPCTurns.cs :1358-1437, :1519; NPCDatabase_B.cs :246).
- **CHR-067:** stale line numbers.

In `docs/systems/PC_NPC_PARITY.md` section 5, add a one-line pointer under steps 1-8, 11 and 12 saying that batch 12 implements them (`docs/designs/pc_npc_parity_batch12.md`).

Commit subject: `File parity findings found mapping batch 12`, with the new IDs listed in the body.

TEST BUDGET: none (docs only). Check the counts and Next IDs add up, and that `git diff --stat` shows only docs/.

**Filed (2026-10-28).** CMB-173, CMB-174, SPL-200, CRE-073 and CHR-086, and the refreshes, with these changes found while re-verifying at HEAD:
- The partial charge was already filed as CMB-108. CMB-175 was not filed and is retired (the CMB Next ID is CMB-176), so wherever this plan or its queue says CMB-175 it means CMB-108; CMB-108 was raised to Med and refreshed (zombies, MM p.266), and step 21 closes CMB-108. Today a slowed creature still charges at double speed, because Slow sets no single-action limit (CMB-095), and the game has no surprise round.
- `RollSkillCheck`'s missing familiar bonus was already in CHR-014. It moved to CHR-086 with the case-sensitive skill lookup, and CHR-014 now holds only synergies, Craft and Profession. A step 4 that fixes only the familiar term narrows CHR-086 to the lookup instead of deleting it.
- CRE-073 also covers the metallic dragons' secondary breath: a daily count instead of sharing the primary breath's 1d4-round recharge (MM p.69; latent, AI-008).
- The dragon recharge is on MM p.69 (D14 said p.68), and the digester's acid spray (MM p.59) joins the D14 list.

### 2. Make the acid spray's numbers RAW and give the PC charge flanking (`b12-acid-spray-numbers-charge-flanking`)

IDs: CRE-040 (numbers, save, frequency), CMB-018 (the flanking one-liner). Behaviour change.

1. **The breath weapon data.** `BreathWeaponDefinition` (Character/Creatures/NPCDatabase.cs :290) gains:
   - `int DamageBonus`;
   - `string RechargeDice` (for example "1d4"; null means the fixed `RechargeRounds`);
   - `bool OncePerRound`;
   - `SaveType` set to Fortitude or Reflex, replacing or keeping `IsReflexSave` (anchor on code).

   Add the new fields to `NPCDefinition.Clone` and to every template's `CopyDefinitionFields` (CRE-023; grep `CopyDefinitionFields`).
2. **The bombardier beetle** (NPCDatabase_B.cs, the `giant_bombardier_beetle` entry around :223-255) gets a 10-ft cone, 1d4+2 acid, Fortitude DC 13 negates and `OncePerRound`. Write the DC as 10 + half HD + Con modifier where the data allows, else 13 with a comment. Fix the special-ability string at :246.
3. **The spray routine** (`TryNPCUseBombardierAcidSpray`, NPCTurns.cs around :1358-1437):
   - Read the definition.
   - Roll the damage once through the shared area roll (`AreaDamageRoll` or the existing one-roll code), with dice labelled "Acid Spray", not "Fireball".
   - Resolve each save with `SavingThrowResolver.ResolveFortitudeSave(stats, dc, "Acid Spray", context)`, using a creature-ability `SaveContext` (grep `SaveContext.For` for the right factory).
   - A success negates. Keep `DealDamage(..., DamagePackets.CreatureAttack(..., Acid))`.
   - Replace the 1d4 cooldown with a per-round flag on `CharacterController` (`_acidSprayUsedThisRound`). Clear it in `TickBombardierAcidSprayCooldown`, which runs at the beetle's turn start on both paths today, and in `ResetForNewCreature`.
   - **The trigger stays where it is** (inside `NPCPerformAttack`, NPCTurns.cs :1519) until step `b12-cleave-and-attacked-triggers`.
4. **CMB-018.** In `SupportActions.ExecuteCharge` (around :1499-1781), compute `bool isFlanking = CombatUtils.IsAttackerFlanking(...)` after the move, exactly as `NPCExecuteCharge` does (around :1975-1977). Pass it at the pounce `FullAttack` (~:1606), the rake (~:1611) and the single `Attack` (~:1701).
5. **Docs.** Narrow CRE-040 to the trigger (applied in a later step). Delete CMB-018's flanking part and keep its charge-core part open.

Tests:
- New scenario `rules/bombardier-acid-spray`: a Scripted PC attacks an AI beetle; assert the 10-ft cone, Fortitude DC 13, 1d4+2 acid, a save negates, and at most one spray per round. Use the RollFilter seam to force a failed and a passed save.
- New scenario `rules/charge-flanking-ui`: a Ui PC charges a target an ally threatens from the opposite side; assert the +2 flanking term in the attack log.
- A static row for the beetle data in an existing creature data suite (grep `NPCDatabase` in Tests).

TEST BUDGET: `rules/bombardier-*`, `rules/charge-flanking-ui`, `rules/area-damage-one-roll`, `rules/haste-natural-pounce-ui`, `rules/pin-release-*` (seeds 1-3), and the static suites AreaDamageOneRollTests, FlankingReachRulesTests and the creature data suite you extended.

### 3. One action-limit rule, and the AI acts while disabled (`b12-action-limits`)

IDs: CMB-095, AI-053 (the HP-gate part only), D5. Behaviour change; *broad*.

1. **`Combat/Core/ActionEconomy.cs`.**
   - Add the field `public bool MoveActionOnly;` and reset it in `Reset()`.
   - While it is set, `HasStandardAction` and `HasFullRoundAction` return false and `CanConvertStandardToMove` is false.
   - Keep `SingleActionOnly` a plain settable field (tests assign it).
2. **`CharacterController.RefreshActionLimits()`** is the one formula:
   - `SingleActionOnly = Disabled || Staggered || Stats.IsSingleActionsOnly || HasActiveSlowEffect`;
   - `MoveActionOnly = HasCondition(Nauseated)`.

   Neither may restore an action already spent: it only sets the flags. `ActionEconomy` already converts the remaining action when `SingleActionOnly` is set mid-turn (ActionEconomy.cs :93-128); check this.
   - Replace the two existing writes (CharacterController.cs :5333 in `OnHPStateChanged`, :12678 in `StartNewTurn`) with calls to it.
   - Also call it where Slow is applied or removed and where Haste clears Slow (CharacterController.cs ~:2154, GameManager_Spells_H.cs ~:276, DispelMagicService.cs ~:527, GameManager.SpellCasting.cs ~:6045), and where Nauseated is applied or removed (`ConditionService` apply and remove).

   Nauseated stays unreachable until step 7, because today's skip gate skips it.
3. **Delete the five separate Slow reads.** `HasFullRoundAction` now refuses for them:
   - CharacterController.cs :825 (flurry), :1005, :1032;
   - GameManager.cs :9093 (`OnFullAttackButtonPressed`);
   - NPCTurns.cs :1552.

   Check that `OnDualWieldButtonPressed` (GameManager.cs ~:9209) and both charge gates read `HasFullRoundAction`.
4. **AI-053, the HP gates.**
   - Replace each `CurrentHP <= 0` gate that means "cannot act" with `!npc.CanTakeTurnActions()` (or `CombatEndRules.IsOutOfFight` where it means "out of the fight"):
     - AIService.cs: 28 hits, including :122, :147, :372, :542, :705, :720, :1175, :1500, :1563, :1619, :1631, :3079 and :3572;
     - NPCTurns.cs :90, :115, :136, :149, :833, :919 and :1761;
     - CharmedBehaviorController.cs :89, :113 and :137;
     - ConfusedBehaviorController.cs :137.
   - Read each gate. A gate that guards a *target* that is dead stays a death check.
   - `ThreatSystem.IsMoverIncapacitated` (ThreatSystem.cs :804) returns `!mover.CanTakeTurnActions()` instead of testing HP <= 0. Audit its callers on both sides (the AIService ones at :172 and :226 included).
   - Check that every AI standard action pays through `CommitStandardAction` (CharacterController.cs ~:5494), which charges a disabled creature its strenuous 1 hp (PHB p.145). The NPC maneuver path already does (NPCTurns.cs :474, :522). Route any AI standard-action path that pays another way (attack, cast, special ability) through it.
5. **Docs.** Delete CMB-095. Narrow AI-053 to its non-HP items (auras and spittle on controller turns, uncalled members). Record D5 as explicit RAW.

Tests:
- New static suite `Tests.Combat.ActionLimitRulesTests`:
  - Slow gives single action;
  - Haste removing Slow mid-turn after one action grants nothing back;
  - Nauseated gives move only;
  - disabled gives single action.
- Extend `GrappleDamageRulesTests.TestAttackSequenceSingleActionOnlyAllowsOneStep` with a slowed attacker.
- New scenarios:
  - `rules/slowed-no-full-attack-ui` and `rules/slowed-no-full-attack-ai`;
  - `rules/disabled-npc-acts`: an NPC at 0 hp takes one standard action, takes 1 damage and becomes dying.

  The ScenarioChecks Econ invariant (ScenarioChecks.cs ~:190, :213) now polices Slow too.

TEST BUDGET: the new suite and scenarios; `rules/single-vs-full-attack*`, `rules/monk-flurry-*`, `rules/movement-aoo-*` (seeds 1-3); AIProfileFrameworkTests, GrappleDamageRulesTests; `smoke/*` seeds 1-3.

### 4. Untrained skill checks for every creature (`b12-untrained-skills`)

IDs: CHR-067 (the fallback part), CHR-086. Behaviour change.

1. Add `CharacterStats.TryGetSkillOrUntrained(string name, out Skill skill)`:
   - Look the name up in `Skills`, then case-insensitively in `ClassSkillDefinitions.AllSkills`.
   - When the dictionary lacks it, build a transient Skill exactly as `InitializeSkills` (~:6784-6807) would: key ability, `TrainedOnly`, 0 ranks, not a class skill. Do not add it to the dictionary.
2. `GetSkillBonus` (~:7074) and `RollSkillCheck` (~:7145) use it.
   - A trained-only skill with 0 ranks keeps today's results: -1 from `RollSkillCheck` and 0 from `GetSkillBonus` (PHB p.62-63).
   - Keep `IsSkillBlockedByMindless` and the deafened Listen failure (~:7162).
3. Both methods compute the total through one private helper that includes `GetFamiliarSkillModifier` (CHR-086).
4. **Docs.** Narrow CHR-067 to the skill-rank data (MM skills, batch 19 per the batch plan) Delete CHR-086 only if the lookup is also case-insensitive end to end (for example `Skills` built with `StringComparer.OrdinalIgnoreCase`); otherwise narrow it to the lookup. Leave CHR-014 (synergies, Craft and Profession).

Real NPC readers that change: Listen and Spot (LastKnownPositionTracker.cs ~:307, :323), Balance (GreaseAreaEffect.cs ~:57, SleetStormAreaEffect.cs ~:128), Escape Artist (GrappleSystem.cs ~:1815, CharacterController.cs ~:11215, GameManager.cs ~:10269, BlackTentaclesAreaEffect.cs ~:255), Sense Motive against a feint (CharacterController.cs ~:14738) and Spellcraft (AIService.cs ~:3504).

Tests: a new static suite `Tests.Character.NpcSkillFallbackTests`:
- an NPC with no Skills dictionary: untrained Listen, Balance and Escape Artist equal the ability modifier plus the other terms;
- Spellcraft (trained only) gives -1 from `RollSkillCheck`;
- lowercase names resolve;
- the familiar term is present in both methods.

TEST BUDGET: NpcSkillFallbackTests, MulticlassSkillRulesTests, RacialTraitRulesTests; `rules/cast-ui-grease`, `rules/grapple-escape-*`, `rules/*feint*`, `rules/invisible*` (seeds 1-3).

### 5. Extract the turn-start routines without changing behaviour (`b12-turn-start-move`)

IDs: preparation for CMB-075, CHR-069, CMB-094. **Pure move:** every pre-existing scenario trace hash must stay unchanged.

1. Create the partial `_Core/GameManager.TurnStart.cs` with two methods.
   - **`BeginCombatantTurn(CharacterController actor)`** holds today's pre-gate work of each side, as explicit branches marked with their issue IDs:
     - for a PC, GameManager.cs :3879-3890 (sphere, Melf's, bombardier cooldown, regeneration, `CloseInventoryIfOpen` for a PC only, the combat-end check, Aid expiry);
     - for an NPC, NPCTurns.cs :50-54 (Aid expiry, sphere, the combat-end check).
   - **`BeginActingTurn(CharacterController actor)`** holds today's post-gate rules work:
     - for a PC, the rage tick (GameManager.cs :3938-3950), `pc.StartNewTurn()`, `PruneTurnUndeadTrackers()` and `CheckTurnUndeadProximityBreakingForCleric(pc)`;
     - for an NPC, the body of `BeginNPCTurnForAI` (GameManager.cs :11039-11052).
   - The PC UI resets (the off-hand and dual-wield fields, `EndAttackSequence`, `EndThrownAttackSequence`, the logs) stay in `StartPCTurn`, in today's order relative to the calls.
2. **Call sites stay where they are:**
   - `StartPCTurn` calls `BeginCombatantTurn(pc)` where its effects ran and `BeginActingTurn(pc)` where the rage and turn-undead code ran;
   - `SingleNPCTurnFromInitiative` calls `BeginCombatantTurn(npc)`;
   - `AIService.ExecuteNPCTurnBody` (:132) calls `_gameManager.BeginActingTurn(npc)` instead of `BeginNPCTurnForAI`.

   Keep `BeginNPCTurnForAI` as a one-line forward only if a test reflects on it (grep). Otherwise delete it.
3. **Turn Undead.** The NPC's `CheckTurnUndeadProximityBreakingForMover` becomes `...ForCleric` directly. This is identical behaviour, because the mover routine only forwards to it.
4. **Docs.** `docs/architecture` (the turn-loop part): name the two methods.

Tests: none new. Every hash unchanged; diff with `ScenarioTraceDiff` if one moves.

As built (2026-10-28):
- Both routines take a second argument, `GameManager.TurnStartPath` (`Pc` or `Npc`), from their caller, so each branch runs on exactly the path that ran it before and no new `IsControllable` read is added.
- `StartPCTurn` now runs `EndAttackSequence` and `EndThrownAttackSequence` just before `BeginActingTurn`, so before the rage tick instead of after it. Two constraints of this step force it: `BeginActingTurn` holds both the rage tick and `StartNewTurn`, and the UI resets stay in `StartPCTurn`. It is inert: neither routine reads what the other writes, and the combat-end safety check in `EndAttackSequence` sees the same sides, because ending a rage lowers max hp and clamps current hp to it, which changes the HP state only if max hp drops to 0 or below. No budget scenario has a raging PC barbarian (rage ticks only on the PC path), so this reorder is not verified in Play mode.
- `ScenarioTrace.CallerFrames` skips the frame `BeginCombatantTurn` when it builds the hashed `by` field of a `phase` event, so the 21 budget jobs that end at the turn-start check (`combat-end-petrified-*`, `tie-*`, `domination-cleared-by-rest`) still report `StartPCTurn` or `SingleNPCTurnFromInitiative`. The skip matches the name as a string (TST-006).

TEST BUDGET: `rules/condition-duration-turn-relative`, `rules/pin-*`, `rules/concentration-lapse-*`, `rules/combat-end-*`, `rules/bard-*` (seeds 1-3), TurnDurationRulesTests, FlatFootedRulesTests, MelfsAcidArrowRulesTests, CombatEndRulesTests; `smoke/*` seeds 1-3. All hashes must equal the previous run's.

### 6. Run every creature's turn-start effects before the skip gate (`b12-turn-start-raw`)

IDs: CMB-075 (ordering), CHR-069, AI-032, CRE-073, SPL-200, CORE-003; D3, D14. Behaviour change; *broad*.

1. **One call site.** `OnTurnStarted` (GameManager.cs ~:3601-3644) calls `BeginCombatantTurn(character)` once, after `TickDomainPowerDurations` and before the `IsPC` fork. Remove the branches. Step 5 added `GameManager.TurnStartPath` (`Pc`/`Npc`) as a temporary second parameter of both routines: delete the enum and its arguments in `StartPCTurn`, `SingleNPCTurnFromInitiative` and `AIService.ExecuteNPCTurnBody`. If it reports the combat ended, return before the fork. The order:
   1. Flaming Sphere tick.
   2. Melf's tick.
   3. Regeneration.
   4. `TickTurnCooldowns()`.
   5. Clear the once-per-round flags (the acid spray flag; the Cleave flag later).
   6. Aid Another expiry.
   7. `actor.StartNewTurn()` (its rules resets, D3).
   8. `EvaluateCombatEnd("TurnStart")`.

   `CloseInventoryIfOpen` stays in `StartPCTurn`. The function stays synchronous with no yields (CORE-012).
2. **Melf's tick (SPL-200).** `ApplyMelfsAcidArrowTurnStartDamage` deals its damage through `DealDamage(target, raw, DamagePackets.Spell(...))` with acid and the same packet flags it uses today, and drops the hand-written concentration, death and summon-cleanup code.
3. **The cooldown ticks (AI-032, CRE-073).**
   - `CharacterController.TickTurnCooldowns()` calls the bombardier tick and the three uncalled ticks (`TickBreathWeaponCooldown` :4701, `TickRangedSpecialAttackCooldown` :4674, `TickTerrainManipulationDuration` :4688).
   - `UseBreathWeapon` (:4710) and the ranged-special cooldown roll `RechargeDice` through `DiceRoller` when it is set, else use `RechargeRounds`.
   - Add a `UsesPerDay` limit to the definition if any audited entry needs it (gorgon), copied in `Clone`/`CopyDefinitionFields`.
   - Audit every breath and ranged-special entry against its MM page (D14) and set the data.
4. **The rage tick (CHR-069)** moves to `OnInitiativeCountReached` (GameManager.cs ~:3684-3702), beside `TickBardicMusicAtCount`. It runs for the creature whose initiative count it is, PC or NPC, through a shared logger (`RageRules.LogTick` or a GameManager helper with the current two log lines). Remove it from `BeginActingTurn`.
5. **The NPC path.**
   - `SingleNPCTurnFromInitiative` no longer runs effects or its own combat-end check before the gate.
   - `BeginActingTurn(npc)` (perception, turn undead) is called from `SingleNPCTurnFromInitiative` after the gate and before `ExecuteNPCTurn`. The AIService.cs :132 call is removed.
   - The AIService.cs :147 HP gate is deleted; the skip gate already covers it.
6. **Logs.** NPC logs now show turn-start effects before "turn begins". Accept and explain the trace differences.
7. **Tests.**
   - Scenario trace: step 5 made `ScenarioTrace.CallerFrames` skip `BeginCombatantTurn` in the hashed `by` field. Once the call sits in `OnTurnStarted`, a combat that ends at the turn-start check reports `OnTurnStarted` (or its caller) instead of `StartPCTurn`/`SingleNPCTurnFromInitiative`. That frame-name move is expected: separate it from real changes in `ScenarioTraceDiff`, and decide whether to keep or drop the skip.
   - Delete the CORE-003 waiver in `KnownIssueWaivers.cs` (~:17) once `rules/turn-start-acid-arrow-last-enemy` passes, and update the ScenarioChecks.cs ~:190 comment that names `BeginNPCTurnForAI`.
   - Delete CMB-075's ordering half, CHR-069, AI-032, CRE-073, SPL-200 and CORE-003 (if the scenario proves the tick kill ends combat at once).

Tests (new scenarios):
- `rules/turn-start-regeneration-skipped-npc`: a paralyzed troll regenerates;
- `rules/turn-start-acid-arrow-last-enemy`: the acid tick drops the last foe and combat ends at that turn start;
- `rules/dragon-breath-recharges`: a dragon breathes, then again after its 1d4 rounds, forced with RollFilter;
- `rules/rage-expires-ui` and `rules/rage-expires-ai`;
- `rules/fight-defensively-ends-skipped`: a creature that fought defensively and is then paralyzed loses the AC bonus at its next turn start.

TEST BUDGET: step 5's set, `rules/dragon-ai-breath-*`, `rules/bombardier-*` and the new scenarios (seeds 1-3); MelfsAcidArrowRulesTests, TurnDurationRulesTests, CombatEndRulesTests, NpcSlotReuseTests; `smoke/*` seeds 1-3.

As built (2026-10-28):
- `GameManager.TurnStartPath` is gone. `BeginCombatantTurn(actor)` runs the D3 order from `OnTurnStarted` and checks `EvaluateCombatEnd("TurnStart")`; `BeginActingTurn(actor)` keeps only the Turn Undead prune and proximity check. Perception stays on the NPC path, called by `SingleNPCTurnFromInitiative` just before `BeginActingTurn` (CMB-094, step 8), so no `IsControllable` read is added. `CloseInventoryIfOpen` stays in `StartPCTurn`, which now runs it after the turn-start check instead of before.
- The once-per-round flag reset is `CharacterController.ClearOncePerRoundFlags` (the renamed `TickBombardierAcidSprayCooldown`); `TickTurnCooldowns` ticks the breath, ranged-special and terrain counters.
- The recharge dice are rolled with `DiceService.RollMultiple` (an NdS roll like `DiceRoller.Roll`, but with a context string, so a scenario can force them; context "<name> breath weapon recharge"), parsed by `DiceExpression`, in `CharacterController.RollRechargeRounds`. `RangedSpecialAttackDefinition.RechargeDice` was added for symmetry; no entry uses it.
- Audit (D14): dragons, chimera, digester 1d4; hell hound 2d4; gorgon 1d4 with `UsesPerDay` 5 (`BreathWeaponDefinition.UsesPerDay`, copied by its `MemberwiseClone`; the per-creature count is cleared by `ConfigureBreathWeapon`, so by slot reuse); behir a fixed 10; ankheg a fixed 3,600 (6 hours). The metallic secondary breath now shares the primary's recharge and no longer spends a daily count (MM p.69). Two MM details found on the way are filed as CRE-074 (the ankheg's acid supply, the digester's stream).
- The scenario trace no longer skips `BeginCombatantTurn`, so a combat that ends at the turn-start check reports it in `by`. Only the 12 `combat-end-petrified-*` and `-tie-*` jobs (an Idle hero, the NPC path, out at its turn start) moved: the check now runs before `SingleNPCTurnFromInitiative` sets the phase, so their traces lose a `PCTurn -> NPCTurn` phase event. The other 96 budget jobs of step 5's set kept their hashes.
- The `AIService` HP gate and the `BeginActingTurn` call in `ExecuteNPCTurnBody` are gone. The CORE-003 waiver is deleted (`KnownIssueWaivers.Global` is empty); no run of the budget hit a phase change after CombatOver.
- New scenarios in `Catalog/TurnStartScenarios.cs`. The Ui rage scenario paralyzes the barbarian for its round-2 turn, so it also shows rage passing on a turn the barbarian cannot use.

### 7. Skip a turn only when no action is possible, and run compulsions for everyone (`b12-skip-gate-compulsions`)

IDs: CMB-075 (the skip gate), CMB-100 (the base); D4. Behaviour change; *broad*.

1. **`ConditionRules.DeniesAllActions(CharacterController c)`** (Combat/StatusEffects/ConditionRules.cs) is true when `!c.CanTakeTurnActions()`, or when the creature has a D4 no-action condition: Dead, Dying, Stable, Unconscious, Asleep, Helpless, Paralyzed, Petrified, Stunned, Dazed, Cowering, Fascinated, HideousLaughter or BlownAway (anchor on the `CombatConditionType` names).
   - `ShouldSkipTurnDueToHPState` (GameManager.cs :3787-3796) returns `DeniesAllActions`.
   - Do not change `CanTakeActions` itself; GrappleSystem.cs ~:2331 keeps using it.
2. **What now reaches the turn:**
   - *Nauseated:* the turn runs with `MoveActionOnly` (step 3).
   - *Pinned:* the PC goes through the existing `RedirectPinnedCharacterToGrappleMenu` (GameManager.cs ~:9214), and the NPC through the AIService grapple branch (~:289). Both are now live; check that they offer only the actions a pinned creature may take.
   - *Panicked and Turned:* the creature flees through `FrightenedBehaviorController`. An NPC uses the existing branches (AIService.cs ~:233 frightened, ~:283 turned). A turned creature that cannot flee cowers: apply Cowering for the round and end the turn.
3. **`TryRunCompulsionTurn(actor)` in `BeginActingTurn`.**
   - It evaluates Confused, Suggested, Frightened, Panicked and Turned once per turn, for every actor, from the existing shared controllers.
   - It stores the result in `actor.CurrentTurnStart.Compulsion`, a new small `TurnStartState` on CharacterController, reset in `ResetForNewCreature`.
   - The AI branches (AIService.cs ~:154-237, :283) read the stored result instead of re-rolling.
   - A **PC** under a fleeing compulsion gets a forced turn on the Confusion pattern (`ExecuteConfusedForcedPCTurn`, GameManager.cs ~:4013-4022: sub-phase Animating, action buttons hidden, the shared controller picks the path, then `EndCurrentTurn`). This is the default answer to owner question Q1. If the owner chooses otherwise, Frightened PCs keep control and get only a log reminder.
   - Charmed stays a target restriction. The AI already filters targets; the PC menu half is batch 13 (CMB-100 menu part).
   - `ScenarioHooks.ScriptedTurn` (AIService.cs ~:211) still runs after the forced kinds.
   - Fascinated stays a skip. Its "source gone" release (AIService.cs ~:183-187) moves into the compulsion pass, so it applies to both sides, before the gate. Move it into `BeginCombatantTurn` just before the combat-end check.
4. **Docs.** Delete CMB-075. Narrow CMB-100 to the PC menu restriction for Charmed and the PC flight-path choice (batch 13). Record D4.

Tests (new scenarios):
- `rules/nauseated-move-only-ui` and `rules/nauseated-move-only-ai`;
- `rules/pinned-escape-ui` and `rules/pinned-escape-ai`;
- `rules/panicked-pc-flees`;
- `rules/turned-undead-flees-ai`;
- `rules/three-skipped-in-a-row`: three paralyzed creatures in consecutive initiative, for CORE-012 re-entrancy.

Re-run `rules/bard-fascinate-*`, `rules/bard-suggestion-ui`, `rules/grappler-feared-stays` and `rules/combat-end-charmed-*`.

TEST BUDGET: those scenarios (seeds 1-3), CharmPersonRulesTests, AIProfileFrameworkTests; `smoke/*` seeds 1-3.

### 8. Every combatant tracks unseen creatures (`b12-perception-all`)

IDs: CMB-094. Behaviour change. Depends on step 4 (NPC Listen and Spot).

1. Add a `LastKnownPositionTracker` (AI/LastKnownPositionTracker.cs) to every combatant when its slot is initialised (the PC and NPC slot initializers; grep `InitializeNPCFromDefinition` and the PC `Init`). Clear it in `ResetForNewCreature` and `ResetCharacterSlotForSpawn`, and check `ResetPCSlotForNewCharacter`. Remove the lazy adds (AIService.cs ~:1190-1192, ~:1838-1840, ~:3781-3783), or keep them as null-safe fallbacks.
2. Move `AIService.ProcessRoundStartPerception` (~:3776) into a shared static `PerceptionRules.ProcessTurnStart(actor, all, log)` (Combat/Core or AI/). AIService forwards to it. `BeginActingTurn` calls it for every actor.
3. **PC targeting.** `GetValidMeleeTargets` and `GetValidRangedTargets` (GameManager.CombatActions.cs ~:233, :254) also accept a creature whose square the PC's tracker pinpoints. The 50% miss and the wrong-square auto-miss are already shared (`TryResolveLastKnownPositionAutoMiss`, CharacterController.cs ~:7297-7330). A believed-square UI (attacking an empty square) is out of scope.
4. **Docs.** Delete CMB-094, or narrow it to the believed-square UI.

Tests (new scenarios): `rules/invisible-pinpoint-ui` (the PC pinpoints by Listen and attacks with a 50% miss) and `rules/invisible-pinpoint-ai`; extend NpcSlotReuseTests (the tracker is cleared on reuse).

TEST BUDGET: `rules/invisible*`, `rules/*mirror*` (seeds 1-3), NpcSlotReuseTests and any LastKnownPosition suite (grep).

### 9. Check combat end after every cast and give every caster one legality list (`b12-cast-core-quirks`)

IDs: CORE-039, SPL-161, SPL-160 (a and d), CMB-101 (the cast-core half). Behaviour change.

1. Remove these bits from every set in `LegacyCastQuirkSets` (Spell/Casting/Core/LegacyCastQuirk.cs ~:247-353): `EvaluateCombatEndOnlyOnTargetDeath`, `NpcCombatEndCheckedByCaller`, `AreaCombatEndOnlyInSomeHandlers`, `EffectResolveSkipsCombatEndCheck`, `AreaLogPrefixOnlyInSomeHandlers`, `NpcResultLogBeforeTargetDeath` and `NpcOwnLegalityChecks`. Delete the bit definitions and every read.
2. **Combat end.**
   - `CastStageCombatEnd` (GameManager.CastCore.cs ~:1650), `FinishAreaCast` (~:1811-1838), `FinishEffectCast` (~:1914) and `FinishGreaseCast` (~:1937) always call `EvaluateCombatEnd` with their context.
   - Delete the four NPC wrapper checks (GameManager.cs ~:11135-11180) and `RequireLegacyQuirk` uses for the removed bits.
   - `HandleCombatVictoryDetected` must stay idempotent; check it.
3. **The area log prefix.** Delete `AreaHandlerEntry.OmitsLogPrefix` (GameManager.SpellHandlerRegistry.cs ~:100); every area handler's log gets the prefix (Hypnotism, Sleep, Deep Slumber, Color Spray, Fear).
4. **The result-log order.** Remove the NPC-only branch at CastCore.cs ~:1583, so both sides log the result in the PC order.
5. **Legality for every caster (SPL-160 a).** Move the checks of `CheckNpcCasterLegality` (CastCore.cs ~:323-330, with ~:653-717 and ~:808: an action for the casting time, an available slot) into `CastStageLegality` for every request, with logged refusals. The PC spell menu already filters, so a PC cast should only refuse in edge cases (F12 and scroll paths). Check the F12 bypass requests keep their bypass.
6. **Grapple casting (CMB-101, cast half).** `GrappleSpellcastRefusal` (GrappleSystem.cs ~:352) runs in Legality for every resolve kind, not only HoldCharge (`CheckHoldChargeLegality`, CastCore.cs ~:301-312). The AI's cast option inside the grapple turn comes in step 19.
7. **`HeldChargeDeliveryIsFree` stays** (SPL-156, step 18). Keep the bit plumbing until then.

Tests:
- Update `CastCoreStageOrderTests` (~:125, ~:227-239) to the new bit sets.
- In CastPipelineScenarios.cs (~:615, `rules/cast-ui-sleep-quickened`), turn `ExpectKnown("SPL-161")` into a plain expectation.
- Update the static-suites.json note (~:1494).
- New scenarios: `rules/cast-combat-end-retaliation-ui` (a PC cast whose fire-shield retaliation drops the last foe or the caster ends combat at once) and `rules/grappled-caster-legality-ui-area` (a grappled PC cannot cast a somatic area spell).

TEST BUDGET: `rules/cast-*`, `rules/combat-end-*`, `rules/npc-*cast*`, `rules/grapple-*` (seeds 1-3), CastCoreStageOrderTests, SpellHandlerRegistryTests, SpellRoutingGoldenTests.

As built (2026-10-28):
- The combat-end check is one helper, `RunCastCombatEndCheck` (once per run, `CastContext.CombatEndChecked`), called by `CastStageCombatEnd`, `FinishAreaCast` (the entry's context, else its name), `FinishEffectCast` and `FinishGreaseCast`, and by `FinishCast` for any other outcome that counts as cast (a spell lost to a distraction, its failure chance or the AoOs, countered or blocked), which the NPC wrappers used to cover. The four wrappers are one-line forwards. `HandleCombatVictoryDetected` and `HandleCombatDefeatDetected` return at once when the combat is over (their only caller, `EvaluateCombatEnd`, already did).
- The caster tests are `CheckCastCasterLegality` (first in `CheckLegality` and the Legality stage): `CastActionRules.StandardActionRefusal` (a standard action for a standard, full-round or item casting; none for a quickened, free or swift one, a delivery or the F12 panel), a living target, then for a slot, spontaneous or conversion source spellcasting and `HasCastResource` (`CanSpontaneousCast` for a conversion, else `CanCastWithMetamagic`). Scrolls, wands, deliveries and the F12 panel skip the slot test; the F12 bypass is otherwise unchanged. The Cost stage refuses a failed standard-action commit for every source but the F12 panel. `SpellcastingComponent.CanCast` reads a prepared caster's slots before the legacy `SlotsRemaining` length, as the spend does (a test-added slot above the caster's table was refused before).
- The grapple rule is `CheckCastGrappleLegality`, for every source but a delivery and a wand (PHB p.156, Activate a Magic Item). The PC targeting's copy (`CanBeginSpellcastWhileGrappledOrPinned`) is deleted; after review the targeting calls `CheckCastGrappleLegality` itself before its prompts for early feedback (the Legality stage stays the binding test), so a PC Wish meets the rule there too (SPL-151). A grappled scroll reader is filed as CMB-184. Review fixes: the rule counts the cast's own casting time (`GrappleSpellcastRefusal` takes the metamagic and source: a spontaneous metamagic cast is a full-round action, PHB p.88; `rules/grapple-spontaneous-still-cast`); a pinned creature may not use a wand (delegated RAW reading 2026-10-28, RULES_COVERAGE 4.1); SPL-203 filed (an Imbue with Spell Ability recipient's cast is refused by the caster tests).
- `rules/cast-combat-end-retaliation-ui` uses the AoO the cast provokes: Fire Shield has no retribution against a touch spell in the game (filed as SPL-202). `rules/cast-full-round-after-move` now expects the Scripted caster's refusal line too.
- Broad (every cast): `smoke/*` seeds 1-3 ran as well.

### 10. One saving-throw roll (`b12-one-save-roll`)

IDs: SPL-018, SPL-090, CORE-030 (narrowed), AI-006; D13. Behaviour change; *broad*.

1. **`SaveRollRules`.** Add `Character/Stats/SaveRollRules.cs` with:

   ```
   public static SaveRoll Roll(CharacterStats stats, SaveType type, int dc, SaveContext ctx, string diceContext, int extraModifier = 0)
   ```

   It returns a struct with Roll, Modifier, Total, Succeeded, NaturalOne and NaturalTwenty. The roll:
   1. d20 through `DiceService.D20` (the RollFilter seam);
   2. the Luck reroll (`stats.ApplyLuckReroll` plus `GameManager.LogLuckRerollIfTriggered`);
   3. `SaveRules.Modifier(stats, type, ctx)` + `extraModifier`;
   4. `BardicMusicRules.ApplyCountersong(stats.OwnerCharacter, total, ctx)`;
   5. Succeeded = natural 20, or (not natural 1 and total >= dc).

   Use whichever `SaveType` enum SaveRules accepts; it has overloads for both.
2. **The resolvers.** `SavingThrowResolver.ResolveSave` (Services/SavingThrowResolver.cs ~:105-137) and both `SpellSaveResolver.RollSave` overloads (Spell/Casting/SpellSaveResolver.cs ~:214-250) build their existing result types from `SaveRollRules.Roll`. Keep their signatures. Log text: add "natural 1" or "natural 20" when they decide.
3. **Route the hand rolls through it**, each with a `SaveContext` (and the caster where a spell is the source):
   - Grease (GameManager_Grease.cs ~:536-542);
   - `ConditionService.ResolveTurnStartEscapeChecks` (~:495-525) and the ConditionService re-saves;
   - the breath weapon save (`NPCExecuteBreathWeaponForAI`) and the bombardier spray;
   - Phantasmal Killer;
   - `ApplyStatusEffectFromSpecialAttack`, Engulf, swarm distraction, disease, coup de grace, Stunning Fist, Sun Blade and similar items, and `CreatureTrapSystem` (the SPL-018 list; grep `D20(` and `ResolveSave(`).
4. **Evasion (SPL-090).** The Javelin of Lightning (JavelinOfLightningBehavior.cs ~:50-61) and the breath weapon resolve Reflex-half damage through `SpellSaveResolver.ApplyEvasion`. Narrow AI-040's text to the action cost.
5. **AI-006.** `AIService.RollRangedTouchAttack` (~:4329-4342) uses the shared spell ranged-touch terms (`SpellCaster.SpellTouchAttack`, ~:1023: natural 1 and 20, size, range, conditions, concealment). Damage stays on `DealDamage`.
6. **CORE-030 (narrowed).** Move the bull rush misdirection d100 (GameManager.CombatActions.cs ~:1020; keep its test override) and the grapple escape-square pick to `DiceService`/`DiceRoller`. Rewrite the CORE-030 entry to list the `Random.Range`/`Random.value` sites that remain, with counts (about 67 and 13 today).
7. **Docs.** Delete SPL-018, SPL-090 and AI-006. Update PARITY step 12 and RULES_COVERAGE (Evasion row, saves).

Tests:
- New static suite `Tests.Character.SaveRollRulesTests`:
  - a natural 1 fails at +30;
  - a natural 20 succeeds at -10;
  - an armed Luck reroll replaces the roll;
  - countersong raises the total;
  - Evasion on the javelin takes no damage on a success.

  It uses the RollFilter seam or the DiceService test hook (grep).
- New scenarios: `rules/save-natural-one`, `rules/save-natural-twenty`, `rules/ai-ranged-touch-natural-one`.

Many trace hashes move (the dice order and the natural rules); explain them.

TEST BUDGET: SaveBonusRulesTests, SavingThrowResolverTests, SpellSaveAndResistanceRulesTests, AreaDamageOneRollTests, the new suite; `rules/save-*`, `rules/spell-save-*`, `rules/cast-ui-grease`, `rules/dragon-ai-breath-*` (seeds 1-3); `smoke/*` seeds 1-3.

### 11. Midpoint full regression (`regression-midpoint`)

Kind regression. Run, through the Unity MCP:
1. the static runner in edit mode, then in Play mode, against `tools/tests/static-suites.json`;
2. `ScenarioHarness.QueueFresh("rules/*,smoke/*", "1-5", "")`;
3. the soak `QueueFresh("soak/*", "1-50", "perSession=10")`.

Record the run in `docs/TESTING.md` 3.2, comparing the soak with batch 11's final run (56aab3d).

The expected, explained shifts are:
- breath weapons recharge;
- disabled NPCs act;
- natural 1 and 20 on saves;
- the turn-start log order;
- nauseated, pinned and panicked creatures act.

Change no game code. File any regression as a new issue.

### 12. One "may this creature attack that one" gate, with ward saves rolled once (`b12-attack-legality`)

IDs: CMB-092, CMB-104, CMB-093, AI-005; D6. Behaviour change; *broad*.

1. **`Combat/Core/Legality.cs`.**

   ```
   public readonly struct Legality { public readonly bool Ok; public readonly string Reason; public readonly LegalityRisk Risk; ... }
   ```

   With `LegalityRisk { None, WardSaveUnrolled }`. This is the shape of docs/systems/tactics/README.md 1.4 plus `Risk`; batch 13 reuses it.
2. **`Combat/Core/AttackLegality.cs`.** `static Legality CanDirectlyAttack(CharacterController a, CharacterController t, DirectAttackKind kind, AttackIntent intent = null)`, with `DirectAttackKind { Weapon, Natural, Touch, Maneuver, AoO, Charge, Spell }`. It is pure: no dice and no log. It checks:
   - `ResilientSphereAreaEffect.DoesSphereBlockInteraction` (~:189), for every kind;
   - the hold bars (`IsBarredFromOrdinaryAttackOn` ~:9821 and `IsBarredFromSpecialAttackOn` ~:9854);
   - reach and range by kind (`CanMeleeAttackDistance` ~:9209, `IsTargetInCurrentWeaponRange` ~:9328, `IsTargetInThrownWeaponRange` ~:9365; for a Maneuver, the melee reach of the weapon or unarmed attack used, plus `CanTrip`/`CanBullRush`; PHB p.155-158);
   - `WardSaveLedger.Predict`: a known failed save gives not-ok, an unrolled ward gives ok with `WardSaveUnrolled`.

   `AttackIntent` arrives in step 13; until then, pass null and derive melee or ranged from the weapon.
3. **`Combat/Core/WardSaveLedger.cs`.**
   - It is keyed by (attacker, ward effect instance). Use the StatusEffect instance or the casting id for Sanctuary and Hide from Undead; grep `SanctuaryActive`/`HideFromUndeadActive` and the StatusEffectManager entry.
   - `Commit(attacker, target)` rolls the Will save once with `SaveContext.ForSpellId`, logs it once, stores the result, and returns pass or fail. A new casting is a new key.
   - Clear an attacker's entries in `ResetForNewCreature`/`ResetCharacterSlotForSpawn`. Clear a casting's entries when the effect ends.
   - Hide from Undead: mindless undead always fail without a roll. A successful save does not end the spell, so delete AIService.cs ~:2102-2104. The spell ends only through the warded creature's own triggers (check that the existing break-on-attack code covers turn, command and touch).
4. **Commit points**, which roll through `Commit`:
   - the PC attack click (`CombatFlowService.PerformPlayerAttack` ~:159-179);
   - each NPC attack step (NPCTurns.cs F loop ~:845, `NPCPerformAttack` ~:1463-1480);
   - the off-hand click (GameManager.CombatActions.cs ~:1497-1516);
   - the maneuver entries (`TryNPCSpecialAttackIfBeneficial` NPCTurns.cs ~:338; the PC special-attack wrapper GameManager.CombatActions.cs ~:1559-1892);
   - both charges, at the attack after the move;
   - `ThreatSystem.ExecuteAoO` (~:676-688; keep its early-outs);
   - the cast core, a new step after `CastStageCost` and before the slot spend, for a spell that targets a warded creature.

   A failed commit spends the attempted step or action (a spell keeps its slot, D6), logs, and stops that attack.
5. **Prediction points**, which never roll:
   - the PC highlight lists (`GetValidMeleeTargets`, `GetValidRangedTargets`, `GetValidTargetsForCurrentWeapon`, GameManager.CombatActions.cs ~:233-306);
   - `AIService.SelectBestTarget` (~:1845-1867), whose `ShouldExcludeTargetBecauseOf...` helpers (~:2037-2114) stop rolling;
   - `SelectAdaptiveFullAttackTarget` (~:3911-3920);
   - `AISpellcastingStrategist.SelectBestEnemyTarget` (~:408).

   The AI excludes a target whose ledger entry says the save failed, and scores a target with `WardSaveUnrolled` lower (the save may fail at commit). It no longer rolls or logs while choosing.
6. **The cast core's Legality stage** calls `CanDirectlyAttack(kind: Spell)` for a non-self, non-area single-target spell: the sphere (CMB-092, spell half). The PC menu's own sphere test (GameManager.SpellCasting.cs ~:763-774) can then read it.
7. **Docs.** Delete CMB-092, CMB-104, CMB-093 and AI-005. Record D6.

Tests:
- New static suite `Tests.Combat.AttackLegalityTests`, a pure table: sphere both ways; the hold bar; reach for trip, disarm, sunder and grapple from 10 ft without reach; the ledger predict/commit (one roll per casting; a new casting rolls again; a predict never rolls).
- New scenarios:
  - `rules/sphere-blocks-npc-attack` and `rules/sphere-blocks-pc-fullattack-ui`;
  - `rules/sanctuary-ai-fails-loses-attack` and `rules/sanctuary-pc-saves-once-ui`;
  - `rules/hide-from-undead-persists`;
  - `rules/ai-trip-needs-reach`.

TEST BUDGET: those, ReachWeaponRulesTests, FlankingReachRulesTests, `rules/maneuver-*`, `rules/improved-grab-*`, `rules/movement-aoo-*` (seeds 1-3); `smoke/*` seeds 1-3.

### 13. Add the attack intent and put every post-attack block behind one hook (`b12-attack-hook-move`)

IDs: preparation for CMB-090, CMB-096, CMB-097, CMB-017, CMB-019. **Pure move:** every pre-existing trace hash unchanged.

1. **`Combat/Core/AttackIntent.cs`** (fields as in Architecture).
   - `AttackIntent.ForUi(GameManager gm, CharacterController attacker)` reads `_currentAttackType`/`_pendingAttackMode` once (through an accessor).
   - `AttackIntent.ForAI(...)` must reproduce what each NPC path reads today. NPC range math reads the PC global through `CalculateRangeInfo` (CombatFlowService.cs ~:226-252, called from NPCTurns.cs ~:952, ~:1510), so `ForAI` copies the global into the intent and sets a quirk bit `NpcIntentReadsUiThrownGlobal`.
   - Thread the intent through `CalculateRangeInfo`, `IsUsingThrownAttackMode` (GameManager.cs ~:11732), `IsAttackModeRanged`, GameManager.CombatActions.cs ~:297-306 and `ResolveThrownWeaponAfterAttack` (GameManager.cs ~:4968). The UI keeps writing `_currentAttackType` (GameManager.cs ~:4087, 7748, 8250, 8824, 9120, 9158, 9199); only `ForUi` reads it.
2. **`Combat/Core/AttackResolution.cs`.**
   - `AttackResolution { Attacker, Target, Results, Intent, Origin }`, with `AttackOrigin` { PcSingle (A), PcSequence (B), PcFullAttackButton (C), PcDualWield (D), PcOffHand (E), NpcMelee (F), NpcRangedFull (G), NpcRangedSingle (H), AoO (I), Charge (J) }.
   - `[Flags] enum PostAttackQuirk` and `PostAttackQuirkSets.For(origin)`, naming today's skips per path. From the attack map, as a starting list to verify in code:
     - B: no grab;
     - C: no grab, ranged AoO, ammo or thrown drop;
     - D: no trip, thrown or grab; Concentration on the summed damage;
     - E: no trip, reactions or grab;
     - G: no ammo or thrown drop; grab after all attacks; Concentration summed;
     - H: no ammo;
     - I: no grab, Cleave or fear break; Concentration, summon and combat end are left to the callers;
     - J: grab after all attacks;
     - every origin but A: no Cleave.
3. **`IAttackDecider`** (`Combat/Core/IAttackDecider.cs`), with `UiAttackDecider` and `AiAttackDecider` and `AttackDeciders.For(actor, ActorSource? overrideSource = null)`.
   - Methods with callbacks: `ChooseImprovedGrab`, `ChooseCleaveTarget`, `ChooseHasteNaturalAttack` and `ChooseAttackedReaction` (unused until step 15).
   - The UI decider wraps the existing prompts (the Improved Grab prompt, `PromptHasteNaturalAttackChoice`). The AI decider wraps `ChooseImprovedGrabForAI` and `AIProfile.ShouldHoldWithImprovedGrab` (~:563), and the Haste choice (~:742).
   - This replaces the `IsControllable` branches at NPCTurns.cs ~:637, :694 and SupportActions.cs ~:1676, :1741.
4. **The hook.** Add the partial `_Core/GameManager.AttackResolved.cs`, `IEnumerator OnAttackResolved(AttackResolution res, PostAttackOutcome outcome)`, plus a callback form for the PC paths that continue by callback.
   - It runs the post-attack work in the Architecture order, skipping what the origin's quirk bits say.
   - Replace the post-attack blocks of paths A-J with calls to it: CombatFlowService.cs ~:869-1013, ~:599-686, ~:1100-1121; GameManager.CombatActions.cs ~:2742-2766, ~:1564-1584; NPCTurns.cs ~:989-1019, ~:1583-1607, ~:1643-1667; ThreatSystem.cs ~:742-771; SupportActions.cs ~:1622-1755 and the NPC mirror.
   - Cleave stays path A's code, called from the hook for origin A only.
   - Summon cleanup must not mutate `NPCs` while a caller iterates it; defer it to the hook's end, and make loops iterate snapshots (AI-015).
   - Do not publish `AttackResolvedEvent` yet.
5. **TST-006.** Grep the reflected names first: `PerformNPCMeleeAttackSequence` (GrappleDamageRulesTests ~:2017), `TryResolveFreeTripOnHit` (~:5652), and the context strings that RulesScenarios ~:5044 expects ("PerformNPCMeleeAttackSequence", "CheckCombatVictory"). Keep the names, or keep the context strings identical.

Tests: a new static suite `Tests.Combat.PostAttackQuirkSetsTests`, which pins the bits per origin (the CastCoreStageOrderTests pattern).

TEST BUDGET: `rules/improved-grab-*`, `rules/freetrip-*`, `rules/bard-fascinate-full-attack-*`, `rules/combat-end-*`, `rules/haste-natural-pounce-ui`, `rules/single-vs-full-attack*`, `rules/monk-flurry-*` (seeds 1-3), GrappleDamageRulesTests, the new suite; `smoke/*` seeds 1-3. Every hash must be unchanged.

### 14. Every attack step gets the same after-attack effects (`b12-post-attack-raw`)

IDs: CMB-097, CMB-169, CMB-170, CMB-090, CMB-019, CMB-096 (the drop), CMB-173 (its hook part), CMB-174; D8, D15 (charm). Behaviour change; *broad*.

Remove the `PostAttackQuirk` bits for grab, breaks, ammunition, thrown drop, Concentration and reactions. Leave only the Cleave bits, removed in step 15.

1. **Improved Grab (D8)** per qualifying hit on every origin:
   - the PC sequence and the Full Attack button (CMB-097);
   - after an AoO hit (CMB-170);
   - inside a pounce right after the hit (CMB-169): the charge pounce runs `FullAttack(startAttackIndex: i, maxAttacks: 1)` per step, as path C does, so the hook runs per step.
2. **Breaks (CMB-090, D15).**
   - The turned-undead fear break moves into `NotifyWeaponAttackAgainst` (GameManager.cs ~:3218-3231), so AoOs get it.
   - `BreakCharmOnHostileAction` (~:3087-3093) also matches the charmer's allies (team or side of the charmer, as command undead does at ~:3122).
   - Maneuvers send the charm and command breaks as well as the fascination break (CharacterController.cs ~:12728-12734).
3. **Concentration** once per hit on every origin (no summed damage).
4. **Ammunition and thrown weapons (CMB-019, CMB-173, CMB-174, CMB-096 drop).**
   - Check and spend ammunition per ranged step on origins C, G and H (`CheckAmmoAvailable`/`ConsumeAmmoForAttack`, CombatFlowService.cs ~:1157, ~:1186).
   - The ranged AoO for origin C (one helper, step 17 merges the copies).
   - A thrown weapon is dropped by intent (Mode Thrown) for every weapon with `IsThrown` and a range increment, javelins and darts included. Replace `IsThrowableMeleeWeapon` with an `IsThrowableWeapon` that keeps the melee-throwable callers' semantics where they need melee (check :5157, :5218).
   - `ResolveThrownWeaponAfterAttack` writes the thrower's own hand slot through its inventory, not `_equippedWeapon`. The PC's UI field is refreshed from the inventory by the PC wrapper.
5. **Combat end** for both the target and the attacker after reactions (a Fire Shield or other retaliation that drops the attacker).
6. **Publish `AttackResolvedEvent`** at the end of the hook (tactics README 1.4 rule 6; no subscribers yet).
7. **Docs.** Delete CMB-097, CMB-169, CMB-170, CMB-090, CMB-019 and CMB-174; narrow CMB-096 and CMB-173 to what step 17 closes. Record D8.

Tests (new scenarios):
- `rules/improved-grab-pc-full-attack-ui`;
- `rules/improved-grab-aoo`;
- `rules/pounce-grab-mid-sequence`;
- `rules/charm-broken-by-ally-attack`;
- `rules/npc-archer-ammo` (an NPC archer with 3 arrows stops shooting after 3);
- `rules/npc-throws-javelin` (the javelin lands on the ground and the next is drawn);
- `rules/fire-shield-kills-attacker-ends-combat`.

Update PostAttackQuirkSetsTests.

TEST BUDGET: step 13's set, CharmPersonRulesTests, GrappleDamageRulesTests, RapidShotTests (seeds 1-3 for the scenarios); `smoke/*` seeds 1-3.

### 15. Cleave everywhere, and the acid spray as a reaction (`b12-cleave-and-attacked-triggers`)

IDs: CMB-017, CRE-040 (the trigger); D7, D1. Behaviour change.

1. **Cleave and Great Cleave in the hook (D7).**
   - The trigger: the step's damage dropped the target below 0 hp, killed it, or made it unconscious.
   - The limit: a per-round flag `CleaveUsedThisRound` on CharacterController, cleared in `BeginCombatantTurn` and on slot reuse; Great Cleave ignores it.
   - The target: candidates within the attacker's reach for that weapon (`IsTargetInCurrentWeaponRange`, multi-square creatures included) that pass `CanDirectlyAttack`, chosen by `IAttackDecider.ChooseCleaveTarget`. The UI prompt appears only when there is more than one candidate. The AI picks by its target scoring.
   - The extra attack: the same weapon and attack bonus as the dropping attack, with no 5-foot step. It runs as one more step through the hook (so a cleave can grab, trip, and cleave again with Great Cleave).
   - Every melee origin (A-J, AoOs and charges included) can trigger it.
   - Delete the path A Cleave code (CombatFlowService.cs ~:924-987) and `FindAdjacentEnemy` (~:345-372) if nothing else uses it.
2. **The acid spray reaction (D1).**
   - Add a target-trigger slot to the hook (step 5 of the Architecture order) and a target-hostile notification at the end of the cast core's Handlers stage for each hostile creature a spell targeted or damaged. Add the same notification in `DealDamage` for damage from a foe's effect.
   - A beetle with `OncePerRound` unused asks `IAttackDecider.ChooseAttackedReaction`, then sprays at a cone that includes the triggering creature (`AoESystem.BestDirectedAimAt`).
   - Remove the spray call from `NPCPerformAttack` (NPCTurns.cs ~:1519).
   - The spray ends the beetle's own wards (`BreakProtectiveWardsOnAttack`).
   - Guard against re-entrancy: no spray inside a spray, and none while the beetle's own attack is resolving against itself.
3. **Docs.** Delete CMB-017 and CRE-040. Record D7 and D1. Remove the Cleave bits from PostAttackQuirkSets, then delete the quirk type if no bit is left.

Tests (new scenarios):
- `rules/cleave-full-attack-ai`;
- `rules/cleave-reach-ui`;
- `rules/cleave-once-per-round`;
- `rules/cleave-after-aoo`;
- `rules/cleave-disabled-target-no-trigger`;
- `rules/great-cleave-chain`;
- `rules/bombardier-spray-when-attacked` (a PC attacks the beetle and the spray comes after the attack, once in the round, even on the PC's turn).

TEST BUDGET: those, `rules/bombardier-*`, `rules/improved-grab-*`, `rules/combat-end-*` (seeds 1-3), PostAttackQuirkSetsTests.

### 16. One iterative-attack ladder, and rake and grapple attacks use the shared attack bonus (`b12-one-attack-ladder`)

IDs: CMB-044, CMB-087. Behaviour change (the weapon Haste attack on the Full Attack button).

1. Retire `CharacterStats.GetIterativeBaseAttackBonuses`, `GetIterativeAttackBonuses` and `IterativeAttackCount` (~:5125-5155). Their consumers move to `CharacterCombatStats.GetAttackBonuses`/`GetIterativeAttackCount` (~:34-65):
   - `CharacterController.FullAttack` (~:6321);
   - `GetPlannedFullAttackCount` (~:6139), which gains the weapon Haste step, so path C makes it;
   - ActionButtonPanel.cs ~:225;
   - AIProfile.cs ~:527;
   - the flurry ladder (CharacterStats.cs ~:1320).

   Rapid Shot stays added on top where it is today.
2. **CMB-087.** `PerformRakeAttacks` (CharacterController.cs ~:6602-6644) and the grapple weapon attacks (~:11788-11797, ~:12026-12027) take their modifier from `BuildAttackBonus` (~:5685), with the grapple penalty as the sequence modifier and no Weapon Focus on a rake. Remove the hand sums.
3. **Docs.** Delete CMB-044 and CMB-087. Narrow CMB-091 and CMB-173 (the Haste part fixed).

Tests: update MonkClassRulesTests (~:354-358), GrappleDamageRulesTests (~:3474-3485, ~:4682-4691) and RapidShotTests; new scenario `rules/haste-weapon-full-attack-ui`.

TEST BUDGET: `rules/single-vs-full-attack*`, `rules/haste-natural-*`, `rules/monk-flurry-*`, `rules/improved-grab-*` (seeds 1-3), the three suites, WeaponDamageModifierTests, ImprovedShieldBashTests.

### 17. One attack-sequence executor for both sides (`b12-attack-sequence-executor`)

IDs: CMB-091, CMB-045, CMB-096 (the reads), CMB-173 (the rest); D9. Behaviour change; *broad*.

1. **`RunAttackSequence`.** Add the partial `_Core/GameManager.AttackCore.cs`, `IEnumerator RunAttackSequence(AttackSequenceRequest req, AttackSequenceOutcome o)`, and the builders `AttackSequenceRequest.ForUiAttack`, `ForUiFullAttack`, `ForAI` and `ForAoO`. `ForCharge` is used in step 21. Per step:
   1. `IAttackDecider.ChooseNextStep` (the target and step kind, or stop);
   2. `CanCommitAttack`;
   3. `CanDirectlyAttack`;
   4. `WardSaveLedger.Commit`;
   5. for a ranged or thrown step, `CanAttackWithWeapon` (~:8601), crossbow loading and the ammunition check;
   6. `TryCommitAttack` (CharacterController.cs ~:1144);
   7. for a ranged step, the one ranged-AoO helper;
   8. `ResolveAttackSequenceStep` (~:1474);
   9. `OnAttackResolved`.

   It stops when combat ends (`CurrentPhase == CombatOver`).
2. **One ranged-AoO helper (CMB-045).** Keep CombatFlowService.cs ~:285-327 with the NPC copy's fallback (derive "ranged" from the weapon when `rangeInfo` is null), and delete NPCTurns.cs ~:540-589.
3. **Callers.**
   - PC path B, with the per-click target pick through the UI decider's `ChooseNextStep`.
   - Path C, whose coroutine keeps only the 5-foot step, retarget and Haste prompt UI (`_fullAttackFiveFootStep*`, `_isAwaitingRangedRetargetSelection`).
   - NPC paths F, G and H. G and H become the F loop with a Ranged or Thrown intent; NPCs throw melee throwables by intent (CMB-096).
   - `IsUsingThrownAttackMode`, `IsAttackModeRanged` and `CalculateRangeInfo` read the intent only. Remove `NpcIntentReadsUiThrownGlobal`.
4. **Rapid Shot (D9).** It is declared before the first attack: the PC toggle is read when the first ranged step starts and fixes the action as a full attack; `RapidShotEnabled` (CharacterController.cs ~:4573, ~:12685) is reset with the attack pool for NPCs. Path B gets Rapid Shot by the same rule.
5. **Delete the dead paths** `CombatFlowService.PerformFullAttack` (~:1016-1076), `ExecuteIterativeAttacks`/`ExecuteDualWieldAttacks` (~:144-148) and `GameManager.CombatActions.PerformFullAttack` (~:2813-2821).
6. **Docs.** Delete CMB-091, CMB-045, CMB-096 and CMB-173. Record D9. Update the attack recipes in DEVELOPMENT_RECIPES.

Tests:
- Update RulesScenarios ~:5044 (the context strings), GrappleDamageRulesTests (keep or rename `PerformNPCMeleeAttackSequence` consistently) and RapidShotTests (~:299-494).
- New static suite `Tests.Combat.AttackSequenceStepTests` (the step order and a stop on combat end).
- New scenarios: `rules/ranged-full-attack-ui`, `rules/ranged-full-attack-ai`, `rules/crossbow-full-attack-reload`, `rules/rapid-shot-declared-first-ui`.

TEST BUDGET: `rules/single-vs-full-attack*`, `rules/haste-natural-*`, `rules/monk-flurry-*`, `rules/improved-grab-*`, `rules/freetrip-*`, `rules/npc-archer-ammo`, `rules/npc-throws-javelin` (seeds 1-3), RapidShotTests, GrappleDamageRulesTests, MonkClassRulesTests; `smoke/*` seeds 1-3.

### 18. Off-hand steps, AI attack modes and the held-charge touch (`b12-off-hand-ai-modes-held-charge`)

IDs: AI-055, SPL-156. Behaviour change; AI-facing.

1. **Off-hand steps.** Dual wield (`CombatFlowService.PerformDualWieldAttack` ~:1078, `CharacterController.DualWieldAttack` ~:6694) and the progressive off-hand attack (GameManager.CombatActions.cs ~:1490, CombatFlowService.cs ~:394) become `AttackStepKind.OffHand` steps of `RunAttackSequence`, with `GetTwoWeaponPenalties`. Delete the summed-damage concentration in `DualWieldAttack`; `RapidShotTests` calls it directly, so update it.
2. **AI-055.** `AttackIntent.ForAI` chooses Rapid Shot and two-weapon fighting with a minimal expected-damage rule in AIProfile (use the mode when expected damage over the turn beats not using it). Label it "AI stopgap (batch 13 catalog)". This is the default answer to owner question Q2. If the owner chooses mechanics-only, the AI never declares either and the entry stays open.
3. **SPL-156.** A held-charge delivery after the casting round is an attack step (`AttackIntent.HeldChargeTouch`: a melee touch attack) through `RunAttackSequence`: the attack action, or one step of a full attack. This is the existing delegated reading of 2026-10-14 in RULES_COVERAGE 4.1; touching friends follows PHB p.141.
   - Remove the bit `HeldChargeDeliveryIsFree` (CastCore.cs ~:782-787) and `AIService._heldChargeTriedThisTurn` (~:113).
   - Give the PC Discharge button (ActionButtonPanel) the step's action gate.
   - With that last bit gone, delete `LegacyCastQuirk.cs` and its plumbing (`RequireLegacyQuirk`, the request field, `LegacyCastQuirkSets`), and update `CastCoreStageOrderTests`.
4. **Docs.** Delete AI-055 (owner decision Q2) and SPL-156. Update CLAUDE.md "Architecture in brief" item 5 (no quirk bits left).

Tests: flip `ExpectKnown("SPL-156")` at CastPipelineScenarios.cs ~:1290; new scenarios `rules/twf-full-attack-ai`, `rules/rapid-shot-ai`, `rules/held-charge-costs-attack-ui`, `rules/held-charge-costs-attack-ai`.

TEST BUDGET: those, `rules/cast-held-touch-charge`, `rules/npc-held-touch`, `rules/ui-touch-miss-holds-charge` (grep the exact name), `rules/single-vs-full-attack*` (seeds 1-3), CastCoreStageOrderTests, RapidShotTests, AIProfileFrameworkTests.

### 19. One maneuver core with one cost table and one decider (`b12-maneuver-core`)

IDs: CMB-102, CMB-098, CMB-103, CMB-101 (the AI grapple cast); D10, D15 (item). Behaviour change.

1. **`Combat/Maneuvers/ManeuverCore.cs`.**
   - `ManeuverRequest { Attacker, Target, Type, ItemSlot, Decider }`.
   - `ManeuverActionCost.For(type, actor)` (extend `Combat/Core/AttackPool.cs` ~:213-236) returns `{ ActionCostType, ReplacesMeleeAttack }` for every `SpecialAttackType`:
     - trip, disarm, grapple and sunder replace a melee attack;
     - feint is a standard action, or a move action with Improved Feint (PHB p.95);
     - coup de grace is a full round;
     - bull rush is a standard action or the end of a charge;
     - overrun happens only in a move or charge (step 20);
     - aid another is a standard action.
   - `Legality CanStartManeuver(req)` is `CanDirectlyAttack(kind: Maneuver)` plus the maneuver gates (`CanTrip`, `CanBullRush`, `CanSunderWithAttack`).
   - `IEnumerator ExecuteManeuver(req, ManeuverOutcome)`: pay first, then the initiation AoOs (`ResolveManeuverInitiationAoOs`, GameManager.CombatActions.cs ~:932), then `CharacterController.ExecuteSpecialAttack`, then the aftermath (`HandleTripAftermath`, `ResolveBullRushPushAndFollow`), then `OnAttackResolved` for the maneuvers that are attacks.
2. **The PC side.** The PC dispatcher (GameManager.CombatActions.cs `ExecuteSpecialAttack` ~:1843-2232) and the `hasAction` cascade (GameManager.cs ~:9018-9035) build requests and read the table. The NPC side (`TryNPCSpecialAttackIfBeneficial`, NPCTurns.cs ~:338-538) builds the same requests. Both pay before the initiation AoOs.
3. **Remove the PC-only globals** from the rules: `_isDualWielding`/`_mainHandPenalty` (StandardManeuvers.cs ~:420) come from the intent, and the `attacker == ActivePC` gates (~:370, ~:402) go.
4. **NPC feint.** Add the row (`TryConsumeFeintAction`, StandardManeuvers.cs ~:933). The AI may feint when its profile scores it; keep it minimal and labelled a stopgap.
5. **`IManeuverDecider`** (`UiManeuverDecider`, `AiManeuverDecider`, `ManeuverDeciders.For`) answers:
   - the disarm and sunder item (removes the `options[0]` picks at CharacterController.cs ~:14462, ~:11714; D15);
   - the bull rush push (move the existing pair);
   - the counter-trip (move `ShouldCounterTrip`);
   - the grapple move destination (CMB-103: `AI_GrappleRestrictedTurn`, GrappleSystem.cs ~:2090, really moves through `ExecuteGrappleMovement`; PHB p.156);
   - the escape square (removes the random pick at GrappleSystem.cs ~:500; D10);
   - whether to oppose an escape (D10: the PC prompt `ShouldPromptPlayerForGrappleEscapeOpposition`/`TryResolveOpposedGrappleEscapeWithPrompt`, ~:1791-1845, becomes the UI answer; the AI answers oppose).
   - Improved Grab's skip option for the AI (the AI decider may answer skip when the profile says grappling is bad).
6. **CMB-101, the AI half.** The AI grapple turn (AIService.cs ~:289, `AI_GrappleRestrictedTurn`) gets a cast option filtered through the shared cast legality (`CheckLegalityForAI` or the cast core Legality stage).
7. **AoO substitution.** If trip and disarm as AoO substitutes (CMB-102's remaining part) do not fit this step, leave that sentence in CMB-102 and delete the rest.
8. **Docs.** Delete CMB-098, CMB-103 and CMB-101; delete or narrow CMB-102. Record D10 and D15.

Tests:
- Update GrappleDamageRulesTests (~:1783-1798; grep the `TryConsume*AttackAction` reflection).
- New scenarios:
  - `rules/npc-disarm-best-item`;
  - `rules/ai-grapple-move`;
  - `rules/grapple-escape-square-choice-ui`;
  - `rules/grapple-escape-not-opposed-ui`;
  - `rules/npc-feint`;
  - `rules/grappled-npc-casts-legal-only`;
  - `rules/maneuver-cost-table-ui` (a PC trip as the second attack of a full attack).

TEST BUDGET: `rules/maneuver-*`, `rules/grapple-*`, `rules/natural-sunder-*`, `rules/unarmed-sunder*`, `rules/stability-trip*`, `rules/ai-maneuver-stopgap-*` (seeds 1-3), GrappleDamageRulesTests, AIProfileFrameworkTests.

### 20. Overrun as a move-through for both sides (`b12-overrun-move-through`)

IDs: CMB-015, CMB-086; D11. Behaviour change.

1. **One overrun inside movement.** `OverrunSystem.ExecuteOverrunMovementPath` (~:228-360) becomes the one overrun for both sides, used by a move and by a charge.
   - Its movement goes through `MoveAlongPath` (not `MoveToCell`, ~:313, ~:352), so movement AoOs and later the step hook apply.
   - The NPC standing overrun (AIProfile.cs ~:788, NPCTurns.cs ~:494, CharacterController.cs ~:12836 `ResolveOverrun(target, defenderBlocks: true)`) becomes an AI-planned move through the defender's square.
2. **Step 1 AoO** from the defender always (CMB-086). `ThreatSystem.DoesManeuverProvokeAoO` (~:850-870) gets the Overrun case.
3. **Avoid or block** is `IManeuverDecider.ChooseOverrunResponse`, asked of the defender's decider on both sides (`ResolveEnemyOverrunResponse`, ~:385-406). Improved Overrun removes avoid and adds +4 (PHB p.96).
4. **The result.** A winner knocks the defender prone and continues. A loser moves 5 ft back (`ResolveOverrunOpposedCheck`, ~:416). An avoided overrun does not count against the actions (PHB p.157).
5. **Delete the dead code:** `ResolveOverrunSpecialAttack` (~:759-849), `ShowOverrunResponsePrompt` (~:731), the unreachable branch in `ExecuteSpecialAttack` (~:2227), and the FeatManager summary's AoO claim.
6. **Docs.** Delete CMB-015 and CMB-086; CMB-117 stays open. Record D11.

Tests: new scenarios `rules/overrun-ai-through`, `rules/overrun-pc-defender-chooses-ui`, `rules/overrun-improved-no-avoid`, `rules/overrun-defender-aoo-improved`.

TEST BUDGET: those, `rules/movement-aoo-*`, `rules/maneuver-*`, `rules/shared-square-*` (seeds 1-3), GrappleDamageRulesTests.

### 21. One charge core with the partial charge (`b12-charge-core`)

IDs: CMB-018 (the core), CMB-013, CMB-108 (planned as CMB-175; see step 1); D2. Behaviour change.

1. **`Combat/Maneuvers/ChargeCore.cs`.**
   - `ChargeRequest { Charger, Target, Path, Partial, BullRush, Overrun }`.
   - `Legality CanCharge(req)`. With `SingleActionOnly`, only a partial charge is legal: a standard action, up to speed (D2). With `MoveActionOnly`, no charge.
   - `IEnumerator ExecuteCharge(req, ChargeOutcome)` runs the step loop (today's per-step `MoveAlongPath` plus `ResolveMovementAoOsBeforeStep`), then `ResolveChargeEnd`:
     - flanking from `CombatUtils.IsAttackerFlanking`;
     - smite (the NPC `TryNPCDeclareSmite`, ~:1988, and the PC declaration feed the same request field);
     - Spirited Charge for any mounted charger;
     - then `RunAttackSequence(ForCharge)` (pounce and rake are steps), the bull rush (`ResolveChargeBullRush`, ~:1826-1866) or the overrun (step 20);
     - `ApplyChargePenaltyUntilStartOfNextTurn`.
   - It replaces the bodies of `SupportActions.ExecuteCharge` (~:1499-1781) and `NPCExecuteCharge` (~:1920-2130). Both become thin wrappers: the PC one keeps its UI fields and `AfterAttackDelay`, and the NPC one keeps the 0.8 s wait.
   - `EndChargeBullRushForDroppedCharger` (~:1872) returns its result in `ChargeOutcome` and no longer ends the PC turn while an NPC charges.
2. **The partial charge (CMB-108).** The charge gates (~:984, ~:1086, ~:1518, ~:1929) call `CanCharge` instead of requiring `HasFullRoundAction`. The PC charge button and the AI charge choice offer the partial charge when it is legal.
3. **CMB-013.**
   - Spirited Charge passes its multiplier into `WeaponDamageRoll.Roll`. It multiplies the weapon damage per PHB p.134, not `TotalDamage` after extra dice (PHB p.100).
   - Snatch Arrows (CharacterController.cs ~:8171), DemonArmorBehavior.cs ~:98, MaceOfSmitingBehavior.cs ~:77 and MountedCombatSystem.cs ~:188, ~:268 deal damage through `GameManager.DealDamage` packets or the weapon roll.
4. **Docs.** Delete CMB-018, CMB-013 and CMB-108. Record D2.

Tests: new scenarios `rules/charge-partial-disabled`, `rules/charge-partial-slowed-ai`, `rules/spirited-charge-dr`, `rules/charge-flanking-ai`. Re-run `rules/haste-natural-pounce-ui`, `rules/pin-release-*-charge`, `rules/improved-grab-size-charge`, `rules/maneuver-bullrush-charge-*` and `rules/charge-flanking-ui`.

TEST BUDGET: those and `rules/*charge*` (seeds 1-3), FlankingReachRulesTests.

### 22. Every mover passes the same movement legality checks (`b12-can-start-movement`)

IDs: GRID-019, GRID-022. Behaviour change; *broad*.

1. **`MovementService.CanStartMovement(actor, MoveKind kind, int squares)`**, with `MoveKind { Move, DoubleMove, Run, Withdraw, RestrictedWithdraw, FiveFootStep, Charge }`, returns `Legality`. It combines:
   - the prone check;
   - no 5-foot step in a round with other movement, and no other movement after a 5-foot step (`HasTakenFiveFootStep`, `HasMovedThisTurn`; PHB p.144);
   - `CombatFlowService.CanPerformWithdraw` (~:27-74), plus no withdraw while Blinded (PHB p.143);
   - Restricted Withdraw: with `SingleActionOnly`, a withdraw is a standard action at single speed (PHB p.144; `GetWithdrawMoveRangeSquares`, GameManager.cs ~:6849, stops doubling);
   - `MoveActionOnly` allows a move but never a double move, a withdraw or a run.
2. **Callers.**
   - The PC buttons (`OnMoveButton`, `OnWithdrawButtonPressed` ~:6870, the 5-ft button).
   - `MoveCharacterAlongComputedPath` and `MoveCharacterAlongComputedPathWithdraw` (GameManager.CombatActions.cs ~:1207-1289).
   - The AI 5-foot step (AIService.cs ~:1253, ~:1424).
   - The behaviour controllers' entry points (GameManager.cs ~:11084, ~:11087).
   - A refused AI move logs and returns false, and the AI routine continues.
3. **Docs.** Delete GRID-019 (its grappling half is already done) and GRID-022.

Tests: a new static suite `Tests.Grid.MovementLegalityTests` (the table above); new scenarios `rules/ai-no-5ft-then-move`, `rules/restricted-withdraw-disabled-ui`, `rules/restricted-withdraw-disabled-ai`, `rules/blinded-no-withdraw-ai`.

TEST BUDGET: those, `rules/movement-*`, `rules/shared-square-*`, `rules/grappler-feared-stays`, `rules/prone-scripted-move` (seeds 1-3); `smoke/*` seeds 1-3.

### 23. Grease, zones and path cost on every movement step (`b12-movement-step-hook`)

IDs: GRID-018, GRID-013; D12. Behaviour change. Depends on step 4 (NPC Balance).

1. **`IMovementStepHook`** (`OnStep(actor, from, to, MoveKind, ref bool stop)`), registered in `MovementService` and run per step inside `CharacterController.MoveAlongPath` (~:3983). The hooks:
   - Grease (moved from GameManager.CombatActions.cs `ExecuteMovement` ~:1116, ~:1166 and GameManager_Grease.cs ~:512-555);
   - zone entry for the area effects that react to entry, so crossers are caught;
   - the Wall of Ice breach (moved from ~:4053-4055).

   The charge loops (SupportActions.cs ~:1537, ~:1946), `ExecutePathWithMovementAoOs` (GameManager.CombatActions.cs ~:850-914), the overrun path and the PC `ExecuteMovement` all inherit it. The 5-foot step runs the same list with `MoveKind.FiveFootStep`.
2. **Grease by D12.**
   - Balance DC 10 once per round on the first greased square of movement: success gives half speed in the area for the round; failure stops the creature and it makes the Reflex save; failure by 5 or more is a fall.
   - The Reflex save for creatures in the area at casting and on each of the caster's later turns, through `SaveRollRules`.
   - Delete the Reflex save on entry (Grease.cs ~:520-555). Fix the double save in `PersistentAreaEffect.UpdateCharacterTracking` (~:616-660).
   - The Grease scenarios that pinned the old entry save change; update them with the reason.
3. **GRID-013.**
   - `SquareGridUtils.CalculatePathCost` (~:201) and `MovementService` (~:136, ~:406) apply the A* wall cells and corner rules (SquareGrid.cs ~:338-364): Resilient Sphere, Wall of Ice, Wall of Force.
   - `FiveFootStep` (CharacterController.cs ~:15526) refuses a step through or into a wall.
   - `ExecuteMovement` counts cost through the same function.
4. **Docs.** Delete GRID-018 and GRID-013. Record D12. Update the Grease row in RULES_COVERAGE.

Tests: new scenarios `rules/grease-crossing-ai`, `rules/grease-balance-half-speed-ui`, `rules/grease-caster-turn-save`, `rules/five-foot-wall`, `rules/wall-of-force-path-cost`. Re-run `rules/cast-ui-grease` and `rules/cast-globe-grease-armor`.

TEST BUDGET: those, `rules/movement-*`, `rules/grapple-escape-move-aoo*`, `rules/*charge*` (seeds 1-3), FlankingReachRulesTests.

### 24. Creature-factory leftovers: armor speed, the innate caster level, the Lion's Shield summon, AI spontaneous conversion (`b12-factory-leftovers`)

IDs: ITM-010, CRE-016 (the Level part), CRE-005, CHR-070 (the AI conversion and the neutral cleric's choice). Behaviour change. If this step is too large for one commit, split it into 24a (armor speed, innate CL) and 24b (Lion's Shield, conversion).

1. **ITM-010.**
   - `CharacterStats.EffectiveSpeedFeet` (~:4311-4335) applies the PHB Table 7-6 speed by armor category (`MaterialProperties.GetEffectiveArmorCategory`, ~:198, as `BarbarianFastMovementBonus` does at ~:1695-1712): medium and heavy armor reduce a base 30 ft to 20 ft and 20 ft to 15 ft (PHB p.123).
   - Dwarves are exempt (PHB p.14), and so is `SpeedNotReducedByArmor`.
   - Restore the unarmored base speed in the baked NPC entries that ITM-010 lists (re-audit with grep "reduces from 30" and the entry's list, for example NPCDatabaseCustom.cs ~:537).
2. **CRE-016, the Level part.**
   - The `CharacterStats.Level` sync (~:474-489) skips `InnateSpellcastingClass`.
   - Store each dragon's caster level on its definition (MM p.68-69 age table) and replace the nested scan in NPCSetup.cs ~:964-998.
   - Spells known by PHB Table 3-16 are deferred (owner decision Q3, 2026-10-23); narrow CRE-016 to that.
3. **CRE-005.**
   - `LionsShieldBehavior.ActivateSummon` (~:239-302) spawns through `GameManager.SpawnSummonedCreature` (GameManager.SpellCasting.cs ~:146; the `_activeSummons` add ~:500): initiative, the index-parallel AI behaviour entry (AI-015), duration and despawn.
   - Delete `BuildSummonStats` and the `gm.Summoning.RegisterSummonedCreature` call. Narrow CORE-023 for the `SummoningService` tracker.
   - Repoint CreatureProgressionTests and `rules/summon-creature-entries`, which call `BuildSummonStats`.
4. **CHR-070.**
   - `AiCastDecider` and `SpellCastRequest.ForAI` build `CastSource.SpontaneousConversion` when a cleric with spontaneous casting wants a cure or inflict spell it has not prepared and has a slot to sacrifice (PHB p.32). The AI chooser lives in the AI spell selection.
   - Add `NPCDefinition.ClericSpontaneousChoice` (Cure, Inflict or none) for a cleric neutral on the good-evil axis, copied in `Clone` and `CopyDefinitionFields`, and read it in `InitializeNPCFromDefinition` (~:583).
   - Deity, specialization and familiar are deferred (Q3); narrow CHR-070 to them.
5. **Docs.** Delete ITM-010 and CRE-005; narrow CRE-016, CHR-070 and CORE-023.

Tests: a new static suite `Tests.Character.ArmorSpeedTests` (a human in chainmail at 20 ft, a dwarf at 20 ft unchanged, and a 20-ft race at 15 ft); new scenarios `rules/armor-speed-npc`, `rules/lion-shield-summon`, `rules/npc-cleric-converts-cure`. Re-run `rules/dragon-ai-breath-*`, `rules/class-progression`, `rules/creature-progression` and `rules/summon-*`.

TEST BUDGET: those (seeds 1-3), CreatureProgressionTests, NPCTemplateSystemTests, NpcSlotReuseTests.

### 25. Final full regression (`regression-final`)

Kind regression. The same runs as the midpoint (static edit and Play passes, `rules/*,smoke/*` seeds 1-5, the soak seeds 1-50). Record the run in `docs/TESTING.md` 3.2, with the soak compared against the midpoint and against batch 11's final run. The expected, explained shifts are:
- archers spend ammunition;
- fighters Cleave;
- grabs happen per hit;
- the AI uses Rapid Shot and two-weapon fighting;
- partial charges;
- NPC overruns move through;
- Grease is a Balance check.

Change no game code; file any regression as a new issue. Then update `CLAUDE.md` "Current state" (batch 12 done, next batch 13) and PARITY section 5 status lines.

## Behaviour changes

| Step | What players and the AI will notice |
|---|---|
| 2 | The bombardier beetle's spray is weaker (1d4+2, Fortitude negates) and once per round; PC charges get flanking |
| 3 | Slowed creatures cannot dual-wield or full attack; disabled enemies take one action and then drop to dying; an AoO that leaves a mover at exactly 0 hp no longer stops it |
| 4 | Enemies use ability modifiers for untrained Listen, Spot, Balance, Escape Artist and Sense Motive |
| 6 | Every creature's turn-start effects happen even when it cannot act; dragons and other breathers breathe again; rage ends for every barbarian; a tick kill ends the fight at once |
| 7 | Nauseated creatures move, pinned creatures try to escape, panicked and turned creatures flee (PCs as forced turns, Q1) |
| 8 | PCs locate invisible creatures by Listen and attack them with a 50% miss |
| 9 | Every cast checks for the end of combat; the area logs are uniform; PC casts meet the slot and action tests at Legality |
| 10 | Natural 1 and 20 on every save; Luck and countersong on every save; Evasion against the Javelin of Lightning and breath; the AI's ranged touch uses the full terms |
| 12 | The Resilient Sphere blocks every attack; Sanctuary and Hide from Undead are rolled once per attacker, and failures cost the attack; maneuvers need reach |
| 14 | Improved Grab on every hit, AoOs and pounces included; fear and charm breaks everywhere; NPC archers spend arrows; javelins and darts are dropped |
| 15 | Cleave and Great Cleave on every melee attack within reach; the beetle sprays when attacked |
| 16-17 | The PC Full Attack button makes the Haste attack; one ranged full attack for both sides; Rapid Shot declared first |
| 18 | The AI uses Rapid Shot and two-weapon fighting (Q2); a held charge costs an attack after the casting round |
| 19-21 | The AI chooses disarm and sunder items, grapple moves, escape squares; NPCs feint; overrun is a move-through with the defender's AoO; slowed or disabled creatures can make a partial charge |
| 22-23 | The AI cannot 5-foot step and move; Restricted Withdraw; Grease affects every mover through Balance; walls cost and block every path |
| 24 | Armor slows everyone but dwarves; the Lion's Shield lion takes real turns; NPC clerics convert to cure or inflict |

## Risks

- **Re-entrancy (CORE-012).** `BeginCombatantTurn` runs inside the skip gate's synchronous `NextInitiativeTurn` chain. It must stay free of yields, and the skip gate must stay the last thing before advancing. `rules/three-skipped-in-a-row` guards this. A forced PC compulsion turn uses the Confusion pattern's sub-phase and `EndCurrentTurn`.
- **Trace churn.** Steps 6, 7, 10, 14, 15 and 17 change many hashes. The pure moves (5 and 13) must prove the baseline first; stop a pure move whose hash moves. Explain every behaviour-step difference with `ScenarioTraceDiff`.
- **Coroutines inside the hook and the executors.** The Improved Grab, counter-trip, Cleave and Haste prompts suspend the loop. NPC loops must await `OnAttackResolved`, and PC paths continue by callback. Every loop re-checks `CombatOver`. `ResetCombatStateForNextEncounter` stops coroutines (CORE-013), so a suspended decider must clear its state there.
- **Reused slots.** The tracker, `CurrentTurnStart`, the ward ledger, the Cleave and spray flags, the Rapid Shot declaration and the intent must be cleared on slot reuse. Extend `NpcSlotReuseTests`.
- **Index-parallel lists (AI-015).** Summon cleanup in the hook and the Lion's Shield summon change `NPCs`. Iterate snapshots, and keep `_npcAIBehaviors` parallel.
- **Reflection (TST-006).** Steps 13, 16, 17 and 19 rename or delete reflected members. Grep the tests first.
- **AI shifts.** Steps 3, 6, 14, 15 and 18 change soak outcomes. That is expected, but each regression must explain the shift.
- **Mid-turn Slow and Haste.** `RefreshActionLimits` must never grant back a spent action.
- **Large files.** Steps 13, 17 and 19 touch GameManager (about 11.5K lines) and CharacterController (about 12K lines). Keep each commit to one concern. Use quirk bits if a step cannot finish in one commit.
- **Weekly usage.** This batch has 23 build steps and two regressions, larger than the batch plan's estimate of 10-14 commits. The queue must respect the owner's weekly cap (no new step at 62%, closing regression below 64%). If the cap stops it, the natural cut points are after step 11 (the midpoint) and after step 18.

## Not in scope

- The action catalog, the PC restricted menu for Charmed and the PC flight-path choice (CMB-100 menu part), `ActorActionSet`, and personalities: batch 13.
- MM skill ranks for NPCs (CHR-067 rest), class feats, skills, spells and gear for class levels added to creatures (CRE-058), dragon spells known by PHB Table 3-16 (CRE-016 rest), and the NPC deity, specialization and familiar fields (CHR-070 rest): batch 19 by default (Q3).
- AI-053's other items (auras and spittle on controller turns, uncalled members); AI-040's action cost for the breath (batch 13 catalog).
- The overrun defender's knock-prone counter (CMB-117); the believed-square attack UI for PCs; starting and completing a full-round action over two rounds (PHB p.142).
- The `Random.Range` and `Random.value` sites outside the save, maneuver and grapple scope (CORE-030 rest).
- Wish (9th level).

## Owner decisions (2026-10-23)

The owner took the recommended default on all three design questions:

- **Q1. Fleeing PCs (CMB-100, step 7): automatic flight.** Until batch 13 builds the restricted PC menu, a panicked, turned or frightened PC flees automatically through the shared flee controller, as a forced turn like confusion today.
- **Q2. AI attack modes (AI-055, step 18): stopgap rule now.** The AI declares Rapid Shot and two-weapon fighting by a minimal expected-damage rule, labelled a stopgap for batch 13's catalog.
- **Q3. Factory-leftover scope (step 24): defer to batch 19.** CRE-058's class feats, skills and spells, CRE-016's spells known and CHR-070's deity, specialization and familiar fields wait for batch 19 with the MM skill data; batch 12 does only the AI conversion, the neutral cleric's choice and the innate caster level.

Every rules question is settled above (D1-D15) under the delegation; the owner may overrule any of them.
