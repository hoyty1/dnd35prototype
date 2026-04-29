using System.Collections.Generic;

/// <summary>
/// D&D 3.5e Fascinated behavior controller.
/// Fascinated creatures take no actions and remain focused on the fascination source.
/// </summary>
public sealed class FascinatedBehaviorController
{
    public sealed class FascinatedTurnDecision
    {
        public CharacterController CasterSource;
        public ConditionService.ActiveCondition Condition;
        public FascinatedConditionData FascinatedData;
    }

    public bool TryBuildDecision(GameManager gameManager, CharacterController actor, out FascinatedTurnDecision decision)
    {
        decision = null;
        if (gameManager == null || actor == null || actor.Stats == null || actor.Stats.IsDead)
            return false;

        if (!actor.HasCondition(CombatConditionType.Fascinated))
            return false;

        List<ConditionService.ActiveCondition> active = gameManager.GetActiveConditions(actor);
        if (active == null || active.Count == 0)
            return false;

        for (int i = 0; i < active.Count; i++)
        {
            ConditionService.ActiveCondition condition = active[i];
            if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Fascinated)
                continue;

            CharacterController source = condition.Source;
            FascinatedConditionData data = condition.Data as FascinatedConditionData;
            if (data != null)
            {
                data.RefreshRemainingRounds(condition.RemainingRounds);
                if (source == null)
                    source = data.Caster;
            }

            if (source == null && !string.IsNullOrWhiteSpace(condition.SourceName))
                source = FindCharacterByName(gameManager, condition.SourceName);

            decision = new FascinatedTurnDecision
            {
                CasterSource = source,
                Condition = condition,
                FascinatedData = data
            };
            return true;
        }

        return false;
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
}
