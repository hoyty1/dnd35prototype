> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-02.

# Testing

This doc covers how to check changes in this project: the headless compile check, the hand-rolled static test suites under `Assets/Scripts/Tests`, and in-game manual testing. It replaces `Assets/Scripts/Tests/README.md`, `Assets/Scripts/Tests/AidAnother_ManualTestScenarios.md` and `Assets/Scripts/Tests/Combat/SleepSpell_ManualTestScenarios.md`.

The suite results dated 2026-10-07 (section 3.1) come from a Play-mode run through the Unity MCP; other claims about runtime behavior come from reading the code. Known test problems are tracked with stable IDs in [issues/TST.md](issues/TST.md) (index: [KNOWN_ISSUES.md](KNOWN_ISSUES.md)); IDs from other areas point to the matching `issues/<PREFIX>.md` file.

## 1. Testing levels at a glance

| Level | How to run | What it catches | What it misses |
|---|---|---|---|
| Compile check | `bash tools/compile_check.sh` (about 5-7 s, no Editor needed) | C# compile errors in all of Assembly-CSharp, test code included | Runtime behavior, scene/asset problems, Editor-folder code, player-build-only errors |
| Static suites | Run them in Play mode through the Unity MCP (section 3.1) or with a temporary hook (section 3) | Rule regressions in mechanics that have a suite | Anything without a suite (section 2.2); results go to the Console only |
| Manual testing | Play `Assets/Scenes/MainScene.unity`, use presets and the F12 panel (section 4) | UI flow, targeting, AI, end-to-end combat | Anything you do not look at |

### Compile check

`tools/compile_check.sh` reuses the compiler response file Unity last generated (`Library/Bee/artifacts/*/Assembly-CSharp.rsp`), keeps its defines and references, replaces the source list with the current `Assets/**/*.cs`, and runs Unity's bundled Roslyn (`F:/Unity/<version>/Editor`, override with `UNITY_DIR`). The DLL goes to a temp dir; `Library/` is not touched. `VERBOSE=1` prints every warning.

- Requires that Unity has opened the project once on this machine (so the `.rsp` exists). If packages or scripting defines change, the `.rsp` is stale until Unity regenerates it.
- Compiles with the Editor defines (`UNITY_EDITOR`, `ENABLE_LEGACY_INPUT_MANAGER`, LangVersion 9.0), so it cannot detect code that only breaks a player build (for example `using UnityEditor` outside `#if UNITY_EDITOR`).
- Skips every path containing `/Editor/` (there are none today), analyzers and source generators. It does not import assets or create `.meta` files.
- Baseline at 0dd8e76: `exit=0 errors=0 warnings=508`, of which 479 are CS0618 (obsolete API: mostly the string overloads of `ItemDatabase.CloneItem`/`ItemDatabase.Get`, then `FindObjectOfType`). Treat any error, or a jump in the warning count, as a regression.

Run it after every code change. All test files compile into the game assembly, so a broken test breaks the game build too.

## 2. Static test suites

### 2.1 How a suite is written

There is no test framework: no `com.unity.test-framework` in `Packages/manifest.json`, no `.asmdef`, no `[Test]` attributes. A suite is a plain class:

- `public static class <Name>Tests` in namespace `Tests.<Folder>` (`Tests.AI`, `.Character`, `.Classes`, `.Combat`, `.Feats`, `.Inventory`, `.Magic`, `.Maneuvers`, `.Mounts`, `.Services`, `.Utilities`). Exceptions in the global namespace: `CraftingSystemTests`, `Phase5IntegrationTests`, `RodTests`.
- Entry point `public static void RunAll()` (94 suites). It resets private static `_passed`/`_failed`, logs a header such as `====== SLEEP SPELL RULES TESTS ======`, calls the database `Init()` methods (all idempotent), runs `private static void TestXxx()` methods, and logs a summary such as `====== ... Results: N passed, M failed ======` (the format varies; see section 3). It returns nothing.
- Most suites have their own private `Assert(bool condition, string testName, string detail = "")` that logs `  PASS: <name>` with `Debug.Log` or `  FAIL: <name> <detail>` with `Debug.LogError` (example: `Tests/Combat/SleepSpellRulesTests.cs:37-49`). Variants (`AssertEqual`, `AssertTrue`, ...) follow the same pattern. Exception: `Phase5IntegrationTests` logs `FAIL:` with plain `Debug.Log`, so its failures do not show under the Console Error filter.
- 19 suites also expose a snake_case alias such as `class_level_feature_progression_test() => RunAll()` (`Tests/Character/ClassLevelFeatureProgressionTests.cs:15`). Nothing calls them.
- Tests build characters with `new GameObject()` + `AddComponent<CharacterController>()` + `CharacterController.Init(stats, pos, null, null)` + `InventoryComponent.Init(stats)`, clean up with `Object.DestroyImmediate` in `finally`, and reach private members through `System.Reflection` (19 test files, about 60 `GetMethod`/`GetField`/`GetProperty` reflection calls; TST-006).

### 2.2 Inventory

102 `.cs` files, 98 suites, about 31.9K lines, about 1,150 `Test*` methods. List them with `find Assets/Scripts/Tests -name '*Tests.cs' | sort`; recount before quoting numbers.

| Folder | Suites | Contents |
|---|---|---|
| AI | 1 | AIProfileFrameworkTests |
| Character | 10 | CharacterTagSystem, ClassLevelFeatureProgression, DeityAlignmentRestriction, Encumbrance, ExperienceCalculator, FatigueExhaustion, MulticlassSaveCalculation, MulticlassSkillRules, ProficiencyAndAcp, RandomEncounterSystem |
| Classes | 4 | NPCTemplateSystem (largest, 92 tests), Phase1ClassTests, Phase2ClassTests, Phase3ClassTests |
| Combat | 53 | 38 spell suites (mostly `<Spell>RulesTests`, plus e.g. AreaControlSpells, DeepSlumberAndHeroism, ProtectionFromAlignment, ShockingGraspMetalBonus; e.g. Sleep, CharmPerson, MirrorImage, DispelMagic, Web) and 15 mechanics suites: Concealment, ConditionSourceAgnostic, CoreConditionRules, CreatureImmunity, DamageModifier, DualWieldPenalty, FlankingReach, ImprovedShieldBash, MediumConditionRules, NonlethalDamage, RangeCalculator, RapidShot, ReachWeapon, SizeDamageScaling, UnarmedDamageMode |
| Crafting | 1 | CraftingSystemTests (global namespace) |
| Encounters | 1 | Phase5IntegrationTests (global namespace) |
| Equipment | 1 | RodTests (global namespace) |
| Feats | 2 | Phase1CombatFeat, Phase2SpecializedTactics |
| Inventory | 1 | PostCombatLootCollection |
| Magic | 5 | MetamagicSystem, SpellRangeCategory, SummonMonster3Creatures, SummonMonsterAlignmentRules, WizardSpellProgression |
| Maneuvers | 5 | BullRushRules (pure push rules anywhere; push-and-follow scenarios need Play mode), CoupDeGraceRules, GrappleDamageRules, OverrunRules, SunderInventoryRemoval |
| Mounts | 1 | MountSystemTests |
| Services | 13 + runner | AttackCalculator, CombatCalculationService, CombatLogHelper, ConcentrationService, DiceService, DispelMagicService, EconomyService, SavingThrowResolver, SpellCastingHelper, SpellResolutionService, SpellTargetingService, SpellUtilities, TeamUtility; plus ServiceTestRunner |
| Utilities | 0 | TestHelpers, MockCharacterFactory, TestFixtures |

No suite covers wand or scroll use in combat (only `CraftingSystemTests` scroll/wand pricing and `NPCTemplateSystemTests.TestConsumableManagerWandEligibility`), the store UI (`EconomyServiceTests` covers gold and buy/sell price only), treasure generation, pathfinding, MovementService, TurnService/initiative order, the save DC actually computed under metamagic (`MetamagicSystemTests` checks effective spell level only), dragon abilities, or any UI.

### 2.3 Runners that exist

- `Tests.Services.ServiceTestRunner.RunAll()` runs 9 suites in sequence: SpellUtilities, SpellCastingHelper, TeamUtility, ConcentrationService, DispelMagicService, CombatLogHelper, SpellTargetingService, CombatCalculationService, DiceService. It skips AttackCalculatorTests, EconomyServiceTests, SavingThrowResolverTests and SpellResolutionServiceTests, has no exception isolation (one throw stops the rest), and its header comment ("Phase 5C", 8 suites, assertion counts) is stale (TST-024). Nothing calls it.
- Three suites are MonoBehaviours whose `Start()` calls `public static void RunAllTests()`: `Tests.Combat.FlankingReachRulesTests`, `RangeCalculatorTests`, `ReachWeaponRulesTests`. They log `[FlankReachTest] PASS/FAIL ...` style lines. None is attached to a scene or prefab. You can add one as a component for a local run; do not commit that scene change.
- Nothing else. No production code calls any `RunAll` (TST-002). `Encounters/DungeonEncounterTableExamples.RunAll()` is an example dump, not a test.

### 2.4 Shared helpers

| Helper | Status | Notes |
|---|---|---|
| `Tests.Utilities.TestHelpers` | Used by 7 suites (AIProfileFramework, CharacterTagSystem, CreatureImmunityRules, AttackCalculator, EconomyService, SavingThrowResolver, SpellResolutionService) | `EnsureCoreDatabasesInitialized` (Race, Class, Item, Feat, Spell), `CreateStats`, `CreateCharacter` (named args, optional `gridPosition`), `CreateWarrior/Rogue/Cleric`, `ResetActions`, `Cleanup`. HP is `level*4 + CON mod`, not real hit dice. |
| `TestHelpers.SetGridPosition` / `GetDistance` | Avoid | `SetGridPosition` sets `GridPosition` correctly but places the transform at `(x*5, 0, y*5)`; `GetDistance` measures world distance between transforms. The game uses `SquareGridUtils.GridToWorld` = `(x, y, 0)` and `SquareGridUtils.GetDistance` (3.5e diagonals). Set `GridPosition` and use `SquareGridUtils.GetDistance` instead. |
| `Tests.Utilities.MockCharacterFactory` | Unused | Commoner/Fighter/Skeleton/Goblin. `CreateSkeleton` has no undead type. |
| `Tests.Utilities.TestFixtures` (`D35TestBase`, `CombatTestBase`) | Unused | `D35TestBase` adds a GameManager (see pitfalls). |

The other 90 suites define their own local `BuildStats`/`CreateController`/cleanup helpers.

### 2.5 Known broken or stale tests

IDs refer to [issues/TST.md](issues/TST.md) unless another prefix is given.

| Suite | Problem | Evidence |
|---|---|---|
| `Tests.Combat.CharmPersonRulesTests` | Reflects on `SpellCaster.GetSaveModifier` with 6 arguments; the method now takes 8 (two `out` params added in 19e8765). `Invoke` throws `TargetParameterCountException` and the suite aborts without a summary line (TST-001). | `Tests/Combat/CharmPersonRulesTests.cs:166-196`, `Spell/Casting/SpellCaster.cs:971-979` |
| `Phase5IntegrationTests` | Asserts `MaxLevel == 9`, `EffectiveMaxLevel == 9` and tables for levels 1-9. Level 9 was removed in 6008d6f; `MaxLevel` is 8 and the CSV has levels 1-8. Also writes `phase5_6_test_results.txt` to the project root (not git-ignored). See ENC-007 ([issues/ENC.md](issues/ENC.md)) and TST-012. | `Tests/Encounters/Phase5IntegrationTests.cs:136-160, 82-83`; `Encounters/DungeonEncounterTableManager.cs:57` |
| `Tests.Classes.Phase3ClassTests` | `TestAllElevenClassesRegistered` expects 11 classes; `ClassRegistry.Init` registers 16 (11 PHB + 5 NPC classes) (TST-008). | `Tests/Classes/Phase3ClassTests.cs:613-616`, `Character/Classes/ClassRegistry.cs:29-46` |
| `Tests.Classes.NPCTemplateSystemTests` | `TestAdeptSpellLookup`/`TestAdeptSpellLevelLookup` pass uppercase ids (`"CURE_LIGHT_WOUNDS"`, `"BLESS"`, `"CURE_MODERATE_WOUNDS"`); `AdeptSpellList` stores lowercase ids and uses case-sensitive `List.Contains`, so 4 assertions fail (TST-008). | `Tests/Classes/NPCTemplateSystemTests.cs:338-356`, `Character/Classes/NPC/AdeptSpellList.cs:40, 121-148` |
| Placeholder passes | 38 `Assert(true, ...)` calls count as passes: TeamUtilityTests (all 10), SpellTargetingServiceTests 9, SpellUtilitiesTests 4, RapidShotTests 4, CounterspellRulesTests 3, EconomyServiceTests 2, NPCTemplateSystemTests 2, one each in AreaControlSpells, GhoulTouch, Scare, DispelMagicService (TST-003). | `grep -rn "Assert(true" Assets/Scripts/Tests` |
| `Tests.Maneuvers.GrappleDamageRulesTests` | Seven assertions check log text that was later rewritten; three pin-duration assertions fail every run; a few more fail by chance because the "very strong/weak grappler" helpers write `Stats.BaseAttackBonus`, which is ignored for classed characters (CHR-068). 10 to 17 failures of 183-209 in Play mode on 2026-10-07 after the per-creature attack sequence (TST-027). | `Tests/Maneuvers/GrappleDamageRulesTests.cs:96-143, 372-437, 520-548` |
| `Tests.Combat.RapidShotTests` | Four older tests expect the constructor to grant Rapid Shot, Point Blank Shot or Power Attack (TST-028). | `Tests/Combat/RapidShotTests.cs:68-112, 150-165` |
| `Tests.AI.AIProfileFrameworkTests` | Stale archetype expectations, and the off-hand threat test sets sides with `IsPlayerControlled` instead of `Team` (TST-025). | `Tests/AI/AIProfileFrameworkTests.cs:919-953` |
| `CauseFearRulesTests`, `ScareRulesTests` | Pass in edit mode, fail 6 and 1 in Play mode because the scene GameManager destroys the suite's own (TST-007). | `_Core/GameManager.cs:483-490` |
| Probably stale (not run) | `MetamagicSystemTests` predates the metamagic rewrite (b5f7987), the DC fix (616bf32) and Enlarge-doubles-AoE (0dd8e76). `ReachWeaponRulesTests.cs:26` locks in halberd reach, which matches `ItemDatabase` but not the PHB. `DiceServiceTests.cs:61-62` has statistical assertions that can fail by chance (TST-015). | Last commit to `Assets/Scripts/Tests` is 40400b7 (2026-05-27) |

Commit history shows repeated compile fixes in test files but no evidence the C# suites were ever run; the "295/295 pass" in 3f72970 refers to a Python port, `phase5_validation.py` (deleted from the repo root; read it with `git show 3f72970:phase5_validation.py`), not the C# tests. The first recorded C# run is the 2026-10-07 MCP run in section 3.1.

### 2.6 Pitfalls

IDs refer to [issues/TST.md](issues/TST.md) unless another prefix is given.

- **GameManager singleton.** 14 suites call `new GameObject().AddComponent<GameManager>()`. `GameManager.Awake` (`_Core/GameManager.cs:483-490`) schedules `Destroy` on the new object and returns early if `GameManager.Instance` already exists (in MainScene, SceneBootstrap creates one), so the test instance has no services; tests patch `_conditionService` in by reflection. In a scene without a GameManager the test instance becomes `Instance`, runs the full Awake, and `Instance` is left pointing at a destroyed object after cleanup (nothing resets it). `DispelMagicRulesTests.cs:244-273` takes a different branch depending on which case applies. Results can depend on the scene (TST-007, TST-005).
- **SquareGrid.Instance is overwritten.** `SquareGrid.Awake` sets `Instance = this` unconditionally (`Grid/SquareGrid.cs:25-28`). Six suites add their own SquareGrid, so after a run `SquareGrid.Instance` points at a destroyed grid. Code that falls back to it (`CharacterController.CurrentGrid`, `CharacterController.cs:11762`, `GameManager.cs:5180, 5385, 5471`) is affected. Restart Play mode after running suites before playtesting (GRID-010, [issues/GRID.md](issues/GRID.md)).
- **Static state.** Databases, `ItemDatabase` registrations and `UnityEngine.Random` state are static and persist for the whole Play session (Enter Play Mode Options are off, so a new Play session resets them). Suites that call `Random.InitState(seed)` never restore the previous state, and `DiceService.Roll` uses `UnityEngine.Random` directly with no seeding API (TST-010).
- **GameSettings auto-creates a GameObject.** `GameSettings.Instance` (`_Core/GameSettings.cs:22-38`) creates a `GameSettings` object if none exists. `CharacterStats.ApplyPendingLevelUp` reads it to pick the HP mode (default Roll, random), so level-up HP in tests such as `MulticlassSkillRulesTests` is random. Left Ctrl+H (`Utilities/DebugCommands.cs`, self-bootstrapped via `RuntimeInitializeOnLoadMethod`) cycles Roll/Average/Maximum in Play mode.
- **Rings, rods and wondrous items are not in ItemDatabase after `ItemDatabase.Init()` alone.** `SceneBootstrap.cs:1036-1063` calls `RingDatabase.Init`, `WondrousItemDatabase.Init`, `RodDatabase.Init` and then `RegisterAllRingsInItemDatabase` / `RegisterAllInItemDatabase`. A test that looks those items up through `ItemDatabase.Get` must do the same.
- **Play mode is required** for some suites: `AreaEffectManager.Instance` calls `DontDestroyOnLoad` (`Spell/AreaEffects/AreaEffectManager.cs:13-21`), which only works in Play mode; `ConcealmentRulesTests` uses it directly (indirect use from other suites not checked) (TST-009).
- **Exceptions are not isolated.** Only 3 test files contain a `catch`. One exception skips the rest of that suite and its summary line (TST-014).
- **Reflection breaks at runtime, not compile time.** Renaming private members such as `GameManager._conditionService`, `_activeSummons`, `_mirrorImageStates`, or `CharacterController.EstablishGrappleWith` breaks tests silently. About 18 test files make about 48 `GetMethod`/`GetField`/`GetProperty` calls. Grep `Assets/Scripts/Tests` for the member name before renaming (TST-006).
- **Namespace shadowing.** Inside any `Tests.*` namespace, `Inventory` resolves to the namespace `Tests.Inventory`; write `global::Inventory` for the class (TST-011).
- **CharacterStats constructor order** is `..., str, dex, con, wis, intelligence, cha, ...` (WIS before INT, `Character/Stats/CharacterStats.cs:3345`). Use named arguments.
- **Tests ship in player builds.** No asmdef or `#if` guard; the three MonoBehaviour suites appear in the Add Component menu (TST-013).

### 2.7 Writing a new suite

1. Create `Assets/Scripts/Tests/<Folder>/<Name>Tests.cs` with `namespace Tests.<Folder>` and `public static class <Name>Tests`.
2. Copy the `RunAll`/`Assert` skeleton from `Tests/Combat/SleepSpellRulesTests.cs:12-49`: reset counters, header log, database init (that file calls `RaceDatabase`/`ClassRegistry`/`ItemDatabase`/`SpellDatabase.Init()` directly; `TestHelpers.EnsureCoreDatabasesInitialized()` also covers `FeatDefinitions`), test calls, summary log.
3. Build characters with `TestHelpers.CreateCharacter(...)` using named arguments; set `GridPosition` directly.
4. Wrap each test body in `try/finally` and destroy everything you created, including any GameManager or SquareGrid.
5. Prefer public API. If reflection is unavoidable, `Assert` that the `MethodInfo`/`FieldInfo` is not null instead of silently returning a default.
6. Do not add `Assert(true, ...)` placeholders. If something cannot be tested yet, log a warning that does not count as a pass.
7. If you seed `UnityEngine.Random`, save `Random.state` first and restore it in `finally`.
8. Run the compile check, then run the suite with the hook in section 3 and paste the summary line into your change notes.

## 3. How to run suites today

There is no runner, menu item or CI. Use a temporary MonoBehaviour. The example below compiles against 0dd8e76 (checked with the compile-check toolchain; it adds no warnings). Save it as, for example, `Assets/Scripts/Tests/TempTestRunner.cs`, edit the `Suites` list, and delete it before committing.

```csharp
#if UNITY_EDITOR
using System;
using UnityEngine;

// TEMPORARY runner. Do not commit. In Play mode press F10, or use the
// component's context menu "Run test suites" on the TempTestRunner object.
public class TempTestRunner : MonoBehaviour
{
    private static readonly (string name, Action run)[] Suites =
    {
        ("SleepSpellRulesTests", Tests.Combat.SleepSpellRulesTests.RunAll),
        ("FlankingReachRulesTests", Tests.Combat.FlankingReachRulesTests.RunAllTests),
        ("ServiceTestRunner", Tests.Services.ServiceTestRunner.RunAll),
        ("RodTests", RodTests.RunAll), // global namespace
        ("BullRushRulesTests", Tests.Maneuvers.BullRushRulesTests.RunAll), // scenarios need Play mode
    };

    private int _errorLogs;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("TempTestRunner");
        go.AddComponent<TempTestRunner>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10)) RunSuites();
    }

    private void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errorLogs++;
    }

    [ContextMenu("Run test suites")]
    private void RunSuites()
    {
        Application.logMessageReceived += OnLog;
        foreach (var (name, run) in Suites)
        {
            int before = _errorLogs;
            try { run(); }
            catch (Exception ex) { Debug.LogError($"[TempTestRunner] {name} threw (summary line missing)"); Debug.LogException(ex); }
            Debug.Log($"[TempTestRunner] {name}: {_errorLogs - before} error log(s)");
        }
        Application.logMessageReceived -= OnLog;
    }
}
#endif
```

Steps: open `Assets/Scenes/MainScene.unity`, press Play, and press F10 once the character-creation screen is up (suites build their own characters, so no party is needed). The bootstrap needs no scene edit. F10 is unused elsewhere (only F12 is bound). Stop Play mode afterwards (see the pitfalls in 2.6).

Reading results: enable only the Error filter in the Console to see `FAIL:` lines and exceptions (except `Phase5IntegrationTests`, which logs failures as plain `Debug.Log`). For summary lines search `passed` (case-insensitive): 81 suites print `Results:`, 13 print `RESULTS`, and `MetamagicSystemTests`, `NPCTemplateSystemTests` and `RodTests` use other formats. A suite with no summary line threw. The per-suite error count includes any `Debug.LogError` from production code, not just `FAIL:` lines, and misses `Phase5IntegrationTests` failures. An agent without Editor access can read the same output from `%LOCALAPPDATA%\Unity\Editor\Editor.log` after the user runs the suites.

An Editor `[MenuItem]` under an `Editor/` folder would also work, but suites need Play mode, and `compile_check.sh` skips `/Editor/` paths, so it would not be compile-checked.

**Proposal (not implemented):** add `com.unity.test-framework` and a thin NUnit wrapper per suite that runs `RunAll()` in a `[UnityTest]` and fails on any logged error. An asmdef test assembly cannot reference the predefined Assembly-CSharp, so this either needs tests in the predefined Editor assembly or asmdefs for game code (a large refactor; `partial class GameManager` spans 52 files). Investigate before adopting.

### 3.1 Running suites through the Unity MCP

When the Unity MCP is attached to this project (check that the console paths it reports exist here), an agent can run suites without adding a file:

1. Enter Play mode with `Unity_RunCommand` calling `EditorApplication.EnterPlaymode();`. Entering Play mode reloads the domain; the next MCP call runs after Play mode has started. Character creation is enough, because suites build their own characters.
2. Run suites in one `Unity_RunCommand` and collect the results with a log hook:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!EditorApplication.isPlaying) { result.LogError("Enter Play mode first."); return; }
        var fails = new List<string>();
        int pass = 0;
        Application.LogCallback hook = (msg, stack, type) =>
        {
            if (msg.Contains("FAIL") || msg.Contains("❌") || type == LogType.Exception) fails.Add(msg.Split('\n')[0]);
            else if (msg.Contains("PASS") || msg.Contains("✅")) pass++;
        };
        Application.logMessageReceived += hook;
        try { Tests.Combat.ScareRulesTests.RunAll(); }
        catch (Exception ex) { fails.Add("threw: " + ex.Message); }
        finally { Application.logMessageReceived -= hook; }
        result.Log($"pass={pass} fail={fails.Count}");
        foreach (string f in fails) result.Log(f);
    }
}
```

3. Exit with `EditorApplication.ExitPlaymode();`. Suites leave GameObjects in the running game, so restart Play mode before playing by hand.

Notes:

- Return the failure messages from the hook, as above. The Console keeps a limited window, and `Unity_GetConsoleLogs` output is too large to read whole (it is saved to a file; grep it).
- Suites that `AddComponent<GameManager>()` behave differently in Play mode, where the scene GameManager destroys theirs (TST-007). Run those in edit mode inside a temporary additive scene: `var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);`, run the suite, then `EditorSceneManager.CloseScene(temp, true);`. Suites that need `Awake` or `AreaEffectManager` (TST-009) still need Play mode.
- Enter Play Mode Options are off (full domain reload), and script changes during Play mode recompile and continue. Exit Play mode before editing scripts.
- `AssetDatabase.DeleteAsset` fails through the MCP ("User interactions are not supported").

Run of 2026-10-07 (Play mode, after the AI roadmap step 1 rules fixes 2dc4d02..e9f6c84). No failure traced back to those fixes.

| Suite | Pass | Fail | Cause of the failures |
|---|---|---|---|
| `Tests.AI.AIProfileFrameworkTests` | 43 | 3 | Stale expectations (TST-025) |
| `Tests.Combat.CauseFearRulesTests` | 28 | 6 | Play mode only (TST-007); 36 pass, 0 fail in edit mode |
| `Tests.Combat.ScareRulesTests` | 76 | 1 | Play mode only (TST-007); 77 pass, 0 fail in edit mode |
| `Tests.Services.SpellUtilitiesTests` | 26 | 0 | |
| `Tests.Services.ConcentrationServiceTests` | 34 | 0 | |
| `Tests.Maneuvers.GrappleDamageRulesTests` | 130 | 28 | Ignored BAB writes, stale log text, pin release (TST-027, CHR-068; CMB-089 since fixed, see below) |
| `Tests.Combat.FlankingReachRulesTests` | 80 | 0 | |
| `Tests.Combat.RapidShotTests` | 48 | 6 | Class-granted feats assumed (TST-028) |

Re-runs of `GrappleDamageRulesTests` on 2026-10-07 after the CMB-089 fix (pin release ends the grapple and moves neither creature): six runs of the first version (165 assertions) gave 140 to 142 pass, 23 to 25 fail; four runs of the final version (166 assertions) gave 135 to 142 pass, 24 to 31 fail. The pin-release assertions (14 per run in the final version) all passed every run; the failures are the TST-027 causes (13 ignored BAB writes, 7 stale log texts, 3 pin-duration assertions) plus 0 to 8 chance failures in the damage, pin, pin-escape and Use Opponent's Weapon tests.

Run of `GrappleDamageRulesTests` on 2026-10-07 after the per-creature attack sequence (CMB-102 step 1; the iterative tests now set `BaseAttackBonusOverride`, and 17 attack-sequence assertions were added, 183 in all): 166 pass, 17 fail. A second run after the review fixes (every attack uses a ladder step, natural-weapon creatures share one natural-attack budget with maneuvers; 5 more assertions, 188 in all): 174 pass, 14 fail. All iterative-maneuver and attack-sequence assertions passed in both runs. The failures are the remaining TST-027 causes: 7 stale log texts, 3 pin-duration assertions and 4 to 7 chance failures (Monk nonlethal and non-monk lethal grapple damage, pinned opposed escape, Escape Artist pin break, weak disarm / Improved Disarm counter-disarm).

Run of 2026-10-07 after NPC maneuvers started paying one attack step (CMB-102 step 2; `TestManeuverActionCostTable` added, 191 assertions): `GrappleDamageRulesTests` 180 pass, 11 fail; `AIProfileFrameworkTests` 43 pass, 3 fail (TST-025, unchanged). All iterative-maneuver, attack-sequence and maneuver-cost assertions passed. The grapple failures are the TST-027 causes: 7 stale log texts, 3 pin-duration assertions and 1 chance failure (grapple damage with a weapon equipped). An earlier run in the same session, whose summary line was cut off, also showed the two Improved Unarmed Strike nonlethal assertions failing by chance. A re-run after adding `TestNpcManeuverCostsOneAttackStep` (194 assertions): 184 pass, 10 fail, all TST-027 (7 stale log texts, 3 pin-duration); the three NPC maneuver-cost assertions passed.

Run of 2026-10-07 after the NPC melee attack started running step by step (CMB-102 step 3; `TestAttackSequenceStepResolverUsesStepBab` and `TestNpcMeleeSequenceTripThenAttacks` added, 199 assertions): `GrappleDamageRulesTests` 188 pass, 11 fail; `AIProfileFrameworkTests` 43 pass, 3 fail (TST-025, unchanged). The five new assertions passed (shared step resolver BAB, adjustment and natural step; NPC trip replacing the first attack followed by attacks at +6 and +1; one attack after the move action). The grapple failures are TST-027: 7 stale log texts, 3 pin-duration assertions and 1 chance failure (non-monk lethal grapple damage). `TestNpcMeleeSequenceTripThenAttacks` calls the private `GameManager.PerformNPCMeleeAttackSequence` by reflection (TST-006). A re-run after the review fixes (a natural-weapon creature keeps its remaining natural attacks after a substitute; one more assertion, 200 in all): `GrappleDamageRulesTests` 187 pass, 13 fail, with the new claw/claw/bite assertion passing; the failures are TST-027 (7 stale log texts, 3 pin-duration assertions) plus 3 chance failures (Monk default grapple damage, unarmed grapple damage dice, Use Opponent's Weapon success). `AIProfileFrameworkTests` 43 pass, 3 fail (TST-025, unchanged). A second `GrappleDamageRulesTests` run in the same Play session showed more pin and chance failures, as expected from objects left by the first run (section 3.1).

Run of 2026-10-07 after bull rush became a standard action or charge end for every creature (CMB-102; the iterative bull rush test and the `PcUiAlsoReplacesAttack` assertion deleted, `TestBullRushIsAStandardAction` and `TestBullRushTargetLimits` added, 209 assertions; then the review fixes: footprint-aware push and follow, the post-move legality check in `ResolveChargeBullRush`, the PC refusal before the turning break, and the NPC executor refusing a standing `BullRushCharge`): `GrappleDamageRulesTests` 196 pass, 13 fail (an earlier run in the same session had 16 failures, all of the same TST-027 kinds); `FlankingReachRulesTests` 80 pass, 0 fail. All 14 new bull rush assertions passed: standard action cost for a fresh, an attacking and a moved creature; the NPC executor refusing a target two squares away through `CanBullRush`'s adjacency rule without spending anything, then bull rushing the same target once adjacent and spending the standard action; Medium vs Large allowed; Medium vs Huge, Small vs Large, swarm, incorporeal, two squares away and a grappling attacker refused; the charge planner leaving adjacency to the path. All 13 failures are TST-027 kinds: stale log texts (the 4 pinner block messages, unarmed grapple damage, Use Opponent's Weapon), the 3 pin-duration assertions, and chance failures in the Improved Unarmed Strike and Monk grapple damage. No NPC bull rush charge was run, no Large push was exercised in a test, and the PC bull rush buttons were not clicked in Play mode.

Run of 2026-10-07 after the bull rush push and follow became one RAW resolution (CMB-109 and CMB-110 fixed; CMB-098's bull rush part done; new suite `Tests.Maneuvers.BullRushRulesTests`, 48 assertions), rerun after the review fixes (push and follow interleaved, no follow without movement or after a 5-foot step, follow AoOs seeded with the charge and initiation provokers, diagonal parity continued from the charge path): `BullRushRulesTests` 48 pass, 0 fail; `GrappleDamageRulesTests` 196 pass, 13 fail on a clean rerun (TST-027 kinds: the 4 pinner block messages, unarmed grapple damage log and dice, Use Opponent's Weapon log, the 3 pin-duration assertions, and chance failures in the Improved Unarmed Strike and Monk grapple damage; a first run in the same session had 19, the extra 6 being chance failures of the opposed pin and escape checks); `FlankingReachRulesTests` 80 pass, 0 fail. The new suite checks `BullRushRules.GetMaxPushSquares` (not following 1; margin 12, limit 6 -> 3; limit cap; no movement -> no follow; failure 0; diagonal cost, also continued from an odd charge-diagonal count), the movement limit (speed; charge twice the speed minus the path cost; 0 after a 5-foot step or while prone), the push direction for Medium and Large footprints, and Play-mode scenarios that call the private `GameManager.ResolveBullRushPushAndFollow`, `ResolveBullRushStepAoOs` and `ResolveBullRushAoO` by reflection (TST-006) with Stats-built actors on the scene grid, registered in `GameManager.NPCs` for the test and removed (grid occupancy cleared) afterwards: a Medium attacker pushes a Large defender east and follows 1; a Large attacker pushes 2 and follows 2; the attacker's ally takes an AoO as the defender leaves its threatened square while the attacker takes none; the defender, beside the attacker's departure square, takes no AoO as the attacker follows (control: it does without the partner exclusion); misdirection with the d100 fixed through `GameManager.BullRushMisdirectionRollOverride` (25 strays, 26 does not, the defender's own AoO never strays); an AI profile that stays pushes exactly 1 square with margin 10 while the default AI pushes 3 and follows 3; a Large follower blocked by a third creature after the first square leaves the defender exactly 1 square away; a prone attacker pushes 1 and does not follow; an opponent that already provoked this action gets no follow AoO (control: it does otherwise). Not verified in Play mode: the PC prompt (buttons and 0-9/Esc keys), a charge bull rush end to end (including the Disrupted branch when a strayed initiation AoO kills the defender), a follow stopped by an AoO (trip or drop), and the victory, defeat and turn-ending paths after a push or follow AoO drops someone.

## 4. Manual testing

### 4.1 Starting a session

Press Play in `Assets/Scenes/MainScene.unity`; `_Core/SceneBootstrap.cs` builds everything at runtime. On the character-creation screen (`UI/CharacterCreation/CharacterCreationUI.cs`):

- **Play Now!** (`OnVeryQuickStart`, line 2802) creates Fighter Aldric (Dwarf), Rogue Lyra (Elf), Cleric Theron (Human) and Wizard Elara (Elf) from each class's `GetQuickStartCharacter()`.
- **Quick Start** (`OnQuickStart`) picks a party size of 1-6 from the 11 PHB quick-start characters. Only PC1-PC4 exist and `GameManager.SetupCreatedCharacters` stops at 4, so sizes 5-6 probably drop the extra characters (not verified in Play mode).

Then the Encounter Selection UI opens (`GameManager.PromptEncounterSelection`, `_Core/GameManager.cs:785`), followed by the Pre-Combat Hub (`UI/Encounter/PreCombatHubUI.cs`: Manage Inventory (Stash), Open Store, Prepare Spells, Crafting Workshop, Start Encounter, Back to Encounter Selection).

### 4.2 Test encounter presets

24 presets in `NPCDatabase.ListEncounterPresets()` (`Character/Creatures/NPCDatabase.cs:90-117`) are test scenarios. `GameManager.ApplyEncounterPreset` (`_Core/GameManager.cs:1769`) matches the id against the constants at `GameManager.cs:100-121` and calls a `Configure<X>TestParty()` method in `_Core/GameManager.TestConfigs.cs`. Those methods re-`Init` PC1-PC4 with fixed stats and deactivate unused PCs. The replacement persists: picking a normal preset next only re-activates PCs (`RestoreStandardPartyLayout`), so restart Play mode to get your created party back (CORE-004 in [issues/CORE.md](issues/CORE.md)).

| Preset id | Active PCs | Notes |
|---|---|---|
| `test_2_goblins`, `xp_levelup_test` | your party | No handler; standard layout. `xp_levelup_test` is one CR 15 "XP Pinata Goblin". |
| `grapple_test`, `feint_sneak_test`, `celestial_template_test`, `fiendish_template_test`, `ogre_battle_test`, `npc_magic_missile_test`, `disrupt_undead_test` | 1 | |
| `wizard_spell_test`, `true_strike_test`, `cleric_spell_test` | 1 | Level 20 caster (Wizard: Archmage Theron; Cleric: High Priestess Ilyra) with every implemented class spell auto-prepared, vs `target_dummy`. `true_strike_test` reuses the wizard setup. |
| `turn_undead_test`, `shield_bash_test`, `summon_monster_test` | 2 | |
| `armor_targeting_test`, `tiger_hunt_test` | 3 | |
| `grease_test` (2), `protection_from_evil_test` (1), `wind_dispersion_test` (2, displayed as "Obscuring Mist Test"), `obscuring_mist_ranged_only` (3), `charm_person_test` (2), `sleep_spell_test` (1), `mirror_image_test` (1) | as shown | **Spell preparation probably broken.** See below. |

The last row's presets set `PreparedSpellSlotIds` to level-1+ spells only (`GameManager.TestConfigs.cs:156, 1000, 1082, 1127, 1252, 1539, 1606, 1671`). `SpellcastingComponent.ApplyPreparedSpellSlotIds` (`Spell/Components/SpellcastingComponent.cs:1258`) fills slots by index, slots start with level 0, and `IsValidSpellForSlot` (:1446) requires an exact level match, so the entries are rejected and auto-prepare is skipped. Expect Console warnings `Cannot prepare level 1 spell in level 0 slot!` and `Ignored invalid prepared spell`. The fix is to pad with cantrips first, as `ConfigureNpcMagicMissileTestParty` does (:933). Until then, cast through the F12 panel or try the hub's Prepare Spells (not verified).

### 4.3 F12 Spell Testing Panel

`UI/Spells/SpellTestingPanel.cs` is added to the canvas unconditionally (`SceneBootstrap.cs:193`, no debug-build guard) and toggled with F12 (`Update`, line 92; key check at :94). It lists every `SpellDatabase` spell (placeholders shown with a darker background), filters by text and level, offers metamagic toggles, and keeps damage/save/SR counters.

`CastSpell` (line 1096) works only during a PC's turn (`gm.ActivePC` must be non-null; otherwise it logs "No active PC!"). Bypasses applied to that PC, so do not treat results as normal play:

- A non-caster temporarily gets a Wizard 10 class level and a `SpellcastingComponent` (`EnsureTemporaryCasterSetup`).
- +4 to the casting ability (`ApplyAbilityBoost`); `Update` restores it (and the temporary Wizard setup) once `GetTestPanelCaster()` returns null.
- `EnsureSpellAvailable` adds the spell to known and prepared lists regardless of class list or caster level, adds a slot at the spell's level if none is ready, sets `SlotsRemaining` to 99 and un-uses spent slots holding that spell.
- `GameManager.TestCastSpellFromPanel` (`_Core/GameManager.TestPanel.cs:38`) sets `_testPanelCastActive`, forces `CurrentPhase = PCTurn`, resets the `_pending*` fields, applies metamagic to a clone, and calls `BeginPendingSpellTargeting`. `OnCellClicked` falls back to `GetTestPanelCaster()` (`GameManager.CombatActions.cs:558`). Because `ActivePC` already requires `PCTurn` (`GameManager.cs:282-291`), forcing the phase has no effect in practice, and the class comment's claim that the panel works outside a PC turn is outdated (UI-021, UI-046 in [issues/UI.md](issues/UI.md)).
- While `_testPanelCastActive` is true, three checks are skipped: the arcane spell failure roll (`GameManager.TryRollArcaneSpellFailure`, `Spell/Resolution/GameManager.SpellCasting.cs:213`), the spell component pouch check in `PerformSpellCast` (`:1689`), and the Stoneskin diamond-dust inventory check (`:7151`).
- These bypasses do not apply to single-target, touch or self casts. `PerformSpellCast` calls `CleanupTestPanelCast` as its first statement (`:1634`), before the pouch check, its ASF roll (`:1837`) and the spell handlers run, so such casts roll ASF and need a pouch and material components exactly as in normal play. The ASF bypass does apply to area casts (`PerformAoESpellCast`, ASF roll at `:3675`) and to summon casts (`PerformSummonMonsterCast` and `PerformSummonSwarmCast` spend resources through `TryConsumePendingSpellCast`, ASF roll at `:114`), because none of these paths calls `CleanupTestPanelCast`. The area path has no pouch check at all.
- The flag can leak into the next cast. `CleanupTestPanelCast` has only two callers: `PerformSpellCast` and the cancel branch of `HandleAttackTargetClick` taken when a non-highlighted cell is clicked (`GameManager.CombatActions.cs:1099`). After an F12 area or summon cast the flag stays set until one of those runs. Until then every cast skips the ASF roll, including NPC casts (`GameManager.NPCTurns.cs:797` calls the same `TryRollArcaneSpellFailure`). The +4 boost and the temporary Wizard setup also stay on the test caster, because `SpellTestingPanel.Update` restores them only once `GetTestPanelCaster()` returns null. This comes from code reading and was not verified in Play mode (SPL-036 in [issues/SPL.md](issues/SPL.md)).

### 4.4 Other dev hooks

Left Ctrl+H cycles the HP calculation mode (`Utilities/DebugCommands.cs`). `Identifiers/EnumTest.cs` and `IdentifierTest.cs` are editor-only `[ContextMenu]` smoke checks, not attached anywhere. There are no other test scripts: the root Python scripts `phase5_validation.py` and `test_range_calculator.py`, which tested Python re-implementations rather than the C# code, were deleted (read them from git history if needed).

## 5. Manual scenarios

### 5.1 Aid Another

Code: `Combat/Maneuvers/SupportActions.cs` (a `partial class GameManager`; the file's `SupportActions` class itself is empty). No preset exists; use a multi-PC party (Play Now! + `test_2_goblins`) and move characters into the layouts below (read "Orc" as any enemy). Distances are Chebyshev squares, reach-aware via `CharacterController.ThreatensWith`. Equipping a longspear for case 2 was not checked; the Store or stash may have one.

Flow:

0. Open **Special Attack** and choose **Aid Another**. The main-panel Aid Another button is always hidden (`UI/Combat/ActionButtonPanel.cs:706-707`). The entry (`UI/Combat/CombatUI.cs:1171-1174`) reads `Aid Another (Standard)` or `Aid Another (<reason>)` with reason `Used`, `Pinned`, `No ally` or `No target`, and is disabled unless `CanUseAidAnother` passes. `OnAidAnotherButtonPressed` has a pinned-to-grapple-menu redirect, but it is not reached from this menu because `CanUseAidAnother` already rejects pinned characters.
1. If both an aid target and an adjacent sleeping ally exist, a picker offers **Aid Attack/Defense** or **Wake Sleeping Ally**; otherwise this step is skipped.
2. Select an enemy the initiator threatens with its equipped weapon.
3. Choose **Aid Defense (+2 AC)** or **Aid Offense (+2 Attack)**.
4. Select an ally who threatens that enemy with the ally's own weapon. Reach-weapon allies adjacent to the enemy do not qualify; unarmed or ranged-weapon allies count as threatening adjacent squares.
5. The log shows `Melee touch attack vs AC 10:` with roll, modifier (BAB + STR + size + condition penalty) and total; on a miss, `Aid Another failed (needed 10).` The standard action is spent either way.

| Case | Layout | Expected |
|---|---|---|
| 1 Adjacent | Fighter (6,5) longsword, Rogue (6,6), Orc (7,6) | Orc listed; choose Defense; Rogue listed. With Fighter at (5,5) instead, the menu shows `Aid Another (No target)`. |
| 2 Reach | Fighter (5,5) longspear, Rogue (7,6), Orc (7,5) | Orc listed (distance 2); choose Offense; Rogue listed. |
| 3 No ally in range | Fighter (5,5), Rogue (10,10), Orc (6,5) | After choosing the aid type: `No allies in melee range of Orc!`; back to the action menu, no action spent. |
| 4 Several enemies | Fighter (5,5), Orc1 (6,5), Orc2 (5,6), Goblin (10,10), Rogue (7,5) | Orc1 and Orc2 listed, not Goblin. Select Orc1: Rogue listed. |
| 5 Several allies | Fighter (5,5), Rogue (7,5), Wizard (6,6), Cleric (10,10), Orc (6,5) | Rogue and Wizard listed, not Cleric. |

Bonus checks: each successful aider adds a +2 entry and entries stack; the first matching attack consumes them (offense: beneficiary attacks that enemy; defense: that enemy attacks the beneficiary). Unused entries expire at the beneficiary's second turn start. The Character Sheet shows an "AID ANOTHER BONUSES" section only while bonuses are active (`CharacterSheetUI.AppendAidAnotherSection`). The AI never uses Aid Another: `AIBehaviorData.UseAidAnother` exists but nothing reads it.

### 5.2 Sleep

Code: `GameManager.ResolveSleepSpell` (`Spell/Resolution/GameManager.SpellCasting.cs:5023`). Automated coverage: `Tests.Combat.SleepSpellRulesTests.RunAll()` (definition, Asleep condition, wake helper, Aid Another availability; not the HD pool).

Setup: `sleep_spell_test` makes PC1 Archmage Theron (Wizard 20, only active PC) against skeleton_archer (1 HD undead, immune), goblin_warchief (2 HD), orc_berserker (3 HD) and human_cleric (5 HD, over the cap). Its prepared list fails (section 4.2), so cast Sleep from the F12 panel on Theron's turn. Immune and over-cap creatures are dropped silently; the `Candidates: N` count excludes them.

1. **HD pool.** Cast so the 10-ft burst covers all four enemies. Log: `HD Pool: N (4d4) | Duration: R rounds | Will DC D` and `Result: K target(s) asleep. Remaining HD pool: P.` Repeat via F12 (slots refresh); N stays within 4-16.
2. **Ordering and cap.** Lowest HD first; ties broken by distance from the caster, not the burst center. A target larger than the remaining pool logs `(X HD) exceeds remaining pool (P) — skipped.` HD are deducted before SR and the Will save, so a resisting target still uses pool. human_cleric is never affected.
3. **Wake on damage.** Damage a sleeper (lethal or nonlethal): `<name> wakes from taking damage.`; Asleep is removed.
4. **Wake Sleeping Ally.** Not possible in `sleep_spell_test` (single PC). Use Play Now! + `test_2_goblins`: Elara casts Sleep (F12) on a burst that includes Aldric or the cleric Theron (Sleep affects allies; elves, including Elara and Lyra, are immune; the ally needs 4 HD or less and gets a Will save, so retry if it resists). An adjacent PC uses Special Attack, Aid Another, then Wake Sleeping Ally. Expect a standard action spent, `<aider> shakes <ally> awake.`, and Asleep plus Unconscious removed.
5. **Duration.** 1 min/level = 10 rounds per caster level (200 rounds for Theron). Use a low-level caster and end turns until `⏱ <name> wakes as sleep duration expires.`
