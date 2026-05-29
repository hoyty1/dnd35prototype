using System.Collections.Generic;
using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Vampire AI profile — intelligent predator with diverse combat options:
    /// - Defensive spellcasting first (Mirror Image, Invisibility, buffs)
    /// - Dominate Person on high-value targets (casters, isolated PCs)
    /// - Energy drain slam attacks in melee
    /// - Blood drain via grapple when advantageous
    /// - Gaseous form escape at low HP (flee threshold)
    /// - Target selection favours isolated, low-Will targets for domination
    /// </summary>
    [CreateAssetMenu(fileName = "Vampire AI", menuName = "DND35/AI/Profiles/Vampire")]
    public class VampireAIProfile : SpellcasterAIProfile
    {
        /// <summary>
        /// HP fraction below which the vampire attempts to disengage or use gaseous form.
        /// </summary>
        [Range(0f, 0.5f)]
        public float GaseousFormFleeThreshold = 0.25f;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Vampire";

            if (string.IsNullOrWhiteSpace(Description))
                Description = "Cunning vampire that buffs, dominates, drains, and escapes when threatened.";

            // Vampires fight in melee but open with spells
            CombatStyle = CombatStyle.Melee;
            Aggression = 0.6f;            // moderate — will retreat to cast or flee
            PrioritizeWounded = true;     // finish off weakened targets for blood drain
            PrioritizeIsolated = true;    // dominate/drain isolated victims
            SwitchTargetsOften = false;   // focus one target for domination + drain combo

            if (Movement == null)
                Movement = new MovementPreferences();

            Movement.AvoidAoOs = true;
            Movement.PreferredRangeSquares = 2; // close but not adjacent until ready to strike
            Movement.MaintainDistance = false;
            Movement.SeekFlanking = true;
            Movement.UseCover = true;

            // Vampires use improved grab + blood drain
            GrappleBehavior = GrappleBehavior.InitiateWhenSafe;

            if (Maneuvers == null)
                Maneuvers = new ManeuverPreferences();

            Maneuvers.AttemptTrip = false;
            Maneuvers.AttemptDisarm = false;
            Maneuvers.AttemptSunder = false;
            Maneuvers.AttemptBullRush = false;
            Maneuvers.AttemptOverrun = false;
            Maneuvers.UsePowerAttack = false;

            // Spell preferences — defensive/utility first
            FleeHealthThreshold = GaseousFormFleeThreshold;
            PreferredCastingDistanceSquares = 4;

            if (SpellSelection == null)
                SpellSelection = new SpellSelectionPreferences();

            SpellSelection.BuffBeforeDamage = true;
            SpellSelection.ConserveHighLevelSpells = false; // vampires use dominate aggressively
            SpellSelection.UseUtilitySpells = true;

            if (AOECasting == null)
                AOECasting = new AOECastingPreferences();

            AOECasting.PreferSingleTarget = true; // prefer dominate/charm over AoE
            AOECasting.AvoidHittingAllies = true;

            EnsureDefaultTags();
        }

        /// <summary>
        /// Vampire target scoring:
        /// 1. Strongly prefer low-Will save targets (Dominate Person)
        /// 2. Prefer isolated targets for domination/drain without interference
        /// 3. Prefer wounded targets for blood drain finishing
        /// 4. Prefer targets that are already charmed/dominated (maintain control)
        /// 5. Bonus for high-value targets (casters, high level)
        /// </summary>
        public override float ScoreTarget(CharacterController target, CharacterController self)
        {
            if (target == null || target.Stats == null || target.Stats.IsDead || self == null)
                return float.MinValue;

            float score = base.ScoreTarget(target, self);
            int distance = SquareGridUtils.GetDistance(self.GridPosition, target.GridPosition);

            // ── Low Will save → domination target ──
            if (target.Stats != null)
            {
                int willSave = target.Stats.WillSave;
                if (willSave <= 2)
                    score += 8f;
                else if (willSave <= 5)
                    score += 4f;
                else if (willSave >= 10)
                    score -= 3f;
            }

            // ── Wounded targets → blood drain opportunity ──
            if (target.Stats != null && target.Stats.TotalMaxHP > 0)
            {
                float hpPct = Mathf.Clamp01((float)target.Stats.CurrentHP / target.Stats.TotalMaxHP);
                if (hpPct < 0.3f)
                    score += 6f; // near death — blood drain for the kill
            }

            // ── Charmed/fascinated targets are already under control ──
            if (target.HasCondition(CombatConditionType.Charmed)
                || target.HasCondition(CombatConditionType.Fascinated))
            {
                score -= 5f; // don't waste attacks on dominated targets
            }

            // ── High-value targets ──
            if (target.Stats != null && target.Stats.Level >= 5)
                score += 2f;

            return score;
        }

        /// <summary>
        /// Vampires will grapple to initiate blood drain when they have STR advantage.
        /// </summary>
        public override bool ShouldInitiateGrapple(CharacterController self, CharacterController target)
        {
            if (self == null || target == null || target.Stats == null || self.Stats == null)
                return false;

            // Only grapple when we have clear strength advantage for blood drain
            if (self.Stats.STRMod >= target.Stats.STRMod + 2)
                return true;

            // Grapple helpless/paralysed targets for easy blood drain
            if (target.HasCondition(CombatConditionType.Helpless)
                || target.HasCondition(CombatConditionType.Paralyzed))
                return true;

            return false;
        }

        /// <summary>
        /// Vampires will coup de grâce helpless targets (energy drain kill).
        /// </summary>
        public override bool ShouldUseCoupDeGrace(CharacterController self)
        {
            return true;
        }

        public override SpecialAttackType? GetPreferredManeuver(CharacterController self, CharacterController target)
        {
            // Prefer grapple for blood drain over maneuvers
            if (ShouldInitiateGrapple(self, target))
                return SpecialAttackType.Grapple;

            return null;
        }

        /// <summary>
        /// Boost scoring for control/debuff spells (Dominate, Charm, Hold) and
        /// defensive/buff spells (Mirror Image, Invisibility, etc).
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

            // ── Strong preference for control/charm spells ──
            if (spell.EffectType == SpellEffectType.Control
                || spell.EffectType == SpellEffectType.Debuff)
            {
                score += 6f;

                // Extra bonus for Dominate/Charm (vampire signature)
                string spellLower = spell.SpellId != null ? spell.SpellId.ToLowerInvariant() : "";
                if (spellLower.Contains("dominate") || spellLower.Contains("charm"))
                    score += 4f;
                if (spellLower.Contains("hold"))
                    score += 3f;
            }

            // ── Buff spells when at high HP (open with buffs) ──
            if (spell.EffectType == SpellEffectType.Buff || spell.EffectType == SpellEffectType.Illusion)
            {
                if (caster != null && caster.Stats != null)
                {
                    float hpPct = caster.Stats.TotalMaxHP > 0
                        ? (float)caster.Stats.CurrentHP / caster.Stats.TotalMaxHP
                        : 1f;
                    if (hpPct > 0.75f)
                        score += 5f; // buff early when healthy
                }
            }

            return score;
        }

        public override bool ShouldSwitchTargetsMidFullAttack(CharacterController self)
        {
            return false; // focus one target
        }

        public override bool ShouldTakeFiveFootStepToContinueFullAttack(CharacterController self)
        {
            return true;
        }

        private void EnsureDefaultTags()
        {
            if (TagPriorities == null)
                TagPriorities = new List<TagPriority>();

            if (TagPriorities.Count > 0)
                return;

            TagPriorities.Add(new TagPriority("Armor: Unarmored", 4f));     // easy grapple target
            TagPriorities.Add(new TagPriority("Armor: Heavy Armor", 3f, true)); // hard to grapple
            TagPriorities.Add(new TagPriority("HP State: Staggered", 4f));
            TagPriorities.Add(new TagPriority("HP State: Disabled", 3f));
        }
    }
}
