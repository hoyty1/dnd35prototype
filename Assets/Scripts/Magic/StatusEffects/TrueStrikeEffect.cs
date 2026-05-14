using UnityEngine;

/// <summary>
/// D&D 3.5e True Strike (PHB p.296):
/// - +20 insight bonus on the caster's next single attack roll
/// - Ignore concealment miss chance on that same attack
/// - Expires at the end of the caster's next turn if unused
/// </summary>
public class TrueStrikeEffect : MonoBehaviour
{
    public const int INSIGHT_BONUS = 20;

    private CharacterController _caster;
    private GameManager _gameManager;
    private int _castRound = -1;
    private bool _consumed;

    public void Initialize(CharacterController caster, GameManager gameManager, int castRound)
    {
        _caster = caster;
        _gameManager = gameManager;
        _castRound = castRound;
        _consumed = false;

        string casterName = _caster != null && _caster.Stats != null ? _caster.Stats.CharacterName : "Unknown";
        Debug.Log($"[TrueStrike] {casterName} gains True Strike (cast round {_castRound}).");
    }

    public bool IsActive()
    {
        if (_consumed) return false;
        if (_caster == null || _caster.Stats == null || _caster.Stats.IsDead) return false;
        return true;
    }

    public int GetAttackBonus()
    {
        return IsActive() ? INSIGHT_BONUS : 0;
    }

    public bool ShouldIgnoreConcealment()
    {
        return IsActive();
    }

    /// <summary>
    /// Consume on the first attack roll attempt.
    /// </summary>
    public void ConsumeOnAttackRoll()
    {
        if (!IsActive())
            return;

        _consumed = true;

        string casterName = _caster != null && _caster.Stats != null ? _caster.Stats.CharacterName : "Unknown";
        _gameManager?.CombatUI?.ShowCombatLog($"<color=#FFD27F>🎯 {casterName}'s True Strike is consumed (+20 insight, concealment ignored).</color>");
        Debug.Log($"[TrueStrike] Consumed on attack roll by {casterName}.");

        Destroy(this);
    }

    /// <summary>
    /// Called at turn end. True Strike expires at end of caster's next turn after casting.
    /// </summary>
    public void CheckExpirationAtTurnEnd(CharacterController endingCharacter, int currentRound)
    {
        if (!IsActive())
            return;

        if (endingCharacter != _caster)
            return;

        if (currentRound >= _castRound + 1)
        {
            _consumed = true;
            string casterName = _caster != null && _caster.Stats != null ? _caster.Stats.CharacterName : "Unknown";
            _gameManager?.CombatUI?.ShowCombatLog($"<color=#FFAA66>⏱ {casterName}'s True Strike expires unused.</color>");
            Debug.Log($"[TrueStrike] Expired unused for {casterName} at end of round {currentRound}.");
            Destroy(this);
        }
    }
}
