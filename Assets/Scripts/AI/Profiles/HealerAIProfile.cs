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

        protected override void OnEnable()
        {
            base.OnEnable();

            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Healer";
            if (string.IsNullOrWhiteSpace(Description))
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

        public HealerActionType DetermineActionPriority(CharacterController healer, List<CharacterController> allCombatants, bool hasCastableSpells)
        {
            if (healer == null || healer.Stats == null)
                return HealerActionType.PhysicalAttack;

            List<CharacterController> alliesNeedingHealing = GetAlliesNeedingHealing(healer, allCombatants);
            if (alliesNeedingHealing.Count > 0)
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

            HealerActionType actionType = DetermineActionPriority(caster, allCombatants, hasCastableSpells: true);
            switch (actionType)
            {
                case HealerActionType.CriticalHealing:
                    return spell.EffectType == SpellEffectType.Healing ? score + 35f : score - 40f;

                case HealerActionType.Healing:
                    return spell.EffectType == SpellEffectType.Healing ? score + 25f : score - 25f;

                case HealerActionType.Buffing:
                    if (spell.EffectType == SpellEffectType.Buff) return score + 20f;
                    if (spell.EffectType == SpellEffectType.Healing) return score + 4f;
                    return score - 18f;

                case HealerActionType.OffensiveSpell:
                    if (spell.EffectType == SpellEffectType.Damage || spell.EffectType == SpellEffectType.Debuff) return score + 12f;
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

        private bool IsCriticallyWounded(CharacterController candidate)
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

            return candidate.Team == healer.Team;
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
