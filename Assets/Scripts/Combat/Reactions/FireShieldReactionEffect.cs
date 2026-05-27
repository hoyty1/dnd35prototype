// ============================================================================
// FireShieldReactionEffect.cs — Fire Shield retribution as an IMeleeReactionEffect
//
// Implements the Fire Shield reactive damage (1d6 + CL, max +15) that triggers
// when the shielded character is hit by a melee attack. PHB p.230.
//
// This replaces all hardcoded ResolveFireShieldRetribution() calls throughout
// the codebase. The effect registers itself with MeleeReactionService when
// Fire Shield is cast and unregisters when it expires or is dispelled.
//
// The original ResolveFireShieldRetribution() method on GameManager is kept
// as a thin redirect for backward compatibility but all attack resolution
// code now uses MeleeReactionService.TriggerReactions() instead.
// ============================================================================
using UnityEngine;

/// <summary>
/// Fire Shield retribution effect. When the shielded character is struck by
/// a melee attack, the attacker takes 1d6 + caster level (max +15) fire
/// or cold damage (no save). PHB p.230.
/// </summary>
public class FireShieldReactionEffect : IMeleeReactionEffect
{
    private readonly CharacterController _defender;

    public string EffectName
    {
        get
        {
            if (_defender == null || _defender.Stats == null) return "Fire Shield";
            return _defender.Stats.FireShieldIsWarm ? "Fire Shield (Warm)" : "Fire Shield (Chill)";
        }
    }

    public FireShieldReactionEffect(CharacterController defender)
    {
        _defender = defender;
    }

    public bool IsActiveOn(CharacterController character)
    {
        return character != null
            && character == _defender
            && _defender != null
            && _defender.Stats != null
            && _defender.Stats.FireShieldActive;
    }

    public void OnMeleeAttackHit(CharacterController attacker, CharacterController defender, CombatResult attackResult)
    {
        // Double-check: this effect is for our specific defender
        if (defender != _defender) return;

        // Validate state
        if (defender == null || defender.Stats == null || !defender.Stats.FireShieldActive)
        {
            Debug.Log($"[FireShield] BAIL: defender null or FireShieldActive=false | active={defender?.Stats?.FireShieldActive}");
            return;
        }
        if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead)
        {
            Debug.Log($"[FireShield] BAIL: attacker null or dead | dead={attacker?.Stats?.IsDead}");
            return;
        }

        // Calculate retribution damage: 1d6 + CL (max +15)
        int casterLevel = defender.Stats.FireShieldCasterLevel;
        int clBonus = Mathf.Min(casterLevel, 15);
        int damage = DiceRoller.D6() + clBonus;

        bool isWarm = defender.Stats.FireShieldIsWarm;
        string dmgType = isWarm ? "fire" : "cold";

        Debug.Log($"[FireShield] Retribution damage={damage} ({dmgType}) | CL={casterLevel} | isWarm={isWarm} | attacker HP before={attacker.Stats.CurrentHP}");

        attacker.Stats.TakeDamage(damage);

        // Show combat log
        var combatUI = GameManager.Instance?.CombatUI;
        combatUI?.ShowCombatLog(CombatLogHelper.Info("", $"  \ud83d\udd25 Fire Shield retribution! {attacker.Stats.CharacterName} takes {damage} {dmgType} damage (no save)!"));

        if (attacker.Stats.IsDead)
        {
            attacker.OnDeath();
            GameManager.Instance?.Combat_HandleSummonDeathCleanup(attacker);
            combatUI?.ShowCombatLog(CombatLogHelper.Info("", $"  \ud83d\udc80 {attacker.Stats.CharacterName} is slain by Fire Shield retribution!"));
        }
    }
}
