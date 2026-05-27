using System;

/// <summary>
/// Runtime payload for an active Touch of Idiocy effect.
/// Tracks the per-ability damage amounts so they can be restored when the spell expires.
/// </summary>
[Serializable]
public sealed class TouchOfIdiocyConditionData
{
    public CharacterController Caster;
    public string CasterName;
    public int RemainingRounds;

    public int IntelligenceDamage;
    public int WisdomDamage;
    public int CharismaDamage;

    public string SourceSpellId;
    public string SourceEffectName;

    public int TotalMentalDamage => Math.Max(0, IntelligenceDamage) + Math.Max(0, WisdomDamage) + Math.Max(0, CharismaDamage);

    public void RefreshRemainingRounds(int rounds)
    {
        RemainingRounds = Math.Max(0, rounds);
    }
}
