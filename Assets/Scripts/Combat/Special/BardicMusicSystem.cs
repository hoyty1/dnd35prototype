using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DND35.AI;

/// <summary>
/// The PC Bardic Music button and the AI's bardic music: thin wrappers over the one rule, BardicMusicRules (PHB p.29-30;
/// CHR-020, CHR-026). The PC presses the button and picks from a list (start a song, then its targets; concentrate; a
/// suggestion; stop); the AI picks with AIProfile.ChooseBardicMusic. Both go through the same executors, which log the
/// same lines and refresh the stats UI. The performance's rounds pass at the bard's initiative count and its
/// concentration is checked at the end of the bard's turn, for PCs and NPCs alike (OnInitiativeCountReached,
/// NextInitiativeTurn; CHR-069). Also here: the suggested creature's turn (the bard's Suggestion; SuggestedBehaviorController)
/// and the record of hostile actions that fascinate's "nearby combat" reads.
/// </summary>
public partial class GameManager
{
    private SuggestedBehaviorController _suggestedBehaviorController;

    /// <summary>The round of the last hostile action against each creature (a weapon attack, a hostile spell, a maneuver).</summary>
    private readonly Dictionary<CharacterController, int> _lastHostileActionRound = new Dictionary<CharacterController, int>();

    /// <summary>The round of the last hostile action against <paramref name="target"/>, or -100 when none this fight.</summary>
    public int LastHostileActionRoundAgainst(CharacterController target)
        => target != null && _lastHostileActionRound.TryGetValue(target, out int round) ? round : -100;

    /// <summary>Records a hostile action against <paramref name="target"/> this round (BreakFascinationOnHostileAction calls it).</summary>
    private void RecordHostileActionAgainst(CharacterController target)
    {
        if (target != null)
            _lastHostileActionRound[target] = CurrentRound;
    }

    // ── PC button ──────────────────────────────────────────────────

    /// <summary>The Bardic Music button: usable when the list below would offer anything.</summary>
    public bool CanUseBardicMusicButton(CharacterController actor, out string reason)
    {
        reason = null;
        if (actor == null || actor.Stats == null || !BardicMusicRules.HasBardicMusic(actor.Stats))
        {
            reason = "Needs 3 Perform ranks";
            return false;
        }
        if (actor.HasCondition(CombatConditionType.Turned))
        {
            reason = "Turned condition";
            return false;
        }
        if (BardicMusicRules.IsPerforming(actor.Stats))
            return true; // stop is always possible
        foreach (BardicAbility a in BardicMusicRules.AvailableAbilities(actor.Stats))
        {
            if (a != BardicAbility.Suggestion && BardicMusicRules.CanStart(actor, a, out reason))
                return true;
        }
        return false;
    }

    /// <summary>The PC Bardic Music button: the list of what the bard can do now.</summary>
    public void OnBardicMusicButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || pc.Stats == null)
            return;
        if (RedirectPinnedCharacterToGrappleMenu(pc, "bardic music"))
            return;
        if (!CanUseBardicMusicButton(pc, out string reason))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("♪", $"{pc.Stats.CharacterName} cannot use bardic music: {reason}."));
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        List<CharacterController> all = GetAllCharacters();
        var labels = new List<string>();
        var actions = new List<System.Action>();
        BardicMusicData d = BardicMusicRules.Data(pc.Stats);
        BardicAbility active = d != null ? d.ActiveAbility : BardicAbility.None;
        if (active != BardicAbility.None)
        {
            string song = BardicAbilityInfo.GetDisplayName(active);
            if (BardicMusicRules.CanConcentrate(pc, out _))
            {
                labels.Add($"Concentrate on {song} (standard action)");
                actions.Add(() => ContinuePCTurnAfterBardicMusic(pc, ExecuteBardicConcentrate(pc), freeAction: false));
            }
            if (active == BardicAbility.Fascinate)
            {
                foreach (CharacterController t in new List<CharacterController>(d.FascinatedTargets))
                {
                    if (BardicMusicRules.SuggestionTargetProblem(pc, t) != null)
                        continue;
                    CharacterController target = t;
                    labels.Add($"Suggestion: {t.Stats.CharacterName} (standard action; leave the fight)");
                    actions.Add(() => ContinuePCTurnAfterBardicMusic(pc, ExecuteBardicSuggestion(pc, target), freeAction: false));
                }
            }
            labels.Add($"Stop {song} (no action)");
            actions.Add(() => ContinuePCTurnAfterBardicMusic(pc, ExecuteBardicStop(pc), freeAction: true));
        }

        foreach (BardicAbility a in BardicMusicRules.AvailableAbilities(pc.Stats))
        {
            if (a == BardicAbility.Suggestion || !BardicMusicRules.CanStart(pc, a, out _))
                continue;
            BardicAbility ability = a;
            switch (a)
            {
                case BardicAbility.InspireCourage:
                    labels.Add($"Inspire Courage (+{d.InspireCourageBonus}; standard action)");
                    actions.Add(() => ContinuePCTurnAfterBardicMusic(pc, ExecuteBardicStart(pc, ability, null, null), freeAction: false));
                    break;
                case BardicAbility.Countersong:
                    labels.Add("Countersong (standard action; up to 10 rounds)");
                    actions.Add(() => ContinuePCTurnAfterBardicMusic(pc, ExecuteBardicStart(pc, ability, null, null), freeAction: false));
                    break;
                case BardicAbility.Fascinate:
                    if (BardicMusicRules.FascinateCandidates(pc, all).Count == 0)
                        continue;
                    labels.Add($"Fascinate (up to {d.FascinateTargetCount}; standard action, concentration)");
                    actions.Add(() => ShowFascinateTargetList(pc, new List<CharacterController>()));
                    break;
                case BardicAbility.InspireCompetence:
                    if (BardicMusicRules.CompetenceCandidates(pc, all).Count == 0)
                        continue;
                    labels.Add("Inspire Competence (+2 on one ally's skill; standard action, concentration)");
                    actions.Add(() => ShowCompetenceAllyList(pc));
                    break;
            }
        }

        if (labels.Count == 0)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("♪", $"{pc.Stats.CharacterName} has no bardic music to use now."));
            return;
        }
        if (CombatUI == null)
            return;
        CombatUI.ShowPickUpItemSelection(
            actorName: pc.Stats.CharacterName,
            itemOptions: labels,
            onSelect: index =>
            {
                if (index < 0 || index >= actions.Count) { ShowActionChoices(); return; }
                actions[index]();
            },
            onCancel: () => ShowActionChoices(),
            titleOverride: $"BARDIC MUSIC ({BardicMusicRules.UsesLeft(pc.Stats)}/{BardicMusicRules.UsesPerDay(pc.Stats)} left today)",
            bodyOverride: active != BardicAbility.None
                ? $"{pc.Stats.CharacterName} is performing {BardicAbilityInfo.GetDisplayName(active)}. Starting another song ends it."
                : $"{pc.Stats.CharacterName}: choose a song (one use; no spells while performing).",
            optionButtonColorOverride: new Color(0.42f, 0.24f, 0.6f, 1f));
    }

    /// <summary>The Fascinate target list: one creature at a time, up to the bard's count, then Begin.</summary>
    private void ShowFascinateTargetList(CharacterController pc, List<CharacterController> chosen)
    {
        List<CharacterController> all = GetAllCharacters();
        BardicMusicData d = BardicMusicRules.Data(pc.Stats);
        int cap = d != null ? d.FascinateTargetCount : 1;
        var options = new List<CharacterController>();
        foreach (CharacterController c in BardicMusicRules.FascinateCandidates(pc, all))
            if (!chosen.Contains(c))
                options.Add(c);
        if (chosen.Count >= cap || (options.Count == 0 && chosen.Count > 0))
        {
            ContinuePCTurnAfterBardicMusic(pc, ExecuteBardicStart(pc, BardicAbility.Fascinate, chosen, null), freeAction: false);
            return;
        }

        var labels = new List<string>();
        foreach (CharacterController c in options)
            labels.Add($"{c.Stats.CharacterName} ({TeamUtility.GetHitDice(c)} HD, {pc.GetMinimumDistanceToTarget(c, chebyshev: false) * 5} ft)");
        if (chosen.Count > 0)
            labels.Add($"Begin Fascinate ({chosen.Count} chosen)");
        CombatUI?.ShowPickUpItemSelection(
            actorName: pc.Stats.CharacterName,
            itemOptions: labels,
            onSelect: index =>
            {
                if (index >= 0 && index < options.Count)
                {
                    var next = new List<CharacterController>(chosen) { options[index] };
                    ShowFascinateTargetList(pc, next);
                }
                else if (chosen.Count > 0 && index == options.Count)
                    ContinuePCTurnAfterBardicMusic(pc, ExecuteBardicStart(pc, BardicAbility.Fascinate, chosen, null), freeAction: false);
                else
                    ShowActionChoices();
            },
            onCancel: () => ShowActionChoices(),
            titleOverride: $"FASCINATE ({chosen.Count}/{cap} chosen)",
            bodyOverride: "Creatures within 90 ft that can see and hear the bard, away from any nearby combat:",
            optionButtonColorOverride: new Color(0.42f, 0.24f, 0.6f, 1f));
    }

    /// <summary>The Inspire Competence ally list, then the skill list.</summary>
    private void ShowCompetenceAllyList(CharacterController pc)
    {
        List<CharacterController> allies = BardicMusicRules.CompetenceCandidates(pc, GetAllCharacters());
        var labels = new List<string>();
        foreach (CharacterController c in allies)
            labels.Add($"{c.Stats.CharacterName} ({pc.GetMinimumDistanceToTarget(c, chebyshev: false) * 5} ft)");
        CombatUI?.ShowPickUpItemSelection(
            actorName: pc.Stats.CharacterName,
            itemOptions: labels,
            onSelect: index =>
            {
                if (index < 0 || index >= allies.Count) { ShowActionChoices(); return; }
                ShowCompetenceSkillList(pc, allies[index]);
            },
            onCancel: () => ShowActionChoices(),
            titleOverride: "INSPIRE COMPETENCE: WHICH ALLY?",
            bodyOverride: "An ally within 30 ft who can see and hear the bard (not the bard himself):",
            optionButtonColorOverride: new Color(0.42f, 0.24f, 0.6f, 1f));
    }

    private void ShowCompetenceSkillList(CharacterController pc, CharacterController ally)
    {
        var skills = new List<string>(BardicMusicRules.CompetenceSkills);
        var labels = new List<string>();
        foreach (string skill in skills)
            labels.Add($"{skill} (now {CharacterStats.FormatMod(ally.Stats.GetSkillBonus(skill))})");
        CombatUI?.ShowPickUpItemSelection(
            actorName: pc.Stats.CharacterName,
            itemOptions: labels,
            onSelect: index =>
            {
                if (index < 0 || index >= skills.Count) { ShowActionChoices(); return; }
                ContinuePCTurnAfterBardicMusic(pc, ExecuteBardicStart(pc, BardicAbility.InspireCompetence,
                    new List<CharacterController> { ally }, skills[index]), freeAction: false);
            },
            onCancel: () => ShowActionChoices(),
            titleOverride: $"INSPIRE COMPETENCE: {ally.Stats.CharacterName.ToUpperInvariant()}",
            bodyOverride: "+2 competence bonus on checks with one skill while the bard concentrates (up to 2 minutes):",
            optionButtonColorOverride: new Color(0.42f, 0.24f, 0.6f, 1f));
    }

    /// <summary>After the PC's bardic music: the action menu again (or the turn ends when nothing is left).</summary>
    private void ContinuePCTurnAfterBardicMusic(CharacterController pc, bool performed, bool freeAction)
    {
        if (CurrentPhase == TurnPhase.CombatOver)
            return;
        if (!performed || freeAction)
        {
            ShowActionChoices();
            return;
        }
        StartCoroutine(AfterAttackDelay(pc, 0.6f));
    }

    // ── Shared executors (PC choice, AI, scenario steps) ───────────

    private bool ShowBardicResult(CharacterController actor, BardicMusicResult result, string what)
    {
        string actorName = actor != null && actor.Stats != null ? actor.Stats.CharacterName : "?";
        if (result == null || !result.Performed)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("♪", $"{actorName} cannot {what}: {result?.Refusal ?? "refused"}."));
            return false;
        }
        CombatUI?.ShowCombatLog(CombatLogHelper.Buff("♪", result.Log));
        UpdateAllStatsUI();
        if (actor != null && IsPC(actor))
            CombatUI?.UpdateActionButtons(actor);
        return true;
    }

    private bool ExecuteBardicStart(CharacterController actor, BardicAbility ability, IList<CharacterController> targets, string skill)
    {
        EndAttackSequence();
        BardicMusicResult result = BardicMusicRules.Start(actor, ability, targets, skill, GetAllCharacters());
        return ShowBardicResult(actor, result, "begin " + BardicAbilityInfo.GetDisplayName(ability));
    }

    private bool ExecuteBardicConcentrate(CharacterController actor)
        => ShowBardicResult(actor, BardicMusicRules.Concentrate(actor), "concentrate on the song");

    private bool ExecuteBardicSuggestion(CharacterController actor, CharacterController target)
    {
        EndAttackSequence();
        bool done = ShowBardicResult(actor, BardicMusicRules.Suggest(actor, target), "make a suggestion");
        if (done)
            EvaluateCombatEnd("BardicMusic.Suggestion");
        return done;
    }

    private bool ExecuteBardicStop(CharacterController actor)
        => ShowBardicResult(actor, BardicMusicRules.Stop(actor, GetAllCharacters()), "stop the song");

    /// <summary>The AI (and scenario) entry: starts <paramref name="ability"/> through the shared executor.</summary>
    public bool TryBardicMusicForAI(CharacterController npc, BardicAbility ability, IList<CharacterController> targets = null, string skill = null)
        => ExecuteBardicStart(npc, ability, targets, skill);

    public bool TryBardicConcentrateForAI(CharacterController npc) => ExecuteBardicConcentrate(npc);

    public bool TryBardicSuggestionForAI(CharacterController npc, CharacterController target) => ExecuteBardicSuggestion(npc, target);

    public bool TryBardicStopForAI(CharacterController npc) => ExecuteBardicStop(npc);

    /// <summary>
    /// The AI's bardic music at the start of its action (AIProfile.ChooseBardicMusic): true when it spent its standard
    /// action on a song (the turn then ends); false when it did nothing or only stopped a song (no action), and the turn
    /// goes on.
    /// </summary>
    public bool TryNPCBardicMusicTurnForAI(CharacterController npc)
    {
        if (npc == null || npc.Stats == null || !BardicMusicRules.HasBardicMusic(npc.Stats))
            return false;
        List<CharacterController> all = GetAllCharacters();
        BardicMusicChoice choice = npc.aiProfile != null
            ? npc.aiProfile.ChooseBardicMusic(npc, all)
            : AIProfile.DefaultBardicMusicChoice(npc, all);
        if (choice == null)
            return false;
        switch (choice.Kind)
        {
            case BardicMusicChoice.ChoiceKind.Stop:
                ExecuteBardicStop(npc);
                return false;
            case BardicMusicChoice.ChoiceKind.Concentrate:
                return ExecuteBardicConcentrate(npc);
            case BardicMusicChoice.ChoiceKind.Suggest:
                return choice.Targets.Count > 0 && ExecuteBardicSuggestion(npc, choice.Targets[0]);
            default:
                return ExecuteBardicStart(npc, choice.Ability, choice.Targets, choice.Skill);
        }
    }

    // ── Turn hooks ─────────────────────────────────────────────────

    /// <summary>At each initiative count (also a dead creature's): the rounds of that bard's performance (BardicMusicRules).</summary>
    private void TickBardicMusicAtCount(CharacterController count)
    {
        if (count == null || count.Stats == null || count.Stats.BardBardicMusic == null)
            return;
        string log = BardicMusicRules.OnBardCountReached(count, GetAllCharacters());
        if (!string.IsNullOrEmpty(log))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("♪", log));
            UpdateAllStatsUI();
        }
    }

    /// <summary>At the end of each turn: a concentration song the bard did not keep up this turn ends.</summary>
    private void CheckBardicConcentrationAtTurnEnd(CharacterController ending)
    {
        if (ending == null || ending.Stats == null || ending.Stats.BardBardicMusic == null)
            return;
        string log = BardicMusicRules.OnBardTurnEnded(ending, GetAllCharacters());
        if (!string.IsNullOrEmpty(log))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("♪", log));
            UpdateAllStatsUI();
        }
    }

    /// <summary>The end of a fight: every song and its effects end; the hostile-action record is cleared.</summary>
    private void EndAllBardicMusicForCombatEnd()
    {
        List<CharacterController> all = new List<CharacterController>();
        foreach (CharacterController pc in PCs) if (pc != null && pc.Stats != null) all.Add(pc);
        foreach (CharacterController npc in NPCs) if (npc != null && npc.Stats != null) all.Add(npc);
        BardicMusicRules.EndAllPerformances(all);
        _lastHostileActionRound.Clear();
    }

    // ── The suggested creature's turn (PC and NPC) ─────────────────

    public bool TryGetSuggestedTurnDecisionForAI(CharacterController actor, out SuggestedBehaviorController.SuggestedTurnDecision decision)
    {
        _suggestedBehaviorController ??= new SuggestedBehaviorController();
        return _suggestedBehaviorController.TryBuildDecision(this, actor, out decision);
    }

    public IEnumerator ExecuteSuggestedTurnDecisionForAI(CharacterController actor, SuggestedBehaviorController.SuggestedTurnDecision decision)
    {
        _suggestedBehaviorController ??= new SuggestedBehaviorController();
        yield return StartCoroutine(_suggestedBehaviorController.ExecuteDecision(this, actor, decision));
    }

    /// <summary>
    /// A suggested PC follows the course on its own (a forced turn, as for confusion), started by the compulsion pass
    /// (GameManager.TryRunCompulsionTurn, batch 12 step 7); a fascinated one is held by the fascination first.
    /// </summary>
    private IEnumerator ExecuteSuggestedForcedPCTurn(CharacterController pc, SuggestedBehaviorController.SuggestedTurnDecision decision)
    {
        yield return StartCoroutine(ExecuteSuggestedTurnDecisionForAI(pc, decision));
        if (CurrentPhase == TurnPhase.PCTurn && CurrentCharacter == pc)
            EndCurrentTurn();
    }
}
