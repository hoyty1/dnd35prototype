> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-03.

# Party management between battles

This doc covers what happens between two fights: loot, treasure, XP, level-up, rest and recovery, the pre-combat hub (store, stash, spell preparation, crafting), and what state carries from one encounter to the next. Each area is compared with the PHB and DMG rules, because the project goal is a party-management simulation that follows those rules: random encounters by DMG rules, with resting, healing, advancement, treasure and shopping by PHB/DMG rules in between.

Read [ARCHITECTURE.md](../ARCHITECTURE.md) (runtime loop) first. Item and store internals are in [items-and-economy.md](../architecture/items-and-economy.md), and XP and level-up internals in [characters-and-creatures.md](../architecture/characters-and-creatures.md). Encounter generation is covered in [ENCOUNTERS.md](ENCOUNTERS.md). Paths are relative to `Assets/Scripts/`. `GameManager.cs` means `_Core/GameManager.cs`. Line numbers are as of the commit above, so grep the member name first. Everything here was read from code and is not verified in Play mode unless stated otherwise.

Rules references use book and chapter: PHB ch.3 (Classes), ch.4 (Skills), ch.5 (Feats), ch.7 (Equipment), ch.8 (Combat), ch.9 (Adventuring), ch.10 (Magic), ch.11 (Spells); DMG ch.2 (Using the Rules), ch.3 (Adventures), ch.4 (Nonplayer Characters), ch.5 (Campaigns), ch.6 (Character Classes), ch.7 (Magic Items), ch.8 (Glossary).

## 1. The loop today

There is no campaign layer. There is no time, place, calendar or travel. The game is one endless sequence: fight, loot, XP, level-ups, an automatic full heal and refresh, pick the next fight, prepare, fight.

```
victory (HandleCombatVictoryDetected)
  -> BeginPostCombatLootCollection    treasure by EL, coins -> party gold, loot window -> PartyStash
  -> ShowPostCombatXPFlow             ExperienceCalculator -> CombatEndXPUI
  -> CheckAndShowLevelUps             LevelUpUI per PC with a pending level, one after another
  -> ContinueToRestAndNextCombat
       RestorePartyAfterCombat        unconditional full rest
       ReturnToEncounterSelection     reset the battlefield
  -> PromptEncounterSelection         presets / random generator / DMG tables / custom builder
  -> OpenPreCombatHubPhase            Store | Inventory/Stash | Spell Prep | Crafting | Start | Back
  -> StartEncounterFromPreCombat      warns about unprepared casters
  -> ForceStartEncounterFromPreCombat lock the stash -> StartCombat
```

| # | Step | What happens | Entry point |
|---|---|---|---|
| 0 | First entry | Character creation ends in `SetupCreatedCharacters` (PCs placed at (3,6), (3,9), (3,12), (3,15), class starting gear). New characters default to `CharacterCreationData.TargetLevel = 3`, so `ProcessCreationLevelUpsThenPromptEncounterSelection` runs `LevelUpUI` before the first fight. | `GameManager.cs:1863`, `:753` |
| 1 | Victory | One of the victory call sites runs `HandleCombatVictoryDetected`. That sets `CombatOver`, snapshots the defeated enemies for XP, and calls `BeginPostCombatLootCollection`. Defeat has no equivalent path and soft-locks the session (CORE-001). | `GameManager.cs:3581` |
| 2 | Treasure | `GeneratePostCombatTreasure` computes EL with `EncounterService.CalculateEncounterLevel` and calls `TreasureGenerator.Generate(el)` (DMG ch.3 treasure tables). Coins go straight into `EconomyService.PartyGold` through `AddGold`. `TreasureItemConverter.ConvertAll` makes placeholder items and runs Appraise checks on gems and art with the party's best modifier. | `_Core/GameManager.LootCollection.cs:25-110`; `_Core/GameManager.TreasureGeneration.cs` |
| 3 | Loot window | `LootCollectionUI` lists the dead enemies' equipment and inventory, ground items and the treasure items. Each item taken goes to `PartyStash` (`TryTransferLootItemInstanceToStash`); there is no option to hand it to a character. Closing the window starts the XP flow. Its exit button calls `ExitCombatLoopToMenu`, which quits the game (CORE-006). | `GameManager.LootCollection.cs:152-172, 666` |
| 4 | XP | `ShowPostCombatXPFlow` records loop statistics (`RegisterCombatLoopCompletion`, `GameManager.cs:1228`). It builds the party from every PC whose GameObject is active, so dead PCs are included, then calls `ExperienceCalculator.CalculateXPForCombat` and shows `CombatEndXPUI`. `CharacterStats.AddExperience` applies the multiclass penalty, raises `Level` at once and adds to `PendingLevelUps`. | `GameManager.LootCollection.cs:175-242`; `Character/Stats/CharacterStats.cs:3507` |
| 5 | Level-up | `CheckAndShowLevelUps` runs `LevelUpUI.ShowForCharacter` for each PC that levelled up. Each pending level calls `ApplyPendingLevelUp(class)` (class level, HP roll, skill flags, XP penalty, slot refresh), then the ability increase on every 4th level, then `CharacterCreationManager.StartLevelUpFlow` (feats, skills, wizard choices, domains, spells). | `GameManager.LootCollection.cs:296-373`; `CharacterStats.cs:3554` |
| 6 | Rest | `RestorePartyAfterCombat` clears area, wind, summon, Mirror Image, melee-reaction and curse state. For each active PC it removes all effects and conditions, restores every spell slot, resets the daily counters listed in section 2.2, clears poisons, sets HP to `TotalMaxHP` and moves the PC to its start cell. It then calls `RingActivationManager.OnRest` and `WondrousItemActivation.OnRest`. It runs after every victory, with no choice and no time cost (CORE-015). | `GameManager.cs:868-1043` |
| 7 | Reset | `ReturnToEncounterSelection` unlocks the stash and calls `ResetCombatStateForNextEncounter`, which stops every coroutine (CORE-013), clears the battlefield, deactivates the NPC slots and resets `TurnService`. The `OnCombatEnded` cleanup does not run (CORE-002). | `GameManager.cs:1253`, `:1076` |
| 8 | Encounter selection | `PromptEncounterSelection` seeds the stash on first use and opens `EncounterSelectionUI` with the party's levels. A preset leads to `ApplyEncounterPreset`, a generated or custom encounter to `ApplyRandomEncounter`, and Cancel to the `goblin_raiders` preset. The player chooses each fight; nothing is rolled for them. A test preset replaces the created party for the rest of the session (CORE-004). | `GameManager.cs:785, 1690, 1769` |
| 9 | Pre-combat hub | `OpenPreCombatHubPhase` opens `PreCombatHubUI` for the living PCs, with spell-preparation status lines. Store: `OpenStoreFromPreCombat` (`StoreUI.ShowStore`). Inventory and stash: `OpenInventoryFromPreCombat` (`PreCombatInventoryUI`). Spell Prep: `OpenSpellPreparationFromPreCombat` (`SpellPreparationUI`). Crafting: `OpenCraftingWorkshopFromPreCombat`, which opens for the first PC with an item creation feat, or silently does nothing (UI-008). Back returns to encounter selection. | `GameManager.cs:1403-1545` |
| 10 | Start | `StartEncounterFromPreCombat` shows a "Prepare Spells / Fight Anyway" dialog if a prepared caster has unprepared classes. `ForceStartEncounterFromPreCombat` closes the windows, calls `PartyStash.Lock()` and then `StartCombat`. | `GameManager.cs:1547, 1598` |

Hub actions cost nothing. The player can shop, swap gear, re-prepare spells and craft any number of times before each fight, and no game time passes.

## 2. Current behaviour vs RAW

Status values:

- **RAW**: matches the rules.
- **Approx.**: right idea, simplified.
- **Deviates**: implemented, but different from the rules.
- **Missing**: nothing in code.
- **Data only**: data or helpers exist, but gameplay never uses them.

Issue IDs point into [`../issues/`](../issues/). The defects found while writing this doc are indexed in [2.8](#28-issues-filed-from-this-review).

### 2.1 Recovery: rest, healing, damage, death

| Area | RAW | Current behaviour | Status | Issues |
|---|---|---|---|---|
| Resting | PHB ch.8 and ch.10: a night's rest takes 8 hours; resting takes time and can be interrupted. | Every victory triggers `RestorePartyAfterCombat` immediately. It cannot be declined or interrupted and takes no time. There is no rest action outside this path. | Deviates | CORE-015 |
| Natural healing (HP) | PHB ch.8: a full night's rest restores 1 hp per character level, and a full day of bed rest 2 hp per level. Nonlethal damage recovers 1 hp per level per hour. The Heal skill's long-term care (PHB ch.4) doubles the rate. | HP is set to `TotalMaxHP`, nonlethal damage is cleared, and temp HP and `BonusMaxHP` are zeroed. Attrition never carries from one fight to the next. | Deviates | CORE-015 |
| Healing math in combat | PHB ch.8: healing reduces lethal and nonlethal damage by the full amount. Stabilization is a 10% chance per round. | `CharacterStats.HealDamage` heals nonlethal damage first. Stabilization is a d20 + CON check against DC 10. | Deviates | CHR-011 |
| CON changes and HP | A CON change changes max HP retroactively. | `MaxHP` is stored and does not follow CON damage, drain or increases. | Deviates | CHR-012 |
| Ability damage | DMG ch.8 (Ability Score Loss): damage returns at 1 point per day of rest. Drain is permanent until magic removes it. | The rest never heals ability damage. The natural-recovery code (`EffectService.ProcessDailyEffects` -> `CharacterController.HealAbilityDamageDaily`) runs only from `GameManager.OnNewRound` when the round number reaches a multiple of `RoundsPerDay` = 14400 (`GameManager.cs:204, 3834`), so in practice never. `CharacterStats.ApplyCompleteRest`, which would also heal 1 point, has no caller outside tests. Damage therefore persists until Heal or Wish is cast, and those can only be cast in combat. Restoration and Greater Restoration only remove negative levels here (`GameManager.SpellCasting.cs`), and Lesser Restoration has no handler. | Deviates | CORE-035 |
| Disease | DMG ch.8: a daily save, then progression or recovery. | `ActiveDiseases` survives the rest and never progresses, because daily processing never runs (see the row above). Poisons are cleared by the rest. | Deviates | CORE-035; CMB-007 (poison timing) |
| Negative levels | DMG ch.8: after 24 hours, a Fortitude save for each negative level; a failure turns it into a lost level. | Negative levels are `EnergyDrained` conditions (`CharacterStats.NegativeLevelCount`). The rest removes every condition, so all negative levels vanish without a save. This also removes the level penalty that Resurrection and Wish resurrection apply as an `EnergyDrained` condition. | Deviates | CORE-036 |
| Fatigue and exhaustion | DMG ch.8 (Condition Summary): 8 hours of complete rest removes fatigue; 1 hour of complete rest turns exhaustion into fatigue. | The rest clears the conditions and `IsFatigued`, with no time cost. `CharacterStats.ApplyCompleteRest(hours)` implements the RAW timing (1 hour turns exhausted into fatigued, 8 hours remove fatigue) but has no caller outside `FatigueExhaustionTests`. | Approx. | CORE-015, CORE-035 |
| Death | PHB ch.8: death at -10 hp. PHB ch.11: Raise Dead or Resurrection, with a level loss and a costly component. | `OnDeath` keeps the PC's GameObject active, so the next rest sets its HP to full and revives it. A dead PC also gets an XP share. Resurrection (cleric 7) has a handler (`ApplyResurrectionEffect`, `GameManager_Spells_Phase2.cs:502`) and Wish has a resurrection branch, but cleric known spells stop at level 2 (SPL-041), there is no Raise Dead spell, the 10,000 gp diamond is not checked (SPL-020), and the level loss is undone by the next rest (CORE-036). | Deviates | CORE-015, SPL-041, SPL-020 |
| Party wipe | DM-dependent. | It soft-locks. There is no defeat screen and no way back to encounter selection. | Missing | CORE-001 |

### 2.2 Daily resources

| Area | RAW | Current behaviour | Status | Issues |
|---|---|---|---|---|
| Spell slot refresh | PHB ch.10: arcane casters need 8 hours of rest, then 1 hour of study (wizard) or 15 minutes of concentration (sorcerer, bard); spells cast in the last 8 hours count against the new day's allotment. Divine casters pray for 1 hour at a fixed time of day and need no rest. | `SpellcastingComponent.RestoreAllSlots` restores all slots on every rest and keeps the prepared spells. It returns early for spontaneous and partial casters, so a multiclass prepared caster that also has a spontaneous class or a ranger/paladin class does not get its prepared slots back. Because the rest follows every victory, daily slots are effectively per fight. | Approx. | SPL-011, CORE-015 |
| Spell preparation | PHB ch.10: choose spells after resting; slots may be left open. | `SpellPreparationUI` is open in the hub before every fight. Preparation can be redone freely and costs no time. Rangers and paladins cannot prepare at all, and wizard and druid slot tables stop early. The unprepared-caster warning (`GameManager.IsPreparedCaster`) lists Bard, a spontaneous caster, in its class check, as `SpellPreparationUI.DoesPrepareSpells` does (UI-017); a single-class bard is skipped only because it has no `SpellSlots`. | Approx. | SPL-039, SPL-041, SPL-014, SPL-015, UI-017 |
| Wizard spellbook growth | PHB ch.3 and ch.10: 2 free spells per wizard level, plus copying from scrolls and books (100 gp per spell level). | Spells are chosen at level-up through `SpellSelectionUI.ShowForLevelUp` (the per-class count was not verified). There is no copying from scrolls. | Approx. / Missing | none |
| Rage, Turn Undead, Bardic Music, domain powers, template smite | PHB ch.3: uses per day. | Reset by the rest: `RagesUsedToday`, `TurnUndeadAttemptsUsedToday`, `BardBardicMusic.RefreshUses()`, the six `*DomainUsesToday` counters and `TemplateSmiteUsed` (`GameManager.cs:958-976`). The reset itself is correct, but it runs after every victory, so per-day uses are really per-fight uses. | Deviates (cadence) | CORE-015; CMB-016, CHR-026 (ability limits) |
| Stunning Fist | PHB ch.5 and ch.3: uses per day. | Stunning Fist cannot be activated at all today, because nothing outside tests sets `StunningFistActive` (CMB-022). Behind that, `StunningFistUsesRemaining` is set once (`FeatManager.cs:506`) and never reset by the rest, so uses would run out for the whole session, and the count ignores monk level. | Deviates | CMB-022, CHR-003; CHR-057 |
| Smite Evil, Lay on Hands, Wild Shape | PHB ch.3. | The data classes exist, but there is no action and no reset. | Data only | CHR-020 |
| Wand charges | DMG ch.7: charges are spent and not regained. | Charges persist between fights and never recharge. They are stored twice (on `ItemData` and on `WandData`). | RAW | ITM-034 |
| Staff charges | DMG ch.7. | Staves cannot be obtained. | Missing | ITM-024 |
| Rod uses | DMG ch.7: per-day uses (metamagic rods 3 per day). | Rods cannot be used, and their daily uses are never reset. | Data only | ITM-018 |
| Ring and wondrous daily uses | DMG ch.7. | `RingActivationManager.OnRest` and `WondrousItemActivation.OnRest` reset daily uses on each rest. `RingUseTracker` counts 7 rests as one week for weekly uses; this is the only week model in the code. Wondrous weekly and monthly resets have no caller. Ring and wondrous activation is unreachable anyway. | Approx. | ITM-019, ITM-021 |
| Ring of the Ram charges | DMG ch.7 entry (check whether it recharges). | `RingChargeManager.RegenerateCharges` adds 1d10 charges on every rest (`RingFactory.cs:596`). | Unverified vs RAW | ITM-064 |
| Specific items (`OnLongRest`) | DMG ch.7. | The behaviors define `OnLongRest`, but specific items never enter play. | Data only | ITM-025 |

### 2.3 Advancement: XP, levels, multiclassing

| Area | RAW | Current behaviour | Status | Issues |
|---|---|---|---|---|
| XP award | DMG ch.2 (Experience Awards table): looked up by each character's level and the creature's CR, then divided by the number of party members. Ad hoc awards for traps and story goals. | `ExperienceCalculator.XPTable` is keyed only by `round(CR - APL)` (clamped to -8..12). A CR = APL creature gives 300 XP total at any level, divided by the active PCs, dead ones included. There are no non-combat awards. | Deviates | CHR-004, ENC-013 |
| XP thresholds | PHB ch.3: 1,000 x n(n-1)/2. | `ExperienceCalculator.GetXPForLevel` uses that formula; the cap is level 20. | RAW | none |
| Multiple levels at once | DMG ch.2: a character should not advance more than one level at a time; extra XP is capped one short of the next level. | `AddExperience` can add several `PendingLevelUps` from one award, and the UI applies them one after another. | Deviates (minor) | none |
| When level-up happens | PHB ch.3 and DMG ch.2: between adventures (training is optional). | Right after the XP screen, before the rest. `Level` rises as soon as XP is gained, and class levels follow when the UI applies each pending level. | Approx. | none |
| HP per level | PHB ch.3: roll the class hit die + CON mod, minimum 1 per level; maximum at 1st level. | `ApplyPendingLevelUp` rolls by `GameSettings.hpCalculationMode` (Roll by default; changed only by the Left Ctrl+H debug key). The die comes from a hard-coded table that is wrong for Rogue, Bard and Ranger. Creation counts CON twice. | Deviates | CHR-001, CHR-002, CORE-007 |
| Skills, feats, ability increases | PHB ch.3 and ch.4. | Skill points use per-class pools. Feat cadence: general feats, plus fighter, wizard and monk bonus feats. +1 ability point every 4th level. A Weapon Focus weapon chosen at level-up is lost. Class auto-feats are granted only at construction. No Craft or Profession skills. | Approx. | CHR-006, CHR-007, CHR-014, CHR-015 |
| Spells known at level-up | PHB ch.3. | `SpellSelectionUI.ShowForLevelUp`. Progression is capped by the slot tables. | Approx. | SPL-041, UI-003 |
| Multiclassing | PHB ch.3: any class is allowed at level-up. NPC classes are for NPCs (DMG ch.4). DMG ch.6 adds prestige classes with entry requirements. | Any of the 11 PHB classes can be taken. `LevelUpUI` strips the 5 NPC classes. There are no prestige classes (no code mentions them). | Approx. | CHR-035 |
| Multiclass XP penalty | PHB ch.3: -20% XP for each class (other than the favored class) that is two or more levels below the highest class. Humans and half-elves use their highest class as favored. | `CharacterStats.RecalculateXPPenaltyStatus` (`:3618`) drops every class at the highest level, then the favored class, and penalizes only when the remaining classes differ from each other by more than one level. As a result, a two-class character never gets a penalty (Fighter 5 / Wizard 1 is not penalized), and classes that are equally far below the highest are not penalized either. A favored class of "Any" (human, half-elf) skips the check entirely, although RAW still penalizes their classes other than the highest. The penalty is a flat 20% (`GetEffectiveXpGain`), never 20% per class. No test covers this. | Deviates | CHR-058 |
| Level loss | DMG ch.8. | Not modelled. Lost levels are represented as negative levels, which the rest erases. | Deviates | CORE-036 |

### 2.4 Wealth: treasure, gold, buying and selling

| Area | RAW | Current behaviour | Status | Issues |
|---|---|---|---|---|
| Treasure per encounter | DMG ch.3: treasure by EL (coins, goods, items). NPC gear counts against it. | `TreasureGenerator.Generate(el)` rolls coins, goods and items by EL. `monsterGearGP` is never passed, so enemy gear is extra wealth. The RNG is unseeded. EL math differs between systems. | Approx. | ITM-016, ENC-013 |
| Treasure items | DMG ch.7 item tables. | Magic and mundane treasure items are value-only placeholders: no weapon dice, no potion or scroll spell, and wands become wondrous items. In practice they are only good for selling. | Deviates | ITM-016 |
| Gems and art | DMG ch.3; Appraise (PHB ch.4). | Appraise is run with the party's best modifier, and a failed check skews the value. They sell at 50% of the appraised value. Whether gems and art should sell at full value (like trade goods, PHB ch.7) needs a rules decision. | Approx. | none |
| Treasure division | Up to the table; the DMG assumes an equal share. | Coins go automatically into one party pool, and loot goes into one shared stash. There is no per-character share or tracking. | Approx. | none |
| Gold pools | One wealth per character (PHB ch.7). | Two unconnected pools. `EconomyService.PartyGold` (starts at 1000) pays for the store, treasure and selling. `CharacterStats.ComponentGold` (1000 per PC, `CharacterStats.cs:196`) pays for crafting and costly components. Nothing moves gold between them. | Deviates | ITM-029 |
| Starting wealth and wealth by level | PHB ch.7 (starting gold by class); DMG ch.5 (Character Wealth by Level table, for example 2,700 gp at 3rd level). | Class starting kits, plus 1000 party gold, plus 1000 component gold each, plus a seeded stash (`PartyStash.SeedDefaultItemsIfEmpty`: weapons, armor, 30 Cure Light Wounds potions, 4 Shield of Faith potions, bolts, rope and torches). There is no wealth-by-level target, check or report. `NPCTemplate.TotalWealthGP` holds values that match the DMG ch.5 wealth-by-level table (9,000 gp at 5th level, 49,000 gp at 10th) but is read only by its `ToString`. | Missing | none |
| Buying | PHB ch.7 price lists; DMG ch.5: town size limits the gp value of available items and the cash on hand. | `StoreInventory` sells its whole catalog in unlimited quantity at list price: mundane gear, +1/+2 arms and armor, every generated scroll, potion and wand, every ring, rod and wondrous item (including 10 test items). There is no settlement, gp limit or availability roll. `StoreItemEntry` holds only `ItemId`, `Category` and `PriceGp`: there is no stock or quantity field, and the merchant has no cash, so every sale is paid in full. Purchases go to the stash (`StoreUI.BuyItem`, `:2093`). Catalog prices are written into the shared `ItemDatabase` templates (ITM-037), so that must be fixed before any pricing or settlement work. | Deviates | ITM-014, ITM-037 |
| Selling | PHB ch.7: half price, except trade goods. DMG ch.7: charged items are prorated. | `StoreInventory.GetSellPrice` (`:383`) is floor(base x 0.5), with wands prorated by charges. Selling a stack pays for one item and removes the whole stack. Enhanced and crafted items get wrong prices. | Approx. with bugs | ITM-003, ITM-007 |
| Spellcasting and healing services | PHB ch.7 (Goods and Services table): spellcasting at caster level x spell level x 10 gp, plus material costs. | No services. No spell can be cast outside combat, so healing, Restoration, Remove Disease and raising the dead are not available between fights. | Missing | none |
| Material components | PHB ch.10: costly components are consumed. | Taken from `ComponentGold`, and enforced only for Stoneskin. | Deviates | SPL-020, ITM-029 |

### 2.5 Item creation

| Area | RAW (DMG ch.7) | Current behaviour | Status | Issues |
|---|---|---|---|---|
| Cost | Half the market price in gp, 1/25 in XP. XP cannot drop below the current level's minimum. | `CraftingCostCalculator.FromMarketPrice`: gp = market/2, XP = market/25. `CharacterStats.MaxSpendableXP` enforces the level floor. The gp comes from `ComponentGold`. The metamagic overlay drops the scroll-substitution cost. | Approx. | ITM-009, ITM-029 |
| Time | 1 day per 1,000 gp of base price, 8 hours of work per day. Crafting and adventuring compete for time. | Days are computed (max(1, market/1000)), and `CraftingTimeTracker.AdvanceDays` counts them, but nothing reads the counter. Crafting is instant and can be repeated in every hub visit. | Deviates | ITM-017 |
| Prerequisites | Feat, caster level, and the item's prerequisite spells, which the creator must have or supply (for example from a scroll). | Feat and caster level are checked. Spell sources: the crafter, the party or a scroll substitute. Rings, rods and wondrous items have no spell prerequisites. The Spellcraft DC is never rolled, and the substitute scroll is not consumed. The crafter is always the first PC with a crafting feat. | Approx. | ITM-017, UI-008 |
| Results | All 8 feats. | Scribe Scroll and Craft Wand work. Brewed potions cannot be drunk. Craft Magic Arms and Armor never succeeds, because all its entries are upgrades and the workshop never sets an upgrade target. Craft Staff lists nothing. Ring, rod and wondrous crafting produces catalog clones (rods and activations are inert). | Partial | ITM-002, ITM-017, ITM-018, ITM-024; ITM-066 |
| Debug mode | n/a | The workshop debug toggle (`CraftingWorkshopUI._debugMode`, a static field, so it persists across opens) sets `CraftingValidator.DebugMode`, which skips every check and cost and offers every crafting feat. | n/a | none (related: UI-014, ungated debug tooling) |

### 2.6 Carrying, equipment condition, time, party

| Area | RAW | Current behaviour | Status | Issues |
|---|---|---|---|---|
| Carrying capacity | PHB ch.9: the STR table, x4 for every +10 STR; Small x3/4, Large x2, quadrupeds more. Loads affect max Dex, check penalty and speed (medium and heavy loads both use the reduced-speed column, 30 ft to 20 ft). Coins weigh 50 to the pound (PHB ch.7). | `CharacterStats.GetHeavyLoadForStrength` uses the table and the x4 rule. Light, medium and heavy are thirds of the heavy load. Inventory applies the Dex cap and check penalty, and speed uses x2/3 for medium and x1/2 for heavy (slower than RAW). There is no size multiplier, and gold weighs nothing. The party stash is weightless and unlimited. Stacks and ammo weigh as one item. | Approx. | ITM-013 |
| Armor speed | PHB ch.7: medium and heavy armor reduce speed. | Not applied. | Missing | ITM-010 |
| Item damage and repair | PHB ch.8 (sunder) and ch.9 (breaking objects); DMG ch.8 (broken condition); repair with Craft or Mending/Make Whole. | Items have hardness and HP, and sunder sets `IsBroken` and `IsDestroyed`. Destroyed items are filtered from loot. `IsBroken` is only shown in the log and tooltip; it applies no penalty. The Mending handler only writes a log line (`GameManager_Spells_Cantrips.cs:201`). There is no repair at rest, in the store or by Craft (no Craft skill). | Data only | CHR-014; ITM-065 |
| Time and calendar | PHB ch.9: overland movement and travel time; spell durations in game time. | No game clock exists. `RoundsPerDay` = 14400 is checked only against the combat round counter. Crafting days are counted but unused. Poison secondary timers run in real seconds. `RingUseTracker` counts rests as days. | Missing | CMB-007, ITM-017 |
| Rest interruptions and wandering monsters | DMG ch.3: wandering monster checks while resting or exploring. | None. The rest is automatic and safe. | Missing | none |
| Encounter cadence | DMG ch.3: encounters per day by difficulty, and resource attrition across the adventuring day. | One encounter per "day", because a full rest follows every fight. The player picks the difficulty. | Deviates | CORE-015 |
| Party roster | PHB ch.7 (hirelings), DMG ch.4 (NPCs, the Leadership feat, cohorts and followers); replacing dead PCs is up to the DM. | Four fixed PC slots (`PC_Hero1..4`) are filled once at creation. There is no recruiting, hiring, dismissing or replacement, and no Leadership feat (CHR-022). Test presets overwrite the party (CORE-004). | Missing | CHR-022, CORE-004 |

### 2.7 What the rest clears and what it keeps

`RestorePartyAfterCombat` is the only out-of-combat state transition, so its exact effect is the party-management model.

| Cleared or restored | Kept |
|---|---|
| HP to `TotalMaxHP`; nonlethal damage; temp HP; `BonusMaxHP` | XP, class levels, `PendingLevelUps` |
| All `StatusEffectManager` effects, all conditions (including `EnergyDrained`, prone, fatigue) | Ability damage and drain (`AbilityScoreDamage`) |
| All spell slots (prepared spells kept), held touch charges, `ActiveBuffs`, Mage Armor | Prepared spell choices |
| Rage, Turn Undead, Bardic Music, domain-power and template-smite counters; `TemporarySTRBonus`, `IsFatigued` | Stunning Fist uses |
| Size back to base; Disguise Self, Expeditious Retreat, invisibility, See Invisibility, Glitterdust, Acid Arrow, Ray of Enfeeblement and Touch of Idiocy side-state; Resist Energy, Protection from Energy, Protection from Arrows, Stoneskin and Dimensional Anchor records | `ActiveDiseases` |
| `ActivePoisons`; temporary item spell effects on equipped and backpack items | Inherent bonuses (Wish, tomes) |
| Area, wind, summon, Mirror Image, melee-reaction and curse state | Wand charges, item HP and broken state, rod uses |
| Ring and wondrous daily uses (via `OnRest`) | Inventories, stash, both gold pools |

The rest does not call `ConditionService.CleanupOnCombatEnd`, `EffectService.ClearAll` or `AIService.ClearAuraSaveImmunities` (CORE-002). Some combat statics, such as grapple links and mounts, also outlive the encounter (CMB-038).

### 2.8 Issues filed from this review

These defects were found while writing this doc. Each is cited in the tables above.

| ID | Defect | Section |
|---|---|---|
| CORE-035 | Daily recovery never runs: `ProcessDailyEffects` needs combat round 14400, and `CharacterStats.ApplyCompleteRest` has no gameplay caller, so ability damage and disease never recover between fights. | 2.1 |
| CORE-036 | The rest erases negative levels, including the permanent `EnergyDrained` penalty from Resurrection and Wish; no 24-hour save, no level loss. | 2.1, 2.3 |
| CHR-057 | Stunning Fist uses are set once (`FeatManager.cs:506`) and never refreshed by the rest; latent until CMB-022 is fixed. | 2.2 |
| ITM-064 | Ring of the Ram regains 1d10 charges on every rest (`RingChargeManager.RegenerateCharges`); check against the DMG ch.7 entry. | 2.2 |
| CHR-058 | Multiclass XP penalty logic is wrong: two-class characters are never penalized, and the penalty does not stack per class. | 2.3 |
| ITM-065 | `IsBroken` has no gameplay effect, and Mending only logs. | 2.6 |
| ITM-066 | Craft Magic Arms and Armor never succeeds: all its entries are upgrades and `CraftingWorkshopUI._upgradeTarget` is never set. | 2.5 |

## 3. Data model: what persists between battles

Everything lives in memory for one Play session. There is no save system. The only persisted data is window layout in `PlayerPrefs` (`DraggableWindow`, `ResizableWindow`, `CombatUI`, `PreCombatInventoryUI`). `CharacterStats` is `[Serializable]`, but Unity serialization would drop its `HashSet` and `Dictionary` fields (CHR-046). The "save compatibility" comments refer to nothing (ITM-060).

| State | Owner | Lifetime and reset |
|---|---|---|
| The party | `GameManager.PCs` and `PC1..PC4`: four `PC_Hero1..4` GameObjects built by `SceneBootstrap`, given stats by `SetupCreatedCharacters` | Whole session. Re-initialized only by a test preset (CORE-004). Dead PCs stay in the list and are revived by the rest. |
| Character sheet | `CharacterStats` on each `CharacterController`: ability scores, `AbilityScoreDamage`, `ClassLevels`, `Level`, `PendingLevelUps`, `ExperiencePoints`, `HasXPPenalty`, `Feats`, skills, `HitPointGainsByClassLevel`, inherent bonuses, `ComponentGold`, the daily counters | Session. The rest changes only the fields in 2.7. |
| Spells | `SpellcastingComponent` on each caster: known spells, `SpellSlots` with their prepared spell, spontaneous and partial-caster data | Session. Slots are refreshed by the rest and by `RefreshSpellSlots` after a level-up (SPL-013). |
| Personal gear | `InventoryComponent.CharacterInventory` (an `Inventory`): body slots, hands, slotless items, backpack `GeneralSlots` | Session. Weight affects encumbrance. |
| Party stash | `EconomyService.PartyStash` (`PartyStash`, a plain `List<ItemData>` with no stacking or weight) | Session. Seeded once. Locked during combat (`PartyStash.Lock`), unlocked in the hub and the loot window. |
| Party gold | `EconomyService.PartyGold` (`GameManager.PartyGold`, `SpendGold` and `AddGold` delegate to it) | Session. Starts at 1000 (`GameManager.partyGold`, passed to `EconomyService.Initialize`). Raised by coins and sales; lowered by purchases. |
| Component gold | `CharacterStats.ComponentGold`, one per PC | Session. Starts at 1000. Lowered by crafting and Stoneskin; refunded only on crafting rollback (ITM-029). |
| Store catalog | `StoreInventory` singleton on the GameManager GameObject | Built once in `Awake`. Unlimited stock. Its build writes prices into shared item templates (ITM-037). |
| Day counters | `CraftingTimeTracker` (static class, crafting days), `RingUseTracker` (static singleton, 7 rests = 1 week) | Session. Neither `Reset` method has a caller, tests included. |
| Loop statistics | `CompletedCombatCount`, `TotalLootItemsCollected`, `TotalEncounterXPDefeated` on `GameManager` | Session. Logged on each return to encounter selection. |
| Enemies | 15 `NPC_Enemy_*` slots, re-initialized per encounter and parked at (-1000,-1000) | Per encounter. Summons are destroyed by the rest, but their entries remain in `NPCs`. |

Things that do not exist and would need a home: a game clock and calendar, location or settlement, a party roster beyond 4 slots, a per-character purse, recorded treasure shares, quest or story state, and campaign settings (the HP mode lives in `GameSettings`, set only by a debug key).

## Extension points

Where to hook each kind of change today. Unqualified names are methods on `GameManager`. Most are private, but `RestorePartyAfterCombat`, `ReturnToEncounterSelection`, `GeneratePostCombatTreasure` and `ProcessDailyEffects` are public.

| Change | Hook | Notes |
|---|---|---|
| Insert a step between loot and XP, or between level-up and rest | `ShowPostCombatXPFlow` and `ContinueToRestAndNextCombat` in `GameManager.LootCollection.cs` | The flow is a chain of UI callbacks. Each step must call the next one when it finishes, including on its error branches. The early returns in `ShowPostCombatXPFlow` call `ContinueToRestAndNextCombat`, but `BeginPostCombatLootCollection` stops the loop if `LootCollectionUI` cannot be created. |
| Change what a rest does | `RestorePartyAfterCombat` (`GameManager.cs:868`) | This is the only rest. The PC start cells are duplicated in `SetupCreatedCharacters` (`:1872-1878`). New per-day counters must be reset here; grep `UsesToday`/`UsedToday` to find them. |
| Add a hub button or window | `PreCombatHubUI.Open(...)` callbacks in `OpenPreCombatHubPhase` | Return through `ReturnToPreCombatHubFromSubWindow` so the stash is unlocked and the spell-preparation status is refreshed. Set `WaitingForPreCombatInventory` while a modal is open, or world input leaks through `Update`. |
| Change XP | `ExperienceCalculator.CalculateXPForCombat` and `GetXPForCR`; `CharacterStats.AddExperience` and `GetEffectiveXpGain` | `CombatXPResult.CharacterLeveledUp` drives which PCs see `LevelUpUI`. |
| Change level-up | `CharacterStats.ApplyPendingLevelUp`; `LevelUpUI.ShowForCharacter`; `CharacterCreationManager.StartLevelUpFlow` | Never assign `Stats.Level` directly: `EnsureMulticlassDataInitialized` rewrites it from `ClassLevels + PendingLevelUps` (CHR-034). |
| Change gold or the stash | `EconomyService` (`PartyGold`, `SpendGold`, `AddGold`, `PartyStash`) | Its `BuyItem`/`SellItem` are unused (ITM-047). Play uses `StoreUI` and `StoreInventory.GetSellPrice`. |
| Change treasure | `GeneratePostCombatTreasure` (`GameManager.TreasureGeneration.cs`) and `TreasureItemConverter` | Ignore the dead `TreasureUI` path and `MagicItemLootGenerator` (ITM-048, ITM-049). |
| Time-based effects | `GameManager.ProcessDailyEffects` (wraps `EffectService.ProcessDailyEffects`); `CraftingTimeTracker` | There is no caller for real days yet (CORE-035). A clock should call these. |
| Natural healing and daily recovery | `CharacterStats.ApplyCompleteRest(hours)` (`CharacterStats.cs:1640`) | Reuse it rather than writing new rest logic. With 1+ hours it turns exhausted into fatigued; with 8+ hours it removes fatigue and heals 1 point of damage on every ability (`HealAllAbilityDamage(1)`). It does not restore HP, slots or per-day uses. Only `FatigueExhaustionTests` calls it today (CORE-035). |

## 4. Gaps and backlog

These are ordered by how much each one holds back a PHB/DMG-faithful party-management simulation. Each item names the issues it would close or depend on.

1. **Give the campaign a state owner and a clock.** Add a `CampaignState` or `PartyManagementService` (a plain class owned by `EconomyService` or a new service, not another `GameManager` partial; CORE-022). It should own the gold, the stash, the roster and a game clock (days and hours). Give it one `AdvanceTime(hours)` entry point that drives everything time-dependent: daily recovery (CORE-035), disease, negative-level saves (CORE-036), crafting days (ITM-017), weekly and monthly item resets (ITM-021), rod uses (ITM-018), and poison timers in rounds (CMB-007). Everything below depends on this.
2. **Replace the automatic full rest with rest actions by the rules.** Offer "Continue (no rest)", "Rest 8 hours" and "Full day of bed rest" between fights. Apply PHB ch.8 natural healing (1 or 2 hp per level), plus ability-damage recovery and the fatigue rules through the existing `CharacterStats.ApplyCompleteRest(hours)` (CORE-035), spell-slot and per-day refresh (fix CHR-057 and SPL-011), and move the rest into the time model. Keep the current full rest as a difficulty option (CORE-015, CORE-007). This brings back attrition across several encounters a day, which DMG ch.3 encounter design assumes.
3. **Make death and defeat real.** Dead PCs stay dead: skip `IsDead` in the rest, exclude them from XP and the hub, and keep their gear lootable. Add a defeat screen (CORE-001). Add raise-dead paths: a spellcasting service in town, Raise Dead and Resurrection with level loss done as real level loss rather than a negative level (CORE-036), and component costs enforced (SPL-020). Add a "replace a dead PC" flow that creates a new character at a level and wealth set by the rules.
4. **Correct the XP awards.** Look up each PC's own level against CR, using the DMG ch.2 table (CHR-004). Exclude dead or absent PCs. Fix the multiclass penalty (CHR-058). Cap gains at one level per award (DMG ch.2). Allow awards for non-combat challenges. Unify the EL and CR math (ENC-013).
5. **Use one wealth model.** Merge `ComponentGold` into a party purse or per-character purses (ITM-029). Track per-PC treasure shares and report wealth against the DMG ch.5 wealth-by-level table after each fight. Pass `monsterGearGP` and turn treasure placeholders into real items (ITM-016). Fix selling bugs (ITM-003, ITM-007).
6. **Model settlements and the market.** A settlement with a DMG ch.5 gp limit and cash on hand, item availability rolls, and a limited magic-item stock replace the unlimited store (ITM-014). This needs new data: `StoreItemEntry` has no stock or quantity, and there is no merchant purse. Fix ITM-037 first, since catalog prices currently leak into the shared item templates. Add PHB ch.7 services: spellcasting (Cure, Restoration, Remove Disease, Raise Dead), hirelings, and lodging that ties into rest quality.
7. **Allow out-of-combat actions.** Casting between fights (healing, Restoration, Mending/Make Whole, long-duration buffs that last into the next fight), using wands of Cure Light Wounds, Heal skill long-term care, and identifying items. Today every cast needs an active PC turn.
8. **Make item creation take time and resources.** Spend crafting days on the clock (one crafter per project, 8 hours a day), roll the Spellcraft DC, consume substitute scrolls, pick the crafter (ITM-017, UI-008), fix potions and arms/armor upgrades (ITM-002, ITM-066), and keep the scroll-substitution cost under metamagic (ITM-009).
9. **Manage the party roster.** Recruit, dismiss and replace PCs; add the Leadership feat and cohorts (CHR-022); choose the starting level and wealth for new members; keep test presets from overwriting the party (CORE-004).
10. **Apply encumbrance and logistics in full.** Size multipliers, coin weight, and stash capacity (pack animal, cart or camp) (ITM-013), armor speed (ITM-010), and the broken-item penalties and repair (ITM-065, CHR-014 Craft skill).
11. **Add wandering monsters and travel.** DMG ch.3 wandering-monster checks during rest and travel, interrupting a rest with an encounter from the encounter system (see [ENCOUNTERS.md](ENCOUNTERS.md)). Overland travel time between settlements and encounter sites (PHB ch.9).
12. **Persist the campaign.** A save format for the campaign state, with DTOs rather than raw `CharacterStats` serialization (CHR-046, ITM-060). This is cheaper once item 1 has gathered the state in one place.

Tests: no test covers the full loop of victory, loot, XP, rest and the hub. `Tests/Inventory/PostCombatLootCollectionTests.cs` covers loot gathering and transfer to the stash only. `Tests/Services/EconomyServiceTests.cs` covers `EconomyService` gold and its unused price helpers (ITM-047), not `StoreInventory` or `StoreUI`. No test covers XP awards, the XP penalty or the rest (ITM-042, TST-015). New party-management code should come with `RunAll()` suites as described in [TESTING.md](../TESTING.md).
