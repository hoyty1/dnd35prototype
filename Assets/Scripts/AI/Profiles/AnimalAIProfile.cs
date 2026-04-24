using System.Collections.Generic;
using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Animal combat specialties based on natural ability packages.
    /// </summary>
    public enum AnimalSpecialty
    {
        Grappler,   // Pounce + Improved Grab + Rake package (Tiger/Lion style)
        Tripper,    // Free trip-on-hit package (Wolf style)
        PackHunter  // Default animal pack tactics
    }

    /// <summary>
    /// Animal profile that adapts behavior by creature specialty.
    /// - Grapplers prioritize charge/pounce pressure and grappling.
    /// - Trippers prioritize bite pressure with free trip follow-ups.
    /// - Pack hunters prioritize safer baseline melee pressure and flanking.
    /// </summary>
    [CreateAssetMenu(fileName = "Animal AI", menuName = "DND35/AI/Profiles/Animal")]
    public class AnimalAIProfile : AIProfile
    {
        [Header("Animal Targeting")]
        [Range(0f, 4f)] public float DistanceWeight = 1.8f;
        [Range(0f, 6f)] public float FlankOpportunityBonus = 2.5f;
        [Range(0f, 6f)] public float GrapplerTargetBonus = 2.0f;
        [Range(0f, 6f)] public float TripperStandingTargetBonus = 1.5f;

        [Header("Charge Preferences (Squares)")]
        [Min(1)] public int MinChargeDistanceSquares = 2; // 10 ft
        [Min(2)] public int PackHunterChargeDistanceSquares = 4; // 20 ft+

        private readonly HashSet<int> _specialtyLoggedActors = new HashSet<int>();

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Animal";

            if (string.IsNullOrWhiteSpace(Description))
                Description = "Specialty-driven animal profile that adapts between grappler, tripper, and pack-hunter tactics.";

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

            // Default to avoid grappling unless specialty checks allow it.
            GrappleBehavior = GrappleBehavior.Avoid;

            if (Maneuvers == null)
                Maneuvers = new ManeuverPreferences();

            Maneuvers.AttemptTrip = true;
            Maneuvers.AttemptDisarm = false;
            Maneuvers.AttemptSunder = false;
            Maneuvers.AttemptBullRush = false;
            Maneuvers.AttemptOverrun = false;
            Maneuvers.UsePowerAttack = false;

            _specialtyLoggedActors.Clear();
        }

        public override float ScoreTarget(CharacterController target, CharacterController self)
        {
            float baseScore = base.ScoreTarget(target, self);
            if (baseScore == float.MinValue || self == null || target == null)
                return baseScore;

            int distance = SquareGridUtils.GetDistance(self.GridPosition, target.GridPosition);
            float distanceBonus = Mathf.Max(0, 8 - distance) * DistanceWeight;
            float flankBonus = CanPackThreatenFlank(target, self) ? FlankOpportunityBonus : 0f;

            AnimalSpecialty specialty = DetermineSpecialty(self);
            float specialtyBonus = 0f;
            switch (specialty)
            {
                case AnimalSpecialty.Grappler:
                    // Prefer targets not already in a grapple with this attacker.
                    if (!IsTargetGrappledByAttacker(target, self))
                        specialtyBonus += GrapplerTargetBonus;
                    break;

                case AnimalSpecialty.Tripper:
                    // Prefer standing targets (trip value) over already-prone ones.
                    if (!target.HasCondition(CombatConditionType.Prone))
                        specialtyBonus += TripperStandingTargetBonus;
                    break;

                case AnimalSpecialty.PackHunter:
                    // Pack hunters lean into flanking support already represented above.
                    break;
            }

            return baseScore + distanceBonus + flankBonus + specialtyBonus;
        }

        public override bool ShouldPreferCharge(CharacterController self, CharacterController target, int distanceSquares, bool preferAggression)
        {
            if (self == null || target == null)
                return false;

            AnimalSpecialty specialty = DetermineSpecialty(self);
            if (distanceSquares < Mathf.Max(1, MinChargeDistanceSquares))
                return false;

            switch (specialty)
            {
                case AnimalSpecialty.Grappler:
                    // Grapplers strongly prefer charge when pounce package is online.
                    return self.Stats != null && self.Stats.HasPounce;

                case AnimalSpecialty.Tripper:
                    // Trippers charge to fish for bite-hit free trip; avoid charging prone targets.
                    return !target.HasCondition(CombatConditionType.Prone);

                case AnimalSpecialty.PackHunter:
                    // Pack hunters charge mostly when still far enough away.
                    return distanceSquares >= Mathf.Max(2, PackHunterChargeDistanceSquares);

                default:
                    return preferAggression;
            }
        }

        public override bool ShouldIgnoreUnconsciousTargets(CharacterController self)
        {
            return true;
        }

        public override bool ShouldSwitchTargetsMidFullAttack(CharacterController self)
        {
            return true;
        }

        public override bool ShouldTakeFiveFootStepToContinueFullAttack(CharacterController self)
        {
            return true;
        }

        /// <summary>
        /// Animals only attempt to escape a grapple when critically wounded.
        /// Otherwise they should keep mauling their prey.
        /// </summary>
        public override bool ShouldEscapeGrapple(CharacterController self)
        {
            return ShouldAttemptEmergencyGrappleEscape(self);
        }

        /// <summary>
        /// Predatory animals in a grapple prioritize lethal natural attacks.
        /// </summary>
        public bool ShouldPrioritizeLethalNaturalGrappleAttacks(CharacterController self)
        {
            if (self == null || self.Stats == null)
                return false;

            AnimalSpecialty specialty = DetermineSpecialty(self);
            if (specialty != AnimalSpecialty.Grappler)
                return false;

            if (!self.IsGrappling())
                return false;

            return !ShouldAttemptEmergencyGrappleEscape(self);
        }

        /// <summary>
        /// Escape only when survival is threatened.
        /// </summary>
        public bool ShouldAttemptEmergencyGrappleEscape(CharacterController self)
        {
            if (self == null || self.Stats == null)
                return false;

            int maxHp = Mathf.Max(1, self.Stats.TotalMaxHP);
            float hpPercent = (float)self.Stats.CurrentHP / maxHp;
            bool shouldEscape = hpPercent < 0.25f;

            if (shouldEscape)
            {
                Debug.Log($"[AI][Animal] {self.Stats.CharacterName} is critically wounded ({self.Stats.CurrentHP}/{maxHp}, {hpPercent:P0}) and will try to escape grapple.");
            }

            return shouldEscape;
        }

        public override bool ShouldInitiateGrapple(CharacterController self, CharacterController target)
        {
            if (self == null || target == null || target.Stats == null || target.Stats.IsDead)
                return false;

            if (self.Stats != null && self.Stats.HasImprovedGrab)
            {
                Debug.Log($"[AI][Animal] {self.Stats.CharacterName} has Improved Grab; skipping manual Grapple action (handled on qualifying hit).");
                return false;
            }

            AnimalSpecialty specialty = DetermineSpecialty(self);
            switch (specialty)
            {
                case AnimalSpecialty.Grappler:
                    if (self.IsGrappling())
                        return false;

                    // Grappler specialty uses attack pressure; grapples are initiated via Improved Grab trigger on hit.
                    return false;

                case AnimalSpecialty.Tripper:
                case AnimalSpecialty.PackHunter:
                default:
                    // Non-grappler animals should not choose grapple maneuvers.
                    return false;
            }
        }

        public override SpecialAttackType? GetPreferredManeuver(CharacterController self, CharacterController target)
        {
            if (self == null || target == null || self.Stats == null || target.Stats == null)
                return null;

            AnimalSpecialty specialty = DetermineSpecialty(self);
            switch (specialty)
            {
                case AnimalSpecialty.Grappler:
                    if (ShouldInitiateGrapple(self, target))
                        return SpecialAttackType.Grapple;
                    return null;

                case AnimalSpecialty.Tripper:
                    // Free trip on hit is resolved by combat flow; don't spend standard action on explicit trip.
                    return null;

                case AnimalSpecialty.PackHunter:
                    // Pack hunters use straightforward pressure and position, not special maneuvers.
                    return null;

                default:
                    return null;
            }
        }

        private AnimalSpecialty DetermineSpecialty(CharacterController self)
        {
            CharacterStats stats = self != null ? self.Stats : null;
            if (stats == null)
                return AnimalSpecialty.PackHunter;

            AnimalSpecialty specialty;

            // Most specific first: full pounce-grapple-rake package.
            if (stats.HasPounce && stats.HasImprovedGrab && stats.HasRake)
            {
                specialty = AnimalSpecialty.Grappler;
            }
            else if (stats.HasTripAttack)
            {
                specialty = AnimalSpecialty.Tripper;
            }
            else
            {
                specialty = AnimalSpecialty.PackHunter;
            }

            MaybeLogSpecialty(self, specialty);
            return specialty;
        }

        private void MaybeLogSpecialty(CharacterController self, AnimalSpecialty specialty)
        {
            if (self == null || self.Stats == null)
                return;

            int actorId = self.GetInstanceID();
            if (_specialtyLoggedActors.Contains(actorId))
                return;

            _specialtyLoggedActors.Add(actorId);

            Debug.Log($"=== {self.Stats.CharacterName} Animal AI Specialty ===");
            Debug.Log($"Specialty: {specialty}");
            Debug.Log($"Abilities: Trip={self.Stats.HasTripAttack}, Pounce={self.Stats.HasPounce}, ImprovedGrab={self.Stats.HasImprovedGrab}, Rake={self.Stats.HasRake}");

            switch (specialty)
            {
                case AnimalSpecialty.Grappler:
                    Debug.Log("Tactics: Charge/pounce pressure, grapple follow-through, avoid trip maneuvers.");
                    break;
                case AnimalSpecialty.Tripper:
                    Debug.Log("Tactics: Bite pressure with free trip attempts, punish standing targets, avoid grappling.");
                    break;
                default:
                    Debug.Log("Tactics: Pack pressure, flanking, standard attacks.");
                    break;
            }
        }

        private static bool IsTargetGrappledByAttacker(CharacterController target, CharacterController attacker)
        {
            if (target == null || attacker == null)
                return false;

            if (!target.TryGetGrappleState(out CharacterController opponent, out _, out _, out _))
                return false;

            return opponent == attacker;
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
