using System.Collections.Generic;
using UnityEngine;

public static partial class NPCDatabase
{
    private static void RegisterCreatures_N()
    {
        RegisterNightmare();
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.15f, 0.1f, 0.15f, 1f),
            PanelColor = new Color(0.05f, 0.02f, 0.05f, 0.85f),
            NameColor = new Color(0.6f, 0.3f, 0.6f),
            Description = "Nightmare (CR 5). Flaming evil steed from lower planes. Fly, ethereal travel. MM 3.5e p.194."
        });
    }
}
