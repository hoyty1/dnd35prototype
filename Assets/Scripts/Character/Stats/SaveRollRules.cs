using UnityEngine;

/// <summary>
/// One saving throw roll (<see cref="SaveRollRules.Roll(CharacterStats, SavingThrowType, int, SaveContext, string, int)"/>):
/// the natural d20 (after a Luck domain reroll), the modifier, the total and whether the save succeeded.
/// </summary>
public struct SaveRoll
{
    /// <summary>
    /// The d20 that counts: the natural roll, or the better of the two when a Luck domain reroll was armed (the current
    /// deviation CHR-063: PHB p.187 makes the reroll's result stand even when it is worse).
    /// </summary>
    public int Roll;
    /// <summary>The save modifier (SaveRules.Modifier plus the caller's extra modifier).</summary>
    public int Modifier;
    /// <summary>Roll + Modifier, or the countersong's Perform result when it replaced the save (PHB p.29).</summary>
    public int Total;
    /// <summary>The DC the save was made against.</summary>
    public int DC;
    /// <summary>Whether the save succeeded (PHB p.177: a natural 20 always succeeds, a natural 1 always fails).</summary>
    public bool Succeeded;
    /// <summary>The d20 came up 1.</summary>
    public bool NaturalOne;
    /// <summary>The d20 came up 20.</summary>
    public bool NaturalTwenty;
    /// <summary>A bard's countersong result replaced the save (PHB p.29; BardicMusicRules.ApplyCountersong).</summary>
    public bool CountersongUsed;

    /// <summary>
    /// True when the natural 1 or 20 changed the outcome: a natural 20 whose total misses the DC, or a natural 1 whose
    /// total meets it (and the countersong did not replace the save).
    /// </summary>
    public bool DecidedByNatural => NaturalTwenty ? Total < DC : NaturalOne && !CountersongUsed && Total >= DC;

    /// <summary>" (natural 1)" or " (natural 20)" when <see cref="DecidedByNatural"/>, else empty: for the save's log line.</summary>
    public string NaturalSuffix => !DecidedByNatural ? string.Empty : NaturalTwenty ? " (natural 20)" : " (natural 1)";
}

/// <summary>
/// The one saving throw roll (D13 of the batch 12 design; SPL-018). Every save roll, PC or NPC, spell, item or creature
/// ability, goes through it:
/// <list type="number">
/// <item>one d20 through <see cref="DiceService.D20"/> (the scenario RollFilter seam), under the caller's dice context
/// followed by <see cref="SaveDiceTag"/>;</item>
/// <item>the Luck domain reroll when it is armed (PHB p.187; CharacterStats.ApplyLuckReroll, logged by
/// GameManager.LogLuckRerollIfTriggered; it is pre-armed and keeps the better roll, the known deviation CHR-063);</item>
/// <item>the save modifier, <see cref="SaveRules.Modifier(CharacterStats, SavingThrowType, SaveContext)"/> for the context,
/// plus the caller's extra modifier;</item>
/// <item>a bard's countersong against a sonic or language-dependent effect (PHB p.29; BardicMusicRules.ApplyCountersong);</item>
/// <item>a natural 20 always succeeds and a natural 1 always fails (PHB p.177).</item>
/// </list>
/// The countersong and the natural 1: the countersong's Perform result is used "in place of" the saving throw when it
/// is higher (PHB p.29), so once it replaces the save, the save's natural 1 no longer decides it, and the Perform
/// result is compared with the DC. A natural 1 is an automatic failure, so any Perform result that meets the DC is
/// higher than it and is used, whatever the save's total; a natural 20 is never replaced (it already succeeds).
/// Delegated RAW reading 2026-10-28 (RULES_COVERAGE 4.1).
/// </summary>
public static class SaveRollRules
{
    /// <summary>
    /// The tag every save d20's dice context carries (<see cref="SaveDiceContext"/>), so a test can force the saves by
    /// it (the scenario harness's save fallback, docs/TESTING.md 3.4). Behaviour-neutral: a context never changes a roll.
    /// </summary>
    public const string SaveDiceTag = "[save]";

    /// <summary>A save d20's dice context: the caller's, followed by <see cref="SaveDiceTag"/> (the tag alone for none).</summary>
    public static string SaveDiceContext(string diceContext)
        => string.IsNullOrEmpty(diceContext) ? SaveDiceTag : diceContext + " " + SaveDiceTag;

    /// <summary>
    /// Rolls <paramref name="stats"/>'s <paramref name="type"/> save against <paramref name="dc"/> for the effect
    /// <paramref name="ctx"/> (null: only the bonuses on every save). <paramref name="extraModifier"/> is a modifier of
    /// this one effect that no SaveContext describes.
    /// </summary>
    public static SaveRoll Roll(CharacterStats stats, SavingThrowType type, int dc, SaveContext ctx, string diceContext, int extraModifier = 0)
    {
        int modifier = SaveRules.Modifier(stats, type, ctx) + extraModifier;
        return RollWithModifier(stats, null, modifier, dc, ctx, diceContext, SaveName(type) + " saving throw");
    }

    /// <summary><see cref="Roll(CharacterStats, SavingThrowType, int, SaveContext, string, int)"/> for a SpellSaveResolver save type.</summary>
    public static SaveRoll Roll(CharacterStats stats, SaveType type, int dc, SaveContext ctx, string diceContext, int extraModifier = 0)
        => Roll(stats, SaveRules.ToSavingThrowType(type), dc, ctx, diceContext, extraModifier);

    /// <summary><see cref="Roll(CharacterStats, SavingThrowType, int, SaveContext, string, int)"/> for a SavingThrowResolver save type.</summary>
    public static SaveRoll Roll(CharacterStats stats, SavingThrowResolver.SaveType type, int dc, SaveContext ctx, string diceContext, int extraModifier = 0)
        => Roll(stats, SaveRules.ToSavingThrowType(type), dc, ctx, diceContext, extraModifier);

    /// <summary>
    /// <see cref="Roll(CharacterStats, SavingThrowType, int, SaveContext, string, int)"/> for a save named as text
    /// ("Fortitude"/"Fort", "Reflex"/"Ref", "Will"). An unknown name gives a modifier of 0 (SaveRules.Modifier's rule).
    /// </summary>
    public static SaveRoll Roll(CharacterStats stats, string saveName, int dc, SaveContext ctx, string diceContext, int extraModifier = 0)
    {
        if (SaveRules.TryParse(saveName, out SavingThrowType save))
            return Roll(stats, save, dc, ctx, diceContext, extraModifier);
        return RollWithModifier(stats, null, extraModifier, dc, ctx, diceContext, "saving throw");
    }

    /// <summary>
    /// The roll with a modifier the caller already computed (SpellCaster.Cast logs the ward's and the situational parts
    /// apart): the d20, the Luck reroll, the countersong and the natural 1 and 20 of
    /// <see cref="Roll(CharacterStats, SavingThrowType, int, SaveContext, string, int)"/>. <paramref name="saver"/> is the
    /// creature making the save (for the countersong), else <paramref name="stats"/>'s owner.
    /// </summary>
    public static SaveRoll RollWithModifier(CharacterStats stats, CharacterController saver, int modifier, int dc, SaveContext ctx,
        string diceContext, string luckLabel = null)
    {
        int roll = DiceService.D20(SaveDiceContext(diceContext));
        if (stats != null)
        {
            roll = stats.ApplyLuckReroll(roll, luckLabel ?? "saving throw");
            GameManager.Instance?.LogLuckRerollIfTriggered(stats);
        }

        bool naturalOne = roll == 1;
        bool naturalTwenty = roll == 20;
        int raw = roll + modifier;
        int total = raw;
        bool countersong = false;
        if (!naturalTwenty)
        {
            CharacterController who = saver != null ? saver : stats != null ? stats.OwnerCharacter : null;
            countersong = BardicMusicRules.TryCountersong(who, raw, ctx, naturalOne ? dc : (int?)null, out total);
        }

        bool succeeded = naturalTwenty || ((!naturalOne || countersong) && total >= dc);
        return new SaveRoll
        {
            Roll = roll,
            Modifier = modifier,
            Total = total,
            DC = dc,
            Succeeded = succeeded,
            NaturalOne = naturalOne,
            NaturalTwenty = naturalTwenty,
            CountersongUsed = countersong
        };
    }

    /// <summary>"Fortitude", "Reflex" or "Will".</summary>
    public static string SaveName(SavingThrowType type)
    {
        switch (type)
        {
            case SavingThrowType.Fortitude: return "Fortitude";
            case SavingThrowType.Reflex: return "Reflex";
            default: return "Will";
        }
    }

    /// <summary>"SUCCESS" or "FAIL" plus the natural note, for the hand-written save log lines.</summary>
    public static string Outcome(SaveRoll save, string success = "SUCCESS", string fail = "FAIL")
        => (save.Succeeded ? success : fail) + save.NaturalSuffix;
}
