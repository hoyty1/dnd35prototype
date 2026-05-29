using System.Collections.Generic;
using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Brute-force intelligent undead profile (Mummies, Bodaks):
    /// - Maximum aggression — advance into melee and never retreat
    /// - Position to maximise aura coverage (Despair, Death Gaze)
    /// - Ignore AoOs — these creatures rely on toughness (DR, high HP)
    /// - Coup de grâce frightened/helpless targets
    /// - No tactical maneuvers — just slam and let auras do the work
    /// </summary>
    [CreateAssetMenu(fileName = "Undead Brute AI", menuName = "DND35/AI/Profiles/Undead Brute")]
    public class UndeadBruteAIProfile : AIProfile
    {
        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Undead Brute";

            if (string.IsNullOrWhiteSpace(Description))
                Description = "Fearsome undead brute that advances relentlessly, using auras to debilitate and melee to destroy.";

            CombatStyle = CombatStyle.Melee;
            Aggression = 1f;             // maximum aggression — never retreats
            PrioritizeWounded = true;    // finish off weakened targets
            PrioritizeIsolated = false;  // actually prefers groups (more aura targets)
            SwitchTargetsOften = false;  // commit to one target until dead

            if (Movement == null)
                Movement = new MovementPreferences();

            Movement.AvoidAoOs = false;  // ignore AoOs — too tough to care
            Movement.PreferredRangeSquares = 0;
            Movement.MaintainDistance = false;
            Movement.SeekFlanking = false; // no finesse, just advance
            Movement.UseCover = false;

            GrappleBehavior = GrappleBehavior.Avoid;

            if (Maneuvers == null)
                Maneuvers = new ManeuverPreferences();

            Maneuvers.AttemptTrip = false;
            Maneuvers.AttemptDisarm = false;
            Maneuvers.AttemptSunder = false;
            Maneuvers.AttemptBullRush = false;
            Maneuvers.AttemptOverrun = false;
            Maneuvers.UsePowerAttack = true; // brutes use Power Attack when available

            EnsureDefaultTags();
        }

        /// <summary>
        /// Target scoring for undead brutes:
        /// 1. Strong proximity bonus — always close to melee range
        /// 2. Prefer clustering near multiple enemies (aura coverage)
        /// 3. Bonus for wounded targets (finish them off)
        /// 4. Bonus for helpless/frightened targets (easy kills / coup de grâce)
        /// 5. Penalty for targets with fire weapons/spells (Mummy vulnerability)
        /// </summary>
        public override float ScoreTarget(CharacterController target, CharacterController self)
        {
            if (target == null || target.Stats == null || target.Stats.IsDead || self == null)
                return float.MinValue;

            float score = 10f;
            int distance = SquareGridUtils.GetDistance(self.GridPosition, target.GridPosition);

            // ── Proximity — always want to close distance ──
            score += Mathf.Max(0, 10 - distance) * 1.5f;

            // ── Helpless/frightened targets — easy kills or coup de grâce ──
            bool isHelpless = target.HasCondition(CombatConditionType.Helpless)
                           || target.HasCondition(CombatConditionType.Paralyzed);
            bool isFrightened = target.HasCondition(CombatConditionType.Frightened)
                             || target.HasCondition(CombatConditionType.Panicked)
                             || target.HasCondition(CombatConditionType.Shaken);

            if (isHelpless && distance <= 1)
                score += 20f; // coup de grâce opportunity
            else if (isHelpless)
                score += 8f;

            if (isFrightened)
                score += 4f; // frightened targets have penalties, easier to kill

            // ── Wounded bonus — finish off weakened enemies ──
            if (target.Stats.TotalMaxHP > 0)
            {
                float hpPct = Mathf.Clamp01((float)target.Stats.CurrentHP / target.Stats.TotalMaxHP);
                score += (1f - hpPct) * 10f;
            }

            // ── Cluster scoring — prefer targets near other enemies for aura coverage ──
            int nearbyEnemies = CountNearbyEnemies(self, target.GridPosition, 6); // 30 ft aura
            score += nearbyEnemies * 2f;

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
        /// Brute undead will absolutely execute helpless targets.
        /// </summary>
        public override bool ShouldUseCoupDeGrace(CharacterController self)
        {
            return true;
        }

        /// <summary>
        /// Brutes ignore AoOs — they are too tough to care.
        /// </summary>
        public override bool ShouldIgnoreAoO(CharacterController self)
        {
            return true;
        }

        /// <summary>
        /// Brutes prefer charging when possible to close distance fast.
        /// </summary>
        public override bool ShouldPreferCharge(CharacterController self, CharacterController target, int distanceSquares, bool preferAggression)
        {
            // Always charge if legal — maximise reach speed for aura deployment
            return true;
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
            return false; // commit to current target
        }

        public override bool ShouldTakeFiveFootStepToContinueFullAttack(CharacterController self)
        {
            return false;
        }

        private void EnsureDefaultTags()
        {
            if (TagPriorities == null)
                TagPriorities = new List<TagPriority>();

            if (TagPriorities.Count > 0)
                return;

            TagPriorities.Add(new TagPriority("HP State: Staggered", 5f));
            TagPriorities.Add(new TagPriority("HP State: Disabled", 4f));
            TagPriorities.Add(new TagPriority("Armor: Unarmored", 2f));
        }

        /// <summary>
        /// Count enemies near a position (for aura coverage scoring).
        /// </summary>
        private static int CountNearbyEnemies(CharacterController self, Vector2Int position, int rangeSq)
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
                if (c == null || c.Stats == null || c.Stats.IsDead)
                    continue;

                if (!gm.IsEnemyTeamForAI(self, c))
                    continue;

                int dist = SquareGridUtils.GetDistance(position, c.GridPosition);
                if (dist <= rangeSq)
                    count++;
            }

            return count;
        }
    }
}
