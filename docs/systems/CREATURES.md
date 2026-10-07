> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-03.

# Creatures: content and status

This doc lists the monsters and NPCs, their special abilities, templates, dragons, summoning and NPC classes. For each it gives what exists, what works, and what is missing measured against the 3.5e Monster Manual (MM), Player's Handbook (PHB) and Dungeon Master's Guide (DMG). For how the code fits together (NPCDefinition, NPCDatabase, `InitializeNPCFromDefinition`, the template registry) read [architecture/characters-and-creatures.md](../architecture/characters-and-creatures.md#creatures); this doc does not repeat it. Bugs are cited by ID; the entries are in [issues/CRE.md](../issues/CRE.md), [issues/AI.md](../issues/AI.md) and the other issue files.

Nothing below was checked in Play mode. Counts come from running `NPCDatabase.Init` outside Unity against the compiled Assembly-CSharp at this commit. To repeat that: write a throwaway .NET 8 console that references the `tools/compile_check.sh` output DLL and the editor's UnityEngine.CoreModule.dll (resolving the other `-r:` references from the compiler response file), set `Debug.unityLogger.logHandler` to a stub, call `NPCDatabase.Init`, and read the private `_npcs` dictionary by reflection. Init logs "Initialized with 389 NPC types." Everything else was traced statically.

## 1. Creature inventory

### By origin

| Origin | IDs | Where |
|---|---|---|
| Entries in the letter partials: Monster Manual creatures plus about 15 hand-written class NPCs (human_monk_3/5/7, human_paladin_3/5/7, human_warrior, orc_warrior, ...) | 255 | Character/Creatures/NPCDatabase_A.cs ... _Y.cs (helpers: `RegisterMonstrous*Variant`, `MephitBase`) |
| True dragons, 10 types x 6 ages | 60 | DragonData.cs + NPCDatabase_Dragons.cs (`BuildDragonDefinition`) |
| Pre-templated undead and lycanthropes | 23 | SkeletonFactory (8), ZombieFactory (7), LycanthropeFactory (8) |
| Custom, test and scenario NPCs | 45 | NPCDatabaseCustom.cs (46 registrations; its `skeleton_archer` is overwritten by SkeletonFactory) |
| Summon aliases | 6 | `NPCDatabase.RegisterSummonCreatureAliases`. 5 of them overwrite real entries (CRE-003); only `wolf` is a new ID |
| **Total** | **389** | |

NPCDatabaseCustom.cs is not only test data. brown_bear, dire_bear, dire_tiger, dire_wolf, tiger and wolf_pack_hunter (the MM wolf) live there too.

### By creature type and CR band

This table covers the 338 MM, dragon and template-built entries. Custom NPCs and aliases are excluded. "MagicalBeast" (chimera and three hydras) is merged into Magical Beast.

| Type | <1 | 1-2 | 3-4 | 5-6 | 7-8 | 9-10 | 11+ | no CR | Total |
|---|---|---|---|---|---|---|---|---|---|
| Dragon | 0 | 4 | 9 | 10 | 10 | 8 | 20 | 0 | 61 |
| Outsider | 1 | 6 | 19 | 11 | 11 | 3 | 0 | 0 | 51 |
| Animal | 8 | 20 | 7 | 2 | 0 | 0 | 0 | 2 | 39 |
| Humanoid | 12 | 9 | 5 | 2 | 4 | 0 | 1 | 0 | 33 |
| Vermin | 8 | 9 | 6 | 2 | 3 | 1 | 2 | 1 | 32 |
| Undead | 4 | 6 | 11 | 2 | 6 | 0 | 1 | 0 | 30 |
| Magical Beast | 2 | 5 | 10 | 6 | 6 | 0 | 1 | 0 | 30 |
| Aberration | 0 | 2 | 7 | 4 | 8 | 1 | 1 | 0 | 23 |
| Monstrous Humanoid | 0 | 1 | 6 | 3 | 2 | 0 | 0 | 0 | 12 |
| Elemental | 0 | 5 | 4 | 0 | 0 | 0 | 0 | 0 | 9 |
| Giant | 0 | 0 | 1 | 2 | 3 | 2 | 0 | 0 | 8 |
| Ooze, Plant | 0 | 1 | 4 | 1 | 2 | 0 | 0 | 0 | 8 |
| Construct, Fey | 0 | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 2 |
| **Total** | 35 | 69 | 89 | 45 | 56 | 15 | 26 | 3 | 338 |

The Dragon row includes the wyvern. The "Humanoid" 11+ entry is werewolf_lord (CR 14). Only one construct (flesh_golem) and one fey (satyr) exist.

### Coverage notes

- **Random-encounter pool.** `RandomEncounterSystem.BuildCandidates` keeps only NPCs that have a parseable CR and no "SummonBase", "SummonAlias" or "Summoned" tag, and whose ID does not contain "summon". That leaves 295 candidates. The tag filter also removes 48 entries that have a CR: the 5 aliases with a CR (badger, giant_bee, owl, raven, riding_dog) and 43 real creatures tagged "SummonBase" because they were added for summon lists: ape, bison, black_bear, boar, crocodile, hell_hound, hippogriff, lantern_archon, satyr, unicorn, wolverine, all small and medium elementals, all 10 mephits, noble_djinni and others. They can never appear in a CR/EL-generated encounter (ENC-020).
- **Missing CR.** octopus, small_viper and monstrous_centipede_medium (the summon version from NPCDatabase_M.cs wins the ID collision) have no ChallengeRating. Neither do the MM animals in the custom file (brown_bear, dire_bear, dire_tiger, tiger, wolf_pack_hunter). None of them can be generated randomly. They still give XP: the live award (`ExperienceCalculator.GetChallengeRating`, called from GameManager.LootCollection.cs) falls back to CR = character level when the CR does not parse; only the `TotalEncounterXPDefeated` statistic in `GameManager.RegisterCombatLoopCompletion` skips them. Conversely, xp_pinata_goblin (CR 15) and mirror_image_test_goblin are random candidates.
- **ID collisions and duplicates:** CRE-003 and CRE-022 (dretch, the monstrous vermin sizes, skeleton_archer; large_viper/viper_large).
- **Alignment.** 162 non-alias definitions set `CharacterAlignment`, but spawned NPCs never receive it (CRE-002).
- **Movement modes.** There are no fly, swim, climb or burrow fields. `BaseSpeed` is in 5-ft squares and the fastest mode is folded into it. A "Fly" tag is read in only a few places, and the "Aquatic" tag is read nowhere, so octopus, large_shark and crocodile walk on land.
- **Class levels.** 334 of the 389 runtime entries carry `CharacterClass = "Warrior"` as a placeholder for racial HD (CRE-024). See section 7 for the side effects.

### Notable core MM monsters missing (CR 1-10)

None of these exists under any ID or name. The CRs are from memory of the MM, not re-checked; check them against the book before entering data.

| Type | Missing (approx. MM CR) |
|---|---|
| Aberration | athach (7), delver (9) |
| Animal | horse light/heavy/war (1-2), rhinoceros (4), polar bear (4), elephant (7), shark Medium/Huge (1/4), squid (1), giant squid (9), orca (5), dire shark (9) |
| Construct | homunculus (1), animated objects (1-5 by size), shield guardian (8), clay golem (10) |
| Dragon | pseudodragon (1), dragon turtle (9) |
| Elemental | Large (5), Huge (7), Greater (9), all four elements |
| Fey | dryad (3), grig (3), nixie (1), pixie (4), nymph (7) |
| Magical Beast | blink dog (2), pegasus (3), griffon (4), sea cat (4), winter wolf (5), hieracosphinx (5), criosphinx (7), dragonne (7), remorhaz (7), gynosphinx (8), lammasu (8), androsphinx (9), yrthak (9); hydra 6-, 8- and 10-headed (5/7/9), pyro- and cryohydras |
| Monstrous Humanoid | kuo-toa (2), sahuagin (2), centaur (3), sea hag (4), lamia (6) |
| Outsider | azer (2), triton (2), tojanida (3/5), achaierai (5), rast (5), ravid (5), lillend (7), vrock (9), bone devil (9), night hag (9), formian warrior (3) and myrmarch (10) |
| Plant | assassin vine (3), tendriculos (6) |
| Vermin | giant ant soldier (1) and queen (2) |
| Naga | water naga (7), guardian naga (10) |

The ghost, vampire and lich exist only as fixed stat blocks, not as MM templates (section 4).

## 2. Defining a creature

For the step-by-step recipe and its pitfalls, follow "Add a monster or NPC" and "Add a creature template" in [DEVELOPMENT_RECIPES.md](../DEVELOPMENT_RECIPES.md#add-a-monster-or-npc). The NPCDefinition fields that change play:

| Field group | Fields | Notes |
|---|---|---|
| Identity | `Id`, `Name`, `ChallengeRating` (string), `CreatureType`, `SizeCategory`, `CharacterAlignment` | CR null removes it from random generation and XP. Alignment is ignored at spawn (CRE-002) |
| Hit dice and HP | `HitDice`, `BaseHitDieHP`, `Level`, `CharacterClass` | HitDice drives BAB and saves through the creature-type progression. HP double-counts CON (see the recipe pitfalls) |
| Progression overrides | `BABOverride`, `BaseAttackBonusOverride`, `Fortitude/Reflex/WillSaveOverride` | `NPCDefinition.BAB` is never read at spawn (CRE-004) |
| Attacks | `NaturalAttacks` (NaturalAttackDefinition: dice, `IsPrimary`, `PoisonOnHitId`, `HasDiseaseOnHit`, `ParalysisOnHitDC`, `PetrificationOnHitDC`, `EnergyDrainOnHit`, `AbilityDrainType/Amount`, `HasBloodDrain`, `BonusElementalDamage*`), `EquipmentIds` | On-hit riders are resolved in `CharacterController.TryApplyNaturalAttackOnHitEffects` |
| Maneuver flags | `HasImprovedGrab` + `ImprovedGrabTriggerAttackName`, `HasPounce`, `HasRake` + `RakeAttack`, `HasTripAttack`, `HasTrample`, `HasScent` | |
| Defenses | `DamageReductionAmount/Bypass/RangedOnly`, `DamageResistances`, `DamageImmunities`, `Immunities` (CreatureImmunities), `SpellResistance`, `IsIncorporeal`, `IsMindless`, `RegenerationAmount` + `RegenerationSuppressedBy` | |
| Ability payloads | `BreathWeapon`, `SecondaryBreathWeapon`, `FrightfulPresence`, `Engulf`, `AuraAbility`, `RangedSpecialAttack`, `BloodDrain`, `TerrainManipulation`, `StenchAuraDC/Range`, `IsSwarm` + `SwarmTraits` | Each needs Clone, Configure*, spawn wiring and AI use (recipe step 6) |
| Magic | `KnownSpellIds`, `PreparedSpellSlotIds` | The only route for spell-like abilities. Casting initializes only for a spellcasting class |
| AI | `AIBehavior`, `AIProfileArchetype`, `CreatureTags` | See [AI.md](AI.md#24-where-configuration-comes-from) and [AI.md section 6](AI.md#6-profiles-and-archetypes) |
| Templates | `AppliedTemplateIds` | Means both "apply" and "already applied" (CRE-001) |

`SpecialAbilities` and `Description` are display text only. Before declaring an ability missing, check all three places mechanics can live: an ability payload on NPCDefinition, an on-hit field on NaturalAttackDefinition, and bespoke ID-checked code (for example `giant_bombardier_beetle` in `GameManager.TryNPCUseBombardierAcidSpray`).

## 3. Special abilities

Status key: **Implemented** = has a runtime effect that matches the rule in outline; **Partial** = has a runtime effect that differs from the MM; **Data-only** = configured but nothing executes it; **Text** = SpecialAbilities string only.

| Family | Data | Creatures | Status | Where it runs; gaps | Issues |
|---|---|---|---|---|---|
| Breath weapon | `BreathWeapon` | 60 dragons, hell_hound, ankheg, behir, chimera, digester, gorgon | Partial | Only `AIService.ExecuteDragonTurn` fires it, which needs the Dragon archetype (the dragons and hell_hound). The ankheg, behir, chimera, digester and gorgon never breathe. `UseBreathWeapon` sets the recharge, but `TickBreathWeaponCooldown` has no caller, so each breath fires at most once per combat. Gorgon breath has 0 damage and no petrification | AI-004 |
| Metallic gas breath | `SecondaryBreathWeapon` | 30 metallic dragons | Data-only | Configured at spawn; no executor | AI-008 |
| Frightful presence | `FrightfulPresence` | 20 dragons (Young Adult, Adult), lich | Implemented | Fires once per combat on the creature's first attack or breath (`GameManager.NPCPerformAttack`, `NPCExecuteBreathWeaponForAI`, `ResolveFrightfulPresence`). The lich's MM Fear Aura is modelled with this payload, so it fires on attack rather than acting as a constant aura | AI-010 |
| Gaze | `AuraAbility` | basilisk and medusa (Petrified), chain_devil (Sickened), spirit_naga ("Charming Gaze" applied as Fear), umber_hulk (Confused) | Partial | Treated as an aura in `AIService.ProcessAuraAbility`. It only fires on the gazer's own AI turn; there is no averting or closing eyes and no save at the start of the target's turn | AI-013 |
| Other auras | `AuraAbility` | allip, beholder, cloaker, gibbering_mouther, harpy, krenshar, mummy, shadow_mastiff, vargouille | Partial | Same processor. It is skipped while the owner is confused, charmed, fascinated, frightened, turned, grappling or has no target. Only the mouther's Gibbering grants immunity on a save; it lasts until combat ends (a static set cleared in `GameManager.OnCombatEnded`), not 24 hours. The beholder's "Antimagic Cone" is a placeholder Frightened aura with save DC 0; the vargouille frightens instead of paralyzing | CRE-012, AI-013 |
| Stench | `StenchAuraDC/Range` | ghast, troglodyte, nightmare | Data-only | `HasStenchAura` has no reader. The tier3_ghast_ambush preset advertises it | CRE-031 |
| Engulf | `Engulf` | cloaker, gelatinous_cube, gibbering_mouther, ochre_jelly | Partial (stub) | `AIService.TryExecuteEngulf`: a Reflex save, then Helpless forever. It does no damage, has no escape or paralysis and no size check. It returns true whenever the target is adjacent, so an engulfer never makes melee attacks and re-engulfs Helpless targets. It only exists in the AggressiveMelee routine | CMB-024 |
| Swallow whole | none | purple worm and others (text) | Text | | |
| Improved grab | `HasImprovedGrab` + trigger attack | 42 | Implemented | `GameManager.TryResolveImprovedGrabFromAttackResults`; AnimalAIProfile and GrapplerAIProfile choose grapples. Engulfers never reach it | AI-025 |
| Constrict | none | otyugh, ochre jelly, constrictor snakes (text) | Text | No constrict logic exists anywhere | |
| Pounce, rake | `HasPounce`, `HasRake` + `RakeAttack` | 10 and 9 (big cats, behir, skum, slaadi, hellcat) | Implemented | Charge full attack (SupportActions); rake in GrappleSystem; AnimalAIProfile | |
| Trip on hit | `HasTripAttack` | 11 (wolves, worg, hyena, cheetah, yeth hound, werewolves) | Implemented | `CharacterController.ResolveFreeTripAttempt` from the NPC attack path and ThreatSystem: no touch attack, no AoO, then the PHB trip check (STR + special size). `TripAttackCheckBonus` is the MM stat-block number for reference only; the check is computed from STR and size. Quadruped stability is missing | CMB-079, CMB-085 |
| Trample | `HasTrample` | treant | Data-only | | CRE-018 |
| Poison | `PoisonOnHitId`; `SwarmTraits.PoisonId` | 13 (vipers, wasp, centipede, wyvern, purple worm, phase spider, Medium and Large monstrous spiders...) | Partial | On natural-attack hit through Effects/PoisonDatabase. The monstrous centipede and scorpion sizes, the spider sizes other than Medium and Large, and small_viper and viper_tiny through viper_large have no `PoisonOnHitId`. Swarm poison only logs. spirit_naga uses phase_spider_poison as a stand-in | CRE-014 |
| Disease | `HasDiseaseOnHit` | 13 (dire rat, fiendish_dire_rat, ghoul, ghast, mummy, otyugh, 7 natural lycanthropes) | Partial | Works except Lycanthropy, which has no DiseaseDatabase entry | CRE-011 |
| Paralysis, petrification on hit | `ParalysisOnHitDC`, `PetrificationOnHitDC` | 6 (ghoul, ghast, carrion crawler, gelatinous cube, lich, mohrg); cockatrice | Implemented | Fort save | |
| Energy and ability drain on hit | `EnergyDrainOnHit`; `AbilityDrainType/Amount` | 7 (wight, wraith, spectre, vampire, vampire spawn, ghost...); allip, shadow, greater_shadow, wraith | Partial | The amounts are fixed (1, or 8 for the greater shadow), and it is always drain. MM, as recalled: allip 1d4 Wis drain, shadow 1d6 Str damage, greater shadow 1d8 Str damage, wraith 1d6 Con drain | |
| Blood drain | `HasBloodDrain` (natural attack); `BloodDrain` (definition) | stirge; gibbering_mouther, vampire | Partial / Data-only | The stirge drains 1 Con per bite hit, with no attach or 1d4. The BloodDrainDefinition payload has no reader | CRE-031 |
| Energy rider on natural attacks | `BonusElementalDamageDice/Count/Type` | 9 (ankheg, salamanders, oozes, efreeti, nightmare) | Data-only | Nothing in combat reads these fields (grep: only the class and its Clone). Small and medium fire elementals do not set them | CRE-037 |
| Ranged special attack | `RangedSpecialAttack`; bespoke bombardier spray | gibbering_mouther; giant_bombardier_beetle | Partial | Mouther spittle: a free action at the nearest enemy in 30 ft, every turn. It rolls d20 + BAB + Dex against a simple touch AC, damage skips resistances, and only "blinded" and "sickened" riders work. `IsRangedTouchAttack`, SaveDC and the cone fields are unread. `TickRangedSpecialAttackCooldown` has no caller | AI-006, AI-013 |
| Terrain manipulation | `TerrainManipulation` | gibbering_mouther | Data-only (log line) | See below | CRE-013 |
| Regeneration, fast healing | `RegenerationAmount` + `RegenerationSuppressedBy` | 14: troll 5, ogre_mage 5, chain_devil 2, lemure 2, 10 mephits 2 | Partial | `ApplyRegenerationAtTurnStart` heals HP each turn, which is fast-healing behaviour. Damage is not converted to nonlethal, and the creature still dies at -10. Suppression accepts only DamageBypassTag (material or alignment), so the troll's and ogre mage's fire and acid cannot be expressed (both are stored as None). Hydra fast healing is text only. The victory check treats any "regeneration"/"fast healing" text as able to recover | CRE-017 |
| DR, energy resistance and immunity | `DamageReduction*`, `DamageResistances`, `DamageImmunities`, `Immunities` | 93 / 27 / 88 | Implemented | Damage pipeline (CharacterStats.ApplyIncomingDamage). Special-attack and swarm damage bypass it | AI-006 |
| Spell resistance | `SpellResistance` | 46 | Implemented | Spell pipeline (Spell/Casting/SpellCaster.cs) | |
| Incorporeal | `IsIncorporeal` | 6 (allip, ghost, shadow, greater shadow, spectre, wraith) | Partial | Its only combat effect is a flat 50% miss chance in `CharacterController.GetMissChance` | CMB-025 |
| Swarm | `IsSwarm` + `SwarmTraits` | 6 (bat, rat, spider, centipede, hellwasp, locust) | Partial | SwarmAI/IndiscriminateSwarmAI; flat 6 damage; distraction | CRE-007, CRE-014 |
| Spell-like abilities | `KnownSpellIds` (no SLA type) | Among MM entries only the lich, vampire and the 33 dragons with CL > 0 list spells, and those are class or sorcerer spells, not SLAs | Mostly text | No SLA type exists (uses per day, at will). The Spellcaster or Caster archetype is set on 14 MM creatures that have **no** spells: aboleth, beholder, couatl, dark naga, drider, efreeti, formian taskmaster, green slaad, mind flayer, ogre mage, rakshasa, spirit naga, succubus, yuan-ti abomination. The vampire's spells are lost because its class is Fighter | CRE-015, CRE-017, CRE-030, AI-002, AI-004 |

### Ability backlog (verified status per monster)

- **Text only:** rust monster rust, phase spider ethereal jaunt, will-o'-wisp shock, beholder and gauth eye rays and antimagic, mind flayer mind blast and extract brain, spider webs, constrict, swallow whole, giant rock throwing (an unread "RockThrowing" tag), orc and kobold light sensitivity, arrowhawk electricity ray (could use `RangedSpecialAttack`), unicorn SLAs and magic circle, mephit breath (CRE-017).
- **Data present, never used:** five non-dragon breath weapons, metallic gas breaths (AI-008), stench (3 creatures) and `BloodDrainDefinition` (2) (CRE-031), `BonusElementalDamage*` (9), `HasTrample`.
- **Partial, remaining work:**
  - Engulf: damage per round, paralysis, an escape check, a size check (Medium or smaller), removing Helpless when the target escapes or the engulfer dies, skipping already-engulfed targets, and falling through to melee (CMB-024).
  - Breath weapons: tick the breath and ranged-special recharges in `GameManager.BeginNPCTurnForAI`, which today ticks only the bombardier cooldown; let non-dragon archetypes breathe.
  - Regeneration: express energy suppression and make damage nonlethal. Add a separate fast-healing field.
- **Not in the database:** blink dog, roper, grell.
- **Clone hazard:** `NPCDefinition.Clone` shares `RangedSpecialAttack`, `BloodDrain` and `TerrainManipulation`, and the undead and lycanthrope templates do not clear them, so a templated mouther would keep its spittle (CRE-023).

### Terrain manipulation (Gibbering Mouther Ground Manipulation)

- **Pipeline.** `TerrainManipulationDefinition` (NPCDatabase.cs) holds Name, `RadiusFeet`, `DurationRounds`, `FollowsCaster`, `EffectType`, `DamagePerRound`, `DamageType` and `SetupRounds`. It flows NPCDefinition -> `CharacterController.ConfigureTerrainManipulation` -> `AIService.ExecuteAggressiveMeleeTurn`, which calls `ActivateTerrainManipulation` after every successful advance move. That call only writes a log line. The mouther's radius is 5 ft (adjacent squares). It does not fire on a charge, when the target is already in reach, or while grappling.
- **Rule as captured in the code comments (MM, Gibbering Mouther).** At will, as a standard action, the adjacent squares become quicksand-like (stone takes longer). Any creature other than the mouther must spend a move action or become mired (treated as pinned). The "Reflex DC 13" in the creature data belongs to Engulf, not to this ability.
- **What is not done.** No cells are marked and nothing is highlighted. `TickTerrainManipulationDuration` and `IsTerrainManipulationActive` have no callers. `EffectType`, `DamagePerRound`, `DamageType` and `SetupRounds` are never read.
- **Trap: marking cells would barely matter.** Difficult terrain does not slow movement anywhere. `MovementService.GetMovementCost` has no callers, and neither movement range nor SquareGrid A* reads difficult terrain. A marked cell only blocks 5-ft steps, prone crawling and charges. Area slowdown that does work goes through `CharacterStats.EffectiveSpeedFeet` multipliers (`SolidFogAreaEffect.GetSpeedMultiplierFor`). `MovementService._difficultTerrainSquares` is a plain set with no per-source counting, so clearing one effect's cells erases overlapping Entangle, Grease or Web cells.
- **Suggested shape.** Build it as a `PersistentAreaEffect` subclass, modelled on `SoftenEarthAreaEffect` (mud with a highlight) plus the caster-following update in `LesserGlobeOfInvulnerabilityAreaEffect`. Apply `CombatConditionType.Pinned` to creatures that did not spend a move action. Tick it from `BeginNPCTurnForAI`.

### Design questions still open

Should a creature use an ability every time, or probabilistically by usefulness? How should abilities consume actions (Engulf is effectively a full turn today)? Should difficulty scale ability use (AI-012)? How should abilities be shown to players? A unified `SpecialAbility` abstraction (CanUse / EvaluateUsefulness / Execute) to replace the per-ability fields and the fixed priority order in AIService is proposed but not started. It is the natural foundation for deeper enemy AI.

## 4. Templates

Registry IDs and application order are described in [characters-and-creatures.md](../architecture/characters-and-creatures.md#templates-dragons-and-class-levels-on-monsters).

| Template | How it is applied | Status vs MM 3.5 | Known problems |
|---|---|---|---|
| skeleton (order 10) | SkeletonFactory builds 8 baked IDs; registry adapter `SkeletonCreatureTemplate` | Undead with d12 HD, Poor BAB override, Dex +2, cold immunity, DR 5/bludgeoning, Improved Initiative, claws by size, CR by HD | Applied twice at spawn (CRE-001). The adapter always passes `hasHands: true` and copies back only part of the result (CRE-023). Reports Fighter 1 (CRE-024) |
| zombie (10) | ZombieFactory builds 7 baked IDs; `ZombieCreatureTemplate` | HD doubled, Str +2 / Dex -2, Toughness, DR 5/slashing, single actions only (`IsSingleActionsOnly`), Poor BAB | CRE-001, CRE-023, CRE-024 |
| werewolf, wererat, wereboar, weretiger, werebear (15) | LycanthropeFactory builds 8 baked IDs; `LycanthropeTemplateBase` subclasses | Hybrid form only. HD and BAB merge with the animal's; DR 10/silver (natural) or 5/silver (afflicted); Iron Will; scent; claws plus bite; curse on bite (natural only) | CRE-001: re-applied at spawn, and the afflicted werewolf is re-applied as natural. CRE-011: no forms and no curse. CR is truncated (werebear 4; MM 5). Wereboar is ChaoticNeutral (MM: neutral). Alignment is ignored at spawn (CRE-002) |
| celestial, fiendish (20) | `AppliedTemplateIds` (fiendish_wolf, fiendish_dire_bear, ...), `SummonMonsterOption.TemplateId`, dungeon `CreatureTemplateIds` | DR 5/magic at 4+ HD and 10/magic at 12+; resistances 5/10; smite 1/day | Ability bonuses, and SR only from 8 HD (CRE-008). The smite never sees alignment on database NPCs (CRE-002) |
| half-dragon | Emitted only by `EncounterDescriptionParser` ("1 half-dragon 4th-level fighter") | Not registered | `ResolveTemplates` drops it silently (ENC-005) |

**Lycanthrope variants.** All are human bases.

| ID | Base | Animal | Type | CR (code) |
|---|---|---|---|---|
| werewolf | warrior 1 | wolf | natural | 3 |
| werewolf_lord | fighter 10 | dire wolf | natural | 14 |
| wererat | rogue 1 | dire rat | natural | 2 |
| wereboar | barbarian 1 | boar | natural | 4 |
| weretiger | fighter 4 | tiger | natural | 8 |
| werebear | commoner 1 | brown bear | natural | 4 |
| dire_wereboar | barbarian 1 | dire boar | natural | 7 (hard-set) |
| werewolf_afflicted | commoner 1 | wolf | afflicted | 2 |

There are 7 lycanthrope encounter presets. The werewolf_lord preset's escorts are two werewolves.

**Factory path vs registry path.** The baked IDs are build-time results that are registered in NPCDatabase. The registry is for runtime templating; Summon Monster, for example, picks arbitrary bases. The two paths give different results, because the adapters copy back about 33-37 fields. Only the summon path clears `AppliedTemplateIds` before adding its template.

**Missing MM templates:** ghost, vampire and lich (only fixed `ghost` CR 7, `vampire` and `lich` entries exist), half-celestial, half-fiend, half-dragon, and lycanthropes on non-human bases (`LycanthropeFactory.CreateFromRegistered` exists but has no data).

**Template backlog:**
- Fix double application (CRE-001) before adding any template.
- Multi-form lycanthropes: humanoid, animal and hybrid forms, a shape-change action, involuntary change, and AI form choice.
- A Lycanthropy DiseaseDatabase entry (DC 15 Fort), plus the belladonna, Remove Disease and Remove Curse cures.
- Animate Dead using the registry IDs. It exists only as a spell descriptor and an Adept spell-list entry.
- Shared helpers: add `BaseCreatureDefinitions.HumanWarrior1` (the class holds only Owlbear and Minotaur today; base_human_warrior is duplicated inline in the skeleton and zombie factories, and LycanthropeFactory has its own base_human_warrior_1) and an `ApplyBaseUndeadTraits` helper.
- Use or delete the dead `UndeadTemplateUtils.StripAllSpecialEffects`.

## 5. Dragons

DragonData.cs defines 10 types x 6 ages. The `DragonAgeCategory` enum runs Wyrmling = 1 to Adult = 6, using 3.5e numbering. The 60 IDs are `dragon_<type>_<age>`. Each entry below is CR (sorcerer caster level, if any).

| Type | Wyrmling | Very young | Young | Juvenile | Young adult | Adult |
|---|---|---|---|---|---|---|
| Red | 4 | 5 | 7 (1) | 10 (3) | 13 (5) | 15 (7) |
| Blue | 3 | 5 | 8 | 11 (1) | 13 (3) | 16 (5) |
| Green | 3 | 5 | 8 | 10 (1) | 12 (3) | 15 (5) |
| Black | 2 | 4 | 6 | 8 | 10 (1) | 12 (3) |
| White | 2 | 3 | 5 | 7 | 9 | 11 (1) |
| Gold | 5 | 7 | 9 (1) | 12 (3) | 15 (5) | 17 (7) |
| Silver | 4 | 6 | 8 (1) | 11 (3) | 13 (5) | 15 (7) |
| Bronze | 3 | 5 | 7 (1) | 10 (3) | 13 (5) | 15 (7) |
| Copper | 2 | 4 | 7 (1) | 9 (3) | 11 (5) | 13 (7) |
| Brass | 2 | 3 | 5 (1) | 7 (3) | 9 (5) | 11 (7) |

- **Breath.** Primary breath is a cone (red, green, white, gold, silver) or a line (blue, black, bronze, copper, brass) with the MM energy type. It works only through the Dragon AI, and only once per combat (section 3). The metallic gases (gold weakening, silver paralysis, bronze repulsion, copper slow, brass sleep; 1/1/2/2/3/3 uses per day by age) are data only (AI-008).
- **Other abilities.** Frightful presence from Young Adult works. Wyrmlings have bite and claws only; from Very Young every dragon also has wing and tail slap attacks, whatever its size (the MM gates wings and tail slap by size; as recalled). SR and DR 5 exist only from Young Adult. Every age has its element immunity.
- **Spellcasting.** At spawn, GameManager.NPCSetup injects `ClassLevelEntry("Sorcerer", CL)`, so a dragon is a real spontaneous Sorcerer caster and its `Level` becomes HD + CL. Its spells are the first 6/9/13/16 entries (CL 1/3/5/7) of a cantrip-first thematic list. With the sorcerer spells-known caps, a CL 1 dragon knows only cantrips, and several low-level picks are `IsPlaceholder` spells (CRE-016). `ExecuteDragonTurn` casts first, but only when no more than 2 enemies are adjacent (`DragonAIProfile.MaxMeleeEnemiesBeforeCasting`). After that it repositions for breath, then melees. The intended spell priority is buffs, then dispel, then disable, then damage.
- **Spell-like abilities.** `DragonData.GetSpellLikeAbilitiesByAge` fills per-age `SpellLikeAbilityIds` for 9 types (copper has none). Some are stand-ins: Suggestion is charm_person, Corrupt Water is create_water. Nothing reads them (CRE-015). The template-level `SorcererSpellIds`/`SpellLikeAbilityIds` lists are dead data.
- **Stale preset text.** The Young Gold, Adult Gold and Juvenile Blue encounter presets quote caster levels from before the fix (NPCDatabase_Dragons.cs presets).

**Dragon backlog.**
- Wire SLAs as uses-per-day abilities.
- Select spells by spell level from the sorcerer spells-known table.
- Add ages Mature Adult (7) through Great Wyrm (12).
- Add the missing Ex/Su abilities. This list comes from an older audit and has not been re-checked against the MM: water breathing (green, black, gold, bronze), alternate form (gold, silver, bronze), cloudwalking (silver), icewalking (white), sound imitation (blue), luck bonus and fire aura (gold), speak with animals (bronze, brass), create food and water (bronze), spider climb and stone shape (copper).
- SpellDatabase has no `suggestion`, `speak_with_animals`, `water_breathing`, `stone_shape` or `create_food_and_water`.
- No tests cover dragon spells or SLA data.

## 6. Summoning

The flow, spawn pipeline and lifecycle are covered in the architecture part. In brief:
- **Flow.** Choose the spell, then the list level and creature (`ShowSummonCreatureSelectionMenu`), then the tile, then `PerformSummonMonsterCast`.
- **Spawn.** `SpawnSummonedCreature` clones the NPCDatabase entry, clears `AppliedTemplateIds`, adds `option.TemplateId` and runs `ApplyTemplatesClone`.
- **Lifecycle.** `RegisterActiveSummon` registers the summon; `TickSummonDurations` runs from `OnNewRound`; `DespawnSummonWithEffect` removes it.
- **Spell-level detection.** It uses the regexes `summon_monster_(\d+)` and `summon_natures_ally_(\d+)` in `SummonMonsterLists`, so the class alias IDs match automatically.

| Spell | Data | List size (code / PHB) | Missing vs PHB list |
|---|---|---|---|
| Summon Monster I | `IsPlaceholder = true`, fixed range 5 squares, Wizard and Cleric, EffectType Buff | 12 / 14 | celestial porpoise, fiendish octopus (octopus exists) |
| Summon Monster II | `IsPlaceholder = true`, range 5, ClassList Wizard only, Buff | 10 / 12 | fiendish squid, fiendish Medium shark. The code uses a Large monstrous scorpion where the PHB lists Medium (as recalled) |
| Summon Monster III | Close range, Buff | 19 / 19 | none (Small elementals count as 4) |
| Summon Monster IV | Close range, Buff | 21 / 22 | fiendish Large shark (large_shark exists). The 10 mephits count separately |
| Summon Monster V-IX | none | V is an empty list | everything (CRE-019) |
| Summon Nature's Ally I | Druid 1, Ranger 1; EffectType Summon | 7 / 8 | porpoise |
| Summon Nature's Ally II | Druid only | 11 / 13 | Medium shark, squid |
| Summon Nature's Ally III | Druid only | 11 / 11 | none |
| Summon Nature's Ally IV | Druid only | 16 / 19 | sea cat, Huge shark, juvenile tojanida |
| Summon Swarm | Close range, Buff | bat, rat or spider swarm | Concentration + 2 rounds; IndiscriminateSwarmAI; cannot be commanded or dismissed |

All missing entries are aquatic. The SNA gaps are CRE-021. Octopus and large_shark already ship as land-walkers whose "Aquatic" tag nothing reads, so adding the others needs a design decision (filter them out when no water is present, or follow that precedent) rather than a swim system. The PHB entries are from memory of PHB Chapter 11; check them against the book.

**Rules as implemented.**
- **Count.** A creature from the spell's own level gives 1; one level lower gives 1d3; two or more lower gives 1d4+1. This matches the PHB.
- **Duration.** 1 round per `caster.Stats.Level` (character level, not caster level), minimum 1.
- **Placement.** Range scales with `GetRangeSquaresForCasterLevel`. Space is checked with `Grid.CanPlaceCreature`. Extra creatures go to `FindBestAdditionalSummonCell`, or stack on the first tile if there is no room.
- **Initiative.** The summon is inserted right after the caster, with the caster's initiative.
- **Alignment.** `SummonMonsterOption.IsAvailableTo` restricts **only clerics**, to creatures within one alignment step (`AlignmentHelper.IsWithinOneStep`, which also allows diagonal steps, CHR-013). As recalled, the PHB instead bars clerics and druids from casting spells whose alignment descriptor opposes theirs. The druid half is not enforced; only the unicorn sets `SummonedCreatureAlignment` among SNA entries. The spawned creature's alignment comes from `SummonedCreatureAlignment` (fallback NG/NE/TN), and it matters for smite and for Protection from Evil's summoned-contact barrier (`SpellCaster`). Dismissal (`TryResolveDismissalSpellEffect`) does not read alignment.
- **Augment Summoning.** `FeatManager.ApplyAugmentSummoningBonuses` adds +4 to `BaseSTR` and `BaseCON`, which the ability modifiers do not read, so a summon gets only the +2 HP per HD (CHR-009).
- **Control.** Allied summons are spawned controllable (`ConfigureTeamControl(team, controllable: alliedToPlayer)`) and take full player turns. The right-click menu (`ShowSummonContextMenu`) sets `SummonCommandType.AttackNearest` or `ProtectCaster`, or dismisses the summon. Commands are refused for Summon Swarm and non-controllable summons, so in practice they do nothing (CRE-032). `AI_SummonedCreature` (GameManager.NPCTurns.cs) runs only for non-controllable summons. It targets by command via `AIService.SelectSummonTarget`, retreats at 30% HP or below, advances, trips, smites once and attacks. Its smite (`TryExecuteSummonSmiteAttack`: +CHA mod +2 to hit, +Level +2 damage) differs from the 3.5e values (+CHA mod to hit, +HD damage) in `GameManager.ExecuteTemplateSmiteAttack`, Combat/Special/TemplateSmiteSystem.cs (a GameManager partial, not a class).

**Known summoning issues.**
- Aliases overwrite badger, giant_bee, riding_dog, owl and raven, so SM I and II and SNA I's owl get the wrong stat blocks (CRE-003). The wolf alias is fine; wolf_pack_hunter matches the MM wolf.
- `large_viper` (SM III, poisonous) and `viper_large` (SNA III, poison as text only) are the same creature (CRE-022).
- Despawned summons pile up in `NPCs` (CRE-006). The Lion's Shield lion uses the orphaned `SummoningService` (CRE-005).
- The Ring of Djinni Calling only sets a flag. Bag of Tricks, elemental gems and figurines are data and tooltips only. No item summons are live.
- **NPCs cannot summon.** `AISpellcastingStrategist` scores summon spells, but `SpawnSummonedCreature` is reachable only from the player targeting flow. Summon Monster's EffectType is Buff, not Summon, so the strategist's `EffectType == Summon` checks miss it anyway.
- **Smaller problems.** The selection dialog says "SUMMON MONSTER" for SNA too. Rangers get only SNA I (the PHB gives SNA I-IV). Staff stubs use `summon_natures_ally_vi`/`summon_monster_ix`, which do not match the regex. Fire elemental burn and unicorn SLAs are text only.
- **Tests.** `summon_monster_test` preset; Tests/Magic/SummonMonsterAlignmentRulesTests.cs and SummonMonster3CreaturesTests.cs. There are no SNA tests. Tests read `_activeSummons` by reflection, so renaming that field breaks them silently.

## 7. NPC classes and NPCs by level

### The five DMG NPC classes

All five are in Character/Classes/NPC/ and registered in `ClassRegistry` after the 11 PHB classes. The DMG reference values in the table are from DMG Chapter 4 as recalled; re-check before changing code.

| Class | HD / BAB / skill pts | Good saves (code) | Deviations from DMG |
|---|---|---|---|
| Adept | d6 / 1/2 / 2 | Will (correct) | 60 spells in `AdeptSpellList`, missing 7 first-level spells (cause fear, command, comprehend languages, detect chaos/evil/good/law). Spells per day exist (`AdeptClass.GetSpellsPerDay`, -1 = no access), but SpellcastingComponent has no Adept support, so an Adept has a caster level and no spells (CHR-024). The familiar is text only, and the text says 1st level (DMG: 2nd). Wrong class skill list |
| Aristocrat | d8 / 3/4 / 4 | Will | Class skills differ (Search added, most missing). InfoText claims a bonus feat (never granted) |
| Commoner | d4 / 1/2 / 2 | none | Missing Handle Animal, Ride, Use Rope (and Craft, Profession); Search added |
| Expert | d6 / 3/4 / 6 | Reflex **and** Will (DMG: Will only) | A fixed 36-skill set instead of 10 chosen skills; bonus-feat claims in InfoText |
| Warrior | d8 / full / 2 | Fort | Missing Handle Animal, Ride. InfoText and `WarriorClass.HasBonusFeat` claim a 1st-level bonus feat (never granted; the DMG gives none) |

- **No proficiencies.** `CharacterStats.HasSimpleWeaponProficiency`, `HasMartialWeaponProficiency` and the armor checks list only PHB classes, and monster NPCs have no race. So every Warrior, Aristocrat, Expert, Adept or Commoner wielding a manufactured weapon is non-proficient (-4 to hit through `GetWeaponNonProficiencyPenalty`) and takes armor check penalties to attack. This includes weapon-using placeholder-Warrior monsters such as goblins and orcs (334 runtime entries carry Warrior). Skeletons and zombies fall back to Fighter and escape it. Traced statically.
- **Unreachable for players.** NPC classes are hidden in character creation and stripped from level-up. They reach characters only through `NPCDefinition.CharacterClass`, dungeon class application, and test-only template spawns.
- **Class flags rarely matter at spawn.** NPC spawns set `UseCreatureTypeProgression`, so the class save flags only affect non-NPC characters (CRE-004).
- **Inconsistent name checks.** NPC-class detection is duplicated: `ClassAssociationRules.IsNPCClass` (case-sensitive), `CharacterStats.HasNPCClass`, and two sets in the UIs.

### Class levels on monsters

`CreatureClassEngine.ApplyClassToDefinition` and `CRCalculator` are described in the architecture part. Status against the DMG/MM rules:

| Rule | Code | Status |
|---|---|---|
| Racial and class BAB and saves stack | Spawn recomputes them from creature type x total HD | Missing (CRE-004). Example: Ogre Barbarian 3 gets BAB +5 and Fort +5 (RAW +6 and +7) |
| CR: associated class +1 per level; nonassociated +1/2 per level up to the racial HD, then +1 (MM, "Improving Monsters"; as recalled) | `CRCalculator.CalculateCRAdjustment`, with association by creature type (`ClassAssociationRules`) | Partial. Integer division means one nonassociated level adds 0. The base CR is rounded (CR 1/4 becomes 0). NPC classes always count as nonassociated |
| NPC-class NPC CR = level - 1; PC-class NPC CR = level (DMG Chapter 4; as recalled) | `CalculateNPCCR` (NPC class level/2, PC class level-1), `GetStandardCR` (NPC class (level+1)/2 with level 1 = 0, PC class level-1) | Deviates, and only tests call either |
| Feats at HD 1, 3, 6, 9...; ability increase every 4 HD; monsters get no x4 skill points | Helpers exist (`FeatsFromTotalHD` uses the wrong formula, `1 + (HD-1)/3`); `CalculateClassSkillPoints` always applies x4 | Not applied at spawn |
| Level adjustment and ECL | `CharacterStats.ECL`/`LevelAdjustment` exist, never assigned | Missing (CHR-028) |
| Class spells on monster casters | none | Missing. An "Ogre Wizard 4" spawns with no spells but caster AI (`DungeonEncounterSpawner.UpdateAIForClass`) |
| HP | First class die maxed, CON included, then CON added again by the constructor | Double-counted |

These test vectors pass against the current CRCalculator with creature type "Giant": Ogre (CR 3) + Barbarian 2 = 5; Ogre + Wizard 4 = 5; Troll (CR 5, 6 HD) + Wizard 8 = 10. They are not yet in the test file.

### Class features on NPCs

NPCs with PHB class levels share `CharacterStats` with PCs: human_monk_3/5/7, human_paladin and human_paladin_3/5/7, human_cleric, orc_berserker, the lich, the barbarian, rogue, ranger and cleric test NPCs (test_barbarian_gust, neutral_bandit_test, ranged_test_archer_*, evil_acolyte_test, ...), and DMG-table "Race Class N" spawns, where `CreatureClassEngine.ApplyClassToDefinition` sets `CharacterClass` and `Level`. The constructor creates one `ClassLevelEntry(CharacterClass, Level)` and runs the class's `InitFeats`, so `IsMonk`, `GetClassLevel("Rogue")` and similar checks see the NPC as a member of the class. BAB and saves still follow the creature type, not the class (CRE-004). Whether a feature works on an NPC depends on what reads it:

- **Apply automatically** (formula features read in shared resolution):
  - Monk AC bonus (`MonkACBonus`, in the AC total) and Still Mind (only in `SpellCaster`'s save modifier, CHR-018). Monk features use fixed low-level values (CHR-003).
  - Fast movement for monks and barbarians (`EffectiveSpeedFeet`).
  - Evasion for monk 2+, rogue 2+ and ranger 9+ (`SpellSaveResolver`).
  - Sneak attack on a rogue's hit while flanking or against a target denied Dex. No creature is ever flat-footed at combat start (CMB-028).
  - Class bonus feats from `InitFeats`, plus the definition's `Feats`, through `FeatManager.ApplyPassiveFeats`.
  - Spellcasting, but only when the definition lists `KnownSpellIds` or `PreparedSpellSlotIds`. human_cleric and the paladins list none and cast nothing. The lich lists spells but prepares none (see [What enemy AI can and cannot use today](#what-enemy-ai-can-and-cannot-use-today)).
- **Never apply:**
  - Features activated through PC-only executors: Rage and Flurry of Blows (PC buttons, CHR-003), Turn Undead (a PC menu flow, AI-054), domain powers (CHR-023) and Bardic Music (CHR-026). Stunning Fist cannot be armed by anyone (CMB-022). See the [action coverage matrix](AI.md#9-action-coverage-matrix).
  - Data-only features: Smite Evil, Lay on Hands, Aura of Courage, Divine Health, Favored Enemy and Wild Shape (CHR-020). Divine Grace is added to no save (CHR-018).
  - The paladin NPCs' `SpecialAbilities` strings ("Smite Evil 1/day", "Divine Grace", "Aura of Courage") are display text only.

### NPCs by level (DMG Chapter 4 style)

Character/Templates/TemplateData.cs holds 70 `NPCTemplate` stat blocks: 11 PHB classes at levels 1/5/10/15/20, and 5 NPC classes at levels 1/5/10.
- **Content.** Every template has abilities, HP, AC, BAB, saves, feats and named equipment. `Skills` is empty in all 70. Only Wizard 5 lists `SpellsPrepared`; `SpellcastingTemplate` has no spells-known field at all, so spontaneous casters get no spell list.
- **Provenance unverified.** The values cite "DMG Chapter 4", but the research they were built from lacked the official DMG stat blocks, so treat the numbers as derived.
- **CR storage.** `ChallengeRating` is an int, so the level-1 NPC-class templates store 0.

### What is reachable from gameplay

| Path | Reachable | Notes |
|---|---|---|
| Hand-written class NPCs (human_monk_3/5/7, human_paladin_3/5/7, human_warrior, human_commoner, dwarf_warrior, ...) | Yes: presets, random generator, DMG table name map | Spawn with Humanoid progression, not class progression (CRE-004) |
| DMG dungeon-table "Race Class N" rows -> `DungeonEncounterSpawner.BuildSpawnDefinition` -> `CreatureClassEngine` | Yes (DMG tables button) | The only production use of the class engine. Leaks `spawn_*` IDs (ENC-004) |
| `QuickSpawnSystem`, `NPCTemplateDatabase`, `NPCTemplateAIConfigurator`, `EquipmentAssigner`, `StatArrayApplier`, `ECLTracker` | No (tests only) | CRE-020, CRE-028, CRE-029, AI-009 |

`NPCTemplateSystemTests` has asserts that fail against the current code: feat counts, skill points x4, `GetStandardCR("Fighter", 10) == 10`, wealth at level 1, and the Fighter 20 template CR. In some of these the test, not the code, matches the DMG. Decide which side is right one assert at a time.

## 8. Gaps and backlog toward MM/DMG fidelity

Ordered by impact on "fights play like 3.5e".

1. **Spawn correctness first.**
   - Copy alignment at spawn (CRE-002).
   - Stop double templating (CRE-001).
   - Delete the overwriting summon aliases (CRE-003).
   - Stack class BAB and saves on class-leveled creatures (CRE-004).
   - Give NPC classes their weapon and armor proficiencies.
   - Change Undead to Poor BAB (CRE-009).
   - Fix celestial and fiendish to MM values (CRE-008).
2. **Make existing ability data execute.**
   - Breath recharge, and breath for non-dragon archetypes.
   - Metallic gases (AI-008).
   - Stench and grapple blood drain (CRE-031).
   - `BonusElementalDamage*`.
   - Swarm damage dice and poison (CRE-007, CRE-014).
   - Engulf (CMB-024).
   - Terrain manipulation, which needs movement cost first (CRE-013).
   - Regeneration with energy suppression and nonlethal conversion, plus a separate fast-healing field.
3. **A real spell-like-ability model.** Add uses per day, at will and caster level. Then wire the dragon SLAs (CRE-015). Give the 14 caster-archetype MM creatures their MM spell-likes. Fix the vampire's Fighter class (CRE-030, AI-002, AI-004).
4. **Missing mechanics with many users:** constrict, swallow whole, gaze as a proper mechanic, rock throwing and catching, fly, swim and burrow movement modes, light sensitivity, trample (CRE-018), and incorporeal beyond the miss chance (CMB-025).
5. **Content gaps for CR 1-10 encounters:** the missing MM list in section 1 (constructs, fey, Large/Huge elementals and the sphinxes are the biggest holes), hydras with 6, 8 and 10 heads, the aquatic summons (CRE-021), SM V-IX and SNA V-IX tables (CRE-019), and ages above Adult.
6. **Random-encounter hygiene:**
   - Give every real MM creature a CR, including the animals in the custom file.
   - Stop the "SummonBase" tag from excluding real creatures from `RandomEncounterSystem`.
   - Keep test NPCs (xp_pinata_goblin) out of the pool.
7. **Templates:** multi-form lycanthropes and the curse (CRE-011), and the ghost, vampire, lich, half-dragon, half-celestial and half-fiend templates (half-dragon is already emitted by the DMG table parser, ENC-005).
8. **NPC generation (DMG Chapter 4):**
   - Wire `QuickSpawnSystem` and the 70 templates into encounters, with skills, spells and real equipment (CRE-020).
   - Apply feats, ability increases and LA/ECL in `ApplyClassToDefinition`.
   - Fix the NPC-class CR formulas.
   - Implement Adept spellcasting.
9. **Data safety:** add a static NPCDefinition validator (CRE-026), warn on duplicate IDs (CRE-022), and deep-copy everything in `Clone` and the adapters (CRE-023).

### What enemy AI can and cannot use today

This is the creature-side input for AI work. Routing and profiles are in [AI.md section 6](AI.md#6-profiles-and-archetypes), spellcasting in [section 7](AI.md#7-spellcasting-ai), and the per-action status in the [action coverage matrix](AI.md#9-action-coverage-matrix).

- **The AI uses:** melee and full attacks; improved grab, grapple, pounce, rake and trip; charge; dragon breath (once), casting and frightful presence; auras and gazes (on the owner's turn); mouther spittle and engulf; bombardier spray; swarm movement and damage; dragon spells, single-target only, because NPCs never cast area spells (AI-001).
- **The lich never casts.** Its 16 `PreparedSpellSlotIds` are matched to slots by index (SPL-015). Wizard slots stop at 2nd level (SPL-041), so the lich has 4 cantrip, 4 first-level and 3 second-level slots, and its list starts at 1st level. Each of its first 11 entries lands in a slot of another level and is rejected, the other 5 have no slot, and auto-preparation is skipped because explicit data was given. It spawns with no prepared spells, even though `LichAIProfile` (Ranged) routes it to a casting routine.
- **The AI never uses:** class features that need activation (see [Class features on NPCs](#class-features-on-npcs)), metallic gases, non-dragon breath weapons, stench, SLAs, any creature summoning (including the MM "summon" abilities of mephits, demons and devils), consumables (AI-009), constrict or swallow, terrain effects, shape changes, or flight.
- **Archetype gaps:**
  - 29 Brute and 2 Caster creatures get no AI profile (AI-004). Among them are ankheg, chimera, gorgon, hydras and wyvern, which are the creatures whose abilities most need tactical use.
  - `NPCAIBehavior.Ranged` is unhandled (AI-003).
  - gelatinous_cube, ochre_jelly, gauth and thoqqua have archetype None, so they run the profile-less routine.
- **Abilities are hard-coded priorities.** Each is a fixed step in the AIService turn routines, not a scored option, so adding a new ability means editing every routine that should use it (AI-020). The open design questions in section 3 apply.
