using System.Collections.Generic;
using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Swarm AI: locks onto nearest enemy, occupies its space, and keeps pressuring until that target dies.
    /// </summary>
    [CreateAssetMenu(fileName = "Swarm AI", menuName = "DND35/AI/Profiles/Swarm")]
    public class SwarmAI : AIProfile
    {
        protected CharacterController CurrentTarget;

        protected virtual void OnEnable()
        {
            ProfileName = "Swarm";
            Description = "Occupies the nearest enemy's space and stays on that target until it dies.";
            CombatStyle = CombatStyle.Melee;
            Aggression = 1f;
            PrioritizeWounded = false;
            PrioritizeIsolated = false;
            SwitchTargetsOften = false;

            if (Movement == null)
                Movement = new MovementPreferences();

            Movement.PreferredRangeSquares = 0;
            Movement.AvoidAoOs = false;
            Movement.SeekFlanking = false;
            Movement.MaintainDistance = false;
            Movement.UseCover = false;

            CurrentTarget = null;
        }

        public virtual CharacterController ResolveTarget(CharacterController swarm, List<CharacterController> candidateTargets)
        {
            if (swarm == null)
            {
                CurrentTarget = null;
                return null;
            }

            if (!IsValidCurrentTarget(CurrentTarget, candidateTargets))
                CurrentTarget = FindNearestTarget(swarm, candidateTargets);

            return CurrentTarget;
        }

        public virtual void ForceClearTarget(CharacterController deadTarget = null)
        {
            if (deadTarget == null || CurrentTarget == deadTarget)
                CurrentTarget = null;
        }

        public bool IsOccupyingTargetSpace(CharacterController swarm, CharacterController target)
        {
            if (swarm == null || target == null)
                return false;

            return SquareGridUtils.GetChebyshevDistance(swarm.GridPosition, target.GridPosition) == 0;
        }

        public override float ScoreTarget(CharacterController target, CharacterController self)
        {
            if (target == null || target.Stats == null || target.Stats.IsDead || self == null)
                return float.MinValue;

            int distance = SquareGridUtils.GetDistance(self.GridPosition, target.GridPosition);
            return 100f - distance;
        }

        protected virtual bool IsValidCurrentTarget(CharacterController target, List<CharacterController> candidateTargets)
        {
            return target != null
                   && target.Stats != null
                   && !target.Stats.IsDead
                   && candidateTargets != null
                   && candidateTargets.Contains(target);
        }

        protected virtual CharacterController FindNearestTarget(CharacterController swarm, List<CharacterController> candidateTargets)
        {
            if (swarm == null || candidateTargets == null || candidateTargets.Count == 0)
                return null;

            CharacterController nearest = null;
            int minDistance = int.MaxValue;

            for (int i = 0; i < candidateTargets.Count; i++)
            {
                CharacterController candidate = candidateTargets[i];
                if (candidate == null || candidate == swarm || candidate.Stats == null || candidate.Stats.IsDead)
                    continue;

                int distance = SquareGridUtils.GetDistance(swarm.GridPosition, candidate.GridPosition);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }
    }
}
