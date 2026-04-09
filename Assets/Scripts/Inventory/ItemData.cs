using UnityEngine;

/// <summary>
/// Types of items in the game.
/// </summary>
public enum ItemType
{
    Weapon,
    Armor,
    Shield,
    Consumable,
    Misc
}

/// <summary>
/// Which equipment slot(s) an item can go into.
/// </summary>
public enum EquipSlot
{
    None,       // Cannot be equipped (consumable, misc)
    Armor,      // Chest armor slot
    LeftHand,   // Shield or weapon
    RightHand,  // Weapon only
    EitherHand  // Can go in left or right hand (weapons)
}

/// <summary>
/// Represents a single item with its properties and stats.
/// Items are value types copied around; use ItemDatabase IDs for identity.
/// </summary>
[System.Serializable]
public class ItemData
{
    public string Id;           // Unique identifier (e.g., "longsword")
    public string Name;         // Display name
    public string Description;  // Tooltip description
    public ItemType Type;
    public EquipSlot Slot;

    // --- Stat bonuses ---
    public int ArmorBonus;      // AC bonus when equipped as armor
    public int ShieldBonus;     // AC bonus when equipped as shield
    public int DamageDice;      // Sides on damage die (e.g., 8 for d8)
    public int DamageCount;     // Number of damage dice (usually 1)
    public int BonusDamage;     // Flat bonus damage
    public int AttackRange;     // 1 = melee, >1 = ranged
    public bool IsLightWeapon;  // Light weapon (dagger, short sword) - reduces TWF penalties

    // --- Consumable ---
    public int HealAmount;      // HP restored if consumable

    // --- Visual ---
    public string IconChar;     // Unicode/emoji character for display (fallback icon)
    public Color IconColor;     // Color tint for the icon

    /// <summary>Create an empty/null item.</summary>
    public static ItemData Empty => null;

    public bool IsWeapon => Type == ItemType.Weapon;
    public bool IsArmor => Type == ItemType.Armor;
    public bool IsShield => Type == ItemType.Shield;
    public bool IsConsumable => Type == ItemType.Consumable;

    /// <summary>Can this item be equipped in the given slot?</summary>
    public bool CanEquipIn(EquipSlot targetSlot)
    {
        if (Slot == EquipSlot.None) return false;
        if (Slot == targetSlot) return true;
        // EitherHand items can go in LeftHand or RightHand
        if (Slot == EquipSlot.EitherHand && (targetSlot == EquipSlot.LeftHand || targetSlot == EquipSlot.RightHand))
            return true;
        return false;
    }

    /// <summary>Get a short stat summary for tooltips.</summary>
    public string GetStatSummary()
    {
        string stats = "";
        if (Type == ItemType.Weapon)
        {
            string dmg = $"{DamageCount}d{DamageDice}";
            if (BonusDamage > 0) dmg += $"+{BonusDamage}";
            stats = $"Damage: {dmg}";
            if (AttackRange > 1) stats += $" | Range: {AttackRange}";
        }
        else if (Type == ItemType.Armor)
        {
            stats = $"AC Bonus: +{ArmorBonus}";
        }
        else if (Type == ItemType.Shield)
        {
            stats = $"Shield Bonus: +{ShieldBonus}";
        }
        else if (Type == ItemType.Consumable && HealAmount > 0)
        {
            stats = $"Heals: {HealAmount} HP";
        }
        return stats;
    }
}
