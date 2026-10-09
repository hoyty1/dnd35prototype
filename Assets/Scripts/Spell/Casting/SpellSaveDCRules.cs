using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What one save DC is made of (<see cref="SpellSaveDCRules.Explain(CharacterStats, SpellData, MetamagicData, string)"/>),
/// for combat logs, debug logs and tests.
/// </summary>
public struct SpellSaveDCBreakdown
{
    /// <summary>The save DC.</summary>
    public int Total;

    /// <summary>"spell", "spell-like", "item" or "fixed" (the spell carried its own DC: scroll, wand, stored or imbued spell).</summary>
    public string Source;

    /// <summary>The spell level used: the casting class's level for the spell (PHB p.177), or the heightened level.</summary>
    public int SpellLevel;

    /// <summary>The class whose key ability applies; null when the caster has no casting class.</summary>
    public string CastingClass;

    /// <summary>"INT", "WIS", "CHA", "best mental" (no casting class) or "min" (magic item rule).</summary>
    public string Ability;

    public int AbilityModifier;

    /// <summary>Spell Focus plus Greater Spell Focus for the spell's school (PHB p.100).</summary>
    public int SpellFocusBonus;

    /// <summary>Gnome +1 on illusion spells (PHB p.17).</summary>
    public int RacialBonus;

    public override string ToString()
    {
        if (Source == "fixed")
            return $"DC {Total} (fixed)";

        string text = $"DC {Total} = 10 + {SpellLevel} (level) + {AbilityModifier} ({Ability})";
        if (SpellFocusBonus != 0) text += $" + {SpellFocusBonus} (Spell Focus)";
        if (RacialBonus != 0) text += $" + {RacialBonus} (racial illusion)";
        if (!string.IsNullOrEmpty(CastingClass)) text += $" [{CastingClass}]";
        return text;
    }
}

/// <summary>
/// The one save DC rule for every cast path (SPL-001): the PC pipelines, the NPC pipeline, the custom spell
/// resolvers, scrolls, wands, staffs, stored and imbued spells and spell-like abilities all compute their DC here.
///
/// Spells (PHB p.177, "Saving Throw Difficulty Class"): 10 + the spell's level for the casting class + the caster's
/// key ability modifier for that class (Intelligence for a wizard; Charisma for a sorcerer or bard; Wisdom for a
/// cleric, druid, paladin or ranger; Wisdom for the DMG adept, DMG p.108). Heighten Spell raises the level used
/// (PHB p.95). Spell Focus and Greater Spell Focus add +1 each for their school (PHB p.94, p.100); a gnome adds +1 to
/// its illusion spells (PHB p.17).
/// Spell-like abilities (MM p.315): 10 + the level of the spell duplicated (the sorcerer/wizard version, else cleric,
/// druid, bard, paladin, ranger in that order) + the creature's Charisma modifier.
/// Magic items (DMG p.214): 10 + the spell level + the modifier of the minimum ability score needed to cast that level
/// (score 10 + level, so modifier level / 2). Staffs are the exception: the wielder's own DC (DMG p.214).
///
/// A spell that already carries a DC (<see cref="SpellData.SaveDC"/> &gt; 0: a scroll, wand, stored or imbued spell
/// clone) keeps it.
/// </summary>
public static class SpellSaveDCRules
{
    private enum KeyAbility { INT, WIS, CHA }

    private static readonly Dictionary<string, KeyAbility> KeyAbilityByClass =
        new Dictionary<string, KeyAbility>(StringComparer.OrdinalIgnoreCase)
        {
            { "Wizard", KeyAbility.INT },
            { "Sorcerer", KeyAbility.CHA },
            { "Bard", KeyAbility.CHA },
            { "Cleric", KeyAbility.WIS },
            { "Druid", KeyAbility.WIS },
            { "Paladin", KeyAbility.WIS },
            { "Ranger", KeyAbility.WIS },
            { "Adept", KeyAbility.WIS },
        };

    /// <summary>MM p.315: the class versions a spell-like ability duplicates, in order of preference.</summary>
    private static readonly string[] SpellLikeAbilityClassOrder = { "Sorcerer", "Wizard", "Cleric", "Druid", "Bard", "Paladin", "Ranger" };

    // ════════════════════════════════════════════════════════════
    //  Spells
    // ════════════════════════════════════════════════════════════

    /// <summary>The save DC of <paramref name="spell"/> cast by <paramref name="caster"/>.</summary>
    /// <param name="metamagic">The cast's metamagic (Heighten raises the level); falls back to <see cref="SpellData.MetamagicDataRef"/>.</param>
    /// <param name="castingClass">The class whose slot or spells known the cast uses; resolved from the caster when null.</param>
    public static int Compute(CharacterController caster, SpellData spell, MetamagicData metamagic = null, string castingClass = null)
        => Explain(caster, spell, metamagic, castingClass).Total;

    /// <inheritdoc cref="Compute(CharacterController, SpellData, MetamagicData, string)"/>
    public static int Compute(CharacterStats caster, SpellData spell, MetamagicData metamagic = null, string castingClass = null)
        => Explain(caster, spell, metamagic, castingClass).Total;

    /// <summary>
    /// As <see cref="Explain(CharacterStats, SpellData, MetamagicData, string)"/>, with the casting class from
    /// <see cref="SpellcastingComponent.GetCastingClassForSpellDC"/>: the class of the slot the caster spent on this
    /// spell, else of an unused prepared slot holding it, else the class lists.
    /// </summary>
    public static SpellSaveDCBreakdown Explain(CharacterController caster, SpellData spell, MetamagicData metamagic = null, string castingClass = null)
    {
        if (string.IsNullOrWhiteSpace(castingClass) && caster != null && spell != null && spell.SaveDC <= 0)
        {
            SpellcastingComponent spellComp = caster.Spellcasting;
            string slotClass = spellComp != null ? spellComp.GetCastingClassForSpellDC(spell) : null;
            if (!string.IsNullOrWhiteSpace(slotClass) && KeyAbilityByClass.ContainsKey(slotClass))
                castingClass = slotClass;
        }

        return Explain(caster != null ? caster.Stats : null, spell, metamagic, castingClass);
    }

    /// <summary>The save DC of <paramref name="spell"/> cast by <paramref name="caster"/>, with its parts.</summary>
    public static SpellSaveDCBreakdown Explain(CharacterStats caster, SpellData spell, MetamagicData metamagic = null, string castingClass = null)
    {
        var result = new SpellSaveDCBreakdown { Source = "spell" };
        if (spell == null)
        {
            result.Total = 10;
            return result;
        }

        if (spell.SaveDC > 0)
        {
            result.Source = "fixed";
            result.Total = spell.SaveDC;
            result.SpellLevel = spell.SpellLevel;
            return result;
        }

        string resolvedClass = ResolveCastingClass(caster, spell, castingClass);
        result.CastingClass = resolvedClass;
        result.SpellLevel = GetEffectiveSpellLevel(spell, resolvedClass, metamagic ?? spell.MetamagicDataRef);
        result.AbilityModifier = GetKeyAbilityModifier(caster, resolvedClass, out result.Ability);
        result.SpellFocusBonus = FeatManager.GetSpellFocusDCBonus(caster, spell.School);
        result.RacialBonus = GetRacialDCBonus(caster, spell);
        result.Total = 10 + result.SpellLevel + result.AbilityModifier + result.SpellFocusBonus + result.RacialBonus;
        return result;
    }

    /// <summary>
    /// The class a cast by <paramref name="caster"/> uses: <paramref name="explicitClass"/> when given; else, among the
    /// caster's casting classes, one whose spell list has the spell (the highest class level wins), else the highest
    /// casting class. Null when the caster has no casting class (a creature without class levels).
    /// </summary>
    public static string ResolveCastingClass(CharacterStats caster, SpellData spell, string explicitClass = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitClass))
            return explicitClass;
        if (caster == null)
            return null;

        caster.EnsureMulticlassDataInitialized();

        string best = null;
        int bestLevel = -1;
        bool bestOnList = false;
        List<ClassLevelEntry> levels = caster.ClassLevels;
        if (levels != null)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                ClassLevelEntry entry = levels[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ClassName) || !KeyAbilityByClass.ContainsKey(entry.ClassName))
                    continue;

                bool onList = spell != null && GetClassListLevel(spell, entry.ClassName) >= 0;
                if (best == null || (onList && !bestOnList) || (onList == bestOnList && entry.Level > bestLevel))
                {
                    best = entry.ClassName;
                    bestLevel = entry.Level;
                    bestOnList = onList;
                }
            }
        }

        if (best != null)
            return best;

        string legacyClass = (caster.CharacterClass ?? string.Empty).Trim();
        return KeyAbilityByClass.ContainsKey(legacyClass) ? legacyClass : null;
    }

    /// <summary>
    /// The key ability modifier for <paramref name="castingClass"/> (PHB p.177; DMG p.108 for the adept). Without a
    /// casting class (a creature casting with no class levels) the best of INT, WIS and CHA, as before SPL-001.
    /// </summary>
    public static int GetKeyAbilityModifier(CharacterStats caster, string castingClass, out string abilityName)
    {
        abilityName = "none";
        if (caster == null)
            return 0;

        if (!string.IsNullOrWhiteSpace(castingClass) && KeyAbilityByClass.TryGetValue(castingClass.Trim(), out KeyAbility ability))
        {
            switch (ability)
            {
                case KeyAbility.INT: abilityName = "INT"; return caster.INTMod;
                case KeyAbility.CHA: abilityName = "CHA"; return caster.CHAMod;
                default: abilityName = "WIS"; return caster.WISMod;
            }
        }

        abilityName = "best mental";
        return Mathf.Max(caster.INTMod, Mathf.Max(caster.WISMod, caster.CHAMod));
    }

    /// <summary>The key ability modifier of the class <paramref name="caster"/> would cast <paramref name="spell"/> with.</summary>
    public static int GetKeyAbilityModifier(CharacterStats caster, SpellData spell = null)
        => GetKeyAbilityModifier(caster, ResolveCastingClass(caster, spell), out _);

    /// <summary>
    /// The spell level for the DC: the casting class's level for the spell ("always use the spell level applicable to
    /// your class", PHB p.177), raised to the Heighten level when the cast is heightened (PHB p.95).
    /// </summary>
    public static int GetEffectiveSpellLevel(SpellData spell, string castingClass, MetamagicData metamagic)
    {
        if (spell == null)
            return 0;

        int level = spell.SpellLevel;
        if (!string.IsNullOrWhiteSpace(castingClass))
        {
            int classLevel = GetClassListLevel(spell, castingClass);
            if (classLevel < 0)
                classLevel = GetDomainLevel(spell, castingClass);
            if (classLevel >= 0)
                level = classLevel;
        }

        if (metamagic != null && metamagic.Has(MetamagicFeatId.HeightenSpell) && metamagic.HeightenToLevel > level)
            level = metamagic.HeightenToLevel;

        return level;
    }

    // ════════════════════════════════════════════════════════════
    //  Spell-like abilities (MM p.315)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// The save DC of a spell-like ability that duplicates <paramref name="spell"/>: 10 + the level of the
    /// sorcerer/wizard version (else cleric, druid, bard, paladin, ranger) + the creature's Charisma modifier (MM p.315).
    /// Spell Focus does not apply: a spell-like ability is not a spell.
    /// </summary>
    public static SpellSaveDCBreakdown ExplainSpellLikeAbility(CharacterStats creature, SpellData spell)
    {
        var result = new SpellSaveDCBreakdown { Source = "spell-like", Ability = "CHA" };
        if (spell == null)
        {
            result.Total = 10;
            return result;
        }

        result.SpellLevel = GetSpellLikeAbilityLevel(spell);
        result.AbilityModifier = creature != null ? creature.CHAMod : 0;
        result.Total = 10 + result.SpellLevel + result.AbilityModifier;
        return result;
    }

    /// <inheritdoc cref="ExplainSpellLikeAbility(CharacterStats, SpellData)"/>
    public static int ComputeSpellLikeAbility(CharacterStats creature, SpellData spell)
        => ExplainSpellLikeAbility(creature, spell).Total;

    /// <summary>MM p.315: the level of the sorcerer/wizard version, else cleric, druid, bard, paladin, ranger.</summary>
    public static int GetSpellLikeAbilityLevel(SpellData spell)
    {
        if (spell == null)
            return 0;
        for (int i = 0; i < SpellLikeAbilityClassOrder.Length; i++)
        {
            int level = GetClassListLevel(spell, SpellLikeAbilityClassOrder[i]);
            if (level >= 0)
                return level;
        }
        return spell.SpellLevel;
    }

    // ════════════════════════════════════════════════════════════
    //  Magic items (DMG p.214)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// The save DC of a spell from a scroll, wand or other magic item: 10 + spell level + the modifier of the minimum
    /// ability score needed to cast that level (10 + level, so level / 2) (DMG p.214). Use the heightened level for a
    /// heightened item spell.
    /// </summary>
    public static int ForMagicItem(int spellLevel)
    {
        int level = Mathf.Max(0, spellLevel);
        return 10 + level + level / 2;
    }

    private static readonly string[] ArcaneItemClasses = { "Wizard", "Sorcerer", "Bard" };
    private static readonly string[] DivineItemClasses = { "Cleric", "Druid", "Paladin", "Ranger" };

    /// <summary>
    /// The spell level of an arcane or divine item spell for its DC: the lowest level on an arcane list (Sor/Wiz,
    /// bard) or a divine list (cleric, druid, paladin, ranger), else <see cref="SpellData.SpellLevel"/>. So an arcane
    /// scroll never takes a divine-only lower level.
    /// </summary>
    public static int GetItemSpellLevel(SpellData spell, bool isArcane)
    {
        if (spell == null)
            return 0;
        string[] classes = isArcane ? ArcaneItemClasses : DivineItemClasses;
        int best = -1;
        for (int i = 0; i < classes.Length; i++)
        {
            int level = spell.GetSpellLevelFor(classes[i]);
            if (level >= 0 && (best < 0 || level < best))
                best = level;
        }
        return best >= 0 ? best : spell.SpellLevel;
    }

    // ════════════════════════════════════════════════════════════
    //  Helpers
    // ════════════════════════════════════════════════════════════

    /// <summary>The spell's level on <paramref name="className"/>'s list (Sor/Wiz share one list), or -1.</summary>
    private static int GetClassListLevel(SpellData spell, string className)
    {
        int level = spell.GetSpellLevelFor(className);
        if (level >= 0)
            return level;
        if (string.Equals(className, "Sorcerer", StringComparison.OrdinalIgnoreCase))
            return spell.GetSpellLevelFor("Wizard");
        if (string.Equals(className, "Wizard", StringComparison.OrdinalIgnoreCase))
            return spell.GetSpellLevelFor("Sorcerer");
        return -1;
    }

    /// <summary>The lowest domain level recorded for <paramref name="className"/> (a cleric's domain-only spell), or -1.</summary>
    private static int GetDomainLevel(SpellData spell, string className)
    {
        if (spell.AvailableFor == null)
            return -1;
        int best = -1;
        for (int i = 0; i < spell.AvailableFor.Count; i++)
        {
            SpellAvailability a = spell.AvailableFor[i];
            if (a == null || string.IsNullOrWhiteSpace(a.Domain) || !a.MatchesClass(className))
                continue;
            if (best < 0 || a.Level < best)
                best = a.Level;
        }
        return best;
    }

    /// <summary>
    /// PHB p.17: a gnome adds +1 to the DC of its illusion spells (a svirfneblin too, MM p.132), PC or NPC (CRE-038):
    /// <see cref="RaceData.IllusionSpellDCBonus"/>.
    /// </summary>
    private static int GetRacialDCBonus(CharacterStats caster, SpellData spell)
    {
        if (caster == null || caster.Race == null || spell == null || caster.Race.IllusionSpellDCBonus <= 0)
            return 0;
        return SpellSchoolUtils.Parse(spell.School) == SpellSchool.Illusion ? caster.Race.IllusionSpellDCBonus : 0;
    }
}
