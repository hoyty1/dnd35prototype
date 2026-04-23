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
