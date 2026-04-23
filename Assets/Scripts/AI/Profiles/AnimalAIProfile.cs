using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Animal profile for pack hunters (wolves, similar creatures).
    /// Prioritizes nearby prey, nearby pack support, and low-risk movement.
    /// </summary>
    [CreateAssetMenu(fileName = "Animal AI", menuName = "DND35/AI/Profiles/Animal")]
    public class AnimalAIProfile : AIProfile
    {
        [Header("Animal Targeting")]
        [Range(0f, 4f)] public float DistanceWeight = 1.8f;
        [Range(0f, 6f)] public float FlankOpportunityBonus = 2.5f;

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Animal";

            if (string.IsNullOrWhiteSpace(Description))
                Description = "Pack-hunter profile that pressures nearby targets while avoiding unnecessary danger.";

            CombatStyle = CombatStyle.Melee;
            Aggression = 0.75f;
            PrioritizeWounded = true;
            PrioritizeIsolated = true;
            SwitchTargetsOften = false;

            if (Movement == null)
                Movement = new MovementPreferences();

            Movement.AvoidAoOs = true;
            Movement.PreferredRangeSquares = 0;
            Movement.MaintainDistance = false;
            Movement.SeekFlanking = true;
            Movement.UseCover = false;

            GrappleBehavior = GrappleBehavior.Avoid;

            if (Maneuvers == null)
                Maneuvers = new ManeuverPreferences();

            Maneuvers.AttemptTrip = true;
            Maneuvers.AttemptDisarm = false;
            Maneuvers.AttemptSunder = false;
            Maneuvers.AttemptBullRush = false;
            Maneuvers.AttemptOverrun = false;
            Maneuvers.UsePowerAttack = false;
        }

        public override float ScoreTarget(CharacterController target, CharacterController self)
        {
            float baseScore = base.ScoreTarget(target, self);
            if (baseScore == float.MinValue || self == null || target == null)
                return baseScore;

            int distance = SquareGridUtils.GetDistance(self.GridPosition, target.GridPosition);
            float distanceBonus = Mathf.Max(0, 8 - distance) * DistanceWeight;
            float flankBonus = CanPackThreatenFlank(target, self) ? FlankOpportunityBonus : 0f;

            return baseScore + distanceBonus + flankBonus;
        }

        public override SpecialAttackType? GetPreferredManeuver(CharacterController self, CharacterController target)
        {
            if (self == null || target == null)
                return null;

            // Creatures with innate trip (wolf bite) should attack normally; GameManager
            // resolves a free trip follow-up on successful melee hits.
            if (self.Stats != null && self.Stats.HasTripAttack)
                return null;

            return base.GetPreferredManeuver(self, target);
        }

        private static bool CanPackThreatenFlank(CharacterController target, CharacterController self)
        {
            if (target == null || self == null)
                return false;

            GameManager gm = GameManager.Instance;
            if (gm == null)
                return false;

            var all = gm.GetAllCharactersForAI();
            if (all == null)
                return false;

            for (int i = 0; i < all.Count; i++)
            {
                CharacterController ally = all[i];
                if (ally == null || ally == self || ally.Stats == null || ally.Stats.IsDead)
                    continue;

                if (!gm.IsEnemyTeamForAI(ally, target))
                    continue;

                int allyDistance = SquareGridUtils.GetDistance(ally.GridPosition, target.GridPosition);
                if (allyDistance <= 1)
                    return true;
            }

            return false;
        }
    }
}
