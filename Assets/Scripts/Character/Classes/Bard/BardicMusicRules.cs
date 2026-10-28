using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>What a bardic music action did: performed or refused (with the reason), the log, the standard action spent.</summary>
public sealed class BardicMusicResult
{
    public bool Performed;
    public string Refusal;
    public string Log;

    public static BardicMusicResult Refused(string reason) => new BardicMusicResult { Performed = false, Refusal = reason };
    public static BardicMusicResult Done(string log) => new BardicMusicResult { Performed = true, Log = log };
}

/// <summary>
/// The bard's bardic music for levels 1-8 (PHB p.29-30; CHR-020, CHR-026): one rules core that the PC Bardic Music button
/// (Combat/Special/BardicMusicSystem.cs) and the AI (AIProfile.ChooseBardicMusic) both call, so PCs and NPCs follow the
/// same rules (docs/systems/PC_NPC_PARITY.md).
///
/// General rules (PHB p.29): uses per day = bard level; each ability needs a bard level and Perform ranks (3 for
/// countersong, fascinate and inspire courage; 6 and 3rd level for inspire competence; 9 and 6th level for suggestion);
/// starting a performance is a standard action and spends one use however long it lasts (suggestion spends none);
/// fascinate and inspire competence need concentration (a standard action each round, else they end at the end of the
/// bard's turn); while performing, the bard casts no spells and activates no scroll or wand
/// (<see cref="BlocksSpellcasting"/>); a deaf bard fails 20% of the time and the use is still spent.
///
/// Delegated RAW readings 2026-10-14 (docs/systems/RULES_COVERAGE.md 4.1), for the owner to overrule:
/// one performance at a time (starting another ends the current one) and stopping costs no action; a performance also ends
/// when the bard cannot act, is silenced or is grappling (no supernatural or spell-like standard action in a grapple, the
/// paladin reading); "a nearby combat or other dangers" (fascinate) is a creature other than the bard hostile to the
/// target within 30 ft, a melee (one creature threatened by a foe; the bard's own threat does not count) within 30 ft of
/// it, a hostile action against it this round or the last, or fear (<see cref="NearbyCombatReason"/>); an obvious threat
/// (any weapon attack, a hostile spell, a maneuver against the target, on every attack path) breaks the fascination at
/// once, a potential threat (the combat noise of an attack nearby) makes the bard roll a new Perform check and gives a
/// new save against it with the first save's context; fascinate and suggestion are spell-like and subject to spell resistance at
/// caster level = bard level; making a suggestion is the round's concentration on the fascination; the suggestion's
/// language is understood by any creature of Intelligence 3 or more (the game has no languages); an NPC bard has the
/// class-skill maximum of Perform ranks (character level + 3), as the DMG's NPC bard (DMG p.113-114); a creature within
/// 30 ft of a countersinging bard may use his result in place of a save whether or not it hears him (hearing is required
/// only for the new save each round against an ongoing effect); an ally keeps Inspire Courage while he hears the song and
/// for 5 of the bard's rounds after he stops hearing it (deafened, silenced, unconscious), each ally on his own count;
/// Inspire Competence offers the skills rolled in a fight except Hide and Move Silently (stealth under a song is
/// self-defeating, the PHB's own example).
/// </summary>
public static class BardicMusicRules
{
    public const int RangeSquares30Ft = 6;
    public const int FascinateRangeSquares = 18;  // 90 ft
    public const int CountersongMaxRounds = 10;
    public const int CompetenceMaxRounds = 20;    // 2 minutes
    public const int DeafFailurePercent = 20;
    public const int SuggestionRoundsPerLevel = 600; // 1 hour per level
    /// <summary>Inspire Courage lasts 5 rounds after an ally stops hearing the song (PHB p.29).</summary>
    public const int LingerRounds = 5;
    public const string PerformSkill = "Perform";
    public const string FascinateSourceId = "bardic_fascinate";
    public const string SuggestionSourceId = "bardic_suggestion";
    public const string CompetenceBonusSource = "inspire_competence";
    /// <summary>DiceService context of the bard's Perform check (a scenario can force it).</summary>
    public const string PerformCheckContext = "Bardic music Perform check";
    /// <summary>DiceService context of a deaf bard's d% failure roll.</summary>
    public const string DeafFailureContext = "Bardic music deaf failure d%";
    public const string SpellcastingBlockedReason = "performing bardic music (no spells, scrolls or wands while performing, PHB p.29; stop the song first)";

    /// <summary>
    /// The skills Inspire Competence offers: the ones the game rolls during a fight (UI choice; the PHB lets the ally use
    /// any skill), without Hide and Move Silently: PHB p.29 names chanting to make a rogue move more quietly as
    /// self-defeating, and hiding while a song is aimed at you is the same (delegated RAW reading 2026-10-14). The bonus
    /// itself is a typed competence bonus on whichever skill is named.
    /// </summary>
    public static readonly string[] CompetenceSkills =
    {
        "Use Magic Device", "Escape Artist", "Balance", "Ride", "Spellcraft", "Listen", "Spot"
    };

    // ── Level, ranks, uses ─────────────────────────────────────────

    public static int BardLevel(CharacterStats stats) => stats != null ? stats.GetClassLevel("Bard") : 0;

    public static int PerformRanks(CharacterStats stats)
        => stats != null && stats.Skills != null && stats.Skills.TryGetValue(PerformSkill, out Skill s) && s != null ? s.Ranks : 0;

    /// <summary>The bard's music data, kept in step with his level, Charisma and ranks (null for a non-bard).</summary>
    public static BardicMusicData Data(CharacterStats stats)
    {
        if (stats == null || BardLevel(stats) < 1)
            return null;
        stats.EnsureBardicMusicInitialized();
        return stats.BardBardicMusic;
    }

    /// <summary>Whether the creature has <paramref name="ability"/>: bard level and Perform ranks (PHB p.29).</summary>
    public static bool Knows(CharacterStats stats, BardicAbility ability)
    {
        int level = BardLevel(stats);
        return level >= BardicAbilityInfo.GetMinLevel(ability) && PerformRanks(stats) >= BardicAbilityInfo.GetMinPerformRanks(ability);
    }

    /// <summary>Whether the creature has bardic music at all (a bard with 3 or more Perform ranks).</summary>
    public static bool HasBardicMusic(CharacterStats stats) => Knows(stats, BardicAbility.InspireCourage);

    /// <summary>The abilities the bard has that the game resolves (levels 1-8), in the PHB order.</summary>
    public static List<BardicAbility> AvailableAbilities(CharacterStats stats)
    {
        var list = new List<BardicAbility>();
        foreach (BardicAbility a in new[] { BardicAbility.Countersong, BardicAbility.Fascinate, BardicAbility.InspireCourage,
            BardicAbility.InspireCompetence, BardicAbility.Suggestion })
        {
            if (Knows(stats, a))
                list.Add(a);
        }
        return list;
    }

    public static int UsesPerDay(CharacterStats stats) => HasBardicMusic(stats) ? BardLevel(stats) : 0;

    public static int UsesLeft(CharacterStats stats)
    {
        BardicMusicData d = Data(stats);
        return d != null && HasBardicMusic(stats) ? d.RemainingUses : 0;
    }

    public static BardicAbility ActiveAbility(CharacterStats stats)
        => stats != null && stats.BardBardicMusic != null ? stats.BardBardicMusic.ActiveAbility : BardicAbility.None;

    public static bool IsPerforming(CharacterStats stats) => ActiveAbility(stats) != BardicAbility.None;

    /// <summary>
    /// While performing any bardic music the bard cannot cast spells or activate a magic item by spell completion or magic
    /// word (PHB p.29): the spell menu, the AI candidate list, the NPC executor, counterspelling and the scroll, wand and
    /// staff validators all ask this.
    /// </summary>
    public static bool BlocksSpellcasting(CharacterStats stats) => !s_ignorePerformanceBlock && IsPerforming(stats);

    [System.ThreadStatic] private static bool s_ignorePerformanceBlock;

    /// <summary>
    /// Whether the creature would have a spell to cast if it stopped performing (AI choice: a performing bard stops its
    /// song, which costs no action, to cast). Asks the same castable-spell test as the spell menu and the AI.
    /// </summary>
    public static bool HasCastableSpellIgnoringPerformance(CharacterController c)
    {
        SpellcastingComponent spells = c != null ? c.Spellcasting : null;
        if (spells == null || !spells.CanCastSpells)
            return false;
        s_ignorePerformanceBlock = true;
        try { return spells.HasAnyCastablePreparedSpell(); }
        finally { s_ignorePerformanceBlock = false; }
    }

    /// <summary>The Will DC of the bard's Suggestion: 10 + 1/2 bard level + Cha modifier (PHB p.29).</summary>
    public static int SuggestionDC(CharacterStats stats) => stats != null ? BardClass.BardicMusicDC(BardLevel(stats), stats.CHAMod) : 0;

    public static int RoundCap(BardicAbility ability, int bardLevel)
    {
        switch (ability)
        {
            case BardicAbility.Countersong: return CountersongMaxRounds;
            case BardicAbility.Fascinate: return Mathf.Max(1, bardLevel);
            case BardicAbility.InspireCompetence: return CompetenceMaxRounds;
            default: return int.MaxValue;
        }
    }

    // ── Who can act, hear and be reached ───────────────────────────

    private static int Distance(CharacterController a, CharacterController b) => a.GetMinimumDistanceToTarget(b, chebyshev: false);

    private static bool IsUp(CharacterController c) => c != null && c.Stats != null && !c.IsDead && !c.IsUnconscious;

    /// <summary>
    /// Whether <paramref name="listener"/> can hear <paramref name="bard"/>'s music: conscious, not deafened, and neither
    /// of them silenced (no range limit for inspire courage, which gives none; PHB p.29).
    /// </summary>
    public static bool CanHear(CharacterController listener, CharacterController bard)
    {
        if (!IsUp(listener) || bard == null || bard.Stats == null)
            return false;
        if (listener.HasCondition(CombatConditionType.Deafened))
            return false;
        return !listener.Stats.SilenceActive && !bard.Stats.SilenceActive;
    }

    /// <summary>Whether the bard can perform at all now (null), or why not.</summary>
    public static string PerformProblem(CharacterController bard)
    {
        if (bard == null || bard.Stats == null || bard.IsDead)
            return "Dead";
        if (!bard.CanTakeTurnActions() || bard.IsUnconscious)
            return "Cannot act";
        if (bard.Stats.SilenceActive)
            return "Silenced (no sound)";
        if (bard.IsGrappling())
            return "Grappling (bardic music is not a grapple action)";
        if (bard.HasCondition(CombatConditionType.Fascinated) || bard.HasCondition(CombatConditionType.Paralyzed)
            || bard.HasCondition(CombatConditionType.Stunned) || bard.HasCondition(CombatConditionType.Dazed)
            || bard.HasCondition(CombatConditionType.Confused) || bard.HasCondition(CombatConditionType.Asleep)
            || bard.HasCondition(CombatConditionType.Petrified) || bard.HasCondition(CombatConditionType.Helpless))
            return "Cannot perform in this condition";
        return null;
    }

    /// <summary>Whether the bard can start <paramref name="ability"/> now (standard action, a use left), or why not.</summary>
    public static bool CanStart(CharacterController bard, BardicAbility ability, out string reason)
    {
        reason = null;
        CharacterStats s = bard != null ? bard.Stats : null;
        if (BardLevel(s) < 1) { reason = "No bardic music"; return false; }
        if (!BardicAbilityInfo.IsImplemented(ability) || ability == BardicAbility.Suggestion)
        {
            reason = ability == BardicAbility.Suggestion ? "Suggestion is made on a fascinated creature" : "Not implemented";
            return false;
        }
        if (BardLevel(s) < BardicAbilityInfo.GetMinLevel(ability)) { reason = $"Needs bard level {BardicAbilityInfo.GetMinLevel(ability)}"; return false; }
        if (PerformRanks(s) < BardicAbilityInfo.GetMinPerformRanks(ability)) { reason = $"Needs {BardicAbilityInfo.GetMinPerformRanks(ability)} Perform ranks"; return false; }
        string problem = PerformProblem(bard);
        if (problem != null) { reason = problem; return false; }
        if (UsesLeft(s) <= 0) { reason = "No uses left today"; return false; }
        if (bard.Actions == null || !bard.Actions.HasStandardAction) { reason = "No standard action"; return false; }
        return true;
    }

    /// <summary>Whether the bard can spend this turn's standard action concentrating on the performance.</summary>
    public static bool CanConcentrate(CharacterController bard, out string reason)
    {
        reason = null;
        BardicMusicData d = bard != null ? Data(bard.Stats) : null;
        if (d == null || !d.IsPerforming || !BardicAbilityInfo.RequiresConcentration(d.ActiveAbility)) { reason = "No performance needs concentration"; return false; }
        if (d.ConcentratedThisTurn) { reason = "Already concentrated this turn"; return false; }
        string problem = PerformProblem(bard);
        if (problem != null) { reason = problem; return false; }
        if (bard.Actions == null || !bard.Actions.HasStandardAction) { reason = "No standard action"; return false; }
        return true;
    }

    /// <summary>
    /// Why a nearby combat or another danger keeps <paramref name="target"/> from paying attention to a fascinating song
    /// (PHB p.29), or null. Delegated RAW reading 2026-10-14: a creature hostile to it within 30 ft; a melee within 30 ft
    /// of it (a creature there threatened by a foe of its own); a hostile action against it (a weapon attack, a hostile
    /// spell, a maneuver) this round or the last; or fear. The performing <paramref name="bard"/> is the source of the
    /// song, not a distraction: he counts neither as the hostile creature nor as the foe making a melee (a foe threatening
    /// the bard himself does count), so his 90-ft range is not cut to the space beyond 30 ft.
    /// </summary>
    public static string NearbyCombatReason(CharacterController target, IList<CharacterController> all, int lastHostileActionRound = -100, int currentRound = 0,
        CharacterController bard = null)
    {
        if (target == null || target.Stats == null)
            return "no creature";
        if (target.HasCondition(CombatConditionType.Frightened) || target.HasCondition(CombatConditionType.Panicked)
            || target.HasCondition(CombatConditionType.Shaken))
            return "afraid (another danger)";
        if (lastHostileActionRound >= 0 && currentRound - lastHostileActionRound <= 1)
            return "it was attacked or threatened this round or the last";
        if (all == null)
            return null;

        List<CharacterController> live = new List<CharacterController>();
        for (int i = 0; i < all.Count; i++)
            if (IsUp(all[i]))
                live.Add(all[i]);

        for (int i = 0; i < live.Count; i++)
        {
            CharacterController c = live[i];
            if (c == target || c == bard)
                continue;
            if (TeamUtility.IsEnemy(target, c) && Distance(target, c) <= RangeSquares30Ft)
                return $"{c.Stats.CharacterName}, a foe, is within 30 ft";
        }

        for (int i = 0; i < live.Count; i++)
        {
            CharacterController c = live[i];
            if (c != target && Distance(target, c) > RangeSquares30Ft)
                continue;
            List<CharacterController> threats = ThreatSystem.GetThreateningEnemies(c.GridPosition, c, live);
            if (threats == null)
                continue;
            for (int k = 0; k < threats.Count; k++)
                if (threats[k] != null && threats[k] != bard)
                    return $"a melee within 30 ft ({threats[k].Stats.CharacterName} threatens {c.Stats.CharacterName})";
        }
        return null;
    }

    /// <summary>Whether <paramref name="target"/> can pay attention to anything (PHB p.29: "able to pay attention").</summary>
    private static bool CanPayAttention(CharacterController target)
    {
        if (!IsUp(target) || !target.CanTakeTurnActions())
            return false;
        return !(target.HasCondition(CombatConditionType.Confused) || target.HasCondition(CombatConditionType.Dazed)
            || target.HasCondition(CombatConditionType.Stunned) || target.HasCondition(CombatConditionType.Paralyzed)
            || target.HasCondition(CombatConditionType.Asleep) || target.HasCondition(CombatConditionType.Helpless)
            || target.HasCondition(CombatConditionType.Petrified) || target.HasCondition(CombatConditionType.Nauseated));
    }

    /// <summary>Why <paramref name="bard"/> cannot fascinate <paramref name="target"/> now (null when he can; PHB p.29).</summary>
    public static string FascinateTargetProblem(CharacterController bard, CharacterController target, IList<CharacterController> all)
    {
        if (target == null || target.Stats == null || target == bard)
            return "not another creature";
        if (!CanPayAttention(target))
            return "cannot pay attention";
        if (Distance(bard, target) > FascinateRangeSquares)
            return "beyond 90 ft";
        if (!target.CanVisuallyPerceive(bard))
            return "cannot see the bard";
        if (!bard.CanVisuallyPerceive(target))
            return "the bard cannot see it";
        if (!CanHear(target, bard))
            return "cannot hear the bard";
        if (SpellUtilities.IsImmuneToMindAffecting(target))
            return "immune to mind-affecting effects";
        if (target.HasCondition(CombatConditionType.Fascinated))
            return "already fascinated";
        BardicMusicData d = Data(bard.Stats);
        if (d != null && d.FascinateResistedToday.Contains(target))
            return "saved against this bard's Fascinate today (24 hours)";
        GameManager gm = GameManager.Instance;
        int last = gm != null ? gm.LastHostileActionRoundAgainst(target) : -100;
        int round = gm != null ? gm.CurrentRound : 0;
        string busy = NearbyCombatReason(target, all, last, round, bard);
        return busy != null ? "distracted: " + busy : null;
    }

    /// <summary>The creatures (not the bard's allies) the bard could fascinate now.</summary>
    public static List<CharacterController> FascinateCandidates(CharacterController bard, IList<CharacterController> all)
    {
        var list = new List<CharacterController>();
        if (bard == null || all == null)
            return list;
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController c = all[i];
            if (c == null || c == bard || TeamUtility.IsAlly(bard, c))
                continue;
            if (FascinateTargetProblem(bard, c, all) == null)
                list.Add(c);
        }
        return list;
    }

    /// <summary>Why <paramref name="bard"/> cannot inspire competence in <paramref name="ally"/> now (null when he can; PHB p.29).</summary>
    public static string CompetenceTargetProblem(CharacterController bard, CharacterController ally)
    {
        if (ally == null || ally.Stats == null)
            return "no ally";
        if (ally == bard)
            return "not on himself";
        if (!TeamUtility.IsAlly(bard, ally))
            return "not an ally";
        if (!IsUp(ally))
            return "cannot hear";
        if (Distance(bard, ally) > RangeSquares30Ft)
            return "beyond 30 ft";
        if (!ally.CanVisuallyPerceive(bard) || !CanHear(ally, bard))
            return "cannot see and hear the bard";
        if (!bard.CanVisuallyPerceive(ally))
            return "the bard cannot see it";
        if (SpellUtilities.IsImmuneToMindAffecting(ally))
            return "immune to mind-affecting effects";
        return null;
    }

    /// <summary>The allies the bard could inspire with competence now.</summary>
    public static List<CharacterController> CompetenceCandidates(CharacterController bard, IList<CharacterController> all)
    {
        var list = new List<CharacterController>();
        if (bard == null || all == null)
            return list;
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && all[i] != bard && CompetenceTargetProblem(bard, all[i]) == null)
                list.Add(all[i]);
        return list;
    }

    /// <summary>Whether <paramref name="c"/> hears and benefits from the bard's Inspire Courage (allies and himself; mind-affecting).</summary>
    public static bool IsInspireCourageRecipient(CharacterController bard, CharacterController c)
    {
        if (c == null || bard == null || (c != bard && !TeamUtility.IsAlly(bard, c)))
            return false;
        if (!CanHear(c, bard))
            return false;
        return !SpellUtilities.IsImmuneToMindAffecting(c);
    }

    /// <summary>Why the bard cannot make a suggestion to <paramref name="target"/> now (null when he can; PHB p.29).</summary>
    public static string SuggestionTargetProblem(CharacterController bard, CharacterController target)
    {
        CharacterStats s = bard != null ? bard.Stats : null;
        if (BardLevel(s) < BardicAbilityInfo.GetMinLevel(BardicAbility.Suggestion))
            return "Needs bard level 6";
        if (PerformRanks(s) < BardicAbilityInfo.GetMinPerformRanks(BardicAbility.Suggestion))
            return "Needs 9 Perform ranks";
        BardicMusicData d = Data(s);
        if (d == null || d.ActiveAbility != BardicAbility.Fascinate)
            return "Not fascinating anyone";
        if (target == null || !d.FascinatedTargets.Contains(target) || !IsFascinatedBy(target, bard))
            return "Not fascinated by this bard";
        if (IsSuggestedBy(target, bard))
            return "Already following this bard's suggestion";
        if (target.Stats.INT < 3)
            return "Cannot understand the suggestion (language-dependent; Int 2 or less)";
        string problem = PerformProblem(bard);
        if (problem != null)
            return problem;
        if (bard.Actions == null || !bard.Actions.HasStandardAction)
            return "No standard action";
        return null;
    }

    // ── Conditions through the GameManager's condition service ─────

    private static List<ConditionService.ActiveCondition> ConditionsOf(CharacterController c)
    {
        GameManager gm = GameManager.Instance;
        return gm != null ? gm.GetActiveConditions(c) : new List<ConditionService.ActiveCondition>();
    }

    public static bool IsFascinatedBy(CharacterController target, CharacterController bard)
    {
        if (target == null || !target.HasCondition(CombatConditionType.Fascinated))
            return false;
        foreach (ConditionService.ActiveCondition c in ConditionsOf(target))
        {
            if (c == null || ConditionRules.Normalize(c.Type) != CombatConditionType.Fascinated)
                continue;
            if (c.SourceId == FascinateSourceId && (c.Source == bard || (c.Data is FascinatedConditionData fd && fd.Caster == bard)))
                return true;
        }
        return false;
    }

    public static bool IsSuggestedBy(CharacterController target, CharacterController bard)
    {
        if (target == null || !target.HasCondition(CombatConditionType.Suggested))
            return false;
        foreach (ConditionService.ActiveCondition c in ConditionsOf(target))
        {
            if (c == null || ConditionRules.Normalize(c.Type) != CombatConditionType.Suggested)
                continue;
            if (c.Source == bard || (c.Data is SuggestedConditionData sd && sd.Caster == bard))
                return true;
        }
        return false;
    }

    private static void Apply(CharacterController target, CombatConditionType type, int rounds, CharacterController source, object data,
        string name, string sourceId)
    {
        GameManager gm = GameManager.Instance;
        if (gm != null)
            gm.ApplyCondition(target, type, rounds, source: source, data: data, sourceNameOverride: name, sourceCategory: "BardicMusic", sourceId: sourceId);
        else
            target.ApplyCondition(type, rounds, source != null && source.Stats != null ? source.Stats.CharacterName : name);
    }

    private static void Remove(CharacterController target, CombatConditionType type)
    {
        GameManager gm = GameManager.Instance;
        if (gm != null)
            gm.RemoveCondition(target, type);
        else
            target.RemoveConditionDirect(type);
    }

    // ── Checks ─────────────────────────────────────────────────────

    /// <summary>The bard's Perform check: d20 + his Perform modifier (ranks, Charisma and every skill bonus).</summary>
    public static int RollPerformCheck(CharacterController bard)
    {
        int roll = DiceService.D20(PerformCheckContext);
        return roll + (bard != null && bard.Stats != null ? bard.Stats.GetSkillBonus(PerformSkill) : 0);
    }

    /// <summary>A deaf bard's 20% chance to fail (PHB p.29); true when the attempt fails.</summary>
    private static bool DeafFailure(CharacterController bard, StringBuilder log)
    {
        if (bard == null || !bard.HasCondition(CombatConditionType.Deafened))
            return false;
        int roll = DiceService.Roll(1, 100, DeafFailureContext);
        if (roll > DeafFailurePercent)
            return false;
        log.Append($" Deafened, the bard fails (d% {roll} ≤ {DeafFailurePercent}).");
        return true;
    }

    private static SaveContext EnchantmentContext(string label, bool languageDependent)
        => new SaveContext(spell: true, enchantment: true, label: label, sonicOrLanguageDependent: languageDependent);

    /// <summary>
    /// The context of every save against the bard's Fascinate, the first and each new one a potential threat gives
    /// (GameManager.TryDisturbFascinatedTarget): a spell-like enchantment (compulsion), so the dwarf's, the elf's and
    /// Still Mind's bonuses apply (PHB p.29; SaveRules).
    /// </summary>
    public static SaveContext FascinateSaveContext() => EnchantmentContext("Fascinate", languageDependent: false);

    // ── Starting, keeping and ending a performance ─────────────────

    /// <summary>
    /// Starts <paramref name="ability"/> (countersong, fascinate, inspire courage or inspire competence): one use and the
    /// standard action. <paramref name="targets"/>: the creatures to fascinate (up to 1 + 1 per 3 levels above 1st), or
    /// the one ally to inspire with competence (with <paramref name="skill"/>). A current performance ends first.
    /// </summary>
    public static BardicMusicResult Start(CharacterController bard, BardicAbility ability, IList<CharacterController> targets,
        string skill, IList<CharacterController> all)
    {
        if (!CanStart(bard, ability, out string reason))
            return BardicMusicResult.Refused(reason);
        BardicMusicData d = Data(bard.Stats);
        string name = bard.Stats.CharacterName;

        // The targets are checked before anything is spent.
        var chosen = new List<CharacterController>();
        if (ability == BardicAbility.Fascinate)
        {
            if (targets == null || targets.Count == 0)
                return BardicMusicResult.Refused("No creature to fascinate");
            int cap = d.FascinateTargetCount;
            for (int i = 0; i < targets.Count; i++)
            {
                CharacterController t = targets[i];
                if (t == null || chosen.Contains(t))
                    continue;
                string problem = FascinateTargetProblem(bard, t, all);
                if (problem != null)
                    return BardicMusicResult.Refused($"{t.Stats.CharacterName}: {problem}");
                chosen.Add(t);
            }
            if (chosen.Count > cap)
                return BardicMusicResult.Refused($"At most {cap} creature(s) at bard level {d.BardLevel}");
        }
        else if (ability == BardicAbility.InspireCompetence)
        {
            CharacterController ally = targets != null && targets.Count > 0 ? targets[0] : null;
            string problem = CompetenceTargetProblem(bard, ally);
            if (problem != null)
                return BardicMusicResult.Refused(problem);
            if (string.IsNullOrWhiteSpace(skill))
                return BardicMusicResult.Refused("No skill chosen");
            chosen.Add(ally);
        }

        var log = new StringBuilder();
        if (d.IsPerforming)
            log.Append(End(bard, "starts another song", all)).Append(' ');

        d.SpendUse();
        bard.CommitStandardAction(); // checked above; a disabled bard takes 1 damage once the act is done (PHB p.145)
        string uses = $"({d.RemainingUses}/{d.MaxUsesPerDay} uses left)";
        if (DeafFailure(bard, log))
            return BardicMusicResult.Done($"{name} tries to begin {BardicAbilityInfo.GetDisplayName(ability)}.{log} {uses}");

        d.BeginActive(ability);
        switch (ability)
        {
            case BardicAbility.InspireCourage:
            {
                d.InspireCourageValue = d.InspireCourageBonus;
                d.InspiredAllies.Clear();
                d.NotHearingRounds.Clear();
                if (all != null)
                    for (int i = 0; i < all.Count; i++)
                        if (IsInspireCourageRecipient(bard, all[i]))
                            d.InspiredAllies.Add(all[i]);
                if (!d.InspiredAllies.Contains(bard) && IsInspireCourageRecipient(bard, bard))
                    d.InspiredAllies.Add(bard);
                RefreshInspireCourage(d.InspiredAllies, all);
                log.Insert(0, $"{name} begins Inspire Courage: +{d.InspireCourageValue} morale on attack and weapon damage rolls and on saves against charm and fear for {d.InspiredAllies.Count} creature(s) who hear the song. ");
                break;
            }
            case BardicAbility.Countersong:
            {
                d.LastPerformCheck = RollPerformCheck(bard);
                log.Insert(0, $"{name} begins a countersong (Perform {d.LastPerformCheck}): creatures within 30 ft may use it in place of saves against sonic or language-dependent magic. ");
                log.Append(CountersongOngoingEffects(bard, all));
                break;
            }
            case BardicAbility.InspireCompetence:
            {
                CharacterController ally = chosen[0];
                d.CompetenceAlly = ally;
                d.CompetenceSkill = skill;
                ally.Stats.Bonuses.Set(CompetenceBonusSource, BonusTarget.Skill, BonusType.Competence, 2, "Inspire Competence", skill);
                log.Insert(0, $"{name} inspires competence in {ally.Stats.CharacterName}: +2 competence bonus on {skill} checks while {name} concentrates. ");
                break;
            }
            case BardicAbility.Fascinate:
            {
                d.LastPerformCheck = RollPerformCheck(bard);
                int casterLevel = d.BardLevel;
                log.Insert(0, $"{name} performs to fascinate (Perform {d.LastPerformCheck} = the Will DC): ");
                for (int i = 0; i < chosen.Count; i++)
                {
                    CharacterController t = chosen[i];
                    string tName = t.Stats.CharacterName;
                    SRResult sr = SpellSaveResolver.RollSpellResistance(bard, t, casterLevel);
                    if (!sr.Overcame)
                    {
                        log.Append($"{tName}: spell resistance {sr.TargetSR} holds (d20 {sr.Roll} + {casterLevel} = {sr.Total}). ");
                        continue;
                    }
                    SaveResult save = SpellSaveResolver.RollSave(t, SaveType.Will, d.LastPerformCheck, FascinateSaveContext());
                    if (save.Saved)
                    {
                        d.FascinateResistedToday.Add(t);
                        log.Append($"{tName} saves (Will {save.Total} vs {d.LastPerformCheck}) and cannot be fascinated by {name} for 24 hours. ");
                        continue;
                    }
                    var data = new FascinatedConditionData
                    {
                        Caster = bard,
                        CasterName = name,
                        RemainingRounds = d.BardLevel,
                        DisturbanceSaveDC = d.LastPerformCheck,
                        SourceSpellId = null,
                        SourceEffectName = "Fascinate (bardic music)"
                    };
                    Apply(t, CombatConditionType.Fascinated, d.BardLevel, bard, data, "Fascinate", FascinateSourceId);
                    d.FascinatedTargets.Add(t);
                    log.Append($"{tName} is fascinated (Will {save.Total} vs {d.LastPerformCheck}). ");
                }
                if (d.FascinatedTargets.Count == 0)
                {
                    d.StopPerformance();
                    log.Append("No creature is fascinated; the song ends.");
                }
                break;
            }
        }
        log.Append(' ').Append(uses);
        return BardicMusicResult.Done(log.ToString().Trim());
    }

    /// <summary>Spends the standard action keeping up fascinate or inspire competence this round.</summary>
    public static BardicMusicResult Concentrate(CharacterController bard)
    {
        if (!CanConcentrate(bard, out string reason))
            return BardicMusicResult.Refused(reason);
        BardicMusicData d = Data(bard.Stats);
        bard.CommitStandardAction(); // checked above; a disabled bard takes 1 damage once the act is done (PHB p.145)
        d.ConcentratedThisTurn = true;
        return BardicMusicResult.Done($"{bard.Stats.CharacterName} keeps up {BardicAbilityInfo.GetDisplayName(d.ActiveAbility)} (concentration).");
    }

    /// <summary>
    /// The bard's Suggestion (Sp, PHB p.29) on a creature he has fascinated: spell resistance (bard level), then Will DC
    /// 10 + 1/2 bard level + Cha modifier (a countersong may replace the save; it is language-dependent). A failed save
    /// sets the course "leave the fight" for 1 hour per bard level. No daily use; the standard action is the round's
    /// concentration on the fascination, which goes on.
    /// </summary>
    public static BardicMusicResult Suggest(CharacterController bard, CharacterController target)
    {
        string problem = SuggestionTargetProblem(bard, target);
        if (problem != null)
            return BardicMusicResult.Refused(problem);
        BardicMusicData d = Data(bard.Stats);
        string name = bard.Stats.CharacterName;
        string tName = target.Stats.CharacterName;
        bard.CommitStandardAction(); // checked above; a disabled bard takes 1 damage once the act is done (PHB p.145)
        d.ConcentratedThisTurn = true;

        var log = new StringBuilder($"{name} makes a suggestion to {tName}: ");
        if (DeafFailure(bard, log))
            return BardicMusicResult.Done(log.ToString());

        SRResult sr = SpellSaveResolver.RollSpellResistance(bard, target, d.BardLevel);
        if (!sr.Overcame)
            return BardicMusicResult.Done(log.Append($"spell resistance {sr.TargetSR} holds (d20 {sr.Roll} + {d.BardLevel} = {sr.Total}).").ToString());

        int dc = SuggestionDC(bard.Stats);
        SaveResult save = SpellSaveResolver.RollSave(target, SaveType.Will, dc, EnchantmentContext("Suggestion", languageDependent: true));
        if (save.Saved)
            return BardicMusicResult.Done(log.Append($"Will {save.Total} vs DC {dc}: resisted. The fascination goes on.").ToString());

        var data = new SuggestedConditionData
        {
            Caster = bard,
            CasterName = name,
            Course = SuggestedCourse.LeaveTheFight,
            SaveDC = dc,
            SourceEffectName = "Suggestion (bardic music)"
        };
        Apply(target, CombatConditionType.Suggested, SuggestionRoundsPerLevel * Mathf.Max(1, d.BardLevel), bard, data, "Suggestion", SuggestionSourceId);
        d.SuggestedTargets.Add(target);
        return BardicMusicResult.Done(log.Append($"Will {save.Total} vs DC {dc}: {tName} will leave the fight once the song releases it.").ToString());
    }

    /// <summary>Stops the current performance (no action). Inspire Courage lingers 5 rounds; the rest end at once.</summary>
    public static BardicMusicResult Stop(CharacterController bard, IList<CharacterController> all)
    {
        BardicMusicData d = bard != null ? Data(bard.Stats) : null;
        if (d == null || !d.IsPerforming)
            return BardicMusicResult.Refused("Not performing");
        return BardicMusicResult.Done(End(bard, "stops", all));
    }

    /// <summary>Ends the bard's current performance and its effects (inspire courage lingers). Returns the log line.</summary>
    public static string End(CharacterController bard, string why, IList<CharacterController> all)
    {
        BardicMusicData d = bard != null && bard.Stats != null ? bard.Stats.BardBardicMusic : null;
        if (d == null || !d.IsPerforming)
            return string.Empty;
        string name = bard.Stats.CharacterName;
        BardicAbility ability = d.ActiveAbility;
        string text = $"{name} {why}: {BardicAbilityInfo.GetDisplayName(ability)} ends";
        switch (ability)
        {
            case BardicAbility.Fascinate:
                for (int i = 0; i < d.FascinatedTargets.Count; i++)
                {
                    CharacterController t = d.FascinatedTargets[i];
                    if (IsFascinatedBy(t, bard))
                        Remove(t, CombatConditionType.Fascinated);
                }
                text += d.FascinatedTargets.Count > 0 ? "; the fascination ends" : string.Empty;
                break;
            case BardicAbility.InspireCompetence:
                if (d.CompetenceAlly != null && d.CompetenceAlly.Stats != null)
                    d.CompetenceAlly.Stats.Bonuses.RemoveOwner(CompetenceBonusSource);
                break;
        }
        d.StopPerformance();
        if (ability == BardicAbility.InspireCourage)
            text += $"; its effect lingers {d.LingerRoundsRemaining} rounds";
        return text + ".";
    }

    /// <summary>
    /// Sets each creature's Inspire Courage bonus to the best song that covers it (singing or lingering): two bards' songs
    /// are one effect and do not stack (PHB p.171).
    /// </summary>
    public static void RefreshInspireCourage(IList<CharacterController> creatures, IList<CharacterController> all)
    {
        if (creatures == null)
            return;
        var copy = new List<CharacterController>(creatures);
        for (int i = 0; i < copy.Count; i++)
        {
            CharacterController c = copy[i];
            if (c == null || c.Stats == null)
                continue;
            int best = 0;
            if (all != null)
            {
                for (int j = 0; j < all.Count; j++)
                {
                    CharacterController b = all[j];
                    BardicMusicData d = b != null && b.Stats != null ? b.Stats.BardBardicMusic : null;
                    if (d == null || !d.IsEffectActive || (d.IsPerforming && d.ActiveAbility != BardicAbility.InspireCourage && d.LingerRoundsRemaining <= 0))
                        continue;
                    if (d.InspiredAllies.Contains(c))
                        best = Mathf.Max(best, d.InspireCourageValue);
                }
            }
            c.Stats.SetInspireCourage(best);
        }
    }

    // ── Countersong ────────────────────────────────────────────────

    /// <summary>
    /// The best countersong result <paramref name="target"/> may use: a bard countersinging within 30 ft of it (or the
    /// target itself; PHB p.29 "any creature within 30 feet", no hearing needed for this use; delegated RAW reading
    /// 2026-10-14). Null when none.
    /// </summary>
    public static int? CountersongResultFor(CharacterController target, IList<CharacterController> all)
    {
        if (target == null || all == null)
            return null;
        int? best = null;
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController b = all[i];
            BardicMusicData d = b != null && b.Stats != null ? b.Stats.BardBardicMusic : null;
            if (d == null || d.ActiveAbility != BardicAbility.Countersong || !IsUp(b))
                continue;
            if (b != target && Distance(b, target) > RangeSquares30Ft)
                continue;
            if (!best.HasValue || d.LastPerformCheck > best.Value)
                best = d.LastPerformCheck;
        }
        return best;
    }

    /// <summary>
    /// A save against a sonic or language-dependent magical attack: the countersong's Perform result replaces the save
    /// total when it is higher (PHB p.29). Every save roll through SpellSaveResolver and SpellCaster asks this, the
    /// saves against monster auras marked sonic too (AIService.AuraSaveContext).
    /// </summary>
    public static int ApplyCountersong(CharacterController target, int saveTotal, SaveContext context)
    {
        if (context == null || !context.IsSonicOrLanguageDependent || target == null)
            return saveTotal;
        GameManager gm = GameManager.Instance;
        int? result = CountersongResultFor(target, gm != null ? gm.GetAllCharactersForAI() : null);
        if (result.HasValue && result.Value > saveTotal)
        {
            Debug.Log($"[BardicMusic] {target.Stats.CharacterName} uses a countersong result {result.Value} in place of save {saveTotal} ({context.Label})");
            gm?.CombatUI?.ShowCombatLog(CombatLogHelper.Info("♪", $"{target.Stats.CharacterName} uses the countersong (Perform {result.Value}) in place of its save ({saveTotal}) against {context.Label ?? "the effect"}."));
            return result.Value;
        }
        return saveTotal;
    }

    /// <summary>
    /// Each countersong round, a creature within 30 ft that hears it and is under a noninstantaneous sonic or
    /// language-dependent magical effect gets a new save, made with the countersong result (PHB p.29): the effect ends
    /// when the result meets its DC.
    /// </summary>
    private static string CountersongOngoingEffects(CharacterController bard, IList<CharacterController> all)
    {
        BardicMusicData d = bard != null && bard.Stats != null ? bard.Stats.BardBardicMusic : null;
        if (d == null || all == null)
            return string.Empty;
        var log = new StringBuilder();
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController c = all[i];
            if (!IsUp(c) || (c != bard && (Distance(bard, c) > RangeSquares30Ft || !CanHear(c, bard))))
                continue;
            foreach (ConditionService.ActiveCondition cond in ConditionsOf(c))
            {
                if (cond == null)
                    continue;
                int dc = OngoingSonicOrLanguageEffectDC(cond);
                if (dc <= 0 || d.LastPerformCheck < dc)
                    continue;
                Remove(c, ConditionRules.Normalize(cond.Type));
                log.Append($" {c.Stats.CharacterName} breaks free of {cond.SourceName ?? cond.Type.ToString()} with the countersong ({d.LastPerformCheck} vs DC {dc}).");
            }
        }
        return log.ToString();
    }

    /// <summary>The save DC of an ongoing sonic or language-dependent magical effect (a spell's or a bard's suggestion), else 0.</summary>
    private static int OngoingSonicOrLanguageEffectDC(ConditionService.ActiveCondition cond)
    {
        if (cond.SourceId == SuggestionSourceId && cond.Data is SuggestedConditionData sd)
            return sd.SaveDC;
        if (string.IsNullOrEmpty(cond.SourceId) || cond.Source == null || cond.Source.Stats == null)
            return 0;
        SpellData spell = SpellDatabase.GetSpell(cond.SourceId);
        if (spell == null || spell.DurationType == DurationType.Instantaneous)
            return 0;
        if (!spell.HasDescriptor(SpellDescriptor.Sonic) && !spell.HasDescriptor(SpellDescriptor.LanguageDependent))
            return 0;
        return SpellUtilities.GetSpellSaveDC(cond.Source, spell);
    }

    // ── Turn hooks (PC and NPC alike) ──────────────────────────────

    /// <summary>
    /// The bard's initiative count was reached (GameManager.OnInitiativeCountReached, also for a dead bard): a round of
    /// the performance has passed. It ends when the bard can no longer perform, at its round cap (countersong 10, fascinate
    /// 1 per bard level, inspire competence 20), when nobody is left fascinated or the inspired ally no longer hears;
    /// otherwise countersong rolls its new Perform check, inspire courage takes in allies who now hear it, and the
    /// concentration of the new turn is still owed. A lingering inspire courage loses a round, and ends at 0.
    /// </summary>
    public static string OnBardCountReached(CharacterController bard, IList<CharacterController> all)
    {
        BardicMusicData d = bard != null && bard.Stats != null ? bard.Stats.BardBardicMusic : null;
        if (d == null)
            return string.Empty;
        var log = new StringBuilder();
        bool lingerStartedNow = false;

        if (d.IsPerforming)
        {
            bool wasInspiring = d.ActiveAbility == BardicAbility.InspireCourage;
            d.RoundsPerformed++;
            d.ConcentratedThisTurn = false;
            string problem = PerformProblem(bard);
            if (d.ActiveAbility == BardicAbility.Fascinate)
                d.FascinatedTargets.RemoveAll(t => !IsFascinatedBy(t, bard));

            if (problem != null)
                log.Append(End(bard, $"can no longer perform ({problem})", all));
            else if (d.RoundsPerformed >= RoundCap(d.ActiveAbility, d.BardLevel))
                log.Append(End(bard, "reaches the song's limit", all));
            else if (d.ActiveAbility == BardicAbility.Fascinate && d.FascinatedTargets.Count == 0)
                log.Append(End(bard, "has no one left fascinated", all));
            else if (d.ActiveAbility == BardicAbility.InspireCompetence && (d.CompetenceAlly == null || !CanHear(d.CompetenceAlly, bard)))
                log.Append(End(bard, "is no longer heard by the inspired ally", all));
            else if (d.ActiveAbility == BardicAbility.Countersong)
            {
                d.LastPerformCheck = RollPerformCheck(bard);
                log.Append($"{bard.Stats.CharacterName}'s countersong goes on (Perform {d.LastPerformCheck}).");
                log.Append(CountersongOngoingEffects(bard, all));
            }
            else if (d.ActiveAbility == BardicAbility.InspireCourage && all != null)
            {
                for (int i = 0; i < all.Count; i++)
                    if (!d.InspiredAllies.Contains(all[i]) && IsInspireCourageRecipient(bard, all[i]))
                        d.InspiredAllies.Add(all[i]);
                RefreshInspireCourage(d.InspiredAllies, all);
            }
            lingerStartedNow = wasInspiring && !d.IsPerforming;
        }

        // A lingering Inspire Courage counts down whatever the bard performs now; it began this count when the song
        // just ended, so its 5 rounds start at the next one.
        if (!lingerStartedNow && d.ActiveAbility != BardicAbility.InspireCourage && d.LingerRoundsRemaining > 0)
        {
            d.TickLingerRound();
            if (d.LingerRoundsRemaining <= 0)
            {
                var was = new List<CharacterController>(d.InspiredAllies);
                d.InspiredAllies.Clear();
                RefreshInspireCourage(was, all);
                log.Append($"{bard.Stats.CharacterName}'s Inspire Courage fades.");
            }
        }

        // Each ally's own 5 rounds after he stops hearing the song (after the bard-wide linger, which may end it for all).
        string faded = AgeInspireCourageHearing(bard, d, all);
        if (faded.Length > 0)
            log.Append(' ').Append(faded);
        return log.ToString().Trim();
    }

    /// <summary>
    /// Inspire Courage lasts for each ally as long as he hears the song and 5 rounds thereafter (PHB p.29), counted at
    /// the bard's initiative count: an ally who hears the singing bard now starts over; one who does not (deafened,
    /// silenced, unconscious, or the song has stopped) counts one more round, and loses the bonus after 5 (delegated RAW
    /// reading 2026-10-14). The bard-wide linger after the song stops ends everyone's bonus at the same count at the
    /// latest. Returns the log line.
    /// </summary>
    private static string AgeInspireCourageHearing(CharacterController bard, BardicMusicData d, IList<CharacterController> all)
    {
        d.NotHearingRounds ??= new Dictionary<CharacterController, int>();
        if (d.InspiredAllies.Count == 0)
        {
            d.NotHearingRounds.Clear();
            return string.Empty;
        }
        bool singing = d.ActiveAbility == BardicAbility.InspireCourage;
        var dropped = new List<CharacterController>();
        foreach (CharacterController c in new List<CharacterController>(d.InspiredAllies))
        {
            if (singing && IsInspireCourageRecipient(bard, c))
            {
                d.NotHearingRounds.Remove(c);
                continue;
            }
            int rounds = d.NotHearingRounds.TryGetValue(c, out int n) ? n + 1 : 0;
            d.NotHearingRounds[c] = rounds;
            if (rounds >= LingerRounds)
            {
                d.InspiredAllies.Remove(c);
                d.NotHearingRounds.Remove(c);
                dropped.Add(c);
            }
        }
        if (dropped.Count == 0)
            return string.Empty;
        RefreshInspireCourage(dropped, all);
        var names = new List<string>();
        foreach (CharacterController c in dropped)
            if (c != null && c.Stats != null)
                names.Add(c.Stats.CharacterName);
        return $"{bard.Stats.CharacterName}'s Inspire Courage fades for {string.Join(", ", names)} (5 rounds without hearing the song).";
    }

    /// <summary>
    /// The end of the bard's turn (GameManager.NextInitiativeTurn): a song that needs concentration ends when the bard
    /// spent no standard action on it this turn (PHB p.29).
    /// </summary>
    public static string OnBardTurnEnded(CharacterController bard, IList<CharacterController> all)
    {
        BardicMusicData d = bard != null && bard.Stats != null ? bard.Stats.BardBardicMusic : null;
        if (d == null || !d.IsPerforming || !BardicAbilityInfo.RequiresConcentration(d.ActiveAbility) || d.ConcentratedThisTurn)
            return string.Empty;
        return End(bard, "stops concentrating", all);
    }

    /// <summary>
    /// A potential threat to a creature the bard fascinates (the combat noise of an attack nearby; PHB p.29): the bard makes a
    /// new Perform check, which is the DC of the creature's new save. Returns the new DC (0 when not a bardic fascination).
    /// </summary>
    public static int NewFascinateCheckForThreat(CharacterController bard)
    {
        BardicMusicData d = bard != null && bard.Stats != null ? bard.Stats.BardBardicMusic : null;
        if (d == null || d.ActiveAbility != BardicAbility.Fascinate)
            return 0;
        d.LastPerformCheck = RollPerformCheck(bard);
        return d.LastPerformCheck;
    }

    /// <summary>Ends every performance and lingering effect of the creatures in <paramref name="all"/> (the end of a fight).</summary>
    public static void EndAllPerformances(IList<CharacterController> all)
    {
        if (all == null)
            return;
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController b = all[i];
            BardicMusicData d = b != null && b.Stats != null ? b.Stats.BardBardicMusic : null;
            if (d == null)
                continue;
            if (d.IsPerforming)
                End(b, "ends the performance with the fight", all);
            d.ClearLinger();
            d.InspiredAllies.Clear();
            d.NotHearingRounds.Clear();
        }
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController c = all[i];
            if (c != null && c.Stats != null)
            {
                c.Stats.RemoveInspireCourage();
                c.Stats.Bonuses.RemoveOwner(CompetenceBonusSource);
            }
        }
    }

    /// <summary>
    /// A reused creature slot (GameManager.ResetCharacterSlotForSpawn, CRE-046): <paramref name="bardStats"/>'s music
    /// forgets <paramref name="c"/>, the slot's old creature (inspired, fascinated, suggested, resisted today, the
    /// competence ally), so the new creature in the slot inherits none of it.
    /// </summary>
    public static void ForgetCreature(CharacterStats bardStats, CharacterController c)
    {
        BardicMusicData d = bardStats != null ? bardStats.BardBardicMusic : null;
        if (d == null || c == null)
            return;
        d.InspiredAllies?.Remove(c);
        d.NotHearingRounds?.Remove(c);
        d.FascinatedTargets?.Remove(c);
        d.SuggestedTargets?.Remove(c);
        d.FascinateResistedToday?.Remove(c);
        if (d.CompetenceAlly == c)
            d.CompetenceAlly = null;
    }

    /// <summary>
    /// An NPC bard's Perform ranks (CHR-067: NPCs have no skill ranks otherwise): the class-skill maximum, character level
    /// + 3, as the DMG's NPC bard has (DMG p.113-114; delegated RAW reading 2026-10-14). A creature without bard levels,
    /// or one that already has Perform ranks, is left alone.
    /// </summary>
    public static void ApplyNpcChoices(CharacterStats stats)
    {
        if (stats == null || BardLevel(stats) < 1 || PerformRanks(stats) > 0)
            return;
        stats.Skills ??= new Dictionary<string, Skill>();
        int ranks = Mathf.Max(1, stats.GetHitDice()) + 3;
        if (!stats.Skills.TryGetValue(PerformSkill, out Skill perform) || perform == null)
        {
            perform = new Skill(PerformSkill, AbilityType.CHA, isClassSkill: true);
            stats.Skills[PerformSkill] = perform;
        }
        perform.Ranks = ranks;
        stats.EnsureBardicMusicInitialized();
    }
}
