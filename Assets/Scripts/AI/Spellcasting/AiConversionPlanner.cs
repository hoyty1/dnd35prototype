using System.Collections.Generic;

/// <summary>
/// The AI's spontaneous conversion (CHR-070; batch 12 step 24): a cleric gives up a prepared spell, other than a domain
/// spell, to cast a cure spell (a good cleric, or a neutral one that chose cure) or an inflict spell (an evil one, or a
/// neutral one that chose inflict) of the same level (PHB p.32). Tactics only: the candidate it gives is cast through the
/// same cast core as the PC's Convert button (SpellCastRequest.AiSourceFor builds the same SpontaneousConversion source
/// as GameManager.BuildSpellMenuCastSource), whose Legality stage checks the convertible slot and whose Spend stage gives
/// up the chosen spell (SpellcastingComponent.SpontaneousCastFromSpecificSpell), so PCs and NPCs share one rule.
/// <list type="bullet">
/// <item><see cref="Candidates"/> gives one candidate per spell level at which the cleric has a convertible prepared
/// spell and the cure or inflict spell of that level is not already castable as a prepared spell (the prepared one is
/// cast instead). Each is a clone of the spell carrying the conversion (SpellData.AiConversionLevel and
/// AiConversionSacrificeId); AIService.ScoreCastableSpells scores it with the other candidates and casts it only if it
/// scores best.</item>
/// <item>The sacrifice is the convertible spell the cleric holds the most copies of at that level, the first such slot on
/// a tie: the AI keeps the widest choice of spells. It does not yet weigh the worth of the spell it gives up (a stopgap
/// for the action catalog, docs/systems/tactics/README.md), and it converts without metamagic.</item>
/// <item>Same-level slots only: PHB p.32 also lets a cleric give up a higher-level prepared spell for a lower-level cure
/// or inflict spell, which this stopgap never offers (a cleric whose 1st-level slots are spent does not turn a 2nd-level
/// spell into Cure Light Wounds). A limit of the AI's choice, not of the shared rule (CHR-070).</item>
/// </list>
/// </summary>
public static class AiConversionPlanner
{
    /// <summary>The highest spell level a conversion can be (PHB p.32: cure and inflict spells up to 9th level).</summary>
    private const int MaxConversionLevel = 9;

    /// <summary>
    /// The conversion candidates of <paramref name="caster"/>: for each spell level with a convertible prepared spell, a
    /// clone of the cure or inflict spell of that level (SpellcastingComponent.GetSpontaneousSpell) marked with the level
    /// and the sacrifice, unless <paramref name="castable"/> already holds that spell. Empty for a caster that is not a
    /// cleric or has no spontaneous casting.
    /// </summary>
    public static List<SpellData> Candidates(CharacterController caster, IList<SpellData> castable)
    {
        var result = new List<SpellData>();
        SpellcastingComponent sc = caster != null ? caster.Spellcasting : null;
        if (sc == null || sc.Stats == null || !sc.Stats.IsCleric || sc.Stats.SpontaneousCasting == SpontaneousCastingType.None)
            return result;

        var prepared = new HashSet<string>();
        if (castable != null)
        {
            for (int i = 0; i < castable.Count; i++)
            {
                SpellData s = castable[i];
                if (s != null && !string.IsNullOrEmpty(s.SpellId) && !s.IsPreparedMetamagicSlotSpell)
                    prepared.Add(s.SpellId);
            }
        }

        for (int level = 0; level <= MaxConversionLevel; level++)
        {
            SpellData spont = sc.GetSpontaneousSpell(level);
            if (spont == null || prepared.Contains(spont.SpellId))
                continue;
            string sacrifice = ChooseSacrifice(sc.GetConvertiblePreparedSpellIds(level));
            if (sacrifice == null)
                continue;
            SpellData candidate = spont.Clone();
            candidate.AiConversionLevel = level;
            candidate.AiConversionSacrificeId = sacrifice;
            result.Add(candidate);
        }
        return result;
    }

    /// <summary>
    /// The prepared spell to give up among <paramref name="convertible"/> (one entry per convertible slot): the one held
    /// most often, the first on a tie; null when there is none.
    /// </summary>
    public static string ChooseSacrifice(IList<string> convertible)
    {
        if (convertible == null || convertible.Count == 0)
            return null;
        var counts = new Dictionary<string, int>();
        for (int i = 0; i < convertible.Count; i++)
        {
            string id = convertible[i];
            counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;
        }
        string best = null;
        int bestCount = 0;
        for (int i = 0; i < convertible.Count; i++)
        {
            string id = convertible[i];
            if (counts[id] > bestCount)
            {
                best = id;
                bestCount = counts[id];
            }
        }
        return best;
    }
}
