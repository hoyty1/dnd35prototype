using System;

/// <summary>
/// Runtime payload for CombatConditionType.Asleep.
/// Stored in ConditionService.ActiveCondition.Data.
/// </summary>
[Serializable]
public sealed class AsleepConditionData
{
    public CharacterController Caster;
    public string CasterName;
    public int RemainingRounds;
    public int WakeDC;
    public string SourceSpellId;
    public string SourceEffectName;

    public void RefreshRemainingRounds(int rounds)
    {
        RemainingRounds = rounds;
    }
}
