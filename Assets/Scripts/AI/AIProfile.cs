using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DND35.AI
{
    /// <summary>
    /// Data-driven AI profile for NPC behavior.
    /// Assign to CharacterController.aiProfile.
    /// </summary>
    [CreateAssetMenu(fileName = "New AI Profile", menuName = "DND35/AI/AI Profile")]
    public class AIProfile : ScriptableObject
    {
        [Header("Identity")]
        public string ProfileName = "Default AI";

        [TextArea(2, 6)]
        public string Description = "";

        [Header("Combat Style")]
        public CombatStyle CombatStyle = CombatStyle.Melee;

        [Range(0f, 1f)]
        [Tooltip("High aggression = less retreating and more direct pressure.")]
        public float Aggression = 0.5f;

        [Header("Target Selection")]
        [Tooltip("Weighted target tag preferences.")]
        public List<TagPriority> TagPriorities = new List<TagPriority>();

        public bool PrioritizeWounded;
        public bool PrioritizeIsolated;
        public bool SwitchTargetsOften;

        /// <summary>
        /// When true, target selection strongly prefers enemies with lower concealment miss chance.
        /// </summary>
        public virtual bool PrioritizeVisibleTargets => true;

        /// <summary>
        /// Multiplier applied to concealment visibility bonuses/penalties during target selection.
        /// 0 disables concealment weighting, 1 uses default weighting, values >1 increase weighting.
        /// </summary>
        public virtual float ConcealmentPenaltyMultiplier => 1f;

        [Header("Movement")]
        public MovementPreferences Movement = new MovementPreferences();

        [Header("Grapple")]
        public GrappleBehavior GrappleBehavior = GrappleBehavior.EscapeOnly;

        [Header("Maneuvers")]
        public ManeuverPreferences Maneuvers = new ManeuverPreferences();

#if UNITY_EDITOR
        [Header("Custom")]
        [Tooltip("Optional metadata reference for custom extension script.")]
        public MonoScript CustomAIScript;
#endif

        public virtual float ScoreTarget(CharacterController target, CharacterController self)
        {
            if (target == null || target.Stats == null || target.Stats.IsDead || self == null)
                return float.MinValue;

            float score = 10f;

            // Tag priorities
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

            // Wounded bonus
            if (PrioritizeWounded && target.Stats.TotalMaxHP > 0)
            {
                float hpPct = Mathf.Clamp01((float)target.Stats.CurrentHP / target.Stats.TotalMaxHP);
                score += (1f - hpPct) * 8f;
            }

            // Isolated bonus (simple approximation by adjacent allies)
            if (PrioritizeIsolated)
            {
                int adjacentAllies = CountAdjacentAllies(target);
                if (adjacentAllies == 0)
                    score += 4f;
                else
                    score -= adjacentAllies * 1.25f;
            }

            int distance = SquareGridUtils.GetDistance(self.GridPosition, target.GridPosition);
            if (CombatStyle == CombatStyle.Ranged)
            {
                int preferred = Mathf.Max(1, Movement != null ? Movement.PreferredRangeSquares : 4);
                score -= Mathf.Abs(distance - preferred) * 0.9f;
                if (distance <= 1)
                    score -= 5f;
            }
            else if (CombatStyle == CombatStyle.Melee)
            {
                score += Mathf.Max(0, 6 - distance) * 0.7f;
            }

            return score;
        }

        public virtual bool ShouldEscapeGrapple(CharacterController self)
        {
            return GrappleBehavior == GrappleBehavior.Avoid || GrappleBehavior == GrappleBehavior.EscapeOnly;
        }

        /// <summary>
        /// Multiplier for how much AoO risk this profile tolerates before preferring safer ranged tactics.
        /// Lower values = more conservative behavior.
        /// </summary>
        public virtual float GetRangedAoORiskToleranceMultiplier()
        {
            return 1f;
        }

        /// <summary>
        /// Profile-level hook for charge preference after baseline charge validity has passed.
        /// Return true to take charge pressure, false to continue with normal attack/move flow.
        /// </summary>
        public virtual bool ShouldPreferCharge(CharacterController self, CharacterController target, int distanceSquares, bool preferAggression)
        {
            if (self == null || target == null)
                return false;

            // Default preserves existing behavior: if charging is legally available, profiles allow it.
            return true;
        }

        /// <summary>
        /// When true, profile target selection should ignore unconscious targets as long as at least one conscious enemy is available.
        /// </summary>
        public virtual bool ShouldIgnoreUnconsciousTargets(CharacterController self)
        {
            return false;
        }

        /// <summary>
        /// When true, this profile can swap targets mid full-attack sequence if the current target drops unconscious/dead.
        /// </summary>
        public virtual bool ShouldSwitchTargetsMidFullAttack(CharacterController self)
        {
            return false;
        }

        /// <summary>
        /// When true, this profile is allowed to spend an available 5-foot step to continue a full attack on a new target.
        /// </summary>
        public virtual bool ShouldTakeFiveFootStepToContinueFullAttack(CharacterController self)
        {
            return false;
        }

        /// <summary>
        /// When true, this profile will execute adjacent helpless targets with Coup de Grace when legal.
        /// </summary>
        public virtual bool ShouldUseCoupDeGrace(CharacterController self)
        {
            return false;
        }

        /// <summary>
        /// When true, this profile is willing to ignore attacks of opportunity while moving.
        /// </summary>
        public virtual bool ShouldIgnoreAoO(CharacterController self)
        {
            return false;
        }

        /// <summary>
        /// Optional per-profile weapon fallback hook used at attack time.
        /// Return true if this profile changed weapon state this turn.
        /// </summary>
        public virtual bool TryEnsureWeaponFallback(CharacterController self)
        {
            return false;
        }

        /// <summary>
        /// AI choice, not a rule: whether a creature with an unused smite (a paladin's Smite Evil, a celestial or fiendish
        /// template smite) declares it before attacking <paramref name="target"/> (CHR-020). The rules (SmiteRules) are
        /// shared with the PC Smite button. <see cref="DefaultShouldDeclareSmite"/>.
        /// </summary>
        public virtual bool ShouldDeclareSmite(CharacterController self, CharacterController target)
            => DefaultShouldDeclareSmite(self, target);

        /// <summary>
        /// The default smite choice, also used for creatures without a profile: smite the first target the creature
        /// believes is of the smitten alignment (SmiteRules.AiBelievesSmiteFoe), never one it does not.
        /// </summary>
        public static bool DefaultShouldDeclareSmite(CharacterController self, CharacterController target)
        {
            if (!SmiteRules.CanDeclare(self, out _, out _))
                return false;
            return SmiteRules.AiBelievesSmiteFoe(self, target);
        }

        /// <summary>
        /// AI choice, not a rule: the bard's bardic music this turn (CHR-020, CHR-026): start a song, concentrate on it,
        /// make a suggestion to a fascinated creature, stop (free, to cast while Inspire Courage lingers), or nothing
        /// (null). The rules (BardicMusicRules) are shared with the PC Bardic Music button. <see cref="DefaultBardicMusicChoice"/>.
        /// </summary>
        public virtual BardicMusicChoice ChooseBardicMusic(CharacterController self, IList<CharacterController> combatants)
            => DefaultBardicMusicChoice(self, combatants);

        /// <summary>
        /// AI choice, not a rule: whether to spend this turn's standard action keeping the caster's concentration spell
        /// going (PHB p.176; ConcentrationRules, decision D2, batch 11 step 16) rather than use it otherwise, which ends the
        /// spell (or starts its tail) at the end of the turn, or at once if it casts (decision D1). The rule is shared with
        /// the PC's Maintain button. <see cref="DefaultShouldMaintainConcentration"/>.
        /// </summary>
        /// <param name="otherSpellLevel">The level of the spell the caster would cast now (AIService.SelectSpell), or -1 for none.</param>
        /// <param name="canAttackNow">The caster can attack a foe without moving (in its weapon's reach or range), or is
        /// grappling and could make a grapple attack instead.</param>
        /// <param name="holdsSummon">The caster keeps a creature summoned by the spell (Summon Swarm).</param>
        public virtual bool ShouldMaintainConcentration(CharacterController self, IList<CharacterController> combatants, int otherSpellLevel, bool canAttackNow, bool holdsSummon)
            => DefaultShouldMaintainConcentration(self, combatants, otherSpellLevel, canAttackNow, holdsSummon);

        /// <summary>
        /// The default choice, also for creatures without a profile: maintain when the spell is worth more than the other
        /// standard action, and always when there is no other (letting it lapse would gain nothing). The spell's worth: 3
        /// when its effect sits on a foe (a foe it holds), 2 when it sits on an ally or keeps a summoned creature, 1 when it
        /// sits on the caster or on no creature (an image, a detection); 1 less when letting it lapse still keeps it 2 rounds
        /// or more (its "+N rounds" tail), but never below 1. The other action's worth: 2 for a spell of 1st level or higher
        /// the AI would cast, 1 for a cantrip or an attack it can make without moving, else 0.
        /// </summary>
        public static bool DefaultShouldMaintainConcentration(CharacterController self, IList<CharacterController> combatants, int otherSpellLevel, bool canAttackNow, bool holdsSummon)
        {
            if (!ConcentrationRules.CanMaintain(self, out _))
                return false;
            ActiveSpellEffect effect = self.Concentration.ConcentratingOn;
            int worth = holdsSummon ? 2 : 1;
            if (effect != null && combatants != null)
            {
                for (int i = 0; i < combatants.Count; i++)
                {
                    CharacterController c = combatants[i];
                    if (c == null || c == self || c.Stats == null || c.IsDead || c.StatusEffectManager == null)
                        continue;
                    if (c.StatusEffectManager.ActiveEffects.Contains(effect))
                        worth = Mathf.Max(worth, TeamUtility.IsEnemy(self, c) ? 3 : 2);
                }
            }
            if (effect != null && ConcentrationRules.TailRounds(effect.Spell, effect.CasterLevel) >= 2)
                worth = Mathf.Max(1, worth - 1);
            int other = otherSpellLevel >= 1 ? 2 : (otherSpellLevel == 0 || canAttackNow) ? 1 : 0;
            return other == 0 || worth > other;
        }

        /// <summary>
        /// The default bardic music choice, also used for creatures without a profile:
        /// - fascinating: a suggestion to a fascinated foe when the bard has it, else concentrate to keep the song up;
        /// - inspiring competence: concentrate;
        /// - singing Inspire Courage: stop (no action) when the bard has a spell to cast, so the song lingers 5 rounds while
        ///   it casts; otherwise keep singing;
        /// - not performing, with a living foe: Fascinate the best foes it can (highest Hit Dice first, up to its count) when
        ///   it can fascinate two, or one while Inspire Courage still lingers or no one else would gain from it; else Inspire
        ///   Courage when no song of its own lingers and it would help two creatures (or the bard alone, when it has no
        ///   spell to cast). The AI never starts Countersong or Inspire Competence (AI-067).
        /// </summary>
        public static BardicMusicChoice DefaultBardicMusicChoice(CharacterController self, IList<CharacterController> combatants)
        {
            if (self == null || self.Stats == null || combatants == null || !BardicMusicRules.HasBardicMusic(self.Stats))
                return null;
            BardicMusicData d = BardicMusicRules.Data(self.Stats);
            if (d == null)
                return null;

            bool anyFoe = false;
            for (int i = 0; i < combatants.Count; i++)
            {
                CharacterController c = combatants[i];
                if (c != null && c.Stats != null && !c.IsDead && !c.IsUnconscious && TeamUtility.IsEnemy(self, c))
                {
                    anyFoe = true;
                    break;
                }
            }

            switch (d.ActiveAbility)
            {
                case BardicAbility.Fascinate:
                    for (int i = 0; i < d.FascinatedTargets.Count; i++)
                    {
                        CharacterController t = d.FascinatedTargets[i];
                        if (TeamUtility.IsEnemy(self, t) && BardicMusicRules.SuggestionTargetProblem(self, t) == null)
                            return BardicMusicChoice.Suggest(t);
                    }
                    return BardicMusicRules.CanConcentrate(self, out _) ? BardicMusicChoice.Concentrate() : null;
                case BardicAbility.InspireCompetence:
                    return BardicMusicRules.CanConcentrate(self, out _) ? BardicMusicChoice.Concentrate() : null;
                case BardicAbility.InspireCourage:
                    return anyFoe && BardicMusicRules.HasCastableSpellIgnoringPerformance(self) ? BardicMusicChoice.Stop() : null;
                case BardicAbility.Countersong:
                    return null;
            }
            if (!anyFoe)
                return null;

            bool icLingers = d.LingerRoundsRemaining > 1;
            int icGain = 0;
            for (int i = 0; i < combatants.Count; i++)
            {
                CharacterController c = combatants[i];
                if (BardicMusicRules.IsInspireCourageRecipient(self, c) && !c.Stats.HasInspireCourageBonus)
                    icGain++;
            }

            if (BardicMusicRules.CanStart(self, BardicAbility.Fascinate, out _))
            {
                List<CharacterController> candidates = BardicMusicRules.FascinateCandidates(self, combatants);
                candidates.RemoveAll(c => !TeamUtility.IsEnemy(self, c));
                if (candidates.Count > 0 && (candidates.Count >= 2 || icLingers || icGain <= 1))
                {
                    candidates.Sort((a, b) =>
                    {
                        int hd = TeamUtility.GetHitDice(b).CompareTo(TeamUtility.GetHitDice(a));
                        return hd != 0 ? hd : self.GetMinimumDistanceToTarget(a, chebyshev: false).CompareTo(self.GetMinimumDistanceToTarget(b, chebyshev: false));
                    });
                    int cap = d.FascinateTargetCount;
                    if (candidates.Count > cap)
                        candidates.RemoveRange(cap, candidates.Count - cap);
                    return BardicMusicChoice.Start(BardicAbility.Fascinate, candidates);
                }
            }

            if (!icLingers && BardicMusicRules.CanStart(self, BardicAbility.InspireCourage, out _)
                && (icGain >= 2 || (icGain >= 1 && !BardicMusicRules.HasCastableSpellIgnoringPerformance(self))))
                return BardicMusicChoice.Start(BardicAbility.InspireCourage, null);
            return null;
        }

        /// <summary>
        /// AI choice, not a rule: whether a paladin lays on hands this turn, and on whom (CHR-020). The rules
        /// (LayOnHandsRules) are shared with the PC Lay on Hands button. Null for no lay on hands.
        /// <see cref="DefaultLayOnHandsTarget"/>.
        /// </summary>
        public virtual CharacterController ChooseLayOnHandsTarget(CharacterController self, System.Collections.Generic.IList<CharacterController> combatants)
            => DefaultLayOnHandsTarget(self, combatants);

        /// <summary>
        /// The default lay on hands choice: heal the most hurt creature within touch, itself or an ally, once it is at half
        /// its hit points or less (dying included); else, when no one needs healing, touch an undead foe within reach that
        /// the remaining pool reduces to 0 hit points (destroyed once CRE-051 is fixed; until then it is disabled at 0).
        /// Otherwise null (the creature fights as usual).
        /// </summary>
        public static CharacterController DefaultLayOnHandsTarget(CharacterController self, System.Collections.Generic.IList<CharacterController> combatants)
        {
            if (self == null || self.Stats == null || combatants == null)
                return null;
            if (!LayOnHandsRules.CanUse(self, out _))
                return null;

            int pool = LayOnHandsRules.RemainingPool(self.Stats);
            CharacterController healPick = null;
            float healPickFraction = float.MaxValue;
            CharacterController harmPick = null;
            for (int i = 0; i < combatants.Count; i++)
            {
                CharacterController c = combatants[i];
                if (c == null || c.Stats == null || c.Stats.IsDead)
                    continue;
                if (!LayOnHandsRules.IsValidTarget(self, c, out bool harms, out _))
                    continue;
                if (harms)
                {
                    if (harmPick == null && TeamUtility.IsEnemy(self, c) && c.Stats.CurrentHP <= pool)
                        harmPick = c;
                    continue;
                }
                if (c != self && !TeamUtility.IsAlly(self, c))
                    continue;
                int max = Mathf.Max(1, c.Stats.TotalMaxHP);
                float fraction = (float)c.Stats.CurrentHP / max;
                if (fraction > 0.5f)
                    continue;
                if (fraction < healPickFraction)
                {
                    healPick = c;
                    healPickFraction = fraction;
                }
            }
            return healPick ?? harmPick;
        }

        /// <summary>
        /// AI choice, not a rule: whether a druid takes a wild shape this turn, and which form (CHR-020). The rules
        /// (WildShapeRules) are shared with the PC Wild Shape button. Null for no wild shape. <see cref="DefaultWildShapeForm"/>.
        /// </summary>
        public virtual NPCDefinition ChooseWildShapeForm(CharacterController self, System.Collections.Generic.IList<CharacterController> combatants)
            => DefaultWildShapeForm(self, combatants);

        /// <summary>
        /// The default wild shape choice, also used for creatures without a profile: the available form with the best
        /// expected melee damage per full attack against the nearest foe's AC (each natural attack at the druid's base
        /// attack bonus with the form's Strength and size, a secondary attack at -5), taken when it beats the druid's
        /// own weapon attacks, and only while the druid has no castable spell left or a foe already stands next to it (a
        /// caster keeps casting while it can). Null otherwise, or when no foe is left.
        /// </summary>
        public static NPCDefinition DefaultWildShapeForm(CharacterController self, System.Collections.Generic.IList<CharacterController> combatants)
        {
            if (self == null || self.Stats == null || combatants == null)
                return null;
            if (!WildShapeRules.CanAssume(self, out _))
                return null;

            CharacterController nearest = null;
            int nearestDistance = int.MaxValue;
            for (int i = 0; i < combatants.Count; i++)
            {
                CharacterController c = combatants[i];
                // A dying, unconscious or dead foe is no threat (CHR-031); distance is footprint to footprint, so a
                // Large foe touching the druid counts as adjacent.
                if (c == null || c == self || c.Stats == null || c.IsDead || c.IsUnconscious || !TeamUtility.IsEnemy(self, c))
                    continue;
                int d = self.GetMinimumDistanceToTarget(c, chebyshev: false);
                if (d < nearestDistance)
                {
                    nearest = c;
                    nearestDistance = d;
                }
            }
            if (nearest == null)
                return null;

            SpellcastingComponent spells = self.Spellcasting;
            bool canStillCast = spells != null && spells.CanCastSpells && spells.HasAnyCastablePreparedSpell();
            if (canStillCast && nearestDistance > 1)
                return null;

            int targetAC = nearest.Stats.ArmorClass;
            float own = ExpectedWeaponDamage(self.Stats, targetAC);
            NPCDefinition best = null;
            float bestScore = own;
            System.Collections.Generic.List<NPCDefinition> forms = WildShapeRules.GetAvailableForms(self.Stats);
            for (int i = 0; i < forms.Count; i++)
            {
                float score = ExpectedFormDamage(self.Stats, forms[i], targetAC);
                if (score > bestScore)
                {
                    best = forms[i];
                    bestScore = score;
                }
            }
            return best;
        }

        /// <summary>Expected damage of a full attack in <paramref name="form"/> (AI estimate; see DefaultWildShapeForm).</summary>
        public static float ExpectedFormDamage(CharacterStats stats, NPCDefinition form, int targetAC)
        {
            if (stats == null || form == null || form.NaturalAttacks == null)
                return 0f;
            int strMod = CharacterStats.GetAbilityModifier(form.STR);
            int baseBonus = stats.BaseAttackBonus + strMod + form.SizeCategory.GetAttackAndAcModifier();
            float total = 0f;
            for (int i = 0; i < form.NaturalAttacks.Count; i++)
            {
                NaturalAttackDefinition a = form.NaturalAttacks[i];
                if (a == null)
                    continue;
                int bonus = baseBonus + (a.IsPrimary ? 0 : -5);
                float average = Mathf.Max(1f, Mathf.Max(1, a.DamageCount) * (a.DamageDice + 1) / 2f + a.GetDamageBonus(strMod));
                total += Mathf.Max(1, a.Count) * HitChance(bonus, targetAC) * average;
            }
            return total;
        }

        /// <summary>Expected damage of the creature's own full attack with its current weapon (AI estimate).</summary>
        public static float ExpectedWeaponDamage(CharacterStats stats, int targetAC)
        {
            if (stats == null)
                return 0f;
            int bab = stats.BaseAttackBonus;
            int bonus = bab + stats.STRMod + stats.SizeModifier;
            float average = Mathf.Max(1f, Mathf.Max(1, stats.BaseDamageCount) * (stats.BaseDamageDice + 1) / 2f + stats.STRMod + stats.BonusDamage);
            float total = 0f;
            for (int step = 0; step == 0 || bab - 5 * step >= 1; step++)
                total += HitChance(bonus - 5 * step, targetAC) * average;
            return total;
        }

        /// <summary>
        /// AI choice, not a rule: whether a monk declares a flurry of blows before its melee sequence (CHR-003). The rules
        /// (CharacterController.CanUseFlurryOfBlows) are shared with the PC Flurry button. <see cref="DefaultShouldUseFlurryOfBlows"/>.
        /// </summary>
        public virtual bool ShouldUseFlurryOfBlows(CharacterController self, CharacterController target)
            => DefaultShouldUseFlurryOfBlows(self, target);

        /// <summary>
        /// The default flurry choice, also used for creatures without a profile: flurry when the flurry's expected hits
        /// against the target's AC (each step at the flurry penalty, PHB p.40) beat those of the plain iterative attacks,
        /// which is nearly always while a hit needs less than a natural 20. Damage per hit is taken as equal.
        /// </summary>
        public static bool DefaultShouldUseFlurryOfBlows(CharacterController self, CharacterController target)
        {
            if (self == null || self.Stats == null || target == null || target.Stats == null)
                return false;
            if (!self.CanUseFlurryOfBlows(out _))
                return false;

            ItemData weapon = self.GetEquippedMainWeapon();
            // Every per-attack term except the BAB step (ability, size, feats, effects, conditions).
            int otherTerms = self.BuildAttackBonus(target, weapon, false, null, false, 0, 0,
                CharacterController.IsWeaponTwoHanded(weapon)).Total;
            int targetAC = target.Stats.ArmorClass;
            int penalty = self.Stats.FlurryOfBlowsAttackPenalty;

            float flurryHits = 0f;
            foreach (int bab in self.Stats.GetFlurryOfBlowsBaseAttackBonuses())
                flurryHits += HitChance(bab + penalty + otherTerms, targetAC);

            float plainHits = 0f;
            // The plain ladder (CMB-044), not the controller's current steps, which are the flurry's while flurrying.
            foreach (int bab in CharacterCombatStats.GetBaseAttackLadder(self.Stats.BaseAttackBonus))
                plainHits += HitChance(bab + otherTerms, targetAC);

            return flurryHits > plainHits;
        }

        /// <summary>
        /// AI stopgap (batch 13 catalog), AI-055 (owner decision Q2, 2026-10-23): whether the creature declares Rapid Shot
        /// for its ranged attack on <paramref name="target"/> with <paramref name="weapon"/>. The rules (CharacterController
        /// .CanDeclareRapidShot, the declaration before the first attack, D9) are the PC toggle's.
        /// <see cref="DefaultShouldUseRapidShot"/>.
        /// </summary>
        public virtual bool ShouldUseRapidShot(CharacterController self, CharacterController target, ItemData weapon)
            => DefaultShouldUseRapidShot(self, target, weapon);

        /// <summary>
        /// The default Rapid Shot choice, also used for creatures without a profile (AI stopgap, batch 13 catalog): declare it
        /// when it may be declared now (the feat, no attack made yet, the full-round action free) and the expected hits of the
        /// turn's shots with it (one extra shot at the highest bonus, -2 on every shot, PHB p.99) beat those without it.
        /// Damage per hit is taken as equal; Haste's extra shot is in both. A thrown weapon (a javelin, or a dagger the NPC
        /// throws, PHB p.113) counts only the throws it has weapons for: the one in hand and the like ones it carries.
        /// </summary>
        public static bool DefaultShouldUseRapidShot(CharacterController self, CharacterController target, ItemData weapon)
        {
            if (self == null || self.Stats == null || target == null || target.Stats == null || weapon == null)
                return false;
            if (!self.CanDeclareRapidShot(weapon, true, out _))
                return false;

            int otherTerms = self.BuildAttackBonus(target, weapon, true, null, false, 0, 0,
                CharacterController.IsWeaponTwoHanded(weapon)).Total;
            int targetAC = target.Stats.ArmorClass;
            System.Collections.Generic.List<int> ladder = CharacterCombatStats.GetBaseAttackLadder(self.Stats.BaseAttackBonus);
            if (ladder.Count == 0)
                return false;

            int shots = ThrowsAvailable(self, weapon);
            float plain = 0f;
            float rapid = 0f;
            var rapidLadder = new System.Collections.Generic.List<int> { ladder[0] };
            rapidLadder.AddRange(ladder);
            for (int i = 0; i < ladder.Count && i < shots; i++)
                plain += HitChance(ladder[i] + otherTerms, targetAC);
            for (int i = 0; i < rapidLadder.Count && i < shots; i++)
                rapid += HitChance(rapidLadder[i] + otherTerms + RapidShotPenalty, targetAC);
            return rapid > plain;
        }

        /// <summary>The throws <paramref name="weapon"/> allows this turn: unlimited for a projectile weapon; for a thrown one the weapon in hand plus the like throwable weapons carried (the post-attack draw's pick, GameManager.TryEquipNextThrowableWeapon).</summary>
        private static int ThrowsAvailable(CharacterController self, ItemData weapon)
        {
            if (!GameManager.IsThrowableWeapon(weapon))
                return int.MaxValue;
            int count = 1;
            Inventory inv = self.InventoryComp != null ? self.InventoryComp.CharacterInventory : null;
            if (inv != null && inv.GeneralSlots != null)
                foreach (ItemData item in inv.GeneralSlots)
                    if (GameManager.IsThrowableWeapon(item) && item.WeaponCat == weapon.WeaponCat)
                        count++;
            return count;
        }

        private const int RapidShotPenalty = -2; // PHB p.99

        /// <summary>
        /// AI stopgap (batch 13 catalog), AI-055 (owner decision Q2, 2026-10-23): whether the creature fights with two weapons
        /// in its melee attack on <paramref name="target"/>. The rules (the off-hand steps of GameManager.RunAttackSequence,
        /// CharacterController.GetDualWieldPenalties, GetOffHandAttackCount) are the PC Off-Hand button's.
        /// <see cref="DefaultShouldUseTwoWeaponFighting"/>.
        /// </summary>
        public virtual bool ShouldUseTwoWeaponFighting(CharacterController self, CharacterController target)
            => DefaultShouldUseTwoWeaponFighting(self, target);

        /// <summary>
        /// The default two-weapon choice, also used for creatures without a profile (AI stopgap, batch 13 catalog): fight with
        /// two weapons when it holds one in each hand (CanDualWield; a shield bash only with Improved Shield Bash, since the
        /// lost shield AC is a cost it cannot weigh), the turn can still become a full attack (PHB p.143:
        /// the off-hand attack needs the full attack action), no flurry is declared, and the expected damage of the turn's
        /// attacks with both weapons (each main-hand step at the main-hand penalty, each off-hand step at the off-hand
        /// penalty with half the Strength bonus, PHB p.160, p.134) beats that of the main-hand attacks alone.
        /// </summary>
        public static bool DefaultShouldUseTwoWeaponFighting(CharacterController self, CharacterController target)
        {
            if (self == null || self.Stats == null || target == null || target.Stats == null)
                return false;
            if (!self.CanDualWield() || self.IsUsingFlurryOfBlows || !self.CanStillReachFullAttack())
                return false;
            if (self.ProgressiveAttackPool.HasStartedAttacking)
                return false;
            // A shield bash in the off hand costs the shield's AC until the next turn (PHB p.125) unless Improved Shield Bash
            // keeps it (PHB p.96): a defensive cost this damage-only stopgap cannot weigh, so it does not bash.
            if (self.IsDualWieldOffHandShieldBash() && !FeatManager.HasImprovedShieldBash(self.Stats))
                return false;
            ItemData main = self.GetDualWieldMainWeapon();
            ItemData off = self.GetDualWieldOffHandWeapon();
            int offCount = self.GetOffHandAttackCount();
            if (main == null || off == null || offCount <= 0)
                return false;

            var (mainPenalty, offPenalty, _) = self.GetDualWieldPenalties();
            int mainTerms = self.BuildAttackBonus(target, main, false, null, false, 0, 0, false).Total;
            int offTerms = self.BuildAttackBonus(target, off, false, null, false, 0, 0, false).Total;
            float mainDamage = AverageWeaponDamage(self, main, offHand: false);
            float offDamage = AverageWeaponDamage(self, off, offHand: true);
            int targetAC = target.Stats.ArmorClass;
            int bab = self.Stats.BaseAttackBonus;

            float plain = 0f;
            float twoWeapons = 0f;
            foreach (int step in CharacterCombatStats.GetBaseAttackLadder(bab))
            {
                plain += HitChance(step + mainTerms, targetAC) * mainDamage;
                twoWeapons += HitChance(step + mainTerms + mainPenalty, targetAC) * mainDamage;
            }
            for (int i = 0; i < offCount; i++)
                twoWeapons += HitChance(self.GetOffHandAttackBAB(i) + offTerms + offPenalty, targetAC) * offDamage;
            return twoWeapons > plain;
        }

        /// <summary>The average damage of one hit with <paramref name="weapon"/> (dice at the wielder's size, the Strength share, its flat bonus): an AI estimate.</summary>
        private static float AverageWeaponDamage(CharacterController self, ItemData weapon, bool offHand)
        {
            self.GetScaledWeaponDamageDice(weapon, out int count, out int dice);
            float average = Mathf.Max(1, count) * (dice + 1) / 2f + self.Stats.GetWeaponDamageModifier(weapon, offHand) + weapon.BonusDamage;
            return Mathf.Max(1f, average);
        }

        // d20 + modifier against AC: a natural 1 misses and a natural 20 hits (PHB p.134).
        private static float HitChance(int attackModifier, int targetAC)
            => Mathf.Clamp((21 + attackModifier - targetAC) / 20f, 0.05f, 0.95f);

        public virtual bool ShouldInitiateGrapple(CharacterController self, CharacterController target)
        {
            if (self == null || target == null || target.Stats == null)
                return false;

            if (self.Stats != null && self.Stats.HasImprovedGrab)
                return false;

            if (GrappleBehavior == GrappleBehavior.Avoid || GrappleBehavior == GrappleBehavior.EscapeOnly)
                return false;

            if (GrappleBehavior == GrappleBehavior.Aggressive)
                return true;

            if (GrappleBehavior == GrappleBehavior.Maintain)
                return self.IsGrappling();

            // InitiateWhenSafe
            return self.Stats != null && self.Stats.STRMod >= target.Stats.STRMod;
        }

        /// <summary>
        /// AI choice after an Improved Grab trigger hit (MM p.310; owner ruling 2026-10-11, CMB-148): grapple normally
        /// (false) or hold with the body part that hit (true: -20 on grapple checks, the free one included, but not
        /// grappled, so it keeps threatening and attacking others). <see cref="DefaultShouldHoldWithImprovedGrab"/>.
        /// </summary>
        public virtual bool ShouldHoldWithImprovedGrab(CharacterController self, CharacterController target, IList<CharacterController> allCharacters)
            => DefaultShouldHoldWithImprovedGrab(self, target, allCharacters);

        /// <summary>
        /// The default hold choice, also used for creatures without a profile: hold only while another able enemy is
        /// within the creature's reach (the hold's point is to keep fighting it) and the -20 still leaves the free grapple
        /// check at least even odds (its modifier -20 at least the target's).
        /// </summary>
        public static bool DefaultShouldHoldWithImprovedGrab(CharacterController self, CharacterController target, IList<CharacterController> allCharacters)
        {
            if (self == null || target == null || self.Stats == null || target.Stats == null || allCharacters == null)
                return false;

            if (self.GetGrappleModifier() + CharacterController.ImprovedGrabHoldGrappleCheckPenalty < target.GetGrappleModifier())
                return false;

            return HasOtherEnemyInReach(self, target, allCharacters);
        }

        /// <summary>True when an able enemy of <paramref name="self"/> other than <paramref name="excluded"/> is within its reach.</summary>
        public static bool HasOtherEnemyInReach(CharacterController self, CharacterController excluded, IList<CharacterController> allCharacters)
        {
            if (self == null || allCharacters == null)
                return false;

            for (int i = 0; i < allCharacters.Count; i++)
            {
                CharacterController other = allCharacters[i];
                if (other == null || other == self || other == excluded || other.Stats == null || other.Stats.IsDead || other.IsUnconscious)
                    continue;
                if (!TeamUtility.IsEnemy(self, other))
                    continue;
                if (self.IsTargetInCurrentWeaponRange(other))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Validates whether trip is meaningful against the current target: it is standing, and, when
        /// <paramref name="self"/> is given, the shared trip legality allows it (size, swarm, incorporeal;
        /// CharacterController.CanTrip, PHB p.158, CMB-079).
        /// </summary>
        protected virtual bool IsValidTripTarget(CharacterController target, CharacterController self = null)
        {
            if (target == null)
            {
                Debug.LogWarning("[AI Validation] Trip target is null.");
                return false;
            }

            if (target.HasCondition(CombatConditionType.Prone))
            {
                Debug.Log($"[AI Validation] Cannot trip {target.Stats?.CharacterName ?? "Unknown"} - target is already prone.");
                return false;
            }

            if (self != null && !self.CanTrip(target, out string reason))
            {
                Debug.Log($"[AI Validation] Cannot trip {target.Stats?.CharacterName ?? "Unknown"} - {reason}.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Lowest chance of winning the opposed check at which an AI-run creature trips back after a failed
        /// trip against it (PHB p.158, CMB-079). A failed counter-trip costs nothing by RAW, so the AI declines
        /// only a clearly bad one: about 1 in 10 or worse, a check some 10 or more points weaker.
        /// </summary>
        public const float CounterTripMinimumWinChance = 0.10f;

        /// <summary>
        /// The defender's choice after a failed trip against <paramref name="self"/> (PHB p.158): whether this
        /// AI-run creature tries to trip <paramref name="tripper"/> back. Asked by AIService.ShouldCounterTrip
        /// only when the rule allows it (CharacterController.CanCounterTrip). The default is
        /// <see cref="DefaultShouldCounterTrip"/>; a profile may override it.
        /// </summary>
        public virtual bool ShouldCounterTrip(CharacterController self, CharacterController tripper)
        {
            return DefaultShouldCounterTrip(self, tripper);
        }

        /// <summary>
        /// Default counter-trip choice (CMB-079; owner direction 2026-10-07: counter-trip unless it is clearly
        /// bad). Declines against an ally (TeamUtility.IsAlly: a tripper on this creature's own side, such as a
        /// confused or charmed companion; the rule lets any defender react, so the side is this choice's
        /// concern, owner ruling 2026-10-08, CMB-136); when the reaction would end this creature's invisibility
        /// or Sanctuary (it counts as an attack); or when its chance to win the opposed check is below
        /// <see cref="CounterTripMinimumWinChance"/> (CharacterController.EstimateOpposedCheckWinChance with
        /// the counter-trip modifiers); otherwise trips back.
        /// </summary>
        public static bool DefaultShouldCounterTrip(CharacterController self, CharacterController tripper)
        {
            if (self == null || tripper == null || self.Stats == null || tripper.Stats == null)
                return false;

            if (TeamUtility.IsAlly(self, tripper))
                return false;

            if (self.HasActiveInvisibilityEffect || self.Stats.SanctuaryActive)
                return false;

            float chance = CharacterController.EstimateOpposedCheckWinChance(
                self.GetTripAttackerCheckModifier(),
                tripper.GetTripOrOverrunDefenderCheckModifier());
            return chance >= CounterTripMinimumWinChance;
        }

        /// <summary>
        /// Validates whether target currently has a disarmable held item.
        /// </summary>
        protected virtual bool IsValidDisarmTarget(CharacterController target)
        {
            if (target == null)
            {
                Debug.LogWarning("[AI Validation] Disarm target is null.");
                return false;
            }

            bool hasDisarmable = target.HasDisarmableWeaponEquipped();
            if (!hasDisarmable)
            {
                Debug.Log($"[AI Validation] Cannot disarm {target.Stats?.CharacterName ?? "Unknown"} - no disarmable held weapon.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates whether target currently has a sunderable item equipped.
        /// </summary>
        protected virtual bool IsValidSunderTarget(CharacterController target)
        {
            if (target == null)
            {
                Debug.LogWarning("[AI Validation] Sunder target is null.");
                return false;
            }

            bool hasSunderable = target.HasSunderableItemEquipped();
            if (!hasSunderable)
            {
                Debug.Log($"[AI Validation] Cannot sunder {target.Stats?.CharacterName ?? "Unknown"} - no sunderable equipment.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Shared bull rush legality (size, swarm, incorporeal, grappling, adjacency; PHB p.154),
        /// the same check the PC wrapper uses (CharacterController.CanBullRush, CMB-102).
        /// </summary>
        protected virtual bool IsValidBullRushTarget(CharacterController target, CharacterController self)
        {
            return self != null && self.CanBullRush(target, false, out _);
        }

        /// <summary>
        /// After a successful bull rush (PHB p.154): stay and push the defender 5 ft (follow false,
        /// return 1), or move with it and push 1 to <paramref name="maxIfFollowing"/> squares (follow
        /// true). Asked by AIService.ChooseBullRushPush for a non-controllable attacker; the PC
        /// prompt makes the same choice (CMB-098). The default follows for the maximum.
        /// </summary>
        public virtual int ChooseBullRushPush(CharacterController self, CharacterController target, int maxIfFollowing, out bool follow)
        {
            follow = true;
            return Mathf.Max(1, maxIfFollowing);
        }

        // ── Maneuver choices (batch 12 step 19, AiManeuverDecider; CMB-098, D10, D15). AI stopgaps until the batch 13
        //    catalog: each answers one choice the rules leave to the creature, never a rule. ──

        /// <summary>
        /// The item a disarm or sunder by this creature aims at (the attacker chooses, PHB p.155, p.158; D15): a slot of
        /// the target's disarmable held items or sunderable items, or null when it has none. <see cref="DefaultChooseManeuverItem"/>.
        /// </summary>
        public virtual EquipSlot? ChooseManeuverItem(CharacterController self, CharacterController target, SpecialAttackType type)
            => DefaultChooseManeuverItem(self, target, type);

        /// <summary>
        /// The default item choice: the item whose loss costs the target most, by a fixed score (AI stopgap): a weapon first
        /// (100, plus 10 per enhancement point, plus its average damage die), then a shield (50 plus 10 per enhancement
        /// point), then armor (20 plus 10 per enhancement point); any other held item 10 plus 10 per enhancement point. The
        /// first listed wins a tie.
        /// </summary>
        public static EquipSlot? DefaultChooseManeuverItem(CharacterController self, CharacterController target, SpecialAttackType type)
        {
            if (target == null)
                return null;

            EquipSlot? best = null;
            float bestScore = float.NegativeInfinity;
            if (type == SpecialAttackType.Sunder)
            {
                List<SunderableItemOption> options = target.GetSunderableItemOptions();
                for (int i = 0; i < options.Count; i++)
                {
                    float score = ScoreManeuverItem(options[i].Item, options[i].Kind == SunderTargetKind.Shield, options[i].Kind == SunderTargetKind.Armor);
                    if (score > bestScore) { bestScore = score; best = options[i].Slot; }
                }
                return best;
            }

            List<DisarmableHeldItemOption> held = target.GetDisarmableHeldItemOptions();
            for (int i = 0; i < held.Count; i++)
            {
                ItemData item = held[i].HeldItem;
                float score = ScoreManeuverItem(item, item != null && item.Type == ItemType.Shield, false);
                if (score > bestScore) { bestScore = score; best = held[i].HandSlot; }
            }
            return best;
        }

        private static float ScoreManeuverItem(ItemData item, bool shield, bool armor)
        {
            if (item == null)
                return 0f;
            float enhancement = 10f * item.EnhancementBonus;
            if (item.IsWeapon)
                return 100f + enhancement + item.DamageCount * (item.DamageDice + 1) * 0.5f;
            if (shield)
                return 50f + enhancement;
            if (armor)
                return 20f + enhancement;
            return 10f + enhancement;
        }

        /// <summary>
        /// Where this creature takes the grapple after a won move check (PHB p.156; CMB-103): one of the destinations of
        /// <paramref name="paths"/>, or null to stay. <see cref="DefaultChooseGrappleMoveDestination"/>.
        /// </summary>
        public virtual Vector2Int? ChooseGrappleMoveDestination(CharacterController self, IReadOnlyDictionary<Vector2Int, List<Vector2Int>> paths,
            IList<CharacterController> opponents, IList<CharacterController> allCharacters)
            => DefaultChooseGrappleMoveDestination(self, paths, opponents, allCharacters);

        /// <summary>
        /// The default grapple move (AI stopgap): the destination where the fewest enemies outside the grapple threaten the
        /// grappler, then the path that provokes the fewest attacks of opportunity from those third parties (the move provokes
        /// from them, CMB-152; ThreatSystem.AnalyzePathForAoOs), then the one nearest to an able ally of the grappler (to
        /// bring the held foe to its friends), then the shortest path, then the lowest square (x, then y), so the choice is
        /// deterministic. The current square is scored the same way (no path, so no AoO): when it is at least as good as the
        /// best destination the grappler stays (null).
        /// </summary>
        public static Vector2Int? DefaultChooseGrappleMoveDestination(CharacterController self, IReadOnlyDictionary<Vector2Int, List<Vector2Int>> paths,
            IList<CharacterController> opponents, IList<CharacterController> allCharacters)
        {
            if (self == null || paths == null || paths.Count == 0)
                return null;

            var all = allCharacters != null ? new List<CharacterController>(allCharacters) : new List<CharacterController>();
            if (opponents != null)
                all.RemoveAll(c => opponents.Contains(c));

            var excluded = opponents != null ? new List<CharacterController>(opponents) : new List<CharacterController>();
            Vector2Int? best = null;
            int bestThreats = int.MaxValue, bestAoOs = int.MaxValue, bestAlly = int.MaxValue, bestLength = int.MaxValue;
            foreach (KeyValuePair<Vector2Int, List<Vector2Int>> kv in paths)
            {
                int threats = ThreatSystem.GetThreateningEnemies(kv.Key, self, all).Count;
                int aoos = kv.Value != null && kv.Value.Count > 0
                    ? ThreatSystem.AnalyzePathForAoOs(self, kv.Value, all, excludedThreateners: excluded).Count
                    : 0;
                int ally = NearestAllyDistance(self, kv.Key, allCharacters);
                int length = kv.Value != null ? kv.Value.Count : int.MaxValue;
                if (IsBetterMoveSquare(threats, aoos, ally, length, kv.Key, bestThreats, bestAoOs, bestAlly, bestLength, best))
                {
                    best = kv.Key;
                    bestThreats = threats;
                    bestAoOs = aoos;
                    bestAlly = ally;
                    bestLength = length;
                }
            }
            if (!best.HasValue)
                return null;

            // Stay when the current square is at least as good (by threats, then the ally distance; staying provokes nothing).
            int hereThreats = ThreatSystem.GetThreateningEnemies(self.GridPosition, self, all).Count;
            int hereAlly = NearestAllyDistance(self, self.GridPosition, allCharacters);
            if (hereThreats < bestThreats || (hereThreats == bestThreats && bestAoOs > 0))
                return null;
            if (hereThreats == bestThreats && hereAlly <= bestAlly)
                return null;
            return best;
        }

        /// <summary>
        /// The space this creature moves into after escaping a grapple (PHB p.156-157; D10): one of
        /// <paramref name="squares"/>, or null to stay. <see cref="DefaultChooseGrappleEscapeSquare"/>.
        /// </summary>
        public virtual Vector2Int? ChooseGrappleEscapeSquare(CharacterController self, IList<Vector2Int> squares, CharacterController formerOpponent,
            IList<CharacterController> allCharacters)
            => DefaultChooseGrappleEscapeSquare(self, squares, formerOpponent, allCharacters);

        /// <summary>
        /// The default escape square (AI stopgap), replacing the old random pick: the square the fewest enemies threaten
        /// (the former grapple opponent included: it may attack next turn), then the one nearest to an able ally, then the
        /// lowest square (x, then y).
        /// </summary>
        public static Vector2Int? DefaultChooseGrappleEscapeSquare(CharacterController self, IList<Vector2Int> squares, CharacterController formerOpponent,
            IList<CharacterController> allCharacters)
        {
            if (self == null || squares == null || squares.Count == 0)
                return null;

            var all = allCharacters != null ? new List<CharacterController>(allCharacters) : new List<CharacterController>();
            Vector2Int? best = null;
            int bestThreats = int.MaxValue, bestAlly = int.MaxValue;
            for (int i = 0; i < squares.Count; i++)
            {
                int threats = ThreatSystem.GetThreateningEnemies(squares[i], self, all).Count;
                int ally = NearestAllyDistance(self, squares[i], allCharacters);
                if (IsBetterSquare(threats, ally, 0, squares[i], bestThreats, bestAlly, 0, best))
                {
                    best = squares[i];
                    bestThreats = threats;
                    bestAlly = ally;
                }
            }
            return best;
        }

        private static bool IsBetterMoveSquare(int threats, int aoos, int ally, int length, Vector2Int square,
            int bestThreats, int bestAoOs, int bestAlly, int bestLength, Vector2Int? best)
        {
            if (!best.HasValue) return true;
            if (threats != bestThreats) return threats < bestThreats;
            if (aoos != bestAoOs) return aoos < bestAoOs;
            return IsBetterSquare(threats, ally, length, square, bestThreats, bestAlly, bestLength, best);
        }

        private static bool IsBetterSquare(int threats, int ally, int length, Vector2Int square,
            int bestThreats, int bestAlly, int bestLength, Vector2Int? best)
        {
            if (!best.HasValue) return true;
            if (threats != bestThreats) return threats < bestThreats;
            if (ally != bestAlly) return ally < bestAlly;
            if (length != bestLength) return length < bestLength;
            if (square.x != best.Value.x) return square.x < best.Value.x;
            return square.y < best.Value.y;
        }

        private static int NearestAllyDistance(CharacterController self, Vector2Int square, IList<CharacterController> allCharacters)
        {
            int best = int.MaxValue;
            if (allCharacters == null)
                return best;
            for (int i = 0; i < allCharacters.Count; i++)
            {
                CharacterController c = allCharacters[i];
                if (c == null || c == self || c.Stats == null || c.IsDead || c.Stats.IsDead || !TeamUtility.IsAlly(self, c))
                    continue;
                best = Mathf.Min(best, SquareGridUtils.GetDistance(square, c.GridPosition));
            }
            return best;
        }

        /// <summary>
        /// Whether this creature opposes an escape attempt from its grapple (PHB p.156: an opponent need not try to hold an
        /// escaping creature; D10). The AI always opposes; a profile may let go.
        /// </summary>
        public virtual bool ShouldOpposeGrappleEscape(CharacterController self, CharacterController escaper) => true;

        /// <summary>
        /// Whether this creature skips the grab after an Improved Grab trigger hit (the creature "can attempt" it, MM p.310;
        /// CMB-098). <see cref="DefaultShouldSkipImprovedGrab"/>.
        /// </summary>
        public virtual bool ShouldSkipImprovedGrab(CharacterController self, CharacterController target, IList<CharacterController> allCharacters)
            => DefaultShouldSkipImprovedGrab(self, target, allCharacters);

        /// <summary>
        /// The default (AI stopgap): skip only a hopeless grab, one whose opposed grapple check the grabber cannot win even
        /// with a natural 20 against a natural 1: its grapple modifier + 19 at most the target's, since a tie goes to the
        /// higher modifier (PHB p.64), the target's.
        /// </summary>
        public static bool DefaultShouldSkipImprovedGrab(CharacterController self, CharacterController target, IList<CharacterController> allCharacters)
        {
            if (self == null || target == null || self.Stats == null || target.Stats == null)
                return false;
            return self.GetGrappleModifier() + 19 <= target.GetGrappleModifier();
        }

        /// <summary>
        /// Whether this creature feints <paramref name="target"/> before its attack (PHB p.155; a move action with Improved
        /// Feint, PHB p.95; CMB-102). <see cref="DefaultShouldFeint"/>.
        /// </summary>
        public virtual bool ShouldFeint(CharacterController self, CharacterController target) => DefaultShouldFeint(self, target);

        /// <summary>
        /// The default (AI stopgap, kept minimal): feint only as a move action (Improved Feint) with both actions left, a
        /// sneak attack to land (a rogue level) and one attack a turn anyway (BAB below +6), against a target in reach that
        /// is not already flat-footed and has an Intelligence score (a feint against a nonintelligent creature is impossible,
        /// PHB p.155), when the Bluff modifier is at least the target's opposed modifier as PHB p.155 gives it: Sense Motive
        /// plus base attack bonus, with the Bluff at -4 against a nonhumanoid and -8 against animal Intelligence (1 or 2). The
        /// resolver still rolls the CMB-177 formula; this estimate is the RAW one.
        /// </summary>
        public static bool DefaultShouldFeint(CharacterController self, CharacterController target)
        {
            if (self == null || target == null || self.Stats == null || target.Stats == null || self.Actions == null)
                return false;
            if (!self.Stats.HasFeat("Improved Feint") || self.Stats.GetClassLevel("Rogue") <= 0 || self.Stats.BaseAttackBonus >= 6)
                return false;
            if (!self.Actions.HasMoveAction || !self.Actions.HasStandardAction || target.IsFlatFootedCondition)
                return false;
            if (!self.IsTargetInCurrentWeaponRange(target) || self.IsEquippedWeaponRanged())
                return false;
            if (target.Stats.IsMindless || target.Stats.INT <= 0)
                return false;
            int bluff = self.Stats.GetSkillBonus("Bluff");
            if (target.Stats.INT <= 2)
                bluff -= 8;
            else if (!TeamUtility.IsHumanoid(target))
                bluff -= 4;
            int opposed = target.Stats.GetSkillBonus("Sense Motive") + target.Stats.BaseAttackBonus;
            return bluff >= opposed;
        }

        /// <summary>
        /// Which natural attack this creature uses for Haste's extra attack on a full attack (PHB p.239;
        /// owner decision 2026-10-07, CMB-106): a natural-sequence index. Asked by the NPC melee
        /// sequence and the NPC pounce. The default takes the best <see cref="ScoreHasteNaturalAttack"/>.
        /// </summary>
        public virtual int ChooseHasteNaturalAttackIndex(CharacterController self, CharacterController target)
        {
            return NaturalAttackChoice.ChooseBest(self, index => ScoreHasteNaturalAttack(self, target, index));
        }

        /// <summary>
        /// Score of one natural attack as Haste's extra attack (higher is better). The default
        /// (<see cref="NaturalAttackChoice.Score"/>) prefers a rider that matters against the target,
        /// then the highest attack bonus, then the higher average damage.
        /// </summary>
        public virtual float ScoreHasteNaturalAttack(CharacterController self, CharacterController target, int naturalAttackIndex)
        {
            return NaturalAttackChoice.Score(self, target, naturalAttackIndex);
        }

        /// <summary>
        /// Placeholder for future overrun restrictions (size/path rules).
        /// </summary>
        protected virtual bool IsValidOverrunTarget(CharacterController target, CharacterController self)
        {
            return target != null && self != null;
        }

        public virtual SpecialAttackType? GetPreferredManeuver(CharacterController self, CharacterController target)
        {
            if (self == null || target == null)
                return null;

            if (self.IsGrappling() && GrappleBehavior == GrappleBehavior.Maintain)
                return SpecialAttackType.Grapple;

            if (Maneuvers != null)
            {
                if (Maneuvers.AttemptTrip && self.HasMeleeWeaponEquipped() && IsValidTripTarget(target, self))
                    return SpecialAttackType.Trip;

                if (Maneuvers.AttemptDisarm && IsValidDisarmTarget(target))
                    return SpecialAttackType.Disarm;

                if (Maneuvers.AttemptSunder && IsValidSunderTarget(target))
                    return SpecialAttackType.Sunder;

                if (Maneuvers.AttemptBullRush && IsValidBullRushTarget(target, self))
                    return SpecialAttackType.BullRushAttack;

                if (Maneuvers.AttemptOverrun && IsValidOverrunTarget(target, self))
                    return SpecialAttackType.Overrun;
            }

            if (ShouldInitiateGrapple(self, target))
                return SpecialAttackType.Grapple;

            return null;
        }

        protected static bool TargetHasMatchingTag(CharacterTags tags, string requested)
        {
            if (tags == null || string.IsNullOrWhiteSpace(requested))
                return false;

            string normalizedRequested = NormalizeTag(requested);

            foreach (string existing in tags.GetAllTags())
            {
                if (string.IsNullOrWhiteSpace(existing))
                    continue;

                string normalizedExisting = NormalizeTag(existing);
                if (normalizedExisting.Equals(normalizedRequested, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Friendly partial matching supports both "Armor: Light Armor" and "Light Armor" etc.
                if (normalizedExisting.IndexOf(normalizedRequested, StringComparison.OrdinalIgnoreCase) >= 0
                    || normalizedRequested.IndexOf(normalizedExisting, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static string NormalizeTag(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            return raw.Trim().Replace(":", ": ").Replace("  ", " ");
        }

        private static int CountAdjacentAllies(CharacterController target)
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
                if (c == null || c == target || c.Stats == null || c.Stats.IsDead)
                    continue;

                if (gm.IsEnemyTeamForAI(c, target))
                    continue;

                int dist = SquareGridUtils.GetDistance(c.GridPosition, target.GridPosition);
                if (dist <= 1)
                    count++;
            }

            return count;
        }
    }

    /// <summary>An AI bardic music choice (AIProfile.ChooseBardicMusic): what to do, with which song and targets.</summary>
    public sealed class BardicMusicChoice
    {
        public enum ChoiceKind { Start, Concentrate, Suggest, Stop }

        public ChoiceKind Kind;
        public BardicAbility Ability;
        public List<CharacterController> Targets = new List<CharacterController>();
        public string Skill;

        public static BardicMusicChoice Start(BardicAbility ability, List<CharacterController> targets, string skill = null)
            => new BardicMusicChoice { Kind = ChoiceKind.Start, Ability = ability, Targets = targets ?? new List<CharacterController>(), Skill = skill };
        public static BardicMusicChoice Concentrate() => new BardicMusicChoice { Kind = ChoiceKind.Concentrate };
        public static BardicMusicChoice Suggest(CharacterController target)
            => new BardicMusicChoice { Kind = ChoiceKind.Suggest, Ability = BardicAbility.Suggestion, Targets = new List<CharacterController> { target } };
        public static BardicMusicChoice Stop() => new BardicMusicChoice { Kind = ChoiceKind.Stop };
    }
}
