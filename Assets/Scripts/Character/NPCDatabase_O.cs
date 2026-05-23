using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: O
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_O()
    {
        RegisterOchreJelly();
        RegisterOwl();
    
        RegisterSummonOctopus();
    }

    /// <summary>
    /// Ochre Jelly (CR 5) — MM 3.5e p.202. Large ooze with acid and split ability.
    /// 6d10+36 HP (69), slam 2d4+3 + acid 1d4. Immune to electricity, splits on slashing/piercing.
    /// </summary>
    private static void RegisterOchreJelly()
    {
        Register(new NPCDefinition
        {
            Id = "ochre_jelly",
            Name = "Ochre Jelly",
            ChallengeRating = "5",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Ooze",
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 15, DEX = 1, CON = 22, WIS = 1, INT = 0, CHA = 1,
            NaturalArmorBonus = 0,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Slam", DamageDice = 4, DamageCount = 2, Count = 1,
                    BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true,
                    BonusElementalDamageDice = 4, BonusElementalDamageCount = 1, BonusElementalDamageType = DamageType.Acid
                }
            },
            BaseSpeed = 2, // 10 ft, climb 10 ft
            BaseHitDieHP = 69,
            IsMindless = true,
            DamageImmunities = new List<DamageType> { DamageType.Electricity },
            Engulf = new EngulfDefinition
            {
                ReflexSaveDC = 15,
                DamagePerRound = 8, // 1d4 acid + constriction
                DamageType = DamageType.Acid,
                EscapeDC = 15
            },
            CreatureTags = new List<string> { "Ooze", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Blindsight 60 ft.", "Acid (1d4)", "Constrict 2d4+2 + 1d4 acid", "Improved grab", "Split (slashing/piercing splits into two)", "Immunity to electricity", "Mindless" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.None,
            SpriteColor = new Color(0.85f, 0.65f, 0.2f, 0.8f),
            PanelColor = new Color(0.35f, 0.25f, 0.05f, 0.85f),
            NameColor = new Color(1f, 0.85f, 0.4f),
            Description = "Ochre Jelly (CR 5). Large ooze. Slam + acid + constrict. Splits when hit by slashing/piercing. MM 3.5e p.202."
        });
    }

    private static void RegisterOwl()
    {
        Register(new NPCDefinition
        {
            Id = "owl",
            Name = "Owl",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Tiny,
            IsTallCreature = false,
            STR = 4, DEX = 17, CON = 10, WIS = 14, INT = 2, CHA = 4,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Talons", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 2,
            BaseHitDieHP = 4,
            CreatureTags = new List<string> { "Animal", "MM35", "Fly" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Low-light vision", "Fly 40 ft (average)", "Skills: Listen +14, Move Silently +17, Spot +6" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.76f, 0.7f, 0.6f, 1f),
            PanelColor = new Color(0.2f, 0.16f, 0.12f, 0.85f),
            NameColor = new Color(0.94f, 0.9f, 0.84f),
            Description = "Monster Manual owl. Tiny aerial hunter with keen senses and low-light vision."
        });
    }



    private static void RegisterSummonOctopus()
    {
        Register(new NPCDefinition
        {
            Id = "octopus",
            Name = "Octopus",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 12, DEX = 15, CON = 11, WIS = 12, INT = 2, CHA = 3,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tentacles", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 5,
            BaseHitDieHP = 11,
            CreatureTags = new List<string> { "Animal", "Aquatic", "SummonBase" },
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Tentacles",
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.62f, 0.48f, 0.68f, 1f),
            PanelColor = new Color(0.18f, 0.13f, 0.23f, 0.85f),
            NameColor = new Color(0.88f, 0.8f, 0.95f),
            Description = "Summon Monster baseline octopus with improved-grab style control attack."
        });
    }

}
