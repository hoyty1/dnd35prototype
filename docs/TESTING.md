> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-02.

# Testing

This doc covers how to check changes in this project: the headless compile check, the hand-rolled static test suites under `Assets/Scripts/Tests`, and in-game manual testing. It replaces `Assets/Scripts/Tests/README.md`, `Assets/Scripts/Tests/AidAnother_ManualTestScenarios.md` and `Assets/Scripts/Tests/Combat/SleepSpell_ManualTestScenarios.md`.

The suite results dated 2026-10-07 (section 3.2) come from Play-mode and edit-mode runs through the Unity MCP; other claims about runtime behavior come from reading the code. Known test problems are tracked with stable IDs in [issues/TST.md](issues/TST.md) (index: [KNOWN_ISSUES.md](KNOWN_ISSUES.md)); IDs from other areas point to the matching `issues/<PREFIX>.md` file.

## 1. Testing levels at a glance

| Level | How to run | What it catches | What it misses |
|---|---|---|---|
| Compile check | `bash tools/compile_check.sh` (about 5-7 s, no Editor needed) | C# compile errors in all of Assembly-CSharp, test code included | Runtime behavior, scene/asset problems, Editor-folder code, player-build-only errors |
| Static suites | The committed runner `Tests.Runner.StaticSuiteRunner`: menu **Tools > DND Tests**, or `RunFromCommand` through the Unity MCP (section 3); an edit pass, then a Play pass | Rule regressions in mechanics that have a suite, compared against the known-failures baseline `tools/tests/static-suites.json` | Anything without a suite (section 2.2); suites that pass on a bug |
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
- Entry point `public static void RunAll()` (95 suites). It resets private static `_passed`/`_failed`, logs a header such as `====== SLEEP SPELL RULES TESTS ======`, calls the database `Init()` methods (all idempotent), runs `private static void TestXxx()` methods, and logs a summary such as `====== ... Results: N passed, M failed ======` (the format varies; the runner reads the counters directly, section 3). It returns nothing.
- Most suites have their own private `Assert(bool condition, string testName, string detail = "")` that logs `  PASS: <name>` with `Debug.Log` or `  FAIL: <name> <detail>` with `Debug.LogError` (example: `Tests/Combat/SleepSpellRulesTests.cs:37-49`). Variants (`AssertEqual`, `AssertTrue`, ...) follow the same pattern. Exception: `Phase5IntegrationTests` logs `FAIL:` with plain `Debug.Log`, so its failures do not show under the Console Error filter.
- 19 suites also expose a snake_case alias such as `class_level_feature_progression_test() => RunAll()` (`Tests/Character/ClassLevelFeatureProgressionTests.cs:15`). Nothing calls them.
- Tests build characters with `new GameObject()` + `AddComponent<CharacterController>()` + `CharacterController.Init(stats, pos, null, null)` + `InventoryComponent.Init(stats)`, clean up with `Object.DestroyImmediate` in `finally`, and reach private members through `System.Reflection` (19 test files, about 60 `GetMethod`/`GetField`/`GetProperty` reflection calls; TST-006).

### 2.2 Inventory

103 `.cs` files, 98 suites, about 31.9K lines, about 1,150 `Test*` methods. List them with `find Assets/Scripts/Tests -name '*Tests.cs' | sort`; recount before quoting numbers.

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
| Runner | 0 | StaticSuiteRunner, the committed runner (section 3) |
| Utilities | 0 | TestHelpers, MockCharacterFactory, TestFixtures |

No suite covers wand or scroll use in combat (only `CraftingSystemTests` scroll/wand pricing and `NPCTemplateSystemTests.TestConsumableManagerWandEligibility`), the store UI (`EconomyServiceTests` covers gold and buy/sell price only), treasure generation, pathfinding, MovementService, TurnService/initiative order, the save DC actually computed under metamagic (`MetamagicSystemTests` checks effective spell level only), dragon abilities, or any UI.

### 2.3 Runners that exist

- **`Tests.Runner.StaticSuiteRunner`** (`Assets/Scripts/Tests/Runner/StaticSuiteRunner.cs`, all inside `#if UNITY_EDITOR`, not in an `Editor/` folder so the compile check covers it) discovers and runs every suite. Section 3 describes it.
- `Tests.Services.ServiceTestRunner.RunAll()` runs 9 suites in sequence: SpellUtilities, SpellCastingHelper, TeamUtility, ConcentrationService, DispelMagicService, CombatLogHelper, SpellTargetingService, CombatCalculationService, DiceService. The committed runner supersedes it and does not discover it (its name does not end in `Tests`). It has no exception isolation and a stale header comment (TST-024). Nothing calls it.
- Three suites are MonoBehaviours whose `Start()` calls `public static void RunAllTests()`: `Tests.Combat.FlankingReachRulesTests`, `RangeCalculatorTests`, `ReachWeaponRulesTests`. They are attached to nothing; the runner calls `RunAllTests()` directly and reads their `N passed, M failed` summary line, because they have no `_passed`/`_failed` fields.
- No production code calls any `RunAll`. `Encounters/DungeonEncounterTableExamples.RunAll()` is an example dump, not a test; the runner skips it by name.

### 2.4 Shared helpers

| Helper | Status | Notes |
|---|---|---|
| `Tests.Utilities.TestHelpers` | Used by 7 suites (AIProfileFramework, CharacterTagSystem, CreatureImmunityRules, AttackCalculator, EconomyService, SavingThrowResolver, SpellResolutionService) | `EnsureCoreDatabasesInitialized` (Race, Class, Item, Feat, Spell), `CreateStats`, `CreateCharacter` (named args, optional `gridPosition`), `CreateWarrior/Rogue/Cleric`, `ResetActions`, `Cleanup`. HP is `level*4 + CON mod`, not real hit dice. |
| `TestHelpers.SetGridPosition` / `GetDistance` | Avoid | `SetGridPosition` sets `GridPosition` correctly but places the transform at `(x*5, 0, y*5)`; `GetDistance` measures world distance between transforms. The game uses `SquareGridUtils.GridToWorld` = `(x, y, 0)` and `SquareGridUtils.GetDistance` (3.5e diagonals). Set `GridPosition` and use `SquareGridUtils.GetDistance` instead. |
| `Tests.Utilities.MockCharacterFactory` | Unused | Commoner/Fighter/Skeleton/Goblin. `CreateSkeleton` has no undead type. |
| `Tests.Utilities.TestFixtures` (`D35TestBase`, `CombatTestBase`) | Unused | `D35TestBase` adds a GameManager (see pitfalls). |

The other 90 suites define their own local `BuildStats`/`CreateController`/cleanup helpers.

### 2.5 Known broken or stale tests

The curated baseline `tools/tests/static-suites.json` is the list of known failures: every assertion that failed with all seeds swept on 2026-10-07 is listed there with its issue ID (section 3.2). Seed-dependent failures are not listed by label; `flakyFailures` tolerates them by count: MagicCircle's "MC vs Evil: summoned blocked attack is treated as miss" (1, it fails with the baseline seed) and SunderInventoryRemoval's "Main-hand weapon destroyed - ..." or "Shield destroyed - ..." checks (3, none with the baseline seed). IDs refer to [issues/TST.md](issues/TST.md) unless another prefix is given. Summary:

| Suite | Fails (baseline seed) | Cause |
|---|---|---|
| `Tests.Combat.CharmPersonRulesTests` | throws `TargetParameterCountException` after 8 passes | Reflects on `SpellCaster.GetSaveModifier` with 6 of its 8 arguments (TST-001) |
| `Tests.Classes.NPCTemplateSystemTests` | 16, then `KeyNotFoundException` | Stale expectations (TST-008); CR and feat-count helpers (CRE-043) |
| `Tests.Classes.Phase3ClassTests` | 11, then `NullReferenceException` | Stale expectations and a wrong form name (TST-008) |
| `Phase5IntegrationTests` | 3 | Level 9 tables (ENC-007); fiendish template not parsed (ENC-022) |
| `Tests.AI.AIProfileFrameworkTests` | 3 | Stale archetype expectations; off-hand threat test sets sides with `IsPlayerControlled` (TST-025) |
| `Tests.Maneuvers.GrappleDamageRulesTests` | 10 (the same 10 with all 67 seeds) | Stale log texts (TST-027); the pin never expires (CMB-120, owner question) |
| `Tests.Combat.RapidShotTests` | 6 | Class-granted feats assumed (TST-028) |
| `Tests.Combat.SizeDamageScalingTests` | 4 | The single-die rows of the weapon damage size table are shifted one step (CMB-119, a game bug) |
| `Tests.Combat.FlamingSphereRulesTests` | 5 | The sphere cannot path out of a creature's square (SPL-120); stale log text (TST-030) |
| `Tests.Magic.SummonMonster3CreaturesTests` | 9 | 8 stale expectations (TST-030); dretch Str 14 (CRE-022) |
| `Tests.Magic.SummonMonsterAlignmentRulesTests` | 2 | Diagonal alignment steps (CHR-013) |
| `Tests.Combat.MagicCircleRulesTests` | 2 (1 to 2 by seed) | Summon registered on the suite's own GameManager (TST-007); the miss check depends on the roll (TST-031) |
| Concealment, Invisibility, JumpAndMagicWeapon, ProtectionFromAlignment, AreaControlSpells | 2, 2, 2, 4, 1 | Actors left in the same square or built without `Init` (TST-029) |
| CoreConditionRules (edit mode) | 2 | No `ConditionManager` hook and no skills without `Awake`/`Init` (TST-029) |
| CommandUndead, DeepSlumberAndHeroism, DazeMonster (edit), ExpeditiousRetreat, GhoulTouch (edit), GustOfWind, WizardSpellProgression, AttackCalculator | 1, 1, 1, 1, 1, 2, 3, 5 | Stale or wrong expectations; the code follows the rules (TST-030) |
| `Tests.Combat.BlindnessDeafnessRulesTests` | 1 | Asserts the Cleric spell level on the merged spell (TST-026) |
| `Tests.Combat.TouchOfIdiocyRulesTests` | 2 | Fixed seed 42 makes the touch attack miss (TST-031) |
| `Tests.Maneuvers.SunderInventoryRemovalTests` | 0 (0 to 3 by seed) | Shield destruction depends on the roll (TST-031) |
| Placeholder passes | none | 38 `Assert(true, ...)` calls count as passes (TST-003): TeamUtilityTests (all 10), SpellTargetingServiceTests 9, SpellUtilitiesTests 4, RapidShotTests 4, CounterspellRulesTests 3, EconomyServiceTests 2, NPCTemplateSystemTests 2, one each in AreaControlSpells, GhoulTouch, Scare, DispelMagicService |

Two suites pass on a bug and stay as they are (owner decision 2026-10-07): `Tests.Feats.Phase2SpecializedTacticsTests` (CHR-009) and `Tests.Character.DeityAlignmentRestrictionTests` (CHR-013). Fixing either bug makes its suite fail until the expectation is updated. `MetamagicSystemTests` passes 131/131 but predates the metamagic rewrite and never checks a save DC (TST-015); `ReachWeaponRulesTests.cs:26` locks in halberd reach, which matches `ItemDatabase` but not the PHB.

Ten suites pass in edit mode on a path the game never runs: ColorSpray, ConditionSourceAgnostic, CoreConditionRules, CoupDeGrace, DazeMonsterAndHideousLaughter, GhoulTouch, Hypnotism, MediumConditionRules, Overrun and Sleep. Without `Awake` there is no `ConditionManager`, so `CharacterController.ApplyConditionDirect` falls back to `CharacterStats.ApplyCondition`; in the game every controller has the `ConditionManager` that `Awake` adds. Until TST-029 is fixed their passes say nothing about `ConditionManager`'s stacking rules and hooks.

`flakyFailures` is a count, not a list of labels: in MagicCircle and SunderInventoryRemoval any other failure within the count still gives verdict OK. After a change to code those suites touch, read the suite's `new:` labels in the summary or `baseline.unexpected` in `static-play.json`, not only the verdict.

The "295/295 pass" in 3f72970 refers to a Python port, `phase5_validation.py` (deleted from the repo root; read it with `git show 3f72970:phase5_validation.py`), not the C# tests. The first recorded C# runs are those of 2026-10-07 in section 3.2.

### 2.6 Pitfalls

IDs refer to [issues/TST.md](issues/TST.md) unless another prefix is given.

- **GameManager singleton.** 14 suites call `new GameObject().AddComponent<GameManager>()`. `GameManager.Awake` (`_Core/GameManager.cs:483-490`) schedules `Destroy` on the new object and returns early if `GameManager.Instance` already exists (in MainScene, SceneBootstrap creates one), so the test instance has no services; tests patch `_conditionService` in by reflection. In a scene without a GameManager the test instance becomes `Instance`, runs the full Awake, and `Instance` is left pointing at a destroyed object after cleanup (nothing resets it). `DispelMagicRulesTests.cs:244-273` takes a different branch depending on which case applies. Results can depend on the scene (TST-007, TST-005).
- **SquareGrid.Instance is overwritten.** `SquareGrid.Awake` sets `Instance = this` unconditionally (`Grid/SquareGrid.cs:25-28`). Six suites add their own SquareGrid, so after a run `SquareGrid.Instance` points at a destroyed grid. Code that falls back to it (`CharacterController.CurrentGrid`, `CharacterController.cs:11762`, `GameManager.cs:5180, 5385, 5471`) is affected. The runner puts `SquareGrid.Instance` back after each suite; a suite called by hand does not. Restart Play mode after running suites before playtesting (GRID-010, [issues/GRID.md](issues/GRID.md)).
- **Static state.** Databases, `ItemDatabase` registrations and `UnityEngine.Random` state are static and persist for the whole Play session (Enter Play Mode Options are off, so a new Play session resets them). Suites that call `Random.InitState(seed)` never restore the previous state, and `DiceService.Roll` uses `UnityEngine.Random` directly with no seeding API (TST-010). The runner seeds each suite and restores `Random.state` after the pass, restores `GameManager.Instance` and `SquareGrid.Instance` when a suite replaced them, and destroys root GameObjects a suite left behind. It does not reset static databases, registrations or other static fields, so suites can still affect each other and the game in the same Play session.
- **GameSettings auto-creates a GameObject.** `GameSettings.Instance` (`_Core/GameSettings.cs:22-38`) creates a `GameSettings` object if none exists. `CharacterStats.ApplyPendingLevelUp` reads it to pick the HP mode (default Roll, random), so level-up HP in tests such as `MulticlassSkillRulesTests` is random. Left Ctrl+H (`Utilities/DebugCommands.cs`, self-bootstrapped via `RuntimeInitializeOnLoadMethod`) cycles Roll/Average/Maximum in Play mode.
- **Rings, rods and wondrous items are not in ItemDatabase after `ItemDatabase.Init()` alone.** `SceneBootstrap.cs:1036-1063` calls `RingDatabase.Init`, `WondrousItemDatabase.Init`, `RodDatabase.Init` and then `RegisterAllRingsInItemDatabase` / `RegisterAllInItemDatabase`. A test that looks those items up through `ItemDatabase.Get` must do the same.
- **Play mode is required** for some suites: `AreaEffectManager.Instance` calls `DontDestroyOnLoad` (`Spell/AreaEffects/AreaEffectManager.cs:13-21`), which only works in Play mode; `ConcealmentRulesTests` uses it directly (indirect use from other suites not checked). `CharacterController.Init` uses the `SpriteRenderer` that `Awake` creates, and `Awake` does not run on `AddComponent` in edit mode, so every suite that calls `Init` throws `NullReferenceException` in edit mode (TST-009).
- **Assigning `controller.Stats` instead of calling `Init`.** In Play mode `Awake` has already added a `ConditionManager` with no stats, so conditions applied to such an actor are dropped without a word; in edit mode the `CharacterStats` fallback path runs instead of `ConditionManager` (TST-029). An actor whose `GridPosition` is never set stands in the same square as every other such actor, and `CharacterController.Attack` refuses the attack as out of range. Build actors with `TestHelpers.CreateCharacter` (or `Init`) at explicit adjacent positions.
- **Exceptions inside a suite.** Only 3 test files contain a `catch`, so one exception skips the rest of that suite and its summary line. The runner catches it, records the exception type, message and three stack frames, still reads the suite's counters, and goes on with the next suite.
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
8. Run the compile check, then run the suite with the runner (`RunFromCommand("filter=<Name>Tests")`, section 3) and paste its line from `static-<mode>.txt` into your change notes. Add an entry for the suite to `tools/tests/static-suites.json` (`needs`, and any known failures with their issue IDs), or the runner reports it as NEW.

## 3. Running suites

`Tests.Runner.StaticSuiteRunner` (`Assets/Scripts/Tests/Runner/StaticSuiteRunner.cs`) is the committed runner. The whole file is inside `#if UNITY_EDITOR`, so it does not ship in player builds; it is not in an `Editor/` folder, so `compile_check.sh` compiles it.

**Entry points.**

- Menu **Tools > DND Tests > Run Static Suites (current mode)**, **List Static Suites** and **Write Proposed Baseline**. Each logs its summary to the Console.
- From a Unity MCP `CommandScript`: `Tests.Runner.StaticSuiteRunner.RunFromCommand("<options>")` returns a summary of at most about 4,000 characters (verdict, totals, the non-passing suites, file paths). `ListSuites()` lists every discovered suite with its `needs` and reports NEW and MISSING suites. `Run(StaticRunOptions)` returns the full `StaticRunReport`. The types are public because the MCP's script assembly cannot see internal members.
- `StaticSuiteRunner.RanInPlayModeThisDomain` is true once a Play pass ran in this domain (it resets when Play mode is entered again).

**Discovery.** Every non-nested type in Assembly-CSharp whose name ends in `Tests` and declares a parameterless `public static void RunAll()` (preferred) or `RunAllTests()`, sorted by full name. That gives 98 suites today (95 `RunAll`, 3 `RunAllTests`). `ServiceTestRunner` and `DungeonEncounterTableExamples` do not end in `Tests` and are not run.

**Mode gating.** Each suite's `needs` comes from the baseline (`play`, `edit`, `either`, `unknown`; a suite missing from the baseline counts as `unknown`).

- A Play pass runs `play`, `either` and `unknown` suites. It records whether a scene `GameManager` existed (`sceneGameManager`) and warns when it did not.
- An edit pass runs only `edit` suites: CauseFear and Scare, which fail in Play mode because of TST-007, and the 10 suites that build actors without `Init` and lose their conditions in Play mode (TST-029). In edit mode those 10 run the `CharacterStats` condition fallback, not `ConditionManager` (section 2.5), so they do not cover the game's condition path until TST-029 moves them back to `play`. It runs inside a temporary additive scene that becomes the active scene, then restores the previous active scene and closes the temporary one without saving. It never saves a scene.
- `all=true` ignores the gate and baseline skips; suites run in the wrong mode are marked `modeMismatch`.

**Refusals** (verdict `REFUSED`, nothing runs, only `static-<mode>.txt` is written, so the JSON of an interrupted pass stays resumable): Unity is compiling; scripts have compile errors; Unity is entering or leaving Play mode; `STALE_CODE`; a resume that does not match (see `after=`); or, in an edit pass, the temporary scene cannot be created (for example while an untitled scene is open). `STALE_CODE` means the loaded code may not be what is on disk: a `.cs` file under `Assets` is newer than both `Library/ScriptAssemblies/Assembly-CSharp.dll` and the last edit-mode script import, or the dll was rebuilt after the domain loaded. Whether Unity recompiles an edit made during Play mode depends on the per-user preference *Script Changes While Playing*, so the runner checks instead of assuming. A file whose timestamp changed without a content change (for example after `touch`) is not reimported by `AssetDatabase.Refresh()` and keeps tripping the check; once a refresh compiled nothing, pass `allowStaleCode=true`.

**Isolation, per suite.**

- Suite logs are captured by swapping `Debug.unityLogger.logHandler` (plus `Application.logMessageReceived` for engine messages) and are not echoed to the Console unless `echo=true`, so Console Error Pause does not stop a run. With `echo=true` the suites' FAIL `LogError` lines reach the Console, and Error Pause can leave Play mode paused after the call; the summary then shows `paused=true`, and `ExitPlaymode` still works.
- Exceptions are caught; the result records the type, message and three stack frames, and the run goes on.
- When the seed is not 0, `UnityEngine.Random` is seeded with `seed XOR FNV-1a(full name)` before the suite. The state from before the pass is restored at the end.
- New root GameObjects in the loaded scenes (and, in Play mode, the DontDestroyOnLoad scene) are counted in `leakedCount`, named in `leaked` (first 10) and destroyed (`cleanupLeaks=false` keeps them).
- Known game leaks are destroyed too but reported as one aggregated `knownLeaks` entry per name, for example `PooledLogMsg x588 (UI-001, destroyed)`. These are the UI-001 orphans: every combat log call rebuilds `CombatLogPanel`'s pool and leaves its 50 prewarmed lines at the scene root. `ObjectPool.Get` skips destroyed entries, so destroying them does not break the combat log.
- Lazily created singletons that keep a static `Instance` are kept and listed under `singletonsCreated` (with `xN` when more than one): `AreaEffectManager`, `WindEffectManager`, `ExperienceCalculator`, `GameSettings`, `DebugCommands`.
- `GameManager.Instance` and `SquareGrid.Instance` are put back if the suite replaced them (TST-007, GRID-010); the result lists them under `restored`.
- Files that appear in the project root during a suite are listed under `strayFiles`. Only names on the runner's allowlist (`phase5_6_test_results.txt`, the old Phase5 dump) are deleted; any other file is marked `(kept)`, because it may not belong to the suite.
- The runner does not reset static databases, registrations or other static fields.

**Counts.** The runner reads the suite's private static `_passed`/`_failed` fields (also after a throw). Without them it uses the last `N passed, M failed` line, else it counts PASS/FAIL marker lines. `noSummary` flags a suite with counters but no summary line that did not throw.

**Failure labels.** Each captured line that matches `[Tag] FAIL:`, `[FAIL]`, `❌` or `✗` becomes a label: the tag and markers are stripped and duplicates merged, at most 60 kept. `failureCounts` (parallel to `failures`) records how many FAIL lines carried each label, so an assertion text used twice (RapidShot's "Rogue should have Rapid Shot feat") counts twice. Other error logs and exceptions are counted as `errorLogCount` with 5 samples; they are informational only.

**Options** (space-separated `key=value` tokens):

| Option | Meaning |
|---|---|
| `filter=A,B` | Run only suites whose full name contains one of the entries (case-insensitive) |
| `all=true` | Ignore the mode gate and baseline skips |
| `seed=N` | Seed base; the default is the baseline's `seed` (20261007). `seed=0` does not seed the suites, so they continue from the session's `Random.state`; the runner restores that state after every pass, so repeated `seed=0` passes in one Play or editor session start from the same state and repeat each other. For a spread, sweep explicit seeds (`seed=1`, `seed=2`, ...), as the 2026-10-07 sweeps did |
| `echo=true` | Also send suite logs to the Console; FAIL lines then trigger Console Error Pause, which can leave Play mode paused (`ExitPlaymode` still works) |
| `cleanupLeaks=false` | Keep leaked root objects |
| `maxSeconds=S` | Stop starting new suites after S seconds; the report has `complete=false` and `resumeAfter`, and the TXT and summary print the full resume options |
| `after=<FullName>` | Resume: run only suites sorted after this one and merge into the existing report of the same mode. Refused unless that report is incomplete, `after` equals its `resumeAfter` or `currentSuite`, and `filter`, `seed` and `all` match; copy the printed `resume with ...` options. The report records `resumedFrom` |
| `writeBaseline=true` | Also write `Logs/TestRunner/static-baseline-proposed.json` |
| `allowStaleCode=true` | Run despite a `STALE_CODE` refusal |

**Result files** in `Logs/TestRunner/` (git-ignored by `/[Ll]ogs/`):

- `static-<play|edit>.json`: the full report (`JsonUtility`). It is written when the pass starts, before each suite (with `currentSuite` set to it) and after each suite, with `complete=false` and `resumeAfter` = the last suite with a result until the pass ends. If Unity froze or crashed, `currentSuite` names the suite that hung. Fields: runId, startedUtc, unityMode, sceneGameManager, seedBase, filter, all, after, resumedFrom, maxSeconds, codeLoadedUtc, durationMs, complete, verdict, resumeAfter, currentSuite, totals, newSuites, missingSuites, warnings, and per suite: status (`pass`, `fail`, `error`, `skipped`), skipReason, modeMismatch, countSource, passed, failed, noSummary, seed, durationMs, exception, failures, failureCounts, errorLogCount and samples, leaked, leakedCount, knownLeaks, singletonsCreated, restored, strayFiles and the baseline diff.
- `static-<mode>.txt`: overwritten with a `RUNNING` header when the pass starts, so an old result is never mistaken for the current one. At the end: a header with run id, mode, seed, scene GameManager, totals and verdict; one line per suite run, for example `FAIL  Tests.Combat.RapidShotTests  48/54  known 0  new 6  0.0s`; then the sections REGRESSIONS, STALE, NEW/MISSING, LEAKS and WARNINGS. Read this first.
- `static-<mode>.log`: every captured line, with a `### <suite>` separator per suite, written at the end (a `RUNNING` line until then). After a resumed run it holds only the suites of the last call.
- The summary's last line lists the files this call wrote; a refusal writes only the TXT.

**Baseline** (`tools/tests/static-suites.json`, versioned, outside `Assets` so it has no `.meta`): `{ "version": 1, "seed": 20261007, "suites": [ ... ] }` with one entry per suite: `type` (full name), `needs`, `skip` (non-empty text skips the suite unless `all=true`), `expectThrow` (an exception type name, for example `TargetParameterCountException` for CharmPerson, TST-001), `passed`, `failed`, `flakyFailures`, `knownFailures` (a list of `{ "match", "issue" }`, where `match` is an ordinal prefix of a failure label and `issue` an issue ID) and `notes`. The runner never writes this file; `writeBaseline=true` writes a proposal to curate by hand: new failure labels get the issue `TODO`, and an exception seen in the run becomes `expectThrow` with `TODO: issue ID for <Exception>` appended to `notes`. The loader warns about known failures with an empty or `TODO` issue, and about an `expectThrow` whose `notes` have no issue ID (or a `TODO`).

**Verdicts.**

- A suite is a REGRESSION when it throws without a matching `expectThrow`, or when its failures not explained by a known label exceed `flakyFailures`. Unexplained = the larger of (unmatched labels + labels beyond the cap) and (the suite's failure count minus the FAIL lines whose label matched, capped at the failure count). Failures without a FAIL line therefore count as unexplained; suites that print each failure twice (Crafting, Rod, Phase5) are not penalized.
- A suite with no baseline entry (NEW) that fails or throws counts as a REGRESSION too and is listed under REGRESSIONS with `(no baseline entry)`.
- It is STALE when a known failure was not seen, or when `expectThrow` is set and it did not throw.
- A drop in total assertions against the baseline counts is a warning.
- The pass verdict is REGRESSION, else STALE, else OK. It is `NO_BASELINE` when the file is missing (a malformed file is reported and the run continues without a comparison) and `REFUSED` when the pass could not start (see Refusals). NEW (discovered, no entry) and MISSING (entry, not discovered) suites are listed; a NEW suite changes the verdict only when it fails or throws.

The baseline was curated from the first full run on 2026-10-07 (section 3.2): 12 suites `edit`, 33 `play`, 53 `either` (run in the Play pass), no `unknown`; every label that failed with all seeds swept is a known failure with an issue ID, and `flakyFailures` covers the seed-dependent labels by count only (SunderInventoryRemoval 3 and MagicCircle 1, the largest number seen over 67 seeds; the suites' `notes` name the labels). GrappleDamage had 11 until its helpers were fixed the same day and is now 0 (TST-027). Both passes give verdict OK with the baseline seed. A label that matches only some seeds is left out of `knownFailures`, so that a run with another seed does not report it as STALE. When you fix a known failure, delete its entry (the runner reports it as STALE until you do) and update `passed`/`failed`. When you curate from `static-baseline-proposed.json`, change only the entries your fix touched: the proposal lists every unmatched label of that run as a `TODO` known failure, including the seed-dependent MagicCircle and SunderInventoryRemoval labels, which would then go STALE with other seeds; copy such a label only if a seed sweep shows it fails with every seed. TouchOfIdiocy reseeds the RNG itself, so its two known failures depend on the order of `Random` calls and can go STALE after an unrelated change (TST-031).

### 3.1 Running suites through the Unity MCP

When the Unity MCP is attached to this project (check that `Application.dataPath` and the console paths it reports are under `F:/DNDPrototype`):

1. In edit mode, after script changes, call `AssetDatabase.Refresh();` and check `!EditorApplication.isCompiling` and `!EditorUtility.scriptCompilationFailed` in the next call.
2. Edit pass: `result.Log(Tests.Runner.StaticSuiteRunner.RunFromCommand(""));`.
3. `EditorApplication.EnterPlaymode();`. Entering Play mode reloads the domain; the next MCP call runs once Play mode has started. Character creation is enough, because suites build their own characters.
4. Play pass: `result.Log(Tests.Runner.StaticSuiteRunner.RunFromCommand(""));`. A full Play pass of the 86 Play-mode suites takes about 3 s (2.8 to 3.2 s on 2026-10-07) and the edit pass about 0.2 s, so `maxSeconds` is a safety net; if you set it, resume with the printed `resume with ...` options until `complete` is true.
5. Read `Logs/TestRunner/static-play.txt` (and the JSON for detail) with the Read tool.
6. `EditorApplication.ExitPlaymode();`. Suites leave static state behind, so do not play by hand, and never run the scenario harness, in a Play session that ran suites.

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!EditorApplication.isPlaying) { result.LogError("Enter Play mode first."); return; }
        result.Log(Tests.Runner.StaticSuiteRunner.RunFromCommand("filter=BullRushRulesTests,RapidShotTests"));
    }
}
```

Notes:

- A filtered suite whose `needs` does not match the current mode is skipped (it shows in the `skipped` count only): run `edit` suites (CauseFear, Scare and the 10 TST-029 suites) in edit mode, or pass `all=true`.
- For a seed sweep, call `Run(new StaticRunOptions { Filter = "...", Seed = n })` in a loop in one `CommandScript` and collect `suites[i].failures`; 67 seeds of one suite take a few seconds.
- The MCP refuses some namespaces in a `CommandScript` (for example `System.Reflection`); call the runner's public API instead.
- `Unity_GetConsoleLogs` output is too large to read whole; the runner's files are the record.
- Enter Play Mode Options are off (full domain reload). Exit Play mode before editing scripts. Whether Unity recompiles an edit made during Play mode depends on the per-user preference *Script Changes While Playing*; on 2026-10-07 it did not, and the old code kept running. The runner refuses with `STALE_CODE` in that case.
- If Unity freezes in a suite (an endless loop, a stack overflow), the owner has to kill and restart it. Then read `currentSuite` in `Logs/TestRunner/static-play.json`, add a `skip` for that suite to the baseline with the reason, and resume with the options for `after=<currentSuite>` (same filter and seed); the runner warns that the suite has no result.
- `AssetDatabase.DeleteAsset` fails through the MCP ("User interactions are not supported").

### 3.2 Recorded runs

**Baseline run of 2026-10-07** (commit 6ca6a6c plus the curated `tools/tests/static-suites.json`; Unity 6000.4.0f1 through the Unity MCP; Play mode on MainScene with the scene GameManager; seed 20261007). Steps: an edit pass with `all=true` to classify every suite in edit mode, the default edit pass, a Play pass in a fresh Play session, a second Play pass in the same session (identical counts), a `seed=0` pass (only GrappleDamage, SunderInventoryRemoval and the MountSystem assertion count changed; later `seed=0` passes in the same session repeated it, because the runner restores `Random.state` after each pass), then seed sweeps (25 seeds in edit mode, 25 and 40 seeds in Play mode) to set `flakyFailures`. Final confirmation after curation: edit pass then a fresh Play pass, both verdict OK, 0 regressions, 0 stale; the active scene was not dirty.

| Folder | Suites | Passed | Failed | Suites not passing |
|---|---|---|---|---|
| AI | 1 | 43 | 3 | 1 |
| Character | 10 | 641 | 0 | 0 |
| Classes | 4 | 581 | 27 | 2 (both throw) |
| Combat | 53 (10 in edit mode) | 2023 | 40 | 19 (CharmPerson throws) |
| Crafting | 1 | 73 | 0 | 0 |
| Encounters | 1 | 158 | 3 | 1 |
| Equipment | 1 | 183 | 0 | 0 |
| Feats | 2 | 102 | 0 | 0 |
| Inventory | 1 | 6 | 0 | 0 |
| Magic | 5 | 263 | 14 | 3 |
| Maneuvers | 5 (2 in edit mode) | 294 | 10 | 1 |
| Mounts | 1 | 107 | 0 | 0 |
| Services | 13 | 451 | 5 | 1 |
| **All** | 98 (12 edit, 86 Play) | 4925 | 102 | 28 (3 throw) |

Edit pass: 12 suites, 319 passed, 4 failed (CoreConditionRules 2, DazeMonster 1, GhoulTouch 1), 0.2 s. Play pass: 86 suites, 4606 passed, 98 failed, 2.9 s. The causes are in section 2.5 and the baseline. The Play pass also destroyed thousands of UI-001 `PooledLogMsg` orphans (2009 from BullRushRulesTests alone), restored `SquareGrid.Instance` after AIProfileFramework, FlamingSphere, GustOfWind and PostCombatLootCollection, and destroyed 5 `FlamingSphere_FlamingWizard` objects that FlamingSphereRulesTests leaves behind. Gameplay beyond the suites was not verified in Play mode.

Same day, after the review of the baseline: the GrappleDamageRulesTests helpers set `BaseAttackBonusOverride` instead of the ignored `Stats.BaseAttackBonus` (CHR-068) and the Escape Artist pin-escape test got enough ranks to beat the +30 grapple check it now faces (TST-027). A Play-mode sweep of GrappleDamage over 67 seeds (the baseline seed and 66 others) gave 225 pass, 10 fail every time with the same 10 labels, so its `flakyFailures` is 0; the 3 pin-duration labels are now CMB-120. The same sweep over MagicCircle, SunderInventoryRemoval and MountSystem set the counts in TST-031. Then a full Play pass in the session that ran the sweeps and another in a fresh Play session each gave 86 suites, 4606 passed, 98 failed, verdict OK, 0 regressions, 0 stale, and an edit pass 12 suites, 319 passed, 4 failed, verdict OK, scene not dirty. Compile check: 0 errors, 508 warnings.

Run of 2026-10-07 with the committed runner (Unity 6000.4.0f1 through the MCP, seed 20261007). Edit pass: CauseFear 36 pass, 0 fail and Scare 77 pass, 0 fail, verdict OK; the active scene was not dirty and the temporary scene was closed. Play pass with `filter=SpellUtilitiesTests,FlankingReachRulesTests,RapidShotTests,CharmPersonRulesTests`: SpellUtilities 26/0, FlankingReach 80/0, RapidShot 48/6 (TST-028; a REGRESSION until the baseline lists it), CharmPerson an error with `TargetParameterCountException` (TST-001, matching `expectThrow`). Two runs with the same seed and one with `seed=0` gave identical counts (the `seed=0` agreement shows nothing about dice: a `seed=0` pass replays the session's restored `Random.state`); a run split by `maxSeconds` and resumed with `after=` merged to the same totals. `Phase5IntegrationTests` gave 158/3 (ENC-007), wrote its dump to `Logs/` and left nothing in the project root. The other 91 suites were not run with the runner yet.

Rerun of 2026-10-07 after the runner review fixes (occurrence-counted failure labels, UI-001 orphans destroyed, stray files only deleted on an allowlist, `RUNNING` and `currentSuite` markers, checked resume, `STALE_CODE` refusal, NEW failing suites as regressions). Edit pass: CauseFear 36/0 and Scare 77/0, OK, active scene not dirty. A `touch` of the runner file made the next edit pass refuse with `STALE_CODE`; `AssetDatabase.Refresh()` then compiled nothing and the refusal stayed, as documented above. `after=` on a complete report and `after=` without the original filter were refused. Play pass, same four-suite filter: SpellUtilities 26/0, FlankingReach 80/0 (588 `PooledLogMsg` UI-001 orphans destroyed; the later suites in the same session ran normally), RapidShot 48/6 (5 labels, "Rogue should have Rapid Shot feat" counted twice), CharmPerson `TargetParameterCountException`. A `maxSeconds=0.01` pass resumed twice with the printed options merged to the same 162/6; a simulated freeze (the JSON edited to `currentSuite=RapidShotTests`) resumed with `after=` that suite and warned that it had no result. `Phase5IntegrationTests` 158/3 (each label counted twice, because the suite prints its failures again at the end), no file left in the project root. Unity was left in edit mode.

The runs below predate the committed runner and used an ad hoc log hook.

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

Run of 2026-10-07 after the NPC melee attack started running step by step (CMB-102 step 3; `TestAttackSequenceStepResolverUsesStepBab` and `TestNpcMeleeSequenceTripThenAttacks` added, 199 assertions): `GrappleDamageRulesTests` 188 pass, 11 fail; `AIProfileFrameworkTests` 43 pass, 3 fail (TST-025, unchanged). The five new assertions passed (shared step resolver BAB, adjustment and natural step; NPC trip replacing the first attack followed by attacks at +6 and +1; one attack after the move action). The grapple failures are TST-027: 7 stale log texts, 3 pin-duration assertions and 1 chance failure (non-monk lethal grapple damage). `TestNpcMeleeSequenceTripThenAttacks` calls the private `GameManager.PerformNPCMeleeAttackSequence` by reflection (TST-006). A re-run after the review fixes (a natural-weapon creature keeps its remaining natural attacks after a substitute; one more assertion, 200 in all): `GrappleDamageRulesTests` 187 pass, 13 fail, with the new claw/claw/bite assertion passing; the failures are TST-027 (7 stale log texts, 3 pin-duration assertions) plus 3 chance failures (Monk default grapple damage, unarmed grapple damage dice, Use Opponent's Weapon success). `AIProfileFrameworkTests` 43 pass, 3 fail (TST-025, unchanged). A second `GrappleDamageRulesTests` run in the same Play session showed more pin and chance failures, as expected from objects left by the first run (section 2.6).

Run of 2026-10-07 after bull rush became a standard action or charge end for every creature (CMB-102; the iterative bull rush test and the `PcUiAlsoReplacesAttack` assertion deleted, `TestBullRushIsAStandardAction` and `TestBullRushTargetLimits` added, 209 assertions; then the review fixes: footprint-aware push and follow, the post-move legality check in `ResolveChargeBullRush`, the PC refusal before the turning break, and the NPC executor refusing a standing `BullRushCharge`): `GrappleDamageRulesTests` 196 pass, 13 fail (an earlier run in the same session had 16 failures, all of the same TST-027 kinds); `FlankingReachRulesTests` 80 pass, 0 fail. All 14 new bull rush assertions passed: standard action cost for a fresh, an attacking and a moved creature; the NPC executor refusing a target two squares away through `CanBullRush`'s adjacency rule without spending anything, then bull rushing the same target once adjacent and spending the standard action; Medium vs Large allowed; Medium vs Huge, Small vs Large, swarm, incorporeal, two squares away and a grappling attacker refused; the charge planner leaving adjacency to the path. All 13 failures are TST-027 kinds: stale log texts (the 4 pinner block messages, unarmed grapple damage, Use Opponent's Weapon), the 3 pin-duration assertions, and chance failures in the Improved Unarmed Strike and Monk grapple damage. No NPC bull rush charge was run, no Large push was exercised in a test, and the PC bull rush buttons were not clicked in Play mode.

Run of 2026-10-07 after the bull rush push and follow became one RAW resolution (CMB-109 and CMB-110 fixed; CMB-098's bull rush part done; new suite `Tests.Maneuvers.BullRushRulesTests`, 48 assertions), rerun after the review fixes (push and follow interleaved, no follow without movement or after a 5-foot step, follow AoOs seeded with the charge and initiation provokers, diagonal parity continued from the charge path): `BullRushRulesTests` 48 pass, 0 fail; `GrappleDamageRulesTests` 196 pass, 13 fail on a clean rerun (TST-027 kinds: the 4 pinner block messages, unarmed grapple damage log and dice, Use Opponent's Weapon log, the 3 pin-duration assertions, and chance failures in the Improved Unarmed Strike and Monk grapple damage; a first run in the same session had 19, the extra 6 being chance failures of the opposed pin and escape checks); `FlankingReachRulesTests` 80 pass, 0 fail. The new suite checks `BullRushRules.GetMaxPushSquares` (not following 1; margin 12, limit 6 -> 3; limit cap; no movement -> no follow; failure 0; diagonal cost, also continued from an odd charge-diagonal count), the movement limit (speed; charge twice the speed minus the path cost; 0 after a 5-foot step or while prone), the push direction for Medium and Large footprints, and Play-mode scenarios that call the private `GameManager.ResolveBullRushPushAndFollow`, `ResolveBullRushStepAoOs` and `ResolveBullRushAoO` by reflection (TST-006) with Stats-built actors on the scene grid, registered in `GameManager.NPCs` for the test and removed (grid occupancy cleared) afterwards: a Medium attacker pushes a Large defender east and follows 1; a Large attacker pushes 2 and follows 2; the attacker's ally takes an AoO as the defender leaves its threatened square while the attacker takes none; the defender, beside the attacker's departure square, takes no AoO as the attacker follows (control: it does without the partner exclusion); misdirection with the d100 fixed through `GameManager.BullRushMisdirectionRollOverride` (25 strays, 26 does not, the defender's own AoO never strays); an AI profile that stays pushes exactly 1 square with margin 10 while the default AI pushes 3 and follows 3; a Large follower blocked by a third creature after the first square leaves the defender exactly 1 square away; a prone attacker pushes 1 and does not follow; an opponent that already provoked this action gets no follow AoO (control: it does otherwise). Not verified in Play mode: the PC prompt (buttons and 0-9/Esc keys), a charge bull rush end to end (including the Disrupted branch when a strayed initiation AoO kills the defender), a follow stopped by an AoO (trip or drop), and the victory, defeat and turn-ending paths after a push or follow AoO drops someone.

Run of 2026-10-07 after exceptional stability moved into creature data (CMB-085; `NPCDefinition.IsExceptionallyStable` and `CharacterStats.IsExceptionallyStable`, the rider check of CMB-111, the skeleton and zombie templates clearing the flag on a humanoid base, and the move-through overrun check routed through the shared helpers with a d20 test hook; five tests added to `GrappleDamageRulesTests`, 26 assertions, 235 in all): `GrappleDamageRulesTests` 223 pass, 12 fail on a fresh Play session; `AIProfileFrameworkTests` 43 pass, 3 fail in the same session (the same three TST-025 assertions as before). All 26 new assertions passed: worg and dwarf_warrior flagged and human_warrior not; the wolf alias, fiendish_dire_bear and skeleton_wolf inherit the flag; `Clone` keeps it; a worg clone with the skeleton and fiendish templates applied through `CreatureTemplateRegistry.ApplyTemplatesClone` keeps it and the database worg is untouched; a worg zombie keeps it; dwarf_warrior clones with the skeleton or zombie template lose it and the database dwarf keeps it; a Stats-built actor with the flag gets `StabilityBonus` 4 on `RollBullRushDefenderCheck(fixedRoll: 10)`, a bull rush total 4 higher and a trip or overrun defender modifier 4 higher; a Race=Dwarf actor with the flag gets exactly 4; mounted on a light horse it gets 0, and 4 again after dismounting; `InitializeNPCFromDefinition`, called by reflection (TST-006) on a worg clone, copies the flag so the spawned worg gets +4; `GameManager.ResolveOverrunOpposedCheck`, called by reflection with fixed d20 rolls, matches the shared helpers, gives a tie to the higher modifier, and counts defender stability and Improved Overrun. The 12 failures are TST-027 kinds: the 4 pinner block messages, the unarmed grapple damage log and dice, the Use Opponent's Weapon log, the 3 pin-duration assertions and the 2 Improved Unarmed Strike nonlethal grapple assertions (chance). The data checks were also run as an edit-mode `Unity_RunCommand` (153 of 389 database entries are flagged). Not verified in Play mode: an actual trip, overrun or bull rush against a flagged NPC in a played encounter.

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
