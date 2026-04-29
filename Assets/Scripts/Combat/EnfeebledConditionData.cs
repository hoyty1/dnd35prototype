using System;

/// <summary>
/// Runtime payload for a single Ray of Enfeeblement instance.
/// Multiple instances may be active at once and stack.
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
}
