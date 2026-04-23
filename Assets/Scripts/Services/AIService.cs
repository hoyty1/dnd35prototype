using System;
using System.Collections;
using System.Collections.Generic;
using DND35.AI;
using UnityEngine;

/// <summary>
/// Centralized NPC AI orchestration and tactical decision-making.
/// GameManager delegates hostile turn decisions and target/movement evaluation to this service.
/// </summary>
public class AIService : MonoBehaviour
{
    public enum AIDifficultyLevel
    {
        Easy,
        Normal,
        Hard
    }

    public enum AIActionType
    {
        Wait,
        Move,
        Attack,
        SpecialManeuver,
        Charge,
        Retreat
    }

    private GameManager _gameManager;

    [SerializeField] private AIDifficultyLevel _difficulty = AIDifficultyLevel.Normal;

    public void Initialize(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public void Cleanup()
    {
        _gameManager = null;
    }

    public IEnumerator ExecuteNPCTurn(CharacterController npc, EnemyAIBehavior behavior)
    {
        if (_gameManager == null || npc == null || npc.Stats == null)
            yield break;

        _gameManager.BeginNPCTurnForAI(npc);

        bool isSummon = _gameManager.IsSummonedCreature(npc);
        string turnColor = isSummon ? "#66E8FF" : "#FF6666";
        string turnIcon = isSummon ? "✶" : "💀";

        _gameManager.CombatUI.SetTurnIndicator($"{_gameManager.GetSummonDisplayName(npc)}'s turn...");
        _gameManager.CombatUI.ShowCombatLog($"<color={turnColor}>{turnIcon} {_gameManager.GetSummonDisplayName(npc)}'s turn begins</color>");
        yield return new WaitForSeconds(0.6f);

        CharacterController targetPC = SelectBestTarget(npc, _gameManager.GetAllCharactersForAI());
        if (targetPC == null)
            yield break;

        if (npc.HasCondition(CombatConditionType.Turned) && _gameManager.IsUndeadCharacterForAI(npc))
        {
            yield return _gameManager.StartCoroutine(ExecuteTurnedUndeadTurn(npc));
            yield break;
        }

        if (npc.IsGrappling())
        {
            yield return _gameManager.StartCoroutine(_gameManager.ExecuteGrappleRestrictedTurnForAI(npc));
            yield break;
        }

        if (isSummon)
        {
            yield return _gameManager.StartCoroutine(_gameManager.ExecuteSummonedCreatureTurnForAI(npc));
            yield break;
        }

        AIProfile profile = GetProfile(npc);
        if (profile != null)
        {
            if (profile.CombatStyle == CombatStyle.Ranged)
            {
                yield return _gameManager.StartCoroutine(ExecuteRangedKiterTurn(npc));
            }
            else
            {
                // Melee and mixed currently share melee tactical shell; maneuvers/targeting are profile-driven.
                yield return _gameManager.StartCoroutine(ExecuteAggressiveMeleeTurn(npc, targetPC));
            }

            yield break;
        }

        switch (behavior)
        {
            case EnemyAIBehavior.AggressiveMelee:
                yield return _gameManager.StartCoroutine(ExecuteAggressiveMeleeTurn(npc, targetPC));
                break;
            case EnemyAIBehavior.RangedKiter:
                yield return _gameManager.StartCoroutine(ExecuteRangedKiterTurn(npc));
                break;
            case EnemyAIBehavior.DefensiveMelee:
                yield return _gameManager.StartCoroutine(ExecuteDefensiveMeleeTurn(npc, targetPC));
                break;
            default:
                yield return _gameManager.StartCoroutine(ExecuteAggressiveMeleeTurn(npc, targetPC));
                break;
        }
    }

    private IEnumerator ExecuteTurnedUndeadTurn(CharacterController npc)
    {
        if (npc == null || npc.Stats == null)
            yield break;

        CharacterController source = _gameManager.GetTurnUndeadTurnerForAI(npc);

        if (source == null)
        {
            List<StatusEffect> activeConditions = npc.GetActiveConditions();
            for (int i = 0; i < activeConditions.Count; i++)
            {
                StatusEffect condition = activeConditions[i];
                if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Turned)
                    continue;

                if (string.IsNullOrWhiteSpace(condition.SourceName))
                    break;

                List<CharacterController> all = _gameManager.GetAllCharactersForAI();
                for (int c = 0; c < all.Count; c++)
                {
                    CharacterController candidate = all[c];
                    if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                        continue;

                    if (string.Equals(candidate.Stats.CharacterName, condition.SourceName, StringComparison.Ordinal))
                    {
                        source = candidate;
                        _gameManager.RegisterTurnUndeadTrackerForAI(npc, source);
                        break;
                    }
                }
                break;
            }
        }

        if (source == null)
            source = _gameManager.GetClosestAliveEnemyToForAI(npc);

        if (source != null && npc.Actions.HasMoveAction && !npc.Stats.MovementBlockedByCondition)
        {
            SquareCell retreatCell = EvaluateMovementOptions(npc, source.GridPosition, retreat: true);
            if (retreatCell != null && retreatCell.Coords != npc.GridPosition)
            {
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, retreatCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI()));
                npc.Actions.UseMoveAction();
                _gameManager.CombatUI?.ShowCombatLog($"↩ {npc.Stats.CharacterName} flees from divine turning!");
                yield return new WaitForSeconds(0.45f);
                yield break;
            }
        }

        _gameManager.CombatUI?.ShowCombatLog($"↩ {npc.Stats.CharacterName} is turned and cowers, unable to attack.");
        yield return new WaitForSeconds(0.35f);
    }

    private IEnumerator ExecuteAggressiveMeleeTurn(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null || target.Stats == null || target.Stats.IsDead)
            yield break;

        AIActionType action = SelectBestAction(npc, target, preferAggression: true);
        if (action == AIActionType.Charge)
        {
            yield return _gameManager.StartCoroutine(_gameManager.NPCExecuteChargeForAI(npc, target));
            yield break;
        }

        AIProfile profile = GetProfile(npc);

        if (!npc.IsTargetInCurrentWeaponRange(target))
        {
            SquareCell bestCell = EvaluateMovementOptions(npc, target.GridPosition, retreat: false, target, profile);
            if (bestCell != null)
            {
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, bestCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI()));
                npc.Actions.UseMoveAction();
                _gameManager.CombatUI.ShowCombatLog($"{npc.Stats.CharacterName} advances toward {target.Stats.CharacterName}!");
                yield return new WaitForSeconds(0.5f);
            }
        }

        target = SelectBestTarget(npc, _gameManager.GetAllCharactersForAI());
        if (target == null)
            yield break;

        if (npc.IsTargetInCurrentWeaponRange(target) && !target.Stats.IsDead)
        {
            bool usedSpecial = ShouldUseManeuver(npc, target) && TryExecutePreferredManeuver(npc, target, profile);
            if (!usedSpecial)
                yield return _gameManager.StartCoroutine(_gameManager.NPCPerformAttackForAI(npc, target));
            else
                yield return new WaitForSeconds(0.8f);
        }
        else
        {
            yield return new WaitForSeconds(0.3f);
        }
    }

    private IEnumerator ExecuteRangedKiterTurn(CharacterController npc)
    {
        AIProfile profile = GetProfile(npc);

        CharacterController closestPC = SelectBestTarget(npc, _gameManager.GetAllCharactersForAI());
        if (closestPC == null)
            yield break;

        int distToClosestPC = SquareGridUtils.GetDistance(npc.GridPosition, closestPC.GridPosition);
        if (distToClosestPC <= 2)
        {
            SquareCell retreatCell = EvaluateMovementOptions(npc, closestPC.GridPosition, retreat: true, profile: profile);
            if (retreatCell != null)
            {
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, retreatCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI()));
                npc.Actions.UseMoveAction();
                _gameManager.CombatUI.ShowCombatLog($"{npc.Stats.CharacterName} retreats to maintain distance!");
                yield return new WaitForSeconds(0.5f);
            }
        }

        CharacterController rangedTarget = SelectBestTarget(npc, _gameManager.GetAllCharactersForAI());
        if (rangedTarget == null)
            yield break;

        int maxRange = GetMaximumAttackRangeInSquares(npc);
        int distToRangedTarget = SquareGridUtils.GetDistance(npc.GridPosition, rangedTarget.GridPosition);

        if (distToRangedTarget <= maxRange && !rangedTarget.Stats.IsDead)
        {
            bool usedSpecial = ShouldUseManeuver(npc, rangedTarget) && TryExecutePreferredManeuver(npc, rangedTarget, profile);
            if (!usedSpecial)
                yield return _gameManager.StartCoroutine(_gameManager.NPCPerformAttackForAI(npc, rangedTarget));
            else
                yield return new WaitForSeconds(0.8f);
        }
        else if (distToRangedTarget > maxRange && npc.Actions.HasMoveAction)
        {
            SquareCell approachCell = EvaluateMovementOptions(npc, rangedTarget.GridPosition, retreat: false, profile: profile);
            if (approachCell != null)
            {
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, approachCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI()));
                npc.Actions.UseMoveAction();
                _gameManager.CombatUI.ShowCombatLog($"{npc.Stats.CharacterName} moves to get a better shot.");
                yield return new WaitForSeconds(0.5f);
            }
        }
        else
        {
            yield return new WaitForSeconds(0.3f);
        }
    }

    private IEnumerator ExecuteDefensiveMeleeTurn(CharacterController npc, CharacterController preferredTarget)
    {
        CharacterController weakerPC = SelectLowestHPEnemy(npc);
        CharacterController target = weakerPC != null ? weakerPC : preferredTarget;

        if (npc == null || target == null || target.Stats == null || target.Stats.IsDead)
            yield break;

        AIActionType action = SelectBestAction(npc, target, preferAggression: false);
        if (action == AIActionType.Charge)
        {
            yield return _gameManager.StartCoroutine(_gameManager.NPCExecuteChargeForAI(npc, target));
            yield break;
        }

        AIProfile profile = GetProfile(npc);

        if (!npc.IsTargetInCurrentWeaponRange(target))
        {
            SquareCell bestCell = EvaluateMovementOptions(npc, target.GridPosition, retreat: false, target, profile);
            if (bestCell != null)
            {
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, bestCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI()));
                npc.Actions.UseMoveAction();
                _gameManager.CombatUI.ShowCombatLog($"{npc.Stats.CharacterName} advances methodically toward {target.Stats.CharacterName}.");
                yield return new WaitForSeconds(0.5f);
            }
        }

        target = SelectBestTarget(npc, _gameManager.GetAllCharactersForAI());
        if (target == null)
            yield break;

        if (npc.IsTargetInCurrentWeaponRange(target) && !target.Stats.IsDead)
        {
            bool usedSpecial = ShouldUseManeuver(npc, target) && TryExecutePreferredManeuver(npc, target, profile);
            if (!usedSpecial)
                yield return _gameManager.StartCoroutine(_gameManager.NPCPerformAttackForAI(npc, target));
            else
                yield return new WaitForSeconds(0.8f);
        }
        else
        {
            yield return new WaitForSeconds(0.3f);
        }
    }

    private const string ArmorPriorityBehaviorTag = "Uses Armor-Based Targeting";

    private static AIProfile GetProfile(CharacterController npc)
    {
        return npc != null ? npc.aiProfile : null;
    }

    public CharacterController SelectBestTarget(CharacterController npc, List<CharacterController> allCombatants)
    {
        if (npc == null || allCombatants == null)
            return null;

        AIProfile profile = GetProfile(npc);
        if (profile != null)
        {
            CharacterController profiled = SelectBestTargetFromProfile(npc, allCombatants, profile);
            if (profiled != null)
                return profiled;
        }

        if (UsesArmorPriorityTargeting(npc))
        {
            CharacterController prioritized = SelectBestArmorPriorityTarget(npc, allCombatants);
            if (prioritized != null)
                return prioritized;
        }

        return SelectBestTargetDefault(npc, allCombatants);
    }

    private CharacterController SelectBestTargetFromProfile(CharacterController npc, List<CharacterController> allCombatants, AIProfile profile)
    {
        CharacterController best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!_gameManager.IsEnemyTeamForAI(npc, candidate))
                continue;

            float score = profile.ScoreTarget(candidate, npc);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best != null)
            Debug.Log($"[AI][Profile:{profile.ProfileName}] {npc.Stats.CharacterName} targets {best.Stats.CharacterName} score={bestScore:F1}");

        return best;
    }

    private CharacterController SelectBestTargetDefault(CharacterController npc, List<CharacterController> allCombatants)
    {
        CharacterController best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!_gameManager.IsEnemyTeamForAI(npc, candidate))
                continue;

            float score = GetTargetPriority(npc, candidate);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private CharacterController SelectBestArmorPriorityTarget(CharacterController npc, List<CharacterController> allCombatants)
    {
        CharacterController best = null;
        float bestScore = float.NegativeInfinity;

        int maxRange = GetMaximumAttackRangeInSquares(npc);

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!_gameManager.IsEnemyTeamForAI(npc, candidate))
                continue;

            int distance = SquareGridUtils.GetDistance(npc.GridPosition, candidate.GridPosition);
            if (distance > maxRange)
                continue;

            float armorScore = GetArmorPriorityScore(candidate);
            float distanceBonus = Mathf.Max(0f, maxRange - distance) * 2f;
            float woundedBonus = Mathf.Clamp01(1f - ((float)candidate.Stats.CurrentHP / Mathf.Max(1f, candidate.Stats.TotalMaxHP))) * 1.5f;
            float totalScore = armorScore + distanceBonus + woundedBonus;

            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                best = candidate;
            }
        }

        if (best != null)
        {
            Debug.Log($"[AI][ArmorPriority] {npc.Stats.CharacterName} targets {best.Stats.CharacterName} ({best.GetArmorTag()}) score={bestScore:F1}");
        }

        return best;
    }

    private bool UsesArmorPriorityTargeting(CharacterController npc)
    {
        if (npc == null)
            return false;

        if (npc.Tags != null && npc.Tags.HasTag(ArmorPriorityBehaviorTag))
            return true;

        if (npc.Stats != null && npc.Stats.CreatureTags != null)
        {
            for (int i = 0; i < npc.Stats.CreatureTags.Count; i++)
            {
                if (string.Equals(npc.Stats.CreatureTags[i], ArmorPriorityBehaviorTag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private float GetArmorPriorityScore(CharacterController target)
    {
        if (target == null)
            return 0f;

        if (target.Tags != null)
        {
            if (target.Tags.HasTag("Unarmored")) return 100f;
            if (target.Tags.HasTag("Light Armor")) return 75f;
            if (target.Tags.HasTag("Medium Armor")) return 50f;
            if (target.Tags.HasTag("Heavy Armor")) return 25f;
        }

        string armorTag = target.GetArmorTag();
        if (string.Equals(armorTag, "Unarmored", StringComparison.OrdinalIgnoreCase)) return 100f;
        if (string.Equals(armorTag, "Light Armor", StringComparison.OrdinalIgnoreCase)) return 75f;
        if (string.Equals(armorTag, "Medium Armor", StringComparison.OrdinalIgnoreCase)) return 50f;
        if (string.Equals(armorTag, "Heavy Armor", StringComparison.OrdinalIgnoreCase)) return 25f;

        return 10f;
    }

    public float GetTargetPriority(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null || target.Stats == null)
            return float.MinValue;

        int distance = SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition);
        float distanceScore = Mathf.Max(0f, 12f - distance) * 1.8f; // proximity

        float threatScore = CalculateThreat(npc, target) * 2.0f; // healers/casters/high offense

        float hpRatio = target.Stats.TotalMaxHP > 0
            ? Mathf.Clamp01((float)target.Stats.CurrentHP / target.Stats.TotalMaxHP)
            : 1f;
        float woundedBonus = (1f - hpRatio) * 8f; // finish wounded enemies

        float armorEase = Mathf.Clamp(26f - target.Stats.ArmorClass, -6f, 8f) * 0.7f; // easier AC is better

        bool flankingOpportunity = CombatUtils.CanThreatenTargetFromPosition(npc, npc.GridPosition, target);
        float flankingBonus = flankingOpportunity ? 2.5f : 0f;

        string creatureType = target.Stats.CreatureType ?? string.Empty;
        bool vulnerableType = creatureType.IndexOf("undead", StringComparison.OrdinalIgnoreCase) >= 0
            || creatureType.IndexOf("outsider", StringComparison.OrdinalIgnoreCase) >= 0;
        float vulnerabilityBonus = vulnerableType ? 1.5f : 0f;

        return distanceScore + threatScore + woundedBonus + armorEase + flankingBonus + vulnerabilityBonus;
    }

    public float CalculateThreat(CharacterController observer, CharacterController target)
    {
        if (target == null || target.Stats == null)
            return 0f;

        float score = 0f;

        if (target.Stats.IsCleric || target.Stats.IsWizard)
            score += 3f;
        if (target.Stats.IsRogue)
            score += 2f;

        score += Mathf.Clamp(target.Stats.Level * 0.35f, 0f, 4f);
        score += Mathf.Clamp(target.Stats.STRMod + target.Stats.DEXMod, -2f, 6f) * 0.35f;

        if (target.HasCondition(CombatConditionType.Prone))
            score -= 0.75f;

        return score;
    }

    public SquareCell EvaluateMovementOptions(CharacterController mover, Vector2Int targetPos, bool retreat, CharacterController targetCharacter = null, AIProfile profile = null)
    {
        if (mover == null || mover.Stats == null || _gameManager.Grid == null)
            return null;

        if (profile == null)
            profile = GetProfile(mover);

        List<SquareCell> moveCells = _gameManager.Grid.GetCellsInRange(mover.GridPosition, mover.Stats.MoveRange);
        List<CharacterController> allCombatants = targetCharacter != null ? _gameManager.GetAllCharactersForAI() : null;

        SquareCell bestCell = null;
        float bestScore = float.NegativeInfinity;

        int preferredRange = profile != null && profile.Movement != null
            ? Mathf.Max(0, profile.Movement.PreferredRangeSquares)
            : 1;

        bool avoidAoOs = profile != null && profile.Movement != null && profile.Movement.AvoidAoOs;
        bool seekFlanking = profile == null || profile.Movement == null || profile.Movement.SeekFlanking;

        for (int i = 0; i < moveCells.Count; i++)
        {
            SquareCell cell = moveCells[i];
            if (cell == null)
                continue;

            if (!_gameManager.Grid.CanPlaceCreature(cell.Coords, mover.GetVisualSquaresOccupied(), mover))
                continue;

            AoOPathResult pathResult = _gameManager.FindPath(mover, cell.Coords, avoidThreats: false, maxRangeOverride: mover.Stats.MoveRange);
            if (pathResult == null || pathResult.Path == null)
                continue;

            int dist = SquareGridUtils.GetDistance(cell.Coords, targetPos);
            bool canThreatenFromCell = false;
            bool wouldFlankFromCell = false;
            if (targetCharacter != null)
            {
                canThreatenFromCell = CombatUtils.CanThreatenTargetFromPosition(mover, cell.Coords, targetCharacter);
                if (canThreatenFromCell)
                {
                    CharacterController flankPartner;
                    wouldFlankFromCell = CombatUtils.IsAttackerFlankingFromPosition(
                        mover,
                        cell.Coords,
                        targetCharacter,
                        allCombatants,
                        out flankPartner);
                }
            }

            float score;
            if (retreat)
            {
                score = dist * 2f;
            }
            else
            {
                int distanceToPreferred = Mathf.Abs(dist - preferredRange);
                score = -distanceToPreferred * 2f;
                if (canThreatenFromCell)
                    score += 2f;
                if (seekFlanking && wouldFlankFromCell)
                    score += 3f;
            }

            if (pathResult.ProvokesAoOs)
                score += avoidAoOs ? -1000f : -2f * pathResult.ProvokedAoOs.Count;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    public float EvaluateAttackOptions(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null || target.Stats == null)
            return float.MinValue;

        bool inRange = npc.IsTargetInCurrentWeaponRange(target);
        float score = inRange ? 8f : 2f;

        score += ShouldUseManeuver(npc, target) ? 2.5f : 0.75f;

        SpellData spell = SelectSpell(npc, target);
        if (spell != null)
            score += 1.5f;

        float hpPercent = npc.Stats.TotalMaxHP > 0 ? (float)npc.Stats.CurrentHP / npc.Stats.TotalMaxHP : 1f;
        if (hpPercent < 0.35f)
            score -= 2f;

        return score;
    }

    public bool ShouldUseManeuver(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null || npc.IsGrappling())
            return false;
        if (!npc.Actions.HasStandardAction)
            return false;

        AIProfile profile = GetProfile(npc);
        if (profile != null)
        {
            SpecialAttackType? preferred = profile.GetPreferredManeuver(npc, target);
            if (preferred.HasValue)
            {
                if (preferred.Value == SpecialAttackType.Trip)
                    return !target.HasCondition(CombatConditionType.Prone) && npc.HasMeleeWeaponEquipped();

                if (preferred.Value == SpecialAttackType.Disarm)
                    return target.GetEquippedMainWeapon() != null;

                if (preferred.Value == SpecialAttackType.Grapple)
                    return profile.ShouldInitiateGrapple(npc, target);

                return true;
            }

            // Profile present but no preferred maneuver => obey profile and skip legacy fallback.
            return false;
        }

        if (target.GetEquippedMainWeapon() != null && npc.Stats.STRMod >= 3)
            return true; // disarm preference
        if (!target.Stats.IsProne && npc.HasMeleeWeaponEquipped())
            return true; // trip preference

        return npc.Stats.STRMod >= 4;
    }

    private bool TryExecutePreferredManeuver(CharacterController npc, CharacterController target, AIProfile profile)
    {
        if (npc == null || target == null)
            return false;

        if (profile != null)
        {
            SpecialAttackType? preferred = profile.GetPreferredManeuver(npc, target);
            if (preferred.HasValue)
                return _gameManager.TryNPCSpecialAttackByTypeForAI(npc, target, preferred.Value);

            return false;
        }

        return _gameManager.TryNPCSpecialAttackIfBeneficialForAI(npc, target);
    }

    public SpellData SelectSpell(CharacterController caster, CharacterController target)
    {
        if (caster == null || caster.Stats == null)
            return null;

        SpellcastingComponent spellcasting = caster.GetComponent<SpellcastingComponent>();
        if (spellcasting == null || !spellcasting.CanCastSpells || !spellcasting.HasAnyCastablePreparedSpell())
            return null;

        List<SpellData> castable = spellcasting.GetCastablePreparedSpells();
        if (castable == null || castable.Count == 0)
            return null;

        SpellData best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < castable.Count; i++)
        {
            SpellData spell = castable[i];
            if (spell == null)
                continue;

            int score = 0;
            if (spell.EffectType == SpellEffectType.Healing)
                score += caster.Stats.CurrentHP <= Mathf.CeilToInt(caster.Stats.TotalMaxHP * 0.4f) ? 10 : 2;
            if (spell.EffectType == SpellEffectType.Buff)
                score += 4;
            if (spell.EffectType == SpellEffectType.Damage || spell.EffectType == SpellEffectType.Debuff)
                score += 6;
            if (target != null && target.Stats != null && target.Stats.CurrentHP <= Mathf.CeilToInt(target.Stats.TotalMaxHP * 0.35f))
                score += 2;

            if (score > bestScore)
            {
                bestScore = score;
                best = spell;
            }
        }

        return best;
    }

    public AIActionType SelectBestAction(CharacterController npc, CharacterController target, bool preferAggression)
    {
        if (npc == null || target == null)
            return AIActionType.Wait;

        float hpPercent = npc.Stats.TotalMaxHP > 0 ? (float)npc.Stats.CurrentHP / npc.Stats.TotalMaxHP : 1f;
        int distance = SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition);
        bool canCharge = _gameManager.ShouldNPCUseChargeForAI(npc, target);

        if (!preferAggression && hpPercent < 0.30f && npc.Actions.HasMoveAction)
            return AIActionType.Retreat;

        if (canCharge)
            return AIActionType.Charge;

        if (npc.IsTargetInCurrentWeaponRange(target))
        {
            if (ShouldUseManeuver(npc, target))
                return AIActionType.SpecialManeuver;
            return AIActionType.Attack;
        }

        if (distance > 1 && npc.Actions.HasMoveAction)
            return AIActionType.Move;

        return AIActionType.Wait;
    }

    private CharacterController SelectLowestHPEnemy(CharacterController npc)
    {
        List<CharacterController> all = _gameManager.GetAllCharactersForAI();
        CharacterController weakest = null;
        int lowestHP = int.MaxValue;

        for (int i = 0; i < all.Count; i++)
        {
            CharacterController candidate = all[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!_gameManager.IsEnemyTeamForAI(npc, candidate))
                continue;

            if (candidate.Stats.CurrentHP < lowestHP)
            {
                lowestHP = candidate.Stats.CurrentHP;
                weakest = candidate;
            }
        }

        return weakest;
    }

    private int GetMaximumAttackRangeInSquares(CharacterController npc)
    {
        ItemData weapon = npc.GetEquippedMainWeapon();
        if (weapon == null)
            return 1;

        if (weapon.WeaponCat == WeaponCategory.Ranged || weapon.RangeIncrement > 0)
            return RangeCalculator.GetMaxRangeSquares(weapon.RangeIncrement, weapon.IsThrown);

        return 1;
    }
}
