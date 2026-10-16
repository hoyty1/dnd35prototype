using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one rule for keeping a Concentration-duration spell going, for PCs and NPCs alike (batch 11 step 16,
/// <c>raw-concentration-rules</c>; decisions D1 and D2 of docs/designs/cast_core_batch11.md; SPL-153). The PC's
/// "Maintain" button, the PC spell menu, the AI and the cast core all call it; only who chooses differs.
///
/// PHB p.176 (Duration, Concentration), p.140 and Table 8-2 (p.141):
/// - Concentrating to maintain a spell is a standard action that provokes no attack of opportunity, owed on each of the
///   caster's turns after the turn it was cast (the casting was that turn's standard action).
/// - A caster cannot cast a spell while concentrating on another one (D1): casting any spell (any casting time, a
///   quickened one included; a scroll too, DMG p.213) first ends the maintained spell, a free action with no roll.
/// - A "concentration + N rounds" spell keeps going N rounds after the concentration ends (<see cref="TailRounds"/>);
///   any other one ends. A spell never lasts past its "up to X" limit (<see cref="MaxRounds"/>).
/// - A caster who cannot take a standard action (dazed, stunned, nauseated, out of actions) cannot maintain the spell.
///
/// Delegated RAW readings (2026-10-15): the spell ends at the end of a caster's turn on which it spent no standard action
/// on it (the timing of the CHR-026 bardic-music ruling); the N-round tail runs however the concentration ends, ceased,
/// broken by a distraction or by the caster's death (the spell's own duration line, specific, beats p.176's general "the
/// spell ends"); a grappled or pinned caster may maintain (PHB p.156 lets it cast, so it can concentrate; the grapple
/// Concentration check while maintaining is SPL-172), and so may a paralyzed one, since concentrating is a purely mental
/// action (PHB p.311); the turn-skip gate gives a pinned or paralyzed creature no turn (CMB-075), so its spell is kept up
/// on that skipped turn (<see cref="MaintainOnSkippedTurn"/>). Open (SPL-114): whether a wand or a spell-like ability counts as casting; until it
/// is decided a wand use does not end the concentration at once (<see cref="CastEndsConcentration"/>), and the wand's
/// standard action leaves none to maintain the spell, so it lapses at the end of the turn.
/// </summary>
public static class ConcentrationRules
{
    /// <summary>The rounds <paramref name="spell"/> keeps going after its concentration ends (0: none; PHB p.176).</summary>
    public static int TailRounds(SpellData spell, int casterLevel)
    {
        if (spell == null || spell.DurationType != DurationType.Concentration || spell.ConcentrationTailRounds <= 0)
            return 0;
        int rounds = spell.ConcentrationTailRounds;
        return spell.ConcentrationTailScalesWithLevel ? rounds * Mathf.Max(1, casterLevel) : rounds;
    }

    /// <summary>The most rounds <paramref name="spell"/> can be kept going ("up to X", PHB p.176); 0: no limit.</summary>
    public static int MaxRounds(SpellData spell, int casterLevel)
    {
        if (spell == null || spell.DurationType != DurationType.Concentration || spell.ConcentrationMaxRounds <= 0)
            return 0;
        int rounds = spell.ConcentrationMaxRounds;
        return spell.ConcentrationMaxScalesWithLevel ? rounds * Mathf.Max(1, casterLevel) : rounds;
    }

    /// <summary>
    /// Whether a cast from <paramref name="source"/> is casting a spell, which ends the caster's concentration first
    /// (D1): a slot, a spontaneous spell or conversion, a hold of the charge, a scroll (DMG p.213) and the F12 panel's
    /// cast. Not the delivery of a held charge (the spell was cast at the hold), and not a wand, an open delegated
    /// question (SPL-114).
    /// </summary>
    public static bool CastEndsConcentration(CastSource source)
    {
        if (source == null)
            return true;
        return source.Kind != CastSourceKind.HeldChargeDelivery && source.Kind != CastSourceKind.Wand;
    }

    /// <summary>True when <paramref name="caster"/> concentrates on a spell and has not kept it up yet this turn.</summary>
    public static bool OwesMaintenance(CharacterController caster)
    {
        ConcentrationManager conc = caster != null ? caster.Concentration : null;
        return conc != null && conc.IsConcentrating && !conc.MaintainedThisTurn;
    }

    /// <summary>
    /// Whether <paramref name="caster"/> can spend this turn's standard action keeping its spell going: it concentrates,
    /// has not done so this turn, has its standard action and no condition that bars standard actions.
    /// </summary>
    public static bool CanMaintain(CharacterController caster, out string reason)
    {
        reason = null;
        ConcentrationManager conc = caster != null ? caster.Concentration : null;
        if (conc == null || !conc.IsConcentrating) { reason = "Not concentrating on a spell"; return false; }
        if (conc.MaintainedThisTurn) { reason = "Already concentrated this turn"; return false; }
        string barred = StandardActionBar(caster);
        if (barred != null) { reason = barred; return false; }
        if (caster.Actions == null || !caster.Actions.HasStandardAction) { reason = "No standard action"; return false; }
        return true;
    }

    /// <summary>
    /// Spends the standard action keeping the concentration up this turn (PHB p.176; no attack of opportunity). Returns
    /// the log line, or null with <paramref name="reason"/> when the caster cannot.
    /// </summary>
    public static string Maintain(CharacterController caster, out string reason)
    {
        if (!CanMaintain(caster, out reason))
            return null;
        ConcentrationManager conc = caster.Concentration;
        caster.CommitStandardAction(); // checked above; a disabled caster at 0 hp drops to -1 (PHB p.145), as for bardic music
        conc.MarkMaintained();
        return $"{caster.Stats.CharacterName} concentrates on {conc.ConcentrationSpellName} (standard action).";
    }

    /// <summary>
    /// A turn the turn-skip gate gives no actions (CMB-075) for a caster whose only bar is one that still lets it
    /// concentrate (pinned or paralyzed: <see cref="StandardActionBar"/>): concentrating is the one thing it can do with
    /// that turn's standard action, so the spell is kept up, for PCs and NPCs alike (delegated RAW reading 2026-10-15).
    /// Returns the log line, or null when the caster is not concentrating or cannot concentrate.
    /// </summary>
    public static string MaintainOnSkippedTurn(CharacterController caster)
    {
        ConcentrationManager conc = caster != null ? caster.Concentration : null;
        if (conc == null || !conc.IsConcentrating || conc.MaintainedThisTurn || StandardActionBar(caster) != null)
            return null;
        conc.MarkMaintained(); // the skipped turn takes no other action, so nothing else spends its standard action
        return $"{caster.Stats.CharacterName} concentrates on {conc.ConcentrationSpellName} (standard action, a purely mental one).";
    }

    /// <summary>
    /// The caster's initiative count was reached, before its turn: a new turn owes a new concentration, and a spell at
    /// its "up to X" limit ends (no tail: its whole duration has run). Returns the log line or an empty string.
    /// </summary>
    public static string OnCasterCountReached(CharacterController caster)
    {
        ConcentrationManager conc = caster != null ? caster.Concentration : null;
        return conc != null ? conc.OnOwnCountReached() : string.Empty;
    }

    /// <summary>
    /// The end of the caster's turn: a spell it spent no standard action on this turn ends, or runs its tail (PHB p.176;
    /// the end-of-turn timing is a delegated RAW reading of 2026-10-15). Returns the log line or an empty string.
    /// </summary>
    public static string OnCasterTurnEnded(CharacterController caster)
    {
        ConcentrationManager conc = caster != null ? caster.Concentration : null;
        if (conc == null || !conc.IsConcentrating || conc.MaintainedThisTurn)
            return string.Empty;
        return conc.CeaseConcentration("spent no standard action concentrating on it this turn");
    }

    /// <summary>The warning a PC gets before a cast ends its maintained spell (D1), or null when nothing would end.</summary>
    public static string CastWarning(CharacterController caster, SpellData newSpell, CastSource source)
    {
        ConcentrationManager conc = caster != null ? caster.Concentration : null;
        if (conc == null || !conc.IsConcentrating || !CastEndsConcentration(source))
            return null;
        int tail = TailRounds(conc.ConcentratingOn != null ? conc.ConcentratingOn.Spell : null,
            conc.ConcentratingOn != null ? conc.ConcentratingOn.CasterLevel : 1);
        string after = tail > 0 ? $" it lasts {tail} more round{(tail == 1 ? "" : "s")}" : " it ends";
        string what = newSpell != null ? newSpell.Name : "a spell";
        return $"Casting {what} ends {caster.Stats.CharacterName}'s concentration on {conc.ConcentrationSpellName}:{after} (PHB p.176: no casting while concentrating).";
    }

    /// <summary>
    /// The warning a PC gets before a wand use, which does not end its concentration (SPL-114) but takes the standard
    /// action that maintaining the spell needs this turn, so the spell ends at the end of the turn (decision D2); null
    /// when the caster already kept it up this turn or concentrates on nothing.
    /// </summary>
    public static string WandUseWarning(CharacterController caster, string wandName)
    {
        if (!OwesMaintenance(caster))
            return null;
        ConcentrationManager conc = caster.Concentration;
        int tail = TailRounds(conc.ConcentratingOn != null ? conc.ConcentratingOn.Spell : null,
            conc.ConcentratingOn != null ? conc.ConcentratingOn.CasterLevel : 1);
        string after = tail > 0 ? $"lasts only {tail} more round{(tail == 1 ? "" : "s")}" : "ends";
        return $"Using {wandName} takes {caster.Stats.CharacterName}'s standard action: {conc.ConcentrationSpellName} is not maintained this turn and {after} at the end of the turn (PHB p.176).";
    }

    /// <summary>
    /// The reason a condition bars <paramref name="caster"/> from concentrating on its spell, or null. Every condition
    /// that bars standard actions does, except (delegated RAW readings 2026-10-15) pinned, since a pinned creature may
    /// still cast (PHB p.156), and paralyzed, and the helplessness it brings, since a paralyzed creature can take purely
    /// mental actions (PHB p.311). Asleep, unconscious, petrified, dazed, stunned, nauseated, fascinated, cowering,
    /// panicked and turned creatures cannot.
    /// </summary>
    private static string StandardActionBar(CharacterController caster)
    {
        if (caster == null)
            return "No caster";
        if (!caster.CanTakeTurnActions())
            return "Cannot act";
        bool paralyzed = caster.HasCondition(CombatConditionType.Paralyzed);
        List<StatusEffect> active = caster.GetActiveConditions();
        for (int i = 0; i < active.Count; i++)
        {
            CombatConditionType type = ConditionRules.Normalize(active[i].Type);
            if (type == CombatConditionType.Pinned || type == CombatConditionType.Paralyzed
                || (type == CombatConditionType.Helpless && paralyzed))
                continue;
            ConditionDefinition def = ConditionRules.GetDefinition(active[i].Type);
            if (def.PreventsStandardActions)
                return $"{def.DisplayName} (no standard action)";
        }
        return null;
    }
}
