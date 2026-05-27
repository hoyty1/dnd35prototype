using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Static registry for all D&D 3.5e wondrous items (DMG pp. 248–271).
/// Follows the same lazy initialization pattern as RingDatabase.
/// 
/// Phase 1 registers foundation items from all slot categories plus test items.
/// Future phases will add the remaining 100+ wondrous items.
/// 
/// Wondrous items are stored here AND registered in ItemDatabase for
/// standard inventory/equipment/loot/shop flow.
/// </summary>
public static class WondrousItemDatabase
{
    private static bool _initialized = false;
    private static Dictionary<string, ItemData> _items = new Dictionary<string, ItemData>();

    /// <summary>
    /// Initialize the wondrous item database. Idempotent — safe to call multiple times.
    /// Must be called before ItemDatabase.Init() so items are available for registration.
    /// </summary>
    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _items.Clear();

        // ════════════════════════════════════════════════════════════
        //  HEAD SLOT — Headbands, circlets, helms, hats
        // ════════════════════════════════════════════════════════════
        for (int bonus = 2; bonus <= 6; bonus += 2)
            Register(WondrousItemFactory.CreateHeadbandOfIntellect(bonus));
        Register(WondrousItemFactory.CreateCircletOfPersuasion());
        Register(WondrousItemFactory.CreateHatOfDisguise());

        // ════════════════════════════════════════════════════════════
        //  FACE/EYES SLOT — Goggles, lenses
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateGogglesOfNight());
        Register(WondrousItemFactory.CreateEyesOfTheEagle());

        // ════════════════════════════════════════════════════════════
        //  NECK/THROAT SLOT — Amulets, periapts, brooches
        // ════════════════════════════════════════════════════════════
        for (int bonus = 1; bonus <= 5; bonus++)
            Register(WondrousItemFactory.CreateAmuletOfNaturalArmor(bonus));
        for (int bonus = 2; bonus <= 6; bonus += 2)
            Register(WondrousItemFactory.CreateAmuletOfHealth(bonus));
        for (int bonus = 2; bonus <= 6; bonus += 2)
            Register(WondrousItemFactory.CreatePeriaptOfWisdom(bonus));
        Register(WondrousItemFactory.CreateBroochOfShielding());
        for (int bonus = 1; bonus <= 5; bonus++)
            Register(WondrousItemFactory.CreateAmuletOfMightyFists(bonus));

        // ════════════════════════════════════════════════════════════
        //  SHOULDERS/BACK SLOT — Cloaks, capes
        // ════════════════════════════════════════════════════════════
        for (int bonus = 1; bonus <= 5; bonus++)
            Register(WondrousItemFactory.CreateCloakOfResistance(bonus));
        for (int bonus = 2; bonus <= 6; bonus += 2)
            Register(WondrousItemFactory.CreateCloakOfCharisma(bonus));
        Register(WondrousItemFactory.CreateCloakOfElvenkind());
        Register(WondrousItemFactory.CreateCloakOfDisplacement(false)); // Minor: 20% miss chance
        Register(WondrousItemFactory.CreateCloakOfDisplacement(true));  // Major: 50% miss chance
        Register(WondrousItemFactory.CreateWingsOfFlying());

        // ════════════════════════════════════════════════════════════
        //  TORSO/BODY SLOT — Vests, robes, shirts
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateVestOfEscape());
        Register(WondrousItemFactory.CreateMonksBeltPhase10());

        // ════════════════════════════════════════════════════════════
        //  WAIST SLOT — Belts
        // ════════════════════════════════════════════════════════════
        for (int bonus = 2; bonus <= 6; bonus += 2)
            Register(WondrousItemFactory.CreateBeltOfGiantStrength(bonus));
        for (int bonus = 2; bonus <= 6; bonus += 2)
            Register(WondrousItemFactory.CreateBeltOfDexterity(bonus));

        // ════════════════════════════════════════════════════════════
        //  WRISTS/ARMS SLOT — Bracers
        // ════════════════════════════════════════════════════════════
        for (int bonus = 1; bonus <= 8; bonus++)
            Register(WondrousItemFactory.CreateBracersOfArmor(bonus));
        Register(WondrousItemFactory.CreateBracersOfArchery(false)); // Lesser
        Register(WondrousItemFactory.CreateBracersOfArchery(true));  // Greater

        // ════════════════════════════════════════════════════════════
        //  HANDS SLOT — Gloves, gauntlets
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateGauntletsOfOgrePower());
        for (int bonus = 2; bonus <= 6; bonus += 2)
            Register(WondrousItemFactory.CreateGlovesOfDexterity(bonus));
        Register(WondrousItemFactory.CreateGlovesOfSwimmingAndClimbing());

        // ════════════════════════════════════════════════════════════
        //  FEET SLOT — Boots, slippers
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateBootsOfSpeed());
        Register(WondrousItemFactory.CreateBootsOfElvenkind());
        Register(WondrousItemFactory.CreateBootsOfStridingAndSpringing());
        Register(WondrousItemFactory.CreateSlippersOfSpiderClimbing());
        Register(WondrousItemFactory.CreateBootsOfLevitation());
        Register(WondrousItemFactory.CreateWingedBoots());
        Register(WondrousItemFactory.CreateBootsOfTheWinterlands());
        Register(WondrousItemFactory.CreateBootsOfTeleportation());

        // ════════════════════════════════════════════════════════════
        //  SLOTLESS — Bags, pearls, stones
        // ════════════════════════════════════════════════════════════
        for (int type = 1; type <= 4; type++)
            Register(WondrousItemFactory.CreateBagOfHolding(type));
        Register(WondrousItemFactory.CreateHandyHaversack());
        Register(WondrousItemFactory.CreateEfficientQuiver());
        Register(WondrousItemFactory.CreateRopeOfClimbing());
        Register(WondrousItemFactory.CreatePortableHole());
        for (int level = 1; level <= 9; level++)
            Register(WondrousItemFactory.CreatePearlOfPower(level));
        Register(WondrousItemFactory.CreateStoneOfGoodLuck());

        // ════════════════════════════════════════════════════════════
        //  PHASE 7: COMBAT ITEMS — Necklaces, beads, bands
        // ════════════════════════════════════════════════════════════
        for (int type = 1; type <= 7; type++)
            Register(WondrousItemFactory.CreateNecklaceOfFireballs(type));
        Register(WondrousItemFactory.CreateBeadsOfForce());
        Register(WondrousItemFactory.CreateIronBandsOfBinding());

        // ════════════════════════════════════════════════════════════
        //  PHASE 8: SUMMONING ITEMS — Bags of Tricks, Gems, Figurines
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateBagOfTricks("gray"));
        Register(WondrousItemFactory.CreateBagOfTricks("rust"));
        Register(WondrousItemFactory.CreateBagOfTricks("tan"));
        Register(WondrousItemFactory.CreateElementalGem("air"));
        Register(WondrousItemFactory.CreateElementalGem("earth"));
        Register(WondrousItemFactory.CreateElementalGem("fire"));
        Register(WondrousItemFactory.CreateElementalGem("water"));
        Register(WondrousItemFactory.CreateFigurineOfWondrousPower("silver_raven"));
        Register(WondrousItemFactory.CreateFigurineOfWondrousPower("serpentine_owl"));
        Register(WondrousItemFactory.CreateFigurineOfWondrousPower("bronze_griffon"));
        Register(WondrousItemFactory.CreateFigurineOfWondrousPower("ebony_fly"));
        Register(WondrousItemFactory.CreateFigurineOfWondrousPower("onyx_dog"));
        Register(WondrousItemFactory.CreateFigurineOfWondrousPower("golden_lions"));
        Register(WondrousItemFactory.CreateFigurineOfWondrousPower("marble_elephant"));
        Register(WondrousItemFactory.CreateFigurineOfWondrousPower("obsidian_steed"));

        // ════════════════════════════════════════════════════════════
        //  PHASE 9: IOUN STONES — All 16 standard Ioun Stones (DMG pp. 260–261)
        // ════════════════════════════════════════════════════════════
        // Ability score stones (+2 enhancement)
        Register(WondrousItemFactory.CreateIounStoneDeepRedSphere());         // +2 Dex
        Register(WondrousItemFactory.CreateIounStoneIncandescentBlueSphere()); // +2 Wis
        Register(WondrousItemFactory.CreateIounStonePaleBlueRhomboid());      // +2 Str
        Register(WondrousItemFactory.CreateIounStonePinkRhomboid());          // +2 Con
        Register(WondrousItemFactory.CreateIounStonePinkAndGreenSphere());    // +2 Cha
        Register(WondrousItemFactory.CreateIounStoneScarletAndBlueSphere());  // +2 Int
        // Utility stones
        Register(WondrousItemFactory.CreateIounStoneClearSpindle());          // Sustains without food/water
        Register(WondrousItemFactory.CreateIounStoneDustyRosePrism());        // +1 insight AC
        Register(WondrousItemFactory.CreateIounStoneDarkBlueRhomboid());      // Alertness (feat)
        Register(WondrousItemFactory.CreateIounStoneVibrantPurplePrism());    // Stores 3 spell levels
        Register(WondrousItemFactory.CreateIounStoneIridescentSpindle());     // Sustains without air
        Register(WondrousItemFactory.CreateIounStonePaleLavenderEllipsoid()); // Absorbs spells ≤4th
        Register(WondrousItemFactory.CreateIounStonePearlyWhiteSpindle());    // Regenerate 1 HP/hour
        Register(WondrousItemFactory.CreateIounStoneOrangePrism());           // +1 caster level
        Register(WondrousItemFactory.CreateIounStonePaleGreenPrism());        // +1 competence to attacks/saves/checks
        Register(WondrousItemFactory.CreateIounStoneLavenderAndGreenEllipsoid()); // Absorbs spells ≤8th

        // ════════════════════════════════════════════════════════════
        //  PHASE 10: COMPLEX MULTI-ABILITY ITEMS (DMG pp. 248–271)
        // ════════════════════════════════════════════════════════════
        // Robes (Torso slot)
        Register(WondrousItemFactory.CreateRobeOfTheArchmagi("good"));
        Register(WondrousItemFactory.CreateRobeOfTheArchmagi("neutral"));
        Register(WondrousItemFactory.CreateRobeOfTheArchmagi("evil"));
        Register(WondrousItemFactory.CreateRobeOfStars());
        Register(WondrousItemFactory.CreateRobeOfScintillatingColors());
        Register(WondrousItemFactory.CreateRobeOfEyes());
        Register(WondrousItemFactory.CreateRobeOfBlending());
        Register(WondrousItemFactory.CreateRobeOfBones());
        Register(WondrousItemFactory.CreateRobeOfUsefulItems());
        Register(WondrousItemFactory.CreateVestmentOfFaith());

        // Cloaks (Back/Shoulders slot)
        Register(WondrousItemFactory.CreateCloakOfArachnida());
        Register(WondrousItemFactory.CreateCloakOfTheBat());

        // Helms (Head slot)
        Register(WondrousItemFactory.CreateHelmOfTelepathy());
        Register(WondrousItemFactory.CreateHelmOfTeleportation());
        Register(WondrousItemFactory.CreateHelmOfUnderwaterAction());
        Register(WondrousItemFactory.CreateHelmOfBrilliance());

        // Periapts & Scarab (Neck slot)
        Register(WondrousItemFactory.CreatePeriaptOfProofAgainstPoison());
        Register(WondrousItemFactory.CreatePeriaptOfHealth());
        Register(WondrousItemFactory.CreateScarabOfProtection());

        // Slotless complex items
        Register(WondrousItemFactory.CreateCubeOfForce());

        // ════════════════════════════════════════════════════════════
        //  PHASE 4/5: PLANAR TRAVEL ITEMS (DMG pp. 247–270)
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateAmuletOfThePlanes());   // 120,000 gp, Neck
        Register(WondrousItemFactory.CreateCubicGate());           // 164,000 gp, Slotless
        Register(WondrousItemFactory.CreateWellOfManyWorlds());    // 82,000 gp, Slotless
        Register(WondrousItemFactory.CreateCarpetOfFlying10x10()); // 60,000 gp, Slotless

        // ════════════════════════════════════════════════════════════
        //  PHASE 2: SPELL RESISTANCE — Mantle of Spell Resistance
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateMantleOfSpellResistance(13)); // SR 13, 90,000 gp
        Register(WondrousItemFactory.CreateMantleOfSpellResistance(15)); // SR 15, 121,000 gp
        Register(WondrousItemFactory.CreateMantleOfSpellResistance(17)); // SR 17, 157,000 gp
        Register(WondrousItemFactory.CreateMantleOfSpellResistance(19)); // SR 19, 198,000 gp
        Register(WondrousItemFactory.CreateMantleOfSpellResistance(21)); // SR 21, 250,000 gp

        // ════════════════════════════════════════════════════════════
        //  PHASE 3: LEGENDARY PROTECTION ITEMS
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateMantleOfFaith());              // +5 saves, 4 SLAs 1/day
        // Periapt of Proof Against Poison already registered in Phase 10 (Neck slot)
        // Scarab of Protection already registered in Phase 10 (Neck slot)

        // ════════════════════════════════════════════════════════════
        //  PHASE 6/7/8: CREATURE TRAPPING ITEMS
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateIronFlask());                         // 170,000 gp, Slotless
        Register(WondrousItemFactory.CreateEfreetiBottle());                     // 145,000 gp, Slotless
        Register(WondrousItemFactory.CreateStoneOfControllingEarthElementals()); // 100,000 gp, Slotless
        Register(WondrousItemFactory.CreateMirrorOfLifeTrapping());              // 200,000 gp, Slotless

        // ════════════════════════════════════════════════════════════
        //  PHASE 9/10: MIRROR ITEMS
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateMirrorOfOpposition());                // 92,000 gp, Slotless
        Register(WondrousItemFactory.CreateMirrorOfMentalProwess());             // 175,000 gp, Slotless

        // ════════════════════════════════════════════════════════════
        //  PHASE 11: CONSTRUCT GUARDIANS
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateIronCobra());                         // 80,000 gp, Slotless
        Register(WondrousItemFactory.CreateStoneHorseCourser());                 // 10,000 gp, Slotless
        Register(WondrousItemFactory.CreateStoneHorseDestrier());                // 14,800 gp, Slotless
        Register(WondrousItemFactory.CreateStoneHorseGriffon());                 // 28,500 gp, Slotless

        // ════════════════════════════════════════════════════════════
        //  PHASE 12: VEHICLE
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateApparatusOfKwalish());                // 90,000 gp, Slotless

        // ════════════════════════════════════════════════════════════
        //  PHASE 13: LEGENDARY TOOLS
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateMattockOfTheTitans());                // 23,348 gp, Slotless
        Register(WondrousItemFactory.CreateMaulOfTheTitans());                   // 25,305 gp, Slotless
        Register(WondrousItemFactory.CreateLyreOfBuilding());                    // 13,000 gp, Slotless
        Register(WondrousItemFactory.CreateHornOfValhallaIron());                // 50,000 gp, Slotless

        // ════════════════════════════════════════════════════════════
        //  TEST ITEMS — One per slot for verification
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Head, "Head"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.FaceEyes, "Face"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Neck, "Neck"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Back, "Back"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Torso, "Torso"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Waist, "Waist"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Wrists, "Wrists"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Hands, "Hands"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Feet, "Feet"));
        Register(WondrousItemFactory.CreateTestSlotlessItem());

        Debug.Log($"[WondrousItemDatabase] Initialized: {_items.Count} wondrous items registered (Phases 1–14, Major Wondrous Items COMPLETE).");
    }

    /// <summary>Register a test slotless item (delegates to factory).</summary>
    private static void RegisterTestSlotlessItem()
    {
        Register(WondrousItemFactory.CreateTestSlotlessItem());
    }

    /// <summary>Register a wondrous item in the database. Validates for duplicates.</summary>
    private static void Register(ItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("[WondrousItemDatabase] Attempted to register null item.");
            return;
        }

        string key = item.WondrousId;
        if (string.IsNullOrEmpty(key))
            key = item.Id;

        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning($"[WondrousItemDatabase] Item '{item.Name}' has no WondrousId or Id — skipping.");
            return;
        }

        if (_items.ContainsKey(key))
        {
            Debug.LogWarning($"[WondrousItemDatabase] Duplicate wondrous item ID: '{key}' — skipping '{item.Name}'.");
            return;
        }

        _items[key] = item;
    }

    /// <summary>
    /// Retrieve a wondrous item definition by its ID.
    /// Returns null if not found.
    /// </summary>
    public static ItemData GetItem(string wondrousId)
    {
        if (!_initialized) Init();
        if (string.IsNullOrEmpty(wondrousId)) return null;
        return _items.ContainsKey(wondrousId) ? _items[wondrousId] : null;
    }

    /// <summary>Get all registered wondrous item definitions (read-only snapshot).</summary>
    public static IReadOnlyDictionary<string, ItemData> GetAllItems()
    {
        if (!_initialized) Init();
        return _items;
    }

    /// <summary>Get all items for a specific equipment slot.</summary>
    public static List<ItemData> GetItemsBySlot(EquipSlot slot)
    {
        if (!_initialized) Init();
        var result = new List<ItemData>();
        foreach (var kvp in _items)
        {
            if (kvp.Value.WondrousRequiredSlot == slot || kvp.Value.Slot == slot)
                result.Add(kvp.Value);
        }
        return result;
    }

    /// <summary>Get all slotless items.</summary>
    public static List<ItemData> GetSlotlessItems()
    {
        if (!_initialized) Init();
        var result = new List<ItemData>();
        foreach (var kvp in _items)
        {
            if (kvp.Value.IsSlotless)
                result.Add(kvp.Value);
        }
        return result;
    }

    /// <summary>Get the count of registered wondrous items.</summary>
    public static int Count
    {
        get
        {
            if (!_initialized) Init();
            return _items.Count;
        }
    }

    /// <summary>
    /// Register all wondrous items into the main ItemDatabase so they appear in
    /// standard inventory/loot/shop systems. Call after ItemDatabase.Init().
    /// </summary>
    public static void RegisterAllInItemDatabase()
    {
        if (!_initialized) Init();

        int registered = 0;
        foreach (var kvp in _items)
        {
            ItemData existing = ItemDatabase.Get(kvp.Key);
            if (existing == null)
            {
                ItemDatabase.RegisterExternal(kvp.Value);
                registered++;
            }
        }

        Debug.Log($"[WondrousItemDatabase] Registered {registered} wondrous items in ItemDatabase.");
    }
}
