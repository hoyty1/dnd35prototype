> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-02.

# Characters & creatures

This part covers `Assets/Scripts/Character/` (Controller, Stats, Feats, Skills, Progression, Races, Religion, Specialization, Familiar, Classes, CreatureClass, Creatures, Templates) and the GameManager methods that turn data into live tokens. All types are in the global namespace. Line numbers are as of the commit above and drift quickly in the two giant files; grep the member name first.

## CharacterController vs CharacterStats

Every combatant (PC, NPC, summon) is one GameObject with a `CharacterController` plus one `CharacterStats` instance.

| | CharacterController | CharacterStats |
|---|---|---|
| File | Assets/Scripts/Character/Controller/CharacterController.cs (12,261 lines) | Assets/Scripts/Character/Stats/CharacterStats.cs (6,200 lines) |
| Kind | MonoBehaviour on the token | Plain `[System.Serializable]` class (not a component) |
| Owns | Grid position, team/control flags, `Actions` (ActionEconomy), HP-state machine, per-spell `Active*Effect` objects, grapple links, feint windows, all attack and maneuver resolution | Ability scores, `ClassLevels`, HP/nonlethal/temp HP, `Feats`, `Skills`, race, deity/domains, wizard specialization and familiar, XP, about 280 public fields in all (most are named bonuses or per-effect state), derived AC/BAB/saves, the incoming-damage pipeline |
| Link | `controller.Stats` | `stats.OwnerCharacter` (`[NonSerialized]`), set in CharacterController.Init (CharacterController.cs:3063; Init starts at L3045), which also subscribes to `Stats.CurrentHPChanged` |

**Helper components.** CharacterController.Awake (CharacterController.cs:1362) adds SpriteRenderer, StatusEffectIndicator and ConditionManager, then calls the `Ensure*()` helpers (L1276-1356). The four component helpers do GetComponent-or-AddComponent and `Initialize(this)` (`EnsureConditions` also passes GameManager's ConditionService); `EnsureTags` and `EnsureStatusTagManager` create plain objects with `new`. They were extracted from the controller on 2026-04-22 and are not serialized in any prefab.

| Helper | File | Role |
|---|---|---|
| CharacterCombatStats | Character/Stats/CharacterCombatStats.cs | Iterative attack bonuses (adds Haste extra attack), off-hand count, grapple modifier |
| CharacterEquipment | Character/Controller/CharacterEquipment.cs | Dual-wield and two-hand queries, equipped weapon lookups, disarm/sunder options, reload state |
| CharacterInventory | Equipment/Inventory/CharacterInventory.cs | Inventory facade (via the `Inventory` property) |
| CharacterConditions | Character/Stats/CharacterConditions.cs | Routes Apply/Remove/Has condition to GameManager's ConditionService if present, else to the controller's `*Direct` methods |
| CharacterTags, StatusTagManager | Character/Stats/ | Plain objects, tooltip/status tags |

Lazy getters (CharacterController.cs:1207-1251): `StatusEffectManager`, `Spellcasting`, `InventoryComp`, `Concentration`.

**Facade vs Direct.** Many controller methods come in pairs. `ApplyCondition` (L3466) goes through CharacterConditions and ConditionService; `ApplyConditionDirect` (L3471) writes state through ConditionManager.ApplyCondition (Combat/Conditions/ConditionManager.cs), falling back to `Stats.ApplyCondition` only when no ConditionManager is attached (Awake always adds one). Services must call the `*Direct` variant to avoid recursion (Services/ConditionService.cs does). `CharacterStats.ApplyCondition` is a legacy second implementation with different rules; see "Two ApplyCondition implementations" in [Conditions and status effects](combat-and-grid.md#conditions-and-status-effects) **[KI]**.

**Team and control.** `Team` (CharacterTeam: Player, Enemy, Neutral) decides allegiance; `IsControllable` decides player input; `UsesAI => !IsControllable`. `IsPlayerControlled` is a legacy shim kept in sync with `Team == Player`; setting it to false forces `Team = Enemy` (overwriting Neutral) and, unless controllability was set explicitly, also changes `IsControllable`. Set both at once with `ConfigureTeamControl(team, controllable)`.

**Two notions of dead.** `Stats.IsDead` is `CurrentHP <= -10` (CharacterStats.cs:3298); `controller.IsDead` is `HPState == Dead` (CharacterController.cs:1202). The seven `HPState` values are in Character/Stats/HPState.cs.

### Region map: CharacterController.cs

| Lines (approx.) | Contents |
|---|---|
| 1-229 | File-level types: SpecialAttackType, GrappleActionType, AttackDamageMode, disarm/sunder option structs, GrappleCheckResult, BullRushCheckResult, CharacterTeam |
| 230-600 | Team/control flags, `Active*Effect` properties, feint and grapple-link state |
| 600-1180 | Power Attack / Rapid Shot / fighting-defensively toggles, lethal/nonlethal damage mode, the per-creature attack-sequence executor (`TryCommitAttack`, `TryPayForNextAttack`, `RegisterAttackMade`), base damage profiles |
| 1180-1410 | Component refs, lazy getters, `Ensure*()`, Awake |
| 1410-3000 | Per-spell effect apply/tick/clear (Haste, Slow, Blur, Displacement, Blink, Invisibility, Glitterdust, False Life, Enfeeblement, Touch of Idiocy, ...) |
| 3045-3430 | `Init`, grid occupancy and movement (`RefreshGridOccupancy`, `MoveToCell`) |
| 3430-3600 | Condition facade, negative levels |
| 3593-4310 | Ability damage/drain, disease, poison, monster ability `Configure*` and ticks, ability-score-zero effects |
| 4357-4700 | HP-state machine (`OnCurrentHPChanged`, `DetermineStateFromHPTransition`, `OnHPStateChanged`, `ProcessEndOfTurnHPState`), natural-attack on-hit riders |
| 4703-5680 | Shared attack modifier `BuildAttackBonus` (4800), `Attack` (4859), `FullAttack` (5089), `DualWieldAttack` (5540) |
| 5681-6161 | Attack helpers: `GetSituationalTargetArmorClass`, DEX denial, feint, sneak-attack immunity |
| 6162-7163 | `PerformSingleAttackWithCrit` (about 1,000 lines: concealment, crits, sneak attack, enchantments, damage) |
| 7164-8029 | Weapon, reload, reach and size queries, visual scaling |
| 8029-8899 | Grapple links, concealment and miss chance |
| 8904-10089 | `ResolveGrappleAction` and sub-actions (three are "not yet implemented" stubs) |
| 10090-10200 | `OnDeath`, `StartNewTurn` |
| 10201-11743 | `ExecuteSpecialAttack` and maneuver resolvers (Trip, Disarm, Sunder, BullRush, Overrun, Feint) |
| 11744-12252 | `FiveFootStep`, `FlurryOfBlows`, `ActivateRage`, counterspell readiness |

### Region map: CharacterStats.cs

| Lines (approx.) | Contents |
|---|---|
| 1-153 | Enums, NaturalAttackDefinition, ClassLevelEntry, ClassHitPointEntry |
| 155-560 | Identity, `ClassLevels` and cache, `EnsureMulticlassDataInitialized` (355), deity/domains, WizardSpecialization/WizardFamiliar (291-292), `Is<Class>` flags (512-557) |
| 560-1080 | ECL, class-feature properties (Bard, Druid, Ranger, Paladin, Fighter, Monk, Barbarian), casting ability, domain uses, turn undead |
| 1081-1400 | Condition-derived modifiers (summed from ConditionRules on every read), Rage |
| 1401-1765 | Legacy condition list, rest, ability damage, Haste/Slow/Solid Fog/Fire Shield fields |
| 1770-1872 | `GetHitDice` (total HD, CHR-071), save progressions, `FortitudeSave`/`ReflexSave`/`WillSave` |
| 1873-2150 | `Feats` (1875), caster level, Weapon Focus list, feat-derived values, `InitFeats` (2084), `TotalMaxHP` (2133) |
| 2151-2340 | Race, size, creature type, natural attacks, DR/resistance/immunity lists, template flags |
| 2340-2750 | Ability scores, `BaseAttackBonus` (2421), armor/encumbrance fields, modifier math |
| 2750-3315 | `MaxHP` (2750), HP events, about 100 buff/ring/wondrous fields, `ArmorClass` (3178), `TouchArmorClass` (3199), `AttackBonus` (3214) |
| 3343-3443 | Constructors |
| 3445-3760 | XP (`AddExperience`), crafting XP/gold, `ApplyPendingLevelUp` (class hit die from `ClassProgression`, HitDice kept in step) |
| 3755-4260 | Natural attacks, special abilities, immunities, roll helpers (several unreferenced) |
| 4259-5127 | `ApplyIncomingDamage` (4259), `TakeDamage` (4660), `HealDamage` (4748), energy protection, DR |
| 5128-5650 | Display, racial, proficiency, ACP, size |
| 5652-6200 | Skills (`InitializeSkills` 5669, `GetSkillBonus` 5941, `RollSkillCheck` 5994), STR-to-damage helpers |

## Derived stats and the bonus model

Derived values are expression-bodied properties recomputed on every read. Nothing is cached: `EnsureMulticlassDataInitialized` sets `_classLevelCache = null` on every call (CharacterStats.cs:358), and it rewrites `Level = sum(ClassLevels) + PendingLevelUps` (L383-384). Never assign `Stats.Level` directly; change `ClassLevels` or `PendingLevelUps`.

| Value | Where | Composition |
|---|---|---|
| Ability scores | `STR`..`CHA` fields; `Effective*Score`, `STRMod`.. (CharacterStats.cs:2732-2747) | Field = base + racial + level-up + inherent + spell buffs written directly (StatusEffectManager.ApplyStatBonus does `_stats.STR += bonus`). Effective score subtracts damage/drain and adds `WondrousEnhancement*`. `NO_SCORE = -1` marks an absent ability |
| AC | `ArmorClass` (L3178) | 10 + DEX (capped by MaxDexBonus, 0 if a condition denies DEX) + max(ArmorBonus + MagicVestment, SpellACBonus) + shield + natural + wondrous natural + size + monk + FeatAC (Dodge) + rage + deflection (`EffectiveDeflectionBonus` = max(spell `DeflectionBonus`, `RingDeflectionBonus`)) + condition + Haste/Slow + insight |
| Situational AC | CharacterController.GetSituationalTargetArmorClass (L5953), then PerformSingleAttackWithCrit (~L6504-6637) | Invisibility, prone, pinned, fighting defensively, mounted; alignment-protection deflection (only the part above the target's own deflection, `AlignmentProtectionRules.DeflectionAcIncrease`); grapple/feint/blink DEX denial |
| BAB | `BaseAttackBonus` | `BaseAttackBonusOverride`, else the racial Hit Dice (`RacialHitDice`) by `CreatureBABProgression` plus the sum over the real `ClassLevels` (not the `RacialHitDiceStandInClass` or the `InnateSpellcastingClass`; CRE-004) of `ClassProgression.GetClassBaseAttackBonus` (the class definition's `BABAtLevel3`, PHB Table 3-1; CHR-002) |
| Attack roll | `CharacterController.BuildAttackBonus` returns an `AttackBonusBreakdown` (`Combat/Core/AttackCalculator.cs`); Attack, FullAttack (iterative and natural), DualWieldAttack and FlurryOfBlows all use it (CMB-043) | BAB step + sequence penalty (two-weapon, flurry, secondary natural -5 or -2 with Multiattack, Manyshot/Mobility via `additionalAttackModifier`) + ability (DEX for ranged and Weapon Finesse, else STR) + size + flanking + racial + range + mounted ranged + feat mods (AttackCalculator.CalculateAllFeatModifiers) + prone + fighting defensively + shooting into melee + non-proficiency + morale + conditions + aid another + damage mode + Solid Fog + Bracers of Archery + Magic Stone. `PerformSingleAttackWithCrit` then adds enhancement/masterwork and target-side terms. Rake and grapple weapon attacks still sum their own (CMB-087). `Stats.AttackBonus` (BAB + STR + size + morale + conditions) is now read only for display and logs (character sheet, info panel, inventory, spawn logs); `GetMeleeAttackBonus`/`GetRangedAttackBonus` are estimates for AI and AoO risk |
| Weapon damage | `CharacterController.BuildWeaponDamageBonus` returns a `WeaponDamageBreakdown` (`Combat/Core/AttackCalculator.cs`); every weapon attack path passes it to `PerformSingleAttackWithCrit`, and coup de grace, sunder and grapple damage add its total (CMB-003). Exceptions that still roll their own damage: mount attacks and trample (`MountedCombatSystem`, `MountNaturalAttack.RollDamage`) and the Confused self-attack (`CharacterStats.RollDamage`, Strength and morale only) (CMB-162) | Strength share (`GetWeaponDamageModifier`: x1, x1-1/2 two-handed, x1/2 off hand, also for an off-hand weapon passed as an `Attack` override, composite rating, a penalty never multiplied; a natural attack's own share, CMB-161; full Strength in a flurry, PHB p.41) + the profile's flat damage + weapon enhancement + bane +2 against its foe (DMG p.224) + material + Power Attack, Point Blank Shot, Weapon Specialization (from `AttackBonusBreakdown.Feats`) + `MoraleDamageBonus` (all spell buffs, SPL-026; Inspire Courage; logged by source name) + `ConditionWeaponDamageModifier` (Sickened -2) + Solid Fog + Bracers of Archery + a per-attack situational term (template smite); `PerformSingleAttackWithCrit` adds the Destruction smite on a melee attack. Added once on a critical (CMB-004). Rake passes no feat terms and grapple damage, the grapple weapon attacks and sunder only Weapon Specialization (CMB-087, CMB-159); coup de grace leaves Power Attack out (CMB-160) |
| Saves | `GetSaveTotal(SavingThrowType)` (`FortitudeSave`/`ReflexSave`/`WillSave` delegate to it); every roll goes through `SaveRules.Modifier(stats, save, SaveContext)` (`Character/Stats/SaveRules.cs`, CHR-018) | Ability mod + base save (`ClassFortSave` etc.: racial HD by the creature progression, `ProgressionCalculator.CalculateRacialSave`, plus the sum over real classes of `ClassProgression.GetClassBaseSave`, CRE-004) + feat (Great Fortitude, Lightning Reflexes, Iron Will) + Divine Grace (paladin 2, PHB p.44) + racial on all saves (`RacialAllSavesBonus`, halfling +1) + `MoraleSaveBonus` (the untyped pool of spell save bonuses, Inspire Courage, Prayer; SPL-026) + equipment luck (`EquipmentLuckSaveBonus`: Luck Blade, Stone of Good Luck, Robe of Stars; luck stacks, house rule) + resistance (`EffectiveResistanceSaveBonus`: the highest of ring, cloak, Robe of the Archmagi for an arcane caster and resistance-typed spells such as Resistance) + competence (`EffectiveCompetenceSaveBonus`: Pale Green Prism, Guidance) + condition; Fort and Ref add the familiar bonus, Will adds rage. `SaveRules.SituationalBonus` adds what applies only against the effect a `SaveContext` describes (`ForSpell(spell, caster)`, `Poison(id)`, `Fear`, `Trap`): racial against poison, spells, enchantment and illusion (added together: PHB p.171 lets racial bonuses stack; DMG p.21 reads otherwise, owner question in CHR-019), morale against fear (the best of halfling +2, Remove Fear +4, Bless or Aid +1 (`FearMoraleSaveBonusFromSpells`) and a conscious allied paladin's Aura of Courage +4 within 10 ft (`SaveRules.AuraOfCourageBonus`), only beyond the rage's morale bonus on Will), Still Mind, Indomitable Will, Resist Nature's Lure, trap sense (barbarian and rogue), Cloak of Arachnida luck against spider poison. Spell-and-caster bonuses (Charm Person, Hideous Laughter, the alignment ward, Lullaby) are added only in `SpellCaster.GetSaveModifier` **[KI]** (SPL-018) |
| HP | `MaxHP` (stored, private set); `TotalMaxHP` | `MaxHP` is set by the constructor and changed by level-up, rage, inherent bonuses and `AdjustMaxHP`; it is not re-derived from CON. `TotalMaxHP` adds Toughness, `BonusMaxHP`, wondrous CON HP and familiar HP, minus 5 per negative level |
| Hit Dice | `GetHitDice()`; field `HitDice` | A creature's definition total (`InitializeNPCFromDefinition` sets the field), or a character's racial HD (none for PHB races) plus applied class levels: the constructor sets it and `ApplyPendingLevelUp` keeps it in step; pending level-ups do not count. Every HD-gated rule reads it (`TeamUtility.GetHitDice`, Sleep/Daze/Color Spray limits, Turn Undead, Circle of Death, Death Knell, Detect Evil, domain HD budgets, smite, CON HP changes, negative-level death); `Level` stays for XP, feats and skill caps (CHR-071) |
| Damage taken | `ApplyIncomingDamage` (L4259) then `TakeDamage` (L4660) | Mitigation order: see the damage pipeline in [Attack resolution](combat-and-grid.md#attack-resolution). The `CurrentHP` setter fires `CurrentHPChanged`, which drives the controller's HP-state machine |

- Constructor HP is `ClassProgression.ApplyConstitution(baseHitDieHP, level, CON modifier)`: the CON-free hit-die total plus the CON modifier per Hit Die (negative too; 0 without a CON score, which `ClassProgression.HitPointConstitutionModifier` also assumes for a stored CON of 0, the way 15 MM undead and construct entries store the missing score, CRE-044), at least 1 HP per die (PHB p.9; CHR-001). `baseHitDieHP` must not include CON: character creation passes `CharacterCreationData.BaseHitDieHP` (its `HP` minus CON per die). A creature spawned from an `NPCDefinition` (`GameManager.InitializeNPCFromDefinition`, and the Lion's Shield summon) then has its `MaxHP` replaced by `ClassProgression.CreatureMaxHitPoints`: the definition's `BaseHitDieHP` when set (the MM total, CON included, plus template and `CreatureClassEngine` class HP with CON per die), else the type's average die plus CON per Hit Die. `TotalMaxHP` still adds Toughness to MM totals that already count it **[KI]** (CRE-041).
- A change to an attack-roll term goes in `BuildAttackBonus` (and, if it needs a log entry, `AttackBonusBreakdown.ApplyToResult`); the four weapon attack paths pick it up. A weapon damage term goes in `BuildWeaponDamageBonus` (a new field on `WeaponDamageBreakdown`, its `Total` and `GetTerms`), which labels it in the combat log and the scenario trace (CMB-003). `PerformRakeAttacks` and the two grapple weapon attacks still need it separately **[KI]** (CMB-087).

### The bonus stacking model

There is no general modifier engine. Typed (`BonusType`) same-type non-stacking exists only among spell effects inside StatusEffectManager (`AddEffect`, `GetTotalBonusOfType`); everything else is hand-summed. Each source has its own named public field on CharacterStats (`HasteACBonus`, `RingResistanceSaveBonus`, `WondrousInsightACBonus`, `DeathWardActive`, ...): 28 `*Active` booleans (mostly spell flags; DestructionSmite, GreaterTurning, StunningFist, Manyshot and WondrousHaste are not), 38 `Wondrous*` and 14 `Ring*` fields. Stacking rules are re-implemented per field (`Mathf.Max` in one place, `+=` in another, a read-time max such as `EffectiveDeflectionBonus` and `EffectiveResistanceSaveBonus` in a third) and are violated in places, for example the Shield spell's bonus added into the equipment-rebuilt `ShieldBonus` (ITM-073; see [Inventory, equipment slots and stat recalculation](items-and-economy.md#inventory-equipment-slots-and-stat-recalculation)) **[KI]**.

Writers follow two patterns:
- Items: Equipment/Inventory/Inventory.cs `RecalculateStats` (L449) resets each `Wondrous*`/`Ring*` field to 0 and then sets it, usually with `Mathf.Max` per bonus type.
- Creature data: `CharacterStats.IsExceptionallyStable` (more than two legs or a stability trait, PHB p.154/157/158) is written once at spawn from `NPCDefinition.IsExceptionallyStable` by `InitializeNPCFromDefinition` (which also serves summons) and by the Lion's Shield summon builder (`LionsShieldBehavior`); its only reader is `CharacterController.GetManeuverStabilityBonus`, which also reads `RaceData.StabilityBonus` for PC dwarves, gives one +4 (never +8) and 0 to a rider (CMB-111; mounting is unreachable in play, CMB-031). That bonus feeds `RollBullRushDefenderCheck` and `GetTripOrOverrunDefenderCheckModifier`.
- Spells: Spell/Components/StatusEffectManager.cs adds on apply and subtracts on removal (ability fields such as `_stats.STR += bonus` in ApplyStatBonus) or sets and clears (`SpellACBonus`; `DeflectionBonus`, spell deflection only, is set to the highest `AppliedDeflectionBonus` of the applied effects on every apply and removal, ITM-001); some spells instead use a controller `Active*Effect` object or a condition.

**To add a new bonus source:**
1. Add a public field on CharacterStats near the related fields (buff/item fields live around L2750-3315). Name it `<Source><BonusType><Target>`.
2. Write it from exactly one owner: reset-and-set in `Inventory.RecalculateStats` for items, or apply/remove symmetrically in StatusEffectManager for spells.
3. Add it to every formula that should read it: `ArmorClass` and `TouchArmorClass`, the three save properties, `GetSkillBonus`, `GetCasterLevel`, for attack rolls `CharacterController.BuildAttackBonus` (shared by Attack, FullAttack, DualWieldAttack and FlurryOfBlows) plus the rake and grapple sums (CMB-087), `Stats.AttackBonus` (UI) and `GetMeleeAttackBonus`/`GetRangedAttackBonus` (AI); for weapon damage `CharacterController.BuildWeaponDamageBonus` (CMB-003: one `WeaponDamageBreakdown` term per source, used by every weapon attack path, coup de grace, sunder and grapple damage).
4. Decide stacking explicitly (`Mathf.Max` against same-type fields).
5. Grep for the new field. A field with a writer but no reader is a silent no-op; `WondrousCompetenceSaveBonus` and `WondrousCasterLevelBonus` (written by Inventory.cs, read nowhere) are current examples, and the computed property `DivineGraceBonus` likewise has no reader **[KI]**.

## Feats

Feats are display-name strings in `Stats.Feats`, a case-sensitive `HashSet<string>` (CharacterStats.cs:1875); `HasFeat(name)` is `Feats.Contains`. There is no FeatNames constants class (unlike SpellNames and ItemIDs). Because it is a set, a feat cannot be taken twice (Toughness counts once).

| Piece | File | Role |
|---|---|---|
| Catalog | Character/Feats/FeatDefinitions.cs | 101 feats: 86 explicit `new FeatDefinition(...)` plus 15 skill-pair feats via `DefineSkillPairFeat`. Lazy `Init()`. Per-level feat counts (`GetsGeneralFeatAtLevel`, `GetsFighterBonusFeatAtLevel`, wizard bonus feats). Does not apply effects |
| Schema | Character/Feats/Feat.cs | `FeatDefinition.MeetsPrerequisites` (L260), `FeatPrerequisite.IsMet`. Most `FeatBenefit` fields are written but never read. Production code reads only SkillBonuses (FeatManager.GetAllSkillBonuses), RequiresWeaponChoice (CharacterSheetUI, FeatSelectionUI) and Description (FeatSelectionUI detail text); IsMetamagic is read only by a test |
| Effects | Character/Feats/FeatManager.cs | Static queries keyed by hard-coded names: Toughness HP, save feats, initiative, Dodge AC, Weapon Focus/Specialization matching, TWF, Improved Critical, skill feats, Stunning Fist, Deflect/Snatch Arrows, Augment Summoning, turning |
| Attack feats | Combat/Core/AttackCalculator.cs `CalculateAllFeatModifiers` (L304) | Power Attack, Point Blank Shot, Weapon Focus/Specialization, Finesse, Combat Expertise, Improved Critical, Rapid Shot |
| Situational | A FeatManager query or an inline `HasFeat(...)` at the call site | Deflect Arrows (`FeatManager.TryDeflectArrow` in PerformSingleAttackWithCrit), Diehard (`HasDiehard`, HP-state machine), Mobility (`HasMobility`, Combat/Core/ThreatSystem.cs), Spell Focus/Penetration (`GetSpellFocusDCBonus`/`GetSpellPenetrationBonus`, Spell/Casting/SpellCaster.cs), Improved Grapple (inline `HasFeat`), ... |

There is no central "apply feats" step. `FeatManager.ApplyPassiveFeats` only logs and tops up CurrentHP for Toughness; passive values are read live (`FeatACBonus`, `FeatHPBonus`, `GetFeatSkillBonus`, CharacterStats.cs:2117-2149). FeatSelectionUI checks prerequisites by temporarily swapping `stats.Feats` with a projected set (UI/Spells/FeatSelectionUI.cs:938-955).

Inert or partially inert feats (defined, selectable, little or no effect) **[KI]**: Combat Expertise (`CombatExpertiseValue` is never written), Spell Focus and Greater Spell Focus (`SpellFocusSchool` is never assigned), Skill Focus for everyone (`CharacterStats.SkillFocusChoice`, which FeatManager reads, is never assigned; `CharacterCreationData.SkillFocusChoice` is never written either), Two-Weapon Defense, Blind-Fight, Quick Draw, Run, Spell Mastery (`IsPlaceholder = true`, but `IsPlaceholder` is never read). Weapon Focus taken at level-up with no earlier focus weapon gives +0, because CharacterCreationManager.ApplySelectedFeats adds only the name and not the weapon choice. NPC feat strings such as "Weapon Focus (longsword)" never match a catalog name; "Multiattack" is not in the catalog either, but `CharacterStats.GetNaturalAttackSequencePenalty` reads it by name (secondary natural attacks at -2, MM p.304; CMB-102); the NPC data lists it only with three or more natural attacks (`Tests.Character.NpcFeatDataTests`, CRE-045). Weapon Focus never reaches a natural attack, whose modifier is built with no weapon (CHR-008), and Weapon Finesse uses DEX even when STR is higher (CHR-073).

**To add a feat:** add a `FeatDefinition` in FeatDefinitions.Init, then add the effect at the point of use (a FeatManager query, AttackCalculator, or an inline `HasFeat`). Without step two the feat is selectable and does nothing. Recipe: "Add a feat with a combat effect" in [DEVELOPMENT_RECIPES.md](../DEVELOPMENT_RECIPES.md).

## Skills, races, religion, specialization, familiar

| Area | Files | Notes |
|---|---|---|
| Skills | Character/Skills/Skill.cs, ClassSkillDefinitions.cs; CharacterStats L5652-6060 | 32 base skills plus 10 Knowledge skills; no Craft, Profession, Speak Language or synergies. Class skills are the union over all `ClassLevels` (from ICharacterClass.ClassSkills). Points: (base + INT, min 1) x4 at character level 1, x1 after; per-class point pools in `classSkillPointPools`. Max ranks Level + 3 (half for cross-class). `RollSkillCheck` omits the familiar bonus that `GetSkillBonus` includes **[KI]** |
| Races | Character/Races/RaceData.cs, RaceDatabase.cs | Seven PHB races. Applied: ability modifiers (constructor), speed, size, darkvision, weapon proficiency/familiarity, racial attack bonuses vs creature tags (`RacialAttackBonuses`: dwarf and gnome), stability, elf sleep immunity, gnome +1 save DC on illusion spells (`SpellSaveDCRules`, PHB p.17; PCs only, since NPCs have no race, CRE-038). racial save bonuses (`SaveBonusAllSaves` in `CharacterStats.GetSaveTotal`, the rest in `SaveRules.SituationalBonus`, CHR-018; PCs only, CRE-038). Display-only (no consumer outside Character/Races/): racial skill bonuses, dwarf dodge vs giants, halfling thrown bonus, Human extra skill points and `ExtraFeatAtFirstLevel`, Stonecunning, `CountsAsRace`. The Human bonus feat is hard-coded in UI/CharacterCreation/CharacterCreationUI.cs:1530. NPCs are built with no race |
| Deities, domains | Character/Religion/DeityDatabase.cs, DomainDatabase.cs | 19 deities, 22 domains; domain spells only for spell levels 1-2. Only the Knowledge domain's class-skill grant is implemented (`RefreshSkillClassFlags`) |
| Alignment | Character/Stats/Alignment.cs | `AlignmentHelper.IsWithinOneStep` allows a diagonal step **[KI]**; GameManager.SetupCreatedCharacters drops an incompatible deity for every class |
| Wizard specialization | Character/Specialization/WizardSpecialization.cs | Specialist school plus 2 prohibited schools (1 for Divination). Extra slot and prohibited-school filtering live in SpellcastingComponent and SpellSelectionUI |
| Familiar | Character/Familiar/WizardFamiliar.cs | Master bonuses only (9 types, no Lizard), read in saves, `TotalMaxHP` and `GetSkillBonus`. Chosen only in the Wizard creation flow; no familiar token exists |

## Classes

`ClassRegistry` (Character/Classes/ClassRegistry.cs) registers 16 stateless `ICharacterClass` singletons in fixed order: Fighter, Rogue, Monk, Barbarian, Wizard, Cleric, Sorcerer, Ranger, Paladin, Bard, Druid, then the DMG NPC classes Adept, Aristocrat, Commoner, Expert, Warrior. Lookups are by display name, ignoring case; `GetClass` logs a warning on a miss, `TryGetClass` does not. Code checks classes with `stats.HasClass("Paladin")` or `GetClassLevel(...)`, never by type.

`ICharacterClass` members: ClassName, Description, HitDie, BABAtLevel3 (3 = full, 2 = 3/4, 1 = 1/2), SkillPointsPerLevel, GoodFortitude/GoodReflex/GoodWill, ClassSkills, DefaultArmorBonus/DefaultShieldBonus/DefaultDamageDice, SetupStartingEquipment, IsSpellcaster, TitleColor/ButtonColor/InfoText, InitFeats. Each PHB class also has a static `GetQuickStartCharacter()` (not an interface member). `InitFeats` grants automatic feats: Monk Improved Unarmed Strike, Ranger Track (and Endurance if total Level >= 3), Wizard Scribe Scroll. The interface comment says only Monk does this; it is stale.

The class objects are thin. Real class-feature rules are properties on CharacterStats (about L512-1330) and code in combat:

| Status | Features |
|---|---|
| Working in play | Barbarian rage tiers (CharacterStats.ActivateRage, CharacterController.ActivateRage); Rogue sneak attack (PerformSingleAttackWithCrit); Monk WIS AC, fast movement, Flurry of Blows, Still Mind; Fighter/Monk/Wizard bonus feats at level-up; Cleric spontaneous cure/inflict; Cleric/Paladin Turn Undead (Combat/Special/TurnUndeadSystem.cs); Bard Inspire Courage (GameManager.OnBardicMusicButtonPressed); Sorcerer/Bard spontaneous casting; domain powers |
| Cannot be activated | Monk Stunning Fist: the on-hit code (CharacterController.cs:7457) runs only when `StunningFistActive` is true, which only tests set, because SceneBootstrap never creates the CombatUI toggle (CMB-022) |
| Computed, never consumed | Paladin Divine Health; Barbarian DR, Uncanny Dodge, Improved Uncanny Dodge; trap sense's AC part (its Reflex part is in `SaveRules`, but nothing in the game is a trap yet). Divine Grace, Aura of Courage (+4 morale against fear for allies within 10 ft, and the paladin's immunity to fear: `CharacterStats.BlocksFearCondition` refuses shaken, frightened and panicked in `CharacterStats.ApplyCondition` and `ConditionManager.ApplyCondition`), Still Mind, Indomitable Will, Resist Nature's Lure and Venom Immunity are applied since CHR-018 (`CharacterStats.GetSaveTotal`, `SaveRules`, `IsImmuneToPoison`) |
| Data-only (classes exist, never instantiated outside tests) | Paladin Smite Evil (SmiteEvilData) and Lay on Hands (LayOnHandsData); Ranger favored enemy, combat style, animal companion; Druid Wild Shape (34 forms) and companion; Bardic Knowledge; 8 of 9 bardic music abilities. The CharacterStats fields that would hold them (L637-730) are always null; only `BardBardicMusic` is created (L588) |
| Approximated | Evasion/Improved Evasion apply only in Cone of Cold, Chain Lightning and Sunburst (`SpellSaveResolver.ApplyEvasion` has no other callers), not in Fireball, Lightning Bolt, other area spells or breath weapons (SPL-090); Monk fixed at level-3 numbers (d6 unarmed, +10 ft from level 1, always two flurry attacks) **[KI]**; Inspire Courage applies a morale bonus to all saves of every living PC, with no range check **[KI]**; Paladin/Ranger slots come from Spell/Casting/PartialCasterData.cs, which is initialized and displayed but whose PrepareSpell/CanCast/SpendSlot have no callers **[KI]** |

The combat "Smite" button is the celestial/fiendish template smite (UI/Combat/ActionButtonPanel.cs:692-698, Combat/Special/TemplateSmiteSystem.cs), not the Paladin feature.

Weapon and armor proficiency is not on `ICharacterClass` either: `CharacterStats` keeps name tables (`SimpleWeaponClasses`, `MartialWeaponClasses`, the armor and shield tables, and the specific lists for Bard, Druid, Monk, Rogue and Wizard/Sorcerer), which include the NPC classes since 2026-10-08 (CHR-072; the Sorcerer gets only the wizard list, CHR-075).

**Class numbers come from ICharacterClass.** Saves, BAB and hit dice all read the class definitions in `ClassRegistry` (lookups ignore case; `ClassRegistry.TryGetClass` is the quiet form). `Character/Classes/ClassProgression.cs` holds the shared arithmetic for every character with class levels, PC or NPC: `GetHitDie`, `GetClassBaseAttackBonus` / `BaseAttackBonusForProgression`, `HitPointsForHitDie` (die + CON, minimum 1), `AverageHitDieResult` (rounded up), `CreationHitPoints`, `ApplyConstitution`, `HitPointConstitutionModifier` (0 for no CON score or CON 0), `CreatureMaxHitPoints` (spawned creatures) and the base saves `BaseSaveForProgression` / `IsGoodSave` / `GetClassBaseSave` (CRE-004). `CharacterStats.BaseAttackBonus` and `ApplyPendingLevelUp`, `LevelUpCalculator` (the preview), `CharacterCreationData.ComputeFinalStats` and `CreatureClassEngine` (dungeon class levels) all call it; the three hard-coded tables are gone (CHR-002). A class name with no definition gets d8 and full BAB. The PHB and DMG class tables were checked on 2026-10-08; the expert's good Reflex save is wrong (CHR-077). `CreatureClassEngine.CalculateClassHP` still averages a die rounded down after 1st level, an owner question in CRE-041, and gives the maximum die to the first class level of a creature that already has Hit Dice (CRE-041). A humanoid with 1 HD or less exchanges its Hit Die for the class levels instead (`ClassLevelsReplaceHitDice`, MM p.290; CRE-004), and a base with the same real class adds the levels to it; class feats, skills and spells are not added (CRE-058).

Multiclassing caveats:
- `InitFeats` runs only from the constructor (plus three test-party setups in GameManager.TestConfigs.cs) and starts with `Feats.Clear()`. Taking a first level of Monk or Wizard at level-up does not grant the automatic feat, and calling `InitFeats` later wipes chosen feats.
- `ApplyPendingLevelUp` sets `CharacterClass` to the class just advanced. `GetPrimaryCastingModifier` (CharacterStats.cs:786) keys Druid/Ranger/Paladin/Sorcerer/Bard off that single string.
- Parameterless `CasterLevel` returns the best caster level across casting classes; `GetCasterLevel(className)` is the full class level, including for Paladin and Ranger. Both subtract negative levels; neither adds `WondrousCasterLevelBonus`.
- Turn Undead eligibility and turning level use total `Level`, not class level (TurnUndeadSystem.cs:418-433; CMB-016); the undead's HD come from `GetHitDice` (CHR-071).
- Creation hides NPC classes with a hard-coded set (CharacterCreationUI.cs:674-677). LevelUpCalculator lists all 16 in `AvailableClasses` (LevelUpCalculator.cs:29), but LevelUpUI.EnsureAvailableClassesForCurrentLevelUp (UI/CharacterSheet/LevelUpUI.cs:713) strips the same five NPC classes with its own hard-coded set, so a PC can never take an NPC class, contrary to the creation comment ("only available during level-up multiclassing").
- The level-up save preview takes the max across classes, while real saves sum them.

## XP and level-up

```
combat ends
  GameManager.LootCollection.cs:206  ExperienceCalculator.CalculateXPForCombat(party, defeated)
     -> per PC: Stats.AddExperience(xp)        multiclass penalty; sets Level, PendingLevelUps += n
  GameManager.CheckAndShowLevelUps -> ShowLevelUpUISequence -> LevelUpUI.ShowForCharacter
     -> LevelUpCalculator.CalculateLevelUp     preview: HP, BAB, saves, skill points, feat slots
     -> Stats.ApplyPendingLevelUp(class)       ClassLevels++, HP roll per GameSettings.hpCalculationMode
     -> LevelUpUI.ApplyAbilityIncrease         every 4th level: BaseX++ and X++
     -> CharacterCreationManager.StartLevelUpFlow   feat panels, skills, wizard choices, domains, spells
  repeat while PendingLevelUps > 0
```

- Award: XPTable keyed by clamp(round(CR - APL), -8, 12), divided by party size. CR = APL is 300 XP at any level; the DMG scales by level **[KI]**. `ExperienceCalculator.GetXPForLevel(n) = 1000 * n(n-1)/2`.
- XP raises `Level` immediately, but class levels, HP and features arrive only when the UI applies each pending level.
- `ApplyPendingLevelUp` (CharacterStats.cs:3554) also records `HitPointGainsByClassLevel`, refreshes class-skill flags and the XP penalty, and refreshes spell slots through SpellcastingComponent.
- Feat cadence: general at total level 1, 3, 6, 9...; Fighter bonus at Fighter 1, 2 and even levels; Wizard at 5/10/15/20; Monk at 1, 2, 6.

## Creatures

`NPCDefinition` (Character/Creatures/NPCDatabase.cs:489) is the full creature record: ability scores, HitDice, BaseHitDieHP, progression overrides (`BABOverride`, `BaseAttackBonusOverride`, `*SaveOverride`), natural attacks, defenses, special-ability payloads (breath, engulf, aura, blood drain, ...), tags, feats, spells, equipment, AI archetype, colors, `AppliedTemplateIds`, `SourceTemplateId`. `Clone()` (L641) is `MemberwiseClone` plus explicit copies of the lists, immunities, swarm traits, rake attack and five ability payloads; RangedSpecialAttack, BloodDrain and TerrainManipulation stay shared references.

`NPCDatabase` is a static partial class with a `Dictionary<string, NPCDefinition>`:
- NPCDatabase.cs: `Init`, `Get`, `RegisterExternal`, `Unregister`, summon aliases, encounter presets, the NPCDefinition class, payload types, ChallengeRatingUtils.
- NPCDatabaseCustom.cs: 46 non-Monster-Manual test and scenario NPCs (target_dummy, goblin_warchief, xp_pinata_goblin, ...).
- NPCDatabase_A.cs ... NPCDatabase_Y.cs: 25 letter partials of Monster Manual creatures.
- NPCDatabase_Dragons.cs, _Skeletons.cs, _Zombies.cs, _Lycanthropes.cs: generated or template-built entries.

Registration pattern: each file has `RegisterCreatures_<X>()` which calls one `private static void Register<Creature>()` per creature, usually doing `Register(new NPCDefinition { ... })` (some go through variant helpers, the dragon builder loop or the skeleton/zombie/lycanthrope factories). `Init` (L18) runs once and calls custom creatures first, then the partials in a hand-maintained, non-alphabetical order (A-H, L, M, O, R, S, T, V, W, Y, Dragons, Skeletons, Zombies, Lycanthropes, I, J, K, P, Q, U, X, N), then `RegisterSummonCreatureAliases`. A new partial must be added to that list by hand.

Conventions: IDs are lowercase snake_case (`dragon_red_young`, `monstrous_spider_small`); `ChallengeRating` is a string ("1/3"); `BaseSpeed` is in 5-ft squares; Monster Manual entries carry the "MM35" tag; `SpecialAbilities` is display text. The creature files contain 250 `CharacterClass = "Warrior"` assignments with `Level = HitDice` as a placeholder (some sit in helpers that build several entries, including the dragon builder), so the constructor creates a real Warrior class level and `IsWarrior` is true for animals and dragons. The skeleton and zombie templates set `CharacterClass = null` and `Level = 0`; `EnsureMulticlassDataInitialized` then falls back to Fighter 1, so those undead report `IsFighter` **[KI]**. `noble_djinni` uses the unregistered class "Outsider", which makes every ClassRegistry lookup log a warning.

Counts (static, not verified at runtime; the log line "[NPCDatabase] Initialized with N NPC types" is authoritative): about 389 unique IDs, including 46 custom, 60 dragons, 8 skeletons, 7 zombies and 8 lycanthropes; 87 encounter presets (55 core, 15 dragon, 5 skeleton, 5 zombie, 7 lycanthrope).

**ID collisions.** `Register` (L212) and `RegisterExternal` overwrite silently, so the last registration in `Init` order wins:
- Summon aliases run last and replace real entries: badger, giant_bee, riding_dog, owl and raven become clones of dire_badger, dire_bat, dog, eagle and eagle **[KI]**. Only the `wolf` alias adds a new ID.
- dretch (two registrations in NPCDatabase_D.cs), monstrous_centipede_medium (C vs M), monstrous_scorpion_small and monstrous_spider_small (M vs S), skeleton_archer (custom vs SkeletonFactory).
- Same creature under two IDs: large_viper / viper_large, huge_monstrous_centipede / monstrous_centipede_huge.

Grep for `"<new_id>"` (not only `Id = "<new_id>"`: the monstrous centipede/scorpion/spider variants are registered through `RegisterMonstrous*Variant("<id>", ...)` helpers) before adding a creature. `NPCDatabase.Get` returns the shared instance; `Clone()` it before mutating (see [Cross-cutting conventions](../ARCHITECTURE.md#cross-cutting-conventions)). Recipe: "Add a monster or NPC" in [DEVELOPMENT_RECIPES.md](../DEVELOPMENT_RECIPES.md).

### From definition to live combatant

`GameManager.InitializeNPCFromDefinition` (_Core/GameManager.NPCSetup.cs, `internal`) is the only path that turns an NPCDefinition into a combatant. It has two call sites (SetupEnemyEncounter and SpawnSummonedCreature) and three entry paths; the test presets and the scenario harness (`Harness_SpawnEnemies`) go through SetupEnemyEncounter. Encounter spawns reuse the pre-placed `GameManager.NPCs` objects that SceneBootstrap builds once (slots beyond the enemy count are deactivated); summons create a new GameObject with a CharacterController. `LionsShieldBehavior` builds its lion through its own copy of the setup on a new GameObject (CRE-005).

```
encounter:  SetupEnemyEncounter (NPCSetup.cs:128) -> NPCDatabase.Get -> BuildEncounterDefinitionForSpawn
            (GameManager.cs:2830, CreatureTemplateRegistry.ApplyTemplatesClone) -> InitializeNPCFromDefinition
            -> ApplyScenarioSpawnOverrides
dungeon:    DungeonEncounterSpawner.BuildSpawnDefinition (clone, class levels, templates)
            -> NPCDatabase.RegisterExternal("spawn_...") -> ApplyRandomEncounter -> SetupEnemyEncounter (as above)
summon:     GameManager.SpawnSummonedCreature (Spell/Resolution/GameManager.SpellCasting.cs:250)
            -> Clone, clear AppliedTemplateIds, add option template, ApplyTemplatesClone -> InitializeNPCFromDefinition
```

What `InitializeNPCFromDefinition` does:
- First `ResetCharacterSlotForSpawn(npc)` (CRE-046), so a reused pool slot starts as a fresh controller. It clears the links others keep to the controller: feint windows, counterspells readied against it and remembered squares (`CharacterController.ForgetCreature`, also the AI `LastKnownPositionTracker`), another creature's concentration on a spell effect that sits on it, a summon registration, Mirror Image state, a Turn Undead tracker, the `ConditionService` synced record (`ForgetCharacter`), aura save immunities in either direction (`AIService.ForgetAuraSaveImmunities`), AI spell plans and lessons (`AISpellcastingStrategist.ForgetCharacter`), curses, emanations centred on it and melee reaction effects. Then `CharacterController.ResetForNewCreature` clears the controller: the grapple link and pin state (the opponent is released), a mount, Command Undead links both ways, owned feint windows, every spell-effect record kept on the controller, a readied counterspell, turn and action-economy state, the attack pool, AI maneuver memory, feat toggles and the damage mode, ability-zero bookkeeping, diseases, poisons, every innate monster ability, HP state, AI profile and target priority, displayed race, tags, shield-bash AC suppression, sprite tint and visibility and running coroutines; it ends its concentration and destroys every component outside `SlotReuseKeptComponentTypes` (Transform, SpriteRenderer, the controller and its Awake helpers, ConditionManager, StatusEffectIndicator and InventoryComponent), so the old SpellcastingComponent, StatusEffectManager with its effects, ConcentrationManager and any tracker or effect behaviour go. Stats are replaced by `npc.Init` below. The old AI profile is destroyed only when the GameManager created it (`TrackRuntimeAIProfile`: BuildRuntimeAIProfile and the preset spawn overrides) and no other character shares it, so a profile the scenario harness assigned is never destroyed here. A new field or component on CharacterController that holds per-creature state must be cleared there too.
- Party slots are reused the same way: `GameManager.ResetPCSlotForNewCharacter` runs the same `ResetCharacterSlotForSpawn` on a party slot that already held a character, right before `Init`, in `SetupCreatedCharacters` (character creation and `Harness_SetupPartyFromCreation`), every `Configure*TestParty` preset and `Harness_SetupPartySlot`. It is never called between encounters for a continuing party member. `SetupCreatedCharacters` reuses the slot's `InventoryComponent` (kept by the reset) and re-initialises it with a new Inventory.
- Resolves the creature-type progression (Character/Creatures/CreatureTypeProgression.cs, 15 MM types; the definition's `BABOverride` and `*SaveOverride` replace it) and the racial Hit Dice (`NPCDefinition.ResolveRacialHitDice`: all HD when the class only stands in for them, else HD less the real class levels). Calls `new CharacterStats(...)` with `def.Level`, `def.CharacterClass` and no race, then sets `HitDice` and, through `CreatureTypeProgressionDatabase.ApplyToStats` (also used by the Lion's Shield summon's `LionsShieldBehavior.BuildSummonStats`), `RacialHitDice`, `RacialHitDiceStandInClass`, the four progressions and `BaseAttackBonusOverride`. BAB and base saves are then the shared `CharacterStats` sums: racial HD by the type's progressions plus every real class level by the class tables (MM p.290, PHB p.59; CRE-004); the stand-in class (`NPCDefinition.ResolveRacialHitDiceStandInClass`, set by `ApplyToStats` and again by `CreatureProficiency` below) is skipped. `NPCDefinition.BAB` is reference data only. The Undead progression uses Medium (3/4) BAB, and only the skeleton and zombie templates override it to Poor **[KI]**.
- Sets weapon and armor proficiency with `CreatureProficiency.ApplyFromDefinition` (Character/Creatures/CreatureProficiency.cs, CHR-072): `RacialHitDiceStandInClass` (the placeholder Warrior or empty-class Fighter level, from `NPCDefinition.ResolveClassLevelsAreRacialHitDice`, grants nothing), the MM type's simple/martial weapons and worn-armor proficiency (`CreatureTypeSimpleWeaponProficiency`, `CreatureTypeMartialWeaponProficiency`, `CreatureArmorProficiency`, `CreatureShieldProficiency`) and the entry's weapons in `ExtraWeaponProficiencies` (carried items only where the type grants entry weapons, giants and humanoids whose levels are racial HD included; `EntryWeaponIds` always; a real commoner's first carried simple weapon). A humanoid that keeps racial HD beside a class (`NPCDefinition.HasRacialHumanoidHitDice`, set by `CreatureClassEngine.ApplyClassToDefinition`) keeps the type's proficiencies. The checks are the PC ones, `IsProficientWithWeapon` and `IsProficientWithArmor`, whose class tables include the five NPC classes (DMG p.108-109). `LionsShieldBehavior` calls it too.
- Copies natural attacks, tags, feats (raw strings), then `FeatManager.ApplyPassiveFeats`; size, natural armor, trip/grab/pounce/rake/scent, exceptional stability (`IsExceptionallyStable`), DR, resistances, immunities, mindless, swarm, SR, template smite flags, special-ability text.
- Calls `npc.Init`, then `Configure*` for acid spray cooldown, regeneration, incorporeal, breath, secondary breath, frightful presence, engulf, ranged special, blood drain, terrain manipulation, stench and aura, each with the definition's value (null, false or 0 configures none).
- Copies `CharacterAlignment` (CRE-002), as set by the definition or the templates applied before it.
- Sets team/control from `IsAlly`/`IsControllable`, equips `EquipmentIds` through `Inventory.EquipStartingLoadout` (random material upgrades at CR >= 1; the MainHand, OffHand and Ranged aliases, ITM-004: a Ranged weapon is held by a creature the AI runs as a ranged attacker, `AIService.RoutesToRangedTurn` on the definition's behaviour and runtime profile, built before the gear, and carried by any other) and backpack items.
- Dragons get an injected `ClassLevelEntry("Sorcerer", CL)`, marked `InnateSpellcastingClass` so it is a caster level only and adds no BAB or saves (CRE-004); any caster with spell lists gets a SpellcastingComponent (a non-caster has none, since the reset removed the previous creature's). Adds StatusEffectManager, ConcentrationManager and `aiProfile = TrackRuntimeAIProfile(BuildRuntimeAIProfile(def))` (the Brute and Caster archetypes have no case and get null).

Alignment: the spawned alignment is the definition's (CRE-002, fixed 2026-10-08); a definition without one spawns as `Alignment.None` **[KI]** (CRE-056). The summon code then sets the Summon Monster table's alignment when the option has one and keeps the spawned one otherwise; `ApplyScenarioSpawnOverrides` still forces alignments for the template, summon and Protection from Evil test presets; `LionsShieldBehavior` copies it too. It does not copy `HasTrample` and `SwarmDamageCount`. Any new NPCDefinition field must be added here, in `Clone()`, and in the three template adapters' `CopyDefinitionFields`.

## Templates, dragons and class levels on monsters

**Template framework.** Character/Templates/CreatureTemplateFramework.cs defines `ICreatureTemplate` and `CreatureTemplateRegistry`, keyed by ID: skeleton, zombie (ApplicationOrder 10), werewolf, wererat, wereboar, weretiger, werebear (15), celestial, fiendish (20). `ApplyTemplatesClone` clones, resolves every ID in `AppliedTemplateIds`, sorts by ApplicationOrder ascending and applies each in place.

Each undead and lycanthrope template has two code paths:
- Factory path: `SkeletonTemplate.Apply` / `ZombieTemplate.Apply` / `LycanthropeTemplate.Apply` return a new definition. The `SkeletonFactory`, `ZombieFactory` and `LycanthropeFactory` presets use it to build the baked entries registered in NPCDatabase.
- Registry path: adapters (SkeletonCreatureTemplate, ZombieCreatureTemplate, LycanthropeTemplateBase subclasses) call the same Apply and copy back only a subset of fields, so the two paths give different results. The skeleton adapter always passes `hasHands: true`; lycanthrope adapters are always natural lycanthropes.

Known template problems **[KI]** (traced statically, not verified in Play mode):
- The factories stamp their own template ID into `AppliedTemplateIds` ("skeleton", "zombie", or "lycanthrope" plus the animal prefix). The field means both "to apply" and "already applied".
- BuildEncounterDefinitionForSpawn re-applies templates on every spawn with no idempotence check, so baked skeletons, zombies and lycanthropes are templated twice. DungeonEncounterSpawner applies templates before registering, and the encounter path applies them again. Only the summon path clears the list first.
- Celestial/fiendish add ability bonuses and grant SR only at 8+ HD, unlike the 3.5 Monster Manual.
- Lycanthropes are hybrid form only. Their bite exposes the target to `DiseaseType.Lycanthropy`, which has no DiseaseDatabase entry, so nothing happens. `CalculateLycanthropeCR` truncates (the werebear is CR 4).

**Dragons.** Character/Creatures/DragonData.cs holds 10 types (5 chromatic, 5 metallic) x 6 ages (Wyrmling to Adult). `NPCDatabase.BuildDragonDefinition` (NPCDatabase_Dragons.cs) builds 60 entries with IDs `dragon_<type>_<age>` (`DragonData.GetNPCId`):
- Warrior placeholder class, Good BAB and all-good save overrides, breath weapon, metallic secondary breath, frightful presence from Young Adult.
- `KnownSpellIds = PreparedSpellSlotIds` = the first N entries of a per-type thematic list (`DragonData.GetSpellsForCasterLevel`), not the sorcerer spells-known table.
- At spawn the injected Sorcerer level makes `Level` = HD + CL. `SpellLikeAbilityIds` are populated but never read.

**Class levels on monsters.** `CreatureClassEngine.ApplyClassToDefinition(def, classDef, levels)` (Character/CreatureClass/CreatureClassEngine.cs:140) mutates a cloned definition:
- HitDice += levels; `def.BAB` += class BAB (ignored at spawn); BaseHitDieHP += class HP (first die maxed, CON included, so CON is counted again by the constructor).
- CharacterClass and Level are replaced, and "<Class> <N>" is appended to SpecialAbilities.
- CR = round(base CR) + `CRCalculator.CalculateCRAdjustment`, an integer; association is by creature type (ClassAssociationRules), and NPC classes always count as nonassociated.
- It does not apply class saves, feats, skills, ability increases or spells, so class-leveled casters spawn without spells.

The only production caller is DungeonEncounterSpawner.BuildSpawnDefinition (Encounters/DungeonEncounterSpawner.cs:168); QuickSpawnSystem also calls it but is itself reached only from tests. StatArrayApplier and ECLTracker are used only by tests (ECLTracker is also the type of the `CharacterStats.ECL` field); `CharacterStats.ECL` is never assigned, so `EffectiveCharacterLevel == Level`.

**NPC-by-level templates.** Character/Templates/TemplateData.cs holds 70 DMG stat blocks (`NPCTemplate`: 11 PHB classes at levels 1/5/10/15/20, 5 NPC classes at 1/5/10), looked up by NPCTemplateDatabase. They are reachable only through QuickSpawnSystem (Utilities/), NPCTemplateAIConfigurator and TemplateSpellUpdater, and only Tests/Classes/NPCTemplateSystemTests.cs calls those. No gameplay or UI path uses them.

**Persistence.** There is no save/load system. Nothing writes CharacterStats or NPCDefinition anywhere: no JsonUtility, and the only file I/O reads encounter CSV tables (Encounters/EncounterCSVParser.cs, DungeonEncounterTableManager.cs). The HashSet and Dictionary fields on CharacterStats would not survive Unity serialization anyway. PlayerPrefs stores only UI window layout. Every run rebuilds characters from CharacterCreationData, quick-start presets, test configs or NPCDatabase.
