using System;

namespace DND35.AI.Knowledge
{
    /// <summary>
    /// Marks AI code that still reads another creature's true state directly instead of through the knowledge facade
    /// (<see cref="ITargetView"/>): the "[KnowledgeLegacy]" marker of docs/designs/enemy_ai_knowledge_and_personalities.md
    /// (Increment 1). Batch 17 (fair sight) converts or gates every marked member; until then a marked member is the known,
    /// named place where the AI is omniscient. Batch 14 step 8 adds the first: <see cref="DND35.AI.Valuation.TargetNumbers"/>,
    /// the one choke point for a target's check modifiers. Documentation only: nothing reads it at run time.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property,
        Inherited = false, AllowMultiple = false)]
    public sealed class KnowledgeLegacyAttribute : Attribute
    {
        /// <summary>Why the read is legacy and what replaces it.</summary>
        public string Reason { get; }

        public KnowledgeLegacyAttribute(string reason = null)
        {
            Reason = reason;
        }
    }
}
