using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// Oathbow (SRD / DMG p.226)
//
// +2 composite longbow (+2 Str). Once per day, wielder may designate a
// "sworn enemy" by speaking an oath ("Swift death to you who have wronged me!").
//
// Against sworn enemy:
//   - Enhancement bonus becomes +5 (extra +3 on attacks)
//   - +2d6 bonus damage per hit
//
// Against all others while oath is active:
//   - Functions as masterwork only (no magical enhancement)
//   - -1 penalty on all attack rolls
//
// The oath persists until the sworn enemy is slain (or 7 days pass).
// Only one sworn enemy at a time. New oath cannot be spoken while one is active.
// ============================================================================

/// <summary>
/// Oathbow specific item behavior.
/// Tracks a sworn enemy for enhanced attacks with penalties against all others.
/// </summary>
public class OathbowBehavior : SpecificItemBehavior
{
    private const int SwornEnemyEnhancement = 5;  // Total enhancement vs sworn enemy
    private const int BaseEnhancement = 2;        // Normal +2
    private const int NonSwornPenalty = -1;        // -1 vs all others while oath active
    private const int SwornEnemyBonusDamageDice = 2; // 2d6

    // Sworn enemy tracking (pure C# — no MonoBehaviour needed)
    private CharacterController _swornEnemy;
    private bool _hasSwornEnemy;
    private bool _oathUsedToday;

    public override string DisplayName => "Oathbow";

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _swornEnemy = null;
        _hasSwornEnemy = false;
        _oathUsedToday = false;
    }

    public override void OnEquip(CharacterController character)
    {
        base.OnEquip(character);
        Log($"Equipped by {character.Stats?.CharacterName}");

        if (_hasSwornEnemy && _swornEnemy != null && !_swornEnemy.IsDead)
        {
            GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🏹", $"Oathbow reminds {character.Stats?.CharacterName}: sworn enemy {_swornEnemy.Stats?.CharacterName} still lives!"));
        }
    }

    // ========================================================================
    //  ACTIVATED: Designate sworn enemy (1/day)
    // ========================================================================

    public override bool CanActivate()
    {
        // Can designate if: equipped, no active oath, and haven't used oath today
        return IsEquipped && !_hasSwornEnemy && !_oathUsedToday;
    }

    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (_hasSwornEnemy)
        {
            logNotes?.Add("🏹 Oathbow: Already have a sworn enemy!");
            return false;
        }

        if (_oathUsedToday)
        {
            logNotes?.Add("🏹 Oathbow: Oath already spoken today.");
            return false;
        }

        if (target == null || target.Stats == null)
        {
            logNotes?.Add("🏹 Oathbow: No valid target to swear oath against.");
            return false;
        }

        if (target.IsDead)
        {
            logNotes?.Add("🏹 Oathbow: Cannot swear oath against a dead creature.");
            return false;
        }

        // Designate sworn enemy
        _swornEnemy = target;
        _hasSwornEnemy = true;
        _oathUsedToday = true;

        string wielderName = Wielder?.Stats?.CharacterName ?? "Wielder";
        string targetName = target.Stats.CharacterName;

        logNotes?.Add($"🏹 <color=#FF0000>\"Swift death to you who have wronged me!\"</color>");
        logNotes?.Add($"🎯 <color=#FF4500>{wielderName} swears an oath against {targetName}!</color>");
        logNotes?.Add($"⚔️ Oathbow: +{SwornEnemyEnhancement} enhancement, +{SwornEnemyBonusDamageDice}d6 damage vs {targetName}");
        logNotes?.Add($"⚠️ Oathbow: {NonSwornPenalty} penalty and no magic bonus vs all others until oath fulfilled");
        Log($"Sworn enemy designated: {targetName}");

        GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Death("🏹", $"{wielderName} speaks the Oathbow's oath against {targetName}!"));

        return true;
    }

    public override string GetActivateDescription()
    {
        if (_hasSwornEnemy && _swornEnemy != null)
            return $"Oath active against {_swornEnemy.Stats?.CharacterName ?? "sworn enemy"}. Cannot designate new enemy until oath fulfilled.";
        if (_oathUsedToday)
            return "Oath already spoken today. Resets on long rest.";
        return "Speak oath to designate sworn enemy: +5 enhancement, +2d6 damage vs target. -1 and no magic bonus vs all others until enemy slain.";
    }

    public override string GetUsesDisplay()
    {
        if (_hasSwornEnemy && _swornEnemy != null)
            return $"Oath: {_swornEnemy.Stats?.CharacterName ?? "enemy"}";
        if (_oathUsedToday)
            return "Oath used today";
        return "Oath available";
    }

    // ========================================================================
    //  ATTACK ROLL: +5 vs sworn enemy, -1 and no enhancement vs others
    // ========================================================================

    public override void OnPreAttackRoll(CharacterController target, ref int attackBonus, List<string> logNotes)
    {
        if (!_hasSwornEnemy || _swornEnemy == null) return;

        // Check if sworn enemy died since we last checked
        if (_swornEnemy.IsDead)
        {
            ClearSwornEnemy(logNotes);
            return;
        }

        if (target == _swornEnemy)
        {
            // Extra +3 on top of base +2 = +5 total
            int extraAttack = SwornEnemyEnhancement - BaseEnhancement;
            attackBonus += extraAttack;
            logNotes?.Add($"🎯 Oathbow vs sworn enemy: +{extraAttack} attack (+{SwornEnemyEnhancement} total)");
            Log($"Sworn enemy attack bonus: +{extraAttack}");
        }
        else
        {
            // -1 penalty AND lose the +2 enhancement (effectively -3 total)
            // The base +2 enhancement is already applied by the item system,
            // so we remove it (-2) and apply the -1 penalty
            attackBonus += (-BaseEnhancement + NonSwornPenalty); // -3 total
            logNotes?.Add($"⚠️ Oathbow vs non-sworn: {NonSwornPenalty} penalty, no magic bonus");
            Log($"Non-sworn penalty: {-BaseEnhancement + NonSwornPenalty}");
        }
    }

    // ========================================================================
    //  DAMAGE ROLL: +2d6 vs sworn enemy, -1 vs others
    // ========================================================================

    public override void OnDamageRoll(CharacterController target, ref int damage, bool isCrit, List<string> logNotes)
    {
        if (!_hasSwornEnemy || _swornEnemy == null) return;

        if (target == _swornEnemy)
        {
            // +2d6 bonus damage
            int bonusDamage = DiceService.RollMultiple(SwornEnemyBonusDamageDice, 6);
            // Extra enhancement damage: +3 (from +5 total minus base +2)
            int extraEnhDamage = SwornEnemyEnhancement - BaseEnhancement;
            damage += bonusDamage + extraEnhDamage;
            logNotes?.Add($"🎯 <color=#FF4500>SWORN ENEMY!</color> +{bonusDamage} ({SwornEnemyBonusDamageDice}d6) +{extraEnhDamage} enhancement = +{bonusDamage + extraEnhDamage} total bonus!");
            Log($"Sworn enemy damage: +{bonusDamage}(dice) +{extraEnhDamage}(enh) = {bonusDamage + extraEnhDamage}");
        }
        else
        {
            // Lose enhancement damage (-2) and apply -1 penalty
            damage += (-BaseEnhancement + NonSwornPenalty);
            if (damage < 1) damage = 1; // Minimum 1 damage
            logNotes?.Add($"⚠️ Oathbow vs non-sworn: reduced damage");
        }
    }

    // ========================================================================
    //  ON KILL: Clear oath if sworn enemy slain
    // ========================================================================

    public override void OnKill(CharacterController target, List<string> logNotes)
    {
        if (_hasSwornEnemy && target == _swornEnemy)
        {
            logNotes?.Add($"🏹 <color=#FFD700>Sworn enemy {target.Stats?.CharacterName} slain! Oath fulfilled!</color>");
            Log($"Sworn enemy slain: {target.Stats?.CharacterName}");

            GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Special("🏹", $"{Wielder?.Stats?.CharacterName}'s oath is fulfilled! {target.Stats?.CharacterName} is slain!"));

            _swornEnemy = null;
            _hasSwornEnemy = false;
            // Note: _oathUsedToday stays true — can't swear a new oath until next day
        }
    }

    // ========================================================================
    //  LONG REST: Reset daily oath use
    // ========================================================================

    public override void OnLongRest()
    {
        base.OnLongRest();
        _oathUsedToday = false;

        // Check if sworn enemy is still alive
        if (_hasSwornEnemy && _swornEnemy != null && _swornEnemy.IsDead)
        {
            _swornEnemy = null;
            _hasSwornEnemy = false;
            Log("Sworn enemy died — oath cleared on rest");
        }

        Log("Oath use refreshed");
    }

    // ========================================================================
    //  HELPERS
    // ========================================================================

    private void ClearSwornEnemy(List<string> logNotes)
    {
        string enemyName = _swornEnemy?.Stats?.CharacterName ?? "sworn enemy";
        _swornEnemy = null;
        _hasSwornEnemy = false;
        logNotes?.Add($"🎯 Oath against {enemyName} is fulfilled (enemy dead)");
        Log($"Sworn enemy cleared: {enemyName}");
    }

    /// <summary>
    /// Whether the Oathbow currently has an active sworn enemy.
    /// </summary>
    public bool HasSwornEnemy => _hasSwornEnemy && _swornEnemy != null && !_swornEnemy.IsDead;

    /// <summary>
    /// The current sworn enemy, or null if none.
    /// </summary>
    public CharacterController SwornEnemy => _hasSwornEnemy ? _swornEnemy : null;

    public string GetTooltipText()
    {
        var lines = new List<string>();
        lines.Add("<b>Oathbow</b> (+2 composite longbow)");
        lines.Add("1/day: Designate sworn enemy");
        lines.Add($"Vs sworn enemy: +{SwornEnemyEnhancement} enhancement, +{SwornEnemyBonusDamageDice}d6 damage");
        lines.Add($"Vs all others: {NonSwornPenalty} penalty, no magic bonus");
        if (_hasSwornEnemy && _swornEnemy != null)
            lines.Add($"<color=#FF0000>🎯 Sworn enemy: {_swornEnemy.Stats?.CharacterName}</color>");
        else if (_oathUsedToday)
            lines.Add("<color=#888888>Oath used today</color>");
        return string.Join("\n", lines);
    }
}
