using System;
using System.Collections.Generic;
using UnityEngine;

namespace DND35.AI
{
    /// <summary>
    /// Preferred combat distance/style for the AI profile.
    /// </summary>
    public enum CombatStyle
    {
        Melee,
        Ranged,
        Mixed
    }

    /// <summary>
    /// What an AI-run creature does when no target passes its attack guard (AiAttackGuard; batch 13 step 5, design
    /// choice C1, docs/systems/RULES_COVERAGE.md 4.1): the ordered list of AIProfile.GetOutOfReachAlternatives, tried by
    /// AiOutOfReach.Run through the action catalog until one is performed. Batch 14 re-weights the list; batch 22
    /// replaces it with the evaluator.
    /// </summary>
    public enum OutOfReachAlternative
    {
        /// <summary>Attack another target the guard allows (the catalog's attack or full attack entry).</summary>
        AttackOtherTarget,
        /// <summary>Move to the profile's preferred range of the focus target (Movement.PreferredRangeSquares; the move entry), then attack if a target is now allowed (AI-045).</summary>
        PreferredRange,
        /// <summary>Move to the reach or range of the weapon in hand from the focus target (the move entry), then attack if a target is now allowed (AI-045).</summary>
        Approach,
        /// <summary>Withdraw from the focus target (the withdraw entry; PHB p.143). In no default list.</summary>
        Withdraw,
        /// <summary>Total defense (the total_defense entry; PHB p.142, D2).</summary>
        TotalDefense
    }

    /// <summary>
    /// What an AI-run creature does with the move action left after a standard action that is not an attack (a cast, a
    /// song, a maintained spell, Lay on Hands, Turn Undead, the healer's cure or buff; batch 14 step 11, AI-077, design
    /// choice C8): AIProfile.ChooseAfterStandardAction, tilted by the traits' RoleDesire (AiBlend.AfterStandardAction). A
    /// turn is a standard and a move action in either order (PHB p.135, p.138, D1), and not moving is legal. An AI choice,
    /// not a rule; batch 22's evaluator replaces it.
    /// </summary>
    public enum AfterStandardActionChoice
    {
        /// <summary>Spend nothing more this turn.</summary>
        Stay,
        /// <summary>Move toward the focus target, to the reach or range of the weapon in hand (the move entry).</summary>
        Approach,
        /// <summary>Move away from the nearest foe (the move entry, its path priced by AiBlend.AoOCost).</summary>
        KeepDistance,
        /// <summary>A prone creature that wants to move stands up with its move action instead (PHB p.141-142).</summary>
        StandUp
    }

    /// <summary>
    /// The pure reads AIProfile.ChooseAfterStandardAction decides on (batch 14 step 11, C8). AIService builds it after the
    /// standard action; a unit test may fill it by hand.
    /// </summary>
    public struct AfterStandardActionContext
    {
        /// <summary>The creature may use its move action now (AiLegal.CanMoveNow; for a prone creature, the stand-up entry with a move action left).</summary>
        public bool CanMove;
        /// <summary>The creature is prone and not grappling, so any move it wants stands it up first (PHB p.141).</summary>
        public bool Prone;
        /// <summary>The routine's focus target is alive, not within the reach or range of the weapon in hand, and not an undead the creature has turned (approaching within 10 feet would end the turning, PHB p.159).</summary>
        public bool FocusOutOfReach;
        /// <summary>A foe is within the keep-away distance (AiBlend.KeepAwayDistance of the kiter's 2 squares).</summary>
        public bool FoeWithinKeepAway;
        /// <summary>A foe threatens the creature's square (ThreatSystem.IsSquareThreatened).</summary>
        public bool Threatened;
        /// <summary>The creature can still fight from range after backing off (AIService.CanFightFromRange, AI-039).</summary>
        public bool CanFightFromRange;
        /// <summary>The Role's combat style (the routine's without a profile).</summary>
        public CombatStyle Style;
        /// <summary>The Role is a casting profile (AIProfile.IsCastingProfile).</summary>
        public bool CastingRole;

        /// <summary>Approach is a choice: the focus is out of reach.</summary>
        public bool CanApproach => CanMove && FocusOutOfReach;

        /// <summary>KeepDistance is a choice: a foe is close or threatens the creature.</summary>
        public bool CanKeepDistance => CanMove && (FoeWithinKeepAway || Threatened);
    }

    /// <summary>
    /// What a Healer-Role creature does when its cure found no target in reach (batch 14 step 12, AI-047, AI-063; design
    /// choice C8, owner question Q8): AIProfile.ChooseHealerApproach, tilted by the traits' RoleDesire
    /// (AiBlend.HealerApproach). A turn is a standard and a move action in either order (PHB p.135, p.138, D1), so the
    /// healer may move beside its patient and touch it with the cure in the same turn (a touch spell's range, PHB p.175). An
    /// AI choice, not a rule; batch 22's evaluator replaces it.
    /// </summary>
    public enum HealerApproachChoice
    {
        /// <summary>Today's turn: the spell walk over every castable spell, then the ranged kiter.</summary>
        Kiter,
        /// <summary>A 5-foot step or a move beside the patient (HealerApproachContext.TouchSquare), then the cure.</summary>
        MoveAndCast,
        /// <summary>A move toward a patient no move reaches this turn (HealerApproachContext.PartialSquare), then the kiter's standard action.</summary>
        PartialApproach
    }

    /// <summary>
    /// The pure reads AIProfile.ChooseHealerApproach decides on (batch 14 step 12, C8). AIService builds it after the cure
    /// found no target (AIService.BuildHealerApproachContext); a unit test may fill it by hand.
    /// </summary>
    public struct HealerApproachContext
    {
        /// <summary>The healer's cure has a patient other than itself (HealerAIProfile.GetPriorityHealTarget).</summary>
        public bool HasPatient;
        /// <summary>The patient is at or below the Role's critical threshold (HealerAIProfile.CriticalHealingThreshold, 25%).</summary>
        public bool PatientCritical;
        /// <summary>The healer's distance to the patient in squares, before any move.</summary>
        public int Distance;
        /// <summary>
        /// The price per provoker of a path that provokes (C6, Q8): the Role's (1000 for a Healer, which avoids AoOs; at
        /// most 6 for a critical patient), then the traits' ProvokeAoO vote (AiBlend.AoOCost). At 1000 or more no
        /// approach provokes.
        /// </summary>
        public float AoOPrice;
        /// <summary>A square beside the patient the healer reaches this turn within the AoO price, or null.</summary>
        public Vector2Int? TouchSquare;
        /// <summary>TouchSquare is reached by a 5-foot step (PHB p.144: it provokes nothing), not by a move action.</summary>
        public bool TouchByFiveFootStep;
        /// <summary>When no square beside the patient is reached: a move's destination closer to it, or null.</summary>
        public Vector2Int? PartialSquare;

        /// <summary>MoveAndCast is a choice: a square beside the patient is reached this turn.</summary>
        public bool CanMoveAndCast => HasPatient && TouchSquare.HasValue;

        /// <summary>PartialApproach is a choice: a move brings the healer closer to the patient.</summary>
        public bool CanApproachPartly => HasPatient && PartialSquare.HasValue;
    }

    /// <summary>
    /// Grapple posture for the AI profile.
    /// </summary>
    public enum GrappleBehavior
    {
        Avoid,
        EscapeOnly,
        InitiateWhenSafe,
        Aggressive,
        Maintain
    }

    /// <summary>
    /// Ordered/weighted target tag preference.
    /// </summary>
    [Serializable]
    public class TagPriority
    {
        [Tooltip("Target fact to test, parsed by DND35.AI.Knowledge.FactQuery and matched whole (e.g. 'Race: Elf', 'Armor: Light Armor', 'HP State: Dying', 'Unarmored')")]
        public string TagName;

        [Tooltip("Weight for this tag. Higher value = stronger impact.")]
        public float Priority = 1f;

        [Tooltip("If true, matching this tag reduces score instead of increasing it.")]
        public bool IsPenalty;

        public TagPriority() { }

        public TagPriority(string tagName, float priority, bool isPenalty = false)
        {
            TagName = tagName;
            Priority = priority;
            IsPenalty = isPenalty;
        }
    }

    /// <summary>
    /// Preferences for maneuver usage.
    /// </summary>
    [Serializable]
    public class ManeuverPreferences
    {
        public bool AttemptTrip;
        public bool AttemptDisarm;
        public bool AttemptBullRush;
        public bool AttemptSunder;
        public bool AttemptOverrun;

        public bool UseAidAnother;
        public bool UseCombatExpertise;
        public bool UsePowerAttack;
    }

    /// <summary>
    /// Preferences for movement/pathing.
    /// </summary>
    [Serializable]
    public class MovementPreferences
    {
        [Tooltip("If true, pathing will strongly prefer non-provoking movement paths.")]
        public bool AvoidAoOs = true;

        [Tooltip("Preferred distance in squares from primary target (0 = adjacent/melee)")]
        [Min(0)]
        public int PreferredRangeSquares = 0;

        [Tooltip("Try to preserve preferred range by backing away when too close.")]
        public bool MaintainDistance;

        [Tooltip("Prefer movement choices that create flanking.")]
        public bool SeekFlanking = true;

        [Tooltip("Reserved flag for future tactical cover support.")]
        public bool UseCover = true;
    }
}
