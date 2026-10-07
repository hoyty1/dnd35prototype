> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-07.

# Tactics inventory and action-catalog design

An inventory of every combat option in the level 1-8 scope (actions, maneuvers, class features, PHB spells of levels 0-4 plus 5th for enemy casters, and the abilities of every creature the DMG dungeon tables and the Summon Monster/Nature's Ally I-IV lists can produce), each with its code status, whether PCs and the enemy AI can use it, and a tactical model. It feeds step 3 (the action catalog) of the AI roadmap in [`../../designs/enemy_ai_knowledge_and_personalities.md`](../../designs/enemy_ai_knowledge_and_personalities.md). Start AI work at [`../AI.md`](../AI.md).

| File | Contents | Entries |
|---|---|---|
| [ACTIONS.md](ACTIONS.md) | Combat actions, special attacks and maneuvers, class features | 125 |
| [SPELLS_0-2.md](SPELLS_0-2.md) | Spells whose lowest level is 0-2 | 204 |
| [SPELLS_3-5.md](SPELLS_3-5.md) | Spells whose lowest level is 3-5 | 205 |
| [CREATURES_L1-4.md](CREATURES_L1-4.md) | Dungeon-table creatures first met at levels 1-4, plus their abilities | 93 |
| [CREATURES_L5-8.md](CREATURES_L5-8.md) | Dungeon-table creatures first met at levels 5-8, plus their abilities | 99 |
| [SUMMONS.md](SUMMONS.md) | Summon Monster and Summon Nature's Ally I-IV creatures | 118 |

## How to use it

- **Planning AI work:** pick P1 rows whose AI status is `no` or `partial`; the "AI use today" cell names the blocking issue IDs. The backlog in section 2 below orders them.
- **Building the action catalog:** the "When smart", "Bad when / counters" and "Value model" cells are the seed data for each catalog entry's AI metadata; section 1 below proposes the C# types.
- **Personalities:** creature rows suggest a blend of the traits in section 4.4 of the design doc.
- **Trust:** this is an agent-written inventory checked by sampling (spells about 1 in 8, actions 1 in 3, creatures 13 of 310 in depth). The code is the source of truth; grep before relying on any status. Nothing was verified in Play mode.
- **Rules sources:** the SRD at d20srd.org and the owner's PHB, DMG and MM (cited as book + printed page; summarized, never quoted).
- **Regenerating:** the raw JSON and the assembly script are local work files (not in git). If they are gone, re-run an inventory before editing these tables by hand, or edit rows directly as code changes.

## Sampled verification

Corrections applied: 37 (marked ✔). Corrections that matched no entry: 0. Systematic errors the verifiers reported, which likely affect unchecked rows too:

- *spells:* pcUsable overstates spells that only Clerics or Druids get at 3rd level and up. Cleric known spells load only levels 0-2 (SpellcastingComponent.cs:744-749), domains cover 1-2, and Druid slots stop at 2nd (SPL-041), so no PC can prepare these spells. A script cross-check of all 409 entries against the DB class availability found 29 such entries marked yes/partial. The ones marked 'yes' are inflict_serious_wounds, magic_vestment, remove_blindness_deafness, searing_light, summon_natures_ally_3, divine_power, imbue_with_spell_ability, inflict_critical_wounds and unholy_blight; the rest are marked 'partial', e.g. prayer, neutralize_poison, death_ward, restoration, insect_plague, wall_of_thorns. Set them to 'no' (or 'partial' only when a scroll or wand path is the stated reason). Wizard-only 3rd+ spells are fine, because Sorcerer mirrors the Wizard list and has a full slot table.
- *spells:* codeStatus 'implemented' is given whenever a bespoke handler exists, even when the entry's own codeDeviations list missing core RAW mechanics: Hold Monster (no per-round save), Hide from Undead (single target), Enervation (no undead branch), Mage Armor (Self only). Similar gaps elsewhere get 'partial' (e.g. Vampiric Touch). Expect over-rating across the 104 'implemented' entries. Re-grade them by this rule: 'implemented' only when the deviations are minor.
- *spells:* codeStatus 'placeholder' means 'no real effect', not the SpellData.IsPlaceholder flag. 20 entries are 'placeholder' with IsPlaceholder=false, among them persistent_image, command, death_ward, heat_metal, telekinesis, wall_of_thorns, continual_flame and levitate. Summon Monster I/II have the flag set but are marked 'implemented' (SPL-021). The assembled docs should define the status by behaviour and not cite the flag as evidence.
- *spells:* SR omissions are under-reported. SpellResistanceApplies defaults to false (SPL-057), and many entries for generically resolved spells with RAW SR Yes do not say that SR is never checked (sampled: Sound Burst, Inflict Serious Wounds; Chill Touch did note it). Audit every SR-Yes spell's DB block for the flag.
- *spells:* Inconsistent lich claims. AI.md:397/471 and SPL-041 show the lich (Wizard 11) casts nothing: its Wizard slots stop at 2nd level and its prepared list is mis-slotted. Most entries say so (Chill Touch, Mage Armor, Ray of Enfeeblement, Vampiric Touch), but Hold Monster says 'Liches cast it'. Grep the unchecked entries for 'lich' and 'vampire' (CRE-030) and correct any claim that they cast.
- *spells:* aiUsable and pcUsable labels are applied inconsistently. Ray of Enfeeblement, Enervation and Hold Monster get aiUsable 'yes' while Acid Splash, Disrupt Undead and Chill Touch get 'partial' with the same routing and no in-scope carrier. Placeholder spells split between pcUsable 'no' (Mage Hand, Obscure Object, Arcane Lock, Knock) and 'partial' (Delay Poison, Erase, Whispering Wind). Wall of Ice uses aiUsable 'partial' for reacting to the spell, not casting it. Normalize the rules: aiUsable = the NPC executor gives a real effect (yes), a degraded or bugged effect (partial), or none (no); carrier availability goes in aiUseNotes.
- *spells:* The Area/AI-001 handling is reliable. In the full cross-check, every entry whose DB TargetType is Area has aiUsable 'no', except Wall of Ice, which is 'partial' for reacting. codeStatus 'missing' is also reliable: name-matching every 'missing' entry against DB SpellData found no false 'missing'.
- *spells:* Inventory ids are spell names, not DB SpellIds, in some cases: summon_monster_ii/iv vs summon_monster_2/4, summon_natures_ally_ii/iv, magic_weapon_greater, black_tentacles, reduce_person_mass, hideous_laughter, see_invisibility, soften_earth_and_stone. Add a dbSpellId mapping before building the catalog. rulesSummary text matched the PHB in every sampled entry except the Persistent Image component note; no wrong dice, ranges, saves or durations were found.
- *actions:* Duplicate rows from two merged batches (generic actions and class features) describe the same feature with slightly different refs: class_feature_turn_undead (35) vs cleric_turn_undead (89); class_feature_rebuke_undead (36) vs cleric_rebuke_command_undead (90), which also covers command_undead (37) and bolster_undead (38); feat_stunning_fist (70) vs monk_stunning_fist (105). Merge them before assembling the docs.
- *actions:* Code reached through GameManager accessor wrappers (Combat_*, *ForAI) was under-searched, so 'no PC path' or 'no AI path' claims can be wrong. Example: ability_trip_on_hit missed four CombatFlowService calls to Combat_TryResolveFreeTripOnHit. Grep the private method name and its Combat_/ForAI wrapper before claiming a path is missing.
- *actions:* Issue IDs copied from AI_ACTION_COVERAGE.md are sometimes the wrong reason. Rapid Shot/TWF cite AI-026 (unread profile flags; no Rapid Shot flag exists) and CMB-008 (off-hand STR bug); the real cause is that no NPC caller exists. The coverage matrix rows 25 and 32 carry the same mis-citation.
- *actions:* Creature lists for monster abilities were written from memory or partial greps and are incomplete (pounce: 6 listed, 9 in data, including a non-RAW blue_slaad). Generate the lists by grepping the flag in Character/Creatures.
- *actions:* The provokesAoO column paraphrases instead of following PHB Table 8-2 (Aid another = Maybe, Run = Yes). Take the action-itself value from Table 8-2 and add the movement caveat separately.
- *actions:* Spot check of about 45 file:line refs across the sample: nearly all resolve within a few lines. The main slip is refs to a block inside a method rather than the method declaration (e.g. 'ExecuteCharge:1545' when the method is at :1436), and NPC-side twins such as NPCExecuteCharge are often omitted. codeStatus, pcUsable and aiUsable were otherwise correct in 40 of 42 sampled rows, and the rules summaries matched the PHB/DMG/MM except the Large reach nuance.
- *creatures:* Sample: 13 of 62 creatures (every 5th: dire rat, hell hound, wererat, celestial lion, flesh golem, eight-headed hydra, Large monstrous spider, vampire, fiendish Medium centipede, small water elemental, water mephit, hippogriff, Medium fire elemental). In these entries npcId and the stat-block deviations held up against both the code and the MM text, including vampire Fighter 5 stats, feats and DR silver-and-magic (MM p.250-251), water mephit Con 13 / 19 hp / Power Attack and Toughness (MM p.185), dire rat swim 20 ft (MM p.64), the hydra's 8-head to 7-head mapping (DungeonEncounterTableManager.cs:775-778), and centipede id override order (C registers before M, and Register overwrites).
- *creatures:* codeRefs line numbers often drift by 2-8 lines from the cited code (Large spider, flesh golem, Medium fire elemental). The cited files and symbols are right, but line-anchored links will land on the wrong line.
- *creatures:* Scent is classified inconsistently: most entries say partial/partial (AIService targeting adjustment), but the hell hound entry says data_only/no. HasScent is copied at spawn (NPCSetup.cs:514) and read by AIService.cs:1935, so partial is correct everywhere.
- *creatures:* Immunity gaps are sometimes judged only from the CreatureImmunities flags. CharacterStats also grants mind-affecting immunity by type (undead, construct, ooze, plant, vermin), poison immunity (+elemental) and disease immunity. Sleep immunity follows from mind-affecting immunity (SpellUtilities.IsImmuneToSleepEffects). Entries that say sleep is not flagged for constructs or undead overstate the gap. For elementals the gap is real, because elementals are not mind-affecting-immune by type.
- *creatures:* Class-feature claims for templated or derived NPCs should check NPCDefinition.CharacterClass, not the base-definition Id or comments. The wererat's 'Rogue 1' exists only in an Id string and comments.
- *creatures:* New finding to file: the summon-AI smite path (GameManager.NPCTurns.cs:206-207) uses its own +2 attack and Level+2 damage formula, which differs from both TemplateSmiteSystem and RAW. No entry in docs/issues covers it.
- *creatures:* New finding to check: 5 definitions (the hydras) use CreatureType "MagicalBeast" with no space. Code that compares against "Magical Beast" (CreatureTemplateFramework.cs:120 Int floor; FavoredEnemyData) will not match them. How favored-enemy matching normalizes the string is not verified.

## Action-catalog design (from the inventory run)

The rest of this file was written by the run's design agent from the full inventory and the verification results. It is a proposal, not a description of the code.

## Action catalog design (tactics inventory, levels 1-8)

> Status (2026-10-07): design only; nothing is built. Inputs: the local inventory work files (125 actions, 409 spells), the monster ability index from the creature stage (ids and creature counts over the 310-entry work list, of which 118 are summon-list entries), and three sampled verification passes. Code claims were checked by grep at `ae06045` on `docs/cleanup-2026-10`. None of this was checked in Play mode. Rulebook references are summaries and printed page numbers only.

---

### 1. Catalog schema

#### 1.1 What the inventory says the schema needs

Each spell and action entry has a `catalogFields` list. Across the 534 entries, these were the most common fields (count), grouped by the schema block they belong to:

| Schema block | Observed fields (count) |
|---|---|
| Duration | duration 128, durationRounds 32, dismissible 51, durationScaling 11, durationRoundsPerLevel 9, permanent 8 |
| Area | areaShape 109, persistentArea 13, friendlyFire 19, multiTarget 13, targetCount 8 |
| Effects | imposesCondition 82, bonusType 40, damageType 19, damage 14, removesCondition 6, healsUndead 7, informationOnly 20 |
| Cost and timing | castingTime 74, actionCost 23, concentration 16, provokesAoO 8, usesPerDay 18, materialCost 14 |
| When to use | outOfCombatOnly 65, preCombatBuff/preBuff/precombatBuff/preBuffable 72, nonCombat 12, downtimeOnly 7 |
| Resolution | srApplies/SR/spellResistance/noSR 134, mindAffecting 50, save/noSave/saveNegates/saveHalf/saveType 74, descriptor 35, touch/touchAttack/requiresTouch 45, opposedCheck 8, blockedByProtectionFromAlignment 10 |
| Targeting | rangeCategory/range/rangeMedium/touchRange 107, needsLineOfEffect 42, selfTargetOnly 39, targetType/targets 46, targetsObject/targetObject 16 |
| Prerequisites | casterClasses 18, featRequired 9, requiresDomain 8, requiresGrappling 7, requiresTerrain 8, sizeLimit 8, livingOnly/requiresLiving 18, languageDependent 7 |

Every entry also has a free-text `valueModel` (expected damage, control value, risk). Those formulas are the starting point for the value-model hooks in 1.2.

#### 1.2 Types (C# 9; plain classes and enums, no `record` or `init` so no IsExternalInit polyfill is needed)

The catalog reuses enums that already exist instead of adding parallel ones: `ActionCostType` (`_Core/Commands/IGameCommand.cs:46`, dormant today), `AoEShape` and `AoETargetFilter` (`Spell/AreaEffects/AoESystem.cs:12`), `SpellRangeCategory`, `SpellDescriptor`, `BonusType`, `DurationType`, `DamageType`, `CombatConditionType` and `SpecialAttackType`/`GrappleActionType` (`CharacterController.cs:12`).

```csharp
// AI/Catalog/ActionDefinition.cs
public enum ActionCategory : byte { Move, Attack, Maneuver, Grapple, Spell, SpellLike, Supernatural,
  Extraordinary, ClassFeature, Feat, ItemUse, Reaction, Passive }
public enum ProvokeRule : byte { No, Yes, Maybe /* PHB Table 8-2 "Maybe" */, MoveOnly }
public enum ResourceKind : byte { None, SpellSlot, SpontaneousSlot, UsesPerDay, UsesPerHour, Recharge, Charges,
  Rounds /* rage */, ItemConsumed, AtWill }
public enum TargetKind : byte { Self, SingleEnemy, SingleAlly, SingleCreature, Object, Point, Square, Area }
public enum AttackKind : byte { None, Melee, Ranged, MeleeTouch, RangedTouch, Ray, NaturalRoutine }
public enum CheckKind : byte { None, AttackRoll, Save, OpposedCheck, CasterLevelCheck, SkillCheck, Automatic }
public enum SaveEffect : byte { None, Negates, Half, Partial, Disbelief, RepeatEachRound }
public enum EffectKind : byte { Damage, Heal, ApplyCondition, RemoveCondition, Buff, Debuff, Summon,
  Reposition, Teleport, CreateArea, Dispel, Information, TempHP, AbilityDamage, EnergyDrain, ActionDenial }
[System.Flags] public enum ActionRole : uint { None = 0, Damage = 1, Control = 2, Debuff = 4, Buff = 8,
  Heal = 16, Escape = 32, Summon = 64, AreaDenial = 128, Information = 256, Opener = 512, Finisher = 1024,
  Mobility = 2048, Defensive = 4096, AntiMagic = 8192 }
public enum UseTiming : byte { AnyRound, PreCombat, Opener, Reactive, Finisher, OutOfCombatOnly, Downtime }
public enum ImplStatus : byte { Implemented, Partial, DataOnly, Placeholder, Missing }  // judged by behaviour, never by SpellData.IsPlaceholder

public sealed class CostSpec {
  public ActionCostType Action = ActionCostType.Standard;  // castingTime / actionCost
  public ProvokeRule Provokes = ProvokeRule.No;            // value from PHB Table 8-2; movement caveat kept separate
  public ResourceKind Resource; public int Uses; public string RechargeDice;  // "1d4" rounds for breath
  public int MaterialCostGp; public bool RequiresConcentration;
  public bool V, S, M, F, DF;                               // silence, grapple, pinned and ASF gates
}
public sealed class AreaSpec {
  public AoEShape Shape; public int SizeSquares;            // Spread, Emanation, Cylinder, Cube and Wall need new AoEShape members
  public AoETargetFilter Filter = AoETargetFilter.All;      // friendlyFire = Filter == All
  public bool Persistent; public int MaxTargets; public int HdBudget;  // sleep, color spray
}
public sealed class TargetingSpec {
  public TargetKind Kind; public SpellRangeCategory Range; public int RangeSquares;
  public AttackKind Attack; public AreaSpec Area;
  public bool NeedsLineOfEffect = true, NeedsLineOfSight;
  public SizeCategory? MaxTargetSize; public string CreatureTypeFilter;  // "living", "humanoid", "undead", "animal"
}
public sealed class ResolutionSpec {
  public CheckKind Check; public SavingThrowType? Save; public SaveEffect OnSave;
  public string DcFormula;            // "10+SL+castMod", "10+HD/2+Con", "fixed:14"
  public string OpposedCheck;         // "grapple", "trip:Str|Dex", "bluff_vs_sensemotive"
  public bool SrApplies;              // explicit for every entry; SpellData's default false is SPL-057
  public bool MindAffecting; public SpellDescriptor Descriptors; public bool BlockedByProtectionFromAlignment;
}
public sealed class EffectSpec {
  public EffectKind Kind; public string Dice; public DamageType Damage; public string ScalesWith;  // "CL:1d6/level max 5"
  public CombatConditionType Condition; public BonusType Bonus; public int Amount;
  public DurationType Duration; public int DurationValue; public bool DurationPerLevel, Dismissible;
  public string SummonListId; public string Notes;
}
public sealed class Prerequisites {
  public string[] Feats; public string ClassName; public int MinClassLevel; public string Domain;
  public bool RequiresGrappling, ForbidsGrappled, RequiresMeleeThreatOnFoe, RequiresAllyEngagingFoe;
  public string RequiresWeaponKind; public string RequiresTerrain; public bool LanguageDependent;
  public string LegalityHookId;       // named predicate for anything not data-expressible
}
public sealed class AIMetadata {
  public ActionRole Roles; public UseTiming Timing; public CognitionTier MinTier;  // from the personality design
  public string ValueModelId;         // registry key, see IValueModel
  public float BaseWeight = 1f;
  public string[] WhenSmart, BadWhen; // closed set of condition ids (Clustered, TargetFlatFooted, SelfThreatened...)
  public string[] Counters, Synergies;
  public bool NpcExecutorReal;        // aiUsable "yes" = real effect on the NPC path; carriers are listed separately
}
public sealed class ActionDefinition {
  public string Id;                   // "spell:sleep", "maneuver:trip", "ability:breath_weapon", "item:drink_potion"
  public string Name; public ActionCategory Category; public string SourceId;  // SpellId / NPC field / ItemID
  public string RulesRef;             // "PHB p.280", "MM p.300"; never quoted text
  public CostSpec Cost; public TargetingSpec Targeting; public ResolutionSpec Resolution;
  public EffectSpec[] Effects; public Prerequisites Prereqs; public AIMetadata AI;
  public string[] Tags;               // "provokes", "area", "touch", "fear", "mind", "ranged", "pre_buff"
  public ImplStatus Status; public string[] IssueIds; public string ExecutorId;
}
// Passive traits (DR, SR, regeneration, scent, auras, on-hit riders) are not choosable actions:
public sealed class TriggeredEffectDefinition {
  public string Id; public string Trigger;  // "OnNaturalHit:Bite", "TurnStart", "OnGrappleCheckWin", "Aura:30ft", "OnDamaged"
  public ResolutionSpec Resolution; public EffectSpec[] Effects; public ImplStatus Status; public string[] IssueIds;
}
```

#### 1.3 How the existing sources map onto the catalog

The catalog is a set of adapters over the existing data, not a second copy of it. `ActionRegistry.Init()` builds the definitions at startup in the same lazy-`Init` style as the other databases.

| Source | Adapter | Notes |
|---|---|---|
| `SpellData` (`Spell/Data/SpellData.cs`) | `SpellActionAdapter` reads TargetType, RangeCategory, AoEShapeType/AoESizeSquares/AoEFilter, EffectType, Damage*, AllowsSavingThrow/SavingThrowType/SaveHalves, SpellResistanceApplies, IsMindAffecting, Descriptors, ActionType, ProvokesAoO, V/S/M/DF, DurationType | It reads the shared database template and never mutates it; metamagic goes on a `SpellData.Clone()` in the context. Add `dbSpellId` aliases for inventory ids that are spell names (summon_monster_ii/iv vs `summon_monster_2/4`, black_tentacles, hideous_laughter, see_invisibility and others). `SpellCategoryClassifier.ReclassifyAll` rewrites EffectType at init (Debuff to Control for many spells; save negation copes via `SpellUtilities.IsEffectNegatedBySave`), so the adapter must run after it, or better, the classifier becomes the adapter's Roles mapping. |
| Maneuvers | One definition per `SpecialAttackType` (Trip, Disarm, Grapple, Sunder, BullRushAttack/Charge, Overrun, Feint, AidAnother, WakeSleepingAlly, CoupDeGrace, TurnUndead) and per `GrappleActionType` | The rules core already exists: `CharacterController.ExecuteSpecialAttack` (:10392) is called by summons (`NPCTurns.cs:160`), and `GameManager.ExecuteSpecialAttack` (`CombatActions.cs:1514`) is the PC wrapper that calls `ShowActionChoices` on every exit. That is the pattern to generalise. |
| Generic actions | move, double move, run, withdraw, 5-ft step, charge, full attack, total defense, fight defensively, stand up, reload, draw | Provokes values follow PHB Table 8-2 (aid another = Maybe, run = Yes); the leaving-a-threatened-square caveat is stored separately (`ProvokeRule.MoveOnly`). |
| Class features | Rage (`CharacterController.ActivateRage` :12099), flurry (:11987), turn undead (`TurnUndeadSystem.ExecuteTurnUndead` :451, private and PC-menu driven), smite, lay on hands, wild shape, bardic music | Data-only features (CHR-020) get `Status = DataOnly` and no executor until they are built. |
| Feats as modes | Power Attack, Rapid Shot, Combat Expertise, Cleave, Manyshot, Spring Attack | Modelled as action modifiers (`Tags: "mode"`) applied to Attack/FullAttack definitions. Today only the PC calls `SetPowerAttack` (`GameManager.cs:9116`), `SetRapidShot` (:9125) and `DualWieldAttack` (`CombatFlowService.cs:1046`). |
| `NPCDefinition` structured fields (`Character/Creatures/NPCDatabase.cs:489`) | `BreathWeapon`/`SecondaryBreathWeapon` become actions with Recharge; `Engulf`, `RangedSpecialAttack`, `TerrainManipulation` become actions; `HasPounce`/`HasRake` become charge and grapple modifiers; `HasTripAttack`, `HasImprovedGrab`, `NaturalAttackDefinition` riders (poison, paralysis, disease, energy drain, ability drain, petrification, blood drain) become `TriggeredEffectDefinition` on hit; `AuraAbility`, `FrightfulPresence`, `StenchAuraDC` become aura triggers; `RegenerationAmount`, DR, SR, immunities and `GainsSmiteEvil/Good` become passives or template actions | Each field gets one definition id. Fields that are set but have no reader become `DataOnly`: `HasTrample` (CRE-018), stench and blood drain (CRE-031), and `BonusElementalDamage*`, which is set on 10 natural attacks across 9 creatures but read nowhere in combat (grep: only `CharacterStats.cs:74-121` and `UndeadTemplateUtils.cs:27`). |
| `NPCDefinition.SpecialAbilities` (free text) | Replace with a new `List<SpecialAbilityEntry> Abilities` (Kind Ex/Su/Sp, CatalogId, UsesPerDay or AtWill, CasterLevel, DcOverride, SpellId for SLAs). The text list stays as display only. | This is the SLA system (CRE-015, CRE-017). It is the largest single missing category: about 100 `sla_*`/`ability_sla_*` ids in the index, all missing. New fields go in `NPCDefinition.Clone` and the templates' `CopyDefinitionFields` (CRE-023). |
| `KnownSpellIds` / `PreparedSpellSlotIds` | These become spell actions through `SpellActionAdapter`, gated by the actor's slots | Useless until ENC-021, SPL-041, SPL-015 and CRE-030 are fixed (section 4). |
| Items | `item:drink_potion`, `item:read_scroll`, `item:use_wand`, `item:throw_splash` wrap the spell definition with Cost Standard, Provokes Yes, Resource ItemConsumed/Charges, CL from the item, and a prerequisite (class list or UMD for scrolls and wands) | Scrolls and wands already enter through `BeginPendingSpellTargeting` (`GameManager.SpellCasting.cs:813`, private and PC-pending). ITM-005 (any stored item costs a full round) must be fixed in the shared core. |

#### 1.4 Shared executor contract (PC UI and AI both call it)

```csharp
public enum ActorSource : byte { PlayerInput, AI, Summon, Reaction, Forced /* confused, charmed */ }
public sealed class ActionContext {
  public CharacterController Actor; public ActionDefinition Def; public ActorSource Source;
  public CharacterController Target; public Vector2Int? Point; public HashSet<Vector2Int> Cells;
  public SpellData SpellInstance;  // a clone when metamagic is applied
  public ItemData SourceItem; public int SourceInventoryIndex = -1;
}
public readonly struct Legality { public readonly bool Ok; public readonly string Reason; /* ctor */ }
public sealed class ActionOutcome { public bool Performed; public ActionCostType Spent; public bool TurnEnded;
  public List<CombatResult> Results = new List<CombatResult>(); }
public interface IActionExecutor {
  string Id { get; }
  Legality CanExecute(ActionContext ctx);                    // pure: rules gates, economy, resources, range, LoE
  IEnumerator Execute(ActionContext ctx, ActionOutcome outcome);
}
```

Contract rules. Each one fixes a coupling that exists today:

1. **No UI flow inside an executor.** An executor never calls `ShowActionChoices`, `EndActivePCTurn` or `AfterAttackDelay`, never reads `_pendingSpell`/`_pendingMetamagic`/`CurrentSubPhase`, and never checks `IsPlayerTurn`/`ActivePC`. These are the AI-054 couplings, verified at `SupportActions.cs:855-904` (Aid Another ends in `ShowActionChoices`/`AfterAttackDelay`), `GameManager.cs:4333` (`TryUseConsumableFromInventory` returns early unless `IsPlayerTurn && ActivePC == actor`), `GameManager.SpellCasting.cs:36-102` (the cast reads `_pendingSpell`) and `CombatFlowService.cs:175,569,597` (`Combat_ShowActionChoices`).
2. **Costs are committed only through `ActionEconomy`** (`Combat/Core/ActionEconomy.cs`), using `Def.Cost.Action` instead of a hard-coded `CommitStandardAction`. This fixes full-round casting being treated as standard (`GameManager.SpellCasting.cs:46-54`). `UseMoveAction` already returns false when no move action is left, and AI routines gate moves on `HasMoveAction`; an executor should check that result instead of moving first.
3. **AoO and Concentration are rolled inside the executor for every source.** Then no pipeline can skip them (SPL-006 lists the ones that still skip some checks). Movement, maneuver and single-target casting AoOs already go through shared helpers (`ResolveMovementAoOsBeforeStep`, `ResolveManeuverInitiationAoOs`, `ResolveThreatenedSpellcast`) that an executor should call.
4. **One spell executor.** The PC single-target path, the AoE path (`PerformAoESpellCast` :3567), the NPC path (`TryNPCPerformSpellCast` `NPCTurns.cs:758`) and the pending path collapse into `SpellExecutor`, which runs the handler chain once. This retires SPL-054 and makes AI-001 (`NPCTurns.cs:787` refuses Area) and SPL-091 disappear by construction.
5. **Wrappers are thin.** `GameManager.RunPlayerAction(ctx)` runs the executor and then does the UI continuation (`ShowActionChoices` or end turn). `AIService.RunAIAction(ctx)` yields the executor and continues the routine. Existing `*ForAI` wrappers (`NPCExecuteChargeForAI`, `NPCExecuteBreathWeaponForAI` `NPCTurns.cs:1430`) become registry entries.
6. **Publish, don't resolve, events.** The executor publishes `AttackResolvedEvent`, `SpellCastEvent` and `DamageTakenEvent` (declared, never published today). Those feed knowledge increment 2.
7. **Tests:** `CanExecute` is pure, so legality tables can be unit-tested without GameManager (the F10 runner pattern).

Migration order: Aid Another, consumables, turn undead, spells (single), spells (area), then the maneuver wrapper, which already follows the pattern.

#### 1.5 How weighted personalities use the catalog

- **Candidate generation.** `ActorActionSet.Build(actor)` lists `ActionDefinition`s whose prerequisites hold and whose resources remain. Per turn, `CanExecute` is called for each candidate × target (or placement) pair.
- **Scoring, in hit-point equivalents** (design doc §7 phase 6, moved earlier because the catalog makes it cheap):
  `Score(a, t) = IValueModel[a.AI.ValueModelId].Estimate(ctx, view) × RoleFit(a.AI.Roles, Role) + K·Σ_traits w_i·TagOpinion_i(a.Tags) − RiskAversion·Risk(a, ctx) + ConsiderationTerm(t)`.
  `IValueModel` ids: ExpectedDamage, AreaDamage (Σ over enemies − friendly-fire penalty), ControlValue (P(fail save) × rounds × target threat), Heal, BuffDelta (Δhit × expected attacks × rounds), Summon, Escape and Dispel. Their inputs come from `ITargetView`, never `target.Stats`, so the same models work in Omniscient and Rules mode.
- **Traits gain one row.** `TraitDefinition.ActionTagWeights: (string tag, float w)[]`. Examples: Cowardly `provokes −, ranged +, escape +`; Sadistic `coup_de_grace` Insist (via the existing Opinion quorum); Caster `area +, control +`; Berserker `charge +, rage +`.
- **Gating order follows design §4.2:** (1) `CanExecute`; (2) `AI.MinTier` against `CognitionTier` (for example, "dispel a known buff" needs Keen); (3) data overrides; (4) quorum veto on tag opinions; (5) argmax with stable tie-breaks.
- **Data shape.** `WhenSmart`/`BadWhen` are condition ids from the same closed set as `OpinionRule` conditions (Clustered, SelfThreatened, TargetFlatFooted, AlliesDownFraction), so the inventory's whenSmart/badWhen text becomes data, not code.
- **Personality-free NPCs keep the legacy routine.** The catalog evaluator is opt-in per NPC until trace parity is shown, as in design §5.3.

---

### 2. Implementation backlog (levels 1-8)

The order is frequency (creatures or entries affected) × AI value × size of the gap. It matches the agreed AI roadmap order: rules fixes, then catalog, then personalities, then spell parity, then MM specials.

| # | Work item | Frequency | AI value | Gap | Issues |
|---|---|---|---|---|---|
| 1 | Shared-action rules fixes: maneuver math, morale on full attacks (NPC movement, maneuver and casting AoOs, casting Concentration and standing up from prone are done) | every encounter | High: legality and risk models are meaningless without them | High | CMB-014, CMB-002/CMB-043 |
| 2 | Executor split plus `ActionRegistry` skeleton (attack, full attack, charge, maneuvers, single-target spell, consumable, aid another, turn undead) | all | High: the precondition for everything below | High | AI-054, SPL-054, ITM-005 |
| 3 | Enemy spell carriers: DMG class casters get spells; slot and known-spell caps; mis-slotting; vampire as Fighter; stale placeholder flag on SM I/II | every caster spawn (max NPC CL in tables is 5) | High | High | ENC-021, SPL-041, SPL-015, CRE-030, SPL-021, CRE-015 |
| 4 | NPC area casting through the shared AoE core; true burst resolution | 77 in-scope entries cite AI-001 (62 Area spells in the DB) | High: sleep, color spray, web, fireball | High | AI-001, SPL-046, SPL-042 |
| 5 | NPC-path effect parity: summon, dispel and escape do nothing; Prayer, Magic Vestment and Magic Weapon have no NPC handler; Hold Person/Monster have no per-round save (new); SR flags missing | about 30 P1/P2 spells | High | Med | SPL-091, SPL-057, new SPL (hold) |
| 6 | Grapple family as triggers: Improved Grab (49, works), **Constrict (22, text only in 10 creature files)**, Rake (regrade, see section 3), grapple blood drain | 49+22+9 | High | Med | CMB-014, CRE-031, CMB-075 |
| 7 | Movement modes: fly 42 (+ fly variants), climb 10+2, swim 3+2+1, burrow 4, earth glide 6. Prerequisite for flyby, ink-cloud escape and burrow-ambush AI | 60+ | High (positioning) | High (no fly/swim/climb speed exists on `NPCDefinition`) | file new GRID/CRE issue; AI-019 |
| 8 | SLA system (`SpecialAbilityEntry`) feeding spell actions | about 100 SLA ids (charm person, darkness, invisibility, stinking cloud, suggestion...) | High | High | CRE-015, CRE-017 |
| 9 | Poison and disease riders: missing poison ids (Dex poisons, 14), swarm poison, real-time timing | 25+14+5 | Med | Med | CMB-007, CRE-014 |
| 10 | Fast healing as its own field (not regeneration) and the defeat check | 22 | Low (AI: n/a) | Med | CRE-017, CORE-034 |
| 11 | Smite (template and paladin): one executor; target alignment; the summon-AI formula | 22+8+11+4+3 | Med | Med | CMB-021, CMB-003, CRE-002, CHR-020, AI-056 |
| 12 | Breath weapons: action cost, saves, recharge, mephits, metallic secondaries | 19 (+2 secondary) | High | Med | AI-040, AI-032, AI-008, CRE-017 |
| 13 | Senses for fair sight: scent (47, partial), tremorsense 15, blindsense 6, blindsight 5, all-around vision 5 | 78 | Med (knowledge increment 2) | High | AI-051, AI-007 |
| 14 | Template defences: SR HD+5 at all HD; DR bypass combos; energy riders on natural attacks (data-only); trample | 14+53+4+2 | Low | Med | CRE-008, CRE-018, new CRE (riders) |
| 15 | Feat modes for NPCs: Power Attack (profile flag unread); Rapid Shot and TWF (no NPC caller); Cleave; natural attacks plus weapon | many humanoids | Med | Med | AI-026, new AI (Rapid Shot/TWF), CMB-017, CMB-077 |
| 16 | NPC class features: rage, flurry, turn/rebuke, sneak-attack positioning | DMG class NPCs | Med | Med | CHR-003, CMB-016, CMB-026, CMB-028 |
| 17 | Consumables for the AI (potions first) | DMG NPC gear | Med | Med | AI-009, AI-054 |
| 18 | Remaining PHB actions: double move, run, total defense, aid another (AI path + expiry fix), withdraw, cover | all | Med | Low to Med | AI-010, GRID-009, new CMB (aid another) |

**Issues filed from this run** (2026-10-07, after checking each against the code and the books): SPL-110 (Hold Person/Monster have no per-round save), SPL-111 (Mage Armor is Self-only), CRE-034 (Summon Monster list deviations, including the SM II scorpion size), CRE-035 (eight creatures with no CR), CRE-036 (blue slaad pounce), CRE-037 (natural-attack energy riders are data only), AI-055 (no NPC Rapid Shot or two-weapon fighting), AI-056 (summon smite formula), CMB-080 (pinned casters cannot cast verbal spells).

**Proposed but not filed** (not verified yet; check before filing): Enervation's missing undead branch and 15-hour cap (no Enervation handler was found by grep, so it may resolve generically); adding Sound Burst and Inflict Serious Wounds to SPL-057; the Aid Another expiry and roll-bonus problems; extending CMB-022 with the Manyshot re-roll. The hydra `"MagicalBeast"` spelling is already CRE-033.

---

### 3. Top 15 monster abilities by creature count

n is the number of creatures in the 310-entry work list (192 CSV creatures and 118 summons) that carry the id. Ids were not deduplicated across chunks, and template abilities are inflated by the summon lists.

| # | Ability (index id) | n | Code | AI | Verified notes |
|---|---|---|---|---|---|
| 1 | Damage reduction (`ability_damage_reduction`) | 53 | partial | n/a | Generic DR id; the name shown ("10/evil and magic") is just the first instance. `DamageBypassTag` flags exist (`DamageModel.cs:10`). Template DR and SR by HD: CRE-008. |
| 2 | Improved grab | 49 | implemented | yes | `HasImprovedGrab` + trigger attack name; inherits CMB-014. Per-creature grab and constrict values need an MM check. |
| 3 | Scent | 47 | partial | partial | Copied at spawn (`NPCSetup.cs:514`), read only for invisible-target scoring (`AIService.cs:1925-1946`). The hell hound entry's data_only/no rating was wrong. |
| 4 | Flight | 42 | missing | partial | No fly speed exists on `NPCDefinition` (only `BaseSpeed`); flyers walk. |
| 5 | Poison (injury, bite) | 25 | partial | yes | `PoisonOnHitId` is used by 13 attacks over 10 poison ids. Secondary damage timed in real seconds (CMB-007). |
| 6 | Constrict | 22 | missing | no | Text only in 10 creature files; no logic (`CREATURES.md:111`). |
| 7 | Fast healing | 22 | missing | n/a | Mephits model it as `RegenerationAmount = 2` (CRE-017), so it is partly a stand-in. Combat stalls on downed regenerators (CORE-034). |
| 8 | Smite good | 22 | partial | partial | `GainsSmiteGood` becomes `HasTemplateSmiteGood` (`NPCSetup.cs:591`). It fails because targets spawn without alignment (CRE-002); summon formula: AI-056. Family total with the `*_template` ids is 31+. |
| 9 | Breath weapon | 19 | partial | partial | Dragons and hell hounds only; no action cost or save rules (AI-040); no recharge (AI-032). |
| 10 | Vermin traits | 19 | implemented | n/a | Mind-affecting immunity by type (`CharacterStats.cs:3966-4012`). |
| 11 | Spell resistance | 17 | implemented | n/a | Applies only when a spell sets `SpellResistanceApplies` (SPL-057). |
| 12 | Tremorsense | 15 | missing | no | No code reference outside creature data (grep). |
| 13 | Poison (1d4 Dex) | 14 | missing | no | No matching Dex poison id among the 10 in use. |
| 14 | Elemental traits | 14 | partial | n/a | Elementals are not mind-affecting-immune by type, so the immunity gap is real for them. |
| 15 | Template energy resistance (celestial) | 14 | implemented | n/a | Correct per CRE-008. Tied at 14: template SR HD+5, missing for scope creatures under 8 HD (CRE-008). |

**Index corrections found in this pass:**

- `ability_rake` (n=9) is marked missing, but rake is implemented. `HasRake` is set on 9 definitions (tiger, dire_tiger, lion, dire_lion, leopard, celestial_lion, hellcat, behir, skum) and executed by `CharacterController.cs:10119`, `SupportActions.cs:1418/1562/1897` and `GrappleSystem.cs:1905`. Regrade it to implemented and check which indexed creatures lack the flag.
- `ability_natural_attack_energy_rider` (4) and `ability_fiery_bite` (2) are data_only, not missing.
- `ability_trample` is data_only for the treant (CRE-018).
- Pounce is set on 9 creatures, not 6, including the non-RAW blue_slaad.

---

### 4. Spell coverage, levels 0-4 (+5 for enemy casters)

Level = lowest level on any PHB class or domain list. Status is judged by behaviour, after re-grading Hold Monster, Hide from Undead, Enervation and Mage Armor from implemented to partial.

| Level | Spells | Implemented | Partial | Placeholder | Data-only | Missing | AI yes / partial / no / n/a | P1 |
|---|---|---|---|---|---|---|---|---|
| 0 | 28 | 8 | 3 | 16 | 0 | 1 | 1 / 10 / 17 / 0 | 3 |
| 1 | 87 | 32 | 18 | 19 | 3 | 15 | 6 / 32 / 49 / 0 | 21 |
| 2 | 89 | 23 | 27 | 22 | 2 | 15 | 5 / 29 / 55 / 0 | 19 |
| 3 | 79 | 26 | 19 | 3 | 0 | 31 | 1 / 21 / 57 / 0 | 14 |
| 4 | 68 | 10 | 25 | 3 | 1 | 29 | 5 / 10 / 46 / 7 | 1 |
| 5 | 58 | 1 | 3 | 3 | 3 | 48 | 0 / 2 / 48 / 8 | 0 |
| **All** | **409** | **100** | **95** | **66** | **9** | **139** | **18 / 104 / 272 / 15** | **58** |

By the `IsPlaceholder` flag the split is 223 functional, 47 placeholder and 139 missing. Behaviour shows 66 no-effect spells, because 20 placeholders do not carry the flag and SM I/II carry it wrongly (SPL-021).

PC usability before correction is 129 yes, 94 partial and 186 no. 29 Cleric- or Druid-only spells of 3rd level and up are overstated, so the corrected split is about 120 yes, 74 partial and 215 no.

**Slot caps (SPL-041, verified in `SpellcastingComponent.cs`):**

| Class | Cap | Evidence |
|---|---|---|
| Wizard | {4,3,2} from level 4: never above 2nd | :919-950 |
| Druid | {4,2,1} from level 3 | :1094+ |
| Cleric | Slots reach 5th at level 9 (:1030-1050), but known spells load only levels 0-2 (:744-749) and domains cover 1-2, so 3rd+ slots stay empty | |
| Sorcerer | Full table (`SpontaneousCastingData`), so Wizard-list 3rd+ spells are reachable by sorcerers only | |

**Effect on enemies:** the tables top out at NPC caster level 5, which should mean 3rd-level spells. In practice DMG class casters get no spells at all (ENC-021), Wizard/Cleric NPCs stop at 2nd, the lich (Wizard 11) casts nothing, and the vampire is a Fighter (CRE-030).

**Biggest gaps:**

1. **AI-001 Area refusal** (77 entries). P1 spells the AI selects but cannot cast: bless, burning hands, color spray, entangle, obscuring mist, sleep, flaming sphere, glitterdust, web, fireball, lightning bolt, stinking cloud, flame strike.
2. **Empty NPC effects:** summon (SM III, SNA III), dispel and escape (SPL-091); prayer, magic vestment and magic weapon have no NPC handler.
3. **P1 placeholders or missing:** command and hypnotic pattern (placeholders), suggestion (missing). Missing at 1-2: charm animal, faerie fire, goodberry, shillelagh, speak with animals, animal trance, flame blade, spike growth, chill metal, misdirection, tongues and more. Druid and ranger 1st-2nd lists are about half missing.
4. **3rd-5th combat gaps:** animate dead, fly, deeper darkness, polymorph, cloudkill, SM V, SNA V, mass cure light wounds, dispel chaos/evil/good/law, teleport, wall of stone, raise dead.
5. **Data errors already filed:** SPL-099 (Flame Strike at 2nd), SPL-100 to SPL-102 and SPL-105 (class lists, domain-only spells), SPL-046 (bursts resolve as single target).
6. **SR opt-in** (SPL-057): audit every RAW SR-yes spell's database block.

---

### 5. Book-check items

**Resolved in this pass:**

| Item | Finding |
|---|---|
| Manyshot (PHB p.97) | Standard action; two arrows at one target within 30 ft; one shared roll at −4. One more arrow per 5 BAB above +6 (penalties −6 and −8). Precision damage applies once. |
| Tumble (PHB p.84) | Each additional enemy after the first adds +2 to the DC. |
| Run (PHB p.144) | Full-round action; ×4 speed in a straight line (×3 in heavy armor); you lose your Dex bonus to AC unless you have the Run feat. |
| Touch of Fatigue (PHB p.294) | No effect on a creature that is already fatigued. |
| Lesser Confusion (PHB p.212) | The printed block omits the save and SR lines and refers to confusion. Use Will negates, SR yes (as the SRD does). |
| Order's Wrath (PHB p.258) | Medium range; affects nonlawful creatures in a burst that fills a 30-ft cube. |

**Still open:**

- **Grapple:**
  - one natural weapon while grappling vs the full routine
  - whether drawing a light weapon is an opposed check
  - escaping must beat every grappler (PHB p.156)
  - per-creature improved grab and constrict values
- **Creatures:**
  - per-creature trip bonuses (hyena, shadow mastiff, dire wolf)
  - ettin Superior Two-Weapon Fighting
  - squeezing
  - non-SRD MM creatures and their SLA lists: mind flayer charm monster DC, yuan-ti deeper darkness, beholder/gauth eye rays
- **Spells:**
  - Summon Instrument, Animal Messenger, Animate Rope and Calm Animals (untrained-animal save clause)
  - Goodberry eating action (no PHB ruling; flag any choice as a house rule)
  - Alter Self form list, Soften Earth and Stone, SM II and SNA II lists, Charm Monster
  - Meld into Stone expulsion damage, Phantom Steed table, Plant Growth speeds, Poison, Quench
  - Giant Vermin CL table, Imbue with Spell Ability recipient rule, Lesser Planar Ally payment
  - Polymorph (types, healing, gear), Restoration casting time, Shadow Conjuration (casting time, mindless creatures), Mirage Arcana range
- **Out of the combat catalog** (record as Downtime or OutOfCombatOnly): Legend Lore, Mark of Justice, Modify Memory, Mnemonic Enhancer, Hallucinatory Terrain, Animal Messenger.
- **Creature tables:** the CREATURES_* and SUMMONS tables were spot-checked: the verifier received every fifth creature (62) and checked 13 of them in depth, with 7 corrections applied. Stat-block deviations in unmarked rows are agent claims; check the MM before fixing a creature from them.
