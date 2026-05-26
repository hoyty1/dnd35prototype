using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: L
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_L()
    {
        RegisterLanternArchon();
        RegisterLemure();
        RegisterLion();
        RegisterDireLion();
    
        RegisterSummonLargeShark();
        RegisterSummonLargeViper();
        RegisterLizardfolk();
        RegisterLocustSwarm();

    }

    /// <summary>
    /// Lantern Archon (CR 2) — Small outsider (archon, extraplanar, good, lawful).
    /// MM 3.5e p.16. 1 HD, fly 60 ft (perfect), light rays (ranged touch 1d6),
    /// aura of menace, magic circle against evil, teleport, aid.
    /// </summary>
    private static void RegisterLanternArchon()
    {
        Register(new NPCDefinition
        {
            Id = "lantern_archon",
            Name = "Lantern Archon",
            ChallengeRating = "2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 1, DEX = 11, CON = 12, WIS = 11, INT = 6, CHA = 10,
            BAB = 1,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                // Light rays — 2 ranged touch attacks, 1d6 damage each, range 30 ft.
                new NaturalAttackDefinition { Name = "Light Ray", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.None, Range = 6, IsPrimary = true }
            },
            BaseSpeed = 12, // Fly 60 ft. (perfect) — no land speed
            BaseHitDieHP = 6, // 1d8+1
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.Evil,
            SpellResistance = 12,
            DamageImmunities = new List<DamageType> { DamageType.Electricity },
            Immunities = new CreatureImmunities
            {
                immuneToElectricity = true
            },
            CreatureTags = new List<string> { "Outsider", "Archon", "Extraplanar", "Good", "Lawful", "MM35", "Fly", "SummonBase" },
            SpecialAbilities = new List<string>
            {
                "Light rays (2 ranged touch +1, 1d6 each, 30 ft.)",
                "Aura of Menace (Will DC 13 or -2 attacks/AC/saves for 24 hrs)",
                "Magic Circle Against Evil (continuous)",
                "Teleport (at will, self + 50 lbs)",
                "Spell-like: Aid (at will)",
                "DR 10/evil",
                "SR 12",
                "Immunity to electricity and petrification",
                "Fly 60 ft. (perfect)",
                "Darkvision 60 ft.",
                "Low-light vision",
                "+4 racial vs poison",
                "Tongues (continuous)",
                "Alignment: Lawful Good"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Ranged,
            SpriteColor = new Color(1f, 0.98f, 0.7f, 1f),
            PanelColor = new Color(0.3f, 0.28f, 0.12f, 0.85f),
            NameColor = new Color(1f, 1f, 0.85f),
            Description = "Monster Manual lantern archon (CR 2). Glowing orb celestial with light ray attacks, DR 10/evil, SR 12, aura of menace. MM 3.5e p.16."
        });
    }

    private static void RegisterLemure()
    {
        Register(new NPCDefinition
        {
            Id = "lemure",
            Name = "Lemure",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 11, DEX = 10, CON = 12, WIS = 11, INT = CharacterStats.NO_SCORE, CHA = 5,
            BAB = 2,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4,
            BaseHitDieHP = 11,
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "MM35", "Devil" },
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Good | DamageBypassTag.Silver,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 }
            },
            DamageImmunities = new List<DamageType> { DamageType.Fire },
            Immunities = ImmunityPresets.Combine(ImmunityPresets.DevilImmunities(), ImmunityPresets.MindlessImmunities()),
            IsMindless = true,
            RegenerationAmount = 2,
            RegenerationSuppressedBy = DamageBypassTag.Good | DamageBypassTag.Silver,
            SpecialAbilities = new List<string>
            {
                "DR 5/good or silver",
                "Immunity to fire",
                "Poison immunity",
                "Mind-affecting immunity",
                "Resist acid 10",
                "Resist cold 10",
                "Regeneration 2 (suppressed by good or silver)"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.58f, 0.24f, 0.24f, 1f),
            PanelColor = new Color(0.22f, 0.08f, 0.08f, 0.85f),
            NameColor = new Color(0.95f, 0.66f, 0.66f),
            Description = "Monster Manual lemure devil. Sluggish fiend with infernal resistances and relentless regeneration."
        });
    }


    /// <summary>
    /// Lion (CR 3) — Large animal with pounce and rake.
    /// MM 3.5e p.274. 5 HD, pounce (full attack on charge), rake 1d4+2 (x2).
    /// </summary>
    private static void RegisterLion()
    {
        Register(new NPCDefinition
        {
            Id = "lion",
            Name = "Lion",
            ChallengeRating = "3",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 5,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 21, DEX = 17, CON = 15, WIS = 12, INT = 2, CHA = 6,
            BAB = 3,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 32, // 5d8+10
            HasPounce = true,
            HasRake = true,
            RakeAttack = new NaturalAttackDefinition { Name = "Rake", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = true },
            HasScent = true,
            CreatureTags = new List<string> { "Animal", "MM35" },
            Feats = new List<string> { "Alertness", "Run" },
            SpecialAbilities = new List<string>
            {
                "Pounce (full attack on charge)",
                "Improved Grab (bite)",
                "Rake (2 × 1d4+2)",
                "Low-light vision",
                "Scent",
                "Skills: Balance +7, Hide +3 (+12 in tall grass), Listen +5, Move Silently +11, Spot +5",
                "Alignment: True Neutral"
            },
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.85f, 0.72f, 0.4f, 1f),
            PanelColor = new Color(0.28f, 0.22f, 0.1f, 0.85f),
            NameColor = new Color(1f, 0.92f, 0.7f),
            Description = "Monster Manual lion (CR 3). 2 claws +7 (1d4+5), bite +2 (1d8+2). Pounce, improved grab, rake 2×1d4+2. MM 3.5e p.274."
        });
    }

    /// <summary>
    /// Dire Lion (CR 5) — Large animal with pounce, improved grab, and rake.
    /// MM 3.5e p.63. 8 HD, pounce (full attack on charge), rake 1d6+3 (x2).
    /// Stronger version of lion used by Lion's Shield, Greater.
    /// </summary>
    private static void RegisterDireLion()
    {
        Register(new NPCDefinition
        {
            Id = "dire_lion",
            Name = "Dire Lion",
            ChallengeRating = "5",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 8,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 25, DEX = 15, CON = 17, WIS = 12, INT = 2, CHA = 10,
            BAB = 6,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 60, // 8d8+24
            HasPounce = true,
            HasRake = true,
            RakeAttack = new NaturalAttackDefinition { Name = "Rake", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = true },
            HasScent = true,
            CreatureTags = new List<string> { "Animal", "Dire", "MM35" },
            Feats = new List<string> { "Alertness", "Run", "Weapon Focus (Claw)" },
            SpecialAbilities = new List<string>
            {
                "Pounce (full attack on charge)",
                "Improved Grab (bite)",
                "Rake (2 × 1d6+3)",
                "Low-light vision",
                "Scent",
                "Skills: Hide +2 (+8 in tall grass), Listen +7, Move Silently +7, Spot +7",
                "Alignment: True Neutral"
            },
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.78f, 0.62f, 0.30f, 1f),
            PanelColor = new Color(0.30f, 0.20f, 0.08f, 0.85f),
            NameColor = new Color(1f, 0.88f, 0.6f),
            Description = "Monster Manual dire lion (CR 5). 2 claws +12 (1d6+7), bite +7 (1d8+3). Pounce, improved grab, rake 2×1d6+3. MM 3.5e p.63."
        });
    }

    private static void RegisterSummonLargeShark()
    {
        Register(new NPCDefinition
        {
            Id = "large_shark",
            Name = "Large Shark",
            ChallengeRating = "2",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 13, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 26,
            CreatureTags = new List<string> { "Animal", "Aquatic", "SummonBase" },
            HasScent = true,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.62f, 0.7f, 0.78f, 1f),
            PanelColor = new Color(0.14f, 0.18f, 0.22f, 0.85f),
            NameColor = new Color(0.88f, 0.94f, 0.99f),
            Description = "Summon Monster baseline large shark."
        });
    }

    private static void RegisterSummonLargeViper()
    {
        Register(new NPCDefinition
        {
            Id = "large_viper",
            Name = "Large Viper",
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
                // Bite +4 melee (1d4 plus poison) — uses Weapon Finesse (Dex to attack)
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true, PoisonOnHitId = "large_viper_poison" }
            },
            BaseSpeed = 4, // 20 ft., climb 20 ft., swim 20 ft.
            BaseHitDieHP = 13,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Poison (Fort DC 11, 1d6 Con/1d6 Con)", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.55f, 0.3f, 1f),
            PanelColor = new Color(0.18f, 0.22f, 0.1f, 0.85f),
            NameColor = new Color(0.88f, 0.94f, 0.72f),
            Description = "Large viper snake. Bite +4 (1d4 + poison Fort DC 11, 1d6 Con/1d6 Con). Weapon Finesse. MM 3.5e p.280."
        });
    
    private static void RegisterLizardfolk()
    {
        Register(new NPCDefinition
        {
            Id = "lizardfolk",
            Name = "Lizardfolk",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            BABOverride = BABProgression.Medium,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 10, CON = 13, WIS = 10, INT = 9, CHA = 10,
            NaturalArmorBonus = 5,
            BaseSpeed = 6,
            BaseHitDieHP = 11,
            BAB = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Humanoid", "Reptilian", "Aquatic", "MM35" },
            Feats = new List<string> { "Multiattack" },
            SpecialAbilities = new List<string> { "Hold Breath (4× Con rounds)", "Swim 30 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("club", EquipSlot.MainHand),
                new EquipmentSlotPair("heavy_wooden_shield", EquipSlot.OffHand)
            },
            BackpackItemIds = new List<string> { "javelin" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.35f, 0.55f, 0.3f, 1f),
            PanelColor = new Color(0.12f, 0.2f, 0.08f, 0.85f),
            NameColor = new Color(0.6f, 0.85f, 0.5f),
            Description = "Lizardfolk (CR 1). Reptilian humanoid with claws and natural armor. MM 3.5e p.169."
        });
    }

    private static void RegisterLocustSwarm()
    {
        Register(new NPCDefinition
        {
            Id = "locust_swarm",
            Name = "Locust Swarm",
            ChallengeRating = "3",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 6,
            SizeCategory = SizeCategory.Diminutive,
            IsTallCreature = false,
            STR = 1, DEX = 19, CON = 8, WIS = 10, INT = 0, CHA = 2,
            NaturalArmorBonus = 0,
            IsMindless = true,
            IsSwarm = true,
            BaseSpeed = 2, // 10 ft, fly 30 ft (poor)
            BaseHitDieHP = 21,
            BAB = 4,
            Immunities = new CreatureImmunities { immuneToMindAffecting = true, immuneToWeaponDamage = true },
            SwarmTraits = new SwarmTraits { SwarmDamage = 6, SwarmDamageCount = 2, SwarmDamageType = DamageType.Piercing, DistractionDC = 12 },
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Vermin", "Swarm", "Fly30", "Darkvision60", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Swarm Attack: 2d6 to creatures in space", "Distraction (Ex): DC 12 Fort or nauseated 1 round", "Immune to weapon damage", "Vermin traits (mindless)", "Fly 30 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.IndiscriminateSwarm,
            SpriteColor = new Color(0.45f, 0.5f, 0.3f, 1f),
            PanelColor = new Color(0.15f, 0.18f, 0.06f, 0.85f),
            NameColor = new Color(0.7f, 0.78f, 0.48f),
            Description = "Locust Swarm (CR 3). Diminutive vermin swarm with distraction. MM 3.5e p.239."
        });
    }
}

}
