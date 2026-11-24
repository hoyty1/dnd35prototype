using UnityEngine;

namespace DND35.AI.Valuation
{
    /// <summary>
    /// The AI's values of the activated domain powers it chooses (batch 14 step 14, decision D7, design choice C14;
    /// docs/designs/personalities_batch14.md "Leftover hooks", ChooseDomainPower), in expected hit points. Estimates for
    /// choosing, never a rule: the domain-power core (GameManager.ActionCore_DomainPower) rolls the real smite and touch.
    /// Pure numbers: the caller reads the creatures (AIService.BuildDomainPowerContext).
    /// <list type="bullet">
    /// <item>The smite (PHB p.186): +4 on one melee attack and the cleric level on its damage, so its gain on that swing is
    /// HitChance(mod + 4) x (D + level) - HitChance(mod) x D.</item>
    /// <item>The death touch (PHB p.186): a melee touch attack, then 1d6 per cleric level; the creature dies when the total
    /// is at least its current hit points. Its value is P(touch) x P(the total reaches them) x those hit points: the hit
    /// points it takes away, in the attack's currency.</item>
    /// </list>
    /// </summary>
    public static class DomainPowerValue
    {
        /// <summary>The smite's bonus on its attack roll (PHB p.186).</summary>
        public const int SmiteAttackBonus = 4;

        /// <summary>The largest dice count the kill chance adds up exactly (a cleric level well above the level 1-8 scope).</summary>
        public const int MaxDice = 40;

        /// <summary>
        /// The chance that <paramref name="dice"/>d6 total at least <paramref name="currentHp"/>, added up exactly: 1 at or
        /// below the dice count, 0 above six times it (or with no dice).
        /// </summary>
        public static float KillChance(int dice, int currentHp)
        {
            if (dice <= 0)
                return 0f;
            dice = Mathf.Min(dice, MaxDice);
            if (currentHp <= dice)
                return 1f;
            if (currentHp > 6 * dice)
                return 0f;

            // The distribution of the sum, one die at a time.
            var ways = new double[6 * dice + 1];
            ways[0] = 1d;
            for (int d = 1; d <= dice; d++)
            {
                var next = new double[6 * dice + 1];
                for (int s = 0; s <= 6 * (d - 1); s++)
                {
                    if (ways[s] == 0d)
                        continue;
                    for (int face = 1; face <= 6; face++)
                        next[s + face] += ways[s] / 6d;
                }
                ways = next;
            }
            double atLeast = 0d;
            for (int s = currentHp; s <= 6 * dice; s++)
                atLeast += ways[s];
            return Mathf.Clamp01((float)atLeast);
        }

        /// <summary>The death touch's value: <paramref name="touchChance"/> x KillChance x <paramref name="currentHp"/> (0 for a creature at 0 or fewer hit points).</summary>
        public static float DeathTouch(float touchChance, int dice, int currentHp)
            => currentHp <= 0 ? 0f : Mathf.Clamp01(touchChance) * KillChance(dice, currentHp) * currentHp;

        /// <summary>The smite's gain on one swing of <paramref name="attackModifier"/> against <paramref name="targetAC"/> whose hit deals <paramref name="hitDamage"/>, by a cleric of <paramref name="clericLevel"/>.</summary>
        public static float SmiteGain(int attackModifier, int targetAC, float hitDamage, int clericLevel)
        {
            float d = Mathf.Max(0f, hitDamage);
            return AttackValue.HitChance(attackModifier + SmiteAttackBonus, targetAC) * (d + Mathf.Max(1, clericLevel))
                   - AttackValue.HitChance(attackModifier, targetAC) * d;
        }
    }
}
