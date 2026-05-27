using System;

/// <summary>
/// Runtime payload for the active Ray of Enfeeblement effect on a target.
/// D&D 3.5e: only one Ray of Enfeeblement effect is active at a time;
/// a new hit replaces the previous one.
/// </summary>
[Serializable]
public sealed class EnfeebledConditionData
{
    public CharacterController Caster;
    public string CasterName;
    public int StrengthPenaltyAmount;
    public int RemainingRounds;
    public string SourceSpellId;
    public string SourceEffectName;

    public void RefreshRemainingRounds(int rounds)
    {
        RemainingRounds = Math.Max(0, rounds);
    }

    public bool IsStrongerThan(EnfeebledConditionData other)
    {
        int otherPenalty = other != null ? Math.Max(0, other.StrengthPenaltyAmount) : 0;
        return Math.Max(0, StrengthPenaltyAmount) > otherPenalty;
    }
}
