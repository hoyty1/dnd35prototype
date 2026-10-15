using System;

/// <summary>
/// Inert side channel for the stages of a spell cast (batch 11, docs/designs/cast_core_batch11.md step 2).
/// Every cast pipeline marks the stage points it passes (legality, cost, distraction, failure, spend,
/// concentration on cast, provocation, counterspell, Ring of Counterspells, invisibility break, Mirror Image,
/// target Blink, the globes, SpellCaster.Cast, the handler that claims the cast, the buff step and combat end),
/// so the scenario harness can line the stages of two runs up (Logs/Scenarios/&lt;runId&gt;/&lt;jobId&gt;/caststages.jsonl,
/// Tests.Scenarios.ScenarioTraceDiff). Outside the harness <see cref="ScenarioHooks.CastStage"/> is null and
/// <see cref="Mark"/> does nothing: no logging, no dice, no allocation. Callers pass constant strings, or check
/// <c>ScenarioHooks.CastStage != null</c> before building a detail string. Rules code never reads anything back.
/// </summary>
public static class CastStageTrace
{
    // Stage names (the third hook argument). Constants, so a Mark never allocates.
    public const string Begin = "begin";
    public const string Legality = "legality";
    public const string Cost = "cost";
    public const string Distraction = "distraction";
    public const string Failure = "failure";
    public const string Spend = "spend";
    public const string ConcentrationOnCast = "concentration-on-cast";
    public const string Provocation = "provocation";
    public const string Counterspell = "counterspell";
    public const string Ring = "ring";
    public const string InvisibilityBreak = "invisibility-break";
    public const string MirrorImage = "mirror-image";
    public const string TargetBlink = "target-blink";
    public const string Globe = "globe";
    public const string SpellCasterCast = "spellcaster-cast";
    public const string Handler = "handler";
    public const string Buff = "buff";
    public const string CombatEnd = "combat-end";

    /// <summary>Raises <see cref="ScenarioHooks.CastStage"/> when a handler is installed; otherwise does nothing.</summary>
    public static void Mark(CharacterController caster, SpellData spell, string stage, string detail = null)
    {
        Action<CharacterController, SpellData, string, string> hook = ScenarioHooks.CastStage;
        if (hook != null)
            hook(caster, spell, stage, detail);
    }
}
