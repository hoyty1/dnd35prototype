> Verified against commit 11c2228 on 2026-10-07 (static audit by 20 agents with adversarial verification; not verified in Play mode).

# PC/NPC rules parity

Owner rule (2026-10-07): the rules governing mechanics must be 100% the same for PCs and NPCs. Who decides (UI or AI), presentation, and per-creature data may differ; formulas and resolution code may not. This doc records how far the code is from that and the plan to get there. It is a static audit; nothing was verified in Play mode. Re-verify a row before relying on it. Fixed issues are deleted from `docs/issues/`, so a missing ID means fixed.

## 1. Verdict (2026-10-07)

No, the project does not follow the principle yet, though the core is in better shape than the edges. About 60 distinct mechanics were verified shared. These include the attack modifier (`BuildAttackBonus`), hit/crit/damage resolution (`PerformSingleAttackWithCrit` -> `ApplyIncomingDamage`), the HP state machine, condition application, movement AoOs, maneuver opposed checks, grapple sub-actions, `SpellCaster.Cast`, save negation, casting-while-threatened, and every equipment-derived stat. Against that are roughly 70 distinct divergences: 39 are new (filed 2026-10-07, including CMB-105 below) and about 32 are already in `docs/issues`. They come from four structural causes, not from side checks inside formulas (a grep finds almost none there):
- orchestrators duplicated per side, each re-implementing the post-attack and pre-cast hooks differently;
- turn-start logic split between `StartPCTurn` and `BeginNPCTurnForAI`, around a shared skip gate;
- two spawn paths that feed the shared stat formulas different inputs (class tables and race for PCs, creature-type progression and no race for NPCs);
- executors that exist for one side only: PC-only area spells, summons, metamagic, items, class abilities and TWF; AI-only monster specials, compulsions and perception.

Several forks key on controllability (`IsControllable`) where allegiance or the creature is meant, so PC-cast summons get "PC rules" and AI-run allies get "NPC rules" (CORE-014).

## 2. Where the paths fork

| Family | PC entry | NPC entry | Shared core reached |
|---|---|---|---|
| Turn start | `OnTurnStarted` -> `IsPC = IsControllable` -> `StartPCTurn` (GameManager.cs:3974): effects, then skip gate, then rage and bardic ticks, then Confused only | `SingleNPCTurnFromInitiative` (NPCTurns.cs:35): skip gate first -> `AIService.ExecuteNPCTurn` -> `BeginNPCTurnForAI` (GameManager.cs:11003; effects plus perception) -> HP<=0 gate -> Charmed/Fascinated/Frightened controllers | `ShouldSkipTurnDueToHPState`, `StartNewTurn`, `NextInitiativeTurn`, `OnNewRound` |
| Weapon attacks | buttons -> `CombatFlowService.PerformPlayerAttack` -> iterative sequence (`Attack` per click), single, natural full attack coroutine, dual wield, flurry, off-hand | `NPCPerformAttack` (NPCTurns.cs:1118) -> one `FullAttack`, adaptive `FullAttack` per step, or `Attack` | `Attack`/`FullAttack` -> `BuildAttackBonus` -> `PerformSingleAttackWithCrit`. Post-attack hooks (ammo, thrown, Cleave, Improved Grab, charm breaks, concentration, victory) differ per orchestrator |
| Charge | `SupportActions.ExecuteCharge` | `NPCExecuteCharge` | legality, path, step AoOs, +2/-2, pounce/rake |
| Movement | `HandleMovementClick` -> `FindSafePath` -> `ExecuteMovement` (Grease hooks) | `MoveCharacterAlongComputedPath[Withdraw]` -> `ExecutePathWithMovementAoOs` | `ResolveMovementAoOsBeforeStep`, `MoveAlongPath`, 5-ft step, stand up |
| Maneuvers | `ExecuteSpecialAttack` wrapper (attack-pool cost, fear and fascination breaks) | `TryNPCSpecialAttackIfBeneficial` (always a standard action) | `ResolveManeuverInitiationAoOs` -> `CharacterController.ExecuteSpecialAttack`. Overrun never rejoins |
| Grapple | action-panel buttons -> `ExecuteGrappleAction` | `AI_GrappleRestrictedTurn` | `ResolveGrappleAction`, escape check |
| Spells | `BeginPendingSpellTargeting` -> `PerformSpellCast` (51 handlers) / `PerformAoESpellCast` / summon / held charge / scroll / wand | `TryNPCPerformSpellCast` (17 handlers; no Area, metamagic, summon, components or concentration) | `IsValidTargetForSpell`, casting Concentration helpers, ASF, `ResolveThreatenedSpellcast`, counterspell, `SpellCaster.Cast`, `IsEffectNegatedBySave`, `ApplySpellBuff` |
| Monster specials | none | breath, aura, spittle, engulf, acid spray, frightful presence (AIService, NPCTurns) | none (own save and damage code) |
| Class abilities, items | rage, bardic, turn undead, aid another, consumables, pick up/drop, equip changes | none (except charmed potion and mindless-undead re-equip) | the cores take an actor, but no AI wrapper exists |
| Stats | `CharacterCreationData` + PC ctor with race + `ApplyPendingLevelUp` | `InitializeNPCFromDefinition` (no race, `UseCreatureTypeProgression`) | `CharacterStats` derived properties, `Inventory.RecalculateStats` |
| Combat end | `TryHandleVictoryAfterEnemyDeath` (victory only) | `AreAllPCsDead` only (except NPC charge) | none central |

## 3. Scorecard

| Family | Status | Main issues |
|---|---|---|
| Attack roll and damage resolution | Shared | (CMB-043 done) |
| Single attack orchestration | Mostly shared | CMB-017, CMB-019, CMB-090, CMB-093 |
| Full attack / iteratives | Divergent | CMB-091, CMB-044, CMB-095 |
| Two-weapon, off-hand, flurry | PC only | AI-055 |
| Charge | Mostly shared (end differs) | CMB-018, CMB-013 |
| AoO (as attacker, movement, maneuver) | Shared | CMB-045 (dup helper) |
| Movement execution | Mostly shared | GRID-001, GRID-018, GRID-019 |
| 5-ft step, stand up | Shared | - |
| Crawl, drop prone | PC only | CMB-074, AI-054 |
| Turn start | Divergent | CMB-075, AI-053, CHR-069, CMB-094 |
| Turn end, round ticks, initiative | Shared | - |
| Action economy | Mostly shared | CMB-095, CMB-102 |
| Single-target spellcasting | Mostly shared core, divergent handlers | SPL-054, SPL-112, SPL-113, SPL-114, SPL-118 |
| Area spells, summons, metamagic | PC only | AI-001, SPL-091, SPL-119 |
| Held charge, scrolls, wands, potions | PC only | SPL-115, AI-009, ITM-070 |
| Maneuvers (trip, disarm, sunder, bull rush, feint) | Shared resolution, divergent cost and choices | CMB-102, CMB-098, CMB-099, CMB-104 |
| Overrun | Divergent | CMB-015 |
| Grapple | Mostly shared | CMB-103, CMB-101, CMB-097 |
| Monster specials | NPC only | CRE-039, CRE-040, AI-040, AI-041 |
| Class abilities (rage, bardic, turn undead, aid another) | PC only | AI-054, CHR-069 |
| Saves | Divergent by effect origin | SPL-018, SPL-090 |
| Conditions: application | Shared | - |
| Conditions: compulsion behaviour | Divergent | CMB-100 |
| Wards and barriers (Sanctuary, Resilient Sphere) | Divergent | CMB-093, CMB-092, AI-005 |
| Perception of unseen creatures | NPC only | CMB-094 |
| HP, dying, death | Mostly shared | CORE-015, AI-053 |
| Damage mitigation | Divergent by origin | SPL-004, AI-040 |
| Stat derivation | Divergent by origin | CRE-004, CHR-071, CHR-072, CRE-038, CHR-070, CRE-041, CHR-067, CRE-002, ITM-004 |
| Equipment-derived stats | Shared | - |
| Ammo, thrown weapons | Divergent | CMB-019, CMB-096 |
| Equipment changes, pick up | Divergent / PC only | ITM-069, AI-054 |
| Combat end | Divergent | CORE-011, CORE-037, CORE-014 |

## 4. Confirmed divergences (by impact)

| ID | Mechanic | What differs | Favours | Impact | Evidence |
|---|---|---|---|---|---|
| CHR-071 (new) | Hit Dice | PC `Stats.HitDice` is frozen at creation level; HD-limited spells (Sleep, Color Spray, Daze, Cause Fear) and Bear's Endurance see creation HD. The mirror case: rules reading `Stats.Level` miscount templated undead and class-levelled monsters | NPC (mostly) | High | CharacterStats.cs:3366, 3566-3628; TeamUtility.cs:80 |
| CHR-072 (new) | Weapon/armor proficiency | NPC classes (Warrior and others) and creature types grant none: -4 plus armor ACP on attacks for about 250 Warrior-class monsters | PC | High | CharacterStats.cs:5269-5441; CharacterController.cs:4829-4830 |
| SPL-054 | Spell handlers | NPC path runs 17 of 51 handlers (Ghoul Touch, Sound Burst, Searing Light, Prayer...) | depends | High | GameManager.SpellCasting.cs:2129-2346 vs NPCTurns.cs:938-1006 |
| AI-001 | Area spells, metamagic | NPCs cannot cast any Area spell or apply metamagic | PC | High | NPCTurns.cs:820, 835, 911 |
| CRE-004 | BAB, base saves | NPCs use creature-type progression over all HD | depends | High | NPCSetup.cs:451-487 |
| CRE-002 | Alignment | NPCs spawn with no alignment | NPC | High | NPCSetup.cs:445-756 |
| ITM-004 | NPC equipment | MainHand/OffHand/Ranged entries are silently dropped (58 hits) | PC | High | Inventory.cs:295-306 |
| CHR-067 | Skills | NPC skill totals are 0 (not the ability modifier); `RollSkillCheck` returns an automatic fail | PC | High | CharacterStats.cs:5970-6029 |
| SPL-004 | Damage mitigation | PC area spells and NPC breath/specials bypass `ApplyIncomingDamage` by different routes | depends | High | GameManager_Spells_Shared.cs:263; NPCTurns.cs:1548-1584 |
| GRID-001 | Withdraw | PC withdraw single speed, first square provokes; AI withdraw correct | NPC | High | MovementService.cs:187-195 |
| CHR-001 | Max HP | PCs get CON twice | PC | High | CharacterCreationData.cs:110; CharacterStats.cs:3444 |
| CMB-100 (new) | Frightened, Charmed | Enforced only by AI controllers; a frightened or charmed PC acts freely | PC | Med | GameManager.cs:4084; AIService.cs:84-133 |
| CMB-091 (new) | Full attack | PC iteratives use `Attack` per click, NPCs use `FullAttack`: PC Rapid Shot is inert, NPC crossbow fires every iterative from one load, adaptive AI drops Haste, ranged AoO and concentration counts differ | depends | Med | CombatFlowService.cs:611-618; NPCTurns.cs:1179, 1213, 1228 |
| CMB-090 (new) | Charm, fascination, command undead breaks | Only `CharacterController.Attack` and the PC maneuver wrapper run them | depends | Med | CharacterController.cs:5040-5046; CombatActions.cs:1917 |
| CMB-097 (new) | Improved Grab | Never triggers on PC full-attack or iterative steps (PC summons) | NPC | Med | CombatFlowService.cs:963, 1004-1017 |
| CMB-102 (new) | Maneuver action cost | PC uses the attack pool at iterative BAB and spends the full round; NPC gets one maneuver at full BAB as a standard action | depends | Med | StandardManeuvers.cs:297-302; NPCTurns.cs:394, 408-411 |
| CMB-092 (new) | Resilient Sphere | Blocks only PC attacks; an enclosed NPC loses its whole turn while an enclosed PC can self-cast | NPC/PC | Med | CombatFlowService.cs:164-177; AIService.cs:196-202 |
| CMB-094 (new) | Unseen creatures | Only AI-run creatures get Listen pinpointing; PCs auto-miss and cannot shoot into a square | NPC | Med | AIService.cs:887, 1524, 2978; CombatActions.cs:95 |
| CMB-095 (new) | Slow | No-full-round check only on the NPC full attack and the natural button; PCs full attack via progressive Attack | PC | Med | GameManager.cs:8149-8173, 8980; NPCTurns.cs:1175 |
| CMB-096 (new) | Thrown mode | PC UI global; NPCs cannot throw melee weapons and NPC range math reads it | depends | Med | GameManager.cs:11559-11587; CombatFlowService.cs:228-254 |
| GRID-018 (new) | Grease movement | Entry save and extra cost only on PC voluntary moves | NPC | Med | CombatActions.cs:1019, 1058 |
| SPL-112 (new) | Caster choices | NPC energy type forced to Fire, Fire Shield warm, Disguise Self own race, or a stale PC choice is inherited | PC | Med | GameManager.SpellCasting.cs:7008, 7061; Spells_F.cs:174 |
| SPL-114 (new) | Components, concentration | NPC cast path skips pouch check, concentration tracking and casting-while-concentrating | NPC | Med | SpellCasting.cs:1691-1712, 1956, 2350-2356 |
| CHR-070 (new) | Class choices | NPC clerics have no domains or spontaneous casting; NPC wizards cannot specialize or have familiars | PC | Med | NPCSetup.cs:445-756; SpellcastingComponent.cs:1048-1058 |
| CRE-038 (new) | Racial traits | NPC members of PC races have no RaceData (sleep immunity, stability, racial attack, familiarity) | PC | Med | NPCSetup.cs:462-477; SpellUtilities.cs:164 |
| CRE-039 (new) | Monster specials | Breath, auras, spittle, engulf, acid spray, frightful presence only on the AI turn | NPC | Med | AIService.cs:170-192, 735, 780 |
| CRE-041 (new) | NPC max HP | CON (and Toughness) re-added to MM hp totals | NPC | Med | CharacterStats.cs:3444-3445, 2145 |
| CRE-042 (new) | NPC monks | Fast movement double counted, unarmed strike as natural attacks, monk AC as natural armor | NPC | Med | NPCDatabase_M.cs:790-916 |
| ITM-069 (new) | Equipment changes | Free for PCs via the sheet; only mindless undead NPCs can re-equip (free) | PC | Med | InventoryUI.cs:576-606; UndeadMindlessAIProfile.cs:93-126 |
| CORE-037 (new) | Defeat test | PCs list plus HP>0 vs Team plus regeneration exception | NPC | Med | GameManager.cs:3388-3446 |
| CORE-011 | Victory after kill | AI-run killers (summons) never trigger victory | neither | Med | NPCTurns.cs:1307-1320 |
| CMB-075 | Turn-start order | Skipped NPC misses regeneration, Melf's, ChargePenalty expiry | depends | Med | GameManager.cs:3978-3989 vs NPCTurns.cs:46-49 |
| CMB-017 | Cleave | NPCs never cleave; PCs only on defensive or natural single attacks | PC | Med | CombatFlowService.cs:887-947 |
| CMB-018 | Charge end | Flanking only on NPC charge; bull-rush charge and Spirited Charge PC only | NPC | Med | SupportActions.cs:1674 vs 1968 |
| CMB-015 | Overrun | Separate PC and NPC resolvers (AoO, check, prone, defender choice) | depends | Med | OverrunSystem.cs:307-406; CharacterController.cs:10292 |
| CMB-019 | Ammunition | Only PC attacks check and spend ammo | NPC | Med | CombatFlowService.cs:565-847 |
| AI-055 | TWF, off-hand, flurry | No NPC executor | PC | Med | NPCTurns.cs:1213, 1286 |
| SPL-091 | Summons | NPC summon spends the slot and does nothing | PC | Med | NPCTurns.cs:913-915 |
| AI-009, AI-054 | Items, class abilities, pick up | PC-only executors | PC | Med | GameManager.cs:4370-4376, 9238-9311 |
| SPL-018 | Saves | Modifier set depends on which resolver produced the effect | depends | Med | SpellCaster.cs:971-1050; NPCTurns.cs:1556 |
| CORE-015 | Death | Post-victory rest revives dead PCs | PC | Med | GameManager.cs:1018 |
| ITM-010 | Armor speed | Baked into 9 NPC entries; never applied to PCs | PC | Med | NPCDatabase_P.cs:207 |
| SPL-113 (new) | Weapon buff spells | Fizzle on the NPC path; stale PC weapon pick can leak | PC | Low | SpellCasting.cs:7216-7246 |
| CHR-069 (new) | Inspire Courage, rage/bardic ticks | Recipients from PCs list; ticks only in StartPCTurn | depends | Low | GameManager.cs:4033-4053, 9334-9353 |
| GRID-019 (new) | Movement legality | NPC executors skip "5-ft step taken" and "grappling" checks | NPC | Low | CombatActions.cs:1108-1191 |
| CMB-093 (new) | Sanctuary | Attacker save exists only in AI target selection | PC | Low | AIService.cs:1721-1790 |
| CMB-098 (new) | RAW choices | Forced for non-controllable actors (grab, escape, push, follow, overrun avoid, disarm item) | depends | Low | GrappleSystem.cs:410, 1430; StandardManeuvers.cs:1006-1010 |
| CMB-099 (new) | Coup de grace | NPC pays cost against crit-immune target | PC | Low | StandardManeuvers.cs:541 |
| CMB-101 (new) | Casting while grappled | NPC has no path; NPC core lacks legality check | PC | Low | AIService.cs:161-165 |
| CMB-103 (new) | Grapple Move | NPC spends action, never moves | PC | Low | GrappleSystem.cs:1548-1552, 1693 |
| CMB-104 (new) | Maneuver reach | Kiter AI can trip or disarm at range | NPC | Low | AIService.cs:1010-1019 |
| CRE-040 (new) | Bombardier acid spray | Made-up save and damage | NPC | Low | NPCTurns.cs:1070-1111 |
| SPL-115 (new) | Held charge | PC only | PC | Low | SpellCasting.cs:1483-1630 |
| SPL-116 (new) | Cast-source flags | PC wand/scroll flags skip NPC ASF | NPC | Low | SpellCasting.cs:217-219 |
| SPL-117 (new) | Mirror Image swap | Controllable casters only | PC | Low | GameManager_MirrorImage.cs:413 |
| SPL-118 (new) | Invisibility break order | Before counterspell for NPC, after for PC | PC | Low | NPCTurns.cs:867-879 |
| SPL-119 (new) | AoE ally/enemy filter | Inverted for non-Player-team casters (latent) | depends | Low | AoESystem.cs:615-625 |
| ITM-070 (new) | Charmed potion | Separate potion resolution | NPC | Low | CharmedBehaviorController.cs:178-215 |
| AI-057 (new) | Reload then fire | NPC stops after reload | PC | Low | NPCTurns.cs:1142-1150 |
| AI-053 | Disabled at 0 HP | NPC loses the turn | PC | Low | AIService.cs:66 |
| AI-056 | Template smite | Two formulas | NPC | Low | TemplateSmiteSystem.cs:191 vs NPCTurns.cs:225 |
| CMB-074 | Crawl, drop prone | PC only | PC | Low | GameManager.cs:7051-7385 |
| SPL-017 | Spell range | Stats.Level vs GetCasterLevel; footprint vs anchor | depends | Low | SpellCasting.cs:1400; NPCTurns.cs:812 |

Dropped after spot-check:
- ITEM-3 / SIDE-12 ("PC ranged full attack provokes no AoO") is refuted. The Full Attack button is shown only when `UsingInnateNaturalAttacks && HasMultipleNaturalAttackTypes` (ActionButtonPanel.cs:594-613), and the defensive full-attack button is always hidden (:614). So no ranged attack reaches that coroutine. The same applies to the "PC Full Attack button skips ammo" clause of ITEM-2.
- sweepMagic SIDE-9 (Flaming Sphere tick) was refuted by its verifier.
- ITEM-12 (Holy Avenger) is dropped: `IsPlayerControlled` is a Team alias, and the code is dead (ITM-025).

## 5. Unification plan

Cheap first, all localized:
1. **Data and one-liners.** These close CRE-002, ITM-004, CMB-099, SPL-118, CRE-040, CMB-018 and part of CHR-071:
   - copy `def.CharacterAlignment` (CRE-002);
   - alias MainHand/OffHand/Ranged in `CanEquipIn` (ITM-004);
   - add a crit-immunity filter to `GetAdjacentHelplessEnemiesForCoupDeGrace` (CMB-099);
   - reorder the NPC counterspell before the invisibility break (SPL-118);
   - fix the acid spray numbers (CRE-040);
   - compute flanking in the PC charge (CMB-018);
   - update `HitDice` in `ApplyPendingLevelUp` (CHR-071, interim).
2. **More cheap fixes.** These close CHR-072, CMB-095, AI-053, CHR-069 (recipients), SPL-119 and CHR-067 (partly):
   - add NPC classes to the proficiency lists and MM gear to `ExtraWeaponProficiencies` (CHR-072);
   - set `SingleActionOnly` while Slowed (CMB-095);
   - delete the AI HP<=0 gates (AI-053);
   - Inspire Courage by `TeamUtility.IsAlly` (CHR-069);
   - `AoESystem.GetTargetsInArea` by `TeamUtility` (SPL-119);
   - untrained skill fallback to the ability modifier (CHR-067).
3. **One `BeginCombatantTurn(actor)`, called before the skip gate and the IsControllable fork.** It holds condition start-of-turn processing, ongoing damage, regeneration, cooldowns, rage and bardic ticks, perception and compulsion checks. Closes CMB-075 (ordering part), CHR-069 (ticks) and CMB-094 (perception), and is the base for CMB-100.
4. **A shared `CanDirectlyAttack(attacker, target, kind)` legality gate.** It covers sphere, Sanctuary/Hide from Undead (cached save), reach, crit immunity and Slow. Both the PC targeting highlight and the AI call it, and executors re-check it. Closes CMB-092, CMB-093, CMB-104, CMB-099 and AI-005 (partly).
5. **A shared post-attack hook `OnAttackResolved(attacker, target, results, intent)`.** It runs for every attack step from every orchestrator and covers charm, fascination and command breaks, Improved Grab, free trip, Cleave, ammo, thrown drop and redraw, concentration per hit, summon cleanup and the combat-end check. Closes CMB-090, CMB-097, CMB-017, CMB-019, CORE-011, CMB-096 (drop) and part of CMB-091.
6. **One full-attack executor that steps `FullAttack` for both sides, with an explicit attack-intent object** (melee, ranged or thrown; Rapid Shot; Haste) and legality before each step. The PC UI only picks targets between steps. Closes CMB-091, CMB-044, CMB-096, CMB-045 and AI-055 (enables TWF and flurry for AI).
7. **One charge core and one maneuver core.** The maneuver core has a `ManeuverActionCost` table and an `IManeuverDecider` (UI or AI) for every RAW choice. Closes CMB-018, CMB-013, CMB-102, CMB-098, CMB-015, CMB-086, CMB-103 and CMB-101.
8. **Movement: one step hook and one `CanStartMovement`.** The step hook (zones, Grease, cost) lives in `MoveAlongPath`; `CanStartMovement` is used by every mover; retire `FindSafePath`. Closes GRID-018, GRID-019, GRID-001 and GRID-013.
9. **A shared cast core with a `SpellCastRequest`.** It carries caster, target or cells, metamagic, caster choices, target item, cast source and spontaneous conversion. It has one handler registry, shared pre-cast legality (components, grapple, sphere, wards) and post-cast concentration registration. Then split area, summon and held-charge execution into cores with PC and AI wrappers. Closes SPL-054, SPL-112, SPL-113, SPL-114, SPL-116, SPL-115, SPL-117, SPL-017, AI-001, SPL-091 and the item-cast half of AI-009.
10. **The action catalog with shared executors** (docs/systems/tactics/README.md, section 1). Every PC-only and AI-only action becomes a catalog entry with one actor-agnostic executor. The PC action panel and the AI both call it. Closes AI-054, AI-009, CMB-074, ITM-069, ITM-070, CRE-039, AI-040, AI-041, AI-057, CMB-100 (menu restriction) and the class abilities.
11. **One creature factory and derived stats.** HD = racial HD + class levels; per-HD BAB, saves and HP; RaceData for NPCs; class-choice fields; skill ranks; proficiency from class and type data. Use it for PCs, NPCs, summons and items. Closes CRE-004, CHR-071, CHR-072, CRE-038, CHR-070, CRE-041, CHR-001, CHR-067, CRE-042, CRE-005, ITM-010 (NPC baking) and CRE-016.
12. **Shared `SaveService` and `DealDamage`, and one `EvaluateCombatEnd`.** `SaveService` covers situational bonuses, Luck, natural 1/20 and Evasion. `DealDamage` wraps `ApplyIncomingDamage`, concentration and the end check. `EvaluateCombatEnd` applies a per-Team predicate. Closes SPL-018, SPL-090, SPL-004, AI-006, CORE-030, CORE-037, CORE-014 (combat end part) and CORE-034.

After step 12, `IsControllable` should only choose between UI input and AI choice; no rule should read it.

## 6. Verified shared (brief) and gaps

**Verified shared:**
- Attacks: attack modifier assembly; hit, crit, concealment, mirror image, sneak attack, DR and energy resistance on weapon hits; flanking term; range increments; natural attack sequences; AoOs as attacker; movement AoOs; maneuver initiation AoOs; opposed checks for trip, disarm, sunder, bull rush, feint and grapple; grapple sub-actions; free trip; melee reactions; rake.
- Turns and movement: initiative (TurnService); turn-skip gate; end of turn and dying; round ticks; 5-ft step; stand up; charge legality and penalties; speed formula.
- Spells: casting while threatened; entangled and grappled casting Concentration; ASF roll; slot tables; Blink; counterspell; `SpellCaster.Cast` (SR, saves, DC); save negation; Lesser Globe; mirror image spell redirect.
- Conditions, HP and items: HP state machine; healing; regeneration amount; condition application and expiry; Confusion; poison; ability damage; negative levels; all `Inventory.RecalculateStats` outputs; reload; disarm and sunder item effects.
- Allegiance checks: these use Team correctly (threat, flanking, shooting into melee, summon barrier, willing saves).

**Not audited, or symmetric gaps:**
- Nothing was run in Play mode.
- Symmetric RAW gaps (not divergences): surprise and flat-footed, cover, line of effect for single targets, Ready, Delay and Total Defense, dominate control, unarmed-attack AoO, Tumble, massive damage, Hold per-round saves, Spiritual Weapon turns, uncanny dodge.
- `PerformSingleAttackWithCrit` classifies any weapon with a range increment as ranged even in melee (CharacterController.cs:6207). This affects daggers and spears on both sides. It is a shared bug, filed as CMB-105.
- Not traced:
  - grapple weapon attacks (CMB-087), swarms, engulf internals;
  - PerformAoESpellCast per-target resolution;
  - each spell handler's own correctness;
  - mounted combat (unreachable);
  - animal companions;
  - whether charmed or confused PCs get IsControllable prompts in maneuver flows;
  - between-battle party management (PC-only by nature);
  - InputService hotkeys and the F12 panel as PC entry points.
