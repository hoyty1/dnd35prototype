using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The per-bearer immunities to a frightful presence (MM p.309: a creature that saves is immune to that same creature's
/// frightful presence for 24 hours; delegated RAW reading 2026-10-29, decision D12: kept for the encounter, since no
/// encounter lasts 24 hours). Cleared at the end of an encounter with the aura immunities and, for one controller, when its
/// slot gets a new creature (CRE-046, <see cref="GameManager.ResetCharacterSlotForSpawn"/>).
/// </summary>
public static class FrightfulPresenceLedger
{
    private static readonly HashSet<(CharacterController Target, CharacterController Bearer)> _immune
        = new HashSet<(CharacterController Target, CharacterController Bearer)>();

    /// <summary>Whether <paramref name="target"/> saved against <paramref name="bearer"/>'s presence this encounter.</summary>
    public static bool IsImmune(CharacterController target, CharacterController bearer)
        => target != null && bearer != null && _immune.Contains((target, bearer));

    /// <summary>Records <paramref name="target"/>'s successful save against <paramref name="bearer"/>'s presence.</summary>
    public static void GrantImmunity(CharacterController target, CharacterController bearer)
    {
        if (target != null && bearer != null)
            _immune.Add((target, bearer));
    }

    /// <summary>Drops every immunity (the end of an encounter, a new party).</summary>
    public static void Clear() => _immune.Clear();

    /// <summary>
    /// Drops every immunity in which <paramref name="character"/> is the bearer or the creature that saved: used when the
    /// controller is reused for a different creature (CRE-046), the twin of <see cref="AIService.ForgetAuraSaveImmunities"/>.
    /// </summary>
    public static void Forget(CharacterController character)
    {
        if (character == null || _immune.Count == 0)
            return;
        _immune.RemoveWhere(k => ReferenceEquals(k.Target, character) || ReferenceEquals(k.Bearer, character));
    }

    /// <summary>The number of recorded immunities (tests).</summary>
    public static int Count => _immune.Count;
}

/// <summary>
/// GameManager partial class: the monster triggers every bearer fires, PC or NPC (batch 13 step 19,
/// docs/designs/action_catalog_batch13.md; AI-041, CRE-039, AI-053; decisions D12 and D13).
/// - <see cref="TriggerFrightfulPresence"/>: frightful presence (MM p.69, p.309), fired by the attack-sequence executor
///   (<c>RunAttackSequence</c>, attacks of opportunity included), a charge's declaration (<c>ExecuteCharge</c>) and a
///   breath (the breath core).
/// - <see cref="ProcessTurnStartAura"/>: a supernatural aura (gibbering, babble, moan and the gaze-like auras), fired once
///   per round at the start of the bearer's acting turn (<c>BeginActingTurn</c>), compelled turns included.
/// </summary>
public partial class GameManager
{
    /// <summary>The dice context of a frightful presence's duration roll (DiceService; a test can force it by this label).</summary>
    public const string FrightfulPresenceDurationContext = "Frightful presence duration";

    /// <summary>The source id of the fear a frightful presence applies (the no-re-roll test reads it with the bearer).</summary>
    public const string FrightfulPresenceSourceId = "FrightfulPresence";

    /// <summary>The dice context of each creature's save against an aura (SaveRollRules; a test can force it by this label).</summary>
    public const string AuraSaveDiceContext = "Aura save";

    /// <summary>The dice context of an aura's random duration (DiceService; a test can force it by this label).</summary>
    public const string AuraDurationContext = "Aura duration";

    /// <summary>
    /// Fires <paramref name="bearer"/>'s frightful presence for one action (<paramref name="action"/>: "attacks", "charges",
    /// "breathes"), the same for every bearer, PC or NPC. Explicit RAW (MM p.69 for dragons, MM p.309 for every bearer;
    /// decision D12): it affects only opponents with fewer Hit Dice than the bearer (<c>GetHitDice</c>) within its radius
    /// (the entry's: 30 ft x the age category for a dragon), and a dragon ignores another dragon's presence; it is a
    /// mind-affecting fear effect, so a creature immune to either is unaffected (MM p.309, PHB p.44); each makes a Will save
    /// at the entry's DC; a success makes it immune to this bearer's presence for 24 hours, a failure panics a creature with
    /// HD at or below the entry's threshold and shakes any other, for the entry's dice of rounds (4d6 for a dragon).
    /// Delegated RAW readings (2026-10-29, D12, docs/systems/RULES_COVERAGE.md 4.1): it fires once per attack sequence,
    /// charge or breath, not once per attack roll; an attack of opportunity is an attack and fires it; the witnesses are the
    /// opponents in the radius that can see the bearer (<c>CanVisuallyPerceive</c>, the sight test) and are still in the
    /// fight; a creature still shaken, frightened or panicked by this bearer is not asked again; the immunity lasts the
    /// encounter (<see cref="FrightfulPresenceLedger"/>). Synchronous: the attack sequence of an attack of opportunity
    /// calls it inline.
    /// </summary>
    internal void TriggerFrightfulPresence(CharacterController bearer, string action)
    {
        if (bearer == null || bearer.Stats == null || !bearer.HasFrightfulPresence || CurrentPhase == TurnPhase.CombatOver)
            return;
        FrightfulPresenceDefinition fp = bearer.GetFrightfulPresenceDefinition();
        if (fp == null)
            return;

        int bearerHD = bearer.Stats.GetHitDice();
        int radiusSquares = Mathf.Max(1, fp.RangeFeet / 5);
        bool bearerIsDragon = IsDragonForFrightfulPresence(bearer);

        var witnesses = new List<CharacterController>();
        List<CharacterController> all = GetAllCharacters();
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController c = all[i];
            if (IsFrightfulPresenceWitness(bearer, c, bearerHD, radiusSquares, bearerIsDragon))
                witnesses.Add(c);
        }

        string bearerName = bearer.Stats.CharacterName;
        if (witnesses.Count == 0)
        {
            Debug.Log($"[FrightfulPresence] {bearerName} {action}: no opponent in {fp.RangeFeet} ft can be affected.");
            return;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("😱", $"<b>{bearerName}</b> {action}: its Frightful Presence unsettles its foes! (Will DC {fp.SaveDC}, {fp.RangeFeet} ft.)"));

        for (int i = 0; i < witnesses.Count; i++)
        {
            CharacterController target = witnesses[i];
            string targetName = target.Stats.CharacterName;
            int targetHD = target.Stats.GetHitDice();

            // A Will save against a fear effect (SaveRules: morale against fear, CHR-018), through the one save roll
            // (SaveRollRules); the context names the bearer, so a bonus against the creature causing the fear applies
            // (an alignment ward, PHB p.266).
            SaveRoll fearSave = SaveRollRules.Roll(target.Stats, SavingThrowType.Will, fp.SaveDC,
                SaveContext.FearFrom(bearerName + "'s frightful presence", bearer), FrightfulPresenceSaveContext);

            if (fearSave.Succeeded)
            {
                FrightfulPresenceLedger.GrantImmunity(target, bearer);
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("✅", $"  {targetName} resists the Frightful Presence (Will {fearSave.Roll}+{fearSave.Modifier}={fearSave.Total} vs DC {fp.SaveDC}{fearSave.NaturalSuffix}) and is immune to {bearerName}'s for 24 hours."));
                continue;
            }

            int rounds = Mathf.Max(1, DiceService.RollMultiple(Mathf.Max(1, fp.DurationDice), Mathf.Max(1, fp.DurationDieSides), FrightfulPresenceDurationContext));
            bool panicked = targetHD <= fp.HDThresholdForPanic;
            CombatConditionType fear = panicked ? CombatConditionType.Panicked : CombatConditionType.Shaken;
            ApplyCondition(target, fear, rounds, source: bearer, sourceNameOverride: bearerName,
                sourceCategory: "Frightful presence", sourceId: FrightfulPresenceSourceId);
            CombatUI?.ShowCombatLog(CombatLogHelper.Info(panicked ? "😱" : "😰",
                $"  {targetName} is <b>{(panicked ? "Panicked" : "Shaken")}</b> for {rounds} rounds! (Will {fearSave.Roll}+{fearSave.Modifier}={fearSave.Total} vs DC {fp.SaveDC}{fearSave.NaturalSuffix}, {targetHD} HD)"));
        }

        UpdateAllStatsUI();
    }

    /// <summary>
    /// Whether <paramref name="c"/> witnesses <paramref name="bearer"/>'s action and may be affected by its frightful
    /// presence now (D12; see <see cref="TriggerFrightfulPresence"/>). Pure: rolls nothing.
    /// </summary>
    internal bool IsFrightfulPresenceWitness(CharacterController bearer, CharacterController c, int bearerHD, int radiusSquares, bool bearerIsDragon)
    {
        if (c == null || c == bearer || c.Stats == null || CombatEndRules.IsOutOfFight(c))
            return false;
        // Opponents only (MM p.309).
        if (!TeamUtility.IsEnemy(bearer, c))
            return false;
        // Only opponents with fewer Hit Dice than the bearer (MM p.309, p.69).
        if (c.Stats.GetHitDice() >= bearerHD)
            return false;
        // Dragons ignore the frightful presence of other dragons (MM p.69).
        if (bearerIsDragon && IsDragonForFrightfulPresence(c))
            return false;
        // A mind-affecting fear effect (MM p.309): immunity to either keeps a creature out (an undead, a paladin's aura of
        // courage, PHB p.44).
        if (SpellUtilities.IsImmuneToMindAffecting(c) || c.Stats.IsImmuneToFear)
            return false;
        // In the radius, measured from the bearer's space (PHB p.147 count).
        if (bearer.GetMinimumDistanceToTarget(c, chebyshev: false) > radiusSquares)
            return false;
        // A witness sees the bearer (delegated RAW reading, D12).
        if (!c.CanVisuallyPerceive(bearer))
            return false;
        // Immune for 24 hours after a save (MM p.309), kept for the encounter.
        if (FrightfulPresenceLedger.IsImmune(c, bearer))
            return false;
        // Still shaken, frightened or panicked by this bearer: not asked again (delegated RAW reading, D12).
        return !IsAfraidOf(c, bearer);
    }

    private static bool IsDragonForFrightfulPresence(CharacterController c)
        => c != null && c.Stats != null && string.Equals(c.Stats.CreatureType, "Dragon", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="c"/> is shaken, frightened or panicked with <paramref name="source"/> as the condition's source.</summary>
    private bool IsAfraidOf(CharacterController c, CharacterController source)
    {
        List<ConditionService.ActiveCondition> active = GetActiveConditions(c);
        if (active == null)
            return false;
        for (int i = 0; i < active.Count; i++)
        {
            ConditionService.ActiveCondition cond = active[i];
            if (cond == null || cond.Source != source)
                continue;
            CombatConditionType t = ConditionRules.Normalize(cond.Type);
            if (t == CombatConditionType.Shaken || t == CombatConditionType.Frightened || t == CombatConditionType.Panicked)
                return true;
        }
        return false;
    }

    /// <summary>
    /// <paramref name="bearer"/>'s supernatural aura at the start of its acting turn (moved from the AI turn in batch 13
    /// step 19; delegated RAW reading 2026-10-29, decision D13, docs/systems/RULES_COVERAGE.md 4.1: the MM is silent on the
    /// timing, so an aura fires once per round, at the start of the bearer's acting turn, for every bearer, AI-run,
    /// controllable or compelled, and each creature in range saves at most once per round against it, since this runs once
    /// per acting turn). The creatures in range (the aura's radius from the bearer's square) make the aura's save; a
    /// mind-affecting aura spares the creatures immune to mind-affecting effects; a creature already under the aura's
    /// condition is not asked again, nor one that saved against this bearer's aura when the aura grants immunity on a save
    /// (24 hours, MM; kept for the encounter, AIService's aura ledger). An aura that affects all creatures
    /// (<see cref="AuraAbilityDefinition.AffectsAllCreatures"/>: gibbering, MM p.126) spares only the creatures with an aura
    /// of the same name (the other mouthers), allies included; any other aura affects opponents only. Returns true when it
    /// affected any creature.
    /// </summary>
    internal bool ProcessTurnStartAura(CharacterController bearer)
    {
        if (bearer == null || bearer.Stats == null || !bearer.HasAuraAbility || CurrentPhase == TurnPhase.CombatOver)
            return false;

        AuraAbilityDefinition aura = bearer.GetAuraAbilityDefinition();
        if (aura == null)
            return false;

        string bearerName = bearer.Stats.CharacterName;
        int rangeSquares = aura.RangeFeet / 5;
        SaveType saveType = aura.IsWillSave ? SaveType.Will : SaveType.Fortitude;
        string saveTypeName = aura.IsWillSave ? "Will" : "Fort";
        CombatConditionType conditionType = AIService.AuraEffectToCondition(aura.Effect);

        Debug.Log($"[Aura] {bearerName} activating {aura.Name} (DC {aura.SaveDC}, {saveTypeName}, range {aura.RangeFeet} ft, effect {aura.Effect}, mind-affecting={aura.IsMindAffecting}, all creatures={aura.AffectsAllCreatures})");

        CombatUI?.ShowCombatLog(CombatLogHelper.Special("🔊", $"{bearerName}'s {aura.Name} echoes across the battlefield!"));

        List<CharacterController> all = GetAllCharacters();
        int affected = 0;
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController candidate = all[i];
            if (candidate == null || candidate == bearer || candidate.Stats == null || CombatEndRules.IsOutOfFight(candidate))
                continue;

            if (aura.AffectsAllCreatures)
            {
                // Every creature but the bearer's kind (MM p.126: all creatures other than mouthers).
                AuraAbilityDefinition own = candidate.HasAuraAbility ? candidate.GetAuraAbilityDefinition() : null;
                if (own != null && string.Equals(own.Name, aura.Name, System.StringComparison.Ordinal))
                    continue;
            }
            else if (!TeamUtility.IsEnemy(bearer, candidate))
            {
                continue;
            }

            // The full mind-affecting immunity (PHB p.309: undead, constructs, oozes, plants, vermin and mindless creatures,
            // plus a recorded immunity), the same test as the frightful presence's.
            if (aura.IsMindAffecting && SpellUtilities.IsImmuneToMindAffecting(candidate))
            {
                Debug.Log($"[Aura] {candidate.Stats.CharacterName} is immune to mind-affecting effects: skipping {aura.Name}");
                continue;
            }

            // In range, measured from the bearer's space (PHB p.147 count), as for the frightful presence.
            if (bearer.GetMinimumDistanceToTarget(candidate, chebyshev: false) > rangeSquares)
                continue;

            if (candidate.HasCondition(conditionType))
            {
                Debug.Log($"[Aura] {candidate.Stats.CharacterName} already has {conditionType}, skipping");
                continue;
            }

            if (aura.GrantsImmunityOnSave && AIService.HasAuraSaveImmunity(candidate, bearer, aura.Name))
            {
                Debug.Log($"[Aura] {candidate.Stats.CharacterName} is immune to {bearerName}'s {aura.Name}: skipping");
                continue;
            }

            SaveResult saveResult = SpellSaveResolver.RollSave(candidate, saveType, aura.SaveDC, AIService.AuraSaveContext(aura, conditionType), AuraSaveDiceContext);
            if (saveResult.Saved)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Success("💪", $"{candidate.Stats.CharacterName} resists {aura.Name} ({saveTypeName} {saveResult.Total} vs DC {aura.SaveDC})."));
                if (aura.GrantsImmunityOnSave)
                {
                    AIService.GrantAuraSaveImmunity(candidate, bearer, aura.Name);
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("🛡", $"{candidate.Stats.CharacterName} is now immune to {bearerName}'s {aura.Name} for 24 hours."));
                }
                continue;
            }

            int duration = aura.DurationDice > 0
                ? DiceService.Roll(1, aura.DurationDice, AuraDurationContext) // 1d(DurationDice)
                : aura.DurationRounds;
            // Through the one condition entry, with the bearer as the source (CORE-017: no lookup by name).
            ApplyCondition(candidate, conditionType, duration, source: bearer, sourceNameOverride: bearerName,
                sourceCategory: "Aura", sourceId: aura.Name);
            CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{candidate.Stats.CharacterName} succumbs to {aura.Name}! ({saveTypeName} {saveResult.Total} vs DC {aura.SaveDC}) — {aura.Effect} for {duration} round(s)!"));
            affected++;
        }

        if (affected > 0)
        {
            Debug.Log($"[Aura] {bearerName}'s {aura.Name} affected {affected} creature(s)");
            UpdateAllStatsUI();
        }
        return affected > 0;
    }
}
