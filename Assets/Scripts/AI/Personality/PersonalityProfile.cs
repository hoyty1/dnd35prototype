using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DND35.AI.Knowledge;
using UnityEngine;

namespace DND35.AI.Personality
{
    /// <summary>One trait of a personality with its given weight (after the validator).</summary>
    public readonly struct TraitWeight
    {
        public readonly TraitDefinition Trait;
        public readonly float Weight;

        public TraitWeight(TraitDefinition trait, float weight)
        {
            Trait = trait;
            Weight = weight;
        }
    }

    /// <summary>
    /// The outcome of a personality's vote on a yes/no decision (C5): a quorum Forbid or Insist, a vote Against or For, or
    /// Neutral, which leaves the Role's legacy answer.
    /// </summary>
    public enum Verdict
    {
        Forbid,
        Against,
        Neutral,
        For,
        Insist
    }

    /// <summary>A vote and how it came out, for the trace and the tests.</summary>
    public readonly struct JudgeResult
    {
        public readonly Verdict Verdict;
        /// <summary>v = sum of weight times opinion (Forbid -2, Insist +2).</summary>
        public readonly float Vote;
        public readonly float ForbidWeight;
        public readonly float InsistWeight;

        public JudgeResult(Verdict verdict, float vote, float forbidWeight, float insistWeight)
        {
            Verdict = verdict;
            Vote = vote;
            ForbidWeight = forbidWeight;
            InsistWeight = insistWeight;
        }

        public string Reason => Verdict + " (v " + Vote.ToString("0.00", CultureInfo.InvariantCulture)
                                + ", forbid " + ForbidWeight.ToString("0.00", CultureInfo.InvariantCulture)
                                + ", insist " + InsistWeight.ToString("0.00", CultureInfo.InvariantCulture) + ")";
    }

    /// <summary>
    /// A creature's weighted traits (batch 14 step 3; docs/designs/personalities_batch14.md C2-C7; design 4.2), built once
    /// at spawn by <see cref="PersonalityFactory"/> from a validated spec and never changed. A scorer, never a routine: it
    /// gives a bounded target term (<see cref="TargetTerm"/>), votes on yes/no decisions (<see cref="Judge"/>), tilts the
    /// Role's leftover choices (<see cref="RoleShift"/>) and sets the risk dial (<see cref="Rho"/>). The weights are the
    /// given ones (C2): a sum above 1 was scaled to 1, a shortfall belongs to the Role (Smart:0.5 is Smart at half
    /// strength), so the Role's share abstains from every vote and term and holds RiskAversion at the neutral 0.5.
    /// It reads targets only through ITargetView (OmniscienceLintTests).
    /// </summary>
    public sealed class PersonalityProfile
    {
        /// <summary>C4: the target term's scale; the term stays within plus or minus K.</summary>
        public const float K = 20f;

        /// <summary>C5: the summed given weight at which a Forbid or Insist decides alone.</summary>
        public const float Quorum = 0.5f;

        /// <summary>C5: a vote above this is yes, below its negative no; inside it the Role's legacy answer stands.</summary>
        public const float VoteThreshold = 0.15f;

        /// <summary>The RiskAversion of the Role's share of the weight (rho 1.0).</summary>
        public const float NeutralRiskAversion = 0.5f;

        /// <summary>Float tolerance of the quorum test (a weight of .5 parsed from text meets it).</summary>
        private const float QuorumEpsilon = 1e-5f;

        private static readonly int ConsiderationCount = Enum.GetValues(typeof(ConsiderationId)).Length;

        /// <summary>
        /// The single ActionRoles flags RoleDesire can name: every bit up to the enum's highest flag, derived so a new
        /// flag is covered without an edit here.
        /// </summary>
        public static readonly int RoleBits = CountRoleBits();

        private readonly List<TraitWeight> _traits;
        private readonly float[] _folded;
        private readonly float[] _desire;
        /// <summary>The role bits some trait of this profile names in its RoleDesire column.</summary>
        private readonly int _namedRoles;

        public IReadOnlyList<TraitWeight> Traits => _traits;

        /// <summary>The sum of the given weights, at most 1.</summary>
        public float TotalWeight { get; }

        /// <summary>The creature's tier when the profile was built.</summary>
        public CognitionTier Tier { get; }

        /// <summary>The spec the profile was built from, as given.</summary>
        public string Source { get; }

        /// <summary>The weighted RiskAversion: sum of w times RA, plus the Role's share at 0.5 (adept .6 Cowardly + .4 Healer: .72).</summary>
        public float RiskAversion { get; }

        /// <summary>C7: rho = lerp(0.4, 1.6, RiskAversion), 1.0 at 0.5; RiskAversion outside [0, 1] clamps.</summary>
        public float Rho => Mathf.Lerp(0.4f, 1.6f, RiskAversion);

        private PersonalityProfile(List<TraitWeight> traits, CognitionTier tier, string source)
        {
            _traits = traits;
            Tier = tier;
            Source = source;
            _folded = new float[ConsiderationCount];
            _desire = new float[RoleBits];
            float total = 0f;
            float ra = 0f;
            foreach (TraitWeight tw in traits)
            {
                total += tw.Weight;
                ra += tw.Weight * tw.Trait.RiskAversion;
                foreach (TargetWeight t in tw.Trait.Target)
                    _folded[(int)t.Consideration] += tw.Weight * t.Weight;
                foreach (RoleDesire d in tw.Trait.RoleDesires)
                {
                    int bit = BitOf(d.Role);
                    if (bit >= 0)
                    {
                        _desire[bit] += tw.Weight * d.Desire;
                        _namedRoles |= 1 << bit;
                    }
                }
            }
            TotalWeight = total;
            RiskAversion = ra + Mathf.Max(0f, 1f - total) * NeutralRiskAversion;
        }

        /// <summary>
        /// A profile of <paramref name="traits"/> (validated: known ids, positive weights summing to at most 1; see
        /// PersonalityValidator), or null when there is none.
        /// </summary>
        public static PersonalityProfile Build(IReadOnlyList<TraitWeight> traits, CognitionTier tier, string source)
        {
            if (traits == null || traits.Count == 0)
                return null;
            var list = new List<TraitWeight>(traits.Count);
            foreach (TraitWeight tw in traits)
                if (tw.Trait != null && tw.Weight > 0f)
                    list.Add(tw);
            return list.Count == 0 ? null : new PersonalityProfile(list, tier, source);
        }

        /// <summary>The weight of trait <paramref name="id"/>, 0 when absent.</summary>
        public float WeightOf(string id)
        {
            foreach (TraitWeight tw in _traits)
                if (string.Equals(tw.Trait.Id, id, StringComparison.OrdinalIgnoreCase))
                    return tw.Weight;
            return 0f;
        }

        /// <summary>The folded weight e_j = sum over traits of w_i times the trait's weight of <paramref name="id"/> (C4).</summary>
        public float FoldedWeight(ConsiderationId id) => _folded[(int)id];

        /// <summary>
        /// The trait term of a target (C4): K times the sum of e_j times consideration j of <paramref name="view"/>,
        /// within plus or minus K. 0 without a view.
        /// </summary>
        public float TargetTerm(ITargetView view)
        {
            if (view == null)
                return 0f;
            float sum = 0f;
            for (int j = 0; j < _folded.Length; j++)
            {
                float e = _folded[j];
                if (e != 0f)
                    sum += e * Considerations.Evaluate((ConsiderationId)j, view);
            }
            return Mathf.Clamp(K * sum, -K, K);
        }

        /// <summary>
        /// The vote on <paramref name="decision"/> (C5 steps 3-4; the rules gates, the data override and the legacy answer
        /// are AiBlend.Judge's): Forbid when traits with a summed weight of at least .5 forbid it, Insist likewise; an
        /// exact tie of both falls through. Otherwise v = sum of w times opinion: above .15 For, below -.15 Against, else
        /// Neutral.
        /// </summary>
        public JudgeResult Judge(BoolDecision decision, in JudgeContext ctx)
        {
            float forbid = 0f;
            float insist = 0f;
            float vote = 0f;
            foreach (TraitWeight tw in _traits)
            {
                Opinion o = tw.Trait.OpinionOn(decision, ctx);
                if (o == Opinion.Forbid)
                    forbid += tw.Weight;
                else if (o == Opinion.Insist)
                    insist += tw.Weight;
                vote += tw.Weight * (int)o;
            }

            bool forbidQuorum = forbid >= Quorum - QuorumEpsilon;
            bool insistQuorum = insist >= Quorum - QuorumEpsilon;
            Verdict verdict;
            if (forbidQuorum && insistQuorum)
                verdict = Verdict.Neutral;
            else if (forbidQuorum)
                verdict = Verdict.Forbid;
            else if (insistQuorum)
                verdict = Verdict.Insist;
            else if (vote > VoteThreshold)
                verdict = Verdict.For;
            else if (vote < -VoteThreshold)
                verdict = Verdict.Against;
            else
                verdict = Verdict.Neutral;
            return new JudgeResult(verdict, vote, forbid, insist);
        }

        /// <summary>
        /// How much the traits want an action of <paramref name="roles"/> (C8): for each flag, the sum of w times the
        /// trait's RoleDesire; for several flags (a move is Approach and Retreat), the best of the flags some trait names,
        /// so an action counts by what it can do for the creature. A flag no trait names abstains (it is not a desire of
        /// 0), so a Berserker's Retreat -.8 reaches a withdraw (Retreat and Escape). 0 for None or when no flag is named.
        /// </summary>
        public float RoleShift(ActionRoles roles)
        {
            int named = (int)roles & _namedRoles;
            if (named == 0)
                return 0f;
            float best = float.NegativeInfinity;
            for (int bit = 0; bit < RoleBits; bit++)
                if ((named & (1 << bit)) != 0)
                    best = Mathf.Max(best, _desire[bit]);
            return best;
        }

        /// <summary>
        /// The traits' change to the Role's preferred range in squares (C3; batch 14 step 6): the sum of each trait's
        /// PreferredRangeAdjust, not scaled by its weight (design 4.2: a range is adjusted by traits, never averaged, since
        /// a weighted range strands a reach-1 creature between two distances). A trait whose adjust applies only to a
        /// Ranged, Mixed or casting Role (Cowardly +2) counts only when <paramref name="rangedOrCastingRole"/>.
        /// </summary>
        public int RangeAdjust(bool rangedOrCastingRole)
        {
            int sum = 0;
            foreach (TraitWeight tw in _traits)
                if (!tw.Trait.RangeAdjustOnlyRangedOrCasting || rangedOrCastingRole)
                    sum += tw.Trait.PreferredRangeAdjust;
            return sum;
        }

        /// <summary>True when <paramref name="role"/> is one ActionRoles flag that a RoleDesire can name (TraitDatabase checks its rows).</summary>
        public static bool IsSingleRole(ActionRoles role) => BitOf(role) >= 0;

        /// <summary>The traits with their weights, as the trace shows them: "Cowardly .60, Healer .40".</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _traits.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(_traits[i].Trait.Id).Append(' ').Append(_traits[i].Weight.ToString(".00", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public override string ToString() => Describe();

        private static int BitOf(ActionRoles role)
        {
            int v = (int)role;
            if (v == 0 || (v & (v - 1)) != 0)
                return -1;
            int bit = 0;
            while ((v >>= 1) != 0)
                bit++;
            return bit < RoleBits ? bit : -1;
        }

        private static int CountRoleBits()
        {
            int all = 0;
            foreach (ActionRoles r in Enum.GetValues(typeof(ActionRoles)))
                all |= (int)r;
            int bits = 0;
            while (all != 0)
            {
                bits++;
                all >>= 1;
            }
            return bits;
        }
    }
}
