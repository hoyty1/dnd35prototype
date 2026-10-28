using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Target-side reactions to being attacked or disturbed (batch 12 step 15, docs/designs/pc_npc_parity_batch12.md; decision
/// D1, a delegated RAW reading recorded in docs/systems/RULES_COVERAGE.md 4.1): the giant bombardier beetle's Acid Spray
/// (Ex), MM p.285, the same for a PC-side and an enemy beetle. An extraordinary ability costs no action unless it says so
/// (MM p.315) and most happen reactively (PHB p.142), so the spray is a reaction, at most once per round (between the start
/// of one of the beetle's turns and the start of the next, CharacterController.AcidSprayUsedThisRound), right after the
/// triggering attack or effect has resolved.
///
/// The triggers notify <see cref="NotifyAttackedOrDisturbed"/>:
/// - attacked: an attack roll against the beetle (every attack origin, the AoO included: the post-attack hook's stage 5),
///   a maneuver against it (CharacterController.ExecuteSpecialAttack, through NotifyManeuverAgainst), a hostile spell aimed
///   at it (the cast core's Handlers stage);
/// - disturbed: a foe's effect damages it (DealDamage, with DamagePacket.SourceCreature or the caster of the cast in
///   progress).
/// The notices wait in a queue while something is still resolving (an attack hook's early stages, a maneuver, a cast past
/// its Spend stage (<see cref="ResolvingCast"/>), a turn start's ongoing effects, a breath weapon, a swarm's damage, a
/// ranged special or a persistent area's tick: <see cref="DeferAttackedReactions"/>) and are resolved once it has
/// (<see cref="FlushAttackedReactions"/>, or the hook's <see cref="RunPendingAttackedReactions"/>). A spray never
/// triggers a spray (no notice is taken while one resolves), and a creature never reacts to itself.
/// The decider chooses (IAttackDecider.ChooseAttackedReaction): the AI sprays when the cone catches more foes than allies; a
/// controlled beetle is asked where the resolving path can wait (an attack hook run as a coroutine), else the AI's choice
/// is taken (CRE-075).
/// </summary>
public partial class GameManager
{
    /// <summary>The acid spray's name in the decider call and the log.</summary>
    internal const string AcidSprayReactionName = "Acid Spray";

    /// <summary>The menu name of the acid spray prompt.</summary>
    public const string AttackedReactionMenuName = "AttackedReactionMenu";

    private readonly List<KeyValuePair<CharacterController, CharacterController>> _pendingAttackedReactions
        = new List<KeyValuePair<CharacterController, CharacterController>>();

    private int _attackedReactionDeferDepth;
    private bool _resolvingAttackedReaction;

    /// <summary>
    /// True while the notices must wait: something is still resolving (a defer scope), a cast is resolving
    /// (<see cref="ResolvingCast"/>), or a spray resolves.
    /// </summary>
    private bool AttackedReactionsDeferred => _attackedReactionDeferDepth > 0 || ResolvingCast != null || _resolvingAttackedReaction;

    /// <summary>
    /// The newest cast on the stack that is resolving: past its Spend stage and not ended. A context that is only open (a
    /// UI cast still choosing its target, or one left open by an exit that never reached Spend; CastContext's BeginCast
    /// notes) is not resolving, so it neither holds the reactions nor counts as the source of damage.
    /// </summary>
    private CastContext ResolvingCast
    {
        get
        {
            foreach (CastContext ctx in _castStack)
                if (ctx != null && !ctx.Ended && ctx.Stage > CastStage.Spend)
                    return ctx;
            return null;
        }
    }

    /// <summary>
    /// <paramref name="trigger"/> attacked or disturbed <paramref name="reactor"/>: a creature with a reaction to that (the
    /// bombardier beetle's acid spray) gets a pending notice, the first trigger of a resolving effect winning. None while a
    /// spray resolves (no spray inside a spray), and none for a creature's own act.
    /// </summary>
    internal void NotifyAttackedOrDisturbed(CharacterController reactor, CharacterController trigger)
    {
        if (reactor == null || trigger == null || reactor == trigger || _resolvingAttackedReaction)
            return;
        if (!HasAcidSprayReaction(reactor))
            return;
        for (int i = 0; i < _pendingAttackedReactions.Count; i++)
            if (_pendingAttackedReactions[i].Key == reactor)
                return;
        _pendingAttackedReactions.Add(new KeyValuePair<CharacterController, CharacterController>(reactor, trigger));
    }

    /// <summary>Holds the notices until the matching <see cref="ResumeAttackedReactions"/>.</summary>
    internal void DeferAttackedReactions() => _attackedReactionDeferDepth++;

    /// <summary>Ends a <see cref="DeferAttackedReactions"/> scope; with <paramref name="flush"/>, resolves the notices once nothing holds them.</summary>
    internal void ResumeAttackedReactions(bool flush = true)
    {
        if (_attackedReactionDeferDepth > 0)
            _attackedReactionDeferDepth--;
        if (flush)
            FlushAttackedReactions();
    }

    /// <summary>
    /// Resolves the pending notices now unless something holds them. A synchronous caller cannot wait on a prompt, so a
    /// controlled reactor gets the AI's choice here (CRE-075).
    /// </summary>
    internal void FlushAttackedReactions()
    {
        IEnumerator run = RunPendingAttackedReactions(canWait: false);
        while (run.MoveNext())
        {
            if (run.Current != null)
                Debug.LogError("[AttackedReaction] a synchronous flush waited on a prompt; it finishes later.");
        }
    }

    /// <summary>
    /// Resolves the pending notices in order (each reactor once), asking each reactor's decider; with
    /// <paramref name="canWait"/> false a decider that would prompt is replaced by the AI's. Nothing while the notices are
    /// held (<see cref="AttackedReactionsDeferred"/>).
    /// </summary>
    internal IEnumerator RunPendingAttackedReactions(bool canWait)
    {
        while (_pendingAttackedReactions.Count > 0 && !AttackedReactionsDeferred)
        {
            KeyValuePair<CharacterController, CharacterController> next = _pendingAttackedReactions[0];
            _pendingAttackedReactions.RemoveAt(0);
            IAttackDecider decider = AttackDeciders.For(next.Key);
            if (!canWait && !decider.IsSynchronous)
                decider = AiAttackDecider.Instance;
            IEnumerator reaction = RunAttackedReaction(next.Key, next.Value, decider);
            while (reaction.MoveNext())
                yield return reaction.Current;
        }
    }

    /// <summary>Drops every pending notice (a combat reset).</summary>
    private void ClearAttackedReactions()
    {
        _pendingAttackedReactions.Clear();
        _attackedReactionDeferDepth = 0;
        _resolvingAttackedReaction = false;
    }

    /// <summary>One reactor's reaction to <paramref name="trigger"/>: the acid spray, when it can spray, has an aim and its decider says so.</summary>
    private IEnumerator RunAttackedReaction(CharacterController reactor, CharacterController trigger, IAttackDecider decider)
    {
        if (!CanUseAcidSprayNow(reactor) || AimAcidSpray(reactor, trigger) == null)
            yield break;

        bool use = false;
        Coroutine wait = decider.ChooseAttackedReaction(this, reactor, trigger, AcidSprayReactionName, b => use = b);
        if (wait != null)
            yield return wait;

        // The prompt may have waited: the beetle must still be able to spray.
        if (use && CanUseAcidSprayNow(reactor))
            ResolveAcidSpray(reactor, trigger);
    }

    // ── The acid spray (MM p.285) ───────────────────────────────────────────────────────────────────────────────

    /// <summary>True for a giant bombardier beetle with its once-per-round spray (its breath definition, OncePerRound).</summary>
    private static bool HasAcidSprayReaction(CharacterController c)
    {
        if (c == null || c.Stats == null || !IsGiantBombardierBeetle(c))
            return false;
        BreathWeaponDefinition spray = c.GetBreathWeaponDefinition();
        return spray != null && spray.OncePerRound;
    }

    /// <summary>
    /// True when <paramref name="beetle"/> can spray now: it has the spray, has not used it this round, and is in the fight
    /// and able to act at all (a free action needs that much; ConditionRules.DeniesAllActions).
    /// </summary>
    private static bool CanUseAcidSprayNow(CharacterController beetle)
        => HasAcidSprayReaction(beetle) && !beetle.AcidSprayUsedThisRound
           && !CombatEndRules.IsOutOfFight(beetle) && !ConditionRules.DeniesAllActions(beetle);

    /// <summary>
    /// The spray's 10-ft cone (MM p.285) from a corner of the beetle's square: the best aim that covers
    /// <paramref name="trigger"/> (AoESystem.BestDirectedAimAt: most foes less allies), else, when no aim reaches the
    /// trigger, the best aim at any foe (D1: the cone includes the trigger if any legal aim does). Null when no foe is in
    /// reach.
    /// </summary>
    private AreaAim AimAcidSpray(CharacterController beetle, CharacterController trigger)
    {
        BreathWeaponDefinition spray = beetle != null ? beetle.GetBreathWeaponDefinition() : null;
        if (spray == null || Grid == null)
            return null;
        int rangeSquares = Mathf.Max(1, spray.RangeFeet / 5);
        AoEShape shape = spray.Shape == BreathWeaponShape.Line ? AoEShape.Line : AoEShape.Cone;

        if (trigger != null && trigger.Stats != null && !trigger.Stats.IsDead)
        {
            AreaAim atTrigger = AoESystem.BestDirectedAimAt(beetle, beetle.GridPosition, shape, rangeSquares, trigger, Grid);
            if (atTrigger != null && atTrigger.Cells != null && atTrigger.Cells.Count > 0)
                return atTrigger;
        }

        AreaAim best = null;
        int bestScore = int.MinValue;
        List<CharacterController> everyone = GetAllCharacters();
        for (int i = 0; i < everyone.Count; i++)
        {
            CharacterController foe = everyone[i];
            if (foe == null || foe == beetle || foe.Stats == null || foe.Stats.IsDead || !TeamUtility.IsEnemy(beetle, foe))
                continue;
            AreaAim aim = AoESystem.BestDirectedAimAt(beetle, beetle.GridPosition, shape, rangeSquares, foe, Grid);
            if (aim == null || aim.Cells == null || aim.Cells.Count == 0)
                continue;
            int score = CountSprayFoesLessAllies(beetle, aim);
            if (score > bestScore)
            {
                bestScore = score;
                best = aim;
            }
        }
        return best;
    }

    /// <summary>The foes less the allies of <paramref name="beetle"/> in <paramref name="aim"/> (the beetle not counted).</summary>
    private int CountSprayFoesLessAllies(CharacterController beetle, AreaAim aim)
    {
        int score = 0;
        foreach (CharacterController c in SprayVictims(beetle, aim))
        {
            if (TeamUtility.IsEnemy(beetle, c))
                score++;
            else if (TeamUtility.IsAlly(beetle, c))
                score--;
        }
        return score;
    }

    /// <summary>Every living creature but the beetle with a square of its space in the cone.</summary>
    private List<CharacterController> SprayVictims(CharacterController beetle, AreaAim aim)
    {
        var victims = new List<CharacterController>();
        if (aim == null || aim.Cells == null)
            return victims;
        List<CharacterController> everyone = GetAllCharacters();
        for (int i = 0; i < everyone.Count; i++)
        {
            CharacterController c = everyone[i];
            if (c == null || c == beetle || c.Stats == null || c.Stats.IsDead)
                continue;
            if (AoESystem.IsCreatureInCells(aim.Cells, c) && !victims.Contains(c))
                victims.Add(c);
        }
        return victims;
    }

    /// <summary>
    /// The AI's acid spray choice (D1): spray when the cone it would aim catches more foes than allies (an AI quality
    /// choice, not a rule: the beetle does not spray its own allies for one foe).
    /// </summary>
    internal bool ShouldUseAttackedReactionForAI(CharacterController reactor, CharacterController trigger, string reaction)
    {
        if (!string.Equals(reaction, AcidSprayReactionName, StringComparison.Ordinal) || !CanUseAcidSprayNow(reactor))
            return false;
        AreaAim aim = AimAcidSpray(reactor, trigger);
        return aim != null && CountSprayFoesLessAllies(reactor, aim) > 0;
    }

    /// <summary>
    /// The acid spray as the AI would use it right now against <paramref name="trigger"/>, synchronously: true when it
    /// sprayed. The reaction path (<see cref="RunAttackedReaction"/>) and the scenario harness use the same pieces.
    /// </summary>
    private bool TryBombardierAcidSprayReaction(CharacterController beetle, CharacterController trigger)
    {
        if (!ShouldUseAttackedReactionForAI(beetle, trigger, AcidSprayReactionName))
            return false;
        return ResolveAcidSpray(beetle, trigger);
    }

    /// <summary>
    /// The giant bombardier beetle's Acid Spray (Ex), MM p.285, read from its breath definition (OncePerRound): a 10-ft cone
    /// (<see cref="AimAcidSpray"/>), at most once per round, costing no action (D1). It counts as the beetle attacking, so
    /// its own Sanctuary, Hide from Undead and invisibility end (PHB p.274, p.241, p.245). The cone is an area effect, so a
    /// creature's Sanctuary does not stop it (PHB p.274). Its damage (1d4+2 acid) is rolled once for the whole spray
    /// (AreaDamageRoll, decision D6 of batch 11, SPL-180), and each creature in the cone makes its own save (DC 13
    /// Fortitude: 10 + half its 2 HD + Con +2) through SavingThrowResolver with a creature-ability context; a success
    /// negates. Acid from an extraordinary ability goes through DealDamage (immunity and resistance, no DR; MM p.307).
    /// True when it sprayed.
    /// </summary>
    private bool ResolveAcidSpray(CharacterController beetle, CharacterController trigger)
    {
        if (!CanUseAcidSprayNow(beetle))
            return false;
        BreathWeaponDefinition spray = beetle.GetBreathWeaponDefinition();
        AreaAim sprayAim = AimAcidSpray(beetle, trigger);
        List<CharacterController> victims = SprayVictims(beetle, sprayAim);
        if (victims.Count == 0)
            return false;

        beetle.SetAcidSprayUsedThisRound(true);
        _resolvingAttackedReaction = true;
        try
        {
            // An attack by the beetle: its own wards end (PHB p.274 Sanctuary, p.241 Hide from Undead, p.245 Invisibility).
            CombatFlowService.BreakProtectiveWardsOnAttack(beetle);
            if (beetle.HasActiveInvisibilityEffect)
                beetle.BreakInvisibility(AcidSprayReactionName, trigger);

            AoEShape shape = spray.Shape == BreathWeaponShape.Line ? AoEShape.Line : AoEShape.Cone;
            string saveName = spray.SaveType == SavingThrowType.Fortitude ? "Fortitude" : "Reflex";
            string triggerName = trigger != null && trigger.Stats != null ? trigger.Stats.CharacterName : "a foe";
            CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("🧪", $"{beetle.Stats.CharacterName} reacts to {triggerName} and unleashes {AcidSprayReactionName} ({spray.RangeFeet}-ft {(shape == AoEShape.Line ? "line" : "cone")}; {saveName} DC {spray.SaveDC} {(spray.SaveNegates ? "negates" : "half")})!"));

            // One damage roll for the whole spray, shared by every creature in the cone (decision D6 of the batch 11 design;
            // delegated RAW reading 2026-10-14, batch 11 step 20, SPL-180), made when it first reaches a creature.
            var damageRoll = new AreaDamageRoll();
            SpellDice dice = SpellDice.Of(spray.DamageCount, spray.DamageDice, spray.DamageBonus);
            SaveContext saveContext = SaveContext.CreatureAbility(AcidSprayReactionName, beetle);

            for (int i = 0; i < victims.Count; i++)
            {
                CharacterController victim = victims[i];
                int rawDamage = damageRoll.GetOrRoll(dice, null, AcidSprayReactionName + " damage").Total;
                if (i == 0)
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("", "   " + damageRoll.Describe(spray.DamageType.ToString())));

                SavingThrowResolver.SaveResult save = spray.SaveType == SavingThrowType.Fortitude
                    ? SavingThrowResolver.ResolveFortitudeSave(victim.Stats, spray.SaveDC, AcidSprayReactionName, saveContext)
                    : SavingThrowResolver.ResolveReflexSave(victim.Stats, spray.SaveDC, AcidSprayReactionName, saveContext);
                bool saveSuccess = save.Succeeded;

                int damageToApply = saveSuccess ? (spray.SaveNegates ? 0 : rawDamage / 2) : rawDamage;

                // Evasion and Improved Evasion apply to every Reflex-half damage (PHB p.50, p.41-42; D13, SPL-090).
                string evasionNote = string.Empty;
                if (spray.SaveType == SavingThrowType.Reflex && !spray.SaveNegates)
                    damageToApply = SpellSaveResolver.ApplyEvasion(damageToApply, victim.Stats, saveSuccess, out evasionNote);

                // D&D 3.5e: Blinking creatures take half damage from area attacks
                if (damageToApply > 0 && victim.HasActiveBlinkEffect)
                    damageToApply = Mathf.Max(1, damageToApply / 2);

                string saveText = $"{saveName} d20({save.Roll}) + {save.Modifier} = {save.Total} vs DC {spray.SaveDC} {(saveSuccess ? "SUCCESS" : "FAIL")}{save.NaturalSuffix}{(string.IsNullOrEmpty(evasionNote) ? "" : " " + evasionNote)}";
                if (damageToApply <= 0)
                {
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   {victim.Stats.CharacterName}: {saveText} | no damage{(saveSuccess && spray.SaveNegates ? " (negated)" : "")}"));
                    continue;
                }

                DamagePacket packet = DamagePackets.CreatureAttack("Bombardier Beetle Acid Spray", spray.DamageType, true);
                packet.SavedForHalf = saveSuccess;
                packet.SourceCreature = beetle;
                DamageResolutionResult mitigation = DealDamage(victim, damageToApply, packet);
                int finalDamage = mitigation.FinalDamage;

                string blinkAreaNote = victim.HasActiveBlinkEffect ? " [Blink: halved]" : "";
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   {victim.Stats.CharacterName}: {saveText} | {spray.DamageType} {finalDamage} damage{blinkAreaNote}{DescribeMitigation(mitigation)}"));
            }
        }
        finally
        {
            _resolvingAttackedReaction = false;
        }

        UpdateAllStatsUI();
        EvaluateCombatEnd("BombardierAcidSpray");
        return true;
    }

    /// <summary>
    /// The controlled beetle's prompt (D1): spray at the creature that attacked or disturbed it, or not. A menu closed
    /// without an answer declines.
    /// </summary>
    internal IEnumerator PromptAttackedReaction(CharacterController reactor, CharacterController trigger, string reaction, Action<bool> onResolved)
    {
        if (reactor == null || reactor.Stats == null || CombatUI == null)
        {
            onResolved?.Invoke(ShouldUseAttackedReactionForAI(reactor, trigger, reaction));
            yield break;
        }

        bool resolved = false;
        bool use = false;
        string triggerName = trigger != null && trigger.Stats != null ? trigger.Stats.CharacterName : "the attacker";
        var labels = new List<string>
        {
            $"{reaction}: spray at {triggerName} (no action, once per round)",
            $"No {reaction}"
        };
        CombatUI.ShowSpecialStyleSelectionMenu(
            menuName: AttackedReactionMenuName,
            optionLabels: labels,
            optionEnabledStates: null,
            onSelect: index =>
            {
                use = index == 0;
                resolved = true;
            },
            onCancel: () =>
            {
                use = false;
                resolved = true;
            },
            showWhileActionPanelHidden: true);

        while (!resolved && CombatUI != null && CombatUI.IsSpecialStyleSelectionMenuOpen())
            yield return null;

        onResolved?.Invoke(resolved && use);
    }
}
