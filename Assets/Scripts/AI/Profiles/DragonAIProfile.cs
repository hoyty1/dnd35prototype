using System.Collections.Generic;
using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Tactical dragon AI profile with breath weapon optimization and intelligent
    /// priority targeting. Suitable for dragons, hell hounds, and other creatures
    /// with breath weapons and/or innate spellcasting.
    ///
    /// Priority order:
    /// 1. Cast buff spells (if not in melee range)
    /// 2. Cast attack spells (if not in melee range)
    /// 3. Avoid attacks of opportunity
    /// 4. Prioritize healers/mages (unless a closer target exists)
    /// 5. Use breath weapon when available to hit the most targets
    /// 6. Full melee attack as last resort
    /// </summary>
    [CreateAssetMenu(fileName = "Dragon AI", menuName = "DND35/AI/Profiles/Dragon")]
    public class DragonAIProfile : AIProfile
    {
        // ── Targeting ──────────────────────────────────────────────────
        [Header("Dragon Targeting")]
        [Tooltip("Extra score for caster/healer targets (Class: Wizard, Cleric, Sorcerer, Druid).")]
        [Range(0f, 10f)] public float CasterTargetBonus = 5f;

        [Tooltip("Per-square distance penalty — closer targets are weighted higher.")]
        [Range(0f, 4f)] public float DistanceWeight = 1.2f;

        [Tooltip("Bonus for wounded targets (HP < 50%).")]
        [Range(0f, 6f)] public float WoundedTargetBonus = 2f;

        // ── Breath Weapon ──────────────────────────────────────────────
        [Header("Breath Weapon")]
        [Tooltip("Minimum enemies the breath weapon must hit to be worth using (when alternative melee exists).")]
        [Range(1, 6)] public int MinEnemiesForBreath = 1;

        [Tooltip("Strongly prefer breath weapon when it can hit this many or more enemies.")]
        [Range(2, 8)] public int PreferBreathThreshold = 2;

        [Tooltip("Maximum acceptable allies hit by breath weapon (0 = never hit allies).")]
        [Range(0, 4)] public int MaxAcceptableAllyHits = 0;

        // ── Spellcasting ───────────────────────────────────────────────
        [Header("Spellcasting")]
        [Tooltip("Maximum melee-adjacent enemies before the dragon gives up on casting and just fights.")]
        [Range(0, 4)] public int MaxMeleeEnemiesBeforeCasting = 0;

        // ── Internal ───────────────────────────────────────────────────
        /// <summary>
        /// When set by the AI turn logic, indicates the dragon wants to use its
        /// breath weapon this turn (and has already selected the best aim target).
        /// Reset each turn by AIService.
        /// </summary>
        [System.NonSerialized] public bool WantsToUseBreathWeapon;
        [System.NonSerialized] public CharacterController BreathWeaponAimTarget;
        [System.NonSerialized] public int BreathWeaponExpectedHits;

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Dragon";

            if (string.IsNullOrWhiteSpace(Description))
                Description = "Tactical dragon AI: breath weapon optimization, spell priority, caster targeting.";

            CombatStyle = CombatStyle.Mixed;
            Aggression = 0.8f;
            PrioritizeWounded = true;
            PrioritizeIsolated = false;
            SwitchTargetsOften = false;

            if (Movement == null)
                Movement = new MovementPreferences();

            // Dragons are intelligent — they avoid provoking AoOs.
            Movement.AvoidAoOs = true;
            Movement.PreferredRangeSquares = 2; // Prefer slight distance to maximize breath weapon use
            Movement.MaintainDistance = false;   // Will close to melee when needed
            Movement.SeekFlanking = false;
            Movement.UseCover = false;

            if (Maneuvers == null)
                Maneuvers = new ManeuverPreferences();

            Maneuvers.AttemptTrip = false;
            Maneuvers.AttemptDisarm = false;
            Maneuvers.AttemptSunder = false;
            Maneuvers.AttemptBullRush = false;
            Maneuvers.AttemptOverrun = false;
            Maneuvers.UsePowerAttack = true; // Dragons have high Str, power attack is efficient.
        }

        // ── Target Scoring ─────────────────────────────────────────────

        public override float ScoreTarget(CharacterController target, CharacterController self)
        {
            float score = base.ScoreTarget(target, self);
            if (score == float.MinValue || self == null || target == null || target.Stats == null)
                return score;

            // Distance: prefer closer targets
            int distance = SquareGridUtils.GetDistance(self.GridPosition, target.GridPosition);
            float distanceBonus = Mathf.Max(0, 10 - distance) * DistanceWeight;
            score += distanceBonus;

            // Prioritize casters/healers (unless they're really far away)
            if (IsCasterOrHealer(target))
            {
                // Full bonus up to 6 squares away, reduced beyond that
                float rangeFactor = distance <= 6 ? 1f : Mathf.Max(0f, 1f - (distance - 6) * 0.15f);
                score += CasterTargetBonus * rangeFactor;
            }

            // Wounded bonus
            float hpPct = target.Stats.TotalMaxHP > 0
                ? (float)target.Stats.CurrentHP / target.Stats.TotalMaxHP
                : 1f;
            if (hpPct < 0.5f)
                score += WoundedTargetBonus;

            return score;
        }

        // ── Breath Weapon Evaluation ───────────────────────────────────

        /// <summary>
        /// Evaluates breath weapon usage. Returns true if the dragon should use its
        /// breath weapon this turn, and sets <see cref="BreathWeaponAimTarget"/> to
        /// the best direction anchor.
        /// </summary>
        public bool EvaluateBreathWeapon(
            CharacterController self,
            List<CharacterController> allCombatants,
            GameManager gameManager)
        {
            WantsToUseBreathWeapon = false;
            BreathWeaponAimTarget = null;
            BreathWeaponExpectedHits = 0;

            if (self == null || !self.HasBreathWeapon || !self.IsBreathWeaponReady)
                return false;

            BreathWeaponDefinition bw = self.GetBreathWeaponDefinition();
            if (bw == null)
                return false;

            int rangeSquares = Mathf.Max(1, bw.RangeFeet / 5);

            // Gather enemy targets
            var enemies = new List<CharacterController>();
            var allies = new List<CharacterController>();
            for (int i = 0; i < allCombatants.Count; i++)
            {
                CharacterController c = allCombatants[i];
                if (c == null || c.Stats == null || c.Stats.IsDead || c == self)
                    continue;

                if (gameManager.IsEnemyTeamForAI(self, c))
                    enemies.Add(c);
                else
                    allies.Add(c);
            }

            if (enemies.Count == 0)
                return false;

            // For each enemy, simulate aiming the breath weapon at them and count hits.
            CharacterController bestTarget = null;
            int bestEnemyHits = 0;
            int bestAllyHits = int.MaxValue;

            for (int e = 0; e < enemies.Count; e++)
            {
                CharacterController aimTarget = enemies[e];
                HashSet<Vector2Int> cells = GetBreathCells(self, aimTarget, bw, rangeSquares, gameManager);
                if (cells == null || cells.Count == 0)
                    continue;

                int enemyHits = 0;
                int allyHits = 0;

                for (int i = 0; i < enemies.Count; i++)
                {
                    if (cells.Contains(enemies[i].GridPosition))
                        enemyHits++;
                }

                for (int i = 0; i < allies.Count; i++)
                {
                    if (cells.Contains(allies[i].GridPosition))
                        allyHits++;
                }

                // Skip configurations that hit too many allies
                if (allyHits > MaxAcceptableAllyHits)
                    continue;

                // Pick best: most enemies, fewest allies, then closest aim target
                bool isBetter = enemyHits > bestEnemyHits
                    || (enemyHits == bestEnemyHits && allyHits < bestAllyHits);

                if (isBetter)
                {
                    bestTarget = aimTarget;
                    bestEnemyHits = enemyHits;
                    bestAllyHits = allyHits;
                }
            }

            if (bestTarget == null || bestEnemyHits < MinEnemiesForBreath)
                return false;

            WantsToUseBreathWeapon = true;
            BreathWeaponAimTarget = bestTarget;
            BreathWeaponExpectedHits = bestEnemyHits;

            Debug.Log($"[AI][Dragon] {self.Stats.CharacterName} plans breath weapon aimed at {bestTarget.Stats.CharacterName} " +
                      $"(expected hits: {bestEnemyHits} enemies, {bestAllyHits} allies)");

            return true;
        }

        /// <summary>
        /// Returns true if the dragon is in melee with too many enemies to safely cast spells.
        /// </summary>
        public bool IsTooCloseForCasting(CharacterController self, List<CharacterController> allCombatants, GameManager gameManager)
        {
            if (self == null || allCombatants == null || gameManager == null)
                return true;

            int meleeEnemies = 0;
            for (int i = 0; i < allCombatants.Count; i++)
            {
                CharacterController c = allCombatants[i];
                if (c == null || c.Stats == null || c.Stats.IsDead || c == self)
                    continue;

                if (!gameManager.IsEnemyTeamForAI(self, c))
                    continue;

                int dist = SquareGridUtils.GetDistance(self.GridPosition, c.GridPosition);
                if (dist <= 1) // Adjacent = melee range
                    meleeEnemies++;
            }

            return meleeEnemies > MaxMeleeEnemiesBeforeCasting;
        }

        // ── Overrides ──────────────────────────────────────────────────

        public override bool ShouldIgnoreAoO(CharacterController self)
        {
            // Dragons are smart — never ignore AoO risk.
            return false;
        }

        public override bool ShouldSwitchTargetsMidFullAttack(CharacterController self)
        {
            // Intelligent creatures switch to better targets mid-full-attack.
            return true;
        }

        public override bool ShouldTakeFiveFootStepToContinueFullAttack(CharacterController self)
        {
            return true;
        }

        public override bool ShouldUseCoupDeGrace(CharacterController self)
        {
            // Dragons are too tactical to waste a full-round on a helpless foe
            // when active threats remain.
            return false;
        }

        public override bool ShouldPreferCharge(CharacterController self, CharacterController target, int distanceSquares, bool preferAggression)
        {
            // Only charge if the dragon has no breath weapon ready and target is at moderate range.
            if (self != null && self.HasBreathWeapon && self.IsBreathWeaponReady)
                return false; // Prefer breath weapon over charge

            return distanceSquares >= 3 && distanceSquares <= 8;
        }

        public override float GetRangedAoORiskToleranceMultiplier()
        {
            // Dragons are tough but smart — moderate risk tolerance for ranged/breath.
            return 0.85f;
        }

        // ── Helpers ────────────────────────────────────────────────────

        private static HashSet<Vector2Int> GetBreathCells(
            CharacterController self,
            CharacterController aimTarget,
            BreathWeaponDefinition bw,
            int rangeSquares,
            GameManager gameManager)
        {
            if (gameManager == null || gameManager.Grid == null)
                return null;

            switch (bw.Shape)
            {
                case BreathWeaponShape.Cone:
                    return AoESystem.GetConeCells(
                        self.GridPosition,
                        aimTarget.GridPosition,
                        rangeSquares,
                        gameManager.Grid);

                case BreathWeaponShape.Line:
                    return AoESystem.GetLineCellsToTarget(
                        self.GridPosition,
                        aimTarget.GridPosition,
                        rangeSquares,
                        gameManager.Grid);

                default:
                    return null;
            }
        }

        private static bool IsCasterOrHealer(CharacterController target)
        {
            if (target == null || target.Stats == null)
                return false;

            return target.Stats.IsWizard
                || target.Stats.IsCleric
                || target.Stats.HasClass("Sorcerer")
                || target.Stats.HasClass("Druid");
        }
    }
}
