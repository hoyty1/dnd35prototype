using System;
using System.Collections.Generic;
using System.Linq;
using DND35.AI;
using DND35.AI.Profiles;
using DND35.Magic;
using DND35e.Identifiers;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Comprehensive AI spellcasting brain for D&D 3.5e.
/// Handles all tactical spell decisions: target selection, spell scoring,
/// pre-buffing, defensive casting, save exploitation, resistance awareness,
/// resource management, dispel logic, summoning, combos, area denial,
/// metamagic, and class-specific features.
///
/// Tiers implemented:
///   T1: Ally targeting, pre-buff, defensive casting, save exploit, resistance awareness
///   T2: Expanded spell categories, resource management, dispel, summoning
///   T3: Multi-round planning, spell combos, area denial, enhanced counterspell, metamagic
///   T4: Wizard prep strategy, cleric domains, bard music, druid features, pattern learning
/// </summary>
public static class AISpellcastingStrategist
{
    // ═══════════════════════════════════════════════════════════════════════
    //  CONSTANTS & THRESHOLDS
    // ═══════════════════════════════════════════════════════════════════════

    // T1.1: Ally targeting
    private const float HEAL_SCORE_CRITICAL = 35f;
    private const float HEAL_SCORE_NORMAL = 25f;
    private const float BUFF_ALLY_MELEE_BONUS = 8f;
    private const float BUFF_ALLY_CASTER_BONUS = 4f;

    // T1.2: Pre-buffing
    private const int PRE_BUFF_ROUNDS = 2;
    private const float PRE_BUFF_SCORE_BOOST = 20f;
    private const float SAFE_CASTING_DISTANCE = 6; // squares

    // T1.3: Defensive casting
    private const float MIN_DEFENSIVE_CAST_SUCCESS = 0.50f;
    // DC constant now in ConcentrationService.DEFENSIVE_CASTING_DC_BASE

    // T1.4: Save exploitation
    private const float WEAK_SAVE_BONUS = 15f;
    private const float MEDIUM_SAVE_BONUS = 5f;
    private const float STRONG_SAVE_PENALTY = -10f;
    private const float NO_SAVE_BONUS = 10f;

    // T1.5: Resistance awareness
    private const float IMMUNE_PENALTY = -50f;
    private const float HIGH_RESIST_PENALTY = -30f;
    private const float MED_RESIST_PENALTY = -15f;
    private const float LOW_RESIST_PENALTY = -5f;
    private const float FORCE_DAMAGE_BONUS = 5f;

    // T2.2: Resource management
    private const float LOW_RESOURCE_THRESHOLD = 0.30f;
    private const float CRITICAL_SLOT_PENALTY = -20f;
    private const float LAST_SLOT_PENALTY = -35f;

    // T2.3: Dispel
    private const float DISPEL_PER_BUFF_BONUS = 10f;
    private const float DISPEL_GAME_CHANGING_BONUS = 25f;

    // T2.4: Summoning
    private const float SUMMON_OUTNUMBERED_BONUS = 15f;
    private const float SUMMON_ALONE_BONUS = 20f;

    // T3.1: Multi-round planning
    private const float SETUP_SPELL_BONUS = 12f;

    // T3.2: Spell combos
    private const float COMBO_SETUP_BONUS = 10f;
    private const float COMBO_FOLLOWUP_BONUS = 15f;

    // T3.3: Area denial
    private const float WALL_SPLIT_BONUS = 20f;
    private const float AREA_DENIAL_BONUS = 12f;

    // T3.5: Metamagic
    private const float QUICKEN_BONUS = 8f;

    // T4 class bonuses
    private const float DOMAIN_SPELL_BONUS = 6f;
    private const float SCHOOL_SPEC_BONUS = 5f;

    // ═══════════════════════════════════════════════════════════════════════
    //  COMBAT STATE TRACKING (per-encounter)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Tracks multi-round AI plan per NPC (instanceId → plan).</summary>
    private static readonly Dictionary<int, AISpellPlan> _activePlans = new Dictionary<int, AISpellPlan>();

    /// <summary>Tracks what damage types failed against each target (targetId → set of ineffective types).</summary>
    private static readonly Dictionary<int, HashSet<DamageType>> _ineffectiveDamageTypes = new Dictionary<int, HashSet<DamageType>>();

    /// <summary>Tracks spells that failed (save succeeded) against specific targets.</summary>
    private static readonly Dictionary<int, HashSet<string>> _failedSpellsPerTarget = new Dictionary<int, HashSet<string>>();

    /// <summary>Clear all tracking data (call at combat start).</summary>
    public static void ResetCombatState()
    {
        _activePlans.Clear();
        _ineffectiveDamageTypes.Clear();
        _failedSpellsPerTarget.Clear();
    }

    /// <summary>Record that a damage type was ineffective against a target.</summary>
    public static void RecordIneffectiveDamage(int targetId, DamageType type)
    {
        if (!_ineffectiveDamageTypes.TryGetValue(targetId, out var set))
        {
            set = new HashSet<DamageType>();
            _ineffectiveDamageTypes[targetId] = set;
        }
        set.Add(type);
    }

    /// <summary>Record that a spell's save was made by the target.</summary>
    public static void RecordSpellSaveSuccess(int targetId, string spellId)
    {
        if (string.IsNullOrEmpty(spellId)) return;
        if (!_failedSpellsPerTarget.TryGetValue(targetId, out var set))
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _failedSpellsPerTarget[targetId] = set;
        }
        set.Add(spellId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T1.1: ALLY/ENEMY TARGET SELECTION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get valid targets for a spell based on its effect type and target type.
    /// Fixes the critical bug where SelectBestSpellTarget only returned enemies.
    /// </summary>
    public static List<CharacterController> GetValidSpellTargets(
        SpellData spell, CharacterController caster,
        List<CharacterController> allCombatants, GameManager gm)
    {
        var targets = new List<CharacterController>();
        if (spell == null || caster == null || allCombatants == null || gm == null)
            return targets;

        // Self-only spells
        if (spell.TargetType == SpellTargetType.Self)
        {
            targets.Add(caster);
            return targets;
        }

        bool isAllySpell = IsAllyTargetedSpell(spell);
        bool isEnemySpell = IsEnemyTargetedSpell(spell);

        int rangeSquares = spell.GetRangeSquaresForCasterLevel(
            caster.Stats != null ? caster.Stats.GetCasterLevel() : 1);
        if (rangeSquares <= 0) rangeSquares = 1;

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            bool isAlly = candidate.Team == caster.Team;
            bool isEnemy = !isAlly;

            // Filter by spell target type
            if (isAllySpell && !isAlly) continue;
            if (isEnemySpell && !isEnemy) continue;

            // Touch spells allow both — use effect type to determine
            if (spell.TargetType == SpellTargetType.Touch)
            {
                if (spell.EffectType == SpellEffectType.Healing || spell.EffectType == SpellEffectType.Buff)
                {
                    if (!isAlly) continue;
                }
                else
                {
                    if (!isEnemy) continue;
                }
            }

            // Range check
            int distance = SquareGridUtils.GetDistance(caster.GridPosition, candidate.GridPosition);
            if (distance > rangeSquares)
                continue;

            targets.Add(candidate);
        }

        return targets;
    }

    /// <summary>Is this a spell that should target allies?</summary>
    public static bool IsAllyTargetedSpell(SpellData spell)
    {
        if (spell == null) return false;
        if (spell.TargetType == SpellTargetType.SingleAlly) return true;
        if (spell.TargetType == SpellTargetType.Self) return true;
        // Effect-type based for Touch/Area
        return spell.EffectType == SpellEffectType.Healing ||
               (spell.EffectType == SpellEffectType.Buff && spell.TargetType != SpellTargetType.Area);
    }

    /// <summary>Is this a spell that should target enemies?</summary>
    public static bool IsEnemyTargetedSpell(SpellData spell)
    {
        if (spell == null) return false;
        if (spell.TargetType == SpellTargetType.SingleEnemy) return true;
        return spell.EffectType == SpellEffectType.Damage ||
               spell.EffectType == SpellEffectType.Debuff ||
               spell.EffectType == SpellEffectType.Control ||
               spell.EffectType == SpellEffectType.Dispel;
    }

    /// <summary>
    /// Select the best target for a spell — supports ally AND enemy targeting.
    /// This replaces the old SelectBestSpellTarget that only iterated enemies.
    /// </summary>
    public static CharacterController SelectBestSpellTarget(
        CharacterController caster, SpellData spell,
        CharacterController fallbackTarget,
        List<CharacterController> allCombatants, GameManager gm)
    {
        if (spell == null || caster == null)
            return fallbackTarget;

        // Self-targeting spells
        if (spell.TargetType == SpellTargetType.Self)
            return caster;

        bool isAllySpell = IsAllyTargetedSpell(spell);

        if (isAllySpell)
            return SelectBestAllyTarget(caster, spell, allCombatants, gm);

        return SelectBestEnemyTarget(caster, spell, fallbackTarget, allCombatants, gm);
    }

    /// <summary>Select best ally target for healing/buff spells.</summary>
    private static CharacterController SelectBestAllyTarget(
        CharacterController caster, SpellData spell,
        List<CharacterController> allCombatants, GameManager gm)
    {
        if (allCombatants == null) return caster;

        int rangeSquares = spell.GetRangeSquaresForCasterLevel(
            caster.Stats != null ? caster.Stats.GetCasterLevel() : 1);
        if (rangeSquares <= 0) rangeSquares = 1;

        CharacterController bestTarget = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (candidate.Team != caster.Team) continue;

            int distance = SquareGridUtils.GetDistance(caster.GridPosition, candidate.GridPosition);
            if (distance > rangeSquares) continue;

            float score = ScoreAllyTarget(caster, candidate, spell);
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget ?? caster;
    }

    /// <summary>Score an ally as a target for a healing or buff spell.</summary>
    private static float ScoreAllyTarget(CharacterController caster, CharacterController ally, SpellData spell)
    {
        float score = 0f;
        if (ally == null || ally.Stats == null) return float.NegativeInfinity;

        float hpPct = ally.Stats.TotalMaxHP > 0
            ? (float)ally.Stats.CurrentHP / ally.Stats.TotalMaxHP : 1f;

        if (spell.EffectType == SpellEffectType.Healing)
        {
            // Prioritize lowest HP% ally
            score += (1f - hpPct) * 100f;

            // Prefer wounded frontline fighters
            if (IsFrontliner(ally)) score += 10f;

            // Consider healing efficiency — don't overheal
            int estimatedHeal = EstimateHealAmount(spell, caster);
            int missingHP = ally.Stats.TotalMaxHP - ally.Stats.CurrentHP;
            if (estimatedHeal > 0 && missingHP > 0)
            {
                float efficiency = Mathf.Min(1f, (float)missingHP / estimatedHeal);
                score += efficiency * 10f;
            }

            // Critical condition bonus
            if (hpPct <= 0.25f) score += 20f;
        }
        else if (spell.EffectType == SpellEffectType.Buff)
        {
            // Check if already buffed by this spell
            StatusEffectManager statusMgr = ally.StatusEffectManager;
            if (statusMgr != null && statusMgr.HasEffect(spell.SpellId))
            {
                int remaining = statusMgr.GetRemainingRounds(spell.SpellId);
                if (remaining > 1 || remaining == -1)
                    return float.NegativeInfinity; // Already has this buff
            }

            // Single-target buffs: prefer melee fighters for combat buffs
            if (IsFrontliner(ally))
                score += BUFF_ALLY_MELEE_BONUS;
            else
                score += BUFF_ALLY_CASTER_BONUS;

            // Self-buff for protective spells
            if (ally == caster && IsProtectiveSpell(spell))
                score += 6f;

            // Healthy allies are better buff targets (won't die before buff matters)
            score += hpPct * 5f;
        }

        return score;
    }

    /// <summary>Select best enemy target for offensive spells (with save/resistance awareness).</summary>
    private static CharacterController SelectBestEnemyTarget(
        CharacterController caster, SpellData spell, CharacterController fallbackTarget,
        List<CharacterController> allCombatants, GameManager gm)
    {
        if (allCombatants == null) return fallbackTarget;

        // Mirror image priority target
        if (gm != null)
        {
            CharacterController mirrorTarget = gm.GetMirrorImagePriorityTargetForAI(caster);
            if (mirrorTarget != null)
            {
                int mirrorDist = SquareGridUtils.GetDistance(caster.GridPosition, mirrorTarget.GridPosition);
                int range = spell.GetRangeSquaresForCasterLevel(caster.Stats?.GetCasterLevel() ?? 1);
                if (range <= 0) range = 1;
                if (mirrorDist <= range) return mirrorTarget;
            }
        }

        int rangeSquares = spell.GetRangeSquaresForCasterLevel(
            caster.Stats != null ? caster.Stats.GetCasterLevel() : 1);
        if (rangeSquares <= 0) rangeSquares = 1;

        CharacterController bestTarget = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (candidate.Team == caster.Team) continue; // skip allies

            int distance = SquareGridUtils.GetDistance(caster.GridPosition, candidate.GridPosition);
            if (distance > rangeSquares) continue;

            // Line of sight check for single-target spells
            bool requiresLOS = spell.TargetType == SpellTargetType.SingleEnemy;
            if (requiresLOS && !caster.CanSee(candidate, spell.IsRangedTouchSpell()))
                continue;

            float score = ScoreEnemyTarget(caster, candidate, spell, gm);
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget ?? fallbackTarget;
    }

    /// <summary>Score an enemy as a target for offensive spells (incorporates save/resistance analysis).</summary>
    private static float ScoreEnemyTarget(CharacterController caster, CharacterController enemy, SpellData spell, GameManager gm)
    {
        float score = 10f;
        if (enemy == null || enemy.Stats == null) return float.NegativeInfinity;

        // Base priority scoring
        float hpPct = enemy.Stats.TotalMaxHP > 0
            ? (float)enemy.Stats.CurrentHP / enemy.Stats.TotalMaxHP : 1f;

        // Prioritize wounded targets
        if (hpPct <= 0.35f) score += 8f;
        else if (hpPct <= 0.65f) score += 3f;

        // Prioritize spellcasters
        if (enemy.Stats.IsSpellcaster) score += 5f;
        if (enemy.Stats.IsWizard || enemy.Stats.IsCleric) score += 3f;

        // T1.4: Save exploitation — prefer targets with weak saves
        score += GetSaveExploitScore(enemy, spell);

        // T1.5: Resistance penalty — avoid immune/resistant targets
        score += GetResistancePenalty(enemy, spell);

        // T4.5: Pattern learning — avoid spells that previously failed
        int enemyId = enemy.GetInstanceID();
        if (_failedSpellsPerTarget.TryGetValue(enemyId, out var failedSet))
        {
            if (failedSet.Contains(spell.SpellId))
                score -= 12f; // Previously saved against this spell
        }

        // "Finishing blow" logic — if damage will kill, prefer this target
        if (spell.EffectType == SpellEffectType.Damage && spell.DamageCount > 0)
        {
            int estDamage = EstimateAverageDamage(spell);
            if (estDamage >= enemy.Stats.CurrentHP && enemy.Stats.CurrentHP > 0)
                score += 15f; // This spell will likely kill the target
        }

        // Spell resistance penalty
        if (spell.SpellResistanceApplies && enemy.Stats.SpellResistance > 0)
        {
            int casterLevel = caster.Stats?.GetCasterLevel() ?? 1;
            int srPenBonus = FeatManager.GetSpellPenetrationBonus(caster.Stats);
            int effectiveCL = casterLevel + srPenBonus;
            float srChance = Mathf.Clamp01((float)(effectiveCL + 1 - enemy.Stats.SpellResistance) / 20f);
            if (srChance < 0.5f) score -= 15f;
            else if (srChance < 0.75f) score -= 5f;
        }

        return score;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T1.2: PRE-BUFFING LOGIC
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Calculate pre-buff score bonus based on combat phase.</summary>
    public static float GetPreBuffBonus(SpellData spell, CharacterController caster,
        GameManager gm, List<CharacterController> allCombatants)
    {
        if (spell == null || caster == null || gm == null) return 0f;
        if (spell.EffectType != SpellEffectType.Buff &&
            spell.EffectType != SpellEffectType.Illusion) return 0f;

        int round = gm.CurrentRound;
        if (round > PRE_BUFF_ROUNDS) return 0f;

        // Check if enemies are adjacent
        bool adjacentEnemy = HasAdjacentEnemy(caster, allCombatants);
        if (adjacentEnemy) return 0f;

        // Check distance to nearest enemy
        float nearestEnemyDist = GetNearestEnemyDistance(caster, allCombatants);
        if (nearestEnemyDist < SAFE_CASTING_DISTANCE) return PRE_BUFF_SCORE_BOOST * 0.5f;

        float bonus = PRE_BUFF_SCORE_BOOST;

        // Prioritize long-duration buffs first (Mage Armor > Shield in round 1)
        if (spell.DurationType == DurationType.Hours ||
            (spell.BuffDurationRounds > 100 && spell.BuffDurationRounds != -1))
            bonus += 5f;

        // Protective self-buffs get extra priority
        if (IsProtectiveSpell(spell))
            bonus += 3f;

        return bonus;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T1.3: DEFENSIVE CASTING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Evaluate whether the AI should attempt defensive casting.
    /// Returns: 0 = don't cast, 1 = cast defensively, 2 = cast normally (no threat).
    /// </summary>
    public static int EvaluateDefensiveCasting(
        CharacterController caster, SpellData spell,
        List<CharacterController> allCombatants)
    {
        if (spell == null || caster == null || caster.Stats == null) return 0;

        bool threatened = HasAdjacentEnemy(caster, allCombatants);
        if (!threatened) return 2; // Safe to cast normally

        // Defensive casting: DC = 15 + spell level
        int dc = ConcentrationService.GetDefensiveCastingDC(spell.SpellLevel);
        int concentrationBonus = ConcentrationService.GetConcentrationBonus(caster);
        float successChance = ConcentrationService.CalculateSuccessChanceFraction(concentrationBonus, dc);

        if (successChance >= MIN_DEFENSIVE_CAST_SUCCESS)
            return 1; // Cast defensively

        // Desperate: if caster is low HP and spell is critical (heal self), attempt anyway
        float hpPct = caster.Stats.TotalMaxHP > 0
            ? (float)caster.Stats.CurrentHP / caster.Stats.TotalMaxHP : 1f;
        if (hpPct <= 0.25f && (spell.EffectType == SpellEffectType.Healing ||
                                spell.EffectType == SpellEffectType.Escape))
            return 1;

        // Try lower-level alternative: score penalty for high-level spells in melee
        // Caller should prefer lower-level spells
        return 0;
    }

    /// <summary>Get score modifier for spells cast while threatened (T1.3).</summary>
    public static float GetThreatenedCastingPenalty(
        CharacterController caster, SpellData spell,
        List<CharacterController> allCombatants)
    {
        if (spell == null || caster == null) return 0f;

        bool threatened = HasAdjacentEnemy(caster, allCombatants);
        if (!threatened) return 0f;

        int dc = ConcentrationService.GetDefensiveCastingDC(spell.SpellLevel);
        int concentrationBonus = ConcentrationService.GetConcentrationBonus(caster);
        float successChance = ConcentrationService.CalculateSuccessChanceFraction(concentrationBonus, dc);

        if (successChance >= 0.75f) return -2f;  // Minor penalty, likely to succeed
        if (successChance >= 0.50f) return -8f;  // Moderate penalty
        if (successChance >= 0.25f) return -20f; // Heavy penalty, risky
        return -40f; // Almost certain to fail, avoid
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T1.4: SAVE-TYPE EXPLOITATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Score bonus/penalty based on targeting an enemy's save weaknesses.</summary>
    public static float GetSaveExploitScore(CharacterController enemy, SpellData spell)
    {
        if (enemy == null || enemy.Stats == null || spell == null) return 0f;
        if (!spell.AllowsSavingThrow || string.IsNullOrEmpty(spell.SavingThrowType))
            return NO_SAVE_BONUS; // No save = guaranteed effect

        int fort = enemy.Stats.FortitudeSave;
        int reflex = enemy.Stats.ReflexSave;
        int will = enemy.Stats.WillSave;

        // Determine which save this spell targets
        int targetedSave;
        if (spell.SavingThrowType.IndexOf("Fort", StringComparison.OrdinalIgnoreCase) >= 0)
            targetedSave = fort;
        else if (spell.SavingThrowType.IndexOf("Ref", StringComparison.OrdinalIgnoreCase) >= 0)
            targetedSave = reflex;
        else if (spell.SavingThrowType.IndexOf("Will", StringComparison.OrdinalIgnoreCase) >= 0)
            targetedSave = will;
        else
            return 0f;

        // Find weakest and strongest saves
        int minSave = Mathf.Min(fort, Mathf.Min(reflex, will));
        int maxSave = Mathf.Max(fort, Mathf.Max(reflex, will));
        int midSave = fort + reflex + will - minSave - maxSave;

        // Score based on whether we're targeting weak, medium, or strong save
        if (targetedSave <= minSave)
            return WEAK_SAVE_BONUS;
        if (targetedSave >= maxSave)
            return STRONG_SAVE_PENALTY;
        return MEDIUM_SAVE_BONUS;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T1.5: RESISTANCE AWARENESS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Score penalty for spells against resistant/immune targets.</summary>
    public static float GetResistancePenalty(CharacterController enemy, SpellData spell)
    {
        if (enemy == null || enemy.Stats == null || spell == null) return 0f;
        if (spell.DamageCount <= 0 && spell.MissileCount <= 0) return 0f;
        if (string.IsNullOrEmpty(spell.DamageType)) return 0f;

        DamageType damageType = DamageTextUtils.ParseSingleDamageType(spell.DamageType);
        if (damageType == DamageType.Untyped) return 0f;

        // Force damage bonus — always works
        if (damageType == DamageType.Force) return FORCE_DAMAGE_BONUS;

        // Check immunity
        if (enemy.Stats.DamageImmunities != null && enemy.Stats.DamageImmunities.Contains(damageType))
            return IMMUNE_PENALTY;

        // Check resistance
        if (enemy.Stats.DamageResistances != null)
        {
            for (int i = 0; i < enemy.Stats.DamageResistances.Count; i++)
            {
                var entry = enemy.Stats.DamageResistances[i];
                if (entry != null && entry.Type == damageType && entry.Amount > 0)
                {
                    if (entry.Amount >= 20) return HIGH_RESIST_PENALTY;
                    if (entry.Amount >= 10) return MED_RESIST_PENALTY;
                    return LOW_RESIST_PENALTY;
                }
            }
        }

        // Check learned ineffectiveness from combat history
        int enemyId = enemy.GetInstanceID();
        if (_ineffectiveDamageTypes.TryGetValue(enemyId, out var ineffective) &&
            ineffective.Contains(damageType))
            return MED_RESIST_PENALTY;

        return 0f;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T2.2: RESOURCE MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Score modifier based on spell slot conservation.</summary>
    public static float GetResourceManagementScore(
        CharacterController caster, SpellData spell,
        SpellcastingComponent spellcasting)
    {
        if (spell == null || caster == null || spellcasting == null) return 0f;

        float score = 0f;
        int spellLevel = spell.SpellLevel;

        // Count remaining slots at this level
        List<SpellData> allCastable = spellcasting.GetCastablePreparedSpells();
        int slotsAtLevel = 0;
        int totalSlots = 0;
        if (allCastable != null)
        {
            for (int i = 0; i < allCastable.Count; i++)
            {
                if (allCastable[i] != null)
                {
                    totalSlots++;
                    if (allCastable[i].SpellLevel == spellLevel)
                        slotsAtLevel++;
                }
            }
        }

        // Last slot at this level penalty
        if (slotsAtLevel <= 1 && spellLevel >= 2)
            score += LAST_SLOT_PENALTY + (spellLevel * 2);

        // Critical slot: only one slot left at this level and it's high-level
        if (slotsAtLevel <= 1 && spellLevel >= 4)
            score += CRITICAL_SLOT_PENALTY;

        // Low total resources: be more conservative with high-level spells
        float resourceRatio = totalSlots > 0 ? (float)totalSlots / Mathf.Max(1, caster.Stats?.Level ?? 1) : 1f;
        if (resourceRatio < LOW_RESOURCE_THRESHOLD && spellLevel >= 3)
            score -= spellLevel * 3f;

        // Emergency reserves: always keep one healing spell for healers
        if (IsHealerProfile(caster) && spell.EffectType == SpellEffectType.Healing)
        {
            int healingSlotsRemaining = CountSpellsByEffect(allCastable, SpellEffectType.Healing);
            if (healingSlotsRemaining <= 1)
                score -= 15f; // Discourage using the last heal casually
        }

        // Emergency reserves: keep one escape spell if available
        if (spell.EffectType == SpellEffectType.Escape)
        {
            int escapeSlots = CountSpellsByEffect(allCastable, SpellEffectType.Escape);
            float hpPct = caster.Stats?.TotalMaxHP > 0
                ? (float)caster.Stats.CurrentHP / caster.Stats.TotalMaxHP : 1f;
            if (escapeSlots <= 1 && hpPct > 0.40f)
                score -= 20f; // Save escape spells for emergencies
        }

        // Spell level weighting: higher-level spells have inherently higher base value
        score += spellLevel * 2f;

        return score;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T2.3: DISPEL MAGIC LOGIC
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Score bonus for Dispel Magic/Remove Curse based on enemy buffs.</summary>
    public static float GetDispelScore(
        CharacterController caster, CharacterController enemy, SpellData spell,
        GameManager gm)
    {
        if (spell == null || enemy == null) return 0f;
        if (spell.EffectType != SpellEffectType.Dispel) return 0f;

        StatusEffectManager enemyStatus = enemy.StatusEffectManager;
        if (enemyStatus == null) return -20f; // No buffs to dispel

        int activeBuffCount = enemyStatus.ActiveEffectCount;
        if (activeBuffCount <= 0) return -20f;

        float score = activeBuffCount * DISPEL_PER_BUFF_BONUS;

        // Check for game-changing buffs
        if (enemyStatus.HasEffect(SpellNames.HASTE)) score += DISPEL_GAME_CHANGING_BONUS;
        if (enemyStatus.HasEffect(SpellNames.DISPLACEMENT)) score += DISPEL_GAME_CHANGING_BONUS;
        if (enemyStatus.HasEffect(SpellNames.MIRROR_IMAGE)) score += 15f;
        if (enemyStatus.HasEffect(SpellNames.INVISIBILITY)) score += 15f;
        if (enemyStatus.HasEffect(SpellNames.MAGE_ARMOR)) score += 5f;
        if (enemyStatus.HasEffect(SpellNames.SHIELD)) score += 5f;

        // If 3+ buffs, strongly prefer dispel
        if (activeBuffCount >= 3) score += 15f;

        return score;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T2.4: SUMMONING TACTICS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Score bonus for summoning spells based on tactical situation.</summary>
    public static float GetSummonScore(
        CharacterController caster, SpellData spell,
        List<CharacterController> allCombatants)
    {
        if (spell == null || caster == null) return 0f;
        if (spell.EffectType != SpellEffectType.Summon) return 0f;

        int allyCount = 0;
        int enemyCount = 0;
        if (allCombatants != null)
        {
            for (int i = 0; i < allCombatants.Count; i++)
            {
                var c = allCombatants[i];
                if (c == null || c.Stats == null || c.Stats.IsDead) continue;
                if (c.Team == caster.Team) allyCount++;
                else enemyCount++;
            }
        }

        float score = 0f;

        // Caster alone: summon for protection
        if (allyCount <= 1) score += SUMMON_ALONE_BONUS;

        // Outnumbered: summon for numbers advantage
        if (enemyCount > allyCount) score += SUMMON_OUTNUMBERED_BONUS;

        // Higher-level summon spells are more valuable
        score += spell.SpellLevel * 2f;

        return score;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T3.1: MULTI-ROUND PLANNING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Simple multi-round plan for AI spellcasters.</summary>
    public struct AISpellPlan
    {
        public string PlannedSpellId;      // Spell planned for next round
        public int PlannedRound;           // What round this plan is for
        public SpellEffectType PlanPhase;  // What phase the plan is in (Buff → Damage)
        public bool IsSetupComplete;       // Whether the setup spell was cast
    }

    /// <summary>Get score bonus for spells that fit into a multi-round plan.</summary>
    public static float GetMultiRoundPlanScore(
        CharacterController caster, SpellData spell, GameManager gm)
    {
        if (spell == null || caster == null || gm == null) return 0f;

        int casterId = caster.GetInstanceID();
        int currentRound = gm.CurrentRound;

        // Check if there's an active plan
        if (_activePlans.TryGetValue(casterId, out var plan))
        {
            if (plan.PlannedRound == currentRound && !string.IsNullOrEmpty(plan.PlannedSpellId))
            {
                // This round should execute the follow-up
                if (string.Equals(spell.SpellId, plan.PlannedSpellId, StringComparison.OrdinalIgnoreCase))
                {
                    _activePlans.Remove(casterId);
                    return COMBO_FOLLOWUP_BONUS;
                }
            }

            // Plan expired
            if (plan.PlannedRound < currentRound)
                _activePlans.Remove(casterId);
        }

        // Create plans for setup spells
        if (spell.EffectType == SpellEffectType.Buff && currentRound <= 2)
        {
            // Plan: buff now, attack next round
            return SETUP_SPELL_BONUS;
        }

        return 0f;
    }

    /// <summary>Register a planned follow-up spell for next round.</summary>
    public static void RegisterPlan(int casterId, string followUpSpellId, int nextRound)
    {
        _activePlans[casterId] = new AISpellPlan
        {
            PlannedSpellId = followUpSpellId,
            PlannedRound = nextRound,
            IsSetupComplete = true
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T3.2: SPELL COMBOS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Score bonus for spells that form tactical combos.</summary>
    public static float GetComboScore(
        CharacterController caster, SpellData spell,
        List<CharacterController> allCombatants, GameManager gm)
    {
        if (spell == null || caster == null) return 0f;

        float score = 0f;

        // Haste + full attack team synergy
        if (spell.SpellId == SpellNames.HASTE)
        {
            int meleeAllies = CountMeleeAllies(caster, allCombatants);
            score += meleeAllies * 5f; // +5 per melee ally who benefits
        }

        // Grease/Web + area damage combo (enemy area + ranged attacks)
        if (spell.EffectType == SpellEffectType.Control &&
            (spell.SpellId == SpellNames.WEB || spell.SpellId == SpellNames.GREASE))
        {
            // If we have ranged allies, control is more valuable
            int rangedAllies = CountRangedAllies(caster, allCombatants);
            score += rangedAllies * 3f;
        }

        // Invisibility + reposition combo
        if (spell.SpellId == SpellNames.INVISIBILITY || spell.EffectType == SpellEffectType.Illusion)
        {
            bool threatened = HasAdjacentEnemy(caster, allCombatants);
            if (threatened) score += 8f; // Escape from melee via invisibility
        }

        // Summon + buff summon combo
        if (spell.EffectType == SpellEffectType.Summon)
        {
            SpellcastingComponent sc = caster.Spellcasting;
            if (sc != null)
            {
                var castable = sc.GetCastablePreparedSpells();
                bool hasBuff = castable != null && castable.Any(s =>
                    s != null && s.EffectType == SpellEffectType.Buff && s.SpellLevel <= 2);
                if (hasBuff) score += COMBO_SETUP_BONUS;
            }
        }

        return score;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T3.3: AREA DENIAL & BATTLEFIELD CONTROL
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Score bonus for wall and area denial spells.</summary>
    public static float GetAreaDenialScore(
        CharacterController caster, SpellData spell,
        List<CharacterController> allCombatants)
    {
        if (spell == null || caster == null) return 0f;

        float score = 0f;

        if (spell.EffectType == SpellEffectType.Wall)
        {
            int enemyCount = CountEnemies(caster, allCombatants);

            // Walls are valuable when enemies are clustered
            if (enemyCount >= 3) score += WALL_SPLIT_BONUS;
            else if (enemyCount >= 2) score += WALL_SPLIT_BONUS * 0.5f;

            // Wall of Fire does damage — extra value
            if (spell.DamageCount > 0) score += 8f;

            // Walls protect ranged allies
            int rangedAllies = CountRangedAllies(caster, allCombatants);
            if (rangedAllies >= 1) score += 5f;
        }

        if (spell.EffectType == SpellEffectType.Control)
        {
            int enemyCount = CountEnemies(caster, allCombatants);
            int allyCount = CountAllies(caster, allCombatants);

            // Control is more valuable when outnumbered
            if (enemyCount > allyCount) score += AREA_DENIAL_BONUS;

            // AoE control (Web, Entangle) scales with enemies
            bool isAoE = spell.TargetType == SpellTargetType.Area || spell.AoEShapeType != AoEShape.None;
            if (isAoE && enemyCount >= 3) score += 10f;
        }

        return score;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T3.4: ENHANCED COUNTERSPELL
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Evaluate whether to ready a counterspell (enhanced version).</summary>
    public static float GetCounterspellScore(
        CharacterController caster, SpellData spell,
        List<CharacterController> allCombatants)
    {
        if (spell == null || caster == null) return 0f;
        // Counterspell readiness is handled separately in TryAIReadyCounterspell
        // This scores Dispel Magic higher when enemy casters are present
        if (spell.SpellId != SpellNames.DISPEL_MAGIC) return 0f;

        int enemyCasters = 0;
        if (allCombatants != null)
        {
            for (int i = 0; i < allCombatants.Count; i++)
            {
                var c = allCombatants[i];
                if (c == null || c.Stats == null || c.Stats.IsDead) continue;
                if (c.Team == caster.Team) continue;
                if (c.Stats.IsSpellcaster) enemyCasters++;
            }
        }

        return enemyCasters > 0 ? 5f * enemyCasters : 0f;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T3.5: METAMAGIC INTEGRATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Score bonus for metamagic considerations.</summary>
    public static float GetMetamagicScore(CharacterController caster, SpellData spell)
    {
        if (spell == null || caster == null || caster.Stats == null) return 0f;

        float score = 0f;

        // Quickened spells are very valuable (extra action)
        if (spell.ActionType == SpellActionType.Swift)
            score += QUICKEN_BONUS;

        // Grappled casters: prefer spells without somatic components
        if (caster.IsGrappling() && !spell.HasSomaticComponent)
            score += 10f; // Can cast this while grappled

        // Spells without verbal components are useful if verbal is restricted
        if (!spell.HasVerbalComponent)
            score += 1f;

        return score;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T4.1: WIZARD SPELL PREPARATION STRATEGY
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Score bonus for school specialization (wizard).</summary>
    public static float GetSchoolSpecializationScore(CharacterController caster, SpellData spell)
    {
        if (spell == null || caster == null || caster.Stats == null) return 0f;

        float score = 0f;

        // Wizard specialization: prefer specialty school spells
        if (caster.Stats.IsWizard && caster.Stats.IsWizardSpecialist &&
            !string.IsNullOrEmpty(spell.School))
        {
            // Spells from specialty school get +1 DC (handled by game system) — AI should prefer them
            score += SCHOOL_SPEC_BONUS;
        }

        return score;
    }

    /// <summary>Get Spell Focus DC bonus as a spell score modifier.</summary>
    public static float GetSpellFocusDCScore(CharacterController caster, SpellData spell)
    {
        if (spell == null || caster == null || caster.Stats == null) return 0f;
        if (!spell.AllowsSavingThrow) return 0f;

        int focusBonus = FeatManager.GetSpellFocusDCBonus(caster.Stats, spell.School ?? "");
        return focusBonus * SCHOOL_SPEC_BONUS;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T4.2: CLERIC DOMAIN POWERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Score bonus for domain spells (bonus spells should be used freely).</summary>
    public static float GetDomainSpellScore(CharacterController caster, SpellData spell)
    {
        if (spell == null || caster == null) return 0f;

        // Domain spells are bonus spells — encourage their use
        if (spell.AvailableFor != null)
        {
            for (int i = 0; i < spell.AvailableFor.Count; i++)
            {
                var avail = spell.AvailableFor[i];
                if (avail != null && !string.IsNullOrWhiteSpace(avail.Domain))
                    return DOMAIN_SPELL_BONUS;
            }
        }

        return 0f;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T4.3: BARD BARDIC MUSIC
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Score modifier for bard spells (music usually better early combat).</summary>
    public static float GetBardSpellScore(CharacterController caster, SpellData spell, GameManager gm)
    {
        if (spell == null || caster == null || gm == null) return 0f;
        if (caster.Stats == null || !caster.Stats.HasClass("Bard")) return 0f;

        // Bards should prefer music early, spells for specific problems
        int round = gm.CurrentRound;
        if (round <= 1)
        {
            // First round: slight penalty to spells (music is usually better)
            return -3f;
        }

        // After music is active, spells that address specific problems are valuable
        if (spell.EffectType == SpellEffectType.Control ||
            spell.EffectType == SpellEffectType.Healing)
            return 4f;

        return 0f;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T4.4: DRUID FEATURES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Score modifier for druid spells (nature-themed priorities).</summary>
    public static float GetDruidSpellScore(CharacterController caster, SpellData spell)
    {
        if (spell == null || caster == null || caster.Stats == null) return 0f;
        if (!caster.Stats.HasClass("Druid")) return 0f;

        float score = 0f;

        // Summon Nature's Ally spells are preferred for druids
        if (spell.EffectType == SpellEffectType.Summon)
            score += 3f;

        // Entangle/plant-themed control spells
        if (spell.EffectType == SpellEffectType.Control)
            score += 2f;

        return score;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  T4.5: ESCAPE SPELL LOGIC
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Score bonus for escape spells when caster is in danger.</summary>
    public static float GetEscapeScore(CharacterController caster, SpellData spell,
        List<CharacterController> allCombatants)
    {
        if (spell == null || caster == null || caster.Stats == null) return 0f;
        if (spell.EffectType != SpellEffectType.Escape) return 0f;

        float hpPct = caster.Stats.TotalMaxHP > 0
            ? (float)caster.Stats.CurrentHP / caster.Stats.TotalMaxHP : 1f;

        // Low HP: strongly prefer escape
        if (hpPct <= 0.25f) return 30f;
        if (hpPct <= 0.40f && HasAdjacentEnemy(caster, allCombatants)) return 20f;

        // Outnumbered and surrounded
        int adjacentEnemies = CountAdjacentEnemies(caster, allCombatants);
        if (adjacentEnemies >= 3 && hpPct <= 0.50f) return 25f;

        // Not in danger — don't waste escape spells
        return -10f;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  COMPREHENSIVE SPELL SCORING (combines all tiers)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Master spell scoring function — applies ALL tactical modifiers from Tiers 1–4.
    /// Called by SpellcasterAIProfile.ScoreSpell and AIService.SelectSpell.
    /// </summary>
    public static float ScoreSpellComprehensive(
        SpellData spell,
        CharacterController caster,
        CharacterController primaryTarget,
        List<CharacterController> allCombatants,
        GameManager gm,
        SpellcasterAIProfile profile = null)
    {
        if (spell == null) return float.MinValue;

        float score = 0f;

        // ── Base: Effect type scoring ──
        score += GetEffectTypeBaseScore(spell, caster, primaryTarget, allCombatants);

        // ── T1.2: Pre-buffing ──
        score += GetPreBuffBonus(spell, caster, gm, allCombatants);

        // ── T1.3: Defensive casting penalty ──
        score += GetThreatenedCastingPenalty(caster, spell, allCombatants);

        // ── T1.4: Save exploitation (for offensive spells targeting a specific enemy) ──
        if (primaryTarget != null && IsEnemyTargetedSpell(spell))
            score += GetSaveExploitScore(primaryTarget, spell);

        // ── T1.5: Resistance awareness ──
        if (primaryTarget != null && IsEnemyTargetedSpell(spell))
            score += GetResistancePenalty(primaryTarget, spell);

        // ── T2.2: Resource management ──
        SpellcastingComponent sc = caster?.Spellcasting;
        if (sc != null)
            score += GetResourceManagementScore(caster, spell, sc);

        // ── T2.3: Dispel logic ──
        if (primaryTarget != null)
            score += GetDispelScore(caster, primaryTarget, spell, gm);

        // ── T2.4: Summoning tactics ──
        score += GetSummonScore(caster, spell, allCombatants);

        // ── T3.1: Multi-round planning ──
        score += GetMultiRoundPlanScore(caster, spell, gm);

        // ── T3.2: Spell combos ──
        score += GetComboScore(caster, spell, allCombatants, gm);

        // ── T3.3: Area denial ──
        score += GetAreaDenialScore(caster, spell, allCombatants);

        // ── T3.4: Counterspell bonus ──
        score += GetCounterspellScore(caster, spell, allCombatants);

        // ── T3.5: Metamagic ──
        score += GetMetamagicScore(caster, spell);

        // ── T4.1: Spell Focus / School specialization ──
        score += GetSpellFocusDCScore(caster, spell);

        // ── T4.2: Domain spell bonus ──
        score += GetDomainSpellScore(caster, spell);

        // ── T4.3: Bard modifier ──
        score += GetBardSpellScore(caster, spell, gm);

        // ── T4.4: Druid modifier ──
        score += GetDruidSpellScore(caster, spell);

        // ── T4.5: Escape logic ──
        score += GetEscapeScore(caster, spell, allCombatants);

        // ── T4.5: Enemy type recognition ──
        if (primaryTarget != null)
            score += GetEnemyTypeScore(caster, primaryTarget, spell);

        return score;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  EFFECT TYPE BASE SCORING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Base score for each spell effect type (replaces old flat scoring).</summary>
    private static float GetEffectTypeBaseScore(
        SpellData spell, CharacterController caster,
        CharacterController primaryTarget,
        List<CharacterController> allCombatants)
    {
        if (spell == null) return 0f;

        float score = 0f;

        switch (spell.EffectType)
        {
            case SpellEffectType.Damage:
                score += 8f;
                // Finishing blow bonus
                if (primaryTarget != null && primaryTarget.Stats != null)
                {
                    int estDamage = EstimateAverageDamage(spell);
                    if (estDamage >= primaryTarget.Stats.CurrentHP && primaryTarget.Stats.CurrentHP > 0)
                        score += 15f;
                }
                break;

            case SpellEffectType.Healing:
                score += 6f;
                // Self-heal bonus when low
                if (caster != null && caster.Stats != null)
                {
                    float hpPct = caster.Stats.TotalMaxHP > 0
                        ? (float)caster.Stats.CurrentHP / caster.Stats.TotalMaxHP : 1f;
                    if (hpPct <= 0.25f) score += 20f;
                    else if (hpPct <= 0.40f) score += 10f;
                }
                break;

            case SpellEffectType.Buff:
                score += 5f;
                break;

            case SpellEffectType.Debuff:
                score += 7f;
                break;

            case SpellEffectType.Control:
                score += 9f; // Control is very valuable in D&D 3.5e
                // Extra value when outnumbered
                if (allCombatants != null)
                {
                    int enemies = CountEnemies(caster, allCombatants);
                    int allies = CountAllies(caster, allCombatants);
                    if (enemies > allies) score += 5f;
                }
                break;

            case SpellEffectType.Summon:
                score += 6f;
                break;

            case SpellEffectType.Utility:
                score += 1f; // Low combat priority
                break;

            case SpellEffectType.Escape:
                score += 2f; // Only valuable when in danger (handled by GetEscapeScore)
                break;

            case SpellEffectType.Dispel:
                score += 4f; // Situational (handled by GetDispelScore)
                break;

            case SpellEffectType.Wall:
                score += 7f;
                break;

            case SpellEffectType.Illusion:
                score += 6f;
                // Illusions are great for self-protection
                if (spell.TargetType == SpellTargetType.Self) score += 4f;
                break;

            case SpellEffectType.Divination:
                score += 1f; // Rarely useful in combat
                break;
        }

        // Spell level contributes to base value
        score += spell.SpellLevel * 1.5f;

        return score;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ENEMY TYPE RECOGNITION (T3.6 / T4.5)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Score modifier based on enemy creature type.</summary>
    private static float GetEnemyTypeScore(CharacterController caster, CharacterController enemy, SpellData spell)
    {
        if (enemy == null || enemy.Stats == null || spell == null) return 0f;

        float score = 0f;

        // Undead: immune to mind-affecting
        if (IsCreatureType(enemy, "Undead"))
        {
            if (spell.IsMindAffecting) score -= 50f;
            // Positive energy heals are damage to undead — bonus
            if (spell.EffectType == SpellEffectType.Damage &&
                !string.IsNullOrEmpty(spell.DamageType) &&
                spell.DamageType.IndexOf("positive", StringComparison.OrdinalIgnoreCase) >= 0)
                score += 10f;
        }

        // Construct: immune to mind-affecting, many spell effects
        if (IsCreatureType(enemy, "Construct"))
        {
            if (spell.IsMindAffecting) score -= 50f;
            if (spell.EffectType == SpellEffectType.Control) score -= 20f;
        }

        // Ooze: immune to mind-affecting
        if (IsCreatureType(enemy, "Ooze"))
        {
            if (spell.IsMindAffecting) score -= 50f;
        }

        return score;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ENCOUNTER DIFFICULTY ESTIMATION (T2.2)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Estimate encounter difficulty relative to caster level.
    /// Returns 0=easy, 1=medium, 2=hard, 3=boss.
    /// </summary>
    public static int EstimateEncounterDifficulty(
        CharacterController caster, List<CharacterController> allCombatants)
    {
        if (caster == null || caster.Stats == null || allCombatants == null) return 1;

        int casterLevel = caster.Stats.Level;
        int maxEnemyLevel = 0;
        int enemyCount = 0;

        for (int i = 0; i < allCombatants.Count; i++)
        {
            var c = allCombatants[i];
            if (c == null || c.Stats == null || c.Stats.IsDead) continue;
            if (c.Team == caster.Team) continue;
            enemyCount++;
            if (c.Stats.Level > maxEnemyLevel) maxEnemyLevel = c.Stats.Level;
        }

        if (enemyCount == 0) return 0;

        int levelDiff = maxEnemyLevel - casterLevel;
        if (levelDiff >= 4 || enemyCount >= 6) return 3; // Boss
        if (levelDiff >= 2 || enemyCount >= 4) return 2; // Hard
        if (levelDiff >= 0 || enemyCount >= 2) return 1; // Medium
        return 0; // Easy
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════════

    private static bool HasAdjacentEnemy(CharacterController caster, List<CharacterController> allCombatants)
    {
        if (caster == null || allCombatants == null) return false;
        for (int i = 0; i < allCombatants.Count; i++)
        {
            var c = allCombatants[i];
            if (c == null || c.Stats == null || c.Stats.IsDead) continue;
            if (c.Team == caster.Team) continue;
            if (SquareGridUtils.IsAdjacent(caster.GridPosition, c.GridPosition))
                return true;
        }
        return false;
    }

    private static int CountAdjacentEnemies(CharacterController caster, List<CharacterController> allCombatants)
    {
        if (caster == null || allCombatants == null) return 0;
        int count = 0;
        for (int i = 0; i < allCombatants.Count; i++)
        {
            var c = allCombatants[i];
            if (c == null || c.Stats == null || c.Stats.IsDead) continue;
            if (c.Team == caster.Team) continue;
            if (SquareGridUtils.IsAdjacent(caster.GridPosition, c.GridPosition))
                count++;
        }
        return count;
    }

    private static float GetNearestEnemyDistance(CharacterController caster, List<CharacterController> allCombatants)
    {
        if (caster == null || allCombatants == null) return float.MaxValue;
        float minDist = float.MaxValue;
        for (int i = 0; i < allCombatants.Count; i++)
        {
            var c = allCombatants[i];
            if (c == null || c.Stats == null || c.Stats.IsDead) continue;
            if (c.Team == caster.Team) continue;
            int dist = SquareGridUtils.GetDistance(caster.GridPosition, c.GridPosition);
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }

    private static int CountEnemies(CharacterController caster, List<CharacterController> allCombatants)
    {
        if (caster == null || allCombatants == null) return 0;
        int count = 0;
        for (int i = 0; i < allCombatants.Count; i++)
        {
            var c = allCombatants[i];
            if (c == null || c.Stats == null || c.Stats.IsDead) continue;
            if (c.Team != caster.Team) count++;
        }
        return count;
    }

    private static int CountAllies(CharacterController caster, List<CharacterController> allCombatants)
    {
        if (caster == null || allCombatants == null) return 0;
        int count = 0;
        for (int i = 0; i < allCombatants.Count; i++)
        {
            var c = allCombatants[i];
            if (c == null || c.Stats == null || c.Stats.IsDead) continue;
            if (c.Team == caster.Team) count++;
        }
        return count;
    }

    private static int CountMeleeAllies(CharacterController caster, List<CharacterController> allCombatants)
    {
        if (caster == null || allCombatants == null) return 0;
        int count = 0;
        for (int i = 0; i < allCombatants.Count; i++)
        {
            var c = allCombatants[i];
            if (c == null || c.Stats == null || c.Stats.IsDead) continue;
            if (c.Team != caster.Team) continue;
            if (IsFrontliner(c)) count++;
        }
        return count;
    }

    private static int CountRangedAllies(CharacterController caster, List<CharacterController> allCombatants)
    {
        if (caster == null || allCombatants == null) return 0;
        int count = 0;
        for (int i = 0; i < allCombatants.Count; i++)
        {
            var c = allCombatants[i];
            if (c == null || c.Stats == null || c.Stats.IsDead) continue;
            if (c.Team != caster.Team) continue;
            if (!IsFrontliner(c)) count++;
        }
        return count;
    }

    private static bool IsFrontliner(CharacterController character)
    {
        if (character == null || character.Stats == null) return false;
        // Fighter, Barbarian, Paladin, Ranger with melee, Monk
        return character.Stats.HasClass("Fighter") || character.Stats.IsBarbarian ||
               character.Stats.IsPaladin || character.Stats.IsMonk ||
               (character.Stats.GetMeleeAttackBonus() > character.Stats.GetRangedAttackBonus());
    }

    private static bool IsProtectiveSpell(SpellData spell)
    {
        if (spell == null) return false;
        return spell.BuffACBonus > 0 || spell.BuffShieldBonus > 0 ||
               spell.BuffDeflectionBonus > 0 || spell.BuffTempHP > 0 ||
               spell.BuffDamageResistanceAmount > 0 || spell.BuffDamageReductionAmount > 0;
    }

    private static int EstimateHealAmount(SpellData spell, CharacterController caster)
    {
        if (spell == null) return 0;
        int dice = spell.HealCount > 0 && spell.HealDice > 0
            ? spell.HealCount * (spell.HealDice + 1) / 2 : 0;
        int bonus = spell.BonusHealing;
        if (caster?.Stats != null && spell.BonusHealing == 0)
            bonus = Mathf.Min(caster.Stats.GetCasterLevel(), spell.SpellLevel * 5);
        return dice + bonus;
    }

    private static int EstimateAverageDamage(SpellData spell)
    {
        if (spell == null) return 0;
        int diceAvg = 0;
        if (spell.DamageDice > 0 && spell.DamageCount > 0)
            diceAvg = spell.DamageCount * (spell.DamageDice + 1) / 2;
        int missileAvg = 0;
        if (spell.AutoHit && spell.MissileCount > 0)
            missileAvg = spell.MissileCount * ((spell.DamageDice + 1) / 2 + spell.BonusDamage);
        return Mathf.Max(diceAvg + spell.BonusDamage, missileAvg);
    }

    private static bool IsHealerProfile(CharacterController caster)
    {
        if (caster == null) return false;
        return caster.aiProfile is HealerAIProfile;
    }

    private static int CountSpellsByEffect(List<SpellData> spells, SpellEffectType effectType)
    {
        if (spells == null) return 0;
        int count = 0;
        for (int i = 0; i < spells.Count; i++)
        {
            if (spells[i] != null && spells[i].EffectType == effectType)
                count++;
        }
        return count;
    }

    private static bool IsCreatureType(CharacterController character, string type)
    {
        if (character == null || character.Stats == null) return false;

        // Check creature tags
        if (character.Tags != null && character.Tags.HasTag($"Type:{type}"))
            return true;

        // Check creature type string
        if (character.Stats.CreatureTags != null)
        {
            for (int i = 0; i < character.Stats.CreatureTags.Count; i++)
            {
                if (character.Stats.CreatureTags[i] != null &&
                    character.Stats.CreatureTags[i].IndexOf(type, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }

        // Check IsMindless for undead/construct heuristic
        if (type == "Undead" && character.Stats.IsMindless)
            return true;

        return false;
    }
}
