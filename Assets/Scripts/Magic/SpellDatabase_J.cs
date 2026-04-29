// ============================================================================
// SpellDatabase_J.cs — Spells starting with J
// Part of the SpellDatabase partial class.
// ============================================================================

public static partial class SpellDatabase
{
    private static void RegisterSpellsJ()
    {
        Register(new SpellData
                {
                    SpellId = "jump",
                    Name = "Jump",
                    Description = "Touched creature gains an enhancement bonus on Jump checks: +10 (CL 1-2), +20 (CL 3-6), +30 (CL 7+). Duration 1 min/level (dismissible). PHB p.246",
                    SpellLevel = 1,
                    School = "Transmutation",
                    ClassList = new[] { "Wizard", "Sorcerer", "Druid", "Ranger" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffSkillName = "Jump",
                    BuffType = "enhancement",
                    BuffBonusType = BonusType.Enhancement,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });
    }
}
