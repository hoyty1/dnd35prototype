using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TurnUndeadSystem : BaseCombatManeuver
{
}

public partial class GameManager
{
    private const int TurnUndeadFearBreakDistanceSquares = 2; // 10 ft in D&D 3.5e square grid.

    private sealed class TurnUndeadTracker
    {
        public CharacterController Turner;
        public float AppliedTime;
    }

    private sealed class TurnUndeadTargetOption
    {
        public CharacterController Target;
        public int HitDice;
        public bool CanDestroy;
    }

    private sealed class TurnUndeadSelectionContext
    {
        public CharacterController Turner;
        public int EffectiveTurnLevel;
        public int MaxAffectedHd;
        public int TurnPoolHd;
        public int CheckRoll;
        public int CheckTotal;
        public int TurnDamageRoll;
        public int AttemptsUsedAfterResolution;
        public int AttemptsRemainingAfterResolution;
        public List<TurnUndeadTargetOption> ValidTargets = new List<TurnUndeadTargetOption>();
    }

    // Tracks which cleric is the source of an active Turned condition for each undead target.
    private readonly Dictionary<CharacterController, TurnUndeadTracker> _activeTurnUndeadTrackers = new Dictionary<CharacterController, TurnUndeadTracker>();

    private bool _isSelectingTurnUndead;
    private CharacterController _turnUndeadPendingInvoker;
    private TurnUndeadTargetSelectionPanel _activeTurnUndeadSelectionPanel;
    private TurnUndeadSelectionContext _activeTurnUndeadSelectionContext;

    public int GetRemainingTurnUndeadAttempts(CharacterController actor)
    {
        if (actor == null || actor.Stats == null)
            return 0;

        return Mathf.Max(0, actor.Stats.MaxTurnUndeadAttemptsPerDay - actor.Stats.TurnUndeadAttemptsUsedToday);
    }

    public bool IsTurnedUndead(CharacterController undead)
    {
        if (undead == null || undead.Stats == null || undead.Stats.IsDead)
            return false;

        StatusEffect turnedCondition = GetActiveTurnedCondition(undead);
        if (turnedCondition == null)
            return false;

        if (turnedCondition.RemainingRounds == 0)
            return false;

        return ConditionRules.Normalize(turnedCondition.Type) == CombatConditionType.Turned;
    }

    public int GetTurnedRoundsRemaining(CharacterController undead)
    {
        if (!IsTurnedUndead(undead))
            return 0;

        StatusEffect turnedCondition = GetActiveTurnedCondition(undead);
        return turnedCondition != null ? Mathf.Max(0, turnedCondition.RemainingRounds) : 0;
    }

    public CharacterController GetTurningCleric(CharacterController undead)
    {
        if (!IsTurnedUndead(undead))
            return null;

        CharacterController recorded = GetTurnUndeadTurner(undead);
        if (recorded != null)
            return recorded;

        StatusEffect turnedCondition = GetActiveTurnedCondition(undead);
        if (turnedCondition == null || string.IsNullOrWhiteSpace(turnedCondition.SourceName))
            return null;

        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            if (string.Equals(candidate.Stats.CharacterName, turnedCondition.SourceName, StringComparison.Ordinal))
            {
                RegisterTurnUndeadTracker(undead, candidate);
                return candidate;
            }
        }

        return null;
    }

    private void RegisterTurnUndeadTracker(CharacterController undead, CharacterController turner)
    {
        if (undead == null)
            return;

        if (turner == null)
        {
            _activeTurnUndeadTrackers.Remove(undead);
            return;
        }

        _activeTurnUndeadTrackers[undead] = new TurnUndeadTracker
        {
            Turner = turner,
            AppliedTime = Time.time
        };
    }

    private CharacterController GetTurnUndeadTurner(CharacterController undead)
    {
        if (undead == null)
            return null;

        if (_activeTurnUndeadTrackers.TryGetValue(undead, out TurnUndeadTracker tracker))
            return tracker != null ? tracker.Turner : null;

        return null;
    }

    private bool IsTurnedBy(CharacterController undead, CharacterController candidateTurner)
    {
        if (undead == null || candidateTurner == null)
            return false;

        CharacterController recordedTurner = GetTurnUndeadTurner(undead);
        return recordedTurner != null && recordedTurner == candidateTurner;
    }

    private StatusEffect GetActiveTurnedCondition(CharacterController undead)
    {
        if (undead == null)
            return null;

        List<StatusEffect> conditions = undead.GetActiveConditions();
        for (int i = 0; i < conditions.Count; i++)
        {
            StatusEffect condition = conditions[i];
            if (condition != null && ConditionRules.Normalize(condition.Type) == CombatConditionType.Turned)
                return condition;
        }

        return null;
    }

    private void ClearTurnUndeadEffect(CharacterController undead, string logMessage = null)
    {
        if (undead == null)
            return;

        _activeTurnUndeadTrackers.Remove(undead);

        bool removed = undead.RemoveCondition(CombatConditionType.Turned);
        if (removed)
        {
            Debug.Log($"[Turn Undead] Cleared turned condition on {undead.Stats?.CharacterName ?? "Unknown"}");
        }

        if (!string.IsNullOrWhiteSpace(logMessage))
            CombatUI?.ShowCombatLog(logMessage);
    }

    private void BreakTurnUndeadFear(CharacterController undead, CharacterController turner, string reason)
    {
        if (undead == null || turner == null || !undead.HasCondition(CombatConditionType.Turned) || !IsTurnedBy(undead, turner))
            return;

        string reasonText = reason == "melee"
            ? $"{turner.Stats.CharacterName} made a melee attack"
            : $"{turner.Stats.CharacterName} approached within 10 feet";

        ClearTurnUndeadEffect(undead, $"[Turn Undead] {undead.Stats.CharacterName}'s fear breaks! ({reasonText})");
        UpdateAllStatsUI();
        Debug.Log($"[Turn Undead] Fear broken for {undead.Stats?.CharacterName ?? "Unknown"} ({reasonText})");
    }

    private void CheckTurnUndeadProximityBreakingForCleric(CharacterController cleric)
    {
        if (cleric == null || cleric.Stats == null || cleric.Stats.IsDead)
            return;

        var toBreak = new List<CharacterController>();

        foreach (var kvp in _activeTurnUndeadTrackers)
        {
            CharacterController undead = kvp.Key;
            TurnUndeadTracker tracker = kvp.Value;

            if (undead == null || tracker == null)
                continue;
            if (!undead.HasCondition(CombatConditionType.Turned))
                continue;
            if (tracker.Turner != cleric)
                continue;

            int distanceSquares = cleric.GetMinimumDistanceToTarget(undead, chebyshev: true);
            if (distanceSquares <= TurnUndeadFearBreakDistanceSquares)
                toBreak.Add(undead);
        }

        for (int i = 0; i < toBreak.Count; i++)
            BreakTurnUndeadFear(toBreak[i], cleric, "proximity");
    }

    private void CheckTurnUndeadProximityBreakingForMover(CharacterController mover)
    {
        if (mover == null || mover.Stats == null || mover.Stats.IsDead)
            return;

        // Rule: only the specific turning cleric can break their own turning effect by proximity.
        CheckTurnUndeadProximityBreakingForCleric(mover);
    }

    private void ProcessTurnUndeadMeleeFearBreak(CharacterController attacker, CharacterController target, bool isMeleeAttack)
    {
        if (!isMeleeAttack || attacker == null || target == null)
            return;

        if (!target.HasCondition(CombatConditionType.Turned))
            return;

        if (IsTurnedBy(target, attacker))
            BreakTurnUndeadFear(target, attacker, "melee");
    }

    private bool IsMeleeAttackForTurnUndeadFearBreak(
        CharacterController attacker,
        ItemData weapon,
        RangeInfo rangeInfo,
        bool treatAsThrownAttack = false)
    {
        if (attacker == null)
            return false;

        if (treatAsThrownAttack)
            return false;

        weapon ??= attacker.GetEquippedMainWeapon();

        // Projectile weapons (bows/crossbows/etc.) never break Turn Undead fear.
        if (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged)
            return false;

        if (rangeInfo != null)
            return rangeInfo.IsMelee;

        // Fallback when range info is unavailable.
        return !IsAttackModeRanged(attacker, weapon);
    }

    private void PruneTurnUndeadTrackers()
    {
        if (_activeTurnUndeadTrackers.Count == 0)
            return;

        var keysToRemove = new List<CharacterController>();
        foreach (var kvp in _activeTurnUndeadTrackers)
        {
            CharacterController undead = kvp.Key;
            TurnUndeadTracker tracker = kvp.Value;

            bool invalid = undead == null
                || undead.Stats == null
                || undead.Stats.IsDead
                || !undead.HasCondition(CombatConditionType.Turned)
                || tracker == null
                || tracker.Turner == null;

            if (invalid)
                keysToRemove.Add(undead);
        }

        for (int i = 0; i < keysToRemove.Count; i++)
            _activeTurnUndeadTrackers.Remove(keysToRemove[i]);
    }

    private void LogOngoingTurnUndeadStatusAtRoundStart()
    {
        if (_activeTurnUndeadTrackers.Count == 0)
            return;

        foreach (var kvp in _activeTurnUndeadTrackers)
        {
            CharacterController undead = kvp.Key;
            TurnUndeadTracker tracker = kvp.Value;
            if (undead == null || tracker == null || tracker.Turner == null)
                continue;

            StatusEffect turnedCondition = GetActiveTurnedCondition(undead);
            if (turnedCondition == null)
                continue;

            CombatUI?.ShowCombatLog($"[Turn Undead] {undead.Stats.CharacterName} continues fleeing ({Mathf.Max(0, turnedCondition.RemainingRounds)} rounds remaining)");
        }
    }

    public bool CanUseTurnUndead(CharacterController actor, out string reason)
    {
        reason = "Unavailable";
        if (actor == null || actor.Stats == null)
        {
            reason = "No active character";
            return false;
        }

        bool isCleric = actor.Stats.IsCleric;
        bool isEligiblePaladin = actor.Stats.IsPaladin && actor.Stats.Level >= 4;
        if (!isCleric && !isEligiblePaladin)
        {
            reason = actor.Stats.IsPaladin ? "Paladin level 4 required" : "Cleric or Paladin (Lv4+) only";
            return false;
        }

        if (actor.Actions == null || !actor.Actions.HasStandardAction)
        {
            reason = "Standard action used";
            return false;
        }

        if (GetRemainingTurnUndeadAttempts(actor) <= 0)
        {
            reason = "No attempts left";
            return false;
        }

        reason = "Ready";
        return true;
    }

    private static bool IsUndeadCharacter(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return false;

        if (string.Equals(character.Stats.CreatureType, "Undead", StringComparison.OrdinalIgnoreCase))
            return true;

        if (character.Stats.CreatureTags != null)
        {
            for (int i = 0; i < character.Stats.CreatureTags.Count; i++)
            {
                if (string.Equals(character.Stats.CreatureTags[i], "Undead", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private List<CharacterController> GetTurnableUndeadInRange(CharacterController turner, int maxRangeSquares = 12)
    {
        var result = new List<CharacterController>();
        if (turner == null || turner.Stats == null)
            return result;

        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == turner || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(turner, candidate) || !IsUndeadCharacter(candidate))
                continue;

            int distance = turner.GetMinimumDistanceToTarget(candidate, chebyshev: true);
            if (distance <= maxRangeSquares)
                result.Add(candidate);
        }

        result.Sort((a, b) =>
        {
            int distA = turner.GetMinimumDistanceToTarget(a, chebyshev: true);
            int distB = turner.GetMinimumDistanceToTarget(b, chebyshev: true);
            int byDist = distA.CompareTo(distB);
            if (byDist != 0) return byDist;

            int hdA = a != null && a.Stats != null ? a.Stats.Level : int.MaxValue;
            int hdB = b != null && b.Stats != null ? b.Stats.Level : int.MaxValue;
            int byHd = hdA.CompareTo(hdB);
            if (byHd != 0) return byHd;

            string nameA = a != null && a.Stats != null ? a.Stats.CharacterName : string.Empty;
            string nameB = b != null && b.Stats != null ? b.Stats.CharacterName : string.Empty;
            return string.Compare(nameA, nameB, StringComparison.Ordinal);
        });

        return result;
    }

    private static int GetTurnUndeadEffectiveLevel(CharacterController turner)
    {
        if (turner == null || turner.Stats == null)
            return 0;

        if (turner.Stats.IsCleric)
            return Mathf.Max(1, turner.Stats.Level);

        // D&D 3.5e: Paladin turns undead as a cleric three levels lower.
        if (turner.Stats.IsPaladin)
            return Mathf.Max(0, turner.Stats.Level - 3);

        return 0;
    }

    private static int ComputeTurnUndeadMaxHitDice(int turningCheckTotal, int clericLevel)
    {
        int level = Mathf.Max(1, clericLevel);

        if (turningCheckTotal <= 0) return Mathf.Max(0, level - 4);
        if (turningCheckTotal <= 3) return Mathf.Max(0, level - 3);
        if (turningCheckTotal <= 6) return Mathf.Max(0, level - 2);
        if (turningCheckTotal <= 9) return Mathf.Max(0, level - 1);
        if (turningCheckTotal <= 12) return level;
        if (turningCheckTotal <= 15) return level + 1;
        if (turningCheckTotal <= 18) return level + 2;
        if (turningCheckTotal <= 21) return level + 3;
        return level + 4;
    }

    private void ExecuteTurnUndead(CharacterController cleric)
    {
        if (cleric == null || cleric.Stats == null)
        {
            ShowActionChoices();
            return;
        }

        if (!CanUseTurnUndead(cleric, out string reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {cleric.Stats.CharacterName} cannot use Turn Undead: {reason}.");
            ShowActionChoices();
            return;
        }

        int effectiveTurnLevel = GetTurnUndeadEffectiveLevel(cleric);
        if (effectiveTurnLevel <= 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {cleric.Stats.CharacterName} cannot use Turn Undead: insufficient effective turning level.");
            ShowActionChoices();
            return;
        }

        int checkRoll = UnityEngine.Random.Range(1, 21);
        int checkTotal = checkRoll + cleric.Stats.CHAMod;
        int maxAffectedHd = ComputeTurnUndeadMaxHitDice(checkTotal, effectiveTurnLevel);

        int turnDamageRoll = UnityEngine.Random.Range(1, 7) + UnityEngine.Random.Range(1, 7);
        int turnPoolHd = Mathf.Max(0, turnDamageRoll + effectiveTurnLevel + cleric.Stats.CHAMod);

        List<CharacterController> candidates = GetTurnableUndeadInRange(cleric, maxRangeSquares: 12);
        List<TurnUndeadTargetOption> validTargets = new List<TurnUndeadTargetOption>();
        int totalValidHd = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterController undead = candidates[i];
            if (undead == null || undead.Stats == null || undead.Stats.IsDead)
                continue;

            int undeadHd = Mathf.Max(1, undead.Stats.Level);
            if (undeadHd > maxAffectedHd)
                continue;

            bool canDestroy = effectiveTurnLevel >= undeadHd * 2;
            validTargets.Add(new TurnUndeadTargetOption
            {
                Target = undead,
                HitDice = undeadHd,
                CanDestroy = canDestroy,
            });
            totalValidHd += undeadHd;
        }

        TurnUndeadSelectionContext context = new TurnUndeadSelectionContext
        {
            Turner = cleric,
            EffectiveTurnLevel = effectiveTurnLevel,
            MaxAffectedHd = maxAffectedHd,
            TurnPoolHd = turnPoolHd,
            CheckRoll = checkRoll,
            CheckTotal = checkTotal,
            TurnDamageRoll = turnDamageRoll,
            ValidTargets = validTargets,
        };

        if (validTargets.Count == 0)
        {
            if (!TryConsumeTurnUndeadResources(cleric, out int attemptsRemaining))
                return;

            context.AttemptsUsedAfterResolution = cleric.Stats.TurnUndeadAttemptsUsedToday;
            context.AttemptsRemainingAfterResolution = attemptsRemaining;
            LogTurnUndeadHeader(context);
            CombatUI?.ShowCombatLog("   No undead are within 60 ft to be affected.");
            CombatUI?.ShowCombatLog($"   Remaining Turn Undead attempts today: {attemptsRemaining}");

            UpdateAllStatsUI();
            StartCoroutine(AfterAttackDelay(cleric, 0.8f));
            return;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterController undead = candidates[i];
            if (undead == null || undead.Stats == null || undead.Stats.IsDead)
                continue;

            int undeadHd = Mathf.Max(1, undead.Stats.Level);
            if (undeadHd > maxAffectedHd)
                CombatUI?.ShowCombatLog($"   {undead.Stats.CharacterName} ({undeadHd} HD) is too powerful to be turned.");
        }

        if (totalValidHd > turnPoolHd)
        {
            ShowTurnUndeadTargetSelectionMenu(context);
            return;
        }

        if (!TryConsumeTurnUndeadResources(cleric, out int autoAttemptsRemaining))
            return;

        context.AttemptsUsedAfterResolution = cleric.Stats.TurnUndeadAttemptsUsedToday;
        context.AttemptsRemainingAfterResolution = autoAttemptsRemaining;
        LogTurnUndeadHeader(context);
        CombatUI?.ShowCombatLog("   Affecting all valid undead automatically.");
        ResolveTurnUndeadOnTargets(context, validTargets);
    }

    private bool TryConsumeTurnUndeadResources(CharacterController cleric, out int attemptsRemaining)
    {
        attemptsRemaining = 0;

        if (cleric == null || cleric.Stats == null)
            return false;

        if (!CanUseTurnUndead(cleric, out string reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {cleric.Stats.CharacterName} cannot use Turn Undead: {reason}.");
            ShowActionChoices();
            return false;
        }

        if (!cleric.CommitStandardAction())
        {
            CombatUI?.ShowCombatLog($"⚠ {cleric.Stats.CharacterName} cannot use Turn Undead: standard action unavailable.");
            ShowActionChoices();
            return false;
        }

        cleric.Stats.TurnUndeadAttemptsUsedToday++;
        attemptsRemaining = GetRemainingTurnUndeadAttempts(cleric);
        return true;
    }

    private void LogTurnUndeadHeader(TurnUndeadSelectionContext context)
    {
        if (context == null || context.Turner == null || context.Turner.Stats == null)
            return;

        CombatUI?.ShowCombatLog($"✝️ {context.Turner.Stats.CharacterName} invokes Turn Undead! (Attempt {context.AttemptsUsedAfterResolution}/{context.Turner.Stats.MaxTurnUndeadAttemptsPerDay})");
        CombatUI?.ShowCombatLog($"   Turning Check: d20 ({context.CheckRoll}) + CHA {CharacterStats.FormatMod(context.Turner.Stats.CHAMod)} = {context.CheckTotal} → affects undead up to {context.MaxAffectedHd} HD");
        CombatUI?.ShowCombatLog($"   Turning Damage: 2d6 ({context.TurnDamageRoll}) + turning level {context.EffectiveTurnLevel} + CHA {CharacterStats.FormatMod(context.Turner.Stats.CHAMod)} = {context.TurnPoolHd} total HD");
    }

    private void ShowTurnUndeadTargetSelectionMenu(TurnUndeadSelectionContext context)
    {
        if (context == null || context.Turner == null)
        {
            ShowActionChoices();
            return;
        }

        CloseTurnUndeadSelectionPanel(clearHighlights: true);
        _activeTurnUndeadSelectionContext = context;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        HighlightCharacterFootprint(context.Turner, HighlightType.Selected, addToSelectableCells: true);

        Canvas canvas = CombatUI != null ? CombatUI.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            CombatUI?.ShowCombatLog("⚠ Turn Undead target selection UI failed to open. Auto-resolving by HD priority.");

            if (!TryConsumeTurnUndeadResources(context.Turner, out int fallbackAttemptsRemaining))
                return;

            context.AttemptsUsedAfterResolution = context.Turner.Stats.TurnUndeadAttemptsUsedToday;
            context.AttemptsRemainingAfterResolution = fallbackAttemptsRemaining;
            LogTurnUndeadHeader(context);
            ResolveTurnUndeadAutomatic(context);
            return;
        }

        _activeTurnUndeadSelectionPanel = TurnUndeadTargetSelectionPanel.Create(canvas);
        if (_activeTurnUndeadSelectionPanel == null)
        {
            CombatUI?.ShowCombatLog("⚠ Turn Undead target selection UI failed to open. Auto-resolving by HD priority.");

            if (!TryConsumeTurnUndeadResources(context.Turner, out int fallbackAttemptsRemaining))
                return;

            context.AttemptsUsedAfterResolution = context.Turner.Stats.TurnUndeadAttemptsUsedToday;
            context.AttemptsRemainingAfterResolution = fallbackAttemptsRemaining;
            LogTurnUndeadHeader(context);
            ResolveTurnUndeadAutomatic(context);
            return;
        }

        _activeTurnUndeadSelectionPanel.Initialize(
            context.TurnPoolHd,
            OnTurnUndeadSelectionToggled,
            OnTurnUndeadSelectionConfirmed,
            CancelTurnUndeadTargetSelection,
            message => CombatUI?.ShowCombatLog(message));

        for (int i = 0; i < context.ValidTargets.Count; i++)
        {
            TurnUndeadTargetOption option = context.ValidTargets[i];
            if (option == null || option.Target == null || option.Target.Stats == null || option.Target.Stats.IsDead)
                continue;

            bool isAlreadyTurned = IsTurnedUndead(option.Target);
            int roundsRemaining = isAlreadyTurned ? GetTurnedRoundsRemaining(option.Target) : 0;

            _activeTurnUndeadSelectionPanel.AddTarget(
                option.Target,
                option.HitDice,
                option.CanDestroy ? "Destroyed" : "Turned",
                isAlreadyTurned,
                roundsRemaining);
        }

        CombatUI?.SetActionButtonsVisible(false);
        CombatUI?.ShowCombatLog("   Turning power is insufficient for all valid targets. Select which undead to affect.");
        CombatUI?.SetTurnIndicator("TURN UNDEAD: Select targets by HD pool, then Confirm or Cancel.");
    }

    private void OnTurnUndeadSelectionToggled(CharacterController target, bool selected)
    {
        if (target == null || target.Stats == null)
            return;

        HighlightCharacterFootprint(target, selected ? HighlightType.AoEAlly : HighlightType.None, addToSelectableCells: false);
        CombatUI?.ShowCombatLog(selected
            ? $"   Selected {target.Stats.CharacterName}."
            : $"   Deselected {target.Stats.CharacterName}.");

        if (selected && IsTurnedUndead(target))
        {
            int roundsLeft = GetTurnedRoundsRemaining(target);
            CombatUI?.ShowCombatLog(
                $"   [Turn Undead] Note: {target.Stats.CharacterName} is already turned ({roundsLeft} rounds left). " +
                "Selecting this target will refresh the timer to 10 rounds.");
        }
    }

    private void OnTurnUndeadSelectionConfirmed(List<CharacterController> selectedTargets, int spentHd, int remainingHd)
    {
        if (_activeTurnUndeadSelectionPanel == null || _activeTurnUndeadSelectionContext == null)
            return;

        if (selectedTargets == null || selectedTargets.Count == 0)
        {
            CombatUI?.ShowCombatLog("⚠ [Turn Undead] No targets selected.");
            return;
        }

        CharacterController turner = _activeTurnUndeadSelectionContext.Turner;
        if (!TryConsumeTurnUndeadResources(turner, out int attemptsRemaining))
        {
            CloseTurnUndeadSelectionPanel(clearHighlights: true);
            _activeTurnUndeadSelectionContext = null;
            return;
        }

        _activeTurnUndeadSelectionContext.AttemptsUsedAfterResolution = turner.Stats.TurnUndeadAttemptsUsedToday;
        _activeTurnUndeadSelectionContext.AttemptsRemainingAfterResolution = attemptsRemaining;

        LogTurnUndeadHeader(_activeTurnUndeadSelectionContext);
        CombatUI?.ShowCombatLog($"   Manual selection confirmed: {selectedTargets.Count} target(s), {spentHd} HD spent, {remainingHd} HD unspent.");

        CloseTurnUndeadSelectionPanel(clearHighlights: true);

        List<TurnUndeadTargetOption> selectedOptions = new List<TurnUndeadTargetOption>();
        for (int i = 0; i < selectedTargets.Count; i++)
        {
            CharacterController selected = selectedTargets[i];
            if (selected == null || selected.Stats == null || selected.Stats.IsDead)
                continue;

            for (int j = 0; j < _activeTurnUndeadSelectionContext.ValidTargets.Count; j++)
            {
                TurnUndeadTargetOption option = _activeTurnUndeadSelectionContext.ValidTargets[j];
                if (option != null && option.Target == selected)
                {
                    selectedOptions.Add(option);
                    break;
                }
            }
        }

        ResolveTurnUndeadOnTargets(_activeTurnUndeadSelectionContext, selectedOptions);
        _activeTurnUndeadSelectionContext = null;
    }

    public void CancelTurnUndeadTargetSelection()
    {
        if (_activeTurnUndeadSelectionPanel == null && _activeTurnUndeadSelectionContext == null)
            return;

        CloseTurnUndeadSelectionPanel(clearHighlights: true);
        _activeTurnUndeadSelectionContext = null;

        CombatUI?.ShowCombatLog("↩ [Turn Undead] Target selection cancelled. Turn attempt was not consumed.");
        ShowActionChoices();
    }

    private void CloseTurnUndeadSelectionPanel(bool clearHighlights)
    {
        if (_activeTurnUndeadSelectionPanel != null)
        {
            _activeTurnUndeadSelectionPanel.Close();
            _activeTurnUndeadSelectionPanel = null;
        }

        if (!clearHighlights)
            return;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        CharacterController pc = ActivePC;
        if (pc != null)
            HighlightCharacterFootprint(pc, HighlightType.Selected, addToSelectableCells: false);
    }

    private void ResolveTurnUndeadAutomatic(TurnUndeadSelectionContext context)
    {
        if (context == null)
            return;

        List<TurnUndeadTargetOption> sorted = new List<TurnUndeadTargetOption>(context.ValidTargets);
        sorted.Sort((a, b) =>
        {
            if (a == null || a.Target == null) return 1;
            if (b == null || b.Target == null) return -1;

            int distA = context.Turner.GetMinimumDistanceToTarget(a.Target, chebyshev: true);
            int distB = context.Turner.GetMinimumDistanceToTarget(b.Target, chebyshev: true);
            int byDist = distA.CompareTo(distB);
            if (byDist != 0) return byDist;

            int byHd = a.HitDice.CompareTo(b.HitDice);
            if (byHd != 0) return byHd;

            string nameA = a.Target.Stats != null ? a.Target.Stats.CharacterName : string.Empty;
            string nameB = b.Target.Stats != null ? b.Target.Stats.CharacterName : string.Empty;
            return string.Compare(nameA, nameB, StringComparison.Ordinal);
        });

        int hdRemaining = context.TurnPoolHd;
        List<TurnUndeadTargetOption> resolved = new List<TurnUndeadTargetOption>();

        for (int i = 0; i < sorted.Count; i++)
        {
            TurnUndeadTargetOption option = sorted[i];
            if (option == null || option.Target == null || option.Target.Stats == null || option.Target.Stats.IsDead)
                continue;

            if (hdRemaining < option.HitDice)
                break;

            hdRemaining -= option.HitDice;
            resolved.Add(option);
        }

        ResolveTurnUndeadOnTargets(context, resolved);
    }

    private void ResolveTurnUndeadOnTargets(TurnUndeadSelectionContext context, List<TurnUndeadTargetOption> targetsToAffect)
    {
        if (context == null || context.Turner == null || context.Turner.Stats == null)
        {
            ShowActionChoices();
            return;
        }

        int hdRemaining = context.TurnPoolHd;
        int turnedCount = 0;
        int destroyedCount = 0;

        if (targetsToAffect != null)
        {
            for (int i = 0; i < targetsToAffect.Count; i++)
            {
                TurnUndeadTargetOption option = targetsToAffect[i];
                if (option == null || option.Target == null || option.Target.Stats == null || option.Target.Stats.IsDead)
                    continue;

                if (hdRemaining < option.HitDice)
                    continue;

                hdRemaining -= option.HitDice;

                if (option.CanDestroy)
                {
                    int lethalDamage = Mathf.Max(1, option.Target.Stats.CurrentHP + 10);
                    option.Target.Stats.TakeDamage(lethalDamage);
                    HandleSummonDeathCleanup(option.Target);
                    destroyedCount++;
                    CombatUI?.ShowCombatLog($"   💥 {option.Target.Stats.CharacterName} is destroyed by holy power! ({option.HitDice} HD)");
                }
                else
                {
                    option.Target.ApplyCondition(CombatConditionType.Turned, 10, context.Turner.Stats.CharacterName);
                    RegisterTurnUndeadTracker(option.Target, context.Turner);
                    turnedCount++;
                    CombatUI?.ShowCombatLog($"   ↩ {option.Target.Stats.CharacterName} is turned for 10 rounds and flees! ({option.HitDice} HD)");
                }
            }
        }

        if (turnedCount == 0 && destroyedCount == 0)
            CombatUI?.ShowCombatLog("   The divine surge fails to overcome any undead this turn.");

        CombatUI?.ShowCombatLog($"   Results: {destroyedCount} destroyed, {turnedCount} turned, {hdRemaining} HD turning power unspent.");
        CombatUI?.ShowCombatLog($"   Remaining Turn Undead attempts today: {context.AttemptsRemainingAfterResolution}");

        UpdateAllStatsUI();

        if (AreAllNPCsDead())
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI?.SetTurnIndicator("VICTORY! All enemies defeated!");
            CombatUI?.SetActionButtonsVisible(false);
            return;
        }

        StartCoroutine(AfterAttackDelay(context.Turner, 0.9f));
    }

    public void OnTurnUndeadButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null)
            return;

        if (!CanUseTurnUndead(pc, out string reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot use Turn Undead: {reason}.");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        EnterTurnUndeadTargeting(pc);
    }

    private void EnterTurnUndeadTargeting(CharacterController turner)
    {
        if (turner == null)
        {
            ShowActionChoices();
            return;
        }

        _isSelectingTurnUndead = true;
        _turnUndeadPendingInvoker = turner;
        _isSelectingSpecialAttack = false;
        CurrentSubPhase = PlayerSubPhase.ConfirmingTurnUndead;

        CombatUI?.HideSpecialAttackMenu();
        CombatUI?.SetActionButtonsVisible(false);

        ShowTurnUndeadTargetingPreview(turner);

        int inRangeUndead = GetTurnableUndeadInRange(turner, 12).Count;
        CombatUI?.SetTurnIndicator($"TURN UNDEAD: {inRangeUndead} undead in 60 ft. Left-click any cell to invoke, Right-click/Esc to cancel.");
    }

    private void ShowTurnUndeadTargetingPreview(CharacterController turner)
    {
        if (turner == null)
            return;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        int maxRangeSquares = 12;
        int sizePadding = Mathf.Max(0, turner.GetVisualSquaresOccupied() - 1);
        List<SquareCell> rangeCells = GetCellsInChebyshevRange(turner.GridPosition, maxRangeSquares + sizePadding);
        for (int i = 0; i < rangeCells.Count; i++)
        {
            SquareCell cell = rangeCells[i];
            if (cell == null)
                continue;

            cell.SetHighlight(HighlightType.SpellRange);
            _highlightedCells.Add(cell);
        }

        List<CharacterController> undeadInRange = GetTurnableUndeadInRange(turner, maxRangeSquares);
        for (int i = 0; i < undeadInRange.Count; i++)
        {
            CharacterController undead = undeadInRange[i];
            if (undead == null || undead.Stats == null || undead.Stats.IsDead)
                continue;

            HighlightCharacterFootprint(undead, HighlightType.Attack, addToSelectableCells: true);
        }

        HighlightCharacterFootprint(turner, HighlightType.Selected, addToSelectableCells: true);
    }

    private void ConfirmTurnUndeadTargeting()
    {
        if (!_isSelectingTurnUndead)
            return;

        CharacterController turner = _turnUndeadPendingInvoker;
        _isSelectingTurnUndead = false;
        _turnUndeadPendingInvoker = null;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI?.SetActionButtonsVisible(true);

        if (turner == null)
        {
            ShowActionChoices();
            return;
        }

        ExecuteTurnUndead(turner);
    }

    private void CancelTurnUndeadTargeting()
    {
        if (_activeTurnUndeadSelectionPanel != null || _activeTurnUndeadSelectionContext != null)
        {
            CancelTurnUndeadTargetSelection();
            return;
        }

        if (!_isSelectingTurnUndead && CurrentSubPhase != PlayerSubPhase.ConfirmingTurnUndead)
            return;

        _isSelectingTurnUndead = false;
        _turnUndeadPendingInvoker = null;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI?.ShowCombatLog("↩ Turn Undead cancelled.");
        ShowActionChoices();
    }
}
