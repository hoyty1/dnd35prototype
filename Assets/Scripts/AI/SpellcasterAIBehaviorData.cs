using System;
using System.Collections.Generic;
using UnityEngine;

namespace DND35.AI
{
    [Serializable]
    public class SpellSchoolPriority
    {
        [Tooltip("Spell school this caster prefers.")]
        public SpellSchool School = SpellSchool.Evocation;

        [Range(0f, 10f)]
        [Tooltip("Higher values are preferred during spell selection.")]
        public float Priority = 5f;

        [Tooltip("If true, school bonus only applies in combat spell choices.")]
        public bool CombatOnly = false;

        public SpellSchoolPriority() { }

        public SpellSchoolPriority(SpellSchool school, float priority)
        {
            School = school;
            Priority = priority;
        }
    }

    [Serializable]
    public class AOECastingPreferences
    {
        [Header("Targeting")]
        public bool PreferSingleTarget = false;

        [Range(1, 10)]
        [Tooltip("Minimum enemies in area before AoE is considered worthwhile.")]
        public int MinimumEnemiesInAOE = 2;

        [Header("Friendly Fire")]
        public bool AvoidHittingAllies = true;

        [Range(0f, 5f)]
        [Tooltip("Acceptable effective ally casualties from one AoE cast.")]
        public float MaxAcceptableAllyCasualties = 0f;

        [Range(0f, 1f)]
        [Tooltip("If estimated ally HP loss percent exceeds this, ally counts as casualty.")]
        public float AcceptableAllyDamagePercent = 0.10f;

        [Header("Mitigation Awareness")]
        public bool ConsiderAllyResistances = true;
        public bool IgnoreImmuneAllies = true;
        public bool ResistantAlliesCountHalf = true;
    }

    [Serializable]
    public class SpellSelectionPreferences
    {
        [Tooltip("Boost buff spell desirability when the caster is still healthy.")]
        public bool BuffBeforeDamage = true;

        [Tooltip("Penalize high-level spells unless target is dangerous/wounded.")]
        public bool ConserveHighLevelSpells = true;

        [Tooltip("Allow utility spells to stay in scoring rotation.")]
        public bool UseUtilitySpells = true;

        [Tooltip("Slightly increase score against likely enemy casters.")]
        public bool CounterEnemySpells = true;
    }

    /// <summary>
    /// How the best aim of an area spell would land (SpellcasterAIProfile.EvaluateAOECast; batch 11 step 25: the aim
    /// AreaAimSearch found, <see cref="Placement"/>, null when the caster has none).
    /// </summary>
    public readonly struct SpellAOEEvaluation
    {
        public readonly bool IsSafe;
        public readonly int EnemiesHit;
        public readonly int AlliesHit;
        public readonly float EffectiveAllyCasualties;

        /// <summary>The allies a beneficial area (Bless) would help (AreaPlacement.AlliesBenefited).</summary>
        public readonly int AlliesBenefited;

        /// <summary>The aim evaluated, or null when the caster has no aim with value.</summary>
        public readonly AreaPlacement Placement;

        public SpellAOEEvaluation(bool isSafe, int enemiesHit, int alliesHit, float effectiveAllyCasualties)
            : this(isSafe, enemiesHit, alliesHit, effectiveAllyCasualties, 0, null)
        {
        }

        public SpellAOEEvaluation(bool isSafe, int enemiesHit, int alliesHit, float effectiveAllyCasualties, int alliesBenefited, AreaPlacement placement)
        {
            IsSafe = isSafe;
            EnemiesHit = enemiesHit;
            AlliesHit = alliesHit;
            EffectiveAllyCasualties = effectiveAllyCasualties;
            AlliesBenefited = alliesBenefited;
            Placement = placement;
        }
    }
}
