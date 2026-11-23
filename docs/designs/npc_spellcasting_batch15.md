# Batch 15: NPC spellcasting depth

> Status (2026-11-16): plan, nothing built. Queue `_work/queue_b15_workflow.js` generated, not launched; the owner launches it. It runs after batch 14 (weighted personalities) and the grouped AI turns. Read-only design pass on master (code unchanged since 5176586): seven mappers (code and books), two competing designs, a judge that checked the key claims in the code and the books, and two adversarial checks. Nothing here was compiled, run or verified in Play mode. Anchor on member names: batches 14 and the grouped turns move lines. Paths are under `Assets/Scripts/` unless they start with `docs/`.

Line numbers, written "l.N", are anchors from 78ff67e only; they drift. Abbreviations: SCC = `Spell/Components/SpellcastingComponent.cs`, DES = `Encounters/DungeonEncounterSpawner.cs`, ASS = `AI/AISpellcastingStrategist.cs`, AIS = `Services/AIService.cs`, NPS = `_Core/GameManager.NPCSetup.cs`. Books are cited by printed page and never quoted.

## Summary

Batch 15 makes the spellcasters of the levels 1-8 DMG encounters cast the way RAW says they do, through the same rules a PC uses. It takes the work that `docs/designs/action_catalog_batch13.md` ("Not in scope: Batch 15"), `docs/designs/personalities_batch14.md` ("Not in scope") and section 7 of `docs/designs/enemy_ai_knowledge_and_personalities.md` hand on.

Four results:

1. **Real casters.** Every DMG class-levelled caster (kobold sorcerer 5, lizardfolk druid 5, troglodyte cleric 5) and every in-scope MM caster (drider, dark naga, the dragons, the adept) spawns with RAW slots and a RAW list.
2. **One rule each.** There is one slot table, one bonus-spell rule, one per-class spell level and one preparation matcher, for PCs and NPCs alike.
3. **One value scale.** The AI weighs spells, potions, scrolls and wands in expected hit points, the same unit as batch 14's `AttackValue`. It also directs a Flaming Sphere and compares a dragon's spells with its breath and full attack.
4. **The rules gaps these casters hit are fixed:** swarms, undead targets, wards against control, Sleep, the summon spell flags and Insect Plague.

There are 20 steps: 18 build steps and two full regressions (steps 11 and 20).
- **Steps 1-10 build the foundation:** the caster soak, the profile range fix, the tables, the per-class spell levels, the spell data, the preparation matcher, multiclass casting, then the DMG, MM and adept lists.
- **Step 11** is the midpoint regression. It is never cut.
- **Steps 12-19 make the AI use what it has:** the spell scale, swarms and undead, the sphere and the dragon, potions, item casts, wards, Insect Plague and Alter Self.

**Why this shape.** This plan is the judged synthesis of design A (foundation-first) and design B (encounter-value-first). A's order is kept: the casters get correct tables and lists before the AI is retuned, so each soak move has one cause. B's single value scale is kept: spells, items and attacks are compared in one unit, which batch 22's evaluator reuses.

**Launch prerequisites.**
1. Batch 14 and the grouped AI turns are built and their final regressions recorded in `docs/TESTING.md` 3.2.
2. A clean master, Unity 6000.4.0f1 open on this project, `Library/Bee` present, and the Unity MCP attached here.
3. **Hash baseline:** the grouped-turns final regression's `rules/*`, `smoke/*` and soak runs. Step 1 names their run ids in its TESTING 3.2 row.
4. Weekly usage below 62% at the start of each step (the queue's gate).

**Anchors.** Every batch 14 and grouped-turn member named here was unbuilt when this plan was written: `AiBlend` with `PreferredRange`, `AoOCost`, `KeepAwayDistance` and `RiskToleranceFactor`; `ChooseAfterStandardAction`; `ChooseHealerApproach`; `AiLegal.CanMoveNow`; `AiDecisionTimer`; `AiDecisionTrace`; `PersonalityValidator`; `AI/Valuation/AttackValue`; the `goblin_adept` fixture; the Destruction smite; `CombatPacing`. Built since (2026-11-23): `AiDecisionTimer` and `AiDecisionTrace` (batch 14 step 1, `AI/Debug/`) and `AttackValue` (step 2, `AI/Valuation/AttackValue.cs`: `HitChance`, `AverageWeaponDamage`, `AverageMeleeDamage`, `AverageNaturalDamage`, `ExpectedRoundDamage`); `PersonalityValidator` and `AiBlend` with `AoOCost` and `RiskToleranceFactor` (step 3, `AI/Personality/`); `AiBlend.PreferredRange`, `KeepAwayDistance`, `PrefersCharge`, `OrderOutOfReach`, `IsRangedOrCastingRole` and the creature overloads of `AoOCost` and `RiskToleranceFactor`, with `PersonalityProfile.RangeAdjust` (step 6, `AI/Personality/AiBlend.cs`). Each step greps its anchors before it starts, adapts to the names as built, and records any rename in this doc.

### Findings (verified 2026-11-16 at 78ff67e)

- **F1. DMG casters cast nothing (ENC-021).** `DungeonEncounterSpawner.UpdateAIForClass` (DES l.227) gives class-levelled spawns a caster profile. `CreatureClassEngine.ApplyEncounterClassLevel` (called at DES l.171) fills no lists, so `DefinitionGivesSpells` is false and no `SpellcastingComponent` is added.
- **F2. The baseline soak cannot see F1.** `soak/dmg-random-encounters` uses dungeon levels 1-3 (`SoakScenarios.DungeonLevelForSeed`, l.58); the Quick Start party is 3rd level. The DMG caster rows are on levels 5, 6 and 8 (`dungeon_encounters.csv` rows 5/89-90, 6/89-90, 8/89-90). The drider, the young copper dragon and the dark naga are on levels 7, 7 and 8.
- **F3. The drider and the dark naga are Warriors with a Spellcaster archetype.** Their casting exists only as `SpecialAbilities` text (`NPCDatabase_D` l.627, l.321). They hang back at six squares (AI-038) and cast nothing.
- **F4. Slot tables stop short (SPL-041).**
  - The wizard stops at 4/3/2 from level 4 (`GetWizardSlotsForLevel`, SCC l.954).
  - The druid stops at 2/1 for 1st and 2nd level from level 3, with no 3rd-level slot (`GetDruidSlotsForLevel`, SCC l.1129).
  - Bonus spells are capped at +1 per level (`baseX > 0 && mod >= n ? 1 : 0`), against PHB Table 1-1 p.8.
  - The cleric tables are correct to 9th level (SCC l.1065-1127).
- **F5. Spontaneous casters can use the wrong key ability.** `InitSpontaneousCaster` (SCC l.530) uses `CharacterStats.GetPrimaryCastingModifier` (l.1038-1049). That returns WIS for any `IsCleric` character and the highest mental modifier for an unlisted class (CHR-117).
- **F6. Bard "0" entries get no bonus spell.** `SpontaneousCastingData` grants a bonus spell only where the base is above 0 (l.88). In PHB Table 3-4 p.27 a "0" means bonus spells only (CHR-181).
- **F7. Paladins get spells too early.** `PartialCasterData.SpellsPerDay` (l.37-60) runs ahead of the paladin table (PHB p.43). The ranger (PHB p.46) is re-checked at step 3 (both in CHR-129).
- **F8. No minimum key score.** Nothing enforces a key score of 10 + the spell level to prepare or cast that level (CHR-116).
- **F9. Prepared ids are matched to slots by index (SPL-015).** `ApplyPreparedSpellSlotIds` (SCC l.1295) works by index. An id at a domain-slot index that is not a domain spell is refused; the Quick Start cleric loses Shield of Faith and Flame Strike this way (`ClericClass` l.84-123). Explicit lists are used only for a single prepared class (`Init`, SCC l.470).
- **F10. A lower spell cannot fill a higher slot.** `IsValidSpellForSlot` (SCC l.1486) requires an exact level match. PHB p.178 allows a lower spell in a higher slot.
- **F11. Spell levels ignore the casting class (SPL-008, SPL-099, SPL-100, SPL-105).** About 250 reads use `SpellData.SpellLevel` instead of `GetSpellLevelFor(class)`. The code gives Flame Strike and Acid Fog spell level 2. The Quick Start wizard's book (`WizardClass` l.140) holds Acid Fog.
- **F12. The adept (CHR-024).**
  - The adept is in neither caster set (`CharacterStats` l.450-458).
  - `AdeptSpellList` lacks 7 of the DMG p.108 1st-level spells: cause fear, command, comprehend languages, detect chaos, detect evil, detect good and detect law (CHR-110).
  - `AdeptClass` (l.121) grants the familiar at 1st level; DMG p.108 gives it at 2nd (CHR-054).
- **F13. Dragons (CRE-016).** `DragonData.GetSpellsForCasterLevel` (l.1337) takes the first 6, 9, 13 or 16 entries of a flat list, and its comment table disagrees with PHB Table 3-17 p.54. The young copper dragon is caster level 1 (MM p.83-84: Copper Dragon Abilities by Age on p.83, the young copper sample's spells on p.84).
- **F14. The vampire (CRE-030).** The vampire is a Fighter with spell lists (`NPCDatabase_V` l.298, l.314). The MM p.250 sample is a 5th-level fighter with no spells.
- **F15. AI scoring.**
  - School priority is multiplied by 10 (AI-022), and buffs get a doubled setup bonus (AI-011).
  - `AISpellcastingStrategist.GetResourceManagementScore` (ASS l.789-824) counts distinct castable spells, not slots, and reads `SpellLevel` and `Stats.Level`. A caster with two copies of its only 2nd-level spell gets the last-slot penalty on its first cast (AI-082).
  - Spells with no combat effect are still cast (AI-069).
- **F16. Missing AI uses and rules gaps.**
  - The AI never moves a Flaming Sphere: `GameManager.TryControlFlamingSphereForAI` has no caller and is a kept hook in AI-029.
  - The AI never chooses consumables (AI-009). Item casts aim at one creature only (`RunItemCastForAI`), and `SpellCastRequest.ForAIArea` has no `CastSource`.
  - Swarms are targeted by single-target spells (AI-059), and swarm damage is a flat 6 (CRE-007).
  - Undead allies and enemies are ignored by cure and inflict choices (AI-062).
  - Dragons compare no spell with breath or full attack (AI-037).

## Rules decisions (explicit RAW; checked in the PHB, DMG and MM on 2026-11-16)

Pages are printed pages. Rules are stated in our own words. Each reading is recorded in `docs/systems/RULES_COVERAGE.md` 4.1 by the step that applies it.

- **R1. Spells per day and spells known** come from the class tables:
  - wizard PHB Table 3-18 p.55;
  - cleric Table 3-6 p.31;
  - druid Table 3-8 p.35;
  - sorcerer Tables 3-16 p.52 (per day) and 3-17 p.54 (known);
  - bard p.27-28;
  - paladin p.43 and ranger p.46;
  - adept DMG Table 4-2 p.107.

  A dash entry means the class cannot cast that level. A "0" means bonus spells only.
- **R2. Bonus spells** follow PHB Table 1-1 p.8 for the class's key ability, at every level the class can cast. A caster needs a key score of at least 10 + the spell level to prepare or cast a spell of that level (PHB p.8 and each caster class entry).
- **R3. Key ability by class:**
  - wizard: INT;
  - cleric, druid, paladin, ranger and adept: WIS (adept DMG p.108);
  - sorcerer and bard: CHA.

  In a multiclass character, each class uses its own table, key ability, list and caster level (PHB p.59).
- **R4. Higher slots.** A prepared caster may put a lower-level spell in a higher slot (PHB p.178). Metamagic slots use the same rule.
- **R5. Spell levels** are the spell's level on the casting class's list, read through `GetSpellLevelFor(class)`. This applies to slots, preparation, permission to cast and the AI's slot accounting.
- **R6. Clerics (PHB p.32).**
  - One domain slot per spell level from 1st, holding a spell of either domain.
  - A cleric may take an alignment domain only if its alignment matches that domain, and may not cast a spell whose alignment descriptor opposes its own alignment or its deity's.
  - Good clerics convert prepared non-domain spells into cure spells; evil clerics into inflict spells; neutral clerics choose one at creation.
  - Domain slots never convert.
- **R7. Druids** may convert a prepared non-orison spell into a Summon Nature's Ally of the same level (PHB p.35).
- **R8. The adept** is a WIS prepared divine caster with the DMG p.108 list and the DMG p.107 table. It gains a familiar at 2nd level, not 1st (DMG p.108).
- **R9. MM casters.**
  - Dragons cast as sorcerers of their age's caster level (MM p.69), with the PHB Table 3-17 spells-known counts.
  - The drider casts as a 6th-level sorcerer (MM p.89-90; the MM's typical one).
  - The dark naga casts as a 7th-level sorcerer (MM p.191).
  - Troglodyte clerics take two of Chaos, Death, Destruction and Evil (MM p.247).
  - The sample vampire casts no spells (MM p.250).
- **R10. Swarms** are immune to spells that target a specific number of creatures. The exception is mind-affecting effects on a swarm that has an Intelligence score and a hive mind (the hellwasp swarm). Area spells and effects affect swarms and deal them half again as much damage (MM p.316).
- **R11. Cures and inflicts on undead.** A cure harms an undead creature and an inflict spell heals one. This applies to potions too (the cure spell entries, PHB p.215; inflict, PHB p.244).
- **R12. Flaming Sphere.** Directing the sphere is a move action of the caster (PHB p.232).
- **R13. Items.**
  - Drinking a potion is a standard action that provokes. Administering one to an unconscious creature is a full-round action that provokes (DMG p.229).
  - A scroll needs the spell on the user's class list, and a caster level check if the scroll's caster level is higher than the user's (DMG p.238).
  - A wand needs the spell on the user's class list (spell trigger activation, DMG p.213; wands, DMG p.245).

  Batch 13's use-item executor already prices these actions.
- **R14. Sleep** affects 4 HD, lowest first; ties go by distance from the point of origin (PHB p.280).
- **R15. Protection from Evil** blocks possession and control effects that grant ongoing control. It suppresses such an effect without ending it. If the ward ends before the control effect does, the controller can command the creature again (PHB p.266, stated explicitly). The mirrored spells are re-read at step 17.
- **R16. Insect Plague** (PHB p.244-245) and **Alter Self** (PHB p.197; owner ruling of 2026-10-11, SPL-037) follow their entries as SPL-136 and SPL-135 state them.

### Delegated RAW readings

Each one is recorded in `docs/systems/RULES_COVERAGE.md` 4.1, dated, at the step that applies it, and labelled "delegated RAW reading" in code. They are reported so the owner can overrule them; they are not owner questions.

- **DR1. Which spells a DMG NPC caster has.** The books say the DM picks them. The DMG's own NPC samples are the strongest text. In order:
  - (a) the DMG Chapter 4 sample of the same class and level, used verbatim when the race matches;
  - (b) the same sample for another race, adjusted only where RAW forces a change (DR3, DR4); racial ability changes alter bonus spells and DCs, not the list;
  - (c) an MM monster's printed typical list (drider, dark naga, a dragon's sample stat block);
  - (d) otherwise the class preference order (DC1).

  Applied at steps 8-10.
- **DR2. Troglodyte cleric domains.** The MM gives the set of four and leaves the pick open. The game picks Evil + Destruction for every troglodyte cleric (DC6 gives the reasons). Step 8.
- **DR3. Barred alignment spells.** A sample spell that an evil or chaotic NPC cannot cast under PHB p.32 is replaced by its opposite-alignment counterpart of the same level (protection from evil becomes protection from good; magic circle the same way). With no counterpart, it is replaced by the next DC1 spell of that level. Step 8.
- **DR4. Missing spells.** A sample spell that `SpellDatabase` lacks is replaced by the next DC1 spell of the same level that the NPC does not already have. The swap is logged and listed in the loadout manifest, and the missing spell is filed. A spell flagged as a placeholder stays prepared, because the sample is verbatim; the AI skips it (step 12). Steps 8-10; Q4.
- **DR5. Domain slots and lower spells.** A domain slot may hold a lower-level spell of its domains: PHB p.178 is general, and PHB p.32 restricts only the list. Step 6.
- **DR6 (withdrawn 2026-11-16).** A suppressed control effect resuming when the ward ends is explicit RAW (R15, PHB p.266), not a delegated reading. Step 17 records it as an R-reading.

### Deferred

- **Ready and readied counterspells** (PHB p.160, p.170-171; CMB-029, SPL-047) are deferred (Q1). `AIService.TryAIReadyCounterspell` stays a kept hook.
- **Casting before combat** (buffs while the party approaches, DMG p.22) is not modelled before batch 17 adds awareness (fair sight). It is listed under Not in scope.

## Design choices (game direction the owner may overrule)

- **DC1. Class preference orders.** Data in `NpcSpellLoadouts`: per class and spell level, an ordered list of spell ids. They are built from the spells the DMG samples of that class choose at any level, then by combat value (damage and control, then defense, then healing). They feed DR1(d), the DR4 substitutions and `FillByPreference`, including the PC Auto-prepare button. A manifest test pins them.
- **DC2. `goblin_adept` and the adept order.** The fixture's prepared list is the adept preference order, filled to the slots its actual Wisdom gives through `BonusSpellRules`. The 1st-level order is cure light wounds, bless, sleep, burning hands, cause fear, command; at 2nd level, cure moderate wounds and scorching ray come first.
- **DC3. The caster soak party** is the AI-run Quick Start party (3rd level), the same as the baseline soak's. The soak measures the casters' behaviour (casts, slots used, refused picks, first-cast round, casts with no effect, item uses), not who wins. Fallback: if more than a third of the jobs end before the casters' second turn, step 1 adds a party-level option if `ActorSource.QuickStart` can take one; otherwise it records the limit.
- **DC4. The AI potion trigger.** A creature drinks a healing potion when all of these hold:
  - it is at or below 40% of its maximum HP;
  - it has no castable self-heal;
  - it is not incorporeal;
  - the potion's expected healing, net of its `AoOCost`, is worth more than the best attack's `AttackValue` this turn.

  It administers a potion only to an adjacent unconscious ally, and only when it has no castable cure. The threshold is fixed at 40% (Q6).
- **DC5. Dominated creatures keep their side.** Team switching is out of batch and filed at step 17. A dominated creature only refuses to act against its controller.
- **DC6. Troglodyte clerics take Evil + Destruction** (DR2). Chaos + Evil and Chaos + Destruction are also complete in `DomainDatabase`; Death is not. Batch 14's Destruction smite gives the most combat value at 5th level.
- **DC7. Alter Self's form** is the form the caster names on `CastChoices`. The AI picks the form whose natural attacks have the highest `AttackValue` against its current target.
- **DC8. The lizardfolk druid's crocodile** spawns as a plain `crocodile` beside the druid. At druid level 5 a crocodile companion counts as effective level 2 (PHB p.36), which gives no stat changes. Link and share spells are not built; step 8 files that gap. Step 8 also checks the companion page and how the row parses.
- **DC9. Quick Start list fixes** (step 4).
  - The cleric's Flame Strike entry becomes Aid. Flame Strike is a 5th-level cleric spell; Aid is PHB p.196.
  - The wizard's Acid Fog (Sor/Wiz 6) is replaced in the book by the first 2nd-level spell of the DMG wizard 5 sample that the book lacks.

## Architecture

### New types (static or plain C# 9 classes; Unity generates the metas)

- **`Spell/Components/Slots/SpellSlotTables.cs`**
  - Spells per day for wizard, cleric, druid, sorcerer, bard, paladin, ranger and adept, levels 1-20. -1 encodes a dash entry, and 0 encodes "bonus spells only".
  - Spells known for sorcerer and bard.
  - Members: `PerDay(className, classLevel, spellLevel)` and `Known(className, classLevel, spellLevel)`.
- **`Spell/Components/Slots/BonusSpellRules.cs`**
  - `BonusSpells(keyScore, spellLevel)` follows PHB Table 1-1.
  - `CanUseLevel(keyScore, spellLevel)` is true when the score is at least 10 + the spell level.
  - `SlotsFor(className, classLevel, spellLevel, keyScore)` combines the table, the bonus spells and the dash and "0" rules.
- **`Character/Classes/CasterClassRegistry.cs`**
  - `CasterClassInfo`: `ClassName`, `Kind` (Prepared, Spontaneous or Partial), `KeyAbility`, `HasDomains`, `HasSpecialistSlots`, `Conversion` (None, CureInflict or SummonNatureAlly) and `CasterLevelFor(classLevel)`.
  - `CharacterStats`' class sets, `GetSpellcastingKind` and a new `GetCastingModifierFor(className)` delegate to it.
  - A test ties it to `ClassRegistry` and `ICharacterClass.IsSpellcaster`.
- **`Spell/Components/Preparation/PreparedSpellEntry.cs`:** `SpellId`, `ClassName`, an optional `SlotLevel` (for a metamagic or higher-slot pick) and `Metamagic` (`MetamagicData`, may be null).
- **`Spell/Components/Preparation/PreparationPlan.cs`**
  - `Apply(SpellcastingComponent, IList<PreparedSpellEntry>, FillRule)`.
  - Placement is a maximum bipartite matching (augmenting paths; at most about 40 entries) between entries and slots. A slot accepts an entry when the spell is on the slot's list (regular, domain or specialist) at a level no higher than the slot (R4, DR5), and R2 and R6 allow it.
  - Ties break in this order: domain-only entries, regular-only entries, flexible entries, lower spells in higher slots. Within each group, entries keep their order.
  - `FillRule.LeaveOpen` (PC creation and level-up) leaves unplaced slots empty (PHB p.177-178 lets a caster leave slots open). `FillRule.FillByPreference` (NPCs and the PC Auto-prepare button) fills the rest from DC1.
  - It returns a `PreparationReport` (placed, refused with a reason, filled).
- **`Encounters/Loadouts/NpcSpellLoadouts.cs`** (data):
  - the DMG sample lists for cleric 5, druid 5 and sorcerer 5, with their sample gear ids;
  - the MM lists for the drider, the dark naga and the printed dragon samples;
  - the DC1 orders;
  - the DR4 Missing list.
- **`Encounters/Loadouts/NpcSpellLoadoutBuilder.cs`**
  - `Apply(NPCDefinition spawnClone, string className, int classLevel, string raceId)` applies DR1 to DR4 and writes `KnownSpellIds` or `PreparedSpellEntries`, the domains and the conversion.
  - It is called from `DungeonEncounterSpawner.BuildSpawnDefinition` on the spawn clone only, never on a template.
- **`AI/Consumables/AiConsumableChooser.cs`** replaces `AIConsumableManager`. It returns one candidate (item, inventory index, variant, target, value) or none.
- **`Combat/Actions/Executors/DirectSpellEffectExecutor.cs`** with **`ActionIds.DirectSpellEffect`:** directing a controllable spell effect (Flaming Sphere first) as its RAW action. One core is used by the PC button and the AI.
- **`Spell/Special/AlterSelfRules.cs`:** the form's natural attacks, speeds and size from the chosen form (step 19).
- **Tests:**
  - static: `SpellSlotTableTests`, `BonusSpellRulesTests`, `CasterClassRegistryTests`, `PreparationPlanTests`, `NpcSpellLoadoutTests`, `AiSpellScaleTests`, `AiConsumableChooserTests`, `DirectSpellEffectTests`;
  - scenarios under `rules/` (per step) and `soak/dmg-casters`.

### Changed members

- **`SpellcastingComponent`:** `Get*SlotsForLevel`, `AppendSpellSlotsForClass`, `InitSpontaneousCaster`, `IsValidSpellForSlot`, `ApplyPreparedSpellSlotIds` (replaced by `PreparationPlan`), `CanCastSpell`, `CanCast` and `CollectCastablePreparedSpells` (slot counts).
- **Slot data:** `SpontaneousCastingData` and `PartialCasterData` become views over `SpellSlotTables`.
- **Spawn:** `GameManager.NPCSetup` (`DefinitionGivesSpells`, the innate casting path `InnateSpellcastingClass` and `InnateCasterLevel`, `ClericDomainMatchesAlignment`).
- **AI scoring:** `AISpellcastingStrategist` (`GetResourceManagementScore`, the score scale, `HasNoModelledCombatEffect`) and `SpellcasterAIProfile.ScoreSpell`.
- **`AIService.ExecuteNPCTurn`:** a new `TryNPCUseConsumableTurnForAI` after the summon branch.
- **The cast core:** a new `GameManager.CastStageSwarmImmunity` after `CastStageGlobe` (`GameManager.CastCore.cs`).
- **`ActionCatalog`** entries. Batch 13's `ActorActionSet` legal set gains every new AI action, so AI-080 gets no new disagreement kinds.
- **New `NPCDefinition` fields** (`PreparedSpellEntries`, `SpellLoadoutId`) go into `NPCDefinition.Clone` and every template's `CopyDefinitionFields` (CRE-023).
- **New per-creature state** (chooser memory, effect control links) is cleared in `ResetForNewCreature` and `GameManager.ResetCharacterSlotForSpawn` (CRE-046).

### Parity rule

Slots, bonus spells, minimum scores, spell levels, preparation, conversion, swarm immunity, undead cures and the direction of a spell effect are each one function, called by the PC UI and by the AI. The only actor-specific inputs are the UI's picks and the NPC loadout or AI choice. Every behaviour step has at least one `Ui`-actor scenario next to its `AI`-actor scenario, unless the step says no PC rule changes.

### Composition with batch 14 and the grouped turns

- **`AiBlend.PreferredRange`.** Step 2 changes only the legacy profile input the blend reads: a casting profile on a melee routine reports its reach. The blend's personality term is unchanged.
- **`ChooseAfterStandardAction`.** Step 14 adds a `DirectEffect` outcome, which uses the move action. `AiLegal.CanMoveNow` is then false for that turn.
- **`ChooseHealerApproach`.** Adepts (step 10) and evil clerics healing undead allies (step 13) use it unchanged.
- **`AttackValue` and `AoOCost`.** The potion trigger (DC4), item casts (step 16) and the dragon comparison (step 14) use them, so the AI has one value scale. Step 12 maps spell scores onto the same expected-HP units.
- **`AiDecisionTrace`.** Every new term (school priority, resource, item, direct effect) records its value and reason, so `ScenarioTraceDiff.FirstDivergence` can classify the seeds that move.
- **`AiDecisionTimer`.** Item candidates are capped at 4 per turn. The step 16 test asserts the timer budget holds on `soak/dmg-casters`.
- **`goblin_adept`.** Step 10 gives it lists. Batch 14's `rules/ai-healer-no-spell-no-approach` is rewritten so the adept's slots are spent through `Tweak`; the assertion is kept. Three step-6 scenarios also assume the adept has no castable spell: `rules/ai-adept-keeps-distance` (it shoots its sling in round 1), `rules/ai-cowardly-caster-defends-before-approach` (it reaches `AiOutOfReach.Run` only because `HasRangedMeans` finds no castable spell beyond touch) and `rules/ai-adept-never-charges` (it runs the kiter routine); each carries a precondition check that the adept casts no spell, so step 10 sees which one its lists break and spends the slots there the same way.
- **The Destruction smite** is available to the troglodyte clerics (DC6).
- **`PersonalityValidator`.** No new personality field.
- **Grouped turns (`CombatPacing`).** No change to pacing. Each new decision happens inside one creature's own turn: the sphere move, a drink, an administer (full-round). A grouped turn still runs each member's decision in order.

## Owner questions (the plan applies the default unless the owner answers before the named step)

Numbered within batch 15.

- **Q1. Ready actions and readied counterspells (CMB-029, SPL-047); before step 20.** Default: not in batch 15; propose them for a later batch (18 suggested). `AIService.TryAIReadyCounterspell` stays a kept hook.
- **Q2. CRE-016 (dragon spells by PHB Table 3-17) in step 9; before step 9.** This reverses batch 12's Q3 deferral of 2026-10-23. Default: yes, confirmed at plan approval.
- **Q3. Delete the vampire's spell lists (CRE-030); before step 9.** The MM sample is a fighter without spells, and the lists may have been deliberate. Default: delete.
- **Q4. Flame Blade and any other DMG-sample spell the game lacks; before step 8.** Default: substitute by DR4 and file the missing spells; building them is later work.
- **Q5. Alter Self in batch 15; before step 19.** Default: keep it as the last, droppable step.
- **Q6. The potion trigger and administering (DC4); before step 15.** Default: on as specified, fixed at 40%. The owner may ask for the threshold to follow batch 14's `RiskToleranceFactor` instead.

## Steps

Every C# step runs `bash tools/compile_check.sh` and expects exit=0, errors=0 and the warning baseline; a jump is a regression.

Each step keeps the docs true in the same commit:
- **Issues.** Delete fixed entries in `docs/issues/<PREFIX>.md`; update the counts and Top issues in `docs/KNOWN_ISSUES.md`. File new issues at the next free ID: another workflow may have taken IDs since this plan.
- **System docs.** Update `docs/systems/AI.md`, `docs/systems/AI_ACTION_COVERAGE.md`, `docs/systems/RULES_COVERAGE.md` and the spell, creature and encounter system docs where an entry lands.
- **Rules.** Record each R-reading, DR-reading and design choice in RULES_COVERAGE 4.1, dated at the step that applies it.
- **Recipes.** Update "Add a spell" and "Add a monster" in `docs/DEVELOPMENT_RECIPES.md` when the loadout or slot path changes (steps 3, 6, 8).

House rules for every step:
- New `.cs` files get their Unity-generated `.meta`; never hand-write one.
- Grep `Assets/Scripts/Tests` before renaming or deleting a member (TST-006).
- Write "not verified in Play mode" in commit bodies for anything only read.
- Never mutate a database template: loadouts write to the spawn clone only.

### Step classes and how they are proven

- **PURE:** a refactor with no behaviour change. Proven when the static suites match and every hash is equal on `rules/*`, `smoke/*` and both soaks.
- **NEUTRAL:** adds tests, scenarios, data or docs with no game path change. Proven by `soak/dmg-random-encounters` seeds 1-50 hash-equal and unchanged static verdicts.
- **Behaviour narrow:** changes named creatures, classes or spells only. Proven by targeted tests, and by the baseline soak being hash-equal or every moved seed's `FirstDivergence` landing on the named change.
- **Behaviour broad:** changes the shared AI or the party. Proven by targeted tests, a new soak baseline, and every moved seed of both soaks classified by `FirstDivergence` in the step's TESTING 3.2 row.

**TEST BUDGET rule for every step.** Run the new and touched scenarios and the touched subsystem's at seeds 1-3 in a fresh Play session (`QueueFresh`), plus the static suites covering the touched code. Unlike batch 14, each step also runs the soaks its own TEST BUDGET names, because the caster soak is the only place the DMG casters fight. Never run scenarios in a Play session that ran static suites.

### Step table

| # | Key | IDs | Class |
|---|---|---|---|
| 1 | `b15-caster-soak` | updates CRE-016, CRE-090 (the plan's filings were made with it on 2026-11-16) | NEUTRAL |
| 2 | `b15-caster-profile-range` | AI-038 | Behaviour broad |
| 3 | `b15-slot-tables` | SPL-041, CHR-116, CHR-117, CHR-129, CHR-181; CHR-159 (narrowed) | Behaviour narrow |
| 4 | `b15-class-spell-levels` | SPL-008 (narrowed), SPL-099, SPL-100, SPL-105 | Behaviour broad |
| 5 | `b15-spell-data` | SPL-021, SPL-266, SPL-267, SPL-178, SPL-196, SPL-035, SPL-044 (narrowed) | Behaviour broad |
| 6 | `b15-prepare-by-entry` | SPL-015, CHR-078 (narrowed) | Behaviour broad |
| 7 | `b15-multiclass-and-conversions` | SPL-011, SPL-159, SPL-108 | Behaviour narrow |
| 8 | `b15-dmg-caster-lists` | ENC-021, ENC-002 (narrowed); files the companion gap | Behaviour narrow |
| 9 | `b15-mm-innate-casters` | CRE-135 and CRE-090 (narrowed), CRE-016, CRE-030 | Behaviour narrow |
| 10 | `b15-adept` | CHR-024 (narrowed), CHR-110, CHR-054 (narrowed), TST-008 (reviewed) | Behaviour narrow |
| 11 | `regression-midpoint` | | Regression |
| 12 | `b15-ai-spell-scale` | AI-022, AI-082, AI-069 (narrowed or closed), AI-011 (narrowed) | Behaviour broad |
| 13 | `b15-swarm-undead-targets` | AI-059, AI-062, CRE-007 | Behaviour broad |
| 14 | `b15-sphere-and-dragon` | AI-029 (kept hook), AI-037 (narrowed) | Behaviour narrow |
| 15 | `b15-potions` | AI-009 (narrowed) | Behaviour broad |
| 16 | `b15-item-casts` | AI-009 (narrowed to summon and wall items) | Behaviour narrow |
| 17 | `b15-wards-and-control` | SPL-137, SPL-140; files the dominated-side issue | Behaviour broad |
| 18 | `b15-insect-plague-swarms` | SPL-136 | Behaviour narrow |
| 19 | `b15-alter-self` | SPL-135 | Behaviour narrow (cut line) |
| 20 | `regression-final` | | Regression |

**Cut line, in drop order:**
1. Step 19 (Alter Self).
2. The area-item half of step 16 (AI-009 then stays narrowed to area, summon and wall items).
3. Step 18.

The midpoint regression and steps 1-10 are never cut.

### 1. The caster soak (`b15-caster-soak`)

IDs: updates CRE-016 and CRE-090. NEUTRAL.

1. Add `soak/dmg-casters` to `SoakScenarios`. Seeds 1-49 cycle 7 groups, group = ((seed - 1) mod 7) + 1:
   1. the 5th-level kobold sorcerer (row 5/89-90);
   2. the 5th-level lizardfolk druid with crocodile (6/89-90);
   3. 1d3 5th-level troglodyte clerics (8/89-90);
   4. the drider (7/29-31);
   5. the young copper dragon (7/26-28);
   6. the dark naga (8/59-60);
   7. batch 14's `goblin_adept` with 2 goblin warriors (a fixture, not a DMG row).
2. The row is forced by seed and spawned through the same `DungeonEncounterSpawner` path as the baseline soak. The party follows DC3.
3. Append-only `SoakStats` columns: `npcCasts`, `npcCastsByLevel`, `npcSlotsLeftAtEnd`, `npcFirstCastRound`, `npcRefusedPicks`, `npcNoEffectCasts`, `npcItemUses`, `npcDirectEffects`. The existing summary hash leaves them out, so old hashes compare equal. `soak-rows.tsv` gains columns only at its end (`SoakStats.MergeRuns` reads by index).
4. Add `rules/npc-caster-spawn-has-spells`: every DMG caster row spawns with a `SpellcastingComponent` and at least one castable spell. It is marked `ExpectKnown("ENC-021")`.
5. **Doc-only commit, separate.** Add `DragonData`'s wrong comment table to CRE-016, and the drider's missing casting to CRE-090.

   The plan's other findings were filed with the plan on 2026-11-16, reconciled with the PHB and DMG table audit of the same day (which had filed most of them):
   - AI-082 (filed with the plan): the resource score counts spells, not slots (F15);
   - CHR-117 (widened to the bard): a spontaneous caster's bonus spells use the wrong key ability in a multiclass character (F5);
   - CHR-181: bard "0" entries give no bonus spell (F6);
   - CHR-129: the paladin and ranger table data (F7);
   - CHR-116: no minimum key score, for every casting class (F8; CHR-159 repeats the cleric part);
   - CHR-110: the 7 missing adept 1st-level spells, and CHR-054 (widened): `AdeptClass.HasFamiliar` grants the familiar at 1st level (F12).

Tests: new `SoakStatsColumnsTests` (the hash ignores the appended columns); the new rules scenario; the caster soak recorded as the batch's caster baseline.

TEST BUDGET: 1 static suite (about 10 asserts), 2 scenarios. Run `QueueFresh("soak/dmg-random-encounters", "1-50", "perSession=10")` (must be hash-equal with the launch baseline), `QueueFresh("soak/dmg-casters", "1-49", "perSession=10")` and `QueueFresh("rules/npc-caster-*", "1-3", "")`.

### 2. Casting profiles on a melee routine close in (`b15-caster-profile-range`)

IDs: AI-038. Behaviour, *broad*.

1. Add a shared helper `AiRangeRules.ProfilePreferredRange(actor, profile)`. A profile with a fixed preferred range (`SpellcasterAIProfile` and its subclass `VampireAIProfile`, both casting profiles; `DragonAIProfile`, a plain `AIProfile`) on a melee routine, or with no castable spell, reports its reach in squares instead of its fixed range (6 for `SpellcasterAIProfile`, 2 for the vampire and dragon profiles, AI-038).
2. The profiles' `PreferredRangeSquares` reads go through the helper (anchors l.20/l.38, l.32/l.42, l.67/l.78 of the three profiles).
3. Batch 14's `AiBlend.PreferredRange` reads the helper as its legacy input. Party members run by the AI use the same helper.

Tests:
- new static `AiRangeRulesTests`: a caster with spells keeps its range; Warrior-class casters without spells, the ogre mage (Spellcaster archetype, melee routine, spell-like abilities only, so no spells in this batch), the vampire and dragons report reach; the drider is not used (a RangedKiter that gains spells at step 9);
- `rules/caster-profile-melee-closes` (AI): the ogre mage closes to melee by round 2 (the drider is not used: it is a RangedKiter and gains spells at step 9);
- no Ui twin: no PC rule changes.

TEST BUDGET: 1 suite (about 15 asserts), 1 scenario. Run the touched rules at seeds 1-3, and both soaks with every moved seed classified.

### 3. Slot tables, bonus spells and the caster registry (`b15-slot-tables`)

IDs: SPL-041, CHR-116, CHR-117, CHR-129, CHR-181; CHR-159 (narrowed). Behaviour, narrow.

1. Add `SpellSlotTables`, `BonusSpellRules` and `CasterClassRegistry`.
2. `Get*SlotsForLevel`, `AppendSpellSlotsForClass`, `InitSpontaneousCaster`, `SpontaneousCastingData` and `PartialCasterData` read from them.
3. Each class uses its own key ability (R3; CHR-117).
4. Bard "0" entries give bonus spells (CHR-181), with the -1/0 encoding.
5. Paladin and ranger data are corrected (CHR-129).
6. `CanCastSpell` and `CanCast` enforce the minimum score (CHR-116, and its cleric part in CHR-159).
7. `CharacterStats`' class sets and `GetSpellcastingKind` delegate to the registry.
8. Both `AddWizardSlot` test helpers move onto `Harness_WidenClassSlotTable` and the new tables: `CastPipelineScenarios` and `PostAttackScenarios`, which adds a 4th-level Fire Shield slot.
9. Closes SPL-041, CHR-117, CHR-129 and CHR-181. Closes CHR-116, or narrows it to what is left (such as learning a spell above the score); narrows CHR-159 to what is left.

Tests:
- `SpellSlotTableTests`: golden rows for every class at levels 1-8 and 20 against the PHB tables; known counts for sorcerer and bard;
- `BonusSpellRulesTests`: Table 1-1 at scores 10-25, including the dash and "0" rules;
- `CasterClassRegistryTests`: the registry agrees with `ICharacterClass.IsSpellcaster`;
- `rules/slots-druid5-has-third` and `rules/slots-min-score-refuses` (Ui and AI twins).

TEST BUDGET: 3 suites (about 120 asserts), 4 scenarios. Run `rules/*slot*` and `rules/*cast*` at seeds 1-3, and both soaks. The baseline soak is expected hash-equal: the Quick Start wizard and cleric are 3rd level, where the old and new tables agree for bonuses from +1 to +4 (not verified: the party's ability scores). If seeds move, each is classified. The caster soak moves (druid and sorcerer slots).

### 4. Spell levels by casting class (`b15-class-spell-levels`)

IDs: SPL-008 (narrowed), SPL-099, SPL-100, SPL-105. Behaviour, *broad*.

1. Slots, preparation, permission to cast and the AI's slot accounting read `GetSpellLevelFor(class)` (R5).
2. Fix the class levels of Flame Strike and Acid Fog (SPL-099), and of Plane Shift, Heal, True Seeing and Mislead (SPL-100).
3. Put the domain-only spells back on their domain lists, with the druid and arcane entries (SPL-105).
4. `IsValidSpellForSlot` accepts a lower spell in a higher slot (R4).
5. Quick Start list fixes (DC9).
6. SPL-008 is narrowed to the log line and the two Concentration DCs, which the issue keeps listing.

Tests:
- `SpellLevelForClassTests`: Bard See Invisibility at 3, Sorcerer Bestow Curse at 4, the cleric list without the SPL-105 spells;
- `rules/lower-spell-higher-slot` (Ui and AI);
- an update to `SpellRoutingGoldenTests` if the levels touch routing.

TEST BUDGET: 1 suite (about 40 asserts), 2 scenarios. Run `rules/*` and `smoke/*` at seeds 1-3, and both soaks. A new baseline is expected: the Quick Start cleric and wizard lists change.

### 5. Spell data: summons, Sleep and Magic Circle (`b15-spell-data`)

IDs: SPL-021, SPL-266, SPL-267, SPL-178, SPL-196, SPL-035, SPL-044 (narrowed). Behaviour, *broad*.

1. Summon Monster I and II: unflag the placeholder, Close range, dismissible; add Bard entries for Summon Monster I-IV (SPL-021 covers only the placeholder flag; the Summon Monster III and IV Bard entries are SPL-266 and SPL-267, which this step also closes). SPL-044 goes from 47 to 45 flagged spells.
2. Sleep affects 4 HD, with ties by distance from the point of origin (R14; SPL-178, SPL-196).
3. Magic Circle ids get their alignment descriptors (SPL-035; step 6's preparation ban needs them).

Tests:
- `SleepRulesTests` (the HD cap and the tie order);
- `rules/summon-monster-1-close-range` and `rules/sleep-four-hd` (Ui and AI).

TEST BUDGET: 1 suite (about 25 asserts), 4 scenarios. Run `rules/*summon*`, `rules/*sleep*` and `smoke/*` at seeds 1-3, and both soaks. A new baseline is expected: the party wizard's Sleep and Summon Monster II change.

### 6. Preparation by entry (`b15-prepare-by-entry`)

IDs: SPL-015, CHR-078 (narrowed). Behaviour, *broad*.

1. Add `PreparedSpellEntry` and `PreparationPlan` (architecture, DR5).
2. PC creation and level-up use `LeaveOpen`. NPCs and the PC Auto-prepare button use `FillByPreference`. Legacy `PreparedSpellSlotIds` are parsed into entries by id, not by index (SPL-015).
3. Preparation refuses spells whose alignment descriptor opposes the cleric's alignment (R6). CHR-078 is narrowed to the domain-choice half, which step 8 uses through `ClericDomainMatchesAlignment`.
4. The metamagic field maps the existing metamagic-slot path.
5. The new `NPCDefinition` fields go into `Clone` and `CopyDefinitionFields` (CRE-023).

Tests:
- `PreparationPlanTests`: a maximum-placement proof on adversarial inputs; domain-only, regular-only and flexible entries; lower spells in higher slots; the alignment ban; `LeaveOpen` against `FillByPreference`;
- a Quick Start manifest of each prepared caster's slots before and after;
- `rules/prepare-domain-by-id` (Ui and AI).

TEST BUDGET: 1 suite (about 60 asserts), 2 scenarios. Run `rules/*` and `smoke/*` at seeds 1-3, and both soaks. A new baseline is expected: Shield of Faith and Aid are now placed.

### 7. Multiclass casting and conversions (`b15-multiclass-and-conversions`)

IDs: SPL-011, SPL-159, SPL-108. Behaviour, narrow.

1. Each class casts from its own slots with its own key ability (R3). Two spontaneous classes are supported (SPL-011). A spell's source is chosen by the spell and the class lists, not by the order of the classes (SPL-159).
2. Druid spontaneous Summon Nature's Ally (R7, SPL-108).
3. The AI casts a prepared slot that already holds the spell before converting another slot.

Tests:
- `MulticlassCastingTests` (cleric/sorcerer and bard/sorcerer);
- `rules/druid-convert-sna` and `rules/multiclass-prepared-and-spontaneous` (Ui and AI).

TEST BUDGET: 1 suite (about 30 asserts), 4 scenarios. Run the touched rules and both soaks. The baseline is expected hash-equal; the cleric's choice of slot is checked and any moved seed is classified.

### 8. DMG caster lists (`b15-dmg-caster-lists`)

IDs: ENC-021, ENC-002 (narrowed); files the companion gap; Q4 default. Behaviour, narrow.

1. Add `NpcSpellLoadouts` and `NpcSpellLoadoutBuilder`, hooked into `BuildSpawnDefinition` on the spawn clone (DR1-DR4).
2. The kobold sorcerer 5 and the lizardfolk druid 5 use their DMG samples. The troglodyte cleric 5 uses the DMG cleric 5 sample with Evil + Destruction domains (DR2, DC6), inflict conversion (R6) and barred spells swapped (DR3).
3. The samples' gear is created through `ItemDatabase.CloneItem` ids (the cleric 5 sample: 2 scrolls of cure light wounds). The AI uses this gear from steps 15-16.
4. The crocodile spawns with the druid (DC8).
5. The spawn warning that `InitializeNPCFromDefinition` logs for a caster class without lists becomes a failing check for every DMG caster row.
6. Close ENC-021. Narrow ENC-002 (the three caster rows spawn whole). File the missing companion link and share spells (CHR, next free).
7. `rules/npc-caster-spawn-has-spells` drops its `ExpectKnown`.

Tests:
- `NpcSpellLoadoutTests`: each row's list against its sample; the DR3 and DR4 swaps listed; the template unchanged after a spawn (`NPCDatabase.Get` compared before and after); the pool reset (`ResetForNewCreature`);
- `rules/troglodyte-cleric-inflict-conversion`.

TEST BUDGET: 1 suite (about 50 asserts), 2 scenarios. Run `rules/npc-*` at seeds 1-3, and both soaks. The baseline is expected hash-equal (no DMG caster on levels 1-3). The caster soak moves for groups 1-3.

### 9. MM innate casters (`b15-mm-innate-casters`)

IDs: CRE-135 and CRE-090 (narrowed), CRE-016, CRE-030; Q2 and Q3 defaults. Behaviour, narrow.

1. The drider becomes an innate 6th-level sorcerer with the MM p.89-90 list (CRE-090 narrowed).
2. The dark naga becomes an innate 7th-level sorcerer (MM p.191) with the MM p.192 list (CRE-135 narrowed).
3. Dragons use the PHB Table 3-17 spells-known counts at their age's caster level, with the color's printed MM sample list where one exists, otherwise DC1. This closes CRE-016 and fixes the comment table (Q2).
4. Delete the vampire's lists (CRE-030, Q3).
5. The dragon's cast-first guard stays until step 14.

Tests:
- `InnateCasterTests` (counts by caster level, lists and caster levels);
- `rules/drider-casts` and `rules/dark-naga-casts` (AI).

TEST BUDGET: 1 suite (about 35 asserts), 2 scenarios. Run `rules/dragon-*` and the new scenarios at seeds 1-3, and both soaks. The baseline is expected hash-equal: the brass wyrmling on dungeon level 3 casts nothing at its age. Moved seeds are classified.

### 10. The adept (`b15-adept`)

IDs: CHR-024 (narrowed), CHR-110, CHR-054 (narrowed), TST-008 (reviewed). Behaviour, narrow.

1. The adept joins the divine prepared casters through `CasterClassRegistry` (CHR-024 narrowed to the other NPC-class shells).
2. The DMG p.107 table goes into `SpellSlotTables`. The 7 missing 1st-level spells are added to `AdeptSpellList` (R8; closes CHR-110).
3. The familiar comes at 2nd level, and `AdeptClass`'s info text says so (CHR-054 narrowed to the sorcerer strings).
4. `goblin_adept`'s lists follow DC2.
5. Rewrite batch 14's `rules/ai-healer-no-spell-no-approach`. Review TST-008's adept and NPC-template cases and update `tools/tests/static-suites.json` if entries change.

Tests:
- `AdeptCastingTests`;
- `rules/adept-heals-ally` (AI) and its Ui twin with an adept PC.

TEST BUDGET: 1 suite (about 30 asserts), 3 scenarios. Run `rules/ai-healer-*` and `rules/adept-*` at seeds 1-3, and both soaks. Caster soak group 7 moves.

### 11. Midpoint full regression (`regression-midpoint`)

Kind regression. Run, through the Unity MCP:
1. the static runner in edit mode, then the Play pass in its own Play session; both verdicts OK;
2. `QueueFresh("rules/*,smoke/*", "1-5", "")`;
3. `QueueFresh("soak/dmg-random-encounters", "1-50", "perSession=10")` and `QueueFresh("soak/dmg-casters", "1-49", "perSession=10")`.

Record the figures, the caster columns against step 1's caster baseline, and the AI-080 disagreement counts in TESTING 3.2. A new Fail, or a new kind of AI-080 disagreement, blocks step 12. Change no game code; file any regression as a new issue.

### 12. Spell scores on the attack scale (`b15-ai-spell-scale`)

IDs: AI-022, AI-082, AI-069 (narrowed or closed), AI-011 (narrowed). Behaviour, *broad*.

1. Spell scores are expressed in expected HP-equivalent units, the same as `AttackValue`.
2. School priority becomes a bounded 0-12 term (AI-022). The flat `SETUP_SPELL_BONUS` is dropped for plain buffs (AI-011 narrowed; `RegisterPlan` and the Record methods stay filed).
3. The resource score counts real slots per class level and uses the caster level (AI-082).
4. A spell must score above doing nothing (0). `HasNoModelledCombatEffect` also skips placeholder spells and orisons with no combat effect. AI-069 closes if the caster soak shows no such casts; otherwise it is narrowed.
5. Every term goes to `AiDecisionTrace`.

Tests:
- `AiSpellScaleTests`: term ranges; the two-copies resource case; placeholders skipped;
- `rules/ai-no-light-in-combat`.

TEST BUDGET: 1 suite (about 40 asserts), 1 scenario. Run `rules/ai-*` at seeds 1-3, and both soaks with every moved seed classified.

### 13. Swarms and undead targets (`b15-swarm-undead-targets`)

IDs: AI-059, AI-062, CRE-007. Behaviour, *broad*.

1. Add `CastStageSwarmImmunity` after `CastStageGlobe`. A spell that targets a specific number of creatures has no effect on a swarm (R10), for PC and NPC casts alike.
2. The AI's chooser skips such spells against swarms and prefers areas (AI-059).
3. Swarm damage dice follow MM p.316 (CRE-007).
4. The AI casts inflict spells on undead allies through `ChooseHealerApproach`, and cures on undead enemies (AI-062). A cure potion harms an undead drinker (R11).

Tests:
- `SwarmImmunityStageTests`;
- `rules/swarm-immune-single-target` (Ui and AI);
- `rules/cleric-inflict-heals-zombie`.

TEST BUDGET: 2 suites (about 40 asserts), 4 scenarios. Run `rules/*swarm*`, `rules/*undead*` and `smoke/*`, and both soaks. A new baseline is expected: the baseline soak's dungeon levels 1-3 hold spider (1), bat and rat (2) and locust (3) swarms.

### 14. Directing the sphere and the dragon's choice (`b15-sphere-and-dragon`)

IDs: AI-029 (kept hook), AI-037 (narrowed). Behaviour, narrow.

1. Add `ActionIds.DirectSpellEffect` and `DirectSpellEffectExecutor` (a move action, R12). The PC sphere path (`GameManager_FlamingSphere`, anchor l.344) and the AI path both call it.
2. Batch 14's `ChooseAfterStandardAction` gains a `DirectEffect` outcome, valued by the sphere's expected damage.
3. Remove `TryControlFlamingSphereForAI` from AI-029's kept hooks.
4. The dragon compares its best spell, breath and full attack in expected HP through `AttackValue`. This replaces the cast-first guard (AI-037 narrowed to the full tactics).
5. Add the action to the legal set.

Tests:
- `DirectSpellEffectTests`;
- `rules/sphere-directed-ai` and its Ui twin;
- `rules/dragon-ai-breath-young` updated to the comparison.

TEST BUDGET: 1 suite (about 25 asserts), 3 scenarios. Run `rules/*sphere*` and `rules/dragon-*`, and both soaks.

### 15. Potions (`b15-potions`)

IDs: AI-009 (narrowed); Q6 default. Behaviour, *broad*.

1. `AiConsumableChooser` replaces `AIConsumableManager`. `NPCTemplateSystemTests` is rewritten.
2. A new `TryNPCUseConsumableTurnForAI` is called in `AIService.ExecuteNPCTurn` after the summon branch and runs batch 13's `UseItemActionExecutor` through `RunAIAction` (DC4, R13).
3. An incorporeal creature cannot use a corporeal item (DMG p.295: it cannot manipulate objects). This is a rule, not an AI choice, so the refusal goes in the shared gate `GameManager.ItemManipulationConditionRefusal`, which the PC use-item button and the chooser both read. Whether the creature's own incorporeal gear is exempt is recorded as a delegated RAW reading. A Ui twin `rules/incorporeal-cannot-drink` sits beside the AI test.
4. `CharmedBehaviorController.AdministerHealingPotion` uses the chooser.
5. AI-009 is narrowed to scrolls and wands.

Tests:
- `AiConsumableChooserTests` (the threshold, `AoOCost`, administer, incorporeal);
- `rules/npc-drinks-potion` and `rules/npc-administers-potion`.

TEST BUDGET: 1 suite (about 30 asserts), 2 scenarios. Run `rules/*potion*` and `smoke/*`, and both soaks. Moves are expected if the party carries potions.

### 16. Scrolls and wands (`b15-item-casts`)

IDs: AI-009 (narrowed to summon and wall items). Behaviour, narrow.

1. Scrolls and wands in the inventory become candidates in the spell chooser, scored on step 12's scale with a `CastSource` of Scroll or Wand.
2. Add a `SpellCastRequest.ForAIArea` overload with a `CastSource`, so area items work. `CheckItemUse` admits area items and still refuses summon and wall items.
3. Wand charges in NPC gear follow the DMG rule, read at this step; record a delegated RAW reading if the DMG is silent.
4. At most 4 item candidates per turn; the `AiDecisionTimer` budget is asserted.
5. AI-009 is narrowed to summon and wall items.

Tests:
- `AiItemCastTests`;
- `rules/npc-reads-scroll` and `rules/npc-wand-area` (AI), with the Ui reference.

TEST BUDGET: 1 suite (about 25 asserts), 2 scenarios. Run `rules/*item*` and `rules/*scroll*`, and both soaks. The step states whether the Quick Start inventory moves the baseline.

### 17. Wards against control (`b15-wards-and-control`)

IDs: SPL-137, SPL-140; files the dominated-side issue. Behaviour, *broad*.

1. Protection from Evil and Magic Circle block control from any source, as SPL-137 states after this step's re-read of PHB p.266 and the mirrored spells (R15). They also block bodily contact by every summoned creature the ward does not exempt (for Protection from Evil, every summon that is not good), and only the deflection and resistance bonuses keep the alignment match (SPL-137's second half; PHB p.266, p.249). `ProtectionFromAlignmentTests` and `MagicCircleRulesTests`, which assert `HasMatch` is false against neutral sources, are updated to test the bonuses separately from the two blocks. Correct SPL-137's wording about the mirrored spells.
2. Audit the "grants ongoing control" flag:
   - Sleep, Hold Person, Hold Monster, Deep Slumber and Hypnotism lose it (the flag is `SpellData.BlockedByProtectionFromAlignment`, true today on those five and on Charm Person, Charm Monster and Dominate Person);
   - Dominate Animal and Command Undead gain it.
3. Suppression follows R15: a suppressed effect resumes when the ward ends if its own duration has not run out (explicit RAW, PHB p.266).
4. Command Undead and Dominate Person run on a tracked clock through `TurnDurations` (SPL-140).
5. File the dominated-side issue (DC5).

Tests:
- `ControlWardTests`;
- `rules/prot-evil-suppresses-dominate` (Ui and AI).

TEST BUDGET: 1 suite (about 30 asserts), 2 scenarios. Run `rules/*ward*`, `rules/*control*` and `smoke/*`, and both soaks.

### 18. Insect Plague swarms (`b15-insect-plague-swarms`)

IDs: SPL-136. Behaviour, narrow (third to cut).

1. Insect Plague creates persistent locust swarms as SPL-136 states (R16; PHB p.244-245; MM p.239).

Tests: `rules/insect-plague-swarms-persist`.

TEST BUDGET: 1 scenario and about 15 asserts. Run the touched rules and the caster soak.

### 19. Alter Self (`b15-alter-self`)

IDs: SPL-135; Q5 default. Behaviour, narrow (first to cut).

1. Add `AlterSelfRules` and a `CastChoices` form field filled by both deciders (DC7, R16). Closes SPL-135.

Tests: `AlterSelfRulesTests`; `rules/alter-self-natural-attacks` (Ui and AI).

TEST BUDGET: 1 suite (about 20 asserts), 2 scenarios. Run the touched rules.

### 20. Final full regression (`regression-final`)

Kind regression. Run the same set as step 11 on a clean tree. Then:
- **Casters.** Record `npcCasts`, `npcRefusedPicks`, `npcNoEffectCasts` and `npcItemUses` against the midpoint and step 1's caster baseline.
- **Legal set.** Record the AI-080 counts against the midpoint; no new disagreement kind.
- **Hashes.** Every soak seed that moved has a classified first divergence.
- **Issues.** Close or narrow the ledger's issues where the run proves them.
- **CLAUDE.md "Current state":** batch 15 done; next is batch 16.

Batch 15 closes when there are 0 Fail, the static verdicts are OK, and every moved soak seed is classified.

## Behaviour changes

| Step | Who changes | `soak/dmg-random-encounters` | `soak/dmg-casters` |
|---|---|---|---|
| 1 | nobody | equal | baseline |
| 2 | casting profiles on a melee routine, or with no castable spell | moves (reach) | moves (drider, dragon) |
| 3 | druids, sorcerers, bards, paladins, rangers; casters below the minimum score | equal expected | moves (druid, sorcerer) |
| 4 | every prepared caster; the Quick Start cleric and wizard | moves (Quick Start lists) | moves |
| 5 | Sleep and Summon Monster I-II users | moves (Sleep, Summon Monster II) | moves |
| 6 | every prepared caster (placement by id) | moves (cleric slots) | moves |
| 7 | multiclass casters and druids | equal expected | moves (druid) |
| 8 | the three DMG caster rows | equal | moves (groups 1-3) |
| 9 | drider, dark naga, dragons, vampire | equal expected | moves (groups 4-6) |
| 10 | adepts | equal | moves (group 7) |
| 12 | every AI caster | moves | moves |
| 13 | casters facing swarms or undead | moves (swarms, undead) | moves |
| 14 | Flaming Sphere casters, dragons | small moves | moves (dragon) |
| 15 | AI creatures with potions | moves if the party has potions | moves |
| 16 | AI creatures with scrolls or wands | small moves | moves |
| 17 | warded creatures; control spells | moves | moves |
| 18 | Insect Plague casters | equal | small moves |
| 19 | Alter Self casters | equal | equal |

### Issue ledger

- **Closed:**
  - SPL: SPL-015, SPL-021, SPL-266, SPL-267, SPL-035, SPL-041, SPL-099, SPL-100, SPL-105, SPL-108, SPL-011, SPL-159, SPL-178, SPL-196, SPL-136, SPL-137, SPL-140 and SPL-135 (if step 19 is built).
  - ENC: ENC-021.
  - CRE: CRE-016, CRE-030, CRE-007.
  - CHR: CHR-110, CHR-116 (or narrowed), CHR-117, CHR-129, CHR-181.
  - AI: AI-022, AI-082, AI-038, AI-059, AI-062.
- **Narrowed:** SPL-008, SPL-044, CHR-078, CHR-024, CHR-054, CHR-159, ENC-002, CRE-090, CRE-135, AI-009, AI-011, AI-029 (its kept-hook list), AI-037, AI-069 (or closed).
- **Filed:** AI-082 with the plan (2026-11-16; the table audit had already filed the plan's other findings, see step 1); during the batch, the companion gap (step 8), the dominated-side issue (step 17), and any missing sample spells (DR4). Each takes the next free ID at filing.

## Risks

- **The preparation matcher (step 6) changes every prepared caster at once.** Mitigation: golden manifests per class and level, a maximum-placement proof test, the before-and-after Quick Start manifest, and a new soak baseline with classified seeds.
- **Batch 14 and grouped-turn names may differ as built.** Mitigation: each step re-greps its anchors and records any rename here.
- **Caster soak fights may end too fast to measure** against a 3rd-level party. Mitigation: the DC3 fallback.
- **Renames break reflection tests** (TST-006). Mitigation: grep `Assets/Scripts/Tests` before renaming slot members.
- **AI turns may slow down** with item and direct-effect candidates. Mitigation: candidate caps and `AiDecisionTimer` asserts.
- **New AI actions may fall outside the legal set** (AI-080). Mitigation: each action joins `ActorActionSet` in its step, and the midpoint blocks on new disagreement kinds.
- **The loadout builder may mutate templates.** Mitigation: it writes only to the spawn clone, and a test compares the template before and after.
- **Pool reuse may leak state** (CRE-046). Mitigation: reset tests for the new per-creature state.
- **Many steps move the soak, which can hide a regression.** Mitigation: every moved seed is classified by `FirstDivergence`.
- **Concurrent issue edits.** Another workflow edits `docs/issues/*`. Every step files issues at the next free ID.
- **The weekly usage cap.** Mitigation: the cut line, with the midpoint protected.

### Not verified

- Wand charges in NPC gear (step 16).
- The exact wording of Protection from Evil's mirrored spells (step 17).
- Which baseline monsters run a Spellcaster profile on a melee routine (step 2).
- The Quick Start party's ability scores (step 3's expectation).
- Whether `ResolveCreatureName` parses the three caster rows and the crocodile (step 8).
- The DMG sample pages, checked on 2026-11-16 by sample header: Sample 5th-Level NPC Cleric (human, NG, Healing and Protection) p.114, Druid (lizardfolk) p.115, Sorcerer (kobold, with a viper familiar) p.124, Wizard (drow) p.125.
- The companion page (PHB p.36).
- Every batch 14 and grouped-turn member, which are unbuilt.
- Nothing here was run in Play mode.

## Not in scope

- Ready actions and readied counterspells (Q1; CMB-029, SPL-047).
- Casting before combat (needs batch 17's awareness).
- Spell-like abilities of MM monsters: green hag, annis, mind flayer, imp, quasit, drow, doppelganger and lantern archon.
- Summon and wall items.
- Use Magic Device.
- Team switching of dominated creatures (DC5).
- Animal companion link and share spells (filed at step 8).
- The familiar's benefits beyond the 2nd-level gate.
- NPC metamagic choices beyond the existing ones (SPL-010, SPL-064).
- Spellbook acquisition and preparation between battles.
- Levels 9-20.
- The rest of SPL-008 (the log line and the Concentration DCs).
