using UnityEngine;

namespace DND35.AI.Custom
{
    /// <summary>
    /// Example custom profile showing how to extend score + maneuver decisions.
    /// </summary>
    [CreateAssetMenu(fileName = "Custom AI Example", menuName = "DND35/AI/Custom/Example")]
    public class CustomAIExample : AIProfile
    {
        [Header("Custom Tuning")]
        [Range(0f, 10f)]
        public float PoisonedTargetBonus = 3f;

        public override float ScoreTarget(CharacterController target, CharacterController self)
        {
            float score = base.ScoreTarget(target, self);
            if (target != null && target.HasCondition(CombatConditionType.Poisoned))
                score += PoisonedTargetBonus;
            return score;
        }

        public override bool ShouldInitiateGrapple(CharacterController self, CharacterController target)
        {
            if (self == null || target == null || self.Stats == null || target.Stats == null)
                return false;

            bool healthy = self.Stats.TotalMaxHP > 0 && self.Stats.CurrentHP >= Mathf.CeilToInt(self.Stats.TotalMaxHP * 0.75f);
            bool woundedTarget = target.Stats.TotalMaxHP > 0 && target.Stats.CurrentHP <= Mathf.CeilToInt(target.Stats.TotalMaxHP * 0.5f);
            if (healthy && woundedTarget)
                return true;

            return base.ShouldInitiateGrapple(self, target);
        }

        public override SpecialAttackType? GetPreferredManeuver(CharacterController self, CharacterController target)
        {
            if (self != null && target != null && self.Stats != null)
            {
                // Example: high-STR bruiser prefers trip when target validation passes.
                if (self.Stats.STRMod >= 3 && IsValidTripTarget(target))
                    return SpecialAttackType.Trip;

                // Example: use disarm when opponent has a valid held weapon.
                if (self.Stats.DEXMod >= 2 && IsValidDisarmTarget(target))
                    return SpecialAttackType.Disarm;
            }

            return base.GetPreferredManeuver(self, target);
        }
    }
}
