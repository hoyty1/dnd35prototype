using UnityEngine;

/// <summary>
/// Encapsulates target-selection oriented UI helpers for CombatUI.
/// Currently owns flanking-indicator text formatting and target tracking state.
/// </summary>
public class TargetSelectionPanel : MonoBehaviour
{
    private CombatUI _combatUI;
    private CharacterController _currentTarget;

    public void Initialize(CombatUI combatUI)
    {
        _combatUI = combatUI;
    }

    public void SetTarget(CharacterController target)
    {
        _currentTarget = target;
    }

    public CharacterController GetCurrentTarget()
    {
        return _currentTarget;
    }

    public void ClearTarget()
    {
        _currentTarget = null;
    }

    /// <summary>
    /// Build a standardized short flanking indicator for turn/targeting text.
    /// </summary>
    public string BuildFlankingIndicator(bool isFlanking, CharacterController flankingAlly)
    {
        if (!isFlanking)
            return string.Empty;

        string allyName = flankingAlly != null && flankingAlly.Stats != null
            ? flankingAlly.Stats.CharacterName
            : string.Empty;

        return string.IsNullOrEmpty(allyName)
            ? " (FLANKING +2)"
            : $" (FLANKING +2 with {allyName})";
    }
}
