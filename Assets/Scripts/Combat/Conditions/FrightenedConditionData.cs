using System;

/// <summary>
/// Runtime payload for CombatConditionType.Frightened.
/// Stored in ConditionService.ActiveCondition.Data.
/// </summary>
[Serializable]
public sealed class FrightenedConditionData
{
    public CharacterController Caster;
    public string CasterName;
    public int RemainingRounds;
    public int AttackPenalty = -2;
    public int SavePenalty = -2;
    public int SkillPenalty = -2;
    public int AbilityCheckPenalty = -2;
    public string SourceSpellId;
    public string SourceEffectName;

    public void RefreshRemainingRounds(int rounds)
    {
        RemainingRounds = rounds;
    }

    public bool IsSource(CharacterController candidate)
    {
        if (candidate == null)
            return false;

        if (Caster != null)
            return Caster == candidate;

        if (candidate.Stats == null)
            return false;

        return !string.IsNullOrWhiteSpace(CasterName)
               && string.Equals(candidate.Stats.CharacterName, CasterName, StringComparison.Ordinal);
    }
}
