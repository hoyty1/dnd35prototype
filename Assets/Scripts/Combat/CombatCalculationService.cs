using UnityEngine;

/// <summary>
/// Static utility that centralises common combat math formulas that are
/// duplicated across attack resolution, spell combat, domain powers,
/// mounted combat, and special-ability code.
///
/// Every method is a pure function — no side-effects, no state.
///
/// Phase 4J extraction — proof-of-concept covering ~40 call sites.
/// </summary>
public static class CombatCalculationService
{
    // ─────────────────────────────────────────────
    //  D20 Hit Check  (natural 20 = auto-hit, natural 1 = auto-miss)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Standard D&amp;D 3.5e d20 attack resolution.
    /// Natural 20 always hits, natural 1 always misses, otherwise total &gt;= AC.
    /// </summary>
    public static bool IsHit(int naturalRoll, int total, int targetAC)
    {
        if (naturalRoll == 20) return true;
        if (naturalRoll == 1)  return false;
        return total >= targetAC;
    }

    // ─────────────────────────────────────────────
    //  Spell Save DC  (10 + spell level + ability mod)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Standard spell save DC = 10 + spell level + casting ability modifier.
    /// D&amp;D 3.5e PHB p.171.
    /// </summary>
    public static int SpellSaveDC(int spellLevel, int castingAbilityMod)
        => 10 + spellLevel + castingAbilityMod;

    // ─────────────────────────────────────────────
    //  Touch Attack Bonus  (BAB + ability + size)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Melee touch attack bonus = BAB + STR mod + size modifier.
    /// </summary>
    public static int MeleeTouchAttackBonus(CharacterStats stats)
    {
        if (stats == null) return 0;
        return stats.BaseAttackBonus + stats.STRMod + stats.SizeModifier;
    }

    /// <summary>
    /// Ranged touch attack bonus = BAB + DEX mod + size modifier.
    /// </summary>
    public static int RangedTouchAttackBonus(CharacterStats stats)
    {
        if (stats == null) return 0;
        return stats.BaseAttackBonus + stats.DEXMod + stats.SizeModifier;
    }

    /// <summary>
    /// Touch attack bonus — picks STR (melee) or DEX (ranged) automatically.
    /// </summary>
    public static int TouchAttackBonus(CharacterStats stats, bool isRanged)
        => isRanged ? RangedTouchAttackBonus(stats) : MeleeTouchAttackBonus(stats);

    // ─────────────────────────────────────────────
    //  Touch AC  (10 + DEX + size + deflection + dodge + misc)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Quick inline touch AC = 10 + DEX mod + size modifier.
    /// For the full touch AC with all bonuses use <c>stats.TouchArmorClass</c>.
    /// This covers the common inline pattern found in combat maneuver code.
    /// </summary>
    public static int SimpleTouchAC(CharacterStats stats)
    {
        if (stats == null) return 10;
        return 10 + stats.DEXMod + stats.SizeModifier;
    }

    // ─────────────────────────────────────────────
    //  Situational AC adjustments
    // ─────────────────────────────────────────────

    /// <summary>
    /// D&amp;D 3.5e prone AC adjustment: +4 vs ranged, -4 vs melee.
    /// </summary>
    public static int ProneACModifier(bool isRangedAttack) => isRangedAttack ? 4 : -4;

    /// <summary>
    /// Fighting Defensively dodge bonus to AC (+2).
    /// D&amp;D 3.5e PHB p.140.
    /// </summary>
    public const int FightingDefensivelyACBonus = 2;

    /// <summary>
    /// Fighting Defensively attack penalty (-4).
    /// D&amp;D 3.5e PHB p.140.
    /// </summary>
    public const int FightingDefensivelyAttackPenalty = -4;

    /// <summary>
    /// D&amp;D 3.5e Pinned AC penalty against non-grapple attackers.
    /// </summary>
    public const int PinnedACPenalty = -4;

    // ─────────────────────────────────────────────
    //  STR damage multiplier
    // ─────────────────────────────────────────────

    /// <summary>
    /// Apply the standard STR-to-damage multiplier.
    /// D&amp;D 3.5e PHB p.113: 1× normal, 1.5× two-handed, 0.5× off-hand.
    /// Negative STR mod always applied at 1×.
    /// </summary>
    public static int ApplyStrDamageMultiplier(int strMod, float multiplier)
    {
        if (strMod <= 0) return strMod; // negative STR always 1×
        return Mathf.FloorToInt(strMod * multiplier);
    }

    /// <summary>
    /// Two-handed STR damage bonus (1.5×).
    /// </summary>
    public static int TwoHandedStrDamage(int strMod) => ApplyStrDamageMultiplier(strMod, 1.5f);

    /// <summary>
    /// Off-hand STR damage bonus (0.5×).
    /// </summary>
    public static int OffHandStrDamage(int strMod) => ApplyStrDamageMultiplier(strMod, 0.5f);

    // ─────────────────────────────────────────────
    //  Minimum-1 damage floor
    // ─────────────────────────────────────────────

    /// <summary>
    /// D&amp;D 3.5e: melee/ranged damage is always at least 1 (nonlethal 0 minimum).
    /// </summary>
    public static int ClampMinimumDamage(int rawDamage, bool isNonlethal = false)
        => isNonlethal ? Mathf.Max(0, rawDamage) : Mathf.Max(1, rawDamage);

    // ─────────────────────────────────────────────
    //  Critical hit helpers
    // ─────────────────────────────────────────────

    /// <summary>
    /// Check whether a natural roll threatens a critical hit.
    /// D&amp;D 3.5e PHB p.140: natural roll ≥ threat minimum.
    /// </summary>
    public static bool IsCriticalThreat(int naturalRoll, int critThreatMin)
        => naturalRoll >= critThreatMin;

    /// <summary>
    /// Calculate the doubled threat range (Improved Critical / Keen).
    /// D&amp;D 3.5e PHB p.95: doubles the weapon's threat range.
    /// E.g., 19-20 becomes 17-20, 20 becomes 19-20.
    /// </summary>
    public static int DoubledThreatMin(int baseThreatMin)
        => 21 - (21 - baseThreatMin) * 2;

    /// <summary>
    /// Calculate critical bonus damage dice count.
    /// D&amp;D 3.5e PHB p.140: ×2 = 1 extra die, ×3 = 2 extra dice, ×4 = 3 extra dice.
    /// </summary>
    public static int CritBonusDice(int damageCount, int critMultiplier)
        => damageCount * (critMultiplier - 1);

    // ─────────────────────────────────────────────
    //  Concealment miss chance
    // ─────────────────────────────────────────────

    /// <summary>
    /// Standard concealment miss chance values.
    /// D&amp;D 3.5e PHB p.152.
    /// </summary>
    public const int ConcealmentMissChance = 20;
    public const int TotalConcealmentMissChance = 50;
    public const int BlindedAttackerMissChance = 50;

    /// <summary>
    /// Check if a concealment roll results in a miss.
    /// Miss if percentile roll ≤ miss chance.
    /// </summary>
    public static bool ConcealmentMiss(int percentileRoll, int missChancePercent)
        => missChancePercent > 0 && percentileRoll <= missChancePercent;

    // ─────────────────────────────────────────────
    //  Opposed check resolution
    // ─────────────────────────────────────────────

    /// <summary>
    /// Resolve a standard opposed check. Attacker wins ties.
    /// D&amp;D 3.5e PHB p.65.
    /// </summary>
    public static bool OpposedCheckWins(int attackerTotal, int defenderTotal)
        => attackerTotal >= defenderTotal;
}
