> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-02.

# Architecture

This document explains how the D&D 3.5e tactical combat prototype under `Assets/Scripts` is put together: one Unity scene built entirely at runtime by `SceneBootstrap`, driven by the `GameManager` partial class and the services on its GameObject. Its primary reader is an AI coding agent maintaining the code, its secondary reader the human owner, and the sections follow the main subsystems in the order a session runs through them: runtime loop, combat and grid, spells, characters and creatures, items and economy, then AI, encounters and UI. Paths are relative to `Assets/Scripts/` unless they start with `Assets/`, a bare file name is meant to be unique (Glob `**/<name>`), `GameManager.cs` means `_Core/GameManager.cs`, and line numbers are as of the commit above and drift quickly, so grep the member name first. Statements marked **[KI]** or "tracked in KNOWN_ISSUES.md" are known bugs or rules deviations detailed in [KNOWN_ISSUES.md](KNOWN_ISSUES.md), and behaviour that was not verified in Play mode is marked as such. Step-by-step change procedures are in [DEVELOPMENT_RECIPES.md](DEVELOPMENT_RECIPES.md) and test practice in [TESTING.md](TESTING.md).

## Parts

This file covers the runtime loop, composition, services and cross-cutting conventions. Each subsystem has its own part:

| Part | Covers |
|---|---|
| [Combat & grid](architecture/combat-and-grid.md) | Grid, movement, turns, attack resolution, damage, AoO, conditions, maneuvers, reactions, combat log |
| [Spells](architecture/spells.md) | Spell data and IDs, casting resources, the four cast pipelines, handlers, saves/DC/CL, durations, areas, metamagic, domains, summoning |
| [Characters & creatures](architecture/characters-and-creatures.md) | CharacterController vs CharacterStats, derived stats, feats, skills, classes, XP, NPC database, templates, dragons |
| [Items & economy](architecture/items-and-economy.md) | ItemData/ItemDatabase, inventory, consumables, enchantments, rings/rods/wondrous/staves, store, crafting, treasure |
| [AI, encounters & UI](architecture/ai-encounters-ui.md) | NPC AI pipeline and profiles, encounter sources and spawning, UI construction, debug tools |

The parts describe how the code is built. For content inventories, status against the 3.5e rules and backlogs, see the systems docs in [systems/](systems/): [AI](systems/AI.md), [rules coverage](systems/RULES_COVERAGE.md), [encounters](systems/ENCOUNTERS.md), [party management](systems/PARTY_MANAGEMENT.md), [spells and metamagic](systems/SPELLS_AND_METAMAGIC.md), [creatures](systems/CREATURES.md), [magic items](systems/MAGIC_ITEMS.md). The index with when to read each is in [README.md](README.md#systems-docs).

## Overview & runtime loop

### What the game is and the runtime loop

A single-scene Unity 6000.4.0f1 prototype of D&D 3.5e tactical combat. A party of up to 4 PCs fights up to 15 enemies on a 20x20 square grid (1 square = 5 ft), using initiative, the 3.5e action economy, spells, items and maneuvers. The program is an endless loop of encounters. It has no main menu, no save/load and no scene changes: `grep SceneManager` finds nothing.

```
Play -> SceneBootstrap.Awake builds everything
     -> CharacterCreationUI (first screen) -> creation level-ups (LevelUpUI)
     -> PromptEncounterSelection --------------------------------------------+
     -> OpenPreCombatHubPhase (store / stash / spell prep / crafting)        |
     -> StartCombat -> TurnService rounds -> PC turns / NPC turns            |
          |-- EvaluateCombatEnd: enemy side out -> HandleCombatVictoryDetected
          |      -> loot window -> XP -> level-ups -> full rest -> ----------+
          |      -> (loot window "exit") -> ExitCombatLoopToMenu = quit
          '-- EvaluateCombatEnd: party side out -> defeat screen
                 -> New Party -> CharacterCreationUI again (or Quit)
```

| Step | What happens | Where to look |
|---|---|---|
| Scene load | Unity loads `Assets/Scenes/MainScene.unity`. `SceneBootstrap.Awake` builds the camera, grid, characters, UI and `GameManager`. | `_Core/SceneBootstrap.cs:24` |
| Start | `GameManager.Start` generates the grid and hooks the creation callbacks, because `CharacterCreationUI` is always present. The `else` branch (`SetupCharacters` + `PromptEncounterSelection`) is unreachable in this scene. | `_Core/GameManager.cs:610` |
| Character creation | Step-by-step creation, Quick Start (premade selection or party builder), or "Play Now!" (Fighter/Rogue/Cleric/Wizard). All paths (four call sites, flow sources `StandardFlow`, `PremadeSelection`, `VeryQuickStart`, `QuickStartPartyBuilder`) end in `CharacterCreationUI.NotifyCreationComplete`, which calls `OnCreationComplete4`. That runs `GameManager.OnCharacterCreationComplete4` -> `SetupCreatedCharacters` -> `ProcessCreationLevelUpsThenPromptEncounterSelection`. New characters default to `TargetLevel = 3`, so the level-up UI runs before the first fight. | `UI/CharacterCreation/CharacterCreationUI.cs:3364`; `GameManager.cs:735, 753, 1863` |
| Encounter selection | `PromptEncounterSelection` opens `EncounterSelectionUI`, which offers presets, a random generator, DMG tables and a custom builder. A preset leads to `ApplyEncounterPreset`; a generated or custom encounter leads to `ApplyRandomEncounter`; Cancel falls back to `goblin_raiders`. All of them then call `SetupEnemyEncounter` and `OpenPreCombatHubPhase`. | `GameManager.cs:785, 1690, 1769`; `_Core/GameManager.NPCSetup.cs:128` |
| Pre-combat hub | `PreCombatHubUI` offers Store, Inventory/Stash, Spell Prep, Crafting, Start Encounter and Back. `StartEncounterFromPreCombat` warns if a prepared caster has not prepared spells, then `ForceStartEncounterFromPreCombat` locks the stash and calls `StartCombat`. | `GameManager.cs:1403-1647` |
| Combat | `StartCombat` calls `TurnService.StartCombat` (`GameManager.cs:3728`), which rolls initiative and raises `OnNewRound(1)`, `OnInitiativeCountReached` (each initiative count, where turn-relative durations tick, CMB-006) and `OnTurnStarted`. `GameManager.OnTurnStarted` routes a controllable character to `StartPCTurn` and anything else to the coroutine `SingleNPCTurnFromInitiative` (AI). `NextInitiativeTurn` advances. | `GameManager.cs:3634, 3766, 3806, 3950, 3976`; `_Core/GameManager.NPCTurns.cs:35` |
| Combat end | One check for both sides, `GameManager.EvaluateCombatEnd` (`_Core/GameManager.CombatEnd.cs`), with the predicate `CombatEndRules.IsOutOfFight` (`Combat/Core/CombatEndRules.cs`): dead, dying or unconscious is out (unconscious includes stable, nonlethal knockout, asleep and petrified), a disabled creature is in, regeneration makes no exception; a side (team) is out when every active member is out and it has or had a member in this combat (owner definition 2026-10-07; a tie is a defeat and control does not change side, both pending the owner, CORE-038). It runs after each attack, spell, maneuver and AoO resolution that checked before (PC and NPC attack sites whenever the target is out, of either team), after every NPC cast (`TryNPCPerformSpellCastForAI`) (`CheckCombatVictory` wraps it with the XP registration), in the PC menu after every PC action (`ShowActionChoices`), at turn start after the turn-start effects, after every NPC turn and at every turn boundary (`NextInitiativeTurn`, which then starts no further turn). Victory calls `HandleCombatVictoryDetected` -> `BeginPostCombatLootCollection`. | `_Core/GameManager.CombatEnd.cs`; `_Core/GameManager.LootCollection.cs:25` |
| Loot / XP / level-up | Loot window (`LootCollectionUI`), then `ShowPostCombatXPFlow` (`ExperienceCalculator`, `CombatEndXPUI`), then `CheckAndShowLevelUps` (`LevelUpUI`), then `ContinueToRestAndNextCombat`. | `GameManager.LootCollection.cs:175, 290` |
| Auto full rest | `RestorePartyAfterCombat` restores HP (no `IsDead` skip, so dead but active PCs appear to be revived; not verified in Play mode), spell slots and daily uses, clears effects and summons, and moves PCs to (3,6)/(3,9)/(3,12)/(3,15). `ReturnToEncounterSelection` -> `ResetCombatStateForNextEncounter` -> `PromptEncounterSelection`. | `GameManager.cs:868, 1253, 1076` |
| Exit | The loot window's exit button calls `ExitCombatLoopToMenu`. Despite its name it stops Play mode (editor) or calls `Application.Quit()`. | `GameManager.cs:1271` |
| Defeat | `HandleCombatDefeatDetected` sets `CombatOver` and opens the code-built defeat screen (`CombatUI.ShowDefeatPanel`; `WaitingForDefeatChoice` blocks world input). New Party (`StartNewPartyAfterDefeat`) clears the fight, summons and grapples, resets gold and stash (`EconomyService.ResetForNewParty`), reactivates the four party slots and reopens character creation (`CharacterCreationUI.ReopenForNewParty`); Quit calls `ExitCombatLoopToMenu`. | `_Core/GameManager.CombatEnd.cs` |

Two behaviours look like bugs and are tracked in [KNOWN_ISSUES.md](KNOWN_ISSUES.md). First, `TurnService` raises `OnCombatEnded` only when no living combatant is left, so an ordinary victory never runs `GameManager.OnCombatEnded` (`GameManager.cs:3838`), and its cleanup is skipped: `EffectService.ClearAll`, `AIService.ClearAuraSaveImmunities`, `ConditionService.CleanupOnCombatEnd`, the bardic-music stop and the `CombatEndedEvent` publish. (`RestorePartyAfterCombat` separately clears grease, area/wind effects, Mirror Image, `MeleeReactionService` and `CurseTracker`.) Second, damage-over-time and area ticks (Flaming Sphere, Wall of Fire, turn-start ticks) run no combat-end check of their own; a tick kill is caught by the shared check at the next turn start, PC action or turn boundary, and both turn starters return when the phase is already `CombatOver` (CORE-003, not verified in Play mode).

Developer shortcuts (22 hard-wired test encounter presets, the F12 Spell Testing panel, Left Ctrl+H) are described in [UI: debug and test tools](architecture/ai-encounters-ui.md#ui-debug-and-test-tools). The I/K/C/Esc keys are handled by `InputService`; see [UI: hotkeys](architecture/ai-encounters-ui.md#ui-hotkeys).

### Composition

#### MainScene

`Assets/Scenes/MainScene.unity` has exactly two root objects:

- `Main Camera`: orthographic, size 10, position (8,7,-10).
- `GameBootstrap`: a Transform plus one MonoBehaviour, `SceneBootstrap`, referenced by script GUID at line 255.

The project has no prefabs (0 `.prefab` files) and no project-authored ScriptableObject assets (the only `.asset` ScriptableObjects are TextMesh Pro's own font/settings files under `Assets/TextMesh Pro/`). Everything else is created at runtime.

The scene references `SceneBootstrap` by the GUID in `Assets/Scripts/_Core/SceneBootstrap.cs.meta`. `.meta` files have been committed since 2026-10-03, so the reference resolves on any clone; keep each `.meta` next to its asset when moving or renaming files.

#### SceneBootstrap (`_Core/SceneBootstrap.cs`)

`Awake` runs these steps in order:

1. `SetupCamera`: reuses `Camera.main` and adds `CameraController` with zoom 0.5-2.0.
2. `CreateSquareGrid`: a `SquareGrid` GameObject with a procedurally drawn cell sprite.
3. `CreateCharacters`: `PC_Hero1..4` (Player team, controllable) and `NPC_Enemy_0..14` (Enemy team, not controllable). Each is a `SpriteRenderer` plus a `CharacterController` with no stats yet; the enemy slots come from `GameManager.CreateNPCPoolSlot` (`initialEnemySlots = 15`), and `SetupEnemyEncounter` adds more for a larger encounter (ENC-001).
4. `CreateUI`: Canvas (ScreenSpaceOverlay, `CanvasScaler` 1920x1080), an `EventSystem` if missing, `CombatUI` and its panels (initiative, party, 3 NPC panels, combat log, action buttons, feat toggles) and `SpellTestingPanel`.
5. `SetupGameManager` (:997):
   - `gameObject.AddComponent<GameManager>()` (:1001).
   - Assigns `Grid`, `PC1..4`, `NPC`, `PCs`, `NPCs` and `CombatUI`.
   - Adds the other UI components to the Canvas and calls their `BuildUI`: Inventory, CharacterSheet, QuickItemUse, StaffSpellSelection, Wish, CharacterCreation, Skills, FeatSelection, SpellSelection, SpellPreparation. Adds `LootCollectionUI`.
   - Calls `Init()` on the Ring, Wondrous, Rod, Staff, Race, Item, Feat, Spell and CraftableItem databases.
   - Starts the `WireButtons` coroutine. After one frame it binds the `CombatUI` action buttons to `GameManager.Instance.On*ButtonPressed` (:1101).

Other GameObjects created at runtime:

- In `GameManager.Start`: `PathPreview`, `HoverMarker`, `CharacterHoverTooltipUI` (via `EnsureInstance`, parented to the Canvas), `PlanarTravelSystem`, `CreatureTrapSystem`.
- On first use, added to the GameManager's GameObject when no existing instance is found (`FindObjectOfType`, or `StoreInventory.Instance` via `EconomyService.EnsureStoreInventoryInitialized`): `EncounterSelectionUI`, `PreCombatHubUI`, `StoreInventory`, `PreCombatInventoryUI`, `StoreUI`, `CraftingWorkshopUI`.
- On first use, on new GameObjects under the Canvas: `CombatEndXPUI` and `LevelUpUI` (`GameManager.LootCollection.cs`).
- Self-creating singletons: `AreaEffectManager`, `WindEffectManager`, `ExperienceCalculator`, `GameSettings`.
- `DebugCommands`, created by `[RuntimeInitializeOnLoadMethod]` with `DontDestroyOnLoad`.

#### GameManager singleton and lifecycle

`GameManager` (`_Core/GameManager.cs:23`) is a MonoBehaviour on the `GameBootstrap` GameObject. It is a partial class split over 55 files (about 53,000 lines; `_Core/GameManager.ScenarioHarness.cs` is editor-only), and it owns all session state:

- the party and enemy lists;
- `CurrentPhase` (`TurnPhase {PCTurn, NPCTurn, CombatOver}`; a backing field whose setter raises the inert `ScenarioHooks.PhaseChanged` on a change) and `CurrentSubPhase` (13-value `PlayerSubPhase`), both at :207-241;
- the pending-action and targeting fields;
- summons, test flags and loop statistics.

`GameManager.Instance` is assigned in `Awake` (:483-490). A second instance destroys its own GameObject. There is no `DontDestroyOnLoad`. `CurrentPhase` defaults to `PCTurn` (enum value 0) before the first combat, so a raw `CurrentPhase == PCTurn` check is true out of combat.

Awake-before-wiring caveat: `AddComponent<GameManager>()` runs `GameManager.Awake` immediately, before `SceneBootstrap` assigns `Grid`, `PCs`, `NPCs` and `CombatUI`. Consequences:

- In Awake, `MovementService.Initialize` receives a null `Grid`, and `InputService` receives a null camera. `Start` initializes both again (:612-620).
- `BaseCombatManeuver.Initialize` caches `combatUI = null`.
- Services read `CombatUI` through lambdas (`() => CombatUI`) for this reason.
- New code that needs scene references must read them in `Start` or later, or take a provider.

`Update` (:2189) does three things:

- It ticks poison timers every frame, in real time.
- It returns early while any of `WaitingForCharacterCreation`, `WaitingForEncounterSelection`, `WaitingForPreCombatInventory`, `WaitingForLootCollection` or `WaitingForDefeatChoice` is set. These flags are how modal phases block world input.
- Otherwise it feeds `InputService` and updates the hover previews.

`OnDestroy` unsubscribes events and calls `Cleanup` on the AI, CombatFlow and maneuver components.

#### How services are created

`GameManager.Awake` (:494-575) first calls `InitializeDiseaseAndPoisonDatabases`, then adds each MonoBehaviour service to the GameManager's own GameObject (not a separate one) with `_x ??= gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>()`, then calls `Initialize(...)`. The `Initialize` signatures differ:

- `AIService` and `CombatFlowService` take `(this)`.
- `EconomyService` takes `(this, () => CombatUI, partyGold)` (`partyGold` defaults to 1000).
- `SummoningService` and `EncounterService` take `(this, () => CombatUI)`; `SpellApplicationService` takes `(this, () => CombatUI, _conditionService)`.
- `MovementService` takes `(Grid, GetAllCharacters)`; `ConditionService` takes `Func<List<CharacterController>>` only.
- `InputService` takes a camera plus four callbacks, then `RegisterClickHandler` is called for five `InputMode` values.
- `DispelMagicService` takes five `Func`/`Action` callbacks.

`TurnService` has no `Initialize`. `GameManager` subscribes to its `OnTurnStarted`, `OnNewRound`, `OnInitiativeCountReached`, `OnInitiativeCountRemoved` (re-anchors the durations of a creature that leaves the order, CMB-006) and `OnCombatEnded` events. ConditionService has no subscription of its own (`BindTurnService` was removed on 2026-10-09, CMB-006); GameManager calls it once per turn, per initiative count and per round boundary.

Awake also creates:

- 4 plain C# behavior controllers: Confused, Charmed, Fascinated, Frightened.
- 5 empty maneuver components (see [Dormant and unused infrastructure](#dormant-and-unused-infrastructure)).
- `CommandProcessor` (assigned with plain `=`, not `??=`).

None of these exist in the scene file. The `??` operator bypasses Unity's overloaded null check, which is harmless here only because the fields start null.

### GameManager partial files

There are 55 files, found with `grep -rlE '^\s*public partial class GameManager\b' Assets/Scripts`. Both naming styles occur: `GameManager.X.cs` and `GameManager_X.cs`. Six files with system-sounding names are also partials.

| Folder / file | Lines | Responsibility |
|---|---|---|
| **_Core/** (12) | | |
| GameManager.cs | 11,467 | Main partial: singleton, state and enums, Awake/Start wiring, creation callbacks, encounter selection, hub, preset/random setup, rest and reset, `Update`/input routing, `StartCombat`, turn callbacks, `StartPCTurn`/`ShowActionChoices`, item/scroll/wand/staff use, 31 of the 51 GameManager `On*ButtonPressed` handlers, `*ForAI` wrappers (~10879-11026), path and hover previews. |
| GameManager.CombatActions.cs | 2,697 | `OnCellClicked` routing by sub-phase; movement with AoO; attack target clicks; off-hand, full attack and special-attack execution; hand-off to `CombatFlowService`; `EndActivePCTurn`. |
| GameManager.CombatEnd.cs | 268 | The shared combat-end check `EvaluateCombatEnd` (both sides, by team, through `CombatEndRules`; `GetCombatEndSides` remembers which sides had members this combat), `CheckCombatVictory`, XP registration of defeated enemies, victory and defeat handling, the defeat screen and `StartNewPartyAfterDefeat` (CORE-011, CORE-037, CORE-034, CORE-001). |
| GameManager.Damage.cs | 79 | The shared damage path for everything but weapon hits (SPL-004): `DealDamage` (`ApplyIncomingDamage` with a `DamagePackets` packet, then `AfterDamageTaken`: concentration and death), `ApplyDamagePacket` (mitigation only, for pipelines that read `SpellResult`), `DescribeMitigation` (log suffix). The combat-end check stays with the caller. |
| GameManager.CombatFlowAccessors.cs | 130 | `Combat_*` getters, setters and forwarders over private state. |
| GameManager.LootCollection.cs | 800 | Post-combat loot, XP flow, level-up sequence, `ContinueToRestAndNextCombat`. |
| GameManager.NPCSetup.cs | 1,027 | `SetupEnemyEncounter` (pool growth `EnsureNPCPoolSize` and `CreateNPCPoolSlot`, spawn squares through `EncounterSpawnPlacement`, ENC-001), `ResetCharacterSlotForSpawn` and `ResetPCSlotForNewCharacter` (CRE-046), `InitializeNPCFromDefinition`, `BuildRuntimeAIProfile`, spawn overrides. |
| GameManager.NPCTurns.cs | 1,853 | `SingleNPCTurnFromInitiative`; summon AI; NPC attacks, full attacks and spellcasting (`TryNPCPerformSpellCast`); breath weapon; grab/trip helpers. |
| GameManager.TestConfigs.cs | 1,875 | 22 `Configure*TestParty` methods, `RestoreStandardPartyLayout`. |
| GameManager.TestPanel.cs | 110 | F12 panel bridge: `TestCastSpellFromPanel`, `CleanupTestPanelCast`. |
| GameManager.ScenarioHarness.cs | 579 | Editor-only (`#if UNITY_EDITOR`) `Harness_*` entry points for the scenario harness: build the party from creation data or exact `CharacterStats`, spawn enemies at exact squares, start, halt and reset combat (the reset also releases grapple links, CMB-038), dump turn state and the open prompt, call the PC menu callbacks (open the Special Attack menu and click a maneuver button as a player would, pick a spell, cancel a selection) and answer the AoO prompt. No game code calls it. |
| GameManager.TreasureGeneration.cs | 158 | `GeneratePostCombatTreasure` (EL -> `TreasureGenerator.Generate`). Its `ShowTreasureUI` path has no external callers. |
| **Spell/Resolution/** (25) | | |
| GameManager.SpellCasting.cs | 8,957 | Spell targeting and casting orchestration (`BeginPendingSpellTargeting`, `PerformSpellCast`), summon spawn and despawn, `TickSummonDurations`, `TickAllSpellDurations`. |
| GameManager.DispelCounterspell.cs | 70 | `_dispelMagicService` field plus thin delegates to `DispelMagicService`. |
| GameManager_Spells_A/B/C/E/L/N/V.cs | 60-234 each | One or two spells each: Align Weapon; Blink, Bestow Curse; Contagion; Enervation; Lesser Globe; Neutralize Poison; Vampiric Touch. |
| GameManager_Spells_D.cs | 780 | Death Ward, Divine Power, Dismissal, Daylight, Displacement, Death Knell, Dimension Door, Dimensional Anchor, Black Tentacles. |
| GameManager_Spells_F/G/H/K/M/P.cs | 189-325 each | Freedom of Movement, Flame Arrow, Fire Shield; Greater Magic Weapon, Greater Invisibility; Hold Person, Halt Undead, Haste; Keen Edge; Mass Enlarge/Reduce, Magic Vestment; Prayer, Poison, Phantasmal Killer. |
| GameManager_Spells_I.cs | 898 | Imbue with Spell Ability (also `OnUseImbuedSpellButtonPressed`), Invisibility Purge/Sphere, Ice Storm. |
| GameManager_Spells_R.cs / _S.cs | 603 / 760 | Remove Disease/Curse/Blindness, Repel Vermin, Rage, Ray of Exhaustion, Rainbow Pattern / Searing Light, Spell Immunity, Slow, Shout, Silence, Sound Burst, Spiritual Weapon, Shield Other. |
| GameManager_Spells_W.cs | 1,160 | Wind Wall, Wall of Fire/Ice resolution, Resilient Sphere, Wish (`HandleWishSpellCast`, `HandleItemWishCast`). |
| GameManager_Spells_Cantrips.cs | 350 | Utility cantrips (Ghost Sound, Create Water, Message, Prestidigitation, Mending, ...). |
| GameManager_Spells_MagicFang.cs | 112 | Magic Fang. |
| GameManager_Spells_Phase1.cs / _Phase2.cs | 1,165 / 803 | Higher-level spells added for staves (Cone of Cold, Chain Lightning, Hold/Charm Monster ... / Disintegrate, Sunburst, Earthquake, Heal, Resurrection, Plane Shift ...). |
| GameManager_Spells_Shared.cs | 337 | Fireball, Lightning Bolt, Call Lightning; the alignment bursts (Chaos Hammer, Holy Smite, Order's Wrath, Unholy Blight); cleric spell duration ticks. |
| **Spell/Special/** (8) | | |
| GameManager_ConcealmentAreas.cs | 479 | Obscuring Mist, Fog Cloud, Darkness, Sleet Storm, Stinking Cloud, Solid Fog, Glitterdust and Web areas. |
| GameManager_FlamingSphere.cs | 675 | Flaming Sphere control (`OnControlFlamingSphereButtonPressed`), turn-start damage, AI control. |
| GameManager_Grease.cs | 836 | Grease area/object/armor modes, ticking, clearing. |
| GameManager_HolyAreas.cs | 304 | Consecrate / Desecrate. |
| GameManager_MirrorImage.cs | 827 | Mirror Image clones, end-of-turn swap selection, cleanup. |
| GameManager_WallOfFire/Ice/Force.cs | 681 / 315 / 208 | Wall placement modes and targeting. |
| **Spell/Domain/** (3) | | |
| GameManager_DomainPowers.cs | 987 | Cleric granted powers (`OnDomainPowerButtonPressed`, `TickDomainPowerDurations`). |
| GameManager_DomainSpells.cs / _DomainAreaSpells.cs | 569 / 256 | Domain spells (Hold Animal, Heat Metal, ...) and domain area spells (Entangle, Spike Stones, ...). |
| **Combat/Maneuvers/** (4) | | |
| GrappleSystem.cs | 2,024 | Grapple suite (`OnGrapple*ButtonPressed`), also `OnOverrunButtonPressed`. |
| OverrunSystem.cs | 943 | Overrun destination selection and resolution. |
| StandardManeuvers.cs | 1,210 | Disarm and sunder, including dual-wield prompts; the shared bull rush push and follow (`ResolveBullRushPushAndFollow`, `ExecuteBullRushMovement`) and overrun's `TryPushTargetAway`; some trip/feint helpers. |
| SupportActions.cs | 2,031 | Aid Another, Charge. |
| **Combat/Special/** (2) | | |
| TurnUndeadSystem.cs | 1,005 | Turn/rebuke undead and turned-state trackers. |
| TemplateSmiteSystem.cs | 247 | Celestial/fiendish template smite. |
| **Encounters/** (1) | | |
| GameManager.DungeonEncounters.cs | 237 | Bridge from DMG dungeon tables to combat. No external callers: `EncounterSelectionUI.OnDMGTablesPressed` uses its own path. |

### Service layer

All MonoBehaviour services live on the GameManager GameObject and are created in `GameManager.Awake`. Status values: **live** means used in normal play; **partial** means live, but a significant part of its API has no production callers; **unused copy** means a parallel implementation of logic that actually runs elsewhere.

| Class | Path | Kind | Purpose | Status |
|---|---|---|---|---|
| TurnService | Services/TurnService.cs | MonoBehaviour | Initiative order, round counter, turn events. | live |
| InputService | Services/InputService.cs | MonoBehaviour | Per-frame mouse and key input, mode-keyed click handlers. | live (events `OnWorldClicked` and others have no subscribers) |
| MovementService | Services/MovementService.cs | MonoBehaviour | Movement range, pathfinding wrappers, AoO and 5-ft step rules. | live |
| ConditionService | Services/ConditionService.cs | MonoBehaviour | Mirrors CharacterController conditions; turn and round expiry; escape saves. | live |
| AIService | Services/AIService.cs | MonoBehaviour | NPC turn coroutine (`ExecuteNPCTurn`), targeting, tactical routines. | live |
| CombatFlowService | Services/CombatFlowService.cs | MonoBehaviour | Attack pipelines (single, full, off-hand), range info, attack log. | partial (the `ExecuteAttack`/`RollAttack` "contract" methods, Whirlwind and Manyshot have no callers) |
| EconomyService | Services/EconomyService.cs | MonoBehaviour | Party gold, `PartyStash`, creates `StoreInventory`. | partial (buy/sell pricing unused; `StoreInventory`/`StoreUI` price items themselves) |
| DispelMagicService | Services/DispelMagicService.cs | MonoBehaviour | Dispel checks, targeted and area dispel, counterspell resolution. | live (nothing in normal play readies a counterspell) |
| SummoningService | Services/SummoningService.cs | MonoBehaviour | Summon registry and ticking. | unused copy (only `LionsShieldBehavior` registers into it; `TickDurations` is never called; the live tracker is `GameManager._activeSummons`) |
| EncounterService | Services/EncounterService.cs | MonoBehaviour | Defeated-enemy tracking, XP, encounter level. | unused copy (only the static `CalculateEncounterLevel` is used, by `GameManager.TreasureGeneration.cs:48`) |
| SpellApplicationService | Services/SpellApplicationService.cs | MonoBehaviour | Spell effect add/remove/tick helpers. | unused copy (only `Initialize` is called; superseded by `EffectService` and GameManager code) |
| SpellResolutionService | Services/SpellResolutionService.cs | static | Blink / SR pre-cast checks. | unused copy (tests only; Blink logic is inline in the PC and NPC cast paths) |
| SavingThrowResolver | Services/SavingThrowResolver.cs | static | Fort/Ref/Will saves with the Luck-domain reroll. | live (item behaviours, AIService, `GameManager_Spells_P.cs`). Parallel to `Spell/Casting/SpellSaveResolver`, which has its own global `SaveType`/`SaveResult`. |
| ConcentrationService | Services/ConcentrationService.cs | static | Concentration DCs and success chance. | live |
| SpellTargetingService | Services/SpellTargetingService.cs | static | Creature-type, alignment, HD and range predicates for spells. | live (called from GameManager spell partials; many methods have no callers) |
| DiceService | Services/DiceService.cs | static | Dice with an optional context label and logging. | live |
| TeamUtility | Combat/Core/TeamUtility.cs | static | `IsEnemy`/`IsAlly`: Player vs Enemy only; Neutral is neither; no alive check. | live |
| CombatLogHelper | Combat/Logging/CombatLogHelper.cs | static | Builds rich-text combat-log strings (colour constants without `#`). | live |
| CombatCalculationService | Combat/Utilities/CombatCalculationService.cs | static | Small combat formulas (touch attack, damage clamp, crit range; the bare 10 + level + modifier, tests only). | live |
| SpellSaveDCRules | Spell/Casting/SpellSaveDCRules.cs | static | The one spell save DC rule (SPL-001): casting class, class spell level, key ability, Heighten, Spell Focus, gnome illusion; spell-like ability (MM p.315) and magic-item (DMG p.214) variants. | live |
| SpellUtilities | Spell/Casting/SpellUtilities.cs | static | Save DC and casting ability modifier (delegate to SpellSaveDCRules), immunity checks. | live |
| SpellCastingHelper | Spell/Casting/SpellCastingHelper.cs | static | Effective CL, duration, dice-count helpers. | live (SR and damage helpers have no callers) |
| EffectService | Spell/Effects/EffectService.cs | static, stateful | Emanations (static list), daily effects, effect ticking. | live. Expired emanations are removed one by one, but the bulk `ClearAll` is called only from `GameManager.OnCombatEnded`; see [the runtime loop](#what-the-game-is-and-the-runtime-loop). |
| DiceRoller | Utilities/DiceRoller.cs | static | `D4`..`D100`, `Roll(count, sides)`. | live |

Related static registries outside these folders: `MeleeReactionService` (Combat/Reactions), `CurseTracker` (Spell/StatusEffects), `LineOfEffectService` (Spell/AreaEffects). Self-creating MonoBehaviour singletons: `AreaEffectManager`, `WindEffectManager`.

### Dormant and unused infrastructure

These types compile and some are even instantiated, but they do not drive behaviour. Do not route new features through them expecting anything to happen.

| Item | Path | Status |
|---|---|---|
| GameEventSystem | _Core/GameEventSystem.cs | Lazy plain-C# pub/sub singleton with 20 event structs. Only 5 are published on live paths, all from GameManager: `CombatStartedEvent` (:3753), `TurnStartedEvent` (:3803), `NewRoundEvent` (:3830), `CombatEndedEvent` (:3870, rarely reached) and `TurnEndedEvent` (:3984, in `NextInitiativeTurn` after the end-of-turn HP state and before `TurnService.EndTurn`; no subscriber in game code). The only subscriber is `WallOfFireAreaEffect.cs:630` (heat-wave damage on `TurnStartedEvent`), so removing that publish breaks Wall of Fire. `Publish` catches and logs subscriber exceptions. |
| CommandProcessor, IGameCommand, IGameCommandAsync, 7 command classes | _Core/Commands/ | `CommandProcessor` is added in Awake (:575) into a field that is never read. No command is ever constructed, and every `Execute` only logs. |
| CombatStateMachine | Combat/Core/CombatStateMachine.cs | Instantiated as `GameManager.CombatState` (:30). Nothing transitions or reads it, so it stays Idle. Real state is `CurrentPhase`/`CurrentSubPhase` plus `InputService.InputMode`. |
| InitiativeSystem | Combat/Core/InitiativeSystem.cs | Static initiative roller with zero references. `TurnService` does this job. |
| GrappleSystem, OverrunSystem, StandardManeuvers, SupportActions, TurnUndeadSystem components | Combat/Maneuvers/, Combat/Special/ | Each is an empty `class X : BaseCombatManeuver { }`. All are added, `Initialize`d and `Cleanup`ed by GameManager, but nothing calls any member on them. The real code is the `partial class GameManager` in the same file. |
| SummoningService, EncounterService, SpellApplicationService, SpellResolutionService | Services/ | Parallel copies of logic that lives in GameManager or `EffectService` (see the service table). Changing them does not change gameplay. |
| GameManager.SetupCharacters, StartPlayerTurn, OnCharacterCreationComplete (2-PC), OnDualWieldButtonPressed, OnFullAttackDefensivelyButtonPressed | _Core/GameManager.cs | Unreachable or without callers. `OnCharacterCreationComplete` is assigned to the legacy `OnCreationComplete` callback in `Start`, but `NotifyCreationComplete` always prefers `OnCreationComplete4`. |

### Folder map of Assets/Scripts

There are 675 `.cs` files (about 305K lines, recounted 2026-10-07). The layout comes from the Phase 5B reorganisation (commit 8a79a42, 2026-05-27), which was a pure directory move. An old path such as `Core/X.cs` or `Magic/X.cs` in a pre-2026-05-27 doc resolves by globbing `**/X.cs`. Counts below are `.cs` files.

```
Assets/Scripts/
  _Core/ (18)            GameManager.cs + 11 GameManager.*.cs partials (ScenarioHarness is editor-only),
                         SceneBootstrap, GameEventSystem, ScenarioHooks, GameSettings, GameConstants, PlaneType
    Commands/ (3)        dormant command pattern
  AI/ (10)               AISpellcastingStrategist, LastKnownPositionTracker, SpellCategoryClassifier,
                         AIProfile base, NaturalAttackChoice, AIManeuverTurnMemory (AI-060 stopgap),
                         AIConsumableManager, AIBehaviorData,
                         SpellcasterAIBehaviorData, NPCTemplateAIConfigurator (+ README.md)
    Profiles/ (19)       AIProfile subclasses (namespace DND35.AI.Profiles)
    Custom/ (1)          unreferenced example profile
  Character/
    Controller/ (2)      CharacterController (12,261 lines, not partial), CharacterEquipment
    Stats/ (8)           CharacterStats, Alignment, HPState, conditions/tags
    Classes/ (9) + Bard, Druid, NPC, Paladin, Ranger, Shared   class definitions
    Creatures/ (35)      NPCDatabase + NPCDatabase_* partials (letters plus Dragons, Lycanthropes,
                         Skeletons, Zombies) and a few creature-data helpers
    Templates/ (14)      creature templates (skeleton, zombie, lycanthrope, NPC templates)
    CreatureClass/ (5), Familiar/ (1), Feats/ (3), Progression/ (3), Races/ (2),
    Religion/ (4), Skills/ (2), Specialization/ (1)
  Combat/
    Core/ (15)           AttackCalculator, ThreatSystem, RangeCalculator, SizeCategory, TeamUtility, CombatEndRules,
                         DamageModel, BullRushRules, dormant CombatStateMachine/InitiativeSystem
    Conditions/ (11), Behaviors/ (4), Maneuvers/ (6), Special/ (3), Reactions/ (3),
    Mounts/ (4), StatusEffects/ (1), Logging/ (2), Utilities/ (2)
  Spell/ (1: BonusType)
    Data/ (8)            SpellData, SpellID, SpellNames, SpellSlot, SpellSchool, ...
    Database/ (25)       SpellDatabase + 23 SpellDatabase_<letter> partials (A..Z, some letters
                         absent) + SpontaneousCastingType
    Casting/ (8), Components/ (7), Effects/ (1: EffectService)
    Resolution/ (25), Special/ (9), Domain/ (3)   mostly GameManager partials
    AreaEffects/ (32), StatusEffects/ (29)
  Equipment/             Items/ (9), Inventory/ (10), Weapons/ (18), Armor/ (10), Rings/ (7),
                         Rods/ (5), Staves/ (3), Wondrous/ (4), SpecificItems/ (4), Enchantments/ (7),
                         Materials/ (1), SpellStorage/ (3), Effects/ (1), Store/ (2: StoreInventory, StoreUI)
                         (root also holds legacy design .md files)
  UI/                    Common/ (8), Combat/ (12), CharacterCreation/ (6), CharacterSheet/ (5),
                         Inventory/ (3), Spells/ (6), Encounter/ (6), Crafting/ (1), Wish/ (1)
  Services/ (16)         see Service layer
  Encounters/ (14)       DMG dungeon tables, CSV parser, spawner, random encounter system,
                         EncounterSpawnPlacement (spawn squares, ENC-001), GameManager.DungeonEncounters partial
  Crafting/ (10)         item creation feats and workshop logic
  TreasureGenerator/ (7) DMG treasure (5 files in namespace DND35e.Treasure) + global TreasureItemConverter, TreasureUI
  Effects/ (5)           diseases and poisons (not spell effects)
  Grid/ (4)              SquareGrid, SquareCell, SquareGridUtils, PathPreview
  World/ (2)             PlanarTravelSystem, CreatureTrapSystem (mostly inert)
  Utilities/ (12)        DiceRoller, CameraController, DebugCommands, IdentifierExtensions, ...
  Identifiers/ (2)       two editor-only ContextMenu smoke tests (the real ID types live elsewhere)
  Tests/ (123)           static RunAll() suites in 14 domain subfolders, plus Runner/ (StaticSuiteRunner)
                         and Scenarios/ (the editor-only scenario harness: model, runner, trace, checks, steps,
                         expectations, fast mode, session guard, fresh-session batch driver, soak statistics,
                         Catalog/ of smoke, rules and soak definitions); ScenarioHooks (in _Core/) is null in
                         normal play and rules code must never read it
```

The empty legacy folders left by the reorganization (`Core/`, `Magic/`, `CombatSystems/`, `Classes/`, `Store/`, `Inventory/`, `UI/Panels/`) were removed on 2026-10-03. Resolve an old doc path such as `Magic/X.cs` by file name (`**/X.cs`).

### Cross-cutting conventions

**Namespaces.** Almost all code is in the global namespace. The exceptions are:

- `DND35e.Identifiers` (13 files): `SpellNames`, `SpellID`, `ItemIDs`, `ItemID`, `RingNames`, `WondrousItemNames`, `GameConstants`, `IdentifierExtensions` and a few enums. 238 files import it.
- `DND35.AI`, `DND35.AI.Profiles` and `DND35.AI.Custom` (23 files).
- `DND35e.Treasure` (5 files).
- `DND35.Magic` (`CounterspellData`).
- `Tests.*` (98 files).

Name clashes to know about: `CharacterController` shadows `UnityEngine.CharacterController` (it resolves only because global-namespace types win over `using UnityEngine`); the global `SaveType` clashes with `SavingThrowResolver.SaveType`; the global `DamageType` and `DND35e.Identifiers.DamageType` have different ordinals.

**String IDs.** Spells, items and NPCs are keyed by snake_case strings:

- Spells: `SpellNames.FIREBALL == "fireball"`, looked up with `SpellDatabase.GetSpell(id)`.
- Items: `ItemIDs.LONGSWORD`, looked up with `ItemDatabase.CloneItem(id)`. The string overloads are marked `[Obsolete]` (warning only), but `CloneItem(ItemIDs.X)` is still the dominant call style.
- NPCs and encounter presets: plain literals such as `"goblin_warchief"` and `"goblin_raiders"`, looked up with `NPCDatabase.Get(id)`, an exact match.

The enums `SpellID` and `ItemID` exist but are rarely used.

**Static databases with lazy `Init()`.** `ItemDatabase`, `SpellDatabase`, `NPCDatabase`, `RaceDatabase`, `FeatDefinitions`, `RingDatabase`, `WondrousItemDatabase`, `RodDatabase`, `StaffDatabase` and `CraftableItemRegistry` hold static dictionaries guarded by `_initialized`. Getters call `Init()` themselves, so call order rarely matters. `SceneBootstrap.SetupGameManager` warms most of them up. Tests that touch static state must reset it themselves.

**Never mutate templates.** Database entries are shared reference objects. Clone before you modify:

- items: `ItemDatabase.CloneItem`;
- spells: `SpellData.Clone()`, as used by `MetamagicModifier`;
- NPC definitions: `NPCDefinition.Clone()`, as used by templates and `DungeonEncounterSpawner`.

There is one deliberate exception. `SpellCategoryClassifier.ReclassifyAll`, called from `SpellDatabase.cs:75`, rewrites `SpellData.EffectType` globally when the spell database initialises; see [Spell data model](architecture/spells.md#spell-data-model).

**UI built in code.** There are no prefabs. Each UI class builds itself through `BuildUI(canvas)`, `EnsureBuilt()` or `UIFactory` helpers. Phase transitions are callback-driven: `Open(onSelect, onBack, onStart, onClosed, ...)`. How panels are hosted (eager vs lazy) is described in [UI: built entirely in code](architecture/ai-encounters-ui.md#ui-built-entirely-in-code).

**Logging.** Player-visible text uses `CombatUI?.ShowCombatLog(CombatLogHelper.<Semantic>(emoji, text))` (details in [Combat log](architecture/combat-and-grid.md#combat-log)). About 1,300 of the roughly 1,500 `ShowCombatLog` calls follow this pattern. Do not hand-write `<color>` tags. `CombatLogger` is an older formatter with two users. Diagnostics use `Debug.Log` with bracketed tags; grep a tag to trace a flow in the Console:

- `[PlayNow]`
- `[CombatStart]`
- `[CombatPhase]`
- `[Initiative]`
- `[TurnService][Flow]`
- `[VictoryCheck]`
- `[LootFlow]`
- `[CombatReset]`
- `[TestPanel]`
- `[AI][Spell]`

**Dice.** Three random sources coexist, all backed by `UnityEngine.Random`, with no seeding hook in game code (some tests call `Random.InitState`). Every `DiceRoller` die and every `DiceService` die (min 1) is drawn first and then passed through the inert test filter `ScenarioHooks.FilterRoll`, which returns it unchanged in normal play; raw `Random.Range` sites bypass it (TST-033):

- `DiceRoller` (Utilities): about 155 referencing lines, none in tests.
- `DiceService` (Services): about 130 referencing lines (about 120 outside tests), adds an optional context label.
- Raw `Random.Range`: about 60 non-test lines outside the two dice helpers.

The two `Roll` methods mean different things. `DiceRoller.Roll(2, 6)` is 2d6, while `DiceService.Roll(2, 6)` is one roll in the range 2..6; use `DiceService.RollMultiple(count, sides)` for NdS.

**Accessor wrapper families.** Services reach private GameManager state through public wrappers instead of direct field access:

- `Combat_*` (59 members in `_Core/GameManager.CombatFlowAccessors.cs`), used by `CombatFlowService`, `ActionButtonPanel`, `CharmedBehaviorController` and `FireShieldReactionEffect`.
- 40 public `*ForAI` methods, used mainly by `AIService`, also by the `AIProfile` subclasses and the Confused/Charmed/Fascinated/Frightened behavior controllers. 34 are in `GameManager.cs` (33 at :10879-11026, plus `GetSummonCasterForAI` at :10314); the others are in CombatActions (3), NPCTurns, FlamingSphere and MirrorImage (1 each).
- One-line delegates kept for backward compatibility, e.g. `GameManager.DispelCounterspell.cs` and `IsEnemyTeam`/`IsAllyTeam` (`GameManager.cs:677-682`).

A new AI need normally means adding a new `*ForAI` wrapper.

**Identity.** `IsPC(c)` means `c.IsControllable` (`GameManager.cs:670`), not team membership. Use `TeamUtility.IsEnemy`/`IsAlly` for sides.

**How to find things.**

- GameManager members: `grep -rn "void MethodName" Assets/Scripts`. Never assume a method is missing because `GameManager.cs` lacks it; 51 other files declare the class.
- Button handlers: `grep -rn "On.*ButtonPressed" Assets/Scripts`. Most are bound in `SceneBootstrap.WireButtons`; a few are called from `ActionButtonPanel` and `CombatUI`.
- Before renaming a private GameManager method, grep `Assets/Scripts/Tests`. Tests reach private members by name through reflection: 19 test files make about 60 GetMethod/GetField/GetProperty calls (recount before relying on it: `grep -rE "\.(GetMethod|GetField|GetProperty)\(" Assets/Scripts/Tests`). A rename breaks them with no compile error.
- Tests are static classes with `RunAll()`, except three MonoBehaviour suites (`FlankingReachRulesTests`, `RangeCalculatorTests`, `ReachWeaponRulesTests`) that run from `Start` but are attached to no scene. There is no NUnit, no Unity Test Framework package and no asmdef, so they compile into the game assembly. No game code calls `RunAll()`; the editor-only `Tests.Runner.StaticSuiteRunner` (Tools > DND Tests, or `RunFromCommand` through the Unity MCP) runs them against `tools/tests/static-suites.json`. See [TESTING.md](TESTING.md).
