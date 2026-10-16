using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Base profile for spellcasters with school priorities and ally-aware AoE logic.
    /// </summary>
    [CreateAssetMenu(fileName = "Spellcaster AI", menuName = "DND35/AI/Profiles/Spellcaster")]
    public class SpellcasterAIProfile : AIProfile
    {
        [Header("Spellcaster")]
        public List<SpellSchoolPriority> SchoolPriorities = new List<SpellSchoolPriority>();

        public AOECastingPreferences AOECasting = new AOECastingPreferences();
        public SpellSelectionPreferences SpellSelection = new SpellSelectionPreferences();

        [Range(1, 12)]
        public int PreferredCastingDistanceSquares = 6;

        [Range(0f, 1f)]
        public float FleeHealthThreshold = 0.25f;

        protected virtual void OnEnable()
        {
            if (Movement == null)
                Movement = new MovementPreferences();

            Movement.AvoidAoOs = true;
            Movement.PreferredRangeSquares = PreferredCastingDistanceSquares;
            Movement.MaintainDistance = true;
            Movement.SeekFlanking = false;
            Movement.UseCover = true;

            if (SchoolPriorities == null)
                SchoolPriorities = new List<SpellSchoolPriority>();
        }

        public float GetSchoolPriority(SpellSchool school)
        {
            if (SchoolPriorities == null || SchoolPriorities.Count == 0)
                return 1f;

            SpellSchoolPriority entry = SchoolPriorities.FirstOrDefault(p => p != null && p.School == school);
            return entry != null ? Mathf.Max(0f, entry.Priority) : 1f;
        }

        public virtual float ScoreSpell(
            SpellData spell,
            CharacterController caster,
            CharacterController primaryTarget,
            List<CharacterController> allCombatants,
            GameManager gameManager)
        {
            if (spell == null)
                return float.MinValue;

            float score = 0f;

            // ── School preference (profile-driven) ──
            SpellSchool school = SpellSchoolUtils.Parse(spell.School);
            score += GetSchoolPriority(school) * 10f;

            // ── AoE evaluation (profile-driven) ──
            bool isAoe = spell.TargetType == SpellTargetType.Area || spell.AoEShapeType != AoEShape.None;

            if (isAoe)
            {
                if (AOECasting.PreferSingleTarget)
                    score -= 5f;

                SpellAOEEvaluation aoeEval = EvaluateAOECast(spell, caster, primaryTarget, allCombatants, gameManager);
                score += aoeEval.EnemiesHit * 3f;
                score -= aoeEval.EffectiveAllyCasualties * 10f;

                if (aoeEval.EnemiesHit < AOECasting.MinimumEnemiesInAOE)
                    score -= 6f;

                if (!aoeEval.IsSafe)
                    score -= 1000f;
            }
            else
            {
                if (AOECasting.PreferSingleTarget)
                    score += 4f;
            }

            // ── Buff-already-active check ──
            if ((spell.EffectType == SpellEffectType.Buff || spell.EffectType == SpellEffectType.Illusion)
                && caster != null && caster.Stats != null)
            {
                StatusEffectManager statusMgr = caster.StatusEffectManager;
                if (statusMgr != null && statusMgr.HasEffect(spell.SpellId))
                {
                    int remaining = statusMgr.GetRemainingRounds(spell.SpellId);
                    if (remaining > 1 || remaining == -1)
                    {
                        score -= 1000f;
                        return score;
                    }
                }

                if (SpellSelection.BuffBeforeDamage)
                {
                    float hpPct = caster.Stats.TotalMaxHP > 0 ? (float)caster.Stats.CurrentHP / caster.Stats.TotalMaxHP : 1f;
                    if (hpPct > 0.65f)
                        score += 4f;
                }
            }

            // ── Profile-driven preferences ──
            if (SpellSelection.ConserveHighLevelSpells && spell.SpellLevel >= 3)
            {
                int casterLevel = caster != null && caster.Stats != null ? caster.Stats.Level : 1;
                bool targetIsHighThreat = primaryTarget != null && primaryTarget.Stats != null
                    && (primaryTarget.Stats.IsWizard || primaryTarget.Stats.IsCleric || primaryTarget.Stats.Level >= casterLevel);
                score += targetIsHighThreat ? -1f : -4f;
            }

            if (SpellSelection.CounterEnemySpells && primaryTarget != null && primaryTarget.Stats != null
                && (primaryTarget.Stats.IsWizard || primaryTarget.Stats.IsCleric)
                && (spell.EffectType == SpellEffectType.Debuff || spell.EffectType == SpellEffectType.Control))
            {
                score += 2f;
            }

            if (!SpellSelection.UseUtilitySpells &&
                (spell.EffectType == SpellEffectType.Utility || spell.EffectType == SpellEffectType.Divination))
                score -= 5f;

            // ── Comprehensive AI Strategist scoring (Tiers 1–4) ──
            score += AISpellcastingStrategist.ScoreSpellComprehensive(
                spell, caster, primaryTarget, allCombatants, gameManager, this);

            return score;
        }

        /// <summary>
        /// How an area cast of <paramref name="spell"/> aimed at <paramref name="primaryTarget"/> would land: the best of the
        /// aims the caster has at that creature (batch 11 step 24, decision D5; AoESystem.GetCandidateAimsAt, the same choices
        /// the PC cursor offers): a burst or cube at each intersection of the target's space (the four corners of a Small or
        /// Medium creature's square) within range, a cone or line from each corner of the caster's space toward it, a
        /// self-centred area from each of the caster's creature-centred origins, a one-square effect on the target's square.
        /// The best is a safe aim first, then the most enemies, then the fewest ally casualties, then the first found.
        /// Each footprint keeps only the squares its origin has line of effect to, and a burst's or cube's origin must be in
        /// the caster's line of effect (AoESystem.GetCandidateAimsAt, the cast's own tests); a creature counts when any
        /// square of its space is in the footprint (AoESystem.IsCreatureInCells). Ally sparing (AOECasting) is unchanged.
        /// </summary>
        public virtual SpellAOEEvaluation EvaluateAOECast(
            SpellData spell,
            CharacterController caster,
            CharacterController primaryTarget,
            List<CharacterController> allCombatants,
            GameManager gameManager)
        {
            if (spell == null || caster == null || primaryTarget == null || gameManager == null || gameManager.Grid == null)
                return new SpellAOEEvaluation(false, 0, 0, 999f);

            List<HashSet<Vector2Int>> footprints = CalculateCandidateFootprints(spell, caster, primaryTarget, gameManager);
            bool found = false;
            SpellAOEEvaluation best = new SpellAOEEvaluation(false, 0, 0, 999f);
            foreach (HashSet<Vector2Int> cells in footprints)
            {
                if (cells == null || cells.Count == 0)
                    continue;
                SpellAOEEvaluation eval = EvaluateFootprint(spell, caster, cells, allCombatants, gameManager);
                if (!found || IsBetterAim(eval, best))
                {
                    best = eval;
                    found = true;
                }
            }
            return best;
        }

        private static bool IsBetterAim(SpellAOEEvaluation a, SpellAOEEvaluation b)
        {
            if (a.IsSafe != b.IsSafe)
                return a.IsSafe;
            if (a.EnemiesHit != b.EnemiesHit)
                return a.EnemiesHit > b.EnemiesHit;
            return a.EffectiveAllyCasualties < b.EffectiveAllyCasualties;
        }

        private SpellAOEEvaluation EvaluateFootprint(SpellData spell, CharacterController caster, HashSet<Vector2Int> cells,
            List<CharacterController> allCombatants, GameManager gameManager)
        {
            int enemiesHit = 0;
            int alliesHit = 0;
            float effectiveCasualties = 0f;
            int estimatedDamage = EstimateAverageDamage(spell, caster);
            DamageType damageType = DamageTextUtils.ParseSingleDamageType(spell.DamageType);

            if (allCombatants == null)
                allCombatants = new List<CharacterController>();

            for (int i = 0; i < allCombatants.Count; i++)
            {
                CharacterController candidate = allCombatants[i];
                if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                    continue;

                // Any square of its space, as the area's resolution counts it (AoESystem.GetTargetsInArea; a Large ally
                // covered only by its other squares is still hit; batch 11 step 24 review).
                if (!AoESystem.IsCreatureInCells(cells, candidate))
                    continue;

                bool isEnemy = gameManager.IsEnemyTeamForAI(caster, candidate);
                if (isEnemy)
                {
                    enemiesHit++;
                    continue;
                }

                if (candidate == caster && spell.AoEFilter == AoETargetFilter.EnemiesOnly)
                    continue;

                alliesHit++;

                if (!AOECasting.AvoidHittingAllies)
                    continue;

                float casualty = GetAllyCasualtyWeight(candidate, damageType, estimatedDamage);
                effectiveCasualties += casualty;
            }

            bool safe = !AOECasting.AvoidHittingAllies || effectiveCasualties <= AOECasting.MaxAcceptableAllyCasualties;
            return new SpellAOEEvaluation(safe, enemiesHit, alliesHit, effectiveCasualties);
        }

        /// <summary>The footprints of every aim the caster has at <paramref name="primaryTarget"/> (see <see cref="EvaluateAOECast"/>).</summary>
        private static List<HashSet<Vector2Int>> CalculateCandidateFootprints(SpellData spell, CharacterController caster,
            CharacterController primaryTarget, GameManager gameManager)
        {
            var footprints = new List<HashSet<Vector2Int>>();
            SquareGrid grid = gameManager.Grid;
            AoEShape shape = spell.AoEShapeType;
            int size = spell.AoESizeSquares;
            if (shape == AoEShape.None && spell.TargetType == SpellTargetType.Area)
            {
                // Area-target spells without shape metadata: a burst of their area radius (no shipped spell; AreaRoutingTests).
                shape = AoEShape.Burst;
                size = spell.AreaRadius;
            }

            if (AoESystem.IsSingleSquarePlacement(spell))
            {
                footprints.Add(new HashSet<Vector2Int> { primaryTarget.GridPosition });
                return footprints;
            }

            int casterSize = caster.GetCurrentSpaceSquares();
            int range = spell.AoERangeSquares > 0 ? spell.AoERangeSquares : CasterLevelRules.RangeSquares(caster, spell);
            if (shape == AoEShape.Burst && range <= 0)
            {
                // Centred on the caster: one footprint per creature-centred origin (the caster's choice of corner).
                // Each keeps the squares its origin has line of effect to, as the cast's Legality stage filters them.
                foreach (GridIntersection origin in AoESystem.GetCreatureCentredOrigins(caster))
                {
                    HashSet<Vector2Int> cells = AoESystem.GetCreatureCentredBurstCells(caster.GridPosition, casterSize, origin, size, grid);
                    AoESystem.FilterCellsByLineOfEffect(cells, origin);
                    footprints.Add(cells);
                }
                return footprints;
            }

            if (AoESystem.IsPlacedShape(shape) || shape == AoEShape.Cone || shape == AoEShape.Line)
            {
                foreach (AreaAim aim in AoESystem.GetCandidateAimsAt(shape, Mathf.Max(1, size), caster.GridPosition, casterSize,
                             primaryTarget.GridPosition, primaryTarget.GetCurrentSpaceSquares(), range, grid))
                    footprints.Add(aim.Cells);
                return footprints;
            }

            footprints.Add(new HashSet<Vector2Int> { primaryTarget.GridPosition });
            return footprints;
        }

        private float GetAllyCasualtyWeight(CharacterController ally, DamageType damageType, int estimatedDamage)
        {
            if (ally == null || ally.Stats == null)
                return 0f;

            bool immune = ally.Stats.DamageImmunities != null && ally.Stats.DamageImmunities.Contains(damageType);
            if (immune && AOECasting.IgnoreImmuneAllies)
                return 0f;

            bool resistant = false;
            if (AOECasting.ConsiderAllyResistances && ally.Stats.DamageResistances != null)
            {
                resistant = ally.Stats.DamageResistances.Any(r => r != null && r.Type == damageType && r.Amount > 0);
            }

            float damageMultiplier = resistant ? 0.5f : 1f;
            float effectiveDamage = estimatedDamage * damageMultiplier;
            float hpPct = ally.Stats.TotalMaxHP > 0 ? effectiveDamage / ally.Stats.TotalMaxHP : 1f;

            if (hpPct <= AOECasting.AcceptableAllyDamagePercent)
                return 0f;

            if (resistant && AOECasting.ResistantAlliesCountHalf)
                return 0.5f;

            return 1f;
        }

        /// <summary>Average damage at the caster's caster level (SpellDiceRules, SPL-005).</summary>
        private static int EstimateAverageDamage(SpellData spell, CharacterController caster)
        {
            if (spell == null)
                return 0;

            int casterLevel = SpellDiceRules.CasterLevelFor(caster != null ? caster.Stats : null, spell);
            int diceAverage = Mathf.FloorToInt(SpellDiceRules.Damage(spell, casterLevel).Average);

            int missileAverage = 0;
            if (spell.AutoHit && spell.MissileCount > 0)
                missileAverage = spell.MissileCount * ((spell.DamageDice + 1) / 2 + spell.BonusDamage);

            return Mathf.Max(diceAverage, missileAverage);
        }

        public override float ScoreTarget(CharacterController target, CharacterController self)
        {
            float score = base.ScoreTarget(target, self);
            if (target == null || target.Stats == null)
                return score;

            if (target.Stats.IsWizard || target.Stats.IsCleric)
                score += 4f;

            return score;
        }

        public override bool ShouldUseCoupDeGrace(CharacterController self)
        {
            // Spellcasters typically prefer casting/control over spending a full-round execution action.
            return false;
        }

        public override float GetRangedAoORiskToleranceMultiplier()
        {
            // Spellcasters are generally more conservative about provoking while threatened.
            return 0.75f;
        }
    }
}
