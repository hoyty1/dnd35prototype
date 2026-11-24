using System;
using System.Collections.Generic;
using System.Globalization;
using DND35.AI.Knowledge;
using UnityEngine;

namespace DND35.AI.Personality
{
    /// <summary>
    /// Where a personality meets a decision (batch 14 step 3; docs/designs/personalities_batch14.md "The blend", C4-C8).
    /// Every member returns its <c>legacy</c> argument unchanged when the personality is null, so a creature with no
    /// traits plays exactly as before. With a personality, every member times itself in the Personality bucket, and with
    /// an <c>actor</c> whose AI turn is open (AiDecisionTrace.HasOpenTurn) it writes one AiDecisionTrace row; no row
    /// string is built otherwise. A turn keeps at most AiDecisionTrace.MaxDecisionsPerTurn rows, so a site that calls
    /// <see cref="ScoreTarget"/> per candidate or <see cref="AoOCost"/> per movement option passes <c>trace: false</c> and
    /// records one summary row itself (the chosen target and its term; one AoO price per evaluation). A null
    /// JudgeContext.Difficulty is filled from <see cref="SettingsDifficulty"/>. AI choices, never rules: the caller
    /// passes only options the rules cores allow. Step 5 calls the creature members below at the target, ignore-downed,
    /// adaptive retarget and coup de grace sites; step 6 calls the others at the charge, movement and out-of-reach sites.
    /// </summary>
    public static class AiBlend
    {
        /// <summary>C6: the per-provoker price of a Forbid, and the flat penalty the AoO price caps at (the legacy avoid-AoO cost).</summary>
        public const float AoOForbidPrice = 1000f;

        /// <summary>C6: an Against verdict prices each provoker at least this.</summary>
        public const float AoOAgainstFloor = 6f;

        /// <summary>C6: a For verdict prices each provoker at most this (the legacy non-avoiding price).</summary>
        public const float AoOForCeiling = 2f;

        /// <summary>
        /// C8: how far one unit of RoleDesire moves an out-of-reach alternative, in list places (starter constant: a
        /// desire gap of .5 moves an entry one place).
        /// </summary>
        public const float OutOfReachDesireGain = 2f;

        /// <summary>
        /// The game's AI difficulty, read when a JudgeContext leaves Difficulty null (C11, Q6). AIService.Initialize points
        /// it at AIService.Difficulty (its harness override, else GameSettings.aiDifficulty read live; batch 14 step 4) and Cleanup clears it; null
        /// (edit mode, no GameManager) means the setting's default, Normal. A test that sets it restores it.
        /// </summary>
        public static Func<AIService.AIDifficultyLevel> SettingsDifficulty { get; set; }

        /// <summary>
        /// A target's score (C4): <paramref name="legacy"/> plus the personality's bounded trait term for
        /// <paramref name="view"/>. An invalid target (float.MinValue or negative infinity) stays invalid.
        /// </summary>
        public static float ScoreTarget(PersonalityProfile p, float legacy, ITargetView view, object actor = null, bool trace = true)
        {
            if (p == null || legacy == float.MinValue || float.IsNegativeInfinity(legacy))
                return legacy;
            using (AiDecisionTimer.Open(AiTimeBucket.Personality, actor))
            {
                float term = p.TargetTerm(view);
                if (trace && Tracing(actor))
                    Trace(actor, "Personality.Target", Fmt(legacy + term), Fmt(legacy), term,
                        view != null && view.Subject != null ? view.Subject.name : "?");
                return legacy + term;
            }
        }

        /// <summary>
        /// A yes/no decision (C5), after the caller's rules gates: <paramref name="dataOverride"/> (a definition's
        /// UseCoupDeGrace) wins; then a quorum Forbid is no and a quorum Insist yes; then the vote; inside plus or minus
        /// .15, <paramref name="legacy"/>. With no personality, <paramref name="legacy"/> as given (the legacy sites already
        /// apply the data override).
        /// </summary>
        public static bool Judge(PersonalityProfile p, BoolDecision decision, bool legacy, in JudgeContext ctx,
            bool? dataOverride = null, object actor = null)
        {
            if (p == null)
                return legacy;
            using (AiDecisionTimer.Open(AiTimeBucket.Personality, actor))
            {
                if (dataOverride.HasValue)
                {
                    if (Tracing(actor))
                        Trace(actor, "Personality." + decision, Yn(dataOverride.Value), Yn(legacy), 0f, "data override");
                    return dataOverride.Value;
                }
                JudgeContext c = WithDifficulty(ctx, out bool fromSettings);
                JudgeResult r = p.Judge(decision, c);
                bool answer = Answer(r.Verdict, legacy);
                if (Tracing(actor))
                    Trace(actor, "Personality." + decision, Yn(answer), Yn(legacy), r.Vote, Reason(r, fromSettings));
                return answer;
            }
        }

        /// <summary>The yes/no answer of <paramref name="verdict"/>: Forbid and Against no, For and Insist yes, Neutral <paramref name="legacy"/>.</summary>
        public static bool Answer(Verdict verdict, bool legacy)
        {
            switch (verdict)
            {
                case Verdict.Forbid:
                case Verdict.Against:
                    return false;
                case Verdict.For:
                case Verdict.Insist:
                    return true;
                default:
                    return legacy;
            }
        }

        /// <summary>
        /// The price per provoker of a path that provokes attacks of opportunity (C6), replacing the Role's boolean
        /// (<paramref name="legacyPrice"/>: 1000 when the Role avoids AoOs, else 2): by the traits' ProvokeAoO verdict,
        /// Forbid 1000, Against max(legacy, 6), For min(legacy, 2), Insist 0, Neutral the legacy price.
        /// <see cref="AoOPenalty"/> turns a price into a path's penalty.
        /// </summary>
        public static float AoOCost(PersonalityProfile p, float legacyPrice, in JudgeContext ctx, object actor = null, bool trace = true)
        {
            if (p == null)
                return legacyPrice;
            using (AiDecisionTimer.Open(AiTimeBucket.Personality, actor))
            {
                JudgeContext c = WithDifficulty(ctx, out bool fromSettings);
                JudgeResult r = p.Judge(BoolDecision.ProvokeAoO, c);
                float price;
                switch (r.Verdict)
                {
                    case Verdict.Forbid: price = AoOForbidPrice; break;
                    case Verdict.Against: price = Mathf.Max(legacyPrice, AoOAgainstFloor); break;
                    case Verdict.For: price = Mathf.Min(legacyPrice, AoOForCeiling); break;
                    case Verdict.Insist: price = 0f; break;
                    default: price = legacyPrice; break;
                }
                if (trace && Tracing(actor))
                    Trace(actor, "Personality.AoOCost", Fmt(price), Fmt(legacyPrice), r.Vote, Reason(r, fromSettings));
                return price;
            }
        }

        /// <summary>
        /// The penalty of a path with <paramref name="provokers"/> attacks of opportunity at <paramref name="price"/> per
        /// provoker: a price of 1000 or more is a flat 1000 (the legacy avoid-AoO cost, AIService.EvaluateMovementOptions),
        /// else price times provokers; 0 for no provoker.
        /// </summary>
        public static float AoOPenalty(float price, int provokers)
        {
            if (provokers <= 0)
                return 0f;
            return price >= AoOForbidPrice ? AoOForbidPrice : price * provokers;
        }

        /// <summary>
        /// C7: the ranged-AoO risk tolerance multiplier, <paramref name="legacyMultiplier"/> (the Role's
        /// GetRangedAoORiskToleranceMultiplier) times (2 - rho): 1.0 at RiskAversion .5, lower for a cautious creature.
        /// The caller keeps its clamp (AIService.CalculateRangedRiskTolerance).
        /// </summary>
        public static float RiskToleranceFactor(PersonalityProfile p, float legacyMultiplier, object actor = null)
        {
            if (p == null)
                return legacyMultiplier;
            using (AiDecisionTimer.Open(AiTimeBucket.Personality, actor))
            {
                float f = legacyMultiplier * (2f - p.Rho);
                if (Tracing(actor))
                    Trace(actor, "Personality.RiskTolerance", Fmt(f), Fmt(legacyMultiplier), f - legacyMultiplier,
                        "RA " + p.RiskAversion.ToString("0.00", CultureInfo.InvariantCulture));
                return f;
            }
        }

        /// <summary>
        /// C8: the Role's out-of-reach list (AIProfile.GetOutOfReachAlternatives), re-weighted by the traits' RoleDesire:
        /// each entry scores minus its place plus <see cref="OutOfReachDesireGain"/> times the desire for its roles
        /// (<see cref="RolesOf"/>), sorted stably (a tie keeps the Role's order). It never adds or removes an entry. The
        /// Role's list itself when there is no personality, fewer than two entries, or no change of order.
        /// </summary>
        public static IReadOnlyList<OutOfReachAlternative> OrderOutOfReach(PersonalityProfile p,
            IReadOnlyList<OutOfReachAlternative> roleList, object actor = null)
        {
            if (p == null || roleList == null || roleList.Count < 2)
                return roleList;
            using (AiDecisionTimer.Open(AiTimeBucket.Personality, actor))
            {
                int n = roleList.Count;
                var order = new List<int>(n);
                var score = new float[n];
                for (int i = 0; i < n; i++)
                {
                    order.Add(i);
                    score[i] = -i + OutOfReachDesireGain * p.RoleShift(RolesOf(roleList[i]));
                }
                // Insertion sort: stable, and the lists hold at most a handful of entries.
                for (int i = 1; i < n; i++)
                {
                    int k = order[i];
                    int j = i - 1;
                    while (j >= 0 && score[order[j]] < score[k])
                    {
                        order[j + 1] = order[j];
                        j--;
                    }
                    order[j + 1] = k;
                }
                bool changed = false;
                for (int i = 0; i < n; i++)
                    if (order[i] != i)
                        changed = true;
                if (!changed)
                {
                    if (Tracing(actor))
                        Trace(actor, "Personality.OutOfReach", Join(roleList), Join(roleList), 0f, "Role order kept");
                    return roleList;
                }
                var result = new List<OutOfReachAlternative>(n);
                for (int i = 0; i < n; i++)
                    result.Add(roleList[order[i]]);
                if (Tracing(actor))
                    Trace(actor, "Personality.OutOfReach", Join(result), Join(roleList), 0f, "RoleDesire");
                return result;
            }
        }

        /// <summary>
        /// What an out-of-reach alternative does for the creature, as the catalog's roles: another target is Attack; the
        /// preferred range is Approach or Retreat (it closes or opens the distance); an approach is Approach; a withdraw is
        /// Retreat and Escape (the withdraw entry); total defense is Defend.
        /// </summary>
        public static ActionRoles RolesOf(OutOfReachAlternative alternative)
        {
            switch (alternative)
            {
                case OutOfReachAlternative.AttackOtherTarget: return ActionRoles.Attack;
                case OutOfReachAlternative.PreferredRange: return ActionRoles.Approach | ActionRoles.Retreat;
                case OutOfReachAlternative.Approach: return ActionRoles.Approach;
                case OutOfReachAlternative.Withdraw: return ActionRoles.Retreat | ActionRoles.Escape;
                case OutOfReachAlternative.TotalDefense: return ActionRoles.Defend;
                default: return ActionRoles.None;
            }
        }

        // ── Batch 14 step 5: the target sites (docs/designs/personalities_batch14.md "The blend") ──
        // Each member below reads self.Personality and returns its legacy argument at once when it is null, before any
        // view or vote context is built, so a creature with no traits pays nothing and plays exactly as before.

        /// <summary>
        /// The score of <paramref name="target"/> for <paramref name="self"/> at a per-candidate site (C4): the legacy
        /// score plus the trait term of the target's knowledge view, with no trace row (the site records one summary row
        /// with <see cref="TraceTarget"/>). The legacy score when <paramref name="self"/> has no personality.
        /// </summary>
        public static float ScoreTarget(CharacterController self, CharacterController target, float legacy)
        {
            PersonalityProfile p = self != null ? self.Personality : null;
            if (p == null || target == null)
                return legacy;
            return ScoreTarget(p, legacy, KnowledgeService.ViewOf(self, target), self, trace: false);
        }

        /// <summary>True when <paramref name="self"/> has a personality and an open AI turn, so a site's summary row is kept.</summary>
        public static bool TracesTargets(CharacterController self)
            => self != null && self.Personality != null && Tracing(self);

        /// <summary>
        /// The one summary row of a per-candidate target site (<paramref name="site"/>): the blended choice, the choice the
        /// legacy scores alone would have made, and the trait term of the chosen target. Written only when
        /// <see cref="TracesTargets"/>; the caller builds the names only then.
        /// </summary>
        public static void TraceTarget(CharacterController self, string site, string chosen, string legacy, float term)
        {
            if (!TracesTargets(self))
                return;
            Trace(self, "Personality." + site, chosen, legacy, term, "trait term " + Fmt(term));
        }

        /// <summary>The same summary row with the two targets named by their character names ("none" for no target).</summary>
        public static void TraceTarget(CharacterController self, string site, CharacterController chosen,
            CharacterController legacy, float term)
        {
            if (!TracesTargets(self))
                return;
            TraceTarget(self, site, NameOf(chosen), NameOf(legacy), term);
        }

        private static string NameOf(CharacterController c)
            => c == null ? "none" : c.Stats != null ? c.Stats.CharacterName : c.name;

        /// <summary>
        /// The ignore-downed test (C5, BoolDecision.IgnoreUnconscious) of <paramref name="self"/>: whether it skips foes
        /// that are down (dying, stable or unconscious). <paramref name="legacy"/> is the Role's answer
        /// (AIProfile.ShouldIgnoreUnconsciousTargets, or the site's own default); the traits' vote overrides it (Sadistic
        /// forbids skipping them; Smart is for it while a standing foe is in reach). The caller keeps its own rules, such
        /// as "only while a foe still stands".
        /// </summary>
        public static bool IgnoresUnconscious(CharacterController self, bool legacy)
        {
            PersonalityProfile p = self != null ? self.Personality : null;
            if (p == null)
                return legacy;
            using (AiDecisionTimer.Open(AiTimeBucket.Personality, self))
                return Judge(p, BoolDecision.IgnoreUnconscious, legacy, SelfFacts.JudgeContextOf(self), actor: self);
        }

        /// <summary>
        /// The mid-sequence retarget of <paramref name="self"/> (AIProfile.ShouldSwitchTargetsMidFullAttack): on when the
        /// Role says so (<paramref name="legacy"/>), or when the traits' IgnoreUnconscious verdict is For or Insist, so a
        /// creature whose traits skip a downed target does not stop its full attack on one (a Smart Humanoid with a
        /// standing foe in reach). A Neutral, Against or Forbid verdict leaves the Role's answer.
        /// </summary>
        public static bool Adaptive(CharacterController self, bool legacy)
        {
            PersonalityProfile p = self != null ? self.Personality : null;
            if (p == null || legacy)
                return legacy;
            using (AiDecisionTimer.Open(AiTimeBucket.Personality, self))
            {
                JudgeContext c = WithDifficulty(SelfFacts.JudgeContextOf(self), out bool fromSettings);
                JudgeResult r = p.Judge(BoolDecision.IgnoreUnconscious, c);
                bool answer = r.Verdict == Verdict.For || r.Verdict == Verdict.Insist;
                if (Tracing(self))
                    Trace(self, "Personality.Adaptive", Yn(answer), Yn(legacy), r.Vote, Reason(r, fromSettings));
                return answer;
            }
        }

        /// <summary>
        /// Whether <paramref name="self"/> takes a coup de grace (PHB p.153) the rules allow (C5, C11; the caller has
        /// already checked GameManager.CanUseCoupDeGraceAttackOption or the adjacent helpless foes): the data override
        /// (<paramref name="dataOverride"/>, CharacterController.EnemyUseCoupDeGraceOverride) wins; then the traits' vote
        /// (a sadist insists at Normal difficulty and above and abstains on Easy, owner 2026-10-30; a healer is against);
        /// then <paramref name="roleLegacy"/>, the Role's AIProfile.ShouldUseCoupDeGrace. With no personality, the override
        /// else the Role's answer, as before. The caller computes <paramref name="roleLegacy"/> only when there is no override.
        /// </summary>
        public static bool UsesCoupDeGrace(CharacterController self, bool roleLegacy, bool? dataOverride)
        {
            PersonalityProfile p = self != null ? self.Personality : null;
            if (p == null)
                return dataOverride ?? roleLegacy;
            using (AiDecisionTimer.Open(AiTimeBucket.Personality, self))
            {
                JudgeContext ctx = dataOverride.HasValue ? default : SelfFacts.JudgeContextOf(self);
                return Judge(p, BoolDecision.CoupDeGrace, roleLegacy, ctx, dataOverride, self);
            }
        }

        // ── Batch 14 step 6: the movement sites and the out-of-reach order (docs/designs/personalities_batch14.md "The blend") ──
        // As above: each member returns its legacy argument at once when self.Personality is null.

        /// <summary>The kiter's legacy keep-away distance: it backs off from a foe within 2 squares (AIService.ExecuteRangedKiterTurn).</summary>
        public const int LegacyKeepAwaySquares = 2;

        /// <summary>
        /// Whether <paramref name="self"/> charges (C5, BoolDecision.Charge) when the rules allow it (the caller has checked
        /// AIService.ShouldNPCCharge): <paramref name="legacy"/> is the Role's AIProfile.ShouldPreferCharge; the traits' vote
        /// overrides it (Cowardly and Caster forbid a charge, a Healer is against it, a Berserker insists).
        /// </summary>
        public static bool PrefersCharge(CharacterController self, bool legacy)
        {
            PersonalityProfile p = self != null ? self.Personality : null;
            if (p == null)
                return legacy;
            using (AiDecisionTimer.Open(AiTimeBucket.Personality, self))
                return Judge(p, BoolDecision.Charge, legacy, SelfFacts.JudgeContextOf(self), actor: self);
        }

        /// <summary>
        /// The per-provoker price of a path that provokes attacks of opportunity for <paramref name="self"/> (C6, see the
        /// profile overload): <paramref name="legacyPrice"/> is the Role's (1000 when it avoids AoOs, else 2,
        /// AIService.EvaluateMovementOptions). The vote reads the creature's own wound band (Cowardly forbids a provoking
        /// path once Hurt). One trace row per call, so the site calls it once per evaluation, not per square.
        /// </summary>
        public static float AoOCost(CharacterController self, float legacyPrice)
        {
            PersonalityProfile p = self != null ? self.Personality : null;
            if (p == null)
                return legacyPrice;
            using (AiDecisionTimer.Open(AiTimeBucket.Personality, self))
                return AoOCost(p, legacyPrice, SelfFacts.JudgeContextOf(self), self);
        }

        /// <summary>
        /// C7: the ranged-AoO risk tolerance multiplier of <paramref name="self"/>: <paramref name="legacyMultiplier"/> (the
        /// Role's GetRangedAoORiskToleranceMultiplier, 1 without a Role) times 2 - rho. The caller keeps its clamp.
        /// </summary>
        public static float RiskToleranceFactor(CharacterController self, float legacyMultiplier)
        {
            PersonalityProfile p = self != null ? self.Personality : null;
            return p == null ? legacyMultiplier : RiskToleranceFactor(p, legacyMultiplier, self);
        }

        /// <summary>
        /// True when <paramref name="profile"/> is a Role that fights from range: a Ranged or Mixed combat style, or a
        /// casting profile. Cowardly's range adjust applies only to such a Role (TraitDefinition.RangeAdjustOnlyRangedOrCasting).
        /// False without a Role.
        /// </summary>
        public static bool IsRangedOrCastingRole(AIProfile profile)
            => profile != null && (profile.CombatStyle == CombatStyle.Ranged || profile.CombatStyle == CombatStyle.Mixed || profile.IsCastingProfile);

        /// <summary>
        /// The preferred range of <paramref name="self"/> in squares (C3): <paramref name="legacy"/> (the Role's
        /// Movement.PreferredRangeSquares) plus the traits' PersonalityProfile.RangeAdjust, at least 0;
        /// <paramref name="rangedOrCastingRole"/> says whether a range adjust that applies only to a Ranged, Mixed or
        /// casting Role counts (<see cref="IsRangedOrCastingRole"/>). The legacy range when the traits change nothing.
        /// </summary>
        public static int PreferredRange(CharacterController self, int legacy, bool rangedOrCastingRole)
            => AdjustRange(self, legacy, rangedOrCastingRole, "Personality.PreferredRange");

        /// <summary>
        /// The kiter's keep-away distance for <paramref name="self"/> (C3): it backs off from a foe within this many squares.
        /// <paramref name="legacy"/> (<see cref="LegacyKeepAwaySquares"/>) plus the traits' range adjust, at least 1.
        /// <paramref name="rangedOrCastingRole"/> is the creature's Role (<see cref="IsRangedOrCastingRole"/>), as at the other
        /// range sites: a melee Role reaches the kiter too (RunsRangedRoutine for a creature with ranged means, a Healer's
        /// ranged attack), and a Ranged-or-casting-only adjust (Cowardly +2) does not apply to it.
        /// </summary>
        public static int KeepAwayDistance(CharacterController self, int legacy, bool rangedOrCastingRole)
            => AdjustRange(self, legacy, rangedOrCastingRole, "Personality.KeepAway", minimum: 1);

        private static int AdjustRange(CharacterController self, int legacy, bool rangedOrCastingRole, string site, int minimum = 0)
        {
            PersonalityProfile p = self != null ? self.Personality : null;
            if (p == null)
                return legacy;
            using (AiDecisionTimer.Open(AiTimeBucket.Personality, self))
            {
                int adjust = p.RangeAdjust(rangedOrCastingRole);
                if (adjust == 0)
                    return legacy;
                int range = Mathf.Max(minimum, legacy + adjust);
                if (Tracing(self))
                    Trace(self, site, range.ToString(CultureInfo.InvariantCulture), legacy.ToString(CultureInfo.InvariantCulture), range - legacy,
                        "range adjust " + adjust.ToString("+0;-0", CultureInfo.InvariantCulture) + (rangedOrCastingRole ? "" : ", melee Role"));
                return range;
            }
        }

        /// <summary>
        /// The out-of-reach list of <paramref name="self"/> (C8, see the profile overload): the Role's list
        /// (<paramref name="roleList"/>, AIProfile.GetOutOfReachAlternatives) re-weighted by the traits' RoleDesire; the
        /// Role's list itself with no personality.
        /// </summary>
        public static IReadOnlyList<OutOfReachAlternative> OrderOutOfReach(CharacterController self, IReadOnlyList<OutOfReachAlternative> roleList)
        {
            PersonalityProfile p = self != null ? self.Personality : null;
            return p == null ? roleList : OrderOutOfReach(p, roleList, self);
        }

        // ── Batch 14 step 11: the move left after a standard-action ability (AI-077, C8) ──

        /// <summary>C8: how far one unit of RoleDesire moves a choice after a standard action (starter constant, as <see cref="OutOfReachDesireGain"/>).</summary>
        public const float AfterStandardDesireGain = 2f;

        /// <summary>
        /// The move left after a standard action (C8; AIProfile.ChooseAfterStandardAction), tilted by the traits'
        /// RoleDesire. The choices open in <paramref name="ctx"/> (Stay always; Approach when the focus is out of reach;
        /// KeepDistance when a foe is close or threatens) each score 1 for the Role's pick, plus
        /// <see cref="AfterStandardDesireGain"/> times the desire for Approach or Retreat (Stay counts 0; a threatened
        /// creature's KeepDistance only while it can still fight from range, AI-039); a choice must beat
        /// the Role's pick to replace it. A prone creature's move becomes StandUp, as in the default. The Role's choice
        /// itself with no personality or no move to make. One trace row "Personality.AfterStandard" when traced.
        /// </summary>
        public static AfterStandardActionChoice AfterStandardAction(PersonalityProfile p, AfterStandardActionChoice roleChoice,
            in AfterStandardActionContext ctx, object actor = null)
        {
            if (p == null || !ctx.CanMove)
                return roleChoice;
            using (AiDecisionTimer.Open(AiTimeBucket.Personality, actor))
            {
                AfterStandardActionChoice roleWish = roleChoice;
                if (roleWish == AfterStandardActionChoice.StandUp)
                    roleWish = DND35.AI.AIProfile.KeepsDistanceRole(ctx) && ctx.CanKeepDistance && ctx.CanFightFromRange
                        ? AfterStandardActionChoice.KeepDistance
                        : AfterStandardActionChoice.Approach;

                AfterStandardActionChoice best = roleWish;
                float bestScore = AfterStandardScore(p, roleWish, roleWish);
                var order = new[] { AfterStandardActionChoice.Stay, AfterStandardActionChoice.Approach, AfterStandardActionChoice.KeepDistance };
                foreach (AfterStandardActionChoice c in order)
                {
                    if (c == roleWish || !IsOpen(c, ctx))
                        continue;
                    float s = AfterStandardScore(p, c, roleWish);
                    if (s > bestScore)
                    {
                        best = c;
                        bestScore = s;
                    }
                }
                AfterStandardActionChoice result = DND35.AI.AIProfile.StandFirstIfProne(best, ctx);
                if (Tracing(actor))
                    Trace(actor, "Personality.AfterStandard", result.ToString(), roleChoice.ToString(), bestScore - AfterStandardScore(p, roleWish, roleWish),
                        result == roleChoice ? "Role choice kept" : "RoleDesire");
                return result;
            }
        }

        /// <summary>The creature overload: the Role's choice with no personality.</summary>
        public static AfterStandardActionChoice AfterStandardAction(CharacterController self, AfterStandardActionChoice roleChoice,
            in AfterStandardActionContext ctx)
        {
            PersonalityProfile p = self != null ? self.Personality : null;
            return p == null ? roleChoice : AfterStandardAction(p, roleChoice, ctx, self);
        }

        /// <summary>What a choice after a standard action does for the creature, as the catalog's roles: Approach or Retreat; Stay none.</summary>
        public static ActionRoles RolesOf(AfterStandardActionChoice choice)
        {
            switch (choice)
            {
                case AfterStandardActionChoice.Approach: return ActionRoles.Approach;
                case AfterStandardActionChoice.KeepDistance: return ActionRoles.Retreat;
                default: return ActionRoles.None;
            }
        }

        // ── Batch 14 step 12: the healer reaches its patient (AI-047, AI-063, C8) ──

        /// <summary>
        /// The healer's approach to its patient (C8; AIProfile.ChooseHealerApproach), tilted by the traits' RoleDesire, as
        /// <see cref="AfterStandardAction"/>: each open choice (Kiter always; MoveAndCast and PartialApproach when the
        /// context allows them) scores 1 for the Role's pick, plus <see cref="AfterStandardDesireGain"/> times the desire
        /// for its roles (MoveAndCast Heal; PartialApproach Heal or Approach, the larger; Kiter none), and only a strictly
        /// higher score replaces the Role's pick. Which paths may provoke is not tilted here: the context's AoO price
        /// already holds the traits' ProvokeAoO vote. The Role's choice itself with no personality. One trace row
        /// "Personality.HealerApproach" when traced.
        /// </summary>
        public static HealerApproachChoice HealerApproach(PersonalityProfile p, HealerApproachChoice roleChoice,
            in HealerApproachContext ctx, object actor = null)
        {
            if (p == null || !ctx.HasPatient)
                return roleChoice;
            using (AiDecisionTimer.Open(AiTimeBucket.Personality, actor))
            {
                HealerApproachChoice best = roleChoice;
                float roleScore = HealerApproachScore(p, roleChoice, roleChoice);
                float bestScore = roleScore;
                var order = new[] { HealerApproachChoice.Kiter, HealerApproachChoice.MoveAndCast, HealerApproachChoice.PartialApproach };
                foreach (HealerApproachChoice c in order)
                {
                    if (c == roleChoice || !IsOpen(c, ctx))
                        continue;
                    float s = HealerApproachScore(p, c, roleChoice);
                    if (s > bestScore)
                    {
                        best = c;
                        bestScore = s;
                    }
                }
                if (Tracing(actor))
                    Trace(actor, "Personality.HealerApproach", best.ToString(), roleChoice.ToString(), bestScore - roleScore,
                        best == roleChoice ? "Role choice kept" : "RoleDesire");
                return best;
            }
        }

        /// <summary>The creature overload: the Role's choice with no personality.</summary>
        public static HealerApproachChoice HealerApproach(CharacterController self, HealerApproachChoice roleChoice, in HealerApproachContext ctx)
        {
            PersonalityProfile p = self != null ? self.Personality : null;
            return p == null ? roleChoice : HealerApproach(p, roleChoice, ctx, self);
        }

        /// <summary>What a healer's approach does for the creature, as the catalog's roles: MoveAndCast Heal, PartialApproach Heal and Approach, Kiter none.</summary>
        public static ActionRoles RolesOf(HealerApproachChoice choice)
        {
            switch (choice)
            {
                case HealerApproachChoice.MoveAndCast: return ActionRoles.Heal;
                case HealerApproachChoice.PartialApproach: return ActionRoles.Heal | ActionRoles.Approach;
                default: return ActionRoles.None;
            }
        }

        // ── Batch 14 step 13: aid another and total defense on a melee turn (AI-063, batch 13's C5, C8) ──

        /// <summary>C8: how far one unit of RoleDesire scales a melee turn option's value (starter constant, the maneuver scorer's 0.5).</summary>
        public const float MeleeTurnDesireGain = 0.5f;

        /// <summary>
        /// The melee turn's option (C8; AIProfile.ChooseMeleeTurnOption), tilted by the traits' RoleDesire, as the maneuver
        /// scorer weighs the attack: each open option's value (MeleeTurnOptionContext.ValueOf) is scaled by
        /// 1 + <see cref="MeleeTurnDesireGain"/> times the desire for its roles (the attack Attack; aid offense Support; aid
        /// defense Support or Defend, the larger; total defense Defend), and another option replaces the Role's pick only when
        /// its scaled value beats the pick's by the context's margin max(0.5, 0.15 S). With no personality, or desires that
        /// name none of these roles, the Role's choice. One trace row "Personality.MeleeTurnOption" when traced.
        /// </summary>
        public static MeleeTurnOption MeleeTurn(PersonalityProfile p, MeleeTurnOption roleChoice, in MeleeTurnOptionContext ctx, object actor = null)
        {
            if (p == null)
                return roleChoice;
            using (AiDecisionTimer.Open(AiTimeBucket.Personality, actor))
            {
                MeleeTurnOption best = ctx.IsOpen(roleChoice) ? roleChoice : DND35.AI.MeleeTurnOption.Attack;
                float roleScore = MeleeTurnScore(p, best, ctx);
                float threshold = roleScore + ctx.Margin;
                float bestScore = float.NegativeInfinity;
                MeleeTurnOption result = best;
                var order = new[] { DND35.AI.MeleeTurnOption.Attack, DND35.AI.MeleeTurnOption.AidOffense,
                    DND35.AI.MeleeTurnOption.AidDefense, DND35.AI.MeleeTurnOption.TotalDefense };
                foreach (MeleeTurnOption c in order)
                {
                    if (c == best || !ctx.IsOpen(c))
                        continue;
                    float s = MeleeTurnScore(p, c, ctx);
                    if (s >= threshold && s > bestScore)
                    {
                        result = c;
                        bestScore = s;
                    }
                }
                if (Tracing(actor))
                    Trace(actor, "Personality.MeleeTurnOption", result.ToString(), roleChoice.ToString(),
                        result == best ? 0f : bestScore - roleScore, result == roleChoice ? "Role choice kept" : "RoleDesire");
                return result;
            }
        }

        /// <summary>The creature overload: the Role's choice with no personality.</summary>
        public static MeleeTurnOption MeleeTurn(CharacterController self, MeleeTurnOption roleChoice, in MeleeTurnOptionContext ctx)
        {
            PersonalityProfile p = self != null ? self.Personality : null;
            return p == null ? roleChoice : MeleeTurn(p, roleChoice, ctx, self);
        }

        /// <summary>What a melee turn option does for the creature, as the catalog's roles: the attack Attack, aid offense Support, aid defense Support and Defend, total defense Defend.</summary>
        public static ActionRoles RolesOf(MeleeTurnOption option)
        {
            switch (option)
            {
                case DND35.AI.MeleeTurnOption.Attack: return ActionRoles.Attack;
                case DND35.AI.MeleeTurnOption.AidOffense: return ActionRoles.Support;
                case DND35.AI.MeleeTurnOption.AidDefense: return ActionRoles.Support | ActionRoles.Defend;
                case DND35.AI.MeleeTurnOption.TotalDefense: return ActionRoles.Defend;
                default: return ActionRoles.None;
            }
        }

        /// <summary>An option's value scaled by the traits' desire for its roles.</summary>
        public static float MeleeTurnScore(PersonalityProfile p, MeleeTurnOption option, in MeleeTurnOptionContext ctx)
        {
            float v = ctx.ValueOf(option);
            return p == null ? v : v * Mathf.Max(0f, 1f + MeleeTurnDesireGain * p.RoleShift(RolesOf(option)));
        }

        private static bool IsOpen(HealerApproachChoice c, in HealerApproachContext ctx)
        {
            switch (c)
            {
                case HealerApproachChoice.Kiter: return true;
                case HealerApproachChoice.MoveAndCast: return ctx.CanMoveAndCast;
                case HealerApproachChoice.PartialApproach: return ctx.CanApproachPartly;
                default: return false;
            }
        }

        private static float HealerApproachScore(PersonalityProfile p, HealerApproachChoice c, HealerApproachChoice roleChoice)
            => (c == roleChoice ? 1f : 0f) + AfterStandardDesireGain * p.RoleShift(RolesOf(c));

        private static bool IsOpen(AfterStandardActionChoice c, in AfterStandardActionContext ctx)
        {
            switch (c)
            {
                case AfterStandardActionChoice.Stay: return true;
                case AfterStandardActionChoice.Approach: return ctx.CanApproach;
                // AI-039, as the default and the kiter: a threatened creature backs away only while it can still fight
                // from range (a trait may still widen the distance from a foe that is close but does not threaten).
                case AfterStandardActionChoice.KeepDistance: return ctx.CanKeepDistance && (ctx.CanFightFromRange || !ctx.Threatened);
                default: return false;
            }
        }

        private static float AfterStandardScore(PersonalityProfile p, AfterStandardActionChoice c, AfterStandardActionChoice roleWish)
            => (c == roleWish ? 1f : 0f) + AfterStandardDesireGain * p.RoleShift(RolesOf(c));

        /// <summary>
        /// <paramref name="ctx"/> with a null Difficulty filled from <see cref="SettingsDifficulty"/> (Normal when unset);
        /// <paramref name="fromSettings"/> says it was filled.
        /// </summary>
        private static JudgeContext WithDifficulty(in JudgeContext ctx, out bool fromSettings)
        {
            JudgeContext c = ctx;
            fromSettings = !c.Difficulty.HasValue;
            if (fromSettings)
            {
                Func<AIService.AIDifficultyLevel> source = SettingsDifficulty;
                c.Difficulty = source != null ? source() : AIService.AIDifficultyLevel.Normal;
            }
            return c;
        }

        private static string Reason(in JudgeResult r, bool difficultyFromSettings)
            => difficultyFromSettings ? r.Reason + ", difficulty from settings" : r.Reason;

        /// <summary>True when a row for <paramref name="actor"/> would be kept; the caller builds the row's strings only then.</summary>
        private static bool Tracing(object actor) => AiDecisionTrace.HasOpenTurn(actor);

        private static void Trace(object actor, string site, string chosen, string legacy, float bonus, string reason)
            => AiDecisionTrace.Record(actor, site, chosen, legacy, bonus, reason);

        private static string Yn(bool b) => b ? "yes" : "no";

        private static string Fmt(float f) => f.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Join(IReadOnlyList<OutOfReachAlternative> list)
        {
            var parts = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
                parts[i] = list[i].ToString();
            return string.Join(">", parts);
        }
    }
}
