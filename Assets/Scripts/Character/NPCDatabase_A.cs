using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: A
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_A()
    {
        RegisterAllip();
        RegisterSummonApe();
        RegisterAboleth();
        RegisterAverageSalamander();
        RegisterAnnis();

    }

    /// <summary>
    /// Allip (CR 3) — MM 3.5e p.10. Incorporeal undead with Wisdom drain and babble aura.
    /// 4d12 HP (26), incorporeal touch +3 (1d4 Wis drain). Babble: DC 15 Will or hypnotized (fascinated).
    /// </summary>
    private static void RegisterAllip()
    {
        Register(new NPCDefinition
        {
            Id = "allip",
            Name = "Allip",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 0, DEX = 12, CON = 0, WIS = 11, INT = 11, CHA = 18,
            NaturalArmorBonus = 0,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Incorporeal Touch", DamageDice = 4, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true,
                    AbilityDrainType = AbilityType.WIS, AbilityDrainAmount = 1
                }
            },
            BaseSpeed = 6, // Fly 30 ft (perfect)
            BaseHitDieHP = 26,
            IsIncorporeal = true,
            IsMindless = false,
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Babble",
                SaveDC = 16,
                IsWillSave = true,
                RangeFeet = 60,
                Effect = AuraEffectType.Fascinated,
                DurationRounds = 3
            },
            CreatureTags = new List<string> { "Undead", "Incorporeal", "MM35" },
            Feats = new List<string> { "Improved Initiative", "Lightning Reflexes" },
            SpecialAbilities = new List<string> { "Incorporeal", "Wisdom drain (1d4)", "Babble (DC 16 Will, hypnotism 2d4 rounds, 60 ft.)", "Madness (+4 CHA for save DC, -6 WIS)", "Darkvision 60 ft.", "Undead traits", "Fly 30 ft. (perfect)" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.4f, 0.35f, 0.55f, 0.5f),
            PanelColor = new Color(0.12f, 0.1f, 0.22f, 0.85f),
            NameColor = new Color(0.7f, 0.6f, 0.9f),
            Description = "Allip (CR 3). Incorporeal undead. Touch drains 1d4 WIS. Babble aura fascinates (DC 16 Will). MM 3.5e p.10."
        });
    }

    private static void RegisterSummonApe()
    {
        Register(new NPCDefinition
        {
            Id = "ape",
            Name = "Ape",
            ChallengeRating = "2",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 21, DEX = 15, CON = 14, WIS = 12, INT = 2, CHA = 7,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 6, // 30 ft., climb 30 ft.
            BaseHitDieHP = 29,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.32f, 0.25f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.09f, 0.85f),
            NameColor = new Color(0.95f, 0.84f, 0.75f),
            Description = "Ape. 2 claws +7 (1d6+5), bite +2 (1d6+2). +8 racial Climb. 10 ft. reach. MM 3.5e p.268."
        });
    private static void RegisterAboleth()
    {
        Register(new NPCDefinition
        {
            Id = "aboleth",
            Name = "Aboleth",
            ChallengeRating = "7",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 8,
            SizeCategory = SizeCategory.Huge,
            IsTallCreature = false,
            STR = 26, DEX = 12, CON = 20, WIS = 17, INT = 15, CHA = 17,
            NaturalArmorBonus = 7,
            BaseSpeed = 2, // 10 ft, swim 60 ft
            BaseHitDieHP = 76,
            BAB = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tentacle", DamageDice = 6, DamageCount = 1, Count = 4, BonusDamageSource = DamageBonusSource.Strength, Range = 3, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Aberration", "Aquatic", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Combat Casting", "Iron Will" },
            SpecialAbilities = new List<string> { "Slime (Ex): tentacle hit, DC 19 Fort or skin transforms in 1d4+1 rounds", "Enslave (Su): 3/day, Will DC 17, as dominate person, unlimited range on same plane", "Mucus Cloud (Ex): 1 ft. cloud in water, DC 19 Fort or breathe water only for 3 hours", "Psionics: Hypnotic Pattern 3/day, Mirage Arcana 3/day, Persistent Image 3/day, Programmed Image 3/day", "Darkvision 60 ft.", "Swim 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.3f, 0.45f, 0.5f, 1f),
            PanelColor = new Color(0.08f, 0.16f, 0.2f, 0.85f),
            NameColor = new Color(0.5f, 0.7f, 0.8f),
            Description = "Aboleth (CR 7). Ancient aquatic aberration with enslave and transformative slime. MM 3.5e p.8."
        });
    }

    private static void RegisterAverageSalamander()
    {
        Register(new NPCDefinition
        {
            Id = "average_salamander",
            Name = "Average Salamander",
            ChallengeRating = "6",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 7,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 16, DEX = 13, CON = 14, WIS = 15, INT = 14, CHA = 13,
            NaturalArmorBonus = 7,
            Immunities = new CreatureImmunities { immuneToFire = true },
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.Magic,
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 45,
            BAB = 7,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tail Slap", DamageDice = 6, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false, BonusElementalDamageDice = 6, BonusElementalDamageCount = 1, BonusElementalDamageType = DamageType.Fire }
            },
            CreatureTags = new List<string> { "Outsider", "Fire", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Cleave", "Multiattack", "Power Attack" },
            SpecialAbilities = new List<string> { "Heat (Ex): +1d6 fire on all melee attacks", "Constrict (Ex): 2d6+1 + 1d6 fire", "DR 10/magic", "Immune to fire", "Vulnerable to cold (×1.5 damage)", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("longspear", EquipSlot.MainHand)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.75f, 0.4f, 0.12f, 1f),
            PanelColor = new Color(0.28f, 0.12f, 0.02f, 0.85f),
            NameColor = new Color(0.92f, 0.6f, 0.2f),
            Description = "Average Salamander (CR 6). Fire outsider warrior with DR 10/magic and heat. MM 3.5e p.218."
        });
    }

    /// <summary>
    /// Annis (CR 6) — Large monstrous humanoid.
    /// MM 3.5e p.142. Powerful hag with rend and improved grab.
    /// 7d8+14 HP (45), 2 claws 1d6+6, bite 1d6+3.
    /// </summary>
    private static void RegisterAnnis()
    {
        Register(new NPCDefinition
        {
            Id = "annis",
            Name = "Annis",
            ChallengeRating = "6",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 7,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 25, DEX = 15, CON = 14, WIS = 13, INT = 13, CHA = 10,
            NaturalArmorBonus = 10,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 45,
            BAB = 7,
            DamageReductionAmount = 2,
            DamageReductionBypass = DamageBypassTag.Bludgeoning,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Claw",
            SpellResistance = 17,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Monstrous Humanoid", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Great Fortitude" },
            SpecialAbilities = new List<string> { "Improved Grab (Ex): claw hit starts grapple", "Rend (Ex): both claws hit → +2d6+10 damage", "Spell-Like Abilities: at will—disguise self, fog cloud; 3/day—alter self", "DR 2/bludgeoning", "SR 17", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.45f, 0.35f, 0.5f, 1f),
            PanelColor = new Color(0.15f, 0.1f, 0.2f, 0.85f),
            NameColor = new Color(0.7f, 0.55f, 0.8f),
            Description = "Annis (CR 6). Powerful hag with rend, improved grab, and DR. MM 3.5e p.142."
        });
    }

    }
}
