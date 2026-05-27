using System.Collections.Generic;
using UnityEngine;

// ════════════════════════════════════════════════════════════════════════
//  D&D 3.5e Rod Factory (DMG pp. 224–228)
//
//  Creates ItemData instances for all 36 DMG rods:
//  - 21 Metamagic rods (7 types × 3 power levels)
//  - 7 Combat rods
//  - 5 Utility rods
//  - 3 Legendary rods (overlap with Combat/Utility)
//
//  Follows the same factory pattern as WondrousItemFactory and RingFactory.
// ════════════════════════════════════════════════════════════════════════

public static class RodFactory
{
    // ── Rod Icons ─────────────────────────────────────────────
    private const string MetamagicIcon = "🪄";   // Wand/magic
    private const string CombatIcon = "⚔️";       // Crossed swords
    private const string UtilityIcon = "🔮";      // Crystal ball
    private const string LegendaryIcon = "👑";    // Crown

    private static readonly Color MetamagicColor = new Color(0.6f, 0.4f, 0.9f);  // Purple
    private static readonly Color CombatColor = new Color(0.9f, 0.3f, 0.3f);     // Red
    private static readonly Color UtilityColor = new Color(0.3f, 0.7f, 0.9f);    // Cyan
    private static readonly Color LegendaryColor = new Color(1f, 0.85f, 0.3f);   // Gold

    // ════════════════════════════════════════════════════════════
    //  Base Rod Creator
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a base rod ItemData with standard rod properties.
    /// All rods: Type = Wondrous, Slot = EitherHand, Weight = 5 lbs, IsRod = true.
    /// </summary>
    private static ItemData CreateBaseRod(string id, string name, string description,
        RodCategory category, int priceGp, int casterLevel = 17, float weightLbs = 5f)
    {
        string icon;
        Color color;
        switch (category)
        {
            case RodCategory.Legendary:
                icon = LegendaryIcon; color = LegendaryColor; break;
            case RodCategory.Combat:
                icon = CombatIcon; color = CombatColor; break;
            case RodCategory.Utility:
                icon = UtilityIcon; color = UtilityColor; break;
            default:
                icon = MetamagicIcon; color = MetamagicColor; break;
        }

        return new ItemData
        {
            Id = id,
            Name = name,
            Description = description,
            Type = ItemType.Wondrous,        // Rods use Wondrous type (held items)
            Slot = EquipSlot.EitherHand,     // Rods are held, not worn
            IsRod = true,
            RodId = id,
            RodCategory = category,
            RodCasterLevel = casterLevel,
            BasePriceGp = priceGp,
            WeightLbs = weightLbs,
            CountsAsMagicForBypass = true,
            IconChar = icon,
            IconColor = color
        };
    }

    // ════════════════════════════════════════════════════════════
    //  METAMAGIC RODS — 7 types × 3 power levels = 21 rods
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a metamagic rod with the specified type and power level.
    /// All metamagic rods: 3/day, CL 17th, use-activated, 5 lbs.
    /// KEY RULE: Apply metamagic WITHOUT increasing spell slot level!
    /// </summary>
    public static ItemData CreateMetamagicRod(MetamagicFeatId metamagicType,
        RodPowerLevel power, string id, string name, int price, int slotIncrease)
    {
        int maxSpellLevel = power == RodPowerLevel.Lesser ? 3 :
                            power == RodPowerLevel.Normal ? 6 : 9;

        string powerLabel = power == RodPowerLevel.Lesser ? "Lesser" :
                            power == RodPowerLevel.Normal ? "" : "Greater";
        string powerDesc = power == RodPowerLevel.Lesser ? "up to 3rd-level" :
                           power == RodPowerLevel.Normal ? "up to 6th-level" : "up to 9th-level";

        string metamagicName = MetamagicData.GetDisplayName(metamagicType);
        string desc = $"This rod allows the wielder to apply {metamagicName} to {powerDesc} spells " +
                      $"without increasing the spell slot level. Usable 3 times per day. " +
                      $"The metamagic effect is equivalent to a +{slotIncrease} level adjustment, " +
                      $"but the rod absorbs this cost entirely.";

        var item = CreateBaseRod(id, name, desc, RodCategory.Metamagic, price);

        // Metamagic rod fields
        item.RodIsMetamagic = true;
        item.RodMetamagicType = metamagicType;
        item.RodPower = power;
        item.RodMaxSpellLevel = maxSpellLevel;
        item.RodSlotLevelIncrease = slotIncrease;
        item.RodUsesPerDay = 3;
        item.RodUsesToday = 0;

        return item;
    }

    // ── Empower Spell Rods (+2 slot equivalent) ───────────────

    public static ItemData CreateRodOfEmpowerLesser()
    {
        return CreateMetamagicRod(MetamagicFeatId.EmpowerSpell, RodPowerLevel.Lesser,
            RodNames.ROD_EMPOWER_LESSER, "Rod of Empower Spell, Lesser", 9000, 2);
    }

    public static ItemData CreateRodOfEmpowerNormal()
    {
        return CreateMetamagicRod(MetamagicFeatId.EmpowerSpell, RodPowerLevel.Normal,
            RodNames.ROD_EMPOWER_NORMAL, "Rod of Empower Spell", 32500, 2);
    }

    public static ItemData CreateRodOfEmpowerGreater()
    {
        return CreateMetamagicRod(MetamagicFeatId.EmpowerSpell, RodPowerLevel.Greater,
            RodNames.ROD_EMPOWER_GREATER, "Rod of Empower Spell, Greater", 73000, 2);
    }

    // ── Enlarge Spell Rods (+1 slot equivalent) ───────────────

    public static ItemData CreateRodOfEnlargeLesser()
    {
        return CreateMetamagicRod(MetamagicFeatId.EnlargeSpell, RodPowerLevel.Lesser,
            RodNames.ROD_ENLARGE_LESSER, "Rod of Enlarge Spell, Lesser", 3000, 1);
    }

    public static ItemData CreateRodOfEnlargeNormal()
    {
        return CreateMetamagicRod(MetamagicFeatId.EnlargeSpell, RodPowerLevel.Normal,
            RodNames.ROD_ENLARGE_NORMAL, "Rod of Enlarge Spell", 11000, 1);
    }

    public static ItemData CreateRodOfEnlargeGreater()
    {
        return CreateMetamagicRod(MetamagicFeatId.EnlargeSpell, RodPowerLevel.Greater,
            RodNames.ROD_ENLARGE_GREATER, "Rod of Enlarge Spell, Greater", 24500, 1);
    }

    // ── Extend Spell Rods (+1 slot equivalent) ────────────────

    public static ItemData CreateRodOfExtendLesser()
    {
        return CreateMetamagicRod(MetamagicFeatId.ExtendSpell, RodPowerLevel.Lesser,
            RodNames.ROD_EXTEND_LESSER, "Rod of Extend Spell, Lesser", 3000, 1);
    }

    public static ItemData CreateRodOfExtendNormal()
    {
        return CreateMetamagicRod(MetamagicFeatId.ExtendSpell, RodPowerLevel.Normal,
            RodNames.ROD_EXTEND_NORMAL, "Rod of Extend Spell", 11000, 1);
    }

    public static ItemData CreateRodOfExtendGreater()
    {
        return CreateMetamagicRod(MetamagicFeatId.ExtendSpell, RodPowerLevel.Greater,
            RodNames.ROD_EXTEND_GREATER, "Rod of Extend Spell, Greater", 24500, 1);
    }

    // ── Maximize Spell Rods (+3 slot equivalent) ──────────────

    public static ItemData CreateRodOfMaximizeLesser()
    {
        return CreateMetamagicRod(MetamagicFeatId.MaximizeSpell, RodPowerLevel.Lesser,
            RodNames.ROD_MAXIMIZE_LESSER, "Rod of Maximize Spell, Lesser", 14000, 3);
    }

    public static ItemData CreateRodOfMaximizeNormal()
    {
        return CreateMetamagicRod(MetamagicFeatId.MaximizeSpell, RodPowerLevel.Normal,
            RodNames.ROD_MAXIMIZE_NORMAL, "Rod of Maximize Spell", 54000, 3);
    }

    public static ItemData CreateRodOfMaximizeGreater()
    {
        return CreateMetamagicRod(MetamagicFeatId.MaximizeSpell, RodPowerLevel.Greater,
            RodNames.ROD_MAXIMIZE_GREATER, "Rod of Maximize Spell, Greater", 121500, 3);
    }

    // ── Quicken Spell Rods (+4 slot equivalent) ───────────────

    public static ItemData CreateRodOfQuickenLesser()
    {
        return CreateMetamagicRod(MetamagicFeatId.QuickenSpell, RodPowerLevel.Lesser,
            RodNames.ROD_QUICKEN_LESSER, "Rod of Quicken Spell, Lesser", 35000, 4);
    }

    public static ItemData CreateRodOfQuickenNormal()
    {
        return CreateMetamagicRod(MetamagicFeatId.QuickenSpell, RodPowerLevel.Normal,
            RodNames.ROD_QUICKEN_NORMAL, "Rod of Quicken Spell", 75500, 4);
    }

    public static ItemData CreateRodOfQuickenGreater()
    {
        return CreateMetamagicRod(MetamagicFeatId.QuickenSpell, RodPowerLevel.Greater,
            RodNames.ROD_QUICKEN_GREATER, "Rod of Quicken Spell, Greater", 170000, 4);
    }

    // ── Silent Spell Rods (+1 slot equivalent) ────────────────

    public static ItemData CreateRodOfSilentLesser()
    {
        return CreateMetamagicRod(MetamagicFeatId.SilentSpell, RodPowerLevel.Lesser,
            RodNames.ROD_SILENT_LESSER, "Rod of Silent Spell, Lesser", 3000, 1);
    }

    public static ItemData CreateRodOfSilentNormal()
    {
        return CreateMetamagicRod(MetamagicFeatId.SilentSpell, RodPowerLevel.Normal,
            RodNames.ROD_SILENT_NORMAL, "Rod of Silent Spell", 11000, 1);
    }

    public static ItemData CreateRodOfSilentGreater()
    {
        return CreateMetamagicRod(MetamagicFeatId.SilentSpell, RodPowerLevel.Greater,
            RodNames.ROD_SILENT_GREATER, "Rod of Silent Spell, Greater", 24500, 1);
    }

    // ── Widen Spell Rods (+3 slot equivalent) ─────────────────

    public static ItemData CreateRodOfWidenLesser()
    {
        return CreateMetamagicRod(MetamagicFeatId.WidenSpell, RodPowerLevel.Lesser,
            RodNames.ROD_WIDEN_LESSER, "Rod of Widen Spell, Lesser", 14000, 3);
    }

    public static ItemData CreateRodOfWidenNormal()
    {
        return CreateMetamagicRod(MetamagicFeatId.WidenSpell, RodPowerLevel.Normal,
            RodNames.ROD_WIDEN_NORMAL, "Rod of Widen Spell", 54000, 3);
    }

    public static ItemData CreateRodOfWidenGreater()
    {
        return CreateMetamagicRod(MetamagicFeatId.WidenSpell, RodPowerLevel.Greater,
            RodNames.ROD_WIDEN_GREATER, "Rod of Widen Spell, Greater", 121500, 3);
    }

    // ════════════════════════════════════════════════════════════
    //  COMBAT RODS — 7 rods
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Rod of Absorption (DMG p.224) — 50,000 gp, CL 15th.
    /// Absorbs targeted spells as a reaction, storing up to 50 spell levels.
    /// Stored levels can be converted to spell slots or discharged.
    /// </summary>
    public static ItemData CreateRodOfAbsorption()
    {
        var item = CreateBaseRod(RodNames.ROD_ABSORPTION, "Rod of Absorption",
            "This rod absorbs spells or spell-like abilities targeted at the wielder. " +
            "It can store up to 50 spell levels. The wielder can use stored energy to " +
            "cast spells they have prepared (or know) without expending a spell slot, " +
            "using absorbed levels equal to the spell's level.",
            RodCategory.Combat, 50000, 15);

        item.RodCanAbsorbSpells = true;
        item.RodAbsorbedLevels = 0;
        item.RodMaxAbsorbedLevels = 50;
        item.RodUsesPerDay = 0; // Unlimited absorptions, limited by capacity

        return item;
    }

    /// <summary>
    /// Rod of Cancellation (DMG p.224) — 11,000 gp, CL 17th.
    /// Touch to permanently destroy one magic item's enchantment. Single use.
    /// </summary>
    public static ItemData CreateRodOfCancellation()
    {
        var item = CreateBaseRod(RodNames.ROD_CANCELLATION, "Rod of Cancellation",
            "This dreaded rod drains an item of all magical properties upon touch. " +
            "The item touched gets no saving throw. The rod can be used only once, " +
            "after which it becomes a nonmagical piece of kindling.",
            RodCategory.Combat, 11000);

        item.RodCanCancelMagic = true;
        item.RodIsExpended = false;

        return item;
    }

    /// <summary>
    /// Rod of Flailing (DMG p.224) — 50,000 gp, CL 9th.
    /// Functions as a +3 heavy flail. On command, becomes a dire flail (two heads).
    /// </summary>
    public static ItemData CreateRodOfFlailing()
    {
        var item = CreateBaseRod(RodNames.ROD_FLAILING, "Rod of Flailing",
            "Upon command, this rod becomes a +3 dire flail with two attacking heads. " +
            "In its normal form it is a +3 heavy flail. " +
            "Each head of the dire flail deals 1d8+3 damage. " +
            "A character wielding this rod in dire flail mode gains a +4 deflection bonus to AC.",
            RodCategory.Combat, 50000, 9);

        item.RodIsFlail = true;
        item.RodWeaponEnhancement = 3;
        item.RodWeaponDamageDice = "1d8";
        item.RodWeaponMode = "Heavy Flail"; // Default mode
        item.RodFlailDeflectionBonus = 4;   // +4 deflection in dire flail mode

        return item;
    }

    /// <summary>
    /// Immovable Rod (DMG p.224) — 5,000 gp, CL 8th.
    /// Button activation: rod holds position in space, supporting up to 8,000 lbs.
    /// DC 30 Strength check to move when activated.
    /// </summary>
    public static ItemData CreateImmovableRod()
    {
        var item = CreateBaseRod(RodNames.ROD_IMMOVABLE, "Immovable Rod",
            "This rod is a flat iron bar with a small button on one end. " +
            "When the button is pushed, the rod does not move from where it is, " +
            "even if staying in place defies gravity. It can hold up to 8,000 pounds " +
            "of weight. A DC 30 Strength check allows a character to move the rod.",
            RodCategory.Combat, 5000, 8);

        item.RodIsImmovable = true;
        item.RodIsActivated = false;     // Button not pressed yet
        item.RodHoldWeightLbs = 8000;
        item.RodMoveDC = 30;

        return item;
    }

    /// <summary>
    /// Rod of Lordly Might (DMG p.225) — 70,000 gp, CL 19th.
    /// 6 weapon transformation modes activated by buttons:
    /// +3 heavy mace, +1 flaming longsword, +4 battleaxe,
    /// +3 shortspear, +2 longsword, 50-ft climbing pole.
    /// Special: Fear cone 2/day (30 ft, DC 16 Will).
    /// </summary>
    public static ItemData CreateRodOfLordlyMight()
    {
        var item = CreateBaseRod(RodNames.ROD_LORDLY_MIGHT, "Rod of Lordly Might",
            "This rod has six different functions activated by buttons:\n" +
            "• Default: +3 heavy mace (1d8+3)\n" +
            "• Button 1: +1 flaming longsword (1d8+1, +1d6 fire)\n" +
            "• Button 2: +4 battleaxe (1d8+4)\n" +
            "• Button 3: +3 shortspear (1d6+3, throwable 20 ft)\n" +
            "• Button 4: +2 longsword (1d8+2)\n" +
            "• Button 5: 50-ft climbing pole\n" +
            "Special: Fear cone 30 ft (DC 16 Will), 2/day.",
            RodCategory.Legendary, 70000, 19);

        item.RodIsLordlyMight = true;
        item.RodLordlyMightMode = (int)LordlyMightWeaponMode.HeavyMace;
        item.RodWeaponEnhancement = 3;   // Default +3 mace
        item.RodWeaponDamageDice = "1d8";
        item.RodWeaponMode = "Heavy Mace";
        item.RodFearConeDC = 16;
        item.RodFearConeRangeFt = 30;
        item.RodFearUsesPerDay = 2;
        item.RodFearUsesToday = 0;
        item.IsLegendary = true;

        return item;
    }

    /// <summary>
    /// Rod of Metal and Mineral Detection (DMG p.226) — 10,500 gp, CL 9th.
    /// Detect metals and minerals within 30 ft, penetrating up to 10 ft of stone.
    /// </summary>
    public static ItemData CreateRodOfMetalAndMineralDetection()
    {
        var item = CreateBaseRod(RodNames.ROD_METAL_AND_MINERAL_DETECTION,
            "Rod of Metal and Mineral Detection",
            "This rod pulses in the wielder's hand when pointed toward a mass of metal " +
            "or mineral within 30 feet. It can penetrate barriers up to 10 feet of stone " +
            "but is blocked by lead or gold sheeting. It identifies the type and approximate " +
            "quantity of the detected substance.",
            RodCategory.Combat, 10500, 9);

        item.RodCanDetectMetals = true;
        item.RodDetectionRadiusFt = 30f;
        item.RodPenetratesStoneFt = 10f;

        return item;
    }

    /// <summary>
    /// Rod of Splendor (DMG p.226) — 25,000 gp, CL 12th.
    /// Create pavilion tent (100 people, 1/week), fine clothes (7/week), feast (12 people, 1/day).
    /// </summary>
    public static ItemData CreateRodOfSplendor()
    {
        var item = CreateBaseRod(RodNames.ROD_SPLENDOR, "Rod of Splendor",
            "This rod provides the following abilities:\n" +
            "• Create a pavilion-sized tent (100 people), 1/week\n" +
            "• Create a set of fine clothes, up to 7/week\n" +
            "• Create a magnificent feast (12 people), 1/day\n" +
            "The wielder also projects a +4 enhancement bonus to Charisma while holding the rod.",
            RodCategory.Combat, 25000, 12);

        item.RodIsSplendor = true;
        item.RodSplendorTentUsesPerWeek = 1;
        item.RodSplendorTentUsesThisWeek = 0;
        item.RodSplendorClothesPerWeek = 7;
        item.RodSplendorClothesThisWeek = 0;
        item.RodSplendorFeastUsesPerDay = 1;
        item.RodSplendorFeastUsesToday = 0;
        item.RodSplendorCharismaBonus = 4;

        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  UTILITY RODS — 5 rods
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Rod of Alertness (DMG p.224) — 85,000 gp, CL 11th.
    /// +1 insight bonus to Initiative and Listen, command-word abilities:
    /// Light, Detect Evil, Detect Magic (60 ft), See Invisible at will.
    /// Can animate into a +1 defender longsword.
    /// </summary>
    public static ItemData CreateRodOfAlertness()
    {
        var item = CreateBaseRod(RodNames.ROD_ALERTNESS, "Rod of Alertness",
            "This rod provides the following abilities:\n" +
            "• +1 insight bonus to Initiative and Listen checks\n" +
            "• Light at will (command word)\n" +
            "• Detect Evil at will (command word)\n" +
            "• Detect Magic at will (command word)\n" +
            "• See Invisible at will (command word)\n" +
            "• Animate Objects 1/day (rod becomes +1 defending longsword)\n" +
            "• Prayer 1/day (30 ft radius)\n" +
            "The rod grants the Alertness feat while held.",
            RodCategory.Legendary, 85000, 11);

        item.RodIsAlertness = true;
        item.RodInsightBonusInit = 1;
        item.RodInsightBonusListen = 1;
        item.RodGrantsSeeInvisible = true;
        item.RodGrantsDetectEvil = true;
        item.RodGrantsDetectMagic = true;
        item.RodGrantsLight = true;
        item.RodAnimateUsesPerDay = 1;
        item.RodAnimateUsesToday = 0;
        item.RodPrayerUsesPerDay = 1;
        item.RodPrayerUsesToday = 0;
        item.IsLegendary = true;

        return item;
    }

    /// <summary>
    /// Rod of Enemy Detection (DMG p.225) — 23,500 gp, CL 10th.
    /// Detect enemies within 60 ft (even invisible/ethereal), 3 charges/day.
    /// Penetrates 20 ft of stone.
    /// </summary>
    public static ItemData CreateRodOfEnemyDetection()
    {
        var item = CreateBaseRod(RodNames.ROD_ENEMY_DETECTION, "Rod of Enemy Detection",
            "This rod pulses in the hand when hostile creatures are within 60 feet. " +
            "It detects enemies even if they are invisible, hidden, or ethereal. " +
            "The detection penetrates up to 20 feet of stone. Usable 3 times per day.",
            RodCategory.Utility, 23500, 10);

        item.RodCanDetectEnemies = true;
        item.RodDetectionRadiusFt = 60f;
        item.RodPenetratesStoneFt = 20f;
        item.RodUsesPerDay = 3;
        item.RodUsesToday = 0;

        return item;
    }

    /// <summary>
    /// Rod of Negation (DMG p.226) — 37,000 gp, CL 15th.
    /// Dispel Magic at will (CL 15), Greater Dispel Magic 2/day.
    /// </summary>
    public static ItemData CreateRodOfNegation()
    {
        var item = CreateBaseRod(RodNames.ROD_NEGATION, "Rod of Negation",
            "This rod allows the wielder to negate the spell or spell-like function of " +
            "magic items. The wielder can cast Dispel Magic at will (CL 15th) and " +
            "Greater Dispel Magic 2 times per day.",
            RodCategory.Utility, 37000, 15);

        item.RodIsNegation = true;
        item.RodDispelCL = 15;
        item.RodGreaterDispelUsesPerDay = 2;
        item.RodGreaterDispelUsesToday = 0;

        return item;
    }

    /// <summary>
    /// Rod of Python (DMG p.226) — 13,000 gp, CL 10th.
    /// Transform into giant constrictor snake: 60 HP, AC 15, +13 attack, 1d3+10 damage.
    /// Snake has constrict ability (grapple + damage). Follows wielder's commands.
    /// </summary>
    public static ItemData CreateRodOfPython()
    {
        var item = CreateBaseRod(RodNames.ROD_PYTHON, "Rod of Python",
            "This rod can transform into a giant constrictor snake on command. " +
            "The snake has 60 HP, AC 15, attacks at +13 for 1d3+10 damage, " +
            "and can constrict grappled foes for 1d3+10 additional damage. " +
            "The snake follows the wielder's commands and returns to rod form when ordered.",
            RodCategory.Utility, 13000, 10);

        item.RodCanTransformToSnake = true;
        item.RodIsInSnakeForm = false;
        item.RodSnakeHP = 60;
        item.RodSnakeMaxHP = 60;
        item.RodSnakeAC = 15;
        item.RodSnakeAttackBonus = 13;
        item.RodSnakeDamage = "1d3+10";
        item.RodSnakeHasConstrict = true;
        item.RodSnakeConstrictDamage = "1d3+10";

        return item;
    }

    /// <summary>
    /// Rod of Security (DMG p.226) — 61,000 gp, CL 20th.
    /// Creates an extradimensional paradise demiplane.
    /// Capacity: 200 people, Duration: 200 days (divided by guests + wielder).
    /// Complete rest and healing. 1/week.
    /// </summary>
    public static ItemData CreateRodOfSecurity()
    {
        var item = CreateBaseRod(RodNames.ROD_SECURITY, "Rod of Security",
            "This rod creates a nondimensional space — a paradise demiplane. " +
            "The rod can hold up to 200 creatures in comfort. Time passes at the same " +
            "rate inside and outside. The total stay cannot exceed 200 person-days " +
            "(e.g., 200 creatures for 1 day, or 1 creature for 200 days). " +
            "Creatures inside heal completely. Usable 1 per week.",
            RodCategory.Legendary, 61000, 20);

        item.RodCanCreateDemiplane = true;
        item.RodDemiplaneCapacity = 200;
        item.RodDemiplanePersonDays = 200;
        item.RodDemiplaneHeals = true;
        item.RodDemiplaneUsesPerWeek = 1;
        item.RodDemiplaneUsesThisWeek = 0;
        item.IsLegendary = true;

        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  REGISTER ALL — Called by RodDatabase.Init()
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates all 36 rod ItemData instances for registration.
    /// </summary>
    public static List<ItemData> CreateAllRods()
    {
        var rods = new List<ItemData>(36);

        // ── 21 Metamagic Rods ─────────────────────────────────
        rods.Add(CreateRodOfEmpowerLesser());
        rods.Add(CreateRodOfEmpowerNormal());
        rods.Add(CreateRodOfEmpowerGreater());

        rods.Add(CreateRodOfEnlargeLesser());
        rods.Add(CreateRodOfEnlargeNormal());
        rods.Add(CreateRodOfEnlargeGreater());

        rods.Add(CreateRodOfExtendLesser());
        rods.Add(CreateRodOfExtendNormal());
        rods.Add(CreateRodOfExtendGreater());

        rods.Add(CreateRodOfMaximizeLesser());
        rods.Add(CreateRodOfMaximizeNormal());
        rods.Add(CreateRodOfMaximizeGreater());

        rods.Add(CreateRodOfQuickenLesser());
        rods.Add(CreateRodOfQuickenNormal());
        rods.Add(CreateRodOfQuickenGreater());

        rods.Add(CreateRodOfSilentLesser());
        rods.Add(CreateRodOfSilentNormal());
        rods.Add(CreateRodOfSilentGreater());

        rods.Add(CreateRodOfWidenLesser());
        rods.Add(CreateRodOfWidenNormal());
        rods.Add(CreateRodOfWidenGreater());

        // ── 7 Combat Rods ─────────────────────────────────────
        rods.Add(CreateRodOfAbsorption());
        rods.Add(CreateRodOfCancellation());
        rods.Add(CreateRodOfFlailing());
        rods.Add(CreateImmovableRod());
        rods.Add(CreateRodOfLordlyMight());
        rods.Add(CreateRodOfMetalAndMineralDetection());
        rods.Add(CreateRodOfSplendor());

        // ── 5 Utility Rods ────────────────────────────────────
        rods.Add(CreateRodOfAlertness());
        rods.Add(CreateRodOfEnemyDetection());
        rods.Add(CreateRodOfNegation());
        rods.Add(CreateRodOfPython());
        rods.Add(CreateRodOfSecurity());

        return rods;
    }
}
