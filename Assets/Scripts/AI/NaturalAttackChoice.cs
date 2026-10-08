using UnityEngine;

namespace DND35.AI
{
    /// <summary>
    /// Which natural attack an AI creature uses for Haste's extra attack (PHB p.239; owner decision
    /// 2026-10-07, CMB-106). The rule lets the attacker pick any one of its natural weapons, at that
    /// weapon's normal bonus. The default AI pick is the highest attack bonus, unless another natural
    /// attack carries a rider that matters against this target (Improved Grab, trip, poison, disease,
    /// paralysis, petrification, energy or ability drain), or does clearly more damage at the same bonus.
    /// <see cref="AIProfile.ChooseHasteNaturalAttackIndex"/> and <see cref="AIProfile.ScoreHasteNaturalAttack"/>
    /// call this and are the override points for profiles.
    /// </summary>
    public static class NaturalAttackChoice
    {
        /// <summary>A rider that matters against the target outweighs any attack-bonus difference.</summary>
        public const float RiderWeight = 1000f;

        /// <summary>One point of attack bonus outweighs any damage difference at levels 1-8.</summary>
        public const float AttackBonusWeight = 50f;

        /// <summary>
        /// Score of natural-sequence attack <paramref name="naturalAttackIndex"/> as Haste's extra attack
        /// against <paramref name="target"/>: rider value x <see cref="RiderWeight"/>, plus attack bonus x
        /// <see cref="AttackBonusWeight"/>, plus average damage. Higher is better; float.MinValue when the
        /// index is not a natural attack.
        /// </summary>
        public static float Score(CharacterController self, CharacterController target, int naturalAttackIndex)
        {
            if (self == null || self.Stats == null)
                return float.MinValue;

            NaturalAttackDefinition natural = self.Stats.GetNaturalAttackAtSequenceIndex(naturalAttackIndex);
            if (natural == null)
                return float.MinValue;

            return CountRidersThatMatter(self, target, natural) * RiderWeight
                + self.Stats.GetNaturalAttackBonus(natural) * AttackBonusWeight
                + self.GetNaturalAttackAverageDamage(natural);
        }

        /// <summary>
        /// The best natural-sequence index by <see cref="Score"/> (ties: the first in the sequence), or the
        /// shared default (CharacterController.GetDefaultHasteNaturalAttackIndex) when nothing scores.
        /// </summary>
        public static int ChooseHasteExtraAttackIndex(CharacterController self, CharacterController target)
            => ChooseBest(self, index => Score(self, target, index));

        /// <summary>The index with the highest score from <paramref name="score"/> (ties: the first).</summary>
        public static int ChooseBest(CharacterController self, System.Func<int, float> score)
        {
            if (self == null || self.Stats == null || score == null)
                return -1;

            int count = self.Stats.GetTotalNaturalAttackCount();
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                float s = score(i);
                if (s > bestScore)
                {
                    best = i;
                    bestScore = s;
                }
            }

            return best >= 0 ? best : self.GetDefaultHasteNaturalAttackIndex();
        }

        /// <summary>
        /// Riders on this natural attack that can still do something to this target. Each counts 1; a
        /// rider the target is immune to, or that it already suffers, counts 0.
        /// </summary>
        public static int CountRidersThatMatter(CharacterController self, CharacterController target, NaturalAttackDefinition natural)
        {
            if (self == null || self.Stats == null || natural == null)
                return 0;

            CharacterStats targetStats = target != null ? target.Stats : null;
            string targetType = targetStats != null && !string.IsNullOrWhiteSpace(targetStats.CreatureType)
                ? targetStats.CreatureType.Trim().ToLowerInvariant()
                : string.Empty;
            bool targetIsUndeadOrConstruct = targetType == "undead" || targetType == "construct";
            int riders = 0;

            // Improved Grab: only the trigger attack starts the free grapple (same name match as
            // GameManager.IsImprovedGrabTriggerAttack); useless while either side is already grappling.
            if (self.Stats.HasImprovedGrab && IsImprovedGrabTrigger(self.Stats, natural)
                && !self.IsGrappling() && (target == null || !target.IsGrappling()))
                riders++;

            // Trip (Ex): the code follows any natural melee hit with the free trip
            // (GameManager.TryResolveFreeTripOnHit), so it adds the same value to every natural attack.
            if (self.Stats.HasTripAttack && (target == null || !target.HasCondition(CombatConditionType.Prone)))
                riders++;

            if (!string.IsNullOrWhiteSpace(natural.PoisonOnHitId) && (targetStats == null || !targetStats.IsImmuneToPoison()))
                riders++;

            if (natural.HasDiseaseOnHit && (targetStats == null || !targetStats.IsImmuneToDisease()))
                riders++;

            if (natural.ParalysisOnHitDC > 0 && !targetIsUndeadOrConstruct
                && (target == null || !target.HasCondition(CombatConditionType.Paralyzed)))
                riders++;

            if (natural.PetrificationOnHitDC > 0 && (target == null || !target.HasCondition(CombatConditionType.Petrified)))
                riders++;

            // Energy drain and ability drain: undead and constructs are immune (MM p.317, p.307).
            if ((natural.EnergyDrainOnHit > 0 || natural.AbilityDrainAmount > 0 || natural.HasBloodDrain) && !targetIsUndeadOrConstruct)
                riders++;

            return riders;
        }

        private static bool IsImprovedGrabTrigger(CharacterStats stats, NaturalAttackDefinition natural)
        {
            string trigger = string.IsNullOrWhiteSpace(stats.ImprovedGrabTriggerAttackName) ? "claw" : stats.ImprovedGrabTriggerAttackName;
            return !string.IsNullOrWhiteSpace(natural.Name)
                && natural.Name.IndexOf(trigger, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
