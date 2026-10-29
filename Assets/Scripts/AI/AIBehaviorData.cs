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
        [Tooltip("Tag to evaluate (e.g. 'Race: Elf', 'Armor: Light Armor', 'Unarmored')")]
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
