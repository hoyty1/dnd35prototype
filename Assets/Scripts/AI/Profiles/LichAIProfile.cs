using System.Collections.Generic;
using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Lich AI profile — apex undead spellcaster:
    /// - Full wizard spell repertoire with intelligent spell selection
    /// - Open with buffs (Stoneskin, Greater Invisibility, Mirror Image) before engaging
    /// - Use fear aura passively (processed by AIService aura system)
    /// - Reserve high-level spells for high-threat targets
    /// - Paralyzing touch as last resort melee option
    /// - Never flee — liches are supremely confident (phylactery backup)
    /// - Prefer AoE control/damage spells to cripple groups
    /// - Counter enemy spellcasters with targeted debuffs
    /// </summary>
    [CreateAssetMenu(fileName = "Lich AI", menuName = "DND35/AI/Profiles/Lich")]
    public class LichAIProfile : SpellcasterAIProfile
    {
        protected override void OnEnable()
        {
            base.OnEnable();

            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Lich";

            if (string.IsNullOrWhiteSpace(Description))
                Description = "Undead archmage: buffs first, controls the battlefield with AoE, and reserves power for threats.";

            CombatStyle = CombatStyle.Ranged; // spell-focused, maintain distance
            Aggression = 0.5f;                // calculated, not reckless
            PrioritizeWounded = false;        // target tactically, not opportunistically
            PrioritizeIsolated = false;       // prefers groups (AoE value)
            SwitchTargetsOften = false;       // methodical focus

            if (Movement == null)
                Movement = new MovementPreferences();

            Movement.AvoidAoOs = true;
            Movement.PreferredRangeSquares = 8; // stay well back
            Movement.MaintainDistance = true;
            Movement.SeekFlanking = false;
            Movement.UseCover = true;

            GrappleBehavior = GrappleBehavior.Avoid; // never waste time grappling

            if (Maneuvers == null)
                Maneuvers = new ManeuverPreferences();

            Maneuvers.AttemptTrip = false;
            Maneuvers.AttemptDisarm = false;
            Maneuvers.AttemptSunder = false;
            Maneuvers.AttemptBullRush = false;
            Maneuvers.AttemptOverrun = false;
            Maneuvers.UsePowerAttack = false;

            // Lich-specific spell preferences
            FleeHealthThreshold = 0f; // never flee — phylactery
            PreferredCastingDistanceSquares = 8;

            if (SpellSelection == null)
                SpellSelection = new SpellSelectionPreferences();

            SpellSelection.BuffBeforeDamage = true;        // open with buffs
            SpellSelection.ConserveHighLevelSpells = true;  // save big spells for big threats
            SpellSelection.CounterEnemySpells = true;       // dispel/counter enemy casters
            SpellSelection.UseUtilitySpells = true;

            if (AOECasting == null)
                AOECasting = new AOECastingPreferences();

            AOECasting.PreferSingleTarget = false;     // prefer AoE for groups
            AOECasting.AvoidHittingAllies = true;
            AOECasting.MinimumEnemiesInAOE = 2;

            // School priorities — necromancy and evocation are lich staples
            if (SchoolPriorities == null)
                SchoolPriorities = new List<SpellSchoolPriority>();

            if (SchoolPriorities.Count == 0)
            {
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Necromancy, 2.0f));
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Evocation, 1.5f));
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Enchantment, 1.3f));
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Abjuration, 1.4f));
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Transmutation, 1.2f));
            }

            EnsureDefaultTags();
        }

        /// <summary>
        /// Lich target scoring:
        /// 1. Heavily prioritise enemy spellcasters (counter-magic strategy)
        /// 2. Score groups for AoE potential
        /// 3. Prefer targets at range (maintain casting distance)
        /// 4. Deprioritise melee bruisers (let minions handle them)
        /// </summary>
        public override float ScoreTarget(CharacterController target, CharacterController self)
        {
            if (target == null || target.Stats == null || target.Stats.IsDead || self == null)
                return float.MinValue;

            float score = base.ScoreTarget(target, self);

            // ── Enemy spellcasters are highest priority ──
            if (target.Stats != null)
            {
                if (target.Stats.IsWizard)
                    score += 8f; // rival arcane caster — primary threat
                if (target.Stats.IsCleric)
                    score += 6f; // divine caster — can turn undead
            }

            // ── Bonus for targets near other enemies (AoE value) ──
            int nearbyEnemies = CountNearbyEnemies(self, target.GridPosition, 4);
            score += nearbyEnemies * 2.5f;

            // ── Prefer targets at casting range, not adjacent ──
            int distance = SquareGridUtils.GetDistance(self.GridPosition, target.GridPosition);
            if (distance >= 3 && distance <= 10)
                score += 3f;
            else if (distance <= 1)
                score -= 4f; // being in melee is bad for a lich

            return score;
        }

        /// <summary>
        /// Enhanced spell scoring for liches:
        /// - Strong bonus for necromancy and control spells
        /// - Extra value for AoE when multiple enemies are clustered
        /// - Buff priority during first rounds of combat
        /// </summary>
        public override float ScoreSpell(
            SpellData spell,
            CharacterController caster,
            CharacterController primaryTarget,
            List<CharacterController> allCombatants,
            GameManager gameManager)
        {
            float score = base.ScoreSpell(spell, caster, primaryTarget, allCombatants, gameManager);

            if (spell == null)
                return score;

            // ── Necromancy spells get lich-specific bonus ──
            SpellSchool school = SpellSchoolUtils.Parse(spell.School);
            if (school == SpellSchool.Necromancy)
                score += 3f;

            // ── Control/debuff spells for battlefield dominance ──
            if (spell.EffectType == SpellEffectType.Control)
                score += 4f;

            // ── Save-or-die / save-or-suck spells ──
            string spellLower = spell.SpellId != null ? spell.SpellId.ToLowerInvariant() : "";
            if (spellLower.Contains("circle_of_death") || spellLower.Contains("flesh_to_stone")
                || spellLower.Contains("phantasmal_killer") || spellLower.Contains("disintegrate"))
            {
                score += 5f;
            }

            // ── Dispel effects against enemy casters ──
            if (spellLower.Contains("dispel") && primaryTarget != null && primaryTarget.Stats != null
                && (primaryTarget.Stats.IsWizard || primaryTarget.Stats.IsCleric))
            {
                score += 6f;
            }

            return score;
        }

        /// <summary>
        /// Liches don't flee — they have phylacteries.
        /// But they do maintain distance from melee threats.
        /// </summary>
        public override bool ShouldIgnoreAoO(CharacterController self)
        {
            return false; // careful positioning
        }

        /// <summary>
        /// Lich will coup de grâce if somehow in melee with a helpless target,
        /// but this should rarely happen given ranged preference.
        /// </summary>
        public override bool ShouldUseCoupDeGrace(CharacterController self)
        {
            return true;
        }

        public override bool ShouldInitiateGrapple(CharacterController self, CharacterController target)
        {
            return false; // never grapple
        }

        public override SpecialAttackType? GetPreferredManeuver(CharacterController self, CharacterController target)
        {
            return null;
        }

        /// <summary>
        /// Liches are more conservative about provoking — they're fragile at melee range
        /// despite their power.
        /// </summary>
        public override float GetRangedAoORiskToleranceMultiplier()
        {
            return 0.5f; // very conservative
        }

        private void EnsureDefaultTags()
        {
            if (TagPriorities == null)
                TagPriorities = new List<TagPriority>();

            if (TagPriorities.Count > 0)
                return;

            TagPriorities.Add(new TagPriority("HP State: Staggered", 3f));
            TagPriorities.Add(new TagPriority("Armor: Unarmored", 2f));
        }

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
