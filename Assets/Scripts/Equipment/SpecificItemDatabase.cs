using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ============================================================================
// D&D 3.5e Specific Item Database
// Complete registry of all named magic items from DMG p.220-232.
// Data-driven: item properties are declarative, combat effects resolved at runtime.
// ============================================================================

/// <summary>
/// Central registry for all specific named magic items from the DMG 3.5e.
/// Initialized after ItemDatabase and EnchantmentProperties are ready.
/// </summary>
public static class SpecificItemDatabase
{
    private static Dictionary<SpecificItemType, SpecificItemDefinition> _items
        = new Dictionary<SpecificItemType, SpecificItemDefinition>();
    private static bool _initialized = false;

    // ========================================================================
    //  PUBLIC API
    // ========================================================================

    /// <summary>Initialize the specific item database. Safe to call multiple times.</summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        _items.Clear();

        RegisterSpecificWeapons();
        RegisterSpecificArmors();
        RegisterSpecificShields();

        Debug.Log($"[SpecificItemDatabase] Initialized with {_items.Count} specific items.");
    }

    /// <summary>Get a specific item definition by type.</summary>
    public static SpecificItemDefinition Get(SpecificItemType type)
    {
        if (!_initialized) Initialize();
        return _items.TryGetValue(type, out var def) ? def : null;
    }

    /// <summary>Get all registered specific item definitions.</summary>
    public static IEnumerable<SpecificItemDefinition> GetAll()
    {
        if (!_initialized) Initialize();
        return _items.Values;
    }

    /// <summary>Get all specific items of a given item category.</summary>
    public static List<SpecificItemDefinition> GetByCategory(ItemType category)
    {
        if (!_initialized) Initialize();
        return _items.Values.Where(d => d.ItemCategory == category).ToList();
    }

    /// <summary>Get all specific items at or below a given priority tier.</summary>
    public static List<SpecificItemDefinition> GetByPriority(int maxTier)
    {
        if (!_initialized) Initialize();
        return _items.Values.Where(d => d.PriorityTier <= maxTier).ToList();
    }

    /// <summary>
    /// Create an ItemData instance for a specific item. Clones the base item from
    /// ItemDatabase, applies enhancement/enchantments, and sets specific item metadata.
    /// Returns null if the base item is not found.
    /// </summary>
    public static ItemData CreateSpecificItem(SpecificItemType type)
    {
        var def = Get(type);
        if (def == null)
        {
            Debug.LogWarning($"[SpecificItemDatabase] Unknown specific item type: {type}");
            return null;
        }

        // Clone base item
        #pragma warning disable CS0618
        ItemData item = ItemDatabase.CloneItem(def.BaseItemId);
        #pragma warning restore CS0618

        if (item == null)
        {
            Debug.LogWarning($"[SpecificItemDatabase] Base item '{def.BaseItemId}' not found for {def.Name}");
            return null;
        }

        // Apply enhancement bonus
        if (def.EnhancementBonus > 0)
        {
            item.EnhancementBonus = def.EnhancementBonus;
            item.IsMasterwork = true;
        }

        // Apply material override
        if (def.MaterialOverride != ItemMaterialType.Standard)
        {
            if (def.ItemCategory == ItemType.Weapon || def.ItemCategory == ItemType.Ammunition)
                item.Material = MaterialProperties.GetWeaponMaterial(def.MaterialOverride, item);
            else
                item.Material = MaterialProperties.GetArmorMaterial(def.MaterialOverride, item);
            item.IsMasterwork = true; // All special materials are masterwork
        }

        // Apply standard enchantments
        if (def.StandardEnchantments != null && def.StandardEnchantments.Count > 0)
        {
            if (item.Enchantment == null)
                item.Enchantment = new ItemEnchantmentData();
            foreach (var ench in def.StandardEnchantments)
            {
                item.Enchantment.AddAbility(ench);
            }
        }

        // Set specific item metadata
        item.Name = def.Name;
        item.Id = $"specific_{def.Type}";
        item.IsSpecificItem = true;
        item.SpecificItemType = def.Type;
        item.SpecificItemData = def;

        // Override price to DMG fixed price
        item.BasePriceGp = def.MarketPrice;

        return item;
    }

    // ========================================================================
    //  REGISTRATION HELPERS
    // ========================================================================

    private static void Register(SpecificItemDefinition def)
    {
        _items[def.Type] = def;
    }

    // ========================================================================
    //  SPECIFIC WEAPONS
    // ========================================================================

    private static void RegisterSpecificWeapons()
    {
        // ── Minor Weapons ──

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.SleepArrow,
            Name = "Sleep Arrow",
            Description = "This +1 arrow converts its damage to nonlethal on hit, and target must make DC 11 Will save or fall asleep.",
            BaseItemId = "Arrow",
            ItemCategory = ItemType.Ammunition,
            EnhancementBonus = 1,
            MarketPrice = 132,
            CasterLevel = 5,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["SleepDC"] = 11, ["SleepDuration"] = 10, ["SingleUse"] = true },
            ImplementationNotes = "Custom on-hit sleep effect. Single-use ammo."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.ScreamingBolt,
            Name = "Screaming Bolt",
            Description = "This +2 bolt emits a piercing scream on hit. All enemies within 20 ft must make DC 14 Will save or be shaken for 1 round.",
            BaseItemId = "Crossbow Bolt",
            ItemCategory = ItemType.Ammunition,
            EnhancementBonus = 2,
            MarketPrice = 267,
            CasterLevel = 5,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["FearDC"] = 14, ["FearRadius"] = 20, ["SingleUse"] = true },
            ImplementationNotes = "Custom AoE fear on hit. Single-use ammo."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.JavelinOfLightning,
            Name = "Javelin of Lightning",
            Description = "When thrown, this +1 javelin transforms into a 5d6 lightning bolt (120 ft line, Reflex DC 14 half). Consumed on use.",
            BaseItemId = "Javelin",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 1,
            MarketPrice = 1500,
            CasterLevel = 5,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = { ["LightningDamage"] = "5d6", ["LightningDC"] = 14, ["SingleUse"] = true },
            ImplementationNotes = "Custom throw → lightning bolt effect. Transforms on throw."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.SlayingArrow,
            Name = "Slaying Arrow",
            Description = "This +1 arrow is keyed to a specific creature type. On hit, the target must make a DC 20 Fortitude save or die instantly.",
            BaseItemId = "Arrow",
            ItemCategory = ItemType.Ammunition,
            EnhancementBonus = 1,
            MarketPrice = 2282,
            CasterLevel = 13,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = { ["DeathEffectDC"] = 20, ["CreatureTypeTarget"] = "Any", ["SingleUse"] = true },
            ImplementationNotes = "Custom death effect with creature type targeting."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.GreaterSlayingArrow,
            Name = "Greater Slaying Arrow",
            Description = "As slaying arrow but with DC 23 Fortitude save.",
            BaseItemId = "Arrow",
            ItemCategory = ItemType.Ammunition,
            EnhancementBonus = 1,
            MarketPrice = 4057,
            CasterLevel = 13,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["DeathEffectDC"] = 23, ["CreatureTypeTarget"] = "Any", ["SingleUse"] = true },
            ImplementationNotes = "Same as Slaying Arrow with higher DC."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.Shatterspike,
            Name = "Shatterspike",
            Description = "This +1 longsword grants +4 bonus on sunder attempts when wielded by someone with the Improved Sunder feat.",
            BaseItemId = "Longsword",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 1,
            MarketPrice = 4315,
            CasterLevel = 13,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["SunderBonus"] = 4, ["RequiresFeat"] = "Improved Sunder" },
            ImplementationNotes = "Custom sunder bonus. Low priority (sunder not fully implemented)."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.DaggerOfVenom,
            Name = "Dagger of Venom",
            Description = "This black +1 dagger can release a poison effect (Fort DC 14) once per day, dealing 1d10/1d10 Con damage.",
            BaseItemId = "Dagger",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 1,
            MarketPrice = 8302,
            CasterLevel = 5,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = { ["PoisonDC"] = 14, ["PoisonDamage"] = "1d10 Con/1d10 Con", ["UsesPerDay"] = 1 },
            ImplementationNotes = "Custom daily poison activation."
        });

        // ── Medium Weapons ──

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.TridentOfWarning,
            Name = "Trident of Warning",
            Description = "This +2 trident enables wielder to determine location, depth, kind, and number of aquatic predators within 680 feet (1 round to scan).",
            BaseItemId = "Trident",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 2,
            MarketPrice = 10115,
            CasterLevel = 7,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["DetectRange"] = 680, ["NoSurpriseAquatic"] = true },
            ImplementationNotes = "Niche aquatic detection. Low priority."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.AssassinsDagger,
            Name = "Assassin's Dagger",
            Description = "This wicked-looking, curved +2 dagger provides a +1 bonus to the DC of a Fortitude save forced by the death attack of an assassin.",
            BaseItemId = "Dagger",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 2,
            MarketPrice = 18302, // SRD: 18,302 gp (not 10,302)
            CasterLevel = 9,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = { ["DeathAttackDCBonus"] = 1 },
            ImplementationNotes = "Only adds +1 to Assassin class Death Attack DC. No poison ability."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.ShiftersSorrow,
            Name = "Shifter's Sorrow",
            Description = "This +1/+1 alchemical silver two-bladed sword deals +2d6 vs shapechangers. Shapechangers hit must make DC 15 Will save or revert to natural form.",
            BaseItemId = "Two-Bladed Sword",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 1,
            MarketPrice = 12780,
            CasterLevel = 15,
            MaterialOverride = ItemMaterialType.AlchemicalSilver,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["AntiShapechanger"] = true, ["BonusDamageVsShapechanger"] = "2d6", ["RevertFormDC"] = 15 },
            ImplementationNotes = "Custom anti-shapechanger effect."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.TridentOfFishCommand,
            Name = "Trident of Fish Command",
            Description = "This +1 trident can charm aquatic animals (up to 14 HD total) 3/day and allows communication with charmed creatures.",
            BaseItemId = "Trident",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 1,
            MarketPrice = 18650,
            CasterLevel = 7,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["CharmHDLimit"] = 14, ["UsesPerDay"] = 3 },
            ImplementationNotes = "Aquatic charm ability. Low priority."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.FlameTongue,
            Name = "Flame Tongue",
            Description = "This +1 flaming burst longsword can emit a fiery ray (4d6 fire, 30 ft range touch) once per day. Sheds bright light 40 ft when flaming.",
            BaseItemId = "Longsword",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 1,
            StandardEnchantments = new List<EnchantmentType> { EnchantmentType.FlamingBurst },
            MarketPrice = 20715,
            CasterLevel = 12,
            PriorityTier = 1,
            HasCustomBehavior = true,
            UniqueProperties = { ["FireRayDamage"] = "4d6", ["FireRayRange"] = 30, ["UsesPerDay"] = 1, ["LightRadius"] = 40 },
            ImplementationNotes = "Standard FlamingBurst + custom daily fire ray ranged touch attack."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.LuckBlade0,
            Name = "Luck Blade",
            Description = "This +2 short sword grants +1 luck bonus on all saving throws and allows one reroll per day. Contains 0 wishes.",
            BaseItemId = "Short Sword",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 2,
            MarketPrice = 22060,
            CasterLevel = 17,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = { ["LuckSaveBonus"] = 1, ["RerollsPerDay"] = 1, ["WishCharges"] = 0 },
            ImplementationNotes = "Custom luck save bonus + daily reroll."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.SwordOfSubtlety,
            Name = "Sword of Subtlety",
            Description = "This +1 short sword grants +4 bonus on attack and damage rolls when making a sneak attack.",
            BaseItemId = "Short Sword",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 1,
            MarketPrice = 22310,
            CasterLevel = 7,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = { ["SneakAttackBonus"] = 4 },
            ImplementationNotes = "Custom sneak attack bonus. Interacts with Rogue class."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.SwordOfThePlanes,
            Name = "Sword of the Planes",
            Description = "This longsword: +1 on Material, +2 on Elemental/vs Elementals, +3 Astral/Ethereal/vs natives, +4 other planes/vs Outsiders.",
            BaseItemId = "Longsword",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 1, // Base (Material Plane)
            MarketPrice = 22315,
            CasterLevel = 15,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = {
                ["MaterialPlaneBonus"] = 1, ["ElementalPlaneBonus"] = 2,
                ["BonusVsElementals"] = 2, ["AstralEtherealBonus"] = 3,
                ["OtherPlaneBonus"] = 4, ["BonusVsOutsiders"] = 4
            },
            ImplementationNotes = "Variable enhancement by plane and target creature type."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.NineLivesStealer,
            Name = "Nine Lives Stealer",
            Description = "This +2 longsword can kill on crit (Fort DC 20, 9 charges, no effect vs crit-immune). Evil; good wielders gain 2 negative levels.",
            BaseItemId = "Longsword",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 2,
            MarketPrice = 23057,
            CasterLevel = 13,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = { ["DeathOnCritDC"] = 20, ["ChargesRemaining"] = 9, ["EvilAligned"] = true, ["GoodWielderNegLevels"] = 2 },
            ImplementationNotes = "Custom critical hit death effect with charge tracking."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.Oathbow,
            Name = "Oathbow",
            Description = "This +2 composite longbow (+2 Str) allows designating one sworn enemy per day. +5 bonus and +2d6 damage vs sworn enemy; -1 penalty vs all others while enemy lives.",
            BaseItemId = "Composite Longbow",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 2,
            MarketPrice = 25600,
            CasterLevel = 15,
            PriorityTier = 1,
            HasCustomBehavior = true,
            UniqueProperties = {
                ["SwornEnemyBonus"] = 5, ["SwornEnemyDamage"] = "2d6",
                ["SwornEnemyCritMultiplier"] = 4,   // x4 instead of x3
                ["NonSwornMasterworkOnly"] = true,   // Only masterwork (no magic) vs non-sworn
                ["NonSwornPenalty"] = -1,             // -1 on all weapon attacks while oath active
                ["OathDurationDays"] = 7
            },
            ImplementationNotes = "Custom sworn enemy tracking. Masterwork only vs non-sworn. Elf-crafted."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.SwordOfLifeStealing,
            Name = "Sword of Life Stealing",
            Description = "This black +2 longsword bestows one negative level on critical hit vs living creatures. Wielder gains 1d6 temporary HP per negative level (24 hrs).",
            BaseItemId = "Longsword",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 2,
            MarketPrice = 25715,
            CasterLevel = 17,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = { ["NegLevelOnCrit"] = 1, ["TempHPPerLevel"] = "1d6", ["TempHPDuration"] = 24 },
            ImplementationNotes = "Custom critical hit negative level + temp HP."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.MaceOfTerror,
            Name = "Mace of Terror",
            Description = "This +2 heavy mace can invoke a 30-ft cone of fear 3/day (Will DC 16 or panicked for 1d4 rounds).",
            BaseItemId = "Heavy Mace",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 2,
            MarketPrice = 38552,
            CasterLevel = 13,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = { ["FearConeDC"] = 16, ["FearConeRange"] = 30, ["UsesPerDay"] = 3, ["PanicDuration"] = "1d4" },
            ImplementationNotes = "Custom fear cone ability. Uses existing panic/fear conditions."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.LifeDrinker,
            Name = "Life-Drinker",
            Description = "This +1 greataxe bestows 2 negative levels on living targets per hit. The wielder also gains 1 temporary negative level per hit (1 hr duration).",
            BaseItemId = "Greataxe",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 1,
            MarketPrice = 40320,
            CasterLevel = 13,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["TargetNegLevels"] = 2, ["SelfNegLevels"] = 1, ["SelfNegLevelDuration"] = 60 },
            ImplementationNotes = "Double-edged negative level weapon."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.SylvanScimitar,
            Name = "Sylvan Scimitar",
            Description = "This +3 scimitar grants the Cleave feat and deals +1d6 bonus damage when used outdoors in natural terrain.",
            BaseItemId = "Scimitar",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 3,
            MarketPrice = 47315,
            CasterLevel = 11,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = { ["GrantsCleave"] = true, ["OutdoorBonusDamage"] = "1d6" },
            ImplementationNotes = "Custom Cleave grant + terrain-conditional bonus."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.RapierOfPuncturing,
            Name = "Rapier of Puncturing",
            Description = "This +2 wounding rapier can make a touch attack 3/day dealing 1d6 Con damage (by blood drain). Crit-immune creatures are immune.",
            BaseItemId = "Rapier",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 2,
            StandardEnchantments = new List<EnchantmentType> { EnchantmentType.Wounding },
            MarketPrice = 50320,
            CasterLevel = 13,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = { ["ConDamage"] = "1d6", ["UsesPerDay"] = 3, ["TouchAttack"] = true, ["CritImmuneResists"] = true },
            ImplementationNotes = "Standard Wounding + 3/day touch attack for Con damage. No save — touch attack."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.SunBlade,
            Name = "Sun Blade",
            Description = "This +2 bastard sword functions as a short sword for size/finesse. +4 vs evil, double damage vs undead. Emits sunlight on command.",
            BaseItemId = "Bastard Sword",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 2,
            MarketPrice = 50335,
            CasterLevel = 10,
            PriorityTier = 1,
            HasCustomBehavior = true,
            UniqueProperties = {
                ["FinesseWeapon"] = true, ["ShortSwordWeight"] = true,
                ["BonusVsEvil"] = 4, ["DoubleDamageVsUndead"] = true,
                ["SunlightEmitter"] = true, ["LightRadius"] = 30
            },
            ImplementationNotes = "Custom variable bonus vs evil/undead + finesse override + light emission."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.FrostBrand,
            Name = "Frost Brand",
            Description = "This +3 frost greatsword absorbs the first 10 points of fire damage per round and extinguishes nonmagical fires. Can dispel fire spells.",
            BaseItemId = "Greatsword",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 3,
            StandardEnchantments = new List<EnchantmentType> { EnchantmentType.Frost },
            MarketPrice = 54475,
            CasterLevel = 14,
            PriorityTier = 1,
            HasCustomBehavior = true,
            UniqueProperties = {
                ["FireResistance"] = 10, ["ExtinguishFiresRadius"] = 20,
                ["DispelFireCheck"] = 14
            },
            ImplementationNotes = "Standard Frost + custom fire absorption + fire extinguish."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.DwarvenThrower,
            Name = "Dwarven Thrower",
            Description = "This +2 warhammer becomes +3 returning when thrown by a dwarf. 30 ft range, +1d8 thrown damage (+2d8 vs giants). Dwarf only.",
            BaseItemId = "Warhammer",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 2,
            MarketPrice = 60312,
            CasterLevel = 10,
            PriorityTier = 1,
            HasCustomBehavior = true,
            UniqueProperties = {
                ["DwarfOnly"] = true, ["ThrownEnhancement"] = 3,
                ["ThrowRange"] = 30, ["ThrownBonusDamage"] = "1d8",
                ["ThrownBonusDamageVsGiants"] = "2d8", ["Returning"] = true
            },
            ImplementationNotes = "Custom race-conditional bonuses + Returning + Throwing."
        });

        // ── Major Weapons ──

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.LuckBlade1,
            Name = "Luck Blade (1 wish)",
            Description = "As Luck Blade but with 1 remaining wish.",
            BaseItemId = "Short Sword",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 2,
            MarketPrice = 62360,
            CasterLevel = 17,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = { ["LuckSaveBonus"] = 1, ["RerollsPerDay"] = 1, ["WishCharges"] = 1 },
            ImplementationNotes = "As LuckBlade0 + 1 wish."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.MaceOfSmiting,
            Name = "Mace of Smiting",
            Description = "This +3 adamantine heavy mace has +5 enhancement vs constructs. Crit vs construct = instant destruction (no save). Crit vs outsider = x4 damage instead of x2.",
            BaseItemId = "Heavy Mace",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 3,
            MaterialOverride = ItemMaterialType.Adamantine,
            MarketPrice = 75312,
            CasterLevel = 11,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = {
                ["BonusVsConstructs"] = 5,
                ["CritDestroyConstruct"] = true,      // No save, instant destruction
                ["CritMultiplierVsOutsider"] = 4      // x4 instead of x2 on crit vs outsiders
            },
            ImplementationNotes = "Custom construct destruction on crit (no save) + outsider crit multiplier."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.LuckBlade2,
            Name = "Luck Blade (2 wishes)",
            Description = "As Luck Blade but with 2 remaining wishes.",
            BaseItemId = "Short Sword",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 2,
            MarketPrice = 102660,
            CasterLevel = 17,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["LuckSaveBonus"] = 1, ["RerollsPerDay"] = 1, ["WishCharges"] = 2 },
            ImplementationNotes = "As LuckBlade0 + 2 wishes."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.HolyAvenger,
            Name = "Holy Avenger",
            Description = "This +2 cold iron longsword becomes +5 holy in a paladin's hands. Grants SR (5 + paladin level) to wielder and adjacent allies. 1/round: greater dispel magic (area).",
            BaseItemId = "Longsword",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 2, // Base; becomes +5 for paladin
            MaterialOverride = ItemMaterialType.ColdIron,
            MarketPrice = 120630,
            CasterLevel = 18,
            PriorityTier = 1,
            HasCustomBehavior = true,
            UniqueProperties = {
                ["PaladinEnhancement"] = 5, ["PaladinHoly"] = true,
                ["GrantsSR"] = true, ["SRFormula"] = "5+PaladinLevel",
                ["GreaterDispelMagic"] = true, ["DispelType"] = "Area"
            },
            ImplementationNotes = "Flagship specific item. Paladin class-conditional bonuses."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.LuckBlade3,
            Name = "Luck Blade (3 wishes)",
            Description = "As Luck Blade but with 3 remaining wishes.",
            BaseItemId = "Short Sword",
            ItemCategory = ItemType.Weapon,
            EnhancementBonus = 2,
            MarketPrice = 142960,
            CasterLevel = 17,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["LuckSaveBonus"] = 1, ["RerollsPerDay"] = 1, ["WishCharges"] = 3 },
            ImplementationNotes = "As LuckBlade0 + 3 wishes."
        });

        // ── Material-only weapons (nonmagical) ──

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.SilverDaggerMW,
            Name = "Masterwork Silver Dagger",
            Description = "A masterwork dagger made of alchemical silver. Bypasses DR/silver but takes -1 damage penalty.",
            BaseItemId = "Dagger",
            ItemCategory = ItemType.Weapon,
            MaterialOverride = ItemMaterialType.AlchemicalSilver,
            MarketPrice = 322,
            PriorityTier = 3,
            ImplementationNotes = "Material variant only. Already supported."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.ColdIronLongswordMW,
            Name = "Masterwork Cold Iron Longsword",
            Description = "A masterwork longsword made of cold iron. Bypasses DR/cold iron.",
            BaseItemId = "Longsword",
            ItemCategory = ItemType.Weapon,
            MaterialOverride = ItemMaterialType.ColdIron,
            MarketPrice = 330,
            PriorityTier = 3,
            ImplementationNotes = "Material variant only. Already supported."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.AdamantineDagger,
            Name = "Adamantine Dagger",
            Description = "A nonmagical dagger made of adamantine. Masterwork quality, bypasses DR/adamantine.",
            BaseItemId = "Dagger",
            ItemCategory = ItemType.Weapon,
            MaterialOverride = ItemMaterialType.Adamantine,
            MarketPrice = 3002,
            PriorityTier = 3,
            ImplementationNotes = "Material variant only. Already supported."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.AdamantineBattleaxe,
            Name = "Adamantine Battleaxe",
            Description = "A nonmagical battleaxe made of adamantine. Masterwork quality, bypasses DR/adamantine.",
            BaseItemId = "Battleaxe",
            ItemCategory = ItemType.Weapon,
            MaterialOverride = ItemMaterialType.Adamantine,
            MarketPrice = 3010,
            PriorityTier = 3,
            ImplementationNotes = "Material variant only. Already supported."
        });
    }

    // ========================================================================
    //  SPECIFIC ARMORS
    // ========================================================================

    private static void RegisterSpecificArmors()
    {
        // ── Nonmagical Material Armors ──

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.MithralShirt,
            Name = "Mithral Shirt",
            Description = "A very light chain shirt made of mithral. AC +4, Max Dex +6, ACP 0, ASF 10%. Light armor.",
            BaseItemId = "Chain Shirt",
            ItemCategory = ItemType.Armor,
            MaterialOverride = ItemMaterialType.Mithral,
            MarketPrice = 1100,
            PriorityTier = 2,
            ImplementationNotes = "Material variant. Mithral chain shirt."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.DragonhidePlate,
            Name = "Dragonhide Plate",
            Description = "Full plate armor made from dragonhide. Druids can wear it without violating their armor restriction.",
            BaseItemId = "Full Plate",
            ItemCategory = ItemType.Armor,
            MarketPrice = 3300,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["DruidCompatible"] = true },
            ImplementationNotes = "Requires Dragonhide material type."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.ElvenChain,
            Name = "Elven Chain",
            Description = "Mithral chainmail treated as light armor. AC +5, Max Dex +4, ACP -2, ASF 20%.",
            BaseItemId = "Chainmail",
            ItemCategory = ItemType.Armor,
            MaterialOverride = ItemMaterialType.Mithral,
            MarketPrice = 4150,
            PriorityTier = 2,
            ImplementationNotes = "Material variant. Mithral chainmail."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.AdamantineBreastplate,
            Name = "Adamantine Breastplate",
            Description = "A nonmagical breastplate of adamantine providing DR 2/—.",
            BaseItemId = "Breastplate",
            ItemCategory = ItemType.Armor,
            MaterialOverride = ItemMaterialType.Adamantine,
            MarketPrice = 10200,
            PriorityTier = 3,
            ImplementationNotes = "Material variant. Already supported via Adamantine."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.DwarvenPlate,
            Name = "Dwarven Plate",
            Description = "This full plate is made of adamantine, giving its wearer DR 3/—. Nonmagical.",
            BaseItemId = "Full Plate",
            ItemCategory = ItemType.Armor,
            MaterialOverride = ItemMaterialType.Adamantine, // SRD: adamantine, NOT mithral
            MarketPrice = 16500,
            PriorityTier = 2,
            ImplementationNotes = "Adamantine full plate. DR comes from adamantine heavy armor."
        });

        // ── Magical Armors ──

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.RhinoHide,
            Name = "Rhino Hide",
            Description = "This +2 hide armor grants +2d6 extra damage on charge attacks.",
            BaseItemId = "Hide",
            ItemCategory = ItemType.Armor,
            EnhancementBonus = 2,
            MarketPrice = 5165,
            CasterLevel = 9,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = { ["ChargeBonusDamage"] = "2d6" },
            ImplementationNotes = "Custom charge damage bonus."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.BandedMailOfLuck,
            Name = "Banded Mail of Luck",
            Description = "This +3 banded mail allows the wearer to force an attacker to reroll a successful attack once per week.",
            BaseItemId = "Banded Mail",
            ItemCategory = ItemType.Armor,
            EnhancementBonus = 3,
            MarketPrice = 18900,
            CasterLevel = 12,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["RerollAttackPerWeek"] = 1 },
            ImplementationNotes = "Custom weekly attack reroll."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.CelestialArmor,
            Name = "Celestial Armor",
            Description = "This brilliant +3 chainmail is treated as light armor. Max Dex +8, ACP -2, ASF 15%. Grants fly spell 1/day.",
            BaseItemId = "Chainmail",
            ItemCategory = ItemType.Armor,
            EnhancementBonus = 3,
            MarketPrice = 22400,
            CasterLevel = 5,
            PriorityTier = 1,
            HasCustomBehavior = true,
            UniqueProperties = {
                ["TreatedAsLightArmor"] = true, ["MaxDexOverride"] = 8,
                ["ACPOverride"] = 2, ["ASFOverride"] = 15,
                ["FlyPerDay"] = 1
            },
            ImplementationNotes = "Custom light-armor override + daily Fly spell."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.PlateArmorOfTheDeep,
            Name = "Plate Armor of the Deep",
            Description = "This +1 full plate allows underwater breathing, unarmored swimming, and aquatic communication.",
            BaseItemId = "Full Plate",
            ItemCategory = ItemType.Armor,
            EnhancementBonus = 1,
            MarketPrice = 24650,
            CasterLevel = 11,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["WaterBreathing"] = true, ["UnarmoredSwim"] = true, ["SpeakAquatic"] = true },
            ImplementationNotes = "Niche aquatic abilities."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.BreastplateOfCommand,
            Name = "Breastplate of Command",
            Description = "This +2 breastplate grants +2 competence bonus on Charisma checks, Cha-based skills, Leadership score, and turning checks. Allies within 360 ft gain +1 morale vs fear.",
            BaseItemId = "Breastplate",
            ItemCategory = ItemType.Armor,
            EnhancementBonus = 2,
            MarketPrice = 25400,
            CasterLevel = 15,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = { ["CharismaBonus"] = 2, ["LeadershipBonus"] = 2, ["AlliedMoraleRadius"] = 360, ["AlliedMoraleBonus"] = 1 },
            ImplementationNotes = "Custom Charisma/leadership/morale bonuses."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.MithralFullPlateOfSpeed,
            Name = "Mithral Full Plate of Speed",
            Description = "This +1 mithral full plate grants haste effect for up to 10 rounds per day (free action activation). Medium armor, Max Dex +3, ACP -3.",
            BaseItemId = "Full Plate",
            ItemCategory = ItemType.Armor,
            EnhancementBonus = 1,
            MaterialOverride = ItemMaterialType.Mithral,
            MarketPrice = 26500,
            CasterLevel = 5,
            PriorityTier = 1,
            HasCustomBehavior = true,
            UniqueProperties = { ["HasteRoundsPerDay"] = 10 },
            ImplementationNotes = "Custom activatable haste with daily round tracking."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.DemonArmor,
            Name = "Demon Armor",
            Description = "This +4 full plate makes the wearer appear demonic. Grants claw attacks (1d10, +1 weapons) with contagion on hit (Fort DC 14). Non-evil gain 1 negative level.",
            BaseItemId = "Full Plate",
            ItemCategory = ItemType.Armor,
            EnhancementBonus = 4,
            MarketPrice = 52260,
            CasterLevel = 13,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = {
                ["ClawDamage"] = "1d10", ["ClawEnhancement"] = 1,
                ["ContagionDC"] = 14, ["EvilAligned"] = true,
                ["NonEvilNegativeLevel"] = 1
            },
            ImplementationNotes = "Custom claw attacks + contagion + alignment restriction."
        });

        // NOTE: "Plate Armor of Etherealness" is NOT in the 3.5e SRD specific items list.
        // In 3.5e, "Etherealness" is an armor enhancement (+49,000 gp modifier), not a specific item.
        // This entry is retained as a convenience item representing +1 full plate with the Etherealness enhancement.
        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.PlateArmorOfEtherealness,
            Name = "Plate Armor of Etherealness",
            Description = "This +1 full plate with the Etherealness enhancement allows the wearer to become ethereal (as ethereal jaunt) 1/day. NOT a standard SRD specific item.",
            BaseItemId = "Full Plate",
            ItemCategory = ItemType.Armor,
            EnhancementBonus = 1,
            StandardEnchantments = new List<EnchantmentType> { EnchantmentType.Etherealness },
            MarketPrice = 57150,
            CasterLevel = 13,
            PriorityTier = 3,
            ImplementationNotes = "NOT in 3.5e SRD specific items. Convenience entry for +1 full plate + Etherealness enhancement."
        });
    }

    // ========================================================================
    //  SPECIFIC SHIELDS
    // ========================================================================

    private static void RegisterSpecificShields()
    {
        // ── Nonmagical Material Shields ──

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.DarkwoodBuckler,
            Name = "Darkwood Buckler",
            Description = "A buckler made of darkwood. Weight 2.5 lbs, no armor check penalty.",
            BaseItemId = "Buckler",
            ItemCategory = ItemType.Shield,
            MaterialOverride = ItemMaterialType.Darkwood,
            MarketPrice = 205,
            PriorityTier = 3,
            ImplementationNotes = "Material variant. Darkwood buckler."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.DarkwoodShield,
            Name = "Darkwood Heavy Shield",
            Description = "A heavy wooden shield made of darkwood. Weight 5 lbs, no armor check penalty.",
            BaseItemId = "Heavy Wooden Shield",
            ItemCategory = ItemType.Shield,
            MaterialOverride = ItemMaterialType.Darkwood,
            MarketPrice = 257,
            PriorityTier = 3,
            ImplementationNotes = "Material variant. Darkwood heavy wooden shield."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.MithralHeavyShield,
            Name = "Mithral Heavy Shield",
            Description = "A heavy steel shield made of mithral. Weight 5 lbs, no armor check penalty.",
            BaseItemId = "Heavy Steel Shield",
            ItemCategory = ItemType.Shield,
            MaterialOverride = ItemMaterialType.Mithral,
            MarketPrice = 1020,
            PriorityTier = 3,
            ImplementationNotes = "Material variant. Mithral heavy steel shield."
        });

        // ── Magical Shields ──

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.CastersShield,
            Name = "Caster's Shield",
            Description = "This +1 light wooden shield has a strip for inscribing a single spell (up to 3rd level) as a scroll. 50% chance of a random 3rd-level scroll.",
            BaseItemId = "Light Wooden Shield",
            ItemCategory = ItemType.Shield,
            EnhancementBonus = 1,
            MarketPrice = 3153,
            CasterLevel = 6,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["ScrollSlotLevel"] = 3, ["HasRandomScroll"] = true },
            ImplementationNotes = "Custom scroll-slot mechanic."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.SpinedShield,
            Name = "Spined Shield",
            Description = "This +1 heavy steel shield can launch spines 3/day (+1 ranged, 120 ft, 1d10+1 damage). Spines regenerate daily.",
            BaseItemId = "Heavy Steel Shield",
            ItemCategory = ItemType.Shield,
            EnhancementBonus = 1,
            MarketPrice = 5580,
            CasterLevel = 6,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["SpineAttackBonus"] = 1, ["SpineRange"] = 120, ["SpineDamage"] = "1d10+1", ["UsesPerDay"] = 3 },
            ImplementationNotes = "Custom ranged spine attack."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.LionsShield,
            Name = "Lion's Shield",
            Description = "This +2 heavy steel shield shaped like a roaring lion. The lion head can bite 3/day as free action (2d6 damage).",
            BaseItemId = "Heavy Steel Shield",
            ItemCategory = ItemType.Shield,
            EnhancementBonus = 2,
            MarketPrice = 9170,
            CasterLevel = 10,
            PriorityTier = 2,
            HasCustomBehavior = true,
            UniqueProperties = { ["BiteDamage"] = "2d6", ["UsesPerDay"] = 3 },
            ImplementationNotes = "Custom bite attack ability."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.WingedShield,
            Name = "Winged Shield",
            Description = "This +3 heavy wooden shield grants fly spell 1/day.",
            BaseItemId = "Heavy Wooden Shield",
            ItemCategory = ItemType.Shield,
            EnhancementBonus = 3,
            MarketPrice = 17257,
            CasterLevel = 7,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["FlyPerDay"] = 1 },
            ImplementationNotes = "Custom daily Fly spell."
        });

        Register(new SpecificItemDefinition
        {
            Type = SpecificItemType.AbsorbingShield,
            Name = "Absorbing Shield",
            Description = "This +1 heavy steel shield with a black, light-absorbing surface can disintegrate a touched non-living object once every 2 days.",
            BaseItemId = "Heavy Steel Shield",
            ItemCategory = ItemType.Shield,
            EnhancementBonus = 1,
            MarketPrice = 50170,
            CasterLevel = 17,
            PriorityTier = 3,
            HasCustomBehavior = true,
            UniqueProperties = { ["DisintegrateObject"] = true, ["CooldownDays"] = 2 },
            ImplementationNotes = "Custom disintegrate-on-touch ability."
        });
    }
}
