using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// D&D 3.5e Charmed behavior controller.
/// Charmed creatures treat the caster as a trusted ally, avoid attacking them,
/// and attempt to help (including healing) when possible.
/// </summary>
public sealed class CharmedBehaviorController
{
    public sealed class CharmedTurnDecision
    {
        public CharacterController CasterSource;
        public ConditionService.ActiveCondition Condition;
        public CharmedConditionData CharmData;
    }

    public bool TryBuildDecision(GameManager gameManager, CharacterController actor, out CharmedTurnDecision decision)
    {
        decision = null;
        if (gameManager == null || actor == null || actor.Stats == null || actor.Stats.IsDead)
            return false;

        if (!actor.HasCondition(CombatConditionType.Charmed))
            return false;

        List<ConditionService.ActiveCondition> active = gameManager.GetActiveConditions(actor);
        if (active == null || active.Count == 0)
            return false;

        for (int i = 0; i < active.Count; i++)
        {
            ConditionService.ActiveCondition condition = active[i];
            if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Charmed)
                continue;

            CharacterController source = condition.Source;
            CharmedConditionData data = condition.Data as CharmedConditionData;
            if (data != null)
            {
                data.RefreshRemainingRounds(condition.RemainingRounds);
                if (source == null)
                    source = data.Caster;
            }

            if (source == null && !string.IsNullOrWhiteSpace(condition.SourceName))
                source = FindCharacterByName(gameManager, condition.SourceName);

            decision = new CharmedTurnDecision
            {
                CasterSource = source,
                Condition = condition,
                CharmData = data
            };
            return true;
        }

        return false;
    }

    public IEnumerator ExecuteDecision(GameManager gameManager, CharacterController actor, CharmedTurnDecision decision)
    {
        if (gameManager == null || actor == null || actor.Stats == null || decision == null)
            yield break;

        CharacterController caster = decision.CasterSource;
        if (caster == null || caster.Stats == null || caster.Stats.IsDead)
        {
            gameManager.RemoveCondition(actor, CombatConditionType.Charmed);
            yield break;
        }

        gameManager.CombatUI?.ShowCombatLog($"💞 {actor.Stats.CharacterName} is charmed by {caster.Stats.CharacterName} and will not attack them.");

        bool casterInjured = caster.Stats.CurrentHP < caster.Stats.TotalMaxHP;
        if (casterInjured)
        {
            // Try healing spell first.
            if (TryGetBestHealingSpell(actor, out SpellData healingSpell))
            {
                int distance = actor.GetMinimumDistanceToTarget(caster, chebyshev: true);
                if (distance > 1 && HasAnyMoveAction(actor))
                {
                    if (TryMoveAdjacentToCaster(gameManager, actor, caster))
                        yield return new WaitForSeconds(0.35f);
                }

                if (actor.GetMinimumDistanceToTarget(caster, chebyshev: true) <= 1 && actor.Actions.HasStandardAction)
                {
                    if (gameManager.TryNPCPerformSpellCastForAI(actor, caster, healingSpell))
                    {
                        gameManager.CombatUI?.ShowCombatLog($"💚 {actor.Stats.CharacterName} uses {healingSpell.Name} to aid {caster.Stats.CharacterName}.");
                        yield break;
                    }
                }
            }

            // Then try healing consumable.
            if (TryUseHealingConsumableOnCaster(gameManager, actor, caster))
            {
                yield return new WaitForSeconds(0.35f);
                yield break;
            }
        }

        CharacterController hostileToCaster = FindNearestEnemyOfCaster(gameManager, actor, caster);
        if (hostileToCaster == null)
        {
            gameManager.CombatUI?.ShowCombatLog($"💞 {actor.Stats.CharacterName} stays near {caster.Stats.CharacterName}.");
            yield return new WaitForSeconds(0.25f);
            yield break;
        }

        if (!actor.IsTargetInCurrentWeaponRange(hostileToCaster) && HasAnyMoveAction(actor))
        {
            if (TryMoveTowardTarget(gameManager, actor, hostileToCaster))
                yield return new WaitForSeconds(0.35f);
        }

        if (actor.IsTargetInCurrentWeaponRange(hostileToCaster) && actor.Actions.HasStandardAction)
            yield return gameManager.StartCoroutine(gameManager.NPCPerformAttackForAI(actor, hostileToCaster));
    }

    private static bool TryGetBestHealingSpell(CharacterController actor, out SpellData healingSpell)
    {
        healingSpell = null;
        if (actor == null || actor.Stats == null || !actor.Stats.IsSpellcaster)
            return false;

        SpellcastingComponent spellcasting = actor.GetComponent<SpellcastingComponent>();
        if (spellcasting == null || !spellcasting.CanCastSpells)
            return false;

        List<SpellData> castable = spellcasting.GetCastablePreparedSpells();
        if (castable == null || castable.Count == 0)
            return false;

        int bestPower = int.MinValue;
        for (int i = 0; i < castable.Count; i++)
        {
            SpellData candidate = castable[i];
            if (candidate == null || candidate.EffectType != SpellEffectType.Healing)
                continue;

            int score = candidate.HealCount * Mathf.Max(1, candidate.HealDice) + candidate.BonusHealing;
            if (score > bestPower)
            {
                bestPower = score;
                healingSpell = candidate;
            }
        }

        return healingSpell != null;
    }

    private static bool TryUseHealingConsumableOnCaster(GameManager gameManager, CharacterController actor, CharacterController caster)
    {
        if (gameManager == null || actor == null || caster == null || actor.Stats == null || caster.Stats == null)
            return false;

        if (!actor.Actions.HasStandardAction)
            return false;

        if (actor.GetMinimumDistanceToTarget(caster, chebyshev: true) > 1)
        {
            if (!HasAnyMoveAction(actor))
                return false;

            if (!TryMoveAdjacentToCaster(gameManager, actor, caster))
                return false;
        }

        InventoryComponent invComp = actor.GetComponent<InventoryComponent>();
        Inventory inventory = invComp != null ? invComp.CharacterInventory : null;
        if (inventory == null || inventory.GeneralSlots == null)
            return false;

        for (int i = 0; i < inventory.GeneralSlots.Length; i++)
        {
            ItemData item = inventory.GeneralSlots[i];
            if (item == null || !item.IsConsumable)
                continue;
            if (!IsHealingConsumable(item))
                continue;

            int healed = ApplyConsumableHealing(actor, caster, item);
            if (healed <= 0)
                continue;

            inventory.RemoveItemAt(i);
            actor.Actions.UseStandardAction();
            gameManager.CombatUI?.ShowCombatLog($"🧪 {actor.Stats.CharacterName} uses {item.Name} to heal {caster.Stats.CharacterName} for {healed} HP.");
            gameManager.Combat_UpdateAllStatsUI();
            return true;
        }

        return false;
    }

    private static bool IsHealingConsumable(ItemData item)
    {
        if (item == null || !item.IsConsumable)
            return false;

        if (item.ConsumableEffect == ConsumableEffectType.HealHP)
            return true;

        if (item.HealAmount > 0 || (item.HealDiceCount > 0 && item.HealDiceSides > 0))
            return true;

        if (item.ConsumableEffect == ConsumableEffectType.SpellEffect)
        {
            string spellName = item.ConsumableSpellName;
            return !string.IsNullOrWhiteSpace(spellName)
                && spellName.ToLowerInvariant().Contains("cure");
        }

        return false;
    }

    private static int ApplyConsumableHealing(CharacterController actor, CharacterController caster, ItemData item)
    {
        if (actor == null || caster == null || item == null)
            return 0;

        // Spell-based consumables first (Potion of Cure...).
        if (item.ConsumableEffect == ConsumableEffectType.SpellEffect && !string.IsNullOrWhiteSpace(item.ConsumableSpellName))
        {
            SpellData spell = SpellDatabase.GetSpellByName(item.ConsumableSpellName);
            if (spell != null && spell.EffectType == SpellEffectType.Healing)
            {
                SpellResult result = SpellCaster.Cast(spell, actor.Stats, caster.Stats, null, forceFriendlyTouchNoRoll: true, forceTargetToFailSave: false, actor, caster);
                return result != null ? Mathf.Max(0, result.HealingDone) : 0;
            }
        }

        int rolled = 0;
        if (item.HealDiceCount > 0 && item.HealDiceSides > 0)
        {
            for (int die = 0; die < item.HealDiceCount; die++)
                rolled += Random.Range(1, item.HealDiceSides + 1);
            rolled += item.HealBonus;
        }
        else if (item.HealAmount > 0)
        {
            rolled = item.HealAmount;
        }

        if (rolled <= 0)
            return 0;

        int nonlethalHealed;
        return caster.Stats.HealDamage(rolled, out nonlethalHealed);
    }

    private static bool TryMoveAdjacentToCaster(GameManager gameManager, CharacterController actor, CharacterController caster)
    {
        if (gameManager == null || actor == null || caster == null)
            return false;

        Vector2Int[] neighbors = SquareGridUtils.GetNeighbors(caster.GridPosition);
        Vector2Int best = actor.GridPosition;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < neighbors.Length; i++)
        {
            Vector2Int candidate = neighbors[i];
            if (candidate == actor.GridPosition)
                continue;

            if (gameManager.Grid == null || !gameManager.Grid.CanPlaceCreature(candidate, actor.GetVisualSquaresOccupied(), actor))
                continue;

            AoOPathResult pathResult = gameManager.FindPath(actor, candidate, avoidThreats: false, maxRangeOverride: actor.Stats.MoveRange);
            if (pathResult == null || pathResult.Path == null || pathResult.Path.Count == 0)
                continue;

            int distance = SquareGridUtils.GetDistance(candidate, caster.GridPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        if (best == actor.GridPosition)
            return false;

        gameManager.StartCoroutine(gameManager.MoveCharacterAlongComputedPathForAI(actor, best, gameManager.GetPlayerMoveSecondsPerStepForAI()));
        ConsumeMoveAction(actor);
        return true;
    }

    private static bool TryMoveTowardTarget(GameManager gameManager, CharacterController actor, CharacterController target)
    {
        if (gameManager == null || actor == null || target == null)
            return false;

        List<SquareCell> moveCells = gameManager.Grid != null
            ? gameManager.Grid.GetCellsInRange(actor.GridPosition, actor.Stats.MoveRange)
            : null;

        if (moveCells == null || moveCells.Count == 0)
            return false;

        Vector2Int best = actor.GridPosition;
        int bestDistance = SquareGridUtils.GetDistance(actor.GridPosition, target.GridPosition);

        for (int i = 0; i < moveCells.Count; i++)
        {
            SquareCell cell = moveCells[i];
            if (cell == null || cell.Coords == actor.GridPosition)
                continue;

            if (gameManager.Grid == null || !gameManager.Grid.CanPlaceCreature(cell.Coords, actor.GetVisualSquaresOccupied(), actor))
                continue;

            AoOPathResult pathResult = gameManager.FindPath(actor, cell.Coords, avoidThreats: false, maxRangeOverride: actor.Stats.MoveRange);
            if (pathResult == null || pathResult.Path == null || pathResult.Path.Count == 0)
                continue;

            int distance = SquareGridUtils.GetDistance(cell.Coords, target.GridPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = cell.Coords;
            }
        }

        if (best == actor.GridPosition)
            return false;

        gameManager.StartCoroutine(gameManager.MoveCharacterAlongComputedPathForAI(actor, best, gameManager.GetPlayerMoveSecondsPerStepForAI()));
        ConsumeMoveAction(actor);
        return true;
    }

    private static CharacterController FindNearestEnemyOfCaster(GameManager gameManager, CharacterController actor, CharacterController caster)
    {
        if (gameManager == null || actor == null || caster == null)
            return null;

        List<CharacterController> all = gameManager.GetAllCharactersForAI();
        CharacterController best = null;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < all.Count; i++)
        {
            CharacterController candidate = all[i];
            if (candidate == null || candidate == actor || candidate == caster || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            if (!gameManager.IsEnemyTeamForAI(caster, candidate))
                continue;

            int distance = SquareGridUtils.GetDistance(actor.GridPosition, candidate.GridPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private static CharacterController FindCharacterByName(GameManager gameManager, string characterName)
    {
        if (gameManager == null || string.IsNullOrWhiteSpace(characterName))
            return null;

        List<CharacterController> all = gameManager.GetAllCharactersForAI();
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController candidate = all[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            if (string.Equals(candidate.Stats.CharacterName, characterName, System.StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    private static bool HasAnyMoveAction(CharacterController actor)
    {
        if (actor == null || actor.Actions == null)
            return false;

        return actor.Actions.HasMoveAction || actor.Actions.CanConvertStandardToMove;
    }

    private static void ConsumeMoveAction(CharacterController actor)
    {
        if (actor == null || actor.Actions == null)
            return;

        if (actor.Actions.HasMoveAction)
            actor.Actions.UseMoveAction();
        else if (actor.Actions.CanConvertStandardToMove)
            actor.Actions.ConvertStandardToMove();
    }
}
