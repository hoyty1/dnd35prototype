> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-02.

# Spells

A spell is a data record plus GameManager code. `SpellDatabase` declares each spell as a `SpellData` and holds no behavior. `SpellcastingComponent` (one per caster) owns known spells and slots. Behavior lives in GameManager partials: targeting and the cast pipelines are in `Spell/Resolution/GameManager.SpellCasting.cs` (8,957 lines), per-spell `TryResolve*` handlers are in `Spell/Resolution/GameManager_Spells_*.cs`, `Spell/Special/` and `Spell/Domain/`, and there are two generic fallbacks: `SpellCaster.Cast` for dice and `StatusEffectManager.AddEffect` for the `Buff*` fields. There is no dispatch table. Every handler string-compares `spell.SpellId` with a `SpellNames` constant.

| Concern | Where to look |
|---|---|
| Definitions and IDs | `Spell/Data/SpellData.cs`, `Spell/Database/SpellDatabase.cs` and `SpellDatabase_<Letter>.cs`, `Spell/Data/SpellNames.cs` |
| Ranges | `Spell/Data/SpellRanges.cs`, `SpellData.GetRangeSquaresForCasterLevel` |
| Per-caster resources | `Spell/Components/SpellcastingComponent.cs` (3,358 lines), `Spell/Data/SpellSlot.cs`, `Spell/Data/SpontaneousCastingData.cs`, `Spell/Casting/PartialCasterData.cs` |
| Cast button to targeting | `_Core/GameManager.cs` (`OnCastSpellButtonPressed`, `OnSpellSelectedWithMetamagic`), `GameManager.SpellCasting.cs` (`BeginPendingSpellTargeting`) |
| Cast pipelines | `GameManager.SpellCasting.cs` (`PerformSpellCast`, `PerformAoESpellCast`, `TryConsumePendingSpellCast`, `ConsumePendingSpellSlot`, `ApplySpellBuff`); NPCs: `_Core/GameManager.NPCTurns.cs` (`TryNPCPerformSpellCast`) |
| Generic resolution | `Spell/Casting/SpellCaster.cs`, `Spell/Components/StatusEffectManager.cs`, `Spell/StatusEffects/ActiveSpellEffect.cs` |
| DC, caster level, saves, SR | `Spell/Casting/SpellUtilities.cs`, `SpellCastingHelper.cs`, `SpellSaveResolver.cs`, `SpellCaster.cs` |
| Durations | `GameManager.SpellCasting.cs` (`TickAllSpellDurations`, `TickCharacterSpellDurations`), `Spell/Effects/EffectService.cs` |
| Areas and walls | `Spell/AreaEffects/` (`PersistentAreaEffect`, `AreaEffectManager`, `AoESystem`, `LineOfEffectService`), `Spell/Special/` |
| Metamagic | `Spell/Components/MetamagicData.cs`, `UI/Spells/SpellPreparationUI.cs`, `UI/Combat/CombatUI.cs` (`ShowMetamagicPanel`), `SpellCaster.ApplyMetamagicToSpellData` |
| Domains | `Character/Religion/DomainDatabase.cs`, `Spell/Domain/GameManager_Domain*.cs` |
| Summoning | `Spell/Components/SummonMonsterLists.cs`, `GameManager.SpellCasting.cs:240-811`, `_Core/GameManager.cs` (`_activeSummons`, selection menus) |

## Spell data model

**SpellData** (`Spell/Data/SpellData.cs`) is a plain class filled with object initializers. The fields that matter:

| Group | Fields | Notes |
|---|---|---|
| Identity | `SpellId` (snake_case key), `Name` (display), `School` (free text), `SpellLevel` | 40 spells use decorated schools such as "Evocation [Fire]" |
| Class and level | `ClassList` (legacy), `AvailableFor` (`List<SpellAvailability>`: ClassName, Level, Domain) | per-class levels exist only in `AvailableFor` |
| Targeting | `TargetType` (Self, SingleEnemy, SingleAlly, Touch, Area), `RangeCategory`, raw `RangeSquares`/`RangeIncreasePerLevels`/`RangeIncreaseSquares`, `IsTouch`/`IsMeleeTouch`/`IsRangedTouch` | |
| Area | `AoEShapeType` (`AoEShape.None` = single target), `AoESizeSquares`, `AoERangeSquares`, `AoEFilter`, `AreaRadius` | `AoEShapeType`, not `TargetType`, decides AoE routing |
| Effect | `EffectType` (12 values), `DamageDice` (die sides), `DamageCount`, `BonusDamage`, `DamageType`, `AutoHit`, `MissileCount`, `HealDice`/`HealCount`/`BonusHealing` | fixed numbers; they do not scale with caster level. Exception: for `AutoHit` spells with `MissileCount > 0` (Magic Missile), `SpellCaster.Cast` computes the missile count from `GetCasterLevel()` |
| Save and SR | `AllowsSavingThrow`, `SavingThrowType` (the strings "Reflex"/"Will"/"Fortitude"), `SaveHalves`, `SaveDC`, `SpellResistanceApplies` | `SaveDC > 0` overrides the DC rule (`SpellSaveDCRules`) on every path; scroll and wand casts set it. Ghoul Touch's stench reads the template's DC rule, not the cast's clone. SR is opt-in: true on 91 of 292 spells |
| Tracked buff | `Buff*` (AC, attack, damage, save, stat, skill, shield, deflection, temp HP, speed, DR, resistance, immunity), `BuffBonusType`, legacy `BuffType` string | read by `StatusEffectManager.AddEffect` |
| Duration | `DurationType`, `DurationValue`, `DurationScalesWithLevel`, legacy `BuffDurationRounds` | see Durations |
| Casting | `ActionType`, `ProvokesAoO`, `HasVerbalComponent`, `HasSomaticComponent`, `HasMaterialComponent`, `HasDivineFocus` | |
| Flags | `Descriptors` (`SpellDescriptor` flags), `IsMindAffecting` (separate bool), `BlockedByProtectionFromAlignment`, `IsPlaceholder`, `PlaceholderReason` | only the bool is read for mind-affecting immunity; `BlockedByProtectionFromAlignment` (not `IsMindAffecting`) drives the protection-from-alignment block in `SpellCaster` |
| Metamagic | `BaseSpellLevel`, `EffectiveSpellLevel`, `AppliedMetamagics`, `MetamagicDataRef`, `Clone()` | `Clone()` deep-copies `AvailableFor`/`AppliedMetamagics` and nulls `MetamagicDataRef` |

**SpellDatabase** (`Spell/Database/SpellDatabase.cs`) is a `static partial class`. `Init()` is lazy (`GetSpell`, `GetAllSpells` and others call it). It sets `_initialized = true` before populating; that stops re-entry from `SpellCategoryClassifier`, but an exception mid-registration leaves a permanently half-filled database. The order is `RegisterSpellsA()` to `RegisterSpellsZ()` (23 files `SpellDatabase_<Letter>.cs`, named by the first letter of the spell name, no Q/X/Y), then `AnnotateDomainAvailabilityFromDomainDatabase`, `AnnotateSpellDescriptors`, `SpellComponentRegistry.Init` and `SpellCategoryClassifier.ReclassifyAll`. `Register` calls `EnsureAvailabilityFromLegacyClassList` and silently overwrites a duplicate `SpellId`.

There are 292 `Register` calls. By `SpellLevel`:

| Level | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---|---|---|---|---|---|---|---|---|---|
| Spells | 27 | 66 | 82 | 44 | 43 | 12 | 9 | 4 | 4 | 1 |

47 spells have `IsPlaceholder = true` and no mechanics. The flag does not block casting. It only filters Cleric/Druid auto-prepare (as a preference; placeholders are used when nothing else fits), `GetImplementedSpellsForClass` / `GetImplementedSpellsForClassAtLevel`, NPC template spell lists (`TemplateSpellValidator`, `TemplateSpellUpdater`), staff spell lists (`StaffDefinition`) and the scroll, wand and potion factories, and it adds a placeholder marker in several UIs (`[PH]`, `[P]`, a star). It is stale for Summon Monster I and II, which work. The header comment in `SpellDatabase.cs` ("~140 spells", levels 0-2) is out of date.

**IDs.** `SpellNames` (`Spell/Data/SpellNames.cs`, namespace `DND35e.Identifiers`) has 318 `const string` IDs. Some are deprecated constants equal to a canonical ID (`BLINDNESS_DEAFNESS_WIZ/_CLR/_BRD`, `DETECT_MAGIC_WIZ`, `RESISTANCE_WIZ`, `DOMAIN_*`), some are legacy IDs kept only as database aliases (`HIDEOUS_LAUGHTER_LEGACY`, `SEE_INVISIBILITY_LEGACY`), and some are not registered at all (for example `PROTECTION_FROM_ENERGY_ACID`/`_COLD`/`_ELECTRICITY`/`_FIRE`/`_SONIC`). The `SpellID` enum (`Spell/Data/SpellID.cs`) has no runtime consumers: only `Utilities/IdentifierExtensions.cs`, the unread `ItemData.ConsumableSpellIDEnum` property and `Identifiers/EnumTest.cs` reference it. A few raw literals remain, for example `"power_word_stun"` in `ApplySpellBuff`, which is not a registered spell.

**Aliases and lookup.**
- `RegisterAlias(alias, canonical)` only maps an ID. `RegisterClassSpellAlias(alias, canonical, className, level, domain = null)` also calls `canonical.AddAvailability`. That works only if the canonical spell is already registered (files run A to Z); otherwise it logs a warning. There are 132 `RegisterClassSpellAlias` and 23 `RegisterAlias` calls. The `domain_*`, `*_wiz` and `blindness_deafness_{wiz,brd,clr}` IDs were consolidated in aa1ad37 and must keep resolving.
- `GetSpell(id)` looks in `_spells` (case-sensitive), then in the alias map (case-insensitive), and returns the canonical `SpellData`, so `spell.SpellId` can differ from the ID passed. It logs a warning on a miss.
- `GetSpellByName(name)` is a case-insensitive linear scan of `Name`. Item code uses `GetSpell(x) ?? GetSpellByName(x)`, which logs a spurious "Spell not found" warning whenever `x` is a display name.
- Database instances are shared templates; mutate only a `Clone()` (see [Cross-cutting conventions](../ARCHITECTURE.md#cross-cutting-conventions)).

**Per-class levels.** `SpellLevel` is a single primary level. When a class has a different level, an `AvailableFor` entry added by `RegisterClassSpellAlias` holds it (Bestow Curse: 3, Wizard/Sorcerer 4; See Invisibility: 2, Bard 3). `EnsureAvailabilityFromLegacyClassList` turns `ClassList` into entries at `SpellLevel` and adds Sorcerer when Wizard is listed (and the reverse). `IsAvailableFor(class, level)` requires an exact level and ignores domain entries. `GetSpellLevelFor(class, domain)` returns the lowest matching level. Item factories and spontaneous learning use per-class levels, but slot validation, spontaneous `CanCast` and every DC formula read `SpellLevel`. So a Bard cannot cast See Invisibility, a Sorcerer cannot cast Bestow Curse, and `InitWizard` drops Bestow Curse and Contagion from the spellbook (tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md)).

**Ranges.** `SpellRangeCategory` (`Spell/Data/SpellRanges.cs`), in squares: Custom, Personal (-1), Touch (1), Close (5 + 1 per 2 CL), Medium (20 + 2 per CL), Long (80 + 8 per CL), Unlimited (9999). The `RangeCategory` setter calls `SpellRanges.Configure`, which overwrites the raw range fields. `GetRangeSquaresForCasterLevel(cl)` uses the category profile for any non-Custom category and ignores the raw fields. 249 spells set `RangeCategory` (Touch 92, Close 55, Medium 47, Personal 39, Long 14, Custom 2). The other 43 set raw `RangeSquares`, which `SpellRanges.TryDetectCategory` promotes to a category only on an exact match. AoE placement uses a fixed `AoERangeSquares` when it is above 0. `CreateWithRange` and `SetRange*` are used only by tests.

**EffectType is rewritten at init.** `SpellCategoryClassifier.ReclassifyAll` (`AI/SpellCategoryClassifier.cs`) runs inside `SpellDatabase.Init` and replaces the registered Damage/Healing/Buff/Debuff with Control, Summon, Utility, Escape, Dispel, Wall, Illusion or Divination using hard-coded spell-ID lists plus one heuristic (a mind-affecting, damage-free Debuff with a Will save becomes Control). Any code that switches on `EffectType`, AI included, sees the new category. What you write in `SpellDatabase_<Letter>.cs` is not what runtime sees. The pipelines branch on `EffectType`: `SpellCaster.Cast` only has Damage/Healing/Buff/Debuff branches, and only Buff/Debuff/Control/Illusion/Wall reach `ApplySpellBuff`. When a spell silently does nothing, check its reclassified type first.

**Adding a spell.** Add a `SpellNames` constant. `Register(new SpellData { ... })` in `SpellDatabase_<Letter>.cs` with `RangeCategory`, `TargetType`, `AoEShapeType`, `EffectType`, duration and `Buff*` fields; put differing per-class levels in `RegisterClassSpellAlias`. Check what the classifier turns `EffectType` into. If dice or `Buff*` fields describe the effect, no code is needed. Otherwise write a handler and wire it into the right chains (see [Cast pipelines](#cast-pipelines)) and into every side-state cleanup list (see [Durations and ticking](#durations-and-ticking)). Add `Tests/Combat/<Name>RulesTests.cs` with a static `RunAll()`. Step-by-step recipes: "Add a spell (data)", "Implement a single-target, touch or self spell effect" and "Implement an area spell" in [DEVELOPMENT_RECIPES.md](../DEVELOPMENT_RECIPES.md).

Data deviations, one line each (tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md)): `AnnotateSpellDescriptors` uses `magic_circle_vs_*` (real IDs: `magic_circle_against_*`) and other nonexistent IDs, which `SetDescriptor` skips silently; Flame Strike is `SpellLevel` 2 (PHB 5); Bane and Sound Burst are single-target; Heal is `HealDice = 1` with a "handled manually" comment, but the manual handler is unreachable (see the `ApplySpellBuff` trap).

## Casting resources

`SpellcastingComponent` is added at setup: for PCs in `GameManager.SetupCreatedCharacters` (`_Core/GameManager.cs` around 2066-2085), for NPCs in `GameManager.NPCSetup.cs` (dragons get an injected Sorcerer class level). `Init(stats)` builds three independent subsystems:

| Subsystem | Classes | State | Cast and spend |
|---|---|---|---|
| Prepared slots | Wizard, Cleric, Druid, in that order; multiclass pools keyed by `SpellSlot.CasterClassName` | `SpellSlots` (`SpellSlot`: Level, PreparedSpell, IsUsed, DisabledByNegativeLevel, LockedByImbue, IsDomainSlot, IsSpecialistSlot, AppliedMetamagic). `CanCast` = prepared, not used, not disabled, not locked | `CastSpellFromSlot`, `CastWizardSpellWithMetamagic` |
| Spontaneous | Sorcerer or Bard (Sorcerer wins if both) | `SpontaneousData` (`SpontaneousCastingData`): known spells per level, PHB slots per day, CHA bonus slots, learn/forget/swap | `SpontaneousData.CanCast(id, spell.SpellLevel)`, `SpendSlot` |
| Partial | Ranger, Paladin | `PartialData` (`PartialCasterData`): slot counts | none at runtime; display only |

- **Slot tables are capped for the prototype.** `GetWizardSlotsForLevel` stops at 4/3/2 (no 3rd-level slots) from level 4, and `GetDruidSlotsForLevel` stops at 4/2/1 from level 3. `GetClericBaseRegularSlotsForCasterLevel` follows the PHB up to CL 9, but `InitCleric` adds only cleric spells of levels 0-2 to the known list, so cleric slots of level 3+ stay empty. The database has 117 spells of levels 3-9 that prepared casters cannot reach. Bonus slots for prepared casters are a flat +1 when the ability modifier is at least the spell level; the spontaneous and partial tables use the PHB 1 + (mod - level) / 4.
- **Bonus slot kinds.** Clerics with any domain get one domain slot per castable level from 1 up. Specialist wizards get one specialist slot per castable level, cantrips included. Ring of Wizardry doubles regular wizard slots.
- **Preparation.** With exactly one prepared class and `PreparedSpellSlotIds` non-null (class quickstarts, `NPCTemplateAIConfigurator`), `ApplyPreparedSpellSlotIds` fills slots by position (regular slots, then that level's bonus slots); a different WIS/INT or domain count shifts entries into the wrong slots. A non-null empty list also skips auto-prepare; `SetupCreatedCharacters` passes the creation data's list through on purpose ("start with no prepared spells"). Otherwise `AutoPrepareWizardSlots`/`AutoPrepareClericSlots`/`AutoPrepareDruidSlots` fill slots round-robin. Manual preparation is `SpellPreparationUI` -> `PrepareSpellInSlot` or `PrepareSpellInSlotWithMetamagic`, validated by `IsValidSpellForSlot`. Domain-only spells (Enlarge Person for Strength, Disguise Self for Trickery) fail manual preparation because the class-list check excludes domain entries; only auto-prepare fills those domain slots.
- **Cantrips** are prepared but never marked used, for every caster type.
- **Legacy mirrors.** `SlotsMax` and `SlotsRemaining` mirror only the first prepared class (or the spontaneous data). `PreparedSpells` is rebuilt by `SyncPreparedSpellsFromSlots` as the de-duplicated list of currently castable spells across all slots of all classes. `ActiveBuffs` and `MageArmorActive`/`MageArmorACBonus` mirror `StatusEffectManager`. `KnownSpells`, `SpellSlots` and `PreparedSpells` are mutable lists that `SpellTestingPanel`, `NPCTemplateAIConfigurator`, `TemplateSpellUpdater` and `GameManager.TestConfigs` edit directly, bypassing validation.
- **Other per-caster state:** held melee touch charge (`HasHeldTouchCharge`, `ClearHeldTouchCharge`), one quickened spell per round (`MarkQuickenedSpellCast`, reset by `GameManager.OnNewRound`), negative-level slot loss (`ApplyNegativeLevelSlotLoss`), Imbue with Spell Ability (`ImbueWithSpellAbilityManager`, `SpellSlot.LockedByImbue`), and costly material components (`SpellComponentRegistry`: four spells registered, only Stoneskin enforced, after the slot is spent).
- **Cleric spontaneous cure/inflict.** The convert button in `CombatUI` sets `CombatUI.IsSpontaneousCast`. `ConsumePendingSpellSlot` then calls `SpontaneousCastFromSpecificSpell` or `SpontaneousCastFromSlot`. `SpontaneousCastingHelper.GetSpontaneousSpellId` (`Spell/Database/SpontaneousCastingType.cs`) covers levels 0-4.

**Rest.** There is no per-day model. `GameManager.RestorePartyAfterCombat` (`_Core/GameManager.cs:868`) runs after every won combat (from `ContinueToRestAndNextCombat` in `_Core/GameManager.LootCollection.cs`): it clears all area and wind effects, despawns summons, then per PC calls `StatusEffectManager.RemoveAllEffects`, `ClearHeldTouchCharge`, `RestoreAllSlots`, `ActiveBuffs.Clear`, per-spell `Clear*Effect` calls, and domain-power use counters reset. `RestoreAllSlots` returns early for a spontaneous or partial caster, so a multiclass that includes Sorcerer, Bard, Ranger or Paladin never restores its prepared slots. `RefreshSpellSlots` (level-up in `CharacterStats`, Ring of Wizardry equip/unequip, character creation) rebuilds the slots and restores only `PreparedSpell`, `IsUsed` and `DisabledByNegativeLevel`, so metamagic preparations and imbue locks are lost.

Known deviations, one line each (tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md)): a multiclass with Sorcerer or Bard takes the spontaneous branch first in `CastSpellFromSlot`, `GetCastablePreparedSpells` and `CanCastSpell`, so it cannot cast prepared spells; Rangers, Paladins and Adepts cannot cast in combat; `CastSpellFromSlot` takes the first unused slot holding the spell and ignores `DisabledByNegativeLevel`, `LockedByImbue` and metamagic; the last resort in `CastWizardSpellWithMetamagic` consumes any unused slot at the effective level, whatever it holds.

## Cast pipelines

**Player entry.** The Cast button calls `GameManager.OnCastSpellButtonPressed`, which requires a standard action (so a quickened spell cannot be cast once the standard action is spent). `CombatUI.ShowSpellSelection` then calls back `OnSpellSelectedWithMetamagic`, which sets `_pendingSpell` and `_pendingMetamagic` and resets most other `_pending*` fields. With metamagic it sets `_pendingSpell = spell.Clone()` and calls `SpellCaster.ApplyMetamagicToSpellData`. Optional Grease, Animate Rope and hold-charge prompts follow, then `BeginPendingSpellTargeting` (`GameManager.SpellCasting.cs:813`). Scrolls and wands enter `BeginPendingSpellTargeting` from `InitiateScrollCastThroughPipeline` / `InitiateWandCastThroughPipeline` (`_Core/GameManager.cs:5947`, `6074`), and the F12 panel from `GameManager.TestCastSpellFromPanel`.

**Routing in `BeginPendingSpellTargeting`:**

```
Resilient Sphere block, grapple restrictions
Grease area / Grease armor mode -> EnterGreaseAreaTargetingMode / EnterGreaseArmorTargetingMode
Disguise Self race dialog -> PerformSpellCast(caster, caster) directly
choice dialogs (Resist/Protection from Energy type, Fire Shield, Wall of Fire/Ice mode);
  each re-enters BeginPendingSpellTargeting
AoEShapeType != None   -> EnterAoETargetingMode -> HandleAoETargetClick / self confirm -> PerformAoESpellCast
IsSummonMonsterSpell   -> ShowSummonCreatureSelectionMenu -> placement click -> PerformSummonMonsterCast
IsSummonSwarmSpell     -> ShowSummonSwarmSelectionMenu -> placement click -> PerformSummonSwarmCast
WISH                   -> HandleWishSpellCast (Spell/Special/WishExecutor.cs)
TargetType.Self        -> PerformSpellCast(caster, caster)
otherwise              -> ShowSpellTargets -> click (GameManager.CombatActions.cs) -> PerformSpellCast
```

Routing depends on `AoEShapeType`, not `TargetType`. Consecrate, Desecrate, Calm Animals, Wall of Thorns and Persistent Image are `TargetType.Area` with no shape, so they go to `PerformSpellCast` and their AoE handlers never run. Prayer is `Self` with `AoEShape.Burst`, so its single-target handler never runs. Tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md).

**The four pipelines.** Each one repeats the cast steps by hand, and they have drifted:

| Pipeline | Used for | Steps | Missing compared with `PerformSpellCast` |
|---|---|---|---|
| `PerformSpellCast` (`GameManager.SpellCasting.cs:1629`, about 860 lines) | PC single target and self | target validation, item pickers, component pouch check, action spend, entangled and grappled concentration, ASF, Blink 20% caster failure, `ConsumePendingSpellSlot`, `HandleConcentrationOnCasting`, `ResolveSpellcastProvocation` (AoO); in its callback `TryResolveCounterspell`, `CounterspellManager.TryRingCounterspell`, invisibility break, Mirror Image redirect, Blink 50% target miss, `SpellCaster.Cast`, 51 handlers, `ApplySpellBuff` | reference path |
| `PerformAoESpellCast` (`:3567`, about 1,275 lines) | every spell with an `AoEShapeType`; wall placement flows | line-of-effect filtering, action, grappled concentration, ASF, slot, concentration on cast, AoO, counterspell, 40 first-match handler blocks, then a generic per-target loop | entangled concentration, pouch check, Blink, Ring of Counterspells, Mirror Image redirect |
| `TryNPCPerformSpellCast` (`_Core/GameManager.NPCTurns.cs:779`) | AI casts via `TryNPCPerformSpellCastForAI` | range from `GetCasterLevel()`, entangled and grappled concentration, `CastSpellFromSlot`, ASF, Blink, `ResolveNPCSpellcastProvocation` (AoO or defensive casting, chosen by `AISpellcastingStrategist.ShouldCastDefensively`, rolled by the shared `ResolveThreatenedSpellcast`), counterspell, Ring of Counterspells, Mirror Image, `SpellCaster.Cast` with null metamagic, save negation via `SpellUtilities.IsEffectNegatedBySave` (shared with `PerformSpellCast`), 17 handlers, `ApplySpellBuff` | returns false for every `TargetType.Area` spell; no concentration on cast or tracking, no pouch, no metamagic |
| `TryConsumePendingSpellCast` (`:34`) plus custom code | `PerformSummonMonsterCast`, `PerformSummonSwarmCast`, Grease area/object/armor casts | action (full round for spontaneous metamagic, quickened flag), entangled and grappled concentration, ASF, slot, then `HandleConcentrationOnCasting` itself; the caller then runs `ResolveSpellcastProvocation` (the three Grease casts call `HandleConcentrationOnCasting` a second time) | all counterspell checks, pouch check, Blink |

`ConsumePendingSpellSlot` (`:162`) spends the resource: scroll -> `ConsumeScrollAfterCast`; wand -> `ConsumeWandChargeAfterCast`; cleric conversion -> `SpontaneousCastFromSpecificSpell` / `SpontaneousCastFromSlot`; metamagic above level 0 -> `CastWizardSpellWithMetamagic`; otherwise `CastSpellFromSlot`. The slot is spent before the AoO prompt and before resolution. `CaptureSpellcastResourceSnapshot` restores it only if the player cancels at the AoO prompt.

**Handler chains.** A handler is a private GameManager method that typically starts with `if (spell == null || spell.SpellId != SpellNames.X) return false;` (some use an `Is<Name>Spell(spell)` helper instead) and returns true once it owns the spell, even if the effect failed (`if (!result.Success) return true;`).

| Kind | Signature | Called from |
|---|---|---|
| Single target | `bool TryResolve<Name>SpellEffect(CharacterController caster, CharacterController target, SpellData spell, SpellResult result)` | `PerformSpellCast` (`:2140-2357`); 16 of them also from the NPC chain (`NPCTurns.cs:891-950`) |
| AoE | `bool TryResolve<Name>Spell(caster, spell, List<CharacterController> targets, HashSet<Vector2Int> aoeCells, out string log)` | `PerformAoESpellCast` (`:3772-4677`); each `if` block repeats the log and pending-state cleanup, then returns |
| Tracked buff | `ActiveSpellEffect Apply<Name>Buff/Effect(caster, target, spell, SpellcastingComponent spellComp)` | spell-ID special cases in `ApplySpellBuff` |

File layout: `Spell/Resolution/GameManager_Spells_<FirstLetter>.cs`, alphabetical by spell name since 3037628. Exceptions: older handlers in `GameManager.SpellCasting.cs` itself (Sleep, Color Spray, Fear, Hypnotism, Cause Fear, Ghoul Touch, Scare, Melf's Acid Arrow and others); `GameManager_Spells_Shared.cs` (`TryResolveScaledAoEDamageSpell` for Fireball, Lightning Bolt and Call Lightning, the alignment bursts, cleric tick wrappers); `_Phase1.cs` and `_Phase2.cs` (staff spells from bc77db6 and 8e8ee86); `_Cantrips.cs` and `_MagicFang.cs`; `GameManager.DispelCounterspell.cs` (thin wrappers such as `PerformTargetedDispel` and `TryResolveCounterspell` that forward to `Services/DispelMagicService.cs`); Animate Rope and Magic Weapon in `_Core/GameManager.cs`; `Spell/Special/GameManager_<Feature>.cs` (Grease, walls, Mirror Image, Flaming Sphere, holy areas, concealment areas) and `WishExecutor.cs`; `Spell/Domain/` for domain spells. Comments that mention `GameManager_NewSpells.cs` refer to a deleted file.

The single-target chain is a cascade of `if (!handledA && !handledB && ...)` guards up to 20 terms long. A handler runs only if every earlier flag is false. To add one: append it to the right group, add its flag to the aggregate booleans (`anyPriorHandled`, `anyClericHandled`, `anyCleric4Handled`) that gate `ApplySpellBuff`, and add it to the NPC chain, or NPC casts fall through to the generic path.

**Generic fallbacks.**
- `SpellCaster.Cast(spell, casterStats, targetStats, metamagic, forceFriendlyTouchNoRoll, forceTargetToFailSave, casterController, targetController)` runs for every single target before the handlers, and for Damage and Healing targets in the AoE loop. Stages: effective level (Heighten), deafened 20% verbal failure, touch attack against touch AC, concealment, protection-from-alignment mental block, mind-affecting immunity, spell-specific target gates, SR, Shield against Magic Missile, save, damage (`DamageCount`d`DamageDice` + `BonusDamage`, Empower/Maximize, then `ApplyIncomingDamage`), healing (`HealCount`d`HealDice` + `BonusHealing`, no reversal against undead), buff and debuff flags. Handlers read the returned `SpellResult`.
- `ApplySpellBuff` (`:6371`, about 1,515 lines) runs only if no handler claimed the spell, `result.Success` is true, the reclassified `EffectType` is Buff/Debuff/Control/Illusion/Wall, and no save negated a Debuff or Control. About 50 spell-ID special cases come first. The generic branch then calls `StatusEffectManager.AddEffect(spell, casterName, caster.Stats.Level)`, mirrors the result into `ActiveBuffs`, registers a Magic Circle emanation if needed, and returns.
- `StatusEffectManager.AddEffect` applies stacking: the same spell keeps the longer duration, and the same core `BonusType` keeps the higher value except Dodge, Untyped, Circumstance and Luck (Luck stacking is a house rule, labelled as such at `Spell/BonusType.cs:37`, `:151` and `Spell/Components/StatusEffectManager.cs:15`). It copies `Buff*` into `Applied*` and calls `ApplyStatModifications`. Attack, damage and save bonuses always go to `MoraleAttackBonus`/`MoraleDamageBonus`/`MoraleSaveBonus`, whatever the declared type. AC is assigned (`=`) to `SpellACBonus`, so Mage Armor, Barkskin and Haste overwrite each other.

**The `ApplySpellBuff` early-return trap.** The generic branch is `if (target.StatusEffectManager != null) { ...; return effect; }` (`GameManager.SpellCasting.cs:7659-7710`), and every PC, NPC and summon gets a `StatusEffectManager` at setup (`_Core/GameManager.cs:2086`, `_Core/GameManager.NPCSetup.cs:971`). The 19 branches after it (`:7712-7832`: Telekinesis, Continual Flame, Levitate, Shrink Item, Barkskin, Passwall, Globe of Invulnerability, Disintegrate, Protection from Spells, Spell Turning, Heal, Resurrection, True Seeing, Mislead, Magic Fang, utility cantrips, Dancing Lights, Plane Shift, Alter Self) and the legacy fallback (`:7834-7884`) never run in normal play. Their `Apply*` methods in `_Phase1.cs`, `_Phase2.cs`, `_MagicFang.cs` and `_Cantrips.cs` are effectively dead, and Heal heals 1d1. Put any new special case above line 7659. Tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md).

**Pending state.** Cast state lives in GameManager fields (`_pendingSpell`, `_pendingMetamagic`, `_pendingResistEnergyType`, `_pendingFireShieldIsWarm`, `_pendingScrollCastActive`, `_pendingWandCastActive`, `_pendingSummonSelection`, ...). Every exit path resets them by hand, and the subsets differ: the AoE ASF and concentration-failure exits clear only `_pendingSpell` and `_pendingMetamagic`. `PerformSpellCast` calls `CleanupTestPanelCast` first, so F12 single-target, touch and self casts get none of the test-panel bypasses (ASF, pouch, Stoneskin components); AoE, summon, Grease and held-touch casts never clear the flag, so they skip ASF and the flag leaks into the next cast until a `PerformSpellCast` or a targeting cancel (details in [UI: debug and test tools](ai-encounters-ui.md#ui-debug-and-test-tools), [SPL-036](../issues/SPL.md)).

Other pipeline deviations, one line each (tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md)): the generic AoE loop applies Buff/Debuff/Control effects to every creature in the area with no save and no SR (Hypnotic Pattern); 22 `Stats.TakeDamage` calls in `Spell/` skip energy resistance, immunity and DR (Fireball, Lightning Bolt, Cone of Cold, Ice Storm, the walls), and Spike Stones, Black Tentacles and Produce Flame (`Spell/Domain/GameManager_DomainSpells.cs:332`) subtract `CurrentHP` directly (Produce Flame was found while checking this section and is not in the verified issue list); NPCs never cast Area spells (see [NPC AI: what it cannot do](ai-encounters-ui.md#npc-ai-what-it-cannot-do)).

## Saves, DC, caster level and SR

The save DC has one rule for every path (SPL-001); the save roll, caster level and SR still have several implementations, and the one a spell uses depends on which path resolves it (SPL-122, SPL-017).

| Concern | Implementation | Used by | Behavior |
|---|---|---|---|
| Save DC | `SpellSaveDCRules` (`Spell/Casting/SpellSaveDCRules.cs`, SPL-001, 2026-10-09) | every spell path: `SpellCaster.Cast` (generic single-target PC and NPC path, AoE Damage loop), `SpellUtilities.GetSpellSaveDC`, the private `GameManager.GetSpellSaveDC` in `GameManager_Grease.cs` (every per-spell handler, including Sleep, Deep Slumber, Hypnotism, Fear, the alignment bursts, Dismissal, Poison, Ghoul Touch's stench, Sanctuary, Wall of Ice), `SpellcastingComponent.GetSpellDC`, Ring of Spell Storing, Imbue with Spell Ability | a pre-baked `SaveDC > 0` is kept; else 10 + the casting class's level for the spell (heightened level with Heighten) + that class's key ability (PHB p.177; Adept WIS, DMG p.108) + Spell Focus/Greater Spell Focus for the parsed school + gnome illusion +1. `GameManager.GetSpellSaveDC` passes `_pendingMetamagic` when the spell is `_pendingSpell`; no GameManager partial calls the static `SpellUtilities.GetSpellSaveDC`. For a multiclass prepared caster the class is that of the slot just spent (`SpellcastingComponent.GetCastingClassForSpellDC`). `ExplainSpellLikeAbility` (MM p.315) and `ForMagicItem` (DMG p.214) are the spell-like and item variants. Verified in Play mode 2026-10-09 (`rules/spell-save-dc`) |
| Save DC | `SaveDC` baked into the clone | scrolls and wands (`InitiateScrollCastThroughPipeline`, `InitiateWandCastThroughPipeline`, `BuildConsumableSpellVariant`) | the item DC, `SpellSaveDCRules.ForMagicItem` (DMG p.214), unless crafting baked another (crafted scrolls bake the crafter's DC, ITM-008) |
| Save DC | `ImbueSpellEntry.SaveDC` (`ImbueWithSpellAbilityManager` computes it through `SpellSaveDCRules` with the slot's class) | nothing | stored and logged only: `GetEffectiveSaveDCForImbue` / `GetEffectiveCasterLevelForImbue` (`Spell/Resolution/GameManager_Spells_I.cs`) have no callers and the entry's `SpellData` is the shared, un-cloned instance, so imbued casts use the casting creature's own DC and CL (inferred from code, not verified in Play mode) |
| Save DC | `CombatCalculationService.SpellSaveDC`, `SpellUtilities.GetSpellSaveDC(level, mod)`, `SpellSaveResolver.CalculateDC` | tests only | the bare arithmetic 10 + level + modifier |
| Save roll | `SpellCaster.GetSaveModifier` | `SpellCaster.Cast` | adds Still Mind, protection from alignment, Charm Person +5 when threatened, Hideous Laughter +4, Remove Fear. `SavingThrowType` must match "Reflex"/"Will"/"Fortitude" exactly |
| Save roll | `SpellSaveResolver.RollSave` | per-spell handlers | raw Fort/Ref/Will totals only. The same class has `ApplyEvasion` and `ApplyBlinkHalving` |
| Caster level | `SpellCastingHelper.GetEffectiveCasterLevel` = max(1, `Stats.GetDomainBoostedCasterLevel(spell)`) | most handlers | `GetCasterLevel()` with no class returns the highest casting-class level minus negative levels, not the level of the class that cast. Ranger and Paladin count their full level |
| Caster level | `caster.Stats.Level` (total character level) | single-target range (`ShowSpellTargets`), generic `ApplySpellBuff` durations, summon range and duration | wrong for multiclass characters |
| Caster level | `Stats.GetCasterLevel()` | AoE placement range, NPC range, SR in `SpellCaster` | |
| SR | `SpellCaster.Cast` | generic path | only if `SpellResistanceApplies`: d20 + `GetCasterLevel()` + Spell Penetration |
| SR | `SpellSaveResolver.RollSpellResistance(caster, target, cl)` | per-spell handlers | the caller decides whether SR applies |

Unused duplicates: `SpellSaveResolver.CalculateDC`, `SpellCastingHelper.IsBlockedBySpellResistance` / `PenetratesSpellResistance`, `Services/SpellResolutionService.cs` (tests only). There are two `SaveType` enums: the global one in `SpellSaveResolver.cs` and the nested `SavingThrowResolver.SaveType` used for non-spell saves.

Known deviations, one line each (tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md)): `FeatManager.GetSpellFocusDCBonus` and the wizard specialist-slot check compare `School` by equality, so the 40 decorated schools never match; scroll and wand casts use the reader's caster level, not the item's (the item level is only logged at `_Core/GameManager.cs:6023` and `6135`); DCs use `SpellLevel`, not the casting class's level for that spell.

## Durations and ticking

`DurationType` (`Spell/StatusEffects/ActiveSpellEffect.cs`) is Instantaneous (enum value 0), Rounds, Minutes (x10 rounds), Hours (x600), Permanent, Days (x14,400), MinutesPerLevel or Concentration. `ActiveSpellEffect.CalculateDurationRounds(spell, cl)` returns the sentinels 0 (Instantaneous), -1 (Permanent) and -2 (Concentration). `Tick()` skips negative values and expires at 0. `SpellCastingHelper.CalculateDuration` wraps it in `Max(1, ...)`, which turns every sentinel into 1 round (Calm Emotions, a Concentration spell, lasts 1 round this way). Call `CalculateDurationRounds` when you need the sentinels. Several handlers overwrite `effect.RemainingRounds` after `AddEffect` with their own value.

Legacy trap: 54 spells set only `BuffDurationRounds` and no `DurationType`, so they default to Instantaneous, get `RemainingRounds = 0` in `StatusEffectManager`, and probably expire on the first tick (not verified in Play mode). The `default:` branch that falls back to `BuffDurationRounds` is reached only by `MinutesPerLevel`, which has no case (Invisibility Purge).

**Tick order.** Everything ticks at the global round boundary, not on the caster's turn, so a 1-round effect cast late in initiative expires before the caster acts again.

```
TurnService.OnNewRound -> GameManager.OnNewRound (_Core/GameManager.cs:3806)
  ResetQuickenedSpellTrackingForAllCharacters
  TickAllSpellDurations (GameManager.SpellCasting.cs:7926)
    per living PC and NPC: TickCharacterSpellDurations (:7979)
      StatusEffectManager.TickAllEffects -> RemoveEffect (reverses stats) on expiry
      expiry if-chain: per-spell side-state cleanup
      sync if-chain: copy RemainingRounds into CharacterController *EffectData objects
      EffectService.TickResistEnergyEffects / TickProtectionFromEnergyEffects / TickDebuffEffects
      TickCharacterItemSpellDurations
      TickClericSpell3Durations, TickClericSpell4Durations   (TickClericSpell2Durations has no caller)
    TickAllAlignmentDetectionDurations
  _conditionService.OnRoundEnd      (conditions; see Combat & grid)
  TickSummonDurations
  TickEmanations -> EffectService.TickEmanations
  TickActiveGreaseEffects -> WindEffectManager, AreaEffectManager.OnCombatRoundStart, greased objects
Turn start: StartPCTurn -> HandleFlamingSphereTurnStart, ApplyMelfsAcidArrowTurnStartDamage
            SingleNPCTurnFromInitiative (NPCTurns.cs) -> HandleFlamingSphereTurnStart
            BeginNPCTurnForAI -> ApplyMelfsAcidArrowTurnStartDamage
            OnTurnStarted -> TickDomainPowerDurations
```

**Side-state cleanup lists.** Many spells keep state outside `StatusEffectManager`: `CharacterStats` flags and counters (Death Ward, Divine Power, Silence, Prayer, Death Knell, Align Weapon, Spiritual Weapon) or `CharacterController` `*EffectData` objects (Haste, Slow, Invisibility, Glitterdust, Stoneskin, Protection from Arrows, Dimensional Anchor, Disguise Self and others). Each removal route keeps its own list of spells. A spell missing from one list leaks state on that route; for example, a dispelled Dimensional Anchor stays in force until combat ends. When you add a spell with side state, update every row:

| Route | Code |
|---|---|
| Any removal (expiry, dispel, `RemoveAllEffects`) | `StatusEffectManager.RemoveEffect` (`:256`) and `ApplySpellSpecificAdjustments(effect, applying: false)` (`:640`) |
| Expiry | the if-chain after `TickAllEffects` in `TickCharacterSpellDurations` |
| Every round while active | the sync if-chain in `TickCharacterSpellDurations` |
| Dispel | `DispelMagicService.HandleDispelSpecialCleanup` (`Services/DispelMagicService.cs:295`) |
| End of combat | `GameManager.RestorePartyAfterCombat` (per-PC `Clear*Effect` calls, `Stats.Active*Effect = null`) and `GameManager.OnCombatEnded`, which an ordinary victory does not reach (see [the runtime loop](../ARCHITECTURE.md#what-the-game-is-and-the-runtime-loop)) (`EffectService.ClearAll`, `ClearAllActiveGreaseEffects`, `ClearAllMirrorImageEffects`, `CurseTracker.ClearAll`) |
| Counter-based flags | `EffectService.TickClericSpell2/3/4Durations`, the only code that clears those flags |

Known deviations, one line each (tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md)):
- `TickClericSpell2Durations` has no callers, so the Silence, Death Knell (+STR) and Align Weapon flags never expire, and `RestorePartyAfterCombat` does not reset them.
- Silence sets `Stats.SilenceActive`, but the only check that reads it is `SpellcastingComponent.CanCastSpell`, which the combat spell menu and the cast pipelines do not call. Silence therefore probably does not stop casting in combat (inferred from code, not verified in Play mode).
- `ProcessSpiritualWeaponTurnStart` has no callers, so Spiritual Weapon attacks only once.
- `ProcessHeatMetalTick` (`Spell/Domain/GameManager_DomainSpells.cs:407`) has no callers, so Heat Metal never deals its per-round damage. This was found while writing this section and is not in the verified issue list; add it to [KNOWN_ISSUES.md](../KNOWN_ISSUES.md).
- Divine Power sets `AppliedAttackBonus` after `AddEffect`, so removing the effect leaves a lasting attack penalty.
- Haste applies its bonuses twice: through its `Buff*` fields and through the `Haste*` fields set by `ApplyHasteBuff`.
- For a Concentration AoE, only the first target's effect is tracked; the others last until combat ends.

## Persistent areas, walls, emanations, holy areas, line of effect

Five unrelated mechanisms track lasting effects:

| Mechanism | Holds | Ticked / cleared by |
|---|---|---|
| `PersistentAreaEffect` subclasses in `Spell/AreaEffects/` (22, including the never-instantiated `FireballAreaEffect`), registered in the self-creating `AreaEffectManager` | Fog Cloud, Obscuring Mist, Solid Fog, Sleet Storm, Stinking Cloud, Darkness, Daylight, Web, Entangle, Grease, Soften Earth, Spike Stones, Plant Growth, Glitterdust, Black Tentacles, Lesser Globe, Resilient Sphere, Wall of Fire, Wall of Ice, Wall of Force, Wind Wall | `AreaEffectManager.OnCombatRoundStart` via `TickActiveGreaseEffects`; `ClearAllEffects` via `ClearAllActiveGreaseEffects` and `RestorePartyAfterCombat` |
| `EmanationEffectData` subclasses in the static `EffectService._activeEmanations` (`Spell/Effects/EffectService.cs`) | Magic Circle (`MagicCircleEffectData`, registered by the generic `ApplySpellBuff` branch) and Invisibility Sphere (`InvisibilitySphereEffect`): centred on a creature, Chebyshev radius | `EffectService.TickEmanations`; `EffectService.ClearAll` in `OnCombatEnded` |
| `_activeHolyAreas` (`Spell/Special/GameManager_HolyAreas.cs`) | Consecrate, Desecrate | never ticked or cleared at combat end (only an overlapping new area replaces one); the handlers are unreachable (see [Cast pipelines](#cast-pipelines)) and the enter/turning hooks (`UpdateHolyAreaEffectsForCharacter`, `GetTurningCheckModifierAtPosition`) have no callers |
| Private GameManager lists | `_activeGreasedObjects`, `_activeFlamingSpheres` (`FlamingSphereEntity`), `_mirrorImageStates` (Mirror Image spawns real `CharacterController` clones) | their own round-start or turn-start hooks |
| `WindEffectManager` (`Spell/AreaEffects/WindEffect.cs`) | wind from Gust of Wind and Wind Wall, which disperses areas with `DispersibleByWind` | `TickActiveGreaseEffects` |

`TickActiveGreaseEffects` and `ClearAllActiveGreaseEffects` (`Spell/Special/GameManager_Grease.cs:633-648`) are misnamed: they drive all zones and all wind, not only Grease. `EffectService`'s class comment claims it orchestrates spell ticking; that orchestration is still in `GameManager.SpellCasting.cs`.

**PersistentAreaEffect lifecycle.** A factory such as `CreateFogCloudArea` (`Spell/Special/GameManager_ConcealmentAreas.cs`) does `new GameObject().AddComponent<T>()` and sets `CenterPosition`, `RoundsRemaining`, `CasterLevel`, `Caster`, and `SaveDC` where the area has a save (Stinking Cloud, Web; Fog Cloud sets none). The subclass `Awake` hard-codes `Shape` and `Radius` or `SizeX`/`SizeY`. `Start` computes `AffectedCells`, calls `OnAreaCreated` and `ApplyInitialEffect`, then registers with `AreaEffectManager`. `OnRoundStart` runs `UpdateCharacterTracking`, then `OnCreatureInAreaAtRoundStart` for each occupant, decrements `RoundsRemaining`, applies wind dispersal and expires the area (`OnAreaExpires`, exit hooks, destroy).
- Entry detection: the 14 subclasses with an `Update()` call `UpdateCharacterTracking` every frame, which runs `FindObjectsOfType<CharacterController>()` (a performance hazard). The others notice a creature entering only at round start; Grease also has a movement hook (`HandleGreaseStepAfterMovement`).
- The footprint comes from `Awake`, not from the previewed cells or `spell.AreaRadius`, so Enlarge and Widen do not change a lasting zone. Only walls take the previewed cells, through each wall subclass's own `SetExplicitCells` (Wall of Fire, Wall of Ice, Wall of Force, Wind Wall; called from `GameManager_Spells_W.cs` and `GameManager_WallOfForce.cs`); their placement flows in `Spell/Special/GameManager_WallOf*.cs` end in `PerformAoESpellCast`.

**Line of effect.** `LineOfEffectService` (`Spell/AreaEffects/LineOfEffectService.cs`) is a static registry of `ILineOfEffectBlocker`. Wall of Ice, Wall of Force and Resilient Sphere register themselves. The only consumers are `AoESystem.FilterCellsByLineOfEffect` and `FilterTargetsByLineOfEffect`, called by the AoE preview and by `PerformAoESpellCast`. Walls do not block single-target spells or attacks (see [Grid](combat-and-grid.md#grid)).

## Metamagic

There are nine PHB feats (`MetamagicFeatId` in `Spell/Components/MetamagicData.cs`). Level adjustments: Enlarge, Extend, Silent and Still +1; Empower +2; Maximize and Widen +3; Quicken +4; Heighten `HeightenToLevel - base`. `GetEffectiveSpellLevel` caps at 9. Feats are detected by name (`SpellcastingComponent.GetKnownMetamagicFeats`, `Stats.HasFeat(MetamagicData.GetFeatName(id))`). The `MetamagicSystem` / `MetamagicModifier` pipeline (`Spell/Components/MetamagicModifier.cs`) is unused at runtime except for `MetamagicSystem.IsSpontaneousCaster`.

| Source | Path | Status |
|---|---|---|
| Prepared (Wizard, Cleric, Druid) | `SpellPreparationUI` slot dropdowns offer single fixed feats, two-feat pairs and Heighten (`HeightenToLevel = slot.Level`) where base + adjustment equals the slot level. Choosing an option commits at once through `PrepareSpellInSlotWithMetamagic` (domain and specialist slots rejected), which stores `SpellSlot.AppliedMetamagic`. In combat `CombatUI` lists `GetPreparedSpellsByLevel` -> `GetUniqueAvailableSpells`, which returns a clone carrying `MetamagicDataRef`, and `CastWizardSpellWithMetamagic` finds the slot by SpellId plus `MetamagicData.GetDisplayName()` | works. Auto-prepare (`SpellSlot.Prepare`) and `RefreshSpellSlots` wipe it; character-creation preparation offers none |
| Spontaneous (Sorcerer, Bard) | `CombatUI.ShowMetamagicPanel`, shown only to spontaneous casters | non-functional: the confirm button requires `HasSlotAtLevel`, which checks only `SpellSlots` (empty for a pure Sorcerer or Bard), and `CastWizardSpellWithMetamagic` also searches only `SpellSlots`. The panel never sets `HeightenToLevel` |
| Scrolls and wands | `InitiateScrollCastThroughPipeline` / `InitiateWandCastThroughPipeline` build a `MetamagicData` from `ScrollData`/`WandData` feats and `HeightenToLevel`, apply it to a clone, and copy the item's DC into `SaveDC`. Self-use consumables go through `BuildConsumableSpellVariant` (`_Core/GameManager.cs:6549`) | works; crafted in `CraftingWorkshopUI` (see [Crafting](items-and-economy.md#crafting)) |
| Rods | `MetamagicData.RodAppliedMetamagic`, `Equipment/Rods/MetamagicRodActivation.cs` | data and tests only; no casting code calls them |
| F12 panel | `SpellTestingPanel` toggles -> `GameManager.TestCastSpellFromPanel(..., metamagic)` | debug only |
| NPCs | `TryNPCPerformSpellCast` passes null | never applied |

What each feat actually changes:

| Feat | Applied in | Effect in code |
|---|---|---|
| Enlarge | `SpellCaster.ApplyMetamagicToSpellData` (on the clone) | doubles `RangeSquares` and `RangeIncreaseSquares`, which category spells ignore, so range does not change for the 249 category spells. Since 0dd8e76 it also doubles `AoESizeSquares` and `AreaRadius` (a rules deviation, not a labelled house rule: RAW Enlarge changes range only; Enlarge + Widen quadruples; tracked as [SPL-010](../issues/SPL.md)) |
| Extend | same | doubles `DurationValue` and `BuffDurationRounds` unless Instantaneous, Permanent or Concentration. Handlers that hard-code durations ignore it. Offered only when `BuffDurationRounds != 0` |
| Widen | same | doubles `AreaRadius` and `AoESizeSquares`; persistent zones are unaffected |
| Quicken | same, plus the action spend in `PerformSpellCast` / `TryConsumePendingSpellCast` | `ActionType = Free`; one per round |
| Silent, Still | same | clear `HasVerbalComponent` / `HasSomaticComponent`; ASF is still rolled |
| Empower | `SpellCaster.Cast` only | adds `RoundToInt(50%)` of dice plus flat bonus; handlers that roll their own dice (Fireball) ignore it |
| Maximize | `SpellCaster.Cast` only | sets every die to its maximum; Empower + Maximize gives max x1.5 |
| Heighten | `SpellCaster.Cast` and the GameManager handlers' `GetSpellSaveDC` | DC uses `HeightenToLevel` (SPL-001); Lesser Globe, dispel and other level-dependent handler effects use the base level (SPL-038) |

A spontaneous metamagic cast costs a full-round action. `MetamagicSystem.IsSpontaneousCaster` is `HasClass("Sorcerer") || HasClass("Bard")`, so a Wizard/Sorcerer also pays it for prepared metamagic. These gaps are tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md). Recipe: "Add a metamagic feat" in [DEVELOPMENT_RECIPES.md](../DEVELOPMENT_RECIPES.md).

## Cleric domains and granted powers

- **Data.** `Character/Religion/DomainDatabase.cs` defines 22 domains, each with a domain spell for levels 1 and 2 only. `SpellDatabase.AnnotateDomainAvailabilityFromDomainDatabase` adds `AvailableFor("Cleric", level, domain)` to those spells. Never register `domain_*` copies; the old `domain_*` IDs are aliases.
- **Slots.** `GetClericSlotsForLevel` adds one domain slot per castable level from 1 up when `Domains` is not empty. `AutoPrepareClericSlots` fills them from `GetAvailableDomainSpells`, which reads `SpellDatabase.GetDomainSpells`. Domain slots of level 3+ stay empty.
- **+1 caster level.** `CharacterStats.GetDomainBoostedCasterLevel(spell)` adds +1 for the Good, Evil, Law, Chaos, Healing and Knowledge domains when the spell has the matching `SpellDescriptor` (Good, Evil, Lawful, Chaotic, Healing, Divination). Descriptors come from `AnnotateSpellDescriptors` (auto-detection from school and damage type plus explicit calls, some with wrong IDs).
- **Domain spell handlers.** `Spell/Domain/GameManager_DomainSpells.cs`: Hold Animal, Calm Animals (unreachable, see [Cast pipelines](#cast-pipelines)), Calm Emotions, Produce Flame, Heat Metal, Dominate Animal, Command Plants. `GameManager_DomainAreaSpells.cs`: Entangle, Soften Earth, Spike Stones, Plant Growth (persistent areas). Magic Vestment is in `GameManager_Spells_M.cs`.
- **Granted powers.** `Spell/Domain/GameManager_DomainPowers.cs`. The Domain Power button (`_Core/SceneBootstrap.cs:1144`) calls `OnDomainPowerButtonPressed`, which lists `GetAvailableDomainPowers` (gated by `CanActivateDomainPower` and the `*DomainUsesToday` counters on `CharacterStats`) and calls `ActivateDomainPowerByName`. Powers: Strength (`ActivateStrengthDomain`), Destruction smite, Death touch (resolved on a chosen target through `ResolveDomainPowerOnTarget`), Sun greater turning, Travel freedom of movement, Luck reroll, Plant rebuke, and Air/Earth/Fire/Water elemental turning. Their durations tick at turn start (`TickDomainPowerDurations`), and `RestorePartyAfterCombat` resets the counters after every combat.

## Summoning

- **Detection.** `SummonMonsterLists.GetSummonMonsterSpellLevel(spellId)` parses the level from the ID with the regexes `summon_monster_(\d+)` and `summon_natures_ally_(\d+)`, so class aliases such as `summon_monster_1_clr` match. `GameManager.IsSummonMonsterSpell` checks for a result above 0. No data flag is consulted: Summon Monster I and II are still `IsPlaceholder` but work, and the flag only keeps them out of auto-prepare and the item factories.
- **Tables.** `Spell/Components/SummonMonsterLists.cs` holds `SummonMonsterOption` lists (NPCDatabase id, template, alignment) for Summon Monster I-IV and Summon Nature's Ally I-IV; the Summon Monster V list is empty. Cleric options are filtered to creatures within one alignment step (`SummonMonsterOption.IsAvailableTo`). Creature count: 1 from the spell's own list, 1d3 from one level lower, 1d4+1 from two or more levels lower.
- **Flow.** `BeginPendingSpellTargeting` -> `ShowSummonCreatureSelectionMenu` (`_Core/GameManager.cs:10500`, list level and creature) -> `ShowSummonPlacementTargets` -> tile click (`_Core/GameManager.CombatActions.cs:1047`) -> `PerformSummonMonsterCast` (`GameManager.SpellCasting.cs:682`): `TryConsumePendingSpellCast`, concentration, AoO, then `SpawnSummonedCreature` (clones an `NPCDatabase` definition, applies the celestial or fiendish template and Augment Summoning) and `RegisterActiveSummon`. Summon Swarm uses `PerformSummonSwarmCast` (`:583`) with a concentration-held duration.
- **Live state.** `GameManager._activeSummons` (`List<ActiveSummonInstance>`, `_Core/GameManager.cs:457`). Duration is `caster.Stats.Level` rounds (character level, minimum 1), ticked by `TickSummonDurations` at round start. `DespawnSummonWithEffect` removes a summon. `HandleSummonDeathCleanup` is the general on-death hook (it also clears Mirror Image). Summons join initiative after their caster (`TurnService.AddToInitiative`). Allied summons are player-controlled (`ConfigureTeamControl(..., controllable: alliedToPlayer)`); AI-controlled summons run `AI_SummonedCreature` in `_Core/GameManager.NPCTurns.cs` (via `AIService` -> `ExecuteSummonedCreatureTurnForAI`). A Summon Swarm creature gets an `IndiscriminateSwarmAI` profile in `PerformSummonSwarmCast`, so `AIService` runs `ExecuteSwarmTurn` for it instead. Commands (Attack Nearest, Protect Caster) are in `Utilities/SummonCommand.cs` and the right-click context menu.
- **`Services/SummoningService.cs` is an unused copy** (see [Service layer](../ARCHITECTURE.md#service-layer)). Change summon behavior in the GameManager code above, not in the service.
