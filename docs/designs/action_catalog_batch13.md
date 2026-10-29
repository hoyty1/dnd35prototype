# Batch 13: AI groundwork and the action catalog

> Status (2026-10-29): building. Step 1 (docs) is committed (20f0161); step 2 (AI groundwork, a pure move) is committed (d41d23d), with every pre-existing scenario hash equal to the launch baseline (rules/* run 20261029-131339, smoke/* run 20261029-131636, seeds 1-3, at 20f0161); step 3 (the catalog skeleton, a pure move, `docs/architecture/combat-actions.md`) is built on d41d23d and not yet committed, with the smoke/* hashes (run 20261029-141011, seeds 1-3) equal to the baseline and the Play pass of its four suites OK; nothing calls its runners outside the tests yet; steps 4-21 are not started. Produced by a read-only design pass: three code mappers on master e48deb5, two independent designs, and one judge who checked the key claims against the code at 3bbf47d (the charge-terrain commit that followed) and against the books. Nothing here was compiled, run or verified in Play mode. Line numbers are anchors from 2026-10-29: they drift, so anchor on the member names.

## Summary

Batch 13 is PARITY section 5 step 10 (`docs/systems/PC_NPC_PARITY.md`) plus the batch plan's AI prerequisites and tags v2. It does four things.

1. **AI groundwork.** It fixes the AI faults that would poison later batches:
   - unguarded UI calls (AI-018) and leaking profiles (AI-017);
   - dead members (AI-053, AI-027);
   - attacks on targets the AI cannot reach or hit (AI-075, owner ruling 2026-10-29);
   - moves paid after the walk, or with no path (AI-076);
   - archetypes with no profile (AI-004, AI-003), and casters that never cast (AI-002).
2. **The action catalog** (`Assets/Scripts/Combat/Actions/`).
   - It has one entry per combat action in scope, with its PHB Table 8-2 cost and provocation, its rules page and its build status.
   - Each entry runs one actor-agnostic executor. The PC panel reaches it through `RunPlayerAction`; the AI, the harness and forced turns through `RunAIAction`.
   - The finished cores (cast, attack sequence, maneuver, charge, overrun) are wrapped, not rewritten. The PC-bound executors (aid another, crawl, items, turn undead, rage, walls) are extracted. The missing ones are built: total defense, the AI's reload, draw and sheathe, breath as an action.
3. **Tags v2.** Typed target facts behind an omniscient knowledge provider, with exact matching and wound bands. Batch 17 swaps the provider; batch 14 scores the facts.
4. **The legal set.** `ActorActionSet` lists what an actor may do now.
   - For the AI it runs in shadow mode: a disagreement is a `Debug.Log` line.
   - It drives the restricted PC menu for frightened, turned and charmed PCs (CMB-100, the inherited Q1).
   - Batch 22's evaluator will choose from it.

There are 21 steps: 19 build steps and two full regressions. Steps 2, 3, 6 and 9 are pure moves. The other 15 build steps are behaviour steps, each changing one rule for both sides at once.

**Why this base.** The base is design B ("foundation"), cut down, with design A's ("strangler") best parts merged in.
- **What B brings.** The seams later batches need: `ActorActionSet` for batch 22, and a settable `KnowledgeService.Provider` for batch 17. B also splits by concern, puts total defense before the guard (the casting profiles fall back to it), and maps Caster creatures correctly.
- **What was cut from B.** It ran four steps over the target. Cut: Run (filed as CMB-202 for batch 18); the full panel-from-set rewrite (kept as a shadow differential); the `ManeuverCost` rename; `EncounterEntityId` (batch 17); non-dragon breath (batch 16); the lich aura (batches 16 and 18).
- **What A brings.** The pure move executor with quirk bits (B walked PCs through the AI walker in a pure step), the minimal skeleton, the turn-undead skip decider, and frightful presence on every attack sequence.

**Launch prerequisites.**
1. **Met:** the charge-terrain change is committed and pushed (3bbf47d). No step here touches charge bypass (owner ruling 2).
2. **Environment.** A clean master at or after 3bbf47d, with Unity 6000.4.0f1 open on this project. `Library/Bee` must exist, and the Unity MCP must be attached here (check the console paths).
3. **A hash baseline.** 3bbf47d changed charge and movement traces after the batch 12 follow-up regression (b1759ad). Step 2's agent records it first, before any code change (step 1 is docs-only, so HEAD's code is the launch commit's):
   - run `ScenarioHarness.QueueFresh("rules/*,smoke/*", "1-3", "")` at the launch commit;
   - keep its hashes for the pure moves;
   - file any failure.
4. **Usage.** Weekly usage below 62% at the start of each step.
5. **Books.** Before step 14, re-check DMG p.229 in the PDF; the local extract lacks it.

## Rules decisions (2026-10-29, delegated; the owner may overrule)

These were settled under the owner's delegation (2026-10-11, 2026-10-14).
- Where the books are silent, the reading with the most textual support is applied.
- Each applied reading is recorded in `docs/systems/RULES_COVERAGE.md` 4.1 when its step lands, labelled "delegated RAW reading" in code, and reported.
- "Explicit RAW" needs only the cite.

Pages are printed pages (pdf page = printed + 1).

| ID | Question | Kind | Applied in step |
|---|---|---|---|
| D1 | When may an AI-run creature declare an attack | owner direction + explicit RAW | `b13-attack-reach-guard` |
| D2 | Total defense, and the cornered panicked creature | explicit RAW + delegated RAW reading | `b13-total-defense` |
| D3 | When a move action is spent | delegated RAW reading | `b13-ai-move-pays-first` |
| D4 | Reloading crossbows | explicit RAW | `b13-posture-and-reload` |
| D5 | Stand up, crawl, drop prone, and a prone confused creature | explicit RAW + delegated RAW reading | `b13-posture-and-reload` |
| D6 | Aid another and waking an ally | explicit RAW | `b13-aid-another-and-walls` |
| D7 | Retrieving and using potions, scrolls and wands | explicit RAW + delegated RAW reading | `b13-item-actions` |
| D8 | Equipment actions in combat | explicit RAW + delegated RAW reading | `b13-equipment-actions` |
| D9 | Which undead a turning check turns | explicit RAW + delegated RAW reading | `b13-class-abilities` |
| D10 | Rage | explicit RAW | `b13-class-abilities` |
| D11 | Breath, spittle and engulf as actions | explicit RAW | `b13-monster-ability-actions` |
| D12 | Frightful presence | explicit RAW + delegated RAW reading | `b13-monster-triggers` |
| D13 | When auras and gibbering fire | delegated RAW reading | `b13-monster-triggers` |
| D14 | What a charmed creature may do to its charmer | explicit RAW + delegated RAW reading | `b13-restricted-menu` |
| D15 | What a frightened, turned or panicked PC may do | explicit RAW + delegated RAW reading | `b13-restricted-menu` |

### D1. The AI attack guard

**Decision.** The owner ruled (2026-10-29) that AI creatures "should never waste an attack on something they can't hit". No AI-chosen attack is declared against a target the attacker cannot reach or hit with that attack. This applies to NPCs and AI-run party members alike.

"Cannot hit" is explicit RAW and means any of these:
- **Reach.** The target is outside the reach of the melee weapon in hand. Or it is adjacent to a reach weapon that cannot strike adjacent foes (PHB p.112, p.137; the spiked chain, p.115, is the exception).
- **Range.** The target is beyond the weapon's maximum range: five range increments thrown, ten projectile (PHB p.134, p.139).
- **Ammunition.** The projectile weapon has no ammunition (PHB p.113), or the crossbow is unloaded and cannot be reloaded this turn.
- **Legality.** `AttackLegality.CanDirectlyAttack` refuses the attack (the sphere, the hold bar, and from step 20 the charm bit).

These do not count:
- **Ward saves** still to roll (`LegalityRisk.WardSaveUnrolled`): the save may succeed, and a failure costs the attack (batch 12 D6).
- **Miss chances and concealment:** they make a hit unlikely, not impossible.
- **Spell range:** it never widens the weapon range; that widening is the AI-075 cause.
- **Line of effect:** no weapon attack tests it on either side (GRID-009), so an AI-only test would split the rules.

The guard adds no rule. It asks the shared predicates before the AI declares, as the PC's target highlighting does. When nothing passes, the behaviour profile picks the alternative (C1).

### D2. Total defense

**Decision.** Explicit RAW, PHB p.142:
- Total defense is a standard action that does not provoke.
- It gives +4 dodge AC from the moment it is taken until the start of the creature's next turn.
- It cannot be combined with fighting defensively or Combat Expertise. Both need an attack action, so the action economy already enforces this.
- A creature in total defense makes no attacks of opportunity.

Losing the Dexterity bonus to AC also loses dodge bonuses (PHB p.136).

**Delegated RAW reading.** A cornered panicked creature "cowers" and typically takes total defense (PHB p.311). Total defense is applied, not the Cowering condition (p.307). That condition's "no actions" would forbid the very action the text names. This matches CMB-179.

### D3. When a move action is spent

**Decision (delegated RAW reading).** A move action is spent when the movement starts, for both sides. An action is taken, then resolved (PHB p.138), and the PC side already pays first.
- A move refused before its first step spends nothing: no path, `CanStartMovement` refusing, a cancelled AoO prompt.
- A move stopped midway (an AoO, a zone, a hook) is spent.

### D4. Reloading crossbows

**Decision.** Explicit RAW:
- **Normal loads (PHB p.115).** A hand or light crossbow loads as a move action; a heavy crossbow as a full-round action.
- **Provocation (PHB p.141).** Both loads provoke (Table 8-2, realigned; see Corrections).
- **Rapid Reload (PHB p.99).** It makes the hand or light load free and the heavy load a move action. Both still provoke.
- **Repeating crossbows (p.116).** They are in scope only if the code models them; otherwise note them on CMB-032.
- **Action economy.** An NPC that reloads with its move action fires with its standard action in the same turn (AI-057).

### D5. Posture

**Decision.** Explicit RAW (PHB p.141-143; prone, p.311):
- Standing up is a move action that provokes.
- Crawling 5 feet is a move action that provokes as movement.
- Dropping prone is a free action.

**Delegated RAW reading (Confusion, PHB p.212, is silent on prone).**
- A prone confused creature stands first, with its move action, when its roll lets it act ("act normally", or "attack the nearest" when it does not already threaten that creature).
- It does not stand on a "babble" or "flee" roll (CMB-074).
- When the AI should crawl instead of standing is batch 14's scoring.

### D6. Aid another and waking an ally

**Decision.** Explicit RAW:
- Aid another is a standard action with an attack roll against AC 10 (PHB p.154).
- It provokes only when the aided action would (Table 8-2 "Maybe", footnote 2, p.141).
- Waking a sleeper is a standard action, an application of aid another (PHB p.280).

The extracted executor pays before it rolls. Today's code pays after the roll; that is an ordering of the same rule, so no issue is filed.

### D7. Items

**Decision.** Explicit RAW:
- **Retrieve.** Retrieving a stored item is a move action that provokes (PHB p.141).
- **Potions and oils.** Drinking a potion or applying an oil is a standard action that provokes (PHB p.141).
- **Scrolls.** A scroll (spell completion) takes the spell's casting time, at least a standard action, and provokes as casting does (DMG p.213).
- **Wands.** A wand (spell trigger) takes a standard action, or the casting time if longer, and does not provoke. It must be held (DMG p.213, p.245).
- **Administering.** Giving a potion to an unconscious creature is a full-round action that provokes (DMG p.229, cited from ITM-070; re-check in the PDF).

**Delegated RAW readings:**
- **A wand in easy reach is drawn like a weapon.** PHB p.142 applies the draw rules to weapon-like objects in easy reach, wands included: a move action, no AoO, free with a move at BAB +1. Other stored items are retrieved.
- **A free-action scroll is free and does not provoke.** Feather Fall is one; it is treated like a quickened spell (Table 8-2).
- **A charmed creature administers a potion only to an unconscious charmer.** A conscious charmer drinks its own (ITM-070).
- **A retrieved item stays in hand** until it is used, stowed or dropped.

### D8. Equipment actions

**Decision.** Explicit RAW (PHB p.141-143 with the Table 8-2 footnotes; Quick Draw p.98):
- **Draw a weapon:** a move action, no AoO.
  - It is free with a regular move at BAB +1 (footnote 4), and free with Quick Draw.
  - Two-Weapon Fighting draws two light or one-handed weapons at once.
- **Sheathe:** a move action that provokes.
- **Ready or loose a shield:** a move action, no AoO. Footnote 4 applies (p.143).
- **Pick up:** a move action that provokes.
- **Drop:** a free action.
- **Thrown weapons:** each next weapon is a draw. Only Quick Draw throws at the full attack rate (p.98).
- **Armor:** it takes rounds to don or remove (PHB Table 7-7, p.123). Nothing spans rounds here, so armor is OutOfScope in combat (ITM-089).

**Delegated RAW reading.** The free draw "with a regular move" is one draw per move action spent moving. A 5-foot step is not a move action, and a charge is not a regular move, so neither carries a free draw.

### D9. Turn undead

**Decision.** Explicit RAW, PHB p.159:
- The closest turnable undead is turned first.
- The range is 60 ft, with line of effect and no total cover.
- Turning damage is a pool of Hit Dice. Undead with HD of half the turning level or less are destroyed.

**Delegated RAW readings:**
- **Stopping.** The pool stops at the first undead in distance order that does not fit. The text allows only one skip, over already-turned undead, so no other skip is read in.
- **Skipping turned undead** is the turner's choice: the PC is asked once per use, and the AI skips them.
- **Ties** at equal distance go to the lower HD, which turns the most.
- **The player no longer picks** whom to turn (CMB-201).

### D10. Rage

**Decision.** Explicit RAW, PHB p.25: entering a rage takes no time, but only on the barbarian's own turn. One executor serves the PC button and the AI.

### D11. Breath, spittle and engulf

**Decision.** Explicit RAW:
- **Breath.** A standard action (MM p.315, supernatural abilities; MM p.306, breath weapons). A dragon's breath recharges in 1d4 rounds (MM p.69). A supernatural ability does not provoke (PHB p.141).
- **Spittle.** A free action once per round against one opponent within 30 ft (MM p.126).
- **Engulf.** A standard action (MM p.202).
- The breath damage stays rolled once per breath (batch 11 D6).

### D12. Frightful presence

**Decision.** Explicit RAW (dragons MM p.69; the glossary MM p.309 for every bearer, whose entry names the triggering actions):
- **Trigger.** It fires when the bearer attacks or charges. Flying overhead is not modelled.
- **Radius.** A dragon's is 30 ft x its age category; other bearers use their entry (usually 30 ft).
- **Who.** Only opponents with fewer HD than the bearer. Dragons ignore dragons.
- **Save.** Will, DC 10 + 1/2 the bearer's HD + its Cha modifier, unless the entry differs. A success makes that opponent immune to that bearer's presence for 24 hours.
- **Failure.** For a dragon, 4 HD or less is panicked for 4d6 rounds, and 5 HD or more is shaken for 4d6 rounds. Other bearers use their entry; the glossary default is 5d6 rounds.

**Delegated RAW readings:**
- **Once per declaration.** It fires once per attack sequence, charge or breath, not once per attack roll.
- **Attacks of opportunity.** An AoO is an attack, so it fires the presence.
- **Breath.** A breath fires it: MM p.306 calls it a breath weapon attack.
- **Witnesses.** "Opponents who witness the action" (p.309) are those in the radius that can see the bearer (`CanSee`).
- **No re-roll** while a creature is still shaken or panicked by that bearer.
- **Immunity** lasts the encounter. It is kept per bearer and cleared on slot reuse.

### D13. When auras and gibbering fire

**Decision (delegated RAW reading; the MM is silent on timing).**
- **When.** An aura fires once per round, at the start of the bearer's acting turn (`BeginActingTurn`), for every bearer: AI-run, controllable or compelled. Today compelled turns skip it (AI-053).
- **Saves.** Each creature in range saves at most once per round per aura.
- **Gibbering** affects every creature in range except other mouthers, allies included (MM p.126). Today only enemies are affected.
- **Spittle** is a choice, so it is skipped on a compelled turn.

### D14. Charm

**Decision.** Explicit RAW, PHB p.209 (Charm Person; Charm Monster on the same page): the charmer is a trusted friend and ally.
- The charmed creature makes no attack, maneuver or hostile spell against the charmer.
- One legality bit enforces this for both sides: `AttackLegalityChecks.Charm` in `All`, plus the cast core's direct-attack legality.

**Delegated RAW reading.** An attack of opportunity is an attack, so a charmed creature makes none against its charmer.

### D15. The frightened, turned or panicked PC's turn

**Decision.** Explicit RAW:
- **Frightened** (PHB p.309): it flees from the source as best it can and may use special abilities, spells included, to flee. If it cannot flee, it may fight.
- **Turned** undead (PHB p.159) flee by the best and fastest means available.
- **Panicked** (PHB p.311): it drops what it holds, flees at top speed on a random path, and takes no other action. So a panicked PC stays automatic, through the shared flee controller. The random path is out of scope (AI-044).

**Delegated RAW reading.** "As best it can" means a frightened or turned PC keeps choosing flight while flight is possible. Its restricted menu offers:
- moves that end farther from every source than they start (move, double move, withdraw);
- stand up when prone;
- a grapple escape;
- Escape-role spells, abilities and items;
- free actions that do not stop the flight, such as dropping an item.

End Turn is refused while a flight option is legal. A PC with no flight option at the start of its turn is cornered and keeps its full menu (batch 12, "it may fight"). Run is offered once it exists (CMB-202).

## Architecture

### End state

**`ActionDefinition`:**
- `Id` (from `ActionIds`), `Name`, `Category`.
- `ActionCostType Cost`: the existing enum in `_Core/Commands/IGameCommand.cs`, reused in place. An optional `CostFor(actor, ctx)` covers per-actor costs: Rapid Reload, Quick Draw, casting time, the free draw with a move.
- `ProvokeRule Provokes` (No, Yes, Maybe, Varies, ByMovement).
- `ActionTargetKind Target` (None, Self, Creature, Cell, Item).
- `[Flags] ActionRoles Roles` (Attack, Approach, Retreat, Escape, Defend, Heal, Support, Equip, Posture, Item).
- `RulesRef`, `ImplStatus Status` (Delegating, Extracted, New, PcOnly, AiOnly, Missing, OutOfScope) and `IssueIds`.
- `Executor`, `UiAfter` (None, ShowChoices, AttackDelay, EndTurn, plus seconds) and `IsAvailableTo(actor)`.
- `AiModeValuation`: the interim attack-mode chooser, on the attack entries only (C2).

**`ActionIds` and `ActionCatalog`:**
- `ActionCatalog` has a lazy `Init()`, `Get(id)`, `All` and `AvailableTo(actor)`.
- `AvailableTo` adds the per-creature entries: breath, spittle, ranged specials, engulf.

**`IActionExecutor`:**
- `CanStart(actor)` and `CanExecute(ActionContext)` return `Legality`. They are pure and roll no dice.
- `IEnumerator Execute(ActionContext, ActionOutcome)` pays through `ActionCosts.TryPay` or through the core it wraps.
- It never continues the UI (`ShowActionChoices`, `AfterAttackDelay`, `EndActivePCTurn`, `DelayedEndActivePCTurn`).
- It never reads `IsControllable`, `ActivePC` or `IsPlayerTurn`.

**Other types:**
- **`ActionContext`:** Definition, Actor, `ActorSource Source`, Target, Cell, Item, InventoryIndex, Spell, Variant, `object CoreRequest`.
- **`ActionOutcome`:** Performed, `Legality Refusal`, `ActionCostType Spent`, Provoked, ActorDown, CombatEnded, `object CoreOutcome`.
- **`ActionCosts`:** `CanAfford`, `TryPay` (over `CommitStandardAction`, `UseMoveAction`, and the full-round and swift spends) and `From(CastActionCost)`. `ManeuverCost` and `ManeuverActionCost.For` stay; a test checks that they agree with the catalog.
- **`ActionLegality`:** adapters into `Legality`: `FromDisabledReason(string)`, `FromCheck(bool, string)` and `FromCast(CastOutcome)`.
- **`ActionDeciders.For(actor, ActorSource?)`:** the catalog's only `IsControllable` read. The Ui decider confirms provocation through `ShowAoOActionConfirmation`; the AI decider always confirms.
- **`ActorActionSet`:**
  - `Build(gm, actor, source)` returns `ActionOption { Definition, Legality, ProvokesNow }`, after the compulsion and charm filters (`CurrentTurnStart`, the charm links).
  - It also has `Allows(id)`, `Check(id, ctx)` and `LegalTargets(id)`.

**Runner** (partial `_Core/GameManager.ActionRunner.cs`):
- **`RunPlayerAction(ctx)`** starts the executor, then applies `UiAfter`. A refusal shows its reason and `ShowActionChoices`.
- **`RunAIAction(ctx, o)`** yields the executor as a nested enumerator.
  - It pre-checks `CanExecute` for Extracted and New entries, where a refusal spends nothing.
  - It skips the pre-check for Delegating entries, whose cores own their refusals.
  - In shadow mode it logs `Debug.Log("[ActionSet] ...")` when the pick is outside the legal set.
- **`ResolveActionProvocation(actor, AoOProvokingAction, decider, outcome)`** is the one AoO loop for non-movement actions. It never ends a turn.

**Legality:**
- `Legality` gains `LegalityBar Bar` (None, Action, Condition, Compulsion, Target, Reach, Range, Ammunition, Resource, Equipment, Terrain, Ward, Charm, Other) and an overload `Refused(LegalityBar, string)`. The old `Refused(string)` maps to Other.
- `LegalityRisk` is unchanged, and so is `ActorSource {Ui, AI}` (C7).
- `AttackLegalityChecks.Charm = 16` joins `All` in step 20.

**Moves.** `MoveRequest` (built by `ForUi` or `ForAI`) and `GameManager.ExecuteMoveAction(MoveRequest, MoveAttempt)` are one executor for both sides. Its AI quirk bits live only from step 9 to step 10.

**Knowledge** (`Assets/Scripts/AI/Knowledge/`):
- **`FactKey`:** Species, CreatureType, Subtype, Size, ArmorCategory, ShieldCarried, WieldsMelee, WieldsRanged, WieldsReach, WoundBand, HpState, Condition, Caster, Mindless.
- **`KnowledgeLevel`:** the AI design doc 3.1 vocabulary (Unknown, Apparent, Inferred, Recalled, Confirmed).
- **`WoundBand`:** Unhurt at full hp; Hurt at half or more; BadlyHurt below half and above 0; Down at 0 or less.
- **Views:** `ITargetView`, `IKnowledgeProvider.GetView(observer, subject)` and `OmniscientKnowledgeProvider`, which gives true values at level Confirmed. `KnowledgeService.Provider` is settable, and batch 17 swaps it.
- **`FactQuery.Parse`** turns each shipped `TagPriority` string into a typed, exact query.
- **`AIControlFlags`** sit on `CharacterController.AIControl`. They are set at spawn from the control tags (`AI:`, `ScenarioOverride:`; re-grep). The unread `Class:` tag is dropped.

**Deleted by the end:**
- `EvaluateAttackOptions`, `EvaluateBestManeuver`, `PruneTurnUndeadTrackersForAI`, `CheckTurnUndeadProximityBreakingForMoverForAI`, `IsUndeadCharacterForAI`, `SwarmAI.ForceClearTarget`, `ClearLastKnownPosition`, `IsAtLastKnownPosition`;
- the second position store and the move quirk bits;
- both `PerformSingleAttack` methods, `ShowTurnUndeadTargetSelectionMenu` and `ShouldExcludeTargetBecauseOfCharm`;
- the "FullRound" string in `GetItemUseActionType`, and `_hasFrightfulPresenceTriggered`.

**Kept on purpose:**
- `HasActiveShieldSpellForAI`, which has a scenario caller;
- `TryControlFlamingSphereForAI`, batch 15's hook, noted on AI-029;
- `TryAIReadyCounterspell`, the Ready hook (CMB-029).

### Design choices (2026-10-29; game direction the owner may overrule)

- **C1. AI-075 alternatives come from the profile.** `AIProfile.GetOutOfReachAlternatives()` returns an ordered list. The defaults by `CombatStyle`:
  - Melee: attack another target that passes the guard, then approach.
  - Ranged: another target in range, then the preferred range, then approach.
  - Casting profiles without a usable weapon: another target in reach, then the preferred range, then total defense.

  `AiOutOfReach.Run(npc)` tries each through `RunAIAction`. Batch 14 re-weights the list; batch 22 replaces it.
- **C2. The Q2 stopgap keeps its formula.** `DefaultShouldUseRapidShot` and `DefaultShouldUseTwoWeaponFighting` become the attack entries' `AiModeValuation`, relabelled "interim rule, replaced by batch 22's evaluator". Reasons:
  - the catalog routes options and does not score them;
  - a new formula without the evaluator is churn;
  - keeping it keeps step 6 pure.
- **C3. ENC-021 moves to batch 15.** RAW NPC slots need SPL-041 first, plus a spells-known generator and an adept table.
- **C4. AI-077 is deferred to batch 14.** The routines end after any cast. What to do with the leftover move is a personality choice; the legal set lists it from step 6 on.
- **C5. Aid another, crawl and consumables get shared executors and harness steps only.** The AI's choice waits for batches 14 and 15 (AI-009 narrowed). Two behaviours that exist today stay: the charmed potion-giver and the cornered panicked total defense.
- **C6. Class abilities get short default AI rules,** like lay on hands and bardic music today:
  - turn undead when a hostile turnable undead is within 60 ft with line of effect and the check can affect the nearest;
  - rage at turn start when a foe is within reach or charge range.
- **C7. `ActorSource` stays `{Ui, AI}`.** A forced PC turn passes `AI`. The tactics README's five-value enum would collide with the live one.
- **C8. The legal set runs in shadow mode.** The PC panel keeps its own states except in the restricted menu (step 20). `ActionPanelParityTests` pins the known mismatches, and batch 22 makes the panel read the set.
- **C9. Run is deferred** to batch 18, with AI-044's 4x flight (CMB-202).

### Migration rule

This is the batch 11 and 12 strangler pattern.

- **Pure moves (steps 2, 3, 6, 9).** Every pre-existing scenario trace hash stays identical.
  - Hashes cover typed trace events, dice events and every combat-log line (ev "log" through `ScenarioHooks.CombatLog`). They do not cover `Debug.Log`.
  - So a pure move may add `Debug.Log` lines. It may not change one character of combat-log text or one die roll.
  - If a hash moves, stop and find the cause.
  - A difference kept on purpose is a named quirk bit (`MoveQuirk`, steps 9-10 only).
- **Behaviour steps.** Each changes one rule for both sides at once, and explains its trace differences in the commit body (`ScenarioTraceDiff.FirstDivergence`, docs/TESTING.md 3.5).
  - Steps 7 and 14 first check a 1:1 conversion against unchanged hashes, then flip the rule in the same commit. The body records both runs.
- **No new hashed trace event types,** so the pure steps compare against the launch baseline.
- **Executors never continue the UI.** `ActionCatalogTests` scans their source files for the UI continuations and for `IsControllable`, `ActivePC` and `IsPlayerTurn`.
- **`IsControllable` is read only** in the `*Deciders.For` factories, the PC UI resets of `StartPCTurn`, and `TryRunCompulsionTurn`'s choice between a forced turn and the restricted menu.
- **Pooled slots (CRE-046).** New per-creature state is cleared in `ResetForNewCreature` or `ResetCharacterSlotForSpawn`, with a `NpcSlotReuseTests` case each.
- **Coroutines.** Executors over callback cores (cast, maneuver) wait on a flag. Every loop re-checks `CombatOver`.

### Corrections to the input designs and maps (checked in code or books 2026-10-29)

**The maps (made at e48deb5):**
- **AI-053's `ProfileName` claim is true**, though a map called it false.
  - The initializer "Default AI" (AIProfile.cs ~:18) makes every profile's `IsNullOrWhiteSpace` guard skip. Only SwarmAI and IndiscriminateSwarmAI set the name.
  - The only reader is a `Debug.Log` (AIService ~:2171), so the fix is hash-neutral.
- **The AI spends its move action at 20 sites, not 13:**
  - AIService ~:547, 724, 819, 1040, 1082, 1129, 1174, 1255, 1322, 1384, 1606, 1680, 3104;
  - NPCTurns ~:126, 150, 1193;
  - the compulsion controllers' move helpers, with a convert fallback: Charmed ~:455, Confused ~:301, Frightened ~:334, Suggested ~:106.
- **Table 8-2's move-action AoO column is offset by two rows** in the text extract. Realigned:
  - provoke: loading a hand or light crossbow, stand up, pick up, sheathe, retrieve;
  - do not provoke: draw, ready shield, open a door, mount.

**Design A:**
- It counted 16 move sites; there are 20.
- Armor donning is Table 7-7 p.123, not "Table 7-6 p.122".
- Crossbow loading is p.115, not p.116; only the repeating crossbow is p.116.
- There is no `CreatureTags.IsCreatureType`. It is `AISpellcastingStrategist.IsCreatureType`, plus one in SpecificItemBehavior.
- There is no `PostAttackQuirkSets` row for `PcSingle`. The bits went in batch 12 step 15.
- **Caster -> SpellcasterAIProfile unconditionally** would freeze the couatl and the spirit naga. They have no spells, so they would hold at 6 squares (AI-038).
- **Brute -> Berserk without trip** still grapples aggressively and always takes a coup de grace (BerserkAIProfile.cs).
- **Total defense is a new rule,** not a pure "infra" step.
- Its step 11 bundled five concerns; they are split here.

**Design B:**
- **`HasActiveShieldSpellForAI` is not dead.** `RulesScenarios.cs` ~:7125 calls it.
- **Pages.** Panicked is PHB p.311, not p.310. Rapid Reload still provoking is explicit RAW (p.99), not a sub-reading.
- **D1's "no line of effect" is dropped.** No weapon attack tests line of effect on either side (GRID-009).
- **"Hashes cover typed trace events, not Debug.Log" is half right.** Combat-log lines are hashed too.
- **`FindBestFleeCell` is reflected** (NonPublic|Static) in GrappleDamageRulesTests ~:1244. Making it public or renaming it breaks the test.
- **Its CMB-201 ("aid another pays after its roll") is not filed.** That is ordering, not a rule.
- **The `ManeuverCost` rename is dropped as churn.**
- **Its move executor walked PCs through the AI walker** inside a pure step.
- **AI-021 as a "thin view" would move hashes.** `CharacterController._lastKnownTargetPositions` is written only when the target is seen (CC ~:11026). The tracker writes always and resets the auto-miss count. The merge is a behaviour change (step 7).

**Both designs:**
- **A +4 dodge "through `CharacterStats.Bonuses`" would do nothing.** No AC formula reads `BonusTarget.ArmorClass`. AC reads `DodgeACBonus` (CharacterStats ~:4287), which drops to 0 when Dex is denied and is removed by Dex-denying attacks (CC ~:8347). A named field is needed.
- **`ActorSource` collides twice.** The README's `ActorSource {PlayerInput, ...}` collides with the live enum (IAttackDecider.cs ~:6). In the harness, the class `Tests.Scenarios.ActorSource` (ScenarioModel.cs ~:50) hides it, so harness code writes `global::ActorSource`.

**The issue files.** AI-040 and CRE-039 cite "MM p.307". The pages are MM p.69, p.306 and p.315.

**Confirmed:**
- The charge legality (`CanCharge`, over the private `TryBuildChargePath` overloads).
- 3bbf47d's mover-taking path helpers; the move and 5-foot-step members were re-grepped there.
- AI-076, AI-018's 17 sites, CMB-200, the turn-undead picker, and the PC-only `ActivateRage`.
- Three uncalled members AI-053 misses: `IsUndeadCharacterForAI`, `EvaluateBestManeuver`, `TryAIReadyCounterspell`.

## Steps

Every C# step runs `bash tools/compile_check.sh` and expects exit=0, errors=0, warnings=507; a jump is a regression.

Each step keeps the docs true in the same commit:
- **Issues.** Delete fixed entries in `docs/issues/<PREFIX>.md`; update the counts and Top issues in `docs/KNOWN_ISSUES.md`.
- **System docs.** Update `docs/systems/AI_ACTION_COVERAGE.md` (from step 3 on, the catalog's status column is the source), PARITY section 5, `docs/systems/AI.md` and the tactics README where an entry lands.
- **Rules.** Record each applied D-reading and design choice in RULES_COVERAGE 4.1, dated 2026-10-29.
- **Architecture.** Update ARCHITECTURE or its part file and DEVELOPMENT_RECIPES where structure or a recipe changes.
- **Test pins.** Remove only the waivers, `ExpectKnown` pins and `static-suites.json` entries the step fixes.

House rules for every step:
- New `.cs` files get their Unity-generated `.meta`; never hand-write one.
- Grep `Assets/Scripts/Tests` before renaming or deleting a member (TST-006).
- Re-grep any movement, charge or 5-foot-step member before relying on it; since 3bbf47d the path and barrier helpers take the mover.
- Write "not verified in Play mode" in commit bodies for anything only read.
- New scenarios go in `Tests/Scenarios/Catalog/ActionCatalogScenarios.cs` unless a subsystem file fits better. Make `-ui`/`-ai` twins when both sides can act; from step 6 on, a Scripted twin can use `Step.Action`.

**TEST BUDGET rule for every step:**
- Run the new or changed scenarios and the touched subsystem's scenarios, seeds 1-3, in a fresh Play session (`QueueFresh`), plus the static suites covering the touched code.
- A *broad* step also runs `smoke/*` seeds 1-3.
- A pure move compares every hash with the baseline or the previous step's run.
- The full sweep and the soak run only in the two regression steps.
- Never run scenarios in a Play session that ran static suites.

### 1. File the batch 13 findings and refresh stale entries (`b13-file-findings`)

IDs: new AI-076, AI-077, AI-078, AI-079, CMB-200, CMB-201, CMB-202, CORE-040, CRE-076, CRE-077, UI-054, and ENC-024 only if confirmed. Refreshes AI-018, AI-053, AI-021, AI-010, AI-040, AI-041, CRE-039, AI-054, ENC-021, AI-044, AI-009, CMB-032, CMB-100, ITM-005, AI-073. Docs-only commit.

Before filing:
- Re-verify each item at HEAD by grepping the members.
- Check each rule page in `C:/Users/Hoyty/AppData/Local/Temp/dnd_books/{PHB,DMG,MM}/pNNN.txt` (pdf page = printed + 1; summarize, never quote).
- Use the next IDs from the KNOWN_ISSUES table, and bump Next ID and the counts.

1. **New entries:**
   - **AI-076 (Med, player-visible).** AI moves are paid after the walk at 20 sites, and a pathless move still pays (`MoveCharacterAlongComputedPath` ~:1328 returns without `Refused`). Fix: steps 9-10.
   - **AI-077 (Med).** Every AI routine ends after any cast, song, maintain, Lay on Hands or breath. That wastes a Quickened cast's standard action and the move. Fix: batch 14.
   - **AI-078 (Low).** Loose fact matching: a two-way substring `TargetHasMatchingTag`; "HP State: Staggered" weighted as wounded in seven profiles (staggered is nonlethal damage equal to current hp, PHB p.313); `IndexOf` in `AISpellcastingStrategist.IsCreatureType`. Fix: step 7.
   - **AI-079 (Med, player-visible).** No NPC rages: `ActivateRage` (CC ~:16093) has only the PC caller. Fix: step 16.
   - **CMB-200 (Low, player-visible).** The Fighting Defensively and natural-attack buttons bypass `RunAttackSequence` (`CombatFlowService.PerformSingleAttack`), so they cannot go on into a full attack (PHB p.143). Fix: step 17. Name this exception in CLAUDE.md's attack-math row until then.
   - **CMB-201 (Low, player-visible).** Turn undead lets the player pick whom to turn, and rolls before paying. PHB p.159 turns the closest first. Fix: step 16.
   - **CMB-202 (Low, player-visible).** No Run action for either side (PHB p.144, the Run feat). Fix: batch 18.
   - **CORE-040 (Med).** `NPCAttackWallForAI` and `NPCBreakWallForAI` (CombatActions ~:2808, ~:2828) end in `AfterAttackDelay`, which can advance the turn inside the NPC coroutine (CORE-012). Split from AI-054. Fix: step 13.
   - **CRE-076 (Med, player-visible).** Gaze attacks have no gaze rules (MM p.309-310), and the naga's gaze maps to Fear. Fix: batch 16.
   - **CRE-077 (Low).** `BreathWeaponDefinition` (NPCDatabase.cs ~:291) has no failed-save effect beyond damage; verify first. Fix: batch 16.
   - **UI-054 (Low, player-visible).** The hover tooltip shows enemies' true HP state and conditions (`CharacterHoverTooltipUI.BuildTooltipText`). Fix: batch 17.
   - **ENC-024 (Low),** only if the code shows it: encounter filters match tags by substring.
2. **Refreshes:**
   - **AI-018:** the 17 anchors.
   - **AI-053:** `ProfileName` is true; swap `HasActiveShieldSpellForAI` out of the uncalled list for `IsUndeadCharacterForAI` and `EvaluateBestManeuver`; move `TryControlFlamingSphereForAI` and `NPCTemplateAIConfigurator` to AI-029; name steps 2, 7 and 19.
   - **AI-021:** the stores hold different data; the merge is step 7.
   - **AI-010:** what the lich definition (NPCDatabase_L) carries for its fear aura (MM p.166-167). Step 19 runs its frightful-presence data under the shared rules.
   - **AI-040, CRE-039:** pages MM p.69, p.306, p.315.
   - **AI-041:** the D12 facts.
   - **AI-054:** the Wall of Ice part moves to CORE-040; steps 12, 13, 14 and 16 take the rest.
   - **ENC-021:** batch 15 (C3).
   - **AI-044:** the random path is RAW, but out of scope.
   - **Pointers on the rest:** AI-009 (step 14), CMB-032 (hand and light loads provoke), CMB-100 (step 20), ITM-005 (D7), AI-073 (steps 5, 15).
3. **Pointers.**
   - Point PARITY section 5 step 10 at this design.
   - In the tactics README, note C7 and that the maneuver core is `GameManager.ExecuteManeuver` (ManeuverCore ~:261).

Commit subject: `File the findings from mapping batch 13`, with the new IDs in the body.

TEST BUDGET: none. Check the counts and Next IDs, and that `git diff --stat` shows only `docs/` and `CLAUDE.md`.

### 2. AI groundwork: null-safe logs, profile lifetimes, dead members (`b13-ai-groundwork`)

IDs: AI-018, AI-017, AI-053 (ProfileName and uncalled members), AI-027, AI-021 (dead members only). Kind infra. **Pure move.**

1. **AI-018.** Change the 17 `CombatUI.` dereferences to `CombatUI?.`:
   - AIService ~:131, 132, 825, 1325, 1327, 1385, 1607;
   - NPCTurns ~:42-45, 55, 99, 127, 151, 1053, 1059.
2. **AI-017.**
   - Add `GameManager.CreateRuntimeAIProfile<T>()`: `CreateInstance`, then a new virtual `AIProfile.ApplyRuntimeDefaults()`, then `TrackRuntimeAIProfile` (NPCSetup ~:313).
   - Move each profile's `OnEnable` tuning writes into `ApplyRuntimeDefaults`, with the same values.
   - Route the spawn overrides and the swarm summons (GameManager.SpellCasting.cs ~:633, ~:773) through the factory.
   - Destroy replaced instances. Destroy tracked summon profiles in `ResetCombatStateForNextEncounter` and old profiles on slot reuse.
   - Keep `_npcAIBehaviors` parallel (AI-015).
3. **AI-053.**
   - Set `ProfileName` unconditionally in `ApplyRuntimeDefaults`.
   - Delete `PruneTurnUndeadTrackersForAI`, `CheckTurnUndeadProximityBreakingForMoverForAI`, `IsUndeadCharacterForAI` and `SwarmAI.ForceClearTarget`.
   - Narrow the entry.
4. **AI-027.** Delete `EvaluateAttackOptions` (~:2561) and `EvaluateBestManeuver` (~:3965). `SelectBestAction`'s Retreat result stays for batch 18.
5. **AI-021, dead members only.** Delete `ClearLastKnownPosition` (CC ~:11046) and `LastKnownPositionTracker.IsAtLastKnownPosition` (~:97).
6. **Stale comments (C7).** Correct the three comments that say batch 13 adds or passes a forced `ActorSource` (`Combat/Core/IAttackDecider.cs` ~:5 and ~:181, `Combat/Maneuvers/ManeuverCore.cs` ~:320): the enum stays `{Ui, AI}` and a forced turn passes `AI`.

Tests:
- AIProfileFrameworkTests: runtime profiles get their name and defaults, and `.asset` values survive.
- NpcSlotReuseTests: a reused slot destroys its old profile.

TEST BUDGET: `smoke/*`, `rules/dragon-ai-*`, `rules/ai-*`, `rules/invisible-pinpoint-*`, `rules/slot-reuse-*`, `rules/large-encounter-*` (seeds 1-3); AIProfileFrameworkTests, LastKnownPositionTrackerTests, NpcSlotReuseTests. Every hash equals the baseline.

### 3. The catalog skeleton over the shared cores (`b13-catalog-skeleton`)

IDs: preparation for every catalog step. Kind infra. **Pure move.**

1. **Types.** Create the End state types in `Combat/Actions/`. Reuse `ActionCostType` in place; do not rename `ManeuverCost`.
2. **Legality.** Add `LegalityBar` and `Refused(LegalityBar, string)`. No reason string changes.
3. **Entries.** One entry per action in scope, with its Table 8-2 cost and provocation, `RulesRef`, roles, status and issue IDs.
   - **Delegating executors over the finished cores:**
     - attack and full attack -> `RunAttackSequence`;
     - cast -> `ExecuteCast`;
     - maneuvers -> `ExecuteManeuver`;
     - charge -> `CanCharge` and `ExecuteCharge(ChargeRequest, ChargeOutcome)` (ChargeCore ~:199, ~:331). `CanCharge`'s path test is `TryBuildChargePath`, the owner's one charge legality test.
     - overrun -> `RunOverrunMove`;
     - move and withdraw -> today's executors, until step 9;
     - 5-foot step -> `ExecuteFiveFootStep` (GameManager ~:7086) and `TryTakeFiveFootStepForAI` (~:11337);
     - stand up -> `ResolveStandUpFromProne`;
     - smite, flurry, lay on hands, wild shape, bardic music, maintain concentration, deliver held charge -> their current entry points.
   - **Every other entry gets its real status and issue:**
     - total defense: Missing, CMB-179;
     - reload: PcOnly, AI-057;
     - aid another, wake, crawl, drop prone, pick up, drop, use item, turn undead, rage, domain power, attack wall: PcOnly, AI-054;
     - draw, sheathe, ready shield, don armor: Missing, ITM-069;
     - breath, spittle, engulf, ranged special: AiOnly, CRE-039;
     - run: Missing, CMB-202;
     - ready and delay: Missing, CMB-029;
     - gaze: OutOfScope, CRE-076.
4. **Runner.** Add `_Core/GameManager.ActionRunner.cs` with `RunPlayerAction`, `RunAIAction` and `ResolveActionProvocation`. Only tests call them yet.
5. **Docs.** A combat-actions part in `docs/architecture/`, and the recipe "Add an action". `AI_ACTION_COVERAGE.md` names the catalog as the status source.

Tests: a new static suite, ActionCatalogTests:
- one entry per `ActionIds` constant;
- every entry has a cost, a provocation and a `RulesRef`;
- a golden Table 8-2 manifest;
- every `SpecialAttackType` has an entry that agrees with `ManeuverActionCost.For`;
- `ActionCosts.From` maps every `CastActionCost`;
- the executor source scan.

TEST BUDGET: ActionCatalogTests, AttackLegalityTests, CastCoreStageOrderTests, GrappleDamageRulesTests; `smoke/*` seeds 1-3. All hashes unchanged.

### 4. Total defense for both sides, and the cornered panicked creature takes it (`b13-total-defense`)

IDs: CMB-179; D2. Behaviour change.

1. **AC.** Add `CharacterStats.TotalDefenseACBonus` and sum it inside `DodgeACBonus` (~:4287). That reaches `ArmorClass`, `TouchArmorClass`, `ArmorClassWithoutDexAndDodge` and the Dex-denying attack (CC ~:8347). Not `Bonuses`: no AC formula reads `BonusTarget.ArmorClass`.
2. **The executor** (`Combat/Actions/Executors/DefenseExecutors.cs`, status New).
   - `CanExecute` needs a standard action (Bar Action), and asserts no fighting defensively or Combat Expertise this turn.
   - `Execute` pays with `CommitStandardAction`, sets `CharacterController.IsInTotalDefense` and the +4, and logs through a new `CombatLogHelper` method.
3. **Lifetime.** `BeginCombatantTurn` clears both, so they also end on a skipped turn. `ResetForNewCreature` clears both too.
4. **AoOs.** `ThreatSystem.CanMakeAoO` (~:339) refuses while `IsInTotalDefense` is set.
5. **The PC side.** A code-built "Total Defense" button in `ActionButtonPanel` calls `RunPlayerAction` (UiAfter ShowChoices).
6. **The AI side.** `FrightenedBehaviorController.ExecuteDecision`'s cornered Panicked branch runs `RunAIAction(total_defense)`.
7. **Docs.** Record D2; delete CMB-179.

Tests:
- Scenarios:
  - `rules/total-defense-ui`;
  - `rules/total-defense-no-aoo-ui`;
  - `rules/total-defense-ends-at-turn-start`;
  - `rules/total-defense-cornered-panicked-ai`.
- A new TotalDefenseRulesTests: +4 dodge, lost when Dex is denied, no AoO, cleared at turn start and on slot reuse.

TEST BUDGET: the new scenarios, `rules/panicked-*`, `rules/frightened-*`, `rules/movement-aoo-*`, `rules/fight-defensively-*` (seeds 1-3); ActionCatalogTests, TotalDefenseRulesTests, NpcSlotReuseTests.

### 5. No AI attack against a target it cannot reach or hit (`b13-attack-reach-guard`)

IDs: AI-075, AI-045, AI-039; AI-073 narrowed; D1, C1, owner ruling 2026-10-29. Behaviour change; *broad* (every AI attack declaration).

1. **The guard.** Add `AI/AiAttackGuard.cs` with `Legality Check(attacker, target, AttackIntent intent)`. It is pure. It returns:
   - `AttackLegality.CanDirectlyAttack(attacker, target, kind, intent)` (AttackLegality ~:61, whose Reach check is `IsInReachOrRange`);
   - then the ammunition test (`Inventory.HasAmmo` ~:1014, as `CombatFlowService.CheckAmmoAvailable` ~:796): Bar Ammunition;
   - then loaded-or-reloadable (`CanReloadEquippedWeapon`, with a move or free action left).

   The intent comes from `AttackIntent.ForAI`. A ward risk is allowed.
2. **Every AI attack declaration asks the guard first:**
   - `NPCPerformAttackForAI`;
   - `ChooseNextAttackStepForAI`;
   - `SelectAdaptiveFullAttackTarget` and `ShouldSwitchTargetsMidFullAttack`;
   - each routine's melee and ranged branches, the compulsion controllers' included.

   `SelectBestTarget` gets a `Func<CharacterController,bool>` filter. The attack maneuvers already need reach (batch 12 step 12).
3. **The AI-075 cause.** `ExecuteRangedKiterTurn` (~:1342) stops widening the attack range with `GetBestAvailableSpellRangeInSquares`.
4. **Alternatives (C1).** Add `AIProfile.GetOutOfReachAlternatives()` and `AiOutOfReach.Run(npc)`. It tries the attack, move, withdraw and total_defense entries in order through `RunAIAction`, and stops at the first one performed.
5. **AI-045.** After an approach move, check the guard again and attack with the remaining standard action.
6. **AI-039.** A kiter retreats only if it has a guard-passing ranged attack, or a castable ranged spell.
7. **AI-073, narrowed.** An archer without ammunition stops declaring shots. Drawing a melee weapon is step 15.
8. **Docs.** Record D1, C1 and the owner direction in 4.1 and `docs/systems/AI.md`. Delete AI-075, AI-045 and AI-039.

Tests: new scenarios:
- `rules/ai-attack-only-in-reach-npc` and `-party`: the AI-075 setup, an evoker with nothing castable, one foe adjacent and one far. It never attacks the far one.
- `rules/ai-kiter-approach-then-attack`;
- `rules/ai-kiter-no-ranged-no-retreat`;
- `rules/ai-reach-weapon-dead-zone`;
- `rules/ai-archer-no-ammo-no-shot`.

TEST BUDGET: the new scenarios, `rules/ai-*`, `rules/ranged-full-attack-ai`, `rules/npc-throws-*`, `rules/sphere-blocks-*`, `rules/hide-from-*`, `rules/charm-*`, `rules/total-defense-*` (seeds 1-3); AttackLegalityTests, AIProfileFrameworkTests; `smoke/*` seeds 1-3.

### 6. The AI runs the catalog, and the legal set in shadow mode (`b13-ai-runs-catalog`)

IDs: preparation for AI-054, CMB-100 and batch 22. Kind infra. **Pure move**; *broad*.

1. **Routing.** Route each AI routine's action calls through `RunAIAction` with the matching entry: attack, full attack, cast, maneuver, charge, overrun, move, withdraw, 5-foot step, stand up, breath, ranged special, engulf, lay on hands, bardic music. Delegating entries skip the pre-check, so no new refusal changes a turn.
2. **The legal set.** Build `ActorActionSet` from today's legality through the `ActionLegality` adapters, with the compulsion filter. `RunAIAction` logs `Debug.Log("[ActionSet] <actor> picked <id> outside the legal set: <reason>")` on a disagreement; that line is not hashed.
3. **Q2 (C2).** Move the stopgap formula behind the attack entries' `AiModeValuation`, unchanged and relabelled. `AttackIntent.ForAI` (~:165) reads it there.
4. **The harness.**
   - Add `Step.Action(actorName, round, actionId, target/cell/item)`. A Scripted or AI-run actor runs it through `RunAIAction`, a Ui actor through `RunPlayerAction`.
   - Write `global::ActorSource` inside `Tests.Scenarios`.
   - Remove from `Step.Problem` (ScenarioSteps.cs ~:372) each gap now shared.
5. **ActionPanelParityTests** (a new static suite).
   - For fixture actors (fighter, wizard, prone, grappled, pinned, nauseated, disabled, wild-shaped druid, frightened, charmed), it compares `ComputeActionButtonStates` (ActionButtonPanel ~:196) with `set.Allows(id)`.
   - Known mismatches sit in a table, each tagged with the step that resolves it or with one new UI issue (the next free UI ID).

TEST BUDGET: `smoke/*`, `rules/ai-*`, `rules/dragon-ai-*`, `rules/charge-*`, `rules/prone-*`, `rules/overrun-*`, `rules/rapid-shot-*`, `rules/ranged-full-attack-*`, `rules/npc-cast-*`, `rules/bard-inspire-courage-ai`, `rules/paladin-lay-*` (seeds 1-3); ActionCatalogTests, ActionPanelParityTests, AIProfileFrameworkTests. All hashes unchanged.

### 7. Tags v2: typed facts behind an omniscient provider, wound bands, one position store (`b13-knowledge-facts`)

IDs: AI-078, AI-053 (armor priority and flanking), AI-021. Behaviour change; *broad* (every AI target choice).

**Pass 1 (pure; the hashes are checked and recorded before pass 2):**
1. Add `AI/Knowledge/` (End state). Target scoring reads `KnowledgeService.Provider.GetView(self, target)`.
2. `FactQuery.Parse` converts every shipped `TagPriority` string 1:1, keeping today's semantics: substring where today is substring, and Staggered as Staggered.
3. Translate `AIControlFlags` at spawn and switch their readers. Clear the flags on slot reuse.

**Pass 2 (the flip):**
1. **Exact matching (AI-078).** "Elf" no longer matches "Half-Elf". `IsCreatureType` reads the typed fact, with the type spellings normalized at spawn. The rule that mindless counts as Undead goes.
2. **Wound bands (AI-078).** "HP State: Staggered" weights become `WoundBand.BadlyHurt`, which is what the authors meant. Dying and Disabled stay HP states.
3. **AI-053.** Armor-priority targeting becomes reachable. The flanking term tests `CombatUtils.IsFlanking`, not threat.
4. **AI-021: one store, the tracker.**
   - The CC store's readers (AIService ~:2319-2323, and `TryResolveLastKnownPositionAutoMiss` CC ~:7740) read the tracker.
   - Its writers (CC ~:7730, GM ~:3221, AIService ~:2319) record only a real observation (seen, or pinpointed), as the tracker's callers do (GM ~:3224, AIService ~:1224, ~:1942).
   - Clear the tracker for continuing party members in `ResetCombatStateForNextEncounter`.
5. **Tags.** Drop the unread `Class:` tag.
6. **Docs.** Delete AI-078 and AI-021, and narrow AI-053. `docs/systems/AI.md`: batch 17 swaps the provider; batch 14 scores the facts.

Tests:
- New scenarios:
  - `rules/ai-wound-band-priority`;
  - `rules/ai-exact-race-match`;
  - `rules/ai-armor-priority-reachable`;
  - `rules/ai-flanking-priority`.
- Update `rules/invisible-pinpoint-*` if the merge moves them, and explain why.
- A new KnowledgeFactsTests: every shipped string parses, the bands are right, and the provider can be swapped.

TEST BUDGET: the new scenarios, `rules/ai-*`, `rules/invisible-pinpoint-*`, `rules/dragon-ai-*`, `rules/large-encounter-*` (seeds 1-3); AIProfileFrameworkTests, LastKnownPositionTrackerTests, NpcSlotReuseTests, KnowledgeFactsTests; `smoke/*` seeds 1-3.

### 8. Every archetype gets a profile, and melee casters cast (`b13-archetype-profiles`)

IDs: AI-004, AI-003, AI-002; owner question Q4 (default applied). Behaviour change; *broad*.

1. **AI-004.** `BuildRuntimeAIProfile` (NPCSetup ~:1096) gets two cases, built through `CreateRuntimeAIProfile<T>`.
   - **Brute -> a new plain `BruteAIProfile`** (the Q4 default). Melee, high aggression, `Maneuvers.UsePowerAttack` set as `UndeadBruteAIProfile` sets it (unread until AI-026 is fixed, so no Power Attack yet). No trip, no forced coup de grace, no aggressive grapple.
   - **Caster -> `SpellcasterAIProfile` only with spells** (`NpcClassGrantsSpells` or a spell list), else `BruteAIProfile`. The couatl and the spirit naga get Brute until batch 16 gives them spell-likes.
2. **AI-003.** `NPCAIBehavior.Ranged` runs the ranged routine and shoots (`RoutesToRangedTurn`), with or without a profile.
3. **AI-002.** Add `AIProfile.ShouldTryCastBeforeMelee(self)`. It defaults to true when the creature has a castable spell with a combat effect (not Utility or Divination, by `SpellCategoryClassifier`). The Aggressive and DefensiveMelee routines then try `TryExecuteSpellcastAction` first. AI-069 stays open; AI-077 is deferred (C4).
4. **Docs.** Delete AI-004, AI-003 and AI-002. Record the Q4 default in 4.1.

Tests:
- New scenarios:
  - `rules/brute-profile-melee-ai`;
  - `rules/caster-archetype-without-spells-fights-ai`;
  - `rules/ranged-behaviour-shoots-ai`;
  - `rules/melee-caster-casts-first-ai`.
- AIProfileFrameworkTests: every archetype maps to a profile.

TEST BUDGET: the new scenarios, `rules/ai-*`, `rules/npc-cast-*`, `rules/dragon-ai-*`, `rules/large-encounter-*` (seeds 1-3); AIProfileFrameworkTests, NpcSlotReuseTests; `smoke/*` seeds 1-3.

### 9. One move executor for both sides (`b13-move-executor`)

IDs: preparation for AI-076. Kind infra. **Pure move**; *broad* (every move).

First re-grep these signatures; since 3bbf47d they take the mover:
- `MovementService.ExecuteMovement(mover, path, secondsPerStep, markAsMoved, lastStepIsDestination, kind)` (~:350);
- `MovementService.FindPath(mover, ...)` and `CanStartMovement`;
- `ExecutePathWithMovementAoOs(mover, path, provokedAoOs, secondsPerStep, aooLogLabel, outcome, kind)` (CombatActions ~:859);
- the `SquareGridUtils` path and barrier helpers.

1. **`Combat/Actions/MoveRequest.cs`.**
   - The request: `MoveRequest { Mover, Kind, Path or Destination, SecondsPerStep, Label, AllowConvertStandard, Spend, Quirks }`.
   - `MoveSpend {MoveAction, None, ExecutorOwned}`. `ExecutorOwned` is the withdraw's own spend; `None` is a caller that pays otherwise, such as the flee run or a forced move.
   - `[Flags] MoveQuirk {None, AiSpendsAfterWalk = 1, AiSpendsWithoutPath = 2, AiSegmentLoop = 4}`. `ForAI` sets all three; `ForUi` sets none.
   - `AllowConvertStandard` is set by `ForUi`, and by the compulsion controllers, which convert a standard action today.
2. **`GameManager.ExecuteMoveAction(MoveRequest, MoveAttempt)`** (partial `GameManager.MoveAction.cs`) runs, in order:
   1. `CanStartMovement`;
   2. the spend, before the walk unless `AiSpendsAfterWalk`;
   3. the walk: the PC per-step loop, or `ExecutePathWithMovementAoOs` under `AiSegmentLoop`;
   4. the after-walk spend under the quirk, also with no path under `AiSpendsWithoutPath`.

   It does no UI continuation.
3. **The PC side.** `ExecuteMovement(pc, path, isWithdraw, provokedAoOs)` (CombatActions ~:1080) builds `ForUi`. Its tail becomes the move entry's `UiAfter`.
4. **The AI side.** `MoveCharacterAlongComputedPath` (~:1328) and `...Withdraw` (~:1271) become `ForAI` builders.
   - Classify all 20 spend sites (Corrections). A site that pays after the walk passes `MoveSpend.MoveAction`, drops its own `UseMoveAction()` and keeps its log line after the call, word for word.
   - A site that reads the economy between walk and spend passes `None`. List such sites in the body.
5. **The catalog.** The move and withdraw entries now run the executor (status Extracted).
6. **The walkers.** If the PC per-step walk and `ExecutePathWithMovementAoOs` differ in a rule (AoO order, budget, step hook), file it as GRID-028. Note mere duplication in `docs/architecture/combat-and-grid.md`.

Tests: MovementLegalityTests covers the builder bits and the site classification.

TEST BUDGET: MovementLegalityTests, ActionCatalogTests; seeds 1-3:
- `rules/movement-*`, `rules/ai-no-5ft-then-move`, `rules/five-foot-*`;
- `rules/restricted-withdraw-*`, `rules/blinded-no-withdraw-ai`;
- `rules/wall-of-force-path-cost`, `rules/shared-square-*`, `rules/nauseated-move-*`, `rules/prone-*`;
- `rules/panicked-*`, `rules/frightened-*`, `rules/charge-*`, and the 3bbf47d `ChargeTerrainScenarios`;
- `smoke/*`.

Every hash unchanged.

### 10. AI move actions are spent when the move starts (`b13-ai-move-pays-first`)

IDs: AI-076; D3. Behaviour change; *broad* (every AI move).

1. **The flip.** Delete `AiSpendsAfterWalk` and `AiSpendsWithoutPath`. Both sides pay before the first step.
   - No path, or a `CanStartMovement` refusal, sets `MoveAttempt.Refused` and spends nothing.
   - A walk stopped midway stays spent.
2. **The callers.** Each of the 20 sites' `Refused` branch goes on to its next choice, with no "moves toward" log and no spend.
3. **`AiSegmentLoop`** stays, unless step 9 found no rules difference; then delete it too and say so in the body.
4. **Docs.** Record D3; delete AI-076.

Tests:
- New scenarios:
  - `rules/ai-move-no-path-keeps-action-ai`;
  - `rules/ai-move-spent-when-aoo-stops-it-ai`.
- MovementLegalityTests: a pathless AI move spends nothing.

TEST BUDGET: the new scenarios, MovementLegalityTests, `rules/movement-*`, `rules/ai-*`, `rules/restricted-withdraw-*`, `rules/frightened-*`, `rules/panicked-*`, `rules/charge-*`, `rules/nauseated-move-*` (seeds 1-3); `smoke/*` seeds 1-3.

### 11. Midpoint full regression (`regression-midpoint`)

Kind regression. Run, through the Unity MCP:
1. the static runner in edit mode, then in Play mode, against `tools/tests/static-suites.json`;
2. `ScenarioHarness.QueueFresh("rules/*,smoke/*", "1-5", "")`;
3. the soak `QueueFresh("soak/*", "1-50", "perSession=10")`.

Record the run in `docs/TESTING.md` 3.2. Compare the soak with batch 12's follow-up regression (b1759ad); 3bbf47d lies in between.

The expected, explained shifts are the Behaviour changes rows for steps 4-10, plus charges that respect terrain (3bbf47d).

Count the distinct `[ActionSet]` lines in the logs. Each is fixed in a later step of this batch or filed. Change no game code; file any regression as a new issue.

### 12. Shared posture and reload executors (`b13-posture-and-reload`)

IDs: AI-057, CMB-074, CMB-032 (reload part), AI-054 (crawl and drop prone); D4, D5. Behaviour change.

1. **Reload (D4).**
   - Extract `CanReloadEquippedWeapon` (GameManager ~:7590) and `ExecuteReload` (~:7644) into the reload executor.
   - Its cost comes through `CostFor`, by weapon and Rapid Reload.
   - It provokes through `ResolveActionProvocation(LoadCrossbow)`.
   - Both sides use it. Update `rules/crossbow-full-attack-reload`.
2. **AI-057.** `NPCPerformAttack` reloads through the entry, then attacks with its standard action instead of `yield break`.
3. **Posture executors (D5).** Extract crawl (`ExecuteCrawl` ~:7499 with `IsValidCrawlDestination`) and drop prone from the PC-bound code (GameManager ~:7054-7388).
   - The PC buttons call `RunPlayerAction`.
   - Crawl provokes as movement.
   - The AI's crawl choice is batch 14.
4. **CMB-074.** In the confused path (`ExecuteConfusedTurnDecisionForAI` and the PC forced turn), a prone creature whose roll lets it act runs stand_up first.
5. **Docs.** Delete AI-057 and CMB-074; narrow CMB-032 and AI-054; record D4 and D5.

Tests: new scenarios:
- `rules/reload-provokes-ui`, `rules/reload-provokes-ai`;
- `rules/npc-reload-then-fire-ai`;
- `rules/rapid-reload-free-ai`;
- `rules/confused-prone-stands-ai`, `rules/confused-prone-stands-ui`;
- `rules/crawl-ui`, `rules/crawl-ai`;
- `rules/drop-prone-ui`.

TEST BUDGET: the new scenarios, `rules/prone-*`, `rules/crossbow-*`, `rules/movement-aoo-*`, `rules/ranged-full-attack-*` (seeds 1-3); ActionCatalogTests.

### 13. Aid another, waking an ally and wall attacks as shared executors (`b13-aid-another-and-walls`)

IDs: AI-054 (aid another and wake), CORE-040; D6. Behaviour change.

1. **Aid another (D6).** Extract `ExecuteAidAnother` and `ExecuteAidAnotherWakeSleepingAlly` (SupportActions ~:817-906) into executors.
   - Each pays its standard action, then rolls.
   - It provokes only when the aided action would.
   - The PC tails (`AfterAttackDelay` 0.9 s, `ShowActionChoices`) become `UiAfter`.
   - The AI reaches both executors; choosing them is batch 14.
2. **CORE-040.** `NPCAttackWallForAI` and `NPCBreakWallForAI` (CombatActions ~:2808, ~:2828) run the attack_wall executor through `RunAIAction`. No `AfterAttackDelay` runs inside the NPC coroutine. The PC wall buttons call `RunPlayerAction`.
3. **Docs.** Delete CORE-040; narrow AI-054; record D6.

Tests: new scenarios:
- `rules/aid-another-ui`, `rules/aid-another-ai`;
- `rules/wake-sleeping-ally-ui`, `rules/wake-sleeping-ally-ai`;
- `rules/npc-wall-of-ice-attack-ai`: one turn advance only.

TEST BUDGET: the new scenarios, `rules/cast-ui-sleep*`, `rules/sleep-*`, `rules/wall-of-force-path-cost`, `rules/five-foot-wall`, `rules/turn-start-*` (seeds 1-3); ActionCatalogTests.

### 14. Retrieve and use items at their RAW cost for both sides (`b13-item-actions`)

IDs: ITM-005, ITM-085, ITM-086, ITM-070, CMB-032 (scroll and wand), AI-054 (items), AI-009 narrowed; D7. Behaviour change.

First re-check DMG p.229 in the PDF.

**Extract** (checked against unchanged hashes, recorded in the body):
1. `TryUseConsumableFromInventory` loses its `IsPlayerTurn && ActivePC == actor` gate and its `EndActivePCTurn` tail, and becomes the use_item executor. InventoryUI (~:677) calls `RunPlayerAction`.
2. `GetItemUseActionType` returns `ActionCostType` instead of "FullRound". `CanUseItemManipulationAction` and `ConsumeItemManipulationAction` compare enums.
3. Update the pin in `CastPipelineScenarios.cs` ~:1148.

**Change (D7):**
1. **Retrieve.** A move action that provokes. The item then stays in hand (a new held-item field, cleared on slot reuse).
2. **Potions and oils.** A standard action that provokes.
3. **Scrolls.** The casting time, at least a standard action, provoking as casting. A free-action scroll is free and does not provoke.
4. **Wands.** A standard action or the casting time, with no AoO. The wand must be held.
   - A wand in easy reach is drawn; until step 15's draw entry, it goes through retrieve.
   - Wire `AoOProvokingAction.UseScroll` and `UseWand` (CMB-032).
5. **Administering a potion** to an unconscious creature is a full-round action that provokes. `CharmedBehaviorController.TryUseHealingConsumableOnCaster` uses it only for an unconscious charmer (ITM-070).
6. **The AI** gets the executor and harness steps. Its consumable choice is batch 15.
7. **Docs.**
   - Delete ITM-005, ITM-085, ITM-086 and ITM-070.
   - Delete CMB-032 if nothing of it is left.
   - Narrow AI-054 and AI-009.
   - Record D7.

Tests: new scenarios:
- `rules/potion-drink-provokes-ui`, `rules/potion-drink-provokes-ai`;
- `rules/retrieve-then-drink-ui`;
- `rules/scroll-casting-time-ui`, `rules/scroll-casting-time-ai`;
- `rules/scroll-feather-fall-free-ui`;
- `rules/wand-no-aoo-ui`, `rules/wand-no-aoo-ai`;
- `rules/administer-potion-unconscious-charmer-ai`.

TEST BUDGET: the new scenarios, `rules/held-charge-wand-keeps`, `rules/stale-cast-state-wand-asf`, the `CastPipelineScenarios.cs` scenarios, `rules/combat-end-charmed-*`, `rules/charm-*` (seeds 1-3); ActionCatalogTests and the static suites that call `TryUseConsumableFromInventory`.

### 15. Drawing, stowing, readying and dropping cost their RAW actions for everyone (`b13-equipment-actions`)

IDs: ITM-069, AI-073, AI-074, CMB-181, CHR-021 (Quick Draw); new ITM-089; D8, owner question Q5 (default applied). Behaviour change; *broad* (every weapon change).

1. **Entries (D8):** draw, sheathe, ready or loose shield, pick up and drop, for both sides. The draw entry:
   - is free with a regular move at BAB +1, once per move action;
   - is free with Quick Draw (`FeatManager.HasQuickDraw`);
   - draws two weapons for Two-Weapon Fighting.

   The wand draw joins it.
2. **Thrown weapons.** Each next thrown weapon is a draw (`ResolveThrownWeaponAfterAttack`). Without Quick Draw, a full attack throws once per weapon in hand (CHR-021, Quick Draw part).
3. **The inventory (Q5 default).**
   - The character sheet's InventoryUI calls these entries at their RAW costs.
   - Armor is locked in combat, with the ITM-089 reason.
   - Every creature uses the same entries (ITM-069).
4. **AI-073.** An archer without ammunition draws its melee weapon through the draw entry (`TryEnsureWeaponFallback`, AIProfile ~:188).
5. **AI-074.** The interim chooser (C2) never throws a creature's last melee weapon unless it has another weapon to draw. This is a design default; batch 22 replaces it.
6. **CMB-181.** A panicked creature, PC or NPC, drops what it holds (free) at the start of its panicked turn, in the shared flee controller (PHB p.311).
7. **File ITM-089 (Low, player-visible).** No multi-round actions, so no armor changes in combat (Table 7-7 p.123).
8. **Docs.** Delete ITM-069, AI-073, AI-074 and CMB-181; narrow CHR-021; record D8 and the Q5 default.

Tests: new scenarios:
- `rules/draw-weapon-move-ui`, `rules/draw-weapon-move-ai`;
- `rules/draw-with-move-bab1-ui`;
- `rules/quick-draw-free-ui`, `rules/quick-draw-free-ai`;
- `rules/sheathe-provokes-ui`;
- `rules/ready-shield-ui`;
- `rules/pick-up-provokes-ui`, `rules/pick-up-provokes-ai`;
- `rules/archer-no-ammo-draws-melee-ai`;
- `rules/npc-keeps-last-melee-weapon-ai`;
- `rules/panicked-drops-held-ai`, `rules/panicked-drops-held-pc`.

TEST BUDGET: the new scenarios, `rules/npc-throws-*`, `rules/ranged-full-attack-*`, `rules/panicked-*`, `rules/cleave-two-weapon-offhand-drop`, `rules/rapid-shot-*` (seeds 1-3); ActionCatalogTests, ActionPanelParityTests; `smoke/*` seeds 1-3.

### 16. Turn undead, rage and domain powers for both sides (`b13-class-abilities`)

IDs: CMB-201, AI-079, CMB-026 (line of effect), AI-054 (closes); D9, D10, C6. Behaviour change.

1. **Turn undead (D9).** Extract the executor from `TurnUndeadSystem.ExecuteTurnUndead` (~:432).
   - `CanStart` wraps `CanUseTurnUndead` (~:302).
   - It pays first (`TryConsumeTurnUndeadResources` ~:541), then rolls.
   - The candidates come from `GetTurnableUndeadInRange(cleric, 12)`. Each needs line of effect (`AoESystem.HasLineOfEffectToSpace`) and no total cover (CMB-026).
   - They are taken closest first, ties to the lower HD. The pool stops at the first misfit, and destroys at 2x HD.
   - The skip decider: the PC is asked once per use; the AI skips turned undead.
   - Delete `ShowTurnUndeadTargetSelectionMenu` (~:577).
2. **The AI turn rule (C6).** A cleric or paladin with uses turns when a hostile turnable undead is within 60 ft with line of effect and its best check could affect the nearest.
3. **Rage (D10).**
   - The rage executor wraps `ActivateRage` (CC ~:16093): it takes no action, on the actor's own turn only.
   - The PC button calls `RunPlayerAction`.
   - The AI rages at turn start when a foe is within reach or charge range (C6).
4. **Domain powers.** Extract the domain power buttons' executors (re-grep `Spell/Domain/`). The AI's choice is batch 14.
5. **Docs.** Delete CMB-201, AI-079 and AI-054; narrow CMB-026 to turn resistance and rebuke; record D9, D10 and C6.

Tests: new scenarios:
- `rules/turn-undead-closest-first-ui`, `rules/turn-undead-closest-first-ai`;
- `rules/turn-undead-stops-at-first-misfit`;
- `rules/turn-undead-needs-line-of-effect`;
- `rules/npc-cleric-turns-undead-ai`;
- `rules/rage-ai`;
- `rules/rage-own-turn-only-ui`.

TEST BUDGET: the new scenarios, `rules/turned-undead-flees-ai`, `rules/rage-expires-*`, `rules/paladin-*`, `rules/druid-wild-*` (seeds 1-3); the static suites covering TurnUndeadSystem and rage, ActionCatalogTests.

### 17. Every PC single attack runs the attack sequence (`b13-single-attack-sequence`)

IDs: CMB-200, CMB-203. Behaviour change; *broad* (every PC single attack).

1. **The buttons.** The Fighting Defensively and natural-attack buttons (`PendingAttackMode.Single`) start `RunAttackSequence`, so the attacker may go on into a full attack (PHB p.143).
2. **Deletions.** Delete `CombatFlowService.PerformSingleAttack` (~:583), its fallback (~:377) and `GameManager.PerformSingleAttack` (CombatActions ~:2438).
   - Update `GrappleDamageRulesTests.cs` ~:5318 and the comment at `RulesScenarios.cs` ~:3723.
   - Retire `AttackOrigin.PcSingle` if nothing produces it, with its entry in the origin list (`GameManager.AttackResolved.cs` ~:267).
3. **CMB-203.** Drop the PC-only `BaseAttackBonus < 1` gate of `OnAttackDefensivelyButtonPressed` and `OnFullAttackDefensivelyButtonPressed` (GameManager.cs ~:9245, ~:9286); PHB p.140 and p.143 set no BAB requirement.
4. **Docs.** Delete CMB-200 and CMB-203, and correct CLAUDE.md's attack-math row.

Tests:
- New scenarios:
  - `rules/fight-defensively-then-full-attack-ui`;
  - `rules/natural-attack-button-sequence-ui`.
- Update `rules/fight-defensively-ends-skipped-ui` and `rules/single-vs-full-attack-ui` if they move, and explain why.

TEST BUDGET: the new scenarios, `rules/fight-defensively-*`, `rules/single-vs-*`, `rules/grapple-natural-*`, `rules/haste-natural-*`, `rules/mm-natural-*`, `rules/improved-grab-*` (seeds 1-3); AttackSequenceStepTests, GrappleDamageRulesTests, PostAttackQuirkSetsTests; `smoke/*` seeds 1-3.

### 18. Breath, spittle and engulf as actions for any controller (`b13-monster-ability-actions`)

IDs: AI-040, CRE-039 (the choices); D11. Behaviour change.

1. **Breath.** `NPCExecuteBreathWeaponForAI` (NPCTurns ~:1271) becomes the breath executor.
   - It pays a standard action (AI-040) and does not provoke.
   - The recharge is unchanged.
   - `CanUseBreathWeaponNow` (CC ~:535) becomes its `CanStart`.
2. **Spittle.** The free spittle executor fires once per round, at one opponent within 30 ft.
3. **Engulf and the standard-action ranged specials** become executors with their existing costs.
4. **Buttons.** `AvailableTo` lists these for any creature with the data. `ActionButtonPanel` shows them for a controllable creature (`RunPlayerAction`). The Dragon profile and the routines use `RunAIAction`.
5. **Docs.** Delete AI-040; narrow CRE-039 to the triggers; record D11.

Tests: new scenarios:
- `rules/breath-costs-standard-ai`, `rules/breath-costs-standard-ui`;
- `rules/spittle-once-per-round-ai`, `rules/spittle-once-per-round-ui`;
- `rules/engulf-ui`.

TEST BUDGET: the new scenarios, `rules/dragon-ai-*`, `rules/dragon-breath-recharges`, `rules/area-origin-breath-ai` (seeds 1-3); ActionCatalogTests, ActionPanelParityTests.

### 19. Frightful presence, auras and gibbering for every bearer (`b13-monster-triggers`)

IDs: AI-041, CRE-039 (closes), AI-053 (closes); D12, D13. Behaviour change; *broad* (every attack sequence).

1. **Frightful presence (D12).** One shared resolver replaces `ResolveFrightfulPresence` (NPCTurns ~:1421).
   - It is called at the start of every attack sequence a bearer makes (`RunAttackSequence`, AoOs included), at a charge's declaration (`ExecuteCharge`), and at a breath.
   - It reads the bearer's `FrightfulPresenceDefinition` (DragonData.cs ~:192), with the D12 witness, HD (`GetHitDice()`), dragon, immunity and no-re-roll rules.
   - Delete `_hasFrightfulPresenceTriggered` (CC ~:559).
   - The lich's current data runs under these rules (the AI-010 note).
2. **Auras (D13).**
   - `ProcessAuraAbility` (AIService ~:4194) moves to `BeginActingTurn`, for every bearer and every turn, compelled ones included. Each creature saves once per round per aura.
   - Gibbering hits everything but mouthers.
   - Free spittle is skipped on compelled turns.
3. **Slot reuse.** Clear both immunity maps on slot reuse (`ForgetAuraSaveImmunities` exists; add the frightful-presence twin), with NpcSlotReuseTests cases.
4. **Docs.** Delete AI-041, CRE-039 and AI-053; note the lich on AI-010; record D12 and D13.

Tests: new scenarios:
- `rules/frightful-presence-on-aoo-ai`;
- `rules/frightful-presence-on-charge-ai`;
- `rules/frightful-presence-hd-limit`;
- `rules/frightful-presence-immune-after-save`;
- `rules/frightful-presence-controllable-bearer-ui`;
- `rules/gibbering-hits-allies-ai`;
- `rules/aura-on-compelled-turn-ai`.

TEST BUDGET: the new scenarios, `rules/dragon-ai-*`, `rules/dragon-breath-recharges`, `rules/frightened-*`, `rules/panicked-*`, `rules/turn-start-*`, `rules/charge-*` (seeds 1-3); NpcSlotReuseTests; `smoke/*` seeds 1-3.

### 20. The restricted PC menu for charm and flight (`b13-restricted-menu`)

IDs: CMB-100 (closes); D14, D15, inherited Q1. Behaviour change; *broad* (every attack and hostile cast checks the charm bit).

1. **Charm (D14).**
   - `AttackLegalityChecks.Charm = 16` joins `All`: no attack, maneuver or AoO against the charmer.
   - `CheckCastDirectAttackLegality` and `IsHostileSpellAt` refuse a hostile spell on the charmer.
   - This holds for both sides.
   - Delete `ShouldExcludeTargetBecauseOfCharm` (AIService ~:1919).
2. **Flight (D15).** In `TryRunCompulsionTurn`, a frightened or turned PC with a flight option gets the restricted menu instead of the forced flight.
   - `ActionButtonPanel` reads the set's compulsion filter for this PC only (C8). End Turn is refused while a flight option is legal.
   - A cornered frightened PC keeps its full menu. A panicked PC keeps `ExecuteFleeingForcedPCTurn`.
   - Replace the panel's reads of `IsActionBlockedByTurnedCondition` (16 references) with the set's filter. Delete it if no reader is left.
3. **ActionPanelParityTests.** Remove the mismatches this resolves.
4. **Docs.** Delete CMB-100; record D14 and D15; mark Q1 fulfilled in "Owner decisions".

Tests: new scenarios:
- `rules/charmed-pc-spares-charmer-ui`;
- `rules/charmed-npc-spares-charmer-ai`;
- `rules/charmed-no-aoo-on-charmer`;
- `rules/frightened-pc-chooses-flight-ui`;
- `rules/turned-pc-flight-menu-ui`.

Keep `rules/panicked-pc-flees` and `rules/frightened-pc-cornered-fights` as they are.

TEST BUDGET: the new scenarios, `rules/frightened-*`, `rules/panicked-*`, `rules/turned-*`, `rules/charm-*`, `rules/combat-end-charmed-*` (seeds 1-3); AttackLegalityTests, ActionPanelParityTests, ActionCatalogTests; `smoke/*` seeds 1-3.

### 21. Final full regression (`regression-final`)

Kind regression. Run the same set as the midpoint: the static edit and Play passes, `rules/*,smoke/*` seeds 1-5, and the soak seeds 1-50. Record the run in `docs/TESTING.md` 3.2, and compare the soak with the midpoint and with batch 12's follow-up.

The expected, explained shifts are the Behaviour changes rows for steps 12-20.

Count the `[ActionSet]` lines again; each must be fixed or filed. Change no game code; file any regression.

Then update:
- CLAUDE.md "Current state": batch 13 done, and next is batch 14, weighted personalities;
- PARITY section 5 step 10's status;
- this doc's status line.

## Behaviour changes

| Step | What players and the AI will notice |
|---|---|
| 4 | Every creature can take total defense (+4 dodge AC, no AoOs); a cornered panicked creature takes it |
| 5 | AI creatures never attack a target out of reach, out of range or without ammunition; an evoker with nothing to cast fights or falls back; kiters approach and attack |
| 7 | AI targeting uses exact races and types and wound bands; armor-priority and flanking terms work |
| 8 | Brutes and spell-less "casters" fight with a profile; melee casters cast first; Ranged NPCs shoot |
| 10 | An AI with no path keeps its move action |
| 12 | Loading a hand or light crossbow provokes; NPCs reload and fire in one turn; prone confused creatures stand |
| 13 | NPC wall attacks cannot double-advance the turn |
| 14 | Potions cost a standard action and provoke; scrolls cost the casting time; wands do not provoke; retrieving provokes |
| 15 | Drawing, sheathing and readying shields cost RAW actions; Quick Draw works; archers without ammunition draw; panicked creatures drop what they hold; armor is locked in combat |
| 16 | NPC clerics turn undead, closest first, with line of effect; barbarians rage; no target picker |
| 17 | Fighting defensively and natural-attack buttons allow a full attack |
| 18 | Breath costs a standard action; controllable monsters get breath, spittle and engulf buttons |
| 19 | Frightful presence fires on every attack, charge and AoO of every bearer; auras fire on compelled turns; gibbering hits allies |
| 20 | Charmed creatures spare their charmer; frightened and turned PCs choose their flight |

## Risks

- **Re-entrancy (CORE-012).** No executor continues the UI or ends a turn; the NPC wall wrappers (step 13) were the live case. `rules/three-skipped-in-a-row` guards the turn advance.
- **Trace churn.** Steps 2, 3, 6 and 9 are pure: stop on any hash change, and keep every combat-log line word for word. Steps 5, 7, 8, 10, 15, 17, 19 and 20 change many hashes; explain them with `ScenarioTraceDiff`.
- **Coroutines.** Executors over callback cores wait on a flag. `ResetCombatStateForNextEncounter` stops coroutines (CORE-013), so a suspended decider clears its state there.
- **Reused slots (CRE-046).** Clear the total defense state, the held item, the immunity maps, `AIControl` and the profile instance, each with a NpcSlotReuseTests case. Keep `_npcAIBehaviors` parallel (AI-015).
- **Reflection (TST-006).** Three members are reached from tests:
  - `FindBestFleeCell` is reflected NonPublic|Static (GrappleDamageRulesTests ~:1244);
  - `GetItemUseActionType` is pinned (CastPipelineScenarios ~:1148);
  - `PerformSingleAttack` is called in GrappleDamageRulesTests ~:5318.
- **Name clash.** `Tests.Scenarios.ActorSource` hides the global enum: write `global::ActorSource`.
- **3bbf47d signatures.** The path, barrier and terrain helpers take the mover. Steps 9, 10 and 15 re-grep them.
- **AI shifts.** Steps 5, 7, 8, 16 and 19 change soak outcomes; each regression explains the shift.
- **Large files.** Steps 9, 12, 14 and 17 touch GameManager and CharacterController; keep one concern per commit.
- **Weekly usage.** No new step at 62% weekly usage or more, and the closing regression below 64%. The natural cut points are after step 11 (the midpoint) and after step 16 (every executor extracted). At a cut, commit, push and update the resume notes.

## Not in scope

- **Batch 14 (weighted personalities):**
  - scoring the legal set: when to aid, crawl or take total defense, and what to do with a leftover move (AI-077);
  - AI-060 and AI-035;
  - re-weighting the C1 lists;
  - the domain power choice.
- **Batch 15 (NPC spellcasting):**
  - ENC-021 (C3) and SPL-041;
  - AI-009's consumable choice;
  - AI-038 and AI-022;
  - the Flaming Sphere AI hook.
- **Batch 16 (MM specials as data):**
  - AI-032, AI-008, spell-like abilities, stench (CRE-031), fly modes;
  - gaze (CRE-076), breath effects (CRE-077), non-dragon breath (AI-033);
  - the lich's fear aura data (AI-010 part);
  - spell-likes for the couatl and the naga.
- **Batch 17 (fair sight):**
  - knowledge levels and sources beyond the omniscient provider;
  - `EncounterEntityId`, the Rules mode, AI-051;
  - the tooltip leak (UI-054).
- **Batch 18 (stances):**
  - Run (CMB-202), AI-044's flight distance and cornered attack;
  - withdraw as a stance;
  - AI-010's flee thresholds, AI-047.
- **Batch 22 (one decision brain):** the evaluator over `ActorActionSet`, replacing the C1 lists and the C2 chooser; the panel reads the set for every actor.
- **Also out:** Ready and Delay (CMB-029), multi-round actions (ITM-089), the panicked random path (AI-044), weapon line of effect (GRID-009), turn resistance and rebuke (CMB-026 rest), Wish.

## Owner decisions

**2026-10-29 (relayed by the parent session; binding):**
1. **AI-075.** The owner said: "that behavior should be determined by their behavior profile but they should never waste an attack on something they can't hit". Step 5 settles it early. A universal guard covers every AI-run attacker, NPC or party member, and the alternative comes from the creature's profile through the catalog (C1).
2. **Charges.** The owner said: "Charges need a clear path unless they have some ability to bypass an object in their way, and they cannot end with two creatures spaces overlapping". The catalog's charge entry uses `CanCharge`, whose path test is the shared `TryBuildChargePath`, as the one legality test (step 3). The bypass and no-overlap parts landed as 3bbf47d; no step here touches them.

**Inherited from batch 12 (2026-10-23), and how this batch settles them:**
- **Q1. Fleeing PCs.** A panicked, turned or frightened PC flees automatically "until batch 13 builds the restricted PC menu". Step 20 builds it:
  - frightened and turned PCs choose their flight from a restricted menu;
  - a cornered frightened PC keeps its full menu;
  - a panicked PC stays automatic, as RAW leaves it no choice (D15).
- **Q2. AI attack modes.** The Rapid Shot and two-weapon rule is "labelled a stopgap for batch 13's catalog". Step 6 moves the formula, unchanged, behind the attack entries' interim chooser (C2), relabelled "interim rule, replaced by batch 22's evaluator". Step 15 adds the last-melee-weapon guard (AI-074).

Every rules question is settled above (D1-D15) under the delegation; the owner may overrule any of them.

## Owner questions

Numbered after batch 12's Q1-Q3, so Q1 and Q2 above always mean the inherited batch 12 decisions.

- **Q4. The profile for Brute creatures (AI-004, step 8).** 29 NPC definitions are Brute and have no profile today.
  - **Recommended default:** a new plain `BruteAIProfile`: melee, high aggression, the `UsePowerAttack` flag set (unread until AI-026 is fixed); no trip, no forced coup de grace, no aggressive grapple. It adds no maneuver spam before batch 14 scores maneuvers.
  - **Alternative:** `BerserkAIProfile`, which trips, grapples aggressively and always takes a coup de grace.
- **Q5. The character sheet's inventory in combat (ITM-069, step 15).**
  - **Recommended default:** usable at RAW costs through the draw, sheathe, ready-shield, retrieve and drop entries, with armor locked (ITM-089).
  - **Alternative:** view-only in combat, with equipment changed only through the action buttons.

The plan applies the defaults unless the owner answers before steps 8 and 15.
