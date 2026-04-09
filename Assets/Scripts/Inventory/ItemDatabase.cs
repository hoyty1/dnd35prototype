using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static database of all items in the game.
/// Call ItemDatabase.Init() once at startup, then use Get(id) to retrieve items.
/// </summary>
public static class ItemDatabase
{
    private static Dictionary<string, ItemData> _items = new Dictionary<string, ItemData>();
    private static bool _initialized = false;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _items.Clear();

        // ===== WEAPONS =====
        // Longsword: 19-20/×2
        Register(new ItemData
        {
            Id = "longsword", Name = "Longsword", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Description = "A versatile one-handed sword favored by fighters.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            CritThreatMin = 19, CritMultiplier = 2,
            IconChar = "\u2694", IconColor = new Color(0.7f, 0.7f, 0.8f)
        });

        // Short Sword: 19-20/×2
        Register(new ItemData
        {
            Id = "short_sword", Name = "Short Sword", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Description = "A light blade ideal for quick strikes and finesse.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            IsLightWeapon = true,
            CritThreatMin = 19, CritMultiplier = 2,
            IconChar = "\u2694", IconColor = new Color(0.6f, 0.8f, 0.6f)
        });

        // Dagger: 19-20/×2
        Register(new ItemData
        {
            Id = "dagger", Name = "Dagger", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Description = "A small blade. Light and easy to conceal.",
            DamageDice = 4, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            IsLightWeapon = true,
            CritThreatMin = 19, CritMultiplier = 2,
            IconChar = "\u2020", IconColor = new Color(0.8f, 0.8f, 0.7f)
        });

        // Rapier: 18-20/×2
        Register(new ItemData
        {
            Id = "rapier", Name = "Rapier", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Description = "An elegant thrusting sword with a wide critical range.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            CritThreatMin = 18, CritMultiplier = 2,
            IconChar = "\u2694", IconColor = new Color(0.8f, 0.8f, 0.9f)
        });

        // Mace: 20/×2
        Register(new ItemData
        {
            Id = "mace", Name = "Mace", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Description = "A heavy bludgeoning weapon effective against armored foes.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            CritThreatMin = 20, CritMultiplier = 2,
            IconChar = "\u2692", IconColor = new Color(0.6f, 0.6f, 0.6f)
        });

        // Battleaxe: 20/×3
        Register(new ItemData
        {
            Id = "battleaxe", Name = "Battleaxe", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Description = "A sturdy one-handed axe capable of devastating critical hits.",
            DamageDice = 8, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            CritThreatMin = 20, CritMultiplier = 3,
            IconChar = "\u2692", IconColor = new Color(0.7f, 0.55f, 0.35f)
        });

        // Greataxe: 20/×3
        Register(new ItemData
        {
            Id = "greataxe", Name = "Greataxe", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Description = "A massive two-handed axe dealing devastating blows.",
            DamageDice = 12, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            CritThreatMin = 20, CritMultiplier = 3,
            IconChar = "\u2694", IconColor = new Color(0.9f, 0.5f, 0.3f)
        });

        // Quarterstaff: 20/×2
        Register(new ItemData
        {
            Id = "quarterstaff", Name = "Quarterstaff", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Description = "A simple wooden staff. Reliable and versatile.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            CritThreatMin = 20, CritMultiplier = 2,
            IconChar = "\u2502", IconColor = new Color(0.6f, 0.5f, 0.3f)
        });

        // Handaxe: 20/×3
        Register(new ItemData
        {
            Id = "handaxe", Name = "Handaxe", Type = ItemType.Weapon,
            Slot = EquipSlot.EitherHand,
            Description = "A small axe suitable for one-handed combat.",
            DamageDice = 6, DamageCount = 1, BonusDamage = 0, AttackRange = 1,
            IsLightWeapon = true,
            CritThreatMin = 20, CritMultiplier = 3,
            IconChar = "\u2692", IconColor = new Color(0.7f, 0.5f, 0.3f)
        });

        // ===== ARMOR =====
        Register(new ItemData
        {
            Id = "leather_armor", Name = "Leather Armor", Type = ItemType.Armor,
            Slot = EquipSlot.Armor,
            Description = "Light armor made from hardened leather. +2 AC.",
            ArmorBonus = 2,
            IconChar = "\u26E8", IconColor = new Color(0.6f, 0.4f, 0.2f)
        });

        Register(new ItemData
        {
            Id = "chain_shirt", Name = "Chain Shirt", Type = ItemType.Armor,
            Slot = EquipSlot.Armor,
            Description = "A shirt of interlocking metal rings. +4 AC.",
            ArmorBonus = 4,
            IconChar = "\u26E8", IconColor = new Color(0.6f, 0.6f, 0.7f)
        });

        Register(new ItemData
        {
            Id = "scale_mail", Name = "Scale Mail", Type = ItemType.Armor,
            Slot = EquipSlot.Armor,
            Description = "Overlapping metal scales on a leather coat. +4 AC.",
            ArmorBonus = 4,
            IconChar = "\u26E8", IconColor = new Color(0.5f, 0.6f, 0.5f)
        });

        Register(new ItemData
        {
            Id = "studded_leather", Name = "Studded Leather", Type = ItemType.Armor,
            Slot = EquipSlot.Armor,
            Description = "Leather armor reinforced with metal studs. +3 AC.",
            ArmorBonus = 3,
            IconChar = "\u26E8", IconColor = new Color(0.5f, 0.35f, 0.2f)
        });

        Register(new ItemData
        {
            Id = "breastplate", Name = "Breastplate", Type = ItemType.Armor,
            Slot = EquipSlot.Armor,
            Description = "A fitted metal chest plate. +5 AC.",
            ArmorBonus = 5,
            IconChar = "\u26E8", IconColor = new Color(0.7f, 0.7f, 0.75f)
        });

        // ===== SHIELDS =====
        Register(new ItemData
        {
            Id = "buckler", Name = "Buckler", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand,
            Description = "A small round shield. +1 AC.",
            ShieldBonus = 1,
            IconChar = "\u26E1", IconColor = new Color(0.5f, 0.5f, 0.4f)
        });

        Register(new ItemData
        {
            Id = "heavy_shield", Name = "Heavy Shield", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand,
            Description = "A large steel shield. +2 AC.",
            ShieldBonus = 2,
            IconChar = "\u26E1", IconColor = new Color(0.6f, 0.6f, 0.7f)
        });

        Register(new ItemData
        {
            Id = "light_shield", Name = "Light Shield", Type = ItemType.Shield,
            Slot = EquipSlot.LeftHand,
            Description = "A light wooden shield. +1 AC.",
            ShieldBonus = 1,
            IconChar = "\u26E1", IconColor = new Color(0.6f, 0.5f, 0.3f)
        });

        // ===== CONSUMABLES =====
        Register(new ItemData
        {
            Id = "potion_healing", Name = "Potion of Healing", Type = ItemType.Consumable,
            Slot = EquipSlot.None,
            Description = "A red potion that restores 2d4+2 hit points.",
            HealAmount = 7, // average of 2d4+2
            IconChar = "\u2661", IconColor = new Color(1f, 0.3f, 0.3f)
        });

        Register(new ItemData
        {
            Id = "potion_greater_healing", Name = "Potion of Greater Healing", Type = ItemType.Consumable,
            Slot = EquipSlot.None,
            Description = "A glowing red potion that restores 4d4+4 hit points.",
            HealAmount = 14,
            IconChar = "\u2661", IconColor = new Color(1f, 0.1f, 0.5f)
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

    private static void Register(ItemData item)
    {
        _items[item.Id] = item;
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
            ArmorBonus = src.ArmorBonus, ShieldBonus = src.ShieldBonus,
            DamageDice = src.DamageDice, DamageCount = src.DamageCount,
            BonusDamage = src.BonusDamage, AttackRange = src.AttackRange,
            IsLightWeapon = src.IsLightWeapon,
            CritThreatMin = src.CritThreatMin, CritMultiplier = src.CritMultiplier,
            HealAmount = src.HealAmount,
            IconChar = src.IconChar, IconColor = src.IconColor
        };
    }
}
