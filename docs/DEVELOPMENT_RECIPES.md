> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-02.

# Development recipes

Step-by-step procedures for the common changes in this project: which files to touch, in which order, and which traps earlier commits fell into. It supersedes the retired how-to docs listed in [archive/INDEX.md](archive/INDEX.md); where they disagree with this doc or the code, the code wins.

- Line numbers are as of 0dd8e76 and drift. Grep the `Class.Method` name first; use the line only as a hint.
- Runtime claims come from reading the code. Nothing here was run in Play mode for this revision unless stated.
- Bugs mentioned under "Pitfalls" are the ones you will hit while following the recipe. The full list is in [KNOWN_ISSUES.md](KNOWN_ISSUES.md); issue IDs link to their area file under `issues/`.
- Example commits are short hashes; read them with `git show --stat <hash>`. Many of them use pre-reorganisation paths (`Assets/Scripts/Core/`, `Identifiers/`, `Inventory/`, `Magic/Spells/Databases/`, `UI/Panels/`). Use the current paths given here.

## Golden rules (apply to every change)

1. **Compile after every edit.** Run `bash tools/compile_check.sh` from the repo root (headless, about 5 s; `VERBOSE=1` lists warnings; see [TESTING.md](TESTING.md)). Baseline at 0dd8e76: 0 errors, 508 warnings. Any error, or a jump in the warning count, is a regression. The history has repeated waves of commits that did not compile (f706ac6 "127+ compilation errors", e8e103b, 0dd2d61, 167daee, 66997bc, afd2df5, 71b869b, 0fec1eb), mostly from invented members. Grep every member you call before you call it.
2. **C# 9 only.** Unity 6000.4.0f1 compiles with LangVersion 9.0. No C# 10+ syntax (file-scoped namespaces, `global using`, `record struct`).
3. **Global namespace.** Game code is in the global namespace. Exceptions: the id constant classes `SpellNames`, `ItemIDs`, `WondrousItemNames` and `RingNames` live in `DND35e.Identifiers` (add `using DND35e.Identifiers;`; `RodNames` is global), and tests use `Tests.<Area>` namespaces. `GameManager` is one partial class across about 52 files; put new GameManager code in the partial for its domain.
4. **Never mutate database templates.** `SpellDatabase.GetSpell`, `ItemDatabase.Get` and `NPCDatabase.Get` return the shared registered instance. Clone first: `SpellData.Clone()`, `ItemDatabase.CloneItem(...)`, `NPCDefinition.Clone()`.
5. **New fields must be copied by hand.** A new `ItemData` field must be copied in `ItemDatabase.CloneItem(string)` (`Assets/Scripts/Equipment/Items/ItemDatabase.cs:1700+`), and in `ItemBuilder.CopyBaseProperties` for builder-made items. A new `NPCDefinition` field needs `NPCDefinition.Clone` and the template `CopyDefinitionFields` lists. Otherwise the value is lost on every clone (store purchase, NPC equip, spawn).
6. **`.meta` files are not versioned.** `*.meta` is gitignored (`.gitignore:49-51`); only a few sprite metas are force-added. Unity creates the `.meta` for a new script or asset when it imports it. Do not hand-write or commit `.meta` files; texture import settings are therefore not versioned either. The compile check does not import assets.
7. **Log through the combat log.** `CombatUI?.ShowCombatLog(CombatLogHelper.X(...))`, plus a tagged `Debug.Log("[System] ...")` for the Console. See [Add a combat-log message](#add-a-combat-log-message).
8. **Add or extend a static `RunAll()` test suite** under `Assets/Scripts/Tests/<Area>/`. There is no Unity Test Framework, no asmdef, and nothing calls `RunAll()` automatically; [TESTING.md](TESTING.md) explains how to run one.
9. **Test in Play mode** through the F12 Spell Testing Panel (PC spells only) or a test encounter preset (see [Add an encounter preset or test scenario](#add-an-encounter-preset-or-test-scenario)). If you could not, say "not verified in Play mode" in the commit message.
10. **Expect silent failures.** Most registries fail quietly: a missing switch case defaults, an uncalled `RegisterX()` still compiles, a misspelled name string never matches, a duplicate id overwrites. After a change, check the init log count or the spawn log. Update this doc in the same commit when a recipe changes.

## Contents

- Magic: [Add a spell (data)](#add-a-spell-data) · [Implement a single-target, touch or self spell effect](#implement-a-single-target-touch-or-self-spell-effect) · [Implement an area spell](#implement-an-area-spell) · [Make NPCs cast a spell, and test a spell](#make-npcs-cast-a-spell-and-test-a-spell) · [Add a metamagic feat](#add-a-metamagic-feat)
- Combat and characters: [Add a condition](#add-a-condition) · [Add a feat with a combat effect](#add-a-feat-with-a-combat-effect) · [Add a class feature](#add-a-class-feature) · [Add a combat maneuver](#add-a-combat-maneuver) · [Add a combat-log message](#add-a-combat-log-message)
- Items and economy: [Add a mundane weapon, armor or shield](#add-a-mundane-weapon-armor-or-shield) · [Add a weapon or armor special ability](#add-a-weapon-or-armor-special-ability) · [Add a wondrous item](#add-a-wondrous-item) · [Add a ring](#add-a-ring) · [Add a rod](#add-a-rod) · [Add a staff](#add-a-staff) · [Add a scroll, potion or wand](#add-a-scroll-potion-or-wand) · [Add an item to the store](#add-an-item-to-the-store) · [Make an item craftable](#make-an-item-craftable)
- Creatures and encounters: [Add a monster or NPC](#add-a-monster-or-npc) · [Add a creature template](#add-a-creature-template) · [Add creature token art](#add-creature-token-art) · [Add an encounter preset or test scenario](#add-an-encounter-preset-or-test-scenario) · [Change dungeon, random or custom encounters](#change-dungeon-random-or-custom-encounters)
- UI: [Add a combat action button](#add-a-combat-action-button) · [Add a UI panel](#add-a-ui-panel)

# Magic

## Add a spell (data)

Use when a spell needs to exist in `SpellDatabase` (always the first step for a new spell).

1. Add the id constant to `Assets/Scripts/Spell/Data/SpellNames.cs` (class `SpellNames`, alphabetical, snake_case value, `/// <summary>Name (spell id: my_spell)</summary>`). Ignore the `SpellID` enum (`Spell/Data/SpellID.cs`): only `IdentifierExtensions`, `EnumTest` and the unused `ItemData.ConsumableSpellIDEnum` read it.
2. Add `Register(new SpellData { ... });` inside `RegisterSpells<Letter>()` in `Assets/Scripts/Spell/Database/SpellDatabase_<FirstLetter>.cs` (templates: Magic Fang in `_M`, Know Direction in `_K`). `SpellDatabase.Init` (`SpellDatabase.cs:33-86`) calls `RegisterSpellsA..Z`, but there are no Q, X or Y files or calls. For those letters create `SpellDatabase_Q.cs` (`public static partial class SpellDatabase { private static void RegisterSpellsQ() { ... } }`) and add the call to `Init` in alphabetical position.
3. Fill the `SpellData` fields (`Spell/Data/SpellData.cs:43-500`):
   - Identity: `SpellId = SpellNames.X`, `Name`, `Description` (cite the PHB page), `SpellLevel`, `School` (string), `ClassList` (string[]; `Register` adds Sorcerer for Wizard and vice versa).
   - Targeting: `TargetType` (`SpellTargetType.Self/SingleEnemy/SingleAlly/Touch/Area`), `RangeCategory` (its setter calls `SpellRanges.Configure`), `IsTouch/IsMeleeTouch/IsRangedTouch`; areas use `AoEShapeType/AoESizeSquares/AoERangeSquares/AoEFilter`.
   - Effect: `EffectType` (`SpellEffectType`), `DamageDice` (die SIDES), `DamageCount`, `BonusDamage`, `DamageType` ("fire", "cold", ...), `AutoHit/MissileCount`, `HealDice/HealCount/BonusHealing`, `Buff*` fields with `BuffBonusType` + `BonusTypeExplicitlySet`.
   - Defenses: `AllowsSavingThrow`, `SavingThrowType` ("Reflex"/"Will"/"Fortitude"), `SaveHalves`, `SpellResistanceApplies`, `IsMindAffecting`.
   - Duration and casting: `DurationType/DurationValue/DurationScalesWithLevel` and `BuffDurationRounds` (set both, see pitfalls), `ActionType`, `ProvokesAoO`, `HasVerbalComponent/HasSomaticComponent` (default true)/`HasMaterialComponent/HasDivineFocus`, `IsPlaceholder` + `PlaceholderReason`.
4. Other class lists at a different level: `RegisterClassSpellAlias("my_spell_brd", SpellNames.MY_SPELL, "Bard", 2);` (`SpellDatabase.cs:111-123`). Plain aliases: `RegisterAlias(alias, canonical)`. Domain spells: add `{ level, SpellNames.X }` to the domain dictionary in `new DomainData(...)` in `Assets/Scripts/Character/Religion/DomainDatabase.cs`; `SpellDatabase.AnnotateDomainAvailabilityFromDomainDatabase` adds the Cleric availability.
5. Descriptors: `AnnotateSpellDescriptors` (`SpellDatabase.cs:149+`) auto-detects Fire/Cold/Acid/Electricity/Sonic/Force from `DamageType`, Divination from `School`, and Healing only for `EffectType` Healing with School Conjuration. Good/Evil/Lawful/Chaotic/Fear/MindAffecting/Death/Light/Darkness need an explicit `SetDescriptor(SpellNames.X, SpellDescriptor.Y);` there (domain caster-level boosts depend on them).
6. Check `Assets/Scripts/AI/SpellCategoryClassifier.cs`. `ReclassifyAll` (called at `SpellDatabase.cs:75`) overwrites `EffectType` at Init for ids in its Control/Summon/Dispel/Wall/Illusion/Escape/Divination/Utility sets (`:115-193`), and turns any Debuff with no damage + Will save + `IsMindAffecting` into Control (`:96-103`). The final `EffectType` decides dispatch (next recipe).
7. Optional exposure: scroll loot `TreasureData.ScrollSpells` (`TreasureGenerator/TreasureData.cs:1696`; keys `arcane_N`/`divine_N`, values are lowercase DISPLAY names), staves (`Spell(SpellNames.X, "Name", level, chargeCost)` in `Equipment/Staves/StaffDatabase.cs`), class default spellbooks (`Character/Classes/WizardClass.cs:133-151`), costly components (`SpellComponentRegistry` in `Spell/Casting/SpellComponentSystem.cs:184`). Scrolls, potions and wands are generated automatically ([Add a scroll, potion or wand](#add-a-scroll-potion-or-wand)).
8. Compile, then confirm in Play mode that `[SpellDatabase] Initialized with N spells` rises by one.

Files: `Spell/Data/SpellNames.cs`, `Spell/Database/SpellDatabase_<Letter>.cs`, `Spell/Database/SpellDatabase.cs`, `Character/Religion/DomainDatabase.cs`, `AI/SpellCategoryClassifier.cs`; optionally `TreasureGenerator/TreasureData.cs`, `Equipment/Staves/StaffDatabase.cs`, `Spell/Casting/SpellComponentSystem.cs`. Examples: 571fe1f, b2f2f1d, f165fd7, 6921d93, aa1ad37, 88043b2, 5b98444, 7509633, f706ac6.

Pitfalls:
- The generic damage path (`SpellCaster.Cast`, `Spell/Casting/SpellCaster.cs:416-520`) rolls a FIXED `DamageCount` and never scales with caster level. Burning Hands is registered as 3d4 at every CL (`SpellDatabase_B.cs:393-407`) and no handler overrides it. CL-scaled spells need a custom handler.
- `MetamagicData.CanExtend` (`Spell/Components/MetamagicData.cs:168-171`) tests the legacy `BuffDurationRounds != 0`, so a spell defined only with `DurationType/DurationValue` shows as not extendable. Set both, or fix `CanExtend`.
- `GetSpell` returns the shared instance. `Clone()` (`SpellData.cs:511`) deep-copies `AvailableFor` and `AppliedMetamagics` but shares the `ClassList` array. Shield of Faith mutates the database entry in place (`GameManager.SpellCasting.cs:7618-7619`).
- `RegisterClassSpellAlias` only works if the canonical spell is already registered (else a warning at `SpellDatabase.cs:121`). Letter files run A to Z, so put the alias in the canonical spell's file or a later one.
- Aliases resolve only through `SpellDatabase.GetSpell`; code comparing `spell.SpellId == SpellNames.X` sees the canonical id.
- Check class lists and levels against the PHB; 5b98444 and 88043b2 had to fix wrong ones.
- The `SpellDatabase.cs` header comment ("~140 spells, Wizard/Cleric 0-2") is stale: there are 292 registered spells (`Register(new SpellData ...)` calls), about 133 class aliases, 25 plain aliases and 47 placeholders.

## Implement a single-target, touch or self spell effect

Use when the spell needs behavior beyond what the generic cast path does with its data fields.

1. **Check whether data is enough.** `GameManager.PerformSpellCast` (`Spell/Resolution/GameManager.SpellCasting.cs:1629`) calls `SpellCaster.Cast` (`:2085`), which does the touch attack, SR, save, typed damage (`ApplyIncomingDamage` with a `DamagePacket`) and healing. If `EffectType` is Buff/Debuff/Control/Illusion/Wall (`appliesTrackedEffect`, `:2089`), the save did not negate it (`:2107`) and no handler claimed it, `ApplySpellBuff` (`:6371`) runs the generic `StatusEffectManager.AddEffect` block (`:7659-7710`), which applies `BuffAttackBonus/BuffACBonus/BuffSaveBonus/BuffStatName+BuffStatBonus/BuffTempHP` with duration and bonus-type stacking. If that describes the spell, stop.
2. **Write the handler** in `public partial class GameManager` in `Assets/Scripts/Spell/Resolution/GameManager_Spells_<FirstLetter>.cs` (files exist for A-I, K, L, M, N, P, R, S, V, W plus Cantrips/MagicFang/Phase1/Phase2/Shared; create a new partial for other letters). Two shapes:
   - Shape A, post-cast effect: `private bool TryResolveMySpellSpellEffect(CharacterController caster, CharacterController target, SpellData spell, SpellResult result)` that returns false unless `spell.SpellId` matches (templates `TryResolveDeathKnellSpellEffect` `GameManager_Spells_D.cs:320`, `TryResolveDimensionalAnchorSpellEffect` `:672`).
   - Shape B, tracked buff/debuff: `private ActiveSpellEffect ApplyMySpellEffect(caster, target, spell, spellComp)` dispatched from `ApplySpellBuff`.
3. **Wire Shape A into both chains.**
   - PC: in `PerformSpellCast` (`:2135-2357`) add `bool handledMySpell = false; if (!anyPriorHandled && ... && result.Success && !effectNegatedBySave) handledMySpell = TryResolveMySpellSpellEffect(caster, target, _pendingSpell, result);` and OR it into `anyCleric4Handled` (`:2352`) so `ApplySpellBuff` (`:2359`) does not also run.
   - NPC: in `TryNPCPerformSpellCast` (`_Core/GameManager.NPCTurns.cs:758`) add the same call after `:950` and add `!handledMySpell` to the guard before `ApplySpellBuff(npc, ...)` (`:952-953`).
4. **Wire Shape B into `ApplySpellBuff` ABOVE line 7659** (`// Use StatusEffectManager for tracked buff application`), next to MAGIC_STONE (`:7565`) or SHIELD_OF_FAITH (`:7599`). `ApplySpellBuff` is shared by the PC chain, the NPC chain and the generic AoE loop, so nothing else needs wiring, but it only runs for Buff/Debuff/Control/Illusion/Wall after reclassification.
5. **Use the shared helpers; never hand-roll rules.**
   - Caster level and duration: `SpellCastingHelper.GetEffectiveCasterLevel(caster, spell)`, `CalculateDuration(spell, cl)` (`Spell/Casting/SpellCastingHelper.cs:103, :144`).
   - DC: `SpellUtilities.GetSpellSaveDC(caster, spell)` (honors a pre-baked `spell.SaveDC` from scrolls/wands).
   - Defenses: `SpellSaveResolver.RollSave / RollSpellResistance / ResolveSpellDefenses / ApplyEvasion / ApplyBlinkHalving` (`Spell/Casting/SpellSaveResolver.cs:193-347`).
   - Damage: `new DamagePacket { RawDamage, Types = new HashSet<DamageType>{...}, Source = AttackSource.Spell, SourceName }` + `target.Stats.ApplyIncomingDamage(raw, packet)` (template `GameManager.SpellCasting.cs:6330-6342`), then `CheckConcentrationOnDamage(target, dmg)`; on kill `target.OnDeath()` + `HandleSummonDeathCleanup(target)`.
   - Tracked effect: `StatusEffectManager.AddEffect`. Conditions: `_conditionService.ApplyCondition(target, type, rounds, source: caster, sourceNameOverride: spell.Name, sourceCategory: "Spell", sourceId: spell.SpellId)` (pattern `:6379-6386`).
6. **Custom state** (fields on `CharacterStats`, or `Spell/StatusEffects/<Name>EffectData.cs` plus a `CharacterController.ActiveXxxEffect`, e.g. `ApplyHasteEffect` `CharacterController.cs:1512`) must be cleared on expiry in `StatusEffectManager.RemoveEffect` (`StatusEffectManager.cs:256-330`, see ENTROPIC_SHIELD/MAGIC_STONE) and on dispel in `DispelMagicService.HandleDispelSpecialCleanup` (`Services/DispelMagicService.cs:295+`). Counters outside StatusEffectManager tick in `TickCharacterSpellDurations` (`GameManager.SpellCasting.cs:7979`) or `EffectService.TickClericSpell2/3/4Durations`.
7. **Pre-cast choices** (energy type, mode): add a `_pendingMyChoice` field, the prompt in `BeginPendingSpellTargeting` (`:813`, pattern `:865-880`), the guard in `PerformSpellCast` (pattern `:1656-1675`), and reset it next to EVERY `_pendingProtectionFromEnergyType = null;` (25 in `GameManager.SpellCasting.cs`, 12 in `_Core/GameManager.cs` including the scroll/wand pipeline, 1 each in `GameManager.CombatActions.cs` and `GameManager.TestPanel.cs`).
8. **Target restrictions** (humanoid only, living only) go in `IsValidTargetForSpell` (`:1215`); PC targeting, `CombatActions.cs:1073` and the NPC path all call it.
9. **Consumables**: self-target potions, non-targeted scrolls and wands go through `GameManager.TryApplySpellConsumableEffect` (`_Core/GameManager.cs:5674`), which only calls `AddEffect`. A Shape A effect needs a special case there like Mirror Image (`:5745-5761`). Targeted scrolls and wands use the normal pipeline ([Add a scroll, potion or wand](#add-a-scroll-potion-or-wand)).
10. Compile, then test with the F12 panel (`UI/Spells/SpellTestingPanel.cs` -> `GameManager.TestCastSpellFromPanel`, `_Core/GameManager.TestPanel.cs:38`). F12 covers only the PC path; test the NPC path with an encounter whose NPC has the spell prepared.

Files: `Spell/Resolution/GameManager_Spells_<Letter>.cs`, `Spell/Resolution/GameManager.SpellCasting.cs`, `_Core/GameManager.NPCTurns.cs`, `Spell/Components/StatusEffectManager.cs`, `Services/DispelMagicService.cs`, `_Core/GameManager.cs`. Examples: 571fe1f, c1d0798, 5b98444, 8758492, 616bf32, f706ac6, 615b1df, ce1fded, 3037628.

Pitfalls:
- **Dead branches in `ApplySpellBuff`.** The generic `if (statusMgr != null) { ... return effect; }` block (`:7659-7710`) returns before the branches at `:7712-7832` (Telekinesis, Continual Flame, Levitate, Barkskin, Heal, True Seeing, Magic Fang `:7804`, `TryApplyUtilityCantrip` `:7811`, Alter Self `:7829` and others). Every PC and NPC has a StatusEffectManager, so those branches never run (static analysis).
- Before moving `ApplyMagicFangEffect` above `:7659`, fix its double bonus: `AddEffect` already applies `BuffAttackBonus/BuffDamageBonus`, the handler adds +1 again (`GameManager_Spells_MagicFang.cs:80-81`) and `RemoveEffect` subtracts once.
- `appliesTrackedEffect` excludes Dispel/Escape/Divination/Utility/Summon. After reclassification, the `ApplySpellBuff` branches for Dispel Magic (`:7643`, the only cast route to `PerformTargetedDispel`), Remove Fear, See Invisibility and alignment detection are never reached from a cast (static analysis). New spells in those categories need a TryResolve handler or a wider gate.
- **The NPC chain is a divergent copy**: 16 handlers versus about 50 on the PC side, null metamagic (`NPCTurns.cs:853`), and its `effectNegatedBySave` omits Control (`:869` vs `GameManager.SpellCasting.cs:2107-2113`). An NPC-cast Hold Person is applied even when the PC saves (static analysis).
- `SpellCaster.Cast` computes its own DC with INT for wizards and WIS for everyone else (`SpellCaster.cs:378-381`), so Sorcerer/Bard DCs on the generic path are wrong. `SpellUtilities.GetSpellSaveDC` uses the right ability but ignores Spell Focus and Heighten. Never hand-roll DCs (616bf32 patched about 10).
- 22 `target.Stats.TakeDamage(int)` calls under `Assets/Scripts/Spell` bypass energy resistance, immunity and Protection from Energy. Use `ApplyIncomingDamage` with a `DamagePacket`.
- The generic StatusEffectManager path passes `caster.Stats.Level` (character level, not caster level, `:7667`).
- Two `SaveResult` structs exist: global `SaveResult` (`SpellSaveResolver.cs:42`, has `Saved`) and `SavingThrowResolver.SaveResult`. f706ac6 and 615b1df fixed handlers that used non-existent members.
- Scroll and wand casts through the pipeline use the reader's caster level; `scrollCasterLevel` is computed (`GameManager.cs:5995`) but only logged.
- Comments at `GameManager.SpellCasting.cs:2222` and `StatusEffectManager.cs:722` name `GameManager_NewSpells.cs`, which no longer exists (split by 3037628).

## Implement an area spell

Use for bursts, cones, lines and persistent zones.

1. Data: `TargetType = SpellTargetType.Area` AND `AoEShapeType = AoEShape.Burst/Cone/Line/Ring` (`Spell/AreaEffects/AoESystem.cs:12-19`), `AoESizeSquares` (radius or length in 5-ft squares), `AoERangeSquares` (0 for cones and self-centered), `AoEFilter`, `RangeCategory`. Set both `TargetType` and `AoEShapeType`.
2. Flow: `BeginPendingSpellTargeting` enters AoE mode when `AoEShapeType != AoEShape.None` (`GameManager.SpellCasting.cs:898-903`) -> `EnterAoETargetingMode` (`:2488`) -> `HandleAoETargetClick` (`:3091`) -> `PerformAoESpellCast` (`:3567`), which spends the action and slot and checks counterspells before dispatch.
3. Pure damage with save-for-half and no CL scaling can use the generic per-target loop (`:4705-4790`), which calls `SpellCaster.Cast` with `_pendingMetamagic`.
4. Anything else: write `private bool TryResolveMySpell(CharacterController caster, SpellData spell, List<CharacterController> targets, HashSet<Vector2Int> aoeCells, out string log)` (templates `TryResolveConeOfColdSpell` in `GameManager_Spells_Phase1.cs:22`, `TryResolveScaledAoEDamageSpell` in `GameManager_Spells_Shared.cs:180`) and add a dispatch block in `PerformAoESpellCast` (`:3798-4675`). Copy the full Fireball block (`:3849-3889`): set `_lastCombatLog`, prefixes, `ShowCombatLog`, `UpdateAllStatsUI`, `Grid.ClearAllHighlights`, `AreAllNPCsDead` -> `HandleCombatVictoryDetected`, `AreAllPCsDead` -> defeat, null `_pendingSpell`/`_pendingMetamagic`, `StartCoroutine(AfterAttackDelay(caster, 1.5f))`, return.
5. Inside the handler: `RollSpellResistance`, `RollSave`, `ApplyEvasion` for Reflex-half, `ApplyBlinkHalving`, `ApplyIncomingDamage(DamagePacket)`, `CheckConcentrationOnDamage`, and on kill `OnDeath()` + `HandleSummonDeathCleanup`.
6. Persistent zones: subclass `PersistentAreaEffect` (`Spell/AreaEffects/PersistentAreaEffect.cs:43`; override `OnCreatureEntersArea`, optionally `OnCreatureInAreaAtRoundStart/OnCreatureExitsArea/OnAreaExpires/CalculateAffectedCells`). Create it like `GameManager.CreateWebArea` (`Spell/Special/GameManager_ConcealmentAreas.cs:467`): new GameObject + `AddComponent<T>()`, set `CenterPosition/RoundsRemaining/CasterLevel/SaveDC/Caster` before `Start`, which registers it with `AreaEffectManager`. Barriers also implement `ILineOfEffectBlocker` and call `LineOfEffectService.Register(this)` / `Unregister(this)` (see `WallOfIceAreaEffect.cs:163, :235`).
7. Fire spells: call `NotifyFireDamageAtPosition(cell, spell.Name)` (`_Core/GameManager.cs:10257`) per cell so webs ignite, and damage Wall of Ice sections as `GameManager_Spells_Shared.cs:282+` does. No AI wiring is possible today (see pitfalls).

Files: `Spell/Database/SpellDatabase_<Letter>.cs`, `Spell/Resolution/GameManager_Spells_<Letter>.cs` or `_Shared.cs`, `Spell/Resolution/GameManager.SpellCasting.cs`, `Spell/AreaEffects/<Name>AreaEffect.cs`. Examples: b2f2f1d, a1762ab, 0d36065, 9043fc1, 58dfe61, 0dd8e76.

Pitfalls:
- The generic AoE loop applies Buff/Debuff/Control/Illusion/Wall through `ApplySpellBuff` with NO save and NO SR (`:4719-4735`). AoE debuffs need their own handler that rolls saves (`ResolveSleepSpell` `:5023`, `ResolveColorSpraySpell` `:5266`, `TryResolveGlitterdustSpell`, `TryResolveWebSpell`).
- The generic damage loop never applies Evasion. `TryResolveScaledAoEDamageSpell` skips Evasion too and uses `Stats.TakeDamage` (no energy resistance); Cone of Cold does apply Evasion (`Phase1.cs:80`).
- Custom AoE handlers never read `_pendingMetamagic`: Empower/Maximize/Heighten do nothing for Fireball, Lightning Bolt, Call Lightning, Cone of Cold, Chain Lightning.
- PC AoE mode keys on `AoEShapeType`, the NPC path rejects `TargetType == Area` (`NPCTurns.cs:787-788`). A spell with a shape but a non-Area `TargetType` is cast by NPCs as single-target.
- NPCs never cast area spells, although `AISpellcastingStrategist.GetAreaDenialScore` (`:882`) and `SpellcasterAIProfile` score them up; the NPC falls back to other actions (static analysis; [AI-001](issues/AI.md)).
- 0dd8e76 made Enlarge Spell also double the area (`SpellCaster.cs:786-806`). That overlaps Widen Spell (Enlarge + Widen = 4x) and contradicts PHB Enlarge (range only). Confirm with the owner before building on it.
- Some dispatch blocks are incomplete copies (Cone of Cold `:4429-4444` lacks `AreAllPCsDead`). Every early return in `PerformAoESpellCast` must null `_pendingSpell`/`_pendingMetamagic` and schedule `AfterAttackDelay`, or the turn soft-locks.

## Make NPCs cast a spell, and test a spell

Use after the spell works for PCs.

1. Give it to NPCs: add the id to `NPCDefinition.KnownSpellIds` / `PreparedSpellSlotIds` in `Character/Creatures/NPCDatabase_<X>.cs` (example `NPCDatabase_L.cs:195, :244`). Dragons get spells from `DragonData.PopulateSpellcastingData` (`DragonData.cs:1274`). Template-built NPCs keep only non-placeholder spells (`TemplateSpellValidator.GetImplementedSpells`, applied in `AI/NPCTemplateAIConfigurator.cs:95-96`).
2. Route the NPC to a turn routine that casts. In `AIService.ExecuteNPCTurn` only three routines call `AIService.TryExecuteSpellcastAction` (`Services/AIService.cs:2340`): the Healer branch (`:231`, `:245`, then `ExecuteRangedKiterTurn`), `ExecuteDragonTurn` (`:651`, only if `Stats.IsSpellcaster`) and `ExecuteRangedKiterTurn` (`:896`, `:970`). `ExecuteAggressiveMeleeTurn` and `ExecuteDefensiveMeleeTurn` never cast, and neither do swarms and summons. The only other NPC cast is a charmed NPC healing its charmer (`CharmedBehaviorController`). The routine is chosen from the runtime profile (`GameManager.BuildRuntimeAIProfile`, `_Core/GameManager.NPCSetup.cs:758`) and `NPCDefinition.AIBehavior` (default `AggressiveMelee`):
   - `AIProfileArchetype = Healer` (`HealerAIProfile`) or `Dragon` (`DragonAIProfile`) always reaches a casting branch.
   - `Ranged`, `Evoker`, `Necromancer` and `Lich` build profiles with `CombatStyle.Ranged` and reach `ExecuteRangedKiterTurn`, unless `AIBehavior = DefensiveMelee`, which wins over the profile.
   - `Spellcaster` (`CombatStyle` left at Melee), `Abjurer` (Mixed), `Vampire`, `Humanoid`, `Berserk`, `Grappler`, `Animal` and the undead archetypes cast only with `AIBehavior = RangedKiter`. So does an NPC with no profile (archetype None on a non-Animal, `Brute`, `Caster`), which uses the `AIBehavior` switch alone.
   - Simplest: set `AIBehavior = NPCAIBehavior.RangedKiter`, or use the Healer, Dragon, Ranged, Evoker, Necromancer or Lich archetype.
   Area spells (`TargetType == Area`) cannot be NPC-cast at all: `TryNPCPerformSpellCast` rejects them and no NPC code calls `PerformAoESpellCast` ([AI-001](issues/AI.md)). Making an area spell NPC-castable needs a new NPC AoE path, not data.
3. Selection is data-driven: `AIService.SelectSpell` (`Services/AIService.cs:2492`) scores via `SpellcasterAIProfile.ScoreSpell` or `AISpellcastingStrategist.ScoreSpellComprehensive` (`:1115`), using `EffectType/TargetType`, damage and save fields. Ally vs enemy: `IsAllyTargetedSpell/IsEnemyTargetedSpell`; targets: `SelectBestSpellTarget/GetValidSpellTargets`. Buff/Illusion spells already active on the caster are skipped.
4. Add spell-specific AI only when generic scoring is wrong: `GetComboScore`, `GetDispelScore`, `GetPreBuffBonus`, class/domain scores in `AISpellcastingStrategist`, or the category sets in `SpellCategoryClassifier`.
5. Execution: `AIService.TryExecuteSpellcastAction` -> `GameManager.TryNPCPerformSpellCastForAI` (`_Core/GameManager.cs:10982`) -> `TryNPCPerformSpellCast` (`NPCTurns.cs:758`). Put Shape A handlers in that chain (`:890-953`); Shape B is shared.
6. Tests: create `Assets/Scripts/Tests/Combat/<Spell>RulesTests.cs` as `namespace Tests.Combat { public static class XRulesTests { public static void RunAll() {...} private static void Assert(bool condition, string testName, string detail = "") {...} } }` with pass/fail counters, calling `SpellDatabase.Init()` (templates `WebSpellRulesTests.cs`, `CauseFearRulesTests.cs`).
   - Data: fields, `GetRangeSquaresForCasterLevel`, `ActiveSpellEffect.CalculateDurationRounds`, `ConditionRules.GetDefinition`.
   - Behavior: build a GameManager on a new GameObject, add and initialize a `ConditionService`, inject it into the private `_conditionService` field by reflection, then invoke the private TryResolve handler by reflection (`CauseFearRulesTests.cs:105-132`). Characters: `Tests/Utilities/TestHelpers.cs`, `MockCharacterFactory.cs`, `TestFixtures.cs`.
7. Run the suite from a temporary hook in the Editor (see [TESTING.md](TESTING.md)); `Tests/Services/ServiceTestRunner.RunAll` covers only service suites.

Files: `Character/Creatures/NPCDatabase_<X>.cs`, `Character/Creatures/DragonData.cs`, `_Core/GameManager.NPCSetup.cs`, `Services/AIService.cs`, `AI/AISpellcastingStrategist.cs`, `AI/SpellCategoryClassifier.cs`, `_Core/GameManager.NPCTurns.cs`, `Tests/Combat/<Spell>RulesTests.cs`. Examples: ce1fded, d47ec1b, fa519af, 098c088, f706ac6, 0dd2d61.

Pitfalls:
- Reflection tests bypass the dispatch chains and cannot catch dispatch bugs (dead `ApplySpellBuff` branches, reclassified `EffectType`, missing NPC wiring). Add one F12 check and one NPC encounter.
- An NPC that has the spell but stays on a melee routine never casts it, and nothing logs why. Check the archetype and `AIBehavior` (step 2) before debugging spell selection.
- NPCs never cast area spells and never use metamagic ([AI-001](issues/AI.md)). AI Quicken scoring looks for `ActionType` Swift (`AISpellcastingStrategist.cs:962-963`) but `ApplyMetamagicToSpellData` sets Free (`SpellCaster.cs:854-858`), so that bonus never fires.
- Placeholder spells are filtered out of template NPCs but not out of hand-written `NPCDatabase` entries.
- Test classes compile into Assembly-CSharp and ship in the player build.

## Add a metamagic feat

1. `Assets/Scripts/Spell/Components/MetamagicData.cs`: APPEND the value to `enum MetamagicFeatId` (`:12-24`). Add cases to all three level switches (static `GetLevelAdjustment(feat)` `:71`, instance `GetLevelAdjustment(feat, baseSpellLevel)` `:92`, `GetStandardLevelAdjustment` `:346`), a `CanXxx` predicate (`:144-182`) and a case in `IsApplicable` (`:184`), cases in `GetDisplayName(feat)` (`:205`), `GetAdjective` (`:226`) and `GetShortEffect` (`:263`), the id in `AllMetamagicFeats` (`:301`) and a case in `GetIdFromFeatName` (`:317`).
2. Define the feat in `FeatDefinitions.DefineMetamagicFeats` (`Character/Feats/FeatDefinitions.cs:1220-1348`): `var x = new FeatDefinition("Exact Name", desc, FeatType.Metamagic) { IsWizardBonus = true }; x.Benefit = new FeatBenefit { IsMetamagic = true, MetamagicId = MetamagicFeatId.X, Description = ... }; Add(x);`. The name must equal `MetamagicData.GetDisplayName(id)`: `SpellcastingComponent.GetKnownMetamagicFeats` checks `Stats.HasFeat(GetFeatName(id))`.
3. Effect:
   - Spell-data changes (range, area, duration, components, action type) go in `SpellCaster.ApplyMetamagicToSpellData` (`Spell/Casting/SpellCaster.cs:780-873`). All callers apply it to a `Clone()` (`OnSpellSelectedWithMetamagic`, the test panel, the scroll and wand pipelines, `BuildConsumableSpellVariant`, `MetamagicSystem.PrepareMetamagicSpell`).
   - Roll-time changes go in `SpellCaster.Cast` (flags `:60-62`, damage `:416-520`, healing `:523+`) AND in the baked approximation for scrolls and wands in `GameManager.BuildConsumableSpellVariant` (`_Core/GameManager.cs:6547-6655`).
4. Action economy and slots: `MetamagicSystem.ValidateMetamagicApplication / PrepareMetamagicSpell` (`Spell/Components/MetamagicModifier.cs:89-237`). The spontaneous-caster full-round rule is repeated at `GameManager.SpellCasting.cs:49, 1499, 1732, 3612`; Quicken special cases at `SpellcastingComponent.cs:3298/3352` and `CombatUI.cs:2830/3031`.
5. UI: the combat metamagic dialog, `SpellPreparationUI` and `CraftingWorkshopUI` enumerate known feats automatically. The F12 panel toggles are hard-coded: add a `CreateMetamagicToggle` line (`UI/Spells/SpellTestingPanel.cs:754-768`). Optional rod: a factory method calling `CreateMetamagicRod(MetamagicFeatId.X, RodPowerLevel..., ...)` in `Equipment/Rods/RodFactory.cs` (rods have no runtime effect yet, see [Add a rod](#add-a-rod)).
6. Tests: `Tests/Magic/MetamagicSystemTests.cs`. Update `TestMetamagicFeatIdEnumHasAll9Types` (asserts `AllMetamagicFeats.Length == 9`) and add adjustment, applicability and effect tests (on a `Clone()`).

Files: `Spell/Components/MetamagicData.cs`, `Character/Feats/FeatDefinitions.cs`, `Spell/Casting/SpellCaster.cs`, `_Core/GameManager.cs`, `Spell/Components/MetamagicModifier.cs`, `UI/Spells/SpellTestingPanel.cs`, `Equipment/Rods/RodFactory.cs`, `Tests/Magic/MetamagicSystemTests.cs`. Examples: 756aec7, b5f7987, 167daee, 616bf32, 0dd8e76, 8758492, 3e32bd8, bd20ab4.

Pitfalls:
- Missing switch cases fail silently: level adjustments default to 0 (a free feat), `IsApplicable` to false (never offered), `GetDisplayName` to "Unknown" (`HasFeat` never matches).
- Metamagic reaches only spells resolved by `SpellCaster.Cast`. Custom TryResolve handlers ignore `_pendingMetamagic`; NPCs never use metamagic.
- Only Heighten should change the save DC (616bf32, PHB p.88). `SpellCaster.Cast` uses the heightened level; `SpellUtilities.GetSpellSaveDC` ignores it.
- Static `GetDisplayName(feat)` and instance `GetDisplayName()` both exist (167daee fixed 13 call sites). The instance one joins `GetAdjective` values sorted by enum int and is used as a matching key; keep adjectives unique.
- Append, never insert: `ScrollData.MetamagicFeats`, `ItemData.ScrollMetamagicFeats` and `WandData` store `MetamagicFeatId` values.
- Three implementations must stay in sync: live cast, `BuildConsumableSpellVariant` (Empower approximated as average x 0.5 added to `BonusDamage`), and validation.
- `CanEnlarge` requires `RangeSquares > 0`; `CanExtend` uses the legacy `BuffDurationRounds`.

# Combat and characters

## Add a condition

1. Add the value to `Assets/Scripts/Combat/Conditions/CombatConditionType.cs` (keep `None = 0`; the enum is not cast to int or parsed).
2. Add `Add(new ConditionDefinition { Type, DisplayName, ShortLabel, Description, StackingRule = ConditionStackingRule.Refresh, AttackModifier, ArmorClassModifier, Fortitude/Reflex/WillModifier, InitiativeModifier, SkillCheckModifier, AbilityCheckModifier, PreventsMovement, MovementMultiplier = 1f, PreventsAoO, PreventsThreatening, PreventsStandardActions, PreventsFullRoundActions, PreventsSpellcasting, DeniesDexToAc, CoupDeGraceVulnerable, Hardness });` in `ConditionRules.BuildDefinitions` (`Combat/StatusEffects/StatusEffect.cs:98-813`). Aliases: `map[New] = map[Existing].CloneFor(New)` plus a case in `Normalize` (`:816`).
3. Mechanics follow from the definition: attack/AC/saves/initiative/ability checks via `CharacterStats.SumConditionValue`, skills, Dex denial, speed (`ConditionMovementMultiplier`, `CharacterConditions.CanMove`), turn skipping (`CanTakeActions` -> `GameManager.ShouldSkipTurnDueToHPState`), `CanAttack`, `CanCastSpells`, coup de grace (`ConditionRules.IsHelplessLike`).
4. Apply it: from GameManager code `_conditionService.ApplyCondition(target, type, rounds, source: caster, data: payload, sourceNameOverride: name, sourceCategory: "Spell", sourceId: spell.SpellId)` (`Services/ConditionService.cs:86`) or the public `GameManager.ApplyCondition`; elsewhere `target.ApplyCondition(type, rounds, sourceName)` (loses source, data and sourceId). Payloads: `Combat/Conditions/<Name>ConditionData.cs` (e.g. `EnfeebledConditionData`).
5. Expiry: `ConditionService.UpdateConditionTimers` raises `OnConditionExpired` -> `GameManager.HandleConditionExpired` (`_Core/GameManager.cs:3012`). Add a `TryHandle<X>ConditionExpiry` call there (pattern `:3032-3038`) for cleanup or a custom log.
6. UI: `StatusEffectIndicator` already shows `ShortLabel` and `Description`. Add a color in `GetConditionColor` (`UI/Combat/StatusEffectIndicator.cs:1192`) and, if needed, a label in `GetConditionLabel` (`:1172`). Optionally add `public bool IsX => HasCondition(CombatConditionType.X);` in `CharacterConditions`.
7. Behavior-forcing conditions (flee, random action): add `Combat/Behaviors/<Name>BehaviorController.cs` (existing: Charmed, Confused, Fascinated, Frightened), expose `TryGet<Name>TurnDecisionForAI/Execute<Name>TurnDecisionForAI` on GameManager (`_Core/GameManager.cs:10879-10920`) and call them at NPC turn start in `AIService` (`:73-110`). The PC side handles only Confused (`TryBeginConfusedPCTurn`).
8. Poisons/diseases that inflict it: map it in `PoisonSpecialEffect.ToConditionType` (`Effects/PoisonSpecialEffect.cs:66`).
9. Spell-applied and dispellable: also create an `ActiveSpellEffect` with `StatusEffectManager.AddEffect` and remove the condition in `RemoveEffect` and `DispelMagicService.HandleDispelSpecialCleanup`. Tests: definition and stat propagation (`Tests/Combat/CauseFearRulesTests.cs`, `CoreConditionRulesTests.cs`, `MediumConditionRulesTests.cs`).

Files: `Combat/Conditions/CombatConditionType.cs`, `Combat/StatusEffects/StatusEffect.cs`, `Combat/Conditions/<Name>ConditionData.cs`, `_Core/GameManager.cs`, `UI/Combat/StatusEffectIndicator.cs`, `Character/Stats/CharacterConditions.cs`, `Combat/Behaviors/`, `Services/AIService.cs`, `Effects/PoisonSpecialEffect.cs`. Examples: 19e8765, c1d0798, a3bf900, 40a5bd9, af568a4.

Pitfalls:
- A missing `ConditionRules` entry falls back to the None definition silently (`StatusEffect.cs:829-836`). `CombatConditionType.Commanded` (af568a4, applied in `Spell/Domain/GameManager_DomainPowers.cs:863`) has no definition: it shows as "None" and does nothing.
- `ConditionDefinition.IsFearCondition` and `GrantsCombatAdvantage` are not read outside `StatusEffect.cs`.
- `MovementMultiplier` 0 (the field default) means "no change" in `CharacterStats` unless `PreventsMovement` is set, but `CharacterConditions.CanMove` returns false for `<= 0`. Always set `MovementMultiplier = 1f` unless the condition slows movement.
- `CanTakeActions` skips the turn only when BOTH `PreventsStandardActions` and `PreventsFullRoundActions` are true.
- `CharacterConditions.GetConditionACModifier/GetConditionAttackModifier` and their CharacterController wrappers have no callers and hard-coded numbers; do not add modifiers there.
- A condition without a matching `ActiveSpellEffect` cannot be dispelled.
- `SumConditionValue` adds every entry: a StackBySource condition applied twice doubles its penalties.

## Add a feat with a combat effect

1. Define it in the matching `FeatDefinitions.Define<Category>Feats` in `Character/Feats/FeatDefinitions.cs` (Combat `:146`, Ranged `:426`, Defensive `:557`, TWF `:650`, Mounted `:710`, Unarmed `:781`, Skill `:846`, General `:934`, Metamagic `:1220`, ItemCreation `:1350`): `var f = new FeatDefinition("Exact Name", description, FeatType.Combat) { IsFighterBonus = true }; f.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 1)); f.Benefit.Description = "..."; Add(f);`. Other flags: `IsMonkBonus` + `MonkBonusLevel`, `IsWizardBonus`, `CanTakeMultiple`, `RequiresChoice`, `IsPlaceholder` (only a warning in `FeatSelectionUI`). Weapon-choice feats set `Benefit.RequiresWeaponChoice = true` and `CanTakeMultiple = true`.
2. Character creation, level-up and `FeatSelectionUI` pick it up through `FeatDefinitions.GetAvailableFeats`.
3. Add a query in `Character/Feats/FeatManager.cs`: `public static bool HasX(CharacterStats s) => s != null && s.HasFeat("Exact Name");` (`HasFeat` is an exact, case-sensitive `Contains`).
4. Hook the mechanic where it is read:
   - Saves, initiative, AC: `FeatManager.GetFortitudeSaveBonus/GetReflexSaveBonus/GetWillSaveBonus/GetInitiativeBonus/GetACBonus` (`:73-113`).
   - Attack/damage: `AttackCalculator.CalculateAllFeatModifiers` (`Combat/Core/AttackCalculator.cs:304`). Only `CharacterController.Attack` and `FullAttack` call it. Patch `DualWieldAttack` (`CharacterController.cs:5629`), `FlurryOfBlows` (`:11987`, no feat modifiers at all) and `PerformRakeAttacks` (`:5543`) separately.
   - On-hit riders: `PerformSingleAttackWithCrit` (`:6353`). AoO rules: `ThreatSystem.ExecuteAoO` (`Combat/Core/ThreatSystem.cs:491`). Spell DC: `SpellCaster.Cast` (Spell Focus at `:380`). SR: `SpellSaveResolver.RollSpellResistance` (`:242`).
5. Trackers: per round, a `CharacterStats` field (pattern `DeflectArrowsUsedThisRound`) reset in `CharacterController.StartNewTurn` (`:10340`); per day, a reset in `GameManager.RestorePartyAfterCombat` (`_Core/GameManager.cs:868`), which runs after every combat.
6. Toggle/activated feats: fields on `CombatUI` (`[Header("Feat Controls")]`), created in `SceneBootstrap.CreateFeatControls` (`_Core/SceneBootstrap.cs:952`; Power Attack `:958`, Rapid Shot `:976-982`), wired in `SceneBootstrap.WireButtons` (`:1241-1244`) to a public GameManager handler (pattern `OnPowerAttackSliderChanged`, `OnRapidShotTogglePressed`), refreshed in `CombatUI.UpdateFeatControls` (`:796`).
7. NPCs: Power Attack/Combat Expertise-like feats use `ManeuverPreferences.UsePowerAttack/UseCombatExpertise` (`AI/AIBehaviorData.cs:58-69`); others need explicit `AIService` logic. Give monsters the feat via `Feats` in `NPCDatabase_*.cs`.
8. Tests: `Tests/Feats/<Phase>Tests.cs` with the `MakeStats` helper pattern (`Phase1CombatFeatTests.cs:82-97`).

Files: `Character/Feats/FeatDefinitions.cs`, `FeatManager.cs`, `Combat/Core/AttackCalculator.cs`, `Character/Controller/CharacterController.cs`, `Character/Stats/CharacterStats.cs`, `_Core/GameManager.cs`, `Combat/Core/ThreatSystem.cs`, `Spell/Casting/SpellCaster.cs`, `SpellSaveResolver.cs`, `UI/Combat/CombatUI.cs`, `_Core/SceneBootstrap.cs`. Examples: 65be722, 65252bd, cfbd478, b1746a8, 36e7c39, 40b58d1.

Pitfalls:
- `FeatBenefit` numeric fields and flags (`AttackBonus`, `ACBonus`, `Grants*`, ...) are never read. Only `Benefit.SkillBonuses`, `RequiresWeaponChoice` and `Description` are consumed; behavior is keyed on the name string. A typo across FeatDefinitions, FeatManager, class `InitFeats` and NPC lists fails silently.
- 65be722 declared Stunning Fist and Manyshot toggle panels on CombatUI but never built them; `CombatFlowService.PerformWhirlwindAttack/PerformManyshotAttack` have no callers. Stunning Fist, Manyshot and Whirlwind Attack are unreachable in play. `StunningFistUsesRemaining` is never reset after combat.
- Spell Focus only affects spells resolved by `SpellCaster.Cast`; `SpellUtilities.GetSpellSaveDC` has no feat bonus.
- Class-scaled feats use `stats.GetClassLevel("Monk")`, not `stats.Level` (40b58d1). Weapon-specific feats store the weapon in lists like `stats.WeaponFocusWeapons` (b1746a8).
- Route SR through `RollSpellResistance`; cfbd478 had to patch inline SR checks for Spell Penetration.

## Add a class feature

Use for an activated PC class ability (Smite Evil, Lay on Hands, Wild Shape). Follow 897d2f4 (Bardic Music), the only recent feature wired end to end.

1. Data: a class under `Assets/Scripts/Character/Classes/<Class>/` (pattern `Bard/BardicMusicData.cs`) plus a field and lazy initializer on `CharacterStats` (`EnsureBardicMusicInitialized`, `CharacterStats.cs:573-600`) that scales with `GetClassLevel("Bard")` and the ability modifier. Static per-level tables go on the class (`PaladinClass.LayOnHandsPool`, `DruidClass.WildShapeUsesPerDay`).
2. Button: follow [Add a combat action button](#add-a-combat-action-button) (Bardic Music: `SceneBootstrap.cs:817`, `:1196-1197`; `ActionButtonPanel.ComputeClassAndSpellActionStates` `:764`).
3. Handler `public void OnMyButtonPressed()` (patterns `OnRageButtonPressed` `GameManager.cs:9171`, `OnBardicMusicButtonPressed` `:9201`): validate `ActivePC` and class, `pc.CommitStandardAction()`, apply, log, `UpdateAllStatsUI()`, `CombatUI.UpdateActionButtons(pc)`.
4. Lifecycle in `_Core/GameManager.cs`: per-turn ticks in `StartPCTurn` (`:3974`; `TickBardicMusic` call `:4049-4053`), daily refresh in `RestorePartyAfterCombat` (`:868`). Put end-of-combat cleanup (stop the effect, clear per-combat state) in `RestorePartyAfterCombat` or `ResetCombatStateForNextEncounter` (`:1076`), not in `OnCombatEnded` (`:3836`). After a normal victory the flow is `HandleCombatVictoryDetected` -> loot -> `ContinueToRestAndNextCombat` (`GameManager.LootCollection.cs:292-293`) -> `RestorePartyAfterCombat` -> `ReturnToEncounterSelection` -> `ResetCombatStateForNextEncounter`, and `OnCombatEnded` does not run ([CORE-002](issues/CORE.md)). `ResetCombatStateForNextEncounter` also runs from `StartCombat` when lingering combat state is detected.
5. Automatic class feats go in the class's `ICharacterClass.InitFeats`. A new class is registered in `ClassRegistry.Init` (`Character/Classes/ClassRegistry.cs:24-46`).
6. NPC use needs separate `AIService`/`NPCTurns` logic: the button is PC-only and `StartPCTurn` ticks do not run for NPCs.
7. Tests: `Tests/Classes/Phase1/2/3ClassTests.cs` pattern, plus a Play-mode check.

Files: `Character/Classes/<Class>/<Feature>Data.cs`, `Character/Stats/CharacterStats.cs`, `UI/Combat/CombatUI.cs`, `_Core/SceneBootstrap.cs`, `UI/Combat/ActionButtonPanel.cs`, `_Core/GameManager.cs`, `Tests/Classes/`. Examples: 897d2f4, 08883ed, 5ab33dd, 83e0f1f, 40b58d1.

Pitfalls:
- 08883ed and 5ab33dd added `SmiteEvilData`, `LayOnHandsData`, `FavoredEnemyData`, `CombatStyleData`, `WildShapeData` and their `CharacterStats` fields, but nothing in combat reads them. PC Smite Evil, Lay on Hands, Favored Enemy, Combat Style and Wild Shape are data-only. The existing Smite button is `TemplateSmiteSystem` (half-celestial/fiendish templates), not the Paladin feature.
- Use `pc.CommitStandardAction()` (applies the disabled-at-0-HP rule), not `pc.Actions.UseStandardAction()` as `OnBardicMusicButtonPressed` does.
- "Per day" resources reset after every victory in `RestorePartyAfterCombat`; there is no real rest/day cycle (CORE-015 in [issues/CORE.md](issues/CORE.md)).
- Existing cleanup that lives only in `OnCombatEnded` (the bardic-music stop, `ConditionService.CleanupOnCombatEnd`, `EffectService.ClearAll`) is skipped after a normal victory ([CORE-002](issues/CORE.md)). Do not copy that placement.

## Add a combat maneuver

Use for a new entry in the Special Attack menu.

1. Add a value to `enum SpecialAttackType` (`Character/Controller/CharacterController.cs:12-26`). Melee-contact maneuvers go in the melee case list of `CanPerformSpecialAttack` (`:7556-7572`); otherwise `default: return true` allows it with a ranged-only loadout.
2. Menu (`UI/Combat/CombatUI.cs`): `CreateSpecialButton("My Maneuver", "My Maneuver")` in `BuildSpecialAttackPanel` (`:1612`), a `case "My Maneuver":` in `WireSpecialAttackMenu` (`:1653`) invoking `onSelect?.Invoke(SpecialAttackType.X, false)`, enable logic in `IsSpecialAttackButtonEnabled` (`:975`), label in `UpdateSpecialAttackButtonLabel` (`:1044`).
3. Availability helpers in the GameManager partial in `Combat/Maneuvers/StandardManeuvers.cs` or a new `Combat/Maneuvers/<X>System.cs`. The MonoBehaviours `StandardManeuvers`, `SupportActions`, `GrappleSystem`, `OverrunSystem` and `TurnUndeadSystem` are empty shells added in `GameManager.cs:562-566`; the logic is `public partial class GameManager` in the same files.
4. Selection: `GameManager.OnSpecialAttackSelected` (`_Core/GameManager.cs:8762`): extend the `hasAction` ternary (`:8838-8854`) and the `reason` ternary (`:8858-8876`).
5. Targeting and execution in `_Core/GameManager.CombatActions.cs`: range in `ShowSpecialAttackTargets` (`:1385`), click in `HandleSpecialAttackTargetClick` (`:1481`), then `ExecuteSpecialAttack` (`:1514`): fear-break list, action consumption, `maneuverProvokesAoO` (`:1723`), melee reactions (`:1899-1900`), `FinalizeSpecialAttackResolution`.
6. Rules: `CharacterController.ExecuteSpecialAttack` (`:10392`): add to `breaksInvisibility` and the switch, calling `private SpecialAttackResult ResolveMyManeuver(CharacterController target)` (patterns `ResolveTrip` `:10503`, `ResolveFeint` `:11328`).
7. AI: a `ManeuverPreferences` flag (`AI/AIBehaviorData.cs:58-69`), returned from `AIProfile.GetPreferredManeuver` (`AI/AIProfile.cs:292`), validated in `AIService.ShouldUseManeuver` (`:2213`). Execution: `AIService.TryExecutePreferredManeuver` -> `GameManager.TryNPCSpecialAttackByTypeForAI` -> `TryNPCSpecialAttackIfBeneficial` (`NPCTurns.cs:289-383`).
8. An Improved <X> feat follows [Add a feat](#add-a-feat-with-a-combat-effect); AoO suppression is checked inline in `CombatActions.ExecuteSpecialAttack` (`:1731-1734`).
9. Tests: `Tests/Maneuvers/<X>RulesTests.cs` (patterns `CoupDeGraceRulesTests.cs`, `OverrunRulesTests.cs`).

Files: `Character/Controller/CharacterController.cs`, `UI/Combat/CombatUI.cs`, `_Core/GameManager.cs`, `_Core/GameManager.CombatActions.cs`, `Combat/Maneuvers/`, `_Core/GameManager.NPCTurns.cs`, `AI/AIProfile.cs`, `AI/AIBehaviorData.cs`, `Services/AIService.cs`. Examples: 44eb44e, e44792a, bcc0d56, 4afd4a0, b1ea348.

Pitfalls:
- Melee-contact maneuvers must call `MeleeReactionService.TriggerReactions(attacker, target, null)` on BOTH paths (4afd4a0 and b1ea348 fixed forgotten Fire Shield retribution). Both paths still trigger it only for Trip and Disarm (`CombatActions.cs:1899-1900`, `NPCTurns.cs:366-367`).
- The NPC path calls `npc.ExecuteSpecialAttack` directly and skips the PC AoO block and attack budgets. It logs every maneuver result with `CombatLogHelper.Death` (`NPCTurns.cs:363`); use a semantic helper.
- Availability is checked in four places (`IsSpecialAttackButtonEnabled`, `OnSpecialAttackSelected`, `CombatActions.ExecuteSpecialAttack`, `CanPerformSpecialAttack`); keep them consistent.
- Menu wiring keys on `btn.name`; a typo gives a dead button. A missing switch case in `CharacterController.ExecuteSpecialAttack` returns "tries an unknown maneuver" with `Success = false`.

## Add a combat-log message

1. Inside a GameManager partial: `CombatUI?.ShowCombatLog(CombatLogHelper.<Semantic>("<icon>", $"..."));` (about 1,200 call sites). `CombatUI.ShowCombatLog` (`UI/Combat/CombatUI.cs:279`) -> `CombatLogPanel.AddMessage` (`UI/Combat/CombatLogPanel.cs:93`; `MaxMessages = 500`).
2. Pick the helper by meaning from `Combat/Logging/CombatLogHelper.cs` (`:52-284`): `SpellEffect/BuffApplied`, `Buff`, `Damage/DamageWithHP`, `Failure/CriticalFailure`, `Success/Healing/SpellResisted`, `Info/NoEffect`, `Warning`, `Special`, `SaveResult(targetName, success, saveType, roll, dc)`, `ConditionApplied/ConditionFaded`, `Expired`, `Summon/SummonRaw`, `SpellResistance`, `Immune`, `Interrupted/InterruptedRaw`, `Debuff`, `Defensive`, `Curse`, `Death`, `Stub/StubRaw`, `StatusEnd`, `PaleBlue/IceBlue/RoyalBlue`, `Color(text, hex)` with the `Color*` constants. Most take `(icon, message)`; check the signature.
3. Services use their provider (`private CombatUI CombatUI => _combatUIProvider?.Invoke();`, e.g. `Services/DispelMagicService.cs:48`). Static classes and components use `GameManager.Instance?.CombatUI?.ShowCombatLog(...)` or `CombatLogger.Show(msg)`. Pure rule services take an `Action<string>` callback (`EffectService.TickClericSpell2Durations`).
4. Multi-line blocks: build a `StringBuilder` framed by `═══` lines and push once (`GameManager_Spells_MagicFang.cs:99-106`, `TryResolveConeOfColdSpell`). Spells resolved by `SpellCaster` already produce `SpellResult.GetFormattedLog()` with dice (8758492).
5. Mirror important events with a tagged `Debug.Log("[System] ...")`; attack details also go to the Console when `GameManager.LogAttacksToConsole` is set. A new helper gets a test in `Tests/Services/CombatLogHelperTests.cs`.

Files: `Combat/Logging/CombatLogHelper.cs`, `Tests/Services/CombatLogHelperTests.cs`. Examples: b16aee5, be1864a, 8c43782, f6da0e8, 71b869b, 2c94e7e.

Pitfalls:
- `CombatLogHelper.Hit/Miss` do not exist (71b869b); use `Damage/Failure`.
- `StatusEffectManager.LogCombatMessage` only writes `Debug.Log`: "doesn't stack" messages never reach the in-game log.
- Always use `CombatUI?.`; CombatUI is null in tests and early init.
- Helpers wrap the whole string in one `<color>` tag; do not pass pre-colored strings (`ApplySpellBuff` nests colors near `GameManager.SpellCasting.cs:7700`). An empty icon gives a leading space.
- `CombatLogPanel` rewrites substrings such as "- HIT!", "CRITICAL HIT!", "has been slain!" for highlighting (`CombatLogPanel.cs:362-377`).
- Prefer `CombatUI?.ShowCombatLog` inside GameManager partials and services; `CombatLogger` (`Combat/Logging/CombatLogger.cs`) is mostly `Format*` string builders, and its `Show` has only 2 callers, both in the unused `SpellResolutionService`.

# Items and economy

All item kinds end up as `ItemData` in `ItemDatabase`; rings, rods and wondrous items are injected by their own databases during `SceneBootstrap` (order: `ItemDatabase.Init`, ring/wondrous/rod `RegisterAll...InItemDatabase`, `SpellDatabase.Init`, `CraftableItemRegistry.Init`; `_Core/SceneBootstrap.cs:1060-1080`). Golden rule 5 (copy new fields in `CloneItem`) applies to every recipe here.

## Add a mundane weapon, armor or shield

1. Add a snake_case id constant to `Equipment/Items/ItemIDs.cs` (`DND35e.Identifiers`). `ItemDatabase._items` is case-sensitive.
2. Register it in the matching `ItemDatabase.RegisterSimpleMeleeWeapons / RegisterSimpleRangedWeapons / RegisterMartialMeleeWeapons / RegisterMartialRangedWeapons / RegisterLightArmor / RegisterMediumArmor / RegisterHeavyArmor / RegisterShields`, following the local style:
   - Simple melee weapons: `ItemBuilder.Weapon(id).Named().Desc().Simple().Melee().Light()|OneHanded()|TwoHanded().Damage(count, sides, "type").Crit(min, mult).Reach(...)/Thrown(ft).Weight().Price().Icon().Build()`.
   - Body armor: `ItemBuilder.Armor(id).AC().MaxDex().CheckPenalty().SpellFailure().LightArmor()|MediumArmor()|Heavy().Metal()|NonMetal().Tags()`.
   - Simple ranged, martial melee, martial ranged and shields: a literal `new ItemData { ... }` (ranged: `RangeIncrement`, `RequiresAmmoType`, `RequiresReload`, `IsLoaded`, `ReloadAction`).
3. Slot: the builders set `EquipSlot.EitherHand` (weapon), `Armor`, `LeftHand` (shield); set the same yourself on literals. `Register` syncs and clamps `EnhancementBonus` (0-5) and infers size, light/two-handed, reach and attack range.
4. Price: `BasePriceGp` (`.Price(gp)`) or an entry in `ApplyDefaultBasePrices`. Otherwise `StoreInventory.ResolveBaseValue` falls back to 10 gp (weapon, shield) or 25 gp (armor).
5. Optional enum id: add `ItemID.Xxx` in `Equipment/Items/ItemID.cs` in its range (simple melee 2000-2099, martial melee 2100-2199, ranged 2200-2299, armor 3000-3299, shields 3300-3399) and map it in `ItemIdToString` (`Utilities/IdentifierExtensions.cs`). For +1/+2 variants add `{BaseEnumName}Plus1`/`Plus2` values (weapons 7000s, armor 8000-8099, shields 8100s); `ItemDatabase.RegisterEnhancedEquipmentVariants` clones bases in [2000,3400) that have them, with ids `{baseId}_plus1/_plus2`.
6. Store: `Add(ItemID.Xxx, "Mundane Weapons"|"Armor"|"Mundane Shields", priceGp)` in `StoreInventory.InitializeStore` ([Add an item to the store](#add-an-item-to-the-store)).
7. Material and masterwork variants: add the id to `ItemMaterialFactory.CommonWeaponIds / WoodenWeaponIds / CommonArmorIds / CommonShieldIds` (`Equipment/Enchantments/ItemMaterialFactory.cs`); `RegisterAllMaterialVariants` creates `mw_`, `cold_iron_`, `silver_`, `adamantine_`, `darkwood_`, `mithral_` ids. NPC spawn upgrades fall back to the plain item when a variant is missing.
8. A new `ItemData` field (as the torch commit added `NoStrengthToDamage`): declare it in `ItemData.cs`, copy it in `CloneItem` and `ItemBuilder.CopyBaseProperties`, show it in `ItemData.GetStatSummary`, consume it in `CharacterController`.
9. Distribute: class kits (`BarbarianClass.SetupStartingEquipment`: `inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.X), EquipSlot.RightHand)`) and NPC `EquipmentIds` (`new EquipmentSlotPair(ItemIDs.X, EquipSlot.RightHand)`). Test by extending a suite such as `Tests/Combat/DamageModifierTests.cs`.

Files: `Equipment/Items/ItemIDs.cs`, `ItemID.cs`, `ItemDatabase.cs`, `ItemBuilder.cs`, `ItemData.cs`, `Utilities/IdentifierExtensions.cs`, `Equipment/Enchantments/ItemMaterialFactory.cs`, `Equipment/Store/StoreInventory.cs`. Examples: 4111694, 2e8e4c0, c2dfe2c, 86d89af, 749f7e9, 525fcb8.

Pitfalls:
- `ItemDatabase.Init` sets `_initialized = true` BEFORE registering, and `ItemBuilder.Build()` throws on validation failure (missing Name, `DamageDice` 0, `CritMultiplier < 2`). One bad builder call aborts Init and every later `Get()` silently uses a partial database.
- `CloneItem` copies every public field except the 4 treasure fields; a field you forget is lost on every purchase, NPC equip and variant.
- NPC `EquipmentIds` must use `EquipSlot.RightHand/LeftHand/Armor/ArmorRobe`. `MainHand/OffHand/Ranged` are rejected and `Inventory.DirectEquip` returns silently; 58 existing entries in 15 `NPCDatabase_*.cs` files use them (e.g. `NPCDatabase_P.cs:280-281`).
- `ItemDatabase.Get(string)` and `CloneItem(string)` are `[Obsolete]` (warning only). Prefer the `ItemID` overloads, use `GetItem(string)`, or wrap in `#pragma warning disable CS0618`.
- Most PHB weapons (glaive, halberd, flails) have only string ids: no store entry and no +1/+2 variants until you add the enum value and mapping.

## Add a weapon or armor special ability

Use for an enchantment such as Flaming, or a pre-built enchanted item.

1. Append a value to `EnchantmentType` (`Equipment/Enchantments/EnchantmentType.cs`) in the right section (order is not load-bearing).
2. Register an `EnchantmentStats` entry in the matching `EnchantmentProperties.RegisterXxx()` (model: `RegisterWeaponElementalAbilities`/Flaming): `Type`, `DisplayName`, `Description`, `BonusEquivalent` (or `FlatCostGp`), `Slot`, `MeleeOnly/RangedOnly/RequiresSlashingOrPiercing`, `IncompatibleWith/RequiredEnchantments` and the effect fields.
3. If the ability only uses fields combat already reads, you are done: `ExtraDamageType/ExtraDamageDice/ExtraDamageDieSides`, `CritBonusDice*`, `IsAlignmentDamage/Alignment*`, `IsBane/Bane*`, `Vicious*`, `VorpalEffect`, `WoundingEffect` (log only), `FortificationPercent`, `AlignmentBypassTag`, Brilliant Energy fields. Readers are in `CharacterController.PerformSingleAttackWithCrit` (`EnchantmentEffects.GetEnchantmentAttackBonus`, `Roll*Damage`, `CheckVorpalEffect`, `CheckFortification`, `GetEnchantmentBypassTags`).
4. Otherwise add an `EnchantmentStats` field, a static helper in `EnchantmentEffects`, and a call in `PerformSingleAttackWithCrit` (attacker side) or its `defenderArmor/defenderShield` block. Creation-time stat changes go in `EnchantmentFactory.ApplyAbilitySideEffects` (Throwing, Distance, Keen; the Keen flag is spelled `DoublesThreadRange`).
5. Validation: `EnchantmentFactory.ValidateAbility` and `CheckIncompatibilities`; `CreateEnchantedVariant` also rejects bonuses outside 1-5 and totals above +10.
6. Pre-built item: `EnchantmentFactory.CreateEnchantedVariant(ItemIDs.BASE, bonus, new[] { EnchantmentType.X }, out result[, baneCreatureType])` in `ItemDatabase.RegisterCommonEnchantedItems`. The id is `{baseId}_plus{N}_{enumname lowercased}` (e.g. `longsword_plus1_flamingburst`). These are NOT listed in the store.
7. Tooltip and price come from the `EnchantmentStats` entry (`ItemData.GetEnchantmentTooltipSection`, `GetEnhancedPriceGp`).

Files: `Equipment/Enchantments/EnchantmentType.cs`, `EnchantmentProperties.cs`, `EnchantmentStats.cs`, `EnchantmentEffects.cs`, `EnchantmentFactory.cs`, `Character/Controller/CharacterController.cs`, `Equipment/Items/ItemDatabase.cs`. Examples: 5c626d9, 49d4bbf, a81ac58, 9db2030, 749f7e9, df170fe.

Pitfalls:
- Data-only despite 5c626d9's "combat integration": these helpers have no game callers: `GetEnergyResistance`, `ApplyEnergyResistance`, `GetSpellResistance`, `GetSkillBonus`, `GetEnchantmentDR`, `GetTotalSpellResistance`, `ApplyInvulnerabilityDR`, `GetDefendingACBonus`, `NegatesConcealment`, `ReturnsWhenThrown`, `GrantsExtraAttack`, `HasDancingAbility`, `IsAnimatedShield`, `CheckDisruptionEffect`. Armor/shield resistances, SR, DR and skills, plus Speed, Defending, Seeking, Returning, Dancing, Animated and Disruption, do nothing.
- Ghost Touch does nothing: `HasGhostTouchWeapon` checks a `"ghost_touch"` visual tag that nothing adds.
- `EnchantmentFactory.ApplyEnchantment` is not idempotent (Keen would double again); today only `CreateEnchantedVariant` calls it.
- The specific-item path is dead: `SpecificItemDatabase.CreateSpecificItem` and `MagicItemLootGenerator` have no callers, and `BaseItemId` values are display names that never match ItemDatabase keys. Fix that first before adding `SpecificItemDefinition`s and `SpecificItemBehavior` subclasses (hooks: `OnPreAttackRoll`, `OnDamageRoll/OnCriticalHit`, `OnHitApplied/OnKill`, `OnAttackedBy`). `CloneItem` shares the behavior object by reference.
- Treasure loot does not use enchantments: `TreasureItemConverter.ConvertMagicItem` builds placeholder `ItemData` with no dice and no enchantment data.
- `CreateMagicWeapon/CreateMagicArmor` (no callers) would generate `{base}_plus{N}`, colliding with the +1/+2 variant ids.
- Only the fields listed in step 3 work without touching combat code; any other effect field needs step 4.

## Add a wondrous item

1. Add an id to `Equipment/Wondrous/WondrousItemNames.cs` (`DND35e.Identifiers`), or interpolate for tiers (`$"headband_of_intellect_{bonus}"`).
2. Add a public static factory in `Equipment/Wondrous/WondrousItemFactory.cs` calling the private `CreateBaseWondrous(id, name, desc, EquipSlot.X, priceGp, casterLevel, weightLbs, icon, iconColor)`, then set effect fields: `WondrousItemType`, `WondrousAbilityBonus/Type`, `WondrousACBonus/WondrousACBonusType`, `WondrousSaveBonus/WondrousSaveType`, `WondrousSkillBonus/WondrousSkillName/WondrousSkillBonusType`, movement fields, `WondrousHasActivation`, `WondrousActivationType`, `WondrousUsesPerDay/PerWeek/PerMonth`, `WondrousMaxCharges`.
3. `Register(WondrousItemFactory.CreateX())` in the right slot section of `WondrousItemDatabase.Init`. Duplicate keys are skipped with a warning.
4. Store listing ("Wondrous Items") and crafting (Craft Wondrous Item, when `BasePriceGp > 0`) are automatic.
5. Passive effects: `Inventory.ApplyAllWondrousItemBonuses` resets the `CharacterStats` `Wondrous*` fields and applies each equipped item (highest wins per field). A NEW effect needs: an `ItemData` field, a `CloneItem` copy (reset use counters to 0), a `GetStatSummary` line, a `CharacterStats` `Wondrous*` field, a reset line, an apply branch in `ApplyWondrousItemBonuses`, and a consumer.
6. Hooks: `WondrousItemActivation.OnWondrousEquipped/OnWondrousUnequipped` (from `Inventory.cs`) and `OnRest` (GameManager rest handler, next to `RingActivationManager.OnRest`).

Files: `Equipment/Wondrous/WondrousItemNames.cs`, `WondrousItemFactory.cs`, `WondrousItemDatabase.cs`, `WondrousItemActivation.cs`, `Equipment/Inventory/Inventory.cs`, `Equipment/Items/ItemData.cs`, `ItemDatabase.cs`, `Character/Stats/CharacterStats.cs`. Examples: 85df096, 388526f, 7ca75b8, fb358ad, d9845dc, 016dff7, 05cb67f.

Pitfalls:
- Activated abilities are unreachable from the UI. The only dispatch (`WondrousItemActivation.TryActivate`) is in the SpellEffect branch of `GameManager.ApplyConsumableEffectAndConsume`, but `TryUseConsumableFromInventory`, `InventoryUI` and `QuickItemUsePanel` require `IsConsumable`. Even when reached, command-word and use-activated abilities only consume a use and log text. Boots of Speed haste ticking (`TickHasteRound`) has no callers.
- `OnWeeklyReset` and `OnMonthlyReset` have no callers.
- Ten 100 gp test items (`test_wondrous_*`, `test_slotless_item`) are registered for real and appear in the store and crafting.
- `RegisterAllInItemDatabase` skips ids ItemDatabase already has, so a colliding id is silently not injected.
- Wondrous SR uses `SpellResistance = Max(SpellResistance, WondrousGrantedSR)`, overwriting the base field; the SR stays after unequipping.

## Add a ring

1. Add an id to `Equipment/Rings/RingNames.cs` (`DND35e.Identifiers`).
2. Add `public static RingFactory.CreateXRing()` (model `CreateProtectionRing`): `Type = ItemType.Ring`, `Slot = EquipSlot.EitherRing`, `IsRing = true`, `RingId = Id`, `RingCasterLevel`, `BasePriceGp`, `CountsAsMagicForBypass = true`, `IconChar = RingIcon`, plus `Ring*` effect fields.
3. `Register(RingFactory.CreateXRing())` in the tier block of `RingDatabase.Init`. Store ("Rings") and crafting (Forge Ring) are automatic.
4. Passive effect: a reset line in `Inventory.ResetRingBonuses`, an apply branch in `Inventory.ApplyRingBonuses` (highest wins via `Mathf.Max` on an `OwnerStats.Ring*` field), the `CharacterStats` field, the `CloneItem` copy (ring block) and a `GetStatSummary` line.
5. Active ability: fill `RingAbilities` with `RingAbility` entries (`AbilityId`, `DisplayName`, `Frequency`, `MaxUsesPerPeriod` or charge cost, `ActionType`, `RequiresTarget`, `RangeFeet`, `CasterLevel`, `SaveDC`, `SaveType`), add `case RingNames.X:` in `RingActivationManager.TryExecuteAbility` with a private static `ActivateX(actor, ring, ability, out resultMessage)`. Uses are tracked in `RingUseTracker` by `RingInstanceId` + `AbilityId`. Hooks: `OnRingEquipped/OnRingUnequipped`, `OnRest`.

Files: `Equipment/Rings/RingNames.cs`, `RingFactory.cs`, `RingDatabase.cs`, `RingAbility.cs`, `RingActivationManager.cs`, `RingUseTracker.cs`, `Equipment/Inventory/Inventory.cs`, `Equipment/Items/ItemData.cs`, `ItemDatabase.cs`, `Character/Stats/CharacterStats.cs`. Examples: a77a6f7, d061ece, b4f7fac.

Pitfalls:
- Active abilities are unreachable from the UI, for the same `IsConsumable` reason as wondrous items. Only passive bonuses and the equip/rest hooks run.
- Ring of Protection deflection stacks on every recalculation: `ResetRingBonuses` zeroes a private field without subtracting it from `OwnerStats.DeflectionBonus`. Do not copy that pattern for a new AC bonus.
- `TestHelpers.EnsureCoreDatabasesInitialized` does not call `RingDatabase.RegisterAllRingsInItemDatabase`; tests must.

## Add a rod

1. Add an id to `Equipment/Rods/RodNames.cs` (global namespace) and its all-ids list.
2. In `RodFactory.cs` add `CreateRodOfX()` calling the private `CreateBaseRod(...)` (sets `Type = ItemType.Wondrous`, `Slot = EquipSlot.EitherHand`, `IsRod = true`, `RodId`, `RodCategory`, `RodCasterLevel`, `BasePriceGp`), set `Rod*` fields, and add it to `CreateAllRods`.
3. `RodDatabase.Init` registers everything from `CreateAllRods`; store ("Rods") and crafting (Craft Rod) are automatic.
4. New field: `ItemData` (Rod block), `CloneItem` (reset counters), `GetStatSummary` (`if (IsRod)`). Test in `Tests/Equipment/RodTests.cs`.

Files: `Equipment/Rods/RodNames.cs`, `RodFactory.cs`, `RodDatabase.cs`, `RodData.cs`, `Equipment/Items/ItemData.cs`, `ItemDatabase.cs`. Examples: a23241a, afd2df5.

Pitfalls:
- There is no `ItemType.Rod` or `ItemType.Staff` (afd2df5 fixed a compile error from assuming one). Test `item.IsRod`.
- Rods have no gameplay effect: no game code reads `Rod*` fields except `RodCasterLevel`. `MetamagicRodActivation` and `MetamagicData.ApplyFromRod` are used only by tests, and `CreateBaseRod` does not set `IsWondrous`.
- `RodDatabase.ResetDailyUses/ResetWeeklyUses` are called only from tests.
- `CreateAllRods` (`RodFactory.cs:565-615`) returns 33 rods: 21 metamagic (7 feats x 3 power levels), 7 combat and 5 utility. Three of the combat/utility rods (Lordly Might, Alertness, Security) are built with `RodCategory.Legendary` and `IsLegendary = true`, so they are not a fourth group. The file header comment and the list capacity still say 36; trust the method body.

## Add a staff

1. Add `Register(new StaffDefinition { ... })` in `StaffDatabase.RegisterTier1Staves..RegisterTier4Staves` (`Equipment/Staves/StaffDatabase.cs`): `StaffId`, `Name`, `Description`, `AuraSchool`, `CasterLevel`, `MarketPrice`, `AllowedClasses`, `Status`, `ImplementationNotes`, `MaxCharges` (default 50), and `Spells` built with `Spell(SpellNames.X, "Display", level, chargeCost)` or `StubSpell(...)`. Spell ids must be canonical.
2. **Required and missing today**: nothing turns a `StaffDefinition` into `ItemData`, and nothing sets `IsStaff = true`. Write a factory modelled on `Equipment/Inventory/WandFactory.cs` that builds `ItemData` with `Type = ItemType.Consumable`, `Slot = EquipSlot.None`, `ConsumableEffect = ConsumableEffectType.SpellEffect`, `IsStaff = true`, `StaffId`, `StaffCharges = def.MaxCharges`, `StaffCasterLevel`, `BasePriceGp`, `WeightLbs`, and register it via `ItemDatabase.RegisterExternal` or from `ItemDatabase.Init` so it exists before `CraftableItemRegistry.Init`.
3. The use path exists: inventory/quick-use -> `TryUseConsumableFromInventory` -> `ApplyConsumableEffectAndConsume` (IsStaff branch) -> `GameManager.TryUseStaff` (`StaffValidator`) -> `StaffSpellSelectionPanel.Open`, which deducts `ItemData.StaffCharges` and calls `ConvertStaffToMundane` at 0.
4. Store: add a staves section to `StoreInventory.InitializeStore`. Crafting (`CraftableItemRegistry.RegisterStaves`) and the tooltip already handle `IsStaff`.

Files: `Equipment/Staves/StaffDatabase.cs`, `StaffDefinition.cs`, `StaffValidator.cs`, `UI/Combat/StaffSpellSelectionPanel.cs`, `_Core/GameManager.cs`, `Equipment/Items/ItemDatabase.cs`, `Crafting/CraftableItemRegistry.cs`, `Equipment/Store/StoreInventory.cs`. Examples: d9da4c4, d07d269, ddbaf8d, bc77db6, aa1ad37.

Pitfalls:
- Until step 2 exists, all 20 staves are unreachable and 0 are craftable.
- Charges live on `ItemData.StaffCharges`, not on the shared `StaffDefinition.CurrentCharges`.
- Staves cannot be recharged (ddbaf8d, DMG rules).
- A stale spell id (aa1ad37 removed `domain_` prefixes and class suffixes) silently becomes "not implemented".
- A `Consumable` staff cannot be wielded as a quarterstaff; decide this explicitly.

## Add a scroll, potion or wand

Usually no item code is needed: implement the spell ([Add a spell](#add-a-spell-data)) with `IsPlaceholder = false` and at least one class or domain list.

1. `ItemDatabase.Init` runs `ScrollFactory.RegisterAllScrolls`, `PotionFactory.RegisterAllPotions` and `WandFactory.RegisterAllWands` after `RegisterConsumablesAndMisc`. They generate:
   - scrolls for every level: `scroll_{arcane|divine}_{spellId}`;
   - potions up to level 3 (`PotionFactory.MaxPotionSpellLevel`) that pass `IsEligibleForPotion` (rejects Personal range, Self without Touch, and Area damage): `potion_{spellId}`;
   - wands up to level 4: `wand_{spellId}`.
   Healing spells get `ConsumableEffect = HealHP` with heal dice; others get `SpellEffect`. Store categories `Scroll/Potion/Wand (Lvl X)` and crafting definitions are automatic. Check the startup logs `[ScrollFactory] Registered ...`, `[PotionFactory]`, `[WandFactory]`.
2. Hand-authored item: `ItemBuilder.Scroll/Wand/Potion(id).ForSpell(...).CasterLevel(cl).SpellLevel(n).Price(gp).BuildAndRegister()` (`.Healing(count, sides, bonus)` for HealHP). `Build()` creates `ScrollData` only; for a wand set `item.Wand = WandData.Create(spell, cl, isArcane, gp)` and keep `CurrentCharges/MaxCharges` in sync. Register before the factories run (from `RegisterConsumablesAndMisc`). `ItemDatabase.RegisterSpellPotion` is private.
3. Use-time dispatch (`_Core/GameManager.cs`): scrolls -> `TryUseScroll` (`:5799`), wands -> `TryUseWand` (`:6193`), potions -> `TryApplySpellConsumableEffect` (`:5674`), which applies only Healing, the Mirror Image case and Buff/Debuff/Illusion/Control via `AddEffect`.
4. Targeted spells: `TryUseScroll` and `TryUseWand` each keep their own `needsTargeting` rule (Damage/Summon/Escape/Dispel/Wall/Divination/Utility, or SingleEnemy/SingleAlly/Area/Touch). Only when it is true AND a combat is running does the item go through `InitiateScrollCastThroughPipeline` (`:5945`) or `InitiateWandCastThroughPipeline` (`:6074`). Change BOTH lists together.

Files: `Spell/Database/SpellDatabase_*.cs`, `Equipment/Inventory/ScrollFactory.cs`, `PotionFactory.cs`, `WandFactory.cs`, `ScrollValidator.cs`, `WandValidator.cs`, `Equipment/Items/ScrollData.cs`, `WandData.cs`, `ItemBuilder.cs`, `_Core/GameManager.cs`. Examples: f312e75, c857d9b, e9ef0ef, d80e7c1, 0982d08, 03a093d, 3e32bd8, 8758492, 4e2dd1a, 616bf32, 6bfa259.

Pitfalls:
- Store `spell.SpellId` in `ConsumableSpellName`. The legacy potions (`potion_cure_light_wounds`, `potion_healing`, `potion_shield_of_faith`) store display names and work only through the `GetSpellByName` fallback (03a093d fixed WandValidator calling it with an id).
- `PotionFactory` and `WandFactory` skip existing ids; `ScrollFactory` overwrites. So the legacy `potion_cure_light_wounds` wins over the factory one, with `ConsumableEffect = SpellEffect` (not HealHP).
- An item with `ConsumableEffect` None fails with "has no implemented consumable effect yet". `CraftingExecutor.CreatePotion` never sets it, so crafted potions probably cannot be used (4e2dd1a fixed the same for scrolls; not verified in Play mode).
- Wand charges live in both `ItemData.CurrentCharges` and `WandData.CurrentCharges` (3e32bd8 fixed drift). Wand sell value is implemented twice, differently (`StoreInventory.GetSellPrice`, `EconomyService.GetSellPrice`).
- Wand DC is `10 + SL + floor(SL/2)`; only Heighten raises it (616bf32). Consumable metamagic is applied in `BuildConsumableSpellVariant`.
- Legacy `ItemID` scroll enums map to ids no factory creates (`scroll_magic_missile` vs `scroll_arcane_magic_missile`). Use the factory id strings.
- Crafted consumables get GUID ids that are not in ItemDatabase.

## Add an item to the store

1. The item must be registered in ItemDatabase; `StoreInventory.Add(string, ...)` skips unknown ids with a log.
2. In `StoreInventory.InitializeStore` (`Equipment/Store/StoreInventory.cs:49-129`): `Add(ItemID.X, category, priceGp)` for enum items; for string ids a helper like `AddRingsToStore` calling `AddExternalItem(id, category, price)`, or the public `AddScrollItem/AddPotionItem/AddWandItem`. `Add` does not de-duplicate (2c4d076 removed duplicate potions).
3. Automatic +1/+2 variants: add the base with category "Mundane Weapons", "Mundane Shields" or "Armor" BEFORE the `AddEnhancedStoreVariants()` call (`:94`), and the `Plus1/Plus2` enum values must exist. Later entries (e.g. `WeaponTorch`) get none.
4. Use a category that `StoreUI.MatchesBuyCategory` (`StoreUI.cs:780-811`) maps: "Weapons" = contains "Weapon"; "Armor" = exactly "Armor" or "Magic Armor"; "Shields" = contains "Shield"; Scrolls/Potions/Wands by prefix; Rings, Rods, Wondrous Items, Ammunition by exact name; "Gear" = "Gear" or "Spell Component". Anything else shows only under "All". A new dropdown category needs `BuyCategoryOptions`, a `MatchesBuyCategory` case, and optionally sub-filters; selling uses `GetSellItemCategory`.
5. Pricing: `priceGp` is the buy price, used by `ResolveBaseValue` for selling, and copied to `BasePriceGp` only if that is <= 0. `StoreUI.BuyItem` clones the item, calls `GameManager.SpendGold`, adds it to `PartyStash`, and refunds if the stash rejects it. Check the log `[Store] Initialized with N items`.

Files: `Equipment/Store/StoreInventory.cs`, `StoreUI.cs`, `Equipment/Items/ItemDatabase.cs`, `ItemID.cs`, `Services/EconomyService.cs`. Examples: fdd49c9, 4111694, b0e35ac, ab503e8, 537b9e9, afd2df5, 6bfa259, 2c4d076.

Pitfalls:
- `AddEnhancedStoreVariants` writes `BasePriceGp` on the SHARED ItemDatabase template (`StoreInventory.cs:160`).
- Sell price has two implementations: `StoreInventory.GetSellPrice` (StoreUI) and `EconomyService.GetSellPrice` (`BasePriceGp/2`, used by `SellItem` and tests). Change both.
- The "Special Properties", "Specific" and "Special Material" sub-filters match nothing currently listed; staves are never listed.
- Rings, rods and wondrous items are listed only because their `AddXToStore` helpers inject them into ItemDatabase first (b0e35ac).

## Make an item craftable

1. Recipes are derived. `CraftableItemRegistry.Init` (`Crafting/CraftableItemRegistry.cs:34-57`) builds definitions from: `RegisterRings` (`RingDatabase.GetAllRings()`, Forge Ring), `RegisterRods` (`IsRod`, Craft Rod), `RegisterWondrousItems` (`IsWondrous`, not rod), `RegisterStaves` (`IsStaff`), `RegisterArmsAndArmorEnhancements` (+1..+5 upgrades); all need `BasePriceGp > 0` and skip `IsSpecificItem`. Scrolls, potions and wands are generated on demand.
2. To make an item craftable, register it in its source database with `BasePriceGp > 0` and its `*CasterLevel`, and make sure it is in ItemDatabase before `CraftableItemRegistry.Init` (execution clones `def.ItemId`). Tests call `CraftableItemRegistry.Reset()`.
3. Spell prerequisites: canonical SpellIds in `def.RequiredSpellIds` (exact match, no aliases). `AddSpellPrereqsFromItemDescription` is an empty stub, so rings, rods and wondrous items have NO prerequisites; add an explicit mapping there.
4. A new feat/category needs: a `CraftingFeatType` value; entries in `CraftingConstants.FeatNames` and `FeatCasterLevelReqs`; a feat with the same name in `FeatDefinitions`; a label in `CraftingWorkshopUI.GetFeatTabLabel`; an item-list case (`:436-461`) or a `Register*` pass; cost formulas in `CraftingCostCalculator`; and a `CraftingExecutor.CreateDynamicItem` case for generated items.
5. Execution (`CraftingExecutor.Execute`): spends `CharacterStats.ComponentGold` and XP, then upgrade / dynamic / `CloneItem` branches; a null result refunds. Missing spells add +5 DC (`MissingSpellDCIncrease`).
6. Test with the workshop debug toggle (`CraftingValidator.DebugMode`, 219e8ab) and `Tests/Crafting/CraftingSystemTests.RunAll`.

Files: `Crafting/CraftableItemRegistry.cs`, `CraftingFeatType.cs`, `CraftingConstants.cs`, `CraftingCostCalculator.cs`, `CraftingValidator.cs`, `CraftingExecutor.cs`, `UI/Crafting/CraftingWorkshopUI.cs`, `Character/Feats/FeatDefinitions.cs`. Examples: 6931142, c5d5086, 219e8ab, bd20ab4, 4e2dd1a, 0982d08, 616bf32.

Pitfalls:
- Weapon enhancement tiers require `"magic_weapon_greater"` (`CraftableItemRegistry.cs:262`), which does not exist (canonical: `SpellNames.GREATER_MAGIC_WEAPON` = `greater_magic_weapon`), so the prerequisite is always missing.
- Crafting spends per-character `ComponentGold` (default 1000), not the party gold the store uses.
- Crafted potions lack `ConsumableEffect` (see the scroll/potion recipe). Staves are never craftable until they exist as `ItemData`. The test wondrous items are craftable.
- The workshop opens for the first party member with any crafting feat; the crafter cannot be chosen.

# Creatures and encounters

## Add a monster or NPC

1. File: Monster Manual creatures in `Character/Creatures/NPCDatabase_{Letter}.cs` (all `static partial class NPCDatabase`; there is no `_Z`), custom and test NPCs in `NPCDatabaseCustom.cs`, dragons via `DragonData.cs` + `NPCDatabase_Dragons.cs`, pre-templated undead/lycanthropes via their factories and `NPCDatabase_Skeletons/Zombies/Lycanthropes.cs`.
2. Write `private static void RegisterX() { Register(new NPCDefinition { ... }); }` with a globally unique name, and call it from `RegisterCreatures_{Letter}()` (custom: `NPCDatabase.RegisterCustomCreatures`, `NPCDatabase.cs:177-208`). A new letter file also needs its call in `NPCDatabase.Init` (`:18-60`).
3. Core fields (`NPCDefinition`, `NPCDatabase.cs:489-637`): unique snake_case `Id`, `Name`, `ChallengeRating` as a string ("1/2"), `Level` (HP) and `HitDice` (BAB, saves), `CharacterClass` ("Warrior" for plain monsters), `CreatureType` (must parse in `CreatureTypeProgressionDatabase.GetFromString`, else Humanoid with a warning), `CharacterAlignment`, `SizeCategory`, `IsTallCreature`, ability scores (`CharacterStats.NO_SCORE` = -1 for "—"), `NaturalArmorBonus`, `BaseSpeed` in SQUARES (6 = 30 ft), `BaseHitDieHP`, `NaturalAttacks` (`NaturalAttackDefinition`: dice, `IsPrimary`, `PoisonOnHitId`, `ParalysisOnHitDC`, `EnergyDrainOnHit`, ability drain, `HasDiseaseOnHit` + `DiseaseOnHitType`).
4. BAB: `NPCDefinition.BAB` is NOT read at spawn. BAB comes from type progression and `HitDice` unless you set `BABOverride` or `BaseAttackBonusOverride`. Saves: `Fortitude/Reflex/WillSaveOverride`.
5. Other fields: DR, resistances, immunities, `IsMindless`, `SpellResistance`, regeneration, `Feats`, `SpecialAbilities` (display only), breath weapons, `FrightfulPresence`, `Engulf`, `RangedSpecialAttack`, `BloodDrain`, `TerrainManipulation`, stench, `AuraAbility`, `IsIncorporeal`, `CreatureTags`, `AIProfileArchetype`, `EquipmentIds`, `BackpackItemIds`, spells, colors, `Description`.
6. A NEW special-ability kind needs: an `NPCDefinition` field, a deep copy in `NPCDefinition.Clone` (`:641-737`), a `CharacterController.ConfigureX`, a call next to the others in `InitializeNPCFromDefinition` (`_Core/GameManager.NPCSetup.cs:601-624`), AI use in `AIService`, and the template `CopyDefinitionFields` lists. Follow the Gibbering Mouther commits (20beb70, 7d26daf, e4123a5).
7. Poison ids must exist in `Effects/PoisonDatabase.cs` (425669d); diseases use the `DiseaseType` enum + `Effects/DiseaseDatabase.cs`.
8. AI: pick an archetype with a case in `BuildRuntimeAIProfile` (`NPCSetup.cs:758-829`). A new one needs an `NPCAIProfileArchetype` value, an `AIProfile` subclass in `AI/Profiles/` and a case. Archetype None + type Animal falls back to `AnimalAIProfile`.
9. Optional: encounter presets, the dungeon name map, Summon Monster lists, token art (see the next recipes).
10. Spawn it and compare the log `[GameManager] {Name}: HP .. AC .. Atk ..` (`NPCSetup.cs:752-755`) with the Monster Manual.

Files: `Character/Creatures/NPCDatabase*.cs`, `_Core/GameManager.NPCSetup.cs`, `Character/Controller/CharacterController.cs`, `Services/AIService.cs`, `AI/Profiles/`, `Effects/PoisonDatabase.cs`, `Effects/DiseaseDatabase.cs`. Examples: 76ff4b2, fab0cf6, c35dc5d, 248f61a, 425669d, 6723207, 20beb70, 7d26daf, e4123a5, fa519af, 96a3982, 5a09abf, 66997bc.

Pitfalls:
- `NPCDatabase.Register` silently overwrites; `dretch` is defined twice. `RegisterSummonCreatureAliases` runs LAST and overwrites `badger`, `riding_dog`, `owl`, `raven`, `giant_bee` with clones of other creatures, so Summon Monster I "Owl" and "Badger" spawn eagle and dire-badger stats.
- Private method names must be unique across all partial files (96a3982, CS0111). One missing brace broke 15 files (5a09abf).
- HP double-counts CON: `MaxHP = BaseHitDieHP + max(1, CONmod) x Level`, but most entries store the MM total including CON (Satyr 5d6+5 = 22 spawns with 27). Creatures with no or negative CON still gain +1 HP per level.
- Use `NO_SCORE` for absent abilities; 0 means "reduced to 0" (incapacitated). About 15 entries use 0 (e.g. Shadow).
- `EquipmentIds` with `MainHand/OffHand/Ranged` are not equipped (58 entries). CR >= 1 NPCs may get random material or masterwork upgrades.
- Archetypes `Brute` (29 NPCs) and `Caster` (2) have no `BuildRuntimeAIProfile` case and fall back to legacy `NPCAIBehavior`.
- Spellcasting initializes only if the class is a spellcasting class AND spells are listed. The Vampire (`CharacterClass = "Fighter"`) silently loses its spells.
- `Clone` does not deep-copy `RangedSpecialAttack`, `BloodDrain`, `TerrainManipulation`.
- Any NPC with a parseable `ChallengeRating` is a random-encounter candidate (only summon tags/ids are excluded). Leave CR null for test NPCs.
- `wyvern_poison` is defined twice in `PoisonDatabase.cs`; the later one wins.
- Check numbers against the MM: 7d26daf corrected DCs right after 20beb70 added the creature.

## Add a creature template

1. Implement `ICreatureTemplate` (`Character/Templates/CreatureTemplateFramework.cs:10-25`): `TemplateId`, `ApplicationOrder` (10 undead, 15 lycanthrope, 20 outsider; lower first), `ApplyToDefinition(NPCDefinition)`, which mutates IN PLACE a clone made by `CreatureTemplateRegistry.ApplyTemplatesClone`. Stat overlays subclass `OutsiderTemplateBase` (`CelestialTemplate`, `FiendishTemplate`). Static templates that return a new object are wrapped like `SkeletonCreatureTemplate` (call the static `Apply`, then `CopyDefinitionFields` back).
2. Register an instance in `CreatureTemplateRegistry._templates` (`:28-42`). The dictionary KEY is the id matched against `AppliedTemplateIds` (case-insensitive, trimmed); `TemplateId` is not used for lookup.
3. Make it idempotent: check for its own id or a marker before mutating; de-duplicate `AppliedTemplateIds` as `OutsiderTemplateBase.AddTemplateId` does.
4. Apply through data: `NPCDefinition.AppliedTemplateIds` (e.g. fiendish wolf, `NPCDatabaseCustom.cs:831`), `SummonMonsterOption.TemplateId` (applied in `GameManager.SpellCasting.cs:262-273`), dungeon `EncounterCreatureEntry.CreatureTemplateIds`, or scenario injection in `GameManager.BuildEncounterDefinitionForSpawn` (`_Core/GameManager.cs:2828-2860`).
5. Runtime flags NPCDefinition cannot express are mapped in `InitializeNPCFromDefinition` (celestial/fiendish smite, `NPCSetup.cs:571-591`; summons at `GameManager.SpellCasting.cs:277-278`).
6. Optional pre-baked variants: a static factory like `SkeletonFactory`, registered in `NPCDatabase_Skeletons.cs` etc., with presets from the matching `Get*EncounterPresets()`.
7. Spawn one templated creature and compare its spawn log with a hand calculation.

Files: `Character/Templates/CreatureTemplateFramework.cs`, the Skeleton/Zombie/Lycanthrope template files, `UndeadTemplateUtils.cs`, `_Core/GameManager.NPCSetup.cs`, `_Core/GameManager.cs`, `Spell/Resolution/GameManager.SpellCasting.cs`, `Encounters/DungeonEncounterSpawner.cs`. Examples: 9570426, 137c9e1, 9c70d7c, 5da7ce9, 2df0304, 2691203, f36567c.

Pitfalls:
- Templates are probably applied twice (code reading; not verified in Play mode). Every spawn runs `ApplyTemplatesClone`; pre-baked skeletons, zombies and lycanthropes already carry their ids, the static `Apply` methods have no guard, and `DungeonEncounterSpawner` applies templates before the spawn applies them again (outsider bonuses stack).
- `CopyDefinitionFields` copies a fixed subset. Fields that `SkeletonTemplate.Apply` clears (breath weapons, engulf, auras, regeneration, incorporeal) are not copied back. Extend the lists when `NPCDefinition` gains fields.
- Unknown template ids are ignored silently. The parser emits `half-dragon`, which is not registered.
- The summon path clears `AppliedTemplateIds` before adding `option.TemplateId`.
- `NPCTemplate`/`TemplateData`/`NPCTemplateDatabase` in the same folder are DMG class-level stat-block templates, not creature templates.

## Add creature token art

1. Grid token: `Assets/Resources/Icons/Tokens/{key}_token.png` (existing ones are 48x48 RGBA). `IconLoader.GetToken(key)` lowercases the key, loads `Icons/Tokens/{key}_token`, falls back to `Sprites/Icons/{key}_token`, and caches.
2. Map names to the key in `IconLoader.DetermineMonsterType(name)` (`UI/Common/IconLoader.cs:121-135`), an ordered substring check used for spawns and summons. Without a token the NPC uses `Sprites/npc_enemy_alive` tinted with `SpriteColor`.
3. Side-panel and initiative icon (separate system): add entries keyed by BOTH npc id and display name to `IconManager.EnemyIconMap` (`UI/Common/IconManager.cs:62-89`), pointing at a file in `Assets/Resources/Sprites/Icons/`.
4. PC tokens and portraits use `{class}_token` / `{class}_portrait` through `IconLoader.GetToken` / `GetPortrait`.

Files: `Assets/Resources/Icons/Tokens/`, `Assets/Resources/Sprites/Icons/`, `UI/Common/IconLoader.cs`, `UI/Common/IconManager.cs`. Examples: 6ccae2c, 7c7c03b, d17fd90, 281dab7.

Pitfalls:
- `DetermineMonsterType` is crude: "hobgoblin" is unreachable behind "goblin", "Sorcerer" matches "orc", Werewolf and Dire Wolf get the wolf token. Put specific checks first.
- `ogre_token` is referenced but does not exist. Dungeon spawn ids (`spawn_goblin_3`) never match `EnemyIconMap`.
- Import settings are not versioned (golden rule 6).
- `Assets/StreamingAssets/CreatureTokens/` holds 278 PNGs extracted from the Monster Manual PDF (d17fd90). The loader was reverted (281dab7), nothing uses them, and the folder README documents a deleted API. This is copyrighted WotC artwork; ask the owner before wiring it in or shipping it.
- Only the first 3 enemies have side panels (15 enemy slots, 3 panels).

## Add an encounter preset or test scenario

1. Simple preset: append `new EncounterPreset(id, displayName, description, new List<string>{ npcIds })` in `NPCDatabase.ListEncounterPresets` (`NPCDatabase.cs:86-166`), or in the themed `Get*EncounterPresets` for dragons, skeletons, zombies, lycanthropes. Grep that the id is unique and every NPC id exists.
2. No UI work: `EncounterSelectionUI.IsMechanicsPreset` files it under "MECHANICS TESTS" if the id or name contains "test" or the description contains "validate" or "mechanic", else "FULL SCENARIOS".
3. Test-only NPCs: `RegisterXxx()` in `NPCDatabaseCustom.cs`, called from `NPCDatabase.RegisterCustomCreatures` (`NPCDatabase.cs:178-210`; pattern `RegisterXPPinataGoblin`). Use an id containing `_test`, `_drill` or `test_` so the custom builder hides it, and leave `ChallengeRating` null.
4. Custom party, positions or overrides: mirror an existing scenario (e.g. `mirror_image_test`) and grep all its touch points:
   - a `private const string XxxTestPresetId` (`GameManager.cs:100-121`) and a `private bool _isXxxTestEncounter` (`:123-144`);
   - set the flag in `ApplyEncounterPreset` (`:1769-1794`) and reset it in `ApplyRandomEncounter` (`:1712-1733`);
   - an `else if (_isXxxTestEncounter) ConfigureXxxTestParty();` before `RestoreStandardPartyLayout()` (`:1809-1854`), implemented in `_Core/GameManager.TestConfigs.cs` (pattern `ConfigureMirrorImageTestParty` `:1620`: build stats, `PC1.Init(...)`, inventory, spellcasting with `SpellNames` ids, StatusEffectManager, ConcentrationManager, `SetPCActiveState`, instructions in the combat log);
   - spawn positions: a `XxxTestSpawnPositions` array next to `EncounterSpawnPositions` (`GameManager.cs:2862+`) and a branch in `SetupEnemyEncounter` (`NPCSetup.cs:70-160`). Keep cells in 0..19 and off PC squares;
   - optional hooks: `BuildEncounterDefinitionForSpawn` (Clone before adding templates), `ApplyScenarioSpawnOverrides` (`NPCSetup.cs:320`), per-NPC tweaks (`NPCSetup.cs:184-235`), post-spawn instructions (`:242-290`), `GetForcedFirstInitiativeActors` (`GameManager.cs:3744`). Some spell code also checks the flags (`GameManager_Grease.cs`, `GameManager.SpellCasting.cs:1238`).

Files: `Character/Creatures/NPCDatabase.cs`, `NPCDatabaseCustom.cs`, `_Core/GameManager.cs`, `_Core/GameManager.TestConfigs.cs`, `_Core/GameManager.NPCSetup.cs`. Examples: 76ff4b2, 0ce24ae, 08e0be3, 7428faa, e44792a, 9d722f2.

Pitfalls:
- Hard cap of 15 enemies (`SceneBootstrap.cs:119`); extras are dropped silently. Only 3 side panels.
- Default positions cover 5 enemies; index 5+ falls back to `(15 + i, 10)`, which is off the 20x20 grid and unclamped. Presets with more than 5 creatures (`creature_showcase`, `tier1_vermin_rats`, `tier1_3_showcase`, `undead_showcase`) probably place creatures off-grid; random and dungeon encounters share this path (not verified in Play mode).
- Unknown NPC ids are caught only at spawn (slot deactivated). `GetEncounterPreset` returns the FIRST preset for an unknown id, so a typo loads the wrong fight.
- Every `_isXxx` flag must be reset in `ApplyRandomEncounter`, or it leaks into random, custom and dungeon encounters.
- `ConfigureXxxTestParty` overwrites PC1-PC4; `RestoreStandardPartyLayout` only re-activates them, so a later normal fight probably keeps the test characters.

## Change dungeon, random or custom encounters

1. Dungeon tables (DMG Tables 1-8) load from `Assets/StreamingAssets/dungeon_encounters.csv` (header `Dungeon_Level,Roll_Min,Roll_Max,Encounter`, 253 rows, LF line endings). Keep each level's d% ranges contiguous over 1-100; gaps and overlaps are only warnings.
2. The `Encounter` text is parsed by `EncounterDescriptionParser.Parse` (dice, compounds, class NPCs, templates). `DungeonEncounterTableData.BuildAllTables` (hard-coded) is used only if the CSV fails.
3. Names resolve through `_creatureNameMap` (`DungeonEncounterTableManager.InitializeTypoCorrections`, `:506+`). `ResolveCreatureName` tries the map with the exact name, the lowercase name, then without one trailing "s"; otherwise it snake-cases the name WITHOUT stripping plurals. Add a map entry whenever the text does not normalize to an existing id, and grep the target id.
4. Live flow (the only one wired to the UI): `GameManager.PromptEncounterSelection` opens `EncounterSelectionUI`; its "DMG Tables" footer button runs `EncounterSelectionUI.OnDMGTablesPressed` (`UI/Encounter/EncounterSelectionUI.cs:653`), which opens `DungeonEncounterGeneratorUI`. Its "Start Combat" button (`OnStartCombatPressed`) invokes the `onStartCombat` callback, which calls `DungeonEncounterSpawner.PrepareEncounter` (clones each base creature, applies class levels and templates, registers `spawn_{baseId}_...` ids via `NPCDatabase.RegisterExternal`) and passes `result.EnemyIds` to `onStartRandomEncounter` with a null `GeneratedRandomEncounter`. GameManager's callback then runs `ApplyRandomEncounter` (`_Core/GameManager.cs:1690`) -> `SetupEnemyEncounter` (`_Core/GameManager.NPCSetup.cs:29`) and opens the pre-combat hub. The random generator and the custom builder enter through the same `onStartRandomEncounter` callback.
   - `Encounters/GameManager.DungeonEncounters.cs` (`StartDungeonEncounter`, `StartDungeonEncounterFromStrings/FromResult`, `PrepareDungeonEncounter`, `OpenDungeonEncounterGenerator`) is an unused bridge with no callers ([ENC-015](issues/ENC.md)). It is the only code that calls `DungeonEncounterSpawner.CleanupSpawnEntries`. Change the live path, or route the live path through it first.
5. Levels: `MaxLevel = 8` and `HardcodedMaxLevel = 8` (`DungeonEncounterTableManager.cs:57, 60`); the generator UI clamps to `MaxLevel`. A new level needs CSV rows AND a higher `MaxLevel` (level 9 was added in e6a9002 and removed in 6008d6f).
6. Random generator: no data edits. `RandomEncounterSystem.BuildCandidates` takes every NPC with a parseable CR that is not a summon (tags SummonBase/SummonAlias/Summoned or "summon" in the id). Type and environment filters read `CreatureType`, `CreatureTags`, `Name` and `Description`; steer it with tags and description keywords.
7. Custom builder: no data edits. `CustomEncounterBuilderUI` lists all NPCs except ids containing `_test`, `_drill`, `test_`, `pinata`, `target_dummy`, `grease_test`, caps at 8, and computes its own spawn positions.
8. Tests: `Tests/Encounters/Phase5IntegrationTests.cs`, `Tests/Character/RandomEncounterSystemTests.cs`.

Files: `Assets/StreamingAssets/dungeon_encounters.csv`, `Encounters/DungeonEncounterTableManager.cs`, `DungeonEncounterTableData.cs`, `EncounterDescriptionParser.cs`, `DungeonEncounterSpawner.cs`, `RandomEncounterSystem.cs`, `UI/Encounter/EncounterSelectionUI.cs`, `DungeonEncounterGeneratorUI.cs`, `CustomEncounterBuilderUI.cs`, `_Core/GameManager.cs`, `_Core/GameManager.NPCSetup.cs` (`Encounters/GameManager.DungeonEncounters.cs` is unused). Examples: 4fa1b48, cf6584c, 9147a9e, 77a9544, cb05db7, c01c872, 2513ce7, 76676a0, e6a9002, 6008d6f, 40e0383, 936bbc1, b2b7bb5.

Pitfalls:
- Unresolved names fail only at spawn ("base creature '<id>' not found", entry skipped). 40e0383 removed map entries that pointed at nonexistent ids. If every entry fails, the encounter does not start and the selection panel reopens.
- `spawn_*` ids are never cleaned up on the live path: each DMG encounter leaves its `NPCDatabase` entries for the rest of the session, and they show up in the custom builder list and as random-encounter candidates ([ENC-004](issues/ENC.md)). A new filter over `NPCDatabase` should exclude the `spawn_` prefix.
- Templates are applied twice (spawner, then `BuildEncounterDefinitionForSpawn`); celestial/fiendish bonuses stack.
- More than 5 creatures use off-grid default positions (see the previous recipe); more than 15 are truncated.
- `Phase5IntegrationTests` still asserts `MaxLevel == 9` and fails since 6008d6f; update it when you touch the tables.
- Editing the CSV does not update the hard-coded fallback.
- The CSV is read with file IO from `Application.streamingAssetsPath`: Editor and standalone only.
- Keep LF endings; 6008d6f rewrote all 522 lines through line-ending churn.
- `RandomEncounterSystem` calls `UnityEngine.Random.Range` directly, not `DiceService`.

# UI

All UI is built in code; there are no prefabs and no scene edits are needed. `SceneBootstrap.CreateUI` makes one ScreenSpaceOverlay canvas (sortingOrder 100, CanvasScaler 1920x1080). Most panels use legacy uGUI `Text`; `ActionButtonPanel`, `LevelUpUI`, `CombatEndXPUI` and `LootCollectionUI` also use TMPro.

## Add a combat action button

Use for a top-level button in the combat action grid (not the Special Attack menu).

1. Field: `public Button MyButton;` on `CombatUI` (`UI/Combat/CombatUI.cs`, `[Header]` groups around `:80-170`).
2. Create it in `SceneBootstrap.CreateActionButtonsSection` (`_Core/SceneBootstrap.cs:661`, grid `:746-844`): `combatUI.MyButton = CreateGridButton(btnGrid.transform, "MyBtn", "Label", color);` (grid order = creation order). Wire it in the coroutine `SceneBootstrap.WireButtons` (`:1101`): `if (ui.MyButton != null) ui.MyButton.onClick.AddListener(() => GameManager.Instance.OnMyButtonPressed());`.
3. `ActionButtonPanel` (`UI/Combat/ActionButtonPanel.cs`): a private accessor (pattern `:30-74`), `states.Set(MyButton, new ActionButtonState(visible, enabled, label))` in the right `Compute*States` method (`ComputeSpecialActionStates` `:617`, `ComputeClassAndSpellActionStates` `:712`, `ComputeEquipmentActionStates` `:844`), and `SetActive(false)` in `HideNonGrappleActionButtons` (`:1161`) and `HideAllActionButtons` (`:1202`). Also add it to CombatUI's hide list (`:410-425`).
4. Handler (public, in `_Core/GameManager.cs` or a domain partial): check `CurrentPhase`/`ActivePC`, spend the action (`pc.CommitStandardAction()`, `pc.Actions.UseMoveAction()`, `UseFullRoundAction()`), log, `UpdateAllStatsUI()`, then `CombatUI.UpdateActionButtons(pc)` or `ShowActionChoices()`.
5. If it provokes an AoO: add a value to `AoOProvokingAction` (`Combat/Core/AoOProvokingAction.cs`) and call `ShowAoOActionConfirmation` (`GameManager.cs:11429`) with an `AoOProvokingActionInfo { ActionType, ActionName, ActionDescription, Actor, ThreateningEnemies, OnProceed, OnCancel }` (examples: DrinkPotion `GameManager.cs:4514-4520`, Movement `GameManager.CombatActions.cs:696-702`).
6. Target selection: follow the special-attack flow (`CurrentSubPhase` or a pending-mode field, highlights, `HandleXTargetClick`; `GameManager.CombatActions.cs:1385-1514`).
7. In Play mode check visibility on the PC turn, the NPC turn and while grappling.

Files: `UI/Combat/CombatUI.cs`, `_Core/SceneBootstrap.cs`, `UI/Combat/ActionButtonPanel.cs`, `_Core/GameManager.cs`, `Combat/Core/AoOProvokingAction.cs`. Examples: 897d2f4, 44eb44e, a44274b, f632d63.

Pitfalls:
- A field that SceneBootstrap never creates stays null and every null-guarded path skips it silently: `StunningFistPanel`/`ManyshotPanel` (65be722), `BardicMusicStatusText` (897d2f4), `DualWieldButton`, `FullAttackDefensivelyButton`. Create it in SceneBootstrap or clone it at runtime (as CombatUI does for `DischargeTouchButton`).
- `ActionButtonPanel.ApplyButtonStates` only touches buttons that got a `Set()` this refresh; others keep their previous state.
- Without the Hide* entries the button stays visible during grapples and NPC turns.
- Derive state from `CharacterStats`/`ActionEconomy` on each refresh (`ActionButtonPanel.UpdateActionButtons` via `CombatUI.UpdateActionButtons`), not from cached button state.

## Add a UI panel

1. Put the MonoBehaviour in `UI/Combat`, `UI/Encounter`, `UI/Inventory`, `UI/Spells`, `UI/Crafting`, `UI/CharacterSheet`, `UI/CharacterCreation`, `UI/Wish` or `UI/Common`. `UI/Panels` is an empty leftover.
2. Pattern A, always-present modal (combat panels): `public void BuildUI(Canvas canvas)` builds a full-screen dark overlay `Image` (anchors 0..1) with a centered root and ends with the overlay inactive. Expose `Open(...)`, `Close()`, `public bool IsOpen { get; private set; }` and `Action` callbacks (pattern `UI/Combat/QuickItemUsePanel.cs`). Add a public field on GameManager (e.g. `QuickItemUsePanel`, `GameManager.cs:84`) and in `SceneBootstrap.SetupGameManager` (`:997+`): `var x = canvas.gameObject.AddComponent<X>(); x.BuildUI(canvas); gm.X = x;` (examples `:1032-1054`). If a combat button opens it, follow [Add a combat action button](#add-a-combat-action-button) with a handler like `OnUseItemButtonPressed` (`GameManager.cs:4610-4654`); a44274b shows every touch point.
3. Pattern B, on-demand full-screen panel: the owner creates it lazily with two explicit null checks (`if (x == null) x = FindObjectOfType<X>(); if (x == null) x = gameObject.AddComponent<X>();`, `GameManager.OpenCraftingWorkshopFromPreCombat`), and the panel builds itself in a private `EnsureBuilt()` on first `Open`, finding the canvas with `GetComponentInParent<Canvas>()` then `FindObjectOfType<Canvas>()` (`CustomEncounterBuilderUI.cs:113-118`).
4. Pre-combat hub button: add an optional `Action` parameter at the end of `PreCombatHubUI.Open` (`UI/Encounter/PreCombatHubUI.cs:38-61`), a `CreateButton(...)` row (`:148-156`; `firstButtonY = 180`, step 94), pass the callback from `GameManager.OpenPreCombatHubPhase`, call `PreCombatHubUI?.HideMenu()` before opening, and pass `onClose: () => ReturnToPreCombatHubFromSubWindow("X.Back")` (6931142 added the Crafting Workshop this way).
5. Helpers: prefer `UIFactory` (`UI/Common/UIFactory.cs`: `GetDefaultFont`, `CreateButton`, `CreateLabel`, `CreatePanel`, `CreateScrollPanel`, `CreateInputField`, layouts) and `UITheme` over the private `MakePanel/MakeText` copies in older panels.
6. Call `SetAsLastSibling()` on open. Keep a raycast-target `Image` on the overlay: `InputService.BuildClickContext` drops grid clicks only when the pointer is over UI. Modals during the player's turn also add an `X != null && X.IsOpen` check to `GameManager.CanProcessWorldInput` (`GameManager.cs:2316-2340`).
7. Hotkey debug panels are attached in `CreateUI` and poll `Input.GetKeyDown` themselves (`SpellTestingPanel`, F12). `activeInputHandler: 2` enables both input systems.
8. Check the layout in Play mode at 1920x1080 and at a smaller window.

Files: `UI/<Area>/<NewPanel>.cs`, `_Core/SceneBootstrap.cs`, `_Core/GameManager.cs`, `UI/Encounter/PreCombatHubUI.cs`, `UI/Common/UIFactory.cs`, `UI/Common/UITheme.cs`, `Services/InputService.cs`. Examples: a44274b, fdd49c9, 140de6f, 6931142, cb05db7, 936bbc1, 516dbff, 4798c86, f632d63.

Pitfalls:
- Fonts: load `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` first; `Arial.ttf` is no longer built in (4798c86). Use `UIFactory.GetDefaultFont`; do not copy `SpellStorageUI.cs:145/166/182`.
- Do not use `??` or `?.` on `UnityEngine.Object` for create-if-missing: they bypass Unity's `== null`, so a destroyed component counts as present. Some existing code still does `GetComponent<X>() ?? AddComponent<X>()`.
- A Pattern A panel never assigned to its GameManager field fails only at runtime ("panel is not available").
- The pre-combat hub uses fixed pixel offsets for six buttons; a seventh may overflow. Layout regressions were the most common UI follow-up fix (790b5d1, 6f65a92, ef0ec6e, 382d47e, fafa38e, 2a9f6d1, f632d63, b0e35ac); anchor-based layouts held up better.
- `InputService.HandleKeyboardInput` runs before the `CanProcessWorldInput` gate, so a modal does not block hotkeys unless the handler checks for it.
