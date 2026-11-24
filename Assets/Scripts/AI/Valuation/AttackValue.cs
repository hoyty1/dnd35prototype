using System.Collections.Generic;
using UnityEngine;

namespace DND35.AI.Valuation
{
    /// <summary>
    /// The AI's estimates of what an attack is worth (batch 14 step 2, docs/designs/personalities_batch14.md, "AI/Valuation"):
    /// the hit chance of a d20 roll against an AC and the average damage of one hit. They are estimates for choosing, never
    /// a rule: the attack code rolls the real attack. Extracted from AIProfile's private HitChance and AverageWeaponDamage
    /// and ThreatSystem's EstimateAverageMeleeDamage, which now delegate here with the same formulas (AttackValueTests pin
    /// them). Batch 22's evaluator reuses them.
    /// </summary>
    public static class AttackValue
    {
        /// <summary>
        /// The chance that d20 + <paramref name="attackModifier"/> hits <paramref name="targetAC"/>: a natural 1 always
        /// misses and a natural 20 always hits (PHB p.134), so it stays within 0.05 and 0.95. Criticals are not counted.
        /// </summary>
        public static float HitChance(int attackModifier, int targetAC)
            => Mathf.Clamp((21 + attackModifier - targetAC) / 20f, 0.05f, 0.95f);

        /// <summary>
        /// The average damage of one hit with <paramref name="weapon"/>: its dice at the wielder's size, the wielder's
        /// Strength share for that hand, and the weapon's flat bonus; at least 1 (PHB p.134). An AI estimate.
        /// </summary>
        public static float AverageWeaponDamage(CharacterController self, ItemData weapon, bool offHand)
        {
            self.GetScaledWeaponDamageDice(weapon, out int count, out int dice);
            float average = Mathf.Max(1, count) * (dice + 1) / 2f + self.Stats.GetWeaponDamageModifier(weapon, offHand) + weapon.BonusDamage;
            return Mathf.Max(1f, average);
        }

        /// <summary>
        /// The average damage of one hit of <paramref name="attacker"/>'s main attack: its main-hand weapon (dice at its
        /// size, DMG Tables 2-2 and 2-3; the Strength modifier for a melee or thrown weapon), else its primary natural
        /// attack (size-scaled, with its own Strength multiple), else its unarmed strike; at least 1. 0 without a creature.
        /// The dice and bonus are <see cref="MainHitDice"/>'s, averaged by <see cref="AverageOfDice"/>.
        /// </summary>
        public static float AverageMeleeDamage(CharacterController attacker)
            => MainHitDice(attacker, out int count, out int sides, out int bonus) ? AverageOfDice(count, sides, bonus) : 0f;

        /// <summary>The average damage of one hit with <paramref name="natural"/> (size-scaled dice and its Strength share); at least 1.</summary>
        public static float AverageNaturalDamage(CharacterStats stats, NaturalAttackDefinition natural)
        {
            NaturalHitDice(stats, natural, out int count, out int sides, out int bonus);
            return AverageOfDice(count, sides, bonus);
        }

        /// <summary>
        /// The dice and flat bonus of one hit of <paramref name="attacker"/>'s main attack, the terms
        /// <see cref="AverageMeleeDamage"/> averages: its main-hand weapon's dice at its size (1d4 when it has none) plus
        /// the weapon's bonus and, for a melee or thrown weapon, the Strength modifier; else its primary natural attack
        /// (<see cref="NaturalHitDice"/>); else its unarmed strike plus the Strength modifier. False without a creature.
        /// An AI estimate: no criticals, no feats (Power Attack, Weapon Specialization) and no 1-1/2 times Strength for a
        /// two-handed weapon (AI-094).
        /// </summary>
        public static bool MainHitDice(CharacterController attacker, out int count, out int sides, out int bonus)
        {
            count = 0;
            sides = 0;
            bonus = 0;
            if (attacker == null || attacker.Stats == null)
                return false;

            ItemData weapon = attacker.GetEquippedMainWeapon();
            if (weapon != null && weapon.IsWeapon)
            {
                attacker.GetScaledWeaponDamageDice(weapon, out count, out sides);
                if (count <= 0 || sides <= 0)
                {
                    count = 1;
                    sides = 4;
                }
                bonus = weapon.BonusDamage;
                if (weapon.WeaponCat == WeaponCategory.Melee || weapon.IsThrown)
                    bonus += attacker.Stats.STRMod;
                return true;
            }

            NaturalAttackDefinition natural = attacker.Stats.GetPrimaryNaturalAttack();
            if (natural != null)
            {
                NaturalHitDice(attacker.Stats, natural, out count, out sides, out bonus);
                return true;
            }

            var unarmed = attacker.GetUnarmedDamage();
            count = unarmed.damageCount;
            sides = unarmed.damageDice;
            bonus = unarmed.bonusDamage + attacker.Stats.STRMod;
            return true;
        }

        /// <summary>The dice (size-scaled) and flat bonus (its Strength share) of one hit with <paramref name="natural"/>.</summary>
        public static void NaturalHitDice(CharacterStats stats, NaturalAttackDefinition natural, out int count, out int sides, out int bonus)
        {
            stats.GetScaledNaturalAttackDamage(natural, out count, out sides);
            bonus = stats.GetNaturalAttackDamageBonus(natural);
        }

        /// <summary>The average of <paramref name="count"/>d<paramref name="sides"/> + <paramref name="bonus"/>, at least 1 (PHB p.134).</summary>
        public static float AverageOfDice(int count, int sides, int bonus)
            => Mathf.Max(1f, count * (sides + 1) * 0.5f + bonus);

        /// <summary>
        /// The expected damage of one hit of <paramref name="count"/>d<paramref name="sides"/> + <paramref name="bonus"/>
        /// (at least 1, PHB p.134) after a flat <paramref name="reduction"/> taken off each hit to a minimum of 0 (damage
        /// reduction and resistance cut each hit, not the average: MM p.307, p.314; AI-091): the mean of
        /// max(0, max(1, roll + bonus) - reduction) over the dice's distribution, so a foe whose larger rolls get past the
        /// reduction still counts them. <see cref="AverageOfDice"/> when <paramref name="reduction"/> is 0 or less. No
        /// criticals. Pure.
        /// </summary>
        public static float ExpectedHitAfterReduction(int count, int sides, int bonus, int reduction)
        {
            if (reduction <= 0)
                return AverageOfDice(count, sides, bonus);
            if (count <= 0 || sides <= 0)
                return Mathf.Max(0f, Mathf.Max(1, bonus) - reduction);
            count = Mathf.Min(count, 40);

            // The distribution of the dice's sum by convolution; index j is the sum minus count.
            double[] ways = { 1.0 };
            for (int d = 0; d < count; d++)
            {
                var next = new double[ways.Length + sides - 1];
                for (int j = 0; j < ways.Length; j++)
                {
                    double share = ways[j] / sides;
                    for (int f = 0; f < sides; f++)
                        next[j + f] += share;
                }
                ways = next;
            }

            double expected = 0.0;
            for (int j = 0; j < ways.Length; j++)
            {
                int damage = Mathf.Max(1, count + j + bonus);
                if (damage > reduction)
                    expected += ways[j] * (damage - reduction);
            }
            return (float)expected;
        }

        /// <summary>
        /// The expected damage of <paramref name="attacker"/>'s full attack against <paramref name="targetAC"/> (an AI
        /// estimate of how dangerous it is for a round; no criticals, riders or feats):
        /// <list type="bullet">
        /// <item>with a weapon in the main hand, its iterative steps (CharacterCombatStats.GetBaseAttackLadder) at the
        /// creature's melee or ranged attack estimate (CharacterStats.GetMeleeAttackBonus, GetRangedAttackBonus), each
        /// hit worth <see cref="AverageMeleeDamage"/>;</item>
        /// <item>otherwise its natural attacks, each repeated by its count, at the melee estimate plus the secondary
        /// attack penalty (MM p.312), each hit worth its own average;</item>
        /// <item>otherwise its unarmed strike on the iterative steps.</item>
        /// </list>
        /// With <paramref name="subject"/>, each hit is taken after the subject's immunity, resistance and damage reduction
        /// against that attack, roll by roll (TargetNumbers.HitAfterMitigation, AI-091); without one, the raw average as before.
        /// </summary>
        public static float ExpectedRoundDamage(CharacterController attacker, int targetAC, CharacterController subject = null)
        {
            if (attacker == null || attacker.Stats == null)
                return 0f;
            CharacterStats st = attacker.Stats;
            int bab = st.BaseAttackBonus;

            ItemData weapon = attacker.GetEquippedMainWeapon();
            if (weapon == null || !weapon.IsWeapon)
            {
                List<NaturalAttackDefinition> naturals = st.GetValidNaturalAttacks();
                if (naturals.Count > 0)
                {
                    int melee = st.GetMeleeAttackBonus();
                    float natTotal = 0f;
                    for (int i = 0; i < naturals.Count; i++)
                    {
                        NaturalAttackDefinition a = naturals[i];
                        float hit = HitChance(melee + st.GetNaturalAttackSequencePenalty(a), targetAC);
                        NaturalHitDice(st, a, out int count, out int sides, out int damageBonus);
                        natTotal += Mathf.Max(1, a.Count) * hit
                            * TargetNumbers.HitAfterMitigation(attacker, subject, count, sides, damageBonus, null, isRangedAttack: false);
                    }
                    return natTotal;
                }
            }

            bool ranged = weapon != null && weapon.IsWeapon && weapon.IsRangedWeapon;
            int bonus = ranged ? st.GetRangedAttackBonus() : st.GetMeleeAttackBonus();
            MainHitDice(attacker, out int hitCount, out int hitSides, out int hitBonus);
            float perHit = TargetNumbers.HitAfterMitigation(attacker, subject, hitCount, hitSides, hitBonus,
                weapon != null && weapon.IsWeapon ? weapon : null, ranged);
            float total = 0f;
            List<int> ladder = CharacterCombatStats.GetBaseAttackLadder(bab);
            for (int i = 0; i < ladder.Count; i++)
                total += HitChance(bonus + (ladder[i] - bab), targetAC) * perHit;
            return total;
        }
    }
}
