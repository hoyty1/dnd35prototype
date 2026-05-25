using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Factory methods for creating D&D 3.5e wondrous item ItemData instances.
/// All items from DMG pp. 248–271, organized by equipment slot.
/// Wondrous items use their specific EquipSlot and ItemType.Wondrous.
/// Follows the same pattern as RingFactory.cs.
/// </summary>
public static class WondrousItemFactory
{
    // ════════════════════════════════════════════════════════════
    //  Common Wondrous Item Icons (by slot)
    // ════════════════════════════════════════════════════════════
    private const string HeadIcon = "👑";
    private const string FaceIcon = "👓";
    private const string NeckIcon = "📿";
    private const string BackIcon = "🧥";
    private const string TorsoIcon = "👘";
    private const string WaistIcon = "🪢";
    private const string WristsIcon = "⌚";
    private const string HandsIcon = "🧤";
    private const string FeetIcon = "👢";
    private const string SlotlessIcon = "✨";

    // Slot-based icon colors
    private static readonly Color HeadColor = new Color(1f, 0.9f, 0.4f);       // Gold
    private static readonly Color FaceColor = new Color(0.6f, 0.9f, 1f);       // Sky blue
    private static readonly Color NeckColor = new Color(0.9f, 0.7f, 0.9f);     // Lavender
    private static readonly Color BackColor = new Color(0.5f, 0.7f, 0.5f);     // Forest green
    private static readonly Color TorsoColor = new Color(0.8f, 0.6f, 0.8f);    // Purple
    private static readonly Color WaistColor = new Color(0.9f, 0.6f, 0.3f);    // Bronze
    private static readonly Color WristsColor = new Color(0.7f, 0.7f, 0.9f);   // Steel blue
    private static readonly Color HandsColor = new Color(0.8f, 0.5f, 0.3f);    // Brown
    private static readonly Color FeetColor = new Color(0.6f, 0.5f, 0.4f);     // Leather
    private static readonly Color SlotlessColor = new Color(0.9f, 0.9f, 1f);   // White/shimmer

    // ════════════════════════════════════════════════════════════
    //  Helper: Create Base Wondrous Item
    // ════════════════════════════════════════════════════════════

    private static ItemData CreateBaseWondrous(string id, string name, string description,
        EquipSlot slot, int priceGp, int casterLevel, float weightLbs = 0f,
        string icon = null, Color? iconColor = null)
    {
        bool isSlotless = (slot == EquipSlot.Slotless);
        return new ItemData
        {
            Id = id,
            Name = name,
            Description = description,
            Type = ItemType.Wondrous,
            Slot = slot,
            IsWondrous = true,
            WondrousId = id,
            WondrousRequiredSlot = slot,
            IsSlotless = isSlotless,
            WondrousCasterLevel = casterLevel,
            BasePriceGp = priceGp,
            WeightLbs = weightLbs,
            CountsAsMagicForBypass = true,
            IconChar = icon ?? SlotlessIcon,
            IconColor = iconColor ?? SlotlessColor
        };
    }

    // ════════════════════════════════════════════════════════════
    //  HEAD SLOT — Headbands, circlets, helms, hats (DMG p.258)
    // ════════════════════════════════════════════════════════════

    /// <summary>Headband of Intellect (+2/+4/+6 enhancement bonus to Intelligence). DMG p.258.</summary>
    public static ItemData CreateHeadbandOfIntellect(int bonus)
    {
        int price = bonus * bonus * 1000; // bonus² × 1000 gp
        int cl = 8;
        string id = $"headband_of_intellect_{bonus}";
        var item = CreateBaseWondrous(id, $"Headband of Intellect +{bonus}",
            $"This headband grants the wearer an enhancement bonus of +{bonus} to Intelligence.",
            EquipSlot.Head, price, cl, 0f, HeadIcon, HeadColor);
        item.WondrousItemType = "ability";
        item.WondrousAbilityBonus = bonus;
        item.WondrousAbilityType = "Int";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Circlet of Persuasion (+3 competence bonus to Charisma-based checks). DMG p.253.</summary>
    public static ItemData CreateCircletOfPersuasion()
    {
        var item = CreateBaseWondrous(WondrousItemNames.CIRCLET_OF_PERSUASION,
            "Circlet of Persuasion",
            "This silver circlet grants the wearer a +3 competence bonus on Charisma-based checks.",
            EquipSlot.Head, 4500, 5, 0f, HeadIcon, HeadColor);
        item.WondrousItemType = "skill";
        item.WondrousSkillBonus = 3;
        item.WondrousSkillName = "Charisma checks";
        item.WondrousSkillBonusType = "competence";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Hat of Disguise (Disguise Self at will). DMG p.258.</summary>
    public static ItemData CreateHatOfDisguise()
    {
        var item = CreateBaseWondrous(WondrousItemNames.HAT_OF_DISGUISE,
            "Hat of Disguise",
            "This apparently normal hat allows its wearer to alter her appearance as with a disguise self spell.",
            EquipSlot.Head, 1800, 1, 0f, HeadIcon, HeadColor);
        item.WondrousItemType = "utility";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousUsesPerDay = 0; // At will
        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  FACE/EYES SLOT — Goggles, lenses (DMG p.256)
    // ════════════════════════════════════════════════════════════

    /// <summary>Goggles of Night (Darkvision 60 ft). DMG p.257.</summary>
    public static ItemData CreateGogglesOfNight()
    {
        var item = CreateBaseWondrous(WondrousItemNames.GOGGLES_OF_NIGHT,
            "Goggles of Night",
            "The lenses of this item are made of dark crystal. Even though the lenses are dark, the wearer can see normally and gains darkvision out to 60 feet.",
            EquipSlot.FaceEyes, 12000, 3, 0f, FaceIcon, FaceColor);
        item.WondrousItemType = "utility";
        item.WondrousDarkvisionRange = 60;
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Eyes of the Eagle (+5 competence bonus to Spot). DMG p.256.</summary>
    public static ItemData CreateEyesOfTheEagle()
    {
        var item = CreateBaseWondrous(WondrousItemNames.EYES_OF_THE_EAGLE,
            "Eyes of the Eagle",
            "These lenses grant a +5 competence bonus on Spot checks.",
            EquipSlot.FaceEyes, 2500, 3, 0f, FaceIcon, FaceColor);
        item.WondrousItemType = "skill";
        item.WondrousSkillBonus = 5;
        item.WondrousSkillName = "Spot";
        item.WondrousSkillBonusType = "competence";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  NECK/THROAT SLOT — Amulets, periapts, brooches (DMG p.246)
    // ════════════════════════════════════════════════════════════

    /// <summary>Amulet of Natural Armor (+1 to +5 enhancement to natural armor). DMG p.246.</summary>
    public static ItemData CreateAmuletOfNaturalArmor(int bonus)
    {
        int price = bonus * bonus * 2000;
        int cl = bonus * 3;
        string id = $"amulet_of_natural_armor_{bonus}";
        var item = CreateBaseWondrous(id, $"Amulet of Natural Armor +{bonus}",
            $"This amulet toughens the wearer's body and flesh, giving a +{bonus} enhancement bonus to natural armor.",
            EquipSlot.Neck, price, cl, 0f, NeckIcon, NeckColor);
        item.WondrousItemType = "ac";
        item.WondrousACBonus = bonus;
        item.WondrousACBonusType = "natural";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Amulet of Health (+2/+4/+6 enhancement bonus to Constitution). DMG p.246.</summary>
    public static ItemData CreateAmuletOfHealth(int bonus)
    {
        int price = bonus * bonus * 1000;
        string id = $"amulet_of_health_{bonus}";
        var item = CreateBaseWondrous(id, $"Amulet of Health +{bonus}",
            $"This amulet is a golden disk on a chain that grants the wearer a +{bonus} enhancement bonus to Constitution.",
            EquipSlot.Neck, price, 8, 0f, NeckIcon, NeckColor);
        item.WondrousItemType = "ability";
        item.WondrousAbilityBonus = bonus;
        item.WondrousAbilityType = "Con";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Periapt of Wisdom (+2/+4/+6 enhancement bonus to Wisdom). DMG p.263.</summary>
    public static ItemData CreatePeriaptOfWisdom(int bonus)
    {
        int price = bonus * bonus * 1000;
        string id = $"periapt_of_wisdom_{bonus}";
        var item = CreateBaseWondrous(id, $"Periapt of Wisdom +{bonus}",
            $"This stone on a chain grants the wearer a +{bonus} enhancement bonus to Wisdom.",
            EquipSlot.Neck, price, 8, 0f, NeckIcon, NeckColor);
        item.WondrousItemType = "ability";
        item.WondrousAbilityBonus = bonus;
        item.WondrousAbilityType = "Wis";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Brooch of Shielding (absorbs magic missiles, 101 HP). DMG p.250.</summary>
    public static ItemData CreateBroochOfShielding()
    {
        var item = CreateBaseWondrous(WondrousItemNames.BROOCH_OF_SHIELDING,
            "Brooch of Shielding",
            "This appears to be a piece of silver or gold jewelry. It absorbs magic missiles (as the shield spell), absorbing a total of 101 points of damage before being destroyed.",
            EquipSlot.Neck, 1500, 1, 0f, NeckIcon, NeckColor);
        item.WondrousItemType = "utility";
        item.WondrousMaxCharges = 101;
        item.WondrousCurrentCharges = 101;
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Amulet of Mighty Fists (+1 to +5 enhancement bonus to unarmed/natural attacks). DMG p.246.</summary>
    public static ItemData CreateAmuletOfMightyFists(int bonus)
    {
        // Price: 6000 × bonus² (DMG p.246)
        int price = 6000 * bonus * bonus;
        string id = $"amulet_of_mighty_fists_{bonus}";
        var item = CreateBaseWondrous(id, $"Amulet of Mighty Fists +{bonus}",
            $"This amulet grants a +{bonus} enhancement bonus to the wearer's unarmed attacks and natural weapon attacks.",
            EquipSlot.Neck, price, 5 * bonus, 0f, NeckIcon, NeckColor);
        item.WondrousItemType = "attack";
        item.WondrousMightyFistsBonus = bonus;
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  SHOULDERS/BACK SLOT — Cloaks, capes (DMG p.252)
    // ════════════════════════════════════════════════════════════

    /// <summary>Cloak of Resistance (+1 to +5 resistance bonus to all saves). DMG p.253.</summary>
    public static ItemData CreateCloakOfResistance(int bonus)
    {
        int price = bonus * bonus * 1000;
        int cl = bonus * 3;
        string id = $"cloak_of_resistance_{bonus}";
        var item = CreateBaseWondrous(id, $"Cloak of Resistance +{bonus}",
            $"This finely crafted cloak offers protection in the form of a +{bonus} resistance bonus to all saving throws.",
            EquipSlot.Back, price, cl, 1f, BackIcon, BackColor);
        item.WondrousItemType = "save";
        item.WondrousSaveBonus = bonus;
        item.WondrousSaveType = "all";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Cloak of Charisma (+2/+4/+6 enhancement bonus to Charisma). DMG p.252.</summary>
    public static ItemData CreateCloakOfCharisma(int bonus)
    {
        int price = bonus * bonus * 1000;
        string id = $"cloak_of_charisma_{bonus}";
        var item = CreateBaseWondrous(id, $"Cloak of Charisma +{bonus}",
            $"This lightweight cloak of fine silk shimmers and grants the wearer a +{bonus} enhancement bonus to Charisma.",
            EquipSlot.Back, price, 8, 2f, BackIcon, BackColor);
        item.WondrousItemType = "ability";
        item.WondrousAbilityBonus = bonus;
        item.WondrousAbilityType = "Cha";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Cloak of Elvenkind (+5 competence bonus to Hide). DMG p.253.</summary>
    public static ItemData CreateCloakOfElvenkind()
    {
        var item = CreateBaseWondrous(WondrousItemNames.CLOAK_OF_ELVENKIND,
            "Cloak of Elvenkind",
            "This cloak of neutral gray cloth is indistinguishable from an ordinary cloak. However, when worn with the hood up, it gives the wearer a +5 competence bonus on Hide checks.",
            EquipSlot.Back, 2500, 3, 1f, BackIcon, BackColor);
        item.WondrousItemType = "skill";
        item.WondrousSkillBonus = 5;
        item.WondrousSkillName = "Hide";
        item.WondrousSkillBonusType = "competence";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>
    /// Cloak of Displacement (Minor: 20% miss chance; Major: 50% miss chance). DMG p.253.
    /// Emits a shimmering displacement aura making the wearer appear to be slightly elsewhere.
    /// Attacks have a miss chance as if the wearer had concealment. True seeing negates.
    /// </summary>
    public static ItemData CreateCloakOfDisplacement(bool major)
    {
        string id = major ? WondrousItemNames.CLOAK_OF_DISPLACEMENT_MAJOR : WondrousItemNames.CLOAK_OF_DISPLACEMENT_MINOR;
        string tier = major ? "Major" : "Minor";
        int missChance = major ? 50 : 20;
        int price = major ? 50000 : 24000;
        int cl = major ? 15 : 3;
        var item = CreateBaseWondrous(id, $"Cloak of Displacement, {tier}",
            major
                ? "This item appears to be a normal cloak, but when worn its magical properties distort and warp light waves. This displacement works similar to the displacement spell (50% miss chance). It functions continually."
                : "This item appears to be a normal cloak, but when worn its magical properties distort and warp light waves. This lesser displacement works similar to the blur spell (20% miss chance). It functions continually.",
            EquipSlot.Back, price, cl, 1f, BackIcon, BackColor);
        item.WondrousItemType = "ac";
        item.WondrousDisplacementMissChance = missChance;
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Wings of Flying (fly 60 ft average maneuverability, unlimited). DMG p.271.</summary>
    public static ItemData CreateWingsOfFlying()
    {
        var item = CreateBaseWondrous(WondrousItemNames.WINGS_OF_FLYING,
            "Wings of Flying",
            "These wings appear to be a cloak made from silk. When the command word is spoken, the cloak turns into a pair of bat wings or bird wings, granting fly 60 ft (average maneuverability). Can fly as much as desired but must rest 1 hour per 12 hours of flight.",
            EquipSlot.Back, 54000, 10, 2f, BackIcon, BackColor);
        item.WondrousItemType = "movement";
        item.WondrousGrantsMovement = true;
        item.WondrousMovementMode = "fly";
        item.WondrousMovementSpeed = 60;
        item.WondrousFlightManeuverability = "average";
        item.WondrousFlightDurationRounds = 0; // unlimited duration
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  TORSO/BODY SLOT — Vests, robes, shirts (DMG p.265)
    // ════════════════════════════════════════════════════════════

    /// <summary>Vest of Escape (+6 competence bonus to Escape Artist & Open Lock). DMG p.270.</summary>
    public static ItemData CreateVestOfEscape()
    {
        var item = CreateBaseWondrous(WondrousItemNames.VEST_OF_ESCAPE,
            "Vest of Escape",
            "Hidden within this simple-looking vest are compartments and tools. The wearer gains a +6 competence bonus on Escape Artist checks and a +6 competence bonus on Open Lock checks.",
            EquipSlot.Torso, 5200, 4, 0f, TorsoIcon, TorsoColor);
        item.WondrousItemType = "skill";
        item.WondrousSkillBonus = 6;
        item.WondrousSkillName = "Escape Artist";
        item.WondrousSkillBonus2 = 6;
        item.WondrousSkillName2 = "Open Lock";
        item.WondrousSkillBonusType = "competence";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Monk's Belt (AC bonus as Monk, +1 effective monk level). DMG p.248.</summary>
    public static ItemData CreateMonksBelt()
    {
        var item = CreateBaseWondrous(WondrousItemNames.MONKS_BELT,
            "Monk's Belt",
            "This belt grants the wearer the AC bonus, unarmed damage, and speed of a monk 5 levels higher if already a monk, or as a 5th-level monk if not.",
            EquipSlot.Torso, 13000, 10, 1f, TorsoIcon, TorsoColor);
        item.WondrousItemType = "utility";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  WAIST SLOT — Belts (DMG p.248)
    // ════════════════════════════════════════════════════════════

    /// <summary>Belt of Giant Strength (+2/+4/+6 enhancement bonus to Strength). DMG p.248.</summary>
    public static ItemData CreateBeltOfGiantStrength(int bonus)
    {
        int price = bonus * bonus * 1000;
        string id = $"belt_of_giant_strength_{bonus}";
        var item = CreateBaseWondrous(id, $"Belt of Giant Strength +{bonus}",
            $"This wide belt is made of thick leather and grants the wearer a +{bonus} enhancement bonus to Strength.",
            EquipSlot.Waist, price, 8, 1f, WaistIcon, WaistColor);
        item.WondrousItemType = "ability";
        item.WondrousAbilityBonus = bonus;
        item.WondrousAbilityType = "Str";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Belt of Incredible Dexterity (+2/+4/+6 enhancement bonus to Dexterity). Custom variant (normally Gloves).</summary>
    public static ItemData CreateBeltOfDexterity(int bonus)
    {
        int price = bonus * bonus * 1000;
        string id = $"belt_of_incredible_dexterity_{bonus}";
        var item = CreateBaseWondrous(id, $"Belt of Incredible Dexterity +{bonus}",
            $"This fine-quality belt grants the wearer a +{bonus} enhancement bonus to Dexterity.",
            EquipSlot.Waist, price, 8, 1f, WaistIcon, WaistColor);
        item.WondrousItemType = "ability";
        item.WondrousAbilityBonus = bonus;
        item.WondrousAbilityType = "Dex";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  WRISTS/ARMS SLOT — Bracers (DMG p.250)
    // ════════════════════════════════════════════════════════════

    /// <summary>Bracers of Armor (+1 to +8 armor bonus to AC, no ACP/ASF). DMG p.250.</summary>
    public static ItemData CreateBracersOfArmor(int bonus)
    {
        int price = bonus * bonus * 1000;
        int cl = Mathf.Max(7, bonus * 2);
        string id = $"bracers_of_armor_{bonus}";
        var item = CreateBaseWondrous(id, $"Bracers of Armor +{bonus}",
            $"These gold bracers grant an armor bonus of +{bonus} to AC with no armor check penalty, maximum Dex restriction, or arcane spell failure chance.",
            EquipSlot.Wrists, price, cl, 1f, WristsIcon, WristsColor);
        item.WondrousItemType = "ac";
        item.WondrousACBonus = bonus;
        item.WondrousACBonusType = "armor";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Bracers of Archery (Lesser: +1 attack; Greater: +2 attack, +1 damage with bows). DMG p.250.</summary>
    public static ItemData CreateBracersOfArchery(bool greater)
    {
        string id = greater ? WondrousItemNames.BRACERS_OF_ARCHERY_GREATER : WondrousItemNames.BRACERS_OF_ARCHERY_LESSER;
        string tier = greater ? "Greater" : "Lesser";
        int attackBonus = greater ? 2 : 1;
        int damageBonus = greater ? 1 : 0;
        int price = greater ? 25000 : 5000;
        string desc = greater
            ? "These wristbands grant a +2 competence bonus on attack rolls and +1 competence bonus on damage rolls made with bows. The wearer must have proficiency with the longbow or shortbow."
            : "These wristbands grant a +1 competence bonus on attack rolls made with bows.";
        var item = CreateBaseWondrous(id, $"Bracers of Archery, {tier}", desc,
            EquipSlot.Wrists, price, 4, 1f, WristsIcon, WristsColor);
        item.WondrousItemType = "attack";
        item.WondrousBowAttackBonus = attackBonus;
        item.WondrousBowDamageBonus = damageBonus;
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  HANDS SLOT — Gloves, gauntlets (DMG p.257)
    // ════════════════════════════════════════════════════════════

    /// <summary>Gauntlets of Ogre Power (+2 enhancement bonus to Strength). DMG p.257.</summary>
    public static ItemData CreateGauntletsOfOgrePower()
    {
        var item = CreateBaseWondrous(WondrousItemNames.GAUNTLETS_OF_OGRE_POWER,
            "Gauntlets of Ogre Power",
            "These gauntlets are made of tough leather with iron studs running across the back of the hands and fingers. They grant the wearer a +2 enhancement bonus to Strength.",
            EquipSlot.Hands, 4000, 6, 4f, HandsIcon, HandsColor);
        item.WondrousItemType = "ability";
        item.WondrousAbilityBonus = 2;
        item.WondrousAbilityType = "Str";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Gloves of Dexterity (+2/+4/+6 enhancement bonus to Dexterity). DMG p.257.</summary>
    public static ItemData CreateGlovesOfDexterity(int bonus)
    {
        int price = bonus * bonus * 1000;
        string id = $"gloves_of_dexterity_{bonus}";
        var item = CreateBaseWondrous(id, $"Gloves of Dexterity +{bonus}",
            $"These thin leather gloves enhance the wearer's coordination and agility, granting a +{bonus} enhancement bonus to Dexterity.",
            EquipSlot.Hands, price, 8, 0f, HandsIcon, HandsColor);
        item.WondrousItemType = "ability";
        item.WondrousAbilityBonus = bonus;
        item.WondrousAbilityType = "Dex";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Gloves of Swimming and Climbing (+5 competence to Swim and Climb). DMG p.257.</summary>
    public static ItemData CreateGlovesOfSwimmingAndClimbing()
    {
        var item = CreateBaseWondrous(WondrousItemNames.GLOVES_OF_SWIMMING_AND_CLIMBING,
            "Gloves of Swimming and Climbing",
            "These thin leather gloves are lightly padded. They grant a +5 competence bonus on Swim and Climb checks.",
            EquipSlot.Hands, 6250, 5, 0f, HandsIcon, HandsColor);
        item.WondrousItemType = "skill";
        item.WondrousSkillBonus = 5;
        item.WondrousSkillName = "Swim";
        item.WondrousSkillBonus2 = 5;
        item.WondrousSkillName2 = "Climb";
        item.WondrousSkillBonusType = "competence";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  FEET SLOT — Boots, slippers (DMG p.249)
    // ════════════════════════════════════════════════════════════

    /// <summary>Boots of Speed (Haste for 10 rounds/day, activated as free action). DMG p.250.</summary>
    public static ItemData CreateBootsOfSpeed()
    {
        var item = CreateBaseWondrous(WondrousItemNames.BOOTS_OF_SPEED,
            "Boots of Speed",
            "As a free action, the wearer can click her boot heels together to gain the effect of haste for up to 10 rounds each day. Haste grants +1 dodge bonus to AC, +1 attack, +30 ft speed, and an extra attack at full BAB.",
            EquipSlot.Feet, 12000, 10, 1f, FeetIcon, FeetColor);
        item.WondrousItemType = "movement";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        // Haste effect: tracked via WondrousGrantsHaste fields, not UsesPerDay
        item.WondrousGrantsHaste = true;
        item.WondrousHasteMaxRounds = 10;
        item.WondrousHasteRoundsUsedToday = 0;
        item.WondrousHasteCurrentlyActive = false;
        return item;
    }

    /// <summary>Boots of Elvenkind (+5 competence bonus to Move Silently). DMG p.250.</summary>
    public static ItemData CreateBootsOfElvenkind()
    {
        var item = CreateBaseWondrous(WondrousItemNames.BOOTS_OF_ELVENKIND,
            "Boots of Elvenkind",
            "These soft boots enable the wearer to move quietly in virtually any surroundings, granting a +5 competence bonus on Move Silently checks.",
            EquipSlot.Feet, 2500, 5, 1f, FeetIcon, FeetColor);
        item.WondrousItemType = "skill";
        item.WondrousSkillBonus = 5;
        item.WondrousSkillName = "Move Silently";
        item.WondrousSkillBonusType = "competence";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Boots of Striding and Springing (+10 ft base land speed, +5 competence to Jump). DMG p.250.</summary>
    public static ItemData CreateBootsOfStridingAndSpringing()
    {
        var item = CreateBaseWondrous(WondrousItemNames.BOOTS_OF_STRIDING_AND_SPRINGING,
            "Boots of Striding and Springing",
            "These boots increase the wearer's base land speed by 10 feet and give a +5 competence bonus on Jump checks.",
            EquipSlot.Feet, 5500, 8, 1f, FeetIcon, FeetColor);
        item.WondrousItemType = "movement";
        item.WondrousSpeedBonus = 10;
        item.WondrousSkillBonus = 5;
        item.WondrousSkillName = "Jump";
        item.WondrousSkillBonusType = "competence";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Slippers of Spider Climbing (spider climb continuous). DMG p.266.</summary>
    public static ItemData CreateSlippersOfSpiderClimbing()
    {
        var item = CreateBaseWondrous(WondrousItemNames.SLIPPERS_OF_SPIDER_CLIMBING,
            "Slippers of Spider Climbing",
            "When worn, a pair of these slippers enables movement on vertical surfaces or even upside down along ceilings, as the spider climb spell.",
            EquipSlot.Feet, 4800, 4, 0.5f, FeetIcon, FeetColor);
        item.WondrousItemType = "movement";
        item.WondrousGrantsMovement = true;
        item.WondrousMovementMode = "spider_climb";
        item.WondrousMovementSpeed = 20;
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Boots of Levitation (levitate at will, 20 ft/round vertical). DMG p.250.</summary>
    public static ItemData CreateBootsOfLevitation()
    {
        var item = CreateBaseWondrous(WondrousItemNames.BOOTS_OF_LEVITATION,
            "Boots of Levitation",
            "These leather boots allow the wearer to levitate as if she had cast levitate on herself. She may move up or down 20 feet per round at will, as a move action.",
            EquipSlot.Feet, 7500, 3, 1f, FeetIcon, FeetColor);
        item.WondrousItemType = "movement";
        item.WondrousGrantsMovement = true;
        item.WondrousMovementMode = "levitate";
        item.WondrousMovementSpeed = 20;
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousUsesPerDay = 0; // Unlimited (at will)
        return item;
    }

    /// <summary>Winged Boots (fly 60 ft good, 5 min/use, 3 uses/day). DMG p.271.</summary>
    public static ItemData CreateWingedBoots()
    {
        var item = CreateBaseWondrous(WondrousItemNames.WINGED_BOOTS,
            "Winged Boots",
            "These boots appear to be ordinary footwear. On command, the boots sprout wings, allowing the wearer to fly 60 ft (good maneuverability) for up to 5 minutes, three times per day.",
            EquipSlot.Feet, 16000, 8, 1f, FeetIcon, FeetColor);
        item.WondrousItemType = "movement";
        item.WondrousGrantsMovement = true;
        item.WondrousMovementMode = "fly";
        item.WondrousMovementSpeed = 60;
        item.WondrousFlightManeuverability = "good";
        item.WondrousFlightDurationRounds = 50; // 5 minutes = 50 rounds
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousUsesPerDay = 3;
        return item;
    }

    /// <summary>Boots of the Winterlands (endure cold, +5 ice walking, +10 Survival cold). DMG p.250.</summary>
    public static ItemData CreateBootsOfTheWinterlands()
    {
        var item = CreateBaseWondrous(WondrousItemNames.BOOTS_OF_THE_WINTERLANDS,
            "Boots of the Winterlands",
            "These furred boots keep the wearer warm in the coldest weather (as endure elements, cold only). The wearer can walk on ice and snow without penalty and gains a +10 competence bonus on Survival checks in cold environments.",
            EquipSlot.Feet, 2500, 5, 1f, FeetIcon, FeetColor);
        item.WondrousItemType = "movement";
        item.WondrousGrantsColdEndurance = true;
        item.WondrousColdSurvivalBonus = 10;
        item.WondrousSkillBonus = 10;
        item.WondrousSkillName = "Survival (cold)";
        item.WondrousSkillBonusType = "competence";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Boots of Teleportation (teleport 3/day, 300 lbs). DMG p.250.</summary>
    public static ItemData CreateBootsOfTeleportation()
    {
        var item = CreateBaseWondrous(WondrousItemNames.BOOTS_OF_TELEPORTATION,
            "Boots of Teleportation",
            "Any character wearing these boots may teleport three times per day, exactly as if she had cast the teleport spell on herself. The boots can carry up to 300 lbs each use.",
            EquipSlot.Feet, 49000, 9, 3f, FeetIcon, FeetColor);
        item.WondrousItemType = "movement";
        item.WondrousGrantsMovement = true;
        item.WondrousMovementMode = "teleport";
        item.WondrousMovementSpeed = 0; // N/A for teleport
        item.WondrousTeleportWeightLimit = 300;
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousUsesPerDay = 3;
        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  SLOTLESS — Bags, pearls, stones (DMG pp. 248–271)
    // ════════════════════════════════════════════════════════════

    /// <summary>Bag of Holding (Type I–IV). DMG p.248.</summary>
    public static ItemData CreateBagOfHolding(int type)
    {
        int[] prices = { 0, 2500, 5000, 7400, 10000 };
        float[] weights = { 0, 15f, 25f, 35f, 60f };
        float[] capacityLbs = { 0, 250f, 500f, 1000f, 1500f };
        float[] capacityCuFt = { 0, 30f, 70f, 150f, 250f };
        float[] apparentWeights = { 0, 15f, 25f, 35f, 60f };

        string id = $"bag_of_holding_{type}";
        var item = CreateBaseWondrous(id, $"Bag of Holding (Type {ToRoman(type)})",
            $"This bag opens into a non-dimensional space. It can hold up to {capacityLbs[type]} lbs in {capacityCuFt[type]} cubic feet of storage, but always weighs only {apparentWeights[type]} lbs.",
            EquipSlot.Slotless, prices[type], 9, apparentWeights[type], SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "storage";
        item.WondrousWeightCapacity = capacityLbs[type];
        item.WondrousVolumeCapacity = capacityCuFt[type];
        item.WondrousIsExtradimensional = true;
        item.WondrousApparentWeight = apparentWeights[type];
        item.WondrousActivationType = WondrousItemActivation.PASSIVE;
        return item;
    }

    /// <summary>Handy Haversack (extradimensional backpack, 120 lbs capacity). DMG p.259.</summary>
    public static ItemData CreateHandyHaversack()
    {
        var item = CreateBaseWondrous(WondrousItemNames.HANDY_HAVERSACK,
            "Handy Haversack",
            "This backpack has two side pouches (each 20 lbs) and a central pouch (80 lbs), but always weighs only 5 lbs. Reaching in always produces the desired item.",
            EquipSlot.Slotless, 2000, 9, 5f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "storage";
        item.WondrousWeightCapacity = 120f;
        item.WondrousVolumeCapacity = 12f;
        item.WondrousIsExtradimensional = true;
        item.WondrousApparentWeight = 5f;
        item.WondrousQuickRetrievalEnabled = true; // Move action to retrieve desired item
        item.WondrousActivationType = WondrousItemActivation.PASSIVE;
        return item;
    }

    /// <summary>Efficient Quiver (Quiver of Ehlonna). DMG p.255.
    /// Three extradimensional compartments: 60 arrows/bolts, 18 javelins/similar, 6 bows/staffs/similar.
    /// Always weighs 2 lbs regardless of contents. Quick retrieval as a move action.</summary>
    public static ItemData CreateEfficientQuiver()
    {
        var item = CreateBaseWondrous(WondrousItemNames.EFFICIENT_QUIVER,
            "Efficient Quiver",
            "This quiver has three extradimensional compartments. The first holds up to 60 arrows, bolts, or similar objects. The second holds up to 18 javelins, or similar objects. The third holds up to 6 bows, staffs, or similar long objects. The quiver always weighs only 2 lbs.",
            EquipSlot.Slotless, 1800, 9, 2f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "storage";
        item.WondrousWeightCapacity = 300f; // Combined effective capacity across all 3 compartments
        item.WondrousVolumeCapacity = 8f;   // Extradimensional
        item.WondrousIsExtradimensional = true;
        item.WondrousApparentWeight = 2f;
        item.WondrousQuickRetrievalEnabled = true; // Move action to retrieve instead of standard
        item.WondrousActivationType = WondrousItemActivation.PASSIVE;
        return item;
    }

    /// <summary>Rope of Climbing (60 ft silk rope, animate on command). DMG p.265.
    /// +5 competence bonus to Use Rope. Can animate to tie/untie itself, climb on command.
    /// AC 22, 12 HP, break DC 23. Weight: 3 lbs.</summary>
    public static ItemData CreateRopeOfClimbing()
    {
        var item = CreateBaseWondrous(WondrousItemNames.ROPE_OF_CLIMBING,
            "Rope of Climbing",
            "This 60-foot-long silk rope can animate on command, fastening itself and unfastening on command. It can also snake up to 60 feet to a point designated by the user. It grants +5 to Use Rope checks. AC 22, 12 HP, break DC 23.",
            EquipSlot.Slotless, 3000, 3, 3f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "utility";
        item.WondrousSkillBonus = 5;
        item.WondrousSkillName = "Use Rope";
        item.WondrousSkillBonusType = "competence";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        return item;
    }

    /// <summary>Portable Hole (6-ft cloth that unfolds into 10×10×10 ft extradimensional pit). DMG p.264.
    /// Holds up to 10,000 lbs. Breathable air for 10 minutes. Weight: negligible (cloth).</summary>
    public static ItemData CreatePortableHole()
    {
        var item = CreateBaseWondrous(WondrousItemNames.PORTABLE_HOLE,
            "Portable Hole",
            "This 6-foot-diameter circle of cloth unfolds into a 10-foot-deep extradimensional pit. It can hold up to 10,000 lbs. Living creatures inside have air for 10 minutes. Folding the cloth closes the pit. WARNING: Placing inside a Bag of Holding or vice versa opens a rift to the Astral Plane!",
            EquipSlot.Slotless, 20000, 12, 0f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "storage";
        item.WondrousWeightCapacity = 10000f;
        item.WondrousVolumeCapacity = 1000f; // 10 × 10 × 10 ft
        item.WondrousIsExtradimensional = true;
        item.WondrousApparentWeight = 0f; // Negligible weight when folded
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  PHASE 7: COMBAT ITEMS — Necklaces, beads, bands
    // ════════════════════════════════════════════════════════════

    /// <summary>Necklace of Fireballs (Type I–VII). DMG p.263.
    /// Consumable beads that detonate as Fireball, 20 ft radius, Reflex for half.</summary>
    public static ItemData CreateNecklaceOfFireballs(int type)
    {
        // DMG-accurate bead configurations per type
        int[][] beadConfigs = {
            null,                           // index 0 unused
            new[] { 5, 5, 5 },              // Type I:   3 beads of 5d6 (1,650 gp)
            new[] { 5, 5, 7, 7 },           // Type II:  2×5d6 + 2×7d6 (2,700 gp)
            new[] { 5, 7, 7, 9 },           // Type III: 1×5d6 + 2×7d6 + 1×9d6 (4,350 gp)
            new[] { 7, 9, 9 },              // Type IV:  1×7d6 + 2×9d6 (5,400 gp)
            new[] { 9, 11, 11 },            // Type V:   1×9d6 + 2×11d6 (5,850 gp)
            new[] { 11, 11, 11 },           // Type VI:  3×11d6 (8,100 gp)
            new[] { 9, 11, 13 }             // Type VII: 1×9d6 + 1×11d6 + 1×13d6 (8,700 gp)
        };
        int[] prices = { 0, 1650, 2700, 4350, 5400, 5850, 8100, 8700 };
        int[] saveDCs = { 0, 14, 15, 15, 15, 16, 16, 17 };

        string id = $"necklace_of_fireballs_{type}";
        int[] beads = beadConfigs[type];
        string beadDesc = string.Join(", ", System.Array.ConvertAll(beads, d => $"{d}d6"));

        var item = CreateBaseWondrous(id, $"Necklace of Fireballs (Type {ToRoman(type)})",
            $"This necklace has {beads.Length} spheres that detach and hurl as Fireballs. Beads: {beadDesc} fire damage. 20 ft radius burst, Reflex DC {saveDCs[type]} for half.",
            EquipSlot.Neck, prices[type], 10, 1f, NeckIcon, NeckColor);
        item.WondrousItemType = "combat";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        item.WondrousUsesPerDay = -1; // Consumable (uses tracked via beads)
        item.WondrousBeadDamageDice = new System.Collections.Generic.List<int>(beads);
        item.WondrousBeadSaveDC = saveDCs[type];
        item.WondrousBeadDamageType = "fire";
        item.WondrousBeadRadius = 20;
        return item;
    }

    /// <summary>Beads of Force (5 beads, 5d6 force + entrapment sphere). DMG p.248.</summary>
    public static ItemData CreateBeadsOfForce()
    {
        var item = CreateBaseWondrous(WondrousItemNames.BEADS_OF_FORCE,
            "Beads of Force",
            "A set of 5 iron spheres. Throw as ranged attack (10 ft range increment). On impact: 5d6 force damage, 10 ft radius. Target must save Fort DC 16 or be trapped in an immobile sphere for 10 minutes (only Disintegrate can destroy it).",
            EquipSlot.Slotless, 3000, 10, 0f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "combat";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        item.WondrousBeadDamageDice = new System.Collections.Generic.List<int> { 5, 5, 5, 5, 5 };
        item.WondrousBeadSaveDC = 16;
        item.WondrousBeadDamageType = "force";
        item.WondrousBeadRadius = 10;
        item.WondrousCreatesEntrapment = true;
        item.WondrousEntrapmentSaveDC = 16;
        item.WondrousEntrapmentSaveType = "fort";
        item.WondrousEntrapmentDurationRounds = 100; // 10 minutes
        return item;
    }

    /// <summary>Iron Bands of Binding (entangle target, DC 30 to break). DMG p.260.</summary>
    public static ItemData CreateIronBandsOfBinding()
    {
        var item = CreateBaseWondrous(WondrousItemNames.IRON_BANDS_OF_BINDING,
            "Iron Bands of Binding",
            "Three rusty iron bands. Throw at target as ranged touch attack (10 ft range increment). On hit, bands expand and entangle target (Reflex DC 20 negates). Trapped creature must make DC 30 Strength check or DC 30 Escape Artist to break free. Single use.",
            EquipSlot.Slotless, 26000, 13, 1f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "combat";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        item.WondrousUsesPerDay = -1; // Single use
        item.WondrousCreatesEntrapment = true;
        item.WondrousEntrapmentSaveDC = 20;
        item.WondrousEntrapmentSaveType = "reflex";
        item.WondrousEntrapmentBreakDC = 30;
        item.WondrousEntrapmentDurationRounds = 0; // Until broken
        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  PHASE 8: SUMMONING ITEMS — Bags of Tricks, Gems, Figurines
    // ════════════════════════════════════════════════════════════

    /// <summary>Bag of Tricks (Gray/Rust/Tan). DMG p.248.
    /// Pull fuzzy ball as standard action, summon random animal for 10 min. 3/week.</summary>
    public static ItemData CreateBagOfTricks(string color)
    {
        string id, name;
        int price, cl;
        System.Collections.Generic.List<string> creatures;
        string creatureDesc;

        switch (color.ToLower())
        {
            case "gray":
                id = WondrousItemNames.BAG_OF_TRICKS_GRAY;
                name = "Bag of Tricks (Gray)";
                price = 900; cl = 3;
                creatures = new System.Collections.Generic.List<string> { "bat", "weasel", "badger", "wolverine", "wolf" };
                creatureDesc = "bat, weasel, badger, wolverine, or wolf";
                break;
            case "rust":
                id = WondrousItemNames.BAG_OF_TRICKS_RUST;
                name = "Bag of Tricks (Rust)";
                price = 3000; cl = 5;
                creatures = new System.Collections.Generic.List<string> { "rat", "owl", "dog", "cheetah", "boar" };
                creatureDesc = "rat, owl, dog, cheetah, or boar";
                break;
            case "tan":
                id = WondrousItemNames.BAG_OF_TRICKS_TAN;
                name = "Bag of Tricks (Tan)";
                price = 16000; cl = 9;
                creatures = new System.Collections.Generic.List<string> { "dire_weasel", "dire_wolverine", "ape", "black_bear", "brown_bear" };
                creatureDesc = "dire weasel, dire wolverine, ape, black bear, or brown bear";
                break;
            default:
                id = "bag_of_tricks_unknown";
                name = "Bag of Tricks";
                price = 900; cl = 3;
                creatures = new System.Collections.Generic.List<string>();
                creatureDesc = "unknown creature";
                break;
        }

        var item = CreateBaseWondrous(id, name,
            $"Pull a fuzzy ball from this bag as a standard action. It transforms into a random animal ({creatureDesc}) that serves the owner for 10 minutes. 3 uses per week.",
            EquipSlot.Slotless, price, cl, 1f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "summoning";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        item.WondrousUsesPerWeek = 3;
        item.WondrousCanSummon = true;
        item.WondrousSummonCreatureIds = creatures;
        item.WondrousSummonDurationRounds = 100; // 10 minutes
        item.WondrousSummonDescription = creatureDesc;
        return item;
    }

    /// <summary>Elemental Gem (summon Large Elemental for 1 hour, single use). DMG p.255.</summary>
    public static ItemData CreateElementalGem(string element)
    {
        string id, name, desc;
        string creatureId;
        switch (element.ToLower())
        {
            case "air":
                id = WondrousItemNames.ELEMENTAL_GEM_AIR;
                name = "Elemental Gem (Air)";
                creatureId = "large_air_elemental";
                desc = "This blue sapphire, when crushed, summons a Large Air Elemental (11 HD) that serves for 1 hour. Single use.";
                break;
            case "earth":
                id = WondrousItemNames.ELEMENTAL_GEM_EARTH;
                name = "Elemental Gem (Earth)";
                creatureId = "large_earth_elemental";
                desc = "This brown topaz, when crushed, summons a Large Earth Elemental (11 HD) that serves for 1 hour. Single use.";
                break;
            case "fire":
                id = WondrousItemNames.ELEMENTAL_GEM_FIRE;
                name = "Elemental Gem (Fire)";
                creatureId = "large_fire_elemental";
                desc = "This red garnet, when crushed, summons a Large Fire Elemental (11 HD) that serves for 1 hour. Single use.";
                break;
            case "water":
                id = WondrousItemNames.ELEMENTAL_GEM_WATER;
                name = "Elemental Gem (Water)";
                creatureId = "large_water_elemental";
                desc = "This blue-green aquamarine, when crushed, summons a Large Water Elemental (11 HD) that serves for 1 hour. Single use.";
                break;
            default:
                id = $"elemental_gem_{element}";
                name = $"Elemental Gem ({element})";
                creatureId = $"large_{element}_elemental";
                desc = $"Summons a Large {element} Elemental.";
                break;
        }

        var item = CreateBaseWondrous(id, name, desc,
            EquipSlot.Slotless, 2250, 11, 0f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "summoning";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        item.WondrousUsesPerDay = -1; // Single use (consumable)
        item.WondrousCanSummon = true;
        item.WondrousSummonCreatureIds = new System.Collections.Generic.List<string> { creatureId };
        item.WondrousSummonDurationRounds = 600; // 1 hour
        item.WondrousSummonDescription = $"Large {char.ToUpper(element[0]) + element.Substring(1)} Elemental (11 HD)";
        return item;
    }

    /// <summary>Figurine of Wondrous Power (8 types). DMG p.256.</summary>
    public static ItemData CreateFigurineOfWondrousPower(string figurineType)
    {
        string id, name, desc, creatureId, summonDesc;
        int price, cl, durationRounds, usesPerWeek = 0, usesPerMonth = 0, usesPerDay = 0;
        bool mountable = false;

        switch (figurineType.ToLower())
        {
            case "silver_raven":
                id = WondrousItemNames.FIGURINE_OF_WONDROUS_POWER_SILVER_RAVEN;
                name = "Figurine of Wondrous Power (Silver Raven)";
                price = 3800; cl = 6;
                creatureId = "raven";
                desc = "This tiny silver raven figurine transforms into a raven for up to 12 hours. Can act as a messenger. Usable once per week.";
                summonDesc = "Raven (12 hr, messenger)";
                durationRounds = 7200; // 12 hours
                usesPerWeek = 1;
                break;
            case "serpentine_owl":
                id = WondrousItemNames.FIGURINE_OF_WONDROUS_POWER_SERPENTINE_OWL;
                name = "Figurine of Wondrous Power (Serpentine Owl)";
                price = 9100; cl = 11;
                creatureId = "giant_owl";
                desc = "This serpentine stone owl figurine transforms into a giant owl for up to 8 hours. Can be ridden. Usable 3 times per week.";
                summonDesc = "Giant Owl (8 hr)";
                durationRounds = 4800; // 8 hours
                usesPerWeek = 3;
                mountable = true;
                break;
            case "bronze_griffon":
                id = WondrousItemNames.FIGURINE_OF_WONDROUS_POWER_BRONZE_GRIFFON;
                name = "Figurine of Wondrous Power (Bronze Griffon)";
                price = 10000; cl = 11;
                creatureId = "griffon";
                desc = "This bronze griffon figurine transforms into a griffon for up to 6 hours. Can be ridden and will fight. Usable twice per week.";
                summonDesc = "Griffon (6 hr)";
                durationRounds = 3600; // 6 hours
                usesPerWeek = 2;
                mountable = true;
                break;
            case "ebony_fly":
                id = WondrousItemNames.FIGURINE_OF_WONDROUS_POWER_EBONY_FLY;
                name = "Figurine of Wondrous Power (Ebony Fly)";
                price = 10000; cl = 11;
                creatureId = "giant_fly";
                desc = "This ebony fly figurine transforms into a giant fly mount for up to 12 hours. Can carry 500 lbs. Usable 3 times per week.";
                summonDesc = "Giant Fly (12 hr, 500 lbs)";
                durationRounds = 7200; // 12 hours
                usesPerWeek = 3;
                mountable = true;
                break;
            case "onyx_dog":
                id = WondrousItemNames.FIGURINE_OF_WONDROUS_POWER_ONYX_DOG;
                name = "Figurine of Wondrous Power (Onyx Dog)";
                price = 15500; cl = 11;
                creatureId = "riding_dog";
                desc = "This onyx dog figurine transforms into a riding dog for up to 6 hours. Has exceptional tracking ability (+8 Survival). Usable once per week.";
                summonDesc = "Riding Dog (6 hr, tracker)";
                durationRounds = 3600; // 6 hours
                usesPerWeek = 1;
                break;
            case "golden_lions":
                id = WondrousItemNames.FIGURINE_OF_WONDROUS_POWER_GOLDEN_LIONS;
                name = "Figurine of Wondrous Power (Golden Lions)";
                price = 16500; cl = 11;
                creatureId = "lion";
                desc = "This pair of gold lion figurines transforms into 2 lions for up to 1 hour. They fight on your behalf. Usable once per day.";
                summonDesc = "2 Lions (1 hr)";
                durationRounds = 600; // 1 hour
                usesPerDay = 1;
                break;
            case "marble_elephant":
                id = WondrousItemNames.FIGURINE_OF_WONDROUS_POWER_MARBLE_ELEPHANT;
                name = "Figurine of Wondrous Power (Marble Elephant)";
                price = 17000; cl = 11;
                creatureId = "elephant";
                desc = "This marble elephant figurine transforms into an elephant for up to 24 hours. Can fight or carry 2,000 lbs. Usable 4 times per month.";
                summonDesc = "Elephant (24 hr, 2,000 lbs)";
                durationRounds = 14400; // 24 hours
                usesPerMonth = 4;
                mountable = true;
                break;
            case "obsidian_steed":
                id = WondrousItemNames.FIGURINE_OF_WONDROUS_POWER_OBSIDIAN_STEED;
                name = "Figurine of Wondrous Power (Obsidian Steed)";
                price = 28500; cl = 15;
                creatureId = "nightmare";
                desc = "This obsidian horse figurine transforms into a nightmare for up to 24 hours. Can fly and plane shift. Usable once per week.";
                summonDesc = "Nightmare (24 hr, fly, plane shift)";
                durationRounds = 14400; // 24 hours
                usesPerWeek = 1;
                mountable = true;
                break;
            default:
                id = $"figurine_{figurineType}";
                name = $"Figurine of Wondrous Power ({figurineType})";
                price = 10000; cl = 11;
                creatureId = figurineType;
                desc = "A magical figurine.";
                summonDesc = figurineType;
                durationRounds = 600;
                usesPerWeek = 1;
                break;
        }

        var item = CreateBaseWondrous(id, name, desc,
            EquipSlot.Slotless, price, cl, 1f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "summoning";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousCanSummon = true;
        item.WondrousSummonCreatureIds = new System.Collections.Generic.List<string> { creatureId };
        item.WondrousSummonDurationRounds = durationRounds;
        item.WondrousSummonIsMountable = mountable;
        item.WondrousSummonDescription = summonDesc;
        if (usesPerWeek > 0) item.WondrousUsesPerWeek = usesPerWeek;
        if (usesPerMonth > 0) item.WondrousUsesPerMonth = usesPerMonth;
        if (usesPerDay > 0) item.WondrousUsesPerDay = usesPerDay;
        return item;
    }

    /// <summary>Pearl of Power (1st–9th: recover one spell slot of given level). DMG p.263.</summary>
    public static ItemData CreatePearlOfPower(int spellLevel)
    {
        // Price: 1000 × level² (level 1 = 1000, level 2 = 4000, etc.)
        int price = 1000 * spellLevel * spellLevel;
        string id = $"pearl_of_power_{spellLevel}";
        string ordinal = GetOrdinal(spellLevel);
        var item = CreateBaseWondrous(id, $"Pearl of Power ({ordinal} level)",
            $"Once per day on command, this lustrous white pearl allows the possessor to recall one {ordinal}-level spell that she had prepared and then cast.",
            EquipSlot.Slotless, price, 17, 0f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "utility";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousUsesPerDay = 1;
        return item;
    }

    /// <summary>Stone of Good Luck (Luckstone: +1 luck bonus to saves, ability checks, skill checks). DMG p.267.</summary>
    public static ItemData CreateStoneOfGoodLuck()
    {
        var item = CreateBaseWondrous(WondrousItemNames.STONE_OF_GOOD_LUCK,
            "Stone of Good Luck (Luckstone)",
            "This polished agate grants the possessor a +1 luck bonus on saving throws, ability checks, and skill checks.",
            EquipSlot.Slotless, 20000, 5, 0f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "utility";
        item.WondrousSaveBonus = 1;
        item.WondrousSaveType = "all";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  TEST ITEMS — One per slot for verification
    // ════════════════════════════════════════════════════════════

    /// <summary>Create a test item for a given slot to verify equip/unequip mechanics.</summary>
    public static ItemData CreateTestItem(EquipSlot slot, string slotName)
    {
        string id = $"test_wondrous_{slotName.ToLower().Replace("/", "_").Replace(" ", "_")}";
        string icon;
        Color color;
        GetSlotIconAndColor(slot, out icon, out color);

        var item = CreateBaseWondrous(id, $"Test {slotName} Item",
            $"A test wondrous item for the {slotName} slot. Used to verify equip/unequip mechanics.",
            slot, 100, 1, 0.5f, icon, color);
        item.WondrousItemType = "test";
        item.WondrousActivationType = WondrousItemActivation.PASSIVE;
        return item;
    }

    /// <summary>Create a test slotless item to verify multiple-equip mechanics.</summary>
    public static ItemData CreateTestSlotlessItem()
    {
        var item = CreateBaseWondrous(WondrousItemNames.TEST_SLOTLESS_ITEM,
            "Test Slotless Wondrous Item",
            "A test slotless wondrous item. Can be equipped alongside other slotless items.",
            EquipSlot.Slotless, 100, 1, 0.5f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "test";
        item.WondrousActivationType = WondrousItemActivation.PASSIVE;
        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  Utility Helpers
    // ════════════════════════════════════════════════════════════

    private static void GetSlotIconAndColor(EquipSlot slot, out string icon, out Color color)
    {
        switch (slot)
        {
            case EquipSlot.Head: icon = HeadIcon; color = HeadColor; break;
            case EquipSlot.FaceEyes: icon = FaceIcon; color = FaceColor; break;
            case EquipSlot.Neck: icon = NeckIcon; color = NeckColor; break;
            case EquipSlot.Back: icon = BackIcon; color = BackColor; break;
            case EquipSlot.Torso: icon = TorsoIcon; color = TorsoColor; break;
            case EquipSlot.Waist: icon = WaistIcon; color = WaistColor; break;
            case EquipSlot.Wrists: icon = WristsIcon; color = WristsColor; break;
            case EquipSlot.Hands: icon = HandsIcon; color = HandsColor; break;
            case EquipSlot.Feet: icon = FeetIcon; color = FeetColor; break;
            default: icon = SlotlessIcon; color = SlotlessColor; break;
        }
    }

    private static string ToRoman(int num)
    {
        switch (num)
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            case 4: return "IV";
            case 5: return "V";
            case 6: return "VI";
            case 7: return "VII";
            case 8: return "VIII";
            case 9: return "IX";
            case 10: return "X";
            default: return num.ToString();
        }
    }

    private static string GetOrdinal(int num)
    {
        switch (num)
        {
            case 1: return "1st";
            case 2: return "2nd";
            case 3: return "3rd";
            default: return $"{num}th";
        }
    }

    // ════════════════════════════════════════════════════════════
    //  PHASE 9: IOUN STONES (DMG pp. 260–261) — All Slotless
    // ════════════════════════════════════════════════════════════

    private static ItemData CreateBaseIounStone(string id, string name, string desc, int price, int cl)
    {
        var item = CreateBaseWondrous(id, name, desc, EquipSlot.Slotless, price, cl, 0f, SlotlessIcon, SlotlessColor);
        item.IsIounStone = true;
        item.WondrousItemType = "ioun_stone";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    /// <summary>Ioun Stone: Ability Score enhancement (+2). DMG p.260.</summary>
    public static ItemData CreateIounStoneAbility(string abilityShort, string abilityFull, string stoneName, string stoneId)
    {
        var item = CreateBaseIounStone(stoneId, stoneName,
            $"This stone grants a +2 enhancement bonus to {abilityFull}.",
            8000, 12);
        item.WondrousAbilityBonus = 2;
        item.WondrousAbilityType = abilityShort;
        return item;
    }

    /// <summary>Deep Red Sphere (+2 Dex). DMG p.260.</summary>
    public static ItemData CreateIounStoneDeepRedSphere()
    {
        return CreateIounStoneAbility("Dex", "Dexterity", "Ioun Stone (Deep Red Sphere)", WondrousItemNames.IOUN_STONE_DEEP_RED_SPHERE);
    }

    /// <summary>Incandescent Blue Sphere (+2 Wis). DMG p.260.</summary>
    public static ItemData CreateIounStoneIncandescentBlueSphere()
    {
        return CreateIounStoneAbility("Wis", "Wisdom", "Ioun Stone (Incandescent Blue Sphere)", WondrousItemNames.IOUN_STONE_INCANDESCENT_BLUE_SPHERE);
    }

    /// <summary>Pale Blue Rhomboid (+2 Str). DMG p.260.</summary>
    public static ItemData CreateIounStonePaleBlueRhomboid()
    {
        return CreateIounStoneAbility("Str", "Strength", "Ioun Stone (Pale Blue Rhomboid)", WondrousItemNames.IOUN_STONE_PALE_BLUE_RHOMBOID);
    }

    /// <summary>Pink Rhomboid (+2 Con). DMG p.260.</summary>
    public static ItemData CreateIounStonePinkRhomboid()
    {
        return CreateIounStoneAbility("Con", "Constitution", "Ioun Stone (Pink Rhomboid)", WondrousItemNames.IOUN_STONE_PINK_RHOMBOID);
    }

    /// <summary>Pink and Green Sphere (+2 Cha). DMG p.260.</summary>
    public static ItemData CreateIounStonePinkAndGreenSphere()
    {
        return CreateIounStoneAbility("Cha", "Charisma", "Ioun Stone (Pink and Green Sphere)", WondrousItemNames.IOUN_STONE_PINK_AND_GREEN_SPHERE);
    }

    /// <summary>Scarlet and Blue Sphere (+2 Int). DMG p.260.</summary>
    public static ItemData CreateIounStoneScarletAndBlueSphere()
    {
        return CreateIounStoneAbility("Int", "Intelligence", "Ioun Stone (Scarlet and Blue Sphere)", WondrousItemNames.IOUN_STONE_SCARLET_AND_BLUE_SPHERE);
    }

    /// <summary>Clear Spindle (Sustains without food/water). DMG p.260.</summary>
    public static ItemData CreateIounStoneClearSpindle()
    {
        var item = CreateBaseIounStone(WondrousItemNames.IOUN_STONE_CLEAR_SPINDLE,
            "Ioun Stone (Clear Spindle)",
            "This stone sustains the user without food or water.",
            4000, 12);
        item.WondrousSustainsWithoutFood = true;
        return item;
    }

    /// <summary>Dusty Rose Prism (+1 insight AC). DMG p.260.</summary>
    public static ItemData CreateIounStoneDustyRosePrism()
    {
        var item = CreateBaseIounStone(WondrousItemNames.IOUN_STONE_DUSTY_ROSE_PRISM,
            "Ioun Stone (Dusty Rose Prism)",
            "This stone grants a +1 insight bonus to AC.",
            5000, 12);
        item.WondrousInsightACBonus = 1;
        return item;
    }

    /// <summary>Dark Blue Rhomboid (grants Alertness feat). DMG p.260.</summary>
    public static ItemData CreateIounStoneDarkBlueRhomboid()
    {
        var item = CreateBaseIounStone(WondrousItemNames.IOUN_STONE_DARK_BLUE_RHOMBOID,
            "Ioun Stone (Dark Blue Rhomboid)",
            "This stone grants the Alertness feat (+2 to Listen and Spot).",
            10000, 12);
        item.WondrousGrantsFeatName = "Alertness";
        return item;
    }

    /// <summary>Vibrant Purple Prism (stores 3 spell levels). DMG p.260.</summary>
    public static ItemData CreateIounStoneVibrantPurplePrism()
    {
        var item = CreateBaseIounStone(WondrousItemNames.IOUN_STONE_VIBRANT_PURPLE_PRISM,
            "Ioun Stone (Vibrant Purple Prism)",
            "This stone can store up to 3 spell levels worth of spells (as a ring of spell storing, but with a maximum of 3 levels).",
            36000, 12);
        item.WondrousSpellStorageLevels = 3;
        return item;
    }

    /// <summary>Iridescent Spindle (sustains without air). DMG p.260.</summary>
    public static ItemData CreateIounStoneIridescentSpindle()
    {
        var item = CreateBaseIounStone(WondrousItemNames.IOUN_STONE_IRIDESCENT_SPINDLE,
            "Ioun Stone (Iridescent Spindle)",
            "This stone sustains the user without air.",
            18000, 12);
        item.WondrousSustainsWithoutAir = true;
        return item;
    }

    /// <summary>Pale Lavender Ellipsoid (absorb spells ≤4th level, 20 charges). DMG p.260.</summary>
    public static ItemData CreateIounStonePaleLavenderEllipsoid()
    {
        var item = CreateBaseIounStone(WondrousItemNames.IOUN_STONE_PALE_LAVENDER_ELLIPSOID,
            "Ioun Stone (Pale Lavender Ellipsoid)",
            "This stone absorbs spells of 4th level or lower. After absorbing 20 spell levels, it burns out and turns to dull gray.",
            20000, 12);
        item.WondrousSpellAbsorptionMaxLevel = 4;
        item.WondrousSpellAbsorptionCharges = 20;
        item.WondrousSpellAbsorptionMaxCharges = 20;
        return item;
    }

    /// <summary>Pearly White Spindle (regenerate 1 HP/hour). DMG p.260.</summary>
    public static ItemData CreateIounStonePearlyWhiteSpindle()
    {
        var item = CreateBaseIounStone(WondrousItemNames.IOUN_STONE_PEARLY_WHITE_SPINDLE,
            "Ioun Stone (Pearly White Spindle)",
            "This stone regenerates 1 hit point per hour of damage the wearer has taken.",
            20000, 12);
        item.WondrousRegenPerHour = 1;
        return item;
    }

    /// <summary>Orange Prism (+1 caster level). DMG p.260.</summary>
    public static ItemData CreateIounStoneOrangePrism()
    {
        var item = CreateBaseIounStone(WondrousItemNames.IOUN_STONE_ORANGE_PRISM,
            "Ioun Stone (Orange Prism)",
            "This stone grants a +1 caster level bonus to all spells.",
            30000, 12);
        item.WondrousCasterLevelBonus = 1;
        return item;
    }

    /// <summary>Pale Green Prism (+1 competence bonus to all saves). DMG p.260.</summary>
    public static ItemData CreateIounStonePaleGreenPrism()
    {
        var item = CreateBaseIounStone(WondrousItemNames.IOUN_STONE_PALE_GREEN_PRISM,
            "Ioun Stone (Pale Green Prism)",
            "This stone grants a +1 competence bonus to attack rolls, saves, skill checks, and ability checks.",
            30000, 12);
        item.WondrousCompetenceSaveBonus = 1;
        return item;
    }

    /// <summary>Lavender and Green Ellipsoid (absorb spells ≤8th level, 50 charges). DMG p.260.</summary>
    public static ItemData CreateIounStoneLavenderAndGreenEllipsoid()
    {
        var item = CreateBaseIounStone(WondrousItemNames.IOUN_STONE_LAVENDER_AND_GREEN_ELLIPSOID,
            "Ioun Stone (Lavender and Green Ellipsoid)",
            "This stone absorbs spells of 8th level or lower. After absorbing 50 spell levels, it burns out.",
            40000, 12);
        item.WondrousSpellAbsorptionMaxLevel = 8;
        item.WondrousSpellAbsorptionCharges = 50;
        item.WondrousSpellAbsorptionMaxCharges = 50;
        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  PHASE 10: COMPLEX MULTI-ABILITY ITEMS
    // ════════════════════════════════════════════════════════════

    // --- ROBES ---

    /// <summary>Robe of the Archmagi (+5 AC, SR 18, +4 saves, +2 CL). DMG p.266. Alignment variants.</summary>
    public static ItemData CreateRobeOfTheArchmagi(string alignment)
    {
        string suffix = alignment.ToLower();
        string id = $"robe_of_the_archmagi_{suffix}";
        string alignLabel = suffix == "good" ? "White" : suffix == "evil" ? "Black" : "Gray";
        var item = CreateBaseWondrous(id,
            $"Robe of the Archmagi ({alignLabel})",
            $"This {alignLabel.ToLower()} robe grants +5 armor bonus to AC, Spell Resistance 18, +4 resistance bonus to all saves, and +2 caster level for arcane spells. Worn by wrong alignment: -4 AC, -2 saves, -2 caster level.",
            EquipSlot.Torso, 75000, 14, 1f, TorsoIcon, TorsoColor);
        item.WondrousItemType = "armor";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        item.WondrousACBonus = 5;
        item.WondrousACBonusType = "armor";
        item.WondrousGrantsSR = 18;
        item.WondrousResistanceSaveBonus = 4;
        item.WondrousCasterLevelBonus = 2;
        item.WondrousRequiredAlignment = suffix;
        item.WondrousWrongAlignmentACPenalty = 4;
        item.WondrousWrongAlignmentSavePenalty = 2;
        return item;
    }

    /// <summary>Robe of Stars (+1d6 AC, 6 Magic Missile patches, Dimension Door 1/day). DMG p.266.</summary>
    public static ItemData CreateRobeOfStars()
    {
        var item = CreateBaseWondrous(WondrousItemNames.ROBE_OF_STARS,
            "Robe of Stars",
            "This deep blue robe is adorned with gold stars. Grants +1 luck bonus to ALL saves, "
            + "+5 armor bonus to AC. Detachable stars: 6 large (Fireball 5d6, DC 15), "
            + "20 small (Magic Missile 1d4+1 each), 30 tiny (Light, 1 hr, 20 ft radius). "
            + "Stars regenerate 1/month. Command word: Plane Shift to Astral Plane.",
            EquipSlot.Torso, 58000, 15, 1f, TorsoIcon, TorsoColor);
        item.WondrousItemType = "armor";
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        item.WondrousHasActivation = true;
        // Legacy patch fields (kept for compatibility)
        item.WondrousPatchesRemaining = 6;
        item.WondrousPatchesMax = 6;
        item.WondrousPatchDescription = "Fireball (5d6, DC 15) stars";
        item.WondrousUsesPerDay = -1; // Unlimited (Plane Shift is command word)
        // Enhanced Robe of Stars fields
        item.WondrousIsRobeOfStars = true;
        item.WondrousRobeStarsLuckSaveBonus = 1;
        item.WondrousRobeStarsArmorBonus = 5;
        item.WondrousRobeFireballStars = 6;
        item.WondrousRobeFireballStarsMax = 6;
        item.WondrousRobeMagicMissileStars = 20;
        item.WondrousRobeMagicMissileStarsMax = 20;
        item.WondrousRobeLightStars = 30;
        item.WondrousRobeLightStarsMax = 30;
        item.WondrousRobeStarsRegenPerMonth = 1;
        item.WondrousRobeGrantsAstralShift = true;
        return item;
    }

    /// <summary>Robe of Scintillating Colors (Hypnotic Pattern 3/day, Will DC 16). DMG p.266.</summary>
    public static ItemData CreateRobeOfScintillatingColors()
    {
        var item = CreateBaseWondrous(WondrousItemNames.ROBE_OF_SCINTILLATING_COLORS,
            "Robe of Scintillating Colors",
            "This robe produces a dazzling display of color. The wearer can use Hypnotic Pattern (Will DC 16, 10 ft radius) as a standard action, 3 times per day.",
            EquipSlot.Torso, 27000, 11, 1f, TorsoIcon, TorsoColor);
        item.WondrousItemType = "activated";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousUsesPerDay = 3;
        return item;
    }

    /// <summary>Robe of Eyes (all-around vision, See Invisible, +10 Search/Spot, can't be flanked). DMG p.265.</summary>
    public static ItemData CreateRobeOfEyes()
    {
        var item = CreateBaseWondrous(WondrousItemNames.ROBE_OF_EYES,
            "Robe of Eyes",
            "Thousands of tiny eyes cover this robe. The wearer sees in all directions, gains See Invisible, +10 to Search and Spot, cannot be surprised or flanked. Vulnerability: Light/gaze attacks.",
            EquipSlot.Torso, 120000, 11, 1f, TorsoIcon, TorsoColor);
        item.WondrousItemType = "skill";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        item.WondrousGrantsSeeInvisible = true;
        item.WondrousPreventsFlanking = true;
        item.WondrousSearchBonus = 10;
        item.WondrousSpotBonus = 10;
        return item;
    }

    /// <summary>Robe of Blending (Disguise Self at will, +10 Disguise). DMG p.265.</summary>
    public static ItemData CreateRobeOfBlending()
    {
        var item = CreateBaseWondrous(WondrousItemNames.ROBE_OF_BLENDING,
            "Robe of Blending",
            "This robe enables the wearer to assume the form and appearance of another creature. Disguise Self at will, +10 bonus to Disguise checks.",
            EquipSlot.Torso, 8400, 10, 1f, TorsoIcon, TorsoColor);
        item.WondrousItemType = "utility";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousUsesPerDay = 0; // At will
        item.WondrousDisguiseBonus = 10;
        return item;
    }

    /// <summary>Robe of Bones (undead patches). DMG p.265.</summary>
    public static ItemData CreateRobeOfBones()
    {
        var item = CreateBaseWondrous(WondrousItemNames.ROBE_OF_BONES,
            "Robe of Bones",
            "This robe has small embroidered undead figures on it. The wearer can detach a figure to create an undead creature that serves the wearer. The robe starts with 2 skeletons and 2 zombies.",
            EquipSlot.Torso, 2400, 6, 1f, TorsoIcon, TorsoColor);
        item.WondrousItemType = "summoning";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        item.WondrousPatchesRemaining = 4;
        item.WondrousPatchesMax = 4;
        item.WondrousPatchDescription = "2 skeletons, 2 zombies";
        item.WondrousCanSummon = true;
        item.WondrousSummonDescription = "Undead servant (skeleton or zombie)";
        return item;
    }

    /// <summary>Robe of Useful Items (item patches). DMG p.266.</summary>
    public static ItemData CreateRobeOfUsefulItems()
    {
        var item = CreateBaseWondrous(WondrousItemNames.ROBE_OF_USEFUL_ITEMS,
            "Robe of Useful Items",
            "This robe has cloth patches that can be detached to become actual items. Standard patches: 2 daggers, 2 lanterns, 2 mirrors, 2 poles, 2 ropes (50 ft), 2 sacks. Additional patches vary.",
            EquipSlot.Torso, 7000, 9, 1f, TorsoIcon, TorsoColor);
        item.WondrousItemType = "utility";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        item.WondrousPatchesRemaining = 12;
        item.WondrousPatchesMax = 12;
        item.WondrousPatchDescription = "daggers, lanterns, mirrors, poles, ropes, sacks";
        return item;
    }

    /// <summary>Vestment of Faith (+3 Wisdom checks divine). DMG p.267.</summary>
    public static ItemData CreateVestmentOfFaith()
    {
        var item = CreateBaseWondrous(WondrousItemNames.VESTMENT_OF_FAITH,
            "Vestment of Faith",
            "This simple vestment confers a damage reduction of 5/evil on the wearer. The wearer also gains a +3 sacred bonus to AC.",
            EquipSlot.Torso, 2500, 1, 1f, TorsoIcon, TorsoColor);
        item.WondrousItemType = "armor";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        return item;
    }

    // --- CLOAKS ---

    /// <summary>Cloak of Arachnida (Spider Climb, web/poison immunity, +2 Fort vs poison). DMG p.253.</summary>
    public static ItemData CreateCloakOfArachnida()
    {
        var item = CreateBaseWondrous(WondrousItemNames.CLOAK_OF_ARACHNIDA,
            "Cloak of Arachnida",
            "This black garment gives the wearer Spider Climb at will, immunity to entrapment by web (magical and mundane), +2 luck bonus on Fortitude saves vs poison, and immunity to spider poison.",
            EquipSlot.Back, 14000, 6, 1f, BackIcon, BackColor);
        item.WondrousItemType = "movement";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        item.WondrousGrantsSpiderClimb = true;
        item.WondrousGrantsWebImmunity = true;
        item.WondrousLuckFortSaveBonus = 2;
        item.WondrousGrantsMovement = true;
        item.WondrousMovementMode = "spider_climb";
        item.WondrousMovementSpeed = 20;
        return item;
    }

    /// <summary>Cloak of the Bat (+5 Hide, Fly or bat form 2/day). DMG p.253.</summary>
    public static ItemData CreateCloakOfTheBat()
    {
        var item = CreateBaseWondrous(WondrousItemNames.CLOAK_OF_THE_BAT,
            "Cloak of the Bat",
            "This dark cloak grants +5 competence bonus on Hide checks. On command, the wearer can fly (60 ft, average maneuverability) or polymorph into a bat, 2 times per day total.",
            EquipSlot.Back, 26000, 7, 1f, BackIcon, BackColor);
        item.WondrousItemType = "movement";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousUsesPerDay = 2;
        item.WondrousSkillBonus = 5;
        item.WondrousSkillName = "Hide";
        item.WondrousSkillBonusType = "competence";
        item.WondrousGrantsMovement = true;
        item.WondrousMovementMode = "fly";
        item.WondrousMovementSpeed = 60;
        item.WondrousFlightManeuverability = "average";
        return item;
    }

    // --- HELMS ---

    /// <summary>Helm of Telepathy (Detect Thoughts at will, Suggestion 1/day). DMG p.258.</summary>
    public static ItemData CreateHelmOfTelepathy()
    {
        var item = CreateBaseWondrous(WondrousItemNames.HELM_OF_TELEPATHY,
            "Helm of Telepathy",
            "This helm enables the wearer to use Detect Thoughts at will (Will DC 13) and Suggestion once per day (Will DC 14).",
            EquipSlot.Head, 27000, 5, 0f, HeadIcon, HeadColor);
        item.WondrousItemType = "activated";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        item.WondrousDetectThoughtsDC = 13;
        item.WondrousSuggestionDC = 14;
        item.WondrousUsesPerDay = 1; // Suggestion
        return item;
    }

    /// <summary>Helm of Teleportation (Teleport 3/day, CL 9). DMG p.258.</summary>
    public static ItemData CreateHelmOfTeleportation()
    {
        var item = CreateBaseWondrous(WondrousItemNames.HELM_OF_TELEPORTATION,
            "Helm of Teleportation",
            "This helm allows the wearer to Teleport (as the spell, CL 9th) three times per day.",
            EquipSlot.Head, 73500, 9, 0f, HeadIcon, HeadColor);
        item.WondrousItemType = "activated";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousUsesPerDay = 3;
        item.WondrousTeleportWeightLimit = 500;
        return item;
    }

    /// <summary>Helm of Underwater Action (see/move underwater freely). DMG p.258.</summary>
    public static ItemData CreateHelmOfUnderwaterAction()
    {
        var item = CreateBaseWondrous(WondrousItemNames.HELM_OF_UNDERWATER_ACTION,
            "Helm of Underwater Action",
            "This helm enables the wearer to see underwater (60 ft clear vision) and grants freedom of movement in water.",
            EquipSlot.Head, 24000, 5, 0f, HeadIcon, HeadColor);
        item.WondrousItemType = "utility";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        item.WondrousUnderwaterVisionRange = 60;
        item.WondrousWaterFreedomOfMovement = true;
        return item;
    }

    /// <summary>Helm of Brilliance (gem-powered spell-like abilities). DMG p.258.</summary>
    public static ItemData CreateHelmOfBrilliance()
    {
        var item = CreateBaseWondrous(WondrousItemNames.HELM_OF_BRILLIANCE,
            "Helm of Brilliance",
            "This helm has 10 diamonds (Prismatic Spray), 20 rubies (Wall of Fire), 30 fire opals (Fireball), 40 opals (Daylight). Gems power spell-like abilities. Also grants fire immunity, Light at will, and SR 13.",
            EquipSlot.Head, 125000, 13, 0f, HeadIcon, HeadColor);
        item.WondrousItemType = "activated";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousGrantsSR = 13;
        item.WondrousPatchesRemaining = 100;
        item.WondrousPatchesMax = 100;
        item.WondrousPatchDescription = "10 diamonds, 20 rubies, 30 fire opals, 40 opals";
        return item;
    }

    // --- PERIAPTS & SCARABS ---

    /// <summary>Periapt of Proof Against Poison (immune to all poison). DMG p.263.</summary>
    public static ItemData CreatePeriaptOfProofAgainstPoison()
    {
        var item = CreateBaseWondrous(WondrousItemNames.PERIAPT_OF_PROOF_AGAINST_POISON,
            "Periapt of Proof Against Poison",
            "This item is a brilliant-cut gem on a delicate gold chain. The wearer is immune to all poisons.",
            EquipSlot.Neck, 27000, 5, 0f, NeckIcon, NeckColor);
        item.WondrousItemType = "protection";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        item.WondrousGrantsPoisonImmunity = true;
        return item;
    }

    /// <summary>Periapt of Health (immune to disease). DMG p.263.</summary>
    public static ItemData CreatePeriaptOfHealth()
    {
        var item = CreateBaseWondrous(WondrousItemNames.PERIAPT_OF_HEALTH,
            "Periapt of Health",
            "This gem on a gold chain grants the wearer immunity to all diseases, including supernatural and magical diseases.",
            EquipSlot.Neck, 7400, 5, 0f, NeckIcon, NeckColor);
        item.WondrousItemType = "protection";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        item.WondrousGrantsDiseaseImmunity = true;
        return item;
    }

    /// <summary>Scarab of Protection (absorbs death/drain effects, 12 charges). DMG p.266.</summary>
    public static ItemData CreateScarabOfProtection()
    {
        var item = CreateBaseWondrous(WondrousItemNames.SCARAB_OF_PROTECTION,
            "Scarab of Protection",
            "This device appears as a gold medallion in the shape of a scarab beetle. It provides +3 resistance bonus to all saves and absorbs energy drain, death effects, and negative energy effects (12 charges). Becomes non-magical when all charges are consumed.",
            EquipSlot.Neck, 38000, 18, 0f, NeckIcon, NeckColor);
        item.WondrousItemType = "protection";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        item.WondrousCurrentCharges = 12;
        item.WondrousMaxCharges = 12;
        item.WondrousSaveBonus = 3;
        item.WondrousSaveType = "all";
        item.WondrousScarabAbsorbsDeath = true;
        item.WondrousScarabAbsorbsDrain = true;
        item.WondrousScarabAbsorbsNegativeEnergy = true;
        return item;
    }

    // ══════════════════════════════════════════════════════════════
    //  PHASE 2: MANTLE OF SPELL RESISTANCE (DMG p.263)
    // ══════════════════════════════════════════════════════════════

    /// <summary>Mantle of Spell Resistance. Grants continuous SR. DMG p.263.</summary>
    public static ItemData CreateMantleOfSpellResistance(int srLevel)
    {
        int price;
        switch (srLevel)
        {
            case 13: price = 90000; break;
            case 15: price = 121000; break;
            case 17: price = 157000; break;
            case 19: price = 198000; break;
            case 21: price = 250000; break;
            default: price = 90000; srLevel = 13; break;
        }
        string id = $"mantle_of_spell_resistance_{srLevel}";
        var item = CreateBaseWondrous(id,
            $"Mantle of Spell Resistance (SR {srLevel})",
            $"This fine garment, woven of undyed wool, radiates strong abjuration magic. When worn, it grants the wearer spell resistance {srLevel}.",
            EquipSlot.Back, price, 9, 1f, BackIcon, BackColor);
        item.WondrousItemType = "protection";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        item.WondrousGrantsSR = srLevel;
        return item;
    }

    // ══════════════════════════════════════════════════════════════
    //  PHASE 3: LEGENDARY PROTECTION ITEMS
    // ══════════════════════════════════════════════════════════════

    /// <summary>Mantle of Faith (+5 resistance saves, 4 spell-like abilities 1/day). DMG p.263.</summary>
    public static ItemData CreateMantleOfFaith()
    {
        var item = CreateBaseWondrous(WondrousItemNames.MANTLE_OF_FAITH,
            "Mantle of Faith",
            "This shimmering mantle of golden cloth is adorned with holy symbols. It grants a +5 resistance bonus to all saving throws and allows the wearer to cast Bless, Detect Evil, Remove Fear, and Aid each once per day (CL 5).",
            EquipSlot.Back, 76000, 5, 1f, BackIcon, BackColor);
        item.WondrousItemType = "protection";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        item.WondrousSaveBonus = 5;
        item.WondrousSaveType = "all";
        item.WondrousSpellLikeAbilities = "Bless,Detect Evil,Remove Fear,Aid";
        item.WondrousSpellLikeCasterLevel = 5;
        item.WondrousSpellLikeUsesPerDay = 1;
        item.WondrousSpellLikeUsesToday = "0,0,0,0"; // 0 uses consumed today
        return item;
    }

    // --- MONK'S BELT (update existing with bonus fields) ---

    /// <summary>Monk's Belt (+2 Wis, monk +5 levels). DMG p.248.</summary>
    public static ItemData CreateMonksBeltPhase10()
    {
        var item = CreateBaseWondrous(WondrousItemNames.MONKS_BELT,
            "Monk's Belt",
            "This belt grants the wearer the AC bonus, unarmed damage, and speed of a monk 5 levels higher if already a monk, or as a 5th-level monk if not. Also grants a +1 insight bonus to AC.",
            EquipSlot.Torso, 13000, 10, 1f, TorsoIcon, TorsoColor);
        item.WondrousItemType = "utility";
        item.WondrousActivationType = WondrousItemActivation.CONTINUOUS;
        item.WondrousMonkLevelBonus = 5;
        item.WondrousInsightACBonus = 1;
        return item;
    }

    // --- CUBE OF FORCE ---

    /// <summary>Cube of Force (36 charges, 6 modes). DMG p.253.</summary>
    public static ItemData CreateCubeOfForce()
    {
        var item = CreateBaseWondrous(WondrousItemNames.CUBE_OF_FORCE,
            "Cube of Force",
            "This small cube creates a 10-ft force cube around the bearer. Six buttons activate different modes: keep out gases, living matter, nonliving matter, magic missiles, all spell effects, everything. 36 charges; modes consume 1-6 charges per round.",
            EquipSlot.Slotless, 62000, 10, 0.5f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "protection";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        item.WondrousCurrentCharges = 36;
        item.WondrousMaxCharges = 36;
        return item;
    }

    // ══════════════════════════════════════════════════════════════
    //  PHASE 4/5: PLANAR TRAVEL ITEMS (DMG pp. 248–265)
    // ══════════════════════════════════════════════════════════════

    /// <summary>Amulet of the Planes (Plane Shift at will, 5% mishap). DMG p.247.</summary>
    public static ItemData CreateAmuletOfThePlanes()
    {
        var item = CreateBaseWondrous(WondrousItemNames.AMULET_OF_THE_PLANES,
            "Amulet of the Planes",
            "This amulet is a disk of deep blue sapphire attached to a chain of silver. It allows the wearer to use Plane Shift at will (standard action). Up to 8 willing creatures may be transported. There is a 5% chance of arriving on a random plane instead.",
            EquipSlot.Neck, 120000, 15, 0f, NeckIcon, NeckColor);
        item.WondrousItemType = "travel";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        item.WondrousUsesPerDay = -1; // Unlimited
        item.WondrousGrantsPlaneShift = true;
        item.WondrousPlaneShiftMaxTravelers = 8;
        item.WondrousPlaneShiftMishapPercent = 5;
        return item;
    }

    /// <summary>Cubic Gate (6 sides, each to different plane, 3 uses/week each). DMG p.253.</summary>
    public static ItemData CreateCubicGate()
    {
        var item = CreateBaseWondrous(WondrousItemNames.CUBIC_GATE,
            "Cubic Gate",
            "This 3-inch cube has six sides, each attuned to a different plane. Rotate to select a side, then activate to open a 10×10 ft gate to that plane for up to 10 minutes (concentration). Each side is usable 3 times per week. Default planes: Fire, Earth, Air, Water, Astral, Nine Hells.",
            EquipSlot.Slotless, 164000, 23, 0.5f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "travel";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        // Set up 6 sides with default plane assignments
        Plane[] defaults = PlanarTravelSystem.GetDefaultCubicGateSides();
        item.WondrousCubicGateSides = new int[6];
        for (int i = 0; i < 6; i++)
            item.WondrousCubicGateSides[i] = (int)defaults[i];
        item.WondrousCubicGateUsesThisWeek = new int[6]; // All zeros
        item.WondrousCubicGateMaxUsesPerSide = 3;
        return item;
    }

    /// <summary>Well of Many Worlds (random plane portal, extradimensional interaction). DMG p.270.</summary>
    public static ItemData CreateWellOfManyWorlds()
    {
        var item = CreateBaseWondrous(WondrousItemNames.WELL_OF_MANY_WORLDS,
            "Well of Many Worlds",
            "This strange, shimmering 6-foot-diameter piece of cloth is a portal to another plane. When unfolded and placed on the ground, it opens a two-way gate to a RANDOM plane of existence. The portal remains open as long as the Well is held open. Closing it ends the portal. WARNING: Contact with a Portable Hole or Bag of Holding destroys both items and creates a 10×10 ft rift to the Astral Plane for 1 round.",
            EquipSlot.Slotless, 82000, 12, 5f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "travel";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        item.WondrousUsesPerDay = -1; // Unlimited
        item.WondrousIsWellOfManyWorlds = true;
        item.WondrousIsExtradimensional = true; // Critical for Portable Hole interaction
        item.WondrousWellIsOpen = false;
        item.WondrousWellCurrentDestination = -1;
        return item;
    }

    /// <summary>Carpet of Flying, 10×10 ft (800 lbs, fly 40 ft average). DMG p.252.</summary>
    public static ItemData CreateCarpetOfFlying10x10()
    {
        var item = CreateBaseWondrous(WondrousItemNames.CARPET_OF_FLYING_10X10,
            "Carpet of Flying (10×10 ft)",
            "This large carpet, 10 feet by 10 feet, can fly through the air carrying up to 800 lbs. Fly speed 40 ft with average maneuverability. Activated by command word; can hover. Grants flight to all passengers standing on it.",
            EquipSlot.Slotless, 60000, 10, 25f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "travel";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousUsesPerDay = -1; // Unlimited
        item.WondrousIsCarpetOfFlying = true;
        item.WondrousCarpetSizeFeet = 10;
        item.WondrousCarpetCapacityLbs = 800;
        item.WondrousCarpetFlySpeed = 40;
        item.WondrousCarpetManeuverability = "average";
        item.WondrousCarpetIsFlying = false;
        // Also grant flight via standard movement system
        item.WondrousGrantsMovement = true;
        item.WondrousMovementMode = "fly";
        item.WondrousMovementSpeed = 40;
        item.WondrousFlightManeuverability = "average";
        return item;
    }

    // ══════════════════════════════════════════════════════════════
    //  PHASE 6/7/8: CREATURE TRAPPING ITEMS (DMG pp. 254–265)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Iron Flask — captures any extraplanar creature (Will DC 19).
    /// DMG p.261. 170,000 gp, CL 20, Weight 1 lb.
    /// Can trap one creature at a time; released creature serves for 1 hour.
    /// </summary>
    public static ItemData CreateIronFlask()
    {
        var item = CreateBaseWondrous(WondrousItemNames.IRON_FLASK,
            "Iron Flask",
            "This iron bottle can capture any extraplanar creature within 60 ft (Will DC 19 negates). "
            + "The creature is held inside until released by the owner. A released creature serves the opener "
            + "faithfully for 1 hour before becoming free. The flask can hold only one creature at a time. "
            + "Opening the flask to release its prisoner is a standard action.",
            EquipSlot.Slotless, 170000, 20, 1f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "trapping";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        // Trapping fields
        item.WondrousMaxTrappedCreatures = 1;
        item.WondrousTrapSaveDC = 19;
        item.WondrousTrapSaveType = "Will";
        item.WondrousTrapRangeFeet = 60;
        item.WondrousTrapAnyType = true; // Any extraplanar creature
        item.WondrousTrapServiceMinutes = 60f; // 1 hour
        item.WondrousTrappedCreatures = new System.Collections.Generic.List<TrappedCreature>();
        return item;
    }

    /// <summary>
    /// Efreeti Bottle — contains an efreeti that grants wishes or serves.
    /// DMG p.254. 145,000 gp, CL 14, Weight 1 lb.
    /// Comes pre-loaded with an efreeti. 1/day activation.
    /// </summary>
    public static ItemData CreateEfreetiBottle()
    {
        var item = CreateBaseWondrous(WondrousItemNames.EFREETI_BOTTLE,
            "Efreeti Bottle",
            "This heavy brass bottle contains a bound efreeti. Opening it (1/day) releases the efreeti, "
            + "which serves the opener for up to 10 minutes per day. The efreeti can grant up to 3 wishes "
            + "total (each counts as a day's service). There is a 10% chance the efreeti is hostile on release. "
            + "If slain, the efreeti reforms in the bottle in 24 hours.",
            EquipSlot.Slotless, 145000, 14, 1f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "trapping";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        item.WondrousUsesPerDay = 1;
        // Trapping fields
        item.WondrousMaxTrappedCreatures = 1;
        item.WondrousTrapSaveDC = 19;
        item.WondrousTrapSaveType = "Will";
        item.WondrousTrapRangeFeet = 0; // Not used for capture
        item.WondrousTrapAllowedTypes = "Outsider";
        item.WondrousTrapServiceMinutes = 10f; // 10 minutes per day
        item.WondrousTrapHasDefaultCreature = true;
        item.WondrousTrapDefaultCreatureType = "Efreeti";
        // Pre-load with efreeti
        item.WondrousTrappedCreatures = new System.Collections.Generic.List<TrappedCreature>();
        item.WondrousTrappedCreatures.Add(CreatureTrapSystem.CreateEfreetiData());
        return item;
    }

    /// <summary>
    /// Stone of Controlling Earth Elementals — summon Elder Earth Elemental 1/day + control earth elementals.
    /// DMG p.264. 100,000 gp, CL 16, Weight 5 lbs.
    /// </summary>
    public static ItemData CreateStoneOfControllingEarthElementals()
    {
        var item = CreateBaseWondrous(WondrousItemNames.STONE_OF_CONTROLLING_EARTH_ELEMENTALS,
            "Stone of Controlling Earth Elementals",
            "This stone appears to be a naturally shaped and polished chunk ofite. The bearer can summon "
            + "an elder earth elemental once per day (standard action). The elemental serves for 1 hour or "
            + "until dismissed. Additionally, while holding the stone, the bearer can attempt to control any "
            + "earth elemental within 60 ft (Will DC 18 negates). Controlled elementals serve for 1 hour.",
            EquipSlot.Slotless, 100000, 16, 5f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "trapping";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousUsesPerDay = 1;
        // Trapping fields for summoned elemental
        item.WondrousMaxTrappedCreatures = 1;
        item.WondrousTrapServiceMinutes = 60f; // 1 hour service
        item.WondrousTrapHasDefaultCreature = true;
        item.WondrousTrapDefaultCreatureType = "ElderEarthElemental";
        // Earth elemental control ability
        item.WondrousControlsEarthElementals = true;
        item.WondrousTrapControlRangeFeet = 60;
        item.WondrousTrapControlSaveDC = 18;
        item.WondrousTrappedCreatures = new System.Collections.Generic.List<TrappedCreature>();
        return item;
    }

    /// <summary>
    /// Mirror of Life Trapping — captures up to 15 creatures (Will DC 23).
    /// DMG p.263. 200,000 gp, CL 17, Weight 50 lbs.
    /// Creatures are trapped when they gaze into the mirror within 30 ft.
    /// Owner can release one or all at a time.
    /// </summary>
    public static ItemData CreateMirrorOfLifeTrapping()
    {
        var item = CreateBaseWondrous(WondrousItemNames.MIRROR_OF_LIFE_TRAPPING,
            "Mirror of Life Trapping",
            "This 4-foot-by-6-foot crystal mirror traps creatures that gaze into it (Will DC 23 negates). "
            + "It can hold up to 15 creatures. The mirror's owner can speak a command word to call forth any "
            + "trapped creature, which appears before the mirror and must serve the owner for 1 hour before "
            + "becoming free. Breaking the mirror frees all trapped creatures at once. "
            + "Range 30 ft for trapping effect. Can trap ANY creature type.",
            EquipSlot.Slotless, 200000, 17, 50f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "trapping";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousUsesPerDay = -1; // Unlimited trapping
        // Trapping fields
        item.WondrousMaxTrappedCreatures = 15;
        item.WondrousTrapSaveDC = 23;
        item.WondrousTrapSaveType = "Will";
        item.WondrousTrapRangeFeet = 30;
        item.WondrousTrapAnyType = true; // Can trap any creature type
        item.WondrousTrapServiceMinutes = 60f; // 1 hour service
        item.WondrousTrappedCreatures = new System.Collections.Generic.List<TrappedCreature>();
        return item;
    }

    // ══════════════════════════════════════════════════════════════
    //  PHASE 9: MIRROR OF OPPOSITION (DMG p.263)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Mirror of Opposition — creates evil duplicate of viewer.
    /// DMG p.263. 92,000 gp, CL 15, Weight 45 lbs.
    /// Large mirror (4×8 ft). Duplicate appears 1d4 rounds after viewing,
    /// has opposite alignment, full HP, exact stats/equipment/spells/feats.
    /// Duplicate attacks original until one is destroyed. Cannot reuse until duplicate defeated.
    /// </summary>
    public static ItemData CreateMirrorOfOpposition()
    {
        var item = CreateBaseWondrous(WondrousItemNames.MIRROR_OF_OPPOSITION,
            "Mirror of Opposition",
            "This large mirror (4×8 ft) creates an evil duplicate of anyone who gazes into it. "
            + "The duplicate appears 1d4 rounds later with opposite alignment, full hit points, "
            + "and exact copies of all stats, equipment, spells, and feats. Its sole goal is to "
            + "kill the original and take their place. The duplicate disappears when defeated. "
            + "The mirror cannot be used again until the current duplicate is destroyed.",
            EquipSlot.Slotless, 92000, 15, 45f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "mirror";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        // Mirror of Opposition fields
        item.WondrousIsMirrorOfOpposition = true;
        item.WondrousMirrorDuplicateActive = false;
        item.WondrousMirrorDuplicateID = null;
        item.WondrousMirrorDelayRounds = 0;
        return item;
    }

    // ══════════════════════════════════════════════════════════════
    //  PHASE 10: MIRROR OF MENTAL PROWESS (DMG p.263)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Mirror of Mental Prowess — +2 Int/Wis/Cha aura + 4 daily mental abilities.
    /// DMG p.263. 175,000 gp, CL 17, Weight 40 lbs.
    /// Constant +2 enhancement bonus to Int, Wis, Cha for anyone within 30 ft.
    /// 4 command-word abilities (1/day each): Scrying (Will DC 19), Detect Thoughts
    /// (Will DC 15, 60 ft), Suggestion (Will DC 17), Telepathy (60 ft).
    /// </summary>
    public static ItemData CreateMirrorOfMentalProwess()
    {
        var item = CreateBaseWondrous(WondrousItemNames.MIRROR_OF_MENTAL_PROWESS,
            "Mirror of Mental Prowess",
            "This ornate mirror (5×10 ft) grants a +2 enhancement bonus to Intelligence, Wisdom, "
            + "and Charisma to anyone within 30 ft (bonus lost when leaving range). Four command-word "
            + "abilities (1/day each): Scrying (Will DC 19) to see remote locations, Detect Thoughts "
            + "(Will DC 15, 60 ft) to read minds, Suggestion (Will DC 17) to plant commands, and "
            + "Telepathy (60 ft) for mental communication.",
            EquipSlot.Slotless, 175000, 17, 40f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "mirror";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousUsesPerDay = -1; // Multiple abilities tracked individually
        // Mental prowess aura
        item.WondrousIsMirrorOfMentalProwess = true;
        item.WondrousMirrorMentalBonus = 2;
        item.WondrousMirrorMentalBonusRange = 30;
        // Daily ability DCs and tracking
        item.WondrousMirrorScryingDC = 19;
        item.WondrousMirrorDetectThoughtsDC = 15;
        item.WondrousMirrorSuggestionDC = 17;
        item.WondrousMirrorTelepathyRange = 60;
        // Daily uses (0 = available, incremented when used)
        item.WondrousMirrorScryingUsesToday = 0;
        item.WondrousMirrorDetectThoughtsUsesToday = 0;
        item.WondrousMirrorSuggestionUsesToday = 0;
        item.WondrousMirrorTelepathyUsesToday = 0;
        return item;
    }

    // ══════════════════════════════════════════════════════════════
    //  PHASE 11: CONSTRUCT GUARDIANS (DMG pp. 259–264)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Iron Cobra — Tiny construct guardian with poison bite.
    /// DMG p.261. 80,000 gp, CL 15, Weight 3 lbs.
    /// AC 20, HP 30, Fast Healing 3. Attack +10, 1d3 + poison (Fort DC 16, 1d6 Con).
    /// Autonomous AI: patrol, detect, attack within 30 ft guard radius.
    /// </summary>
    public static ItemData CreateIronCobra()
    {
        var item = CreateBaseWondrous(WondrousItemNames.IRON_COBRA,
            "Iron Cobra",
            "This Tiny iron cobra construct follows its owner's commands. It can guard an area (30 ft radius), "
            + "patrol, detect hidden enemies, and attack intruders. Bite attack +10 for 1d3 damage plus "
            + "poison (Fort DC 16, 1d6 Con damage initial and secondary 1 minute later). AC 20, HP 30, "
            + "Fast Healing 3. Understands Common but cannot speak. Construct immunities apply.",
            EquipSlot.Slotless, 80000, 15, 3f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "construct";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        item.WondrousUsesPerDay = -1; // Always active when deployed
        // Iron Cobra stats
        item.WondrousIsIronCobra = true;
        item.WondrousIronCobraMaxHP = 30;
        item.WondrousIronCobraCurrentHP = 30;
        item.WondrousIronCobraAC = 20;
        item.WondrousIronCobraAttackBonus = 10;
        item.WondrousIronCobraDamageDice = "1d3";
        item.WondrousIronCobraFastHealing = 3;
        item.WondrousIronCobraPoisonDC = 16;
        item.WondrousIronCobraPoisonDamage = "1d6 Con";
        item.WondrousIronCobraGuardRadius = 30;
        item.WondrousIronCobraIsActive = false;
        return item;
    }

    /// <summary>
    /// Stone Horse (Courser) — fast light warhorse construct mount.
    /// DMG p.264. 10,000 gp, CL 14, Weight 6,000 lbs (stone), 600 lbs (active).
    /// Speed 50 ft, AC 14, HP 30. Light warhorse stats.
    /// </summary>
    public static ItemData CreateStoneHorseCourser()
    {
        var item = CreateBaseWondrous(WondrousItemNames.STONE_HORSE_COURSER,
            "Stone Horse (Courser)",
            "This finely crafted stone statuette transforms into a living light warhorse on command. "
            + "Speed 50 ft, AC 14, HP 30. Str 16, Dex 13, Con 15. Does not eat, sleep, or tire "
            + "(construct). Grants mount speed to rider. Reverts to stone form on command or when reduced to 0 HP.",
            EquipSlot.Slotless, 10000, 14, 1f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "construct";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        // Stone Horse stats - Courser
        item.WondrousIsStoneHorse = true;
        item.WondrousStoneHorseType = "Courser";
        item.WondrousStoneHorseSpeed = 50;
        item.WondrousStoneHorseFlySpeed = 0;
        item.WondrousStoneHorseAC = 14;
        item.WondrousStoneHorseMaxHP = 30;
        item.WondrousStoneHorseCurrentHP = 30;
        item.WondrousStoneHorseSTR = 16;
        item.WondrousStoneHorseDEX = 13;
        item.WondrousStoneHorseCON = 15;
        item.WondrousStoneHorseIsActive = false;
        // Also grant mount movement
        item.WondrousGrantsMovement = true;
        item.WondrousMovementMode = "mount";
        item.WondrousMovementSpeed = 50;
        return item;
    }

    /// <summary>
    /// Stone Horse (Destrier) — heavy warhorse construct mount, better combat.
    /// DMG p.264. 14,800 gp, CL 14, Weight 6,000 lbs (stone), 900 lbs (active).
    /// Speed 40 ft, AC 16, HP 45. Heavy warhorse stats with better Str.
    /// </summary>
    public static ItemData CreateStoneHorseDestrier()
    {
        var item = CreateBaseWondrous(WondrousItemNames.STONE_HORSE_DESTRIER,
            "Stone Horse (Destrier)",
            "This stone statuette transforms into a heavy warhorse on command. Slower but tougher than "
            + "the courser. Speed 40 ft, AC 16, HP 45. Str 20, Dex 11, Con 17. Does not eat, sleep, "
            + "or tire (construct). Better suited for combat with higher Str and HP. "
            + "Reverts to stone form on command or when reduced to 0 HP.",
            EquipSlot.Slotless, 14800, 14, 1f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "construct";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        // Stone Horse stats - Destrier
        item.WondrousIsStoneHorse = true;
        item.WondrousStoneHorseType = "Destrier";
        item.WondrousStoneHorseSpeed = 40;
        item.WondrousStoneHorseFlySpeed = 0;
        item.WondrousStoneHorseAC = 16;
        item.WondrousStoneHorseMaxHP = 45;
        item.WondrousStoneHorseCurrentHP = 45;
        item.WondrousStoneHorseSTR = 20;
        item.WondrousStoneHorseDEX = 11;
        item.WondrousStoneHorseCON = 17;
        item.WondrousStoneHorseIsActive = false;
        item.WondrousGrantsMovement = true;
        item.WondrousMovementMode = "mount";
        item.WondrousMovementSpeed = 40;
        return item;
    }

    /// <summary>
    /// Stone Horse (Griffon) — flying construct mount.
    /// DMG p.264. 28,500 gp, CL 14, Weight 6,000 lbs (stone), 500 lbs (active).
    /// Fly 100 ft (perfect), land 30 ft, AC 17, HP 60. Grants flight to rider.
    /// </summary>
    public static ItemData CreateStoneHorseGriffon()
    {
        var item = CreateBaseWondrous(WondrousItemNames.STONE_HORSE_GRIFFON,
            "Stone Horse (Griffon)",
            "This stone statuette transforms into a griffon-like construct mount on command. "
            + "Fly speed 100 ft (perfect maneuverability), land speed 30 ft. AC 17, HP 60. "
            + "Str 18, Dex 15, Con 16. Does not eat, sleep, or tire (construct). "
            + "Grants flight to rider! Reverts to stone form on command or when reduced to 0 HP.",
            EquipSlot.Slotless, 28500, 14, 1f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "construct";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.COMMAND_WORD;
        // Stone Horse stats - Griffon
        item.WondrousIsStoneHorse = true;
        item.WondrousStoneHorseType = "Griffon";
        item.WondrousStoneHorseSpeed = 30;
        item.WondrousStoneHorseFlySpeed = 100;
        item.WondrousStoneHorseManeuverability = "perfect";
        item.WondrousStoneHorseAC = 17;
        item.WondrousStoneHorseMaxHP = 60;
        item.WondrousStoneHorseCurrentHP = 60;
        item.WondrousStoneHorseSTR = 18;
        item.WondrousStoneHorseDEX = 15;
        item.WondrousStoneHorseCON = 16;
        item.WondrousStoneHorseIsActive = false;
        // Grant flight via mount
        item.WondrousGrantsMovement = true;
        item.WondrousMovementMode = "fly";
        item.WondrousMovementSpeed = 100;
        item.WondrousFlightManeuverability = "perfect";
        return item;
    }

    // ══════════════════════════════════════════════════════════════
    //  PHASE 12: APPARATUS OF KWALISH (DMG p.247)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Apparatus of Kwalish — Large iron lobster vehicle.
    /// DMG p.247. 90,000 gp, CL 19, Weight 500 lbs.
    /// Holds 2 Medium creatures. AC 20, HP 200, Hardness 10.
    /// 10 levers control movement, hatches, pincers, etc. Air supply 10 hours sealed.
    /// Can walk (10 ft/round) or swim (30-200 ft/round). Pincer attack +10, 2d6.
    /// </summary>
    public static ItemData CreateApparatusOfKwalish()
    {
        var item = CreateBaseWondrous(WondrousItemNames.APPARATUS_OF_KWALISH,
            "Apparatus of Kwalish",
            "This iron barrel resembles a large lobster when its 10 levers are operated. It holds 2 Medium "
            + "creatures. AC 20, HP 200, Hardness 10. 10 levers control: (1) fast swim 200 ft/rnd, "
            + "(2) slow swim 30 ft/rnd, (3-4) turn left/right 90°, (5) hatch, (6) forward window, "
            + "(7) side windows, (8) extend legs (walk 10 ft/rnd), (9) pincers (+10, 2d6), "
            + "(10) antenna (30 ft light). Air supply: 10 hours when sealed. Can walk or swim. "
            + "Immune to most spells while sealed.",
            EquipSlot.Slotless, 90000, 19, 500f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "vehicle";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        // Apparatus stats
        item.WondrousIsApparatusOfKwalish = true;
        item.WondrousApparatusAC = 20;
        item.WondrousApparatusMaxHP = 200;
        item.WondrousApparatusCurrentHP = 200;
        item.WondrousApparatusHardness = 10;
        item.WondrousApparatusMaxOccupants = 2;
        item.WondrousApparatusAirHours = 10f;
        item.WondrousApparatusCurrentSpeed = 0;
        item.WondrousApparatusFacing = 0;
        item.WondrousApparatusLevers = new bool[10]; // All off initially
        item.WondrousApparatusPincerAttack = 10;
        item.WondrousApparatusPincerDamage = "2d6";
        return item;
    }

    // ══════════════════════════════════════════════════════════════
    //  PHASE 13: LEGENDARY TOOLS (DMG pp. 259–265)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Mattock of the Titans — Huge adamantine mattock +3.
    /// DMG p.262. 23,348 gp, CL 15, Weight 120 lbs.
    /// 4d6+3 damage. Destroys stone structures, auto-breaks DC ≤ 40 objects.
    /// Ignores hardness up to 20 (adamantine). Medium wielders take -4 penalty.
    /// </summary>
    public static ItemData CreateMattockOfTheTitans()
    {
        var item = CreateBaseWondrous(WondrousItemNames.MATTOCK_OF_THE_TITANS,
            "Mattock of the Titans",
            "This Huge adamantine mattock is sized for a titan but can be wielded (clumsily) by a Medium "
            + "creature at a -4 penalty. +3 enhancement bonus, 4d6+3 damage. As adamantine, it ignores "
            + "hardness up to 20 and automatically breaks objects with a Break DC of 40 or less. "
            + "Particularly effective against stone structures, dealing double damage to stone.",
            EquipSlot.Slotless, 23348, 15, 120f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "weapon";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        // Titan weapon stats
        item.WondrousIsMattockOfTitans = true;
        item.WondrousTitanEnhancement = 3;
        item.WondrousTitanDamageDice = "4d6";
        item.WondrousTitanWeightLbs = 120;
        item.WondrousTitanSize = "Huge";
        item.WondrousTitanMaterial = "Adamantine";
        item.WondrousTitanIgnoreHardness = 20;
        item.WondrousTitanAutoBreakDC = 40;
        item.WondrousTitanOversizePenalty = -4;
        return item;
    }

    /// <summary>
    /// Maul of the Titans — Huge adamantine maul +3 with superior sunder.
    /// DMG p.262. 25,305 gp, CL 15, Weight 160 lbs.
    /// 4d6+3 damage. +4 sunder bonus, sunder does not provoke AoO.
    /// Can sunder any item (even artifacts). Medium wielders take -4 penalty.
    /// </summary>
    public static ItemData CreateMaulOfTheTitans()
    {
        var item = CreateBaseWondrous(WondrousItemNames.MAUL_OF_THE_TITANS,
            "Maul of the Titans",
            "This Huge adamantine maul is the ultimate sundering weapon. +3 enhancement bonus, 4d6+3 damage. "
            + "Grants a +4 bonus on sunder attempts and sundering does NOT provoke attacks of opportunity. "
            + "Can attempt to sunder any item, even artifacts (though artifacts may resist). "
            + "Ignores hardness up to 20. Medium wielders take -4 penalty for oversized weapon.",
            EquipSlot.Slotless, 25305, 15, 160f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "weapon";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        // Titan weapon stats
        item.WondrousIsMaulOfTitans = true;
        item.WondrousTitanEnhancement = 3;
        item.WondrousTitanDamageDice = "4d6";
        item.WondrousTitanWeightLbs = 160;
        item.WondrousTitanSize = "Huge";
        item.WondrousTitanMaterial = "Adamantine";
        item.WondrousTitanIgnoreHardness = 20;
        item.WondrousTitanAutoBreakDC = 0; // Maul uses sunder, not auto-break
        item.WondrousTitanSunderBonus = 4;
        item.WondrousTitanSunderNoAoO = true;
        item.WondrousTitanOversizePenalty = -4;
        return item;
    }

    /// <summary>
    /// Lyre of Building — magical instrument that accelerates construction.
    /// DMG p.262. 13,000 gp, CL 6, Weight 5 lbs.
    /// 1 hour playing = 800 worker-hours. Perform DC 15. 3 uses/week.
    /// </summary>
    public static ItemData CreateLyreOfBuilding()
    {
        var item = CreateBaseWondrous(WondrousItemNames.LYRE_OF_BUILDING,
            "Lyre of Building",
            "This magical stringed instrument accelerates construction projects when played. One hour of "
            + "playing accomplishes as much work as 100 workers laboring for 8 hours (800 worker-hours). "
            + "Requires a Perform (string instruments) check DC 15 to activate the magic. Usable 3 times "
            + "per week. Can construct buildings, fortifications, ships, and other large projects.",
            EquipSlot.Slotless, 13000, 6, 5f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "utility";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        // Lyre fields
        item.WondrousIsLyreOfBuilding = true;
        item.WondrousLyreUsesPerWeek = 3;
        item.WondrousLyreUsesThisWeek = 0;
        item.WondrousLyrePerformDC = 15;
        item.WondrousLyreWorkerHoursPerUse = 800;
        return item;
    }

    /// <summary>
    /// Horn of Valhalla (Iron) — summons 20 barbarian warriors.
    /// DMG p.259. 50,000 gp, CL 13, Weight 2 lbs.
    /// Summons 20× 2nd-level barbarians for 1 hour. Once per week.
    /// </summary>
    public static ItemData CreateHornOfValhallaIron()
    {
        var item = CreateBaseWondrous(WondrousItemNames.HORN_OF_VALHALLA_IRON,
            "Horn of Valhalla (Iron)",
            "This great iron war horn summons a host of warriors from Valhalla when blown. 20 barbarian "
            + "warriors (2nd level each) appear and fight for the horn blower for 1 hour. Each barbarian "
            + "has 19 HP, AC 14, attacks at +4 with a greataxe (1d12+3), and possesses Rage, Power Attack, "
            + "and Cleave. They follow commands and fight to the death. Usable once per week.",
            EquipSlot.Slotless, 50000, 13, 2f, SlotlessIcon, SlotlessColor);
        item.WondrousItemType = "summoning";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        // Horn fields
        item.WondrousIsHornOfValhalla = true;
        item.WondrousHornType = "Iron";
        item.WondrousHornBarbarianCount = 20;
        item.WondrousHornBarbarianLevel = 2;
        item.WondrousHornBarbarianHP = 19;
        item.WondrousHornBarbarianAC = 14;
        item.WondrousHornBarbarianAttack = 4;
        item.WondrousHornBarbarianDamage = "1d12+3";
        item.WondrousHornServiceMinutes = 60f;
        item.WondrousHornUsesPerWeek = 1;
        item.WondrousHornUsesThisWeek = 0;
        return item;
    }
}
