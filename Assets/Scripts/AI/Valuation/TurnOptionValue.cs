using System.Collections.Generic;
using DND35.AI.Knowledge;
using UnityEngine;

namespace DND35.AI.Valuation
{
    /// <summary>
    /// The AI's values of the melee turn's alternatives to the attack (batch 14 step 13, AI-063, batch 13's C5;
    /// docs/designs/personalities_batch14.md "Leftover hooks", ChooseMeleeTurnOption), in expected hit points for the round,
    /// and the reach estimate total defense and the prone start read. Estimates for choosing, never a rule: the cores roll
    /// the real aid (PHB p.154, D3), apply total defense (PHB p.142, D4) and the crawl (PHB p.142, D5). The constants are
    /// starter numbers (AI tuning), pinned by AiLeftoverHookTests. Reads only.
    /// <list type="bullet">
    /// <item>Aid offense: P(aid roll hits AC 10) x (the ally's hit chance at +2 minus its hit chance) x the ally's hit damage.</item>
    /// <item>Aid defense: P(aid roll) x (the opponent's hit chance on the ally minus at +2 AC) x the opponent's hit damage x
    /// its share on the ally x the drop weight.</item>
    /// <item>Total defense: for each foe, (its next-round damage on the creature minus at +4 AC) x its share on the creature x
    /// the drop weight, summed, times the creature's exchange rate (<see cref="TotalDefense"/>); 0 when no foe can reach
    /// the creature.</item>
    /// </list>
    /// A foe's share on a creature of the other side (<see cref="Share"/>) is the AI's guess at whom it attacks next round:
    /// among the creatures it threatens now when it threatens any (moving away from them provokes and loses its full
    /// attack), else among those it can reach this turn. The drop weight (<see cref="DropWeight"/>) doubles damage kept off a
    /// creature that one average hit of the attacker would drop (current hit points at most that hit). Every foe hit in
    /// these values is taken after the creature's own immunity, resistance and damage reduction against that foe's attack,
    /// off each roll (<see cref="AverageHitOn"/>, <see cref="FoeReach.DamageOn"/>; AI-091), so a foe none of whose rolls
    /// gets past the reduction counts no damage and drops nobody, and one whose larger rolls get past counts only those.
    /// </summary>
    public static class TurnOptionValue
    {
        /// <summary>Damage kept off a creature one average hit would drop counts this many times (starter constant).</summary>
        public const float DropWeight = 2f;

        /// <summary>Aid another's bonus (PHB p.154).</summary>
        public const int AidBonus = 2;

        /// <summary>Total defense's dodge bonus (PHB p.142).</summary>
        public const int TotalDefenseBonus = 4;

        /// <summary>The aid roll's target AC (PHB p.154).</summary>
        public const int AidRollAC = 10;

        /// <summary>The charge's +2 on its attack roll (PHB p.154).</summary>
        public const int ChargeAttackBonus = 2;

        /// <summary>A charge moves at least 10 feet (PHB p.154).</summary>
        public const int MinChargeSquares = 2;

        /// <summary>Aid offense: the aid roll's odds times the ally's gain in hit chance from +2 times its hit damage.</summary>
        public static float AidOffense(int aidModifier, int allyModifier, int opponentAC, float allyHitDamage)
            => AttackValue.HitChance(aidModifier, AidRollAC)
               * Mathf.Max(0f, AttackValue.HitChance(allyModifier + AidBonus, opponentAC) - AttackValue.HitChance(allyModifier, opponentAC))
               * Mathf.Max(0f, allyHitDamage);

        /// <summary>Aid defense: the aid roll's odds times the opponent's lost hits on the ally at +2 AC times its hit damage, its share on the ally and the drop weight.</summary>
        public static float AidDefense(int aidModifier, int opponentModifier, int allyAC, float opponentHitDamage, float share, float dropWeight)
            => AttackValue.HitChance(aidModifier, AidRollAC)
               * Mathf.Max(0f, AttackValue.HitChance(opponentModifier, allyAC) - AttackValue.HitChance(opponentModifier, allyAC + AidBonus))
               * Mathf.Max(0f, opponentHitDamage) * Mathf.Max(0f, share) * Mathf.Max(1f, dropWeight);

        /// <summary>One foe's part of total defense: its damage at the creature's AC minus at +4, times its share and the drop weight.</summary>
        public static float TotalDefensePart(float damageAtAC, float damageAtAcPlusFour, float share, float dropWeight)
            => Mathf.Max(0f, damageAtAC - damageAtAcPlusFour) * Mathf.Max(0f, share) * Mathf.Max(1f, dropWeight);

        /// <summary>
        /// Total defense's value in the attack's currency: the damage it keeps off the creature (the sum of its
        /// <see cref="TotalDefensePart"/>s) times the creature's exchange rate, its own attack value per hit point it takes
        /// next round, at most 1 (starter reading, an AI choice: a hit point kept is worth the damage the creature deals
        /// with it; owner question Q9 of docs/designs/personalities_batch14.md). Without the rate, the plan's one-for-one sum made every
        /// creature that one hit drops (a goblin) stand in total defense against any stronger fighter, which only delays a
        /// loss. With it the value is at most the damage kept off times S over the incoming damage, so it never reaches the
        /// attack's threshold S + max(0.5, 0.15 S) unless a drop weight applies (one average hit of a foe would drop the
        /// creature) and +4 AC removes at least about 57.5% of the incoming damage (2 x 0.575 = 1.15), with S at least about
        /// 0.5 / (2 x the share removed - 1): on the melee turn, total defense is the near-dropped creature's choice against
        /// foes that hit it only on high rolls (AiLeftoverHookTests.TestMeleeTurnOptionFormulas pins the bound).
        /// </summary>
        public static float TotalDefense(float keptOff, float incoming, float attackValue)
            => incoming <= 0f ? 0f : Mathf.Max(0f, keptOff) * Mathf.Clamp01(Mathf.Max(0f, attackValue) / incoming);

        /// <summary>The drop weight of an attacker whose average hit is <paramref name="hitDamage"/> on a creature at <paramref name="currentHp"/>.</summary>
        public static float DropWeightFor(float hitDamage, int currentHp) => currentHp <= hitDamage ? DropWeight : 1f;

        /// <summary>
        /// The expected damage of one attack of <paramref name="attacker"/>'s main attack against <paramref name="targetAC"/>
        /// (a move and an attack): its ranged estimate with a ranged weapon in the main hand (unless <paramref name="meleeOnly"/>),
        /// else its melee estimate, plus <paramref name="extraBonus"/> (a charge's +2), times AttackValue.AverageMeleeDamage
        /// (the weapon, the primary natural attack or the unarmed strike), taken after <paramref name="subject"/>'s
        /// immunity, resistance and damage reduction when a subject is given (<see cref="AverageHitOn"/>, AI-091).
        /// </summary>
        public static float SingleAttackDamage(CharacterController attacker, int targetAC, int extraBonus = 0, bool meleeOnly = false,
            CharacterController subject = null)
        {
            if (attacker == null || attacker.Stats == null)
                return 0f;
            ItemData weapon = attacker.GetEquippedMainWeapon();
            bool ranged = !meleeOnly && weapon != null && weapon.IsWeapon && weapon.IsRangedWeapon;
            int bonus = ranged ? attacker.Stats.GetRangedAttackBonus() : attacker.Stats.GetMeleeAttackBonus();
            return AttackValue.HitChance(bonus + extraBonus, targetAC) * HitOn(attacker, subject, weapon, ranged);
        }

        /// <summary>
        /// The expected damage of one hit of <paramref name="attacker"/>'s main attack (AttackValue.MainHitDice, whose
        /// average is AverageMeleeDamage) on <paramref name="subject"/>, after the subject's immunity, resistance and damage
        /// reduction against that attack, off each roll (AI-091: TargetNumbers.HitAfterMitigation, the damage pipeline's
        /// own rules; a ranged attack with a ranged weapon in the main hand). The drop weight and aid another's defense value
        /// read it, so a foe none of whose rolls passes the creature's reduction counts no hit; the raw average without a
        /// subject.
        /// </summary>
        public static float AverageHitOn(CharacterController attacker, CharacterController subject)
        {
            if (attacker == null || attacker.Stats == null)
                return 0f;
            ItemData weapon = attacker.GetEquippedMainWeapon();
            return HitOn(attacker, subject, weapon, weapon != null && weapon.IsWeapon && weapon.IsRangedWeapon);
        }

        private static float HitOn(CharacterController attacker, CharacterController subject, ItemData weapon, bool ranged)
        {
            if (!AttackValue.MainHitDice(attacker, out int count, out int sides, out int bonus))
                return 0f;
            return TargetNumbers.HitAfterMitigation(attacker, subject, count, sides, bonus,
                weapon != null && weapon.IsWeapon ? weapon : null, ranged);
        }

        /// <summary>How a foe can attack a creature next round.</summary>
        public enum ReachKind
        {
            /// <summary>It cannot attack it next round.</summary>
            None,
            /// <summary>It threatens it now (a full attack).</summary>
            Threatens,
            /// <summary>A ranged weapon in hand that can shoot covers it (a full attack from where it stands).</summary>
            Ranged,
            /// <summary>One move brings it into reach (a move and one attack).</summary>
            Move,
            /// <summary>
            /// A charge brings it into reach (one attack at +2, PHB p.154): it could charge next round
            /// (<see cref="FoeReach.CanChargeNextRound"/>), and the gap to its melee reach is beyond one move, at least 10 feet
            /// and within its charge distance (twice its speed; its speed when limited to one action).
            /// </summary>
            Charge
        }

        /// <summary>
        /// What one foe can do next round, read once: the squares it threatens, its melee reach, its move and its ranged
        /// weapon's range. A foe that cannot attack this round (KnowledgeFacts.CanAttackThisRound) reaches nothing. Its
        /// state (alive, move, the charge's conditions) and the subject's (alive) are read through the [KnowledgeLegacy]
        /// choke point TargetNumbers (AI-089), so the omniscience lint holds this file to the knowledge facade.
        /// </summary>
        public sealed class FoeReach
        {
            public readonly CharacterController Foe;
            public readonly bool CanAttack;
            private readonly HashSet<Vector2Int> _threatened;
            private readonly int _meleeReach;
            private readonly int _move;
            private readonly int _rangedSquares;
            private readonly int _chargeSquares;

            /// <summary>
            /// It could charge next round (the charge's own legality, GameManager.CanCharge, read for the next round; PHB
            /// p.154): a melee attack to make (a melee weapon, or unarmed: CharacterController.HasMeleeWeaponEquipped),
            /// standing, not grappling, not fatigued, exhausted or entangled, and not limited to a move action. The path is not
            /// checked (an estimate).
            /// </summary>
            public readonly bool CanChargeNextRound;

            public FoeReach(CharacterController foe)
            {
                Foe = foe;
                CanAttack = TargetNumbers.Alive(foe) && KnowledgeFacts.CanAttackThisRound(foe);
                if (!CanAttack)
                    return;
                _threatened = ThreatSystem.GetThreatenedSquares(foe, log: false);
                _meleeReach = Mathf.Max(1, foe.GetMeleeMaxAttackDistance());
                _move = Mathf.Max(0, TargetNumbers.MoveSquares(foe));
                ItemData weapon = foe.GetEquippedMainWeapon();
                if (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged && weapon.RangeIncrement > 0
                    && AiAttackGuard.CheckWeapon(foe, null).Ok)
                    _rangedSquares = RangeCalculator.GetMaxRangeSquares(weapon.RangeIncrement, weapon.IsThrown);
                CanChargeNextRound = _move > 0 && TargetNumbers.ChargeConditionsMet(foe);
                if (CanChargeNextRound)
                    _chargeSquares = TargetNumbers.SingleActionOnly(foe) ? _move : 2 * _move;
            }

            /// <summary>It threatens <paramref name="square"/> now.</summary>
            public bool ThreatensSquare(Vector2Int square) => CanAttack && _threatened != null && _threatened.Contains(square);

            /// <summary>It threatens <paramref name="square"/> now, or one move or a charge brings it within its melee reach of it.</summary>
            public bool CouldThreatenNextTurn(Vector2Int square)
            {
                if (!CanAttack)
                    return false;
                if (ThreatensSquare(square))
                    return true;
                return MeleeReachKind(MinDistance(square)) != ReachKind.None;
            }

            /// <summary>Move when one move closes <paramref name="distance"/> to its melee reach, Charge when only a charge does, else None.</summary>
            private ReachKind MeleeReachKind(int distance)
            {
                int gap = distance - _meleeReach;
                if (_move > 0 && gap <= _move)
                    return ReachKind.Move;
                if (CanChargeNextRound && gap >= MinChargeSquares && gap <= _chargeSquares)
                    return ReachKind.Charge;
                return ReachKind.None;
            }

            /// <summary>How it can attack <paramref name="subject"/> next round (<see cref="ReachKind"/>).</summary>
            public ReachKind ReachOf(CharacterController subject)
            {
                if (!CanAttack || subject == null || subject == Foe || !TargetNumbers.Alive(subject))
                    return ReachKind.None;
                List<Vector2Int> squares = subject.GetOccupiedSquares();
                for (int i = 0; i < squares.Count; i++)
                    if (ThreatensSquare(squares[i]))
                        return ReachKind.Threatens;
                int distance = Foe.GetMinimumDistanceToTarget(subject, chebyshev: false);
                if (_rangedSquares > 0 && distance <= _rangedSquares)
                    return ReachKind.Ranged;
                return MeleeReachKind(distance);
            }

            /// <summary>
            /// Its expected damage on <paramref name="subject"/> at <paramref name="ac"/> next round: a full attack when it
            /// threatens or shoots it, one attack after a move, one at +2 after a charge, else 0; each hit after the
            /// subject's resistance and damage reduction against it (AI-091).
            /// </summary>
            public float DamageOn(CharacterController subject, int ac)
            {
                switch (ReachOf(subject))
                {
                    case ReachKind.Threatens:
                    case ReachKind.Ranged:
                        return AttackValue.ExpectedRoundDamage(Foe, ac, subject);
                    case ReachKind.Move:
                        return SingleAttackDamage(Foe, ac, subject: subject);
                    case ReachKind.Charge:
                        return SingleAttackDamage(Foe, ac, ChargeAttackBonus, meleeOnly: true, subject: subject);
                    default:
                        return 0f;
                }
            }

            private int MinDistance(Vector2Int square)
            {
                int best = int.MaxValue;
                List<Vector2Int> mine = Foe.GetOccupiedSquares();
                for (int i = 0; i < mine.Count; i++)
                    best = Mathf.Min(best, SquareGridUtils.GetDistance(mine[i], square));
                return best;
            }
        }

        /// <summary>
        /// <paramref name="foe"/>'s share on <paramref name="subject"/>: 1 over the creatures of <paramref name="side"/> it
        /// threatens now when it threatens any (0 when the subject is not among them), else 1 over those it can reach next
        /// round (0 when it cannot reach the subject).
        /// </summary>
        public static float Share(FoeReach foe, CharacterController subject, IList<CharacterController> side)
        {
            if (foe == null || !foe.CanAttack || subject == null || side == null)
                return 0f;
            ReachKind mine = foe.ReachOf(subject);
            if (mine == ReachKind.None)
                return 0f;
            int threatened = 0;
            int reached = 0;
            for (int i = 0; i < side.Count; i++)
            {
                ReachKind k = foe.ReachOf(side[i]);
                if (k == ReachKind.Threatens)
                    threatened++;
                if (k != ReachKind.None)
                    reached++;
            }
            if (threatened > 0)
                return mine == ReachKind.Threatens ? 1f / threatened : 0f;
            return reached > 0 ? 1f / reached : 0f;
        }
    }
}
