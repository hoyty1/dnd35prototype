> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-02.

# Items & economy

This part covers `Assets/Scripts/Equipment/**`, `Assets/Scripts/Crafting/`, `Assets/Scripts/TreasureGenerator/` and the GameManager partials that use them. Two points shape everything below:

- Items are data. Most item effects are applied by other systems: `CharacterController` (enchantment and specific-item combat hooks), `GameManager` (scroll, wand, staff and ring use), and `CharacterStats` (the fields that `Inventory.RecalculateStats` writes).
- The catalogs hold far more than gameplay can reach. Rods, staves, specific named items, ring and wondrous activation, and treasure magic items are mostly data-only. Check the reachability table in "Rings, rods, wondrous items, staves, specific items" before assuming an item does something.

Nothing in this area is saved. Items, gold, the stash and crafting time all reset when the session ends. Comments that mention "save compatibility" (for example the `[Obsolete]` string overloads and the `EquipSlot` numbering) refer to a save system that does not exist.

## Item model: ItemData, ItemDatabase, ids

**ItemData** (`Equipment/Items/ItemData.cs`, about 3,080 lines) is one `[Serializable]`, mutable class used for every item. It has 441 public instance fields; most are single-item payloads for rings, rods and wondrous items. It also holds pricing (`GetEnhancedPriceGp`), naming (`FullDisplayName`; `FullNameWithEnhancement` is an alias), `CanEquipIn`, `GetQualityColor` and a roughly 960-line tooltip builder (`GetStatSummary`, from ItemData.cs:2116).

An item's kind is recorded in two ways: the `ItemType` enum and a set of bool flags. The two can disagree. `ItemType` has only these values: Weapon, Armor, Shield, Consumable, Misc, Ammunition, Ring, Wondrous. There is no Rod or Staff value. Commit afd2df5 fixed a compile error caused by code that assumed an `ItemType.Rod` existed.

| Kind | `Type` | Flag and payload | `Slot` | Created by |
|---|---|---|---|---|
| Weapon, armor, shield | Weapon / Armor / Shield | (none) | EitherHand (gauntlets: Hands) / legacy `Armor` (aliased to ArmorRobe) / LeftHand | `ItemDatabase.Register*`, `ItemMaterialFactory`, `EnchantmentFactory` |
| Potion | Consumable | `IsPotion` | None | `PotionFactory`; 4 hand-registered in `ItemDatabase.RegisterConsumablesAndMisc`; `CraftingExecutor.CreatePotion` |
| Scroll | Consumable | `IsScroll`, `Scroll` (ScrollData) | None | `ScrollFactory`, `CraftingExecutor.CreateScroll` |
| Wand | Consumable | `IsWand`, `Wand` (WandData) | None | `WandFactory`, `CraftingExecutor.CreateWand` |
| Staff | (none) | `IsStaff`, `StaffId` | (none) | nothing; see Staves below |
| Ring | Ring | `IsRing`, `RingId` | EitherRing | `RingFactory` |
| Rod | **Wondrous** | `IsRod`, `RodId`; `IsWondrous` is **false** | EitherHand | `RodFactory.CreateBaseRod` |
| Wondrous | Wondrous | `IsWondrous`, `WondrousId` | body slot or Slotless | `WondrousItemFactory.CreateBaseWondrous` |
| DMG named item | per base item | `IsSpecificItem`, `SpecificItemBehavior` | per base item | `SpecificItemDatabase.CreateSpecificItem`, which has no callers |
| Treasure placeholder | from the rolled kind | `IsTreasureItem` | varies | `TreasureItemConverter` |

Pairs of accessors read different sources. `IsConsumable`, `IsRingItem` and `IsWondrousItem` test `Type`, while `IsRing` and `IsWondrous` are the flags; `IsRodItem` is only an alias for `IsRod`. Because rods have `IsWondrousItem == true` but `IsWondrous == false`, all code that keys on the `IsWondrous` flag skips rods: wondrous bonuses, wondrous hooks and quality color. Rods therefore show white.

**Enhancement** is stored twice: in `EnhancementBonus` and in the lowercase `enhancementBonus` (ItemData.cs:276, :289). `ResolveEnhancementBonus` (ItemData.cs:1520) takes the larger of the two, clamped to 0-5. If both are 0, it parses "+N" from `Name`, so renaming an item can change its stats. `ItemDatabase.Register` syncs the two fields and clamps both to 0-5. `EnchantmentFactory`, `CraftingExecutor` and `TreasureItemConverter` write both fields.

**EquipSlot** mixes legacy `Armor = 1`, the hand slots (2-4), the 3.5e body slots (5-18) and creature aliases `MainHand/OffHand/Ranged` (19-21). `CanEquipIn` accepts the aliases Armor<->ArmorRobe, EitherHand->Left/RightHand and EitherRing->Left/RightRing, and checks a creature alias target as the hand it stands for (`ItemData.ResolveHandAlias`: MainHand and Ranged the right hand, OffHand the left hand; ITM-004, fixed 2026-10-08). `SetEquipSlot` and `GetEquipped` resolve the aliases themselves, so `EquipFromInventory` and `Unequip` work with them too; `DirectEquip` equips the MainHand or OffHand hand, refuses the Ranged alias (where a carried ranged weapon goes is a loadout decision, and replacing the right hand would drop its weapon), returns whether it equipped the item and warns when it did not. NPC definitions in `Character/Creatures/NPCDatabase_*.cs` use the aliases in 58 of their 97 `EquipmentSlotPair` entries; `NPCDatabaseCustom.cs` (68 entries) uses none. `GameManager.InitializeNPCFromDefinition` passes the entries to `Inventory.EquipStartingLoadout`: MainHand and OffHand go to the hands; the first Ranged weapon is held (the melee weapon moving to the pack, and a shield too through the two-handed rule of `RecalculateStats`) when the AI runs the creature through the ranged routine (`AIService.RoutesToRangedTurn`: RangedKiter, or a profile whose `CombatStyle` is Ranged, unless DefensiveMelee), and otherwise carried in the pack (NPCs cannot draw it later, ITM-069); an item that cannot be equipped goes to the pack. Verified in Play mode on 2026-10-08 (`rules/npc-spawn-alignment-gear`, `NpcAlignmentGearTests`).

**ItemDatabase** (`Equipment/Items/ItemDatabase.cs`) is a static `Dictionary<string, ItemData>` with the default (case-sensitive) comparer. Use `Get`, `HasItem`, `AllItems` and `CloneItem` to read it; `Register` and `RegisterExternal` add to it. It returns **shared templates**. Mutating the result of `Get` changes the item for everyone, and `StoreInventory` already does this to `BasePriceGp` (see [Store, pricing and gold](#store-pricing-and-gold)). Always call `CloneItem` for an instance you will mutate or give to a character.

`CloneItem(string)` (ItemDatabase.cs:1700 to the end of the file) is a hand-written, field-by-field copy. **Every new ItemData field must be added there, or it is silently dropped on every clone.** At 0dd8e76, 437 of the 441 fields are copied. The four missing are the treasure fields `TrueValueGp`, `AppraisedValueGp`, `IsAppraised` and `IsTreasureItem`. Other behavior:

- Subclasses need explicit handling. `RopeItemData` is the only one today.
- `SpecificItemBehavior` is copied by reference, so clones share one stateful behavior object (ItemDatabase.cs:1739).
- Runtime counters are reset: rod and wondrous daily, weekly and monthly uses, absorbed levels and activation states.
- New `RingInstanceId` and `WondrousInstanceId` values are generated.
- `ItemBuilder.CopyBaseProperties` is a second, smaller copy list. `ItemBuilder.Build` runs it only when `FromBase` set a base id, and nothing calls `FromBase`.

`Get(string)` and `CloneItem(string)` are `[Obsolete]` in favor of the `ItemID` overloads. However, `DND35e.Identifiers.ItemID` (`Equipment/Items/ItemID.cs`) only covers potions (1000+), base gear (2000+) and 84 `PlusN` variants (7000+). Scrolls, wands, rings, rods, wondrous items and material variants are string-only, so the string overloads are used everywhere and produce CS0618 warnings. `ItemIDs.cs` has 90 string constants (base weapons, armor and shields plus the legacy potions, ammo, rope and spell components), and `Utilities/IdentifierExtensions.cs` maps between `ItemID` and strings. `ItemBuilder` is used only for `ItemBuilder.Weapon/Armor` inside `ItemDatabase`; its other builders have no callers.

Id schemes (snake_case, matched case-insensitively by Inventory stacking and by the store price lookup, but case-sensitively by `ItemDatabase` itself):

| Pattern | Source |
|---|---|
| `longsword`, `chain_shirt` ... | `ItemIDs` constants |
| `{id}_plus{N}` | `ItemDatabase.RegisterEnhancedEquipmentVariants`, `IdentifierExtensions` |
| `{id}_plus{N}_{ability...}[_{banetype}]` | `EnchantmentFactory` |
| `mw_{id}`; `adamantine_`, `mithral_`, `cold_iron_`, `silver_`, `darkwood_{id}` | `ItemMaterialFactory` (prefix from `MaterialProperties.GetMaterialPrefix`) |
| `scroll_{arcane\|divine}_{spellId}`, `potion_{spellId}`, `wand_{spellId}` | Scroll/Potion/WandFactory |
| `crafted_{scroll\|potion\|wand}_{spellId}_{guid}` | `CraftingExecutor`; not in ItemDatabase |
| `ring_of_*`, `rod_*`, wondrous ids | `RingNames`, `RodNames`, `WondrousItemNames`; `test_wondrous_{slot}` for test items |
| `treasure_{gem\|art\|mundane\|magic}_{n}_{rand}` | `TreasureItemConverter`; not in ItemDatabase |
| `specific_{SpecificItemType}` | `SpecificItemDatabase` (never runs) |

## Catalog initialization order

All catalogs are static classes with lazy `Init()` (or `Initialize()`) guarded by an `_initialized` flag. The order is set in `_Core/SceneBootstrap.cs`:

```
SceneBootstrap (SceneBootstrap.cs:1037-1080)
  RingDatabase.Init, WondrousItemDatabase.Init, RodDatabase.Init, StaffDatabase.Init
  ItemDatabase.Init()                              :1060
     PHB weapons/armor/shields, default prices, +1/+2 variants, legacy potions and gear
     ScrollFactory / PotionFactory / WandFactory   (each calls SpellDatabase.Init itself)
     ItemMaterialFactory.RegisterAllMaterialVariants
     EnchantmentProperties.Initialize; RegisterCommonEnchantedItems (16 items)
  RingDatabase.RegisterAllRingsInItemDatabase      :1061
  WondrousItemDatabase.RegisterAllInItemDatabase   :1062
  RodDatabase.RegisterAllInItemDatabase            :1063
  ...
  SpellDatabase.Init; CraftableItemRegistry.Init   :1079-1080
```

Rules that follow from this:

- After `ItemDatabase.Init()` alone, rings, wondrous items and rods are **not** in ItemDatabase. Tests or tools that call only `ItemDatabase.Init` will not find them. `StoreInventory.AddRings/Rods/WondrousItemsToStore` repeats the push as a fallback.
- Staves and specific items are never pushed into ItemDatabase.
- `CraftableItemRegistry.Init` reads rings from `RingDatabase.GetAllRings()`, but reads rods, wondrous items and staves from `ItemDatabase.AllItems`. If it runs before the push, those categories stay empty for the session, because `_initialized` blocks a second Init. `CraftableItemRegistry.Reset()` exists for tests.
- `ItemDatabase.Init` sets `_initialized = true` before it populates (ItemDatabase.cs:21-22). This stops factories that call `Get` from recursing. The cost is that an exception mid-Init leaves a half-built catalog for the session.
- Many classes call `ItemDatabase.Init()` defensively (class kits, `InventoryComponent`, `PartyStash`, `GameManager.TestConfigs`). That is harmless because Init is idempotent. The `RegisterAll*InItemDatabase` calls are made only by `SceneBootstrap` and by the `StoreInventory` fallback above.

## Inventory, equipment slots and stat recalculation

Three similarly named types are easy to confuse:

| Type | What it is | How to reach it |
|---|---|---|
| `Inventory` (`Equipment/Inventory/Inventory.cs`) | Plain `[Serializable]` class with slots, stacking and `RecalculateStats` | `actor.InventoryComp.CharacterInventory` or `actor.GetInventoryData()` |
| `InventoryComponent` (`Equipment/Inventory/InventoryComponent.cs`) | MonoBehaviour whose **field** `CharacterInventory` is the `Inventory`. Also holds hardcoded starting kits `SetupAldric`/`SetupLyra` | `actor.InventoryComp` |
| `CharacterInventory` (`Equipment/Inventory/CharacterInventory.cs`) | MonoBehaviour **adapter** added by `CharacterController.EnsureInventory`. Has disarm and sunder helpers, `GetAllItems` and `GetConsumableCount` | `actor.Inventory` (this returns the adapter, not an `Inventory`) |

**Slots.** `Inventory` has 12 body slots (Head, FaceEyes, Neck, Torso, ArmorRobe, Waist, Back, Wrists, Hands, LeftRing, RightRing, Feet) and 2 hand slots. `SlotlessItems` holds up to 10 items (`MaxSlotlessItems`). `GeneralSlots` is the backpack: it starts at 20 and grows by 20 when full. Wondrous robes use `Torso`, which is a separate slot from `ArmorRobe`. A character can therefore wear a robe and armor together, which deviates from the DMG body-slot rule.

**AddItem** (Inventory.cs:85) merges a stackable item into existing stacks with the same `Id` (case-insensitive) up to `MaxStackSize`. For a stackable item, any new stack is made from `ItemDatabase.CloneItem(item.Id)` (Inventory.cs:131), which discards per-instance state on the incoming item. It falls back to the item itself only when the Id is unknown. Non-stackable items (wands, gear) are stored as the passed instance. Other gaps:

- `RemoveItem` (:179) searches equipped and general slots but not `SlotlessItems`.
- `GetTotalCarriedWeightLbs` ignores `StackCount` and ammo `Quantity`.

**Equip flow.** The UI (`UI/Inventory/PreCombatInventoryUI.cs`, `InventoryUI.cs`) calls `EquipFromInventory`, `Unequip`, `DirectEquip` or the slotless methods. The body and hand slot methods run `SetEquipSlot` (Inventory.cs:385) and then `RecalculateStats`; `EquipSlotless`/`UnequipSlotless` edit `SlotlessItems` directly. `SetEquipSlot` calls `RingActivationManager.OnRingUnequipped/OnRingEquipped` only for rings with `HasActiveRingAbility`. It calls `WondrousItemActivation.OnWondrousEquipped`, which only sets an instance id and logs. For body slots the previous item's unequip hook is a no-op stub (Inventory.cs:428-434); `OnWondrousUnequipped` (also log-only) is called only from `UnequipSlotless`.

**RecalculateStats** (Inventory.cs:449) runs after every add, remove and equip. In order, it does the following:

1. Two-handed enforcement: the other hand is pushed back into the backpack.
2. `ArmorBonus` and `ShieldBonus`.
3. Max Dex, taking the most restrictive of armor and encumbrance.
4. ACP and ASF. These use `ItemData.Effective*`, which includes material effects.
5. `ApplyAdamantineArmorDR`.
6. `ResetRingBonuses`, then `ApplyRingBonuses` for each ring slot. The Ring of Force Shield's bonus does not stack with a physical shield; the higher one wins.
7. `ApplyAllWondrousItemBonuses`, which covers body slots and slotless items.
8. Weapon stats from the right hand, the left hand or a spiked gauntlet. If there is none, it falls back to the primary natural attack or 1d3 unarmed.
9. `OwnerCharacter.RefreshEquipmentTags()`.

**Bonus stacking.** Ring and wondrous bonuses are reset to 0 on each recalculation and reapplied with "highest wins per bonus type" (`Mathf.Max`). Boolean grants are OR'ed. Ring deflection has its own field, `CharacterStats.RingDeflectionBonus`; the AC formulas read `EffectiveDeflectionBonus`, the higher of it and the spell field `DeflectionBonus`, which StatusEffectManager sets to the highest applied spell deflection (ITM-001, fixed 2026-10-08; checked in Play mode by `rules/ring-deflection`). The Stone of Good Luck's save bonus goes to the luck field `WondrousLuckSaveBonus`, not the resistance field `WondrousSaveAllBonus`. A Protection from Evil ward adds only what exceeds the target's own deflection and resistance bonuses (`AlignmentProtectionRules.DeflectionAcIncrease`, `ResistanceSaveIncrease`). These writes still break the pattern; all are tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md):

- The Shield spell adds +4 into `ShieldBonus`, which every recalculation rebuilds from the held shield, so a recalculation during the spell erases it (ITM-073).
- `FreedomOfMovementActive` (Inventory.cs:986) is set by a ring and never cleared.
- Wondrous spell resistance raises `SpellResistance` with `Max` and is never reverted.

Several `Ring*`/`Wondrous*` flags on `CharacterStats` are written here but never read by gameplay: feather fall, water walking, sustenance, mind shielding, flight, spider climb, levitation, caster-level bonus, see invisible and others. Grep for a reader before relying on one.

**PartyStash** (`Equipment/Inventory/PartyStash.cs`) is a plain `List<ItemData>` with no stacking, owned by `Services/EconomyService.cs`. When it is locked, `AddItem` and `RemoveItem` fail. `GameManager` locks it when combat starts (`ForceStartEncounterFromPreCombat`, GameManager.cs:1611) and unlocks it in the pre-combat menus and during post-combat loot collection. `SeedDefaultItemsIfEmpty` adds starter gear, including 30 separate Cure Light Wounds potions.

## Consumables: potions, scrolls, wands, staves

| Factory (`Equipment/Inventory/`) | Spells | Price (DMG) | Stack | Notes |
|---|---|---|---|---|
| `ScrollFactory` | every non-placeholder spell on a class list (domain-only spells count as divine), arcane and/or divine copy | SL x CL x 25 (0-level: ceil, 13 gp) | 20 | DC defaults to 10 + SL (`ScrollData.Create`) |
| `PotionFactory` | SL <= 3, filtered by `IsEligibleForPotion` (not personal, not area damage) | SL x CL x 50 | 20 | Healing spells become `ConsumableEffect = HealHP` |
| `WandFactory` | SL <= 4 | SL x CL x 750 | no | 50 charges; DC 10 + SL + SL/2; healing wands use `HealHP` |

All three use the minimum caster level for the spell level and iterate `SpellDatabase.GetAllSpells()`, so their counts depend on the spell data. `TreasureData` scroll tables were filtered to implemented spells in 7509633.

**ScrollData and WandData** (`Equipment/Items/ScrollData.cs`, `WandData.cs`) are the canonical payloads (commits d80e7c1, 0982d08). They hold the spell id, caster level, base and effective level, baked save DC, heighten level, metamagic feats, the arcane flag and gold value. Every creator still fills the legacy flat fields on ItemData as well (`ScrollSpellLevel`, `ScrollSavedDC`, `WandSpellId`, `WandCasterLevel`, `CurrentCharges`/`MaxCharges`, `ConsumableSpellName` and others).

**Wand charges are stored twice**: `ItemData.CurrentCharges/MaxCharges` and `WandData.CurrentCharges/MaxCharges`. All three consume sites decrement both by hand (GameManager.cs:5624-5625, 6160-6161, 6291-6292). `WandValidator` checks only the legacy field, and the store sell price prefers the `WandData` values. If you add a charge write, update both. A wand at 0 charges stays in the inventory.

**Use pipeline.** Using any consumable costs a full-round action in combat:

```
InventoryUI / QuickItemUsePanel
 -> GameManager.TryUseConsumableFromInventory   (GameManager.cs:4335; rejects !IsConsumable)
      in combat: active PC, ChoosingAction, full-round action available
      -> ResolveConsumableUseProvocation        (:4479; AoO first unless IsWand)
 -> ApplyConsumableEffectAndConsume             (:5475) switch ConsumableEffect
      HealHP      roll healing; wands then validate (WandValidator) and spend a charge
      SpellEffect IsScroll -> TryUseScroll  (:5799, ScrollValidator: class list, ability, CL check, Magic domain, UMD 20+CL)
                  IsWand   -> TryUseWand    (:6193, WandValidator: class list, Magic domain, UMD 20)
                  IsStaff  -> TryUseStaff   (:6321)            unreachable, see Staves
                  IsRing / IsWondrous branches                 unreachable (Type is not Consumable)
                  otherwise (potions) -> TryApplySpellConsumableEffect (:5674)
      None        HealAmount fallback, else "has no implemented consumable effect yet"
 -> ConsumeOneFromStack (:5659)
```

`TryUseScroll` and `TryUseWand` send a spell into the normal targeting pipeline when combat is running and the spell needs a target. That covers effect types Damage, Summon, Escape, Dispel, Wall, Divination and Utility, and target types SingleEnemy, SingleAlly, Area and Touch. The path is `Initiate{Scroll,Wand}CastThroughPipeline`, then `BeginPendingSpellTargeting`, with `_pendingScrollCastActive`/`_pendingWandCastActive` set. The action and the scroll or charge are consumed only after the cast resolves: `ConsumePendingSpellSlot` (Spell/Resolution/GameManager.SpellCasting.cs:174-187) calls `ConsumeScrollAfterCast` or `ConsumeWandChargeAfterCast` in place of spending a spell slot.

Everything else goes through `TryApplySpellConsumableEffect`, which affects only the user. It handles healing, Mirror Image, and Buff/Debuff/Illusion/Control spells through `StatusEffectManager.AddEffect`. Any other effect returns "not supported for consumable use yet". `BuildConsumableSpellVariant` (GameManager.cs:6549) applies the stored metamagic with its own approximations. The spell pipeline itself is described in [Cast pipelines](spells.md#cast-pipelines).

Known deviations (details in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md)):

- On a healing wand (`HealHP`), the healing roll happens before `WandValidator` and the UMD check run. A failed check still heals and spends no charge.
- Save DC rules differ by source. Store scrolls use 10 + SL, wands use 10 + SL + SL/2, and crafted metamagic scrolls bake in the crafter's ability modifier.
- Crafted potions cannot be used (see [Crafting](#crafting)).

## Enchantments and materials

| File | Role |
|---|---|
| `Equipment/Items/ItemMaterial.cs` | `ItemMaterialType` (Standard, Adamantine, Mithral, ColdIron, AlchemicalSilver, Darkwood) and the per-item `ItemMaterial` (on `ItemData.Material`) |
| `Equipment/Materials/MaterialProperties.cs` | Single source of material rules: costs, modifiers, prefixes, `IsMaterialValidForItem`, `GetMasterworkCost` (300 weapon, 150 armor/shield) |
| `Equipment/Enchantments/ItemMaterialFactory.cs` | Registers `mw_*` and material variants during `ItemDatabase.Init`; `ApplyMaterial` mutates in place; `GetRandomMaterialWeapon/Armor` gives NPC gear by CR (`_Core/GameManager.NPCSetup.cs`) |
| `Equipment/Enchantments/EnchantmentType.cs`, `EnchantmentStats.cs`, `EnchantmentProperties.cs` | 73 special abilities (plus `None`) and their registry rows (bonus equivalent or flat cost, slot, incompatibilities) |
| `Equipment/Enchantments/EnchantmentFactory.cs` | `CreateEnchantedVariant`: validate, check incompatibilities, cap the total at +10, apply side effects, **register into ItemDatabase**. Used by `ItemDatabase.RegisterCommonEnchantedItems` (16 items) |
| `Equipment/Enchantments/EnchantmentEffects.cs` | Combat math. `AdvancedEnchantmentEffects` is a second class in the same file. Called only from `CharacterController` (14 call sites) |
| `Equipment/Enchantments/ItemEnchantmentData.cs` | Per-item ability list (on `ItemData.Enchantment`) |

Price for weapons is base + (effective bonus)^2 x 2000, and for armor and shields base + (effective bonus)^2 x 1000, plus flat-cost abilities. Effective bonus is the enhancement plus the abilities' bonus equivalents (`ItemData.GetEnhancedPriceGp`).

**Materials wired in play:**

- `CharacterController` applies the masterwork +1 attack and the silver -1 damage (CharacterController.cs:6618-6621), and DR bypass through `ItemData.GetBypassTags`.
- `Inventory` applies ACP, max Dex, ASF and weight through `ItemData.Effective*`, and adamantine armor DR.
- `CharacterStats` uses the mithral category shift only for armor proficiency and the Barbarian fast-movement check.

There is a data gap: base shields never set `ArmorMaterial`, so no mithral or darkwood shield variant can be created.

Reachability: `StoreInventory` never lists the `mw_*`/material variants or the 16 enchanted items from `RegisterCommonEnchantedItems`, so the store's "Special Properties" and "Special Material" sub-filters stay empty. Material items reach play only as NPC gear (`GetRandomMaterialWeapon/Armor`), which can be looted after combat. No code path found gives a player the 16 enchanted items (read from code, not verified in Play mode).

**Enchantments wired in play.** These run in `CharacterController` attack resolution:

- extra-dice abilities: Flaming, Frost, Shock, Corrosive and Merciful
- burst and Thundering crit dice
- Holy, Unholy, Axiomatic and Anarchic (damage plus alignment bypass)
- Bane, Vicious, Vorpal and Brilliant Energy
- Wounding, which only logs
- Fortification (armor and shield percentages summed)

Keen, Throwing and Distance act only at creation time, in `EnchantmentFactory.ApplyAbilitySideEffects`. The remaining roughly 49 abilities are data only, including Speed, Ghost Touch, armor energy resistance, SR and skill bonuses. Their helpers in `EnchantmentEffects` have no callers, so grep for a call site before assuming an ability works.

Several combat defects are tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md): enchantment energy damage is resolved as physical damage, normal crits do not check crit immunity, Fortification is checked after Vorpal and burst dice, and Keen stacks with Improved Critical. `MagicItemLootGenerator` (`Equipment/SpecificItems/`) also uses enchantments, but it is dead code.

## Rings, rods, wondrous items, staves, specific items

| System | Catalog | Obtainable in play | What works | What does not |
|---|---|---|---|---|
| Rings (`Equipment/Rings/`) | 54 variants: 37 passive, 9 active, 8 complex (`RingDatabase.Init`) | Store, crafting | Passive bonuses in `Inventory.ApplyRingBonuses`; Ring of Wizardry slots (`SpellcastingComponent.cs:1022`); Ring of Counterspells hook (`GameManager.SpellCasting.cs:2002`); Spell Turning pool and Ram charges on rest | Active abilities (`RingActivationManager.TryActivateRing`) are unreachable: the only entry is the consumable path, which rejects `Type == Ring`. Spell Storing/Counterspells cannot be loaded (`SpellStorageUI` is never opened). Ring of Regeneration never attaches `RegenerationEffect`, because it has no `RingAbilities` |
| Rods (`Equipment/Rods/`) | 33 (21 metamagic, 7 combat, 5 utility; comments saying 36 are wrong) | Store, crafting; held in a hand slot | Tooltip only | `MetamagicRodActivation` is called only by `Tests/Equipment/RodTests.cs`. No casting or combat code reads rod fields; only tooltips, the store and the crafting registry do. The spell-side hook `MetamagicData.ApplyFromRod` is also called only by tests. Rod daily uses are never reset (`RodDatabase.ResetDailyUses/ResetWeeklyUses` are called only by tests) |
| Wondrous (`Equipment/Wondrous/`) | 180 registration calls: about 170 real items plus 10 `TEST` items (WondrousItemDatabase.cs:266-277), which also appear in the store and crafting | Store, crafting | Passive bonuses in `Inventory.ApplyAllWondrousItemBonuses`; daily-use reset on rest | `WondrousItemActivation.TryActivate` is unreachable (`Type == Wondrous`). Even if reached it only spends a use and logs, except that Boots of Speed set haste flags (`CharacterStats.WondrousHasteActive`) that nothing reads. Boots of Speed haste, summons, Cubic Gate, Iron Cobra and similar items are data and tooltip only. `OnWeeklyReset` and `OnMonthlyReset` have no callers |
| Staves (`Equipment/Staves/`) | 20 `StaffDefinition`s (status labels: 2 Full, 13 Partial, 5 Stub) | No | `StaffValidator`, `StaffSpellSelectionPanel` and `TryUseStaff` exist | No code creates an ItemData with `IsStaff = true`. The only writes are the clone copy and the expiry (GameManager.cs:6513). Craft Staff lists nothing |
| Specific items (`Equipment/SpecificItems/`) | 60 definitions, 28 `SpecificItemBehavior` subclasses in `Equipment/Weapons` and `Equipment/Armor` | No | `CharacterController.cs:6687-7538` invokes `OnPreAttackRoll`, `OnDamageRoll`, `OnCriticalHit`, `OnHitApplied`, `OnKill` and defender `OnAttackedBy` | `CreateSpecificItem` has no callers. `OnEquip`, `Activate`, `OnLongRest` and other hooks are never called, so `IsEquipped`/`Wielder` guards stay false. The store "Specific" filter is always empty |

Rest resets happen in `GameManager.RestorePartyAfterCombat` (GameManager.cs:1032-1035), which calls `RingActivationManager.OnRest` and `WondrousItemActivation.OnRest`. Nothing resets rods, weekly or monthly wondrous uses, or `CraftingTimeTracker`.

Despite its name, `Equipment/SpellStorage/` holds the Ring of Spell Storing and Ring of Counterspells code. It has nothing to do with scrolls or wands, which live in `Equipment/Items` (data) and `Equipment/Inventory` (factories and validators).

## Store, pricing and gold

`StoreInventory` (`Equipment/Store/StoreInventory.cs`) is a singleton MonoBehaviour on the GameManager GameObject. `EconomyService.EnsureStoreInventoryInitialized` creates it. `Awake` calls `InitializeStore` (:49), which builds `_availableItems` (rows of `StoreItemEntry`: ItemId, Category, PriceGp) and `_priceLookup`:

- hardcoded mundane weapons, armor and shields with prices
- their +1/+2 variants under "Magic Weapons/Armor/Shields"
- 2 potions, spell components and gear
- then every generated scroll, potion and wand, and every ring, rod and wondrous item

`StoreUI` (`Equipment/Store/StoreUI.cs`, about 2,570 lines of code-built uGUI) is opened from the pre-combat hub through `GameManager.OpenStoreFromPreCombat` (GameManager.cs:1499).

```
Buy  StoreUI.BuyItem (:2093)
       CreateItemInstance(entry.ItemId) -> CloneItem
       GameManager.SpendGold(entry.PriceGp) -> PartyStash.AddItem   (refund if the stash is locked)
Sell StoreUI.BuildSellStacks (:1463)  stash, or the chosen character's GeneralSlots only
       grouped by FullNameWithEnhancement; UnitSellPrice = GetSellPrice(first item)
     StoreUI.SellStackItems (:2312)  remove N instances -> GameManager.AddGold(unit x N)
```

**Pricing.** The buy price is `StoreItemEntry.PriceGp`. The sell price is `StoreInventory.GetSellPrice` (:383), which is floor(`ResolveBaseValue` x 0.5). For wands that result is then scaled by current/max charges, preferring the `WandData` values (commit 6bfa259). `ResolveBaseValue` takes the first match in this order:

1. The appraised value, for appraised treasure items such as gems and art. They sell at 50% of it.
2. The listed store price for the item's `Id`.
3. `GetEnhancedPriceGp(BasePriceGp)`.
4. A flat fallback by `ItemType`.

Store quirks (tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md)):

- `Add` writes the store price into a template's `BasePriceGp` when it is 0 (:285), and `AddEnhancedStoreVariants` overwrites the +1/+2 templates' `BasePriceGp` (:165). Prices seen elsewhere can therefore depend on whether the store has been opened.
- A sell stack's quantity is the number of `ItemData` instances. A character-inventory stack (`StackCount` > 1) therefore sells as one unit, and the whole stack is removed.
- Stacks are keyed by display name, so wands with different charge counts share the first wand's price.
- Treasure magic weapons and armor already include their enhancement in `BasePriceGp`, and `GetEnhancedPriceGp` adds it again.
- `EconomyService` has its own `GetBuyPrice`, `GetSellPrice`, `BuyItem` and `SellItem` with different rules. `BuyItem` and `SellItem` have no callers. The two price methods are used only by `Tests/Services/EconomyServiceTests.cs`, so that test does not cover the real store.

**Gold pools.** There are two, and no code moves gold between them:

| Pool | Field | Used by |
|---|---|---|
| Party gold | `EconomyService.PartyGold`; `GameManager.PartyGold/SpendGold/AddGold` delegate to it; starts at 1000 | Store buy and sell, treasure coins |
| Per-character | `CharacterStats.ComponentGold` (default 1000, CharacterStats.cs:196) | Crafting (`SpendComponentGold`), costly spell components (`Spell/Casting/SpellComponentSystem.cs`) |

## Crafting

The crafting files are `Assets/Scripts/Crafting/` (10 files) and the UI in `UI/Crafting/CraftingWorkshopUI.cs`. The workshop is opened from the "Crafting Workshop" button in `UI/Encounter/PreCombatHubUI.cs`, which calls `GameManager.OpenCraftingWorkshopFromPreCombat` (GameManager.cs:1439). Only the **first** party member with any item creation feat can craft. If no one has one, nothing is shown to the player (TODO at GameManager.cs:1455).

```
CraftingWorkshopUI.Open(stats, spellcasting, Inventory)
  list: CraftableItemRegistry.GetItemsForFeat (rings, rods, wondrous, staves, +1..+5 arms/armor tiers)
        or Generate{Scroll,Potion,Wand}Definitions(known spells)   (debug mode: all spells)
  CraftingValidator.Validate   (:154) feat by name -> crafter CL -> cost -> CheckSpellSources
        (crafter, party, scroll substitute) -> ComponentGold -> XP floor -> upgrade target
  metamagic overlay for scrolls and wands (CraftingWorkshopUI.cs ~955-1050) recomputes level, DC and cost
  CraftingExecutor.Execute     SpendComponentGold, SpendXP (with rollback)
        -> ApplyEnhancementUpgrade | CreateScroll/CreatePotion/CreateWand | ItemDatabase.CloneItem(def.ItemId)
        -> crafter's Inventory.AddItem -> CraftingTimeTracker.AdvanceDays
```

Costs come from `CraftingCostCalculator.FromMarketPrice`: market/2 gp, market/25 XP, and max(1, market/1000) days. Gold comes from the crafter's `ComponentGold`, not party gold. `CraftingValidator.DebugMode` is a static property set from the workshop's debug toggle; it makes crafting free and instant, skips every check and shows all spells. The toggle state is a static field (`CraftingWorkshopUI._debugMode`) that is copied into `DebugMode` on every `Open`, so it persists across workshop opens.

What each feat produces in practice:

- **Scribe Scroll, Craft Wand:** work, including metamagic scrolls and wands with a baked DC.
- **Forge Ring, Craft Rod, Craft Wondrous Item:** produce catalog clones. There are no spell prerequisites, because `AddSpellPrereqsFromItemDescription` is an empty stub.
- **Brew Potion:** the potion is created, but `CreatePotion` never sets `ConsumableEffect`, so drinking it fails.
- **Craft Magic Arms and Armor:** never succeeds. All tiers are upgrades, and the UI never sets an upgrade target, so `Validate` stops at the upgrade-target check.
- **Craft Staff:** lists nothing.

Other gaps:

- The Spellcraft DC for a missing spell is displayed but never rolled.
- Scroll substitution charges gold but consumes no scroll.
- The metamagic overlay overwrites `GoldCost`, dropping the substitution cost.
- `CraftingTimeTracker.OnDaysAdvanced` has no subscribers.

All of these are tracked in [KNOWN_ISSUES.md](../KNOWN_ISSUES.md).

## Treasure generation and post-combat loot

```
GameManager.BeginPostCombatLootCollection (GameManager.LootCollection.cs)
  GeneratePostCombatTreasure (GameManager.TreasureGeneration.cs)
     EL = EncounterService.CalculateEncounterLevel(defeated enemies)
     DND35e.Treasure.TreasureGenerator.Generate(el)      coins, goods, items per DMG Table 3-5
        MagicItemGenerator.Generate(tier) -> MagicItemResult (name, type string, price, enhancement,
                                             ability names, scroll spells; no ItemData)
  coins -> AddGold (party gold, automatic)
  TreasureItemConverter.ConvertAll (:58)                  best party Appraise modifier
  GatherTreasureItemLootEntries + enemy equipment -> LootCollectionUI (UI/Inventory/LootCollectionUI.cs)
  ... later RestorePartyAfterCombat (ring and wondrous rest resets)
```

`TreasureGenerator.Generate` accepts a `monsterGearGP` deduction, but the caller never passes one. It uses unseeded `UnityEngine.Random`.

Converted treasure items are **value-only placeholders**. Each has a name, price, `Type`, `Slot` and an enhancement number. Weapons have no damage dice. Potions and scrolls have no spell or `ConsumableEffect`. Rings have `IsRing = false`. Wands, rods and staves become `ItemType.Wondrous` with no flags set. Their ids are not in ItemDatabase, so they are only useful for selling. Gems and art get an Appraise check (DC 12/15/20/25 by value); a failure skews the appraised value by 10-80%. Magic items are not appraised.

Dead code to ignore: `Equipment/SpecificItems/MagicItemLootGenerator.cs`, an older CR-budget generator, and `TreasureGenerator/TreasureUI.cs` with `GameManager.GenerateAndShowTreasure`. Neither has callers, and `LootCollectionUI` replaced the second.

## Tests that touch this area

The tests are static `RunAll()` harnesses run by hand (see [TESTING.md](../TESTING.md)). Coverage of this area:

- `Tests/Crafting/CraftingSystemTests.cs`: cost formulas, minimum caster levels, constants and `SpendXP`; no validator or executor.
- `Tests/Equipment/RodTests.cs`: rod data and `MetamagicRodActivation`.
- `Tests/Services/EconomyServiceTests.cs`: the unused EconomyService pricing.
- `Tests/Inventory/PostCombatLootCollectionTests.cs`: loot, stash and inventory transfer.
- `Tests/Character/EncumbranceTests.cs`, `Tests/Maneuvers/SunderInventoryRemovalTests.cs`: inventory weight and removal.

No test references the scroll, potion or wand factories, `ScrollData`/`WandData`, the validators, `StoreInventory`, rings, wondrous items, staves, enchantments, materials, the crafting validator and executor, or the treasure generator. Changes there need a manual Play-mode check. Item recipes (weapons, abilities, rings, rods, staves, wondrous items, consumables, store, crafting) are in [DEVELOPMENT_RECIPES.md](../DEVELOPMENT_RECIPES.md).
