using System;
using System.Collections.Generic;
using UnityEngine;

namespace DND35.AI
{
    /// <summary>
    /// Data-driven AI profile for NPC behavior.
    /// Assign to CharacterController.aiProfile.
    /// </summary>
    [CreateAssetMenu(fileName = "New AI Profile", menuName = "DND35/AI/AI Profile")]
    public class AIProfile : ScriptableObject
    {
        [Header("Identity")]
        public string ProfileName = "Default AI";

        [TextArea(2, 6)]
        public string Description = "";

        [Header("Combat Style")]
        public CombatStyle CombatStyle = CombatStyle.Melee;

        [Range(0f, 1f)]
        [Tooltip("High aggression = less retreating and more direct pressure.")]
        public float Aggression = 0.5f;

        [Header("Target Selection")]
        [Tooltip("Weighted target tag preferences.")]
        public List<TagPriority> TagPriorities = new List<TagPriority>();

        public bool PrioritizeWounded;
        public bool PrioritizeIsolated;
        public bool SwitchTargetsOften;

        [Header("Movement")]
        public MovementPreferences Movement = new MovementPreferences();

        [Header("Grapple")]
        public GrappleBehavior GrappleBehavior = GrappleBehavior.EscapeOnly;

        [Header("Maneuvers")]
        public ManeuverPreferences Maneuvers = new ManeuverPreferences();

        [Header("Custom")]
        [Tooltip("Optional metadata reference for custom extension script.")]
        public MonoScript CustomAIScript;

        public virtual float ScoreTarget(CharacterController target, CharacterController self)
        {
            if (target == null || target.Stats == null || target.Stats.IsDead || self == null)
                return float.MinValue;

            float score = 10f;

            // Tag priorities
            if (target.Tags != null && TagPriorities != null)
            {
                foreach (TagPriority priority in TagPriorities)
                {
                    if (priority == null || string.IsNullOrWhiteSpace(priority.TagName))
                        continue;

                    if (TargetHasMatchingTag(target.Tags, priority.TagName))
                        score += priority.IsPenalty ? -priority.Priority : priority.Priority;
                }
            }

            // Wounded bonus
            if (PrioritizeWounded && target.Stats.TotalMaxHP > 0)
            {
                float hpPct = Mathf.Clamp01((float)target.Stats.CurrentHP / target.Stats.TotalMaxHP);
                score += (1f - hpPct) * 8f;
            }

            // Isolated bonus (simple approximation by adjacent allies)
            if (PrioritizeIsolated)
            {
                int adjacentAllies = CountAdjacentAllies(target);
                if (adjacentAllies == 0)
                    score += 4f;
                else
                    score -= adjacentAllies * 1.25f;
            }

            int distance = SquareGridUtils.GetDistance(self.GridPosition, target.GridPosition);
            if (CombatStyle == CombatStyle.Ranged)
            {
                int preferred = Mathf.Max(1, Movement != null ? Movement.PreferredRangeSquares : 4);
                score -= Mathf.Abs(distance - preferred) * 0.9f;
                if (distance <= 1)
                    score -= 5f;
            }
            else if (CombatStyle == CombatStyle.Melee)
            {
                score += Mathf.Max(0, 6 - distance) * 0.7f;
            }

            return score;
        }

        public virtual bool ShouldEscapeGrapple(CharacterController self)
        {
            return GrappleBehavior == GrappleBehavior.Avoid || GrappleBehavior == GrappleBehavior.EscapeOnly;
        }

        public virtual bool ShouldInitiateGrapple(CharacterController self, CharacterController target)
        {
            if (self == null || target == null || target.Stats == null)
                return false;

            if (GrappleBehavior == GrappleBehavior.Avoid || GrappleBehavior == GrappleBehavior.EscapeOnly)
                return false;

            if (GrappleBehavior == GrappleBehavior.Aggressive)
                return true;

            if (GrappleBehavior == GrappleBehavior.Maintain)
                return self.IsGrappling();

            // InitiateWhenSafe
            return self.Stats != null && self.Stats.STRMod >= target.Stats.STRMod;
        }

        public virtual SpecialAttackType? GetPreferredManeuver(CharacterController self, CharacterController target)
        {
            if (self == null || target == null)
                return null;

            if (self.IsGrappling() && GrappleBehavior == GrappleBehavior.Maintain)
                return SpecialAttackType.Grapple;

            if (Maneuvers != null)
            {
                if (Maneuvers.AttemptTrip && !target.HasCondition(CombatConditionType.Prone) && self.HasMeleeWeaponEquipped())
                    return SpecialAttackType.Trip;

                if (Maneuvers.AttemptDisarm && target.GetEquippedMainWeapon() != null)
                    return SpecialAttackType.Disarm;
            }

            if (ShouldInitiateGrapple(self, target))
                return SpecialAttackType.Grapple;

            return null;
        }

        protected static bool TargetHasMatchingTag(CharacterTags tags, string requested)
        {
            if (tags == null || string.IsNullOrWhiteSpace(requested))
                return false;

            string normalizedRequested = NormalizeTag(requested);

            foreach (string existing in tags.GetAllTags())
            {
                if (string.IsNullOrWhiteSpace(existing))
                    continue;

                string normalizedExisting = NormalizeTag(existing);
                if (normalizedExisting.Equals(normalizedRequested, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Friendly partial matching supports both "Armor: Light Armor" and "Light Armor" etc.
                if (normalizedExisting.IndexOf(normalizedRequested, StringComparison.OrdinalIgnoreCase) >= 0
                    || normalizedRequested.IndexOf(normalizedExisting, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static string NormalizeTag(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            return raw.Trim().Replace(":", ": ").Replace("  ", " ");
        }

        private static int CountAdjacentAllies(CharacterController target)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
                return 0;

            List<CharacterController> all = gm.GetAllCharactersForAI();
            if (all == null)
                return 0;

            int count = 0;
            for (int i = 0; i < all.Count; i++)
            {
                CharacterController c = all[i];
                if (c == null || c == target || c.Stats == null || c.Stats.IsDead)
                    continue;

                if (gm.IsEnemyTeamForAI(c, target))
                    continue;

                int dist = SquareGridUtils.GetDistance(c.GridPosition, target.GridPosition);
                if (dist <= 1)
                    count++;
            }

            return count;
        }
    }
}
