# Folder Structure Reorganization Proposal

## Phase 5A — D&D 3.5e Prototype

**Date:** 2026-05-27  
**Scope:** `Assets/Scripts/` (644 C# files across 60+ directories)

---

## 1. Executive Summary

The current `Assets/Scripts/` hierarchy has grown organically and suffers from **fragmentation**, **mixed concerns**, and a **god-object GameManager** (45 partial files, 44,000+ lines). This proposal reorganizes the codebase into a **domain-driven folder structure** that mirrors game systems, improves discoverability, co-locates tests with their domain, and reduces the cognitive load of finding where any piece of logic lives.

---

## 2. Current Structure — Problems Identified

### 2.1 GameManager God Object (Critical)

| Metric | Value |
|--------|-------|
| Partial files in `Core/` | 45 |
| Total lines | 44,087 |
| Responsibilities | Spell casting, combat actions, NPC setup, loot, treasure, domain powers, walls, grease, mirror image, testing panels, dispel/counterspell |

The single `GameManager` class has accreted every new feature via partial files. This is the single largest architectural debt item.

**Files affected:**
- `GameManager.cs` (main)
- `GameManager.CombatActions.cs`, `GameManager.CombatFlowAccessors.cs`
- `GameManager.SpellCasting.cs`, `GameManager.DispelCounterspell.cs`
- `GameManager_Spells_A.cs` through `GameManager_Spells_W.cs` (20 alphabetical spell files)
- `GameManager_Spells_Cantrips.cs`, `GameManager_Spells_Phase1.cs`, `GameManager_Spells_Phase2.cs`, `GameManager_Spells_Shared.cs`, `GameManager_Spells_MagicFang.cs`
- `GameManager_FlamingSphere.cs`, `GameManager_Grease.cs`, `GameManager_WallOfFire.cs`, `GameManager_WallOfForce.cs`, `GameManager_WallOfIce.cs`
- `GameManager_DomainAreaSpells.cs`, `GameManager_DomainPowers.cs`, `GameManager_DomainSpells.cs`
- `GameManager_ConcealmentAreas.cs`, `GameManager_HolyAreas.cs`, `GameManager_MirrorImage.cs`
- `GameManager.NPCSetup.cs`, `GameManager.NPCTurns.cs`
- `GameManager.LootCollection.cs`, `GameManager.TreasureGeneration.cs`
- `GameManager.TestConfigs.cs`, `GameManager.TestPanel.cs`

### 2.2 Combat Logic Fragmentation

Combat-related code is spread across **4+ top-level folders**:

| Location | Files | Contents |
|----------|-------|----------|
| `Combat/` | 34 | Conditions, calculators, logging, initiative, status effects |
| `CombatSystems/` | 11 | Grapple, overrun, maneuvers, turn undead, melee reactions |
| `Core/GameManager.CombatActions.cs` | 1 | Combat action dispatch |
| `Core/GameManager.CombatFlowAccessors.cs` | 1 | Combat flow accessors |
| `Mounts/` | 4 | Mounted combat system |
| `Services/CombatFlowService.cs` | 1 | Combat flow orchestration |

A developer looking for "how attacks work" must search 4+ directories.

### 2.3 Magic / Spell System Disorganization

Spell logic is scattered across **5 locations**:

| Location | Files | Purpose |
|----------|-------|---------|
| `Core/GameManager_Spells_*.cs` | 25 | Spell resolution by letter (A–W) |
| `Magic/` | 19 | SpellData, SpellDatabase, SpellCaster, casting helpers |
| `Magic/AreaEffects/` | 31 | Area-of-effect spell implementations |
| `Magic/StatusEffects/` | 28 | Buff/debuff data |
| `Magic/Spells/Databases/` | 23 | Spell definition databases (A–Z) |
| `Magic/Components/` | 7 | Concentration, metamagic, summon lists |
| `Magic/Spells/WishExecutor.cs` | 1 | Wish spell |
| `Services/SpellApplicationService.cs` | 1 | Spell application |
| `Services/SpellResolutionService.cs` | 1 | Spell resolution |
| `Services/SpellTargetingService.cs` | 1 | Spell targeting |
| `Services/ConcentrationService.cs` | 1 | Concentration |
| `Services/DispelMagicService.cs` | 1 | Dispel magic |

### 2.4 Equipment / Inventory Overlap

| Location | Files | Purpose |
|----------|-------|---------|
| `Inventory/` | 13 | ItemData, ItemDatabase, potions, scrolls, wands, inventory UI |
| `Equipment/` | 34 | Enchantments, rings, rods, staves, wondrous items, materials |
| `Equipment/Behaviors/` | 28 | Specific magic item behaviors |
| `Store/` | 2 | Store UI and inventory |

`ItemData` lives in `Inventory/` but enchantment data lives in `Equipment/`. A "magic sword" requires touching 3+ folders.

### 2.5 Character Data Bloat

`Character/` contains **65 files** mixing:
- Core stats (`CharacterStats.cs`, `CharacterCombatStats.cs`, `HPState.cs`)
- NPC databases (`NPCDatabase_A.cs` … `NPCDatabase_Zombies.cs` — 30 files)
- Race data, deity data, domain data, feats, skills
- Templates (`LycanthropeTemplate.cs`, `SkeletonTemplate.cs`, `ZombieTemplate.cs`)
- Unrelated: `RandomEncounterSystem.cs`, `WizardFamiliar.cs`

### 2.6 Inconsistent Test Organization

Tests are in `Tests/` with per-domain subdirectories, but coverage is uneven:

| Test folder | Files |
|-------------|-------|
| `Tests/Combat/` | 53 |
| `Tests/Services/` | 14 |
| `Tests/Character/` | 10 |
| `Tests/Magic/` | 5 |
| `Tests/Classes/` | 4 |
| `Tests/Maneuvers/` | 4 |
| `Tests/Utilities/` | 3 |
| `Tests/Crafting/` | 1 |
| `Tests/Equipment/` | 1 |
| `Tests/Encounters/` | 1 |
| `Tests/Feats/` | 2 |
| `Tests/Inventory/` | 1 |
| `Tests/Mounts/` | 1 |
| `Tests/AI/` | 1 |

Tests are already domain-grouped but distant from source. Navigation friction is high.

### 2.7 Other Issues

- **`Systems/`** has only 2 files (`EquipmentAssigner.cs`, `QuickSpawnSystem.cs`) — too vague a name for a grab-bag folder.
- **`World/`** has 2 files — could be merged.
- **`Effects/`** (Poisons/Diseases) is separate from `Magic/StatusEffects/` and `Combat/` conditions — related but split.
- **`Identifiers/`** (14 files) — enums and constants that could live with their domains.
- **`Data/NPCTemplates/`** overlaps with `Character/Templates/`.

---

## 3. Proposed New Structure

The reorganization follows **domain-driven grouping**: each game system owns its folder, co-locates its tests, and minimizes cross-folder hunting.

```
Assets/Scripts/
│
├── _Core/                                  ← Bootstrap, global singletons, config
│   ├── GameManager.cs                      ← Slim orchestrator (delegates to services)
│   ├── SceneBootstrap.cs
│   ├── GameSettings.cs
│   ├── GameConfig.cs                       ← Extracted from GameManager
│   ├── GameEventSystem.cs
│   ├── PlaneType.cs
│   ├── GameConstants.cs                    ← From Identifiers/
│   └── Commands/
│       ├── CommandProcessor.cs
│       ├── CombatCommands.cs
│       └── IGameCommand.cs
│
├── Character/                              ← Character definition & progression
│   ├── Stats/
│   │   ├── CharacterStats.cs
│   │   ├── CharacterCombatStats.cs
│   │   ├── CharacterConditions.cs
│   │   ├── CharacterTags.cs
│   │   ├── HPState.cs
│   │   ├── AbilityScoreDamage.cs
│   │   ├── Alignment.cs
│   │   └── StatusTagManager.cs
│   ├── Controller/
│   │   ├── CharacterController.cs
│   │   └── CharacterEquipment.cs
│   ├── Classes/                            ← Merged from Classes/ top-level
│   │   ├── ICharacterClass.cs
│   │   ├── ClassRegistry.cs
│   │   ├── BarbarianClass.cs
│   │   ├── FighterClass.cs
│   │   ├── RogueClass.cs
│   │   ├── ClericClass.cs
│   │   ├── WizardClass.cs
│   │   ├── SorcererClass.cs
│   │   ├── MonkClass.cs
│   │   ├── Bard/
│   │   │   ├── BardClass.cs
│   │   │   ├── BardicKnowledgeData.cs
│   │   │   └── BardicMusicData.cs
│   │   ├── Druid/
│   │   │   ├── DruidClass.cs
│   │   │   ├── WildShapeData.cs
│   │   │   └── WildShapeFormDatabase.cs
│   │   ├── Paladin/
│   │   │   ├── PaladinClass.cs
│   │   │   ├── LayOnHandsData.cs
│   │   │   └── SmiteEvilData.cs
│   │   ├── Ranger/
│   │   │   ├── RangerClass.cs
│   │   │   ├── CombatStyleData.cs
│   │   │   └── FavoredEnemyData.cs
│   │   ├── NPC/
│   │   │   ├── AdeptClass.cs
│   │   │   ├── AdeptSpellList.cs
│   │   │   ├── AristocratClass.cs
│   │   │   ├── CommonerClass.cs
│   │   │   ├── ExpertClass.cs
│   │   │   └── WarriorClass.cs
│   │   └── Shared/
│   │       └── AnimalCompanionData.cs
│   ├── Feats/
│   │   ├── Feat.cs
│   │   ├── FeatDefinitions.cs
│   │   └── FeatManager.cs
│   ├── Races/
│   │   ├── RaceData.cs
│   │   └── RaceDatabase.cs
│   ├── Religion/                           ← Domain/deity data
│   │   ├── DeityData.cs
│   │   ├── DeityDatabase.cs
│   │   ├── DomainData.cs
│   │   └── DomainDatabase.cs
│   ├── Skills/
│   │   ├── Skill.cs
│   │   └── ClassSkillDefinitions.cs
│   ├── Creatures/                          ← NPC databases, creature types
│   │   ├── NPCDatabase.cs
│   │   ├── NPCDatabaseCustom.cs
│   │   ├── NPCDatabase_A.cs
│   │   ├── NPCDatabase_B.cs
│   │   ├── ... (NPCDatabase_C through NPCDatabase_Z)
│   │   ├── NPCDatabase_Dragons.cs
│   │   ├── NPCDatabase_Lycanthropes.cs
│   │   ├── NPCDatabase_Skeletons.cs
│   │   ├── NPCDatabase_Zombies.cs
│   │   ├── DragonData.cs
│   │   ├── SwarmTraits.cs
│   │   ├── CreatureImmunities.cs
│   │   └── CreatureTypeProgression.cs
│   ├── Templates/                          ← Creature templates (merged with Data/NPCTemplates)
│   │   ├── NPCTemplate.cs
│   │   ├── NPCTemplateDatabase.cs
│   │   ├── TemplateData.cs
│   │   ├── TemplateSpellUpdater.cs
│   │   ├── TemplateSpellValidator.cs
│   │   ├── BaseCreatureDefinitions.cs
│   │   ├── CreatureTemplateFramework.cs
│   │   ├── LycanthropeTemplate.cs
│   │   ├── LycanthropeTemplateBase.cs
│   │   ├── SkeletonTemplate.cs
│   │   ├── SkeletonCreatureTemplate.cs
│   │   ├── ZombieTemplate.cs
│   │   └── ZombieCreatureTemplate.cs
│   ├── CreatureClass/
│   │   ├── CRCalculator.cs
│   │   ├── ClassAssociationRules.cs
│   │   ├── CreatureClassEngine.cs
│   │   ├── ECLTracker.cs
│   │   └── StatArrayApplier.cs
│   ├── Progression/
│   │   ├── LevelUpCalculator.cs
│   │   ├── LevelUpData.cs
│   │   └── ExperienceCalculator.cs         ← From Core/
│   ├── Familiar/
│   │   └── WizardFamiliar.cs
│   └── Specialization/
│       └── WizardSpecialization.cs
│
├── Combat/                                 ← All combat mechanics unified
│   ├── Core/                               ← Attack resolution, action economy
│   │   ├── AttackCalculator.cs
│   │   ├── AttackPool.cs
│   │   ├── ActionEconomy.cs
│   │   ├── AoOProvokingAction.cs
│   │   ├── CombatResult.cs
│   │   ├── FullAttackResult.cs
│   │   ├── CombatStateMachine.cs
│   │   ├── DamageModel.cs
│   │   ├── InitiativeSystem.cs
│   │   ├── RangeCalculator.cs
│   │   ├── SizeCategory.cs
│   │   ├── TeamUtility.cs
│   │   └── ThreatSystem.cs
│   ├── Conditions/                         ← Condition data & management
│   │   ├── ConditionManager.cs
│   │   ├── CombatConditionType.cs
│   │   ├── AnimateRopeEntangledConditionData.cs
│   │   ├── AsleepConditionData.cs
│   │   ├── CharmedConditionData.cs
│   │   ├── ColorSprayEffectData.cs
│   │   ├── EnfeebledConditionData.cs
│   │   ├── FascinatedConditionData.cs
│   │   ├── FrightenedConditionData.cs
│   │   ├── TouchOfIdiocyConditionData.cs
│   │   └── WebEntangledConditionData.cs
│   ├── Behaviors/                          ← AI-driven condition behaviors
│   │   ├── CharmedBehaviorController.cs
│   │   ├── ConfusedBehaviorController.cs
│   │   ├── FascinatedBehaviorController.cs
│   │   └── FrightenedBehaviorController.cs
│   ├── StatusEffects/
│   │   └── StatusEffect.cs
│   ├── Maneuvers/                          ← From CombatSystems/
│   │   ├── BaseCombatManeuver.cs
│   │   ├── GrappleSystem.cs
│   │   ├── OverrunSystem.cs
│   │   ├── StandardManeuvers.cs
│   │   ├── SupportActions.cs
│   │   └── ICombatSystem.cs
│   ├── Reactions/                          ← Melee reactions
│   │   ├── FireShieldReactionEffect.cs
│   │   ├── IMeleeReactionEffect.cs
│   │   └── MeleeReactionService.cs
│   ├── Special/                            ← Special combat systems
│   │   ├── NegativeLevelSystem.cs
│   │   ├── TemplateSmiteSystem.cs
│   │   └── TurnUndeadSystem.cs
│   ├── Mounts/                             ← From top-level Mounts/
│   │   ├── MountData.cs
│   │   ├── MountDatabase.cs
│   │   ├── MountSystem.cs
│   │   └── MountedCombatSystem.cs
│   ├── Logging/
│   │   ├── CombatLogHelper.cs
│   │   └── CombatLogger.cs
│   ├── Utilities/
│   │   ├── CombatCalculationService.cs
│   │   └── CombatUtils.cs
│   └── Tests/                              ← Co-located combat tests
│       └── (53 existing test files from Tests/Combat/)
│
├── Spell/                                  ← Consolidated spell system
│   ├── Data/                               ← Spell definitions
│   │   ├── SpellData.cs
│   │   ├── SpellID.cs                      ← From Identifiers/
│   │   ├── SpellNames.cs                   ← From Identifiers/
│   │   ├── SpellSchool.cs
│   │   ├── SpellSlot.cs
│   │   ├── SpellResult.cs
│   │   ├── SpellRanges.cs
│   │   └── SpontaneousCastingData.cs
│   ├── Database/                           ← Spell databases (A–Z)
│   │   ├── SpellDatabase.cs
│   │   ├── SpellDatabase_A.cs
│   │   ├── ... (SpellDatabase_B through SpellDatabase_Z)
│   │   └── SpontaneousCastingType.cs
│   ├── Casting/                            ← Casting pipeline
│   │   ├── SpellCaster.cs
│   │   ├── SpellCastingHelper.cs
│   │   ├── SpellComponentSystem.cs
│   │   ├── SpellSaveResolver.cs
│   │   ├── SpellUtilities.cs
│   │   ├── ImbueSpellEntry.cs
│   │   ├── ImbueWithSpellAbilityManager.cs
│   │   └── PartialCasterData.cs
│   ├── Resolution/                         ← GameManager spell partials → standalone resolvers
│   │   ├── SpellResolver_A.cs              ← From Core/GameManager_Spells_A.cs
│   │   ├── SpellResolver_B.cs              ← From Core/GameManager_Spells_B.cs
│   │   ├── SpellResolver_C.cs
│   │   ├── SpellResolver_Cantrips.cs
│   │   ├── SpellResolver_D.cs
│   │   ├── SpellResolver_E.cs
│   │   ├── SpellResolver_F.cs
│   │   ├── SpellResolver_G.cs
│   │   ├── SpellResolver_H.cs
│   │   ├── SpellResolver_I.cs
│   │   ├── SpellResolver_K.cs
│   │   ├── SpellResolver_L.cs
│   │   ├── SpellResolver_M.cs
│   │   ├── SpellResolver_MagicFang.cs
│   │   ├── SpellResolver_N.cs
│   │   ├── SpellResolver_P.cs
│   │   ├── SpellResolver_Phase1.cs
│   │   ├── SpellResolver_Phase2.cs
│   │   ├── SpellResolver_R.cs
│   │   ├── SpellResolver_S.cs
│   │   ├── SpellResolver_Shared.cs
│   │   ├── SpellResolver_V.cs
│   │   └── SpellResolver_W.cs
│   ├── AreaEffects/                        ← From Magic/AreaEffects/
│   │   ├── AreaEffectManager.cs
│   │   ├── AreaEffectColors.cs
│   │   ├── PersistentAreaEffect.cs
│   │   ├── (all 31 area effect files)
│   │   └── Wind/
│   │       ├── WindEffect.cs
│   │       ├── WindStrength.cs
│   │       ├── WindWallAreaEffect.cs
│   │       └── GustOfWindEffect.cs
│   ├── StatusEffects/                      ← From Magic/StatusEffects/
│   │   ├── (all 28 status effect data files)
│   │   └── ActiveSpellEffect.cs
│   ├── Components/                         ← From Magic/Components/
│   │   ├── ConcentrationManager.cs
│   │   ├── CounterspellData.cs
│   │   ├── MetamagicData.cs
│   │   ├── MetamagicModifier.cs
│   │   ├── SpellcastingComponent.cs
│   │   ├── StatusEffectManager.cs
│   │   └── SummonMonsterLists.cs
│   ├── Domain/                             ← Domain spell logic from GameManager
│   │   ├── DomainAreaSpells.cs             ← From Core/GameManager_DomainAreaSpells.cs
│   │   ├── DomainPowers.cs                 ← From Core/GameManager_DomainPowers.cs
│   │   └── DomainSpells.cs                 ← From Core/GameManager_DomainSpells.cs
│   ├── Special/                            ← Named spell implementations from GameManager
│   │   ├── ConcealmentAreas.cs             ← From Core/GameManager_ConcealmentAreas.cs
│   │   ├── FlamingSphere.cs                ← From Core/GameManager_FlamingSphere.cs
│   │   ├── Grease.cs                       ← From Core/GameManager_Grease.cs
│   │   ├── HolyAreas.cs                    ← From Core/GameManager_HolyAreas.cs
│   │   ├── MirrorImage.cs                  ← From Core/GameManager_MirrorImage.cs
│   │   ├── WallOfFire.cs                   ← From Core/GameManager_WallOfFire.cs
│   │   ├── WallOfForce.cs                  ← From Core/GameManager_WallOfForce.cs
│   │   ├── WallOfIce.cs                    ← From Core/GameManager_WallOfIce.cs
│   │   └── WishExecutor.cs                 ← From Magic/Spells/
│   ├── BonusType.cs
│   └── Tests/                              ← Co-located spell tests
│       └── (from Tests/Magic/)
│
├── Equipment/                              ← Unified items, inventory, enchantments
│   ├── Items/                              ← Core item data
│   │   ├── ItemData.cs                     ← From Inventory/
│   │   ├── ItemDatabase.cs                 ← From Inventory/
│   │   ├── ItemID.cs                       ← From Identifiers/
│   │   ├── ItemIDs.cs                      ← From Identifiers/
│   │   ├── ItemBuilder.cs
│   │   ├── ItemSpellEffect.cs              ← From Inventory/
│   │   └── ItemMaterial.cs
│   ├── Enchantments/
│   │   ├── EnchantmentEffects.cs
│   │   ├── EnchantmentFactory.cs
│   │   ├── EnchantmentProperties.cs
│   │   ├── EnchantmentStats.cs
│   │   ├── EnchantmentType.cs
│   │   ├── ItemEnchantmentData.cs
│   │   └── ItemMaterialFactory.cs
│   ├── Materials/
│   │   └── MaterialProperties.cs
│   ├── Weapons/                            ← Specific weapon behaviors
│   │   ├── DwarvenThrowerBehavior.cs
│   │   ├── FrostBrandBehavior.cs
│   │   ├── HolyAvengerBehavior.cs
│   │   ├── JavelinOfLightningBehavior.cs
│   │   ├── LuckBladeBehavior.cs
│   │   ├── MaceOfSmitingBehavior.cs
│   │   ├── MaceOfTerrorBehavior.cs
│   │   ├── NineLivesStealerBehavior.cs
│   │   ├── OathbowBehavior.cs
│   │   ├── RapierOfPuncturingBehavior.cs
│   │   ├── ShatterspikeBehavior.cs
│   │   ├── SlayingArrowBehavior.cs
│   │   ├── SleepArrowBehavior.cs
│   │   ├── SunBladeBehavior.cs
│   │   ├── SwordOfLifeStealingBehavior.cs
│   │   ├── SwordOfSubtletyBehavior.cs
│   │   ├── SwordOfThePlanesBehavior.cs
│   │   └── SylvanScimitarBehavior.cs
│   ├── Armor/
│   │   ├── AbsorbingShieldBehavior.cs
│   │   ├── AnimatedShieldBehavior.cs
│   │   ├── ArmorOfRageBehavior.cs
│   │   ├── BandedMailOfLuckBehavior.cs
│   │   ├── BreastplateOfCommandBehavior.cs
│   │   ├── CastersShieldBehavior.cs
│   │   ├── DemonArmorBehavior.cs
│   │   ├── LionsShieldBehavior.cs
│   │   ├── MithralFullPlateOfSpeedBehavior.cs
│   │   └── WingedShieldBehavior.cs
│   ├── Rings/
│   │   ├── RingAbility.cs
│   │   ├── RingActivationManager.cs
│   │   ├── RingChargeManager.cs
│   │   ├── RingDatabase.cs
│   │   ├── RingFactory.cs
│   │   ├── RingUseTracker.cs
│   │   └── RingNames.cs                    ← From Identifiers/
│   ├── Rods/
│   │   ├── RodData.cs
│   │   ├── RodDatabase.cs
│   │   ├── RodFactory.cs
│   │   ├── RodNames.cs                     ← From Identifiers/
│   │   └── MetamagicRodActivation.cs
│   ├── Staves/
│   │   ├── StaffDatabase.cs
│   │   ├── StaffDefinition.cs
│   │   └── StaffValidator.cs
│   ├── Wondrous/
│   │   ├── WondrousItemActivation.cs
│   │   ├── WondrousItemDatabase.cs
│   │   ├── WondrousItemFactory.cs
│   │   └── WondrousItemNames.cs            ← From Identifiers/
│   ├── SpecificItems/
│   │   ├── SpecificItemBehavior.cs
│   │   ├── SpecificItemDatabase.cs
│   │   ├── SpecificMagicItem.cs
│   │   └── MagicItemLootGenerator.cs
│   ├── SpellStorage/
│   │   ├── CounterspellManager.cs
│   │   ├── SpellStorageManager.cs
│   │   └── StoredSpell.cs
│   ├── Effects/
│   │   └── RegenerationEffect.cs
│   ├── Inventory/                          ← From top-level Inventory/
│   │   ├── Inventory.cs
│   │   ├── InventoryComponent.cs
│   │   ├── CharacterInventory.cs           ← From Character/
│   │   ├── PartyStash.cs
│   │   ├── PotionFactory.cs
│   │   ├── ScrollFactory.cs
│   │   ├── ScrollValidator.cs
│   │   ├── WandFactory.cs
│   │   ├── WandValidator.cs
│   │   └── RopeItemData.cs
│   ├── Store/                              ← From top-level Store/
│   │   ├── StoreInventory.cs
│   │   └── StoreUI.cs
│   └── Tests/
│       └── (from Tests/Equipment/ + Tests/Inventory/)
│
├── AI/                                     ← AI systems (unchanged, already well-organized)
│   ├── AIBehaviorData.cs
│   ├── AIConsumableManager.cs
│   ├── AIProfile.cs
│   ├── AISpellcastingStrategist.cs
│   ├── LastKnownPositionTracker.cs
│   ├── NPCTemplateAIConfigurator.cs
│   ├── SpellCategoryClassifier.cs
│   ├── SpellcasterAIBehaviorData.cs
│   ├── Profiles/                           ← (14 profile files, unchanged)
│   ├── Custom/
│   │   └── CustomAIExample.cs
│   └── Tests/
│       └── (from Tests/AI/)
│
├── Encounters/                             ← Encounter system (mostly unchanged)
│   ├── DiceExpression.cs
│   ├── DungeonEncounterExamples.cs
│   ├── DungeonEncounterSpawner.cs
│   ├── DungeonEncounterTable.cs
│   ├── DungeonEncounterTableData.cs
│   ├── DungeonEncounterTableEntry.cs
│   ├── DungeonEncounterTableExamples.cs
│   ├── DungeonEncounterTableManager.cs
│   ├── EncounterCSVParser.cs
│   ├── EncounterDefinition.cs
│   ├── EncounterDescriptionParser.cs
│   ├── DungeonEncounters.cs                ← From Core/GameManager.DungeonEncounters.cs
│   ├── RandomEncounterSystem.cs            ← From Character/
│   └── Tests/
│       └── (from Tests/Encounters/)
│
├── Crafting/                               ← Unchanged, already well-organized
│   ├── (10 files)
│   └── Tests/
│       └── (from Tests/Crafting/)
│
├── TreasureGenerator/                      ← Unchanged
│   ├── MagicItemGenerator.cs
│   ├── TreasureData.cs
│   ├── TreasureDice.cs
│   ├── TreasureGenerator.cs
│   ├── TreasureResult.cs
│   └── TreasureUI.cs
│
├── Effects/                                ← Poisons & Diseases (renamed for clarity)
│   ├── Disease.cs
│   ├── DiseaseDatabase.cs
│   ├── Poison.cs
│   ├── PoisonDatabase.cs
│   └── PoisonSpecialEffect.cs
│
├── Grid/                                   ← Grid system (unchanged)
│   ├── PathPreview.cs
│   ├── SquareCell.cs
│   ├── SquareGrid.cs
│   └── SquareGridUtils.cs
│
├── Services/                               ← Cross-cutting services (slimmed)
│   ├── AIService.cs
│   ├── CombatFlowService.cs
│   ├── ConcentrationService.cs
│   ├── ConditionService.cs
│   ├── DiceService.cs
│   ├── DispelMagicService.cs
│   ├── EconomyService.cs
│   ├── EncounterService.cs
│   ├── InputService.cs
│   ├── MovementService.cs
│   ├── SavingThrowResolver.cs
│   ├── SpellApplicationService.cs
│   ├── SpellResolutionService.cs
│   ├── SpellTargetingService.cs
│   ├── SummoningService.cs
│   └── TurnService.cs
│
├── UI/                                     ← UI layer (semantically grouped)
│   ├── Common/                             ← Shared UI utilities
│   │   ├── DraggableWindow.cs
│   │   ├── ResizableWindow.cs
│   │   ├── ScrollbarHelper.cs
│   │   ├── HoverMarker.cs
│   │   ├── IconLoader.cs
│   │   ├── IconManager.cs
│   │   ├── UIFactory.cs
│   │   └── UITheme.cs
│   ├── Combat/                             ← Combat HUD panels
│   │   ├── CombatUI.cs
│   │   ├── ActionButtonPanel.cs
│   │   ├── InitiativePanel.cs
│   │   ├── TargetSelectionPanel.cs
│   │   ├── CombatLogPanel.cs
│   │   ├── CombatEndXPUI.cs
│   │   ├── StatusEffectIndicator.cs
│   │   ├── StatusEffectTooltipUI.cs
│   │   ├── QuickItemUsePanel.cs
│   │   ├── StaffSpellSelectionPanel.cs
│   │   ├── TurnUndeadTargetSelectionPanel.cs
│   │   └── SummonedCreatureVisual.cs
│   ├── CharacterCreation/                  ← (unchanged)
│   │   ├── CharacterCreationManager.cs
│   │   ├── CharacterCreationData.cs
│   │   ├── CharacterCreationUI.cs
│   │   ├── DomainSelectionUI.cs
│   │   ├── FamiliarSelectionUI.cs
│   │   └── WizardSpecializationUI.cs
│   ├── CharacterSheet/
│   │   ├── CharacterSheetUI.cs
│   │   ├── CharacterHoverTooltipUI.cs
│   │   ├── CharacterInfoPanel.cs
│   │   ├── SkillsUIPanel.cs
│   │   └── LevelUpUI.cs
│   ├── Spells/
│   │   ├── SpellPreparationUI.cs
│   │   ├── SpellSelectionUI.cs
│   │   ├── SpellStorageUI.cs
│   │   ├── SpellTestingPanel.cs
│   │   ├── DisguiseSelfRaceSelector.cs
│   │   └── FeatSelectionUI.cs
│   ├── Inventory/
│   │   ├── InventoryUI.cs                  ← From Inventory/
│   │   ├── PreCombatInventoryUI.cs
│   │   └── LootCollectionUI.cs
│   ├── Encounter/
│   │   ├── EncounterSelectionUI.cs
│   │   ├── EncounterPreviewPanel.cs
│   │   ├── DungeonEncounterGeneratorUI.cs
│   │   ├── RandomEncounterGeneratorUI.cs
│   │   └── PreCombatHubUI.cs
│   ├── Crafting/
│   │   └── CraftingWorkshopUI.cs
│   └── Wish/
│       └── WishUI.cs
│
├── Utilities/                              ← Shared utilities & identifiers
│   ├── DebugCommands.cs                    ← From Core/
│   ├── SpriteLoader.cs                     ← From Core/
│   ├── CameraController.cs                 ← From Core/
│   ├── SummonCommand.cs                    ← From Core/
│   ├── AbilityScore.cs                     ← From Identifiers/
│   ├── DamageType.cs                       ← From Identifiers/
│   ├── SavingThrow.cs                      ← From Identifiers/
│   ├── IdentifierExtensions.cs             ← From Identifiers/
│   ├── (existing Utilities/ files)
│   ├── EquipmentAssigner.cs                ← From Systems/
│   └── QuickSpawnSystem.cs                 ← From Systems/
│
├── World/                                  ← World systems
│   ├── CreatureTrapSystem.cs
│   └── PlanarTravelSystem.cs
│
└── Tests/                                  ← Integration / cross-cutting tests
    ├── Maneuvers/                          ← From Tests/Maneuvers/
    ├── Utilities/                          ← From Tests/Utilities/
    └── Feats/                              ← From Tests/Feats/
```

---

## 4. Rationale & Design Principles

### 4.1 Domain-Driven Grouping
Files are grouped by **game domain** (Combat, Spell, Equipment, Character) rather than by technical layer. This matches how developers think about the D&D ruleset: "I need to fix a spell" → go to `Spell/`.

### 4.2 Co-located Tests
Each domain folder has a `Tests/` subfolder. This keeps test files physically close to the code they test, reducing context switching.

### 4.3 Slim Core
`_Core/` (underscore prefix for sort-to-top) contains only the bootstrap and global orchestrator. The GameManager shrinks from 45 partials to a thin delegation layer as spell resolution logic moves to `Spell/Resolution/`.

### 4.4 Consolidated Spell System
All 110+ spell-related files converge into `Spell/`. The alphabetical `GameManager_Spells_X.cs` files become `Spell/Resolution/SpellResolver_X.cs` — same content, proper home.

### 4.5 Unified Equipment
`Inventory/`, `Equipment/`, `Equipment/Behaviors/`, and `Store/` merge into one `Equipment/` tree with semantic subdivisions (Weapons, Armor, Rings, Rods, etc.).

### 4.6 Character Hierarchy
65 `Character/` files get meaningful sub-folders: Stats, Classes, Feats, Races, Creatures (NPC databases), Templates, Progression.

### 4.7 UI Semantic Grouping
Flat `UI/` and `UI/Panels/` reorganize into `UI/Combat/`, `UI/CharacterSheet/`, `UI/Spells/`, `UI/Inventory/`, etc.

---

## 5. Folders Eliminated

| Old Folder | Reason | New Location |
|------------|--------|--------------|
| `CombatSystems/` | Merged into Combat | `Combat/Maneuvers/`, `Combat/Reactions/`, `Combat/Special/` |
| `Mounts/` | Combat subdomain | `Combat/Mounts/` |
| `Inventory/` (top-level) | Merged into Equipment | `Equipment/Inventory/`, `Equipment/Items/` |
| `Store/` | Merged into Equipment | `Equipment/Store/` |
| `Systems/` | Only 2 files, vague name | `Utilities/` |
| `Identifiers/` | Distributed to owning domains | `Spell/Data/`, `Equipment/Items/`, `Equipment/Rings/`, `_Core/`, `Utilities/` |
| `Data/NPCTemplates/` | Merged with Character templates | `Character/Templates/` |
| `Classes/` (top-level) | Merged into Character | `Character/Classes/` |
| `Magic/` | Renamed & restructured | `Spell/` |
| `Core/` | Slim down; most files relocated | `_Core/` (lean) |
| `UI/Panels/` | Distributed to semantic groups | `UI/Combat/`, `UI/CharacterSheet/`, etc. |

---

## 6. Migration Plan

### Phase 5B Execution Order

Migration should proceed in dependency order, with one commit per subsystem:

| Step | Subsystem | Files Moved | Risk | Priority |
|------|-----------|-------------|------|----------|
| 1 | **Combat unification** | ~50 files | Medium — many cross-references | High |
| 2 | **Spell consolidation** | ~110 files | High — GameManager partials | High |
| 3 | **Equipment merge** | ~75 files | Medium | High |
| 4 | **Character reorganization** | ~65 files | Low — mostly internal | Medium |
| 5 | **UI semantic grouping** | ~42 files | Low — UI is leaf layer | Medium |
| 6 | **Cleanup** (Identifiers, Systems, Core slim) | ~20 files | Low | Low |
| 7 | **Test co-location** | ~100 files | Low — tests only | Low |

### Migration Strategy

1. **One subsystem per commit** — easy bisect if something breaks
2. **Update namespace declarations** after each move (if applicable)
3. **Run tests after each step** — ensure nothing breaks
4. **Keep `.meta` files** (Unity asset references) — move them alongside `.cs` files
5. **Update any hardcoded paths** in test fixtures or asset references

---

## 7. Benefits

| Benefit | Before | After |
|---------|--------|-------|
| **Find spell logic** | Search 5+ folders | Go to `Spell/` |
| **Find combat logic** | Search 4+ folders | Go to `Combat/` |
| **Find item/equipment** | Search 3 folders | Go to `Equipment/` |
| **GameManager complexity** | 45 partials, 44K lines | Thin orchestrator + domain services |
| **Test proximity** | Root `Tests/` folder | Co-located `Tests/` in each domain |
| **Onboarding time** | High — must learn fragmented layout | Low — intuitive domain folders |

---

## 8. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Broken references** after moves | Build errors | Unity handles `.meta` GUIDs; careful `.cs` namespace updates |
| **Merge conflicts** with active branches | Dev friction | Coordinate with team; do in a single feature branch |
| **GameManager partial class moves** | Complex refactor | Phase 2 focuses specifically on this; keep `partial class GameManager` declarations intact initially |
| **Test breakage** | CI fails | Run full test suite after each step |
| **Lost git blame history** | Hard to trace changes | Use `git mv` for all moves to preserve history |

---

## 9. Implementation Readiness

- ✅ Full directory analysis complete
- ✅ All 644 files cataloged
- ✅ Problem areas identified with file counts
- ✅ New structure designed with rationale
- ✅ Migration plan with phased approach
- ✅ File mapping CSV generated (see `FOLDER_STRUCTURE_MAPPING.csv`)
- ⏳ Ready for Phase 5B execution

---

*Document generated as part of Phase 5A — Folder Structure Analysis & Reorganization Plan*
