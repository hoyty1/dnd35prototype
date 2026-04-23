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

            // School preference
            SpellSchool school = SpellSchoolUtils.Parse(spell.School);
            score += GetSchoolPriority(school) * 10f;

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

            if (SpellSelection.BuffBeforeDamage && spell.EffectType == SpellEffectType.Buff && caster != null && caster.Stats != null)
            {
                float hpPct = caster.Stats.TotalMaxHP > 0 ? (float)caster.Stats.CurrentHP / caster.Stats.TotalMaxHP : 1f;
                if (hpPct > 0.65f)
                    score += 4f;
            }

            if (SpellSelection.ConserveHighLevelSpells && spell.SpellLevel >= 3)
            {
                int casterLevel = caster != null && caster.Stats != null ? caster.Stats.Level : 1;
                bool targetIsHighThreat = primaryTarget != null && primaryTarget.Stats != null
                    && (primaryTarget.Stats.IsWizard || primaryTarget.Stats.IsCleric || primaryTarget.Stats.Level >= casterLevel);
                score += targetIsHighThreat ? -1f : -4f;
            }

            if (SpellSelection.CounterEnemySpells && primaryTarget != null && primaryTarget.Stats != null
                && (primaryTarget.Stats.IsWizard || primaryTarget.Stats.IsCleric)
                && spell.EffectType == SpellEffectType.Debuff)
            {
                score += 2f;
            }

            if (!SpellSelection.UseUtilitySpells && spell.EffectType == SpellEffectType.Buff && spell.DamageCount <= 0 && spell.HealCount <= 0)
                score -= 5f;

            return score;
        }

        public virtual SpellAOEEvaluation EvaluateAOECast(
            SpellData spell,
            CharacterController caster,
            CharacterController primaryTarget,
            List<CharacterController> allCombatants,
            GameManager gameManager)
        {
            if (spell == null || caster == null || primaryTarget == null || gameManager == null || gameManager.Grid == null)
                return new SpellAOEEvaluation(false, 0, 0, 999f);

            HashSet<Vector2Int> cells = CalculateSpellCells(spell, caster, primaryTarget, gameManager);
            if (cells == null || cells.Count == 0)
                return new SpellAOEEvaluation(false, 0, 0, 999f);

            int enemiesHit = 0;
            int alliesHit = 0;
            float effectiveCasualties = 0f;
            int estimatedDamage = EstimateAverageDamage(spell);
            DamageType damageType = DamageTextUtils.ParseSingleDamageType(spell.DamageType);

            if (allCombatants == null)
                allCombatants = new List<CharacterController>();

            for (int i = 0; i < allCombatants.Count; i++)
            {
                CharacterController candidate = allCombatants[i];
                if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                    continue;

                if (!cells.Contains(candidate.GridPosition))
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

        private HashSet<Vector2Int> CalculateSpellCells(SpellData spell, CharacterController caster, CharacterController primaryTarget, GameManager gameManager)
        {
            switch (spell.AoEShapeType)
            {
                case AoEShape.Burst:
                    return AoESystem.GetBurstCells(primaryTarget.GridPosition, Mathf.Max(1, spell.AoESizeSquares), gameManager.Grid);
                case AoEShape.Cone:
                    return AoESystem.GetConeCells(caster.GridPosition, primaryTarget.GridPosition, Mathf.Max(1, spell.AoESizeSquares), gameManager.Grid);
                default:
                    // Fallback for area-target spells without detailed shape metadata.
                    if (spell.TargetType == SpellTargetType.Area)
                        return AoESystem.GetBurstCells(primaryTarget.GridPosition, Mathf.Max(1, spell.AreaRadius), gameManager.Grid);
                    return new HashSet<Vector2Int> { primaryTarget.GridPosition };
            }
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

        private static int EstimateAverageDamage(SpellData spell)
        {
            if (spell == null)
                return 0;

            int diceAverage = 0;
            if (spell.DamageDice > 0 && spell.DamageCount > 0)
                diceAverage = spell.DamageCount * (spell.DamageDice + 1) / 2;

            int missileAverage = 0;
            if (spell.AutoHit && spell.MissileCount > 0)
                missileAverage = spell.MissileCount * ((spell.DamageDice + 1) / 2 + spell.BonusDamage);

            return Mathf.Max(diceAverage + spell.BonusDamage, missileAverage);
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

        public override float GetRangedAoORiskToleranceMultiplier()
        {
            // Spellcasters are generally more conservative about provoking while threatened.
            return 0.75f;
        }
    }
}
