using System;
using System.Collections.Generic;
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
/// Built-in consumable effect categories.
/// Keep this extensible so future item effects can be added cleanly.
/// </summary>
public enum ConsumableEffectType
{
    None,
    HealHP,
    SpellEffect
}

/// <summary>
/// Which equipment slot(s) an item can go into.
/// </summary>
public enum EquipSlot
{
    None = 0,         // Cannot be equipped (consumable, misc)

    // Legacy/core combat slots (kept stable for serialized data compatibility)
    Armor = 1,        // Legacy name for armor/robe slot
    LeftHand = 2,     // Shield or weapon
    RightHand = 3,    // Weapon only
    EitherHand = 4,   // Can go in left or right hand (weapons)

    // D&D 3.5e body equipment slots
    Head = 5,
    FaceEyes = 6,
    Neck = 7,
    Torso = 8,
    ArmorRobe = 9,
    Waist = 10,
    Back = 11,
    Wrists = 12,
    Hands = 13,
    LeftRing = 14,
    RightRing = 15,
    EitherRing = 16,
    Feet = 17
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
/// Handedness/size category for weapon use in D&D 3.5 combat maneuvers.
/// </summary>
public enum WeaponSizeCategory
{
    None,
    Light,
    OneHanded,
    TwoHanded
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
/// D&D 3.5 reload action required for crossbows.
/// </summary>
public enum ReloadActionType
{
    None,
    FreeAction,
    MoveAction,
    FullRound
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
    public WeaponSizeCategory WeaponSize;   // Light, One-Handed, or Two-Handed
    public int DamageDice;      // Sides on damage die (e.g., 8 for d8)
    public int DamageCount;     // Number of damage dice (usually 1)
    public int BonusDamage;     // Flat bonus damage
    public int AttackRange;     // Legacy max range field: melee in squares, ranged in feet. Use ReachSquares for melee semantics.
    public bool IsLightWeapon;  // Light weapon (dagger, short sword) - reduces TWF penalties
    public bool IsTwoHanded;    // Two-handed weapon - can't be dual-wielded, 1.5x STR to damage
    public bool HasReach;       // Legacy reach flag (kept for backward compatibility)

    // --- D&D 3.5 Reach Mechanics ---
    public int ReachSquares;          // Melee reach in squares (1 = 5 ft, 2 = 10 ft, 3 = 15 ft)
    public bool CanAttackAdjacent;    // Whether this melee weapon can attack adjacent (distance 1)
    public bool IsReachWeapon;        // True for reach weapons (typically ReachSquares > 1)
    public bool DealsNonlethalDamage; // Whip and similar weapons can deal nonlethal damage
    public bool WhipLikeArmorRestriction; // Cannot harm targets with armor/natural armor bonus +1 or higher

    public string DamageType;   // Legacy display/source string ("slashing", "piercing", etc.)

    // --- Damage bypass/material/alignment properties (for DR interactions) ---
    public bool CountsAsMagicForBypass;    // Bypasses DR/magic
    public bool IsSilvered;                // Bypasses DR/silver
    public bool IsColdIron;                // Bypasses DR/cold iron
    public bool IsAdamantine;              // Bypasses DR/adamantine
    public bool IsAlignedGood;             // Bypasses DR/good
    public bool IsAlignedEvil;             // Bypasses DR/evil
    public bool IsAlignedLawful;           // Bypasses DR/lawful

    // --- Reloading (D&D 3.5 crossbows) ---
    public bool RequiresReload;              // True for crossbows that must be reloaded after firing
    public bool IsLoaded = true;             // Runtime state: starts loaded
    public ReloadActionType ReloadAction;    // Base reload action without Rapid Reload
    public bool IsAlignedChaotic;          // Bypasses DR/chaotic

    // --- Damage Modifier Properties (D&D 3.5) ---
    public DamageModifierType DmgModType;  // How STR (or other) applies to damage
    public int CompositeRating;            // For composite bows: max STR bonus allowed (0 = no bonus)
    public bool IsThrown;                  // Whether this weapon can be thrown (gets STR on throw)

    // --- Range Increment (D&D 3.5) ---
    // Max range = 5 × RangeIncrement for thrown weapons (IsThrown), 10 × RangeIncrement for projectile weapons.
    public int RangeIncrement;             // Range increment in feet (0 = melee only).

    // Compatibility aliases for gameplay/UI code that still uses explicit throwable naming.
    public bool IsThrowable { get => IsThrown; set => IsThrown = value; }
    public int ThrowRangeIncrement { get => RangeIncrement; set => RangeIncrement = value; }

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

    // --- Extensible tag metadata ---
    // Tags inherited by characters while this item is equipped.
    // Examples: "Light Armor", "Chain Shirt".
    public HashSet<string> VisualTags = new HashSet<string>();

    // --- Item durability (used by Sunder) ---
    public int EnhancementBonus;    // Magic enhancement bonus to durability (+2 hardness, +10 HP per +1)
    public int Hardness;            // Effective hardness after enhancement
    public int MaxHitPoints;        // Maximum object HP after enhancement
    public int CurrentHitPoints;    // Runtime durability HP
    public bool IsBroken;           // Broken at <= half max HP (until repaired)
    public bool IsDestroyed;        // Destroyed at <= 0 HP

    // --- Consumable ---
    public ConsumableEffectType ConsumableEffect; // Generic effect type for extensibility
    public string ConsumableSpellName;            // Spell name this consumable emulates (e.g., "Cure Light Wounds")
    public int ConsumableMinimumCasterLevel = 1;  // Potions use minimum caster level by default (D&D 3.5e)
    public int ConsumableModifier;                // Generic +X modifier for spell-derived consumables
    public int HealAmount;      // Legacy flat HP restore fallback
    public int HealDiceCount;   // Number of healing dice (e.g., 1 for 1d8)
    public int HealDiceSides;   // Sides per healing die (e.g., 8 for 1d8)
    public int HealBonus;       // Flat healing bonus (e.g., +1)

    // --- Visual ---
    public string IconChar;     // Unicode/emoji character for display (fallback icon)
    public Color IconColor;     // Color tint for the icon

    /// <summary>Create an empty/null item.</summary>
    public static ItemData Empty => null;


    /// <summary>True if this item is one of the supported crossbow weapon types.</summary>
    public bool IsCrossbowWeapon => IsWeapon && RequiresReload;

    /// <summary>
    /// Returns true if this weapon has a Rapid Reload feat variant keyed by this weapon type.
    /// </summary>
    public bool IsRapidReloadSupportedCrossbow
    {
        get
        {
            if (!IsCrossbowWeapon) return false;
            string id = (Id ?? string.Empty).ToLowerInvariant();
            return id.Contains("crossbow_light")
                || id.Contains("crossbow_heavy")
                || id.Contains("crossbow_hand")
                || id.Contains("crossbow_repeating");
        }
    }

    /// <summary>
    /// Get the feat name that applies Rapid Reload to this crossbow.
    /// Returns empty for non-crossbows or unsupported crossbow variants.
    /// </summary>
    public string GetRapidReloadFeatName()
    {
        if (!IsCrossbowWeapon) return string.Empty;

        string id = (Id ?? string.Empty).ToLowerInvariant();
        if (id.Contains("crossbow_light")) return "Rapid Reload (Light Crossbow)";
        if (id.Contains("crossbow_heavy")) return "Rapid Reload (Heavy Crossbow)";
        if (id.Contains("crossbow_hand")) return "Rapid Reload (Hand Crossbow)";
        if (id.Contains("crossbow_repeating")) return "Rapid Reload (Repeating Crossbow)";
        return string.Empty;
    }

    /// <summary>
    /// Get the effective reload action after applying Rapid Reload if the character has it for this weapon.
    /// </summary>
    public ReloadActionType GetEffectiveReloadAction(bool hasRapidReload)
    {
        ReloadActionType action = ReloadAction;
        if (!hasRapidReload) return action;

        if (action == ReloadActionType.FullRound) return ReloadActionType.MoveAction;
        if (action == ReloadActionType.MoveAction) return ReloadActionType.FreeAction;
        return action;
    }
    public bool IsWeapon => Type == ItemType.Weapon;
    public bool IsArmor => Type == ItemType.Armor;
    public bool IsShield => Type == ItemType.Shield;
    public bool IsConsumable => Type == ItemType.Consumable;

    public bool IsSunderable => IsWeapon || IsArmor || IsShield;

    /// <summary>
    /// Ensure durability stats are initialized for sunderable items.
    /// Durability persists on the item once initialized.
    /// </summary>
    public void EnsureDurabilityInitialized()
    {
        if (!IsSunderable)
            return;

        if (MaxHitPoints > 0 && Hardness > 0)
        {
            if (CurrentHitPoints <= 0 && !IsDestroyed)
                CurrentHitPoints = MaxHitPoints;
            return;
        }

        int baseHardness = GetBaseHardness();
        int baseHp = GetBaseHitPoints();
        int enhancement = Mathf.Max(0, ResolveEnhancementBonus());

        Hardness = baseHardness + (enhancement * 2);
        MaxHitPoints = baseHp + (enhancement * 10);
        CurrentHitPoints = Mathf.Clamp(CurrentHitPoints <= 0 ? MaxHitPoints : CurrentHitPoints, 0, MaxHitPoints);
        IsBroken = CurrentHitPoints > 0 && CurrentHitPoints <= Mathf.Max(1, MaxHitPoints / 2);
        IsDestroyed = CurrentHitPoints <= 0;
    }

    public int ResolveEnhancementBonus()
    {
        if (EnhancementBonus > 0)
            return EnhancementBonus;

        if (string.IsNullOrEmpty(Name))
            return 0;

        int plusIndex = Name.IndexOf('+');
        if (plusIndex < 0 || plusIndex >= Name.Length - 1)
            return 0;

        int cursor = plusIndex + 1;
        int parsed = 0;
        while (cursor < Name.Length && char.IsDigit(Name[cursor]))
        {
            parsed = (parsed * 10) + (Name[cursor] - '0');
            cursor++;
        }

        return Mathf.Max(0, parsed);
    }

    public int ApplySunderDamage(int incomingDamage, out int effectiveDamage, out int hpBefore, out int hpAfter)
    {
        EnsureDurabilityInitialized();

        hpBefore = CurrentHitPoints;
        effectiveDamage = Mathf.Max(0, incomingDamage - Mathf.Max(0, Hardness));

        if (effectiveDamage > 0)
            CurrentHitPoints = Mathf.Max(0, CurrentHitPoints - effectiveDamage);

        hpAfter = CurrentHitPoints;
        IsDestroyed = CurrentHitPoints <= 0;
        IsBroken = !IsDestroyed && CurrentHitPoints <= Mathf.Max(1, MaxHitPoints / 2);

        return effectiveDamage;
    }

    private int GetBaseHardness()
    {
        if (IsWeapon || IsShield)
            return 10;

        if (IsArmor)
            return ArmorCat == ArmorCategory.Heavy ? 10 : 5;

        return 0;
    }

    private int GetBaseHitPoints()
    {
        if (IsWeapon)
        {
            if (WeaponSize == WeaponSizeCategory.Light || IsLightWeapon)
                return 2;
            if (WeaponSize == WeaponSizeCategory.TwoHanded || IsTwoHanded)
                return 10;
            return 5;
        }

        if (IsShield)
        {
            string id = (Id ?? string.Empty).ToLowerInvariant();
            string n = (Name ?? string.Empty).ToLowerInvariant();

            if (id.Contains("buckler") || n.Contains("buckler"))
                return 5;
            if (id.Contains("tower") || n.Contains("tower"))
                return 20;
            if (id.Contains("heavy") || n.Contains("heavy") || ShieldBonus >= 2)
                return 10;
            return 5;
        }

        if (IsArmor)
        {
            switch (ArmorCat)
            {
                case ArmorCategory.Light: return 10;
                case ArmorCategory.Medium: return 20;
                case ArmorCategory.Heavy: return 30;
                default: return 10;
            }
        }

        return 0;
    }

    /// <summary>Can this item be equipped in the given slot?</summary>
    public bool CanEquipIn(EquipSlot targetSlot)
    {
        if (Slot == EquipSlot.None) return false;
        if (Slot == targetSlot) return true;

        // Backward-compatible aliasing between legacy Armor and new ArmorRobe slot name.
        if ((Slot == EquipSlot.Armor && targetSlot == EquipSlot.ArmorRobe) ||
            (Slot == EquipSlot.ArmorRobe && targetSlot == EquipSlot.Armor))
            return true;

        // EitherHand items can go in LeftHand or RightHand.
        if (Slot == EquipSlot.EitherHand && (targetSlot == EquipSlot.LeftHand || targetSlot == EquipSlot.RightHand))
            return true;

        // Ring items can support either finger ring slot.
        if (Slot == EquipSlot.EitherRing && (targetSlot == EquipSlot.LeftRing || targetSlot == EquipSlot.RightRing))
            return true;

        return false;
    }

    /// <summary>Get parsed canonical damage types for this weapon.</summary>
    public HashSet<DamageType> GetDamageTypes()
    {
        return DamageTextUtils.ParseDamageTypes(DamageType);
    }

    /// <summary>
    /// Build bypass tags granted by this weapon (material/alignment and physical forms).
    /// Physical damage forms are included so DR/slashing, DR/piercing, etc. can be bypassed.
    /// </summary>
    public DamageBypassTag GetBypassTags()
    {
        DamageBypassTag tags = DamageBypassTag.None;
        var dmgTypes = GetDamageTypes();

        if (dmgTypes.Contains(global::DamageType.Bludgeoning)) tags |= DamageBypassTag.Bludgeoning;
        if (dmgTypes.Contains(global::DamageType.Piercing)) tags |= DamageBypassTag.Piercing;
        if (dmgTypes.Contains(global::DamageType.Slashing)) tags |= DamageBypassTag.Slashing;

        if (CountsAsMagicForBypass) tags |= DamageBypassTag.Magic;
        if (IsSilvered) tags |= DamageBypassTag.Silver;
        if (IsColdIron) tags |= DamageBypassTag.ColdIron;
        if (IsAdamantine) tags |= DamageBypassTag.Adamantine;
        if (IsAlignedGood) tags |= DamageBypassTag.Good;
        if (IsAlignedEvil) tags |= DamageBypassTag.Evil;
        if (IsAlignedLawful) tags |= DamageBypassTag.Lawful;
        if (IsAlignedChaotic) tags |= DamageBypassTag.Chaotic;

        if (WeaponCat == WeaponCategory.Ranged || RangeIncrement > 0)
            tags |= DamageBypassTag.Ranged;

        return tags;
    }
    /// <summary>Get a formatted critical hit range string (e.g., "19-20/×2").</summary>
    public string GetCritRangeString()
    {
        int threatMin = CritThreatMin > 0 ? CritThreatMin : 20;
        int mult = CritMultiplier > 0 ? CritMultiplier : 2;
        string range = threatMin < 20 ? $"{threatMin}-20" : "20";
        return $"{range}/×{mult}";
    }

    /// <summary>Get formatted melee reach description (e.g., "5 ft", "10 ft").</summary>
    public string GetReachDescription()
    {
        int reach = ReachSquares > 0 ? ReachSquares : Mathf.Max(1, AttackRange);
        return $"{reach * 5} ft";
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
                int maxIncrements = IsThrown ? 5 : 10;
                int maxRange = RangeIncrement * maxIncrements;
                int incSquares = RangeIncrement / 5;
                int maxSquares = maxRange / 5;

                string weaponType = IsThrown ? "thrown" : "projectile";
                stats += $"\nRange: {RangeIncrement} ft increment ({incSquares} sq), max {maxRange} ft ({maxSquares} sq) [{weaponType}]";
            }
            else if (WeaponCat == WeaponCategory.Melee)
            {
                int minReach = CanAttackAdjacent ? 1 : Mathf.Min(2, Mathf.Max(1, ReachSquares));
                int maxReach = ReachSquares > 0 ? ReachSquares : Mathf.Max(1, AttackRange);
                stats += $"\nReach: {GetReachDescription()} ({minReach}-{maxReach} sq)";
                if (!CanAttackAdjacent)
                    stats += " | Cannot attack adjacent";
            }

            if (RequiresReload)
            {
                string reloadLabel = ReloadAction == ReloadActionType.FullRound ? "Full-round"
                    : ReloadAction == ReloadActionType.MoveAction ? "Move"
                    : ReloadAction == ReloadActionType.FreeAction ? "Free"
                    : "None";
                string loadedLabel = IsLoaded ? "Loaded" : "Unloaded";
                stats += $"\nReload: {reloadLabel} | {loadedLabel}";
            }
            string props = "";
            if (WeaponSize == WeaponSizeCategory.Light) props += "Light, ";
            else if (WeaponSize == WeaponSizeCategory.OneHanded) props += "One-handed, ";
            else if (WeaponSize == WeaponSizeCategory.TwoHanded) props += "Two-handed, ";
            if (IsReachWeapon) props += "Reach, ";
            if (DealsNonlethalDamage) props += "Nonlethal, ";
            if (WhipLikeArmorRestriction) props += "Cannot harm armor/natural armor +1+, ";
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
        }
        else if (Type == ItemType.Shield)
        {
            stats = $"Shield Bonus: +{ShieldBonus}";
            if (MaxDexBonus >= 0) stats += $"\nMax Dex: +{MaxDexBonus}";
            if (ArmorCheckPenalty > 0) stats += $" | Check: -{ArmorCheckPenalty}";
            if (ArcaneSpellFailure > 0) stats += $"\nSpell Fail: {ArcaneSpellFailure}%";

            // D&D 3.5 shield bash profile (when present on this shield definition).
            if (DamageDice > 0 && DamageCount > 0)
            {
                string bashDmg = $"{DamageCount}d{DamageDice}";
                if (BonusDamage > 0) bashDmg += $"+{BonusDamage}";
                string dmgType = string.IsNullOrEmpty(DamageType) ? "bludgeoning" : DamageType;
                string prof = Proficiency == WeaponProficiency.None ? "Martial" : Proficiency.ToString();
                stats += $"\nShield Bash: {bashDmg} {dmgType} ({prof})";
            }
        }
        else if (Type == ItemType.Consumable)
        {
            if (ConsumableEffect == ConsumableEffectType.HealHP)
            {
                if (HealDiceCount > 0 && HealDiceSides > 0)
                {
                    string healExpr = $"{HealDiceCount}d{HealDiceSides}";
                    if (HealBonus > 0)
                        healExpr += $"+{HealBonus}";
                    stats = $"Heals: {healExpr} HP";
                }
                else if (HealAmount > 0)
                {
                    stats = $"Heals: {HealAmount} HP";
                }
            }
            else if (ConsumableEffect == ConsumableEffectType.SpellEffect)
            {
                string spellLabel = string.IsNullOrEmpty(ConsumableSpellName) ? "Unknown Spell" : ConsumableSpellName;
                stats = $"Spell Effect: {spellLabel}";

                if (ConsumableModifier != 0)
                    stats += $"\nModifier: {ConsumableModifier:+#;-#;0}";

                if (ConsumableMinimumCasterLevel > 0)
                    stats += $"\nCaster Level: {ConsumableMinimumCasterLevel}";
            }
            else if (HealAmount > 0)
            {
                // Backward-compatible fallback for legacy consumables.
                stats = $"Heals: {HealAmount} HP";
            }
        }

        if (IsSunderable)
        {
            EnsureDurabilityInitialized();
            string durabilityLine = $"Hardness: {Hardness} | HP: {CurrentHitPoints}/{MaxHitPoints}";
            if (IsDestroyed)
                durabilityLine += " (Destroyed)";
            else if (IsBroken)
                durabilityLine += " (Broken)";

            stats = string.IsNullOrEmpty(stats) ? durabilityLine : $"{stats}\n{durabilityLine}";
        }

        if (WeightLbs > 0f)
        {
            string weightLabel = WeightLbs == Mathf.Floor(WeightLbs)
                ? $"{WeightLbs:0} lbs"
                : $"{WeightLbs:0.##} lbs";
            stats = string.IsNullOrEmpty(stats) ? $"Weight: {weightLabel}" : $"{stats}\nWeight: {weightLabel}";
        }

        return stats;
    }
}