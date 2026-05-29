using System.Collections.Generic;
using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Tactical intelligent undead profile (Ghouls, Ghasts, Wights, Vampire Spawn):
    /// - Prioritize paralysing/draining multiple targets over finishing one
    /// - Coup de grâce helpless (paralysed) enemies when safe
    /// - Ghasts leverage stench aura positioning
    /// - Prefer isolated or lightly-armoured targets vulnerable to natural attacks
    /// - Moderate aggression with flanking awareness
    /// </summary>
    [CreateAssetMenu(fileName = "Undead Tactical AI", menuName = "DND35/AI/Profiles/Undead Tactical")]
    public class UndeadTacticalAIProfile : AIProfile
    {
        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Undead Tactical";

            if (string.IsNullOrWhiteSpace(Description))
                Description = "Intelligent undead that paralyse targets, coup de grâce the helpless, and drain life energy.";

            CombatStyle = CombatStyle.Melee;
            Aggression = 0.8f;
            PrioritizeWounded = false;  // prefer fresh targets to spread paralysis
            PrioritizeIsolated = true;  // easier to paralyse and finish isolated PCs
            SwitchTargetsOften = true;  // spread paralysis across multiple enemies

            if (Movement == null)
                Movement = new MovementPreferences();

            Movement.AvoidAoOs = true;
            Movement.PreferredRangeSquares = 0;
            Movement.MaintainDistance = false;
            Movement.SeekFlanking = true;
            Movement.UseCover = false;

            GrappleBehavior = GrappleBehavior.Avoid; // rely on paralysis, not grappling

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
        /// Target scoring for tactical undead:
        /// 1. Strong bonus for targets NOT already paralysed (spread the effect)
        /// 2. Huge bonus for helpless adjacent targets (coup de grâce opportunity)
        /// 3. Prefer low-Fortitude targets (more likely to fail paralysis/energy drain saves)
        /// 4. Prefer unarmoured/lightly-armoured targets (easier to hit with natural attacks)
        /// 5. Standard proximity bonus for melee
        /// </summary>
        public override float ScoreTarget(CharacterController target, CharacterController self)
        {
            if (target == null || target.Stats == null || target.Stats.IsDead || self == null)
                return float.MinValue;

            float score = 10f;
            int distance = SquareGridUtils.GetDistance(self.GridPosition, target.GridPosition);

            // ── Proximity bonus (melee pressure) ──
            score += Mathf.Max(0, 8 - distance) * 1.2f;

            // ── Helpless target adjacent → coup de grâce is extremely valuable ──
            bool isHelpless = target.HasCondition(CombatConditionType.Helpless)
                           || target.HasCondition(CombatConditionType.Paralyzed);
            if (isHelpless && distance <= 1)
            {
                score += 25f; // massive incentive to execute helpless adjacent targets
            }
            else if (isHelpless)
            {
                // Helpless but not adjacent — still worth moving toward
                score += 8f;
            }
            else
            {
                // Not yet paralysed → prefer spreading paralysis to fresh targets
                score += 6f;
            }

            // ── Low Fortitude targets are more vulnerable to paralysis/energy drain ──
            if (target.Stats != null)
            {
                int fortSave = target.Stats.FortitudeSave;
                if (fortSave <= 2)
                    score += 5f;
                else if (fortSave <= 5)
                    score += 2f;
                else if (fortSave >= 10)
                    score -= 2f;
            }

            // ── Prefer lightly armoured targets (easier to land natural attacks) ──
            if (target.Stats != null)
            {
                int ac = target.Stats.ArmorClass;
                if (ac <= 14)
                    score += 3f;
                else if (ac >= 22)
                    score -= 2f;
            }

            // ── Isolated targets are easier to paralyse and finish ──
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
        /// Tactical undead will coup de grâce paralysed/helpless adjacent enemies.
        /// This is their core combat loop: paralyse → execute.
        /// </summary>
        public override bool ShouldUseCoupDeGrace(CharacterController self)
        {
            return true;
        }

        /// <summary>
        /// Tactical undead don't initiate grapple — they rely on paralysis instead.
        /// Exception: creatures with HasImprovedGrab (Mohrg) use GrapplerAIProfile.
        /// </summary>
        public override bool ShouldInitiateGrapple(CharacterController self, CharacterController target)
        {
            return false;
        }

        /// <summary>
        /// No maneuvers — natural attack paralysis is the primary tactic.
        /// </summary>
        public override SpecialAttackType? GetPreferredManeuver(CharacterController self, CharacterController target)
        {
            return null;
        }

        /// <summary>
        /// Switch to adjacent helpless targets mid-full-attack to maximise damage.
        /// </summary>
        public override bool ShouldSwitchTargetsMidFullAttack(CharacterController self)
        {
            return true;
        }

        /// <summary>
        /// Take 5-ft step to reach a new (potentially helpless) target during full attack.
        /// </summary>
        public override bool ShouldTakeFiveFootStepToContinueFullAttack(CharacterController self)
        {
            return true;
        }

        /// <summary>
        /// Tactical undead are moderately cautious about AoOs but won't hesitate if target is helpless.
        /// </summary>
        public override bool ShouldIgnoreAoO(CharacterController self)
        {
            return false;
        }

        private void EnsureDefaultTags()
        {
            if (TagPriorities == null)
                TagPriorities = new List<TagPriority>();

            if (TagPriorities.Count > 0)
                return;

            // Prefer targets that are easy to paralyse/hit
            TagPriorities.Add(new TagPriority("Armor: Unarmored", 4f));
            TagPriorities.Add(new TagPriority("Armor: Light Armor", 2f));
            TagPriorities.Add(new TagPriority("Armor: Heavy Armor", 3f, true)); // harder to hit
            TagPriorities.Add(new TagPriority("HP State: Disabled", 2f));       // close to death
            TagPriorities.Add(new TagPriority("HP State: Staggered", 3f));      // finish off
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
