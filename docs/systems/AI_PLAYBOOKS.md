> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-03.

# Enemy AI: request playbooks

Part of [AI.md](AI.md) (section 11.8). Each playbook maps a likely owner request to the code to read first, what already works, what blocks it, the issues to fix first, the rules and the presets to test with. Everything is static reading, not verified in Play mode.


Seven requests the owner is likely to make, each with the code to read first, what already works, what stands in the way, and the issues to fix before tuning behaviour. Everything here is static reading, not verified in Play mode. The general order in 13.2 (rules parity, then capability data, then behaviour) applies to all of them; most also need the comparable expected-value unit described in 13.3, because "use X when it is better than attacking" cannot be scored today.

## 11.8.1 "Make goblins flank and use Aid Another"

- **Start here.** `AIService.EvaluateMovementOptions` (2059; flank term near 2100) and `CombatUtils.IsAttackerFlankingFromPosition` (Combat/Utilities/CombatUtils.cs:106); `AIService.ExecuteAggressiveMeleeTurn` step 3, the only place a goblin moves (5.1). For Aid Another: `GameManager.CanUseAidAnother` (Combat/Maneuvers/SupportActions.cs:326), `ExecuteAidAnother` (855), `ExecuteAidAnotherWakeSleepingAlly` (814).
- **What exists.**
  - `goblin` (NPCDatabase_G.cs) is archetype Humanoid → `HumanoidAIProfile` (`SeekFlanking = true`) on AggressiveMelee. The melee approach calls (AIService.cs:531, 794, 1188) pass the target, so a moving goblin scores +3 for a cell that flanks it, but only when an ally already stands on the opposite side.
  - Flanking is applied when NPCs attack: `NPCPerformAttack` (NPCTurns.cs:584-587) and `NPCExecuteCharge` (SupportActions.cs:1982) compute `IsAttackerFlanking` and pass the +2; sneak attack follows from that flag.
  - Aid Another is complete for PCs: attack roll against AC 10, then +2 attack or +2 AC for one ally against one enemy, or wake an adjacent sleeping ally. Bonuses are consumed inside the shared `CharacterController.Attack` and `FullAttack` (4883, 5181, 5291) and expired by `ExpireAidBonusesAtTurnStart`, which already runs for NPCs (NPCTurns.cs:46). An NPC that receives an aid bonus would therefore already benefit.
  - `ManeuverPreferences.UseAidAnother` exists and nothing reads it (AI-026).
- **What blocks it.**
  - A goblin already in reach never moves, so it never steps or repositions into a flank (5.1); a goblin at charge distance charges straight in (charge is checked before movement). There is no shared target, reserved square or awareness of allies acting later in the round (AI-010; 13.3 group tactics).
  - `HumanoidAIProfile` sets `AttemptTrip` and `AttemptDisarm`, so goblins in reach of a standing PC trip instead of attacking (AI-035). Fix or gate this first, or flanking goblins will not swing.
  - NPC movement provokes AoOs like PC movement, so walking around a PC to its far side costs AoOs. The flank term must be weighed against them; the path scorer today applies only -1000 (profiles that avoid AoOs) or -2 per provoked AoO.
  - Flanking is worth the RAW +2 on melee attacks (plus sneak attack for rogues); the Flanked condition is a display tag with no AC effect.
  - `ExecuteAidAnother` cannot be called by an NPC (AI-054) and `AddAidBonus` is private. Split out a rules core that takes (aider, ally, enemy, type) and returns the result, keep the PC wrapper for the menus, and add a `*ForAI` wrapper (11.3).
- **Decision to build.** Aid Another pays when the aider's own expected damage against the enemy is lower than the ally's gain from +2 attack (or the expected damage prevented by +2 AC), and both threaten the same enemy. Waking an adjacent sleeping ally is a simpler first rule and matters against PC Sleep spells.
- **Fix first.** AI-035; then AI-054 for Aid Another.
- **Rules.** PHB ch.8, Combat Modifiers (Flanking) and Special Attacks (Aid Another).
- **Test with.** `test_2_goblins`, `goblin_raiders`, `sleep_spell_test` (includes the wake-ally flow), or the Custom Encounter Builder with three or more goblins against one PC.

## 11.8.2 "Make enemy wizards cast Fireball and Haste sensibly"

- **Start here.** `AIService.TryExecuteSpellcastAction` (2340) → `SelectSpell` → `SpellcasterAIProfile.ScoreSpell` (AI/Profiles/SpellcasterAIProfile.cs:49) and `EvaluateAOECast` (139) → `AISpellcastingStrategist.SelectBestSpellTarget` (226) → `GameManager.TryNPCPerformSpellCast` (NPCTurns.cs:758). PC area code to reuse: `GameManager.PerformAoESpellCast` (Spell/Resolution/GameManager.SpellCasting.cs:3567), `AoESystem.GetBurstCells`, `GetConeCells`, `GetLineCellsToTarget`, `GetTargetsInArea`, `FilterCellsByLineOfEffect` (Spell/AreaEffects/AoESystem.cs), and `TryResolveScaledAoEDamageSpell` (GameManager_Spells_Shared.cs:180), which resolves Fireball's damage.
- **What exists.**
  - Data: Fireball is `TargetType.Area`, Burst, radius 4 squares, Long range, Reflex half, SR (SpellDatabase_F.cs). Haste is `SingleAlly`, Close range, Buff (SpellDatabase_H.cs:420); PHB ch.11 allows one creature per level, the data allows one.
  - Scoring already values areas: `EvaluateAOECast` counts enemies and allies in a burst centred on the primary target (+3 per enemy, -10 per ally casualty, -1000 unsafe; 7.3). The strategist has a Haste-per-frontliner combo term and a pre-buff bonus in rounds 1-2; `SelectBestAllyTarget` picks a frontliner, or the caster when no ally is in range.
  - Haste resolves on the NPC path: `TryNPCPerformSpellCast` → `ApplySpellBuff` (NPCTurns.cs:953) reaches the Haste branch (`ApplyHasteBuff`, GameManager.SpellCasting.cs:6986), which sits before the generic block described in SPL-037; the extra attack is read in `CharacterController.FullAttack` (5269). Inferred.
  - The only aim-point search is the breath search, `DragonAIProfile.FindBestBreathPosition` (8.2).
- **Who could cast them today.**
  - No wizard NPC: wizard slots stop at 2nd level (`SpellcastingComponent.GetWizardSlotsForLevel`, SPL-041). The lich lists Fireball and Haste (NPCDatabase_L.cs:216-219) but prepares nothing (SPL-015). DMG class-levelled wizards have no list (ENC-021), Adepts cannot cast (CHR-024), and the rakshasa's sorcerer spells are text (CRE-017).
  - Adult red, gold, silver, bronze, copper and brass dragons cast as 7th-level sorcerers, and `DragonData.GetSpellsForCasterLevel` takes the first 16 entries of the thematic list (CRE-016). For the red dragon that includes `dispel_magic`, `haste` and `fireball`, so `dragon_red_adult` should know Fireball (refused at cast, AI-001), Haste (castable; alone, it hastes itself) and Dispel Magic (no effect, SPL-091). Inferred, not verified in Play mode.
- **What blocks it.**
  1. Spell supply: SPL-041, SPL-015, ENC-021, CHR-024.
  2. Routing: a profiled wizard casts only on the RangedKiter routine (Evoker, Lich, Ranged profiles or `RangedKiter` behaviour). `SpellcasterAIProfile` is Melee and never casts (AI-002, AI-038).
  3. Area casting: `TryNPCPerformSpellCast` rejects `TargetType.Area` while `SelectSpell` still picks area spells, so a caster that prefers Fireball does nothing better (AI-001, 7.2). `PerformAoESpellCast` cannot be called by an NPC (AI-054); build an NPC area path that takes caster, spell and cells explicitly and shares the per-target resolution.
  4. Placement: `EvaluateAOECast` only centres the burst on the chosen target. There is no search over aim points (generalise the breath search) and no line-of-effect filter in the AI estimate.
  5. Rules gaps on the NPC path: SPL-038 (metamagic ignored by the Fireball resolver), SPL-027 (area buffs without a handler skip saves and SR), SPL-024 and SPL-025 (Haste gives double bonuses and overwrites Mage Armor), AI-046 (SR estimate), SPL-092 (Silence ignored).
  6. "Sensibly": there is no score floor and no comparison with a weapon attack (7.2), school priority ×10 dominates (AI-022), and the multi-round plan store is never filled (AI-011). Haste before Fireball is encouraged only by the round 1-2 pre-buff bonus. A 3rd-level slot also competes with the fixed "conserve 3rd+ level" penalty.
- **Fix first.** AI-001 (with AI-054), SPL-041 and SPL-015, AI-002; then SPL-024. An NPC area path must keep the AoO and Concentration rolls the single-target NPC path now makes (`ResolveNPCSpellcastProvocation`).
- **Rules.** PHB ch.11 (Fireball, Haste); PHB ch.10 (aiming a spell, area, spell resistance); PHB ch.8 (casting provokes, Concentration).
- **Test with.** `dragon_red_adult` (7th-level sorcerer), `npc_magic_missile_test` for the cast path. No preset has an enemy wizard able to cast 3rd-level spells.

## 11.8.3 "Make enemies drink potions when low"

- **Start here.** `GameManager.ApplyConsumableEffectAndConsume` (_Core/GameManager.cs:5475), the effect core with no PC-turn dependency found; the PC wrapper `TryUseConsumableFromInventory` (4333) and its AoO coroutine `ResolveConsumableAoOsAndApply` (4526); AI/AIConsumableManager.cs. The call site would be a new step near the top of `ExecuteNPCTurn` (after the condition gates) so every routine gets it.
- **What exists.**
  - Seven definitions carry potions in `BackpackItemIds`: `hobgoblin_sergeant`, `human_monk_3`, `human_monk_5`, `human_monk_7`, `human_paladin_3`, `human_paladin_5`, `human_paladin_7`. `InitializeNPCFromDefinition` adds them to the inventory (NPCSetup.cs:670-675). DMG random-encounter spawns carry none.
  - Potions of Cure Light Wounds are spell potions (`RegisterSpellPotion`, Equipment/Items/ItemDatabase.cs:1241) resolved by `ApplyConsumableEffectAndConsume` through the spell-consumable branch.
  - `AIConsumableManager` has a healing threshold (`HealingPotionThreshold` 0.4), potion, scroll and wand name lists and pickers, but only tests attach it and it keeps its own lists instead of reading the inventory (AI-009).
  - The only live NPC consumable use is a charmed NPC feeding a healing potion to its charmer (`CharmedBehaviorController.TryUseHealingConsumableOnCaster`, about line 178): it removes the item and spends a standard action but resolves no AoO (AI-042 covers the charm behaviour).
- **What blocks it.**
  - The PC wrapper refuses any actor that is not `ActivePC` and ends the PC turn (AI-054). NPC use needs its own wrapper around `ApplyConsumableEffectAndConsume`, resolving provoked AoOs with `ThreatSystem.ExecuteAoO` as `ResolveRangedAttackAoOForNPCAttackIfProvoked` (NPCTurns.cs:406) does.
  - Item use costs a full-round action for everyone (ITM-005). RAW, retrieving a stored item is a move action and drinking is a standard action, so an NPC could retrieve and drink in one turn but not also move.
  - Nothing decides between drinking, withdrawing and attacking; `FleeHealthThreshold` is unread (AI-010).
  - Wands and scrolls also need targets and Use Magic Device checks for non-casters (`WandValidator`), so start with self-targeted potions.
- **Fix first.** AI-009 (wire `AIConsumableManager` to the real inventory or delete it), AI-054, ITM-005.
- **Rules.** PHB ch.8, Actions in Combat (drink a potion: standard action, provokes; retrieve a stored item: move action, provokes); DMG ch.7 (Potions).
- **Test with.** `goblin_raiders` (hobgoblin_sergeant), or the Custom Encounter Builder with `human_monk_3` or `human_paladin_3`.

## 11.8.4 "Make monsters trip and disarm"

- **Start here.** `AIProfile.GetPreferredManeuver` (AI/AIProfile.cs:300-319) and `ManeuverPreferences` (AI/AIBehaviorData.cs); `AIService.ShouldUseManeuver` (2213) and `TryExecutePreferredManeuver`; `GameManager.TryNPCSpecialAttackIfBeneficial` (NPCTurns.cs:289, legacy chooser plus executor); `CharacterController.ResolveTrip` (10503) and `ResolveDisarm` (10553). Free trips: `GameManager.TryResolveFreeTripOnHit` and `ThreatSystem.ExecuteAoO`, both through `CharacterController.ResolveFreeTripAttempt` (no touch attack, no AoO).
- **What exists.**
  - Deliberate trips and disarms by Humanoid, Berserk, Grappler and null-profile NPCs (6.2, 6.4). The maneuver replaces the attack, spends a standard action, and triggers melee reactions.
  - Free trip on any hit, including AoOs, for `HasTripAttack` creatures (wolves; lycanthropes copy it from the animal in `LycanthropeTemplate`); the Animal Tripper specialty charges to fish for it.
  - Disarm moves the weapon to the attacker's free hand or the ground (`DropItemToGround`), and a failed disarm gives the defender one counter-disarm (none against Improved Disarm).
- **What blocks it.**
  - AI-035: no odds or value test; profiles trip anything standing. A chooser needs P(success) from the opposed terms in `ResolveTrip` and a value for prone (+4 to hit for adjacent allies, -4 on the target's melee attacks, standing up provokes).
  - Trip and disarm provoke from the target unless the attacker has Improved Trip or Improved Disarm, and bull rush provokes from every threatening enemy (Improved Bull Rush spares only the defender); NPCs and PCs share `ResolveManeuverInitiationAoOs`. A risk model must now price that AoO.
  - A tripped NPC stands up at the start of its next turn (move action, provokes), so a trip costs it its full attack and its move; confused NPCs stay prone (CMB-074).
  - CMB-079: no counter-trip, no Improved Trip follow-up attack, no size limit.
  - The opposed-check math now follows PHB p.154-158 (CMB-014 fixed): trip is a melee touch attack, then STR + special size [+4 Improved Trip] vs the better of STR/DEX + special size + stability; ties go to the higher modifier, then a reroll. The terms are exposed as `GetTripAttackerCheckModifier` and `GetTripOrOverrunDefenderCheckModifier`, so a chooser can compute P(success). Quadrupeds still lack their +4 stability (CMB-085).
  - NPCs cannot trip or disarm as one attack of a full attack (the PC path uses a shared pool, `TryConsumeTripAttackAction`). NPC disarmers never pick up the dropped weapon and disarmed NPCs never re-arm (5.9).
  - Two choosers (AI-025); Sunder, BullRush and Overrun flags are never set and their target checks are placeholders (AI-014).
- **Fix first.** AI-035; then CMB-079.
- **Rules.** PHB ch.8, Special Attacks (Trip, Disarm); PHB ch.5 (Improved Trip, Improved Disarm); MM entries for free trips on a hit (for example the wolf).
- **Test with.** `wolf_pack`, `beast_arena` (Tripper animals), `goblin_raiders` and `test_2_goblins` (Humanoid trips and disarms).

## 11.8.5 "Add morale and fleeing"

- **Start here.** A gate in `AIService.ExecuteNPCTurn` after the condition controllers (around line 113) so it covers every routine. Movement to reuse: `FrightenedBehaviorController.FindBestFleeCell` (Combat/Behaviors/FrightenedBehaviorController.cs), or `EvaluateWithdrawRetreatDestination` (2143) with the withdraw move (`MoveCharacterAlongComputedPathWithdraw`, CombatActions.cs:914). Data to read: `AIProfile.Aggression` (AI/AIProfile.cs:28), `SpellcasterAIProfile.FleeHealthThreshold` (0.25; Lich 0), `VampireAIProfile.GaseousFormFleeThreshold`, `CharacterStats.IsMindless`, INT and `CreatureType`.
- **What exists.** Hard-coded retreats only (8.9). `SelectBestAction` already returns `Retreat` below 30% HP when called with `preferAggression: false` (only DefensiveMelee does) and the NPC has a move action; DefensiveMelee ignores it and withdraws by its own 30% test (5.4). This is the natural hook for a morale policy (13.3), so keep the `Retreat` result when fixing AI-027.
- **What blocks it.**
  - Nowhere to go. The map has no exit, so a fleeing creature stays in play. To remove one, follow `GameManager.DespawnSummonWithEffect` (GameManager.SpellCasting.cs): it clears occupancy, removes the creature from `NPCs` and `_npcAIBehaviors` together (AI-015) and calls `TurnService.RemoveFromInitiative`.
  - Victory and XP count only enemies at HP ≤ 0 (`AreAllNPCsDead`, `RegisterDefeatedEnemyForXP`; [ENCOUNTERS.md 6.2](ENCOUNTERS.md#62-xp)). A fled, surrendered, charmed or cornered frightened enemy keeps combat going and gives no XP, while DMG XP is for overcoming a challenge (CHR-004). Morale needs a "defeated" state that both checks honour, and a decision about the treasure of fled creatures.
  - The existing fear path is broken: Panicked creatures lose their turn instead of fleeing (CMB-075), and Frightened flight is 1× speed (AI-044); it does provoke AoOs like any move.
  - No surrender or parley state. No effect changes a creature's team (the only `SetTeam` caller is the Lion's Shield), and team checks are inconsistent (AI-016).
  - The core books give no morale roll, so the trigger is a house rule to agree with the owner (for example a Will save when reduced below half HP or when the leader falls; never for mindless creatures; lich 0 as already set).
- **Fix first.** CMB-075, AI-044, AI-010, AI-027 (by acting on `Retreat`, not by removing it); agree the defeated-state rule with [ENCOUNTERS.md](ENCOUNTERS.md) and [PARTY_MANAGEMENT.md](PARTY_MANAGEMENT.md) before coding.
- **Rules.** PHB ch.8 and DMG ch.8, Condition Summary (Frightened, Panicked, Cowering); MM Introduction (Intelligence, mindless creatures).
- **Test with.** Any preset; `tier3_hell_hound_pack` and the dragon presets apply frightful presence (Panicked targets).

## 11.8.6 "Make NPC clerics heal and channel"

In 3.5, "channelling" is turn or rebuke undead (PHB ch.8) plus the cleric's spontaneous conversion of prepared spells into cure or inflict spells (PHB ch.3, Cleric).

- **Start here.** The Healer branch of `ExecuteNPCTurn` (197-257), `HealerAIProfile.DetermineActionPriority` (AI/Profiles/HealerAIProfile.cs:84), `AISpellcastingStrategist.SelectBestAllyTarget`. Turning: Combat/Special/TurnUndeadSystem.cs (`CanUseTurnUndead` 327, target gathering about 360-415, `ExecuteTurnUndead` 451, `TryConsumeTurnUndeadResources` 560). Spontaneous casting: `SpellcastingComponent.SpontaneousCastFromSlot` (2236) and `SpontaneousCastFromSpecificSpell` (2280); `ClericClass` sets the `SpontaneousCasting` type.
- **What exists.**
  - The Healer profile and branch (5.6): heal any ally below 70% HP, then buff, then offensive spells or weapons. Only DMG Cleric spawns get it (`DungeonEncounterSpawner.UpdateAIForClass`); `human_cleric` is a Cleric on Humanoid/DefensiveMelee with no spells.
  - Touch healing resolves on the NPC path against adjacent targets (7.6); charmed NPCs heal their charmer (8.1).
  - PC turning is complete enough to reuse: attempts per day, turning check and damage pool, the Turned condition and tracker. `CanUseTurnUndead` works for any actor, and target gathering uses `TeamUtility.IsEnemy`, so an NPC cleric would turn party-side undead.
- **What blocks it.**
  - No NPC cleric has spells: ENC-021 (DMG spawns), CHR-024 (Adepts), SPL-041 and CHR-023 (cleric known and domain spells stop at 2nd level); prepared lists are assigned by slot position (7.1).
  - Healing reach: touch spells need adjacency, the healer never moves to touch, heals itself instead, and can aim offensive spells at its wounded ally (AI-047); `Team ==` checks (AI-016).
  - Spontaneous conversion exists only on the PC cast path (GameManager.SpellCasting.cs:193) and the AI's candidate list (`GetCastablePreparedSpells`) contains only prepared spells, so the AI cannot trade a prepared spell for a cure.
  - Turning is a PC menu flow (AI-054). Evil clerics turn instead of rebuke; there is no command, bolster or turn resistance (CMB-026); the turning level uses total character level (CMB-016); turned NPC undead lose their turn instead of fleeing (CMB-075). Cure and Inflict ignore creature type (SPL-005), so an evil cleric cannot heal undead allies with Inflict.
  - For enemy clerics the useful cases are mostly the evil ones: rebuking or commanding the party's undead (rare) and bolstering their own undead allies, none of which exists.
- **Fix first.** ENC-021 (give DMG Cleric spawns spell lists), SPL-041 and CHR-023, AI-047, AI-016; then AI-054 for turning and CMB-026 for rebuke.
- **Rules.** PHB ch.3 (Cleric: spontaneous casting, turn or rebuke undead); PHB ch.8 (Turn or Rebuke Undead); PHB ch.11 (cure and inflict spells against undead).
- **Test with.** DMG spawns with Cleric levels (Healer profile), `charm_person_test` (charmed healing), `undead_showcase` (turnable undead for a PC comparison).

## 11.8.7 "Make enemies use class abilities (Smite Evil, Rage, Turn/Rebuke Undead, Bardic Music, Flurry)"

- **Start here.** The PC handlers and the rules cores behind them: `GameManager.OnRageButtonPressed` (_Core/GameManager.cs:9171) → `CharacterController.ActivateRage` (Character/Controller/CharacterController.cs:12099) → `CharacterStats.ActivateRage`, with `TickRage` called only in `StartPCTurn` (GameManager.cs:4036); `OnFlurryOfBlowsButtonPressed` (9145) → `CombatFlowService.PerformFlurryOfBlows` (Services/CombatFlowService.cs:1089) → `CharacterController.FlurryOfBlows` (11987); `OnBardicMusicButtonPressed` (9201), `ApplyInspireCourageToParty` (9267), `TickBardicMusic` (9294, called from `StartPCTurn`); `TurnUndeadSystem.CanUseTurnUndead` (Combat/Special/TurnUndeadSystem.cs:327) and `ExecuteTurnUndead` (451); `GameManager.ActivateDestructionSmite` and `GetDestructionSmiteAttackBonus`/`GetDestructionSmiteDamageBonus`/`ConsumeDestructionSmite` (Spell/Domain/GameManager_DomainPowers.cs:237, 440-465); template smite in Combat/Special/TemplateSmiteSystem.cs and the summon version `TryExecuteSummonSmiteAttack` (NPCTurns.cs:192). The call site would be a class-ability step in the melee routines (before the charge test for Rage, in place of `NPCPerformAttackForAI` for Flurry and smite), or a shared step near the top of `ExecuteNPCTurn` for Rage.
- **What exists.**
  - NPCs with these classes: `human_paladin` (Paladin 5) and `human_paladin_3`/`_5`/`_7`; `human_monk_3`/`_5`/`_7`; `orc_berserker` (Barbarian 3, Berserk) and `test_barbarian_gust` (Barbarian 2, Berserk); `human_cleric` (Cleric 5, Humanoid on DefensiveMelee, no spells) and `evil_acolyte_test` (Cleric 3, Spellcaster on RangedKiter). Paladins and monks use the Humanoid profile on AggressiveMelee. No database NPC is a Bard. DMG spawns can add Barbarian (Berserk), Cleric (Healer), Bard (Spellcaster), Paladin or Monk (Humanoid) levels (`DungeonEncounterSpawner.UpdateAIForClass`).
  - Class data is populated for NPCs: `InitializeNPCFromDefinition` builds `CharacterStats` with `characterClass: def.CharacterClass` (NPCSetup.cs:462), whose constructor fills `ClassLevels` and runs `InitFeats`, so `IsBarbarian`, `IsMonk`, `IsCleric`, `IsPaladin`, `MaxRagesPerDay`, `GetFlurryOfBlowsBonuses` and the turning attempt count all work for NPCs. What is missing: `ChosenDomains` (no `NPCDefinition` field, so NPC clerics have no domain powers or domain spells), alignment (CRE-002), and the paladin, ranger, druid and bard feature objects, which are not assigned for anyone (CHR-020). The paladin's "Smite Evil 1/day" is a `SpecialAbilities` string.
  - Rules cores that take an explicit actor and have no PC-turn coupling: `CharacterController.ActivateRage` (no action spent; PHB: free action); `CharacterController.FlurryOfBlows(target, ...)`; `CanUseTurnUndead`; and the Destruction smite. That smite is modelled as a flag: `ActivateDestructionSmite(cleric)` checks `CanActivateDomainPower` (cleric, has the domain, 1/day), sets `DestructionSmiteActive` and spends no action; `PerformSingleAttackWithCrit` adds +4 to the next attack roll and clears the flag, so it works on any attack path including `NPCPerformAttack`. Its damage bonus is never added (CHR-005).
  - Per-day counters (`RagesUsedToday`, `TurnUndeadAttemptsUsedToday`, `DestructionDomainUsesToday`, bardic music uses) start at 0 on every spawn, and the party's are reset after every victory (CORE-015). Treat every per-day ability as per encounter: use it early rather than conserving it.
- **What blocks it.**
  - Paladin Smite Evil does not exist as a mechanic for PCs or NPCs (CHR-020); the Smite button runs template smite only and is hidden for characters without it. Build one rules core (declare, +CHA to attack, +paladin level to damage, uses per day) for both sides; the template smite executor `ExecuteTemplateSmiteAttack` is PC-coupled (it calls `ShowActionChoices` and `Combat_StartAfterAttackDelay`) and puts its bonuses in the Morale fields that weapon damage never reads (CMB-003).
  - Alignment: spawned NPCs are `Alignment.None` (CRE-002). An enemy paladin smiting the party would trigger only against evil PCs or evil party allies, a PC paladin's or templated ally's smite never triggers against ordinary monsters, and an NPC cleric cannot be told to rebuke rather than turn. Fix CRE-002 before any alignment-gated ability.
  - PC-turn coupling (AI-054): `PerformFlurryOfBlows` ends with `Combat_StartDelayedEndActivePCTurn`; `ExecuteTurnUndead` is private and calls `ShowActionChoices`; the Rage and Bardic Music handlers read `ActivePC`. Add `*ForAI` wrappers around the cores (11.3) and replicate the per-hit work that `NPCPerformAttack` does (melee reactions, Improved Grab, kill cleanup, victory check, CORE-011).
  - Durations tick only on PC turns: `TickRage` and `TickBardicMusic` run in `StartPCTurn`, so an NPC rage would never end (and never fatigue) and an NPC performance would never lapse. Tick them in `BeginNPCTurnForAI`.
  - Bardic music is party-only: `ApplyInspireCourageToParty` and its removal iterate `PCs`, so an NPC bard would inspire the party. Only Inspire Courage exists (CHR-026), and Morale bonuses do not reach weapon damage (CMB-003).
  - Turning: no rebuke, command or bolster and no turn resistance (CMB-026); the turning level uses total character level (CMB-016); turned NPC undead lose their turn instead of fleeing (CMB-075). See 11.8.6.
  - Flurry: the penalties and attack count are fixed at low-level values (CHR-003). Monk NPCs on the Humanoid profile trip instead of attacking (AI-035), so a flurry step must come before the maneuver test.
  - Rage: `HumanoidAIProfile`/`BerserkAIProfile` have no notion of when to rage; with per-encounter uses the simple rule is to rage on the first turn an enemy is within charge range.
- **Fix first.** CHR-020 (a real Smite Evil core), CRE-002, AI-054; then CHR-005, CHR-003, CMB-003 and CMB-026; tick rage and bardic music for NPCs.
- **Rules.** PHB ch.3 (Barbarian: rage is a free action on the barbarian's turn; Monk: flurry of blows is a full attack; Paladin: smite evil; Bard: bardic music, Inspire Courage is a standard action to start; Cleric: turn or rebuke undead, Destruction domain smite); PHB ch.8 (Turn or Rebuke Undead is a standard action).
- **Test with.** `fiendish_template_test` (an enemy `human_paladin` scripted Lawful Good and `human_cleric` Neutral Good against Neutral Evil fiendish allies, so Smite Evil has legal targets); `shield_bash_test` and `charm_person_test` (`orc_berserker`); the Custom Encounter Builder with `human_monk_3` or `human_paladin_5`; DMG spawns with Bard or Cleric levels.
