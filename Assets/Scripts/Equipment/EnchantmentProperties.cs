using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// D&D 3.5e Enchantment Properties - Central database for all enchantment data
// Phase 2: Foundation - Mirrors MaterialProperties pattern
// ============================================================================

/// <summary>
/// Central, authoritative database of all enchantment properties.
/// All enchantment data is defined HERE — no hardcoding in gameplay code.
/// Mirrors the MaterialProperties pattern: static lookup, switch-free, data-driven.
/// 
/// Usage:
///   EnchantmentProperties.Initialize();  // Call once at game start
///   EnchantmentStats stats = EnchantmentProperties.Get(EnchantmentType.Flaming);
/// </summary>
public static class EnchantmentProperties
{
    private static Dictionary<EnchantmentType, EnchantmentStats> _database;
    private static bool _initialized;

    // ========================================================================
    // PUBLIC API
    // ========================================================================

    /// <summary>Initialize the enchantment database. Safe to call multiple times.</summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _database = new Dictionary<EnchantmentType, EnchantmentStats>();
        RegisterAllEnchantments();
        _initialized = true;
        Debug.Log($"[EnchantmentProperties] Initialized with {_database.Count} enchantments.");
    }

    /// <summary>Get enchantment stats by type. Returns null if not found.</summary>
    public static EnchantmentStats Get(EnchantmentType type)
    {
        if (!_initialized) Initialize();
        return _database.TryGetValue(type, out var stats) ? stats : null;
    }

    /// <summary>Get display name for an enchantment type.</summary>
    public static string GetDisplayName(EnchantmentType type)
    {
        var stats = Get(type);
        return stats != null ? stats.DisplayName : type.ToString();
    }

    /// <summary>Get all registered enchantment stats.</summary>
    public static IEnumerable<EnchantmentStats> GetAll()
    {
        if (!_initialized) Initialize();
        return _database.Values;
    }

    /// <summary>Get all enchantments valid for a given slot.</summary>
    public static List<EnchantmentStats> GetForSlot(EnchantmentSlot slot)
    {
        if (!_initialized) Initialize();
        var result = new List<EnchantmentStats>();
        foreach (var kvp in _database)
        {
            if (kvp.Value.Slot == slot ||
                (slot == EnchantmentSlot.Armor && kvp.Value.Slot == EnchantmentSlot.ArmorOrShield) ||
                (slot == EnchantmentSlot.Shield && kvp.Value.Slot == EnchantmentSlot.ArmorOrShield))
            {
                result.Add(kvp.Value);
            }
        }
        return result;
    }

    // ========================================================================
    // REGISTRATION
    // ========================================================================

    private static void Register(EnchantmentStats stats)
    {
        if (_database.ContainsKey(stats.Type))
        {
            Debug.LogWarning($"[EnchantmentProperties] Duplicate registration for {stats.Type}, overwriting.");
        }
        _database[stats.Type] = stats;
    }

    // ========================================================================
    // ALL ENCHANTMENT DEFINITIONS
    // ========================================================================

    private static void RegisterAllEnchantments()
    {
        // --- Weapon: Elemental Damage ---
        RegisterWeaponElementalAbilities();

        // --- Weapon: Alignment Damage ---
        RegisterWeaponAlignmentAbilities();

        // --- Weapon: Critical Enhancement ---
        RegisterWeaponCriticalAbilities();

        // --- Weapon: Attack/Damage Modifiers ---
        RegisterWeaponAttackDamageAbilities();

        // --- Weapon: Speed ---
        RegisterWeaponSpeedAbilities();

        // --- Weapon: Thrown/Ranged ---
        RegisterWeaponThrownRangedAbilities();

        // --- Weapon: Defensive ---
        RegisterWeaponDefensiveAbilities();

        // --- Weapon: Spell-like ---
        RegisterWeaponSpellAbilities();

        // --- Weapon: Advanced (Phase 5-6) ---
        RegisterAdvancedWeaponAbilities();

        // --- Armor/Shield: Fortification ---
        RegisterFortificationAbilities();

        // --- Armor/Shield: Energy Resistance ---
        RegisterEnergyResistanceAbilities();

        // --- Armor/Shield: Skill Bonuses ---
        RegisterSkillBonusAbilities();

        // --- Armor/Shield: DR & SR ---
        RegisterDRAndSRAbilities();

        // --- Armor/Shield: Misc ---
        RegisterMiscArmorShieldAbilities();

        // --- Shield Specific ---
        RegisterShieldSpecificAbilities();

        // --- Advanced Armor/Shield (Phase 5-6) ---
        RegisterAdvancedArmorShieldAbilities();
    }

    // ========================================================================
    // WEAPON - ELEMENTAL DAMAGE
    // ========================================================================

    private static void RegisterWeaponElementalAbilities()
    {
        // Flaming: +1 bonus equivalent, +1d6 fire on every hit
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Flaming,
            DisplayName = "Flaming",
            Description = "Weapon is sheathed in fire, dealing +1d6 fire damage on each hit.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            ExtraDamageType = DamageType.Fire,
            ExtraDamageDice = 1,
            ExtraDamageDieSides = 6,
        });

        // Flaming Burst: +2 bonus, +1d6 fire + extra d10s on crit
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.FlamingBurst,
            DisplayName = "Flaming Burst",
            Description = "Weapon deals +1d6 fire damage on hit, plus extra fire damage on critical hits.",
            BonusEquivalent = 2,
            Slot = EnchantmentSlot.Weapon,
            ExtraDamageType = DamageType.Fire,
            ExtraDamageDice = 1,
            ExtraDamageDieSides = 6,
            CritBonusDice = 1,
            CritBonusDieSides = 10,
            CritDiceScaleWithMultiplier = true,
            IncompatibleWith = new List<EnchantmentType> { EnchantmentType.Flaming },
        });

        // Frost: +1 bonus, +1d6 cold on every hit
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Frost,
            DisplayName = "Frost",
            Description = "Weapon is rimed with frost, dealing +1d6 cold damage on each hit.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            ExtraDamageType = DamageType.Cold,
            ExtraDamageDice = 1,
            ExtraDamageDieSides = 6,
        });

        // Icy Burst: +2 bonus, +1d6 cold + extra d10s on crit
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.IcyBurst,
            DisplayName = "Icy Burst",
            Description = "Weapon deals +1d6 cold damage on hit, plus extra cold damage on critical hits.",
            BonusEquivalent = 2,
            Slot = EnchantmentSlot.Weapon,
            ExtraDamageType = DamageType.Cold,
            ExtraDamageDice = 1,
            ExtraDamageDieSides = 6,
            CritBonusDice = 1,
            CritBonusDieSides = 10,
            CritDiceScaleWithMultiplier = true,
            IncompatibleWith = new List<EnchantmentType> { EnchantmentType.Frost },
        });

        // Shock: +1 bonus, +1d6 electricity on every hit
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Shock,
            DisplayName = "Shock",
            Description = "Weapon crackles with electricity, dealing +1d6 electricity damage on each hit.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            ExtraDamageType = DamageType.Electricity,
            ExtraDamageDice = 1,
            ExtraDamageDieSides = 6,
        });

        // Shocking Burst: +2 bonus, +1d6 electricity + extra d10s on crit
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.ShockingBurst,
            DisplayName = "Shocking Burst",
            Description = "Weapon deals +1d6 electricity on hit, plus extra electricity damage on critical hits.",
            BonusEquivalent = 2,
            Slot = EnchantmentSlot.Weapon,
            ExtraDamageType = DamageType.Electricity,
            ExtraDamageDice = 1,
            ExtraDamageDieSides = 6,
            CritBonusDice = 1,
            CritBonusDieSides = 10,
            CritDiceScaleWithMultiplier = true,
            IncompatibleWith = new List<EnchantmentType> { EnchantmentType.Shock },
        });

        // Corrosive: +1 bonus, +1d6 acid on every hit
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Corrosive,
            DisplayName = "Corrosive",
            Description = "Weapon drips with acid, dealing +1d6 acid damage on each hit.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            ExtraDamageType = DamageType.Acid,
            ExtraDamageDice = 1,
            ExtraDamageDieSides = 6,
        });

        // Thundering: +1 bonus, +1d8 sonic on crit only
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Thundering,
            DisplayName = "Thundering",
            Description = "Weapon thunders on critical hits, dealing +1d8 sonic damage.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            ExtraDamageType = DamageType.Sonic,
            // No per-hit damage — only on crit
            CritBonusDice = 1,
            CritBonusDieSides = 8,
            CritDiceScaleWithMultiplier = true,
        });
    }

    // ========================================================================
    // WEAPON - ALIGNMENT DAMAGE
    // ========================================================================

    private static void RegisterWeaponAlignmentAbilities()
    {
        // Holy: +2 bonus, +2d6 vs evil, weapon gains Good alignment tag
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Holy,
            DisplayName = "Holy",
            Description = "Weapon is imbued with holy power, dealing +2d6 damage to evil creatures.",
            BonusEquivalent = 2,
            Slot = EnchantmentSlot.Weapon,
            IsAlignmentDamage = true,
            AlignmentDamageTargets = DamageBypassTag.Evil,   // Targets evil creatures
            AlignmentBypassTag = DamageBypassTag.Good,       // Weapon counts as Good for DR bypass
            AlignmentDamageDice = 2,
            AlignmentDamageDieSides = 6,
            IncompatibleWith = new List<EnchantmentType> { EnchantmentType.Unholy },
        });

        // Unholy: +2 bonus, +2d6 vs good, weapon gains Evil alignment tag
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Unholy,
            DisplayName = "Unholy",
            Description = "Weapon radiates unholy power, dealing +2d6 damage to good creatures.",
            BonusEquivalent = 2,
            Slot = EnchantmentSlot.Weapon,
            IsAlignmentDamage = true,
            AlignmentDamageTargets = DamageBypassTag.Good,
            AlignmentBypassTag = DamageBypassTag.Evil,
            AlignmentDamageDice = 2,
            AlignmentDamageDieSides = 6,
            IncompatibleWith = new List<EnchantmentType> { EnchantmentType.Holy },
        });

        // Axiomatic: +2 bonus, +2d6 vs chaotic, weapon gains Lawful alignment tag
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Axiomatic,
            DisplayName = "Axiomatic",
            Description = "Weapon embodies law, dealing +2d6 damage to chaotic creatures.",
            BonusEquivalent = 2,
            Slot = EnchantmentSlot.Weapon,
            IsAlignmentDamage = true,
            AlignmentDamageTargets = DamageBypassTag.Chaotic,
            AlignmentBypassTag = DamageBypassTag.Lawful,
            AlignmentDamageDice = 2,
            AlignmentDamageDieSides = 6,
            IncompatibleWith = new List<EnchantmentType> { EnchantmentType.Anarchic },
        });

        // Anarchic: +2 bonus, +2d6 vs lawful, weapon gains Chaotic alignment tag
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Anarchic,
            DisplayName = "Anarchic",
            Description = "Weapon embodies chaos, dealing +2d6 damage to lawful creatures.",
            BonusEquivalent = 2,
            Slot = EnchantmentSlot.Weapon,
            IsAlignmentDamage = true,
            AlignmentDamageTargets = DamageBypassTag.Lawful,
            AlignmentBypassTag = DamageBypassTag.Chaotic,
            AlignmentDamageDice = 2,
            AlignmentDamageDieSides = 6,
            IncompatibleWith = new List<EnchantmentType> { EnchantmentType.Axiomatic },
        });
    }

    // ========================================================================
    // WEAPON - CRITICAL ENHANCEMENT
    // ========================================================================

    private static void RegisterWeaponCriticalAbilities()
    {
        // Keen: +1 bonus, doubles threat range, slashing/piercing only
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Keen,
            DisplayName = "Keen",
            Description = "Weapon's threat range is doubled (applied before other modifiers).",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            MeleeOnly = true,
            RequiresSlashingOrPiercing = true,
            DoublesThreadRange = true,
        });

        // Vorpal: +5 bonus, decapitate on natural 20 confirmed crit, requires Keen
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Vorpal,
            DisplayName = "Vorpal",
            Description = "On a natural 20 confirmed critical hit, the target is decapitated (if applicable).",
            BonusEquivalent = 5,
            Slot = EnchantmentSlot.Weapon,
            MeleeOnly = true,
            RequiresSlashingOrPiercing = true,
            VorpalEffect = true,
            RequiredEnchantments = new List<EnchantmentType> { EnchantmentType.Keen },
        });
    }

    // ========================================================================
    // WEAPON - ATTACK/DAMAGE MODIFIERS
    // ========================================================================

    private static void RegisterWeaponAttackDamageAbilities()
    {
        // Vicious: +1 bonus, +2d6 to target, 1d6 to wielder
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Vicious,
            DisplayName = "Vicious",
            Description = "Weapon deals +2d6 damage to the target, but also deals 1d6 damage to the wielder.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            MeleeOnly = true,
            ViciousEffect = true,
            ViciousDamageDice = 2,
            ViciousDamageDieSides = 6,
            ViciousBacklashDice = 1,
            ViciousBacklashDieSides = 6,
        });

        // Wounding: +2 bonus, 1 CON damage per hit
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Wounding,
            DisplayName = "Wounding",
            Description = "Each hit deals 1 point of Constitution damage to the target.",
            BonusEquivalent = 2,
            Slot = EnchantmentSlot.Weapon,
            MeleeOnly = true,
            WoundingEffect = true,
        });

        // Bane (generic template — specific creature type set at creation time)
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Bane,
            DisplayName = "Bane",
            Description = "Weapon gains +2 enhancement bonus and deals +2d6 damage against a specific creature type.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            IsBane = true,
            BaneCreatureType = "", // Set per-instance by EnchantmentFactory
            BaneEnhancementBonus = 2,
            BaneDamageDice = 2,
            BaneDamageDieSides = 6,
        });

        // Merciful: +1 bonus, +1d6 nonlethal, can suppress to deal lethal
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.MercifulWeapon,
            DisplayName = "Merciful",
            Description = "Weapon deals +1d6 nonlethal damage. Can be suppressed to deal lethal damage normally.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            ExtraDamageDice = 1,
            ExtraDamageDieSides = 6,
            // Note: nonlethal/lethal toggle handled in combat logic
        });
    }

    // ========================================================================
    // WEAPON - SPEED
    // ========================================================================

    private static void RegisterWeaponSpeedAbilities()
    {
        // Speed: +3 bonus, extra attack at full BAB (as haste)
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Speed,
            DisplayName = "Speed",
            Description = "Weapon grants one extra attack per round at the wielder's full base attack bonus (as haste).",
            BonusEquivalent = 3,
            Slot = EnchantmentSlot.Weapon,
            GrantsExtraAttack = true,
        });
    }

    // ========================================================================
    // WEAPON - THROWN/RANGED
    // ========================================================================

    private static void RegisterWeaponThrownRangedAbilities()
    {
        // Throwing: +1 bonus, makes melee weapon throwable (10 ft range increment)
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Throwing,
            DisplayName = "Throwing",
            Description = "Allows a melee weapon to be thrown with a 10 ft. range increment. Uses STR for attack and damage.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            MeleeOnly = true,
            AllowsThrow = true,
            ThrowRangeIncrement = 10,
        });

        // Returning: +1 bonus, thrown weapon returns immediately
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Returning,
            DisplayName = "Returning",
            Description = "A thrown weapon with this ability returns to the thrower's hand immediately after the attack.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            // Can be applied to thrown weapons (natural or via Throwing enhancement)
            ReturnsWhenThrown = true,
        });

        // Distance: +1 bonus, doubles range increment
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Distance,
            DisplayName = "Distance",
            Description = "Doubles the weapon's range increment.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            RangedOnly = true,
            DoublesRange = true,
        });

        // Seeking: +1 bonus, negates concealment penalties
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Seeking,
            DisplayName = "Seeking",
            Description = "Weapon negates any miss chance from concealment (but not total concealment).",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            RangedOnly = true,
            NegatesConcealment = true,
        });
    }

    // ========================================================================
    // WEAPON - DEFENSIVE
    // ========================================================================

    private static void RegisterWeaponDefensiveAbilities()
    {
        // Defending: +1 bonus, transfer weapon enhancement to AC
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Defending,
            DisplayName = "Defending",
            Description = "Wielder can transfer some or all of the weapon's enhancement bonus to AC as a free action.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            MeleeOnly = true,
            DefendingEffect = true,
        });
    }

    // ========================================================================
    // WEAPON - SPELL-LIKE
    // ========================================================================

    private static void RegisterWeaponSpellAbilities()
    {
        // Spell Storing: +1 bonus, store up to 3rd level spell
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.SpellStoring,
            DisplayName = "Spell Storing",
            Description = "Weapon can store a spell of up to 3rd level, released on a successful hit.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            MeleeOnly = true,
            CanStoreSpell = true,
            MaxStoredSpellLevel = 3,
        });
    }

    // ========================================================================
    // WEAPON - ADVANCED ABILITIES (Phase 5-6)
    // ========================================================================

    private static void RegisterAdvancedWeaponAbilities()
    {
        // Brilliant Energy: +4 bonus, ignores armor/shield/natural armor
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.BrilliantEnergy,
            DisplayName = "Brilliant Energy",
            Description = "Weapon ignores armor, shield, and natural armor bonuses to AC. Cannot harm undead, constructs, or objects.",
            BonusEquivalent = 4,
            Slot = EnchantmentSlot.Weapon,
            BrilliantEnergyEffect = true,
        });

        // Dancing: +4 bonus, weapon fights on its own for 4 rounds
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Dancing,
            DisplayName = "Dancing",
            Description = "As a free action, release the weapon to fight on its own for 4 rounds, then returns to hand.",
            BonusEquivalent = 4,
            Slot = EnchantmentSlot.Weapon,
            MeleeOnly = true,
            DancingEffect = true,
            DancingDuration = 4,
        });

        // Disruption: +2 bonus, undead must Fort DC 14 or be destroyed
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Disruption,
            DisplayName = "Disruption",
            Description = "Undead hit by this weapon must succeed on a DC 14 Fortitude save or be destroyed.",
            BonusEquivalent = 2,
            Slot = EnchantmentSlot.Weapon,
            BludgeoningOnly = true,
            DisruptionEffect = true,
            DisruptionSaveDC = 14,
        });

        // Ki Focus: +1 bonus, allows monk ki attacks through weapon
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.KiFocus,
            DisplayName = "Ki Focus",
            Description = "Allows a monk to use ki strike abilities and unarmed attack feats through this weapon.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            MeleeOnly = true,
            KiFocusEffect = true,
        });

        // Ghost Touch (weapon): +1 bonus, full damage vs incorporeal
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.GhostTouchWeapon,
            DisplayName = "Ghost Touch",
            Description = "Weapon deals full damage to incorporeal creatures as if they were corporeal.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Weapon,
            GhostTouchWeaponEffect = true,
        });
    }

    // ========================================================================
    // ARMOR/SHIELD - FORTIFICATION
    // ========================================================================

    private static void RegisterFortificationAbilities()
    {
        // Light Fortification: +1 bonus, 25% chance to negate crits/sneak attack
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.FortificationLight,
            DisplayName = "Fortification, Light",
            Description = "25% chance that a critical hit or sneak attack is negated and damage is rolled normally.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.ArmorOrShield,
            FortificationPercent = 25,
            IncompatibleWith = new List<EnchantmentType>
            {
                EnchantmentType.FortificationModerate,
                EnchantmentType.FortificationHeavy,
            },
        });

        // Moderate Fortification: +3 bonus, 50% chance
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.FortificationModerate,
            DisplayName = "Fortification, Moderate",
            Description = "50% chance that a critical hit or sneak attack is negated and damage is rolled normally.",
            BonusEquivalent = 3,
            Slot = EnchantmentSlot.ArmorOrShield,
            FortificationPercent = 50,
            IncompatibleWith = new List<EnchantmentType>
            {
                EnchantmentType.FortificationLight,
                EnchantmentType.FortificationHeavy,
            },
        });

        // Heavy Fortification: +5 bonus, 75% chance
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.FortificationHeavy,
            DisplayName = "Fortification, Heavy",
            Description = "75% chance that a critical hit or sneak attack is negated and damage is rolled normally.",
            BonusEquivalent = 5,
            Slot = EnchantmentSlot.ArmorOrShield,
            FortificationPercent = 75,
            IncompatibleWith = new List<EnchantmentType>
            {
                EnchantmentType.FortificationLight,
                EnchantmentType.FortificationModerate,
            },
        });
    }

    // ========================================================================
    // ARMOR/SHIELD - ENERGY RESISTANCE
    // ========================================================================

    private static void RegisterEnergyResistanceAbilities()
    {
        // Base Energy Resistance: +1 bonus equivalent, resist 10
        RegisterEnergyResistanceTier(EnchantmentType.EnergyResistanceFire, "Energy Resistance (Fire)", DamageType.Fire, 10, 1);
        RegisterEnergyResistanceTier(EnchantmentType.EnergyResistanceCold, "Energy Resistance (Cold)", DamageType.Cold, 10, 1);
        RegisterEnergyResistanceTier(EnchantmentType.EnergyResistanceElectricity, "Energy Resistance (Electricity)", DamageType.Electricity, 10, 1);
        RegisterEnergyResistanceTier(EnchantmentType.EnergyResistanceAcid, "Energy Resistance (Acid)", DamageType.Acid, 10, 1);
        RegisterEnergyResistanceTier(EnchantmentType.EnergyResistanceSonic, "Energy Resistance (Sonic)", DamageType.Sonic, 10, 1);

        // Improved Energy Resistance: +2 bonus equivalent, resist 20
        RegisterEnergyResistanceTier(EnchantmentType.ImprovedEnergyResistanceFire, "Improved Energy Resistance (Fire)", DamageType.Fire, 20, 2);
        RegisterEnergyResistanceTier(EnchantmentType.ImprovedEnergyResistanceCold, "Improved Energy Resistance (Cold)", DamageType.Cold, 20, 2);
        RegisterEnergyResistanceTier(EnchantmentType.ImprovedEnergyResistanceElectricity, "Improved Energy Resistance (Electricity)", DamageType.Electricity, 20, 2);
        RegisterEnergyResistanceTier(EnchantmentType.ImprovedEnergyResistanceAcid, "Improved Energy Resistance (Acid)", DamageType.Acid, 20, 2);
        RegisterEnergyResistanceTier(EnchantmentType.ImprovedEnergyResistanceSonic, "Improved Energy Resistance (Sonic)", DamageType.Sonic, 20, 2);

        // Greater Energy Resistance: +3 bonus equivalent, resist 30
        RegisterEnergyResistanceTier(EnchantmentType.GreaterEnergyResistanceFire, "Greater Energy Resistance (Fire)", DamageType.Fire, 30, 3);
        RegisterEnergyResistanceTier(EnchantmentType.GreaterEnergyResistanceCold, "Greater Energy Resistance (Cold)", DamageType.Cold, 30, 3);
        RegisterEnergyResistanceTier(EnchantmentType.GreaterEnergyResistanceElectricity, "Greater Energy Resistance (Electricity)", DamageType.Electricity, 30, 3);
        RegisterEnergyResistanceTier(EnchantmentType.GreaterEnergyResistanceAcid, "Greater Energy Resistance (Acid)", DamageType.Acid, 30, 3);
        RegisterEnergyResistanceTier(EnchantmentType.GreaterEnergyResistanceSonic, "Greater Energy Resistance (Sonic)", DamageType.Sonic, 30, 3);
    }

    private static void RegisterEnergyResistanceTier(EnchantmentType type, string name, DamageType damageType, int amount, int bonusEquiv)
    {
        // Build incompatibility list: can't have two tiers of the same element
        var incompatible = new List<EnchantmentType>();
        // Find all other energy resistance of the same element
        string element = damageType.ToString();

        Register(new EnchantmentStats
        {
            Type = type,
            DisplayName = name,
            Description = $"Grants resistance {amount} against {element.ToLower()} damage.",
            BonusEquivalent = bonusEquiv,
            Slot = EnchantmentSlot.ArmorOrShield,
            ResistanceDamageType = damageType,
            ResistanceAmount = amount,
        });
    }

    // ========================================================================
    // ARMOR/SHIELD - SKILL BONUSES
    // ========================================================================

    private static void RegisterSkillBonusAbilities()
    {
        // Shadow: +1/+1/+1 bonus, +5/+10/+15 to Hide
        RegisterSkillBonus(EnchantmentType.Shadow, "Shadow", "Hide", 5, 1,
            new List<EnchantmentType> { EnchantmentType.ImprovedShadow, EnchantmentType.GreaterShadow });
        RegisterSkillBonus(EnchantmentType.ImprovedShadow, "Improved Shadow", "Hide", 10, 2,
            new List<EnchantmentType> { EnchantmentType.Shadow, EnchantmentType.GreaterShadow });
        RegisterSkillBonus(EnchantmentType.GreaterShadow, "Greater Shadow", "Hide", 15, 3,
            new List<EnchantmentType> { EnchantmentType.Shadow, EnchantmentType.ImprovedShadow });

        // Silent Moves: +1/+1/+1 bonus, +5/+10/+15 to Move Silently
        RegisterSkillBonus(EnchantmentType.SilentMoves, "Silent Moves", "Move Silently", 5, 1,
            new List<EnchantmentType> { EnchantmentType.ImprovedSilentMoves, EnchantmentType.GreaterSilentMoves });
        RegisterSkillBonus(EnchantmentType.ImprovedSilentMoves, "Improved Silent Moves", "Move Silently", 10, 2,
            new List<EnchantmentType> { EnchantmentType.SilentMoves, EnchantmentType.GreaterSilentMoves });
        RegisterSkillBonus(EnchantmentType.GreaterSilentMoves, "Greater Silent Moves", "Move Silently", 15, 3,
            new List<EnchantmentType> { EnchantmentType.SilentMoves, EnchantmentType.ImprovedSilentMoves });

        // Slick: +1/+1/+1 bonus, +5/+10/+15 to Escape Artist
        RegisterSkillBonus(EnchantmentType.SlickArmor, "Slick", "Escape Artist", 5, 1,
            new List<EnchantmentType> { EnchantmentType.ImprovedSlick, EnchantmentType.GreaterSlick });
        RegisterSkillBonus(EnchantmentType.ImprovedSlick, "Improved Slick", "Escape Artist", 10, 2,
            new List<EnchantmentType> { EnchantmentType.SlickArmor, EnchantmentType.GreaterSlick });
        RegisterSkillBonus(EnchantmentType.GreaterSlick, "Greater Slick", "Escape Artist", 15, 3,
            new List<EnchantmentType> { EnchantmentType.SlickArmor, EnchantmentType.ImprovedSlick });
    }

    private static void RegisterSkillBonus(EnchantmentType type, string name, string skill, int bonus, int bonusEquiv, List<EnchantmentType> incompatible)
    {
        Register(new EnchantmentStats
        {
            Type = type,
            DisplayName = name,
            Description = $"Grants +{bonus} competence bonus on {skill} checks.",
            BonusEquivalent = bonusEquiv,
            Slot = EnchantmentSlot.ArmorOrShield,
            SkillBonus = bonus,
            SkillBonusTarget = skill,
            IncompatibleWith = incompatible,
        });
    }

    // ========================================================================
    // ARMOR/SHIELD - DR & SPELL RESISTANCE
    // ========================================================================

    private static void RegisterDRAndSRAbilities()
    {
        // Invulnerability: +3 bonus, DR 5/magic
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Invulnerability,
            DisplayName = "Invulnerability",
            Description = "Armor grants DR 5/magic.",
            BonusEquivalent = 3,
            Slot = EnchantmentSlot.Armor,
            DamageReductionAmount = 5,
            DamageReductionBypass = "magic",
        });

        // Spell Resistance (various levels)
        RegisterSR(EnchantmentType.SpellResistance13, "Spell Resistance (13)", 13, 2);
        RegisterSR(EnchantmentType.SpellResistance15, "Spell Resistance (15)", 15, 3);
        RegisterSR(EnchantmentType.SpellResistance17, "Spell Resistance (17)", 17, 4);
        RegisterSR(EnchantmentType.SpellResistance19, "Spell Resistance (19)", 19, 5);
    }

    private static void RegisterSR(EnchantmentType type, string name, int sr, int bonusEquiv)
    {
        Register(new EnchantmentStats
        {
            Type = type,
            DisplayName = name,
            Description = $"Armor grants spell resistance {sr}.",
            BonusEquivalent = bonusEquiv,
            Slot = EnchantmentSlot.Armor,
            SpellResistance = sr,
            IncompatibleWith = new List<EnchantmentType>
            {
                EnchantmentType.SpellResistance13,
                EnchantmentType.SpellResistance15,
                EnchantmentType.SpellResistance17,
                EnchantmentType.SpellResistance19,
            },
        });
    }

    // ========================================================================
    // ARMOR/SHIELD - MISC
    // ========================================================================

    private static void RegisterMiscArmorShieldAbilities()
    {
        // Ghost Touch (armor): +3 bonus, full AC vs incorporeal
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.GhostTouch,
            DisplayName = "Ghost Touch",
            Description = "Armor's enhancement bonus applies fully against incorporeal touch attacks.",
            BonusEquivalent = 3,
            Slot = EnchantmentSlot.ArmorOrShield,
            GhostTouchEffect = true,
        });

        // Wild: +3 bonus, armor melds with wild shape
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.WildArmor,
            DisplayName = "Wild",
            Description = "Armor melds into the wearer's new form during wild shape, continuing to provide its armor bonus.",
            BonusEquivalent = 3,
            Slot = EnchantmentSlot.Armor,
            WildShapeCompatible = true,
        });
    }

    // ========================================================================
    // SHIELD SPECIFIC
    // ========================================================================

    private static void RegisterShieldSpecificAbilities()
    {
        // Arrow Deflection: +2 bonus
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.ArrowDeflection,
            DisplayName = "Arrow Deflection",
            Description = "Once per round, the shield can deflect a ranged attack that would normally hit.",
            BonusEquivalent = 2,
            Slot = EnchantmentSlot.Shield,
            ArrowDeflectionEffect = true,
        });

        // Bashing: +1 bonus, shield bash deals damage as 2 sizes larger
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Bashing,
            DisplayName = "Bashing",
            Description = "Shield bash deals damage as if the shield were two size categories larger.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Shield,
            BashingEffect = true,
        });

        // Blinding: +1 bonus, flash 2/day
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Blinding,
            DisplayName = "Blinding",
            Description = "Shield can flash brilliantly twice per day. Creatures within 20 ft. must make Fort DC 14 or be blinded for 1d4 rounds.",
            BonusEquivalent = 1,
            Slot = EnchantmentSlot.Shield,
        });
    }

    // ========================================================================
    // ADVANCED ARMOR/SHIELD ABILITIES (Phase 5-6)
    // ========================================================================

    private static void RegisterAdvancedArmorShieldAbilities()
    {
        // Animated: +2 bonus, shield floats and defends without being held
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Animated,
            DisplayName = "Animated",
            Description = "Shield floats near the wielder, providing its shield bonus without being held, freeing both hands.",
            BonusEquivalent = 2,
            Slot = EnchantmentSlot.Shield,
            AnimatedEffect = true,
        });

        // Reflecting: +5 bonus, reflect targeted spells
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Reflecting,
            DisplayName = "Reflecting",
            Description = "Once per day, the shield can reflect a single targeted spell back at its caster.",
            BonusEquivalent = 5,
            Slot = EnchantmentSlot.Shield,
            ReflectingEffect = true,
        });

        // Ghost Touch (shield): +3 bonus, blocks incorporeal touch attacks
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.GhostTouchShield,
            DisplayName = "Ghost Touch",
            Description = "Shield's enhancement bonus applies fully against incorporeal touch attacks.",
            BonusEquivalent = 3,
            Slot = EnchantmentSlot.Shield,
            GhostTouchShieldEffect = true,
        });

        // Glamered: flat cost 2,700 gp, disguise armor appearance
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Glamered,
            DisplayName = "Glamered",
            Description = "Armor can appear as any normal clothing. The illusion does not change its properties.",
            FlatCostGp = 2700,
            Slot = EnchantmentSlot.Armor,
            GlameredEffect = true,
        });

        // Etherealness: +4 equivalent (DMG: on command, become ethereal 1/day, 10 min)
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.Etherealness,
            DisplayName = "Etherealness",
            Description = "Once per day, the wearer can become ethereal (as Ethereal Jaunt) for up to 10 minutes.",
            BonusEquivalent = 4, // Per DMG errata: counted as +4 equivalent in practice
            Slot = EnchantmentSlot.Armor,
            EtherealJauntEffect = true,
        });

        // Undead Controlling: +3 bonus, command undead as evil cleric
        Register(new EnchantmentStats
        {
            Type = EnchantmentType.UndeadControlling,
            DisplayName = "Undead Controlling",
            Description = "Wearer can command undead 3 times per day as a cleric of 3 levels lower than their character level.",
            BonusEquivalent = 3,
            Slot = EnchantmentSlot.ArmorOrShield,
            UndeadControllingEffect = true,
        });
    }
}
