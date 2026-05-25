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
}
