using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// NPCDatabase_Dragons.cs — Dragon NPC Registration
// Generates 60 dragon variants (10 types × 6 age categories) from
// DragonData templates and registers them in the NPC database.
// Also defines dragon encounter presets.
// =====================================================================

public static partial class NPCDatabase
{
    /// <summary>
    /// Register all dragon NPCs (10 types × 6 age categories = 60 variants).
    /// Called from NPCDatabase.Initialize().
    /// </summary>
    private static void RegisterCreatures_Dragons()
    {
        foreach (DragonType type in DragonData.AllTypes())
        {
            DragonTypeTemplate template = DragonData.GetTemplate(type);
            if (template == null) continue;

            foreach (DragonAgeCategory age in DragonData.AllAges())
            {
                if (!template.AgeStats.ContainsKey(age)) continue;

                NPCDefinition def = BuildDragonDefinition(template, age);
                if (def != null)
                    Register(def);
            }
        }
    }

    /// <summary>
    /// Factory method: builds a complete NPCDefinition from a dragon template + age category.
    /// </summary>
    private static NPCDefinition BuildDragonDefinition(DragonTypeTemplate template, DragonAgeCategory age)
    {
        DragonAgeStats stats = template.AgeStats[age];
        SizeCategory size = DragonData.GetSize(template.SizeClass, age);
        string id = DragonData.GetNPCId(template.Type, age);
        string displayName = DragonData.GetDisplayName(template.Type, age);
        string ageName = DragonData.GetAgeName(age);

        // --- Natural attacks ---
        List<NaturalAttackDefinition> attacks = BuildDragonNaturalAttacks(stats, size);

        // --- Breath weapon (primary) ---
        BreathWeaponDefinition breathWeapon = new BreathWeaponDefinition
        {
            Shape = template.BreathShape,
            RangeFeet = stats.BreathRangeFeet,
            DamageDice = stats.BreathDamageDice,
            DamageCount = stats.BreathDamageCount,
            DamageType = template.BreathDamageType,
            SaveDC = stats.BreathSaveDC,
            IsReflexSave = true,
            RechargeRounds = 3  // 1d4 average ≈ 2.5, round to 3
        };

        // --- Secondary breath weapon (metallic dragons only) ---
        SecondaryBreathWeaponDefinition secondaryBreath = null;
        if (template.IsMetallic && template.SecondaryBreath != SecondaryBreathType.None
            && stats.SecondaryBreathUsesPerDay > 0)
        {
            secondaryBreath = BuildSecondaryBreathWeapon(template, stats, size);
        }

        // --- Frightful Presence (Young Adult+ only) ---
        FrightfulPresenceDefinition frightfulPresence = null;
        if (stats.FrightfulPresenceDC > 0 && (int)age >= (int)DragonAgeCategory.YoungAdult)
        {
            frightfulPresence = new FrightfulPresenceDefinition
            {
                SaveDC = stats.FrightfulPresenceDC,
                RangeFeet = stats.FrightfulPresenceRangeFeet,
                HDThresholdForPanic = 4,
                DurationDice = 4,
                DurationDieSides = 6
            };
        }

        // --- Immunities ---
        CreatureImmunities immunities = new CreatureImmunities();
        // All dragons immune to sleep and paralysis (handled via condition immunity in creature type)
        // Element immunity
        switch (template.ElementImmunity)
        {
            case DamageType.Fire: immunities.immuneToFire = true; break;
            case DamageType.Cold: immunities.immuneToCold = true; break;
            case DamageType.Acid: immunities.immuneToAcid = true; break;
            case DamageType.Electricity: immunities.immuneToElectricity = true; break;
        }

        List<DamageType> damageImmunities = new List<DamageType> { template.ElementImmunity };

        // --- Spells (sorcerer casting for applicable dragons) ---
        List<string> knownSpells = new List<string>();
        List<string> preparedSlots = new List<string>();
        if (stats.SorcererCasterLevel > 0 && template.SorcererSpellIds != null && template.SorcererSpellIds.Count > 0)
        {
            knownSpells.AddRange(template.SorcererSpellIds);
            // Sorcerer: spontaneous caster — use known spells as prepared slots
            // Give slots equal to caster level (simplified)
            for (int s = 0; s < Mathf.Min(stats.SorcererCasterLevel, template.SorcererSpellIds.Count); s++)
                preparedSlots.Add(template.SorcererSpellIds[s]);
        }

        // --- Special abilities display ---
        List<string> specialAbilities = BuildDragonSpecialAbilities(template, stats, age, size);

        // --- Creature tags ---
        List<string> tags = new List<string> { "Dragon", "MM35" };
        if (template.IsMetallic)
            tags.Add("Good");
        else
            tags.Add("Evil");

        // --- Description ---
        string breathDesc = template.BreathShape == BreathWeaponShape.Cone ? "cone" : "line";
        string crStr = stats.ChallengeRating.ToString();
        string description = $"{displayName} (CR {crStr}). {stats.HitDice}d12 HD, {stats.BaseHP} HP. " +
            $"Breath: {stats.BreathRangeFeet}-ft. {breathDesc} {stats.BreathDamageCount}d{stats.BreathDamageDice} {template.BreathDamageType}, DC {stats.BreathSaveDC}. " +
            $"Immune to {template.ElementImmunity}, sleep, paralysis.";
        if (template.IsMetallic && secondaryBreath != null)
            description += $" Secondary: {template.SecondaryBreath}.";
        if (frightfulPresence != null)
            description += $" Frightful Presence DC {frightfulPresence.SaveDC}.";

        // --- Build the NPCDefinition ---
        NPCDefinition def = new NPCDefinition
        {
            Id = id,
            Name = displayName,
            Description = description,
            ChallengeRating = crStr,
            Level = stats.HitDice,
            CharacterClass = "Warrior",
            CreatureType = "Dragon",
            HitDice = stats.HitDice,
            BABOverride = BABProgression.Good,
            FortitudeSaveOverride = SaveProgression.Good,
            ReflexSaveOverride = SaveProgression.Good,
            WillSaveOverride = SaveProgression.Good,
            SizeCategory = size,
            IsTallCreature = true,
            NaturalArmorBonus = stats.NaturalArmor,
            STR = stats.STR,
            DEX = stats.DEX,
            CON = stats.CON,
            INT = stats.INT,
            WIS = stats.WIS,
            CHA = stats.CHA,
            BAB = stats.BAB,
            BaseSpeed = stats.BaseSpeed,
            BaseHitDieHP = stats.BaseHP,
            NaturalAttacks = attacks,
            BreathWeapon = breathWeapon,
            SecondaryBreathWeapon = secondaryBreath,
            FrightfulPresence = frightfulPresence,
            DamageReductionAmount = stats.DamageReduction,
            DamageReductionBypass = stats.DRBypass,
            SpellResistance = stats.SpellResistance,
            DamageImmunities = damageImmunities,
            Immunities = immunities,
            CreatureTags = tags,
            Feats = stats.Feats != null ? new List<string>(stats.Feats) : new List<string>(),
            KnownSpellIds = knownSpells,
            PreparedSpellSlotIds = preparedSlots,
            SpecialAbilities = specialAbilities,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Dragon,
            SpriteColor = template.SpriteColor,
            PanelColor = template.PanelColor,
            NameColor = template.NameColor
        };

        return def;
    }

    /// <summary>
    /// Build the natural attack list for a dragon based on size and age stats.
    /// Full attack routine: Bite / 2 Claws / 2 Wings / Tail Slap
    /// Wyrmlings only get Bite + 2 Claws (no wings/tail until Very Young+).
    /// </summary>
    private static List<NaturalAttackDefinition> BuildDragonNaturalAttacks(DragonAgeStats stats, SizeCategory size)
    {
        var attacks = new List<NaturalAttackDefinition>();

        // Bite (primary, full STR)
        // Reach = 2 for Large+, 1 for Medium/Small
        int biteReach = size >= SizeCategory.Large ? 2 : 1;
        attacks.Add(new NaturalAttackDefinition
        {
            Name = "Bite",
            DamageDice = stats.BiteDamageDice,
            DamageCount = stats.BiteDamageCount,
            Count = 1,
            BonusDamageSource = DamageBonusSource.Strength,
            Range = biteReach,
            IsPrimary = true
        });

        // 2 Claws (secondary, ½ STR)
        if (stats.ClawDamageDice > 0)
        {
            attacks.Add(new NaturalAttackDefinition
            {
                Name = "Claw",
                DamageDice = stats.ClawDamageDice,
                DamageCount = stats.ClawDamageCount,
                Count = 2,
                BonusDamageSource = DamageBonusSource.StrengthHalf,
                Range = 1,
                IsPrimary = false
            });
        }

        // 2 Wings (secondary, ½ STR) — not available for Wyrmlings (dice = 0)
        if (stats.WingDamageDice > 0)
        {
            attacks.Add(new NaturalAttackDefinition
            {
                Name = "Wing",
                DamageDice = stats.WingDamageDice,
                DamageCount = stats.WingDamageCount,
                Count = 2,
                BonusDamageSource = DamageBonusSource.StrengthHalf,
                Range = 1,
                IsPrimary = false
            });
        }

        // Tail Slap (secondary, 1.5× STR) — not available for Wyrmlings (dice = 0)
        if (stats.TailDamageDice > 0)
        {
            attacks.Add(new NaturalAttackDefinition
            {
                Name = "Tail Slap",
                DamageDice = stats.TailDamageDice,
                DamageCount = stats.TailDamageCount,
                Count = 1,
                BonusDamageSource = DamageBonusSource.StrengthOneAndHalf,
                Range = 1,
                IsPrimary = false
            });
        }

        return attacks;
    }

    /// <summary>
    /// Build secondary breath weapon definition for metallic dragons.
    /// </summary>
    private static SecondaryBreathWeaponDefinition BuildSecondaryBreathWeapon(
        DragonTypeTemplate template, DragonAgeStats stats, SizeCategory size)
    {
        // Secondary breath uses the same shape/range as primary for simplicity
        // In D&D 3.5e, it uses a cone for cone-breathers and line for line-breathers
        var sbw = new SecondaryBreathWeaponDefinition
        {
            EffectType = template.SecondaryBreath,
            Shape = template.BreathShape,
            RangeFeet = stats.BreathRangeFeet,
            SaveDC = stats.SecondaryBreathSaveDC,
            UsesPerDay = stats.SecondaryBreathUsesPerDay,
            UsesRemaining = stats.SecondaryBreathUsesPerDay
        };

        // Configure save type and effect-specific parameters
        switch (template.SecondaryBreath)
        {
            case SecondaryBreathType.WeakeningGas:
                // Gold: 1d6 Str damage, Fort negates
                sbw.IsWillSave = false; // Fort save
                sbw.AbilityDamageAmount = 6; // 1d6
                sbw.DurationDice = 0;
                sbw.DurationBonus = 0;
                break;
            case SecondaryBreathType.ParalysisGas:
                // Silver: paralyzed 1d6+3 rounds, Fort negates
                sbw.IsWillSave = false; // Fort save
                sbw.DurationDice = 6;
                sbw.DurationBonus = 3;
                break;
            case SecondaryBreathType.RepulsionGas:
                // Bronze: knockback 1d6×10 feet, Fort negates
                sbw.IsWillSave = false; // Fort save
                sbw.DurationDice = 0;
                sbw.DurationBonus = 0;
                break;
            case SecondaryBreathType.SlowGas:
                // Copper: slowed 1d6+3 rounds, Fort negates
                sbw.IsWillSave = false; // Fort save
                sbw.DurationDice = 6;
                sbw.DurationBonus = 3;
                break;
            case SecondaryBreathType.SleepGas:
                // Brass: sleep 1d6+3 rounds, Will negates
                sbw.IsWillSave = true; // Will save
                sbw.DurationDice = 6;
                sbw.DurationBonus = 3;
                break;
        }

        return sbw;
    }

    /// <summary>
    /// Build the special abilities display list for tooltips/UI.
    /// </summary>
    private static List<string> BuildDragonSpecialAbilities(
        DragonTypeTemplate template, DragonAgeStats stats, DragonAgeCategory age, SizeCategory size)
    {
        var abilities = new List<string>();

        // Breath weapon
        string breathShape = template.BreathShape == BreathWeaponShape.Cone ? "cone" : "line";
        abilities.Add($"Breath Weapon ({stats.BreathRangeFeet}-ft. {breathShape}, " +
            $"{stats.BreathDamageCount}d{stats.BreathDamageDice} {template.BreathDamageType}, " +
            $"Reflex DC {stats.BreathSaveDC} half, 1d4 rds recharge)");

        // Secondary breath
        if (template.IsMetallic && template.SecondaryBreath != SecondaryBreathType.None
            && stats.SecondaryBreathUsesPerDay > 0)
        {
            string effectName;
            switch (template.SecondaryBreath)
            {
                case SecondaryBreathType.WeakeningGas: effectName = "Weakening Gas (1d6 Str, Fort negates)"; break;
                case SecondaryBreathType.ParalysisGas: effectName = "Paralysis Gas (1d6+3 rds, Fort negates)"; break;
                case SecondaryBreathType.RepulsionGas: effectName = "Repulsion Gas (knockback, Fort negates)"; break;
                case SecondaryBreathType.SlowGas: effectName = "Slow Gas (1d6+3 rds, Fort negates)"; break;
                case SecondaryBreathType.SleepGas: effectName = "Sleep Gas (1d6+3 rds, Will negates)"; break;
                default: effectName = template.SecondaryBreath.ToString(); break;
            }
            abilities.Add($"Secondary Breath: {effectName}, {stats.SecondaryBreathUsesPerDay}/day, DC {stats.SecondaryBreathSaveDC}");
        }

        // Frightful Presence
        if (stats.FrightfulPresenceDC > 0 && (int)age >= (int)DragonAgeCategory.YoungAdult)
        {
            abilities.Add($"Frightful Presence ({stats.FrightfulPresenceRangeFeet} ft., Will DC {stats.FrightfulPresenceDC})");
        }

        // Damage reduction
        if (stats.DamageReduction > 0)
        {
            abilities.Add($"Damage Reduction {stats.DamageReduction}/magic");
        }

        // Spell resistance
        if (stats.SpellResistance > 0)
        {
            abilities.Add($"Spell Resistance {stats.SpellResistance}");
        }

        // Sorcerer casting
        if (stats.SorcererCasterLevel > 0)
        {
            abilities.Add($"Casts as {GetOrdinal(stats.SorcererCasterLevel)}-level Sorcerer");
        }

        // Standard dragon qualities
        abilities.Add($"Immunity to {template.ElementImmunity}, sleep, paralysis");
        abilities.Add("Darkvision 120 ft., low-light vision, blindsense 60 ft.");

        return abilities;
    }

    /// <summary>Helper: ordinal number string (1st, 2nd, 3rd, etc.)</summary>
    private static string GetOrdinal(int n)
    {
        if (n <= 0) return n.ToString();
        switch (n % 100)
        {
            case 11: case 12: case 13: return n + "th";
        }
        switch (n % 10)
        {
            case 1: return n + "st";
            case 2: return n + "nd";
            case 3: return n + "rd";
            default: return n + "th";
        }
    }

    // ================================================================
    // Dragon Encounter Presets
    // ================================================================

    /// <summary>
    /// Returns dragon-specific encounter presets for the encounter selector.
    /// </summary>
    public static List<EncounterPreset> GetDragonEncounterPresets()
    {
        return new List<EncounterPreset>
        {
            // --- Single Dragon Encounters ---
            new EncounterPreset("dragon_red_young_solo", "🐉 Young Red Dragon",
                "Classic boss encounter: a Young Red Dragon (CR 7). 123 HP, 40-ft fire cone, full melee routine.",
                new List<string> { "dragon_red_young" }),

            new EncounterPreset("dragon_gold_young_solo", "🐉 Young Gold Dragon",
                "Powerful metallic dragon (CR 9). 142 HP, fire cone + weakening gas, 3rd-level sorcerer.",
                new List<string> { "dragon_gold_young" }),

            new EncounterPreset("dragon_white_wyrmling_pair", "🐉 White Dragon Wyrmling Pair",
                "Two White Dragon Wyrmlings (CR 2 each). Low-level dragon encounter for new adventurers.",
                new List<string> { "dragon_white_wyrmling", "dragon_white_wyrmling" }),

            new EncounterPreset("dragon_black_young_swamp", "🐉 Young Black Dragon + Gnoll War Party",
                "A Young Black Dragon (CR 6) with gnoll minions in an ambush.",
                new List<string> { "dragon_black_young", "gnoll", "gnoll", "gnoll" }),

            // --- Chromatic vs Metallic ---
            new EncounterPreset("dragon_red_vs_silver", "⚔️ Red vs Silver Dragon",
                "Young Red Dragon vs Young Silver Dragon (both Large). Epic dragon-on-dragon battle.",
                new List<string> { "dragon_red_young", "dragon_silver_young" }),

            // --- Young Adult Encounters (with Frightful Presence) ---
            new EncounterPreset("dragon_red_young_adult", "🐉 Young Adult Red Dragon",
                "Fearsome Young Adult Red Dragon (CR 13). Frightful Presence, DR 5/magic, SR 21, 10d10 fire breath.",
                new List<string> { "dragon_red_young_adult" }),

            new EncounterPreset("dragon_blue_young_adult", "🐉 Young Adult Blue Dragon",
                "Young Adult Blue Dragon (CR 13). Lightning line breath, Frightful Presence, 199 HP.",
                new List<string> { "dragon_blue_young_adult" }),

            // --- Adult Dragon Boss Encounters ---
            new EncounterPreset("dragon_red_adult", "🐉 Adult Red Dragon",
                "Ultimate boss: Adult Red Dragon (CR 15). 253 HP, 12d10 fire cone, DR 5/magic, SR 23.",
                new List<string> { "dragon_red_adult" }),

            new EncounterPreset("dragon_gold_adult", "🐉 Adult Gold Dragon",
                "Mightiest metallic: Adult Gold Dragon (CR 17). 288 HP, 12d10 fire + weakening gas, 9th-level sorcerer.",
                new List<string> { "dragon_gold_adult" }),

            // --- Wyrmling Gauntlet ---
            new EncounterPreset("dragon_wyrmling_gauntlet", "🐉 Wyrmling Gauntlet",
                "Face five chromatic wyrmlings: Red, Blue, Green, Black, White. Low-level dragon menagerie.",
                new List<string> {
                    "dragon_red_wyrmling", "dragon_blue_wyrmling", "dragon_green_wyrmling",
                    "dragon_black_wyrmling", "dragon_white_wyrmling"
                }),

            // --- Metallic Dragon Encounter ---
            new EncounterPreset("dragon_metallic_trio", "🐉 Metallic Dragon Trio",
                "Three Very Young metallic dragons: Gold, Silver, Bronze. Powerful allies or foes.",
                new List<string> {
                    "dragon_gold_very_young", "dragon_silver_very_young", "dragon_bronze_very_young"
                }),

            // --- Juvenile Dragon ---
            new EncounterPreset("dragon_blue_juvenile", "🐉 Juvenile Blue Dragon",
                "Juvenile Blue Dragon (CR 11). 152 HP, 80-ft lightning line, 3rd-level sorcerer.",
                new List<string> { "dragon_blue_juvenile" }),

            // --- Mixed Dragon + Minion Encounters ---
            new EncounterPreset("dragon_green_goblins", "🐉 Young Green Dragon + Goblin Warband",
                "A Young Green Dragon commands a goblin raiding party in the forest.",
                new List<string> { "dragon_green_young", "goblin", "goblin", "goblin", "goblin", "goblin", "goblin" }),

            new EncounterPreset("dragon_brass_desert", "🐉 Young Brass Dragon (Desert)",
                "A talkative Young Brass Dragon (CR 5). Prefers sleep gas over direct combat.",
                new List<string> { "dragon_brass_young" }),

            new EncounterPreset("dragon_copper_young_solo", "🐉 Young Copper Dragon",
                "Playful Young Copper Dragon (CR 7). Acid line + slow gas. Loves jokes.",
                new List<string> { "dragon_copper_young" })
        };
    }
}
