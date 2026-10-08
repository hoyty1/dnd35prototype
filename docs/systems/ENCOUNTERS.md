> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-03.

# Encounters

Random encounters are the core game mode: the party fights an encounter, collects treasure and XP, rests and picks the next encounter. This guide covers where encounters come from, how a DMG table row becomes creatures on the grid, the CR/EL/XP/treasure math, and how far the code is from the DMG random-encounter rules. The aim is to help you extend the system toward DMG fidelity. Known bugs are cited by issue ID (see [../issues/ENC.md](../issues/ENC.md) and [../issues/CRE.md](../issues/CRE.md)) rather than re-described.

Everything here was checked by reading code. None of it was re-checked in Play mode. Counts marked "static" come from a script that re-implements the C# parsing and lookup rules against the ID literals in the source (see [Content coverage](#7-content-coverage)).

Related docs: [../architecture/ai-encounters-ui.md](../architecture/ai-encounters-ui.md) (overview of the same code), [../DEVELOPMENT_RECIPES.md](../DEVELOPMENT_RECIPES.md#change-dungeon-random-or-custom-encounters) (step-by-step changes), [PARTY_MANAGEMENT.md](PARTY_MANAGEMENT.md) (the between-battle loop). Enemy AI (routines, profiles, spellcasting, its known issues) is covered in [AI.md](AI.md); this guide only covers how spawned enemies get their AI settings.

## 1. The loop at a glance

```
GameManager.PromptEncounterSelection                       _Core/GameManager.cs:785
  EncounterSelectionUI.Open(presets, onSelect, onStartRandomEncounter, onCancel, party levels)
    preset card        -> onSelect(presetId)       -> ApplyEncounterPreset       GameManager.cs:1769
    "Random Encounter" -> RandomEncounterGeneratorUI -> RandomEncounterSystem.Generate
    "DMG Tables"       -> DungeonEncounterGeneratorUI -> DungeonEncounterTableManager.GenerateRandomEncounter
                          -> DungeonEncounterSpawner.PrepareEncounter (spawn_* ids)
    "Custom Encounter" -> CustomEncounterBuilderUI
                       (the last three) -> onStartRandomEncounter(ids, generated) -> ApplyRandomEncounter   GameManager.cs:1690
  SetupEnemyEncounter(ids)                                  _Core/GameManager.NPCSetup.cs:29
  OpenPreCombatHubPhase (stash, store, spell prep, crafting, Start Encounter)   GameManager.cs:1403
  ... combat ...
  BeginPostCombatLootCollection                             _Core/GameManager.LootCollection.cs:25
    GeneratePostCombatTreasure (EL -> DMG Table 3-5), coins auto-collected, LootCollectionUI
  ShowPostCombatXPFlow -> ExperienceCalculator.CalculateXPForCombat -> CombatEndXPUI -> level-ups
  ContinueToRestAndNextCombat -> RestorePartyAfterCombat (full rest, CORE-015) -> ReturnToEncounterSelection
```

The enemies are spawned when the encounter is chosen, before the pre-combat hub opens. There is no overworld, dungeon map, travel clock or wandering-monster check. The player picks the next encounter by hand every time.

## 2. The four encounter sources

All four end in a `List<string>` of NPC IDs handed to `GameManager.ApplyEncounterPreset` or `GameManager.ApplyRandomEncounter`.

| Source | Main code | Picks creatures by | Handoff |
|---|---|---|---|
| Preset cards | `NPCDatabase.ListEncounterPresets` (Character/Creatures/NPCDatabase.cs:86) | Fixed ID list | `onSelect(presetId)` -> `ApplyEncounterPreset` |
| Random CR/EL generator | Encounters/RandomEncounterSystem.cs, UI/Encounter/RandomEncounterGeneratorUI.cs | Party level, difficulty, CR bands | `onStartRandomEncounter(NpcIds, generated)` |
| DMG dungeon tables | Encounters/DungeonEncounterTableManager.cs and friends, UI/Encounter/DungeonEncounterGeneratorUI.cs | d% roll on a dungeon-level table | `EncounterSelectionUI.OnDMGTablesPressed` runs `PrepareEncounter`, then `onStartRandomEncounter(result.EnemyIds, null)` |
| Custom builder | UI/Encounter/CustomEncounterBuilderUI.cs | Player picks creatures | `onStartRandomEncounter(ids, new GeneratedRandomEncounter { IsCustomEncounter = true })` |

### 2.1 Presets, including test presets

- `ListEncounterPresets` builds 55 presets inline and appends, in this order, `GetDragonEncounterPresets` (15), `GetSkeletonEncounterPresets` (5), `GetZombieEncounterPresets` (5) and `GetLycanthropeEncounterPresets` (7): 87 in total. The list is rebuilt on every call.
- 22 test preset IDs are constants in _Core/GameManager.cs:100-121 (`grapple_test` through `mirror_image_test`). `ApplyEncounterPreset` sets one `_is*TestEncounter` flag per ID. The flags pick a `Configure*TestParty` party layout and, for many, a fixed spawn array in `SetupEnemyEncounter` (CORE-004, CORE-025). `test_2_goblins` and `xp_levelup_test` (one CR 15 `xp_pinata_goblin`) are test presets with no flag.
- An unknown ID silently loads the first preset (ENC-012). Cancel, or an empty preset ID, loads `goblin_raiders`. An empty ID list from any source falls back to goblin_warchief, hobgoblin_sergeant and skeleton_archer (both `ApplyRandomEncounter` and `ApplyEncounterPreset` do this).
- Preset cards show `ChallengeRatingUtils.CalculateEncounterDifficulty`: sum of per-creature XP values, converted back to an equivalent CR, and compared to APL. The tiers are Easy (<= -2), Moderate, Challenging (within 0.5), Hard and Deadly (> +2). These are not DMG labels.

### 2.2 RandomEncounterSystem (CR/EL generator)

`RandomEncounterSystem.Generate(RandomEncounterRequest)`:

1. APL = `ChallengeRatingUtils.CalculateAPL` and target EL = `GetTargetELForDifficulty` (see [section 4](#4-cr-el-and-difficulty)).
2. Candidates (`BuildCandidates`) are every `NPCDatabase.AllNPCs` entry with a CR that `ChallengeRatingUtils.TryParse` accepts, minus summons (`SummonBase`, `SummonAlias` or `Summoned` tag, or "summon" in the ID). Test NPCs are not excluded, so `xp_pinata_goblin` (CR 15) and `mirror_image_test_goblin` (CR 1/3) are candidates (static). Leaked `spawn_*` definitions are candidates too (ENC-004).
3. Filters are substring matches. Environment (Any, Forest, Dungeon, Underground, Urban, Swamp, Desert, Mountain) is matched against `Description`, `CreatureTags`, `CreatureType` and `Name`, because `NPCDefinition` has no environment field. Creature type (Humanoid, Undead, Beast, Monstrous Humanoid, Aberration, Outsider, Construct) is matched against type, tags and name. "Beast" is not a 3.5e type; it matches only names and "Magical Beast".
4. The four strategies are shuffled and each gets up to 220 attempts (`MaxGenerationAttempts`). The first result within 0.75 EL of the target wins; otherwise the closest result overall.

| Strategy | Count (default, hard cap) | Selection |
|---|---|---|
| SingleBoss | 1 | Any candidate with CR in target EL +/- 2 (any candidate if that band is empty) |
| EliteSquad | 2-4, max 8 | One NPC ID repeated n times, CR within 1 (then 2) of `targetEL - GroupElBonusForCount(n)` |
| Swarm | 5-8, max 15 | One NPC ID repeated n times, CR at most `max(0.5, targetEL - bonus + 0.5)` (fallback `max(1, targetEL - bonus + 1.5)`) |
| MixedGroup | 3-7, max 12 | One leader with CR in [L - 2, L + 1], L = max(1, target EL - 1), then minions with CR at most leader CR - 2 (fallback - 1); minions are independent random picks, so IDs can repeat |

The UI's Min/Max Creatures fields override the defaults within the caps (the minimum is still floored at 2, 5 and 3 respectively; SingleBoss is rejected when Min > 1). The generator ignores creature organization, environment data, alignment and treasure, so mixes are not ecologically plausible. Swarm and MixedGroup routinely exceed the 5 standard spawn positions (ENC-001).

### 2.3 DMG dungeon tables

The DMG-style random encounter path. Covered in detail in [section 3](#3-dmg-dungeon-table-system). The generator UI preselects the dungeon level equal to APL (clamped to 1-8) and shows a row of level buttons sized by `DungeonEncounterTableManager.MaxLevel`.

### 2.4 Custom Encounter Builder

Lists `NPCDatabase.AllNPCs` minus `IsTestCreature` matches ("_test", "test_", "_drill", "pinata", "target_dummy"), but not `spawn_*` (ENC-004). Total creatures are capped at `min(8, NPC slots)` (`MaxTotalCreatures = 8`). It shows a live EL from its own `CalculateEncounterLevel` ([section 4](#4-cr-el-and-difficulty)). It is the only source that gets computed spawn positions (`CustomEncounterBuilderUI.CalculateSpawnPositions`, [section 5](#5-spawning)).

### 2.5 Unwired alternatives

- Encounters/GameManager.DungeonEncounters.cs (`OpenDungeonEncounterGenerator`, `StartDungeonEncounter` and 5 more) is a complete second DMG entry path with spawn cleanup, and nothing calls it (ENC-015).
- `QuickSpawnSystem` and `NPCTemplateDatabase` (DMG NPC-by-level templates) are reached only from tests (CRE-020).

## 3. DMG dungeon table system

### 3.1 Files

| File (Assets/Scripts/...) | Role |
|---|---|
| StreamingAssets/dungeon_encounters.csv (under Assets/) | The live table data |
| Encounters/EncounterCSVParser.cs | `ParseCSV` -> `RawEncounterRow` list, `GroupByLevel`, `GetSummary` |
| Encounters/EncounterDescriptionParser.cs | Regex grammar for one description -> `ParsedEncounterDescription` / `ParsedCreatureGroup` |
| Encounters/DiceExpression.cs | Immutable `NdS+M` or fixed count; `Roll()` floors at 1 and uses `UnityEngine.Random` |
| Encounters/DungeonEncounterTableData.cs | `BuildFromCSV`, name resolution, `EstimateEL`, and the hard-coded fallback `BuildAllTables` / `BuildTable1`..`8` |
| Encounters/DungeonEncounterTable.cs, DungeonEncounterTableEntry.cs | One level's d% table (`RollEncounter` uses `DiceRoller.D100`), row type, `CascadeDirection`, `ToEncounterDefinition` |
| Encounters/DungeonEncounterTableManager.cs | Static facade: `LoadTables`, `GenerateRandomEncounter`, cascades, the CSV name map |
| Encounters/EncounterDefinition.cs | `EncounterDefinition` and `EncounterCreatureEntry` (base ID, class/level, template IDs, count or dice) |
| Encounters/DungeonEncounterSpawner.cs | `PrepareEncounter`: clone, class levels, templates, register `spawn_*` IDs |
| UI/Encounter/DungeonEncounterGeneratorUI.cs | Level buttons, roll, preview, Start Combat |

### 3.2 CSV format and content

Header `Dungeon_Level,Roll_Min,Roll_Max,Encounter`. The fourth field is quoted when it contains a comma (3 rows, e.g. `7,36,38,"1 ghost, 5th-level fighter"`). The parser accepts levels 1-20 and d% 1-100, and joins any extra fields back with commas.

| Level | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | Total |
|---|---|---|---|---|---|---|---|---|---|
| Rows | 19 | 25 | 33 | 32 | 38 | 35 | 36 | 35 | 253 |

15 rows are cascades ("Roll on Nth-level table"): levels 2-8 have 01-10 (easier) and 91-100 (harder); level 1 has only 91-100. The level-8 harder row reads "Roll on 8th-level table". That leaves 238 encounter rows with 249 creature groups.

### 3.3 Loading

`DungeonEncounterTableManager.LoadTables` builds the name map once, then reads `Path.Combine(Application.streamingAssetsPath, "dungeon_encounters.csv")` with `File.Exists` / `File.ReadAllLines`. If the file is missing, throws or yields no tables, it uses `DungeonEncounterTableData.BuildAllTables`, whose rows, ranges and counts differ from the CSV (ENC-014). Consequences:

- While the CSV exists, edits to `BuildTable1`..`8` do nothing. Change the CSV and/or the name map.
- `IsCSVLoaded` can be true when the fallback was used (ENC-010).
- The CSV path works only in the Editor and desktop builds (ENC-008).
- There is no per-level fallback. A level absent from the CSV has no table.
- `DungeonEncounterTable.Validate` warns on every load that level 1 lacks an easier cascade (ENC-007). Note that `Validate` sorts `Entries` in place.

### 3.4 Description grammar

`EncounterDescriptionParser.Parse` checks the cascade pattern first, then splits compound rows on `\s+and\s+(?=\d)` (so "1 ettercap and 1d3+1 Medium monstrous spiders" splits but "bugbear" does not). `ParseSingleGroup` tries the patterns in this order; the first match wins.

| # | Pattern | CSV example | Result |
|---|---|---|---|
| 0 | Cascade `Roll on Nth-level table` | `Roll on 3rd-level table` | `IsCascade`, target level (used only for direction) |
| 1 | Dominated NPC | `1 dominated 5th-level human barbarian NPC` | race, class, level, `Notes = "dominated"` |
| 2 | NPC `[count] Nth-level race class NPC[s] [...]` | `1d3 5th-level troglodyte cleric NPCs`; `5th-level lizardfolk druid NPC (with crocodile)` | race, class, level, `Notes = "with crocodile"` |
| 3 | Half-dragon | `1 half-dragon 4th-level fighter` | race hard-coded "human", class, level, template `half-dragon` |
| 4 | Devils comma `N devils, X` | none in current CSV (the row is `1 hellcat (devil)`) | name X, annotation "devil" |
| 5 | Special comma + class | `1 ghost, 5th-level fighter` | base creature as "race", class, level |
| 6 | Special comma + level | `1 ogre barbarian, 4th level` | base creature, class, level |
| 7 | Standard `count name [(annotation)]` | `1d4+1 Large monstrous centipedes (vermin)` | dice, lowercased name, annotation; `(with X)` goes to `Notes` |
| 8 | Fallback | (none in current CSV, static) | count 1, whole text as name, warning logged |

Static tally of the 249 groups: 238 standard, 6 NPC, 2 special comma + class, 1 special comma + level, 1 dominated, 1 half-dragon.

Annotations are parsed, then dropped when `EncounterCreatureEntry` is built. Tags in the CSV: vermin 14, animal 8, lycanthrope 7, genie 5, ooze 4, devil 4, demon 4, ghoul 3, pyro- or cryo- 2, hag 2, fungus 2, eladrin 2, beholder 2, with crocodile 1, mixed types 1. So "pyro- or cryo-" hydras spawn as plain hydras, "(mixed types)" mephits are always `fire_mephit`, dominated NPCs fight as ordinary enemies and the crocodile companion never spawns (ENC-005).

### 3.5 Name resolution

`DungeonEncounterTableData.BuildCreatureFromGroup` resolves the race for NPC groups and the creature name otherwise, through `ResolveCreatureName` (DungeonEncounterTableData.cs:313):

1. Exact key in `_creatureNameMap` (case-insensitive dictionary).
2. Lowercased key.
3. Key with one trailing "s" removed, unless the word ends in "ss" (map lookup only).
4. Otherwise `NormalizeName`: lowercase, spaces and hyphens to "_", apostrophes removed. This is returned as the ID without checking `NPCDatabase`.

Failure modes:

- It never returns null for non-empty input, so the "unresolved" counter, the NPC `RawText` fallback and the load-time "N unresolved creature names" log are dead. A bad ID only shows up at spawn as `[NPCDatabase] Unknown NPC ID` and `[DungeonEncounterSpawner] ... base creature '...' not found in NPCDatabase` (ENC-009).
- Plurals missing from the map stay plural ("1d3 stirges" -> `stirges`; the ID is `stirge`). `EncounterDescriptionParser.Depluralize` exists but has no callers (ENC-002, ENC-019). Do not wire it in unchanged: its "-es" rule mangles words whose singular ends in "e" ("stirges" -> "stirg", "ogres" -> "ogr", "giant stag beetles" -> "giant stag beetl").
- NPC rows resolve the race alone. There is no `human` or `kobold` NPC ID, so human and kobold NPC rows fail, including the half-dragon row (race forced to "human"). Map keys such as "5th-level human monk npc" -> `human_monk_5` are never consulted for NPC rows.
- The map (`DungeonEncounterTableManager.InitializeTypoCorrections`, line 506) has 245 assignments over 226 distinct keys. Four keys are assigned twice with different values and the later one wins ("kobold warrior", "orc warrior", "hobgoblin warrior", "small viper snake"). Several entries point at stand-ins: sized vermin plurals map to smaller variants and "troglodyte zombies" / "human warrior skeletons" map to generic undead, although the right IDs exist (ENC-003). Six- and eight-headed hydras map to the 5- and 7-headed ones because no 6/8-head IDs exist.

### 3.6 Class levels and templates

For NPC groups, `EncounterCreatureEntry.TemplateClass` / `TemplateLevel` come from the regex. `DungeonEncounterSpawner.BuildSpawnDefinition` clones the base definition, then calls `CreatureClassEngine.ApplyClassToDefinition` with `ClassRegistry.GetClass(TemplateClass)` and renames the creature "Base Class Level". Points to know:

- `ApplyClassToDefinition` adds class HD, HP, CR and class BAB to `NPCDefinition.BAB`, but spawn never reads that field (BAB and saves follow creature-type progressions), and it adds no saves, feats, skills or gear (CRE-004). NPC gear by level (DMG ch.4) is not modelled.
- Class levels stack on whatever the base stat block already has. `ghost` (5 HD, CR 7) and `vampire` (Fighter, 7 HD, CR 7) are complete stat blocks, so "1 ghost, 5th-level fighter" becomes about 10 HD and "1 vampire, 5th-level fighter" about 12 HD. In the DMG these rows mean a 5th-level fighter with the ghost or vampire template.
- `UpdateAIForClass` overwrites `AIBehavior` / `AIProfileArchetype` by class: Wizard/Sorcerer -> RangedKiter + Evoker, Cleric -> RangedKiter + Healer, Druid/Bard/Adept -> RangedKiter + Spellcaster, Ranger -> RangedKiter + Ranged, Barbarian -> AggressiveMelee + Berserk, Fighter/Paladin/Warrior/Monk -> AggressiveMelee (+ Humanoid if None/Animal), Rogue -> DefensiveMelee (+ Humanoid).
- Template IDs are added to `AppliedTemplateIds` and applied with `CreatureTemplateRegistry.ApplyTemplatesClone`. The registry knows only skeleton, zombie, werewolf, wererat, wereboar, weretiger, werebear, celestial and fiendish (Character/Templates/CreatureTemplateFramework.cs). "half-dragon" is skipped silently (ENC-005). `GameManager.BuildEncounterDefinitionForSpawn` then re-applies `AppliedTemplateIds` at spawn (CRE-001).

### 3.7 Rolling, cascades and the level cap

`GenerateRandomEncounter(dungeonLevel, partyLevel)` clamps the level to `[MinLevel = 1, EffectiveMaxLevel]` (the largest loaded table key), then `RollWithCascade`:

- `DungeonEncounterTable.RollEncounter` rolls d% and finds the row.
- A cascade row moves exactly one level: Easier -> level - 1, Harder -> level + 1, clamped. The number in the CSV text only sets the direction (Easier if it is below the row's level). At the edges the same table is re-rolled.
- After `MaxCascadeDepth = 3` cascades it takes `GetFallbackEntry`, the first non-cascade row of the current table.
- `partyLevel` is only logged (ENC-006).

`entry.ToEncounterDefinition()` clones the creature entries and calls `EncounterCreatureEntry.ResolveCount` on each, so dice are rolled once per generated encounter. The definition gets `TargetEL` = the row's EL (always the level of the table the row came from, from `EstimateEL`) and `Environment = "Underground"`. The spawner sees only an int `Count` (floored to 1).

`MaxLevel` and `HardcodedMaxLevel` are both the const 8. Level 9 was implemented and then removed (commit 6008d6f). Re-adding deeper levels needs CSV rows, a new target for the level-8 harder cascade, `MaxLevel` raised (it sizes the UI button arrays), the UI's hard-coded "(1-8)" text, and Phase5IntegrationTests fixed (it still asserts 9 levels, ENC-007).

### 3.8 Preparing the spawn

`DungeonEncounterSpawner.PrepareEncounter` (DungeonEncounterSpawner.cs:73) calls `NPCDatabase.Get(BaseCreatureId)` per entry. A missing ID adds a warning and skips the whole group. Each copy gets a temporary ID, `spawn_{baseId}_{counter}` or `spawn_{baseId}_{class}_{level}_{counter}`, "#n" appended to its name when there are several, and is registered with `NPCDatabase.RegisterExternal`. `SpawnResult.IsValid` needs at least one creature. If none resolves, `OnDMGTablesPressed` logs a warning and reopens the selection panel. A partly resolved row starts with fewer creatures and no message to the player. `CleanupSpawnEntries` is never called on this path, so `spawn_*` definitions accumulate for the session (ENC-004).

## 4. CR, EL and difficulty

### 4.1 What the DMG does

DMG ch.3 ("Encounter Level"): a single creature's EL equals its CR; each doubling of the number of same-CR creatures adds 2 to the EL (two = CR + 2, four = CR + 4); mixed groups are combined by equivalent value. An encounter whose EL equals the party level is a challenging one, expected to use roughly a fifth of the party's resources, and the DMG recommends a mix of easier and harder encounters. The party level is the average character level, adjusted for parties smaller or larger than four. Check the exact tables and thresholds in DMG ch.3 before encoding them; they were not re-checked for this doc.

### 4.2 What the code does

There are five EL calculations and three APL calculations, and they disagree (ENC-013, CRE-027 for the CR parsers):

| Function | Used for | Formula |
|---|---|---|
| `ChallengeRatingUtils.GetELForSameCrGroup` / `CalculateEncounterEL` (NPCDatabase.cs) | RandomEncounterSystem `ActualEL` | Same CR: CR + 1 + ceil(log2 n) (2 -> +2, 3-4 -> +3, 5-8 -> +4, 9-16 -> +5). Mixed: sum `GetXpForCr`, then the smallest table CR whose value is at least the sum |
| `RandomEncounterSystem.GroupElBonusForCount` | Choosing CR bands per strategy | 1 + ceil(log2 n) |
| `CustomEncounterBuilderUI.CalculateEncounterLevel` | Builder EL display | Same step table per CR group (17+ -> +6), then, from the highest group down, +1 for each group within 1 EL of the running total, +0.5 within 2, nothing beyond |
| `EncounterService.CalculateEncounterLevel` (static) | Treasure EL | Highest CR + 2 * floor(log2 n) over all defeated enemies |
| `DungeonEncounterTableData.EstimateEL` | DMG table display | The dungeon level |

Deviations from the DMG: the 1 + ceil(log2 n) rule gives four creatures CR + 3 instead of + 4 and eight creatures CR + 4 instead of + 6, so large groups are under-rated. The treasure formula applies the count bonus to the highest CR, so one CR 7 leader with eight CR 1/2 minions is rated EL 13.

`ChallengeRatingUtils.CrToXp` is commented as the "DMG 3.5e XP table" but is not a DMG table (not re-checked against the book; it does not match the DMG ch.2 XP award table at any character level). It maps CR 1-3 to 300/600/900 and CR 4-20 to 1,200, 1,600, 2,400, 3,200, 4,800 ... 307,200 (doubling every two CRs), with fractions 1/10 to 1/2 at 15-150. It feeds the mixed-group EL above, the generator's "Total XP" line, preset-card tiers and the session XP statistic. It does not affect XP awards.

APL: `ChallengeRatingUtils.CalculateAPL` rounds the average, subtracts 1 for fewer than 4 PCs and adds 1 for more than 6. `ExperienceCalculator.CalculateAPL` rounds to the nearest 0.5 with no size adjustment. `EncounterService.CalculateAPL` (floor) has no callers. `GameManager.GetCurrentPartyAverageLevel` (rounded average) is passed to `EncounterSelectionUI.Open`, which only uses it to fill default party levels and then replaces it with `ChallengeRatingUtils.CalculateAPL`.

### 4.3 Difficulty bands

`RandomEncounterDifficulty` sets target EL = APL + offset, minimum 1: Easy -1, Average 0, Challenging +1, Hard +2, Epic +3. The generator UI defaults to Challenging, which is APL + 1, while the DMG calls EL = party level challenging. Nothing tracks the mix of difficulties across a session, and party resources (spells, HP) do not influence generation.

## 5. Spawning

`GameManager.SetupEnemyEncounter` (GameManager.NPCSetup.cs:29) handles every source:

- **15 NPC slots.** `SceneBootstrap.CreateCharacters` makes 15 enemy GameObjects once (`totalEnemySlots`, SceneBootstrap.cs:119). `spawnCount = min(ids.Count, NPCs.Count)`; extra IDs are dropped silently and unused slots are deactivated. Only 3 NPC stat panels exist (SceneBootstrap.cs:181). The slots are created as `CharacterTeam.Enemy`, so every spawned creature is hostile unless a test-scenario override (`ApplyScenarioSpawnOverrides`, e.g. the celestial/fiendish template tests) moves it to the player's team.
- **Definition.** `NPCDatabase.Get(id)` -> `BuildEncounterDefinitionForSpawn` (template application, CRE-001) -> `InitializeNPCFromDefinition` (no alignment, CRE-002; creature-type BAB and saves, CRE-004). An unknown ID deactivates the slot and skips the AI-behavior entry, which shifts `_npcAIBehaviors` against `NPCs` (AI-015).
- **Position**, first match wins: a test-scenario rule or array; custom positions; `EncounterSpawnPositions[i]` (5 cells: (16,6), (14,10), (16,14), (13,8), (13,12); GameManager.cs:2864); otherwise `(15 + i, 10)`, which is off the 20x20 grid from the 6th enemy on (ENC-001). 16 of the 238 CSV encounter rows can roll more than 5 creatures (largest: 13, "1d3+1 ghasts (ghoul) and 2d4+1 ghouls"; static).
- **Size.** A creature's footprint extends +x/+y from its base cell (`SquareGrid.GetOccupiedSquares`). Nothing checks bounds or overlap at spawn, so Huge and larger creatures in neighbouring default cells can overlap, and very large creatures near the right edge extend off the grid. `CustomEncounterBuilderUI.CalculateSpawnPositions` picks random cells with x 11-18 and y 2-18, at least 2 squares apart (relaxed if it runs out), and is also size-blind.
- **Distance.** PCs start at (3,6), (3,9), (3,12), (3,15). Each default enemy cell is 10-13 squares (50-65 ft) from the nearest PC, so every encounter that uses the default cells starts at the same range. No encounter-distance roll and no surprise round (CMB-028); everyone is aware at the start.
- **AI.** Each NPC gets the `AIBehavior` and `AIProfileArchetype` from its definition (adjusted by `UpdateAIForClass` for DMG class-leveled spawns). There is no encounter-level AI: no group roles, leader/minion coordination, morale or retreat (AI-010), surrender or parley. How behaviours and archetypes become turn routines is in [AI.md](AI.md#6-profiles-and-archetypes); morale is in [AI.md](AI.md#89-morale-and-fleeing).
- **Tokens.** `IconLoader.DetermineMonsterType(def.Name)` keyword match, otherwise a tinted generic sprite.

## 6. After the fight: treasure, loot and XP

### 6.1 Treasure

`GameManager.GeneratePostCombatTreasure` (_Core/GameManager.TreasureGeneration.cs) runs once per victory:

1. Collect enemies with HP <= 0.
2. EL = `EncounterService.CalculateEncounterLevel(enemies)`, clamped to 1-20 ([section 4.2](#42-what-the-code-does)).
3. `DND35e.Treasure.TreasureGenerator.Generate(el)` rolls coins, goods (gems/art) and items from `TreasureData.Table3_5` (EL 1-20, DMG ch.3 Table 3-5 and sub-tables).
4. `BeginPostCombatLootCollection` adds coins to party gold automatically, converts gems, art, mundane and magic results to `ItemData` with Appraise checks (`TreasureItemConverter.ConvertAll`), and shows them in `LootCollectionUI` together with the defeated enemies' carried items and ground items.

Deviations: every encounter gets full Table 3-5 treasure, including animals and vermin that have no treasure in the MM, and nothing doubles it for "double standard" creatures. The enemies' own gear is looted on top and `monsterGearGP` is not passed, so the DMG rule that NPC gear counts against the encounter's treasure is not applied. Generated items are sell-only placeholders (ITM-016). There is no wealth-by-level check (DMG ch.5 character wealth by level).

### 6.2 XP

`ShowPostCombatXPFlow` -> `ExperienceCalculator.CalculateXPForCombat(party, defeatedEnemies)`:

- Defeated enemies are those `RegisterDefeatedEnemyForXP` recorded: team Enemy, HP <= 0, no regeneration or fast healing. Enemies that are not reduced to 0 HP give nothing.
- The party is every PC whose GameObject is active (`IsActiveCombatant` in `ShowPostCombatXPFlow`, GameManager.LootCollection.cs:188; `IsActiveCombatant` checks `activeInHierarchy` and `Stats`, not HP). Nothing deactivates a PC when it dies (only the test party layouts hide PCs), so dead PCs count toward the APL and the split and receive a full share; they can even level up while dead.
- Per enemy: CR (fallback: level) minus APL (nearest 0.5), rounded and clamped to -8..+12, looked up in a flat table (0 -> 300, +1 -> 400, +2 -> 600, -1 -> 200 ...). The total is divided evenly (integer division) among that party, and `CharacterStats.AddExperience` handles level-ups.
- DMG ch.2 awards XP per character from a table indexed by character level and CR. The code's table does not scale with level, so a CR = APL creature is worth 300 XP at any level (CHR-004).
- `ExperienceCalculator.GetXPForLevel` is the PHB level table (n(n-1)/2 x 1,000).
- `EncounterService` has its own `AwardXP` and defeated-enemy tracking that nothing calls (CORE-023).

### 6.3 Then

`ContinueToRestAndNextCombat` -> `RestorePartyAfterCombat` (automatic full rest that also revives dead PCs, CORE-015) -> `ReturnToEncounterSelection`. The between-battle loop is covered in [PARTY_MANAGEMENT.md](PARTY_MANAGEMENT.md).

## 7. Content coverage

Static count, using a re-implementation of `EncounterDescriptionParser`, `ResolveCreatureName` and the name map against the NPC ID literals in Character/Creatures and the skeleton, zombie, lycanthrope and dragon factories. It confirms the figures in ENC-002. Not verified in Play mode.

| Measure | Count |
|---|---|
| Encounter rows (non-cascade) | 238 |
| Rows whose every group resolves to an existing ID | 202 |
| Rows that spawn only some groups | 6 |
| Rows that spawn nothing (selection panel reopens) | 30 |
| Creature groups that fail to resolve | 36 of 249 |
| Distinct IDs referenced / existing | 195 / 165 |
| Rows that resolve but to a wrong or weaker variant (ENC-003) | 7 |

The 30 rows that spawn nothing:

- Plurals not in the map (the singular ID exists): stirges (L1, L3), gnolls, krenshars, ghouls, grimlocks, troglodytes, worgs, bugbears, dire bats, doppelgangers, ogres, vargouilles, cockatrices, gricks, shadows, weretigers, manticores, minotaurs, flamebrother salamanders, wights, barghests, gauths, mummies, trolls, giant stag beetles.
- No base ID: "advanced megaraptor skeletons" (no advancement support; `skeleton_megaraptor` exists), "5th-level human monk NPC", "5th-level kobold sorcerer NPC", "1 half-dragon 4th-level fighter".

The 6 partial rows lose wererats, gnolls, troglodytes, shriekers, ghouls and the dominated human barbarian, and keep their partners (dire rats, hyenas, monitor lizards, violet fungi, ghasts, formian taskmaster).

A plural fix needs no new creatures: try the name as is, then with "ies" -> "y", "fungi" -> "fungus", one trailing "s" removed and "es" removed, and keep the first candidate that `NPCDatabase` knows (through the map or normalized). Static result: 233 rows resolve fully, 1 partially (the dominated human barbarian) and 4 not at all. What then remains needs new modelling: a race-only humanoid base for NPC rows (human, kobold), the half-dragon template, monster HD advancement, and real ghost/vampire templates for the fighter rows.

How many monsters the database holds is known only at runtime (CRE-025): read the "[NPCDatabase] Initialized with N NPC types." log line.

## 8. DMG coverage

| DMG/PHB/MM rule | Status | Notes |
|---|---|---|
| Dungeon random encounter tables by dungeon level (DMG ch.3) | Partial | Levels 1-8 transcribed (253 rows); deeper levels missing; about 13% of rows spawn nothing (section 7) |
| Easier/harder cascades between level tables | Implemented | Always one level, depth 3, boundary re-rolls its own table |
| Wilderness and terrain encounter rules (DMG ch.3) | Missing | Only a text-substring "environment" filter in RandomEncounterSystem; no terrain on the map |
| Wandering monster checks (DMG ch.3) | Missing | No time, travel or rest interruption; the player picks every encounter |
| Encounter distance and noticing the enemy (DMG ch.3; Spot/Listen, PHB ch.4) | Missing | Fixed 50-65 ft start; no distance roll |
| Surprise and awareness (PHB ch.8) | Missing | No surprise round, no flat-footed start (CMB-028) |
| EL from multiple creatures (DMG ch.3) | Partial | Five formulas, none matching the doubling rule for large groups (ENC-013) |
| Party level and difficulty bands (DMG ch.3) | Partial | APL with size adjustment; difficulty offsets -1..+3; no session difficulty mix |
| XP awards (DMG ch.2) | Partial | Flat CR - APL table, not by character level (CHR-004); kill-only |
| Treasure per encounter by EL (DMG ch.3 Table 3-5) | Partial | Rolled every encounter; EL formula inflated; MM treasure ratings and NPC gear offset ignored; items are placeholders (ITM-016) |
| Wealth by level (DMG ch.5) | Missing | No check or correction |
| NPC encounters with class levels (DMG ch.4) | Partial | Class HD/HP/CR only (CRE-004); human/kobold bases missing; DMG NPC templates unwired (CRE-020); no NPC gear by level |
| Monster templates in encounters (MM) | Partial | 9 registered templates; half-dragon, ghost and vampire templates missing; double application (CRE-001) |
| Monster advancement by HD/size (MM) | Missing | No advancement code |
| Creature organization and numbers (MM) | Missing | RandomEncounterSystem ignores organization; the DMG tables carry their own counts |
| Dominated creatures and companions in table rows | Missing | Parsed, then dropped (ENC-005) |
| Alignment of spawned monsters (MM) | Missing at spawn | `CharacterAlignment` not copied (CRE-002) |
| Morale, fleeing, surrender | Missing | No morale model (AI-010) |
| Story and ad hoc XP awards (DMG ch.2) | Missing | Only defeated-enemy XP |

### 8.1 DMG wandering-monster check (rules summary)

This is a summary of the rules as written (DMG ch.3, with Spot and Listen from PHB ch.4 and surprise from PHB ch.8), not a description of the code. It was not re-checked against the book; confirm the per-area chances, intervals and distance dice in DMG ch.3 before encoding them.

- **The check.** At fixed intervals of game time (for example while the party explores, travels or rests) the DM rolls d% against a chance that depends on the area. Busy or dangerous areas get a higher chance or more frequent checks; quiet or civilized areas fewer. On a hit, the DM rolls on the area's encounter table (in a dungeon, the table for that dungeon level; outdoors, the terrain's table).
- **Encounter distance.** Each terrain type in DMG ch.3 gives a dice formula for the distance at which an encounter begins; dense cover (thick forest, marsh) gives short distances and open ground (plains, desert) long ones. In a dungeon, line of sight, light and walls limit the distance.
- **Noticing and surprise.** Each side makes Spot checks (sight; -1 per 10 ft of distance) and Listen checks (sound) to notice the other, opposed by Hide and Move Silently when the other side is trying to stay unnoticed. Combatants who are aware of their foes act in a surprise round; those who are not are flat-footed until they first act.

### 8.2 Entry points for a wandering-monster feature

What a feature that interrupts a rest or a journey with an encounter would call today. All verified by reading code.

| Step | Code | Notes |
|---|---|---|
| Roll an encounter | `DungeonEncounterTableManager.GenerateRandomEncounter(dungeonLevel, partyLevel)` (static, returns `EncounterDefinition`), or `RandomEncounterSystem.Generate` | Same paths as the manual sources (section 2) |
| Prepare and load it | `StartDungeonEncounter(EncounterDefinition)`, `StartDungeonEncounterFromResult(SpawnResult)` and `PrepareDungeonEncounter` in Encounters/GameManager.DungeonEncounters.cs (public, lines 42, 105, 136) | Unwired (ENC-015). Runs `DungeonEncounterSpawner.PrepareEncounter`, cleans up the previous encounter's `spawn_*` entries, then calls `ApplyRandomEncounter(ids, null)`. It does not open the pre-combat hub and does not start combat. The best candidate entry point |
| Load enemies | `GameManager.ApplyRandomEncounter(ids, generated)` (private, GameManager.cs:1690) | Clears every test flag, calls `RestoreStandardPartyLayout` (GameManager.TestConfigs.cs:1889), then `SetupEnemyEncounter`, `SetupNPCIcons` and `UpdateAllStatsUI`. `RestoreStandardPartyLayout` re-activates PC1-PC4 and their panels; it does not move them |
| Pre-combat hub | `OpenPreCombatHubPhase` (private, GameManager.cs:1403) | Opened by the `onStartRandomEncounter` callback in `PromptEncounterSelection` right after `ApplyRandomEncounter`, not by `ApplyRandomEncounter` itself. It unlocks the stash and offers the store, spell preparation and crafting, which is wrong for an ambush during a rest |
| Start combat | `ForceStartEncounterFromPreCombat` (private, GameManager.cs:1598; closes the hub UIs, locks the stash, calls `StartCombat`) or public `StartCombat()` (GameManager.cs:3634) | `StartEncounterFromPreCombat` adds the unprepared-caster warning |

Things that are wrong for an interrupted rest and need changing, not just calling:

- The rest itself is `RestorePartyAfterCombat` (GameManager.cs:868): an all-at-once full rest (CORE-015) that also moves the PCs back to the start squares (3,6), (3,9), (3,12), (3,15). It runs before the next encounter is chosen, so there is no partly rested state to interrupt. A wandering-monster check needs the rest split into time steps, with the check between steps and the PCs left where they are.
- Enemies use the fixed `EncounterSpawnPositions` (section 5), so there is no encounter distance; there is no surprise round (CMB-028).
- `ApplyRandomEncounter` re-activates all four PCs, which overrides any party layout set before it.

## 9. Gaps and backlog toward DMG fidelity

Ordered by value for the "random DMG encounters" game mode. Each item names the code it touches.

1. **Make every CSV row spawn.** In `DungeonEncounterTableData.ResolveCreatureName`, try singular candidates and keep the first that `NPCDatabase.Get` knows (section 7; not `Depluralize` as written); return null when nothing matches so the load log reports real failures (ENC-002, ENC-009). Fix the stand-in entries (ENC-003). Add a check (extend `DungeonEncounterTableManager.RunIntegrationTest`, ENC-011) that runs `PrepareEncounter` for every row and fails on any unresolved group.
2. **Spawn any group size on the grid.** One size-aware, bounds-checked placement routine for all non-test encounters, used instead of `EncounterSpawnPositions` and the `(15 + i, 10)` fallback (ENC-001). Decide whether 15 slots is enough (the CSV peaks at 13; Swarm can ask for 15).
3. **One CR/EL/APL module.** Consolidate into `ChallengeRatingUtils` with the DMG doubling rule and a DMG mixed-group method, and use it for the generator, the custom builder, DMG table rows (replace `EstimateEL`) and treasure (ENC-013, ENC-006).
4. **DMG XP.** Index awards by each character's level and CR (DMG ch.2), award XP for enemies defeated by any means (fled, surrendered, captured) once morale exists (CHR-004).
5. **Treasure by the book.** Add a treasure rating to `NPCDefinition` (none / standard / double / triple), scale or skip Table 3-5 by it, pass the defeated NPCs' gear value as `monsterGearGP`, and turn generated items into real items (ITM-016).
6. **NPC rows.** Add race-only humanoid bases (human, kobold and so on) or map NPC races to existing NPC IDs; give class levels full BAB/saves/feats (CRE-004); give NPCs gear by level (DMG ch.4); consider wiring `QuickSpawnSystem` (CRE-020).
7. **Templates in rows.** Add half-dragon, ghost and vampire templates to `CreatureTemplateRegistry`, warn on unknown template IDs, and apply them to the class-leveled base instead of stacking class levels on monster stat blocks (ENC-005, CRE-001).
8. **Use the parsed notes.** Spawn "(with X)" companions, make "dominated" NPCs a separate side, honour "(pyro- or cryo-)" and "(mixed types)" (ENC-005).
9. **Deeper dungeon levels.** Transcribe the remaining DMG dungeon tables, retarget the level-8 harder cascade, raise `MaxLevel` and drive the UI from `EffectiveMaxLevel` (ENC-007).
10. **Encounter context.** Encounter distance by terrain and a Spot/Listen awareness step, then a surprise round (CMB-028). Terrain-aware maps belong to the grid work.
11. **Campaign-style generation.** A dungeon/wilderness session that rolls wandering-monster checks during travel and rest ([section 8.1](#81-dmg-wandering-monster-check-rules-summary), entry points in [section 8.2](#82-entry-points-for-a-wandering-monster-feature)), chooses the table by dungeon level or terrain, and tracks the session's difficulty mix. This replaces the hand-picked next encounter and the automatic full rest (CORE-015).
12. **Monster data needed by generation.** Environment, organization and advancement fields on `NPCDefinition`, so the CR/EL generator can filter by terrain and build MM-style groups.
13. **Encounter-level AI.** Group tactics, leaders, morale and retreat. Current limits, known issues and options are in [AI.md](AI.md) (sections 10 and 13).
14. **Clean-up.** Call `DungeonEncounterSpawner.CleanupSpawnEntries` after combat and exclude `spawn_*` from both list builders (ENC-004); delete or adopt the unreferenced `GameManager.DungeonEncounters` partial (ENC-015); remove the example classes and dead helpers (ENC-016 to ENC-019); load the CSV through a TextAsset or `UnityWebRequest` (ENC-008); exclude test NPCs from RandomEncounterSystem candidates.

## 10. Testing hooks

- Assets/Scripts/Tests/Encounters/Phase5IntegrationTests.cs: static `RunAll()`, no callers. Covers CSV load, parser patterns, dice and cascades. Stale: asserts 9 levels (ENC-007), and its bulk check only tests for non-null definitions (ENC-011).
- Assets/Scripts/Tests/Character/RandomEncounterSystemTests.cs: APL, same-CR and mixed-CR EL, difficulty offsets, generation sweeps.
- Assets/Scripts/Tests/Character/ExperienceCalculatorTests.cs and Assets/Scripts/Tests/Inventory/PostCombatLootCollectionTests.cs cover the post-combat side.
- In Play mode, roll DMG tables and watch for `[DungeonEncounterSpawner]` "not found in NPCDatabase" warnings and `[EncounterTableData]` load warnings. How to run the suites is in [../TESTING.md](../TESTING.md); compile-check with `bash tools/compile_check.sh`.
