using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Factory methods for creating D&D 3.5e magic ring ItemData instances.
/// All rings from DMG pp. 229–233, organized by Tier 1 (passive) first.
/// Rings use EquipSlot.EitherRing and ItemType.Ring.
/// </summary>
public static class RingFactory
{
    // ════════════════════════════════════════════════════════════
    //  Common Ring Icon
    // ════════════════════════════════════════════════════════════
    private const string RingIcon = "\uD83D\uDC8D"; // 💍

    // ════════════════════════════════════════════════════════════
    //  Protection Rings (+1 to +5 deflection bonus to AC)
    //  DMG p.232: CL = bonus × 3
    //  Price: bonus² × 2000 gp
    // ════════════════════════════════════════════════════════════

    public static ItemData CreateProtectionRing(int bonus)
    {
        int price = bonus * bonus * 2000;
        int casterLevel = bonus * 3;
        string id = $"ring_of_protection_{bonus}";

        return new ItemData
        {
            Id = id,
            Name = $"Ring of Protection +{bonus}",
            Description = $"This ring offers continual magical protection in the form of a deflection bonus of +{bonus} to AC.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = id,
            RingDeflectionBonus = bonus,
            RingCasterLevel = casterLevel,
            BasePriceGp = price,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.7f, 0.85f, 1.0f) // Pale blue
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Resistance Rings (+1 to +5 resistance bonus to all saves)
    //  DMG p.232: CL = bonus × 3
    //  Price: bonus² × 2000 gp (Minor +1 = 2000, Major +2 = 8000, Greater +3 = 18000, +4 = 32000, +5 = 50000)
    //  Note: DMG names these as Minor (+1), Major (+2), Greater (+3).
    //  +4 and +5 exist as unnamed higher variants.
    // ════════════════════════════════════════════════════════════

    public static ItemData CreateResistanceRing(int bonus)
    {
        int price = bonus * bonus * 2000;
        int casterLevel = bonus * 3;
        string id = $"ring_of_resistance_{bonus}";
        string tier = bonus <= 1 ? "Minor" : bonus == 2 ? "Major" : bonus == 3 ? "Greater" : $"+{bonus}";

        return new ItemData
        {
            Id = id,
            Name = $"Ring of Resistance +{bonus}",
            Description = $"This ring grants its wearer a +{bonus} resistance bonus on all saving throws (Fortitude, Reflex, and Will).",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = id,
            RingResistanceSaveBonus = bonus,
            RingCasterLevel = casterLevel,
            BasePriceGp = price,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.85f, 0.75f, 1.0f) // Lavender
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Energy Resistance Rings
    //  DMG p.230: Minor (resist 10, 12000 gp, CL 3),
    //             Major (resist 20, 28000 gp, CL 7),
    //             Greater (resist 30, 44000 gp, CL 11)
    //  Energy types: Acid, Cold, Electricity, Fire, Sonic
    // ════════════════════════════════════════════════════════════

    public static ItemData CreateEnergyResistanceRing(string energyType, int amount)
    {
        string tier = amount == 10 ? "Minor" : amount == 20 ? "Major" : "Greater";
        int price = amount == 10 ? 12000 : amount == 20 ? 28000 : 44000;
        int casterLevel = amount == 10 ? 3 : amount == 20 ? 7 : 11;
        string tierSuffix = amount == 10 ? "minor" : amount == 20 ? "major" : "greater";
        string id = $"ring_of_energy_resistance_{energyType.ToLower()}_{tierSuffix}";

        return new ItemData
        {
            Id = id,
            Name = $"Ring of Energy Resistance ({energyType}, {tier})",
            Description = $"This ring continually protects the wearer from energy damage. Each time the wearer would normally take {energyType.ToLower()} damage, the ring absorbs the first {amount} points of damage per attack.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = id,
            RingEnergyType = energyType,
            RingEnergyResistanceAmount = amount,
            RingCasterLevel = casterLevel,
            BasePriceGp = price,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = GetEnergyColor(energyType)
        };
    }

    private static Color GetEnergyColor(string energyType)
    {
        switch (energyType)
        {
            case "Acid": return new Color(0.5f, 0.9f, 0.3f);      // Green
            case "Cold": return new Color(0.6f, 0.8f, 1.0f);      // Ice blue
            case "Electricity": return new Color(1.0f, 1.0f, 0.4f); // Yellow
            case "Fire": return new Color(1.0f, 0.5f, 0.2f);       // Orange-red
            case "Sonic": return new Color(0.8f, 0.6f, 1.0f);      // Purple
            default: return new Color(0.8f, 0.8f, 0.8f);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Ring of Force Shield
    //  DMG p.230: +2 shield bonus to AC (force effect), CL 9, 8500 gp
    //  Does not occupy a hand. No ACP or ASF.
    // ════════════════════════════════════════════════════════════

    public static ItemData CreateForceShieldRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_FORCE_SHIELD,
            Name = "Ring of Force Shield",
            Description = "An iron band that creates an invisible shield of force that hovers near the wearer. The shield grants a +2 shield bonus to AC. It has no armor check penalty or arcane spell failure chance.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_FORCE_SHIELD,
            RingShieldBonus = 2,
            RingCasterLevel = 9,
            BasePriceGp = 8500,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.6f, 0.9f, 1.0f) // Cyan
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Ring of Evasion
    //  DMG p.230: Grants Evasion as the class feature, CL 7, 25000 gp
    // ════════════════════════════════════════════════════════════

    public static ItemData CreateEvasionRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_EVASION,
            Name = "Ring of Evasion",
            Description = "This ring continually grants the wearer the ability to avoid damage as if she had evasion. Whenever she makes a Reflex saving throw to determine whether she takes half damage, a successful save results in no damage.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_EVASION,
            RingGrantsEvasion = true,
            RingCasterLevel = 7,
            BasePriceGp = 25000,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.9f, 0.9f, 0.5f) // Gold
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Ring of Freedom of Movement
    //  DMG p.230: Continuous freedom of movement, CL 7, 40000 gp
    // ════════════════════════════════════════════════════════════

    public static ItemData CreateFreedomOfMovementRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_FREEDOM_OF_MOVEMENT,
            Name = "Ring of Freedom of Movement",
            Description = "This gold ring allows the wearer to act as if continually under the effect of a freedom of movement spell. The wearer can move and attack normally while underwater, in magical webs, or under similar movement-restricting effects.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_FREEDOM_OF_MOVEMENT,
            RingGrantsFreedomOfMovement = true,
            RingCasterLevel = 7,
            BasePriceGp = 40000,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(1.0f, 0.85f, 0.4f) // Golden
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Ring of Feather Falling
    //  DMG p.230: Continuous feather fall, CL 1, 2200 gp
    // ════════════════════════════════════════════════════════════

    public static ItemData CreateFeatherFallingRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_FEATHER_FALLING,
            Name = "Ring of Feather Falling",
            Description = "This ring is crafted with a feather pattern around the outside. It acts exactly like a feather fall spell, activated immediately if the wearer falls more than 5 feet.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_FEATHER_FALLING,
            RingGrantsFeatherFall = true,
            RingCasterLevel = 1,
            BasePriceGp = 2200,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.95f, 0.95f, 0.9f) // White
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Ring of Swimming
    //  DMG p.233: +5 competence bonus to Swim, CL 2, 2500 gp
    // ════════════════════════════════════════════════════════════

    public static ItemData CreateSwimmingRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_SWIMMING,
            Name = "Ring of Swimming",
            Description = "This silver ring has a wave pattern etched into the band. It continually grants the wearer a +5 competence bonus on Swim checks.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_SWIMMING,
            RingSkillBonus = 5,
            RingSkillName = "Swim",
            RingCasterLevel = 2,
            BasePriceGp = 2500,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.3f, 0.6f, 1.0f) // Blue
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Ring of Climbing
    //  DMG p.230: +5 competence bonus to Climb, CL 5, 2500 gp
    // ════════════════════════════════════════════════════════════

    public static ItemData CreateClimbingRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_CLIMBING,
            Name = "Ring of Climbing",
            Description = "This ring is actually a pair of iron bands connected by a chain. It continually grants the wearer a +5 competence bonus on Climb checks.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_CLIMBING,
            RingSkillBonus = 5,
            RingSkillName = "Climb",
            RingCasterLevel = 5,
            BasePriceGp = 2500,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.6f, 0.45f, 0.3f) // Brown
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Ring of Jumping
    //  DMG p.231: +5 competence bonus to Jump, CL 2, 2500 gp
    // ════════════════════════════════════════════════════════════

    public static ItemData CreateJumpingRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_JUMPING,
            Name = "Ring of Jumping",
            Description = "This ring continually allows the wearer to leap about, providing a +5 competence bonus on Jump checks.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_JUMPING,
            RingSkillBonus = 5,
            RingSkillName = "Jump",
            RingCasterLevel = 2,
            BasePriceGp = 2500,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.4f, 0.9f, 0.4f) // Green
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Ring of Water Walking
    //  DMG p.233: Continuous water walk, CL 9, 15000 gp
    // ════════════════════════════════════════════════════════════

    public static ItemData CreateWaterWalkingRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_WATER_WALKING,
            Name = "Ring of Water Walking",
            Description = "This ring allows the wearer to continually walk on water as though she were walking on solid ground.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_WATER_WALKING,
            RingGrantsWaterWalking = true,
            RingCasterLevel = 9,
            BasePriceGp = 15000,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.4f, 0.7f, 0.9f) // Sea blue
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Ring of Sustenance
    //  DMG p.233: No food/water/sleep needed (after 1 week attunement), CL 5, 2500 gp
    // ════════════════════════════════════════════════════════════

    public static ItemData CreateSustenanceRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_SUSTENANCE,
            Name = "Ring of Sustenance",
            Description = "This ring continually provides its wearer with life-sustaining nourishment. The ring also refreshes the body and mind, so the wearer needs only sleep 2 hours per day to gain the benefit of 8 hours of sleep.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_SUSTENANCE,
            RingGrantsSustenance = true,
            RingCasterLevel = 5,
            BasePriceGp = 2500,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.5f, 0.85f, 0.5f) // Soft green
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Ring of Mind Shielding
    //  DMG p.232: Immune to detect thoughts, discern lies, alignment detection, CL 3, 8000 gp
    // ════════════════════════════════════════════════════════════

    public static ItemData CreateMindShieldingRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_MIND_SHIELDING,
            Name = "Ring of Mind Shielding",
            Description = "This ring is usually of fine workmanship and wrought from heavy gold. The wearer is continually immune to detect thoughts, discern lies, and any attempt to magically discern her alignment.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_MIND_SHIELDING,
            RingGrantsMindShielding = true,
            RingCasterLevel = 3,
            BasePriceGp = 8000,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.7f, 0.5f, 0.9f) // Purple
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Ring of Warmth
    //  Endure elements (cold), CL 5, 8000 gp
    //  Note: While not in the SRD ring table, it appears in various
    //  3.5e sources. Included per task specification.
    // ════════════════════════════════════════════════════════════

    public static ItemData CreateWarmthRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_WARMTH,
            Name = "Ring of Warmth",
            Description = "This simple copper ring provides continual warmth, protecting the wearer from cold environments as if by endure elements (cold only).",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_WARMTH,
            RingGrantsColdEndurance = true,
            RingCasterLevel = 5,
            BasePriceGp = 8000,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(1.0f, 0.6f, 0.3f) // Warm orange
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Ring of Chameleon Power
    //  DMG p.230: +10 competence bonus to Hide, CL 3, 12700 gp
    // ════════════════════════════════════════════════════════════

    public static ItemData CreateChameleonPowerRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_CHAMELEON_POWER,
            Name = "Ring of Chameleon Power",
            Description = "As a free action, the wearer of this ring can gain the ability to magically blend in with the surroundings. This grants a +10 competence bonus on her Hide checks.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_CHAMELEON_POWER,
            RingSkillBonus = 10,
            RingSkillName = "Hide",
            RingCasterLevel = 3,
            BasePriceGp = 12700,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.5f, 0.7f, 0.5f) // Muted green
        };
    }

    // ════════════════════════════════════════════════════════════════════
    //  TIER 2 — Sprint 2: Active Ability Rings (9 rings)
    //  DMG pp. 229–233: Command word activation, use tracking.
    // ════════════════════════════════════════════════════════════════════

    // ── Ring of Invisibility (DMG p.232) ──
    // At will, CL 3, standard action, casts Invisibility on wearer
    // Duration: 1 min/level = 30 rounds at CL 3
    // Price: 20,000 gp
    public static ItemData CreateInvisibilityRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_INVISIBILITY,
            Name = "Ring of Invisibility",
            Description = "By activating this simple band, the wearer can benefit from invisibility, as the spell. The ring functions at will, allowing the wearer to activate and deactivate invisibility at any time.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_INVISIBILITY,
            RingCasterLevel = 3,
            BasePriceGp = 20000,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.85f, 0.85f, 0.95f), // Ghostly white-blue
            RingAbilities = new List<RingAbility>
            {
                new RingAbility
                {
                    AbilityId = "invisibility",
                    DisplayName = "Become Invisible",
                    Description = "Cast Invisibility on yourself (CL 3, 30 rounds). Breaks on attack.",
                    Frequency = RingUseFrequency.AtWill,
                    ActionType = RingActionType.Standard,
                    RequiresTarget = false,
                    CasterLevel = 3,
                    RangeFeet = 0
                }
            }
        };
    }

    // ── Ring of Blinking (DMG p.230) ──
    // At will, CL 7, standard action, casts Blink on wearer
    // Duration: 1 round/level = 7 rounds at CL 7
    // 50% miss chance incoming, 20% miss on own attacks
    // Price: 27,000 gp
    public static ItemData CreateBlinkingRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_BLINKING,
            Name = "Ring of Blinking",
            Description = "On command, this ring makes the wearer blink, as with the blink spell. The wearer rapidly shifts between the Material and Ethereal Planes, gaining a 50% miss chance on incoming attacks but suffering a 20% miss chance on their own attacks.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_BLINKING,
            RingCasterLevel = 7,
            BasePriceGp = 27000,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.6f, 0.7f, 0.95f), // Ethereal blue
            RingAbilities = new List<RingAbility>
            {
                new RingAbility
                {
                    AbilityId = "blink",
                    DisplayName = "Blink",
                    Description = "Blink between planes (CL 7, 7 rounds). 50% miss chance incoming, 20% on own attacks.",
                    Frequency = RingUseFrequency.AtWill,
                    ActionType = RingActionType.Standard,
                    RequiresTarget = false,
                    CasterLevel = 7,
                    RangeFeet = 0
                }
            }
        };
    }

    // ── Ring of Animal Friendship (DMG p.229) ──
    // 3/day, CL 1, standard action, Charm Animal Will DC 11
    // Target: Animal type only, max 12 HD charmed simultaneously
    // Price: 10,800 gp
    public static ItemData CreateAnimalFriendshipRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_ANIMAL_FRIENDSHIP,
            Name = "Ring of Animal Friendship",
            Description = "On command, this ring affects an animal as if the wearer had cast charm animal (Will DC 11 negates). The wearer can have up to 12 Hit Dice of charmed animals at any one time.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_ANIMAL_FRIENDSHIP,
            RingCasterLevel = 1,
            BasePriceGp = 10800,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.6f, 0.85f, 0.45f), // Natural green
            RingAbilities = new List<RingAbility>
            {
                new RingAbility
                {
                    AbilityId = "charm_animal",
                    DisplayName = "Charm Animal",
                    Description = "Charm one animal (Will DC 11, 1 hour). Max 12 HD charmed total.",
                    Frequency = RingUseFrequency.PerDay,
                    MaxUsesPerPeriod = 3,
                    ActionType = RingActionType.Standard,
                    RequiresTarget = true,
                    RangeFeet = 25,
                    CasterLevel = 1,
                    SaveDC = 11,
                    SaveType = "Will"
                }
            }
        };
    }

    // ── Ring of the Ram (DMG p.233) ──
    // Charge-based: 50 charges, expend 1–3 per use, regen 1d10/day
    // Ranged touch attack: 1d6 force per charge, bull rush Str 25
    // Price: 8,600 gp
    public static ItemData CreateRamRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_RAM,
            Name = "Ring of the Ram",
            Description = "The wearer can command the ring to give forth a ramlike force, manifested by a vaguely discernible shape that resembles the head of a ram. This force strikes a single target, dealing 1d6 points of damage per charge expended and possibly bull rushing the target.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_RAM,
            RingCasterLevel = 9,
            BasePriceGp = 8600,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.85f, 0.55f, 0.35f), // Ram brown-orange
            RingCurrentCharges = 50,
            RingMaxCharges = 50,
            RingChargesPerDay = 10, // 1d10 per day
            RingAbilities = new List<RingAbility>
            {
                new RingAbility
                {
                    AbilityId = "ram_1charge",
                    DisplayName = "Ram (1 charge)",
                    Description = "1d6 force damage + bull rush (Str 26). Ranged touch, 50 ft.",
                    Frequency = RingUseFrequency.Charged,
                    ChargeCost = 1,
                    ActionType = RingActionType.Standard,
                    RequiresTarget = true,
                    RangeFeet = 50,
                    CasterLevel = 9
                },
                new RingAbility
                {
                    AbilityId = "ram_2charges",
                    DisplayName = "Ram (2 charges)",
                    Description = "2d6 force damage + bull rush (Str 27). Ranged touch, 50 ft.",
                    Frequency = RingUseFrequency.Charged,
                    ChargeCost = 2,
                    ActionType = RingActionType.Standard,
                    RequiresTarget = true,
                    RangeFeet = 50,
                    CasterLevel = 9
                },
                new RingAbility
                {
                    AbilityId = "ram_3charges",
                    DisplayName = "Ram (3 charges)",
                    Description = "3d6 force damage + bull rush (Str 28). Ranged touch, 50 ft. 2× vs objects.",
                    Frequency = RingUseFrequency.Charged,
                    ChargeCost = 3,
                    ActionType = RingActionType.Standard,
                    RequiresTarget = true,
                    RangeFeet = 50,
                    CasterLevel = 9
                }
            }
        };
    }

    // ── Ring of Telekinesis (DMG p.233) ──
    // At will, CL 9, standard action
    // Three modes: Violent Thrust, Combat Maneuver, Sustained Force
    // Price: 75,000 gp
    public static ItemData CreateTelekinesisRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_TELEKINESIS,
            Name = "Ring of Telekinesis",
            Description = "This ring allows the wearer to use the spell telekinesis on command, giving access to all three modes: sustained force, combat maneuver, and violent thrust.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_TELEKINESIS,
            RingCasterLevel = 9,
            BasePriceGp = 75000,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.75f, 0.5f, 0.95f), // Telekinetic purple
            RingAbilities = new List<RingAbility>
            {
                new RingAbility
                {
                    AbilityId = "telekinesis_violent_thrust",
                    DisplayName = "Violent Thrust",
                    Description = "Hurl objects at target: up to 5d6 bludgeoning, 225 lb limit. CL 9.",
                    Frequency = RingUseFrequency.AtWill,
                    ActionType = RingActionType.Standard,
                    RequiresTarget = true,
                    RangeFeet = 760,
                    CasterLevel = 9
                },
                new RingAbility
                {
                    AbilityId = "telekinesis_combat_maneuver",
                    DisplayName = "Combat Maneuver",
                    Description = "Telekinetic bull rush, disarm, or trip. Opposed check at CL 9.",
                    Frequency = RingUseFrequency.AtWill,
                    ActionType = RingActionType.Standard,
                    RequiresTarget = true,
                    RangeFeet = 760,
                    CasterLevel = 9
                },
                new RingAbility
                {
                    AbilityId = "telekinesis_sustained_force",
                    DisplayName = "Sustained Force",
                    Description = "Move objects up to 225 lbs. Concentration, up to 9 rounds.",
                    Frequency = RingUseFrequency.AtWill,
                    ActionType = RingActionType.Standard,
                    RequiresTarget = false,
                    RangeFeet = 760,
                    CasterLevel = 9
                }
            }
        };
    }

    // ── Ring of X-Ray Vision (DMG p.233) ──
    // At will, CL 5, standard action
    // See through solid matter 20 ft, 10 rounds
    // Con damage on 2nd+ use per rest
    // Price: 25,000 gp
    public static ItemData CreateXRayVisionRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_X_RAY_VISION,
            Name = "Ring of X-Ray Vision",
            Description = "On command, this ring gives its wearer the ability to see into and through solid matter. Vision range is 20 feet, with the viewer seeing as if he were looking at something in normal light even if there is no illumination. Each use has a 1-minute duration. Repeated use causes Constitution damage.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_X_RAY_VISION,
            RingCasterLevel = 5,
            BasePriceGp = 25000,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.3f, 0.95f, 0.65f), // X-ray green
            RingAbilities = new List<RingAbility>
            {
                new RingAbility
                {
                    AbilityId = "xray_vision",
                    DisplayName = "X-Ray Vision",
                    Description = "See through barriers 20 ft (10 rounds). 1d4 Con damage per use after first per rest.",
                    Frequency = RingUseFrequency.AtWill,
                    ActionType = RingActionType.Standard,
                    RequiresTarget = false,
                    RangeFeet = 20,
                    CasterLevel = 5
                }
            }
        };
    }

    // ── Ring of Shooting Stars (DMG p.233) ──
    // 5 abilities with mixed frequency, CL 12
    // Price: 50,000 gp
    public static ItemData CreateShootingStarsRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_SHOOTING_STARS,
            Name = "Ring of Shooting Stars",
            Description = "This ring has two modes of operation: indoors and underground, or outdoors at night. It provides five abilities: Dancing Lights and Shooting Stars (outdoors at night only), Light, Ball Lightning, and Faerie Fire (anywhere).",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_SHOOTING_STARS,
            RingCasterLevel = 12,
            BasePriceGp = 50000,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(1.0f, 0.85f, 0.3f), // Stellar gold
            RingAbilities = new List<RingAbility>
            {
                new RingAbility
                {
                    AbilityId = "shooting_stars_light",
                    DisplayName = "Light",
                    Description = "As Light spell (CL 12, 120 minutes).",
                    Frequency = RingUseFrequency.AtWill,
                    ActionType = RingActionType.Standard,
                    RequiresTarget = false,
                    CasterLevel = 12
                },
                new RingAbility
                {
                    AbilityId = "shooting_stars_dancing_lights",
                    DisplayName = "Dancing Lights",
                    Description = "As Dancing Lights spell (CL 12, 1 minute). Outdoors at night only.",
                    Frequency = RingUseFrequency.AtWill,
                    ActionType = RingActionType.Standard,
                    RequiresTarget = false,
                    RequiresOutdoorsNight = true,
                    CasterLevel = 12
                },
                new RingAbility
                {
                    AbilityId = "shooting_stars_faerie_fire",
                    DisplayName = "Faerie Fire",
                    Description = "Outline targets: -20 Hide, reveal invisible. 12 minutes.",
                    Frequency = RingUseFrequency.PerDay,
                    MaxUsesPerPeriod = 2,
                    ActionType = RingActionType.Standard,
                    RequiresTarget = true,
                    RangeFeet = 80,
                    CasterLevel = 12
                },
                new RingAbility
                {
                    AbilityId = "shooting_stars_ball_lightning",
                    DisplayName = "Ball Lightning",
                    Description = "Create 1–4 lightning balls. Damage scales inversely with count. Reflex DC 13 half.",
                    Frequency = RingUseFrequency.PerDay,
                    MaxUsesPerPeriod = 1,
                    ActionType = RingActionType.Standard,
                    RequiresTarget = true,
                    RangeFeet = 120,
                    CasterLevel = 12,
                    SaveDC = 13,
                    SaveType = "Reflex"
                },
                new RingAbility
                {
                    AbilityId = "shooting_stars_shooting_stars",
                    DisplayName = "Shooting Stars",
                    Description = "Fire 1–3 stars: 12 fire damage each in 5-ft radius. Reflex DC 13 half. Outdoors at night only.",
                    Frequency = RingUseFrequency.PerWeek,
                    MaxUsesPerPeriod = 3,
                    ActionType = RingActionType.Standard,
                    RequiresTarget = true,
                    RangeFeet = 70,
                    RequiresOutdoorsNight = true,
                    CasterLevel = 12,
                    SaveDC = 13,
                    SaveType = "Reflex"
                }
            }
        };
    }

    // ── Ring of Spell Turning (DMG p.233) ──
    // Automatic — no activation needed, applies on equip
    // Pool: 1d4+6 spell levels (7–10), refreshes on rest
    // Reflects targeted spells back at caster
    // Price: 98,280 gp
    public static ItemData CreateSpellTurningRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_SPELL_TURNING,
            Name = "Ring of Spell Turning",
            Description = "Up to three times per day on command, this ring automatically reflects the next spell cast at the wearer. The ring absorbs 1d4+6 spell levels of spells directed at the wearer, reflecting them back upon the original caster. Once the pool is depleted, the ring becomes inert until recharged on rest.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_SPELL_TURNING,
            RingCasterLevel = 13,
            BasePriceGp = 98280,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(0.95f, 0.85f, 0.5f), // Reflective gold
            RingAbilities = new List<RingAbility>
            {
                new RingAbility
                {
                    AbilityId = "spell_turning",
                    DisplayName = "Spell Turning",
                    Description = "Automatically reflects targeted spells. Pool: 1d4+6 spell levels.",
                    Frequency = RingUseFrequency.Automatic,
                    ActionType = RingActionType.None,
                    RequiresTarget = false,
                    CasterLevel = 13
                }
            }
        };
    }

    // ── Ring of Djinni Calling (DMG p.232) ──
    // 1/week, CL 17, full-round action
    // Summons a Noble Djinni for 1 hour (600 rounds)
    // If Djinni is slain, ring becomes permanently inert
    // Price: 125,000 gp
    public static ItemData CreateDjinniCallingRing()
    {
        return new ItemData
        {
            Id = RingNames.RING_OF_DJINNI_CALLING,
            Name = "Ring of Djinni Calling",
            Description = "One of the many rings of fable, this \"brass ring\" is actually forged of gold. It serves as a special gate by means of which a specific noble djinni can be called from the Elemental Plane of Air. The djinni serves its master for 1 hour. If the djinni is ever killed, the ring becomes nonmagical and worthless.",
            Type = ItemType.Ring,
            Slot = EquipSlot.EitherRing,
            IsRing = true,
            RingId = RingNames.RING_OF_DJINNI_CALLING,
            RingCasterLevel = 17,
            BasePriceGp = 125000,
            WeightLbs = 0f,
            CountsAsMagicForBypass = true,
            IconChar = RingIcon,
            IconColor = new Color(1.0f, 0.8f, 0.3f), // Brass/gold
            RingAbilities = new List<RingAbility>
            {
                new RingAbility
                {
                    AbilityId = "summon_djinni",
                    DisplayName = "Call Noble Djinni",
                    Description = "Summon a Noble Djinni for 1 hour. Full-round action. If slain, ring is destroyed.",
                    Frequency = RingUseFrequency.PerWeek,
                    MaxUsesPerPeriod = 1,
                    ActionType = RingActionType.FullRound,
                    RequiresTarget = false,
                    CasterLevel = 17
                }
            }
        };
    }
}
