using System;
using System.Collections.Generic;

/// <summary>How an AI spell candidate is cast (batch 11 step 27): the request the AI's walk builds for it.</summary>
public enum AiSpellCastKind
{
    /// <summary>At one target (SpellCastRequest.ForAI).</summary>
    Single,

    /// <summary>At an aimed area (SpellCastRequest.ForAIArea or ForAIGrease; batch 11 step 25).</summary>
    Area,

    /// <summary>A Summon Monster or Summon Nature's Ally whose creature and square the AI decider picks (ForAISummon; step 27).</summary>
    Summon,

    /// <summary>A melee touch spell cast and held, then delivered after a move into reach (ForAIHoldCharge; step 27).</summary>
    HoldCharge
}

/// <summary>
/// One (spell, target) pair the AI would cast, with its spell's score (batch 11 step 22; AIService.TryExecuteSpellcastAction);
/// for an area spell, the aim its search found instead of a target (batch 11 step 25, AreaAimSearch); since batch 11 step
/// 27 a summon (no target: the AI decider places the creature) or a held touch charge (its target out of reach now).
/// </summary>
public readonly struct AiSpellCandidate
{
    public readonly SpellData Spell;
    public readonly CharacterController Target;
    public readonly float Score;

    /// <summary>The area's aim for an area spell (no <see cref="Target"/>), else null.</summary>
    public readonly AreaPlacement Placement;

    /// <summary>How the walk casts it.</summary>
    public readonly AiSpellCastKind Kind;

    public AiSpellCandidate(SpellData spell, CharacterController target, float score, AreaPlacement placement = null,
        AiSpellCastKind kind = AiSpellCastKind.Single)
    {
        Spell = spell;
        Target = target;
        Score = score;
        Placement = placement;
        Kind = placement != null ? AiSpellCastKind.Area : kind;
    }
}

/// <summary>
/// The AI's ordered retries (batch 11 step 22, AI-063; area casts joined them in step 25): the ranked candidates
/// are tried best first, and a cast the cast core refuses (nothing spent) passes the turn's spell to the next one, at
/// most <see cref="MaxTries"/> tries, so a refused pick no longer ends the caster's spellcasting for the turn. Tactics
/// only; the rules are the cast core's.
/// </summary>
public static class AiSpellCandidates
{
    /// <summary>At most this many casts are tried in one decision (the plan's cap, so retries keep AI turns short).</summary>
    public const int MaxTries = 5;

    /// <summary>
    /// Tries <paramref name="ranked"/> in order (it may be built lazily) with <paramref name="tryCast"/>, which returns
    /// whether the cast counts as cast; stops at the first that does or after <paramref name="cap"/> tries.
    /// <paramref name="tries"/> is the number of casts tried. Returns whether one counted.
    /// </summary>
    public static bool TryInOrder<T>(IEnumerable<T> ranked, int cap, Func<T, bool> tryCast, out int tries)
    {
        tries = 0;
        if (ranked == null || tryCast == null || cap <= 0)
            return false;
        foreach (T candidate in ranked)
        {
            tries++;
            if (tryCast(candidate))
                return true;
            if (tries >= cap)
                break;
        }
        return false;
    }
}
