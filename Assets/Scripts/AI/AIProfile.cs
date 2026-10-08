using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

        /// <summary>
        /// When true, target selection strongly prefers enemies with lower concealment miss chance.
        /// </summary>
        public virtual bool PrioritizeVisibleTargets => true;

        /// <summary>
        /// Multiplier applied to concealment visibility bonuses/penalties during target selection.
        /// 0 disables concealment weighting, 1 uses default weighting, values >1 increase weighting.
        /// </summary>
        public virtual float ConcealmentPenaltyMultiplier => 1f;

        [Header("Movement")]
        public MovementPreferences Movement = new MovementPreferences();

        [Header("Grapple")]
        public GrappleBehavior GrappleBehavior = GrappleBehavior.EscapeOnly;

        [Header("Maneuvers")]
        public ManeuverPreferences Maneuvers = new ManeuverPreferences();

#if UNITY_EDITOR
        [Header("Custom")]
        [Tooltip("Optional metadata reference for custom extension script.")]
        public MonoScript CustomAIScript;
#endif

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

        /// <summary>
        /// Multiplier for how much AoO risk this profile tolerates before preferring safer ranged tactics.
        /// Lower values = more conservative behavior.
        /// </summary>
        public virtual float GetRangedAoORiskToleranceMultiplier()
        {
            return 1f;
        }

        /// <summary>
        /// Profile-level hook for charge preference after baseline charge validity has passed.
        /// Return true to take charge pressure, false to continue with normal attack/move flow.
        /// </summary>
        public virtual bool ShouldPreferCharge(CharacterController self, CharacterController target, int distanceSquares, bool preferAggression)
        {
            if (self == null || target == null)
                return false;

            // Default preserves existing behavior: if charging is legally available, profiles allow it.
            return true;
        }

        /// <summary>
        /// When true, profile target selection should ignore unconscious targets as long as at least one conscious enemy is available.
        /// </summary>
        public virtual bool ShouldIgnoreUnconsciousTargets(CharacterController self)
        {
            return false;
        }

        /// <summary>
        /// When true, this profile can swap targets mid full-attack sequence if the current target drops unconscious/dead.
        /// </summary>
        public virtual bool ShouldSwitchTargetsMidFullAttack(CharacterController self)
        {
            return false;
        }

        /// <summary>
        /// When true, this profile is allowed to spend an available 5-foot step to continue a full attack on a new target.
        /// </summary>
        public virtual bool ShouldTakeFiveFootStepToContinueFullAttack(CharacterController self)
        {
            return false;
        }

        /// <summary>
        /// When true, this profile will execute adjacent helpless targets with Coup de Grace when legal.
        /// </summary>
        public virtual bool ShouldUseCoupDeGrace(CharacterController self)
        {
            return false;
        }

        /// <summary>
        /// When true, this profile is willing to ignore attacks of opportunity while moving.
        /// </summary>
        public virtual bool ShouldIgnoreAoO(CharacterController self)
        {
            return false;
        }

        /// <summary>
        /// Optional per-profile weapon fallback hook used at attack time.
        /// Return true if this profile changed weapon state this turn.
        /// </summary>
        public virtual bool TryEnsureWeaponFallback(CharacterController self)
        {
            return false;
        }

        public virtual bool ShouldInitiateGrapple(CharacterController self, CharacterController target)
        {
            if (self == null || target == null || target.Stats == null)
                return false;

            if (self.Stats != null && self.Stats.HasImprovedGrab)
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

        /// <summary>
        /// Validates whether trip is meaningful against the current target: it is standing, and, when
        /// <paramref name="self"/> is given, the shared trip legality allows it (size, swarm, incorporeal;
        /// CharacterController.CanTrip, PHB p.158, CMB-079).
        /// </summary>
        protected virtual bool IsValidTripTarget(CharacterController target, CharacterController self = null)
        {
            if (target == null)
            {
                Debug.LogWarning("[AI Validation] Trip target is null.");
                return false;
            }

            if (target.HasCondition(CombatConditionType.Prone))
            {
                Debug.Log($"[AI Validation] Cannot trip {target.Stats?.CharacterName ?? "Unknown"} - target is already prone.");
                return false;
            }

            if (self != null && !self.CanTrip(target, out string reason))
            {
                Debug.Log($"[AI Validation] Cannot trip {target.Stats?.CharacterName ?? "Unknown"} - {reason}.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Lowest chance of winning the opposed check at which an AI-run creature trips back after a failed
        /// trip against it (PHB p.158, CMB-079). A failed counter-trip costs nothing by RAW, so the AI declines
        /// only a clearly bad one: about 1 in 10 or worse, a check some 10 or more points weaker.
        /// </summary>
        public const float CounterTripMinimumWinChance = 0.10f;

        /// <summary>
        /// The defender's choice after a failed trip against <paramref name="self"/> (PHB p.158): whether this
        /// AI-run creature tries to trip <paramref name="tripper"/> back. Asked by AIService.ShouldCounterTrip
        /// only when the rule allows it (CharacterController.CanCounterTrip). The default is
        /// <see cref="DefaultShouldCounterTrip"/>; a profile may override it.
        /// </summary>
        public virtual bool ShouldCounterTrip(CharacterController self, CharacterController tripper)
        {
            return DefaultShouldCounterTrip(self, tripper);
        }

        /// <summary>
        /// Default counter-trip choice (CMB-079; owner direction 2026-10-07: counter-trip unless it is clearly
        /// bad). Declines against an ally (TeamUtility.IsAlly: a tripper on this creature's own side, such as a
        /// confused or charmed companion; the rule lets any defender react, so the side is this choice's
        /// concern, owner ruling 2026-10-08, CMB-136); when the reaction would end this creature's invisibility
        /// or Sanctuary (it counts as an attack); or when its chance to win the opposed check is below
        /// <see cref="CounterTripMinimumWinChance"/> (CharacterController.EstimateOpposedCheckWinChance with
        /// the counter-trip modifiers); otherwise trips back.
        /// </summary>
        public static bool DefaultShouldCounterTrip(CharacterController self, CharacterController tripper)
        {
            if (self == null || tripper == null || self.Stats == null || tripper.Stats == null)
                return false;

            if (TeamUtility.IsAlly(self, tripper))
                return false;

            if (self.HasActiveInvisibilityEffect || self.Stats.SanctuaryActive)
                return false;

            float chance = CharacterController.EstimateOpposedCheckWinChance(
                self.GetTripAttackerCheckModifier(),
                tripper.GetTripOrOverrunDefenderCheckModifier());
            return chance >= CounterTripMinimumWinChance;
        }

        /// <summary>
        /// Validates whether target currently has a disarmable held item.
        /// </summary>
        protected virtual bool IsValidDisarmTarget(CharacterController target)
        {
            if (target == null)
            {
                Debug.LogWarning("[AI Validation] Disarm target is null.");
                return false;
            }

            bool hasDisarmable = target.HasDisarmableWeaponEquipped();
            if (!hasDisarmable)
            {
                Debug.Log($"[AI Validation] Cannot disarm {target.Stats?.CharacterName ?? "Unknown"} - no disarmable held weapon.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates whether target currently has a sunderable item equipped.
        /// </summary>
        protected virtual bool IsValidSunderTarget(CharacterController target)
        {
            if (target == null)
            {
                Debug.LogWarning("[AI Validation] Sunder target is null.");
                return false;
            }

            bool hasSunderable = target.HasSunderableItemEquipped();
            if (!hasSunderable)
            {
                Debug.Log($"[AI Validation] Cannot sunder {target.Stats?.CharacterName ?? "Unknown"} - no sunderable equipment.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Shared bull rush legality (size, swarm, incorporeal, grappling, adjacency; PHB p.154),
        /// the same check the PC wrapper uses (CharacterController.CanBullRush, CMB-102).
        /// </summary>
        protected virtual bool IsValidBullRushTarget(CharacterController target, CharacterController self)
        {
            return self != null && self.CanBullRush(target, false, out _);
        }

        /// <summary>
        /// After a successful bull rush (PHB p.154): stay and push the defender 5 ft (follow false,
        /// return 1), or move with it and push 1 to <paramref name="maxIfFollowing"/> squares (follow
        /// true). Asked by AIService.ChooseBullRushPush for a non-controllable attacker; the PC
        /// prompt makes the same choice (CMB-098). The default follows for the maximum.
        /// </summary>
        public virtual int ChooseBullRushPush(CharacterController self, CharacterController target, int maxIfFollowing, out bool follow)
        {
            follow = true;
            return Mathf.Max(1, maxIfFollowing);
        }

        /// <summary>
        /// Which natural attack this creature uses for Haste's extra attack on a full attack (PHB p.239;
        /// owner decision 2026-10-07, CMB-106): a natural-sequence index. Asked by the NPC melee
        /// sequence and the NPC pounce. The default takes the best <see cref="ScoreHasteNaturalAttack"/>.
        /// </summary>
        public virtual int ChooseHasteNaturalAttackIndex(CharacterController self, CharacterController target)
        {
            return NaturalAttackChoice.ChooseBest(self, index => ScoreHasteNaturalAttack(self, target, index));
        }

        /// <summary>
        /// Score of one natural attack as Haste's extra attack (higher is better). The default
        /// (<see cref="NaturalAttackChoice.Score"/>) prefers a rider that matters against the target,
        /// then the highest attack bonus, then the higher average damage.
        /// </summary>
        public virtual float ScoreHasteNaturalAttack(CharacterController self, CharacterController target, int naturalAttackIndex)
        {
            return NaturalAttackChoice.Score(self, target, naturalAttackIndex);
        }

        /// <summary>
        /// Placeholder for future overrun restrictions (size/path rules).
        /// </summary>
        protected virtual bool IsValidOverrunTarget(CharacterController target, CharacterController self)
        {
            return target != null && self != null;
        }

        public virtual SpecialAttackType? GetPreferredManeuver(CharacterController self, CharacterController target)
        {
            if (self == null || target == null)
                return null;

            if (self.IsGrappling() && GrappleBehavior == GrappleBehavior.Maintain)
                return SpecialAttackType.Grapple;

            if (Maneuvers != null)
            {
                if (Maneuvers.AttemptTrip && self.HasMeleeWeaponEquipped() && IsValidTripTarget(target, self))
                    return SpecialAttackType.Trip;

                if (Maneuvers.AttemptDisarm && IsValidDisarmTarget(target))
                    return SpecialAttackType.Disarm;

                if (Maneuvers.AttemptSunder && IsValidSunderTarget(target))
                    return SpecialAttackType.Sunder;

                if (Maneuvers.AttemptBullRush && IsValidBullRushTarget(target, self))
                    return SpecialAttackType.BullRushAttack;

                if (Maneuvers.AttemptOverrun && IsValidOverrunTarget(target, self))
                    return SpecialAttackType.Overrun;
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
