using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: Y
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_Y()
    {
        RegisterYethHound();
        RegisterYuantiAbomination();
        RegisterYuantiHalfblood();
        RegisterYuantiPureblood();

    }

    /// <summary>
    /// Yeth Hound (CR 3) — Medium outsider (evil, extraplanar).
    /// MM 3.5e p.260. 3 HD, fly 60 ft (good), bay (fear), trip.
    /// Damage reduction 5/silver.
    /// </summary>
    private static void RegisterYethHound()
    {
        Register(new NPCDefinition
        {
            Id = "yeth_hound",
            Name = "Yeth Hound",
            ChallengeRating = "3",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 17, WIS = 12, INT = 6, CHA = 10,
            BAB = 3,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8, // 40 ft., fly 60 ft. (good)
            BaseHitDieHP = 19, // 3d8+6
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Silver,
            HasTripAttack = true,
            TripAttackCheckBonus = 3, // Str +3 trip modifier
            HasScent = true,
            CreatureTags = new List<string> { "Outsider", "Evil", "Extraplanar", "MM35", "Fly" },
            Feats = new List<string> { "Improved Initiative", "Track" },
            SpecialAbilities = new List<string>
            {
                "Bay (Will DC 11 or panicked for 2d4 rounds; 300-ft. spread; sonic mind-affecting fear)",
                "Trip (free trip attempt on bite hit, Str +3)",
                "DR 5/silver",
                "Fly 60 ft. (good)",
                "Darkvision 60 ft.",
                "Scent",
                "Sinister Bite (only harmed by silver or magic weapons at night)",
                "Skills: Listen +7, Spot +7, Survival +7 (+11 tracking by scent)",
                "Alignment: Neutral Evil"
            },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.35f, 0.32f, 0.42f, 1f),
            PanelColor = new Color(0.12f, 0.1f, 0.18f, 0.85f),
            NameColor = new Color(0.78f, 0.75f, 0.9f),
            Description = "Monster Manual yeth hound (CR 3). Bite +6 (1d6+4), trip +3, bay (fear), fly 60 ft. (good), DR 5/silver. Outsider hunting hound. MM 3.5e p.260."
        });
    private static void RegisterYuantiAbomination()
    {
        Register(new NPCDefinition
        {
            Id = "yuan_ti_abomination",
            Name = "Yuan-ti Abomination",
            ChallengeRating = "7",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 9,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 19, DEX = 14, CON = 15, WIS = 18, INT = 18, CHA = 18,
            NaturalArmorBonus = 11,
            SpellResistance = 18,
            BaseSpeed = 6, // 30 ft, climb 20 ft, swim 20 ft
            BaseHitDieHP = 58,
            BAB = 9,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            Immunities = new CreatureImmunities { immuneToPoison = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Monstrous Humanoid", "Reptilian", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Dodge", "Expertise", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Poison (Ex): bite, DC 16 Fort, 1d6 Con/1d6 Con", "Improved Grab → Constrict 1d6+4", "SR 18", "Produce Acid (Sp): at will", "All halfblood SLAs at will", "Aversion (Sp): 1/day, DC 18 Will or shaken", "Blasphemy (Sp): 1/day", "Alternate Form: viper/hybrid", "Immune to poison", "Chameleon Power", "Detect Poison (at will)", "Darkvision 60 ft.", "Scent" },
            HasScent = true,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("scimitar", EquipSlot.MainHand),
                new EquipmentSlotPair("composite_longbow", EquipSlot.Ranged)
            },
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.3f, 0.42f, 0.22f, 1f),
            PanelColor = new Color(0.06f, 0.14f, 0.03f, 0.85f),
            NameColor = new Color(0.48f, 0.65f, 0.35f),
            Description = "Yuan-ti Abomination (CR 7). Massive snake-bodied yuan-ti with powerful spells and constriction. MM 3.5e p.265."
        });
    }

    private static void RegisterYuantiHalfblood()
    {
        Register(new NPCDefinition
        {
            Id = "yuan_ti_halfblood",
            Name = "Yuan-ti Halfblood",
            ChallengeRating = "5",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 7,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 17, DEX = 14, CON = 14, WIS = 16, INT = 18, CHA = 13,
            NaturalArmorBonus = 6,
            SpellResistance = 16,
            BaseSpeed = 6,
            BaseHitDieHP = 45,
            BAB = 7,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Monstrous Humanoid", "Reptilian", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Dodge", "Expertise" },
            SpecialAbilities = new List<string> { "Poison (Ex): bite, DC 15 Fort, 1d6 Con/1d6 Con", "SR 16", "Produce Acid (Sp): at will (pureblood SLAs + more)", "Animal Trance (Sp): at will", "Entangle (Sp): at will", "Suggestion (Sp): at will, DC 14", "Darkness (Sp): 3/day", "Fear (Sp): 3/day, DC 15", "Baleful Polymorph (Sp): 1/day, DC 16", "Alternate Form: viper", "Immune to poison", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("scimitar", EquipSlot.MainHand),
                new EquipmentSlotPair("composite_longbow", EquipSlot.Ranged)
            },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.35f, 0.48f, 0.28f, 1f),
            PanelColor = new Color(0.1f, 0.16f, 0.05f, 0.85f),
            NameColor = new Color(0.55f, 0.72f, 0.4f),
            Description = "Yuan-ti Halfblood (CR 5). Half-snake yuan-ti with poison and at-will spell-likes. MM 3.5e p.264."
        });
    }

    private static void RegisterYuantiPureblood()
    {
        Register(new NPCDefinition
        {
            Id = "yuan_ti_pureblood",
            Name = "Yuan-ti Pureblood",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 15, CON = 14, WIS = 12, INT = 18, CHA = 13,
            NaturalArmorBonus = 1,
            SpellResistance = 14,
            BaseSpeed = 6,
            BaseHitDieHP = 26,
            BAB = 4,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Monstrous Humanoid", "Reptilian", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight" },
            SpecialAbilities = new List<string> { "SR 14", "Detect Poison (Sp): at will", "Animal Trance (Sp): 1/day, DC 13", "Entangle (Sp): 1/day, DC 13", "Suggestion (Sp): 1/day, DC 14", "Cause Fear (Sp): 1/day, DC 12", "Alternate Form: viper", "Immune to poison", "Chameleon Power: +8 Hide", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("scimitar", EquipSlot.MainHand),
                new EquipmentSlotPair("shortbow", EquipSlot.Ranged)
            },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.4f, 0.5f, 0.3f, 1f),
            PanelColor = new Color(0.12f, 0.18f, 0.06f, 0.85f),
            NameColor = new Color(0.62f, 0.78f, 0.45f),
            Description = "Yuan-ti Pureblood (CR 3). Most human-looking yuan-ti with spell-like abilities. MM 3.5e p.264."
        });
    }

    }
}
