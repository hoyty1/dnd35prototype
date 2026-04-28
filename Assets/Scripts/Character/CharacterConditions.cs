using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Encapsulates character-side condition queries and operations used by <see cref="CharacterController"/>.
/// Integrates with <see cref="ConditionService"/> when available and falls back to direct condition manager access.
/// </summary>
public class CharacterConditions : MonoBehaviour
{
    private CharacterController _character;
    private ConditionService _conditionService;

    public void Initialize(CharacterController character, ConditionService conditionService)
    {
        _character = character;
        _conditionService = conditionService;
    }

    public void SetConditionService(ConditionService conditionService)
    {
        _conditionService = conditionService;
    }

    private ConditionService ResolveConditionService()
    {
        if (_conditionService != null)
            return _conditionService;

        if (GameManager.Instance == null)
            return null;

        _conditionService = GameManager.Instance.GetComponent<ConditionService>();
        return _conditionService;
    }

    public List<StatusEffect> GetActiveConditions()
    {
        if (_character == null)
            return new List<StatusEffect>();

        return _character.GetActiveConditionsDirect();
    }

    public int GetActiveConditionsCount()
    {
        return GetActiveConditions().Count;
    }

    public bool HasCondition(CombatConditionType type)
    {
        if (_character == null)
            return false;

        ConditionService service = ResolveConditionService();
        return service != null
            ? service.HasCondition(_character, type)
            : _character.HasConditionDirect(type);
    }

    public void ApplyCondition(CombatConditionType type, int rounds, string sourceName)
    {
        if (_character == null)
            return;

        ConditionService service = ResolveConditionService();
        if (service != null)
        {
            service.ApplyCondition(_character, type, rounds, source: null, sourceNameOverride: sourceName);
            return;
        }

        _character.ApplyConditionDirect(type, rounds, sourceName);
    }

    public bool RemoveCondition(CombatConditionType type)
    {
        if (_character == null)
            return false;

        ConditionService service = ResolveConditionService();
        return service != null
            ? service.RemoveCondition(_character, type)
            : _character.RemoveConditionDirect(type);
    }

    public List<StatusEffect> TickConditions()
    {
        if (_character == null)
            return new List<StatusEffect>();

        return _character.TickConditionsDirect();
    }

    public int ClearAllConditions()
    {
        if (_character == null)
            return 0;

        ConditionService service = ResolveConditionService();
        if (service != null)
            return service.RemoveAllConditions(_character);

        List<StatusEffect> active = _character.GetActiveConditionsDirect();
        int removed = 0;
        for (int i = 0; i < active.Count; i++)
        {
            if (_character.RemoveConditionDirect(active[i].Type))
                removed++;
        }

        return removed;
    }

    public bool IsProne => HasCondition(CombatConditionType.Prone);
    public bool IsGrappled => HasCondition(CombatConditionType.Grappled);
    public bool IsPinned => HasCondition(CombatConditionType.Pinned);
    public bool IsStunned => HasCondition(CombatConditionType.Stunned);
    public bool IsInvisible => HasCondition(CombatConditionType.Invisible);
    public bool IsTurned => HasCondition(CombatConditionType.Turned);
    public bool IsDazed => HasCondition(CombatConditionType.Dazed);
    public bool IsDazzled => HasCondition(CombatConditionType.Dazzled);
    public bool IsFeinted => HasCondition(CombatConditionType.Feinted);
    public bool IsFlatFooted => HasCondition(CombatConditionType.FlatFooted);

    // Pinning is represented by grapple link state in CharacterController (not a combat condition enum value).
    public bool IsPinning => _character != null && _character.IsPinningOpponent();

    public bool IsDexDenied => IsFlatFooted || IsFeinted || IsProne || IsStunned || IsPinned;

    public int GetConditionACModifier()
    {
        int modifier = 0;

        if (IsProne)
            modifier -= 4;

        if (IsStunned)
            modifier -= 2;

        if (IsInvisible)
            modifier += 2;

        return modifier;
    }

    public int GetConditionAttackModifier()
    {
        int modifier = 0;

        if (IsProne)
            modifier -= 4;

        if (IsStunned)
            modifier -= 999;

        if (IsInvisible)
            modifier += 2;

        if (IsDazzled)
            modifier -= 1;

        return modifier;
    }

    public bool CanTakeActions()
    {
        List<StatusEffect> active = GetActiveConditions();
        for (int i = 0; i < active.Count; i++)
        {
            ConditionDefinition def = ConditionRules.GetDefinition(active[i].Type);
            if (def.PreventsStandardActions && def.PreventsFullRoundActions)
                return false;
        }

        return true;
    }

    public bool CanMove()
    {
        if (_character != null && _character.Stats != null && _character.Stats.MovementBlockedByCondition)
            return false;

        List<StatusEffect> active = GetActiveConditions();
        for (int i = 0; i < active.Count; i++)
        {
            ConditionDefinition def = ConditionRules.GetDefinition(active[i].Type);
            if (def.PreventsMovement || def.MovementMultiplier <= 0f)
                return false;
        }

        return true;
    }

    public bool CanAttack()
    {
        List<StatusEffect> active = GetActiveConditions();
        for (int i = 0; i < active.Count; i++)
        {
            ConditionDefinition def = ConditionRules.GetDefinition(active[i].Type);
            if (def.PreventsStandardActions || def.PreventsFullRoundActions)
                return false;
        }

        return true;
    }

    public bool CanCastSpells()
    {
        List<StatusEffect> active = GetActiveConditions();
        for (int i = 0; i < active.Count; i++)
        {
            ConditionDefinition def = ConditionRules.GetDefinition(active[i].Type);
            if (def.PreventsSpellcasting || def.PreventsStandardActions)
                return false;
        }

        return true;
    }

    public bool IsHelplessLike()
    {
        List<StatusEffect> active = GetActiveConditions();
        for (int i = 0; i < active.Count; i++)
        {
            if (ConditionRules.IsHelplessLike(active[i].Type))
                return true;
        }

        return false;
    }

    public string GetConditionSummary()
    {
        List<StatusEffect> active = GetActiveConditions();
        if (active.Count == 0)
            return string.Empty;

        List<string> labels = new List<string>(active.Count + 1);
        for (int i = 0; i < active.Count; i++)
        {
            ConditionDefinition def = ConditionRules.GetDefinition(active[i].Type);
            labels.Add($"[{def.DisplayName}]");
        }

        if (IsPinning)
            labels.Add("[Pinning]");

        return string.Join(" ", labels);
    }
}
