using System;
using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Mindless undead behavior (skeletons, zombies, etc.):
    /// - Always pressure the nearest enemy
    /// - No tactical maneuvers
    /// - Ignores attacks of opportunity
    /// - Uses simple weapon fallback (equipped -> inventory weapon -> natural -> unarmed)
    /// </summary>
    [CreateAssetMenu(fileName = "Undead Mindless AI", menuName = "DND35/AI/Profiles/Undead Mindless")]
    public class UndeadMindlessAIProfile : AIProfile
    {
        public override void ApplyRuntimeDefaults()
        {
            base.ApplyRuntimeDefaults();

            ProfileName = "Undead Mindless";
            Description = "Mindless undead that shamble toward the nearest living target and attack without tactics.";

            CombatStyle = CombatStyle.Melee;
            Aggression = 1f;
            PrioritizeWounded = false;
            PrioritizeIsolated = false;
            SwitchTargetsOften = true; // re-evaluate nearest each turn

            if (Movement == null)
                Movement = new MovementPreferences();

            Movement.AvoidAoOs = false;
            Movement.PreferredRangeSquares = 0;
            Movement.MaintainDistance = false;
            Movement.SeekFlanking = false;
            Movement.UseCover = false;

            GrappleBehavior = GrappleBehavior.Avoid;

            if (Maneuvers == null)
                Maneuvers = new ManeuverPreferences();

            Maneuvers.AttemptTrip = false;
            Maneuvers.AttemptDisarm = false;
            Maneuvers.AttemptSunder = false;
            Maneuvers.AttemptBullRush = false;
            Maneuvers.AttemptOverrun = false;
            Maneuvers.UsePowerAttack = false;
        }

        public override float ScoreTarget(CharacterController target, CharacterController self)
        {
            if (target == null || target.Stats == null || target.Stats.IsDead || self == null)
                return float.MinValue;

            int distance = SquareGridUtils.GetDistance(self.GridPosition, target.GridPosition);

            // Keep this dominant over any perception/other additive modifiers so nearest always wins.
            return 100000f - (distance * 1000f);
        }

        public override bool ShouldIgnoreAoO(CharacterController self)
        {
            return true;
        }

        public override bool ShouldUseCoupDeGrace(CharacterController self)
        {
            return false;
        }

        public override bool ShouldInitiateGrapple(CharacterController self, CharacterController target)
        {
            return false;
        }

        public override SpecialAttackType? GetPreferredManeuver(CharacterController self, CharacterController target)
        {
            return null;
        }

        public override bool ShouldSwitchTargetsMidFullAttack(CharacterController self)
        {
            return false;
        }

        public override bool ShouldTakeFiveFootStepToContinueFullAttack(CharacterController self)
        {
            return false;
        }

        /// <summary>
        /// A mindless undead re-arms itself after a disarm (or a throw) with the melee weapon in its pack, through the
        /// catalog's draw entry at its RAW cost (a move action, PHB p.142; batch 13 step 15, D8), even when it has natural
        /// attacks (a skeleton's claws): the shambling pressure this profile models.
        /// </summary>
        public override bool TryEnsureWeaponFallback(CharacterController self)
            => AiWeaponFallback.TryDraw(self, preferMelee: true, evenWithNaturalAttacks: true);
    }
}
