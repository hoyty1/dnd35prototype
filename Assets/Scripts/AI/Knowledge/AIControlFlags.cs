using System;
using System.Collections.Generic;

namespace DND35.AI.Knowledge
{
    /// <summary>
    /// Switches that steer a creature's own AI (batch 13 step 7), kept apart from the facts other creatures read about it.
    /// They come from the control tags, translated once at spawn (<see cref="AIControlFlagRules"/>; GameManager.
    /// InitializeNPCFromDefinition and the preset spawn overrides) onto CharacterController.AIControl, which the AI reads;
    /// a reused slot clears them (CharacterController.ResetForNewCreature, CRE-046).
    /// </summary>
    [Flags]
    public enum AIControlFlags
    {
        None = 0,
        /// <summary>"AI:MagicMissileOnly": the caster casts only Magic Missile (the arcane_missile_adept test creature).</summary>
        MagicMissileOnly = 1 << 0,
        /// <summary>"Uses Armor-Based Targeting": target choice by the target's armor (the armor-targeting test preset).</summary>
        ArmorPriorityTargeting = 1 << 1,
        /// <summary>A "ScenarioOverride:..." tag or a test preset's marker ("ShieldBashTestSimpleMeleeAI"): set by a test spawn override.</summary>
        ScenarioOverride = 1 << 2
    }

    /// <summary>The one translation from control tag spellings to <see cref="AIControlFlags"/>.</summary>
    public static class AIControlFlagRules
    {
        public const string MagicMissileOnlyTag = "AI:MagicMissileOnly";
        public const string ArmorPriorityTargetingTag = "Uses Armor-Based Targeting";
        public const string ScenarioOverridePrefix = "ScenarioOverride:";
        public const string ShieldBashTestTag = "ShieldBashTestSimpleMeleeAI";

        /// <summary>The flag a control tag sets, or None for a tag that is not a control tag (compared trimmed, case-insensitively).</summary>
        public static AIControlFlags FromTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return AIControlFlags.None;
            string t = tag.Trim();
            if (string.Equals(t, MagicMissileOnlyTag, StringComparison.OrdinalIgnoreCase))
                return AIControlFlags.MagicMissileOnly;
            if (string.Equals(t, ArmorPriorityTargetingTag, StringComparison.OrdinalIgnoreCase))
                return AIControlFlags.ArmorPriorityTargeting;
            if (t.StartsWith(ScenarioOverridePrefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, ShieldBashTestTag, StringComparison.OrdinalIgnoreCase))
                return AIControlFlags.ScenarioOverride;
            return AIControlFlags.None;
        }

        /// <summary>The flags of every control tag in <paramref name="tags"/>.</summary>
        public static AIControlFlags FromTags(IEnumerable<string> tags)
        {
            AIControlFlags flags = AIControlFlags.None;
            if (tags == null)
                return flags;
            foreach (string tag in tags)
                flags |= FromTag(tag);
            return flags;
        }
    }
}
