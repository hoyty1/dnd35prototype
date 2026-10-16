using System;
using System.Collections.Generic;
using DND35e.Identifiers;

/// <summary>
/// The Components line of every registered spell, as the PHB prints it (batch 11 step 17, decision D3 of
/// docs/designs/cast_core_batch11.md; checked against the owner's PHB, chapter 11, on 2026-10-15). A spell that only
/// "functions like" another takes that spell's line (cure moderate wounds as cure light wounds, the summon monster
/// spells as summon monster I). SpellDatabase.Register applies the line to the spell's material, focus and divine
/// focus flags (<see cref="Apply"/>): M, F, DF, and the split M/DF and F/DF (PHB p.174: the arcane version uses the
/// material component or focus, the divine version the divine focus). The verbal and somatic flags stay as the spell
/// blocks set them: ten blocks give a somatic component the PHB line lacks (Command, Feather Fall, Knock, ...; SPL-175),
/// which Tests.Magic.SpellComponentRulesTests lists (Hypnotic Pattern and Rainbow Pattern keep the V that only a bard casting them has). XP costs are not modelled here.
/// </summary>
public static class SpellComponentLines
{
    private static readonly Dictionary<string, string> Lines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { SpellNames.ACID_ARROW, "V, S, M, F" },
        { SpellNames.ACID_FOG, "V, S, M/DF" },
        { SpellNames.ACID_SPLASH, "V, S" },
        { SpellNames.AID, "V, S, DF" },
        { SpellNames.ALARM, "V, S, F/DF" },
        { SpellNames.ALIGN_WEAPON, "V, S, DF" },
        { SpellNames.ALTER_SELF, "V, S" },
        { SpellNames.ANIMATE_ROPE, "V, S" },
        { SpellNames.ARCANE_LOCK, "V, S, M" },
        { SpellNames.ARCANE_MARK, "V, S" },
        { SpellNames.AUGURY, "V, S, M, F" },
        { SpellNames.BANE, "V, S, DF" },
        { SpellNames.BARKSKIN, "V, S, DF" },
        { SpellNames.BEARS_ENDURANCE, "V, S, DF" },
        { SpellNames.BESTOW_CURSE, "V, S" },
        { SpellNames.BLESS, "V, S, DF" },
        { SpellNames.BLESS_WEAPON, "V, S" },
        { SpellNames.BLINDNESS_DEAFNESS, "V" },
        { SpellNames.BLINK, "V, S" },
        { SpellNames.BLUR, "V" },
        { SpellNames.BREAK_ENCHANTMENT, "V, S" },
        { SpellNames.BULLS_STRENGTH, "V, S, M/DF" },
        { SpellNames.BURNING_HANDS, "V, S" },
        { SpellNames.CALL_LIGHTNING, "V, S" },
        { SpellNames.CALM_ANIMALS, "V, S" },
        { SpellNames.CALM_EMOTIONS, "V, S, DF" },
        { SpellNames.CATS_GRACE, "V, S, M" },
        { SpellNames.CAUSE_FEAR, "V, S" },
        { SpellNames.CHAIN_LIGHTNING, "V, S, F" },
        { SpellNames.CHAOS_HAMMER, "V, S" },
        { SpellNames.CHARM_MONSTER, "V, S" },
        { SpellNames.CHARM_PERSON, "V, S" },
        { SpellNames.CHILL_TOUCH, "V, S" },
        { SpellNames.CIRCLE_OF_DEATH, "V, S, M" },
        { SpellNames.COLOR_SPRAY, "V, S, M" },
        { SpellNames.COMMAND, "V" },
        { SpellNames.COMMAND_PLANTS, "V" },
        { SpellNames.COMMAND_UNDEAD, "V, S, M" },
        { SpellNames.COMPREHEND_LANGUAGES, "V, S, M/DF" },
        { SpellNames.CONE_OF_COLD, "V, S, M/DF" },
        { SpellNames.CONFUSION, "V, S, M/DF" },
        { SpellNames.CONSECRATE, "V, S, M, DF" },
        { SpellNames.CONTAGION, "V, S" },
        { SpellNames.CONTINUAL_FLAME, "V, S, M" },
        { SpellNames.CREATE_WATER, "V, S" },
        { SpellNames.CRUSHING_DESPAIR, "V, S, M" },
        { SpellNames.CURE_CRITICAL_WOUNDS, "V, S" },
        { SpellNames.CURE_LIGHT_WOUNDS, "V, S" },
        { SpellNames.CURE_MINOR_WOUNDS, "V, S" },
        { SpellNames.CURE_MODERATE_WOUNDS, "V, S" },
        { SpellNames.CURE_SERIOUS_WOUNDS, "V, S" },
        { SpellNames.DANCING_LIGHTS, "V, S" },
        { SpellNames.DARKNESS, "V, M/DF" },
        { SpellNames.DARKVISION, "V, S, M" },
        { SpellNames.DAYLIGHT, "V, S" },
        { SpellNames.DAZE, "V, S, M" },
        { SpellNames.DAZE_MONSTER, "V, S, M" },
        { SpellNames.DEATHWATCH, "V, S" },
        { SpellNames.DEATH_KNELL, "V, S" },
        { SpellNames.DEATH_WARD, "V, S, DF" },
        { SpellNames.DEEP_SLUMBER, "V, S, M" },
        { SpellNames.DELAY_POISON, "V, S, DF" },
        { SpellNames.DESECRATE, "V, S, M, DF" },
        { SpellNames.DETECT_CHAOS, "V, S, DF" },
        { SpellNames.DETECT_EVIL, "V, S, DF" },
        { SpellNames.DETECT_GOOD, "V, S, DF" },
        { SpellNames.DETECT_LAW, "V, S, DF" },
        { SpellNames.DETECT_MAGIC, "V, S" },
        { SpellNames.DETECT_POISON, "V, S" },
        { SpellNames.DETECT_SECRET_DOORS, "V, S" },
        { SpellNames.DETECT_THOUGHTS, "V, S, F/DF" },
        { SpellNames.DETECT_UNDEAD, "V, S, M/DF" },
        { SpellNames.DIMENSIONAL_ANCHOR, "V, S" },
        { SpellNames.DIMENSION_DOOR, "V" },
        { SpellNames.DISGUISE_SELF, "V, S" },
        { SpellNames.DISINTEGRATE, "V, S, M/DF" },
        { SpellNames.DISMISSAL, "V, S, DF" },
        { SpellNames.DISPEL_MAGIC, "V, S" },
        { SpellNames.DISPLACEMENT, "V, M" },
        { SpellNames.DISRUPT_UNDEAD, "V, S" },
        { SpellNames.DIVINE_FAVOR, "V, S, DF" },
        { SpellNames.DIVINE_POWER, "V, S, DF" },
        { SpellNames.DOMINATE_ANIMAL, "V, S" },
        { SpellNames.DOMINATE_PERSON, "V, S" },
        { SpellNames.DOOM, "V, S, DF" },
        { SpellNames.EAGLES_SPLENDOR, "V, S, M/DF" },
        { SpellNames.EARTHQUAKE, "V, S, DF" },
        { SpellNames.ENDURE_ELEMENTS, "V, S" },
        { SpellNames.ENERVATION, "V, S" },
        { SpellNames.ENLARGE_PERSON, "V, S, M" },
        { SpellNames.ENTANGLE, "V, S, DF" },
        { SpellNames.ENTHRALL, "V, S" },
        { SpellNames.ENTROPIC_SHIELD, "V, S" },
        { SpellNames.ERASE, "V, S" },
        { SpellNames.EVARDS_BLACK_TENTACLES, "V, S, M" },
        { SpellNames.EXPEDITIOUS_RETREAT, "V, S" },
        { SpellNames.FALSE_LIFE, "V, S, M" },
        { SpellNames.FEAR, "V, S, M" },
        { SpellNames.FEATHER_FALL, "V" },
        { SpellNames.FIND_TRAPS, "V, S" },
        { SpellNames.FIREBALL, "V, S, M" },
        { SpellNames.FIRE_SHIELD, "V, S, M/DF" },
        { SpellNames.FLAME_ARROW, "V, S, M" },
        { SpellNames.FLAME_STRIKE, "V, S, DF" },
        { SpellNames.FLAMING_SPHERE, "V, S, M/DF" },
        { SpellNames.FLARE, "V" },
        { SpellNames.FLESH_TO_STONE, "V, S, M" },
        { SpellNames.FLOATING_DISK, "V, S, M" },
        { SpellNames.FOG_CLOUD, "V, S" },
        { SpellNames.FOXS_CUNNING, "V, S, M/DF" },
        { SpellNames.FREEDOM_OF_MOVEMENT, "V, S, M, DF" },
        { SpellNames.GENTLE_REPOSE, "V, S, M/DF" },
        { SpellNames.GHOST_SOUND, "V, S, M" },
        { SpellNames.GHOUL_TOUCH, "V, S, M" },
        { SpellNames.GIANT_VERMIN, "V, S, DF" },
        { SpellNames.GLITTERDUST, "V, S, M" },
        { SpellNames.GLOBE_OF_INVULNERABILITY, "V, S, M" },
        { SpellNames.GLYPH_OF_WARDING, "V, S, M" },
        { SpellNames.GREASE, "V, S, M" },
        { SpellNames.GREATER_INVISIBILITY, "V, S" },
        { SpellNames.GREATER_MAGIC_WEAPON, "V, S, M/DF" },
        { SpellNames.GREATER_RESTORATION, "V, S, XP" },
        { SpellNames.GUIDANCE, "V, S" },
        { SpellNames.GUST_OF_WIND, "V, S" },
        { SpellNames.HALT_UNDEAD, "V, S, M" },
        { SpellNames.HASTE, "V, S, M" },
        { SpellNames.HEAL, "V, S" },
        { SpellNames.HEAT_METAL, "V, S, DF" },
        { SpellNames.HEROISM, "V, S" },
        { SpellNames.HIDEOUS_LAUGHTER, "V, S, M" },
        { SpellNames.HIDE_FROM_UNDEAD, "V, S, DF" },
        { SpellNames.HOLD_ANIMAL, "V, S" },
        { SpellNames.HOLD_MONSTER, "V, S, M/DF" },
        { SpellNames.HOLD_PERSON, "V, S, F/DF" },
        { SpellNames.HOLD_PORTAL, "V" },
        { SpellNames.HOLY_SMITE, "V, S" },
        { SpellNames.HYPNOTIC_PATTERN, "V, S, M" },  // the verbal component is the bard's only
        { SpellNames.HYPNOTISM, "V, S" },
        { SpellNames.ICE_STORM, "V, S, M/DF" },
        { SpellNames.IDENTIFY, "V, S, M/DF" },
        { SpellNames.IMBUE_WITH_SPELL_ABILITY, "V, S, DF" },
        { SpellNames.INFLICT_CRITICAL_WOUNDS, "V, S" },
        { SpellNames.INFLICT_LIGHT_WOUNDS, "V, S" },
        { SpellNames.INFLICT_MINOR_WOUNDS, "V, S" },
        { SpellNames.INFLICT_MODERATE_WOUNDS, "V, S" },
        { SpellNames.INFLICT_SERIOUS_WOUNDS, "V, S" },
        { SpellNames.INSECT_PLAGUE, "V, S, DF" },
        { SpellNames.INVISIBILITY, "V, S, M/DF" },
        { SpellNames.INVISIBILITY_PURGE, "V, S" },
        { SpellNames.INVISIBILITY_SPHERE, "V, S, M" },
        { SpellNames.JUMP, "V, S, M" },
        { SpellNames.KEEN_EDGE, "V, S" },
        { SpellNames.KNOCK, "V" },
        { SpellNames.KNOW_DIRECTION, "V, S" },
        { SpellNames.LESSER_GLOBE_OF_INVULNERABILITY, "V, S, M" },
        { SpellNames.LESSER_RESTORATION, "V, S" },
        { SpellNames.LEVITATE, "V, S, F" },
        { SpellNames.LIGHT, "V, M/DF" },
        { SpellNames.LIGHTNING_BOLT, "V, S, M" },
        { SpellNames.LOCATE_OBJECT, "V, S, F/DF" },
        { SpellNames.LONGSTRIDER, "V, S, M" },
        { SpellNames.LULLABY, "V, S" },
        { SpellNames.MAGE_ARMOR, "V, S, F" },
        { SpellNames.MAGE_HAND, "V, S" },
        { SpellNames.MAGIC_CIRCLE_AGAINST_CHAOS, "V, S, M/DF" },
        { SpellNames.MAGIC_CIRCLE_AGAINST_EVIL, "V, S, M/DF" },
        { SpellNames.MAGIC_CIRCLE_AGAINST_GOOD, "V, S, M/DF" },
        { SpellNames.MAGIC_CIRCLE_AGAINST_LAW, "V, S, M/DF" },
        { SpellNames.MAGIC_FANG, "V, S, DF" },
        { SpellNames.MAGIC_MISSILE, "V, S" },
        { SpellNames.MAGIC_STONE, "V, S, DF" },
        { SpellNames.MAGIC_VESTMENT, "V, S, DF" },
        { SpellNames.MAGIC_WEAPON, "V, S, DF" },
        { SpellNames.MAKE_WHOLE, "V, S" },
        { SpellNames.MASS_ENLARGE_PERSON, "V, S, M" },
        { SpellNames.MASS_REDUCE_PERSON, "V, S, M" },
        { SpellNames.MASS_SUGGESTION, "V, M" },
        { SpellNames.MELFS_ACID_ARROW, "V, S, M, F" },
        { SpellNames.MENDING, "V, S" },
        { SpellNames.MESSAGE, "V, S, F" },
        { SpellNames.MIND_FOG, "V, S" },
        { SpellNames.MINOR_IMAGE, "V, S, F" },
        { SpellNames.MIRROR_IMAGE, "V, S" },
        { SpellNames.MISLEAD, "S" },
        { SpellNames.MOUNT, "V, S, M" },
        { SpellNames.NEUTRALIZE_POISON, "V, S, M/DF" },
        { SpellNames.NYSTULS_MAGIC_AURA, "V, S, F" },
        { SpellNames.OBSCURE_OBJECT, "V, S, M/DF" },
        { SpellNames.OBSCURING_MIST, "V, S" },
        { SpellNames.OPEN_CLOSE, "V, S, F" },
        { SpellNames.ORDERS_WRATH, "V, S" },
        { SpellNames.OWLS_WISDOM, "V, S, M/DF" },
        { SpellNames.PASSWALL, "V, S, M" },
        { SpellNames.PERSISTENT_IMAGE, "V, S, M" },
        { SpellNames.PHANTASMAL_KILLER, "V, S" },
        { SpellNames.PLANE_SHIFT, "V, S, F" },
        { SpellNames.PLANT_GROWTH, "V, S, DF" },
        { SpellNames.POISON, "V, S, DF" },
        { SpellNames.PRAYER, "V, S, DF" },
        { SpellNames.PRESTIDIGITATION, "V, S" },
        { SpellNames.PRODUCE_FLAME, "V, S" },
        { SpellNames.PROTECTION_FROM_ARROWS, "V, S, F" },
        { SpellNames.PROTECTION_FROM_CHAOS, "V, S, M/DF" },
        { SpellNames.PROTECTION_FROM_ENERGY, "V, S, DF" },
        { SpellNames.PROTECTION_FROM_EVIL, "V, S, M/DF" },
        { SpellNames.PROTECTION_FROM_GOOD, "V, S, M/DF" },
        { SpellNames.PROTECTION_FROM_LAW, "V, S, M/DF" },
        { SpellNames.PROTECTION_FROM_SPELLS, "V, S, M, F" },
        { SpellNames.PURIFY_FOOD_DRINK, "V, S" },
        { SpellNames.PYROTECHNICS, "V, S, M" },
        { SpellNames.RAGE, "V, S" },
        { SpellNames.RAINBOW_PATTERN, "V, S, M, F" },  // the verbal component is the bard's only
        { SpellNames.RAY_OF_ENFEEBLEMENT, "V, S" },
        { SpellNames.RAY_OF_EXHAUSTION, "V, S, M" },
        { SpellNames.RAY_OF_FROST, "V, S" },
        { SpellNames.READ_MAGIC, "V, S, F" },
        { SpellNames.REDUCE_PERSON, "V, S, M" },
        { SpellNames.REMOVE_BLINDNESS_DEAFNESS, "V, S" },
        { SpellNames.REMOVE_CURSE, "V, S" },
        { SpellNames.REMOVE_DISEASE, "V, S" },
        { SpellNames.REMOVE_FEAR, "V, S" },
        { SpellNames.REMOVE_PARALYSIS, "V, S" },
        { SpellNames.REPEL_VERMIN, "V, S, DF" },
        { SpellNames.RESILIENT_SPHERE, "V, S, M" },
        { SpellNames.RESISTANCE, "V, S, M/DF" },
        { SpellNames.RESIST_ENERGY, "V, S, DF" },
        { SpellNames.RESTORATION, "V, S, M" },
        { SpellNames.RESURRECTION, "V, S, M, DF" },
        { SpellNames.ROPE_TRICK, "V, S, M" },
        { SpellNames.SANCTUARY, "V, S, DF" },
        { SpellNames.SCARE, "V, S, M" },
        { SpellNames.SCORCHING_RAY, "V, S" },
        { SpellNames.SEARING_LIGHT, "V, S" },
        { SpellNames.SEE_INVISIBLE, "V, S, M" },
        { SpellNames.SHATTER, "V, S, M/DF" },
        { SpellNames.SHIELD, "V, S" },
        { SpellNames.SHIELD_OF_FAITH, "V, S, M" },
        { SpellNames.SHIELD_OF_LAW, "V, S, F" },
        { SpellNames.SHIELD_OTHER, "V, S, F" },
        { SpellNames.SHOCKING_GRASP, "V, S" },
        { SpellNames.SHOUT, "V" },
        { SpellNames.SHRINK_ITEM, "V, S" },
        { SpellNames.SILENCE, "V, S" },
        { SpellNames.SILENT_IMAGE, "V, S, F" },
        { SpellNames.SLEEP, "V, S, M" },
        { SpellNames.SLEET_STORM, "V, S, M/DF" },
        { SpellNames.SLOW, "V, S, M" },
        { SpellNames.SOFTEN_EARTH, "V, S, DF" },
        { SpellNames.SOLID_FOG, "V, S, M" },
        { SpellNames.SOUND_BURST, "V, S, F/DF" },
        { SpellNames.SPECTRAL_HAND, "V, S" },
        { SpellNames.SPELL_IMMUNITY, "V, S, DF" },
        { SpellNames.SPELL_TURNING, "V, S, M/DF" },
        { SpellNames.SPIDER_CLIMB, "V, S, M" },
        { SpellNames.SPIKE_STONES, "V, S, DF" },
        { SpellNames.SPIRITUAL_WEAPON, "V, S, DF" },
        { SpellNames.STATUS, "V, S" },
        { SpellNames.STINKING_CLOUD, "V, S, M" },
        { SpellNames.STONESKIN, "V, S, M" },
        { SpellNames.STONE_TO_FLESH, "V, S, M" },
        { SpellNames.SUMMON_MONSTER_1, "V, S, F/DF" },
        { SpellNames.SUMMON_MONSTER_2, "V, S, F/DF" },
        { SpellNames.SUMMON_MONSTER_3, "V, S, F/DF" },
        { SpellNames.SUMMON_MONSTER_4, "V, S, F/DF" },
        { SpellNames.SUMMON_NATURES_ALLY_1, "V, S, DF" },
        { SpellNames.SUMMON_NATURES_ALLY_2, "V, S, DF" },
        { SpellNames.SUMMON_NATURES_ALLY_3, "V, S, DF" },
        { SpellNames.SUMMON_NATURES_ALLY_4, "V, S, DF" },
        { SpellNames.SUMMON_SWARM, "V, S, M/DF" },
        { SpellNames.SUNBURST, "V, S, M/DF" },
        { SpellNames.TELEKINESIS, "V, S" },
        { SpellNames.TEST_CONE_30, "V, S" },  // test spell, no PHB entry
        { SpellNames.TEST_CONE_60, "V, S" },  // test spell, no PHB entry
        { SpellNames.TEST_LINE_60, "V, S" },  // test spell, no PHB entry
        { SpellNames.TOUCH_OF_FATIGUE, "V, S, M" },
        { SpellNames.TOUCH_OF_IDIOCY, "V, S" },
        { SpellNames.TRUE_SEEING, "V, S, M" },
        { SpellNames.TRUE_STRIKE, "V, F" },
        { SpellNames.UNHOLY_BLIGHT, "V, S" },
        { SpellNames.UNSEEN_SERVANT, "V, S, M" },
        { SpellNames.VAMPIRIC_TOUCH, "V, S" },
        { SpellNames.VENTRILOQUISM, "V, F" },
        { SpellNames.VIRTUE, "V, S, DF" },
        { SpellNames.WALL_OF_FIRE, "V, S, M/DF" },
        { SpellNames.WALL_OF_FORCE, "V, S, M" },
        { SpellNames.WALL_OF_ICE, "V, S, M" },
        { SpellNames.WALL_OF_THORNS, "V, S" },
        { SpellNames.WEB, "V, S, M" },
        { SpellNames.WHISPERING_WIND, "V, S" },
        { SpellNames.WIND_WALL, "V, S, M/DF" },
        { SpellNames.WISH, "V, XP" },
        { SpellNames.ZONE_OF_TRUTH, "V, S, DF" },
    };

    /// <summary>The spell ids that have a Components line.</summary>
    public static IEnumerable<string> SpellIds => Lines.Keys;

    /// <summary>The PHB Components line of <paramref name="spellId"/> ("V, S, M/DF"), or null.</summary>
    public static string Get(string spellId)
    {
        if (string.IsNullOrWhiteSpace(spellId))
            return null;
        return Lines.TryGetValue(spellId, out string line) ? line : null;
    }

    /// <summary>The components of a line, one token each ("V", "S", "M", "F", "DF", "M/DF", "F/DF", "XP").</summary>
    public static List<string> Tokens(string line)
    {
        var tokens = new List<string>();
        if (string.IsNullOrWhiteSpace(line))
            return tokens;
        foreach (string part in line.Split(','))
        {
            string token = part.Trim();
            if (token.Length > 0)
                tokens.Add(token);
        }
        return tokens;
    }

    /// <summary>
    /// Sets <paramref name="spell"/>'s material, focus and divine focus flags from its line: M (or M/DF) sets
    /// HasMaterialComponent, F (or F/DF) HasFocusComponent, DF, M/DF and F/DF HasDivineFocus, and M/DF and F/DF the split
    /// flags. False, with the flags left as they were, for a spell with no line (a new spell must add one; the
    /// SpellComponentRulesTests audit fails on it).
    /// </summary>
    public static bool Apply(SpellData spell)
    {
        if (spell == null)
            return false;
        string line = Get(spell.SpellId);
        if (line == null)
            return false;

        List<string> tokens = Tokens(line);
        bool materialOrDivine = tokens.Contains("M/DF");
        bool focusOrDivine = tokens.Contains("F/DF");
        spell.HasMaterialComponent = tokens.Contains("M") || materialOrDivine;
        spell.HasFocusComponent = tokens.Contains("F") || focusOrDivine;
        spell.HasDivineFocus = tokens.Contains("DF") || materialOrDivine || focusOrDivine;
        spell.MaterialOrDivineFocus = materialOrDivine;
        spell.FocusOrDivineFocus = focusOrDivine;
        return true;
    }
}
