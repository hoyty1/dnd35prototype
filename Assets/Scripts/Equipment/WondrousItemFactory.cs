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

    /// <summary>Wings of Flying (fly 60 ft good maneuverability). DMG p.271.</summary>
    public static ItemData CreateWingsOfFlying()
    {
        var item = CreateBaseWondrous(WondrousItemNames.WINGS_OF_FLYING,
            "Wings of Flying",
            "These wings appear to be a cloak made from silk. When the command word is spoken, the cloak turns into a pair of bat wings or bird wings, granting fly 60 ft (good maneuverability).",
            EquipSlot.Back, 54000, 10, 2f, BackIcon, BackColor);
        item.WondrousItemType = "movement";
        item.WondrousGrantsMovement = true;
        item.WondrousMovementMode = "fly";
        item.WondrousMovementSpeed = 60;
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

    /// <summary>Bracers of Archery (Lesser: +1 competence; Greater: +2 competence to bow attacks). DMG p.250.</summary>
    public static ItemData CreateBracersOfArchery(bool greater)
    {
        string id = greater ? WondrousItemNames.BRACERS_OF_ARCHERY_GREATER : WondrousItemNames.BRACERS_OF_ARCHERY_LESSER;
        string tier = greater ? "Greater" : "Lesser";
        int bonus = greater ? 2 : 1;
        int price = greater ? 25000 : 5000;
        var item = CreateBaseWondrous(id, $"Bracers of Archery, {tier}",
            $"These wristbands grant a +{bonus} competence bonus on attack rolls made with bows.",
            EquipSlot.Wrists, price, 4, 1f, WristsIcon, WristsColor);
        item.WondrousItemType = "attack";
        item.WondrousSkillBonus = bonus;
        item.WondrousSkillName = "Bow Attack";
        item.WondrousSkillBonusType = "competence";
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
            "As a free action, the wearer can click her boot heels together to gain the effect of haste for up to 10 rounds each day.",
            EquipSlot.Feet, 12000, 10, 1f, FeetIcon, FeetColor);
        item.WondrousItemType = "movement";
        item.WondrousHasActivation = true;
        item.WondrousActivationType = WondrousItemActivation.USE_ACTIVATED;
        item.WondrousUsesPerDay = 10;
        item.WondrousSpeedBonus = 30; // Haste grants +30 ft
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
        item.WondrousActivationType = WondrousItemActivation.PASSIVE;
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
