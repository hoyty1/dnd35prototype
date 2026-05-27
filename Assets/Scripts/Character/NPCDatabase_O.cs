using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

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
        RegisterOgre();
        RegisterOgreMage();
        RegisterOtyugh();
        RegisterOwlbear();
        RegisterOrcWarrior();

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
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.62f, 0.48f, 0.68f, 1f),
            PanelColor = new Color(0.18f, 0.13f, 0.23f, 0.85f),
            NameColor = new Color(0.88f, 0.8f, 0.95f),
            Description = "Summon Monster baseline octopus with improved-grab style control attack."
        });
    
    private static void RegisterOgre()
    {
        Register(new NPCDefinition
        {
            Id = "ogre",
            Name = "Ogre",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 21, DEX = 8, CON = 15, WIS = 10, INT = 6, CHA = 7,
            NaturalArmorBonus = 5,
            BaseSpeed = 6, // 30 ft (40 ft base, -10 armor)
            BaseHitDieHP = 29,
            BAB = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Giant", "Darkvision60", "MM35" },
            Feats = new List<string> { "Toughness", "Weapon Focus (greatclub)" },
            SpecialAbilities = new List<string> { "Darkvision 60 ft.", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("greatclub", EquipSlot.MainHand),
                new EquipmentSlotPair("hide_armor", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string> { "javelin" },
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.6f, 0.5f, 0.35f, 1f),
            PanelColor = new Color(0.22f, 0.18f, 0.1f, 0.85f),
            NameColor = new Color(0.85f, 0.7f, 0.5f),
            Description = "Ogre (CR 3). Large brute with greatclub. MM 3.5e p.198."
        });
    }

    private static void RegisterOgreMage()
    {
        Register(new NPCDefinition
        {
            Id = "ogre_mage",
            Name = "Ogre Mage",
            ChallengeRating = "8",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 5,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 19, DEX = 12, CON = 17, WIS = 14, INT = 14, CHA = 17,
            NaturalArmorBonus = 5,
            SpellResistance = 19,
            RegenerationAmount = 5,
            BaseSpeed = 6, // 30 ft (also fly 40 ft)
            BaseHitDieHP = 37,
            BAB = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Giant", "Shapechanger", "Fly40", "Darkvision90", "MM35" },
            Feats = new List<string> { "Combat Expertise", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Regeneration 5 (fire/acid)", "SR 19", "Flight (Su) 40 ft. (good)", "Darkvision 90 ft.", "Low-light vision", "Darkness (Sp) at will", "Invisibility (Sp) at will", "Charm Person (Sp) 1/day DC 14", "Cone of Cold (Sp) 1/day DC 18", "Gaseous Form (Sp) 1/day", "Polymorph (Sp) 1/day", "Sleep (Sp) 1/day DC 14" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("greatsword", EquipSlot.MainHand),
                new EquipmentSlotPair("chain_shirt", EquipSlot.Armor)
            },
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.35f, 0.45f, 0.6f, 1f),
            PanelColor = new Color(0.12f, 0.18f, 0.25f, 0.85f),
            NameColor = new Color(0.55f, 0.7f, 0.9f),
            Description = "Ogre Mage (CR 8). Intelligent giant with spell-like abilities, flight, and regeneration. MM 3.5e p.200."
        });
    }

    private static void RegisterOtyugh()
    {
        Register(new NPCDefinition
        {
            Id = "otyugh",
            Name = "Otyugh",
            ChallengeRating = "4",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 15, DEX = 10, CON = 13, WIS = 12, INT = 5, CHA = 6,
            NaturalArmorBonus = 8,
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 36,
            BAB = 4,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Tentacle",
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tentacle", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false, HasDiseaseOnHit = true, DiseaseOnHitType = DiseaseType.FilthFever }
            },
            CreatureTags = new List<string> { "Aberration", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Toughness" },
            SpecialAbilities = new List<string> { "Improved Grab", "Constrict 1d6+2", "Disease (Ex): filth fever, bite, DC 14 Fort", "Scent", "Darkvision 60 ft." },
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.45f, 0.4f, 0.3f, 1f),
            PanelColor = new Color(0.15f, 0.12f, 0.06f, 0.85f),
            NameColor = new Color(0.7f, 0.62f, 0.45f),
            Description = "Otyugh (CR 4). Trash-dwelling aberration with grabbing tentacles and disease. MM 3.5e p.204."
        });
    }

    private static void RegisterOwlbear()
    {
        Register(new NPCDefinition
        {
            Id = "owlbear",
            Name = "Owlbear",
            ChallengeRating = "4",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 5,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 21, DEX = 12, CON = 21, WIS = 12, INT = 2, CHA = 10,
            NaturalArmorBonus = 5,
            BaseSpeed = 6,
            BaseHitDieHP = 52,
            BAB = 5,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Claw",
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Track" },
            SpecialAbilities = new List<string> { "Improved Grab", "Scent", "Darkvision 60 ft.", "Low-light vision" },
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.55f, 0.45f, 0.3f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.08f, 0.85f),
            NameColor = new Color(0.82f, 0.7f, 0.48f),
            Description = "Owlbear (CR 4). Ferocious hybrid beast with improved grab. MM 3.5e p.206."
        });
    }

    /// <summary>
    /// Orc Warrior (CR 1/2) — Medium humanoid (orc), Warrior 1.
    /// MM 3.5e p.203. Aggressive humanoid with greataxe.
    /// </summary>
    private static void RegisterOrcWarrior()
    {
        Register(new NPCDefinition
        {
            Id = "orc_warrior",
            Name = "Orc Warrior",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 17, DEX = 11, CON = 12, WIS = 8, INT = 8, CHA = 6,
            NaturalArmorBonus = 0,
            BaseSpeed = 6, // 30 ft
            BaseHitDieHP = 5,
            BAB = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Orc", "MM35" },
            Feats = new List<string> { "Alertness" },
            SpecialAbilities = new List<string> { "Darkvision 60 ft.", "Light Sensitivity: dazzled in bright sunlight" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.SCALE_MAIL, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.GREATAXE, EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string> { ItemIDs.JAVELIN },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.45f, 0.55f, 0.35f, 1f),
            PanelColor = new Color(0.18f, 0.22f, 0.1f, 0.85f),
            NameColor = new Color(0.7f, 0.85f, 0.55f),
            Description = "Orc Warrior (CR 1/2). Aggressive humanoid with greataxe and light sensitivity. MM 3.5e p.203."
        });
    }
}

}
