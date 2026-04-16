using System.Collections.Generic;
using UnityEngine;

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
        RegisterConsumablesAndMisc();
    }

    // ============================================================
    //  SIMPLE MELEE WEAPONS (D&D 3.5 PHB Table 7-5)
    // ============================================================
    private static void RegisterSimpleMeleeWeapons()
    {
        // Unarmed Strike: 1d3, 20/×2, bludgeoning
        Register(new ItemData
        {
            Id = "unarmed_strike", Name = "Unarmed Strike", Type = ItemType.Weapon,
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

        // Gauntlet: 1d3, 20/×2, bludgeoning
        Register(new ItemData
        {
            Id = "gauntlet", Name = "Gauntlet", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Melee,
            Description = "An armored glove that lets you deal lethal damage with unarmed strikes.",
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
            Id = "dagger", Name = "Dagger", Type = ItemType.Weapon,
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
            Id = "mace_light", Name = "Mace, Light", Type = ItemType.Weapon,
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
            Id = "sickle", Name = "Sickle", Type = ItemType.Weapon,
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
            Id = "club", Name = "Club", Type = ItemType.Weapon,
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
            Id = "mace_heavy", Name = "Mace, Heavy", Type = ItemType.Weapon,
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
            Id = "morningstar", Name = "Morningstar", Type = ItemType.Weapon,
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
            Id = "shortspear", Name = "Shortspear", Type = ItemType.Weapon,
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
            Id = "quarterstaff", Name = "Quarterstaff", Type = ItemType.Weapon,
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

        // Spear: 1d8, 20/×3, piercing, two-handed
        Register(new ItemData
        {
            Id = "spear", Name = "Spear", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Melee,
            Description = "A long thrusting weapon with a pointed tip. Two-handed.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            IsTwoHanded = true,
            DamageType = "piercing",
            DmgModType = DamageModifierType.StrengthOneAndHalf,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 6f,
            IconChar = "\u2191", IconColor = new Color(0.6f, 0.6f, 0.5f)
        });

        // Longspear: 1d8, 20/×3, piercing, two-handed, reach (cannot attack adjacent)
        Register(new ItemData
        {
            Id = "longspear", Name = "Longspear", Type = ItemType.Weapon,
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

        // Legacy alias: "mace" -> "mace_heavy" for backward compatibility
        Register(new ItemData
        {
            Id = "mace", Name = "Mace, Heavy", Type = ItemType.Weapon,
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
            Id = "crossbow_light", Name = "Crossbow, Light", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Ranged,
            Description = "A light crossbow that fires bolts. Requires two hands to load.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 80,
            DamageType = "piercing",
            DmgModType = DamageModifierType.None,
            RangeIncrement = 80,
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
            Id = "crossbow_heavy", Name = "Crossbow, Heavy", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Ranged,
            Description = "A heavy crossbow with greater range and damage than its lighter cousin.",
            DamageDice = 10, DamageCount = 1, BonusDamage = 0, AttackRange = 120,
            DamageType = "piercing",
            DmgModType = DamageModifierType.None,
            RangeIncrement = 120,
            RequiresReload = true,
            IsLoaded = true,
            ReloadAction = ReloadActionType.FullRound,
            CritThreatMin = 19, CritMultiplier = 2,
            WeightLbs = 8f,
            IconChar = "\u2732", IconColor = new Color(0.5f, 0.5f, 0.5f)
        });

        // Dart: 1d4, 20/×2, piercing, range 20 ft
        Register(new ItemData
        {
            Id = "dart", Name = "Dart", Type = ItemType.Weapon,
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
            Id = "javelin", Name = "Javelin", Type = ItemType.Weapon,
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
            Id = "sling", Name = "Sling", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Simple, WeaponCat = WeaponCategory.Ranged,
            Description = "A simple weapon that hurls stones at high velocity.",
            DamageDice = 4, DamageCount = 1, BonusDamage = 0, AttackRange = 50,
            DamageType = "bludgeoning",
            DmgModType = DamageModifierType.None,
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
            Id = "handaxe", Name = "Handaxe", Type = ItemType.Weapon,
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
            Id = "short_sword", Name = "Shortsword", Type = ItemType.Weapon,
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
            Id = "flail_light", Name = "Flail, Light", Type = ItemType.Weapon,
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
            Id = "longsword", Name = "Longsword", Type = ItemType.Weapon,
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
            Id = "rapier", Name = "Rapier", Type = ItemType.Weapon,
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
            Id = "scimitar", Name = "Scimitar", Type = ItemType.Weapon,
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
            Id = "battleaxe", Name = "Battleaxe", Type = ItemType.Weapon,
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
            Id = "warhammer", Name = "Warhammer", Type = ItemType.Weapon,
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
            Id = "trident", Name = "Trident", Type = ItemType.Weapon,
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
            Id = "warpick", Name = "War Pick", Type = ItemType.Weapon,
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
            Id = "lance", Name = "Lance", Type = ItemType.Weapon,
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
            Id = "greatsword", Name = "Greatsword", Type = ItemType.Weapon,
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
            Id = "greataxe", Name = "Greataxe", Type = ItemType.Weapon,
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
            Id = "greatclub", Name = "Greatclub", Type = ItemType.Weapon,
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
            Id = "falchion", Name = "Falchion", Type = ItemType.Weapon,
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
            Id = "flail_heavy", Name = "Flail, Heavy", Type = ItemType.Weapon,
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
            Id = "guisarme", Name = "Guisarme", Type = ItemType.Weapon,
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
            Id = "halberd", Name = "Halberd", Type = ItemType.Weapon,
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
            Id = "ranseur", Name = "Ranseur", Type = ItemType.Weapon,
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
            Id = "spiked_chain", Name = "Spiked Chain", Type = ItemType.Weapon,
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
            Id = "whip", Name = "Whip", Type = ItemType.Weapon,
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
            Id = "glaive", Name = "Glaive", Type = ItemType.Weapon,
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
            Id = "longbow", Name = "Longbow", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Ranged,
            Description = "A tall bow with excellent range. Requires two hands.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 100,
            IsTwoHanded = true,
            DamageType = "piercing",
            DmgModType = DamageModifierType.None,
            RangeIncrement = 100,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 3f,
            IconChar = "\u2191", IconColor = new Color(0.6f, 0.5f, 0.3f)
        });

        // Shortbow: 1d6, 20/×3, piercing, range 60 ft, two-handed
        Register(new ItemData
        {
            Id = "shortbow", Name = "Shortbow", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Ranged,
            Description = "A compact bow favored by mounted archers. Requires two hands.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 60,
            IsTwoHanded = true,
            DamageType = "piercing",
            DmgModType = DamageModifierType.None,
            RangeIncrement = 60,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 2f,
            IconChar = "\u2191", IconColor = new Color(0.5f, 0.5f, 0.3f)
        });

        // Composite Longbow: 1d8, 20/×3, piercing, range 110 ft, two-handed
        Register(new ItemData
        {
            Id = "composite_longbow", Name = "Composite Longbow", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Ranged,
            Description = "A powerful composite bow with superior range. Allows STR bonus to damage.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 110,
            IsTwoHanded = true,
            DamageType = "piercing",
            DmgModType = DamageModifierType.Composite,
            CompositeRating = 0,
            RangeIncrement = 110,
            CritThreatMin = 20, CritMultiplier = 3,
            WeightLbs = 3f,
            IconChar = "\u2191", IconColor = new Color(0.7f, 0.5f, 0.3f)
        });

        // Composite Shortbow: 1d6, 20/×3, piercing, range 70 ft, two-handed
        Register(new ItemData
        {
            Id = "composite_shortbow", Name = "Composite Shortbow", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Ranged,
            Description = "A compact composite bow. Allows STR bonus to damage.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 70,
            IsTwoHanded = true,
            DamageType = "piercing",
            DmgModType = DamageModifierType.Composite,
            CompositeRating = 0,
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
            Id = "padded_armor", Name = "Padded Armor", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Light,
            Description = "Layers of quilted cloth. The lightest armor available.",
            ArmorBonus = 1, MaxDexBonus = 8, ArmorCheckPenalty = 0,
            ArcaneSpellFailure = 5, WeightLbs = 10f,
            IconChar = "\u26E8", IconColor = new Color(0.7f, 0.7f, 0.6f)
        });

        // Leather: +2 AC, Max Dex +6, Check 0, Spell Failure 10%, 15 lbs
        Register(new ItemData
        {
            Id = "leather_armor", Name = "Leather Armor", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Light,
            Description = "Light armor made from hardened leather.",
            ArmorBonus = 2, MaxDexBonus = 6, ArmorCheckPenalty = 0,
            ArcaneSpellFailure = 10, WeightLbs = 15f,
            IconChar = "\u26E8", IconColor = new Color(0.6f, 0.4f, 0.2f)
        });

        // Studded Leather: +3 AC, Max Dex +5, Check -1, Spell Failure 15%, 20 lbs
        Register(new ItemData
        {
            Id = "studded_leather", Name = "Studded Leather", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Light,
            Description = "Leather armor reinforced with metal studs.",
            ArmorBonus = 3, MaxDexBonus = 5, ArmorCheckPenalty = 1,
            ArcaneSpellFailure = 15, WeightLbs = 20f,
            IconChar = "\u26E8", IconColor = new Color(0.5f, 0.35f, 0.2f)
        });

        // Chain Shirt: +4 AC, Max Dex +4, Check -2, Spell Failure 20%, 25 lbs
        Register(new ItemData
        {
            Id = "chain_shirt", Name = "Chain Shirt", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Light,
            Description = "A shirt of interlocking metal rings. Best light armor.",
            ArmorBonus = 4, MaxDexBonus = 4, ArmorCheckPenalty = 2,
            ArcaneSpellFailure = 20, WeightLbs = 25f,
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
            Id = "hide_armor", Name = "Hide Armor", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Medium,
            Description = "Thick furs and pelts of animals, crudely prepared.",
            ArmorBonus = 3, MaxDexBonus = 4, ArmorCheckPenalty = 3,
            ArcaneSpellFailure = 20, WeightLbs = 25f,
            IconChar = "\u26E8", IconColor = new Color(0.6f, 0.5f, 0.3f)
        });

        // Scale Mail: +4 AC, Max Dex +3, Check -4, Spell Failure 25%, 30 lbs
        Register(new ItemData
        {
            Id = "scale_mail", Name = "Scale Mail", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Medium,
            Description = "Overlapping metal scales on a leather coat.",
            ArmorBonus = 4, MaxDexBonus = 3, ArmorCheckPenalty = 4,
            ArcaneSpellFailure = 25, WeightLbs = 30f,
            IconChar = "\u26E8", IconColor = new Color(0.5f, 0.6f, 0.5f)
        });

        // Chainmail: +5 AC, Max Dex +2, Check -5, Spell Failure 30%, 40 lbs
        Register(new ItemData
        {
            Id = "chainmail", Name = "Chainmail", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Medium,
            Description = "A full suit of interlocking metal rings covering the body.",
            ArmorBonus = 5, MaxDexBonus = 2, ArmorCheckPenalty = 5,
            ArcaneSpellFailure = 30, WeightLbs = 40f,
            IconChar = "\u26E8", IconColor = new Color(0.5f, 0.5f, 0.6f)
        });

        // Breastplate: +5 AC, Max Dex +3, Check -4, Spell Failure 25%, 30 lbs
        Register(new ItemData
        {
            Id = "breastplate", Name = "Breastplate", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Medium,
            Description = "A fitted metal chest plate. Best medium armor for most characters.",
            ArmorBonus = 5, MaxDexBonus = 3, ArmorCheckPenalty = 4,
            ArcaneSpellFailure = 25, WeightLbs = 30f,
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
            Id = "splint_mail", Name = "Splint Mail", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Heavy,
            Description = "Strips of metal between layers of leather and chain.",
            ArmorBonus = 6, MaxDexBonus = 0, ArmorCheckPenalty = 7,
            ArcaneSpellFailure = 40, WeightLbs = 45f,
            IconChar = "\u26E8", IconColor = new Color(0.5f, 0.5f, 0.55f)
        });

        // Banded Mail: +6 AC, Max Dex +1, Check -6, Spell Failure 35%, 35 lbs
        Register(new ItemData
        {
            Id = "banded_mail", Name = "Banded Mail", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Heavy,
            Description = "Overlapping strips of metal banded over chain and leather.",
            ArmorBonus = 6, MaxDexBonus = 1, ArmorCheckPenalty = 6,
            ArcaneSpellFailure = 35, WeightLbs = 35f,
            IconChar = "\u26E8", IconColor = new Color(0.55f, 0.55f, 0.6f)
        });

        // Half-Plate: +7 AC, Max Dex +0, Check -7, Spell Failure 40%, 50 lbs
        Register(new ItemData
        {
            Id = "half_plate", Name = "Half-Plate", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Heavy,
            Description = "Plate armor covering most of the body with chain and leather.",
            ArmorBonus = 7, MaxDexBonus = 0, ArmorCheckPenalty = 7,
            ArcaneSpellFailure = 40, WeightLbs = 50f,
            IconChar = "\u26E8", IconColor = new Color(0.6f, 0.6f, 0.65f)
        });

        // Full Plate: +8 AC, Max Dex +1, Check -6, Spell Failure 35%, 50 lbs
        Register(new ItemData
        {
            Id = "full_plate", Name = "Full Plate", Type = ItemType.Armor,
            Slot = EquipSlot.Armor, ArmorCat = ArmorCategory.Heavy,
            Description = "A complete suit of heavy plate armor. The finest protection available.",
            ArmorBonus = 8, MaxDexBonus = 1, ArmorCheckPenalty = 6,
            ArcaneSpellFailure = 35, WeightLbs = 50f,
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
            Id = "buckler", Name = "Buckler", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand, ArmorCat = ArmorCategory.Shield,
            Description = "A small round shield strapped to the forearm.",
            ShieldBonus = 1, MaxDexBonus = -1, ArmorCheckPenalty = 1,
            ArcaneSpellFailure = 5, WeightLbs = 5f,
            IconChar = "\u26E1", IconColor = new Color(0.5f, 0.5f, 0.4f)
        });

        // Shield, Light Wooden: +1 AC, bash 1d3 bludgeoning (martial, light off-hand)
        Register(new ItemData
        {
            Id = "shield_light_wooden", Name = "Shield, Light Wooden", Type = ItemType.Shield,
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
            Id = "shield_light_steel", Name = "Shield, Light Steel", Type = ItemType.Shield,
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

        // Shield, Light Steel (Spiked): +1 AC, bash 1d3+1 piercing (martial, light off-hand)
        Register(new ItemData
        {
            Id = "shield_light_steel_spiked", Name = "Shield, Light Steel (Spiked)", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand, ArmorCat = ArmorCategory.Shield,
            Description = "A light steel shield fitted with shield spikes. Shield spikes add +1 damage and change bash damage to piercing.",
            ShieldBonus = 1, MaxDexBonus = -1, ArmorCheckPenalty = 1,
            ArcaneSpellFailure = 5, WeightLbs = 6f,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee, WeaponSize = WeaponSizeCategory.Light,
            DamageDice = 3, DamageCount = 1, BonusDamage = 1, AttackRange = 1,
            ReachSquares = 1, CanAttackAdjacent = true, IsLightWeapon = true,
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            DamageType = "piercing",
            IconChar = "\u26E1", IconColor = new Color(0.65f, 0.65f, 0.75f)
        });

        // Shield, Heavy Wooden: +2 AC, bash 1d4 bludgeoning (martial)
        Register(new ItemData
        {
            Id = "shield_heavy_wooden", Name = "Shield, Heavy Wooden", Type = ItemType.Shield,
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
            Id = "shield_heavy_steel", Name = "Shield, Heavy Steel", Type = ItemType.Shield,
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

        // Shield, Heavy Steel (Spiked): +2 AC, bash 1d4+1 piercing (martial)
        Register(new ItemData
        {
            Id = "shield_heavy_steel_spiked", Name = "Shield, Heavy Steel (Spiked)", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand, ArmorCat = ArmorCategory.Shield,
            Description = "A heavy steel shield fitted with shield spikes. Shield spikes add +1 damage and change bash damage to piercing.",
            ShieldBonus = 2, MaxDexBonus = -1, ArmorCheckPenalty = 2,
            ArcaneSpellFailure = 15, WeightLbs = 15f,
            Proficiency = WeaponProficiency.Martial, WeaponCat = WeaponCategory.Melee, WeaponSize = WeaponSizeCategory.OneHanded,
            DamageDice = 4, DamageCount = 1, BonusDamage = 1, AttackRange = 1,
            ReachSquares = 1, CanAttackAdjacent = true, IsLightWeapon = false,
            DmgModType = DamageModifierType.Strength,
            CritThreatMin = 20, CritMultiplier = 2,
            DamageType = "piercing",
            IconChar = "\u26E1", IconColor = new Color(0.65f, 0.65f, 0.75f)
        });

        // Tower Shield: +4 AC, Max Dex -, Check -10, Spell Failure 50%, 45 lbs
        Register(new ItemData
        {
            Id = "tower_shield", Name = "Tower Shield", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand, ArmorCat = ArmorCategory.Shield,
            Description = "A massive shield providing cover. Severe penalties to skills and attacks.",
            ShieldBonus = 4, MaxDexBonus = -1, ArmorCheckPenalty = 10,
            ArcaneSpellFailure = 50, WeightLbs = 45f,
            IconChar = "\u26E1", IconColor = new Color(0.5f, 0.5f, 0.5f)
        });

        // Legacy aliases for backward compatibility
        Register(new ItemData
        {
            Id = "heavy_shield", Name = "Shield, Heavy Steel", Type = ItemType.Shield,
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
            Id = "light_shield", Name = "Shield, Light Wooden", Type = ItemType.Shield,
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

    // ============================================================
    //  CONSUMABLES & MISCELLANEOUS
    // ============================================================
    private static void RegisterConsumablesAndMisc()
    {
        RegisterSpellPotion(
            id: "potion_cure_light_wounds",
            name: "Potion of Cure Light Wounds",
            spellName: "Cure Light Wounds",
            description: "Standard D&D 3.5e potion. Mimics Cure Light Wounds at minimum caster level (1d8+1).",
            minimumCasterLevel: 1,
            modifier: 1,
            iconChar: "\u2661",
            iconColor: new Color(1f, 0.3f, 0.3f));

        // Backward-compatible alias used by existing class loadouts.
        RegisterSpellPotion(
            id: "potion_healing",
            name: "Potion of Cure Light Wounds",
            spellName: "Cure Light Wounds",
            description: "Standard D&D 3.5e potion. Mimics Cure Light Wounds at minimum caster level (1d8+1).",
            minimumCasterLevel: 1,
            modifier: 1,
            iconChar: "\u2661",
            iconColor: new Color(1f, 0.3f, 0.3f));

        RegisterSpellPotion(
            id: "potion_shield_of_faith",
            name: "Potion of Shield of Faith",
            spellName: "Shield of Faith",
            description: "Grants a +2 deflection bonus to AC for 10 rounds (minimum caster level 1).",
            minimumCasterLevel: 1,
            modifier: 2,
            iconChar: "\u2726",
            iconColor: new Color(0.45f, 0.75f, 1f));

        Register(new ItemData
        {
            Id = "potion_greater_healing", Name = "Potion of Greater Healing", Type = ItemType.Consumable,
            Slot = EquipSlot.None,
            Description = "A glowing red potion that restores 4d4+4 hit points.",
            ConsumableEffect = ConsumableEffectType.HealHP,
            HealDiceCount = 4,
            HealDiceSides = 4,
            HealBonus = 4,
            IconChar = "\u2661", IconColor = new Color(1f, 0.1f, 0.5f)
        });

        Register(new ItemData
        {
            Id = "spiked_gauntlet", Name = "Spiked Gauntlet", Type = ItemType.Weapon,
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
            Id = "locked_gauntlet", Name = "Locked Gauntlet", Type = ItemType.Misc,
            Slot = EquipSlot.Hands,
            Description = "A locking hand harness that secures held weapons. Grants +10 to resist disarm attempts.",
            WeightLbs = 5f,
            IconChar = "⛓", IconColor = new Color(0.55f, 0.55f, 0.65f)
        });
        Register(new ItemData
        {
            Id = "torch", Name = "Torch", Type = ItemType.Misc,
            Slot = EquipSlot.None,
            Description = "A simple wooden torch. Provides light in dark places.",
            IconChar = "\u2600", IconColor = new Color(1f, 0.8f, 0.2f)
        });

        Register(new ItemData
        {
            Id = "rope", Name = "Rope (50 ft)", Type = ItemType.Misc,
            Slot = EquipSlot.None,
            Description = "A coil of hempen rope. Useful for climbing and binding.",
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
            IconChar = iconChar,
            IconColor = iconColor
        });
    }

    // ============================================================
    //  UTILITY METHODS
    // ============================================================

    private static void Register(ItemData item)
    {
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

            if (id == "unarmed_strike")
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
                     || id.Contains("longbow")
                     || id.Contains("shortbow")
                     || id.Contains("composite_longbow")
                     || id.Contains("composite_shortbow"))
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

    /// <summary>Get an item by ID. Returns null if not found.</summary>
    public static ItemData Get(string id)
    {
        if (!_initialized) Init();
        return _items.TryGetValue(id, out var item) ? item : null;
    }

    /// <summary>Create a copy of an item (since items are reference types).</summary>
    public static ItemData CloneItem(string id)
    {
        var src = Get(id);
        if (src == null) return null;
        return new ItemData
        {
            Id = src.Id, Name = src.Name, Description = src.Description,
            Type = src.Type, Slot = src.Slot,
            // Weapon properties
            Proficiency = src.Proficiency, WeaponCat = src.WeaponCat,
            WeaponSize = src.WeaponSize,
            DamageDice = src.DamageDice, DamageCount = src.DamageCount,
            BonusDamage = src.BonusDamage, AttackRange = src.AttackRange,
            IsLightWeapon = src.IsLightWeapon, IsTwoHanded = src.IsTwoHanded,
            HasReach = src.HasReach, ReachSquares = src.ReachSquares,
            CanAttackAdjacent = src.CanAttackAdjacent, IsReachWeapon = src.IsReachWeapon,
            DealsNonlethalDamage = src.DealsNonlethalDamage, WhipLikeArmorRestriction = src.WhipLikeArmorRestriction,
            DamageType = src.DamageType,
            CountsAsMagicForBypass = src.CountsAsMagicForBypass,
            IsSilvered = src.IsSilvered, IsColdIron = src.IsColdIron,
            IsAdamantine = src.IsAdamantine,
            IsAlignedGood = src.IsAlignedGood, IsAlignedEvil = src.IsAlignedEvil,
            IsAlignedLawful = src.IsAlignedLawful, IsAlignedChaotic = src.IsAlignedChaotic,
            DmgModType = src.DmgModType, CompositeRating = src.CompositeRating,
            IsThrown = src.IsThrown, RangeIncrement = src.RangeIncrement,
            RequiresReload = src.RequiresReload, IsLoaded = src.IsLoaded, ReloadAction = src.ReloadAction,
            CritThreatMin = src.CritThreatMin, CritMultiplier = src.CritMultiplier,
            // Armor/Shield properties
            ArmorBonus = src.ArmorBonus, ShieldBonus = src.ShieldBonus,
            ArmorCat = src.ArmorCat, MaxDexBonus = src.MaxDexBonus,
            ArmorCheckPenalty = src.ArmorCheckPenalty,
            ArcaneSpellFailure = src.ArcaneSpellFailure, WeightLbs = src.WeightLbs,
            // Other
            ConsumableEffect = src.ConsumableEffect,
            ConsumableSpellName = src.ConsumableSpellName,
            ConsumableMinimumCasterLevel = src.ConsumableMinimumCasterLevel,
            ConsumableModifier = src.ConsumableModifier,
            HealAmount = src.HealAmount,
            HealDiceCount = src.HealDiceCount,
            HealDiceSides = src.HealDiceSides,
            HealBonus = src.HealBonus,
            IconChar = src.IconChar, IconColor = src.IconColor
        };
    }
}