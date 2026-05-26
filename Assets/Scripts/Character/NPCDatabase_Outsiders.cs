using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual outsider creatures — demons, devils, celestials, genies, etc.
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_Outsiders()
    {
        RegisterDretch();
        RegisterQuasit();
        RegisterImp();
        RegisterHoundArchon();
        RegisterBarghest();
        RegisterGreaterBarghest();
        RegisterBeardedDevil();
        RegisterChainDevil();
        RegisterErinyes();
        RegisterBabau();
        RegisterSuccubus();
        RegisterHellcat();
        RegisterBralani();
        RegisterDjinni();
        RegisterEfreeti();
        RegisterJanni();
        RegisterXill();
        RegisterFormianWorker();
        RegisterFormianTaskmaster();
        RegisterShadowMastiff();
        RegisterVargouille();
    }

    // ════════════════════════════════════════════════════════════
    //  Dretch — MM p.42
    //  Outsider (Chaotic, Evil, Extraplanar, Tanar'ri), Small, CR 2
    //  2d8+4 HP (13), 2 claws +4 melee (1d6+2), bite +2 melee (1d4+1)
    //  Str 14, Dex 10, Con 14, Int 5, Wis 11, Cha 11
    //  AC 14 (+1 size, +3 natural), DR 5/cold iron or good
    //  Stinking Cloud 1/day, Telepathy 100 ft.
    // ════════════════════════════════════════════════════════════
    private static void RegisterDretch()
    {
        Register(new NPCDefinition
        {
            Id = "dretch",
            Name = "Dretch",
            ChallengeRating = "2",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = true,
            STR = 14, DEX = 10, CON = 14, WIS = 11, INT = 5, CHA = 11,
            NaturalArmorBonus = 3,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.ColdIron | DamageBypassTag.Good,
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 13,
            BAB = 2,
            Immunities = new CreatureImmunities { immuneToElectricity = true, immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Evil", "Extraplanar", "Tanarri", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Multiattack" },
            SpecialAbilities = new List<string> { "Stinking Cloud (Sp): 1/day, DC 13 Fort", "Summon Demon: 35% chance to summon 1 dretch", "DR 5/cold iron or good", "Immune to electricity/poison", "Resist acid 10, cold 10, fire 10", "Telepathy 100 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.5f, 0.35f, 0.3f, 1f),
            PanelColor = new Color(0.2f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(0.78f, 0.55f, 0.48f),
            Description = "Dretch (CR 2). Lowly demon with stinking cloud. MM 3.5e p.42."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Quasit — MM p.46
    //  Outsider (Chaotic, Evil, Extraplanar), Tiny, CR 2
    //  3d8 HP (13), 2 claws +8 melee (1d3-1 + poison), bite +3 melee (1d4-1)
    //  Str 8, Dex 17, Con 10, Int 10, Wis 12, Cha 10
    //  AC 18 (+2 size, +3 Dex, +3 natural), DR 5/cold iron or good
    //  Poison: DC 13 Fort, 1d4 Dex/1d4 Dex
    //  Alternate form (bat, Small centipede, toad, wolf), Invisibility at will
    // ════════════════════════════════════════════════════════════
    private static void RegisterQuasit()
    {
        Register(new NPCDefinition
        {
            Id = "quasit",
            Name = "Quasit",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 3,
            SizeCategory = SizeCategory.Tiny,
            IsTallCreature = true,
            STR = 8, DEX = 17, CON = 10, WIS = 12, INT = 10, CHA = 10,
            NaturalArmorBonus = 3,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.ColdIron | DamageBypassTag.Good,
            BaseSpeed = 4, // 20 ft, fly 50 ft (perfect)
            BaseHitDieHP = 13,
            BAB = 3,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Evil", "Extraplanar", "Fly50", "Darkvision60", "MM35" },
            Feats = new List<string> { "Improved Initiative", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Poison (Ex): claw, DC 13 Fort, 1d4 Dex/1d4 Dex", "Alternate Form (Su): bat, Small centipede, toad, or wolf", "Invisibility (Su): at will, self only", "Cause Fear (Sp): at will, 30 ft., DC 11 Will", "Detect Good/Magic (Sp): at will", "DR 5/cold iron or good", "Immune to poison", "Resist fire 10", "Fast healing 2", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.45f, 0.35f, 0.5f, 1f),
            PanelColor = new Color(0.15f, 0.1f, 0.2f, 0.85f),
            NameColor = new Color(0.7f, 0.55f, 0.78f),
            Description = "Quasit (CR 2). Tiny demon familiar with poison and invisibility. MM 3.5e p.46."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Imp — MM p.56
    //  Outsider (Evil, Extraplanar, Lawful), Tiny, CR 2
    //  3d8 HP (13), sting +8 melee (1d4 + poison)
    //  Str 10, Dex 17, Con 10, Int 10, Wis 12, Cha 14
    //  AC 20 (+2 size, +3 Dex, +5 natural), DR 5/good or silver
    //  Poison: DC 13 Fort, 1d4 Dex/2d4 Dex
    //  Alternate form (boar, giant spider, rat, raven), Invisibility at will
    // ════════════════════════════════════════════════════════════
    private static void RegisterImp()
    {
        Register(new NPCDefinition
        {
            Id = "imp",
            Name = "Imp",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 3,
            SizeCategory = SizeCategory.Tiny,
            IsTallCreature = true,
            STR = 10, DEX = 17, CON = 10, WIS = 12, INT = 10, CHA = 14,
            NaturalArmorBonus = 5,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Good | DamageBypassTag.Silver,
            BaseSpeed = 4, // 20 ft, fly 50 ft (perfect)
            BaseHitDieHP = 13,
            BAB = 3,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 5 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Sting", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "Extraplanar", "Fly50", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Poison (Ex): sting, DC 13 Fort, 1d4 Dex/2d4 Dex", "Alternate Form (Su): boar, giant spider, rat, or raven", "Invisibility (Su): at will, self only", "Suggestion (Sp): 1/day, DC 15", "Detect Good/Magic (Sp): at will", "DR 5/good or silver", "Immune to poison", "Resist fire 5", "Fast healing 2", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.5f, 0.3f, 0.3f, 1f),
            PanelColor = new Color(0.2f, 0.08f, 0.08f, 0.85f),
            NameColor = new Color(0.78f, 0.45f, 0.45f),
            Description = "Imp (CR 2). Tiny devil familiar with poison sting and invisibility. MM 3.5e p.56."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Hound Archon — MM p.16
    //  Outsider (Archon, Extraplanar, Good, Lawful), Medium, CR 4
    //  6d8+6 HP (33), bite +8 melee (1d8+3), slam +3 melee (1d4+1)
    //  or greatsword +8/+3 melee (2d6+3) and bite +3 melee (1d8+1)
    //  Str 15, Dex 10, Con 13, Int 10, Wis 13, Cha 12
    //  AC 19 (+9 natural), DR 10/evil, SR 16
    //  Change Shape, Detect Evil, at-will: aid, continual flame, message
    // ════════════════════════════════════════════════════════════
    private static void RegisterHoundArchon()
    {
        Register(new NPCDefinition
        {
            Id = "hound_archon",
            Name = "Hound Archon",
            ChallengeRating = "4",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulGood,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 15, DEX = 10, CON = 13, WIS = 13, INT = 10, CHA = 12,
            NaturalArmorBonus = 9,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.Evil,
            SpellResistance = 16,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 33,
            BAB = 6,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Archon", "Good", "Lawful", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Improved Initiative", "Power Attack" },
            SpecialAbilities = new List<string> { "Change Shape (Su): any canine form", "Detect Evil (Su): at will", "DR 10/evil", "SR 16", "Aura of Menace (Su): 20 ft., Will DC 16 or -2 attacks/AC/saves", "Aid (Sp): at will, Continual Flame (at will), Message (at will)", "Immune to electricity/petrification", "Magic Circle Against Evil (constant)", "+4 saves vs. poison", "Scent", "Darkvision 60 ft., Low-light vision" },
            Immunities = new CreatureImmunities { immuneToElectricity = true },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("greatsword", EquipSlot.MainHand),
                new EquipmentSlotPair("full_plate", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.7f, 0.65f, 0.5f, 1f),
            PanelColor = new Color(0.25f, 0.22f, 0.15f, 0.85f),
            NameColor = new Color(0.92f, 0.88f, 0.7f),
            Description = "Hound Archon (CR 4). Dog-headed celestial warrior with aura of menace. MM 3.5e p.16."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Barghest — MM p.22
    //  Outsider (Evil, Extraplanar, Lawful, Shapechanger), Medium, CR 4
    //  6d8+6 HP (33), bite +9 melee (1d6+3), 2 claws +4 melee (1d4+1)
    //  Str 17, Dex 15, Con 13, Int 14, Wis 14, Cha 14
    //  AC 18 (+2 Dex, +6 natural), DR 5/magic
    //  Feed: kills → +1 growth
    //  Spell-like: blink, levitate, misdirection (at will), charm monster (1/day)
    // ════════════════════════════════════════════════════════════
    private static void RegisterBarghest()
    {
        Register(new NPCDefinition
        {
            Id = "barghest",
            Name = "Barghest",
            ChallengeRating = "4",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 17, DEX = 15, CON = 13, WIS = 14, INT = 14, CHA = 14,
            NaturalArmorBonus = 6,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Magic,
            BaseSpeed = 6,
            BaseHitDieHP = 33,
            BAB = 6,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "Extraplanar", "Shapechanger", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Reflexes", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Feed (Su): devour corpse of HD 0-3 creature, gains growth", "Change Shape (Su): goblin or wolf form", "DR 5/magic", "Blink (Sp): at will", "Levitate (Sp): at will", "Misdirection (Sp): at will", "Charm Monster (Sp): 1/day, DC 16", "Crushing Despair (Sp): 1/day, DC 16", "Dimension Door (Sp): 1/day", "Scent", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.35f, 0.3f, 0.25f, 1f),
            PanelColor = new Color(0.1f, 0.08f, 0.06f, 0.85f),
            NameColor = new Color(0.6f, 0.5f, 0.4f),
            Description = "Barghest (CR 4). Fiendish wolf-goblin shapechanger that grows by feeding. MM 3.5e p.22."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Greater Barghest — MM p.23
    //  Outsider (Evil, Extraplanar, Lawful, Shapechanger), Large, CR 7
    //  9d8+27 HP (67), bite +14 melee (1d8+6), 2 claws +9 melee (1d6+3)
    //  Str 23, Dex 15, Con 17, Int 18, Wis 18, Cha 18
    //  AC 20 (-1 size, +2 Dex, +9 natural), DR 10/magic
    // ════════════════════════════════════════════════════════════
    private static void RegisterGreaterBarghest()
    {
        Register(new NPCDefinition
        {
            Id = "greater_barghest",
            Name = "Greater Barghest",
            ChallengeRating = "7",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 9,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 23, DEX = 15, CON = 17, WIS = 18, INT = 18, CHA = 18,
            NaturalArmorBonus = 9,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.Magic,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 67,
            BAB = 9,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "Extraplanar", "Shapechanger", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Casting", "Combat Reflexes", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Feed (Su): as barghest but HD 0-8", "Change Shape (Su): goblin or dire wolf form", "DR 10/magic", "Blink/Levitate/Misdirection (Sp): at will", "Charm Monster (Sp): at will, DC 18", "Crushing Despair (Sp): at will, DC 18", "Dimension Door (Sp): at will", "Mass Bull's Strength (Sp): 1/day", "Mass Enlarge (Sp): 1/day", "Scent", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.3f, 0.25f, 0.2f, 1f),
            PanelColor = new Color(0.08f, 0.06f, 0.04f, 0.85f),
            NameColor = new Color(0.55f, 0.45f, 0.35f),
            Description = "Greater Barghest (CR 7). Fully grown fiendish wolf with at-will charm monster. MM 3.5e p.23."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Bearded Devil (Barbazu) — MM p.52
    //  Outsider (Baatezu, Evil, Extraplanar, Lawful), Medium, CR 5
    //  6d8+18 HP (45), glaive +9/+4 melee (1d10+3 + infernal wound) or 2 claws +8 melee (1d6+2)
    //  Str 15, Dex 15, Con 17, Int 6, Wis 10, Cha 10
    //  AC 19 (+2 Dex, +7 natural), DR 5/silver or good, SR 17
    //  Infernal Wound: glaive wound bleeds 2 HP/round
    //  Beard: touch +8 melee, DC 16 Fort or diseased (devil chills)
    // ════════════════════════════════════════════════════════════
    private static void RegisterBeardedDevil()
    {
        Register(new NPCDefinition
        {
            Id = "bearded_devil",
            Name = "Bearded Devil",
            ChallengeRating = "5",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 15, DEX = 15, CON = 17, WIS = 10, INT = 6, CHA = 10,
            NaturalArmorBonus = 7,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Silver | DamageBypassTag.Good,
            SpellResistance = 17,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 45,
            BAB = 6,
            Immunities = new CreatureImmunities { immuneToFire = true, immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "Extraplanar", "Baatezu", "Darkvision60", "MM35" },
            Feats = new List<string> { "Improved Initiative", "Power Attack" },
            SpecialAbilities = new List<string> { "Infernal Wound (Su): glaive wound bleeds 2 HP/round (DC 16 Heal or Heal spell to stop)", "Beard (Ex): touch attack, DC 16 Fort or devil chills disease", "Battle Frenzy (Ex): +4 Str for 6 rounds, 1/day", "DR 5/silver or good", "SR 17", "Immune to fire/poison", "Resist acid 10, cold 10", "See in Darkness (Su)", "Telepathy 100 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("glaive", EquipSlot.MainHand)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.5f, 0.3f, 0.25f, 1f),
            PanelColor = new Color(0.2f, 0.08f, 0.05f, 0.85f),
            NameColor = new Color(0.78f, 0.45f, 0.38f),
            Description = "Bearded Devil (CR 5). Glaive-wielding devil with infernal wounds and disease. MM 3.5e p.52."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Chain Devil (Kyton) — MM p.53
    //  Outsider (Baatezu, Evil, Extraplanar, Lawful), Medium, CR 6
    //  8d8+16 HP (52), 4 chains +8 melee (2d4+2)
    //  Str 15, Dex 15, Con 15, Int 6, Wis 10, Cha 12
    //  AC 20 (+2 Dex, +8 natural), DR 5/silver or good, SR 18
    //  Dancing Chains, Unnerving Gaze DC 15
    //  Regeneration 2 (silver or good)
    // ════════════════════════════════════════════════════════════
    private static void RegisterChainDevil()
    {
        Register(new NPCDefinition
        {
            Id = "chain_devil",
            Name = "Chain Devil",
            ChallengeRating = "6",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 8,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 15, DEX = 15, CON = 15, WIS = 10, INT = 6, CHA = 12,
            NaturalArmorBonus = 8,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Silver | DamageBypassTag.Good,
            SpellResistance = 18,
            RegenerationAmount = 2,
            BaseSpeed = 6,
            BaseHitDieHP = 52,
            BAB = 8,
            Immunities = new CreatureImmunities { immuneToFire = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 }
            },
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Unnerving Gaze",
                SaveDC = 15,
                IsWillSave = true,
                RangeFeet = 30,
                Effect = AuraEffectType.Sickened,
                DurationRounds = 10
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Chain", DamageDice = 4, DamageCount = 2, Count = 4, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "Extraplanar", "Baatezu", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Improved Initiative", "Iron Will" },
            SpecialAbilities = new List<string> { "Dancing Chains (Su): animate up to 4 chains within 20 ft.", "Unnerving Gaze (Su): 30 ft., Will DC 15 or sickened 1d3 rounds", "Regeneration 2 (silver or good weapons deal lethal)", "DR 5/silver or good", "SR 18", "Immune to fire", "Resist cold 10", "See in Darkness (Su)", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.4f, 0.35f, 0.35f, 1f),
            PanelColor = new Color(0.12f, 0.1f, 0.1f, 0.85f),
            NameColor = new Color(0.65f, 0.55f, 0.55f),
            Description = "Chain Devil (CR 6). Devil wrapped in animated chains with unnerving gaze. MM 3.5e p.53."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Erinyes — MM p.54
    //  Outsider (Baatezu, Evil, Extraplanar, Lawful), Medium, CR 8
    //  9d8+45 HP (85), longsword +14/+9 melee (1d8+5) or
    //  +1 flaming composite longbow +14/+9 ranged (1d8+6+1d6 fire) or rope +14 ranged (entangle)
    //  Str 21, Dex 17, Con 21, Int 14, Wis 18, Cha 20
    //  AC 23 (+3 Dex, +10 natural), DR 5/good, SR 20
    //  True Seeing, Charm Monster at will
    // ════════════════════════════════════════════════════════════
    private static void RegisterErinyes()
    {
        Register(new NPCDefinition
        {
            Id = "erinyes",
            Name = "Erinyes",
            ChallengeRating = "8",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 9,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 21, DEX = 17, CON = 21, WIS = 18, INT = 14, CHA = 20,
            NaturalArmorBonus = 10,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Good,
            SpellResistance = 20,
            BaseSpeed = 6, // 30 ft, fly 50 ft (good)
            BaseHitDieHP = 85,
            BAB = 9,
            Immunities = new CreatureImmunities { immuneToFire = true, immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "Extraplanar", "Baatezu", "Fly50", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Mobility", "Point Blank Shot", "Rapid Shot", "Shot on the Run" },
            SpecialAbilities = new List<string> { "True Seeing (Su): constant", "Charm Monster (Sp): at will, DC 19", "Minor Image (Sp): at will", "Unholy Blight (Sp): at will, DC 19", "Teleport Greater (Sp): at will, self + 50 lb.", "DR 5/good", "SR 20", "Immune to fire/poison", "Resist acid 10, cold 10", "See in Darkness (Su)", "Telepathy 100 ft.", "Fly 50 ft. (good)" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("longsword", EquipSlot.MainHand),
                new EquipmentSlotPair("composite_longbow", EquipSlot.Ranged)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Ranged,
            SpriteColor = new Color(0.55f, 0.3f, 0.35f, 1f),
            PanelColor = new Color(0.22f, 0.08f, 0.1f, 0.85f),
            NameColor = new Color(0.85f, 0.48f, 0.55f),
            Description = "Erinyes (CR 8). Fallen angel devil with flaming bow and charm monster. MM 3.5e p.54."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Babau — MM p.40
    //  Outsider (Chaotic, Evil, Extraplanar, Tanar'ri), Medium, CR 6
    //  7d8+35 HP (66), claw +12 melee (1d6+5), 2 claws, bite +7 melee (1d6+2)
    //  or longspear +12/+7 melee (1d8+7)
    //  Str 21, Dex 12, Con 20, Int 14, Wis 13, Cha 16
    //  AC 19 (+1 Dex, +8 natural), DR 10/cold iron or good, SR 14
    //  Protective Slime: 1d8 acid on melee attackers
    //  Sneak Attack +2d6
    // ════════════════════════════════════════════════════════════
    private static void RegisterBabau()
    {
        Register(new NPCDefinition
        {
            Id = "babau",
            Name = "Babau",
            ChallengeRating = "6",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 7,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 21, DEX = 12, CON = 20, WIS = 13, INT = 14, CHA = 16,
            NaturalArmorBonus = 8,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.ColdIron | DamageBypassTag.Good,
            SpellResistance = 14,
            BaseSpeed = 6,
            BaseHitDieHP = 66,
            BAB = 7,
            Immunities = new CreatureImmunities { immuneToElectricity = true, immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Evil", "Extraplanar", "Tanarri", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Cleave", "Improved Initiative", "Multiattack" },
            SpecialAbilities = new List<string> { "Protective Slime (Su): 1d8 acid to melee attackers/weapons", "Sneak Attack +2d6", "DR 10/cold iron or good", "SR 14", "Immune to electricity/poison", "Resist acid 10, cold 10, fire 10", "Darkness (Sp): at will", "Dispel Magic (Sp): at will", "See Invisibility (Sp): at will", "Greater Teleport (Sp): at will, self + 50 lb.", "Telepathy 100 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.35f, 0.25f, 0.3f, 1f),
            PanelColor = new Color(0.12f, 0.06f, 0.08f, 0.85f),
            NameColor = new Color(0.6f, 0.4f, 0.5f),
            Description = "Babau (CR 6). Skeletal demon assassin with protective acid slime and sneak attack. MM 3.5e p.40."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Succubus — MM p.47
    //  Outsider (Chaotic, Evil, Extraplanar, Tanar'ri), Medium, CR 7
    //  6d8+6 HP (33), 2 claws +7 melee (1d3+1)
    //  Str 13, Dex 13, Con 13, Int 16, Wis 14, Cha 26
    //  AC 20 (+1 Dex, +9 natural), DR 10/cold iron or good
    //  Energy Drain: kiss drains 1 level (Will DC 21 to resist), SR 18
    //  Charm Monster, Ethereal Jaunt, Suggestion at will
    //  Alternate form, tongues
    // ════════════════════════════════════════════════════════════
    private static void RegisterSuccubus()
    {
        Register(new NPCDefinition
        {
            Id = "succubus",
            Name = "Succubus",
            ChallengeRating = "7",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 13, CON = 13, WIS = 14, INT = 16, CHA = 26,
            NaturalArmorBonus = 9,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.ColdIron | DamageBypassTag.Good,
            SpellResistance = 18,
            BaseSpeed = 6, // 30 ft, fly 50 ft (average)
            BaseHitDieHP = 33,
            BAB = 6,
            Immunities = new CreatureImmunities { immuneToElectricity = true, immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Evil", "Extraplanar", "Tanarri", "Fly50", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Mobility", "Persuasive" },
            SpecialAbilities = new List<string> { "Energy Drain (Su): kiss, 1 negative level, Will DC 21 to resist", "Charm Monster (Sp): at will, DC 22", "Detect Good/Evil/Thoughts (Sp): at will", "Ethereal Jaunt (Sp): at will", "Suggestion (Sp): at will, DC 21", "Greater Teleport (Sp): at will, self + 50 lb.", "Polymorph (Sp): at will, humanoid form", "Tongues (Su): constant", "DR 10/cold iron or good", "SR 18", "Immune to electricity/poison", "Resist acid 10, cold 10, fire 10", "Darkvision 60 ft.", "Fly 50 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.6f, 0.3f, 0.4f, 1f),
            PanelColor = new Color(0.25f, 0.08f, 0.12f, 0.85f),
            NameColor = new Color(0.9f, 0.48f, 0.6f),
            Description = "Succubus (CR 7). Seductive demon with energy draining kiss and charm. MM 3.5e p.47."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Hellcat (Bezekira) — MM p.54
    //  Outsider (Evil, Extraplanar, Lawful), Large, CR 7
    //  10d8+20 HP (65), bite +13 melee (1d8+5), 2 claws +8 melee (1d6+2)
    //  Str 21, Dex 19, Con 15, Int 10, Wis 14, Cha 10
    //  AC 21 (-1 size, +4 Dex, +8 natural), DR 5/good
    //  Invisible in light, pounce, improved grab, rake 1d6+2
    // ════════════════════════════════════════════════════════════
    private static void RegisterHellcat()
    {
        Register(new NPCDefinition
        {
            Id = "hellcat",
            Name = "Hellcat",
            ChallengeRating = "7",
            Level = 10,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 10,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 21, DEX = 19, CON = 15, WIS = 14, INT = 10, CHA = 10,
            NaturalArmorBonus = 8,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Good,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 65,
            BAB = 10,
            HasPounce = true,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Claw",
            HasRake = true,
            RakeAttack = new NaturalAttackDefinition { Name = "Rake", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false },
            HasScent = true,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Improved Initiative", "Lightning Reflexes", "Track" },
            SpecialAbilities = new List<string> { "Invisible in Light (Su): invisible in any light brighter than darkness", "Pounce", "Improved Grab", "Rake: 2 claws 1d6+2 each (grapple)", "DR 5/good", "Resist fire 10", "Scent", "See in Darkness (Su)", "Telepathy 100 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.6f, 0.4f, 0.25f, 1f),
            PanelColor = new Color(0.22f, 0.12f, 0.06f, 0.85f),
            NameColor = new Color(0.88f, 0.62f, 0.38f),
            Description = "Hellcat (CR 7). Fiendish feline invisible in light, with pounce and rake. MM 3.5e p.54."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Bralani — MM p.93
    //  Outsider (Chaotic, Eladrin, Extraplanar, Good), Medium, CR 6
    //  6d8+18 HP (45), +1 holy scimitar +10/+5 melee (1d6+4+2d6 vs evil) or
    //  +1 holy composite longbow +10/+5 ranged (1d8+4+2d6 vs evil)
    //  Str 17, Dex 18, Con 17, Int 13, Wis 14, Cha 14
    //  AC 20 (+4 Dex, +6 natural), DR 10/cold iron or evil, SR 17
    //  Whirlwind/human alternate form, tongues
    // ════════════════════════════════════════════════════════════
    private static void RegisterBralani()
    {
        Register(new NPCDefinition
        {
            Id = "bralani",
            Name = "Bralani",
            ChallengeRating = "6",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticGood,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 17, DEX = 18, CON = 17, WIS = 14, INT = 13, CHA = 14,
            NaturalArmorBonus = 6,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.ColdIron | DamageBypassTag.Evil,
            SpellResistance = 17,
            BaseSpeed = 8, // 40 ft, fly 100 ft (perfect in whirlwind)
            BaseHitDieHP = 45,
            BAB = 6,
            Immunities = new CreatureImmunities { immuneToElectricity = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Good", "Extraplanar", "Eladrin", "Fly100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Whirlwind Form (Su): 10-ft. radius, 3d6+3 damage", "Alternate Form: whirlwind or humanoid", "Tongues (Su): constant", "DR 10/cold iron or evil", "SR 17", "Immune to electricity/petrification", "Resist cold 10, fire 10", "Blur (Sp): at will", "Charm Person (Sp): at will, DC 13", "Gust of Wind (Sp): at will, DC 14", "Mirror Image (Sp): at will", "Wind Wall (Sp): at will", "Lightning Bolt (Sp): 2/day, DC 15", "Cure Serious Wounds (Sp): 2/day", "Darkvision 60 ft., Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("scimitar", EquipSlot.MainHand),
                new EquipmentSlotPair("composite_longbow", EquipSlot.Ranged)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Ranged,
            SpriteColor = new Color(0.5f, 0.65f, 0.8f, 1f),
            PanelColor = new Color(0.15f, 0.22f, 0.3f, 0.85f),
            NameColor = new Color(0.7f, 0.85f, 0.95f),
            Description = "Bralani (CR 6). Wind eladrin that shifts between humanoid and whirlwind forms. MM 3.5e p.93."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Djinni — MM p.114
    //  Outsider (Air, Extraplanar), Large, CR 5
    //  7d8+14 HP (45), slam +10 melee (1d8+6) × 2
    //  Str 18, Dex 17, Con 14, Int 14, Wis 15, Cha 15
    //  AC 16 (-1 size, +3 Dex, +4 natural)
    //  Air mastery, whirlwind, spell-like abilities
    // ════════════════════════════════════════════════════════════
    private static void RegisterDjinni()
    {
        Register(new NPCDefinition
        {
            Id = "djinni",
            Name = "Djinni",
            ChallengeRating = "5",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticGood,
            HitDice = 7,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 18, DEX = 17, CON = 14, WIS = 15, INT = 14, CHA = 15,
            NaturalArmorBonus = 4,
            BaseSpeed = 4, // 20 ft, fly 60 ft (perfect)
            BaseHitDieHP = 45,
            BAB = 7,
            Immunities = new CreatureImmunities { immuneToAcid = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 8, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Air", "Extraplanar", "Fly60", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Casting", "Combat Reflexes", "Dodge", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Air Mastery (Ex): +1 attack/damage vs airborne, -4 vs grounded", "Whirlwind (Su): 10-70 ft., 2d6+4, Ref DC 18", "Invisibility (Sp): at will, self", "Create Food/Water (Sp): 1/day", "Major Creation (Sp): 1/day", "Persistent Image (Sp): 1/day, DC 17", "Wind Walk (Sp): 1/day", "Gaseous Form (Sp): 1/day", "Plane Shift (Sp): at will", "Immune to acid", "Telepathy 100 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.55f, 0.7f, 0.85f, 1f),
            PanelColor = new Color(0.15f, 0.22f, 0.32f, 0.85f),
            NameColor = new Color(0.75f, 0.88f, 0.98f),
            Description = "Djinni (CR 5). Air genie with whirlwind and spell-like abilities. MM 3.5e p.114."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Efreeti — MM p.115
    //  Outsider (Extraplanar, Fire), Large, CR 8
    //  10d8+20 HP (65), slam +15/+10 melee (1d8+6 + 1d6 fire) × 2
    //  Str 23, Dex 17, Con 14, Int 12, Wis 15, Cha 15
    //  AC 18 (-1 size, +3 Dex, +6 natural)
    //  Fire subtype, spell-like abilities, grant wishes
    // ════════════════════════════════════════════════════════════
    private static void RegisterEfreeti()
    {
        Register(new NPCDefinition
        {
            Id = "efreeti",
            Name = "Efreeti",
            ChallengeRating = "8",
            Level = 10,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 10,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 23, DEX = 17, CON = 14, WIS = 15, INT = 12, CHA = 15,
            NaturalArmorBonus = 6,
            BaseSpeed = 4, // 20 ft, fly 40 ft (perfect)
            BaseHitDieHP = 65,
            BAB = 10,
            Immunities = new CreatureImmunities { immuneToFire = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 8, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true, BonusElementalDamageDice = 6, BonusElementalDamageCount = 1, BonusElementalDamageType = DamageType.Fire }
            },
            CreatureTags = new List<string> { "Outsider", "Fire", "Extraplanar", "Fly40", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Casting", "Combat Reflexes", "Dodge", "Improved Initiative", "Quicken Spell-Like Ability" },
            SpecialAbilities = new List<string> { "Heat (Ex): +1d6 fire on melee attacks", "Change Size (Sp): 2/day, reduce/enlarge", "Gaseous Form (Sp): at will", "Invisibility (Sp): at will", "Permanent Image (Sp): at will, DC 17", "Polymorph (Sp): 3/day, self, up to 1 hr", "Scorching Ray (Sp): 3/day", "Wall of Fire (Sp): 1/day, DC 16", "Grant up to 3 Wishes (Sp): to non-genies, 1/day", "Plane Shift (Sp): at will", "Immune to fire", "Vulnerable to cold", "Telepathy 100 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.8f, 0.45f, 0.2f, 1f),
            PanelColor = new Color(0.3f, 0.15f, 0.05f, 0.85f),
            NameColor = new Color(0.95f, 0.65f, 0.3f),
            Description = "Efreeti (CR 8). Fire genie with heat attacks and wish-granting. MM 3.5e p.115."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Janni — MM p.116
    //  Outsider (Native), Medium, CR 4
    //  6d8+6 HP (33), scimitar +9/+4 melee (1d6+4) or longbow +7/+2 ranged (1d8)
    //  Str 16, Dex 15, Con 12, Int 14, Wis 15, Cha 13
    //  AC 18 (+2 Dex, +1 natural, +5 chainmail), Elemental Endurance, Change Size
    // ════════════════════════════════════════════════════════════
    private static void RegisterJanni()
    {
        Register(new NPCDefinition
        {
            Id = "janni",
            Name = "Janni",
            ChallengeRating = "4",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 16, DEX = 15, CON = 12, WIS = 15, INT = 14, CHA = 13,
            NaturalArmorBonus = 1,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            BaseSpeed = 4, // 20 ft (armor), fly 20 ft (perfect)
            BaseHitDieHP = 33,
            BAB = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Outsider", "Native", "Fly20", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Improved Initiative", "Mobility" },
            SpecialAbilities = new List<string> { "Elemental Endurance (Ex): survive on Elemental Planes", "Change Size (Sp): 2/day, enlarge/reduce", "Invisibility (Sp): 3/day", "Speak with Animals (Sp): 3/day", "Create Food and Water (Sp): 1/day", "Ethereal Jaunt (Sp): 1/day", "Resist fire 10", "Telepathy 100 ft.", "Plane Shift (Sp): at will", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("scimitar", EquipSlot.MainHand),
                new EquipmentSlotPair("longbow", EquipSlot.Ranged),
                new EquipmentSlotPair("chainmail", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.6f, 0.55f, 0.5f, 1f),
            PanelColor = new Color(0.22f, 0.2f, 0.18f, 0.85f),
            NameColor = new Color(0.85f, 0.8f, 0.72f),
            Description = "Janni (CR 4). Weakest genie, native to Material Plane. MM 3.5e p.116."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Xill — MM p.259
    //  Outsider (Extraplanar), Medium, CR 6
    //  5d8+10 HP (32), 4 short swords +7 melee (1d6+2) or 2 claws +7 melee (1d4+2), bite +2 melee (1d3+1)
    //  Str 14, Dex 16, Con 15, Int 12, Wis 12, Cha 11
    //  AC 19 (+3 Dex, +6 natural), Improved Grab, Implant, Planewalk
    //  SR 21
    // ════════════════════════════════════════════════════════════
    private static void RegisterXill()
    {
        Register(new NPCDefinition
        {
            Id = "xill",
            Name = "Xill",
            ChallengeRating = "6",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 14, DEX = 16, CON = 15, WIS = 12, INT = 12, CHA = 11,
            NaturalArmorBonus = 6,
            SpellResistance = 21,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 32,
            BAB = 5,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Claw",
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Improved Initiative", "Multiattack" },
            SpecialAbilities = new List<string> { "Improved Grab", "Implant (Ex): implant egg in paralyzed/helpless foe", "Paralytic Bite (Ex): DC 14 Fort or paralyzed 1d4 hours", "Planewalk (Su): shift Ethereal/Material as standard action", "Multiweapon Fighting: four arms", "SR 21", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("short_sword", EquipSlot.MainHand),
                new EquipmentSlotPair("short_sword", EquipSlot.OffHand)
            },
            BackpackItemIds = new List<string> { "short_sword", "short_sword" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.5f, 0.4f, 0.35f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.1f, 0.85f),
            NameColor = new Color(0.78f, 0.62f, 0.55f),
            Description = "Xill (CR 6). Four-armed extraplanar raider that implants eggs. MM 3.5e p.259."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Formian Worker — MM p.108
    //  Outsider (Lawful), Small, CR 1/2
    //  1d8+1 HP (5), bite +3 melee (1d4+1)
    //  Str 13, Dex 14, Con 13, Int 6, Wis 10, Cha 9
    //  AC 17 (+1 size, +2 Dex, +4 natural), Hive Mind
    // ════════════════════════════════════════════════════════════
    private static void RegisterFormianWorker()
    {
        Register(new NPCDefinition
        {
            Id = "formian_worker",
            Name = "Formian Worker",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulNeutral,
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 13, DEX = 14, CON = 13, WIS = 10, INT = 6, CHA = 9,
            NaturalArmorBonus = 4,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 5,
            BAB = 1,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Sonic, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Lawful", "HiveMind", "Darkvision60", "MM35" },
            Feats = new List<string> { "Skill Focus (Craft)" },
            SpecialAbilities = new List<string> { "Hive Mind (Ex): all formians within 50 mi. in contact", "Cure Serious Wounds (Sp): 8/day (hive structures only)", "Make Whole (Sp): 3/day", "Immune to poison/petrification", "Resist sonic 10, cold 10, fire 10", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.65f, 0.55f, 0.35f, 1f),
            PanelColor = new Color(0.25f, 0.2f, 0.1f, 0.85f),
            NameColor = new Color(0.88f, 0.78f, 0.55f),
            Description = "Formian Worker (CR 1/2). Ant-like outsider laborer with hive mind. MM 3.5e p.108."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Formian Taskmaster — MM p.109
    //  Outsider (Lawful), Medium, CR 7
    //  6d8+12 HP (39), sting +9 melee (2d4+3 + poison)
    //  Str 17, Dex 16, Con 14, Int 16, Wis 16, Cha 19
    //  AC 19 (+3 Dex, +6 natural), Dominate Monster, Hive Mind
    //  Poison: DC 15 Fort, 1d6 Str/1d6 Str
    // ════════════════════════════════════════════════════════════
    private static void RegisterFormianTaskmaster()
    {
        Register(new NPCDefinition
        {
            Id = "formian_taskmaster",
            Name = "Formian Taskmaster",
            ChallengeRating = "7",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulNeutral,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 17, DEX = 16, CON = 14, WIS = 16, INT = 16, CHA = 19,
            NaturalArmorBonus = 6,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 39,
            BAB = 6,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Sonic, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Sting", DamageDice = 4, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Lawful", "HiveMind", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Dominate Monster (Su): DC 17 Will, range 30 ft., up to 6 targets", "Poison (Ex): sting, DC 15 Fort, 1d6 Str/1d6 Str", "Hive Mind (Ex): all formians within 50 mi. in contact", "Immune to poison/petrification", "Resist sonic 10, cold 10, fire 10", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.6f, 0.5f, 0.3f, 1f),
            PanelColor = new Color(0.22f, 0.18f, 0.08f, 0.85f),
            NameColor = new Color(0.85f, 0.75f, 0.48f),
            Description = "Formian Taskmaster (CR 7). Ant-like overseer with dominate monster. MM 3.5e p.109."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Shadow Mastiff — MM p.222
    //  Outsider (Extraplanar), Medium, CR 5
    //  4d8+12 HP (30), bite +7 melee (1d6+4)
    //  Str 17, Dex 15, Con 17, Int 4, Wis 12, Cha 13
    //  AC 14 (+2 Dex, +2 natural), Trip, Shadow Blend
    //  Bay: 300 ft., Will DC 13 or panicked 2d4 rounds
    // ════════════════════════════════════════════════════════════
    private static void RegisterShadowMastiff()
    {
        Register(new NPCDefinition
        {
            Id = "shadow_mastiff",
            Name = "Shadow Mastiff",
            ChallengeRating = "5",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 17, WIS = 12, INT = 4, CHA = 13,
            NaturalArmorBonus = 2,
            BaseSpeed = 10, // 50 ft
            BaseHitDieHP = 30,
            BAB = 4,
            HasTripAttack = true,
            TripAttackCheckBonus = 3,
            HasScent = true,
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Bay",
                SaveDC = 13,
                IsWillSave = true,
                RangeFeet = 300,
                Effect = AuraEffectType.Frightened,
                DurationRounds = 6
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Extraplanar", "ShadowBlend", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Bay (Su): howl, 300 ft., Will DC 13 or panicked 2d4 rounds", "Trip (Ex): free trip on bite hit", "Shadow Blend (Su): total concealment in dim light except when near light", "Scent", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.2f, 0.2f, 0.25f, 1f),
            PanelColor = new Color(0.05f, 0.05f, 0.08f, 0.85f),
            NameColor = new Color(0.38f, 0.38f, 0.48f),
            Description = "Shadow Mastiff (CR 5). Extraplanar shadow hound with frightening bay and trip. MM 3.5e p.222."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Vargouille — MM p.254
    //  Outsider (Evil, Extraplanar), Small, CR 2
    //  1d8+1 HP (5), bite +3 melee (1d4 + poison)
    //  Str 10, Dex 13, Con 12, Int 5, Wis 12, Cha 8
    //  AC 12 (+1 size, +1 Dex)
    //  Shriek: 60 ft., Fort DC 12 or paralyzed 2d4 rounds
    //  Kiss: transforms paralyzed victim into vargouille
    //  Poison: DC 12 Fort, 1d2 Str/1d2 Str
    // ════════════════════════════════════════════════════════════
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.35f, 0.3f, 1f),
            PanelColor = new Color(0.15f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(0.72f, 0.55f, 0.48f),
            Description = "Vargouille (CR 2). Flying fiendish head with paralysing shriek and transformative kiss. MM 3.5e p.254."
        });
    }
}
