using System;

/// <summary>The course of activity a suggestion sets (PHB p.285). Only one is modelled so far (SPL-139).</summary>
public enum SuggestedCourse
{
    /// <summary>Leave the fight: move away from the suggester and take no hostile action for the duration.</summary>
    LeaveTheFight = 0
}

/// <summary>
/// Runtime payload for CombatConditionType.Suggested (the bard's Suggestion, PHB p.29; CHR-026). Stored in
/// ConditionService.ActiveCondition.Data. The save DC is kept so a countersong can give the subject its new save each
/// round (PHB p.29).
/// </summary>
[Serializable]
public sealed class SuggestedConditionData
{
    public CharacterController Caster;
    public string CasterName;
    public SuggestedCourse Course;
    public int SaveDC;
    public string SourceEffectName;
}
