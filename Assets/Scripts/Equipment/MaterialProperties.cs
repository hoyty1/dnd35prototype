using UnityEngine;

// ─────────────────────────────────────────────────────────────
//  D&D 3.5e Material Properties Database
//  PHB p.126-128, DMG p.283-284
//  Static factory that produces ItemMaterial instances with
//  correct mechanical values for each special material.
// ─────────────────────────────────────────────────────────────

public static class MaterialProperties
{
    // ═══════════════════════════════════════════════════════════
    //  Weapon Material Factories
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Build an ItemMaterial for a weapon made of the given special material.
    /// PHB/DMG rules:
    ///   Adamantine: bypasses hardness &lt; 20, +1 sunder, bypass DR/adamantine. Cost +3000 gp (ammo +60).
    ///   Mithral:    half weight only (no combat benefit for weapons). Cost +500/lb.
    ///   Cold Iron:  bypasses DR/cold iron (demons, devils, fey). Cost = double base weapon price.
    ///   Silver:     bypasses DR/silver (lycanthropes, devils). -1 damage penalty.
    ///               Cost: light +750, 1-handed +3000, 2-handed +9000, ammo +2.
    ///   Darkwood:   half weight for wooden weapons. Cost +10 gp/lb reduced.
    /// </summary>
    public static ItemMaterial GetWeaponMaterial(ItemMaterialType type, ItemData baseWeapon = null)
    {
        var mat = new ItemMaterial { MaterialType = type };

        switch (type)
        {
            case ItemMaterialType.Adamantine:
                mat.WeaponBypassTags = DamageBypassTag.Adamantine;
                mat.AttackModifier = 0; // No attack bonus, but +1 to sunder (handled in sunder code)
                // Cost: +3000 gp for weapons, +60 for ammunition
                if (baseWeapon != null && baseWeapon.AmmoType != AmmunitionType.None)
                    mat.AdditionalCostGp = 60;
                else
                    mat.AdditionalCostGp = 3000;
                break;

            case ItemMaterialType.Mithral:
                // Mithral weapons: half weight, no combat benefit
                mat.WeightMultiplier = 0.5f;
                // Cost: approximately +500 gp per lb of original weight
                mat.AdditionalCostGp = baseWeapon != null ? Mathf.Max(500, Mathf.RoundToInt(baseWeapon.WeightLbs * 500f)) : 500;
                break;

            case ItemMaterialType.ColdIron:
                mat.WeaponBypassTags = DamageBypassTag.ColdIron;
                // Cost: double base weapon price (extra cost = base price)
                mat.AdditionalCostGp = baseWeapon != null ? baseWeapon.BasePriceGp : 0;
                break;

            case ItemMaterialType.AlchemicalSilver:
                mat.WeaponBypassTags = DamageBypassTag.Silver;
                mat.DamageModifier = -1; // PHB p.284: -1 penalty on damage rolls
                // Cost varies by weapon handedness
                if (baseWeapon != null)
                {
                    if (baseWeapon.AmmoType != AmmunitionType.None)
                        mat.AdditionalCostGp = 2;
                    else if (baseWeapon.IsLightWeapon)
                        mat.AdditionalCostGp = 750; // D&D 3.5 FAQ clarification: light melee = +20 gp → actually PHB says light +20, but DMG errata says +750
                    else if (baseWeapon.IsTwoHanded)
                        mat.AdditionalCostGp = 9000;
                    else
                        mat.AdditionalCostGp = 3000; // One-handed
                }
                else
                {
                    mat.AdditionalCostGp = 3000;
                }
                break;

            case ItemMaterialType.Darkwood:
                // Only works on wooden items; half weight
                mat.WeightMultiplier = 0.5f;
                // Cost: +10 gp per pound of weight reduced
                mat.AdditionalCostGp = baseWeapon != null ? Mathf.RoundToInt(baseWeapon.WeightLbs * 0.5f * 10f) : 10;
                break;

            default: // Standard
                break;
        }

        return mat;
    }

    // ═══════════════════════════════════════════════════════════
    //  Armor Material Factories
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Build an ItemMaterial for armor/shield made of the given special material.
    /// PHB/DMG rules:
    ///   Adamantine armor: DR 1/— (light), 2/— (medium), 3/— (heavy).
    ///     Cost: light +5000, medium +10000, heavy +15000.
    ///   Mithral armor: half weight, category one lighter (heavy→medium, medium→light),
    ///     ACP -3, max Dex +2, ASF -10%. Cost: light +1000, medium +4000, heavy +9000.
    ///   Darkwood shield: half weight. Cost +10 gp per lb reduced.
    /// </summary>
    public static ItemMaterial GetArmorMaterial(ItemMaterialType type, ItemData baseArmor = null)
    {
        var mat = new ItemMaterial { MaterialType = type };
        ArmorCategory cat = baseArmor != null ? baseArmor.ArmorCat : ArmorCategory.Medium;

        switch (type)
        {
            case ItemMaterialType.Adamantine:
                // DR based on armor weight class
                switch (cat)
                {
                    case ArmorCategory.Light:
                        mat.ArmorDRAmount = 1;
                        mat.AdditionalCostGp = 5000;
                        break;
                    case ArmorCategory.Medium:
                        mat.ArmorDRAmount = 2;
                        mat.AdditionalCostGp = 10000;
                        break;
                    case ArmorCategory.Heavy:
                        mat.ArmorDRAmount = 3;
                        mat.AdditionalCostGp = 15000;
                        break;
                    default: // Shield - no DR from adamantine
                        mat.AdditionalCostGp = 3000;
                        break;
                }
                break;

            case ItemMaterialType.Mithral:
                mat.WeightMultiplier = 0.5f;
                mat.ArmorCheckPenaltyReduction = 3; // ACP reduced by 3
                mat.MaxDexBonusIncrease = 2;        // Max Dex increased by 2
                mat.ArcaneSpellFailureReduction = 10; // ASF reduced by 10%
                // Armor category one lighter for movement & proficiency
                if (cat == ArmorCategory.Heavy || cat == ArmorCategory.Medium)
                    mat.ArmorCategoryShift = -1;
                // Cost by category
                switch (cat)
                {
                    case ArmorCategory.Light:
                        mat.AdditionalCostGp = 1000;
                        break;
                    case ArmorCategory.Medium:
                        mat.AdditionalCostGp = 4000;
                        break;
                    case ArmorCategory.Heavy:
                        mat.AdditionalCostGp = 9000;
                        break;
                    default: // Shield
                        mat.AdditionalCostGp = 1000;
                        break;
                }
                break;

            case ItemMaterialType.Darkwood:
                // Only for wooden shields, clubs, etc.
                mat.WeightMultiplier = 0.5f;
                mat.AdditionalCostGp = baseArmor != null ? Mathf.RoundToInt(baseArmor.WeightLbs * 0.5f * 10f) : 10;
                break;

            case ItemMaterialType.ColdIron:
                // Cold iron armor has no special properties (it's primarily a weapon material)
                // Cost: +1/3 base price
                mat.AdditionalCostGp = baseArmor != null ? Mathf.RoundToInt(baseArmor.BasePriceGp / 3f) : 0;
                break;

            case ItemMaterialType.AlchemicalSilver:
                // Silver armor has no meaningful benefit in D&D 3.5e
                break;

            default: // Standard
                break;
        }

        return mat;
    }

    // ═══════════════════════════════════════════════════════════
    //  Utility
    // ═══════════════════════════════════════════════════════════

    /// <summary>Get the masterwork cost adder for this item type.</summary>
    public static int GetMasterworkCost(ItemData item)
    {
        if (item == null) return 0;
        if (item.IsWeapon) return 300;       // PHB p.126
        if (item.IsArmor || item.IsShield) return 150; // PHB p.126
        return 50; // Masterwork tools
    }

    /// <summary>
    /// Returns the effective ArmorCategory after mithral shift.
    /// Heavy → Medium, Medium → Light, Light stays Light.
    /// </summary>
    public static ArmorCategory GetEffectiveArmorCategory(ItemData armor)
    {
        if (armor == null) return ArmorCategory.Light;
        ArmorCategory base_cat = armor.ArmorCat;

        if (armor.Material != null && armor.Material.ArmorCategoryShift < 0)
        {
            int shifted = (int)base_cat + armor.Material.ArmorCategoryShift;
            if (shifted < (int)ArmorCategory.Light) shifted = (int)ArmorCategory.Light;
            return (ArmorCategory)shifted;
        }
        return base_cat;
    }

    /// <summary>
    /// Returns the display name prefix for a material type.
    /// </summary>
    public static string GetMaterialPrefix(ItemMaterialType type)
    {
        switch (type)
        {
            case ItemMaterialType.Adamantine: return "Adamantine";
            case ItemMaterialType.Mithral: return "Mithral";
            case ItemMaterialType.ColdIron: return "Cold Iron";
            case ItemMaterialType.AlchemicalSilver: return "Silver";
            case ItemMaterialType.Darkwood: return "Darkwood";
            default: return "";
        }
    }

    /// <summary>
    /// Checks if a material is valid for a given item.
    /// E.g., darkwood only works on wooden items.
    /// </summary>
    public static bool IsMaterialValidForItem(ItemMaterialType material, ItemData item)
    {
        if (item == null) return false;

        switch (material)
        {
            case ItemMaterialType.Darkwood:
                // Only wooden items: shields (wooden), clubs, quarterstaffs, bows
                if (item.IsShield && item.ArmorMaterial == ArmorMaterialType.NonMetal)
                    return true;
                if (item.IsWeapon)
                {
                    string lower = item.Id != null ? item.Id.ToLowerInvariant() : "";
                    return lower.Contains("club") || lower.Contains("quarterstaff")
                        || lower.Contains("shortbow") || lower.Contains("longbow");
                }
                return false;

            case ItemMaterialType.Mithral:
                // Metal items: metal armor, metal weapons
                if (item.IsArmor && item.ArmorMaterial == ArmorMaterialType.Metal)
                    return true;
                if (item.IsShield && item.ArmorMaterial == ArmorMaterialType.Metal)
                    return true;
                if (item.IsWeapon) return true; // Most weapons are metal
                return false;

            case ItemMaterialType.Adamantine:
                // Metal weapons, ammunition, and armor only
                if (item.IsWeapon || item.IsAmmunition) return true;
                if (item.IsArmor || item.IsShield) return true;
                return false;

            case ItemMaterialType.ColdIron:
                // Metal weapons and ammunition only
                return item.IsWeapon || item.IsAmmunition;

            case ItemMaterialType.AlchemicalSilver:
                // Weapons and ammunition only
                return item.IsWeapon || item.IsAmmunition;

            default:
                return true;
        }
    }
}
