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
/// Weapon proficiency category from D&D 3.5 PHB.
/// </summary>
public enum WeaponProficiency
{
    None,
    Simple,
    Martial,
    Exotic
}

/// <summary>
/// Weapon category: melee or ranged.
/// </summary>
public enum WeaponCategory
{
    None,
    Melee,
    Ranged
}

/// <summary>
/// How a weapon applies ability modifiers to damage (D&D 3.5 rules).
/// </summary>
public enum DamageModifierType
{
    None,               // No ability modifier to damage (bows, crossbows, slings)
    Strength,           // Add full STR modifier (one-handed melee, thrown weapons)
    StrengthOneAndHalf, // Add 1.5× STR modifier, rounded down (two-handed melee)
    StrengthHalf,       // Add 0.5× STR modifier, rounded down (off-hand; handled separately)
    Composite           // Add STR up to composite rating (composite bows)
}

/// <summary>
/// Armor weight category from D&D 3.5 PHB.
/// </summary>
public enum ArmorCategory
{
    None,
    Light,
    Medium,
    Heavy,
    Shield
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

    // --- Weapon Properties ---
    public WeaponProficiency Proficiency;   // Simple, Martial, Exotic
    public WeaponCategory WeaponCat;        // Melee or Ranged
    public int DamageDice;      // Sides on damage die (e.g., 8 for d8)
    public int DamageCount;     // Number of damage dice (usually 1)
    public int BonusDamage;     // Flat bonus damage
    public int AttackRange;     // 1 = melee, >1 = ranged (in feet for ranged weapons, hexes for melee)
    public bool IsLightWeapon;  // Light weapon (dagger, short sword) - reduces TWF penalties
    public bool IsTwoHanded;    // Two-handed weapon - can't be dual-wielded, 1.5x STR to damage
    public bool HasReach;       // Reach weapon - can attack at 2 hexes
    public string DamageType;   // "slashing", "piercing", "bludgeoning", or combinations

    // --- Damage Modifier Properties (D&D 3.5) ---
    public DamageModifierType DmgModType;  // How STR (or other) applies to damage
    public int CompositeRating;            // For composite bows: max STR bonus allowed (0 = no bonus)
    public bool IsThrown;                  // Whether this weapon can be thrown (gets STR on throw)

    // --- Range Increment (D&D 3.5) ---
    public int RangeIncrement;             // Range increment in feet (0 = melee only). Max range = 10 × this.

    // --- Critical Hit (D&D 3.5) ---
    public int CritThreatMin;   // Minimum natural d20 roll to threaten a crit (e.g., 19 for 19-20, 20 for 20 only)
    public int CritMultiplier;  // Damage multiplier on confirmed crit (e.g., 2 for ×2, 3 for ×3)

    // --- Armor/Shield Properties (D&D 3.5 PHB) ---
    public int ArmorBonus;          // AC bonus when equipped as armor
    public int ShieldBonus;         // AC bonus when equipped as shield
    public ArmorCategory ArmorCat;  // Light, Medium, Heavy, Shield
    public int MaxDexBonus;         // Maximum DEX bonus to AC while wearing (-1 = no limit)
    public int ArmorCheckPenalty;   // Penalty to STR/DEX skills (stored as positive, applied as negative)
    public int ArcaneSpellFailure;  // Percentage chance of arcane spell failure (0-100)
    public float WeightLbs;         // Weight in pounds

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

    /// <summary>Get a formatted critical hit range string (e.g., "19-20/×2").</summary>
    public string GetCritRangeString()
    {
        int threatMin = CritThreatMin > 0 ? CritThreatMin : 20;
        int mult = CritMultiplier > 0 ? CritMultiplier : 2;
        string range = threatMin < 20 ? $"{threatMin}-20" : "20";
        return $"{range}/×{mult}";
    }

    /// <summary>Get a short stat summary for tooltips.</summary>
    public string GetStatSummary()
    {
        string stats = "";
        if (Type == ItemType.Weapon)
        {
            string dmg = $"{DamageCount}d{DamageDice}";
            if (BonusDamage > 0) dmg += $"+{BonusDamage}";
            stats = $"Damage: {dmg} | Crit: {GetCritRangeString()}";
            if (!string.IsNullOrEmpty(DamageType)) stats += $"\nType: {DamageType}";
            if (RangeIncrement > 0)
            {
                int maxRange = RangeIncrement * 10;
                int incHexes = RangeIncrement / 5;
                int maxHexes = maxRange / 5;
                stats += $"\nRange: {RangeIncrement} ft increment ({incHexes} hex), max {maxRange} ft ({maxHexes} hex)";
            }
            else if (AttackRange > 1) stats += $" | Range: {AttackRange} ft";
            string props = "";
            if (IsLightWeapon) props += "Light, ";
            if (IsTwoHanded) props += "Two-handed, ";
            if (HasReach) props += "Reach, ";
            if (IsThrown) props += "Thrown, ";
            if (DmgModType == DamageModifierType.Composite) props += $"Composite (+{CompositeRating} STR), ";
            else if (DmgModType == DamageModifierType.StrengthOneAndHalf) props += "1.5× STR dmg, ";
            else if (DmgModType == DamageModifierType.None && Type == ItemType.Weapon && WeaponCat == WeaponCategory.Ranged) props += "No STR to dmg, ";
            if (props.Length > 0)
            {
                props = props.TrimEnd(',', ' ');
                stats += $"\n{props}";
            }
            stats += $"\n{Proficiency} {WeaponCat}";
        }
        else if (Type == ItemType.Armor)
        {
            stats = $"AC Bonus: +{ArmorBonus} ({ArmorCat})";
            if (MaxDexBonus >= 0) stats += $"\nMax Dex: +{MaxDexBonus}";
            if (ArmorCheckPenalty > 0) stats += $" | Check: -{ArmorCheckPenalty}";
            if (ArcaneSpellFailure > 0) stats += $"\nSpell Fail: {ArcaneSpellFailure}%";
            if (WeightLbs > 0) stats += $" | {WeightLbs} lbs";
        }
        else if (Type == ItemType.Shield)
        {
            stats = $"Shield Bonus: +{ShieldBonus}";
            if (MaxDexBonus >= 0) stats += $"\nMax Dex: +{MaxDexBonus}";
            if (ArmorCheckPenalty > 0) stats += $" | Check: -{ArmorCheckPenalty}";
            if (ArcaneSpellFailure > 0) stats += $"\nSpell Fail: {ArcaneSpellFailure}%";
            if (WeightLbs > 0) stats += $" | {WeightLbs} lbs";
        }
        else if (Type == ItemType.Consumable && HealAmount > 0)
        {
            stats = $"Heals: {HealAmount} HP";
        }
        return stats;
    }
}
