using System;
using System.Collections.Generic;
using System.Globalization;
using DND35.AI.Knowledge;

namespace DND35.AI.Personality
{
    /// <summary>What <see cref="PersonalityValidator.Validate"/> kept and what it reported.</summary>
    public sealed class PersonalityValidation
    {
        public readonly List<TraitWeight> Traits = new List<TraitWeight>();
        /// <summary>Each problem found, in order; the factory joins them into one warning per definition.</summary>
        public readonly List<string> Problems = new List<string>();

        public bool HasProblems => Problems.Count > 0;

        /// <summary>The one warning line for <paramref name="ownerId"/>, or null when there is no problem.</summary>
        public string Warning(string ownerId, string spec)
            => HasProblems
                ? "[Personality] " + (string.IsNullOrEmpty(ownerId) ? "?" : ownerId) + ": personality '" + spec + "': " + string.Join("; ", Problems)
                : null;
    }

    /// <summary>
    /// Checks a personality spec against a creature (batch 14 step 3; docs/designs/personalities_batch14.md "The seven
    /// starter traits"; design 4.1). In order:
    /// <list type="number">
    /// <item>parses "Id:weight" entries split by ',' or ';' (':' or '=' before the weight; a bare id weighs 1); an entry
    /// with a weight that is not a positive number, or a repeated id, is dropped;</item>
    /// <item>drops an unknown id, and a trait whose minimum tier is above the creature's;</item>
    /// <item>applies the Mindless lock: a kept ExclusiveLock trait removes every other trait;</item>
    /// <item>warns on an alignment mismatch and keeps the trait (owner, 2026-10-30: alignment only warns);</item>
    /// <item>when the caller says the creature has no castable spell, warns on a Caster or Healer trait
    /// (TraitDefinition.NeedsSpells) and keeps it: it still scores targets (C1; batch 14 step 4);</item>
    /// <item>scales the weights to 1 when they sum above 1; a shortfall is kept (C2: it belongs to the Role).</item>
    /// </list>
    /// Pure: it logs nothing; <see cref="PersonalityFactory"/> writes the problems as one warning per definition.
    /// </summary>
    public static class PersonalityValidator
    {
        private static readonly char[] EntrySeparators = { ',', ';' };
        private static readonly char[] WeightSeparators = { ':', '=' };

        /// <param name="hasCastableSpell">False: the creature has no castable spell (the spells check of C1); null: unknown, no check.</param>
        public static PersonalityValidation Validate(string spec, CognitionTier tier, Alignment alignment, bool? hasCastableSpell = null)
        {
            var result = new PersonalityValidation();
            if (string.IsNullOrWhiteSpace(spec))
                return result;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in spec.Split(EntrySeparators))
            {
                string entry = raw.Trim();
                if (entry.Length == 0)
                    continue;

                string id = entry;
                float weight = 1f;
                int sep = entry.IndexOfAny(WeightSeparators);
                if (sep >= 0)
                {
                    id = entry.Substring(0, sep).Trim();
                    string w = entry.Substring(sep + 1).Trim();
                    if (!float.TryParse(w, NumberStyles.Float, CultureInfo.InvariantCulture, out weight)
                        || float.IsNaN(weight) || float.IsInfinity(weight) || weight <= 0f)
                    {
                        result.Problems.Add("'" + entry + "' dropped: the weight is not a positive number");
                        continue;
                    }
                }

                TraitDefinition trait = TraitDatabase.Get(id);
                if (trait == null)
                {
                    result.Problems.Add("unknown trait '" + id + "' dropped");
                    continue;
                }
                if (!seen.Add(trait.Id))
                {
                    result.Problems.Add(trait.Id + " repeated: the later entry dropped");
                    continue;
                }
                if (tier < trait.MinTier)
                {
                    result.Problems.Add(trait.Id + " dropped: requires " + trait.MinTier + " tier (the creature is " + tier + ")");
                    continue;
                }
                result.Traits.Add(new TraitWeight(trait, weight));
            }

            ApplyExclusiveLock(result);
            WarnAlignment(result, alignment);
            if (hasCastableSpell == false)
                WarnNoSpells(result);
            ScaleAboveOne(result);
            return result;
        }

        private static void ApplyExclusiveLock(PersonalityValidation result)
        {
            int lockIndex = result.Traits.FindIndex(t => t.Trait.ExclusiveLock);
            if (lockIndex < 0 || result.Traits.Count == 1)
                return;
            TraitWeight locked = result.Traits[lockIndex];
            var dropped = new List<string>();
            foreach (TraitWeight t in result.Traits)
                if (t.Trait != locked.Trait)
                    dropped.Add(t.Trait.Id);
            result.Traits.Clear();
            result.Traits.Add(locked);
            result.Problems.Add(locked.Trait.Id + " lock: " + string.Join(", ", dropped) + " dropped");
        }

        private static void WarnAlignment(PersonalityValidation result, Alignment alignment)
        {
            if (alignment == Alignment.None)
                return;
            foreach (TraitWeight t in result.Traits)
                if (!Fits(t.Trait.Alignment, alignment))
                    result.Problems.Add(t.Trait.Id + " hints at " + t.Trait.Alignment + " but the creature is "
                                        + AlignmentHelper.GetAbbreviation(alignment) + " (kept: alignment only warns)");
        }

        private static void WarnNoSpells(PersonalityValidation result)
        {
            foreach (TraitWeight t in result.Traits)
                if (t.Trait.NeedsSpells)
                    result.Problems.Add(t.Trait.Id + " needs a castable spell and the creature has none (kept: it scores targets only; ENC-021)");
        }

        /// <summary>True when <paramref name="alignment"/> fits <paramref name="hint"/> (None fits everything).</summary>
        public static bool Fits(AlignmentHint hint, Alignment alignment)
        {
            switch (hint)
            {
                case AlignmentHint.Good: return AlignmentHelper.IsGood(alignment);
                case AlignmentHint.Evil: return AlignmentHelper.IsEvil(alignment);
                case AlignmentHint.Lawful: return AlignmentHelper.IsLawful(alignment);
                case AlignmentHint.Chaotic: return AlignmentHelper.IsChaotic(alignment);
                default: return true;
            }
        }

        private static void ScaleAboveOne(PersonalityValidation result)
        {
            float sum = 0f;
            foreach (TraitWeight t in result.Traits)
                sum += t.Weight;
            if (sum <= 1f + 1e-5f)
                return;
            for (int i = 0; i < result.Traits.Count; i++)
                result.Traits[i] = new TraitWeight(result.Traits[i].Trait, result.Traits[i].Weight / sum);
            result.Problems.Add("weights sum to " + sum.ToString("0.###", CultureInfo.InvariantCulture) + ", scaled to 1");
        }
    }
}
