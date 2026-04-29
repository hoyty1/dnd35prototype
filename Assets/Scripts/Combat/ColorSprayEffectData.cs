using System;

/// <summary>
/// Runtime payload for Color Spray staged debuff progression.
/// Stored in ConditionService.ActiveCondition.Data on Color Spray-applied conditions.
/// </summary>
[Serializable]
public sealed class ColorSprayEffectData
{
    public CharacterController Caster;
    public string CasterName;
    public string SourceSpellId;
    public string SourceEffectName;

    public int HitDice;
    public int HdTier;

    public int CurrentStage;
    public int RemainingDuration;
    public int NextStage;

    public int Stage1Duration;
    public int Stage2Duration;
    public int Stage3Duration;

    public bool IsManagedColorSprayCondition(CombatConditionType type)
    {
        CombatConditionType normalized = ConditionRules.Normalize(type);
        return normalized == CombatConditionType.Unconscious
            || normalized == CombatConditionType.Blinded
            || normalized == CombatConditionType.Stunned;
    }
}
