using System.Collections.Generic;
using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Incorporeal undead profile (Shadow, Wraith, Spectre, Ghost, Allip):
    /// - Prioritize targets with low touch AC (incorporeal touch attacks target touch AC)
    /// - Score targets by vulnerability to ability drain (low STR for shadows, etc.)
    /// - High aggression — 50% miss chance from incorporeality provides natural survivability
    /// - Ignore AoOs — many corporeal attacks miss anyway
    /// - Prefer isolated targets to drain without interference
    /// - Never grapple (incorporeal can't grapple corporeal creatures)
    /// </summary>
    [CreateAssetMenu(fileName = "Undead Incorporeal AI", menuName = "DND35/AI/Profiles/Undead Incorporeal")]
    public class UndeadIncorporealAIProfile : AIProfile
    {
        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Undead Incorporeal";

            if (string.IsNullOrWhiteSpace(Description))
                Description = "Incorporeal undead that phases through defences, draining ability scores with touch attacks.";

            CombatStyle = CombatStyle.Melee;
            Aggression = 0.9f;           // very aggressive — incorporeality protects them
            PrioritizeWounded = false;   // prefer spreading drain across targets
            PrioritizeIsolated = true;   // easier to drain isolated PCs
            SwitchTargetsOften = true;   // switch to spread drain / hit more vulnerable targets

            if (Movement == null)
                Movement = new MovementPreferences();

            Movement.AvoidAoOs = false;  // incorporeal — most AoOs miss anyway
            Movement.PreferredRangeSquares = 0;
            Movement.MaintainDistance = false;
            Movement.SeekFlanking = false; // touch attacks don't need flanking advantage as much
            Movement.UseCover = false;     // incorporeal — don't need cover

            GrappleBehavior = GrappleBehavior.Avoid; // can't grapple corporeal

            if (Maneuvers == null)
                Maneuvers = new ManeuverPreferences();

            Maneuvers.AttemptTrip = false;
            Maneuvers.AttemptDisarm = false;
            Maneuvers.AttemptSunder = false;
            Maneuvers.AttemptBullRush = false;
            Maneuvers.AttemptOverrun = false;
            Maneuvers.UsePowerAttack = false;

            EnsureDefaultTags();
        }

        /// <summary>
        /// Target scoring for incorporeal undead:
        /// 1. Prefer targets with low touch AC (incorporeal touch attacks vs touch AC)
        /// 2. Prefer targets already weakened by ability drain (finish what we started)
        /// 3. Prefer casters (often low touch AC, high-value targets)
        /// 4. Proximity bonus (standard melee)
        /// 5. Penalty for targets with ghost touch weapons or force effects
        /// </summary>
        public override float ScoreTarget(CharacterController target, CharacterController self)
        {
            if (target == null || target.Stats == null || target.Stats.IsDead || self == null)
                return float.MinValue;

            float score = 10f;
            int distance = SquareGridUtils.GetDistance(self.GridPosition, target.GridPosition);

            // ── Proximity bonus ──
            score += Mathf.Max(0, 8 - distance) * 1.0f;

            // ── Touch AC targeting — lower touch AC = much easier to hit ──
            if (target.Stats != null)
            {
                int touchAC = target.Stats.TouchArmorClass;
                // Touch AC typically ranges 10-15; lower is better for us
                if (touchAC <= 11)
                    score += 8f;
                else if (touchAC <= 13)
                    score += 4f;
                else if (touchAC <= 15)
                    score += 1f;
                else
                    score -= 2f; // high Dex monks etc. are harder targets
            }

            // ── Prefer targets already suffering ability damage (finish the drain) ──
            if (target.Stats != null)
            {
                int totalDrain = target.Stats.GetAbilityDrain(AbilityType.STR)
                              + target.Stats.GetAbilityDrain(AbilityType.DEX)
                              + target.Stats.GetAbilityDrain(AbilityType.CON)
                              + target.Stats.GetAbilityDrain(AbilityType.WIS)
                              + target.Stats.GetAbilityDrain(AbilityType.CHA);
                if (totalDrain > 0)
                    score += Mathf.Min(totalDrain * 1.5f, 8f);
            }

            // ── Prefer spellcasters (high value, often low touch AC) ──
            if (target.Stats != null && (target.Stats.IsWizard || target.Stats.IsCleric))
                score += 3f;

            // ── Isolated targets ──
            if (PrioritizeIsolated)
            {
                int adjacentAllies = CountAdjacentAlliesOf(target);
                if (adjacentAllies == 0)
                    score += 4f;
                else
                    score -= adjacentAllies * 1f;
            }

            // ── Tag priorities ──
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

            return score;
        }

        /// <summary>
        /// Incorporeal undead ignore AoOs — 50% miss chance means most attacks miss.
        /// </summary>
        public override bool ShouldIgnoreAoO(CharacterController self)
        {
            return true;
        }

        /// <summary>
        /// Incorporeal undead don't coup de grâce — they drain ability scores instead.
        /// </summary>
        public override bool ShouldUseCoupDeGrace(CharacterController self)
        {
            return false;
        }

        public override bool ShouldInitiateGrapple(CharacterController self, CharacterController target)
        {
            return false; // incorporeal can't grapple
        }

        public override SpecialAttackType? GetPreferredManeuver(CharacterController self, CharacterController target)
        {
            return null;
        }

        /// <summary>
        /// Switch targets to spread drain across multiple enemies.
        /// </summary>
        public override bool ShouldSwitchTargetsMidFullAttack(CharacterController self)
        {
            return true;
        }

        public override bool ShouldTakeFiveFootStepToContinueFullAttack(CharacterController self)
        {
            return true;
        }

        /// <summary>
        /// Incorporeal undead don't need to ignore unconscious targets — they may still
        /// drain ability scores from dying creatures to create spawn.
        /// </summary>
        public override bool ShouldIgnoreUnconsciousTargets(CharacterController self)
        {
            return false;
        }

        private void EnsureDefaultTags()
        {
            if (TagPriorities == null)
                TagPriorities = new List<TagPriority>();

            if (TagPriorities.Count > 0)
                return;

            // Touch AC correlates with armour type inversely
            TagPriorities.Add(new TagPriority("Armor: Heavy Armor", 3f));  // heavy armour = low touch AC!
            TagPriorities.Add(new TagPriority("Armor: Unarmored", 2f, true)); // unarmoured = higher touch AC (Dex)
            TagPriorities.Add(new TagPriority("HP State: Staggered", 3f));
            TagPriorities.Add(new TagPriority("HP State: Disabled", 2f));
        }

        private static int CountAdjacentAlliesOf(CharacterController target)
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
