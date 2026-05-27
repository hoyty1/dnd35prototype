using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
//  D&D 3.5e Special Materials & Masterwork System
//  PHB p.126-128, DMG p.283-284
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Special material types from D&D 3.5e (PHB/DMG).
/// Standard = no special material, just normal steel/wood/leather.
/// </summary>
public enum ItemMaterialType
{
    Standard,       // Normal materials (steel, wood, leather, etc.)
    Adamantine,     // Hardest known metal - DR bypass, armor grants DR
    Mithral,        // Light silvery metal - half weight, lighter armor category
    ColdIron,       // Pure iron without heat - bypasses DR of demons/devils/fey
    AlchemicalSilver, // Silver-coated weapon - bypasses DR of lycanthropes/devils
    Darkwood        // Rare magical wood - half weight for wooden items
}

/// <summary>
/// Runtime material data attached to an item. Holds both the material type
/// and the computed mechanical effects for weapons and armor.
/// </summary>
[System.Serializable]
public class ItemMaterial
{
    public ItemMaterialType MaterialType = ItemMaterialType.Standard;

    // ── Weapon Effects ──
    /// <summary>Attack roll modifier from material (masterwork gives +1 via IsMasterwork, not here).</summary>
    public int AttackModifier;
    /// <summary>Damage roll modifier (alchemical silver gives -1).</summary>
    public int DamageModifier;
    /// <summary>DR bypass tags granted by this material on a weapon.</summary>
    public DamageBypassTag WeaponBypassTags = DamageBypassTag.None;

    // ── Armor Effects ──
    /// <summary>Armor check penalty reduction (masterwork -1 via IsMasterwork; mithral -3 additional).</summary>
    public int ArmorCheckPenaltyReduction;
    /// <summary>Max Dex bonus increase (mithral +2).</summary>
    public int MaxDexBonusIncrease;
    /// <summary>Arcane spell failure reduction in percentage points (mithral -10%).</summary>
    public int ArcaneSpellFailureReduction;
    /// <summary>Weight multiplier (mithral/darkwood = 0.5f).</summary>
    public float WeightMultiplier = 1f;
    /// <summary>
    /// Mithral armor: effective category is one lighter for movement/proficiency.
    /// -1 = one category lighter (Heavy→Medium, Medium→Light).
    /// 0 = no change.
    /// </summary>
    public int ArmorCategoryShift;
    /// <summary>DR granted by adamantine armor (1/— light, 2/— medium, 3/— heavy).</summary>
    public int ArmorDRAmount;

    // ── Cost ──
    /// <summary>Additional cost in GP added by this material.</summary>
    public int AdditionalCostGp;

    /// <summary>
    /// Creates a deep copy of this material data.
    /// </summary>
    public ItemMaterial Clone()
    {
        return new ItemMaterial
        {
            MaterialType = MaterialType,
            AttackModifier = AttackModifier,
            DamageModifier = DamageModifier,
            WeaponBypassTags = WeaponBypassTags,
            ArmorCheckPenaltyReduction = ArmorCheckPenaltyReduction,
            MaxDexBonusIncrease = MaxDexBonusIncrease,
            ArcaneSpellFailureReduction = ArcaneSpellFailureReduction,
            WeightMultiplier = WeightMultiplier,
            ArmorCategoryShift = ArmorCategoryShift,
            ArmorDRAmount = ArmorDRAmount,
            AdditionalCostGp = AdditionalCostGp
        };
    }
}
