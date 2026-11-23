namespace DND35.AI.Knowledge
{
    /// <summary>
    /// How much a creature can reason about a fight, from its Intelligence (batch 14 step 3; the AI design doc's 3.3,
    /// docs/designs/enemy_ai_knowledge_and_personalities.md). The tier caps reasoning, not perception: a personality trait
    /// above a creature's tier is dropped (PersonalityValidator), and batch 17 makes the tier filter facts. Ordered, so
    /// tiers compare with &lt; and &gt;. An AI tuning scale, not a rule: the books give Intelligence scores, not tiers.
    /// </summary>
    public enum CognitionTier
    {
        /// <summary>No Intelligence score (MM p.313: mindless; zombie, skeleton, ooze, vermin).</summary>
        Mindless = 0,
        /// <summary>Intelligence 1-2 (animal intelligence; wolf, bear). A score of 0 counts here too.</summary>
        Animal = 1,
        /// <summary>Intelligence 3-7 (ogre, troll).</summary>
        Low = 2,
        /// <summary>Intelligence 8-12 (orc, goblin, gnoll, bugbear).</summary>
        Average = 3,
        /// <summary>Intelligence 13-16 (a hobgoblin captain, most casters).</summary>
        Keen = 4,
        /// <summary>Intelligence 17 or more (adult dragons, liches).</summary>
        Brilliant = 5
    }

    /// <summary>The tier of a creature or an Intelligence score.</summary>
    public static class CognitionTiers
    {
        /// <summary>
        /// The tier of an Intelligence score: Mindless for <paramref name="mindless"/> or no score
        /// (CharacterStats.NO_SCORE, -1); Animal up to 2; Low 3-7; Average 8-12; Keen 13-16; Brilliant 17 and up.
        /// </summary>
        public static CognitionTier FromIntelligence(int intelligence, bool mindless)
        {
            if (mindless || intelligence == CharacterStats.NO_SCORE)
                return CognitionTier.Mindless;
            if (intelligence <= 2)
                return CognitionTier.Animal;
            if (intelligence <= 7)
                return CognitionTier.Low;
            if (intelligence <= 12)
                return CognitionTier.Average;
            if (intelligence <= 16)
                return CognitionTier.Keen;
            return CognitionTier.Brilliant;
        }

        /// <summary>
        /// The tier of <paramref name="stats"/>: its IsMindless flag, else its Intelligence score as it stands
        /// (CharacterStats.EffectiveINTScore: ability damage and an item's enhancement counted). Mindless without stats.
        /// </summary>
        public static CognitionTier Of(CharacterStats stats)
        {
            if (stats == null)
                return CognitionTier.Mindless;
            if (stats.IsMindless || !stats.HasAbilityScore(AbilityType.INT))
                return CognitionTier.Mindless;
            return FromIntelligence(stats.EffectiveINTScore, mindless: false);
        }
    }
}
