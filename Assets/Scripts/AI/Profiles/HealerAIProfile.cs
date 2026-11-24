using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Healer/support profile priority model:
    /// Healing > Buffs > Offensive spells > Physical attacks.
    /// </summary>
    [CreateAssetMenu(fileName = "Healer AI", menuName = "DND35/AI/Profiles/Spellcasters/Healer")]
    public class HealerAIProfile : SpellcasterAIProfile
    {
        [Header("Healing Priorities")]
        [Range(0.2f, 1f)]
        [Tooltip("Heal allies when they fall below this health percentage.")]
        public float HealingThreshold = 0.70f;

        [Range(0.05f, 0.5f)]
        [Tooltip("Emergency healing threshold for critical allies.")]
        public float CriticalHealingThreshold = 0.25f;

        [Range(0.5f, 1f)]
        [Tooltip("Allies are considered healthy above this threshold.")]
        public float HealthyThreshold = 0.75f;

        [Header("Adaptive Physical Combat")]
        [Range(10, 25)]
        [Tooltip("Minimum AC required before healer considers melee as safe.")]
        public int MinimumMeleeArmorClass = 16;

        [Range(0, 5)]
        [Tooltip("Prefer style with this many attack bonus points advantage.")]
        public int AttackBonusPreference = 2;

        [Tooltip("If AC is below MinimumMeleeArmorClass, always prefer ranged physical attacks.")]
        public bool ForceRangedIfLowAC = true;

        [Header("Support Positioning")]
        [Range(1, 8)]
        public int HealingRangeSquares = 4;

        public bool StayNearWoundedAllies = true;

        public override void ApplyRuntimeDefaults()
        {
            base.ApplyRuntimeDefaults();

            ProfileName = "Healer";
            Description = "Support spellcaster focused on healing, buffing, and safe combat decisions.";

            CombatStyle = CombatStyle.Ranged;
            Aggression = 0.2f;

            PreferredCastingDistanceSquares = HealingRangeSquares;
            Movement.PreferredRangeSquares = PreferredCastingDistanceSquares;
            Movement.AvoidAoOs = true;
            Movement.MaintainDistance = false;
            Movement.SeekFlanking = false;
            Movement.UseCover = true;

            if (SchoolPriorities.Count == 0)
            {
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Conjuration, 10f));
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Abjuration, 8f));
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Transmutation, 7f));
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Evocation, 4f));
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Enchantment, 3f));
            }

            AOECasting.PreferSingleTarget = true;
            AOECasting.MinimumEnemiesInAOE = 4;
            AOECasting.AvoidHittingAllies = true;
            AOECasting.MaxAcceptableAllyCasualties = 0f;
            AOECasting.AcceptableAllyDamagePercent = 0f;

            SpellSelection.BuffBeforeDamage = true;
            SpellSelection.ConserveHighLevelSpells = true;
            SpellSelection.UseUtilitySpells = true;
        }

        /// <summary>
        /// The healer's turn priority: CriticalHealing or Healing when an ally (or the healer) it can cure is below
        /// <see cref="HealingThreshold"/> and <paramref name="hasCastableCure"/> (a castable spell that cures hit points,
        /// <see cref="HasCastableCure"/>; batch 14 step 12, AI-063: without one, a Healing turn found nothing to cast and fell
        /// to the kiter's total defense); else Buffing when the party is healthy, OffensiveSpell with any castable spell,
        /// else PhysicalAttack.
        /// </summary>
        public HealerActionType DetermineActionPriority(CharacterController healer, List<CharacterController> allCombatants, bool hasCastableSpells, bool hasCastableCure)
        {
            if (healer == null || healer.Stats == null)
                return HealerActionType.PhysicalAttack;

            List<CharacterController> alliesNeedingHealing = GetAlliesNeedingHealing(healer, allCombatants);
            if (alliesNeedingHealing.Count > 0 && hasCastableCure)
            {
                bool hasCritical = alliesNeedingHealing.Any(IsCriticallyWounded);
                return hasCritical ? HealerActionType.CriticalHealing : HealerActionType.Healing;
            }

            if (hasCastableSpells && ShouldBuffAllies(healer, allCombatants))
                return HealerActionType.Buffing;

            if (hasCastableSpells)
                return HealerActionType.OffensiveSpell;

            return HealerActionType.PhysicalAttack;
        }

        public CharacterController GetPriorityHealTarget(CharacterController healer, List<CharacterController> allCombatants)
        {
            List<CharacterController> alliesNeedingHealing = GetAlliesNeedingHealing(healer, allCombatants);
            if (alliesNeedingHealing.Count == 0)
                return null;

            CharacterController critical = alliesNeedingHealing.FirstOrDefault(IsCriticallyWounded);
            return critical != null ? critical : alliesNeedingHealing[0];
        }

        public CombatStyle DetermineCombatMode(CharacterController healer)
        {
            if (healer == null || healer.Stats == null)
                return CombatStyle.Ranged;

            int ac = healer.Stats.GetArmorClass();
            int meleeAttackBonus = healer.Stats.GetMeleeAttackBonus();
            int rangedAttackBonus = healer.Stats.GetRangedAttackBonus();

            if (ForceRangedIfLowAC && ac < MinimumMeleeArmorClass)
                return CombatStyle.Ranged;

            int diff = meleeAttackBonus - rangedAttackBonus;
            if (diff >= AttackBonusPreference && ac >= MinimumMeleeArmorClass)
                return CombatStyle.Melee;

            if (-diff >= AttackBonusPreference)
                return CombatStyle.Ranged;

            return ac >= (MinimumMeleeArmorClass + 2) ? CombatStyle.Melee : CombatStyle.Ranged;
        }

        public override float ScoreSpell(
            SpellData spell,
            CharacterController caster,
            CharacterController primaryTarget,
            List<CharacterController> allCombatants,
            GameManager gameManager)
        {
            float score = base.ScoreSpell(spell, caster, primaryTarget, allCombatants, gameManager);
            if (spell == null || caster == null)
                return score;

            // The turn's own priority (AIService's Healer branch): Healing only with a castable cure (batch 14 step 12, AI-063).
            HealerActionType actionType = DetermineActionPriority(caster, allCombatants, hasCastableSpells: true,
                hasCastableCure: GetAlliesNeedingHealing(caster, allCombatants).Count == 0 || HasCastableCure(caster, gameManager));
            switch (actionType)
            {
                // The bonus goes to a spell that cures hit points (SpellDiceRules.CuresHitPoints, PHB p.32), not to every
                // Healing-effect spell: Remove Paralysis is no answer to a wounded ally (batch 14 step 12, AI-063).
                case HealerActionType.CriticalHealing:
                    return SpellDiceRules.CuresHitPoints(spell) ? score + 35f : score - 40f;

                case HealerActionType.Healing:
                    return SpellDiceRules.CuresHitPoints(spell) ? score + 25f : score - 25f;

                case HealerActionType.Buffing:
                    if (spell.EffectType == SpellEffectType.Buff) return score + 20f;
                    if (spell.EffectType == SpellEffectType.Healing) return score + 4f;
                    return score - 18f;

                case HealerActionType.OffensiveSpell:
                    if (spell.EffectType == SpellEffectType.Damage || spell.EffectType == SpellEffectType.Debuff ||
                        spell.EffectType == SpellEffectType.Control) return score + 12f;
                    if (spell.EffectType == SpellEffectType.Summon) return score + 8f;
                    if (spell.EffectType == SpellEffectType.Dispel) return score + 6f;
                    return score - 10f;

                default:
                    return score - 30f;
            }
        }

        private List<CharacterController> GetAlliesNeedingHealing(CharacterController healer, List<CharacterController> allCombatants)
        {
            var wounded = new List<CharacterController>();
            if (healer == null || allCombatants == null)
                return wounded;

            for (int i = 0; i < allCombatants.Count; i++)
            {
                CharacterController candidate = allCombatants[i];
                if (!IsAliveAlly(healer, candidate))
                    continue;
                // A cure harms an undead and does nothing to a construct (PHB p.215; SpellDiceRules.OutcomeFor, SPL-005),
                // so they are not cure targets. Healing an undead ally with an inflict spell is AI-062.
                if (SpellDiceRules.IsUndead(candidate.Stats) || SpellDiceRules.IsConstruct(candidate.Stats))
                    continue;

                float hpPct = GetHealthPercent(candidate);
                if (hpPct < HealingThreshold)
                    wounded.Add(candidate);
            }

            wounded.Sort((a, b) => GetHealthPercent(a).CompareTo(GetHealthPercent(b)));
            return wounded;
        }

        private bool ShouldBuffAllies(CharacterController healer, List<CharacterController> allCombatants)
        {
            if (healer == null || allCombatants == null)
                return false;

            bool foundAnyAlly = false;
            for (int i = 0; i < allCombatants.Count; i++)
            {
                CharacterController candidate = allCombatants[i];
                if (!IsAliveAlly(healer, candidate))
                    continue;

                foundAnyAlly = true;
                if (GetHealthPercent(candidate) < HealthyThreshold)
                    return false;
            }

            return foundAnyAlly;
        }

        /// <summary>
        /// Whether <paramref name="healer"/> can cast a spell that cures hit points now (batch 14 step 12, AI-063):
        /// <see cref="CastableCures"/> is not empty.
        /// </summary>
        public static bool HasCastableCure(CharacterController healer, GameManager gameManager)
            => CastableCures(healer, gameManager).Count > 0;

        /// <summary>
        /// The spells that cure hit points (SpellDiceRules.CuresHitPoints: the cure ... wounds spells and Heal, PHB p.32,
        /// p.239) that <paramref name="healer"/> can cast now (batch 14 step 12, AI-063): its castable prepared or known
        /// spells (SpellcastingComponent.GetCastablePreparedSpells: a slot left, the form, the components), or a cure a
        /// cleric converts a prepared spell into (PHB p.32; AiConversionPlanner, CHR-070), that the cast core's caster tests
        /// let through (GameManager.CheckCasterLegalityForAI: the casting action, Silence, the metamagic; skipped without a
        /// GameManager). Whether it has a target in reach is not asked. In Play mode the answer is kept for the frame while
        /// the healer's castable spells, slots left and spontaneous casting stay the same (ScoreSpell asks once per scored
        /// spell; a cast that spends a slot changes them), so a turn's scoring passes do not repeat the legality checks. Never null.
        /// </summary>
        public static List<SpellData> CastableCures(CharacterController healer, GameManager gameManager)
        {
            SpellcastingComponent sc = healer != null && healer.Stats != null ? healer.Spellcasting : null;
            if (sc == null || !sc.CanCastSpells)
                return new List<SpellData>();
            List<SpellData> castable = sc.GetCastablePreparedSpells() ?? new List<SpellData>();
            bool cache = Application.isPlaying;
            int signature = cache ? CureCacheSignature(healer, sc, castable) : 0;
            if (cache && _cureCacheHealer == healer && _cureCacheGameManager == gameManager
                && _cureCacheFrame == Time.frameCount && _cureCacheSignature == signature && _cureCache != null)
                return _cureCache;

            var cures = new List<SpellData>();
            for (int i = 0; i < castable.Count; i++)
                if (IsCastableCure(healer, castable[i], gameManager))
                    cures.Add(castable[i]);
            List<SpellData> converted = AiConversionPlanner.Candidates(healer, castable);
            if (converted != null)
                for (int i = 0; i < converted.Count; i++)
                    if (IsCastableCure(healer, converted[i], gameManager))
                        cures.Add(converted[i]);

            if (cache)
            {
                _cureCacheHealer = healer;
                _cureCacheGameManager = gameManager;
                _cureCacheFrame = Time.frameCount;
                _cureCacheSignature = signature;
                _cureCache = cures;
            }
            return cures;
        }

        private static CharacterController _cureCacheHealer;
        private static GameManager _cureCacheGameManager;
        private static int _cureCacheFrame = -1;
        private static int _cureCacheSignature = -1;
        private static List<SpellData> _cureCache;

        /// <summary>What a cached <see cref="CastableCures"/> answer depends on that a cast or a conversion changes: the castable spells, the slots left, the spontaneous casting.</summary>
        private static int CureCacheSignature(CharacterController healer, SpellcastingComponent sc, List<SpellData> castable)
        {
            int slotsLeft = 0;
            if (sc.SpellSlots != null)
                for (int i = 0; i < sc.SpellSlots.Count; i++)
                    if (sc.SpellSlots[i] != null && sc.SpellSlots[i].CanCast)
                        slotsLeft++;
            int spontaneous = healer.Stats != null ? (int)healer.Stats.SpontaneousCasting : 0;
            return castable.Count + 1000 * slotsLeft + 1000000 * spontaneous;
        }

        private static bool IsCastableCure(CharacterController healer, SpellData spell, GameManager gameManager)
            => SpellDiceRules.CuresHitPoints(spell)
               && (gameManager == null || gameManager.CheckCasterLegalityForAI(healer, spell) == null);

        /// <summary>Whether <paramref name="candidate"/> is at or below <see cref="CriticalHealingThreshold"/> (Q8: a provoking path to it is priced 6 per provoker).</summary>
        public bool IsCriticallyWounded(CharacterController candidate)
        {
            return candidate != null && GetHealthPercent(candidate) <= CriticalHealingThreshold;
        }

        private static float GetHealthPercent(CharacterController candidate)
        {
            if (candidate == null || candidate.Stats == null || candidate.Stats.TotalMaxHP <= 0)
                return 1f;

            return Mathf.Clamp01((float)candidate.Stats.CurrentHP / candidate.Stats.TotalMaxHP);
        }

        private static bool IsAliveAlly(CharacterController healer, CharacterController candidate)
        {
            if (healer == null || candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                return false;

            return candidate == healer || TeamUtility.IsAlly(healer, candidate); // AI-016
        }
    }

    public enum HealerActionType
    {
        CriticalHealing,
        Healing,
        Buffing,
        OffensiveSpell,
        PhysicalAttack
    }
}
