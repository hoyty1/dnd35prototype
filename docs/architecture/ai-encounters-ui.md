> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-02.

# AI, encounters & UI

This part covers how non-player combatants choose actions, how an encounter's enemies are picked and placed, and how the code-built UI is organised. Grid geometry and the combat rules that the AI calls into are described in [Combat & grid](combat-and-grid.md#combat--grid). Behaviour here was checked by reading code; none of it was re-checked in Play mode.

## NPC AI: two-axis configuration

Each NPC's AI is set by two independent fields on `NPCDefinition` (Assets/Scripts/Character/Creatures/NPCDatabase.cs:626-627):

| Field | Enum | Selects | Where it is consumed |
|---|---|---|---|
| `AIBehavior` (default `AggressiveMelee`) | `NPCAIBehavior`: AggressiveMelee, RangedKiter, DefensiveMelee, Ranged (NPCDatabase.cs:808) | The tactical routine in AIService | `GameManager.SetupEnemyEncounter` appends it to the parallel list `GameManager._npcAIBehaviors` (GameManager.NPCSetup.cs:179). `GameManager.GetNPCBehaviorForAI` reads it back by `NPCs.IndexOf(npc)` |
| `AIProfileArchetype` (default `None`) | `NPCAIProfileArchetype`, 23 values (NPCDatabase.cs:777) | The `AIProfile` subclass that scores targets, maneuvers, charges and spells | `GameManager.BuildRuntimeAIProfile` (GameManager.NPCSetup.cs:758), called from `InitializeNPCFromDefinition`, sets `CharacterController.aiProfile` |

In short, the profile decides whom to attack and with what, and the behavior decides whether the NPC closes to melee, kites or holds back. Some profiles bypass the behavior entirely (see the routing table below).

Check these before changing either axis:

- Profiles are never loaded from assets. `BuildRuntimeAIProfile` calls `ScriptableObject.CreateInstance<T>()` for every NPC at every spawn, and each subclass writes its tuning in `OnEnable`. The `[CreateAssetMenu]` attributes and inspector values have no effect, so tune profiles in code. Instances are never destroyed.
- A new profile class needs a new `NPCAIProfileArchetype` value, a `case` in `BuildRuntimeAIProfile`, and the archetype set on the NPC definitions. Assigning `aiProfile` by hand is overwritten at spawn. `AI/Custom/CustomAIExample.cs` is not wired to any archetype.
- `None` becomes `Animal` only when `CreatureType` is "Animal". `Brute` (29 definitions) and `Caster` (2) have no `case`, so those NPCs get a null profile and run the profile-less path.
- `NPCAIBehavior.Ranged` is handled nowhere. It runs AggressiveMelee unless the profile's `CombatStyle` is `Ranged`. It is used at NPCDatabase_B.cs:844, NPCDatabase_G.cs:1130, NPCDatabase_R.cs:235 and SkeletonTemplate.cs:738.
- Other code also writes these fields: `DungeonEncounterSpawner.UpdateAIForClass` (DMG spawns with class levels), and the Skeleton, Zombie and Lycanthrope templates. The armor-targeting and shield-bash test encounters bypass both fields and replace `npc.aiProfile` directly after spawn (GameManager.NPCSetup.cs:184-196); Summon Swarm also assigns `SwarmAI`/`IndiscriminateSwarmAI` directly (Spell/Resolution/GameManager.SpellCasting.cs:641, 778). `NPCTemplateAIConfigurator.ConfigureDefinition` is reached only through `QuickSpawnSystem`, and only tests call that.
- `_npcAIBehaviors` must stay index-aligned with `GameManager.NPCs`. `SetupEnemyEncounter` skips the `Add` when an ID is unknown, and `LionsShieldBehavior.cs:286` appends to `NPCs` without adding a behavior. Summons keep the lists aligned: they add to both (GameManager.SpellCasting.cs:335-336) and remove from both (367-369). A missing index falls back to AggressiveMelee.

## NPC AI: the turn, end to end

```
TurnService.StartTurnAtCurrentIndex -> OnTurnStarted
  GameManager.OnTurnStarted (_Core/GameManager.cs:3764), non-PC branch
    StartCoroutine(SingleNPCTurnFromInitiative)            _Core/GameManager.NPCTurns.cs:35
      ShouldSkipTurnDueToHPState -> NextInitiativeTurn, stop
      behavior = GetNPCBehaviorForAI(npc)                  _Core/GameManager.cs:10922
      yield AIService.ExecuteNPCTurn(npc, behavior)        Services/AIService.cs:48
        BeginNPCTurnForAI: ConditionService.OnTurnStart, Melf's Acid Arrow damage, regeneration,
                           StartNewTurn, ProcessRoundStartPerception (Listen checks)
        HP <= 0 -> stop
        Confused / Charmed / Fascinated / Frightened controllers (may end the turn)
        Animate Rope escape attempt
        SelectBestTarget; none -> ExecuteSearchTurnWhenNoTargets (300), reselect, else stop
        Turned undead -> ExecuteTurnedUndeadTurn (419, flee)
        grappling -> GameManager.ExecuteGrappleRestrictedTurnForAI -> GameManager.AI_GrappleRestrictedTurn
                     (a GameManager partial inside Combat/Maneuvers/GrappleSystem.cs)
        aura (free action), free-action ranged special such as Spittle
        inside a Resilient Sphere -> stop
        routing (table below)
      AreAllPCsDead -> TurnPhase.CombatOver, else NextInitiativeTurn (GameManager.cs:3948)
```

Routing, checked in this order (AIService.cs:182-297):

| Condition | Routine |
|---|---|
| Profile is `SwarmAI` or `IndiscriminateSwarmAI` | `ExecuteSwarmTurn` (1233) |
| `GameManager.IsSummonedCreature(npc)` | `ExecuteSummonedCreatureTurnForAI` -> `GameManager.AI_SummonedCreature` (GameManager.NPCTurns.cs:79): follows the `SummonCommand` (AttackNearest/ProtectCaster), retreats at 30% HP, trips or smites, then attacks via `NPCPerformAttack`. It never casts; the profile is not used for routing or targeting (only the hooks inside `NPCPerformAttack` see it) |
| Profile is `HealerAIProfile` | Healer branch (197-257). `DetermineActionPriority` returns CriticalHealing, Healing, Buffing, OffensiveSpell or PhysicalAttack. PhysicalAttack runs RangedKiter if `DetermineCombatMode` is Ranged, else DefensiveMelee or AggressiveMelee per behavior. Healing casts `TryExecuteSpellcastAction` on `GetPriorityHealTarget`; Buffing casts with no fallback target. OffensiveSpell, and a heal or buff that fails, fall through to RangedKiter |
| Profile is `DragonAIProfile` | `ExecuteDragonTurn` (631): casts if it is a spellcaster and not swarmed, positions for breath, otherwise charges and melees |
| Any other profile, behavior DefensiveMelee | `ExecuteDefensiveMeleeTurn` (1140): targets `SelectLowestHPEnemy` and withdraws below 30% HP |
| Any other profile, behavior RangedKiter or `profile.CombatStyle == Ranged` | `ExecuteRangedKiterTurn` (835) |
| Any other profile | `ExecuteAggressiveMeleeTurn` (477) |
| No profile | `switch (behavior)`; `Ranged` and the default case go to AggressiveMelee |

`CombatStyle.Mixed` (Abjurer, Dragon) counts as melee for routing.

Conventions for this code:

- **The `*ForAI` wrappers.** AIService reaches GameManager only through public members: mainly the 40 `*ForAI` wrappers (locations and the add-a-wrapper rule under [Cross-cutting conventions](../ARCHITECTURE.md#cross-cutting-conventions)), plus `CombatUI` and `StartCoroutine`. GameManager performs the actions: `NPCPerformAttack` (GameManager.NPCTurns.cs:1065), `TryNPCPerformSpellCast` (GameManager.NPCTurns.cs:758), `NPCExecuteBreathWeaponForAI` (GameManager.NPCTurns.cs:1430), and `NPCExecuteCharge` (Combat/Maneuvers/SupportActions.cs:1801).
- **Coroutines.** AIService is a MonoBehaviour on the GameManager object (GameManager.cs:523-524), but it starts nested coroutines with `_gameManager.StartCoroutine`. Pacing uses hard-coded `WaitForSeconds` values. After every movement yield the routines re-check `npc.Stats.CurrentHP <= 0`, because area damage during movement can kill; new routines must do the same.
- **Target selection.** `AIService.SelectBestTarget` (1471) works in this order. (1) A Mirror Image priority target wins outright, before any exclusion. (2) Candidates are living enemies per `TeamUtility.IsEnemy`, which pairs only Player with Enemy, so a `CharacterTeam.Neutral` character is never a candidate. The charm source, the fear source, Sanctuary-protected targets (Will save) and Hide from Undead targets are removed. (3) Visible targets come first, then Listen-pinpointed concealed targets, then tracked concealed ones. (4) Within a group, `SelectBestTargetFromCandidates` (1558) tries `PriorityTargetName` (from `NPCDefinition.AITargetPriority`), then `profile.ScoreTarget`, then the "Uses Armor-Based Targeting" tag, then a default heuristic. The Sanctuary and Hide from Undead saves are re-rolled on every call, and the function runs several times per turn.

## NPC AI: profiles

The base class is `AIProfile` (AI/AIProfile.cs, namespace `DND35.AI`). There are 19 concrete subclasses in AI/Profiles (namespace `DND35.AI.Profiles`). AIService, the strategist and the tracker are in the global namespace.

| Archetype | Class | CombatStyle | Notes |
|---|---|---|---|
| Animal (or None with CreatureType "Animal") | AnimalAIProfile | Melee | Picks a specialty (Grappler/Tripper/PackHunter) from the creature's abilities. The GameManager grapple code in Combat/Maneuvers/GrappleSystem.cs also checks for this profile |
| Humanoid | HumanoidAIProfile | Melee | Disciplined melee |
| Berserk | BerserkAIProfile | Melee | Prefers wounded targets, low caution |
| Grappler | GrapplerAIProfile | Melee | Trip and disarm also enabled; the base maneuver order is Trip > Disarm > Sunder > BullRush > Overrun > Grapple |
| Ranged | RangedAIProfile | Ranged | Keeps distance, prefers lightly armored targets |
| Healer | HealerAIProfile : SpellcasterAIProfile | Ranged | Has its own routing branch |
| Spellcaster | SpellcasterAIProfile | Melee (inherited default) | Casts only if the behavior is RangedKiter |
| Evoker, Necromancer | EvokerAIProfile, NecromancerAIProfile : SpellcasterAIProfile | Ranged | School priority 10 for its own school |
| Abjurer | AbjurerAIProfile : SpellcasterAIProfile | Mixed | Routed as melee |
| UndeadMindless | UndeadMindlessAIProfile | Melee | Attacks the nearest enemy, no maneuvers |
| Undead, UndeadTactical | UndeadTacticalAIProfile | Melee | Spreads paralysis, coup de grace |
| UndeadBrute | UndeadBruteAIProfile | Melee | Ignores AoOs |
| UndeadIncorporeal | UndeadIncorporealAIProfile | Melee | Prefers targets with low touch AC |
| Vampire | VampireAIProfile : SpellcasterAIProfile | Melee | Its docstring promises spells first; with the default behavior it never casts |
| Lich | LichAIProfile : SpellcasterAIProfile | Ranged | Casts through RangedKiter |
| Swarm, IndiscriminateSwarm | SwarmAI, IndiscriminateSwarmAI | Melee | Own routine; the indiscriminate variant ignores teams |
| Dragon | DragonAIProfile | Mixed | Own routine. Per-turn state lives on the instance, so never share one instance between NPCs |
| Brute, Caster | none | n/a | `BuildRuntimeAIProfile` returns null |

The live virtual hooks on `AIProfile` are `ScoreTarget`, `ShouldPreferCharge`, `ShouldIgnoreUnconsciousTargets`, `ShouldSwitchTargetsMidFullAttack`, `ShouldTakeFiveFootStepToContinueFullAttack`, `ShouldUseCoupDeGrace`, `ShouldIgnoreAoO`, `TryEnsureWeaponFallback`, `ShouldInitiateGrapple`, `GetPreferredManeuver` and `GetRangedAoORiskToleranceMultiplier`, plus the virtual properties `PrioritizeVisibleTargets` and `ConcealmentPenaltyMultiplier` (read by AIService target scoring). `ShouldEscapeGrapple` is never called. Several fields are assigned but never read: `Aggression`, `SwitchTargetsOften`, `Movement.MaintainDistance`, `Movement.UseCover`, `Maneuvers.UsePowerAttack/UseAidAnother/UseCombatExpertise`, `SpellcasterAIProfile.FleeHealthThreshold` and `HealerAIProfile.StayNearWoundedAllies`. No profile can make an NPC flee. The only low-HP retreats are the DefensiveMelee withdraw and the summon retreat, both hard-coded at 30% HP in AIService and GameManager. The AI never toggles Power Attack or Combat Expertise.

## NPC AI: spellcasting

```
AIService.TryExecuteSpellcastAction (2340)       requires a standard action and Stats.IsSpellcaster
  SelectSpell (2492)                             castable prepared spells only
    "AI:MagicMissileOnly" tag -> Magic Missile or nothing
    skip Buff/Illusion spells already active with > 1 round left (or indefinite)
    score = SpellcasterAIProfile.ScoreSpell      school priority x10 + EvaluateAOECast + strategist
         or AISpellcastingStrategist.ScoreSpellComprehensive (no caster profile)
  AISpellcastingStrategist.EvaluateDefensiveCasting  0 = try SelectLowerLevelAlternative else abort,
                                                     1 = "cast defensively" (only logged)
  AISpellcastingStrategist.SelectBestSpellTarget     self / ally (lowest HP, frontliner) / enemy
  GameManager.TryNPCPerformSpellCastForAI -> TryNPCPerformSpellCast (_Core/GameManager.NPCTurns.cs:758)
```

`TryExecuteSpellcastAction` has callers only in the Healer branch (AIService.cs:231, 245), `ExecuteDragonTurn` (651) and `ExecuteRangedKiterTurn` (896, 970). The only other NPC cast path is a charmed NPC healing its charmer (Combat/Behaviors/CharmedBehaviorController.cs:91).

- **`AISpellcastingStrategist`** (AI/AISpellcastingStrategist.cs) is a static class of about 1.5K lines. Its header lists tiers T1-T4. `ScoreSpellComprehensive` (1115) adds up roughly 20 modifiers: pre-buff, threatened penalty, save weakness, resistance and SR, slot conservation, dispel value, summons, combos, area denial, domain and class tweaks. Its static per-combat dictionaries are cleared by `ResetCombatState`, which runs at combat start (GameManager.cs:3732). `RegisterPlan`, `RecordIneffectiveDamage` and `RecordSpellSaveSuccess` have no callers, so the multi-round planning and "pattern learning" features do nothing. The school-priority term (0-100) in `SpellcasterAIProfile.ScoreSpell` usually outweighs the strategist's terms.
- **`SpellCategoryClassifier.ReclassifyAll`** (AI/SpellCategoryClassifier.cs) lives with the AI but runs once from `SpellDatabase.Init` (Spell/Database/SpellDatabase.cs:75) and permanently rewrites `SpellData.EffectType` for all gameplay code; see [Spell data model](spells.md#spell-data-model).

## NPC AI: perception

- `LastKnownPositionTracker` (AI/LastKnownPositionTracker.cs) is a MonoBehaviour that AIService adds to an NPC on first use (AIService.cs:855, 1487, 2941). It stores last-known enemy squares, runs Listen DC 20 checks (`AttemptListenChecks`, 132) to pinpoint concealed targets, and runs a Spot-vs-Hide check for See Invisibility.
- "Visible" means `CharacterController.CanSee` (total concealment only); there is no geometric line of sight (see [Grid](combat-and-grid.md#grid)).
- `AttemptListenChecks` is called from `ProcessRoundStartPerception` (2968), `SelectBestTarget` (1536) and `ExecuteRangedKiterTurn` (865). Each call re-rolls, so an NPC can get several Listen checks per turn even though the comments say one per round.
- A second store, `CharacterController._lastKnownTargetPositions`, feeds `AIService.GetPerceptionTargetingAdjustment`. Nothing calls `LastKnownPositionTracker.ClearAllPositions`, and NPC GameObjects are reused across encounters, so old positions probably carry over into the next fight (not verified in Play mode).

## NPC AI: what it cannot do

- **Cast area spells.** `TryNPCPerformSpellCast` returns false for any `SpellTargetType.Area` spell (GameManager.NPCTurns.cs:787-788). The profile and the strategist still score AoE spells, so a caster can pick Fireball, fail to cast it, and fall back to another action in silence.
- **Cast from the melee routines.** `ExecuteAggressiveMeleeTurn` and `ExecuteDefensiveMeleeTurn` never call `TryExecuteSpellcastAction`. A Spellcaster-archetype monster that keeps the default AggressiveMelee behavior never casts. Examples are mind_flayer (NPCDatabase_M.cs:600) and vampire (NPCDatabase_V.cs:242, Vampire profile with CombatStyle Melee). The Lich casts only because its profile sets `CombatStyle.Ranged`.
- **Counterspell.** `AIService.TryAIReadyCounterspell` (2660) has no callers, and no player UI calls `CharacterController.ReadyCounterspell` (CharacterController.cs:12137); only tests do. `DispelMagicService.TryResolveCounterspell` therefore never finds a readied caster in normal play.
- **Rules deviations.** NPC casting never provokes an AoO and never rolls a defensive Concentration check (see the `TryNPCPerformSpellCast` row in [Cast pipelines](spells.md#cast-pipelines)). Monster special attacks (Spittle, swarm damage) bypass the normal attack and damage math. These and the other AI rules gaps are tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md).
- **Consumables.** `AIConsumableManager` is used only in Tests/Classes/NPCTemplateSystemTests.cs, so NPCs never use potions, scrolls or wands.

## Encounters: the four sources

All four sources end in a `List<string>` of NPC IDs. `GameManager.PromptEncounterSelection` (GameManager.cs:785) passes three callbacks to `EncounterSelectionUI.Open`: `onSelect` receives a preset ID (an empty ID becomes `goblin_raiders`) that `ApplyEncounterPreset` resolves to its ID list, `onStartRandomEncounter` receives the ID list directly, and `onCancel` applies `goblin_raiders`.

| Source | Code | Path into GameManager |
|---|---|---|
| Preset cards | `NPCDatabase.ListEncounterPresets` (NPCDatabase.cs:86) has 55 presets, extended by `GetDragonEncounterPresets`, `GetSkeletonEncounterPresets`, `GetZombieEncounterPresets` and `GetLycanthropeEncounterPresets` (about 87 in total; breakdown under [Creatures](characters-and-creatures.md#creatures)) | `onSelect` -> `GameManager.ApplyEncounterPreset` (GameManager.cs:1769) |
| Random CR/EL generator | Encounters/RandomEncounterSystem.cs, UI in RandomEncounterGeneratorUI | `onStartRandomEncounter(ids, generated)` -> `ApplyRandomEncounter` (1690) |
| DMG dungeon d% tables | Encounters/DungeonEncounterTableManager.cs + DungeonEncounterSpawner.cs, UI in DungeonEncounterGeneratorUI | `EncounterSelectionUI.OnDMGTablesPressed` opens the generator; its `onStartCombat` callback calls `DungeonEncounterSpawner.PrepareEncounter`, then `onStartRandomEncounter(result.EnemyIds, null)` |
| Custom Encounter Builder | UI/Encounter/CustomEncounterBuilderUI.cs | `onStartRandomEncounter(ids, new GeneratedRandomEncounter { IsCustomEncounter = true })` |

- **Test presets.** 22 preset IDs (ids and flags declared at `_Core/GameManager.cs:100-144`, presets in `NPCDatabase`) set a test-scenario flag in `ApplyEncounterPreset` (`_isGrappleTestEncounter` through `_isMirrorImageTestEncounter`). Each flag selects a `Configure*TestParty` party layout; 17 of them also select a fixed spawn rule or array in `SetupEnemyEncounter`, and some add AI overrides. `test_2_goblins` and `xp_levelup_test` are also test presets but set no flag. An unknown preset ID resolves to the first preset (`NPCDatabase.GetEncounterPreset`). An empty ID list (preset or random) falls back to goblin_warchief, hobgoblin_sergeant and skeleton_archer. `ApplyRandomEncounter` clears all 22 flags.
- **RandomEncounterSystem.Generate.** It computes APL with `ChallengeRatingUtils.CalculateAPL` and the target EL with `GetTargetELForDifficulty`. Candidates are every NPC with a parseable CR, minus summons, after the type and environment filters. It shuffles the four strategies (SingleBoss, EliteSquad, Swarm with 5-8 creatures by default and at most 15, MixedGroup with 3-7 by default and at most 12) and runs up to 220 attempts per strategy (`MaxGenerationAttempts`). It returns the first result within 0.75 EL of the target, otherwise the closest one.
- **DMG tables.** `DungeonEncounterTableManager.LoadTables` reads Assets/StreamingAssets/dungeon_encounters.csv through `System.IO`. The file has 253 rows for dungeon levels 1-8 (`MaxLevel = 8`) and 15 "Roll on Nth-level table" cascade rows. If the file is missing, throws, or yields no tables, the manager uses the hard-coded `DungeonEncounterTableData.BuildAllTables`, whose contents differ from the CSV. While the CSV exists, edits to the hard-coded tables have no effect. `EncounterDescriptionParser` turns each description into dice counts, class levels and templates. `GenerateRandomEncounter` rolls d%, and a cascade re-rolls one level up or down, at most 3 deep. Plural CSV names that are missing from the name map (for example "1d3 stirges") resolve to NPC IDs that do not exist and are skipped by `DungeonEncounterSpawner.PrepareEncounter` with only a warning (DungeonEncounterSpawner.cs:100-106); this is tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md). `GameManager.StartDungeonEncounter`, `OpenDungeonEncounterGenerator` and the other entry points in Encounters/GameManager.DungeonEncounters.cs have no external callers.
- **Custom builder.** It lists `NPCDatabase.AllNPCs` minus IDs that look like test creatures (`IsTestCreature`: "_test", "test_", "_drill", "pinata", "target_dummy"), caps the total at `min(8, NPC slots)` (`MaxTotalCreatures`), and shows a live EL.

## Encounters: spawning onto the grid

```
ApplyEncounterPreset / ApplyRandomEncounter           _Core/GameManager.cs:1769 / 1690
  (custom only) CustomEncounterBuilderUI.CalculateSpawnPositions  x 11-18, y 2-18, >= 2 squares apart
  RestoreStandardPartyLayout or Configure*TestParty
  SetupEnemyEncounter(ids)                             _Core/GameManager.NPCSetup.cs:29
    spawnCount = min(ids.Count, NPCs.Count); unused slots SetActive(false)
    def = BuildEncounterDefinitionForSpawn(id, NPCDatabase.Get(id), i)
    position: test-scenario rule/array -> custom positions -> EncounterSpawnPositions[i] -> (15+i, 10)
    token: IconLoader.DetermineMonsterType + GetToken, else tinted Sprites/npc_enemy_alive
    InitializeNPCFromDefinition (stats, aiProfile), _npcAIBehaviors.Add
  SetupNPCIcons, UpdateAllStatsUI
```

- **15 NPC slots.** `SceneBootstrap.CreateCharacters` creates exactly 15 enemy GameObjects (`totalEnemySlots`, _Core/SceneBootstrap.cs:119), and `SetupEnemyEncounter` silently drops any extra IDs. Only 3 NPC stat panels exist on the HUD (`CreateNPCPanelsRight(..., 3)`, SceneBootstrap.cs:181).
- **5 spawn positions.** `GameManager.EncounterSpawnPositions` (GameManager.cs:2862) has 5 entries. Preset, random and DMG encounters with more than 5 enemies place the 6th enemy at (20, 10) and later ones further right, outside the default 20x20 grid. Nothing validates bounds or size overlap. Only custom encounters get computed positions. This is tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md).
- **`spawn_*` temporary IDs.** `DungeonEncounterSpawner.PrepareEncounter` clones the base definition, applies class levels (`CreatureClassEngine.ApplyClassToDefinition`), `UpdateAIForClass` and templates, then registers the result with `NPCDatabase.RegisterExternal` as `spawn_{baseId}_{counter}` or `spawn_{baseId}_{class}_{level}_{counter}` (DungeonEncounterSpawner.cs:294-307). The live DMG path never calls `CleanupSpawnEntries`. These entries stay in `NPCDatabase.AllNPCs` for the whole session, appear in the Custom Encounter Builder list, and can become RandomEncounterSystem candidates.

## UI: built entirely in code

There are no `.prefab` files under Assets, no serialized scene UI and no UI Toolkit. The scene contents and the `SceneBootstrap.Awake` steps are described under [Composition](../ARCHITECTURE.md#composition); the fresh-clone missing-script problem under [MainScene](../ARCHITECTURE.md#mainscene). UI-specific points: the single ScreenSpaceOverlay Canvas has sortingOrder 100 and a CanvasScaler at 1920x1080 (`CreateUI`, _Core/SceneBootstrap.cs:142). `SetupGameManager` (:997) attaches most panels to the canvas and calls database `Init` methods in between; some panels' `BuildUI` reads those databases, so keep the order. The `WireButtons` coroutine (:1101) binds every action button to `GameManager.OnXxxButtonPressed`.

Two hosting styles are in use:

- **Eager.** `canvas.gameObject.AddComponent<X>()` in `SceneBootstrap.SetupGameManager`, then a stored reference on GameManager.
- **Lazy.** Two `if (x == null)` steps, `FindObjectOfType<X>()` and then `gameObject.AddComponent<X>()`, on the GameManager object (or on EncounterSelectionUI's object, which is the same GameObject); for example `PromptEncounterSelection` and `OpenPreCombatHubPhase`. `FindObjectOfType` is deprecated in Unity 6. Such a panel has no parent canvas, so its `EnsureBuilt` ends up calling `FindObjectOfType<Canvas>()`. A variant (Domain, Wizard school and Familiar selection, `CombatEndXPUI`, `LevelUpUI`) creates a new child GameObject under a canvas found the same way. Unity does not define which canvas that returns once a nested canvas exists, and only the Random and DMG generator screens add one (`overrideSorting`).

Panels expose `Open(..., callbacks)`, `Close()` and `IsOpen`. They never advance game state themselves; GameManager passes lambdas. Modals call `transform.SetAsLastSibling()` to draw on top.

Shared helpers in Assets/Scripts/UI/Common are `UIFactory` (buttons, labels, panels, scroll panels; used by only 7 files), `UITheme` (constants used only by UIFactory), `ScrollbarHelper.CreateVerticalScrollbar` (10 files), `DraggableWindow` and `ResizableWindow` (PlayerPrefs keys `ui_window_combat_log`, `ui_window_action_panel` and `ui_window_precombat_inventory`), `IconLoader` (real token and portrait loader), `IconManager` (overlapping icon cache) and `HoverMarker`. Most panels still carry private `CreateText`/`CreatePanel` copies and their own colour constants.

To add an action button you need: a public `Button` field on `CombatUI`, a `CreateGridButton` call in `SceneBootstrap.CreateActionButtonsSection` (661), a listener in `WireButtons`, a proxy property and state rule in `ActionButtonPanel`, and a `GameManager.OnXxxButtonPressed` handler. A `CombatUI` field that SceneBootstrap never assigns stays null and does nothing. `StunningFistPanel` and `ManyshotPanel` are examples. Recipes: "Add a combat action button" and "Add a UI panel" in [DEVELOPMENT_RECIPES.md](../DEVELOPMENT_RECIPES.md).

## UI: screens and panels

| Screen | Owning class (Assets/Scripts/...) | Hosting | Opened by |
|---|---|---|---|
| Character creation wizard (4 PCs, Quick Start, premades) | UI/CharacterCreation/CharacterCreationUI.cs | Eager | At boot; `GameManager.Start` assigns `OnCreationComplete4 = OnCharacterCreationComplete4` (GameManager.cs:657) |
| Feats, skills, spells, preparation | UI/Spells/FeatSelectionUI.cs, UI/CharacterSheet/SkillsUIPanel.cs, UI/Spells/SpellSelectionUI.cs, UI/Spells/SpellPreparationUI.cs | Eager | Creation steps; level-up through `CharacterCreationManager.StartLevelUpFlow` |
| Domains, wizard school, familiar | UI/CharacterCreation/DomainSelectionUI.cs, WizardSpecializationUI.cs, FamiliarSelectionUI.cs | Lazy | CharacterCreationUI and CharacterCreationManager |
| Encounter selection | UI/Encounter/EncounterSelectionUI.cs | Lazy (GameManager object) | `GameManager.PromptEncounterSelection` |
| Random / DMG / custom generators | RandomEncounterGeneratorUI, DungeonEncounterGeneratorUI, CustomEncounterBuilderUI | Lazy (EncounterSelectionUI object) | Footer buttons "Random Encounter", "DMG Tables", "Custom Encounter" |
| Pre-combat hub | UI/Encounter/PreCombatHubUI.cs | Lazy | `GameManager.OpenPreCombatHubPhase` (GameManager.cs:1403) |
| Store | Equipment/Store/StoreUI.cs | Lazy | `OpenStoreFromPreCombat` (1499) |
| Party stash inventory | UI/Inventory/PreCombatInventoryUI.cs | Lazy | `OpenInventoryFromPreCombat` (1480) |
| Crafting workshop | UI/Crafting/CraftingWorkshopUI.cs | Lazy | `OpenCraftingWorkshopFromPreCombat` (1439); does nothing unless a party member has an item creation feat |
| Combat HUD (turn, initiative, party, 3 NPC panels, Combat Log and Action windows) | UI/Combat/CombatUI.cs with presenters CombatLogPanel, ActionButtonPanel, InitiativePanel, TargetSelectionPanel (UI/Combat) and CharacterInfoPanel (UI/CharacterSheet) | Eager | Always present |
| In-combat modals (spell + metamagic, AoO confirm, cast defensively, touch spell, summon choice and menu, confirmation, pick-up/drop/disarm/sunder, special attack, bull rush, character choice, Disguise Self race) | `CombatUI.Show*` methods | Rebuilt per call under the canvas | GameManager and Combat/Maneuvers code |
| Use Item | UI/Combat/QuickItemUsePanel.cs | Eager | `GameManager.OnUseItemButtonPressed` (4610) |
| Staff spell choice | UI/Combat/StaffSpellSelectionPanel.cs | Eager | GameManager.cs:6378 |
| Wish | UI/Wish/WishUI.cs | Eager | Spell/Resolution/GameManager_Spells_W.cs:1085, 1144 |
| Turn Undead targets | UI/Combat/TurnUndeadTargetSelectionPanel.cs | Static `Create(canvas)` | Combat/Special/TurnUndeadSystem.cs:629 |
| Character sheet with embedded inventory | UI/CharacterSheet/CharacterSheetUI.cs + UI/Inventory/InventoryUI.cs | Eager | C key |
| Status badges and tooltips | UI/Combat/StatusEffectIndicator.cs (world-space sprites), StatusEffectTooltipUI.cs, UI/CharacterSheet/CharacterHoverTooltipUI.cs | Per character / singleton | CharacterController.cs:1372; GameManager.cs:632 |
| Loot, XP, level-up | UI/Inventory/LootCollectionUI.cs, UI/Combat/CombatEndXPUI.cs, UI/CharacterSheet/LevelUpUI.cs | Eager / lazy / lazy | _Core/GameManager.LootCollection.cs: `BeginPostCombatLootCollection` (25), `ShowPostCombatXPFlow` (175), `ShowLevelUpUISequence` (344) |

Dead UI: `EncounterPreviewPanel` and `SpellStorageUI` are never opened. `TreasureGenerator/TreasureUI.cs` is dead (see [Treasure generation and post-combat loot](items-and-economy.md#treasure-generation-and-post-combat-loot)), and `UI/Panels` is an empty leftover folder (see [Folder map](../ARCHITECTURE.md#folder-map-of-assetsscripts)).

## UI: debug and test tools

- **F12 Spell Testing panel** (UI/Spells/SpellTestingPanel.cs, attached unconditionally at SceneBootstrap.cs:193 with no editor or development-build guard). It offers spell search, a level filter, a Cast button per spell and a metamagic toggle grid (Empower, Maximize, Extend, Enlarge, Quicken, Silent, Still, Widen, Heighten with a level stepper; `BuildMetamagicSection` 726). The grid is the "metamagic debug panel" from commit 8758492. Casting uses `GameManager.ActivePC`, so it only works on a PC's turn. Non-casters temporarily become a Wizard 10, and the caster gets +4 to their casting ability. Update polls `GameManager.GetTestPanelCaster()` and restores both once it returns null, that is once `_testPanelCastActive` is cleared. Bypasses: while that flag is set, `GameManager.TryRollArcaneSpellFailure` skips arcane spell failure, `PerformSpellCast` skips the spell component pouch check (Spell/Resolution/GameManager.SpellCasting.cs:1689) and `ApplySpellBuff` skips Stoneskin's diamond dust (:7151). Which casts actually get them depends on the pipeline. `PerformSpellCast`, which resolves single-target, touch and self casts, calls `CleanupTestPanelCast` first (:1634), so those test casts get none of the bypasses: they roll ASF, need a pouch, and Stoneskin needs diamond dust. `PerformAoESpellCast`, `TryConsumePendingSpellCast` (summons, Grease) and `HoldPendingMeleeTouchCharge` never clear the flag, so those test casts skip ASF (the AoE path has no pouch check for any caster), and the flag stays set after the cast. It is cleared only by the next `PerformSpellCast` or a targeting cancel (_Core/GameManager.CombatActions.cs:1099). Until then the next AoE, summon, Grease or held-touch cast by anyone, including NPC casts (`GameManager.NPCTurns.cs:797`), also skips ASF, `OnCellClicked` falls back to the stale test caster, and the +4 and temporary Wizard 10 stay applied. Tracked as [SPL-036](../issues/SPL.md). The backend `GameManager.TestCastSpellFromPanel` (_Core/GameManager.TestPanel.cs:38) forces `CurrentPhase = PCTurn` and never restores the previous phase. The damage/save/SR counters never change.
- **Crafting debug toggle.** The DEBUG button in CraftingWorkshopUI flips the static `_debugMode`, which is copied into `CraftingValidator.DebugMode`. That skips the feat, spell, caster level and cost checks, and `CraftingExecutor` (Crafting/CraftingExecutor.cs:34) skips the cost deduction. The setting is session-wide, and the workshop opens only if someone already has a crafting feat.
- **Test encounter presets.** See [Encounters: the four sources](#encounters-the-four-sources). The `Configure*TestParty` methods (_Core/GameManager.TestConfigs.cs) replace the created party for the scenario.
- How to use these tools for manual testing: [TESTING.md](../TESTING.md).
- **Left Ctrl+H** (Utilities/DebugCommands.cs, created by `RuntimeInitializeOnLoadMethod`) cycles `GameSettings.hpCalculationMode` between Roll, Average and Maximum. Right Ctrl does not work, and it works only with the legacy input manager enabled (no Input System branch).

## UI: hotkeys

| Key | Effect | Code |
|---|---|---|
| C | Toggle character sheet; opens only on a PC turn with an ActivePC | InputService -> `GameManager.HandleCharacterSheetInput` |
| K | Toggle skills panel; same PC-turn rule | `GameManager.HandleSkillsInput` |
| I | Does nothing in the shipped scene, because InventoryUI is embedded (`IsEmbedded`, CharacterSheetUI.cs:369) | `GameManager.HandleInventoryInput` |
| Esc or right click | Cancel the current targeting mode | `InputService.HandleCancelInput` |
| Esc | Close the crafting workshop or loot window; in the bull rush chooser it picks 0 extra squares | CraftingWorkshopUI, LootCollectionUI, CombatUI |
| Enter / keypad Enter | Loot All | LootCollectionUI |
| 0-9 (top row or keypad) | Bull rush extra push distance | CombatUI |
| F12, Left Ctrl+H | Debug tools above | SpellTestingPanel, DebugCommands |
| WASD / arrows, wheel or + / - / =, R, left-drag | Pan, zoom, reset, drag-pan (drag is off during targeting sub-phases) | Utilities/CameraController.cs |

Esc has no modal stack, so a single press can reach several handlers. SpellTestingPanel, CraftingWorkshopUI and LootCollectionUI poll the legacy `Input.GetKeyDown` without the `#if ENABLE_LEGACY_INPUT_MANAGER` guard that InputService and CombatUI use. That works only because ProjectSettings sets `activeInputHandler: 2` (Both).

## UI: TextMeshPro vs legacy Text

Almost all UI uses legacy `UnityEngine.UI.Text`. `LegacyRuntime.ttf` is referenced on 63 lines in 35 files; the full fallback chain `LegacyRuntime.ttf` -> `Arial.ttf` -> `Font.CreateDynamicFontFromOSFont` (e.g. `CombatLogPanel.InitializeLogMessagePool`) is present in roughly 23 of them, and the rest use a shorter chain. Only LevelUpUI, ActionButtonPanel, CombatEndXPUI and LootCollectionUI use TMP. Each has its own copy of a resolver that tries `TMP_Settings.defaultFontAsset`, then `Resources.Load("Fonts & Materials/LiberationSans SDF")`, then a runtime `TMP_FontAsset.CreateFontAsset` built from the legacy font (e.g. ActionButtonPanel.cs:1080-1097).

The TMP GUIDs are broken. Assets/TextMesh Pro (33 files) was committed in 0089af1 without `.meta` files, so every machine generates new GUIDs, and references inside the TMP assets no longer resolve. For example, TMP Settings.asset:27 points `m_defaultFontAsset` at GUID 8f586378..., but the local LiberationSans SDF.asset.meta is 005e11d8.... The expected effect is a null default font and an SDF material without a shader, which the resolvers then work around (not verified in Play mode). New UI should use legacy `Text` with the font fallback chain unless the TMP folder is re-imported with its metas.

## UI: recurring bug patterns

The history contains many fixes for the same few problems. Avoid them as follows:

| Symptom | Cause (example fix) | How to avoid |
|---|---|---|
| Panel shows but text is invisible | `Text.font` is null when the only font tried is missing (d5ec5cb, 350fe6a) | Always use the 3-step font fallback |
| Whole panel blank after confirming | The callback re-opened the same panel, then `Close()` hid it (bc0c100) | Call `Close()` before invoking the callback |
| Scroll list empty or not clipped | `UIFactory.CreateScrollPanel` sets no size and adds no `LayoutElement`, so the panel had no usable height inside a LayoutGroup (2306bc1); a fully transparent viewport Image broke Mask clipping (b77b66a). `UIFactory.CreateScrollPanel` itself still creates its viewport Image with alpha 0 (UIFactory.cs:330) | Give scroll panels a `LayoutElement.preferredHeight` or an explicit size; give the viewport Image a non-zero alpha (opaque white in SceneBootstrap, 0.004 in SpellTestingPanel) and set `Mask.showMaskGraphic = false` |
| Blank band at the top of a list | Children anchored at (0.5, 0.5) inside content pivoted at (0.5, 1) (48106c2, 6e542ef; see the `yOffset = contentH / 2` workaround at CharacterCreationUI.cs:735) | Anchor rows to the top, or use VerticalLayoutGroup + ContentSizeFitter |
| Overlapping cards, buttons or footers | Absolute pixel layout with fixed heights (a1f1242, ebf2bd9, f632d63, cbe85d0, fafa38e) | Prefer layout groups; budget widths for every footer button |
| Rows or buttons not clickable | Decorative Text with `raycastTarget = true` on top (56edba2); a control created before the panels that cover it (25387d2) | Set `raycastTarget = false` on labels; create top controls last or call `SetAsLastSibling` |
| Targeting mode cancelled by a UI callback | `GameManager.ShowActionChoices()` resets `CurrentSubPhase` (de5c5c7, guard at GameManager.cs:4646) | Do not call `ShowActionChoices` while a pending scroll, wand or spell targeting is active |

One known UI defect is worth knowing because it hides itself: `CombatUI.EnsureCombatLogPanel` calls `CombatLogPanel.Initialize` on every `ShowCombatLog`, and `Initialize` builds a new pool of 50 pre-warmed `PooledLogMsg` GameObjects each time. This leak is tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md).

## Creature token art (unused)

Assets/StreamingAssets/CreatureTokens holds 278 PNG tokens (256x256, about 29 MB), `creature_manifest.json` and a README. They were extracted from the Monster Manual PDF in d17fd90 (2026-05-27). No code reads this folder. The loader (`CreatureTokenLoader.cs`) and `NPCDefinition.TokenPath` were reverted in 281dab7 the next day, yet the folder's README still documents them. The only use of `Application.streamingAssetsPath` in the scripts is the dungeon CSV. StreamingAssets ships verbatim in builds, so the art is dead weight with a licensing risk. A spot check by the auditors found several filenames that do not match their art (for example, skeleton.png shows a shocker lizard).

The tokens actually used come from `IconLoader.GetToken` (UI/Common/IconLoader.cs). It loads Resources/Icons/Tokens/{type}_token (11 PNGs: barbarian, cleric, fighter, goblin, monk, orc, rogue, skeleton, wizard, wolf, zombie) with Resources/Sprites/Icons as a fallback. For NPCs the type comes from `IconLoader.DetermineMonsterType`, a substring match on the name: "orc" also matches "Sorcerer", and the "hobgoblin" branch is unreachable. Every other monster gets Resources/Sprites/npc_enemy_alive tinted with `NPCDefinition.SpriteColor`.
