using System;

public enum SummonCommandType
{
    AttackNearest,
    ProtectCaster
}

[Serializable]
public class SummonCommand
{
    public SummonCommandType Type;
    public string Description;

    public static SummonCommand AttackNearest()
    {
        return new SummonCommand
        {
            Type = SummonCommandType.AttackNearest,
            Description = "Attack nearest enemy"
        };
    }

    public static SummonCommand ProtectCaster()
    {
        return new SummonCommand
        {
            Type = SummonCommandType.ProtectCaster,
            Description = "Protect caster (attack enemies near caster)"
        };
    }
}
