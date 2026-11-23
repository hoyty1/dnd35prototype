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
    /// passes only options the rules cores allow. Steps 5 and 6 call these at the target, coup de grace, charge, movement
    /// and out-of-reach sites.
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
        /// The game's AI difficulty, read when a JudgeContext leaves Difficulty null (C11, Q6). Step 4 points it at
        /// AIService.Difficulty (GameSettings.aiDifficulty); null means the setting's default, Normal. A test that sets it
        /// restores it.
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
