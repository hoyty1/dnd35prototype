using System;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Static database of all items in the game.
/// Contains all weapons, armor, and shields from the D&D 3.5 Player's Handbook.
/// Call ItemDatabase.Init() once at startup, then use Get(id) to retrieve items.
/// </summary>
public static class ItemDatabase
{
    private static Dictionary<string, ItemData> _items = new Dictionary<string, ItemData>();
    private static bool _initialized = false;

    /// <summary>Get all registered items (for browsing/shops).</summary>
    public static IEnumerable<ItemData> AllItems => _items.Values;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _items.Clear();

        RegisterSimpleMeleeWeapons();
        RegisterSimpleRangedWeapons();
        RegisterMartialMeleeWeapons();
        RegisterMartialRangedWeapons();
        RegisterLightArmor();
        RegisterMediumArmor();
        RegisterHeavyArmor();
        RegisterShields();
        ApplyDefaultBasePrices();
        RegisterEnhancedEquipmentVariants();
        RegisterConsumablesAndMisc();
        ScrollFactory.RegisterAllScrolls();
        PotionFactory.RegisterAllPotions();
        WandFactory.RegisterAllWands();
    }

    // ============================================================
    //  SIMPLE MELEE WEAPONS (D&D 3.5 PHB Table 7-5)
    // ============================================================
    private static void RegisterSimpleMeleeWeapons()
    {
        // Unarmed Strike: 1d3, 20/×2, bludgeoning
        Register(new ItemData
        {
            Id = ItemIDs.UNARMED_STRIKE, Name = "Unarmed Strike", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Melee,
            Description = "A punch, kick, or other unarmed attack.",
            DamageDice = 3, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            DamageType = "bludgeoning",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 0f,
            IconChar = "\u270A", IconColor = new Color(0.9f, 0.8f, 0.7f)
        });

        // Gauntlet: 1d3, 20/×2, bludgeoning (equipped in Hands slot)
        Register(new ItemData
        {
            Id = ItemIDs.GAUNTLET, Name = "Gauntlet", Type = ItemType.Weapon,
            Slot = EquipSlot.Hands,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Melee,
            WeaponSize = WeaponSizeCategory.Light,
            IsLightWeapon = true,
            Description = "An armored glove worn in the hands slot. It allows unarmed strikes to deal lethal damage by default.",
            DamageDice = 3, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            DamageType = "bludgeoning",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 1f,
            IconChar = "\u270A", IconColor = new Color(0.6f, 0.6f, 0.7f)
        });

        // Dagger: 1d4, 19-20/×2, piercing or slashing, light
        Register(new ItemData
        {
            Id = ItemIDs.DAGGER, Name = "Dagger", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Melee,
            Description = "A small blade. Light and easy to conceal. Can be thrown.",
            DamageDice = 4, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            IsLightWeapon = true,
            DamageType = "piercing/slashing",
            DmgModType = DamageModifierType.Strength,
            IsThrown = true,
            RangeIncrement = 10,
            CritThreatMin = 19, CritMultiplier = 2,
            WeightLbs = 1f,
            IconChar = "\u2020", IconColor = new Color(0.8f, 0.8f, 0.7f)
        });

        // Mace, Light: 1d6, 20/×2, bludgeoning, light
        Register(new ItemData
        {
            Id = ItemIDs.MACE_LIGHT, Name = "Mace, Light", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Melee,
            Description = "A light bludgeoning weapon with a flanged metal head.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            IsLightWeapon = true,
            DamageType = "bludgeoning",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 4f,
            IconChar = "\u2692", IconColor = new Color(0.6f, 0.6f, 0.6f)
        });

        // Sickle: 1d6, 20/×2, slashing, light
        Register(new ItemData
        {
            Id = ItemIDs.SICKLE, Name = "Sickle", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Melee,
            Description = "A curved blade on a short handle. Favored by druids.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            IsLightWeapon = true,
            DamageType = "slashing",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 2f,
            IconChar = "\u262D", IconColor = new Color(0.5f, 0.6f, 0.4f)
        });

        // Club: 1d6, 20/×2, bludgeoning
        Register(new ItemData
        {
            Id = ItemIDs.CLUB, Name = "Club", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Melee,
            Description = "A stout piece of wood, simple but effective.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            DamageType = "bludgeoning",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 3f,
            IconChar = "\u2502", IconColor = new Color(0.5f, 0.4f, 0.2f)
        });

        // Mace, Heavy: 1d8, 20/×2, bludgeoning
        Register(new ItemData
        {
            Id = ItemIDs.MACE_HEAVY, Name = "Mace, Heavy", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Melee,
            Description = "A heavy bludgeoning weapon effective against armored foes.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            DamageType = "bludgeoning",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 8f,
            IconChar = "\u2692", IconColor = new Color(0.6f, 0.6f, 0.6f)
        });

        // Morningstar: 1d8, 20/×2, bludgeoning and piercing
        Register(new ItemData
        {
            Id = ItemIDs.MORNINGSTAR, Name = "Morningstar", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Melee,
            Description = "A spiked metal ball on the end of a handle. Deals bludgeoning and piercing.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            DamageType = "bludgeoning/piercing",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 6f,
            IconChar = "\u2692", IconColor = new Color(0.5f, 0.5f, 0.5f)
        });

        // Shortspear: 1d6, 20/×2, piercing
        Register(new ItemData
        {
            Id = ItemIDs.SHORTSPEAR, Name = "Shortspear", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Melee,
            Description = "A short thrusting spear. Can be thrown.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            DamageType = "piercing",
            DmgModType = DamageModifierType.Strength,
            IsThrown = true,
            RangeIncrement = 20,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 3f,
            IconChar = "\u2191", IconColor = new Color(0.6f, 0.5f, 0.3f)
        });

        // Quarterstaff: 1d6/1d6, 20/×2, bludgeoning, two-handed (double weapon)
        Register(new ItemData
        {
            Id = ItemIDs.QUARTERSTAFF, Name = "Quarterstaff", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Melee,
            Description = "A simple wooden staff. Reliable, versatile, and can be used as a double weapon.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            IsTwoHanded = true,
            DamageType = "bludgeoning",
            DmgModType = DamageModifierType.StrengthOneAndHalf,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 4f,
            IconChar = "\u2502", IconColor = new Color(0.6f, 0.5f, 0.3f)
        });

        // Spear: 1d8, 20/×3, piercing, two-handed (can be thrown)
        Register(new ItemData
        {
            Id = ItemIDs.SPEAR, Name = "Spear", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Melee,
            Description = "A long thrusting weapon with a pointed tip. Two-handed. Can be thrown.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            IsTwoHanded = true,
            DamageType = "piercing",
            DmgModType = DamageModifierType.StrengthOneAndHalf,
            IsThrown = true,
            RangeIncrement = 20,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 6f,
            IconChar = "\u2191", IconColor = new Color(0.6f, 0.6f, 0.5f)
        });

        // Longspear: 1d8, 20/×3, piercing, two-handed, reach (cannot attack adjacent)
        Register(new ItemData
        {
            Id = ItemIDs.LONGSPEAR, Name = "Longspear", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Melee,
            Description = "A long spear with 10-ft reach. Cannot attack adjacent creatures.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 2,
            IsTwoHanded = true, HasReach = true,
            ReachSquares = 2, CanAttackAdjacent = false, IsReachWeapon = true,
            DamageType = "piercing",
            DmgModType = DamageModifierType.StrengthOneAndHalf,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 9f,
            IconChar = "\u2191", IconColor = new Color(0.65f, 0.55f, 0.4f)
        });

        // Legacy alias: ItemIDs.MACE -> ItemIDs.MACE_HEAVY for backward compatibility
        Register(new ItemData
        {
            Id = ItemIDs.MACE, Name = "Mace, Heavy", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Melee,
            Description = "A heavy bludgeoning weapon effective against armored foes.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            DamageType = "bludgeoning",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 8f,
            IconChar = "\u2692", IconColor = new Color(0.6f, 0.6f, 0.6f)
        });
    }

    // ============================================================
    //  SIMPLE RANGED WEAPONS (D&D 3.5 PHB Table 7-5)
    // ============================================================
    private static void RegisterSimpleRangedWeapons()
    {
        // Crossbow, Light: 1d8, 19-20/×2, piercing, range 80 ft
        Register(new ItemData
        {
            Id = ItemIDs.CROSSBOW_LIGHT, Name = "Crossbow, Light", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Ranged,
            Description = "A light crossbow that fires bolts. Requires two hands to load.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 80,
            DamageType = "piercing",
            DmgModType = DamageModifierType.None,
            RangeIncrement = 80,
            RequiresAmmoType = AmmunitionType.Bolt,
            RequiresReload = true,
            IsLoaded = true,
            ReloadAction = ReloadActionType.MoveAction,
            CritThreatMin = 19, CritMultiplier = 2,
            WeightLbs = 4f,
            IconChar = "\u2732", IconColor = new Color(0.6f, 0.5f, 0.3f)
        });

        // Crossbow, Heavy: 1d10, 19-20/×2, piercing, range 120 ft
        Register(new ItemData
        {
            Id = ItemIDs.CROSSBOW_HEAVY, Name = "Crossbow, Heavy", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Ranged,
            Description = "A heavy crossbow with greater range and damage than its lighter cousin.",
            DamageDice = 10, DamageCount = 1, BonusDamage = 0, AttackRange = 120,
            DamageType = "piercing",
            DmgModType = DamageModifierType.None,
            RangeIncrement = 120,
            RequiresAmmoType = AmmunitionType.Bolt,
            RequiresReload = true,
            IsLoaded = true,
            ReloadAction = ReloadActionType.FullRound,
            CritThreatMin = 19, CritMultiplier = 2,
            WeightLbs = 8f,
            IconChar = "\u2732", IconColor = new Color(0.5f, 0.5f, 0.5f)
        });

        // --- Ammunition Items (D&D 3.5 PHB) ---
        // Arrows (20): 1 gp, 3 lbs per 20
        Register(new ItemData
        {
            Id = ItemIDs.AMMO_ARROW, Name = "Arrows (20)", Type = ItemType.Ammunition,
            Slot = EquipSlot.None,
            AmmoType = AmmunitionType.Arrow,
            Quantity = 20, MaxQuantity = 20,
            Description = "A quiver of 20 arrows for use with bows.",
            BasePriceGp = 1,
            WeightLbs = 3f,
            IconChar = "↑", IconColor = new Color(0.7f, 0.55f, 0.3f)
        });

        // Crossbow Bolts (20): 1 gp, 1 lb per 10
        Register(new ItemData
        {
            Id = ItemIDs.AMMO_BOLT, Name = "Crossbow Bolts (20)", Type = ItemType.Ammunition,
            Slot = EquipSlot.None,
            AmmoType = AmmunitionType.Bolt,
            Quantity = 20, MaxQuantity = 20,
            Description = "A case of 20 crossbow bolts.",
            BasePriceGp = 1,
            WeightLbs = 2f,
            IconChar = "•", IconColor = new Color(0.6f, 0.6f, 0.6f)
        });

        // Sling Bullets (10): 1 sp, 5 lbs per 10
        Register(new ItemData
        {
            Id = ItemIDs.AMMO_SLING_BULLET, Name = "Sling Bullets (10)", Type = ItemType.Ammunition,
            Slot = EquipSlot.None,
            AmmoType = AmmunitionType.SlingBullet,
            Quantity = 10, MaxQuantity = 10,
            Description = "A pouch of 10 sling bullets.",
            BasePriceGp = 0,
            WeightLbs = 5f,
            IconChar = "○", IconColor = new Color(0.5f, 0.5f, 0.5f)
        });

        // Legacy ammo bundle placeholder (kept for backward compatibility with existing saves/tests)
        Register(new ItemData
        {
            Id = ItemIDs.CROSSBOW_BOLTS_20, Name = "Crossbow Bolts (20) [Legacy]", Type = ItemType.Ammunition,
            Slot = EquipSlot.None,
            AmmoType = AmmunitionType.Bolt,
            Quantity = 20, MaxQuantity = 20,
            Description = "A bundle of 20 crossbow bolts.",
            BasePriceGp = 1,
            WeightLbs = 2f,
            IconChar = "\u2022", IconColor = new Color(0.7f, 0.65f, 0.45f)
        });

        // ── Spell Components ──
        Register(new ItemData
        {
            Id = ItemIDs.COMPONENT_DIAMOND_DUST, Name = "Diamond Dust (250 gp)", Type = ItemType.Consumable,
            Slot = EquipSlot.None,
            Description = "A pouch of finely ground diamond dust worth 250 gp. Required material component for Stoneskin and other spells.",
            BasePriceGp = 250,
            WeightLbs = 0.1f,
            IconChar = "💎", IconColor = new Color(0.7f, 0.85f, 1f)
        });

        // ── Spell Component Pouch (PHB p.130) ──
        // Contains all common material components that don't have a listed GP cost.
        // Reusable — NOT consumed when casting spells.
        Register(new ItemData
        {
            Id = ItemIDs.COMPONENT_SPELL_POUCH, Name = "Spell Component Pouch", Type = ItemType.Misc,
            Slot = EquipSlot.None,
            Description = "A small waterproof belt pouch containing all the common material components and focuses needed to cast spells — except those with a specific cost. Reusable; not consumed when casting.",
            BasePriceGp = 5,
            WeightLbs = 2f,
            IconChar = "🎒", IconColor = new Color(0.55f, 0.45f, 0.30f)
        });

        // Dart: 1d4, 20/×2, piercing, range 20 ft
        Register(new ItemData
        {
            Id = ItemIDs.DART, Name = "Dart", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Ranged,
            Description = "A small throwable missile with a weighted tip.",
            DamageDice = 4, DamageCount = 1, BonusDamage = 0, AttackRange = 20,
            DamageType = "piercing",
            DmgModType = DamageModifierType.Strength,
            IsThrown = true,
            RangeIncrement = 20,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 0.5f,
            IconChar = "\u2794", IconColor = new Color(0.7f, 0.7f, 0.5f)
        });

        // Javelin: 1d6, 20/×2, piercing, range 30 ft
        Register(new ItemData
        {
            Id = ItemIDs.JAVELIN, Name = "Javelin", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Ranged,
            Description = "A light throwing spear designed for ranged combat.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 30,
            DamageType = "piercing",
            DmgModType = DamageModifierType.Strength,
            IsThrown = true,
            RangeIncrement = 30,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 2f,
            IconChar = "\u2191", IconColor = new Color(0.6f, 0.5f, 0.3f)
        });

        // Sling: 1d4, 20/×2, bludgeoning, range 50 ft
        Register(new ItemData
        {
            Id = ItemIDs.SLING, Name = "Sling", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Ranged,
            Description = "A simple weapon that hurls stones at high velocity.",
            DamageDice = 4, DamageCount = 1, BonusDamage = 0, AttackRange = 50,
            DamageType = "bludgeoning",
            DmgModType = DamageModifierType.None,
            RequiresAmmoType = AmmunitionType.SlingBullet,
            RangeIncrement = 50,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 0f,
            IconChar = "\u223F", IconColor = new Color(0.6f, 0.5f, 0.3f)
        });
    }

    // ============================================================
    //  MARTIAL MELEE WEAPONS (D&D 3.5 PHB Table 7-5)
    // ============================================================
    private static void RegisterMartialMeleeWeapons()
    {
        // --- Light Martial Melee ---

        // Handaxe: 1d6, 20/×3, slashing, light
        Register(new ItemData
        {
            Id = ItemIDs.HANDAXE, Name = "Handaxe", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A small axe suitable for one-handed combat. Can be thrown.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            IsLightWeapon = true,
            DamageType = "slashing",
            DmgModType = DamageModifierType.Strength,
            IsThrown = true,
            RangeIncrement = 10,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 3f,
            IconChar = "\u2692", IconColor = new Color(0.7f, 0.5f, 0.3f)
        });

        // Shortsword: 1d6, 19-20/×2, piercing, light
        Register(new ItemData
        {
            Id = ItemIDs.SHORT_SWORD, Name = "Shortsword", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A light blade ideal for quick strikes and finesse.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            IsLightWeapon = true,
            DamageType = "piercing",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 19, CritMultiplier = 2,
            WeightLbs = 2f,
            IconChar = "\u2694", IconColor = new Color(0.6f, 0.8f, 0.6f)
        });

        // Flail, Light: 1d8, 20/×2, bludgeoning, light
        Register(new ItemData
        {
            Id = ItemIDs.FLAIL_LIGHT, Name = "Flail, Light", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A spiked ball on a chain. Difficult to parry.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            IsLightWeapon = true,
            DamageType = "bludgeoning",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 5f,
            IconChar = "\u2692", IconColor = new Color(0.5f, 0.5f, 0.5f)
        });

        // --- One-Handed Martial Melee ---

        // Longsword: 1d8, 19-20/×2, slashing
        Register(new ItemData
        {
            Id = ItemIDs.LONGSWORD, Name = "Longsword", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A versatile one-handed sword favored by fighters.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            DamageType = "slashing",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 19, CritMultiplier = 2,
            WeightLbs = 4f,
            IconChar = "\u2694", IconColor = new Color(0.7f, 0.7f, 0.8f)
        });

        // Rapier: 1d6, 18-20/×2, piercing
        Register(new ItemData
        {
            Id = ItemIDs.RAPIER, Name = "Rapier", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "An elegant thrusting sword with a wide critical range.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            DamageType = "piercing",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 18, CritMultiplier = 2,
            WeightLbs = 2f,
            IconChar = "\u2694", IconColor = new Color(0.8f, 0.8f, 0.9f)
        });

        // Scimitar: 1d6, 18-20/×2, slashing
        Register(new ItemData
        {
            Id = ItemIDs.SCIMITAR, Name = "Scimitar", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A curved blade that excels at slashing attacks. Wide crit range.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            DamageType = "slashing",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 18, CritMultiplier = 2,
            WeightLbs = 4f,
            IconChar = "\u2694", IconColor = new Color(0.8f, 0.7f, 0.5f)
        });

        // Battleaxe: 1d8, 20/×3, slashing
        Register(new ItemData
        {
            Id = ItemIDs.BATTLEAXE, Name = "Battleaxe", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A sturdy one-handed axe capable of devastating critical hits.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            DamageType = "slashing",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 6f,
            IconChar = "\u2692", IconColor = new Color(0.7f, 0.55f, 0.35f)
        });

        // Warhammer: 1d8, 20/×3, bludgeoning
        Register(new ItemData
        {
            Id = ItemIDs.WARHAMMER, Name = "Warhammer", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A heavy hammer designed for war. Devastating critical hits.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            DamageType = "bludgeoning",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 5f,
            IconChar = "\u2692", IconColor = new Color(0.6f, 0.6f, 0.7f)
        });

        // Trident: 1d8, 20/×2, piercing
        Register(new ItemData
        {
            Id = ItemIDs.TRIDENT, Name = "Trident", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A three-pronged spear. Can be thrown.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            DamageType = "piercing",
            DmgModType = DamageModifierType.Strength,
            IsThrown = true,
            RangeIncrement = 10,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 4f,
            IconChar = "\u2191", IconColor = new Color(0.5f, 0.6f, 0.7f)
        });

        // War Pick: 1d8, 20/×4, piercing
        Register(new ItemData
        {
            Id = ItemIDs.WARPICK, Name = "War Pick", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A pick designed for piercing armor. Extremely high crit multiplier.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            DamageType = "piercing",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 4,
            WeightLbs = 6f,
            IconChar = "\u2692", IconColor = new Color(0.5f, 0.5f, 0.5f)
        });

        // Lance: 1d8, 20/×3, piercing, reach
        Register(new ItemData
        {
            Id = ItemIDs.LANCE, Name = "Lance", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A long thrusting weapon with reach. Double damage on a mounted charge.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 2,
            HasReach = true,
            DamageType = "piercing",
            DmgModType = DamageModifierType.StrengthOneAndHalf,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 10f,
            IconChar = "\u2191", IconColor = new Color(0.6f, 0.6f, 0.5f)
        });

        // --- Two-Handed Martial Melee ---

        // Greatsword: 2d6, 19-20/×2, slashing, two-handed
        Register(new ItemData
        {
            Id = ItemIDs.GREATSWORD, Name = "Greatsword", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A massive two-handed sword dealing heavy damage.",
            DamageDice = 6, DamageCount = 2, BonusDamage = 0, AttackRange = 1,
            IsTwoHanded = true,
            DamageType = "slashing",
            DmgModType = DamageModifierType.StrengthOneAndHalf,
            CritThreatMin = 19, CritMultiplier = 2,
            WeightLbs = 8f,
            IconChar = "\u2694", IconColor = new Color(0.8f, 0.8f, 0.9f)
        });

        // Greataxe: 1d12, 20/×3, slashing, two-handed
        Register(new ItemData
        {
            Id = ItemIDs.GREATAXE, Name = "Greataxe", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A massive two-handed axe dealing devastating blows.",
            DamageDice = 12, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            IsTwoHanded = true,
            DamageType = "slashing",
            DmgModType = DamageModifierType.StrengthOneAndHalf,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 12f,
            IconChar = "\u2694", IconColor = new Color(0.9f, 0.5f, 0.3f)
        });

        // Greatclub: 1d10, 20/×2, bludgeoning, two-handed
        Register(new ItemData
        {
            Id = ItemIDs.GREATCLUB, Name = "Greatclub", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A massive wooden club requiring two hands.",
            DamageDice = 10, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            IsTwoHanded = true,
            DamageType = "bludgeoning",
            DmgModType = DamageModifierType.StrengthOneAndHalf,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 8f,
            IconChar = "\u2502", IconColor = new Color(0.5f, 0.4f, 0.2f)
        });

        // Falchion: 2d4, 18-20/×2, slashing, two-handed
        Register(new ItemData
        {
            Id = ItemIDs.FALCHION, Name = "Falchion", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A heavy curved sword with a wide cutting edge and excellent crit range.",
            DamageDice = 4, DamageCount = 2, BonusDamage = 0, AttackRange = 1,
            IsTwoHanded = true,
            DamageType = "slashing",
            DmgModType = DamageModifierType.StrengthOneAndHalf,
            CritThreatMin = 18, CritMultiplier = 2,
            WeightLbs = 8f,
            IconChar = "\u2694", IconColor = new Color(0.7f, 0.6f, 0.5f)
        });

        // Flail, Heavy: 1d10, 19-20/×2, bludgeoning, two-handed
        Register(new ItemData
        {
            Id = ItemIDs.FLAIL_HEAVY, Name = "Flail, Heavy", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A massive spiked ball on a heavy chain. Two-handed.",
            DamageDice = 10, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            IsTwoHanded = true,
            DamageType = "bludgeoning",
            DmgModType = DamageModifierType.StrengthOneAndHalf,
            CritThreatMin = 19, CritMultiplier = 2,
            WeightLbs = 10f,
            IconChar = "\u2692", IconColor = new Color(0.5f, 0.5f, 0.5f)
        });

        // Guisarme: 2d4, 20/×3, slashing, two-handed, reach (cannot attack adjacent)
        Register(new ItemData
        {
            Id = ItemIDs.GUISARME, Name = "Guisarme", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A hooked polearm with reach. Cannot attack adjacent creatures.",
            DamageDice = 4, DamageCount = 2, BonusDamage = 0, AttackRange = 2,
            IsTwoHanded = true, HasReach = true,
            ReachSquares = 2, CanAttackAdjacent = false, IsReachWeapon = true,
            DamageType = "slashing",
            DmgModType = DamageModifierType.StrengthOneAndHalf,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 12f,
            IconChar = "\u2191", IconColor = new Color(0.6f, 0.6f, 0.65f)
        });

        // Halberd: 1d10, 20/×3, piercing/slashing, two-handed, reach (cannot attack adjacent)
        Register(new ItemData
        {
            Id = ItemIDs.HALBERD, Name = "Halberd", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A polearm with axe and spear head. Reach weapon; cannot attack adjacent creatures.",
            DamageDice = 10, DamageCount = 1, BonusDamage = 0, AttackRange = 2,
            IsTwoHanded = true, HasReach = true,
            ReachSquares = 2, CanAttackAdjacent = false, IsReachWeapon = true,
            DamageType = "piercing/slashing",
            DmgModType = DamageModifierType.StrengthOneAndHalf,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 12f,
            IconChar = "\u2191", IconColor = new Color(0.65f, 0.6f, 0.55f)
        });

        // Ranseur: 2d4, 20/×3, piercing, two-handed, reach (cannot attack adjacent)
        Register(new ItemData
        {
            Id = ItemIDs.RANSEUR, Name = "Ranseur", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A long polearm with side spikes. Reach weapon; cannot attack adjacent creatures.",
            DamageDice = 4, DamageCount = 2, BonusDamage = 0, AttackRange = 2,
            IsTwoHanded = true, HasReach = true,
            ReachSquares = 2, CanAttackAdjacent = false, IsReachWeapon = true,
            DamageType = "piercing",
            DmgModType = DamageModifierType.StrengthOneAndHalf,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 12f,
            IconChar = "\u2191", IconColor = new Color(0.6f, 0.6f, 0.55f)
        });

        // Spiked Chain: 2d4, 20/×2, piercing, reach weapon that CAN attack adjacent
        Register(new ItemData
        {
            Id = ItemIDs.SPIKED_CHAIN, Name = "Spiked Chain", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Exotic, WeaponCat = WeaponCategory.Melee,
            Description = "A chain with spikes. Reach weapon that can attack adjacent and 10-ft targets.",
            DamageDice = 4, DamageCount = 2, BonusDamage = 0, AttackRange = 2,
            IsTwoHanded = true, HasReach = true,
            ReachSquares = 2, CanAttackAdjacent = true, IsReachWeapon = true,
            DamageType = "piercing",
            DmgModType = DamageModifierType.StrengthOneAndHalf,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 10f,
            IconChar = "\u223E", IconColor = new Color(0.6f, 0.6f, 0.6f)
        });

        // Whip: 1d3, 20/×2, slashing, 15-ft reach, nonlethal, cannot attack adjacent, cannot harm armored/naturally armored +1+
        Register(new ItemData
        {
            Id = ItemIDs.WHIP, Name = "Whip", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Exotic, WeaponCat = WeaponCategory.Melee,
            Description = "A flexible lash. 15-ft reach; cannot attack adjacent. Deals nonlethal damage and cannot harm armor/natural armor +1+.",
            DamageDice = 3, DamageCount = 1, BonusDamage = 0, AttackRange = 3,
            ReachSquares = 3, CanAttackAdjacent = false, IsReachWeapon = true,
            DealsNonlethalDamage = true, WhipLikeArmorRestriction = true,
            DamageType = "slashing",
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            WeightLbs = 2f,
            IconChar = "\u224B", IconColor = new Color(0.7f, 0.55f, 0.35f)
        });

        // Glaive: 1d10, 20/×3, slashing, two-handed, reach
        Register(new ItemData
        {
            Id = ItemIDs.GLAIVE, Name = "Glaive", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee,
            Description = "A long polearm with a curved blade. Has reach.",
            DamageDice = 10, DamageCount = 1, BonusDamage = 0, AttackRange = 2,
            IsTwoHanded = true, HasReach = true,
            DamageType = "slashing",
            DmgModType = DamageModifierType.StrengthOneAndHalf,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 10f,
            IconChar = "\u2191", IconColor = new Color(0.6f, 0.6f, 0.6f)
        });
    }

    // ============================================================
    //  MARTIAL RANGED WEAPONS (D&D 3.5 PHB Table 7-5)
    // ============================================================
    private static void RegisterMartialRangedWeapons()
    {
        // Longbow: 1d8, 20/×3, piercing, range 100 ft, two-handed
        Register(new ItemData
        {
            Id = ItemIDs.LONGBOW, Name = "Longbow", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Ranged,
            Description = "A tall bow with excellent range. Requires two hands.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 100,
            IsTwoHanded = true,
            DamageType = "piercing",
            DmgModType = DamageModifierType.None,
            RequiresAmmoType = AmmunitionType.Arrow,
            RangeIncrement = 100,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 3f,
            IconChar = "\u2191", IconColor = new Color(0.6f, 0.5f, 0.3f)
        });

        // Shortbow: 1d6, 20/×3, piercing, range 60 ft, two-handed
        Register(new ItemData
        {
            Id = ItemIDs.SHORTBOW, Name = "Shortbow", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Ranged,
            Description = "A compact bow favored by mounted archers. Requires two hands.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 60,
            IsTwoHanded = true,
            DamageType = "piercing",
            DmgModType = DamageModifierType.None,
            RequiresAmmoType = AmmunitionType.Arrow,
            RangeIncrement = 60,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 2f,
            IconChar = "\u2191", IconColor = new Color(0.5f, 0.5f, 0.3f)
        });

        // Composite Longbow: 1d8, 20/×3, piercing, range 110 ft, two-handed
        Register(new ItemData
        {
            Id = ItemIDs.COMPOSITE_LONGBOW, Name = "Composite Longbow", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Ranged,
            Description = "A powerful composite bow with superior range. Allows STR bonus to damage.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 110,
            IsTwoHanded = true,
            DamageType = "piercing",
            DmgModType = DamageModifierType.Composite,
            CompositeRating = 0,
            RequiresAmmoType = AmmunitionType.Arrow,
            RangeIncrement = 110,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 3f,
            IconChar = "\u2191", IconColor = new Color(0.7f, 0.5f, 0.3f)
        });

        // Composite Shortbow: 1d6, 20/×3, piercing, range 70 ft, two-handed
        Register(new ItemData
        {
            Id = ItemIDs.COMPOSITE_SHORTBOW, Name = "Composite Shortbow", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Ranged,
            Description = "A compact composite bow. Allows STR bonus to damage.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 70,
            IsTwoHanded = true,
            DamageType = "piercing",
            DmgModType = DamageModifierType.Composite,
            CompositeRating = 0,
            RequiresAmmoType = AmmunitionType.Arrow,
            RangeIncrement = 70,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 2f,
            IconChar = "\u2191", IconColor = new Color(0.6f, 0.5f, 0.3f)
        });

        // --- Composite Longbow Variants (with STR rating) ---
        for (int rating = 1; rating <= 4; rating++)
        {
            Register(new ItemData
            {
                Id = $"composite_longbow_{rating}", Name = $"Composite Longbow (+{rating})", Type = ItemType.Weapon,
                Slot = EquipSlot.EitherHand,
                Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Ranged,
                Description = $"A powerful composite longbow rated for up to +{rating} STR bonus to damage.",
                DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 110,
                IsTwoHanded = true,
                DamageType = "piercing",
                DmgModType = DamageModifierType.Composite,
                CompositeRating = rating,
                RequiresAmmoType = AmmunitionType.Arrow,
                RangeIncrement = 110,
                CritThreatMin = 20, CritMultiplier = 3,
                WeightLbs = 3f,
                IconChar = "\u2191", IconColor = new Color(0.7f, 0.5f, 0.3f)
            });
        }

        // --- Composite Shortbow Variants (with STR rating) ---
        for (int rating = 1; rating <= 4; rating++)
        {
            Register(new ItemData
            {
                Id = $"composite_shortbow_{rating}", Name = $"Composite Shortbow (+{rating})", Type = ItemType.Weapon,
                Slot = EquipSlot.EitherHand,
                Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Ranged,
                Description = $"A compact composite shortbow rated for up to +{rating} STR bonus to damage.",
                DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 70,
                IsTwoHanded = true,
                DamageType = "piercing",
                DmgModType = DamageModifierType.Composite,
                CompositeRating = rating,
                RequiresAmmoType = AmmunitionType.Arrow,
                RangeIncrement = 70,
                CritThreatMin = 20, CritMultiplier = 3,
                WeightLbs = 2f,
                IconChar = "\u2191", IconColor = new Color(0.6f, 0.5f, 0.3f)
            });
        }
    }

    // ============================================================
    //  LIGHT ARMOR (D&D 3.5 PHB Table 7-6)
    // ============================================================
    private static void RegisterLightArmor()
    {
        // Padded: +1 AC, Max Dex +8, Check 0, Spell Failure 5%, 10 lbs
        Register(new ItemData
        {
            Id = ItemIDs.PADDED_ARMOR, Name = "Padded Armor", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Light, ArmorMaterial = ArmorMaterialType.NonMetal,
            Description = "Layers of quilted cloth. The lightest armor available.",
            ArmorBonus = 1, MaxDexBonus = 8, ArmorCheckPenalty = 0,
            ArcaneSpellFailure = 5, WeightLbs = 10f,
            VisualTags = new HashSet<string> { "Light Armor", "Padded Armor" },
            IconChar = "\u26E8", IconColor = new Color(0.7f, 0.7f, 0.6f)
        });

        // Leather: +2 AC, Max Dex +6, Check 0, Spell Failure 10%, 15 lbs
        Register(new ItemData
        {
            Id = ItemIDs.LEATHER_ARMOR, Name = "Leather Armor", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Light, ArmorMaterial = ArmorMaterialType.NonMetal,
            Description = "Light armor made from hardened leather.",
            ArmorBonus = 2, MaxDexBonus = 6, ArmorCheckPenalty = 0,
            ArcaneSpellFailure = 10, WeightLbs = 15f,
            VisualTags = new HashSet<string> { "Light Armor", "Leather Armor" },
            IconChar = "\u26E8", IconColor = new Color(0.6f, 0.4f, 0.2f)
        });

        // Studded Leather: +3 AC, Max Dex +5, Check -1, Spell Failure 15%, 20 lbs
        Register(new ItemData
        {
            Id = ItemIDs.STUDDED_LEATHER, Name = "Studded Leather", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Light, ArmorMaterial = ArmorMaterialType.Mixed,
            Description = "Leather armor reinforced with metal studs.",
            ArmorBonus = 3, MaxDexBonus = 5, ArmorCheckPenalty = 1,
            ArcaneSpellFailure = 15, WeightLbs = 20f,
            VisualTags = new HashSet<string> { "Light Armor", "Studded Leather" },
            IconChar = "\u26E8", IconColor = new Color(0.5f, 0.35f, 0.2f)
        });

        // Chain Shirt: +4 AC, Max Dex +4, Check -2, Spell Failure 20%, 25 lbs
        Register(new ItemData
        {
            Id = ItemIDs.CHAIN_SHIRT, Name = "Chain Shirt", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Light, ArmorMaterial = ArmorMaterialType.Metal,
            Description = "A shirt of interlocking metal rings. Best light armor.",
            ArmorBonus = 4, MaxDexBonus = 4, ArmorCheckPenalty = 2,
            ArcaneSpellFailure = 20, WeightLbs = 25f,
            VisualTags = new HashSet<string> { "Light Armor", "Chain Shirt" },
            IconChar = "\u26E8", IconColor = new Color(0.6f, 0.6f, 0.7f)
        });
    }

    // ============================================================
    //  MEDIUM ARMOR (D&D 3.5 PHB Table 7-6)
    // ============================================================
    private static void RegisterMediumArmor()
    {
        // Hide: +3 AC, Max Dex +4, Check -3, Spell Failure 20%, 25 lbs
        Register(new ItemData
        {
            Id = ItemIDs.HIDE_ARMOR, Name = "Hide Armor", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Medium, ArmorMaterial = ArmorMaterialType.NonMetal,
            Description = "Thick furs and pelts of animals, crudely prepared.",
            ArmorBonus = 3, MaxDexBonus = 4, ArmorCheckPenalty = 3,
            ArcaneSpellFailure = 20, WeightLbs = 25f,
            VisualTags = new HashSet<string> { "Medium Armor", "Hide Armor" },
            IconChar = "\u26E8", IconColor = new Color(0.6f, 0.5f, 0.3f)
        });

        // Scale Mail: +4 AC, Max Dex +3, Check -4, Spell Failure 25%, 30 lbs
        Register(new ItemData
        {
            Id = ItemIDs.SCALE_MAIL, Name = "Scale Mail", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Medium, ArmorMaterial = ArmorMaterialType.Metal,
            Description = "Overlapping metal scales on a leather coat.",
            ArmorBonus = 4, MaxDexBonus = 3, ArmorCheckPenalty = 4,
            ArcaneSpellFailure = 25, WeightLbs = 30f,
            VisualTags = new HashSet<string> { "Medium Armor", "Scale Mail" },
            IconChar = "\u26E8", IconColor = new Color(0.5f, 0.6f, 0.5f)
        });

        // Chainmail: +5 AC, Max Dex +2, Check -5, Spell Failure 30%, 40 lbs
        Register(new ItemData
        {
            Id = ItemIDs.CHAINMAIL, Name = "Chainmail", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Medium, ArmorMaterial = ArmorMaterialType.Metal,
            Description = "A full suit of interlocking metal rings covering the body.",
            ArmorBonus = 5, MaxDexBonus = 2, ArmorCheckPenalty = 5,
            ArcaneSpellFailure = 30, WeightLbs = 40f,
            VisualTags = new HashSet<string> { "Medium Armor", "Chainmail" },
            IconChar = "\u26E8", IconColor = new Color(0.5f, 0.5f, 0.6f)
        });

        // Breastplate: +5 AC, Max Dex +3, Check -4, Spell Failure 25%, 30 lbs
        Register(new ItemData
        {
            Id = ItemIDs.BREASTPLATE, Name = "Breastplate", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Medium, ArmorMaterial = ArmorMaterialType.Metal,
            Description = "A fitted metal chest plate. Best medium armor for most characters.",
            ArmorBonus = 5, MaxDexBonus = 3, ArmorCheckPenalty = 4,
            ArcaneSpellFailure = 25, WeightLbs = 30f,
            VisualTags = new HashSet<string> { "Medium Armor", "Breastplate" },
            IconChar = "\u26E8", IconColor = new Color(0.7f, 0.7f, 0.75f)
        });
    }

    // ============================================================
    //  HEAVY ARMOR (D&D 3.5 PHB Table 7-6)
    // ============================================================
    private static void RegisterHeavyArmor()
    {
        // Splint Mail: +6 AC, Max Dex +0, Check -7, Spell Failure 40%, 45 lbs
        Register(new ItemData
        {
            Id = ItemIDs.SPLINT_MAIL, Name = "Splint Mail", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Heavy, ArmorMaterial = ArmorMaterialType.Metal,
            Description = "Strips of metal between layers of leather and chain.",
            ArmorBonus = 6, MaxDexBonus = 0, ArmorCheckPenalty = 7,
            ArcaneSpellFailure = 40, WeightLbs = 45f,
            VisualTags = new HashSet<string> { "Heavy Armor", "Splint Mail" },
            IconChar = "\u26E8", IconColor = new Color(0.5f, 0.5f, 0.55f)
        });

        // Banded Mail: +6 AC, Max Dex +1, Check -6, Spell Failure 35%, 35 lbs
        Register(new ItemData
        {
            Id = ItemIDs.BANDED_MAIL, Name = "Banded Mail", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Heavy, ArmorMaterial = ArmorMaterialType.Metal,
            Description = "Overlapping strips of metal banded over chain and leather.",
            ArmorBonus = 6, MaxDexBonus = 1, ArmorCheckPenalty = 6,
            ArcaneSpellFailure = 35, WeightLbs = 35f,
            VisualTags = new HashSet<string> { "Heavy Armor", "Banded Mail" },
            IconChar = "\u26E8", IconColor = new Color(0.55f, 0.55f, 0.6f)
        });

        // Half-Plate: +7 AC, Max Dex +0, Check -7, Spell Failure 40%, 50 lbs
        Register(new ItemData
        {
            Id = ItemIDs.HALF_PLATE, Name = "Half-Plate", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Heavy, ArmorMaterial = ArmorMaterialType.Metal,
            Description = "Plate armor covering most of the body with chain and leather.",
            ArmorBonus = 7, MaxDexBonus = 0, ArmorCheckPenalty = 7,
            ArcaneSpellFailure = 40, WeightLbs = 50f,
            VisualTags = new HashSet<string> { "Heavy Armor", "Half-Plate" },
            IconChar = "\u26E8", IconColor = new Color(0.6f, 0.6f, 0.65f)
        });

        // Full Plate: +8 AC, Max Dex +1, Check -6, Spell Failure 35%, 50 lbs
        Register(new ItemData
        {
            Id = ItemIDs.FULL_PLATE, Name = "Full Plate", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Heavy, ArmorMaterial = ArmorMaterialType.Metal,
            Description = "A complete suit of heavy plate armor. The finest protection available.",
            ArmorBonus = 8, MaxDexBonus = 1, ArmorCheckPenalty = 6,
            ArcaneSpellFailure = 35, WeightLbs = 50f,
            VisualTags = new HashSet<string> { "Heavy Armor", "Full Plate" },
            IconChar = "\u26E8", IconColor = new Color(0.7f, 0.7f, 0.75f)
        });
    }

    // ============================================================
    //  SHIELDS (D&D 3.5 PHB Table 7-6)
    // ============================================================
    private static void RegisterShields()
    {
        // Buckler: +1 AC, Max Dex -, Check -1, Spell Failure 5%, 5 lbs
        Register(new ItemData
        {
            Id = ItemIDs.BUCKLER, Name = "Buckler", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand, ArmorCat = ArmorCategory.Shield,
            Description = "A small round shield strapped to the forearm.",
            ShieldBonus = 1, MaxDexBonus = -1, ArmorCheckPenalty = 1,
            ArcaneSpellFailure = 5, WeightLbs = 5f,
            IconChar = "\u26E1", IconColor = new Color(0.5f, 0.5f, 0.4f)
        });

        // Shield, Light Wooden: +1 AC, bash 1d3 bludgeoning (martial, light off-hand)
        Register(new ItemData
        {
            Id = ItemIDs.SHIELD_LIGHT_WOODEN, Name = "Shield, Light Wooden", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand, ArmorCat = ArmorCategory.Shield,
            Description = "A light shield made of wood. Can be used for shield bashes.",
            ShieldBonus = 1, MaxDexBonus = -1, ArmorCheckPenalty = 1,
            ArcaneSpellFailure = 5, WeightLbs = 5f,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee, WeaponSize = WeaponSizeCategory.Light,
            DamageDice = 3, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            ReachSquares = 1, CanAttackAdjacent = true, IsLightWeapon = true,
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            DamageType = "bludgeoning",
            IconChar = "\u26E1", IconColor = new Color(0.6f, 0.5f, 0.3f)
        });

        // Shield, Light Steel: +1 AC, bash 1d3 bludgeoning (martial, light off-hand)
        Register(new ItemData
        {
            Id = ItemIDs.SHIELD_LIGHT_STEEL, Name = "Shield, Light Steel", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand, ArmorCat = ArmorCategory.Shield,
            Description = "A light shield made of steel. Can be used for shield bashes.",
            ShieldBonus = 1, MaxDexBonus = -1, ArmorCheckPenalty = 1,
            ArcaneSpellFailure = 5, WeightLbs = 6f,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee, WeaponSize = WeaponSizeCategory.Light,
            DamageDice = 3, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            ReachSquares = 1, CanAttackAdjacent = true, IsLightWeapon = true,
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            DamageType = "bludgeoning",
            IconChar = "\u26E1", IconColor = new Color(0.6f, 0.6f, 0.7f)
        });

        // Shield, Light Steel (Spiked): +1 AC, bash 1d4 piercing (martial, light off-hand)
        Register(new ItemData
        {
            Id = ItemIDs.SHIELD_LIGHT_STEEL_SPIKED, Name = "Shield, Light Steel (Spiked)", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand, ArmorCat = ArmorCategory.Shield,
            Description = "A light steel shield fitted with shield spikes. Shield spikes increase bash damage die by one step and change damage to piercing.",
            ShieldBonus = 1, MaxDexBonus = -1, ArmorCheckPenalty = 1,
            ArcaneSpellFailure = 5, WeightLbs = 11f,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee, WeaponSize = WeaponSizeCategory.Light,
            DamageDice = 4, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            ReachSquares = 1, CanAttackAdjacent = true, IsLightWeapon = true,
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            DamageType = "piercing",
            IconChar = "\u26E1", IconColor = new Color(0.65f, 0.65f, 0.75f)
        });

        // Shield, Heavy Wooden: +2 AC, bash 1d4 bludgeoning (martial)
        Register(new ItemData
        {
            Id = ItemIDs.SHIELD_HEAVY_WOODEN, Name = "Shield, Heavy Wooden", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand, ArmorCat = ArmorCategory.Shield,
            Description = "A heavy shield made of wood. Provides solid protection and can be used for shield bashes.",
            ShieldBonus = 2, MaxDexBonus = -1, ArmorCheckPenalty = 2,
            ArcaneSpellFailure = 15, WeightLbs = 10f,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee, WeaponSize = WeaponSizeCategory.OneHanded,
            DamageDice = 4, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            ReachSquares = 1, CanAttackAdjacent = true, IsLightWeapon = false,
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            DamageType = "bludgeoning",
            IconChar = "\u26E1", IconColor = new Color(0.6f, 0.5f, 0.3f)
        });

        // Shield, Heavy Steel: +2 AC, bash 1d4 bludgeoning (martial)
        Register(new ItemData
        {
            Id = ItemIDs.SHIELD_HEAVY_STEEL, Name = "Shield, Heavy Steel", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand, ArmorCat = ArmorCategory.Shield,
            Description = "A heavy shield made of steel. Standard protection for fighters, and usable for shield bashes.",
            ShieldBonus = 2, MaxDexBonus = -1, ArmorCheckPenalty = 2,
            ArcaneSpellFailure = 15, WeightLbs = 15f,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee, WeaponSize = WeaponSizeCategory.OneHanded,
            DamageDice = 4, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            ReachSquares = 1, CanAttackAdjacent = true, IsLightWeapon = false,
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            DamageType = "bludgeoning",
            IconChar = "\u26E1", IconColor = new Color(0.6f, 0.6f, 0.7f)
        });

        // Shield, Heavy Steel (Spiked): +2 AC, bash 1d6 piercing (martial)
        Register(new ItemData
        {
            Id = ItemIDs.SHIELD_HEAVY_STEEL_SPIKED, Name = "Shield, Heavy Steel (Spiked)", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand, ArmorCat = ArmorCategory.Shield,
            Description = "A heavy steel shield fitted with shield spikes. Shield spikes increase bash damage die by one step and change damage to piercing.",
            ShieldBonus = 2, MaxDexBonus = -1, ArmorCheckPenalty = 2,
            ArcaneSpellFailure = 15, WeightLbs = 20f,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee, WeaponSize = WeaponSizeCategory.OneHanded,
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            ReachSquares = 1, CanAttackAdjacent = true, IsLightWeapon = false,
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            DamageType = "piercing",
            IconChar = "\u26E1", IconColor = new Color(0.65f, 0.65f, 0.75f)
        });

        // Tower Shield: +4 AC, Max Dex -, Check -10, Spell Failure 50%, 45 lbs
        Register(new ItemData
        {
            Id = ItemIDs.TOWER_SHIELD, Name = "Tower Shield", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand, ArmorCat = ArmorCategory.Shield,
            Description = "A massive shield providing cover. Severe penalties to skills and attacks.",
            ShieldBonus = 4, MaxDexBonus = -1, ArmorCheckPenalty = 10,
            ArcaneSpellFailure = 50, WeightLbs = 45f,
            IconChar = "\u26E1", IconColor = new Color(0.5f, 0.5f, 0.5f)
        });

        // Legacy aliases for backward compatibility
        Register(new ItemData
        {
            Id = ItemIDs.HEAVY_SHIELD, Name = "Shield, Heavy Steel", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand, ArmorCat = ArmorCategory.Shield,
            Description = "A heavy shield made of steel. Standard protection for fighters, and usable for shield bashes.",
            ShieldBonus = 2, MaxDexBonus = -1, ArmorCheckPenalty = 2,
            ArcaneSpellFailure = 15, WeightLbs = 15f,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee, WeaponSize = WeaponSizeCategory.OneHanded,
            DamageDice = 4, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            ReachSquares = 1, CanAttackAdjacent = true, IsLightWeapon = false,
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            DamageType = "bludgeoning",
            IconChar = "\u26E1", IconColor = new Color(0.6f, 0.6f, 0.7f)
        });

        Register(new ItemData
        {
            Id = ItemIDs.LIGHT_SHIELD, Name = "Shield, Light Wooden", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand, ArmorCat = ArmorCategory.Shield,
            Description = "A light shield made of wood. Can be used for shield bashes.",
            ShieldBonus = 1, MaxDexBonus = -1, ArmorCheckPenalty = 1,
            ArcaneSpellFailure = 5, WeightLbs = 5f,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee, WeaponSize = WeaponSizeCategory.Light,
            DamageDice = 3, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            ReachSquares = 1, CanAttackAdjacent = true, IsLightWeapon = true,
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            DamageType = "bludgeoning",
            IconChar = "\u26E1", IconColor = new Color(0.6f, 0.5f, 0.3f)
        });
    }

    private static void ApplyDefaultBasePrices()
    {
        var basePrices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            // Weapons
            { ItemIDs.DAGGER, 2 },
            { ItemIDs.QUARTERSTAFF, 0 },
            { ItemIDs.CLUB, 0 },
            { ItemIDs.MACE_LIGHT, 5 },
            { ItemIDs.MACE_HEAVY, 8 },
            { ItemIDs.SPEAR, 2 },
            { ItemIDs.TORCH, 1 },
            { ItemIDs.LONGSWORD, 15 },
            { ItemIDs.SHORT_SWORD, 10 },
            { ItemIDs.GREATSWORD, 50 },
            { ItemIDs.BATTLEAXE, 10 },
            { ItemIDs.GREATAXE, 20 },
            { ItemIDs.WARHAMMER, 12 },
            { ItemIDs.RAPIER, 20 },
            { ItemIDs.SCIMITAR, 15 },
            { ItemIDs.FALCHION, 75 },
            { ItemIDs.FLAIL_HEAVY, 15 },
            { ItemIDs.LANCE, 10 },
            { ItemIDs.MORNINGSTAR, 8 },
            { ItemIDs.JAVELIN, 1 },
            { ItemIDs.SHORTBOW, 30 },
            { ItemIDs.LONGBOW, 75 },
            { ItemIDs.CROSSBOW_LIGHT, 35 },
            { ItemIDs.CROSSBOW_HEAVY, 50 },
            { ItemIDs.SLING, 0 },

            // Armor
            { ItemIDs.PADDED_ARMOR, 5 },
            { ItemIDs.LEATHER_ARMOR, 10 },
            { ItemIDs.STUDDED_LEATHER, 25 },
            { ItemIDs.CHAIN_SHIRT, 100 },
            { ItemIDs.HIDE_ARMOR, 15 },
            { ItemIDs.SCALE_MAIL, 50 },
            { ItemIDs.CHAINMAIL, 150 },
            { ItemIDs.BREASTPLATE, 200 },
            { ItemIDs.SPLINT_MAIL, 200 },
            { ItemIDs.BANDED_MAIL, 250 },
            { ItemIDs.HALF_PLATE, 600 },
            { ItemIDs.FULL_PLATE, 1500 },

            // Shields
            { ItemIDs.BUCKLER, 15 },
            { ItemIDs.SHIELD_LIGHT_WOODEN, 3 },
            { ItemIDs.SHIELD_LIGHT_STEEL, 9 },
            { ItemIDs.SHIELD_HEAVY_WOODEN, 7 },
            { ItemIDs.SHIELD_HEAVY_STEEL, 20 },
            { ItemIDs.TOWER_SHIELD, 30 },
            { ItemIDs.LIGHT_SHIELD, 3 },
            { ItemIDs.HEAVY_SHIELD, 20 }
        };

        foreach (KeyValuePair<string, int> kvp in basePrices)
        {
            if (_items.TryGetValue(kvp.Key, out ItemData item) && item != null)
                item.BasePriceGp = Mathf.Max(0, kvp.Value);
        }
    }

    private static void RegisterEnhancedEquipmentVariants()
    {
        var baseIds = new List<ItemID>();
        foreach (ItemID id in Enum.GetValues(typeof(ItemID)))
        {
            int numeric = (int)id;
            if (numeric < 2000 || numeric >= 3400)
                continue;

            string storageId = id.ToStorageString();
            if (string.IsNullOrWhiteSpace(storageId))
                continue;

            if (!_items.TryGetValue(storageId, out ItemData baseItem) || baseItem == null)
                continue;

            if (!baseItem.IsWeapon && !baseItem.IsArmor && !baseItem.IsShield)
                continue;

            baseIds.Add(id);
        }

        for (int i = 0; i < baseIds.Count; i++)
        {
            RegisterEnhancedVariant(baseIds[i], 1);
            RegisterEnhancedVariant(baseIds[i], 2);
        }
    }

    private static void RegisterEnhancedVariant(ItemID baseId, int bonus)
    {
        if (bonus < 1 || bonus > 5)
            return;

        string enhancedEnumName = $"{baseId}Plus{bonus}";
        if (!Enum.TryParse(enhancedEnumName, out ItemID enhancedId) || enhancedId == ItemID.None)
            return;

        string baseStorageId = baseId.ToStorageString();
        if (string.IsNullOrWhiteSpace(baseStorageId) || !_items.TryGetValue(baseStorageId, out ItemData baseItem) || baseItem == null)
            return;

        string enhancedStorageId = enhancedId.ToStorageString();
        if (string.IsNullOrWhiteSpace(enhancedStorageId) || _items.ContainsKey(enhancedStorageId))
            return;

        ItemData enhanced = CloneItem(baseStorageId);
        if (enhanced == null)
            return;

        enhanced.Id = enhancedStorageId;
        enhanced.Name = ItemData.FormatEnhancedName(baseItem.Name, bonus);
        enhanced.Description = string.IsNullOrWhiteSpace(baseItem.Description)
            ? $"Magically enhanced {ItemData.StripEnhancementNotation(baseItem.Name).ToLowerInvariant()} (+{bonus} enhancement)."
            : $"{baseItem.Description}\nMagical enhancement: +{bonus}.";

        enhanced.enhancementBonus = bonus;
        enhanced.EnhancementBonus = bonus;

        Register(enhanced);
    }

    // ============================================================
    //  CONSUMABLES & MISCELLANEOUS
    // ============================================================
    private static void RegisterConsumablesAndMisc()
    {
        RegisterSpellPotion(
            id: ItemIDs.POTION_CURE_LIGHT_WOUNDS,
            name: "Potion of Cure Light Wounds",
            spellName: "Cure Light Wounds",
            description: "Standard D&D 3.5e potion. Mimics Cure Light Wounds at minimum caster level (1d8+1).",
            minimumCasterLevel: 1,
            modifier: 1,
            iconChar: "\u2661",
            iconColor: new Color(1f, 0.3f, 0.3f));

        // Backward-compatible alias used by existing class loadouts.
        RegisterSpellPotion(
            id: ItemIDs.POTION_HEALING,
            name: "Potion of Cure Light Wounds",
            spellName: "Cure Light Wounds",
            description: "Standard D&D 3.5e potion. Mimics Cure Light Wounds at minimum caster level (1d8+1).",
            minimumCasterLevel: 1,
            modifier: 1,
            iconChar: "\u2661",
            iconColor: new Color(1f, 0.3f, 0.3f));

        RegisterSpellPotion(
            id: ItemIDs.POTION_SHIELD_OF_FAITH,
            name: "Potion of Shield of Faith",
            spellName: "Shield of Faith",
            description: "Grants a +2 deflection bonus to AC for 10 rounds (minimum caster level 1).",
            minimumCasterLevel: 1,
            modifier: 2,
            iconChar: "\u2726",
            iconColor: new Color(0.45f, 0.75f, 1f));

        Register(new ItemData
        {
            Id = ItemIDs.POTION_GREATER_HEALING, Name = "Potion of Greater Healing", Type = ItemType.Consumable,
            Slot = EquipSlot.None,
            Description = "A glowing red potion that restores 4d4+4 hit points.",
            ConsumableEffect = ConsumableEffectType.HealHP,
            HealDiceCount = 4,
            HealDiceSides = 4,
            HealBonus = 4,
            WeightLbs = 0.1f,
            IconChar = "\u2661", IconColor = new Color(1f, 0.1f, 0.5f),
            IsPotion = true,
            PotionSpellLevel = 2,
            IsStackable = true,
            MaxStackSize = 20,
            StackCount = 1
        });

        Register(new ItemData
        {
            Id = ItemIDs.SPIKED_GAUNTLET, Name = "Spiked Gauntlet", Type = ItemType.Weapon,
            Slot = EquipSlot.Hands,
            Description = "A hand-slot spiked gauntlet setup. The gauntlet itself cannot be disarmed.",
            Proficiency = WeaponProficiency.Simple,
            WeaponCat = WeaponCategory.Melee,
            WeaponSize = WeaponSizeCategory.Light,
            IsLightWeapon = true,
            DamageDice = 4,
            DamageCount = 1,
            BonusDamage = 0,
            DmgModType = DamageModifierType.Strength,
            AttackRange = 1,
            ReachSquares = 1,
            CanAttackAdjacent = true,
            CritThreatMin = 20,
            CritMultiplier = 2,
            DamageType = "piercing",
            WeightLbs = 1f,
            IconChar = "✹", IconColor = new Color(0.65f, 0.65f, 0.72f)
        });

        Register(new ItemData
        {
            Id = ItemIDs.LOCKED_GAUNTLET, Name = "Locked Gauntlet", Type = ItemType.Misc,
            Slot = EquipSlot.Hands,
            Description = "A locking hand harness that secures held weapons. Grants +10 to resist disarm attempts.",
            WeightLbs = 5f,
            IconChar = "⛓", IconColor = new Color(0.55f, 0.55f, 0.65f)
        });
        Register(new ItemData
        {
            Id = ItemIDs.TORCH, Name = "Torch", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple,
            WeaponCat = WeaponCategory.Melee,
            WeaponSize = WeaponSizeCategory.Light,
            IsLightWeapon = true,
            Description = "A wooden rod capped with tallow-soaked flax. Provides light and can be used as a weapon. Deals 1 point of fire damage (does not add Strength modifier to damage).",
            DamageDice = 1,
            DamageCount = 1,
            BonusDamage = 0,
            DmgModType = DamageModifierType.None,
            NoStrengthToDamage = true,
            SpecialProperties = "no_str_damage, provides_light, ignites_flammables",
            AttackRange = 1,
            ReachSquares = 1,
            CanAttackAdjacent = true,
            CritThreatMin = 20,
            CritMultiplier = 2,
            DamageType = "fire",
            WeightLbs = 1f,
            BasePriceGp = 1,
            IconChar = "\u2600", IconColor = new Color(1f, 0.8f, 0.2f)
        });

        Register(new RopeItemData
        {
            Id = ItemIDs.ROPE_HEMP, Name = "Hemp Rope (50 ft)", Type = ItemType.Misc,
            Slot = EquipSlot.None,
            Description = "A 50-foot coil of hemp rope. Break DC 24.",
            WeightLbs = 10f,
            BreakDC = 24,
            LengthFeet = 50,
            IconChar = "\u221E", IconColor = new Color(0.7f, 0.6f, 0.4f)
        });

        Register(new RopeItemData
        {
            Id = ItemIDs.ROPE_SILK, Name = "Silk Rope (50 ft)", Type = ItemType.Misc,
            Slot = EquipSlot.None,
            Description = "A 50-foot coil of silk rope. Break DC 23.",
            WeightLbs = 5f,
            BreakDC = 23,
            LengthFeet = 50,
            IconChar = "\u221E", IconColor = new Color(0.82f, 0.74f, 0.58f)
        });

        // Backward-compatible alias for older references.
        Register(new RopeItemData
        {
            Id = ItemIDs.ROPE, Name = "Hemp Rope (50 ft)", Type = ItemType.Misc,
            Slot = EquipSlot.None,
            Description = "A 50-foot coil of hemp rope. Break DC 24.",
            WeightLbs = 10f,
            BreakDC = 24,
            LengthFeet = 50,
            IconChar = "\u221E", IconColor = new Color(0.7f, 0.6f, 0.4f)
        });
    }

    /// <summary>
    /// Helper for registering potions/oils that emulate a spell at a specific caster level.
    /// Keeps potion definitions compact and extensible.
    /// </summary>
    private static void RegisterSpellPotion(
        string id,
        string name,
        string spellName,
        string description,
        int minimumCasterLevel,
        int modifier,
        string iconChar,
        Color iconColor)
    {
        Register(new ItemData
        {
            Id = id,
            Name = name,
            Type = ItemType.Consumable,
            Slot = EquipSlot.None,
            Description = description,
            ConsumableEffect = ConsumableEffectType.SpellEffect,
            ConsumableSpellName = spellName,
            ConsumableMinimumCasterLevel = Mathf.Max(1, minimumCasterLevel),
            ConsumableModifier = modifier,
            WeightLbs = 0.1f,
            IconChar = iconChar,
            IconColor = iconColor,
            IsPotion = true,
            PotionSpellLevel = 1, // Legacy potions default to spell level 1
            IsStackable = true,
            MaxStackSize = 20,
            StackCount = 1
        });
    }

    // ============================================================
    //  SCROLL REGISTRATION (called by ScrollFactory)
    // ============================================================

    /// <summary>
    /// Register a scroll item generated by ScrollFactory. Public so ScrollFactory can call it.
    /// </summary>
    public static void RegisterScrollItem(ItemData scrollItem)
    {
        if (scrollItem == null || string.IsNullOrWhiteSpace(scrollItem.Id))
            return;
        Register(scrollItem);
    }

    // ============================================================
    //  POTION REGISTRATION (called by PotionFactory)
    // ============================================================

    /// <summary>
    /// Register a potion item generated by PotionFactory. Public so PotionFactory can call it.
    /// </summary>
    public static void RegisterPotionItem(ItemData potionItem)
    {
        if (potionItem == null || string.IsNullOrWhiteSpace(potionItem.Id))
            return;
        Register(potionItem);
    }

    // ============================================================
    //  WAND REGISTRATION (called by WandFactory)
    // ============================================================

    /// <summary>
    /// Register a wand item generated by WandFactory. Public so WandFactory can call it.
    /// </summary>
    public static void RegisterWandItem(ItemData wandItem)
    {
        if (wandItem == null || string.IsNullOrWhiteSpace(wandItem.Id))
            return;
        Register(wandItem);
    }

    // ============================================================
    //  UTILITY METHODS
    // ============================================================

    private static void Register(ItemData item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Id))
            return;

        if (item.enhancementBonus <= 0 && item.EnhancementBonus > 0)
            item.enhancementBonus = item.EnhancementBonus;
        else if (item.EnhancementBonus <= 0 && item.enhancementBonus > 0)
            item.EnhancementBonus = item.enhancementBonus;

        item.enhancementBonus = Mathf.Clamp(item.enhancementBonus, 0, 5);
        item.EnhancementBonus = Mathf.Clamp(item.EnhancementBonus, 0, 5);

        ApplyWeaponSizeDefaults(item);
        ApplyReachDefaults(item);
        _items[item.Id] = item;
    }

    /// <summary>
    /// Ensures every registered weapon has an explicit D&D 3.5 size/handedness category.
    /// Also keeps legacy IsLightWeapon/IsTwoHanded flags synchronized for older systems.
    /// </summary>
    private static void ApplyWeaponSizeDefaults(ItemData item)
    {
        if (item == null || item.Type != ItemType.Weapon)
            return;

        if (item.WeaponSize == WeaponSizeCategory.None)
        {
            string id = (item.Id ?? string.Empty).ToLowerInvariant();

            if (id == ItemIDs.UNARMED_STRIKE)
            {
                item.WeaponSize = WeaponSizeCategory.Light;
            }
            else if (item.IsTwoHanded || item.DmgModType == DamageModifierType.StrengthOneAndHalf)
            {
                item.WeaponSize = WeaponSizeCategory.TwoHanded;
            }
            else if (item.IsLightWeapon)
            {
                item.WeaponSize = WeaponSizeCategory.Light;
            }
            else if (id.Contains("crossbow")
                     || id.Contains(ItemIDs.LONGBOW)
                     || id.Contains(ItemIDs.SHORTBOW)
                     || id.Contains(ItemIDs.COMPOSITE_LONGBOW)
                     || id.Contains(ItemIDs.COMPOSITE_SHORTBOW))
            {
                item.WeaponSize = WeaponSizeCategory.TwoHanded;
            }
            else
            {
                item.WeaponSize = WeaponSizeCategory.OneHanded;
            }
        }

        item.IsLightWeapon = item.WeaponSize == WeaponSizeCategory.Light;
        item.IsTwoHanded = item.WeaponSize == WeaponSizeCategory.TwoHanded;
    }

    /// <summary>
    /// Normalize D&D 3.5 reach semantics for melee weapons.
    /// ReachSquares is the max melee reach in squares (1=5ft, 2=10ft, 3=15ft).
    /// CanAttackAdjacent controls whether distance-1 attacks/threat are allowed.
    /// </summary>
    private static void ApplyReachDefaults(ItemData item)
    {
        if (item == null || item.Type != ItemType.Weapon || item.WeaponCat != WeaponCategory.Melee)
            return;

        int normalizedReach = item.ReachSquares > 0 ? item.ReachSquares : Mathf.Max(1, item.AttackRange);
        item.ReachSquares = Mathf.Max(1, normalizedReach);

        // If flagged as legacy reach or has >1 reach, mark as reach weapon.
        item.IsReachWeapon = item.IsReachWeapon || item.HasReach || item.ReachSquares > 1;

        // D&D baseline: melee weapons can attack adjacent unless they are reach-only.
        if (item.IsReachWeapon && !item.CanAttackAdjacent)
        {
            // Reach-only by default (longspear/glaive/halberd etc.).
            item.CanAttackAdjacent = false;
        }
        else if (!item.IsReachWeapon)
        {
            item.CanAttackAdjacent = true;
        }

        // Keep AttackRange synced as the max melee reach used by legacy systems.
        item.AttackRange = item.ReachSquares;
    }

    /// <summary>Get an item by enum ID. Returns null if not found.</summary>
    public static ItemData Get(ItemID id)
    {
        return Get(id.ToStorageString());
    }

    /// <summary>Check whether an item exists by enum ID.</summary>
    public static bool HasItem(ItemID id)
    {
        string storageId = id.ToStorageString();
        if (string.IsNullOrWhiteSpace(storageId))
            return false;

        if (!_initialized) Init();
        return _items.ContainsKey(storageId);
    }

    /// <summary>Check if an item exists by string ID.</summary>
    public static bool HasItem(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (!_initialized) Init();
        return _items.ContainsKey(id);
    }

    /// <summary>Get an item by string ID. Returns null if not found.</summary>
    [System.Obsolete("Prefer Get(ItemID) for compile-time type safety. String overload is kept for save compatibility.", false)]
    public static ItemData Get(string id)
    {
        if (!_initialized) Init();
        return _items.TryGetValue(id, out var item) ? item : null;
    }

    /// <summary>Create a copy of an item by enum ID (since items are reference types).</summary>
    public static ItemData CloneItem(ItemID id)
    {
        return CloneItem(id.ToStorageString());
    }

    /// <summary>Create a copy of an item by string ID (since items are reference types).</summary>
    [System.Obsolete("Prefer CloneItem(ItemID) for compile-time type safety. String overload is kept for save compatibility.", false)]
    public static ItemData CloneItem(string id)
    {
        var src = Get(id);
        if (src == null) return null;

        ItemData clone = src is RopeItemData ? new RopeItemData() : new ItemData();

        clone.Id = src.Id;
        clone.Name = src.Name;
        clone.Description = src.Description;
        clone.Type = src.Type;
        clone.Slot = src.Slot;

        // Weapon properties
        clone.Proficiency = src.Proficiency;
        clone.WeaponCat = src.WeaponCat;
        clone.WeaponSize = src.WeaponSize;
        clone.DamageDice = src.DamageDice;
        clone.DamageCount = src.DamageCount;
        clone.BonusDamage = src.BonusDamage;
        clone.DesignedForSize = src.DesignedForSize;
        clone.AttackRange = src.AttackRange;
        clone.IsLightWeapon = src.IsLightWeapon;
        clone.IsTwoHanded = src.IsTwoHanded;
        clone.HasReach = src.HasReach;
        clone.ReachSquares = src.ReachSquares;
        clone.CanAttackAdjacent = src.CanAttackAdjacent;
        clone.IsReachWeapon = src.IsReachWeapon;
        clone.DealsNonlethalDamage = src.DealsNonlethalDamage;
        clone.WhipLikeArmorRestriction = src.WhipLikeArmorRestriction;
        clone.DamageType = src.DamageType;
        clone.NoStrengthToDamage = src.NoStrengthToDamage;
        clone.SpecialProperties = src.SpecialProperties;
        clone.CountsAsMagicForBypass = src.CountsAsMagicForBypass;
        clone.IsSilvered = src.IsSilvered;
        clone.IsColdIron = src.IsColdIron;
        clone.IsAdamantine = src.IsAdamantine;
        clone.IsAlignedGood = src.IsAlignedGood;
        clone.IsAlignedEvil = src.IsAlignedEvil;
        clone.IsAlignedLawful = src.IsAlignedLawful;
        clone.IsAlignedChaotic = src.IsAlignedChaotic;
        clone.DmgModType = src.DmgModType;
        clone.CompositeRating = src.CompositeRating;
        clone.IsThrown = src.IsThrown;
        clone.RangeIncrement = src.RangeIncrement;
        clone.RequiresReload = src.RequiresReload;
        clone.IsLoaded = src.IsLoaded;
        clone.ReloadAction = src.ReloadAction;
        clone.RequiresAmmoType = src.RequiresAmmoType;
        clone.AmmoType = src.AmmoType;
        clone.Quantity = src.Quantity;
        clone.MaxQuantity = src.MaxQuantity;
        clone.CritThreatMin = src.CritThreatMin;
        clone.CritMultiplier = src.CritMultiplier;

        // Armor/Shield properties
        clone.ArmorBonus = src.ArmorBonus;
        clone.ShieldBonus = src.ShieldBonus;
        clone.ArmorCat = src.ArmorCat;
        clone.ArmorMaterial = src.ArmorMaterial;
        clone.MaxDexBonus = src.MaxDexBonus;
        clone.ArmorCheckPenalty = src.ArmorCheckPenalty;
        clone.ArcaneSpellFailure = src.ArcaneSpellFailure;
        clone.WeightLbs = src.WeightLbs;
        clone.VisualTags = src.VisualTags != null ? new HashSet<string>(src.VisualTags) : new HashSet<string>();
        clone.BasePriceGp = src.BasePriceGp;
        clone.enhancementBonus = src.enhancementBonus;
        clone.EnhancementBonus = src.EnhancementBonus;
        clone.Hardness = src.Hardness;
        clone.MaxHitPoints = src.MaxHitPoints;
        clone.CurrentHitPoints = src.CurrentHitPoints;
        clone.IsBroken = src.IsBroken;
        clone.IsDestroyed = src.IsDestroyed;
        clone.ActiveSpellEffects = new List<ItemSpellEffect>();
        if (src.ActiveSpellEffects != null)
        {
            for (int i = 0; i < src.ActiveSpellEffects.Count; i++)
            {
                ItemSpellEffect effect = src.ActiveSpellEffects[i];
                if (effect == null)
                    continue;

                clone.ActiveSpellEffects.Add(new ItemSpellEffect
                {
                    SpellId = effect.SpellId,
                    SpellName = effect.SpellName,
                    CasterName = effect.CasterName,
                    CasterLevel = effect.CasterLevel,
                    RemainingRounds = effect.RemainingRounds,
                    BonusType = effect.BonusType,
                    EnhancementBonusAttack = effect.EnhancementBonusAttack,
                    EnhancementBonusDamage = effect.EnhancementBonusDamage,
                    CountsAsMagicForBypass = effect.CountsAsMagicForBypass,
                    BonusDamageDice = effect.BonusDamageDice,
                    BonusDamageType = effect.BonusDamageType,
                    CritThreatRangeModifier = effect.CritThreatRangeModifier,
                    EnchantedAmmoRemaining = effect.EnchantedAmmoRemaining
                });
            }
        }

        // Other
        clone.ConsumableEffect = src.ConsumableEffect;
        clone.ConsumableSpellName = src.ConsumableSpellName;
        clone.ConsumableMinimumCasterLevel = src.ConsumableMinimumCasterLevel;
        clone.ConsumableModifier = src.ConsumableModifier;
        clone.HealAmount = src.HealAmount;
        clone.HealDiceCount = src.HealDiceCount;
        clone.HealDiceSides = src.HealDiceSides;
        clone.HealBonus = src.HealBonus;
        clone.IsScroll = src.IsScroll;
        clone.ScrollType = src.ScrollType;
        clone.ScrollSpellLevel = src.ScrollSpellLevel;
        clone.IsPotion = src.IsPotion;
        clone.PotionSpellLevel = src.PotionSpellLevel;
        clone.IsWand = src.IsWand;
        clone.CurrentCharges = src.CurrentCharges;
        clone.MaxCharges = src.MaxCharges;
        clone.WandSpellId = src.WandSpellId;
        clone.WandCasterLevel = src.WandCasterLevel;
        clone.WandSpellLevel = src.WandSpellLevel;
        clone.IsStackable = src.IsStackable;
        clone.MaxStackSize = src.MaxStackSize;
        clone.StackCount = src.StackCount;
        clone.IconChar = src.IconChar;
        clone.IconColor = src.IconColor;

        if (clone is RopeItemData ropeClone && src is RopeItemData ropeSrc)
        {
            ropeClone.BreakDC = ropeSrc.BreakDC;
            ropeClone.LengthFeet = ropeSrc.LengthFeet;
        }

        return clone;
    }
}