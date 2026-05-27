// ============================================================================
// GameManager_Spells_V.cs — Spell resolution methods starting with "V".
//
// Part of the GameManager partial class.
// D&D 3.5e PHB rules.
// ============================================================================
using DND35e.Identifiers;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using System.Text;
using System;
using UnityEngine;

public partial class GameManager
{
    // ================================================================
    //  VAMPIRIC TOUCH — PHB p.281
    //  Melee touch attack. Deals 1d6 negative energy damage per 2 CL
    //  (max 10d6). Caster gains temp HP equal to damage dealt
    //  (capped at caster's max HP), lasting 1 hour. SR: Yes.
    // ================================================================

    private static bool IsVampiricTouchSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.VAMPIRIC_TOUCH, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Vampiric Touch. The melee touch must hit. Rolls
    /// 1d6/2CL (max 10d6) negative energy damage. Caster gains temporary
    /// HP equal to damage dealt, capped at caster's max HP, for 1 hour.
    /// Called from the touch/ray spell pipeline in PC and NPC casts.
    /// </summary>
    private bool TryResolveVampiricTouchSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (!IsVampiricTouchSpell(spell) || target == null || target.Stats == null)
            return false;

        if (result == null)
            return true;

        // Melee touch missed → no effect
        if (result.RequiredAttackRoll && !result.AttackHit)
        {
            CombatUI?.ShowCombatLog($"❌ Vampiric Touch misses {target.Stats.CharacterName}.");
            return true;
        }

        if (caster == null || caster.Stats == null)
            return true;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);

        // SR is already checked by the base spell pipeline (SpellCaster.cs).
        // If this method is called with result.Success = true, SR has already passed.

        // Roll damage: 1d6 per 2 caster levels, max 10d6
        int diceCount = Mathf.Clamp(casterLevel / 2, 1, 10);
        int damage = 0;
        for (int i = 0; i < diceCount; i++)
            damage += DiceRoller.D6();

        int hpBefore = target.Stats.CurrentHP;
        target.Stats.TakeDamage(damage);
        int hpAfter = target.Stats.CurrentHP;

        result.DamageDealt = damage;
        result.DamageRolled = damage;
        result.DamageType = "negative";
        result.TargetHPBefore = hpBefore;
        result.TargetHPAfter = hpAfter;
        result.TargetKilled = target.Stats.IsDead;

        CombatUI?.ShowCombatLog($"<color=#9933CC>🖤 {caster.Stats.CharacterName}'s Vampiric Touch deals {damage} negative energy damage to {target.Stats.CharacterName} ({hpBefore} → {hpAfter} HP).</color>");

        // Concentration on damage is checked downstream in the touch pipeline (uses result.DamageDealt).

        // Caster gains temp HP equal to damage dealt, capped at caster's max HP
        int casterMaxHP = Mathf.Max(1, caster.Stats.MaxHP);
        int tempHP = Mathf.Min(damage, casterMaxHP);

        if (tempHP > 0)
        {
            // Vampiric Touch temp HP lasts 1 hour = 600 rounds
            int durationRounds = 600;
            FalseLifeEffectData vampiricTempHP = FalseLifeEffectData.CreateGenericTempHP(
                SpellNames.VAMPIRIC_TOUCH,
                "Vampiric Touch",
                tempHP,
                casterLevel,
                durationRounds,
                caster);

            caster.ApplyFalseLifeEffect(vampiricTempHP);

            CombatUI?.ShowCombatLog($"<color=#FF6666>🩸 {caster.Stats.CharacterName} drains {tempHP} temporary HP from {target.Stats.CharacterName} (lasts 1 hour).</color>");
        }

        if (target.Stats.IsDead)
        {
            CombatUI?.ShowCombatLog($"<color=#660033>💀 {target.Stats.CharacterName} is slain by Vampiric Touch!</color>");
            // OnDeath/HandleSummonDeathCleanup are called downstream in the touch pipeline (uses result.TargetKilled).
        }

        Debug.Log($"[GameManager] Vampiric Touch: {caster.Stats.CharacterName} dealt {damage} negative damage to {target.Stats.CharacterName}, gained {tempHP} temp HP (CL {casterLevel}, {diceCount}d6)");
        return true;
    }

}
