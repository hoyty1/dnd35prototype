using System.Collections.Generic;
using UnityEngine;

public static partial class NPCDatabase
{
    private static void RegisterCreatures_N()
    {
        RegisterNightmare();
        RegisterSpiritNaga();
    }

    /// <summary>
    /// Nightmare (CR 5) — Large outsider (evil, extraplanar).
    /// MM 3.5e p.194. 6d8+18 HP (45), bite 1d8+4 + 1d4 fire, 2 hooves 1d6+2 + 1d4 fire.
    /// Flaming hooves, smoke aura (sickened DC 16 Fort), astral/ethereal travel.
    /// Can carry a rider. Flies 90 ft (good).
    /// </summary>
    private static void RegisterNightmare()
    {
        Register(new NPCDefinition
        {
            Id = "nightmare",
            Name = "Nightmare",
            ChallengeRating = "5",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 18, DEX = 15, CON = 16, WIS = 13, INT = 13, CHA = 12,
            NaturalArmorBonus = 4,
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 45,
            BAB = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true,
                    BonusElementalDamageDice = 4, BonusElementalDamageCount = 1, BonusElementalDamageType = DamageType.Fire
                },
                new NaturalAttackDefinition
                {
                    Name = "Hoof", DamageDice = 6, DamageCount = 1, Count = 2,
                    BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false,
                    BonusElementalDamageDice = 4, BonusElementalDamageCount = 1, BonusElementalDamageType = DamageType.Fire
                }
            },
            StenchAuraDC = 16,
            StenchAuraRange = 15, // Smoke aura 15 ft
            CreatureTags = new List<string> { "Outsider", "Evil", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Improved Initiative", "Run" },
            SpecialAbilities = new List<string> { "Flaming Hooves (Su): +1d4 fire on all attacks", "Smoke (Su): 15 ft., Fort DC 16 or sickened for 1d6 minutes", "Astral Projection (Su): self + 1 rider", "Etherealness (Su): self + 1 rider", "Fly 90 ft. (good)", "Darkvision 60 ft." },
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.15f, 0.1f, 0.15f, 1f),
            PanelColor = new Color(0.05f, 0.02f, 0.05f, 0.85f),
            NameColor = new Color(0.6f, 0.3f, 0.6f),
            Description = "Nightmare (CR 5). Flaming evil steed from lower planes. Fly, ethereal travel. MM 3.5e p.194."
        });
    }

    /// <summary>
    /// Spirit Naga (CR 9) — Large aberration.
    /// MM 3.5e p.192. Serpentine creature with charming gaze, poison bite, and sorcerer spells.
    /// </summary>
    private static void RegisterSpiritNaga()
    {
        Register(new NPCDefinition
        {
            Id = "spirit_naga",
            Name = "Spirit Naga",
            ChallengeRating = "9",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 9,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false, // serpentine
            STR = 18, DEX = 17, CON = 16, WIS = 17, INT = 12, CHA = 17,
            NaturalArmorBonus = 5,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 76,
            BAB = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Bite", DamageDice = 4, DamageCount = 2, Count = 1,
                    BonusDamageSource = DamageBonusSource.Strength,
                    Range = 1, IsPrimary = true,
                    PoisonOnHitId = "phase_spider_poison" // similar CON poison
                }
            },
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Charming Gaze",
                Range = 30,
                EffectType = AuraEffectType.Fear,
                SaveDC = 17,
                SaveType = SavingThrowType.Will
            },
            CreatureTags = new List<string> { "Aberration", "Darkvision60", "MM35" },
            Feats = new List<string> { "Ability Focus (charming gaze)", "Alertness", "Combat Casting", "Eschew Materials", "Lightning Reflexes" },
            SpecialAbilities = new List<string>
            {
                "Charming Gaze (Su): 30 ft., Will DC 17 or charmed as charm person (CL 9th)",
                "Poison (Ex): bite, Fort DC 17, 1d8 Con/1d8 Con",
                "Spells: casts as 7th-level sorcerer",
                "Darkvision 60 ft."
            },
            AIProfileArchetype = NPCAIProfileArchetype.Caster,
            SpriteColor = new Color(0.4f, 0.55f, 0.3f, 1f),
            PanelColor = new Color(0.12f, 0.2f, 0.08f, 0.85f),
            NameColor = new Color(0.6f, 0.85f, 0.45f),
            Description = "Spirit Naga (CR 9). Serpentine aberration with charming gaze and poisonous bite. Casts as 7th-level sorcerer. MM 3.5e p.192."
        });
    }
}
