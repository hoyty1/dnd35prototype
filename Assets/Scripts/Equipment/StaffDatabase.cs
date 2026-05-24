using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

// ============================================================================
// StaffDatabase.cs — Registry of all D&D 3.5e DMG magic staves (DMG p.243-248)
//
// DMG 3.5e CORE RULES ONLY:
//   - Staves CANNOT be recharged. Once a staff's charges are expended,
//     it becomes non-magical and worthless.
//   - Standard staves have 50 charges.
//   - Each spell costs 1-5 charges to cast.
//   - Spell trigger activation (class list or UMD DC 20).
//   - All staves weigh 5 lbs and can double as quarterstaves.
//   - NO Unearthed Arcana variants. NO Dragon Magazine feats. NO house rules.
//
// Contains all 20 DMG staves organized by implementation tier:
//   Tier 1 (Full/near-full): Staff of Fire, Healing, Defense
//   Tier 2 (50-74%): Charming, Frost, Size Alteration, Necromancy, Evocation,
//                     Illumination, Swarming Insects
//   Tier 3 (25-49%): Enchantment, Illusion, Power, Transmutation
//   Tier 4 (Stub):   Life, Woodlands, Divination, Earth/Stone, Passage, Magi
//
// All staves follow core DMG 3.5e rules: disposable, no recharging.
// ============================================================================

public static class StaffDatabase
{
    private static readonly Dictionary<string, StaffDefinition> _staves
        = new Dictionary<string, StaffDefinition>();

    private static bool _initialized;

    // ── Public API ──

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        RegisterTier1Staves();
        RegisterTier2Staves();
        RegisterTier3Staves();
        RegisterTier4Staves();

        Debug.Log($"[StaffDatabase] Registered {_staves.Count} staves (core DMG 3.5e, non-rechargeable). " +
                  $"Full: {CountByStatus(StaffImplementationStatus.Full)}, " +
                  $"Partial: {CountByStatus(StaffImplementationStatus.Partial)}, " +
                  $"Stub: {CountByStatus(StaffImplementationStatus.Stub)}");
    }

    public static StaffDefinition GetStaff(string staffId)
    {
        Init();
        return _staves.TryGetValue(staffId, out var def) ? def : null;
    }

    public static List<StaffDefinition> GetAllStaves()
    {
        Init();
        return new List<StaffDefinition>(_staves.Values);
    }

    public static List<StaffDefinition> GetImplementedStaves()
    {
        Init();
        var result = new List<StaffDefinition>();
        foreach (var staff in _staves.Values)
        {
            if (staff.Status != StaffImplementationStatus.Stub)
                result.Add(staff);
        }
        return result;
    }

    public static int Count => _staves.Count;

    // ── Internal helpers ──

    private static void Register(StaffDefinition def)
    {
        if (def == null || string.IsNullOrWhiteSpace(def.StaffId))
        {
            Debug.LogWarning("[StaffDatabase] Attempted to register null/unnamed staff.");
            return;
        }
        if (_staves.ContainsKey(def.StaffId))
        {
            Debug.LogWarning($"[StaffDatabase] Duplicate staff ID: {def.StaffId}");
            return;
        }
        // Ensure CurrentCharges starts at MaxCharges for new definitions
        if (def.CurrentCharges <= 0)
            def.CurrentCharges = def.MaxCharges;
        _staves[def.StaffId] = def;
    }

    private static int CountByStatus(StaffImplementationStatus status)
    {
        int count = 0;
        foreach (var s in _staves.Values)
            if (s.Status == status) count++;
        return count;
    }

    /// <summary>Create a functional spell entry (IsStub = false).</summary>
    private static StaffSpellEntry Spell(string spellId, string displayName, int level, int chargeCost)
    {
        return new StaffSpellEntry
        {
            SpellId = spellId,
            SpellName = displayName,
            SpellLevel = level,
            ChargeCost = chargeCost,
            IsStub = false
        };
    }

    /// <summary>Create a stub spell entry (spell not yet implemented).</summary>
    private static StaffSpellEntry StubSpell(string spellId, string displayName, int level, int chargeCost, string stubDesc = null)
    {
        return new StaffSpellEntry
        {
            SpellId = spellId,
            SpellName = displayName,
            SpellLevel = level,
            ChargeCost = chargeCost,
            IsStub = true,
            StubDescription = stubDesc ?? $"{displayName} (not yet implemented)"
        };
    }

    // ====================================================================
    //  TIER 1 — All/nearly-all spells implemented (≥75%)
    // ====================================================================

    private static void RegisterTier1Staves()
    {
        // ── Staff of Fire (DMG p.245) ──
        // 100% ready: all 3 spells functional
        Register(new StaffDefinition
        {
            StaffId = "staff_of_fire",
            Name = "Staff of Fire",
            Description = "Crafted from bronzewood with brass bindings, this staff smells faintly of smoke. " +
                          "It allows use of Burning Hands (1 charge), Fireball (1 charge), and Wall of Fire (2 charges). " +
                          "Once all charges are spent, the staff becomes non-magical.",
            AuraSchool = "Evocation",
            AuraStrength = "Strong",
            CasterLevel = 8,
            MarketPrice = 17750,
            AllowedClasses = new[] { "Wizard", "Sorcerer" },
            Status = StaffImplementationStatus.Full,
            ImplementationNotes = "All 3 spells fully implemented.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.BURNING_HANDS, "Burning Hands", 1, 1),
                Spell(SpellNames.FIREBALL, "Fireball", 3, 1),
                Spell(SpellNames.WALL_OF_FIRE, "Wall of Fire", 4, 2)
            }
        });

        // ── Staff of Healing (DMG p.245) ──
        // 100% ready: all 3 spells functional
        Register(new StaffDefinition
        {
            StaffId = "staff_of_healing",
            Name = "Staff of Healing",
            Description = "This white ash staff, inlaid with silver runes, is a boon to healers. " +
                          "Cure Light Wounds (1 charge), Cure Moderate Wounds (1 charge), " +
                          "Cure Serious Wounds (2 charges). Non-rechargeable.",
            AuraSchool = "Conjuration",
            AuraStrength = "Strong",
            CasterLevel = 8,
            MarketPrice = 27750,
            AllowedClasses = new[] { "Cleric", "Druid" },
            Status = StaffImplementationStatus.Full,
            ImplementationNotes = "All 3 healing spells fully implemented.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.CURE_LIGHT_WOUNDS, "Cure Light Wounds", 1, 1),
                Spell(SpellNames.CURE_MODERATE_WOUNDS, "Cure Moderate Wounds", 2, 1),
                Spell(SpellNames.CURE_SERIOUS_WOUNDS, "Cure Serious Wounds", 3, 2)
            }
        });

        // ── Staff of Defense (DMG p.243) ──
        // 75% ready: 3 of 4 spells functional; Shield of Law (L8) missing
        Register(new StaffDefinition
        {
            StaffId = "staff_of_defense",
            Name = "Staff of Defense",
            Description = "This sturdy iron-banded staff helps protect its wielder. " +
                          "Shield (1 charge), Shield of Faith (1 charge), Shield Other (1 charge), " +
                          "Shield of Law (3 charges). Non-rechargeable.",
            AuraSchool = "Abjuration",
            AuraStrength = "Strong",
            CasterLevel = 15,
            MarketPrice = 62000,
            AllowedClasses = new[] { "Cleric", "Wizard", "Sorcerer" },
            Status = StaffImplementationStatus.Partial,
            ImplementationNotes = "3/4 spells work. Shield of Law (L8) not yet registered.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.SHIELD, "Shield", 1, 1),
                Spell(SpellNames.SHIELD_OF_FAITH, "Shield of Faith", 1, 1),
                Spell(SpellNames.SHIELD_OTHER, "Shield Other", 2, 1),
                Spell(SpellNames.SHIELD_OF_LAW, "Shield of Law", 8, 3)
            }
        });
    }

    // ====================================================================
    //  TIER 2 — Most spells work, 1-2 stubs needed (50-74%)
    // ====================================================================

    private static void RegisterTier2Staves()
    {
        // ── Staff of Charming (DMG p.243) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_charming",
            Name = "Staff of Charming",
            Description = "Made of polished cherry wood, this staff helps beguile the minds of others. " +
                          "Charm Person (1 charge), Charm Monster (2 charges). Non-rechargeable.",
            AuraSchool = "Enchantment",
            AuraStrength = "Moderate",
            CasterLevel = 8,
            MarketPrice = 16500,
            AllowedClasses = new[] { "Wizard", "Sorcerer", "Bard" },
            Status = StaffImplementationStatus.Partial,
            ImplementationNotes = "Charm Person works. Charm Monster not yet registered.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.CHARM_PERSON, "Charm Person", 1, 1),
                Spell(SpellNames.CHARM_MONSTER, "Charm Monster", 4, 2)
            }
        });

        // ── Staff of Frost (DMG p.245) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_frost",
            Name = "Staff of Frost",
            Description = "Tipped with a gem of blue-white ice, this staff radiates cold. " +
                          "Ice Storm (1 charge), Wall of Ice (1 charge), Cone of Cold (2 charges). Non-rechargeable.",
            AuraSchool = "Evocation",
            AuraStrength = "Strong",
            CasterLevel = 10,
            MarketPrice = 56250,
            AllowedClasses = new[] { "Wizard", "Sorcerer" },
            Status = StaffImplementationStatus.Partial,
            ImplementationNotes = "Ice Storm and Wall of Ice work. Cone of Cold (L5) not yet registered.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.ICE_STORM, "Ice Storm", 4, 1),
                Spell(SpellNames.WALL_OF_ICE, "Wall of Ice", 4, 1),
                Spell(SpellNames.CONE_OF_COLD, "Cone of Cold", 5, 2)
            }
        });

        // ── Staff of Size Alteration (DMG p.246) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_size_alteration",
            Name = "Staff of Size Alteration",
            Description = "Stout and sturdy, this staff of dark wood allows its wielder to alter the size of objects and creatures. " +
                          "Enlarge Person (1 charge), Reduce Person (1 charge), Shrink Item (1 charge). Non-rechargeable.",
            AuraSchool = "Transmutation",
            AuraStrength = "Moderate",
            CasterLevel = 8,
            MarketPrice = 26150,
            AllowedClasses = new[] { "Wizard", "Sorcerer" },
            Status = StaffImplementationStatus.Partial,
            ImplementationNotes = "Enlarge/Reduce Person work. Shrink Item (utility) not registered.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.ENLARGE_PERSON, "Enlarge Person", 1, 1),
                Spell(SpellNames.REDUCE_PERSON, "Reduce Person", 1, 1),
                Spell(SpellNames.SHRINK_ITEM, "Shrink Item", 3, 1)
            }
        });

        // ── Staff of Necromancy (DMG p.247) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_necromancy",
            Name = "Staff of Necromancy",
            Description = "This staff is made from ebony or bone and is set with a gem of jet. " +
                          "It provides access to necromantic spells of increasing power. Non-rechargeable.",
            AuraSchool = "Necromancy",
            AuraStrength = "Strong",
            CasterLevel = 13,
            MarketPrice = 82000,
            AllowedClasses = new[] { "Wizard", "Sorcerer" },
            Status = StaffImplementationStatus.Partial,
            ImplementationNotes = "4/6 spells work (L1-4). Waves of Fatigue (L5) and Circle of Death (L6) not registered.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.CAUSE_FEAR, "Cause Fear", 1, 1),
                Spell(SpellNames.GHOUL_TOUCH, "Ghoul Touch", 2, 1),
                Spell(SpellNames.HALT_UNDEAD, "Halt Undead", 3, 1),
                Spell(SpellNames.ENERVATION, "Enervation", 4, 2),
                StubSpell("waves_of_fatigue", "Waves of Fatigue", 5, 2, "30-ft cone, all creatures fatigued, no save"),
                Spell(SpellNames.CIRCLE_OF_DEATH, "Circle of Death", 6, 3)
            }
        });

        // ── Staff of Evocation (DMG p.244) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_evocation",
            Name = "Staff of Evocation",
            Description = "Usually made of smooth hickory, this staff commands the raw forces of energy. " +
                          "It channels destructive elemental power. Non-rechargeable.",
            AuraSchool = "Evocation",
            AuraStrength = "Strong",
            CasterLevel = 13,
            MarketPrice = 82000,
            AllowedClasses = new[] { "Wizard", "Sorcerer" },
            Status = StaffImplementationStatus.Partial,
            ImplementationNotes = "5/6 spells work. Chain Lightning (L6) not yet registered.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.MAGIC_MISSILE, "Magic Missile", 1, 1),
                Spell(SpellNames.SHATTER, "Shatter", 2, 1),
                Spell(SpellNames.LIGHTNING_BOLT, "Lightning Bolt", 3, 1),
                Spell(SpellNames.ICE_STORM, "Ice Storm", 4, 2),
                Spell(SpellNames.WALL_OF_FORCE, "Wall of Force", 5, 2),
                Spell(SpellNames.CHAIN_LIGHTNING, "Chain Lightning", 6, 3)
            }
        });

        // ── Staff of Illumination (DMG p.245) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_illumination",
            Name = "Staff of Illumination",
            Description = "This staff is usually topped with a brass lantern-shaped head and made of willow. " +
                          "It commands light in all its forms. Non-rechargeable.",
            AuraSchool = "Evocation",
            AuraStrength = "Strong",
            CasterLevel = 15,
            MarketPrice = 48250,
            AllowedClasses = new[] { "Wizard", "Sorcerer", "Cleric" },
            Status = StaffImplementationStatus.Partial,
            ImplementationNotes = "2/4 spells work (Flare, Daylight). Dancing Lights is placeholder. Sunburst (L8) not registered.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.DANCING_LIGHTS, "Dancing Lights", 0, 1),
                Spell(SpellNames.FLARE, "Flare", 0, 1),
                Spell(SpellNames.DAYLIGHT, "Daylight", 3, 1),
                Spell(SpellNames.SUNBURST, "Sunburst", 8, 2)
            }
        });

        // ── Staff of Swarming Insects (DMG p.246) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_swarming_insects",
            Name = "Staff of Swarming Insects",
            Description = "Made of twisted dark wood with spider web patterns, this staff calls forth vermin. " +
                          "Summon Swarm (1 charge), Insect Plague (3 charges). Non-rechargeable.",
            AuraSchool = "Conjuration",
            AuraStrength = "Moderate",
            CasterLevel = 9,
            MarketPrice = 22800,
            AllowedClasses = new[] { "Cleric", "Druid" },
            Status = StaffImplementationStatus.Partial,
            ImplementationNotes = "Summon Swarm works. Insect Plague (L5) not registered.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.SUMMON_SWARM, "Summon Swarm", 2, 1),
                Spell(SpellNames.INSECT_PLAGUE, "Insect Plague", 5, 3)
            }
        });
    }

    // ====================================================================
    //  TIER 3 — Multiple spells need stubs (25-49%)
    // ====================================================================

    private static void RegisterTier3Staves()
    {
        // ── Staff of Enchantment (DMG p.244) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_enchantment",
            Name = "Staff of Enchantment",
            Description = "Often made of holly with silver inlays, this staff commands the minds of others. " +
                          "It offers an escalating suite of enchantment spells. Non-rechargeable.",
            AuraSchool = "Enchantment",
            AuraStrength = "Strong",
            CasterLevel = 13,
            MarketPrice = 82000,
            AllowedClasses = new[] { "Wizard", "Sorcerer", "Bard" },
            Status = StaffImplementationStatus.Partial,
            ImplementationNotes = "2/6 spells work (Sleep, Hideous Laughter). Suggestion, Crushing Despair, Mind Fog, Mass Suggestion not registered.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.SLEEP, "Sleep", 1, 1),
                Spell(SpellNames.HIDEOUS_LAUGHTER, "Tasha's Hideous Laughter", 2, 1),
                StubSpell("suggestion", "Suggestion", 3, 1, "Target follows reasonable suggestion, Will negates"),
                Spell(SpellNames.CRUSHING_DESPAIR, "Crushing Despair", 4, 2),
                Spell(SpellNames.MIND_FOG, "Mind Fog", 5, 2),
                Spell(SpellNames.MASS_SUGGESTION, "Mass Suggestion", 6, 3)
            }
        });

        // ── Staff of Illusion (DMG p.245) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_illusion",
            Name = "Staff of Illusion",
            Description = "This staff is made from ebony or dark walnut and has the scent of incense. " +
                          "It weaves increasingly powerful illusions. Non-rechargeable.",
            AuraSchool = "Illusion",
            AuraStrength = "Strong",
            CasterLevel = 13,
            MarketPrice = 82000,
            AllowedClasses = new[] { "Wizard", "Sorcerer", "Bard" },
            Status = StaffImplementationStatus.Partial,
            ImplementationNotes = "3/6 spells work (Disguise Self, Mirror Image, Rainbow Pattern). Major Image, Persistent Image, Mislead not registered.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.DISGUISE_SELF, "Disguise Self", 1, 1),
                Spell(SpellNames.MIRROR_IMAGE, "Mirror Image", 2, 1),
                StubSpell("major_image", "Major Image", 3, 1, "Creates visual, auditory, olfactory, and thermal figment"),
                Spell(SpellNames.RAINBOW_PATTERN, "Rainbow Pattern", 4, 2),
                Spell(SpellNames.PERSISTENT_IMAGE, "Persistent Image", 5, 2),
                Spell(SpellNames.MISLEAD, "Mislead", 6, 3)
            }
        });

        // ── Staff of Power (DMG p.247) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_power",
            Name = "Staff of Power",
            Description = "A staff of power is a very potent magic item, with offensive and defensive abilities. " +
                          "Provides +2 luck bonus to AC and saving throws while held. " +
                          "Can be broken in a Retributive Strike for massive damage. Non-rechargeable.",
            AuraSchool = "Evocation",
            AuraStrength = "Strong",
            CasterLevel = 15,
            MarketPrice = 235000,
            AllowedClasses = new[] { "Wizard", "Sorcerer" },
            PassiveACBonus = 2,
            PassiveSaveBonus = 2,
            HasRetributiveStrike = true,
            RetributiveStrikeDamageFactor = 8,
            Status = StaffImplementationStatus.Partial,
            ImplementationNotes = "9/10 spells functional. Passive bonuses and Retributive Strike need implementation.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.MAGIC_MISSILE, "Magic Missile", 1, 1),
                Spell(SpellNames.RAY_OF_ENFEEBLEMENT, "Ray of Enfeeblement", 1, 1),
                Spell(SpellNames.CONTINUAL_FLAME, "Continual Flame", 2, 1),
                Spell(SpellNames.LEVITATE, "Levitate", 2, 1),
                Spell(SpellNames.LIGHTNING_BOLT, "Lightning Bolt", 3, 1),
                Spell(SpellNames.FIREBALL, "Fireball", 3, 1),
                Spell(SpellNames.CONE_OF_COLD, "Cone of Cold", 5, 2),
                Spell(SpellNames.HOLD_MONSTER, "Hold Monster", 5, 2),
                Spell(SpellNames.WALL_OF_FORCE, "Wall of Force", 5, 2),
                Spell(SpellNames.GLOBE_OF_INVULNERABILITY, "Globe of Invulnerability", 6, 2)
            }
        });

        // ── Staff of Transmutation (DMG p.248) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_transmutation",
            Name = "Staff of Transmutation",
            Description = "This staff is generally made from iron wood, and has iron bands along its length. " +
                          "It commands the ability to reshape reality. Non-rechargeable.",
            AuraSchool = "Transmutation",
            AuraStrength = "Strong",
            CasterLevel = 13,
            MarketPrice = 82000,
            AllowedClasses = new[] { "Wizard", "Sorcerer" },
            Status = StaffImplementationStatus.Partial,
            ImplementationNotes = "2/6 spells work (Expeditious Retreat, Blink). Alter Self placeholder; Polymorph, Baleful Polymorph, Disintegrate not registered.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.EXPEDITIOUS_RETREAT, "Expeditious Retreat", 1, 1),
                Spell(SpellNames.ALTER_SELF, "Alter Self", 2, 1),
                Spell(SpellNames.BLINK, "Blink", 3, 1),
                StubSpell("polymorph", "Polymorph", 4, 2, "Change target into another form, Fort negates"),
                StubSpell("baleful_polymorph", "Baleful Polymorph", 5, 2, "Permanently transforms target into harmless animal"),
                Spell(SpellNames.DISINTEGRATE, "Disintegrate", 6, 3)
            }
        });
    }

    // ====================================================================
    //  TIER 4 — Stub only (<25% spells ready)
    // ====================================================================

    private static void RegisterTier4Staves()
    {
        // ── Staff of Life (DMG p.245) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_life",
            Name = "Staff of Life",
            Description = "Made from the heartwood of a treant, this staff pulses with positive energy. " +
                          "Heal (1 charge), Resurrection (5 charges). Non-rechargeable.",
            AuraSchool = "Conjuration",
            AuraStrength = "Strong",
            CasterLevel = 13,
            MarketPrice = 155750,
            AllowedClasses = new[] { "Cleric" },
            Status = StaffImplementationStatus.Stub,
            ImplementationNotes = "Neither Heal (L6) nor Resurrection (L7) are registered.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.HEAL, "Heal", 6, 1),
                Spell(SpellNames.RESURRECTION, "Resurrection", 7, 5)
            }
        });

        // ── Staff of the Woodlands (DMG p.246) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_the_woodlands",
            Name = "Staff of the Woodlands",
            Description = "Appearing to have grown naturally into its shape, this staff allows use of nature spells. " +
                          "Doubles as a +2 quarterstaff. Pass without Trace at will. Non-rechargeable.",
            AuraSchool = "Conjuration",
            AuraStrength = "Strong",
            CasterLevel = 13,
            MarketPrice = 101250,
            AllowedClasses = new[] { "Druid" },
            Status = StaffImplementationStatus.Stub,
            ImplementationNotes = "Only Barkskin (domain) is implemented of 6 spells. Druid-specific, low priority.",
            Spells = new List<StaffSpellEntry>
            {
                StubSpell("charm_animal", "Charm Animal", 1, 1, "Charms one animal, Will negates"),
                StubSpell("speak_with_animals", "Speak with Animals", 1, 1, "Communicate with animals"),
                Spell(SpellNames.DOMAIN_BARKSKIN, "Barkskin", 2, 2),
                Spell(SpellNames.WALL_OF_THORNS, "Wall of Thorns", 5, 3),
                StubSpell("summon_natures_ally_vi", "Summon Nature's Ally VI", 6, 3, "Summons nature creature"),
                StubSpell("animate_plants", "Animate Plants", 7, 4, "Animates trees to fight for caster")
            }
        });

        // ── Staff of Divination (DMG p.243) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_divination",
            Name = "Staff of Divination",
            Description = "Made from a precious wood such as ebony and set with a large crystal, " +
                          "this staff grants great powers of knowledge and sight. Non-rechargeable.",
            AuraSchool = "Divination",
            AuraStrength = "Strong",
            CasterLevel = 13,
            MarketPrice = 73500,
            AllowedClasses = new[] { "Wizard", "Sorcerer", "Cleric" },
            Status = StaffImplementationStatus.Stub,
            ImplementationNotes = "0/5 spells implemented. All are placeholders or unregistered.",
            Spells = new List<StaffSpellEntry>
            {
                StubSpell("detect_secret_doors", "Detect Secret Doors", 1, 1, "Reveals hidden doors within 60 ft"),
                StubSpell("locate_object", "Locate Object", 2, 1, "Senses direction toward object"),
                StubSpell("tongues", "Tongues", 3, 1, "Speak and understand any language"),
                Spell(SpellNames.TRUE_SEEING, "True Seeing", 5, 3),
                StubSpell("prying_eyes", "Prying Eyes", 5, 4, "Creates 1d4+CL floating eyes for scouting")
            }
        });

        // ── Staff of Earth and Stone (DMG p.244) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_earth_and_stone",
            Name = "Staff of Earth and Stone",
            Description = "This staff is topped with a fist-sized emerald and allows control of earth and stone. " +
                          "Passwall (1 charge), Move Earth (3 charges), Earthquake (5 charges). Non-rechargeable.",
            AuraSchool = "Transmutation",
            AuraStrength = "Strong",
            CasterLevel = 15,
            MarketPrice = 85800,
            AllowedClasses = new[] { "Wizard", "Sorcerer", "Cleric", "Druid" },
            Status = StaffImplementationStatus.Stub,
            ImplementationNotes = "0/3 spells registered. All are L5+.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.PASSWALL, "Passwall", 5, 1),
                StubSpell("move_earth", "Move Earth", 6, 3, "Moves dirt slowly over large areas"),
                Spell(SpellNames.EARTHQUAKE, "Earthquake", 8, 5)
            }
        });

        // ── Staff of Passage (DMG p.247) ──
        Register(new StaffDefinition
        {
            StaffId = "staff_of_passage",
            Name = "Staff of Passage",
            Description = "This argent staff is topped with a sapphire and allows the wielder to travel freely. " +
                          "The most expensive non-artifact staff in the DMG. Non-rechargeable.",
            AuraSchool = "Conjuration",
            AuraStrength = "Strong",
            CasterLevel = 17,
            MarketPrice = 206900,
            AllowedClasses = new[] { "Wizard", "Sorcerer" },
            Status = StaffImplementationStatus.Stub,
            ImplementationNotes = "Only Dimension Door (L4) is implemented of 5 spells. All others are L5+.",
            Spells = new List<StaffSpellEntry>
            {
                Spell(SpellNames.DIMENSION_DOOR, "Dimension Door", 4, 1),
                Spell(SpellNames.PASSWALL, "Passwall", 5, 1),
                StubSpell("phase_door", "Phase Door", 7, 2, "Creates ethereal passage through solid objects"),
                StubSpell("greater_teleport", "Greater Teleport", 7, 2, "Teleport with no off-target chance"),
                StubSpell("astral_projection", "Astral Projection", 9, 2, "Projects you and companions onto Astral Plane")
            }
        });

        // ── Staff of the Magi (DMG p.248) ──
        // Legendary artifact-level staff
        Register(new StaffDefinition
        {
            StaffId = "staff_of_the_magi",
            Name = "Staff of the Magi",
            Description = "The most powerful staff in existence. Contains all spells from the Staff of Power " +
                          "plus additional high-level spells. Can absorb spells targeted at the wielder. " +
                          "Retributive Strike: break the staff for devastating damage. Non-rechargeable.",
            AuraSchool = "Universal",
            AuraStrength = "Overwhelming",
            CasterLevel = 20,
            MarketPrice = 200000,
            MaxCharges = 50,
            AllowedClasses = new[] { "Wizard", "Sorcerer" },
            PassiveACBonus = 2,
            PassiveSaveBonus = 2,
            HasRetributiveStrike = true,
            RetributiveStrikeDamageFactor = 8,
            Status = StaffImplementationStatus.Partial,
            ImplementationNotes = "15/16 spells functional (93.75%). Remaining stubs: Summon Monster IX (L9).",
            Spells = new List<StaffSpellEntry>
            {
                // Staff of Power spells
                Spell(SpellNames.MAGIC_MISSILE, "Magic Missile", 1, 1),
                Spell(SpellNames.RAY_OF_ENFEEBLEMENT, "Ray of Enfeeblement", 1, 1),
                Spell(SpellNames.CONTINUAL_FLAME, "Continual Flame", 2, 1),
                Spell(SpellNames.LEVITATE, "Levitate", 2, 1),
                Spell(SpellNames.LIGHTNING_BOLT, "Lightning Bolt", 3, 1),
                Spell(SpellNames.FIREBALL, "Fireball", 3, 1),
                Spell(SpellNames.DISPEL_MAGIC, "Dispel Magic", 3, 1),
                Spell(SpellNames.CONE_OF_COLD, "Cone of Cold", 5, 2),
                Spell(SpellNames.HOLD_MONSTER, "Hold Monster", 5, 2),
                Spell(SpellNames.WALL_OF_FORCE, "Wall of Force", 5, 2),
                Spell(SpellNames.GLOBE_OF_INVULNERABILITY, "Globe of Invulnerability", 6, 2),
                // Additional Magi spells
                Spell(SpellNames.TELEKINESIS, "Telekinesis", 5, 1),
                Spell(SpellNames.PLANE_SHIFT, "Plane Shift", 7, 2),
                Spell(SpellNames.SPELL_TURNING, "Spell Turning", 7, 2),
                Spell(SpellNames.PROTECTION_FROM_SPELLS, "Protection from Spells", 8, 2),
                StubSpell("summon_monster_ix", "Summon Monster IX", 9, 2, "Summons powerful extraplanar creature")
            }
        });
    }
}
