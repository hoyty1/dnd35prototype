using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: T
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_T()
    {
        RegisterTroglodyte();
        RegisterTroll();
        RegisterThoqqua();
    }

    /// <summary>
    /// Troglodyte (CR 1) — MM 3.5e p.246. Reptilian humanoid with stench aura.
    /// 2d8+4 HP (13), 2 claws 1d4+1, bite 1d4, javelin 1d6+1. Stench DC 13 Fort or sickened 10 rounds.
    /// </summary>
    private static void RegisterTroglodyte()
    {
        Register(new NPCDefinition
        {
            Id = "troglodyte",
            Name = "Troglodyte",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            HitDice = 2,
            BABOverride = BABProgression.Medium,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 12, DEX = 10, CON = 14, WIS = 10, INT = 8, CHA = 10,
            NaturalArmorBonus = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 13,
            StenchAuraDC = 13,
            StenchAuraRange = 30,
            CreatureTags = new List<string> { "Humanoid", "Reptilian", "MM35" },
            Feats = new List<string> { "Multiattack" },
            SpecialAbilities = new List<string> { "Stench (DC 13 Fort, sickened 10 rounds, 30 ft.)", "Darkvision 90 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.42f, 0.55f, 0.35f, 1f),
            PanelColor = new Color(0.15f, 0.22f, 0.1f, 0.85f),
            NameColor = new Color(0.7f, 0.9f, 0.6f),
            Description = "Troglodyte (CR 1). Reptilian humanoid with nauseating stench aura. Claw/claw/bite. MM 3.5e p.246."
        });
    }

    /// <summary>
    /// Troll (CR 5) — MM 3.5e p.247. Large regenerating brute with rend.
    /// 6d8+36 HP (63), 2 claws 1d6+6, bite 1d6+3. Regeneration 5 (fire/acid).
    /// </summary>
    private static void RegisterTroll()
    {
        Register(new NPCDefinition
        {
            Id = "troll",
            Name = "Troll",
            ChallengeRating = "5",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 23, DEX = 14, CON = 23, WIS = 9, INT = 6, CHA = 6,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            BaseSpeed = 6, // 30 ft.
            BaseHitDieHP = 63,
            RegenerationAmount = 5,
            RegenerationSuppressedBy = DamageBypassTag.None, // Fire and Acid suppress — using damage type tracking
            HasScent = true,
            CreatureTags = new List<string> { "Giant", "MM35" },
            Feats = new List<string> { "Alertness", "Iron Will", "Track" },
            SpecialAbilities = new List<string> { "Regeneration 5 (fire/acid)", "Rend (2d6+9 if both claws hit)", "Darkvision 90 ft.", "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.35f, 0.52f, 0.32f, 1f),
            PanelColor = new Color(0.12f, 0.2f, 0.1f, 0.85f),
            NameColor = new Color(0.6f, 0.85f, 0.55f),
            Description = "Troll (CR 5). Regenerating giant. Claw/claw/bite + rend. Regeneration 5, suppressed by fire or acid. MM 3.5e p.247."
        });
    }

    /// <summary>
    /// Thoqqua (CR 2) — Medium elemental (earth, fire).
    /// MM 3.5e p.242. Worm-like burrower. Body heat deals fire damage to grapplers/attackers.
    /// 3d8+3 HP (16), slam 1d6+3 + 2d6 fire.
    /// </summary>
    private static void RegisterThoqqua()
    {
        Register(new NPCDefinition
        {
            Id = "thoqqua",
            Name = "Thoqqua",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Elemental",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 15, DEX = 13, CON = 13, WIS = 12, INT = 6, CHA = 10,
            NaturalArmorBonus = 4,
            BaseSpeed = 6, // 30 ft, burrow 20 ft
            BaseHitDieHP = 16,
            BAB = 2,
            DamageImmunities = new List<DamageType> { DamageType.Fire },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true, BonusDamage = 6 }
            },
            CreatureTags = new List<string> { "Elemental", "Earth", "Fire", "MM35" },
            Feats = new List<string> { "Alertness" },
            SpecialAbilities = new List<string> { "Heat (Ex): slam deals +2d6 fire", "Body Heat: melee attackers take 1d6 fire", "Burrow 20 ft", "Darkvision 60 ft.", "Tremorsense 60 ft.", "Immune to fire", "Vulnerable to cold" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.None,
            SpriteColor = new Color(0.9f, 0.4f, 0.15f, 1f),
            PanelColor = new Color(0.35f, 0.12f, 0.05f, 0.85f),
            NameColor = new Color(1f, 0.6f, 0.25f),
            Description = "Thoqqua (CR 2). Fire worm elemental. Body heat burns attackers. MM 3.5e p.242."
        });
    }
}
