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
}
