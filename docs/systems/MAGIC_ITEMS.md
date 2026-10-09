> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-03.

# Equipment and magic items: content and status

This is a content-and-status reference for weapons, armor, gear and magic items, measured against PHB ch.7 (Equipment) and DMG ch.7 (Magic Items). It answers three questions per item family: what exists in the catalog, what actually does something in play, and how far it is from the books. How the item code is built (ItemData, ItemDatabase, cloning, init order, inventory, store and crafting pipelines) is in [architecture/items-and-economy.md](../architecture/items-and-economy.md); it is not repeated here. Bugs are cited by ID; the entries are in [issues/ITM.md](../issues/ITM.md) and the other `issues/` files. The between-battle loop that spends and gains items is in [PARTY_MANAGEMENT.md](PARTY_MANAGEMENT.md).

Counts below were computed from the registration code with grep and small scripts, not from a running game. Everything here was read, not run: treat all of it as not verified in Play mode unless stated.

Status words used throughout:

| Status | Meaning |
|---|---|
| **Wired** | Gameplay code reads the data during play. Where is named. |
| **Creation** | Applied once, when `EnchantmentFactory` builds the item. Items built any other way do not get it. |
| **Data** | Registered and shown in tooltips; no gameplay code reads it. |
| **Unreachable** | The runtime code exists, but no player or AI path calls it. |

## 1. Mundane equipment coverage

Base gear is registered in `ItemDatabase.Register*` (Equipment/Items/ItemDatabase.cs). Prices come from `ItemDatabase.ApplyDefaultBasePrices`, a hand-written table.

### Weapons (PHB Table 7-5)

| PHB group | In the catalog | Missing |
|---|---|---|
| Simple, unarmed and light | Unarmed strike, gauntlet (Hands slot), dagger, light mace, sickle, spiked gauntlet | Punching dagger |
| Simple, one- and two-handed | Club, heavy mace (plus legacy alias `mace`), morningstar, shortspear, longspear, quarterstaff, spear | none |
| Simple, ranged | Light and heavy crossbow, dart, javelin, sling | none |
| Martial, light | Handaxe, short sword, "Flail, Light" | Throwing axe, light hammer, kukri, light pick, sap |
| Martial, one-handed | Longsword, rapier, scimitar, battleaxe, warhammer, trident, "War Pick" | none by name (see deviations) |
| Martial, two-handed | Greatsword, greataxe, greatclub, falchion, heavy flail, guisarme, halberd, ranseur, glaive, lance | Scythe |
| Martial, ranged | Longbow, shortbow, composite longbow and shortbow, plus `composite_{long,short}bow_{1..4}` Strength-rated variants | none |
| Exotic | Spiked chain, whip | Bastard sword, dwarven waraxe, kama, nunchaku, sai, siangham, orc double axe, dire flail, gnome hooked hammer, two-bladed sword, dwarven urgrosh, bolas, hand crossbow, repeating crossbows, shuriken, net |

That is 54 Weapon-typed entries (52 in the weapon sections plus spiked gauntlet and torch). Deviations from PHB data:

- Halberd is registered as a reach weapon (ITM-012).
- "Flail, Light" is a 3.0 name. It is flagged `IsLightWeapon`; the PHB 3.5 flail is a one-handed, non-light martial weapon.
- "War Pick" deals 1d8 ×4. The PHB heavy pick deals 1d6 ×4.
- `ApplyDefaultBasePrices` and the store price the heavy mace at 8 gp; PHB Table 7-5 gives 12 gp. The torch is 1 gp (PHB 1 cp).
- `ApplyDefaultBasePrices` has no entry for the gauntlet, sickle, shortspear, longspear, dart, handaxe, light flail, trident, war pick, guisarme, halberd, ranseur, spiked chain, whip, glaive, greatclub, the composite bows and their 8 rated variants, the spiked gauntlet or the `mace` alias. Their `BasePriceGp` is 0, which flows into material-variant prices (for example `mw_halberd` costs only the 300 gp masterwork add-on), cold iron cost and crafting. Selling one of the mundane items falls back to a flat 10 gp estimate in `StoreInventory.ResolveBaseValue`.
- The store sells 16 of these weapons plus the torch (`StoreInventory.InitializeStore`), with +1 and +2 variants of the 16 weapons. The torch is added after `AddEnhancedStoreVariants` runs and has no `PlusN` id, so it gets no magic variants.

### Armor and shields (PHB Tables 7-6)

All 12 PHB armors exist with metal/non-metal tags (`.Metal()`, `.NonMetal()`, `.MixedMaterial()` for studded leather). The six PHB shields exist, plus spiked light and heavy steel shields and two legacy aliases (`light_shield`, `heavy_shield`). Spiked armor is missing. The store lists 11 armors (no padded) and 6 shields.

Rules gaps: armor never reduces speed (ITM-010); wondrous robes do not share the armor slot (ITM-011); base shields never set `ArmorMaterial`, which blocks mithral and darkwood shields (section 2).

### Ammunition and adventuring gear

Arrows (20), bolts (20) and sling bullets (10) are registered, plus a legacy bolt stack. Gear is limited to what play uses: torch (as a light weapon), hemp and silk rope plus a legacy `rope` alias (`RopeItemData`, used by Animate Rope), spell component pouch, diamond dust (250 gp, a costly material component) and the locked gauntlet. Four potions are hand-registered (two Cure Light Wounds ids, Shield of Faith, Greater Healing).

No PHB special substances or tools exist as usable items: alchemist's fire, acid flask, holy water, tanglefoot bag, thunderstone, antitoxin, sunrod, smokestick, healer's kit, thieves' tools and the like. `TreasureData.Table3_8_Alchemical` and `Table3_8_Tools` roll them as treasure, but the converter turns them into sell-only placeholders (ITM-016).

## 2. Masterwork, special materials and enhancement bonuses

`MaterialProperties` (Equipment/Materials/MaterialProperties.cs) holds all material rules. `ItemMaterialFactory` (Equipment/Enchantments/ItemMaterialFactory.cs) builds the variants. `ItemMaterial` (Equipment/Items/ItemMaterial.cs) is the per-item record on `ItemData.Material`. Every special-material item is also masterwork.

**What registers.** `ItemMaterialFactory.RegisterAllMaterialVariants` builds, from fixed id lists: masterwork, cold iron, silver and adamantine versions of 23 common weapons; darkwood club, quarterstaff, shortbow and longbow; masterwork and adamantine versions of all 12 armors plus mithral for the 8 metal armors; masterwork versions of the 4 wooden and steel shields; and masterwork, cold iron, silver and adamantine versions of the 3 ammunition stacks. By those lists that is 144 variants (the runtime log line was not checked). Ids are `mw_<id>`, `cold_iron_<id>`, `silver_<id>`, `adamantine_<id>`, `mithral_<id>` and `darkwood_<id>`.

| Material | Valid on (code) | Wired effects | Code cost added | DMG cost | Deviations |
|---|---|---|---|---|---|
| Masterwork | weapons, armor, shields, ammo | +1 attack (`CharacterController`, folded into the enhancement term in logs); armor ACP −1 (`ItemData.Effective*`) | +300 weapon, +150 armor/shield, +50 otherwise (ammo stacks) | +300 / +150; ammo +6 each | Ammo stack price |
| Adamantine | every weapon (bows included), ammo, every armor and shield | DR/adamantine bypass; body armor DR 1/2/3 (`Inventory.ApplyAdamantineArmorDR`) | weapon +3,000; ammo +60; armor +5,000/10,000/15,000; shield +3,000, no DR | same for weapons and metal armor; ammo +60 per missile | Variants are made for non-metal armor and bows. The ammo surcharge is charged once per stack of 20 or 10, not per missile (per-missile reading of the DMG price not verified against the book). Hardness and the sunder bonus are not implemented (`ItemData.GetBaseHardness` ignores material) |
| Mithral | metal armor and shields; any weapon | half weight, ACP −3, max Dex +2, ASF −10% | armor +1,000/4,000/9,000; shield +1,000; weapon max(500, 500/lb) | armor and shield same; other items +500/lb | Category shift is used only for proficiency and Barbarian fast movement (ITM-010). No weapon variants are registered |
| Cold iron | weapons and ammo | DR/cold iron bypass | + base price (double cost) | double cost, and +2,000 gp to enchant | The +2,000 enchanting surcharge is not charged. A weapon with `BasePriceGp` 0 gets cold iron for free |
| Alchemical silver | weapons and ammo | DR/silver bypass; −1 damage | ammo +2; light +750; one-handed +3,000; two-handed +9,000 | ammo +2 per missile; light +20; one-handed +90; two-handed +180 | Weapon costs are 30-50 times the DMG values. The ammo surcharge is charged once per stack |
| Darkwood | ids containing club, quarterstaff, shortbow or longbow; non-metal shields | half weight | +5 gp per pound of base weight | +10 gp per pound | Half the DMG cost. No darkwood shield is ever made (shields have no material tag) |

**Data gap.** The shield registrations in `ItemDatabase.RegisterShields` never set `ArmorMaterial`, so it stays `Unknown`. `MaterialProperties.IsMaterialValidForItem` then rejects mithral and darkwood on every shield. Darkwood and mithral shields exist only as `SpecificItemDatabase` entries, which are unreachable (section 9).

**Reachability.** The store lists none of the material variants. They reach play only as NPC gear: `GameManager.NPCSetup` upgrades mundane NPC weapons and armor through `ItemMaterialFactory.GetRandomMaterialWeapon/Armor` by CR, and those items can be looted.

**Display name.** `ItemData.FullDisplayName` builds "+N", then the material prefix, then the ability names joined with "/" ("+1 Holy/Flaming Longsword", "Adamantine Longsword"). "Masterwork" appears only when there is no enhancement, material or ability. Alchemical silver shows as "Silver".

**Enhancement bonuses.** `ItemDatabase.RegisterEnhancedEquipmentVariants` registers +1 and +2 copies of every base weapon, armor and shield that has an `ItemID` `PlusN` value. Price is `ItemData.GetEnhancedPriceGp`: base + (effective bonus)² × 2,000 for weapons or × 1,000 for armor and shields, plus flat-cost abilities. The DMG adds the masterwork cost on top; the code does not, and `Tests/Combat/JumpAndMagicWeaponRulesTests.cs` pins that (+1 longsword = 2,015 gp, DMG 2,315 gp). Enhancement is clamped to 0-5 and stored in two fields (ITM-035). `EnchantmentFactory.CreateEnchantedVariant` enforces enhancement 1-5 and a total of +10.

## 3. Weapon, armor and shield special abilities

There are 73 `EnchantmentType` values plus `None` (Equipment/Enchantments/EnchantmentType.cs): 30 weapon abilities and 43 armor and shield abilities. Each has an `EnchantmentStats` row in `EnchantmentProperties` (bonus equivalent or flat cost, slot, restrictions, incompatibilities). Combat effects run only inside `CharacterController.PerformSingleAttackWithCrit`, through `EnchantmentEffects` and `AdvancedEnchantmentEffects` (both in EnchantmentEffects.cs). Grep shows 14 call sites there and no others. The many other helpers in that file have no callers (ITM-023).

Summary: 21 abilities are wired (18 weapon abilities on the attacker side plus the 3 fortification levels on the defender side), 3 are creation-only, and 49 are data only. In the table, "Combat" means attack resolution in `PerformSingleAttackWithCrit` and "SRD" is the DMG/SRD price.

| Ability (`EnchantmentType`) | Slot (code) | Code cost | SRD | Runtime status | Deviations |
|---|---|---|---|---|---|
| Flaming, Frost, Shock | Weapon | +1 | +1 | Wired: +1d6 per hit | Damage is typed as the weapon's physical type, so DR applies and energy resistance does not (CMB-011) |
| Corrosive | Weapon | +1 | not in DMG | Wired: +1d6 acid | Non-DMG addition; CMB-011 |
| FlamingBurst, IcyBurst, ShockingBurst | Weapon | +2 | +2 | Wired: +1d6 per hit; crit +1d10 (×3 2d10, ×4 3d10) | CMB-011. Crit dice stay when Fortification negates the crit |
| Thundering | Weapon | +1 | +1 | Wired: crit sonic dice only (1d8, scaling with the multiplier) | No DC 14 Fortitude save against deafness |
| Holy, Unholy, Axiomatic, Anarchic | Weapon | +2 | +2 | Wired: +2d6 vs the opposed alignment; alignment DR-bypass tags | No negative level on a wielder of the opposed alignment |
| Bane | Weapon | +1 | +1 | Wired: +2 attack, +2d6 | Missing the +2 damage. Matches `CreatureType` by exact string, so no subtypes |
| Keen | Weapon, melee, slashing or piercing | +1 | +1 | Creation: lowers `CritThreatMin` | Stacks with Improved Critical (CMB-010) |
| Vorpal | Weapon, melee, slashing or piercing; requires Keen | +5 | +5 | Wired: kills on a natural 20 that confirms | Piercing allowed and the Keen prerequisite is not DMG. Ignores crit immunity (CMB-064) and resolves before Fortification (ITM-067); see note |
| Vicious | Weapon, melee | +1 | +1 | Wired: +2d6 to target, 1d6 to wielder | none |
| Wounding | Weapon, melee | +2 | +2 | Wired, log only (ITM-015) | No Constitution damage |
| Merciful (`MercifulWeapon`) | Weapon | +1 | +1 | Wired: +1d6 | The bonus is lethal and weapon damage is never made nonlethal |
| Speed | Weapon | +3 | +3 | Data | No extra attack (ITM-023) |
| Throwing | Weapon, melee | +1 | +1 | Creation: `IsThrown`, 10 ft increment; `ItemData.CanBeThrown` also checks the ability | none |
| Returning | Weapon | +1 | +1 | Data | No thrown-only check; thrown weapons stay on the ground |
| Distance | Weapon, ranged | +1 | +1 | Creation: doubles `RangeIncrement` | none |
| Seeking | Weapon, ranged | +1 | +1 | Data | Description excludes total concealment |
| Defending | Weapon, melee | +1 | +1 | Data | none |
| SpellStoring | Weapon, melee | +1 | +1 | Data | No runtime code |
| BrilliantEnergy | Weapon | +4 | +4 | Wired: ignores armor, shield and natural armor AC; cannot harm undead or constructs | DMG ignores armor and shield bonuses only. No restriction against bows |
| Dancing | Weapon, melee | +4 | +4 | Data | Description says free action; DMG says standard action |
| Disruption | Weapon, bludgeoning | +2 | +2 | Data | Data uses a Fortitude save; DMG uses Will DC 14 |
| KiFocus | Weapon, melee | +1 | +1 | Data | none |
| GhostTouchWeapon | Weapon | +1 | +1 | Data | `CharacterController.HasGhostTouchWeapon` checks a `VisualTags` entry nothing sets (CMB-025) |
| Mighty cleaving | none | not in code | +1 | Missing | Exists only as a `TreasureData` string |
| FortificationLight, Moderate, Heavy | Armor or shield | +1 / +3 / +5 | +1 / +3 / +5 | Wired (defender): negates crit and sneak attack damage | 25/50/75% in code vs 25/75/100% in the DMG. Armor and shield percentages are summed, not maxed. Checked after Vorpal, burst dice and `OnCriticalHit` (CMB-020). Only a shield in the left hand counts |
| Energy resistance, Improved, Greater (5 energies, 15 types) | Armor or shield | +1 / +2 / +3 | +18,000 / +42,000 / +66,000 gp | Data | Priced as bonus equivalents, so they count toward the +10 cap. Tiers are not mutually exclusive (the incompatibility list is built but never assigned) |
| Shadow, SilentMoves, SlickArmor (3 tiers each, 9 types) | Armor or shield | +1 / +2 / +3 | +3,750 / +15,000 / +33,750 gp | Data | Bonus equivalents, not flat costs. DMG puts them on armor only |
| SpellResistance13/15/17/19 | Armor | +2 / +3 / +4 / +5 | same | Data | DMG also allows shields |
| Invulnerability | Armor | +3 | +3 | Data | DR 5/magic is never applied |
| GhostTouch | Armor or shield | +3 | +3 | Data | Can stack with GhostTouchShield on one shield |
| WildArmor | Armor | +3 | +3 | Data | DMG also allows shields |
| Glamered | Armor | 2,700 gp | 2,700 gp | Data | The only flat-cost ability |
| Etherealness | Armor | +4 | +49,000 gp | Data | Description caps the ethereal time at 10 minutes; the DMG lets the wearer stay ethereal as long as desired, once per day (rules recall, not verified against the book) |
| UndeadControlling | Armor or shield | +3 | +49,000 gp | Data | Description says "command undead 3/day as a cleric 3 levels lower"; the DMG ability controls up to 26 HD of undead per day as control undead (rules recall, not verified against the book) |
| ArrowDeflection | Shield | +2 | +2 | Data | none |
| Bashing | Shield | +1 | +1 | Data | Every magic shield already adds its enhancement to bash attack rolls, because the shield is the bash weapon |
| Blinding | Shield | +1 | +1 | Data | Description says Fortitude; DMG says Reflex |
| Animated | Shield | +2 | +2 | Data | `AnimatedShieldBehavior` serves only the unreachable specific items |
| Reflecting | Shield | +5 | +5 | Data | none |
| GhostTouchShield | Shield | +3 | +3 | Data | none |
| Arrow catching | none | not in code | +1 | Missing | none |

Combat defect CMB-064: normal crit confirmation never calls `IsImmuneToCriticalHits`. Grep finds it only on the paralyzed auto-crit path and coup de grace. Crit multipliers, burst dice and Vorpal therefore apply to undead, constructs and oozes.

**Where abilities come from.** `ItemDatabase.RegisterCommonEnchantedItems` builds 16 items through `EnchantmentFactory`: 10 weapons (including a +2 Speed longsword and a +1 Returning dagger, whose abilities do nothing, and a +1 Bane longsword whose bane type is the string "Undead", which does work against `CreatureType` "Undead"), 4 armors and 2 heavy steel shields. The store does not list them, and no code found gives them to a player. Treasure magic weapons and armor carry their ability names only in `Description` text (ITM-016). Crafting cannot add abilities (section 10). So in normal play a party meets enchanted weapons only if someone wires one of these sources.

## 4. Potions, scrolls and wands

| Factory (Equipment/Inventory/) | Spells covered | Caster level | Price | Save DC | Notes |
|---|---|---|---|---|---|
| `ScrollFactory` | every non-placeholder spell on a class list, arcane and/or divine copy | minimum for the spell level | SL × CL × 25; 0-level uses ceil(12.5 × CL) = 13 gp at CL 1 | 10 + SL + floor(SL/2) (`ScrollData.Create` fallback, `SpellSaveDCRules.ForMagicItem`) | Crafting floors the 0-level price to 12 gp (ITM-044) |
| `PotionFactory` | SL 0-3 passing `IsEligibleForPotion` (not personal, not area damage) | minimum | SL × CL × 50 (0-level 25 gp) | n/a | Healing spells become `ConsumableEffect.HealHP`, with the dice at the potion's caster level (`SpellDiceRules.Healing`, SPL-005: Cure Light Wounds at CL 1 is 1d8+1; Cure Minor Wounds a flat 1 point) |
| `WandFactory` | SL 0-4 | minimum | SL × CL × 750 (0-level 375 gp) | 10 + SL + floor(SL/2) (`SpellSaveDCRules.ForMagicItem`) | 50 charges; healing wands get their dice at the wand's caster level, and combat wand and scroll casts fix the spell's dice at the item's caster level (`GameManager.BuildWandPipelineSpell`/`BuildScrollPipelineSpell`, SPL-005; duration, range and SR still use the user's, SPL-016) |

Counts depend on the spell data and are logged at startup; they were not computed here. The DMG save DC for scrolls and wands is 10 + spell level + the minimum ability modifier needed to cast it (DMG p.214); since 2026-10-09 (SPL-001) scrolls and wands share it through `SpellSaveDCRules.ForMagicItem`, and a scroll with no stored DC uses it instead of the reader's DC. Scrolls crafted in the workshop still bake the crafter's own DC (ITM-008, owner question). A staff's DC is the wielder's own (DMG p.214; `StaffValidator.CalculateStaffSaveDC`).

**Payloads.** `ScrollData` and `WandData` (Equipment/Items/) carry spell id, caster level, base and effective level, baked save DC, heighten level, metamagic feats, the arcane flag and gold value. Legacy flat fields on ItemData are still filled, and wand charges are stored twice (ITM-034, ITM-036).

**Use rules implemented.** Scrolls go through `ScrollValidator`: class list, arcane/divine type, ability score, a caster level check (d20 + CL against scroll CL + 1, with a DC 5 Wisdom check against mishap), the Magic domain, and Use Magic Device at DC 20 + CL. Wands go through `WandValidator`: class list, Magic domain, and UMD at DC 20; using a wand does not provoke. Scrolls and wands with a targeted spell go through the normal targeting pipeline and are consumed after resolution. Potions affect only the drinker. Gaps: every item use costs a full-round action (ITM-005), scrolls and wands cast at the user's caster level rather than the item's (SPL-016), and brewed potions cannot be drunk (ITM-002).

**Metamagic consumables.** Store and loot scrolls and wands carry no metamagic. Metamagic scrolls and wands come only from the Crafting Workshop overlay (`CraftingWorkshopUI`), which recomputes the effective level, price and DC; it drops the scroll-substitution cost (ITM-009) and leaves the item's caster level below what was paid for. `GameManager.BuildConsumableSpellVariant` applies stored metamagic at use time (SPL-064 covers the duplicated metamagic code).

**AI.** NPCs never use potions, scrolls or wands (AI-009). Seven NPC definitions carry Cure Light Wounds potions in `BackpackItemIds` (in `NPCDatabase_M`, `NPCDatabase_P` and `NPCDatabaseCustom`), `GameManager.NPCSetup` puts them in the NPC's inventory, and `QuickSpawnSystem` template NPCs get consumables through `NPCTemplateAIConfigurator`, but no AI path drinks or uses them.

## 5. Rings

`RingDatabase.Init` registers 54 ring items of 28 types (Equipment/Rings/). Against the DMG ring table (as encoded in `TreasureData.Table7_18_*`: 63 rows across minor, medium and major, 46 distinct ring names):

| Group | Code | Status |
|---|---|---|
| Passive: Protection +1..+5, Energy Resistance (5 energies × minor/major/greater), Force Shield, Evasion, Freedom of Movement, Feather Falling, Swimming, Climbing, Jumping, Water Walking, Sustenance, Mind Shielding, Chameleon Power | 31 items | Deflection, energy resistance, force shield, skill competence and Evasion are wired in `Inventory.ApplyRingBonuses`. Ring deflection is its own field, and AC takes the higher of it and spell deflection (two rings: the higher; a Protection from Evil ward adds only its excess); checked in Play mode on 2026-10-08 by `rules/ring-deflection` (ITM-001). Feather Falling, Water Walking, Sustenance and Mind Shielding only set flags nothing reads (ITM-022). Freedom of Movement sets a flag that sticks and has no mechanical reader (ITM-006). Chameleon Power gives the Hide bonus only |
| Non-DMG rings: Resistance +1..+5, Warmth | 6 items | Resistance is wired (highest of ring and cloak). Warmth sets an unread flag |
| Activated: Invisibility, Blinking, Animal Friendship, Ram, Telekinesis, X-Ray Vision, Shooting Stars, Spell Turning, Djinni Calling | 9 items | Unreachable: the only call to `RingActivationManager.TryActivateRing` sits behind the consumable gate (ITM-019). Behind it, only Invisibility, Blink and X-Ray change state; Ram, Telekinesis and Shooting Stars never finish ability selection, and Animal Friendship and Djinni Calling only log. Spell Turning adds a status tag nothing reads |
| Counterspells, Spell Storing (Minor, "Major") | 3 items | No path loads a spell (ITM-020). The auto-counter hook in `GameManager.SpellCasting` would work if one were stored. "Major" holds 5 spell levels at 200,000 gp (DMG: standard ring 5 levels at 50,000 gp; major 10 levels at 200,000 gp) |
| Wizardry I-IV | 4 items | Wired only for prepared Wizards, in `SpellcastingComponent.GetWizardSlotsForLevel`, whose slot array covers spell levels 0-2, so Wizardry III and IV do nothing. Sorcerers and Bards never benefit. Slots are recomputed only at level-up or init |
| Regeneration | 1 item | The equip hook is skipped because the ring has no `RingAbilities`, so `RegenerationEffect` is never attached |
| Missing DMG rings | none | Improved Climbing/Jumping/Swimming, Friend Shield, Three Wishes, Elemental Command (4), standard Spell Storing |

Ring bugs: a ring's energy resistance can overwrite an active Resist Energy spell entry and then outlive the ring, because `CharacterStats.SetResistEnergyEffect` mutates the existing entry (ITM-068). "Rest" means `GameManager.RestorePartyAfterCombat`, which runs after every combat, so daily uses and Ram charges refresh per encounter. Unequipping and re-equipping a ring also resets its use counters. Only the Ring of Protection has a test (the scenario `rules/ring-deflection`).

## 6. Rods

`RodFactory.CreateAllRods` builds 33 rods (comments say 36, ITM-061). Rods are `ItemType.Wondrous` with `IsRod = true` and `IsWondrous = false` (ITM-033), held in a hand slot.

| Group | Code | DMG (Table 7-19) |
|---|---|---|
| Metamagic: Empower, Enlarge, Extend, Maximize, Quicken, Silent × lesser/normal/greater | 18; all prices match the DMG; max spell level 3/6/9, 3/day, CL 17 | 18 |
| Metamagic: Widen × 3 | 3; priced 14,000 / 54,000 / 121,500 | not in DMG |
| Other rods present | Absorption, Alertness, Cancellation, Enemy Detection, Flailing, Immovable, Lordly Might, Metal and Mineral Detection, Negation, Python, Security, Splendor | 12 of 18 |
| Other rods missing | none | Flame Extinguishing, Rulership, Thunder and Lightning, Viper, Withering, Wonder |

**Status: data only (ITM-018).** Rods can be bought and crafted, but `MetamagicRodActivation`, `MetamagicData.ApplyFromRod` and `RodDatabase.ResetDailyUses/ResetWeeklyUses` are called only from `Tests/Equipment/RodTests.cs` and `Tests/Magic/MetamagicSystemTests.cs`. The non-metamagic helpers (Immovable, Splendor, Security and others) only change counters and log. The weapon modes of Flailing and Lordly Might are tooltip text. Known data deviations: Immovable Rod CL 8 (DMG 10); Rod of Absorption can recharge because spending lowers the counter that the 50-level cap checks; `ApplyMultipleRods` allows several rods on one spell.

**Backlog for metamagic rods** (DMG ch.7 Rods; the plumbing in `MetamagicModifier` already skips the slot increase and feat check when `AppliedByRod` is set):

1. Offer held, applicable rods at cast time (spell level within the rod's maximum, uses left), apply through `MetamagicRodActivation.ApplyRodToSpell`, and spend a use only when the cast resolves.
2. Compute slot level with `MetamagicData.GetEffectiveSpellLevelWithRods`. `CastWizardSpellWithMetamagic` uses `GetEffectiveSpellLevel`, and the `MetamagicSystem` overloads that take `MetamagicData` drop `appliedByRod` (SPL-055).
3. Rules to keep: one rod per spell; the rod plus the caster's own feat is legal and only the feat raises the slot; a spontaneous caster still takes a full-round action unless the rod is Quicken; the rod must be held.
4. Reset rod uses on rest, and let the AI use rods (AI-001 covers NPC metamagic in general).

## 7. Staves

`StaffDatabase` defines 20 `StaffDefinition`s: 2 labelled Full (Fire, Healing), 13 Partial and 5 Stub (Life, Woodlands, Divination, Earth and Stone, Passage), with 18 stub spells. Against the DMG staff table (21 distinct staves in `TreasureData.Table7_25_*`, 29 rows across medium and major), Abjuration and Conjuration are missing, and Staff of the Magi (a DMG minor artifact) is added.

**Status: unobtainable (ITM-024).** No code creates an ItemData with `IsStaff = true`; the only writes are the clone copy and the expiry in `GameManager.ConvertStaffToMundane`. So the store has no staves, Craft Staff lists nothing, treasure staves become placeholders, and `TryUseStaff`, `StaffValidator` and `StaffSpellSelectionPanel` are unreachable. The designed rules are 50 charges, no recharge, UMD DC 20, save DC 10 + L + floor(L/2), and conversion to a mundane quarterstaff when empty. DMG ch.7 (Staffs) instead lets the wielder use their own caster level and ability modifier when higher. Related: ITM-026 (stub spells), ITM-027 (Staff of Power), ITM-053, ITM-062, ITM-063.

## 8. Wondrous items

`WondrousItemDatabase.Init` makes 180 registration calls: 170 real items and 10 "Test ... Item" entries (100 gp each, one per slot), which the store and crafting also list (ITM-014). The bands below use the price ranges of the DMG tables as encoded in `TreasureData`: minor (Table 7-27) runs 50-7,400 gp, medium (7-28) 7,500-27,500 gp and major (7-29) 28,000 gp and up. Those ranges do not overlap.

| Code slot | Minor | Medium | Major | Total |
|---|---|---|---|---|
| Slotless | 18 | 29 | 23 | 70 |
| Neck | 11 | 8 | 9 | 28 |
| Back | 4 | 7 | 9 | 20 |
| Torso | 4 | 3 | 5 | 12 |
| Wrists | 3 | 4 | 3 | 10 |
| Head | 3 | 3 | 3 | 9 |
| Feet | 4 | 3 | 1 | 8 |
| Waist | 2 | 2 | 2 | 6 |
| Hands | 3 | 1 | 1 | 5 |
| FaceEyes | 1 | 1 | 0 | 2 |
| **Total** | **53** | **61** | **56** | **170** |

DMG slot names map to code slots as follows: shoulders to `Back`, throat to `Neck`, arms to `Wrists`, face to `FaceEyes`, and body or robe to `Torso`. `Torso` is separate from the armor slot (ITM-011). Up to 10 slotless items can be equipped, with no one-per-Ioun-type rule.

**Wired passive effects** (`Inventory.ApplyAllWondrousItemBonuses`, highest value per bonus type): ability enhancement (Constitution feeds HP), natural armor, bracers armor (the higher of bracers and armor), insight AC, the all-saves resistance bonus (Cloak of Resistance, Scarab, maxed with the Ring of Resistance), the Stone of Good Luck's +1 luck bonus on saves (`WondrousLuckSaveBonus`, stacks with resistance; its bonus on ability and skill checks is not wired), land speed, darkvision, displacement miss chance, bow attack and damage (Bracers of Archery), skill competence by exact skill name, spell resistance (sticks after unequip, ITM-006), and poison and disease immunity. Containers only replace their weight with `WondrousApparentWeight`.

**Data only.** Activation is unreachable (ITM-019), and even when reached it only spends a use and logs (ITM-021). Boots of Speed haste, flight, levitation, spider climb, see invisibility, anti-flanking, web immunity, regeneration, +caster level, Robe of the Archmagi +4 saves, Robe of Stars, Monk's Belt monk levels, Amulet of Mighty Fists, summoning (Bag of Tricks, Elemental Gems, Figurines, Horn of Valhalla), Necklace of Fireballs and Beads of Force, Iron Bands, Cube of Force, Ioun absorption and storage, Pearl of Power recall, robe patches, Cubic Gate and planar items, trapping items (Iron Flask, Mirror of Life Trapping), Iron Cobra, Stone Horses, Apparatus of Kwalish and the Titan tools are all data. `World/PlanarTravelSystem.cs` and `World/CreatureTrapSystem.cs` are instantiated by `GameManager` at startup, but only tooltip and default-data helpers (plane names, default Cubic Gate sides) are called; no gameplay path uses them. `WondrousUsesPerDay = -1` means both "single use" and "unlimited", and the use check treats it as unlimited. Weekly and monthly uses never reset. See ITM-022 for the written-but-unread stat flags.

Other deviations: Circlet of Persuasion ("Charisma checks") and Boots of the Winterlands ("Survival (cold)") never match a skill name. Monk's Belt is in the Torso slot with a non-DMG +1 insight AC. Cloak of Displacement, major is always on (DMG: 15 rounds per day). Necklace of Fireballs bead sets and DCs differ from the DMG. Prices that differ from the DMG table include Bag of Tricks (tan) 16,000 (6,300), Robe of Blending 8,400 (30,000), and Necklace of Fireballs V and VII 5,850 and 8,700 (table 6,150 and 9,150; the code values appear to match the DMG item description, which disagrees with the table, not verified against the book). Mantle of Spell Resistance comes in five SR variants (DMG: one item, SR 21, 90,000 gp). Stone Horse (Griffon) is not a DMG item.

**Not in the catalog.** A row-by-row name match (Quiver of Ehlonna counted as the code's Efficient Quiver, Iron Bands of Bilarro as Iron Bands of Binding) finds no code item for 134 of the 286 DMG table rows: 42 of 89 minor, 41 of 98 medium and 51 of 99 major. The missing families include all consumable wondrous items (elixirs, dusts, salves, Universal Solvent, Sovereign Glue and the like); manuals and tomes (inherent bonuses, 30 rows); crystal balls; golem manuals; strands of prayer beads; horns other than the iron Horn of Valhalla, pipes and chimes; Horseshoes of Speed; Gloves of Arrow Snaring; Cloak of Etherealness; Bowl, Brazier and Censer of elementals; and carpets other than 10 × 10 ft. The DMG names and prices are in `TreasureData.Table7_27/28/29`, which is the in-repo reference to use before trusting a design doc or the factory.

## 9. Specific weapons, armor and shields

`SpecificItemDatabase` (Equipment/SpecificItems/) defines 60 items: 30 weapons, 4 ammunition, 14 armors and 12 shields. Allowing for naming differences, that covers all 50 distinct specific armor, shield and weapon names in `TreasureData` (Tables 7-7, 7-8 and 7-16; 73 rows). The other 10 entries are not in those tables. Five are DMG items the tables omit (Assassin's Dagger, Shifter's Sorrow, Caster's Shield, Lion's Shield, and Armor of Rage, a DMG cursed item); five are variants or additions (Animated Shield and its greater version, the greater Absorbing and Lion's Shields, Plate Armor of Etherealness). There are 28 `SpecificItemBehavior` subclasses in Equipment/Weapons and Equipment/Armor. Thirteen definitions are marked `HasCustomBehavior` but have no behavior class (Flame Tongue, Life-Drinker, Shifter's Sorrow, Screaming Bolt, Celestial Armor and 8 more).

**Status: unreachable (ITM-025).** `SpecificItemDatabase.CreateSpecificItem` has no callers, so none of these items exists in play, and the store's "Specific" filter is always empty. If they were created, only the attack-side hooks would run (`OnPreAttackRoll`, `OnDamageRoll`, `OnCriticalHit`, `OnHitApplied`, `OnKill`, and the defender's `OnAttackedBy` for body armor and a left-hand shield). `OnEquip`, `Activate`, `OnLongRest` and the passive-bonus hooks are never called, so behaviors that check `IsEquipped`/`Wielder` stay inert. Clones would share one behavior instance (ITM-031). Item-specific defects already tracked: ITM-015 (Banded Mail of Luck), ITM-028 (Sword of the Planes), ITM-057, CRE-005 (Lion's Shield).

## 10. Acquisition

### Store

`StoreInventory.InitializeStore` builds the catalog: 16 weapons plus the torch, 11 armors and 6 shields; +1 and +2 versions of the weapons, armors and shields (not the torch); 2 potions; 2 spell components; bolts and 2 ropes; then every generated scroll, potion and wand, and every ring, rod and wondrous item (test items included). There are no material, masterwork, special-ability, specific or staff items, and nothing above +2.

Pricing rules: buy at the listed `PriceGp`. Mundane prices are hard-coded, mostly to PHB ch.7 values; exceptions are the heavy mace at 8 gp (PHB 12 gp), the torch at 1 gp (PHB 1 cp) and 20 crossbow bolts for 1 gp (PHB 10 for 1 gp). +N prices use `GetEnhancedPriceGp` without the masterwork cost. Consumables use the DMG formulas in section 4. Rings, rods and wondrous items use the factory `BasePriceGp`. Sell price is 50% of `StoreInventory.ResolveBaseValue`, prorated by charges for wands. Known pricing bugs: ITM-003, ITM-007, ITM-037. ITM-037 matters beyond its Low severity: while building the catalog, `AddEnhancedVariants` and the string `Add()` overload write store prices into the shared `ItemDatabase` templates' `BasePriceGp`, so any pricing-rule change has to fix it first or template prices will keep depending on whether the store was initialized.

The store has no stock and no merchant cash. `StoreInventory.StoreItemEntry` holds only `ItemId`, `Category` and `PriceGp`; buying (`StoreUI`, `SpendGold` then `CreateItemInstance`) never removes or decrements the entry, and selling pays `AddGold` with no check against any store funds. So every item at every price is always available and the store buys anything at any value. The DMG (ch.5) ties availability and purchase limits to a community's gp limit; settlement gp limits or limited stock need a data-model change (a quantity or stock field on the entry and a merchant cash or gp-limit value on the store), not just new rules.

### Treasure

After combat, `GameManager.GeneratePostCombatTreasure` rolls DMG ch.3 Table 3-5 by encounter level through `DND35e.Treasure.TreasureGenerator`. Coins go to party gold. Gems and art get an Appraise check. Mundane and magic items roll on the DMG ch.7 tables in `TreasureData` (item type, armor and weapon bonus and abilities, specific items, potions, rings, rods, staves, scrolls, wands, wondrous items). `TreasureItemConverter` then turns every non-coin result into a value-only placeholder: a name, price, guessed type and slot, and an enhancement number. Nothing is looked up in `ItemDatabase` or the factories (ITM-016). So no usable magic item comes from treasure. Enemy equipment is real `ItemData` and can be looted, but NPC definitions equip only mundane gear (a few unknown item ids are skipped, CRE-052), upgraded at random to masterwork or a special material by CR. Apart from the Cure Light Wounds potions a few definitions carry (section 4), no NPC carries magic gear, so DMG ch.4 NPC gear value is not modelled. `MagicItemLootGenerator` is dead code (ITM-048).

### Crafting

The Crafting Workshop opens for the first party member with an item creation feat. Costs follow DMG ch.7 (Creating Magic Items): half the market price in gold (from the crafter's `ComponentGold`, ITM-029), 1/25 in XP and one day per 1,000 gp. Details are in the architecture doc.

| Feat | Lists | Works in practice |
|---|---|---|
| Scribe Scroll | the crafter's known spells (all spells in debug mode) | Yes, including metamagic scrolls |
| Brew Potion | the crafter's known spells of level 3 or lower | Creates a potion that cannot be drunk (ITM-002) |
| Craft Wand | known spells of level 4 or lower | Yes, including metamagic wands |
| Forge Ring | 54 catalog rings | Clones the catalog ring; no spell prerequisites (ITM-017) |
| Craft Rod | 33 catalog rods | Clones; produces an inert item (ITM-018) |
| Craft Wondrous Item | 180 catalog entries, test items included | Clones; no spell prerequisites |
| Craft Magic Arms and Armor | +1..+5 tiers for weapons, armor and shields (15 upgrades) | Never succeeds: the UI never sets an upgrade target. Weapon tiers also require the spell id `magic_weapon_greater`, which does not exist (the real id is `greater_magic_weapon`) |
| Craft Staff | nothing | No staff items exist (ITM-024) |

Special abilities, special materials, masterwork base items and the +10 cap are not craftable. The Spellcraft DC for a missing prerequisite spell is shown but never rolled (ITM-017).

## 11. Gaps and backlog toward DMG fidelity

Ordered by impact on the target loop: random encounter, then loot, then party management by PHB/DMG rules, with smarter enemies.

1. **Make treasure real (ITM-016).** Map generated results to `ItemDatabase` templates and the factories: potions, scrolls and wands by spell; rings, rods and wondrous items by name (reconcile `TreasureData` names with `RingNames`/`RodNames`/`WondrousItemNames`); +N arms and armor with `EnchantmentFactory` abilities; specific items through `SpecificItemDatabase`. Pass `monsterGearGP`. Without this, the between-battle loop has no magic-item income except the store.
2. **Make wired enchantments rules-correct.** Type the energy damage (CMB-011); check crit immunity on normal crits (CMB-064); fix the Fortification percentages, use the highest rather than the sum, and check it before Vorpal, burst dice and `OnCriticalHit` (CMB-020, ITM-067); Keen with Improved Critical (CMB-010); Bane +2 damage; Merciful nonlethal; Wounding Constitution damage (ITM-015); the alignment wielder penalty.
3. **Wire the high-value data-only abilities (ITM-023).** Speed, Ghost Touch (CMB-025), armor energy resistance, spell resistance and Invulnerability DR, skill bonuses, Returning and Defending. Fix the cost model for energy resistance, skill, Etherealness and Undead Controlling abilities (flat gp per DMG, outside the +10 cap), and make energy resistance tiers mutually exclusive.
4. **Item actions and action economy.** Add an "activate equipped item" action for rings and wondrous items (ITM-019), loading for spell storing and counterspell rings (ITM-020), and real effects behind the activations (ITM-021). Use the PHB action costs (ITM-005). Let NPCs use consumables and items (AI-009). That opens a large AI decision space (potions when wounded, wands, item spell-likes, rods), so plan for it in the AI design.
5. **Fix passive bugs that distort every fight.** Sticky SR and Freedom of Movement (ITM-006), ring energy resistance overwriting Resist Energy, Ring of Wizardry scope and Regeneration attachment, and wire or hide the unread flags (ITM-022).
6. **Rods (ITM-018).** Metamagic rods per section 6, then the remaining rods, then the 6 missing DMG rods.
7. **Crafting completeness.** Potions (ITM-002); a Craft Magic Arms and Armor flow with an upgrade target and the correct spell id; special abilities and materials as craftable options; spell prerequisites and the Spellcraft roll (ITM-017); metamagic cost and caster level (ITM-009); a single gold pool (ITM-029); and a crafter selector.
8. **Store and economy fidelity.** Stack selling and sell prices (ITM-003, ITM-007, ITM-037); include the masterwork cost in +N prices; fix the alchemical silver, darkwood and cold iron costs; fill the missing weapon base prices; sell masterwork and material items; and consider the DMG community-size gp limit and limited stock, which need a quantity/stock field on `StoreItemEntry` and a merchant cash or gp-limit value on the store (neither exists). ITM-037 blocks any pricing-rule work: fix it first so store prices stop leaking into the shared templates.
9. **Staves and specific items.** A `StaffFactory` (ITM-024) and the missing staff spells (ITM-026); register specific items and drive the behavior lifecycle hooks (ITM-025).
10. **Mundane rules and content.** Armor speed (ITM-010), robe slot (ITM-011), halberd (ITM-012), shield material tags, the light flail and war pick data, spiked armor, the missing simple, martial and exotic weapons, and PHB special substances (alchemist's fire, holy water, tanglefoot bag, antitoxin) as usable thrown or consumable items.
11. **Catalog breadth.** Missing rings (Three Wishes, Elemental Command, Friend Shield, Improved Climbing/Jumping/Swimming, standard and true major Spell Storing), consumable wondrous items, manuals and tomes, and the other missing wondrous rows. Remove or gate test items (ITM-014). Add tests: no test covers rings, wondrous items, enchantments, materials, consumable factories or the treasure converter.
