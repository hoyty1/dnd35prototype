using System.Collections.Generic;

/// <summary>
/// Shared base creature definitions used by multiple template factories.
/// Eliminates duplication of identical inline NPCDefinition blocks across
/// SkeletonFactory, ZombieFactory, and LycanthropeFactory.
/// Each method returns a fresh NPCDefinition representing the living base creature
/// before any template is applied.
/// </summary>
public static class BaseCreatureDefinitions
{
    /// <summary>
    /// Owlbear — Large magical beast, 5 HD. MM 3.5e p.206.
    /// Used by both SkeletonFactory and ZombieFactory.
    /// </summary>
    public static NPCDefinition Owlbear()
    {
        return new NPCDefinition
        {
            Id = "base_owlbear",
            Name = "Owlbear",
            HitDice = 5,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 21, DEX = 12, CON = 21, WIS = 12, INT = 2, CHA = 10,
            BAB = 5,
            BaseSpeed = 6, // 30 ft
            NaturalArmorBonus = 5,
            CreatureType = "MagicalBeast",
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Claw",
                    DamageDice = 6,
                    DamageCount = 1,
                    Count = 2,
                    BonusDamageSource = DamageBonusSource.Strength,
                    IsPrimary = true,
                    Range = 1
                },
                new NaturalAttackDefinition
                {
                    Name = "Bite",
                    DamageDice = 8,
                    DamageCount = 1,
                    Count = 1,
                    BonusDamageSource = DamageBonusSource.StrengthHalf,
                    IsPrimary = false,
                    Range = 1
                }
            },
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Claw"
        };
    }

    /// <summary>
    /// Minotaur — Large monstrous humanoid, 6 HD. MM 3.5e p.188.
    /// Used by both SkeletonFactory and ZombieFactory.
    /// </summary>
    public static NPCDefinition Minotaur()
    {
        return new NPCDefinition
        {
            Id = "base_minotaur",
            Name = "Minotaur",
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 19, DEX = 10, CON = 15, WIS = 10, INT = 7, CHA = 8,
            BAB = 6,
            BaseSpeed = 6, // 30 ft
            NaturalArmorBonus = 5,
            CreatureType = "MonstrousHumanoid",
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Gore",
                    DamageDice = 8,
                    DamageCount = 1,
                    Count = 1,
                    BonusDamageSource = DamageBonusSource.Strength,
                    IsPrimary = true,
                    Range = 1
                }
            }
        };
    }
}
