> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-03.

# CLAUDE.md

Working agreement for AI agents. It loads every session, so it stays short; depth is in `docs/`.

## Project

- D&D 3.5e tactical combat prototype, Unity **6000.4.0f1** only (`F:/Unity/6000.4.0f1/Editor/Unity.exe`; ignore other editors under `F:/Unity/`). C# 9.0, one assembly (Assembly-CSharp), about 675 `.cs` files in `Assets/Scripts`, mostly global namespace.
- One scene, `Assets/Scenes/MainScene.unity`; `SceneBootstrap.Awake` (`Assets/Scripts/_Core/SceneBootstrap.cs`) builds grid, characters, UI and `GameManager` at runtime. No prefabs, no saves.
- Written mostly by earlier AI agents; maintained by Claude for the owner, hoyty1.
- **Goal (owner, 2026-10-03):** play as close to the 3.5e core rules (PHB/DMG/MM) as possible. Battles are randomized encounters based on the DMG random encounter rules. Between battles you manage the party by PHB/DMG rules. Enemy AI needs much more depth because the combat option space is vast. Follow RAW; no house rules (owner, 2026-10-09; owner interpretations of RAW silences in `docs/systems/RULES_COVERAGE.md` 4.1).
- **Enemy AI work:** start at `docs/systems/AI.md` (requests: `AI_PLAYBOOKS.md`; option matrix: `AI_ACTION_COVERAGE.md`; level 1-8 tactics inventory and action-catalog design: `docs/systems/tactics/README.md`); rules fidelity: `docs/systems/RULES_COVERAGE.md`.

## Ground rules

- **The code is the source of truth.** Docs, comments and commit messages are often wrong. Grep a member before calling or relying on it.
- **Compile after every C# change:** `bash tools/compile_check.sh`. Keep `errors=0`; baseline 507 warnings (479 CS0618; 508 until batch 11 step 7 removed an unused local), a jump is a regression. Needs `Library/Bee` from a prior Unity open; cannot catch player-build-only errors.
- **C# 9 only** (no file-scoped namespaces, `global using`, `record struct`). With `using System;` and `Random`, add `using Random = UnityEngine.Random;` (REPO-006).
- **Unity MCP tools may point at another project:** check that console paths exist here. No Unity batchmode without permission (writes `Library/` and `.meta` files).
- **PCs and NPCs follow identical rules (owner, 2026-10-07).** Put mechanics in actor-agnostic code that the PC UI and the AI both call; `IsControllable` may only choose between UI input and AI choice. Never fix a rule on one side only. Divergences and the unification plan: `docs/systems/PC_NPC_PARITY.md`.
- **Verify D&D rules claims.** A PHB cite in a comment shows intent, not correctness; say which rule you applied. Deviations from RAW that might be intentional (e.g. Enlarge Spell area SPL-010) are unconfirmed: ask the owner before changing such behaviour. **No house rules (owner, 2026-10-09): everything follows RAW.** Where the books are silent or ambiguous, the owner has delegated the call (2026-10-11, 2026-10-14): pick the reading with the most textual support, apply it, record it with its date in `docs/systems/RULES_COVERAGE.md` 4.1 (labelled "owner interpretation" or "delegated RAW reading" in code) and report it so the owner can overrule it; do not file it as an owner question. Ask first only about possibly intentional deviations and design or game-direction choices.
- **Never mutate database templates.** `SpellDatabase.GetSpell`, `ItemDatabase.Get`, `NPCDatabase.Get` return shared instances: clone (`SpellData.Clone()`, `ItemDatabase.CloneItem`, `NPCDefinition.Clone()`). New `ItemData` fields go in `ItemDatabase.CloneItem(string)` and `ItemBuilder.CopyBaseProperties`; new `NPCDefinition` fields in `Clone` and the templates' `CopyDefinitionFields` (CRE-023).
- **Keep docs true in the same change.** Markdown in `docs/` only. Fixed issue: delete its entry in `docs/issues/<PREFIX>.md`, update counts/Top issues in `docs/KNOWN_ISSUES.md`, cite the ID in the commit. New issue: next free ID for the prefix. Update `docs/ARCHITECTURE.md` (or its part), the matching `docs/systems/` doc and `docs/DEVELOPMENT_RECIPES.md` when structure, status or a recipe changes. Refresh status lines you re-verify; say "not verified in Play mode" for anything only read.
- **`.meta` files are versioned.** Commit each asset's `.meta` with it; after adding scripts outside Unity, let Unity import them and commit the generated metas; move or rename assets with their metas (`git mv` both); never hand-write metas.
- **Ask first:** history rewrites, force-pushes, mass deletions, Unity asset/scene/package/settings changes, anything pending an owner decision.
- **Commits:** imperative sentence-case subject, no period, e.g. `Fix flanking AC double count (CMB-001)`; no "Phase/Sprint/Tier N" prefixes; doc-only changes separate; "not verified in Play mode" in the body when true.

## Build, run, test

- **Compile:** `bash tools/compile_check.sh` (about 5 s, `VERBOSE=1` lists warnings); expect `exit=0 errors=0 warnings=507`.
- **Run:** open in 6000.4.0f1, open `MainScene`, Play. Through the Unity MCP you can enter and exit Play mode, run the static suites (`docs/TESTING.md` 3) and play scripted or AI-vs-AI fights with `Tests.Scenarios.ScenarioHarness` (`docs/TESTING.md` 3.4-3.5); hand play-testing of UI flows is still the owner's, so state what you did not verify.
- **Fast party:** **Play Now!** or **Quick Start** on character creation.
- **Dev tools:** the F12 Spell Testing Panel and `*_test` presets bypass normal rules; see `docs/TESTING.md` 4.2-4.3.
- **Static suites** in `Assets/Scripts/Tests` (no test framework, no CI): run with Tools > DND Tests or `Tests.Runner.StaticSuiteRunner.RunFromCommand` through the Unity MCP (edit pass, then Play pass; `docs/TESTING.md` 3); results in `Logs/TestRunner/`. Known failures live in `tools/tests/static-suites.json`, curated 2026-10-07 with an issue ID for each; both passes must give verdict OK, and a fix removes its entry.
- **Scenario harness** (`Assets/Scripts/Tests/Scenarios`, editor only): real fights in a fresh Play session through the Unity MCP, `Tests.Scenarios.ScenarioHarness.Start("rules/*,smoke/*", "1-3")`, then poll `Logs/Scenarios/<runId>/status.json` and read `summary.json` (`docs/TESTING.md` 3.4). Never in a Play session that ran static suites; never edit scripts during a run. AI-run and Scripted party members take the NPC turn path; Ui actors use the PC callbacks. Typed steps (`.Turn(actor, round, Step...)`) script turns. From edit mode, `ScenarioHarness.QueueFresh(filters, seeds, options)` runs each filter in its own fresh Play session (progress in `Logs/Scenarios/batch.json`); `soak/dmg-random-encounters` (seeds 1-50) is the AI-vs-AI baseline against DMG random encounters. Recipe: `docs/DEVELOPMENT_RECIPES.md` "Add a combat scenario".

## Code map

Under `Assets/Scripts/`; full map in `docs/ARCHITECTURE.md`.

| Folder | Contents |
|---|---|
| `_Core/` | `GameManager.cs` (about 11.5K lines) + 15 `GameManager.*.cs` partials, `SceneBootstrap`, `ScenarioHooks` (inert test seams) |
| `Combat/` | `Core/` (AttackCalculator, ThreatSystem, DamageModel, TeamUtility), `Conditions/`, `StatusEffects/` (ConditionRules), `Maneuvers/` + `Special/` (GameManager partials), `Behaviors/` (Charmed/Confused/Fascinated/Frightened), `Utilities/` (CombatCalculationService, CombatUtils), `Reactions/`, `Mounts/`, `Logging/` |
| `Spell/` | `Data/`, `Database/`, `Casting/` (SpellCaster), `Components/` (StatusEffectManager, MetamagicData); GameManager partials in `Resolution/`, `Special/`, `Domain/` |
| `Character/` | `Controller/CharacterController.cs` (about 12K lines), `Stats/CharacterStats.cs`, `Classes/`, `Creatures/`, `Templates/`, `Feats/` |
| `Equipment/` | `Items/` (ItemData, ItemDatabase, ItemIDs, ScrollData, WandData), `Inventory/`, `Store/`, enchantments, rings, rods, staves, wondrous |
| `Services/` | MonoBehaviour services on GameManager (Turn, Input, Movement, Condition, AI, CombatFlow, Economy, DispelMagic) + static helpers |
| `AI/`, `UI/`, `Encounters/`, `Grid/` | AI profiles; code-built UI; DMG tables and spawner; square grid ("Hex" names are legacy) |

Resolve an old doc path like `Magic/X.cs` with Glob `**/X.cs` (the 2026-05-27 reorganization moved files, not names).

Where to look first:

| Concern | Start at |
|---|---|
| Attack math | `GameManager.RunAttackSequence` (`_Core/GameManager.AttackCore.cs`: every attack sequence, off-hand steps included) -> `CharacterController.ResolveAttackSequenceStep`/`ResolveOffHandAttackStep` -> `Attack`/`FullAttack` (a flurry is `TryBeginFlurryOfBlows`, then the shared sequence) (modifier from `BuildAttackBonus`, damage modifier from `BuildWeaponDamageBonus`) -> `PerformSingleAttackWithCrit`; `CombatFlowService.PerformPlayerAttack`; `GameManager.NPCPerformAttack`; every attack sequence (the Attack and Full Attack buttons, NPC attacks, AoOs) steps through `GameManager.RunAttackSequence` (`_Core/GameManager.AttackCore.cs`); after the roll, every path's `GameManager.OnAttackResolved` (`_Core/GameManager.AttackResolved.cs`, the same stages for every path, Cleave and the target's reactions included) |
| Damage | `CharacterStats.ApplyIncomingDamage` -> `TakeDamage`; non-weapon damage `GameManager.DealDamage` (`_Core/GameManager.Damage.cs`); `Combat/Core/DamageModel.cs` (`DamagePackets`) |
| Conditions | `GameManager.ApplyCondition`, `ConditionService`, `ConditionManager`, `ConditionRules` |
| Maneuvers, grapple | `CombatUI.ShowSpecialAttackMenu` -> `GameManager.OnSpecialAttackSelected` -> `GameManager.ExecuteSpecialAttack` (`GameManager.CombatActions.cs`); partials in `Combat/Maneuvers/*.cs`; `CharacterController.ExecuteSpecialAttack` -> `Resolve*` |
| Spell casting | `Spell/Resolution/GameManager.SpellCasting.cs` (`BeginPendingSpellTargeting`, `PerformSpellCast`, `PerformAoESpellCast`, `ApplySpellBuff`); the staged cast core `GameManager.CastCore.cs` (`ExecuteCast`, every PC and NPC cast but Wish; requests `Spell/Casting/Core/SpellCastRequest.cs`); `GameManager_Spells_<Letter>.cs`; `SpellCaster.Cast` |
| Spell / item / NPC data | `SpellNames`, `SpellDatabase_<Letter>`; `ItemDatabase`, `ItemIDs`, `Inventory.RecalculateStats`; `NPCDatabase_<Letter>`, `GameManager.NPCSetup.cs` |
| AI turn | `GameManager.SingleNPCTurnFromInitiative` -> `AIService.ExecuteNPCTurn`; `TryNPCPerformSpellCast` |
| UI | `SceneBootstrap.WireButtons`, `CombatUI`, `ActionButtonPanel`, `UI/Encounter/`, `StoreUI` |

## Architecture in brief

1. **GameManager**: singleton `partial class`, 67 files in 7 folders (the `ScenarioHarness` partial is editor-only), owns all session state (party, NPCs, `CurrentPhase`); each cast's state is its own `SpellCastRequest`/`CastContext` on a cast stack; the old `_pending*` spell names are read-only views of the current cast's request (`Spell/Resolution/GameManager.CastContext.cs`).
2. **Services** are added in `GameManager.Awake` and reach private state via `Combat_*` accessors and `*ForAI` wrappers.
3. **CharacterController** resolves actions; **CharacterStats** has one named field per bonus source, hand-summed per stat. No modifier engine.
4. **Spell effects dispatch by SpellId**: single-target `TryResolve<Name>SpellEffect` handlers sit in a SpellId registry (`GameManager.SpellHandlerRegistry.cs`); area handlers sit in an area registry in the same file (`BuildAreaHandlers`, the generic loop as fallback); unclaimed single-target spells fall to `ApplySpellBuff`.
5. **One cast core:** every cast but Wish (9th level, out of scope) is a `SpellCastRequest` built by a request builder (`SpellCastRequest.For*`: the spell menu, scrolls, wands, the F12 panel, held charges, Imbue and the AI) and run by the staged cast core (`ExecuteCast`, `GameManager.CastCore.cs`), the UI decider answering for a PC and the AI decider (`RunCastSync`) for an NPC. Its resolve variants (`CastResolveKind`) are Single, Area, Summon and HoldCharge (plus Grease); its effects come from one handler registry (`GameManager.SpellHandlerRegistry.cs`: the single-target SpellId registry and the area registry), then `ApplySpellBuff`. No legacy quirk bit is left: batch 12 step 18 built the last (the held-charge delivery's cost, `HeldChargeDeliveryRules`, SPL-156) and deleted `LegacyCastQuirk`, so every path runs one rule.
6. **String IDs:** `SpellNames.X`, `ItemIDs.X` (`[Obsolete]` string overloads still the norm), snake_case NPC ids, free-string feats (`HasFeat("Power Attack")`).
7. **Static databases** with lazy `Init()`; rings, rods, wondrous items join `ItemDatabase` only via `RegisterAll*` (ITM-032).
8. **UI is built in code** and wired by `SceneBootstrap`; `WaitingFor*` flags block world input.
9. **Dormant:** `CommandProcessor`, `CombatStateMachine`, `BaseCombatManeuver` shells, the half-dead `SummoningService` and `EncounterService` copies (CORE-023; `SpellApplicationService` was deleted with SPL-056). `MetamagicSystem` is the live metamagic rule of every cast since batch 11 step 21.
10. **Conditions:** apply with `GameManager.ApplyCondition` (wraps `ConditionService.ApplyCondition`, which records source and data); `ConditionManager` stores them in `CharacterStats.ActiveConditions`. Spell buffs live in `StatusEffectManager`, unlinked to the conditions they impose (CMB-034). Both tick at the initiative count of the creature whose turn it was when they were created (`Combat/Core/TurnDurations.cs`, PHB p.138, CMB-006); unanchored ones at the round boundary.
11. **Logging:** `CombatUI?.ShowCombatLog(CombatLogHelper.<Semantic>(...))`, never raw `<color>`; diagnostics `Debug.Log("[Tag] ...")`.
12. **Dice:** unseeded `UnityEngine.Random`; `DiceRoller.Roll(count, sides)` is NdS, `DiceService.Roll(min, max)` is a range.

## Traps that bite

- A method missing from `GameManager.cs` is in another partial. Grep before adding a duplicate.
- A new single-target spell handler is one line in the SpellId registry (`BuildSingleTargetHandlers`, `GameManager.SpellHandlerRegistry.cs`) with its gate, plus a manifest row in `SpellHandlerRegistryTests`; the cast core's Handlers stage runs it for PC and NPC casts alike (batch 11 steps 6-8). A handler reads the caster it is given and its cast's request (an area handler its `CastContext`), never ActivePC or IsControllable, and never writes the `_pending*` views (read-only since batch 11 step 29); a caster choice goes on `CastChoices` for both deciders to fill (Dimension Door's destination), and one the AI cannot give yet goes in `AISpellcastingStrategist.NeedsCasterChoiceAiCannotGive` (AI-071). A new area handler is one `Add(...)` line in `BuildAreaHandlers` plus an `AreaManifest` row; it returns its log and never shows it, ends the cast or schedules `AfterAttackDelay` (the core's `FinishAreaCast` does, with its combat-end context).
- A spell effect that no registry handler claims falls to `ApplySpellBuff`, whose generic `StatusEffectManager` branch is last and returns for every spell: a special effect is a registry handler (above) or a case above that branch, and a spell that is not Buff/Debuff/Control/Illusion/Wall also needs an entry in `SpellEffectRouting` (a Healing or Damage one also `HealingResolvedByHandler`/`DamageResolvedByHandler`); the routing golden suite (`SpellRoutingGoldenTests`) pins both. Recipe: `docs/DEVELOPMENT_RECIPES.md` "Add a spell".
- Weapon attack-roll terms go in `CharacterController.BuildAttackBonus` (CMB-043), weapon damage terms in `BuildWeaponDamageBonus` (CMB-003), the rake and grapple weapon attacks included (CMB-087); iterative steps come from `CharacterCombatStats.GetAttackBonuses`/`GetIterativeAttackCount` only (CMB-044); typed attack, damage, save, skill, speed and ability bonuses go in `CharacterStats.Bonuses` with their real `BonusType` (stacked by `BonusStacking`, PHB p.171-172); never sum a new bonus source by hand.
- Every save is one roll, `SaveRollRules.Roll(stats, save, dc, SaveContext, diceContext)` (`Character/Stats/SaveRollRules.cs`: natural 1 and 20, Luck reroll, countersong; batch 12 step 10), never a hand-rolled d20; its modifier is `SaveRules.Modifier` (`Character/Stats/SaveRules.cs`): a bonus on every save goes in `CharacterStats.GetSaveTotal`, one against some effects in `SaveRules.SituationalBonus` (CHR-018).
- A new `CharacterStats` bonus field needs a writer and a reader in every formula (`docs/architecture/characters-and-creatures.md`).
- `TakeDamage`/`CurrentHP -=` skip immunity, resistance and DR; deal non-weapon damage with `GameManager.DealDamage(target, raw, DamagePackets.Spell/Supernatural/CreatureAttack(...))` (mitigation, concentration, death; SPL-004), then `GameManager.EvaluateCombatEnd` (one check for both sides) once the effect has resolved.
- `GameManager.Awake` runs before scene references are assigned (CORE-009); unassigned `CombatUI` fields fail silently (UI-013).
- `ShowActionChoices` cancels active targeting (UI-012). Turns advance re-entrantly (CORE-012); `ResetCombatStateForNextEncounter` stops GameManager coroutines (CORE-013).
- Magic strings fail silently: case-sensitive `HasFeat` (CHR-030), duplicate NPC ids (CRE-022), unknown preset ids (ENC-012), sources matched by name (CORE-017).
- Wand charges (ITM-034) and enhancement (ITM-035) are stored twice; update both.
- `IsPC` means controllable (CORE-014); use `controller.IsDead` (CHR-031); never assign `Stats.Level` (CHR-034); HD-gated rules read `Stats.GetHitDice()`, not `Level`, and class BAB, hit die and HP go through `ClassProgression` (CHR-071, CHR-002); the positional `CharacterStats` constructor takes WIS before INT (CHR-032).
- 20 test files reflect on private members by name; grep `Assets/Scripts/Tests` before renaming (TST-006).
- `_npcAIBehaviors` is index-parallel to `NPCs` (AI-015).
- Enemy pool and party slots are reused: new per-creature state on `CharacterController` (a field, a component, a link) must also be cleared in `ResetForNewCreature` or `GameManager.ResetCharacterSlotForSpawn` (CRE-046); a party slot that gets a new character calls `ResetPCSlotForNewCharacter` before `Init`.
- Never run scenarios in a Play session that ran static suites; `ScenarioHooks` must stay null outside the harness.

## Current state

- **`master` = `docs/cleanup-2026-10` since 2026-10-16** (fast-forward of 136 commits on top of 0dd8e76; the owner approved it after batch 11's clean final regression). It holds the docs rebuild, `.meta` tracking, the tactics inventory, the test runner and scenario harness, and rules batches 1-12 (batch 11: one spell-cast core for PCs and NPCs, `docs/designs/cast_core_batch11.md`). Every batch ends with a full regression recorded in `docs/TESTING.md` 3.2. **Batch 12** (2026-10-28 to 2026-10-29, `docs/designs/pc_npc_parity_batch12.md`: the rest of `docs/systems/PC_NPC_PARITY.md` section 5, steps 9 and 10 excluded) **is closed** by the follow-up regression of 2026-10-29 on b1759ad (`docs/TESTING.md` 3.2): static edit and Play passes, rules, smoke and soak all OK, 0 Fail; no regression; filed AI-075 (Med, an older AI fault the soak showed). Next is batch 13, the action catalog (PARITY section 5 step 10, `docs/systems/tactics/README.md`), which needs its design pass before any build; the rest of the AI roadmap (tags v2, personalities) follows. The Unity MCP is attached to this project (check console paths before trusting it).
- **Metamagic/consumables thread mid-flight:** scrolls and wands now use the cast pipeline, F12 has metamagic toggles, and 0dd8e76 added Enlarge area doubling and `[Metamagic]` logging. Open: SPL-010, SPL-064 (SPL-038, metamagic in custom resolvers, fixed in batch 11 step 19; SPL-040, spontaneous metamagic, in step 21).
- **Repo hygiene (2026-10-03):** `.meta` files tracked; `.abacus.donotdelete` untracked (history purge only on request, REPO-012); creature token art kept for later (REPO-015); TMP re-import pending (REPO-002).
- **Docs:** rebuilt 2026-10-03. All old docs are retired (`docs/archive/INDEX.md`); every finding is filed in `docs/issues/`; `docs/designs/` holds unbuilt plans with status headers, including the batch 11 cast-core plan (`cast_core_batch11.md`, 2026-10-15).

## Docs index

- `README.md`: overview, setup, controls.
- `docs/README.md`: docs index and policy.
- `docs/ARCHITECTURE.md` + `docs/architecture/*.md`: structure, services, conventions, subsystems.
- `docs/KNOWN_ISSUES.md` + `docs/issues/<PREFIX>.md`: rules, counts, next IDs, Top issues; entries. Read the prefix file before changing a subsystem.
- `docs/DEVELOPMENT_RECIPES.md`: how to add spells, items, monsters, UI and more.
- `docs/TESTING.md`: compile check, suites, presets, F12 panel.
- `docs/designs/`: older unbuilt plans.
- `docs/archive/INDEX.md`: retired docs via `git show`.
- `docs/systems/*.md`: per-system content, 3.5e status, backlog (AI, rules, encounters, party, spells, creatures, items).
- `docs/systems/PC_NPC_PARITY.md`: PC/NPC rules parity audit (2026-10-07): fork map, scorecard, divergences, unification plan.
- `docs/systems/tactics/`: level 1-8 tactics inventory (actions, spells, creatures, summons) and the proposed action-catalog design; sampled verification only.
