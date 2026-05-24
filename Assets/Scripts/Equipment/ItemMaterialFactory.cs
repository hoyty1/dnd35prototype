using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
//  D&D 3.5e Item Material Factory
//  Creates masterwork and special-material variants of
//  existing weapon and armor items from ItemDatabase.
// ─────────────────────────────────────────────────────────────

public static class ItemMaterialFactory
{
    // ═══════════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Create a masterwork copy of the given base item (weapon or armor).
    /// D&D 3.5e PHB p.126: MW weapons +1 attack, MW armor -1 ACP.
    /// All special-material items are automatically masterwork.
    /// </summary>
    public static ItemData CreateMasterwork(ItemData baseItem)
    {
        if (baseItem == null) return null;
        var clone = CloneBase(baseItem);
        clone.IsMasterwork = true;
        clone.Id = $"mw_{baseItem.Id}";
        clone.Name = baseItem.Name; // FullDisplayName will prepend "Masterwork"
        clone.BasePriceGp = baseItem.BasePriceGp + MaterialProperties.GetMasterworkCost(baseItem);
        return clone;
    }

    /// <summary>
    /// Create a weapon made of a special material.
    /// Automatically masterwork (D&D 3.5e rule: special material weapons are MW).
    /// </summary>
    public static ItemData CreateMaterialWeapon(ItemData baseWeapon, ItemMaterialType material)
    {
        if (baseWeapon == null || (!baseWeapon.IsWeapon && !baseWeapon.IsAmmunition)) return null;
        if (!MaterialProperties.IsMaterialValidForItem(material, baseWeapon)) return null;

        var clone = CloneBase(baseWeapon);
        clone.IsMasterwork = true; // All special material weapons are masterwork
        clone.Material = MaterialProperties.GetWeaponMaterial(material, baseWeapon);

        string prefix = MaterialProperties.GetMaterialPrefix(material).ToLowerInvariant().Replace(" ", "_");
        clone.Id = $"{prefix}_{baseWeapon.Id}";
        clone.Name = baseWeapon.Name; // FullDisplayName handles prefix

        // Cost: base + masterwork + material (masterwork cost from central source)
        clone.BasePriceGp = baseWeapon.BasePriceGp + MaterialProperties.GetMasterworkCost(baseWeapon) + clone.Material.AdditionalCostGp;

        // Set legacy bypass flags for backward compatibility
        switch (material)
        {
            case ItemMaterialType.AlchemicalSilver:
                clone.IsSilvered = true;
                break;
            case ItemMaterialType.ColdIron:
                clone.IsColdIron = true;
                break;
            case ItemMaterialType.Adamantine:
                clone.IsAdamantine = true;
                break;
        }

        return clone;
    }

    /// <summary>
    /// Create an armor/shield made of a special material.
    /// Automatically masterwork.
    /// </summary>
    public static ItemData CreateMaterialArmor(ItemData baseArmor, ItemMaterialType material)
    {
        if (baseArmor == null || (!baseArmor.IsArmor && !baseArmor.IsShield)) return null;
        if (!MaterialProperties.IsMaterialValidForItem(material, baseArmor)) return null;

        var clone = CloneBase(baseArmor);
        clone.IsMasterwork = true;
        clone.Material = MaterialProperties.GetArmorMaterial(material, baseArmor);

        string prefix = MaterialProperties.GetMaterialPrefix(material).ToLowerInvariant().Replace(" ", "_");
        clone.Id = $"{prefix}_{baseArmor.Id}";
        clone.Name = baseArmor.Name;

        // Cost: base + masterwork + material (masterwork cost from central source)
        clone.BasePriceGp = baseArmor.BasePriceGp + MaterialProperties.GetMasterworkCost(baseArmor) + clone.Material.AdditionalCostGp;

        return clone;
    }

    // ═══════════════════════════════════════════════════════════
    //  Registration: Populate ItemDatabase with material variants
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Register all standard material variants in the ItemDatabase.
    /// Called once during initialization (after base items are registered).
    /// </summary>
    public static void RegisterAllMaterialVariants()
    {
        int count = 0;

        // --- Masterwork weapons ---
        foreach (string weaponId in CommonWeaponIds)
        {
            ItemData baseWeapon = ItemDatabase.Get(weaponId);
            if (baseWeapon == null || !baseWeapon.IsWeapon) continue;

            // Masterwork
            RegisterVariant(CreateMasterwork(baseWeapon), ref count);

            // Cold Iron (all metal weapons)
            RegisterVariant(CreateMaterialWeapon(baseWeapon, ItemMaterialType.ColdIron), ref count);

            // Alchemical Silver
            RegisterVariant(CreateMaterialWeapon(baseWeapon, ItemMaterialType.AlchemicalSilver), ref count);

            // Adamantine
            RegisterVariant(CreateMaterialWeapon(baseWeapon, ItemMaterialType.Adamantine), ref count);
        }

        // --- Darkwood weapons (wooden only) ---
        foreach (string woodenId in WoodenWeaponIds)
        {
            ItemData baseWeapon = ItemDatabase.Get(woodenId);
            if (baseWeapon == null) continue;
            RegisterVariant(CreateMaterialWeapon(baseWeapon, ItemMaterialType.Darkwood), ref count);
        }

        // --- Masterwork armor ---
        foreach (string armorId in CommonArmorIds)
        {
            ItemData baseArmor = ItemDatabase.Get(armorId);
            if (baseArmor == null) continue;

            // Masterwork
            RegisterVariant(CreateMasterwork(baseArmor), ref count);

            // Adamantine (all armor)
            RegisterVariant(CreateMaterialArmor(baseArmor, ItemMaterialType.Adamantine), ref count);

            // Mithral (metal armor only)
            if (baseArmor.ArmorMaterial == ArmorMaterialType.Metal || baseArmor.ArmorMaterial == ArmorMaterialType.Mixed)
                RegisterVariant(CreateMaterialArmor(baseArmor, ItemMaterialType.Mithral), ref count);
        }

        // --- Masterwork & material shields ---
        foreach (string shieldId in CommonShieldIds)
        {
            ItemData baseShield = ItemDatabase.Get(shieldId);
            if (baseShield == null) continue;

            RegisterVariant(CreateMasterwork(baseShield), ref count);

            // Mithral shields (metal only)
            if (baseShield.ArmorMaterial == ArmorMaterialType.Metal)
                RegisterVariant(CreateMaterialArmor(baseShield, ItemMaterialType.Mithral), ref count);

            // Darkwood shields (wooden only)
            if (baseShield.ArmorMaterial == ArmorMaterialType.NonMetal)
                RegisterVariant(CreateMaterialArmor(baseShield, ItemMaterialType.Darkwood), ref count);
        }

        // --- Masterwork ammunition ---
        foreach (string ammoId in CommonAmmoIds)
        {
            ItemData baseAmmo = ItemDatabase.Get(ammoId);
            if (baseAmmo == null) continue;

            RegisterVariant(CreateMasterwork(baseAmmo), ref count);
            RegisterVariant(CreateMaterialWeapon(baseAmmo, ItemMaterialType.ColdIron), ref count);
            RegisterVariant(CreateMaterialWeapon(baseAmmo, ItemMaterialType.AlchemicalSilver), ref count);
            RegisterVariant(CreateMaterialWeapon(baseAmmo, ItemMaterialType.Adamantine), ref count);
        }

        Debug.Log($"[ItemMaterialFactory] Registered {count} material variants.");
    }

    // ═══════════════════════════════════════════════════════════
    //  Loot Table Helpers
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Get an appropriate material weapon for the given CR.
    /// CR 1-3: 20% chance masterwork
    /// CR 4-6: 50% MW, 10% cold iron/silver
    /// CR 7-10: above + 10% mithral
    /// CR 11+: above + 5% adamantine
    /// </summary>
    public static ItemData GetRandomMaterialWeapon(string baseWeaponId, int cr)
    {
        ItemData baseWeapon = ItemDatabase.Get(baseWeaponId);
        if (baseWeapon == null) return null;

        float roll = Random.value;

        if (cr >= 11 && roll < 0.05f)
            return ItemDatabase.CloneItem($"adamantine_{baseWeaponId}") ?? ItemDatabase.CloneItem($"mw_{baseWeaponId}");

        if (cr >= 7 && roll < 0.10f)
            return ItemDatabase.CloneItem($"mw_{baseWeaponId}"); // Mithral weapons have no combat benefit

        if (cr >= 4 && roll < 0.10f)
        {
            string matId = Random.value < 0.5f ? $"cold_iron_{baseWeaponId}" : $"silver_{baseWeaponId}";
            return ItemDatabase.CloneItem(matId) ?? ItemDatabase.CloneItem($"mw_{baseWeaponId}");
        }

        float mwChance = cr >= 7 ? 0.50f : (cr >= 4 ? 0.50f : (cr >= 1 ? 0.20f : 0f));
        if (roll < mwChance)
            return ItemDatabase.CloneItem($"mw_{baseWeaponId}");

        return null; // Standard weapon
    }

    /// <summary>
    /// Get an appropriate material armor for the given CR.
    /// </summary>
    public static ItemData GetRandomMaterialArmor(string baseArmorId, int cr)
    {
        ItemData baseArmor = ItemDatabase.Get(baseArmorId);
        if (baseArmor == null) return null;

        float roll = Random.value;

        if (cr >= 11 && roll < 0.05f)
        {
            string adamId = $"adamantine_{baseArmorId}";
            return ItemDatabase.CloneItem(adamId) ?? ItemDatabase.CloneItem($"mw_{baseArmorId}");
        }

        if (cr >= 7 && roll < 0.10f)
        {
            string mithralId = $"mithral_{baseArmorId}";
            return ItemDatabase.CloneItem(mithralId) ?? ItemDatabase.CloneItem($"mw_{baseArmorId}");
        }

        float mwChance = cr >= 7 ? 0.50f : (cr >= 4 ? 0.50f : (cr >= 1 ? 0.20f : 0f));
        if (roll < mwChance)
            return ItemDatabase.CloneItem($"mw_{baseArmorId}");

        return null; // Standard
    }

    // ═══════════════════════════════════════════════════════════
    //  Item ID Lists
    // ═══════════════════════════════════════════════════════════

    private static readonly string[] CommonWeaponIds = new string[]
    {
        ItemIDs.LONGSWORD, ItemIDs.GREATSWORD, ItemIDs.SHORT_SWORD,
        ItemIDs.RAPIER, ItemIDs.SCIMITAR, ItemIDs.FALCHION,
        ItemIDs.BATTLEAXE, ItemIDs.GREATAXE, ItemIDs.HANDAXE,
        ItemIDs.WARHAMMER, ItemIDs.MACE_HEAVY, ItemIDs.MACE_LIGHT,
        ItemIDs.MORNINGSTAR, ItemIDs.FLAIL_LIGHT, ItemIDs.FLAIL_HEAVY,
        ItemIDs.HALBERD, ItemIDs.GLAIVE, ItemIDs.LANCE,
        ItemIDs.DAGGER, ItemIDs.SHORTBOW, ItemIDs.LONGBOW,
        ItemIDs.CROSSBOW_LIGHT, ItemIDs.CROSSBOW_HEAVY,
    };

    private static readonly string[] WoodenWeaponIds = new string[]
    {
        ItemIDs.CLUB, ItemIDs.QUARTERSTAFF,
        ItemIDs.SHORTBOW, ItemIDs.LONGBOW,
    };

    private static readonly string[] CommonArmorIds = new string[]
    {
        // Light
        ItemIDs.PADDED_ARMOR, ItemIDs.LEATHER_ARMOR, ItemIDs.STUDDED_LEATHER, ItemIDs.CHAIN_SHIRT,
        // Medium
        ItemIDs.HIDE_ARMOR, ItemIDs.SCALE_MAIL, ItemIDs.CHAINMAIL, ItemIDs.BREASTPLATE,
        // Heavy
        ItemIDs.SPLINT_MAIL, ItemIDs.BANDED_MAIL, ItemIDs.HALF_PLATE, ItemIDs.FULL_PLATE,
    };

    private static readonly string[] CommonShieldIds = new string[]
    {
        ItemIDs.SHIELD_LIGHT_WOODEN, ItemIDs.SHIELD_LIGHT_STEEL,
        ItemIDs.SHIELD_HEAVY_WOODEN, ItemIDs.SHIELD_HEAVY_STEEL,
    };

    private static readonly string[] CommonAmmoIds = new string[]
    {
        ItemIDs.AMMO_ARROW, ItemIDs.AMMO_BOLT, ItemIDs.AMMO_SLING_BULLET,
    };

    // ═══════════════════════════════════════════════════════════
    //  Runtime Convenience: Apply material to any existing item
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Apply a special material to an existing item instance at runtime.
    /// Does NOT create a clone — mutates the item in-place.
    /// Use for dynamic loot, scripted events, or editor tools.
    /// Returns true if the material was successfully applied.
    /// </summary>
    public static bool ApplyMaterial(ItemData item, ItemMaterialType material)
    {
        if (item == null) return false;
        if (material == ItemMaterialType.Standard) return false;
        if (!MaterialProperties.IsMaterialValidForItem(material, item)) return false;

        // All special material items are masterwork
        item.IsMasterwork = true;

        // Apply the correct material properties from the central source
        if (item.IsWeapon || item.AmmoType != AmmunitionType.None)
        {
            item.Material = MaterialProperties.GetWeaponMaterial(material, item);
        }
        else if (item.IsArmor || item.IsShield)
        {
            item.Material = MaterialProperties.GetArmorMaterial(material, item);
        }
        else
        {
            return false; // Material not applicable to this item type
        }

        // Recalculate cost: base + masterwork + material
        item.BasePriceGp += MaterialProperties.GetMasterworkCost(item) + item.Material.AdditionalCostGp;

        // Set legacy bypass flags for backward compatibility
        switch (material)
        {
            case ItemMaterialType.AlchemicalSilver:
                item.IsSilvered = true;
                break;
            case ItemMaterialType.ColdIron:
                item.IsColdIron = true;
                break;
            case ItemMaterialType.Adamantine:
                item.IsAdamantine = true;
                break;
        }

        return true;
    }

    // ═══════════════════════════════════════════════════════════
    //  Internal Helpers
    // ═══════════════════════════════════════════════════════════

    private static ItemData CloneBase(ItemData src)
    {
        return ItemDatabase.CloneItem(src.Id) ?? new ItemData
        {
            Id = src.Id,
            Name = src.Name,
            Description = src.Description,
            Type = src.Type,
            Slot = src.Slot
        };
    }

    private static void RegisterVariant(ItemData variant, ref int count)
    {
        if (variant == null) return;

        // Only register if not already present
        if (ItemDatabase.Get(variant.Id) != null) return;

        ItemDatabase.RegisterItem(variant);
        count++;
    }
}
