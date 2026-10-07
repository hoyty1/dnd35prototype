> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-03.

# CLAUDE.md

Working agreement for AI agents. It loads every session, so it stays short; depth is in `docs/`.

## Project

- D&D 3.5e tactical combat prototype, Unity **6000.4.0f1** only (`F:/Unity/6000.4.0f1/Editor/Unity.exe`; ignore other editors under `F:/Unity/`). C# 9.0, one assembly (Assembly-CSharp), about 670 `.cs` files in `Assets/Scripts`, mostly global namespace.
- One scene, `Assets/Scenes/MainScene.unity`; `SceneBootstrap.Awake` (`Assets/Scripts/_Core/SceneBootstrap.cs`) builds grid, characters, UI and `GameManager` at runtime. No prefabs, no saves.
- Written mostly by earlier AI agents; maintained by Claude for the owner, hoyty1.
- **Goal (owner, 2026-10-03):** play as close to the 3.5e core rules (PHB/DMG/MM) as possible. Battles are randomized encounters based on the DMG random encounter rules. Between battles you manage the party by PHB/DMG rules. Enemy AI needs much more depth because the combat option space is vast. Prefer RAW unless the owner confirms a house rule.
- **Enemy AI work:** start at `docs/systems/AI.md` (requests: `AI_PLAYBOOKS.md`; option matrix: `AI_ACTION_COVERAGE.md`; level 1-8 tactics inventory and action-catalog design: `docs/systems/tactics/README.md`); rules fidelity: `docs/systems/RULES_COVERAGE.md`.

## Ground rules

- **The code is the source of truth.** Docs, comments and commit messages are often wrong. Grep a member before calling or relying on it.
- **Compile after every C# change:** `bash tools/compile_check.sh`. Keep `errors=0`; baseline 508 warnings (479 CS0618), a jump is a regression. Needs `Library/Bee` from a prior Unity open; cannot catch player-build-only errors.
- **C# 9 only** (no file-scoped namespaces, `global using`, `record struct`). With `using System;` and `Random`, add `using Random = UnityEngine.Random;` (REPO-006).
- **Unity MCP tools may point at another project:** check that console paths exist here. No Unity batchmode without permission (writes `Library/` and `.meta` files).
- **PCs and NPCs follow identical rules (owner, 2026-10-07).** Put mechanics in actor-agnostic code that the PC UI and the AI both call; `IsControllable` may only choose between UI input and AI choice. Never fix a rule on one side only. Divergences and the unification plan: `docs/systems/PC_NPC_PARITY.md`.
- **Verify D&D rules claims.** A PHB cite in a comment shows intent, not correctness; say which rule you applied. Deviations from RAW that might be intentional (e.g. crit multiplier CMB-004, Enlarge Spell area SPL-010) are unconfirmed: ask the owner before changing such behaviour. Call something a house rule only if the code labels it so (luck stacking in `Assets/Scripts/Spell/BonusType.cs`, the attack pool in `Assets/Scripts/Combat/Core/AttackPool.cs`) or the owner confirmed it.
- **Never mutate database templates.** `SpellDatabase.GetSpell`, `ItemDatabase.Get`, `NPCDatabase.Get` return shared instances: clone (`SpellData.Clone()`, `ItemDatabase.CloneItem`, `NPCDefinition.Clone()`). New `ItemData` fields go in `ItemDatabase.CloneItem(string)` and `ItemBuilder.CopyBaseProperties`; new `NPCDefinition` fields in `Clone` and the templates' `CopyDefinitionFields` (CRE-023).
- **Keep docs true in the same change.** Markdown in `docs/` only. Fixed issue: delete its entry in `docs/issues/<PREFIX>.md`, update counts/Top issues in `docs/KNOWN_ISSUES.md`, cite the ID in the commit. New issue: next free ID for the prefix. Update `docs/ARCHITECTURE.md` (or its part), the matching `docs/systems/` doc and `docs/DEVELOPMENT_RECIPES.md` when structure, status or a recipe changes. Refresh status lines you re-verify; say "not verified in Play mode" for anything only read.
- **`.meta` files are versioned.** Commit each asset's `.meta` with it; after adding scripts outside Unity, let Unity import them and commit the generated metas; move or rename assets with their metas (`git mv` both); never hand-write metas.
- **Ask first:** history rewrites, force-pushes, mass deletions, Unity asset/scene/package/settings changes, anything pending an owner decision.
- **Commits:** imperative sentence-case subject, no period, e.g. `Fix flanking AC double count (CMB-001)`; no "Phase/Sprint/Tier N" prefixes; doc-only changes separate; "not verified in Play mode" in the body when true.

## Build, run, test

- **Compile:** `bash tools/compile_check.sh` (about 5 s, `VERBOSE=1` lists warnings); expect `exit=0 errors=0 warnings=508`.
- **Run:** open in 6000.4.0f1, open `MainScene`, Play. Through the Unity MCP you can enter and exit Play mode and run suites (`docs/TESTING.md` 3.1); hand play-testing of UI flows is still the owner's, so state what you did not verify.
- **Fast party:** **Play Now!** or **Quick Start** on character creation.
- **Dev tools:** the F12 Spell Testing Panel and `*_test` presets bypass normal rules; see `docs/TESTING.md` 4.2-4.3.
- **Static suites** in `Assets/Scripts/Tests` (no test framework, no CI): run with Tools > DND Tests or `Tests.Runner.StaticSuiteRunner.RunFromCommand` through the Unity MCP (edit pass, then Play pass; `docs/TESTING.md` 3); results in `Logs/TestRunner/`. Known failures live in `tools/tests/static-suites.json`, curated 2026-10-07 with an issue ID for each; both passes must give verdict OK, and a fix removes its entry.
- **Scenario harness** (`Assets/Scripts/Tests/Scenarios`, editor only): real fights in a fresh Play session through the Unity MCP, `Tests.Scenarios.ScenarioHarness.Start("smoke/*", "1-3", "repeat=2")`, then poll `Logs/Scenarios/<runId>/status.json` and read `summary.json` (`docs/TESTING.md` 3.4). Never in a Play session that ran static suites; never edit scripts during a run. AI-run party members take the NPC turn path.

## Code map

Under `Assets/Scripts/`; full map in `docs/ARCHITECTURE.md`.

| Folder | Contents |
|---|---|
| `_Core/` | `GameManager.cs` (about 11.5K lines) + 9 `GameManager.*.cs` partials, `SceneBootstrap`, `ScenarioHooks` (inert test seams) |
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
| Attack math | `CharacterController.Attack`/`FullAttack`/`DualWieldAttack`/`FlurryOfBlows` (modifier from `BuildAttackBonus`) -> `PerformSingleAttackWithCrit`; `CombatFlowService.PerformPlayerAttack`; `GameManager.NPCPerformAttack` |
| Damage | `CharacterStats.ApplyIncomingDamage` -> `TakeDamage`; `Combat/Core/DamageModel.cs` |
| Conditions | `GameManager.ApplyCondition`, `ConditionService`, `ConditionManager`, `ConditionRules` |
| Maneuvers, grapple | `CombatUI.ShowSpecialAttackMenu` -> `GameManager.OnSpecialAttackSelected` -> `GameManager.ExecuteSpecialAttack` (`GameManager.CombatActions.cs`); partials in `Combat/Maneuvers/*.cs`; `CharacterController.ExecuteSpecialAttack` -> `Resolve*` |
| Spell casting | `Spell/Resolution/GameManager.SpellCasting.cs` (`BeginPendingSpellTargeting`, `PerformSpellCast`, `PerformAoESpellCast`, `ApplySpellBuff`); `GameManager_Spells_<Letter>.cs`; `SpellCaster.Cast` |
| Spell / item / NPC data | `SpellNames`, `SpellDatabase_<Letter>`; `ItemDatabase`, `ItemIDs`, `Inventory.RecalculateStats`; `NPCDatabase_<Letter>`, `GameManager.NPCSetup.cs` |
| AI turn | `GameManager.SingleNPCTurnFromInitiative` -> `AIService.ExecuteNPCTurn`; `TryNPCPerformSpellCast` |
| UI | `SceneBootstrap.WireButtons`, `CombatUI`, `ActionButtonPanel`, `UI/Encounter/`, `StoreUI` |

## Architecture in brief

1. **GameManager**: singleton `partial class`, 53 files in 7 folders (the `ScenarioHarness` partial is editor-only), owns all session state (party, NPCs, `CurrentPhase`, `_pending*`).
2. **Services** are added in `GameManager.Awake` and reach private state via `Combat_*` accessors and `*ForAI` wrappers.
3. **CharacterController** resolves actions; **CharacterStats** has one named field per bonus source, hand-summed per stat. No modifier engine.
4. **Spell effects dispatch by SpellId string compare** in chained `TryResolve<Name>SpellEffect` handlers; unclaimed spells fall to `ApplySpellBuff`.
5. **Four hand-synced cast pipelines:** `PerformSpellCast`, `PerformAoESpellCast`, `TryNPCPerformSpellCast` (no metamagic), `TryConsumePendingSpellCast`. Scrolls, wands, F12 enter via `BeginPendingSpellTargeting`.
6. **String IDs:** `SpellNames.X`, `ItemIDs.X` (`[Obsolete]` string overloads still the norm), snake_case NPC ids, free-string feats (`HasFeat("Power Attack")`).
7. **Static databases** with lazy `Init()`; rings, rods, wondrous items join `ItemDatabase` only via `RegisterAll*` (ITM-032).
8. **UI is built in code** and wired by `SceneBootstrap`; `WaitingFor*` flags block world input.
9. **Dormant:** `CommandProcessor`, `CombatStateMachine`, `BaseCombatManeuver` shells, unused service copies, most of `MetamagicSystem` (SPL-055, SPL-056).
10. **Conditions:** apply with `GameManager.ApplyCondition` (wraps `ConditionService.ApplyCondition`, which records source and data); `ConditionManager` stores them in `CharacterStats.ActiveConditions`. Spell buffs live in `StatusEffectManager` with separate durations (CMB-034).
11. **Logging:** `CombatUI?.ShowCombatLog(CombatLogHelper.<Semantic>(...))`, never raw `<color>`; diagnostics `Debug.Log("[Tag] ...")`.
12. **Dice:** unseeded `UnityEngine.Random`; `DiceRoller.Roll(count, sides)` is NdS, `DiceService.Roll(min, max)` is a range.

## Traps that bite

- A method missing from `GameManager.cs` is in another partial. Grep before adding a duplicate.
- A new single-target spell handler goes in the PC chain, its `anyPriorHandled`/`anyClericHandled` flags, and `TryNPCPerformSpellCast` (SPL-054).
- In `ApplySpellBuff`, special cases go above the generic `StatusEffectManager` branch (SPL-037).
- Weapon attack-roll terms go in `CharacterController.BuildAttackBonus` (CMB-043); rake and grapple weapon attacks still sum their own (CMB-087); spell bonuses all land in `Morale*` (SPL-026).
- A new `CharacterStats` bonus field needs a writer and a reader in every formula (`docs/architecture/characters-and-creatures.md`).
- `TakeDamage`/`CurrentHP -=` skip DR and resistance (SPL-004); use `ApplyIncomingDamage` and run victory/defeat checks (CORE-011).
- `GameManager.Awake` runs before scene references are assigned (CORE-009); unassigned `CombatUI` fields fail silently (UI-013).
- `ShowActionChoices` cancels active targeting (UI-012). Turns advance re-entrantly (CORE-012); `ResetCombatStateForNextEncounter` stops GameManager coroutines (CORE-013).
- Magic strings fail silently: case-sensitive `HasFeat` (CHR-030), duplicate NPC ids (CRE-022), unknown preset ids (ENC-012), sources matched by name (CORE-017).
- Wand charges (ITM-034) and enhancement (ITM-035) are stored twice; update both.
- `IsPC` means controllable (CORE-014); use `controller.IsDead` (CHR-031); never assign `Stats.Level` (CHR-034); the positional `CharacterStats` constructor takes WIS before INT (CHR-032).
- 19 test files reflect on private members by name; grep `Assets/Scripts/Tests` before renaming (TST-006).
- `_npcAIBehaviors` is index-parallel to `NPCs` (AI-015).

## Current state

- **Branch `docs/cleanup-2026-10` (unmerged, 2026-10-07)** on top of 0dd8e76: docs rebuild, `.meta` tracking, the tactics inventory (`docs/systems/tactics/`) and AI roadmap step 1, rules fixes for shared actions (CMB-001, AI-034, SPL-007, CMB-005/073/076, SPL-006, CMB-074, CMB-014, CMB-043/002). Next is step 2, tags v2 (`docs/designs/enemy_ai_knowledge_and_personalities.md`). The Unity MCP is attached to this project (check console paths before trusting it).
- **Metamagic/consumables thread mid-flight:** scrolls and wands now use the cast pipeline, F12 has metamagic toggles, and 0dd8e76 added Enlarge area doubling and `[Metamagic]` logging. Open: SPL-010, SPL-038, SPL-040, SPL-064.
- **Repo hygiene (2026-10-03):** `.meta` files tracked; `.abacus.donotdelete` untracked (history purge only on request, REPO-012); creature token art kept for later (REPO-015); TMP re-import pending (REPO-002).
- **Docs:** rebuilt 2026-10-03. All old docs are retired (`docs/archive/INDEX.md`); every finding is filed in `docs/issues/`; `docs/designs/` holds 3 unbuilt plans with status headers.

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
