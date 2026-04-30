using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: C
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_C()
    {
        RegisterMonstrousCentipedes();
    }

    private static void RegisterMonstrousCentipedes()
    {
        RegisterMonstrousCentipedeVariant("monstrous_centipede_tiny", "Monstrous Centipede (Tiny)", 1, SizeCategory.Tiny, 1, 15, 10, 3, 1, 20, 1, 3, 10, "1 Dex", "1 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_small", "Monstrous Centipede (Small)", 1, SizeCategory.Small, 2, 15, 10, 5, 1, 30, 1, 4, 10, "1d2 Dex", "1d2 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_medium", "Monstrous Centipede (Medium)", 1, SizeCategory.Medium, 4, 15, 10, 9, 2, 40, 1, 6, 10, "1d3 Dex", "1d3 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_large", "Monstrous Centipede (Large)", 3, SizeCategory.Large, 13, 15, 10, 13, 3, 40, 1, 8, 11, "1d4 Dex", "1d4 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_huge", "Monstrous Centipede (Huge)", 6, SizeCategory.Huge, 33, 15, 12, 17, 6, 40, 2, 6, 14, "1d6 Dex", "1d6 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_gargantuan", "Monstrous Centipede (Gargantuan)", 12, SizeCategory.Gargantuan, 66, 15, 12, 23, 10, 40, 2, 8, 17, "1d8 Dex", "1d8 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_colossal", "Monstrous Centipede (Colossal)", 24, SizeCategory.Colossal, 132, 13, 12, 27, 16, 40, 4, 6, 23, "2d6 Dex", "2d6 Dex");
    }

    private static void RegisterMonstrousCentipedeVariant(string id, string name, int hitDice, SizeCategory size, int hp, int dex, int con, int str, int naturalArmor, int speed, int damageCount, int damageDice, int poisonDc, string poisonInitial, string poisonSecondary)
    {
        Register(new NPCDefinition
        {
            Id = id,
            Name = name,
            ChallengeRating = id switch
            {
                "monstrous_centipede_tiny" => "1/8",
                "monstrous_centipede_small" => "1/4",
                "monstrous_centipede_medium" => "1/2",
                "monstrous_centipede_large" => "1",
                "monstrous_centipede_huge" => "2",
                "monstrous_centipede_gargantuan" => "6",
                "monstrous_centipede_colossal" => "8",
                _ => null
            },
            Level = Mathf.Max(1, hitDice),
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = Mathf.Max(1, hitDice),
            SizeCategory = size,
            IsTallCreature = false,
            STR = str, DEX = dex, CON = con, WIS = 10, INT = 1, CHA = 2,
            NaturalArmorBonus = naturalArmor,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = damageDice, DamageCount = damageCount, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = speed / 5,
            BaseHitDieHP = hp,
            CreatureTags = new List<string> { "Vermin", "MM35" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string>
            {
                $"Poison (Fort DC {poisonDc}; initial {poisonInitial}; secondary {poisonSecondary})",
                "Climb speed equals land speed",
                "Darkvision 60 ft",
                "Vermin traits",
                "Mindless"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.68f, 0.2f, 0.16f, 1f),
            PanelColor = new Color(0.22f, 0.08f, 0.07f, 0.85f),
            NameColor = new Color(0.96f, 0.78f, 0.74f),
            Description = $"Monster Manual {name.ToLowerInvariant()}. Poisonous vermin striker with climb mobility and vermin immunities."
        });
    }

}
