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
        /// </summary>
        public static float AverageMeleeDamage(CharacterController attacker)
        {
            if (attacker == null || attacker.Stats == null)
                return 0f;

            ItemData weapon = attacker.GetEquippedMainWeapon();
            if (weapon != null && weapon.IsWeapon)
            {
                attacker.GetScaledWeaponDamageDice(weapon, out int count, out int dice);
                float diceAverage = count > 0 && dice > 0
                    ? count * (dice + 1) * 0.5f
                    : 2.5f;

                float damageBonus = weapon.BonusDamage;
                if (weapon.WeaponCat == WeaponCategory.Melee || weapon.IsThrown)
                    damageBonus += attacker.Stats.STRMod;

                return Mathf.Max(1f, diceAverage + damageBonus);
            }

            NaturalAttackDefinition natural = attacker.Stats.GetPrimaryNaturalAttack();
            if (natural != null)
                return AverageNaturalDamage(attacker.Stats, natural);

            var unarmed = attacker.GetUnarmedDamage();
            return Mathf.Max(1f, unarmed.damageCount * (unarmed.damageDice + 1) * 0.5f + unarmed.bonusDamage + attacker.Stats.STRMod);
        }

        /// <summary>The average damage of one hit with <paramref name="natural"/> (size-scaled dice and its Strength share); at least 1.</summary>
        public static float AverageNaturalDamage(CharacterStats stats, NaturalAttackDefinition natural)
        {
            stats.GetScaledNaturalAttackDamage(natural, out int naturalCount, out int naturalDice);
            return Mathf.Max(1f, naturalCount * (naturalDice + 1) * 0.5f + stats.GetNaturalAttackDamageBonus(natural));
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
        /// </summary>
        public static float ExpectedRoundDamage(CharacterController attacker, int targetAC)
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
                        natTotal += Mathf.Max(1, a.Count) * hit * AverageNaturalDamage(st, a);
                    }
                    return natTotal;
                }
            }

            int bonus = weapon != null && weapon.IsWeapon && weapon.IsRangedWeapon ? st.GetRangedAttackBonus() : st.GetMeleeAttackBonus();
            float perHit = AverageMeleeDamage(attacker);
            float total = 0f;
            List<int> ladder = CharacterCombatStats.GetBaseAttackLadder(bab);
            for (int i = 0; i < ladder.Count; i++)
                total += HitChance(bonus + (ladder[i] - bab), targetAC) * perHit;
            return total;
        }
    }
}
