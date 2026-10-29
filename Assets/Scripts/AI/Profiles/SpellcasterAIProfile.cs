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

        public override void ApplyRuntimeDefaults()
        {
            base.ApplyRuntimeDefaults();

            ProfileName = "Spellcaster";

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

                // The best aim the caster has (AreaAimSearch, batch 11 step 25): the spell is scored on that placement.
                SpellAOEEvaluation aoeEval = EvaluateAOECast(spell, caster, primaryTarget, allCombatants, gameManager);
                AreaAimPolarity polarity = AreaAimSearch.PolarityOf(spell);
                if (polarity == AreaAimPolarity.Beneficial || polarity == AreaAimPolarity.Mixed)
                {
                    // Bless helps allies; Prayer (PHB p.264) helps allies and hinders foes, each one it reaches counts.
                    score += (aoeEval.AlliesBenefited + (polarity == AreaAimPolarity.Mixed ? aoeEval.EnemiesHit : 0)) * 3f;
                    if (aoeEval.Placement == null)
                        score -= 1000f;
                }
                else
                {
                    score += aoeEval.EnemiesHit * 3f;
                    score -= aoeEval.EffectiveAllyCasualties * 10f;

                    if (aoeEval.EnemiesHit < AOECasting.MinimumEnemiesInAOE)
                        score -= 6f;

                    if (!aoeEval.IsSafe)
                        score -= 1000f;
                }
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
        /// How the best area cast of <paramref name="spell"/> would land (batch 11 step 25): the best of the aims the caster
        /// has (AreaAimSearch.Search: the step 24 geometry and line of effect, the same tests the PC cursor and the cast core
        /// make, so the aim evaluated is one the cast core lets through), wherever the enemies stand, not only at
        /// <paramref name="primaryTarget"/>. A harmful spell's aim must reach an enemy it affects, a beneficial one's an ally
        /// it helps, Prayer's an ally or a foe not already under it. The best is a safe aim first (AOECasting: the allies' expected casualties within
        /// MaxAcceptableAllyCasualties when AvoidHittingAllies), then the search's score (the enemies affected less twice the
        /// allies affected), then fewer allies harmed, then the first found. A creature counts when any square of its space
        /// is in the area (AoESystem.IsCreatureInCells) and the spell's filter takes it in. No aim: unsafe, nothing hit.
        /// </summary>
        public virtual SpellAOEEvaluation EvaluateAOECast(
            SpellData spell,
            CharacterController caster,
            CharacterController primaryTarget,
            List<CharacterController> allCombatants,
            GameManager gameManager)
        {
            if (spell == null || caster == null || gameManager == null || gameManager.Grid == null)
                return new SpellAOEEvaluation(false, 0, 0, 999f);

            AreaAimPolarity polarity = AreaAimSearch.PolarityOf(spell);
            AreaPlacement best = null;
            bool bestSafe = false;
            float bestCasualties = 999f;
            foreach (AreaPlacement aim in AreaAimSearch.Search(spell, caster, allCombatants, gameManager.Grid))
            {
                if (polarity == AreaAimPolarity.Harmful && aim.EnemiesHit == 0)
                    continue;
                if (polarity == AreaAimPolarity.Beneficial && aim.AlliesBenefited == 0)
                    continue;
                if (polarity == AreaAimPolarity.Mixed && aim.AlliesBenefited + aim.EnemiesHit == 0)
                    continue;
                float casualties = GetPlacementCasualties(spell, caster, aim, allCombatants);
                bool safe = IsSafeCasualties(casualties);
                if (best == null || (safe && !bestSafe) || (safe == bestSafe && AreaAimSearch.IsBetter(aim, best)))
                {
                    best = aim;
                    bestSafe = safe;
                    bestCasualties = casualties;
                }
            }

            if (best == null)
                return new SpellAOEEvaluation(false, 0, 0, 999f);
            return new SpellAOEEvaluation(bestSafe, best.EnemiesHit, best.AlliesHit, bestCasualties, best.AlliesBenefited, best);
        }

        /// <summary>
        /// The aim this profile casts <paramref name="spell"/> at (batch 11 step 25): the best worthwhile aim
        /// (AreaAimSearch.FindBest) that is safe for its allies by this profile's AOECasting; null when there is none, and
        /// then the AI drops the spell (no fallback).
        /// </summary>
        public AreaPlacement FindAreaPlacement(SpellData spell, CharacterController caster, List<CharacterController> allCombatants, GameManager gameManager)
        {
            if (spell == null || caster == null || gameManager == null)
                return null;
            var options = new AreaAimOptions
            {
                Accept = aim => IsSafeCasualties(GetPlacementCasualties(spell, caster, aim, allCombatants))
            };
            return AreaAimSearch.FindBest(spell, caster, allCombatants, gameManager.Grid, options);
        }

        private bool IsSafeCasualties(float casualties)
            => !AOECasting.AvoidHittingAllies || casualties <= AOECasting.MaxAcceptableAllyCasualties;

        /// <summary>
        /// The allies' expected casualties from <paramref name="aim"/> (GetAllyCasualtyWeight per ally the spell's filter
        /// takes in), for a harmful spell when the profile avoids hitting allies; 0 otherwise.
        /// </summary>
        private float GetPlacementCasualties(SpellData spell, CharacterController caster, AreaPlacement aim, List<CharacterController> allCombatants)
        {
            if (aim == null || allCombatants == null || !AOECasting.AvoidHittingAllies
                || AreaAimSearch.PolarityOf(spell) != AreaAimPolarity.Harmful)
                return 0f;
            int estimatedDamage = EstimateAverageDamage(spell, caster);
            DamageType damageType = DamageTextUtils.ParseSingleDamageType(spell.DamageType);
            AoETargetFilter filter = AreaAimSearch.IsGrease(spell) ? AoETargetFilter.All : spell.AoEFilter;
            float casualties = 0f;
            for (int i = 0; i < allCombatants.Count; i++)
            {
                CharacterController c = allCombatants[i];
                if (c == null || c.Stats == null || c.Stats.IsDead)
                    continue;
                if (c != caster && !TeamUtility.IsAlly(caster, c))
                    continue;
                if (!AoESystem.IsCreatureInCells(aim.Cells, c) || !AoESystem.IncludedByFilter(caster, c, filter))
                    continue;
                if (!AreaAimSearch.WouldAffect(spell, c))
                    continue;
                casualties += GetAllyCasualtyWeight(c, damageType, estimatedDamage);
            }
            return casualties;
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

        /// <summary>
        /// Average damage at the caster's caster level (SpellDiceRules, SPL-005); a custom resolver's dice where the spell's
        /// data dice are a placeholder (CustomSpellDice.TryDamageEstimate, as AISpellcastingStrategist's estimate; batch 11
        /// step 28 review: Fireball's 1d6 per level, read as 1d6 before).
        /// </summary>
        private static int EstimateAverageDamage(SpellData spell, CharacterController caster)
        {
            if (spell == null)
                return 0;

            int casterLevel = SpellDiceRules.CasterLevelFor(caster != null ? caster.Stats : null, spell);
            SpellDice damage = CustomSpellDice.TryDamageEstimate(spell, casterLevel, out SpellDice custom) ? custom : SpellDiceRules.Damage(spell, casterLevel);
            int diceAverage = Mathf.FloorToInt(damage.Average);

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
