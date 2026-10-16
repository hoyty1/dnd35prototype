using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One caster-level rule for every cast (batch 11 step 14, raw-resources-caster-level; SPL-017, docs/designs/
/// cast_core_batch11.md), the same for PCs and NPCs (docs/systems/PC_NPC_PARITY.md): the PC targeting highlight, the
/// NPC range test, the summon and swarm placement ranges, the area placement ranges, the generic buff path, the spell
/// durations (SpellDurationRules.CasterLevelFor), the generic spell dice (SpellDiceRules.CasterLevelFor), the handlers'
/// cast context (SpellCastingHelper.GetEffectiveCasterLevel) and SpellCaster.Cast's missile count and spell resistance
/// check all read it. Pure apart from <see cref="ForCaster"/>'s lookup of the open cast.
///
/// RAW applied:
/// - A scroll's or wand's spell works at the item's caster level, whoever uses it (DMG p.237, p.245; SPL-016).
/// - A class's caster level is its class level (PHB p.171), except the paladin's and the ranger's: none through 3rd
///   level, then one-half the class level (PHB p.44, p.48). <see cref="ClassCasterLevel"/>, which
///   CharacterStats.GetCasterLevel uses for every class, so every reader of a caster level gets it.
/// - The class whose slot or spells known the cast uses gives the caster level (PHB p.171: caster level is the level in
///   the casting class), the class the save DC uses too (SpellSaveDCRules.ResolveCastingClass).
/// - A domain's +1 caster level for spells of its descriptor (CharacterStats.GetDomainBoostedCasterLevel).
/// - Each negative level lowers the level used in a calculation by one (DMG p.293): the class level is lowered before
///   the class's caster level is worked out from it, so a paladin 4 with one negative level is an effective paladin 3
///   with no caster level (delegated RAW reading 2026-10-14, docs/systems/RULES_COVERAGE.md 4.1).
/// - A creature with no casting class casts at a caster level equal to its Hit Dice less its negative levels, plus a worn
///   item's caster level bonus: MM p.315 sets the Hit Dice as the default caster level of a creature's spell-like
///   abilities, and no text gives a classless caster any other (delegated RAW reading 2026-10-14,
///   docs/systems/RULES_COVERAGE.md 4.1).
/// - A cast always works at caster level 1 or more.
/// </summary>
public static class CasterLevelRules
{
    /// <summary>The first class level at which a paladin or ranger has a caster level (PHB p.44, p.48).</summary>
    public const int HalfCasterFirstLevel = 4;

    /// <summary>True for the classes whose caster level is one-half the class level: paladin and ranger (PHB p.44, p.48).</summary>
    public static bool IsHalfCasterClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return false;
        string name = className.Trim();
        return string.Equals(name, "Paladin", System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, "Ranger", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The caster level <paramref name="className"/> gives at <paramref name="effectiveClassLevel"/> (the class level less
    /// the negative levels): the class level, or for a paladin or ranger 0 through 3rd level and one-half the class level
    /// from 4th (PHB p.44, p.48). Never below 0.
    /// </summary>
    public static int ClassCasterLevel(string className, int effectiveClassLevel)
    {
        int level = Mathf.Max(0, effectiveClassLevel);
        if (!IsHalfCasterClass(className))
            return level;
        return level < HalfCasterFirstLevel ? 0 : level / 2;
    }

    /// <summary>
    /// The caster level of the cast <paramref name="req"/>: the item's caster level for a scroll or wand; otherwise the
    /// caster's (<see cref="ForCreature"/>). At least 1.
    /// </summary>
    public static int For(SpellCastRequest req)
    {
        if (req == null)
            return 1;
        int? itemCasterLevel = ItemCasterLevel(req.Source);
        if (itemCasterLevel.HasValue)
            return itemCasterLevel.Value;
        return ForCreature(req.Caster, req.Spell);
    }

    /// <summary>
    /// The caster level <paramref name="caster"/> casts <paramref name="spell"/> at: the open cast's (<see cref="For"/>,
    /// so a scroll or wand gives its own) when the caster's cast of this spell is open, else <see cref="ForCreature"/>.
    /// For the readers that have no request in hand (durations, handlers, SpellCaster.Cast). At least 1.
    /// </summary>
    public static int ForCaster(CharacterController caster, SpellData spell)
    {
        SpellCastRequest open = GameManager.Instance != null ? GameManager.Instance.OpenCastRequestFor(caster, spell) : null;
        if (open != null)
            return For(open);
        return ForCreature(caster, spell);
    }

    /// <summary>
    /// The creature's own caster level for <paramref name="spell"/>, with no item: the casting class's caster level
    /// (<see cref="CastingClassFor"/>, half for a paladin or ranger, less the negative levels, plus a worn item's caster
    /// level bonus) with the domain boost (CharacterStats.GetDomainBoostedCasterLevel); for a creature with no casting
    /// class its Hit Dice less its negative levels, plus a worn item's bonus (<see cref="HitDiceCasterLevel"/>). At least 1.
    /// </summary>
    public static int ForCreature(CharacterController caster, SpellData spell)
    {
        if (caster == null || caster.Stats == null)
            return 1;
        return ForStats(caster.Stats, spell, CastingClassFor(caster, spell));
    }

    /// <summary>
    /// As <see cref="ForCreature"/> for a caster known only by its stats (SpellDiceRules): the casting class is
    /// <paramref name="castingClass"/>, else resolved from the class lists (SpellSaveDCRules.ResolveCastingClass).
    /// </summary>
    public static int ForStats(CharacterStats stats, SpellData spell, string castingClass = null)
    {
        if (stats == null)
            return 1;

        if (!stats.IsSpellcaster)
            return HitDiceCasterLevel(stats);

        if (string.IsNullOrWhiteSpace(castingClass) || stats.GetClassLevel(castingClass) <= 0)
            castingClass = SpellSaveDCRules.ResolveCastingClass(stats, spell);

        int casterLevel = stats.GetDomainBoostedCasterLevel(spell, castingClass);
        return Mathf.Max(1, casterLevel);
    }

    /// <summary>
    /// The caster level of a creature with no casting class: its Hit Dice (MM p.315, the default caster level of a
    /// spell-like ability) less its negative levels (DMG p.293), plus a worn item's caster level bonus as
    /// CharacterStats.GetCasterLevel adds it to a class's (delegated RAW reading 2026-10-14). At least 1.
    /// </summary>
    public static int HitDiceCasterLevel(CharacterStats stats)
    {
        if (stats == null)
            return 1;
        int level = Mathf.Max(0, stats.GetHitDice() - stats.NegativeLevelCount);
        if (level > 0)
            level += Mathf.Max(0, stats.WondrousCasterLevelBonus);
        return Mathf.Max(1, level);
    }

    /// <summary>
    /// The class <paramref name="caster"/> casts <paramref name="spell"/> with: the class of the slot it spent on the spell
    /// or of an unused prepared slot holding it (SpellcastingComponent.GetCastingClassForSpellDC), else the class lists
    /// (SpellSaveDCRules.ResolveCastingClass), as the save DC resolves it. Null when the creature has no casting class.
    /// </summary>
    public static string CastingClassFor(CharacterController caster, SpellData spell)
    {
        if (caster == null || caster.Stats == null)
            return null;
        SpellcastingComponent spellComp = caster.Spellcasting;
        string slotClass = spellComp != null && spell != null ? spellComp.GetCastingClassForSpellDC(spell) : null;
        if (!string.IsNullOrWhiteSpace(slotClass) && caster.Stats.GetClassLevel(slotClass) > 0)
            return slotClass;
        return SpellSaveDCRules.ResolveCastingClass(caster.Stats, spell);
    }

    /// <summary>The item's caster level of a scroll or wand source (at least 1), or null for any other source.</summary>
    public static int? ItemCasterLevel(CastSource source)
    {
        if (source == null || !source.IsItem || !source.ItemCasterLevel.HasValue)
            return null;
        return Mathf.Max(1, source.ItemCasterLevel.Value);
    }

    /// <summary>
    /// The range in squares of the cast <paramref name="req"/> at its caster level (<see cref="For"/>;
    /// SpellData.GetRangeSquaresForCasterLevel): 0 or less for a touch or personal spell, as the spell data gives it.
    /// </summary>
    public static int RangeSquares(SpellCastRequest req)
    {
        if (req == null || req.Spell == null)
            return 0;
        return req.Spell.GetRangeSquaresForCasterLevel(For(req));
    }

    /// <summary>As <see cref="RangeSquares(SpellCastRequest)"/> at <see cref="ForCaster"/>'s caster level.</summary>
    public static int RangeSquares(CharacterController caster, SpellData spell)
    {
        if (spell == null)
            return 0;
        return spell.GetRangeSquaresForCasterLevel(ForCaster(caster, spell));
    }

    /// <summary>
    /// The distance in squares from <paramref name="caster"/>'s square to the nearest square <paramref name="target"/>
    /// occupies: a spell reaches a creature when any of its squares is in range, the measure the PC targeting
    /// highlight has always used (it lights every square of a large creature in range), now the NPC range test's too.
    /// </summary>
    public static int DistanceToTarget(CharacterController caster, CharacterController target)
    {
        if (caster == null || target == null)
            return int.MaxValue;
        List<Vector2Int> squares = target.GetOccupiedSquares();
        if (squares == null || squares.Count == 0)
            return SquareGridUtils.GetDistance(caster.GridPosition, target.GridPosition);
        int best = int.MaxValue;
        for (int i = 0; i < squares.Count; i++)
            best = Mathf.Min(best, SquareGridUtils.GetDistance(caster.GridPosition, squares[i]));
        return best;
    }

    /// <summary>
    /// True when <paramref name="target"/> is within the cast's range: <see cref="RangeSquares(SpellCastRequest)"/>, at
    /// least 1 square (a touch spell reaches an adjacent creature; RAW a touch reaches the caster's natural reach, PHB
    /// p.141 and p.137, so a Large or enlarged caster is refused a 10-ft touch: SPL-170), to the target's nearest square
    /// (<see cref="DistanceToTarget"/>).
    /// </summary>
    public static bool InRange(SpellCastRequest req, CharacterController target)
    {
        if (req == null || req.Caster == null || target == null)
            return false;
        int range = Mathf.Max(1, RangeSquares(req));
        return DistanceToTarget(req.Caster, target) <= range;
    }
}
