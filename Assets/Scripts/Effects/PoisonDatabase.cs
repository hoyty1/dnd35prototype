using System.Collections.Generic;

/// <summary>
/// D&D 3.5e DMG poison catalog (core/common examples).
/// </summary>
public static class PoisonDatabase
{
    private static Dictionary<string, PoisonData> _poisons;

    public static void Initialize()
    {
        _poisons = new Dictionary<string, PoisonData>
        {
            ["small_centipede_poison"] = new PoisonData
            {
                Id = "small_centipede_poison",
                Name = "Small Centipede Poison",
                Type = PoisonType.Injury,
                FortitudeDC = 11,
                InitialDamage = Damage(AbilityType.DEX, "1d2"),
                SecondaryDamage = Damage(AbilityType.DEX, "1d2"),
                PriceInGold = 90,
                Description = "Venom from small monstrous centipede."
            },
            ["medium_spider_poison"] = new PoisonData
            {
                Id = "medium_spider_poison",
                Name = "Medium Spider Poison",
                Type = PoisonType.Injury,
                FortitudeDC = 12,
                InitialDamage = Damage(AbilityType.STR, "1d3"),
                SecondaryDamage = Damage(AbilityType.STR, "1d3"),
                PriceInGold = 150,
                Description = "Venom from medium monstrous spider."
            },
            ["greenblood_oil"] = new PoisonData
            {
                Id = "greenblood_oil",
                Name = "Greenblood Oil",
                Type = PoisonType.Injury,
                FortitudeDC = 13,
                InitialDamage = Damage(AbilityType.CON, "1"),
                SecondaryDamage = Damage(AbilityType.CON, "1d2"),
                PriceInGold = 100,
                Description = "Plant-derived injury poison."
            },
            ["bloodroot"] = new PoisonData
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
                Description = "Causes weakness and confusion on delayed onset."
            },
            ["blue_whinnis"] = new PoisonData
            {
                Id = "blue_whinnis",
                Name = "Blue Whinnis",
                Type = PoisonType.Injury,
                FortitudeDC = 14,
                InitialDamage = Damage(AbilityType.CON, "1"),
                SecondaryDamage = Damage(AbilityType.CON, "1"),
                PriceInGold = 120,
                Description = "Blue crystalline poison."
            },
            ["giant_wasp_poison"] = new PoisonData
            {
                Id = "giant_wasp_poison",
                Name = "Giant Wasp Poison",
                Type = PoisonType.Injury,
                FortitudeDC = 18,
                InitialDamage = Damage(AbilityType.DEX, "1d6"),
                SecondaryDamage = Damage(AbilityType.DEX, "1d6"),
                PriceInGold = 210,
                Description = "Potent giant wasp venom."
            },
            ["shadow_essence"] = new PoisonData
            {
                Id = "shadow_essence",
                Name = "Shadow Essence",
                Type = PoisonType.Injury,
                FortitudeDC = 17,
                InitialDamage = Damage(AbilityType.STR, "1d6", drain: true),
                SecondaryDamage = Damage(AbilityType.STR, "2d6", drain: true),
                PriceInGold = 250,
                Description = "Dark alchemical toxin; causes Strength drain."
            },
            ["black_lotus_extract"] = new PoisonData
            {
                Id = "black_lotus_extract",
                Name = "Black Lotus Extract",
                Type = PoisonType.Contact,
                FortitudeDC = 20,
                InitialDamage = Damage(AbilityType.CON, "3d6"),
                SecondaryDamage = Damage(AbilityType.CON, "3d6"),
                PriceInGold = 4500,
                Description = "Legendary deadly contact poison."
            },
            ["wyvern_poison"] = new PoisonData
            {
                Id = "wyvern_poison",
                Name = "Wyvern Poison",
                Type = PoisonType.Injury,
                FortitudeDC = 17,
                InitialDamage = Damage(AbilityType.CON, "2d6"),
                SecondaryDamage = Damage(AbilityType.CON, "2d6"),
                PriceInGold = 3000,
                Description = "Powerful venom from a wyvern sting."
            },
            ["purple_worm_poison"] = new PoisonData
            {
                Id = "purple_worm_poison",
                Name = "Purple Worm Poison",
                Type = PoisonType.Injury,
                FortitudeDC = 24,
                InitialDamage = Damage(AbilityType.STR, "1d6"),
                SecondaryDamage = Damage(AbilityType.STR, "2d6"),
                PriceInGold = 700,
                Description = "Extremely potent giant worm venom."
            },
            ["nitharit"] = new PoisonData
            {
                Id = "nitharit",
                Name = "Nitharit",
                Type = PoisonType.Contact,
                FortitudeDC = 13,
                InitialDamage = Damage(AbilityType.CON, "0"),
                SecondaryDamage = Damage(AbilityType.CON, "3d6"),
                PriceInGold = 650,
                Description = "Delayed severe contact poison."
            },
            ["arsenic"] = new PoisonData
            {
                Id = "arsenic",
                Name = "Arsenic",
                Type = PoisonType.Ingested,
                FortitudeDC = 13,
                InitialDamage = Damage(AbilityType.CON, "1"),
                SecondaryDamage = Damage(AbilityType.CON, "1d8"),
                PriceInGold = 120,
                Description = "Classic ingested poison."
            },
            ["burnt_othur_fumes"] = new PoisonData
            {
                Id = "burnt_othur_fumes",
                Name = "Burnt Othur Fumes",
                Type = PoisonType.Inhaled,
                FortitudeDC = 18,
                InitialDamage = Damage(AbilityType.CON, "1", drain: true),
                SecondaryDamage = Damage(AbilityType.CON, "3d6"),
                PriceInGold = 2100,
                Description = "Inhaled toxin causing Constitution drain then damage."
            }
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
