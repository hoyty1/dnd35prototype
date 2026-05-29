using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Monster Manual creatures: V
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_V()
    {
        RegisterViperTiny();
        RegisterViperSmall();
        RegisterViperMedium();
        RegisterViperLarge();
        RegisterViperHuge();
        RegisterVampireSpawn();
        RegisterVampire();
        RegisterVargouille();
        RegisterVioletFungus();

    }

    private static void RegisterViperTiny()
    {
        Register(new NPCDefinition
        {
            Id = "viper_tiny",
            Name = "Viper (Tiny)",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Tiny,
            IsTallCreature = false,
            STR = 4, DEX = 17, CON = 11, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 2, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 3,
            BaseHitDieHP = 1,
            CreatureTags = new List<string> { "Animal", "MM35", "Snake" },
            Feats = new List<string> { "Improved Initiative", "Weapon Finesse" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Poison (Fort DC 10; initial 1d6 Con; secondary 1d6 Con)", "Climb 15 ft", "Swim 15 ft", "Scent" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.42f, 0.72f, 0.34f, 1f),
            PanelColor = new Color(0.14f, 0.24f, 0.11f, 0.85f),
            NameColor = new Color(0.84f, 0.94f, 0.8f),
            Description = "Monster Manual tiny viper. Venomous familiar-scale snake with scent and mixed movement modes."
        });
    }

    private static void RegisterViperSmall()
    {
        Register(new NPCDefinition
        {
            Id = "viper_small",
            Name = "Viper (Small)",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 6, DEX = 17, CON = 11, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 2, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4,
            BaseHitDieHP = 4,
            CreatureTags = new List<string> { "Animal", "MM35", "Snake" },
            Feats = new List<string> { "Improved Initiative", "Weapon Finesse" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Poison (Fort DC 10; initial 1d6 Con; secondary 1d6 Con)", "Climb 20 ft", "Swim 20 ft", "Scent" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.38f, 0.68f, 0.3f, 1f),
            PanelColor = new Color(0.12f, 0.21f, 0.1f, 0.85f),
            NameColor = new Color(0.82f, 0.92f, 0.78f),
            Description = "Monster Manual small viper. Agile poisonous snake with scent and climbing/swimming mobility."
        });
    }

    private static void RegisterViperMedium()
    {
        Register(new NPCDefinition
        {
            Id = "viper_medium",
            Name = "Viper (Medium)",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 8, DEX = 17, CON = 11, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4,
            BaseHitDieHP = 9,
            CreatureTags = new List<string> { "Animal", "MM35", "Snake" },
            Feats = new List<string> { "Weapon Finesse" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Poison (Fort DC 11; initial 1d6 Con; secondary 1d6 Con)", "Climb 20 ft", "Swim 20 ft", "Scent" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.36f, 0.64f, 0.27f, 1f),
            PanelColor = new Color(0.11f, 0.19f, 0.09f, 0.85f),
            NameColor = new Color(0.8f, 0.9f, 0.76f),
            Description = "Monster Manual medium viper. Core serpent baseline for poison-focused animal encounters."
        });
    }

    private static void RegisterViperLarge()
    {
        Register(new NPCDefinition
        {
            Id = "viper_large",
            Name = "Viper (Large)",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 10, DEX = 17, CON = 11, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4,
            BaseHitDieHP = 13,
            CreatureTags = new List<string> { "Animal", "MM35", "Snake" },
            Feats = new List<string> { "Improved Initiative", "Weapon Finesse" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Poison (Fort DC 11; initial 1d6 Con; secondary 1d6 Con)", "Climb 20 ft", "Swim 20 ft", "Scent" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.34f, 0.6f, 0.25f, 1f),
            PanelColor = new Color(0.1f, 0.18f, 0.08f, 0.85f),
            NameColor = new Color(0.78f, 0.88f, 0.72f),
            Description = "Monster Manual large viper. Broad-bodied venomous snake suited for mid-tier wilderness fights."
        });
    }

    private static void RegisterViperHuge()
    {
        Register(new NPCDefinition
        {
            Id = "viper_huge",
            Name = "Viper (Huge)",
            ChallengeRating = "3",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 6,
            SizeCategory = SizeCategory.Huge,
            IsTallCreature = false,
            STR = 16, DEX = 15, CON = 13, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true, PoisonOnHitId = "huge_viper_poison" }
            },
            BaseSpeed = 4,
            BaseHitDieHP = 33,
            CreatureTags = new List<string> { "Animal", "MM35", "Snake" },
            Feats = new List<string> { "Improved Initiative", "Run", "Weapon Focus" },
            WeaponFocusChoice = "Bite",
            HasScent = true,
            SpecialAbilities = new List<string> { "Poison (Fort DC 14; initial 1d6 Con; secondary 1d6 Con)", "Climb 20 ft", "Swim 20 ft", "Scent", "Alignment: True Neutral" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.3f, 0.54f, 0.22f, 1f),
            PanelColor = new Color(0.09f, 0.16f, 0.07f, 0.85f),
            NameColor = new Color(0.76f, 0.86f, 0.7f),
            Description = "Monster Manual huge viper. High-HD giant serpent with potent Constitution poison and long reach."
        });
    }
    
    private static void RegisterVampireSpawn()
    {
        Register(new NPCDefinition
        {
            Id = "vampire_spawn",
            Name = "Vampire Spawn",
            ChallengeRating = "4",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 16, DEX = 14, CON = 0, WIS = 13, INT = 13, CHA = 14,
            NaturalArmorBonus = 3,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Silver,
            BaseSpeed = 6,
            BaseHitDieHP = 29,
            BAB = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true, EnergyDrainOnHit = 1, EnergyDrainRemovalDC = 14 }
            },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Electricity, Amount = 10 }
            },
            CreatureTags = new List<string> { "Undead", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Improved Initiative", "Lightning Reflexes", "Toughness" },
            SpecialAbilities = new List<string> { "Energy Drain (Su): 1 negative level on slam, DC 14 Fort", "Dominate (Su): DC 14 Will, as dominate person", "Blood Drain (Ex): Pin + 1d4 CON drain", "Fast Healing 2", "Gaseous Form (Su)", "Spider Climb (Ex)", "DR 5/silver", "Resist cold 10, electricity 10", "+2 turn resistance", "Darkvision 60 ft.", "Undead traits" },
            AIProfileArchetype = NPCAIProfileArchetype.UndeadTactical,
            SpriteColor = new Color(0.35f, 0.25f, 0.3f, 1f),
            PanelColor = new Color(0.15f, 0.08f, 0.12f, 0.85f),
            NameColor = new Color(0.7f, 0.4f, 0.5f),
            Description = "Vampire Spawn (CR 4). Lesser vampire with energy drain and domination. MM 3.5e p.253."
        });
    }

    /// <summary>
    /// Vampire (CR 7) — Medium undead (augmented humanoid).
    /// MM 3.5e p.250. Template applied to a 5th-level human fighter base.
    /// Energy drain, dominate person, blood drain, gaseous form escape,
    /// DR 10/silver+magic, resist cold/electricity 10, fast healing 5.
    /// Spellcasting: Wizard-like spell list (defensive/utility).
    /// </summary>
    private static void RegisterVampire()
    {
        Register(new NPCDefinition
        {
            Id = "vampire",
            Name = "Vampire",
            ChallengeRating = "7",
            Level = 7,
            CharacterClass = "Fighter",
            CreatureType = "Undead",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 7,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 22, DEX = 18, CON = 0, WIS = 16, INT = 17, CHA = 20,
            NaturalArmorBonus = 6,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.Silver,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Electricity, Amount = 10 }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 45,
            BAB = 5,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Slam",
            BloodDrain = new BloodDrainDefinition
            {
                AbilityDrainAmount = 1,
                AbilityType = AbilityType.CON,
                DamagePerRound = 0,
                AbilityDrainDice = 4
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true, EnergyDrainOnHit = 2, EnergyDrainRemovalDC = 18 }
            },
            Immunities = new CreatureImmunities { ImmuneToCriticalHits = true, ImmuneToMindAffecting = true, ImmuneToPoison = true },
            KnownSpellIds = new List<string>
            {
                SpellNames.CHARM_PERSON,
                SpellNames.SHIELD,
                SpellNames.MAGE_ARMOR,
                SpellNames.CAUSE_FEAR,
                SpellNames.MIRROR_IMAGE,
                SpellNames.INVISIBILITY,
                SpellNames.HOLD_PERSON,
                SpellNames.DOMINATE_PERSON,
                SpellNames.DISPEL_MAGIC,
                SpellNames.HASTE,
                SpellNames.DISPLACEMENT,
                SpellNames.GREATER_INVISIBILITY,
                SpellNames.CHARM_MONSTER
            },
            PreparedSpellSlotIds = new List<string>
            {
                SpellNames.SHIELD,
                SpellNames.CHARM_PERSON,
                SpellNames.CAUSE_FEAR,
                SpellNames.MIRROR_IMAGE,
                SpellNames.INVISIBILITY,
                SpellNames.HOLD_PERSON,
                SpellNames.DOMINATE_PERSON,
                SpellNames.DISPLACEMENT,
                SpellNames.GREATER_INVISIBILITY,
                SpellNames.CHARM_MONSTER
            },
            CreatureTags = new List<string> { "Undead", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Combat Reflexes", "Dodge", "Improved Initiative", "Lightning Reflexes", "Toughness" },
            SpecialAbilities = new List<string> { "Energy Drain (Su): slam bestows 2 negative levels, DC 18 Fort to remove", "Dominate (Su): DC 18 Will, as dominate person (CL 12)", "Blood Drain (Ex): grapple + pin, 1d4 CON drain/round", "Improved Grab (slam)", "Children of the Night (Su): 1d6+1 rat swarms or 1d4+1 bat swarms or 3d6 wolves", "Fast Healing 5", "Gaseous Form (Su): at will, as per spell", "Spider Climb (Ex): constant", "Alternate Form (Su): bat or dire bat", "DR 10/silver and magic", "Resist cold 10, electricity 10", "+4 turn resistance", "Darkvision 60 ft.", "Undead traits" },
            AIProfileArchetype = NPCAIProfileArchetype.Vampire,
            SpriteColor = new Color(0.3f, 0.15f, 0.2f, 1f),
            PanelColor = new Color(0.12f, 0.04f, 0.08f, 0.9f),
            NameColor = new Color(0.85f, 0.25f, 0.35f),
            Description = "Vampire (CR 7). Cunning undead predator with energy drain, domination, blood drain, and spellcasting. MM 3.5e p.250."
        });
    }

    private static void RegisterVargouille()
    {
        Register(new NPCDefinition
        {
            Id = "vargouille",
            Name = "Vargouille",
            ChallengeRating = "2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 13, CON = 12, WIS = 12, INT = 5, CHA = 8,
            NaturalArmorBonus = 0,
            BaseSpeed = 6, // Fly 30 ft (good) — no land speed
            BaseHitDieHP = 5,
            BAB = 1,
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Shriek",
                SaveDC = 12,
                IsWillSave = false, // Fort save
                RangeFeet = 60,
                Effect = AuraEffectType.Frightened, // Actually paralysis but closest approximation
                DurationRounds = 6
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Evil", "Extraplanar", "Fly30", "Darkvision60", "MM35" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Shriek (Su): 60 ft., Fort DC 12 or paralyzed 2d4 rounds", "Kiss (Su): transforms paralyzed victim into vargouille over 1d6 hours (remove disease stops)", "Poison (Ex): bite, DC 12 Fort, 1d2 Str/1d2 Str", "Flight only (no land speed)", "Darkvision 60 ft." },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.35f, 0.3f, 1f),
            PanelColor = new Color(0.15f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(0.72f, 0.55f, 0.48f),
            Description = "Vargouille (CR 2). Flying fiendish head with paralysing shriek and transformative kiss. MM 3.5e p.254."
        });
    }

    private static void RegisterVioletFungus()
    {
        Register(new NPCDefinition
        {
            Id = "violet_fungus",
            Name = "Violet Fungus",
            ChallengeRating = "3",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Plant",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 14, DEX = 8, CON = 16, WIS = 11, INT = 0, CHA = 9,
            NaturalArmorBonus = 4,
            IsMindless = true,
            BaseSpeed = 2, // 10 ft
            BaseHitDieHP = 15,
            BAB = 1,
            Immunities = new CreatureImmunities
            {
                immuneToMindAffecting = true,
                immuneToPoison = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tentacle", DamageDice = 6, DamageCount = 1, Count = 4, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Plant", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Poison (Ex): tentacle, DC 14 Fort, 1d4 Str + 1d4 Con / 1d4 Str + 1d4 Con", "Plant traits (immune to mind-affecting, poison, sleep, paralysis, polymorph, stun, crits)", "Low-light vision" },
            AIProfileArchetype = NPCAIProfileArchetype.UndeadMindless,
            SpriteColor = new Color(0.5f, 0.25f, 0.55f, 1f),
            PanelColor = new Color(0.2f, 0.08f, 0.22f, 0.85f),
            NameColor = new Color(0.75f, 0.4f, 0.82f),
            Description = "Violet Fungus (CR 3). Poisonous plant with long tentacles. MM 3.5e p.112."
        });
    }

}
