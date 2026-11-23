using System;
using System.Collections.Generic;
using DND35.AI.Knowledge;

namespace DND35.AI.Personality
{
    /// <summary>
    /// Builds a creature's personality from its spec (batch 14 step 3; docs/designs/personalities_batch14.md). Step 4 calls
    /// it from GameManager.InitializeNPCFromDefinition with NPCDefinition.AIPersonality, and the scenario harness from
    /// ActorSpec.Personality. The spec is validated by
    /// <see cref="PersonalityValidator"/>; its problems become one warning per definition (a definition spawned again
    /// does not warn again; the same definition with another spec, tier or alignment, such as a template variant, warns
    /// for itself); a spec with no surviving trait gives null, a creature that plays as its Role alone.
    /// </summary>
    public static class PersonalityFactory
    {
        private static readonly HashSet<string> Warned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The personality of <paramref name="spec"/> for a creature of <paramref name="stats"/> (its tier, CognitionTiers.Of,
        /// and its alignment), or null. <paramref name="warn"/> receives the one warning line; null logs it with
        /// Debug.LogWarning once per <paramref name="ownerId"/>, spec, tier, alignment and spells answer.
        /// <paramref name="hasCastableSpell"/> false warns on a Caster or Healer trait (C1); null skips that check.
        /// </summary>
        public static PersonalityProfile Create(string spec, CharacterStats stats, string ownerId, Action<string> warn = null,
            bool? hasCastableSpell = null)
            => Create(spec, CognitionTiers.Of(stats), stats != null ? stats.CharacterAlignment : Alignment.None, ownerId, warn,
                hasCastableSpell);

        /// <summary>The personality of <paramref name="spec"/> for a creature of <paramref name="tier"/> and <paramref name="alignment"/>, or null.</summary>
        public static PersonalityProfile Create(string spec, CognitionTier tier, Alignment alignment, string ownerId, Action<string> warn = null,
            bool? hasCastableSpell = null)
        {
            if (string.IsNullOrWhiteSpace(spec))
                return null;
            PersonalityValidation v = PersonalityValidator.Validate(spec, tier, alignment, hasCastableSpell);
            string warning = v.Warning(ownerId, spec.Trim());
            if (warning != null)
            {
                if (warn != null)
                    warn(warning);
                else if (Warned.Add(WarnKey(ownerId, spec, tier, alignment, hasCastableSpell)))
                    UnityEngine.Debug.LogWarning(warning);
            }
            return PersonalityProfile.Build(v.Traits, tier, spec.Trim());
        }

        /// <summary>
        /// Forgets which definitions already warned (tests). Also run at each Play session start, so the set does not
        /// carry over when the editor enters Play mode without a domain reload.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetWarnings() => Warned.Clear();

        private static string WarnKey(string ownerId, string spec, CognitionTier tier, Alignment alignment, bool? hasCastableSpell)
            => (ownerId ?? "") + "|" + spec.Trim() + "|" + tier + "|" + alignment + "|" + (hasCastableSpell?.ToString() ?? "-");
    }
}
