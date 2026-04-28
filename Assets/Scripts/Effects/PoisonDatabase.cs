using System.Collections.Generic;

/// <summary>
/// Complete D&D 3.5e DMG poison catalog (pp. 296-297).
/// Includes all core injury, contact, ingested, and inhaled poisons.
/// </summary>
public static class PoisonDatabase
{
    private static Dictionary<string, PoisonData> _poisons;

    public static void Initialize()
    {
        _poisons = new Dictionary<string, PoisonData>();

        // ========================================
        // INJURY POISONS (DMG p. 296-297)
        // ========================================

        _poisons["black_adder_venom"] = new PoisonData
        {
            Id = "black_adder_venom",
            Name = "Black Adder Venom",
            Type = PoisonType.Injury,
            FortitudeDC = 11,
            InitialDamage = Damage(AbilityType.CON, "1d6"),
            SecondaryDamage = Damage(AbilityType.CON, "1d6"),
            PriceInGold = 120,
            Description = "Venom from black adder snake."
        };

        _poisons["bloodroot"] = new PoisonData
        {
            Id = "bloodroot",
            Name = "Bloodroot",
            Type = PoisonType.Injury,
            FortitudeDC = 12,
            InitialDamage = Damage(AbilityType.CON, "0"),
            SecondaryDamage = new List<AbilityDamageEffect>
            {
                new AbilityDamageEffect { Ability = AbilityType.CON, DamageAmount = "1d4", IsDrain = false },
                new AbilityDamageEffect { Ability = AbilityType.WIS, DamageAmount = "1d4", IsDrain = false }
            },
            PriceInGold = 100,
            Description = "Reddish root poison causing confusion and weakness."
        };

        _poisons["blue_whinnis"] = new PoisonData
        {
            Id = "blue_whinnis",
            Name = "Blue Whinnis",
            Type = PoisonType.Injury,
            FortitudeDC = 14,
            InitialDamage = Damage(AbilityType.CON, "1"),
            SecondaryDamage = Damage(AbilityType.CON, "1"),
            PriceInGold = 120,
            Description = "Blue crystalline powder poison."
        };

        _poisons["carrion_crawler_brain_juice"] = new PoisonData
        {
            Id = "carrion_crawler_brain_juice",
            Name = "Carrion Crawler Brain Juice",
            Type = PoisonType.Injury,
            FortitudeDC = 13,
            InitialDamage = Damage(AbilityType.DEX, "10"),
            SecondaryDamage = Damage(AbilityType.DEX, "0"),
            PriceInGold = 200,
            Description = "Paralyzing toxin from carrion crawler tentacles; represented as severe Dexterity damage."
        };

        _poisons["dark_reaver_powder"] = new PoisonData
        {
            Id = "dark_reaver_powder",
            Name = "Dark Reaver Powder",
            Type = PoisonType.Injury,
            FortitudeDC = 18,
            InitialDamage = Damage(AbilityType.CON, "2d6"),
            SecondaryDamage = new List<AbilityDamageEffect>
            {
                new AbilityDamageEffect { Ability = AbilityType.CON, DamageAmount = "1d6", IsDrain = false },
                new AbilityDamageEffect { Ability = AbilityType.STR, DamageAmount = "1d6", IsDrain = false }
            },
            PriceInGold = 300,
            Description = "Deadly poison made from rare fungi and dark magic."
        };

        _poisons["deathblade"] = new PoisonData
        {
            Id = "deathblade",
            Name = "Deathblade",
            Type = PoisonType.Injury,
            FortitudeDC = 20,
            InitialDamage = Damage(AbilityType.CON, "1d6"),
            SecondaryDamage = Damage(AbilityType.CON, "2d6"),
            PriceInGold = 1800,
            Description = "One of the deadliest poisons, distilled from rare nightshade."
        };

        _poisons["giant_wasp_poison"] = new PoisonData
        {
            Id = "giant_wasp_poison",
            Name = "Giant Wasp Poison",
            Type = PoisonType.Injury,
            FortitudeDC = 18,
            InitialDamage = Damage(AbilityType.DEX, "1d6"),
            SecondaryDamage = Damage(AbilityType.DEX, "1d6"),
            PriceInGold = 210,
            Description = "Potent venom from giant wasp."
        };

        _poisons["greenblood_oil"] = new PoisonData
        {
            Id = "greenblood_oil",
            Name = "Greenblood Oil",
            Type = PoisonType.Injury,
            FortitudeDC = 13,
            InitialDamage = Damage(AbilityType.CON, "1"),
            SecondaryDamage = Damage(AbilityType.CON, "1d2"),
            PriceInGold = 100,
            Description = "Greenish, oily poison extracted from certain plants."
        };

        _poisons["large_scorpion_venom"] = new PoisonData
        {
            Id = "large_scorpion_venom",
            Name = "Large Scorpion Venom",
            Type = PoisonType.Injury,
            FortitudeDC = 14,
            InitialDamage = Damage(AbilityType.STR, "1d4"),
            SecondaryDamage = Damage(AbilityType.STR, "1d4"),
            PriceInGold = 200,
            Description = "Venom from large monstrous scorpion."
        };

        _poisons["medium_spider_poison"] = new PoisonData
        {
            Id = "medium_spider_poison",
            Name = "Medium Spider Venom",
            Type = PoisonType.Injury,
            FortitudeDC = 12,
            InitialDamage = Damage(AbilityType.STR, "1d3"),
            SecondaryDamage = Damage(AbilityType.STR, "1d3"),
            PriceInGold = 150,
            Description = "Venom from medium monstrous spider."
        };

        _poisons["purple_worm_poison"] = new PoisonData
        {
            Id = "purple_worm_poison",
            Name = "Purple Worm Poison",
            Type = PoisonType.Injury,
            FortitudeDC = 24,
            InitialDamage = Damage(AbilityType.STR, "1d6"),
            SecondaryDamage = Damage(AbilityType.STR, "2d6"),
            PriceInGold = 700,
            Description = "Extremely potent venom from purple worm."
        };

        _poisons["shadow_essence"] = new PoisonData
        {
            Id = "shadow_essence",
            Name = "Shadow Essence",
            Type = PoisonType.Injury,
            FortitudeDC = 17,
            InitialDamage = Damage(AbilityType.STR, "1d6", drain: true),
            SecondaryDamage = Damage(AbilityType.STR, "2d6", drain: true),
            PriceInGold = 250,
            Description = "Magical poison distilled from shadows; causes Strength drain."
        };

        _poisons["small_centipede_poison"] = new PoisonData
        {
            Id = "small_centipede_poison",
            Name = "Small Centipede Poison",
            Type = PoisonType.Injury,
            FortitudeDC = 11,
            InitialDamage = Damage(AbilityType.DEX, "1d2"),
            SecondaryDamage = Damage(AbilityType.DEX, "1d2"),
            PriceInGold = 90,
            Description = "Venom from small monstrous centipede."
        };

        _poisons["wyvern_poison"] = new PoisonData
        {
            Id = "wyvern_poison",
            Name = "Wyvern Poison",
            Type = PoisonType.Injury,
            FortitudeDC = 17,
            InitialDamage = Damage(AbilityType.CON, "2d6"),
            SecondaryDamage = Damage(AbilityType.CON, "2d6"),
            PriceInGold = 3000,
            Description = "Potent venom from wyvern stinger."
        };

        // ========================================
        // CONTACT POISONS (DMG p. 297)
        // ========================================

        _poisons["black_lotus_extract"] = new PoisonData
        {
            Id = "black_lotus_extract",
            Name = "Black Lotus Extract",
            Type = PoisonType.Contact,
            FortitudeDC = 20,
            InitialDamage = Damage(AbilityType.CON, "3d6"),
            SecondaryDamage = Damage(AbilityType.CON, "3d6"),
            PriceInGold = 4500,
            Description = "Deadly poison from the legendary black lotus flower."
        };

        _poisons["malyss_root_paste"] = new PoisonData
        {
            Id = "malyss_root_paste",
            Name = "Malyss Root Paste",
            Type = PoisonType.Contact,
            FortitudeDC = 16,
            InitialDamage = Damage(AbilityType.DEX, "1d4"),
            SecondaryDamage = Damage(AbilityType.DEX, "2d4"),
            PriceInGold = 500,
            Description = "Sticky paste from malyss root, causing numbness and weakness."
        };

        _poisons["nitharit"] = new PoisonData
        {
            Id = "nitharit",
            Name = "Nitharit",
            Type = PoisonType.Contact,
            FortitudeDC = 13,
            InitialDamage = Damage(AbilityType.CON, "0"),
            SecondaryDamage = Damage(AbilityType.CON, "3d6"),
            PriceInGold = 650,
            Description = "Contact poison with delayed but severe effect."
        };

        _poisons["sassone_leaf_residue"] = new PoisonData
        {
            Id = "sassone_leaf_residue",
            Name = "Sassone Leaf Residue",
            Type = PoisonType.Contact,
            FortitudeDC = 16,
            InitialDamage = Damage(AbilityType.CON, "2d12"),
            SecondaryDamage = Damage(AbilityType.CON, "1d6"),
            PriceInGold = 300,
            Description = "Dangerous residue from sassone leaves, causing sudden weakness."
        };

        _poisons["terinav_root"] = new PoisonData
        {
            Id = "terinav_root",
            Name = "Terinav Root",
            Type = PoisonType.Contact,
            FortitudeDC = 16,
            InitialDamage = Damage(AbilityType.DEX, "1d6"),
            SecondaryDamage = Damage(AbilityType.DEX, "2d6"),
            PriceInGold = 750,
            Description = "Poison from terinav root, causing muscle spasms and weakness."
        };

        // ========================================
        // INGESTED POISONS (DMG p. 297)
        // ========================================

        _poisons["arsenic"] = new PoisonData
        {
            Id = "arsenic",
            Name = "Arsenic",
            Type = PoisonType.Ingested,
            FortitudeDC = 13,
            InitialDamage = Damage(AbilityType.CON, "1"),
            SecondaryDamage = Damage(AbilityType.CON, "1d8"),
            PriceInGold = 120,
            Description = "Classic poison, tasteless white powder."
        };

        _poisons["dark_reaver_powder_ingested"] = new PoisonData
        {
            Id = "dark_reaver_powder_ingested",
            Name = "Dark Reaver Powder",
            Type = PoisonType.Ingested,
            FortitudeDC = 18,
            InitialDamage = Damage(AbilityType.CON, "2d6"),
            SecondaryDamage = new List<AbilityDamageEffect>
            {
                new AbilityDamageEffect { Ability = AbilityType.CON, DamageAmount = "1d6", IsDrain = false },
                new AbilityDamageEffect { Ability = AbilityType.STR, DamageAmount = "1d6", IsDrain = false }
            },
            PriceInGold = 300,
            Description = "Dark reaver powder prepared for ingestion (also available as an injury poison)."
        };

        _poisons["dragon_bile"] = new PoisonData
        {
            Id = "dragon_bile",
            Name = "Dragon Bile",
            Type = PoisonType.Ingested,
            FortitudeDC = 26,
            InitialDamage = Damage(AbilityType.STR, "3d6"),
            SecondaryDamage = Damage(AbilityType.STR, "0"),
            PriceInGold = 1500,
            Description = "Incredibly potent poison from dragon digestive system."
        };

        _poisons["id_moss"] = new PoisonData
        {
            Id = "id_moss",
            Name = "Id Moss",
            Type = PoisonType.Ingested,
            FortitudeDC = 14,
            InitialDamage = Damage(AbilityType.INT, "1d4"),
            SecondaryDamage = Damage(AbilityType.INT, "2d6"),
            PriceInGold = 125,
            Description = "Psychoactive moss that damages the mind."
        };

        _poisons["lich_dust"] = new PoisonData
        {
            Id = "lich_dust",
            Name = "Lich Dust",
            Type = PoisonType.Ingested,
            FortitudeDC = 17,
            InitialDamage = Damage(AbilityType.STR, "2d6"),
            SecondaryDamage = Damage(AbilityType.STR, "1d6"),
            PriceInGold = 250,
            Description = "Necrotic powder from ground lich bones."
        };

        _poisons["oil_of_taggit"] = new PoisonData
        {
            Id = "oil_of_taggit",
            Name = "Oil of Taggit",
            Type = PoisonType.Ingested,
            FortitudeDC = 15,
            InitialDamage = Damage(AbilityType.WIS, "0"),
            SecondaryDamage = Damage(AbilityType.WIS, "10"),
            PriceInGold = 90,
            Description = "Sleep-inducing poison; represented as severe Wisdom damage for unconsciousness."
        };

        _poisons["striped_toadstool"] = new PoisonData
        {
            Id = "striped_toadstool",
            Name = "Striped Toadstool",
            Type = PoisonType.Ingested,
            FortitudeDC = 11,
            InitialDamage = Damage(AbilityType.WIS, "1"),
            SecondaryDamage = new List<AbilityDamageEffect>
            {
                new AbilityDamageEffect { Ability = AbilityType.WIS, DamageAmount = "2d6", IsDrain = false },
                new AbilityDamageEffect { Ability = AbilityType.INT, DamageAmount = "1d4", IsDrain = false }
            },
            PriceInGold = 180,
            Description = "Toxic mushroom that damages the mind."
        };

        _poisons["ungol_dust"] = new PoisonData
        {
            Id = "ungol_dust",
            Name = "Ungol Dust",
            Type = PoisonType.Ingested,
            FortitudeDC = 15,
            InitialDamage = Damage(AbilityType.CHA, "1"),
            SecondaryDamage = new List<AbilityDamageEffect>
            {
                new AbilityDamageEffect { Ability = AbilityType.CHA, DamageAmount = "1d6", IsDrain = false },
                new AbilityDamageEffect { Ability = AbilityType.CHA, DamageAmount = "1", IsDrain = true }
            },
            PriceInGold = 1000,
            Description = "Rare poison that damages personality and life force."
        };

        // ========================================
        // INHALED POISONS (DMG p. 297)
        // ========================================

        _poisons["burnt_othur_fumes"] = new PoisonData
        {
            Id = "burnt_othur_fumes",
            Name = "Burnt Othur Fumes",
            Type = PoisonType.Inhaled,
            FortitudeDC = 18,
            InitialDamage = Damage(AbilityType.CON, "1", drain: true),
            SecondaryDamage = Damage(AbilityType.CON, "3d6"),
            PriceInGold = 2100,
            Description = "Toxic fumes causing Constitution drain and damage."
        };

        _poisons["insanity_mist"] = new PoisonData
        {
            Id = "insanity_mist",
            Name = "Insanity Mist",
            Type = PoisonType.Inhaled,
            FortitudeDC = 15,
            InitialDamage = Damage(AbilityType.WIS, "1d4"),
            SecondaryDamage = Damage(AbilityType.WIS, "2d6"),
            PriceInGold = 1500,
            Description = "Mind-altering gas that causes confusion and madness."
        };
    }

    public static PoisonData GetPoison(string id)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(id))
            return null;

        string key = id.Trim().ToLowerInvariant();
        return _poisons.TryGetValue(key, out PoisonData poison) ? poison : null;
    }

    public static IReadOnlyDictionary<string, PoisonData> GetAllPoisons()
    {
        EnsureInitialized();
        return _poisons;
    }

    public static List<PoisonData> GetPoisonsByType(PoisonType type)
    {
        EnsureInitialized();
        List<PoisonData> result = new List<PoisonData>();
        foreach (PoisonData poison in _poisons.Values)
        {
            if (poison.Type == type)
                result.Add(poison);
        }

        return result;
    }

    private static void EnsureInitialized()
    {
        if (_poisons == null)
            Initialize();
    }

    private static List<AbilityDamageEffect> Damage(AbilityType ability, string amount, bool drain = false)
    {
        return new List<AbilityDamageEffect>
        {
            new AbilityDamageEffect { Ability = ability, DamageAmount = amount, IsDrain = drain }
        };
    }
}
