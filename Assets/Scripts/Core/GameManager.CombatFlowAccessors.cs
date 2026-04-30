using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class GameManager
{
    // ===== CombatFlowService accessors =====
    public PendingAttackMode Combat_GetPendingAttackMode() => _pendingAttackMode;
    public AttackType Combat_GetCurrentAttackType() => _currentAttackType;
    public void Combat_SetCurrentAttackType(AttackType attackType) => _currentAttackType = attackType;

    public bool Combat_IsInAttackSequence() => _isInAttackSequence;
    public CharacterController Combat_GetAttackingCharacter() => _attackingCharacter;

    public int Combat_GetCurrentAttackBAB() => _currentAttackBAB;
    public void Combat_SetCurrentAttackBAB(int value) => _currentAttackBAB = value;

    public int Combat_GetTotalAttackBudget() => _totalAttackBudget;
    public void Combat_SetTotalAttackBudget(int value) => _totalAttackBudget = value;

    public int Combat_GetTotalAttacksUsed() => _totalAttacksUsed;
    public void Combat_SetTotalAttacksUsed(int value) => _totalAttacksUsed = value;

    public bool Combat_GetAttackSequenceConsumesFullRound() => _attackSequenceConsumesFullRound;
    public void Combat_SetAttackSequenceConsumesFullRound(bool value) => _attackSequenceConsumesFullRound = value;

    public bool Combat_IsDualWielding() => _isDualWielding;
    public int Combat_GetMainHandPenalty() => _mainHandPenalty;
    public int Combat_GetOffHandPenalty() => _offHandPenalty;

    public ItemData Combat_GetEquippedWeapon() => _equippedWeapon;
    public void Combat_SetEquippedWeapon(ItemData weapon) => _equippedWeapon = weapon;

    public string Combat_GetLastCombatLog() => _lastCombatLog;
    public void Combat_SetLastCombatLog(string value) => _lastCombatLog = value;

    public bool Combat_ConsumeSkipNextSingleAttackStandardActionCommitFlag()
    {
        bool value = _skipNextSingleAttackStandardActionCommit;
        _skipNextSingleAttackStandardActionCommit = false;
        return value;
    }

    public bool Combat_HasPendingNaturalAttackSelection() => HasPendingNaturalAttackSelection();
    public int Combat_GetPendingNaturalAttackSequenceIndex() => _pendingNaturalAttackSequenceIndex;
    public string Combat_GetPendingNaturalAttackLabel() => _pendingNaturalAttackLabel;
    public void Combat_ClearPendingNaturalAttackSelection() => ClearPendingNaturalAttackSelection();
    public int Combat_GetWeaponAttacksCommittedThisTurn() => _weaponAttacksCommittedThisTurn;
    public bool Combat_TryEnterProgressiveFullAttackStage(CharacterController attacker, string attemptedActionLabel)
        => TryEnterProgressiveFullAttackStage(attacker, attemptedActionLabel);
    public void Combat_RegisterWeaponAttackCommitted(CharacterController attacker)
        => RegisterWeaponAttackCommitted(attacker);
    public void Combat_MarkNaturalAttackSequenceIndexUsed(int sequenceIndex)
    {
        if (sequenceIndex >= 0)
            _usedNaturalAttackSequenceIndices.Add(sequenceIndex);
    }

    public void Combat_ClearPendingDefensiveAttackSelectionFlag()
    {
        _pendingDefensiveAttackSelection = false;
    }

    public void Combat_SetSubPhase(PlayerSubPhase subPhase) => CurrentSubPhase = subPhase;
    public void Combat_SetPhase(TurnPhase phase) => CurrentPhase = phase;

    public List<CharacterController> Combat_GetAllCharacters() => GetAllCharacters();
    public bool Combat_IsEnemyTeam(CharacterController source, CharacterController target) => IsEnemyTeam(source, target);
    public bool Combat_IsUsingThrownAttackMode(CharacterController attacker, ItemData weapon = null) => IsUsingThrownAttackMode(attacker, weapon);
    public bool Combat_IsAttackModeRanged(CharacterController attacker, ItemData weapon = null) => IsAttackModeRanged(attacker, weapon);
    public bool Combat_IsTargetInCurrentWeaponRange(CharacterController attacker, CharacterController target) => IsTargetInCurrentWeaponRange(attacker, target);

    public bool Combat_IsMeleeFearBreakAttack(CharacterController attacker, ItemData weapon, RangeInfo rangeInfo, bool treatAsThrownAttack)
        => IsMeleeAttackForTurnUndeadFearBreak(attacker, weapon, rangeInfo, treatAsThrownAttack);

    public void Combat_ProcessTurnUndeadMeleeFearBreak(CharacterController attacker, CharacterController target, bool isMeleeFearBreakAttack)
        => ProcessTurnUndeadMeleeFearBreak(attacker, target, isMeleeFearBreakAttack);

    public void Combat_ResolveThrownWeaponAfterAttack(CharacterController attacker, CharacterController target, ItemData weapon)
        => ResolveThrownWeaponAfterAttack(attacker, target, weapon);

    public bool Combat_HasMoreAttacksAvailable() => HasMoreAttacksAvailable();
    public void Combat_EndAttackSequence() => EndAttackSequence();

    public void Combat_UpdateAllStatsUI() => UpdateAllStatsUI();
    public void Combat_ClearHighlights()
    {
        Grid?.ClearAllHighlights();
        _highlightedCells?.Clear();
    }

    public void Combat_CheckConcentrationOnDamage(CharacterController target, int damage)
        => CheckConcentrationOnDamage(target, damage);

    public void Combat_BreakCharmOnHostileAction(CharacterController attacker, CharacterController target)
        => BreakCharmOnHostileAction(attacker, target);

    public void Combat_TryResolveFreeTripOnHit(CharacterController attacker, CharacterController target, CombatResult attackResult, RangeInfo attackRange)
        => TryResolveFreeTripOnHit(attacker, target, attackResult, attackRange);

    public bool Combat_TryResolveImprovedGrabAfterSingleAttack(CharacterController attacker, CharacterController target, CombatResult attackResult, System.Action onResolved)
        => TryResolveImprovedGrabAfterSingleAttack(attacker, target, attackResult, onResolved);

    public void Combat_HandleSummonDeathCleanup(CharacterController target)
        => HandleSummonDeathCleanup(target);

    public bool Combat_AreAllNPCsDead() => AreAllNPCsDead();
    public int Combat_GetAliveNPCCount() => GetAliveNPCCount();
    public bool Combat_CheckCombatVictory(string sourceContext, CharacterController defeatedTarget = null)
        => CheckCombatVictory(sourceContext, defeatedTarget);

    public bool Combat_AreAllPCsDead() => AreAllPCsDead();

    public void Combat_ShowActionChoices() => ShowActionChoices();

    public Coroutine Combat_StartAfterAttackDelay(CharacterController attacker, float delay)
        => StartCoroutine(AfterAttackDelay(attacker, delay));

    public Coroutine Combat_StartDelayedEndActivePCTurn(float delay)
        => StartCoroutine(DelayedEndActivePCTurn(delay));

    public Coroutine Combat_StartFullAttackRetargeting(CharacterController attacker, CharacterController target)
        => StartCoroutine(PerformFullAttackWithRetargetingAndFiveFootStep(attacker, target));

    public string Combat_BuildFlankingIndicator(bool isFlanking, CharacterController flankPartner)
        => CombatUI != null ? CombatUI.BuildFlankingIndicator(isFlanking, flankPartner) : string.Empty;
}
